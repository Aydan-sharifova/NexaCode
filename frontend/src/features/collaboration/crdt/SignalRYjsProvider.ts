import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";
import { applyAwarenessUpdate, encodeAwarenessUpdate, type Awareness } from "y-protocols/awareness";
import * as Y from "yjs";
import { tokenStore } from "../../../services/tokenStore";
import { decodeBinary, encodeBinary } from "./updateEncoding";
import type { AwarenessUpdateMessage, CollaborativeState, DocumentUpdateMessage, SyncStatus } from "./types";

const HUB_URL = import.meta.env.VITE_SIGNALR_URL ?? "/hubs/collaboration";
const remoteOrigin = Symbol("signalr-remote");

export class SignalRYjsProvider {
  private connection?: HubConnection;
  private disposed = false;
  private seen = new Set<string>();
  private pending = new Map<string, DocumentUpdateMessage>();
  private listeners = new Set<(status: SyncStatus, pending: number) => void>();
  private retryTimer?: ReturnType<typeof setTimeout>;
  private retryDelay = 1_000;
  status: SyncStatus = "connecting";

  constructor(readonly projectId: string, readonly fileId: string, readonly clientId: string, readonly doc: Y.Doc, readonly awareness: Awareness) {
    doc.on("update", this.onDocumentUpdate);
    awareness.on("update", this.onAwarenessUpdate);
  }

  async connect() {
    if (this.disposed || this.connection) return;
    const connection = new HubConnectionBuilder().withUrl(HUB_URL, { accessTokenFactory: () => tokenStore.get() ?? "" })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000]).configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.None).build();
    this.connection = connection;
    connection.on("DocumentUpdateReceived", this.receiveDocument);
    connection.on("AwarenessUpdateReceived", this.receiveAwareness);
    connection.on("CollaborativeDocumentReset", this.receiveReset);
    connection.onreconnecting(() => this.setStatus("reconnecting"));
    connection.onclose(() => this.setStatus(this.pending.size ? "offline" : "failed"));
    connection.onreconnected(() => void this.synchronize());
    try { await connection.start(); await this.synchronize(); } catch { this.setStatus(this.pending.size ? "offline" : "failed"); }
  }

  subscribe(listener: (status: SyncStatus, pending: number) => void) { this.listeners.add(listener); listener(this.status, this.pending.size); return () => { this.listeners.delete(listener); }; }

  async destroy() {
    if (this.disposed) return;
    this.disposed = true;
    if (this.retryTimer) clearTimeout(this.retryTimer);
    this.retryTimer = undefined;
    this.doc.off("update", this.onDocumentUpdate);
    this.awareness.off("update", this.onAwarenessUpdate);
    const connection = this.connection; this.connection = undefined;
    if (connection) {
      connection.off("DocumentUpdateReceived", this.receiveDocument); connection.off("AwarenessUpdateReceived", this.receiveAwareness); connection.off("CollaborativeDocumentReset", this.receiveReset);
      if (connection.state === HubConnectionState.Connected) await connection.invoke("LeaveCollaborativeFile", this.projectId, this.fileId).catch(() => undefined);
      await connection.stop().catch(() => undefined);
    }
  }

  private synchronize = async () => {
    const connection = this.connection;
    if (!connection || connection.state !== HubConnectionState.Connected || this.disposed) return;
    this.setStatus("synchronizing");
    try {
      const state = await connection.invoke<CollaborativeState>("JoinCollaborativeFile", this.projectId, this.fileId, encodeBinary(Y.encodeStateVector(this.doc)));
      if (state.snapshot) Y.applyUpdate(this.doc, decodeBinary(state.snapshot), remoteOrigin);
      for (const update of state.updates) this.applyDocument(update);
      for (const message of [...this.pending.values()]) await this.send(message);
      const recoveryUpdate = Y.encodeStateAsUpdate(this.doc);
      await this.send({ projectId: this.projectId, fileId: this.fileId, clientId: this.clientId, updateId: await deterministicUpdateId(recoveryUpdate), encodedUpdate: encodeBinary(recoveryUpdate), updateType: "state", createdAt: new Date().toISOString(), plainContent: this.doc.getText("monaco").toString() });
      const awareness = encodeAwarenessUpdate(this.awareness, [this.doc.clientID]);
      await connection.send("SendAwarenessUpdate", { projectId: this.projectId, fileId: this.fileId, clientId: this.clientId, updateId: crypto.randomUUID(), encodedUpdate: encodeBinary(awareness), updateType: "awareness", createdAt: new Date().toISOString() });
      this.retryDelay = 1_000;
      this.setStatus(this.pending.size ? "synchronizing" : "synchronized");
      if (this.pending.size) this.scheduleRetry();
    } catch {
      this.setStatus(this.pending.size ? "offline" : "failed");
      this.scheduleRetry();
    }
  };

  private onDocumentUpdate = (update: Uint8Array, origin: unknown) => {
    if (origin === remoteOrigin || this.disposed) return;
    const message: DocumentUpdateMessage = { projectId: this.projectId, fileId: this.fileId, clientId: this.clientId, updateId: crypto.randomUUID(), encodedUpdate: encodeBinary(update), updateType: "document", createdAt: new Date().toISOString(), plainContent: this.doc.getText("monaco").toString() };
    this.pending.set(message.updateId, message); this.emit(); void this.send(message);
  };
  private send = async (message: DocumentUpdateMessage) => {
    const connection = this.connection;
    if (!connection || connection.state !== HubConnectionState.Connected) { this.setStatus("offline"); return; }
    try { await connection.invoke("SendDocumentUpdate", message); this.pending.delete(message.updateId); this.seen.add(message.updateId); this.setStatus(this.pending.size ? "synchronizing" : "synchronized"); }
    catch { this.setStatus("offline"); this.scheduleRetry(); }
  };
  private receiveDocument = (message: DocumentUpdateMessage) => this.applyDocument(message);
  private receiveReset = (message: DocumentUpdateMessage) => { this.seen.clear(); this.applyDocument(message); };
  private applyDocument(message: DocumentUpdateMessage) { if (message.fileId !== this.fileId || this.seen.has(message.updateId)) return; this.seen.add(message.updateId); Y.applyUpdate(this.doc, decodeBinary(message.encodedUpdate), remoteOrigin); }
  private onAwarenessUpdate = ({ added, updated, removed }: { added: number[]; updated: number[]; removed: number[] }, origin: unknown) => {
    if (origin === remoteOrigin || this.connection?.state !== HubConnectionState.Connected) return;
    const update = encodeAwarenessUpdate(this.awareness, [...added, ...updated, ...removed]);
    void this.connection.send("SendAwarenessUpdate", { projectId: this.projectId, fileId: this.fileId, clientId: this.clientId, updateId: crypto.randomUUID(), encodedUpdate: encodeBinary(update), updateType: "awareness", createdAt: new Date().toISOString() });
  };
  private receiveAwareness = (message: AwarenessUpdateMessage) => { if (message.fileId === this.fileId && message.clientId !== this.clientId) applyAwarenessUpdate(this.awareness, decodeBinary(message.encodedUpdate), remoteOrigin); };
  private setStatus(status: SyncStatus) { this.status = status; this.emit(); }
  private emit() { for (const listener of this.listeners) listener(this.status, this.pending.size); }
  private scheduleRetry() {
    if (this.disposed || this.retryTimer) return;
    const delay = this.retryDelay;
    this.retryDelay = Math.min(this.retryDelay * 2, 10_000);
    this.retryTimer = setTimeout(() => {
      this.retryTimer = undefined;
      if (this.disposed) return;
      if (this.connection?.state === HubConnectionState.Connected) void this.synchronize();
    }, delay);
  }
}

export { remoteOrigin };

async function deterministicUpdateId(update: Uint8Array) {
  const bytes = new Uint8Array(await crypto.subtle.digest("SHA-256", Uint8Array.from(update).buffer)).slice(0, 16); bytes[6] = (bytes[6] & 0x0f) | 0x40; bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((value) => value.toString(16).padStart(2, "0")).join(""); return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
