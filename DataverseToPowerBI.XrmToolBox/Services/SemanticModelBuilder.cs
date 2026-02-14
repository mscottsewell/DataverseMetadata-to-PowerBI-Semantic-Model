// ===================================================================================
// SemanticModelBuilder.cs - TMDL Generation for Power BI Semantic Models
// ===================================================================================
//
// PURPOSE:
// This class generates Power BI Semantic Model (PBIP) projects from Dataverse
// metadata. It creates TMDL (Tabular Model Definition Language) files that define
// tables, columns, relationships, and expressions for DirectQuery access to Dataverse.
//
// SUPPORTED CONNECTION MODES:
// - DataverseTDS: Uses CommonDataService.Database connector with native SQL queries
// - FabricLink: Uses Sql.Database connector against Fabric Lakehouse SQL endpoint
//
// OUTPUT STRUCTURE:
// {WorkingFolder}/
// └─ {EnvironmentName}/
//    └─ {ModelName}/
//       ├─ {ModelName}.pbip              - Power BI Project file
//       ├─ {ModelName}.SemanticModel/    - Semantic model folder
//       │  ├─ definition/
//       │  │  ├─ model.tmdl            - Model metadata and table/expression refs
//       │  │  ├─ expressions.tmdl      - FabricLink: FabricSQLEndpoint, FabricLakehouse
//       │  │  ├─ relationships.tmdl    - All relationship definitions
//       │  │  └─ tables/               - Individual table TMDL files
//       │  │     ├─ DataverseURL.tmdl  - Hidden parameter table (Enable Load, both TDS and FabricLink)
//       │  │     ├─ Date.tmdl (if configured)
//       │  │     └─ {TableName}.tmdl ...
//       │  └─ .platform                 - Fabric platform metadata
//       └─ {ModelName}.Report/            - Empty report folder
//
// KEY FEATURES:
// - DirectQuery partitions using native SQL against Dataverse TDS endpoint
// - Automatic relationship generation from lookup field metadata
// - Date dimension table generation with timezone support
// - FetchXML view filter translation to SQL WHERE clauses
// - Incremental update support with user measure preservation
// - Change analysis before applying updates
//
// TMDL FORMAT REQUIREMENTS:
// - UTF-8 encoding without BOM
// - CRLF line endings (Windows)
// - Tab indentation
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using XrmModels = DataverseToPowerBI.XrmToolBox.Models;
using DataverseToPowerBI.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace DataverseToPowerBI.XrmToolBox.Services
{
    /// <summary>
    /// Builds a Power BI Semantic Model (PBIP) from Dataverse metadata
    /// </summary>
    public class SemanticModelBuilder
    {
        private readonly string _templatePath;
        private readonly Action<string>? _statusCallback;
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        
        /// <summary>
        /// Corrections for virtual column names that don't match metadata or don't exist in TDS.
        /// Maps table.incorrectColumnName to the actual column name that exists in the endpoint.
        /// Key format: "tablename.columnname" (both lowercase for case-insensitive matching).
        /// </summary>
        private static readonly Dictionary<string, string> VirtualColumnCorrections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "contact.donotsendmmname", "donotsendmarketingmaterialname" },  // contact.donotsendmm virtual attribute correction
            { "account.donotsendmmname", "donotsendmarketingmaterialname" }  // account.donotsendmm virtual attribute correction
        };
        
        private readonly string _connectionType;
        private readonly string? _fabricLinkEndpoint;
        private readonly string? _fabricLinkDatabase;
        private readonly int _languageCode;
        private readonly bool _useDisplayNameAliasesInSql;
        private readonly string _storageMode;
        private Dictionary<string, string> _tableStorageModeOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        /// <summary>
        /// Whether this builder is configured for FabricLink (Lakehouse SQL) mode
        /// </summary>
        private bool IsFabricLink => _connectionType == "FabricLink";

        public SemanticModelBuilder(string templatePath, Action<string>? statusCallback = null,
            string connectionType = "DataverseTDS", string? fabricLinkEndpoint = null, string? fabricLinkDatabase = null,
            int languageCode = 1033, bool useDisplayNameAliasesInSql = true, string storageMode = "DirectQuery")
        {
            if (string.IsNullOrWhiteSpace(templatePath))
            {
                throw new ArgumentException("Template path is required.", nameof(templatePath));
            }

            _templatePath = templatePath;
            _statusCallback = statusCallback;
            _connectionType = connectionType ?? "DataverseTDS";
            _fabricLinkEndpoint = fabricLinkEndpoint;
            _fabricLinkDatabase = fabricLinkDatabase;
            _languageCode = languageCode;
            _useDisplayNameAliasesInSql = useDisplayNameAliasesInSql;
            _storageMode = storageMode ?? "DirectQuery";
        }

        /// <summary>
        /// Sets per-table storage mode overrides for DualSelect mode.
        /// Key = table logical name, Value = "directQuery" or "dual".
        /// </summary>
        public void SetTableStorageModeOverrides(Dictionary<string, string>? overrides)
        {
            _tableStorageModeOverrides = overrides != null
                ? new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the TMDL partition mode string for a table based on the global storage mode setting.
        /// DirectQuery: all tables use directQuery.
        /// Dual: fact table uses directQuery, dimensions use dual.
        /// DualSelect: fact uses directQuery, dimensions use per-table override (default directQuery).
        /// Import: all Dataverse/FabricLink tables use import.
        /// </summary>
        private string GetPartitionMode(string tableRole, string? tableLogicalName = null)
        {
            return _storageMode switch
            {
                "Import" => "import",
                "Dual" => tableRole == "Fact" ? "directQuery" : "dual",
                "DualSelect" => tableRole == "Fact" ? "directQuery" : GetDualSelectMode(tableLogicalName),
                _ => "directQuery"
            };
        }

        private string GetDualSelectMode(string? tableLogicalName)
        {
            if (tableLogicalName != null && _tableStorageModeOverrides.TryGetValue(tableLogicalName, out var mode))
                return mode;
            return "directQuery";
        }

        /// <summary>
        /// Returns true if user-context view filters (CURRENT_USER) should be stripped for this table.
        /// User context requires DirectQuery; it is not available in import or dual modes.
        /// </summary>
        private bool ShouldStripUserContext(string tableRole, string? tableLogicalName = null)
        {
            return _storageMode switch
            {
                "Import" => true,
                "Dual" => tableRole != "Fact",
                "DualSelect" => tableRole == "Fact" ? false : GetDualSelectMode(tableLogicalName) == "dual",
                _ => false
            };
        }

        /// <summary>
        /// Gets the effective display name for a column, considering overrides.
        /// Returns OverrideDisplayName if set, otherwise the standard DisplayName.
        /// </summary>
        private string GetEffectiveDisplayName(AttributeDisplayInfo? attrDisplayInfo, string fallbackDisplayName)
        {
            if (_useDisplayNameAliasesInSql && attrDisplayInfo?.OverrideDisplayName != null)
                return attrDisplayInfo.OverrideDisplayName;
            return fallbackDisplayName;
        }

        /// <summary>
        /// Wraps a SQL field expression with an AS [alias] clause when display name aliasing is enabled.
        /// For hidden columns (primary keys, lookup FK IDs), no alias is added.
        /// </summary>
        private string ApplySqlAlias(string sqlExpression, string displayName, string logicalName, bool isHidden)
        {
            if (!_useDisplayNameAliasesInSql || isHidden)
                return sqlExpression;
            
            // Only add alias if display name differs from logical name
            if (displayName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
                return sqlExpression;
            
            return $"{sqlExpression} AS [{displayName}]";
        }

        private void SetStatus(string message)
        {
            _statusCallback?.Invoke(message);
            DebugLogger.Log($"[SemanticModelBuilder] {message}");
        }

        /// <summary>
        /// Extracts the environment name from a Dataverse URL
        /// Example: "portfolioshapingdev.crm.dynamics.com" returns "portfolioshapingdev"
        /// </summary>
        private static string ExtractEnvironmentName(string dataverseUrl)
        {
            if (string.IsNullOrEmpty(dataverseUrl))
                return "default";
            
            // Remove protocol if present
            var url = dataverseUrl.Replace("https://", "").Replace("http://", "");
            
            // Get first segment before dot
            var firstDot = url.IndexOf('.');
            if (firstDot > 0)
                return url.Substring(0, firstDot);
            
            return url;
        }

        /// <summary>
        /// Writes text to a file using UTF-8 without BOM encoding and CRLF line endings
        /// </summary>
        private static void WriteTmdlFile(string path, string content)
        {
            // Ensure CRLF line endings (Power BI Desktop standard)
            content = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(path, content, Utf8WithoutBom);
        }

        /// <summary>
        /// Writes the DataverseURL parameter table TMDL file.
        /// This is a hidden table with mode: import partition that acts as a Power Query parameter.
        /// The table must have Enable Load checked (which is the default for tables) — without it,
        /// PBI Desktop throws KeyNotFoundException during CommonDataService.Database refresh.
        /// </summary>
        private string GenerateDataverseUrlTableTmdl(string normalizedUrl)
        {
            var sb = new StringBuilder();
            sb.AppendLine("table DataverseURL");
            sb.AppendLine("\tisHidden");
            sb.AppendLine("\tlineageTag: " + Guid.NewGuid().ToString());
            sb.AppendLine();
            sb.AppendLine("\tcolumn DataverseURL");
            sb.AppendLine("\t\tdataType: string");
            sb.AppendLine("\t\tisHidden");
            sb.AppendLine("\t\tlineageTag: " + Guid.NewGuid().ToString());
            sb.AppendLine("\t\tsummarizeBy: none");
            sb.AppendLine("\t\tsourceColumn: DataverseURL");
            sb.AppendLine();
            sb.AppendLine("\t\tchangedProperty = IsHidden");
            sb.AppendLine();
            sb.AppendLine("\t\tannotation SummarizationSetBy = Automatic");
            sb.AppendLine();
            sb.AppendLine("\tpartition DataverseURL = m");
            sb.AppendLine("\t\tmode: import");
            sb.AppendLine($"\t\tsource = \"{normalizedUrl}\" meta [IsParameterQuery=true, Type=\"Any\", IsParameterQueryRequired=true]");
            sb.AppendLine();
            sb.AppendLine("\tchangedProperty = IsHidden");
            sb.AppendLine();
            sb.AppendLine("\tannotation PBI_NavigationStepName = Navigation");
            sb.AppendLine();
            sb.AppendLine("\tannotation PBI_ResultType = Text");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Writes the DataverseURL parameter table TMDL file.
        /// </summary>
        private void WriteDataverseUrlTable(string path, string normalizedUrl)
        {
            // Ensure the parent directory exists (tables/ may not exist yet during initial build)
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            WriteTmdlFile(path, GenerateDataverseUrlTableTmdl(normalizedUrl));
            DebugLogger.Log($"Generated DataverseURL parameter table: {normalizedUrl}");
        }

        /// <summary>
        /// Builds the semantic model in the specified output folder
        /// </summary>
        public void Build(
            string semanticModelName,
            string outputFolder,
            string dataverseUrl,
            List<ExportTable> tables,
            List<ExportRelationship> relationships,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            DateTableConfig? dateTableConfig = null)
        {
            SetStatus("Starting semantic model build...");

            // Extract environment name from Dataverse URL
            var environmentName = ExtractEnvironmentName(dataverseUrl);

            // Determine the PBIP folder path - inside environment and semantic model subfolders
            var pbipFolder = Path.Combine(outputFolder, environmentName, semanticModelName);
            var projectName = semanticModelName;
            var pbipFilePath = Path.Combine(pbipFolder, $"{projectName}.pbip");

            // Always start fresh to avoid corruption
            SetStatus("Preparing project folder...");
            if (Directory.Exists(pbipFolder))
            {
                Directory.Delete(pbipFolder, true);
            }
            
            SetStatus("Copying PBIP template...");
            CopyTemplate(pbipFolder, projectName);

            // Update project name and DataverseURL
            SetStatus("Updating project configuration...");
            UpdateProjectConfiguration(pbipFolder, projectName, dataverseUrl);

            // Build a dictionary of lookup columns needed for relationships per source table
            var relationshipColumnsPerTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in relationships)
            {
                if (!relationshipColumnsPerTable.ContainsKey(rel.SourceTable))
                    relationshipColumnsPerTable[rel.SourceTable] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                relationshipColumnsPerTable[rel.SourceTable].Add(rel.SourceAttribute);
            }

            // Build tables
            SetStatus($"Building {tables.Count} tables...");
            var tablesFolder = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "tables");
            
            // Clear any existing tables from template (except Date table if present)
            if (Directory.Exists(tablesFolder))
            {
                var existingDateTable = FindExistingDateTable(tablesFolder);
                var filesToDelete = Directory.GetFiles(tablesFolder, "*.tmdl")
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f);
                        // Preserve Date table
                        if (existingDateTable != null && fileName.Equals(existingDateTable, StringComparison.OrdinalIgnoreCase))
                            return false;
                        // Preserve DataverseURL parameter table (TDS)
                        if (fileName.Equals("DataverseURL.tmdl", StringComparison.OrdinalIgnoreCase))
                            return false;
                        return true;
                    })
                    .ToList();
                
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                    DebugLogger.Log($"Deleted template table: {Path.GetFileName(file)}");
                }
            }
            
            Directory.CreateDirectory(tablesFolder);

            foreach (var table in tables)
            {
                SetStatus($"Building table: {table.DisplayName}...");
                var requiredLookupColumns = relationshipColumnsPerTable.ContainsKey(table.LogicalName)
                    ? relationshipColumnsPerTable[table.LogicalName]
                    : new HashSet<string>();
                var tableTmdl = GenerateTableTmdl(table, attributeDisplayInfo, requiredLookupColumns, dateTableConfig, outputFolder);
                var tableFileName = SanitizeFileName(table.DisplayName ?? table.SchemaName ?? table.LogicalName) + ".tmdl";
                WriteTmdlFile(Path.Combine(tablesFolder, tableFileName), tableTmdl);
            }

            // Build Date table if configured (only if model doesn't already have a date table)
            if (dateTableConfig != null)
            {
                var existingDateTable = FindExistingDateTable(tablesFolder);
                if (existingDateTable != null)
                {
                    SetStatus($"Date table '{existingDateTable}' already exists in template - preserving it");
                    DebugLogger.Log($"Skipping Date table generation - found existing date table: {existingDateTable}");
                }
                else
                {
                    SetStatus("Building Date table...");
                    var dateTableTmdl = GenerateDateTableTmdl(dateTableConfig);
                    WriteTmdlFile(Path.Combine(tablesFolder, "Date.tmdl"), dateTableTmdl);
                }
            }

            // Build relationships
            var definitionFolder = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition");
            var relationshipsPath = Path.Combine(definitionFolder, "relationships.tmdl");
            
            if (relationships.Any() || dateTableConfig != null)
            {
                SetStatus($"Building {relationships.Count} relationships...");
                var relationshipsTmdl = GenerateRelationshipsTmdl(tables, relationships, attributeDisplayInfo, dateTableConfig);
                WriteTmdlFile(relationshipsPath, relationshipsTmdl);
            }
            else if (File.Exists(relationshipsPath))
            {
                // Remove relationships file if it exists but no relationships are defined
                SetStatus("Removing relationships file (no relationships defined)...");
                File.Delete(relationshipsPath);
            }

            // Update model.tmdl with table references
            SetStatus("Updating model configuration...");
            // Check if there's an existing date table OR if we created one
            var hasDateTable = FindExistingDateTable(tablesFolder) != null || dateTableConfig != null;
            UpdateModelTmdl(pbipFolder, projectName, tables, hasDateTable);

            // Verify critical files exist
            VerifyPbipStructure(pbipFolder, projectName);

            SetStatus("Semantic model build complete!");
        }

        /// <summary>
        /// Analyzes what changes would be made without applying them
        /// </summary>
        public List<SemanticModelChange> AnalyzeChanges(
            string semanticModelName,
            string outputFolder,
            string dataverseUrl,
            List<ExportTable> tables,
            List<ExportRelationship> relationships,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            DateTableConfig? dateTableConfig = null)
        {
            SetStatus("Analyzing changes...");

            // Extract environment name from Dataverse URL
            var environmentName = ExtractEnvironmentName(dataverseUrl);

            var pbipFolder = Path.Combine(outputFolder, environmentName, semanticModelName);
            var projectName = semanticModelName;
            bool pbipExists = Directory.Exists(pbipFolder);

            var changes = new List<SemanticModelChange>();

            // If the folder exists, validate structural integrity before attempting incremental analysis
            bool requiresFullRebuild = false;
            if (pbipExists)
            {
                var missingElements = ValidateModelIntegrity(pbipFolder, projectName);
                if (missingElements.Count > 0)
                {
                    requiresFullRebuild = true;

                    changes.Add(new SemanticModelChange
                    {
                        ChangeType = ChangeType.Warning,
                        ObjectType = "Integrity",
                        ObjectName = "Model Structure",
                        Description = $"Incomplete PBIP structure detected — {missingElements.Count} missing element(s). A full rebuild is recommended."
                    });

                    foreach (var missing in missingElements)
                    {
                        changes.Add(new SemanticModelChange
                        {
                            ChangeType = ChangeType.Warning,
                            ObjectType = "Missing",
                            ObjectName = missing,
                            Description = "Required file or folder is missing"
                        });
                    }
                }
            }

            if (!pbipExists || requiresFullRebuild)
            {
                // First time or damaged structure - create everything
                changes.Add(new SemanticModelChange
                {
                    ChangeType = ChangeType.New,
                    ObjectType = "Project",
                    ObjectName = projectName,
                    Description = requiresFullRebuild
                        ? "Full rebuild of Power BI project (missing structural files)"
                        : $"Create new Power BI project from template (storage: {_storageMode})"
                });

                foreach (var table in tables)
                {
                    changes.Add(new SemanticModelChange
                    {
                        ChangeType = ChangeType.New,
                        ObjectType = "Table",
                        ObjectName = table.DisplayName ?? table.LogicalName,
                        Description = $"Create table with {table.Attributes?.Count ?? 0} columns"
                    });
                }

                foreach (var rel in relationships)
                {
                    changes.Add(new SemanticModelChange
                    {
                        ChangeType = ChangeType.New,
                        ObjectType = "Relationship",
                        ObjectName = $"{rel.SourceTable} → {rel.TargetTable}",
                        Description = $"via {rel.SourceAttribute}"
                    });
                }
            }
            else
            {
                // PBIP exists - analyze incremental changes with deep comparison
                var tablesFolder = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "tables");

                // Detect storage mode change by reading an existing table TMDL
                if (Directory.Exists(tablesFolder))
                {
                    var existingMode = DetectExistingStorageMode(tablesFolder);
                    if (existingMode != null && !existingMode.Equals(_storageMode, StringComparison.OrdinalIgnoreCase))
                    {
                        // Normalize for comparison: existing file says "directQuery"/"import"/"dual", config says "DirectQuery"/"Import"/"Dual"
                        changes.Add(new SemanticModelChange
                        {
                            ChangeType = ChangeType.Warning,
                            ObjectType = "StorageMode",
                            ObjectName = "Storage Mode Change",
                            Description = $"Changing from {existingMode} to {_storageMode} — cache.abf will be deleted to prevent stale data"
                        });
                    }
                }

                // Build relationship columns lookup
                var relationshipColumnsPerTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var rel in relationships)
                {
                    if (!relationshipColumnsPerTable.ContainsKey(rel.SourceTable))
                        relationshipColumnsPerTable[rel.SourceTable] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    relationshipColumnsPerTable[rel.SourceTable].Add(rel.SourceAttribute);
                }

                if (Directory.Exists(tablesFolder))
                {
                    var existingTables = Directory.GetFiles(tablesFolder, "*.tmdl")
                        .Select(f => Path.GetFileNameWithoutExtension(f))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var metadataTables = tables.Select(t => SanitizeFileName(t.DisplayName ?? t.SchemaName ?? t.LogicalName))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // Analyze each table for actual changes
                    foreach (var table in tables)
                    {
                        var fileName = SanitizeFileName(table.DisplayName ?? table.SchemaName ?? table.LogicalName);
                        var tmdlPath = Path.Combine(tablesFolder, fileName + ".tmdl");
                        
                        if (!existingTables.Contains(fileName))
                        {
                            // New table
                            changes.Add(new SemanticModelChange
                            {
                                ChangeType = ChangeType.New,
                                ObjectType = "Table",
                                ObjectName = table.DisplayName ?? table.LogicalName,
                                Description = $"Create new table with {table.Attributes?.Count ?? 0} columns"
                            });
                        }
                        else
                        {
                            // Table exists - deep comparison
                            var requiredLookupColumns = relationshipColumnsPerTable.ContainsKey(table.LogicalName)
                                ? relationshipColumnsPerTable[table.LogicalName]
                                : new HashSet<string>();

                            var tableChanges = AnalyzeTableChanges(tmdlPath, table, attributeDisplayInfo, requiredLookupColumns, dateTableConfig);

                            // Check for user measures to preserve
                            var userMeasures = ExtractUserMeasures(tmdlPath, table);
                            if (userMeasures.Count > 0)
                            {
                                changes.Add(new SemanticModelChange
                                {
                                    ChangeType = ChangeType.Preserve,
                                    ObjectType = "Measures",
                                    ObjectName = table.DisplayName ?? table.LogicalName,
                                    Description = $"Preserve {userMeasures.Count} user-created measure(s): {string.Join(", ", userMeasures.Take(3))}{(userMeasures.Count > 3 ? "..." : "")}"
                                });
                            }

                            // Add table changes if any
                            if (tableChanges.HasChanges)
                            {
                                var changeDetails = new List<string>();
                                if (tableChanges.QueryChanged)
                                {
                                    // Use detailed description if available, otherwise generic "query"
                                    var queryDesc = !string.IsNullOrEmpty(tableChanges.QueryChangeDetail) 
                                        ? tableChanges.QueryChangeDetail 
                                        : "query";
                                    changeDetails.Add(queryDesc);
                                }
                                if (tableChanges.NewColumns.Count > 0) changeDetails.Add($"{tableChanges.NewColumns.Count} new column(s)");
                                if (tableChanges.ModifiedColumns.Count > 0) changeDetails.Add($"{tableChanges.ModifiedColumns.Count} modified column(s)");
                                if (tableChanges.RemovedColumns.Count > 0) changeDetails.Add($"{tableChanges.RemovedColumns.Count} removed column(s)");

                                changes.Add(new SemanticModelChange
                                {
                                    ChangeType = ChangeType.Update,
                                    ObjectType = "Table",
                                    ObjectName = table.DisplayName ?? table.LogicalName,
                                    Description = $"Update: {string.Join(", ", changeDetails)}"
                                });

                                // Add detailed changes
                                foreach (var col in tableChanges.NewColumns)
                                {
                                    changes.Add(new SemanticModelChange
                                    {
                                        ChangeType = ChangeType.New,
                                        ObjectType = "Column",
                                        ObjectName = $"{table.DisplayName}.{col}",
                                        Description = "New column"
                                    });
                                }

                                foreach (var kvp in tableChanges.ModifiedColumns)
                                {
                                    changes.Add(new SemanticModelChange
                                    {
                                        ChangeType = ChangeType.Update,
                                        ObjectType = "Column",
                                        ObjectName = $"{table.DisplayName}.{kvp.Key}",
                                        Description = $"Changed: {kvp.Value}"
                                    });
                                }
                            }
                            else
                            {
                                // No changes - preserve as-is
                                changes.Add(new SemanticModelChange
                                {
                                    ChangeType = ChangeType.Preserve,
                                    ObjectType = "Table",
                                    ObjectName = table.DisplayName ?? table.LogicalName,
                                    Description = "No changes detected"
                                });
                            }
                        }
                    }

                    // Warn about orphaned tables (exclude generated tables like Date/DateAutoTemplate/DataverseURL)
                    var generatedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Date", "DateAutoTemplate", "DataverseURL" };
                    var orphanedTables = existingTables
                        .Except(metadataTables, StringComparer.OrdinalIgnoreCase)
                        .Where(t => !generatedTables.Contains(t))
                        .ToList();
                    
                    foreach (var orphan in orphanedTables)
                    {
                        changes.Add(new SemanticModelChange
                        {
                            ChangeType = ChangeType.Warning,
                            ObjectType = "Table",
                            ObjectName = orphan,
                            Description = "Exists in PBIP but not in Dataverse metadata (will be kept as-is)"
                        });
                    }
                }

                // Check for relationship changes
                var relationshipChanges = AnalyzeRelationshipChanges(pbipFolder, projectName, relationships, tables, attributeDisplayInfo, dateTableConfig);
                if (relationshipChanges.HasChanges)
                {
                    var relDetails = new List<string>();
                    if (relationshipChanges.NewRelationships.Count > 0) relDetails.Add($"{relationshipChanges.NewRelationships.Count} new");
                    if (relationshipChanges.ModifiedRelationships.Count > 0) relDetails.Add($"{relationshipChanges.ModifiedRelationships.Count} modified");
                    if (relationshipChanges.RemovedRelationships.Count > 0) relDetails.Add($"{relationshipChanges.RemovedRelationships.Count} removed");

                    changes.Add(new SemanticModelChange
                    {
                        ChangeType = ChangeType.Update,
                        ObjectType = "Relationships",
                        ObjectName = "Relationships",
                        Description = $"Update: {string.Join(", ", relDetails)}"
                    });
                }
                else
                {
                    changes.Add(new SemanticModelChange
                    {
                        ChangeType = ChangeType.Preserve,
                        ObjectType = "Relationships",
                        ObjectName = "All",
                        Description = "No changes detected"
                    });
                }

                // Check data source expression changes
                if (IsFabricLink)
                {
                    // FabricLink: check FabricSQLEndpoint and FabricLakehouse expressions
                    var currentEndpoint = ExtractExpression(pbipFolder, projectName, "FabricSQLEndpoint");
                    var currentDatabase = ExtractExpression(pbipFolder, projectName, "FabricLakehouse");
                    var expectedEndpoint = _fabricLinkEndpoint ?? "";
                    var expectedDatabase = _fabricLinkDatabase ?? "";
                    
                    var endpointChanged = !string.Equals(currentEndpoint, expectedEndpoint, StringComparison.OrdinalIgnoreCase);
                    var databaseChanged = !string.Equals(currentDatabase, expectedDatabase, StringComparison.OrdinalIgnoreCase);
                    
                    if (endpointChanged || databaseChanged)
                    {
                        var details = new List<string>();
                        if (endpointChanged) details.Add($"Endpoint: {currentEndpoint} → {expectedEndpoint}");
                        if (databaseChanged) details.Add($"Database: {currentDatabase} → {expectedDatabase}");
                        
                        changes.Add(new SemanticModelChange
                        {
                            ChangeType = ChangeType.Update,
                            ObjectType = "FabricLink",
                            ObjectName = "Expressions",
                            Description = $"Update: {string.Join(", ", details)}"
                        });
                    }
                    else
                    {
                        changes.Add(new SemanticModelChange
                        {
                            ChangeType = ChangeType.Preserve,
                            ObjectType = "FabricLink",
                            ObjectName = "Expressions",
                            Description = "No changes detected"
                        });
                    }
                }

                // Check DataverseURL table (both TDS and FabricLink)
                var currentUrl = ExtractDataverseUrl(pbipFolder, projectName);
                
                // Normalize the dataverseUrl for comparison (remove https:// prefix if present)
                var normalizedDataverseUrl = dataverseUrl;
                if (normalizedDataverseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    normalizedDataverseUrl = normalizedDataverseUrl.Substring(8);
                
                if (!string.Equals(currentUrl, normalizedDataverseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add(new SemanticModelChange
                    {
                        ChangeType = ChangeType.Update,
                        ObjectType = "DataverseURL",
                        ObjectName = "Table",
                        Description = $"Update: {currentUrl} → {normalizedDataverseUrl}"
                    });
                }
                else
                {
                    changes.Add(new SemanticModelChange
                    {
                        ChangeType = ChangeType.Preserve,
                        ObjectType = "DataverseURL",
                        ObjectName = "Table",
                        Description = "No changes detected"
                    });
                }
            }

            // Summarize
            var summary = changes.GroupBy(c => c.ChangeType).ToDictionary(g => g.Key, g => g.Count());
            var totalChanges = summary.Where(kvp => kvp.Key == ChangeType.New || kvp.Key == ChangeType.Update).Sum(kvp => kvp.Value);
            
            if (totalChanges == 0)
            {
                SetStatus("No changes detected - model is up to date");
            }
            else
            {
                SetStatus($"Detected {totalChanges} change(s) requiring update");
            }

            return changes;
        }

        /// <summary>
        /// Deeply analyzes a table for actual changes
        /// </summary>
        private TableChangeAnalysis AnalyzeTableChanges(
            string tmdlPath,
            ExportTable table,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            HashSet<string> requiredLookupColumns,
            DateTableConfig? dateTableConfig = null)
        {
            var analysis = new TableChangeAnalysis();

            if (!File.Exists(tmdlPath))
            {
                analysis.QueryChanged = true; // File doesn't exist, treat as all changes
                return analysis;
            }

            try
            {
                var existingContent = File.ReadAllText(tmdlPath);

                // Parse existing columns
                var existingColumns = ParseExistingColumns(existingContent);

                // Generate what the new columns should be (pass existing to preserve lookup column types)
                var newColumns = GenerateExpectedColumns(table, attributeDisplayInfo, requiredLookupColumns, existingColumns, dateTableConfig);

                // DEBUG: Log column comparison for Allocation
                if (table.LogicalName == "cai_allocation")
                {
                    DebugLogger.Log($"Allocation column comparison:");
                    DebugLogger.Log($"  Existing ({existingColumns.Count}): {string.Join(", ", existingColumns.Keys.Take(5))}...");
                    DebugLogger.Log($"  Expected ({newColumns.Count}): {string.Join(", ", newColumns.Keys.Take(5))}...");
                }

                // Compare columns
                foreach (var newCol in newColumns)
                {
                    if (!existingColumns.ContainsKey(newCol.Key))
                    {
                        analysis.NewColumns.Add(newCol.Key);
                    }
                    else
                    {
                        var existing = existingColumns[newCol.Key];
                        var diffs = CompareColumnDefinitions(existing, newCol.Value);
                        if (diffs.Count > 0)
                        {
                            analysis.ModifiedColumns[newCol.Key] = string.Join(", ", diffs);
                        }
                    }
                }

                // Check for removed columns (only Dataverse columns, not user-added)
                foreach (var existingCol in existingColumns)
                {
                    if (existingCol.Value.LogicalName != null && !newColumns.ContainsKey(existingCol.Key))
                    {
                        analysis.RemovedColumns.Add(existingCol.Key);
                        DebugLogger.Log($"Removed column detected in {table.DisplayName ?? table.LogicalName}: {existingCol.Key} (logical: {existingCol.Value.LogicalName})");
                    }
                }

                // Compare M query
                var existingQuery = ExtractMQuery(existingContent);
                var requiredLookupCols = requiredLookupColumns ?? new HashSet<string>();
                var newQuery = GenerateMQuery(table, requiredLookupCols, dateTableConfig, attributeDisplayInfo);
                
                // DEBUG: Log query comparison
                DebugLogger.Log($"Query comparison for {table.DisplayName ?? table.LogicalName}:");
                DebugLogger.Log($"  View: {table.View?.ViewName ?? "(none)"}");
                DebugLogger.Log($"  Has FetchXML: {!string.IsNullOrWhiteSpace(table.View?.FetchXml)}");
                
                // Only flag as changed if queries actually differ
                if (!string.IsNullOrEmpty(existingQuery) && !string.IsNullOrEmpty(newQuery))
                {
                    if (!CompareQueries(existingQuery, newQuery))
                    {
                        analysis.QueryChanged = true;
                        
                        // Try to determine what specifically changed
                        var changeDetails = new List<string>();
                        
                        // Extract column parts for comparison
                        var existingSelectMatch = Regex.Match(existingQuery, @"SELECT(.+?)FROM", RegexOptions.Singleline);
                        var newSelectMatch = Regex.Match(newQuery, @"SELECT(.+?)FROM", RegexOptions.Singleline);
                        
                        if (existingSelectMatch.Success && newSelectMatch.Success)
                        {
                            var existingCols = existingSelectMatch.Groups[1].Value;
                            var newCols = newSelectMatch.Groups[1].Value;
                            if (existingCols != newCols)
                            {
                                changeDetails.Add("columns changed");
                            }
                        }
                        
                        // Extract WHERE parts for comparison
                        var existingWhereMatch = Regex.Match(existingQuery, @"WHERE(.+)$", RegexOptions.Singleline);
                        var newWhereMatch = Regex.Match(newQuery, @"WHERE(.+)$", RegexOptions.Singleline);
                        
                        var existingHasWhere = existingWhereMatch.Success && !string.IsNullOrWhiteSpace(existingWhereMatch.Groups[1].Value);
                        var newHasWhere = newWhereMatch.Success && !string.IsNullOrWhiteSpace(newWhereMatch.Groups[1].Value);
                        
                        if (existingHasWhere != newHasWhere)
                        {
                            changeDetails.Add(newHasWhere ? "filter added" : "filter removed");
                        }
                        else if (existingHasWhere && newHasWhere)
                        {
                            if (existingWhereMatch.Groups[1].Value != newWhereMatch.Groups[1].Value)
                            {
                                changeDetails.Add("filter modified");
                            }
                        }
                        
                        analysis.QueryChangeDetail = changeDetails.Count > 0 
                            ? string.Join(", ", changeDetails) 
                            : "query structure changed";
                        
                        DebugLogger.Log($"  ✗ Query CHANGED: {analysis.QueryChangeDetail}");
                        DebugLogger.Log($"  Existing (normalized): {existingQuery.Substring(0, Math.Min(150, existingQuery.Length))}");
                        DebugLogger.Log($"  Expected (normalized): {newQuery.Substring(0, Math.Min(150, newQuery.Length))}");
                    }
                    else
                    {
                        DebugLogger.Log($"  ✓ Query unchanged");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Warning: Could not analyze table changes for {tmdlPath}: {ex.Message}");
                analysis.QueryChanged = true; // Assume changes if we can't parse
                analysis.QueryChangeDetail = "unable to parse existing file";
            }

            return analysis;
        }

        /// <summary>
        /// Parses existing column definitions from TMDL
        /// </summary>
        private Dictionary<string, ColumnDefinition> ParseExistingColumns(string tmdlContent)
        {
            var columns = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
            
            // Match column blocks with optional /// comment
            // Pattern captures: comment, display name (quoted or unquoted), and properties block
            var columnPattern = @"(?:///\s*([^\r\n]+)\r?\n)?\s*column\s+(?:'([^']+)'|""([^""]+)""|([^\r\n]+))\r?\n((?:\t[^\r\n]+\r?\n)+)";
            var matches = Regex.Matches(tmdlContent, columnPattern, RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                var logicalName = match.Groups[1].Success ? match.Groups[1].Value.Trim() : null;
                var displayName = match.Groups[2].Success ? match.Groups[2].Value :
                                 match.Groups[3].Success ? match.Groups[3].Value :
                                 match.Groups[4].Value.Trim();
                var properties = match.Groups[5].Value;

                // Parse properties - dataType is the word after "dataType:"
                var dataTypeMatch = Regex.Match(properties, @"\bdataType:\s*([^\r\n]+)");
                var sourceColumnMatch = Regex.Match(properties, @"\bsourceColumn:\s*([^\r\n]+)");
                var formatStringMatch = Regex.Match(properties, @"\bformatString:\s*([^\r\n]+)");

                columns[displayName] = new ColumnDefinition
                {
                    DisplayName = displayName,
                    LogicalName = logicalName,
                    DataType = dataTypeMatch.Success ? dataTypeMatch.Groups[1].Value.Trim() : null,
                    SourceColumn = sourceColumnMatch.Success ? sourceColumnMatch.Groups[1].Value.Trim() : null,
                    FormatString = formatStringMatch.Success ? formatStringMatch.Groups[1].Value.Trim() : null
                };
            }

            return columns;
        }

        /// <summary>
        /// Generates expected column definitions for comparison
        /// </summary>
        private Dictionary<string, ColumnDefinition> GenerateExpectedColumns(
            ExportTable table,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            HashSet<string> requiredLookupColumns,
            Dictionary<string, ColumnDefinition>? existingColumns = null,
            DateTableConfig? dateTableConfig = null)
        {
            var columns = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);

            var attrInfo = attributeDisplayInfo.ContainsKey(table.LogicalName)
                ? attributeDisplayInfo[table.LogicalName]
                : new Dictionary<string, AttributeDisplayInfo>();

            var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Include primary key (always uses logical name as display name when hidden)
            var primaryKey = table.PrimaryIdAttribute ?? table.LogicalName + "id";
            if (table.Attributes?.Any(a => a.LogicalName == primaryKey) == true)
            {
                var pkAttr = table.Attributes.First(a => a.LogicalName == primaryKey);
                
                var (dataType, formatString, _, _) = MapDataType(pkAttr.AttributeType);
                columns[pkAttr.LogicalName] = new ColumnDefinition
                {
                    DisplayName = pkAttr.LogicalName, // Primary key uses logical name
                    LogicalName = pkAttr.LogicalName,
                    DataType = dataType,
                    SourceColumn = pkAttr.LogicalName,
                    FormatString = formatString
                };
                processedColumns.Add(primaryKey);
            }

            // Process each attribute matching the actual generation logic
            if (table.Attributes != null)
            {
                foreach (var attr in table.Attributes)
                {
                    if (processedColumns.Contains(attr.LogicalName)) continue;

                    var attrDisplayInfo = attrInfo.ContainsKey(attr.LogicalName) ? attrInfo[attr.LogicalName] : null;
                    var attrType = attr.AttributeType ?? attrDisplayInfo?.AttributeType ?? "";
                    // CRITICAL: Match the same priority as TMDL generation code
                    var attrDisplayName = attr.DisplayName ?? attrDisplayInfo?.DisplayName ?? attr.SchemaName ?? attr.LogicalName;
                    var effectiveName = GetEffectiveDisplayName(attrDisplayInfo, attrDisplayName);
                    var targets = attr.Targets ?? attrDisplayInfo?.Targets;

                    // Skip statecode and special owning name columns (not available in TDS/Fabric endpoints)
                    if (attr.LogicalName.Equals("statecode", StringComparison.OrdinalIgnoreCase) ||
                        attr.LogicalName.Equals("owningusername", StringComparison.OrdinalIgnoreCase) ||
                        attr.LogicalName.Equals("owningteamname", StringComparison.OrdinalIgnoreCase) ||
                        attr.LogicalName.Equals("owningbusinessunitname", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var isLookup = attrType.Equals("Lookup", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("Owner", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("Customer", StringComparison.OrdinalIgnoreCase);
                    var isChoice = attrType.Equals("Picklist", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("State", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("Status", StringComparison.OrdinalIgnoreCase);
                    var isMultiSelectChoice = attrType.Equals("MultiSelectPicklist", StringComparison.OrdinalIgnoreCase);
                    var isBoolean = attrType.Equals("Boolean", StringComparison.OrdinalIgnoreCase);

                    if (isLookup)
                    {
                        // Hidden ID column
                        var (dataType, formatString, _, _) = MapDataType("lookup");
                        columns[attr.LogicalName] = new ColumnDefinition
                        {
                            DisplayName = attr.LogicalName,
                            LogicalName = attr.LogicalName,
                            DataType = dataType,
                            SourceColumn = attr.LogicalName,
                            FormatString = formatString
                        };

                        // Lookup name columns exist in both TDS and FabricLink
                        // Skip name column for owning* lookups (not available in TDS/Fabric endpoints)
                        var nameColumn = attr.LogicalName + "name";
                        var isOwningLookup = attr.LogicalName.Equals("owninguser", StringComparison.OrdinalIgnoreCase) ||
                                             attr.LogicalName.Equals("owningteam", StringComparison.OrdinalIgnoreCase) ||
                                             attr.LogicalName.Equals("owningbusinessunit", StringComparison.OrdinalIgnoreCase);
                        
                        if (!processedColumns.Contains(nameColumn) && !isOwningLookup)
                        {
                            var lookupSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                            columns[effectiveName] = new ColumnDefinition
                            {
                                DisplayName = effectiveName,
                                LogicalName = nameColumn,
                                DataType = "string",
                                SourceColumn = lookupSourceCol,
                                FormatString = null
                            };
                        }
                        processedColumns.Add(nameColumn);
                        processedColumns.Add(attr.LogicalName);
                    }
                    else if (isChoice || isBoolean)
                    {
                        if (IsFabricLink)
                        {
                            // FabricLink: JOINs to metadata tables produce a string label column
                            var nameColumn = attr.LogicalName + "name";
                            if (!processedColumns.Contains(nameColumn))
                            {
                                var fabricChoiceSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                                columns[effectiveName] = new ColumnDefinition
                                {
                                    DisplayName = effectiveName,
                                    LogicalName = nameColumn,
                                    DataType = "string",
                                    SourceColumn = fabricChoiceSourceCol,
                                    FormatString = null
                                };
                            }
                            processedColumns.Add(nameColumn);
                            processedColumns.Add(attr.LogicalName);
                        }
                        else
                        {
                            // TDS: use the virtual attribute name from metadata
                            var nameColumn = attrDisplayInfo?.VirtualAttributeName ?? (attr.LogicalName + "name");
                            
                            // Apply correction if this virtual column name needs fixing
                            var correctionKey = $"{table.LogicalName}.{nameColumn}";
                            if (VirtualColumnCorrections.TryGetValue(correctionKey, out var correctedName))
                            {
                                nameColumn = correctedName;
                            }
                            
                            if (!processedColumns.Contains(nameColumn))
                            {
                                var tdsChoiceSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                                columns[effectiveName] = new ColumnDefinition
                                {
                                    DisplayName = effectiveName,
                                    LogicalName = nameColumn,
                                    DataType = "string",
                                    SourceColumn = tdsChoiceSourceCol,
                                    FormatString = null
                                };
                            }
                            processedColumns.Add(nameColumn);
                            processedColumns.Add(attr.LogicalName);
                        }
                    }
                    else if (isMultiSelectChoice)
                    {
                        // Multi-select choice: produces a string label column
                        // FabricLink: uses {attributename}name pattern; TDS: uses actual VirtualAttributeName
                        var nameColumn = IsFabricLink ? (attr.LogicalName + "name") 
                                                       : (attrDisplayInfo?.VirtualAttributeName ?? (attr.LogicalName + "name"));
                        
                        // Apply correction if this virtual column name needs fixing
                        var correctionKey = $"{table.LogicalName}.{nameColumn}";
                        if (VirtualColumnCorrections.TryGetValue(correctionKey, out var correctedName))
                        {
                            nameColumn = correctedName;
                        }
                        
                        if (!processedColumns.Contains(nameColumn))
                        {
                            var msSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                            columns[effectiveName] = new ColumnDefinition
                            {
                                DisplayName = effectiveName,
                                LogicalName = nameColumn,
                                DataType = "string",
                                SourceColumn = msSourceCol,
                                FormatString = null
                            };
                        }
                        processedColumns.Add(nameColumn);
                        processedColumns.Add(attr.LogicalName);
                    }
                    else
                    {
                        // Regular column - check if it's a wrapped DateTime field
                        var isDateTime = attrType.Equals("DateTime", StringComparison.OrdinalIgnoreCase);
                        var shouldWrapDateTime = isDateTime && dateTableConfig != null &&
                            dateTableConfig.WrappedFields.Any(f =>
                                f.TableName.Equals(table.LogicalName, StringComparison.OrdinalIgnoreCase) &&
                                f.FieldName.Equals(attr.LogicalName, StringComparison.OrdinalIgnoreCase));

                        // If wrapping datetime, use dateonly format
                        var effectiveAttrType = shouldWrapDateTime ? "dateonly" : attr.AttributeType;
                        var (dataType, formatString, _, _) = MapDataType(effectiveAttrType);
                        
                        var isPrimaryKey = attr.LogicalName.Equals(table.PrimaryIdAttribute, StringComparison.OrdinalIgnoreCase);
                        var regularDisplayName = isPrimaryKey ? attr.LogicalName : effectiveName;
                        var regularSourceCol = isPrimaryKey ? attr.LogicalName : (_useDisplayNameAliasesInSql ? effectiveName : attr.LogicalName);
                        
                        columns[regularDisplayName] = new ColumnDefinition
                        {
                            DisplayName = regularDisplayName,
                            LogicalName = attr.LogicalName,
                            DataType = dataType,
                            SourceColumn = regularSourceCol,
                            FormatString = formatString
                        };
                        processedColumns.Add(attr.LogicalName);
                    }
                }
            }

            // Add any missing lookup columns from requiredLookupColumns that weren't in table.Attributes
            // These are needed for relationships but might not be selected as attributes (e.g., owninguser)
            // IMPORTANT: If column exists in TMDL, preserve its dataType/formatString to avoid false changes
            foreach (var lookupCol in requiredLookupColumns)
            {
                if (!processedColumns.Contains(lookupCol))
                {
                    // Check if this column exists in the TMDL
                    if (existingColumns != null && existingColumns.TryGetValue(lookupCol, out var existingCol))
                    {
                        // Use existing column definition to preserve dataType, formatString, etc.
                        columns[lookupCol] = existingCol;
                    }
                    else
                    {
                        // New column - use default lookup type
                        columns[lookupCol] = new ColumnDefinition
                        {
                            DisplayName = lookupCol,
                            LogicalName = lookupCol,
                            DataType = "int64",
                            SourceColumn = lookupCol,
                            FormatString = "0"
                        };
                    }
                    processedColumns.Add(lookupCol);
                }
            }

            return columns;
        }

        /// <summary>
        /// Compares two column definitions
        /// </summary>
        private List<string> CompareColumnDefinitions(ColumnDefinition existing, ColumnDefinition expected)
        {
            var diffs = new List<string>();

            if (existing.DataType != expected.DataType)
                diffs.Add($"dataType: {existing.DataType} → {expected.DataType}");

            if (existing.DisplayName != expected.DisplayName)
                diffs.Add($"displayName: {existing.DisplayName} → {expected.DisplayName}");

            if (existing.FormatString != expected.FormatString && 
                !(string.IsNullOrEmpty(existing.FormatString) && string.IsNullOrEmpty(expected.FormatString)))
                diffs.Add($"formatString changed");

            return diffs;
        }

        /// <summary>
        /// Extracts M query from partition section
        /// </summary>
        private string ExtractMQuery(string tmdlContent)
        {
            // Extract the SQL from inside Value.NativeQuery(Dataverse,"...SQL...")
            // The partition format spans multiple lines:
            //   Source = Value.NativeQuery(Dataverse,"
            //
            //       SELECT ... FROM ... WHERE ...
            //
            //   " ,null ,[EnableFolding=true])
            
            // Match from Value.NativeQuery to the closing quote before " ,null" (TDS pattern)
            var queryMatch = Regex.Match(tmdlContent, @"Value\.NativeQuery\([^,]+,\s*""(.*?)""", RegexOptions.Singleline);
            if (queryMatch.Success)
            {
                var sql = queryMatch.Groups[1].Value.Trim();
                return NormalizeQuery(sql);
            }
            
            // Also try FabricLink pattern: [Query="..."]
            var fabricQueryMatch = Regex.Match(tmdlContent, @"\[Query\s*=\s*""(.*?)""", RegexOptions.Singleline);
            if (fabricQueryMatch.Success)
            {
                var sql = fabricQueryMatch.Groups[1].Value.Trim();
                return NormalizeQuery(sql);
            }
            return string.Empty;
        }

        /// <summary>
        /// Generates expected M query for comparison - must exactly match actual GenerateTableTmdl logic
        /// </summary>
        private string GenerateMQuery(ExportTable table, HashSet<string> requiredLookupColumns, DateTableConfig? dateTableConfig = null,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>>? attributeDisplayInfo = null)
        {
            // FabricLink: table names are lowercase, no schema prefix
            // TDS: uses schemaName (e.g. Opportunity, Account)
            var fromTable = IsFabricLink ? table.LogicalName : (table.SchemaName ?? table.LogicalName);
            var sqlFields = new List<string>();
            var joinClauses = new List<string>(); // FabricLink: metadata table JOINs and OUTER APPLY
            var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Get attribute display info for this table
            var attrInfo = (attributeDisplayInfo != null && attributeDisplayInfo.ContainsKey(table.LogicalName))
                ? attributeDisplayInfo[table.LogicalName]
                : new Dictionary<string, AttributeDisplayInfo>();

            // CRITICAL: Must match GenerateTableTmdl logic exactly
            var primaryKey = table.PrimaryIdAttribute ?? table.LogicalName + "id";

            // Only add primary key first if NOT already in Attributes (matching GenerateTableTmdl)
            if (!table.Attributes.Any(a => a.LogicalName.Equals(primaryKey, StringComparison.OrdinalIgnoreCase)))
            {
                sqlFields.Add($"Base.{primaryKey}");
                processedColumns.Add(primaryKey);
            }

            // Add required lookup columns only if NOT in Attributes (matching GenerateTableTmdl)
            foreach (var lookupCol in requiredLookupColumns)
            {
                if (!table.Attributes.Any(a => a.LogicalName.Equals(lookupCol, StringComparison.OrdinalIgnoreCase)) && 
                    !processedColumns.Contains(lookupCol))
                {
                    sqlFields.Add($"Base.{lookupCol}");
                    processedColumns.Add(lookupCol);
                }
            }

            // Process attributes matching the exact logic in GenerateTableTmdl
            if (table.Attributes != null)
            {
                foreach (var attr in table.Attributes)
                {
                    if (processedColumns.Contains(attr.LogicalName)) continue;

                    var attrType = attr.AttributeType ?? "";
                    var attrDisplayInfo2 = attrInfo.ContainsKey(attr.LogicalName) ? attrInfo[attr.LogicalName] : null;
                    var attrDisplayName = attr.DisplayName ?? attrDisplayInfo2?.DisplayName ?? attr.SchemaName ?? attr.LogicalName;
                    var effectiveName = GetEffectiveDisplayName(attrDisplayInfo2, attrDisplayName);

                    // Skip statecode and special owning name columns (not available in TDS/Fabric endpoints)
                    if (attr.LogicalName.Equals("statecode", StringComparison.OrdinalIgnoreCase) ||
                        attr.LogicalName.Equals("owningusername", StringComparison.OrdinalIgnoreCase) ||
                        attr.LogicalName.Equals("owningteamname", StringComparison.OrdinalIgnoreCase) ||
                        attr.LogicalName.Equals("owningbusinessunitname", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var isLookup = attrType.Equals("Lookup", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("Owner", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("Customer", StringComparison.OrdinalIgnoreCase);
                    var isChoice = attrType.Equals("Picklist", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("State", StringComparison.OrdinalIgnoreCase) ||
                                   attrType.Equals("Status", StringComparison.OrdinalIgnoreCase);
                    var isMultiSelectChoice = attrType.Equals("MultiSelectPicklist", StringComparison.OrdinalIgnoreCase);
                    var isBoolean = attrType.Equals("Boolean", StringComparison.OrdinalIgnoreCase);
                    var isPrimaryKey = attr.LogicalName.Equals(table.PrimaryIdAttribute, StringComparison.OrdinalIgnoreCase);

                    if (isLookup)
                    {
                        // Add ID column — lookup name columns exist in both TDS and FabricLink
                        // Skip name column for owning* lookups (not available in TDS/Fabric endpoints)
                        sqlFields.Add($"Base.{attr.LogicalName}");
                        var nameColumn = attr.LogicalName + "name";
                        var isOwningLookup = attr.LogicalName.Equals("owninguser", StringComparison.OrdinalIgnoreCase) ||
                                             attr.LogicalName.Equals("owningteam", StringComparison.OrdinalIgnoreCase) ||
                                             attr.LogicalName.Equals("owningbusinessunit", StringComparison.OrdinalIgnoreCase);
                        
                        if (!processedColumns.Contains(nameColumn) && !isOwningLookup)
                        {
                            sqlFields.Add(ApplySqlAlias($"Base.{nameColumn}", effectiveName, nameColumn, false));
                        }
                        processedColumns.Add(nameColumn);
                        processedColumns.Add(attr.LogicalName);
                    }
                    else if (isChoice || isBoolean)
                    {
                        if (IsFabricLink)
                        {
                            // FabricLink: JOIN to metadata table, select LocalizedLabel
                            var isState = attrType.Equals("State", StringComparison.OrdinalIgnoreCase);
                            var isStatus2 = attrType.Equals("Status", StringComparison.OrdinalIgnoreCase);
                            var joinAlias = $"{table.LogicalName}_{attr.LogicalName}";
                            var nameColumn = attr.LogicalName + "name";

                            if (isState)
                            {
                                joinClauses.Add($"JOIN [StateMetadata] {joinAlias} ON {joinAlias}.[EntityName]='{table.LogicalName}' AND {joinAlias}.[LocalizedLabelLanguageCode]={_languageCode} AND {joinAlias}.[State]=Base.{attr.LogicalName}");
                            }
                            else if (isStatus2)
                            {
                                joinClauses.Add($"JOIN [StatusMetadata] {joinAlias} ON {joinAlias}.[EntityName]='{table.LogicalName}' AND {joinAlias}.[LocalizedLabelLanguageCode]={_languageCode} AND {joinAlias}.[State]=Base.statecode AND {joinAlias}.[Status]=Base.statuscode");
                            }
                            else
                            {
                                var isGlobal = attr.IsGlobal ?? attrDisplayInfo2?.IsGlobal ?? false;
                                var optionSetName = attr.OptionSetName ?? attrDisplayInfo2?.OptionSetName ?? attr.LogicalName;
                                var metadataTable = isGlobal ? "GlobalOptionsetMetadata" : "OptionsetMetadata";
                                joinClauses.Add($"LEFT JOIN [{metadataTable}] {joinAlias} ON {joinAlias}.[OptionSetName]='{optionSetName}' AND {joinAlias}.[EntityName]='{table.LogicalName}' AND {joinAlias}.[LocalizedLabelLanguageCode]={_languageCode} AND {joinAlias}.[Option]=Base.{attr.LogicalName}");
                            }
                            if (!processedColumns.Contains(nameColumn))
                            {
                                var fabricChoiceAlias = _useDisplayNameAliasesInSql && !effectiveName.Equals(nameColumn, StringComparison.OrdinalIgnoreCase)
                                    ? $"{joinAlias}.[LocalizedLabel] AS [{effectiveName}]"
                                    : $"{joinAlias}.[LocalizedLabel] {nameColumn}";
                                sqlFields.Add(fabricChoiceAlias);
                            }
                            processedColumns.Add(nameColumn);
                            processedColumns.Add(attr.LogicalName);
                        }
                        else
                        {
                            // TDS: add virtual name column only (use actual virtual attribute name from metadata)
                            var nameColumn2 = attrDisplayInfo2?.VirtualAttributeName ?? (attr.LogicalName + "name");
                            
                            // Apply correction if this virtual column name needs fixing
                            var correctionKey = $"{table.LogicalName}.{nameColumn2}";
                            if (VirtualColumnCorrections.TryGetValue(correctionKey, out var correctedName))
                            {
                                nameColumn2 = correctedName;
                            }
                            
                            if (!processedColumns.Contains(nameColumn2))
                            {
                                sqlFields.Add(ApplySqlAlias($"Base.{nameColumn2}", effectiveName, nameColumn2, false));
                            }
                            processedColumns.Add(nameColumn2);
                            processedColumns.Add(attr.LogicalName);
                        }
                    }
                    else if (isMultiSelectChoice)
                    {
                        // FabricLink: uses {attributename}name pattern; TDS: uses actual VirtualAttributeName
                        string nameColumn;

                        if (IsFabricLink)
                        {
                            // FabricLink: use OUTER APPLY instead of CTE for DirectQuery compatibility
                            nameColumn = attr.LogicalName + "name";
                            var applyAlias = $"mspl_{attr.LogicalName}";
                            var joinAlias2 = $"meta_{attr.LogicalName}";
                            var isGlobal = attr.IsGlobal ?? attrDisplayInfo2?.IsGlobal ?? false;
                            var optionSetName = attr.OptionSetName ?? attrDisplayInfo2?.OptionSetName ?? attr.LogicalName;
                            var metadataTable = isGlobal ? "GlobalOptionsetMetadata" : "OptionsetMetadata";

                            joinClauses.Add($"OUTER APPLY (SELECT STRING_AGG({joinAlias2}.[LocalizedLabel], ', ') AS {nameColumn} FROM STRING_SPLIT(CAST(Base.{attr.LogicalName} AS VARCHAR(4000)), ',') AS split JOIN [{metadataTable}] AS {joinAlias2} ON {joinAlias2}.[OptionSetName]='{optionSetName}' AND {joinAlias2}.[EntityName]='{table.LogicalName}' AND {joinAlias2}.[LocalizedLabelLanguageCode]={_languageCode} AND {joinAlias2}.[Option]=CAST(LTRIM(RTRIM(split.value)) AS INT) WHERE Base.{attr.LogicalName} IS NOT NULL) {applyAlias}");
                            if (!processedColumns.Contains(nameColumn))
                            {
                                sqlFields.Add(ApplySqlAlias($"{applyAlias}.{nameColumn}", effectiveName, nameColumn, false));
                            }
                        }
                        else
                        {
                            // TDS: use the actual virtual name column from metadata (e.g., donotsendmm -> donotsendmarketingmaterial)
                            nameColumn = attrDisplayInfo2?.VirtualAttributeName ?? (attr.LogicalName + "name");
                            
                            // Apply correction if this virtual column name needs fixing
                            var correctionKey = $"{table.LogicalName}.{nameColumn}";
                            if (VirtualColumnCorrections.TryGetValue(correctionKey, out var correctedName))
                            {
                                nameColumn = correctedName;
                            }
                            
                            if (!processedColumns.Contains(nameColumn))
                            {
                                sqlFields.Add(ApplySqlAlias($"Base.{nameColumn}", effectiveName, nameColumn, false));
                            }
                        }
                        processedColumns.Add(nameColumn);
                        processedColumns.Add(attr.LogicalName);
                    }
                    else
                    {
                        // Regular column - handle datetime wrapping (must match GenerateTableTmdl)
                        var isDateTime = attrType.Equals("DateTime", StringComparison.OrdinalIgnoreCase);
                        var shouldWrapDateTime = isDateTime && dateTableConfig != null &&
                            dateTableConfig.WrappedFields.Any(f =>
                                f.TableName.Equals(table.LogicalName, StringComparison.OrdinalIgnoreCase) &&
                                f.FieldName.Equals(attr.LogicalName, StringComparison.OrdinalIgnoreCase));

                        if (shouldWrapDateTime)
                        {
                            var offset = dateTableConfig!.UtcOffsetHours;
                            var dtAlias = isPrimaryKey ? attr.LogicalName : (_useDisplayNameAliasesInSql ? effectiveName : attr.LogicalName);
                            var dtAliasClause = dtAlias.Equals(attr.LogicalName, StringComparison.OrdinalIgnoreCase)
                                ? $"AS {attr.LogicalName}" : $"AS [{dtAlias}]";
                            sqlFields.Add($"CAST(DATEADD(hour, {offset}, Base.{attr.LogicalName}) AS DATE) {dtAliasClause}");
                        }
                        else
                        {
                            sqlFields.Add(ApplySqlAlias($"Base.{attr.LogicalName}", effectiveName, attr.LogicalName, isPrimaryKey));
                        }
                        processedColumns.Add(attr.LogicalName);
                    }
                }
            }

            var selectList = string.Join(", ", sqlFields);
            
            // Build WHERE clause - use view filter if present, otherwise default statecode filter
            var whereClause = "";
            if (table.View != null && !string.IsNullOrWhiteSpace(table.View.FetchXml))
            {
                // Convert FetchXML to SQL WHERE clause for comparison
                var utcOffset = (int)(dateTableConfig?.UtcOffsetHours ?? -6);
                var converter = new FetchXmlToSqlConverter(utcOffset, IsFabricLink, ShouldStripUserContext(table.Role, table.LogicalName));
                var conversionResult = converter.ConvertToWhereClause(table.View.FetchXml, "Base");
                
                if (!string.IsNullOrWhiteSpace(conversionResult.SqlWhereClause))
                {
                    whereClause = $" WHERE {conversionResult.SqlWhereClause}";
                }
                else if (table.HasStateCode)
                {
                    whereClause = " WHERE Base.statecode=0";
                }
            }
            else if (table.HasStateCode)
            {
                whereClause = " WHERE Base.statecode=0";
            }
            
            // Build JOIN clauses string for FabricLink (includes OUTER APPLY for multi-select fields)
            var joinSection = joinClauses.Count > 0 ? " " + string.Join(" ", joinClauses) : "";

            return NormalizeQuery($"SELECT {selectList} FROM {fromTable} AS Base{joinSection}{whereClause}");
        }

        /// <summary>
        /// Normalizes queries for comparison (removes all whitespace, formatting differences, and SQL comments)
        /// </summary>
        private string NormalizeQuery(string query)
        {
            if (string.IsNullOrEmpty(query)) return string.Empty;
            
            // Remove SQL comments (-- comments) since they're metadata, not functional SQL
            var withoutComments = Regex.Replace(query, @"--[^\r\n]*", "", RegexOptions.Multiline);
            
            // Remove ALL whitespace for comparison - this handles different formatting styles
            // (single line vs multi-line, different indentation, etc.)
            return Regex.Replace(withoutComments.Trim().ToUpperInvariant(), @"\s+", "");
        }

        /// <summary>
        /// Compares two queries
        /// </summary>
        private bool CompareQueries(string existing, string expected)
        {
            return string.Equals(existing, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Analyzes relationship changes
        /// </summary>
        private RelationshipChangeAnalysis AnalyzeRelationshipChanges(
            string pbipFolder,
            string projectName,
            List<ExportRelationship> newRelationships,
            List<ExportTable> tables,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            DateTableConfig? dateTableConfig = null)
        {
            var analysis = new RelationshipChangeAnalysis();
            var relationshipsPath = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "relationships.tmdl");

            if (!File.Exists(relationshipsPath))
            {
                // File doesn't exist - all relationships would be new
                var tableDisplayNames = tables.ToDictionary(t => t.LogicalName, t => t.DisplayName ?? t.SchemaName ?? t.LogicalName, StringComparer.OrdinalIgnoreCase);
                var tablePrimaryKeys = tables.ToDictionary(t => t.LogicalName, t => t.PrimaryIdAttribute ?? $"{t.LogicalName}id", StringComparer.OrdinalIgnoreCase);
                
                analysis.NewRelationships.AddRange(newRelationships.Select(r =>
                {
                    var sourceTable = tableDisplayNames.ContainsKey(r.SourceTable) ? tableDisplayNames[r.SourceTable] : r.SourceTable;
                    var targetTable = tableDisplayNames.ContainsKey(r.TargetTable) ? tableDisplayNames[r.TargetTable] : r.TargetTable;
                    var targetAttr = tablePrimaryKeys.ContainsKey(r.TargetTable) ? tablePrimaryKeys[r.TargetTable] : $"{r.TargetTable}id";
                    return $"{sourceTable}.{r.SourceAttribute} → {targetTable}.{targetAttr}";
                }));
                return analysis;
            }

            try
            {
                var existingContent = File.ReadAllText(relationshipsPath);
                var existingRels = ParseExistingRelationships(existingContent);
                var expectedRels = GenerateExpectedRelationships(newRelationships, tables, attributeDisplayInfo, dateTableConfig);

                // DEBUG: Log what we're comparing
                DebugLogger.Log($"Relationship comparison:");
                DebugLogger.Log($"  Existing ({existingRels.Count}): {string.Join(", ", existingRels.Take(3))}...");
                DebugLogger.Log($"  Expected ({expectedRels.Count}): {string.Join(", ", expectedRels.Take(3))}...");

                // Compare
                foreach (var expected in expectedRels)
                {
                    if (!existingRels.Contains(expected))
                    {
                        analysis.NewRelationships.Add(expected);
                        DebugLogger.Log($"  NEW: {expected}");
                    }
                }

                foreach (var existing in existingRels)
                {
                    if (!expectedRels.Contains(existing))
                    {
                        analysis.RemovedRelationships.Add(existing);
                        DebugLogger.Log($"  REMOVED: {existing}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Warning: Could not analyze relationship changes: {ex.Message}");
                // Fallback - treat all as new
                var tableDisplayNames = tables.ToDictionary(t => t.LogicalName, t => t.DisplayName ?? t.SchemaName ?? t.LogicalName, StringComparer.OrdinalIgnoreCase);
                var tablePrimaryKeys = tables.ToDictionary(t => t.LogicalName, t => t.PrimaryIdAttribute ?? $"{t.LogicalName}id", StringComparer.OrdinalIgnoreCase);
                
                analysis.NewRelationships.AddRange(newRelationships.Select(r =>
                {
                    var sourceTable = tableDisplayNames.ContainsKey(r.SourceTable) ? tableDisplayNames[r.SourceTable] : r.SourceTable;
                    var targetTable = tableDisplayNames.ContainsKey(r.TargetTable) ? tableDisplayNames[r.TargetTable] : r.TargetTable;
                    var targetAttr = tablePrimaryKeys.ContainsKey(r.TargetTable) ? tablePrimaryKeys[r.TargetTable] : $"{r.TargetTable}id";
                    return $"{sourceTable}.{r.SourceAttribute} → {targetTable}.{targetAttr}";
                }));
            }

            return analysis;
        }

        /// <summary>
        /// Parses existing relationships from TMDL
        /// </summary>
        private HashSet<string> ParseExistingRelationships(string content)
        {
            var rels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            DebugLogger.Log($"Parsing relationships from TMDL content ({content.Length} chars)");
            
            // Match relationship blocks that may have properties on separate lines
            // Pattern: relationship <guid> ... everything until next relationship or end of string
            // Use (?=\r?\nrelationship\s) to match until newline+relationship (not just any whitespace after "relationship")
            var relPattern = @"relationship\s+[a-f0-9\-]+[\s\S]*?(?=\r?\nrelationship\s|\Z)";
            var matches = Regex.Matches(content, relPattern, RegexOptions.Multiline);
            
            DebugLogger.Log($"Found {matches.Count} relationship blocks");

            foreach (Match match in matches)
            {
                var relBlock = match.Value;
                var escapedBlock = relBlock.Replace("\r", "").Replace("\n", " | ").Replace("\t", "[TAB]");
                DebugLogger.Log($"Block content: {escapedBlock}");
                
                // Extract fromColumn and toColumn from the block
                // TMDL format: fromColumn: Table.Column (table and column separated by dot, may have quotes around names with spaces)
                var fromMatch = Regex.Match(relBlock, @"fromColumn:\s*(?:'([^']+)'|([^\s.]+))\.(?:'([^']+)'|([^\s\r\n.]+))", RegexOptions.Multiline);
                var toMatch = Regex.Match(relBlock, @"toColumn:\s*(?:'([^']+)'|([^\s.]+))\.(?:'([^']+)'|([^\s\r\n.]+))", RegexOptions.Multiline);
                
                DebugLogger.Log($"  fromMatch.Success={fromMatch.Success}, toMatch.Success={toMatch.Success}");
                if (fromMatch.Success) 
                {
                    DebugLogger.Log($"  fromColumn matched: {fromMatch.Value}");
                    DebugLogger.Log($"    Groups: [1]='{fromMatch.Groups[1].Value}' [2]='{fromMatch.Groups[2].Value}' [3]='{fromMatch.Groups[3].Value}' [4]='{fromMatch.Groups[4].Value}'");
                }
                else
                {
                    DebugLogger.Log($"  fromColumn FAILED to match in block");
                }
                
                if (toMatch.Success) 
                {
                    DebugLogger.Log($"  toColumn matched: {toMatch.Value}");
                    DebugLogger.Log($"    Groups: [1]='{toMatch.Groups[1].Value}' [2]='{toMatch.Groups[2].Value}' [3]='{toMatch.Groups[3].Value}' [4]='{toMatch.Groups[4].Value}'");
                }
                else
                {
                    DebugLogger.Log($"  toColumn FAILED to match in block");
                }
                
                if (fromMatch.Success && toMatch.Success)
                {
                    var fromTable = fromMatch.Groups[1].Success ? fromMatch.Groups[1].Value : fromMatch.Groups[2].Value;
                    var fromCol = fromMatch.Groups[3].Success ? fromMatch.Groups[3].Value : fromMatch.Groups[4].Value;
                    var toTable = toMatch.Groups[1].Success ? toMatch.Groups[1].Value : toMatch.Groups[2].Value;
                    var toCol = toMatch.Groups[3].Success ? toMatch.Groups[3].Value : toMatch.Groups[4].Value;

                    var relString = $"{fromTable}.{fromCol}→{toTable}.{toCol}";
                    rels.Add(relString);
                    DebugLogger.Log($"  Parsed TMDL: {relString}");
                }
            }

            return rels;
        }

        /// <summary>
        /// Generates expected relationships for comparison
        /// </summary>
        private HashSet<string> GenerateExpectedRelationships(
            List<ExportRelationship> relationships,
            List<ExportTable> tables,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            DateTableConfig? dateTableConfig = null)
        {
            var rels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tableDisplayNames = tables.ToDictionary(t => t.LogicalName, t => t.DisplayName ?? t.SchemaName ?? t.LogicalName, StringComparer.OrdinalIgnoreCase);
            var tablePrimaryKeys = tables.ToDictionary(t => t.LogicalName, t => t.PrimaryIdAttribute ?? $"{t.LogicalName}id", StringComparer.OrdinalIgnoreCase);

            foreach (var rel in relationships)
            {
                var sourceTableDisplay = tableDisplayNames.ContainsKey(rel.SourceTable) ? tableDisplayNames[rel.SourceTable] : rel.SourceTable;
                var targetTableDisplay = tableDisplayNames.ContainsKey(rel.TargetTable) ? tableDisplayNames[rel.TargetTable] : rel.TargetTable;

                // CRITICAL: Use logical names, not display names, as TMDL relationships use logical names
                var sourceColLogical = rel.SourceAttribute; // This is the lookup ID field

                // Target is the primary key of the target table
                var targetColLogical = tablePrimaryKeys.ContainsKey(rel.TargetTable) ? tablePrimaryKeys[rel.TargetTable] : $"{rel.TargetTable}id";

                var relString = $"{sourceTableDisplay}.{sourceColLogical}→{targetTableDisplay}.{targetColLogical}";
                rels.Add(relString);
                DebugLogger.Log($"  Generated: {relString} (from {rel.SourceTable}.{rel.SourceAttribute} to {rel.TargetTable})");
            }

            // Add Date table relationship if configured (only for the primary date field)
            if (dateTableConfig != null && !string.IsNullOrEmpty(dateTableConfig.PrimaryDateTable) && !string.IsNullOrEmpty(dateTableConfig.PrimaryDateField))
            {
                var tableDisplayName = tableDisplayNames.ContainsKey(dateTableConfig.PrimaryDateTable) 
                    ? tableDisplayNames[dateTableConfig.PrimaryDateTable] 
                    : dateTableConfig.PrimaryDateTable;
                var sourceTable = tables.FirstOrDefault(t =>
                    t.LogicalName.Equals(dateTableConfig.PrimaryDateTable, StringComparison.OrdinalIgnoreCase));
                var dateAttr = sourceTable?.Attributes
                    .FirstOrDefault(a => a.LogicalName.Equals(dateTableConfig.PrimaryDateField, StringComparison.OrdinalIgnoreCase));

                if (dateAttr != null)
                {
                    var primaryDateFieldName = dateAttr.DisplayName ?? dateAttr.SchemaName ?? dateAttr.LogicalName;
                    if (attributeDisplayInfo.TryGetValue(dateTableConfig.PrimaryDateTable, out var tableAttrs) &&
                        tableAttrs.TryGetValue(dateTableConfig.PrimaryDateField, out var fieldDisplayInfo))
                    {
                        primaryDateFieldName = fieldDisplayInfo.DisplayName ?? primaryDateFieldName;
                    }

                    var dateRelString = $"{tableDisplayName}.{primaryDateFieldName}→Date.Date";
                    rels.Add(dateRelString);
                    DebugLogger.Log($"  Generated Date table relationship: {dateRelString}");
                }
                else
                {
                    DebugLogger.Log($"Date relationship skipped: '{dateTableConfig.PrimaryDateTable}.{dateTableConfig.PrimaryDateField}' not found in selected attributes.");
                }
            }

            return rels;
        }

        /// <summary>
        /// Extracts current DataverseURL from the model.
        /// Checks: 1) DataverseURL.tmdl table (both TDS and FabricLink), 2) expressions.tmdl (legacy), 3) inline URL in table M queries (legacy)
        /// </summary>
        private string ExtractDataverseUrl(string pbipFolder, string projectName)
        {
            // First try DataverseURL parameter table (both TDS and FabricLink)
            var dataverseUrlTablePath = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "tables", "DataverseURL.tmdl");
            if (File.Exists(dataverseUrlTablePath))
            {
                try
                {
                    var content = File.ReadAllText(dataverseUrlTablePath);
                    // Match: source = "url" meta [IsParameterQuery=true, ...]
                    var match = Regex.Match(content, @"source\s*=\s*""([^""]+)""\s*meta\s*\[IsParameterQuery\s*=\s*true");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
                catch { }
            }

            // Fallback: try expressions.tmdl (legacy)
            var url = ExtractExpression(pbipFolder, projectName, "DataverseURL");
            if (!string.IsNullOrEmpty(url))
                return url;

            return string.Empty;
        }

        /// <summary>
        /// Extracts a named expression value from expressions.tmdl
        /// </summary>
        private string ExtractExpression(string pbipFolder, string projectName, string expressionName)
        {
            var expressionsPath = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "expressions.tmdl");
            if (!File.Exists(expressionsPath))
                return string.Empty;

            try
            {
                var content = File.ReadAllText(expressionsPath);
                var match = Regex.Match(content, $@"expression\s+{Regex.Escape(expressionName)}\s*=\s*""([^""]+)""");
                if (match.Success)
                    return match.Groups[1].Value;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Warning: Could not extract expression '{expressionName}': {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Column definition for comparison
        /// </summary>
        private class ColumnDefinition
        {
            public string? DisplayName { get; set; }
            public string? LogicalName { get; set; }
            public string? DataType { get; set; }
            public string? SourceColumn { get; set; }
            public string? FormatString { get; set; }
        }

        /// <summary>
        /// Table change analysis result
        /// </summary>
        private class TableChangeAnalysis
        {
            public bool QueryChanged { get; set; }
            public string? QueryChangeDetail { get; set; }  // Specific detail about what changed in query
            public List<string> NewColumns { get; } = new List<string>();
            public Dictionary<string, string> ModifiedColumns { get; } = new Dictionary<string, string>();
            public List<string> RemovedColumns { get; } = new List<string>();

            public bool HasChanges => QueryChanged || NewColumns.Count > 0 || ModifiedColumns.Count > 0 || RemovedColumns.Count > 0;
        }

        /// <summary>
        /// Relationship change analysis result
        /// </summary>
        private class RelationshipChangeAnalysis
        {
            public List<string> NewRelationships { get; } = new List<string>();
            public List<string> ModifiedRelationships { get; } = new List<string>();
            public List<string> RemovedRelationships { get; } = new List<string>();

            public bool HasChanges => NewRelationships.Count > 0 || ModifiedRelationships.Count > 0 || RemovedRelationships.Count > 0;
        }

        /// <summary>
        /// Validates the structural integrity of an existing PBIP project.
        /// Returns a list of missing critical files/folders that would prevent
        /// an incremental update from succeeding.
        /// </summary>
        /// <returns>
        /// A list of human-readable descriptions of missing elements.
        /// An empty list means the structure is intact and safe for incremental update.
        /// </returns>
        private List<string> ValidateModelIntegrity(string pbipFolder, string projectName)
        {
            var missing = new List<string>();

            // .pbip project file
            var pbipFile = Path.Combine(pbipFolder, $"{projectName}.pbip");
            if (!File.Exists(pbipFile))
                missing.Add($"{projectName}.pbip (project file)");

            // SemanticModel folder
            var semanticModelFolder = Path.Combine(pbipFolder, $"{projectName}.SemanticModel");
            if (!Directory.Exists(semanticModelFolder))
            {
                missing.Add($"{projectName}.SemanticModel/ (semantic model folder)");
                return missing; // No point checking children
            }

            // .platform file
            var platformFile = Path.Combine(semanticModelFolder, ".platform");
            if (!File.Exists(platformFile))
                missing.Add($"{projectName}.SemanticModel/.platform");

            // definition.pbism
            var pbismFile = Path.Combine(semanticModelFolder, "definition.pbism");
            if (!File.Exists(pbismFile))
                missing.Add($"{projectName}.SemanticModel/definition.pbism");

            // definition/ folder
            var definitionFolder = Path.Combine(semanticModelFolder, "definition");
            if (!Directory.Exists(definitionFolder))
            {
                missing.Add($"{projectName}.SemanticModel/definition/ (definition folder)");
                return missing;
            }

            // model.tmdl
            var modelTmdl = Path.Combine(definitionFolder, "model.tmdl");
            if (!File.Exists(modelTmdl))
                missing.Add($"definition/model.tmdl");

            // tables/ folder
            var tablesFolder = Path.Combine(definitionFolder, "tables");
            if (!Directory.Exists(tablesFolder))
                missing.Add($"definition/tables/ (tables folder)");

            // Report folder
            var reportFolder = Path.Combine(pbipFolder, $"{projectName}.Report");
            if (!Directory.Exists(reportFolder))
                missing.Add($"{projectName}.Report/ (report folder)");

            // DataverseURL table (both TDS and FabricLink)
            var dvUrlTable = Path.Combine(definitionFolder, "tables", "DataverseURL.tmdl");
            if (Directory.Exists(tablesFolder) && !File.Exists(dvUrlTable))
                missing.Add("tables/DataverseURL.tmdl (connection parameter)");

            // FabricLink expressions (FabricSQLEndpoint, FabricLakehouse)
            if (IsFabricLink)
            {
                var expressionsFile = Path.Combine(definitionFolder, "expressions.tmdl");
                if (!File.Exists(expressionsFile))
                    missing.Add("definition/expressions.tmdl (FabricLink connection parameters)");
            }

            if (missing.Count > 0)
            {
                DebugLogger.Log($"Model integrity check for '{projectName}': {missing.Count} missing element(s):");
                foreach (var m in missing)
                    DebugLogger.Log($"  - {m}");
            }
            else
            {
                DebugLogger.Log($"Model integrity check for '{projectName}': all critical files present");
            }

            return missing;
        }

        /// <summary>
        /// Detects the existing storage mode by reading the partition mode from the first
        /// non-generated table TMDL file found in the tables folder.
        /// Returns "DirectQuery", "Import", or "Dual", or null if not determinable.
        /// </summary>
        private string? DetectExistingStorageMode(string tablesFolder)
        {
            var generatedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Date", "DateAutoTemplate", "DataverseURL" };
            
            foreach (var file in Directory.GetFiles(tablesFolder, "*.tmdl"))
            {
                var tableName = Path.GetFileNameWithoutExtension(file);
                if (generatedTables.Contains(tableName)) continue;

                try
                {
                    var content = File.ReadAllText(file);
                    if (content.Contains("mode: import"))
                        return "Import";
                    if (content.Contains("mode: dual"))
                        return "Dual";
                    if (content.Contains("mode: directQuery"))
                        return "DirectQuery";
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// Deletes the cache.abf file to prevent stale imported data after a storage mode change.
        /// </summary>
        private void DeleteCacheAbf(string pbipFolder, string projectName)
        {
            var cacheAbfPath = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", ".pbi", "cache.abf");
            if (File.Exists(cacheAbfPath))
            {
                try
                {
                    File.Delete(cacheAbfPath);
                    SetStatus("Deleted cache.abf (storage mode changed)");
                    DebugLogger.Log($"Deleted cache.abf due to storage mode change: {cacheAbfPath}");
                }
                catch (Exception ex)
                {
                    SetStatus($"Warning: Could not delete cache.abf: {ex.Message}");
                    DebugLogger.Log($"Failed to delete cache.abf: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Applies changes to the semantic model
        /// </summary>
        public bool ApplyChanges(
            string semanticModelName,
            string outputFolder,
            string dataverseUrl,
            List<ExportTable> tables,
            List<ExportRelationship> relationships,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            bool createBackup,
            DateTableConfig? dateTableConfig = null,
            bool removeOrphanedTables = false)
        {
            try
            {
                // Extract environment name from Dataverse URL to build correct path
                var environmentName = ExtractEnvironmentName(dataverseUrl);
                var pbipFolder = Path.Combine(outputFolder, environmentName, semanticModelName);
                bool pbipExists = Directory.Exists(pbipFolder);

                // Validate structural integrity if the folder exists
                bool requiresFullRebuild = false;
                if (pbipExists)
                {
                    var missingElements = ValidateModelIntegrity(pbipFolder, semanticModelName);
                    if (missingElements.Count > 0)
                    {
                        requiresFullRebuild = true;
                        SetStatus($"Incomplete model structure detected ({missingElements.Count} missing element(s)) — performing full rebuild...");
                    }
                }

                // Create backup if requested
                if (createBackup && pbipExists)
                {
                    CreateBackup(pbipFolder, outputFolder, environmentName, semanticModelName);
                }

                // Delete cache.abf if storage mode is changing
                if (pbipExists)
                {
                    var tablesFolder = Path.Combine(pbipFolder, $"{semanticModelName}.SemanticModel", "definition", "tables");
                    if (Directory.Exists(tablesFolder))
                    {
                        var existingMode = DetectExistingStorageMode(tablesFolder);
                        if (existingMode != null && !existingMode.Equals(_storageMode, StringComparison.OrdinalIgnoreCase))
                        {
                            DeleteCacheAbf(pbipFolder, semanticModelName);
                        }
                    }
                }

                // Apply changes
                if (pbipExists && !requiresFullRebuild)
                {
                    BuildIncremental(semanticModelName, outputFolder, dataverseUrl, tables, relationships, attributeDisplayInfo, dateTableConfig, removeOrphanedTables);
                }
                else
                {
                    Build(semanticModelName, outputFolder, dataverseUrl, tables, relationships, attributeDisplayInfo, dateTableConfig);
                }

                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"Error applying changes: {ex.Message}");
                DebugLogger.Log($"Error in ApplyChanges: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Creates a timestamped backup of the PBIP folder
        /// </summary>
        private void CreateBackup(string pbipFolder, string outputFolder, string environmentName, string semanticModelName)
        {
            try
            {
                SetStatus("Creating backup...");
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFolder = Path.Combine(outputFolder, environmentName, semanticModelName, "Backup", $"PBIP_Backup_{timestamp}");
                
                // Simple directory copy for backup (no template replacement needed)
                CopyDirectorySimple(pbipFolder, backupFolder);
                SetStatus($"Backup created: {Path.GetFileName(backupFolder)}");
            }
            catch (Exception ex)
            {
                SetStatus($"Warning: Backup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple directory copy without template replacement
        /// </summary>
        private void CopyDirectorySimple(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                
                // Skip .pbi folders (contains user-specific Power BI Desktop settings that may be locked)
                if (dirName.Equals(".pbi", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.Log($"  Skipping .pbi folder during backup: {subDir}");
                    continue;
                }
                
                string targetSubDir = Path.Combine(targetDir, dirName);
                CopyDirectorySimple(subDir, targetSubDir);
            }
        }

        /// <summary>
        /// Extracts user-created measures from existing TMDL (excludes auto-generated measures)
        /// </summary>
        private List<string> ExtractUserMeasures(string tmdlPath, ExportTable table)
        {
            var userMeasures = new List<string>();

            if (!File.Exists(tmdlPath))
                return userMeasures;

            // Auto-generated measure names to exclude
            var displayName = table.DisplayName ?? table.SchemaName ?? table.LogicalName;
            var autoMeasures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                $"Link to {displayName}",
                $"{displayName} Count"
            };

            try
            {
                var content = File.ReadAllText(tmdlPath);
                var measurePattern = @"^\s*measure\s+([^\s]+)";
                var matches = Regex.Matches(content, measurePattern, RegexOptions.Multiline);

                foreach (Match match in matches)
                {
                    var measureName = match.Groups[1].Value.Trim('\'', '"', '[', ']');
                    // Skip auto-generated measures (they'll be re-generated)
                    if (!autoMeasures.Contains(measureName))
                        userMeasures.Add(measureName);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Warning: Could not parse measures from {tmdlPath}: {ex.Message}");
            }

            return userMeasures;
        }

        /// <summary>
        /// Performs incremental update to existing PBIP, preserving user changes
        /// </summary>
        private void BuildIncremental(
            string semanticModelName,
            string outputFolder,
            string dataverseUrl,
            List<ExportTable> tables,
            List<ExportRelationship> relationships,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            DateTableConfig? dateTableConfig = null,
            bool removeOrphanedTables = false)
        {
            SetStatus("Performing incremental update...");

            // Extract environment name from Dataverse URL to build correct path
            var environmentName = ExtractEnvironmentName(dataverseUrl);
            var pbipFolder = Path.Combine(outputFolder, environmentName, semanticModelName);
            var projectName = semanticModelName;

            // Update project configuration (DataverseURL)
            SetStatus("Updating Dataverse URL...");
            UpdateProjectConfiguration(pbipFolder, projectName, dataverseUrl);

            // Build relationship columns lookup
            var relationshipColumnsPerTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in relationships)
            {
                if (!relationshipColumnsPerTable.ContainsKey(rel.SourceTable))
                    relationshipColumnsPerTable[rel.SourceTable] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                relationshipColumnsPerTable[rel.SourceTable].Add(rel.SourceAttribute);
            }

            // Update tables incrementally
            SetStatus($"Updating {tables.Count} table(s)...");
            var tablesFolder = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "tables");
            Directory.CreateDirectory(tablesFolder);

            foreach (var table in tables)
            {
                SetStatus($"Updating table: {table.DisplayName}...");
                var tableFileName = SanitizeFileName(table.DisplayName ?? table.SchemaName ?? table.LogicalName) + ".tmdl";
                var tablePath = Path.Combine(tablesFolder, tableFileName);

                var requiredLookupColumns = relationshipColumnsPerTable.ContainsKey(table.LogicalName)
                    ? relationshipColumnsPerTable[table.LogicalName]
                    : new HashSet<string>();

                // Extract user measures if table exists
                string? userMeasuresSection = null;
                if (File.Exists(tablePath))
                {
                    userMeasuresSection = ExtractUserMeasuresSection(tablePath, table);
                }

                // Generate new table TMDL
                var tableTmdl = GenerateTableTmdl(table, attributeDisplayInfo, requiredLookupColumns, dateTableConfig);

                // Append user measures if any
                if (!string.IsNullOrEmpty(userMeasuresSection))
                {
                    tableTmdl = InsertUserMeasures(tableTmdl, userMeasuresSection!);
                }

                WriteTmdlFile(tablePath, tableTmdl);
            }

            // Remove orphaned tables if requested
            if (removeOrphanedTables)
            {
                var generatedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Date", "DateAutoTemplate", "DataverseURL" };
                var metadataTables = tables.Select(t => SanitizeFileName(t.DisplayName ?? t.SchemaName ?? t.LogicalName))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var existingTableFiles = Directory.GetFiles(tablesFolder, "*.tmdl");
                foreach (var filePath in existingTableFiles)
                {
                    var tableName = Path.GetFileNameWithoutExtension(filePath);
                    if (!metadataTables.Contains(tableName) && !generatedTables.Contains(tableName))
                    {
                        SetStatus($"Removing orphaned table: {tableName}...");
                        DebugLogger.Log($"Removing orphaned table file: {filePath}");
                        File.Delete(filePath);
                    }
                }
            }

            // Build/Update Date table if configured (only if model doesn't already have a date table)
            if (dateTableConfig != null)
            {
                var existingDateTable = FindExistingDateTable(tablesFolder);
                if (existingDateTable != null)
                {
                    SetStatus($"Date table '{existingDateTable}' already exists - preserving it");
                    DebugLogger.Log($"Skipping Date table generation - found existing date table: {existingDateTable}");
                }
                else
                {
                    SetStatus("Updating Date table...");
                    var dateTableTmdl = GenerateDateTableTmdl(dateTableConfig);
                    WriteTmdlFile(Path.Combine(tablesFolder, "Date.tmdl"), dateTableTmdl);
                }
            }

            // Update relationships
            var relationshipsPath = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "relationships.tmdl");
            
            if (relationships.Any() || dateTableConfig != null)
            {
                SetStatus("Updating relationships...");
                var relationshipsTmdl = GenerateRelationshipsTmdl(tables, relationships, attributeDisplayInfo, dateTableConfig);
                WriteTmdlFile(relationshipsPath, relationshipsTmdl);
            }
            else if (File.Exists(relationshipsPath))
            {
                // Remove relationships file if it exists but no relationships are defined
                SetStatus("Removing relationships file (no relationships defined)...");
                File.Delete(relationshipsPath);
            }

            // Update model.tmdl
            SetStatus("Updating model metadata...");
            // Check if there's an existing date table from template
            var hasDateTable = FindExistingDateTable(tablesFolder) != null;
            UpdateModelTmdl(pbipFolder, projectName, tables, hasDateTable);

            // Verify critical files exist
            VerifyPbipStructure(pbipFolder, projectName);

            SetStatus("Incremental update complete!");
        }

        /// <summary>
        /// Extracts the user measures section from existing TMDL (excludes auto-generated measures).
        /// The table parameter provides context for identifying auto-generated measure names.
        /// </summary>
        private string? ExtractUserMeasuresSection(string tmdlPath, ExportTable? table = null)
        {
            try
            {
                var content = File.ReadAllText(tmdlPath);
                
                // Build set of auto-generated measure names to exclude
                var autoMeasures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (table != null)
                {
                    var displayName = table.DisplayName ?? table.SchemaName ?? table.LogicalName;
                    autoMeasures.Add($"Link to {displayName}");
                    autoMeasures.Add($"{displayName} Count");
                }

                // Find all measure blocks
                var measurePattern = @"(^\s*(?:///[^\r\n]*\r?\n)*\s*measure\s+([^\r\n]+)\r?\n(?:.*?\r?\n)*?(?=^\s*(?:measure|column|partition|annotation)\s|\z))";
                var matches = Regex.Matches(content, measurePattern, RegexOptions.Multiline);

                if (matches.Count == 0)
                    return null;

                var sb = new StringBuilder();
                foreach (Match match in matches)
                {
                    // Extract measure name from the "measure 'Name' = ..." line
                    var nameMatch = Regex.Match(match.Groups[2].Value, @"^'([^']+)'|^([^\s=]+)");
                    var measureName = nameMatch.Groups[1].Success ? nameMatch.Groups[1].Value : nameMatch.Groups[2].Value;
                    
                    // Skip auto-generated measures (they'll be re-generated)
                    if (autoMeasures.Contains(measureName))
                        continue;

                    sb.Append(match.Value);
                }

                return sb.Length > 0 ? sb.ToString() : null;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Warning: Could not extract measures from {tmdlPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Inserts user measures into generated TMDL (after columns, before partition)
        /// </summary>
        private string InsertUserMeasures(string tableTmdl, string measuresSection)
        {
            // Find the partition section and insert measures before it
            var partitionIndex = tableTmdl.IndexOf("\tpartition");
            if (partitionIndex > 0)
            {
                return tableTmdl.Insert(partitionIndex, measuresSection);
            }

            // If no partition found, append before annotations
            var annotationIndex = tableTmdl.IndexOf("\tannotation");
            if (annotationIndex > 0)
            {
                return tableTmdl.Insert(annotationIndex, measuresSection);
            }

            // Fallback: append at end
            return tableTmdl + measuresSection;
        }

        /// <summary>
        /// Updates the model.tmdl file with table references
        /// </summary>
        private void UpdateModelTmdl(string pbipFolder, string projectName, List<ExportTable> tables, bool includeDateTable = false)
        {
            var modelPath = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition", "model.tmdl");
            if (!File.Exists(modelPath))
                return;

            var sb = new StringBuilder();
            
            // Write the model header
            sb.AppendLine("model Model");
            sb.AppendLine("\tculture: en-US");
            sb.AppendLine("\tdefaultPowerBIDataSourceVersion: powerBI_V3");
            sb.AppendLine("\tsourceQueryCulture: en-US");
            sb.AppendLine("\tdataAccessOptions");
            sb.AppendLine("\t\tlegacyRedirects");
            sb.AppendLine("\t\treturnErrorValuesAsNull");
            sb.AppendLine();
            sb.AppendLine("annotation __PBI_TimeIntelligenceEnabled = 0");
            sb.AppendLine();

            // Build PBI_QueryOrder annotation
            var tableNames = tables.Select(t => t.DisplayName ?? t.SchemaName ?? t.LogicalName).ToList();
            if (IsFabricLink)
            {
                tableNames.Insert(0, "DataverseURL");
                tableNames.Insert(0, "FabricLakehouse");
                tableNames.Insert(0, "FabricSQLEndpoint");
            }
            else
            {
                // TDS: DataverseURL is a parameter table (must appear first in query order)
                tableNames.Insert(0, "DataverseURL");
            }
            if (includeDateTable)
            {
                tableNames.Add("Date"); // Date table at the end
            }
            var queryOrder = string.Join("\",\"", tableNames);
            sb.AppendLine($"annotation PBI_QueryOrder = [\"{queryOrder}\"]");
            sb.AppendLine();
            sb.AppendLine("annotation PBI_ProTooling = [\"TMDLView_Desktop\",\"DevMode\",\"TMDL-Extension\"]");
            sb.AppendLine();

            // Write ref table entries
            foreach (var table in tables)
            {
                var displayName = table.DisplayName ?? table.SchemaName ?? table.LogicalName;
                // Use quotes for names with spaces
                if (displayName.Contains(' '))
                {
                    sb.AppendLine($"ref table '{displayName}'");
                }
                else
                {
                    sb.AppendLine($"ref table {displayName}");
                }
            }

            // Add Date table reference if configured
            if (includeDateTable)
            {
                sb.AppendLine("ref table Date");
            }

            // Add DataverseURL parameter table reference for TDS
            // (FabricLink uses ref expression entries instead)
            if (!IsFabricLink)
            {
                sb.AppendLine("ref table DataverseURL");
            }
            sb.AppendLine();

            // Write ref expression entries (FabricLink only)
            // FabricLink: PBI Desktop expects all 3 expression refs in model.tmdl
            // TDS: DataverseURL is a parameter table (ref table), not an expression
            if (IsFabricLink)
            {
                sb.AppendLine("ref expression FabricSQLEndpoint");
                sb.AppendLine("ref expression FabricLakehouse");
                sb.AppendLine("ref expression DataverseURL");
            }
            sb.AppendLine();

            sb.AppendLine("ref cultureInfo en-US");
            sb.AppendLine();

            WriteTmdlFile(modelPath, sb.ToString());
        }

        /// <summary>
        /// Copies the PBIP template to the target folder
        /// </summary>
        private void CopyTemplate(string targetFolder, string projectName)
        {
            // Create target folder
            Directory.CreateDirectory(targetFolder);

            // Find the template .pbip file (could be named anything)
            var templatePbipFiles = Directory.GetFiles(_templatePath, "*.pbip");
            if (templatePbipFiles.Length == 0)
            {
                throw new FileNotFoundException($"No .pbip file found in template folder: {_templatePath}");
            }
            
            var templatePbipFile = templatePbipFiles[0];
            var templateName = Path.GetFileNameWithoutExtension(templatePbipFile);
            DebugLogger.Log($"Using template: {templateName}.pbip");

            // Copy all template files, replacing the template name with project name
            CopyDirectory(_templatePath, targetFolder, projectName, templateName);
            
            // Verify .pbip file was created
            var pbipFile = Path.Combine(targetFolder, $"{projectName}.pbip");
            if (!File.Exists(pbipFile))
            {
                throw new FileNotFoundException($"Failed to create .pbip file at: {pbipFile}");
            }
        }

        /// <summary>
        /// Recursively copies a directory, renaming template name to the project name
        /// </summary>
        private void CopyDirectory(string sourceDir, string targetDir, string projectName, string templateName)
        {
            // Create target directory
            Directory.CreateDirectory(targetDir);

            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    var fileName = Path.GetFileName(file);
                    // Rename files containing the template name
                    var newFileName = fileName.Replace(templateName, projectName);
                    var targetPath = Path.Combine(targetDir,newFileName);

                    DebugLogger.Log($"Copying: {fileName} -> {newFileName}");

                    // Determine if this is a text file that needs content replacement
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    var isTextFile = extension == ".json" || extension == ".pbip" || extension == ".pbism" || 
                                    extension == ".pbir" || extension == ".tmdl" || extension == ".txt" || 
                                    extension == ".platform";

                    if (isTextFile)
                    {
                        // Read, replace, and write text files
                        var content = File.ReadAllText(file, Utf8WithoutBom);
                        if (extension == ".json" || extension == ".pbip" || extension == ".pbism" || extension == ".pbir")
                        {
                            content = content.Replace(templateName, projectName);
                        }
                        WriteTmdlFile(targetPath, content);
                        DebugLogger.Log($"  ✓ Written text file: {targetPath}");
                    }
                    else
                    {
                        // Binary copy for non-text files
                        File.Copy(file, targetPath, true);
                        DebugLogger.Log($"  ✓ Copied binary file: {targetPath}");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"  ✗ ERROR copying file {Path.GetFileName(file)}: {ex.Message}");
                    DebugLogger.Log($"     Source: {file}");
                    DebugLogger.Log($"     Target: {targetDir}");
                    DebugLogger.Log($"     Exception: {ex.GetType().Name} - {ex.Message}");
                    throw; // Re-throw to fail the build if file copy fails
                }
            }

            // Copy subdirectories
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                
                // Skip .pbi folders (contains user-specific Power BI Desktop settings)
                if (dirName.Equals(".pbi", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.Log($"  Skipping .pbi folder: {dir}");
                    continue;
                }
                
                // Rename directories containing the template name
                var newDirName = dirName.Replace(templateName, projectName);
                CopyDirectory(dir, Path.Combine(targetDir, newDirName), projectName, templateName);
            }
        }

        /// <summary>
        /// Updates the project configuration files
        /// </summary>
        private void UpdateProjectConfiguration(string pbipFolder, string projectName, string dataverseUrl)
        {
            // Normalize the Dataverse URL (remove https:// if present)
            var normalizedUrl = dataverseUrl;
            if (normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                normalizedUrl = normalizedUrl.Substring(8);

            // Manage DataverseURL and expressions
            var definitionFolder = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition");
            var expressionsPath = Path.Combine(definitionFolder, "expressions.tmdl");
            var dataverseUrlTablePath = Path.Combine(definitionFolder, "tables", "DataverseURL.tmdl");

            if (IsFabricLink)
            {
                // FabricLink: Create expressions for FabricSQLEndpoint and FabricLakehouse
                var fabricExpressions = GenerateFabricLinkExpressions(
                    _fabricLinkEndpoint ?? "", _fabricLinkDatabase ?? "");
                WriteTmdlFile(expressionsPath, fabricExpressions);
                
                // FabricLink ALSO needs DataverseURL as a table (for DAX measure references)
                WriteDataverseUrlTable(dataverseUrlTablePath, normalizedUrl);
            }
            else
            {
                // TDS: DataverseURL is a hidden parameter table with mode: import and Enable Load.
                // This is the PBI Desktop pattern for Power Query parameters — the table name
                // acts as the parameter name in M queries (DataverseURL in CommonDataService.Database(DataverseURL,...)).
                // The table MUST have Enable Load checked (default for tables) — without it,
                // PBI Desktop throws KeyNotFoundException during refresh.
                WriteDataverseUrlTable(dataverseUrlTablePath, normalizedUrl);

                // Remove any stale expressions.tmdl from previous FabricLink or legacy builds
                if (File.Exists(expressionsPath))
                {
                    DebugLogger.Log("Removing stale expressions.tmdl from TDS model");
                    File.Delete(expressionsPath);
                }

                // For TDS: strip any stale ref expression DataverseURL from model.tmdl
                // (may exist from earlier tool versions with expression-based DataverseURL)
                var modelCleanupPath = Path.Combine(definitionFolder, "model.tmdl");
                if (File.Exists(modelCleanupPath))
                {
                    var content = File.ReadAllText(modelCleanupPath, Utf8WithoutBom);
                    if (Regex.IsMatch(content, @"ref\s+expression\s+DataverseURL"))
                    {
                        DebugLogger.Log("Removing stale ref expression DataverseURL from TDS model.tmdl");
                        content = Regex.Replace(content, @"^\s*ref\s+expression\s+DataverseURL\s*\r?\n?", "", RegexOptions.Multiline);
                        WriteTmdlFile(modelCleanupPath, content);
                    }
                }
            }

            // Update .platform file with display name
            var platformPath = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", ".platform");
            if (File.Exists(platformPath))
            {
                try
                {
                    var json = JObject.Parse(File.ReadAllText(platformPath, Utf8WithoutBom));
                    if (json["metadata"] != null)
                    {
                        json["metadata"]!["displayName"] = projectName;
                    }
                    // Generate new logicalId for uniqueness
                    if (json["config"] != null)
                    {
                        json["config"]!["logicalId"] = Guid.NewGuid().ToString();
                    }
                    WriteTmdlFile(platformPath, json.ToString(Formatting.Indented));
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Warning: Failed to update .platform file: {ex.Message}");
                }
            }

            // Update Report .platform file as well
            var reportPlatformPath = Path.Combine(pbipFolder, $"{projectName}.Report", ".platform");
            if (File.Exists(reportPlatformPath))
            {
                try
                {
                    var json = JObject.Parse(File.ReadAllText(reportPlatformPath, Utf8WithoutBom));
                    if (json["metadata"] != null)
                    {
                        json["metadata"]!["displayName"] = projectName;
                    }
                    // Generate new logicalId for uniqueness
                    if (json["config"] != null)
                    {
                        json["config"]!["logicalId"] = Guid.NewGuid().ToString();
                    }
                    WriteTmdlFile(reportPlatformPath, json.ToString(Formatting.Indented));
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Warning: Failed to update Report .platform file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Generates TMDL content for a table
        /// </summary>
        private string GenerateTableTmdl(ExportTable table, Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo, HashSet<string> requiredLookupColumns, DateTableConfig? dateTableConfig = null, string? outputFolder = null)
        {
            var sb = new StringBuilder();
            var displayName = table.DisplayName ?? table.SchemaName ?? table.LogicalName;
            var tableLineageTag = Guid.NewGuid().ToString();

            // Process view filter if present
            string viewFilterClause = "";
            string viewFilterComment = "";
            string viewDisplayName = "";
            bool hasPartialSupport = false;
            
            if (table.View != null && !string.IsNullOrWhiteSpace(table.View.FetchXml))
            {
                var utcOffset = (int)(dateTableConfig?.UtcOffsetHours ?? -6);
                var converter = new FetchXmlToSqlConverter(utcOffset, IsFabricLink, ShouldStripUserContext(table.Role, table.LogicalName));
                var conversionResult = converter.ConvertToWhereClause(table.View.FetchXml, "Base");
                
                if (!string.IsNullOrWhiteSpace(conversionResult.SqlWhereClause))
                {
                    viewFilterClause = conversionResult.SqlWhereClause;
                    viewDisplayName = table.View.ViewName;
                    hasPartialSupport = !conversionResult.IsFullySupported;
                    
                    // Create filter comment for SQL
                    var filterCommentBuilder = new StringBuilder();
                    filterCommentBuilder.Append($"-- View Filter: {viewDisplayName}{(hasPartialSupport ? " *" : "")}");
                    
                    if (hasPartialSupport && conversionResult.UnsupportedFeatures.Any())
                    {
                        filterCommentBuilder.AppendLine();
                        filterCommentBuilder.AppendLine($"-- * Partially supported - some conditions were not translated:");
                        foreach (var unsupported in conversionResult.UnsupportedFeatures)
                        {
                            filterCommentBuilder.AppendLine($"--   - {unsupported}");
                        }
                    }
                    
                    viewFilterComment = filterCommentBuilder.ToString();
                    
                    // Log debug information
                    if (outputFolder != null)
                    {
                        FetchXmlToSqlConverter.LogConversionDebug(
                            table.View.ViewName,
                            table.View.FetchXml,
                            conversionResult,
                            outputFolder
                        );
                    }
                    
                    SetStatus($"Applied view filter: {viewDisplayName}{(hasPartialSupport ? " (partial)" : "")}");
                }
            }

            // Add table description if available
            if (!string.IsNullOrEmpty(table.LogicalName))
            {
                sb.AppendLine($"/// Source: {table.LogicalName}");
            }
            sb.AppendLine($"table {QuoteTmdlName(displayName)}");
            sb.AppendLine($"\tlineageTag: {tableLineageTag}");
            sb.AppendLine();

            // Collect columns and SQL fields
            var columns = new List<ColumnInfo>();
            var sqlFields = new List<string>();
            var joinClauses = new List<string>(); // FabricLink: metadata table JOINs and OUTER APPLY for multi-select fields
            var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Get attribute info for this table
            var attrInfo = attributeDisplayInfo.ContainsKey(table.LogicalName) 
                ? attributeDisplayInfo[table.LogicalName] 
                : new Dictionary<string, AttributeDisplayInfo>();

            // Always include primary key first (required for relationships)
            var primaryKey = table.PrimaryIdAttribute ?? table.LogicalName + "id";
            if (!table.Attributes.Any(a => a.LogicalName.Equals(primaryKey, StringComparison.OrdinalIgnoreCase)))
            {
                columns.Add(new ColumnInfo
                {
                    LogicalName = primaryKey,
                    DisplayName = primaryKey,
                    SourceColumn = primaryKey,
                    IsHidden = true,
                    IsKey = true,
                    Description = $"Source: {table.LogicalName}.{primaryKey}",
                    AttributeType = "uniqueidentifier"
                });
                sqlFields.Add($"Base.{primaryKey}");
                processedColumns.Add(primaryKey);
            }

            // Add required lookup columns for relationships (if not in selected attributes)
            foreach (var lookupCol in requiredLookupColumns)
            {
                if (!table.Attributes.Any(a => a.LogicalName.Equals(lookupCol, StringComparison.OrdinalIgnoreCase)) && !processedColumns.Contains(lookupCol))
                {
                    columns.Add(new ColumnInfo
                    {
                        LogicalName = lookupCol,
                        DisplayName = lookupCol,
                        SourceColumn = lookupCol,
                        IsHidden = true,
                        Description = $"Source: {table.LogicalName}.{lookupCol}",
                        AttributeType = "lookup"
                    });
                    sqlFields.Add($"Base.{lookupCol}");
                    processedColumns.Add(lookupCol);
                }
            }

            foreach (var attr in table.Attributes)
            {
                // Skip if already processed (e.g., primary key was added above)
                if (processedColumns.Contains(attr.LogicalName))
                    continue;

                var attrDisplayInfo = attrInfo.ContainsKey(attr.LogicalName) ? attrInfo[attr.LogicalName] : null;
                var attrType = attr.AttributeType ?? attrDisplayInfo?.AttributeType ?? "";
                var attrDisplayName = attr.DisplayName ?? attrDisplayInfo?.DisplayName ?? attr.SchemaName ?? attr.LogicalName;
                var effectiveName = GetEffectiveDisplayName(attrDisplayInfo, attrDisplayName);
                var targets = attr.Targets ?? attrDisplayInfo?.Targets;

                // Skip statecode and special owning name columns
                // statecode: used in WHERE clause but not included in model
                // owning*name columns: not available in TDS or Fabric endpoints (but owning* lookup IDs are fine)
                if (attr.LogicalName.Equals("statecode", StringComparison.OrdinalIgnoreCase) ||
                    attr.LogicalName.Equals("owningusername", StringComparison.OrdinalIgnoreCase) ||
                    attr.LogicalName.Equals("owningteamname", StringComparison.OrdinalIgnoreCase) ||
                    attr.LogicalName.Equals("owningbusinessunitname", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if this is a lookup, choice, or status field
                var isLookup = attrType.Equals("Lookup", StringComparison.OrdinalIgnoreCase) ||
                               attrType.Equals("Owner", StringComparison.OrdinalIgnoreCase) ||
                               attrType.Equals("Customer", StringComparison.OrdinalIgnoreCase);
                var isChoice = attrType.Equals("Picklist", StringComparison.OrdinalIgnoreCase) ||
                               attrType.Equals("State", StringComparison.OrdinalIgnoreCase) ||
                               attrType.Equals("Status", StringComparison.OrdinalIgnoreCase);
                var isMultiSelectChoice = attrType.Equals("MultiSelectPicklist", StringComparison.OrdinalIgnoreCase);
                var isBoolean = attrType.Equals("Boolean", StringComparison.OrdinalIgnoreCase);
                var isPrimaryKey = attr.LogicalName.Equals(table.PrimaryIdAttribute, StringComparison.OrdinalIgnoreCase);
                var isPrimaryName = attr.LogicalName.Equals(table.PrimaryNameAttribute, StringComparison.OrdinalIgnoreCase);

                // Build description
                var description = BuildDescription(table.LogicalName, attr.LogicalName, attr.SchemaName ?? attr.LogicalName, attr.Description, targets);

                if (isLookup)
                {
                    // For lookups: add the ID column (hidden) and the name column (visible)
                    // Lookup name columns (e.g. transactioncurrencyidname) exist in BOTH TDS and FabricLink
                    // Hidden ID column
                    columns.Add(new ColumnInfo
                    {
                        LogicalName = attr.LogicalName,
                        DisplayName = attr.LogicalName, // Use logical name as display for hidden column
                        SourceColumn = attr.LogicalName,
                        IsHidden = true,
                        IsKey = isPrimaryKey,
                        Description = description,
                        AttributeType = "lookup"
                    });
                    sqlFields.Add($"Base.{attr.LogicalName}");

                    // Visible name column - check if already processed (user may have selected both ID and name)
                    // Skip name column for owning* lookups (not available in TDS/Fabric endpoints)
                    var nameColumn = attr.LogicalName + "name";
                    var isOwningLookup = attr.LogicalName.Equals("owninguser", StringComparison.OrdinalIgnoreCase) ||
                                         attr.LogicalName.Equals("owningteam", StringComparison.OrdinalIgnoreCase) ||
                                         attr.LogicalName.Equals("owningbusinessunit", StringComparison.OrdinalIgnoreCase);
                    
                    if (!processedColumns.Contains(nameColumn) && !isOwningLookup)
                    {
                        var lookupSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                        columns.Add(new ColumnInfo
                        {
                            LogicalName = nameColumn,
                            DisplayName = effectiveName,
                            SourceColumn = lookupSourceCol,
                            IsHidden = false,
                            IsRowLabel = isPrimaryName,
                            Description = description,
                            AttributeType = "string"  // Name columns are always strings
                        });
                        sqlFields.Add(ApplySqlAlias($"Base.{nameColumn}", effectiveName, nameColumn, false));
                    }
                    processedColumns.Add(nameColumn);
                    processedColumns.Add(attr.LogicalName);
                }
                else if (isChoice || isBoolean)
                {
                    if (IsFabricLink)
                    {
                        // FabricLink: choice/boolean virtual name columns don't exist in Lakehouse SQL
                        // Instead, JOIN to metadata tables to get the localized label
                        var isState = attrType.Equals("State", StringComparison.OrdinalIgnoreCase);
                        var isStatus = attrType.Equals("Status", StringComparison.OrdinalIgnoreCase);
                        var nameColumn = attr.LogicalName + "name";
                        var joinAlias = $"{table.LogicalName}_{attr.LogicalName}";

                        if (isState)
                        {
                            // StateMetadata: join on EntityName, LanguageCode, State
                            // statecode is never null, so use INNER JOIN for performance
                            joinClauses.Add(
                                $"JOIN [StateMetadata] {joinAlias}\r\n" +
                                $"\t\t\t\t            ON  {joinAlias}.[EntityName] = '{table.LogicalName}'\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[LocalizedLabelLanguageCode] = {_languageCode}\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[State] = Base.{attr.LogicalName}");
                        }
                        else if (isStatus)
                        {
                            // StatusMetadata: join on EntityName, LanguageCode, State, and Status
                            // The [State] column in StatusMetadata corresponds to statecode
                            // The [Status] column in StatusMetadata corresponds to statuscode
                            // Both conditions are needed to get the specific status label (avoid row fan-out)
                            // statuscode is never null, so use INNER JOIN for performance
                            joinClauses.Add(
                                $"JOIN [StatusMetadata] {joinAlias}\r\n" +
                                $"\t\t\t\t            ON  {joinAlias}.[EntityName] = '{table.LogicalName}'\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[LocalizedLabelLanguageCode] = {_languageCode}\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[State] = Base.statecode\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[Status] = Base.statuscode");
                        }
                        else if (isBoolean)
                        {
                            // Boolean fields: use GlobalOptionsetMetadata with LEFT JOIN (value can be null)
                            var optionSetName = attr.OptionSetName ?? attrDisplayInfo?.OptionSetName ?? attr.LogicalName;
                            joinClauses.Add(
                                $"LEFT JOIN [GlobalOptionsetMetadata] {joinAlias}\r\n" +
                                $"\t\t\t\t            ON  {joinAlias}.[OptionSetName] = '{optionSetName}'\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[EntityName] = '{table.LogicalName}'\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[LocalizedLabelLanguageCode] = {_languageCode}\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[Option] = Base.{attr.LogicalName}");
                        }
                        else
                        {
                            // Picklist: determine GlobalOptionsetMetadata vs OptionsetMetadata
                            var isGlobal = attr.IsGlobal ?? attrDisplayInfo?.IsGlobal ?? false;
                            var optionSetName = attr.OptionSetName ?? attrDisplayInfo?.OptionSetName ?? attr.LogicalName;
                            var metadataTable = isGlobal ? "GlobalOptionsetMetadata" : "OptionsetMetadata";
                            joinClauses.Add(
                                $"LEFT JOIN [{metadataTable}] {joinAlias}\r\n" +
                                $"\t\t\t\t            ON  {joinAlias}.[OptionSetName] = '{optionSetName}'\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[EntityName] = '{table.LogicalName}'\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[LocalizedLabelLanguageCode] = {_languageCode}\r\n" +
                                $"\t\t\t\t            AND {joinAlias}.[Option] = Base.{attr.LogicalName}");
                        }

                        // SELECT the localized label aliased as {attributename}name
                        // Check if already processed (user may have selected virtual name column)
                        if (!processedColumns.Contains(nameColumn))
                        {
                            var fabricChoiceSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                            var fabricChoiceAlias = _useDisplayNameAliasesInSql && !effectiveName.Equals(nameColumn, StringComparison.OrdinalIgnoreCase)
                                ? $"{joinAlias}.[LocalizedLabel] AS [{effectiveName}]"
                                : $"{joinAlias}.[LocalizedLabel] {nameColumn}";
                            sqlFields.Add(fabricChoiceAlias);

                            // Column definition uses string type (the label text)
                            columns.Add(new ColumnInfo
                            {
                                LogicalName = nameColumn,
                                DisplayName = effectiveName,
                                SourceColumn = fabricChoiceSourceCol,
                                IsHidden = false,
                                IsRowLabel = isPrimaryName,
                                Description = description,
                                AttributeType = "string"  // Localized label is always a string
                            });
                        }
                        processedColumns.Add(nameColumn);
                        processedColumns.Add(attr.LogicalName);
                    }
                    else
                    {
                    // For Choice/Boolean: use the virtual attribute name from metadata
                    // Most follow pattern {attributename}name, but there are exceptions (e.g., donotsendmm -> donotsendmarketingmaterial)
                    var nameColumn = attrDisplayInfo?.VirtualAttributeName ?? (attr.LogicalName + "name");
                    
                    // Apply correction if this virtual column name needs fixing
                    var correctionKey = $"{table.LogicalName}.{nameColumn}";
                    if (VirtualColumnCorrections.TryGetValue(correctionKey, out var correctedName))
                    {
                        DebugLogger.Log($"CORRECTED: Virtual column {correctionKey} -> {correctedName} (base: {attr.LogicalName})");
                        nameColumn = correctedName;
                    }
                    
                    // Debug logging for virtual attribute name resolution
                    if (attrDisplayInfo == null)
                    {
                        DebugLogger.Log($"WARNING: No attrDisplayInfo for {table.LogicalName}.{attr.LogicalName}, using fallback: {nameColumn}");
                    }
                    else if (attrDisplayInfo.VirtualAttributeName == null)
                    {
                        DebugLogger.Log($"WARNING: VirtualAttributeName is null for {table.LogicalName}.{attr.LogicalName}, using fallback: {nameColumn}");
                    }
                    else if (attrDisplayInfo.VirtualAttributeName != nameColumn)
                    {
                        DebugLogger.Log($"Using virtual attribute: {table.LogicalName}.{attr.LogicalName} -> {nameColumn}");
                    }
                    
                    if (!processedColumns.Contains(nameColumn))
                    {
                        var tdsChoiceSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                        columns.Add(new ColumnInfo
                        {
                            LogicalName = nameColumn,
                            DisplayName = effectiveName,
                            SourceColumn = tdsChoiceSourceCol,
                            IsHidden = false,
                            IsRowLabel = isPrimaryName,
                            Description = description,
                            AttributeType = "string"  // Choice/Boolean name columns are always strings
                        });
                        sqlFields.Add(ApplySqlAlias($"Base.{nameColumn}", effectiveName, nameColumn, false));
                    }
                    processedColumns.Add(nameColumn);
                    processedColumns.Add(attr.LogicalName);
                    }
                }
                else if (isMultiSelectChoice)
                {
                    // Multi-select choice fields store comma-separated integer values
                    // FabricLink: uses {attributename}name pattern; TDS: uses actual VirtualAttributeName
                    string nameColumn;
                    
                    if (IsFabricLink)
                    {
                        // FabricLink: use OUTER APPLY with subquery instead of CTE for DirectQuery compatibility
                        // CTEs can break Power BI's query folding, so we use OUTER APPLY which is better supported
                        nameColumn = attr.LogicalName + "name";
                        var applyAlias = $"mspl_{attr.LogicalName}";
                        var joinAlias = $"meta_{attr.LogicalName}";
                        var isGlobal = attr.IsGlobal ?? attrDisplayInfo?.IsGlobal ?? false;
                        var optionSetName = attr.OptionSetName ?? attrDisplayInfo?.OptionSetName ?? attr.LogicalName;
                        var metadataTable = isGlobal ? "GlobalOptionsetMetadata" : "OptionsetMetadata";

                        // Use OUTER APPLY with a correlated subquery
                        joinClauses.Add(
                            $"OUTER APPLY (\r\n" +
                            $"\t\t\t\t        SELECT STRING_AGG({joinAlias}.[LocalizedLabel], ', ') AS {nameColumn}\r\n" +
                            $"\t\t\t\t        FROM STRING_SPLIT(CAST(Base.{attr.LogicalName} AS VARCHAR(4000)), ',') AS split\r\n" +
                            $"\t\t\t\t        JOIN [{metadataTable}] AS {joinAlias}\r\n" +
                            $"\t\t\t\t            ON  {joinAlias}.[OptionSetName] = '{optionSetName}'\r\n" +
                            $"\t\t\t\t            AND {joinAlias}.[EntityName] = '{table.LogicalName}'\r\n" +
                            $"\t\t\t\t            AND {joinAlias}.[LocalizedLabelLanguageCode] = {_languageCode}\r\n" +
                            $"\t\t\t\t            AND {joinAlias}.[Option] = CAST(LTRIM(RTRIM(split.value)) AS INT)\r\n" +
                            $"\t\t\t\t        WHERE Base.{attr.LogicalName} IS NOT NULL\r\n" +
                            $"\t\t\t\t    ) {applyAlias}");

                        if (!processedColumns.Contains(nameColumn))
                        {
                            sqlFields.Add(ApplySqlAlias($"{applyAlias}.{nameColumn}", effectiveName, nameColumn, false));
                        }
                    }
                    else
                    {
                        // TDS: use the actual virtual name column from metadata (e.g., donotsendmm -> donotsendmarketingmaterial)
                        nameColumn = attrDisplayInfo?.VirtualAttributeName ?? (attr.LogicalName + "name");
                        if (!processedColumns.Contains(nameColumn))
                        {
                            sqlFields.Add(ApplySqlAlias($"Base.{nameColumn}", effectiveName, nameColumn, false));
                        }
                    }

                    if (!processedColumns.Contains(nameColumn))
                    {
                        var msSourceCol = _useDisplayNameAliasesInSql ? effectiveName : nameColumn;
                        columns.Add(new ColumnInfo
                        {
                            LogicalName = nameColumn,
                            DisplayName = effectiveName,
                            SourceColumn = msSourceCol,
                            IsHidden = false,
                            IsRowLabel = isPrimaryName,
                            Description = description,
                            AttributeType = "string"  // Multi-select labels are always strings
                        });
                    }
                    processedColumns.Add(nameColumn);
                    processedColumns.Add(attr.LogicalName);
                }
                else
                {
                    // Regular column
                    var isDateTime = attrType.Equals("DateTime", StringComparison.OrdinalIgnoreCase);
                    var shouldWrapDateTime = isDateTime && dateTableConfig != null &&
                        dateTableConfig.WrappedFields.Any(f =>
                            f.TableName.Equals(table.LogicalName, StringComparison.OrdinalIgnoreCase) &&
                            f.FieldName.Equals(attr.LogicalName, StringComparison.OrdinalIgnoreCase));

                    // If wrapping datetime, change the data type to dateTime (date-only)
                    var effectiveAttrType = shouldWrapDateTime ? "dateonly" : attrType;
                    var regularSourceCol = isPrimaryKey ? attr.LogicalName : (_useDisplayNameAliasesInSql ? effectiveName : attr.LogicalName);

                    columns.Add(new ColumnInfo
                    {
                        LogicalName = attr.LogicalName,
                        DisplayName = isPrimaryKey ? attr.LogicalName : effectiveName,
                        SourceColumn = regularSourceCol,
                        IsHidden = isPrimaryKey,
                        IsKey = isPrimaryKey,
                        IsRowLabel = isPrimaryName,
                        Description = description,
                        AttributeType = effectiveAttrType
                    });

                    // Generate SQL field - wrap datetime if configured
                    if (shouldWrapDateTime)
                    {
                        var offset = dateTableConfig!.UtcOffsetHours;
                        var dtAlias = isPrimaryKey ? attr.LogicalName : (_useDisplayNameAliasesInSql ? effectiveName : attr.LogicalName);
                        var dtAliasClause = dtAlias.Equals(attr.LogicalName, StringComparison.OrdinalIgnoreCase)
                            ? $"AS {attr.LogicalName}" : $"AS [{dtAlias}]";
                        sqlFields.Add($"CAST(DATEADD(hour, {offset}, Base.{attr.LogicalName}) AS DATE) {dtAliasClause}");
                    }
                    else
                    {
                        sqlFields.Add(ApplySqlAlias($"Base.{attr.LogicalName}", effectiveName, attr.LogicalName, isPrimaryKey));
                    }
                    processedColumns.Add(attr.LogicalName);
                }
            }

            // Write columns
            foreach (var col in columns)
            {
                // Add column description
                if (!string.IsNullOrEmpty(col.Description))
                {
                    sb.AppendLine($"\t/// {col.Description}");
                }

                // Map the data type
                var (dataType, formatString, sourceProviderType, summarizeBy) = MapDataType(col.AttributeType);
                var isDateTime = col.AttributeType?.Equals("dateonly", StringComparison.OrdinalIgnoreCase) == true ||
                                 col.AttributeType?.Equals("datetime", StringComparison.OrdinalIgnoreCase) == true;

                sb.AppendLine($"\tcolumn {QuoteTmdlName(col.DisplayName)}");
                sb.AppendLine($"\t\tdataType: {dataType}");
                if (formatString != null)
                {
                    sb.AppendLine($"\t\tformatString: {formatString}");
                }
                if (sourceProviderType != null)
                {
                    sb.AppendLine($"\t\tsourceProviderType: {sourceProviderType}");
                }
                if (col.IsHidden)
                {
                    sb.AppendLine($"\t\tisHidden");
                }
                if (col.IsKey)
                {
                    sb.AppendLine($"\t\tisKey");
                }
                sb.AppendLine($"\t\tlineageTag: {Guid.NewGuid()}");
                if (col.IsRowLabel)
                {
                    sb.AppendLine($"\t\tisDefaultLabel");
                }
                sb.AppendLine($"\t\tsummarizeBy: {summarizeBy}");
                sb.AppendLine($"\t\tsourceColumn: {col.SourceColumn}");
                sb.AppendLine();
                if (isDateTime)
                {
                    sb.AppendLine($"\t\tchangedProperty = DataType");
                    sb.AppendLine();
                }
                sb.AppendLine($"\t\tannotation SummarizationSetBy = Automatic");
                if (isDateTime)
                {
                    sb.AppendLine();
                    sb.AppendLine($"\t\tannotation UnderlyingDateTimeDataType = Date");
                }
                sb.AppendLine();
            }

            // Write partition (Power Query)
            // FabricLink: table names are lowercase, no schema prefix
            // TDS: uses schemaName (e.g. Opportunity, Account)
            var fromTable = IsFabricLink ? table.LogicalName : (table.SchemaName ?? table.LogicalName);

            // Build SQL SELECT list with proper formatting
            var sqlSelectList = new StringBuilder();
            for (int i = 0; i < sqlFields.Count; i++)
            {
                if (i == 0)
                {
                    sqlSelectList.Append($"SELECT {sqlFields[i]}");
                }
                else
                {
                    sqlSelectList.Append($"\r\n\t\t\t\t        ,{sqlFields[i]}");
                }
            }

            // Partition name matches table display name (PBI Desktop requires this for DirectQuery evaluation)
            var partitionName = displayName;

            // Auto-generate measures for fact tables
            if (table.Role == "Fact")
            {
                var entityLogicalName = table.LogicalName;
                var factPrimaryKey = table.PrimaryIdAttribute ?? (table.LogicalName + "id");

                // Link measure: builds a URL to open the record in Dynamics 365
                sb.AppendLine($"\tmeasure 'Link to {displayName}' = ```");
                sb.AppendLine($"\t\t\t");
                sb.AppendLine($"\t\t\t\"https://\" & DataverseURL & \"/main.aspx?pagetype=entityrecord&etn={entityLogicalName}&id=\" ");
                sb.AppendLine($"\t\t\t\t& SELECTEDVALUE('{displayName}'[{factPrimaryKey}], BLANK())");
                sb.AppendLine($"\t\t\t```");
                sb.AppendLine($"\t\tlineageTag: {Guid.NewGuid()}");
                sb.AppendLine($"\t\tdataCategory: WebUrl");
                sb.AppendLine();

                // Count measure: counts rows in the fact table
                sb.AppendLine($"\tmeasure '{displayName} Count' = COUNTROWS('{displayName}')");
                sb.AppendLine($"\t\tformatString: 0");
                sb.AppendLine($"\t\tlineageTag: {Guid.NewGuid()}");
                sb.AppendLine();
            }

            sb.AppendLine($"\tpartition {QuoteTmdlName(partitionName)} = m");
            sb.AppendLine($"\t\tmode: {GetPartitionMode(table.Role, table.LogicalName)}");
            sb.AppendLine($"\t\tsource =");
            sb.AppendLine($"\t\t\t\tlet");
            if (IsFabricLink)
            {
                // FabricLink: uses Sql.Database with inline Query parameter
                sb.AppendLine($"\t\t\t\t    Source = Sql.Database(FabricSQLEndpoint, FabricLakehouse,");
                sb.AppendLine($"\t\t\t\t    [Query=\"");
            }
            else
            {
                // TDS: reference the DataverseURL parameter table.
                // DataverseURL must be a table with mode: import and IsParameterQuery=true
                // ("Enable Load" checked) — otherwise PBI Desktop throws KeyNotFoundException.
                sb.AppendLine($"\t\t\t\t    Dataverse = CommonDataService.Database(DataverseURL,[CreateNavigationProperties=false]),");
                sb.AppendLine($"\t\t\t\t    Source = Value.NativeQuery(Dataverse,\"");
            }
            
            // Add blank line after query opening
            sb.AppendLine($"\t\t\t\t");

            // Add filter comment if present
            if (!string.IsNullOrWhiteSpace(viewFilterComment))
            {
                foreach (var line in viewFilterComment.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"\t\t\t\t    {line}");
                    }
                }
                sb.AppendLine($"\t\t\t\t");
            }
            
            sb.AppendLine($"\t\t\t\t    {sqlSelectList}");
            sb.AppendLine($"\t\t\t\t    FROM {fromTable} AS Base");

            // FabricLink: add JOIN and OUTER APPLY clauses for choice/optionset metadata
            foreach (var joinClause in joinClauses)
            {
                sb.AppendLine($"\t\t\t\t    {joinClause}");
            }
            
            // Build WHERE clause - use view filter if present, otherwise default statecode filter
            if (!string.IsNullOrWhiteSpace(viewFilterClause))
            {
                // View filter is present - use it (it likely already includes statecode condition)
                sb.AppendLine($"\t\t\t\t    WHERE {viewFilterClause}");
            }
            else if (table.HasStateCode)
            {
                // No view filter - apply default active records filter
                sb.AppendLine($"\t\t\t\t    WHERE Base.statecode = 0");
            }
            if (IsFabricLink)
            {
                sb.AppendLine($"\t\t\t\t        \"");
                sb.AppendLine($"\t\t\t\t    , CreateNavigationProperties=false])");
            }
            else
            {
                sb.AppendLine($"\t\t\t\t    \" ,null ,[EnableFolding=true])");
            }
            sb.AppendLine($"\t\t\t\tin");
            sb.AppendLine($"\t\t\t\t    Source");
            sb.AppendLine();
            sb.AppendLine($"\tannotation PBI_NavigationStepName = Navigation");
            sb.AppendLine();
            sb.AppendLine($"\tannotation PBI_ResultType = Table");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Generates FabricLink expressions TMDL (FabricSQLEndpoint, FabricLakehouse, and DataverseURL parameters)
        /// </summary>
        private string GenerateFabricLinkExpressions(string endpoint, string database)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"expression FabricSQLEndpoint = \"{endpoint}\" meta [IsParameterQuery=true, Type=\"Any\", IsParameterQueryRequired=true]");
            sb.AppendLine($"\tlineageTag: {Guid.NewGuid()}");
            sb.AppendLine();
            sb.AppendLine("\tannotation PBI_ResultType = Text");
            sb.AppendLine();

            sb.AppendLine($"expression FabricLakehouse = \"{database}\" meta [IsParameterQuery=true, Type=\"Any\", IsParameterQueryRequired=true]");
            sb.AppendLine($"\tlineageTag: {Guid.NewGuid()}");
            sb.AppendLine();
            sb.AppendLine("\tannotation PBI_NavigationStepName = Navigation");
            sb.AppendLine();
            sb.AppendLine("\tannotation PBI_ResultType = Text");

            return sb.ToString();
        }

        /// <summary>
        /// Finds an existing date table in the model by checking for dataCategory: Time
        /// </summary>
        private string? FindExistingDateTable(string tablesFolder)
        {
            if (!Directory.Exists(tablesFolder))
                return null;

            var tmdlFiles = Directory.GetFiles(tablesFolder, "*.tmdl");
            foreach (var file in tmdlFiles)
            {
                try
                {
                    var content = File.ReadAllText(file, Utf8WithoutBom);
                    // Check if this table has dataCategory: Time
                    if (Regex.IsMatch(content, @"^\s*dataCategory:\s*Time\s*$", RegexOptions.Multiline))
                    {
                        var tableName = Path.GetFileNameWithoutExtension(file);
                        DebugLogger.Log($"Found date table with dataCategory: Time - {tableName}");
                        return tableName;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Warning: Could not read {file}: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Generates the Date table TMDL from template with configured year range
        /// </summary>
        private string GenerateDateTableTmdl(DateTableConfig config)
        {
            // Read the template from embedded resource
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "DataverseToPowerBI.XrmToolBox.Assets.DateTable.tmdl";
            
            string content;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException($"Date table template not found as embedded resource: {resourceName}");
                }
                using (var reader = new System.IO.StreamReader(stream))
                {
                    content = reader.ReadToEnd();
                }
            }

            // Update _startdate: DATE(YYYY,1,1) -> DATE(config.StartYear,1,1)
            content = Regex.Replace(
                content,
                @"VAR _startdate\s*=\s*\r?\n\s*DATE\(\d+,\s*1,\s*1\)",
                $"VAR _startdate =\r\n\t\t\t\t    DATE({config.StartYear},1,1)",
                RegexOptions.Multiline);

            // Update _enddate: DATE(YYYY,1,1)-1 -> DATE(config.EndYear+1,1,1)-1
            content = Regex.Replace(
                content,
                @"VAR _enddate\s*=\s*\r?\n\s*DATE\(\d+,\s*1,\s*1\)\s*-\s*1",
                $"VAR _enddate =\r\n\t\t\t\t\tDATE({config.EndYear + 1},1,1)-1",
                RegexOptions.Multiline);

            return content;
        }

        /// <summary>
        /// Generates relationships TMDL content
        /// </summary>
        private string GenerateRelationshipsTmdl(
            List<ExportTable> tables,
            List<ExportRelationship> relationships,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            DateTableConfig? dateTableConfig = null)
        {
            var sb = new StringBuilder();

            // Create lookup for table display names
            var tableDisplayNames = tables.ToDictionary(
                t => t.LogicalName,
                t => t.DisplayName ?? t.SchemaName ?? t.LogicalName,
                StringComparer.OrdinalIgnoreCase);

            // Create lookup for primary key attributes
            var tablePrimaryKeys = tables.ToDictionary(
                t => t.LogicalName,
                t => t.PrimaryIdAttribute ?? t.LogicalName + "id",
                StringComparer.OrdinalIgnoreCase);

            foreach (var rel in relationships)
            {
                // Skip if either table is not in the model
                if (!tableDisplayNames.ContainsKey(rel.SourceTable) || !tableDisplayNames.ContainsKey(rel.TargetTable))
                    continue;

                var sourceTableDisplay = tableDisplayNames[rel.SourceTable];
                var targetTableDisplay = tableDisplayNames[rel.TargetTable];
                var targetPrimaryKey = tablePrimaryKeys[rel.TargetTable];

                // CRITICAL: In TMDL, relationships must reference columns by their logical names
                // as they appear in the column declarations, not by display names.
                // Column names in TMDL are the logical names (e.g., 'parentaccountid', 'stageid')
                var sourceColumn = rel.SourceAttribute;
                var targetColumn = targetPrimaryKey;

                sb.AppendLine($"relationship {Guid.NewGuid()}");
                
                // Add relyOnReferentialIntegrity if lookup is required OR if snowflake
                if (rel.AssumeReferentialIntegrity || rel.IsSnowflake)
                {
                    sb.AppendLine($"\trelyOnReferentialIntegrity");
                }

                // Set inactive if not active
                if (!rel.IsActive)
                {
                    sb.AppendLine($"\tisActive: false");
                }

                sb.AppendLine($"\tfromColumn: {QuoteTmdlName(sourceTableDisplay)}.{QuoteTmdlName(sourceColumn)}");
                sb.AppendLine($"\ttoColumn: {QuoteTmdlName(targetTableDisplay)}.{QuoteTmdlName(targetColumn)}");
                sb.AppendLine();
            }

            // Add Date table relationship if configured
            if (dateTableConfig != null && 
                !string.IsNullOrEmpty(dateTableConfig.PrimaryDateTable) && 
                !string.IsNullOrEmpty(dateTableConfig.PrimaryDateField) &&
                tableDisplayNames.ContainsKey(dateTableConfig.PrimaryDateTable))
            {
                var sourceTableDisplay = tableDisplayNames[dateTableConfig.PrimaryDateTable];
                var sourceTable = tables.FirstOrDefault(t =>
                    t.LogicalName.Equals(dateTableConfig.PrimaryDateTable, StringComparison.OrdinalIgnoreCase));
                var dateAttr = sourceTable?.Attributes
                    .FirstOrDefault(a => a.LogicalName.Equals(dateTableConfig.PrimaryDateField, StringComparison.OrdinalIgnoreCase));

                if (dateAttr != null)
                {
                    var isDateFieldRequired = false;
                    var primaryDateFieldName = dateAttr.DisplayName ?? dateAttr.SchemaName ?? dateAttr.LogicalName;
                    if (attributeDisplayInfo.TryGetValue(dateTableConfig.PrimaryDateTable, out var tableAttrs) &&
                        tableAttrs.TryGetValue(dateTableConfig.PrimaryDateField, out var fieldDisplayInfo))
                    {
                        isDateFieldRequired = fieldDisplayInfo.IsRequired;
                        primaryDateFieldName = GetEffectiveDisplayName(fieldDisplayInfo, fieldDisplayInfo.DisplayName ?? primaryDateFieldName);
                    }

                    sb.AppendLine($"relationship {Guid.NewGuid()}");
                    
                    // Add relyOnReferentialIntegrity if the date field is required
                    if (isDateFieldRequired)
                    {
                        sb.AppendLine($"\trelyOnReferentialIntegrity");
                    }
                    
                    sb.AppendLine($"\tfromColumn: {QuoteTmdlName(sourceTableDisplay)}.{QuoteTmdlName(primaryDateFieldName)}");
                    sb.AppendLine($"\ttoColumn: Date.Date");
                    sb.AppendLine();
                }
                else
                {
                    DebugLogger.Log($"Date relationship skipped: '{dateTableConfig.PrimaryDateTable}.{dateTableConfig.PrimaryDateField}' not found in selected attributes.");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds a description string for a column
        /// </summary>
        private string BuildDescription(string tableLogicalName, string attrLogicalName, string schemaName, string? dataverseDescription, List<string>? targets)
        {
            var parts = new List<string>();

            // Add Dataverse description first if available
            if (!string.IsNullOrWhiteSpace(dataverseDescription))
            {
                parts.Add(dataverseDescription);
            }

            // Add Source information
            parts.Add($"Source: {tableLogicalName}.{attrLogicalName}");

            // Add targets if available
            if (targets != null && targets.Any())
            {
                parts.Add($"Targets: {string.Join(", ", targets)}");
            }

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Maps Dataverse attribute types to Power BI data types.
        /// FabricLink note: The Fabric SQL endpoint returns money/decimal as float,
        /// so PBI Desktop will change them to double. We generate double directly for
        /// FabricLink to avoid false change detection on subsequent rebuilds.
        /// </summary>
        private (string dataType, string? formatString, string? sourceProviderType, string summarizeBy) MapDataType(string? attributeType)
        {
            if (attributeType == null || attributeType.Length == 0)
                return ("string", null, "nvarchar", "none");

            var normalizedType = attributeType!.ToLowerInvariant();

            if (IsFabricLink)
            {
                return normalizedType switch
                {
                    // Numeric types - Fabric SQL endpoint returns money/decimal as float (double)
                    "integer" => ("int64", "0", "int", "sum"),
                    "bigint" => ("int64", "0", "bigint", "sum"),
                    "decimal" => ("double", "#,0.00", null, "sum"),
                    "double" => ("double", "#,0.00", null, "sum"),
                    "money" => ("double", "\\$#,0.00;(\\$#,0.00);\\$#,0.00", null, "sum"),
                    
                    // Date/Time types
                    "datetime" => ("dateTime", "Short Date", "datetime2", "none"),
                    "dateonly" => ("dateTime", "Short Date", "datetime2", "none"),
                    
                    // Boolean types
                    "boolean" => ("boolean", null, "bit", "none"),
                    
                    // GUID types
                    "lookup" or "owner" or "customer" or "uniqueidentifier" => ("string", null, "uniqueidentifier", "none"),
                    
                    // Text types
                    "string" or "memo" or "picklist" or "state" or "status" or "multiselectpicklist" => ("string", null, "nvarchar", "none"),
                    
                    _ => ("string", null, "nvarchar", "none")
                };
            }

            return normalizedType switch
            {
                // Numeric types
                // PBI Desktop converts money/decimal to double on TDS too — match that
                // to avoid false change detection ("Changed: dataType: double → decimal")
                "integer" => ("int64", "0", "int", "sum"),
                "bigint" => ("int64", "0", "bigint", "sum"),
                "decimal" => ("double", "#,0.00", null, "sum"),
                "double" => ("double", "#,0.00", null, "sum"),
                "money" => ("double", "\\$#,0.00;(\\$#,0.00);\\$#,0.00", null, "sum"),
                
                // Date/Time types
                "datetime" => ("dateTime", "Short Date", "datetime2", "none"),
                "dateonly" => ("dateTime", "Short Date", "datetime2", "none"),  // Date-only (no time component, timezone adjusted)
                
                // Boolean types
                "boolean" => ("boolean", null, "bit", "none"),
                
                // GUID types (lookups are GUIDs in the database)
                "lookup" or "owner" or "customer" or "uniqueidentifier" => ("string", null, "uniqueidentifier", "none"),
                
                // Text types
                "string" or "memo" or "picklist" or "state" or "status" or "multiselectpicklist" => ("string", null, "nvarchar", "none"),
                
                _ => ("string", null, "nvarchar", "none")
            };
        }

        /// <summary>
        /// Maps attribute type to Power Query type expression
        /// </summary>
        private string MapToPowerQueryType(string? attributeType)
        {
            if (attributeType == null || attributeType.Length == 0)
                return "type text";

            var normalizedType = attributeType.ToLowerInvariant();
            return normalizedType switch
            {
                // Numeric types
                "integer" or "bigint" => "Int64.Type",
                "decimal" or "money" => "type number",
                "double" => "type number",
                
                // Date/Time types
                "datetime" => "type datetime",
                "dateonly" => "type date",
                
                // Boolean types
                "boolean" => "type logical",
                
                // Text types (including Lookup/Choice which use 'name' suffix)
                _ => "type text"
            };
        }

        /// <summary>
        /// Verifies that essential PBIP structure exists and creates missing pieces
        /// </summary>
        private void VerifyPbipStructure(string pbipFolder, string projectName)
        {
            // Discover the template name from the template folder
            var templatePbipFiles = Directory.GetFiles(_templatePath, "*.pbip");
            if (templatePbipFiles.Length == 0)
            {
                throw new FileNotFoundException($"No .pbip file found in template folder: {_templatePath}");
            }
            var templateName = Path.GetFileNameWithoutExtension(templatePbipFiles[0]);

            // Check for .pbip file
            var pbipFile = Path.Combine(pbipFolder, $"{projectName}.pbip");
            if (!File.Exists(pbipFile))
            {
                DebugLogger.Log($"Missing .pbip file, recreating: {pbipFile}");
                var templatePbip = Path.Combine(_templatePath, $"{templateName}.pbip");
                if (File.Exists(templatePbip))
                {
                    var content = File.ReadAllText(templatePbip, Utf8WithoutBom);
                    content = content.Replace(templateName, projectName);
                    WriteTmdlFile(pbipFile, content);
                }
                else
                {
                    throw new FileNotFoundException($"Template .pbip file not found at: {templatePbip}");
                }
            }

            // Check for Report folder
            var reportFolder = Path.Combine(pbipFolder, $"{projectName}.Report");
            if (!Directory.Exists(reportFolder))
            {
                DebugLogger.Log($"Missing Report folder, recreating: {reportFolder}");
                var templateReport = Path.Combine(_templatePath, $"{templateName}.Report");
                if (Directory.Exists(templateReport))
                {
                    CopyDirectory(templateReport, reportFolder, projectName, templateName);
                }
                else
                {
                    throw new DirectoryNotFoundException($"Template Report folder not found at: {templateReport}");
                }
            }

            // Check for SemanticModel .platform file
            var platformFile = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", ".platform");
            if (!File.Exists(platformFile))
            {
                DebugLogger.Log($"Missing .platform file, recreating: {platformFile}");
                var templatePlatform = Path.Combine(_templatePath, $"{templateName}.SemanticModel", ".platform");
                if (File.Exists(templatePlatform))
                {
                    var content = File.ReadAllText(templatePlatform, Utf8WithoutBom);
                    content = content.Replace(templateName, projectName);
                    WriteTmdlFile(platformFile, content);
                }
                else
                {
                    throw new FileNotFoundException($"Template .platform file not found at: {templatePlatform}");
                }
            }

            // Check for definition.pbism
            var pbismFile = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition.pbism");
            if (!File.Exists(pbismFile))
            {
                DebugLogger.Log($"Missing definition.pbism file, recreating: {pbismFile}");
                var templatePbism = Path.Combine(_templatePath, $"{templateName}.SemanticModel", "definition.pbism");
                if (File.Exists(templatePbism))
                {
                    var content = File.ReadAllText(templatePbism, Utf8WithoutBom);
                    content = content.Replace(templateName, projectName);
                    WriteTmdlFile(pbismFile, content);
                }
                else
                {
                    throw new FileNotFoundException($"Template definition.pbism file not found at: {templatePbism}");
                }
            }
        }

        /// <summary>
        /// Sanitizes a string to be used as a file name
        /// </summary>
        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("", name.Select(c => invalid.Contains(c) ? '_' : c));
        }

        /// <summary>
        /// Quotes a TMDL name if it contains spaces or special characters
        /// </summary>
        private string QuoteTmdlName(string name)
        {
            // TMDL requires quotes for names with spaces, special characters, or starting with numbers
            if (name.Contains(' ') || name.Contains('-') || name.Contains('.') || 
                name.Contains('[') || name.Contains(']') || name.Contains('(') || 
                name.Contains(')') || char.IsDigit(name[0]))
            {
                return $"'{name}'";
            }
            return name;
        }

        /// <summary>
        /// Helper class for column information
        /// </summary>
        private class ColumnInfo
        {
            public string LogicalName { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string SourceColumn { get; set; } = "";
            public bool IsHidden { get; set; }
            public string? Description { get; set; }
            public string? AttributeType { get; set; }  // Dataverse attribute type for data type mapping
            public bool IsKey { get; set; }  // Marks this as the key column (Primary ID)
            public bool IsRowLabel { get; set; }  // Marks this as the row label (Primary Name)
        }

        /// <summary>
        /// Generates TMDL content for preview without writing files.
        /// Returns a dictionary of entry name → TmdlPreviewEntry with content and type.
        /// </summary>
        public Dictionary<string, TmdlPreviewEntry> GenerateTmdlPreview(
            string dataverseUrl,
            List<ExportTable> tables,
            List<ExportRelationship> relationships,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            DateTableConfig? dateTableConfig = null)
        {
            var result = new Dictionary<string, TmdlPreviewEntry>();

            // Normalize URL for DataverseURL table
            var normalizedUrl = dataverseUrl;
            if (normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                normalizedUrl = normalizedUrl.Substring(8);

            // Expressions / DataverseURL entries
            if (IsFabricLink)
            {
                var fabricExpressions = GenerateFabricLinkExpressions(
                    _fabricLinkEndpoint ?? "", _fabricLinkDatabase ?? "");
                result["Expressions"] = new TmdlPreviewEntry
                {
                    Content = fabricExpressions,
                    EntryType = TmdlEntryType.Expression
                };
            }

            result["DataverseURL"] = new TmdlPreviewEntry
            {
                Content = GenerateDataverseUrlTableTmdl(normalizedUrl),
                EntryType = TmdlEntryType.Expression
            };

            // Date table
            if (dateTableConfig != null)
            {
                result["Date"] = new TmdlPreviewEntry
                {
                    Content = GenerateDateTableTmdl(dateTableConfig),
                    EntryType = TmdlEntryType.DateTable
                };
            }

            // Compute required lookup columns per table (same as Build())
            var relationshipColumnsPerTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in relationships)
            {
                if (!relationshipColumnsPerTable.ContainsKey(rel.SourceTable))
                    relationshipColumnsPerTable[rel.SourceTable] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                relationshipColumnsPerTable[rel.SourceTable].Add(rel.SourceAttribute);
            }

            // Per-table TMDL
            foreach (var table in tables)
            {
                var requiredLookupColumns = relationshipColumnsPerTable.ContainsKey(table.LogicalName)
                    ? relationshipColumnsPerTable[table.LogicalName]
                    : new HashSet<string>();

                var tableTmdl = GenerateTableTmdl(table, attributeDisplayInfo, requiredLookupColumns, dateTableConfig);
                var displayName = table.DisplayName ?? table.SchemaName ?? table.LogicalName;
                var entryType = string.Equals(table.Role, "Fact", StringComparison.OrdinalIgnoreCase)
                    ? TmdlEntryType.FactTable
                    : TmdlEntryType.DimensionTable;

                result[displayName] = new TmdlPreviewEntry
                {
                    Content = tableTmdl,
                    EntryType = entryType
                };
            }

            return result;
        }
    }

    /// <summary>
    /// Type of TMDL preview entry, also controls sort order in the preview dialog.
    /// </summary>
    public enum TmdlEntryType
    {
        FactTable = 0,
        DimensionTable = 1,
        DateTable = 2,
        Expression = 3
    }

    /// <summary>
    /// A single TMDL preview entry with content and type metadata.
    /// </summary>
    public class TmdlPreviewEntry
    {
        public string Content { get; set; } = "";
        public TmdlEntryType EntryType { get; set; }
    }
}
