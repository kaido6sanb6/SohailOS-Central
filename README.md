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

The architecture now uses a layered control-plane model that keeps forks and upstream repositories independent while making them continuously discoverable and retrievable.

```text
Clients
  |
  v
Orchestration + Policy
  |
  +--> Knowledge Fabric
  |      lexical / vector / graph / hybrid retrieval
  |
  +--> Execution Fabric
  |      GitHub / MCP / APIs / cloud / local runtime
  |
  v
SohailOS-Central Control Plane
  |
  +--> registry / trust / provenance / prompts / workflows
  |
  v
GitHub Source Mesh
  |
  +--> owned repositories
  +--> personal forks
  +--> upstream relationships
```

See `docs/ARCHITECTURE.md` for the complete model.

### Continuous fork synchronization

All owner forks are discovered from GitHub automatically. `.github/workflows/fork-sync.yml` runs every 15 minutes, resolves upstream/source metadata, and attempts a non-destructive default-branch upstream synchronization. Conflicts are recorded instead of force-pushed.

After synchronization, the control plane reconciles repository metadata and refreshes the knowledge layer. Cross-repository fork writes use a GitHub App installation token generated at runtime from the `SOHAILOS_GITHUB_APP_ID` Actions variable and `SOHAILOS_GITHUB_APP_PRIVATE_KEY` Actions secret.

GitHub scheduled workflows use UTC by default and can run at intervals as short as five minutes; this project deliberately uses a 15-minute schedule.

## Design principle

ChatGPT, Claude, and Gemini should be able to use the same central specification and portable context while remaining separate model providers. The desktop application is the runtime/orchestration layer; GitHub is the source-of-truth for code and system specification.

Sensitive personal information, credentials, API keys, tokens, and secrets must never be committed to this repository.



## Ecosystem control plane

The `ecosystem/ecosystem.json` manifest is the control-plane registry for the owner-scoped GitHub ecosystem. A live reconciler compares it with GitHub and safely records newly discovered repositories or metadata drift. `SohailOS-Central` remains the control plane; repositories are connected by metadata and capability composition rather than merged into this repository.

See `docs/ECOSYSTEM_KNOWLEDGE_LAYER.md` for the retrieval/knowledge model and `docs/IMPLEMENTATION_STATUS.md` for the current production-readiness gap list.
