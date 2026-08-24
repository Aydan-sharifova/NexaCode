import { useEffect, useRef, useState } from "react";
import { aiApi } from "./api";
import {
  AI_ATTACHMENT_ACCEPT,
  extractCodeSuggestion,
  formatAttachmentSize,
  MAX_AI_ATTACHMENTS,
  readAiAttachment,
  type PendingAiAttachment,
} from "./attachmentUtils";
import type { AiAction, AiMessage } from "./types";

interface AiAssistantPanelProps {
  projectId: string;
  fileId?: string;
  fileName?: string;
  language?: string;
  selectedCode?: string;
  fileContent?: string;
  contextText?: string;
  contextLabel?: string;
  onApplySuggestion: (content: string) => void;
  externalRequest?: { id: string; action: AiAction; message: string };
}

const actions: Array<{ action: AiAction; label: string }> = [
  { action: "GenerateCode", label: "Generate" },
  { action: "Explain", label: "Explain" },
  { action: "FindBug", label: "Find bug" },
  { action: "SuggestFix", label: "Fix" },
  { action: "Optimize", label: "Optimize" },
  { action: "GenerateTests", label: "Tests" },
  { action: "Refactor", label: "Refactor" },
];

export function AiAssistantPanel({
  projectId,
  fileId,
  fileName,
  language,
  selectedCode,
  fileContent,
  contextText,
  contextLabel,
  onApplySuggestion,
  externalRequest,
}: AiAssistantPanelProps) {
  const [messages, setMessages] = useState<AiMessage[]>([]);
  const [message, setMessage] = useState("");
  const [conversationId, setConversationId] = useState<string>();
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState<string>();
  const [attachments, setAttachments] = useState<PendingAiAttachment[]>([]);
  const controller = useRef<AbortController | undefined>(undefined);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    return () => {
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const submit = async (action: AiAction, explicitMessage?: string) => {
    const userMessage = (explicitMessage ?? message).trim();
    if (!userMessage && !selectedCode && attachments.length === 0 && action === "Chat") return;
    if (!userMessage && !selectedCode && !fileId && !contextText && attachments.length === 0) return;
    controller.current?.abort();
    controller.current = new AbortController();
    setStreaming(true);
    setError(undefined);
    setMessage("");
    const submittedAttachments = attachments;
    setAttachments([]);

    const user: AiMessage = {
      id: crypto.randomUUID(),
      role: "User",
      content: userMessage || (submittedAttachments.length
        ? `Analyze ${submittedAttachments.map((attachment) => attachment.fileName).join(", ")}.`
        : `${action} using the ${
          selectedCode ? "selected code" : contextText ? "available project context" : "current file"
        }.`),
      action,
      fileId,
      attachmentNames: submittedAttachments.map((attachment) => attachment.fileName),
      createdAt: new Date().toISOString(),
    };
    const assistantId = crypto.randomUUID();
    setMessages((current) => [...current, user, { id: assistantId, role: "Assistant", content: "", action, fileId, createdAt: new Date().toISOString() }]);

    try {
      await aiApi.stream({
        projectId,
        userMessage: user.content,
        action,
        conversationId,
        currentFileId: fileId,
        selectedCode,
        neighboringCode: selectedCode
          ? fileContent?.slice(0, 4_000)
          : contextText?.slice(0, 4_000),
        programmingLanguage: language,
        attachments: submittedAttachments.map(({ fileName, mediaType, content, isImage }) => ({
          fileName,
          mediaType,
          content,
          isImage,
        })),
      }, controller.current.signal, (chunk) => {
        if (chunk.conversationId) setConversationId(chunk.conversationId);
        if (chunk.content) {
          setMessages((current) => current.map((item) =>
            item.id === assistantId ? { ...item, content: item.content + chunk.content } : item));
        }
      });
    } catch (reason) {
      if (!controller.current.signal.aborted)
        setError(reason instanceof Error ? reason.message : "AI generation failed.");
    } finally {
      setStreaming(false);
    }
  };

  const handledExternalRequest = useRef<string | undefined>(undefined);
  useEffect(() => {
    if (!externalRequest || handledExternalRequest.current === externalRequest.id) return;
    handledExternalRequest.current = externalRequest.id;
    void submit(externalRequest.action, externalRequest.message);
  }, [externalRequest?.id]);

  const addAttachments = async (files: FileList | null) => {
    if (!files?.length) return;
    const available = MAX_AI_ATTACHMENTS - attachments.length;
    if (available <= 0) {
      setError(`You can attach up to ${MAX_AI_ATTACHMENTS} files.`);
      return;
    }

    const selected = Array.from(files).slice(0, available);
    const results = await Promise.allSettled(selected.map(readAiAttachment));
    const accepted = results
      .filter((result): result is PromiseFulfilledResult<PendingAiAttachment> => result.status === "fulfilled")
      .map((result) => result.value);
    const rejected = results.find((result): result is PromiseRejectedResult => result.status === "rejected");
    setAttachments((current) => [...current, ...accepted]);
    if (files.length > available)
      setError(`Only ${MAX_AI_ATTACHMENTS} files can be attached at once.`);
    else if (rejected)
      setError(rejected.reason instanceof Error ? rejected.reason.message : "An attachment could not be read.");
    else
      setError(undefined);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const lastAssistant = [...messages].reverse().find((item) => item.role === "Assistant" && item.content);
  const hasContext = Boolean(fileId || selectedCode || fileContent || contextText);
  return (
    <section className="ai-assistant-panel" aria-label="AI assistant">
      <header>
        <div>
          <span className="ai-mark">AI</span>
          <div><strong>Assistant</strong><small>{fileName ? `Context: ${fileName}` : "Open a file for code context"}</small></div>
        </div>
        {streaming && <button onClick={() => controller.current?.abort()}>Stop</button>}
      </header>

      <div className="ai-actions">
        {actions.map((item) => (
          <button key={item.action} disabled={!hasContext && item.action !== "Chat"} onClick={() => void submit(item.action)}>
            {item.label}
          </button>
        ))}
      </div>

      {(selectedCode || contextText) && (
        <div className="ai-context-chip">
          <span>{selectedCode ? "Selected code" : contextLabel ?? "Project context"}</span>
          <b>{(selectedCode ?? contextText ?? "").length} chars</b>
        </div>
      )}

      <div className="ai-messages" aria-live="polite">
        {!messages.length && (
          <div className="ai-welcome">
            <span>✦</span>
            <strong>Ask about your code</strong>
            <p>Generate, explain, fix, optimize, refactor, test, or ask about project context.</p>
          </div>
        )}
        {messages.map((item) => (
          <article key={item.id} className={item.role.toLowerCase()}>
            <small>{item.role === "Assistant" ? "AI" : "You"}</small>
            {item.role === "Assistant"
              ? <AssistantResponse content={item.content || (streaming ? "Thinking…" : "")} />
              : <p>{item.content}</p>}
            {item.attachmentNames?.length ? (
              <ul className="ai-message-attachments">
                {item.attachmentNames.map((name) => <li key={name}>⌕ {name}</li>)}
              </ul>
            ) : null}
            {item.role === "Assistant" && item.content && (
              <div className="ai-message-actions">
                <button onClick={() => void navigator.clipboard.writeText(item.content)}>Copy</button>
                {fileId && extractCodeSuggestion(item.content) && (
                  <button onClick={() => onApplySuggestion(extractCodeSuggestion(item.content)!)}>Apply code</button>
                )}
              </div>
            )}
          </article>
        ))}
        {error && <div className="ai-error" role="alert">{error}<button onClick={() => setError(undefined)}>×</button></div>}
        <div ref={endRef} />
      </div>

      <footer>
        {attachments.length > 0 && (
          <div className="ai-attachment-list" aria-label="Attachments">
            {attachments.map((attachment) => (
              <div className="ai-attachment-chip" key={attachment.id}>
                {attachment.previewUrl
                  ? <img src={attachment.previewUrl} alt="" />
                  : <span aria-hidden="true">⌕</span>}
                <div>
                  <strong title={attachment.fileName}>{attachment.fileName}</strong>
                  <small>{attachment.isImage ? "Image" : "Text"} · {formatAttachmentSize(attachment.size)}</small>
                </div>
                <button
                  type="button"
                  aria-label={`Remove ${attachment.fileName}`}
                  onClick={() => setAttachments((current) => current.filter((item) => item.id !== attachment.id))}
                >×</button>
              </div>
            ))}
          </div>
        )}
        <div className="ai-composer-row">
          <input
            ref={fileInputRef}
            className="sr-only"
            type="file"
            multiple
            accept={AI_ATTACHMENT_ACCEPT}
            onChange={(event) => void addAttachments(event.target.files)}
          />
          <button
            type="button"
            className="ai-attach-button"
            title="Attach code, text, or an image"
            aria-label="Attach code, text, or an image"
            disabled={streaming || attachments.length >= MAX_AI_ATTACHMENTS}
            onClick={() => fileInputRef.current?.click()}
          >＋</button>
          <textarea
            value={message}
            onChange={(event) => setMessage(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                if (!streaming) void submit("Chat");
              }
            }}
            placeholder={attachments.length
              ? "What should AI analyze?"
              : fileId ? "Ask about this file…" : "Ask a software question…"}
          />
          <button
            className="ai-send-button"
            disabled={streaming || (!message.trim() && !selectedCode && attachments.length === 0)}
            onClick={() => void submit("Chat")}
          >Send</button>
        </div>
      </footer>
      {lastAssistant && <small className="ai-safety-note">AI suggestions are never applied without confirmation.</small>}
    </section>
  );
}

function AssistantResponse({ content }: { content: string }) {
  const parts: Array<{ kind: "text" | "code"; value: string; language?: string }> = [];
  const expression = /```([^\n`]*)\n([\s\S]*?)```/g;
  let cursor = 0;
  for (const match of content.matchAll(expression)) {
    if (match.index > cursor)
      parts.push({ kind: "text", value: content.slice(cursor, match.index) });
    parts.push({ kind: "code", value: match[2].trimEnd(), language: match[1].trim() });
    cursor = match.index + match[0].length;
  }
  if (cursor < content.length)
    parts.push({ kind: "text", value: content.slice(cursor) });

  return (
    <div className="ai-message-content">
      {parts.map((part, index) => part.kind === "code"
        ? (
          <pre key={index}>
            {part.language && <small>{part.language}</small>}
            <code>{part.value}</code>
          </pre>
        )
        : <p key={index}>{part.value}</p>)}
    </div>
  );
}
