# SohailOS Final V1 Architecture Design

## Goal

Close the remaining V1 engineering gaps without adding duplicate, decorative, or unrelated integrations. SohailOS remains a single personal AI operating system whose source of truth is GitHub, whose orchestration/runtime can be consumed by desktop and remote clients, and whose model providers remain interchangeable.

## Integration rule

Named tools and plugins are capabilities, not mandatory dependencies. Duplicate names are ignored. An integration is kept only when it materially improves one of four areas: architecture, implementation, verification, design, or deployment. Tools that cannot access this private repository or cannot safely modify the runtime are used only for analysis when appropriate; they are not added as fake project dependencies.

## Target architecture

User/client -> authenticated gateway -> Master Orchestrator -> module routing -> bounded tool loop -> provider abstraction -> OpenAI/Anthropic/Gemini; memory is loaded before execution and persisted after execution; external integrations sit behind explicit contracts and permission policy.

The existing 12 modules remain canonical: THINK, SOCIOLOGY, CINEMA, RESEARCH, STATS, AI, CODE, PRODUCT, OFFICE, OPERATIONS, STRATEGY, LEARNING.

## Runtime contract

The remote gateway exposes:

- `GET /health`
- `GET /`
- authenticated `POST /v1/agent/run`
- authenticated `POST /mcp`

MCP must support initialize, initialized notification, tools/list, and tools/call for the SohailOS agent tool. The deployment pipeline must verify both public endpoints and an authenticated MCP call.

## Provider contract

All providers implement the same internal request/response contract. Provider selection is configurable, task routing can select a provider, timeouts are bounded, and provider failures are sanitized. No credential is stored in source control or transmitted through ChatGPT conversation text.

## Memory contract

Memory is abstracted behind a store interface. Local JSON remains a development fallback; Supabase/PostgREST is the remote persistence boundary. Memory reads and writes are bounded and best-effort so persistence failure cannot expose credentials or crash the request path unexpectedly.

## Security contract

Use bearer authentication for protected gateway routes, strict origin handling, security headers, bounded request sizes, bounded provider execution, sanitized upstream errors, and no secrets in logs. Runtime secrets remain account-owned configuration.

## Deployment contract

GitHub Actions performs dependency installation, type checking, Wrangler dry-run, Worker deployment, public smoke tests, and authenticated MCP smoke tests. Deployment configuration must reference the actual Worker name and URL. Runtime-only configuration is deliberately excluded from repository files.

## Completion criteria

V1 is complete when:

1. Repository architecture and documentation are internally consistent.
2. CI validates the Cloudflare Worker and deployment workflow.
3. The live Worker responds successfully to `/health` and `/`.
4. The live Worker reports at least one configured AI provider.
5. An authenticated `/v1/agent/run` request succeeds with a non-sensitive test prompt.
6. Authenticated MCP initialize, tools/list, and tools/call succeed.
7. One remote client can invoke the MCP gateway end-to-end, subject to client-side availability.

## Explicit non-goals for V1

Full multi-user OAuth, generalized SaaS tenancy, arbitrary third-party plugin execution, provider-specific lock-in, and speculative integrations are deferred. They must not be introduced merely to satisfy a plugin checklist.

## Tool-assisted quality strategy

GitHub is the authoritative implementation surface. Architecture/review tools may critique the design; research tools may benchmark relevant patterns; design tools may validate any future client UI; learning tools may package operator documentation. Legal, medical, commerce, WordPress, and unrelated developer catalogs are not project dependencies unless a concrete project requirement emerges.
