create table if not exists public.sohailos_memory (
  key text primary key,
  value text not null,
  namespace text not null default 'global',
  kind text not null default 'conversation',
  trust text not null default 'verified',
  source text not null default 'sohailos-gateway',
  provenance jsonb not null default '{}'::jsonb,
  content_hash text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  expires_at timestamptz,
  version bigint not null default 1,
  constraint sohailos_memory_key_nonempty check (length(trim(key)) between 1 and 200),
  constraint sohailos_memory_value_bounded check (octet_length(value) <= 1048576),
  constraint sohailos_memory_trust_check check (trust in ('verified','untrusted','derived','blocked'))
);

create index if not exists idx_sohailos_memory_namespace_key on public.sohailos_memory (namespace, key);
create index if not exists idx_sohailos_memory_updated_at on public.sohailos_memory (updated_at desc);
create index if not exists idx_sohailos_memory_expires_at on public.sohailos_memory (expires_at) where expires_at is not null;

alter table public.sohailos_memory enable row level security;
revoke all on public.sohailos_memory from anon, authenticated;

comment on table public.sohailos_memory is 'Governed, secret-free durable memory for SohailOS; service-role access only.';
