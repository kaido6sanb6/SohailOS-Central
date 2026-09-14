# Deployment alternatives for SohailOS

The preferred public deployment path for the current account constraints is Cloudflare Workers Free. Koyeb and Render are not the primary targets because the user's available account flows require paid billing verification.

## Recommended order

1. **Cloudflare Workers Free** — primary remote gateway. It is serverless, globally distributed, supports GitHub-based deployment, and currently provides 100,000 requests/day on the Free plan. Cloudflare advertises Workers as free to start without a credit card. See `docs/CLOUDFLARE_FREE.md` and `cloudflare/sohailos-gateway/`. citeturn4search14turn4search13
2. **Local/self-hosted C# gateway** — canonical full-featured runtime for development and machines where the user controls the host.
3. **Render Free** — technically compatible with the Dockerized .NET gateway and has a Free web-service tier, but the connected Render workspace currently rejects service creation until payment information is added, so it is not usable as the user's no-card path. citeturn2search1turn2search2
4. **Koyeb** — repository tooling remains for reference, but the current account UI/billing path makes it unsuitable as the primary free target.

## Cloudflare Free gateway

The repository contains a small TypeScript Worker under `cloudflare/sohailos-gateway`.

It provides:

- `GET /health`
- authenticated `POST /v1/agent/run`
- authenticated stateless `POST /mcp`
- OpenAI, Gemini, and Anthropic provider selection/fallback
- optional Supabase memory persistence
- CORS configuration
- a GitHub Actions deployment workflow

Cloudflare Workers Free currently allows 100,000 requests/day, 10 ms CPU time per invocation, and 50 subrequests per invocation. The Worker therefore keeps orchestration lightweight and delegates model inference to configured providers. citeturn4search14

### Cloudflare deployment

From `cloudflare/sohailos-gateway`:

```bash
npm install
npx wrangler login
npx wrangler deploy
```

Cloudflare also supports connecting a GitHub repository through Workers Builds for automatic deployment. citeturn4search6turn4search8

For GitHub Actions deployment, the repository includes `.github/workflows/deploy-cloudflare.yml`. Add these GitHub repository secrets:

- `CLOUDFLARE_ACCOUNT_ID`
- `CLOUDFLARE_API_TOKEN`

Cloudflare documents these two secrets as the required CI credentials for Wrangler and recommends narrowly scoped Workers permissions. citeturn4search0

### Cloudflare Worker secrets

Set these with `wrangler secret put` or in the Cloudflare Worker secret interface:

- `SOHAILOS_GATEWAY_TOKEN`
- at least one of `SOHAILOS_OPENAI_API_KEY`, `SOHAILOS_GEMINI_API_KEY`, `SOHAILOS_ANTHROPIC_API_KEY`
- optionally `SOHAILOS_SUPABASE_URL`
- optionally `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`

Never commit real values. Cloudflare exposes Worker secrets to the runtime without putting them in source control.

## Render findings

Render documents a Free web-service tier and supports Docker, including .NET applications via Docker. citeturn1search0turn1search4turn1search7 However, an actual creation attempt through the connected Render workspace returned a payment-required response. Therefore Render is not a no-card solution for this account at present.

Do not move the project architecture to Render unless the account later allows Free service creation without payment verification.

## Koyeb findings

Koyeb's public documentation still describes a dashboard creation flow and a CLI path, but that does not solve the account-level billing/UI problem. The repository retains `scripts/deploy-koyeb.ps1` as a fallback for an account that actually exposes an eligible free deployment.

## Secrets and API keys

SohailOS supports multiple keys for OpenAI, Gemini, and Anthropic. Use the plural variables below and separate keys with `;`, `,`, or new lines:

- `SOHAILOS_OPENAI_API_KEYS`
- `SOHAILOS_GEMINI_API_KEYS`
- `SOHAILOS_ANTHROPIC_API_KEYS`

The C# runtime rotates through configured keys and falls back to remaining providers. The singular variables remain supported for compatibility.

Use real values only in a host secret store or local untracked environment file. Never paste them into GitHub issues, chat, source files, Dockerfiles, or commit history.

## Persistent memory

The C# runtime and Cloudflare Worker can use the same Supabase memory table:

- `SOHAILOS_SUPABASE_URL`
- `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`
- optional `SOHAILOS_SUPABASE_TABLE` (defaults to `sohailos_memory`)

Apply `supabase/migrations/001_sohailos_memory.sql` to the Supabase project first. The service-role key is server-side only.

## Production-readiness boundary

The Cloudflare gateway is intentionally V1. It is a remote edge facade, not complete feature parity with the C# AgentRuntime. OAuth-based production MCP registration, richer tool schemas, durable MCP sessions, and full tool-executor parity remain follow-up work.

Before calling the remote gateway production-ready, validate:

- GET `/health`
- authenticated POST `/v1/agent/run`
- MCP initialize/tools/list/tools/call
- provider fallback and error handling
- Supabase persistence
- CORS policy
- no secrets in repository history
- CI success
- public HTTPS endpoint
- final ChatGPT MCP/App connection
