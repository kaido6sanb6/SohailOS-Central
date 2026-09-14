# AI Provider Architecture

SohailOS uses a provider abstraction so the agent runtime is not tied to one model vendor. Provider selection happens through `SOHAILOS_AI_PROVIDER`; the gateway and desktop app use the same factory.

## Local AMD / vLLM

Set:

- `SOHAILOS_AI_PROVIDER=local`
- `SOHAILOS_AI_ENDPOINT=http://localhost:8000/v1`
- `SOHAILOS_AI_MODEL=Qwen/Qwen3.5-9B`
- `SOHAILOS_AI_API_KEY=` when the local server does not require a key

`local` and `openai-compatible` are aliases for the generic OpenAI-compatible adapter.

## Gemini

SohailOS now has an explicit Gemini profile. It uses Google's OpenAI-compatible endpoint, so function/tool calling can participate in the same agent runtime:

- `SOHAILOS_AI_PROVIDER=gemini`
- `SOHAILOS_GEMINI_ENDPOINT=https://generativelanguage.googleapis.com/v1beta/openai`
- `SOHAILOS_GEMINI_MODEL=gemini-3.8-flash`
- `SOHAILOS_GEMINI_API_KEY=<Gemini key>`

Google documents this compatibility route and function calling in the Gemini API documentation. For advanced Gemini-only features, a native adapter can be added later.

## Anthropic / Claude

Use the native adapter:

- `SOHAILOS_AI_PROVIDER=claude` (or `anthropic`)
- `SOHAILOS_ANTHROPIC_MODEL=claude-sonnet-5`
- `SOHAILOS_ANTHROPIC_API_KEY=<Claude key>`

The model name should be kept configurable because Anthropic regularly changes the active/retired model set.

## OpenAI

Use:

- `SOHAILOS_AI_PROVIDER=openai`
- `SOHAILOS_OPENAI_ENDPOINT=https://api.openai.com/v1`
- `SOHAILOS_OPENAI_MODEL=<chosen model>`
- `SOHAILOS_OPENAI_API_KEY=<OpenAI key>`

## Secret handling

API keys are not stored in source code, GitHub files, prompts, or chat messages. For local Windows development, inject them as environment variables or through a local secret store. For the future public gateway, store them in the hosting provider's encrypted secret/environment-variable facility. Never paste a key into an issue, pull request, README, or `.env` file that is committed.

## Recommended routing policy

SohailOS should eventually route by task instead of forcing one vendor:

1. Local model for routine/private work and low-cost background tasks.
2. Gemini for multimodal/Google-oriented tasks when enabled.
3. Claude for long-context, coding, and agentic reasoning when enabled.
4. OpenAI for OpenAI-native capabilities and tasks where its tool ecosystem is preferred.

The provider abstraction intentionally keeps this routing independent from orchestration, memory, permissions, and integrations.
