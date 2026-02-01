// =============================================================================
// SemanticModelSettingsDialog.cs - Configuration Management Dialog
// =============================================================================
// Purpose: Manages multiple semantic model configurations.
//
// This dialog provides CRUD operations for configurations:
//   - View all available configurations in a list
//   - Edit configuration details (URL, connection type, working folder)
//   - Create new configurations
//   - Copy existing configurations (with all settings and cache)
//   - Rename configurations 
//   - Delete configurations (keeps PBIP files on disk)
//
// Connection Types Supported:
//   - Dataverse TDS Endpoint: Native TDS connector
//   - FabricLink Lakehouse SQL: Azure Synapse Link with Fabric
//
// Note: Changes are saved immediately when editing fields.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Models;
using DataverseToPowerBI.Configurator.Services;

namespace DataverseToPowerBI.Configurator.Forms
{
    /// <summary>
    /// Dialog for managing multiple semantic model configurations.
    /// </summary>
    /// <remarks>
    /// Allows users to view, edit, create, copy, rename, and delete
    /// configurations. Each configuration represents a separate
    /// Power BI semantic model setup with its own tables and settings.
    /// </remarks>
    public class SemanticModelSettingsDialog : Form
    {
        private ListBox lstConfigurations = null!;
        private TextBox txtName = null!;
        private TextBox txtEnvironmentUrl = null!;
        private TextBox txtWorkingFolder = null!;
        private ComboBox cboConnectionType = null!;
        private TextBox txtFabricEndpoint = null!;
        private TextBox txtFabricDatabase = null!;
        private Label lblFabricEndpoint = null!;
        private Label lblFabricDatabase = null!;
        private Button btnChangeFolder = null!;
        private Button btnNew = null!;
        private Button btnCopy = null!;
        private Button btnRename = null!;
        private Button btnDelete = null!;
        private Button btnClose = null!;
        private Label lblConfigurations = null!;
        private Label lblName = null!;
        private Label lblEnvironmentUrl = null!;
        private Label lblConnectionType = null!;
        private Label lblWorkingFolder = null!;
        private GroupBox groupDetails = null!;

        private readonly SettingsManager _settingsManager;
        private ConfigurationEntry? _selectedConfig;
        public bool ConfigurationsChanged { get; private set; } = false;
        public string? NewlyCreatedConfiguration { get; private set; }

        public SemanticModelSettingsDialog(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            InitializeComponent();
            LoadConfigurations();
        }

        private void InitializeComponent()
        {
            this.Text = "Manage Semantic Model Configurations";
            this.Size = new System.Drawing.Size(700, 550);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Left panel - configuration list
            lblConfigurations = new Label
            {
                Text = "Available Configurations:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };

            lstConfigurations = new ListBox
            {
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(250, 330)
            };
            lstConfigurations.SelectedIndexChanged += LstConfigurations_SelectedIndexChanged;

            btnNew = new Button
            {
                Text = "New...",
                Location = new System.Drawing.Point(20, 385),
                Size = new System.Drawing.Size(60, 28)
            };
            btnNew.Click += BtnNew_Click;

            btnCopy = new Button
            {
                Text = "Copy...",
                Location = new System.Drawing.Point(90, 385),
                Size = new System.Drawing.Size(65, 28),
                Enabled = false
            };
            btnCopy.Click += BtnCopy_Click;

            btnRename = new Button
            {
                Text = "Rename...",
                Location = new System.Drawing.Point(165, 385),
                Size = new System.Drawing.Size(75, 28),
                Enabled = false
            };
            btnRename.Click += BtnRename_Click;

            btnDelete = new Button
            {
                Text = "Delete",
                Location = new System.Drawing.Point(250, 385),
                Size = new System.Drawing.Size(60, 28),
                Enabled = false
            };
            btnDelete.Click += BtnDelete_Click;

            // Right panel - configuration details
            groupDetails = new GroupBox
            {
                Text = "Configuration Details",
                Location = new System.Drawing.Point(290, 20),
                Size = new System.Drawing.Size(380, 440)
            };

            lblName = new Label
            {
                Text = "Name:",
                Location = new System.Drawing.Point(15, 30),
                AutoSize = true
            };

            txtName = new TextBox
            {
                Location = new System.Drawing.Point(15, 55),
                Size = new System.Drawing.Size(350, 23),
                ReadOnly = true
            };

            lblConnectionType = new Label
            {
                Text = "Connection Type:",
                Location = new System.Drawing.Point(15, 95),
                AutoSize = true
            };

            cboConnectionType = new ComboBox
            {
                Location = new System.Drawing.Point(15, 120),
                Size = new System.Drawing.Size(350, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboConnectionType.Items.AddRange(new object[] { "Dataverse TDS Endpoint", "FabricLink Lakehouse SQL" });
            cboConnectionType.SelectedIndex = 0;
            cboConnectionType.SelectedIndexChanged += CboConnectionType_SelectedIndexChanged;

            lblEnvironmentUrl = new Label
            {
                Text = "Dataverse Environment URL:",
                Location = new System.Drawing.Point(15, 160),
                AutoSize = true
            };

            txtEnvironmentUrl = new TextBox
            {
                Location = new System.Drawing.Point(15, 185),
                Size = new System.Drawing.Size(350, 23)
            };
            txtEnvironmentUrl.TextChanged += ConfigDetail_Changed;

            lblFabricEndpoint = new Label
            {
                Text = "FabricLink SQL Endpoint:",
                Location = new System.Drawing.Point(15, 225),
                AutoSize = true,
                Visible = false
            };

            txtFabricEndpoint = new TextBox
            {
                Location = new System.Drawing.Point(15, 250),
                Size = new System.Drawing.Size(350, 23),
                PlaceholderText = "e.g., xxxxx.msit-datawarehouse.fabric.microsoft.com",
                Visible = false
            };
            txtFabricEndpoint.TextChanged += ConfigDetail_Changed;

            lblFabricDatabase = new Label
            {
                Text = "FabricLink SQL Database:",
                Location = new System.Drawing.Point(15, 290),
                AutoSize = true,
                Visible = false
            };

            txtFabricDatabase = new TextBox
            {
                Location = new System.Drawing.Point(15, 315),
                Size = new System.Drawing.Size(350, 23),
                PlaceholderText = "e.g., dataverse_xxx_workspace_xxx",
                Visible = false
            };
            txtFabricDatabase.TextChanged += ConfigDetail_Changed;

            lblWorkingFolder = new Label
            {
                Text = "Working Folder:",
                Location = new System.Drawing.Point(15, 355),
                AutoSize = true
            };

            txtWorkingFolder = new TextBox
            {
                Location = new System.Drawing.Point(15, 380),
                Size = new System.Drawing.Size(265, 23),
                ReadOnly = true,
                BackColor = System.Drawing.SystemColors.Window
            };

            btnChangeFolder = new Button
            {
                Text = "Change...",
                Location = new System.Drawing.Point(290, 379),
                Size = new System.Drawing.Size(75, 25)
            };
            btnChangeFolder.Click += BtnChangeFolder_Click;

            groupDetails.Controls.Add(lblName);
            groupDetails.Controls.Add(txtName);
            groupDetails.Controls.Add(lblConnectionType);
            groupDetails.Controls.Add(cboConnectionType);
            groupDetails.Controls.Add(lblEnvironmentUrl);
            groupDetails.Controls.Add(txtEnvironmentUrl);
            groupDetails.Controls.Add(lblFabricEndpoint);
            groupDetails.Controls.Add(txtFabricEndpoint);
            groupDetails.Controls.Add(lblFabricDatabase);
            groupDetails.Controls.Add(txtFabricDatabase);
            groupDetails.Controls.Add(lblWorkingFolder);
            groupDetails.Controls.Add(txtWorkingFolder);
            groupDetails.Controls.Add(btnChangeFolder);

            btnClose = new Button
            {
                Text = "Close",
                Location = new System.Drawing.Point(595, 465),
                Size = new System.Drawing.Size(75, 28),
                DialogResult = DialogResult.OK
            };

            this.Controls.Add(lblConfigurations);
            this.Controls.Add(lstConfigurations);
            this.Controls.Add(btnNew);
            this.Controls.Add(btnCopy);
            this.Controls.Add(btnRename);
            this.Controls.Add(btnDelete);
            this.Controls.Add(groupDetails);
            this.Controls.Add(btnClose);

            this.AcceptButton = btnClose;
        }

        private void LoadConfigurations()
        {
            lstConfigurations.Items.Clear();
            
            var configNames = _settingsManager.GetConfigurationNames();
            var currentConfig = _settingsManager.GetCurrentConfigurationName();

            foreach (var name in configNames.OrderBy(n => n))
            {
                lstConfigurations.Items.Add(name);
            }

            // Select current configuration
            var index = lstConfigurations.Items.IndexOf(currentConfig);
            if (index >= 0)
            {
                lstConfigurations.SelectedIndex = index;
            }
            else if (lstConfigurations.Items.Count > 0)
            {
                lstConfigurations.SelectedIndex = 0;
            }
        }

        private void LstConfigurations_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (lstConfigurations.SelectedItem == null)
            {
                _selectedConfig = null;
                ClearDetails();
                btnCopy.Enabled = false;
                btnRename.Enabled = false;
                btnDelete.Enabled = false;
                return;
            }

            var configName = lstConfigurations.SelectedItem.ToString()!;
            try
            {
                var settings = _settingsManager.GetConfiguration(configName);
                _selectedConfig = new ConfigurationEntry
                {
                    Name = configName,
                    Settings = settings
                };

                DisplayDetails();
                btnCopy.Enabled = true;
                btnRename.Enabled = true;
                btnDelete.Enabled = lstConfigurations.Items.Count > 1; // Can't delete last config
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayDetails()
        {
            if (_selectedConfig == null) return;

            txtName.Text = _selectedConfig.Name;
            txtEnvironmentUrl.Text = _selectedConfig.Settings.LastEnvironmentUrl ?? "";
            txtWorkingFolder.Text = _selectedConfig.Settings.OutputFolder ?? "";
            
            // Set connection type
            var connectionType = _selectedConfig.Settings.ConnectionType ?? "DataverseTDS";
            cboConnectionType.SelectedIndex = connectionType == "FabricLink" ? 1 : 0;
            
            // Set FabricLink fields
            txtFabricEndpoint.Text = _selectedConfig.Settings.FabricLinkSQLEndpoint ?? "";
            txtFabricDatabase.Text = _selectedConfig.Settings.FabricLinkSQLDatabase ?? "";
            
            UpdateFabricLinkFieldsVisibility();
        }

        private void ClearDetails()
        {
            txtName.Text = "";
            txtEnvironmentUrl.Text = "";
            txtWorkingFolder.Text = "";
            cboConnectionType.SelectedIndex = 0;
            txtFabricEndpoint.Text = "";
            txtFabricDatabase.Text = "";
            UpdateFabricLinkFieldsVisibility();
        }

        private void CboConnectionType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateFabricLinkFieldsVisibility();
            ConfigDetail_Changed(sender, e);
        }

        private void UpdateFabricLinkFieldsVisibility()
        {
            bool isFabricLink = cboConnectionType.SelectedIndex == 1;
            lblFabricEndpoint.Visible = isFabricLink;
            txtFabricEndpoint.Visible = isFabricLink;
            lblFabricDatabase.Visible = isFabricLink;
            txtFabricDatabase.Visible = isFabricLink;
        }

        private void ConfigDetail_Changed(object? sender, EventArgs e)
        {
            if (_selectedConfig == null) return;

            // Save changes immediately
            try
            {
                var url = txtEnvironmentUrl.Text.Trim();
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = url.Substring(8);

                _selectedConfig.Settings.LastEnvironmentUrl = url;
                _selectedConfig.Settings.ConnectionType = cboConnectionType.SelectedIndex == 1 ? "FabricLink" : "DataverseTDS";
                _selectedConfig.Settings.FabricLinkSQLEndpoint = txtFabricEndpoint.Text.Trim();
                _selectedConfig.Settings.FabricLinkSQLDatabase = txtFabricDatabase.Text.Trim();
                
                _settingsManager.SaveSettings(_selectedConfig.Settings);
                ConfigurationsChanged = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving changes:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnChangeFolder_Click(object? sender, EventArgs e)
        {
            if (_selectedConfig == null) return;

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Working Folder",
                SelectedPath = txtWorkingFolder.Text
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                txtWorkingFolder.Text = dialog.SelectedPath;
                _selectedConfig.Settings.OutputFolder = dialog.SelectedPath;
                
                try
                {
                    _settingsManager.SaveSettings(_selectedConfig.Settings);
                    ConfigurationsChanged = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving changes:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            var defaultFolder = txtWorkingFolder.Text;
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
                    // Check if configuration already exists
                    var existingConfigs = _settingsManager.GetConfigurationNames();
                    if (existingConfigs.Contains(modelName))
                    {
                        MessageBox.Show($"A configuration named '{modelName}' already exists.", "Duplicate Name",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Create the folder structure
                    var modelFolder = Path.Combine(folder, modelName);
                    Directory.CreateDirectory(modelFolder);

                    // Create new configuration
                    var newSettings = new AppSettings
                    {
                        ProjectName = modelName,
                        OutputFolder = folder,
                        LastEnvironmentUrl = envUrl,
                        ConnectionType = dialog.ConnectionType,
                        FabricLinkSQLEndpoint = dialog.FabricLinkSQLEndpoint,
                        FabricLinkSQLDatabase = dialog.FabricLinkSQLDatabase
                    };
                    _settingsManager.CreateNewConfiguration(modelName, newSettings);

                    ConfigurationsChanged = true;
                    LoadConfigurations();

                    // Select the new configuration
                    var index = lstConfigurations.Items.IndexOf(modelName);
                    if (index >= 0)
                    {
                        lstConfigurations.SelectedIndex = index;
                    }

                    // Track this as newly created for auto-setup
                    NewlyCreatedConfiguration = modelName;

                    MessageBox.Show($"Configuration '{modelName}' created successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating configuration:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnCopy_Click(object? sender, EventArgs e)
        {
            if (_selectedConfig == null) return;

            using var dialog = new Form
            {
                Text = "Copy Configuration",
                Size = new System.Drawing.Size(400, 150),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent
            };

            var lblNewName = new Label
            {
                Text = "New Configuration Name:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };

            var txtNewName = new TextBox
            {
                Text = _selectedConfig.Name + " - Copy",
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(340, 23)
            };

            var btnOk = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(200, 80),
                Size = new System.Drawing.Size(75, 28),
                DialogResult = DialogResult.OK
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(285, 80),
                Size = new System.Drawing.Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            dialog.Controls.Add(lblNewName);
            dialog.Controls.Add(txtNewName);
            dialog.Controls.Add(btnOk);
            dialog.Controls.Add(btnCancel);
            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var newName = txtNewName.Text.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show("Please enter a valid name.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    // Check if name already exists
                    var existingConfigs = _settingsManager.GetConfigurationNames();
                    if (existingConfigs.Contains(newName))
                    {
                        MessageBox.Show($"A configuration named '{newName}' already exists.", "Duplicate Name",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Create a copy of the current settings
                    var copiedSettings = new AppSettings
                    {
                        ProjectName = newName,
                        LastEnvironmentUrl = _selectedConfig.Settings.LastEnvironmentUrl,
                        LastSolution = _selectedConfig.Settings.LastSolution,
                        OutputFolder = _selectedConfig.Settings.OutputFolder,
                        ConnectionType = _selectedConfig.Settings.ConnectionType,
                        FabricLinkSQLEndpoint = _selectedConfig.Settings.FabricLinkSQLEndpoint,
                        FabricLinkSQLDatabase = _selectedConfig.Settings.FabricLinkSQLDatabase,
                        SelectedTables = new List<string>(_selectedConfig.Settings.SelectedTables),
                        TableForms = new Dictionary<string, string>(_selectedConfig.Settings.TableForms),
                        TableFormNames = new Dictionary<string, string>(_selectedConfig.Settings.TableFormNames),
                        TableViews = new Dictionary<string, string>(_selectedConfig.Settings.TableViews),
                        TableViewNames = new Dictionary<string, string>(_selectedConfig.Settings.TableViewNames),
                        TableAttributes = _selectedConfig.Settings.TableAttributes.ToDictionary(
                            k => k.Key, 
                            v => new List<string>(v.Value)),
                        TableDisplayInfo = _selectedConfig.Settings.TableDisplayInfo.ToDictionary(
                            k => k.Key,
                            v => new TableDisplayInfo
                            {
                                LogicalName = v.Value.LogicalName,
                                DisplayName = v.Value.DisplayName,
                                SchemaName = v.Value.SchemaName,
                                PrimaryIdAttribute = v.Value.PrimaryIdAttribute,
                                PrimaryNameAttribute = v.Value.PrimaryNameAttribute
                            }),
                        AttributeDisplayInfo = _selectedConfig.Settings.AttributeDisplayInfo.ToDictionary(
                            k => k.Key,
                            v => v.Value.ToDictionary(
                                ak => ak.Key,
                                av => new AttributeDisplayInfo
                                {
                                    LogicalName = av.Value.LogicalName,
                                    DisplayName = av.Value.DisplayName,
                                    SchemaName = av.Value.SchemaName,
                                    AttributeType = av.Value.AttributeType,
                                    IsRequired = av.Value.IsRequired,
                                    Targets = av.Value.Targets != null ? new List<string>(av.Value.Targets) : null
                                })),
                        WindowGeometry = _selectedConfig.Settings.WindowGeometry,
                        AutoloadCache = _selectedConfig.Settings.AutoloadCache,
                        ShowAllAttributes = _selectedConfig.Settings.ShowAllAttributes,
                        FactTable = _selectedConfig.Settings.FactTable,
                        TableRoles = new Dictionary<string, TableRole>(_selectedConfig.Settings.TableRoles),
                        Relationships = _selectedConfig.Settings.Relationships?.Select(r => new RelationshipConfig
                        {
                            SourceTable = r.SourceTable,
                            SourceAttribute = r.SourceAttribute,
                            TargetTable = r.TargetTable,
                            DisplayName = r.DisplayName,
                            IsActive = r.IsActive,
                            IsSnowflake = r.IsSnowflake,
                            IsReverse = r.IsReverse,
                            AssumeReferentialIntegrity = r.AssumeReferentialIntegrity
                        }).ToList() ?? new List<RelationshipConfig>(),
                        DateTableConfig = _selectedConfig.Settings.DateTableConfig != null ? new DateTableConfig
                        {
                            PrimaryDateTable = _selectedConfig.Settings.DateTableConfig.PrimaryDateTable,
                            PrimaryDateField = _selectedConfig.Settings.DateTableConfig.PrimaryDateField,
                            TimeZoneId = _selectedConfig.Settings.DateTableConfig.TimeZoneId,
                            UtcOffsetHours = _selectedConfig.Settings.DateTableConfig.UtcOffsetHours,
                            StartYear = _selectedConfig.Settings.DateTableConfig.StartYear,
                            EndYear = _selectedConfig.Settings.DateTableConfig.EndYear,
                            WrappedFields = _selectedConfig.Settings.DateTableConfig.WrappedFields?.Select(f => new DateTimeFieldConfig
                            {
                                TableName = f.TableName,
                                FieldName = f.FieldName,
                                ConvertToDateOnly = f.ConvertToDateOnly
                            }).ToList() ?? new List<DateTimeFieldConfig>()
                        } : null
                    };

                    // Create the new configuration
                    _settingsManager.CreateNewConfiguration(newName, copiedSettings);

                    // Copy the cache file if it exists
                    var sourceCache = _settingsManager.LoadCache(_selectedConfig.Name);
                    if (sourceCache != null)
                    {
                        _settingsManager.SaveCache(sourceCache, newName);
                    }

                    ConfigurationsChanged = true;
                    LoadConfigurations();

                    // Select the new configuration
                    var index = lstConfigurations.Items.IndexOf(newName);
                    if (index >= 0)
                    {
                        lstConfigurations.SelectedIndex = index;
                    }

                    MessageBox.Show($"Configuration copied to '{newName}'.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error copying configuration:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnRename_Click(object? sender, EventArgs e)
        {
            if (_selectedConfig == null) return;

            using var dialog = new Form
            {
                Text = "Rename Configuration",
                Size = new System.Drawing.Size(400, 150),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent
            };

            var lblNewName = new Label
            {
                Text = "New Name:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };

            var txtNewName = new TextBox
            {
                Text = _selectedConfig.Name,
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(340, 23)
            };

            var btnOk = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(200, 80),
                Size = new System.Drawing.Size(75, 28),
                DialogResult = DialogResult.OK
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(285, 80),
                Size = new System.Drawing.Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            dialog.Controls.Add(lblNewName);
            dialog.Controls.Add(txtNewName);
            dialog.Controls.Add(btnOk);
            dialog.Controls.Add(btnCancel);
            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var newName = txtNewName.Text.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show("Please enter a valid name.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (newName == _selectedConfig.Name)
                {
                    return; // No change
                }

                try
                {
                    _settingsManager.RenameConfiguration(_selectedConfig.Name, newName);
                    ConfigurationsChanged = true;
                    LoadConfigurations();

                    // Select the renamed configuration
                    var index = lstConfigurations.Items.IndexOf(newName);
                    if (index >= 0)
                    {
                        lstConfigurations.SelectedIndex = index;
                    }

                    MessageBox.Show($"Configuration renamed to '{newName}'.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error renaming configuration:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedConfig == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the configuration '{_selectedConfig.Name}'?\n\n" +
                "This will remove the configuration but will NOT delete any PBIP files or folders.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    _settingsManager.DeleteConfiguration(_selectedConfig.Name);
                    ConfigurationsChanged = true;
                    LoadConfigurations();

                    MessageBox.Show($"Configuration '{_selectedConfig.Name}' deleted.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting configuration:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
