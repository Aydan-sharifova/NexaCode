import { useMutation, useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { billingApi } from "../features/billing/api";
import { useToast } from "../contexts/ToastContext";

const freeFeatures = ["Public and private projects", "Live editor and collaboration", "Community marketplace", "Standard analytics"];
const plusFeatures = ["Everything in Free", "Advanced analytics", "AI Mentor and Project Planner", "Priority access to new AI tools"];

export function BillingPage() {
  const [params] = useSearchParams();
  const { show } = useToast();
  const status = useQuery({ queryKey: ["billing", "status"], queryFn: billingApi.status });
  const redirect = useMutation({
    mutationFn: () => status.data?.isPlus ? billingApi.portal() : billingApi.checkout(),
    onSuccess: ({ url }) => { window.location.assign(url); },
    onError: (error) => show(error instanceof Error ? error.message : "Billing request failed.", "error"),
  });
  const checkoutState = params.get("checkout");

  return <main className="dashboard-content billing-page">
    <header className="billing-heading"><span>PLANS & BILLING</span><h1>Choose the plan that fits your work</h1><p>Start free and upgrade when you need advanced analytics and AI tools.</p></header>
    {checkoutState === "success" && <div className="billing-banner success">Payment completed. Your Plus plan will activate as soon as Stripe confirms it.</div>}
    {checkoutState === "cancelled" && <div className="billing-banner">Checkout was cancelled. No payment was taken.</div>}
    <section className="pricing-grid" aria-label="Subscription plans">
      <article className={`pricing-card ${!status.data?.isPlus ? "current" : ""}`}>
        <div><span className="plan-name">FREE</span><h2>Ordinary</h2><p>Core tools for individuals getting started.</p></div>
        <div className="plan-price"><strong>$0</strong><span>forever</span></div>
        <ul>{freeFeatures.map(feature => <li key={feature}>✓ {feature}</li>)}</ul>
        <button disabled>{status.data?.isPlus ? "Included in Plus" : "Current plan"}</button>
      </article>
      <article className={`pricing-card plus ${status.data?.isPlus ? "current" : ""}`}>
        <div className="popular-label">MOST POPULAR</div>
        <div><span className="plan-name">PLUS</span><h2>NexaCode Plus</h2><p>More intelligence and visibility for serious builders.</p></div>
        <div className="plan-price"><strong>Stripe</strong><span>secure recurring billing</span></div>
        <ul>{plusFeatures.map(feature => <li key={feature}>✓ {feature}</li>)}</ul>
        <button disabled={redirect.isPending || status.isLoading} onClick={() => redirect.mutate()}>
          {redirect.isPending ? "Opening Stripe…" : status.data?.isPlus ? "Manage subscription" : "Upgrade to Plus"}
        </button>
        <small>Price and currency are shown securely on Stripe Checkout.</small>
      </article>
    </section>
  </main>;
}
