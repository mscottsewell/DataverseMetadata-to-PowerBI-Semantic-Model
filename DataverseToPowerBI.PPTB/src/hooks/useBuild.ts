/**
 * useBuild.ts - Hook for TMDL generation and file output
 *
 * Connects the BuildOrchestrator to UI state and file system adapter.
 */

import { useCallback } from 'react';
import { BuildOrchestrator, type BuildParams, type BuildResult } from '../core/tmdl/BuildOrchestrator';
import { FileSystemAdapter } from '../adapters/FileSystemAdapter';
import { useConfigStore } from '../stores/useConfigStore';
import { useMetadataStore } from '../stores/useMetadataStore';
import { useConnectionStore } from '../stores/useConnectionStore';
import { useUIStore } from '../stores/useUIStore';
import type { ExportTable } from '../types/DataModels';

/** Hook for generating and saving TMDL output */
export function useBuild() {
  const addToast = useUIStore((s) => s.addToast);
  const setGlobalLoading = useUIStore((s) => s.setGlobalLoading);
  const connection = useConnectionStore((s) => s.connection);

  const generatePreview = useCallback((): BuildResult | null => {
    try {
      const config = useConfigStore.getState();
      const metadata = useMetadataStore.getState();

      const orchestrator = new BuildOrchestrator({
        connectionType: config.connectionMode,
        fabricLinkEndpoint: config.fabricLinkEndpoint ?? undefined,
        fabricLinkDatabase: config.fabricLinkDatabase ?? undefined,
        storageMode: config.storageMode,
        useDisplayNameAliasesInSql: config.useDisplayNameAliasesInSql,
        tableStorageModeOverrides: config.tableStorageModes,
      });

      // Build ExportTable array from config + metadata
      // Fall back to config store attribute selections when metadata is not yet hydrated
      const tables: ExportTable[] = config.selectedTables.map((logicalName) => {
        const tableInfo = metadata.tables.find((t) => t.logicalName === logicalName);
        const metaAttrs = metadata.tableAttributes[logicalName] ?? [];
        const configAttrNames = config.tableAttributes[logicalName];

        // Use metadata attrs when available; otherwise synthesize from config's attributeDisplayInfo
        let attrs = metaAttrs;
        if (attrs.length === 0 && config.attributeDisplayInfo[logicalName]) {
          attrs = Object.entries(config.attributeDisplayInfo[logicalName]).map(([name, info]) => ({
            logicalName: name,
            displayName: info.displayName,
            schemaName: info.schemaName,
            attributeType: info.attributeType,
            isCustomAttribute: false,
            isRequired: false,
            targets: info.targets,
            virtualAttributeName: info.virtualAttributeName,
            isGlobal: info.isGlobal,
            optionSetName: info.optionSetName,
          }));
        }

        const selectedAttrs = configAttrNames ?? attrs.map((a) => a.logicalName);
        const view = metadata.tableViews[logicalName]?.find(
          (v) => v.viewId === config.tableViews[logicalName]
        );
        return {
          logicalName,
          displayName: tableInfo?.displayName ?? logicalName,
          schemaName: tableInfo?.schemaName,
          objectTypeCode: tableInfo?.objectTypeCode ?? 0,
          primaryIdAttribute: tableInfo?.primaryIdAttribute,
          primaryNameAttribute: tableInfo?.primaryNameAttribute,
          role: config.tableRoles[logicalName] ?? 'Dimension',
          hasStateCode: attrs.some((a) => a.logicalName === 'statecode'),
          forms: [],
          view: view ? { viewId: view.viewId, viewName: view.name, fetchXml: view.fetchXml } : undefined,
          attributes: attrs.filter((a) => selectedAttrs.includes(a.logicalName)),
        };
      });

      const params: BuildParams = {
        semanticModelName: config.projectName || 'SemanticModel',
        dataverseUrl: connection?.url ?? '',
        tables,
        relationships: config.relationships.map((r) => ({
          sourceTable: r.sourceTable,
          sourceAttribute: r.sourceAttribute,
          targetTable: r.targetTable,
          displayName: r.displayName,
          isActive: r.isActive,
          isSnowflake: r.isSnowflake,
          assumeReferentialIntegrity: r.assumeReferentialIntegrity,
        })),
        attributeDisplayInfo: config.attributeDisplayInfo,
        dateTableConfig: config.dateTableConfig ?? undefined,
      };

      return orchestrator.build(params);
    } catch (err) {
      addToast({ type: 'error', title: 'Preview failed', message: String(err) });
      return null;
    }
  }, [connection, addToast]);

  const generateAndSave = useCallback(async () => {
    setGlobalLoading(true, 'Generating semantic model...');
    try {
      const result = generatePreview();
      if (!result || result.files.size === 0) {
        addToast({ type: 'error', title: 'No files generated' });
        return;
      }

      const fs = new FileSystemAdapter();
      const config = useConfigStore.getState();
      await fs.writeFilesToFolder(result.files, config.outputFolder ?? undefined);
      addToast({ type: 'success', title: 'Semantic model saved successfully' });
    } catch (err) {
      addToast({ type: 'error', title: 'Save failed', message: String(err) });
    } finally {
      setGlobalLoading(false);
    }
  }, [generatePreview, addToast, setGlobalLoading]);

  return { generatePreview, generateAndSave };
}
