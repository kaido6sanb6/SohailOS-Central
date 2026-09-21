# Ecosystem Control Plane Implementation Plan

> **For agentic workers:** Use the host's available task-by-task implementation workflow. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `SohailOS-Central` the machine-readable control plane for all 46 owner-scoped repositories, with explicit capabilities, safe relationships, routing rules, and verification state.

**Architecture:** A single JSON ecosystem manifest is the source of truth for repository identity, capabilities, edges, and routing. The manifest is deliberately non-invasive: it links repositories by metadata and documented relationships rather than physically merging code, and it refuses automatic routing for entries whose capability evidence is insufficient or whose role is infrastructure/networking. Existing .NET runtime code remains unchanged in this first integration slice; the registry is consumed by tooling and future orchestrator work.

**Tech Stack:** JSON, .NET 10/xUnit for manifest validation, GitHub repository metadata, Markdown documentation, GitHub Actions.

## Global Constraints

- `SohailOS-Central` remains the code/specification source of truth.
- Do not merge the 46 repositories into a monolith.
- Do not invent fork/upstream provenance. Record it as unverified when the normalized GitHub connector does not expose parent/source metadata.
- Only relationships supported by repository evidence or clear capability composition are marked active; uncertain relationships are advisory.
- Network/proxy repositories and an empty repository are registry-visible but never auto-routed as application components.
- No credentials, tokens, secrets, or private runtime configuration may be stored in the manifest.
- The registry must cover all repositories visible in the authenticated owner inventory at implementation time.

---

### Task 1: Establish the failing ecosystem-manifest contract

**Files:**
- Create: `ecosystem/ecosystem.json` — source-of-truth manifest.
- Create: `tests/SohailOS.Tests/EcosystemRegistryDocumentTests.cs` — manifest contract tests.

**Interfaces:**
- Consumes: JSON document at `ecosystem/ecosystem.json`.
- Produces: deterministic validation of repository count/identity, graph integrity, routing safety, and central-control-plane invariants.

- [ ] **Step 1: Add the focused failing test**

Test the following externally visible invariants:
1. The manifest exists at `ecosystem/ecosystem.json`.
2. It declares `schema_version`, `control_plane`, `repositories`, `edges`, and `routing`.
3. Exactly 46 unique repository names are registered, including `kaido6sanb6/SohailOS-Central`.
4. Every edge references registered repository IDs.
5. `control_plane` equals `kaido6sanb6/SohailOS-Central`.
6. No repository marked `auto_route=true` has `verification_status=unverified`.
7. Routing rules reference only registered repositories.
8. `dddeu83`, `argo-pass`, and `V2ray-for-Doprax` are registry-visible but have `auto_route=false`.

- [ ] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~EcosystemRegistryDocumentTests`

Expected: non-zero exit because `ecosystem/ecosystem.json` does not exist yet.

- [ ] **Step 3: Implement the minimum behavior**

Add only the machine-readable contract test described above. Keep path resolution deterministic from the test output directory.

- [ ] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~EcosystemRegistryDocumentTests`

Expected: the test reaches manifest assertions after the manifest is added in Task 2.

---

### Task 2: Add the complete 46-repository ecosystem manifest

**Files:**
- Create: `ecosystem/ecosystem.json`.
- Modify: `tests/SohailOS.Tests/SohailOS.Tests.csproj` only to copy the manifest to test output.

**Interfaces:**
- Consumes: authenticated GitHub owner inventory and inspected repository README evidence.
- Produces: repository registry, capability taxonomy, graph edges, routing rules, and explicit provenance verification states.

- [ ] **Step 1: Add or update the focused failing test**

Use the Task 1 test unchanged; the red state is the missing manifest.

- [ ] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~EcosystemRegistryDocumentTests`

Expected: manifest-not-found failure.

- [ ] **Step 3: Implement the minimum behavior**

Create a single source-of-truth JSON document containing:
- All 46 `kaido6sanb6/*` repositories visible from the owner inventory.
- Default branch for every entry.
- A conservative role and capability list.
- Evidence URLs/status for inspected repositories.
- Explicit `auto_route` safety flags.
- Active edges for capability composition (control plane, provider routing, agent runtimes, UI clients, local model serving, data/context, knowledge/skills, research/learning).
- Advisory edges where integration is plausible but not verified.
- Routing policies that select repositories by outcome class.
- Provenance records with `upstream=null` and `status=unverified` where GitHub normalized metadata did not return parent/source.

Copy the manifest into the test output using standard MSBuild content metadata; do not add third-party JSON packages.

- [ ] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~EcosystemRegistryDocumentTests`

Expected: all manifest contract tests pass.

- [ ] **Step 5: Run the affected integration check**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj`

Expected: all existing routing/agent tests plus manifest tests pass.

---

### Task 3: Make the registry discoverable and govern future routing

**Files:**
- Create: `ecosystem/README.md` — operator-facing contract and usage.
- Create: `docs/plans/2026-09-22-sohailos-ecosystem-control-plane.md` — implementation record.
- Modify: `system/TOOL_ROUTING.md` — route code/project decisions through the ecosystem registry before selecting repositories.

**Interfaces:**
- Consumes: `ecosystem/ecosystem.json`.
- Produces: human-readable governance rules for future assistant/runtime use.

- [ ] **Step 1: Add the focused test**

Extend the manifest test to assert that `ecosystem/README.md` exists only as documentation and that routing remains manifest-driven; no duplicate registry data is stored in the README.

- [ ] **Step 2: Verify the relevant failure**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~EcosystemRegistryDocumentTests`

Expected: the new documentation assertion fails before the file exists.

- [ ] **Step 3: Implement the minimum behavior**

Document the workflow:
`request -> capability -> registry -> minimum repository set -> execution -> verification`.
State that GitHub metadata is authoritative for repository identity, repository READMEs are evidence for capabilities, and unverified provenance must remain unverified until independently confirmed.

Update `system/TOOL_ROUTING.md` with one non-duplicative rule: for code/project requests, consult the ecosystem registry first, then use only the minimum applicable repositories.

- [ ] **Step 4: Verify the focused pass**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj --filter FullyQualifiedName~EcosystemRegistryDocumentTests`

Expected: all manifest/document assertions pass.

- [ ] **Step 5: Run the full verification**

Run: `dotnet test tests/SohailOS.Tests/SohailOS.Tests.csproj`

Expected: zero test failures.

Run: `dotnet build SohailOS.sln --configuration Release`

Expected: exit code 0 with no compilation errors.

---

## Unresolved externally observable decisions

- Whether the registry should later drive live repository selection inside the .NET orchestrator is intentionally deferred; this change establishes the stable data contract first.
- Upstream/fork parent URLs remain unverified until the GitHub integration exposes reliable parent/source metadata or they are independently verified repository-by-repository.
