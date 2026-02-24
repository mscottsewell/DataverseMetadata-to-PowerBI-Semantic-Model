// =============================================================================
// TestDataBuilders.cs - Fluent Test Data Builders
// =============================================================================
// Purpose: Provides fluent builder APIs for constructing realistic Dataverse
// metadata objects used in integration tests. Eliminates boilerplate when
// creating ExportTable, ExportRelationship, AttributeDisplayInfo, and
// DateTableConfig objects.
//
// Usage:
//   var table = new TableBuilder("account", "Account")
//       .WithAttribute("accountid", "Account Id", "Uniqueidentifier")
//       .WithAttribute("name", "Account Name", "String")
//       .WithLookup("_primarycontactid_value", "Primary Contact", "contact")
//       .WithPicklist("industrycode", "Industry", "industrycode")
//       .AsFact()
//       .Build();
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using DataverseToPowerBI.Core.Models;
using DataverseToPowerBI.XrmToolBox;
using XrmToolBoxModels = DataverseToPowerBI.XrmToolBox.Models;

// Type aliases to disambiguate XrmToolBox vs Core types
using XrmExportTable = DataverseToPowerBI.XrmToolBox.ExportTable;
using XrmExportView = DataverseToPowerBI.XrmToolBox.ExportView;
using XrmAttributeDisplayInfo = DataverseToPowerBI.XrmToolBox.AttributeDisplayInfo;
using CoreAttributeMetadata = DataverseToPowerBI.Core.Models.AttributeMetadata;

namespace DataverseToPowerBI.Tests
{
    /// <summary>
    /// Fluent builder for constructing <see cref="XrmExportTable"/> instances with
    /// associated <see cref="XrmAttributeDisplayInfo"/> metadata for testing.
    /// </summary>
    public class TableBuilder
    {
        private readonly string _logicalName;
        private readonly string _displayName;
        private string? _schemaName;
        private string _primaryId;
        private string? _primaryName;
        private string _role = "Dimension";
        private bool _hasStateCode;
        private int _objectTypeCode;
        private XrmExportView? _view;
        private readonly List<CoreAttributeMetadata> _attributes = new();
        private readonly Dictionary<string, XrmAttributeDisplayInfo> _displayInfo = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ExpandedLookupConfig> _expandedLookups = new();
#pragma warning disable CS0649 // Field is never assigned to (LookupSubColumnConfigs is reserved for future use)
        private Dictionary<string, LookupSubColumnConfig>? _lookupSubColumnConfigs;
#pragma warning restore CS0649

        private static int _objectTypeCounter = 10000;

        /// <summary>
        /// Creates a new table builder with the given logical and display names.
        /// Automatically adds a primary key attribute.
        /// </summary>
        public TableBuilder(string logicalName, string displayName)
        {
            _logicalName = logicalName;
            _displayName = displayName;
            _schemaName = ToPascalCase(logicalName);
            _primaryId = logicalName + "id";
            _primaryName = "name";
            _objectTypeCode = _objectTypeCounter++;

            // Auto-add primary key
            AddAttributeInternal(_primaryId, displayName + " Id", "Uniqueidentifier");
        }

        /// <summary>Adds a simple attribute (String, Integer, Decimal, Money, DateTime, etc.).</summary>
        public TableBuilder WithAttribute(string logicalName, string displayName, string attributeType)
        {
            AddAttributeInternal(logicalName, displayName, attributeType);
            return this;
        }

        /// <summary>Adds a Lookup attribute with target table(s).</summary>
        public TableBuilder WithLookup(string logicalName, string displayName, params string[] targets)
        {
            AddAttributeInternal(logicalName, displayName, "Lookup", targets: targets.ToList());
            return this;
        }

        /// <summary>Adds a Picklist (Choice) attribute with optionset metadata.</summary>
        public TableBuilder WithPicklist(string logicalName, string displayName, string optionSetName, bool isGlobal = false)
        {
            AddAttributeInternal(logicalName, displayName, "Picklist",
                virtualAttribute: logicalName + "name",
                optionSetName: optionSetName,
                isGlobal: isGlobal);
            return this;
        }

        /// <summary>Adds a Boolean attribute.</summary>
        public TableBuilder WithBoolean(string logicalName, string displayName)
        {
            AddAttributeInternal(logicalName, displayName, "Boolean",
                virtualAttribute: logicalName + "name");
            return this;
        }

        /// <summary>Adds a Status attribute (statecode + statuscode pair).</summary>
        public TableBuilder WithStatusFields()
        {
            _hasStateCode = true;
            AddAttributeInternal("statecode", "Status", "State",
                virtualAttribute: "statecodename");
            AddAttributeInternal("statuscode", "Status Reason", "Status",
                virtualAttribute: "statuscodename");
            return this;
        }

        /// <summary>Adds a DateTime attribute.</summary>
        public TableBuilder WithDateTime(string logicalName, string displayName)
        {
            AddAttributeInternal(logicalName, displayName, "DateTime");
            return this;
        }

        /// <summary>Adds a Money attribute.</summary>
        public TableBuilder WithMoney(string logicalName, string displayName)
        {
            AddAttributeInternal(logicalName, displayName, "Money");
            return this;
        }

        /// <summary>Sets the primary name attribute (defaults to "name").</summary>
        public TableBuilder WithPrimaryName(string logicalName, string displayName)
        {
            _primaryName = logicalName;
            // Add if not already present
            if (!_attributes.Any(a => a.LogicalName == logicalName))
                AddAttributeInternal(logicalName, displayName, "String");
            return this;
        }

        /// <summary>Sets the table role to Fact.</summary>
        public TableBuilder AsFact()
        {
            _role = "Fact";
            return this;
        }

        /// <summary>Sets the table role to Dimension (the default).</summary>
        public TableBuilder AsDimension()
        {
            _role = "Dimension";
            return this;
        }

        /// <summary>Adds a view with optional FetchXML filter.</summary>
        public TableBuilder WithView(string viewName, string? fetchXml = null)
        {
            _view = new XrmExportView
            {
                ViewId = Guid.NewGuid().ToString(),
                ViewName = viewName,
                FetchXml = fetchXml
            };
            return this;
        }

        /// <summary>Adds an expanded lookup configuration.</summary>
        public TableBuilder WithExpandedLookup(string lookupAttribute, string targetTable, string targetPrimaryKey, params (string logicalName, string displayName, string type)[] attributes)
        {
            var config = new ExpandedLookupConfig
            {
                LookupAttributeName = lookupAttribute,
                LookupDisplayName = lookupAttribute,
                TargetTableLogicalName = targetTable,
                TargetTableDisplayName = ToPascalCase(targetTable),
                TargetTablePrimaryKey = targetPrimaryKey,
                Attributes = attributes.Select(a => new ExpandedLookupAttribute
                {
                    LogicalName = a.logicalName,
                    DisplayName = a.displayName,
                    AttributeType = a.type,
                    SchemaName = ToPascalCase(a.logicalName)
                }).ToList()
            };
            _expandedLookups.Add(config);
            return this;
        }

        /// <summary>Sets a custom schema name (defaults to PascalCase of logical name).</summary>
        public TableBuilder WithSchemaName(string schemaName)
        {
            _schemaName = schemaName;
            return this;
        }

        /// <summary>Builds the <see cref="XrmExportTable"/>.</summary>
        public XrmExportTable Build()
        {
            return new XrmExportTable
            {
                LogicalName = _logicalName,
                DisplayName = _displayName,
                SchemaName = _schemaName,
                PrimaryIdAttribute = _primaryId,
                PrimaryNameAttribute = _primaryName,
                ObjectTypeCode = _objectTypeCode,
                Role = _role,
                HasStateCode = _hasStateCode,
                Attributes = new List<CoreAttributeMetadata>(_attributes),
                View = _view,
                ExpandedLookups = new List<ExpandedLookupConfig>(_expandedLookups),
                LookupSubColumnConfigs = _lookupSubColumnConfigs
            };
        }

        /// <summary>
        /// Gets the <see cref="XrmAttributeDisplayInfo"/> dictionary for this table,
        /// keyed by attribute logical name.
        /// </summary>
        public Dictionary<string, XrmAttributeDisplayInfo> BuildDisplayInfo()
        {
            return new Dictionary<string, XrmAttributeDisplayInfo>(_displayInfo, StringComparer.OrdinalIgnoreCase);
        }

        private void AddAttributeInternal(
            string logicalName, string displayName, string attributeType,
            List<string>? targets = null, string? virtualAttribute = null,
            string? optionSetName = null, bool? isGlobal = null)
        {
            // Avoid duplicates
            if (_attributes.Any(a => a.LogicalName == logicalName))
                return;

            var schemaName = ToPascalCase(logicalName);

            _attributes.Add(new CoreAttributeMetadata
            {
                LogicalName = logicalName,
                DisplayName = displayName,
                SchemaName = schemaName,
                AttributeType = attributeType,
                Targets = targets,
                VirtualAttributeName = virtualAttribute,
                IsGlobal = isGlobal,
                OptionSetName = optionSetName
            });

            _displayInfo[logicalName] = new XrmAttributeDisplayInfo
            {
                LogicalName = logicalName,
                DisplayName = displayName,
                SchemaName = schemaName,
                AttributeType = attributeType,
                Targets = targets,
                VirtualAttributeName = virtualAttribute,
                IsGlobal = isGlobal,
                OptionSetName = optionSetName
            };
        }

        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // Handle underscore-separated names
            var parts = input.Split('_');
            return string.Join("", parts.Select(p =>
                p.Length > 0 ? char.ToUpper(p[0]) + p.Substring(1) : p));
        }
    }

    /// <summary>
    /// Fluent builder for constructing <see cref="ExportRelationship"/> instances.
    /// </summary>
    public class RelationshipBuilder
    {
        private string _sourceTable = "";
        private string _sourceAttribute = "";
        private string _targetTable = "";
        private string? _displayName;
        private bool _isActive = true;
        private bool _isSnowflake;
        private int _snowflakeLevel;
        private bool _assumeReferentialIntegrity;

        /// <summary>Configures the many-side (source) of the relationship.</summary>
        public RelationshipBuilder From(string sourceTable, string sourceAttribute)
        {
            _sourceTable = sourceTable;
            _sourceAttribute = sourceAttribute;
            return this;
        }

        /// <summary>Configures the one-side (target) of the relationship.</summary>
        public RelationshipBuilder To(string targetTable)
        {
            _targetTable = targetTable;
            return this;
        }

        /// <summary>Sets the display name.</summary>
        public RelationshipBuilder Named(string displayName)
        {
            _displayName = displayName;
            return this;
        }

        /// <summary>Marks this relationship as inactive.</summary>
        public RelationshipBuilder Inactive()
        {
            _isActive = false;
            return this;
        }

        /// <summary>Marks this as a snowflake relationship at the specified depth.</summary>
        public RelationshipBuilder Snowflake(int level = 1)
        {
            _isSnowflake = true;
            _snowflakeLevel = level;
            return this;
        }

        /// <summary>Enables referential integrity assumption.</summary>
        public RelationshipBuilder WithReferentialIntegrity()
        {
            _assumeReferentialIntegrity = true;
            return this;
        }

        /// <summary>Builds the <see cref="ExportRelationship"/>.</summary>
        public ExportRelationship Build()        {
            return new ExportRelationship
            {
                SourceTable = _sourceTable,
                SourceAttribute = _sourceAttribute,
                TargetTable = _targetTable,
                DisplayName = _displayName,
                IsActive = _isActive,
                IsSnowflake = _isSnowflake,
                SnowflakeLevel = _snowflakeLevel,
                AssumeReferentialIntegrity = _assumeReferentialIntegrity
            };
        }
    }

    /// <summary>
    /// Fluent builder for constructing <see cref="DateTableConfig"/> instances.
    /// </summary>
    public class DateTableConfigBuilder
    {
        private string _primaryDateTable = "";
        private string _primaryDateField = "";
        private string _timeZoneId = "Eastern Standard Time";

        /// <summary>Sets the primary date table and field.</summary>
        public DateTableConfigBuilder ForTable(string tableName, string dateField)
        {
            _primaryDateTable = tableName;
            _primaryDateField = dateField;
            return this;
        }

        /// <summary>Sets the timezone (defaults to Eastern Standard Time).</summary>
        public DateTableConfigBuilder WithTimeZone(string timeZoneId)
        {
            _timeZoneId = timeZoneId;
            return this;
        }

        /// <summary>Builds the <see cref="DateTableConfig"/>.</summary>
        public DateTableConfig Build()
        {
            return new DateTableConfig
            {
                PrimaryDateTable = _primaryDateTable,
                PrimaryDateField = _primaryDateField,
                TimeZoneId = _timeZoneId
            };
        }
    }

    /// <summary>
    /// Helper to aggregate multiple <see cref="TableBuilder"/> results into
    /// the data structures needed by <see cref="SemanticModelBuilder.Build"/>.
    /// </summary>
    public class ScenarioBuilder
    {
        private readonly List<TableBuilder> _tableBuilders = new();
        private readonly List<RelationshipBuilder> _relationshipBuilders = new();
        private DateTableConfigBuilder? _dateTableConfigBuilder;
        private string _connectionType = "DataverseTDS";
        private string _dataverseUrl = "https://testorg.crm.dynamics.com";
        private string _semanticModelName = "TestModel";
        private string _storageMode = "DirectQuery";
        private bool _useDisplayNameAliases = true;
        private string? _fabricEndpoint;
        private string? _fabricDatabase;

        /// <summary>Adds a table to the scenario.</summary>
        public ScenarioBuilder WithTable(TableBuilder tableBuilder)
        {
            _tableBuilders.Add(tableBuilder);
            return this;
        }

        /// <summary>Adds a relationship to the scenario.</summary>
        public ScenarioBuilder WithRelationship(RelationshipBuilder relationshipBuilder)
        {
            _relationshipBuilders.Add(relationshipBuilder);
            return this;
        }

        /// <summary>Configures a date table.</summary>
        public ScenarioBuilder WithDateTable(DateTableConfigBuilder dateTableConfigBuilder)
        {
            _dateTableConfigBuilder = dateTableConfigBuilder;
            return this;
        }

        /// <summary>Sets the connection type to FabricLink.</summary>
        public ScenarioBuilder UseFabricLink(string endpoint = "test-endpoint.database.fabric.microsoft.com", string database = "TestLakehouse")
        {
            _connectionType = "FabricLink";
            _fabricEndpoint = endpoint;
            _fabricDatabase = database;
            return this;
        }

        /// <summary>Sets the connection type to DataverseTDS (default).</summary>
        public ScenarioBuilder UseDataverseTds()
        {
            _connectionType = "DataverseTDS";
            _fabricEndpoint = null;
            _fabricDatabase = null;
            return this;
        }

        /// <summary>Sets the Dataverse URL.</summary>
        public ScenarioBuilder WithDataverseUrl(string url)
        {
            _dataverseUrl = url;
            return this;
        }

        /// <summary>Sets the semantic model name.</summary>
        public ScenarioBuilder WithModelName(string name)
        {
            _semanticModelName = name;
            return this;
        }

        /// <summary>Sets the storage mode (DirectQuery or Import).</summary>
        public ScenarioBuilder WithStorageMode(string storageMode)
        {
            _storageMode = storageMode;
            return this;
        }

        /// <summary>Controls display name alias behavior.</summary>
        public ScenarioBuilder WithDisplayNameAliases(bool enabled)
        {
            _useDisplayNameAliases = enabled;
            return this;
        }

        /// <summary>Gets the connection type.</summary>
        public string ConnectionType => _connectionType;

        /// <summary>Gets the Dataverse URL.</summary>
        public string DataverseUrl => _dataverseUrl;

        /// <summary>Gets the semantic model name.</summary>
        public string SemanticModelName => _semanticModelName;

        /// <summary>Gets the storage mode.</summary>
        public string StorageMode => _storageMode;

        /// <summary>Gets whether display name aliases are enabled.</summary>
        public bool UseDisplayNameAliases => _useDisplayNameAliases;

        /// <summary>Gets the Fabric endpoint (FabricLink mode only).</summary>
        public string? FabricEndpoint => _fabricEndpoint;

        /// <summary>Gets the Fabric database (FabricLink mode only).</summary>
        public string? FabricDatabase => _fabricDatabase;

        /// <summary>Builds the list of <see cref="XrmExportTable"/> objects.</summary>
        public List<XrmExportTable> BuildTables()
        {
            return _tableBuilders.Select(b => b.Build()).ToList();
        }

        /// <summary>Builds the list of <see cref="ExportRelationship"/> objects.</summary>
        public List<ExportRelationship> BuildRelationships()
        {
            return _relationshipBuilders.Select(b => b.Build()).ToList();
        }

        /// <summary>
        /// Builds the aggregated attribute display info dictionary
        /// (outer key = table logical name, inner key = attribute logical name).
        /// </summary>
        public Dictionary<string, Dictionary<string, XrmAttributeDisplayInfo>> BuildAttributeDisplayInfo()
        {
            var result = new Dictionary<string, Dictionary<string, XrmAttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var builder in _tableBuilders)
            {
                var table = builder.Build();
                result[table.LogicalName!] = builder.BuildDisplayInfo();
            }
            return result;
        }

        /// <summary>Builds the optional <see cref="DateTableConfig"/>.</summary>
        public DateTableConfig? BuildDateTableConfig()
        {
            return _dateTableConfigBuilder?.Build();
        }
    }
}
