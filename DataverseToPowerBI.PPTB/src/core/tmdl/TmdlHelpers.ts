/**
 * TmdlHelpers.ts - TMDL Generation Utilities
 *
 * PURPOSE:
 * Shared helper functions for TMDL (Tabular Model Definition Language) generation.
 * Includes data type mapping, file writing, name sanitization, and column helpers.
 *
 * TMDL FORMAT REQUIREMENTS:
 * - UTF-8 encoding without BOM
 * - CRLF line endings (\r\n)
 * - Tab indentation
 */

import { TMDL_LINE_ENDING } from '../../types/Constants';
import { AttributeDisplayInfo } from '../../types/DataModels';


// #region Data Type Mapping

/** Result of mapping a Dataverse attribute type to Power BI types */
export interface DataTypeMapping {
  /** Power BI data type (e.g., "int64", "double", "string", "dateTime", "boolean") */
  dataType: string;
  /** Power BI format string (e.g., "0", "#,0.00", "Short Date") */
  formatString: string | null;
  /** SQL source provider type (e.g., "int", "nvarchar", "datetime2") */
  sourceProviderType: string | null;
  /** Default summarization (e.g., "sum", "none") */
  summarizeBy: string;
}

/**
 * Maps Dataverse attribute types to Power BI data types.
 * FabricLink note: Fabric SQL endpoint returns money/decimal as float,
 * so PBI Desktop will change them to double. We generate double directly
 * to avoid false change detection on subsequent rebuilds.
 */
export function mapDataType(attributeType: string | null | undefined, isFabricLink: boolean): DataTypeMapping {
  if (!attributeType) {
    return { dataType: 'string', formatString: null, sourceProviderType: 'nvarchar', summarizeBy: 'none' };
  }

  const normalizedType = attributeType.toLowerCase();

  if (isFabricLink) {
    switch (normalizedType) {
      // Numeric types - Fabric SQL endpoint returns money/decimal as float (double)
      case 'integer': return { dataType: 'int64', formatString: '0', sourceProviderType: 'int', summarizeBy: 'sum' };
      case 'bigint': return { dataType: 'int64', formatString: '0', sourceProviderType: 'bigint', summarizeBy: 'sum' };
      case 'decimal': return { dataType: 'double', formatString: '#,0.00', sourceProviderType: null, summarizeBy: 'sum' };
      case 'double': return { dataType: 'double', formatString: '#,0.00', sourceProviderType: null, summarizeBy: 'sum' };
      case 'money': return { dataType: 'double', formatString: '\\$#,0.00;(\\$#,0.00);\\$#,0.00', sourceProviderType: null, summarizeBy: 'sum' };

      // Date/Time types
      case 'datetime': return { dataType: 'dateTime', formatString: 'Short Date', sourceProviderType: 'datetime2', summarizeBy: 'none' };
      case 'dateonly': return { dataType: 'dateTime', formatString: 'Short Date', sourceProviderType: 'datetime2', summarizeBy: 'none' };

      // Boolean types
      case 'boolean': return { dataType: 'boolean', formatString: null, sourceProviderType: 'bit', summarizeBy: 'none' };

      // GUID types
      case 'lookup':
      case 'owner':
      case 'customer':
      case 'uniqueidentifier':
        return { dataType: 'string', formatString: null, sourceProviderType: 'uniqueidentifier', summarizeBy: 'none' };

      // Text types
      case 'string':
      case 'memo':
      case 'picklist':
      case 'state':
      case 'status':
      case 'multiselectpicklist':
        return { dataType: 'string', formatString: null, sourceProviderType: 'nvarchar', summarizeBy: 'none' };

      default:
        return { dataType: 'string', formatString: null, sourceProviderType: 'nvarchar', summarizeBy: 'none' };
    }
  }

  // TDS mode
  switch (normalizedType) {
    // Numeric types - PBI Desktop converts money/decimal to double on TDS too
    case 'integer': return { dataType: 'int64', formatString: '0', sourceProviderType: 'int', summarizeBy: 'sum' };
    case 'bigint': return { dataType: 'int64', formatString: '0', sourceProviderType: 'bigint', summarizeBy: 'sum' };
    case 'decimal': return { dataType: 'double', formatString: '#,0.00', sourceProviderType: null, summarizeBy: 'sum' };
    case 'double': return { dataType: 'double', formatString: '#,0.00', sourceProviderType: null, summarizeBy: 'sum' };
    case 'money': return { dataType: 'double', formatString: '\\$#,0.00;(\\$#,0.00);\\$#,0.00', sourceProviderType: null, summarizeBy: 'sum' };

    // Date/Time types
    case 'datetime': return { dataType: 'dateTime', formatString: 'Short Date', sourceProviderType: 'datetime2', summarizeBy: 'none' };
    case 'dateonly': return { dataType: 'dateTime', formatString: 'Short Date', sourceProviderType: 'datetime2', summarizeBy: 'none' };

    // Boolean types
    case 'boolean': return { dataType: 'boolean', formatString: null, sourceProviderType: 'bit', summarizeBy: 'none' };

    // GUID types
    case 'lookup':
    case 'owner':
    case 'customer':
    case 'uniqueidentifier':
      return { dataType: 'string', formatString: null, sourceProviderType: 'uniqueidentifier', summarizeBy: 'none' };

    // Text types
    case 'string':
    case 'memo':
    case 'picklist':
    case 'state':
    case 'status':
    case 'multiselectpicklist':
      return { dataType: 'string', formatString: null, sourceProviderType: 'nvarchar', summarizeBy: 'none' };

    default:
      return { dataType: 'string', formatString: null, sourceProviderType: 'nvarchar', summarizeBy: 'none' };
  }
}

/**
 * Maps attribute type to Power Query type expression
 */
export function mapToPowerQueryType(attributeType: string | null | undefined): string {
  if (!attributeType) return 'type text';

  switch (attributeType.toLowerCase()) {
    case 'integer':
    case 'bigint':
      return 'Int64.Type';
    case 'decimal':
    case 'money':
    case 'double':
      return 'type number';
    case 'datetime':
      return 'type datetime';
    case 'dateonly':
      return 'type date';
    case 'boolean':
      return 'type logical';
    default:
      return 'type text';
  }
}

// #endregion

// #region Virtual Column Corrections

/**
 * Corrections for virtual column names that don't match metadata or don't exist in TDS.
 * Key format: "tablename.columnname" (lowercase for case-insensitive matching).
 */
export const VIRTUAL_COLUMN_CORRECTIONS: Record<string, string> = {
  'contact.donotsendmmname': 'donotsendmarketingmaterialname',
  'account.donotsendmmname': 'donotsendmarketingmaterialname',
};

/**
 * Gets the corrected virtual column name, if a correction exists.
 */
export function getVirtualColumnName(tableLogicalName: string, virtualColumnName: string): string {
  const key = `${tableLogicalName.toLowerCase()}.${virtualColumnName.toLowerCase()}`;
  return VIRTUAL_COLUMN_CORRECTIONS[key] ?? virtualColumnName;
}

// #endregion

// #region File & Name Helpers

/**
 * Ensures CRLF line endings for TMDL content.
 * Power BI Desktop requires Windows-style line endings.
 */
export function normalizeTmdlLineEndings(content: string): string {
  return content.replace(/\r\n/g, '\n').replace(/\n/g, TMDL_LINE_ENDING);
}

/**
 * Sanitizes a string to be used as a file name.
 */
export function sanitizeFileName(name: string): string {
  // Characters invalid in file names across platforms
  return name.replace(/[<>:"/\\|?*\x00-\x1F]/g, '_');
}

/**
 * Extracts the environment name from a Dataverse URL.
 * Example: "https://portfolioshapingdev.crm.dynamics.com" returns "portfolioshapingdev"
 */
export function extractEnvironmentName(dataverseUrl: string): string {
  if (!dataverseUrl) return 'default';

  const url = dataverseUrl.replace(/^https?:\/\//, '');
  const firstDot = url.indexOf('.');
  if (firstDot > 0) return url.substring(0, firstDot);
  return url;
}

// #endregion

// #region TMDL Column Helpers

/** Internal column info for TMDL generation */
export interface ColumnInfo {
  logicalName: string;
  displayName: string;
  sourceColumn: string;
  isHidden: boolean;
  description?: string;
  attributeType?: string;
  isKey: boolean;
  isRowLabel: boolean;
}

/**
 * Gets the effective display name for a column, considering overrides.
 */
export function getEffectiveDisplayName(
  attrDisplayInfo: AttributeDisplayInfo | undefined,
  fallbackDisplayName: string,
  useDisplayNameAliases: boolean,
): string {
  if (useDisplayNameAliases && attrDisplayInfo?.overrideDisplayName) {
    return attrDisplayInfo.overrideDisplayName;
  }
  return fallbackDisplayName;
}

/**
 * Wraps a SQL field expression with an AS [alias] clause when display name aliasing is enabled.
 * For hidden columns (primary keys, lookup FK IDs), no alias is added.
 */
export function applySqlAlias(
  sqlExpression: string,
  displayName: string,
  logicalName: string,
  isHidden: boolean,
  useDisplayNameAliases: boolean,
): string {
  if (!useDisplayNameAliases || isHidden) return sqlExpression;

  // Only add alias if display name differs from logical name
  if (displayName.toLowerCase() === logicalName.toLowerCase()) return sqlExpression;

  return `${sqlExpression} AS [${displayName}]`;
}

/**
 * Builds column description from Dataverse metadata.
 */
export function buildDescription(
  tableLogicalName: string,
  attrLogicalName: string,
  _schemaName: string,
  dataverseDescription: string | null | undefined,
  targets: string[] | null | undefined,
): string {
  const parts: string[] = [];

  if (dataverseDescription?.trim()) {
    parts.push(dataverseDescription);
  }

  parts.push(`Source: ${tableLogicalName}.${attrLogicalName}`);

  if (targets && targets.length > 0) {
    parts.push(`Targets: ${targets.join(', ')}`);
  }

  return parts.join(' | ');
}

// #endregion

// #region Storage Mode Helpers

/**
 * Returns the TMDL partition mode string for a table based on the global storage mode.
 * DirectQuery: all tables use directQuery.
 * Dual: fact table uses directQuery, dimensions use dual.
 * DualSelect: fact uses directQuery, dimensions use per-table override.
 * Import: all tables use import.
 */
export function getPartitionMode(
  storageMode: string,
  tableRole: string,
  tableLogicalName?: string,
  tableStorageModeOverrides?: Record<string, string>,
): string {
  switch (storageMode) {
    case 'Import': return 'import';
    case 'Dual': return tableRole === 'Fact' ? 'directQuery' : 'dual';
    case 'DualSelect':
      return tableRole === 'Fact' ? 'directQuery' : getDualSelectMode(tableLogicalName, tableStorageModeOverrides);
    default: return 'directQuery';
  }
}

/**
 * Gets per-table storage mode override for DualSelect mode.
 */
export function getDualSelectMode(
  tableLogicalName?: string,
  overrides?: Record<string, string>,
): string {
  if (tableLogicalName && overrides) {
    const key = Object.keys(overrides).find(k => k.toLowerCase() === tableLogicalName.toLowerCase());
    if (key) return overrides[key];
  }
  return 'directQuery';
}

/**
 * Returns true if user-context view filters (CURRENT_USER) should be stripped.
 * User context requires DirectQuery; it is not available in import or dual modes.
 */
export function shouldStripUserContext(
  storageMode: string,
  tableRole: string,
  tableLogicalName?: string,
  tableStorageModeOverrides?: Record<string, string>,
): boolean {
  switch (storageMode) {
    case 'Import': return true;
    case 'Dual': return tableRole !== 'Fact';
    case 'DualSelect':
      return tableRole === 'Fact' ? false : getDualSelectMode(tableLogicalName, tableStorageModeOverrides) === 'dual';
    default: return false;
  }
}

/**
 * Normalizes storage mode strings for comparison.
 * "Dual" and "DualSelect" are considered equivalent for detection purposes.
 */
export function normalizeStorageMode(mode: string | null | undefined): string | null {
  if (!mode) return null;
  // DualSelect and Dual are equivalent for mode detection
  if (mode.toLowerCase() === 'dualselect') return 'Dual';
  return mode;
}

// #endregion

// #region GUID Generation

/** Generates a new random GUID string */
export function newGuid(): string {
  return crypto.randomUUID();
}

/**
 * Gets a lineageTag from existing tags dictionary, or generates a new one.
 */
export function getOrNewLineageTag(existingTags: Record<string, string> | null | undefined, key: string): string {
  if (existingTags && key in existingTags) return existingTags[key];
  return newGuid();
}

// #endregion
