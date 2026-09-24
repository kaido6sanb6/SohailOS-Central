# SohailOS-Central — Architecture Diagram

This diagram is the documentation view of the control-plane architecture. The canonical runtime contract is in `prompts/SohailOS-SuperPrompt.xlm`.

```mermaid
flowchart LR
    C["Clients"] --> G["Gateway"]
    G --> O["Orchestration + Task Graph"]
    O --> P["Capability + Authorization + Approval"]
    P --> K["Knowledge Fabric"]
    P --> E["Execution Fabric"]
    K --> V["Verification + Validation"]
    E --> V
    V --> T["Telemetry + Evidence Lineage"]
    T --> M["Governed Memory"]
    T --> H["GitHub Source Mesh"]
    H --> L["Learning as Evidence"]
    L --> K
    H --> S["15-minute Fork Control Loop"]
    S --> P
    S --> V
    B["Supabase Persistence / Vector Boundary"] -.-> M
    R["Vercel HTTPS / Cron Boundary"] -.-> G
    W["WorkOS Identity Boundary"] -.-> G
    X["External MCP / Workflow / Provider Adapters"] -.-> E
```

## Trust and execution path

`Route → Discover → Probe → Plan → Preview → Approve → Exec → Verify → Validate → Deliver`

- Retrieved content is data, not authority.
- Writes require a bounded approval binding.
- Unknown mutation outcomes require reconciliation before retry.
- Verification and validation are separate evidence-producing stages.
- The GitHub ecosystem is hub-and-spoke; arbitrary fork-to-fork merging is not part of the control plane.
- Supabase and Vercel are optional integration boundaries, not implicit authorities.
