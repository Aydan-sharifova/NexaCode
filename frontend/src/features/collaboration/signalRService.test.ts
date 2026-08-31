import { beforeEach, describe, expect, it, vi } from "vitest";

let releaseStart!: () => void;
let connection: any;
const builder = { withUrl: vi.fn(() => builder), withAutomaticReconnect: vi.fn(() => builder), configureLogging: vi.fn(() => builder), build: vi.fn(() => connection) };

vi.mock("@microsoft/signalr", () => ({
  HubConnectionBuilder: vi.fn(() => builder),
  HubConnectionState: { Disconnected: "Disconnected", Connecting: "Connecting", Connected: "Connected", Reconnecting: "Reconnecting" },
  LogLevel: { Information: 2, Warning: 3 },
}));

import { SignalRService } from "./signalRService";

describe("SignalRService connection coordination", () => {
  beforeEach(() => {
    const started = new Promise<void>(resolve => { releaseStart = resolve; });
    connection = {
      state: "Disconnected",
      start: vi.fn(async () => { connection.state = "Connecting"; await started; connection.state = "Connected"; }),
      stop: vi.fn(async () => { connection.state = "Disconnected"; }),
      invoke: vi.fn(async () => undefined), send: vi.fn(async () => undefined), on: vi.fn(),
      onreconnecting: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(),
    };
    vi.clearAllMocks();
  });

  it("shares one pending handshake and never joins before it is connected", async () => {
    const service = new SignalRService();
    const first = service.joinProject("project-one");
    const second = service.joinProject("project-one");
    await Promise.resolve();
    expect(connection.start).toHaveBeenCalledOnce();
    expect(connection.invoke).not.toHaveBeenCalled();
    releaseStart();
    await Promise.all([first, second]);
    expect(connection.invoke).toHaveBeenCalledOnce();
    expect(connection.invoke).toHaveBeenCalledWith("JoinProject", "project-one");
    await service.disconnect();
  });
});
