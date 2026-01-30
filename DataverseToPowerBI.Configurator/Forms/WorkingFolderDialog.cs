using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace DataverseToPowerBI.Configurator.Forms
{
    public class WorkingFolderDialog : Form
    {
        private TextBox txtFolder = null!;
        private Button btnBrowse = null!;
        private Button btnOpen = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Label lblFolder = null!;

        public string WorkingFolder { get; private set; } = "";

        public WorkingFolderDialog(string currentFolder)
        {
            InitializeComponent();
            txtFolder.Text = currentFolder;
            WorkingFolder = currentFolder;
        }

        private void InitializeComponent()
        {
            this.Text = "Working Folder";
            this.Size = new System.Drawing.Size(550, 180);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            lblFolder = new Label
            {
                Text = "Working Folder:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };

            txtFolder = new TextBox
            {
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(400, 23),
                ReadOnly = true,
                BackColor = System.Drawing.SystemColors.Window
            };

            btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new System.Drawing.Point(430, 44),
                Size = new System.Drawing.Size(80, 25)
            };
            btnBrowse.Click += BtnBrowse_Click;

            btnOpen = new Button
            {
                Text = "Open in Explorer",
                Location = new System.Drawing.Point(20, 85),
                Size = new System.Drawing.Size(120, 28)
            };
            btnOpen.Click += BtnOpen_Click;

            btnOk = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(350, 100),
                Size = new System.Drawing.Size(75, 28),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(435, 100),
                Size = new System.Drawing.Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblFolder);
            this.Controls.Add(txtFolder);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(btnOpen);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Working Folder for Semantic Models",
                SelectedPath = txtFolder.Text
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                txtFolder.Text = dialog.SelectedPath;
            }
        }

        private void BtnOpen_Click(object? sender, EventArgs e)
        {
            var folder = txtFolder.Text.Trim();
            if (string.IsNullOrEmpty(folder))
            {
                MessageBox.Show("No working folder is set.", "Info", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Directory.Exists(folder))
            {
                var result = MessageBox.Show(
                    $"The folder does not exist:\n{folder}\n\nWould you like to create it?",
                    "Create Folder?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        Directory.CreateDirectory(folder);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to create folder:\n{ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            WorkingFolder = txtFolder.Text.Trim();
        }
    }
}
