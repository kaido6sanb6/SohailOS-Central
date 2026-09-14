# Supabase persistent memory

SohailOS now supports an optional server-side memory store backed by Supabase PostgREST.

## Database

Apply `supabase/migrations/001_sohailos_memory.sql` to the Supabase project. The table is `public.sohailos_memory` with:

- `key` — primary key
- `value` — JSON/text payload
- `updated_at` — last write timestamp

Row Level Security is enabled. The gateway uses the Supabase service-role key server-side, so that key must never be exposed to a browser, ChatGPT client, repository, or chat message.

## Gateway configuration

Set these deployment secrets/environment variables:

- `SOHAILOS_SUPABASE_URL`
- `SOHAILOS_SUPABASE_SERVICE_ROLE_KEY`
- optional `SOHAILOS_SUPABASE_MEMORY_TABLE` (default: `sohailos_memory`)

When both required values exist, the gateway uses `SupabaseMemoryStore`. If they are absent, it falls back to the existing `InMemoryStore` so local development still works.

`POST /v1/agent/run` accepts an optional `memoryKey` field and defaults to `global`. The runtime loads that key before the request and stores the latest user/assistant exchange after a successful completion.

This is the V1 persistence foundation, not the final long-term-memory system. Retrieval, multiple memories per user, embeddings, summarization, retention rules, and memory governance remain future work.
