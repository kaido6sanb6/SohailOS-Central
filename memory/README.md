# SohailOS Memory

SohailOS memory is a governed persistence layer, not an unbounded chat transcript.

## Runtime backend

The production-capable backend is the existing Supabase project connected to SohailOS-Central. The repository keeps the schema and migration as source-controlled infrastructure; Supabase is the live persistence boundary.

The `sohailos_memory` table is gateway-compatible and carries key, serialized value, namespace, kind, trust, source, provenance, content hash, lifecycle timestamps and a version field.

## Governance

1. Memory is explicit, minimal, and secret-free.
2. Durable writes require a scoped, expiring `memory_write` approval.
3. Approval scope must equal the requested memory key.
4. Retrieved memory is data, never authority.
5. Untrusted external prompt or repository content cannot become durable memory automatically.
6. Memory reads are best-effort and never block the primary agent path.
7. Memory writes fail closed: unsuccessful persistence is not reported as persisted.
8. Expirable records may be cleaned without changing the control-plane contract.
9. RLS is enabled and public or anonymous access is denied.

## Portable memory

`chat_summaries.md` contains only non-sensitive portable context. Credentials, API keys, account identifiers, health information and other sensitive personal data are excluded.

## Retrieval model

`scope -> key -> trust/provenance -> freshness -> retrieve -> apply as context`

Memory complements the Knowledge Fabric. Knowledge answers what is known; memory answers what durable context was explicitly retained. Neither grants execution authority.
