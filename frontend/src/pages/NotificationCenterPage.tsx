import { useInfiniteQuery } from "@tanstack/react-query";
import { ErrorState } from "../components/AsyncState";
import { Icon } from "../components/Icon";
import { notificationApi } from "../features/notifications/api";
import { useNotificationStore } from "../features/notifications/notificationStore";
import { usePageTranslation } from "../hooks/usePageTranslation";
import { projectApi } from "../features/projects/api";
import { useToast } from "../contexts/ToastContext";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useEffect, useMemo, useState } from "react";

export function NotificationCenterPage() {
  const store = useNotificationStore();
  const { pt, locale } = usePageTranslation();
  const { show } = useToast();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const highlightedInvitation = searchParams.get("invitation");
  const [responding, setResponding] = useState<string>();
  const query = useInfiniteQuery({
    queryKey: ["notifications"],
    queryFn: ({ pageParam }) => notificationApi.list(pageParam),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (page) => page.nextCursor,
  });
  const queriedItems = useMemo(() => query.data?.pages.flatMap((page) => page.items) ?? [], [query.data?.pages]);
  const serverUnreadCount = query.data?.pages[0]?.unreadCount;

  useEffect(() => {
    if (query.data) store.mergePage(queriedItems, serverUnreadCount);
  }, [query.data, queriedItems, serverUnreadCount, store.mergePage]);

  const items = store.items;
  const unreadCount = items.filter((item) => !item.isRead).length;

  const openDirectMessage = async (notificationId: string, conversationId: string) => {
    store.read(notificationId);
    try {
      await notificationApi.read(notificationId);
    } catch {
      await query.refetch();
    } finally {
      navigate(`/chat?conversation=${conversationId}`);
    }
  };

  const markAllRead = async () => {
    store.read();
    try {
      await notificationApi.readAll();
    } catch (error) {
      await query.refetch();
      show(error instanceof Error ? error.message : "Unable to mark notifications as read.", "error");
    }
  };

  const respondToInvitation = async (notificationId: string, invitationId: string, accept: boolean) => {
    setResponding(invitationId);
    try {
      if (accept) {
        const result = await projectApi.acceptInvitationById(invitationId);
        store.read(notificationId);
        show("Invitation accepted. Welcome to the project.");
        navigate(`/projects/${result.projectId}/workspace`);
      } else {
        await projectApi.rejectInvitationById(invitationId);
        store.read(notificationId);
        show("Invitation rejected.");
        await query.refetch();
      }
    } catch (error) {
      show(error instanceof Error ? error.message : "Unable to respond to the invitation.", "error");
    } finally {
      setResponding(undefined);
    }
  };

  return (
    <main className="notification-center">
      <header>
        <div>
          <span>{pt("inbox")}</span>
          <h1>{pt("notifications")}</h1>
          <p>{pt("notificationCenterCopy")}</p>
        </div>
        <button disabled={unreadCount === 0} onClick={() => void markAllRead()}>
          <Icon name="check" />
          {pt("markAllRead")}
        </button>
      </header>

      {query.isError ? (
        <ErrorState message={query.error.message} retry={() => query.refetch()} />
      ) : (
        <section aria-label={pt("notifications")}>
          {query.isLoading && !items.length && (
            <div className="notification-center-loading" aria-label={pt("loadingNotifications")}>
              <i /><i /><i />
            </div>
          )}

          {items.map((item) => (
            <article key={item.id} className={`${item.isRead ? "" : "unread"} ${item.relatedEntityId === highlightedInvitation ? "highlighted" : ""}`}>
              <i aria-hidden="true">{item.type.slice(0, 1)}</i>
              <div>
                <header>
                  <strong>{item.title}</strong>
                  {!item.isRead && <span />}
                </header>
                <p>{item.message}</p>
                <small>{new Date(item.createdAt).toLocaleString(locale)}</small>
                {item.type === "Invitation" && item.relatedEntityId && <div className="invitation-actions">
                  <button disabled={responding === item.relatedEntityId} onClick={() => void respondToInvitation(item.id, item.relatedEntityId!, false)}>Reject</button>
                  <button className="accept" disabled={responding === item.relatedEntityId} onClick={() => void respondToInvitation(item.id, item.relatedEntityId!, true)}>{responding === item.relatedEntityId ? "Responding…" : "Accept invitation"}</button>
                </div>}
                {item.type === "DirectMessage" && item.relatedEntityId && <div className="invitation-actions">
                  <button className="accept" onClick={() => void openDirectMessage(item.id, item.relatedEntityId!)}>Open conversation</button>
                </div>}
              </div>
              {!item.isRead && (
                <button onClick={async () => {
                  store.read(item.id);
                  try {
                    await notificationApi.read(item.id);
                  } catch (error) {
                    await query.refetch();
                    show(error instanceof Error ? error.message : "Unable to update the notification.", "error");
                  }
                }}>
                  {pt("markRead")}
                </button>
              )}
            </article>
          ))}

          {!items.length && !query.isLoading && (
            <div className="notification-center-empty">
              <span aria-hidden="true"><Icon name="bell" /></span>
              <h2>{pt("noNotifications")}</h2>
              <p>{pt("notificationEmptyCopy")}</p>
            </div>
          )}

          {query.hasNextPage && (
            <div className="notification-center-more">
              <button disabled={query.isFetchingNextPage} onClick={() => query.fetchNextPage()}>
                {query.isFetchingNextPage ? pt("loadingNotifications") : pt("loadOlder")}
              </button>
            </div>
          )}
        </section>
      )}
    </main>
  );
}
