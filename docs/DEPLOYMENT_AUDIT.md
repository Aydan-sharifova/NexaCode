# Production deployment audit

Status: production preparation implemented; external infrastructure is not deployed.

## Repository structure

| Surface | Detected implementation |
|---|---|
| API | `src/Coding.Api`, ASP.NET Core 8 startup project |
| Application | `src/Coding.Application`, CQRS/MediatR/FluentValidation |
| Domain | `src/Coding.Domain` |
| Infrastructure | `src/Coding.Infrastructure`, EF Core/Npgsql/Identity/providers |
| Main web app | `frontend`, React 19 + TypeScript + Vite + React Router |
| Showcase | `showcase`, vinext/Cloudflare Worker-compatible Next API surface |
| Tests | xUnit unit/integration projects; Vitest and Playwright in both web apps |

`AppDbContext` uses Npgsql and 13 checked-in migrations. No `EnsureCreated()` production path was found. The API supports `--migrate`, which is used as a controlled one-shot deployment command.

## Runtime services and ports

| Component | Development | Container/internal | Public production |
|---|---:|---:|---:|
| React/Vite | 5173 | independently hosted | HTTPS 443, `app.yourdomain.com` |
| Showcase | framework dev server | independently hosted | HTTPS 443, `yourdomain.com` |
| API | 5192 | 8080 | via Nginx HTTPS 443, `api.yourdomain.com` |
| Nginx bootstrap | n/a | 8080 | HTTP 80 until TLS is enabled |
| PostgreSQL | configured connection | 5432 | not published |
| Redis | optional locally | 6379 | not published |

## Existing production capabilities

- JWT bearer authentication, refresh tokens, explicit credentialed CORS, rate limiting, Problem Details and structured Serilog output.
- SignalR JWT query-token support, automatic client reconnect and optional Redis backplane.
- PostgreSQL readiness and optional Redis readiness checks.
- Background services for stale connections, collaboration materialization and demo reset (demo is disabled in production).
- Provider abstraction for development, OpenAI, and OpenAI-compatible AI services.
- Multi-stage non-root API and frontend Dockerfiles.

## Environment model

The complete placeholder model is in `.env.example`, `frontend/.env.example`, and `showcase/.env.example`. Required production secrets are PostgreSQL, Redis, JWT and—only when enabled—SMTP/AI credentials. ASP.NET configuration uses `ConnectionStrings__Default`, `ConnectionStrings__Redis`, `Jwt__*`, `Cors__AllowedOrigins__0`, `Smtp__*`, `AI__Provider`, `OpenAI__*`, and `OpenAICompatible__*`.

## Problems found and disposition

| Finding | Severity | Disposition |
|---|---|---|
| A real Gmail address and app password were committed in two appsettings files | Critical | Removed. Rotate/revoke the exposed app password immediately; repository history still contains it. |
| Production AI fallback referenced localhost Ollama | High | Replaced in production settings and Compose with external/provider-driven configuration. |
| Only one aggregate `/health` endpoint existed | Medium | Added `/health/live` and `/health/ready`; AI is intentionally excluded. |
| No API-domain Nginx configuration or production-specific Compose | High | Added WebSocket-capable Nginx and `docker-compose.prod.yml`. |
| No SPA provider rewrite | High | Added Vercel rewrite for React Router deep links. |
| No gated CI/deployment chain | High | Added CI, GHCR API publishing after successful CI, and VPS workflow skeleton. |
| No production backup/restore procedure | High | Added guarded scripts and documentation. |
| API trusts forwarded headers from its network | Medium | Acceptable only while API port remains on the internal Docker network; never publish port 8080. |
| No execution worker, queue, or hardened sandbox implementation exists | Critical for code execution | Public API performs no direct process/Docker execution. Keep execution disabled until a separate host and authenticated queue are implemented. |
| Showcase is vinext/Sites/Cloudflare-oriented rather than standard Vercel Next.js | Medium | Preserve the detected runtime. Vercel deployment requires a framework migration; current supported path is Sites/Cloudflare-compatible hosting. |

## Production blockers

- Domain ownership and DNS records.
- Ubuntu VPS, firewall, Docker Engine and TLS certificates.
- GitHub Actions secrets and a production `.env` stored only on the VPS.
- Vercel project for the React app; Sites/Cloudflare deployment access for the current showcase runtime.
- Rotated SMTP credential if email is enabled.
- External AI key/host if AI is enabled.
- A separate execution host and worker implementation before untrusted execution can be offered.

No production deployment was claimed or attempted.

## Verification results (2026-08-09)

- API Release build: passed, zero warnings/errors.
- Frontend: clean install passed; lint/typecheck passed; 11 tests passed; production build passed. Vite reported CSS/chunk-size warnings and npm reported two high-severity dependency advisories.
- Showcase: clean install, lint (one unused-import warning), and two tests passed. The vinext build repeatedly stalled after transforming the client environment under both local Node 23 and supported Node 22.13; it is a release blocker until diagnosed. npm reported 14 high-severity advisories.
- .NET tests: test projects compiled, but the local VSTest process stalled before producing results; test success is not claimed. CI must run them on a clean Ubuntu runner.
- Production Compose: configuration validated successfully.
- API Docker image: built successfully from the repository root.
- Secret scan: the exposed SMTP value is absent from the working tree; because it was previously tracked, history cleanup and credential rotation remain required.
