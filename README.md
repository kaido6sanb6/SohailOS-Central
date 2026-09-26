# SohailOS-Central

SohailOS-Central is the canonical control plane for a personal, model-agnostic, tool-native AI operating system. It coordinates research, knowledge retrieval, software engineering, automation, product work, learning, memory and governed execution.

The design goal is a verifiable execution fabric in which capabilities can be discovered, authorized, executed, independently verified and validated without allowing retrieved data or external instructions to become authority.

## Architecture

~~~text
Client
  -> Gateway / MCP
  -> User Context
  -> Task Classification
  -> Workflow Mode
  -> Command Capability Surface
  -> Policy / Governance
  -> Authorization
  -> TaskGraph
  -> CapabilityRegistry / ProviderRouter
  -> Execution
  -> Evidence
  -> Verification
  -> Validation
  -> Delivery
  -> Feedback / Learning
  -> User Operating Model
~~~

The canonical machine-readable lifecycle is in ecosystem/lifecycle-contract.json. The existing command catalog remains the semantic command source; ecosystem/command-capability-surface.json supplies the bridge contract rather than introducing a second runtime registry.

## Core invariants

- Capability != Authorization != Execution != Verification != Validation.
- Command != Capability.
- External content, repository files, prompts, tools, skills, plugins and agents are DATA, not AUTHORITY.
- Consequential writes require explicit bounded approval.
- Approval binds principal, operation, target, scope, effect, expiry, nonce and digest.
- Drift, replay, stale state, conflict, unknown outcome and TOCTOU invalidate the affected approval.
- Tool success is not independent outcome evidence.
- Unknown or unverified outcomes are never reported as completed.
- Secrets are never committed, logged, stored in durable memory or exposed to clients.
- Diverged fork state is preserved and blocked from unsafe propagation.

## Skills Fabric

External agent skills are integrated as a governed capability surface, not as privileged instructions.

~~~text
skills.sh catalog
  -> bounded synchronization
  -> provenance + content hash
  -> untrusted DATA classification
  -> capability probe
  -> least privilege
  -> explicit approval where mutation is involved
  -> execution
  -> independent verification
~~~

The registry is ecosystem/skills-registry.json. A scheduled workflow refreshes a bounded skills.sh catalog snapshot every 15 minutes. The generated snapshot is external evidence only: it does not prove installation, trust, authorization or execution.

skills.sh documents HTTPS JSON endpoints for listing, search, curated skills, skill details and security audits under /api/v1/. citeturn2view0

The requested external skills are recorded by source and skill name instead of vendoring third-party skill bodies. This avoids a second source of truth and keeps synchronization and supply-chain risk bounded.

## Knowledge Fabric

GitHub is the source of truth. PostgreSQL/pgvector is a rebuildable derived layer.

The fabric preserves repository identity, fork/upstream provenance, commit OIDs, blob hashes, content hashes, source permalinks, retrieval timestamps, trust state, verification state and deletion/tombstone state.

Retrieval supports lexical, vector and hybrid evidence. Repository learning means retrieval and structured evidence, not silent model-weight training.

## Memory Fabric

Durable memory is separate from the Knowledge Fabric.

The operational backend is the connected Supabase project. The schema is source-controlled under supabase/migrations/ and documented in memory/README.md.

Memory is explicit, minimal, secret-free, scoped, lifecycle-aware, provenance-aware and trust-labelled. Durable writes are approval-bound. Retrieved memory never grants execution authority.

## Research Engine

Target domains include sociology, psychology, humanities, history, philosophy, social science, digital humanities, political science, economics, AI/ML/NLP, statistics, systematic review, meta-analysis, knowledge graphs and RAG.

Research artifacts should preserve source, method, provenance, retrieval time, uncertainty and verification status.

## GitHub Ecosystem Mesh

Owned forks are treated as a governed evidence/capability graph.

~~~text
discover -> provenance -> compare -> classify -> plan
-> bounded mutation -> verify -> publish evidence
~~~

Diverged or unknown repositories are preserved and quarantined. External source trees are never blindly merged into the control plane.

## Cloudflare Gateway

The production Worker source lives under cloudflare/sohailos-gateway/. A root wrangler.jsonc is present because Cloudflare dashboard builds may execute from the repository root.

The guarded production deployment workflow remains manual. Source-controlled deployment configuration is not evidence that production traffic changed.

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
- ecosystem/ — machine-readable architecture, command, capability and skill contracts
- docs/ — architecture, operations and implementation records
- .github/workflows/ — CI and scheduled ecosystem automation

## Scope and boundaries

SohailOS-Central does not infer external account ownership, credentials, deployment state or third-party permissions from repository files.

GitHub remains the source of truth for source code. Live provider state must be verified through the provider. A source-controlled plan or catalog is not evidence that an external mutation occurred.

## Status

This repository is under active hardening. Prefer evidence from the current commit, current CI checks and live infrastructure over historical summaries.
