import { apiClient } from "../../services/apiClient";

export interface BillingStatus {
  plan: "Free" | "Plus";
  status: string;
  isPlus: boolean;
  customerId?: string;
}

interface BillingRedirect { url: string; }

export const billingApi = {
  status: () => apiClient.get<BillingStatus>("/billing/status"),
  checkout: () => apiClient.post<BillingRedirect>("/billing/checkout"),
  portal: () => apiClient.post<BillingRedirect>("/billing/portal"),
};
