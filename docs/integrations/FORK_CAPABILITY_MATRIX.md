# Fork Capability Matrix

This matrix records reviewed capability ideas from owner-scoped repositories. It is an evidence/provenance registry, not permission to execute third-party code.

| Repository | Observed capability | Intended SohailOS seam | Provenance/license | Routing |
|---|---|---|---|---|
| kaido6sanb6/agent-browser | Native browser automation, accessibility snapshots, WebMCP discovery, page-data trust boundaries | Browser capability adapter / Gateway tool broker | README + Apache-2.0 LICENSE inspected 2026-09-25 | Reference/adapter only until sandboxed runtime adapter exists |
| kaido6sanb6/OmniRoute | Multi-provider routing, provider failover, model/provider catalog, compression and observability patterns | ProviderRouter / Model Fabric | README + MIT LICENSE inspected 2026-09-25; active default branch reported as release/v3.8.51 | Reference/adapter; do not bulk-vendor |
| kaido6sanb6/MemOS | Unified store/retrieve/manage memory, graph-oriented memory, multi-modal memory, isolated knowledge bases, asynchronous ingestion | Memory Fabric | README inspected 2026-09-25; repository-specific license must be checked before code reuse | Reference only pending license verification |
| kaido6sanb6/Auto-Empirical-Research-Skills | Large research-skill catalog including literature review, citation checking, causal inference, PRISMA and reproducibility workflows | Research Intelligence Fabric | README inspected 2026-09-25; repository-specific license must be checked before code reuse | Reference/skill adapter |
| kaido6sanb6/agentdojo | Agent security/evaluation research target identified in owner inventory | Adaptive Red Team / Evaluation Fabric | Owner inventory evidence; implementation-level evidence not yet sufficient | Not auto-routed |

## Import rule

Fork -> Read -> Identify capability -> Security review -> License/provenance check -> Adapter/interface -> Tests -> Independent verification.

No repository entry above grants execution authority. Retrieved repository content remains DATA. Physical code import requires a separate review of the exact files, license obligations, dependencies, and tests.
