# React application deployment

Create a Vercel project with root directory `frontend`, framework Vite, install command `npm ci`, build command `npm run build`, and output directory `dist`. Configure:

```dotenv
VITE_API_URL=https://api.yourdomain.com/api
VITE_SIGNALR_URL=https://api.yourdomain.com/hubs/collaboration
VITE_SHOWCASE_URL=https://yourdomain.com
VITE_APP_ENV=production
VITE_DEMO_MODE=false
```

These values are public build-time configuration and must never contain secrets. `vercel.json` rewrites React Router routes to `index.html`. Add `app.yourdomain.com` in Vercel, update DNS as instructed there, then add the exact HTTPS origin to backend CORS. Verify login cookies/tokens and WebSocket transport from the deployed origin.
