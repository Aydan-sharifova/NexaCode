# Full Project Audit

Audit date: 2026-08-05. Scope: static source review and local baseline verification. Statuses marked **Untested** require PostgreSQL/Redis and browser validation; they are not claims of production readiness.

## Repository map

| Area | Location | Responsibility |
|---|---|---|
| Domain | src/Coding.Domain | Entities, enums and core rules |
| Application | src/Coding.Application | CQRS contracts, validation and abstractions |
| Infrastructure | src/Coding.Infrastructure | EF Core/PostgreSQL, auth, projects, files, chat, AI and handlers |
| API | src/Coding.Api | HTTP controllers, middleware, composition, health and SignalR |
| Web | frontend | React/Vite UI, query layer, Monaco and CRDT client |
| Tests | tests, frontend tests | xUnit, Testcontainers, Vitest and Playwright |
| Delivery | Compose files, Dockerfiles, scripts/dev-local.sh | Local/demo deployment |

Startup projects are Coding.Api and the Vite client. Requests follow React API client to controllers to MediatR handlers to AppDbContext. Realtime uses /hubs/collaboration, a presence tracker and stale-connection cleanup. AI is implemented in Infrastructure. No dedicated execution worker/service was found.

## Build status

| Check | Status | Evidence / action |
|---|---|---|
| dotnet restore Coding.sln | Blocked | Did not finish in the local command window; rerun where NuGet access is available. |
| dotnet build --no-restore | Partial | Domain compilation started but reports widespread nullable-model warnings. Re-run after restore for final solution result. |
| dotnet test | Untested | Needs successful restore; integration tests also need Docker/Testcontainers. |
| Frontend lint | Complete | tsc -b --pretty false passed. |
| Frontend typecheck | Fixed | Required command was missing; added npm run typecheck. |
| Frontend tests | Complete with warnings | 4 files / 11 tests pass; Zustand warns because test storage is unavailable. |
| Frontend build | Complete with warnings | Vite build passes; LightningCSS warns about Tailwind at-rules and Monaco chunks exceed 500 kB. |
| Compose/migrations | Untested | Requires Docker, configuration values and PostgreSQL. |

## Feature status matrix

| Module | Frontend | Backend | Database | Authorization | Realtime | Testing | Overall | Main problem |
|---|---|---|---|---|---|---|---|---|
| Authentication, registration, refresh, logout | Complete | Complete | Complete | Partial | N/A | Partial | Partially working | Live rotation/401/403 validation required |
| Profile, avatar, password, sessions | Partial | Partial | Complete | Partial | N/A | Untested | Partially working | Email/storage end-to-end checks needed |
| Project CRUD, roles, invitations | Complete | Complete | Complete | Partial | N/A | Partial | Partially working | Ownership, expiry and IDOR tests missing |
| File explorer, versions, autosave | Complete | Complete | Complete | Partial | Partial | Partial | Partially working | Transaction/conflict E2E tests needed |
| Monaco tabs/editor | Complete | N/A | N/A | N/A | Partial | Partial | Partially working | Bundle/model lifecycle needs profiling |
| CRDT, cursors, presence | Complete | Complete | Complete | Partial | Complete | Partial | Partially working | Two-client reconnect/offline proof missing |
| Chat, private chat, notifications | Complete | Complete | Complete | Partial | Complete | Partial | Partially working | Pagination and duplicate-delivery testing needed |
| Kanban/comments | Complete | Complete | Complete | Partial | N/A | Untested | Partially working | DnD rollback/order tests missing |
| Search, analytics, activity | Complete | Complete | Complete | Partial | N/A | Untested | Partially working | Authorization and real-data checks needed |
| Admin/settings | Complete | Complete | Complete | Partial | N/A | Untested | Partially working | Admin/session regression tests missing |
| AI chat, agent, approvals | Complete | Complete | Complete | Partial | N/A | Partial | Partially working | Provider/sandbox configuration not exercised |
| Code execution/terminal | Partial | Missing | Missing | Missing | Missing | Untested | Missing | No isolated execution worker found |
| Demo environment | Complete | Complete | Complete | Complete | N/A | Partial | Partially working | Needs Compose smoke test |

## Interaction and contract audit

The reviewed production pages wire primary controls to API clients for projects, editor save/restore, chat, Kanban, settings, admin, notifications and demo. No production href=# or empty callback was found. Menus, drag-and-drop, keyboard interactions and failed-request behavior still require Playwright/browser validation.

| Priority | Surface | Finding | Root cause / action |
|---|---|---|---|
| P2 | Verification | Required npm run typecheck command absent | Added explicit script |
| P1 | Code execution | Product surface has no identifiable isolated runtime | Implement queued non-root sandbox before exposing it |
| P3 | API errors | Central Problem Details parser and single refresh retry exist | Add malformed/validation/error-retry tests |
| P3 | Production bundles | Monaco/editor chunks exceed warning limit | Profile and lazy-load languages/workers/chart code |

No confirmed source-only client/API route mismatch was found. The API client consistently prefixes /api, uses credentials/token authorization and limits refresh retries to one. Generate or test contracts against OpenAPI to validate every DTO.

## Security, database and realtime audit

Positive controls: centralized exception handling, rate limiting, CORS policy, auth before authorization, demo guard middleware, security headers, hashed refresh-token flow, project handlers and a collaboration hub. Migrations show indexes for membership, notifications, conversations, tasks, files and invitations.

1. **P1** — Prove membership authorization for every project controller and SignalR group with negative IDOR integration tests.
2. **P1** — Do not enable execution until a separate worker enforces non-root/no-network execution, CPU/memory/PID/output limits and cleanup.
3. **P2** — Apply migrations to fresh PostgreSQL; inspect delete behavior, unique sibling/project-member constraints, UTC handling and rollback safety.
4. **P2** — Test invitation token use, refresh-token reuse/rotation, private conversation access and production CORS/JWT/forwarded-header settings.
5. **P3** — Fix widespread nullable entity properties and Message.IsDeleted hiding Base.IsDeleted; that risks divergent soft-delete state.
6. **P3** — Add memory storage to Vitest setup and validate SignalR rejoin/listener cleanup/two-tab presence.

## UX and performance audit

Reviewed major pages contain loading, empty or error primitives. Validate focus return/trapping, mobile dialogs, keyboard DnD, labels and reduced-motion through browser testing. Profile no-tracking/projection usage and populated-database queries before claiming N+1 safety.

## Priority plan

1. **P0**: complete restore/build/test, Compose and fresh-migration smoke test in an environment with NuGet, Docker and PostgreSQL.
2. **P1**: implement or explicitly disable secure execution; add negative project/SignalR authorization tests.
3. **P2**: fix model warnings and validate auth, invitations, files, Kanban, chat and AI critical paths.
4. **P3**: remove test-storage/Tailwind warnings and split heavy editor/analytics bundles.

## Verification commands

    dotnet restore Coding.sln
    dotnet build Coding.sln --no-restore
    dotnet test Coding.sln --no-build
    npm --prefix frontend ci
    npm --prefix frontend run lint
    npm --prefix frontend run typecheck
    npm --prefix frontend run test
    npm --prefix frontend run build
    docker compose config
    docker compose build
