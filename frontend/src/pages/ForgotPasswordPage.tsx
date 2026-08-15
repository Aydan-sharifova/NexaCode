import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { authService } from "../services/authService";
import { FormField } from "../components/FormField";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [status, setStatus] = useState<"idle" | "sending" | "sent">("idle");
  const [error, setError] = useState<string>();
  const submit = async (event: FormEvent) => {
    event.preventDefault(); setError(undefined); setStatus("sending");
    try { await authService.forgotPassword(email); setStatus("sent"); }
    catch { setError("Email göndərilə bilmədi. Bir qədər sonra yenidən cəhd edin."); setStatus("idle"); }
  };
  return <>
    <header className="form-heading"><p className="eyebrow">Account recovery</p><h2>Forgot password?</h2><p>We will send a secure reset link if the account exists.</p></header>
    {status === "sent" ? <div className="form-alert success" role="status">If the account exists, a reset email has been sent.</div> :
      <form onSubmit={(event) => void submit(event)}><FormField label="Email address" required type="email" autoComplete="email" placeholder="you@company.com" value={email} onChange={(event) => setEmail(event.target.value)} />{error && <div className="form-alert" role="alert">{error}</div>}<button className="primary-button" disabled={status === "sending"}>{status === "sending" ? "Sending…" : "Send reset link"}</button></form>}
    <p className="auth-switch"><Link to="/login">Back to sign in</Link></p>
  </>;
}
