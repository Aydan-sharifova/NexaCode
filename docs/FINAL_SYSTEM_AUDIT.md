# Final System Audit

Audit date: 2026-08-12  
Repository: `/Users/aydansrifova/Desktop/Coding`  
Baseline branch: `fix/full-project-stabilization`

## Scope and safety

This audit follows the full-stack stabilization brief. Docker cleanup is limited to resources proven to belong to this repository. No database volume may be removed until its contents have been evaluated and any useful data has been backed up.

The requested `fix/final-system-stabilization` branch was not created at discovery time because the repository was already on `fix/full-project-stabilization` with substantial pre-existing work. The existing work must be preserved before any branch transition.

## Repository discovery

- Backend: .NET 8 solution with `Coding.Api`, `Coding.Application`, `Coding.Domain`, and `Coding.Infrastructure`.
- Tests: `Coding.UnitTests` and `Coding.IntegrationTests`.
- Frontend: React/TypeScript/Vite application under `frontend`.
- Showcase: Vinext/React application under `showcase`.
- Containers: development, production, and demo Compose definitions; API and frontend Dockerfiles.
- Infrastructure: PostgreSQL, Redis, SignalR, EF Core migrations, JWT authentication, SMTP, AI-provider abstractions, and project collaboration modules are present.

## Baseline results (before audit fixes)

| Check | Result | Evidence |
|---|---|---|
| .NET SDK | PASS | SDK 8.0.420 on macOS ARM64 |
| `dotnet restore Coding.sln` | PASS | All projects up to date |
| Backend Release build | FAIL | Initial parallel build stalled; serialized build reported a truncated generated `Coding.Application` reference assembly/MVID read failure and was cancelled after 4m46s |
| Frontend `npm ci` | PASS WITH FINDINGS | 390 packages installed; audit reports 2 high-severity findings |
| Frontend lint/typecheck | PASS | `tsc -b --pretty false` |
| Frontend unit tests | PASS | 4 files, 11 tests |
| Showcase `npm ci` | PASS WITH FINDINGS | 668 packages installed; audit reports 21 findings, including 14 high |
| Showcase lint | PASS WITH WARNING | Unused `Sparkles` import in duplicate `components/Hero 2.tsx` |
| Showcase tests | PASS | 1 file, 2 tests |
| Docker Compose config | FAIL (P0) | Required `JWT_KEY` is absent; `.env` contains the different name `JWT__KEY` |
| Redis Compose config | FAIL (P0) | Required `REDIS_PASSWORD` is absent |
| Docker services | FAIL | No healthy project stack; three project API containers are exited with code 139 and a legacy `postgres-db` container is stopped |

## Initial defects and risks

### P0

1. Development Compose cannot interpolate required secrets, so the stack cannot start.
2. No project service is healthy; API containers previously exited with code 139.
3. Generated backend build artifacts are inconsistent/truncated after interrupted or concurrent builds.

### P1

1. Core end-to-end flows cannot yet be verified because the Docker/database/API baseline is unavailable.
2. The database backup requirement has not yet been satisfied; no project volume will be deleted until evaluated.

### P2

1. Frontend dependency audit reports two high-severity findings.
2. Showcase dependency audit reports 21 findings, including 14 high-severity findings.
3. Node 23.11.0 is outside the engine range declared by `eslint-visitor-keys@5.0.1`.

### P3

1. Duplicate files with ` 2` suffixes exist in documentation, workflows, and showcase source; at least one duplicate source file creates a lint warning.

## Docker resource mapping (initial)

Observed stopped containers:

- `practical_ganguly` — image `coding-api:1.0.0`, exited 139.
- `xenodochial_noether` — image `coding-api:docker-user-fix`, exited 139.
- `peaceful_goodall` — image `coding-api:docker-user-fix`, exited 139.
- `postgres-db` — official `postgres` image, exited 0; ownership/data usefulness still requires verification.

No Docker resources have been deleted during discovery.

## Next verification actions

1. Repair development environment-variable names without committing secrets.
2. clean generated .NET build artifacts and repeat Release build/tests.
3. Map Docker labels, volumes, networks, and database contents.
4. Back up useful PostgreSQL data before project-specific reset.
5. Bring up a clean stack and verify migrations, health, logs, and HTTP flows.
6. Execute the feature matrix and focused browser/API end-to-end scenarios.

## Stabilization results

- Added `icu-libs` to the Alpine API runtime. This resolves the proven exit-139 startup failure caused by `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` without ICU.
- Reduced the API/frontend Docker build context by excluding showcase, docs, tests, and workflow files that are not inputs to either image.
- Removed identical duplicate frontend source files and the obsolete duplicate showcase Hero component.
- Migrated a fresh PostgreSQL 16 database successfully: 14 migrations, 46 public tables.
- Verified all four Compose services healthy: frontend, API, PostgreSQL, Redis.
- Corrected Settings so it exposes the short Public ID instead of the internal user GUID.
- Corrected direct-chat creation so the public API accepts Public ID/email rather than requiring an internal GUID.
- Corrected an EF Core translation failure in direct-conversation loading by filtering participants before projecting the complex conversation DTO.
- Verified two-user direct conversation creation, message persistence, recipient retrieval, duplicate-conversation reuse, and self-chat rejection.
- Backend Release build passes with zero warnings/errors; 146 unit tests and 1 integration test pass.
- Frontend typecheck, 11 unit tests, and production build pass. Showcase lint/tests/build pass after duplicate removal.

## Remaining verified gaps

- A dedicated current-user endpoint such as `/api/users/me` is absent; callers currently obtain identity from login/refresh and full profile data through `/api/settings`.
- SignalR delivery was not proven with two live browser sessions during this run; persistence and publisher invocation paths are present.
- EF Core reports five global-query-filter/required-navigation model warnings and one multi-collection projection performance warning.
- Data Protection keys are ephemeral inside the API container.
- SMTP and external AI are intentionally disabled/unconfigured in the local environment.
- The broad feature set beyond the focused P0/P1 smoke flows remains only partially verified and must not be represented as fully production-ready.
