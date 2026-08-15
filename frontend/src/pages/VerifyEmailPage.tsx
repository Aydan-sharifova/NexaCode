import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { authService } from "../services/authService";

export function VerifyEmailPage() {
  const [params] = useSearchParams(); const token = params.get("token") ?? ""; const email = params.get("email") ?? "";
  const [state, setState] = useState<"verifying" | "verified" | "error">("verifying");
  useEffect(() => { if (!token) { setState("error"); return; } void authService.verifyEmail(token).then(async () => { await authService.logout(); setState("verified"); }).catch(() => setState("error")); }, [token]);
  const resend = async () => { if (email) await authService.requestEmailVerification(email); };
  return <><header className="form-heading"><p className="eyebrow">Email verification</p><h2>{state === "verifying" ? "Verifying…" : state === "verified" ? "Email verified" : "Link unavailable"}</h2><p>{state === "verified" ? "Your email has been confirmed. You can continue to NexaCode." : state === "error" ? "The verification link is invalid or expired." : "Please wait while we confirm your email."}</p></header>{state === "verified" && <Link className="primary-button" to="/login">Continue to sign in</Link>}{state === "error" && email && <button className="primary-button" onClick={() => void resend()}>Resend verification email</button>}</>;
}
