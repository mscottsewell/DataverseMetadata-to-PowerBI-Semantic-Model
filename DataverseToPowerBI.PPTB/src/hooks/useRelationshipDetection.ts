/**
 * useRelationshipDetection.ts - Detects relationships from Dataverse metadata
 *
 * Scans attribute metadata for Lookup-type attributes and builds
 * a relationship graph used by the star-schema wizard.
 */

import { useCallback, useMemo } from 'react';
import { useMetadataStore } from '../stores/useMetadataStore';
import { useConfigStore } from '../stores/useConfigStore';
import type { RelationshipConfig, AttributeMetadata } from '../types/DataModels';

export interface DetectedRelationship {
  sourceTable: string;
  sourceAttribute: string;
  sourceDisplayName: string;
  targetTable: string;
  targetDisplayName: string;
  isPolymorphic: boolean;
}

/** Detect lookup relationships from attribute metadata for a given source table */
function detectRelationshipsForTable(
  sourceTable: string,
  attributes: AttributeMetadata[],
  tableDisplayNames: Record<string, string>
): DetectedRelationship[] {
  const results: DetectedRelationship[] = [];

  for (const attr of attributes) {
    if (attr.attributeType !== 'Lookup' && attr.attributeType !== 'Customer' && attr.attributeType !== 'Owner') {
      continue;
    }
    if (!attr.targets || attr.targets.length === 0) continue;

    for (const target of attr.targets) {
      results.push({
        sourceTable,
        sourceAttribute: attr.logicalName,
        sourceDisplayName: attr.displayName ?? attr.logicalName,
        targetTable: target,
        targetDisplayName: tableDisplayNames[target] ?? target,
        isPolymorphic: attr.targets.length > 1,
      });
    }
  }

  return results;
}

/** Hook that provides detected relationships for the current fact table */
export function useRelationshipDetection() {
  const selectedTables = useConfigStore((s) => s.selectedTables);
  const factTable = useConfigStore((s) => s.factTable);
  const relationships = useConfigStore((s) => s.relationships);
  const setRelationships = useConfigStore((s) => s.setRelationships);
  const tableAttributes = useMetadataStore((s) => s.tableAttributes);
  const tables = useMetadataStore((s) => s.tables);

  const tableDisplayNames = useMemo(() => {
    const map: Record<string, string> = {};
    for (const t of tables) {
      map[t.logicalName] = t.displayName ?? t.logicalName;
    }
    return map;
  }, [tables]);

  /** All detected relationships from the fact table */
  const detectedRelationships = useMemo((): DetectedRelationship[] => {
    if (!factTable) return [];
    const attrs = tableAttributes[factTable];
    if (!attrs) return [];
    return detectRelationshipsForTable(factTable, attrs, tableDisplayNames);
  }, [factTable, tableAttributes, tableDisplayNames]);

  /** Detected relationships grouped by target table */
  const groupedByTarget = useMemo(() => {
    const groups: Record<string, DetectedRelationship[]> = {};
    for (const rel of detectedRelationships) {
      if (!groups[rel.targetTable]) groups[rel.targetTable] = [];
      groups[rel.targetTable].push(rel);
    }
    return groups;
  }, [detectedRelationships]);

  /** Auto-detect and populate relationships from metadata */
  const autoDetect = useCallback(() => {
    if (!factTable) return;
    const newRels: RelationshipConfig[] = detectedRelationships
      .filter((d) => selectedTables.includes(d.targetTable) || selectedTables.includes(d.sourceTable))
      .map((d) => ({
        sourceTable: d.sourceTable,
        sourceAttribute: d.sourceAttribute,
        targetTable: d.targetTable,
        isActive: true,
        isSnowflake: false,
        isReverse: false,
        assumeReferentialIntegrity: true,
      }));

    // Merge with existing: preserve user-configured active/inactive states
    const merged = newRels.map((newRel) => {
      const existing = relationships.find(
        (r) =>
          r.sourceTable === newRel.sourceTable &&
          r.sourceAttribute === newRel.sourceAttribute &&
          r.targetTable === newRel.targetTable
      );
      return existing ?? newRel;
    });

    // Preserve manually-added relationships not in the detected set
    const manualRels = relationships.filter(
      (r) => !newRels.some(
        (n) => n.sourceTable === r.sourceTable &&
               n.sourceAttribute === r.sourceAttribute &&
               n.targetTable === r.targetTable
      )
    );

    setRelationships([...merged, ...manualRels]);
  }, [factTable, detectedRelationships, selectedTables, relationships, setRelationships]);

  return {
    detectedRelationships,
    groupedByTarget,
    autoDetect,
  };
}
