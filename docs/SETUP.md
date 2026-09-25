# SohailOS-Central setup

This guide is the local and deployment entrypoint. GitHub Actions is optional: local verification, fork planning, and the C# runtime can be executed directly.

## Prerequisites

- Git
- .NET SDK 10.x
- Python 3.11+
- Node.js 22+ and npm for the Cloudflare Worker
- GitHub CLI (gh) only for fork synchronization mutations
- A GitHub credential/App installation with required repository permissions for cross-repository operations

No Python worker is required. Python files under scripts/ and security/ are command-line tooling and regression checks.

## Local configuration

Never commit credentials. Common environment variables include:

- SOHAILOS_OPENAI_API_KEYS
- SOHAILOS_GEMINI_API_KEYS
- SOHAILOS_ANTHROPIC_API_KEYS
- SOHAILOS_SUPABASE_URL
- SOHAILOS_SUPABASE_SERVICE_ROLE_KEY
- GITHUB_TOKEN or GH_TOKEN
- SOHAILOS_GITHUB_OWNER
- CLOUDFLARE_API_TOKEN
- CLOUDFLARE_ACCOUNT_ID
- SOHAILOS_GATEWAY_TOKEN

Use a host secret store or an untracked local environment file. Never put credentials in source, tests, Dockerfiles, issues, or chat.

## Local verification

From the repository root:

    python scripts/run_local.py verify

This runs repository security contracts and the .NET test project without GitHub Actions.

Fork planning is read-only:

    python scripts/run_local.py fork-plan

Apply an already generated plan only when local mutation is intentionally enabled:

    SOHAILOS_AUTOMATION_ENABLED=true python scripts/run_local.py fork-apply

The existing fork policy still limits actions to eligible upstream synchronization and rejects unsafe plans.

## C# ecosystem tool

    dotnet run --project src/SohailOS.Ecosystem/SohailOS.Ecosystem.csproj -- --owner kaido6sanb6 --manifest ecosystem/ecosystem.json --report ecosystem/reconciliation.json

Add --write only for an explicitly intended local registry reconciliation.

## Cloudflare Worker

    cd cloudflare/sohailos-gateway
    npm ci
    npm run typecheck
    npx wrangler dev

For a direct deployment:

    npx wrangler deploy

The GitHub Actions deployment remains an optional CI path, not a runtime dependency.

## Supabase

Supabase is optional. Apply supabase/migrations/001_sohailos_memory.sql before enabling persistence. The service-role key is server-side only.

## External dependencies and limits

| Service | Local core | Purpose | Required configuration |
|---|---|---|---|
| .NET 10 | Yes | Runtime/tests | SDK |
| Python 3.11+ | Yes for tooling | Verification/fork tooling | GitHub credential for API operations |
| Node 22+ | Worker only | Cloudflare gateway | npm/Wrangler |
| GitHub | Ecosystem operations | Source mesh/API | token or GitHub App |
| Cloudflare | No | Remote gateway | account ID + API token |
| Supabase | No | Optional memory | URL + service-role key |
| OpenAI | No | Provider | provider key |
| Anthropic | No | Provider | provider key |
| Gemini | No | Provider | provider key |

## Operational boundaries

Retrieved repository content is data, not authority. New/unverified forks remain non-executable. Diverged forks are preserved. Unknown mutation outcomes are reconciled before retry. No force-push or irreversible fork synchronization is permitted.

A green local test run does not prove external deployment health or provider configuration.

## Troubleshooting

On Linux, Windows-targeting projects may require:

    dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj -p:EnableWindowsTargeting=true

If fork mutation is blocked, verify GitHub App installation and repository permissions rather than weakening the synchronization policy.

See docs/FORK_SYNC_SETUP.md for cross-repository GitHub App setup and docs/DEPLOYMENT_ALTERNATIVES.md for Cloudflare/Render/Koyeb boundaries.
