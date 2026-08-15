# Chat and user discovery audit

## Previous flow and root cause

Settings exposed `User.ID`, the internal PostgreSQL UUID, as “YOUR CHAT ID”. `ChatPage` required that UUID and posted `{ otherUserId }` to `POST /api/chat/conversations/direct`; the controller bound it directly to `Guid`. There was no user-discovery endpoint or public identity layer. In the reported failure the entered UUID was the authenticated user's own ID, so the request could only enter the self-message rejection path. Earlier stale API/schema state and a broad `DbUpdateException` race catch could turn other database failures into a generic 500 by assuming every save failure meant a duplicate conversation.

The normalized `Conversation.DirectKey`, unique filtered database index, participant authorization, message persistence-before-broadcast, and SignalR `JoinConversation` membership check were already correct and were retained.

## Implemented backend design

- `User.PublicId`: immutable, server-generated eight-character identifier using `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`.
- Cryptographic generator with database collision checking and a unique index.
- Central `IUserLookupService` for case-normalized public-ID and email lookup plus paginated PostgreSQL search.
- Authenticated, rate-limited `/api/users/search`; exact matching for email prevents wildcard email enumeration.
- Authenticated public profile and public-project endpoints. Project queries independently require `IsPublic`.
- `POST /api/chat/direct` accepts `{ "userIdentifier": "A7KM42" }` or an exact email. The authenticated sender always comes from JWT claims.
- Existing normalized direct-conversation key is reused. Save failures are treated as a uniqueness race only when the existing row can actually be loaded.

## Frontend changes

- Settings displays/copies `@PublicId`, never the internal UUID.
- Chat provides debounced real user search by email, name, username or public ID and opens the persisted backend conversation.
- Global search user results open `/users/:publicId` and include a Message action.
- Public profiles show only safe profile fields and public projects.
- Conversation users include `publicId`; SignalR continues using real persisted messages and authorized groups.

## Migration

`20260810010000_AddPublicUserIdsAndImproveDirectMessaging` adds a nullable column, backfills existing users with non-sequential hash-derived values, makes it required, creates the unique index, and replaces the project owner index with `(OwnerId, IsPublic)`. No users or projects are deleted.

## Privacy and security

- All discovery endpoints require authentication and have per-user/IP rate limiting.
- Empty/short searches are rejected and page size is capped at 20.
- Email is accepted only as an exact lookup and is not returned in search/profile DTOs.
- Suspended/deleted users are excluded from lookup/search.
- Private projects, project files, membership details, secrets, AI data and activity are never returned.
- Admin roles receive no chat bypass; REST and SignalR access still require conversation participation.
