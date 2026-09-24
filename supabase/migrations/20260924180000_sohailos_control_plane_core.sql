-- SohailOS-Central control-plane persistence
-- Applied to Supabase project zyktyiokxfhropmnotnt as migration sohailos_control_plane_core.
-- Keep schema additive; all tables use RLS and are intended for trusted server-side runtime access.

create table if not exists public.execution_runs (
  id uuid primary key default gen_random_uuid(),
  request_id text not null,
  status text not null check (status in ('planned','previewed','approved','executing','verifying','validating','completed','blocked','failed')),
  operation_hash text,
  started_at timestamptz not null default now(),
  completed_at timestamptz,
  metadata jsonb not null default '{}'::jsonb
);

create table if not exists public.capability_attestations (
  id uuid primary key default gen_random_uuid(),
  execution_id uuid references public.execution_runs(id) on delete cascade,
  capability_name text not null,
  status text not null check (status in ('VERIFIED','LIMITED','UNAVAILABLE','FORBIDDEN')),
  fingerprint text,
  evidence jsonb not null default '{}'::jsonb,
  expires_at timestamptz,
  created_at timestamptz not null default now()
);

create table if not exists public.approval_bindings (
  id uuid primary key default gen_random_uuid(),
  execution_id uuid references public.execution_runs(id) on delete cascade,
  operation_hash text not null,
  target text not null,
  scope_hash text not null,
  effect text not null,
  reversible boolean not null,
  rollback text,
  risk_summary text,
  nonce text not null unique,
  expires_at timestamptz not null,
  consumed_at timestamptz,
  created_at timestamptz not null default now()
);

create table if not exists public.evidence_items (
  id uuid primary key default gen_random_uuid(),
  execution_id uuid references public.execution_runs(id) on delete cascade,
  kind text not null,
  source text not null,
  content_hash text not null,
  provenance jsonb not null default '{}'::jsonb,
  independent boolean not null default false,
  created_at timestamptz not null default now()
);

create table if not exists public.verification_records (
  id uuid primary key default gen_random_uuid(),
  execution_id uuid references public.execution_runs(id) on delete cascade,
  evidence_id uuid references public.evidence_items(id) on delete set null,
  verifier text not null,
  result text not null check (result in ('PASS','FAIL','LIMITED','UNKNOWN')),
  details jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create table if not exists public.validation_records (
  id uuid primary key default gen_random_uuid(),
  execution_id uuid references public.execution_runs(id) on delete cascade,
  evidence_id uuid references public.evidence_items(id) on delete set null,
  validator text not null,
  result text not null check (result in ('PASS','FAIL','LIMITED','UNKNOWN')),
  details jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create table if not exists public.telemetry_events (
  id bigint generated always as identity primary key,
  execution_id uuid references public.execution_runs(id) on delete set null,
  event_type text not null,
  severity text not null default 'info',
  payload jsonb not null default '{}'::jsonb,
  occurred_at timestamptz not null default now()
);

create index if not exists idx_execution_runs_request_id on public.execution_runs(request_id);
create index if not exists idx_capability_attestations_execution on public.capability_attestations(execution_id);
create index if not exists idx_approval_bindings_execution on public.approval_bindings(execution_id);
create index if not exists idx_evidence_items_execution on public.evidence_items(execution_id);
create index if not exists idx_verification_records_execution on public.verification_records(execution_id);
create index if not exists idx_validation_records_execution on public.validation_records(execution_id);
create index if not exists idx_telemetry_events_execution on public.telemetry_events(execution_id);

alter table public.execution_runs enable row level security;
alter table public.capability_attestations enable row level security;
alter table public.approval_bindings enable row level security;
alter table public.evidence_items enable row level security;
alter table public.verification_records enable row level security;
alter table public.validation_records enable row level security;
alter table public.telemetry_events enable row level security;