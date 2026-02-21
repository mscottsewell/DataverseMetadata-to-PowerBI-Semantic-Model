// ===================================================================================
// TmdlPreviewDialog.cs - TMDL Preview Dialog
// ===================================================================================
//
// PURPOSE:
// Provides an interactive dialog for previewing and exporting TMDL table definitions
// exactly as they will be written to the semantic model. Shows full TMDL output
// including table declarations, columns, measures, and partition M expressions.
//
// FEATURES:
// - Sorted list: Fact tables → Dimension tables → Date table → Expressions (alpha)
// - Copy selected TMDL to clipboard
// - Save individual .tmdl file
// - Save all .tmdl files to a folder
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DataverseToPowerBI.XrmToolBox.Services;

namespace DataverseToPowerBI.XrmToolBox
{
    public class TmdlPreviewDialog : Form
    {
        private ListView listViewTables = null!;
        private TextBox txtTmdl = null!;
        private Button btnCopy = null!;
        private Button btnSave = null!;
        private Button btnSaveAll = null!;
        private Button btnClose = null!;

        private readonly Dictionary<string, TmdlPreviewEntry> _entries;
        private readonly string _connectionType;

        // Sorted list of entry keys for consistent ordering
        private readonly List<string> _sortedKeys;

        // Cached fonts to avoid GDI handle leaks
        private Font? _italicFont;
        private Font? _boldFont;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _italicFont?.Dispose();
                _boldFont?.Dispose();
            }
            base.Dispose(disposing);
        }

        public TmdlPreviewDialog(
            Dictionary<string, TmdlPreviewEntry> entries,
            string connectionType)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            _connectionType = connectionType;

            // Sort entries: FactTable(0) → DimensionTable(1) → DateTable(2) → Expression(3), then alphabetically within each type
            _sortedKeys = _entries
                .OrderBy(kvp => (int)kvp.Value.EntryType)
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => kvp.Key)
                .ToList();

            InitializeComponent();
            LoadEntries();
        }

        private void InitializeComponent()
        {
            this.Text = "TMDL Preview";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(900, 600);

            var entryCount = _entries.Count;
            var tableCount = _entries.Count(e =>
                e.Value.EntryType == TmdlEntryType.FactTable ||
                e.Value.EntryType == TmdlEntryType.DimensionTable);

            // Table list (left panel)
            var lblTables = new Label
            {
                Location = new Point(10, 10),
                Size = new Size(280, 20),
                Text = $"TMDL Files ({entryCount}) — {_connectionType}"
            };
            this.Controls.Add(lblTables);

            listViewTables = new ListView
            {
                Location = new Point(10, 35),
                Size = new Size(280, 650),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HideSelection = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };
            listViewTables.Columns.Add("Name", 190);
            listViewTables.Columns.Add("Type", 80);
            listViewTables.SelectedIndexChanged += ListViewTables_SelectedIndexChanged;
            this.Controls.Add(listViewTables);

            // TMDL display (right panel)
            var lblTmdl = new Label
            {
                Location = new Point(300, 10),
                Size = new Size(400, 20),
                Text = "TMDL Content"
            };
            this.Controls.Add(lblTmdl);

            txtTmdl = new TextBox
            {
                Location = new Point(300, 35),
                Size = new Size(770, 620),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                WordWrap = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(txtTmdl);

            // Buttons
            btnCopy = new Button
            {
                Location = new Point(300, 665),
                Size = new Size(100, 30),
                Text = "Copy",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnCopy.Click += BtnCopy_Click;
            this.Controls.Add(btnCopy);

            btnSave = new Button
            {
                Location = new Point(410, 665),
                Size = new Size(100, 30),
                Text = "Save...",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnSaveAll = new Button
            {
                Location = new Point(520, 665),
                Size = new Size(100, 30),
                Text = "Save All...",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnSaveAll.Click += BtnSaveAll_Click;
            this.Controls.Add(btnSaveAll);

            btnClose = new Button
            {
                Location = new Point(960, 665),
                Size = new Size(110, 30),
                Text = "Close",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            this.Controls.Add(btnClose);

            this.AcceptButton = btnClose;
        }

        private void LoadEntries()
        {
            listViewTables.Items.Clear();

            foreach (var key in _sortedKeys)
            {
                var entry = _entries[key];
                var item = new ListViewItem(key);
                item.Tag = key;

                // Type label for the second column
                string typeLabel;
                switch (entry.EntryType)
                {
                    case TmdlEntryType.Expression:
                        typeLabel = "Config";
                        item.ForeColor = Color.FromArgb(100, 100, 180);
                        _italicFont ??= new Font(listViewTables.Font, FontStyle.Italic);
                        item.Font = _italicFont;
                        break;
                    case TmdlEntryType.DateTable:
                        typeLabel = "Date";
                        item.ForeColor = Color.FromArgb(100, 100, 180);
                        _italicFont ??= new Font(listViewTables.Font, FontStyle.Italic);
                        item.Font = _italicFont;
                        break;
                    case TmdlEntryType.FactTable:
                        typeLabel = "Fact";
                        _boldFont ??= new Font(listViewTables.Font, FontStyle.Bold);
                        item.Font = _boldFont;
                        break;
                    case TmdlEntryType.DimensionTable:
                        typeLabel = "Dimension";
                        break;
                    default:
                        typeLabel = "";
                        break;
                }

                item.SubItems.Add(typeLabel);
                listViewTables.Items.Add(item);
            }

            if (listViewTables.Items.Count > 0)
            {
                listViewTables.Items[0].Selected = true;
            }
        }

        private void ListViewTables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewTables.SelectedItems.Count == 0)
            {
                txtTmdl.Text = "";
                return;
            }

            var entryName = listViewTables.SelectedItems[0].Tag as string;
            if (entryName != null && _entries.TryGetValue(entryName, out var entry))
            {
                txtTmdl.Text = WrapWithCreateOrReplace(entry.Content);
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtTmdl.Text))
            {
                Clipboard.SetText(txtTmdl.Text);
                MessageBox.Show("TMDL copied to clipboard.", "Copied",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (listViewTables.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a TMDL entry to save.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var entryName = listViewTables.SelectedItems[0].Tag as string;
            if (entryName == null || !_entries.TryGetValue(entryName, out var entry))
                return;

            var defaultFileName = SanitizeFileName(entryName) + ".tmdl";

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "TMDL files (*.tmdl)|*.tmdl|All files (*.*)|*.*";
                dialog.FileName = defaultFileName;
                dialog.DefaultExt = "tmdl";
                dialog.Title = "Save TMDL File";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(dialog.FileName, WrapWithCreateOrReplace(entry.Content), new System.Text.UTF8Encoding(false));
                    MessageBox.Show($"Saved: {Path.GetFileName(dialog.FileName)}",
                        "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnSaveAll_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder to save all TMDL files";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var folder = dialog.SelectedPath;
                    var encoding = new System.Text.UTF8Encoding(false);
                    var count = 0;

                    foreach (var kvp in _entries)
                    {
                        var fileName = SanitizeFileName(kvp.Key) + ".tmdl";
                        var filePath = Path.Combine(folder, fileName);
                        File.WriteAllText(filePath, WrapWithCreateOrReplace(kvp.Value.Content), encoding);
                        count++;
                    }

                    MessageBox.Show($"Saved {count} TMDL files to:\n{folder}",
                        "Save All Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>
        /// Wraps TMDL content with a createOrReplace header and indents all lines by one tab,
        /// producing content that can be pasted directly into the Power BI TMDL editor.
        /// </summary>
        private static string WrapWithCreateOrReplace(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var indented = string.Join("\r\n", lines.Select(l => "\t" + l));
            return "createOrReplace\r\n" + indented;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("", name.Select(c => invalid.Contains(c) ? '_' : c));
        }
    }
}
