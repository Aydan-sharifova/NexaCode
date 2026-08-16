import { apiClient } from "../../services/apiClient";
export interface Page<T>{items:T[];total:number;page:number;pageSize:number}
export interface AdminUser{id:string;displayName:string;userName:string;email:string;isSuspended:boolean;roles:string[];createdAt:string;lastSeen:string}
export interface AdminUserDetails{id:string;firstName:string;lastName:string;userName:string;email:string;bio?:string;avatarUrl?:string;isSuspended:boolean;suspensionReason?:string;roles:string[];projectCount:number;createdAt:string;lastSeen:string}
export interface AdminProject{id:string;name:string;ownerName:string;isPublic:boolean;memberCount:number;taskCount:number;createdAt:string}
export interface PlatformStats{totalUsers:number;activeUsers30Days:number;suspendedUsers:number;totalProjects:number;projects30Days:number;activity30Days:number}
export interface ProgrammingLanguage{id:string;name:string;slug:string;sortOrder:number;isActive:boolean}
export const adminApi={
 stats:()=>apiClient.get<PlatformStats>("/admin/statistics"),
 users:(search:string,page:number)=>apiClient.get<Page<AdminUser>>(`/admin/users?search=${encodeURIComponent(search)}&page=${page}&pageSize=20`),
 user:(id:string)=>apiClient.get<AdminUserDetails>(`/admin/users/${id}`),
 suspension:(id:string,suspended:boolean,reason?:string)=>apiClient.put<void>(`/admin/users/${id}/suspension`,{suspended,reason}),
 role:(id:string,role:string,enabled:boolean)=>apiClient.put<void>(`/admin/users/${id}/roles/${role}`,{enabled}),
 updateUser:(id:string,value:{firstName:string;lastName:string;userName:string;email:string;bio?:string})=>apiClient.put<AdminUserDetails>(`/admin/users/${id}`,value),
 deleteUser:(id:string,reason:string)=>apiClient.delete<void>(`/admin/users/${id}`,{reason}),
 projects:(search:string,page:number)=>apiClient.get<Page<AdminProject>>(`/admin/projects?search=${encodeURIComponent(search)}&page=${page}&pageSize=20`),
 deleteProject:(id:string,reason:string)=>apiClient.delete<void>(`/admin/projects/${id}`,{reason}),
 languages:()=>apiClient.get<ProgrammingLanguage[]>("/admin/programming-languages"),
 createLanguage:(value:{name:string;slug?:string;sortOrder:number})=>apiClient.post<ProgrammingLanguage>("/admin/programming-languages",value),
 updateLanguage:(id:string,value:{name:string;slug?:string;sortOrder:number;isActive:boolean})=>apiClient.put<ProgrammingLanguage>(`/admin/programming-languages/${id}`,value),
 deleteLanguage:(id:string)=>apiClient.delete<void>(`/admin/programming-languages/${id}`),
};
