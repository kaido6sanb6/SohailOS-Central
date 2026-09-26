# SohailOS-Central

SohailOS-Central is the canonical control plane for a personal, model-agnostic, tool-native AI operating system. It coordinates research, knowledge retrieval, software engineering, automation, product work, learning, memory and governed execution.

The design goal is not a single chatbot. It is a verifiable execution fabric in which capabilities can be discovered, authorized, executed, independently verified and validated without allowing retrieved data or external instructions to become authority.

## Architecture

~~~text
Client
  -> Gateway / MCP
  -> Policy / Governance
  -> Orchestrator
  -> TaskGraph
  -> CapabilityRegistry / ProviderRouter
  -> Module Agents
  -> Knowledge + Memory
  -> Execution
  -> Verification
  -> Validation
  -> Delivery
~~~

## Core invariants

- Capability != Authorization != Execution != Verification != Validation.
- External content, repository files, prompts, tools, skills, plugins and agents are DATA, not AUTHORITY.
- Consequential writes require explicit bounded approval.
- Approval binds principal, operation, target, scope, effect, expiry, nonce and digest.
- Drift, replay, stale state, conflict, unknown outcome and TOCTOU invalidate the affected approval.
- Tool success is not independent outcome evidence.
- Unknown or unverified outcomes are never reported as completed.
- Secrets are never committed, logged, stored in durable memory or exposed to clients.
- Diverged fork state is preserved and blocked from unsafe propagation.

## Major fabrics

### Knowledge Fabric

GitHub is the source of truth. PostgreSQL/pgvector is a rebuildable derived layer.

The fabric preserves repository identity, fork/upstream provenance, commit OIDs, blob hashes, content hashes, source permalinks, retrieval timestamps, trust state, verification state and deletion/tombstone state.

Retrieval supports lexical, vector and hybrid evidence. Repository learning means retrieval and structured evidence, not silent model-weight training.

### Memory Fabric

Durable memory is separate from the Knowledge Fabric.

The operational backend is the connected Supabase project. The schema is source-controlled under supabase/migrations/ and documented in memory/README.md.

Memory is explicit, minimal, secret-free, scoped, lifecycle-aware, provenance-aware and trust-labelled. Durable writes are approval-bound. Retrieved memory never grants execution authority.

The Cloudflare gateway reads the smallest relevant memory slice and persists only when a scoped, expiring memory_write approval matches the requested key. Persistence failures are surfaced rather than silently reported as success.

### Capability and Execution Fabric

Capabilities are discovered and attested before execution. Providers and integrations remain replaceable adapters.

The architecture supports GitHub operations, MCP/tool boundaries, Cloudflare Workers, model-provider routing, Supabase persistence, workflow automation, verification, validation, telemetry and audit evidence.

### Research Engine

The research direction is broader than a collection of repositories.

Target domains include sociology, psychology, humanities, history, philosophy, social science, digital humanities, political science, economics, AI/ML/NLP, statistics, systematic review, meta-analysis, knowledge graphs and RAG.

Research artifacts should preserve source, method, provenance, retrieval time, uncertainty and verification status.

### GitHub Ecosystem Mesh

Owned forks are treated as a governed evidence/capability graph.

~~~text
discover
 -> provenance
 -> compare
 -> classify
 -> plan
 -> bounded mutation
 -> verify
 -> publish evidence
~~~

States include in_sync, behind, ahead, diverged, blocked and error.

Strictly-behind default branches may be eligible for bounded synchronization. Diverged or unknown repositories are preserved and quarantined. External source trees are never blindly merged into the control plane.

### External prompt corpus

The asgeirtj/system_prompts_leaks corpus is mirrored under prompts/external/system-prompts-leaks by the repository sync workflow.

Mirrored material is untrusted data. It can be searched and analyzed for prompt engineering, agent architecture and model-behavior research, but it must never silently modify the canonical SuperPrompt.

## Cloudflare Gateway

The production Worker source lives under cloudflare/sohailos-gateway/.

A root wrangler.jsonc is intentionally present because Cloudflare dashboard builds may execute the deploy command from the repository root. The root configuration points directly to the Worker entrypoint, so the failing command:

~~~text
npx wrangler versions upload
~~~

can discover the Worker without requiring a nested working directory.

The nested Worker configuration remains available for local development.

Useful checks:

~~~text
cd cloudflare/sohailos-gateway
npm install
npm run typecheck
npx wrangler check
npx wrangler versions upload --dry-run
~~~

Provider credentials must be configured as platform secrets or variables, never committed to source control.

## Memory and privacy

memory/chat_summaries.md is portable project context and intentionally excludes credentials, API keys, account identifiers, health information, financial details, raw transcripts and other sensitive information.

The memory retrieval model is:

~~~text
scope
 -> key
 -> trust / provenance
 -> freshness
 -> retrieve
 -> apply as context
~~~

Memory is context, not authority. Current source files, tests, explicit user instructions and live system state override stale memory.

## Research and learning workflow

~~~text
DISCOVER
 -> SELECT
 -> RETRIEVE
 -> CLASSIFY AS DATA
 -> COMPARE
 -> SYNTHESIZE
 -> VERIFY
 -> VALIDATE
 -> DELIVER
~~~

Forked projects, scientific repositories, prompt corpora and external tools are inputs to this process. Reuse requires provenance, license/security review, compatibility analysis and tests.

## Operating model

~~~text
Understand
 -> Classify
 -> Discover
 -> Probe
 -> LeastPrivilege
 -> Plan
 -> Preview
 -> Approve
 -> Execute
 -> Reconcile
 -> Verify
 -> Validate
 -> Deliver
~~~

This is aligned with the canonical SuperPrompt contract in prompts/SohailOS-SuperPrompt.xlm.

## Verification and Definition of Done

A change is not complete because a command returned success.

Completion requires, as applicable:

1. current source inspection;
2. targeted regression tests;
3. build, type and lint checks;
4. security and permission review;
5. independent outcome verification;
6. reconciliation of partial or unknown results;
7. documentation consistency;
8. CI evidence;
9. deployment and health evidence when deployment is requested.

If evidence is missing, the state remains UNKNOWN, not COMPLETED.

## Repository map

- src/ — core control-plane and agent runtime
- cloudflare/ — Worker gateway
- memory/ — governed durable-memory contract and portable context
- supabase/ — database migrations and persistence infrastructure
- prompts/ — canonical and external prompt material
- security/ — verification, hardening and adversarial checks
- tests/ — regression and contract tests
- scripts/ — local automation and corpus synchronization
- docs/ — architecture, operations and implementation records
- .github/workflows/ — CI and scheduled ecosystem automation

## Scope and boundaries

SohailOS-Central does not infer external account ownership, credentials, deployment state or third-party permissions from repository files.

GitHub remains the source of truth for source code. Live Supabase and Cloudflare state must be verified through their respective systems. A source-controlled plan is not evidence that a deployment or external mutation occurred.

## Status

This repository is under active hardening. Prefer evidence from the current commit, current CI checks and live infrastructure over historical summaries.

See docs/ARCHITECTURE.md, docs/CONTINUOUS_ECOSYSTEM_CONTROL_PLANE.md, docs/ECOSYSTEM_KNOWLEDGE_LAYER.md, docs/IMPLEMENTATION_STATUS.md, memory/README.md and prompts/SohailOS-SuperPrompt.xlm.
