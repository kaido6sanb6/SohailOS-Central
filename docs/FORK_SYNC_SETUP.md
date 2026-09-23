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

Configure these repository Actions values:

- Variable: `SOHAILOS_GITHUB_APP_ID`
- Secret: `SOHAILOS_GITHUB_APP_PRIVATE_KEY`

The workflow uses `actions/create-github-app-token@v3` to create a short-lived installation token at runtime, scoped to the installed `kaido6sanb6` repositories. The GitHub App should have `Contents: Read and write` and be installed on all repositories that the automatic fork discovery is expected to synchronize.

Do not paste the private key into source files, workflow YAML, issues, commits, or chat. Store it only as the GitHub Actions secret.

## Verification

A successful fork-sync run must satisfy:

- fork inventory is internally consistent;
- reconciliation reports no removals unless explicitly allowed;
- synchronization report has zero blocked, error, and conflict results;
- generated inventory/report can be committed;
- the synchronization gate passes.

If a fork has a genuine upstream conflict, the run is intentionally marked incomplete rather than resolving the conflict destructively.

## Current operational state

The repository currently discovers **121 forks**. The earlier verified synchronization run demonstrated inventory discovery and registry reconciliation, but cross-repository synchronization was blocked before the GitHub App credential was configured. The workflow intentionally fails its synchronization gate rather than reporting a false success. After the App is installed and the two values above are configured, run the workflow manually once to verify real cross-repository synchronization.
