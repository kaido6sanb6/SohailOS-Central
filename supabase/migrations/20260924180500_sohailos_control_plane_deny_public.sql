-- Keep control-plane persistence server-side only.
-- service_role/server-side trusted runtime bypasses RLS; anon/authenticated are explicitly denied.

create policy execution_runs_deny_public on public.execution_runs for all to anon, authenticated using (false) with check (false);
create policy capability_attestations_deny_public on public.capability_attestations for all to anon, authenticated using (false) with check (false);
create policy approval_bindings_deny_public on public.approval_bindings for all to anon, authenticated using (false) with check (false);
create policy evidence_items_deny_public on public.evidence_items for all to anon, authenticated using (false) with check (false);
create policy verification_records_deny_public on public.verification_records for all to anon, authenticated using (false) with check (false);
create policy validation_records_deny_public on public.validation_records for all to anon, authenticated using (false) with check (false);
create policy telemetry_events_deny_public on public.telemetry_events for all to anon, authenticated using (false) with check (false);