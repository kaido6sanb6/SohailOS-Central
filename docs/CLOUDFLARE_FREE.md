# Cloudflare Free Gateway

SohailOS includes an optional Cloudflare Workers edge gateway under `cloudflare/sohailos-gateway`.

## Role in SohailOS

Cloudflare is the free public edge/deployment layer. The existing C# gateway remains the canonical full runtime. The Worker is intentionally lightweight and currently exposes `/health`, authenticated `/v1/agent/run`, and a stateless `/mcp` facade.

## What is free

Cloudflare Workers has a Free plan. The Worker delegates model inference to external AI providers rather than performing heavy inference itself. Workers AI is not required by this V1 implementation.

## Secrets: where each key goes

There are two different secret locations. Do not mix them.

### A. Cloudflare Worker secrets — AI, gateway, and Supabase

These belong in the Cloudflare Worker runtime, not in GitHub and not in the source code:

- `SOHAILOS_GATEWAY_TOKEN` — required; a private random token used to authenticate `/v1/agent/run` and `/mcp`.
- `SOHAILOS_OPENAI_API_KEY` — optional if OpenAI is used.
- `SOHAILOS_GEMINI_API_KEY` — optional if Gemini is used.
- `SOHAILOS_ANTHROPIC_API_KEY` — optional if Anthropic is used.
- `SOHAILOS_SUPABASE_URL` — optional, but required together with the service-role key for remote memory.
- `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY` — optional; server-side only and must never be exposed to a browser/client.

In the Cloudflare dashboard, open the `sohailos-gateway` Worker, then its Settings/Variables area and add these under Secrets (encrypted runtime secrets). Do not put them under GitHub repository files or `wrangler.jsonc`.

If using Wrangler locally, run `npx wrangler secret put NAME` from `cloudflare/sohailos-gateway` and enter the value only when Wrangler prompts you. Never paste the value into this repository.

### B. GitHub Actions secrets — only Cloudflare deployment credentials

If deployment is performed by `.github/workflows/deploy-cloudflare.yml`, the GitHub repository needs only:

- `CLOUDFLARE_ACCOUNT_ID`
- `CLOUDFLARE_API_TOKEN`

Add them in GitHub: repository → Settings → Secrets and variables → Actions → New repository secret.

These are deployment credentials for GitHub Actions. They are different from the AI provider keys. The workflow does not need the OpenAI, Gemini, Anthropic, or Supabase secrets in GitHub.

Use a narrowly scoped Cloudflare API token with the permissions required to deploy the Worker. Never commit the token.

## Recommended V1 setup

For the first deployment, configure only one AI provider to reduce cost and complexity. OpenAI can be the default provider; the Worker can later be configured for Gemini or Anthropic, or use the built-in provider selection/fallback logic when multiple provider secrets are present.

Also create `SOHAILOS_GATEWAY_TOKEN` as a long random value. This is not an AI API key; it is the password protecting the SohailOS gateway itself.

If remote memory is wanted, add the Supabase URL and service-role key. The service-role key must remain a Cloudflare Worker secret and must never be sent to the frontend.

## Deployment path 1 — Cloudflare dashboard / Workers Builds

1. Create or log in to a Cloudflare account.
2. Open Workers & Pages and create the Worker from the GitHub repository/Workers Builds flow.
3. Select the `cloudflare/sohailos-gateway` project directory and the `main` branch for production deployment.
4. Deploy the Worker.
5. In the Worker settings, add the Worker secrets listed above.
6. Redeploy if the dashboard requires it after secret changes.
7. Open the generated `workers.dev` URL and verify `GET /health` returns a healthy response.

This path does not require putting AI keys into GitHub.

## Deployment path 2 — Wrangler from the local machine

From `cloudflare/sohailos-gateway`:

```bash
npm install
npx wrangler login
npx wrangler deploy
```

Then configure secrets without writing them to files:

```bash
npx wrangler secret put SOHAILOS_GATEWAY_TOKEN
npx wrangler secret put SOHAILOS_OPENAI_API_KEY
npx wrangler secret put SOHAILOS_GEMINI_API_KEY
npx wrangler secret put SOHAILOS_ANTHROPIC_API_KEY
npx wrangler secret put SOHAILOS_SUPABASE_URL
npx wrangler secret put SOHAILOS_SUPABASE_SERVICE_ROLE_KEY
```

Only run the commands for the secrets you actually use. `SOHAILOS_GATEWAY_TOKEN` and at least one AI provider key are required for a useful protected deployment.

## Deployment path 3 — GitHub Actions

The repository contains `.github/workflows/deploy-cloudflare.yml`.

1. Add `CLOUDFLARE_ACCOUNT_ID` and `CLOUDFLARE_API_TOKEN` under GitHub repository Actions secrets.
2. Merge the Worker changes into `main`.
3. GitHub Actions performs the dry-run and then deploys the Worker when both deployment secrets are present.
4. Add the AI, gateway, and Supabase secrets in Cloudflare Worker settings as described above.

Do not add AI provider keys to GitHub Actions merely because the deployment workflow exists.

## Non-secret configuration

Optional configuration values include:

- `SOHAILOS_AI_PROVIDER` — `auto`, `openai`, `gemini`, or `anthropic`.
- `SOHAILOS_OPENAI_MODEL`
- `SOHAILOS_GEMINI_MODEL`
- `SOHAILOS_ANTHROPIC_MODEL`
- `SOHAILOS_SUPABASE_TABLE` — defaults to `sohailos_memory`.
- `SOHAILOS_CORS_ORIGINS` — comma-separated allowed origins; keep `*` only for an intentionally broad client setup.

These can be ordinary Worker variables. API keys, service-role credentials, and the gateway token must remain secrets.

## Security rules

Never put any API key, service-role key, gateway token, or Cloudflare API token in:

- Git commits
- `wrangler.jsonc`
- `.env` files that are committed or uploaded
- browser/frontend code
- screenshots or public documentation
- chat messages

If a secret is accidentally exposed, revoke it and create a replacement rather than trying to hide it in a later commit.

## Current V1 scope

The Worker provides provider routing/fallback, bounded request input, optional Supabase memory persistence, `/health`, `/v1/agent/run`, and a stateless MCP transport exposing the `sohailos_agent_run` tool. It is not yet full parity with the C# `AgentRuntime`.

OAuth-based production MCP registration, richer tool schemas, persistent MCP sessions, web/tool execution, and complete parity with the C# runtime are follow-up stages. Do not expose the Worker publicly without setting `SOHAILOS_GATEWAY_TOKEN`.
