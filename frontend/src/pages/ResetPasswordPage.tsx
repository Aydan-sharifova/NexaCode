import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { authService } from "../services/authService";
import { FormField } from "../components/FormField";

export function ResetPasswordPage() {
  const [params] = useSearchParams(); const token = params.get("token") ?? "";
  const [password, setPassword] = useState(""); const [confirm, setConfirm] = useState("");
  const [status, setStatus] = useState<"idle" | "saving" | "done">("idle"); const [error, setError] = useState<string>();
  const submit = async (event: FormEvent) => { event.preventDefault(); if (password !== confirm) { setError("Passwords do not match."); return; } setError(undefined); setStatus("saving"); try { await authService.resetPassword(token, password); setStatus("done"); } catch { setError("The reset link is invalid or expired."); setStatus("idle"); } };
  if (!token) return <><div className="form-alert" role="alert">The reset link is invalid or incomplete.</div><Link to="/forgot-password">Request a new link</Link></>;
  if (status === "done") return <><header className="form-heading"><h2>Password updated</h2><p>You can now sign in with your new password.</p></header><Link className="primary-button" to="/login">Sign in</Link></>;
  return <><header className="form-heading"><p className="eyebrow">Secure recovery</p><h2>Choose a new password</h2><p>Use at least 12 characters with uppercase, lowercase, number, and special character.</p></header><form onSubmit={(event) => void submit(event)}><FormField label="New password" required minLength={12} type="password" autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} /><FormField label="Confirm password" required minLength={12} type="password" autoComplete="new-password" value={confirm} onChange={(event) => setConfirm(event.target.value)} />{error && <div className="form-alert" role="alert">{error}</div>}<button className="primary-button" disabled={status === "saving"}>{status === "saving" ? "Updating…" : "Update password"}</button></form></>;
}
