/**
 * DataModels.ts - Core Data Models
 * 
 * TypeScript port of DataverseToPowerBI.Core/Models/DataModels.cs
 * 
 * This file contains TypeScript interfaces for:
 *   - Configuration settings (persisted to JSON)
 *   - Dataverse metadata (solutions, tables, attributes, forms, views)
 *   - Star-schema relationship configuration
 *   - Date/Calendar table configuration
 *   - Export metadata (used when generating Power BI semantic models)
 *   - Metadata caching (reduces API calls to Dataverse)
 */

// ============================================================================
// Enums
// ============================================================================

/**
 * Defines the role of a table in a star-schema data model.
 * - Fact tables contain measurable data (sales amounts, quantities)
 * - Dimension tables contain descriptive attributes (customer names, product categories)
 */
export enum TableRole {
  Dimension = 'Dimension',
  Fact = 'Fact',
}

// ============================================================================
// Configuration Models
// ============================================================================

/**
 * Root container for all saved configurations.
 * This is the top-level structure persisted to configurations.json.
 */
export interface ConfigurationsFile {
  /** Collection of all saved configurations */
  configurations: ConfigurationEntry[];
  
  /** Name of the configuration that was last used */
  lastUsedConfigurationName?: string;
}

/**
 * A named configuration entry containing all settings for a project.
 */
export interface ConfigurationEntry {
  /** Unique name for this configuration */
  name: string;
  
  /** Timestamp of when this configuration was last used */
  lastUsed: string; // ISO 8601 date string
  
  /** The complete settings for this configuration */
  settings: AppSettings;
}

/**
 * Complete application settings for a configuration.
 */
export interface AppSettings {
  /** Last connected Dataverse environment URL */
  lastEnvironmentUrl?: string;
  
  /** Last selected solution unique name */
  lastSolution?: string;
  
  /** List of selected table logical names */
  selectedTables: string[];
  
  /** Map of table logical name to form ID */
  tableForms: Record<string, string>;
  
  /** Map of table logical name to form display name */
  tableFormNames: Record<string, string>;
  
  /** Map of table logical name to view ID */
  tableViews: Record<string, string>;
  
  /** Map of table logical name to view display name */
  tableViewNames: Record<string, string>;
  
  /** Map of table logical name to list of selected attribute logical names */
  tableAttributes: Record<string, string[]>;
  
  /** Map of table logical name to table display metadata */
  tableDisplayInfo: Record<string, TableDisplayInfo>;
  
  /** Map of table logical name to map of attribute logical name to attribute display metadata */
  attributeDisplayInfo: Record<string, Record<string, AttributeDisplayInfo>>;
  
  /** Output folder path for generated PBIP files */
  outputFolder?: string;
  
  /** Project name (used as semantic model name) */
  projectName?: string;
  
  /** Serialized window geometry for UI state */
  windowGeometry?: string;
  
  /** Whether to auto-load cached metadata on startup */
  autoloadCache: boolean;
  
  /** Whether to show all attributes by default (true) or only selected ones (false) */
  showAllAttributes: boolean;
  
  /** The fact table logical name in the star schema */
  factTable?: string;
  
  /** Map of table logical name to its role (Fact or Dimension) */
  tableRoles: Record<string, TableRole>;
  
  /** List of configured relationships */
  relationships: RelationshipConfig[];
  
  /** Date/calendar table configuration */
  dateTableConfig?: DateTableConfig;
  
  /** Whether to use display names as aliases in SQL queries */
  useDisplayNameAliasesInSql: boolean;
  
  /** Map of table logical name to map of attribute logical name to display name override */
  attributeDisplayNameOverrides: Record<string, Record<string, string>>;
}

// ============================================================================
// Star-Schema Configuration
// ============================================================================

/**
 * Configuration for a single relationship in the star schema.
 */
export interface RelationshipConfig {
  /** Source table logical name (typically the fact table) */
  sourceTable: string;
  
  /** Source attribute logical name (foreign key) */
  sourceAttribute: string;
  
  /** Target table logical name (typically a dimension table) */
  targetTable: string;
  
  /** Display name override for the relationship */
  displayName?: string;
  
  /** Whether this relationship is active in the semantic model */
  isActive: boolean;
  
  /** Whether this is a snowflake relationship (dimension to dimension) */
  isSnowflake: boolean;
  
  /** Whether this is a reverse relationship direction */
  isReverse: boolean;
  
  /** Whether to assume referential integrity (improves query performance) */
  assumeReferentialIntegrity: boolean;
}

// ============================================================================
// Date/Calendar Table Configuration
// ============================================================================

/**
 * Configuration for date/calendar table generation.
 */
export interface DateTableConfig {
  /** Primary date table logical name */
  primaryDateTable: string;
  
  /** Primary date field logical name */
  primaryDateField: string;
  
  /** Timezone identifier (e.g., "Pacific Standard Time") */
  timeZoneId: string;
  
  /** UTC offset in hours for timezone conversion */
  utcOffsetHours: number;
  
  /** Start year for date range */
  startYear: number;
  
  /** End year for date range */
  endYear: number;
  
  /** List of datetime fields to wrap with timezone conversion */
  wrappedFields: DateTimeFieldConfig[];
}

/**
 * Configuration for a single datetime field that needs timezone conversion.
 */
export interface DateTimeFieldConfig {
  /** Table logical name containing the datetime field */
  tableName: string;
  
  /** Datetime field logical name */
  fieldName: string;
  
  /** Whether to convert to date-only (strip time portion) */
  convertToDateOnly: boolean;
}

// ============================================================================
// Metadata Display Models
// ============================================================================

/**
 * Display metadata for a table.
 */
export interface TableDisplayInfo {
  /** Logical name (not serialized to JSON, used internally) */
  logicalName: string;
  
  /** Display name override */
  displayName?: string;
  
  /** Schema name (e.g., "dbo.accountBase") */
  schemaName?: string;
  
  /** Primary ID attribute logical name */
  primaryIdAttribute?: string;
  
  /** Primary name attribute logical name */
  primaryNameAttribute?: string;
}

/**
 * Display metadata for an attribute.
 */
export interface AttributeDisplayInfo {
  /** Logical name (not serialized to JSON, used internally) */
  logicalName: string;
  
  /** Display name override */
  displayName?: string;
  
  /** Schema name */
  schemaName?: string;
  
  /** Attribute type (e.g., "String", "Lookup", "Money") */
  attributeType?: string;
  
  /** Whether the attribute is required */
  isRequired: boolean;
  
  /** For Lookup attributes, list of target table logical names */
  targets?: string[];
  
  /** For virtual attributes (polymorphic lookups), the base attribute name */
  virtualAttributeName?: string;
  
  /** Whether this is a global choice set */
  isGlobal?: boolean;
  
  /** Choice set (OptionSet) name */
  optionSetName?: string;
  
  /** User-specified display name override */
  overrideDisplayName?: string;
}

// ============================================================================
// Metadata Cache Models
// ============================================================================

/**
 * Cache of Dataverse metadata to reduce API calls.
 */
export interface MetadataCache {
  /** Environment URL this cache is for */
  environmentUrl?: string;
  
  /** Solution name this cache is for */
  solutionName?: string;
  
  /** When this cache was last updated */
  cachedDate: string; // ISO 8601 date string
  
  /** List of all solutions */
  solutions: DataverseSolution[];
  
  /** List of tables in the selected solution */
  tables: TableInfo[];
  
  /** Map of table logical name to table info */
  tableData: Record<string, TableInfo>;
  
  /** Map of table logical name to list of forms */
  tableForms: Record<string, FormMetadata[]>;
  
  /** Map of table logical name to list of views */
  tableViews: Record<string, ViewMetadata[]>;
  
  /** Map of table logical name to list of attributes */
  tableAttributes: Record<string, AttributeMetadata[]>;
}

// ============================================================================
// Dataverse Metadata Models
// ============================================================================

/**
 * Metadata for a Dataverse table (entity).
 */
export interface TableInfo {
  /** Logical name (e.g., "account", "contact") */
  logicalName: string;
  
  /** Display name */
  displayName?: string;
  
  /** Schema name */
  schemaName?: string;
  
  /** Object type code (numeric identifier) */
  objectTypeCode: number;
  
  /** Primary ID attribute logical name */
  primaryIdAttribute?: string;
  
  /** Primary name attribute logical name */
  primaryNameAttribute?: string;
  
  /** Metadata ID (GUID) */
  metadataId?: string;
}

/**
 * Metadata for a Dataverse solution.
 */
export interface DataverseSolution {
  /** Solution GUID */
  solutionId: string;
  
  /** Unique name */
  uniqueName: string;
  
  /** Friendly display name */
  friendlyName: string;
  
  /** Version string (e.g., "1.0.0.0") */
  version?: string;
  
  /** Whether this is a managed solution */
  isManaged: boolean;
  
  /** Publisher ID */
  publisherId?: string;
  
  /** Last modified date */
  modifiedOn?: string; // ISO 8601 date string
}

/**
 * Metadata for a table (minimal version).
 */
export interface TableMetadata {
  /** Logical name */
  logicalName: string;
  
  /** Display name */
  displayName?: string;
  
  /** Schema name */
  schemaName?: string;
  
  /** Primary ID attribute */
  primaryIdAttribute?: string;
  
  /** Primary name attribute */
  primaryNameAttribute?: string;
}

/**
 * Metadata for an attribute (column).
 */
export interface AttributeMetadata {
  /** Logical name */
  logicalName: string;
  
  /** Display name */
  displayName?: string;
  
  /** Schema name */
  schemaName?: string;
  
  /** Description */
  description?: string;
  
  /** Attribute type (e.g., "String", "Lookup", "Money") */
  attributeType?: string;
  
  /** Whether this is a custom attribute */
  isCustomAttribute: boolean;
  
  /** Whether the attribute is required */
  isRequired: boolean;
  
  /** For Lookup attributes, list of target table logical names */
  targets?: string[];
  
  /** For virtual attributes (polymorphic lookups), the base attribute name */
  virtualAttributeName?: string;
  
  /** Whether this uses a global choice set */
  isGlobal?: boolean;
  
  /** Choice set name */
  optionSetName?: string;
}

/**
 * Metadata for a form.
 */
export interface FormMetadata {
  /** Form GUID */
  formId: string;
  
  /** Form name */
  name: string;
  
  /** FormXML definition */
  formXml?: string;
  
  /** List of field logical names displayed on the form */
  fields?: string[];
}

/**
 * Metadata for a view (saved query).
 */
export interface ViewMetadata {
  /** View GUID */
  viewId: string;
  
  /** View name */
  name: string;
  
  /** Whether this is the default view */
  isDefault: boolean;
  
  /** FetchXML query definition */
  fetchXml?: string;
  
  /** List of column logical names displayed in the view */
  columns: string[];
}

// ============================================================================
// Export Models (for TMDL generation)
// ============================================================================

/**
 * Complete export metadata for generating a semantic model.
 */
export interface ExportMetadata {
  /** Dataverse environment URL */
  environment: string;
  
  /** Solution unique name */
  solution: string;
  
  /** Project name (semantic model name) */
  projectName: string;
  
  /** Fact table logical name */
  factTable?: string;
  
  /** List of relationships */
  relationships: ExportRelationship[];
  
  /** List of tables */
  tables: ExportTable[];
}

/**
 * Relationship metadata for export.
 */
export interface ExportRelationship {
  /** Source table logical name */
  sourceTable: string;
  
  /** Source attribute logical name */
  sourceAttribute: string;
  
  /** Target table logical name */
  targetTable: string;
  
  /** Display name override */
  displayName?: string;
  
  /** Whether this relationship is active */
  isActive: boolean;
  
  /** Whether this is a snowflake relationship */
  isSnowflake: boolean;
  
  /** Whether to assume referential integrity */
  assumeReferentialIntegrity: boolean;
}

/**
 * Table metadata for export.
 */
export interface ExportTable {
  /** Logical name */
  logicalName: string;
  
  /** Display name */
  displayName?: string;
  
  /** Schema name */
  schemaName?: string;
  
  /** Object type code */
  objectTypeCode: number;
  
  /** Primary ID attribute */
  primaryIdAttribute?: string;
  
  /** Primary name attribute */
  primaryNameAttribute?: string;
  
  /** Table role (Fact or Dimension) */
  role: string;
  
  /** Whether the table has statecode/statuscode fields */
  hasStateCode: boolean;
  
  /** List of forms */
  forms: ExportForm[];
  
  /** Selected view */
  view?: ExportView;
  
  /** List of attributes */
  attributes: AttributeMetadata[];
}

/**
 * Form metadata for export.
 */
export interface ExportForm {
  /** Form GUID */
  formId: string;
  
  /** Form name */
  formName: string;
  
  /** Number of fields on the form */
  fieldCount: number;
}

/**
 * View metadata for export.
 */
export interface ExportView {
  /** View GUID */
  viewId: string;
  
  /** View name */
  viewName: string;
  
  /** FetchXML query definition */
  fetchXml?: string;
}
