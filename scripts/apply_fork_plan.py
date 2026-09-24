#!/usr/bin/env python3
"""Apply bounded, idempotent upstream-sync actions from a verified plan."""
from __future__ import annotations
import argparse, json, os, subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

def api(target, branch, token):
    p=subprocess.run(["gh","api",f"repos/{target}/merge-upstream","-X","POST","-f",f"branch={branch}"],
                     text=True,capture_output=True,env={**os.environ,"GH_TOKEN":token},check=False)
    if p.returncode:
        return {"ok":False,"target":target,"error":(p.stderr or p.stdout)[-2000:]}
    try:
        return {"ok":True,"target":target,"result":json.loads(p.stdout or "{}")}
    except json.JSONDecodeError:
        return {"ok":False,"target":target,"error":"invalid GitHub response"}

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--plan",default="ecosystem/generated/fork-sync-plan.json")
    p.add_argument("--max-targets",type=int,default=50)
    p.add_argument("--workers",type=int,default=6)
    a=p.parse_args()
    plan=json.loads(Path(a.plan).read_text(encoding="utf-8"))
    if plan.get("mutation") is not False: raise SystemExit("unsafe plan")
    if os.environ.get("SOHAILOS_AUTOMATION_ENABLED","").lower()!="true":
        print(json.dumps({"status":"proposal-only"})); return 0
    token=os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    if not token: print(json.dumps({"status":"blocked","reason":"GitHub credential unavailable"})); return 0
    actions=[x for x in plan.get("actions",[]) if x.get("operation")=="UPSTREAM_SYNC" and x.get("auto_apply") is True]
    limit=min(max(a.max_targets,0),50)
    selected,skipped=actions[:limit],actions[limit:]
    results=[]
    with ThreadPoolExecutor(max_workers=max(1,min(a.workers,6))) as pool:
        futures={pool.submit(api,x["target"],x["branch"],token):x for x in selected}
        for f in as_completed(futures): results.append(f.result())
    failed=sum(not x["ok"] for x in results)
    print(json.dumps({"attempted":len(results),"succeeded":len(results)-failed,"failed":failed,
                      "deferred":len(skipped),"retry":"reconcile-next-run"},indent=2))
    return 0 if failed==0 else 2
if __name__=="__main__": raise SystemExit(main())
