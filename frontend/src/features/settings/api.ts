import { apiClient } from "../../services/apiClient";
export interface Profile{id:string;publicId:string;firstName:string;lastName:string;userName:string;email:string;bio?:string;avatarUrl?:string}
export interface Preference{theme:string;language:string;reducedMotion:boolean;compactMode:boolean;securityAlertsEnabled:boolean}
export interface NotificationPreference{type:string;inAppEnabled:boolean;emailEnabled:boolean}
export interface Settings{profile:Profile;preferences:Preference;notifications:NotificationPreference[]}
export interface UserSession{id:string;ipAddress?:string;device:string;createdAt:string;lastSeenAt:string;expiresAt:string;isCurrent:boolean}
export const settingsApi={
 get:()=>apiClient.get<Settings>("/settings"),
 profile:(value:{firstName:string;lastName:string;bio?:string})=>apiClient.put<Profile>("/settings/profile",value),
 preferences:(value:Preference)=>apiClient.put<Preference>("/settings/preferences",value),
 notifications:(preferences:NotificationPreference[])=>apiClient.put<void>("/settings/notifications",{preferences}),
 password:(value:{currentPassword:string;newPassword:string;revokeOtherSessions:boolean})=>apiClient.put<void>("/settings/password",value),
 sessions:()=>apiClient.get<UserSession[]>("/settings/sessions"),
 revoke:(id:string)=>apiClient.delete<void>(`/settings/sessions/${id}`),
 avatar:(file:File)=>{const form=new FormData();form.append("file",file);return apiClient.postForm<{url:string}>("/settings/avatar",form);},
 removeAvatar:()=>apiClient.delete<void>("/settings/avatar"),
};
