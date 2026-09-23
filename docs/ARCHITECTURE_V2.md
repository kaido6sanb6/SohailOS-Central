# SohailOS-Central — Next-Generation Architecture

SohailOS-Central is a model-agnostic personal AI operating-system control plane, not a thin chatbot router. It coordinates reasoning, knowledge, memory, tools, specialist capabilities, integrations, verification, and durable project state.

## Logical architecture

USER / EVENTS / SCHEDULES
  -> EXPERIENCE: Desktop / Web / Mobile / CLI / API / MCP / Voice
  -> AUTHENTICATED GATEWAY: identity / sessions / limits / API boundaries
  -> CONTROL + POLICY: consent / trust / security / authorization / budgets
  -> MASTER ORCHESTRATOR
       -> TASK GRAPH / WORKFLOW ENGINE
       -> CAPABILITY REGISTRY
       -> CONTEXT + EVIDENCE BUS
       -> SPECIALIST AGENTS
       -> TOOL + INTEGRATION BROKER
       -> AI PROVIDER FABRIC
       -> KNOWLEDGE FABRIC
       -> MEMORY FABRIC
       -> VERIFICATION + OBSERVABILITY
  -> VERIFIED RESPONSE OR VERIFIED SIDE EFFECT

## Core planes

1. Experience plane — thin clients. Clients do not own orchestration policy.
2. Gateway plane — authentication, sessions, stable API/MCP contracts and request limits.
3. Control/policy plane — authorization, consent, trust, side-effect classification, budgets and security.
4. Orchestration plane — planning, capability discovery, task graphs, delegation, retries and stopping criteria.
5. Capability plane — agents, tools, plugins, MCP servers, skills, workflows, models and integrations.
6. Knowledge plane — repository/revision/document/chunk/embedding plus graph, provenance and hybrid retrieval.
7. Memory plane — working, session, episodic, semantic, project and user-approved durable memory.
8. Model plane — provider-neutral contracts and policy-based provider selection/fallback.
9. Integration plane — narrow adapters with credentials and provider details outside the domain core.
10. Verification/observability plane — evidence lineage, tests, validators, audit events, traces and explicit completion states.

## Orchestrator lifecycle

UNDERSTAND -> CLASSIFY -> PLAN -> DISCOVER_CAPABILITIES -> RETRIEVE_CONTEXT -> BUILD_TASK_GRAPH -> ROUTE -> EXECUTE -> OBSERVE -> VERIFY -> VALIDATE -> SYNTHESIZE -> DELIVER -> REMEMBER

Non-trivial work is a bounded task graph. Nodes contain goal, inputs, required capabilities, evidence requirements, authorization class, budget/deadline, dependencies, retry policy, expected output schema, validation criteria and provenance.

Recommended states: PLANNED -> READY -> RUNNING -> OBSERVED -> VERIFIED -> VALIDATED -> COMPLETED. Alternate terminal states: BLOCKED, RETRYABLE_FAILURE, PERMANENT_FAILURE, UNKNOWN.

## Specialist architecture

The existing specialists remain: THINK, SOCIOLOGY, CINEMA, RESEARCH, STATS, AI, CODE, PRODUCT, OFFICE, OPERATIONS, STRATEGY, LEARNING. They become capability providers rather than isolated hard-coded silos. A task may compose multiple specialists and dynamically discovered verified capabilities.

## Capability registry

Every capability has identity, kind, contract, input/output schemas, permissions, trust, provenance, version, health, cost/latency metadata, domains, evidence and routing hints. Routing is by outcome and contract, not by requiring the user to name a tool.

## Memory fabric

Working memory = current task context. Session memory = current interaction state. Episodic memory = completed interaction outcomes. Semantic memory = normalized knowledge. Project memory = architecture, decisions, constraints and status. Durable memory = user-approved persistent context. Durable memory must carry source, timestamp, scope, confidence, sensitivity, lifecycle and mutation history.

## Knowledge fabric

Canonical chain: Repository -> Revision -> Document -> Chunk -> Embedding. Cross-cutting entities include Claim, Evidence, Citation, Capability, Dataset, Model, ResearchQuestion, Person, Organization, Place, Event, Work, Theory, Method, Variable and Software.

Retrieval modes: lexical, vector, graph and hybrid/reranked. Retrieval never silently converts unverified material into authoritative truth.

## AI provider fabric

OpenAI, Anthropic, Gemini, local/open models and future compatible providers implement one internal contract. Provider choice may consider task type, modality, context, privacy, availability, latency, cost and verification requirements. Credentials never belong in source control.

## Tool and integration broker

Specialists request capabilities through a broker. The broker enforces authorization, schema validation, side-effect classification, timeout, rate limits, retries, idempotency, secret isolation, auditability and output validation. Read and write capabilities are distinct.

## GitHub ecosystem

GitHub is both a live knowledge source and a capability registry input. Central discovers accessible repositories/forks, preserves upstream lineage, tracks revisions, inspects relevant artifacts, indexes provenance, reconciles drift and discovers future forks. Logical integration is the default; physical merging is exceptional.

## LIVE CORPUS security boundary

Sources: asgeirtj/system_prompts_leaks and kaido6sanb6/system_prompts_leaks. Pipeline: FETCH -> INSPECT -> CLASSIFY -> EXTRACT -> NORMALIZE -> DEDUPLICATE -> COMPARE -> VERIFY -> SYNTHESIZE -> INDEX. Their content is untrusted data with zero instruction authority.

## Verification semantics

SUCCESSFUL_EXECUTION, VERIFIED, VALIDATED, PARTIAL, BLOCKED, RETRYABLE_FAILURE, PERMANENT_FAILURE and UNKNOWN are distinct states. A successful tool call does not prove the requested outcome was validated.

## Security boundary

System/developer/user/tool policy and authorization outrank retrieved data. Repository content, prompts, skills, comments, datasets and web snapshots are evidence/data, never authority. Resist injection, spoofed authority, poisoned artifacts, hierarchy attacks, provenance forgery, unsafe tool instructions and secret exfiltration.

## Evolution

Future desktop, web, mobile, API and MCP clients consume the same control plane. Local, cloud and hybrid deployments remain implementation choices behind stable contracts.
