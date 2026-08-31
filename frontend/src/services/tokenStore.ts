let accessToken: string | null = null;
let expiresAt = 0;

export const tokenStore = {
  get: () => accessToken,
  set: (token: string, expiration?: string) => { accessToken = token; expiresAt = expiration ? Date.parse(expiration) : 0; },
  expiresSoon: (skewMs = 30_000) => Boolean(accessToken && expiresAt && expiresAt <= Date.now() + skewMs),
  clear: () => { accessToken = null; expiresAt = 0; },
};
