import { apiClient } from "../../services/apiClient";
export interface DatabaseColumn { name:string; dataType:string; isNullable:boolean; isPrimaryKey:boolean; isUnique:boolean; defaultValue?:string }
export interface DatabaseForeignKey { name:string; sourceTable:string; sourceColumns:string[]; targetTable:string; targetColumns:string[] }
export interface DatabaseIndex { name:string; isUnique:boolean; columns:string[] }
export interface DatabaseTable { schema:string; name:string; columns:DatabaseColumn[]; foreignKeys:DatabaseForeignKey[]; indexes:DatabaseIndex[] }
export interface DatabaseSchema { name:string; tables:DatabaseTable[] }
export type DatabaseProvider="PostgreSQL"|"MySQL"|"SQLServer"|"SQLite";
export interface ProjectDatabase { isConfigured:boolean; provider?:DatabaseProvider; version:number; schemas:DatabaseSchema[] }
export interface DatabaseMigration { id:string; name:string; baseVersion:number; status:"Draft"|"Applied"|"Superseded"; ddlPreview:string; createdAt:string; appliedAt?:string }
export interface MigrationColumn { name:string; type:"uuid"|"string"|"text"|"integer"|"boolean"|"decimal"|"timestamp"; isNullable:boolean; isUnique:boolean }
export const databaseMetadataApi={
  schema:(projectId:string)=>apiClient.get<ProjectDatabase>(`/projects/${projectId}/database/schema`),
  configure:(projectId:string,input:{provider:DatabaseProvider;schemaName:string})=>apiClient.post<ProjectDatabase>(`/projects/${projectId}/database/configure`,input),
  migrations:(projectId:string)=>apiClient.get<DatabaseMigration[]>(`/projects/${projectId}/database/migrations`),
  createTableMigration:(projectId:string,input:{name:string;schema:string;table:string;columns:MigrationColumn[];expectedVersion:number})=>apiClient.post<DatabaseMigration>(`/projects/${projectId}/database/migrations/tables`,input),
  applyMigration:(projectId:string,migrationId:string,input:{expectedVersion:number;confirm:boolean})=>apiClient.post<ProjectDatabase>(`/projects/${projectId}/database/migrations/${migrationId}/apply`,input),
};
