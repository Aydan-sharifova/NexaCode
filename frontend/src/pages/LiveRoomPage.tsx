import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useToast } from "../contexts/ToastContext";
import { signalRService } from "../features/collaboration/signalRService";
import { liveRoomsApi } from "../features/live-rooms/api";
import type {
  InterviewerNote,
  LiveRoomDetails,
  LiveRoomMessage,
  LiveRoomParticipant,
  LiveRoomReaction,
  LiveRoomRole,
  LiveRoomStateEvent,
  LiveRoomStatus,
  LiveRoomTask,
} from "../features/live-rooms/types";

export function LiveRoomPage() {
  const { roomId = "" } = useParams();
  const [data, setData] = useState<LiveRoomDetails>();
  const [loadError, setLoadError] = useState("");
  const [loading, setLoading] = useState(true);
  const [messages, setMessages] = useState<LiveRoomMessage[]>([]);
  const [tasks, setTasks] = useState<LiveRoomTask[]>([]);
  const [notes, setNotes] = useState<InterviewerNote[]>([]);
  const [text, setText] = useState("");
  const [taskTitle, setTaskTitle] = useState("");
  const [noteText, setNoteText] = useState("");
  const [reaction, setReaction] = useState<LiveRoomReaction>();
  const [inviteId, setInviteId] = useState("");
  const [inviteRole, setInviteRole] = useState<LiveRoomRole>("Participant");
  const [now, setNow] = useState(Date.now());
  const { show } = useToast();
  const navigate = useNavigate();
  const load = async () => {
    setLoading(true);
    setLoadError("");
    try {
      const joined = await liveRoomsApi.join(roomId);
      setData(joined);
      const [roomMessages, roomTasks] = await Promise.all([
        liveRoomsApi.messages(roomId),
        liveRoomsApi.tasks(roomId),
      ]);
      setMessages(roomMessages);
      setTasks(roomTasks);
      if (
        joined.room.mode === "Interview" &&
        ["Owner", "Host", "Interviewer"].includes(joined.room.currentUserRole)
      )
        setNotes(await liveRoomsApi.notes(roomId));
    } catch (e) {
      const message = e instanceof Error ? e.message : "Room could not load.";
      setLoadError(message);
      show(message, "error");
      return;
    } finally {
      setLoading(false);
    }
    try {
      await signalRService.joinLiveRoom(roomId);
    } catch {
      show("Room opened, but live synchronization is reconnecting.", "error");
    }
  };
  useEffect(() => {
    void load();
    const stateOff = signalRService.onLiveRoomState(
      (state: LiveRoomStateEvent) =>
        state.roomId === roomId &&
        setData((current) =>
          current
            ? {
                ...current,
                room: {
                  ...current.room,
                  status: state.status,
                  startedAt: state.startedAt,
                  completedAt: state.completedAt,
                  stateVersion: state.stateVersion,
                },
              }
            : current,
        ),
    );
    const participantOff = signalRService.onLiveRoomParticipant(
      ({
        roomId: eventRoomId,
        participant,
      }: {
        roomId: string;
        participant: LiveRoomParticipant;
      }) =>
        eventRoomId === roomId &&
        setData((current) =>
          current
            ? {
                ...current,
                participants: [
                  ...current.participants.filter(
                    (x) => x.user.id !== participant.user.id,
                  ),
                  participant,
                ],
              }
            : current,
        ),
    );
    const messageOff = signalRService.onLiveRoomMessage(
      (message) =>
        message.roomId === roomId &&
        setMessages((current) =>
          current.some((x) => x.id === message.id)
            ? current
            : [...current, message],
        ),
    );
    const taskOff = signalRService.onLiveRoomTask(
      (task) =>
        task.roomId === roomId &&
        setTasks((current) =>
          [...current.filter((x) => x.id !== task.id), task].sort((a, b) =>
            a.createdAt.localeCompare(b.createdAt),
          ),
        ),
    );
    const reactionOff = signalRService.onLiveRoomReaction((value) => {
      if (value.roomId === roomId) {
        setReaction(value);
        window.setTimeout(
          () =>
            setReaction((current) =>
              current?.id === value.id ? undefined : current,
            ),
          2200,
        );
      }
    });
    return () => {
      stateOff();
      participantOff();
      messageOff();
      taskOff();
      reactionOff();
      void signalRService.leaveLiveRoom(roomId);
    };
  }, [roomId]);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  const elapsed = useMemo(
    () =>
      data?.room.startedAt
        ? Math.max(
            0,
            Math.floor((now - new Date(data.room.startedAt).getTime()) / 1000),
          )
        : 0,
    [data?.room.startedAt, now],
  );
  if (loading && !data)
    return (
      <main className="dashboard-content">
        <p className="marketplace-state" role="status">
          Joining secure room…
        </p>
      </main>
    );
  if (loadError && !data)
    return (
      <main className="dashboard-content">
        <p className="marketplace-state" role="alert">
          {loadError} <button onClick={() => void load()}>Retry</button>
        </p>
      </main>
    );
  if (!data) return null;
  const transition = async (status: LiveRoomStatus) => {
    try {
      setData(
        await liveRoomsApi.status(roomId, status, data.room.stateVersion),
      );
    } catch (e) {
      show(e instanceof Error ? e.message : "Room state changed.", "error");
      void load();
    }
  };
  const send = async () => {
    if (!text.trim()) return;
    try {
      const sent = await liveRoomsApi.send(roomId, text);
      setMessages((current) =>
        current.some((x) => x.id === sent.id) ? current : [...current, sent],
      );
      setText("");
    } catch (e) {
      show(e instanceof Error ? e.message : "Message failed.", "error");
    }
  };
  const invite = async () => {
    try {
      setData(await liveRoomsApi.invite(roomId, inviteId, inviteRole));
      setInviteId("");
      show("Invitation sent.");
    } catch (e) {
      show(e instanceof Error ? e.message : "Invitation failed.", "error");
    }
  };
  const createTask = async () => {
    if (!taskTitle.trim()) return;
    try {
      const task = await liveRoomsApi.createTask(roomId, taskTitle);
      setTasks((current) =>
        current.some((x) => x.id === task.id) ? current : [...current, task],
      );
      setTaskTitle("");
    } catch (e) {
      show(e instanceof Error ? e.message : "Task failed.", "error");
    }
  };
  const toggleTask = async (task: LiveRoomTask) => {
    try {
      const changed = await liveRoomsApi.setTaskStatus(
        roomId,
        task.id,
        task.status === "Open" ? "Completed" : "Open",
      );
      setTasks((current) =>
        current.map((x) => (x.id === changed.id ? changed : x)),
      );
    } catch (e) {
      show(e instanceof Error ? e.message : "Task update failed.", "error");
    }
  };
  const saveNote = async () => {
    if (!noteText.trim()) return;
    try {
      const note = await liveRoomsApi.saveNote(roomId, noteText);
      setNotes((current) => [...current, note]);
      setNoteText("");
    } catch (e) {
      show(e instanceof Error ? e.message : "Note failed.", "error");
    }
  };
  const react = async (emoji: string) => {
    try {
      await liveRoomsApi.react(roomId, emoji);
    } catch (e) {
      show(e instanceof Error ? e.message : "Reaction failed.", "error");
    }
  };
  return (
    <main className="dashboard-content live-room">
      <header className="live-room-header">
        <button onClick={() => navigate("/live-rooms")}>← Rooms</button>
        <div>
          <span className={`room-status ${data.room.status.toLowerCase()}`}>
            {data.room.status}
          </span>
          <h1>{data.room.title}</h1>
          <p>
            {data.room.mode} · hosted by @{data.room.owner.userName}
          </p>
        </div>
        <div className="room-timer">
          <small>ELAPSED</small>
          <strong>
            {String(Math.floor(elapsed / 60)).padStart(2, "0")}:
            {String(elapsed % 60).padStart(2, "0")}
          </strong>
        </div>
        <div>
          {data.canStart && (
            <button
              className="ui-button primary"
              onClick={() => void transition("Active")}
            >
              Start
            </button>
          )}
          {data.canComplete && (
            <button
              className="ui-button primary"
              onClick={() => void transition("Completed")}
            >
              Complete
            </button>
          )}
        </div>
      </header>
      {reaction && (
        <div className="room-reaction-pop" aria-live="polite">
          <b>{reaction.emoji}</b>
          <span>{reaction.user.fullName}</span>
        </div>
      )}
      <section className="live-room-layout">
        <div className="room-main">
          <article className="room-problem">
            <small>{data.room.challengeType || data.room.mode}</small>
            <h2>{data.room.problemTitle || "Session brief"}</h2>
            <p>
              {data.problemStatement ||
                data.room.description ||
                "The host has not added a problem statement."}
            </p>
          </article>
          {data.room.projectId ? (
            <article className="room-workspace-callout">
              <div>
                <h2>Shared project workspace</h2>
                <p>
                  The project editor provides CRDT collaboration, cursors, AI
                  assistance, tests, isolated Run output and shared preview.
                </p>
              </div>
              <button
                className="ui-button primary"
                onClick={() =>
                  navigate(`/projects/${data.room.projectId}/workspace`)
                }
              >
                Open synchronized editor →
              </button>
            </article>
          ) : (
            <article className="room-workspace-callout">
              <div>
                <h2>Invite-only discussion room</h2>
                <p>
                  Attach a project when creating a room to enable the
                  synchronized editor and sandboxed execution tools.
                </p>
              </div>
            </article>
          )}
          <article className="room-tools">
            <header>
              <h2>Session tasks</h2>
              <div className="room-reactions">
                {["👍", "👏", "🎉", "💡", "❤️", "🚀"].map((emoji) => (
                  <button
                    key={emoji}
                    disabled={data.room.status !== "Active"}
                    onClick={() => void react(emoji)}
                  >
                    {emoji}
                  </button>
                ))}
              </div>
            </header>
            {tasks.length ? (
              tasks.map((task) => (
                <label
                  key={task.id}
                  className={task.status === "Completed" ? "done" : ""}
                >
                  <input
                    type="checkbox"
                    checked={task.status === "Completed"}
                    disabled={data.room.status !== "Active"}
                    onChange={() => void toggleTask(task)}
                  />
                  <span>
                    <strong>{task.title}</strong>
                    {task.description && <small>{task.description}</small>}
                  </span>
                </label>
              ))
            ) : (
              <p>No session tasks yet.</p>
            )}
            {data.canManage && data.room.status !== "Completed" && (
              <div className="room-task-create">
                <input
                  aria-label="New room task"
                  value={taskTitle}
                  onChange={(e) => setTaskTitle(e.target.value)}
                  placeholder="Add a workshop task…"
                />
                <button onClick={() => void createTask()}>Add</button>
              </div>
            )}
          </article>
          {data.room.mode === "Interview" &&
            ["Owner", "Host", "Interviewer"].includes(
              data.room.currentUserRole,
            ) && (
              <article className="room-notes">
                <h2>Private interviewer notes</h2>
                <p>
                  Visible only to interview staff. The candidate cannot access
                  these notes.
                </p>
                {notes.map((note) => (
                  <div key={note.id}>
                    <strong>{note.author.fullName}</strong>
                    <small>{new Date(note.updatedAt).toLocaleString()}</small>
                    <p>{note.content}</p>
                  </div>
                ))}
                <textarea
                  value={noteText}
                  onChange={(e) => setNoteText(e.target.value)}
                  placeholder="Add evidence-based interview notes…"
                />
                <button
                  onClick={() => void saveNote()}
                  disabled={!noteText.trim()}
                >
                  Save private note
                </button>
              </article>
            )}
          <article className="room-participants">
            <h2>Participants</h2>
            {data.participants.map((p) => (
              <div key={p.id}>
                <span className="participant-avatar">
                  {p.user.fullName[0] || "U"}
                </span>
                <div>
                  <strong>{p.user.fullName}</strong>
                  <small>
                    @{p.user.userName} · {p.role}
                  </small>
                </div>
                <b className={p.status.toLowerCase()}>{p.status}</b>
              </div>
            ))}
          </article>
        </div>
        <aside className="room-chat">
          <header>
            <h2>Room chat</h2>
            <span>Persisted</span>
          </header>
          <div className="room-messages">
            {messages.map((message) => (
              <div key={message.id}>
                <b>{message.author.fullName}</b>
                <small>
                  {new Date(message.sentAt).toLocaleTimeString([], {
                    hour: "2-digit",
                    minute: "2-digit",
                  })}
                </small>
                <p>{message.content}</p>
              </div>
            ))}
          </div>
          <footer>
            <textarea
              aria-label="Room message"
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder={
                data.room.status === "Completed"
                  ? "Room completed"
                  : "Message participants…"
              }
              disabled={data.room.status === "Completed"}
            />
            <button disabled={!text.trim()} onClick={() => void send()}>
              Send
            </button>
          </footer>
          {data.canManage && (
            <section className="room-invite">
              <h3>Invite by Public ID</h3>
              <input
                placeholder="@ABCD1234"
                value={inviteId}
                onChange={(e) => setInviteId(e.target.value)}
              />
              <select
                value={inviteRole}
                onChange={(e) => setInviteRole(e.target.value as LiveRoomRole)}
              >
                <option>Participant</option>
                <option>Candidate</option>
                <option>Interviewer</option>
                <option>Host</option>
              </select>
              <button disabled={!inviteId.trim()} onClick={() => void invite()}>
                Invite
              </button>
            </section>
          )}
        </aside>
      </section>
    </main>
  );
}
