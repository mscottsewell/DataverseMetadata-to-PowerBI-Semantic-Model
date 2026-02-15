/**
 * SemanticModelBuilder.ts - TMDL Generation Engine
 *
 * PURPOSE:
 * Generates TMDL (Tabular Model Definition Language) content strings for
 * Power BI Semantic Models from Dataverse metadata. This is a TypeScript
 * port of the C# SemanticModelBuilder.cs.
 *
 * SUPPORTED CONNECTION MODES:
 * - DataverseTDS: Uses CommonDataService.Database connector with native SQL queries
 * - FabricLink: Uses Sql.Database connector against Fabric Lakehouse SQL endpoint
 *
 * NOTE: This class generates TMDL content only. File I/O and orchestration
 * are handled by BuildOrchestrator.ts.
 */

import {
  ExportTable,
  ExportRelationship,
  AttributeDisplayInfo,
  DateTableConfig,
} from '../../types/DataModels';
import {
  mapDataType,
  getEffectiveDisplayName,
  applySqlAlias,
  buildDescription,
  getPartitionMode,
  shouldStripUserContext,
  getOrNewLineageTag,
  getVirtualColumnName,
  ColumnInfo,
  newGuid,
} from './TmdlHelpers';
import { ExistingColumnInfo } from './TmdlPreservation';
import { FetchXmlToSqlConverter } from '../converters/FetchXmlToSqlConverter';
import { quoteTmdlName } from '../../utils/Validation';
import { logger } from '../../utils/Logger';
import { normalizeQuery } from './ChangeDetector';

// #region Types

/** Configuration for the SemanticModelBuilder */
interface SemanticModelBuilderConfig {
  connectionType?: string;
  fabricLinkEndpoint?: string;
  fabricLinkDatabase?: string;
  languageCode?: number;
  useDisplayNameAliasesInSql?: boolean;
  storageMode?: string;
  statusCallback?: (message: string) => void;
}

/** Known tool-generated annotations that are always regenerated */
const TOOL_ANNOTATIONS = new Set<string>([
  'summarizationsetby',
  'underlyingdatetimedatatype',
]);

// #endregion

// #region SemanticModelBuilder Class

/**
 * Builds Power BI Semantic Model TMDL content from Dataverse metadata.
 * Generates TMDL strings for tables, relationships, expressions, and the model file.
 */
export class SemanticModelBuilder {
  private readonly connectionType: string;
  private readonly languageCode: number;
  private readonly useDisplayNameAliasesInSql: boolean;
  private readonly storageMode: string;
  private readonly statusCallback?: (message: string) => void;
  private tableStorageModeOverridesMap: Record<string, string> = {};

  /** Whether this builder is configured for FabricLink (Lakehouse SQL) mode */
  get isFabricLink(): boolean {
    return this.connectionType === 'FabricLink';
  }

  constructor(config: SemanticModelBuilderConfig = {}) {
    this.connectionType = config.connectionType ?? 'DataverseTDS';
    this.languageCode = config.languageCode ?? 1033;
    this.useDisplayNameAliasesInSql = config.useDisplayNameAliasesInSql ?? true;
    this.storageMode = config.storageMode ?? 'DirectQuery';
    this.statusCallback = config.statusCallback;
  }

  /**
   * Sets per-table storage mode overrides for DualSelect mode.
   * Key = table logical name, Value = "directQuery" or "dual".
   */
  setTableStorageModeOverrides(overrides: Record<string, string> | null | undefined): void {
    if (overrides) {
      this.tableStorageModeOverridesMap = {};
      for (const [key, value] of Object.entries(overrides)) {
        this.tableStorageModeOverridesMap[key.toLowerCase()] = value;
      }
    } else {
      this.tableStorageModeOverridesMap = {};
    }
  }

  // #region Private Helpers

  private setStatus(message: string): void {
    this.statusCallback?.(message);
    logger.debug('SemanticModelBuilder', message);
  }

  // #endregion

  // #region generateTableTmdl

  /**
   * Generates complete TMDL for one table including columns, measures, and partition.
   * Handles lookup columns, choice/boolean virtual columns, multi-select picklists.
   * TDS mode: uses Value.NativeQuery with SQL.
   * FabricLink mode: uses Sql.Database with JOINs to metadata tables.
   */
  generateTableTmdl(
    table: ExportTable,
    attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
    requiredLookupColumns: Set<string>,
    dateTableConfig?: DateTableConfig | null,
    existingLineageTags?: Record<string, string> | null,
    existingColumnMetadata?: Record<string, ExistingColumnInfo> | null,
  ): string {
    const lines: string[] = [];
    const displayName = table.displayName ?? table.schemaName ?? table.logicalName;
    const tableLineageTag = getOrNewLineageTag(existingLineageTags, 'table');

    // Process view filter if present
    let viewFilterClause = '';
    let viewFilterComment = '';
    let viewDisplayName = '';
    let hasPartialSupport = false;

    if (table.view?.fetchXml?.trim()) {
      const utcOffset = Math.trunc(dateTableConfig?.utcOffsetHours ?? -6);
      const stripUserCtx = shouldStripUserContext(
        this.storageMode, table.role, table.logicalName, this.tableStorageModeOverridesMap,
      );
      const converter = new FetchXmlToSqlConverter(utcOffset, this.isFabricLink, stripUserCtx);
      const result = converter.convertToWhereClause(table.view.fetchXml, 'Base');

      if (result.sqlWhereClause?.trim()) {
        viewFilterClause = result.sqlWhereClause;
        viewDisplayName = table.view.viewName;
        hasPartialSupport = !result.isFullySupported;

        const commentParts: string[] = [];
        commentParts.push(`-- View Filter: ${viewDisplayName}${hasPartialSupport ? ' *' : ''}`);

        if (hasPartialSupport && result.unsupportedFeatures.length > 0) {
          commentParts.push('');
          commentParts.push('-- * Partially supported - some conditions were not translated:');
          for (const unsupported of result.unsupportedFeatures) {
            commentParts.push(`--   - ${unsupported}`);
          }
        }

        viewFilterComment = commentParts.join('\r\n');
        this.setStatus(`Applied view filter: ${viewDisplayName}${hasPartialSupport ? ' (partial)' : ''}`);
      }
    }

    // Table description comment
    if (table.logicalName) {
      lines.push(`/// Source: ${table.logicalName}`);
    }
    lines.push(`table ${quoteTmdlName(displayName)}`);
    lines.push(`\tlineageTag: ${tableLineageTag}`);
    lines.push('');

    // Collect columns and SQL fields
    const columns: ColumnInfo[] = [];
    const sqlFields: string[] = [];
    const joinClauses: string[] = [];
    const processedColumns = new Set<string>();

    const attrInfo = attributeDisplayInfo[table.logicalName] ?? {};

    // Always include primary key first
    const primaryKey = table.primaryIdAttribute ?? table.logicalName + 'id';
    const pkInAttrs = table.attributes.some(
      a => a.logicalName.toLowerCase() === primaryKey.toLowerCase(),
    );

    if (!pkInAttrs) {
      columns.push({
        logicalName: primaryKey,
        displayName: primaryKey,
        sourceColumn: primaryKey,
        isHidden: true,
        isKey: true,
        isRowLabel: false,
        description: `Source: ${table.logicalName}.${primaryKey}`,
        attributeType: 'uniqueidentifier',
      });
      sqlFields.push(`Base.${primaryKey}`);
      processedColumns.add(primaryKey.toLowerCase());
    }

    // Add required lookup columns for relationships
    for (const lookupCol of requiredLookupColumns) {
      const lookupLower = lookupCol.toLowerCase();
      const inAttrs = table.attributes.some(a => a.logicalName.toLowerCase() === lookupLower);
      if (!inAttrs && !processedColumns.has(lookupLower)) {
        columns.push({
          logicalName: lookupCol,
          displayName: lookupCol,
          sourceColumn: lookupCol,
          isHidden: true,
          isKey: false,
          isRowLabel: false,
          description: `Source: ${table.logicalName}.${lookupCol}`,
          attributeType: 'lookup',
        });
        sqlFields.push(`Base.${lookupCol}`);
        processedColumns.add(lookupLower);
      }
    }

    // Process attributes
    if (table.attributes) {
      for (const attr of table.attributes) {
        if (processedColumns.has(attr.logicalName.toLowerCase())) continue;

        const attrDisplayInfoItem = attrInfo[attr.logicalName] ?? undefined;
        const attrType = attr.attributeType ?? attrDisplayInfoItem?.attributeType ?? '';
        const attrDisplayName = attr.displayName ?? attrDisplayInfoItem?.displayName ?? attr.schemaName ?? attr.logicalName;
        const effectiveName = getEffectiveDisplayName(attrDisplayInfoItem, attrDisplayName, this.useDisplayNameAliasesInSql);
        const targets = attr.targets ?? attrDisplayInfoItem?.targets;

        // Skip statecode and special owning name columns
        const lowerName = attr.logicalName.toLowerCase();
        if (lowerName === 'statecode' ||
            lowerName === 'owningusername' ||
            lowerName === 'owningteamname' ||
            lowerName === 'owningbusinessunitname') {
          continue;
        }

        const isLookup = ['lookup', 'owner', 'customer'].includes(attrType.toLowerCase());
        const isChoice = ['picklist', 'state', 'status'].includes(attrType.toLowerCase());
        const isMultiSelectChoice = attrType.toLowerCase() === 'multiselectpicklist';
        const isBoolean = attrType.toLowerCase() === 'boolean';
        const isPrimaryKey = attr.logicalName.toLowerCase() === (table.primaryIdAttribute ?? '').toLowerCase();
        const isPrimaryName = attr.logicalName.toLowerCase() === (table.primaryNameAttribute ?? '').toLowerCase();

        const description = buildDescription(
          table.logicalName, attr.logicalName,
          attr.schemaName ?? attr.logicalName,
          attr.description, targets ?? null,
        );

        if (isLookup) {
          this.processLookupColumn(
            table, attr, columns, sqlFields, processedColumns,
            effectiveName, isPrimaryKey, isPrimaryName, description,
          );
        } else if (isChoice || isBoolean) {
          this.processChoiceColumn(
            table, attr, attrDisplayInfoItem, attrType,
            columns, sqlFields, joinClauses, processedColumns,
            effectiveName, isPrimaryName, description,
          );
        } else if (isMultiSelectChoice) {
          this.processMultiSelectColumn(
            table, attr, attrDisplayInfoItem,
            columns, sqlFields, joinClauses, processedColumns,
            effectiveName, isPrimaryName, description,
          );
        } else {
          this.processRegularColumn(
            table, attr, attrType, dateTableConfig,
            columns, sqlFields, processedColumns,
            effectiveName, isPrimaryKey, isPrimaryName, description,
          );
        }
      }
    }

    // Write columns
    for (const col of columns) {
      const mapping = mapDataType(col.attributeType, this.isFabricLink);
      let { formatString, summarizeBy } = mapping;
      const { dataType, sourceProviderType } = mapping;
      const isDateTime = col.attributeType?.toLowerCase() === 'dateonly' ||
                         col.attributeType?.toLowerCase() === 'datetime';

      // Check for existing column metadata to preserve user customizations
      const existingCol = existingColumnMetadata?.[col.sourceColumn];

      if (existingCol && existingCol.dataType === dataType) {
        if (existingCol.formatString != null) formatString = existingCol.formatString;
        if (existingCol.summarizeBy != null) summarizeBy = existingCol.summarizeBy;
      }

      if (col.description) {
        lines.push(`\t/// ${col.description}`);
      }
      lines.push(`\tcolumn ${quoteTmdlName(col.displayName)}`);
      lines.push(`\t\tdataType: ${dataType}`);
      if (formatString != null) {
        lines.push(`\t\tformatString: ${formatString}`);
      }
      if (sourceProviderType != null) {
        lines.push(`\t\tsourceProviderType: ${sourceProviderType}`);
      }
      if (col.isHidden) {
        lines.push('\t\tisHidden');
      }
      if (col.isKey) {
        lines.push('\t\tisKey');
      }
      lines.push(`\t\tlineageTag: ${getOrNewLineageTag(existingLineageTags, `col:${col.sourceColumn}`)}`);
      if (col.isRowLabel) {
        lines.push('\t\tisDefaultLabel');
      }
      lines.push(`\t\tsummarizeBy: ${summarizeBy}`);
      lines.push(`\t\tsourceColumn: ${col.sourceColumn}`);
      lines.push('');
      if (isDateTime) {
        lines.push('\t\tchangedProperty = DataType');
        lines.push('');
      }
      lines.push('\t\tannotation SummarizationSetBy = Automatic');
      if (isDateTime) {
        lines.push('');
        lines.push('\t\tannotation UnderlyingDateTimeDataType = Date');
      }

      // Preserve user-added annotations
      if (existingCol) {
        for (const [annKey, annValue] of Object.entries(existingCol.annotations)) {
          if (!TOOL_ANNOTATIONS.has(annKey.toLowerCase())) {
            lines.push('');
            lines.push(`\t\tannotation ${annKey} = ${annValue}`);
          }
        }
      }

      lines.push('');
    }

    // Auto-generate measures for fact tables
    if (table.role === 'Fact') {
      const entityLogicalName = table.logicalName;
      const factPrimaryKey = table.primaryIdAttribute ?? (table.logicalName + 'id');

      lines.push(`\tmeasure 'Link to ${displayName}' = \`\`\``);
      lines.push('\t\t\t');
      lines.push(`\t\t\t"https://" & DataverseURL & "/main.aspx?pagetype=entityrecord&etn=${entityLogicalName}&id=" `);
      lines.push(`\t\t\t\t& SELECTEDVALUE('${displayName}'[${factPrimaryKey}], BLANK())`);
      lines.push('\t\t\t```');
      lines.push(`\t\tlineageTag: ${getOrNewLineageTag(existingLineageTags, `measure:Link to ${displayName}`)}`);
      lines.push('\t\tdataCategory: WebUrl');
      lines.push('');

      lines.push(`\tmeasure '${displayName} Count' = COUNTROWS('${displayName}')`);
      lines.push('\t\tformatString: 0');
      lines.push(`\t\tlineageTag: ${getOrNewLineageTag(existingLineageTags, `measure:${displayName} Count`)}`);
      lines.push('');
    }

    // Write partition
    const fromTable = this.isFabricLink ? table.logicalName : (table.schemaName ?? table.logicalName);
    const partitionMode = getPartitionMode(
      this.storageMode, table.role, table.logicalName, this.tableStorageModeOverridesMap,
    );

    // Build SQL SELECT list with proper formatting
    const sqlSelectParts: string[] = [];
    for (let i = 0; i < sqlFields.length; i++) {
      if (i === 0) {
        sqlSelectParts.push(`SELECT ${sqlFields[i]}`);
      } else {
        sqlSelectParts.push(`\r\n\t\t\t\t        ,${sqlFields[i]}`);
      }
    }
    const sqlSelectList = sqlSelectParts.join('');

    const partitionName = displayName;
    lines.push(`\tpartition ${quoteTmdlName(partitionName)} = m`);
    lines.push(`\t\tmode: ${partitionMode}`);
    lines.push(`\t\tmode: ${partitionMode}`);
    lines.push('\t\tsource =');
    lines.push('\t\t\t\tlet');

    if (this.isFabricLink) {
      lines.push('\t\t\t\t    Source = Sql.Database(FabricSQLEndpoint, FabricLakehouse,');
      lines.push('\t\t\t\t    [Query="');
    } else {
      lines.push('\t\t\t\t    Dataverse = CommonDataService.Database(DataverseURL,[CreateNavigationProperties=false]),');
      lines.push('\t\t\t\t    Source = Value.NativeQuery(Dataverse,"');
    }

    // Blank line after query opening
    lines.push('\t\t\t\t');

    // Add filter comment if present
    if (viewFilterComment.trim()) {
      for (const commentLine of viewFilterComment.split(/\r\n|\n/)) {
        if (commentLine.trim()) {
          lines.push(`\t\t\t\t    ${commentLine}`);
        }
      }
      lines.push('\t\t\t\t');
    }

    lines.push(`\t\t\t\t    ${sqlSelectList}`);
    lines.push(`\t\t\t\t    FROM ${fromTable} AS Base`);

    // JOIN clauses for FabricLink
    for (const joinClause of joinClauses) {
      lines.push(`\t\t\t\t    ${joinClause}`);
    }

    // WHERE clause
    if (viewFilterClause.trim()) {
      lines.push(`\t\t\t\t    WHERE ${viewFilterClause}`);
    } else if (table.hasStateCode) {
      lines.push('\t\t\t\t    WHERE Base.statecode = 0');
    }

    if (this.isFabricLink) {
      lines.push('\t\t\t\t        "');
      lines.push('\t\t\t\t    , CreateNavigationProperties=false])');
    } else {
      lines.push('\t\t\t\t    " ,null ,[EnableFolding=true])');
    }

    lines.push('\t\t\t\tin');
    lines.push('\t\t\t\t    Source');
    lines.push('');
    lines.push('\tannotation PBI_NavigationStepName = Navigation');
    lines.push('');
    lines.push('\tannotation PBI_ResultType = Table');
    lines.push('');

    return lines.join('\r\n') + '\r\n';
  }

  // #endregion

  // #region Column Processing Helpers

  private processLookupColumn(
    _table: ExportTable,
    attr: { logicalName: string; displayName?: string; schemaName?: string },
    columns: ColumnInfo[],
    sqlFields: string[],
    processedColumns: Set<string>,
    effectiveName: string,
    isPrimaryKey: boolean,
    isPrimaryName: boolean,
    description: string,
  ): void {
    // Hidden ID column
    columns.push({
      logicalName: attr.logicalName,
      displayName: attr.logicalName,
      sourceColumn: attr.logicalName,
      isHidden: true,
      isKey: isPrimaryKey,
      isRowLabel: false,
      description,
      attributeType: 'lookup',
    });
    sqlFields.push(`Base.${attr.logicalName}`);

    // Visible name column
    const nameColumn = attr.logicalName + 'name';
    const lowerName = attr.logicalName.toLowerCase();
    const isOwningLookup = lowerName === 'owninguser' ||
                           lowerName === 'owningteam' ||
                           lowerName === 'owningbusinessunit';

    if (!processedColumns.has(nameColumn.toLowerCase()) && !isOwningLookup) {
      const lookupSourceCol = this.useDisplayNameAliasesInSql ? effectiveName : nameColumn;
      columns.push({
        logicalName: nameColumn,
        displayName: effectiveName,
        sourceColumn: lookupSourceCol,
        isHidden: false,
        isKey: false,
        isRowLabel: isPrimaryName,
        description,
        attributeType: 'string',
      });
      sqlFields.push(applySqlAlias(`Base.${nameColumn}`, effectiveName, nameColumn, false, this.useDisplayNameAliasesInSql));
    }
    processedColumns.add(nameColumn.toLowerCase());
    processedColumns.add(attr.logicalName.toLowerCase());
  }

  private processChoiceColumn(
    table: ExportTable,
    attr: { logicalName: string; isGlobal?: boolean; optionSetName?: string },
    attrDisplayInfo: AttributeDisplayInfo | undefined,
    attrType: string,
    columns: ColumnInfo[],
    sqlFields: string[],
    joinClauses: string[],
    processedColumns: Set<string>,
    effectiveName: string,
    isPrimaryName: boolean,
    description: string,
  ): void {
    if (this.isFabricLink) {
      this.processChoiceFabricLink(
        table, attr, attrDisplayInfo, attrType,
        columns, sqlFields, joinClauses, processedColumns,
        effectiveName, isPrimaryName, description,
      );
    } else {
      this.processChoiceTds(
        table, attr, attrDisplayInfo,
        columns, sqlFields, processedColumns,
        effectiveName, isPrimaryName, description,
      );
    }
  }

  private processChoiceFabricLink(
    table: ExportTable,
    attr: { logicalName: string; isGlobal?: boolean; optionSetName?: string },
    attrDisplayInfo: AttributeDisplayInfo | undefined,
    attrType: string,
    columns: ColumnInfo[],
    sqlFields: string[],
    joinClauses: string[],
    processedColumns: Set<string>,
    effectiveName: string,
    isPrimaryName: boolean,
    description: string,
  ): void {
    const isState = attrType.toLowerCase() === 'state';
    const isStatus = attrType.toLowerCase() === 'status';
    const isBoolean = attrType.toLowerCase() === 'boolean';
    const nameColumn = attr.logicalName + 'name';
    const joinAlias = `${table.logicalName}_${attr.logicalName}`;

    if (isState) {
      joinClauses.push(
        `JOIN [StateMetadata] ${joinAlias}\r\n` +
        `\t\t\t\t            ON  ${joinAlias}.[EntityName] = '${table.logicalName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[LocalizedLabelLanguageCode] = ${this.languageCode}\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[State] = Base.${attr.logicalName}`,
      );
    } else if (isStatus) {
      joinClauses.push(
        `JOIN [StatusMetadata] ${joinAlias}\r\n` +
        `\t\t\t\t            ON  ${joinAlias}.[EntityName] = '${table.logicalName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[LocalizedLabelLanguageCode] = ${this.languageCode}\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[State] = Base.statecode\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[Status] = Base.statuscode`,
      );
    } else if (isBoolean) {
      const optionSetName = attr.optionSetName ?? attrDisplayInfo?.optionSetName ?? attr.logicalName;
      joinClauses.push(
        `LEFT JOIN [GlobalOptionsetMetadata] ${joinAlias}\r\n` +
        `\t\t\t\t            ON  ${joinAlias}.[OptionSetName] = '${optionSetName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[EntityName] = '${table.logicalName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[LocalizedLabelLanguageCode] = ${this.languageCode}\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[Option] = Base.${attr.logicalName}`,
      );
    } else {
      const isGlobal = attr.isGlobal ?? attrDisplayInfo?.isGlobal ?? false;
      const optionSetName = attr.optionSetName ?? attrDisplayInfo?.optionSetName ?? attr.logicalName;
      const metadataTable = isGlobal ? 'GlobalOptionsetMetadata' : 'OptionsetMetadata';
      joinClauses.push(
        `LEFT JOIN [${metadataTable}] ${joinAlias}\r\n` +
        `\t\t\t\t            ON  ${joinAlias}.[OptionSetName] = '${optionSetName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[EntityName] = '${table.logicalName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[LocalizedLabelLanguageCode] = ${this.languageCode}\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[Option] = Base.${attr.logicalName}`,
      );
    }

    if (!processedColumns.has(nameColumn.toLowerCase())) {
      const fabricChoiceSourceCol = this.useDisplayNameAliasesInSql ? effectiveName : nameColumn;
      const fabricChoiceAlias = this.useDisplayNameAliasesInSql &&
        effectiveName.toLowerCase() !== nameColumn.toLowerCase()
        ? `${joinAlias}.[LocalizedLabel] AS [${effectiveName}]`
        : `${joinAlias}.[LocalizedLabel] ${nameColumn}`;
      sqlFields.push(fabricChoiceAlias);

      columns.push({
        logicalName: nameColumn,
        displayName: effectiveName,
        sourceColumn: fabricChoiceSourceCol,
        isHidden: false,
        isKey: false,
        isRowLabel: isPrimaryName,
        description,
        attributeType: 'string',
      });
    }
    processedColumns.add(nameColumn.toLowerCase());
    processedColumns.add(attr.logicalName.toLowerCase());
  }

  private processChoiceTds(
    table: ExportTable,
    attr: { logicalName: string },
    attrDisplayInfo: AttributeDisplayInfo | undefined,
    columns: ColumnInfo[],
    sqlFields: string[],
    processedColumns: Set<string>,
    effectiveName: string,
    isPrimaryName: boolean,
    description: string,
  ): void {
    let nameColumn = attrDisplayInfo?.virtualAttributeName ?? (attr.logicalName + 'name');
    nameColumn = getVirtualColumnName(table.logicalName, nameColumn);

    if (!processedColumns.has(nameColumn.toLowerCase())) {
      const tdsChoiceSourceCol = this.useDisplayNameAliasesInSql ? effectiveName : nameColumn;
      columns.push({
        logicalName: nameColumn,
        displayName: effectiveName,
        sourceColumn: tdsChoiceSourceCol,
        isHidden: false,
        isKey: false,
        isRowLabel: isPrimaryName,
        description,
        attributeType: 'string',
      });
      sqlFields.push(applySqlAlias(`Base.${nameColumn}`, effectiveName, nameColumn, false, this.useDisplayNameAliasesInSql));
    }
    processedColumns.add(nameColumn.toLowerCase());
    processedColumns.add(attr.logicalName.toLowerCase());
  }

  private processMultiSelectColumn(
    table: ExportTable,
    attr: { logicalName: string; isGlobal?: boolean; optionSetName?: string },
    attrDisplayInfo: AttributeDisplayInfo | undefined,
    columns: ColumnInfo[],
    sqlFields: string[],
    joinClauses: string[],
    processedColumns: Set<string>,
    effectiveName: string,
    isPrimaryName: boolean,
    description: string,
  ): void {
    let nameColumn: string;

    if (this.isFabricLink) {
      nameColumn = attr.logicalName + 'name';
      const applyAlias = `mspl_${attr.logicalName}`;
      const joinAlias = `meta_${attr.logicalName}`;
      const isGlobal = attr.isGlobal ?? attrDisplayInfo?.isGlobal ?? false;
      const optionSetName = attr.optionSetName ?? attrDisplayInfo?.optionSetName ?? attr.logicalName;
      const metadataTable = isGlobal ? 'GlobalOptionsetMetadata' : 'OptionsetMetadata';

      joinClauses.push(
        `OUTER APPLY (\r\n` +
        `\t\t\t\t        SELECT STRING_AGG(${joinAlias}.[LocalizedLabel], ', ') AS ${nameColumn}\r\n` +
        `\t\t\t\t        FROM STRING_SPLIT(CAST(Base.${attr.logicalName} AS VARCHAR(4000)), ',') AS split\r\n` +
        `\t\t\t\t        JOIN [${metadataTable}] AS ${joinAlias}\r\n` +
        `\t\t\t\t            ON  ${joinAlias}.[OptionSetName] = '${optionSetName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[EntityName] = '${table.logicalName}'\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[LocalizedLabelLanguageCode] = ${this.languageCode}\r\n` +
        `\t\t\t\t            AND ${joinAlias}.[Option] = CAST(LTRIM(RTRIM(split.value)) AS INT)\r\n` +
        `\t\t\t\t        WHERE Base.${attr.logicalName} IS NOT NULL\r\n` +
        `\t\t\t\t    ) ${applyAlias}`,
      );

      if (!processedColumns.has(nameColumn.toLowerCase())) {
        sqlFields.push(applySqlAlias(
          `${applyAlias}.${nameColumn}`, effectiveName, nameColumn, false, this.useDisplayNameAliasesInSql,
        ));
      }
    } else {
      nameColumn = attrDisplayInfo?.virtualAttributeName ?? (attr.logicalName + 'name');
      nameColumn = getVirtualColumnName(table.logicalName, nameColumn);

      if (!processedColumns.has(nameColumn.toLowerCase())) {
        sqlFields.push(applySqlAlias(
          `Base.${nameColumn}`, effectiveName, nameColumn, false, this.useDisplayNameAliasesInSql,
        ));
      }
    }

    if (!processedColumns.has(nameColumn.toLowerCase())) {
      const msSourceCol = this.useDisplayNameAliasesInSql ? effectiveName : nameColumn;
      columns.push({
        logicalName: nameColumn,
        displayName: effectiveName,
        sourceColumn: msSourceCol,
        isHidden: false,
        isKey: false,
        isRowLabel: isPrimaryName,
        description,
        attributeType: 'string',
      });
    }
    processedColumns.add(nameColumn.toLowerCase());
    processedColumns.add(attr.logicalName.toLowerCase());
  }

  private processRegularColumn(
    table: ExportTable,
    attr: { logicalName: string; attributeType?: string },
    attrType: string,
    dateTableConfig: DateTableConfig | null | undefined,
    columns: ColumnInfo[],
    sqlFields: string[],
    processedColumns: Set<string>,
    effectiveName: string,
    isPrimaryKey: boolean,
    isPrimaryName: boolean,
    description: string,
  ): void {
    const isDateTime = attrType.toLowerCase() === 'datetime';
    const shouldWrapDateTime = isDateTime && dateTableConfig != null &&
      dateTableConfig.wrappedFields.some(f =>
        f.tableName.toLowerCase() === table.logicalName.toLowerCase() &&
        f.fieldName.toLowerCase() === attr.logicalName.toLowerCase(),
      );

    const effectiveAttrType = shouldWrapDateTime ? 'dateonly' : attrType;
    const regularSourceCol = isPrimaryKey ? attr.logicalName
      : (this.useDisplayNameAliasesInSql ? effectiveName : attr.logicalName);

    columns.push({
      logicalName: attr.logicalName,
      displayName: isPrimaryKey ? attr.logicalName : effectiveName,
      sourceColumn: regularSourceCol,
      isHidden: isPrimaryKey,
      isKey: isPrimaryKey,
      isRowLabel: isPrimaryName,
      description,
      attributeType: effectiveAttrType,
    });

    if (shouldWrapDateTime) {
      const offset = dateTableConfig!.utcOffsetHours;
      const dtAlias = isPrimaryKey ? attr.logicalName
        : (this.useDisplayNameAliasesInSql ? effectiveName : attr.logicalName);
      const dtAliasClause = dtAlias.toLowerCase() === attr.logicalName.toLowerCase()
        ? `AS ${attr.logicalName}` : `AS [${dtAlias}]`;
      sqlFields.push(`CAST(DATEADD(hour, ${offset}, Base.${attr.logicalName}) AS DATE) ${dtAliasClause}`);
    } else {
      sqlFields.push(applySqlAlias(
        `Base.${attr.logicalName}`, effectiveName, attr.logicalName, isPrimaryKey, this.useDisplayNameAliasesInSql,
      ));
    }
    processedColumns.add(attr.logicalName.toLowerCase());
  }

  // #endregion

  // #region generateRelationshipsTmdl

  /**
   * Generates relationships.tmdl with all relationships.
   * Includes date table relationship if configured.
   * Preserves existing relationship GUIDs.
   */
  generateRelationshipsTmdl(
    tables: ExportTable[],
    relationships: ExportRelationship[],
    attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
    dateTableConfig?: DateTableConfig | null,
    existingRelationshipGuids?: Record<string, string> | null,
  ): string {
    const lines: string[] = [];

    // Create lookup for table display names and primary keys
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

      const sourceColumn = rel.sourceAttribute;
      const targetColumn = targetPrimaryKey;

      const fromRef = `${quoteTmdlName(sourceTableDisplay)}.${quoteTmdlName(sourceColumn)}`;
      const toRef = `${quoteTmdlName(targetTableDisplay)}.${quoteTmdlName(targetColumn)}`;
      const relKey = `${fromRef}\u2192${toRef}`;
      const relGuid = existingRelationshipGuids?.[relKey] ?? newGuid();

      lines.push(`relationship ${relGuid}`);

      if (rel.assumeReferentialIntegrity || rel.isSnowflake) {
        lines.push('\trelyOnReferentialIntegrity');
      }

      if (!rel.isActive) {
        lines.push('\tisActive: false');
      }

      lines.push(`\tfromColumn: ${quoteTmdlName(sourceTableDisplay)}.${quoteTmdlName(sourceColumn)}`);
      lines.push(`\ttoColumn: ${quoteTmdlName(targetTableDisplay)}.${quoteTmdlName(targetColumn)}`);
      lines.push('');
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
          let isDateFieldRequired = false;
          let primaryDateFieldName = dateAttr.displayName ?? dateAttr.schemaName ?? dateAttr.logicalName;

          const tableAttrs = attributeDisplayInfo[dateTableConfig.primaryDateTable];
          const fieldDisplayInfo = tableAttrs?.[dateTableConfig.primaryDateField];
          if (fieldDisplayInfo) {
            isDateFieldRequired = fieldDisplayInfo.isRequired;
            primaryDateFieldName = getEffectiveDisplayName(
              fieldDisplayInfo, fieldDisplayInfo.displayName ?? primaryDateFieldName, this.useDisplayNameAliasesInSql,
            );
          }

          const dateFromRef = `${quoteTmdlName(sourceTableDisplay)}.${quoteTmdlName(primaryDateFieldName)}`;
          const dateToRef = 'Date.Date';
          const dateRelKey = `${dateFromRef}\u2192${dateToRef}`;
          const dateRelGuid = existingRelationshipGuids?.[dateRelKey] ?? newGuid();

          lines.push(`relationship ${dateRelGuid}`);

          if (isDateFieldRequired) {
            lines.push('\trelyOnReferentialIntegrity');
          }

          lines.push(`\tfromColumn: ${quoteTmdlName(sourceTableDisplay)}.${quoteTmdlName(primaryDateFieldName)}`);
          lines.push('\ttoColumn: Date.Date');
          lines.push('');
        } else {
          logger.debug(
            'SemanticModelBuilder',
            `Date relationship skipped: '${dateTableConfig.primaryDateTable}.${dateTableConfig.primaryDateField}' not found in selected attributes.`,
          );
        }
      }
    }

    return lines.join('\r\n') + '\r\n';
  }

  // #endregion

  // #region generateDateTableTmdl

  /**
   * Generates Date dimension table TMDL using DAX-based partition.
   * Uses the provided template content with year range substitution.
   * Note: In the PPTB port, the template content must be provided directly
   * since we don't have embedded resources.
   */
  generateDateTableTmdl(dateTableConfig: DateTableConfig, templateContent: string): string {
    let content = templateContent;

    // Update _startdate: DATE(YYYY,1,1) -> DATE(config.startYear,1,1)
    content = content.replace(
      /VAR _startdate\s*=\s*\r?\n\s*DATE\(\d+,\s*1,\s*1\)/,
      `VAR _startdate =\r\n\t\t\t\t    DATE(${dateTableConfig.startYear},1,1)`,
    );

    // Update _enddate: DATE(YYYY,1,1)-1 -> DATE(config.endYear+1,1,1)-1
    content = content.replace(
      /VAR _enddate\s*=\s*\r?\n\s*DATE\(\d+,\s*1,\s*1\)\s*-\s*1/,
      `VAR _enddate =\r\n\t\t\t\t\tDATE(${dateTableConfig.endYear + 1},1,1)-1`,
    );

    return content;
  }

  // #endregion

  // #region generateDataverseUrlTableTmdl

  /**
   * Generates hidden DataverseURL parameter table TMDL.
   * Mode: import partition with IsParameterQuery meta.
   */
  generateDataverseUrlTableTmdl(
    normalizedUrl: string,
    existingTags?: Record<string, string> | null,
  ): string {
    const lines: string[] = [];

    lines.push('table DataverseURL');
    lines.push('\tisHidden');
    lines.push('\tlineageTag: ' + getOrNewLineageTag(existingTags, 'table'));
    lines.push('');
    lines.push('\tcolumn DataverseURL');
    lines.push('\t\tdataType: string');
    lines.push('\t\tisHidden');
    lines.push('\t\tlineageTag: ' + getOrNewLineageTag(existingTags, 'col:DataverseURL'));
    lines.push('\t\tsummarizeBy: none');
    lines.push('\t\tsourceColumn: DataverseURL');
    lines.push('');
    lines.push('\t\tchangedProperty = IsHidden');
    lines.push('');
    lines.push('\t\tannotation SummarizationSetBy = Automatic');
    lines.push('');
    lines.push('\tpartition DataverseURL = m');
    lines.push('\t\tmode: import');
    lines.push(`\t\tsource = "${normalizedUrl}" meta [IsParameterQuery=true, Type="Any", IsParameterQueryRequired=true]`);
    lines.push('');
    lines.push('\tchangedProperty = IsHidden');
    lines.push('');
    lines.push('\tannotation PBI_NavigationStepName = Navigation');
    lines.push('');
    lines.push('\tannotation PBI_ResultType = Text');
    lines.push('');

    return lines.join('\r\n') + '\r\n';
  }

  // #endregion

  // #region generateFabricLinkExpressions

  /**
   * Generates expressions.tmdl with FabricSQLEndpoint and FabricLakehouse expressions.
   */
  generateFabricLinkExpressions(
    endpoint: string,
    database: string,
    existingTags?: Record<string, string> | null,
  ): string {
    const lines: string[] = [];

    lines.push(`expression FabricSQLEndpoint = "${endpoint}" meta [IsParameterQuery=true, Type="Any", IsParameterQueryRequired=true]`);
    lines.push(`\tlineageTag: ${getOrNewLineageTag(existingTags, 'expr:FabricSQLEndpoint')}`);
    lines.push('');
    lines.push('\tannotation PBI_ResultType = Text');
    lines.push('');

    lines.push(`expression FabricLakehouse = "${database}" meta [IsParameterQuery=true, Type="Any", IsParameterQueryRequired=true]`);
    lines.push(`\tlineageTag: ${getOrNewLineageTag(existingTags, 'expr:FabricLakehouse')}`);
    lines.push('');
    lines.push('\tannotation PBI_NavigationStepName = Navigation');
    lines.push('');
    lines.push('\tannotation PBI_ResultType = Text');

    return lines.join('\r\n') + '\r\n';
  }

  // #endregion

  // #region generateModelTmdl

  /**
   * Generates model.tmdl with culture, data access options, table references.
   * Includes QueryOrder annotation and expression refs for FabricLink.
   */
  generateModelTmdl(
    tables: ExportTable[],
    isFabricLink: boolean,
    includeDateTable: boolean,
  ): string {
    const lines: string[] = [];

    lines.push('model Model');
    lines.push('\tculture: en-US');
    lines.push('\tdefaultPowerBIDataSourceVersion: powerBI_V3');
    lines.push('\tsourceQueryCulture: en-US');
    lines.push('\tdataAccessOptions');
    lines.push('\t\tlegacyRedirects');
    lines.push('\t\treturnErrorValuesAsNull');
    lines.push('');
    lines.push('annotation __PBI_TimeIntelligenceEnabled = 0');
    lines.push('');

    // Build PBI_QueryOrder annotation
    const tableNames = tables.map(t => t.displayName ?? t.schemaName ?? t.logicalName);
    if (isFabricLink) {
      tableNames.unshift('DataverseURL');
      tableNames.unshift('FabricLakehouse');
      tableNames.unshift('FabricSQLEndpoint');
    } else {
      tableNames.unshift('DataverseURL');
    }
    if (includeDateTable) {
      tableNames.push('Date');
    }

    const queryOrder = tableNames.map(n => `"${n}"`).join(',');
    lines.push(`annotation PBI_QueryOrder = [${queryOrder}]`);
    lines.push('');
    lines.push('annotation PBI_ProTooling = ["TMDLView_Desktop","DevMode","TMDL-Extension"]');
    lines.push('');

    // Write ref table entries
    for (const table of tables) {
      const dn = table.displayName ?? table.schemaName ?? table.logicalName;
      if (dn.includes(' ')) {
        lines.push(`ref table '${dn}'`);
      } else {
        lines.push(`ref table ${dn}`);
      }
    }

    if (includeDateTable) {
      lines.push('ref table Date');
    }

    if (!isFabricLink) {
      lines.push('ref table DataverseURL');
    }
    lines.push('');

    // FabricLink expression refs
    if (isFabricLink) {
      lines.push('ref expression FabricSQLEndpoint');
      lines.push('ref expression FabricLakehouse');
      lines.push('ref expression DataverseURL');
    }
    lines.push('');

    lines.push('ref cultureInfo en-US');
    lines.push('');

    return lines.join('\r\n') + '\r\n';
  }

  // #endregion

  // #region generateMQuery

  /**
   * Generates the SQL query for a table partition (used for comparison).
   * Must match the logic in generateTableTmdl exactly.
   */
  generateMQuery(
    table: ExportTable,
    requiredLookupColumns: Set<string>,
    dateTableConfig?: DateTableConfig | null,
    attributeDisplayInfo?: Record<string, Record<string, AttributeDisplayInfo>> | null,
  ): string {
    const fromTable = this.isFabricLink ? table.logicalName : (table.schemaName ?? table.logicalName);
    const sqlFields: string[] = [];
    const joinClauses: string[] = [];
    const processedColumns = new Set<string>();

    const attrInfo = (attributeDisplayInfo && attributeDisplayInfo[table.logicalName])
      ? attributeDisplayInfo[table.logicalName]
      : {};

    const primaryKey = table.primaryIdAttribute ?? table.logicalName + 'id';

    // Only add primary key if NOT in attributes
    if (!table.attributes.some(a => a.logicalName.toLowerCase() === primaryKey.toLowerCase())) {
      sqlFields.push(`Base.${primaryKey}`);
      processedColumns.add(primaryKey.toLowerCase());
    }

    // Add required lookup columns not in attributes
    for (const lookupCol of requiredLookupColumns) {
      const lookupLower = lookupCol.toLowerCase();
      if (!table.attributes.some(a => a.logicalName.toLowerCase() === lookupLower) &&
          !processedColumns.has(lookupLower)) {
        sqlFields.push(`Base.${lookupCol}`);
        processedColumns.add(lookupLower);
      }
    }

    // Process attributes
    if (table.attributes) {
      for (const attr of table.attributes) {
        if (processedColumns.has(attr.logicalName.toLowerCase())) continue;

        const attrType = attr.attributeType ?? '';
        const attrDisplayInfo2 = attrInfo[attr.logicalName] ?? undefined;
        const attrDisplayName = attr.displayName ?? attrDisplayInfo2?.displayName ?? attr.schemaName ?? attr.logicalName;
        const effectiveName = getEffectiveDisplayName(attrDisplayInfo2, attrDisplayName, this.useDisplayNameAliasesInSql);

        // Skip statecode and special owning name columns
        const lowerName = attr.logicalName.toLowerCase();
        if (lowerName === 'statecode' ||
            lowerName === 'owningusername' ||
            lowerName === 'owningteamname' ||
            lowerName === 'owningbusinessunitname') {
          continue;
        }

        const isLookup = ['lookup', 'owner', 'customer'].includes(attrType.toLowerCase());
        const isChoice = ['picklist', 'state', 'status'].includes(attrType.toLowerCase());
        const isMultiSelectChoice = attrType.toLowerCase() === 'multiselectpicklist';
        const isBoolean = attrType.toLowerCase() === 'boolean';
        const isPrimaryKey = attr.logicalName.toLowerCase() === (table.primaryIdAttribute ?? '').toLowerCase();

        if (isLookup) {
          sqlFields.push(`Base.${attr.logicalName}`);
          const nameColumn = attr.logicalName + 'name';
          const isOwningLookup = lowerName === 'owninguser' || lowerName === 'owningteam' || lowerName === 'owningbusinessunit';

          if (!processedColumns.has(nameColumn.toLowerCase()) && !isOwningLookup) {
            sqlFields.push(applySqlAlias(`Base.${nameColumn}`, effectiveName, nameColumn, false, this.useDisplayNameAliasesInSql));
          }
          processedColumns.add(nameColumn.toLowerCase());
          processedColumns.add(attr.logicalName.toLowerCase());
        } else if (isChoice || isBoolean) {
          if (this.isFabricLink) {
            const isState = attrType.toLowerCase() === 'state';
            const isStatus2 = attrType.toLowerCase() === 'status';
            const joinAlias = `${table.logicalName}_${attr.logicalName}`;
            const nameColumn = attr.logicalName + 'name';

            if (isState) {
              joinClauses.push(`JOIN [StateMetadata] ${joinAlias} ON ${joinAlias}.[EntityName]='${table.logicalName}' AND ${joinAlias}.[LocalizedLabelLanguageCode]=${this.languageCode} AND ${joinAlias}.[State]=Base.${attr.logicalName}`);
            } else if (isStatus2) {
              joinClauses.push(`JOIN [StatusMetadata] ${joinAlias} ON ${joinAlias}.[EntityName]='${table.logicalName}' AND ${joinAlias}.[LocalizedLabelLanguageCode]=${this.languageCode} AND ${joinAlias}.[State]=Base.statecode AND ${joinAlias}.[Status]=Base.statuscode`);
            } else if (isBoolean) {
              const optionSetName = attr.optionSetName ?? attrDisplayInfo2?.optionSetName ?? attr.logicalName;
              joinClauses.push(`LEFT JOIN [GlobalOptionsetMetadata] ${joinAlias} ON ${joinAlias}.[OptionSetName]='${optionSetName}' AND ${joinAlias}.[EntityName]='${table.logicalName}' AND ${joinAlias}.[LocalizedLabelLanguageCode]=${this.languageCode} AND ${joinAlias}.[Option]=Base.${attr.logicalName}`);
            } else {
              const isGlobal = attr.isGlobal ?? attrDisplayInfo2?.isGlobal ?? false;
              const optionSetName = attr.optionSetName ?? attrDisplayInfo2?.optionSetName ?? attr.logicalName;
              const metadataTable = isGlobal ? 'GlobalOptionsetMetadata' : 'OptionsetMetadata';
              joinClauses.push(`LEFT JOIN [${metadataTable}] ${joinAlias} ON ${joinAlias}.[OptionSetName]='${optionSetName}' AND ${joinAlias}.[EntityName]='${table.logicalName}' AND ${joinAlias}.[LocalizedLabelLanguageCode]=${this.languageCode} AND ${joinAlias}.[Option]=Base.${attr.logicalName}`);
            }
            if (!processedColumns.has(nameColumn.toLowerCase())) {
              const fabricChoiceAlias = this.useDisplayNameAliasesInSql && effectiveName.toLowerCase() !== nameColumn.toLowerCase()
                ? `${joinAlias}.[LocalizedLabel] AS [${effectiveName}]`
                : `${joinAlias}.[LocalizedLabel] ${nameColumn}`;
              sqlFields.push(fabricChoiceAlias);
            }
            processedColumns.add(nameColumn.toLowerCase());
            processedColumns.add(attr.logicalName.toLowerCase());
          } else {
            let nameColumn2 = attrDisplayInfo2?.virtualAttributeName ?? (attr.logicalName + 'name');
            nameColumn2 = getVirtualColumnName(table.logicalName, nameColumn2);

            if (!processedColumns.has(nameColumn2.toLowerCase())) {
              sqlFields.push(applySqlAlias(`Base.${nameColumn2}`, effectiveName, nameColumn2, false, this.useDisplayNameAliasesInSql));
            }
            processedColumns.add(nameColumn2.toLowerCase());
            processedColumns.add(attr.logicalName.toLowerCase());
          }
        } else if (isMultiSelectChoice) {
          let nameColumn: string;

          if (this.isFabricLink) {
            nameColumn = attr.logicalName + 'name';
            const applyAlias = `mspl_${attr.logicalName}`;
            const joinAlias2 = `meta_${attr.logicalName}`;
            const isGlobal = attr.isGlobal ?? attrDisplayInfo2?.isGlobal ?? false;
            const optionSetName = attr.optionSetName ?? attrDisplayInfo2?.optionSetName ?? attr.logicalName;
            const metadataTable = isGlobal ? 'GlobalOptionsetMetadata' : 'OptionsetMetadata';

            joinClauses.push(`OUTER APPLY (SELECT STRING_AGG(${joinAlias2}.[LocalizedLabel], ', ') AS ${nameColumn} FROM STRING_SPLIT(CAST(Base.${attr.logicalName} AS VARCHAR(4000)), ',') AS split JOIN [${metadataTable}] AS ${joinAlias2} ON ${joinAlias2}.[OptionSetName]='${optionSetName}' AND ${joinAlias2}.[EntityName]='${table.logicalName}' AND ${joinAlias2}.[LocalizedLabelLanguageCode]=${this.languageCode} AND ${joinAlias2}.[Option]=CAST(LTRIM(RTRIM(split.value)) AS INT) WHERE Base.${attr.logicalName} IS NOT NULL) ${applyAlias}`);
            if (!processedColumns.has(nameColumn.toLowerCase())) {
              sqlFields.push(applySqlAlias(`${applyAlias}.${nameColumn}`, effectiveName, nameColumn, false, this.useDisplayNameAliasesInSql));
            }
          } else {
            nameColumn = attrDisplayInfo2?.virtualAttributeName ?? (attr.logicalName + 'name');
            nameColumn = getVirtualColumnName(table.logicalName, nameColumn);

            if (!processedColumns.has(nameColumn.toLowerCase())) {
              sqlFields.push(applySqlAlias(`Base.${nameColumn}`, effectiveName, nameColumn, false, this.useDisplayNameAliasesInSql));
            }
          }
          processedColumns.add(nameColumn.toLowerCase());
          processedColumns.add(attr.logicalName.toLowerCase());
        } else {
          // Regular column
          const isDateTime = attrType.toLowerCase() === 'datetime';
          const shouldWrapDateTime = isDateTime && dateTableConfig != null &&
            dateTableConfig.wrappedFields.some(f =>
              f.tableName.toLowerCase() === table.logicalName.toLowerCase() &&
              f.fieldName.toLowerCase() === attr.logicalName.toLowerCase(),
            );

          if (shouldWrapDateTime) {
            const offset = dateTableConfig!.utcOffsetHours;
            const dtAlias = isPrimaryKey ? attr.logicalName
              : (this.useDisplayNameAliasesInSql ? effectiveName : attr.logicalName);
            const dtAliasClause = dtAlias.toLowerCase() === attr.logicalName.toLowerCase()
              ? `AS ${attr.logicalName}` : `AS [${dtAlias}]`;
            sqlFields.push(`CAST(DATEADD(hour, ${offset}, Base.${attr.logicalName}) AS DATE) ${dtAliasClause}`);
          } else {
            sqlFields.push(applySqlAlias(`Base.${attr.logicalName}`, effectiveName, attr.logicalName, isPrimaryKey, this.useDisplayNameAliasesInSql));
          }
          processedColumns.add(attr.logicalName.toLowerCase());
        }
      }
    }

    const selectList = sqlFields.join(', ');

    // Build WHERE clause
    let whereClause = '';
    if (table.view?.fetchXml?.trim()) {
      const utcOffset = Math.trunc(dateTableConfig?.utcOffsetHours ?? -6);
      const stripUserCtx = shouldStripUserContext(
        this.storageMode, table.role, table.logicalName, this.tableStorageModeOverridesMap,
      );
      const converter = new FetchXmlToSqlConverter(utcOffset, this.isFabricLink, stripUserCtx);
      const conversionResult = converter.convertToWhereClause(table.view.fetchXml, 'Base');

      if (conversionResult.sqlWhereClause?.trim()) {
        whereClause = ` WHERE ${conversionResult.sqlWhereClause}`;
      } else if (table.hasStateCode) {
        whereClause = ' WHERE Base.statecode=0';
      }
    } else if (table.hasStateCode) {
      whereClause = ' WHERE Base.statecode=0';
    }

    const joinSection = joinClauses.length > 0 ? ' ' + joinClauses.join(' ') : '';

    return normalizeQuery(`SELECT ${selectList} FROM ${fromTable} AS Base${joinSection}${whereClause}`);
  }

  // #endregion
}

// #endregion
