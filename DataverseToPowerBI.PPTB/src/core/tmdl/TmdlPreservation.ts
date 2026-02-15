/**
 * TmdlPreservation.ts - TMDL Preservation Logic
 *
 * PURPOSE:
 * Preserves user customizations during incremental TMDL rebuilds.
 * Handles lineageTags, column metadata, relationships, and user measures.
 *
 * PRESERVATION TARGETS:
 * - LineageTags (GUIDs for tables, columns, measures, expressions)
 * - Column metadata (formatString, summarizeBy, annotations, descriptions)
 * - Relationship GUIDs and user-added relationships
 * - User-created measures (excludes auto-generated Link/Count)
 */

import { ExportTable, ExportRelationship, AttributeDisplayInfo, DateTableConfig } from '../../types/DataModels';
import { getEffectiveDisplayName } from './TmdlHelpers';
import { quoteTmdlName } from '../../utils/Validation';
import { logger } from '../../utils/Logger';

// #region Models

/** Metadata parsed from an existing column in a TMDL file */
export interface ExistingColumnInfo {
  sourceColumn: string;
  description?: string;
  formatString?: string;
  summarizeBy?: string;
  dataType?: string;
  annotations: Record<string, string>;
}

// #endregion

// #region LineageTag Preservation

/**
 * Parses an existing TMDL file content and extracts lineageTags, keyed by entity identifier.
 * For tables: key = "table" → lineageTag
 * For columns: key = "col:{sourceColumn}" → lineageTag
 * For measures: key = "measure:{measureName}" → lineageTag
 * For expressions: key = "expr:{expressionName}" → lineageTag
 */
export function parseExistingLineageTags(tmdlContent: string): Record<string, string> {
  const tags: Record<string, string> = {};

  try {
    const lines = tmdlContent.split(/\r?\n/);
    let currentEntity: string | null = null;
    let currentSourceColumn: string | null = null;

    for (let i = 0; i < lines.length; i++) {
      const trimmed = lines[i].trimStart();

      if (trimmed.startsWith('table ')) {
        currentEntity = 'table';
        currentSourceColumn = null;
      } else if (trimmed.startsWith('column ')) {
        currentEntity = 'column';
        currentSourceColumn = null;
      } else if (trimmed.startsWith('measure ')) {
        const nameMatch = trimmed.match(/^measure\s+'([^']+)'|^measure\s+(\S+)/);
        if (nameMatch) {
          const measureName = nameMatch[1] ?? nameMatch[2];
          currentEntity = `measure:${measureName}`;
        }
        currentSourceColumn = null;
      } else if (trimmed.startsWith('expression ')) {
        const nameMatch = trimmed.match(/^expression\s+(\S+)/);
        if (nameMatch) {
          currentEntity = `expr:${nameMatch[1]}`;
        }
        currentSourceColumn = null;
      } else if (trimmed.startsWith('sourceColumn:') && currentEntity === 'column') {
        currentSourceColumn = trimmed.substring('sourceColumn:'.length).trim();
      } else if (trimmed.startsWith('lineageTag:')) {
        const tag = trimmed.substring('lineageTag:'.length).trim();
        if (currentEntity === 'table') {
          tags['table'] = tag;
        } else if (currentEntity === 'column' && currentSourceColumn != null) {
          tags[`col:${currentSourceColumn}`] = tag;
        } else if (currentEntity === 'column') {
          // lineageTag appears before sourceColumn — scan ahead
          for (let j = i + 1; j < lines.length && j < i + 10; j++) {
            const ahead = lines[j].trimStart();
            if (ahead.startsWith('sourceColumn:')) {
              const sc = ahead.substring('sourceColumn:'.length).trim();
              tags[`col:${sc}`] = tag;
              break;
            }
            if (ahead.startsWith('column ') || ahead.startsWith('measure ') || ahead.startsWith('partition ')) {
              break;
            }
          }
        } else if (currentEntity?.startsWith('measure:') || currentEntity?.startsWith('expr:')) {
          tags[currentEntity] = tag;
        }
      } else if (trimmed.startsWith('partition ')) {
        currentEntity = null;
        currentSourceColumn = null;
      }
    }
  } catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning('TmdlPreservation', `Could not parse lineageTags: ${msg}`);
  }

  return tags;
}

// #endregion

// #region Column Metadata Preservation

/**
 * Parses existing TMDL content to extract per-column metadata.
 * Key = sourceColumn value. Used to preserve user customizations.
 */
export function parseExistingColumnMetadata(tmdlContent: string): Record<string, ExistingColumnInfo> {
  const columns: Record<string, ExistingColumnInfo> = {};

  try {
    // Match each column block: starts with \tcolumn and continues with double-tab lines
    const colPattern = /^\tcolumn\s+.+?\r?\n((?:\t\t.+\r?\n|\s*\r?\n)*)/gm;
    let match: RegExpExecArray | null;

    while ((match = colPattern.exec(tmdlContent)) !== null) {
      const block = match[0];
      const sourceMatch = block.match(/sourceColumn:\s*(.+)$/m);
      if (!sourceMatch) continue;

      const sourceColumn = sourceMatch[1].trim();
      const info: ExistingColumnInfo = { sourceColumn, annotations: {} };

      const descMatch = block.match(/description:\s*(.+)$/m);
      if (descMatch) info.description = descMatch[1].trim();

      // Multi-line description (```-delimited)
      const multiDescMatch = block.match(/description:\s*\r?\n\t\t\t(.+?)(?=\r?\n\t\t[a-z])/s);
      if (multiDescMatch) info.description = multiDescMatch[1].trim();

      const fmtMatch = block.match(/formatString:\s*(.+)$/m);
      if (fmtMatch) info.formatString = fmtMatch[1].trim();

      const sumMatch = block.match(/summarizeBy:\s*(.+)$/m);
      if (sumMatch) info.summarizeBy = sumMatch[1].trim();

      const dtMatch = block.match(/dataType:\s*(.+)$/m);
      if (dtMatch) info.dataType = dtMatch[1].trim();

      // Extract annotations (key = value pairs)
      const annotPattern = /annotation\s+(\S+)\s*=\s*(.+)$/gm;
      let annotMatch: RegExpExecArray | null;
      while ((annotMatch = annotPattern.exec(block)) !== null) {
        info.annotations[annotMatch[1].trim()] = annotMatch[2].trim();
      }

      columns[sourceColumn] = info;
    }
  } catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning('TmdlPreservation', `Could not parse column metadata: ${msg}`);
  }

  return columns;
}

// #endregion

// #region Relationship Preservation

/**
 * Parses existing relationships.tmdl content and returns a map of relationship keys to their GUIDs.
 * Key format: "fromTable.fromColumn→toTable.toColumn" (using display names as they appear in TMDL).
 */
export function parseExistingRelationshipGuids(content: string): Record<string, string> {
  const guids: Record<string, string> = {};

  try {
    // Split content into relationship blocks
    const blocks = content.split(/(?=^relationship\s)/m).filter(b => b.trim());

    for (const block of blocks) {
      const guidMatch = block.match(/^relationship\s+(\S+)/);
      if (!guidMatch) continue;

      const guid = guidMatch[1];
      const fromMatch = block.match(/fromColumn:\s*(.+)$/m);
      const toMatch = block.match(/toColumn:\s*(.+)$/m);

      if (fromMatch && toMatch) {
        const key = `${fromMatch[1].trim()}→${toMatch[1].trim()}`;
        guids[key] = guid;
      }
    }
  }catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning('TmdlPreservation', `Could not parse relationship GUIDs: ${msg}`);
  }

  return guids;
}

/**
 * Parses existing relationships.tmdl content and returns full relationship blocks
 * keyed by their fromColumn→toColumn key.
 */
export function parseExistingRelationshipBlocks(content: string): Record<string, string> {
  const blocks: Record<string, string> = {};

  try {
    // Split content into relationship blocks
    const rawBlocks = content.split(/(?=^relationship\s)/m).filter(b => b.trim());

    for (const block of rawBlocks) {
      if (!block.match(/^relationship\s/)) continue;

      const fromMatch = block.match(/fromColumn:\s*(.+)$/m);
      const toMatch = block.match(/toColumn:\s*(.+)$/m);

      if (fromMatch && toMatch) {
        const key = `${fromMatch[1].trim()}→${toMatch[1].trim()}`;
        blocks[key] = block;
      }
    }
  }catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning('TmdlPreservation', `Could not parse relationship blocks: ${msg}`);
  }

  return blocks;
}

/**
 * Identifies user-added relationships by comparing existing relationship blocks against
 * the set of tool-generated relationship keys. Returns the TMDL text for user relationships.
 */
export function extractUserRelationships(
  existingBlocks: Record<string, string>,
  toolGeneratedKeys: Set<string>,
): string | null {
  const parts: string[] = [];

  for (const [key, block] of Object.entries(existingBlocks)) {
    if (!toolGeneratedKeys.has(key)) {
      // Skip stale date table relationships
      if (key.toLowerCase().endsWith('→date.date')) {
        logger.debug('TmdlPreservation', `Removing stale date relationship: ${key}`);
        continue;
      }

      // This relationship was not generated by the tool — preserve it
      let preserved = block;
      if (!preserved.includes('/// User-added relationship')) {
        preserved = `/// User-added relationship (preserved by DataverseToPowerBI)\r\n${preserved}`;
      }
      parts.push(preserved.endsWith('\n') ? preserved : preserved + '\r\n');
      logger.debug('TmdlPreservation', `Preserving user-added relationship: ${key}`);
    }
  }

  return parts.length > 0 ? parts.join('') : null;
}

/**
 * Builds the set of relationship keys that the tool would generate, without actually generating TMDL.
 * Used to identify which existing relationships are user-added (not in this set).
 */
export function buildToolRelationshipKeys(
  tables: ExportTable[],
  relationships: ExportRelationship[],
  attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
  dateTableConfig: DateTableConfig | null | undefined,
): Set<string> {
  const keys = new Set<string>();

  const tableDisplayNames: Record<string, string> = {};
  const tablePrimaryKeys: Record<string, string> = {};

  for (const t of tables) {
    const lowerName = t.logicalName.toLowerCase();
    tableDisplayNames[lowerName] = t.displayName ?? t.schemaName ?? t.logicalName;
    tablePrimaryKeys[lowerName] = t.primaryIdAttribute ?? t.logicalName + 'id';
  }

  for (const rel of relationships) {
    const sourceKey = rel.sourceTable.toLowerCase();
    const targetKey = rel.targetTable.toLowerCase();
    if (!(sourceKey in tableDisplayNames) || !(targetKey in tableDisplayNames)) continue;

    const sourceTableDisplay = tableDisplayNames[sourceKey];
    const targetTableDisplay = tableDisplayNames[targetKey];
    const targetPrimaryKey = tablePrimaryKeys[targetKey];

    const fromRef = `${quoteTmdlName(sourceTableDisplay)}.${quoteTmdlName(rel.sourceAttribute)}`;
    const toRef = `${quoteTmdlName(targetTableDisplay)}.${quoteTmdlName(targetPrimaryKey)}`;
    keys.add(`${fromRef}→${toRef}`);
  }

  // Date table relationship
  if (dateTableConfig?.primaryDateTable && dateTableConfig.primaryDateField) {
    const dateTableKey = dateTableConfig.primaryDateTable.toLowerCase();
    if (dateTableKey in tableDisplayNames) {
      const sourceTableDisplay = tableDisplayNames[dateTableKey];
      const sourceTable = tables.find(t => t.logicalName.toLowerCase() === dateTableKey);
      const dateAttr = sourceTable?.attributes.find(
        a => a.logicalName.toLowerCase() === dateTableConfig.primaryDateField.toLowerCase(),
      );

      if (dateAttr) {
        let primaryDateFieldName = dateAttr.displayName ?? dateAttr.schemaName ?? dateAttr.logicalName;
        const tableAttrs = attributeDisplayInfo[dateTableConfig.primaryDateTable];
        const fieldDisplayInfo = tableAttrs?.[dateTableConfig.primaryDateField];
        if (fieldDisplayInfo) {
          primaryDateFieldName = getEffectiveDisplayName(fieldDisplayInfo, fieldDisplayInfo.displayName ?? primaryDateFieldName, true);
        }

        const fromRef = `${quoteTmdlName(sourceTableDisplay)}.${quoteTmdlName(primaryDateFieldName)}`;
        keys.add(`${fromRef}→Date.Date`);
      }
    }
  }

  return keys;
}

// #endregion

// #region User Measures Preservation

/**
 * Extracts the user measures section from existing TMDL content (excludes auto-generated measures).
 */
export function extractUserMeasuresSection(tmdlContent: string, table?: ExportTable): string | null {
  try {
    // Build set of auto-generated measure names to exclude
    const autoMeasures = new Set<string>();
    if (table) {
      const displayName = table.displayName ?? table.schemaName ?? table.logicalName;
      autoMeasures.add(`Link to ${displayName}`.toLowerCase());
      autoMeasures.add(`${displayName} Count`.toLowerCase());
    }

    // Find all measure blocks
    const measurePattern = /(^\s*(?:\/\/\/[^\r\n]*\r?\n)*\s*measure\s+([^\r\n]+)\r?\n(?:.*?\r?\n)*?(?=^\s*(?:measure|column|partition|annotation)\s|$))/gm;
    let match: RegExpExecArray | null;
    const parts: string[] = [];

    while ((match = measurePattern.exec(tmdlContent)) !== null) {
      // Extract measure name from the "measure 'Name' = ..." line
      const nameMatch = match[2].match(/^'([^']+)'|^([^\s=]+)/);
      if (!nameMatch) continue;
      const measureName = nameMatch[1] ?? nameMatch[2];

      // Skip auto-generated measures (they'll be re-generated)
      if (autoMeasures.has(measureName.toLowerCase())) continue;

      parts.push(match[0]);
    }

    return parts.length > 0 ? parts.join('') : null;
  } catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning('TmdlPreservation', `Could not extract measures: ${msg}`);
    return null;
  }
}

/**
 * Inserts user measures into generated TMDL (after columns, before partition).
 */
export function insertUserMeasures(tableTmdl: string, measuresSection: string): string {
  // Find the partition section and insert measures before it
  const partitionIndex = tableTmdl.indexOf('\tpartition');
  if (partitionIndex > 0) {
    return tableTmdl.substring(0, partitionIndex) + measuresSection + tableTmdl.substring(partitionIndex);
  }

  // If no partition found, append before annotations
  const annotationIndex = tableTmdl.indexOf('\tannotation');
  if (annotationIndex > 0) {
    return tableTmdl.substring(0, annotationIndex) + measuresSection + tableTmdl.substring(annotationIndex);
  }

  // Fallback: append at end
  return tableTmdl + measuresSection;
}

// #endregion
