using System;
using System.IO;
using System.Windows.Forms;

namespace DataverseToPowerBI.Configurator.Forms
{
    public class NewSemanticModelDialog : Form
    {
        private TextBox txtName = null!;
        private TextBox txtEnvironmentUrl = null!;
        private TextBox txtFolder = null!;
        private Button btnChangeFolder = null!;
        private Button btnCreate = null!;
        private Button btnCancel = null!;
        private Label lblName = null!;
        private Label lblEnvironmentUrl = null!;
        private Label lblFolder = null!;
        private Label lblPreview = null!;
        private Label lblPreviewPath = null!;

        public string SemanticModelName { get; private set; } = "";
        public string EnvironmentUrl { get; private set; } = "";
        public string WorkingFolder { get; private set; } = "";
        public bool WorkingFolderChanged { get; private set; } = false;

        public NewSemanticModelDialog(string currentWorkingFolder, string currentEnvironmentUrl = "")
        {
            WorkingFolder = currentWorkingFolder;
            InitializeComponent();
            txtFolder.Text = currentWorkingFolder;
            txtEnvironmentUrl.Text = currentEnvironmentUrl;
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            this.Text = "Create New Semantic Model";
            this.Size = new System.Drawing.Size(550, 340);
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

            lblFolder = new Label
            {
                Text = "Working Folder:",
                Location = new System.Drawing.Point(20, 145),
                AutoSize = true
            };

            txtFolder = new TextBox
            {
                Location = new System.Drawing.Point(20, 170),
                Size = new System.Drawing.Size(380, 23),
                ReadOnly = true,
                BackColor = System.Drawing.SystemColors.Window
            };

            btnChangeFolder = new Button
            {
                Text = "Change Folder...",
                Location = new System.Drawing.Point(410, 169),
                Size = new System.Drawing.Size(100, 25)
            };
            btnChangeFolder.Click += BtnChangeFolder_Click;

            lblPreview = new Label
            {
                Text = "Will be created at:",
                Location = new System.Drawing.Point(20, 210),
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray
            };

            lblPreviewPath = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(20, 228),
                Size = new System.Drawing.Size(490, 20),
                ForeColor = System.Drawing.Color.DarkBlue,
                AutoEllipsis = true
            };

            btnCreate = new Button
            {
                Text = "Create",
                Location = new System.Drawing.Point(340, 260),
                Size = new System.Drawing.Size(80, 28),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            btnCreate.Click += BtnCreate_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(430, 260),
                Size = new System.Drawing.Size(80, 28),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblEnvironmentUrl);
            this.Controls.Add(txtEnvironmentUrl);
            this.Controls.Add(lblFolder);
            this.Controls.Add(txtFolder);
            this.Controls.Add(btnChangeFolder);
            this.Controls.Add(lblPreview);
            this.Controls.Add(lblPreviewPath);
            this.Controls.Add(btnCreate);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnCreate;
            this.CancelButton = btnCancel;
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

        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            var name = txtName.Text.Trim();
            var envUrl = txtEnvironmentUrl.Text.Trim();
            var folder = txtFolder.Text.Trim();

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
            WorkingFolder = folder;
        }
    }
}
