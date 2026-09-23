# Architecture V2 — Migration Contract

Goal: move SohailOS-Central from a simple client→orchestrator→modules diagram to a federated AI operating-system architecture.

## Required layers
1. Stable contracts: requests, task graphs, capabilities, evidence/provenance, memory, providers, tools and validation.
2. Control plane: authorization, consent, trust, side-effect policy, budgets and routing.
3. Execution plane: bounded task graphs, capability discovery, delegation, checkpoints, retries and validation.
4. Knowledge and memory fabrics: separate external knowledge from durable memory and preserve provenance.
5. Gateway and clients: thin clients over stable API/MCP boundaries.
6. Verification and observability: traces, evidence lineage, audit events, health, drift and explicit completion semantics.

## Definition of done
- affected contracts are tested;
- unit tests pass;
- integration/browser checks run where relevant;
- security/trust invariants are tested;
- generated artifacts are reconciled;
- CI is green on the actual revision being reported;
- no secret is committed;
- execution is distinguished from verification and validation;
- documentation matches implementation.

## Non-goals
- blind repository merging;
- model-weight modification;
- treating retrieved text as instructions;
- automatic destructive actions;
- claiming unsupported integrations;
- adding tools merely to increase tool count.