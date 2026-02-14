// ===================================================================================
// SemanticModelSelectorDialog.cs - Semantic Model Management Hub
// ===================================================================================
//
// PURPOSE:
// This dialog serves as the central management interface for semantic model
// configurations. It allows users to create, edit, copy, rename, and delete
// model configurations stored in the central settings file.
//
// FEATURES:
// - ListView grouped by Dataverse environment URL
// - Current environment shown first for quick access
// - Full CRUD operations for model configurations
// - Working folder and template path management
// - Connection type selection (standard Dataverse or Fabric mirroring)
//
// MODEL CONFIGURATION INCLUDES:
// - Name and Dataverse URL
// - Working folder for PBIP output
// - Template path for base PBIP project
// - Connection type (OData/Fabric)
// - Fabric endpoint and database (when applicable)
// - All table/attribute/relationship selections
//
// DATA BINDING:
// The dialog uses the SemanticModelManager for persistence and the
// SemanticModelConfig class for individual model data.
//
// DIALOG RESULT:
// Returns the selected SemanticModelConfig through SelectedSemanticModel property.
// ConfigurationsChanged indicates if any models were modified.
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Dialog for selecting and managing semantic models.
    /// Groups models by Dataverse URL with current environment first.
    /// </summary>
    public class SemanticModelSelectorDialog : Form
    {
        private ListView listViewModels = null!;
        private GroupBox groupDetails = null!;
        private TextBox txtName = null!;
        private TextBox txtEnvironmentUrl = null!;
        private ComboBox cboConnectionType = null!;
        private TextBox txtFabricEndpoint = null!;
        private TextBox txtFabricDatabase = null!;
        private Label lblFabricEndpoint = null!;
        private Label lblFabricDatabase = null!;
        private CheckBox chkUseDisplayNameAliases = null!;
        private ComboBox cboStorageMode = null!;
        private Label lblStorageMode = null!;
        private TextBox txtWorkingFolder = null!;
        private TextBox txtTemplatePath = null!;
        private Button btnChangeFolder = null!;
        private Button btnChangeTemplate = null!;
        private Button btnNew = null!;
        private Button btnCopy = null!;
        private Button btnRename = null!;
        private Button btnDelete = null!;
        private Button btnExport = null!;
        private Button btnImport = null!;
        private Button btnSelect = null!;
        private Button btnCancel = null!;
        private Label lblName = null!;
        private Label lblEnvironmentUrl = null!;
        private Label lblConnectionType = null!;
        private Label lblWorkingFolder = null!;
        private Label lblTemplatePath = null!;

        private readonly SemanticModelManager _modelManager;
        private readonly string _currentEnvironmentUrl;
        private SemanticModelConfig? _selectedModel;

        public SemanticModelConfig? SelectedSemanticModel => _selectedModel;
        public bool ConfigurationsChanged { get; private set; } = false;
        public bool UrlWasChanged { get; private set; } = false;
        public string NewlyCreatedConfiguration { get; private set; } = "";

        public SemanticModelSelectorDialog(SemanticModelManager modelManager, string currentEnvironmentUrl)
        {
            _modelManager = modelManager;
            _currentEnvironmentUrl = NormalizeUrl(currentEnvironmentUrl);
            InitializeComponent();
            LoadModels();
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

        private void InitializeComponent()
        {
            this.Text = "Semantic Model Manager";
            this.Size = new Size(1050, 620);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Padding = new Padding(0);

            // Left panel - model list
            var lblModels = new Label
            {
                Text = "Semantic Models (grouped by environment):",
                Location = new Point(10, 15),
                AutoSize = true,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, SystemFonts.MessageBoxFont.Size, FontStyle.Regular)
            };

            // Add info label about grayed items under the header
            var lblGrayedInfo = new Label
            {
                Text = "ℹ️ Grayed models are from other environments.",
                Location = new Point(35, 35),
                Size = new Size(400, 18),
                ForeColor = Color.Gray
            };

            listViewModels = new ListView
            {
                Location = new Point(35, 58),
                Size = new Size(565, 362),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false
            };
            listViewModels.Columns.Add("Name", 130);
            listViewModels.Columns.Add("Fact Table", 90);
            listViewModels.Columns.Add("Tables", 45);
            listViewModels.Columns.Add("Mode", 80);
            listViewModels.Columns.Add("Connection", 75);
            listViewModels.Columns.Add("Last Used", 80);
            listViewModels.SelectedIndexChanged += ListViewModels_SelectedIndexChanged;
            listViewModels.DoubleClick += ListViewModels_DoubleClick;
            
            // Set visual styles for group headers and indentation
            listViewModels.OwnerDraw = true;
            listViewModels.DrawItem += ListViewModels_DrawItem;
            listViewModels.DrawSubItem += ListViewModels_DrawSubItem;
            listViewModels.DrawColumnHeader += ListViewModels_DrawColumnHeader;

            // Buttons for model management
            btnNew = new Button
            {
                Text = "New...",
                Location = new Point(35, 428),
                Size = new Size(70, 28)
            };
            btnNew.Click += BtnNew_Click;

            btnCopy = new Button
            {
                Text = "Copy...",
                Location = new Point(110, 428),
                Size = new Size(70, 28),
                Enabled = false
            };
            btnCopy.Click += BtnCopy_Click;

            btnRename = new Button
            {
                Text = "Rename...",
                Location = new Point(185, 428),
                Size = new Size(80, 28),
                Enabled = false
            };
            btnRename.Click += BtnRename_Click;

            btnDelete = new Button
            {
                Text = "Delete",
                Location = new Point(270, 428),
                Size = new Size(70, 28),
                Enabled = false
            };
            btnDelete.Click += BtnDelete_Click;

            btnExport = new Button
            {
                Text = "Export...",
                Location = new Point(370, 428),
                Size = new Size(70, 28),
                Enabled = false
            };
            btnExport.Click += BtnExport_Click;

            btnImport = new Button
            {
                Text = "Import...",
                Location = new Point(445, 428),
                Size = new Size(70, 28)
            };
            btnImport.Click += BtnImport_Click;

            // Right panel - configuration details
            groupDetails = new GroupBox
            {
                Text = "Configuration Details",
                Location = new Point(620, 15),
                Size = new Size(400, 505)
            };

            lblName = new Label
            {
                Text = "Name:",
                Location = new Point(15, 30),
                AutoSize = true
            };

            txtName = new TextBox
            {
                Location = new Point(15, 50),
                Size = new Size(365, 23),
                ReadOnly = true,
                BackColor = SystemColors.Control
            };

            lblEnvironmentUrl = new Label
            {
                Text = "Dataverse Environment URL:",
                Location = new Point(15, 85),
                AutoSize = true
            };

            txtEnvironmentUrl = new TextBox
            {
                Location = new Point(15, 105),
                Size = new Size(365, 23),
                ReadOnly = true,
                BackColor = SystemColors.Control
            };

            // Working Folder - moved up under Environment URL
            lblWorkingFolder = new Label
            {
                Text = "Working Folder:",
                Location = new Point(15, 145),
                AutoSize = true
            };

            txtWorkingFolder = new TextBox
            {
                Location = new Point(15, 165),
                Size = new Size(270, 23),
                ReadOnly = true,
                BackColor = SystemColors.Window
            };

            btnChangeFolder = new Button
            {
                Text = "Change...",
                Location = new Point(295, 164),
                Size = new Size(85, 25),
                Enabled = false
            };
            btnChangeFolder.Click += BtnChangeFolder_Click;

            // PBIP Template - moved up under Working Folder
            lblTemplatePath = new Label
            {
                Text = "PBIP Template:",
                Location = new Point(15, 205),
                AutoSize = true
            };

            txtTemplatePath = new TextBox
            {
                Location = new Point(15, 225),
                Size = new Size(270, 23),
                ReadOnly = true,
                BackColor = SystemColors.Window
            };

            btnChangeTemplate = new Button
            {
                Text = "Change...",
                Location = new Point(295, 224),
                Size = new Size(85, 25),
                Enabled = false
            };
            btnChangeTemplate.Click += BtnChangeTemplate_Click;

            // Storage Mode - above Connection Type
            lblStorageMode = new Label
            {
                Text = "Storage Mode:",
                Location = new Point(15, 265),
                AutoSize = true
            };

            cboStorageMode = new ComboBox
            {
                Location = new Point(15, 285),
                Size = new Size(365, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboStorageMode.Items.AddRange(new object[] { "Direct Query", "Dual - All Dimensions", "Dual - Select Tables", "Import" });
            cboStorageMode.SelectedIndex = 0;
            cboStorageMode.SelectedIndexChanged += CboStorageMode_SelectedIndexChanged;

            // Connection Type - below Storage Mode
            lblConnectionType = new Label
            {
                Text = "Connection Type:",
                Location = new Point(15, 315),
                AutoSize = true
            };

            cboConnectionType = new ComboBox
            {
                Location = new Point(15, 335),
                Size = new Size(365, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboConnectionType.Items.AddRange(new object[] { "Dataverse TDS Endpoint", "FabricLink Lakehouse SQL" });
            cboConnectionType.SelectedIndex = 0;
            cboConnectionType.SelectedIndexChanged += CboConnectionType_SelectedIndexChanged;

            // Fabric fields - below Storage Mode
            lblFabricEndpoint = new Label
            {
                Text = "FabricLink SQL Endpoint:",
                Location = new Point(15, 365),
                AutoSize = true,
                Visible = false
            };

            txtFabricEndpoint = new TextBox
            {
                Location = new Point(15, 385),
                Size = new Size(365, 23),
                Visible = false
            };
            txtFabricEndpoint.TextChanged += TxtFabricEndpoint_TextChanged;

            lblFabricDatabase = new Label
            {
                Text = "FabricLink Lakehouse:",
                Location = new Point(15, 425),
                AutoSize = true,
                Visible = false
            };

            txtFabricDatabase = new TextBox
            {
                Location = new Point(15, 445),
                Size = new Size(365, 23),
                Visible = false
            };
            txtFabricDatabase.TextChanged += TxtFabricDatabase_TextChanged;

            // Display Name Aliases checkbox - below connection fields
            chkUseDisplayNameAliases = new CheckBox
            {
                Text = "Use display name aliases in SQL queries",
                Location = new Point(15, 475),
                Size = new Size(365, 20),
                Checked = true
            };
            chkUseDisplayNameAliases.CheckedChanged += ChkUseDisplayNameAliases_CheckedChanged;

            groupDetails.Controls.Add(lblName);
            groupDetails.Controls.Add(txtName);
            groupDetails.Controls.Add(lblEnvironmentUrl);
            groupDetails.Controls.Add(txtEnvironmentUrl);
            groupDetails.Controls.Add(lblWorkingFolder);
            groupDetails.Controls.Add(txtWorkingFolder);
            groupDetails.Controls.Add(btnChangeFolder);
            groupDetails.Controls.Add(lblTemplatePath);
            groupDetails.Controls.Add(txtTemplatePath);
            groupDetails.Controls.Add(btnChangeTemplate);
            groupDetails.Controls.Add(lblStorageMode);
            groupDetails.Controls.Add(cboStorageMode);
            groupDetails.Controls.Add(lblConnectionType);
            groupDetails.Controls.Add(cboConnectionType);
            groupDetails.Controls.Add(lblFabricEndpoint);
            groupDetails.Controls.Add(txtFabricEndpoint);
            groupDetails.Controls.Add(lblFabricDatabase);
            groupDetails.Controls.Add(txtFabricDatabase);
            groupDetails.Controls.Add(chkUseDisplayNameAliases);

            // Bottom buttons
            btnSelect = new Button
            {
                Text = "Select",
                Location = new Point(840, 540),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            btnSelect.Click += BtnSelect_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(940, 540),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblModels);
            this.Controls.Add(lblGrayedInfo);
            this.Controls.Add(listViewModels);
            this.Controls.Add(btnNew);
            this.Controls.Add(btnCopy);
            this.Controls.Add(btnRename);
            this.Controls.Add(btnDelete);
            this.Controls.Add(btnExport);
            this.Controls.Add(btnImport);
            this.Controls.Add(groupDetails);
            this.Controls.Add(btnSelect);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSelect;
            this.CancelButton = btnCancel;
        }

        private void LoadModels()
        {
            listViewModels.Items.Clear();
            listViewModels.Groups.Clear();

            var allModels = _modelManager.GetAllModels();
            
            // Group models by environment URL
            var currentEnvModels = allModels
                .Where(m => NormalizeUrl(m.DataverseUrl) == _currentEnvironmentUrl)
                .OrderByDescending(m => m.LastUsed)
                .ToList();

            var otherEnvModels = allModels
                .Where(m => NormalizeUrl(m.DataverseUrl) != _currentEnvironmentUrl)
                .GroupBy(m => NormalizeUrl(m.DataverseUrl))
                .OrderBy(g => g.Key)
                .ToList();

            // Add group for current environment
            var currentGroup = new ListViewGroup("current", $"Current Environment ({_currentEnvironmentUrl})");
            listViewModels.Groups.Add(currentGroup);

            foreach (var model in currentEnvModels)
            {
                var item = new ListViewItem(model.Name)
                {
                    Tag = model,
                    Group = currentGroup
                };
                
                // Fact Table column
                var factTableDisplay = "(none)";
                var factTableKey = model.PluginSettings?.FactTable;
                var tableDisplayInfo = model.PluginSettings?.TableDisplayInfo;
                if (factTableKey is { Length: > 0 } key && tableDisplayInfo != null &&
                    tableDisplayInfo.TryGetValue(key, out var factTableInfo))
                {
                    factTableDisplay = factTableInfo.DisplayName ?? key;
                }
                item.SubItems.Add(factTableDisplay);
                
                // Tables count column
                var tableCount = model.PluginSettings?.SelectedTableNames?.Count ?? 0;
                item.SubItems.Add(tableCount.ToString());
                
                // Storage Mode column
                var storageMode = (model.StorageMode ?? "DirectQuery") switch
                {
                    "Dual" => "Dual - All",
                    "DualSelect" => "Dual - Select",
                    "Import" => "Import",
                    _ => "Direct Query"
                };
                item.SubItems.Add(storageMode);
                
                // Connection column
                var connectionType = string.IsNullOrEmpty(model.ConnectionType) || model.ConnectionType == "DataverseTDS" ? "TDS" : "FabricLink";
                item.SubItems.Add(connectionType);
                
                // Last Used column
                item.SubItems.Add(model.LastUsed.ToString("d"));
                listViewModels.Items.Add(item);
            }

            // Add groups for other environments
            foreach (var group in otherEnvModels)
            {
                var envUrl = group.Key;
                var lvGroup = new ListViewGroup(envUrl, $"Other: {envUrl}");
                listViewModels.Groups.Add(lvGroup);

                foreach (var model in group.OrderByDescending(m => m.LastUsed))
                {
                    var item = new ListViewItem(model.Name)
                    {
                        Tag = model,
                        Group = lvGroup,
                        ForeColor = Color.Gray
                    };
                    
                    // Fact Table column
                    var factTableDisplay = "(none)";
                    var factTableKey = model.PluginSettings?.FactTable;
                    var tableDisplayInfo = model.PluginSettings?.TableDisplayInfo;
                    if (factTableKey is { Length: > 0 } key && tableDisplayInfo != null &&
                        tableDisplayInfo.TryGetValue(key, out var factTableInfo))
                    {
                        factTableDisplay = factTableInfo.DisplayName ?? key;
                    }
                    item.SubItems.Add(factTableDisplay);
                    
                    // Tables count column
                    var tableCount = model.PluginSettings?.SelectedTableNames?.Count ?? 0;
                    item.SubItems.Add(tableCount.ToString());
                    
                    // Storage Mode column
                    var storageMode = (model.StorageMode ?? "DirectQuery") switch
                    {
                        "Dual" => "Dual - All",
                        "DualSelect" => "Dual - Select",
                        "Import" => "Import",
                        _ => "Direct Query"
                    };
                    item.SubItems.Add(storageMode);
                    
                    // Connection column
                    var connectionType = string.IsNullOrEmpty(model.ConnectionType) || model.ConnectionType == "DataverseTDS" ? "TDS" : "FabricLink";
                    item.SubItems.Add(connectionType);
                    
                    // Last Used column
                    item.SubItems.Add(model.LastUsed.ToString("d"));
                    listViewModels.Items.Add(item);
                }
            }

            // Auto-select current model if any
            var currentModelName = _modelManager.GetCurrentModelName();
            if (!string.IsNullOrEmpty(currentModelName))
            {
                var item = listViewModels.Items.Cast<ListViewItem>()
                    .FirstOrDefault(i => ((SemanticModelConfig)i.Tag).Name == currentModelName);
                if (item != null)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                }
            }
            else if (listViewModels.Items.Count > 0)
            {
                listViewModels.Items[0].Selected = true;
            }
        }

        private string TruncateUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            url = NormalizeUrl(url);
            if (url.Length > 25)
                return url.Substring(0, 22) + "...";
            return url;
        }

        private void ListViewModels_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewModels.SelectedItems.Count == 0)
            {
                _selectedModel = null;
                ClearDetails();
                SetButtonStates(false);
                return;
            }

            _selectedModel = (SemanticModelConfig)listViewModels.SelectedItems[0].Tag;
            DisplayDetails(_selectedModel);
            SetButtonStates(true);
        }

        private void SetButtonStates(bool hasSelection)
        {
            btnSelect.Enabled = hasSelection;
            btnCopy.Enabled = hasSelection;
            btnRename.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection && _modelManager.GetAllModels().Count > 1;
            btnExport.Enabled = hasSelection;
            btnChangeFolder.Enabled = hasSelection;
            btnChangeTemplate.Enabled = hasSelection;
        }

        private void DisplayDetails(SemanticModelConfig model)
        {
            txtName.Text = model.Name;
            txtEnvironmentUrl.Text = model.DataverseUrl ?? "";
            
            // Set connection type
            var connectionType = model.ConnectionType ?? "DataverseTDS";
            cboConnectionType.SelectedIndex = connectionType == "FabricLink" ? 1 : 0;
            
            // Set storage mode
            cboStorageMode.SelectedIndex = (model.StorageMode ?? "DirectQuery") switch
            {
                "Dual" => 1,
                "DualSelect" => 2,
                "Import" => 3,
                _ => 0
            };
            
            // Set FabricLink fields
            txtFabricEndpoint.Text = model.FabricLinkSQLEndpoint ?? "";
            txtFabricDatabase.Text = model.FabricLinkSQLDatabase ?? "";
            UpdateFabricLinkFieldsVisibility();
            
            txtWorkingFolder.Text = model.WorkingFolder ?? "";
            txtTemplatePath.Text = model.TemplatePath ?? "";
            chkUseDisplayNameAliases.Checked = model.UseDisplayNameAliasesInSql;

            // Indicate if model is from different environment
            bool isCurrentEnv = NormalizeUrl(model.DataverseUrl ?? "") == _currentEnvironmentUrl;
            txtEnvironmentUrl.ForeColor = isCurrentEnv ? SystemColors.ControlText : Color.Orange;
        }

        private void ClearDetails()
        {
            txtName.Text = "";
            txtEnvironmentUrl.Text = "";
            cboConnectionType.SelectedIndex = 0;
            cboStorageMode.SelectedIndex = 0;
            txtFabricEndpoint.Text = "";
            txtFabricDatabase.Text = "";
            UpdateFabricLinkFieldsVisibility();
            txtWorkingFolder.Text = "";
            txtTemplatePath.Text = "";
            chkUseDisplayNameAliases.Checked = true;
        }

        private void CboConnectionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateFabricLinkFieldsVisibility();
            if (_selectedModel != null)
            {
                _selectedModel.ConnectionType = cboConnectionType.SelectedIndex == 1 ? "FabricLink" : "DataverseTDS";
                _modelManager.SaveModel(_selectedModel);
                ConfigurationsChanged = true;
            }
        }

        private void TxtFabricEndpoint_TextChanged(object sender, EventArgs e)
        {
            if (_selectedModel != null)
            {
                _selectedModel.FabricLinkSQLEndpoint = txtFabricEndpoint.Text.Trim();
                _modelManager.SaveModel(_selectedModel);
                ConfigurationsChanged = true;
            }
        }

        private void TxtFabricDatabase_TextChanged(object sender, EventArgs e)
        {
            if (_selectedModel != null)
            {
                _selectedModel.FabricLinkSQLDatabase = txtFabricDatabase.Text.Trim();
                _modelManager.SaveModel(_selectedModel);
                ConfigurationsChanged = true;
            }
        }

        private void ChkUseDisplayNameAliases_CheckedChanged(object sender, EventArgs e)
        {
            if (_selectedModel != null)
            {
                _selectedModel.UseDisplayNameAliasesInSql = chkUseDisplayNameAliases.Checked;
                _modelManager.SaveModel(_selectedModel);
                ConfigurationsChanged = true;
            }
        }

        private void CboStorageMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_selectedModel != null)
            {
                _selectedModel.StorageMode = cboStorageMode.SelectedIndex switch
                {
                    1 => "Dual",
                    2 => "DualSelect",
                    3 => "Import",
                    _ => "DirectQuery"
                };
                _modelManager.SaveModel(_selectedModel);
                ConfigurationsChanged = true;
            }
        }

        private void UpdateFabricLinkFieldsVisibility()
        {
            bool isFabricLink = cboConnectionType.SelectedIndex == 1;
            lblFabricEndpoint.Visible = isFabricLink;
            txtFabricEndpoint.Visible = isFabricLink;
            lblFabricDatabase.Visible = isFabricLink;
            txtFabricDatabase.Visible = isFabricLink;
        }

        private void ListViewModels_DoubleClick(object sender, EventArgs e)
        {
            if (_selectedModel != null)
            {
                ConfirmAndSelect();
            }
        }

        private void BtnSelect_Click(object sender, EventArgs e)
        {
            ConfirmAndSelect();
        }

        private void ListViewModels_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void ListViewModels_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            // Draw group headers at left edge, items indented
            if (e.Item.Group != null && e.Item.Index == e.Item.Group.Items[0].Index)
            {
                // This is the first item in a group - draw the group header
                // Group headers are automatically drawn by the ListView
            }
            e.DrawDefault = false;
        }

        private void ListViewModels_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            // Indent all items by 20 pixels
            var bounds = e.Bounds;
            if (e.ColumnIndex == 0)
            {
                // First column - add indentation
                bounds = new Rectangle(bounds.X + 20, bounds.Y, bounds.Width - 20, bounds.Height);
            }

            // Draw background
            var bgColor = e.Item.Selected ? SystemColors.Highlight : e.Item.BackColor;
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            // Draw text
            var fgColor = e.Item.Selected ? SystemColors.HighlightText : e.Item.ForeColor;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, bounds,
                fgColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void ConfirmAndSelect()
        {
            if (_selectedModel == null) return;

            bool isCurrentEnv = NormalizeUrl(_selectedModel.DataverseUrl) == _currentEnvironmentUrl;

            if (!isCurrentEnv && !string.IsNullOrEmpty(_selectedModel.DataverseUrl))
            {
                // Model is from a different environment - ask if user wants to update URL
                var result = MessageBox.Show(
                    $"This semantic model is configured for a different environment:\n\n" +
                    $"Current environment:  {_currentEnvironmentUrl}\n" +
                    $"Model's environment:  {NormalizeUrl(_selectedModel.DataverseUrl)}\n\n" +
                    $"Do you want to update this model's environment URL to use the current environment?\n\n" +
                    $"• Yes - Update URL and reload metadata from current environment\n" +
                    $"• No - Cancel selection",
                    "Different Environment",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    this.DialogResult = DialogResult.None;
                    return;
                }

                // Update the model's URL
                _selectedModel.DataverseUrl = _currentEnvironmentUrl;
                _modelManager.SaveModel(_selectedModel);
                UrlWasChanged = true;
                ConfigurationsChanged = true;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnNew_Click(object sender, EventArgs e)
        {
            var defaultFolder = GetDefaultWorkingFolder();
            var defaultTemplate = GetDefaultTemplatePath();

            using (var dialog = new NewSemanticModelDialogXrm(defaultFolder, _currentEnvironmentUrl, defaultTemplate))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        // Check if model already exists
                        if (_modelManager.ModelExists(dialog.SemanticModelName))
                        {
                            MessageBox.Show($"A semantic model named '{dialog.SemanticModelName}' already exists.",
                                "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Create the new model
                        var newModel = new SemanticModelConfig
                        {
                            Name = dialog.SemanticModelName,
                            DataverseUrl = _currentEnvironmentUrl,
                            ConnectionType = dialog.ConnectionType,
                            StorageMode = dialog.StorageMode,
                            FabricLinkSQLEndpoint = dialog.FabricLinkSQLEndpoint,
                            FabricLinkSQLDatabase = dialog.FabricLinkSQLDatabase,
                            WorkingFolder = dialog.WorkingFolder,
                            TemplatePath = dialog.TemplatePath,
                            UseDisplayNameAliasesInSql = dialog.UseDisplayNameAliasesInSql,
                            LastUsed = DateTime.Now,
                            CreatedDate = DateTime.Now,
                            PluginSettings = new PluginSettings()  // Explicitly initialize with empty settings
                        };

                        _modelManager.CreateModel(newModel);
                        ConfigurationsChanged = true;
                        NewlyCreatedConfiguration = dialog.SemanticModelName;

                        LoadModels();

                        // Select the newly created model
                        var item = listViewModels.Items.Cast<ListViewItem>()
                            .FirstOrDefault(i => ((SemanticModelConfig)i.Tag).Name == dialog.SemanticModelName);
                        if (item != null)
                        {
                            item.Selected = true;
                            item.EnsureVisible();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error creating semantic model:\n{ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            using (var dialog = new Form())
            {
                dialog.Text = "Copy Semantic Model";
                dialog.Size = new Size(400, 150);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.StartPosition = FormStartPosition.CenterParent;

                var lblNewName = new Label
                {
                    Text = "New Name:",
                    Location = new Point(20, 20),
                    AutoSize = true
                };

                var txtNewName = new TextBox
                {
                    Text = _selectedModel.Name + " - Copy",
                    Location = new Point(20, 45),
                    Size = new Size(340, 23)
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    Location = new Point(200, 80),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.OK
                };

                var btnCancelDlg = new Button
                {
                    Text = "Cancel",
                    Location = new Point(285, 80),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.Cancel
                };

                dialog.Controls.Add(lblNewName);
                dialog.Controls.Add(txtNewName);
                dialog.Controls.Add(btnOk);
                dialog.Controls.Add(btnCancelDlg);
                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancelDlg;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var newName = txtNewName.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        MessageBox.Show("Please enter a valid name.", "Validation",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (_modelManager.ModelExists(newName))
                    {
                        MessageBox.Show($"A semantic model named '{newName}' already exists.",
                            "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        _modelManager.CopyModel(_selectedModel.Name, newName);
                        ConfigurationsChanged = true;
                        LoadModels();

                        // Select the copied model
                        var item = listViewModels.Items.Cast<ListViewItem>()
                            .FirstOrDefault(i => ((SemanticModelConfig)i.Tag).Name == newName);
                        if (item != null)
                        {
                            item.Selected = true;
                            item.EnsureVisible();
                        }

                        MessageBox.Show($"Semantic model copied to '{newName}'.",
                            "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error copying semantic model:\n{ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnRename_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            using (var dialog = new Form())
            {
                dialog.Text = "Rename Semantic Model";
                dialog.Size = new Size(400, 150);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.StartPosition = FormStartPosition.CenterParent;

                var lblNewName = new Label
                {
                    Text = "New Name:",
                    Location = new Point(20, 20),
                    AutoSize = true
                };

                var txtNewName = new TextBox
                {
                    Text = _selectedModel.Name,
                    Location = new Point(20, 45),
                    Size = new Size(340, 23)
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    Location = new Point(200, 80),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.OK
                };

                var btnCancelDlg = new Button
                {
                    Text = "Cancel",
                    Location = new Point(285, 80),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.Cancel
                };

                dialog.Controls.Add(lblNewName);
                dialog.Controls.Add(txtNewName);
                dialog.Controls.Add(btnOk);
                dialog.Controls.Add(btnCancelDlg);
                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancelDlg;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var newName = txtNewName.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newName) || newName == _selectedModel.Name)
                        return;

                    if (_modelManager.ModelExists(newName))
                    {
                        MessageBox.Show($"A semantic model named '{newName}' already exists.",
                            "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        var oldName = _selectedModel.Name;
                        _modelManager.RenameModel(oldName, newName);
                        ConfigurationsChanged = true;
                        LoadModels();

                        MessageBox.Show($"Semantic model renamed from '{oldName}' to '{newName}'.",
                            "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error renaming semantic model:\n{ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            if (_modelManager.GetAllModels().Count <= 1)
            {
                MessageBox.Show("Cannot delete the last semantic model.",
                    "Cannot Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete the semantic model '{_selectedModel.Name}'?\n\n" +
                "This will delete the configuration settings. The working folder and files will not be deleted.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    _modelManager.DeleteModel(_selectedModel.Name);
                    ConfigurationsChanged = true;
                    LoadModels();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting semantic model:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Export Semantic Model Configuration";
                dialog.Filter = "JSON files (*.json)|*.json";
                dialog.FileName = $"{_selectedModel.Name}.json";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(
                            typeof(SemanticModelConfig),
                            new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings
                            {
                                UseSimpleDictionaryFormat = true
                            });
                        using (var ms = new System.IO.MemoryStream())
                        {
                            serializer.WriteObject(ms, _selectedModel);
                            System.IO.File.WriteAllText(dialog.FileName,
                                System.Text.Encoding.UTF8.GetString(ms.ToArray()));
                        }
                        MessageBox.Show($"Configuration exported to:\n{dialog.FileName}",
                            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exporting configuration:\n{ex.Message}",
                            "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Import Semantic Model Configuration";
                dialog.Filter = "JSON files (*.json)|*.json";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(dialog.FileName);
                        SemanticModelConfig? imported;
                        var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(
                            typeof(SemanticModelConfig),
                            new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings
                            {
                                UseSimpleDictionaryFormat = true
                            });
                        using (var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                        {
                            imported = serializer.ReadObject(ms) as SemanticModelConfig;
                        }

                        if (imported == null)
                        {
                            MessageBox.Show("The file does not contain a valid semantic model configuration.",
                                "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Ensure unique name
                        var baseName = imported.Name;
                        var existingNames = _modelManager.GetAllModels().Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        if (existingNames.Contains(baseName))
                        {
                            int suffix = 2;
                            while (existingNames.Contains($"{baseName} ({suffix})"))
                                suffix++;
                            imported.Name = $"{baseName} ({suffix})";
                        }

                        imported.LastUsed = DateTime.Now;
                        _modelManager.CreateModel(imported);
                        ConfigurationsChanged = true;
                        LoadModels();

                        // Select the imported model
                        var item = listViewModels.Items.Cast<ListViewItem>()
                            .FirstOrDefault(i => ((SemanticModelConfig)i.Tag).Name == imported.Name);
                        if (item != null)
                        {
                            item.Selected = true;
                            item.EnsureVisible();
                        }

                        MessageBox.Show($"Configuration imported as '{imported.Name}'.\n\n" +
                            "You may need to update the Working Folder and Template Path for this machine.",
                            "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error importing configuration:\n{ex.Message}",
                            "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnChangeFolder_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            var selectedPath = VistaFolderPicker.ShowDialog(this, "Select Working Folder", txtWorkingFolder.Text);
            if (selectedPath != null)
            {
                txtWorkingFolder.Text = selectedPath;
                _selectedModel.WorkingFolder = selectedPath;
                _modelManager.SaveModel(_selectedModel);
                ConfigurationsChanged = true;
            }
        }

        private void BtnChangeTemplate_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            var selectedPath = VistaFolderPicker.ShowDialog(this, "Select PBIP Template Folder", txtTemplatePath.Text);
            if (selectedPath != null)
            {
                // Verify it's a valid template folder
                var pbipFiles = Directory.GetFiles(selectedPath, "*.pbip");
                if (pbipFiles.Length == 0)
                {
                    MessageBox.Show("Selected folder does not contain a valid PBIP template (.pbip file not found).",
                        "Invalid Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                txtTemplatePath.Text = selectedPath;
                _selectedModel.TemplatePath = selectedPath;
                _modelManager.SaveModel(_selectedModel);
                ConfigurationsChanged = true;
            }
        }

        private string GetDefaultWorkingFolder()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DataverseToPowerBI");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private string GetDefaultTemplatePath()
        {
            // First check settings folder for installed template
            var settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MscrmTools", "XrmToolBox", "Settings", "DataverseToPowerBI", "PBIP_DefaultTemplate");
            if (Directory.Exists(settingsFolder))
                return settingsFolder;

            // Check AppData Plugins folder
            var appDataPlugins = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MscrmTools", "XrmToolBox", "Plugins", "DataverseToPowerBI", "PBIP_DefaultTemplate");
            if (Directory.Exists(appDataPlugins))
                return appDataPlugins;

            // Check plugin DLL folder with Assets subfolder (development)
            var pluginFolder = Path.GetDirectoryName(GetType().Assembly.Location);
            var assetsPath = Path.Combine(pluginFolder ?? "", "Assets", "PBIP_DefaultTemplate");
            if (Directory.Exists(assetsPath))
                return assetsPath;

            // Fall back to plugin DLL folder
            return Path.Combine(pluginFolder ?? "", "PBIP_DefaultTemplate");
        }
    }

    /// <summary>
    /// Simplified New Semantic Model dialog for XrmToolBox
    /// </summary>
    public class NewSemanticModelDialogXrm : Form
    {
        private TextBox txtName = null!;
        private ComboBox cboConnectionType = null!;
        private ComboBox cboStorageMode = null!;
        private TextBox txtFabricEndpoint = null!;
        private TextBox txtFabricDatabase = null!;
        private Label lblFabricEndpoint = null!;
        private Label lblFabricDatabase = null!;
        private CheckBox chkUseDisplayNameAliases = null!;
        private TextBox txtFolder = null!;
        private TextBox txtTemplate = null!;
        private Button btnChangeFolder = null!;
        private Button btnChangeTemplate = null!;
        private Button btnCreate = null!;
        private Button btnCancelDlg = null!;
        private Label lblPreview = null!;
        private Label lblPreviewPath = null!;

        public string SemanticModelName { get; private set; } = "";
        public string ConnectionType { get; private set; } = "DataverseTDS";
        public string StorageMode { get; private set; } = "DirectQuery";
        public string FabricLinkSQLEndpoint { get; private set; } = "";
        public string FabricLinkSQLDatabase { get; private set; } = "";
        public string WorkingFolder { get; private set; } = "";
        public string TemplatePath { get; private set; } = "";
        public bool UseDisplayNameAliasesInSql { get; private set; } = true;

        public NewSemanticModelDialogXrm(string defaultFolder, string environmentUrl, string defaultTemplate)
        {
            WorkingFolder = defaultFolder;
            TemplatePath = defaultTemplate;
            InitializeComponent(environmentUrl);
            txtFolder.Text = defaultFolder;
            txtTemplate.Text = defaultTemplate;
        }

        private void InitializeComponent(string environmentUrl)
        {
            this.Text = "Create New Semantic Model";
            this.Size = new Size(520, 635);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Name field
            var lblName = new Label
            {
                Text = "Semantic Model Name:",
                Location = new Point(20, 20),
                AutoSize = true
            };

            txtName = new TextBox
            {
                Location = new Point(20, 45),
                Size = new Size(460, 23)
            };
            txtName.TextChanged += TxtName_TextChanged;

            // Environment URL
            var lblEnv = new Label
            {
                Text = $"Environment: {environmentUrl}",
                Location = new Point(20, 80),
                Size = new Size(460, 20),
                ForeColor = Color.Gray
            };

            // Working Folder - moved up
            var lblFolder = new Label
            {
                Text = "Working Folder:",
                Location = new Point(20, 110),
                AutoSize = true
            };

            txtFolder = new TextBox
            {
                Location = new Point(20, 135),
                Size = new Size(360, 23),
                ReadOnly = true,
                BackColor = SystemColors.Window
            };

            btnChangeFolder = new Button
            {
                Text = "Change...",
                Location = new Point(390, 134),
                Size = new Size(90, 25)
            };
            btnChangeFolder.Click += BtnChangeFolder_Click;

            // PBIP Template - moved up
            var lblTemplate = new Label
            {
                Text = "PBIP Template:",
                Location = new Point(20, 175),
                AutoSize = true
            };

            txtTemplate = new TextBox
            {
                Location = new Point(20, 200),
                Size = new Size(360, 23),
                ReadOnly = true,
                BackColor = SystemColors.Window
            };

            btnChangeTemplate = new Button
            {
                Text = "Change...",
                Location = new Point(390, 199),
                Size = new Size(90, 25)
            };
            btnChangeTemplate.Click += BtnChangeTemplate_Click;

            // Preview labels - moved up
            lblPreview = new Label
            {
                Text = "Will be created at:",
                Location = new Point(20, 240),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            lblPreviewPath = new Label
            {
                Text = "(enter a name)",
                Location = new Point(20, 258),
                Size = new Size(460, 20),
                ForeColor = Color.DarkBlue,
                AutoEllipsis = true
            };

            // Connection Type - moved down
            var lblConnectionType = new Label
            {
                Text = "Connection Type:",
                Location = new Point(20, 295),
                AutoSize = true
            };

            cboConnectionType = new ComboBox
            {
                Location = new Point(20, 320),
                Size = new Size(460, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboConnectionType.Items.AddRange(new object[] { "Dataverse TDS Endpoint", "FabricLink Lakehouse SQL" });
            cboConnectionType.SelectedIndex = 0;
            cboConnectionType.SelectedIndexChanged += CboConnectionType_SelectedIndexChanged_New;

            // Storage Mode
            var lblStorageModeNew = new Label
            {
                Text = "Storage Mode:",
                Location = new Point(20, 355),
                AutoSize = true
            };

            cboStorageMode = new ComboBox
            {
                Location = new Point(20, 375),
                Size = new Size(460, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboStorageMode.Items.AddRange(new object[] { "Direct Query", "Dual - All Dimensions", "Dual - Select Tables", "Import" });
            cboStorageMode.SelectedIndex = 0;

            // Fabric fields
            lblFabricEndpoint = new Label
            {
                Text = "FabricLink SQL Endpoint:",
                Location = new Point(20, 410),
                AutoSize = true,
                Visible = false
            };

            txtFabricEndpoint = new TextBox
            {
                Location = new Point(20, 435),
                Size = new Size(460, 23),
                Visible = false
            };

            lblFabricDatabase = new Label
            {
                Text = "FabricLink Lakehouse:",
                Location = new Point(20, 475),
                AutoSize = true,
                Visible = false
            };

            txtFabricDatabase = new TextBox
            {
                Location = new Point(20, 500),
                Size = new Size(460, 23),
                Visible = false
            };

            // Display Name Aliases checkbox
            chkUseDisplayNameAliases = new CheckBox
            {
                Text = "Use display name aliases in SQL queries",
                Location = new Point(20, 535),
                Size = new Size(460, 20),
                Checked = true
            };

            btnCreate = new Button
            {
                Text = "Create",
                Location = new Point(310, 565),
                Size = new Size(80, 28),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            btnCreate.Click += BtnCreate_Click;

            btnCancelDlg = new Button
            {
                Text = "Cancel",
                Location = new Point(400, 565),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblEnv);
            this.Controls.Add(lblFolder);
            this.Controls.Add(txtFolder);
            this.Controls.Add(btnChangeFolder);
            this.Controls.Add(lblTemplate);
            this.Controls.Add(txtTemplate);
            this.Controls.Add(btnChangeTemplate);
            this.Controls.Add(lblPreview);
            this.Controls.Add(lblPreviewPath);
            this.Controls.Add(lblConnectionType);
            this.Controls.Add(cboConnectionType);
            this.Controls.Add(lblStorageModeNew);
            this.Controls.Add(cboStorageMode);
            this.Controls.Add(lblFabricEndpoint);
            this.Controls.Add(txtFabricEndpoint);
            this.Controls.Add(lblFabricDatabase);
            this.Controls.Add(txtFabricDatabase);
            this.Controls.Add(chkUseDisplayNameAliases);
            this.Controls.Add(btnCreate);
            this.Controls.Add(btnCancelDlg);

            this.AcceptButton = btnCreate;
            this.CancelButton = btnCancelDlg;
        }

        private void CboConnectionType_SelectedIndexChanged_New(object sender, EventArgs e)
        {
            bool isFabricLink = cboConnectionType.SelectedIndex == 1;
            lblFabricEndpoint.Visible = isFabricLink;
            txtFabricEndpoint.Visible = isFabricLink;
            lblFabricDatabase.Visible = isFabricLink;
            txtFabricDatabase.Visible = isFabricLink;
        }

        private void TxtName_TextChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var name = txtName.Text.Trim();
            var folder = txtFolder.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                lblPreviewPath.Text = "(enter a name)";
                btnCreate.Enabled = false;
            }
            else if (string.IsNullOrEmpty(folder))
            {
                lblPreviewPath.Text = "(set a working folder)";
                btnCreate.Enabled = false;
            }
            else
            {
                var fullPath = Path.Combine(folder, name);
                lblPreviewPath.Text = fullPath;

                if (Directory.Exists(fullPath))
                {
                    lblPreviewPath.ForeColor = Color.Red;
                    lblPreview.Text = "Already exists:";
                    btnCreate.Enabled = false;
                }
                else
                {
                    lblPreviewPath.ForeColor = Color.DarkBlue;
                    lblPreview.Text = "Will be created at:";
                    btnCreate.Enabled = true;
                }
            }
        }

        private void BtnChangeFolder_Click(object sender, EventArgs e)
        {
            var selectedPath = VistaFolderPicker.ShowDialog(this, "Select Working Folder", txtFolder.Text);
            if (selectedPath != null)
            {
                txtFolder.Text = selectedPath;
                WorkingFolder = selectedPath;
                UpdatePreview();
            }
        }

        private void BtnChangeTemplate_Click(object sender, EventArgs e)
        {
            var selectedPath = VistaFolderPicker.ShowDialog(this, "Select PBIP Template Folder", txtTemplate.Text);
            if (selectedPath != null)
            {
                var pbipFiles = Directory.GetFiles(selectedPath, "*.pbip");
                if (pbipFiles.Length == 0)
                {
                    MessageBox.Show("Selected folder does not contain a valid PBIP template.",
                        "Invalid Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                txtTemplate.Text = selectedPath;
                TemplatePath = selectedPath;
            }
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            SemanticModelName = txtName.Text.Trim();
            ConnectionType = cboConnectionType.SelectedIndex == 1 ? "FabricLink" : "DataverseTDS";
            StorageMode = cboStorageMode.SelectedIndex switch
            {
                1 => "Dual",
                2 => "DualSelect",
                3 => "Import",
                _ => "DirectQuery"
            };
            FabricLinkSQLEndpoint = txtFabricEndpoint.Text.Trim();
            FabricLinkSQLDatabase = txtFabricDatabase.Text.Trim();
            WorkingFolder = txtFolder.Text.Trim();
            TemplatePath = txtTemplate.Text.Trim();
            UseDisplayNameAliasesInSql = chkUseDisplayNameAliases.Checked;

            if (string.IsNullOrWhiteSpace(SemanticModelName))
            {
                MessageBox.Show("Please enter a name for the semantic model.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Note: Don't create folder here - SemanticModelBuilder will create the proper
            // environment/model structure when Build is clicked
        }
    }
}
