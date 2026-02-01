// =============================================================================
// DataModels.cs - Configurator Data Models
// =============================================================================
// Purpose: Data model classes for the Configurator application.
//
// These models are used by the standalone Configurator WinForms application for:
//   - Persisting user configuration settings
//   - Caching Dataverse metadata for performance
//   - Representing star-schema table roles and relationships
//   - Storing date table configuration for timezone handling
//   - Exporting configuration for TMDL generation
//
// Model Categories:
//   1. Configuration Storage (ConfigurationsFile, ConfigurationEntry, AppSettings)
//   2. Star-Schema Modeling (TableRole, RelationshipConfig)
//   3. Date Table (DateTableConfig, DateTimeFieldConfig)
//   4. Table/Attribute Display (TableDisplayInfo, AttributeDisplayInfo)
//   5. Metadata Cache (MetadataCache, TableInfo, etc.)
//   6. Export Format (ExportMetadata, ExportTable, etc.)
//
// Note: The Core project has its own DataModels.cs with similar but not identical
// models. The Configurator models include additional properties for the WinForms
// UI such as TableForms, TableViews, and connection type settings.
//
// JSON Serialization: Uses Newtonsoft.Json for persistence. Properties marked
// with [JsonIgnore] are transient and not saved to disk.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DataverseToPowerBI.Configurator.Models
{
    #region Configuration Storage Models

    /// <summary>
    /// Root container for all saved configurations.
    /// Persisted to disk as the main settings file.
    /// </summary>
    /// <remarks>
    /// Supports multiple named configurations, allowing users to switch between
    /// different Dataverse environments or project setups. The last used
    /// configuration is remembered for automatic loading on startup.
    /// </remarks>
    public class ConfigurationsFile
    {
        /// <summary>
        /// All saved configurations by name.
        /// </summary>
        public List<ConfigurationEntry> Configurations { get; set; } = new();

        /// <summary>
        /// Name of the configuration to load automatically on startup.
        /// </summary>
        public string? LastUsedConfigurationName { get; set; }
    }

    /// <summary>
    /// A named configuration with metadata and settings.
    /// </summary>
    /// <remarks>
    /// Each configuration captures a complete setup including environment URL,
    /// selected solution, tables, forms, views, and output settings.
    /// </remarks>
    public class ConfigurationEntry
    {
        /// <summary>
        /// User-friendly name for this configuration.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Timestamp of last use, for sorting recent configurations.
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.Now;

        /// <summary>
        /// The complete settings for this configuration.
        /// </summary>
        public AppSettings Settings { get; set; } = new();
    }

    #endregion

    #region Star-Schema Modeling

    /// <summary>
    /// Defines the role of a table in a star-schema Power BI model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In a star schema:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>Fact</term>
    ///     <description>
    ///     The central table containing measurable events (e.g., Orders, Cases).
    ///     Has many-to-one relationships with dimension tables.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Dimension</term>
    ///     <description>
    ///     Lookup tables containing descriptive attributes for slicing data
    ///     (e.g., Account, Contact, Product). One-to-many relationship with fact.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    public enum TableRole
    {
        /// <summary>
        /// Dimension (lookup) table - the "one" side of relationships.
        /// </summary>
        Dimension,

        /// <summary>
        /// Fact table - the "many" side containing measurable events.
        /// </summary>
        Fact
    }

    /// <summary>
    /// Represents a relationship between tables in the star schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Relationships are created from Dataverse lookup fields. The configuration
    /// captures both the physical relationship and options for Power BI.
    /// </para>
    /// <para>
    /// Snowflake relationships (dimension-to-parent-dimension) are supported
    /// for hierarchical lookups like Account -> Parent Account.
    /// </para>
    /// </remarks>
    public class RelationshipConfig
    {
        /// <summary>
        /// Table containing the lookup field (the "many" side).
        /// Either a fact table or a dimension in snowflake relationships.
        /// </summary>
        public string SourceTable { get; set; } = "";

        /// <summary>
        /// The lookup attribute logical name on the source table.
        /// </summary>
        public string SourceAttribute { get; set; } = "";

        /// <summary>
        /// The dimension table being referenced (the "one" side).
        /// </summary>
        public string TargetTable { get; set; } = "";

        /// <summary>
        /// User-friendly display name for the relationship in Power BI.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Whether this is the active relationship for cross-filtering.
        /// Only one relationship between any two tables can be active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// True for dimension-to-parent-dimension relationships (snowflake schema).
        /// Example: Account -> Parent Account.
        /// </summary>
        public bool IsSnowflake { get; set; } = false;

        /// <summary>
        /// True for one-to-many relationships from the fact's perspective.
        /// Used for special relationship directions.
        /// </summary>
        public bool IsReverse { get; set; } = false;

        /// <summary>
        /// When true, enables DirectQuery performance optimizations.
        /// Should only be set when the lookup field is required (not nullable).
        /// </summary>
        public bool AssumeReferentialIntegrity { get; set; } = false;
    }

    #endregion

    #region Date Table Configuration

    /// <summary>
    /// Configuration for the Date/Calendar dimension table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Power BI recommends a dedicated Date table for time intelligence calculations.
    /// This configuration controls:
    /// </para>
    /// <list type="bullet">
    ///   <item>Which date field the calendar joins to</item>
    ///   <item>Timezone adjustment for UTC-stored dates</item>
    ///   <item>Date range for the generated calendar</item>
    ///   <item>Which datetime fields should be timezone-wrapped</item>
    /// </list>
    /// </remarks>
    public class DateTableConfig
    {
        /// <summary>
        /// Logical name of the table containing the primary date field.
        /// </summary>
        public string PrimaryDateTable { get; set; } = "";

        /// <summary>
        /// The date field that the calendar table joins to.
        /// </summary>
        public string PrimaryDateField { get; set; } = "";

        /// <summary>
        /// Windows timezone ID (e.g., "Eastern Standard Time", "Pacific Standard Time").
        /// Used for displaying timezone in the UI.
        /// </summary>
        public string TimeZoneId { get; set; } = "";

        /// <summary>
        /// UTC offset in hours for date conversions.
        /// Negative for Americas (-5 EST, -8 PST), positive for Europe/Asia (+1 CET).
        /// </summary>
        public double UtcOffsetHours { get; set; } = 0;

        /// <summary>
        /// First year to include in the generated calendar.
        /// </summary>
        public int StartYear { get; set; }

        /// <summary>
        /// Last year to include in the generated calendar.
        /// </summary>
        public int EndYear { get; set; }

        /// <summary>
        /// DateTime fields that should have timezone adjustment applied in DAX.
        /// </summary>
        public List<DateTimeFieldConfig> WrappedFields { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a DateTime field requiring timezone adjustment.
    /// </summary>
    /// <remarks>
    /// Dataverse stores all DateTimes in UTC. For accurate reporting, datetime
    /// fields shown to users should be converted to local time. This configuration
    /// tells the TMDL generator to wrap the field in a timezone conversion expression.
    /// </remarks>
    public class DateTimeFieldConfig
    {
        /// <summary>
        /// Logical name of the table containing this field.
        /// </summary>
        public string TableName { get; set; } = "";

        /// <summary>
        /// Logical name of the DateTime field.
        /// </summary>
        public string FieldName { get; set; } = "";

        /// <summary>
        /// When true, the time component is removed after conversion.
        /// Use for "Date Only" display requirements.
        /// </summary>
        public bool ConvertToDateOnly { get; set; } = true;
    }

    #endregion

    #region Application Settings

    /// <summary>
    /// Complete application settings for a configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Captures all user choices including:
    /// </para>
    /// <list type="bullet">
    ///   <item>Dataverse environment and solution</item>
    ///   <item>Selected tables and their forms/views</item>
    ///   <item>Column selections per table</item>
    ///   <item>Connection type (Dataverse TDS or Fabric Link)</item>
    ///   <item>Star-schema configuration (fact table, relationships)</item>
    ///   <item>Date table settings</item>
    ///   <item>Output folder and project name</item>
    /// </list>
    /// </remarks>
    public class AppSettings
    {
        /// <summary>
        /// Dataverse environment URL (e.g., "https://org.crm.dynamics.com").
        /// </summary>
        public string? LastEnvironmentUrl { get; set; }

        /// <summary>
        /// Selected solution unique name.
        /// </summary>
        public string? LastSolution { get; set; }

        /// <summary>
        /// Logical names of tables selected for inclusion.
        /// </summary>
        public List<string> SelectedTables { get; set; } = new();

        /// <summary>
        /// Selected form ID per table. Key: table logical name, Value: form GUID.
        /// </summary>
        public Dictionary<string, string> TableForms { get; set; } = new();

        /// <summary>
        /// Selected form display name per table. For UI display.
        /// </summary>
        public Dictionary<string, string> TableFormNames { get; set; } = new();

        /// <summary>
        /// Selected view ID per table. Key: table logical name, Value: view GUID.
        /// </summary>
        public Dictionary<string, string> TableViews { get; set; } = new();

        /// <summary>
        /// Selected view display name per table. For UI display.
        /// </summary>
        public Dictionary<string, string> TableViewNames { get; set; } = new();

        /// <summary>
        /// Selected attribute logical names per table.
        /// Key: table logical name, Value: list of attribute logical names.
        /// </summary>
        public Dictionary<string, List<string>> TableAttributes { get; set; } = new();

        /// <summary>
        /// Display metadata per table. Key: table logical name.
        /// </summary>
        public Dictionary<string, TableDisplayInfo> TableDisplayInfo { get; set; } = new();

        /// <summary>
        /// Display metadata per attribute per table.
        /// Key: table logical name, Value: dictionary of attribute logical name to info.
        /// </summary>
        public Dictionary<string, Dictionary<string, AttributeDisplayInfo>> AttributeDisplayInfo { get; set; } = new();

        /// <summary>
        /// Root folder for generated Power BI project files.
        /// </summary>
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Name of the Power BI project (used in folder and file names).
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Serialized window geometry for restoring form position/size.
        /// </summary>
        public string? WindowGeometry { get; set; }

        /// <summary>
        /// Whether to automatically load cached metadata on startup.
        /// </summary>
        public bool AutoloadCache { get; set; } = true;

        /// <summary>
        /// UI preference: false = show only selected attributes, true = show all.
        /// </summary>
        public bool ShowAllAttributes { get; set; } = false;

        /// <summary>
        /// Connection type for the Power BI DirectQuery connection.
        /// "DataverseTDS" for native Dataverse connector, "FabricLink" for Azure Synapse Link.
        /// </summary>
        public string ConnectionType { get; set; } = "DataverseTDS";

        /// <summary>
        /// Fabric Link SQL endpoint URL (when ConnectionType is "FabricLink").
        /// </summary>
        public string? FabricLinkSQLEndpoint { get; set; }

        /// <summary>
        /// Fabric Link SQL database name (when ConnectionType is "FabricLink").
        /// </summary>
        public string? FabricLinkSQLDatabase { get; set; }

        /// <summary>
        /// Logical name of the designated fact table (null if not configured).
        /// </summary>
        public string? FactTable { get; set; }

        /// <summary>
        /// Role assignment per table. Key: table logical name, Value: TableRole.
        /// </summary>
        public Dictionary<string, TableRole> TableRoles { get; set; } = new();

        /// <summary>
        /// All configured star-schema relationships.
        /// </summary>
        public List<RelationshipConfig> Relationships { get; set; } = new();

        /// <summary>
        /// Date table configuration (null if not configured).
        /// </summary>
        public DateTableConfig? DateTableConfig { get; set; }
    }

    #endregion

    #region Display Information Models

    /// <summary>
    /// Display metadata for a table (names and key attributes).
    /// </summary>
    /// <remarks>
    /// Cached from Dataverse metadata to avoid repeated API calls.
    /// Used for UI display and TMDL generation.
    /// </remarks>
    public class TableDisplayInfo
    {
        /// <summary>
        /// Logical name of the table (not persisted, set at runtime).
        /// </summary>
        [JsonIgnore]
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// User-friendly display name from Dataverse.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Schema name (typically PascalCase logical name).
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// Primary key column name (e.g., "accountid").
        /// </summary>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>
        /// Primary name column (e.g., "name" for accounts).
        /// </summary>
        public string? PrimaryNameAttribute { get; set; }
    }

    /// <summary>
    /// Display metadata for an attribute (column).
    /// </summary>
    /// <remarks>
    /// Captures attribute properties needed for UI display and TMDL generation,
    /// including type information and lookup targets.
    /// </remarks>
    public class AttributeDisplayInfo
    {
        /// <summary>
        /// Logical name of the attribute (not persisted, set at runtime).
        /// </summary>
        [JsonIgnore]
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// User-friendly display name from Dataverse.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Schema name (typically PascalCase).
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// Dataverse attribute type (String, Lookup, DateTime, etc.).
        /// </summary>
        public string? AttributeType { get; set; }

        /// <summary>
        /// Whether the field is required (SystemRequired or ApplicationRequired).
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// For Lookup attributes: the logical names of related table(s).
        /// Most lookups have one target; polymorphic lookups have multiple.
        /// </summary>
        public List<string>? Targets { get; set; }
    }

    #endregion

    #region Metadata Cache Models

    /// <summary>
    /// Cached Dataverse metadata for offline access and performance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cache stores:
    /// </para>
    /// <list type="bullet">
    ///   <item>Solutions in the environment</item>
    ///   <item>Tables within the selected solution</item>
    ///   <item>Forms, views, and attributes per table</item>
    /// </list>
    /// <para>
    /// Cache is considered valid for 24 hours and is invalidated if the
    /// environment or solution changes.
    /// </para>
    /// </remarks>
    public class MetadataCache
    {
        /// <summary>
        /// Environment URL this cache was created from.
        /// </summary>
        public string? EnvironmentUrl { get; set; }

        /// <summary>
        /// Solution unique name this cache was created for.
        /// </summary>
        public string? SolutionName { get; set; }

        /// <summary>
        /// Timestamp when the cache was created.
        /// </summary>
        public DateTime CachedDate { get; set; }

        /// <summary>
        /// All solutions in the environment.
        /// </summary>
        public List<DataverseSolution> Solutions { get; set; } = new();

        /// <summary>
        /// Summary list of tables in the solution.
        /// </summary>
        public List<TableInfo> Tables { get; set; } = new();

        /// <summary>
        /// Full table metadata indexed by logical name.
        /// </summary>
        public Dictionary<string, TableInfo> TableData { get; set; } = new();

        /// <summary>
        /// Forms per table. Key: table logical name.
        /// </summary>
        public Dictionary<string, List<FormMetadata>> TableForms { get; set; } = new();

        /// <summary>
        /// Views per table. Key: table logical name.
        /// </summary>
        public Dictionary<string, List<ViewMetadata>> TableViews { get; set; } = new();

        /// <summary>
        /// Attributes per table. Key: table logical name.
        /// </summary>
        public Dictionary<string, List<AttributeMetadata>> TableAttributes { get; set; } = new();

        /// <summary>
        /// Checks if cache is still valid (less than 24 hours old).
        /// </summary>
        /// <returns>True if cache should be used; false if stale.</returns>
        public bool IsValid()
        {
            return CachedDate > DateTime.Now.AddHours(-24);
        }

        /// <summary>
        /// Checks if cache is valid for a specific environment and solution.
        /// </summary>
        /// <param name="environmentUrl">Environment URL to validate against.</param>
        /// <param name="solutionName">Solution name to validate against.</param>
        /// <returns>True if cache matches and is not stale.</returns>
        public bool IsValidFor(string environmentUrl, string solutionName)
        {
            // Normalize URLs for comparison (ensure both have https://)
            var normalizedCachedUrl = EnvironmentUrl ?? "";
            if (!normalizedCachedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                normalizedCachedUrl = "https://" + normalizedCachedUrl;
            
            var normalizedInputUrl = environmentUrl ?? "";
            if (!normalizedInputUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                normalizedInputUrl = "https://" + normalizedInputUrl;
            
            return string.Equals(normalizedCachedUrl, normalizedInputUrl, StringComparison.OrdinalIgnoreCase) &&
                   SolutionName == solutionName &&
                   Tables.Count > 0 &&
                   IsValid();
        }
    }

    /// <summary>
    /// Summary information for a Dataverse table.
    /// </summary>
    public class TableInfo
    {
        /// <summary>Logical name of the table (e.g., "account").</summary>
        public string LogicalName { get; set; } = "";

        /// <summary>User-friendly display name.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Schema name (PascalCase).</summary>
        public string? SchemaName { get; set; }

        /// <summary>Dataverse-assigned object type code.</summary>
        public int ObjectTypeCode { get; set; }

        /// <summary>Primary key column name.</summary>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>Primary name column name.</summary>
        public string? PrimaryNameAttribute { get; set; }

        /// <summary>Dataverse metadata GUID.</summary>
        public string? MetadataId { get; set; }
    }

    /// <summary>
    /// Information about a Dataverse solution.
    /// </summary>
    public class DataverseSolution
    {
        /// <summary>Solution GUID.</summary>
        public string SolutionId { get; set; } = "";

        /// <summary>Unique name for API references.</summary>
        public string UniqueName { get; set; } = "";

        /// <summary>User-friendly display name.</summary>
        public string FriendlyName { get; set; } = "";

        /// <summary>Version string (e.g., "1.0.0.0").</summary>
        public string? Version { get; set; }

        /// <summary>Whether this is a managed or unmanaged solution.</summary>
        public bool IsManaged { get; set; }

        /// <summary>GUID of the solution publisher.</summary>
        public string? PublisherId { get; set; }

        /// <summary>Last modification timestamp.</summary>
        public DateTime? ModifiedOn { get; set; }
    }

    /// <summary>
    /// Minimal table metadata for display purposes.
    /// </summary>
    public class TableMetadata
    {
        /// <summary>Logical name of the table.</summary>
        public string LogicalName { get; set; } = "";

        /// <summary>User-friendly display name.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Schema name (PascalCase).</summary>
        public string? SchemaName { get; set; }

        /// <summary>Primary key column name.</summary>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>Primary name column name.</summary>
        public string? PrimaryNameAttribute { get; set; }
    }

    /// <summary>
    /// Metadata for a table attribute (column).
    /// </summary>
    public class AttributeMetadata
    {
        /// <summary>Logical name of the attribute.</summary>
        public string LogicalName { get; set; } = "";

        /// <summary>User-friendly display name.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Schema name (PascalCase).</summary>
        public string? SchemaName { get; set; }

        /// <summary>Dataverse attribute type (String, Lookup, Integer, etc.).</summary>
        public string? AttributeType { get; set; }

        /// <summary>Whether this is a custom (publisher-prefixed) attribute.</summary>
        public bool IsCustomAttribute { get; set; }

        /// <summary>Whether the field is required.</summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>For Lookup fields: the related table(s).</summary>
        public List<string>? Targets { get; set; }
    }

    /// <summary>
    /// Metadata for a Dataverse form.
    /// </summary>
    public class FormMetadata
    {
        /// <summary>Form GUID.</summary>
        public string FormId { get; set; } = "";

        /// <summary>Form display name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Raw FormXML definition.</summary>
        public string? FormXml { get; set; }

        /// <summary>Attribute names included on this form.</summary>
        public List<string>? Fields { get; set; }
    }

    /// <summary>
    /// Metadata for a Dataverse view (saved query).
    /// </summary>
    public class ViewMetadata
    {
        /// <summary>View GUID.</summary>
        public string ViewId { get; set; } = "";

        /// <summary>View display name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Whether this is the default view for the table.</summary>
        public bool IsDefault { get; set; }

        /// <summary>FetchXML query definition (includes filter conditions).</summary>
        public string? FetchXml { get; set; }

        /// <summary>Columns included in the view grid.</summary>
        public List<string> Columns { get; set; } = new();
    }

    #endregion

    #region Export Models

    /// <summary>
    /// Complete export configuration for TMDL generation.
    /// </summary>
    /// <remarks>
    /// This model is created when the user triggers TMDL generation and contains
    /// all the information needed by SemanticModelBuilder to create the Power BI
    /// project files.
    /// </remarks>
    public class ExportMetadata
    {
        /// <summary>Dataverse environment URL.</summary>
        public string Environment { get; set; } = "";

        /// <summary>Solution unique name.</summary>
        public string Solution { get; set; } = "";

        /// <summary>Power BI project name.</summary>
        public string ProjectName { get; set; } = "";

        /// <summary>Logical name of the fact table (if star-schema configured).</summary>
        public string? FactTable { get; set; }

        /// <summary>All configured relationships.</summary>
        public List<ExportRelationship> Relationships { get; set; } = new();

        /// <summary>All tables to include in the model.</summary>
        public List<ExportTable> Tables { get; set; } = new();
    }

    /// <summary>
    /// Relationship configuration for export.
    /// </summary>
    public class ExportRelationship
    {
        /// <summary>Table containing the lookup (many side).</summary>
        public string SourceTable { get; set; } = "";

        /// <summary>Lookup attribute logical name.</summary>
        public string SourceAttribute { get; set; } = "";

        /// <summary>Target dimension table (one side).</summary>
        public string TargetTable { get; set; } = "";

        /// <summary>Relationship display name in Power BI.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Whether this is an active relationship.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Whether this is a snowflake (dimension-to-dimension) relationship.</summary>
        public bool IsSnowflake { get; set; } = false;

        /// <summary>Whether to enable referential integrity optimization.</summary>
        public bool AssumeReferentialIntegrity { get; set; } = false;
    }

    /// <summary>
    /// Table configuration for export to TMDL.
    /// </summary>
    public class ExportTable
    {
        /// <summary>Logical name of the table.</summary>
        public string LogicalName { get; set; } = "";

        /// <summary>Display name for the table in Power BI.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Schema name.</summary>
        public string? SchemaName { get; set; }

        /// <summary>Dataverse object type code.</summary>
        public int ObjectTypeCode { get; set; }

        /// <summary>Primary key column name.</summary>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>Primary name column name.</summary>
        public string? PrimaryNameAttribute { get; set; }

        /// <summary>Role in star-schema ("Fact" or "Dimension").</summary>
        public string Role { get; set; } = "Dimension";

        /// <summary>Whether the table has a statecode attribute for filtering.</summary>
        public bool HasStateCode { get; set; } = false;

        /// <summary>Forms selected for column extraction.</summary>
        public List<ExportForm> Forms { get; set; } = new();

        /// <summary>View selected for filtering (optional).</summary>
        public ExportView? View { get; set; }

        /// <summary>All attributes to include in the model.</summary>
        public List<AttributeMetadata> Attributes { get; set; } = new();
    }

    /// <summary>
    /// Form reference for export.
    /// </summary>
    public class ExportForm
    {
        /// <summary>Form GUID.</summary>
        public string FormId { get; set; } = "";

        /// <summary>Form display name.</summary>
        public string FormName { get; set; } = "";

        /// <summary>Number of fields on this form.</summary>
        public int FieldCount { get; set; }
    }

    /// <summary>
    /// View reference for export.
    /// </summary>
    public class ExportView
    {
        /// <summary>View GUID.</summary>
        public string ViewId { get; set; } = "";

        /// <summary>View display name.</summary>
        public string ViewName { get; set; } = "";

        /// <summary>FetchXML query for filtering.</summary>
        public string? FetchXml { get; set; }
    }

    #endregion
}
