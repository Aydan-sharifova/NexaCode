import type { SignalRYjsProvider } from "../crdt/SignalRYjsProvider";
import { useOfflineSyncStatus } from "../hooks/useOfflineSyncStatus";

const labels = { connecting: "Connecting", connected: "Connected", reconnecting: "Reconnecting", offline: "Offline changes", synchronizing: "Synchronizing", synchronized: "Synchronized", failed: "Synchronization failed" } as const;
export function CollaborationStatus({ provider }: { provider?: SignalRYjsProvider }) {
  const { status, pending } = useOfflineSyncStatus(provider);
  const pendingLabel = pending > 0 ? `${pending} change${pending === 1 ? "" : "s"} waiting to sync` : undefined;
  return <span className={`crdt-status ${status}`} aria-live="polite" title={pendingLabel}><i />{labels[status]}{pending > 0 ? ` · ${pending} pending` : ""}</span>;
}
