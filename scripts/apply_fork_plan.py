#!/usr/bin/env python3
"""Apply only bounded, idempotent upstream-sync actions from a verified plan."""
from __future__ import annotations
import argparse, json, os, subprocess
from pathlib import Path

def api(target, branch, token):
    p=subprocess.run(["gh","api",f"repos/{target}/merge-upstream","-X","POST","-f",f"branch={branch}"],
                     text=True,capture_output=True,env={**os.environ,"GH_TOKEN":token},check=False)
    if p.returncode: return {"ok":False,"target":target,"error":(p.stderr or p.stdout)[-2000:]}
    try: return {"ok":True,"target":target,"result":json.loads(p.stdout or "{}")}
    except json.JSONDecodeError: return {"ok":False,"target":target,"error":"invalid GitHub response"}

def main():
    p=argparse.ArgumentParser(); p.add_argument("--plan",default="ecosystem/generated/fork-sync-plan.json"); p.add_argument("--max-targets",type=int,default=50); a=p.parse_args()
    plan=json.loads(Path(a.plan).read_text(encoding="utf-8"))
    if plan.get("mutation") is not False: raise SystemExit("unsafe plan")
    if os.environ.get("SOHAILOS_AUTOMATION_ENABLED","").lower()!="true":
        print("automation disabled; proposal-only"); return 0
    token=os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    if not token: raise SystemExit("GitHub credential unavailable")
    actions=[x for x in plan.get("actions",[]) if x.get("operation")=="UPSTREAM_SYNC" and x.get("auto_apply") is True]
    if len(actions)>min(a.max_targets,50): raise SystemExit("safety cap exceeded")
    results=[api(x["target"],x["branch"],token) for x in actions]
    failed=sum(not x["ok"] for x in results)
    print(json.dumps({"attempted":len(results),"succeeded":len(results)-failed,"failed":failed,"retry":"reconcile-next-run"},indent=2))
    return 0 if failed==0 else 2
if __name__=="__main__": raise SystemExit(main())
