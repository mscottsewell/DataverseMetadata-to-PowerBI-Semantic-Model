/**
 * useConfigStore.ts - Configuration State Management
 *
 * Manages the active semantic model configuration including selected tables,
 * relationships, attributes, and all settings. Integrates Immer for immutable
 * updates and Zundo for undo/redo.
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { temporal } from 'zundo';
import type {
  AppSettings,
  RelationshipConfig,
  DateTableConfig,
  TableDisplayInfo,
  AttributeDisplayInfo,
} from '../types/DataModels';
import { TableRole } from '../types/DataModels';
import { ConnectionMode, StorageMode, DEFAULT_CONNECTION_MODE, DEFAULT_STORAGE_MODE } from '../types/Constants';

interface ConfigState {
  /** Current configuration name */
  configName: string;
  /** Whether the config has been modified since last save */
  isDirty: boolean;
  /** Project name for semantic model */
  projectName: string;
  /** Connection mode */
  connectionMode: ConnectionMode;
  /** Global storage mode */
  storageMode: StorageMode;
  /** Per-table storage mode overrides */
  tableStorageModes: Record<string, StorageMode>;
  /** Selected solution unique name */
  selectedSolution: string | null;
  /** Output folder path */
  outputFolder: string | null;
  /** Selected table logical names */
  selectedTables: string[];
  /** Fact table logical name */
  factTable: string | null;
  /** Table roles */
  tableRoles: Record<string, TableRole>;
  /** Relationships */
  relationships: RelationshipConfig[];
  /** Per-table form selections */
  tableForms: Record<string, string>;
  tableFormNames: Record<string, string>;
  /** Per-table view selections */
  tableViews: Record<string, string>;
  tableViewNames: Record<string, string>;
  /** Per-table attribute selections */
  tableAttributes: Record<string, string[]>;
  /** Table display info */
  tableDisplayInfo: Record<string, TableDisplayInfo>;
  /** Attribute display info */
  attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>;
  /** Attribute display name overrides */
  attributeDisplayNameOverrides: Record<string, Record<string, string>>;
  /** Date table configuration */
  dateTableConfig: DateTableConfig | null;
  /** FabricLink settings */
  fabricLinkEndpoint: string | null;
  fabricLinkDatabase: string | null;
  /** Misc settings */
  useDisplayNameAliasesInSql: boolean;
  showAllAttributes: boolean;
  autoloadCache: boolean;
}

interface ConfigActions {
  // Configuration lifecycle
  loadFromSettings: (name: string, settings: AppSettings) => void;
  toSettings: () => AppSettings;
  setConfigName: (name: string) => void;
  setProjectName: (name: string) => void;
  markClean: () => void;

  // Connection & output
  setConnectionMode: (mode: ConnectionMode) => void;
  setStorageMode: (mode: StorageMode) => void;
  setTableStorageMode: (table: string, mode: StorageMode) => void;
  setSelectedSolution: (solution: string | null) => void;
  setOutputFolder: (folder: string | null) => void;
  setFabricLinkEndpoint: (endpoint: string | null) => void;
  setFabricLinkDatabase: (database: string | null) => void;

  // Table selection
  setSelectedTables: (tables: string[]) => void;
  addTable: (table: string) => void;
  removeTable: (table: string) => void;
  toggleTable: (table: string) => void;

  // Star schema
  setFactTable: (table: string | null) => void;
  setTableRole: (table: string, role: TableRole) => void;
  setRelationships: (relationships: RelationshipConfig[]) => void;
  addRelationship: (relationship: RelationshipConfig) => void;
  removeRelationship: (sourceTable: string, sourceAttribute: string, targetTable: string) => void;
  toggleRelationshipActive: (sourceTable: string, sourceAttribute: string, targetTable: string) => void;

  // Forms & Views
  setTableForm: (table: string, formId: string, formName: string) => void;
  setTableView: (table: string, viewId: string, viewName: string) => void;
  clearTableForm: (table: string) => void;
  clearTableView: (table: string) => void;

  // Attributes
  setTableAttributes: (table: string, attributes: string[]) => void;
  toggleAttribute: (table: string, attribute: string) => void;
  setAttributeDisplayNameOverride: (table: string, attribute: string, displayName: string) => void;
  clearAttributeDisplayNameOverride: (table: string, attribute: string) => void;

  // Date table
  setDateTableConfig: (config: DateTableConfig | null) => void;

  // Misc
  setUseDisplayNameAliases: (value: boolean) => void;
  setShowAllAttributes: (value: boolean) => void;
  setAutoloadCache: (value: boolean) => void;

  // Reset
  reset: () => void;
}

const initialState: ConfigState = {
  configName: 'New Configuration',
  isDirty: false,
  projectName: '',
  connectionMode: DEFAULT_CONNECTION_MODE,
  storageMode: DEFAULT_STORAGE_MODE,
  tableStorageModes: {},
  selectedSolution: null,
  outputFolder: null,
  selectedTables: [],
  factTable: null,
  tableRoles: {},
  relationships: [],
  tableForms: {},
  tableFormNames: {},
  tableViews: {},
  tableViewNames: {},
  tableAttributes: {},
  tableDisplayInfo: {},
  attributeDisplayInfo: {},
  attributeDisplayNameOverrides: {},
  dateTableConfig: null,
  fabricLinkEndpoint: null,
  fabricLinkDatabase: null,
  useDisplayNameAliasesInSql: false,
  showAllAttributes: true,
  autoloadCache: true,
};

export const useConfigStore = create<ConfigState & ConfigActions>()(
  temporal(
    immer((set, get) => ({
      ...initialState,

      // --- Configuration lifecycle ---
      loadFromSettings: (name, settings) =>
        set((state) => {
          Object.assign(state, initialState);
          state.configName = name;
          state.isDirty = false;
          state.projectName = settings.projectName ?? '';
          state.selectedSolution = settings.lastSolution ?? null;
          state.outputFolder = settings.outputFolder ?? null;
          state.selectedTables = [...settings.selectedTables];
          state.factTable = settings.factTable ?? null;
          state.tableRoles = { ...settings.tableRoles };
          state.relationships = settings.relationships.map((r) => ({ ...r }));
          state.tableForms = { ...settings.tableForms };
          state.tableFormNames = { ...settings.tableFormNames };
          state.tableViews = { ...settings.tableViews };
          state.tableViewNames = { ...settings.tableViewNames };
          state.tableAttributes = Object.fromEntries(
            Object.entries(settings.tableAttributes).map(([k, v]) => [k, [...v]])
          );
          state.tableDisplayInfo = { ...settings.tableDisplayInfo };
          state.attributeDisplayInfo = { ...settings.attributeDisplayInfo };
          state.attributeDisplayNameOverrides = { ...settings.attributeDisplayNameOverrides };
          state.dateTableConfig = settings.dateTableConfig ? { ...settings.dateTableConfig } : null;
          state.useDisplayNameAliasesInSql = settings.useDisplayNameAliasesInSql;
          state.showAllAttributes = settings.showAllAttributes;
          state.autoloadCache = settings.autoloadCache;
        }),

      toSettings: (): AppSettings => {
        const s = get();
        return {
          lastEnvironmentUrl: undefined,
          lastSolution: s.selectedSolution ?? undefined,
          selectedTables: [...s.selectedTables],
          tableForms: { ...s.tableForms },
          tableFormNames: { ...s.tableFormNames },
          tableViews: { ...s.tableViews },
          tableViewNames: { ...s.tableViewNames },
          tableAttributes: Object.fromEntries(
            Object.entries(s.tableAttributes).map(([k, v]) => [k, [...v]])
          ),
          tableDisplayInfo: { ...s.tableDisplayInfo },
          attributeDisplayInfo: { ...s.attributeDisplayInfo },
          outputFolder: s.outputFolder ?? undefined,
          projectName: s.projectName || undefined,
          autoloadCache: s.autoloadCache,
          showAllAttributes: s.showAllAttributes,
          factTable: s.factTable ?? undefined,
          tableRoles: { ...s.tableRoles },
          relationships: s.relationships.map((r) => ({ ...r })),
          dateTableConfig: s.dateTableConfig ? { ...s.dateTableConfig } : undefined,
          useDisplayNameAliasesInSql: s.useDisplayNameAliasesInSql,
          attributeDisplayNameOverrides: { ...s.attributeDisplayNameOverrides },
        };
      },

      setConfigName: (name) => set((state) => { state.configName = name; state.isDirty = true; }),
      setProjectName: (name) => set((state) => { state.projectName = name; state.isDirty = true; }),
      markClean: () => set((state) => { state.isDirty = false; }),

      // --- Connection & output ---
      setConnectionMode: (mode) => set((state) => { state.connectionMode = mode; state.isDirty = true; }),
      setStorageMode: (mode) => set((state) => { state.storageMode = mode; state.isDirty = true; }),
      setTableStorageMode: (table, mode) => set((state) => { state.tableStorageModes[table] = mode; state.isDirty = true; }),
      setSelectedSolution: (solution) => set((state) => { state.selectedSolution = solution; state.isDirty = true; }),
      setOutputFolder: (folder) => set((state) => { state.outputFolder = folder; state.isDirty = true; }),
      setFabricLinkEndpoint: (endpoint) => set((state) => { state.fabricLinkEndpoint = endpoint; state.isDirty = true; }),
      setFabricLinkDatabase: (database) => set((state) => { state.fabricLinkDatabase = database; state.isDirty = true; }),

      // --- Table selection ---
      setSelectedTables: (tables) =>
        set((state) => { state.selectedTables = tables; state.isDirty = true; }),
      addTable: (table) =>
        set((state) => {
          if (!state.selectedTables.includes(table)) {
            state.selectedTables.push(table);
            state.isDirty = true;
          }
        }),
      removeTable: (table) =>
        set((state) => {
          state.selectedTables = state.selectedTables.filter((t) => t !== table);
          if (state.factTable === table) state.factTable = null;
          delete state.tableRoles[table];
          state.relationships = state.relationships.filter(
            (r) => r.sourceTable !== table && r.targetTable !== table
          );
          state.isDirty = true;
        }),
      toggleTable: (table) =>
        set((state) => {
          if (state.selectedTables.includes(table)) {
            state.selectedTables = state.selectedTables.filter((t) => t !== table);
            if (state.factTable === table) state.factTable = null;
            delete state.tableRoles[table];
          } else {
            state.selectedTables.push(table);
          }
          state.isDirty = true;
        }),

      // --- Star schema ---
      setFactTable: (table) =>
        set((state) => {
          if (state.factTable) {
            delete state.tableRoles[state.factTable];
          }
          state.factTable = table;
          if (table) {
            state.tableRoles[table] = TableRole.Fact;
          }
          state.isDirty = true;
        }),
      setTableRole: (table, role) =>
        set((state) => { state.tableRoles[table] = role; state.isDirty = true; }),
      setRelationships: (relationships) =>
        set((state) => { state.relationships = relationships; state.isDirty = true; }),
      addRelationship: (relationship) =>
        set((state) => { state.relationships.push(relationship); state.isDirty = true; }),
      removeRelationship: (sourceTable, sourceAttribute, targetTable) =>
        set((state) => {
          state.relationships = state.relationships.filter(
            (r) => !(r.sourceTable === sourceTable && r.sourceAttribute === sourceAttribute && r.targetTable === targetTable)
          );
          state.isDirty = true;
        }),
      toggleRelationshipActive: (sourceTable, sourceAttribute, targetTable) =>
        set((state) => {
          const rel = state.relationships.find(
            (r) => r.sourceTable === sourceTable && r.sourceAttribute === sourceAttribute && r.targetTable === targetTable
          );
          if (rel) rel.isActive = !rel.isActive;
          state.isDirty = true;
        }),

      // --- Forms & Views ---
      setTableForm: (table, formId, formName) =>
        set((state) => { state.tableForms[table] = formId; state.tableFormNames[table] = formName; state.isDirty = true; }),
      setTableView: (table, viewId, viewName) =>
        set((state) => { state.tableViews[table] = viewId; state.tableViewNames[table] = viewName; state.isDirty = true; }),
      clearTableForm: (table) =>
        set((state) => { delete state.tableForms[table]; delete state.tableFormNames[table]; state.isDirty = true; }),
      clearTableView: (table) =>
        set((state) => { delete state.tableViews[table]; delete state.tableViewNames[table]; state.isDirty = true; }),

      // --- Attributes ---
      setTableAttributes: (table, attributes) =>
        set((state) => { state.tableAttributes[table] = attributes; state.isDirty = true; }),
      toggleAttribute: (table, attribute) =>
        set((state) => {
          if (!state.tableAttributes[table]) state.tableAttributes[table] = [];
          const attrs = state.tableAttributes[table];
          const idx = attrs.indexOf(attribute);
          if (idx >= 0) {
            attrs.splice(idx, 1);
          } else {
            attrs.push(attribute);
          }
          state.isDirty = true;
        }),
      setAttributeDisplayNameOverride: (table, attribute, displayName) =>
        set((state) => {
          if (!state.attributeDisplayNameOverrides[table]) state.attributeDisplayNameOverrides[table] = {};
          state.attributeDisplayNameOverrides[table][attribute] = displayName;
          state.isDirty = true;
        }),
      clearAttributeDisplayNameOverride: (table, attribute) =>
        set((state) => {
          if (state.attributeDisplayNameOverrides[table]) {
            delete state.attributeDisplayNameOverrides[table][attribute];
          }
          state.isDirty = true;
        }),

      // --- Date table ---
      setDateTableConfig: (config) =>
        set((state) => { state.dateTableConfig = config; state.isDirty = true; }),

      // --- Misc ---
      setUseDisplayNameAliases: (value) =>
        set((state) => { state.useDisplayNameAliasesInSql = value; state.isDirty = true; }),
      setShowAllAttributes: (value) =>
        set((state) => { state.showAllAttributes = value; state.isDirty = true; }),
      setAutoloadCache: (value) =>
        set((state) => { state.autoloadCache = value; state.isDirty = true; }),

      // --- Reset ---
      reset: () => set(() => ({ ...initialState })),
    })),
    { limit: 50 }
  )
);
