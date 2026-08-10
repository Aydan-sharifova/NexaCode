import { apiClient } from "../../services/apiClient";
export interface DatabaseColumn { name:string; dataType:string; isNullable:boolean; isPrimaryKey:boolean; isUnique:boolean; defaultValue?:string }
export interface DatabaseForeignKey { name:string; sourceTable:string; sourceColumns:string[]; targetTable:string; targetColumns:string[] }
export interface DatabaseIndex { name:string; isUnique:boolean; columns:string[] }
export interface DatabaseTable { schema:string; name:string; columns:DatabaseColumn[]; foreignKeys:DatabaseForeignKey[]; indexes:DatabaseIndex[] }
export interface DatabaseSchema { name:string; tables:DatabaseTable[] }
export const databaseMetadataApi={schema:(projectId:string)=>apiClient.get<DatabaseSchema[]>(`/projects/${projectId}/database/schema`)};
