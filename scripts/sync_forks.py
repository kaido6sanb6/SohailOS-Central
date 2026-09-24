#!/usr/bin/env python3
"""Read-only GitHub fork reconciliation. Never mutates repositories."""
import argparse, json, os, subprocess
from datetime import datetime, timezone
INVENTORY=os.environ.get("SOHAILOS_FORK_INVENTORY_PATH","ecosystem/generated/forks.json")
REPORT=os.environ.get("SOHAILOS_FORK_SYNC_REPORT_PATH","ecosystem/generated/fork-sync-report.json")

def now(): return datetime.now(timezone.utc).isoformat()
def compare(repo, branch, upstream, token):
    env={**os.environ,"GH_TOKEN":token}
    r=subprocess.run(["gh","api",f"repos/{upstream}/compare/{branch}...{repo}"],text=True,capture_output=True,env=env,check=False)
    if r.returncode:
        return {"repo":repo,"branch":branch,"upstream":upstream,"status":"error","error":(r.stderr or r.stdout)[:2000],"mutation":False}
    try: data=json.loads(r.stdout or "{}")
    except json.JSONDecodeError: return {"repo":repo,"branch":branch,"upstream":upstream,"status":"error","error":"invalid API response","mutation":False}
    base=(data.get("base_commit") or {}).get("sha"); head=(data.get("head_commit") or {}).get("sha")
    ahead=int(data.get("ahead_by",0)); behind=int(data.get("behind_by",0))
    status="diverged" if ahead and behind else "ahead" if ahead else "behind" if behind else "in_sync"
    return {"repo":repo,"branch":branch,"upstream":upstream,"status":status,"ahead_by":ahead,"behind_by":behind,
            "upstream_sha":base,"target_sha":head,"changed_files":len(data.get("files") or []),
            "commits":data.get("total_commits",0),"retrieved_at":now(),"mutation":False}
def main():
    ap=argparse.ArgumentParser(); ap.add_argument("--fail-on-removal",action="store_true"); a=ap.parse_args()
    data=json.load(open(INVENTORY,encoding="utf-8")); token=os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    results=[]
    for f in data.get("forks",[]):
        repo,up,branch=f.get("full_name"),f.get("upstream"),f.get("default_branch") or "main"
        if not repo or not up: results.append({"repo":repo,"upstream":up,"status":"blocked","reason":"incomplete fork provenance","mutation":False}); continue
        if not token: results.append({"repo":repo,"branch":branch,"upstream":up,"status":"blocked","reason":"GitHub credential unavailable","mutation":False}); continue
        results.append(compare(repo,branch,up,token))
    summary={s:sum(r["status"]==s for r in results) for s in ("behind","ahead","diverged","in_sync","error","blocked")}
    out={"schema_version":"3.0","generated_at":now(),"owner":data.get("owner"),"fork_count":len(results),
         "read_only":True,"results":results,"summary":summary}
    os.makedirs(os.path.dirname(REPORT),exist_ok=True)
    json.dump(out,open(REPORT,"w",encoding="utf-8"),ensure_ascii=False,indent=2)
    if a.fail_on_removal and len(results)!=data.get("fork_count"): raise SystemExit("fork inventory/reconciliation count mismatch")
    print(json.dumps(summary,indent=2))
if __name__=="__main__": main()
