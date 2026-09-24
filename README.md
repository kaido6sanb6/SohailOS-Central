# SohailOS-Central

SohailOS-Central is the canonical control plane for a personal, model-agnostic AI operating system spanning research, thinking, software/product development, office work, automation, strategy and learning.

## Control-plane architecture

Clients -> Gateway -> Orchestration/Task Graph -> Policy/Approval -> Knowledge + Execution -> Verification/Validation -> Memory/Telemetry -> GitHub Source Mesh.

The architecture is federated. Repositories remain independent and are connected through provenance-aware retrieval, capability metadata and verified adapters.

## Autonomous GitHub ecosystem

The owner fork mesh is reconciled every 15 minutes.

The runtime:
1. discovers owned forks;
2. verifies parent/source provenance;
3. compares default branches;
4. classifies behind/ahead/diverged/in-sync/error states;
5. creates a deterministic graph and plan;
6. automatically syncs only strictly-behind forks;
7. quarantines divergence;
8. verifies the resulting state;
9. publishes audit evidence;
10. refreshes knowledge/index layers.

New forks are registered automatically and begin unverified/non-executable.

## Learning from the ecosystem

The inspected owner inventory contains 121 forks in the current snapshot and spans agent runtimes, model serving, research/data systems, UI clients, knowledge catalogs, prompt collections, networking and other specialized projects.

These repositories are evidence sources. Their source trees are not blindly merged. Central-to-fork propagation requires compatibility, provenance, CI/security evidence and rollback; arbitrary fork-to-fork merging is forbidden.

## SuperPrompt

The canonical contract is prompts/SohailOS-SuperPrompt.xlm and is aligned with the requested orchestrate-not-claim/adaptive-redteam protocol.

## Vercel

Vercel is reserved for an optional HTTPS control endpoint/dashboard and Cron integration. The connected Vercel account currently exposes no team/project in this session, so no live Vercel deployment or team creation is claimed. GitHub Actions is the active scheduler.

## Security

Never commit credentials, private keys, API keys, tokens or sensitive personal data. Runtime credentials must be short-lived and scoped.

See docs/ARCHITECTURE.md, docs/CONTINUOUS_ECOSYSTEM_CONTROL_PLANE.md, docs/ECOSYSTEM_KNOWLEDGE_LAYER.md, docs/IMPLEMENTATION_STATUS.md, prompts/SohailOS-SuperPrompt.xlm and ecosystem/policies/fork-propagation.json.
