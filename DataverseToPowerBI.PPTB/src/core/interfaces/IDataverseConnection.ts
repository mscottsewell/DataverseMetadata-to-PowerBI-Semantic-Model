/**
 * IDataverseConnection.ts
 * 
 * TypeScript port of DataverseToPowerBI.Core/Interfaces/IDataverseConnection.cs
 * 
 * Defines the contract for connecting to Microsoft Dataverse.
 * Provides an abstraction layer for accessing Dataverse metadata.
 * 
 * The concrete implementation (DataverseAdapter) wraps window.dataverseAPI
 * from PowerPlatformToolBox.
 */

import {
  DataverseSolution,
  TableInfo,
  TableMetadata,
  AttributeMetadata,
  FormMetadata,
  ViewMetadata,
} from '../../types/DataModels';

/**
 * Abstraction for connecting to Microsoft Dataverse.
 * 
 * This interface provides methods to retrieve Dataverse metadata including:
 * - Solutions and their components
 * - Table (entity) definitions and attributes
 * - Forms and Views with their XML definitions
 * 
 * The concrete implementation (DataverseAdapter) wraps window.dataverseAPI
 * from PowerPlatformToolBox. Authentication is handled by PPTB.
 */
export interface IDataverseConnection {
  /**
   * Authenticates to the Dataverse environment.
   * 
   * For PPTB implementations, authentication is handled by the framework,
   * so this method may return immediately with a placeholder value.
   * 
   * @param clearCredentials When true, forces re-authentication by clearing cached tokens
   * @returns The access token string on successful authentication
   */
  authenticateAsync(clearCredentials?: boolean): Promise<string>;

  /**
   * Retrieves all visible solutions from the Dataverse environment.
   * 
   * Only visible solutions are returned (isvisible eq true).
   * Results are ordered by friendly name for display purposes.
   * 
   * @returns A list of solution metadata objects
   */
  getSolutionsAsync(): Promise<DataverseSolution[]>;

  /**
   * Retrieves all tables (entities) that belong to a specific solution.
   * 
   * Filters out Activity and Intersect entities as they are typically
   * not suitable for Power BI semantic models.
   * 
   * @param solutionId The GUID of the solution to retrieve tables from
   * @returns A list of table metadata objects
   */
  getSolutionTablesAsync(solutionId: string): Promise<TableInfo[]>;

  /**
   * Retrieves detailed metadata for a specific table.
   * 
   * @param logicalName The logical name of the table (e.g., "account", "contact")
   * @returns Table metadata including display name, schema name, and primary attributes
   */
  getTableMetadataAsync(logicalName: string): Promise<TableMetadata>;

  /**
   * Retrieves all attributes (columns) for a specific table.
   * 
   * @param tableName The logical name of the table to retrieve attributes from
   * @returns A list of attribute metadata objects
   */
  getAttributesAsync(tableName: string): Promise<AttributeMetadata[]>;

  /**
   * Retrieves all Main forms for a specific table.
   * 
   * @param entityLogicalName The logical name of the table to retrieve forms from
   * @param includeXml When true, includes the FormXML in the results
   * @returns A list of form metadata objects
   */
  getFormsAsync(entityLogicalName: string, includeXml?: boolean): Promise<FormMetadata[]>;

  /**
   * Retrieves the FormXML for a specific form by its ID.
   * 
   * FormXML contains the layout and field definitions for the form.
   * This is parsed to extract the list of fields displayed on the form.
   * 
   * @param formId The GUID of the form to retrieve
   * @returns The FormXML string, or null if the form is not found
   */
  getFormXmlAsync(formId: string): Promise<string | null>;

  /**
   * Retrieves all views (saved queries) for a specific table.
   * 
   * @param entityLogicalName The logical name of the table to retrieve views from
   * @param includeFetchXml When true, includes the FetchXML query definition in the results
   * @returns A list of view metadata objects
   */
  getViewsAsync(entityLogicalName: string, includeFetchXml?: boolean): Promise<ViewMetadata[]>;

  /**
   * Retrieves the FetchXML query for a specific view.
   * 
   * FetchXML is a proprietary query language used by Dataverse.
   * This project converts FetchXML to SQL-like expressions for Power BI.
   * 
   * @param viewId The GUID of the view to retrieve
   * @returns The FetchXML query string, or null if the view is not found
   */
  getViewFetchXmlAsync(viewId: string): Promise<string | null>;

  /**
   * Gets the URL of the connected Dataverse environment.
   * 
   * Used for display purposes, logging, and cache validation.
   * 
   * @returns The base URL of the Dataverse environment (e.g., "https://myorg.crm.dynamics.com")
   */
  getEnvironmentUrl(): string;

  /**
   * Gets a value indicating whether the connection is ready for use.
   * 
   * @returns true if authenticated and ready to make API calls; otherwise, false
   */
  readonly isConnected: boolean;
}
