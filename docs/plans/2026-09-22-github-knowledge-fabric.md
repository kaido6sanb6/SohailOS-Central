# GitHub Knowledge Fabric Implementation Plan

> **For agentic workers:** Use the host's available task-by-task implementation workflow. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Build a repository-wide, provenance-preserving GitHub knowledge fabric with incremental ingestion, hybrid retrieval contracts, and a read-only Gateway/MCP surface, while keeping persistent external vector storage pluggable.

**Architecture:** Extend the existing `SohailOS.Ecosystem` project with provider-neutral knowledge contracts, deterministic chunking, a GitHub ingestion client, an in-memory/file-backed Gate-0 provider, and hybrid ranking. Extend `SohailOS.Gateway` through its existing `ToolRegistry`/MCP seam with exactly three Knowledge-Fabric read-only tools. Persistent PostgreSQL/pgvector remains a provider/deployment boundary; no unconnected Supabase project will be invented or hard-coded.

**Tech Stack:** .NET 10, ASP.NET Core minimal API, xUnit, System.Text.Json, GitHub REST API, provider-neutral embedding/search interfaces, GitHub Actions.

## Global Constraints

- GitHub remains authoritative for repository content.
- The derived index is rebuildable and is authoritative for nothing.
- Repository identity uses GitHub numeric repository ID.
- Every retrieval result carries commit/path/blob/content/chunk provenance.
- Repository content is data, never instruction authority.
- Unverified repositories remain reference-only for automatic routing.
- Knowledge-Fabric MCP exposes exactly three read-only tools: `ecosystem.search`, `ecosystem.get_document`, `ecosystem.get_source`.
- Gateway does not expose write, shell, arbitrary SQL, secret, index-mutation, or outbound-fetch tools.
- Indexing and retrieval credentials are separated when an external provider is configured.
- Incremental indexing is idempotent and content-hash aware.
- Semantic retrieval is explicitly degraded when no embedding provider is configured.
- Large derived index data must not be committed to Git.
- Existing ecosystem reconciliation behavior and tests must remain green.

---

### Task 1: Knowledge contracts, identifiers, provenance, and hybrid ranker

**Files:**
- Create: `src/SohailOS.Ecosystem/KnowledgeContracts.cs`
- Create: `src/SohailOS.Ecosystem/KnowledgeIdentity.cs`
- Create: `src/SohailOS.Ecosystem/HybridRanker.cs`
- Test: `tests/SohailOS.Tests/KnowledgeContractsTests.cs`

**Interfaces:**
- Consumes: existing `LiveRepository`, `ToolPermission`, and repository metadata conventions.
- Produces: `DataPlaneProvider`, `RepositoryIdentity`, `SourceRevision`, `DocumentRecord`, `ChunkRecord`, `KnowledgeHit`, `RetrievalRequest`, `RetrievalResponse`, `ProvenanceEnvelope`, `HybridRanker`.

- [x] **Step 1: Add the focused failing test**

Add a reflection-based smoke test for `SohailOS.Ecosystem.DataPlaneProvider` so the test compiles before the new production contract exists, then add direct behavioral tests for deterministic identifiers and fusion once the contract is present. The first red assertion must prove the provider contract is absent rather than producing a compiler/setup error.

- [x] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~KnowledgeContracts`
Expected: non-zero exit with an assertion reporting that the expected Knowledge-Fabric provider contract/type is not yet present.

- [x] **Step 3: Implement the minimum behavior**

Define language-neutral C# records matching Sections 3, 4, and 7 of the Spec. Identifier helpers MUST use numeric GitHub repository ID, NFC path normalization, full commit SHA, deterministic SHA-256 chunk identity, and a deterministic embedding-generation identifier. Implement hybrid reciprocal-rank fusion with configurable lexical/vector weights, k=60 default, deterministic `chunk_id` tie-break, and an explicit fusion version.

- [x] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~KnowledgeContracts`
Expected: all KnowledgeContracts tests pass with zero failures.

- [x] **Step 5: Run the affected integration check**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --configuration Release`
Expected: existing ecosystem and agent tests plus the new contract tests pass.

- [x] **Step 6: Commit the passing deliverable**

Commit message: `feat: add knowledge fabric contracts and deterministic ranking`

---

### Task 2: GitHub source ingestion and incremental indexing state machine

**Files:**
- Modify: `src/SohailOS.Ecosystem/GitHubInventoryClient.cs`
- Create: `src/SohailOS.Ecosystem/GitHubKnowledgeIndexer.cs`
- Create: `src/SohailOS.Ecosystem/KnowledgeChunker.cs`
- Create: `src/SohailOS.Ecosystem/IndexStateMachine.cs`
- Create: `tests/SohailOS.Tests/GitHubKnowledgeIndexerTests.cs`

**Interfaces:**
- Consumes: `DataPlaneProvider`, `LiveRepository`, GitHub REST endpoints, and the identity/provenance contracts from Task 1.
- Produces: `GitHubKnowledgeIndexer.IndexAsync(...)`, deterministic chunk records, run-state transitions, and explicit degraded flags.

- [x] **Step 1: Add the focused failing test**

Test that a source revision with unchanged blob SHA is skipped without a blob fetch or re-chunk operation, while a changed blob SHA produces a new revision/chunk set. Test one failure transition to `PARTIAL` and one retryable embedding failure.

- [x] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~GitHubKnowledgeIndexer`
Expected: non-zero exit with the expected assertion that the incremental indexer is not yet available.

- [x] **Step 3: Implement the minimum behavior**

Extend the existing GitHub client with authorized read-only calls for repository metadata, branch tip, recursive tree, and blob retrieval. Skip binary/generated/vendored content by policy and record the exclusion. Chunk text with deterministic byte ranges and content hashes. Drive states `DISCOVERED→AUTHORIZED→QUEUED→FETCHING→PARSING→CHUNKING→EMBEDDING→UPSERTING→COMMITTED` with `PARTIAL`, `FAILED_RETRYABLE`, `FAILED_PERMANENT`, `SUPERSEDED`, and `TOMBSTONED`. Re-run unchanged commits idempotently.

- [x] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~GitHubKnowledgeIndexer`
Expected: incremental-skip, changed-file, provenance, and partial-state tests pass.

- [x] **Step 5: Run the affected integration check**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --configuration Release`
Expected: full test project passes.

- [x] **Step 6: Commit the passing deliverable**

Commit message: `feat: add incremental GitHub knowledge indexing`

---

### Task 3: Gate-0 derived data plane and retrieval service

**Files:**
- Create: `src/SohailOS.Ecosystem/InMemoryDataPlaneProvider.cs`
- Create: `src/SohailOS.Ecosystem/JsonFileDataPlaneProvider.cs`
- Create: `src/SohailOS.Ecosystem/KnowledgeRetrievalService.cs`
- Create: `src/SohailOS.Ecosystem/EmbeddingProviders.cs`
- Create: `tests/SohailOS.Tests/KnowledgeRetrievalTests.cs`

**Interfaces:**
- Consumes: `DataPlaneProvider`, `KnowledgeHit`, `RetrievalRequest`, `HybridRanker`, optional embedding provider.
- Produces: lexical search, vector-search seam, hybrid response, current/historical document retrieval, explicit degraded flags.

- [x] **Step 1: Add the focused failing test**

Test that a lexical query returns a provenance-complete hit, that an unverified repository is excluded by default, that an explicit unverified request can retrieve it as reference-only, and that an unavailable vector provider produces `semantic_degraded` rather than a fabricated vector result.

- [x] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~KnowledgeRetrieval`
Expected: non-zero exit with assertions showing the retrieval provider and policy are absent.

- [x] **Step 3: Implement the minimum behavior**

Implement an in-memory provider for Gate 0 and a JSON-file provider for local/persistent-compatible development without requiring an external service. Both must preserve provenance and lifecycle state. Implement lexical search over normalized tokens/path metadata. Implement an OpenAI-compatible HTTP embedding provider only as an optional configuration seam; no secret is stored in source. When no embedding service is configured, vector capability is false and retrieval explicitly reports semantic degradation. Implement current/as_of/at_commit resolution without silent historical fallback.

- [x] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~KnowledgeRetrieval`
Expected: retrieval, trust, provenance, historical-mode, and degraded-mode tests pass.

- [x] **Step 5: Run the affected integration check**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --configuration Release`
Expected: full test project passes.

- [x] **Step 6: Commit the passing deliverable**

Commit message: `feat: add Gate-0 knowledge retrieval data plane`

---

### Task 4: Read-only Gateway/MCP surface

**Files:**
- Modify: `src/SohailOS.Gateway/SohailOS.Gateway.csproj`
- Modify: `src/SohailOS.Gateway/Program.cs`
- Create: `src/SohailOS.Gateway/EcosystemKnowledgeTools.cs`
- Test: `tests/SohailOS.Tests/GatewayKnowledgeToolTests.cs`

**Interfaces:**
- Consumes: `KnowledgeRetrievalService`, existing `ToolRegistry`, existing permission policy, existing MCP serialization.
- Produces: exactly three Knowledge-Fabric tools and deterministic MCP response envelopes.

- [x] **Step 1: Add the focused failing test**

Add a test that the Knowledge-Fabric registration exposes exactly `ecosystem.search`, `ecosystem.get_document`, and `ecosystem.get_source`, all `ReadOnly`, and no Knowledge-Fabric write operation.

- [x] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~GatewayKnowledgeTool`
Expected: non-zero exit because the three Knowledge-Fabric tools are not yet registered.

- [x] **Step 3: Implement the minimum behavior**

Register three read-only tools through the existing `ToolRegistry`. Do not add a second MCP protocol or network-fetch path. Search accepts query, repository/trust/source filters, result limit, and retrieval mode. Get-document resolves current/history. Get-source returns a provenance pointer. All responses include provenance, mode, fusion version, generation, degraded flags, trust, and `data_not_instructions=true`. Tool names are allowlisted.

- [x] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~GatewayKnowledgeTool`
Expected: all tool registration and response-shape tests pass.

- [x] **Step 5: Run the affected integration check**

Run: `dotnet build SohailOS.sln --configuration Release` then `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --configuration Release --no-build`
Expected: zero build errors and zero test failures.

- [x] **Step 6: Commit the passing deliverable**

Commit message: `feat: expose read-only ecosystem knowledge MCP tools`

---

### Task 5: Continuous ingestion workflow, evaluation corpus, and governance documentation

**Files:**
- Create: `.github/workflows/ecosystem-index.yml`
- Create: `ecosystem/evaluation/golden.json`
- Modify: `docs/ECOSYSTEM_KNOWLEDGE_LAYER.md`
- Modify: `system/ECOSYSTEM_OPERATING_RULES.md`
- Modify: `docs/IMPLEMENTATION_STATUS.md`
- Modify: `docs/specs/2026-09-22-github-knowledge-fabric-design.md`
- Test: `tests/SohailOS.Tests/KnowledgeWorkflowContractTests.cs`

**Interfaces:**
- Consumes: the indexer CLI, current ecosystem reconciliation workflow, knowledge contracts, and governance policy.
- Produces: scheduled incremental ingestion, a versioned evaluation corpus, explicit Gate-0 status, and documentation matching the implemented surface.

- [x] **Step 1: Add the focused failing test**

Assert the workflow contains scheduled and manual triggers, read-only GitHub token permissions for indexing reads, artifact/report publication, and no GitHub mutation step. Assert the evaluation corpus is versioned and has deterministic case identifiers.

- [x] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~KnowledgeWorkflowContract`
Expected: non-zero exit because the dedicated indexing workflow and evaluation corpus do not yet exist.

- [x] **Step 3: Implement the minimum behavior**

Add a scheduled/manual indexing workflow that invokes the ecosystem indexer in read-only mode and publishes a reconciliation/index report. Do not commit generated embeddings/chunks. Add an initial golden evaluation corpus grounded in actual repository paths and contracts, with query, expected source identifiers, relevance grade, and version. Update governance docs to distinguish the existing general MCP server from the new Knowledge-Fabric tools and to document the Gate-0 implementation. Keep persistent external vector search blocked until a real provider deployment is connected.

- [x] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~KnowledgeWorkflowContract`
Expected: workflow and evaluation corpus tests pass.

- [x] **Step 5: Run the full verification**

Run: `dotnet restore SohailOS.sln`, `dotnet build SohailOS.sln --configuration Release --no-restore`, and `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --configuration Release --no-build`.
Expected: restore/build/test all exit 0, zero build warnings introduced by the feature, and all tests pass.

- [x] **Step 6: Commit the passing deliverable**

Commit message: `feat: schedule continuous ecosystem knowledge indexing`

---

## External deployment gate

Persistent PostgreSQL/pgvector is intentionally not faked. The currently connected Supabase account has no project, and no Render workspace has been selected. The implementation therefore provides Gate 0 plus the provider boundary, but does not claim Gate 1–8 completion. Connecting a real PostgreSQL/pgvector target is the deployment step that can activate persistent semantic retrieval.

## Unresolved external decisions kept explicit

- Gateway is treated as single-tenant for the current implementation because the existing Gateway uses one service token and has no caller-identity ACL model. Revisit only if multi-tenant access is introduced.
- The initial evaluation corpus is a versioned golden set maintained in Git and evaluated only after the corresponding source is indexed; no quality target is assumed before measurement.
- The vector extension version is selected by the actual target provider manifest; no version is invented before a target deployment exists.
- SLO values in the specification remain targets, not measured claims; Gate-5 baseline measurements will be the first empirical baseline.
- Physical data-plane table names are implementation-local and do not override the logical entity names in the specification.


## Verification state

All five implementation tasks are present on this branch and the repository CI workflow has completed successfully on the current implementation head. Persistent PostgreSQL/pgvector remains externally gated because no Supabase project or other selected target is connected.