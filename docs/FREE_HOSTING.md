# Free Hosting — SohailOS Gateway

SohailOS can run its remote Gateway on Render Free without a VPS. The repository contains a `render.yaml` Blueprint and a Dockerfile for the ASP.NET Gateway.

## Deployment

1. In Render, create a new Blueprint and select the private GitHub repository `kaido6sanb6/SohailOS-Central`.
2. Keep the service on the Free plan and the Frankfurt region unless another region is preferred.
3. Let Render generate `SOHAILOS_GATEWAY_TOKEN`.
4. Enter these secrets in Render Environment Variables. Never commit them to GitHub:
   - `OPENAI_API_KEY`
   - `GEMINI_API_KEY`
   - `ANTHROPIC_API_KEY`
5. Keep `SOHAILOS_AI_PROVIDER=auto` so SohailOS can route requests among configured providers.
6. After deployment, verify `/health` on the generated HTTPS service URL.

## Runtime behavior

- Render supplies the `PORT` environment variable; the gateway binds to `0.0.0.0` and that port.
- The service uses HTTPS at the public Render URL.
- The free web service can sleep after inactivity and may take time to wake on the next request.
- The free filesystem is ephemeral. Do not treat the Render container as durable memory storage.
- `SOHAILOS_GATEWAY_TOKEN` protects `/v1/agent/run` and `/mcp`.

## Secrets

The following values must stay outside source control:

`OPENAI_API_KEY`
`GEMINI_API_KEY`
`ANTHROPIC_API_KEY`
`SOHAILOS_GATEWAY_TOKEN`

If a key is accidentally pasted into a chat, commit, log, or screenshot, revoke/rotate it at its provider and replace it in Render.

## Current limitation

The Gateway has an initial MCP implementation with sessions and tool discovery/calls. Full production Streamable HTTP/SSE behavior, OAuth, persistent remote memory, and complete cloud adapters remain separate hardening work. Do not describe the service as production-grade until those items and end-to-end tests are complete.
