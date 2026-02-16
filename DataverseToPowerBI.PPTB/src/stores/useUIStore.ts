/**
 * useUIStore.ts - UI State Management
 *
 * Manages UI-specific state: active tab, loading indicators,
 * toast messages, dialog visibility, and search filters.
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';

export type AppTab = 'setup' | 'tables' | 'schema' | 'attributes' | 'build';

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message?: string;
  duration?: number;
}

interface UIState {
  /** Currently active tab */
  activeTab: AppTab;
  /** Global loading state */
  globalLoading: boolean;
  /** Loading message */
  loadingMessage: string | null;
  /** Toast messages queue */
  toasts: ToastMessage[];
  /** Search/filter text for table list */
  tableSearchText: string;
  /** Search/filter text for attribute list */
  attributeSearchText: string;
  /** Currently selected table in attribute view */
  selectedAttributeTable: string | null;
  /** Dialog visibility flags */
  dialogs: {
    calendarTable: boolean;
    tmdlPreview: boolean;
    changePreview: boolean;
    configManager: boolean;
    formPicker: boolean;
    viewPicker: boolean;
  };
  /** Form/View picker context */
  pickerContext: {
    tableName: string | null;
  };
}

interface UIActions {
  setActiveTab: (tab: AppTab) => void;
  setGlobalLoading: (loading: boolean, message?: string | null) => void;
  addToast: (toast: Omit<ToastMessage, 'id'>) => void;
  removeToast: (id: string) => void;
  clearToasts: () => void;
  setTableSearchText: (text: string) => void;
  setAttributeSearchText: (text: string) => void;
  setSelectedAttributeTable: (table: string | null) => void;
  openDialog: (dialog: keyof UIState['dialogs'], context?: { tableName?: string }) => void;
  closeDialog: (dialog: keyof UIState['dialogs']) => void;
  reset: () => void;
}

const initialState: UIState = {
  activeTab: 'setup',
  globalLoading: false,
  loadingMessage: null,
  toasts: [],
  tableSearchText: '',
  attributeSearchText: '',
  selectedAttributeTable: null,
  dialogs: {
    calendarTable: false,
    tmdlPreview: false,
    changePreview: false,
    configManager: false,
    formPicker: false,
    viewPicker: false,
  },
  pickerContext: {
    tableName: null,
  },
};

let toastCounter = 0;

export const useUIStore = create<UIState & UIActions>()(
  immer((set) => ({
    ...initialState,

    setActiveTab: (tab) => set((state) => { state.activeTab = tab; }),

    setGlobalLoading: (loading, message) =>
      set((state) => { state.globalLoading = loading; state.loadingMessage = message ?? null; }),

    addToast: (toast) =>
      set((state) => {
        state.toasts.push({ ...toast, id: `toast-${++toastCounter}` });
      }),

    removeToast: (id) =>
      set((state) => { state.toasts = state.toasts.filter((t) => t.id !== id); }),

    clearToasts: () => set((state) => { state.toasts = []; }),

    setTableSearchText: (text) => set((state) => { state.tableSearchText = text; }),

    setAttributeSearchText: (text) => set((state) => { state.attributeSearchText = text; }),

    setSelectedAttributeTable: (table) => set((state) => { state.selectedAttributeTable = table; }),

    openDialog: (dialog, context) =>
      set((state) => {
        state.dialogs[dialog] = true;
        if (context?.tableName) state.pickerContext.tableName = context.tableName;
      }),

    closeDialog: (dialog) =>
      set((state) => { state.dialogs[dialog] = false; }),

    reset: () => set(() => ({ ...initialState })),
  }))
);
