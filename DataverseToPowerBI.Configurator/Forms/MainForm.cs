// =============================================================================
// MainForm.cs - Main Application Window for Dataverse Metadata Extractor
// =============================================================================
// Purpose: Primary WinForms interface for the Configurator application.
//
// This form provides the complete workflow for creating Power BI semantic
// models from Dataverse metadata:
//
//   1. Connection Management
//      - Connect to Dataverse environments via MSAL OAuth
//      - Solution selection and table discovery
//      - Metadata caching for offline access
//
//   2. Table Configuration
//      - Select tables from solutions
//      - Designate Fact vs Dimension roles (star-schema)
//      - Configure snowflake relationships
//
//   3. Column/Attribute Selection
//      - Select columns for each table
//      - Filter by form fields (use form layout as column template)
//      - View all or selected attributes
//
//   4. View/Filter Configuration
//      - Select views to apply filter conditions
//      - FetchXML filters converted to SQL WHERE clauses
//
//   5. Calendar/Date Table
//      - Configure date dimension with timezone adjustment
//      - Set primary date field for relationships
//
//   6. Semantic Model Generation
//      - Generate TMDL files for Power BI .pbip projects
//      - Incremental updates with change detection
//      - Launch Power BI Desktop on completion
//
// UI Structure:
//   - Semantic Model Dropdown: Switch between configurations
//   - Tables ListView: Selected tables with role, form, view, attribute count
//   - Attributes ListView: Columns for the selected table
//   - Relationships ListView: Star-schema relationships
//   - Status Bar: Connection status and progress
//
// Settings Persistence:
//   - SettingsManager handles configuration storage
//   - MetadataCache for offline Dataverse metadata
//   - Multiple named configurations supported
//
// Related Classes:
//   - DataverseClient: API communication
//   - SettingsManager: Configuration persistence
//   - SemanticModelBuilder: TMDL generation
//   - FetchXmlToSqlConverter: View filter conversion
// =============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Models;
using DataverseToPowerBI.Configurator.Services;
using Newtonsoft.Json;

namespace DataverseToPowerBI.Configurator.Forms
{
    /// <summary>
    /// Main application window for the Dataverse Metadata Extractor for Power BI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This form orchestrates the complete workflow for generating Power BI
    /// semantic models from Dataverse metadata. Key features include:
    /// </para>
    /// <list type="bullet">
    ///   <item>OAuth authentication to Dataverse environments</item>
    ///   <item>Solution-based table discovery</item>
    ///   <item>Star-schema modeling (Fact/Dimension designation)</item>
    ///   <item>Form-based column selection</item>
    ///   <item>View-based filter configuration</item>
    ///   <item>Date table generation with timezone handling</item>
    ///   <item>TMDL file generation for Power BI Desktop</item>
    /// </list>
    /// <para>
    /// State is managed through in-memory dictionaries that are persisted
    /// via SettingsManager and MetadataCache for session continuity.
    /// </para>
    /// </remarks>
    public partial class MainForm : Form
    {
        #region Private Fields

        /// <summary>Dataverse API client (null when disconnected).</summary>
        private DataverseClient? _client;

        /// <summary>Settings persistence manager.</summary>
        private readonly SettingsManager _settingsManager;

        /// <summary>Current application settings.</summary>
        private AppSettings _settings;

        /// <summary>Cached Dataverse metadata for offline access.</summary>
        private MetadataCache _cache;
        
        // Table state management
        /// <summary>Selected tables indexed by logical name.</summary>
        private Dictionary<string, TableInfo> _selectedTables = new();

        /// <summary>Forms per table (logicalName -> forms).</summary>
        private Dictionary<string, List<FormMetadata>> _tableForms = new();

        /// <summary>Views per table (logicalName -> views).</summary>
        private Dictionary<string, List<ViewMetadata>> _tableViews = new();

        /// <summary>Attributes per table (logicalName -> attributes).</summary>
        private Dictionary<string, List<AttributeMetadata>> _tableAttributes = new();

        /// <summary>Selected attribute names per table.</summary>
        private Dictionary<string, HashSet<string>> _selectedAttributes = new();

        /// <summary>Selected view ID per table.</summary>
        private Dictionary<string, string> _selectedViews = new();

        /// <summary>Loading state flags per table.</summary>
        private Dictionary<string, bool> _loadingStates = new();

        // Star-schema state
        /// <summary>Logical name of the fact table (null if not designated).</summary>
        private string? _factTable = null;

        /// <summary>Configured star-schema relationships.</summary>
        private List<RelationshipConfig> _relationships = new();

        /// <summary>Calendar/Date table configuration.</summary>
        private DateTableConfig? _dateTableConfig = null;
        
        // ListView sorting state
        private int _selectedTablesSortColumn = -1;
        private bool _selectedTablesSortAscending = true;
        private int _attributesSortColumn = -1;
        private bool _attributesSortAscending = true;
        
        /// <summary>Currently selected solution name.</summary>
        private string? _currentSolutionName;

        /// <summary>Flag to prevent settings save during initialization.</summary>
        private bool _isLoading = false;

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the MainForm class.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            _settingsManager = new SettingsManager();
            _settings = _settingsManager.LoadSettings();
            // Load cache for the current configuration
            var currentConfig = _settingsManager.GetCurrentConfigurationName();
            _cache = _settingsManager.LoadCache(currentConfig) ?? new MetadataCache();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Services.DebugLogger.Log($"=== Application Started ===");
            Services.DebugLogger.Log($"Debug log: {Services.DebugLogger.GetLogPath()}");
            
            _isLoading = true;  // Prevent SaveSettings during initialization
            
            // Disable connection-dependent controls
            btnSelectTables.Enabled = false;
            
            try
            {
                // Clean up orphaned cache files on startup
                var orphansRemoved = _settingsManager.CleanupOrphanedCacheFiles();
                if (orphansRemoved > 0)
                {
                    Services.DebugLogger.Log($"Removed {orphansRemoved} orphaned cache file(s)");
                }

                // Log diagnostics to help understand settings loading
                var diagnostics = _settingsManager.GetSettingsDiagnostics();
                Services.DebugLogger.LogSection("Settings Storage Diagnostics", diagnostics);

                // Restore settings (strip https:// from URL for display)
                var url = _settings.LastEnvironmentUrl ?? "";
                Services.DebugLogger.Log($"Loading environment URL from settings: '{url}'");
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = url.Substring(8);
                txtEnvironmentUrl.Text = url;
                
                // Project name matches configuration name
                var configName = _settingsManager.GetCurrentConfigurationName();
                txtProjectName.Text = configName;
                txtOutputFolder.Text = _settings.OutputFolder ?? "";
                
                // Restore radio button state (only set one since they're mutually exclusive)
                if (_settings.ShowAllAttributes)
                    radioShowAll.Checked = true;
                else
                    radioShowSelected.Checked = true;

                // Restore current solution name
                _currentSolutionName = _settings.LastSolution;

                // Check if this is the very first run (no configurations exist)
                var allConfigs = _settingsManager.GetConfigurationNames();
                if (allConfigs.Count == 0 || (allConfigs.Count == 1 && allConfigs[0] == "Default" && 
                    string.IsNullOrEmpty(_settings.LastEnvironmentUrl)))
                {
                    Services.DebugLogger.Log("First run detected - prompting for new semantic model");
                    _isLoading = false;
                    
                    // Prompt for first semantic model
                    this.Invoke(new Action(async () =>
                    {
                        await Task.Delay(500); // Brief delay for UI to initialize
                        await PromptForFirstSemanticModel();
                    }));
                    return;
                }

                // Restore star-schema configuration
                _factTable = _settings.FactTable;
                _relationships = _settings.Relationships ?? new List<RelationshipConfig>();
                _dateTableConfig = _settings.DateTableConfig;

                // Populate semantic model dropdown
                RefreshSemanticModelDropdown();

                // Restore from cache if valid
                if (!string.IsNullOrEmpty(_settings.LastEnvironmentUrl) && 
                    !string.IsNullOrEmpty(_settings.LastSolution) &&
                    _cache.IsValidFor(_settings.LastEnvironmentUrl, _settings.LastSolution))
                {
                    Services.DebugLogger.Log($"Cache is VALID - calling RestoreFromCache()");
                    SetStatus($"Loaded cache from {_cache.CachedDate:g}");
                    RestoreFromCache();
                    // Cache is valid, enable metadata-dependent controls
                   EnableMetadataDependentControls(true);
                }
                else if (_settings.SelectedTables.Any())
                {
                    Services.DebugLogger.Log($"Cache is INVALID - calling RestoreFromSettings()");
                    // Cache is invalid but we have settings - restore minimal state without metadata
                    RestoreFromSettings();
                    // No valid metadata, disable controls and force "Selected" mode
                    EnableMetadataDependentControls(false);
                }
                else
                {
                    // No cache and no settings, disable controls
                    EnableMetadataDependentControls(false);
                }
                
                UpdateTableCount();
            }
            finally
            {
                _isLoading = false;  // Re-enable SaveSettings
                
                // Update window title with current configuration name
                var configName = _settingsManager.GetCurrentConfigurationName();
                this.Text = $"Dataverse Metadata Extractor for Power BI - {configName}";
                
                // Log where debug log is saved
                Services.DebugLogger.Log($"=== MainForm_Load Complete ===");
                Services.DebugLogger.Log($"Debug log location: {Services.DebugLogger.GetLogPath()}");
                
                // Auto-connect and refresh metadata on launch if environment URL is configured
                if (!string.IsNullOrEmpty(_settings.LastEnvironmentUrl))
                {
                    this.Invoke(new Action(async () =>
                    {
                        await Task.Delay(500); // Brief delay for UI to initialize
                        await ConnectToEnvironmentAsync();
                        
                        // After connection, refresh metadata (skip confirmation prompt)
                        if (_client != null)
                        {
                            await RefreshMetadataAsync(promptForConfirmation: false);
                        }
                    }));
                }
            }
        }

        private async Task PromptForFirstSemanticModel()
        {
            var result = MessageBox.Show(
                "Welcome to Dataverse Metadata Extractor for Power BI!\n\n" +
                "Let's create your first semantic model.\n\n" +
                "You'll need:\n" +
                "  • A Dataverse environment URL\n" +
                "  • A name for your semantic model\n" +
                "  • A working folder location\n\n" +
                "Would you like to continue?",
                "First Time Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                SetStatus("Setup cancelled. You can create a semantic model anytime from the dropdown.");
                return;
            }

            // Get default working folder
            var defaultFolder = txtOutputFolder.Text;
            if (string.IsNullOrEmpty(defaultFolder))
            {
                var baseDir = Path.GetDirectoryName(Application.ExecutablePath);
                defaultFolder = Path.Combine(baseDir ?? "", "..", "..", "..", "..", "Reports");
                defaultFolder = Path.GetFullPath(defaultFolder);
            }

            using var dialog = new NewSemanticModelDialog(defaultFolder);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var modelName = dialog.SemanticModelName;
                var envUrl = dialog.EnvironmentUrl;
                var folder = dialog.WorkingFolder;

                try
                {
                    // Update working folder if changed
                    if (dialog.WorkingFolderChanged)
                    {
                        txtOutputFolder.Text = folder;
                    }

                    // Update environment URL
                    txtEnvironmentUrl.Text = envUrl;

                    // Create the folder structure
                    var modelFolder = Path.Combine(folder, modelName);
                    Directory.CreateDirectory(modelFolder);

                    // Update or create configuration
                    if (_settingsManager.GetConfigurationNames().Contains("Default"))
                    {
                        // Rename Default to the new model name
                        _settingsManager.RenameConfiguration("Default", modelName);
                    }
                    else
                    {
                        // Create new configuration
                        var newSettings = new AppSettings
                        {
                            ProjectName = modelName,
                            OutputFolder = folder,
                            LastEnvironmentUrl = envUrl
                        };
                        _settingsManager.CreateNewConfiguration(modelName, newSettings);
                    }

                    // Switch to the new configuration
                    await SwitchToConfiguration(modelName);

                    SetStatus($"Connecting to {envUrl}...");

                    // Auto-connect and refresh metadata (skip confirmation prompt on first run)
                    // RefreshMetadataAsync will automatically open the table selector
                    await RefreshMetadataAsync(promptForConfirmation: false);

                    // Check if connection failed
                    if (_client == null)
                    {
                        MessageBox.Show(
                            $"Semantic model '{modelName}' created, but connection failed.\n\n" +
                            "You can try connecting again using the 'Refresh Metadata' button.",
                            "Connection Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create semantic model:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    RefreshSemanticModelDropdown();
                }
            }
            else
            {
                SetStatus("Setup cancelled. You can create a semantic model anytime from the dropdown.");
            }
        }

        private void RestoreFromSettings()
        {
            // Restore minimal state from settings (without full metadata)
            // This prevents SaveSettings() from clearing everything when cache is expired
            
            // DEBUG: Check AttributeDisplayInfo on entry
            var attrInfoCount = _settings.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
            Services.DebugLogger.LogSection("RestoreFromSettings - Entry",
                $"AttributeDisplayInfo: {attrInfoCount} total attributes across {_settings.AttributeDisplayInfo?.Count ?? 0} tables");
            
            foreach (var tableName in _settings.SelectedTables)
            {
                // Create TableInfo from saved display info or use logical name as fallback
                var tableInfo = new TableInfo
                {
                    LogicalName = tableName,
                    DisplayName = _settings.TableDisplayInfo.ContainsKey(tableName) 
                        ? _settings.TableDisplayInfo[tableName].DisplayName ?? tableName
                        : tableName,
                    SchemaName = _settings.TableDisplayInfo.ContainsKey(tableName)
                        ? _settings.TableDisplayInfo[tableName].SchemaName
                        : tableName,
                    PrimaryIdAttribute = _settings.TableDisplayInfo.ContainsKey(tableName)
                        ? _settings.TableDisplayInfo[tableName].PrimaryIdAttribute
                        : null,
                    PrimaryNameAttribute = _settings.TableDisplayInfo.ContainsKey(tableName)
                        ? _settings.TableDisplayInfo[tableName].PrimaryNameAttribute
                        : null
                };
                
                _selectedTables[tableName] = tableInfo;
                
                // Restore selected attributes
                if (_settings.TableAttributes.ContainsKey(tableName))
                {
                    _selectedAttributes[tableName] = new HashSet<string>(_settings.TableAttributes[tableName]);
                    
                    // Create minimal AttributeMetadata for display purposes
                    var attrList = new List<AttributeMetadata>();
                    foreach (var attrName in _settings.TableAttributes[tableName])
                    {
                        // Check if we have saved display info
                        if (_settings.AttributeDisplayInfo.ContainsKey(tableName) &&
                            _settings.AttributeDisplayInfo[tableName].ContainsKey(attrName))
                        {
                            var info = _settings.AttributeDisplayInfo[tableName][attrName];
                            attrList.Add(new AttributeMetadata
                            {
                                LogicalName = attrName, // Use key instead of info.LogicalName (which has [JsonIgnore])
                                DisplayName = info.DisplayName,
                                SchemaName = info.SchemaName,
                                AttributeType = info.AttributeType
                            });
                        }
                        else
                        {
                            // Fallback: create minimal metadata with just logical name
                            attrList.Add(new AttributeMetadata
                            {
                                LogicalName = attrName,
                                DisplayName = attrName,
                                SchemaName = attrName,
                                AttributeType = "(reconnect to load)"
                            });
                        }
                    }
                    _tableAttributes[tableName] = attrList;
                }
                
                // Restore selected view IDs
                if (_settings.TableViews.ContainsKey(tableName))
                {
                    _selectedViews[tableName] = _settings.TableViews[tableName];
                }
                
                // Add to UI with display name
                var isFact = tableName == _factTable;
                
                // Check if this dimension is a snowflake (is the target of a snowflake relationship)
                // A snowflake dimension is referenced BY another dimension (not referencing another dimension)
                var isSnowflake = !isFact && _relationships.Any(r => 
                    r.TargetTable == tableName && 
                    r.IsSnowflake);
                
                var roleText = isFact ? "⭐ Fact" : (isSnowflake ? "Dim ❄️" : "Dim");
                var formName = _settings.TableFormNames.ContainsKey(tableName)
                    ? _settings.TableFormNames[tableName]
                    : "(reconnect to load)";
                var viewName = _settings.TableViewNames.ContainsKey(tableName)
                    ? _settings.TableViewNames[tableName]
                    : "(reconnect to load)";

                var item = new ListViewItem("✏️");  // Edit column
                item.Name = tableName;
                item.SubItems.Add(roleText);  // Role column
                item.SubItems.Add(tableInfo.DisplayName ?? tableName);  // Table column
                item.SubItems.Add(formName);  // Form column
                item.SubItems.Add(viewName);  // View column
                item.SubItems.Add(_settings.TableAttributes.ContainsKey(tableName)
                    ? _settings.TableAttributes[tableName].Count.ToString()
                    : "0");  // Attrs column

                // Highlight fact table row
                if (isFact)
                {
                    item.BackColor = System.Drawing.Color.LightYellow;
                    item.Font = new System.Drawing.Font(listViewSelectedTables.Font, System.Drawing.FontStyle.Bold);
                }

                listViewSelectedTables.Items.Add(item);
            }
            
            // Add Date Table if configured
            if (_dateTableConfig != null && !string.IsNullOrEmpty(_dateTableConfig.PrimaryDateTable))
            {
                var yearRange = $"{_dateTableConfig.StartYear}-{_dateTableConfig.EndYear}";
                var dateItem = new ListViewItem("📅");  // Calendar icon
                dateItem.Name = "__DateTable";
                dateItem.SubItems.Add("Dim");
                dateItem.SubItems.Add("Date Table");
                dateItem.SubItems.Add("");  // No form
                dateItem.SubItems.Add(yearRange);  // Year range in Filter column
                dateItem.SubItems.Add("365+");  // Approximate row count
                dateItem.ForeColor = System.Drawing.Color.DarkGreen;
                
                // Insert after Fact table (position 1) if there is a fact, otherwise at position 0
                var insertPosition = listViewSelectedTables.Items.Cast<ListViewItem>()
                    .Any(i => i.SubItems[1].Text.StartsWith("⭐")) ? 1 : 0;
                listViewSelectedTables.Items.Insert(insertPosition, dateItem);
            }
            
            // Auto-select first table and show its attributes
            if (listViewSelectedTables.Items.Count > 0)
            {
                listViewSelectedTables.Items[0].Selected = true;
                // Manually trigger attribute display since event may not fire during form load
                var firstTableName = listViewSelectedTables.Items[0].Name;
                UpdateAttributesDisplay(firstTableName);
            }
            
            // DEBUG: Check _tableAttributes after restore
            var tableAttrCount = _tableAttributes.Sum(t => t.Value.Count);
            Services.DebugLogger.LogSection("RestoreFromSettings - Complete",
                $"_tableAttributes: {tableAttrCount} total attributes across {_tableAttributes.Count} tables");
            
            SetStatus("Previous session restored. Please reconnect to refresh metadata.");
        }

        private void RestoreFromCache()
        {
            _currentSolutionName = _cache.SolutionName;

            // Restore tables from cache
            foreach (var kvp in _cache.TableData)
            {
                var logicalName = kvp.Key;
                var tableInfo = kvp.Value;
                
                _selectedTables[logicalName] = tableInfo;
                
                // Save display info to settings for persistence
                _settings.TableDisplayInfo[logicalName] = new TableDisplayInfo
                {
                    LogicalName = tableInfo.LogicalName,
                    DisplayName = tableInfo.DisplayName,
                    SchemaName = tableInfo.SchemaName,
                    PrimaryIdAttribute = tableInfo.PrimaryIdAttribute,
                    PrimaryNameAttribute = tableInfo.PrimaryNameAttribute
                };
                
                // Restore cached metadata
                if (_cache.TableForms.ContainsKey(logicalName))
                    _tableForms[logicalName] = _cache.TableForms[logicalName];
                
                if (_cache.TableViews.ContainsKey(logicalName))
                    _tableViews[logicalName] = _cache.TableViews[logicalName];
                
                if (_cache.TableAttributes.ContainsKey(logicalName))
                {
                    _tableAttributes[logicalName] = _cache.TableAttributes[logicalName];
                    
                    // Save attribute display info to settings
                    var requiredAttrs = GetRequiredAttributes(tableInfo);
                    var attrDict = new Dictionary<string, AttributeDisplayInfo>();
                    foreach (var attr in _cache.TableAttributes[logicalName])
                    {
                        attrDict[attr.LogicalName] = new AttributeDisplayInfo
                        {
                            LogicalName = attr.LogicalName,
                            DisplayName = attr.DisplayName,
                            SchemaName = attr.SchemaName,
                            AttributeType = attr.AttributeType,
                            IsRequired = requiredAttrs.Contains(attr.LogicalName),
                            Targets = attr.Targets  // Lookup target tables
                        };
                    }
                    _settings.AttributeDisplayInfo[logicalName] = attrDict;
                }
                
                // Restore attribute selections
                var savedAttrs = _settings.TableAttributes.ContainsKey(logicalName) 
                    ? new HashSet<string>(_settings.TableAttributes[logicalName])
                    : new HashSet<string>();
                
                // Always include required attributes
                var required = GetRequiredAttributes(tableInfo);
                savedAttrs.UnionWith(required);
                _selectedAttributes[logicalName] = savedAttrs;
                
                // Restore view selection
                if (_settings.TableViews.ContainsKey(logicalName))
                {
                    _selectedViews[logicalName] = _settings.TableViews[logicalName];
                }
                else if (_tableViews.ContainsKey(logicalName))
                {
                    var views = _tableViews[logicalName];
                    var defaultView = views.FirstOrDefault(v => v.IsDefault) ?? views.FirstOrDefault();
                    if (defaultView != null)
                    {
                        _selectedViews[logicalName] = defaultView.ViewId;
                        // Also save to settings for persistence
                        _settings.TableViews[logicalName] = defaultView.ViewId;
                    }
                }
                
                // Add to UI
                AddTableToSelectedList(tableInfo);
            }
            
            UpdateTableCount();
            
            // Add Date Table if configured
            if (_dateTableConfig != null && !string.IsNullOrEmpty(_dateTableConfig.PrimaryDateTable))
            {
                var yearRange = $"{_dateTableConfig.StartYear}-{_dateTableConfig.EndYear}";
                var dateItem = new ListViewItem("📅");  // Calendar icon
                dateItem.Name = "__DateTable";
                dateItem.SubItems.Add("Dim");
                dateItem.SubItems.Add("Date Table");
                dateItem.SubItems.Add("");  // No form
                dateItem.SubItems.Add(yearRange);  // Year range in Filter column
                dateItem.SubItems.Add("365+");  // Approximate row count
                dateItem.ForeColor = System.Drawing.Color.DarkGreen;
                
                // Insert after Fact table (position 1) if there is a fact, otherwise at position 0
                var insertPosition = listViewSelectedTables.Items.Cast<ListViewItem>()
                    .Any(i => i.SubItems[1].Text.StartsWith("⭐")) ? 1 : 0;
                listViewSelectedTables.Items.Insert(insertPosition, dateItem);
            }
            
            // Auto-select first table and show its attributes
            if (listViewSelectedTables.Items.Count > 0)
            {
                listViewSelectedTables.Items[0].Selected = true;
                // Manually trigger attribute display since event may not fire during form load
                var firstTableName = listViewSelectedTables.Items[0].Name;
                UpdateAttributesDisplay(firstTableName);
            }
            
            // DEBUG: Log state after cache restore
            var cacheAttrCount = _tableAttributes.Sum(t => t.Value.Count);
            var cacheSelectedCount = _selectedAttributes.Sum(t => t.Value.Count);
            Services.DebugLogger.LogSection("RestoreFromCache - Complete",
                $"_tableAttributes: {cacheAttrCount} total attributes across {_tableAttributes.Count} tables\n" +
                $"_selectedAttributes: {cacheSelectedCount} selected attributes across {_selectedAttributes.Count} tables\n" +
                $"_settings.AttributeDisplayInfo: {_settings.AttributeDisplayInfo.Sum(t => t.Value.Count)} attrs across {_settings.AttributeDisplayInfo.Count} tables");
        }

        private HashSet<string> GetRequiredAttributes(TableInfo table)
        {
            var required = new HashSet<string>();
            if (!string.IsNullOrEmpty(table.PrimaryIdAttribute))
                required.Add(table.PrimaryIdAttribute);
            if (!string.IsNullOrEmpty(table.PrimaryNameAttribute))
                required.Add(table.PrimaryNameAttribute);
            return required;
        }

        private void AddDateTableToDisplay()
        {
            // Remove existing Date Table entry if present (to avoid duplicates)
            var existingDateItem = listViewSelectedTables.Items.Cast<ListViewItem>()
                .FirstOrDefault(i => i.Name == "__DateTable");
            if (existingDateItem != null)
            {
                listViewSelectedTables.Items.Remove(existingDateItem);
            }

            // Add Date Table if configured
            if (_dateTableConfig != null && !string.IsNullOrEmpty(_dateTableConfig.PrimaryDateTable))
            {
                var yearRange = $"{_dateTableConfig.StartYear}-{_dateTableConfig.EndYear}";
                var dateItem = new ListViewItem("📅");  // Calendar icon
                dateItem.Name = "__DateTable";
                dateItem.SubItems.Add("Dim");
                dateItem.SubItems.Add("Date Table");
                dateItem.SubItems.Add("");  // No form
                dateItem.SubItems.Add(yearRange);  // Year range in Filter column
                dateItem.SubItems.Add("365+");  // Approximate row count
                dateItem.ForeColor = System.Drawing.Color.DarkGreen;
                
                // Insert after Fact table (position 1) if there is a fact, otherwise at position 0
                var insertPosition = listViewSelectedTables.Items.Cast<ListViewItem>()
                    .Any(i => i.SubItems[1].Text.StartsWith("⭐")) ? 1 : 0;
                listViewSelectedTables.Items.Insert(insertPosition, dateItem);
            }
        }

        private void EnableMetadataDependentControls(bool enabled)
        {
            btnSelectFromForm.Enabled = enabled;
            radioShowAll.Enabled = enabled;
            
            // Force "Selected" radio button when disabling
            if (!enabled && !radioShowSelected.Checked)
            {
                radioShowSelected.Checked = true;
            }
        }

        private void AutoSelectFormFields(string logicalName, FormMetadata form)
        {
            if (!_selectedAttributes.ContainsKey(logicalName) || 
                !_tableAttributes.ContainsKey(logicalName))
                return;

            var selectedAttrs = _selectedAttributes[logicalName];
            var attributes = _tableAttributes[logicalName];
            var requiredAttrs = GetRequiredAttributes(_selectedTables[logicalName]);

            // Clear current selections
            selectedAttrs.Clear();

            // Always include required attributes (primary ID and name)
            foreach (var req in requiredAttrs)
                selectedAttrs.Add(req);

            // Add all fields from the form
            if (form.Fields != null)
            {
                foreach (var field in form.Fields)
                {
                    if (attributes.Any(a => a.LogicalName.Equals(field, StringComparison.OrdinalIgnoreCase)))
                        selectedAttrs.Add(field);
                }
            }
        }

        private void SaveSettings()
        {
            if (_isLoading) return;  // Don't save during initial load
            
            // DEBUG: Check state before save
            var beforeAttrInfoCount = _settings.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
            var tableAttrCount = _tableAttributes.Sum(t => t.Value.Count);
            Services.DebugLogger.LogSection("SaveSettings - Entry",
                $"_settings.AttributeDisplayInfo: {beforeAttrInfoCount} attrs across {_settings.AttributeDisplayInfo?.Count ?? 0} tables\n" +
                $"_tableAttributes: {tableAttrCount} attrs across {_tableAttributes.Count} tables\n" +
                $"_selectedAttributes: {_selectedAttributes.Sum(t => t.Value.Count)} attrs across {_selectedAttributes.Count} tables\n" +
                $"_selectedTables: {_selectedTables.Count} tables\n" +
                $"_tableAttributes.Any(): {_tableAttributes.Any()}");
            
            // Store URL without https:// prefix
            var url = txtEnvironmentUrl.Text.Trim();
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(8);
            
            _settings.LastEnvironmentUrl = url;
            _settings.LastSolution = _currentSolutionName ?? "";
            _settings.SelectedTables = _selectedTables.Keys.ToList();
            // Don't regenerate TableForms from UI - it's maintained through direct updates in ShowFormViewSelector
            // _settings.TableForms is already correct from direct assignment: _settings.TableForms[logicalName] = dialog.SelectedForm.FormId
            _settings.TableFormNames = GetSelectedFormNames();
            // Don't regenerate TableViews from _selectedViews - it's maintained through direct updates
            // _settings.TableViews is already correct from direct assignment in ShowFormViewSelector and LoadTableMetadata
            _settings.TableViewNames = GetSelectedViewNames();
            _settings.TableAttributes = _selectedAttributes.ToDictionary(
                k => k.Key,
                v => v.Value.ToList()
            );
            
            // Save table display info
            _settings.TableDisplayInfo = _selectedTables.ToDictionary(
                k => k.Key,
                v => new TableDisplayInfo
                {
                    LogicalName = v.Value.LogicalName,
                    DisplayName = v.Value.DisplayName,
                    SchemaName = v.Value.SchemaName,
                    PrimaryIdAttribute = v.Value.PrimaryIdAttribute,
                    PrimaryNameAttribute = v.Value.PrimaryNameAttribute
                }
            );
            
            // Save attribute display info
            // Always update from _selectedAttributes when we have selections
            // This ensures custom attribute selections are preserved even if _tableAttributes was cleared
            if (_selectedAttributes.Any())
            {
                Services.DebugLogger.Log($"Rebuilding AttributeDisplayInfo from {_selectedAttributes.Count} tables in _selectedAttributes");
                
                // DEBUG: Log what's actually in _tableAttributes
                foreach (var tableKvp in _tableAttributes)
                {
                    var attrNames = string.Join(", ", tableKvp.Value.Select(a => a.LogicalName).Take(3));
                    Services.DebugLogger.Log($"  _tableAttributes[{tableKvp.Key}] has {tableKvp.Value.Count} attrs: {attrNames}...");
                }
                
                _settings.AttributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>();
                foreach (var tableName in _selectedAttributes.Keys)
                {
                    Services.DebugLogger.Log($"  Processing table: {tableName}");
                    
                    // DEBUG: Show what's being looked for
                    var selectedNames = string.Join(", ", _selectedAttributes[tableName].Take(3));
                    Services.DebugLogger.Log($"    _selectedAttributes has: {selectedNames}...");
                    
                    if (!_selectedTables.ContainsKey(tableName))
                    {
                        Services.DebugLogger.Log($"    SKIPPED: Not in _selectedTables");
                        continue;
                    }
                    
                    var requiredAttrs = GetRequiredAttributes(_selectedTables[tableName]);
                    var attrDict = new Dictionary<string, AttributeDisplayInfo>();
                    var selectedAttrCount = _selectedAttributes[tableName].Count;
                    Services.DebugLogger.Log($"    Has {selectedAttrCount} selected attributes");
                    
                    // If we have full metadata, use it. Otherwise, save minimal info to preserve selections
                    bool hasMetadata = _tableAttributes.ContainsKey(tableName);
                    
                    foreach (var attrLogicalName in _selectedAttributes[tableName])
                    {
                        if (hasMetadata)
                        {
                            var attrMeta = _tableAttributes[tableName].FirstOrDefault(a => a.LogicalName == attrLogicalName);
                            if (attrMeta != null)
                            {
                                attrDict[attrLogicalName] = new AttributeDisplayInfo
                                {
                                    LogicalName = attrMeta.LogicalName,
                                    DisplayName = attrMeta.DisplayName,
                                    SchemaName = attrMeta.SchemaName,
                                    AttributeType = attrMeta.AttributeType,
                                    IsRequired = requiredAttrs.Contains(attrLogicalName),
                                    Targets = attrMeta.Targets  // Lookup target tables
                                };
                            }
                            else
                            {
                                Services.DebugLogger.Log($"      Attribute {attrLogicalName} not found in _tableAttributes[{tableName}]");
                            }
                        }
                        else
                        {
                            // No metadata available - save minimal info to preserve the selection
                            attrDict[attrLogicalName] = new AttributeDisplayInfo
                            {
                                LogicalName = attrLogicalName,
                                DisplayName = attrLogicalName,  // Will be updated when metadata reloads
                                SchemaName = attrLogicalName,
                                AttributeType = "Unknown",
                                IsRequired = requiredAttrs.Contains(attrLogicalName)
                            };
                        }
                    }
                    
                    Services.DebugLogger.Log($"    attrDict has {attrDict.Count} attributes, attrDict.Any() = {attrDict.Any()}");
                    
                    if (attrDict.Any())
                        _settings.AttributeDisplayInfo[tableName] = attrDict;
                    else
                        Services.DebugLogger.Log($"    SKIPPED: attrDict is empty");
                }
                
                Services.DebugLogger.Log($"Final AttributeDisplayInfo has {_settings.AttributeDisplayInfo.Sum(t => t.Value.Count)} attributes across {_settings.AttributeDisplayInfo.Count} tables");
            }
            // else: No selected attributes, keep existing AttributeDisplayInfo from loaded settings
            
            // DEBUG: Check AttributeDisplayInfo after update
            var afterAttrInfoCount = _settings.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
            Services.DebugLogger.LogSection("SaveSettings - After AttributeDisplayInfo Update",
                $"_settings.AttributeDisplayInfo: {afterAttrInfoCount} attrs across {_settings.AttributeDisplayInfo?.Count ?? 0} tables\n" +
                $"Updated: {_selectedAttributes.Any()}");
            
            // Project name always matches configuration name
            var configName = _settingsManager.GetCurrentConfigurationName();
            _settings.ProjectName = configName;
            
            _settings.OutputFolder = txtOutputFolder.Text;
            _settings.WindowGeometry = $"{Width},{Height}";
            _settings.ShowAllAttributes = radioShowAll.Checked;

            // Save star-schema configuration
            _settings.FactTable = _factTable;
            _settings.Relationships = _relationships;
            _settings.DateTableConfig = _dateTableConfig;
            _settings.TableRoles = _selectedTables.ToDictionary(
                k => k.Key,
                v => v.Key == _factTable ? TableRole.Fact : TableRole.Dimension
            );

            _settingsManager.SaveSettings(_settings);
        }

        private Dictionary<string, string> GetSelectedFormIds()
        {
            var result = new Dictionary<string, string>();
            foreach (ListViewItem item in listViewSelectedTables.Items)
            {
                var logicalName = item.Name;
                // Column indices: 0=Edit, 1=Role, 2=Table, 3=Form, 4=View, 5=Attrs
                var formName = item.SubItems[3].Text;

                if (_tableForms.ContainsKey(logicalName))
                {
                    var form = _tableForms[logicalName].FirstOrDefault(f => f.Name == formName);
                    if (form != null)
                        result[logicalName] = form.FormId;
                }
            }
            return result;
        }

        private Dictionary<string, string> GetSelectedFormNames()
        {
            var result = new Dictionary<string, string>();
            foreach (ListViewItem item in listViewSelectedTables.Items)
            {
                var logicalName = item.Name;
                // Column indices: 0=Edit, 1=Role, 2=Table, 3=Form, 4=View, 5=Attrs
                var formName = item.SubItems[3].Text;

                // Only save if it's not a placeholder
                if (!formName.Contains("loading") && !formName.Contains("not loaded") &&
                    !formName.Contains("no forms") && !formName.Contains("reconnect"))
                {
                    result[logicalName] = formName;
                }
            }
            return result;
        }

        private Dictionary<string, string> GetSelectedViewNames()
        {
            var result = new Dictionary<string, string>();
            foreach (ListViewItem item in listViewSelectedTables.Items)
            {
                var logicalName = item.Name;
                // Column indices: 0=Edit, 1=Role, 2=Table, 3=Form, 4=View, 5=Attrs
                var viewName = item.SubItems[4].Text;

                // Only save if it's not a placeholder
                if (!viewName.Contains("loading") && !viewName.Contains("not loaded") &&
                    !viewName.Contains("no views") && !viewName.Contains("reconnect"))
                {
                    result[logicalName] = viewName;
                }
            }
            return result;
        }

        private void SaveCache()
        {
            var envUrl = txtEnvironmentUrl.Text.Trim();
            if (string.IsNullOrEmpty(envUrl) || string.IsNullOrEmpty(_currentSolutionName))
                return;
            
            // Add https:// prefix for cache storage (consistency with validation)
            if (!envUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                envUrl = "https://" + envUrl;
            
            _cache.EnvironmentUrl = envUrl;
            _cache.SolutionName = _currentSolutionName;
            _cache.CachedDate = DateTime.Now;
            _cache.TableData = _selectedTables.ToDictionary(k => k.Key, v => v.Value);
            _cache.TableForms = _tableForms.ToDictionary(k => k.Key, v => v.Value);
            _cache.TableViews = _tableViews.ToDictionary(k => k.Key, v => v.Value);
            _cache.TableAttributes = _tableAttributes.ToDictionary(k => k.Key, v => v.Value);
            
            // Explicitly save cache for current configuration
            var currentConfig = _settingsManager.GetCurrentConfigurationName();
            _settingsManager.SaveCache(_cache, currentConfig);
        }

        private void UpdateTableCount()
        {
            var count = _selectedTables.Count;
            if (count == 0)
            {
                lblTableCount.Text = "No tables selected";
            }
            else if (_factTable != null)
            {
                var dimCount = count - 1;
                lblTableCount.Text = $"1 Fact + {dimCount} Dimension{(dimCount != 1 ? "s" : "")} selected";
            }
            else
            {
                lblTableCount.Text = count == 1 ? "1 table selected" : $"{count} tables selected";
            }

            // Enable Calendar Table button when tables are selected
            btnCalendarTable.Enabled = count > 0;
            
            // Update relationships display
            UpdateRelationshipsDisplay();
        }

        private void UpdateRelationshipsDisplay()
        {
            listViewRelationships.Items.Clear();
            
            // Display relationship to date table first if configured
            if (_dateTableConfig != null && !string.IsNullOrEmpty(_dateTableConfig.PrimaryDateTable))
            {
                var dateSourceTable = _selectedTables.ContainsKey(_dateTableConfig.PrimaryDateTable)
                    ? _selectedTables[_dateTableConfig.PrimaryDateTable].DisplayName ?? _dateTableConfig.PrimaryDateTable
                    : _dateTableConfig.PrimaryDateTable;
                
                var item = new ListViewItem($"{dateSourceTable}.{_dateTableConfig.PrimaryDateField}");
                item.SubItems.Add("Date Table 📅");
                item.SubItems.Add("Active (Date)");
                item.ForeColor = System.Drawing.Color.DarkGreen;
                
                listViewRelationships.Items.Add(item);
            }
            
            if (!_relationships.Any())
            {
                return;
            }
            
            // Display regular relationships
            foreach (var rel in _relationships.Where(r => !r.IsSnowflake))
            {
                var fromTable = _selectedTables.ContainsKey(rel.SourceTable) 
                    ? _selectedTables[rel.SourceTable].DisplayName ?? rel.SourceTable 
                    : rel.SourceTable;
                var toTable = _selectedTables.ContainsKey(rel.TargetTable) 
                    ? _selectedTables[rel.TargetTable].DisplayName ?? rel.TargetTable 
                    : rel.TargetTable;
                
                var item = new ListViewItem($"{fromTable}.{rel.SourceAttribute}");
                item.SubItems.Add(toTable);
                item.SubItems.Add(rel.IsActive ? "Active" : "Inactive");
                
                listViewRelationships.Items.Add(item);
            }
            
            // Display snowflake relationships
            foreach (var rel in _relationships.Where(r => r.IsSnowflake))
            {
                var fromTable = _selectedTables.ContainsKey(rel.SourceTable) 
                    ? _selectedTables[rel.SourceTable].DisplayName ?? rel.SourceTable 
                    : rel.SourceTable;
                var toTable = _selectedTables.ContainsKey(rel.TargetTable) 
                    ? _selectedTables[rel.TargetTable].DisplayName ?? rel.TargetTable 
                    : rel.TargetTable;
                
                var item = new ListViewItem($"{fromTable}.{rel.SourceAttribute}");
                item.SubItems.Add($"{toTable} ❄️");
                item.SubItems.Add(rel.IsActive ? "Snowflake (Active)" : "Snowflake (Inactive)");
                
                listViewRelationships.Items.Add(item);
            }
        }

        private void SetStatus(string message)
        {
            lblStatus.Text = message;
            Application.DoEvents();
        }

        private void ShowProgress(bool show)
        {
            progressBar.Visible = show;
        }

        private void RefreshSemanticModelDropdown()
        {
            cboSemanticModels.Items.Clear();
            
            // Add "+ Create New" and "⚙ Settings..." options first
            cboSemanticModels.Items.Add("+ Create New...");
            cboSemanticModels.Items.Add("⚙ Settings...");
            cboSemanticModels.Items.Add("---");  // Separator
            
            // Get all configurations from SettingsManager
            var configurations = _settingsManager.GetConfigurationNames();
            if (configurations.Any())
            {
                foreach (var config in configurations.OrderBy(c => c))
                {
                    cboSemanticModels.Items.Add(config);
                }
                
                // Select current configuration
                var currentConfig = _settingsManager.GetCurrentConfigurationName();
                var index = cboSemanticModels.Items.IndexOf(currentConfig);
                if (index >= 0)
                {
                    cboSemanticModels.SelectedIndex = index;
                }
                else if (cboSemanticModels.Items.Count > 3)  // Skip "+ Create New", "⚙ Settings...", and separator
                {
                    cboSemanticModels.SelectedIndex = 3;
                }
            }
            else
            {
                // Only "+ Create New" and "⚙ Settings..." are available
                cboSemanticModels.SelectedIndex = 0;
            }
        }

        private async void CboSemanticModels_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isLoading) return;
            
            var selectedModel = cboSemanticModels.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedModel))
                return;
            
            // Handle "+ Create New" option
            if (selectedModel == "+ Create New...")
            {
                CreateNewSemanticModel();
                return;
            }
            
            // Handle "⚙ Settings..." option
            if (selectedModel == "⚙ Settings...")
            {
                OpenSettingsDialog();
                return;
            }
            
            // Skip separator and placeholder items
            if (selectedModel.StartsWith("---") || selectedModel.StartsWith("(No"))
                return;
            
            // Switch to the corresponding configuration
            var configs = _settingsManager.GetConfigurationNames();
            if (configs.Contains(selectedModel))
            {
                await SwitchToConfiguration(selectedModel);
            }
        }

        private void OpenSettingsDialog()
        {
            using var dialog = new SemanticModelSettingsDialog(_settingsManager);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var hasChanges = dialog.ConfigurationsChanged;
                var newlyCreated = dialog.NewlyCreatedConfiguration;
                
                if (hasChanges)
                {
                    // Reload current configuration in case it was modified
                    var currentConfig = _settingsManager.GetCurrentConfigurationName();
                    _settings = _settingsManager.LoadSettings();
                    
                    // Update UI with potentially changed values
                    _isLoading = true;
                    txtEnvironmentUrl.Text = _settings.LastEnvironmentUrl ?? "";
                    txtProjectName.Text = currentConfig;
                    txtOutputFolder.Text = _settings.OutputFolder ?? "";
                    _isLoading = false;
                    
                    // Refresh dropdown
                    RefreshSemanticModelDropdown();
                    
                    SetStatus("Settings updated.");
                }
                
                // If a new configuration was created, trigger auto-setup
                if (!string.IsNullOrEmpty(newlyCreated))
                {
                    this.Invoke(new Action(async () =>
                    {
                        await Task.Delay(300); // Brief delay for UI to settle
                        await AutoSetupNewConfiguration(newlyCreated);
                    }));
                }
            }
            else
            {
                // Re-select current configuration (user may have clicked Settings then cancelled)
                RefreshSemanticModelDropdown();
            }
        }

        private async Task AutoSetupNewConfiguration(string configurationName)
        {
            try
            {
                // Switch to the new configuration
                var currentConfig = _settingsManager.GetCurrentConfigurationName();
                if (currentConfig != configurationName)
                {
                    await SwitchToConfiguration(configurationName, clearCredentials: true);
                }
                
                // Clear cached metadata for fresh start
                _cache = new MetadataCache();
                _settingsManager.SaveCache(_cache, configurationName);
                
                // Clear any existing state
                ClearAllTables();
                
                var envUrl = txtEnvironmentUrl.Text.Trim();
                if (string.IsNullOrWhiteSpace(envUrl))
                {
                    MessageBox.Show(
                        $"Configuration '{configurationName}' created successfully.\n\n" +
                        "Please configure the Dataverse environment URL in Settings before proceeding.",
                        "Setup Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                
                // Prompt for authentication
                var result = MessageBox.Show(
                    $"Configuration '{configurationName}' created successfully.\n\n" +
                    "Connect to Dataverse and select tables now?",
                    "Setup New Configuration",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    SetStatus($"Connecting to {envUrl}...");
                    ShowProgress(true);
                    
                    // Connect with forced authentication
                    await ConnectToEnvironmentAsync(clearCredentials: true);
                    
                    ShowProgress(false);
                    
                    // If connection succeeded, open table selector
                    if (_client != null)
                    {
                        await Task.Delay(500); // Brief delay after connection
                        
                        // Open the table selector dialog
                        BtnSelectTables_Click(null, EventArgs.Empty);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Connection failed. You can try again using the 'Connect' button.",
                            "Connection Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowProgress(false);
                MessageBox.Show(
                    $"Error during configuration setup:\n{ex.Message}",
                    "Setup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void CreateNewSemanticModel()
        {
            using var dialog = new NewSemanticModelDialog(txtOutputFolder.Text, txtEnvironmentUrl.Text);
            
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var modelName = dialog.SemanticModelName;
                var envUrl = dialog.EnvironmentUrl;
                var folder = dialog.WorkingFolder;
                
                // Update working folder and environment URL if changed
                if (dialog.WorkingFolderChanged)
                {
                    txtOutputFolder.Text = folder;
                }
                
                if (!string.IsNullOrEmpty(envUrl))
                {
                    txtEnvironmentUrl.Text = envUrl;
                }
                
                SaveSettings();
                
                // Check if configuration already exists
                var existingConfigs = _settingsManager.GetConfigurationNames();
                if (existingConfigs.Contains(modelName))
                {
                    // Configuration exists - just switch to it
                    var result = MessageBox.Show(
                        $"A configuration named '{modelName}' already exists.\n\n" +
                        $"Would you like to switch to that configuration?",
                        "Configuration Exists",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        await SwitchToConfiguration(modelName);
                    }
                    else
                    {
                        RefreshSemanticModelDropdown();
                    }
                    return;
                }
                
                // Create the folder structure for the new semantic model
                try
                {
                    var modelFolder = Path.Combine(folder, modelName);
                    Directory.CreateDirectory(modelFolder);
                    
                    // Create a new configuration for this semantic model
                    var newSettings = new AppSettings 
                    { 
                        ProjectName = modelName,
                        OutputFolder = folder,
                        LastEnvironmentUrl = envUrl
                    };
                    _settingsManager.CreateNewConfiguration(modelName, newSettings);
                    
                    SetStatus($"Created new semantic model: {modelName}");
                    
                    // Trigger auto-setup for the new configuration
                    await AutoSetupNewConfiguration(modelName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create semantic model:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    RefreshSemanticModelDropdown();  // Reset dropdown
                }
            }
            else
            {
                // User cancelled - reset dropdown to previous selection
                RefreshSemanticModelDropdown();
            }
        }

        private void BtnChangeWorkingFolder_Click(object? sender, EventArgs e)
        {
            using var dialog = new WorkingFolderDialog(txtOutputFolder.Text);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                txtOutputFolder.Text = dialog.WorkingFolder;
                SaveSettings();
                RefreshSemanticModelDropdown();
                SetStatus($"Working folder set to: {dialog.WorkingFolder}");
            }
        }

        private void BtnSettingsFolder_Click(object? sender, EventArgs e)
        {
            try
            {
                var settingsFolder = _settingsManager.GetSettingsFolderPath();
                
                // Log diagnostics to debug log
                var diagnostics = _settingsManager.GetSettingsDiagnostics();
                Services.DebugLogger.LogSection("Settings Folder Opened", diagnostics);
                
                // Ensure the folder exists
                if (!Directory.Exists(settingsFolder))
                {
                    Directory.CreateDirectory(settingsFolder);
                }
                
                // Open the folder in Windows Explorer
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = settingsFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
                
                SetStatus($"Opened settings folder: {settingsFolder}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open settings folder:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ConnectToEnvironmentAsync(bool clearCredentials = false)
        {
            var url = txtEnvironmentUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("No environment URL configured for this semantic model.\n\nPlease configure it in Settings (⚙ icon in dropdown).", "No Environment URL", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Add https:// for connection
            var connectionUrl = url;
            if (!connectionUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                connectionUrl = "https://" + connectionUrl;

            // Check if there's a different configuration for this environment URL
            var mostRecentConfigForUrl = _settingsManager.GetMostRecentConfigurationForEnvironment(connectionUrl);
            var currentConfig = _settingsManager.GetCurrentConfigurationName();

            if (!string.IsNullOrEmpty(mostRecentConfigForUrl) && mostRecentConfigForUrl != currentConfig)
            {
                var result = MessageBox.Show(
                    $"You previously used the configuration '{mostRecentConfigForUrl}' for this environment.\n\n" +
                    $"Would you like to switch to it?\n\n" +
                    $"(Current configuration: '{currentConfig}')",
                    "Switch Configuration?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    await SwitchToConfiguration(mostRecentConfigForUrl, clearCredentials: clearCredentials);
                    return;
                }
            }

            try
            {
                lblConnectionStatus.Text = "Connecting...";
                lblConnectionStatus.ForeColor = System.Drawing.Color.Orange;
                SetStatus(clearCredentials ? "Re-authenticating to Dataverse..." : "Authenticating to Dataverse...");
                ShowProgress(true);

                _client = new DataverseClient(connectionUrl);
                await _client.AuthenticateAsync(clearCredentials);

                lblConnectionStatus.Text = "Connected";
                lblConnectionStatus.ForeColor = System.Drawing.Color.Green;
                btnSelectTables.Enabled = true;
                SetStatus("Connected successfully.");
                SaveSettings();
                
                // Revalidate cached metadata if we have any selected tables
                if (_selectedTables.Any())
                {
                    await RevalidateMetadata();
                }
            }
            catch (Exception ex)
            {
                lblConnectionStatus.Text = "Connection failed";
                lblConnectionStatus.ForeColor = System.Drawing.Color.Red;
                btnSelectTables.Enabled = false;
                MessageBox.Show($"Connection failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Connection failed.");
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private async void BtnRefreshMetadata_Click(object sender, EventArgs e)
        {
            await RefreshMetadataAsync(promptForConfirmation: true);
        }

        private async Task RefreshMetadataAsync(bool promptForConfirmation = true, bool clearCredentials = false)
        {
            // If not connected, connect first
            if (_client == null)
            {
                await ConnectToEnvironmentAsync();
                
                // If connection failed, don't proceed
                if (_client == null)
                {
                    return;
                }
            }

            // Show what we're doing (no confirmation needed)
            if (promptForConfirmation)
            {
                SetStatus("Refreshing Metadata...");
                ShowProgress(true);
            }

            try
            {
                if (!promptForConfirmation)
                {
                    ShowProgress(true);
                }
                SetStatus("Refreshing metadata from Dataverse...");

                // Track changes
                var removedTables = new List<string>();
                var removedAttributes = new Dictionary<string, List<string>>();
                var addedAttributes = new Dictionary<string, List<string>>();
                var updatedViews = new List<string>();
                var removedForms = new Dictionary<string, List<string>>();
                var removedViews = new Dictionary<string, List<string>>();

                // Step 1: Validate tables still exist in solution
                if (_selectedTables.Any() && !string.IsNullOrEmpty(_currentSolutionName))
                {
                    SetStatus("Validating tables...");
                    
                    // Get the solution ID from the solution name
                    var solutions = await _client.GetSolutionsAsync();
                    var currentSolution = solutions.FirstOrDefault(s => s.UniqueName == _currentSolutionName);
                    
                    if (currentSolution == null)
                    {
                        MessageBox.Show($"Solution '{_currentSolutionName}' not found. Metadata refresh cancelled.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    var allTables = await _client.GetSolutionTablesAsync(currentSolution.SolutionId);
                    var validTableNames = allTables.Select(t => t.LogicalName).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var tableName in _selectedTables.Keys.ToList())
                {
                    if (!validTableNames.Contains(tableName))
                    {
                        removedTables.Add(tableName);
                        _selectedTables.Remove(tableName);
                        _tableAttributes.Remove(tableName);
                        _selectedAttributes.Remove(tableName);
                        _tableForms.Remove(tableName);
                        _tableViews.Remove(tableName);
                    }
                }
            }

            // Step 2: For each remaining table, refresh metadata
            foreach (var tableName in _selectedTables.Keys.ToList())
            {
                SetStatus($"Refreshing metadata for {tableName}...");

                    // Before refresh: identify which fields are on the current form but NOT selected
                    // These are fields the user has explicitly opted out of
                    var optedOutFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (_tableForms.ContainsKey(tableName) && _settings.TableForms.ContainsKey(tableName))
                    {
                        var currentForm = _tableForms[tableName].FirstOrDefault(f => f.FormId == _settings.TableForms[tableName]);
                        if (currentForm?.Fields != null && _selectedAttributes.ContainsKey(tableName))
                        {
                            var selectedAttrs = _selectedAttributes[tableName];
                            foreach (var formField in currentForm.Fields)
                            {
                                // If field is on form but NOT selected, user opted out
                                if (!selectedAttrs.Contains(formField, StringComparer.OrdinalIgnoreCase))
                                {
                                    optedOutFields.Add(formField);
                                }
                            }
                        }
                    }

                    // Refresh attributes
                    var currentAttrs = await _client.GetAttributesAsync(tableName);
                    var oldAttrs = _tableAttributes.ContainsKey(tableName) ? _tableAttributes[tableName] : new List<AttributeMetadata>();
                    _tableAttributes[tableName] = currentAttrs;

                    // Check for removed attributes
                    var currentAttrNames = currentAttrs.Select(a => a.LogicalName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (_selectedAttributes.ContainsKey(tableName))
                    {
                        var invalidAttrs = _selectedAttributes[tableName].Where(a => !currentAttrNames.Contains(a)).ToList();
                        if (invalidAttrs.Any())
                        {
                            if (!removedAttributes.ContainsKey(tableName))
                                removedAttributes[tableName] = new List<string>();
                            removedAttributes[tableName].AddRange(invalidAttrs);
                            
                            foreach (var attr in invalidAttrs)
                            {
                                _selectedAttributes[tableName].Remove(attr);
                            }
                        }
                    }

                    // Refresh forms and add new fields from selected form (excluding opted-out fields)
                    if (_settings.TableForms.ContainsKey(tableName))
                    {
                        var forms = await _client.GetFormsAsync(tableName, includeXml: false);
                        var selectedFormId = _settings.TableForms[tableName];
                        
                        if (forms.Any(f => f.FormId == selectedFormId))
                        {
                            // Form still exists - get updated field list
                            var formXml = await _client.GetFormXmlAsync(selectedFormId);
                            if (!string.IsNullOrEmpty(formXml))
                            {
                                var formFields = DataverseClient.ExtractFieldsFromFormXml(formXml);
                                
                                // Add new fields from form that aren't already selected
                                // BUT skip fields the user has explicitly opted out of
                                if (_selectedAttributes.ContainsKey(tableName))
                                {
                                    var newFieldsFromForm = formFields.Where(f => 
                                        currentAttrNames.Contains(f) && 
                                        !_selectedAttributes[tableName].Contains(f, StringComparer.OrdinalIgnoreCase) &&
                                        !optedOutFields.Contains(f)).ToList();  // Don't re-add opted-out fields
                                    
                                    if (newFieldsFromForm.Any())
                                    {
                                        if (!addedAttributes.ContainsKey(tableName))
                                            addedAttributes[tableName] = new List<string>();
                                        addedAttributes[tableName].AddRange(newFieldsFromForm);
                                        
                                        foreach (var field in newFieldsFromForm)
                                        {
                                            _selectedAttributes[tableName].Add(field);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Form was deleted - remove it
                            if (!removedForms.ContainsKey(tableName))
                                removedForms[tableName] = new List<string>();
                            removedForms[tableName].Add(selectedFormId);
                            _settings.TableForms.Remove(tableName);
                            _settings.TableFormNames.Remove(tableName);
                            _tableForms.Remove(tableName);
                        }
                    }

                    // Refresh views and update FetchXML
                    if (_settings.TableViews.ContainsKey(tableName))
                    {
                        var views = await _client.GetViewsAsync(tableName, includeFetchXml: false);
                        var selectedViewId = _settings.TableViews[tableName];
                        
                        if (views.Any(v => v.ViewId == selectedViewId))
                        {
                            // View still exists - get updated FetchXML
                            var fetchXml = await _client.GetViewFetchXmlAsync(selectedViewId);
                            
                            // Update cache with new FetchXML
                            if (_cache.TableViews.ContainsKey(tableName))
                            {
                                var cachedView = _cache.TableViews[tableName].FirstOrDefault(v => v.ViewId == selectedViewId);
                                if (cachedView != null)
                                {
                                    cachedView.FetchXml = fetchXml;
                                    updatedViews.Add($"{tableName}: {cachedView.Name}");
                                }
                            }
                        }
                        else
                        {
                            // View was deleted - remove it
                            if (!removedViews.ContainsKey(tableName))
                                removedViews[tableName] = new List<string>();
                            removedViews[tableName].Add(selectedViewId);
                            _settings.TableViews.Remove(tableName);
                            _settings.TableViewNames.Remove(tableName);
                            _tableViews.Remove(tableName);
                        }
                    }
                }

                // Save updated cache
                var currentConfig = _settingsManager.GetCurrentConfigurationName();
                _settingsManager.SaveCache(_cache, currentConfig);

                SetStatus("Metadata refresh complete.");

                // Rebuild the display to reflect changes
                RefreshTableListDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh metadata:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Metadata refresh failed.");
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private void BtnSelectTables_Click(object? sender, EventArgs e)
        {
            if (_client == null)
            {
                MessageBox.Show("Please connect first.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Show Fact & Dimension selector dialog
            using var dialog = new FactDimensionSelectorDialog(
                _client,
                _currentSolutionName,
                _factTable,
                _relationships);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _currentSolutionName = dialog.SelectedSolutionName;

                // Clear existing tables and relationships
                ClearAllTables();

                // Store fact table and relationships
                _factTable = dialog.SelectedFactTable?.LogicalName;
                _relationships = dialog.SelectedRelationships;

                // Add all selected tables (Fact + Dimensions)
                if (dialog.AllSelectedTables.Any())
                {
                    // Preserve existing attribute selections before adding tables
                    var preservedSelections = new Dictionary<string, HashSet<string>>();
                    foreach (var table in dialog.AllSelectedTables)
                    {
                        if (_selectedAttributes.ContainsKey(table.LogicalName))
                        {
                            preservedSelections[table.LogicalName] = new HashSet<string>(_selectedAttributes[table.LogicalName]);
                        }
                    }
                    
                    AddTablesInBulk(dialog.AllSelectedTables, preservedSelections);
                }

                SaveSettings();
            }
        }

        private void ClearAllTables()
        {
            _selectedTables.Clear();
            _tableForms.Clear();
            _tableViews.Clear();
            _tableAttributes.Clear();
            _selectedAttributes.Clear();
            _selectedViews.Clear();
            _loadingStates.Clear();
            listViewSelectedTables.Items.Clear();
            listViewAttributes.Items.Clear();
        }

        private async void AddTablesInBulk(List<TableInfo> tables, Dictionary<string, HashSet<string>>? preservedSelections = null)
        {
            // Add all tables to the list first
            foreach (var table in tables)
            {
                var logicalName = table.LogicalName;
                bool isExistingTable = _selectedTables.ContainsKey(logicalName);
                
                if (isExistingTable)
                    continue;  // Already added
                
                _selectedTables[logicalName] = table;
                
                // Restore preserved selections if available, otherwise use required attributes only
                if (preservedSelections != null && preservedSelections.ContainsKey(logicalName))
                {
                    _selectedAttributes[logicalName] = preservedSelections[logicalName];
                }
                else
                {
                    _selectedAttributes[logicalName] = GetRequiredAttributes(table);
                }
                
                _loadingStates[logicalName] = true;
                
                AddTableToSelectedList(table, loading: true);
            }
            
            UpdateTableCount();
            SaveSettings();

            // Now load metadata in parallel
            SetStatus($"Loading metadata for {tables.Count} tables...");
            ShowProgress(true);

            var tasks = tables.Select(t => LoadTableMetadata(t.LogicalName)).ToList();
            await Task.WhenAll(tasks);

            ShowProgress(false);
            SetStatus($"Loaded metadata for {tables.Count} tables.");
            SaveCache();
            
            // Add Date Table to display after loading metadata
            AddDateTableToDisplay();
            
            // Enable metadata-dependent controls after successful load
            EnableMetadataDependentControls(true);

            // Auto-select first table if nothing selected
            if (listViewSelectedTables.SelectedItems.Count == 0 && listViewSelectedTables.Items.Count > 0)
            {
                listViewSelectedTables.Items[0].Selected = true;
            }
        }

        private async Task LoadTableMetadata(string logicalName)
        {
            if (_client == null) return;

            try
            {
                // Load attributes, forms, and views in parallel
                var attrTask = _client.GetAttributesAsync(logicalName);
                var formsTask = _client.GetFormsAsync(logicalName, includeXml: true);
                var viewsTask = _client.GetViewsAsync(logicalName, includeFetchXml: true);

                await Task.WhenAll(attrTask, formsTask, viewsTask);

                var attributes = await attrTask;
                var forms = await formsTask;
                var views = await viewsTask;

                // Update state
                _tableAttributes[logicalName] = attributes;
                _tableForms[logicalName] = forms;
                _tableViews[logicalName] = views;

                // Auto-select attributes based on form (for new tables only, not when restoring from cache)
                var selectedAttrs = _selectedAttributes[logicalName];
                var requiredAttrs = GetRequiredAttributes(_selectedTables[logicalName]);
                
                // Check if this is a truly new table (not restored from cache or reopened from dialog)
                // Only auto-select if we have EXACTLY the required attrs and nothing more
                bool isNewTable = selectedAttrs.Count == requiredAttrs.Count && 
                                  requiredAttrs.All(r => selectedAttrs.Contains(r)) &&
                                  selectedAttrs.All(s => requiredAttrs.Contains(s));
                
                // Ensure form ID is set (for new tables or if missing)
                if (!_settings.TableForms.ContainsKey(logicalName) && forms.Any())
                {
                    _settings.TableForms[logicalName] = forms.First().FormId;
                }
                
                if (isNewTable && forms.Any())
                {
                    // Get the form that will be used (from settings or default to first)
                    var formId = _settings.TableForms.ContainsKey(logicalName) 
                        ? _settings.TableForms[logicalName] 
                        : forms.First().FormId;
                    
                    var selectedForm = forms.FirstOrDefault(f => f.FormId == formId) ?? forms.First();
                    
                    // Clear current selections (except required) and select form fields
                    selectedAttrs.Clear();
                    
                    // Always include required attributes (primary ID and name)
                    foreach (var req in requiredAttrs)
                        selectedAttrs.Add(req);
                    
                    // Add all fields from the selected form
                    if (selectedForm.Fields != null)
                    {
                        foreach (var field in selectedForm.Fields)
                        {
                            if (attributes.Any(a => a.LogicalName.Equals(field, StringComparison.OrdinalIgnoreCase)))
                                selectedAttrs.Add(field);
                        }
                    }
                }

                // Select default view
                if (views.Any() && !_selectedViews.ContainsKey(logicalName))
                {
                    var defaultView = views.FirstOrDefault(v => v.IsDefault) ?? views.First();
                    _selectedViews[logicalName] = defaultView.ViewId;
                    // Also save to settings for persistence
                    _settings.TableViews[logicalName] = defaultView.ViewId;
                }

                // Update UI on main thread
                this.Invoke((MethodInvoker)delegate
                {
                    UpdateSelectedTableRow(logicalName);
                    _loadingStates[logicalName] = false;
                });
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    SetStatus($"Error loading metadata for {logicalName}: {ex.Message}");
                    _loadingStates[logicalName] = false;
                });
            }
        }

        private async Task RevalidateMetadata()
        {
            if (_client == null || !_selectedTables.Any())
                return;

            SetStatus($"Revalidating metadata for {_selectedTables.Count} tables...");
            ShowProgress(true);

            try
            {
                var tasks = _selectedTables.Keys.Select(logicalName => RevalidateTableMetadata(logicalName)).ToList();
                await Task.WhenAll(tasks);

                SetStatus($"Metadata revalidation complete.");
                SaveCache();
                
                // Enable metadata-dependent controls after successful revalidation
                EnableMetadataDependentControls(true);
                
                // Refresh UI for currently selected table
                if (listViewSelectedTables.SelectedItems.Count > 0)
                {
                    var selectedLogicalName = listViewSelectedTables.SelectedItems[0].Name;
                    UpdateAttributesDisplay(selectedLogicalName);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error during revalidation: {ex.Message}");
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private async Task RevalidateTableMetadata(string logicalName)
        {
            if (_client == null) return;

            try
            {
                // Get fresh metadata from Dataverse (including table display name)
                var tableMetadata = await _client.GetTableMetadataAsync(logicalName);
                var attributes = await _client.GetAttributesAsync(logicalName);
                var forms = await _client.GetFormsAsync(logicalName, includeXml: true);
                var views = await _client.GetViewsAsync(logicalName, includeFetchXml: true);

                // Update table info with fresh display name
                if (_selectedTables.ContainsKey(logicalName))
                {
                    var existingTable = _selectedTables[logicalName];
                    existingTable.DisplayName = tableMetadata.DisplayName;
                    existingTable.SchemaName = tableMetadata.SchemaName;
                    existingTable.PrimaryIdAttribute = tableMetadata.PrimaryIdAttribute;
                    existingTable.PrimaryNameAttribute = tableMetadata.PrimaryNameAttribute;
                }

                // Update cached metadata
                _tableAttributes[logicalName] = attributes;
                _tableForms[logicalName] = forms;
                _tableViews[logicalName] = views;

                // Get current attribute names from Dataverse
                var currentAttributeNames = new HashSet<string>(
                    attributes.Select(a => a.LogicalName),
                    StringComparer.OrdinalIgnoreCase
                );

                // Get required attributes
                var requiredAttrs = GetRequiredAttributes(_selectedTables[logicalName]);

                // Update selected attributes
                var selectedAttrs = _selectedAttributes[logicalName];
                
                // Remove deleted attributes (attributes that no longer exist in the table)
                var toRemove = selectedAttrs
                    .Where(attr => !currentAttributeNames.Contains(attr))
                    .ToList();
                
                foreach (var attr in toRemove)
                    selectedAttrs.Remove(attr);

                // Always ensure required attributes are included
                foreach (var req in requiredAttrs)
                {
                    if (!selectedAttrs.Contains(req))
                        selectedAttrs.Add(req);
                }

                // Update UI on main thread
                this.Invoke((MethodInvoker)delegate
                {
                    UpdateSelectedTableRow(logicalName);
                });
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    SetStatus($"Error revalidating metadata for {logicalName}: {ex.Message}");
                });
            }
        }

        private void AddTableToSelectedList(TableInfo table, bool loading = false)
        {
            var logicalName = table.LogicalName;
            var isFact = logicalName == _factTable;
            
            // Check if this dimension is a snowflake (is the target of a snowflake relationship)
            // A snowflake dimension is referenced BY another dimension (not referencing another dimension)
            var isSnowflake = !isFact && _relationships.Any(r => 
                r.TargetTable == logicalName && 
                r.IsSnowflake);
            
            var roleText = isFact ? "⭐ Fact" : (isSnowflake ? "Dim ❄️" : "Dim");
            var formText = loading ? "(loading...)" : GetFormDisplayText(logicalName);
            var viewText = loading ? "(loading...)" : GetViewDisplayText(logicalName);
            var attrCount = _selectedAttributes.ContainsKey(logicalName)
                ? _selectedAttributes[logicalName].Count.ToString()
                : "0";

            var item = new ListViewItem("✏️");  // Edit column
            item.Name = logicalName;
            item.SubItems.Add(roleText);  // Role column
            item.SubItems.Add(table.DisplayName ?? logicalName);  // Table column
            item.SubItems.Add(formText);  // Form column
            item.SubItems.Add(viewText);  // View column
            item.SubItems.Add(attrCount);  // Attrs column

            // Highlight fact table row
            if (isFact)
            {
                item.BackColor = System.Drawing.Color.LightYellow;
                item.Font = new System.Drawing.Font(listViewSelectedTables.Font, System.Drawing.FontStyle.Bold);
            }

            listViewSelectedTables.Items.Add(item);
        }

        private void UpdateSelectedTableRow(string logicalName)
        {
            var item = listViewSelectedTables.Items.Cast<ListViewItem>()
                .FirstOrDefault(i => i.Name == logicalName);

            if (item != null)
            {
                // Column indices: 0=Edit, 1=Role, 2=Table, 3=Form, 4=View, 5=Attrs
                var isFact = logicalName == _factTable;
                
                // Check if this dimension is a snowflake (is the target of a snowflake relationship)
                // A snowflake dimension is referenced BY another dimension (not referencing another dimension)
                var isSnowflake = !isFact && _relationships.Any(r => 
                    r.TargetTable == logicalName && 
                    r.IsSnowflake);
                
                item.SubItems[1].Text = isFact ? "⭐ Fact" : (isSnowflake ? "Dim ❄️" : "Dim");
                item.SubItems[3].Text = GetFormDisplayText(logicalName);
                item.SubItems[4].Text = GetViewDisplayText(logicalName);
                item.SubItems[5].Text = _selectedAttributes.ContainsKey(logicalName)
                    ? _selectedAttributes[logicalName].Count.ToString()
                    : "0";

                // Update styling
                if (isFact)
                {
                    item.BackColor = System.Drawing.Color.LightYellow;
                    item.Font = new System.Drawing.Font(listViewSelectedTables.Font, System.Drawing.FontStyle.Bold);
                }
                else
                {
                    item.BackColor = System.Drawing.Color.White;
                    item.Font = listViewSelectedTables.Font;
                }
            }
        }

        private void RefreshTableListDisplay()
        {
            // Clear and rebuild the table list
            listViewSelectedTables.Items.Clear();
            
            foreach (var kvp in _selectedTables)
            {
                AddTableToSelectedList(kvp.Value, loading: false);
            }
            
            AddDateTableToDisplay();
            UpdateTableCount();
        }

        private string GetFormDisplayText(string logicalName)
        {
            if (!_tableForms.ContainsKey(logicalName))
                return "(not loaded)";
            
            var forms = _tableForms[logicalName];
            if (!forms.Any())
                return "(no forms)";
            
            var selectedFormId = _settings.TableForms.ContainsKey(logicalName)
                ? _settings.TableForms[logicalName]
                : forms.First().FormId;
            
            var form = forms.FirstOrDefault(f => f.FormId == selectedFormId) ?? forms.First();
            return form.Name;
        }

        private string GetViewDisplayText(string logicalName)
        {
            if (!_tableViews.ContainsKey(logicalName))
                return "(not loaded)";
            
            var views = _tableViews[logicalName];
            if (!views.Any())
                return "(no views)";
            
            var selectedViewId = _selectedViews.ContainsKey(logicalName)
                ? _selectedViews[logicalName]
                : (views.FirstOrDefault(v => v.IsDefault) ?? views.First()).ViewId;
            
            var view = views.FirstOrDefault(v => v.ViewId == selectedViewId);
            return view != null ? view.Name : views.First().Name;
        }



        private void ListViewSelectedTables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0)
            {
                listViewAttributes.Items.Clear();
                return;
            }

            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            UpdateAttributesDisplay(logicalName);
        }

        private void ListViewSelectedTables_Click(object sender, EventArgs e)
        {
            var info = listViewSelectedTables.HitTest(listViewSelectedTables.PointToClient(Cursor.Position));
            if (info.Item != null && info.SubItem != null)
            {
                // Check if click was on the Edit column (first column, index 0)
                if (info.Item.SubItems.IndexOf(info.SubItem) == 0)
                {
                    var logicalName = info.Item.Name;
                    ShowFormViewSelector(logicalName);
                }
            }
        }

        private void ListViewSelectedTables_DoubleClick(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;

            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            ShowFormViewSelector(logicalName);
        }

        private void ShowFormViewSelector(string logicalName)
        {
            if (!_tableForms.ContainsKey(logicalName) || !_tableViews.ContainsKey(logicalName))
            {
                MessageBox.Show("Metadata is still loading. Please wait.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Get current selections as objects
            var currentFormId = GetSelectedFormId(logicalName);
            var currentViewId = _selectedViews.ContainsKey(logicalName) ? _selectedViews[logicalName] : null;
            
            var currentForm = currentFormId != null && _tableForms.ContainsKey(logicalName)
                ? _tableForms[logicalName].FirstOrDefault(f => f.FormId == currentFormId)
                : null;
            
            var currentView = currentViewId != null && _tableViews.ContainsKey(logicalName)
                ? _tableViews[logicalName].FirstOrDefault(v => v.ViewId == currentViewId)
                : null;

            using var dialog = new FormViewSelectorDialog(
                _tableForms[logicalName],
                _tableViews[logicalName],
                currentForm,
                currentView
            );

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                bool formChanged = false;
                
                // Update selections
                if (dialog.SelectedForm != null)
                {
                    var oldFormId = currentFormId;
                    _settings.TableForms[logicalName] = dialog.SelectedForm.FormId;
                    formChanged = oldFormId != dialog.SelectedForm.FormId;
                }

                if (dialog.SelectedView != null)
                {
                    _selectedViews[logicalName] = dialog.SelectedView.ViewId;
                    // Also save to settings for persistence (matching form handling)
                    _settings.TableViews[logicalName] = dialog.SelectedView.ViewId;
                }

                // If form changed, switch to "show all" mode so user can see all attributes
                // Don't auto-select - only "Match Selected Form" button should do that
                if (formChanged)
                {
                    radioShowAll.Checked = true;
                }

                UpdateSelectedTableRow(logicalName);
                UpdateAttributesDisplay(logicalName);
                SaveSettings();
            }
        }

        private string? GetSelectedFormId(string logicalName)
        {
            if (_settings.TableForms.ContainsKey(logicalName))
                return _settings.TableForms[logicalName];
            
            if (_tableForms.ContainsKey(logicalName) && _tableForms[logicalName].Any())
                return _tableForms[logicalName].First().FormId;
            
            return null;
        }

        private void UpdateAttributesDisplay(string logicalName)
        {
            listViewAttributes.ItemChecked -= ListViewAttributes_ItemChecked;  // Temporarily disable event
            listViewAttributes.Items.Clear();

            var selected = _selectedAttributes.ContainsKey(logicalName) 
                ? _selectedAttributes[logicalName] 
                : new HashSet<string>();
            var required = GetRequiredAttributes(_selectedTables[logicalName]);
            var filterText = txtAttrSearch.Text.ToLower();
            var showMode = radioShowAll.Checked ? "all" : "selected";

            // Get fields on form (may be empty if not loaded yet)
            var formFields = GetFormFields(logicalName);

            // If we have full attribute metadata loaded, use it
            if (_tableAttributes.ContainsKey(logicalName))
            {
                var attributes = _tableAttributes[logicalName];
                
                foreach (var attr in attributes)
                {
                    var display = attr.DisplayName ?? attr.SchemaName ?? "";
                    var logical = attr.LogicalName;
                    var isSelected = selected.Contains(logical);
                    var isRequired = required.Contains(logical);
                    var isOnForm = formFields.Contains(logical.ToLower());

                    // Apply show mode filter
                    if (showMode == "selected" && !isSelected) continue;

                    // Apply text filter
                    if (!string.IsNullOrEmpty(filterText))
                    {
                        if (!display.ToLower().Contains(filterText) && 
                            !logical.ToLower().Contains(filterText))
                            continue;
                    }

                    var item = new ListViewItem("");
                    item.Name = logical;
                    item.Checked = isSelected;
                    // Show lock for required attributes, checkmark for form fields
                    var formColumnText = isRequired ? "🔒" : (isOnForm ? "✓" : "");
                    item.SubItems.Add(formColumnText);
                    item.SubItems.Add(display);
                    item.SubItems.Add(logical);
                    item.SubItems.Add(attr.AttributeType ?? "");

                    // Apply visual styling
                    if (isRequired)
                    {
                        item.ForeColor = System.Drawing.Color.Blue;
                        item.Font = new System.Drawing.Font(item.Font, System.Drawing.FontStyle.Bold);
                    }
                    else if (isSelected)
                    {
                        item.ForeColor = System.Drawing.Color.Black;
                    }
                    else
                    {
                        item.ForeColor = System.Drawing.Color.Gray;
                    }

                    listViewAttributes.Items.Add(item);
                }
            }
            // If metadata not loaded yet but we have saved display info, show selected attributes from settings
            else if (_settings.AttributeDisplayInfo.ContainsKey(logicalName))
            {
                var savedAttrInfo = _settings.AttributeDisplayInfo[logicalName];
                
                foreach (var attrName in selected)
                {
                    var attrInfo = savedAttrInfo.ContainsKey(attrName) 
                        ? savedAttrInfo[attrName] 
                        : new AttributeDisplayInfo { LogicalName = attrName, DisplayName = attrName };
                    
                    var display = attrInfo.DisplayName ?? attrInfo.SchemaName ?? attrName;
                    // Use IsRequired from saved info or check if it's a required attribute
                    var isRequired = attrInfo.IsRequired || required.Contains(attrName);

                    // Apply text filter
                    if (!string.IsNullOrEmpty(filterText))
                    {
                        if (!display.ToLower().Contains(filterText) && 
                            !attrName.ToLower().Contains(filterText))
                            continue;
                    }

                    var item = new ListViewItem("");
                    item.Name = attrName;
                    item.Checked = true;  // All are selected in this view
                    // Show lock for required attributes (using saved IsRequired info)
                    item.SubItems.Add(isRequired ? "🔒" : "");
                    item.SubItems.Add(display);
                    item.SubItems.Add(attrName);
                    item.SubItems.Add(attrInfo.AttributeType ?? "(reconnect to load)");

                    // Apply visual styling
                    if (isRequired)
                    {
                        item.ForeColor = System.Drawing.Color.Blue;
                        item.Font = new System.Drawing.Font(item.Font, System.Drawing.FontStyle.Bold);
                    }
                    else
                    {
                        item.ForeColor = System.Drawing.Color.Black;
                    }

                    listViewAttributes.Items.Add(item);
                }
            }

            listViewAttributes.ItemChecked += ListViewAttributes_ItemChecked;  // Re-enable event
        }

        private HashSet<string> GetFormFields(string logicalName)
        {
            var fields = new HashSet<string>();
            
            if (!_tableForms.ContainsKey(logicalName))
                return fields;
            
            var formId = GetSelectedFormId(logicalName);
            if (formId == null)
                return fields;
            
            var form = _tableForms[logicalName].FirstOrDefault(f => f.FormId == formId);
            if (form?.Fields != null)
            {
                foreach (var field in form.Fields)
                    fields.Add(field.ToLower());
            }
            
            return fields;
        }

        private void ListViewAttributes_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;

            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            var attrName = e.Item.Name;

            if (!_selectedAttributes.ContainsKey(logicalName))
                _selectedAttributes[logicalName] = new HashSet<string>();

            // Check if required
            var required = GetRequiredAttributes(_selectedTables[logicalName]);
            if (required.Contains(attrName))
            {
                e.Item.Checked = true;  // Force required attributes to stay checked
                return;
            }

            if (e.Item.Checked)
            {
                _selectedAttributes[logicalName].Add(attrName);
                // Update color to black when checked (unless it's required which stays blue/bold)
                if (!required.Contains(attrName))
                {
                    e.Item.ForeColor = System.Drawing.Color.Black;
                }
            }
            else
            {
                _selectedAttributes[logicalName].Remove(attrName);
                // Update color to gray when unchecked
                e.Item.ForeColor = System.Drawing.Color.Gray;
            }

            UpdateSelectedTableRow(logicalName);
            SaveSettings();
        }

        private void TxtAttrSearch_TextChanged(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count > 0)
            {
                var logicalName = listViewSelectedTables.SelectedItems[0].Name;
                UpdateAttributesDisplay(logicalName);
            }
        }

        private void RadioShowMode_CheckedChanged(object sender, EventArgs e)
        {
            // Only process when a radio button is being checked (not unchecked)
            if (sender is System.Windows.Forms.RadioButton radio && !radio.Checked)
                return;
                
            if (listViewSelectedTables.SelectedItems.Count > 0)
            {
                var logicalName = listViewSelectedTables.SelectedItems[0].Name;
                UpdateAttributesDisplay(logicalName);
            }
            
            SaveSettings();  // Save the show mode preference
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            listViewAttributes.ItemChecked -= ListViewAttributes_ItemChecked;
            
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                item.Checked = true;
            }
            
            listViewAttributes.ItemChecked += ListViewAttributes_ItemChecked;

            if (listViewSelectedTables.SelectedItems.Count > 0)
            {
                var logicalName = listViewSelectedTables.SelectedItems[0].Name;
                var selected = _selectedAttributes[logicalName];
                foreach (ListViewItem item in listViewAttributes.Items)
                {
                    selected.Add(item.Name);
                }
                UpdateSelectedTableRow(logicalName);
                SaveSettings();
            }
        }

        private void BtnDeselectAll_Click(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;

            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            var required = GetRequiredAttributes(_selectedTables[logicalName]);

            listViewAttributes.ItemChecked -= ListViewAttributes_ItemChecked;
            
            // Keep only required attributes
            _selectedAttributes[logicalName] = new HashSet<string>(required);
            
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                item.Checked = required.Contains(item.Name);
            }
            
            listViewAttributes.ItemChecked += ListViewAttributes_ItemChecked;
            
            UpdateSelectedTableRow(logicalName);
            SaveSettings();
        }

        private void BtnSelectFromForm_Click(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;

            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            var formFields = GetFormFields(logicalName);

            if (!formFields.Any())
            {
                MessageBox.Show("No form fields available. Please select a form first.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            listViewAttributes.ItemChecked -= ListViewAttributes_ItemChecked;

            var selected = _selectedAttributes[logicalName];
            var requiredAttrs = GetRequiredAttributes(_selectedTables[logicalName]);
            
            // Clear all selections
            selected.Clear();
            
            // Always include required attributes
            foreach (var req in requiredAttrs)
                selected.Add(req);
            
            // Add form fields
            foreach (var field in formFields)
            {
                selected.Add(field);
            }

            // Update checkbox states to match
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                item.Checked = selected.Contains(item.Name);
            }

            listViewAttributes.ItemChecked += ListViewAttributes_ItemChecked;

            UpdateSelectedTableRow(logicalName);
            SaveSettings();
            SetStatus($"Selected {formFields.Count} fields from form.");
        }



        private void ListViewSelectedTables_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Toggle sort direction if same column, otherwise sort ascending
            if (e.Column == _selectedTablesSortColumn)
                _selectedTablesSortAscending = !_selectedTablesSortAscending;
            else
            {
                _selectedTablesSortColumn = e.Column;
                _selectedTablesSortAscending = true;
            }

            // Get all items as array
            var items = listViewSelectedTables.Items.Cast<ListViewItem>().ToArray();
            
            // Sort items
            var sorted = _selectedTablesSortAscending
                ? items.OrderBy(item => item.SubItems[e.Column].Text)
                : items.OrderByDescending(item => item.SubItems[e.Column].Text);

            // Clear and re-add sorted items
            listViewSelectedTables.Items.Clear();
            listViewSelectedTables.Items.AddRange(sorted.ToArray());
        }

        private void ListViewAttributes_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Toggle sort direction if same column, otherwise sort ascending
            if (e.Column == _attributesSortColumn)
                _attributesSortAscending = !_attributesSortAscending;
            else
            {
                _attributesSortColumn = e.Column;
                _attributesSortAscending = true;
            }

            // Temporarily disable ItemChecked event during sorting
            listViewAttributes.ItemChecked -= ListViewAttributes_ItemChecked;

            // Get all items as array
            var items = listViewAttributes.Items.Cast<ListViewItem>().ToArray();
            
            // Sort items
            var sorted = _attributesSortAscending
                ? items.OrderBy(item => item.SubItems[e.Column].Text)
                : items.OrderByDescending(item => item.SubItems[e.Column].Text);

            // Clear and re-add sorted items
            listViewAttributes.Items.Clear();
            listViewAttributes.Items.AddRange(sorted.ToArray());

            // Re-enable ItemChecked event
            listViewAttributes.ItemChecked += ListViewAttributes_ItemChecked;
        }

        private async Task SwitchToConfiguration(string configurationName, bool clearCredentials = false)
        {
            try
            {
                // Save current settings and cache first
                SaveSettings();

                // Get the URL from the new configuration before switching
                var newSettings = _settingsManager.GetConfiguration(configurationName);
                var currentEnvUrl = _settings.LastEnvironmentUrl;
                var newEnvUrl = newSettings.LastEnvironmentUrl;
                
                // Normalize URLs for comparison (remove https:// prefix if present)
                var normalizeUrl = (string? url) => {
                    if (string.IsNullOrEmpty(url)) return "";
                    if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        return url.Substring(8);
                    return url;
                };
                
                var currentUrlNormalized = normalizeUrl(currentEnvUrl);
                var newUrlNormalized = normalizeUrl(newEnvUrl);
                var sameEnvironment = !string.IsNullOrEmpty(currentUrlNormalized) && 
                                     !string.IsNullOrEmpty(newUrlNormalized) &&
                                     currentUrlNormalized.Equals(newUrlNormalized, StringComparison.OrdinalIgnoreCase);

                // Switch to the new configuration
                _settingsManager.SwitchToConfiguration(configurationName);

                // Load the new configuration
                _settings = newSettings;
                
                // Load the cache for this configuration
                _cache = _settingsManager.LoadCache(configurationName) ?? new MetadataCache();

                // Apply to UI - Configuration name IS the project name
                _isLoading = true;
                txtEnvironmentUrl.Text = _settings.LastEnvironmentUrl ?? "";
                txtProjectName.Text = configurationName;  // Use config name as project name
                txtOutputFolder.Text = _settings.OutputFolder ?? "";
                
                // Restore radio button state
                if (_settings.ShowAllAttributes)
                    radioShowAll.Checked = true;
                else
                    radioShowSelected.Checked = true;

                // Restore current solution name
                _currentSolutionName = _settings.LastSolution;

                // Restore star-schema configuration
                _factTable = _settings.FactTable;
                _relationships = _settings.Relationships ?? new List<RelationshipConfig>();

                // Clear UI first
                listViewSelectedTables.Items.Clear();
                listViewAttributes.Items.Clear();
                _selectedTables.Clear();
                _selectedAttributes.Clear();
                _selectedViews.Clear();
                _tableAttributes.Clear();
                _tableForms.Clear();
                _tableViews.Clear();

                // Restore from cache if valid
                var statusMsg = "";
                if (!string.IsNullOrEmpty(_settings.LastEnvironmentUrl) && 
                    !string.IsNullOrEmpty(_settings.LastSolution) &&
                    _cache.IsValidFor(_settings.LastEnvironmentUrl, _settings.LastSolution))
                {
                    RestoreFromCache();
                    EnableMetadataDependentControls(true);
                    statusMsg = $"Loaded {_selectedTables.Count} tables from cache.";
                }
                else if (_settings.SelectedTables.Any())
                {
                    // Cache is invalid but we have settings - restore minimal state
                    RestoreFromSettings();
                    EnableMetadataDependentControls(false);
                    statusMsg = $"Loaded {_selectedTables.Count} tables from settings. Reconnect to refresh metadata.";
                }
                
                // If environment changed and clearCredentials is true, reconnect with forced re-authentication
                if (clearCredentials && !sameEnvironment && !string.IsNullOrEmpty(newEnvUrl))
                {
                    _client = null;
                    lblConnectionStatus.Text = "Not Connected";
                    lblConnectionStatus.ForeColor = System.Drawing.Color.Red;
                    
                    statusMsg = "Configuration switched. Reconnect to load metadata.";
                }
                else
                {
                    // No cache and no settings, disable controls
                    EnableMetadataDependentControls(false);
                    statusMsg = "No previous data for this configuration.";
                }

                UpdateTableCount();
                
                // Refresh the semantic model dropdown
                RefreshSemanticModelDropdown();
                
                _isLoading = false;

                // Update title
                this.Text = $"Dataverse Metadata Extractor for Power BI - {configurationName}";
                
                // If we have an active connection and same environment, just refresh metadata without reconnecting
                if (_client != null && sameEnvironment)
                {
                    lblConnectionStatus.Text = "Connected";
                    lblConnectionStatus.ForeColor = System.Drawing.Color.Green;
                    btnSelectTables.Enabled = true;
                    
                    // Revalidate metadata if we have selected tables (uses existing connection)
                    if (_selectedTables.Any())
                    {
                        await RevalidateMetadata();
                        statusMsg = $"Metadata refreshed for {_selectedTables.Count} tables using existing connection.";
                    }
                    else
                    {
                        statusMsg = "Switched to configuration with same environment. Connection maintained.";
                    }
                }
                // If we have an active connection but different environment, disconnect and require reconnect
                else if (_client != null && !sameEnvironment)
                {
                    _client = null;
                    lblConnectionStatus.Text = "Not connected";
                    lblConnectionStatus.ForeColor = System.Drawing.Color.Gray;
                    btnSelectTables.Enabled = false;
                    statusMsg += " Please reconnect to the new environment.";
                }
                else
                {
                    lblConnectionStatus.Text = "Not connected";
                    lblConnectionStatus.ForeColor = System.Drawing.Color.Gray;
                    btnSelectTables.Enabled = false;
                }
                
                // Show status
                SetStatus(statusMsg);

                MessageBox.Show($"Switched to configuration '{configurationName}'.\n\n{statusMsg}", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _isLoading = false;
                MessageBox.Show($"Failed to switch configuration:\n{ex.Message}\n\n{ex.StackTrace}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // DEBUG: Check state before closing
            var attrInfoCount = _settings.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
            var tableAttrCount = _tableAttributes.Sum(t => t.Value.Count);
            Services.DebugLogger.LogSection("MainForm_FormClosing - Entry",
                $"_settings.AttributeDisplayInfo: {attrInfoCount} attrs across {_settings.AttributeDisplayInfo?.Count ?? 0} tables\n" +
                $"_tableAttributes: {tableAttrCount} attrs across {_tableAttributes.Count} tables");
            
            SaveSettings();
            SaveCache();

            Services.DebugLogger.Log($"Log saved to: {Services.DebugLogger.GetLogPath()}");
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Save window size when user resizes
            if (!_isLoading && this.WindowState == FormWindowState.Normal)
            {
                SaveSettings();
            }
        }

        private void BtnCalendarTable_Click(object? sender, EventArgs e)
        {
            if (!_selectedTables.Any())
            {
                MessageBox.Show("No tables selected. Please select tables first.", "No Tables",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check if Date table already exists in config
            if (_dateTableConfig != null)
            {
                var result = MessageBox.Show(
                    "A Calendar Table configuration already exists. Do you want to update it?\n\n" +
                    "Note: This will only update the primary date relationship and timezone adjustments.\n" +
                    "If a Date table already exists in your model, it will be preserved.",
                    "Update Calendar Configuration",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
            }

            // Build full attribute display info from _tableAttributes (includes all attributes, not just selected)
            var fullAttributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>();
            foreach (var tableName in _selectedTables.Keys)
            {
                if (_tableAttributes.ContainsKey(tableName))
                {
                    var attrDict = new Dictionary<string, AttributeDisplayInfo>();
                    foreach (var attr in _tableAttributes[tableName])
                    {
                        attrDict[attr.LogicalName] = new AttributeDisplayInfo
                        {
                            LogicalName = attr.LogicalName,
                            DisplayName = attr.DisplayName,
                            SchemaName = attr.SchemaName,
                            AttributeType = attr.AttributeType,
                            IsRequired = false,
                            Targets = attr.Targets
                        };
                    }
                    fullAttributeDisplayInfo[tableName] = attrDict;
                }
            }
            
            using var dialog = new CalendarTableDialog(
                _selectedTables,
                fullAttributeDisplayInfo,
                _selectedAttributes,
                _factTable,
                _dateTableConfig);

            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Config != null)
            {
                _dateTableConfig = dialog.Config;
                SaveSettings();

                var tzName = !string.IsNullOrEmpty(_dateTableConfig.TimeZoneId)
                    ? TimeZoneInfo.FindSystemTimeZoneById(_dateTableConfig.TimeZoneId)?.DisplayName ?? _dateTableConfig.TimeZoneId
                    : "UTC";

                MessageBox.Show(
                    $"Calendar Table configured successfully.\n\n" +
                    $"Primary Date: {_dateTableConfig.PrimaryDateTable}.{_dateTableConfig.PrimaryDateField}\n" +
                    $"Time Zone: {tzName}\n" +
                    $"Date Range: {_dateTableConfig.StartYear} - {_dateTableConfig.EndYear}\n" +
                    $"Fields to adjust: {_dateTableConfig.WrappedFields.Count}\n\n" +
                    $"The Date table will be created when you build the semantic model.",
                    "Configuration Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private async void BtnBuildSemanticModel_Click(object? sender, EventArgs e)
        {
            if (!_selectedTables.Any())
            {
                MessageBox.Show("No tables selected.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var semanticModelName = txtProjectName.Text.Trim();
            if (string.IsNullOrEmpty(semanticModelName))
            {
                MessageBox.Show("Please enter a semantic model name.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var envUrl = txtEnvironmentUrl.Text.Trim();
            if (string.IsNullOrEmpty(envUrl))
            {
                MessageBox.Show("Please enter a Dataverse URL.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var baseOutputFolder = txtOutputFolder.Text.Trim();
            if (string.IsNullOrEmpty(baseOutputFolder))
            {
                // Default to Reports/SemanticModelName
                var baseDir = Path.GetDirectoryName(Application.ExecutablePath);
                baseOutputFolder = Path.Combine(baseDir ?? "", "..", "..", "..", "..", "Reports");
                baseOutputFolder = Path.GetFullPath(baseOutputFolder);
                txtOutputFolder.Text = baseOutputFolder;
            }

            // Combine base folder with semantic model name as subfolder
            var outputFolder = Path.Combine(baseOutputFolder, semanticModelName);

            // Get template path
            var templatePath = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? "",
                "..", "..", "..", "..", "PBIP_DefaultTemplate");
            templatePath = Path.GetFullPath(templatePath);

            if (!Directory.Exists(templatePath))
            {
                MessageBox.Show($"PBIP template not found at: {templatePath}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnBuildSemanticModel.Enabled = false;
                SetStatus("Building semantic model...");
                ShowProgress(true);

                // Build export tables
                var exportTables = BuildExportTables();

                // Build export relationships
                var exportRelationships = _relationships.Select(r => new ExportRelationship
                {
                    SourceTable = r.SourceTable,
                    SourceAttribute = r.SourceAttribute,
                    TargetTable = r.TargetTable,
                    DisplayName = r.DisplayName,
                    IsActive = r.IsActive,
                    IsSnowflake = r.IsSnowflake,
                    AssumeReferentialIntegrity = r.AssumeReferentialIntegrity
                }).ToList();

                // Don't add https:// - TMDL format stores URLs without protocol prefix
                // The URL validation and https:// addition happens in DataverseClient when actually connecting
                var fullUrl = envUrl;

                // First analyze changes (non-blocking)
                SemanticModelBuilder? builder = null;
                List<SemanticModelChange>? changes = null;

                await Task.Run(() =>
                {
                    builder = new SemanticModelBuilder(templatePath, msg =>
                    {
                        this.Invoke((MethodInvoker)delegate { SetStatus(msg); });
                    });

                    changes = builder.AnalyzeChanges(
                        semanticModelName,
                        outputFolder,
                        fullUrl,
                        exportTables,
                        exportRelationships,
                        _settings.AttributeDisplayInfo,
                        _dateTableConfig);
                });

                if (builder == null || changes == null)
                {
                    ShowProgress(false);
                    SetStatus("Build failed");
                    return;
                }

                // Show preview dialog on UI thread
                bool userApproved = false;
                bool createBackup = false;

                using (var dialog = new SemanticModelChangesDialog(changes))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK && dialog.UserApproved)
                    {
                        userApproved = true;
                        createBackup = dialog.CreateBackup;
                    }
                }

                if (!userApproved)
                {
                    ShowProgress(false);
                    SetStatus("Build cancelled");
                    MessageBox.Show(
                        "Semantic model build was cancelled.",
                        "Build Cancelled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // Apply changes
                bool buildSucceeded = false;
                await Task.Run(() =>
                {
                    buildSucceeded = builder.ApplyChanges(
                        semanticModelName,
                        outputFolder,
                        fullUrl,
                        exportTables,
                        exportRelationships,
                        _settings.AttributeDisplayInfo,
                        createBackup,
                        _dateTableConfig);
                });

                ShowProgress(false);

                if (!buildSucceeded)
                {
                    SetStatus("Build failed");
                    MessageBox.Show(
                        "Semantic model build failed.",
                        "Build Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                SetStatus("Semantic model build complete!");

                var pbipPath = Path.GetFullPath(Path.Combine(outputFolder, "PBIP", $"{semanticModelName}.pbip"));
                var result = MessageBox.Show(
                    $"Semantic model built successfully!\n\n" +
                    $"Location: {pbipPath}\n\n" +
                    $"Tables: {exportTables.Count}\n" +
                    $"Relationships: {exportRelationships.Count}\n\n" +
                    $"Would you like to open the semantic model in Power BI Desktop?",
                    "Build Complete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = pbipPath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception openEx)
                    {
                        MessageBox.Show($"Failed to open Power BI Desktop:\n{openEx.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Build failed:\n{ex.Message}\n\n{ex.StackTrace}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Build failed.");
            }
            finally
            {
                btnBuildSemanticModel.Enabled = true;
                ShowProgress(false);
            }
        }

        private List<ExportTable> BuildExportTables()
        {
            Services.DebugLogger.Log("=== BuildExportTables Starting ===");
            Services.DebugLogger.Log($"  _selectedTables count: {_selectedTables.Count}");
            Services.DebugLogger.Log($"  _selectedViews count: {_selectedViews.Count}");
            Services.DebugLogger.Log($"  _selectedViews keys: {string.Join(", ", _selectedViews.Keys)}");
            Services.DebugLogger.Log($"  _tableViews count: {_tableViews.Count}");
            Services.DebugLogger.Log($"  _cache.TableViews count: {_cache?.TableViews?.Count ?? 0}");
            
            // If _tableViews is empty but we have selections, reload from cache
            if (_tableViews.Count == 0 && _cache?.TableViews != null && _cache.TableViews.Any())
            {
                Services.DebugLogger.Log("  Reloading views from cache...");
                foreach (var tableName in _selectedTables.Keys)
                {
                    if (_cache.TableViews.ContainsKey(tableName))
                    {
                        _tableViews[tableName] = _cache.TableViews[tableName];
                        Services.DebugLogger.Log($"    Loaded {_cache.TableViews[tableName].Count} views for {tableName}");
                    }
                }
                Services.DebugLogger.Log($"  After reload: _tableViews count: {_tableViews.Count}");
            }
            
            // Check if any cached views are missing FetchXML (from older cache format)
            var tablesNeedingReload = new List<string>();
            foreach (var kvp in _tableViews)
            {
                var tableName = kvp.Key;
                var views = kvp.Value;
                if (views.Any() && views.All(v => string.IsNullOrEmpty(v.FetchXml)))
                {
                    tablesNeedingReload.Add(tableName);
                    Services.DebugLogger.Log($"  WARNING: Table '{tableName}' has views without FetchXML");
                }
            }
            
            if (tablesNeedingReload.Any())
            {
                var tableNames = string.Join("\n  - ", tablesNeedingReload.Select(name => 
                    _selectedTables.ContainsKey(name) ? _selectedTables[name].DisplayName : name));
                    
                MessageBox.Show(
                    $"The following tables have cached views without filter data (FetchXML):\n\n  - {tableNames}\n\n" +
                    "This happens when views were loaded with an older version of the tool.\n\n" +
                    "To fix this:\n" +
                    "1. Click OK to close this dialog\n" +
                    "2. For each table listed above, click the 'Reload Table' button\n" +
                    "3. Then try building the semantic model again\n\n" +
                    "Note: Reloading will refresh all metadata from Dataverse including view filters.",
                    "Views Need Reloading",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                    
                return new List<ExportTable>();
            }
            
            var exportTables = new List<ExportTable>();

            foreach (var kvp in _selectedTables)
            {
                var logicalName = kvp.Key;
                var table = kvp.Value;

                // Get selected form
                var forms = new List<ExportForm>();
                if (_tableForms.ContainsKey(logicalName))
                {
                    var formId = GetSelectedFormId(logicalName);
                    var form = _tableForms[logicalName].FirstOrDefault(f => f.FormId == formId);
                    if (form != null)
                    {
                        forms.Add(new ExportForm
                        {
                            FormId = form.FormId,
                            FormName = form.Name,
                            FieldCount = form.Fields?.Count ?? 0
                        });
                    }
                }

                // Get selected view
                ExportView? view = null;
                Services.DebugLogger.Log($"  Checking view for table: {logicalName}");
                Services.DebugLogger.Log($"    _tableViews.ContainsKey({logicalName}): {_tableViews.ContainsKey(logicalName)}");
                Services.DebugLogger.Log($"    _selectedViews.ContainsKey({logicalName}): {_selectedViews.ContainsKey(logicalName)}");
                
                if (_tableViews.ContainsKey(logicalName) && _selectedViews.ContainsKey(logicalName))
                {
                    var viewId = _selectedViews[logicalName];
                    Services.DebugLogger.Log($"    Selected view ID: {viewId}");
                    var viewData = _tableViews[logicalName].FirstOrDefault(v => v.ViewId == viewId);
                    if (viewData != null)
                    {
                        Services.DebugLogger.Log($"    Found view: {viewData.Name}");
                        Services.DebugLogger.Log($"    FetchXML length: {viewData.FetchXml?.Length ?? 0}");
                        view = new ExportView
                        {
                            ViewId = viewData.ViewId,
                            ViewName = viewData.Name,
                            FetchXml = viewData.FetchXml
                        };
                    }
                    else
                    {
                        Services.DebugLogger.Log($"    WARNING: View ID {viewId} not found in _tableViews!");
                    }
                }
                else
                {
                    Services.DebugLogger.Log($"    No view selected for this table");
                }

                // Get selected attributes
                var selectedAttrNames = _selectedAttributes.ContainsKey(logicalName)
                    ? _selectedAttributes[logicalName]
                    : new HashSet<string>();
                var allAttrs = _tableAttributes.ContainsKey(logicalName)
                    ? _tableAttributes[logicalName]
                    : new List<AttributeMetadata>();

                var attributes = allAttrs
                    .Where(a => selectedAttrNames.Contains(a.LogicalName))
                    .ToList();

                // If no attributes loaded from cache, try to build from settings
                if (!attributes.Any() && _settings.AttributeDisplayInfo.ContainsKey(logicalName))
                {
                    attributes = _settings.AttributeDisplayInfo[logicalName]
                        .Where(a => selectedAttrNames.Contains(a.Key))
                        .Select(a => new AttributeMetadata
                        {
                            LogicalName = a.Key,
                            DisplayName = a.Value.DisplayName,
                            SchemaName = a.Value.SchemaName,
                            AttributeType = a.Value.AttributeType,
                            Targets = a.Value.Targets
                        })
                        .ToList();
                }

                // Determine table role
                var role = logicalName == _factTable ? "Fact" : "Dimension";

                // Check if table has a statecode attribute
                var hasStateCode = allAttrs.Any(a => a.LogicalName.Equals("statecode", StringComparison.OrdinalIgnoreCase));
                if (!hasStateCode && _settings.AttributeDisplayInfo.ContainsKey(logicalName))
                {
                    hasStateCode = _settings.AttributeDisplayInfo[logicalName]
                        .Any(a => a.Key.Equals("statecode", StringComparison.OrdinalIgnoreCase));
                }

                exportTables.Add(new ExportTable
                {
                    LogicalName = table.LogicalName,
                    DisplayName = table.DisplayName,
                    SchemaName = table.SchemaName,
                    ObjectTypeCode = table.ObjectTypeCode,
                    PrimaryIdAttribute = table.PrimaryIdAttribute,
                    PrimaryNameAttribute = table.PrimaryNameAttribute,
                    Role = role,
                    HasStateCode = hasStateCode,
                    Forms = forms,
                    View = view,
                    Attributes = attributes
                });
            }

            return exportTables;
        }
    }
}
