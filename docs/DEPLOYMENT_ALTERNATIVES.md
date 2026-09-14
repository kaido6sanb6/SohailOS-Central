# Deployment alternatives for SohailOS

Render is not the only deployment path. If Render billing verification is unavailable, keep GitHub as the source of truth and use a host with a usable free/trial path.

## Recommended order

1. Koyeb Free Instance — current target for the public gateway. Koyeb provides one free web service per organization with 512 MB RAM, 0.1 vCPU and 2 GB SSD. It can deploy directly from GitHub using the repository Dockerfile and scales to zero after one hour without traffic. It is intended for testing/hobby use, not production. citeturn1search0turn1search1
2. Railway Free Trial — strong alternative for Docker/GitHub deployment. Railway currently provides a one-time $5 trial credit for up to 30 days and then a limited Free plan. Account verification can affect outbound network access. citeturn0search0
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

Koyeb supports encrypted Secrets and can expose them to a service as environment variables. citeturn0search4turn0search1 Railway likewise provides service variables for runtime configuration and secrets. citeturn0search2

## Koyeb deployment target

Connect `kaido6sanb6/SohailOS-Central` to Koyeb using GitHub deployment, select Docker as the builder, use the repository-root `Dockerfile`, expose the gateway HTTP port, and configure the health check at `/health`. Koyeb supports GitHub-driven deployment and Dockerfiles. citeturn0search5

Required runtime variables:

- `SOHAILOS_AI_PROVIDER=auto`
- `SOHAILOS_GATEWAY_TOKEN=<generated secret>`
- `SOHAILOS_OPENAI_API_KEYS=<one or more keys>`
- `SOHAILOS_GEMINI_API_KEYS=<one or more keys>`
- `SOHAILOS_ANTHROPIC_API_KEYS=<one or more keys>`
- `SOHAILOS_OPENAI_MODEL=gpt-5`
- `SOHAILOS_GEMINI_MODEL=gemini-3.8-flash`
- `SOHAILOS_ANTHROPIC_MODEL=claude-sonnet-5`

The gateway already reads the platform-provided `PORT` and binds to `0.0.0.0`.

## Security

Never commit `OPENAI_API_KEY`, `GEMINI_API_KEY`, `ANTHROPIC_API_KEY`, `SOHAILOS_*_API_KEYS`, `SOHAILOS_GATEWAY_TOKEN`, GitHub tokens, or any other secret.

The repository contains `.env.example` only as a placeholder configuration reference. Real `.env.*` files are ignored by Git.

## Current validation target

Before calling the backend production-ready, validate:

- GET `/health`
- authenticated POST `/v1/agent/run`
- authenticated MCP initialize/tools/list/tools/call flow
- multi-key rotation and provider fallback behavior
- no secrets in repository history
- CI build and test success
- persistent remote memory rather than ephemeral local storage
