import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useToast } from "../contexts/ToastContext";
import {
  liveRoomsApi,
  type CreateLiveRoomInput,
} from "../features/live-rooms/api";
import type {
  LiveRoomMode,
  LiveRoomSummary,
} from "../features/live-rooms/types";
import { useProjects } from "../features/projects/hooks";

export function LiveRoomsPage() {
  const [rooms, setRooms] = useState<LiveRoomSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [open, setOpen] = useState(false);
  const projects = useProjects();
  const navigate = useNavigate();
  const { show } = useToast();
  const load = async () => {
    setLoading(true);
    setError("");
    try {
      setRooms(await liveRoomsApi.list());
    } catch (e) {
      const message = e instanceof Error ? e.message : "Rooms could not load.";
      setError(message);
      show(message, "error");
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => {
    void load();
  }, []);
  return (
    <main className="dashboard-content feature-page live-rooms-page">
      <header className="feature-heading">
        <div>
          <p className="dashboard-date">REAL-TIME COLLABORATION</p>
          <h1>Live coding rooms</h1>
          <p>
            Run interviews, workshops and pair-programming sessions with
            synchronized state and persistent chat.
          </p>
        </div>
        <button className="create-button" onClick={() => setOpen(true)}>
          + Create room
        </button>
      </header>
      {loading ? (
        <p className="marketplace-state" role="status">
          Loading rooms…
        </p>
      ) : error ? (
        <p className="marketplace-state" role="alert">
          {error} <button onClick={() => void load()}>Retry</button>
        </p>
      ) : rooms.length ? (
        <section className="room-grid">
          {rooms.map((room) => (
            <button
              className="room-card"
              key={room.id}
              onClick={() => navigate(`/live-rooms/${room.id}`)}
            >
              <span className={`room-status ${room.status.toLowerCase()}`}>
                {room.status}
              </span>
              <small>{room.mode.replace(/([A-Z])/g, " $1")}</small>
              <h2>{room.title}</h2>
              <p>
                {room.description ||
                  room.problemTitle ||
                  "Collaborative coding session"}
              </p>
              <footer>
                <span>{room.participantCount} participants</span>
                <span>
                  {room.durationMinutes
                    ? `${room.durationMinutes} min`
                    : "Open timer"}
                </span>
              </footer>
            </button>
          ))}
        </section>
      ) : (
        <p className="marketplace-state">
          No rooms yet. Create a private interview or project workshop.
        </p>
      )}
      {open && (
        <CreateRoomDialog
          projectOptions={projects.data ?? []}
          close={() => setOpen(false)}
          created={(room) => navigate(`/live-rooms/${room.id}`)}
        />
      )}
    </main>
  );
}

function CreateRoomDialog({
  projectOptions,
  close,
  created,
}: {
  projectOptions: Array<{ id: string; name: string }>;
  close: () => void;
  created: (room: LiveRoomSummary) => void;
}) {
  const [form, setForm] = useState({
    title: "",
    description: "",
    mode: "PairProgramming" as LiveRoomMode,
    projectId: "",
    duration: "60",
    challengeType: "CodingTask",
    problemTitle: "",
    problemStatement: "",
  });
  const [busy, setBusy] = useState(false);
  const { show } = useToast();
  const submit = async () => {
    const input: CreateLiveRoomInput = {
      title: form.title,
      description: form.description || undefined,
      mode: form.mode,
      visibility: form.projectId ? "ProjectMembers" : "InviteOnly",
      projectId: form.projectId || undefined,
      durationMinutes: Number(form.duration) || undefined,
      challengeType: form.mode === "Interview" ? form.challengeType : undefined,
      problemTitle: form.problemTitle || undefined,
      problemStatement: form.problemStatement || undefined,
    };
    setBusy(true);
    try {
      created((await liveRoomsApi.create(input)).room);
    } catch (e) {
      show(e instanceof Error ? e.message : "Room creation failed.", "error");
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="dialog-backdrop">
      <section className="dialog room-create-dialog">
        <header>
          <h2>Create live coding room</h2>
          <button onClick={close}>×</button>
        </header>
        <label>
          Title
          <input
            value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })}
          />
        </label>
        <label>
          Mode
          <select
            value={form.mode}
            onChange={(e) =>
              setForm({ ...form, mode: e.target.value as LiveRoomMode })
            }
          >
            <option value="PairProgramming">Pair programming</option>
            <option value="Interview">Interview</option>
            <option value="Workshop">Workshop</option>
            <option value="CommunityEvent">Community event</option>
          </select>
        </label>
        <label>
          Project
          <select
            value={form.projectId}
            onChange={(e) => setForm({ ...form, projectId: e.target.value })}
          >
            <option value="">No project — invite only</option>
            {projectOptions.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Duration (minutes)
          <input
            type="number"
            min="5"
            max="480"
            value={form.duration}
            onChange={(e) => setForm({ ...form, duration: e.target.value })}
          />
        </label>
        {form.mode === "Interview" && (
          <label>
            Challenge type
            <select
              value={form.challengeType}
              onChange={(e) =>
                setForm({ ...form, challengeType: e.target.value })
              }
            >
              <option>CodingTask</option>
              <option>Algorithm</option>
              <option>Architecture</option>
              <option>Debugging</option>
            </select>
          </label>
        )}
        <label>
          Problem title
          <input
            value={form.problemTitle}
            onChange={(e) => setForm({ ...form, problemTitle: e.target.value })}
          />
        </label>
        <label>
          Description
          <textarea
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
          />
        </label>
        <label>
          Problem statement
          <textarea
            value={form.problemStatement}
            onChange={(e) =>
              setForm({ ...form, problemStatement: e.target.value })
            }
          />
        </label>
        <footer>
          <button onClick={close}>Cancel</button>
          <button
            className="ui-button primary"
            disabled={busy || !form.title.trim()}
            onClick={() => void submit()}
          >
            {busy ? "Creating…" : "Create room"}
          </button>
        </footer>
      </section>
    </div>
  );
}
