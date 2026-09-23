# SohailOS-Central

Central repository for SohailOS — a personal AI agent and operating system for research, thinking, software/product development, office work, automation, strategy, and learning.

## Current implementation

The repository now contains the first executable .NET foundation:

- `SohailOS.sln` — Visual Studio solution
- `src/SohailOS.Core/` — domain models and stable interfaces
- `src/SohailOS.Agents/` — module agents and Master Orchestrator
- `src/SohailOS.AI/` — provider abstraction and local stub provider
- `src/SohailOS.Memory/` — memory abstraction and in-memory implementation
- `src/SohailOS.Data/` — persistence boundary
- `src/SohailOS.Integrations/` — external service boundary
- `src/SohailOS.App/` — initial Windows WPF desktop client
- `tests/SohailOS.Tests/` — routing tests
- `.github/workflows/build.yml` — CI build/test pipeline
- `system/` — identity, routing, modules, and operating rules
- `memory/` — portable, non-sensitive user context
- `prompts/` — reusable prompt library
- `workflows/` — repeatable operating workflows
- `docs/` — architecture and implementation documentation

## Architecture

SohailOS-Central is a federated, model-agnostic AI operating-system control plane.

```text
User / Events / Schedules
          |
          v
Experience Layer (Desktop / Web / Mobile / CLI / API / MCP)
          |
          v
Authenticated Gateway
          |
          v
Policy + Consent + Trust + Security
          |
          v
Master Orchestrator
          |
          +--> Task Graph / Workflow Engine
          +--> Capability Registry
          +--> Specialist Agents
          +--> Tool + Integration Broker
          +--> AI Provider Fabric
          +--> Knowledge Fabric
          +--> Memory Fabric
          +--> Verification + Observability
          |
          v
Verified Response / Verified Side Effect
```

The full architecture is documented in `docs/ARCHITECTURE_V2.md`. The canonical model is:

Client → Gateway → Policy → Orchestrator → Task Graph → Capabilities → Knowledge/Memory/Providers/Tools → Verification → Delivery.

## Design principle

ChatGPT, Claude, and Gemini should be able to use the same central specification and portable context while remaining separate model providers. The desktop application is the runtime/orchestration layer; GitHub is the source-of-truth for code and system specification.

Sensitive personal information, credentials, API keys, tokens, and secrets must never be committed to this repository.



## Ecosystem control plane

The `ecosystem/ecosystem.json` manifest is the control-plane registry for the owner-scoped GitHub ecosystem. A live reconciler compares it with GitHub and safely records newly discovered repositories or metadata drift. `SohailOS-Central` remains the control plane; repositories are connected by metadata and capability composition rather than merged into this repository.

See `docs/ECOSYSTEM_KNOWLEDGE_LAYER.md` for the retrieval/knowledge model and `docs/IMPLEMENTATION_STATUS.md` for the current production-readiness gap list.
