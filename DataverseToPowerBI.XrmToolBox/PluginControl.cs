using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using DataverseToPowerBI.Core.Interfaces;
using DataverseToPowerBI.Core.Models;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// XrmToolBox plugin control - mirrors MainForm from standalone app
    /// Only difference: authentication via XrmToolBox instead of custom OAuth
    /// </summary>
    public partial class PluginControl : PluginControlBase
    {
        private XrmServiceAdapterImpl _xrmAdapter;
        private string _templatePath;
        private SemanticModelManager _modelManager;
        private SemanticModelConfig _currentModel;
        private string _currentEnvironmentUrl;
        
        // State management - same as MainForm
        private Dictionary<string, TableInfo> _selectedTables = new Dictionary<string, TableInfo>();
        private Dictionary<string, List<FormMetadata>> _tableForms = new Dictionary<string, List<FormMetadata>>();
        private Dictionary<string, List<ViewMetadata>> _tableViews = new Dictionary<string, List<ViewMetadata>>();
        private Dictionary<string, List<AttributeMetadata>> _tableAttributes = new Dictionary<string, List<AttributeMetadata>>();
        private Dictionary<string, HashSet<string>> _selectedAttributes = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, string> _selectedFormIds = new Dictionary<string, string>();
        private Dictionary<string, string> _selectedViewIds = new Dictionary<string, string>();
        private Dictionary<string, bool> _loadingStates = new Dictionary<string, bool>();
        
        // Star-schema state
        private string _factTable = null;
        private List<ExportRelationship> _relationships = new List<ExportRelationship>();
        
        // Sorting state
        private int _selectedTablesSortColumn = -1;
        private bool _selectedTablesSortAscending = true;
        private int _attributesSortColumn = -1;
        private bool _attributesSortAscending = true;
        private int _relationshipsSortColumn = -1;
        private bool _relationshipsSortAscending = true;
        
        // Solution 
        private string _currentSolutionName;
        private string _currentSolutionId;
        private List<TableInfo> _solutionTables = new List<TableInfo>();
        
        private bool _isLoading = false;
        
        public PluginControl()
        {
            InitializeComponent();
            
            // Initialize the semantic model manager
            _modelManager = new SemanticModelManager();
            
            // Determine template path from plugin folder
            var pluginFolder = Path.GetDirectoryName(GetType().Assembly.Location);
            _templatePath = Path.Combine(pluginFolder ?? "", "PBIP_DefaultTemplate");
            
            if (!Directory.Exists(_templatePath))
            {
                _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "DataverseToPowerBI", "PBIP_DefaultTemplate");
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
            
            // Set version from assembly
            var version = GetType().Assembly.GetName().Version;
            lblVersion.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            
            // Initialize toolbar icons
            btnSelectTables.Image = RibbonIcons.TableIcon;
            btnCalendarTable.Image = RibbonIcons.CalendarIcon;
            btnBuildSemanticModel.Image = RibbonIcons.BuildIcon;
            btnChangeWorkingFolder.Image = RibbonIcons.FolderIcon;
            
            // Disable controls until connected
            btnSelectTables.Enabled = false;
            btnCalendarTable.Enabled = false;
            
            // Update semantic model display
            UpdateSemanticModelDisplay();
            
            // Initial column sizing (deferred to ensure controls are sized)
            BeginInvoke(new Action(() =>
            {
                ResizeAttributeColumns();
                ResizeTableColumns();
                ResizeRelationshipColumns();
            }));
        }
        
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (newService != null && detail != null)
            {
                var environmentUrl = detail.WebApplicationUrl ?? detail.OrganizationServiceUrl;
                _currentEnvironmentUrl = NormalizeUrl(environmentUrl);
                _xrmAdapter = new XrmServiceAdapterImpl(newService, environmentUrl);

                lblConnectionStatus.Text = $"Connected: {detail.OrganizationFriendlyName}";
                lblConnectionStatus.ForeColor = Color.Green;
                SetStatus($"Connected to {detail.OrganizationFriendlyName}");
                
                // Check for first-run experience
                if (_modelManager.IsFirstRun())
                {
                    PromptForFirstSemanticModel();
                    return;
                }
                
                // Load the most recent model for this environment, or prompt to select
                var recentModel = _modelManager.GetMostRecentModelForEnvironment(_currentEnvironmentUrl);
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
            else
            {
                SaveCurrentModel();
                
                _xrmAdapter = null;
                _currentEnvironmentUrl = null;
                lblConnectionStatus.Text = "Not Connected";
                lblConnectionStatus.ForeColor = Color.Gray;
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
                "DataverseToPowerBI", "Reports");
            Directory.CreateDirectory(defaultFolder);

            var defaultTemplate = _modelManager.GetInstalledTemplatePath() ?? _templatePath;

            using (var dialog = new NewSemanticModelDialogXrm(defaultFolder, _currentEnvironmentUrl, defaultTemplate))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        var newModel = new SemanticModelConfig
                        {
                            Name = dialog.SemanticModelName,
                            DataverseUrl = _currentEnvironmentUrl,
                            WorkingFolder = dialog.WorkingFolder,
                            TemplatePath = dialog.TemplatePath,
                            LastUsed = DateTime.Now,
                            CreatedDate = DateTime.Now
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
                        MessageBox.Show($"Error: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    var solutions = args.Result as List<DataverseSolution> ?? new List<DataverseSolution>();
                    ShowSolutionAndTableSelector(solutions);
                }
            });
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
            
            // Restore relationships
            _relationships.Clear();
            if (settings.Relationships != null)
            {
                foreach (var r in settings.Relationships)
                {
                    _relationships.Add(new ExportRelationship
                    {
                        SourceTable = r.SourceTable,
                        SourceAttribute = r.SourceAttribute,
                        TargetTable = r.TargetTable,
                        IsActive = r.IsActive,
                        IsSnowflake = r.IsSnowflake
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
                
                settings.LastSolutionName = _currentSolutionName;
                settings.LastSolutionId = _currentSolutionId;
                settings.FactTable = _factTable;
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
                    IsSnowflake = r.IsSnowflake
                }).ToList();
                
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
            using (var dialog = new SemanticModelSelectorDialog(_modelManager, _currentEnvironmentUrl))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedSemanticModel != null)
                {
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
                else if (dialog.ConfigurationsChanged && !string.IsNullOrEmpty(dialog.NewlyCreatedConfiguration))
                {
                    // A new model was created, load it and start table selection
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
        }
        
        private void UpdateSemanticModelDisplay()
        {
            cboSemanticModels.Items.Clear();
            
            if (_currentModel != null)
            {
                cboSemanticModels.Items.Add(_currentModel.Name);
                cboSemanticModels.SelectedIndex = 0;
            }
            else
            {
                cboSemanticModels.Items.Add("(Click to select...)");
                cboSemanticModels.SelectedIndex = 0;
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
            // Simple solution selector first
            using (var solutionDialog = new SolutionSelectorForm(solutions, _currentSolutionId))
            {
                if (solutionDialog.ShowDialog(this) != DialogResult.OK || solutionDialog.SelectedSolution == null)
                    return;
                
                _currentSolutionId = solutionDialog.SelectedSolution.SolutionId;
                _currentSolutionName = solutionDialog.SelectedSolution.FriendlyName;
                
                // Now load tables for this solution
                WorkAsync(new WorkAsyncInfo
                {
                    Message = $"Loading tables from {_currentSolutionName}...",
                    Work = (worker, args) =>
                    {
                        args.Result = _xrmAdapter.GetSolutionTablesSync(Service, _currentSolutionId);
                    },
                    PostWorkCallBack = (args) =>
                    {
                        if (args.Error != null)
                        {
                            MessageBox.Show($"Error: {args.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        
                        _solutionTables = args.Result as List<TableInfo> ?? new List<TableInfo>();
                        ShowFactDimensionSelector();
                    }
                });
            }
        }
        
        private void ShowFactDimensionSelector()
        {
            using (var dialog = new FactDimensionSelectorForm(
                _xrmAdapter,
                Service,
                _currentSolutionName,
                _solutionTables,
                _factTable,
                _relationships))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
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
                                if (!_selectedAttributes.ContainsKey(tableName) || _selectedAttributes[tableName].Count == 0)
                                {
                                    if (!_selectedAttributes.ContainsKey(tableName))
                                        _selectedAttributes[tableName] = new HashSet<string>();
                                    
                                    var selectedAttrs = _selectedAttributes[tableName];
                                    var attributes = _tableAttributes.ContainsKey(tableName) ? _tableAttributes[tableName] : new List<AttributeMetadata>();
                                    
                                    // Always include primary ID and name attributes
                                    if (!string.IsNullOrEmpty(table.PrimaryIdAttribute))
                                        selectedAttrs.Add(table.PrimaryIdAttribute);
                                    if (!string.IsNullOrEmpty(table.PrimaryNameAttribute))
                                        selectedAttrs.Add(table.PrimaryNameAttribute);
                                    
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
                    
                    SaveSettings();
                }
            });
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
                if (!string.IsNullOrEmpty(table.PrimaryIdAttribute))
                    requiredAttrs.Add(table.PrimaryIdAttribute);
                if (!string.IsNullOrEmpty(table.PrimaryNameAttribute))
                    requiredAttrs.Add(table.PrimaryNameAttribute);
            }
            
            // Update selected attributes - remove any that no longer exist, ensure required are included
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
        
        #endregion
        
        #region UI Display
        
        private void RefreshTableListDisplay()
        {
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
            item.SubItems.Add(formText);
            item.SubItems.Add(viewText);
            item.SubItems.Add(attrCount);

            if (isFact)
            {
                item.BackColor = Color.LightYellow;
                item.Font = new Font(listViewSelectedTables.Font, FontStyle.Bold);
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
                item.SubItems[3].Text = GetFormDisplayText(logicalName);
                item.SubItems[4].Text = GetViewDisplayText(logicalName);
                item.SubItems[5].Text = _selectedAttributes.ContainsKey(logicalName)
                    ? _selectedAttributes[logicalName].Count.ToString()
                    : "0";

                if (isFact)
                {
                    item.BackColor = Color.LightYellow;
                    item.Font = new Font(listViewSelectedTables.Font, FontStyle.Bold);
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
            UpdateRelationshipsDisplay();
        }
        
        private void UpdateRelationshipsDisplay()
        {
            listViewRelationships.Items.Clear();
            
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
            
            // Get required attributes (primary ID and name)
            var requiredAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string primaryIdAttr = null;
            string primaryNameAttr = null;
            if (_selectedTables.ContainsKey(logicalName))
            {
                var table = _selectedTables[logicalName];
                if (!string.IsNullOrEmpty(table.PrimaryIdAttribute))
                {
                    requiredAttrs.Add(table.PrimaryIdAttribute);
                    primaryIdAttr = table.PrimaryIdAttribute;
                }
                if (!string.IsNullOrEmpty(table.PrimaryNameAttribute))
                {
                    requiredAttrs.Add(table.PrimaryNameAttribute);
                    primaryNameAttr = table.PrimaryNameAttribute;
                }
            }
            
            // Get form fields if form is selected
            HashSet<string> formFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_selectedFormIds.ContainsKey(logicalName) && _tableForms.ContainsKey(logicalName))
            {
                var formId = _selectedFormIds[logicalName];
                var form = _tableForms[logicalName].FirstOrDefault(f => f.FormId == formId);
                if (form?.Fields != null)
                    formFields = new HashSet<string>(form.Fields, StringComparer.OrdinalIgnoreCase);
            }
            
            // Sort attributes based on current sort column
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
            
            // Force Id and Name attributes to top (always, regardless of sort)
            var attrList = sortedAttrs.ToList();
            var idAttr = attrList.FirstOrDefault(a => a.LogicalName.Equals(primaryIdAttr, StringComparison.OrdinalIgnoreCase));
            var nameAttr = attrList.FirstOrDefault(a => a.LogicalName.Equals(primaryNameAttr, StringComparison.OrdinalIgnoreCase));
            
            if (idAttr != null) attrList.Remove(idAttr);
            if (nameAttr != null) attrList.Remove(nameAttr);
            
            var finalList = new List<AttributeMetadata>();
            if (idAttr != null) finalList.Add(idAttr);
            if (nameAttr != null) finalList.Add(nameAttr);
            finalList.AddRange(attrList);
            
            foreach (var attr in finalList)
            {
                var isSelected = selected.Contains(attr.LogicalName);
                var isRequired = requiredAttrs.Contains(attr.LogicalName);
                var onForm = formFields.Contains(attr.LogicalName);
                
                // Apply filters (required attributes always shown)
                if (showSelected && !isSelected && !isRequired) continue;
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!(attr.DisplayName?.ToLower().Contains(searchText) == true ||
                          attr.LogicalName.ToLower().Contains(searchText)))
                        continue;
                }
                
                var item = new ListViewItem();
                item.Text = "";
                item.Checked = isSelected || isRequired;  // Required attrs always checked
                item.Name = attr.LogicalName;
                
                // Show lock for required attributes, checkmark for form fields
                var formColumnText = isRequired ? "🔒" : (onForm ? "✓" : "");
                item.SubItems.Add(formColumnText);
                item.SubItems.Add(attr.DisplayName ?? attr.LogicalName);
                item.SubItems.Add(attr.LogicalName);
                item.SubItems.Add(attr.AttributeType ?? "");
                item.Tag = attr.LogicalName;
                
                // Apply visual styling (matching MainForm)
                if (isRequired)
                {
                    item.ForeColor = Color.Blue;
                    item.Font = new Font(listViewAttributes.Font, FontStyle.Bold);
                }
                else if (isSelected)
                {
                    item.ForeColor = Color.Black;
                }
                else
                {
                    item.ForeColor = Color.Gray;
                }
                
                listViewAttributes.Items.Add(item);
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
                if (attrName.Equals(table.PrimaryIdAttribute, StringComparison.OrdinalIgnoreCase) ||
                    attrName.Equals(table.PrimaryNameAttribute, StringComparison.OrdinalIgnoreCase))
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
            UpdateAttributesDisplay(logicalName);
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
            _isLoading = true;
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                item.Checked = false;
            }
            _isLoading = false;
            
            // Update state
            if (listViewSelectedTables.SelectedItems.Count > 0)
            {
                var logicalName = listViewSelectedTables.SelectedItems[0].Name;
                _selectedAttributes[logicalName]?.Clear();
                UpdateSelectedTableRow(logicalName);
                SaveSettings();
            }
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
            
            // Fixed-width columns: Sel (40), Form (50), Type (100)
            const int selWidth = 40;
            const int formWidth = 50;
            const int typeWidth = 100;
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
            
            // Fixed-width columns: Edit (30), Role (55), Attrs (50)
            const int editWidth = 30;
            const int roleWidth = 55;
            const int attrsWidth = 50;
            const int scrollBarWidth = 20;
            
            var availableWidth = listViewSelectedTables.Width - editWidth - roleWidth - attrsWidth - scrollBarWidth;
            if (availableWidth <= 0) return;
            
            // Distribute remaining: Table (40%), Form (25%), Filter (35%)
            var tableWidth = (int)(availableWidth * 0.40);
            var formWidth = (int)(availableWidth * 0.25);
            var filterWidth = availableWidth - tableWidth - formWidth;
            
            listViewSelectedTables.BeginUpdate();
            try
            {
                colEdit.Width = editWidth;
                colRole.Width = roleWidth;
                colTable.Width = tableWidth;
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
            
            using (var dialog = new FormViewSelectorForm(
                logicalName,
                _tableForms[logicalName],
                _tableViews[logicalName],
                _selectedFormIds.ContainsKey(logicalName) ? _selectedFormIds[logicalName] : null,
                _selectedViewIds.ContainsKey(logicalName) ? _selectedViewIds[logicalName] : null))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(dialog.SelectedFormId))
                        _selectedFormIds[logicalName] = dialog.SelectedFormId;
                    if (!string.IsNullOrEmpty(dialog.SelectedViewId))
                        _selectedViewIds[logicalName] = dialog.SelectedViewId;
                    
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
            // TODO: Implement calendar table dialog
            MessageBox.Show("Calendar table configuration not yet implemented in XrmToolBox version.", 
                "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void BtnBuildSemanticModel_Click(object sender, EventArgs e)
        {
            if (_selectedTables.Count == 0)
            {
                MessageBox.Show("Please select tables first.", "No Tables", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Use current model's working folder if available
            string outputPath = null;
            if (_currentModel != null && !string.IsNullOrEmpty(_currentModel.WorkingFolder))
            {
                outputPath = _currentModel.WorkingFolder;
                
                var result = MessageBox.Show(
                    $"Build semantic model to:\n{outputPath}\n\nClick No to choose a different folder.",
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
        
        private void BuildSemanticModel(string outputPath)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Exporting semantic model configuration...",
                Work = (worker, args) =>
                {
                    var modelName = _currentModel?.Name ?? _currentSolutionName ?? "MySemanticModel";
                    var projectFolder = Path.Combine(outputPath, modelName);
                    Directory.CreateDirectory(projectFolder);
                    
                    // Build export data
                    var exportData = new ExportData
                    {
                        EnvironmentUrl = _currentEnvironmentUrl ?? "",
                        ProjectName = modelName,
                        FactTable = _factTable,
                        Tables = _selectedTables.Values.Select(t => new ExportTable
                        {
                            LogicalName = t.LogicalName,
                            DisplayName = t.DisplayName,
                            SchemaName = t.SchemaName,
                            PrimaryIdAttribute = t.PrimaryIdAttribute,
                            PrimaryNameAttribute = t.PrimaryNameAttribute
                        }).ToList(),
                        Relationships = _relationships.Select(r => new SerializedRelationship
                        {
                            SourceTable = r.SourceTable,
                            SourceAttribute = r.SourceAttribute,
                            TargetTable = r.TargetTable,
                            IsActive = r.IsActive,
                            IsSnowflake = r.IsSnowflake
                        }).ToList(),
                        SelectedAttributes = _selectedAttributes.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.ToList()
                        ),
                        SelectedForms = _selectedFormIds,
                        SelectedViews = _selectedViewIds
                    };
                    
                    // Export as JSON
                    var jsonPath = Path.Combine(projectFolder, "model-config.json");
                    using (var ms = new MemoryStream())
                    {
                        var serializer = new DataContractJsonSerializer(typeof(ExportData));
                        serializer.WriteObject(ms, exportData);
                        File.WriteAllText(jsonPath, Encoding.UTF8.GetString(ms.ToArray()));
                    }
                    
                    // Also create a summary text file
                    var summaryPath = Path.Combine(projectFolder, "README.txt");
                    var summary = new StringBuilder();
                    summary.AppendLine($"Semantic Model: {modelName}");
                    summary.AppendLine($"Environment: {exportData.EnvironmentUrl}");
                    summary.AppendLine($"Exported: {DateTime.Now:g}");
                    summary.AppendLine();
                    summary.AppendLine($"Fact Table: {_factTable ?? "(none)"}");
                    summary.AppendLine($"Tables: {_selectedTables.Count}");
                    summary.AppendLine($"Relationships: {_relationships.Count}");
                    summary.AppendLine();
                    summary.AppendLine("Tables:");
                    foreach (var t in _selectedTables.Values)
                    {
                        var attrCount = _selectedAttributes.ContainsKey(t.LogicalName) 
                            ? _selectedAttributes[t.LogicalName].Count : 0;
                        summary.AppendLine($"  - {t.DisplayName ?? t.LogicalName} ({t.LogicalName}): {attrCount} attributes");
                    }
                    File.WriteAllText(summaryPath, summary.ToString());
                    
                    args.Result = projectFolder;
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show($"Error: {args.Error.Message}", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    var projectFolder = args.Result as string;
                    SetStatus($"Configuration exported to {projectFolder}");
                    
                    var result = MessageBox.Show(
                        $"Configuration exported successfully!\n\n{projectFolder}\n\n" +
                        "Use the standalone Dataverse Metadata Extractor app to generate the full TMDL semantic model.\n\n" +
                        "Open folder?",
                        "Export Complete",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    
                    if (result == DialogResult.Yes)
                        System.Diagnostics.Process.Start("explorer.exe", projectFolder);
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
        
        private void CboSemanticModels_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Don't handle during loading
            if (_isLoading) return;
            
            // If dropdown was clicked, show the selector dialog
            // We trigger this on dropdown open instead
        }
        
        private void CboSemanticModels_DropDown(object sender, EventArgs e)
        {
            // Cancel the dropdown and show our dialog instead
            cboSemanticModels.DroppedDown = false;
            
            if (string.IsNullOrEmpty(_currentEnvironmentUrl))
            {
                MessageBox.Show("Please connect to an environment first.",
                    "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            ShowSemanticModelSelector();
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
        
        #endregion
        
        #region Helpers
        
        private void SetStatus(string message)
        {
            lblStatus.Text = message;
        }
        
        private void ShowProgress(bool show)
        {
            progressBar.Visible = show;
        }
        
        #endregion
    }
    
    #region Settings Classes
    
    [System.Runtime.Serialization.DataContract]
    public class PluginSettings
    {
        [System.Runtime.Serialization.DataMember]
        public string LastSolutionId { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string LastSolutionName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string FactTable { get; set; }
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
    }
    
    [System.Runtime.Serialization.DataContract]
    public class TableDisplayInfo
    {
        [System.Runtime.Serialization.DataMember]
        public string DisplayName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string SchemaName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string PrimaryIdAttribute { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string PrimaryNameAttribute { get; set; }
    }
    
    [System.Runtime.Serialization.DataContract]
    public class SerializedRelationship
    {
        [System.Runtime.Serialization.DataMember]
        public string SourceTable { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string SourceAttribute { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string TargetTable { get; set; }
        [System.Runtime.Serialization.DataMember]
        public bool IsActive { get; set; }
        [System.Runtime.Serialization.DataMember]
        public bool IsSnowflake { get; set; }
    }
    
    [System.Runtime.Serialization.DataContract]
    public class ExportData
    {
        [System.Runtime.Serialization.DataMember]
        public string EnvironmentUrl { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string ProjectName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string FactTable { get; set; }
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
        public string LogicalName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string DisplayName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string SchemaName { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string PrimaryIdAttribute { get; set; }
        [System.Runtime.Serialization.DataMember]
        public string PrimaryNameAttribute { get; set; }
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
