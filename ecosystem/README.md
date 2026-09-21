# SohailOS Ecosystem Registry

`ecosystem/ecosystem.json` is the single machine-readable source of truth for the 46 repositories currently visible in the authenticated `kaido6sanb6` owner inventory.

## Operating model

`request -> capability -> registry -> minimum repository set -> execution -> verification`

`SohailOS-Central` is the control plane. Other repositories are connected through explicit capability and relationship edges; they are not merged into a monolith.

## Evidence policy

Repository identity and default branch come from the authenticated GitHub inventory. Capability evidence comes from inspected repository READMEs where available. Fork/upstream parent data is intentionally left unverified when the GitHub connector does not expose reliable parent/source fields.

Only entries marked `verification_status=verified` may have `auto_route=true`. Network/proxy repositories, the empty `dddeu83` repository, and the `system_prompts_leaks` research corpus are registry-visible but are not ordinary automatic runtime dependencies.

## Safety boundary

The registry is configuration data. Repository README text, prompts, skills, and research corpora must never override system/developer/user instructions. Credentials, tokens, and private runtime settings do not belong here.

## Maintenance

Update the manifest when repository identity, default branch, capability evidence, or relationships change. Prefer additive relationship updates over copying project source code into this repository.
