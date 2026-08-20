import { create } from "zustand";
import type { AppNotification } from "./types";
interface State {
  items: AppNotification[];
  unreadCount: number;
  setPage: (items: AppNotification[], unread: number) => void;
  mergePage: (items: AppNotification[], unread?: number) => void;
  receive: (item: AppNotification) => void;
  read: (id?: string) => void;
  setUnread: (count: number) => void;
}

const newestFirst = (items: AppNotification[]) =>
  [...items].sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());

export const useNotificationStore = create<State>((set) => ({
  items: [], unreadCount: 0,
  setPage: (items, unreadCount) => set({ items: newestFirst(items), unreadCount }),
  mergePage: (items, unreadCount) => set((state) => {
    const merged = new Map(state.items.map((item) => [item.id, item]));
    items.forEach((item) => merged.set(item.id, { ...merged.get(item.id), ...item }));
    return {
      items: newestFirst([...merged.values()]),
      unreadCount: unreadCount ?? state.unreadCount,
    };
  }),
  receive: (item) => set((state) => {
    const existing = state.items.find((current) => current.id === item.id);
    const addsUnread = !item.isRead && (!existing || existing.isRead);
    return {
      items: [item, ...state.items.filter((current) => current.id !== item.id)],
      unreadCount: state.unreadCount + (addsUnread ? 1 : 0),
    };
  }),
  read: (id) => set((state) => {
    const readAt = new Date().toISOString();
    const unreadMatches = id ? state.items.filter((item) => item.id === id && !item.isRead).length : state.unreadCount;
    return {
      items: state.items.map((item) => !id || item.id === id ? { ...item, isRead: true, readAt } : item),
      unreadCount: id ? Math.max(0, state.unreadCount - unreadMatches) : 0,
    };
  }),
  setUnread: (unreadCount) => set({ unreadCount }),
}));
