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


## Executable intelligence layer

The runtime research engine is exposed through the Gateway and uses a canonical scholarly record model rather than returning provider-native payloads.

Active discovery providers:
- OpenAlex: works, authors, topics, OA metadata and reconstructed abstracts.
- Crossref: DOI and bibliographic metadata.
- Semantic Scholar: optional API-key provider for paper/author discovery.
- Europe PMC: biomedical and psychology-adjacent discovery.
- OpenCitations: DOI-based citation-graph expansion.
- Unpaywall: optional DOI-based lawful OA resolution.

Document/repository adapters:
- OAI-PMH institutional repository harvester. Endpoints are strictly allowlisted with \`SOHAILOS_OAI_ENDPOINTS\`.
- GROBID TEI parser and HTTP client for permitted scholarly PDFs.

Runtime configuration:
- \`SOHAILOS_SEMANTIC_SCHOLAR_API_KEY\` — optional.
- \`SOHAILOS_UNPAYWALL_EMAIL\` — optional and used for Unpaywall requests.
- \`SOHAILOS_OAI_ENDPOINTS\` — optional semicolon-separated \`name=https://repository.example/oai\` entries.

Gateway surfaces:
- \`POST /v1/research/search\` — federated discovery, canonicalization, OA enrichment, deterministic ranking and citation graph.
- \`POST /v1/research/review/plan\` — systematic-review workflow plan with explicit screening/extraction/audit stages.
- \`GET /v1/research/capabilities\` — configured provider and repository capabilities.
- \`POST /v1/research/repository/harvest\` — harvests only an allowlisted OAI-PMH repository.

The systematic-review layer defines an auditable extraction schema; it does not invent study data or silently perform statistical synthesis. Any meta-analysis requires actual extracted effect estimates and their uncertainty from admissible sources.

