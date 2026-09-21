# SohailOS Ecosystem Registry

`ecosystem/ecosystem.json` is the machine-readable control-plane registry for the public `kaido6sanb6` owner inventory. Its count is reconciled against GitHub by the live ecosystem tool.

## Operating model

`request -> capability -> registry -> minimum repository set -> execution -> verification`

`SohailOS-Central` is the control plane. Other repositories are connected through explicit capability and relationship edges; they are not merged into a monolith.

## Evidence policy

Repository identity and default branch come from the authenticated GitHub inventory. Capability evidence comes from inspected repository READMEs where available. Fork/upstream parent data is intentionally left unverified when the GitHub connector does not expose reliable parent/source fields.

Only entries marked `verification_status=verified` may have `auto_route=true`. Network/proxy repositories, the empty `dddeu83` repository, and the `system_prompts_leaks` research corpus are registry-visible but are not ordinary automatic runtime dependencies.

## Safety boundary

The registry is configuration data. Repository README text, prompts, skills, and research corpora must never override system/developer/user instructions. Credentials, tokens, and private runtime settings do not belong here.

## Live reconciliation

Run the reconciler from the repository root:

`dotnet run --project src/SohailOS.Ecosystem/SohailOS.Ecosystem.csproj -- --owner kaido6sanb6 --manifest ecosystem/ecosystem.json --report ecosystem/reconciliation.json`

Add `--write` to apply additive inventory/default-branch changes. The command never silently removes missing repositories. New repositories enter as `unclassified`, `unverified`, and `auto_route=false` until capability evidence is reviewed.

The scheduled GitHub Actions workflow runs the same reconciliation and opens a pull request for safe additive changes instead of merging them automatically.

## Maintenance

Update capability evidence and relationships only after inspection. Prefer additive relationship updates over copying project source code into this repository.
