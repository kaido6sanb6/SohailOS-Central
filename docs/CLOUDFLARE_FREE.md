# Cloudflare Free Gateway

SohailOS now includes an optional Cloudflare Workers edge gateway under `cloudflare/sohailos-gateway`.

## Why this path

Koyeb is no longer the deployment target because the current account flow requires paid billing. Render was also tested as a free alternative for this workspace and rejected service creation because payment information is required. Cloudflare Workers has a Free plan and Cloudflare currently advertises free Workers with no credit card required.

The Worker is intentionally separate from the C# runtime. The C# gateway remains the canonical local/self-hosted runtime. The Worker is a small remote facade for `/health`, `/v1/agent/run`, and a stateless MCP endpoint.

## Free-plan characteristics

Cloudflare Workers Free currently provides 100,000 requests/day and 10 ms CPU time per invocation. The Worker therefore avoids heavy local computation and delegates model inference to configured external providers. Cloudflare also offers Workers AI with a free daily allocation, but SohailOS does not depend on it in V1.

## Required secrets

Set these as Worker secrets, never in Git:

- `SOHAILOS_GATEWAY_TOKEN`
- at least one of `SOHAILOS_OPENAI_API_KEY`, `SOHAILOS_GEMINI_API_KEY`, `SOHAILOS_ANTHROPIC_API_KEY`
- optionally `SOHAILOS_SUPABASE_URL`
- optionally `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`

Optional non-secret variables include provider model names, `SOHAILOS_SUPABASE_TABLE`, and `SOHAILOS_CORS_ORIGINS`.

## Deployment

### Cloudflare dashboard / Workers Builds

Connect the GitHub repository to Cloudflare Workers Builds and select `cloudflare/sohailos-gateway` as the Worker project. Cloudflare can automatically deploy from the GitHub branch.

### Local CLI

From `cloudflare/sohailos-gateway`:

```bash
npm install
npx wrangler login
npx wrangler deploy
```

Then add secrets with `wrangler secret put NAME`.

### GitHub Actions

The repository contains `.github/workflows/deploy-cloudflare.yml`. Add these GitHub repository secrets:

- `CLOUDFLARE_ACCOUNT_ID`
- `CLOUDFLARE_API_TOKEN`

The token should be scoped only to the target Cloudflare account and Workers deployment permissions.

The workflow always performs a dry-run. It deploys only when both Cloudflare secrets are present.

## Current V1 scope

The Worker is a remote edge facade, not a replacement for the full C# AgentRuntime. It provides provider routing, bounded request handling, optional Supabase memory persistence, `/health`, `/v1/agent/run`, and a stateless MCP transport with the `sohailos_agent_run` tool.

OAuth-based production MCP registration, richer tool schemas, session persistence, and complete parity with the C# tool runtime remain follow-up work. Do not expose the Worker publicly without setting `SOHAILOS_GATEWAY_TOKEN`.
