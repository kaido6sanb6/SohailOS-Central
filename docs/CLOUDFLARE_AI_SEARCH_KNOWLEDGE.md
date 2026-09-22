# Managed GitHub Knowledge Search

SohailOS-Central now has a managed external retrieval target for the GitHub Knowledge Fabric.

## Architecture

```text
GitHub owner inventory
        |
        v
.NET knowledge indexer
        |
        +--> provenance-rich export
        |
        v
Cloudflare AI Search
  - vector retrieval
  - BM25 keyword retrieval
  - RRF fusion
  - optional reranking
  - built-in storage
        |
        +--> HTTPS search
        +--> MCP /mcp
```

Cloudflare AI Search is used as a conforming derived retrieval provider rather than as a source of truth. GitHub remains authoritative. Exported documents carry repository, commit, blob, chunk, trust, and pinned-permalink provenance.

The synchronization excludes `unverified` and `tombstoned` repositories from the external public search surface. `authoritative`, `verified`, and `reference_only` content can be exported.

## Automation

`.github/workflows/ecosystem-knowledge-search.yml` runs:

1. live GitHub repository discovery;
2. incremental Gate-0 indexing;
3. provenance-rich export with a 3.5 MB per-item limit;
4. AI Search instance provisioning/configuration;
5. upload/update of current searchable items;
6. deletion of stale AI Search items;
7. hybrid search configuration and reranking;
8. MCP public-endpoint configuration;
9. deployment metadata publication as a workflow artifact.

The synchronization uses only `contents: read` GitHub permissions. Cloudflare credentials are read from GitHub Actions secrets and never committed.

## Required external permission

The Cloudflare API token used by the synchronization workflow must include AI Search `Edit` and `Run` permissions. Cloudflare documents those permissions as required for the AI Search REST API. urlCloudflare AI Search REST API documentationhttps://developers.cloudflare.com/ai-search/get-started/api/

If the existing `CLOUDFLARE_API_TOKEN` does not have those permissions, the workflow fails explicitly rather than silently leaving the index stale.

## MCP

After successful synchronization, the workflow artifact `ecosystem-knowledge-search` contains `ai-search-deployment.json`, including the generated MCP endpoint.

Cloudflare AI Search's MCP endpoint exposes a read-only `search` tool over the indexed content. urlCloudflare AI Search MCP documentationhttps://developers.cloudflare.com/ai-search/api/search/mcp/

The endpoint is intentionally public because the external index currently contains only repositories classified as `authoritative`, `verified`, or `reference_only`; private repositories are not indexed unless private ingestion is explicitly enabled. If private content is introduced later, the endpoint MUST be placed behind Cloudflare Access or another authenticated boundary.

## Relationship to SohailOS.Gateway

The existing .NET Gateway remains the canonical local read-only MCP surface with its three Knowledge Fabric tools:

- `ecosystem.search`
- `ecosystem.get_document`
- `ecosystem.get_source`

The managed Cloudflare AI Search MCP endpoint is a deployment-level retrieval surface, not an additional write-capable SohailOS tool.

## Operational verification

A successful synchronization is established only when:

- the GitHub index run has no permanent failures;
- export succeeds;
- AI Search provisioning/update succeeds;
- every current export item is uploaded;
- stale items are deleted;
- AI Search reports queryable indexed content;
- the generated MCP endpoint is present in the deployment artifact;
- an independent MCP search probe returns a result with repository/path provenance.

No successful external deployment is claimed until the corresponding GitHub Actions run provides those artifacts.
