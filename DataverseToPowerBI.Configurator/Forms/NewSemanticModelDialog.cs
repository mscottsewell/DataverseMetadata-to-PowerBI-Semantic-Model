// =============================================================================
// NewSemanticModelDialog.cs - Create New Semantic Model Dialog
// =============================================================================
// Purpose: Dialog for creating a new Power BI semantic model configuration.
//
// This dialog collects all required information to create a new semantic model:
//   - Semantic model name (becomes folder name and configuration name)
//   - Dataverse environment URL
//   - Connection type (Dataverse TDS or FabricLink)
//   - Working folder location
//   - PBIP template folder path
//
// Connection Types:
//   - Dataverse TDS Endpoint: Native TDS connector for Dataverse
//   - FabricLink Lakehouse SQL: Azure Synapse Link with Fabric Lakehouse
//
// Output:
//   Properties are populated on OK: SemanticModelName, EnvironmentUrl,
//   ConnectionType, WorkingFolder, TemplatePath, etc.
// =============================================================================

using System;
using System.IO;
using System.Windows.Forms;

namespace DataverseToPowerBI.Configurator.Forms
{
    /// <summary>
    /// Dialog for creating a new Power BI semantic model configuration.
    /// </summary>
    /// <remarks>
    /// Collects semantic model name, environment URL, connection type,
    /// and output folder. Validates inputs and shows preview path.
    /// </remarks>
    public class NewSemanticModelDialog : Form
    {
        private TextBox txtName = null!;
        private TextBox txtEnvironmentUrl = null!;
        private ComboBox cboConnectionType = null!;
        private TextBox txtFabricEndpoint = null!;
        private TextBox txtFabricDatabase = null!;
        private Label lblFabricEndpoint = null!;
        private Label lblFabricDatabase = null!;
        private TextBox txtFolder = null!;
        private TextBox txtTemplate = null!;
        private Button btnChangeFolder = null!;
        private Button btnChangeTemplate = null!;
        private Button btnCreate = null!;
        private Button btnCancel = null!;
        private Label lblName = null!;
        private Label lblEnvironmentUrl = null!;
        private Label lblConnectionType = null!;
        private Label lblFolder = null!;
        private Label lblTemplate = null!;
        private Label lblPreview = null!;
        private Label lblPreviewPath = null!;

        public string SemanticModelName { get; private set; } = "";
        public string EnvironmentUrl { get; private set; } = "";
        public string ConnectionType { get; private set; } = "DataverseTDS";
        public string FabricLinkSQLEndpoint { get; private set; } = "";
        public string FabricLinkSQLDatabase { get; private set; } = "";
        public string WorkingFolder { get; private set; } = "";
        public string TemplatePath { get; private set; } = "";
        public bool WorkingFolderChanged { get; private set; } = false;
        public bool TemplateChanged { get; private set; } = false;

        public NewSemanticModelDialog(string currentWorkingFolder, string currentEnvironmentUrl = "", string currentTemplatePath = "")
        {
            WorkingFolder = currentWorkingFolder;
            
            // Set default template path
            if (string.IsNullOrEmpty(currentTemplatePath))
            {
                var baseDir = Path.GetDirectoryName(Application.ExecutablePath);
                TemplatePath = Path.Combine(baseDir ?? "", "..", "..", "..", "..", "PBIP_DefaultTemplate");
                TemplatePath = Path.GetFullPath(TemplatePath);
            }
            else
            {
                TemplatePath = currentTemplatePath;
            }
            
            InitializeComponent();
            txtFolder.Text = currentWorkingFolder;
            txtEnvironmentUrl.Text = currentEnvironmentUrl;
            txtTemplate.Text = TemplatePath;
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            this.Text = "Create New Semantic Model";
            this.Size = new System.Drawing.Size(550, 550);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            lblName = new Label
            {
                Text = "Semantic Model Name:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };

            txtName = new TextBox
            {
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(490, 23)
            };
            txtName.TextChanged += TxtName_TextChanged;

            lblEnvironmentUrl = new Label
            {
                Text = "Dataverse Environment URL:",
                Location = new System.Drawing.Point(20, 80),
                AutoSize = true
            };

            txtEnvironmentUrl = new TextBox
            {
                Location = new System.Drawing.Point(20, 105),
                Size = new System.Drawing.Size(490, 23),
                PlaceholderText = "e.g., yourorg.crm.dynamics.com"
            };

            lblConnectionType = new Label
            {
                Text = "Connection Type:",
                Location = new System.Drawing.Point(20, 145),
                AutoSize = true
            };

            cboConnectionType = new ComboBox
            {
                Location = new System.Drawing.Point(20, 170),
                Size = new System.Drawing.Size(490, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboConnectionType.Items.AddRange(new object[] { "Dataverse TDS Endpoint", "FabricLink Lakehouse SQL" });
            cboConnectionType.SelectedIndex = 0;
            cboConnectionType.SelectedIndexChanged += CboConnectionType_SelectedIndexChanged;

            lblFabricEndpoint = new Label
            {
                Text = "FabricLink SQL Endpoint:",
                Location = new System.Drawing.Point(20, 210),
                AutoSize = true,
                Visible = false
            };

            txtFabricEndpoint = new TextBox
            {
                Location = new System.Drawing.Point(20, 235),
                Size = new System.Drawing.Size(490, 23),
                PlaceholderText = "e.g., xxxxx.msit-datawarehouse.fabric.microsoft.com",
                Visible = false
            };

            lblFabricDatabase = new Label
            {
                Text = "FabricLink SQL Database:",
                Location = new System.Drawing.Point(20, 275),
                AutoSize = true,
                Visible = false
            };

            txtFabricDatabase = new TextBox
            {
                Location = new System.Drawing.Point(20, 300),
                Size = new System.Drawing.Size(490, 23),
                PlaceholderText = "e.g., dataverse_xxx_workspace_xxx",
                Visible = false
            };

            lblFolder = new Label
            {
                Text = "Working Folder:",
                Location = new System.Drawing.Point(20, 340),
                AutoSize = true
            };

            txtFolder = new TextBox
            {
                Location = new System.Drawing.Point(20, 365),
                Size = new System.Drawing.Size(380, 23),
                ReadOnly = true,
                BackColor = System.Drawing.SystemColors.Window
            };

            btnChangeFolder = new Button
            {
                Text = "Change...",
                Location = new System.Drawing.Point(410, 364),
                Size = new System.Drawing.Size(100, 25)
            };
            btnChangeFolder.Click += BtnChangeFolder_Click;

            lblTemplate = new Label
            {
                Text = "PBIP Template:",
                Location = new System.Drawing.Point(20, 405),
                AutoSize = true
            };

            txtTemplate = new TextBox
            {
                Location = new System.Drawing.Point(20, 430),
                Size = new System.Drawing.Size(380, 23),
                ReadOnly = true,
                BackColor = System.Drawing.SystemColors.Window
            };

            btnChangeTemplate = new Button
            {
                Text = "Change...",
                Location = new System.Drawing.Point(410, 429),
                Size = new System.Drawing.Size(100, 25)
            };
            btnChangeTemplate.Click += BtnChangeTemplate_Click;

            lblPreview = new Label
            {
                Text = "Will be created at:",
                Location = new System.Drawing.Point(20, 465),
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray
            };

            lblPreviewPath = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(20, 483),
                Size = new System.Drawing.Size(490, 20),
                ForeColor = System.Drawing.Color.DarkBlue,
                AutoEllipsis = true
            };

            btnCreate = new Button
            {
                Text = "Create",
                Location = new System.Drawing.Point(340, 515),
                Size = new System.Drawing.Size(80, 28),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            btnCreate.Click += BtnCreate_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(430, 515),
                Size = new System.Drawing.Size(80, 28),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblEnvironmentUrl);
            this.Controls.Add(txtEnvironmentUrl);
            this.Controls.Add(lblConnectionType);
            this.Controls.Add(cboConnectionType);
            this.Controls.Add(lblFabricEndpoint);
            this.Controls.Add(txtFabricEndpoint);
            this.Controls.Add(lblFabricDatabase);
            this.Controls.Add(txtFabricDatabase);
            this.Controls.Add(lblFolder);
            this.Controls.Add(txtFolder);
            this.Controls.Add(btnChangeFolder);
            this.Controls.Add(lblTemplate);
            this.Controls.Add(txtTemplate);
            this.Controls.Add(btnChangeTemplate);
            this.Controls.Add(lblPreview);
            this.Controls.Add(lblPreviewPath);
            this.Controls.Add(btnCreate);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnCreate;
            this.CancelButton = btnCancel;
        }

        private void CboConnectionType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            bool isFabricLink = cboConnectionType.SelectedIndex == 1;
            lblFabricEndpoint.Visible = isFabricLink;
            txtFabricEndpoint.Visible = isFabricLink;
            lblFabricDatabase.Visible = isFabricLink;
            txtFabricDatabase.Visible = isFabricLink;
        }

        private void TxtName_TextChanged(object? sender, EventArgs e)
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
                
                // Check if already exists
                if (Directory.Exists(fullPath))
                {
                    lblPreviewPath.ForeColor = System.Drawing.Color.Red;
                    lblPreview.Text = "Already exists:";
                    btnCreate.Enabled = false;
                }
                else
                {
                    lblPreviewPath.ForeColor = System.Drawing.Color.DarkBlue;
                    lblPreview.Text = "Will be created at:";
                    btnCreate.Enabled = true;
                }
            }
        }

        private void BtnChangeFolder_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Working Folder for Semantic Models",
                SelectedPath = txtFolder.Text
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                txtFolder.Text = dialog.SelectedPath;
                WorkingFolder = dialog.SelectedPath;
                WorkingFolderChanged = true;
                UpdatePreview();
            }
        }

        private void BtnChangeTemplate_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select PBIP Template Folder",
                SelectedPath = txtTemplate.Text
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Verify it's a valid template folder - look for any .pbip file
                var pbipFiles = Directory.GetFiles(dialog.SelectedPath, "*.pbip");
                if (pbipFiles.Length == 0)
                {
                    MessageBox.Show("Selected folder does not contain a valid PBIP template (.pbip file not found).",
                        "Invalid Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                txtTemplate.Text = dialog.SelectedPath;
                TemplatePath = dialog.SelectedPath;
                TemplateChanged = true;
            }
        }

        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            var name = txtName.Text.Trim();
            var envUrl = txtEnvironmentUrl.Text.Trim();
            var folder = txtFolder.Text.Trim();
            var template = txtTemplate.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a name for the semantic model.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrWhiteSpace(envUrl))
            {
                MessageBox.Show("Please enter a Dataverse environment URL.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Validate name (no invalid path characters)
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                if (name.Contains(c))
                {
                    MessageBox.Show($"Name contains invalid character: '{c}'", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show("Please set a working folder first.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrEmpty(template) || !Directory.Exists(template))
            {
                MessageBox.Show("Please select a valid PBIP template folder.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            var fullPath = Path.Combine(folder, name);
            if (Directory.Exists(fullPath))
            {
                MessageBox.Show($"A folder with this name already exists:\n{fullPath}", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Normalize URL (remove https:// if present)
            if (envUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                envUrl = envUrl.Substring(8);

            SemanticModelName = name;
            EnvironmentUrl = envUrl;
            ConnectionType = cboConnectionType.SelectedIndex == 1 ? "FabricLink" : "DataverseTDS";
            FabricLinkSQLEndpoint = txtFabricEndpoint.Text.Trim();
            FabricLinkSQLDatabase = txtFabricDatabase.Text.Trim();
            WorkingFolder = folder;
        }
    }
}
