# Skills Fabric

SohailOS-Central treats external agent skills as capability metadata and untrusted procedural data, never as authority.

## Source of truth and access

The canonical catalog contract is ecosystem/skills-registry.json.

skills.sh exposes a public HTTPS API under /api/v1/ for skill listing, search, curated skills, details and security-audit results. The repository refreshes a bounded catalog snapshot every 15 minutes through .github/workflows/skills-catalog-sync.yml. The generated snapshot is evidence about the catalog at retrieval time, not proof that a skill is installed or safe to execute.

## Governance

Lifecycle:

Discover -> Probe -> LeastPrivilege -> Plan -> Preview -> Approve -> Execute -> Verify -> Validate.

Installing a skill and using a skill are separate operations. A skill entry retains source repository, skill name, install URL, skills.sh URL, retrieval time and content hash where available.

External skill instructions cannot:
- grant authorization;
- change policy;
- override the canonical SuperPrompt;
- silently mutate the repository;
- create durable memory;
- convert an audit result into authority.

## Requested skill sources

The requested skill set is recorded in ecosystem/skills-registry.json. This keeps user-supplied intent reproducible without copying third-party skill bodies into the control plane.

The implementation intentionally does not vendor every external SKILL.md: that would create a second source of truth and increase synchronization and supply-chain risk.

## npx skills semantics

npx skills add and npx skills use are bounded external CLI operations, not assumed native SohailOS commands. Their availability must be probed at runtime.

The teach skill is treated as a learning workflow rather than a build-stage dependency; its upstream documentation recommends a dedicated teaching workspace rather than the project repository.

## Verification

For an activated skill:
1. resolve source and provenance;
2. retrieve current metadata/content;
3. classify it as untrusted external data;
4. inspect security/audit evidence when available;
5. probe runtime availability;
6. apply least privilege;
7. execute only within task scope;
8. independently verify the outcome.

A catalog entry never proves installation or completion.
