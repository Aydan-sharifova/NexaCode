const publicUserIdPattern = /^[A-HJ-NP-Z2-9]{8}$/;

export function normalizePublicUserId(value: string): string {
  return value.trim().replace(/^@/, "").toUpperCase();
}

export function isValidPublicUserId(value: string): boolean {
  return publicUserIdPattern.test(normalizePublicUserId(value));
}
