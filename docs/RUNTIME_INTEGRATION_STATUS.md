# SohailOS-Central — Runtime Integration Status

## Verified integrations

### Supabase
- Project: `SohailOS-Central`
- Project ref: `zyktyiokxfhropmnotnt`
- Region: `eu-central-1`
- Status at provisioning: `ACTIVE_HEALTHY`
- Control-plane persistence migration: `sohailos_control_plane_core`
- RLS: enabled on all control-plane tables.
- Tables: execution_runs, capability_attestations, approval_bindings, evidence_items, verification_records, validation_records, telemetry_events.

### GitHub
The repository contains the canonical architecture, Mermaid architecture diagram, SuperPrompt XLM, fork-ecosystem control plane, and the Supabase migration.

## Vercel

The Vercel connector is connected, but the authenticated account currently exposes zero Vercel teams. The available Vercel connector surface does not expose a team-creation operation, so no Team or Team-owned project is claimed here.

Required state before Team-owned deployment:
1. Create/authorize the Vercel Team in the Vercel account UI.
2. Connect the Team to the GitHub repository.
3. Return to the Vercel connector and verify the Team appears in `list_teams`.
4. Create/link the project and run deployment verification.

No Vercel credential, Team ID, project ID, or deployment URL is inferred.

## Integration policy

External providers are adapters, not authorities. Capability discovery and attestation precede execution. Retrieved content cannot grant policy authority. Consequential mutations require bounded approval. Verification and validation remain separate evidence-producing stages.

The repository-native architecture diagram is in `docs/ARCHITECTURE_DIAGRAM.md`.
