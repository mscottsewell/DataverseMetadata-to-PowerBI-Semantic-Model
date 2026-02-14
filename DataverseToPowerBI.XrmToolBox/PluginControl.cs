// =============================================================================
// PluginControl.cs - XrmToolBox Plugin User Interface
// =============================================================================
// Purpose: Main UI control for the XrmToolBox plugin.
//
// This control provides the full workflow for building Power BI semantic
// models from Dataverse metadata, integrated with XrmToolBox for:
//   - Connection management (uses XrmToolBox connection manager)
//   - Authentication (inherits CRM authentication from XrmToolBox)
//   - Plugin lifecycle (Load, Unload, ConnectionUpdated events)
//
// Architecture Notes:
//   - Uses IOrganizationService via XrmServiceAdapterImpl
//   - Integrates with XrmToolBox settings storage
//   - No custom OAuth - uses XrmToolBox connection
//
// Features:
//   - Solution and table selection
//   - Star-schema modeling (Fact/Dimension)
//   - Form and view selection
//   - Attribute selection with form-based presets
//   - Relationship configuration including snowflake
//   - TMDL generation for Power BI projects
//
// The plugin stores configuration per-environment in the XrmToolBox settings
// folder using SemanticModelManager.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using DataverseToPowerBI.Core.Interfaces;
using DataverseToPowerBI.Core.Models;
using DataverseToPowerBI.XrmToolBox.Services;
using XrmModels = DataverseToPowerBI.XrmToolBox.Models;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// XrmToolBox plugin control - main UI for the Dataverse to Power BI tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This control provides the full workflow for building Power BI semantic
    /// models from Dataverse metadata, with XrmToolBox connection management.
    /// </para>
    /// <para>
    /// Key workflow:
    /// </para>
    /// <list type="number">
    ///   <item>User connects via XrmToolBox connection manager</item>
    ///   <item>Select solution and tables (same as standalone)</item>
    ///   <item>Configure star-schema, forms, views, attributes</item>
    ///   <item>Generate TMDL files for Power BI Desktop</item>
    /// </list>
    /// </remarks>
    public partial class PluginControl : PluginControlBase
    {
        private XrmServiceAdapterImpl? _xrmAdapter;
        private string _templatePath;
        private SemanticModelManager _modelManager;
        private SemanticModelConfig? _currentModel;
        private string? _currentEnvironmentUrl;
        
        // State management - same as MainForm
        private Dictionary<string, TableInfo> _selectedTables = new Dictionary<string, TableInfo>();
        private Dictionary<string, List<FormMetadata>> _tableForms = new Dictionary<string, List<FormMetadata>>();
        private Dictionary<string, List<ViewMetadata>> _tableViews = new Dictionary<string, List<ViewMetadata>>();
        private Dictionary<string, List<AttributeMetadata>> _tableAttributes = new Dictionary<string, List<AttributeMetadata>>();
        private Dictionary<string, HashSet<string>> _selectedAttributes = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, string> _selectedFormIds = new Dictionary<string, string>();
        private Dictionary<string, string> _selectedViewIds = new Dictionary<string, string>();
        private Dictionary<string, string> _tableStorageModes = new Dictionary<string, string>();
        private Dictionary<string, bool> _loadingStates = new Dictionary<string, bool>();
        
        // Star-schema state
        private string? _factTable = null;
        private List<ExportRelationship> _relationships = new List<ExportRelationship>();
        
        // Display name override state
        private Dictionary<string, Dictionary<string, string>> _attributeDisplayNameOverrides = new Dictionary<string, Dictionary<string, string>>();
        
        // Sorting state
        private int _selectedTablesSortColumn = -1;
        private bool _selectedTablesSortAscending = true;
        private int _attributesSortColumn = -1;
        private bool _attributesSortAscending = true;
        private int _relationshipsSortColumn = -1;
        private bool _relationshipsSortAscending = true;
        private readonly ToolTip _versionToolTip = new ToolTip();

        // Pre-sorted attribute cache per table (built during metadata load)
        private Dictionary<string, List<AttributeMetadata>> _sortedAttributeCache = new Dictionary<string, List<AttributeMetadata>>();
        private int _sortedCacheSortColumn = -1;
        private bool _sortedCacheSortAscending = true;

        // Cached fonts to avoid GDI handle leaks (WinForms does not dispose replaced Fonts)
        private Font? _boldTableFont;
        private Font? _boldAttrFont;

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
        
        // Solution 
        private string? _currentSolutionName;
        private string? _currentSolutionId;
        private List<TableInfo> _solutionTables = new List<TableInfo>();
        
        private bool _isLoading = false;
        
        public PluginControl()
        {
            InitializeComponent();
            
            // Initialize the semantic model manager
            _modelManager = new SemanticModelManager();
            
            // Determine template path - try multiple locations in priority order
            // 1. Settings folder
            var settingsTemplate = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MscrmTools", "XrmToolBox", "Settings", "DataverseToPowerBI", "PBIP_DefaultTemplate");
            
            if (Directory.Exists(settingsTemplate))
            {
                _templatePath = settingsTemplate;
            }
            else
            {
                // 2. AppData Plugins folder
                var appDataPlugins = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MscrmTools", "XrmToolBox", "Plugins", "DataverseToPowerBI", "PBIP_DefaultTemplate");
                
                if (Directory.Exists(appDataPlugins))
                {
                    _templatePath = appDataPlugins;
                }
                else
                {
                    // 3. Plugin DLL folder with Assets subfolder (development)
                    var pluginFolder = Path.GetDirectoryName(GetType().Assembly.Location);
                    var assetsPath = Path.Combine(pluginFolder ?? "", "Assets", "PBIP_DefaultTemplate");
                    if (Directory.Exists(assetsPath))
                    {
                        _templatePath = assetsPath;
                    }
                    else
                    {
                        _templatePath = Path.Combine(pluginFolder ?? "", "PBIP_DefaultTemplate");
                        
                        if (!Directory.Exists(_templatePath))
                        {
                            // 4. Base directory fallback
                            _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "DataverseToPowerBI", "PBIP_DefaultTemplate");
                        }
                    }
                }
            }
            
            // Install template to settings folder on first run
            if (!_modelManager.IsTemplateInstalled() && Directory.Exists(_templatePath))
            {
                try
                {
                    _modelManager.InstallDefaultTemplate(_templatePath);
                }
                catch
                {
                    // Ignore template installation errors
                }
            }
        }
        
        private void PluginControl_Load(object sender, EventArgs e)
        {
            SetStatus("Connect to an environment using XrmToolBox connection manager");
            
            // Set version from assembly file version and normalize to 4 segments
            var fileVersion = System.Diagnostics.FileVersionInfo
                .GetVersionInfo(GetType().Assembly.Location)
                .FileVersion;
            string displayVersion;
            if (Version.TryParse(fileVersion, out var parsedFileVersion))
            {
                var revision = parsedFileVersion.Revision < 0 ? 0 : parsedFileVersion.Revision;
                displayVersion = $"{parsedFileVersion.Major}.{parsedFileVersion.Minor}.{parsedFileVersion.Build}.{revision}";
            }
            else
            {
                var fallbackVersion = GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
                var revision = fallbackVersion.Revision < 0 ? 0 : fallbackVersion.Revision;
                displayVersion = $"{fallbackVersion.Major}.{fallbackVersion.Minor}.{fallbackVersion.Build}.{revision}";
            }
            lblVersion.Text = $"v{displayVersion}";
            var assemblyLocation = GetType().Assembly.Location;
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyLocation);
            _versionToolTip.SetToolTip(
                lblVersion,
                $"Path: {assemblyLocation}{Environment.NewLine}" +
                $"FileVersion: {versionInfo.FileVersion}{Environment.NewLine}" +
                $"ProductVersion: {versionInfo.ProductVersion}");
            UpdateVersionLabelLayout();
            panelStatus.Resize += (s, ev) => UpdateVersionLabelLayout();
            
            // Initialize toolbar icons
            btnRefreshMetadata.Image = RibbonIcons.RefreshIcon;
            btnSelectTables.Image = RibbonIcons.TableIcon;
            btnCalendarTable.Image = RibbonIcons.CalendarIcon;
            btnBuildSemanticModel.Image = RibbonIcons.BuildIcon;
            btnSemanticModel.Image = RibbonIcons.ModelIcon;
            btnChangeWorkingFolder.Image = RibbonIcons.FolderIcon;
            btnSettingsFolder.Image = RibbonIcons.FolderIcon;
            btnPreviewTmdl.Image = RibbonIcons.PreviewIcon;
            
            // Add custom paint for semantic model button border
            btnSemanticModel.Paint += (s, pe) =>
            {
                var btn = (ToolStripButton)s;
                using (var pen = new Pen(Color.FromArgb(50, 100, 200), 1))
                {
                    var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    pe.Graphics.DrawRectangle(pen, rect);
                }
            };
            
            // Disable controls until connected
            btnSelectTables.Enabled = false;
            btnCalendarTable.Enabled = false;
            btnPreviewTmdl.Enabled = false;
            
            // Update semantic model display
            UpdateSemanticModelDisplay();
            
            // Initial column sizing (deferred to ensure controls are sized)
            BeginInvoke(new Action(() =>
            {
                ResizeAttributeColumns();
                ResizeTableColumns();
                ResizeRelationshipColumns();
            }));
            
            // Check if XrmToolBox already has an active connection
            // (UpdateConnection may not be called if the connection was established before the plugin loaded)
            if (Service != null && ConnectionDetail != null)
            {
                // Defer initialization to allow the UI to finish loading
                BeginInvoke(new Action(() =>
                {
                    InitializeFromExistingConnection();
                }));
            }
        }

        private void UpdateVersionLabelLayout()
        {
            if (panelStatus == null || lblVersion == null)
            {
                return;
            }

            var rightPadding = 6;
            var newLeft = panelStatus.ClientSize.Width - lblVersion.Width - rightPadding;
            lblVersion.Left = Math.Max(0, newLeft);
        }
        
        /// <summary>
        /// Initializes the plugin when XrmToolBox already has an active connection.
        /// This handles the case where the user connects first, then opens the plugin.
        /// </summary>
        private void InitializeFromExistingConnection()
        {
            if (_xrmAdapter != null) return; // Already initialized
            
            var detail = ConnectionDetail;
            var environmentUrl = detail.WebApplicationUrl ?? detail.OrganizationServiceUrl;
            _currentEnvironmentUrl = NormalizeUrl(environmentUrl);
            _xrmAdapter = new XrmServiceAdapterImpl(Service, environmentUrl);

            btnRefreshMetadata.Enabled = true;
            SetStatus($"Connected to {detail.OrganizationFriendlyName}");
            
            // Check for first-run experience
            if (_modelManager.IsFirstRun())
            {
                PromptForFirstSemanticModel();
                return;
            }
            
            // Load the most recent model for this environment, or prompt to select
            var recentModel = _modelManager.GetMostRecentModelForEnvironment(_currentEnvironmentUrl ?? "");
            if (recentModel != null)
            {
                LoadSemanticModel(recentModel);
            }
            else
            {
                // No model for this environment - show selector dialog
                ShowSemanticModelSelector();
            }
            
            btnSelectTables.Enabled = true;
            btnCalendarTable.Enabled = _selectedTables.Count > 0;
        }
        
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (newService != null && detail != null)
            {
                var environmentUrl = detail.WebApplicationUrl ?? detail.OrganizationServiceUrl;
                _currentEnvironmentUrl = NormalizeUrl(environmentUrl);
                _xrmAdapter = new XrmServiceAdapterImpl(newService, environmentUrl);

                btnRefreshMetadata.Enabled = true;
                SetStatus($"Connected to {detail.OrganizationFriendlyName}");
                
                // Check for first-run experience
                if (_modelManager.IsFirstRun())
                {
                    PromptForFirstSemanticModel();
                    return;
                }
                
                // Load the most recent model for this environment, or prompt to select
                var recentModel = _modelManager.GetMostRecentModelForEnvironment(_currentEnvironmentUrl ?? "");
                if (recentModel != null)
                {
                    LoadSemanticModel(recentModel);
                }
                else
                {
                    // No model for this environment - show selector dialog
                    ShowSemanticModelSelector();
                }
                
                btnSelectTables.Enabled = true;
                btnCalendarTable.Enabled = _selectedTables.Count > 0;
                btnPreviewTmdl.Enabled = _selectedTables.Count > 0;
            }
            else
            {
                SaveCurrentModel();
                
                _xrmAdapter = null;
                _currentEnvironmentUrl = null;
                btnRefreshMetadata.Enabled = false;
                btnSelectTables.Enabled = false;
                btnCalendarTable.Enabled = false;
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
        
        private void PromptForFirstSemanticModel()
        {
            var result = MessageBox.Show(
                "Welcome to Dataverse Metadata Extractor for Power BI!\n\n" +
                "Let's create your first semantic model.\n\n" +
                "You'll need:\n" +
                "  • A name for your semantic model\n" +
                "  • A working folder location\n\n" +
                "Would you like to continue?",
                "First Time Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                SetStatus("Setup cancelled. Click the Semantic Model button to create one.");
                UpdateSemanticModelDisplay();
                // Still enable Select Tables so user can proceed
                btnSelectTables.Enabled = true;
                return;
            }

            // Show the new semantic model dialog
            var defaultFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DataverseToPowerBI");
            Directory.CreateDirectory(defaultFolder);

            var defaultTemplate = _modelManager.GetInstalledTemplatePath() ?? _templatePath;

            using (var dialog = new NewSemanticModelDialogXrm(defaultFolder, _currentEnvironmentUrl ?? "", defaultTemplate))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        // Clear any existing metadata before creating the first model
                        ClearAllMetadata();
                        
                        var newModel = new SemanticModelConfig
                        {
                            Name = dialog.SemanticModelName,
                            DataverseUrl = _currentEnvironmentUrl ?? "",
                            WorkingFolder = dialog.WorkingFolder,
                            TemplatePath = dialog.TemplatePath,
                            LastUsed = DateTime.Now,
                            CreatedDate = DateTime.Now,
                            PluginSettings = new PluginSettings()  // Explicitly initialize with empty settings
                        };

                        _modelManager.CreateModel(newModel);
                        LoadSemanticModel(newModel);

                        SetStatus($"Semantic model '{newModel.Name}' created successfully.");
                        btnSelectTables.Enabled = true;
                        
                        // Immediately start the table selection workflow
                        BeginInvoke(new Action(() => StartTableSelectionWorkflow()));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error creating semantic model:\n{ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    SetStatus("Setup cancelled. Click the Semantic Model button to create one.");
                    // Still enable Select Tables so user can proceed
                    btnSelectTables.Enabled = true;
                }
            }
            
            UpdateSemanticModelDisplay();
        }
        
        /// <summary>
        /// Starts the table selection workflow - loads solutions and opens the selector
        /// </summary>
        private void StartTableSelectionWorkflow()
        {
            if (_xrmAdapter == null) return;
            
            // Load solutions and show table selector
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solutions...",
                Work = (worker, args) =>
                {
                    args.Result = _xrmAdapter.GetSolutionsSync(Service);
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        // Check if this is a privilege error for reading solutions
                        var errorMessage = args.Error.Message ?? "";
                        if (errorMessage.Contains("prvReadSolution") || 
                            errorMessage.Contains("missing") && errorMessage.Contains("privilege") && errorMessage.Contains("solution"))
                        {
                            // User doesn't have permission to view solutions - gracefully fall back to all tables
                            var result = MessageBox.Show(
                                "You don't have permission to view Solutions in this Dataverse environment.\n\n" +
                                "You can still select tables from the complete list of all tables in the environment.\n" +
                                "Note: This list may contain hundreds of tables.\n\n" +
                                "Would you like to continue and select from all available tables?",
                                "Solution Access Limited",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information);
                            
                            if (result == DialogResult.Yes)
                            {
                                LoadAllTablesAndShowSelector();
                            }
                            return;
                        }
                        
                        MessageBox.Show($"Error: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    var solutions = args.Result as List<DataverseSolution> ?? new List<DataverseSolution>();
                    ShowSolutionAndTableSelector(solutions);
                }
            });
        }
        
        /// <summary>
        /// Loads all tables from Dataverse (fallback when user doesn't have prvReadSolution privilege)
        /// </summary>
        private void LoadAllTablesAndShowSelector()
        {
            if (_xrmAdapter == null) return;
            
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading all tables (this may take a moment)...",
                Work = (worker, args) =>
                {
                    args.Result = _xrmAdapter.GetAllTablesSync(Service);
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show($"Error loading tables: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    var allTables = args.Result as List<TableInfo> ?? new List<TableInfo>();
                    
                    if (allTables.Count == 0)
                    {
                        MessageBox.Show("No tables found in the environment.", "No Tables", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    
                    // Set solution info to indicate using all tables
                    _currentSolutionId = null;
                    _currentSolutionName = $"All Tables ({allTables.Count})";
                    _solutionTables = allTables;
                    
                    // Proceed to fact/dimension selector
                    ShowFactDimensionSelector();
                }
            });
        }
        
        /// <summary>
        /// Clears all metadata for selected tables, attributes, forms, views, and relationships.
        /// Used when starting a fresh semantic model to ensure no previous selections remain.
        /// </summary>
        private void ClearAllMetadata()
        {
            _selectedTables.Clear();
            _tableForms.Clear();
            _tableViews.Clear();
            _tableAttributes.Clear();
            _sortedAttributeCache.Clear();
            _selectedAttributes.Clear();
            _selectedFormIds.Clear();
            _selectedViewIds.Clear();
            _loadingStates.Clear();
            _factTable = null;
            _relationships.Clear();
            _currentSolutionName = null;
            _currentSolutionId = null;
            _solutionTables.Clear();
            
            // Clear UI displays
            listViewSelectedTables.Items.Clear();
            listViewRelationships.Items.Clear();
            listViewAttributes.Items.Clear();
            UpdateTableCount();
            UpdateSemanticModelDisplay();
        }
        
        private void LoadSemanticModel(SemanticModelConfig model)
        {
            _currentModel = model;
            _modelManager.SetCurrentModel(model.Name);
            
            // Load plugin settings from the model
            var settings = model.PluginSettings ?? new PluginSettings();
            
            _currentSolutionName = settings.LastSolutionName;
            _currentSolutionId = settings.LastSolutionId;
            _factTable = settings.FactTable;
            
            // If this is a brand new model with no tables, ensure fact table is null
            if (settings.SelectedTableNames == null || settings.SelectedTableNames.Count == 0)
            {
                _factTable = null;
            }
            
            // Restore selected attributes
            _selectedAttributes.Clear();
            if (settings.SelectedAttributes != null)
            {
                foreach (var kvp in settings.SelectedAttributes)
                    _selectedAttributes[kvp.Key] = new HashSet<string>(kvp.Value);
            }
            
            // Restore form/view selections
            _selectedFormIds = settings.SelectedFormIds ?? new Dictionary<string, string>();
            _selectedViewIds = settings.SelectedViewIds ?? new Dictionary<string, string>();
            _tableStorageModes = settings.TableStorageModes ?? new Dictionary<string, string>();
            
            // Restore display name overrides
            _attributeDisplayNameOverrides.Clear();
            if (settings.AttributeDisplayNameOverrides != null)
            {
                foreach (var kvp in settings.AttributeDisplayNameOverrides)
                    _attributeDisplayNameOverrides[kvp.Key] = new Dictionary<string, string>(kvp.Value);
            }
            
            // Restore relationships
            _relationships.Clear();
            if (settings.Relationships != null)
            {
                foreach (var r in settings.Relationships)
                {
                    _relationships.Add(new ExportRelationship
                    {
                        SourceTable = r.SourceTable ?? "",
                        SourceAttribute = r.SourceAttribute ?? "",
                        TargetTable = r.TargetTable ?? "",
                        IsActive = r.IsActive,
                        IsSnowflake = r.IsSnowflake,
                        AssumeReferentialIntegrity = r.AssumeReferentialIntegrity
                    });
                }
            }
            
            // Restore radio button state
            if (settings.ShowAllAttributes)
                radioShowAll.Checked = true;
            else
                radioShowSelected.Checked = true;
            
            // Restore tables from settings
            if (settings.SelectedTableNames?.Any() == true)
            {
                _isLoading = true;
                
                _selectedTables.Clear();
                foreach (var tableName in settings.SelectedTableNames)
                {
                    var displayInfo = settings.TableDisplayInfo?.ContainsKey(tableName) == true
                        ? settings.TableDisplayInfo[tableName]
                        : null;
                    
                    _selectedTables[tableName] = new TableInfo
                    {
                        LogicalName = tableName,
                        DisplayName = displayInfo?.DisplayName ?? tableName,
                        SchemaName = displayInfo?.SchemaName ?? tableName,
                        PrimaryIdAttribute = displayInfo?.PrimaryIdAttribute,
                        PrimaryNameAttribute = displayInfo?.PrimaryNameAttribute
                    };
                }
                
                RefreshTableListDisplay();
                UpdateTableCount();
                
                // Revalidate metadata in background - preserves user selections while refreshing from Dataverse
                // This handles the case where user closes and reopens the solution
                RevalidateMetadata();
                
                _isLoading = false;
            }
            else
            {
                // No tables yet - enable Select Tables button so user can start
                btnSelectTables.Enabled = true;
            }
            
            UpdateSemanticModelDisplay();
            SetStatus($"Loaded semantic model: {model.Name}");
            
            // Note: Buttons will be enabled after RevalidateMetadata completes
        }
        
        private void SaveCurrentModel()
        {
            if (_currentModel == null) return;
            
            try
            {
                var settings = _currentModel.PluginSettings ?? new PluginSettings();
                
                settings.LastSolutionName = _currentSolutionName ?? "";
                settings.LastSolutionId = _currentSolutionId ?? "";
                settings.FactTable = _factTable ?? "";
                settings.SelectedTableNames = _selectedTables.Keys.ToList();
                settings.ShowAllAttributes = radioShowAll.Checked;
                
                // Save selected attributes
                settings.SelectedAttributes = new Dictionary<string, List<string>>();
                foreach (var kvp in _selectedAttributes)
                    settings.SelectedAttributes[kvp.Key] = kvp.Value.ToList();
                
                settings.SelectedFormIds = _selectedFormIds;
                settings.SelectedViewIds = _selectedViewIds;
                
                // Convert relationships to serialized form
                settings.Relationships = _relationships.Select(r => new SerializedRelationship
                {
                    SourceTable = r.SourceTable,
                    SourceAttribute = r.SourceAttribute,
                    TargetTable = r.TargetTable,
                    IsActive = r.IsActive,
                    IsSnowflake = r.IsSnowflake,
                    AssumeReferentialIntegrity = r.AssumeReferentialIntegrity
                }).ToList();
                
                // Save display name overrides
                settings.AttributeDisplayNameOverrides = new Dictionary<string, Dictionary<string, string>>();
                foreach (var kvp in _attributeDisplayNameOverrides)
                {
                    if (kvp.Value.Count > 0)
                        settings.AttributeDisplayNameOverrides[kvp.Key] = new Dictionary<string, string>(kvp.Value);
                }

                // Save table display info for offline display
                settings.TableDisplayInfo = new Dictionary<string, TableDisplayInfo>();
                foreach (var kvp in _selectedTables)
                {
                    settings.TableDisplayInfo[kvp.Key] = new TableDisplayInfo
                    {
                        DisplayName = kvp.Value.DisplayName,
                        SchemaName = kvp.Value.SchemaName,
                        PrimaryIdAttribute = kvp.Value.PrimaryIdAttribute,
                        PrimaryNameAttribute = kvp.Value.PrimaryNameAttribute
                    };
                }

                // Save per-table storage mode overrides
                settings.TableStorageModes = new Dictionary<string, string>(_tableStorageModes);
                
                _currentModel.PluginSettings = settings;
                _modelManager.SaveModel(_currentModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveCurrentModel failed: {ex.Message}");
            }
        }
        
        private void ShowSemanticModelSelector()
        {
            using (var dialog = new SemanticModelSelectorDialog(_modelManager, _currentEnvironmentUrl ?? ""))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedSemanticModel != null)
                {
                    // Check if this is a newly created model that needs to be initialized
                    var isNewlyCreated = !string.IsNullOrEmpty(dialog.NewlyCreatedConfiguration) &&
                        dialog.SelectedSemanticModel.Name == dialog.NewlyCreatedConfiguration;
                    
                    if (isNewlyCreated)
                    {
                        // A new model was created and selected - clear all existing metadata first
                        ClearAllMetadata();
                        
                        LoadSemanticModel(dialog.SelectedSemanticModel);
                        
                        // Immediately start the table selection workflow for newly created models
                        BeginInvoke(new Action(() => StartTableSelectionWorkflow()));
                    }
                    else
                    {
                        // Loading an existing model
                        // If URL was changed, we need to reload metadata
                        if (dialog.UrlWasChanged)
                        {
                            // Clear metadata since we're now using a different environment's model
                            _tableAttributes.Clear();
                            _tableForms.Clear();
                            _tableViews.Clear();
                        }
                        
                        LoadSemanticModel(dialog.SelectedSemanticModel);
                        
                        // Reload metadata if we have tables
                        if (_selectedTables.Count > 0 && dialog.UrlWasChanged)
                        {
                            LoadMetadataForAllTables();
                        }
                    }
                }
                else if (dialog.ConfigurationsChanged && !string.IsNullOrEmpty(dialog.NewlyCreatedConfiguration))
                {
                    // A new model was created but not selected (user closed dialog) - still load it
                    ClearAllMetadata();
                    
                    // Load the new model
                    var newModel = _modelManager.GetModel(dialog.NewlyCreatedConfiguration);
                    if (newModel != null)
                    {
                        LoadSemanticModel(newModel);
                        
                        // Immediately start the table selection workflow for newly created models
                        BeginInvoke(new Action(() => StartTableSelectionWorkflow()));
                    }
                }
            }
            
            UpdateSemanticModelDisplay();
            UpdateModeColumnVisibility();
            RefreshTableListDisplay();
        }
        
        private void UpdateSemanticModelDisplay()
        {
            if (_currentModel != null)
            {
                btnSemanticModel.Text = $"Semantic Model: {_currentModel.Name}";
                btnSemanticModel.ToolTipText = _currentModel.Name;
            }
            else
            {
                btnSemanticModel.Text = "Semantic Model: (Click to select...)";
                btnSemanticModel.ToolTipText = "Click to select or manage semantic models";
            }
        }
        
        #region Settings
        
        private void SaveSettings()
        {
            SaveCurrentModel();
        }
        
        #endregion
        
        #region Table Selection
        
        private void BtnSelectTables_Click(object sender, EventArgs e)
        {
            StartTableSelectionWorkflow();
        }
        
        private void ShowSolutionAndTableSelector(List<DataverseSolution> solutions)
        {
            // Skip the solution selector dialog - the solution dropdown is on the table selector form
            // If we have a current solution, load its tables; otherwise pass empty list
            if (!string.IsNullOrEmpty(_currentSolutionId))
            {
                var existingSolution = solutions.FirstOrDefault(s => s.SolutionId == _currentSolutionId);
                if (existingSolution != null)
                {
                    // Proceed directly with existing solution
                    _currentSolutionName = existingSolution.FriendlyName;
                    LoadTablesForSolution(existingSolution.SolutionId, existingSolution.FriendlyName, solutions);
                    return;
                }
            }
            
            // No previous solution or it's no longer available - show dialog with empty tables
            // The user will select a solution from the dropdown in the FactDimensionSelectorForm
            _solutionTables = new List<TableInfo>();
            ShowFactDimensionSelector(solutions);
        }
        
        private void LoadTablesForSolution(string? solutionId, string? solutionName, List<DataverseSolution> allSolutions)
        {
            if (_xrmAdapter == null || string.IsNullOrEmpty(solutionId) || string.IsNullOrEmpty(solutionName))
                return;

            var adapter = _xrmAdapter;
            var resolvedSolutionId = solutionId!;
            var resolvedSolutionName = solutionName!;

            // Now load tables for this solution
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading tables from {resolvedSolutionName}...",
                Work = (worker, args) =>
                {
                    args.Result = adapter.GetSolutionTablesSync(Service, resolvedSolutionId);
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show($"Error: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    _solutionTables = args.Result as List<TableInfo> ?? new List<TableInfo>();
                    ShowFactDimensionSelector(allSolutions);
                }
            });
        }
        
        private void ShowFactDimensionSelector(List<DataverseSolution>? allSolutions = null)
        {
            if (_xrmAdapter == null)
                return;

            using (var dialog = new FactDimensionSelectorForm(
                _xrmAdapter,
                Service,
                _currentSolutionName ?? "All Tables",
                _solutionTables,
                _factTable,
                _relationships,
                allSolutions,
                _currentSolutionId))
            {
                // Set up callback for when solution changes in the dialog
                dialog.OnSolutionChangeRequested = (solutionId, solutionName, callback) =>
                {
                    if (string.IsNullOrEmpty(solutionId) || string.IsNullOrEmpty(solutionName))
                    {
                        callback(new List<TableInfo>());
                        return;
                    }

                    var adapter = _xrmAdapter;
                    if (adapter == null)
                    {
                        callback(new List<TableInfo>());
                        return;
                    }

                    // Load tables for the new solution
                    WorkAsync(new WorkAsyncInfo
                    {
                        Message = $"Loading tables from {solutionName}...",
                        Work = (worker, args) =>
                        {
                            args.Result = adapter.GetSolutionTablesSync(Service, solutionId);
                        },
                        PostWorkCallBack = (args) =>
                        {
                            if (args.Error != null)
                            {
                                MessageBox.Show($"Error: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                callback(new List<TableInfo>());
                                return;
                            }
                            
                            var tables = args.Result as List<TableInfo> ?? new List<TableInfo>();
                            _solutionTables = tables;
                            _currentSolutionId = solutionId;
                            _currentSolutionName = solutionName;
                            callback(tables);
                        }
                    });
                };
                
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Update solution if it was changed in the dialog
                    if (!string.IsNullOrEmpty(dialog.SelectedSolutionId))
                    {
                        _currentSolutionId = dialog.SelectedSolutionId;
                        _currentSolutionName = dialog.SelectedSolutionName;
                    }
                    
                    _selectedTables.Clear();
                    _factTable = dialog.SelectedFactTable?.LogicalName;
                    _relationships = dialog.SelectedRelationships;
                    
                    foreach (var table in dialog.AllSelectedTables)
                    {
                        _selectedTables[table.LogicalName] = table;
                        
                        // Initialize attribute selection if not exists
                        if (!_selectedAttributes.ContainsKey(table.LogicalName))
                            _selectedAttributes[table.LogicalName] = new HashSet<string>();
                    }
                    
                    RefreshTableListDisplay();
                    UpdateTableCount();
                    LoadMetadataForAllTables();
                    SaveSettings();
                }
            }
        }
        
        private void LoadMetadataForAllTables()
        {
            if (_xrmAdapter == null || _selectedTables.Count == 0) return;

            var tablesToLoad = _selectedTables.Keys.ToList();
            
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading metadata for {tablesToLoad.Count} tables...",
                Work = (worker, args) =>
                {
                    var attrResults = new Dictionary<string, List<AttributeMetadata>>();
                    var formResults = new Dictionary<string, List<FormMetadata>>();
                    var viewResults = new Dictionary<string, List<ViewMetadata>>();
                    
                    int count = 0;
                    foreach (var tableName in tablesToLoad)
                    {
                        count++;
                        worker.ReportProgress(count * 100 / tablesToLoad.Count, $"Loading {tableName} ({count}/{tablesToLoad.Count})...");
                        
                        try
                        {
                            var attrs = _xrmAdapter.GetAttributesSync(Service, tableName);
                            attrResults[tableName] = attrs;
                            
                            var forms = _xrmAdapter.GetFormsSync(Service, tableName, true);
                            formResults[tableName] = forms;
                            
                            var views = _xrmAdapter.GetViewsSync(Service, tableName, true);
                            viewResults[tableName] = views;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error loading metadata for {tableName}: {ex.Message}");
                            
                            // Even if metadata loading fails, create minimal attribute list with at least the primary key
                            // This ensures relationships can still be generated
                            if (_selectedTables.ContainsKey(tableName))
                            {
                                var table = _selectedTables[tableName];
                                var minimalAttrs = new List<AttributeMetadata>();
                                
                                // Add primary ID attribute
                                var primaryId = table.PrimaryIdAttribute;
                                if (!string.IsNullOrEmpty(primaryId))
                                {
                                    minimalAttrs.Add(new AttributeMetadata
                                    {
                                        LogicalName = primaryId,
                                        DisplayName = primaryId,
                                        SchemaName = primaryId,
                                        AttributeType = "Uniqueidentifier",
                                        IsRequired = true
                                    });
                                }
                                
                                // Add primary name attribute if different
                                var primaryName = table.PrimaryNameAttribute;
                                if (!string.IsNullOrEmpty(primaryName) &&
                                    primaryName != table.PrimaryIdAttribute)
                                {
                                    minimalAttrs.Add(new AttributeMetadata
                                    {
                                        LogicalName = primaryName,
                                        DisplayName = primaryName,
                                        SchemaName = primaryName,
                                        AttributeType = "String"
                                    });
                                }
                                
                                if (minimalAttrs.Any())
                                {
                                    attrResults[tableName] = minimalAttrs;
                                }
                            }
                        }
                    }
                    args.Result = new Tuple<Dictionary<string, List<AttributeMetadata>>, Dictionary<string, List<FormMetadata>>, Dictionary<string, List<ViewMetadata>>>(attrResults, formResults, viewResults);
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState?.ToString() ?? "Loading...");
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show($"Error: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var result = args.Result as Tuple<Dictionary<string, List<AttributeMetadata>>, Dictionary<string, List<FormMetadata>>, Dictionary<string, List<ViewMetadata>>>;
                    if (result != null)
                    {
                        foreach (var kvp in result.Item1)
                            _tableAttributes[kvp.Key] = kvp.Value;
                        foreach (var kvp in result.Item2)
                            _tableForms[kvp.Key] = kvp.Value;
                        foreach (var kvp in result.Item3)
                            _tableViews[kvp.Key] = kvp.Value;
                        
                        // Update TableInfo with actual metadata from loaded attributes
                        foreach (var tableName in _selectedTables.Keys.ToList())
                        {
                            if (_tableAttributes.ContainsKey(tableName))
                            {
                                var table = _selectedTables[tableName];
                                var attrs = _tableAttributes[tableName];
                                
                                // Primary key and name are retrieved from entity metadata.
                                // Only use fallback logic if metadata didn't provide them (which should be rare)
                                if (string.IsNullOrEmpty(table.PrimaryIdAttribute))
                                {
                                    // Fallback: Try standard naming pattern (tablename + "id")
                                    var primaryId = attrs.FirstOrDefault(a => 
                                        a.LogicalName.Equals(tableName + "id", StringComparison.OrdinalIgnoreCase));
                                    
                                    if (primaryId != null)
                                    {
                                        table.PrimaryIdAttribute = primaryId.LogicalName;
                                    }
                                    else
                                    {
                                        // Log warning if we can't determine primary key
                                        DebugLogger.Log($"Warning: Could not determine primary key for table '{tableName}'");
                                    }
                                }
                                
                                // Update primary name if not set
                                if (string.IsNullOrEmpty(table.PrimaryNameAttribute))
                                {
                                    // Fallback: Try common primary name patterns
                                    var primaryName = attrs.FirstOrDefault(a =>
                                        a.LogicalName.Equals(tableName + "name", StringComparison.OrdinalIgnoreCase) ||
                                        a.LogicalName.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                                        a.LogicalName.Equals("fullname", StringComparison.OrdinalIgnoreCase));
                                    
                                    if (primaryName != null)
                                    {
                                        table.PrimaryNameAttribute = primaryName.LogicalName;
                                    }
                                }
                                
                                // Ensure primary key and name attributes are selected (critical for relationships)
                                if (!_selectedAttributes.ContainsKey(tableName))
                                    _selectedAttributes[tableName] = new HashSet<string>();
                                
                                var primaryIdAttr = table.PrimaryIdAttribute;
                                if (!string.IsNullOrEmpty(primaryIdAttr))
                                    _selectedAttributes[tableName].Add(primaryIdAttr);
                                var primaryNameAttr = table.PrimaryNameAttribute;
                                if (!string.IsNullOrEmpty(primaryNameAttr))
                                    _selectedAttributes[tableName].Add(primaryNameAttr);
                            }
                        }
                        
                        // Auto-select form and its fields for each table if not already set
                        foreach (var tableName in _selectedTables.Keys)
                        {
                            var table = _selectedTables[tableName];
                            
                            // Select default form if not already set
                            if (!_selectedFormIds.ContainsKey(tableName) && _tableForms.ContainsKey(tableName) && _tableForms[tableName].Count > 0)
                            {
                                var infoForm = _tableForms[tableName].FirstOrDefault(f => f.Name == "Information");
                                var selectedForm = infoForm ?? _tableForms[tableName][0];
                                _selectedFormIds[tableName] = selectedForm.FormId;
                                
                                // Auto-select form fields as attributes (matching MainForm behavior)
                                // Check if user has selected any attributes beyond the required primary ID/name
                                var requiredAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                if (!string.IsNullOrEmpty(table.PrimaryIdAttribute))
                                    requiredAttrs.Add(table.PrimaryIdAttribute);
                                if (!string.IsNullOrEmpty(table.PrimaryNameAttribute))
                                    requiredAttrs.Add(table.PrimaryNameAttribute);
                                
                                var hasUserSelectedAttributes = _selectedAttributes.ContainsKey(tableName) &&
                                    _selectedAttributes[tableName].Any(a => !requiredAttrs.Contains(a));
                                
                                if (!hasUserSelectedAttributes)
                                {
                                    if (!_selectedAttributes.ContainsKey(tableName))
                                        _selectedAttributes[tableName] = new HashSet<string>();
                                    
                                    var selectedAttrs = _selectedAttributes[tableName];
                                    var attributes = _tableAttributes.ContainsKey(tableName) ? _tableAttributes[tableName] : new List<AttributeMetadata>();
                                    
                                    // Always include primary ID and name attributes
                                    var primaryId = table.PrimaryIdAttribute;
                                    if (!string.IsNullOrEmpty(primaryId))
                                        selectedAttrs.Add(primaryId);
                                    var primaryName = table.PrimaryNameAttribute;
                                    if (!string.IsNullOrEmpty(primaryName))
                                        selectedAttrs.Add(primaryName);
                                    
                                    // Add all fields from the selected form
                                    if (selectedForm.Fields != null)
                                    {
                                        foreach (var field in selectedForm.Fields)
                                        {
                                            if (!string.IsNullOrEmpty(field) &&
                                                attributes.Any(a => a.LogicalName.Equals(field, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                selectedAttrs.Add(field);
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // Select default view if not already set
                            if (!_selectedViewIds.ContainsKey(tableName) && _tableViews.ContainsKey(tableName) && _tableViews[tableName].Count > 0)
                            {
                                var defaultView = _tableViews[tableName].FirstOrDefault(v => v.IsDefault) ?? _tableViews[tableName][0];
                                _selectedViewIds[tableName] = defaultView.ViewId;
                            }
                        }
                    }
                    
                    SetStatus($"Loaded metadata for {tablesToLoad.Count} tables");
                    
                    // Auto-create display name overrides for primary name attributes
                    AutoOverridePrimaryNameAttributes();
                    
                    // Pre-build sorted attribute cache for all tables (default sort: Display Name ascending)
                    PreBuildAttributeSortCache();
                    
                    RefreshTableListDisplay();
                    
                    // Select first table (Fact) and show its attributes
                    if (_selectedTables.Count > 0 && listViewSelectedTables.Items.Count > 0)
                    {
                        listViewSelectedTables.Items[0].Selected = true;
                        listViewSelectedTables.Items[0].Focused = true;
                    }
                    
                    SaveSettings();
                }
            });
        }
        
        /// <summary>
        /// Refresh metadata from Dataverse while preserving user selections.
        /// This follows the exact same pattern as MainForm.RevalidateMetadata().
        /// </summary>
        private void RevalidateMetadata()
        {
            if (_xrmAdapter == null || _selectedTables.Count == 0) return;

            var tablesToLoad = _selectedTables.Keys.ToList();
            
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Revalidating metadata for {tablesToLoad.Count} tables...",
                Work = (worker, args) =>
                {
                    var attrResults = new Dictionary<string, List<AttributeMetadata>>();
                    var formResults = new Dictionary<string, List<FormMetadata>>();
                    var viewResults = new Dictionary<string, List<ViewMetadata>>();
                    
                    int count = 0;
                    foreach (var tableName in tablesToLoad)
                    {
                        count++;
                        worker.ReportProgress(count * 100 / tablesToLoad.Count, $"Revalidating {tableName} ({count}/{tablesToLoad.Count})...");
                        
                        try
                        {
                            var attrs = _xrmAdapter.GetAttributesSync(Service, tableName);
                            attrResults[tableName] = attrs;
                            
                            var forms = _xrmAdapter.GetFormsSync(Service, tableName, true);
                            formResults[tableName] = forms;
                            
                            var views = _xrmAdapter.GetViewsSync(Service, tableName, true);
                            viewResults[tableName] = views;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error revalidating metadata for {tableName}: {ex.Message}");
                        }
                    }
                    args.Result = new Tuple<Dictionary<string, List<AttributeMetadata>>, Dictionary<string, List<FormMetadata>>, Dictionary<string, List<ViewMetadata>>>(attrResults, formResults, viewResults);
                },
                ProgressChanged = (args) =>
                {
                    SetWorkingMessage(args.UserState?.ToString() ?? "Revalidating...");
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show($"Error: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var result = args.Result as Tuple<Dictionary<string, List<AttributeMetadata>>, Dictionary<string, List<FormMetadata>>, Dictionary<string, List<ViewMetadata>>>;
                    if (result != null)
                    {
                        // Update cached metadata
                        foreach (var kvp in result.Item1)
                            _tableAttributes[kvp.Key] = kvp.Value;
                        foreach (var kvp in result.Item2)
                            _tableForms[kvp.Key] = kvp.Value;
                        foreach (var kvp in result.Item3)
                            _tableViews[kvp.Key] = kvp.Value;
                        
                        // Revalidate each table's selections (matching MainForm.RevalidateTableMetadata)
                        foreach (var tableName in _selectedTables.Keys.ToList())
                        {
                            RevalidateTableMetadata(tableName);
                        }
                    }
                    
                    // Re-apply auto-overrides for any new or updated primary name attributes
                    AutoOverridePrimaryNameAttributes();
                    
                    SetStatus($"Revalidated metadata for {tablesToLoad.Count} tables");
                    RefreshTableListDisplay();
                    
                    // Refresh attributes display if a table is selected
                    if (listViewSelectedTables.SelectedItems.Count > 0)
                    {
                        var logicalName = listViewSelectedTables.SelectedItems[0].Name;
                        UpdateAttributesDisplay(logicalName);
                    }
                    else if (_selectedTables.Count > 0 && listViewSelectedTables.Items.Count > 0)
                    {
                        // Auto-select first table (Fact) to populate attributes list
                        listViewSelectedTables.Items[0].Selected = true;
                        listViewSelectedTables.Items[0].Focused = true;
                    }
                    
                    // Enable buttons now that metadata is loaded
                    btnSelectTables.Enabled = true;
                    btnCalendarTable.Enabled = _selectedTables.Count > 0;
                    btnPreviewTmdl.Enabled = _selectedTables.Count > 0;
                }
            });
        }
        
        /// <summary>
        /// Gets the set of lookup attribute logical names on a given table that are
        /// required by currently-configured relationships (i.e. the table is a source
        /// in a relationship and the lookup column connects it to a dimension).
        /// These columns must remain selected as long as the dimension tables are
        /// part of the model.
        /// </summary>
        private HashSet<string> GetRelationshipRequiredColumns(string tableLogicalName)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in _relationships)
            {
                if (rel.SourceTable.Equals(tableLogicalName, StringComparison.OrdinalIgnoreCase) &&
                    _selectedTables.ContainsKey(rel.TargetTable))
                {
                    result.Add(rel.SourceAttribute);
                }
            }
            return result;
        }

        /// <summary>
        /// Revalidate metadata for a single table, preserving user selections.
        /// Matches the logic from MainForm.RevalidateTableMetadata().
        /// </summary>
        private void RevalidateTableMetadata(string tableName)
        {
            if (!_tableAttributes.ContainsKey(tableName)) return;
            
            var currentAttrs = _tableAttributes[tableName];
            var currentAttrNames = new HashSet<string>(currentAttrs.Select(a => a.LogicalName), StringComparer.OrdinalIgnoreCase);
            
            // Get required attributes for this table
            var requiredAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_selectedTables.ContainsKey(tableName))
            {
                var table = _selectedTables[tableName];
                var primaryId = table.PrimaryIdAttribute;
                if (!string.IsNullOrEmpty(primaryId))
                    requiredAttrs.Add(primaryId);
                var primaryName = table.PrimaryNameAttribute;
                if (!string.IsNullOrEmpty(primaryName))
                    requiredAttrs.Add(primaryName);
            }
            // Lock lookup columns required by relationships to dimension tables
            requiredAttrs.UnionWith(GetRelationshipRequiredColumns(tableName));
            if (_selectedAttributes.ContainsKey(tableName))
            {
                // Remove attributes that no longer exist in the table metadata
                var toRemove = _selectedAttributes[tableName]
                    .Where(a => !currentAttrNames.Contains(a))
                    .ToList();
                
                foreach (var attr in toRemove)
                {
                    _selectedAttributes[tableName].Remove(attr);
                }
                
                // Ensure required attributes are always included
                foreach (var required in requiredAttrs)
                {
                    _selectedAttributes[tableName].Add(required);
                }
            }
            
            // Validate selected form still exists
            if (_selectedFormIds.ContainsKey(tableName) && _tableForms.ContainsKey(tableName))
            {
                var selectedFormId = _selectedFormIds[tableName];
                if (!_tableForms[tableName].Any(f => f.FormId == selectedFormId))
                {
                    // Form was deleted - select a new default
                    if (_tableForms[tableName].Count > 0)
                    {
                        var infoForm = _tableForms[tableName].FirstOrDefault(f => f.Name == "Information");
                        _selectedFormIds[tableName] = (infoForm ?? _tableForms[tableName][0]).FormId;
                    }
                    else
                    {
                        _selectedFormIds.Remove(tableName);
                    }
                }
            }
            
            // Validate selected view still exists
            if (_selectedViewIds.ContainsKey(tableName) && _tableViews.ContainsKey(tableName))
            {
                var selectedViewId = _selectedViewIds[tableName];
                if (!_tableViews[tableName].Any(v => v.ViewId == selectedViewId))
                {
                    // View was deleted - select a new default
                    if (_tableViews[tableName].Count > 0)
                    {
                        var defaultView = _tableViews[tableName].FirstOrDefault(v => v.IsDefault) ?? _tableViews[tableName][0];
                        _selectedViewIds[tableName] = defaultView.ViewId;
                    }
                    else
                    {
                        _selectedViewIds.Remove(tableName);
                    }
                }
            }
        }
        
        /// <summary>
        /// Auto-creates display name overrides for primary name attributes to disambiguate
        /// generic names like "Name" across tables (e.g., "Account Name", "Contact Name").
        /// Only runs when UseDisplayNameAliasesInSql is enabled.
        /// Skips if the display name already starts with the table display name.
        /// </summary>
        private void AutoOverridePrimaryNameAttributes()
        {
            if (_currentModel?.UseDisplayNameAliasesInSql != true)
                return;
            
            foreach (var kvp in _selectedTables)
            {
                var tableName = kvp.Key;
                var table = kvp.Value;
                var primaryName = table.PrimaryNameAttribute;
                
                if (string.IsNullOrEmpty(primaryName) || !_tableAttributes.ContainsKey(tableName))
                    continue;
                
                // Skip if an override already exists for this attribute
                if (_attributeDisplayNameOverrides.ContainsKey(tableName) &&
                    _attributeDisplayNameOverrides[tableName].ContainsKey(primaryName))
                    continue;
                
                var attr = _tableAttributes[tableName].FirstOrDefault(a =>
                    a.LogicalName.Equals(primaryName, StringComparison.OrdinalIgnoreCase));
                if (attr == null) continue;
                
                var tableDisplayName = table.DisplayName ?? tableName;
                var attrDisplayName = attr.DisplayName ?? attr.SchemaName ?? attr.LogicalName;
                
                // Skip if display name already starts with the table name
                if (attrDisplayName.StartsWith(tableDisplayName, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Create override: "{TableDisplayName} {OriginalDisplayName}"
                var overrideName = $"{tableDisplayName} {attrDisplayName}";
                
                if (!_attributeDisplayNameOverrides.ContainsKey(tableName))
                    _attributeDisplayNameOverrides[tableName] = new Dictionary<string, string>();
                
                _attributeDisplayNameOverrides[tableName][primaryName] = overrideName;
            }
        }
        
        #endregion
        
        #region UI Display
        
        private void RefreshTableListDisplay()
        {
            UpdateModeColumnVisibility();
            listViewSelectedTables.Items.Clear();
            
            // Sort: Fact first, then by display name
            var sortedTables = _selectedTables.Values
                .OrderByDescending(t => t.LogicalName == _factTable)
                .ThenBy(t => t.DisplayName ?? t.LogicalName)
                .ToList();
            
            foreach (var table in sortedTables)
            {
                AddTableToSelectedList(table);
            }
            
            // Add Date table if configured
            AddDateTableToDisplay();
        }
        
        private void AddTableToSelectedList(TableInfo table)
        {
            var logicalName = table.LogicalName;
            var isFact = logicalName == _factTable;
            
            var isSnowflake = !isFact && _relationships.Any(r => 
                r.TargetTable == logicalName && 
                r.IsSnowflake);
            
            var roleText = isFact ? "⭐ Fact" : (isSnowflake ? "Dim ❄️" : "Dim");
            var formText = GetFormDisplayText(logicalName);
            var viewText = GetViewDisplayText(logicalName);
            var attrCount = _selectedAttributes.ContainsKey(logicalName)
                ? _selectedAttributes[logicalName].Count.ToString()
                : "0";

            var item = new ListViewItem("✏️");
            item.Name = logicalName;
            item.SubItems.Add(roleText);
            item.SubItems.Add(table.DisplayName ?? logicalName);
            item.SubItems.Add(GetTableModeDisplayText(logicalName, isFact));
            item.SubItems.Add(formText);
            item.SubItems.Add(viewText);
            item.SubItems.Add(attrCount);

            if (isFact)
            {
                item.BackColor = Color.LightYellow;
                _boldTableFont ??= new Font(listViewSelectedTables.Font, FontStyle.Bold);
                item.Font = _boldTableFont;
            }

            listViewSelectedTables.Items.Add(item);
        }
        
        private void UpdateSelectedTableRow(string logicalName)
        {
            var item = listViewSelectedTables.Items.Cast<ListViewItem>()
                .FirstOrDefault(i => i.Name == logicalName);

            if (item != null)
            {
                var isFact = logicalName == _factTable;
                var isSnowflake = !isFact && _relationships.Any(r => 
                    r.TargetTable == logicalName && 
                    r.IsSnowflake);
                
                item.SubItems[1].Text = isFact ? "⭐ Fact" : (isSnowflake ? "Dim ❄️" : "Dim");
                item.SubItems[3].Text = GetTableModeDisplayText(logicalName, isFact);
                item.SubItems[4].Text = GetFormDisplayText(logicalName);
                item.SubItems[5].Text = GetViewDisplayText(logicalName);
                item.SubItems[6].Text = _selectedAttributes.ContainsKey(logicalName)
                    ? _selectedAttributes[logicalName].Count.ToString()
                    : "0";

                if (isFact)
                {
                    item.BackColor = Color.LightYellow;
                    _boldTableFont ??= new Font(listViewSelectedTables.Font, FontStyle.Bold);
                    item.Font = _boldTableFont;
                }
                else
                {
                    item.BackColor = Color.White;
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
            
            var selectedFormId = _selectedFormIds.ContainsKey(logicalName)
                ? _selectedFormIds[logicalName]
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
            
            var selectedViewId = _selectedViewIds.ContainsKey(logicalName)
                ? _selectedViewIds[logicalName]
                : (views.FirstOrDefault(v => v.IsDefault) ?? views.First()).ViewId;
            
            var view = views.FirstOrDefault(v => v.ViewId == selectedViewId);
            return view != null ? view.Name : views.First().Name;
        }
        
        private string GetTableModeDisplayText(string logicalName, bool isFact)
        {
            var mode = (_currentModel?.StorageMode ?? "DirectQuery");
            switch (mode)
            {
                case "Import":
                    return "Import";
                case "Dual":
                    return isFact ? "Direct Query" : "Dual";
                case "DualSelect":
                    if (isFact) return "Direct Query";
                    if (_tableStorageModes.TryGetValue(logicalName, out var overrideMode))
                        return overrideMode == "dual" ? "Dual" : "Direct Query";
                    return "Direct Query";
                default: // DirectQuery
                    return "Direct Query";
            }
        }

        private void UpdateModeColumnVisibility()
        {
            colMode.Width = 90;
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
            if (_currentModel?.PluginSettings?.DateTableConfig != null)
            {
                var dateConfig = _currentModel.PluginSettings.DateTableConfig;
                if (!string.IsNullOrEmpty(dateConfig.PrimaryDateTable))
                {
                    var yearRange = $"{dateConfig.StartYear}-{dateConfig.EndYear}";
                    var dateItem = new ListViewItem("📅");  // Calendar icon
                    dateItem.Name = "__DateTable";
                    dateItem.SubItems.Add("Dim");
                    dateItem.SubItems.Add("Date Table");
                    dateItem.SubItems.Add("");  // No form
                    dateItem.SubItems.Add(yearRange);  // Year range in Filter column
                    dateItem.SubItems.Add("365+");  // Approximate row count
                    dateItem.ForeColor = System.Drawing.Color.DarkGreen;
                    
                    // Insert after Fact table (position 1) if there is a fact, otherwise at position 0
                    var factItemIndex = listViewSelectedTables.Items.Cast<ListViewItem>()
                        .Select((item, index) => new { item, index })
                        .FirstOrDefault(x => x.item.SubItems[1].Text.StartsWith("⭐"))?. index;
                    var insertPosition = factItemIndex.HasValue ? factItemIndex.Value + 1 : 0;
                    listViewSelectedTables.Items.Insert(insertPosition, dateItem);
                }
            }
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

            btnCalendarTable.Enabled = count > 0;
            btnPreviewTmdl.Enabled = count > 0;
            UpdateRelationshipsDisplay();
        }
        
        private void UpdateRelationshipsDisplay()
        {
            listViewRelationships.Items.Clear();
            
            // Display relationship to date table first if configured
            if (_currentModel?.PluginSettings?.DateTableConfig != null)
            {
                var dateConfig = _currentModel.PluginSettings.DateTableConfig;
                if (!string.IsNullOrEmpty(dateConfig.PrimaryDateTable))
                {
                    var dateSourceTable = _selectedTables.ContainsKey(dateConfig.PrimaryDateTable)
                        ? _selectedTables[dateConfig.PrimaryDateTable].DisplayName ?? dateConfig.PrimaryDateTable
                        : dateConfig.PrimaryDateTable;
                    
                    var item = new ListViewItem($"{dateSourceTable}.{dateConfig.PrimaryDateField}");
                    item.SubItems.Add("Date Table 📅");
                    item.SubItems.Add("Active (Date)");
                    item.ForeColor = System.Drawing.Color.DarkGreen;
                    
                    listViewRelationships.Items.Add(item);
                }
            }
            
            if (!_relationships.Any())
                return;
            
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
        
        #endregion
        
        #region Attributes
        
        private void ListViewSelectedTables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0)
            {
                listViewAttributes.Items.Clear();
                groupBoxAttributes.Text = "Attributes";
                return;
            }

            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            UpdateAttributesDisplay(logicalName);
        }
        
        private void UpdateAttributesDisplay(string logicalName)
        {
            listViewAttributes.Items.Clear();
            
            var tableDisplay = _selectedTables.ContainsKey(logicalName) 
                ? _selectedTables[logicalName].DisplayName ?? logicalName 
                : logicalName;
            groupBoxAttributes.Text = $"Attributes - {tableDisplay}";
            
            if (!_tableAttributes.ContainsKey(logicalName))
            {
                return;
            }
            
            var attributes = _tableAttributes[logicalName];
            var selected = _selectedAttributes.ContainsKey(logicalName) ? _selectedAttributes[logicalName] : new HashSet<string>();
            var searchText = txtAttrSearch.Text.ToLower();
            var showSelected = radioShowSelected.Checked;
            
            // Use BeginUpdate/EndUpdate for better performance
            listViewAttributes.BeginUpdate();
            
            try
            {
                // Get required attributes (primary ID and name)
                var requiredAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? primaryIdAttr = null;
                string? primaryNameAttr = null;
                if (_selectedTables.ContainsKey(logicalName))
                {
                    var table = _selectedTables[logicalName];
                    var primaryId = table.PrimaryIdAttribute;
                    if (!string.IsNullOrEmpty(primaryId))
                    {
                        requiredAttrs.Add(primaryId);
                        primaryIdAttr = primaryId;
                    }
                    var primaryName = table.PrimaryNameAttribute;
                    if (!string.IsNullOrEmpty(primaryName))
                    {
                        requiredAttrs.Add(primaryName);
                        primaryNameAttr = primaryName;
                    }
                }
                // Lock lookup columns required by relationships to dimension tables
                requiredAttrs.UnionWith(GetRelationshipRequiredColumns(logicalName));
            
            // Get form fields if form is selected
            HashSet<string> formFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_selectedFormIds.ContainsKey(logicalName) && _tableForms.ContainsKey(logicalName))
            {
                var formId = _selectedFormIds[logicalName];
                var form = _tableForms[logicalName].FirstOrDefault(f => f.FormId == formId);
                if (form?.Fields != null)
                    formFields = new HashSet<string>(form.Fields, StringComparer.OrdinalIgnoreCase);
            }
            
            // Use pre-sorted cache if sort parameters match, otherwise rebuild
            var sortedList = GetSortedAttributes(logicalName, attributes, formFields, primaryIdAttr, primaryNameAttr);
            
            // Build effective display name map and detect duplicates
            var overrides = _attributeDisplayNameOverrides.ContainsKey(logicalName)
                ? _attributeDisplayNameOverrides[logicalName]
                : new Dictionary<string, string>();
            var useAliases = _currentModel?.UseDisplayNameAliasesInSql ?? true;
            
            // Count effective display names to detect duplicates
            var displayNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (useAliases)
            {
                foreach (var attr in sortedList)
                {
                    var isSelected2 = selected.Contains(attr.LogicalName);
                    var isRequired2 = requiredAttrs.Contains(attr.LogicalName);
                    if (!isSelected2 && !isRequired2) continue;
                    
                    var dn = overrides.ContainsKey(attr.LogicalName) 
                        ? overrides[attr.LogicalName] 
                        : (attr.DisplayName ?? attr.LogicalName);
                    if (!displayNameCounts.ContainsKey(dn))
                        displayNameCounts[dn] = 0;
                    displayNameCounts[dn]++;
                }
            }
            
            // Pre-build all items into an array, then add in one shot
            var items = new List<ListViewItem>(sortedList.Count);
            _boldAttrFont ??= new Font(listViewAttributes.Font, FontStyle.Bold);
            
            foreach (var attr in sortedList)
            {
                var isSelected = selected.Contains(attr.LogicalName);
                var isRequired = requiredAttrs.Contains(attr.LogicalName);
                var onForm = formFields.Contains(attr.LogicalName);
                
                // Exclude virtual attributes from the list
                if (attr.AttributeType?.Equals("Virtual", StringComparison.OrdinalIgnoreCase) == true && !isRequired)
                    continue;
                
                // Apply filters (required attributes always shown)
                if (showSelected && !isSelected && !isRequired) continue;
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!(attr.DisplayName?.ToLower().Contains(searchText) == true ||
                          attr.LogicalName.ToLower().Contains(searchText)))
                        continue;
                }
                
                // Compute effective display name with override indicator
                var hasOverride = useAliases && overrides.ContainsKey(attr.LogicalName);
                var effectiveDisplayName = hasOverride 
                    ? overrides[attr.LogicalName] 
                    : (attr.DisplayName ?? attr.LogicalName);
                var displayText = hasOverride ? $"{effectiveDisplayName} *" : effectiveDisplayName;
                
                var item = new ListViewItem();
                item.Text = "";
                item.Checked = isSelected || isRequired;  // Required attrs always checked
                item.Name = attr.LogicalName;
                
                // Show lock for required attributes, checkmark for form fields
                var formColumnText = isRequired ? "🔒" : (onForm ? "✓" : "");
                item.SubItems.Add(formColumnText);
                item.SubItems.Add(displayText);
                item.SubItems.Add(attr.LogicalName);
                item.SubItems.Add(attr.AttributeType ?? "");
                item.Tag = attr.LogicalName;
                
                // Apply visual styling (matching MainForm)
                if (isRequired)
                {
                    item.ForeColor = Color.Blue;
                    item.Font = _boldAttrFont;
                }
                else if (isSelected)
                {
                    item.ForeColor = Color.Black;
                }
                else
                {
                    item.ForeColor = Color.Gray;
                }
                
                // Highlight duplicate display names in light red (only for selected/required columns)
                if (useAliases && (isSelected || isRequired) && 
                    displayNameCounts.ContainsKey(effectiveDisplayName) && displayNameCounts[effectiveDisplayName] > 1)
                {
                    item.BackColor = Color.FromArgb(255, 200, 200);
                }
                
                items.Add(item);
            }
            
            _isLoading = true;
            listViewAttributes.Items.AddRange(items.ToArray());
            _isLoading = false;
            }
            finally
            {
                listViewAttributes.EndUpdate();
            }
        }

        /// <summary>
        /// Returns a pre-sorted attribute list for a table, using a cache to avoid re-sorting.
        /// The cache is invalidated when sort parameters change.
        /// </summary>
        private List<AttributeMetadata> GetSortedAttributes(string logicalName,
            List<AttributeMetadata> attributes, HashSet<string> formFields,
            string? primaryIdAttr, string? primaryNameAttr)
        {
            // Check if we can use the cache
            bool cacheValid = _sortedCacheSortColumn == _attributesSortColumn
                           && _sortedCacheSortAscending == _attributesSortAscending
                           && _sortedAttributeCache.ContainsKey(logicalName);

            if (cacheValid)
                return _sortedAttributeCache[logicalName];

            // Rebuild cache for this sort order
            if (_sortedCacheSortColumn != _attributesSortColumn || _sortedCacheSortAscending != _attributesSortAscending)
            {
                _sortedAttributeCache.Clear();
                _sortedCacheSortColumn = _attributesSortColumn;
                _sortedCacheSortAscending = _attributesSortAscending;
            }

            IEnumerable<AttributeMetadata> sortedAttrs;
            switch (_attributesSortColumn)
            {
                case 1: // Form column
                    sortedAttrs = _attributesSortAscending
                        ? attributes.OrderBy(a => formFields.Contains(a.LogicalName) ? "✓" : "")
                        : attributes.OrderByDescending(a => formFields.Contains(a.LogicalName) ? "✓" : "");
                    break;
                case 2: // Display Name column
                    sortedAttrs = _attributesSortAscending
                        ? attributes.OrderBy(a => a.DisplayName ?? a.LogicalName, StringComparer.OrdinalIgnoreCase)
                        : attributes.OrderByDescending(a => a.DisplayName ?? a.LogicalName, StringComparer.OrdinalIgnoreCase);
                    break;
                case 3: // Logical Name column
                    sortedAttrs = _attributesSortAscending
                        ? attributes.OrderBy(a => a.LogicalName, StringComparer.OrdinalIgnoreCase)
                        : attributes.OrderByDescending(a => a.LogicalName, StringComparer.OrdinalIgnoreCase);
                    break;
                case 4: // Type column
                    sortedAttrs = _attributesSortAscending
                        ? attributes.OrderBy(a => a.AttributeType ?? "", StringComparer.OrdinalIgnoreCase)
                        : attributes.OrderByDescending(a => a.AttributeType ?? "", StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    sortedAttrs = attributes.OrderBy(a => a.DisplayName ?? a.LogicalName, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            // Force Id and Name attributes to top
            var attrList = sortedAttrs.ToList();
            var idAttr = attrList.FirstOrDefault(a => a.LogicalName.Equals(primaryIdAttr, StringComparison.OrdinalIgnoreCase));
            var nameAttr = attrList.FirstOrDefault(a => a.LogicalName.Equals(primaryNameAttr, StringComparison.OrdinalIgnoreCase));

            if (idAttr != null) attrList.Remove(idAttr);
            if (nameAttr != null) attrList.Remove(nameAttr);

            var finalList = new List<AttributeMetadata>();
            if (idAttr != null) finalList.Add(idAttr);
            if (nameAttr != null) finalList.Add(nameAttr);
            finalList.AddRange(attrList);

            _sortedAttributeCache[logicalName] = finalList;
            return finalList;
        }

        /// <summary>
        /// Pre-builds the sorted attribute cache for all loaded tables using the current sort order.
        /// Called after metadata loading completes to eliminate sort latency on first table selection.
        /// </summary>
        private void PreBuildAttributeSortCache()
        {
            _sortedAttributeCache.Clear();
            _sortedCacheSortColumn = _attributesSortColumn;
            _sortedCacheSortAscending = _attributesSortAscending;

            foreach (var tableName in _tableAttributes.Keys)
            {
                if (!_selectedTables.ContainsKey(tableName)) continue;
                var table = _selectedTables[tableName];
                var attributes = _tableAttributes[tableName];

                HashSet<string> formFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_selectedFormIds.ContainsKey(tableName) && _tableForms.ContainsKey(tableName))
                {
                    var formId = _selectedFormIds[tableName];
                    var form = _tableForms[tableName].FirstOrDefault(f => f.FormId == formId);
                    if (form?.Fields != null)
                        formFields = new HashSet<string>(form.Fields, StringComparer.OrdinalIgnoreCase);
                }

                GetSortedAttributes(tableName, attributes, formFields,
                    table.PrimaryIdAttribute, table.PrimaryNameAttribute);
            }
        }
        
        private void ListViewAttributes_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_isLoading) return;
            if (listViewSelectedTables.SelectedItems.Count == 0) return;
            
            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            var attrName = e.Item.Tag as string;
            if (attrName == null) return;
            
            // Check if this is a required attribute (cannot be unchecked)
            if (_selectedTables.ContainsKey(logicalName))
            {
                var table = _selectedTables[logicalName];
                var relationshipCols = GetRelationshipRequiredColumns(logicalName);
                if (attrName.Equals(table.PrimaryIdAttribute, StringComparison.OrdinalIgnoreCase) ||
                    attrName.Equals(table.PrimaryNameAttribute, StringComparison.OrdinalIgnoreCase) ||
                    relationshipCols.Contains(attrName))
                {
                    // Re-check if user tried to uncheck
                    if (!e.Item.Checked)
                    {
                        _isLoading = true;
                        e.Item.Checked = true;
                        _isLoading = false;
                    }
                    return;
                }
            }
            
            if (!_selectedAttributes.ContainsKey(logicalName))
                _selectedAttributes[logicalName] = new HashSet<string>();
            
            if (e.Item.Checked)
                _selectedAttributes[logicalName].Add(attrName);
            else
                _selectedAttributes[logicalName].Remove(attrName);
            
            UpdateSelectedTableRow(logicalName);
            SaveSettings();
        }
        
        private void TxtAttrSearch_TextChanged(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;
            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            UpdateAttributesDisplay(logicalName);
        }
        
        private void RadioShowMode_CheckedChanged(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;
            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            
            // Optimize display update to reduce lag
            listViewAttributes.SuspendLayout();
            try
            {
                UpdateAttributesDisplay(logicalName);
            }
            finally
            {
                listViewAttributes.ResumeLayout();
            }
            SaveSettings();
        }
        
        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            _isLoading = true;
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                item.Checked = true;
            }
            _isLoading = false;
            
            // Update state
            if (listViewSelectedTables.SelectedItems.Count > 0)
            {
                var logicalName = listViewSelectedTables.SelectedItems[0].Name;
                if (!_selectedAttributes.ContainsKey(logicalName))
                    _selectedAttributes[logicalName] = new HashSet<string>();
                
                foreach (ListViewItem item in listViewAttributes.Items)
                {
                    var attrName = item.Tag as string;
                    if (attrName != null)
                        _selectedAttributes[logicalName].Add(attrName);
                }
                
                UpdateSelectedTableRow(logicalName);
                SaveSettings();
            }
        }
        
        private void BtnDeselectAll_Click(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;
            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            
            // Get required attributes (ID, display name, and relationship lookup columns)
            var requiredAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_selectedTables.ContainsKey(logicalName))
            {
                var table = _selectedTables[logicalName];
                if (!string.IsNullOrEmpty(table.PrimaryIdAttribute))
                    requiredAttrs.Add(table.PrimaryIdAttribute);
                if (!string.IsNullOrEmpty(table.PrimaryNameAttribute))
                    requiredAttrs.Add(table.PrimaryNameAttribute);
            }
            // Lock lookup columns required by relationships to dimension tables
            requiredAttrs.UnionWith(GetRelationshipRequiredColumns(logicalName));
            
            _isLoading = true;
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                var attrName = item.Tag as string;
                // Uncheck only non-required attributes
                if (attrName != null && !requiredAttrs.Contains(attrName))
                {
                    item.Checked = false;
                }
            }
            _isLoading = false;
            
            // Update state - clear all but keep required attributes
            if (!_selectedAttributes.ContainsKey(logicalName))
                _selectedAttributes[logicalName] = new HashSet<string>();
            
            _selectedAttributes[logicalName].Clear();
            foreach (var req in requiredAttrs)
            {
                _selectedAttributes[logicalName].Add(req);
            }
            
            UpdateSelectedTableRow(logicalName);
            SaveSettings();
        }
        
        private void BtnSelectFromForm_Click(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;
            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            
            if (!_selectedFormIds.ContainsKey(logicalName) || !_tableForms.ContainsKey(logicalName))
            {
                MessageBox.Show("Please select a form first by double-clicking the table row.", "No Form Selected", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var formId = _selectedFormIds[logicalName];
            var form = _tableForms[logicalName].FirstOrDefault(f => f.FormId == formId);
            if (form?.Fields == null || form.Fields.Count == 0)
            {
                MessageBox.Show("The selected form has no fields.", "No Fields", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var formFields = new HashSet<string>(form.Fields, StringComparer.OrdinalIgnoreCase);
            
            _isLoading = true;
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                var attrName = item.Tag as string;
                if (attrName != null && formFields.Contains(attrName))
                    item.Checked = true;
            }
            _isLoading = false;
            
            // Update state
            if (!_selectedAttributes.ContainsKey(logicalName))
                _selectedAttributes[logicalName] = new HashSet<string>();
            
            foreach (var field in formFields)
            {
                _selectedAttributes[logicalName].Add(field);
            }
            
            UpdateSelectedTableRow(logicalName);
            SaveSettings();
        }
        
        private void ListViewAttributes_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Skip column 0 (checkbox column)
            if (e.Column == 0) return;
            
            // Toggle sort direction if clicking the same column
            if (e.Column == _attributesSortColumn)
            {
                _attributesSortAscending = !_attributesSortAscending;
            }
            else
            {
                _attributesSortColumn = e.Column;
                _attributesSortAscending = true;
            }
            
            // Re-display with new sort order
            if (listViewSelectedTables.SelectedItems.Count > 0)
            {
                var logicalName = listViewSelectedTables.SelectedItems[0].Name;
                UpdateAttributesDisplay(logicalName);
            }
        }
        
        private void ListViewAttributes_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;
            if (!(_currentModel?.UseDisplayNameAliasesInSql ?? true)) return;
            
            var hit = listViewAttributes.HitTest(e.Location);
            if (hit.Item == null || hit.SubItem == null) return;
            
            // Only allow editing the Display Name column (index 2)
            var subItemIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (subItemIndex != 2) return;
            
            var attrLogicalName = hit.Item.Tag as string;
            if (string.IsNullOrEmpty(attrLogicalName)) return;
            
            var tableName = listViewSelectedTables.SelectedItems[0].Name;
            
            // Get current effective display name (strip the * indicator if present)
            var currentText = hit.SubItem.Text;
            if (currentText.EndsWith(" *"))
                currentText = currentText.Substring(0, currentText.Length - 2);
            
            // Create an inline TextBox overlay for editing
            var bounds = hit.SubItem.Bounds;
            var editBox = new TextBox
            {
                Text = currentText,
                Bounds = bounds,
                Font = listViewAttributes.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            editBox.SelectAll();
            
            void CommitEdit()
            {
                var newName = editBox.Text.Trim();
                if (editBox.Parent == null) return; // Already removed
                listViewAttributes.Controls.Remove(editBox);
                editBox.Dispose();
                
                if (string.IsNullOrEmpty(newName)) return;
                
                // Get the original display name for this attribute
                var originalDisplayName = "";
                if (_tableAttributes.ContainsKey(tableName))
                {
                    var attr = _tableAttributes[tableName].FirstOrDefault(a => 
                        a.LogicalName.Equals(attrLogicalName, StringComparison.OrdinalIgnoreCase));
                    if (attr != null) originalDisplayName = attr.DisplayName ?? attr.LogicalName;
                }
                
                if (!_attributeDisplayNameOverrides.ContainsKey(tableName))
                    _attributeDisplayNameOverrides[tableName] = new Dictionary<string, string>();
                
                // If user set it back to the original name, remove the override
                if (newName.Equals(originalDisplayName, StringComparison.OrdinalIgnoreCase))
                    _attributeDisplayNameOverrides[tableName].Remove(attrLogicalName);
                else
                    _attributeDisplayNameOverrides[tableName][attrLogicalName] = newName;
                
                SaveSettings();
                UpdateAttributesDisplay(tableName);
            }
            
            void CancelEdit()
            {
                if (editBox.Parent == null) return;
                listViewAttributes.Controls.Remove(editBox);
                editBox.Dispose();
            }
            
            editBox.KeyDown += (s, ke) =>
            {
                if (ke.KeyCode == Keys.Enter) { ke.SuppressKeyPress = true; CommitEdit(); }
                else if (ke.KeyCode == Keys.Escape) { ke.SuppressKeyPress = true; CancelEdit(); }
            };
            editBox.LostFocus += (s, _) => CommitEdit();
            
            listViewAttributes.Controls.Add(editBox);
            editBox.Focus();
        }
        
        private void ListViewRelationships_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Toggle sort direction if clicking the same column
            if (e.Column == _relationshipsSortColumn)
            {
                _relationshipsSortAscending = !_relationshipsSortAscending;
            }
            else
            {
                _relationshipsSortColumn = e.Column;
                _relationshipsSortAscending = true;
            }
            
            // Sort the list
            listViewRelationships.ListViewItemSorter = new ListViewItemComparer(e.Column, _relationshipsSortAscending);
            listViewRelationships.Sort();
        }
        
        private void ListViewAttributes_Resize(object sender, EventArgs e)
        {
            ResizeAttributeColumns();
        }
        
        private void ResizeAttributeColumns()
        {
            if (listViewAttributes.Width <= 0) return;
            
            // Fixed-width columns: Sel (40), Form (50), Type (140)
            const int selWidth = 40;
            const int formWidth = 50;
            const int typeWidth = 140;
            const int scrollBarWidth = 20;
            
            var availableWidth = listViewAttributes.Width - selWidth - formWidth - typeWidth - scrollBarWidth;
            if (availableWidth <= 0) return;
            
            // Distribute remaining width: Display Name (50%), Logical Name (50%)
            var displayWidth = (int)(availableWidth * 0.5);
            var logicalWidth = availableWidth - displayWidth;
            
            listViewAttributes.BeginUpdate();
            try
            {
                colAttrSelected.Width = selWidth;
                colAttrOnForm.Width = formWidth;
                colAttrDisplay.Width = displayWidth;
                colAttrLogical.Width = logicalWidth;
                colAttrType.Width = typeWidth;
            }
            finally
            {
                listViewAttributes.EndUpdate();
            }
        }
        
        private void ListViewSelectedTables_Resize(object sender, EventArgs e)
        {
            ResizeTableColumns();
        }
        
        private void ResizeTableColumns()
        {
            if (listViewSelectedTables.Width <= 0) return;
            
            // Fixed-width columns
            const int editWidth = 30;
            const int roleWidth = 55;
            const int attrsWidth = 30;
            const int scrollBarWidth = 20;
            
            var modeWidth = colMode.Width; // 0 when hidden, 90 when visible
            
            var availableWidth = listViewSelectedTables.Width - editWidth - roleWidth - modeWidth - attrsWidth - scrollBarWidth;
            if (availableWidth <= 0) return;
            
            // Distribute remaining: Table (30%), Form (30%), Filter (40%)
            var tableWidth = (int)(availableWidth * 0.30);
            var formWidth = (int)(availableWidth * 0.30);
            var filterWidth = availableWidth - tableWidth - formWidth;
            
            listViewSelectedTables.BeginUpdate();
            try
            {
                colEdit.Width = editWidth;
                colRole.Width = roleWidth;
                colTable.Width = tableWidth;
                colMode.Width = modeWidth;
                colForm.Width = formWidth;
                colView.Width = filterWidth;
                colAttrs.Width = attrsWidth;
            }
            finally
            {
                listViewSelectedTables.EndUpdate();
            }
        }
        
        private void ListViewRelationships_Resize(object sender, EventArgs e)
        {
            ResizeRelationshipColumns();
        }
        
        private void ResizeRelationshipColumns()
        {
            if (listViewRelationships.Width <= 0) return;
            
            const int typeWidth = 120;
            const int scrollBarWidth = 20;
            
            var availableWidth = listViewRelationships.Width - typeWidth - scrollBarWidth;
            if (availableWidth <= 0) return;
            
            // Distribute remaining: From (50%), To (50%)
            var fromWidth = availableWidth / 2;
            var toWidth = availableWidth - fromWidth;
            
            listViewRelationships.BeginUpdate();
            try
            {
                colRelFrom.Width = fromWidth;
                colRelTo.Width = toWidth;
                colRelType.Width = typeWidth;
            }
            finally
            {
                listViewRelationships.EndUpdate();
            }
        }
        
        #endregion
        
        #region Form/View Selection
        
        private void ListViewSelectedTables_Click(object sender, EventArgs e)
        {
            var info = listViewSelectedTables.HitTest(listViewSelectedTables.PointToClient(Cursor.Position));
            if (info.Item != null && info.SubItem != null)
            {
                var colIndex = info.Item.SubItems.IndexOf(info.SubItem);
                
                // Check if click was on the Edit column (first column, index 0)
                if (colIndex == 0)
                {
                    var logicalName = info.Item.Name;
                    ShowFormViewSelector(logicalName);
                }
                // Check if click was on the Mode column (index 3) — editable for non-Import modes
                else if (colIndex == 3)
                {
                    var storageMode = _currentModel?.StorageMode ?? "DirectQuery";
                    if (storageMode == "Import") return; // Import mode is not editable

                    var logicalName = info.Item.Name;
                    var isFact = logicalName == _factTable;
                    if (!isFact) // Fact table is always Direct Query
                    {
                        // Determine the current effective mode for this table
                        string currentMode;
                        if (storageMode == "DualSelect")
                        {
                            currentMode = _tableStorageModes.TryGetValue(logicalName, out var m) ? m : "directQuery";
                        }
                        else if (storageMode == "Dual")
                        {
                            currentMode = "dual"; // All dims are dual in this mode
                        }
                        else
                        {
                            currentMode = "directQuery"; // All dims are DQ in DirectQuery mode
                        }

                        var newMode = currentMode == "dual" ? "directQuery" : "dual";

                        // If not already in DualSelect, switch to it and seed overrides
                        if (storageMode != "DualSelect")
                        {
                            SwitchToDualSelect(storageMode);
                        }

                        _tableStorageModes[logicalName] = newMode;
                        RefreshTableListDisplay();
                        SaveSettings();
                    }
                }
            }
        }
        
        private void ListViewSelectedTables_DoubleClick(object sender, EventArgs e)
        {
            if (listViewSelectedTables.SelectedItems.Count == 0) return;
            var logicalName = listViewSelectedTables.SelectedItems[0].Name;
            ShowFormViewSelector(logicalName);
        }

        /// <summary>
        /// Switches the model from DirectQuery or Dual to DualSelect, seeding per-table overrides
        /// based on the previous mode, and alerting the user.
        /// </summary>
        private void SwitchToDualSelect(string previousMode)
        {
            // Seed per-table overrides from the previous mode
            _tableStorageModes.Clear();
            foreach (var tableName in _selectedTables.Keys)
            {
                if (tableName == _factTable) continue; // Fact is always directQuery
                _tableStorageModes[tableName] = previousMode == "Dual" ? "dual" : "directQuery";
            }

            _currentModel!.StorageMode = "DualSelect";
            _modelManager.SaveModel(_currentModel);

            var fromLabel = previousMode == "Dual" ? "Dual - All Dimensions" : "Direct Query";
            MessageBox.Show(
                $"Storage mode has been changed from \"{fromLabel}\" to \"Dual - Select Tables\" " +
                "to allow per-table mode selection.\n\n" +
                "You can change this back in the Semantic Model Manager.",
                "Storage Mode Changed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ShowFormViewSelector(string logicalName)
        {
            if (!_tableForms.ContainsKey(logicalName) || !_tableViews.ContainsKey(logicalName))
            {
                MessageBox.Show("Metadata is still loading. Please wait.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var currentFormId = _selectedFormIds.TryGetValue(logicalName, out var formId) ? formId : null;
            var currentViewId = _selectedViewIds.TryGetValue(logicalName, out var viewId) ? viewId : null;
            using (var dialog = new FormViewSelectorForm(
                logicalName,
                _tableForms[logicalName],
                _tableViews[logicalName],
                currentFormId,
                currentViewId))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Check if form changed
                    var previousFormId = currentFormId;
                    var selectedFormId = dialog.SelectedFormId;
                    bool formChanged = selectedFormId != previousFormId;
                    
                    if (!string.IsNullOrEmpty(selectedFormId))
                        _selectedFormIds[logicalName] = selectedFormId;
                    var selectedViewId = dialog.SelectedViewId;
                    if (!string.IsNullOrEmpty(selectedViewId))
                        _selectedViewIds[logicalName] = selectedViewId;
                    
                    // If form changed, clear and re-select form fields
                    if (formChanged && !string.IsNullOrEmpty(selectedFormId))
                    {
                        var form = _tableForms[logicalName].FirstOrDefault(f => f.FormId == selectedFormId);
                        if (form != null)
                        {
                            // Clear current selections
                            if (!_selectedAttributes.ContainsKey(logicalName))
                                _selectedAttributes[logicalName] = new HashSet<string>();
                            
                            var selectedAttrs = _selectedAttributes[logicalName];
                            selectedAttrs.Clear();
                            
                            var table = _selectedTables[logicalName];
                            var attributes = _tableAttributes.ContainsKey(logicalName) ? _tableAttributes[logicalName] : new List<AttributeMetadata>();
                            
                            // Always include primary ID and name attributes
                            var primaryId = table.PrimaryIdAttribute;
                            if (!string.IsNullOrEmpty(primaryId))
                                selectedAttrs.Add(primaryId);
                            var primaryName = table.PrimaryNameAttribute;
                            if (!string.IsNullOrEmpty(primaryName))
                                selectedAttrs.Add(primaryName);
                            
                            // Add all fields from the newly selected form
                            if (form.Fields != null)
                            {
                                foreach (var field in form.Fields)
                                {
                                    if (!string.IsNullOrEmpty(field) &&
                                        attributes.Any(a => a.LogicalName.Equals(field, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        selectedAttrs.Add(field);
                                    }
                                }
                            }
                        }
                    }
                    
                    UpdateSelectedTableRow(logicalName);
                    UpdateAttributesDisplay(logicalName);
                    SaveSettings();
                }
            }
        }
        
        private void ListViewSelectedTables_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Skip column 0 (edit icon column)
            if (e.Column == 0) return;
            
            // Toggle sort direction if clicking the same column
            if (e.Column == _selectedTablesSortColumn)
            {
                _selectedTablesSortAscending = !_selectedTablesSortAscending;
            }
            else
            {
                _selectedTablesSortColumn = e.Column;
                _selectedTablesSortAscending = true;
            }
            
            // Sort the list
            listViewSelectedTables.ListViewItemSorter = new ListViewItemComparer(e.Column, _selectedTablesSortAscending);
            listViewSelectedTables.Sort();
        }
        
        #endregion
        
        #region Toolbar Actions
        
        private void BtnCalendarTable_Click(object sender, EventArgs e)
        {
            if (_selectedTables.Count == 0)
            {
                MessageBox.Show("Please select tables first.", "No Tables", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Get existing config if any
            var existingConfig = _currentModel?.PluginSettings?.DateTableConfig;

            using (var dialog = new CalendarTableDialog(
                _selectedTables,
                _tableAttributes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToDictionary(
                        a => a.LogicalName,
                        a => new AttributeDisplayInfo
                        {
                            LogicalName = a.LogicalName,
                            DisplayName = a.DisplayName,
                            SchemaName = a.SchemaName,
                            AttributeType = a.AttributeType,
                            IsRequired = a.IsRequired,
                            Targets = a.Targets,
                            VirtualAttributeName = a.VirtualAttributeName
                        })),
                _selectedAttributes,
                _factTable,
                existingConfig))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Config != null)
                {
                    // Save the configuration
                    if (_currentModel != null)
                    {
                        if (_currentModel.PluginSettings == null)
                            _currentModel.PluginSettings = new PluginSettings();

                        _currentModel.PluginSettings.DateTableConfig = dialog.Config;
                        _modelManager.SaveModel(_currentModel);

                        // Update UI to show the Date table and relationship
                        AddDateTableToDisplay();
                        UpdateRelationshipsDisplay();

                        MessageBox.Show(
                            $"Calendar table configured:\n\n" +
                            $"Primary Date Field: {dialog.Config.PrimaryDateTable}.{dialog.Config.PrimaryDateField}\n" +
                            $"Time Zone: {dialog.Config.TimeZoneId} (UTC {dialog.Config.UtcOffsetHours:+0.0;-0.0})\n" +
                            $"Year Range: {dialog.Config.StartYear}-{dialog.Config.EndYear}\n" +
                            $"Additional Fields: {dialog.Config.WrappedFields.Count}",
                            "Calendar Table Configured",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
        }
        
        private void BtnBuildSemanticModel_Click(object sender, EventArgs e)
        {
            if (_selectedTables.Count == 0)
            {
                MessageBox.Show("Please select tables first.", "No Tables", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Check if all selected tables have their metadata loaded
            var tablesWithoutMetadata = _selectedTables.Keys
                .Where(tableName => !_tableAttributes.ContainsKey(tableName))
                .ToList();
            
            if (tablesWithoutMetadata.Any())
            {
                var tableNames = string.Join(", ", tablesWithoutMetadata.Select(t => 
                    _selectedTables.ContainsKey(t) ? _selectedTables[t].DisplayName ?? t : t));
                
                var result = MessageBox.Show(
                    $"Metadata has not been loaded for the following tables:\n{tableNames}\n\n" +
                    "This usually happens when tables are selected from outside your current solution. " +
                    "Would you like to load their metadata now?\n\n" +
                    "(Click Yes to load metadata, No to cancel)",
                    "Metadata Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                
                if (result == DialogResult.Yes)
                {
                    // Load metadata for missing tables
                    LoadMetadataForAllTables();
                    return; // Exit - user can click Build again after metadata loads
                }
                else
                {
                    return; // Cancel build
                }
            }
            
            // Validate no duplicate display name aliases
            if (HasDuplicateDisplayNames()) return;
            
            // Use current model's working folder if available
            string? outputPath = null;
            if (_currentModel != null && !string.IsNullOrEmpty(_currentModel.WorkingFolder))
            {
                outputPath = _currentModel.WorkingFolder;
                
                // Migrate old paths that contain "\Reports\" subfolder (legacy structure)
                if (outputPath.Contains("\\Reports\\"))
                {
                    var oldPath = outputPath;
                    outputPath = outputPath.Replace("\\Reports\\", "\\");
                    _currentModel.WorkingFolder = outputPath;
                    _modelManager.SaveModel(_currentModel);
                    DebugLogger.Log($"Migrated WorkingFolder from '{oldPath}' to '{outputPath}'");
                }
                
                // Show the actual path where PBIP will be built
                var modelName = _currentModel?.Name ?? _currentSolutionName ?? "MySemanticModel";
                var environmentName = ExtractEnvironmentName(_currentEnvironmentUrl ?? "");
                var pbipPath = Path.Combine(outputPath, environmentName, modelName);
                
                var result = MessageBox.Show(
                    $"Build semantic model to:\n{pbipPath}\n\nClick No to choose a different folder.",
                    "Confirm Output Folder",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Cancel)
                    return;
                
                if (result == DialogResult.No)
                    outputPath = null;
            }
            
            // Get output folder if not set
            if (string.IsNullOrEmpty(outputPath))
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select output folder for the semantic model";
                    folderDialog.ShowNewFolderButton = true;
                    
                    if (folderDialog.ShowDialog() != DialogResult.OK)
                        return;
                    
                    outputPath = folderDialog.SelectedPath;
                    
                    // Update the model's working folder
                    if (_currentModel != null)
                    {
                        _currentModel.WorkingFolder = outputPath;
                        _modelManager.SaveModel(_currentModel);
                    }
                }
            }
            
            BuildSemanticModel(outputPath);
        }
        
        /// <summary>
        /// Checks for duplicate effective display names within each table when aliasing is enabled.
        /// Returns true if duplicates were found and the user should be blocked from proceeding.
        /// </summary>
        private bool HasDuplicateDisplayNames()
        {
            if (!(_currentModel?.UseDisplayNameAliasesInSql ?? true)) return false;
            
            var conflicts = new List<string>();
            
            foreach (var tableName in _selectedTables.Keys)
            {
                if (!_tableAttributes.ContainsKey(tableName)) continue;
                var selected = _selectedAttributes.ContainsKey(tableName) ? _selectedAttributes[tableName] : new HashSet<string>();
                
                // Include required attributes
                var requiredAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var table = _selectedTables[tableName];
                if (!string.IsNullOrEmpty(table.PrimaryIdAttribute)) requiredAttrs.Add(table.PrimaryIdAttribute);
                if (!string.IsNullOrEmpty(table.PrimaryNameAttribute)) requiredAttrs.Add(table.PrimaryNameAttribute);
                requiredAttrs.UnionWith(GetRelationshipRequiredColumns(tableName));
                
                var overrides = _attributeDisplayNameOverrides.ContainsKey(tableName)
                    ? _attributeDisplayNameOverrides[tableName]
                    : new Dictionary<string, string>();
                    
                var nameToAttrs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var attr in _tableAttributes[tableName])
                {
                    if (!selected.Contains(attr.LogicalName) && !requiredAttrs.Contains(attr.LogicalName)) continue;
                    
                    var effectiveName = overrides.ContainsKey(attr.LogicalName)
                        ? overrides[attr.LogicalName]
                        : (attr.DisplayName ?? attr.LogicalName);
                    
                    if (!nameToAttrs.ContainsKey(effectiveName))
                        nameToAttrs[effectiveName] = new List<string>();
                    nameToAttrs[effectiveName].Add(attr.LogicalName);
                }
                
                foreach (var kvp in nameToAttrs.Where(n => n.Value.Count > 1))
                {
                    var tableDisplay = table.DisplayName ?? tableName;
                    conflicts.Add($"  • {tableDisplay}: \"{kvp.Key}\" → {string.Join(", ", kvp.Value)}");
                }
            }
            
            if (conflicts.Count == 0) return false;
            
            MessageBox.Show(
                "Duplicate display name aliases found. Each column in a table must have a unique display name.\n\n" +
                "Conflicts:\n" + string.Join("\n", conflicts) + "\n\n" +
                "Double-click the Display Name column in the attributes list to rename conflicting columns.",
                "Duplicate Column Names",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return true;
        }
        
        /// <summary>
        /// Prepares the export data structures needed by the SemanticModelBuilder.
        /// Shared between Build and Preview TMDL flows.
        /// </summary>
        private (List<ExportTable> tables, List<ExportRelationship> relationships,
                 Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo) PrepareExportData()
        {
            // Build export tables with full metadata
            var exportTables = _selectedTables.Values.Select(t =>
            {
                var table = new ExportTable
                {
                    LogicalName = t.LogicalName,
                    DisplayName = t.DisplayName ?? t.LogicalName,
                    SchemaName = t.SchemaName ?? t.LogicalName,
                    PrimaryIdAttribute = t.PrimaryIdAttribute ?? "",
                    PrimaryNameAttribute = t.PrimaryNameAttribute ?? "",
                    ObjectTypeCode = t.ObjectTypeCode,
                    Role = (t.LogicalName == _factTable) ? "Fact" : "Dimension",
                    Attributes = new List<AttributeMetadata>()
                };
                
                // Add selected attributes
                if (_selectedAttributes.ContainsKey(t.LogicalName) && _tableAttributes.ContainsKey(t.LogicalName))
                {
                    var selectedAttrNames = _selectedAttributes[t.LogicalName];
                    table.Attributes = _tableAttributes[t.LogicalName]
                        .Where(a => selectedAttrNames.Contains(a.LogicalName))
                        .ToList();
                }
                
                // Check if table has statecode attribute (for WHERE clause generation)
                if (_tableAttributes.ContainsKey(t.LogicalName))
                {
                    table.HasStateCode = _tableAttributes[t.LogicalName]
                        .Any(a => a.LogicalName.Equals("statecode", StringComparison.OrdinalIgnoreCase));
                }

                // Add selected view (with FetchXML)
                if (_selectedViewIds.ContainsKey(t.LogicalName) && _tableViews.ContainsKey(t.LogicalName))
                {
                    var viewId = _selectedViewIds[t.LogicalName];
                    var view = _tableViews[t.LogicalName].FirstOrDefault(v => v.ViewId == viewId);
                    if (view != null)
                    {
                        table.View = new ExportView
                        {
                            ViewId = view.ViewId,
                            ViewName = view.Name,
                            FetchXml = view.FetchXml
                        };
                    }
                }
                
                return table;
            }).ToList();
            
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
            
            // Get attribute display info
            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>();
            foreach (var kvp in _tableAttributes)
            {
                var tableDict = new Dictionary<string, AttributeDisplayInfo>();
                foreach (var attr in kvp.Value)
                {
                    var displayInfo = new AttributeDisplayInfo
                    {
                        LogicalName = attr.LogicalName,
                        DisplayName = attr.DisplayName,
                        SchemaName = attr.SchemaName,
                        AttributeType = attr.AttributeType,
                        IsRequired = attr.IsRequired,
                        Targets = attr.Targets,
                        VirtualAttributeName = attr.VirtualAttributeName,
                        IsGlobal = attr.IsGlobal,
                        OptionSetName = attr.OptionSetName
                    };
                    
                    // Populate override display name from runtime state
                    if (_attributeDisplayNameOverrides.ContainsKey(kvp.Key) &&
                        _attributeDisplayNameOverrides[kvp.Key].ContainsKey(attr.LogicalName))
                    {
                        displayInfo.OverrideDisplayName = _attributeDisplayNameOverrides[kvp.Key][attr.LogicalName];
                    }
                    
                    tableDict[attr.LogicalName] = displayInfo;
                }
                attributeDisplayInfo[kvp.Key] = tableDict;
            }
            
            return (exportTables, exportRelationships, attributeDisplayInfo);
        }
        
        private void BuildSemanticModel(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show("Please select a working folder before building the model.",
                    "Missing Working Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var outputFolder = outputPath!;

            // Prepare all data structures first
            var modelName = _currentModel?.Name ?? _currentSolutionName ?? "MySemanticModel";
            
            // Use saved template path if it exists, otherwise use detected path
            var templatePath = _currentModel?.TemplatePath;
            if (string.IsNullOrEmpty(templatePath) || !Directory.Exists(templatePath))
            {
                templatePath = _templatePath;
            }
            if (string.IsNullOrEmpty(templatePath) || !Directory.Exists(templatePath))
            {
                MessageBox.Show("Template path could not be resolved.", "Missing Template",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            var fullUrl = _currentEnvironmentUrl ?? "";
            
            var (exportTables, exportRelationships, attributeDisplayInfo) = PrepareExportData();
            
            // Single WorkAsync that does EVERYTHING including showing the dialog
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Building semantic model...",
                Work = (worker, args) =>
                {
                    worker.ReportProgress(0, "Initializing semantic model builder...");
                    
                    var builder = new SemanticModelBuilder(templatePath!, msg =>
                    {
                        worker.ReportProgress(-1, msg);
                    },
                    _currentModel?.ConnectionType ?? "DataverseTDS",
                    _currentModel?.FabricLinkSQLEndpoint,
                    _currentModel?.FabricLinkSQLDatabase,
                    _currentModel?.PluginSettings?.LanguageCode ?? 1033,
                    _currentModel?.UseDisplayNameAliasesInSql ?? true,
                    _currentModel?.StorageMode ?? "DirectQuery");
                    
                    if ((_currentModel?.StorageMode ?? "DirectQuery") == "DualSelect")
                        builder.SetTableStorageModeOverrides(_currentModel?.PluginSettings?.TableStorageModes);
                    worker.ReportProgress(10, "Analyzing changes...");

                    // Get date table config from current model
                    var dateTableConfig = _currentModel?.PluginSettings?.DateTableConfig;

                    var changes = builder.AnalyzeChanges(
                        modelName,
                        outputFolder,
                        fullUrl,
                        exportTables,
                        exportRelationships,
                        attributeDisplayInfo,
                        dateTableConfig);
                    
                    // Show the dialog on the UI thread and wait for response
                    bool userApproved = false;
                    bool createBackup = false;
                    bool removeOrphanedTables = false;
                    
                    this.Invoke(new Action(() =>
                    {
                        using (var dialog = new SemanticModelChangesDialog(changes))
                        {
                            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.UserApproved)
                            {
                                userApproved = true;
                                createBackup = dialog.CreateBackup;
                                removeOrphanedTables = dialog.RemoveOrphanedTables;
                            }
                        }
                    }));
                    
                    if (!userApproved)
                    {
                        args.Result = new { Success = false, Cancelled = true };
                        return;
                    }
                    
                    worker.ReportProgress(30, "Applying changes...");

                    // Apply the changes
                    var success = builder.ApplyChanges(
                        modelName,
                        outputPath,
                        fullUrl,
                        exportTables,
                        exportRelationships,
                        attributeDisplayInfo,
                        createBackup,
                        dateTableConfig,
                        removeOrphanedTables);
                    
                    args.Result = new { 
                        Success = success, 
                        Cancelled = false,
                        ModelName = modelName, 
                        OutputPath = outputPath,
                        ExportTables = exportTables,
                        ExportRelationships = exportRelationships
                    };
                },
                ProgressChanged = (args) =>
                {
                    SetStatus(args.UserState?.ToString() ?? "Working...");
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show($"Error: {args.Error.Message}\n\n{args.Error.StackTrace}", "Build Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        SetStatus("Build failed");
                        return;
                    }
                    
                    dynamic result = args.Result;
                    
                    if (result.Cancelled)
                    {
                        SetStatus("Build cancelled");
                        MessageBox.Show("Semantic model build was cancelled.", "Build Cancelled",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    
                    if (!result.Success)
                    {
                        MessageBox.Show("Semantic model build failed.", "Build Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        SetStatus("Build failed");
                        return;
                    }
                    
                    SetStatus("Semantic model build complete!");
                    
                    var environmentName = ExtractEnvironmentName(fullUrl);
                    var pbipPath = Path.GetFullPath(Path.Combine(result.OutputPath, environmentName, result.ModelName, $"{result.ModelName}.pbip"));
                    var dialogResult = MessageBox.Show(
                        $"Semantic model built successfully!\n\n" +
                        $"Location: {pbipPath}\n\n" +
                        $"Tables: {result.ExportTables.Count}\n" +
                        $"Relationships: {result.ExportRelationships.Count}\n\n" +
                        $"Would you like to open the semantic model in Power BI Desktop?",
                        "Build Complete",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    
                    if (dialogResult == DialogResult.Yes)
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
            });
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
        
        private void BtnSemanticModel_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentEnvironmentUrl))
            {
                MessageBox.Show("Please connect to an environment first.",
                    "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            ShowSemanticModelSelector();
        }
        
        private void BtnRefreshMetadata_Click(object sender, EventArgs e)
        {
            if (_xrmAdapter == null || _currentModel == null)
            {
                MessageBox.Show("Please connect to an environment and select a semantic model first.",
                    "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            RevalidateMetadata();
        }
        
        private void BtnChangeWorkingFolder_Click(object sender, EventArgs e)
        {
            // Open the current model's working folder if it exists
            if (_currentModel != null && !string.IsNullOrEmpty(_currentModel.WorkingFolder))
            {
                if (Directory.Exists(_currentModel.WorkingFolder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", _currentModel.WorkingFolder);
                    return;
                }
            }
            
            // Otherwise let user select a folder
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select working folder for semantic model output";
                dialog.ShowNewFolderButton = true;
                
                if (_currentModel != null && !string.IsNullOrEmpty(_currentModel.WorkingFolder))
                    dialog.SelectedPath = _currentModel.WorkingFolder;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (_currentModel != null)
                    {
                        _currentModel.WorkingFolder = dialog.SelectedPath;
                        _modelManager.SaveModel(_currentModel);
                    }
                    SetStatus($"Working folder: {dialog.SelectedPath}");
                }
            }
        }
        
        private void BtnSettingsFolder_Click(object sender, EventArgs e)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "MscrmTools", "XrmToolBox", "Settings", "DataverseToPowerBI");
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start("explorer.exe", folder);
        }

        private void BtnPreviewTmdl_Click(object sender, EventArgs e)
        {
            if (_selectedTables.Count == 0)
            {
                MessageBox.Show("Please select tables first.", "No Tables", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check if all selected tables have their metadata loaded
            var tablesWithoutMetadata = _selectedTables.Keys
                .Where(tableName => !_tableAttributes.ContainsKey(tableName))
                .ToList();

            if (tablesWithoutMetadata.Any())
            {
                MessageBox.Show(
                    "Metadata has not been loaded for all tables. Please wait for metadata to load before previewing.",
                    "Metadata Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Validate no duplicate display name aliases
            if (HasDuplicateDisplayNames()) return;

            string connectionType = _currentModel?.ConnectionType ?? "DataverseTDS";
            var fullUrl = _currentEnvironmentUrl ?? "";

            // Use saved template path if it exists, otherwise use detected path
            var templatePath = _currentModel?.TemplatePath;
            if (string.IsNullOrEmpty(templatePath) || !Directory.Exists(templatePath))
            {
                templatePath = _templatePath;
            }
            if (string.IsNullOrEmpty(templatePath) || !Directory.Exists(templatePath))
            {
                MessageBox.Show("Template path could not be resolved.", "Missing Template",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                SetStatus("Generating TMDL preview...");

                var (exportTables, exportRelationships, attributeDisplayInfo) = PrepareExportData();
                var dateTableConfig = _currentModel?.PluginSettings?.DateTableConfig;

                var builder = new SemanticModelBuilder(templatePath, msg => { },
                    connectionType,
                    _currentModel?.FabricLinkSQLEndpoint,
                    _currentModel?.FabricLinkSQLDatabase,
                    _currentModel?.PluginSettings?.LanguageCode ?? 1033,
                    _currentModel?.UseDisplayNameAliasesInSql ?? true,
                    _currentModel?.StorageMode ?? "DirectQuery");

                if ((_currentModel?.StorageMode ?? "DirectQuery") == "DualSelect")
                    builder.SetTableStorageModeOverrides(_currentModel?.PluginSettings?.TableStorageModes);

                var entries = builder.GenerateTmdlPreview(
                    fullUrl,
                    exportTables,
                    exportRelationships,
                    attributeDisplayInfo,
                    dateTableConfig);

                SetStatus("Opening TMDL preview...");

                using (var dialog = new TmdlPreviewDialog(entries, connectionType))
                {
                    dialog.ShowDialog(this);
                }

                SetStatus("Ready.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error generating TMDL preview:\n\n{ex.Message}",
                    "Preview Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                SetStatus("Preview failed.");
                DebugLogger.Log($"TMDL Preview error: {ex}");
            }
        }
        
        #endregion
        
        #region Helpers
        
        private void SetStatus(string message)
        {
            lblStatus.Text = message;
        }
        
        #endregion
    }
    
    #region Settings Classes
    
    [System.Runtime.Serialization.DataContract]
    public class PluginSettings
    {
        [System.Runtime.Serialization.DataMember]
        public string? LastSolutionId { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? LastSolutionName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? FactTable { get; set; }
        [System.Runtime.Serialization.DataMember]
        public List<string> SelectedTableNames { get; set; } = new List<string>();
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, List<string>> SelectedAttributes { get; set; } = new Dictionary<string, List<string>>();
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, string> SelectedFormIds { get; set; } = new Dictionary<string, string>();
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, string> SelectedViewIds { get; set; } = new Dictionary<string, string>();
        [System.Runtime.Serialization.DataMember]
        public List<SerializedRelationship> Relationships { get; set; } = new List<SerializedRelationship>();
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, TableDisplayInfo> TableDisplayInfo { get; set; } = new Dictionary<string, TableDisplayInfo>();
        [System.Runtime.Serialization.DataMember]
        public bool ShowAllAttributes { get; set; } = false;
        [System.Runtime.Serialization.DataMember]
        public DateTableConfig? DateTableConfig { get; set; } = null;
        /// <summary>
        /// The organization's base language code (LCID) for FabricLink metadata queries.
        /// Defaults to 1033 (US English).
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int LanguageCode { get; set; } = 1033;
        /// <summary>
        /// User-specified override display names for attributes.
        /// Outer key = table logical name, inner key = attribute logical name, value = override display name.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, Dictionary<string, string>> AttributeDisplayNameOverrides { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        /// <summary>
        /// Per-table storage mode overrides for "DualSelect" mode.
        /// Key = table logical name, Value = "directQuery" or "dual".
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, string> TableStorageModes { get; set; } = new Dictionary<string, string>();
    }
    
    [System.Runtime.Serialization.DataContract]
    public class TableDisplayInfo
    {
        [System.Runtime.Serialization.DataMember]
        public string? DisplayName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? SchemaName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? PrimaryIdAttribute { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? PrimaryNameAttribute { get; set; }
    }
    
    [System.Runtime.Serialization.DataContract]
    public class SerializedRelationship
    {
        [System.Runtime.Serialization.DataMember]
        public string? SourceTable { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? SourceAttribute { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? TargetTable { get; set; }
        [System.Runtime.Serialization.DataMember]
        public bool IsActive { get; set; }
        [System.Runtime.Serialization.DataMember]
        public bool IsSnowflake { get; set; }
        [System.Runtime.Serialization.DataMember]
        public bool AssumeReferentialIntegrity { get; set; } = false;  // True if lookup field is required
    }
    
    [System.Runtime.Serialization.DataContract]
    public class ExportData
    {
        [System.Runtime.Serialization.DataMember]
        public string? EnvironmentUrl { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? ProjectName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? FactTable { get; set; }
        [System.Runtime.Serialization.DataMember]
        public List<ExportTable> Tables { get; set; } = new List<ExportTable>();
        [System.Runtime.Serialization.DataMember]
        public List<SerializedRelationship> Relationships { get; set; } = new List<SerializedRelationship>();
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, List<string>> SelectedAttributes { get; set; } = new Dictionary<string, List<string>>();
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, string> SelectedForms { get; set; } = new Dictionary<string, string>();
        [System.Runtime.Serialization.DataMember]
        public Dictionary<string, string> SelectedViews { get; set; } = new Dictionary<string, string>();
    }
    
    [System.Runtime.Serialization.DataContract]
    public class ExportTable
    {
        [System.Runtime.Serialization.DataMember]
        public string? LogicalName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? DisplayName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? SchemaName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? PrimaryIdAttribute { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? PrimaryNameAttribute { get; set; }
        [System.Runtime.Serialization.DataMember]
        public int ObjectTypeCode { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string Role { get; set; } = "Dimension";  // "Fact" or "Dimension"
        [System.Runtime.Serialization.DataMember]
        public bool HasStateCode { get; set; } = false;  // True if table has a statecode attribute
        [System.Runtime.Serialization.DataMember]
        public List<Core.Models.AttributeMetadata> Attributes { get; set; } = new List<Core.Models.AttributeMetadata>();
        [System.Runtime.Serialization.DataMember]
        public ExportView? View { get; set; }
    }
    
    [System.Runtime.Serialization.DataContract]
    public class ExportView
    {
        [System.Runtime.Serialization.DataMember]
        public string? ViewId { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? ViewName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? FetchXml { get; set; }
    }
    
    [System.Runtime.Serialization.DataContract]
    public class AttributeDisplayInfo
    {
        [System.Runtime.Serialization.DataMember]
        public string? LogicalName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? DisplayName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? SchemaName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? AttributeType { get; set; }
        [System.Runtime.Serialization.DataMember]
        public bool IsRequired { get; set; } = false;
        [System.Runtime.Serialization.DataMember]
        public List<string>? Targets { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? VirtualAttributeName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public bool? IsGlobal { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? OptionSetName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string? OverrideDisplayName { get; set; }
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// Comparer for sorting ListView items by column
    /// </summary>
    public class ListViewItemComparer : System.Collections.IComparer
    {
        private readonly int _column;
        private readonly bool _ascending;
        
        public ListViewItemComparer(int column, bool ascending)
        {
            _column = column;
            _ascending = ascending;
        }
        
        public int Compare(object x, object y)
        {
            var itemX = x as ListViewItem;
            var itemY = y as ListViewItem;
            if (itemX == null || itemY == null) return 0;
            
            var textX = _column < itemX.SubItems.Count ? itemX.SubItems[_column].Text : "";
            var textY = _column < itemY.SubItems.Count ? itemY.SubItems[_column].Text : "";
            
            var result = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
            return _ascending ? result : -result;
        }
    }
    
    #endregion
}
