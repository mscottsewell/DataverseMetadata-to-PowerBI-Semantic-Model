// ===================================================================================
// SemanticModelChangesDialog.cs - Change Preview and Confirmation Dialog
// ===================================================================================
//
// PURPOSE:
// Displays a detailed preview of all changes that will be made to the Power BI
// semantic model files before applying them. This gives users visibility into
// what's being modified and an opportunity to cancel or create a backup.
//
// CHANGE TYPES DISPLAYED:
// - New: Tables, columns, or relationships being added
// - Update: Existing elements being modified
// - Preserve: User customizations that will be kept (e.g., measures)
// - Warning: Issues like orphaned tables or partial FetchXML support
//
// COLOR CODING:
// - Green: New additions
// - Yellow: Updates and modifications
// - Gray: Preserved elements (no changes)
// - Orange: Warnings requiring attention
//
// BACKUP OPTION:
// Users can opt to create a timestamped backup of the PBIP folder before
// changes are applied. This provides a recovery point.
//
// OUTPUT:
// - UserApproved: True if user clicked Apply
// - CreateBackup: True if backup checkbox was checked
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DataverseToPowerBI.XrmToolBox
{
    public class SemanticModelChangesDialog : Form
    {
        private ListView listViewChanges = null!;
        private Button btnApply = null!;
        private Button btnCancel = null!;
        private Label lblSummary = null!;
        private CheckBox chkBackup = null!;

        public bool UserApproved { get; private set; }
        public bool CreateBackup => chkBackup.Checked;

        public SemanticModelChangesDialog(List<SemanticModelChange> changes)
        {
            InitializeComponent();
            LoadChanges(changes);
        }

        private void InitializeComponent()
        {
            this.Text = "Review Semantic Model Changes";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Summary label
            lblSummary = new Label
            {
                Location = new Point(12, 12),
                Size = new Size(760, 40),
                Text = "The following changes will be made to your Power BI semantic model:",
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(lblSummary);

            // ListView for changes
            listViewChanges = new ListView
            {
                Location = new Point(12, 60),
                Size = new Size(760, 410),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Consolas", 9)
            };
            listViewChanges.Columns.Add("Type", 80);
            listViewChanges.Columns.Add("Object", 200);
            listViewChanges.Columns.Add("Change", 450);
            this.Controls.Add(listViewChanges);

            // Backup checkbox
            chkBackup = new CheckBox
            {
                Location = new Point(12, 480),
                Size = new Size(400, 24),
                Text = "Create backup of existing PBIP before applying changes",
                Checked = true
            };
            this.Controls.Add(chkBackup);

            // Buttons
            btnCancel = new Button
            {
                Location = new Point(596, 510),
                Size = new Size(80, 30),
                Text = "Cancel",
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => { UserApproved = false; this.Close(); };
            this.Controls.Add(btnCancel);

            btnApply = new Button
            {
                Location = new Point(692, 510),
                Size = new Size(80, 30),
                Text = "Apply",
                DialogResult = DialogResult.OK
            };
            btnApply.Click += (s, e) => { UserApproved = true; this.Close(); };
            this.Controls.Add(btnApply);

            this.AcceptButton = btnApply;
            this.CancelButton = btnCancel;
        }

        private void LoadChanges(List<SemanticModelChange> changes)
        {
            listViewChanges.Items.Clear();

            var stats = new Dictionary<string, int>
            {
                ["New"] = 0,
                ["Update"] = 0,
                ["Preserve"] = 0
            };

            foreach (var change in changes)
            {
                var item = new ListViewItem(new[]
                {
                    change.ChangeType.ToString(),
                    change.ObjectName,
                    change.Description
                });

                // Color code by change type
                switch (change.ChangeType)
                {
                    case ChangeType.New:
                        item.BackColor = Color.LightGreen;
                        stats["New"]++;
                        break;
                    case ChangeType.Update:
                        item.BackColor = Color.LightYellow;
                        stats["Update"]++;
                        break;
                    case ChangeType.Preserve:
                        item.BackColor = Color.LightBlue;
                        item.ForeColor = Color.Gray;
                        stats["Preserve"]++;
                        break;
                    case ChangeType.Warning:
                        item.BackColor = Color.LightCoral;
                        break;                    case ChangeType.Error:
                        item.BackColor = Color.IndianRed;
                        item.ForeColor = Color.White;
                        break;
                    case ChangeType.Info:
                        item.BackColor = Color.LightCyan;
                        break;                }

                listViewChanges.Items.Add(item);
            }

            // Update summary
            lblSummary.Text = $"Changes: {stats["New"]} new, {stats["Update"]} updates, {stats["Preserve"]} preserved. Review and apply?";
        }
    }

    public class SemanticModelChange
    {
        public ChangeType ChangeType { get; set; }
        public string ObjectType { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public enum ChangeType
    {
        New,
        Update,
        Preserve,
        Warning,
        Error,
        Info
    }
}
