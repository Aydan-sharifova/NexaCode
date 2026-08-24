# Environment configuration

Copy `.env.example` to an untracked `.env` only for local Docker Compose. Production secrets should come from a managed secret store and be injected into the container runtime.

ASP.NET Core converts `__` to a configuration path separator. Compose maps convenient variables such as `JWT_KEY` to `Jwt__Key`. Direct API deployments should use:

| API variable | Secret | Notes |
|---|---:|---|
| `ConnectionStrings__Default` | Yes | PostgreSQL connection string with TLS options appropriate to the provider |
| `ConnectionStrings__Redis` | Yes | StackExchange format (`host:port,password=...`) or provider URL (`redis://` / `rediss://`); provider URLs are normalized before cache, SignalR, and health-check registration |
| `Jwt__Issuer` | No | Stable issuer URI/name |
| `Jwt__Audience` | No | Stable client audience |
| `Jwt__Key` | Yes | Random value of at least 32 bytes; rotate with an overlap strategy |
| `Cors__AllowedOrigins__0` | No | Exact HTTPS origin; add indices for more origins |
| `Smtp__Enabled` | No | Enables real delivery; keep false when no provider is configured |
| `Smtp__Host` | No | Gmail uses `smtp.gmail.com` |
| `Smtp__Port` | No | Use `587` with STARTTLS |
| `Smtp__UseSsl` | No | `true` uses STARTTLS on port 587 (or implicit TLS on port 465) |
| `Smtp__Username` | Yes | Provider login; for Gmail this is the sending account |
| `Smtp__Password` | Yes | Gmail requires an app password when SMTP is enabled; never commit it |
| `Smtp__FromEmail` | No | Verified sender address, normally matching the Gmail login |
| `Smtp__ClientBaseUrl` | No | Public frontend origin used in verification and reset links |
| `AI__Provider` | No | Use `Ollama` (or the equivalent `OpenAICompatible`) |
| `OpenAICompatible__BaseUrl` | No | Ollama/vLLM OpenAI-compatible `/v1/` base URL |
| `OpenAICompatible__Model` | No | Local or self-hosted model name |
| `OpenAICompatible__VisionModel` | For images | Vision-capable model used when an image is attached |
| `OpenAICompatible__ApiKey` | When required | Use `ollama` for local Ollama; use a secret for authenticated vLLM |
| `Database__ApplyMigrations` | No | Must remain false in normal production API containers |
| `Database__SeedDevelopmentData` | No | Must remain false in production |
| `Execution__Enabled` | No | Local development only; must remain false in the public production API |
| `Execution__DotNetImage` | No | Fixed .NET SDK runtime image used by the local Docker sandbox |

Validate configuration in a staging environment. Do not log environment dumps. Restrict secret-read permissions to the deployment identity, audit access, and rotate database, Redis, JWT, SMTP, and AI credentials independently.

## Local Gmail SMTP

Enable Google 2-Step Verification, create a Google App Password, and store it with
.NET User Secrets. A normal Google account password will not work and must not be
stored in this repository.

```bash
cd /Users/aydansrifova/Desktop/Coding/src/Coding.Api
dotnet user-secrets set "Smtp:Enabled" "true"
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:UseSsl" "true"
dotnet user-secrets set "Smtp:Username" "YOUR_EMAIL@gmail.com"
dotnet user-secrets set "Smtp:Password" "YOUR_GOOGLE_APP_PASSWORD"
dotnet user-secrets set "Smtp:FromEmail" "YOUR_EMAIL@gmail.com"
dotnet user-secrets set "Smtp:FromName" "NexaCode"
dotnet user-secrets set "Smtp:ClientBaseUrl" "http://localhost:5173"
```
