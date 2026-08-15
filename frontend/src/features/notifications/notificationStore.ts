import { create } from "zustand";
import type { AppNotification } from "./types";
interface State { items: AppNotification[]; unreadCount: number; setPage: (items: AppNotification[], unread: number) => void; receive: (item: AppNotification) => void; read: (id?: string) => void; setUnread: (count: number) => void; }
export const useNotificationStore = create<State>((set) => ({
  items: [], unreadCount: 0,
  setPage: (items, unreadCount) => set({ items, unreadCount }),
  receive: (item) => set((state) => {
    const existing = state.items.find((current) => current.id === item.id);
    const addsUnread = !item.isRead && (!existing || existing.isRead);
    return {
      items: [item, ...state.items.filter((current) => current.id !== item.id)],
      unreadCount: state.unreadCount + (addsUnread ? 1 : 0),
    };
  }),
  read: (id) => set((state) => ({ items: state.items.map((item) => !id || item.id === id ? { ...item, isRead: true, readAt: new Date().toISOString() } : item), unreadCount: id ? Math.max(0, state.unreadCount - 1) : 0 })),
  setUnread: (unreadCount) => set({ unreadCount }),
}));
