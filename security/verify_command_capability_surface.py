#!/usr/bin/env python3
"""Verify cross-file governance contracts without external dependencies."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(relative: str) -> dict:
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))


def main() -> None:
    architecture = load_json("ecosystem/architecture-v2.json")
    lifecycle = load_json("ecosystem/lifecycle-contract.json")
    commands = load_json("ecosystem/command-capability-surface.json")
    skills = load_json("ecosystem/skills-registry.json")

    expected_lifecycle = [
        "understand", "classify", "discover", "probe", "least_privilege",
        "plan", "preview", "approve", "execute", "reconcile", "verify",
        "validate", "deliver", "feedback",
    ]
    assert architecture["orchestrator_lifecycle"] == expected_lifecycle
    assert architecture["lifecycle_contract"] == "ecosystem/lifecycle-contract.json"
    assert architecture["command_plane"]["surface_contract"] == (
        "ecosystem/command-capability-surface.json"
    )
    assert architecture["skills_fabric"]["registry"] == "ecosystem/skills-registry.json"

    assert lifecycle["authority_model"] == [
        "System", "Developer", "Security", "Tool", "User", "ExternalData"
    ]
    assert "Command != Capability" in lifecycle["invariants"]
    assert "Inference != Authorization" in lifecycle["invariants"]

    assert commands["user_slash_commands_policy"]["native_only_when_repository_evidence_exists"]
    assert commands["user_slash_commands_policy"]["unknown_commands"] == "block"
    assert "USER_CONFIRMATION_REQUIRED" in commands["implementation_status"]

    assert skills["skills_sh_api"]["refresh_interval_minutes"] == 15
    assert skills["installation_policy"]["install_is_not_trust"] is True
    assert skills["installation_policy"]["external_skill_content_is_untrusted"] is True
    assert skills["requested_skill_count"] == len(skills["requested_skills"])

    canonical = ROOT / "src/SohailOS.Core/CanonicalPrompt.cs"
    text = canonical.read_text(encoding="utf-8")
    marker = 'public const string Sha256 = "'
    sha = text.split(marker, 1)[1].split('"', 1)[0]
    compact = text.split('public const string Compact = "', 1)[1].split('";', 1)[0]
    assert hashlib.sha256(compact.encode("utf-8")).hexdigest() == sha

    print("command/lifecycle/skills governance contracts: PASS")


if __name__ == "__main__":
    main()
