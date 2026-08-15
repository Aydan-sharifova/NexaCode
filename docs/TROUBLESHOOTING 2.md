# Production troubleshooting

- **502 from Nginx:** inspect API container health/logs and internal port 8080; do not publish that port as a workaround.
- **Readiness fails:** inspect PostgreSQL/Redis health and connection environment without printing credentials.
- **Browser CORS failure:** `Cors__AllowedOrigins__0` must exactly match `https://app.yourdomain.com`; wildcard origins cannot be used with credentials.
- **SignalR polling instead of WebSockets:** verify HTTPS frontend URL, Nginx `/hubs/` upgrade headers, load-balancer WebSocket support, JWT query token and Redis when multiple API replicas run.
- **Deep route returns 404:** verify the frontend project uses the committed Vercel SPA rewrite.
- **Migration failure:** do not start the new API; retain logs, review SQL and restore only after confirming compatibility.
- **AI unavailable:** verify provider selection/base URL/model/key on the API host. Core `/health/ready` should remain healthy.
- **Email unavailable:** keep SMTP disabled until host, sender and rotated credential are present. Never restore the credential removed from repository history.
