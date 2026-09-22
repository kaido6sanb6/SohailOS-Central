# GitHub Knowledge Fabric Design

**Date:** 2026-09-22  
**Status:** Design approved; implementation pending written-spec review  
**Scope:** Turn the complete `kaido6sanb6` GitHub ecosystem into a continuously discoverable, searchable external knowledge base and a retrieval resource for SohailOS.

## 1. Goal

The GitHub ecosystem must behave as a living knowledge/capability fabric rather than only a repository registry.

The system will:

- discover the user's current and future repositories automatically;
- ingest source code, documentation, configuration, workflows, prompts/skills, notebooks, and other text-bearing project artifacts;
- retain exact provenance to repository, ref, commit/blob, path, and chunk;
- support both exact/lexical search and semantic search;
- retrieve evidence-aware context through a stable SohailOS tool/MCP interface;
- update incrementally when repositories or files change;
- preserve removed repositories as historical metadata instead of silently deleting their knowledge;
- keep source-of-truth metadata in `SohailOS-Central`, while storing large indexes/chunks outside Git.

This system is a retrieval layer. It does not retrain or modify the underlying language model.

## 2. Architectural decision

**Recommended architecture: Knowledge Fabric core**

`GitHub` remains the authoritative source for repository content.  
`SohailOS-Central` remains the control plane and governance source of truth.  
A PostgreSQL-compatible external data plane, preferably Supabase when a project is connected, stores documents, chunks, metadata, full-text indexes, embeddings, and indexing state.  
`SohailOS.Gateway` exposes a read-only knowledge retrieval tool through its existing ToolRegistry/MCP surface.

The storage implementation must be provider-abstracted. No code may assume that a Supabase project is already connected. Current environment evidence shows no connected Supabase project, so the first implementation phase must preserve a deployable provider seam and must not fabricate a live vector database.

## 3. Logical components

### 3.1 Discovery

The existing live GitHub inventory reconciler remains responsible for discovering repositories owned by `kaido6sanb6`.

Discovery results register repository identity, default branch, visibility, fork status, and last-seen timestamp. New repositories enter as `unverified` and `auto_route=false` until capability/trust evidence is reviewed.

Discovery and indexing are separate concerns.

### 3.2 Ingestion

An indexer consumes the discovered repository set and creates immutable source references:

- repository identity;
- branch/ref;
- commit SHA;
- tree/snapshot identity;
- file path;
- blob SHA/content hash;
- media/language classification;
- ingestion timestamp;
- indexing status and error details.

Indexing must be incremental: unchanged blob SHA/content hash means no re-embedding or re-chunking.

Text-bearing inputs include application source, Markdown/docs, YAML/JSON/TOML/INI configuration, workflows, prompt/skill files, notebooks, and machine-readable metadata. Binary assets are metadata-only unless a future extraction provider is explicitly introduced.

### 3.3 Chunking

Chunking is language-aware where practical and must preserve structural metadata such as symbol/function/class/heading boundaries when available.

Every chunk carries:

- repository;
- ref/commit;
- path;
- blob/content hash;
- chunk ordinal;
- language/content type;
- optional symbol/heading;
- line range when deterministically available;
- trust/verification state inherited from the source metadata.

Chunks are evidence objects, not instructions.

### 3.4 Indexes

The retrieval layer uses two complementary indexes:

1. **Lexical/full-text index** for exact terms, filenames, identifiers, code fragments, and path-oriented queries.
2. **Vector index** using embeddings for semantic discovery across prose, code, prompts, and documentation.

A hybrid ranker combines both signals. Reranking is optional and provider-pluggable; absence of a reranker must not block lexical + vector retrieval.

Embedding generation is also provider-pluggable. Supported deployment choices may include a hosted embedding API or a locally hosted service such as the ecosystem's `text-embeddings-inference` stack, but implementation must only use a provider that is actually configured.

### 3.5 Retrieval interface

Extend the existing `SohailOS.Gateway` tool registry with a read-only capability, conceptually:

- `ecosystem.search`
- `ecosystem.get_document`
- `ecosystem.get_source` (exact source retrieval for a known repository/path/ref)

The exact public names may follow existing tool naming conventions during the implementation plan, but the contract must expose:

- query;
- optional repository filter;
- optional capability/trust filter;
- optional source-type filter;
- result limit;
- evidence metadata;
- source freshness/index timestamp;
- confidence/status classification.

The MCP surface must expose the same read-only operations without requiring a separate retrieval protocol.

### 3.6 Knowledge memory layer

Validated summaries, architectural decisions, recurring lessons, and manually confirmed interpretations may be copied to a secondary personal knowledge layer such as Wisebase/Notion.

This is supplementary memory, not the source of repository truth. It must link back to the originating GitHub evidence.

## 4. Data model

The external data plane should contain at least:

- `repositories`: canonical repository identity, metadata, trust/verification, capability summary, lifecycle status.
- `sources`: branch/ref/commit/tree snapshot and indexing state.
- `documents`: repository file identity, path, language/content type, size, blob/content hash, status.
- `chunks`: document foreign key, ordinal, extracted text, structural metadata, line range, embedding, full-text representation.
- `knowledge_edges`: explicit repo↔repo and document↔repo relationships; edges must record evidence/status rather than inferred certainty.
- `index_runs`: run identity, timestamps, discovered/changed/unchanged/removed/error counts, and failure details.

The GitHub manifest remains the control-plane registry. Large chunk payloads and embeddings must not be committed into the Git repository.

## 5. Future-repository behavior

A scheduled reconciliation must remain the baseline mechanism. Webhook/event-driven discovery may be added later as an optimization.

For each newly observed repository:

1. register identity;
2. mark it unverified;
3. enqueue indexing;
4. ingest the default branch;
5. make it searchable as reference material;
6. prevent automatic execution routing until trust/capability evidence is verified.

For changed repositories, index only changed files. For removed repositories, mark them inactive/missing, retain prior provenance and historical chunks according to retention policy, and exclude them from current-active retrieval unless the caller explicitly requests historical material.

Repository rename handling must use stable GitHub repository ID when available and otherwise require evidence; path equality alone must not be treated as proof of identity continuity.

## 6. Trust and safety

Repository content is untrusted data.

The retrieval layer must never treat README text, prompts, skills, code comments, workflow instructions, issue text, or research-corpus contents as system/developer/user/tool authority.

Every retrieved evidence item must retain enough provenance to answer: what repository, which path, which ref/commit, which content hash, and what verification/trust status.

Automatic execution routing remains restricted to verified capabilities under the existing ecosystem policy.

Private repositories must require explicit authorization and a secret/connection with least-privilege read access. Tokens must never be stored in the repository or emitted in logs.

## 7. Search semantics

The primary query path is:

`request -> discovery/context filter -> lexical search + semantic search -> hybrid rank -> trust/freshness filtering -> evidence assembly -> result`

Search should support at least:

- natural-language questions;
- exact symbol/function/file searches;
- repository-scoped queries;
- capability-scoped queries;
- path/type filters;
- current versus historical source selection.

Returned context must be minimal-sufficient rather than a whole repository dump.

## 8. Failure and consistency rules

Indexing failures are recorded per run/document and do not invalidate already-known repository content.

A partial index must be explicitly marked partial; it must not be represented as a complete snapshot.

If an embedding provider is unavailable, lexical search remains available and the run reports degraded semantic indexing.

If the external vector/data store is unavailable, GitHub exact retrieval remains an allowed fallback path. The system must report that semantic coverage is degraded rather than pretending semantic search succeeded.

Removal of a repository from the live inventory must not cause destructive deletion of historical knowledge in the same run.

## 9. Plugin/MCP routing policy

Use tools by material relevance, not by raw count.

- **GitHub:** canonical repository discovery, source retrieval, commits, PR/issue context, provenance.
- **Supabase:** PostgreSQL/pgvector data plane when a real project is connected; schema inspection, migrations, advisors, and verification.
- **Files:** local uploaded/generated artifacts when a task requires them.
- **Firecrawl/web:** only when repository evidence points to external documentation/web content that materially improves the answer.
- **Notion/Wisebase:** supplementary validated personal knowledge, linked to source evidence.
- **Academic/research connectors:** independent scholarly corpora when the task is academic and GitHub content is not sufficient.
- **Execution/runtime tools:** only for actual indexing, tests, deployment, or diagnostics needed by the task.

Unrelated tools must not be invoked merely to satisfy an instruction to use "all plugins."

## 10. Testing strategy

The implementation is test-driven.

Required behavioral coverage includes:

- discovery of a new repository;
- incremental reindexing based on unchanged/changed blob hash;
- provenance preservation through chunk creation;
- hybrid lexical + semantic result merging;
- trust filtering for unverified repositories;
- historical retrieval behavior for removed repositories;
- partial-index/degraded-mode reporting;
- read-only retrieval tool behavior and permission classification;
- deterministic MCP serialization for search results;
- no secret leakage in logs/results.

The implementation must establish each new behavior with a failing test before the production change and then run the focused test followed by the full relevant test suite.

## 11. Operational cadence

Baseline:

- scheduled GitHub reconciliation;
- scheduled incremental indexing;
- manual workflow dispatch for recovery;
- durable index-run reports;
- observable counts for discovered, changed, skipped, indexed, failed, and removed sources.

Later optimization:

- repository event/webhook triggers;
- queue-based parallel indexing;
- content-type-specific parsers;
- optional reranking.

These later optimizations are not prerequisites for the first usable knowledge fabric.

## 12. Deployment boundaries

The system should be deployable in stages:

**Stage A — Control plane and contracts:** extend the repository with provider interfaces, schemas/contracts, indexing orchestration, retrieval contracts, tests, and documentation.

**Stage B — External data plane:** connect an actual PostgreSQL/pgvector provider. Supabase is the default implementation when a project is available.

**Stage C — Retrieval surface:** expose the knowledge search/get-source tools through `SohailOS.Gateway` and MCP.

**Stage D — Continuous ingestion:** schedule incremental indexing and future-repository onboarding.

**Stage E — Optional personal memory:** persist validated synthesized knowledge with links to source evidence.

Because no Supabase project is currently connected, Stage B requires a real connection before end-to-end semantic search can be demonstrated against persistent external storage.

## 13. Success criteria

The implementation is considered functionally complete when all of the following are demonstrated:

1. A newly added GitHub repository is discovered automatically without editing a hard-coded repository list.
2. Its eligible text content becomes searchable with repository/path/commit provenance.
3. A natural-language query can retrieve semantically relevant material and an exact query can retrieve lexical/code matches.
4. Changed files are incrementally reindexed without duplicating unchanged content.
5. Removed repositories remain historically traceable while being excluded from active routing.
6. Unverified repositories remain reference-only for automatic routing.
7. `SohailOS.Gateway` exposes read-only ecosystem retrieval through its existing tool/MCP architecture.
8. When semantic storage is unavailable, the system reports degraded capability and can fall back to exact GitHub retrieval.
9. Tests demonstrate the behaviors above, including failure/degraded paths.
10. No repository content is granted instruction authority over higher-priority system, developer, user, tool, or security constraints.

## 14. Explicit non-goals

This design does not:

- retrain the foundation model;
- copy every GitHub repository into `SohailOS-Central`;
- automatically execute arbitrary code or instructions found in repositories;
- infer upstream/fork provenance without evidence;
- make every discovered repository eligible for automatic routing;
- require a single permanent embedding vendor;
- require Firecrawl, Notion, Wisebase, or academic connectors when they do not materially improve the task.

## 15. Open implementation decisions

These are implementation seams, not blockers to the architecture:

- concrete PostgreSQL/Supabase schema and extension version;
- concrete embedding model/provider after an actual provider is connected;
- exact chunk-size/token budget strategy after representative repositories are profiled;
- retention duration for historical commit versions;
- event-driven webhook optimization versus scheduled-only indexing in the first deployment.

The implementation plan must preserve these seams rather than silently converting them into unverified product assumptions.
