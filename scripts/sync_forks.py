#!/usr/bin/env python3
"""Synchronize all owner forks with their upstream default branches.

The script is intentionally non-destructive: it never force-pushes and records
conflicts/errors in a machine-readable report. A separate workflow gate decides
whether the run is complete.
"""

import json
import os
import subprocess
from datetime import datetime, timezone

INVENTORY = os.environ.get(
    "SOHAILOS_FORK_INVENTORY_PATH",
    "ecosystem/generated/forks.json",
)
REPORT = os.environ.get(
    "SOHAILOS_FORK_SYNC_REPORT_PATH",
    "ecosystem/generated/fork-sync-report.json",
)


def now():
    return datetime.now(timezone.utc).isoformat()


def run_sync(repo, branch, token):
    completed = subprocess.run(
        [
            "gh",
            "api",
            "--method",
            "POST",
            f"repos/{repo}/merge-upstream",
            "-f",
            f"branch={branch}",
        ],
        text=True,
        capture_output=True,
        env={**os.environ, "GH_TOKEN": token},
        check=False,
    )
    if completed.returncode == 0:
        try:
            payload = json.loads(completed.stdout or "{}")
        except json.JSONDecodeError:
            payload = {}
        return {
            "repo": repo,
            "branch": branch,
            "status": "synced",
            "message": payload.get("message"),
            "merge_type": payload.get("merge_type"),
        }

    error = (completed.stderr or completed.stdout or "").strip()
    status = (
        "conflict"
        if "409" in error or "conflict" in error.lower()
        else "error"
    )
    return {
        "repo": repo,
        "branch": branch,
        "status": status,
        "error": error[:2000],
    }


def main():
    with open(INVENTORY, encoding="utf-8") as handle:
        data = json.load(handle)

    forks = data.get("forks", [])
    token = os.environ.get("GH_TOKEN")
    results = []

    if not token:
        results = [
            {
                "repo": fork.get("full_name"),
                "branch": fork.get("default_branch"),
                "upstream": fork.get("upstream"),
                "status": "blocked",
                "reason": "SOHAILOS_GITHUB_SYNC_TOKEN is not configured",
            }
            for fork in forks
        ]
    else:
        for fork in forks:
            repo = fork.get("full_name")
            branch = fork.get("default_branch") or "main"
            upstream = fork.get("upstream")

            if not repo:
                results.append(
                    {"status": "error", "reason": "fork record has no full_name"}
                )
                continue

            if not upstream:
                results.append(
                    {
                        "repo": repo,
                        "branch": branch,
                        "status": "skipped",
                        "reason": "upstream_not_exposed_by_github",
                    }
                )
                continue

            result = run_sync(repo, branch, token)
            result["upstream"] = upstream
            results.append(result)

    summary = {
        "synced": sum(r["status"] == "synced" for r in results),
        "conflict": sum(r["status"] == "conflict" for r in results),
        "error": sum(r["status"] == "error" for r in results),
        "skipped": sum(r["status"] == "skipped" for r in results),
        "blocked": sum(r["status"] == "blocked" for r in results),
    }

    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as handle:
        json.dump(
            {
                "schema_version": "1.1",
                "generated_at": now(),
                "owner": data.get("owner"),
                "fork_count": len(forks),
                "results": results,
                "summary": summary,
            },
            handle,
            ensure_ascii=False,
            indent=2,
        )

    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
