import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { authService } from "../services/authService";

export function VerifyEmailPage() {
  const [params] = useSearchParams();
  const token = params.get("token")?.trim() ?? "";
  const email = params.get("email")?.trim() ?? "";
  const [state, setState] = useState<"verifying" | "verified" | "error">("verifying");
  const [resendState, setResendState] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const verificationStarted = useRef(false);

  useEffect(() => {
    if (verificationStarted.current) return;
    verificationStarted.current = true;
    if (!token) {
      setState("error");
      return;
    }

    void authService.verifyEmail(token, email || undefined)
      .then(async () => {
        await authService.logout();
        setState("verified");
      })
      .catch(() => setState("error"));
  }, [email, token]);

  const resend = async () => {
    if (!email || resendState === "sending") return;
    setResendState("sending");
    try {
      await authService.requestEmailVerification(email);
      setResendState("sent");
    } catch {
      setResendState("error");
    }
  };

  const title = state === "verifying" ? "Verifying…" : state === "verified" ? "Email verified" : "Link unavailable";
  const description = state === "verified"
    ? "Your email has been confirmed. You can continue to NexaCode."
    : state === "error"
      ? "The verification link is invalid, expired, or was replaced by a newer email."
      : "Please wait while we confirm your email.";

  return <>
    <header className="form-heading">
      <p className="eyebrow">Email verification</p>
      <h2>{title}</h2>
      <p>{description}</p>
    </header>
    {state === "verified" && <Link className="primary-button" to="/login">Continue to sign in</Link>}
    {state === "error" && email && resendState !== "sent" && (
      <button className="primary-button" disabled={resendState === "sending"} onClick={() => void resend()}>
        {resendState === "sending" ? "Sending…" : "Resend verification email"}
      </button>
    )}
    {resendState === "sent" && <p role="status">A new verification email has been sent. Use only the newest link.</p>}
    {resendState === "error" && <p role="alert">The email could not be sent. Please try again shortly.</p>}
  </>;
}
