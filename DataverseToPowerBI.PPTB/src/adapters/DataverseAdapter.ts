/**
 * DataverseAdapter.ts
 * 
 * Implements IDataverseConnection by wrapping PowerPlatformToolBox's window.dataverseAPI.
 * 
 * This adapter translates the interface methods to PPTB API calls,
 * handling authentication (managed by PPTB), metadata retrieval, and error handling.
 */

import { IDataverseConnection } from '../core/interfaces/IDataverseConnection';
import {
  DataverseSolution,
  TableInfo,
  TableMetadata,
  AttributeMetadata,
  FormMetadata,
  ViewMetadata,
} from '../types/DataModels';

/**
 * Dataverse connection adapter for PowerPlatformToolBox.
 * Wraps window.dataverseAPI to implement IDataverseConnection interface.
 */
export class DataverseAdapter implements IDataverseConnection {
  private connection: any = null;
  
  constructor() {
    // Connection is managed by PPTB framework
    if (typeof window !== 'undefined' && window.toolboxAPI) {
      // Get active connection on initialization
      window.toolboxAPI.connections?.getActiveConnection?.()
        .then((conn: any) => {
          this.connection = conn;
        })
        .catch((err: Error) => {
          console.error('Failed to get active connection:', err);
        });
    }
  }

  /**
   * Get the Dataverse API instance.
   * Throws if PPTB APIs are not available.
   */
  private get api(): any {
    if (typeof window === 'undefined' || !window.dataverseAPI) {
      throw new Error('window.dataverseAPI is not available. This tool must run in PowerPlatformToolBox.');
    }
    return window.dataverseAPI;
  }

  /**
   * Authenticates to Dataverse.
   * For PPTB, authentication is handled by the framework, so this returns immediately.
   * 
   * @param _clearCredentials Unused - authentication is managed by PPTB
   */
  async authenticateAsync(_clearCredentials = false): Promise<string> {
    // Authentication handled by PPTB
    // Return placeholder token
    return 'pptb-managed-auth';
  }

  /**
   * Retrieves all visible solutions from the Dataverse environment.
   */
  async getSolutionsAsync(): Promise<DataverseSolution[]> {
    try {
      // Use FetchXML to query solutions
      const fetchXml = `
        <fetch>
          <entity name="solution">
            <attribute name="solutionid" />
            <attribute name="uniquename" />
            <attribute name="friendlyname" />
            <attribute name="version" />
            <attribute name="ismanaged" />
            <attribute name="publisherid" />
            <attribute name="modifiedon" />
            <filter>
              <condition attribute="isvisible" operator="eq" value="true" />
            </filter>
            <order attribute="friendlyname" />
          </entity>
        </fetch>
      `;

      const result = await this.api.executeFetchXml(fetchXml);
      
      // Transform result to DataverseSolution array
      const solutions: DataverseSolution[] = [];
      
      if (result?.entities) {
        for (const entity of result.entities) {
          solutions.push({
            solutionId: entity.solutionid || '',
            uniqueName: entity.uniquename || '',
            friendlyName: entity.friendlyname || '',
            version: entity.version,
            isManaged: entity.ismanaged === true,
            publisherId: entity.publisherid,
            modifiedOn: entity.modifiedon,
          });
        }
      }

      return solutions;
    } catch (error) {
      console.error('Failed to fetch solutions:', error);
      throw new Error(`Failed to fetch solutions: ${error}`);
    }
  }

  /**
   * Retrieves all tables that belong to a specific solution.
   */
  async getSolutionTablesAsync(solutionId: string): Promise<TableInfo[]> {
    try {
      // Use PPTB metadata API to get all entities
      // Then filter by solution components
      
      // First, get solution components
      const componentsFetchXml = `
        <fetch>
          <entity name="solutioncomponent">
            <attribute name="objectid" />
            <filter>
              <condition attribute="solutionid" operator="eq" value="${solutionId}" />
              <condition attribute="componenttype" operator="eq" value="1" />
            </filter>
          </entity>
        </fetch>
      `;

      const componentsResult = await this.api.executeFetchXml(componentsFetchXml);
      const componentIds = componentsResult?.entities?.map((e: any) => e.objectid) || [];

      if (componentIds.length === 0) {
        return [];
      }

      // Get entity metadata for these components
      const entities = await this.api.retrieveAllEntityMetadata();
      
      const tables: TableInfo[] = [];
      
      for (const entity of entities) {
        // Filter to only include entities in this solution
        if (componentIds.includes(entity.MetadataId)) {
          // Skip Activity and Intersect entities
          if (entity.IsActivity === true || entity.IsIntersect === true) {
            continue;
          }

          tables.push({
            logicalName: entity.LogicalName || '',
            displayName: entity.DisplayName?.UserLocalizedLabel?.Label,
            schemaName: entity.SchemaName,
            objectTypeCode: entity.ObjectTypeCode || 0,
            primaryIdAttribute: entity.PrimaryIdAttribute,
            primaryNameAttribute: entity.PrimaryNameAttribute,
            metadataId: entity.MetadataId,
          });
        }
      }

      return tables.sort((a, b) => 
        (a.displayName || a.logicalName).localeCompare(b.displayName || b.logicalName)
      );
    } catch (error) {
      console.error('Failed to fetch solution tables:', error);
      throw new Error(`Failed to fetch solution tables: ${error}`);
    }
  }

  /**
   * Retrieves detailed metadata for a specific table.
   */
  async getTableMetadataAsync(logicalName: string): Promise<TableMetadata> {
    try {
      const metadata = await this.api.retrieveEntityMetadata(logicalName);
      
      return {
        logicalName: metadata.LogicalName || logicalName,
        displayName: metadata.DisplayName?.UserLocalizedLabel?.Label,
        schemaName: metadata.SchemaName,
        primaryIdAttribute: metadata.PrimaryIdAttribute,
        primaryNameAttribute: metadata.PrimaryNameAttribute,
      };
    } catch (error) {
      console.error(`Failed to fetch metadata for ${logicalName}:`, error);
      throw new Error(`Failed to fetch table metadata: ${error}`);
    }
  }

  /**
   * Retrieves all attributes for a specific table.
   */
  async getAttributesAsync(tableName: string): Promise<AttributeMetadata[]> {
    try {
      const metadata = await this.api.retrieveEntityMetadata(tableName);
      const attributes: AttributeMetadata[] = [];

      if (metadata.Attributes) {
        for (const attr of metadata.Attributes) {
          // Determine targets for Lookup attributes
          let targets: string[] | undefined;
          if (attr.AttributeType === 'Lookup' && attr.Targets) {
            targets = Array.isArray(attr.Targets) ? attr.Targets : [attr.Targets];
          }

          attributes.push({
            logicalName: attr.LogicalName || '',
            displayName: attr.DisplayName?.UserLocalizedLabel?.Label,
            schemaName: attr.SchemaName,
            description: attr.Description?.UserLocalizedLabel?.Label,
            attributeType: attr.AttributeType,
            isCustomAttribute: attr.IsCustomAttribute === true,
            isRequired: attr.RequiredLevel?.Value === 'ApplicationRequired',
            targets,
            virtualAttributeName: attr.AttributeOf,
            isGlobal: attr.OptionSet?.IsGlobal,
            optionSetName: attr.OptionSet?.Name,
          });
        }
      }

      return attributes.sort((a, b) => 
        (a.displayName || a.logicalName).localeCompare(b.displayName || b.logicalName)
      );
    } catch (error) {
      console.error(`Failed to fetch attributes for ${tableName}:`, error);
      throw new Error(`Failed to fetch attributes: ${error}`);
    }
  }

  /**
   * Retrieves all Main forms for a specific table.
   */
  async getFormsAsync(entityLogicalName: string, includeXml = false): Promise<FormMetadata[]> {
    try {
      const fetchXml = `
        <fetch>
          <entity name="systemform">
            <attribute name="formid" />
            <attribute name="name" />
            ${includeXml ? '<attribute name="formxml" />' : ''}
            <filter>
              <condition attribute="objecttypecode" operator="eq" value="${entityLogicalName}" />
              <condition attribute="type" operator="eq" value="2" />
              <condition attribute="iscustomizable" operator="eq" value="true" />
            </filter>
            <order attribute="name" />
          </entity>
        </fetch>
      `;

      const result = await this.api.executeFetchXml(fetchXml);
      const forms: FormMetadata[] = [];

      if (result?.entities) {
        for (const entity of result.entities) {
          const form: FormMetadata = {
            formId: entity.formid || '',
            name: entity.name || '',
            formXml: includeXml ? entity.formxml : undefined,
          };

          // Parse FormXML to extract field names if available
          if (form.formXml) {
            form.fields = this.parseFormXmlFields(form.formXml);
          }

          forms.push(form);
        }
      }

      return forms;
    } catch (error) {
      console.error(`Failed to fetch forms for ${entityLogicalName}:`, error);
      throw new Error(`Failed to fetch forms: ${error}`);
    }
  }

  /**
   * Retrieves the FormXML for a specific form.
   */
  async getFormXmlAsync(formId: string): Promise<string | null> {
    try {
      const fetchXml = `
        <fetch top="1">
          <entity name="systemform">
            <attribute name="formxml" />
            <filter>
              <condition attribute="formid" operator="eq" value="${formId}" />
            </filter>
          </entity>
        </fetch>
      `;

      const result = await this.api.executeFetchXml(fetchXml);
      
      if (result?.entities && result.entities.length > 0) {
        return result.entities[0].formxml || null;
      }

      return null;
    } catch (error) {
      console.error(`Failed to fetch FormXML for form ${formId}:`, error);
      return null;
    }
  }

  /**
   * Retrieves all views for a specific table.
   */
  async getViewsAsync(entityLogicalName: string, includeFetchXml = false): Promise<ViewMetadata[]> {
    try {
      const fetchXml = `
        <fetch>
          <entity name="savedquery">
            <attribute name="savedqueryid" />
            <attribute name="name" />
            <attribute name="isdefault" />
            <attribute name="layoutxml" />
            ${includeFetchXml ? '<attribute name="fetchxml" />' : ''}
            <filter>
              <condition attribute="returnedtypecode" operator="eq" value="${entityLogicalName}" />
              <condition attribute="querytype" operator="eq" value="0" />
            </filter>
            <order attribute="name" />
          </entity>
        </fetch>
      `;

      const result = await this.api.executeFetchXml(fetchXml);
      const views: ViewMetadata[] = [];

      if (result?.entities) {
        for (const entity of result.entities) {
          views.push({
            viewId: entity.savedqueryid || '',
            name: entity.name || '',
            isDefault: entity.isdefault === true,
            fetchXml: includeFetchXml ? entity.fetchxml : undefined,
            columns: this.parseLayoutXmlColumns(entity.layoutxml || ''),
          });
        }
      }

      return views;
    } catch (error) {
      console.error(`Failed to fetch views for ${entityLogicalName}:`, error);
      throw new Error(`Failed to fetch views: ${error}`);
    }
  }

  /**
   * Retrieves the FetchXML for a specific view.
   */
  async getViewFetchXmlAsync(viewId: string): Promise<string | null> {
    try {
      const fetchXml = `
        <fetch top="1">
          <entity name="savedquery">
            <attribute name="fetchxml" />
            <filter>
              <condition attribute="savedqueryid" operator="eq" value="${viewId}" />
            </filter>
          </entity>
        </fetch>
      `;

      const result = await this.api.executeFetchXml(fetchXml);
      
      if (result?.entities && result.entities.length > 0) {
        return result.entities[0].fetchxml || null;
      }

      return null;
    } catch (error) {
      console.error(`Failed to fetch FetchXML for view ${viewId}:`, error);
      return null;
    }
  }

  /**
   * Gets the URL of the connected Dataverse environment.
   */
  getEnvironmentUrl(): string {
    return this.connection?.url || '';
  }

  /**
   * Gets whether the connection is ready for use.
   */
  get isConnected(): boolean {
    return this.connection !== null && typeof window !== 'undefined' && !!window.dataverseAPI;
  }

  // ============================================================================
  // Private Helper Methods
  // ============================================================================

  /**
   * Parses FormXML to extract field logical names.
   */
  private parseFormXmlFields(formXml: string): string[] {
    try {
      const parser = new DOMParser();
      const xmlDoc = parser.parseFromString(formXml, 'text/xml');
      const fields: string[] = [];

      // Find all <control> elements with datafieldname attribute
      const controls = xmlDoc.querySelectorAll('control[datafieldname]');
      controls.forEach((control) => {
        const fieldName = control.getAttribute('datafieldname');
        if (fieldName && !fields.includes(fieldName)) {
          fields.push(fieldName);
        }
      });

      return fields;
    } catch (error) {
      console.error('Failed to parse FormXML:', error);
      return [];
    }
  }

  /**
   * Parses LayoutXML to extract column names.
   */
  private parseLayoutXmlColumns(layoutXml: string): string[] {
    try {
      const parser = new DOMParser();
      const xmlDoc = parser.parseFromString(layoutXml, 'text/xml');
      const columns: string[] = [];

      // Find all <cell> elements with name attribute
      const cells = xmlDoc.querySelectorAll('cell[name]');
      cells.forEach((cell) => {
        const colName = cell.getAttribute('name');
        if (colName && !columns.includes(colName)) {
          columns.push(colName);
        }
      });

      return columns;
    } catch (error) {
      console.error('Failed to parse LayoutXML:', error);
      return [];
    }
  }
}
