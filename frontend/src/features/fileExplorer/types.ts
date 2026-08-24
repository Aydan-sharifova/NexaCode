export type NodeType = "Folder" | "File";
export interface WorkspaceNode { id: string; projectId: string; parentId?: string; name: string; nodeType: NodeType; path: string; hasChildren: boolean; createdAt: string; }
export interface FileContent { nodeId: string; path: string; content: string; isBinary: boolean; contentHash: string; concurrencyToken: string; versionNumber: number; updatedAt: string; }
export interface FileVersion { id: string; nodeId: string; versionNumber: number; contentHash: string; createdById: string; createdBy: string; createdAt: string; }
export interface FileVersionDetails extends FileVersion { content: string; isBinary: boolean; }
export interface VersionComparison { left: FileVersionDetails; right: FileVersionDetails; isIdentical: boolean; }
