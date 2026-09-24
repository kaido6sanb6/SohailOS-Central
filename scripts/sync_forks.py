#!/usr/bin/env python3
"""Read-only upstream divergence detector for forks. Never mutates repositories."""
import json, os, subprocess
from datetime import datetime, timezone
INVENTORY=os.environ.get("SOHAILOS_FORK_INVENTORY_PATH","ecosystem/generated/forks.json")
REPORT=os.environ.get("SOHAILOS_FORK_SYNC_REPORT_PATH","ecosystem/generated/fork-sync-report.json")
def now(): return datetime.now(timezone.utc).isoformat()
def compare(repo, branch, upstream, token):
    owner=repo.split("/")[0]; up_owner,up_name=upstream.split("/",1)
    env={**os.environ,"GH_TOKEN":token}
    endpoint=f"repos/{upstream}/compare/{branch}...{repo}"
    r=subprocess.run(["gh","api",endpoint],text=True,capture_output=True,env=env,check=False)
    if r.returncode!=0:
        return {"repo":repo,"branch":branch,"upstream":upstream,"status":"error","error":(r.stderr or r.stdout)[:2000],"mutation":False}
    try: data=json.loads(r.stdout or "{}")
    except json.JSONDecodeError: return {"repo":repo,"branch":branch,"upstream":upstream,"status":"error","error":"invalid API response","mutation":False}
    return {"repo":repo,"branch":branch,"upstream":upstream,"status":"ahead" if data.get("ahead_by",0)>0 else "in_sync","ahead_by":data.get("ahead_by",0),"behind_by":data.get("behind_by",0),"changed_files":data.get("files") and len(data["files"]) or 0,"commits":data.get("total_commits",0),"retrieved_at":now(),"mutation":False}
def main():
    data=json.load(open(INVENTORY,encoding="utf-8")); token=os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    results=[]
    for f in data.get("forks",[]):
        repo=f.get("full_name"); upstream=f.get("upstream"); branch=f.get("default_branch") or "main"
        if not repo or not upstream: results.append({"repo":repo,"upstream":upstream,"status":"blocked","reason":"incomplete fork provenance","mutation":False}); continue
        if not token: results.append({"repo":repo,"branch":branch,"upstream":upstream,"status":"blocked","reason":"GitHub credential unavailable","mutation":False}); continue
        results.append(compare(repo,branch,upstream,token))
    summary={s:sum(r["status"]==s for r in results) for s in ("ahead","in_sync","error","blocked")}
    out={"schema_version":"2.0","generated_at":now(),"owner":data.get("owner"),"fork_count":len(results),"read_only":True,"results":results,"summary":summary}
    os.makedirs(os.path.dirname(REPORT),exist_ok=True); json.dump(out,open(REPORT,"w",encoding="utf-8"),ensure_ascii=False,indent=2)
    print(json.dumps(summary,indent=2))
if __name__=="__main__": main()
