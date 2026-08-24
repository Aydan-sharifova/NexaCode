# CODING 2.0 implementation audit

Audit date: 2026-08-22. This is a living, evidence-based implementation record. `Present` means source exists; it does not imply production verification.

## Architecture retained

- .NET 8 clean-style backend: Domain, Application, Infrastructure and API projects.
- React/TypeScript/Vite frontend with Monaco, TanStack Query and feature modules.
- PostgreSQL/EF Core migrations, Redis, SignalR, JWT/refresh-token authentication and role/policy authorization.
- Docker/Compose delivery, health checks and separate migration command.
- Ollama through an OpenAI-compatible provider; no browser-exposed provider key.

The repository already contains substantial, overlapping user work. Stabilization is incremental; unrelated dirty-tree changes are preserved.

## Verified baseline

| Check | Result |
|---|---|
| Backend solution build | PASS, 0 warnings/errors |
| Backend unit tests | PASS, 282 tests |
| Backend integration tests | 3 skipped when Docker/PostgreSQL testcontainers are unavailable; equivalent local branch round-trip smoke passed |
| Frontend TypeScript | PASS |
| Frontend unit tests | PASS, 34 tests |
| Frontend production build | PASS; existing CSS at-rule and large Monaco chunk warnings remain |

## Capability inventory

| Area | State | Evidence / next boundary |
|---|---|---|
| Authentication and lifecycle | Present | JWT refresh, verification, password flows, timed ban/user status; negative E2E coverage remains |
| Developer identity/social graph | Partial | Public profiles, follows, two-way blocking, persistent feed posts/comments/replies/likes/saves/shares, cursor pagination, transparent feed filters, notifications, audit logging and verified achievement-based reputation exist; richer moderation remains |
| Projects/workspace/editor | Present | Owner/Admin/Maintainer/Developer/Viewer roles, deadline lifecycle, server-enforced Viewer/expired-Developer read-only boundaries, projects, membership, explorer, Monaco, versions and collaboration exist; browser E2E remains |
| Git and code review | Partial | Native Git status/stage/commit/branches/history/diff plus protected-branch pull requests, review gates and real merges are integrated. Checkout transactionally imports branch text/binary snapshots into the database workspace, and exact materialization records deletes/renames; browser E2E remains |
| Collaboration/chat | Present | SignalR, CRDT, presence and messaging exist; multi-session resilience needs E2E proof |
| AI assistant/agent | Partial | Ollama chat, tool registry, approvals, a privacy-safe evidence-based Personal Mentor, an approval-gated Project Planner and evidence-backed project impact reports exist; several agent workflows need end-to-end completion |
| Project knowledge graph | Partial | Versioned dependency snapshots, bounded language-aware indexing, interactive graph exploration and impact analysis across files/symbols/APIs/tables/components/tests are integrated; deeper compiler-grade language adapters remain |
| AI debugging timeline | Partial | Failed execution incidents, successful-run baselines, file versions, exact-path Git changes, logs/tests, evidence confidence and optional Ollama root-cause reports are integrated; production worker telemetry remains pending |
| Autonomous test agent | Partial | Persistent bounded C# analyze/generate/run/failure/fix/re-run workflow, isolated execution evidence and explicit concurrency-checked apply are integrated; additional language runners and production worker deployment remain |
| Screenshot to code | Partial | Local Ollama vision upload, evidence analysis, bounded React/TypeScript/CSS generation, CSP-isolated preview, review diff and concurrency-checked explicit apply are integrated; additional framework targets and browser visual-regression scoring remain |
| AI UI generator | Partial | Prompt-to-multi-file React generation, page/component/routing/visual-system boundaries, explicit sample-data approval, CSP preview, diff and atomic concurrency-checked database apply are integrated; broader frameworks and visual regression remain |
| Runtime/live preview | Partial | Browser HTML/CSS/JS preview and bounded local Docker C# runner exist; production queue/worker is intentionally disabled |
| Voice coding | Partial | Typed commands and optional browser speech recognition dispatch bounded file-open, AI explain/fix, autonomous test and branch actions; risky actions require confirmation |
| Database explorer | Partial | Schema metadata and project blueprint exist; query editor/migration workflow needs completion |
| Admin/analytics | Partial | Existing admin and analytics surfaces need authorization and populated-data E2E checks |
| Marketplace | Partial | Versioned six-category catalog, publish/like/save, strict manifest validation, explicit dangerous-permission approval and project-scoped AI-agent installations are integrated; sandbox runtimes for templates/themes/plugins remain intentionally unavailable |
| Live rooms | Partial | Persisted private/project rooms, modes, challenges, invitations, roles, lifecycle/timer state, chat and SignalR fan-out are integrated with the existing synchronized project workspace; embedded room reactions/tasks/notes and full two-browser E2E remain |
| Learning/achievements | Partial | Eleven server-evaluated achievements, immutable evidence, reputation levels, public/private visibility, declared skills/learning topics, developer journey and Personal Mentor UI are integrated; structured learning tracks and verified skill assessments remain |
| Developer portfolio/reputation | Partial | Bio, skills, experience, links, public projects/posts/snippets, privacy-safe contribution data, followers/following, verified reputation and journey timeline are integrated; richer employment history and external contribution verification remain |
| Discover | Partial | Dedicated backend-driven developers/projects/snippets/templates/agents/themes catalog with search, technology, language, popularity, recent and trending filters is integrated; larger-scale search indexing remains |
| Saved content | Partial | Consolidated `/saved` library covers posts, public projects, snippets, project templates and AI agents with backend type/search filters; bulk organization remains |
| Advanced notifications | Partial | Unified persisted/realtime notification types cover social, PR/review, invite, role, ban, deadline, deployment, achievement and autonomous AI tasks with database-backed deduplication; generic agent-run completion awaits its unfinished orchestrator |
| Analytics | Partial | Range-scoped developer and membership-protected project analytics use real Git/PR/review/deployment/social/activity/save/view evidence; repository forks remain explicitly unavailable until a real fork workflow exists |

## Critical security decisions

1. Submitted server code is never executed directly on the API host.
2. The local C# runtime uses a fixed Docker invocation with no network, bounded resources/time/output, dropped capabilities and a server-created temporary mount.
3. Production execution stays disabled until a separate queue and isolated worker host are implemented and abuse-tested.
4. AI provider credentials stay server-side; Ollama is the default provider.
5. Every new project resource must enforce membership/role checks server-side and receive negative authorization tests.
6. Existing numeric project role value `2` is intentionally retained as `Developer`; this upgrades legacy `Member` rows without accidental privilege escalation.

## Security hardening verification

- Refresh-token and logout mutations now reject untrusted browser origins with `403`; cross-site refresh cookies are emitted only for explicitly configured origins. Same-origin and non-browser clients retain the normal authentication flow.
- Screenshot-to-code verifies PNG/JPEG/WebP bytes instead of trusting the declared media type. Avatar and workspace image uploads retain their independent extension, size and signature checks.
- Avatar, chat-attachment and workspace uploads share a per-user/IP token-bucket limiter in addition to request/file size limits.
- Native Git paths are canonicalized beneath the configured repository root, repository-relative paths reject traversal and `.git`, and process arguments use `ProcessStartInfo.ArgumentList` without shell execution. Runtime execution retains the network-disabled, capability-dropped Docker boundary.
- Static review found no controller without an explicit authorization/anonymous boundary, no frontend raw-HTML/eval sink, and no committed production provider/JWT/SMTP secret. The local `.env` remains ignored.
- Backend build passes with zero warnings/errors and 282/282 unit tests pass. Negative API smoke returned `403` for hostile-origin refresh/logout and preserved `401` for same-origin refresh without a valid cookie. Broader endpoint-by-endpoint IDOR and multi-browser abuse testing remains required before a complete security claim.

## Prioritized implementation queue

1. Stabilize runtime UX/error contracts and introduce a persistent execution-job model plus dedicated worker boundary.
2. Complete critical auth/project/file/collaboration/AI browser and negative authorization tests.
3. Complete social blocking/moderation and feed realtime fan-out, then add negative authorization/performance tests.
4. Add browser E2E and multi-session concurrency stress around branch checkout and the PR/review engine.
5. Build marketplace/agent installation with versioning, scopes, audit logs and explicit approvals.
6. Add live rooms, learning tracks and achievements only after the core collaboration/security baseline is proven.

## Social feed verification

- Migration `20260822184600_AddSocialFeed` applied successfully to the local PostgreSQL database without resetting existing data.
- API smoke: feed tabs and saved list return 200; create 201; like/save 200; comment 201; comment/post cleanup 204.
- EF translation failure discovered by the first smoke run was corrected and the same list calls then passed.
- Frontend typecheck, 31 tests and production build pass with the lazy-loaded Feed page included.
- Empty authenticated feeds now render honest empty states; the former client-side sample-post substitution was removed so production success with zero rows can never display fabricated activity.
- Discover exposes visible developers, public projects and code topics using documented follower/post, feed-save and visible-topic counts. It explicitly states that no ML ranker is used.
- Replies are now actionable in the feed UI and retain their verified parent-comment relationship. Mentions, blocking-aware notifications, likes, saves, follows, shares and cursor pagination remain server-backed.
- `Achievement` and `Deployment` can no longer be submitted as user claims. Their public create path is denied in both validation and the handler; verified achievement evaluation publishes evidence-backed achievement posts and emits a deployment post only when a real `DeploymentSucceeded` activity unlocks the deployment achievement.
- Current verification: backend build has zero warnings/errors and 263/263 unit tests pass; frontend TypeScript, 31 tests and production build pass with only the existing CSS/chunk warnings.

## Social blocking and project-role verification

- Migration `20260822190721_AddUserBlocking` was applied without resetting existing data.
- Two-user API smoke verified block/unblock, profile/search/feed filtering, follow denial and direct-message denial; temporary smoke data was removed afterwards.
- Blocked direct conversations and their unread counts are filtered server-side.
- Project roles now match Owner, Admin, Maintainer, Developer and Viewer. Viewer file/runtime mutations are rejected on the backend and the workspace renders read-only controls.

## Project deadline verification

- Migration `20260822192446_AddProjectDeadlineLifecycle` was applied to local PostgreSQL; existing projects default to `Active` rather than being incorrectly downgraded to `Draft`.
- A background lifecycle monitor persists `DeadlineSoon` and `DeadlineExpired` transitions and emits activity/audit records plus member notifications.
- Real API smoke verified an expired Developer receives read-only project details and `403` on file mutation, a non-SuperAdmin receives `403` on extension, SuperAdmin can extend to a strictly later future date, and Developer writes resume immediately (`201`) after extension.
- Temporary users/projects/activity records created by the smoke test were removed and the cleanup count was verified as zero.
- Redis provider URLs are normalized to StackExchange.Redis configuration syntax. Local health remains intentionally unhealthy while the configured remote/private Redis host is unreachable; the prior malformed `redis://host:port:port` endpoint is fixed and covered by unit tests.

## Pull request and merge-protection verification

- Migration `20260822194332_AddPullRequestReviewEngine` was applied to the local PostgreSQL database without resetting existing data.
- Protected-branch PRs support persisted reviews, revision-bound approvals, change requests, inline and blocking comments, comment resolution, optional test attestation, conflict checks, close and real native-Git merge operations.
- Authors cannot review their own PR. Review, test-report and merge operations require Maintainer or higher; policy changes require Owner/Admin. Direct commits to a protected branch are rejected after its bootstrap commit.
- The frontend includes a responsive PR list/detail/diff/review surface, workspace/project entry points, merge-policy controls, cache invalidation and notification routing.
- Native Git merge/conflict/snapshot tests and pure merge-gate policy tests pass.
- Branch checkout refuses dirty worktrees, reads bounded Git snapshots without mutating the selected branch, transactionally imports folders/files into `WorkspaceNode` state, preserves common node identity, versions changed content, soft-deletes absent nodes, and restores the prior branch if database import fails.
- Migration `20260822202620_PreserveBinaryWorkspaceContent` idempotently stores binary current content and versions as PostgreSQL `bytea`. Upload, raw download, restore, exact Git materialization and branch import preserve binary bytes; legacy binary worktree files are lazily backfilled before exact materialization.
- Real local API round-trip created and committed `main`/feature revisions, switched both directions, verified text and feature-only file isolation, verified four binary bytes and metadata after restoration, then removed the temporary project/repository.
- A singleton per-project repository coordinator now serializes checkout, branch creation, commits, staging, text/binary saves, file-tree mutations, raw reads and native merges while allowing unrelated projects to proceed concurrently. Unit tests cover serialization, cross-project independence, cancellation and merge waiting.
- A parallel API smoke started a file save and branch checkout together: save completed, checkout waited and then returned `409` for the newly dirty worktree, with the feature branch and saved database/Git content intact.
- Playwright now covers the PR review surface from protected merge gate through Maintainer approval to merge. The full browser suite passes its anonymous auth navigation and PR workflow; credential-dependent login/logout remains explicitly skipped when E2E credentials are not supplied.

## Marketplace verification

- Migration `20260822202453_AddMarketplaceCatalog` adds versioned items, installations, likes and saves and was applied to local PostgreSQL without resetting existing data.
- The catalog supports Project Templates, Themes, Plugins, AI Agents, Snippets and Components. Every publish passes category-specific manifest validation, semantic version validation and a SHA-256 content/permission checksum.
- Plugin manifests cannot embed executable frontend code; theme values reject URL/script payloads; template paths and likely secrets are rejected; AI agents can request only registered tools.
- Only AI Agent packages currently expose installation because that path has project Owner/Admin authorization and explicit approval for every requested dangerous permission. Other categories are honestly shown as catalog-only until dedicated sandbox/application runtimes exist.
- Backend build passes with zero warnings/errors; marketplace policy coverage is included in the current 222 unit tests, while 31 frontend tests, TypeScript checking and the production frontend build pass.

## Live coding room verification

- Migration `20260822203511_AddLiveCodingRooms` was applied to local PostgreSQL and adds rooms, participants and persistent room messages.
- Migration `20260823072208_AddLiveRoomWorkshopTools` adds persistent workshop tasks, allowlisted live reactions, and private interviewer notes. Task/reaction changes are broadcast through SignalR; interviewer notes are never broadcast and are API-restricted to Owner/Host/Interviewer roles in interview rooms.
- No passive or secret recording was added. The platform stores only explicit chat, tasks, and staff-authored interview notes.
- Interview, workshop, pair-programming and community-event modes use explicit Owner/Host/Interviewer/Candidate/Participant roles. Invite-only rooms deny uninvited users; project-member visibility still requires real project membership.
- Room state uses an expected state version and only permits Scheduled→Active/Cancelled and Active→Completed/Cancelled transitions. Timer clients derive elapsed time from the server start timestamp.
- SignalR groups are authorized independently and publish room-state, participant and message changes; reconnect rejoins the active room.
- A two-user API smoke proved uninvited access `403`, invitation, candidate join, persisted chat `201`, start version 1, complete version 2 and cleanup. The first smoke exposed an EF entity-state bug in invitation insertion; it was fixed and the exact failed flow then passed.
- Project-backed rooms link to the existing CRDT workspace, where synchronized editing, cursors, Ollama assistance and sandboxed Run/tests already operate. Standalone rooms do not claim an editor/runtime they do not possess.
- Backend unit tests now pass 268/268. Frontend TypeScript, 31 tests and production build pass; the existing Tailwind at-rule and large Monaco chunk warnings remain.

## Content moderation verification

- Reports cover posts, comments, public projects, code snippets and public profiles. Target existence, visibility, two-way blocking, ownership and duplicate-open-report boundaries are enforced server-side.
- The explicit lifecycle is Pending → Reviewing → ActionTaken/Dismissed, with a controlled return to Pending. Enforcement cannot skip review and terminal reports cannot be reopened through the public API.
- Moderator, Admin and SuperAdmin roles can access the queue. Every transition creates both an immutable per-report action record and a platform ActivityLog entry; removal is soft-delete and profile enforcement uses suspension.
- Feed comments/posts/snippets, public projects and public profiles expose report controls. The moderator UI provides filters, evidence context, required notes and the full action history.
- Migration `20260823073328_AddContentModeration` and the follow-up open-report uniqueness constraint were applied locally. Policy tests are included in the current 278-test unit baseline; frontend retains 31 passing tests.

## Voice coding verification

- The workspace accepts the documented voice intents through optional browser speech recognition and an always-available typed fallback. Unsupported phrases remain unrecognized instead of being guessed as executable commands.
- `Open <file>` resolves an exact workspace file or asks for the full path when names are ambiguous. Explain and fix-error intents reuse the existing Ollama assistant and never apply generated code automatically.
- `Run tests` and `Create a branch called <name>` are visibly classified as risky and require an explicit second confirmation. Test execution starts the existing bounded autonomous-test workflow with one iteration and no automatic fix application; branch creation uses the authenticated repository API.
- Read-only project members cannot invoke test or branch mutations. Speech text is always previewed before dispatch, and browsers without the Web Speech API retain complete typed-command functionality.
- Intent parser coverage includes every supported example, trailing branch punctuation and rejection of an unsupported destructive phrase. Frontend verification passes 34/34 tests and the production build; existing CSS at-rule and large editor chunk warnings remain non-blocking.

## Discover verification

- `/discover` now exposes developers, public projects, visible code snippets, project templates, AI agents and themes from server-backed data; no sample catalog is substituted for empty results.
- Search, technology, language and Trending/Popularity/Recent controls are sent to the backend. Two-way blocking is enforced for developers, project owners, snippet authors and marketplace authors.
- Marketplace JSON tags use PostgreSQL JSON containment rather than invalid text matching. Authenticated API smoke exercised all filters and all six result collections with `200`; the temporary smoke account was deleted.
- Backend and frontend production builds pass, alongside 278/278 backend and 34/34 frontend tests. Moderator routing was also corrected so the frontend role gate matches the backend Moderator/Admin/SuperAdmin policy.

## Saved content verification

- `/saved` consolidates the five required content types with backend type and text filters. Existing post/snippet and marketplace save records are reused; public project saves use a dedicated composite-key table.
- Project save/un-save validates current public visibility and enforces two-way blocking server-side. Saved reads also filter blocked authors and unpublished/private/deleted content.
- Migration `20260823080601_AddSavedProjects` was applied locally and EF reports no pending model changes. Authenticated API smoke returned `200` for All and every filter; project save and remove both returned `200`; the temporary account was deleted.
- Backend/frontend builds and 278/278 backend plus 34/34 frontend tests pass.

## Advanced notifications verification

- The shared enum/frontend contract now includes Deployment, Achievement, Ban, AI Task and Agent Completion alongside existing follow/like/comment/mention/PR/review/invite/role/deadline events.
- Every notification receives a deterministic 64-character deduplication key. A partial unique database index prevents concurrent duplicate delivery; single and batch service paths reuse existing rows on retry and emit SignalR events only for new rows.
- Administrator suspend/restore, verified deployment evidence and completed autonomous-test runs now publish their corresponding real notifications. No synthetic generic agent-completion producer was added because the generic agent orchestrator is not yet implemented.
- Migration `20260823081151_AddNotificationDeduplication` is applied with no pending model changes. Backend build passes with zero warnings and 280/280 tests; frontend retains 34/34 tests and a passing production build.

## Analytics verification

- Developer analytics now reports commits, PRs, reviews, deployments, projects, contributions, followers, posts and snippets for the selected period from persisted evidence.
- Project analytics reports unique viewer/day views, project-share likes, project saves, contributors, verified deployments and activity. Private projects are included only through current membership; public detail access enforces blocking before recording a view.
- Fork count is visibly marked unavailable instead of fabricating a zero-as-success metric because a real repository fork workflow does not yet exist.
- Migration `20260823081919_AddProjectViewAnalytics` is applied. Authenticated smoke returned `200` for analytics and public view tracking, exposed all nine developer metrics and no inaccessible private project. The temporary account was deleted.
- Backend/frontend builds, 280/280 backend tests and 34/34 frontend tests pass.

## Verified achievements and reputation

- Migration `20260822214725_AddVerifiedAchievements` was applied without resetting existing data. It adds a fixed achievement catalog and immutable per-user awards with a unique user/achievement boundary and JSON evidence.
- Eleven achievements cover first project/commit/PR/merge/deployment/follower, ten followers, sustained community contribution, bug hunting, AI building and public open-source contribution. Deployment remains correctly locked until a real successful deployment activity exists.
- Eligibility is evaluated after relevant successful commands and by an idempotent background backfill. Read endpoints never mutate achievement state, likes do not inflate reputation, and every awarded point requires a verified evidence record.
- Public profile visibility and blocking rules also protect achievement profiles and developer journeys. The frontend exposes the full catalog, progress, verified evidence state, contribution level and a chronological journey.
- Real API/database smoke returned all 11 catalog entries, awarded `first-project` with `verified=true` from a persisted owned project, returned no false award to a user without a project, and rejected anonymous access with `401`.
- Achievement policy tests bring the backend suite to 230/230. Frontend TypeScript, 31 tests and production build pass.
- The public profile now operates as a consolidated developer portfolio: experience/role/location and verified links, public projects, authored posts, reusable code snippets, followers/following, contribution aggregates, public activity and the verified journey are visible in one responsive surface.
- Portfolio contribution counts are derived only from real public-project commits, merged pull requests, accepted reviews, public projects, saved code snippets, successful deployment activities, public community participation and verified achievement rows. Likes are displayed on posts but never contribute directly to reputation.
- `IsProfilePublic`, `IsActivityPublic`, `AreFollowersPublic` and two-way blocking are enforced in the portfolio handler. Detailed activity/contribution data disappears when the owner makes activity private; private-project names and activity are not projected.

## AI Personal Mentor verification

- Migration `20260823041719_AddMentorLearningEvidence` adds bounded declared skills and learning topics to developer profiles without replacing existing profile data.
- The mentor analyzes only projects the current user owns or belongs to, declared skills/topics, aggregate completed assigned tasks, authored commits, project languages and bounded workspace filename patterns. Repository content and another user's inaccessible activity are not sent to the model.
- Five required recommendation categories are always produced from visible evidence: next technology, project idea, missing skill, testing improvement and architecture topic. Recommendations explicitly state their rationale and next action.
- Optional narrative generation uses the configured Ollama provider. A real request to `qwen2.5-coder:1.5b` returned a model narrative and all five recommendations. If Ollama is unavailable, the UI shows the evidence engine results and honestly reports that no model narrative was generated.
- The model system boundary forbids sensitive personal-attribute inference and invention. The API is authenticated and AI generation uses the existing AI rate limiter.
- Real API smoke returned `200`, three authorized projects, observed C#/Java evidence, test-file counts and five categories; anonymous access returned `401`.
- Backend build passes with zero warnings/errors and 233/233 unit tests pass. Frontend TypeScript, 31 tests and production build pass; existing CSS at-rule and Monaco chunk-size warnings remain.

## AI Project Planner verification

- Migration `20260823042939_AddApprovedProjectPlanner` adds versioned/hash-protected plan drafts plus real project milestones, issues and task-to-issue links.
- Ollama produces bounded structured JSON for Architecture, Database, API, Frontend, Authentication, Testing and Deployment. Server validation requires all seven sections, an active catalog language, 1-8 milestones, bounded issues/tasks and at most 100 tasks; invalid model output is retried once and is never persisted as a usable plan.
- Generation has no side effects beyond the private plan draft. Draft→Approved/Rejected uses expected versions and an atomic conditional database update. Apply requires a separate Approved state, the latest version, explicit bulk-creation confirmation, a SHA-256 integrity check and a PostgreSQL row lock to prevent concurrent double application.
- Applying a reviewed plan creates a private project, owner membership, project conversation, native Git repository, milestones, issues and linked Kanban tasks in one database transaction around repository initialization. Project visibility can be changed later through existing project settings.
- The responsive planner UI exposes draft history, every section, milestone/issue/task expansion, priorities and distinct Reject, Approve and Create actions. It explicitly states that generation creates no project.
- Real Ollama/API smoke generated a six-milestone/eight-issue/eight-task TypeScript plan. Draft apply returned `409`, missing confirmation `400`, stale approval `409`; approval moved version 1→2 and apply moved it to Applied version 3 with all eight tasks linked to issues.
- Temporary smoke database rows were deleted. The exact temporary native repository was moved to macOS Trash and remains recoverable as `coding-project-planner-smoke-c0e849f8d0cd4a17ab8f031360d9a5aa`.
- Backend build passes with zero warnings/errors and 236/236 unit tests pass. Frontend TypeScript, 31 tests and production build pass; existing CSS at-rule and Monaco chunk-size warnings remain.

## Project knowledge graph and impact verification

- Migrations `20260823045141_AddProjectKnowledgeGraph` and `20260823045644_EnforceSingleCurrentKnowledgeGraphSnapshot` add append-only graph snapshots and enforce exactly one current snapshot per project at the database boundary.
- The indexer recognizes files, imports, classes, interfaces, methods, controllers, services, API endpoints, database tables, components and tests. It records evidence-bearing containment, import, use, call, exposure, persistence and test edges.
- Indexing is serialized with repository writes, skips binary files, validates parent cycles and enforces explicit file, character, node and edge limits. A deterministic repository fingerprint makes unchanged re-indexing idempotent.
- Project membership protects graph reads and impact analysis; indexing requires repository-write permission. Anonymous access returned `401` and a non-member returned `403` in real API smoke tests.
- Impact analysis traverses dependencies, reverse dependencies and the bounded symbol neighborhood to list affected files, services, APIs, tests, database tables and components. Optional Ollama reports receive graph evidence and aggregates only, never repository source.
- Real API smoke indexed an existing project into a 10-node/eight-edge graph, returned the same snapshot/version on an unchanged second index, produced dependency impact, and generated a non-empty report with `qwen2.5-coder:1.5b`.
- Backend build passes with zero warnings/errors and 238/238 unit tests pass, including backend/API/table/test and frontend import/component extraction coverage. Frontend TypeScript, 31 tests and production build pass; existing CSS at-rule and large editor chunk warnings remain.

## AI debugging timeline verification

- Migration `20260823052529_AddAiDebuggingTimeline` adds persistent execution observations, failure incidents and immutable fingerprinted evidence without recreating or resetting existing tables.
- Every bounded runtime request records an observation. Failures retain error, stack trace, output, timeout and source-hash context; earlier successful runs provide a temporal baseline when one exists.
- Correlation uses bounded file versions, exact-path native-Git diffs, recent execution/build/test activity and test results. A “started after commit” claim is emitted only when the same file succeeded before a touching commit and failed after it; otherwise the relevant commit and regression confidence remain unset.
- Project membership protects timeline reads and analysis. AI analysis uses the existing rate limiter and sends Ollama only the bounded incident/evidence summary, never repository source.
- Real API smoke returned `200` for the member timeline, `401` anonymously, and `200` for deterministic and Ollama analysis. Ollama `qwen2.5-coder:1.5b` produced an evidence-constrained root cause/fix while the absence of a successful baseline correctly produced no commit claim.
- The smoke test exposed an EF child-entity state bug when adding evidence with a preassigned GUID; new evidence is now explicitly inserted and the exact failing request passes. Git correlation is bounded to 10 seconds and Ollama analysis to 60 seconds with honest fallback behavior.
- Backend build passes with zero warnings/errors and 240/240 unit tests pass. Frontend TypeScript, 31 tests and production build pass; existing CSS at-rule and large editor chunk warnings remain.

## Autonomous test agent verification

- Migration `20260823060430_AddAutonomousTestAgent` adds persistent runs and per-iteration execution evidence. Every run records its source hash/concurrency token, hard maximum iteration count, Ollama identity, generated harness, exit status, timeout, output and final evidence state.
- The workflow implements Analyze → Generate Tests → Run → Analyze Failure → Suggest Fix → Optional Apply → Run Again. Requested iterations are clamped to 1–3 server-side, preventing infinite loops independently of the client.
- Ollama returns only a bounded `AutonomousTestRunner` harness. The server appends that harness to the exact current source and selects the dedicated startup object, so the model cannot silently substitute a rewritten source before execution.
- Test programs run only through the existing network-disabled, resource/time/output-bounded Docker runtime. Every execution also enters the AI Debugging Timeline as Test evidence. A pass requires independent exit code 0, no timeout and empty stderr; model text can never set a pass result.
- Applying a proposed fix is a separate explicit-confirmation endpoint. It requires repository-write permission, an `AwaitingApply` run, SHA-256 proposal integrity, and the exact original file hash/concurrency token; the existing file-save path then creates a version and synchronizes Git. Failed runs cannot apply (`409` in real API smoke).
- The responsive Test Agent page exposes compatible C# files, goal and iteration controls, persistent run history, generated harnesses, sandbox output, failure evidence, proposed-source preview, confirmation before apply and a distinct Run Again action.
- Real Ollama/API smoke generated a bounded 312-character harness for a temporary calculator file. The unhealthy local Docker engine caused the isolated run to time out; the API honestly returned `Failed`/`TimedOut` with one completed iteration rather than claiming success. Member access returned `200`, anonymous access `401`, and apply on the failed run returned `409`.
- All temporary smoke runs, execution/debug observations, activity rows and the temporary file were removed by exact identifiers and verified at zero. Backend build passes with zero warnings/errors and 253/253 unit tests pass; frontend TypeScript, 31 tests and production build pass with only the existing CSS/chunk warnings.

## Screenshot to code verification

- Migration `20260823064650_AddScreenshotToCode` adds project-scoped persistent generation drafts without storing uploaded image binaries. The database keeps the image filename/media type, SHA-256 fingerprint, visual analysis, generated files, preview and exact target snapshots.
- The authenticated multipart endpoint accepts only PNG, JPEG and WebP up to 5 MB and requires repository-write permission. The configured local Ollama vision model receives the image and returns bounded marked sections for analysis, `App.tsx`, `styles.css` and a standalone preview; malformed or remote-resource output is rejected rather than exposed as a successful draft.
- Generated previews run in a sandboxed iframe and receive a CSP that denies network connections, forms, base URLs and all resources except embedded styles/scripts and data/blob images. The source image itself is not persisted.
- The review UI presents generated code, live preview and an explicit before/after diff. Existing files are not touched during generation. Apply is a separate confirmed action that rechecks every original concurrency token and then uses the existing versioned/Git-synchronized file commands; changed targets reject the stale draft.
- Migration was applied to the local PostgreSQL database. Anonymous list access returns `401`. Backend build passes with zero warnings/errors and 257/257 unit tests pass, including marker parsing, malformed output, remote-resource rejection and preview CSP coverage. Frontend TypeScript, 31 tests and production build pass with only the existing chunk-size warning.

## AI UI generator verification

- Migration `20260823070118_AddAiUiGenerator` adds project-scoped persistent prompt drafts with the exact generated file set and original target snapshots.
- The generator requires `src/App.tsx`, `src/pages/DashboardPage.tsx`, `src/components/DashboardShell.tsx` and `src/styles.css`, covering routing/composition, page, component and visual-system layers. Ollama output is bounded and must pass structural validation before becoming a reviewable draft.
- Non-production sample data is disabled by default and separately authorized in the UI. The server prompt requires empty/loading-ready states when disabled, and deterministic policy rejects common mock/sample/fake record declarations without that approval.
- Preview uses the same network-denying CSP sandbox as screenshot-to-code. The review surface exposes Preview → Code/Diff → Apply; no generated source is written during generation.
- Apply requires repository-write permission and explicit confirmation, runs under the per-project repository coordinator, verifies original hash/token plus unchanged path identity, and writes all files plus versions inside one serializable database transaction before exact Git worktree synchronization.
- Migration is applied locally; anonymous list and generate calls return `401`. Backend build passes with zero warnings/errors and 261/261 unit tests pass. Frontend TypeScript, 31 tests and production build pass with the existing CSS/chunk warnings only.

## Observability verification

- Serilog emits compact structured JSON. Every request now receives a safe `X-Correlation-ID`; valid caller IDs are propagated and invalid/header-injection values are replaced with a generated trace identifier.
- Completed request events include correlation ID, authenticated user ID (or `anonymous`) and route project ID when present. Headers, bodies, cookies, tokens, provider keys and connection strings are not logged.
- Ollama completion events record provider/model/action, duration and token counters without prompt, repository context or generated content. Isolated runtime events record language, duration, outcome and output length without source/output contents.
- AI tool execution already persists user/project/run/tool/risk/approval/outcome metadata through the activity audit boundary. Verified deployment events are likewise retained as activity evidence.
- Runtime smoke propagated `request_abc-123`, replaced an invalid correlation header, and returned `200` from liveness. Backend build has zero warnings/errors and 282/282 tests pass. External metrics/traces, dashboards and alert delivery remain future production infrastructure work.

## Rate-limiting verification

- Authentication and guest AI are partitioned by client IP; authenticated AI/runtime/approval work, invitations, user/global search, social mutations and uploads use separate policies partitioned by user with IP fallback.
- Upload limits cover avatar, chat attachments and workspace multipart files. Social limits cover posts, replies, comments, reactions, follows and saves through the controller boundary; runtime and autonomous agent creation share the stricter AI bucket.
- SignalR connections use a dedicated concurrency limit. Rejected requests return `429` without entering application handlers.

## Database-design verification

- Existing equivalent entities were retained; no duplicate social, room, AI, marketplace or showcase tables were introduced. The schema uses foreign keys, soft-delete query filters, UTC timestamps, bounded indexed lookup columns and unique constraints for identity/idempotency boundaries.
- Migration `20260823083335_HardenWorkspaceSchema` enables PostgreSQL `citext` for workspace names and adds separate partial unique indexes for active root nodes and active nested siblings. This closes the cross-instance race that application checks and a process-local project lock could not prevent.
- The migration performs a preflight duplicate check and fails with an explicit remediation message instead of deleting or renaming user data. It was applied successfully to the local PostgreSQL database with no pending model changes.
- File contents, live-room state and project-plan approval state are optimistic-concurrency tokens. Unhandled EF concurrency conflicts now return `409` rather than an opaque server error.
- Backend build passes with zero warnings/errors and 282/282 unit tests pass. Production-scale query-plan and load validation remains part of the performance phase.

## Frontend architecture verification

- Existing feature-oriented modules and lazy route boundaries were retained. Shared TanStack Query keys now cover repository views, saved content, discover, notifications, social feed, blocked users and chat without introducing a parallel state layer.
- Public-project save uses an optimistic cache update, disables duplicate submission, rolls back on failure and invalidates the consolidated saved-library prefix on success; the result is visible immediately without a manual refresh.
- Frontend production build and 35/35 tests pass. Remaining legacy pages with local query-key literals or broad manual refetches stay explicitly tracked for incremental migration rather than being claimed complete.

## UI/UX verification

- The retained dark-first surface uses restrained violet/blue/cyan AI accents, dense workspace layouts, shared async/error/empty states, reduced-motion rules and authorization-gated navigation.
- Real browser smoke at a 390×844 viewport confirmed the public login route at exactly viewport width, no horizontal overflow, accessible form labels/navigation and no console errors. The authenticated workspace and social microinteraction visual matrix remains pending and is not claimed from this public-route check.

## Feed-performance verification

- Feed, saved posts and comment threads use bounded cursor pagination and TanStack infinite queries; backend queries request only `limit + 1` projected rows and never load the complete feed.
- The frontend now stably deduplicates merged pages by persisted IDs, preventing duplicate cards/comments when invalidation, optimistic cache state or future realtime delivery overlaps a REST page.
- A dedicated ordering/deduplication test passes. Frontend now passes 36/36 tests and its production build; the existing Monaco/editor chunk-size and imported CSS at-rule warnings remain visible rather than suppressed.

## File-security verification

- Workspace node validation rejects empty/dot/traversal-shaped, absolute-separator, `.git` (any casing), control-character and invalid filesystem names before persistence. Active sibling uniqueness is also enforced by PostgreSQL.
- Native Git canonicalizes every project/worktree path beneath its configured root, rejects traversal and `.git` segments case-insensitively, passes subprocess arguments without a shell and now refuses existing symbolic-link components before file reads/writes.
- Multipart workspace/avatar/chat limits and file/image signature checks remain server-enforced. Uploaded data is materialized as regular files, never as caller-supplied filesystem links.
- Backend passes 288/288 unit tests, including path escape, command injection, symlink and repository metadata casing checks.

## AI data-security verification

- AI context still begins with server-side project membership/write authorization and bounded explicit file IDs; protected `.env*`, `appsettings*.json`, credential/key/certificate and package-registry files are rejected rather than included.
- Selected code, neighboring code, uploaded text, prior conversation messages, repository excerpts, debugging evidence and AI-tool result/error persistence pass through centralized secret redaction. Generic API/token/client-secret assignments are covered in addition to JWT, provider-token, cloud-key, connection-string and private-key shapes.
- The Ollama-compatible provider applies a final defense-in-depth redaction to system, user, history and repository channels immediately before serialization. Provider error bodies are redacted before becoming logged exceptions; prompts and generated content are never written to observability logs.
- Autonomous testing and multi-file UI generation fail safely when source targets contain secret-shaped values, avoiding both disclosure and accidental replacement of a real secret with a redaction marker during apply.
- A captured HTTP-payload test proves the same secret is removed from all four textual provider channels. Backend build has zero warnings/errors and 292/292 unit tests pass.

## Honest production status

## Core social E2E verification

- A deterministic Chromium E2E now verifies the required profile follow → feed publish → like → comment → save sequence through the real routed React UI and API client/cache behavior. Network responses are isolated fixtures, so this proves browser integration while the separate API smoke tests continue to prove database behavior.
- Live-room Chromium E2E verifies host join, participant invitation, start, shared task creation, persistent chat and completion. REST failures now render retryable error states, while SignalR outages degrade to an explicit reconnecting notice instead of trapping the page in an endless loading state.
- SuperAdmin Chromium E2E verifies user inspection, Moderator assignment, reasoned 24-hour suspension, unban and expired-project deadline extension. Admin/Moderator E2E verifies report review, content removal and the corresponding audit-log surface. The audit also found and fixed an inverted suspension boolean that previously made the “block” confirmation call the unban path.
- The full Playwright suite runs with bounded concurrency to avoid false lazy-route timeouts: 7 deterministic Chromium flows pass and the credential-dependent real login/logout flow is explicitly skipped when secrets are absent.

## Static deployment verification

- A real static deployment pipeline now snapshots the saved root `index.html` plus allowlisted text assets into versioned database records, binds each version to a source SHA-256 and latest Git commit, retains superseded releases, and exposes the active public release through a stable slug URL.
- Deployment requires a public project, repository-write membership and a writable project lifecycle state. Limits of 250 files and 2,000,000 characters prevent unbounded snapshots; ambiguous/traversal asset paths are rejected.
- Public assets use strict content types, `nosniff` and a restrictive CSP. Returned links are origin-relative to prevent Host-header link poisoning. The migration enforces unique project versions, unique slugs and one active deployment per project. Backend 300/300 tests, frontend build, and Chromium deployment workflow pass; the PostgreSQL testcontainer case is present but skips when Docker is unavailable.

The platform is a strong working foundation, not yet the complete CODING 2.0 target. Missing systems remain documented as missing. Production code execution is not claimed until the isolated worker architecture is deployed and verified.
