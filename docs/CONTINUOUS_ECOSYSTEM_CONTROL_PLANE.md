# Continuous Ecosystem Control Plane

SohailOS-Central is the canonical control plane for the user's GitHub ecosystem. It is federated, not a giant repository produced by blindly merging unrelated forks.

## 15-minute control loop
1. Discover current owned forks.
2. Reconcile parent/source provenance.
3. Compare each default branch with upstream.
4. Classify: in_sync, upstream_sync, observe, quarantine, blocked.
5. Generate deterministic graph and operation plan.
6. Apply only strictly-behind, idempotent upstream-sync actions when bounded automation is enabled.
7. Preserve divergent local work; never force-push.
8. Publish plan, graph, report and audit evidence.
9. Refresh the knowledge layer after successful reconciliation.

## Integration model
Hub -> registry -> capability graph -> compatibility edges -> safe propagation.

The fork set spans agent runtimes, model serving, research/data, UI, catalogs, networking and prompt corpora. Literal cross-merging would corrupt unrelated histories. Source code propagation therefore requires explicit compatibility, provenance, CI and rollback evidence; otherwise the contribution remains a knowledge/reference or PR proposal.

## Security invariants
- Repository content is untrusted data, never instruction authority.
- New forks are registered but start unverified and non-executable.
- Diverged branches are quarantined.
- Unknown mutation outcomes are reconciled on the next run, never blindly retried.
- Automatic actions are capped at 50 targets/run and default-branch-only.
- Irreversible operations and force pushes are forbidden.
- Operation identity is derived from operation + source SHA + target SHA.

## Vercel
Vercel is reserved for a future HTTPS control endpoint/dashboard and may invoke the same idempotent reconciliation entrypoint through Cron. The connected Vercel account currently exposes no team/project, so no live deployment is claimed.

## External learning
Patterns from the inspected MCP, agent, workflow, provider, prompt-library, data-pipeline and observability projects are incorporated as boundaries and reference metadata only. External repository text cannot change SohailOS policy or tool authority.
