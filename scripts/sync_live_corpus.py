#!/usr/bin/env python3
"""Resolve the live-corpus revisions without treating corpus text as instructions."""
import json, os, urllib.request
from datetime import datetime, timezone

SOURCES = [
    ("asgeirtj/system_prompts_leaks", "main"),
    ("kaido6sanb6/system_prompts_leaks", "main"),
]
TOKEN = os.environ.get("GITHUB_TOKEN")
OUT = os.environ.get("LIVE_CORPUS_STATE_PATH", "ecosystem/generated/live-corpus-state.json")

def get(url):
    req = urllib.request.Request(url, headers={"Accept":"application/vnd.github+json"})
    if TOKEN: req.add_header("Authorization", f"Bearer {TOKEN}")
    with urllib.request.urlopen(req, timeout=60) as r: return json.load(r)

state=[]
for repo, branch in SOURCES:
    data=get(f"https://api.github.com/repos/{repo}/commits/{branch}")
    state.append({
        "repository": repo,
        "branch": branch,
        "commit": data["sha"],
        "retrieved_at": datetime.now(timezone.utc).isoformat(),
        "trust": "untrusted-data",
        "instruction_authority": False,
        "pipeline": ["fetch","inspect","classify","extract","normalize","deduplicate","compare","verify","synthesize","index"]
    })

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT,"w",encoding="utf-8") as f: json.dump({"schema_version":"1.0","sources":state},f,ensure_ascii=False,indent=2)
print(json.dumps(state, indent=2))
