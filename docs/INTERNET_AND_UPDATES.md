# Internet Connectivity and Self-Update

SohailOS is designed as an online-capable agent, but network access is explicit and bounded.

## Connectivity

The desktop app runs a background connectivity check every five minutes against the project's GitHub README endpoint. The UI reports Online/Offline without treating connectivity as proof that every external provider is available.

## Web access

`WebFetchTool` only permits HTTPS and can use a host allowlist configured by `SOHAILOS_WEB_ALLOWLIST`.

`WebSearchTool` is available for a configured search provider. The endpoint and key are supplied through environment variables; credentials are never stored in the repository.

## Self-update

The application checks `deploy/update-manifest.json` when `SOHAILOS_UPDATES_ENABLED=true`.

The update flow is:

1. Fetch manifest over HTTPS from the trusted GitHub repository.
2. Compare the remote version with the running version.
3. Download the package.
4. Verify SHA-256 against the manifest.
5. Stage the package in the user's local application-data directory.
6. Launch the local update script after the application exits.
7. Replace the application files and remove the staged package.

GitHub Actions packages tagged releases and updates the manifest automatically. A release must therefore be created by pushing a semantic version tag such as `v0.1.1`.

## Safety boundary

Self-update is intentionally not implemented as arbitrary self-modifying code. SohailOS can update its released binaries, but model prompts, permissions, tool policies, and source code remain auditable in GitHub.

Future hardening should add signed release metadata in addition to SHA-256 and a rollback mechanism.
