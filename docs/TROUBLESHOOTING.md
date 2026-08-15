# Production troubleshooting

- **502 from Nginx:** inspect API container health/logs and internal port 8080; do not publish that port as a workaround.
- **Readiness fails:** inspect PostgreSQL/Redis health and connection environment without printing credentials.
- **Browser CORS failure:** `Cors__AllowedOrigins__0` must exactly match `https://app.yourdomain.com`; wildcard origins cannot be used with credentials.
- **SignalR polling instead of WebSockets:** verify HTTPS frontend URL, Nginx `/hubs/` upgrade headers, load-balancer WebSocket support, JWT query token and Redis when multiple API replicas run.
- **Deep route returns 404:** verify the frontend project uses the committed Vercel SPA rewrite.
- **Migration failure:** do not start the new API; retain logs, review SQL and restore only after confirming compatibility.
- **AI unavailable:** verify provider selection/base URL/model/key on the API host. Core `/health/ready` should remain healthy.
- **Email unavailable:** keep SMTP disabled until host, sender and rotated credential are present. Never restore the credential removed from repository history.
# Docker API exits with code 139

If Alpine logs report that no valid ICU package is installed, the runtime is configured for full globalization but lacks ICU. The API Dockerfile must install `icu-libs`; do not hide the failure by globally disabling globalization unless invariant behavior is explicitly acceptable.

# Compose reports missing JWT or Redis variables

Compose interpolation variables are case-sensitive. Development `.env` must define `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_KEY`, `REDIS_PASSWORD`, and `ALLOWED_HOSTS`. ASP.NET names such as `Jwt__Key` do not satisfy `${JWT_KEY:?...}` interpolation.

# Direct chat returns 400 or 500

The normal UX uses a short Public ID such as `A7KM42`, not an internal GUID. The direct-conversation endpoint accepts Public ID/email and resolves the user server-side. Complex conversation DTO filters must be applied before EF projection; filtering the projected record can fail SQL translation.
