# Deployment alternatives for SohailOS

Render is not the only deployment path. If Render billing verification is unavailable, keep the repository as the source of truth and use a host that does not require a payment method for the initial free/trial path.

## Recommended order

1. Railway Free Trial — easiest Docker/GitHub path for a temporary public gateway. Railway currently advertises a $0 Free plan and a new-user $5 trial credit; the trial can be started without a credit card. The free plan is usage-limited, so this is a validation path rather than a guaranteed always-on production host.
2. Koyeb Free Instance — a genuine free web-service instance with 512 MB RAM, 0.1 vCPU and 2 GB SSD. It scales to zero after one hour without traffic and is intended for testing/hobby use. Check account eligibility before relying on it.
3. Hugging Face Spaces — useful if we adapt the gateway to a Docker Space. Free personal accounts can use free compute in supported Space configurations, but current Docker/Gradio Space creation rules require care and private/protected hosting has plan restrictions. Do not put secrets in a public Space repository.

## Security

Never commit OPENAI_API_KEY, GEMINI_API_KEY, ANTHROPIC_API_KEY, SOHAILOS_GATEWAY_TOKEN, GitHub tokens, or any other secret. Inject them through the host's secret/environment-variable mechanism.

The Render Blueprint remains in `render.yaml` so the project can still be deployed there later. The application itself is host-agnostic and reads the provider credentials from environment variables.

## Current validation target

Before calling the backend production-ready, validate:

- GET /health
- authenticated POST /v1/agent/run
- authenticated MCP initialize/tools/list/tools/call flow
- provider fallback behavior
- no secrets in repository history
- CI build and test success
