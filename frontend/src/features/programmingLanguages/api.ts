import { apiClient } from "../../services/apiClient";

export interface ProgrammingLanguage {
  id: string;
  name: string;
  slug: string;
  sortOrder: number;
  isActive: boolean;
}

export const programmingLanguageApi = {
  list: () => apiClient.get<ProgrammingLanguage[]>("/programming-languages"),
};
