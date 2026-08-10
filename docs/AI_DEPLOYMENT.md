# AI deployment

AI is optional for core platform startup. `AI__Provider` selects `Development`, `OpenAI`, `Ollama`/`OpenAICompatible`. Production should use either:

1. External provider: `AI_PROVIDER=OpenAI`, HTTPS `OPENAI_BASE_URL`, model and server-side API key.
2. Separate private Ollama/vLLM host: `AI_PROVIDER=OpenAICompatible`, private/TLS base URL, model and authentication where supported.

Never use localhost in production or expose Ollama directly to the internet. Keep keys only in VPS/provider secrets. Configure provider timeouts at the network edge, limit output tokens and test streaming. Basic health intentionally excludes AI; AI requests should return a graceful provider error while projects, authentication and collaboration remain available. GPU/RAM requirements depend on the selected model and belong on the separate AI host.
