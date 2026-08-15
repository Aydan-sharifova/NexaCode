# SMTP Verification Report

## Automated checks

- Backend API build: passed after the SMTP, invitation-email, and development test-endpoint changes.
- Docker Compose configuration: passed with `SMTP_USE_STARTTLS` mapped to `Smtp__UseStartTls`.
- Unit coverage: validates disabled SMTP, valid Gmail STARTTLS settings, invalid required fields, and conflicting TLS modes.
- Frontend TypeScript build stage: passed for the forgot-password, reset-password, verify-email, login, and registration changes. Vite bundling and the Vitest process did not finish while iCloud File Provider was still hydrating repository files, so those checks must be re-run after all files are local.

## Delivery check

The Gmail credentials were accepted in a direct SMTP diagnostic and Gmail accepted a message. The application-level MailKit check on the current Mac failed before authentication because the local TLS stack reported an incomplete certificate revocation check. The application deliberately does not bypass certificate validation.

Result for this workstation: transport configuration and credentials are valid; MailKit delivery remains blocked until the machine's certificate trust/revocation environment is repaired. Re-run the development-only endpoint after that repair:

```http
POST /api/dev/email/test
Authorization: Bearer <admin-token>
Content-Type: application/json

{"email":"recipient@example.com"}
```

The endpoint is admin-only, exists only in Development, returns success only after the provider accepts the message, and returns a safe `503` response when delivery fails.

## Security checks

- Tracked JSON configuration contains placeholders only and disables SMTP by default.
- Local credentials are stored with .NET User Secrets.
- Docker uses environment-variable placeholders intended for a secret manager.
- SMTP failures are logged without logging the password or message body.
- TLS certificate validation is not disabled.

Because an app password was previously exposed outside the secret store, revoke it in the Google account and create a replacement before production use.
