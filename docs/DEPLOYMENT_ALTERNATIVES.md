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

The application rotates through the configured keys and tries the remaining keys if the current key fails. The singular variables remain supported for compatibility:

- `OPENAI_API_KEY` / `SOHAILOS_OPENAI_API_KEY`
- `GEMINI_API_KEY` / `SOHAILOS_GEMINI_API_KEY`
- `ANTHROPIC_API_KEY` / `SOHAILOS_ANTHROPIC_API_KEY`

For three Gemini keys, a single environment variable can therefore contain three values, for example:

`SOHAILOS_GEMINI_API_KEYS=KEY_1;KEY_2;KEY_3`

Use the real values only in the host secret store or local untracked environment file. Do not paste them into GitHub issues, chat, source files, Dockerfiles, or commit history.

## Koyeb: current UI path for secrets

The direct `/secrets` page may redirect to the current Koyeb landing/control-panel screen. If that happens, do not look for a separate Secrets page first.

Use the Service creation flow instead:

1. Open Koyeb and start **Create Web Service**.
2. Select **GitHub** as the deployment method.
3. Select `kaido6sanb6/SohailOS-Central` and branch `main`.
4. In **Builder**, select **Dockerfile** and use the repository-root `Dockerfile`.
5. Expand **Environment variables and files** and click **Add variable**.
6. For each sensitive variable, choose **Secret** as the variable type. Koyeb's current deployment flow can create the Secret directly from this screen; you do not need to navigate to a separate Secrets page first.
7. Create these four secrets:
   - `SOHAILOS_OPENAI_API_KEYS`
   - `SOHAILOS_GEMINI_API_KEYS`
   - `SOHAILOS_ANTHROPIC_API_KEYS`
   - `SOHAILOS_GATEWAY_TOKEN`
8. For the model/provider settings, use plaintext variables:
   - `SOHAILOS_AI_PROVIDER=auto`
   - `SOHAILOS_OPENAI_MODEL=gpt-5`
   - `SOHAILOS_GEMINI_MODEL=gemini-3.8-flash`
   - `SOHAILOS_ANTHROPIC_MODEL=claude-sonnet-5`
9. Expose HTTP port `10000`. The gateway also reads Koyeb's `PORT` variable and binds to `0.0.0.0`.
10. Configure the health check as `/health` and deploy.

If a Secret has already been created globally, Koyeb also supports referencing it from a Service environment variable with the form `{{ secret.SECRET_NAME }}`.

Koyeb documents Secrets as encrypted server-side values that can be reused by Services, and its environment-variable system supports Secret interpolation. See the current Koyeb documentation for Secrets and environment variables.

## Koyeb deployment target

The repository is already prepared for this deployment path:

- Dockerfile: repository root
- Gateway: `src/SohailOS.Gateway`
- HTTP port: `10000` by default, or Koyeb's `PORT` value
- Health endpoint: `/health`
- Authenticated API: `POST /v1/agent/run`
- MCP endpoint: `POST /mcp`
- Gateway authentication: `SOHAILOS_GATEWAY_TOKEN`
- Optional web allowlist: `SOHAILOS_WEB_ALLOWLIST`
- MCP session lifetime: `SOHAILOS_MCP_SESSION_TTL_MINUTES` (default 60)

## Security

Never commit `OPENAI_API_KEY`, `GEMINI_API_KEY`, `ANTHROPIC_API_KEY`, `SOHAILOS_*_API_KEYS`, `SOHAILOS_GATEWAY_TOKEN`, GitHub tokens, or any other secret.

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
- persistent remote memory rather than ephemeral local storage
