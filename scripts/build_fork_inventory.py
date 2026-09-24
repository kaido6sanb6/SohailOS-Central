#!/usr/bin/env python3
"""Build the owner-fork inventory without guessing provenance."""
import json, os, urllib.request
from datetime import datetime, timezone

OWNER=os.environ.get("SOHAILOS_GITHUB_OWNER","kaido6sanb6")
TOKEN=os.environ.get("GITHUB_TOKEN")
OUT=os.environ.get("SOHAILOS_FORK_INVENTORY_PATH","ecosystem/generated/forks.json")

def request(url):
    req=urllib.request.Request(url,headers={"Accept":"application/vnd.github+json","X-GitHub-Api-Version":"2022-11-28"})
    if TOKEN: req.add_header("Authorization",f"Bearer {TOKEN}")
    with urllib.request.urlopen(req,timeout=60) as r: return json.load(r)

repos=[]; page=1
while True:
    batch=request(f"https://api.github.com/users/{OWNER}/repos?per_page=100&page={page}&type=owner")
    if not batch: break
    repos.extend(batch)
    if len(batch)<100: break
    page+=1

now=datetime.now(timezone.utc).isoformat(); forks=[]
for repo in repos:
    if not repo.get("fork"): continue
    parent=repo.get("parent") or {}; source=repo.get("source") or {}
    if not parent.get("full_name"):
        details=request(f"https://api.github.com/repos/{repo['full_name']}")
        parent=details.get("parent") or {}; source=details.get("source") or source
    forks.append({
        "id":repo.get("id"),"full_name":repo.get("full_name"),"name":repo.get("name"),
        "default_branch":repo.get("default_branch") or "main","html_url":repo.get("html_url"),
        "upstream":parent.get("full_name"),"source":source.get("full_name"),
        "private":bool(repo.get("private")),"archived":bool(repo.get("archived")),
        "license":(repo.get("license") or {}).get("spdx_id"),
        "updated_at":repo.get("updated_at"),"retrieved_at":now
    })
forks.sort(key=lambda x:(x["full_name"] or "").lower())
os.makedirs(os.path.dirname(OUT),exist_ok=True)
json.dump({"schema_version":"2.0","owner":OWNER,"generated_at":now,"fork_count":len(forks),"forks":forks},
          open(OUT,"w",encoding="utf-8"),ensure_ascii=False,indent=2)
print(f"generated {len(forks)} fork records -> {OUT}")
