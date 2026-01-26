using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DataverseMetadataExtractor.Models
{
    /// <summary>
    /// Container for all saved configurations
    /// </summary>
    public class ConfigurationsFile
    {
        public List<ConfigurationEntry> Configurations { get; set; } = new();
        public string? LastUsedConfigurationName { get; set; }
    }

    /// <summary>
    /// A named configuration with metadata
    /// </summary>
    public class ConfigurationEntry
    {
        public string Name { get; set; } = "Default";
        public DateTime LastUsed { get; set; } = DateTime.Now;
        public AppSettings Settings { get; set; } = new();
    }

    /// <summary>
    /// Defines the role of a table in a star-schema model
    /// </summary>
    public enum TableRole
    {
        Dimension,  // Default - dimension table (lookup target)
        Fact        // Fact table - the central table with measures
    }

    /// <summary>
    /// Represents a relationship/lookup between two tables in the star schema
    /// </summary>
    public class RelationshipConfig
    {
        public string SourceTable { get; set; } = "";      // The Fact or Dimension table (Many side)
        public string SourceAttribute { get; set; } = "";   // The lookup attribute on source
        public string TargetTable { get; set; } = "";       // The Dimension table (One side)
        public string? DisplayName { get; set; }            // Friendly name of the lookup
        public bool IsActive { get; set; } = true;          // Active relationship (only one active per table pair)
        public bool IsSnowflake { get; set; } = false;      // True if this is a Dimension->ParentDimension relationship
    }

    public class AppSettings
    {
        public string? LastEnvironmentUrl { get; set; }
        public string? LastSolution { get; set; }
        public List<string> SelectedTables { get; set; } = new();
        public Dictionary<string, string> TableForms { get; set; } = new();  // table -> selected form id
        public Dictionary<string, string> TableFormNames { get; set; } = new();  // table -> selected form name
        public Dictionary<string, string> TableViews { get; set; } = new();  // table -> selected view id
        public Dictionary<string, string> TableViewNames { get; set; } = new();  // table -> selected view name
        public Dictionary<string, List<string>> TableAttributes { get; set; } = new();  // table -> selected attributes
        public Dictionary<string, TableDisplayInfo> TableDisplayInfo { get; set; } = new();  // table -> display/schema names
        public Dictionary<string, Dictionary<string, AttributeDisplayInfo>> AttributeDisplayInfo { get; set; } = new();  // table -> attr -> display/schema
        public string? OutputFolder { get; set; }
        public string? ProjectName { get; set; }
        public string? WindowGeometry { get; set; }
        public bool AutoloadCache { get; set; } = true;
        public bool ShowAllAttributes { get; set; } = false;  // false = show selected, true = show all

        // Star-schema configuration
        public string? FactTable { get; set; }  // Logical name of the fact table (null if not set)
        public Dictionary<string, TableRole> TableRoles { get; set; } = new();  // table -> role
        public List<RelationshipConfig> Relationships { get; set; } = new();  // All configured relationships
    }

    public class TableDisplayInfo
    {
        [JsonIgnore]
        public string LogicalName { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? SchemaName { get; set; }
        public string? PrimaryIdAttribute { get; set; }
        public string? PrimaryNameAttribute { get; set; }
    }

    public class AttributeDisplayInfo
    {
        [JsonIgnore]
        public string LogicalName { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? SchemaName { get; set; }
        public string? AttributeType { get; set; }
        public bool IsRequired { get; set; } = false;
        public List<string>? Targets { get; set; }  // For Lookup fields - related table(s)
    }

    public class MetadataCache
    {
        public string? EnvironmentUrl { get; set; }
        public string? SolutionName { get; set; }
        public DateTime CachedDate { get; set; }
        public List<DataverseSolution> Solutions { get; set; } = new();
        public List<TableInfo> Tables { get; set; } = new();  // All solution tables
        public Dictionary<string, TableInfo> TableData { get; set; } = new();  // logical_name -> table metadata
        public Dictionary<string, List<FormMetadata>> TableForms { get; set; } = new();  // logical_name -> forms
        public Dictionary<string, List<ViewMetadata>> TableViews { get; set; } = new();  // logical_name -> views
        public Dictionary<string, List<AttributeMetadata>> TableAttributes { get; set; } = new();  // logical_name -> attributes

        public bool IsValid()
        {
            return CachedDate > DateTime.Now.AddHours(-24);
        }

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

    public class TableInfo
    {
        public string LogicalName { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? SchemaName { get; set; }
        public int ObjectTypeCode { get; set; }
        public string? PrimaryIdAttribute { get; set; }
        public string? PrimaryNameAttribute { get; set; }
        public string? MetadataId { get; set; }
    }

    public class DataverseSolution
    {
        public string SolutionId { get; set; } = "";
        public string UniqueName { get; set; } = "";
        public string FriendlyName { get; set; } = "";
        public string? Version { get; set; }
        public bool IsManaged { get; set; }
        public string? PublisherId { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class TableMetadata
    {
        public string LogicalName { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? SchemaName { get; set; }
        public string? PrimaryIdAttribute { get; set; }
        public string? PrimaryNameAttribute { get; set; }
    }

    public class AttributeMetadata
    {
        public string LogicalName { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? SchemaName { get; set; }
        public string? AttributeType { get; set; }
        public bool IsCustomAttribute { get; set; }
        public List<string>? Targets { get; set; }  // For Lookup fields - related table(s)
    }

    public class FormMetadata
    {
        public string FormId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? FormXml { get; set; }
        public List<string>? Fields { get; set; }
    }

    public class ViewMetadata
    {
        public string ViewId { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsDefault { get; set; }
        public string? FetchXml { get; set; }
        public List<string> Columns { get; set; } = new();
    }

    public class ExportMetadata
    {
        public string Environment { get; set; } = "";
        public string Solution { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string? FactTable { get; set; }  // Logical name of the fact table
        public List<ExportRelationship> Relationships { get; set; } = new();  // Star-schema relationships
        public List<ExportTable> Tables { get; set; } = new();
    }

    public class ExportRelationship
    {
        public string SourceTable { get; set; } = "";       // Fact or Dimension table (Many side)
        public string SourceAttribute { get; set; } = "";   // Lookup attribute
        public string TargetTable { get; set; } = "";       // Dimension table (One side)
        public string? DisplayName { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSnowflake { get; set; } = false;      // True if Dimension->ParentDimension
    }

    public class ExportTable
    {
        public string LogicalName { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? SchemaName { get; set; }
        public int ObjectTypeCode { get; set; }
        public string? PrimaryIdAttribute { get; set; }
        public string? PrimaryNameAttribute { get; set; }
        public string Role { get; set; } = "Dimension";  // "Fact" or "Dimension"
        public List<ExportForm> Forms { get; set; } = new();
        public ExportView? View { get; set; }
        public List<AttributeMetadata> Attributes { get; set; } = new();
    }

    public class ExportForm
    {
        public string FormId { get; set; } = "";
        public string FormName { get; set; } = "";
        public int FieldCount { get; set; }
    }

    public class ExportView
    {
        public string ViewId { get; set; } = "";
        public string ViewName { get; set; } = "";
        public string? FetchXml { get; set; }
    }
}
