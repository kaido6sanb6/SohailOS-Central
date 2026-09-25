#!/usr/bin/env python3
"""Build an auditable policy-aware fork graph and mutation plan. Never mutates GitHub."""
from __future__ import annotations
import argparse, hashlib, json
from datetime import datetime, timezone
from pathlib import Path

def now(): return datetime.now(timezone.utc).isoformat()
def load(p): return json.loads(Path(p).read_text(encoding="utf-8"))
def op_id(op, repo, source, target): return hashlib.sha256(f"{op}|{repo}|{source}|{target}".encode()).hexdigest()[:24]

def classify(record, result):
    repo, upstream = record.get("full_name"), record.get("upstream")
    if not repo or not upstream: return "blocked", False, "missing verified fork provenance"
    if record.get("archived"): return "quarantine", False, "target repository is archived"
    if result.get("status") in {"error", "blocked"}: return "quarantine", False, result.get("reason", "comparison unavailable")
    ahead, behind = int(result.get("ahead_by", 0)), int(result.get("behind_by", 0))
    if ahead == 0 and behind > 0: return "upstream_sync", True, "strictly behind upstream with no local divergence"
    if ahead > 0 and behind > 0: return "quarantine", False, "fork and upstream diverged; preserve local work"
    if ahead > 0: return "observe", False, "fork contains local commits; no automatic overwrite"
    return "in_sync", False, "no upstream delta"

def build(inv, report, policy):
    by_repo = {r.get("repo"): r for r in report.get("results", [])}
    nodes, edges, actions = [], [], []
    for record in inv.get("forks", []):
        repo = record.get("full_name")
        result = by_repo.get(repo, {"status": "blocked", "reason": "missing comparison result"})
        status, auto, reason = classify(record, result)
        nodes.append({"id": repo or f"missing-{len(nodes)}", "kind": "repository", "fork": True,
                      "trust": "verified" if record.get("upstream") else "unverified", "status": status})
        if record.get("upstream"): edges.append({"from": repo, "to": record["upstream"], "type": "fork-of"})
        source, target = str(result.get("upstream_sha", "unknown")), str(result.get("target_sha", "unknown"))
        actions.append({"operation_id": op_id("UPSTREAM_SYNC" if auto else "OBSERVE", repo or "missing", source, target),
                        "operation": "UPSTREAM_SYNC" if auto else "OBSERVE", "target": repo,
                        "upstream": record.get("upstream"), "branch": record.get("default_branch") or "main",
                        "source_sha": source, "target_sha": target, "idempotent": bool(auto),
                        "reversible": bool(auto), "auto_apply": bool(auto), "reason": reason})
    return {"schema_version": "1.0", "generated_at": now(), "policy_id": policy["policy_id"],
            "hub": policy["hub"], "inventory_count": len(inv.get("forks", [])), "nodes": nodes,
            "edges": edges, "actions": actions, "mutation": False,
            "safety": {"cross_repository_code_merge": "proposal_only",
                       "arbitrary_fork_to_fork_merge": "forbidden", "conflict_overwrite": "forbidden"}}

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--inventory", default="ecosystem/generated/forks.json")
    p.add_argument("--report", default="ecosystem/generated/fork-sync-report.json")
    p.add_argument("--policy", default="ecosystem/policies/fork-propagation.json")
    p.add_argument("--graph", default="ecosystem/generated/fork-graph.json")
    p.add_argument("--plan", default="ecosystem/generated/fork-sync-plan.json")
    a = p.parse_args()
    inv, rep, pol = load(a.inventory), load(a.report), load(a.policy)
    if rep.get("fork_count") != inv.get("fork_count"): raise SystemExit("inventory/report count mismatch")
    plan = build(inv, rep, pol)
    Path(a.graph).parent.mkdir(parents=True, exist_ok=True)
    Path(a.plan).parent.mkdir(parents=True, exist_ok=True)
    Path(a.graph).write_text(json.dumps({"schema_version": "1.0", "generated_at": plan["generated_at"],
                                         "nodes": plan["nodes"], "edges": plan["edges"]}, indent=2) + "\n",
                             encoding="utf-8")
    Path(a.plan).write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"inventory_count": plan["inventory_count"], "actions": len(plan["actions"]),
                      "auto_apply": sum(x["auto_apply"] for x in plan["actions"]), "mutation": False}, indent=2))

if __name__ == "__main__":
    main()
