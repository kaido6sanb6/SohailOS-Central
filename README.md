# SohailOS-Central

Central repository for SohailOS — a personal AI agent and operating system for research, thinking, software/product development, office work, automation, strategy, and learning.

## Current implementation

The repository now contains the first executable .NET foundation:

- `SohailOS.sln` — Visual Studio solution
- `src/SohailOS.Core/` — domain models and stable interfaces
- `src/SohailOS.Agents/` — module agents and Master Orchestrator
- `src/SohailOS.AI/` — provider abstraction and local stub provider
- `src/SohailOS.Memory/` — memory abstraction and in-memory implementation
- `src/SohailOS.Data/` — persistence boundary
- `src/SohailOS.Integrations/` — external service boundary
- `src/SohailOS.App/` — initial Windows WPF desktop client
- `tests/SohailOS.Tests/` — routing tests
- `.github/workflows/build.yml` — CI build/test pipeline
- `system/` — identity, routing, modules, and operating rules
- `memory/` — portable, non-sensitive user context
- `prompts/` — reusable prompt library
- `workflows/` — repeatable operating workflows
- `docs/` — architecture and implementation documentation

## Architecture

```text
User
  |
  v
Desktop App / future clients
  |
  v
Master Orchestrator
  |
  +--> Module Agents
  |      THINK / SOCIOLOGY / CINEMA / RESEARCH / STATS / AI
  |      CODE / PRODUCT / OFFICE / OPERATIONS / STRATEGY / LEARNING
  |
  +--> Memory
  |
  +--> AI Provider Layer
  |      OpenAI / Anthropic / Gemini
  |
  +--> Integrations
         GitHub / Notion / Todoist / Supabase / Airtable / Research
```

## Design principle

ChatGPT, Claude, and Gemini should be able to use the same central specification and portable context while remaining separate model providers. The desktop application is the runtime/orchestration layer; GitHub is the source-of-truth for code and system specification.

Sensitive personal information, credentials, API keys, tokens, and secrets must never be committed to this repository.

See `docs/IMPLEMENTATION_STATUS.md` for the current production-readiness gap list.
