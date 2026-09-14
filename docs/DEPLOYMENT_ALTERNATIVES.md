# Deployment alternatives for SohailOS

Render is not the only deployment path. If Render billing verification is unavailable, keep GitHub as the source of truth and use a host with a usable free/trial path.

## Recommended order

1. Koyeb Free Instance — current target for the public gateway. It can deploy directly from GitHub using the repository Dockerfile. Treat the free instance as a testing/hobby target rather than production infrastructure.
2. Railway Free Trial — strong alternative for Docker/GitHub deployment, subject to its current account verification and free/trial limits.
3. Hugging Face Spaces — useful for experiments or a later MCP-facing adaptation, but it is not currently the preferred private gateway path for SohailOS.

## Secrets and API keys

Never commit real credentials. Put them into the deployment platform's secret/environment-variable interface.

SohailOS supports multiple keys for OpenAI, Gemini, and Anthropic. Use the plural variables below and separate keys with `;`, `,`, or new lines:

- `SOHAILOS_OPENAI_API_KEYS`
- `SOHAILOS_GEMINI_API_KEYS`
- `SOHAILOS_ANTHROPIC_API_KEYS`

The application rotates through the configured keys and tries the remaining keys if the current key fails. The singular variables remain supported for compatibility.

Use the real values only in the host secret store or local untracked environment file. Do not paste them into GitHub issues, chat, source files, Dockerfiles, or commit history.

## Koyeb: why the dashboard path may not match your account

Koyeb's current public documentation still describes an **Overview → Create Web Service** flow for GitHub deployments. citeturn1search2 If your account currently lands on a different dashboard and does not expose that control, do not block the project on the UI.

The Koyeb CLI is a supported deployment path and can create the App and Service directly from the GitHub repository. It also supports Secret interpolation such as `{{secret.NAME}}`. citeturn2search0turn2search1

The repository contains `scripts/deploy-koyeb.ps1` for this path. It expects the Koyeb Secrets to already exist and never embeds their values.

### CLI deployment

1. Install and authenticate the Koyeb CLI locally.
2. Create the required Koyeb Secrets using the CLI or any working Koyeb secret-management screen:
   - `SOHAILOS_OPENAI_API_KEYS`
   - `SOHAILOS_GEMINI_API_KEYS`
   - `SOHAILOS_ANTHROPIC_API_KEYS`
   - `SOHAILOS_GATEWAY_TOKEN`
   - `SOHAILOS_SUPABASE_URL`
   - `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`
3. From the repository root, run:

```powershell
.\scripts\deploy-koyeb.ps1
```

The script deploys `main` from `github.com/kaido6sanb6/SohailOS-Central`, uses the repository Dockerfile, exposes HTTP `10000`, and attaches the secrets through Koyeb interpolation.

Koyeb's CLI reference documents `koyeb secrets create NAME --value ...` and `koyeb apps init` with GitHub, Docker builder, ports, and `--env` secret interpolation. citeturn2search0

Koyeb also defines `PORT` for Web Services and permits explicitly setting it; SohailOS listens on the supplied value and defaults to `10000`. citeturn2search3

## Koyeb control-panel path when available

If the dashboard for your account later exposes the creation control, the expected flow is:

1. Start the Web Service creation flow.
2. Select **GitHub**.
3. Select `kaido6sanb6/SohailOS-Central` and branch `main`.
4. Select **Dockerfile** as the builder.
5. Expand **Environment variables and files** and add the required plaintext/Secret variables.
6. Expose HTTP port `10000` and use `/health` for the health check.
7. Deploy.

The public Koyeb documentation currently describes this GitHub/Docker deployment flow. citeturn1search0turn1search2

## Koyeb deployment target

The repository is prepared for this deployment path:

- Dockerfile: repository root
- Gateway: `src/SohailOS.Gateway`
- HTTP port: `10000` by default, or Koyeb's `PORT` value
- Health endpoint: `/health`
- Authenticated API: `POST /v1/agent/run`
- MCP endpoint: `POST /mcp`
- Gateway authentication: `SOHAILOS_GATEWAY_TOKEN`
- Optional web allowlist: `SOHAILOS_WEB_ALLOWLIST`
- MCP session lifetime: `SOHAILOS_MCP_SESSION_TTL_MINUTES` (default 60)
- Optional persistent memory: Supabase via `SOHAILOS_SUPABASE_URL` and `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`

## Supabase persistent memory

Apply `supabase/migrations/001_sohailos_memory.sql` to the Supabase project. Then provide these values as Koyeb Secrets:

- `SOHAILOS_SUPABASE_URL`
- `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`

The gateway uses Supabase persistence automatically when both are configured; otherwise it falls back to `InMemoryStore` for local development.

The service-role key is server-side only and must never be placed in a client application, ChatGPT configuration, GitHub file, or chat message.

## Security

Never commit `OPENAI_API_KEY`, `GEMINI_API_KEY`, `ANTHROPIC_API_KEY`, `SOHAILOS_*_API_KEYS`, `SOHAILOS_GATEWAY_TOKEN`, `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`, GitHub tokens, or any other secret.

The repository contains `.env.example` only as a placeholder configuration reference. Real `.env.*` files are ignored by Git.

## Current validation target

Before calling the backend production-ready, validate:

- GET `/health`
- authenticated POST `/v1/agent/run`
- authenticated MCP initialize/tools/list/tools/call flow
- correct MCP JSON Schema for tool inputs
- MCP session expiration
- multi-key rotation and provider fallback behavior
- no secrets in repository history
- CI build and test success
- persistent Supabase memory with a real project
- public HTTPS deployment
- final ChatGPT MCP/App connection
