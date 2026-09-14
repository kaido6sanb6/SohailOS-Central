# Local AI on AMD

SohailOS is provider-agnostic. The desktop app can call any OpenAI-compatible `/v1/chat/completions` endpoint, so a local vLLM server can be used without changing the orchestration layer.

Recommended architecture:

`SohailOS.App -> OpenAiCompatibleProvider -> local vLLM -> AMD Instinct GPU -> model`

The app reads these environment variables:

- `SOHAILOS_AI_ENDPOINT` — defaults to `http://localhost:8000/v1`
- `SOHAILOS_AI_MODEL` — defaults to `Qwen/Qwen3.5-9B`
- `SOHAILOS_AI_API_KEY` — optional; do not commit it to GitHub

For AMD Instinct hardware, use the ROCm/vLLM serving workflow appropriate to the detected GPU. Validate ROCm, `amd-smi`, Docker, `/dev/kfd`, and `/dev/dri` before launching a model. Choose tensor parallelism and context length from the model's actual VRAM requirements rather than hard-coding them.

This repository intentionally does not contain GPU credentials, Hugging Face tokens, model caches, or host-specific Docker configuration.

## Next AMD integration step

When an AMD Instinct host is available, validate the host first, select a model/quantization variant based on VRAM, launch vLLM, then point the desktop app at the resulting endpoint. The provider contract remains unchanged.
