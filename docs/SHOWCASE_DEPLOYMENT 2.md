# Showcase deployment

The detected showcase is a vinext application with `.openai/hosting.json` and Cloudflare Worker-compatible output. It is not a conventional Next.js Vercel artifact; deploying it to Vercel without migrating away from vinext is unsupported. The current safe path is the existing Sites/Cloudflare-compatible hosting flow with root directory `showcase` and build command `npm run build`.

Set public values from `showcase/.env.example`: `NEXT_PUBLIC_APP_URL`, `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_DEMO_APP_URL`, GitHub/docs links and optional video URL. They must contain no secrets. Attach `yourdomain.com` and optional `www` in the hosting provider and follow its DNS instructions. Git-connected deployments should run `npm ci`, lint/tests, then build.

A future Vercel migration must first replace the vinext/Cloudflare-specific build and validate server/runtime features. It is not represented as complete here.
