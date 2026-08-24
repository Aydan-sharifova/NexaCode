import { apiClient } from "../../services/apiClient";
export type SavedType="All"|"Posts"|"Projects"|"Snippets"|"Templates"|"Agents";
export interface SavedPost{id:string;type:string;content:string;language?:string;author:{publicId:string;userName:string;displayName:string;avatarUrl?:string};savedAt:string}
export interface SavedProject{id:string;name:string;description?:string;language:string;ownerPublicId:string;savedAt:string}
export interface SavedPackage{id:string;slug:string;title:string;description:string;category:string;tags:string[];savedAt:string}
export interface SavedContent{posts:SavedPost[];projects:SavedProject[];snippets:SavedPost[];templates:SavedPackage[];agents:SavedPackage[]}
export const savedApi={list:(type:SavedType="All",search="")=>apiClient.get<SavedContent>(`/saved?type=${type}${search?`&search=${encodeURIComponent(search)}`:""}`),project:(id:string,saved:boolean)=>apiClient.put<boolean>(`/saved/projects/${id}`,{saved})};
