/**
 * useConnectionStore.ts - Connection State Management
 *
 * Manages Dataverse connection state including active connection info,
 * connection status, and environment URL.
 */

import { create } from 'zustand';

export interface ConnectionInfo {
  name: string;
  url: string;
  environment: string;
  id?: string;
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

interface ConnectionState {
  /** Current connection info */
  connection: ConnectionInfo | null;
  /** Connection status */
  status: ConnectionStatus;
  /** Error message if connection failed */
  error: string | null;
  /** Whether PPTB APIs are available */
  apiAvailable: boolean;
}

interface ConnectionActions {
  setConnection: (connection: ConnectionInfo | null) => void;
  setStatus: (status: ConnectionStatus) => void;
  setError: (error: string | null) => void;
  setApiAvailable: (available: boolean) => void;
  reset: () => void;
}

const initialState: ConnectionState = {
  connection: null,
  status: 'disconnected',
  error: null,
  apiAvailable: false,
};

export const useConnectionStore = create<ConnectionState & ConnectionActions>()((set) => ({
  ...initialState,

  setConnection: (connection) =>
    set({ connection, status: connection ? 'connected' : 'disconnected', error: null }),

  setStatus: (status) => set({ status }),

  setError: (error) => set({ error, status: 'error' }),

  setApiAvailable: (apiAvailable) => set({ apiAvailable }),

  reset: () => set(initialState),
}));
