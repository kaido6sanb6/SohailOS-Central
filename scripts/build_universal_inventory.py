#!/usr/bin/env python3
"""Build a provenance-preserving GitHub inventory for SohailOS-Central."""
import json, os, urllib.request
from datetime import datetime, timezone

OWNER = os.environ.get("SOHAILOS_GITHUB_OWNER", "kaido6sanb6")
TOKEN = os.environ.get("GITHUB_TOKEN")
OUT = os.environ.get("SOHAILOS_INVENTORY_PATH", "ecosystem/generated/repositories.json")

def request(url):
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json"})
    if TOKEN:
        req.add_header("Authorization", f"Bearer {TOKEN}")
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)

repos, page = [], 1
while True:
    batch = request(f"https://api.github.com/users/{OWNER}/repos?per_page=100&page={page}&type=all")
    if not batch: break
    repos.extend(batch)
    if len(batch) < 100: break
    page += 1

now = datetime.now(timezone.utc).isoformat()
items = []
for r in repos:
    parent = r.get("parent") or {}
    items.append({
        "id": r.get("id"),
        "name": r.get("name"),
        "full_name": r.get("full_name"),
        "default_branch": r.get("default_branch") or "main",
        "html_url": r.get("html_url"),
        "private": bool(r.get("private")),
        "fork": bool(r.get("fork")),
        "archived": bool(r.get("archived")),
        "license": (r.get("license") or {}).get("spdx_id"),
        "upstream": parent.get("full_name"),
        "updated_at": r.get("updated_at"),
        "source": "github-owner-api",
        "retrieved_at": now,
        "trust": "unverified"
    })

items.sort(key=lambda x: (x["full_name"] or "").lower())
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=2)
print(f"generated {len(items)} repository records -> {OUT}")
