import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "../components/FormField";
import { useAuth } from "../hooks/useAuth";

const loginSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
  password: z.string().min(1, "Enter your password."),
});

type LoginValues = z.infer<typeof loginSchema>;

export function LoginPage() {
  const { login, session, isInitializing } = useAuth();
  const navigate = useNavigate();
  const [serverError, setServerError] = useState<string | null>(null);
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  const submit = handleSubmit(async (values) => {
    setServerError(null);
    try {
      await login(values);
      navigate("/dashboard", { replace: true });
    } catch (error) {
      setServerError(error instanceof Error ? error.message : "Sign in failed. Please try again.");
    }
  });

  if (!isInitializing && session) return <Navigate to={!session.user.isDemo && !session.user.isEmailVerified ? "/register" : "/dashboard"} replace />;

  return (
    <>
      <header className="form-heading">
        <p className="eyebrow">Welcome back</p>
        <h2>Sign in to your account</h2>
        <p>Use your work email and password to continue.</p>
      </header>
      <form onSubmit={submit} noValidate>
        {serverError && <div className="form-alert" role="alert">{serverError}</div>}
        <FormField label="Email address" type="email" autoComplete="email" placeholder="you@company.com" error={errors.email?.message} {...register("email")} />
        <FormField label="Password" type="password" autoComplete="current-password" placeholder="Enter your password" error={errors.password?.message} {...register("password")} />
        <p className="auth-switch"><Link to="/forgot-password">Forgot password?</Link></p>
        <button className="primary-button" type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Signing in…" : "Sign in"}
        </button>
      </form>
    </>
  );
}
