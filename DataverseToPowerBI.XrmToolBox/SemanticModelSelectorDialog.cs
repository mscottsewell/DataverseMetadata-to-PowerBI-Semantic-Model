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
        private ListView listViewModels;
        private GroupBox groupDetails;
        private TextBox txtName;
        private TextBox txtEnvironmentUrl;
        private ComboBox cboConnectionType;
        private TextBox txtFabricEndpoint;
        private TextBox txtFabricDatabase;
        private Label lblFabricEndpoint;
        private Label lblFabricDatabase;
        private TextBox txtWorkingFolder;
        private TextBox txtTemplatePath;
        private Button btnChangeFolder;
        private Button btnChangeTemplate;
        private Button btnNew;
        private Button btnCopy;
        private Button btnRename;
        private Button btnDelete;
        private Button btnSelect;
        private Button btnCancel;
        private Label lblName;
        private Label lblEnvironmentUrl;
        private Label lblConnectionType;
        private Label lblWorkingFolder;
        private Label lblTemplatePath;

        private readonly SemanticModelManager _modelManager;
        private readonly string _currentEnvironmentUrl;
        private SemanticModelConfig _selectedModel;

        public SemanticModelConfig SelectedSemanticModel => _selectedModel;
        public bool ConfigurationsChanged { get; private set; } = false;
        public bool UrlWasChanged { get; private set; } = false;
        public string NewlyCreatedConfiguration { get; private set; }

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
            this.Size = new Size(935, 580);
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
                Size = new Size(450, 362),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false
            };
            listViewModels.Columns.Add("Name", 150);
            listViewModels.Columns.Add("Fact Table", 100);
            listViewModels.Columns.Add("Tables", 60);
            listViewModels.Columns.Add("Last Used", 140);
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

            // Right panel - configuration details
            groupDetails = new GroupBox
            {
                Text = "Configuration Details",
                Location = new Point(505, 15),
                Size = new Size(400, 465)
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

            // Connection Type - moved down below PBIP Template
            lblConnectionType = new Label
            {
                Text = "Connection Type:",
                Location = new Point(15, 265),
                AutoSize = true
            };

            cboConnectionType = new ComboBox
            {
                Location = new Point(15, 285),
                Size = new Size(365, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboConnectionType.Items.AddRange(new object[] { "Dataverse TDS Endpoint", "FabricLink Lakehouse SQL" });
            cboConnectionType.SelectedIndex = 0;
            cboConnectionType.SelectedIndexChanged += CboConnectionType_SelectedIndexChanged;

            // Fabric fields - below Connection Type
            lblFabricEndpoint = new Label
            {
                Text = "FabricLink SQL Endpoint:",
                Location = new Point(15, 325),
                AutoSize = true,
                Visible = false
            };

            txtFabricEndpoint = new TextBox
            {
                Location = new Point(15, 345),
                Size = new Size(365, 23),
                Visible = false
            };
            txtFabricEndpoint.TextChanged += TxtFabricEndpoint_TextChanged;

            lblFabricDatabase = new Label
            {
                Text = "FabricLink SQL Database:",
                Location = new Point(15, 385),
                AutoSize = true,
                Visible = false
            };

            txtFabricDatabase = new TextBox
            {
                Location = new Point(15, 405),
                Size = new Size(365, 23),
                Visible = false
            };
            txtFabricDatabase.TextChanged += TxtFabricDatabase_TextChanged;

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
            groupDetails.Controls.Add(lblConnectionType);
            groupDetails.Controls.Add(cboConnectionType);
            groupDetails.Controls.Add(lblFabricEndpoint);
            groupDetails.Controls.Add(txtFabricEndpoint);
            groupDetails.Controls.Add(lblFabricDatabase);
            groupDetails.Controls.Add(txtFabricDatabase);

            // Bottom buttons
            btnSelect = new Button
            {
                Text = "Select",
                Location = new Point(725, 500),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            btnSelect.Click += BtnSelect_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(825, 500),
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
                if (!string.IsNullOrEmpty(model.PluginSettings?.FactTable) && 
                    model.PluginSettings?.TableDisplayInfo?.ContainsKey(model.PluginSettings.FactTable) == true)
                {
                    factTableDisplay = model.PluginSettings.TableDisplayInfo[model.PluginSettings.FactTable].DisplayName ?? model.PluginSettings.FactTable;
                }
                item.SubItems.Add(factTableDisplay);
                
                // Tables count column
                var tableCount = model.PluginSettings?.SelectedTableNames?.Count ?? 0;
                item.SubItems.Add(tableCount.ToString());
                
                // Last Used column
                item.SubItems.Add(model.LastUsed.ToString("g"));
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
                    if (!string.IsNullOrEmpty(model.PluginSettings?.FactTable) && 
                        model.PluginSettings?.TableDisplayInfo?.ContainsKey(model.PluginSettings.FactTable) == true)
                    {
                        factTableDisplay = model.PluginSettings.TableDisplayInfo[model.PluginSettings.FactTable].DisplayName ?? model.PluginSettings.FactTable;
                    }
                    item.SubItems.Add(factTableDisplay);
                    
                    // Tables count column
                    var tableCount = model.PluginSettings?.SelectedTableNames?.Count ?? 0;
                    item.SubItems.Add(tableCount.ToString());
                    
                    // Last Used column
                    item.SubItems.Add(model.LastUsed.ToString("g"));
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
            
            // Set FabricLink fields
            txtFabricEndpoint.Text = model.FabricLinkSQLEndpoint ?? "";
            txtFabricDatabase.Text = model.FabricLinkSQLDatabase ?? "";
            UpdateFabricLinkFieldsVisibility();
            
            txtWorkingFolder.Text = model.WorkingFolder ?? "";
            txtTemplatePath.Text = model.TemplatePath ?? "";

            // Indicate if model is from different environment
            bool isCurrentEnv = NormalizeUrl(model.DataverseUrl) == _currentEnvironmentUrl;
            txtEnvironmentUrl.ForeColor = isCurrentEnv ? SystemColors.ControlText : Color.Orange;
        }

        private void ClearDetails()
        {
            txtName.Text = "";
            txtEnvironmentUrl.Text = "";
            cboConnectionType.SelectedIndex = 0;
            txtFabricEndpoint.Text = "";
            txtFabricDatabase.Text = "";
            UpdateFabricLinkFieldsVisibility();
            txtWorkingFolder.Text = "";
            txtTemplatePath.Text = "";
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
                            FabricLinkSQLEndpoint = dialog.FabricLinkSQLEndpoint,
                            FabricLinkSQLDatabase = dialog.FabricLinkSQLDatabase,
                            WorkingFolder = dialog.WorkingFolder,
                            TemplatePath = dialog.TemplatePath,
                            LastUsed = DateTime.Now,
                            CreatedDate = DateTime.Now
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

                        MessageBox.Show($"Semantic model '{dialog.SemanticModelName}' created successfully.",
                            "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void BtnChangeFolder_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Working Folder";
                dialog.SelectedPath = txtWorkingFolder.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtWorkingFolder.Text = dialog.SelectedPath;
                    _selectedModel.WorkingFolder = dialog.SelectedPath;
                    _modelManager.SaveModel(_selectedModel);
                    ConfigurationsChanged = true;
                }
            }
        }

        private void BtnChangeTemplate_Click(object sender, EventArgs e)
        {
            if (_selectedModel == null) return;

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select PBIP Template Folder";
                dialog.SelectedPath = txtTemplatePath.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Verify it's a valid template folder
                    var pbipFiles = Directory.GetFiles(dialog.SelectedPath, "*.pbip");
                    if (pbipFiles.Length == 0)
                    {
                        MessageBox.Show("Selected folder does not contain a valid PBIP template (.pbip file not found).",
                            "Invalid Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    txtTemplatePath.Text = dialog.SelectedPath;
                    _selectedModel.TemplatePath = dialog.SelectedPath;
                    _modelManager.SaveModel(_selectedModel);
                    ConfigurationsChanged = true;
                }
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
        private TextBox txtName;
        private ComboBox cboConnectionType;
        private TextBox txtFabricEndpoint;
        private TextBox txtFabricDatabase;
        private Label lblFabricEndpoint;
        private Label lblFabricDatabase;
        private TextBox txtFolder;
        private TextBox txtTemplate;
        private Button btnChangeFolder;
        private Button btnChangeTemplate;
        private Button btnCreate;
        private Button btnCancelDlg;
        private Label lblPreview;
        private Label lblPreviewPath;

        public string SemanticModelName { get; private set; } = "";
        public string ConnectionType { get; private set; } = "DataverseTDS";
        public string FabricLinkSQLEndpoint { get; private set; } = "";
        public string FabricLinkSQLDatabase { get; private set; } = "";
        public string WorkingFolder { get; private set; } = "";
        public string TemplatePath { get; private set; } = "";

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
            this.Size = new Size(520, 560);
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
            cboConnectionType.SelectedIndexChanged += CboConnectionType_SelectedIndexChanged;

            // Fabric fields - moved down
            lblFabricEndpoint = new Label
            {
                Text = "FabricLink SQL Endpoint:",
                Location = new Point(20, 360),
                AutoSize = true,
                Visible = false
            };

            txtFabricEndpoint = new TextBox
            {
                Location = new Point(20, 385),
                Size = new Size(460, 23),
                Visible = false
            };

            lblFabricDatabase = new Label
            {
                Text = "FabricLink SQL Database:",
                Location = new Point(20, 425),
                AutoSize = true,
                Visible = false
            };

            txtFabricDatabase = new TextBox
            {
                Location = new Point(20, 450),
                Size = new Size(460, 23),
                Visible = false
            };

            btnCreate = new Button
            {
                Text = "Create",
                Location = new Point(310, 485),
                Size = new Size(80, 28),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            btnCreate.Click += BtnCreate_Click;

            btnCancelDlg = new Button
            {
                Text = "Cancel",
                Location = new Point(400, 485),
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
            this.Controls.Add(lblFabricEndpoint);
            this.Controls.Add(txtFabricEndpoint);
            this.Controls.Add(lblFabricDatabase);
            this.Controls.Add(txtFabricDatabase);
            this.Controls.Add(btnCreate);
            this.Controls.Add(btnCancelDlg);

            this.AcceptButton = btnCreate;
            this.CancelButton = btnCancelDlg;
        }

        private void CboConnectionType_SelectedIndexChanged(object sender, EventArgs e)
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
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Working Folder";
                dialog.SelectedPath = txtFolder.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtFolder.Text = dialog.SelectedPath;
                    WorkingFolder = dialog.SelectedPath;
                    UpdatePreview();
                }
            }
        }

        private void BtnChangeTemplate_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select PBIP Template Folder";
                dialog.SelectedPath = txtTemplate.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var pbipFiles = Directory.GetFiles(dialog.SelectedPath, "*.pbip");
                    if (pbipFiles.Length == 0)
                    {
                        MessageBox.Show("Selected folder does not contain a valid PBIP template.",
                            "Invalid Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    txtTemplate.Text = dialog.SelectedPath;
                    TemplatePath = dialog.SelectedPath;
                }
            }
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            SemanticModelName = txtName.Text.Trim();
            ConnectionType = cboConnectionType.SelectedIndex == 1 ? "FabricLink" : "DataverseTDS";
            FabricLinkSQLEndpoint = txtFabricEndpoint.Text.Trim();
            FabricLinkSQLDatabase = txtFabricDatabase.Text.Trim();
            WorkingFolder = txtFolder.Text.Trim();
            TemplatePath = txtTemplate.Text.Trim();

            if (string.IsNullOrWhiteSpace(SemanticModelName))
            {
                MessageBox.Show("Please enter a name for the semantic model.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Create the folder structure
            try
            {
                var fullPath = Path.Combine(WorkingFolder, SemanticModelName);
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating folder:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
            }
        }
    }
}
