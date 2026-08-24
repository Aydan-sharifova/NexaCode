import { apiClient } from "../../services/apiClient";

export interface ProjectDeployment { id:string; projectId:string; slug:string; version:number; sourceHash:string; commitSha?:string; deployedAt:string; isActive:boolean; url:string; }
export const deploymentKeys = { list:(projectId:string)=>["deployments",projectId] as const };
export const deploymentsApi = {
  list:(projectId:string)=>apiClient.get<ProjectDeployment[]>(`/projects/${projectId}/deployments`),
  deploy:(projectId:string)=>apiClient.post<ProjectDeployment>(`/projects/${projectId}/deployments`,{}),
};
