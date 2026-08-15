# SMTP Setup

The repository contains no SMTP credential. For Gmail on port 587 use STARTTLS, not SSL-on-connect.

```bash
dotnet user-secrets set "Smtp:Enabled" "true" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:Port" "587" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:UseSsl" "false" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:UseStartTls" "true" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:Username" "YOUR_EMAIL@gmail.com" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:Password" "YOUR_GMAIL_APP_PASSWORD" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:FromEmail" "YOUR_EMAIL@gmail.com" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:FromName" "NexaCode" --project src/Coding.Api/Coding.Api.csproj
dotnet user-secrets set "Smtp:ClientBaseUrl" "http://localhost:5173" --project src/Coding.Api/Coding.Api.csproj
```

For Docker/production, provide the corresponding `SMTP_*` variables from a secret manager. Never use a normal Gmail account password; use a revocable app password where required.

## Development delivery test

With the API running in `Development`, an authenticated `Admin` or `SuperAdmin` can call:

```http
POST /api/dev/email/test
Content-Type: application/json
Authorization: Bearer <admin-token>

{"email":"recipient@example.com"}
```

The route is unavailable outside Development. A `200` response means the SMTP provider accepted the message; a `503` response means delivery failed without exposing provider or credential details.

## TLS troubleshooting on macOS

If MailKit reports `SslHandshakeException` with an incomplete certificate revocation check, repair the Mac's certificate trust/revocation access and retry. Check VPNs, TLS-inspecting proxies, antivirus filters, system time, and access to certificate-authority OCSP/CRL endpoints. Do not work around the problem with a permissive `ServerCertificateValidationCallback`; that would make SMTP credentials vulnerable to interception.
