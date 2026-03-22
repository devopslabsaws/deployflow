import { create } from "zustand";
import { persist } from "zustand/middleware";

interface SidebarStore {
  isCollapsed: boolean;
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
}

interface UIStore {
  activeDialog: string | null;
  openDialog: (id: string) => void;
  closeDialog: () => void;
  commandPaletteOpen: boolean;
  setCommandPaletteOpen: (open: boolean) => void;
}

export const useSidebarStore = create<SidebarStore>()(
  persist(
    (set) => ({
      isCollapsed: false,
      toggleSidebar: () => set((state) => ({ isCollapsed: !state.isCollapsed })),
      setSidebarCollapsed: (collapsed: boolean) => set({ isCollapsed: collapsed }),
    }),
    { name: "deployflow-sidebar" }
  )
);

export const useUIStore = create<UIStore>((set) => ({
  activeDialog: null,
  openDialog: (id: string) => set({ activeDialog: id }),
  closeDialog: () => set({ activeDialog: null }),
  commandPaletteOpen: false,
  setCommandPaletteOpen: (open: boolean) => set({ commandPaletteOpen: open }),
}));
