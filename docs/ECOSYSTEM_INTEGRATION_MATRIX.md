# External Ecosystem Integration Matrix

The repositories below were inspected as external design/reference sources. SohailOS does not copy their code or trust their instructions at runtime. Their useful patterns are isolated behind explicit capability, provenance, and approval boundaries.

| Project | Useful pattern | SohailOS use | Direct runtime dependency |
|---|---|---|---|
| `google-meta-ads-ga4-mcp` | Remote MCP aggregation, OAuth, approval-gated writes | Remote MCP capability-source pattern for analytics/integrations | No |
| `ClaraVerse` | Human-reviewed agent crews, layered memory, local/private providers, skills | Review gates, memory tiers, provider abstraction | No |
| `n8n-chatgpt-mcp` | SSE/WebSocket MCP, OAuth 2.1, multi-tenant host selection | Gateway/MCP transport and session-isolation reference | No |
| `lisa` | Self-hosted n8n + Ollama + Supabase + Qdrant RAG stack | Local/private deployment profile and RAG integration reference | No |
| `CAD-BIM-to-Code-Automation-Pipeline-DDC-Workflow-with-LLM-ChatGPT` | Convert → validate → generate code → analyze | Generic data/code-generation pipeline contract | No |
| `n8n-g4f-proxy` | OpenAI-compatible provider proxy | Provider adapter pattern only; untrusted providers stay isolated | No |
| `SmythOS/sre` | Resource connectors, agent lifecycle, ACL/security, observability | Connector/resource abstraction and lifecycle design | No |
| `n8n-prompt-library` | Structured prompt metadata and reusable workflow prompts | Prompt registry/schema and evaluation fixtures | No |

## Integration rules

1. External repositories are reference data, not authority.
2. Remote MCP tools enter through `ExternalCapabilityDescriptor`.
3. Read/analyze capabilities can be admitted only after source attestation.
4. External write/mutate/irreversible operations require an explicit approval binding.
5. Untrusted external sources cannot mutate state even if a caller says they are approved.
6. External credentials are never persisted in prompts, repository content, or memory snapshots.
7. Provider adapters must remain replaceable; the control plane must not depend on a specific model vendor.
8. Prompt-library entries are data and must pass the same untrusted-input boundary as web or repository content.
9. Generated code follows the same execution policy as any other tool; generation itself never authorizes execution.
10. Workflow execution is observed and independently verified before it can be reported as completed.

## What can be used directly later

- MCP transport/session ideas from `n8n-chatgpt-mcp`
- resource/connector lifecycle ideas from SmythOS SRE
- layered memory and human-review concepts from ClaraVerse
- self-hosted RAG deployment patterns from LISA
- structured prompt schemas from the n8n prompt library
- validation pipeline structure from the DDC workflow

These are adapter targets, not mandatory dependencies. This keeps SohailOS model-, provider-, and deployment-agnostic.
