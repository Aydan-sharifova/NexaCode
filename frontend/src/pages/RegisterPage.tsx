import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "../components/FormField";
import { useAuth } from "../hooks/useAuth";
import { authService } from "../services/authService";

const registerSchema = z.object({
  firstName: z.string().trim().min(2, "Use at least 2 characters.").max(50),
  lastName: z.string().trim().min(2, "Use at least 2 characters.").max(50),
  userName: z.string().trim().min(3, "Use at least 3 characters.").max(50).regex(/^[a-zA-Z0-9._-]+$/, "Use letters, numbers, dots, dashes, or underscores."),
  email: z.string().trim().email("Enter a valid email address."),
  password: z.string().min(12, "Use at least 12 characters.").max(128).regex(/[A-Z]/, "Add an uppercase letter.").regex(/[a-z]/, "Add a lowercase letter.").regex(/[0-9]/, "Add a number.").regex(/[^A-Za-z0-9]/, "Add a special character."),
  confirmPassword: z.string(),
}).refine((values) => values.password === values.confirmPassword, {
  message: "Passwords do not match.",
  path: ["confirmPassword"],
});

type RegisterValues = z.infer<typeof registerSchema>;

export function RegisterPage() {
  const { register: createAccount, logout, session, isInitializing } = useAuth();
  const navigate = useNavigate();
  const [serverError, setServerError] = useState<string | null>(null);
  const [registeredEmail, setRegisteredEmail] = useState<string>();
  const [resendState, setResendState] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [isSwitchingAccount, setIsSwitchingAccount] = useState(false);
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { firstName: "", lastName: "", userName: "", email: "", password: "", confirmPassword: "" },
  });

  const submit = handleSubmit(async ({ confirmPassword: _, ...values }) => {
    setServerError(null);
    try {
      await createAccount(values);
      setRegisteredEmail(values.email);
    } catch (error) {
      setServerError(error instanceof Error ? error.message : "Registration failed. Please try again.");
    }
  });

  const pendingEmail = registeredEmail ?? (!session?.user.isDemo && !session?.user.isEmailVerified ? session?.user.email : undefined);

  if (!isInitializing && session?.user.isEmailVerified) return <Navigate to="/dashboard" replace />;

  if (pendingEmail) {
    const resend = async () => {
      setResendState("sending");
      try {
        await authService.requestEmailVerification(pendingEmail);
        setResendState("sent");
      } catch {
        setResendState("error");
      }
    };
    const useDifferentAccount = async () => {
      setIsSwitchingAccount(true);
      setRegisteredEmail(undefined);
      try {
        await logout();
      } finally {
        navigate("/login", { replace: true });
      }
    };
    return <section className="verification-pending">
      <div className="verification-icon" aria-hidden="true">✉</div>
      <header className="form-heading">
        <p className="eyebrow">One last step</p>
        <h2>Check your inbox</h2>
        <p>We sent a secure verification link to <strong>{pendingEmail}</strong>. Confirm your email before entering the workspace.</p>
      </header>
      <div className="verification-note"><span aria-hidden="true">i</span><p>The link expires in one hour. Check your spam or junk folder if it does not arrive within a minute.</p></div>
      {resendState === "sent" && <p className="verification-status success" role="status">A fresh verification email was sent.</p>}
      {resendState === "error" && <p className="verification-status error" role="alert">We could not resend the email. Please try again.</p>}
      <button className="primary-button" type="button" disabled={resendState === "sending"} onClick={() => void resend()}>{resendState === "sending" ? "Sending…" : "Resend verification email"}</button>
      <button
        className="verification-signin"
        type="button"
        disabled={isSwitchingAccount}
        onClick={() => void useDifferentAccount()}
      >
        {isSwitchingAccount ? "Switching account…" : "Use a different account"}
      </button>
    </section>;
  }

  return (
    <>
      <header className="form-heading">
        <p className="eyebrow">Start building</p>
        <h2>Create your account</h2>
        <p>Set up secure access to your collaborative workspace.</p>
      </header>
      <form onSubmit={submit} noValidate>
        {serverError && <div className="form-alert" role="alert">{serverError}</div>}
        <div className="form-row">
          <FormField label="First name" autoComplete="given-name" error={errors.firstName?.message} {...register("firstName")} />
          <FormField label="Last name" autoComplete="family-name" error={errors.lastName?.message} {...register("lastName")} />
        </div>
        <FormField label="Username" autoComplete="username" placeholder="your.name" error={errors.userName?.message} {...register("userName")} />
        <FormField label="Email address" type="email" autoComplete="email" placeholder="you@company.com" error={errors.email?.message} {...register("email")} />
        <div className="form-row">
          <FormField label="Password" type="password" autoComplete="new-password" error={errors.password?.message} {...register("password")} />
          <FormField label="Confirm password" type="password" autoComplete="new-password" error={errors.confirmPassword?.message} {...register("confirmPassword")} />
        </div>
        <p className="password-hint">Use 12+ characters with uppercase, lowercase, number, and special character.</p>
        <button className="primary-button" type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Creating account…" : "Create account"}
        </button>
      </form>
    </>
  );
}
