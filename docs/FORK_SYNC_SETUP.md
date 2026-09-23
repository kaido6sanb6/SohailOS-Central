# Fork synchronization operations

## What is automated

```text
Every ~15 minutes
    -> discover all forks owned by kaido6sanb6
    -> resolve parent/source provenance
    -> sync each fork's default branch from upstream
    -> record synced/conflict/error/blocked state
    -> reconcile ecosystem registry
    -> refresh knowledge-fabric consumers
```

The scheduler is intentionally non-destructive. It never force-pushes and records conflicts instead of overwriting fork-specific work.

## Required cross-repository credential

GitHub's built-in `GITHUB_TOKEN` is scoped to the repository that runs the workflow. It cannot be used to write to the other repositories that contain the forks.

Configure the repository Actions secret:

`SOHAILOS_GITHUB_SYNC_TOKEN`

Recommended credential choices:

1. **GitHub App installation token** with the minimum required repository permissions for the fork set.
2. **Fine-grained personal access token** restricted to the exact fork repositories that must be synchronized.

Do not paste the credential into source files, workflow YAML, issues, commits, or chat. Store it only as the GitHub Actions secret.

## Verification

A successful fork-sync run must satisfy:

- fork inventory is internally consistent;
- reconciliation reports no removals unless explicitly allowed;
- synchronization report has zero blocked, error, and conflict results;
- generated inventory/report can be committed;
- the synchronization gate passes.

If a fork has a genuine upstream conflict, the run is intentionally marked incomplete rather than resolving the conflict destructively.

## Current operational state

The repository currently discovers **121 forks**. The latest verified synchronization run demonstrated inventory discovery and registry reconciliation, but cross-repository synchronization was blocked because `SOHAILOS_GITHUB_SYNC_TOKEN` was absent. The workflow therefore fails its synchronization gate rather than reporting a false success.
