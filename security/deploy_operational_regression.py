#!/usr/bin/env python3
"""Static operational contract tests for the Cloudflare deploy pipeline."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
WF = ROOT / ".github" / "workflows" / "deploy-cloudflare.yml"

def main():
    s = WF.read_text(encoding="utf-8")
    required = [
        "concurrency:",
        "cancel-in-progress: false",
        "permissions:",
        "contents: read",
        "npm run typecheck",
        "wrangler whoami",
        "wrangler deploy --dry-run",
        "id: deploy",
        "id: public_smoke",
        "id: provider",
        "id: mcp_smoke",
        "cloudflare/wrangler-action@v3",
        "command: deploy",
        "wrangler rollback",
        "if: always()",
        "SOHAILOS_GATEWAY_URL",
    ]
    for x in required:
        assert x in s, f"missing deploy invariant: {x!r}"

    rollback_gate = (
        "steps.deploy.outcome == 'success' && "
        "(steps.public_smoke.outcome == 'failure' || "
        "steps.provider.outcome == 'failure' || "
        "steps.mcp_smoke.outcome == 'failure')"
    )
    assert rollback_gate in s, "rollback is not scoped to post-deploy verification failures"

    assert "npm install --no-audit --no-fund" in s
    assert "npx wrangler --version" in s
    assert "secret put" in s
    secret_names = [
        "SOHAILOS_GATEWAY_TOKEN",
        "SOHAILOS_OPENAI_API_KEY",
        "SOHAILOS_GEMINI_API_KEY",
        "SOHAILOS_ANTHROPIC_API_KEY",
        "SOHAILOS_SUPABASE_SERVICE_ROLE_KEY",
    ]
    for secret in secret_names:
        assert f'echo "$' + secret + '"' not in s, f"workflow echoes secret variable: {secret}"
    assert not re.search(r"git\s+push\s+--force|wrangler\s+deploy.*--force", s)

    print("PASS deploy operational contract")

if __name__ == "__main__":
    main()
