# AI Provider Architecture

SohailOS uses a provider abstraction so the agent runtime is not tied to one model vendor.

## Local AMD / vLLM

Set:

- `SOHAILOS_AI_PROVIDER=openai-compatible`
- `SOHAILOS_AI_ENDPOINT=http://localhost:8000/v1`
- `SOHAILOS_AI_MODEL=Qwen/Qwen3.5-9B`
- `SOHAILOS_AI_API_KEY=` when the local server does not require a key

## Gemini

Gemini can use the same OpenAI-compatible provider by changing the endpoint and model:

- `SOHAILOS_AI_PROVIDER=openai-compatible`
- `SOHAILOS_AI_ENDPOINT=https://generativelanguage.googleapis.com/v1beta/openai`
- `SOHAILOS_AI_MODEL=<chosen Gemini model>`
- `SOHAILOS_AI_API_KEY=<Gemini key>`

The compatibility layer is intentionally the first integration because it keeps the SohailOS runtime provider-neutral. Advanced Gemini-only capabilities can later receive a native adapter.

## Anthropic / Claude

Use the native adapter:

- `SOHAILOS_AI_PROVIDER=anthropic`
- `SOHAILOS_ANTHROPIC_MODEL=<chosen active Claude model>`
- `SOHAILOS_ANTHROPIC_API_KEY=<Claude key>`

Secrets are environment variables only. Never commit API keys to GitHub.

## Routing policy

The runtime should eventually support model routing by task rather than one global model. A practical policy is:

1. Local model for routine/private work.
2. Gemini for multimodal or Google ecosystem workloads when explicitly enabled.
3. Claude for long-context, coding, and agentic tasks when explicitly enabled.
4. OpenAI for workloads that benefit from OpenAI-native tools or Responses API features.

The provider layer must remain swappable so changing a model never requires rewriting the orchestration, memory, permissions, or integration layers.
