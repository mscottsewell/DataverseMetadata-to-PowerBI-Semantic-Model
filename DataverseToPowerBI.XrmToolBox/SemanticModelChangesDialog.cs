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
using System.Linq;
using System.Windows.Forms;

namespace DataverseToPowerBI.XrmToolBox
{
    public class SemanticModelChangesDialog : Form
    {
        private TreeView treeViewChanges = null!;
        private RichTextBox rtbDetail = null!;
        private SplitContainer splitContainer = null!;
        private Panel pnlSummary = null!;
        private Panel pnlFilters = null!;
        private Button btnApply = null!;
        private Button btnCancel = null!;
        private CheckBox chkBackup = null!;
        private CheckBox chkRemoveOrphaned = null!;

        // Filter toggle buttons
        private CheckBox chkShowPreserved = null!;
        private CheckBox chkShowWarnings = null!;
        private CheckBox chkShowNew = null!;
        private CheckBox chkShowUpdates = null!;

        // Summary labels
        private Label lblWarnings = null!;
        private Label lblNew = null!;
        private Label lblUpdated = null!;
        private Label lblPreserved = null!;

        private readonly List<SemanticModelChange> _changes;
        private readonly List<string> _orphanedTableNames;

        public bool UserApproved { get; private set; }
        public bool CreateBackup => chkBackup.Checked;
        public bool RemoveOrphanedTables => chkRemoveOrphaned.Checked;

        public SemanticModelChangesDialog(List<SemanticModelChange> changes)
        {
            _changes = changes;
            _orphanedTableNames = changes
                .Where(c => c.ChangeType == ChangeType.Warning && c.ObjectType == "Table")
                .Select(c => c.ObjectName)
                .ToList();
            InitializeComponent();
            LoadChanges();
        }

        private void InitializeComponent()
        {
            this.Text = "Review Semantic Model Changes";
            this.Size = new Size(900, 700);
            this.MinimumSize = new Size(750, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = true;
            this.MinimizeBox = false;

            // ── Summary bar at top ──
            pnlSummary = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(245, 245, 250),
                Padding = new Padding(12, 8, 12, 8)
            };
            this.Controls.Add(pnlSummary);

            lblWarnings = CreateSummaryBadge("⚠ 0 Warnings", Color.FromArgb(200, 80, 40));
            lblNew = CreateSummaryBadge("✚ 0 New", Color.FromArgb(40, 140, 40));
            lblUpdated = CreateSummaryBadge("↻ 0 Updated", Color.FromArgb(160, 130, 20));
            lblPreserved = CreateSummaryBadge("≡ 0 Preserved", Color.FromArgb(100, 100, 140));

            var flowSummary = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false
            };
            flowSummary.Controls.AddRange(new Control[] { lblWarnings, lblNew, lblUpdated, lblPreserved });
            pnlSummary.Controls.Add(flowSummary);

            // ── Filter bar ──
            pnlFilters = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(250, 250, 252),
                Padding = new Padding(12, 4, 12, 4)
            };
            this.Controls.Add(pnlFilters);

            var flowFilters = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            pnlFilters.Controls.Add(flowFilters);

            var lblFilter = new Label { Text = "Show:", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold) };
            flowFilters.Controls.Add(lblFilter);

            chkShowWarnings = CreateFilterToggle("Warnings", true);
            chkShowNew = CreateFilterToggle("New", true);
            chkShowUpdates = CreateFilterToggle("Updates", true);
            chkShowPreserved = CreateFilterToggle("Preserved", false);

            flowFilters.Controls.AddRange(new Control[] { chkShowWarnings, chkShowNew, chkShowUpdates, chkShowPreserved });

            // ── Split container: TreeView top, Detail bottom ──
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 380,
                Panel1MinSize = 150,
                Panel2MinSize = 80
            };
            this.Controls.Add(splitContainer);

            // TreeView
            treeViewChanges = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                HideSelection = false,
                FullRowSelect = true,
                ItemHeight = 24,
                DrawMode = TreeViewDrawMode.OwnerDrawText
            };
            treeViewChanges.AfterSelect += TreeViewChanges_AfterSelect;
            treeViewChanges.DrawNode += TreeViewChanges_DrawNode;
            splitContainer.Panel1.Controls.Add(treeViewChanges);

            // Detail pane
            rtbDetail = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                Text = "Select a change to see details..."
            };
            splitContainer.Panel2.Controls.Add(rtbDetail);

            var lblDetailHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "  Detail",
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };
            splitContainer.Panel2.Controls.Add(lblDetailHeader);

            // ── Bottom panel: checkboxes + buttons ──
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 75,
                Padding = new Padding(12, 8, 12, 8)
            };
            this.Controls.Add(pnlBottom);

            chkBackup = new CheckBox
            {
                Location = new Point(12, 8),
                AutoSize = true,
                Text = "Create backup of existing PBIP before applying changes",
                Checked = true
            };
            pnlBottom.Controls.Add(chkBackup);

            chkRemoveOrphaned = new CheckBox
            {
                Location = new Point(12, 30),
                AutoSize = true,
                Text = "Remove tables no longer in the model",
                Checked = false,
                Visible = _orphanedTableNames.Count > 0
            };
            pnlBottom.Controls.Add(chkRemoveOrphaned);

            btnCancel = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Size = new Size(80, 30),
                Text = "Cancel",
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Location = new Point(pnlBottom.ClientSize.Width - 180, 38);
            btnCancel.Click += (s, e) => { UserApproved = false; this.Close(); };
            pnlBottom.Controls.Add(btnCancel);

            btnApply = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Size = new Size(80, 30),
                Text = "Apply"
            };
            btnApply.Location = new Point(pnlBottom.ClientSize.Width - 92, 38);
            btnApply.Click += BtnApply_Click;
            pnlBottom.Controls.Add(btnApply);

            this.AcceptButton = btnApply;
            this.CancelButton = btnCancel;

            // Ensure dock order: bottom first, then top panels, then fill
            this.Controls.SetChildIndex(pnlBottom, 0);
            this.Controls.SetChildIndex(splitContainer, 1);
            this.Controls.SetChildIndex(pnlFilters, 2);
            this.Controls.SetChildIndex(pnlSummary, 3);
        }

        private Label CreateSummaryBadge(string text, Color color)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 10f, FontStyle.Bold),
                ForeColor = color,
                Margin = new Padding(0, 4, 24, 0),
                Cursor = Cursors.Default
            };
        }

        private CheckBox CreateFilterToggle(string text, bool isChecked)
        {
            var chk = new CheckBox
            {
                Text = text,
                AutoSize = true,
                Checked = isChecked,
                Margin = new Padding(0, 2, 12, 0),
                Font = new Font(this.Font.FontFamily, 8.5f)
            };
            chk.CheckedChanged += (s, e) => LoadChanges();
            return chk;
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            if (chkRemoveOrphaned.Checked && _orphanedTableNames.Count > 0)
            {
                var tableList = string.Join("\n  • ", _orphanedTableNames);
                var confirmResult = MessageBox.Show(
                    this,
                    $"The following tables will be permanently deleted from the PBIP:\n\n  • {tableList}\n\nAre you sure you want to proceed?",
                    "Confirm Table Removal",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.OK)
                {
                    chkRemoveOrphaned.Checked = false;
                    return;
                }
            }

            UserApproved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void LoadChanges()
        {
            treeViewChanges.BeginUpdate();
            treeViewChanges.Nodes.Clear();

            // Count stats from ALL changes (not filtered)
            int warnCount = 0, newCount = 0, updateCount = 0, preserveCount = 0;
            foreach (var c in _changes)
            {
                switch (c.ChangeType)
                {
                    case ChangeType.Warning: case ChangeType.Error: warnCount++; break;
                    case ChangeType.New: newCount++; break;
                    case ChangeType.Update: updateCount++; break;
                    case ChangeType.Preserve: preserveCount++; break;
                }
            }
            lblWarnings.Text = $"⚠ {warnCount} Warning{(warnCount != 1 ? "s" : "")}";
            lblWarnings.Visible = warnCount > 0;
            lblNew.Text = $"✚ {newCount} New";
            lblUpdated.Text = $"↻ {updateCount} Updated";
            lblPreserved.Text = $"≡ {preserveCount} Preserved";

            // Determine which change types are visible
            var visibleTypes = new HashSet<ChangeType>();
            if (chkShowWarnings.Checked) { visibleTypes.Add(ChangeType.Warning); visibleTypes.Add(ChangeType.Error); }
            if (chkShowNew.Checked) visibleTypes.Add(ChangeType.New);
            if (chkShowUpdates.Checked) visibleTypes.Add(ChangeType.Update);
            if (chkShowPreserved.Checked) { visibleTypes.Add(ChangeType.Preserve); visibleTypes.Add(ChangeType.Info); }

            var filtered = _changes.Where(c => visibleTypes.Contains(c.ChangeType)).ToList();

            // Group into categories
            var categories = new (string Name, string[] ObjectTypes, int SortOrder)[]
            {
                ("⚠ Warnings", new[] { "Integrity", "Missing", "StorageMode", "ConnectionType" }, 0),
                ("📋 Tables", new[] { "Table", "Column", "Measures", "Project" }, 1),
                ("🔗 Relationships", new[] { "Relationship", "Relationships" }, 2),
                ("🔌 Data Sources", new[] { "FabricLink", "DataverseURL" }, 3)
            };

            foreach (var cat in categories)
            {
                var catChanges = filtered.Where(c => cat.ObjectTypes.Contains(c.ObjectType)).ToList();
                if (catChanges.Count == 0) continue;

                var catNode = new TreeNode(cat.Name)
                {
                    NodeFont = new Font(treeViewChanges.Font, FontStyle.Bold),
                };
                catNode.Tag = null; // Category node has no change

                // For tables category, group by parent (table name)
                if (cat.Name.Contains("Tables"))
                {
                    LoadTableNodes(catNode, catChanges);
                }
                else
                {
                    foreach (var change in catChanges)
                    {
                        var node = CreateChangeNode(change);
                        catNode.Nodes.Add(node);
                    }
                }

                // Update category label with count
                var actionCount = catChanges.Count(c => c.ChangeType != ChangeType.Preserve && c.ChangeType != ChangeType.Info);
                if (actionCount > 0)
                    catNode.Text = $"{cat.Name} ({actionCount} change{(actionCount != 1 ? "s" : "")})";
                else
                    catNode.Text = $"{cat.Name} (no changes)";

                treeViewChanges.Nodes.Add(catNode);

                // Expand categories with actionable changes, collapse preserved-only
                if (actionCount > 0)
                    catNode.Expand();
            }

            treeViewChanges.EndUpdate();
        }

        private void LoadTableNodes(TreeNode catNode, List<SemanticModelChange> changes)
        {
            // Separate top-level table/project changes from column-level changes
            var tableChanges = changes.Where(c => c.ObjectType == "Table" || c.ObjectType == "Measures" || c.ObjectType == "Project").ToList();
            var columnChanges = changes.Where(c => c.ObjectType == "Column").ToList();

            // Group columns by their parent table (from ParentKey or parse from ObjectName)
            var columnsByTable = new Dictionary<string, List<SemanticModelChange>>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columnChanges)
            {
                var tableName = !string.IsNullOrEmpty(col.ParentKey) ? col.ParentKey
                    : col.ObjectName.Contains(".") ? col.ObjectName.Split('.')[0] : "Unknown";
                if (!columnsByTable.ContainsKey(tableName))
                    columnsByTable[tableName] = new List<SemanticModelChange>();
                columnsByTable[tableName].Add(col);
            }

            foreach (var tc in tableChanges)
            {
                var tableNode = CreateChangeNode(tc);

                // Attach child column changes
                var tableName = tc.ObjectName;
                if (columnsByTable.TryGetValue(tableName, out var cols))
                {
                    foreach (var col in cols)
                    {
                        tableNode.Nodes.Add(CreateChangeNode(col));
                    }
                    columnsByTable.Remove(tableName);
                }

                // Attach preserve/measures entries as children too
                var measureChanges = changes.Where(c => c.ObjectType == "Measures" && c.ObjectName == tableName).ToList();
                foreach (var m in measureChanges)
                {
                    tableNode.Nodes.Add(CreateChangeNode(m));
                }

                catNode.Nodes.Add(tableNode);

                // Expand table nodes that have actual changes
                if (tc.ChangeType != ChangeType.Preserve)
                    tableNode.Expand();
            }

            // Any leftover column changes without a parent table entry
            foreach (var kvp in columnsByTable)
            {
                foreach (var col in kvp.Value)
                {
                    catNode.Nodes.Add(CreateChangeNode(col));
                }
            }
        }

        private TreeNode CreateChangeNode(SemanticModelChange change)
        {
            var icon = GetChangeIcon(change);
            var impactTag = GetImpactTag(change);
            var text = $"{icon} {change.ObjectName}  {impactTag}— {change.Description}";

            var node = new TreeNode(text) { Tag = change };
            return node;
        }

        private string GetChangeIcon(SemanticModelChange change)
        {
            switch (change.ChangeType)
            {
                case ChangeType.New: return "✚";
                case ChangeType.Update: return "↻";
                case ChangeType.Preserve: return "✓";
                case ChangeType.Warning: return "⚠";
                case ChangeType.Error: return "✖";
                case ChangeType.Info: return "ℹ";
                default: return "•";
            }
        }

        private string GetImpactTag(SemanticModelChange change)
        {
            switch (change.Impact)
            {
                case ImpactLevel.Destructive: return "[DESTRUCTIVE] ";
                case ImpactLevel.Moderate: return "[MODERATE] ";
                case ImpactLevel.Additive: return "[ADDITIVE] ";
                default: return "";
            }
        }

        private void TreeViewChanges_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.IsEmpty) return;

            var change = e.Node.Tag as SemanticModelChange;

            // Determine colors
            Color backColor = Color.White;
            Color textColor = Color.Black;

            if (change != null)
            {
                switch (change.ChangeType)
                {
                    case ChangeType.New:
                        backColor = Color.FromArgb(232, 250, 232);
                        textColor = Color.FromArgb(30, 100, 30);
                        break;
                    case ChangeType.Update:
                        backColor = Color.FromArgb(255, 250, 220);
                        textColor = Color.FromArgb(120, 100, 10);
                        break;
                    case ChangeType.Preserve:
                        backColor = Color.FromArgb(240, 245, 255);
                        textColor = Color.FromArgb(120, 120, 150);
                        break;
                    case ChangeType.Warning:
                        backColor = Color.FromArgb(255, 235, 225);
                        textColor = Color.FromArgb(180, 60, 20);
                        break;
                    case ChangeType.Error:
                        backColor = Color.FromArgb(255, 220, 220);
                        textColor = Color.FromArgb(180, 30, 30);
                        break;
                    case ChangeType.Info:
                        backColor = Color.FromArgb(230, 245, 255);
                        textColor = Color.FromArgb(40, 90, 140);
                        break;
                }

                // Highlight destructive impact
                if (change.Impact == ImpactLevel.Destructive)
                {
                    backColor = Color.FromArgb(255, 225, 220);
                }
            }

            // Selected node highlighting
            if ((e.State & TreeNodeStates.Selected) != 0)
            {
                backColor = Color.FromArgb(0, 120, 215);
                textColor = Color.White;
            }

            using (var bgBrush = new SolidBrush(backColor))
            {
                e.Graphics!.FillRectangle(bgBrush, e.Bounds);
            }

            var font = e.Node.NodeFont ?? treeViewChanges.Font;
            TextRenderer.DrawText(e.Graphics!, e.Node.Text, font, e.Bounds, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        private void TreeViewChanges_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is SemanticModelChange change)
            {
                ShowDetail(change);
            }
            else
            {
                rtbDetail.Clear();
                rtbDetail.Text = "Select a specific change to see details...";
            }
        }

        private void ShowDetail(SemanticModelChange change)
        {
            rtbDetail.Clear();

            var sb = new System.Text.StringBuilder();

            // Header
            sb.AppendLine($"  {change.ChangeType.ToString().ToUpper()} — {change.ObjectType}: {change.ObjectName}");
            sb.AppendLine($"  Impact: {change.Impact}");
            sb.AppendLine(new string('─', 70));
            sb.AppendLine();

            // Description
            sb.AppendLine($"  {change.Description}");
            sb.AppendLine();

            // Detail text (before/after, context)
            if (!string.IsNullOrEmpty(change.DetailText))
            {
                sb.AppendLine("  ── Detail ──");
                sb.AppendLine();
                foreach (var line in change.DetailText.Split('\n'))
                {
                    sb.AppendLine($"  {line.TrimEnd('\r')}");
                }
            }
            else
            {
                // Provide contextual help based on change type
                sb.AppendLine(GetContextualHelp(change));
            }

            rtbDetail.Text = sb.ToString();
        }

        private string GetContextualHelp(SemanticModelChange change)
        {
            switch (change.ChangeType)
            {
                case ChangeType.New:
                    return change.ObjectType == "Table"
                        ? "  This table will be created with all selected columns.\n  Auto-measures will be generated based on table role (Fact/Dimension)."
                        : "  This element will be added to the semantic model.";
                case ChangeType.Update:
                    return "  This element will be updated. User customizations (measures, descriptions,\n  formatting, annotations) will be preserved where possible.";
                case ChangeType.Preserve:
                    return "  No changes detected — this element will remain as-is.\n  Any user customizations are safe.";
                case ChangeType.Warning:
                    if (change.ObjectType == "StorageMode")
                        return "  Changing storage mode affects how data is cached.\n  The cache file (cache.abf) will be deleted to prevent stale data.\n  This is safe but will require a data refresh in Power BI.";
                    if (change.ObjectType == "ConnectionType")
                        return "  Changing connection type restructures all table queries.\n  User measures and relationships will be preserved,\n  but partition expressions will be regenerated.";
                    if (change.ObjectType == "Table")
                        return "  This table exists in the PBIP but is not in the current selection.\n  Check 'Remove tables no longer in the model' to delete it,\n  or leave unchecked to keep it.";
                    return "  Review this warning before applying changes.";
                default:
                    return "";
            }
        }
    }

    public class SemanticModelChange
    {
        public ChangeType ChangeType { get; set; }
        public string ObjectType { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ImpactLevel Impact { get; set; } = ImpactLevel.Safe;
        public string DetailText { get; set; } = string.Empty;
        public string ParentKey { get; set; } = string.Empty; // Groups child items under a parent (e.g., table name for columns)
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

    public enum ImpactLevel
    {
        Safe,       // No risk — additive or preserved
        Additive,   // New content being added
        Moderate,   // Existing content modified, user data preserved
        Destructive // Structural change, potential data/customization loss
    }
}
