import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Icon } from "../../components/Icon";
import { usePageTranslation } from "../../hooks/usePageTranslation";
import { notificationApi } from "./api";
import { useNotificationStore } from "./notificationStore";
import { signalRService } from "../collaboration/signalRService";

export function NotificationBell() {
  const navigate = useNavigate(); const { pt, locale } = usePageTranslation(); const [open, setOpen] = useState(false); const controlRef = useRef<HTMLDivElement>(null);
  const { items, unreadCount, setPage, read } = useNotificationStore();
  useEffect(() => { void signalRService.connect().catch(() => undefined); void notificationApi.list().then((page) => setPage(page.items, page.unreadCount)); }, [setPage]);
  useEffect(() => {
    if (!open) return;
    const closeOnOutsideClick = (event: MouseEvent) => {
      if (!controlRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open]);
  const markRead = async (id: string) => { await notificationApi.read(id); read(id); };
  return <div className="notification-control" ref={controlRef}>
    <button
      className="icon-button notification-button"
      aria-label={pt("notifications")}
      aria-expanded={open}
      aria-haspopup="dialog"
      onClick={() => setOpen((value) => !value)}
    >
      <Icon name="bell" />
      {unreadCount > 0 && <span>{unreadCount > 99 ? "99+" : unreadCount}</span>}
    </button>
    {open && <div className="notification-dropdown" role="dialog" aria-label={pt("notifications")}>
      <header>
        <div><strong>{pt("notifications")}</strong>{unreadCount > 0 && <small>{unreadCount}</small>}</div>
        <button disabled={unreadCount === 0} onClick={async () => { await notificationApi.readAll(); read(); }}>{pt("markAllRead")}</button>
      </header>
      <div className="notification-dropdown-body">
        {items.slice(0, 6).map((item) => <button key={item.id} className={item.isRead ? "" : "unread"} onClick={() => { if (item.type === "Invitation" && item.relatedEntityId) { setOpen(false); navigate(`/notifications?invitation=${item.relatedEntityId}`); } else void markRead(item.id); }}>
          <i aria-hidden="true">{item.type.slice(0, 1)}</i>
          <span>
            <b>{item.title}</b>
            <span>{item.message}</span>
            <small>{new Date(item.createdAt).toLocaleString(locale)}{item.type === "Invitation" && item.relatedEntityId ? " · Review invitation" : ""}</small>
          </span>
          {!item.isRead && <em aria-label="Unread" />}
        </button>)}
        {!items.length && <div className="notification-empty">
          <span className="notification-empty-icon" aria-hidden="true"><Icon name="bell" /></span>
          <strong>{pt("allCaughtUp")}</strong>
          <p>{pt("notificationEmptyCopy")}</p>
        </div>}
      </div>
      <footer><button onClick={() => { setOpen(false); navigate("/notifications"); }}>{pt("viewNotificationCenter")} <span aria-hidden="true">→</span></button></footer>
    </div>}
  </div>;
}
