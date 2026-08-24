import { apiClient } from "../../services/apiClient";

export interface MentorEvidence {
  declaredSkills: string[];
  learningTopics: string[];
  observedTechnologies: string[];
  projectCount: number;
  completedTaskCount: number;
  commitCount: number;
  testFileCount: number;
  usesLayeredArchitecture: boolean;
  analyzedAt: string;
}
export interface MentorRecommendation { category: string; title: string; rationale: string; action: string; }
export interface MentorAnalysis {
  evidence: MentorEvidence;
  recommendations: MentorRecommendation[];
  modelNarrative?: string;
  provider: string;
  model?: string;
  modelAvailable: boolean;
  privacyNotice: string;
}
export const mentorKeys = { analysis: ["mentor", "analysis"] as const };
export const mentorApi = {
  analysis: () => apiClient.get<MentorAnalysis>("/mentor"),
  generate: () => apiClient.post<MentorAnalysis>("/mentor/generate", {}),
};
