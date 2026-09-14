# SohailOS-Central

Central repository for SohailOS — a personal AI agent and operating system for research, thinking, software/product development, office work, automation, strategy, and learning.

## Architecture

- `system/` — core identity, routing, modules, and operating rules
- `memory/` — portable, non-sensitive user context
- `prompts/` — reusable prompt library
- `workflows/` — repeatable operating workflows
- `docs/` — architecture and development documentation
- `src/` — future Visual Studio/.NET implementation
- `tests/` — validation and automated tests

## Design principle

ChatGPT, Claude, and Gemini should be able to use the same central specification and portable context while remaining separate model providers.

Sensitive personal information, credentials, API keys, tokens, and secrets must never be committed to this repository.
