# AI deployment

AI is optional for core platform startup. The application uses `Ollama`/`OpenAICompatible` for every model-generated AI feature.

Local development uses `AI_PROVIDER=Ollama` with `OPENAI_COMPATIBLE_BASE_URL=http://localhost:11434/v1/`. Production must use a separately hosted Ollama service reachable from the API and set `OPENAI_COMPATIBLE_BASE_URL`, `OPENAI_COMPATIBLE_MODEL`, and, when the endpoint is protected, `OPENAI_COMPATIBLE_API_KEY`.

Never use localhost in production or expose Ollama directly to the internet. Keep keys only in VPS/provider secrets. Configure provider timeouts at the network edge, limit output tokens and test streaming. Basic health intentionally excludes AI; AI requests should return a graceful provider error while projects, authentication and collaboration remain available. GPU/RAM requirements depend on the selected model and belong on the separate AI host.
