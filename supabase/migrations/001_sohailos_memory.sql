create table if not exists public.sohailos_memory (
    key text primary key,
    value text not null,
    updated_at timestamptz not null default now()
);

create index if not exists sohailos_memory_updated_at_idx
    on public.sohailos_memory (updated_at desc);

alter table public.sohailos_memory enable row level security;

comment on table public.sohailos_memory is
    'Server-side persistent key/value memory for SohailOS. Accessed by the gateway with the Supabase service-role key.';
