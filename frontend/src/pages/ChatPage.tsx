import { useEffect, useMemo, useRef, useState } from "react";
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { chatApi } from "../features/chat/api";
import type { ChatMessage, Conversation } from "../features/chat/types";
import { signalRService } from "../features/collaboration/signalRService";
import { useAuth } from "../hooks/useAuth";
import { usePageTranslation } from "../hooks/usePageTranslation";
import { Dialog } from "../components/ui/Dialog";
import { AiAssistantPanel } from "../features/ai/AiAssistantPanel";
import { useToast } from "../contexts/ToastContext";
import { conversationsQueryKey, upsertConversation } from "../features/chat/conversationCache";
import { Link, useSearchParams } from "react-router-dom";

export function ChatPage() {
  const { pt } = usePageTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const client = useQueryClient(); const { session } = useAuth(); const { show } = useToast(); const [active, setActive] = useState<string>(); const [content, setContent] = useState(""); const [editing, setEditing] = useState<ChatMessage>(); const [otherUserId, setOtherUserId] = useState(""); const [typingIds, setTypingIds] = useState<string[]>([]); const [aiOpen, setAiOpen] = useState(false); const typingTimer = useRef<number | undefined>(undefined); const fileInput = useRef<HTMLInputElement>(null);
  const conversations = useQuery({ queryKey: conversationsQueryKey, queryFn: chatApi.conversations, refetchInterval: 15_000 });
  useEffect(() => {
    if (!conversations.data?.length) return;
    const requested = searchParams.get("conversation");
    const requestedConversation = conversations.data.find((item) => item.id === requested);
    if (active && !requestedConversation) return;
    const selected = requestedConversation ?? conversations.data[0];
    setActive(selected.id);
    if (requestedConversation) setSearchParams({}, { replace: true });
  }, [active, conversations.data, searchParams, setSearchParams]);
  const messages = useInfiniteQuery({ queryKey: ["chat-messages", active], enabled: Boolean(active), queryFn: ({ pageParam }) => chatApi.messages(active!, pageParam), initialPageParam: undefined as string | undefined, getNextPageParam: (page) => page.nextCursor });
  useEffect(() => { if (!active) return; void signalRService.joinConversation(active); const offMessage = signalRService.onMessage((message) => { if (message.conversationId === active) void messages.refetch(); void conversations.refetch(); }); const offUpdated = signalRService.onConversationUpdated(() => void conversations.refetch()); const offTyping = signalRService.onChatTyping((event) => { if (event.conversationId === active) setTypingIds((ids) => event.typing ? [...new Set([...ids, event.userId])] : ids.filter((id) => id !== event.userId)); }); return () => { offMessage(); offUpdated(); offTyping(); void signalRService.leaveConversation(active); }; }, [active]);
  const send = useMutation({
    mutationFn: () => chatApi.send(active!, content),
    onSuccess: () => {
      setContent("");
      signalRService.stopChatTyping(active!);
      void messages.refetch();
      void conversations.refetch();
    },
    onError: (error) => show(error instanceof Error ? error.message : "Unable to send the message.", "error"),
  });
  const createDirect = useMutation({
    mutationFn: (userId: string) => chatApi.direct(userId),
    onSuccess: (conversation) => {
      setOtherUserId("");
      client.setQueryData<Conversation[]>(conversationsQueryKey, (current) =>
        upsertConversation(current, conversation));
      setActive(conversation.id);
      void client.invalidateQueries({ queryKey: conversationsQueryKey });
      show("Direct conversation opened.");
    },
    onError: (error) => show(error instanceof Error ? error.message : "Unable to start the conversation.", "error"),
  });
  const refreshChat = () => { void messages.refetch(); void conversations.refetch(); };
  const editMessage = useMutation({ mutationFn: () => chatApi.edit(editing!.id, content), onSuccess: () => { setEditing(undefined); setContent(""); refreshChat(); show("Message updated."); }, onError: (error) => show(error.message, "error") });
  const removeMessage = useMutation({ mutationFn: chatApi.remove, onSuccess: () => { refreshChat(); show("Message deleted."); }, onError: (error) => show(error.message, "error") });
  const removeConversation = useMutation({ mutationFn: chatApi.deleteConversation, onSuccess: () => { setActive(undefined); void conversations.refetch(); show("Conversation deleted."); }, onError: (error) => show(error.message, "error") });
  const upload = useMutation({ mutationFn: (file: File) => chatApi.upload(active!, file, content), onSuccess: () => { setContent(""); refreshChat(); show("File sent."); }, onError: (error) => show(error.message, "error") });
  const downloadAttachment = async (id: string, fileName: string) => { try { const blob = await chatApi.attachment(id); const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = fileName; anchor.click(); URL.revokeObjectURL(url); } catch (error) { show(error instanceof Error ? error.message : "Download failed.", "error"); } };
  const startDirect = () => {
    const identifier = otherUserId.trim().replace(/^@/, "");
    if (identifier.length < 2) {
      show("Enter a Public ID or username.", "error");
      return;
    }
    createDirect.mutate(identifier);
  };
  const orderedMessages = useMemo(
    () => [...(messages.data?.pages.flatMap((page) => page.items) ?? [])].reverse(),
    [messages.data?.pages],
  );
  const selected = conversations.data?.find((item) => item.id === active);
  const directParticipant = selected?.type === "Direct"
    ? selected.participants.find((participant) => participant.id !== session?.user.id)
    : undefined;
  const directProfileIdentifier = directParticipant?.publicId || directParticipant?.userName || directParticipant?.id;
  const unreadCount = conversations.data?.reduce((sum, item) => sum + item.unreadCount, 0) ?? 0;
  useEffect(() => {
    if (!selected?.lastMessage || selected.unreadCount === 0) return;
    void chatApi.read(selected.id, selected.lastMessage.id).then(() => conversations.refetch());
  }, [selected?.id, selected?.lastMessage?.id, selected?.unreadCount]);

  return (
    <main className="chat-page">
      <aside className="chat-sidebar">
        <header>
          <div>
            <span>{pt("messages")}</span>
            <h1>Chat</h1>
          </div>
          <b aria-label={`${unreadCount} unread`}>{unreadCount}</b>
        </header>

        <div className="direct-create">
          <input
            aria-label={pt("userIdDm")}
            value={otherUserId}
            onChange={(event) => setOtherUserId(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && otherUserId.trim()) startDirect();
            }}
            placeholder={pt("userIdDm")}
          />
          <button
            aria-label={pt("userIdDm")}
            disabled={!otherUserId.trim() || createDirect.isPending}
            onClick={startDirect}
          >
            {createDirect.isPending ? "…" : "＋"}
          </button>
        </div>
        <p className="direct-create-help">Paste the ID shown in Settings → Profile.</p>

        <nav aria-label={pt("messages")}>
          {conversations.isPending && (
            <div className="chat-list-loading" aria-label="Loading">
              <i /><i /><i />
            </div>
          )}
          {conversations.data?.map((conversation) => (
            <ConversationButton
              key={conversation.id}
              item={conversation}
              active={active === conversation.id}
              emptyLabel={pt("noMessages")}
              onClick={async () => {
                setActive(conversation.id);
                if (conversation.lastMessage) await chatApi.read(conversation.id, conversation.lastMessage.id);
              }}
            />
          ))}
          {!conversations.isPending && !conversations.data?.length && (
            conversations.isError ? (
              <div className="chat-list-empty" role="alert">
                <span aria-hidden="true">!</span>
                <p>Conversations could not be loaded.</p>
                <button type="button" onClick={() => void conversations.refetch()}>Try again</button>
              </div>
            ) : (
              <div className="chat-list-empty">
                <span aria-hidden="true">•••</span>
                <p>{pt("noConversations")}</p>
              </div>
            )
          )}
        </nav>
      </aside>

      <section className="chat-thread">
        {selected ? (
          <>
            <header>
              <div className="chat-avatar">{selected.type === "ProjectChannel" ? "#" : selected.name.slice(0, 1)}</div>
              <div>
                {selected.type === "Direct" && directProfileIdentifier
                  ? <Link className="chat-user-profile-link" to={`/users/${encodeURIComponent(directProfileIdentifier)}`}>{selected.name}</Link>
                  : <strong>{selected.name}</strong>}
                <small>{selected.type === "ProjectChannel" ? pt("projectChannel") : pt("directConversation")}</small>
              </div>
              {selected.projectId && (
                <button className="chat-ai-button" onClick={() => setAiOpen(true)}>
                  ✦ Ask AI
                </button>
              )}
              {selected.type === "Direct" && <button className="chat-delete-conversation" onClick={() => { if (window.confirm(`Delete conversation with ${selected.name}?`)) removeConversation.mutate(selected.id); }}>Delete chat</button>}
            </header>
            <div className="chat-messages">
              {messages.isError && (
                <div className="chat-list-empty" role="alert">
                  <p>Messages could not be loaded.</p>
                  <button type="button" onClick={() => void messages.refetch()}>Try again</button>
                </div>
              )}
              {messages.hasNextPage && <button className="older-messages" onClick={() => messages.fetchNextPage()}>{pt("olderMessages")}</button>}
              {orderedMessages.map((message) => (
                <article key={message.id} className={message.sender.id === session?.user.id ? "mine" : ""}>
                  <div className="chat-message-avatar">{message.sender.displayName.slice(0, 1)}</div>
                  <div>
                    <header><Link className="chat-user-profile-link" to={`/users/${encodeURIComponent(message.sender.publicId || message.sender.userName || message.sender.id)}`}>{message.sender.displayName}</Link><time>{new Date(message.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}{message.editedAt ? " · edited" : ""}</time>{message.sender.id === session?.user.id && !message.isDeleted && <span className="chat-message-actions"><button onClick={() => { setEditing(message); setContent(message.content); }}>Edit</button><button onClick={() => { if (window.confirm("Delete this message?")) removeMessage.mutate(message.id); }}>Delete</button></span>}</header>
                    <p>{message.content}</p>
                    {message.attachments?.map((attachment) => <button className="chat-attachment" key={attachment.id} onClick={() => void downloadAttachment(attachment.id, attachment.fileName)}><span>📎 {attachment.fileName}</span><small>{formatFileSize(attachment.size)} · Download</small></button>)}
                    {message.sender.id === session?.user.id && <small>{message.readByUserIds.length > 1 ? pt("read") : pt("sent")}</small>}
                  </div>
                </article>
              ))}
            </div>
            <div className="typing-line" role="status">{typingIds.length ? `${typingIds.length} ${typingIds.length === 1 ? pt("typingOne") : pt("typingMany")}` : ""}</div>
            <footer>
              <input ref={fileInput} hidden type="file" onChange={(event) => { const file = event.target.files?.[0]; if (file) upload.mutate(file); event.target.value = ""; }} />
              <button className="chat-attach-button" aria-label="Attach file" disabled={upload.isPending} onClick={() => fileInput.current?.click()}>＋</button>
              {editing && <button className="chat-cancel-edit" onClick={() => { setEditing(undefined); setContent(""); }}>Cancel edit</button>}
              <textarea
                aria-label={`${pt("send")}: ${selected.name}`}
                value={content}
                onChange={(event) => {
                  setContent(event.target.value);
                  signalRService.startChatTyping(selected.id);
                  if (typingTimer.current) clearTimeout(typingTimer.current);
                  typingTimer.current = window.setTimeout(() => signalRService.stopChatTyping(selected.id), 1000);
                }}
                onKeyDown={(event) => {
                  if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    if (content.trim() && !send.isPending) editing ? editMessage.mutate() : send.mutate();
                  }
                }}
                placeholder={`${pt("send")}: ${selected.name}`}
              />
              <button disabled={!content.trim() || send.isPending || editMessage.isPending} onClick={() => editing ? editMessage.mutate() : send.mutate()}>{editing ? "Save" : pt("send")}</button>
            </footer>
            {selected.projectId && (
              <Dialog
                open={aiOpen}
                title="AI coding assistant"
                description={`Project channel: ${selected.name}`}
                onClose={() => setAiOpen(false)}
              >
                <div className="project-ai-dialog chat-ai-dialog">
                  <AiAssistantPanel
                    projectId={selected.projectId}
                    fileName={selected.name}
                    contextLabel="Recent channel messages"
                    contextText={orderedMessages
                      .slice(-20)
                      .map((item) => `${item.sender.displayName}: ${item.content}`)
                      .join("\n")}
                    onApplySuggestion={() => undefined}
                  />
                </div>
              </Dialog>
            )}
          </>
        ) : (
          <div className="chat-empty-state">
            <div className="chat-empty-visual" aria-hidden="true">
              <span />
              <span />
            </div>
            <h2>{pt("selectConversation")}</h2>
            <p>{pt("conversationCopy")}</p>
          </div>
        )}
      </section>
    </main>
  );
}

function formatFileSize(size: number) { return size < 1024 ? `${size} B` : size < 1024 * 1024 ? `${(size / 1024).toFixed(1)} KB` : `${(size / 1024 / 1024).toFixed(1)} MB`; }

function ConversationButton({ item, active, emptyLabel, onClick }: { item: Conversation; active: boolean; emptyLabel:string; onClick: () => void }) {
  return <button className={active ? "active" : ""} onClick={onClick}><span className="chat-avatar">{item.type === "ProjectChannel" ? "#" : item.name.slice(0, 1)}</span><div><strong>{item.name}</strong><small>{item.lastMessage?.content ?? emptyLabel}</small></div>{item.unreadCount > 0 && <b>{item.unreadCount}</b>}</button>;
}
