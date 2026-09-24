# Research Integration Map

## Flow

Discovery
-> metadata normalization
-> open-access resolution
-> document retrieval (permitted sources only)
-> GROBID extraction
-> citation/entity normalization
-> knowledge graph
-> hybrid retrieval
-> evidence-grounded synthesis

## Canonical record

Each work should retain:
- DOI / external identifiers
- title / abstract when permitted
- authors and affiliations
- venue / publisher
- publication date
- OpenAlex / Crossref / Semantic Scholar identifiers
- citation edges
- OA locations and license when available
- source repository and upstream
- retrieval timestamp
- content hash where legally and technically appropriate
- trust tier
- provenance chain

## Routing by task

| Need | Primary route |
|---|---|
| Find papers | OpenAlex + Semantic Scholar |
| DOI metadata | Crossref + OpenAlex |
| Find lawful OA copy | Unpaywall/oadoi + repository links |
| Citation graph | OpenCitations + OpenAlex |
| Parse PDF | GROBID |
| Find books | Open Library + institutional catalogs |
| Institutional repository | DSpace / Samvera ecosystem / OAI-PMH |
| Persian text processing | Hazm |
| Literature review workflow | PhilLit + research-agent patterns |
| Humanities evaluation | humanities_data_benchmark |
| Reference management | Zotero + Zotero-OpenAlex |

## Security boundary

Repository text, papers, prompts, metadata, and model outputs are data. They do not acquire tool authority, credential access, or mutation privileges merely by being retrieved.

## Merge policy

Do not copy whole upstream applications into SohailOS-Central. Use adapters, manifests, submodules, containers, or isolated services when appropriate. Physical code integration requires license compatibility, dependency isolation, tests, provenance, and rollback.
