/**
 * useDataverse.ts - Custom hooks for Dataverse data fetching
 *
 * Wraps DataverseAdapter calls with store integration and error handling.
 * These hooks manage loading states, caching in the metadata store, and
 * provide a clean interface for UI components to fetch data.
 */

import { useCallback } from 'react';
import { DataverseAdapter } from '../adapters/DataverseAdapter';
import { useMetadataStore } from '../stores/useMetadataStore';
import { useUIStore } from '../stores/useUIStore';

// Singleton adapter instance
let adapterInstance: DataverseAdapter | null = null;

function getAdapter(): DataverseAdapter {
  if (!adapterInstance) {
    adapterInstance = new DataverseAdapter();
  }
  return adapterInstance;
}

/** Fetch solutions from Dataverse and update the metadata store */
export function useFetchSolutions() {
  const setSolutions = useMetadataStore((s) => s.setSolutions);
  const setLoadingSolutions = useMetadataStore((s) => s.setLoadingSolutions);
  const addToast = useUIStore((s) => s.addToast);

  return useCallback(async () => {
    setLoadingSolutions(true);
    try {
      const adapter = getAdapter();
      const solutions = await adapter.getSolutionsAsync();
      setSolutions(solutions);
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to load solutions', message: String(err) });
      setSolutions([]);
    }
  }, [setSolutions, setLoadingSolutions, addToast]);
}

/** Fetch tables for a solution and update the metadata store */
export function useFetchTables() {
  const setTables = useMetadataStore((s) => s.setTables);
  const setLoadingTables = useMetadataStore((s) => s.setLoadingTables);
  const setSolutionName = useMetadataStore((s) => s.setSolutionName);
  const addToast = useUIStore((s) => s.addToast);

  return useCallback(async (solutionName: string) => {
    setLoadingTables(true);
    setSolutionName(solutionName);
    try {
      const adapter = getAdapter();
      const tables = await adapter.getSolutionTablesAsync(solutionName);
      setTables(tables);
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to load tables', message: String(err) });
      setTables([]);
    }
  }, [setTables, setLoadingTables, setSolutionName, addToast]);
}

/** Fetch attributes for a specific table */
export function useFetchAttributes() {
  const setTableAttributes = useMetadataStore((s) => s.setTableAttributes);
  const setLoadingAttributes = useMetadataStore((s) => s.setLoadingAttributes);
  const addToast = useUIStore((s) => s.addToast);

  return useCallback(async (tableName: string) => {
    setLoadingAttributes(tableName, true);
    try {
      const adapter = getAdapter();
      const attrs = await adapter.getAttributesAsync(tableName);
      setTableAttributes(tableName, attrs);
    } catch (err) {
      addToast({ type: 'error', title: `Failed to load attributes for ${tableName}`, message: String(err) });
      setTableAttributes(tableName, []);
    }
  }, [setTableAttributes, setLoadingAttributes, addToast]);
}

/** Fetch forms for a specific table */
export function useFetchForms() {
  const setTableForms = useMetadataStore((s) => s.setTableForms);
  const setLoadingForms = useMetadataStore((s) => s.setLoadingForms);
  const addToast = useUIStore((s) => s.addToast);

  return useCallback(async (tableName: string) => {
    setLoadingForms(tableName, true);
    try {
      const adapter = getAdapter();
      const forms = await adapter.getFormsAsync(tableName);
      setTableForms(tableName, forms);
    } catch (err) {
      addToast({ type: 'error', title: `Failed to load forms for ${tableName}`, message: String(err) });
      setTableForms(tableName, []);
    }
  }, [setTableForms, setLoadingForms, addToast]);
}

/** Fetch views for a specific table */
export function useFetchViews() {
  const setTableViews = useMetadataStore((s) => s.setTableViews);
  const setLoadingViews = useMetadataStore((s) => s.setLoadingViews);
  const addToast = useUIStore((s) => s.addToast);

  return useCallback(async (tableName: string) => {
    setLoadingViews(tableName, true);
    try {
      const adapter = getAdapter();
      const views = await adapter.getViewsAsync(tableName);
      setTableViews(tableName, views);
    } catch (err) {
      addToast({ type: 'error', title: `Failed to load views for ${tableName}`, message: String(err) });
      setTableViews(tableName, []);
    }
  }, [setTableViews, setLoadingViews, addToast]);
}

/** Load metadata for all selected tables (attributes, forms, views) */
export function useFetchAllTableMetadata() {
  const fetchAttributes = useFetchAttributes();
  const fetchForms = useFetchForms();
  const fetchViews = useFetchViews();
  const metaAttributes = useMetadataStore((s) => s.tableAttributes);

  return useCallback(async (tableNames: string[]) => {
    const promises = tableNames
      .filter((name) => !metaAttributes[name]) // Skip already loaded
      .map(async (name) => {
        await Promise.all([
          fetchAttributes(name),
          fetchForms(name),
          fetchViews(name),
        ]);
      });
    await Promise.all(promises);
  }, [fetchAttributes, fetchForms, fetchViews, metaAttributes]);
}
