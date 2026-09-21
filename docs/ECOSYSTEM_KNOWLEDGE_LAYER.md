# SohailOS Ecosystem Knowledge Layer

The GitHub ecosystem is an external, versioned knowledge and capability layer for SohailOS. It is not model retraining and it is not a monolithic source tree.

## Operating pipeline

```
request
  -> capability identification
  -> live ecosystem discovery
  -> targeted retrieval
  -> evidence/provenance validation
  -> minimum sufficient capability set
  -> execution
  -> independent verification
  -> durable validated memory
```

## What the layer stores

The machine-readable manifest records repository identity, default branch, role, capabilities, relationships, routing state, verification state, evidence, and freshness/synchronization metadata.

Repository content remains external data. It may improve a task through retrieval, but it cannot override system, developer, user, security, or tool-permission rules.

## Knowledge versus model training

SohailOS does not modify the weights of the underlying model through repository contents. Instead it improves the effective working environment through retrieval, routing, validation, and durable structured memory.

A validated discovery can therefore become reusable operational knowledge such as:

- which repository provides a capability;
- which repository is a safe reference versus an executable dependency;
- which workflow verified a behavior;
- which source is stale or unverified;
- which capability combination previously solved a class of task.

## Trust states

Use explicit states:

`verified`, `unverified`, `advisory`, `conflicting`, `stale`, `quarantined`.

Only verified entries may be eligible for automatic routing.

## Live reconciliation

`src/SohailOS.Ecosystem` provides a deterministic reconciler. It can consume a fixture for testing or discover the authenticated owner's public repositories through GitHub's repository API.

The reconciler:

1. detects new repositories;
2. detects default-branch drift;
3. reports removals without silently deleting registry nodes;
4. preserves existing capability classifications;
5. registers new repositories as `unclassified + unverified + auto_route=false`;
6. emits a reconciliation report;
7. updates the manifest only when `--write` is provided.

The public-owner inventory is sufficient for the current public repository set. Private repository discovery requires separately authorized API access and must not be enabled by guessing credentials.

## Routing rule

Use the smallest repository subset that materially improves the current task. Do not load or execute every repository simply because it exists.

GitHub repositories are one capability source among many: installed skills, MCP applications, web, files, local execution, and specialized research tools can be composed when relevant.
