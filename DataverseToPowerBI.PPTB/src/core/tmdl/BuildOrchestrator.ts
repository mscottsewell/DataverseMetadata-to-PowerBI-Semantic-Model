/**
 * BuildOrchestrator.ts - TMDL Build Orchestration
 *
 * PURPOSE:
 * Orchestrates TMDL generation for the PPTB web app. Unlike the C# version
 * (which writes files directly), this generates a Map of file paths to content
 * strings that can be written via FileSystemAdapter.writeFilesToFolder().
 *
 * SUPPORTED FEATURES:
 * - Fresh build: generates complete PBIP project from scratch
 * - Change analysis: compares config against existing files
 * - Incremental build: preserves user customizations (measures, relationships, lineageTags)
 * - Dual connection modes: DataverseTDS and FabricLink
 */

import {
  ExportTable,
  ExportRelationship,
  AttributeDisplayInfo,
  DateTableConfig,
} from '../../types/DataModels';
import { SemanticModelBuilder } from './SemanticModelBuilder';
import {
  SemanticModelChange,
  ChangeType,
  ImpactLevel,
  parseExistingColumns,
  generateExpectedColumns,
  compareColumnDefinitions,
  extractMQuery,
  normalizeQuery,
  compareQueries,
  parseExistingRelationships,
  generateExpectedRelationships,
} from './ChangeDetector';
import {
  parseExistingLineageTags,
  parseExistingColumnMetadata,
  parseExistingRelationshipGuids,
  parseExistingRelationshipBlocks,
  extractUserRelationships,
  buildToolRelationshipKeys,
  extractUserMeasuresSection,
  insertUserMeasures,
} from './TmdlPreservation';
import {
  extractEnvironmentName,
  sanitizeFileName,
  normalizeStorageMode,
  normalizeTmdlLineEndings,
} from './TmdlHelpers';
import { logger } from '../../utils/Logger';

// #region Types

/** Result of a TMDL build operation */
export interface BuildResult {
  /** Map of relative file paths to content strings */
  files: Map<string, string>;
  /** Root folder path for the PBIP project */
  rootFolder: string;
  /** Project name */
  projectName: string;
  /** Status messages generated during build */
  statusMessages: string[];
  /** Whether this was an incremental build */
  isIncremental: boolean;
}

/** Common parameters for build operations */
export interface BuildParams {
  semanticModelName: string;
  dataverseUrl: string;
  tables: ExportTable[];
  relationships: ExportRelationship[];
  attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>;
  dateTableConfig?: DateTableConfig;
  dateTableTemplate?: string;
}

// #endregion

// #region Constants

const LOG_CATEGORY = 'BuildOrchestrator';

/** Generated table names that are not user-selectable */
const GENERATED_TABLES = new Set<string>(['date', 'dateautotemplate', 'dataverseurl']);

// #endregion

// #region BuildOrchestrator Class

/**
 * Orchestrates TMDL generation, producing a Map of file paths to content strings.
 * The UI layer handles file I/O via PPTB APIs.
 */
export class BuildOrchestrator {
  private readonly builder: SemanticModelBuilder;
  private readonly statusMessages: string[] = [];
  private readonly statusCallback?: (message: string) => void;
  private readonly connectionType: string;
  private readonly fabricLinkEndpoint?: string;
  private readonly fabricLinkDatabase?: string;
  private readonly storageMode: string;

  constructor(config: {
    connectionType?: string;
    fabricLinkEndpoint?: string;
    fabricLinkDatabase?: string;
    languageCode?: number;
    useDisplayNameAliasesInSql?: boolean;
    storageMode?: string;
    statusCallback?: (message: string) => void;
    tableStorageModeOverrides?: Record<string, string>;
  }) {
    this.connectionType = config.connectionType ?? 'DataverseTDS';
    this.fabricLinkEndpoint = config.fabricLinkEndpoint;
    this.fabricLinkDatabase = config.fabricLinkDatabase;
    this.storageMode = config.storageMode ?? 'DirectQuery';
    this.statusCallback = config.statusCallback;

    this.builder = new SemanticModelBuilder({
      connectionType: config.connectionType,
      fabricLinkEndpoint: config.fabricLinkEndpoint,
      fabricLinkDatabase: config.fabricLinkDatabase,
      languageCode: config.languageCode,
      useDisplayNameAliasesInSql: config.useDisplayNameAliasesInSql,
      storageMode: config.storageMode,
      statusCallback: (msg) => this.setStatus(msg),
    });

    if (config.tableStorageModeOverrides) {
      this.builder.setTableStorageModeOverrides(config.tableStorageModeOverrides);
    }
  }

  // #region Private Helpers

  private get isFabricLink(): boolean {
    return this.connectionType === 'FabricLink';
  }

  private setStatus(message: string): void {
    this.statusMessages.push(message);
    this.statusCallback?.(message);
    logger.debug(LOG_CATEGORY, message);
  }

  /**
   * Builds relationship columns lookup: table logical name → set of required lookup column names.
   */
  private buildRelationshipColumnsPerTable(
    relationships: ExportRelationship[],
  ): Record<string, Set<string>> {
    const result: Record<string, Set<string>> = {};
    for (const rel of relationships) {
      const key = rel.sourceTable.toLowerCase();
      if (!result[key]) {
        result[key] = new Set<string>();
      }
      result[key].add(rel.sourceAttribute);
    }
    return result;
  }

  /**
   * Normalizes the Dataverse URL for use in TMDL (strips https:// prefix).
   */
  private normalizeDataverseUrl(dataverseUrl: string): string {
    let url = dataverseUrl;
    if (url.toLowerCase().startsWith('https://')) {
      url = url.substring(8);
    }
    return url;
  }

  /**
   * Gets the definition base path for TMDL files.
   */
  private getDefinitionBase(projectName: string): string {
    return `${projectName}.SemanticModel/definition`;
  }

  /**
   * Adds a file to the result map with normalized line endings.
   */
  private addFile(files: Map<string, string>, relativePath: string, content: string): void {
    files.set(relativePath, normalizeTmdlLineEndings(content));
  }

  /**
   * Gets existing file content from a map by finding a matching table file name.
   */
  private findExistingTableFile(
    existingFiles: Map<string, string>,
    definitionBase: string,
    tableName: string,
  ): string | null {
    const expectedPath = `${definitionBase}/tables/${tableName}.tmdl`;
    for (const [path, content] of existingFiles) {
      if (path.toLowerCase() === expectedPath.toLowerCase()) {
        return content;
      }
    }
    return null;
  }

  /**
   * Finds an existing table file by its `/// Source:` comment (for rename detection).
   */
  private findExistingTableBySource(
    existingFiles: Map<string, string>,
    definitionBase: string,
    logicalName: string,
  ): { path: string; content: string } | null {
    const tablesPrefix = `${definitionBase}/tables/`.toLowerCase();
    for (const [path, content] of existingFiles) {
      if (!path.toLowerCase().startsWith(tablesPrefix)) continue;
      if (!path.toLowerCase().endsWith('.tmdl')) continue;

      const firstLines = content.split(/\r?\n/).slice(0, 3);
      const sourceComment = firstLines.find(l => l.startsWith('/// Source:'));
      if (sourceComment) {
        const source = sourceComment.substring('/// Source:'.length).trim();
        if (source.toLowerCase() === logicalName.toLowerCase()) {
          return { path, content };
        }
      }
    }
    return null;
  }

  /**
   * Detects the storage mode from existing TMDL table content.
   */
  private detectExistingStorageMode(existingFiles: Map<string, string>, definitionBase: string): string | null {
    const tablesPrefix = `${definitionBase}/tables/`.toLowerCase();
    for (const [path, content] of existingFiles) {
      if (!path.toLowerCase().startsWith(tablesPrefix)) continue;
      if (!path.toLowerCase().endsWith('.tmdl')) continue;
      // Skip generated tables
      const fileName = path.substring(path.lastIndexOf('/') + 1).replace('.tmdl', '').toLowerCase();
      if (GENERATED_TABLES.has(fileName)) continue;

      const modeMatch = content.match(/^\t\tmode:\s*(\w+)/m);
      if (modeMatch) {
        const mode = modeMatch[1];
        if (mode === 'directQuery') return 'DirectQuery';
        if (mode === 'import') return 'Import';
        if (mode === 'dual') return 'Dual';
        return mode;
      }
    }
    return null;
  }

  /**
   * Checks if an existing date table file is present.
   */
  private findExistingDateTable(existingFiles: Map<string, string>, definitionBase: string): boolean {
    const datePath = `${definitionBase}/tables/Date.tmdl`.toLowerCase();
    const dateAutoPath = `${definitionBase}/tables/DateAutoTemplate.tmdl`.toLowerCase();
    for (const path of existingFiles.keys()) {
      const lower = path.toLowerCase();
      if (lower === datePath || lower === dateAutoPath) return true;
    }
    return false;
  }

  /**
   * Extracts the DataverseURL parameter value from existing TMDL content.
   */
  private extractDataverseUrlFromFiles(existingFiles: Map<string, string>, definitionBase: string): string | null {
    const dvUrlPath = `${definitionBase}/tables/DataverseURL.tmdl`.toLowerCase();
    for (const [path, content] of existingFiles) {
      if (path.toLowerCase() === dvUrlPath) {
        const match = content.match(/source\s*=\s*"([^"]+)"/);
        return match ? match[1] : null;
      }
    }
    return null;
  }

  /**
   * Extracts a FabricLink expression value from existing expressions.tmdl.
   */
  private extractFabricExpression(
    existingFiles: Map<string, string>,
    definitionBase: string,
    expressionName: string,
  ): string | null {
    const exprPath = `${definitionBase}/expressions.tmdl`.toLowerCase();
    for (const [path, content] of existingFiles) {
      if (path.toLowerCase() === exprPath) {
        const pattern = new RegExp(`expression\\s+${expressionName}\\s*=\\s*"([^"]+)"`, 'i');
        const match = content.match(pattern);
        return match ? match[1] : null;
      }
    }
    return null;
  }

  // #endregion

  // #region build (Fresh Build)

  /**
   * Performs a fresh build of the semantic model.
   * Generates a complete set of TMDL files from scratch.
   */
  build(params: BuildParams): BuildResult {
    this.statusMessages.length = 0;
    this.setStatus('Starting semantic model build...');

    try {
      const {
        semanticModelName,
        dataverseUrl,
        tables,
        relationships,
        attributeDisplayInfo,
        dateTableConfig,
        dateTableTemplate,
      } = params;

      const environmentName = extractEnvironmentName(dataverseUrl);
      const rootFolder = `${environmentName}/${semanticModelName}`;
      const projectName = semanticModelName;
      const definitionBase = this.getDefinitionBase(projectName);
      const files = new Map<string, string>();

      // Build relationship columns lookup
      const relColsPerTable = this.buildRelationshipColumnsPerTable(relationships);

      // Generate table TMDL files
      this.setStatus(`Building ${tables.length} tables...`);
      for (const table of tables) {
        this.setStatus(`Building table: ${table.displayName ?? table.logicalName}...`);
        const lookupKey = table.logicalName.toLowerCase();
        const requiredLookupColumns = relColsPerTable[lookupKey] ?? new Set<string>();
        const tableTmdl = this.builder.generateTableTmdl(
          table, attributeDisplayInfo, requiredLookupColumns, dateTableConfig,
        );
        const tableFileName = sanitizeFileName(table.displayName ?? table.schemaName ?? table.logicalName);
        this.addFile(files, `${definitionBase}/tables/${tableFileName}.tmdl`, tableTmdl);
      }

      // DataverseURL table (always generated)
      this.setStatus('Building DataverseURL table...');
      const normalizedUrl = this.normalizeDataverseUrl(dataverseUrl);
      const dvUrlTmdl = this.builder.generateDataverseUrlTableTmdl(normalizedUrl);
      this.addFile(files, `${definitionBase}/tables/DataverseURL.tmdl`, dvUrlTmdl);

      // FabricLink expressions
      if (this.isFabricLink && this.fabricLinkEndpoint && this.fabricLinkDatabase) {
        this.setStatus('Building FabricLink expressions...');
        const exprTmdl = this.builder.generateFabricLinkExpressions(
          this.fabricLinkEndpoint, this.fabricLinkDatabase,
        );
        this.addFile(files, `${definitionBase}/expressions.tmdl`, exprTmdl);
      }

      // Date table
      if (dateTableConfig && dateTableTemplate) {
        this.setStatus('Building Date table...');
        const dateTmdl = this.builder.generateDateTableTmdl(dateTableConfig, dateTableTemplate);
        this.addFile(files, `${definitionBase}/tables/Date.tmdl`, dateTmdl);
      }

      // Relationships
      const hasDateTable = dateTableConfig != null && dateTableTemplate != null;
      if (relationships.length > 0 || hasDateTable) {
        this.setStatus(`Building ${relationships.length} relationships...`);
        const relTmdl = this.builder.generateRelationshipsTmdl(
          tables, relationships, attributeDisplayInfo, dateTableConfig,
        );
        this.addFile(files, `${definitionBase}/relationships.tmdl`, relTmdl);
      }

      // Model.tmdl
      this.setStatus('Building model configuration...');
      const modelTmdl = this.builder.generateModelTmdl(tables, this.isFabricLink, hasDateTable);
      this.addFile(files, `${definitionBase}/model.tmdl`, modelTmdl);

      this.setStatus('Semantic model build complete!');

      return {
        files,
        rootFolder,
        projectName,
        statusMessages: [...this.statusMessages],
        isIncremental: false,
      };
    } catch (ex) {
      const msg = ex instanceof Error ? ex.message : String(ex);
      this.setStatus(`Error during build: ${msg}`);
      logger.error(LOG_CATEGORY, `Build failed: ${msg}`);
      throw ex;
    }
  }

  // #endregion

  // #region analyzeChanges

  /**
   * Analyzes changes between current configuration and existing PBIP files.
   * Returns a list of semantic model changes for preview.
   */
  analyzeChanges(params: BuildParams, existingFiles: Map<string, string>): SemanticModelChange[] {
    this.statusMessages.length = 0;
    this.setStatus('Analyzing changes...');

    const {
      semanticModelName,
      dataverseUrl,
      tables,
      relationships,
      attributeDisplayInfo,
      dateTableConfig,
    } = params;

    const projectName = semanticModelName;
    const definitionBase = this.getDefinitionBase(projectName);
    const changes: SemanticModelChange[] = [];

    const hasExistingFiles = existingFiles.size > 0;
    const normalizedUrl = this.normalizeDataverseUrl(dataverseUrl);

    // Validate structural integrity for existing projects
    let requiresFullRebuild = false;
    if (hasExistingFiles) {
      const missingElements = this.validateModelIntegrity(existingFiles, projectName);
      if (missingElements.length > 0) {
        requiresFullRebuild = true;
        changes.push({
          changeType: ChangeType.Warning,
          objectType: 'Integrity',
          objectName: 'Model Structure',
          impact: ImpactLevel.Destructive,
          description: `Incomplete PBIP structure detected — ${missingElements.length} missing element(s). A full rebuild is recommended.`,
          detailText: `Missing elements:\n${missingElements.map(m => `  • ${m}`).join('\n')}`,
          parentKey: '',
        });

        for (const missing of missingElements) {
          changes.push({
            changeType: ChangeType.Warning,
            objectType: 'Missing',
            objectName: missing,
            impact: ImpactLevel.Destructive,
            description: 'Required file or folder is missing',
            detailText: '',
            parentKey: '',
          });
        }
      }
    }

    if (!hasExistingFiles || requiresFullRebuild) {
      // New project or damaged structure
      changes.push({
        changeType: ChangeType.New,
        objectType: 'Project',
        objectName: projectName,
        impact: requiresFullRebuild ? ImpactLevel.Destructive : ImpactLevel.Additive,
        description: requiresFullRebuild
          ? 'Full rebuild of Power BI project (missing structural files)'
          : `Create new Power BI project from template (storage: ${this.storageMode})`,
        detailText: requiresFullRebuild
          ? 'The existing PBIP structure is incomplete and will be fully regenerated.\nAll files will be overwritten.'
          : `Storage mode: ${this.storageMode}\nConnection type: ${this.connectionType}`,
        parentKey: '',
      });

      for (const table of tables) {
        const colNames = table.attributes?.map(a => a.displayName ?? a.logicalName).slice(0, 10) ?? [];
        let colPreview = colNames.join('\n  ');
        if ((table.attributes?.length ?? 0) > 10) {
          colPreview += `\n  ... and ${table.attributes!.length - 10} more`;
        }
        changes.push({
          changeType: ChangeType.New,
          objectType: 'Table',
          objectName: table.displayName ?? table.logicalName,
          impact: ImpactLevel.Additive,
          description: `Create table with ${table.attributes?.length ?? 0} columns`,
          detailText: `Logical name: ${table.logicalName}\nColumns:\n  ${colPreview}`,
          parentKey: '',
        });
      }

      for (const rel of relationships) {
        changes.push({
          changeType: ChangeType.New,
          objectType: 'Relationship',
          objectName: `${rel.sourceTable} → ${rel.targetTable}`,
          impact: ImpactLevel.Additive,
          description: `via ${rel.sourceAttribute}`,
          detailText: `From: ${rel.sourceTable}.${rel.sourceAttribute}\nTo: ${rel.targetTable} (primary key)`,
          parentKey: '',
        });
      }
    } else {
      // Existing project — analyze incremental changes
      this.analyzeIncrementalChanges(
        changes, existingFiles, definitionBase, tables, relationships,
        attributeDisplayInfo, dateTableConfig, normalizedUrl,
      );
    }

    // Summarize
    const updateCount = changes.filter(
      c => c.changeType === ChangeType.New || c.changeType === ChangeType.Update,
    ).length;

    if (updateCount === 0) {
      this.setStatus('No changes detected - model is up to date');
    } else {
      this.setStatus(`Detected ${updateCount} change(s) requiring update`);
    }

    return changes;
  }

  /**
   * Analyzes incremental changes for an existing project.
   */
  private analyzeIncrementalChanges(
    changes: SemanticModelChange[],
    existingFiles: Map<string, string>,
    definitionBase: string,
    tables: ExportTable[],
    relationships: ExportRelationship[],
    attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
    dateTableConfig?: DateTableConfig,
    expectedDataverseUrl?: string,
  ): void {
    // Detect storage mode change
    const existingMode = this.detectExistingStorageMode(existingFiles, definitionBase);
    const normalizedExisting = normalizeStorageMode(existingMode);
    const normalizedCurrent = normalizeStorageMode(this.storageMode);
    if (normalizedExisting != null && normalizedCurrent != null &&
        normalizedExisting.toLowerCase() !== normalizedCurrent.toLowerCase()) {
      changes.push({
        changeType: ChangeType.Warning,
        objectType: 'StorageMode',
        objectName: 'Storage Mode Change',
        impact: ImpactLevel.Moderate,
        description: `Changing from ${existingMode} to ${this.storageMode} — cache.abf will be deleted to prevent stale data`,
        detailText: `Current mode: ${existingMode}\nNew mode: ${this.storageMode}`,
        parentKey: '',
      });
    }

    // Detect connection type change (TDS ↔ FabricLink)
    const exprPath = `${definitionBase}/expressions.tmdl`.toLowerCase();
    const existingIsFabricLink = Array.from(existingFiles.keys()).some(p => p.toLowerCase() === exprPath);
    if (existingIsFabricLink !== this.isFabricLink) {
      const fromType = existingIsFabricLink ? 'FabricLink' : 'TDS (DataverseTDS)';
      const toType = this.isFabricLink ? 'FabricLink' : 'TDS (DataverseTDS)';
      changes.push({
        changeType: ChangeType.Warning,
        objectType: 'ConnectionType',
        objectName: 'Connection Type Change',
        impact: ImpactLevel.Destructive,
        description: `Changing from ${fromType} to ${toType} — all table queries will be restructured. User measures and relationships will be preserved.`,
        detailText: `Current: ${fromType}\nNew: ${toType}\n\nAll partition expressions (table queries) will be regenerated.\nUser measures, descriptions, formatting, and relationships are preserved.`,
        parentKey: '',
      });
    }

    // Build relationship columns lookup
    const relColsPerTable = this.buildRelationshipColumnsPerTable(relationships);

    // Analyze table changes
    const existingTableNames = new Set<string>();
    const tablesPrefix = `${definitionBase}/tables/`.toLowerCase();
    for (const path of existingFiles.keys()) {
      if (path.toLowerCase().startsWith(tablesPrefix) && path.toLowerCase().endsWith('.tmdl')) {
        const fileName = path.substring(path.lastIndexOf('/') + 1).replace('.tmdl', '');
        existingTableNames.add(fileName.toLowerCase());
      }
    }

    const metadataTableNames = new Set<string>(
      tables.map(t => sanitizeFileName(t.displayName ?? t.schemaName ?? t.logicalName).toLowerCase()),
    );

    for (const table of tables) {
      const fileName = sanitizeFileName(table.displayName ?? table.schemaName ?? table.logicalName);
      const parentTableName = table.displayName ?? table.logicalName;

      if (!existingTableNames.has(fileName.toLowerCase())) {
        // New table
        changes.push({
          changeType: ChangeType.New,
          objectType: 'Table',
          objectName: parentTableName,
          impact: ImpactLevel.Additive,
          description: `Create new table with ${table.attributes?.length ?? 0} columns`,
          detailText: `Logical name: ${table.logicalName}\nThis table does not yet exist in the PBIP and will be created.`,
          parentKey: '',
        });
      } else {
        // Table exists — deep comparison
        const lookupKey = table.logicalName.toLowerCase();
        const requiredLookupColumns = relColsPerTable[lookupKey] ?? new Set<string>();
        const existingContent = this.findExistingTableFile(existingFiles, definitionBase, fileName);

        if (existingContent) {
          this.analyzeTableChanges(
            changes, existingContent, table, attributeDisplayInfo,
            requiredLookupColumns, dateTableConfig, parentTableName,
          );
        } else {
          changes.push({
            changeType: ChangeType.Preserve,
            objectType: 'Table',
            objectName: parentTableName,
            impact: ImpactLevel.Safe,
            description: 'No changes detected',
            detailText: '',
            parentKey: '',
          });
        }
      }
    }

    // Warn about orphaned tables
    for (const existingName of existingTableNames) {
      if (!metadataTableNames.has(existingName) && !GENERATED_TABLES.has(existingName)) {
        changes.push({
          changeType: ChangeType.Warning,
          objectType: 'Table',
          objectName: existingName,
          impact: ImpactLevel.Safe,
          description: 'Exists in PBIP but not in Dataverse metadata (will be kept as-is)',
          detailText: 'This table file exists in the PBIP folder but is not in the current\ntable selection. It will be left untouched unless you check\n\'Remove tables no longer in the model\'.',
          parentKey: '',
        });
      }
    }

    // Analyze relationship changes
    this.analyzeRelationshipChanges(changes, existingFiles, definitionBase, tables, relationships, attributeDisplayInfo, dateTableConfig);

    // Check FabricLink expression changes
    if (this.isFabricLink) {
      const currentEndpoint = this.extractFabricExpression(existingFiles, definitionBase, 'FabricSQLEndpoint');
      const currentDatabase = this.extractFabricExpression(existingFiles, definitionBase, 'FabricLakehouse');
      const expectedEndpoint = this.fabricLinkEndpoint ?? '';
      const expectedDatabase = this.fabricLinkDatabase ?? '';

      const endpointChanged = (currentEndpoint ?? '').toLowerCase() !== expectedEndpoint.toLowerCase();
      const databaseChanged = (currentDatabase ?? '').toLowerCase() !== expectedDatabase.toLowerCase();

      if (endpointChanged || databaseChanged) {
        const details: string[] = [];
        if (endpointChanged) details.push(`Endpoint: ${currentEndpoint ?? '(none)'} → ${expectedEndpoint}`);
        if (databaseChanged) details.push(`Database: ${currentDatabase ?? '(none)'} → ${expectedDatabase}`);
        changes.push({
          changeType: ChangeType.Update,
          objectType: 'FabricLink',
          objectName: 'Expressions',
          impact: ImpactLevel.Moderate,
          description: `Update: ${details.join(', ')}`,
          detailText: `FabricLink connection parameters are changing:\n${details.join('\n')}`,
          parentKey: '',
        });
      } else {
        changes.push({
          changeType: ChangeType.Preserve,
          objectType: 'FabricLink',
          objectName: 'Expressions',
          impact: ImpactLevel.Safe,
          description: 'No changes detected',
          detailText: '',
          parentKey: '',
        });
      }
    }

    // Check DataverseURL changes
    const currentUrl = this.extractDataverseUrlFromFiles(existingFiles, definitionBase);

    if (expectedDataverseUrl && currentUrl != null &&
        currentUrl.toLowerCase() !== expectedDataverseUrl.toLowerCase()) {
      changes.push({
        changeType: ChangeType.Update,
        objectType: 'DataverseURL',
        objectName: 'Table',
        impact: ImpactLevel.Moderate,
        description: `Update: ${currentUrl} → ${expectedDataverseUrl}`,
        detailText: `The Dataverse URL parameter will be updated.\nOld: ${currentUrl}\nNew: ${expectedDataverseUrl}`,
        parentKey: '',
      });
    } else {
      changes.push({
        changeType: ChangeType.Preserve,
        objectType: 'DataverseURL',
        objectName: 'Table',
        impact: ImpactLevel.Safe,
        description: 'No changes detected',
        detailText: '',
        parentKey: '',
      });
    }
  }

  /**
   * Analyzes changes to a single table by comparing existing TMDL content.
   */
  private analyzeTableChanges(
    changes: SemanticModelChange[],
    existingContent: string,
    table: ExportTable,
    attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
    requiredLookupColumns: Set<string>,
    dateTableConfig: DateTableConfig | undefined,
    parentTableName: string,
  ): void {
    // Parse existing columns
    const existingColumns = parseExistingColumns(existingContent);

    // Generate expected columns
    const expectedColumns = generateExpectedColumns(
      table, attributeDisplayInfo, requiredLookupColumns,
      existingColumns, this.isFabricLink, true, dateTableConfig,
    );

    // Compare queries
    const existingQuery = extractMQuery(existingContent);
    const expectedQuery = this.builder.generateMQuery(
      table, requiredLookupColumns, dateTableConfig, attributeDisplayInfo,
    );
    const queryChanged = !compareQueries(
      normalizeQuery(existingQuery),
      normalizeQuery(expectedQuery),
    );

    // Compare columns
    const newColumns: string[] = [];
    const modifiedColumns: Record<string, string> = {};
    const removedColumns: string[] = [];

    const existingKeys = new Set(Object.keys(existingColumns));
    const expectedKeys = new Set(Object.keys(expectedColumns));

    for (const key of expectedKeys) {
      if (!existingKeys.has(key)) {
        newColumns.push(expectedColumns[key].displayName ?? key);
      } else {
        const diffs = compareColumnDefinitions(existingColumns[key], expectedColumns[key]);
        if (diffs.length > 0) {
          modifiedColumns[expectedColumns[key].displayName ?? key] = diffs.join(', ');
        }
      }
    }

    for (const key of existingKeys) {
      if (!expectedKeys.has(key)) {
        removedColumns.push(existingColumns[key].displayName ?? key);
      }
    }

    // Check for user measures
    const userMeasuresSection = extractUserMeasuresSection(existingContent, table);
    if (userMeasuresSection) {
      const measureNames = this.extractMeasureNames(userMeasuresSection);
      if (measureNames.length > 0) {
        const preview = measureNames.slice(0, 3).join(', ') + (measureNames.length > 3 ? '...' : '');
        changes.push({
          changeType: ChangeType.Preserve,
          objectType: 'Measures',
          objectName: parentTableName,
          impact: ImpactLevel.Safe,
          description: `Preserve ${measureNames.length} user-created measure(s): ${preview}`,
          detailText: `User measures that will be preserved:\n${measureNames.map(m => `  • ${m}`).join('\n')}`,
          parentKey: '',
        });
      }
    }

    const hasChanges = queryChanged || newColumns.length > 0 ||
      Object.keys(modifiedColumns).length > 0 || removedColumns.length > 0;

    if (hasChanges) {
      const changeDetails: string[] = [];
      if (queryChanged) changeDetails.push('query');
      if (newColumns.length > 0) changeDetails.push(`${newColumns.length} new column(s)`);
      if (Object.keys(modifiedColumns).length > 0) changeDetails.push(`${Object.keys(modifiedColumns).length} modified column(s)`);
      if (removedColumns.length > 0) changeDetails.push(`${removedColumns.length} removed column(s)`);

      const detailLines: string[] = [`Logical name: ${table.logicalName}`];
      if (newColumns.length > 0) detailLines.push(`New columns: ${newColumns.join(', ')}`);
      if (removedColumns.length > 0) detailLines.push(`Removed columns: ${removedColumns.join(', ')}`);
      if (Object.keys(modifiedColumns).length > 0) {
        detailLines.push(`Modified columns:\n${Object.entries(modifiedColumns).map(([k, v]) => `  ${k}: ${v}`).join('\n')}`);
      }

      const tableImpact = removedColumns.length > 0 ? ImpactLevel.Moderate
        : queryChanged ? ImpactLevel.Moderate
        : ImpactLevel.Additive;

      changes.push({
        changeType: ChangeType.Update,
        objectType: 'Table',
        objectName: parentTableName,
        impact: tableImpact,
        description: `Update: ${changeDetails.join(', ')}`,
        detailText: detailLines.join('\n'),
        parentKey: '',
      });

      // Add detailed column changes
      for (const col of newColumns) {
        changes.push({
          changeType: ChangeType.New,
          objectType: 'Column',
          objectName: `${parentTableName}.${col}`,
          impact: ImpactLevel.Additive,
          parentKey: parentTableName,
          description: 'New column',
          detailText: '',
        });
      }

      for (const [colName, detail] of Object.entries(modifiedColumns)) {
        changes.push({
          changeType: ChangeType.Update,
          objectType: 'Column',
          objectName: `${parentTableName}.${colName}`,
          impact: detail.includes('dataType') ? ImpactLevel.Moderate : ImpactLevel.Safe,
          parentKey: parentTableName,
          description: `Changed: ${detail}`,
          detailText: detail.includes('dataType')
            ? 'Data type change — user formatting (formatString/summarizeBy) will be reset.'
            : '',
        });
      }
    } else {
      changes.push({
        changeType: ChangeType.Preserve,
        objectType: 'Table',
        objectName: parentTableName,
        impact: ImpactLevel.Safe,
        description: 'No changes detected',
        detailText: '',
        parentKey: '',
      });
    }
  }

  /**
   * Extracts measure names from a measures TMDL section.
   */
  private extractMeasureNames(measuresSection: string): string[] {
    const names: string[] = [];
    const pattern = /measure\s+(?:'([^']+)'|(\S+))/g;
    let match: RegExpExecArray | null;
    while ((match = pattern.exec(measuresSection)) !== null) {
      names.push(match[1] ?? match[2]);
    }
    return names;
  }

  /**
   * Analyzes relationship changes between existing and expected.
   */
  private analyzeRelationshipChanges(
    changes: SemanticModelChange[],
    existingFiles: Map<string, string>,
    definitionBase: string,
    tables: ExportTable[],
    relationships: ExportRelationship[],
    attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>,
    dateTableConfig?: DateTableConfig,
  ): void {
    const relPath = `${definitionBase}/relationships.tmdl`.toLowerCase();
    let existingRelContent = '';
    for (const [path, content] of existingFiles) {
      if (path.toLowerCase() === relPath) {
        existingRelContent = content;
        break;
      }
    }

    const existingRels = parseExistingRelationships(existingRelContent);
    const expectedRels = generateExpectedRelationships(relationships, tables, attributeDisplayInfo, dateTableConfig);

    const newRels: string[] = [];
    const removedRels: string[] = [];

    for (const rel of expectedRels) {
      if (!existingRels.has(rel)) newRels.push(rel);
    }
    for (const rel of existingRels) {
      if (!expectedRels.has(rel)) removedRels.push(rel);
    }

    if (newRels.length > 0 || removedRels.length > 0) {
      const relDetails: string[] = [];
      if (newRels.length > 0) relDetails.push(`${newRels.length} new`);
      if (removedRels.length > 0) relDetails.push(`${removedRels.length} removed`);

      const detailLines: string[] = [];
      if (newRels.length > 0) detailLines.push(`New:\n${newRels.map(r => `  ${r}`).join('\n')}`);
      if (removedRels.length > 0) detailLines.push(`Removed:\n${removedRels.map(r => `  ${r}`).join('\n')}`);

      changes.push({
        changeType: ChangeType.Update,
        objectType: 'Relationships',
        objectName: 'Relationships',
        impact: removedRels.length > 0 ? ImpactLevel.Moderate : ImpactLevel.Additive,
        description: `Update: ${relDetails.join(', ')}`,
        detailText: detailLines.join('\n'),
        parentKey: '',
      });
    } else {
      changes.push({
        changeType: ChangeType.Preserve,
        objectType: 'Relationships',
        objectName: 'All',
        impact: ImpactLevel.Safe,
        description: 'No changes detected',
        detailText: '',
        parentKey: '',
      });
    }
  }

  // #endregion

  // #region buildIncremental

  /**
   * Performs an incremental build that preserves user customizations.
   * Preserves lineage tags, column metadata, user measures, and user-added relationships.
   */
  buildIncremental(params: BuildParams, existingFiles: Map<string, string>): BuildResult {
    this.statusMessages.length = 0;
    this.setStatus('Performing incremental update...');

    try {
      const {
        semanticModelName,
        dataverseUrl,
        tables,
        relationships,
        attributeDisplayInfo,
        dateTableConfig,
        dateTableTemplate,
      } = params;

      const environmentName = extractEnvironmentName(dataverseUrl);
      const rootFolder = `${environmentName}/${semanticModelName}`;
      const projectName = semanticModelName;
      const definitionBase = this.getDefinitionBase(projectName);
      const files = new Map<string, string>();

      // Update DataverseURL
      this.setStatus('Updating DataverseURL...');
      const normalizedUrl = this.normalizeDataverseUrl(dataverseUrl);
      const existingDvUrlContent = this.findExistingTableFile(existingFiles, definitionBase, 'DataverseURL');
      const existingDvUrlTags = existingDvUrlContent ? parseExistingLineageTags(existingDvUrlContent) : undefined;
      const dvUrlTmdl = this.builder.generateDataverseUrlTableTmdl(normalizedUrl, existingDvUrlTags);
      this.addFile(files, `${definitionBase}/tables/DataverseURL.tmdl`, dvUrlTmdl);

      // Build relationship columns lookup
      const relColsPerTable = this.buildRelationshipColumnsPerTable(relationships);

      // Update tables incrementally
      this.setStatus(`Updating ${tables.length} table(s)...`);

      for (const table of tables) {
        this.setStatus(`Updating table: ${table.displayName ?? table.logicalName}...`);
        const tableFileName = sanitizeFileName(table.displayName ?? table.schemaName ?? table.logicalName);
        const lookupKey = table.logicalName.toLowerCase();
        const requiredLookupColumns = relColsPerTable[lookupKey] ?? new Set<string>();

        // Find existing content (direct match or renamed file via Source comment)
        let existingContent = this.findExistingTableFile(existingFiles, definitionBase, tableFileName);
        let renamedFrom: string | null = null;

        if (!existingContent) {
          const sourceMatch = this.findExistingTableBySource(existingFiles, definitionBase, table.logicalName);
          if (sourceMatch) {
            existingContent = sourceMatch.content;
            renamedFrom = sourceMatch.path;
            const oldName = renamedFrom.substring(renamedFrom.lastIndexOf('/') + 1).replace('.tmdl', '');
            this.setStatus(`Table renamed: '${oldName}' → '${table.displayName ?? table.logicalName}'`);
            logger.debug(LOG_CATEGORY, `Table rename detected: ${oldName} → ${tableFileName} (source: ${table.logicalName})`);
          }
        }

        // Parse existing preservation data
        const existingTags = existingContent ? parseExistingLineageTags(existingContent) : undefined;
        const existingColMeta = existingContent ? parseExistingColumnMetadata(existingContent) : undefined;

        // Extract user measures
        let userMeasuresSection: string | null = null;
        if (existingContent) {
          userMeasuresSection = extractUserMeasuresSection(existingContent, table);
        }

        // Generate new TMDL with preserved metadata
        let tableTmdl = this.builder.generateTableTmdl(
          table, attributeDisplayInfo, requiredLookupColumns,
          dateTableConfig, existingTags, existingColMeta,
        );

        // Insert user measures
        if (userMeasuresSection) {
          tableTmdl = insertUserMeasures(tableTmdl, userMeasuresSection);
        }

        this.addFile(files, `${definitionBase}/tables/${tableFileName}.tmdl`, tableTmdl);
      }

      // Date table
      if (dateTableConfig && dateTableTemplate) {
        const hasExistingDate = this.findExistingDateTable(existingFiles, definitionBase);
        if (hasExistingDate) {
          this.setStatus('Date table already exists - preserving it');
          // Copy existing date table content
          const datePath = `${definitionBase}/tables/Date.tmdl`.toLowerCase();
          for (const [path, content] of existingFiles) {
            if (path.toLowerCase() === datePath) {
              this.addFile(files, `${definitionBase}/tables/Date.tmdl`, content);
              break;
            }
          }
        } else {
          this.setStatus('Building Date table...');
          const dateTmdl = this.builder.generateDateTableTmdl(dateTableConfig, dateTableTemplate);
          this.addFile(files, `${definitionBase}/tables/Date.tmdl`, dateTmdl);
        }
      }

      // FabricLink expressions
      if (this.isFabricLink && this.fabricLinkEndpoint && this.fabricLinkDatabase) {
        this.setStatus('Updating FabricLink expressions...');
        const existingExprContent = this.getExistingFileContent(existingFiles, `${definitionBase}/expressions.tmdl`);
        const existingExprTags = existingExprContent ? parseExistingLineageTags(existingExprContent) : undefined;
        const exprTmdl = this.builder.generateFabricLinkExpressions(
          this.fabricLinkEndpoint, this.fabricLinkDatabase, existingExprTags,
        );
        this.addFile(files, `${definitionBase}/expressions.tmdl`, exprTmdl);
      }

      // Update relationships — preserve GUIDs and user-added relationships
      this.setStatus('Updating relationships...');
      const existingRelContent = this.getExistingFileContent(existingFiles, `${definitionBase}/relationships.tmdl`) ?? '';
      const existingRelGuids = parseExistingRelationshipGuids(existingRelContent);
      const existingRelBlocks = parseExistingRelationshipBlocks(existingRelContent);

      const hasDateTable = this.findExistingDateTable(existingFiles, definitionBase) ||
        (dateTableConfig != null && dateTableTemplate != null);

      if (relationships.length > 0 || hasDateTable) {
        let relTmdl = this.builder.generateRelationshipsTmdl(
          tables, relationships, attributeDisplayInfo, dateTableConfig, existingRelGuids,
        );

        // Preserve user-added relationships
        const toolRelKeys = buildToolRelationshipKeys(tables, relationships, attributeDisplayInfo, dateTableConfig);
        const userRelSection = extractUserRelationships(existingRelBlocks, toolRelKeys);
        if (userRelSection) {
          relTmdl += userRelSection;
          this.setStatus('Preserved user-added relationships');
        }

        this.addFile(files, `${definitionBase}/relationships.tmdl`, relTmdl);
      } else if (existingRelContent) {
        // Check for user-added relationships even when no tool relationships exist
        const toolRelKeys = new Set<string>();
        const userRelSection = extractUserRelationships(existingRelBlocks, toolRelKeys);
        if (userRelSection) {
          this.addFile(files, `${definitionBase}/relationships.tmdl`, userRelSection);
          this.setStatus('Preserved user-added relationships (no tool relationships)');
        }
        // If no user relationships either, omit relationships.tmdl from output
      }

      // Update model.tmdl
      this.setStatus('Updating model metadata...');
      const modelTmdl = this.builder.generateModelTmdl(tables, this.isFabricLink, hasDateTable);
      this.addFile(files, `${definitionBase}/model.tmdl`, modelTmdl);

      this.setStatus('Incremental update complete!');

      return {
        files,
        rootFolder,
        projectName,
        statusMessages: [...this.statusMessages],
        isIncremental: true,
      };
    } catch (ex) {
      const msg = ex instanceof Error ? ex.message : String(ex);
      this.setStatus(`Error during incremental build: ${msg}`);
      logger.error(LOG_CATEGORY, `Incremental build failed: ${msg}`);
      throw ex;
    }
  }

  /**
   * Gets content of an existing file by path (case-insensitive).
   */
  private getExistingFileContent(existingFiles: Map<string, string>, path: string): string | null {
    const lowerPath = path.toLowerCase();
    for (const [p, content] of existingFiles) {
      if (p.toLowerCase() === lowerPath) return content;
    }
    return null;
  }

  // #endregion

  // #region validateModelIntegrity

  /**
   * Validates structural integrity of existing PBIP files.
   * Returns a list of missing critical elements.
   */
  private validateModelIntegrity(existingFiles: Map<string, string>, projectName: string): string[] {
    const missing: string[] = [];
    const definitionBase = this.getDefinitionBase(projectName);
    const lowerPaths = new Set(Array.from(existingFiles.keys()).map(p => p.toLowerCase()));

    // Check for model.tmdl
    if (!lowerPaths.has(`${definitionBase}/model.tmdl`.toLowerCase())) {
      missing.push('definition/model.tmdl');
    }

    // Check for tables folder (at least one .tmdl file under tables/)
    const tablesPrefix = `${definitionBase}/tables/`.toLowerCase();
    const hasTableFiles = Array.from(lowerPaths).some(p => p.startsWith(tablesPrefix) && p.endsWith('.tmdl'));
    if (!hasTableFiles) {
      missing.push('definition/tables/ (no table files found)');
    }

    // Check for DataverseURL table
    if (!lowerPaths.has(`${definitionBase}/tables/dataverseurl.tmdl`)) {
      missing.push('tables/DataverseURL.tmdl (connection parameter)');
    }

    // FabricLink: check for expressions.tmdl
    if (this.isFabricLink) {
      if (!lowerPaths.has(`${definitionBase}/expressions.tmdl`.toLowerCase())) {
        missing.push('definition/expressions.tmdl (FabricLink connection parameters)');
      }
    }

    if (missing.length > 0) {
      logger.debug(LOG_CATEGORY, `Model integrity check for '${projectName}': ${missing.length} missing element(s):`);
      for (const m of missing) {
        logger.debug(LOG_CATEGORY, `  - ${m}`);
      }
    } else {
      logger.debug(LOG_CATEGORY, `Model integrity check for '${projectName}': all critical files present`);
    }

    return missing;
  }

  // #endregion

  // #region setTableStorageModeOverrides

  /**
   * Sets per-table storage mode overrides for DualSelect mode.
   * Delegates to the underlying SemanticModelBuilder.
   */
  setTableStorageModeOverrides(overrides: Record<string, string> | null | undefined): void {
    this.builder.setTableStorageModeOverrides(overrides);
  }

  // #endregion
}

// #endregion
