# Deployment alternatives for SohailOS

Render is not the only deployment path. If Render billing verification is unavailable, keep GitHub as the source of truth and use a host with a usable free/trial path.

## Recommended order

1. Koyeb Free Instance — current target for the public gateway. Koyeb provides one free web service per organization with 512 MB RAM, 0.1 vCPU and 2 GB SSD. It can deploy directly from GitHub using the repository Dockerfile and scales to zero after one hour without traffic. It is intended for testing/hobby use, not production.
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

## Exact Koyeb location for the keys

There are two Koyeb screens involved:

1. **Create the encrypted secrets:** open the Koyeb control panel's **Secrets** page: https://app.koyeb.com/secrets
2. Create these four Secrets there:
   - `SOHAILOS_OPENAI_API_KEYS`
   - `SOHAILOS_GEMINI_API_KEYS`
   - `SOHAILOS_ANTHROPIC_API_KEYS`
   - `SOHAILOS_GATEWAY_TOKEN`
3. Then, in the SohailOS Service configuration, open **Settings → Environment variables and files**.
4. Add variables with the same names and set each value to the corresponding Secret reference, for example:
   - `SOHAILOS_OPENAI_API_KEYS={{ secret.SOHAILOS_OPENAI_API_KEYS }}`
   - `SOHAILOS_GEMINI_API_KEYS={{ secret.SOHAILOS_GEMINI_API_KEYS }}`
   - `SOHAILOS_ANTHROPIC_API_KEYS={{ secret.SOHAILOS_ANTHROPIC_API_KEYS }}`
   - `SOHAILOS_GATEWAY_TOKEN={{ secret.SOHAILOS_GATEWAY_TOKEN }}`

Koyeb encrypts Secret values server-side and allows them to be referenced by Service environment variables. The environment-variable screen is separate from the global Secrets page.

## Koyeb deployment target

Connect `kaido6sanb6/SohailOS-Central` to Koyeb using GitHub deployment, select Docker as the builder, use the repository-root `Dockerfile`, expose HTTP port `10000`, and configure the health check at `/health`. The gateway also reads the platform-provided `PORT` value and binds to `0.0.0.0`.

Required runtime variables:

- `SOHAILOS_AI_PROVIDER=auto`
- `SOHAILOS_GATEWAY_TOKEN={{ secret.SOHAILOS_GATEWAY_TOKEN }}`
- `SOHAILOS_OPENAI_API_KEYS={{ secret.SOHAILOS_OPENAI_API_KEYS }}`
- `SOHAILOS_GEMINI_API_KEYS={{ secret.SOHAILOS_GEMINI_API_KEYS }}`
- `SOHAILOS_ANTHROPIC_API_KEYS={{ secret.SOHAILOS_ANTHROPIC_API_KEYS }}`
- `SOHAILOS_OPENAI_MODEL=gpt-5`
- `SOHAILOS_GEMINI_MODEL=gemini-3.8-flash`
- `SOHAILOS_ANTHROPIC_MODEL=claude-sonnet-5`

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
