# Domains and TLS

Create an `A` record `api` pointing to the VPS public IP. Configure `app.yourdomain.com` in the React hosting provider and the apex/`www` records in the showcase hosting provider. Wait for DNS propagation before requesting certificates.

The included Nginx configuration is an HTTP bootstrap and intentionally contains no fake certificate paths. On Ubuntu, install Certbot using the current Ubuntu-supported package/snap method, then obtain a certificate for `api.yourdomain.com`. Either let Certbot manage a host Nginx installation that proxies to the container, or mount managed certificate files read-only into a TLS-enabled edge container after they exist. Do not expose API port 8080.

After activation, redirect port 80 to HTTPS, enable automatic renewal, and test renewal. Set `API_DOMAIN`, `APP_DOMAIN`, `AllowedHosts`, CORS and frontend URLs to the exact production hostnames. SignalR uses `wss://` automatically when its configured URL is HTTPS.
