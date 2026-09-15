# SohailOS Cloudflare Gateway deployment

The Worker is configured as `sohailos-central` with `workers_dev: true`. Cloudflare documents that a Worker with workers.dev enabled is exposed as `<worker-name>.<account-subdomain>.workers.dev`.

## GitHub Actions deployment

The repository contains `.github/workflows/deploy-cloudflare.yml`. It validates the Worker with TypeScript and Wrangler dry-run, then deploys with Cloudflare's official Wrangler action.

GitHub Actions requires these repository secrets:

- `CLOUDFLARE_ACCOUNT_ID`
- `CLOUDFLARE_API_TOKEN`

The token should be a Cloudflare API token scoped to the account and Workers deployment permissions. Do not put the token in source code, `.env` files committed to Git, or chat messages.

After both secrets are present, push a change under `cloudflare/sohailos-gateway/` or run the deployment workflow manually from GitHub Actions.

## Worker runtime secrets

These are configured in the Cloudflare Worker itself, not committed to GitHub:

- `SOHAILOS_GATEWAY_TOKEN` — required for `/v1/agent/run` and `/mcp`.
- At least one AI provider secret:
  - `SOHAILOS_OPENAI_API_KEY`
  - `SOHAILOS_GEMINI_API_KEY`
  - `SOHAILOS_ANTHROPIC_API_KEY`
- Optional model overrides:
  - `SOHAILOS_OPENAI_MODEL`
  - `SOHAILOS_GEMINI_MODEL`
  - `SOHAILOS_ANTHROPIC_MODEL`
- Optional Supabase memory:
  - `SOHAILOS_SUPABASE_URL`
  - `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`
  - `SOHAILOS_SUPABASE_TABLE` (defaults to `sohailos_memory`)
- Optional CORS allow-list:
  - `SOHAILOS_CORS_ORIGINS`

`GET /health` and `GET /` are public diagnostic endpoints. They do not expose secret values.

## Verification

After deployment, check:

- `/health` — should return JSON with `status: "ok"`.
- `/` — should return service metadata and endpoint names.
- `/v1/agent/run` — requires `Authorization: Bearer <SOHAILOS_GATEWAY_TOKEN>`.
- `/mcp` — requires the same bearer token.

If the workers.dev URL displays Cloudflare's "There is nothing here yet" page, the Worker has not been published to that workers.dev endpoint yet, or the workers.dev route is disabled. Check the Worker's Domains/Routes settings and the latest deployment in Cloudflare.
