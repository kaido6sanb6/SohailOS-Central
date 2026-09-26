#!/usr/bin/env python3
"""Local, GitHub-Actions-independent entrypoint for verification and corpus tooling."""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def run(command: list[str], *, cwd: Path = ROOT) -> int:
    print("$", " ".join(command))
    return subprocess.run(command, cwd=cwd, check=False).returncode


def verify() -> int:
    checks = [
        [sys.executable, "security/verify_superprompt_sync.py"],
        [sys.executable, "security/deep_operational_hardening.py"],
        [sys.executable, "security/ai_security_regression.py"],
    ]
    for command in checks:
        if run(command) != 0:
            return 1
    dotnet = os.environ.get("DOTNET", "dotnet")
    return 0 if run(
        [dotnet, "test", "tests/SohailOS.Tests/SohailOS.Tests.csproj", "--configuration", "Release"]
    ) == 0 else 1


def fork_plan() -> int:
    return run([sys.executable, "scripts/fork_ecosystem.py"])


def fork_apply() -> int:
    return run([sys.executable, "scripts/apply_fork_plan.py"])


def corpus_sync() -> int:
    return run([sys.executable, "scripts/sync_system_prompts_leaks.py"])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("verify", help="Run repository verification locally.")
    sub.add_parser("fork-plan", help="Build a read-only fork synchronization plan.")
    sub.add_parser("fork-apply", help="Apply an already verified fork plan when explicitly enabled.")
    sub.add_parser("corpus-sync", help="Mirror the external system-prompt corpus locally.")
    args = parser.parse_args()
    return {
        "verify": verify,
        "fork-plan": fork_plan,
        "fork-apply": fork_apply,
        "corpus-sync": corpus_sync,
    }[args.command]()


if __name__ == "__main__":
    raise SystemExit(main())
