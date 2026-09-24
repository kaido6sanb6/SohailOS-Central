# SohailOS Research Knowledge Fabric

SohailOS treats scholarly repositories as a federated knowledge fabric, not as one monolithic codebase.

## Goals
- Discover scholarly works, authors, institutions, citations, books, theses, and open-access locations.
- Support sociology, psychology, philosophy, history, humanities, and adjacent sciences.
- Preserve provenance, license metadata, upstream identity, and retrieval timestamps.
- Prefer lawful open-access retrieval and metadata APIs.
- Keep external repository code isolated from the SohailOS control plane.

## Integration modes
- API_CONNECTOR: query an external scholarly service.
- DATA_CONNECTOR: consume a permitted metadata/data export.
- DOCUMENT_PIPELINE: parse permitted PDFs/documents into structured records.
- REFERENCE_ONLY: architecture/workflow inspiration; not a runtime dependency.
- ISOLATED_TOOL: runnable tool kept outside the core tree.
- CATALOG_ONLY: discover and link resources without importing their contents.
- EXCLUDE: intentionally not integrated because the access/licensing model is unsuitable.

## Initial fabric
Core discovery and provenance:
- OpenAlex
- OpenCitations
- Unpaywall/oadoi
- Crossref (API integration; no code fork required)

Document and repository layer:
- GROBID
- DSpace
- Samvera/Hyrax
- Open Library

Research workflow layer:
- Zotero-OpenAlex
- PhilLit
- Wisp Science
- existing research-agent / Research-RAG forks

Adjacent sources:
- Semantic Scholar API
- PubMed / Europe PMC
- institutional repositories exposed through OAI-PMH

## Copyright / access boundary
SohailOS may index metadata and use openly licensed or otherwise permitted full text. A fork existing on GitHub does not imply that its associated corpus is freely redistributable. Paywall circumvention or unauthorized distribution is not part of the research fabric.

## Synchronization
Forks remain independently synchronized with their upstreams. SohailOS-Central records identity and capability; it does not blindly merge upstream application code into the core runtime.
