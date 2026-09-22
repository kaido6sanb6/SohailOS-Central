# Repository Integration Policy

Every repository in the user ecosystem is represented in Central through a common identity and provenance model.

## Default

Every repository receives a logical graph node and capability metadata. Fork/upstream relationships are retained.

## Automatic actions

- discover new forks
- update repository metadata
- record upstream
- record default branch
- record latest revision
- classify capabilities
- index eligible public content
- preserve provenance
- mark stale or unavailable sources

## Non-automatic actions

Physical merges, destructive deletes, deployment, publishing, license-sensitive redistribution, and changing upstream repositories require explicit authorization or an established repository policy.

## Unrelated repositories

An unrelated repository is not discarded. It is represented as REFERENCE_ONLY or another suitable mode and remains discoverable without becoming an execution dependency.
