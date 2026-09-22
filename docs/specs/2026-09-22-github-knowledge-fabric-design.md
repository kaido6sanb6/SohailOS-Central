---
spec_id: sohailos-central.github-knowledge-fabric
version: 0.2.0-draft
status: DRAFT
approval: NOT_APPROVED
implementation: NONE
normative_language: RFC2119
supersedes: 0.1.0 (design branch, unmerged)
created: 2026-09-22
owners:
  - SohailOS-Central (control plane)
consumers:
  - SohailOS.Gateway (read-only retrieval)
  - MCP clients (read-only)
---

# SohailOS-Central — GitHub Knowledge Fabric

> **Provenance of this revision.** This document was reconstructed from the
> change request and the enumerated design constraints supplied by the owner.
> It has now been reconciled against the existing 0.1.0 design file and current
> repository architecture. Passages that intentionally formalize new normative
> requirements are recorded below as specification changes. No implementation
> of this 0.2.0 design is claimed.

---

## 0. Document Control

### 0.1 Normative Language

The key words MUST, MUST NOT, REQUIRED, SHALL, SHALL NOT, SHOULD, SHOULD NOT,
RECOMMENDED, MAY, and OPTIONAL are to be interpreted as described in RFC 2119.

### 0.2 Status Semantics

| Field | Value | Meaning |
|-------|-------|---------|
| \`status\` | DRAFT | Not approved; subject to change |
| \`approval\` | NOT_APPROVED | Section 15 checklist incomplete |
| \`implementation\` | NONE | No Knowledge-Fabric-specific migrations, indexers, jobs, endpoints, or MCP tools are enabled; the existing general SohailOS.Gateway MCP server remains an existing platform component |

### 0.3 Change Protocol

Any change to a MUST-level requirement MUST increment the minor version and
MUST be recorded in Appendix A. Any change to the identifier scheme
(Section 3), the provider contract (Section 4), or the MCP allowlist
(Section 11) MUST increment the major version.

---

## 1. Scope and Non-Goals

### 1.1 Scope

This specification defines the GitHub Knowledge Fabric:

- **GitHub** is the canonical source of content.
- **SohailOS-Central** is the control plane.
- **PostgreSQL/pgvector** (or a conforming substitute) is a **derived,
  rebuildable** storage layer. It is not a system of record.
- **SohailOS.Gateway** exposes read-only retrieval over MCP.

In scope:

1. Repository discovery and authorization policy
2. Canonical identity and provenance model
3. Provider-agnostic vector storage contract
4. Incremental indexing with a defined state machine
5. Hybrid (lexical + vector) ranking
6. Current-state and historical (point-in-time) retrieval
7. Trust classification and prompt-injection containment
8. Read-only Gateway/MCP surface
9. Deployment dependency gates
10. Observability, retention, and deletion

### 1.2 Non-Goals

The system MUST NOT:

| ID | Prohibition |
|----|-------------|
| NG-1 | Write to, mutate, or open pull requests against any GitHub repository |
| NG-2 | Serve as a system of record for repository content |
| NG-3 | Provide a general-purpose SQL, shell, or network proxy |
| NG-4 | Perform autonomous code execution, dependency installation, or test running |
| NG-5 | Act as a secrets manager or credential broker |
| NG-6 | Expose write-capable MCP tools in any phase covered by this spec |
| NG-7 | Guarantee recall over content not present in GitHub at index time |
| NG-8 | Infer trust tier from content heuristics alone |
| NG-9 | Treat retrieved content as instructions, configuration, or policy |

### 1.3 Authority

- GitHub commit objects are **authoritative** for content.
- The derived index is authoritative for **nothing**; it is a cache with
  ranking metadata.
- If the derived index and GitHub disagree, GitHub wins and the index MUST
  be re-derived (Section 2.2, rule C2).

---

## 2. Source-of-Truth Hierarchy and Conflict Resolution

### 2.1 Hierarchy

| Rank | Layer | Authority | Mutability | Rebuildable |
|------|-------|-----------|------------|-------------|
| 0 | Git commit object (\`git_oid\`) | Absolute | Append-only | N/A |
| 1 | GitHub tree/blob at a commit | Canonical | Immutable per commit | N/A |
| 2 | Repository default-branch pointer | Canonical for "current" | Mutable | N/A |
| 3 | Derived document/chunk records | None | Derived | Yes |
| 4 | Vector embeddings | None | Derived | Yes |
| 5 | Gateway response cache | None | Ephemeral | Yes |

### 2.2 Conflict Resolution Rules

- **C1.** A derived record MUST carry the \`git_oid\` it was derived from.
  A record with no \`git_oid\` MUST be treated as invalid and purged.
- **C2.** On disagreement between a derived record and GitHub at the recorded
  \`git_oid\`, the derived record MUST be discarded and re-derived. Silent
  reconciliation is PROHIBITED.
- **C3.** On force-push or history rewrite, all derived records for affected
  paths MUST be tombstoned (Section 14.4), not updated in place, so that
  provenance remains auditable.
- **C4.** Deletion in GitHub MUST propagate as deletion in the derived layer
  within the retention window (Section 14).
- **C5.** The system MUST NOT present derived content to a consumer without
  the provenance envelope defined in Section 7.

---

## 3. Canonical Data Model and Stable Identifiers

### 3.1 Identifier Rules

- **ID1.** Identifiers MUST NOT use mutable attributes as their primary key.
  Owner/name renames and repository transfers MUST NOT change identity.
- **ID2.** Repository identity MUST be derived from the GitHub **numeric**
  repository ID, which is stable across rename and transfer.
- **ID3.** Path-based identifiers MUST be scoped by \`git_oid\` where the
  path's content is referenced.
- **ID4.** All identifiers MUST be deterministic: identical inputs MUST
  produce identical identifiers across runs, hosts, and providers.

### 3.2 Identifier Forms

\`\`\`
repo_id            gh:repo:<github_numeric_id>
doc_id             gh:doc:<github_numeric_id>:<normalized_path>
revision_id        gh:rev:<git_oid>:<normalized_path>
chunk_id           gh:chunk:<sha256(normalized_chunk_bytes)>:<ordinal>
embedding_gen_id   emb:<provider>:<model>:<dim>:<norm_version>
run_id             run:<ulid>
\`\`\`

\`normalized_path\` MUST be NFC-normalized, forward-slash separated, and
case-preserved. Case-folded comparison MUST NOT be used for identity.

### 3.3 Entities

| Entity | Key | Required Attributes |
|--------|-----|---------------------|
| \`repository\` | \`repo_id\` | \`full_name\`, \`default_branch\`, \`visibility\`, \`trust_tier\`, \`authorized_at\`, \`last_seen_at\` |
| \`source_revision\` | \`revision_id\` | \`repo_id\`, \`git_oid\`, \`path\`, \`blob_sha\`, \`content_hash\`, \`size_bytes\`, \`fetched_at\` |
| \`document\` | \`doc_id\` | \`repo_id\`, \`normalized_path\`, \`current_revision_id\`, \`language\`, \`is_binary\`, \`deleted_at\` |
| \`chunk\` | \`chunk_id\` | \`doc_id\`, \`revision_id\`, \`ordinal\`, \`byte_start\`, \`byte_end\`, \`content_hash\`, \`text\` |
| \`embedding\` | \`(embedding_gen_id, chunk_id)\` | \`vector\`, \`created_at\` |
| \`run\` | \`run_id\` | \`repo_id\`, \`git_oid\`, \`state\`, \`started_at\`, \`ended_at\`, \`phase_timings\`, \`counts\`, \`degraded_flags\` |
| \`edge\` | \`(src_doc_id, dst_doc_id, kind)\` | \`kind\`, \`confidence\`, \`derived_from_run\` |

> **SOURCE-DEPENDENT RECONCILIATION:** The 0.1.0 draft named these logical
> records \`sources\`, \`documents\`, \`chunks\`, \`knowledge_edges\`, and
> \`index_runs\`. The 0.2.0 names above are normative logical entity names;
> physical table names remain an implementation decision until schema freeze.

### 3.4 Embedding Generations

- **G1.** Every embedding MUST be tagged with an \`embedding_gen_id\` capturing
  provider, model, dimensionality, and normalization version.
- **G2.** Embeddings from different generations MUST NOT be compared or
  co-ranked in a single vector search. Queries MUST filter to exactly one
  \`embedding_gen_id\`.
- **G3.** A generation change MUST NOT require a full delete-then-rebuild
  outage. Generations MAY coexist; the active generation is selected by
  configuration and reported in every response.
- **G4.** A chunk MAY have zero embeddings (lexical-only). This MUST be
  representable without error.

---

## 4. Provider-Agnostic Vector Storage Contract

The control plane MUST depend on an abstract interface, never on a specific
vendor or extension directly.

### 4.1 Required Interface

The following is language-neutral pseudocode; the production implementation
may be written in the repository's existing implementation language.

\`\`\`text
Protocol DataPlaneProvider:

    # --- lifecycle ---
    health_check() -> HealthStatus
    capabilities() -> {
        vector: bool,
        lexical: bool,
        iterative_scan: bool,
        max_dim: int,
    }
    migrate(target_version: str) -> MigrationResult

    # --- writes (indexing role) ---
    upsert_revisions(revisions: [SourceRevision]) -> WriteResult
    upsert_chunks(chunks: [Chunk]) -> WriteResult
    upsert_embeddings(gen_id: str, items: [(ChunkId, Vector)]) -> WriteResult
    tombstone(ids: [str], reason: str) -> WriteResult
    record_run(run: Run) -> WriteResult

    # --- reads (retrieval role; MUST use a distinct credential, Sec 11.4) ---
    lexical_search(query: str, filters: Filters, k: int) -> [Hit]
    vector_search(vector: [float], gen_id: str, filters: Filters, k: int) -> [Hit]
    get_document(doc_id: str, as_of: str | None) -> Document | None
    get_source(revision_id: str) -> SourcePointer
    list_generations() -> [str]
\`\`\`

### 4.2 Conformance Requirements

- **P1.** The system MUST ship at least one provider implementation that
  requires no external service (\`null\` / in-memory) sufficient to satisfy
  Gate 0 of Section 12.
- **P2.** Provider selection MUST be configuration-driven and MUST NOT
  require code changes.
- **P3.** \`capabilities()\` MUST be consulted before issuing a query that
  depends on iterative index scans or lexical ranking.
- **P4.** If the provider cannot satisfy a requested capability, the system
  MUST degrade explicitly (Section 6.5) rather than silently returning
  incomplete results.
- **P5.** Provider-specific schema qualification and extension placement
  MUST be encapsulated in the provider. No caller may embed
  provider-specific SQL.

---

## 5. Repository Discovery and Authorization Policy

### 5.1 Discovery

- **D1.** The control plane MUST maintain an explicit allowlist of authorized
  repositories or organizations. Discovery MUST NOT be "all repositories
  visible to the token."
- **D2.** New repositories matching an authorized organization scope MUST be
  enrolled automatically, subject to D3, and MUST be visible as
  \`pending_authorization\` until D3 completes.
- **D3.** Enrollment MUST record: \`repo_id\`, the scope that authorized it,
  the timestamp, and the policy version in force.
- **D4.** If a repository is transferred out of an authorized scope, it MUST
  be tombstoned (Section 14.4).

### 5.2 Authorization Boundaries

- **A1.** Private repository content MUST NOT be indexed unless the
  authorization policy explicitly permits private ingestion for that scope.
- **A2.** Visibility MUST be recorded per repository and MUST be enforced at
  query time, not merely at index time.
- **A3.** Tokens MUST be scoped to the minimum read permissions required.
  Tokens with write scope MUST NOT be used by the control plane.
- **A4.** Authorization decisions MUST be evaluated per query, using the
  caller's identity where the caller identity is available.

> **OPEN QUESTION OQ-1** — Is Gateway single-tenant (one authorization
> context) or multi-tenant (per-caller visibility)? This determines whether
> per-document ACLs are required and MUST be resolved before schema freeze,
> because retrofitting ACLs after indexing is expensive.

---

## 6. Incremental Indexing State Machine and Failure Handling

### 6.1 States

\`\`\`
DISCOVERED → AUTHORIZED → QUEUED → FETCHING → PARSING → CHUNKING
  → EMBEDDING → UPSERTING → COMMITTED

Terminal / exceptional:
  FAILED_RETRYABLE
  FAILED_PERMANENT
  SUPERSEDED
  PARTIAL
  TOMBSTONED
\`\`\`

### 6.2 Transition Requirements

- **S1.** Every transition MUST be recorded with timestamp and reason in the
  \`run\` ledger.
- **S2.** \`PARTIAL\` MUST be a first-class, queryable state. A partial index
  MUST be reported as degraded in every response derived from it.
- **S3.** The pipeline MUST be resumable from any non-terminal state without
  re-embedding already-embedded content.
- **S4.** Processing MUST be idempotent: re-running on an unchanged \`git_oid\`
  MUST produce no new embeddings and no duplicate chunks.
- **S5.** \`SUPERSEDED\` MUST be set when a newer \`git_oid\` for the same
  \`doc_id\` commits. The older revision MUST remain retrievable for historical
  queries (Section 9).

### 6.3 Incremental Decision Rules

- **I1.** If \`blob_sha\` is unchanged, the system MUST NOT fetch blob content
  or re-chunk.
- **I2.** If \`content_hash\` of a chunk is unchanged, the system MUST NOT
  re-embed that chunk.
- **I3.** Identical \`content_hash\` values across chunks SHOULD share a single
  embedding record keyed by content hash, to reduce cost and index size.
- **I4.** Embedding MUST be skipped (not failed) for content classified as
  binary, generated, vendored, or excluded by policy. Exclusions MUST be
  recorded.

### 6.4 Failure Handling

| Failure | Classification | Required Behavior |
|---------|---------------|-------------------|
| GitHub rate limit | Retryable | Backoff with jitter; resume from last committed state |
| Auth / permission denied | Permanent | Mark repo \`unauthorized\`; alert; do not retry |
| Blob fetch timeout | Retryable | Bounded retries; then \`PARTIAL\` |
| Embedding provider unavailable | Retryable | Commit lexical-only; set \`semantic_degraded\` |
| Data plane unavailable | Retryable | Halt before commit; no partial writes visible |
| Schema / extension missing | Permanent | Fail fast with actionable error; block Gate 2 |
| Malformed content | Retryable → Permanent | Skip chunk; record skip; continue run |

- **F1.** No run may leave the derived layer in a state that is visible to
  retrieval but not marked \`COMMITTED\` or \`PARTIAL\`.
- **F2.** Every failure MUST record the \`phase\` at which it occurred.

### 6.5 Degraded Modes

The following MUST be explicitly representable in responses, not inferred by
the caller:

\`\`\`
semantic_degraded
lexical_only
stale
partial_index
generation_mismatch
historical_fallback
\`\`\`

---

## 7. Provenance Contract

Every retrieved chunk MUST be returned with a provenance envelope. A response
missing any required field MUST be treated as a system error, not a partial
success.

### 7.1 Required Fields

| Field | Description |
|-------|-------------|
| \`repo_id\` | Stable repository identifier |
| \`repo_full_name\` | Human-readable, current at response time |
| \`path\` | Repository-relative path |
| \`git_oid\` | Full commit SHA the content was derived from |
| \`blob_sha\` | Git blob SHA |
| \`content_hash\` | Hash of the indexed bytes |
| \`chunk_id\` | Chunk identifier |
| \`ordinal\` | Position within the document |
| \`byte_start\` | Offset into the source blob (start) |
| \`byte_end\` | Offset into the source blob (end) |
| \`indexed_at\` | When this revision was indexed |
| \`embedding_gen_id\` | Or \`null\` if lexical-only |
| \`trust_tier\` | Section 10 |
| \`degraded_flags\` | Section 6.5 |
| \`permalink\` | GitHub permalink pinned to \`git_oid\` |

### 7.2 Rules

- **PV1.** The permalink MUST be pinned to the commit, never to a branch name.
- **PV2.** Provenance MUST survive ranking; the ranker may not drop fields.
- **PV3.** The envelope MUST be identical in shape for lexical, vector, and
  hybrid results.

---

## 8. Hybrid Search Ranking and Evaluation

### 8.1 Fusion

The system MUST use a documented, deterministic fusion function.
Recommended default:

\`\`\`
score(d) = Σ_r  w_r * 1 / (k + rank_r(d))

  k            = 60
  w_lexical    = 1.0   (configurable)
  w_vector     = 1.0   (configurable)
\`\`\`

- **H1.** Ties MUST be broken deterministically (by \`chunk_id\` ascending) so
  that results are reproducible.
- **H2.** The fusion constant and weights MUST be versioned and reported in
  the response.
- **H3.** Weights and \`k\` MUST NOT be tuned on the evaluation set used to
  report quality.

### 8.2 Filters

Filters MUST be applied **inside** the provider, not post-hoc on a truncated
candidate set, when the provider supports it. Where iterative index scanning
is unavailable, the system MUST over-fetch by a configured factor and MUST
report \`approximate: true\`.

- **H4.** Trust filters (Section 10) MUST be applied before result truncation.
- **H5.** Visibility filters MUST be applied before result truncation.

### 8.3 Evaluation Metrics

The system SHOULD report, on a versioned evaluation set:

| Metric | Target |
|--------|--------|
| \`recall@5\` | Reported, no target yet |
| \`recall@10\` | Reported, no target yet |
| \`recall@50\` | Reported, no target yet |
| \`nDCG@10\` | Reported, no target yet |
| \`MRR\` | Reported, no target yet |
| \`provenance_completeness_rate\` | 1.0 |
| \`staleness_rate\` | Reported |
| \`degraded_result_rate\` | Reported |

> **OPEN QUESTION OQ-2** — No evaluation corpus is defined. Until one exists,
> quality claims MUST NOT be made.

---

## 9. Current vs Historical Retrieval

### 9.1 Modes

| Mode | Semantics | Requires |
|------|----------|----------|
| \`current\` | Resolve to default-branch tip at query time | Default-branch mapping |
| \`as_of\` | Newest indexed revision with \`committed_at <= T\` | Retained history |
| \`at_commit\` | Exact \`git_oid\` | That commit retained |

### 9.2 Rules

- **T1.** Mode MUST default to \`current\`.
- **T2.** \`as_of\` MUST NOT fabricate a commit. If the requested time predates
  the retention horizon, the system MUST return an explicit
  \`historical_unavailable\` marker rather than the nearest available content.
- **T3.** \`at_commit\` for a non-indexed commit MUST fail explicitly, not fall
  back silently.
- **T4.** Every response MUST state which mode was used and how the revision
  was resolved.

---

## 10. Trust Filtering and Instruction Containment

### 10.1 Trust Tiers

| Tier | Meaning | Default Query Treatment |
|------|---------|-------------------------|
| \`authoritative\` | Owned, controlled, protected-branch content | Included, ranked normally |
| \`verified\` | Allowlisted, reviewed origin | Included, ranked normally |
| \`reference_only\` | Public, unvetted | Included with reduced weight; MUST be labeled |
| \`unverified\` | Unknown or quarantined | Excluded from ranking by default; retrievable only on explicit request |
| \`tombstoned\` | Deleted or revoked | Never returned in any mode |

- **TR1.** Tier MUST be recorded per repository, not inferred at query time
  from heuristics alone.
- **TR2.** Tier changes MUST be versioned and auditable.
- **TR3.** \`unverified\` content MUST NOT be silently promoted to \`verified\`
  by any automated process.

### 10.2 Data-Not-Instructions

- **TR4.** All retrieved content MUST be treated as **data**. It MUST NOT be
  interpreted as instructions, tool calls, configuration, or policy.
- **TR5.** Retrieved content MUST be delimited in any prompt assembly, with a
  stable, non-content-collidable boundary marker.
- **TR6.** Content containing instruction-like patterns (imperative
  directives aimed at a model, embedded tool schemas, credential requests,
  exfiltration URLs) MUST be flagged as \`injection_suspected\` and MUST NOT
  be returned as trusted context.
- **TR7.** The Gateway MUST NOT execute, evaluate, or interpolate retrieved
  content into any executable context.
- **TR8.** Provenance and trust labels MUST be conveyed to the consumer
  alongside the content so that downstream policy can act on them.

---

## 11. Gateway and MCP Contract

### 11.1 Allowed Operations

Exactly three Knowledge-Fabric tools, all read-only:

| Tool | Category | \`readOnlyHint\` | Notes |
|------|----------|------------------|-------|
| \`ecosystem.search\` | READ | \`true\` | Hybrid search with filters |
| \`ecosystem.get_document\` | READ | \`true\` | Full document, current or historical |
| \`ecosystem.get_source\` | READ | \`true\` | Returns provenance pointer, not raw credentials |

- **M1.** The Knowledge-Fabric MCP surface MUST register exactly these three
  tools. Registration is allowlist-based, not denylist-based.
- **M2.** These tools MUST be additive-only in any given deployment; removing a
  tool requires a spec revision.

### 11.2 Prohibited Operations

The Knowledge-Fabric MCP surface MUST NOT expose:

\`\`\`
file writes
repository mutation
arbitrary SQL
shell execution
outbound network fetch
secret or token access
index mutation
any tool with a non-read-only hint
\`\`\`

### 11.3 Response Requirements

Every response MUST include:

\`\`\`
results[] with provenance envelopes (Section 7)
mode
fusion_version
embedding_gen_id
degraded_flags
trust_tier            (per result)
data_not_instructions: true
\`\`\`

### 11.4 Credential Separation

- **M3.** The retrieval path MUST use a distinct credential with read-only
  privileges on the derived layer.
- **M4.** The indexing path MUST use a separate credential with write
  privileges.
- **M5.** Neither credential MAY be exposed to a Gateway client.

---

## 12. Deployment Dependency Gates

> **No Supabase project is currently connected. This is a hard blocking
> dependency for persistent external vector search. Gate 0 may be exercised
> without external storage; nothing requiring Gates 1–8 is enabled until its
> prerequisites are met.**

| Gate | Requirement | Blocking | Verification |
|------|-------------|----------|--------------|
| 0 | \`null\` / in-memory provider satisfies the \`DataPlaneProvider\` contract; system runs with no external DB | No | Conformance test suite passes |
| 1 | A PostgreSQL-compatible project exists and is reachable | **Yes** | \`health_check()\` returns healthy |
| 2 | Vector extension installed and enabled in the correct schema | **Yes** | Extension version query returns ≥ required version |
| 3 | Schema migrated to the pinned version | **Yes** | Migration reports idempotent success on re-run |
| 4 | Distinct read-only and write roles exist with correct grants | **Yes** | Negative test: read role cannot INSERT |
| 5 | Indexing pipeline completes one full run on one authorized repo | **Yes** | \`run.state = COMMITTED\` |
| 6 | Retrieval returns provenance-complete results | **Yes** | All Section 7 fields present, 100% |
| 7 | Hybrid search returns both lexical and vector hits | **Yes** | Non-zero contribution from each retriever |
| 8 | MCP surface exposes exactly three read-only tools | **Yes** | Tool enumeration matches allowlist |

### 12.1 Version Pinning

- **DP1.** The vector extension version MUST be pinned in the deployment
  manifest and asserted at startup. If the pinned version provides iterative
  index scans, the system SHOULD use them for filtered queries.
- **DP2.** Version assertions MUST fail loudly. Silent capability downgrade is
  PROHIBITED.

> **OPEN QUESTION OQ-3** — The exact required extension version MUST be
> determined against the target deployment, not assumed.

---

## 13. Observability, Logging, and SLOs

### 13.1 Required Metrics

| Group | Metrics |
|-------|---------|
| Indexing | runs by terminal state, per-phase duration, chunks skipped, embeddings created, reuse ratio |
| Retrieval | latency by mode, result count, degraded rate, provenance completeness |
| Provider | health, error rate, capability mismatches |
| Data | chunk count, tombstone count, generations active, staleness distribution |

### 13.2 Logging Rules

- **O1.** Logs MUST NOT contain repository content beyond the minimum needed
  for debugging, and MUST NOT contain tokens, credentials, or full private
  file bodies.
- **O2.** Every log line relating to a run MUST include \`run_id\`; every log
  line relating to retrieval MUST include the fusion version and generation.
- **O3.** Provenance fields MUST be logged as structured fields, not
  interpolated into message strings.

### 13.3 SLOs

> These are **targets**, not measured claims.

| SLO | Target |
|-----|--------|
| Retrieval availability | 99.5% monthly |
| Retrieval p95 latency | ≤ 800 ms |
| Provenance completeness | 100% |
| Index freshness (default-branch push → indexed) | ≤ 15 min p95 |
| Tombstone propagation | ≤ 24 h |

> **OPEN QUESTION OQ-4** — No baseline measurements exist. These are
> proposals pending Stage B.

---

## 14. Privacy, Retention, and Deletion

- **R1.** Retention MUST be configurable per trust tier and per visibility.
- **R2.** Private repository content MUST be subject to a shorter default
  retention than public content.
- **R3.** Content removed from GitHub MUST be tombstoned within the tombstone
  SLO and MUST NOT appear in any retrieval mode thereafter.
- **R4.** Embeddings derived from deleted content MUST be deleted or
  tombstoned with the chunk. Orphaned embeddings MUST NOT remain queryable.
- **R5.** Deletion MUST be verifiable: the system MUST provide a way to
  confirm that a given \`content_hash\` is no longer retrievable.
- **R6.** A full purge of the derived layer MUST be possible without touching
  GitHub. Rebuild from GitHub MUST restore an equivalent index.

### 14.1 Tombstone Semantics

Tombstoning MUST:

1. Retain the identifier and provenance for audit.
2. Remove the content text and any embeddings from all query paths.
3. Record \`tombstoned_at\`, \`reason\`, and the authorizing actor.

> **OPEN QUESTION OQ-5** — No retention durations are specified in the source
> material. They MUST be set explicitly before any private content is indexed.

---

## 15. Approval-Phase Acceptance Checklist

This spec is approvable only when every box is checked. **Implementation MUST
NOT begin before approval.**

**Scope and authority**

- [ ] Scope and non-goals accepted (Section 1)
- [ ] Source-of-truth hierarchy and conflict rules accepted (Section 2)
- [ ] Identifier scheme accepted (Section 3)

**Contracts**

- [ ] \`DataPlaneProvider\` interface accepted (Section 4)
- [ ] Embedding generation rules accepted (Section 3.4)
- [ ] Provenance envelope accepted as mandatory and non-optional (Section 7)
- [ ] MCP tool allowlist accepted as exactly three read-only tools (Section 11)

**Behavior**

- [ ] Indexing state machine and failure table accepted (Section 6)
- [ ] Hybrid fusion function and determinism rules accepted (Section 8)
- [ ] Current / as-of / at-commit semantics accepted (Section 9)
- [ ] Trust tiers and data-not-instructions rules accepted (Section 10)

**Operations**

- [ ] Deployment gates accepted as blocking (Section 12)
- [ ] SLO targets accepted as targets (Section 13)
- [ ] Retention and deletion rules accepted (Section 14)

**Open questions resolved**

- [ ] OQ-1 tenancy model
- [ ] OQ-2 evaluation corpus
- [ ] OQ-3 pinned extension version
- [ ] OQ-4 SLO baselines
- [ ] OQ-5 retention durations
- [ ] OQ-6 table naming reconciliation

**Explicitly deferred**

- [ ] Confirmed: no Knowledge-Fabric-specific implementation, migration,
      index job, retrieval endpoint, or MCP tool is enabled

---

## 16. Open Questions

| ID | Question | Blocks |
|----|----------|--------|
| OQ-1 | Is Gateway single-tenant or multi-tenant? Determines whether per-document ACLs are needed. | §5.2, §11 |
| OQ-2 | What is the evaluation corpus for hybrid search quality? | §8.3 |
| OQ-3 | Which exact vector extension version will the target deployment provide? | §12.1 |
| OQ-4 | What are the baseline latency and freshness measurements? | §13.3 |
| OQ-5 | What retention durations apply per tier and visibility? | §14 |
| OQ-6 | Does the existing implementation use logical entity names different from the normative 0.2.0 names, and should physical table names follow the logical names? | §3.3, §12 |

---

## Appendix A — Change Log

| # | Change | Type | Section |
|---|--------|------|---------|
| 1 | Added formal scope, non-goals, and authority statement | ADD | 1 |
| 2 | Added source-of-truth hierarchy table and conflict rules C1–C5 | ADD | 2 |
| 3 | Added identifier rules ID1–ID4 and canonical identifier forms | ADD | 3.1–3.2 |
| 4 | Added entity table with required attributes | ADD | 3.3 |
| 5 | Added embedding generation rules G1–G4 | ADD | 3.4 |
| 6 | Added provider-agnostic interface and conformance rules P1–P5 | ADD | 4 |
| 7 | Added discovery rules D1–D4 and authorization boundaries A1–A4 | ADD | 5 |
| 8 | Added indexing state machine, transition rules S1–S5, incremental rules I1–I4 | ADD | 6.1–6.3 |
| 9 | Added failure classification table and rules F1–F2 | ADD | 6.4 |
| 10 | Added enumerated degraded modes | ADD | 6.5 |
| 11 | Elevated provenance to a mandatory envelope with field table and PV1–PV3 | MODIFY | 7 |
| 12 | Added hybrid fusion formula, filter placement, tie-breaking H1–H5 | ADD | 8.1–8.2 |
| 13 | Added evaluation metrics and OQ-2 | ADD | 8.3 |
| 14 | Added current / as-of / at-commit semantics and rules T1–T4 | ADD | 9 |
| 15 | Added trust tier table and TR1–TR3 | ADD | 10.1 |
| 16 | Added data-not-instructions rules TR4–TR8 | ADD | 10.2 |
| 17 | Formalized MCP allowlist, prohibited operations, response requirements | MODIFY | 11.1–11.3 |
| 18 | Added credential separation M3–M5 | ADD | 11.4 |
| 19 | Replaced implicit dependency with blocking gate table (Gate 0–8) | MODIFY | 12 |
| 20 | Added version pinning rules DP1–DP2 | ADD | 12.1 |
| 21 | Added metrics, logging rules O1–O3, SLO table | ADD | 13 |
| 22 | Added privacy / retention rules R1–R6 and tombstone semantics | ADD | 14 |
| 23 | Added approval-phase acceptance checklist | ADD | 15 |
| 24 | Consolidated six open questions | ADD | 16 |
| 25 | Reconciled source-dependent claims against the existing 0.1.0 file and current Gateway architecture | CORRECT | 0, 3, 11, 12, 16 |
| 26 | Added document control and change protocol | ADD | 0 |

---

## Appendix B — Machine-Applicable Change Set

\`\`\`yaml
task: revise_specification
target: docs/specs/2026-09-22-github-knowledge-fabric-design.md
mode: edit_in_place
preserve: existing_valid_content
forbidden:
  - invent_repository_content
  - claim_implementation_complete
  - introduce_write_capable_mcp_tools
  - assert_unverified_versions_or_physical_table_names

preconditions:
  - read_full_source_file
  - reconcile_source_dependent_claims_against_current_repository
  - report_any_section_not_understood_as_OPEN_QUESTION

operations:

  - op: add_section
    id: "0"
    title: Document Control
    must_include: [rfc2119_statement, status_table, change_protocol]

  - op: add_section
    id: "1"
    title: Scope and Non-Goals
    must_include: [in_scope_list, non_goals_table_NG1_NG9, authority_statement]
    language: rfc2119

  - op: add_section
    id: "2"
    title: Source-of-Truth Hierarchy and Conflict Resolution
    must_include: [ranked_table_0_to_5, rules_C1_C5]
    rule: github_is_absolute__derived_layer_is_authoritative_for_nothing

  - op: add_section
    id: "3"
    title: Canonical Data Model and Stable Identifiers
    must_include:
      - rules_ID1_ID4
      - identifier_forms_block
      - entity_table
      - embedding_generation_rules_G1_G4
    constraints:
      - repo_identity_uses_numeric_id_not_owner_name
      - determinism_required
      - no_cross_generation_vector_comparison
      - physical_table_names_not_fixed_before_schema_freeze

  - op: add_section
    id: "4"
    title: Provider-Agnostic Vector Storage Contract
    must_include: [protocol_interface, capabilities_method, conformance_rules_P1_P5]
    constraints:
      - no_provider_specific_sql_outside_provider
      - null_provider_must_exist
      - interface_is_language_neutral

  - op: add_section
    id: "5"
    title: Repository Discovery and Authorization Policy
    must_include: [rules_D1_D4, boundaries_A1_A4]
    constraints:
      - allowlist_only
      - no_write_scoped_tokens
      - visibility_enforced_at_query_time
    mark_open_question: OQ-1

  - op: add_section
    id: "6"
    title: Incremental Indexing State Machine and Failure Handling
    must_include:
      - state_enum
      - transition_rules_S1_S5
      - incremental_rules_I1_I4
      - failure_table
      - rules_F1_F2
      - degraded_mode_enum
    constraints:
      - partial_is_first_class
      - resumable_and_idempotent
      - content_hash_gates_re_embedding

  - op: replace_section
    id: "7"
    title: Provenance Contract
    change: elevate_from_mention_to_mandatory_envelope
    must_include: [required_field_table, rules_PV1_PV3]
    constraint: missing_field_is_error_not_partial_success

  - op: add_section
    id: "8"
    title: Hybrid Search Ranking and Evaluation
    must_include:
      - fusion_formula_with_k_and_weights
      - determinism_rules_H1_H2_H3
      - filter_placement_rules_H4_H5
      - metrics_table
    mark_open_question: OQ-2

  - op: add_section
    id: "9"
    title: Current vs Historical Retrieval
    must_include: [mode_table, rules_T1_T4]
    constraint: no_silent_fallback__explicit_unavailable_markers

  - op: add_section
    id: "10"
    title: Trust Filtering and Instruction Containment
    must_include:
      - trust_tier_table
      - rules_TR1_TR3
      - data_not_instructions_rules_TR4_TR8
    constraint: content_is_data_never_instructions

  - op: replace_section
    id: "11"
    title: Gateway and MCP Contract
    change: formalize_allowlist__add_prohibitions__add_credentials
    must_include:
      - exactly_three_knowledge_fabric_tools
      - rules_M1_M2
      - prohibited_operations_block
      - response_requirements
      - credential_separation_M3_M5
    constraint: allowlist_based_registration_not_denylist

  - op: replace_section
    id: "12"
    title: Deployment Dependency Gates
    change: convert_implicit_dependency_to_blocking_gate_table
    must_include: [gate_table_0_through_8, version_pinning_rules_DP1_DP2]
    statement: no_supabase_project_connected_is_a_hard_dependency_for_persistent_vector_search
    mark_open_question: OQ-3

  - op: add_section
    id: "13"
    title: Observability, Logging, and SLOs
    must_include: [metric_groups, logging_rules_O1_O3, slo_table]
    constraint: slos_are_targets_not_claims
    mark_open_question: OQ-4

  - op: add_section
    id: "14"
    title: Privacy, Retention, and Deletion
    must_include: [rules_R1_R6, tombstone_semantics]
    mark_open_question: OQ-5

  - op: add_section
    id: "15"
    title: Approval-Phase Acceptance Checklist
    must_include:
      - checklist_grouped_by_scope_contracts_behavior_operations
      - explicit_deferral_confirmation
    gate: implementation_must_not_begin_until_approval

  - op: add_section
    id: "16"
    title: Open Questions
    must_include: [OQ-1_through_OQ-6_table]

  - op: correct
    target: prior_draft
    actions:
      - reconcile_source_dependent_claims
      - remove_or_soften_unverified_version_claims
      - avoid_confusing_existing_general_MCP_with_Knowledge_Fabric_MCP

  - op: append_appendix
    id: "A"
    title: Change Log
    format: table_with_change_type_and_section

  - op: append_appendix
    id: "B"
    title: Machine-Applicable Change Set
    format: yaml_block

acceptance_criteria:
  - every_change_list_item_mapped_to_a_section
  - every_unresolved_item_marked_OPEN_QUESTION
  - no_implementation_claimed
  - no_write_tool_introduced
  - all_retrieved_content_treated_as_data
  - current_general_Gateway_MCP_not_misreported_as_Knowledge_Fabric_implementation
  - document_is_self_consistent_and_implementation_ready_after_open_questions_are_resolved
\`\`\`

---

## Appendix C — Glossary

| Term | Definition |
|------|------------|
| Derived layer | Rebuildable storage (documents, chunks, embeddings); never authoritative |
| Generation | A versioned embedding configuration (provider + model + dim + normalization) |
| Provenance envelope | Mandatory metadata block accompanying every retrieved chunk |
| Tombstone | Audit-preserving deletion that removes content from all query paths |
| Degraded mode | Explicitly labeled reduction in retrieval capability |
| Data-not-instructions | Doctrine that retrieved content is inert data, never directives |
| Gate | A blocking deployment prerequisite |
| Partial index | Index state where some content is committed and some is not |
