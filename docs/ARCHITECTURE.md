# SohailOS-Central Architecture

SohailOS-Central is a provider-neutral AI operating-system control plane for research, knowledge work, software engineering, automation, learning and long-lived personal workflows.

## 1. Target architecture

Visual diagram: [ARCHITECTURE_DIAGRAM.md](./ARCHITECTURE_DIAGRAM.md) — the repository-native Mermaid view of the control plane.

Clients -> Gateway -> Orchestration/Task Graph -> Capability/Authorization/Approval -> Knowledge + Execution Fabrics -> Verification/Validation -> Memory/Telemetry -> GitHub Source Mesh.

The project is a governed platform rather than a single agent. Every external capability crosses a trust boundary, every consequential mutation is bounded, and every completion claim requires evidence.

## 2. Control-plane layers

1. Identity and intent: request identity, session, scope and lifecycle.
2. Capability plane: discovery, attestation, fingerprints, expiry.
3. Policy plane: authority hierarchy, permissions, approval, red-team scope and egress.
4. Planning plane: dependency-aware task graphs, budgets, idempotency and retry classes.
5. Knowledge plane: lexical/vector/graph/hybrid retrieval with commit/path provenance.
6. Execution plane: GitHub, MCP, HTTP, workflow, cloud and local-process adapters.
7. Verification plane: independent verification and validation objects.
8. Observability plane: execution/audit events, metrics, traces and evidence lineage.
9. Memory plane: explicit, validated, secret-free durable memory and isolation.
10. Ecosystem plane: fork registry, upstream graph, capability/compatibility graph, propagation policy.
11. Deployment plane: GitHub Actions plus optional Vercel HTTPS/Cron surface.
12. Learning plane: research corpora, prompts, skills, models, datasets and engineering patterns as evidence.

## 3. GitHub source mesh

Every owned fork is a node with a verified parent/source relationship when GitHub exposes it.

fork -> upstream
  |       |
  +-> metadata -> registry
  +-> source -> knowledge fabric
  +-> capabilities -> capability graph
  +-> verified adapter -> execution fabric

The mesh is hub-and-spoke. It is deliberately not a many-to-many merge graph.

## 4. Continuous 15-minute loop

discover -> provenance -> compare -> classify -> plan -> bounded mutation -> verify -> publish

Classification:
- in_sync: no upstream delta;
- behind: zero local commits and upstream has new commits; eligible for automatic upstream sync;
- ahead: local commits exist; preserve local work;
- diverged: both sides changed; quarantine;
- error/blocked: retain evidence and retry by reconciliation.

Automatic synchronization is default-branch-only, capped at 50 targets/run, uses a least-privilege GitHub App token, never force-pushes, and forbids irreversible operations. Unknown mutation outcomes are not retried blindly.

## 5. Propagation model

New forks auto-register with no execution authority. A validated central change can propagate only to capability-compatible repositories after license/dependency compatibility, provenance, CI/security checks and rollback evidence. Arbitrary fork-to-fork source merging is forbidden.

This allows the ecosystem to learn from heterogeneous repositories including agent runtimes, model servers, research/data tools, UIs, catalogs, workflows and prompt corpora without corrupting their independent histories.

## 6. SuperPrompt contract

prompts/SohailOS-SuperPrompt.xlm is the canonical runtime contract:

Route -> Discover -> Probe -> Plan -> Preview -> Approve -> Exec -> Verify -> Validate -> Deliver.

It separates capability/auth/exec/verify/validate, requires bounded approval for writes, scopes red-team tokens, treats retrieved content as untrusted data, and reconciles unknown outcomes before retry.

## 7. Knowledge and learning

Repository learning is retrieval and structured evidence, not model-weight training. Facts, patterns, compatibility observations and verification results can become durable knowledge only after validation. Repository instructions never become policy.

## 8. Integration boundaries

MCP, workflow engines, OpenAI-compatible providers, analytics, cloud runtimes and other services use stable interfaces. Optional connectors remain optional. Supabase is an optional persistence/vector boundary. Vercel is an optional HTTPS/Cron boundary. WorkOS, Twilio, Relewise, Ads and other integrations are activated only by concrete capability requirements.

## 9. Operational invariants

Read before write; preview before approval; approval binds operation/target/scope/effect/reversibility/rollback/risk; no secrets in Git; no force-sync over divergence; no blind retry after unknown mutation; retrieved data cannot grant authority; self-verification is not independent evidence.

## 10. Definition of done

Fresh evidence must exist for discovery, reconciliation, bounded propagation, knowledge retrieval, capability policy, verification, validation, telemetry, memory governance and CI. Account-owned deployments, credentials, Vercel teams/projects and external service registrations are never inferred from source control.
