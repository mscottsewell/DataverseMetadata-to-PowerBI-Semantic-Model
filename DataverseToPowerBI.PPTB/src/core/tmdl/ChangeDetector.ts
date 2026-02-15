/**
 * ChangeDetector.ts - Change Detection Logic
 *
 * PURPOSE:
 * TypeScript port of the change detection logic from SemanticModelBuilder.cs.
 * Compares existing TMDL files against expected output to identify what
 * has changed, enabling incremental updates and change preview.
 *
 * SUPPORTED FEATURES:
 * - Column parsing and comparison from TMDL content
 * - M Query extraction and normalization for comparison
 * - Relationship parsing and comparison
 * - Expected column/relationship generation from export metadata
 */

import { ExportTable, ExportRelationship, AttributeDisplayInfo, DateTableConfig } from '../../types/DataModels';
import { mapDataType, getEffectiveDisplayName, getVirtualColumnName } from './TmdlHelpers';
import { logger } from '../../utils/Logger';

// #region Enums

/** Type of change detected */
export enum ChangeType {
  New = 'New',
  Update = 'Update',
  Preserve = 'Preserve',
  Warning = 'Warning',
  Error = 'Error',
  Info = 'Info',
}

/** Level of impact a change has on the semantic model */
export enum ImpactLevel {
  Safe = 'Safe',
  Additive = 'Additive',
  Moderate = 'Moderate',
  Destructive = 'Destructive',
}

// #endregion

// #region Models

/** A single detected change in the semantic model */
export interface SemanticModelChange {
  changeType: ChangeType;
  objectType: string;
  objectName: string;
  description: string;
  impact: ImpactLevel;
  detailText: string;
  parentKey: string;
}

/** Definition of a column parsed from or expected in TMDL */
export interface ColumnDefinition {
  displayName?: string;
  logicalName?: string;
  dataType?: string;
  sourceColumn?: string;
  formatString?: string;
}

/** Result of analyzing changes to a single table */
export interface TableChangeAnalysis {
  queryChanged: boolean;
  queryChangeDetail?: string;
  newColumns: string[];
  modifiedColumns: Record<string, string>;
  removedColumns: string[];
  readonly hasChanges: boolean;
}

/** Result of analyzing relationship changes */
export interface RelationshipChangeAnalysis {
  newRelationships: string[];
  modifiedRelationships: string[];
  removedRelationships: string[];
  readonly hasChanges: boolean;
}

// #endregion

// #region Constants

const LOG_CATEGORY = 'ChangeDetector';

// Columns skipped during generation (not available in TDS/Fabric endpoints)
const SKIPPED_COLUMNS = new Set([
  'statecode',
  'owningusername',
  'owningteamname',
  'owningbusinessunitname',
]);

// Owning lookups whose name columns are not available
const OWNING_LOOKUPS = new Set([
  'owninguser',
  'owningteam',
  'owningbusinessunit',
]);

// #endregion

// #region Column Parsing

/**
 * Parses existing columns from TMDL content.
 * Matches column blocks with optional `///` comment lines and tab-indented property blocks.
 * Returns a case-insensitive map keyed by display name.
 */
export function parseExistingColumns(tmdlContent: string): Record<string, ColumnDefinition> {
  const columns: Record<string, ColumnDefinition> = {};

  try {
    // Pattern: optional /// comment, then column declaration, then tab-indented properties
    const columnPattern = /(?:\/\/\/\s*([^\r\n]+)\r?\n)?\s*column\s+(?:'([^']+)'|"([^"]+)"|([^\r\n]+))\r?\n((?:\t[^\r\n]+\r?\n)+)/g;
    let match: RegExpExecArray | null;

    while ((match = columnPattern.exec(tmdlContent)) !== null) {
      const logicalName = match[1] ? match[1].trim() : undefined;
      const displayName = match[2] ?? match[3] ?? match[4]?.trim() ?? '';
      const properties = match[5];

      const dataTypeMatch = properties.match(/\bdataType:\s*([^\r\n]+)/);
      const sourceColumnMatch = properties.match(/\bsourceColumn:\s*([^\r\n]+)/);
      const formatStringMatch = properties.match(/\bformatString:\s*([^\r\n]+)/);

      const key = displayName.toLowerCase();
      columns[key] = {
        displayName,
        logicalName,
        dataType: dataTypeMatch ? dataTypeMatch[1].trim() : undefined,
        sourceColumn: sourceColumnMatch ? sourceColumnMatch[1].trim() : undefined,
        formatString: formatStringMatch ? formatStringMatch[1].trim() : undefined,
      };
    }
  } catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning(LOG_CATEGORY, `Could not parse existing columns: ${msg}`);
  }

  return columns;
}

// #endregion

// #region Expected Column Generation

/**
 * Generates expected column definitions for a table based on export metadata.
 * Mirrors the logic in SemanticModelBuilder.GenerateExpectedColumns.
 */
export function generateExpectedColumns(
  table: ExportTable,
  attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
  requiredLookupColumns: Set<string>,
  existingColumns: Record<string, ColumnDefinition> | null,
  isFabricLink: boolean,
  useDisplayNameAliases: boolean,
  dateTableConfig: DateTableConfig | null | undefined,
): Record<string, ColumnDefinition> {
  const columns: Record<string, ColumnDefinition> = {};

  const attrInfo = attributeDisplayInfo[table.logicalName] ?? {};
  const processedColumns = new Set<string>();

  // Primary key (always uses logical name as display name when hidden)
  const primaryKey = table.primaryIdAttribute ?? table.logicalName + 'id';
  const pkAttr = table.attributes?.find(a => a.logicalName === primaryKey);

  if (pkAttr) {
    const mapping = mapDataType(pkAttr.attributeType, isFabricLink);
    const key = pkAttr.logicalName.toLowerCase();
    columns[key] = {
      displayName: pkAttr.logicalName,
      logicalName: pkAttr.logicalName,
      dataType: mapping.dataType,
      sourceColumn: pkAttr.logicalName,
      formatString: mapping.formatString ?? undefined,
    };
    processedColumns.add(primaryKey.toLowerCase());
  }

  // Process each attribute
  if (table.attributes) {
    for (const attr of table.attributes) {
      if (processedColumns.has(attr.logicalName.toLowerCase())) continue;

      // Skip columns not available in TDS/Fabric
      if (SKIPPED_COLUMNS.has(attr.logicalName.toLowerCase())) continue;

      const attrDisplayInfo = attrInfo[attr.logicalName];
      const attrType = (attr.attributeType ?? attrDisplayInfo?.attributeType ?? '').toLowerCase();
      const attrDisplayName = attr.displayName ?? attrDisplayInfo?.displayName ?? attr.schemaName ?? attr.logicalName;
      const effectiveName = getEffectiveDisplayName(attrDisplayInfo, attrDisplayName, useDisplayNameAliases);
      const isLookup = attrType === 'lookup' || attrType === 'owner' || attrType === 'customer';
      const isChoice = attrType === 'picklist' || attrType === 'state' || attrType === 'status';
      const isMultiSelectChoice = attrType === 'multiselectpicklist';
      const isBoolean = attrType === 'boolean';

      if (isLookup) {
        addLookupColumns(columns, processedColumns, attr, effectiveName, isFabricLink, useDisplayNameAliases, table.logicalName);
      } else if (isChoice || isBoolean) {
        addChoiceBooleanColumns(columns, processedColumns, attr, effectiveName, attrDisplayInfo, isFabricLink, useDisplayNameAliases, table.logicalName);
      } else if (isMultiSelectChoice) {
        addMultiSelectColumns(columns, processedColumns, attr, effectiveName, attrDisplayInfo, isFabricLink, useDisplayNameAliases, table.logicalName);
      } else {
        addRegularColumn(columns, processedColumns, attr, effectiveName, table, isFabricLink, useDisplayNameAliases, dateTableConfig);
      }
    }
  }

  // Add missing required lookup columns from existing TMDL
  for (const lookupCol of requiredLookupColumns) {
    if (!processedColumns.has(lookupCol.toLowerCase())) {
      const existingKey = lookupCol.toLowerCase();
      if (existingColumns && existingKey in existingColumns) {
        columns[existingKey] = existingColumns[existingKey];
      } else {
        columns[existingKey] = {
          displayName: lookupCol,
          logicalName: lookupCol,
          dataType: 'int64',
          sourceColumn: lookupCol,
          formatString: '0',
        };
      }
      processedColumns.add(lookupCol.toLowerCase());
    }
  }

  return columns;
}

/** Adds lookup columns (hidden ID + visible name) */
function addLookupColumns(
  columns: Record<string, ColumnDefinition>,
  processedColumns: Set<string>,
  attr: { logicalName: string; attributeType?: string },
  effectiveName: string,
  _isFabricLink: boolean,
  useDisplayNameAliases: boolean,
  _tableLogicalName: string,
): void {
  // Hidden ID column
  const mapping = mapDataType('lookup', _isFabricLink);
  const idKey = attr.logicalName.toLowerCase();
  columns[idKey] = {
    displayName: attr.logicalName,
    logicalName: attr.logicalName,
    dataType: mapping.dataType,
    sourceColumn: attr.logicalName,
    formatString: mapping.formatString ?? undefined,
  };

  // Name column (skip for owning* lookups)
  const nameColumn = attr.logicalName + 'name';
  const isOwningLookup = OWNING_LOOKUPS.has(attr.logicalName.toLowerCase());

  if (!processedColumns.has(nameColumn.toLowerCase()) && !isOwningLookup) {
    const lookupSourceCol = useDisplayNameAliases ? effectiveName : nameColumn;
    const nameKey = effectiveName.toLowerCase();
    columns[nameKey] = {
      displayName: effectiveName,
      logicalName: nameColumn,
      dataType: 'string',
      sourceColumn: lookupSourceCol,
      formatString: undefined,
    };
  }
  processedColumns.add(nameColumn.toLowerCase());
  processedColumns.add(attr.logicalName.toLowerCase());
}

/** Adds choice/state/status/boolean columns */
function addChoiceBooleanColumns(
  columns: Record<string, ColumnDefinition>,
  processedColumns: Set<string>,
  attr: { logicalName: string },
  effectiveName: string,
  attrDisplayInfo: AttributeDisplayInfo | undefined,
  isFabricLink: boolean,
  useDisplayNameAliases: boolean,
  tableLogicalName: string,
): void {
  let nameColumn: string;

  if (isFabricLink) {
    nameColumn = attr.logicalName + 'name';
  } else {
    // TDS: use the virtual attribute name from metadata
    nameColumn = attrDisplayInfo?.virtualAttributeName ?? (attr.logicalName + 'name');
    nameColumn = getVirtualColumnName(tableLogicalName, nameColumn);
  }

  if (!processedColumns.has(nameColumn.toLowerCase())) {
    const sourceCol = useDisplayNameAliases ? effectiveName : nameColumn;
    const key = effectiveName.toLowerCase();
    columns[key] = {
      displayName: effectiveName,
      logicalName: nameColumn,
      dataType: 'string',
      sourceColumn: sourceCol,
      formatString: undefined,
    };
  }
  processedColumns.add(nameColumn.toLowerCase());
  processedColumns.add(attr.logicalName.toLowerCase());
}

/** Adds multi-select picklist columns */
function addMultiSelectColumns(
  columns: Record<string, ColumnDefinition>,
  processedColumns: Set<string>,
  attr: { logicalName: string },
  effectiveName: string,
  attrDisplayInfo: AttributeDisplayInfo | undefined,
  isFabricLink: boolean,
  useDisplayNameAliases: boolean,
  tableLogicalName: string,
): void {
  let nameColumn: string;

  if (isFabricLink) {
    nameColumn = attr.logicalName + 'name';
  } else {
    nameColumn = attrDisplayInfo?.virtualAttributeName ?? (attr.logicalName + 'name');
    nameColumn = getVirtualColumnName(tableLogicalName, nameColumn);
  }

  if (!processedColumns.has(nameColumn.toLowerCase())) {
    const sourceCol = useDisplayNameAliases ? effectiveName : nameColumn;
    const key = effectiveName.toLowerCase();
    columns[key] = {
      displayName: effectiveName,
      logicalName: nameColumn,
      dataType: 'string',
      sourceColumn: sourceCol,
      formatString: undefined,
    };
  }
  processedColumns.add(nameColumn.toLowerCase());
  processedColumns.add(attr.logicalName.toLowerCase());
}

/** Adds a regular (non-lookup, non-choice) column */
function addRegularColumn(
  columns: Record<string, ColumnDefinition>,
  processedColumns: Set<string>,
  attr: { logicalName: string; attributeType?: string },
  effectiveName: string,
  table: ExportTable,
  isFabricLink: boolean,
  useDisplayNameAliases: boolean,
  dateTableConfig: DateTableConfig | null | undefined,
): void {
  const attrType = (attr.attributeType ?? '').toLowerCase();
  const isDateTime = attrType === 'datetime';

  // Check if this datetime field should be wrapped (date-only conversion)
  const shouldWrapDateTime = isDateTime && dateTableConfig != null &&
    dateTableConfig.wrappedFields.some(f =>
      f.tableName.toLowerCase() === table.logicalName.toLowerCase() &&
      f.fieldName.toLowerCase() === attr.logicalName.toLowerCase());

  const effectiveAttrType = shouldWrapDateTime ? 'dateonly' : attr.attributeType;
  const mapping = mapDataType(effectiveAttrType, isFabricLink);

  const isPrimaryKey = attr.logicalName.toLowerCase() === (table.primaryIdAttribute ?? '').toLowerCase();
  const regularDisplayName = isPrimaryKey ? attr.logicalName : effectiveName;
  const regularSourceCol = isPrimaryKey ? attr.logicalName : (useDisplayNameAliases ? effectiveName : attr.logicalName);

  const key = regularDisplayName.toLowerCase();
  columns[key] = {
    displayName: regularDisplayName,
    logicalName: attr.logicalName,
    dataType: mapping.dataType,
    sourceColumn: regularSourceCol,
    formatString: mapping.formatString ?? undefined,
  };
  processedColumns.add(attr.logicalName.toLowerCase());
}

// #endregion

// #region Column Comparison

/**
 * Compares two column definitions and returns a list of human-readable differences.
 * Returns an empty array if the columns are equivalent.
 */
export function compareColumnDefinitions(existing: ColumnDefinition, expected: ColumnDefinition): string[] {
  const diffs: string[] = [];

  if (existing.dataType !== expected.dataType) {
    diffs.push(`dataType: ${existing.dataType ?? '(none)'} → ${expected.dataType ?? '(none)'}`);
  }

  if (existing.displayName !== expected.displayName) {
    diffs.push(`displayName: ${existing.displayName ?? '(none)'} → ${expected.displayName ?? '(none)'}`);
  }

  const existingFmt = existing.formatString ?? '';
  const expectedFmt = expected.formatString ?? '';
  if (existingFmt !== expectedFmt && !(existingFmt === '' && expectedFmt === '')) {
    diffs.push('formatString changed');
  }

  return diffs;
}

// #endregion

// #region Query Extraction and Comparison

/**
 * Extracts the SQL query from TMDL partition content.
 * Supports both TDS (Value.NativeQuery) and FabricLink ([Query="..."]) patterns.
 * Returns the normalized query string, or empty string if not found.
 */
export function extractMQuery(tmdlContent: string): string {
  try {
    // TDS pattern: Value.NativeQuery(source,"...SQL...")
    const tdsMatch = tmdlContent.match(/Value\.NativeQuery\([^,]+,\s*"([\s\S]*?)"/);
    if (tdsMatch) {
      return normalizeQuery(tdsMatch[1].trim());
    }

    // FabricLink pattern: [Query="...SQL..."]
    const fabricMatch = tmdlContent.match(/\[Query\s*=\s*"([\s\S]*?)"/);
    if (fabricMatch) {
      return normalizeQuery(fabricMatch[1].trim());
    }
  } catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning(LOG_CATEGORY, `Could not extract M query: ${msg}`);
  }

  return '';
}

/**
 * Normalizes a SQL query for comparison.
 * Removes SQL comments (-- style), collapses all whitespace, and uppercases.
 */
export function normalizeQuery(query: string): string {
  if (!query) return '';

  // Remove SQL line comments
  const withoutComments = query.replace(/--[^\r\n]*/g, '');

  // Collapse all whitespace and uppercase for comparison
  return withoutComments.trim().toUpperCase().replace(/\s+/g, '');
}

/**
 * Compares two query strings for equivalence (case-insensitive).
 * Both queries should already be normalized.
 */
export function compareQueries(existing: string, expected: string): boolean {
  return existing.toLowerCase() === expected.toLowerCase();
}

// #endregion

// #region Relationship Parsing

/**
 * Parses existing relationships from TMDL content.
 * Extracts fromColumn and toColumn references and builds relationship keys
 * in the format "fromTable.fromCol→toTable.toCol".
 */
export function parseExistingRelationships(content: string): Set<string> {
  const rels = new Set<string>();

  try {
    // Match relationship blocks
    const relPattern = /relationship\s+[a-f0-9-]+[\s\S]*?(?=\r?\nrelationship\s|$)/g;
    let match: RegExpExecArray | null;

    while ((match = relPattern.exec(content)) !== null) {
      const relBlock = match[0];

      // Extract fromColumn: Table.Column (handles quoted names with spaces)
      const fromMatch = relBlock.match(/fromColumn:\s*(?:'([^']+)'|([^\s.]+))\.(?:'([^']+)'|([^\s\r\n.]+))/);
      const toMatch = relBlock.match(/toColumn:\s*(?:'([^']+)'|([^\s.]+))\.(?:'([^']+)'|([^\s\r\n.]+))/);

      if (fromMatch && toMatch) {
        const fromTable = fromMatch[1] ?? fromMatch[2];
        const fromCol = fromMatch[3] ?? fromMatch[4];
        const toTable = toMatch[1] ?? toMatch[2];
        const toCol = toMatch[3] ?? toMatch[4];

        const relString = `${fromTable}.${fromCol}→${toTable}.${toCol}`;
        rels.add(relString);
        logger.debug(LOG_CATEGORY, `Parsed relationship: ${relString}`);
      }
    }
  } catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logger.warning(LOG_CATEGORY, `Could not parse existing relationships: ${msg}`);
  }

  return rels;
}

// #endregion

// #region Expected Relationship Generation

/**
 * Generates expected relationship keys from export metadata.
 * Uses display names for tables and logical names for columns.
 * Includes date table relationship if configured.
 */
export function generateExpectedRelationships(
  relationships: ExportRelationship[],
  tables: ExportTable[],
  attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
  dateTableConfig: DateTableConfig | null | undefined,
): Set<string> {
  const rels = new Set<string>();

  // Build lookup maps (case-insensitive)
  const tableDisplayNames: Record<string, string> = {};
  const tablePrimaryKeys: Record<string, string> = {};

  for (const t of tables) {
    const lowerName = t.logicalName.toLowerCase();
    tableDisplayNames[lowerName] = t.displayName ?? t.schemaName ?? t.logicalName;
    tablePrimaryKeys[lowerName] = t.primaryIdAttribute ?? t.logicalName + 'id';
  }

  // Regular relationships
  for (const rel of relationships) {
    const sourceKey = rel.sourceTable.toLowerCase();
    const targetKey = rel.targetTable.toLowerCase();
    if (!(sourceKey in tableDisplayNames) || !(targetKey in tableDisplayNames)) continue;

    const sourceTableDisplay = tableDisplayNames[sourceKey];
    const targetTableDisplay = tableDisplayNames[targetKey];
    const sourceColLogical = rel.sourceAttribute;
    const targetColLogical = tablePrimaryKeys[targetKey];

    const relString = `${sourceTableDisplay}.${sourceColLogical}→${targetTableDisplay}.${targetColLogical}`;
    rels.add(relString);
    logger.debug(LOG_CATEGORY, `Generated: ${relString}`);
  }

  // Date table relationship
  if (dateTableConfig?.primaryDateTable && dateTableConfig.primaryDateField) {
    const dateTableKey = dateTableConfig.primaryDateTable.toLowerCase();
    if (dateTableKey in tableDisplayNames) {
      const tableDisplayName = tableDisplayNames[dateTableKey];
      const sourceTable = tables.find(t => t.logicalName.toLowerCase() === dateTableKey);
      const dateAttr = sourceTable?.attributes.find(
        a => a.logicalName.toLowerCase() === dateTableConfig.primaryDateField.toLowerCase(),
      );

      if (dateAttr) {
        let primaryDateFieldName = dateAttr.displayName ?? dateAttr.schemaName ?? dateAttr.logicalName;
        const tableAttrs = attributeDisplayInfo[dateTableConfig.primaryDateTable];
        const fieldDisplayInfo = tableAttrs?.[dateTableConfig.primaryDateField];
        if (fieldDisplayInfo) {
          primaryDateFieldName = fieldDisplayInfo.displayName ?? primaryDateFieldName;
        }

        const dateRelString = `${tableDisplayName}.${primaryDateFieldName}→Date.Date`;
        rels.add(dateRelString);
        logger.debug(LOG_CATEGORY, `Generated date relationship: ${dateRelString}`);
      } else {
        logger.debug(LOG_CATEGORY,
          `Date relationship skipped: '${dateTableConfig.primaryDateTable}.${dateTableConfig.primaryDateField}' not found`);
      }
    }
  }

  return rels;
}

// #endregion

// #region Factory Functions for Analysis Results

/**
 * Creates a new TableChangeAnalysis with the hasChanges computed property.
 */
export function createTableChangeAnalysis(
  overrides?: Partial<Omit<TableChangeAnalysis, 'hasChanges'>>,
): TableChangeAnalysis {
  const analysis = {
    queryChanged: overrides?.queryChanged ?? false,
    queryChangeDetail: overrides?.queryChangeDetail,
    newColumns: overrides?.newColumns ?? [],
    modifiedColumns: overrides?.modifiedColumns ?? {},
    removedColumns: overrides?.removedColumns ?? [],
    get hasChanges(): boolean {
      return this.queryChanged ||
        this.newColumns.length > 0 ||
        Object.keys(this.modifiedColumns).length > 0 ||
        this.removedColumns.length > 0;
    },
  };
  return analysis;
}

/**
 * Creates a new RelationshipChangeAnalysis with the hasChanges computed property.
 */
export function createRelationshipChangeAnalysis(
  overrides?: Partial<Omit<RelationshipChangeAnalysis, 'hasChanges'>>,
): RelationshipChangeAnalysis {
  const analysis = {
    newRelationships: overrides?.newRelationships ?? [],
    modifiedRelationships: overrides?.modifiedRelationships ?? [],
    removedRelationships: overrides?.removedRelationships ?? [],
    get hasChanges(): boolean {
      return this.newRelationships.length > 0 ||
        this.modifiedRelationships.length > 0 ||
        this.removedRelationships.length > 0;
    },
  };
  return analysis;
}

// #endregion
