import { apiClient } from "../../services/apiClient";
import type { CreateMarketplaceInput, MarketplaceCategory, MarketplaceDetails, MarketplaceItem } from "./types";

export const marketplaceApi = {
  list: (category?: MarketplaceCategory, search?: string) => apiClient.get<MarketplaceItem[]>(`/marketplace?${new URLSearchParams({ ...(category ? { category } : {}), ...(search ? { search } : {}) })}`),
  mine: () => apiClient.get<MarketplaceItem[]>("/marketplace/mine"),
  create: (input: CreateMarketplaceInput) => apiClient.post<MarketplaceDetails>("/marketplace", input),
  publish: (itemId: string, versionId: string) => apiClient.post<MarketplaceDetails>(`/marketplace/${itemId}/versions/${versionId}/publish`),
  like: (itemId: string, enabled: boolean) => apiClient.put<MarketplaceItem>(`/marketplace/${itemId}/like`, { enabled }),
  save: (itemId: string, enabled: boolean) => apiClient.put<MarketplaceItem>(`/marketplace/${itemId}/save`, { enabled }),
  installAgent: (projectId: string, itemId: string, versionId: string, approvedDangerousPermissions: string[]) => apiClient.post(`/projects/${projectId}/marketplace/installations`, { itemId, versionId, approvedDangerousPermissions }),
};
