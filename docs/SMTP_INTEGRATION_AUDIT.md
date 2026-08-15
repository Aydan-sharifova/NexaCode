# SMTP Integration Audit

## Existing implementation

- `Coding.Application` exposes `IEmailSender`; it does not reference MailKit.
- `Coding.Infrastructure` contains a MailKit 4.17.0 SMTP sender and typed `SmtpSettings`.
- Registration creates a one-hour email-verification token and sends a verification message.
- Resend verification, verification confirmation, forgot-password, and reset-password API flows already exist.
- Project invitations are persisted and create in-app notifications, but did not send email.
- Task assignment creates an in-app notification; email delivery is not yet preference/throttle aware.
- AI approvals exist, but no approved transactional-email policy is currently defined.

## Problems found

- A Gmail credential was stored in tracked `appsettings.json`.
- `UseSsl` appeared twice with contradictory values.
- Two competing email abstractions/implementations were registered; one was incomplete and used the wrong namespace.
- Port 587 transport selection was implicit instead of an explicit STARTTLS option.
- No development-only SMTP diagnostic endpoint existed.
- The frontend lacks public verify-email, forgot-password, and reset-password routes.

## Repair scope

- Keep the established `IEmailSender` abstraction and MailKit implementation.
- Remove the incomplete duplicate email stack.
- Store secrets only in User Secrets/environment variables.
- Add explicit `UseStartTls`, safe configuration defaults, Docker mapping, templates, protected test flow, project invitation delivery, frontend auth pages, tests, and verification documentation.

