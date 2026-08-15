# Feature Verification Matrix

Status reflects verified behavior, not the mere presence of UI or source code. Initial entries remain `BLOCKED` until the clean API/database baseline is available.

| Feature | Frontend | Backend | Database | Authorization | Realtime | Tests | Status | Issue |
|---|---|---|---|---|---|---|---|---|
| Authentication: register/login/logout/refresh/current user/protected routes | Present | Present | PASS | PASS | N/A | Partial | PARTIAL | Register/refresh/logout verified; dedicated current-user endpoint missing |
| Users: Public ID/search/email/profile/public projects/copy ID | Present | Present | Present | Needs E2E | N/A | Partial | BLOCKED | Requires two-user verification |
| Projects: CRUD/invitations/members/roles/permissions | Present | Present | Present | Needs E2E | Partial | Partial | BLOCKED | Requires clean database and multi-user test |
| Workspace: files/folders/move/delete/nesting | Present | Present | Present | Needs E2E | Partial | Partial | BLOCKED | Persistence and invalid-operation tests pending |
| Monaco: tabs/save/autosave/history/restore | Present | Present | Present | Needs E2E | Partial | Unit partial | BLOCKED | Browser persistence test pending |
| Realtime: connection/reconnect/presence/collaboration/cursors | Present | Present | Present | Needs E2E | Present | CRDT unit pass | BLOCKED | Multi-session test pending |
| Chat: discovery/conversation/messages/unread/typing | PASS | PASS | PASS | PASS | Partial | Partial | PARTIAL | Two-user persistence, duplicate reuse, and self-chat denial pass; live SignalR/typing still pending |
| Kanban: CRUD/drag/assignment/deadline/priority/comments | Present | Present | Present | Needs E2E | Partial | Partial | BLOCKED | End-to-end test pending |
| Notifications: persistence/realtime/read/unread | Present | Present | Present | Needs E2E | Present | Partial | BLOCKED | End-to-end test pending |
| Search: users/email/Public ID/projects/files/tasks | Present | Present | Present | Needs E2E | N/A | Partial | BLOCKED | Privacy/pagination test pending |
| AI: availability/chat/context/patch/approval/application | Present | Present | Present | Needs E2E | Partial | Partial | BLOCKED | External provider may be unavailable; graceful mode pending |
| Git: status/branch/commits/history/diff | Present | Present | Provider | Needs E2E | N/A | Partial | BLOCKED | Real repository test pending |
| Database Explorer: schemas/tables/columns/keys/relations | Present | Present | Provider | Project scoped | N/A | Partial | BLOCKED | Configured database test pending |
| Execution: request/queue/worker/output/cancel/timeout | Partial | Partial | Partial | Needs audit | Partial | Partial | BLOCKED | Sandbox/worker deployment pending |
| Admin: users/projects/logs/analytics/access restriction | Present | Present | Present | Needs E2E | N/A | Partial | BLOCKED | Member denial/admin tests pending |
| Settings: profile/theme/editor/notifications/security | Present | Present | Partial | Present | N/A | Partial | BLOCKED | Persistence test pending |
| Showcase: home/links/demo/docs/responsive | Present | N/A | N/A | N/A | N/A | 2 tests pass | PARTIAL | Lint/tests/build pass; full responsive route interaction audit pending |

## Infrastructure verification

| Feature | Status | Evidence |
|---|---|---|
| Docker configuration | PASS | `docker compose config --quiet` |
| Clean API image | PASS | Multi-stage Release publish, non-root runtime, ICU installed |
| Clean frontend image | PASS | Node 22 build and unprivileged Nginx runtime |
| Fresh PostgreSQL | PASS | Named volume, healthy PostgreSQL 16.4 Alpine |
| Migrations | PASS | 14 history rows, 46 public tables |
| Redis | PASS | Password-protected Redis 7.4 healthy |
| Service health | PASS | All four Compose services healthy |
