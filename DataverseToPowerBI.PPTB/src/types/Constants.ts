/**
 * Constants.ts
 * 
 * Shared constants used throughout the application.
 */

// ============================================================================
// Connection Modes
// ============================================================================

/**
 * Connection modes for Power BI semantic models.
 * 
 * - DataverseTDS: Uses Dataverse TDS endpoint with CommonDataService.Database connector
 * - FabricLink: Uses Fabric Lakehouse SQL endpoint with Sql.Database connector
 */
export enum ConnectionMode {
  DataverseTDS = 'DataverseTDS',
  FabricLink = 'FabricLink',
}

// ============================================================================
// Storage Modes
// ============================================================================

/**
 * Storage modes for Power BI tables.
 * 
 * - DirectQuery: Data is queried from source at runtime
 * - Import: Data is cached in the Power BI model
 * - Dual: Supports both DirectQuery and Import
 */
export enum StorageMode {
  DirectQuery = 'DirectQuery',
  Import = 'Import',
  Dual = 'Dual',
}

// ============================================================================
// File Extensions
// ============================================================================

/** Power BI Project file extension */
export const PBIP_EXTENSION = '.pbip';

/** TMDL file extension */
export const TMDL_EXTENSION = '.tmdl';

// ============================================================================
// Default Values
// ============================================================================

/** Default start year for calendar table */
export const DEFAULT_START_YEAR = 2020;

/** Default end year for calendar table */
export const DEFAULT_END_YEAR = 2030;

/** Default UTC offset hours */
export const DEFAULT_UTC_OFFSET = 0;

/** Default connection mode */
export const DEFAULT_CONNECTION_MODE = ConnectionMode.DataverseTDS;

/** Default storage mode */
export const DEFAULT_STORAGE_MODE = StorageMode.DirectQuery;

// ============================================================================
// TMDL Template Strings
// ============================================================================

/** TMDL indentation (tabs) */
export const TMDL_INDENT = '\t';

/** TMDL line ending (CRLF for Windows compatibility) */
export const TMDL_LINE_ENDING = '\r\n';

// ============================================================================
// Validation Constants
// ============================================================================

/** Maximum length for table names */
export const MAX_TABLE_NAME_LENGTH = 128;

/** Maximum length for attribute names */
export const MAX_ATTRIBUTE_NAME_LENGTH = 128;

/** Maximum length for display names */
export const MAX_DISPLAY_NAME_LENGTH = 256;

// ============================================================================
// Error Messages
// ============================================================================

export const ERROR_MESSAGES = {
  NO_CONNECTION: 'No Dataverse connection available. Please create a connection in PowerPlatformToolBox.',
  NO_SOLUTION_SELECTED: 'Please select a solution first.',
  NO_TABLES_SELECTED: 'Please select at least one table.',
  NO_FACT_TABLE: 'Please select a fact table for the star schema.',
  DUPLICATE_TABLE_NAME: 'A table with this name already exists.',
  INVALID_RELATIONSHIP: 'Invalid relationship configuration.',
  FETCH_FAILED: 'Failed to fetch data from Dataverse.',
  BUILD_FAILED: 'Failed to generate semantic model.',
  SAVE_FAILED: 'Failed to save files.',
} as const;

// ============================================================================
// Success Messages
// ============================================================================

export const SUCCESS_MESSAGES = {
  MODEL_SAVED: 'Semantic model saved successfully.',
  CONFIGURATION_SAVED: 'Configuration saved successfully.',
  METADATA_CACHED: 'Metadata cached successfully.',
} as const;
