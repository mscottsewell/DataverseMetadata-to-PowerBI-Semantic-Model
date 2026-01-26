using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DataverseMetadataExtractor.Models;
using DataverseMetadataExtractor.Services;
using Newtonsoft.Json;

namespace DataverseMetadataExtractor.Forms
{
    public partial class MainForm : Form
    {
        private DataverseClient? _client;
        private readonly SettingsManager _settingsManager;
        private AppSettings _settings;
        private MetadataCache _cache;
        
        // State management
        private Dictionary<string, TableInfo> _selectedTables = new();  // logicalName -> table
        private Dictionary<string, List<FormMetadata>> _tableForms = new();  // logicalName -> forms
        private Dictionary<string, List<ViewMetadata>> _tableViews = new();  // logicalName -> views
        private Dictionary<string, List<AttributeMetadata>> _tableAttributes = new();  // logicalName -> attributes
        private Dictionary<string, HashSet<string>> _selectedAttributes = new();  // logicalName -> selected attr names
        private Dictionary<string, string> _selectedViews = new();  // logicalName -> selected view id
        private Dictionary<string, bool> _loadingStates = new();  // logicalName -> is loading

        // Star-schema state
        private string? _factTable = null;  // Logical name of the fact table
        private List<RelationshipConfig> _relationships = new();  // Configured relationships
        
        // Sorting state
        private int _selectedTablesSortColumn = -1;
        private bool _selectedTablesSortAscending = true;
        private int _attributesSortColumn = -1;
        private bool _attributesSortAscending = true;
        
        private string? _currentSolutionName;
        private bool _isLoading = false;  // Prevent SaveSettings during initial load

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
                // Restore settings (strip https:// from URL for display)
                var url = _settings.LastEnvironmentUrl ?? "";
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

                // Restore star-schema configuration
                _factTable = _settings.FactTable;
                _relationships = _settings.Relationships ?? new List<RelationshipConfig>();

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
                var roleText = isFact ? "⭐ Fact" : "Dim";
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
                        _selectedViews[logicalName] = defaultView.ViewId;
                }
                
                // Add to UI
                AddTableToSelectedList(tableInfo);
            }
            
            UpdateTableCount();
            
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

        private void EnableMetadataDependentControls(bool enabled)
        {
            btnExport.Enabled = enabled;
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
            _settings.TableForms = GetSelectedFormIds();
            _settings.TableFormNames = GetSelectedFormNames();
            _settings.TableViews = _selectedViews.ToDictionary(k => k.Key, v => v.Value);
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
            // Only update if we have full metadata loaded, otherwise preserve existing display info
            if (_tableAttributes.Any())
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
                    
                    if (!_tableAttributes.ContainsKey(tableName))
                    {
                        Services.DebugLogger.Log($"    SKIPPED: Not in _tableAttributes");
                        continue;
                    }
                    
                    if (!_selectedTables.ContainsKey(tableName))
                    {
                        Services.DebugLogger.Log($"    SKIPPED: Not in _selectedTables");
                        continue;
                    }
                    
                    var requiredAttrs = GetRequiredAttributes(_selectedTables[tableName]);
                    var attrDict = new Dictionary<string, AttributeDisplayInfo>();
                    var selectedAttrCount = _selectedAttributes[tableName].Count;
                    Services.DebugLogger.Log($"    Has {selectedAttrCount} selected attributes");
                    
                    foreach (var attrLogicalName in _selectedAttributes[tableName])
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
                    
                    Services.DebugLogger.Log($"    attrDict has {attrDict.Count} attributes, attrDict.Any() = {attrDict.Any()}");
                    
                    if (attrDict.Any())
                        _settings.AttributeDisplayInfo[tableName] = attrDict;
                    else
                        Services.DebugLogger.Log($"    SKIPPED: attrDict is empty");
                }
                
                Services.DebugLogger.Log($"Final AttributeDisplayInfo has {_settings.AttributeDisplayInfo.Sum(t => t.Value.Count)} attributes across {_settings.AttributeDisplayInfo.Count} tables");
            }
            // else: keep existing AttributeDisplayInfo from loaded settings
            
            // DEBUG: Check AttributeDisplayInfo after update
            var afterAttrInfoCount = _settings.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
            Services.DebugLogger.LogSection("SaveSettings - After AttributeDisplayInfo Update",
                $"_settings.AttributeDisplayInfo: {afterAttrInfoCount} attrs across {_settings.AttributeDisplayInfo?.Count ?? 0} tables\n" +
                $"Updated: {_tableAttributes.Any()}");
            
            // Project name always matches configuration name
            var configName = _settingsManager.GetCurrentConfigurationName();
            _settings.ProjectName = configName;
            
            _settings.OutputFolder = txtOutputFolder.Text;
            _settings.WindowGeometry = $"{Width},{Height}";
            _settings.ShowAllAttributes = radioShowAll.Checked;

            // Save star-schema configuration
            _settings.FactTable = _factTable;
            _settings.Relationships = _relationships;
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

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            var url = txtEnvironmentUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter an environment URL.", "Validation", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Add https:// for connection (but don't update the text box)
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
                    SwitchToConfiguration(mostRecentConfigForUrl);
                    return; // Don't connect yet, let user click Connect again
                }
            }

            try
            {
                btnConnect.Enabled = false;
                lblConnectionStatus.Text = "Connecting...";
                SetStatus("Authenticating to Dataverse...");
                ShowProgress(true);

                _client = new DataverseClient(connectionUrl);
                await _client.AuthenticateAsync();

                lblConnectionStatus.Text = "Connected";
                btnSelectTables.Enabled = true;  // Enable table selection
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
                btnSelectTables.Enabled = false;  // Disable table selection
                MessageBox.Show($"Connection failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Connection failed.");
            }
            finally
            {
                btnConnect.Enabled = true;
                ShowProgress(false);
            }
        }

        private void BtnSelectTables_Click(object sender, EventArgs e)
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
                    AddTablesInBulk(dialog.AllSelectedTables);
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

        private async void AddTablesInBulk(List<TableInfo> tables)
        {
            // Add all tables to the list first
            foreach (var table in tables)
            {
                var logicalName = table.LogicalName;
                if (_selectedTables.ContainsKey(logicalName))
                    continue;  // Already added
                
                _selectedTables[logicalName] = table;
                _selectedAttributes[logicalName] = GetRequiredAttributes(table);
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
                var viewsTask = _client.GetViewsAsync(logicalName);

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
                
                // Check if this is a new table (not restored from cache) by seeing if only required attrs are selected
                bool isNewTable = selectedAttrs.Count == requiredAttrs.Count && requiredAttrs.All(r => selectedAttrs.Contains(r));
                
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
                var views = await _client.GetViewsAsync(logicalName);

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

                // Get all fields from all forms
                var allFormFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var form in forms)
                {
                    if (form.Fields != null)
                    {
                        foreach (var field in form.Fields)
                            allFormFields.Add(field);
                    }
                }

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

                // Add new attributes that are in forms but not yet selected
                foreach (var formField in allFormFields)
                {
                    if (currentAttributeNames.Contains(formField) && !selectedAttrs.Contains(formField))
                    {
                        selectedAttrs.Add(formField);
                    }
                }

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
            var roleText = isFact ? "⭐ Fact" : "Dim";
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
                item.SubItems[1].Text = isFact ? "⭐ Fact" : "Dim";
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

        private void BtnRemoveTable_Click(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;

            var item = listViewSelectedTables.SelectedItems[0];
            var logicalName = item.Name;

            _selectedTables.Remove(logicalName);
            _tableForms.Remove(logicalName);
            _tableViews.Remove(logicalName);
            _tableAttributes.Remove(logicalName);
            _selectedAttributes.Remove(logicalName);
            _selectedViews.Remove(logicalName);
            _loadingStates.Remove(logicalName);

            listViewSelectedTables.Items.Remove(item);
            listViewAttributes.Items.Clear();

            UpdateTableCount();
            SaveSettings();
            SaveCache();
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
                }

                // If form changed, auto-select fields from the new form
                if (formChanged && dialog.SelectedForm != null)
                {
                    AutoSelectFormFields(logicalName, dialog.SelectedForm);
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

        private void TxtProjectName_TextChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            var newProjectName = txtProjectName.Text.Trim();
            if (string.IsNullOrWhiteSpace(newProjectName))
            {
                SaveSettings();
                return;
            }

            var currentConfigName = _settingsManager.GetCurrentConfigurationName();
            
            // If project name changed, rename the configuration to match
            if (newProjectName != currentConfigName)
            {
                try
                {
                    _settingsManager.RenameConfiguration(currentConfigName, newProjectName);
                    this.Text = $"Dataverse Metadata Extractor for Power BI - {newProjectName}";
                }
                catch
                {
                    // If rename fails (e.g., name already exists), revert the text
                    _isLoading = true;
                    txtProjectName.Text = currentConfigName;
                    _isLoading = false;
                    MessageBox.Show($"Cannot rename configuration to '{newProjectName}' - a configuration with that name already exists.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            
            SaveSettings();
        }

        private void TxtOutputFolder_TextChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void BtnBrowseOutput_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Output Folder",
                SelectedPath = txtOutputFolder.Text
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                txtOutputFolder.Text = dialog.SelectedPath;
            }
        }

        private async void BtnExport_Click(object sender, EventArgs e)
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

            var baseOutputFolder = txtOutputFolder.Text.Trim();
            if (string.IsNullOrEmpty(baseOutputFolder))
            {
                // Default to Reports/SemanticModelName
                var baseDir = Path.GetDirectoryName(Application.ExecutablePath);
                baseOutputFolder = Path.Combine(baseDir, "..", "..", "..", "..", "Reports");
                txtOutputFolder.Text = baseOutputFolder;
            }

            // Combine base folder with semantic model name as subfolder
            var outputFolder = Path.Combine(baseOutputFolder, semanticModelName);

            try
            {
                btnExport.Enabled = false;
                SetStatus("Preparing export...");
                ShowProgress(true);

                await Task.Run(() => ExportMetadata(semanticModelName, outputFolder));

                SetStatus($"Exported to {outputFolder}");
                ShowProgress(false);

                var totalAttrs = _selectedAttributes.Values.Sum(s => s.Count);
                var factDisplay = _factTable != null && _selectedTables.ContainsKey(_factTable)
                    ? _selectedTables[_factTable].DisplayName ?? _factTable
                    : "(none)";
                var directRels = _relationships.Count(r => !r.IsSnowflake);
                var snowflakeRels = _relationships.Count(r => r.IsSnowflake);
                var relInfo = snowflakeRels > 0
                    ? $"{directRels} direct + {snowflakeRels} snowflake"
                    : $"{directRels}";
                MessageBox.Show(
                    $"Metadata exported successfully!\n\n" +
                    $"File: {Path.Combine(outputFolder, semanticModelName + " Metadata Dictionary.json")}\n" +
                    $"Fact Table: {factDisplay}\n" +
                    $"Dimension Tables: {_selectedTables.Count - (_factTable != null ? 1 : 0)}\n" +
                    $"Relationships: {relInfo}\n" +
                    $"Total Attributes: {totalAttrs}",
                    "Export Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Export failed.");
            }
            finally
            {
                btnExport.Enabled = true;
                ShowProgress(false);
            }
        }

        private void ExportMetadata(string projectName, string outputFolder)
        {
            // Add https:// prefix for export metadata
            var envUrl = txtEnvironmentUrl.Text.Trim();
            if (!envUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                envUrl = "https://" + envUrl;

            // Export relationships
            var exportRelationships = _relationships.Select(r => new ExportRelationship
            {
                SourceTable = r.SourceTable,
                SourceAttribute = r.SourceAttribute,
                TargetTable = r.TargetTable,
                DisplayName = r.DisplayName,
                IsActive = r.IsActive,
                IsSnowflake = r.IsSnowflake
            }).ToList();

            var metadata = new ExportMetadata
            {
                Environment = envUrl,
                Solution = _currentSolutionName ?? "",
                ProjectName = projectName,
                FactTable = _factTable,
                Relationships = exportRelationships,
                Tables = new List<ExportTable>()
            };

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
                if (_tableViews.ContainsKey(logicalName) && _selectedViews.ContainsKey(logicalName))
                {
                    var viewId = _selectedViews[logicalName];
                    var viewData = _tableViews[logicalName].FirstOrDefault(v => v.ViewId == viewId);
                    if (viewData != null)
                    {
                        view = new ExportView
                        {
                            ViewId = viewData.ViewId,
                            ViewName = viewData.Name,
                            FetchXml = viewData.FetchXml
                        };
                    }
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

                // Determine table role
                var role = logicalName == _factTable ? "Fact" : "Dimension";

                metadata.Tables.Add(new ExportTable
                {
                    LogicalName = table.LogicalName,
                    DisplayName = table.DisplayName,
                    SchemaName = table.SchemaName,
                    ObjectTypeCode = table.ObjectTypeCode,
                    PrimaryIdAttribute = table.PrimaryIdAttribute,
                    PrimaryNameAttribute = table.PrimaryNameAttribute,
                    Role = role,
                    Forms = forms,
                    View = view,
                    Attributes = attributes
                });
            }

            // Save to file
            Directory.CreateDirectory(outputFolder);
            var outputFile = Path.Combine(outputFolder, $"{projectName} Metadata Dictionary.json");

            var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            File.WriteAllText(outputFile, json);

            // Also save DataverseURL.txt (with https:// prefix)
            var urlFile = Path.Combine(outputFolder, "DataverseURL.txt");
            var urlForFile = txtEnvironmentUrl.Text.Trim();
            if (!urlForFile.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                urlForFile = "https://" + urlForFile;
            File.WriteAllText(urlFile, urlForFile);
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

        private void ConfigurationsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            // Populate the Switch Configuration submenu
            switchConfigurationToolStripMenuItem.DropDownItems.Clear();

            var currentConfig = _settingsManager.GetCurrentConfigurationName();
            var allConfigs = _settingsManager.GetConfigurationNames();

            foreach (var configName in allConfigs)
            {
                var item = new ToolStripMenuItem(configName);
                item.Click += (s, args) => SwitchToConfiguration(configName);
                
                // Check the current configuration
                if (configName == currentConfig)
                {
                    item.Checked = true;
                    item.Enabled = false; // Can't switch to current
                }

                switchConfigurationToolStripMenuItem.DropDownItems.Add(item);
            }

            // Update title to show current configuration
            this.Text = $"Dataverse Metadata Extractor for Power BI - {currentConfig}";
        }

        private void SwitchToConfiguration(string configurationName)
        {
            try
            {
                // Save current settings and cache first
                SaveSettings();

                // Switch to the new configuration
                _settingsManager.SwitchToConfiguration(configurationName);

                // Load the new configuration
                _settings = _settingsManager.GetConfiguration(configurationName);
                
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
                else
                {
                    // No cache and no settings, disable controls
                    EnableMetadataDependentControls(false);
                    statusMsg = "No previous data for this configuration.";
                }

                UpdateTableCount();
                _isLoading = false;

                // Update title
                this.Text = $"Dataverse Metadata Extractor for Power BI - {configurationName}";
                
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

        private void NewConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var inputDialog = new Form())
            {
                inputDialog.Text = "New Configuration";
                inputDialog.Width = 400;
                inputDialog.Height = 150;
                inputDialog.StartPosition = FormStartPosition.CenterParent;

                var label = new Label { Text = "Configuration Name:", Left = 20, Top = 20, Width = 340 };
                var textBox = new TextBox { Left = 20, Top = 45, Width = 340 };
                var btnOk = new Button { Text = "Create", Left = 220, Top = 80, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 300, Top = 80, DialogResult = DialogResult.Cancel };
                
                btnOk.Click += (s, args) => {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        MessageBox.Show("Please enter a configuration name.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        inputDialog.DialogResult = DialogResult.None;
                    }
                };

                inputDialog.Controls.AddRange(new Control[] { label, textBox, btnOk, btnCancel });
                inputDialog.AcceptButton = btnOk;
                inputDialog.CancelButton = btnCancel;

                if (inputDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var newName = textBox.Text.Trim();
                        var newSettings = new AppSettings { ProjectName = newName };
                        _settingsManager.CreateNewConfiguration(newName, newSettings);
                        
                        // Switch to the new configuration
                        SwitchToConfiguration(newName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to create configuration:\n{ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void RenameConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var currentName = _settingsManager.GetCurrentConfigurationName();

            using (var inputDialog = new Form())
            {
                inputDialog.Text = "Rename Configuration";
                inputDialog.Width = 400;
                inputDialog.Height = 150;
                inputDialog.StartPosition = FormStartPosition.CenterParent;

                var label = new Label { Text = $"Rename '{currentName}' to:", Left = 20, Top = 20, Width = 340 };
                var textBox = new TextBox { Text = currentName, Left = 20, Top = 45, Width = 340 };
                var btnOk = new Button { Text = "Rename", Left = 220, Top = 80, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 300, Top = 80, DialogResult = DialogResult.Cancel };
                
                btnOk.Click += (s, args) => {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        MessageBox.Show("Please enter a configuration name.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        inputDialog.DialogResult = DialogResult.None;
                    }
                };

                inputDialog.Controls.AddRange(new Control[] { label, textBox, btnOk, btnCancel });
                inputDialog.AcceptButton = btnOk;
                inputDialog.CancelButton = btnCancel;

                if (inputDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var newName = textBox.Text.Trim();
                        _settingsManager.RenameConfiguration(currentName, newName);
                        
                        // Update project name to match new config name
                        _isLoading = true;
                        txtProjectName.Text = newName;
                        _isLoading = false;
                        
                        this.Text = $"Dataverse Metadata Extractor for Power BI - {newName}";

                        MessageBox.Show($"Configuration renamed to '{newName}'.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to rename configuration:\n{ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void DeleteConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var currentName = _settingsManager.GetCurrentConfigurationName();
            
            var result = MessageBox.Show(
                $"Are you sure you want to delete the configuration '{currentName}'?\n\nThis cannot be undone.",
                "Delete Configuration",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    _settingsManager.DeleteConfiguration(currentName);
                    
                    // Load the new current configuration
                    _settings = _settingsManager.LoadSettings();
                    var newCurrentName = _settingsManager.GetCurrentConfigurationName();

                    // Apply to UI - project name matches config name
                    _isLoading = true;
                    txtEnvironmentUrl.Text = _settings.LastEnvironmentUrl ?? "";
                    txtProjectName.Text = newCurrentName;
                    txtOutputFolder.Text = _settings.OutputFolder ?? "";
                    _isLoading = false;

                    listViewSelectedTables.Items.Clear();
                    listViewAttributes.Items.Clear();
                    UpdateTableCount();

                    this.Text = $"Dataverse Metadata Extractor for Power BI - {newCurrentName}";

                    MessageBox.Show($"Configuration '{currentName}' deleted.\n\nSwitched to '{newCurrentName}'.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete configuration:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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
    }
}
