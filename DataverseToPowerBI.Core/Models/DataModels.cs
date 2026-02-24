// =============================================================================
// DataModels.cs - Core Data Models
// =============================================================================
// Purpose: Defines the core data models used throughout the application.
// 
// This file contains models for:
//   - Configuration settings (persisted to JSON)
//   - Dataverse metadata (solutions, tables, attributes, forms, views)
//   - Star-schema relationship configuration
//   - Date/Calendar table configuration
//   - Export metadata (used when generating Power BI semantic models)
//   - Metadata caching (reduces API calls to Dataverse)
//
// These models are shared across the solution to ensure consistent data
// structures for configuration, metadata, and export operations.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DataverseToPowerBI.Core.Models
{
    #region Configuration Models

    /// <summary>
    /// Root container for all saved configurations.
    /// This is the top-level structure persisted to the configurations.json file.
    /// </summary>
    /// <remarks>
    /// Users can create multiple named configurations for different Dataverse
    /// environments or projects. The last used configuration is automatically
    /// loaded on startup.
    /// </remarks>
    public class ConfigurationsFile
    {
        /// <summary>
        /// Collection of all saved configurations.
        /// Each configuration represents a complete project setup.
        /// </summary>
        public List<ConfigurationEntry> Configurations { get; set; } = new();

        /// <summary>
        /// Name of the configuration that was last used.
        /// Used to auto-load the most recent configuration on startup.
        /// </summary>
        public string? LastUsedConfigurationName { get; set; }
    }

    /// <summary>
    /// A named configuration entry containing all settings for a project.
    /// </summary>
    /// <remarks>
    /// Each configuration is independent and stores:
    /// - The Dataverse environment URL
    /// - Selected solution and tables
    /// - Form and view selections for each table
    /// - Star-schema configuration (fact/dimension roles, relationships)
    /// - Calendar table settings
    /// </remarks>
    public class ConfigurationEntry
    {
        /// <summary>
        /// Unique name for this configuration.
        /// Used to identify the configuration in the UI and for auto-loading.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Timestamp of when this configuration was last used.
        /// Used for sorting configurations by recent usage.
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.Now;

        /// <summary>
        /// The complete settings for this configuration.
        /// </summary>
        public AppSettings Settings { get; set; } = new();
    }

    #endregion

    #region Star-Schema Configuration

    /// <summary>
    /// Defines the role of a table in a star-schema data model.
    /// </summary>
    /// <remarks>
    /// In Power BI star-schema design:
    /// <list type="bullet">
    ///   <item>
    ///     <term>Fact tables</term>
    ///     <description>
    ///       Contain the measurable, quantitative data (sales amounts, quantities, etc.).
    ///       There is typically one central fact table that relates to multiple dimensions.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Dimension tables</term>
    ///     <description>
    ///       Contain descriptive attributes used for filtering and grouping
    ///       (customers, products, dates, etc.). These surround the fact table.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    public enum TableRole
    {
        /// <summary>
        /// Dimension table - lookup/reference table containing descriptive attributes.
        /// Most tables in a star schema are dimensions (the "points" of the star).
        /// </summary>
        Dimension,

        /// <summary>
        /// Fact table - the central table containing measurable business events.
        /// Only one table should be designated as the fact table (the "center" of the star).
        /// </summary>
        Fact
    }

    /// <summary>
    /// Represents a relationship between two tables in the star schema.
    /// Defines how tables are connected for Power BI relationship generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Relationships in Power BI connect tables through key columns.
    /// This configuration captures:
    /// </para>
    /// <list type="bullet">
    ///   <item>Which lookup field connects the tables</item>
    ///   <item>Whether the relationship is active (only one can be active per column)</item>
    ///   <item>Whether it's a snowflake relationship (dimension to parent dimension)</item>
    ///   <item>Referential integrity settings for query optimization</item>
    /// </list>
    /// </remarks>
    public class RelationshipConfig
    {
        /// <summary>
        /// The table on the "many" side of the relationship.
        /// Typically the fact table or a dimension referencing a parent dimension.
        /// </summary>
        /// <example>For a Fact -> Dimension relationship, this is the Fact table.</example>
        public string SourceTable { get; set; } = "";

        /// <summary>
        /// The lookup attribute on the source table that points to the target.
        /// This is the foreign key column.
        /// </summary>
        /// <example>"_customerid_value" for a customer lookup field.</example>
        public string SourceAttribute { get; set; } = "";

        /// <summary>
        /// The table on the "one" side of the relationship.
        /// This is the dimension/lookup target table.
        /// </summary>
        /// <example>For a Fact -> Dimension relationship, this is the Dimension table.</example>
        public string TargetTable { get; set; } = "";

        /// <summary>
        /// User-friendly display name for the relationship.
        /// Used in the UI to help users understand the relationship's purpose.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Whether this relationship is the active relationship between the tables.
        /// </summary>
        /// <remarks>
        /// Power BI requires exactly one active relationship between any two tables.
        /// Additional relationships must be inactive and used via USERELATIONSHIP in DAX.
        /// </remarks>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Indicates if this is a snowflake relationship (dimension to parent dimension).
        /// </summary>
        /// <remarks>
        /// Snowflake relationships occur when dimensions have their own hierarchies,
        /// such as Product -> Category -> Department. These create a "snowflake"
        /// pattern extending from the star schema.
        /// </remarks>
        public bool IsSnowflake { get; set; } = false;

        /// <summary>
        /// Indicates if this is a reverse (one-to-many) relationship direction.
        /// </summary>
        /// <remarks>
        /// Standard relationships are many-to-one (lookup direction).
        /// Reverse relationships may be needed for specific DAX calculations.
        /// </remarks>
        public bool IsReverse { get; set; } = false;

        /// <summary>
        /// Whether Power BI should assume referential integrity for this relationship.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, Power BI uses INNER JOINs instead of LEFT OUTER JOINs,
        /// which can significantly improve query performance.
        /// </para>
        /// <para>
        /// Only enable this when you're certain all lookup values exist in the target
        /// table (i.e., the lookup field is required in Dataverse).
        /// </para>
        /// </remarks>
        public bool AssumeReferentialIntegrity { get; set; } = false;
    }

    #endregion

    #region Calendar Table Configuration

    /// <summary>
    /// Configuration for the Date/Calendar table feature.
    /// Enables timezone-aware date handling and calendar table generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dataverse stores all DateTime values in UTC. This configuration allows:
    /// </para>
    /// <list type="bullet">
    ///   <item>Converting UTC dates to a specific timezone for reporting</item>
    ///   <item>Generating a calendar table with the appropriate date range</item>
    ///   <item>Wrapping DateTime fields in calculated columns for timezone adjustment</item>
    /// </list>
    /// </remarks>
    public class DateTableConfig
    {
        /// <summary>
        /// Logical name of the table containing the primary date field.
        /// This table's dates drive the calendar table date range.
        /// </summary>
        public string PrimaryDateTable { get; set; } = "";

        /// <summary>
        /// The primary date field used for the main date relationship.
        /// This field will be used to determine the calendar table's date range.
        /// </summary>
        public string PrimaryDateField { get; set; } = "";

        /// <summary>
        /// Windows timezone identifier for date conversion.
        /// </summary>
        /// <example>"Eastern Standard Time", "Pacific Standard Time", "GMT Standard Time"</example>
        public string TimeZoneId { get; set; } = "";

        /// <summary>
        /// UTC offset in hours for the configured timezone.
        /// </summary>
        /// <remarks>
        /// Negative values for timezones west of UTC (Americas),
        /// positive values for timezones east of UTC (Europe, Asia).
        /// </remarks>
        /// <example>-5 for EST, -8 for PST, 0 for UTC, +1 for CET</example>
        public double UtcOffsetHours { get; set; } = 0;

        /// <summary>
        /// Start year for the calendar table date range.
        /// </summary>
        public int StartYear { get; set; }

        /// <summary>
        /// End year for the calendar table date range.
        /// </summary>
        public int EndYear { get; set; }

        /// <summary>
        /// Collection of DateTime fields that should be timezone-adjusted.
        /// Each field will get a calculated column with the adjusted date/datetime.
        /// </summary>
        public List<DateTimeFieldConfig> WrappedFields { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a single DateTime field that should be timezone-adjusted.
    /// </summary>
    public class DateTimeFieldConfig
    {
        /// <summary>
        /// Logical name of the table containing this field.
        /// </summary>
        public string TableName { get; set; } = "";

        /// <summary>
        /// Logical name of the DateTime field to wrap.
        /// </summary>
        public string FieldName { get; set; } = "";

        /// <summary>
        /// Whether to truncate the time component, keeping only the date portion.
        /// </summary>
        /// <remarks>
        /// When true, generates a Date type column suitable for relating to a calendar table.
        /// When false, preserves the full DateTime value with timezone adjustment.
        /// </remarks>
        public bool ConvertToDateOnly { get; set; } = true;
    }

    #endregion

    #region Application Settings

    /// <summary>
    /// Complete application settings for a configuration.
    /// Contains all user selections and project settings.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// The Dataverse environment URL for this configuration.
        /// </summary>
        /// <example>"https://myorg.crm.dynamics.com"</example>
        public string? LastEnvironmentUrl { get; set; }

        /// <summary>
        /// Unique name of the selected Dataverse solution.
        /// </summary>
        public string? LastSolution { get; set; }

        /// <summary>
        /// List of table logical names selected for the semantic model.
        /// </summary>
        public List<string> SelectedTables { get; set; } = new();

        /// <summary>
        /// Maps table logical names to their selected form IDs.
        /// Forms determine which attributes are included in the model.
        /// </summary>
        public Dictionary<string, string> TableForms { get; set; } = new();

        /// <summary>
        /// Maps table logical names to their selected form display names.
        /// Used for UI display purposes.
        /// </summary>
        public Dictionary<string, string> TableFormNames { get; set; } = new();

        /// <summary>
        /// Maps table logical names to their selected view IDs.
        /// Views provide FetchXML filters for the data queries.
        /// </summary>
        public Dictionary<string, string> TableViews { get; set; } = new();

        /// <summary>
        /// Maps table logical names to their selected view display names.
        /// Used for UI display purposes.
        /// </summary>
        public Dictionary<string, string> TableViewNames { get; set; } = new();

        /// <summary>
        /// Maps table logical names to their list of selected attribute logical names.
        /// These are the columns included in the semantic model for each table.
        /// </summary>
        public Dictionary<string, List<string>> TableAttributes { get; set; } = new();

        /// <summary>
        /// Maps table logical names to their display/schema information.
        /// Used for generating user-friendly names in the semantic model.
        /// </summary>
        public Dictionary<string, TableDisplayInfo> TableDisplayInfo { get; set; } = new();

        /// <summary>
        /// Nested mapping of table -> attribute -> display information.
        /// Used for generating column names and descriptions in the semantic model.
        /// </summary>
        public Dictionary<string, Dictionary<string, AttributeDisplayInfo>> AttributeDisplayInfo { get; set; } = new();

        /// <summary>
        /// Output folder path where the semantic model files will be generated.
        /// </summary>
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Name of the semantic model project.
        /// Used as the folder name and model display name.
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Serialized window geometry (position and size) for UI restoration.
        /// </summary>
        public string? WindowGeometry { get; set; }

        /// <summary>
        /// Whether to automatically load cached metadata on startup.
        /// Improves startup performance by avoiding API calls.
        /// </summary>
        public bool AutoloadCache { get; set; } = true;

        /// <summary>
        /// Controls attribute list display mode.
        /// When false, shows only form-derived attributes.
        /// When true, shows all table attributes.
        /// </summary>
        public bool ShowAllAttributes { get; set; } = false;

        /// <summary>
        /// Logical name of the designated fact table.
        /// Null if no fact table has been configured.
        /// </summary>
        public string? FactTable { get; set; }

        /// <summary>
        /// Maps table logical names to their star-schema roles.
        /// </summary>
        public Dictionary<string, TableRole> TableRoles { get; set; } = new();

        /// <summary>
        /// Collection of all configured relationships between tables.
        /// </summary>
        public List<RelationshipConfig> Relationships { get; set; } = new();

        /// <summary>
        /// Configuration for the calendar/date table feature.
        /// Null if not configured.
        /// </summary>
        public DateTableConfig? DateTableConfig { get; set; }

        /// <summary>
        /// When true, uses display names as SQL column aliases (AS [Display Name])
        /// instead of renaming columns at the TMDL level.
        /// Default: true.
        /// </summary>
        public bool UseDisplayNameAliasesInSql { get; set; } = true;

        /// <summary>
        /// User-specified override display names for attributes.
        /// Outer key = table logical name, inner key = attribute logical name, value = override display name.
        /// Used for disambiguating duplicate display names and custom naming.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> AttributeDisplayNameOverrides { get; set; } = new();
    }

    #endregion

    #region Display Information Models

    /// <summary>
    /// Display and schema information for a table.
    /// Cached to avoid repeated API calls for metadata.
    /// </summary>
    public class TableDisplayInfo
    {
        /// <summary>
        /// The logical name (system name) of the table.
        /// Not serialized as it's used as the dictionary key.
        /// </summary>
        [JsonIgnore]
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// User-friendly display name (localized label).
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// The schema name (typically PascalCase version of logical name).
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// The primary key attribute for this table.
        /// </summary>
        /// <example>"accountid" for the Account table</example>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>
        /// The primary name attribute (main display field).
        /// </summary>
        /// <example>"name" for the Account table</example>
        public string? PrimaryNameAttribute { get; set; }
    }

    /// <summary>
    /// Display and schema information for an attribute/column.
    /// Cached to avoid repeated API calls for metadata.
    /// </summary>
    public class AttributeDisplayInfo
    {
        /// <summary>
        /// The logical name (system name) of the attribute.
        /// Not serialized as it's used as the dictionary key.
        /// </summary>
        [JsonIgnore]
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// User-friendly display name (localized label).
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// The schema name (typically PascalCase version of logical name).
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// The Dataverse attribute type.
        /// </summary>
        /// <example>"String", "Integer", "Lookup", "DateTime", "Picklist"</example>
        public string? AttributeType { get; set; }

        /// <summary>
        /// For DateTime attributes, the Dataverse DateTimeBehavior value.
        /// </summary>
        /// <remarks>
        /// Expected values include "UserLocal", "DateOnly", and "TimeZoneIndependent".
        /// Null for non-DateTime attributes or when metadata is unavailable.
        /// </remarks>
        public string? DateTimeBehavior { get; set; }

        /// <summary>
        /// Whether this attribute is required in Dataverse.
        /// Used for referential integrity settings.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// For Lookup attributes, the list of target table logical names.
        /// Most lookups have a single target, but polymorphic lookups have multiple.
        /// </summary>
        public List<string>? Targets { get; set; }

        /// <summary>
        /// For Picklist (Choice) and Boolean attributes, the logical name of the associated virtual attribute
        /// that contains the display text/label value.
        /// </summary>
        /// <remarks>
        /// Most choice fields use the pattern "{attributename}name" (e.g., "statecode" -> "statecode name"),
        /// but there are exceptions (e.g., "donotsendmm" -> "donotsendmarketingmaterial").
        /// This property captures the actual virtual attribute name from metadata.
        /// </remarks>
        /// <example>"statuscodename", "donotsendmarketingmaterial"</example>
        public string? VirtualAttributeName { get; set; }

        /// <summary>
        /// For Picklist attributes, indicates whether the optionset is global.
        /// Global optionsets use GlobalOptionsetMetadata table in FabricLink queries,
        /// while entity-specific optionsets use OptionsetMetadata table.
        /// </summary>
        public bool? IsGlobal { get; set; }

        /// <summary>
        /// For Picklist attributes, the logical name of the optionset.
        /// Used for JOINs to metadata tables in FabricLink queries.
        /// </summary>
        public string? OptionSetName { get; set; }

        /// <summary>
        /// User-specified override display name for SQL AS alias.
        /// When set, this value is used instead of DisplayName for SQL column aliases.
        /// Allows disambiguation of duplicate display names and custom naming.
        /// </summary>
        public string? OverrideDisplayName { get; set; }
    }

    #endregion

    #region Metadata Cache

    /// <summary>
    /// Cached Dataverse metadata to reduce API calls and improve performance.
    /// Persisted to a local JSON file and validated before use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cache stores:
    /// </para>
    /// <list type="bullet">
    ///   <item>Available solutions in the environment</item>
    ///   <item>Tables in the selected solution</item>
    ///   <item>Forms and views for each table</item>
    ///   <item>Attributes for each table</item>
    /// </list>
    /// <para>
    /// Cache is considered valid for 24 hours and only if it matches
    /// the current environment URL and solution.
    /// </para>
    /// </remarks>
    public class MetadataCache
    {
        /// <summary>
        /// The Dataverse environment URL this cache was created from.
        /// </summary>
        public string? EnvironmentUrl { get; set; }

        /// <summary>
        /// The solution this cache was created for.
        /// </summary>
        public string? SolutionName { get; set; }

        /// <summary>
        /// When this cache was created/last updated.
        /// </summary>
        public DateTime CachedDate { get; set; }

        /// <summary>
        /// List of all solutions in the environment.
        /// </summary>
        public List<DataverseSolution> Solutions { get; set; } = new();

        /// <summary>
        /// List of all tables in the selected solution.
        /// </summary>
        public List<TableInfo> Tables { get; set; } = new();

        /// <summary>
        /// Table metadata indexed by logical name for quick lookup.
        /// </summary>
        public Dictionary<string, TableInfo> TableData { get; set; } = new();

        /// <summary>
        /// Forms for each table, indexed by table logical name.
        /// </summary>
        public Dictionary<string, List<FormMetadata>> TableForms { get; set; } = new();

        /// <summary>
        /// Views for each table, indexed by table logical name.
        /// </summary>
        public Dictionary<string, List<ViewMetadata>> TableViews { get; set; } = new();

        /// <summary>
        /// Attributes for each table, indexed by table logical name.
        /// </summary>
        public Dictionary<string, List<AttributeMetadata>> TableAttributes { get; set; } = new();

        /// <summary>
        /// Checks if the cache is still valid (less than 24 hours old).
        /// </summary>
        /// <returns>True if the cache was created within the last 24 hours.</returns>
        public bool IsValid()
        {
            return CachedDate > DateTime.Now.AddHours(-24);
        }

        /// <summary>
        /// Validates the cache against a specific environment and solution.
        /// </summary>
        /// <param name="environmentUrl">The environment URL to validate against.</param>
        /// <param name="solutionName">The solution name to validate against.</param>
        /// <returns>
        /// True if the cache is valid, matches the environment/solution, and contains data.
        /// </returns>
        /// <remarks>
        /// URLs are normalized (with https:// prefix) before comparison to handle
        /// variations in how URLs might be entered or stored.
        /// </remarks>
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

    #endregion

    #region Dataverse Metadata Models

    /// <summary>
    /// Basic information about a Dataverse table (entity).
    /// Used for table listing and selection.
    /// </summary>
    public class TableInfo
    {
        /// <summary>
        /// The system name of the table (lowercase, no spaces).
        /// </summary>
        /// <example>"account", "contact", "cr123_customtable"</example>
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// User-friendly display name from the table's localized label.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// The schema name (typically PascalCase).
        /// </summary>
        /// <example>"Account", "Contact", "cr123_CustomTable"</example>
        public string? SchemaName { get; set; }

        /// <summary>
        /// The internal object type code assigned by Dataverse.
        /// Unique identifier for the table type.
        /// </summary>
        public int ObjectTypeCode { get; set; }

        /// <summary>
        /// The primary key attribute name.
        /// </summary>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>
        /// The primary name/display attribute name.
        /// </summary>
        public string? PrimaryNameAttribute { get; set; }

        /// <summary>
        /// GUID for the table's metadata record.
        /// Used for some API calls that require the metadata ID.
        /// </summary>
        public string? MetadataId { get; set; }
    }

    /// <summary>
    /// Information about a Dataverse solution.
    /// Solutions are containers for customizations and components.
    /// </summary>
    public class DataverseSolution
    {
        /// <summary>
        /// GUID that uniquely identifies this solution.
        /// </summary>
        public string SolutionId { get; set; } = "";

        /// <summary>
        /// The unique name used for import/export operations.
        /// </summary>
        /// <example>"MySolution", "MicrosoftDynamics365"</example>
        public string UniqueName { get; set; } = "";

        /// <summary>
        /// User-friendly display name for the solution.
        /// </summary>
        public string FriendlyName { get; set; } = "";

        /// <summary>
        /// Solution version string.
        /// </summary>
        /// <example>"1.0.0.0"</example>
        public string? Version { get; set; }

        /// <summary>
        /// Whether this is a managed solution (read-only) or unmanaged.
        /// </summary>
        public bool IsManaged { get; set; }

        /// <summary>
        /// GUID of the publisher who owns this solution.
        /// </summary>
        public string? PublisherId { get; set; }

        /// <summary>
        /// When this solution was last modified.
        /// </summary>
        public DateTime? ModifiedOn { get; set; }

        /// <summary>
        /// Returns the friendly name for display in ComboBox and other controls.
        /// </summary>
        public override string ToString()
        {
            return FriendlyName ?? UniqueName ?? "(Unnamed)";
        }
    }

    /// <summary>
    /// Detailed metadata for a table.
    /// Retrieved when more information is needed than TableInfo provides.
    /// </summary>
    public class TableMetadata
    {
        /// <summary>
        /// The system name of the table.
        /// </summary>
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// User-friendly display name.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// The schema name.
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// Primary key attribute name.
        /// </summary>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>
        /// Primary name/display attribute name.
        /// </summary>
        public string? PrimaryNameAttribute { get; set; }
    }

    /// <summary>
    /// Metadata for a table attribute (column/field).
    /// Contains type information needed for semantic model generation.
    /// </summary>
    public class AttributeMetadata
    {
        /// <summary>
        /// The system name of the attribute.
        /// </summary>
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// User-friendly display name.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// The schema name.
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// The description from Dataverse metadata.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The Dataverse attribute type.
        /// </summary>
        /// <example>"String", "Integer", "Decimal", "Lookup", "DateTime", "Picklist", "Boolean"</example>
        public string? AttributeType { get; set; }

        /// <summary>
        /// For DateTime attributes, the Dataverse DateTimeBehavior value.
        /// </summary>
        /// <remarks>
        /// Expected values include "UserLocal", "DateOnly", and "TimeZoneIndependent".
        /// Null for non-DateTime attributes or when metadata is unavailable.
        /// </remarks>
        public string? DateTimeBehavior { get; set; }

        /// <summary>
        /// Whether this is a custom (user-created) attribute vs. a system attribute.
        /// </summary>
        public bool IsCustomAttribute { get; set; }

        /// <summary>
        /// Whether this attribute is required (SystemRequired or ApplicationRequired).
        /// Used for setting referential integrity on relationships.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// For Lookup attributes, the list of target table logical names.
        /// Standard lookups have one target; polymorphic lookups have multiple.
        /// </summary>
        /// <example>["account"], ["contact", "account", "lead"]</example>
        public List<string>? Targets { get; set; }

        /// <summary>
        /// For Picklist (Choice) and Boolean attributes, the logical name of the associated virtual attribute
        /// that contains the display text/label value.
        /// </summary>
        /// <remarks>
        /// Most choice fields use the pattern "{attributename}name" (e.g., "statecode" -> "statecode name"),
        /// but there are exceptions (e.g., "donotsendmm" -> "donotsendmarketingmaterial").
        /// This property captures the actual virtual attribute name from metadata.
        /// </remarks>
        /// <example>"statuscodename", "donotsendmarketingmaterial"</example>
        public string? VirtualAttributeName { get; set; }

        /// <summary>
        /// For Picklist attributes, indicates whether the optionset is global.
        /// Global optionsets use GlobalOptionsetMetadata table in FabricLink queries,
        /// while entity-specific optionsets use OptionsetMetadata table.
        /// </summary>
        public bool? IsGlobal { get; set; }

        /// <summary>
        /// For Picklist attributes, the logical name of the optionset.
        /// Used for JOINs to metadata tables in FabricLink queries.
        /// </summary>
        public string? OptionSetName { get; set; }
    }

    /// <summary>
    /// Metadata for a Dataverse form.
    /// Forms define the UI layout and which fields are displayed.
    /// </summary>
    public class FormMetadata
    {
        /// <summary>
        /// GUID that uniquely identifies this form.
        /// </summary>
        public string FormId { get; set; } = "";

        /// <summary>
        /// Display name of the form.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// The FormXML definition (if loaded).
        /// Contains the form layout and field definitions.
        /// </summary>
        public string? FormXml { get; set; }

        /// <summary>
        /// List of field logical names extracted from the FormXML.
        /// These are the fields displayed on this form.
        /// </summary>
        public List<string>? Fields { get; set; }
    }

    /// <summary>
    /// Metadata for a Dataverse view (saved query).
    /// Views define which records and columns are displayed in lists.
    /// </summary>
    public class ViewMetadata
    {
        /// <summary>
        /// GUID that uniquely identifies this view.
        /// </summary>
        public string ViewId { get; set; } = "";

        /// <summary>
        /// Display name of the view.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Whether this is the default view for the table.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// The FetchXML query definition (if loaded).
        /// Used to filter records and is converted to SQL for the semantic model.
        /// </summary>
        public string? FetchXml { get; set; }

        /// <summary>
        /// List of column logical names visible in this view layout.
        /// Extracted from LayoutXML when available, with fallback to FetchXML attributes.
        /// </summary>
        public List<string> Columns { get; set; } = new();

        /// <summary>
        /// Whether this is a personal (user) view vs. a system (public) view.
        /// </summary>
        public bool IsPersonal { get; set; }

        /// <summary>
        /// Columns from related entities referenced via link-entity in the view's FetchXML.
        /// Each entry describes the lookup field on the current entity and the attribute from the related entity.
        /// </summary>
        public List<ViewLinkedColumn> LinkedColumns { get; set; } = new();
    }

    /// <summary>
    /// Represents a column from a related entity referenced via link-entity in a view's FetchXML.
    /// </summary>
    public class ViewLinkedColumn
    {
        /// <summary>
        /// The lookup attribute on the current (source) entity that the link-entity joins through.
        /// This is the "to" attribute in the FetchXML link-entity element.
        /// </summary>
        public string LookupAttribute { get; set; } = "";

        /// <summary>
        /// Logical name of the related entity being joined.
        /// </summary>
        public string LinkedEntityName { get; set; } = "";

        /// <summary>
        /// Logical name of the attribute on the related entity.
        /// </summary>
        public string AttributeName { get; set; } = "";

        /// <summary>
        /// Alias prefix for the linked entity (from the FetchXML alias attribute).
        /// </summary>
        public string? Alias { get; set; }
    }

    /// <summary>
    /// Determines how the default field selection is derived for a table.
    /// </summary>
    public enum FieldSelectionMode
    {
        /// <summary>Use fields from the selected Dataverse form.</summary>
        Form,
        /// <summary>Use columns from the selected Dataverse view.</summary>
        View,
        /// <summary>Use columns from a different Dataverse view than the row filter view.</summary>
        DifferentView,
        /// <summary>User manually selects fields (no automatic source).</summary>
        Custom
    }

    #endregion

    #region Export Models

    /// <summary>
    /// Complete metadata for semantic model export.
    /// This is the data structure passed to the SemanticModelBuilder.
    /// </summary>
    /// <remarks>
    /// Contains all information needed to generate a Power BI semantic model:
    /// - Environment and solution identification
    /// - Table definitions with their attributes
    /// - Relationship configurations for the star schema
    /// - View definitions with FetchXML filters
    /// </remarks>
    public class ExportMetadata
    {
        /// <summary>
        /// The Dataverse environment URL.
        /// </summary>
        public string Environment { get; set; } = "";

        /// <summary>
        /// The solution name.
        /// </summary>
        public string Solution { get; set; } = "";

        /// <summary>
        /// The project/model name.
        /// </summary>
        public string ProjectName { get; set; } = "";

        /// <summary>
        /// The designated fact table (null if not configured).
        /// </summary>
        public string? FactTable { get; set; }

        /// <summary>
        /// All star-schema relationships to generate in the model.
        /// </summary>
        public List<ExportRelationship> Relationships { get; set; } = new();

        /// <summary>
        /// All tables to include in the semantic model.
        /// </summary>
        public List<ExportTable> Tables { get; set; } = new();
    }

    /// <summary>
    /// Relationship definition for export.
    /// Simplified version of RelationshipConfig for model generation.
    /// </summary>
    public class ExportRelationship
    {
        /// <summary>
        /// The table on the "many" side (source of the lookup).
        /// </summary>
        public string SourceTable { get; set; } = "";

        /// <summary>
        /// The lookup attribute on the source table.
        /// </summary>
        public string SourceAttribute { get; set; } = "";

        /// <summary>
        /// The table on the "one" side (target of the lookup).
        /// </summary>
        public string TargetTable { get; set; } = "";

        /// <summary>
        /// Display name for the relationship.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Whether this is the active relationship between the tables.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Whether this is a snowflake relationship.
        /// </summary>
        public bool IsSnowflake { get; set; } = false;

        /// <summary>
        /// Snowflake depth level: 0 = Direct, 1 = Snowflake, 2 = Double Snowflake.
        /// </summary>
        public int SnowflakeLevel { get; set; } = 0;

        /// <summary>
        /// Whether to assume referential integrity.
        /// </summary>
        public bool AssumeReferentialIntegrity { get; set; } = false;
    }

    /// <summary>
    /// Table definition for export.
    /// Contains all data needed to generate the table in the semantic model.
    /// </summary>
    public class ExportTable
    {
        /// <summary>
        /// Table logical name.
        /// </summary>
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// Table display name.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Table schema name.
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// Object type code.
        /// </summary>
        public int ObjectTypeCode { get; set; }

        /// <summary>
        /// Primary key attribute name.
        /// </summary>
        public string? PrimaryIdAttribute { get; set; }

        /// <summary>
        /// Primary name attribute name.
        /// </summary>
        public string? PrimaryNameAttribute { get; set; }

        /// <summary>
        /// The role of this table in the star schema ("Fact" or "Dimension").
        /// </summary>
        public string Role { get; set; } = "Dimension";

        /// <summary>
        /// Whether the table has a statecode attribute.
        /// Used to add "Exclude Inactive" filter to queries.
        /// </summary>
        public bool HasStateCode { get; set; } = false;

        /// <summary>
        /// Selected forms for this table.
        /// </summary>
        public List<ExportForm> Forms { get; set; } = new();

        /// <summary>
        /// Selected view for this table (null if no view selected).
        /// </summary>
        public ExportView? View { get; set; }

        /// <summary>
        /// Selected attributes/columns for this table.
        /// </summary>
        public List<AttributeMetadata> Attributes { get; set; } = new();

        /// <summary>
        /// Expanded lookup configurations for this table.
        /// Each entry represents a lookup field whose related table attributes
        /// are flattened into this table via LEFT OUTER JOIN.
        /// </summary>
        public List<ExpandedLookupConfig> ExpandedLookups { get; set; } = new();

        /// <summary>
        /// Per-lookup sub-column configurations. Keyed by lookup attribute logical name.
        /// Null config or missing key means defaults should be resolved by the caller.
        /// </summary>
        public Dictionary<string, LookupSubColumnConfig>? LookupSubColumnConfigs { get; set; }

        /// <summary>
        /// Per-choice sub-column configurations. Keyed by attribute logical name for
        /// picklist/state/status/boolean/multiselect attributes.
        /// Null config or missing key means defaults should be resolved by the caller.
        /// </summary>
        public Dictionary<string, ChoiceSubColumnConfig>? ChoiceSubColumnConfigs { get; set; }
    }

    /// <summary>
    /// Form reference for export.
    /// </summary>
    public class ExportForm
    {
        /// <summary>
        /// Form GUID.
        /// </summary>
        public string FormId { get; set; } = "";

        /// <summary>
        /// Form display name.
        /// </summary>
        public string FormName { get; set; } = "";

        /// <summary>
        /// Number of fields on this form.
        /// </summary>
        public int FieldCount { get; set; }
    }

    /// <summary>
    /// View reference for export.
    /// </summary>
    public class ExportView
    {
        /// <summary>
        /// View GUID.
        /// </summary>
        public string ViewId { get; set; } = "";

        /// <summary>
        /// View display name.
        /// </summary>
        public string ViewName { get; set; } = "";

        /// <summary>
        /// The FetchXML query definition.
        /// Converted to SQL WHERE clause for the semantic model partition.
        /// </summary>
        public string? FetchXml { get; set; }
    }

    #endregion

    #region Expanded Lookup Configuration

    /// <summary>
    /// Configuration for expanding a lookup field to include attributes from the related table.
    /// This flattens related table columns into the parent table via a LEFT OUTER JOIN,
    /// avoiding the need for a separate dimension table.
    /// </summary>
    /// <remarks>
    /// <para>Use cases:</para>
    /// <list type="bullet">
    ///   <item>Pull 1-2 fields from a related table without creating a full dimension</item>
    ///   <item>Flatten lookup references for simpler reporting</item>
    ///   <item>Avoid star-schema complexity for infrequently used dimensions</item>
    /// </list>
    /// <para>Performance note: expanding 3+ related tables or selecting 10+ fields
    /// from a single join may impact DirectQuery performance.</para>
    /// </remarks>
    public class ExpandedLookupConfig
    {
        /// <summary>
        /// Logical name of the lookup attribute on the source table.
        /// This is the foreign key column used for the JOIN.
        /// </summary>
        public string LookupAttributeName { get; set; } = "";

        /// <summary>
        /// Display name of the lookup attribute (for UI display).
        /// </summary>
        public string? LookupDisplayName { get; set; }

        /// <summary>
        /// Logical name of the related/target table being expanded.
        /// </summary>
        public string TargetTableLogicalName { get; set; } = "";

        /// <summary>
        /// Display name of the related/target table (for UI display).
        /// </summary>
        public string? TargetTableDisplayName { get; set; }

        /// <summary>
        /// Primary key attribute of the target table (for JOIN condition).
        /// </summary>
        public string TargetTablePrimaryKey { get; set; } = "";

        /// <summary>
        /// The form ID used to select attributes from the related table.
        /// </summary>
        public string? FormId { get; set; }

        /// <summary>
        /// Selected attributes from the related table to include in the parent table.
        /// </summary>
        public List<ExpandedLookupAttribute> Attributes { get; set; } = new();
    }

    /// <summary>
    /// Per-sub-column configuration for a lookup field's generated columns.
    /// Null values mean caller should use smart defaults.
    /// </summary>
    public class LookupSubColumnConfig
    {
        /// <summary>
        /// Logical name of the parent lookup attribute (e.g., "productid").
        /// </summary>
        public string LookupAttributeLogicalName { get; set; } = "";

        /// <summary>
        /// Include the ID (GUID) lookup column.
        /// </summary>
        public bool? IncludeIdField { get; set; }

        /// <summary>
        /// Hide the ID (GUID) lookup column in report field list.
        /// </summary>
        public bool? IdFieldHidden { get; set; }

        /// <summary>
        /// Include the generated name lookup column ({lookup}name).
        /// </summary>
        public bool? IncludeNameField { get; set; }

        /// <summary>
        /// Hide the generated name lookup column in report field list.
        /// </summary>
        public bool? NameFieldHidden { get; set; }

        /// <summary>
        /// Include the generated polymorphic type column ({lookup}type) for Owner/Customer.
        /// </summary>
        public bool? IncludeTypeField { get; set; }

        /// <summary>
        /// Hide the generated polymorphic type column in report field list.
        /// </summary>
        public bool? TypeFieldHidden { get; set; }

        /// <summary>
        /// Include the generated polymorphic yomi column ({lookup}yominame) for Owner/Customer.
        /// </summary>
        public bool? IncludeYomiField { get; set; }

        /// <summary>
        /// Hide the generated polymorphic yomi column in report field list.
        /// </summary>
        public bool? YomiFieldHidden { get; set; }
    }

    /// <summary>
    /// Per-sub-column configuration for a choice-style field's generated columns.
    /// Null values mean caller should use defaults.
    /// </summary>
    public class ChoiceSubColumnConfig
    {
        /// <summary>
        /// Logical name of the parent attribute (e.g., "statuscode").
        /// </summary>
        public string AttributeLogicalName { get; set; } = "";

        /// <summary>
        /// Include the numeric/raw value column for the attribute.
        /// </summary>
        public bool? IncludeValueField { get; set; }

        /// <summary>
        /// Hide the numeric/raw value column in report field list.
        /// </summary>
        public bool? ValueFieldHidden { get; set; }

        /// <summary>
        /// Include the generated label/name column ({attribute}name).
        /// </summary>
        public bool? IncludeLabelField { get; set; }

        /// <summary>
        /// Hide the generated label/name column in report field list.
        /// </summary>
        public bool? LabelFieldHidden { get; set; }
    }

    /// <summary>
    /// An attribute from a related table that is included via lookup expansion.
    /// </summary>
    public class ExpandedLookupAttribute
    {
        /// <summary>
        /// Logical name of the attribute in the related table.
        /// </summary>
        public string LogicalName { get; set; } = "";

        /// <summary>
        /// Display name of the attribute.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// The attribute type (String, Integer, Lookup, etc.).
        /// </summary>
        public string? AttributeType { get; set; }

        /// <summary>
        /// Schema name of the attribute.
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        /// For Lookup attributes, the target table logical names.
        /// </summary>
        public List<string>? Targets { get; set; }

        /// <summary>
        /// For Choice/Boolean attributes (TDS mode), the virtual attribute name containing the label text.
        /// </summary>
        public string? VirtualAttributeName { get; set; }

        /// <summary>
        /// For Choice attributes (FabricLink mode), whether the optionset is global.
        /// </summary>
        public bool? IsGlobal { get; set; }

        /// <summary>
        /// For Choice attributes (FabricLink mode), the optionset logical name.
        /// </summary>
        public string? OptionSetName { get; set; }

        /// <summary>
        /// Whether this expanded attribute is included in the semantic model output.
        /// </summary>
        public bool? IncludeInModel { get; set; }

        /// <summary>
        /// Whether this expanded attribute should be hidden in the report field list.
        /// </summary>
        public bool? IsHidden { get; set; }
    }

    #endregion
}
