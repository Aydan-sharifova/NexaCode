# Provider Integration Gap Analysis

## Scope and current architecture

The platform is a .NET 8 Clean Architecture application with React/Vite, PostgreSQL/EF Core, MediatR, JWT authentication, project roles, SignalR, persistent file versions, and an AI tool registry. Projects currently store a virtual workspace in PostgreSQL; they do not store a host repository path or an external database connection. The AI approval entities and execution pipeline exist, but no public approval API or orchestrator implementation exists.

## Direct browser schema metadata

- Existing UI: project Database route currently reports that direct access is unavailable.
- Existing backend: `GetDatabaseSchemaTool` derives metadata from the EF Core model for an authorized agent run.
- Missing: browser query/API, richer key/index DTOs, and external project database connection/secret storage.
- Security risks: platform-wide metadata disclosure, connection-secret disclosure, arbitrary SQL, and project IDOR.
- Implementation: expose read-only EF model metadata to authenticated project members. This is explicitly platform-model metadata associated with the current project context; it executes no SQL and returns no connection values or row data. External connections remain a separate future provider boundary.

## Repository-wide Git history

- Existing UI: project Version route reports that only persisted file versions are available.
- Existing backend: legacy `GitCommit` entity is not connected to an actual repository; no safe process abstraction or project repository mapping exists.
- Missing: `ProjectRepository`, repository provisioning/mapping, safe Git process runner, queries/controllers, diff DTOs, and UI.
- Security risks: repository/path traversal, arbitrary process arguments, leaking absolute paths, and cross-project access.
- Required implementation: first define and provision a server-owned project repository root. Git reads must use fixed executable arguments, validated revisions and relative paths, pagination, timeouts, output limits, and project membership checks. It cannot truthfully be completed for current virtual workspaces until that mapping exists.

## Payment provider integration

- Existing UI: billing route is a configuration-aware unavailable state.
- Existing backend/provider: no subscription entities, entitlements, payment abstraction, SDK, webhook verifier, or provider credentials.
- Missing: provider-independent domain/application contracts, persistence, configured sandbox adapter, webhook source-of-truth flow, and quota enforcement.
- Security risks: browser-driven upgrades, forged/replayed webhooks, secret logging, duplicate events, and inconsistent entitlements.
- Required implementation: introduce provider-neutral billing models and enforce a signed, idempotent webhook flow. A real checkout cannot be verified without selecting a provider and supplying valid test credentials; the application must continue to start without them.

## Executable AI approvals

- Existing UI: project Approval route is informational.
- Existing backend: `AiAgentRun`, `AiToolCall`, `AiApprovalRequest`, risk policy, authorization, argument hashing, idempotency and the central dispatch pipeline exist. Only read-only tools are registered; patch/build/test tools and an orchestrator implementation are absent.
- Missing: approval query/commands/API, post-approval dispatch path, realtime events, mutating tool implementations, secure execution sandbox, patch service and frontend API.
- Security risks: replay/double execution, stale arguments, expired approvals, project IDOR, and bypassing the risk gate.
- Implementation: approval decisions must reload membership and persisted state, compare the canonical argument hash, update atomically, and execute only a registered tool. Mutating patch/build/test approval remains blocked until the corresponding guarded tools exist.

## Migration assessment

The current AI tables and unique tool idempotency index already exist and must not be duplicated. Browser metadata for the EF model requires no schema change. Git repository mapping and billing require new tables, but should not be migrated before repository provisioning and a payment provider are selected because otherwise they would create inert production schema.
