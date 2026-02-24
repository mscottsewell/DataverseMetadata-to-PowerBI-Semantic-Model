// ===================================================================================
// SemanticModelManager.cs - Central Configuration Management for Semantic Models
// ===================================================================================
//
// PURPOSE:
// This class manages the central registry of all semantic model configurations
// across multiple Dataverse environments. It provides a single source of truth for
// model settings, enabling users to switch between environments and models easily.
//
// STORAGE LOCATION:
// All configurations are stored in:
//   %APPDATA%\MscrmTools\XrmToolBox\Settings\DataverseToPowerBI\semantic-models.json
//
// This follows XrmToolBox conventions for plugin settings storage.
//
// DATA MODEL:
// - SemanticModelsFile: Root container for all models and global settings
// - SemanticModelConfig: Individual model configuration including:
//   - Name and Dataverse URL
//   - Selected solution, tables, and attributes
//   - Star-schema configuration (fact table, relationships)
//   - Date table settings
//   - Working folder path
//
// KEY OPERATIONS:
// - GetModelsForEnvironment(): List models for a specific Dataverse environment
// - GetOrCreateModel(): Get existing or create new model by name
// - SaveModel(): Persist model configuration to JSON
// - DeleteModel(): Remove model from registry
// - SetCurrentModel(): Track the active model for quick access
//
// SERIALIZATION:
// Uses DataContractJsonSerializer for compatibility with .NET Framework 4.6.2
// (required by XrmToolBox). This ensures proper handling of:
// - Dictionary serialization
// - Nullable types
// - DateTime values
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using DataverseToPowerBI.XrmToolBox.Services;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Manages semantic model configurations stored centrally for all environments.
    /// Models are stored in %APPDATA%\MscrmTools\XrmToolBox\Settings\DataverseToPowerBI\semantic-models.json
    /// </summary>
    public class SemanticModelManager
    {
        private const string ModelsFileName = "semantic-models.json";
        private readonly string _settingsFolder;
        private readonly string _modelsFilePath;
        private SemanticModelsFile _modelsFile = null!;

        public SemanticModelManager()
        {
            _settingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MscrmTools", "XrmToolBox", "Settings", "DataverseToPowerBI");
            Directory.CreateDirectory(_settingsFolder);
            _modelsFilePath = Path.Combine(_settingsFolder, ModelsFileName);
            
            LoadModelsFile();
        }

        public string SettingsFolder => _settingsFolder;

        private void LoadModelsFile()
        {
            _modelsFile = new SemanticModelsFile();

            if (File.Exists(_modelsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_modelsFilePath);
                    using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(SemanticModelsFile));
                        _modelsFile = (SemanticModelsFile)serializer.ReadObject(ms) ?? new SemanticModelsFile();
                    }
                }
                catch (SerializationException ex)
                {
                    DebugLogger.Log($"Configuration file corrupt: {ex.Message}\n{ex.StackTrace}");

                    // Backup the corrupt file so the user doesn't lose data permanently
                    try
                    {
                        var backupPath = _modelsFilePath + ".bak";
                        File.Copy(_modelsFilePath, backupPath, overwrite: true);
                    }
                    catch (Exception backupEx)
                    {
                        DebugLogger.Log($"Failed to create backup of corrupt config: {backupEx.Message}");
                    }

                    MessageBox.Show(
                        "Configuration file could not be loaded and may be corrupt. " +
                        "A backup has been created. Settings have been reset to defaults.",
                        "DataverseToPowerBI",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    _modelsFile = new SemanticModelsFile();
                }
            }
        }

        private void SaveModelsFile()
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(SemanticModelsFile));
                    serializer.WriteObject(ms, _modelsFile);
                    File.WriteAllText(_modelsFilePath, Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save semantic models: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all semantic models
        /// </summary>
        public List<SemanticModelConfig> GetAllModels()
        {
            return _modelsFile.Models.ToList();
        }

        /// <summary>
        /// Get models for a specific environment URL
        /// </summary>
        public List<SemanticModelConfig> GetModelsForEnvironment(string environmentUrl)
        {
            var normalized = NormalizeUrl(environmentUrl);
            return _modelsFile.Models
                .Where(m => NormalizeUrl(m.DataverseUrl) == normalized)
                .OrderByDescending(m => m.LastUsed)
                .ToList();
        }

        /// <summary>
        /// Get the current/last-used model name
        /// </summary>
        public string GetCurrentModelName()
        {
            return _modelsFile.CurrentModelName;
        }

        /// <summary>
        /// Get a model by name
        /// </summary>
        public SemanticModelConfig GetModel(string name)
        {
            return _modelsFile.Models.FirstOrDefault(m => m.Name == name);
        }

        /// <summary>
        /// Get the most recently used model for an environment
        /// </summary>
        public SemanticModelConfig GetMostRecentModelForEnvironment(string environmentUrl)
        {
            var normalized = NormalizeUrl(environmentUrl);
            return _modelsFile.Models
                .Where(m => NormalizeUrl(m.DataverseUrl) == normalized)
                .OrderByDescending(m => m.LastUsed)
                .FirstOrDefault();
        }

        /// <summary>
        /// Check if a model with the given name exists
        /// </summary>
        public bool ModelExists(string name)
        {
            return _modelsFile.Models.Any(m => 
                m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Create a new semantic model
        /// </summary>
        public void CreateModel(SemanticModelConfig model)
        {
            if (ModelExists(model.Name))
            {
                throw new Exception($"A semantic model named '{model.Name}' already exists.");
            }

            model.CreatedDate = DateTime.Now;
            model.LastUsed = DateTime.Now;
            _modelsFile.Models.Add(model);
            _modelsFile.CurrentModelName = model.Name;
            SaveModelsFile();
        }

        /// <summary>
        /// Save/update an existing model
        /// </summary>
        public void SaveModel(SemanticModelConfig model)
        {
            var existing = _modelsFile.Models.FirstOrDefault(m => m.Name == model.Name);
            if (existing == null)
            {
                throw new Exception($"Semantic model '{model.Name}' not found.");
            }

            // Update the existing model
            existing.DataverseUrl = model.DataverseUrl;
            existing.ConnectionType = model.ConnectionType;
            existing.FabricLinkSQLEndpoint = model.FabricLinkSQLEndpoint;
            existing.FabricLinkSQLDatabase = model.FabricLinkSQLDatabase;
            existing.WorkingFolder = model.WorkingFolder;
            existing.TemplatePath = model.TemplatePath;
            existing.LastUsed = DateTime.Now;
            existing.PluginSettings = model.PluginSettings;
            
            SaveModelsFile();
        }

        /// <summary>
        /// Set a model as the current/active model
        /// </summary>
        public void SetCurrentModel(string name)
        {
            var model = _modelsFile.Models.FirstOrDefault(m => m.Name == name);
            if (model == null)
            {
                throw new Exception($"Semantic model '{name}' not found.");
            }

            model.LastUsed = DateTime.Now;
            _modelsFile.CurrentModelName = name;
            SaveModelsFile();
        }

        /// <summary>
        /// Copy a model to a new name
        /// </summary>
        public void CopyModel(string sourceName, string newName)
        {
            var source = _modelsFile.Models.FirstOrDefault(m => m.Name == sourceName);
            if (source == null)
            {
                throw new Exception($"Semantic model '{sourceName}' not found.");
            }

            if (ModelExists(newName))
            {
                throw new Exception($"A semantic model named '{newName}' already exists.");
            }

            var copy = new SemanticModelConfig
            {
                Name = newName,
                DataverseUrl = source.DataverseUrl,
                ConnectionType = source.ConnectionType,
                FabricLinkSQLEndpoint = source.FabricLinkSQLEndpoint,
                FabricLinkSQLDatabase = source.FabricLinkSQLDatabase,
                WorkingFolder = source.WorkingFolder,
                TemplatePath = source.TemplatePath,
                    IncludeChoiceNumericValueAsHiddenAttributes = source.IncludeChoiceNumericValueAsHiddenAttributes,
                CreatedDate = DateTime.Now,
                LastUsed = DateTime.Now,
                PluginSettings = ClonePluginSettings(source.PluginSettings)
            };

            _modelsFile.Models.Add(copy);
            _modelsFile.CurrentModelName = newName;
            SaveModelsFile();
        }

        /// <summary>
        /// Rename a model
        /// </summary>
        public void RenameModel(string oldName, string newName)
        {
            var model = _modelsFile.Models.FirstOrDefault(m => m.Name == oldName);
            if (model == null)
            {
                throw new Exception($"Semantic model '{oldName}' not found.");
            }

            if (ModelExists(newName))
            {
                throw new Exception($"A semantic model named '{newName}' already exists.");
            }

            model.Name = newName;
            if (_modelsFile.CurrentModelName == oldName)
            {
                _modelsFile.CurrentModelName = newName;
            }
            SaveModelsFile();
        }

        /// <summary>
        /// Delete a model
        /// </summary>
        public void DeleteModel(string name)
        {
            var model = _modelsFile.Models.FirstOrDefault(m => m.Name == name);
            if (model == null)
            {
                throw new Exception($"Semantic model '{name}' not found.");
            }

            if (_modelsFile.Models.Count <= 1)
            {
                throw new Exception("Cannot delete the last semantic model.");
            }

            _modelsFile.Models.Remove(model);

            // If we deleted the current model, switch to most recent
            if (_modelsFile.CurrentModelName == name)
            {
                var mostRecent = _modelsFile.Models
                    .OrderByDescending(m => m.LastUsed)
                    .First();
                _modelsFile.CurrentModelName = mostRecent.Name;
            }

            SaveModelsFile();
        }

        /// <summary>
        /// Check if this is first run (no models exist)
        /// </summary>
        public bool IsFirstRun()
        {
            return _modelsFile.Models.Count == 0;
        }

        /// <summary>
        /// Check if template is installed in settings folder
        /// </summary>
        public bool IsTemplateInstalled()
        {
            var templatePath = Path.Combine(_settingsFolder, "PBIP_DefaultTemplate");
            return Directory.Exists(templatePath) && 
                   Directory.GetFiles(templatePath, "*.pbip").Length > 0;
        }

        /// <summary>
        /// Install template from plugin folder to settings folder
        /// </summary>
        public void InstallDefaultTemplate(string sourceTemplatePath)
        {
            if (string.IsNullOrEmpty(sourceTemplatePath) || !Directory.Exists(sourceTemplatePath))
                return;

            var destPath = Path.Combine(_settingsFolder, "PBIP_DefaultTemplate");
            
            if (Directory.Exists(destPath))
                Directory.Delete(destPath, true);

            CopyDirectory(sourceTemplatePath, destPath);
        }

        /// <summary>
        /// Get the path to the installed template
        /// </summary>
        public string? GetInstalledTemplatePath()
        {
            var templatePath = Path.Combine(_settingsFolder, "PBIP_DefaultTemplate");
            if (Directory.Exists(templatePath))
                return templatePath;
            return null;
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            url = url.Trim().ToLowerInvariant();
            if (url.StartsWith("https://"))
                url = url.Substring(8);
            if (url.EndsWith("/"))
                url = url.Substring(0, url.Length - 1);
            return url;
        }

        private PluginSettings ClonePluginSettings(PluginSettings source)
        {
            if (source == null) return new PluginSettings();

            return new PluginSettings
            {
                LastSolutionId = source.LastSolutionId,
                LastSolutionName = source.LastSolutionName,
                FactTable = source.FactTable,
                SelectedTableNames = source.SelectedTableNames?.ToList() ?? new List<string>(),
                SelectedAttributes = source.SelectedAttributes?.ToDictionary(
                    k => k.Key,
                    v => v.Value?.ToList() ?? new List<string>()
                ) ?? new Dictionary<string, List<string>>(),
                SelectedFormIds = source.SelectedFormIds?.ToDictionary(k => k.Key, v => v.Value) 
                    ?? new Dictionary<string, string>(),
                SelectedViewIds = source.SelectedViewIds?.ToDictionary(k => k.Key, v => v.Value) 
                    ?? new Dictionary<string, string>(),
                Relationships = source.Relationships?.Select(r => new SerializedRelationship
                {
                    SourceTable = r.SourceTable,
                    SourceAttribute = r.SourceAttribute,
                    TargetTable = r.TargetTable,
                    IsActive = r.IsActive,
                    IsSnowflake = r.IsSnowflake
                }).ToList() ?? new List<SerializedRelationship>(),
                TableDisplayInfo = source.TableDisplayInfo?.ToDictionary(
                    k => k.Key,
                    v => new TableDisplayInfo
                    {
                        DisplayName = v.Value.DisplayName,
                        SchemaName = v.Value.SchemaName,
                        PrimaryIdAttribute = v.Value.PrimaryIdAttribute,
                        PrimaryNameAttribute = v.Value.PrimaryNameAttribute
                    }
                ) ?? new Dictionary<string, TableDisplayInfo>(),
                ShowAllAttributes = source.ShowAllAttributes,
                TableStorageModes = source.TableStorageModes?.ToDictionary(k => k.Key, v => v.Value)
                    ?? new Dictionary<string, string>(),
                ExpandedLookups = source.ExpandedLookups?.ToDictionary(
                    k => k.Key,
                    v => v.Value?.Select(e => new SerializedExpandedLookup
                    {
                        LookupAttributeName = e.LookupAttributeName,
                        TargetTableLogicalName = e.TargetTableLogicalName,
                        TargetTableDisplayName = e.TargetTableDisplayName,
                        TargetTablePrimaryKey = e.TargetTablePrimaryKey,
                        FormId = e.FormId,
                        Attributes = e.Attributes?.Select(a => new SerializedExpandedLookupAttribute
                        {
                            LogicalName = a.LogicalName,
                            DisplayName = a.DisplayName,
                            AttributeType = a.AttributeType,
                            SchemaName = a.SchemaName,
                            Targets = a.Targets?.ToList(),
                            VirtualAttributeName = a.VirtualAttributeName,
                            IsGlobal = a.IsGlobal,
                            OptionSetName = a.OptionSetName,
                            IncludeInModel = a.IncludeInModel,
                            IsHidden = a.IsHidden
                        }).ToList() ?? new List<SerializedExpandedLookupAttribute>()
                    }).ToList() ?? new List<SerializedExpandedLookup>()
                ) ?? new Dictionary<string, List<SerializedExpandedLookup>>(),
                LookupSubColumnConfigs = source.LookupSubColumnConfigs?.ToDictionary(
                    k => k.Key,
                    v => v.Value?.Select(cfg => new SerializedLookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = cfg.LookupAttributeLogicalName,
                        IncludeIdField = cfg.IncludeIdField,
                        IdFieldHidden = cfg.IdFieldHidden,
                        IncludeNameField = cfg.IncludeNameField,
                        NameFieldHidden = cfg.NameFieldHidden,
                        IncludeTypeField = cfg.IncludeTypeField,
                        TypeFieldHidden = cfg.TypeFieldHidden,
                        IncludeYomiField = cfg.IncludeYomiField,
                        YomiFieldHidden = cfg.YomiFieldHidden
                    }).ToList() ?? new List<SerializedLookupSubColumnConfig>()
                ) ?? new Dictionary<string, List<SerializedLookupSubColumnConfig>>(),
                ChoiceSubColumnConfigs = source.ChoiceSubColumnConfigs?.ToDictionary(
                    k => k.Key,
                    v => v.Value?.Select(cfg => new SerializedChoiceSubColumnConfig
                    {
                        AttributeLogicalName = cfg.AttributeLogicalName,
                        IncludeValueField = cfg.IncludeValueField,
                        ValueFieldHidden = cfg.ValueFieldHidden,
                        IncludeLabelField = cfg.IncludeLabelField,
                        LabelFieldHidden = cfg.LabelFieldHidden
                    }).ToList() ?? new List<SerializedChoiceSubColumnConfig>()
                ) ?? new Dictionary<string, List<SerializedChoiceSubColumnConfig>>(),
                CollapsedLookupGroups = source.CollapsedLookupGroups?.ToList() ?? new List<string>()
            };
        }
    }

    /// <summary>
    /// Root file containing all semantic model configurations
    /// </summary>
    [DataContract]
    public class SemanticModelsFile
    {
        [DataMember]
        public List<SemanticModelConfig> Models { get; set; } = new List<SemanticModelConfig>();

        [DataMember]
        public string CurrentModelName { get; set; } = "";
    }

    /// <summary>
    /// Semantic model configuration - contains all settings for a single model
    /// </summary>
    [DataContract]
    public class SemanticModelConfig
    {
        [OnDeserializing]
        private void SetDefaults(StreamingContext context)
        {
            UseDisplayNameAliasesInSql = true;
            IncludeChoiceNumericValueAsHiddenAttributes = false;
        }

        [DataMember]
        public string Name { get; set; } = "";

        [DataMember]
        public string DataverseUrl { get; set; } = "";

        [DataMember]
        public string WorkingFolder { get; set; } = "";

        [DataMember]
        public string TemplatePath { get; set; } = "";

        [DataMember]
        public DateTime CreatedDate { get; set; }

        [DataMember]
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Connection type: "DataverseTDS" or "FabricLink"
        /// </summary>
        [DataMember]
        public string ConnectionType { get; set; } = "DataverseTDS";

        /// <summary>
        /// FabricLink SQL endpoint (only used when ConnectionType is FabricLink)
        /// </summary>
        [DataMember]
        public string FabricLinkSQLEndpoint { get; set; } = "";

        /// <summary>
        /// FabricLink Lakehouse name (only used when ConnectionType is FabricLink)
        /// </summary>
        [DataMember]
        public string FabricLinkSQLDatabase { get; set; } = "";

        /// <summary>
        /// When true, uses display names as SQL column aliases (AS [Display Name])
        /// instead of renaming columns at the TMDL level. Default: true.
        /// </summary>
        [DataMember]
        public bool UseDisplayNameAliasesInSql { get; set; } = true;

        /// <summary>
        /// Storage mode for Dataverse tables: "DirectQuery", "Dual", or "Import".
        /// DirectQuery: all tables use directQuery (default).
        /// Dual: fact table stays directQuery, dimensions use dual mode.
        /// Import: all Dataverse/FabricLink tables use import mode.
        /// </summary>
        [DataMember]
        public string StorageMode { get; set; } = "DirectQuery";

        /// <summary>
        /// When true, default behavior for choice-style attributes includes the raw
        /// numeric value as a hidden field.
        /// </summary>
        [DataMember]
        public bool IncludeChoiceNumericValueAsHiddenAttributes { get; set; } = false;

        /// <summary>
        /// Embedded plugin settings for this model
        /// </summary>
        [DataMember]
        public PluginSettings PluginSettings { get; set; } = new PluginSettings();
    }
}
