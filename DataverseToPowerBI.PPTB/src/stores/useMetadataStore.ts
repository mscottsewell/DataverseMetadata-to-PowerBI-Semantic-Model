/**
 * useMetadataStore.ts - Dataverse Metadata Cache State
 *
 * Manages cached Dataverse metadata (solutions, tables, attributes, forms, views)
 * to minimize API calls and provide fast UI responsiveness.
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import type {
  DataverseSolution,
  TableInfo,
  AttributeMetadata,
  FormMetadata,
  ViewMetadata,
  MetadataCache,
} from '../types/DataModels';

interface MetadataState {
  /** Available solutions */
  solutions: DataverseSolution[];
  /** Tables in current solution */
  tables: TableInfo[];
  /** Per-table attributes */
  tableAttributes: Record<string, AttributeMetadata[]>;
  /** Per-table forms */
  tableForms: Record<string, FormMetadata[]>;
  /** Per-table views */
  tableViews: Record<string, ViewMetadata[]>;
  /** Loading flags */
  loading: {
    solutions: boolean;
    tables: boolean;
    attributes: Record<string, boolean>;
    forms: Record<string, boolean>;
    views: Record<string, boolean>;
  };
  /** Environment URL this metadata is for */
  environmentUrl: string | null;
  /** Solution this metadata is for */
  solutionName: string | null;
  /** Cache timestamp */
  cachedDate: string | null;
}

interface MetadataActions {
  // Solutions
  setSolutions: (solutions: DataverseSolution[]) => void;
  setLoadingSolutions: (loading: boolean) => void;

  // Tables
  setTables: (tables: TableInfo[]) => void;
  setLoadingTables: (loading: boolean) => void;

  // Attributes
  setTableAttributes: (table: string, attributes: AttributeMetadata[]) => void;
  setLoadingAttributes: (table: string, loading: boolean) => void;

  // Forms
  setTableForms: (table: string, forms: FormMetadata[]) => void;
  setLoadingForms: (table: string, loading: boolean) => void;

  // Views
  setTableViews: (table: string, views: ViewMetadata[]) => void;
  setLoadingViews: (table: string, loading: boolean) => void;

  // Cache management
  loadFromCache: (cache: MetadataCache) => void;
  toCache: () => MetadataCache;
  setEnvironmentUrl: (url: string | null) => void;
  setSolutionName: (name: string | null) => void;

  // Lookup helpers
  getTable: (logicalName: string) => TableInfo | undefined;
  getTableDisplayName: (logicalName: string) => string;

  reset: () => void;
}

const initialState: MetadataState = {
  solutions: [],
  tables: [],
  tableAttributes: {},
  tableForms: {},
  tableViews: {},
  loading: {
    solutions: false,
    tables: false,
    attributes: {},
    forms: {},
    views: {},
  },
  environmentUrl: null,
  solutionName: null,
  cachedDate: null,
};

export const useMetadataStore = create<MetadataState & MetadataActions>()(
  immer((set, get) => ({
    ...initialState,

    setSolutions: (solutions) =>
      set((state) => { state.solutions = solutions; state.loading.solutions = false; }),
    setLoadingSolutions: (loading) =>
      set((state) => { state.loading.solutions = loading; }),

    setTables: (tables) =>
      set((state) => { state.tables = tables; state.loading.tables = false; }),
    setLoadingTables: (loading) =>
      set((state) => { state.loading.tables = loading; }),

    setTableAttributes: (table, attributes) =>
      set((state) => { state.tableAttributes[table] = attributes; state.loading.attributes[table] = false; }),
    setLoadingAttributes: (table, loading) =>
      set((state) => { state.loading.attributes[table] = loading; }),

    setTableForms: (table, forms) =>
      set((state) => { state.tableForms[table] = forms; state.loading.forms[table] = false; }),
    setLoadingForms: (table, loading) =>
      set((state) => { state.loading.forms[table] = loading; }),

    setTableViews: (table, views) =>
      set((state) => { state.tableViews[table] = views; state.loading.views[table] = false; }),
    setLoadingViews: (table, loading) =>
      set((state) => { state.loading.views[table] = loading; }),

    loadFromCache: (cache) =>
      set((state) => {
        state.solutions = cache.solutions;
        state.tables = cache.tables;
        state.tableAttributes = cache.tableAttributes;
        state.tableForms = cache.tableForms;
        state.tableViews = cache.tableViews;
        state.environmentUrl = cache.environmentUrl ?? null;
        state.solutionName = cache.solutionName ?? null;
        state.cachedDate = cache.cachedDate;
      }),

    toCache: (): MetadataCache => {
      const s = get();
      return {
        environmentUrl: s.environmentUrl ?? undefined,
        solutionName: s.solutionName ?? undefined,
        cachedDate: s.cachedDate ?? new Date().toISOString(),
        solutions: s.solutions,
        tables: s.tables,
        tableData: Object.fromEntries(s.tables.map((t) => [t.logicalName, t])),
        tableForms: s.tableForms,
        tableViews: s.tableViews,
        tableAttributes: s.tableAttributes,
      };
    },

    setEnvironmentUrl: (url) => set((state) => { state.environmentUrl = url; }),
    setSolutionName: (name) => set((state) => { state.solutionName = name; }),

    getTable: (logicalName) => get().tables.find((t) => t.logicalName === logicalName),
    getTableDisplayName: (logicalName) => {
      const table = get().tables.find((t) => t.logicalName === logicalName);
      return table?.displayName ?? logicalName;
    },

    reset: () => set(() => ({ ...initialState })),
  }))
);
