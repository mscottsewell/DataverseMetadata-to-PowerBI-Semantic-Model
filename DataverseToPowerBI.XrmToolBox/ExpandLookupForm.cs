// ===================================================================================
// ExpandLookupForm.cs - Expand Lookup Field to Include Related Table Attributes
// ===================================================================================
//
// PURPOSE:
// This dialog allows users to select attributes from a related table (via a lookup
// field) to flatten into the parent table. Instead of creating a separate dimension
// table and relationship, the selected columns are brought in via a LEFT OUTER JOIN
// in the generated SQL.
//
// USE CASES:
// - Pull a few reference fields (e.g., Account Name, Industry) without a full dim
// - Flatten lookup references for simpler, denormalized reporting
// - Reduce the number of tables in the semantic model
//
// UI FLOW:
// 1. User sees the related table name and lookup field info at top
// 2. User selects a form to filter available attributes
// 3. User checks attributes to include from that form
// 4. Performance warnings shown if thresholds exceeded
//
// EXPERIMENTAL: This feature is gated behind FeatureFlags.EnableExpandLookup
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DataverseToPowerBI.Core.Models;

using CoreAttributeMetadata = DataverseToPowerBI.Core.Models.AttributeMetadata;
using WinLabel = System.Windows.Forms.Label;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Dialog for selecting attributes from a related table to expand (flatten) into the parent table.
    /// </summary>
    public class ExpandLookupForm : Form
    {
        private const int MAX_RECOMMENDED_FIELDS = 10;
        private const int MAX_RECOMMENDED_EXPANDS = 3;

        private readonly string _lookupAttributeName;
        private readonly string _lookupDisplayName;
        private readonly string _targetTableLogicalName;
        private readonly string _targetTableDisplayName;
        private readonly string _targetTablePrimaryKey;
        private readonly List<FormMetadata> _forms;
        private readonly List<CoreAttributeMetadata> _allAttributes;
        private readonly List<ExpandedLookupAttribute>? _existingSelection;
        private readonly string? _existingFormId;
        private readonly int _currentExpandCount;

        // UI Controls
        private WinLabel lblLookupInfo = null!;
        private WinLabel lblTargetTable = null!;
        private WinLabel lblForm = null!;
        private ComboBox cboForm = null!;
        private WinLabel lblFormFieldCount = null!;
        private WinLabel lblWarning = null!;
        private Panel pnlWarning = null!;
        private ListView listViewAttributes = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;
        private WinLabel lblStatus = null!;

        private bool _isLoading = false;

        /// <summary>
        /// The selected attributes from the related table.
        /// </summary>
        public List<ExpandedLookupAttribute> SelectedAttributes { get; private set; } = new List<ExpandedLookupAttribute>();

        /// <summary>
        /// The form ID selected by the user.
        /// </summary>
        public string? SelectedFormId { get; private set; }

        public ExpandLookupForm(
            string lookupAttributeName,
            string lookupDisplayName,
            string targetTableLogicalName,
            string targetTableDisplayName,
            string targetTablePrimaryKey,
            List<FormMetadata> forms,
            List<CoreAttributeMetadata> allAttributes,
            List<ExpandedLookupAttribute>? existingSelection = null,
            string? existingFormId = null,
            int currentExpandCount = 0)
        {
            _lookupAttributeName = lookupAttributeName;
            _lookupDisplayName = lookupDisplayName;
            _targetTableLogicalName = targetTableLogicalName;
            _targetTableDisplayName = targetTableDisplayName;
            _targetTablePrimaryKey = targetTablePrimaryKey;
            _forms = forms;
            _allAttributes = allAttributes;
            _existingSelection = existingSelection;
            _existingFormId = existingFormId;
            _currentExpandCount = currentExpandCount;

            InitializeComponent();
            PopulateFormDropdown();
        }

        private void InitializeComponent()
        {
            this.Text = $"Expand Lookup - {_lookupDisplayName}";
            this.Size = new Size(600, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 15;

            // Lookup info header
            lblLookupInfo = new WinLabel
            {
                Text = $"Lookup Field: {_lookupDisplayName} ({_lookupAttributeName})",
                Location = new Point(15, y),
                AutoSize = true,
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(lblLookupInfo);
            y += 25;

            lblTargetTable = new WinLabel
            {
                Text = $"Related Table: {_targetTableDisplayName} ({_targetTableLogicalName})",
                Location = new Point(15, y),
                AutoSize = true,
                ForeColor = Color.DarkBlue
            };
            this.Controls.Add(lblTargetTable);
            y += 30;

            // Warning panel
            pnlWarning = new Panel
            {
                Location = new Point(15, y),
                Size = new Size(550, 50),
                BackColor = Color.FromArgb(255, 248, 220),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            lblWarning = new WinLabel
            {
                Text = "",
                Location = new Point(8, 5),
                Size = new Size(530, 40),
                ForeColor = Color.DarkGoldenrod
            };
            pnlWarning.Controls.Add(lblWarning);
            this.Controls.Add(pnlWarning);
            y += 55;

            // Form selector
            lblForm = new WinLabel
            {
                Text = "Form:",
                Location = new Point(15, y + 3),
                AutoSize = true
            };
            this.Controls.Add(lblForm);

            cboForm = new ComboBox
            {
                Location = new Point(60, y),
                Size = new Size(380, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboForm.SelectedIndexChanged += CboForm_SelectedIndexChanged;
            this.Controls.Add(cboForm);

            lblFormFieldCount = new WinLabel
            {
                Text = "",
                Location = new Point(450, y + 3),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblFormFieldCount);
            y += 35;

            // Attribute list
            listViewAttributes = new ListView
            {
                Location = new Point(15, y),
                Size = new Size(550, 320),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true
            };
            listViewAttributes.Columns.Add("Sel", 35);
            listViewAttributes.Columns.Add("Display Name", 200);
            listViewAttributes.Columns.Add("Logical Name", 170);
            listViewAttributes.Columns.Add("Type", 120);
            listViewAttributes.ItemChecked += ListViewAttributes_ItemChecked;
            this.Controls.Add(listViewAttributes);
            y += 325;

            // Buttons row
            btnSelectAll = new Button
            {
                Text = "Select All",
                Location = new Point(15, y + 5),
                Size = new Size(80, 28)
            };
            btnSelectAll.Click += BtnSelectAll_Click;
            this.Controls.Add(btnSelectAll);

            btnDeselectAll = new Button
            {
                Text = "Deselect All",
                Location = new Point(100, y + 5),
                Size = new Size(85, 28)
            };
            btnDeselectAll.Click += BtnDeselectAll_Click;
            this.Controls.Add(btnDeselectAll);

            lblStatus = new WinLabel
            {
                Text = "Select a form to see available attributes.",
                Location = new Point(195, y + 10),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblStatus);

            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(400, y + 5),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            btnOk.Click += BtnOk_Click;
            this.Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(485, y + 5),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void PopulateFormDropdown()
        {
            cboForm.Items.Clear();
            cboForm.Items.Add("(All Attributes)");

            foreach (var form in _forms.OrderBy(f => f.Name))
            {
                var fieldCount = form.Fields?.Count ?? 0;
                cboForm.Items.Add(new FormComboItem(form, fieldCount));
            }

            // Pre-select existing form or first available
            if (!string.IsNullOrEmpty(_existingFormId))
            {
                for (int i = 1; i < cboForm.Items.Count; i++)
                {
                    if (cboForm.Items[i] is FormComboItem item && item.Form.FormId == _existingFormId)
                    {
                        cboForm.SelectedIndex = i;
                        return;
                    }
                }
            }

            // Default to first form if available, otherwise "All Attributes"
            cboForm.SelectedIndex = cboForm.Items.Count > 1 ? 1 : 0;
        }

        private void CboForm_SelectedIndexChanged(object? sender, EventArgs e)
        {
            PopulateAttributes();
        }

        private void PopulateAttributes()
        {
            _isLoading = true;
            listViewAttributes.BeginUpdate();
            listViewAttributes.Items.Clear();

            // Get form fields filter
            HashSet<string>? formFields = null;
            if (cboForm.SelectedItem is FormComboItem formItem)
            {
                formFields = formItem.Form.Fields != null
                    ? new HashSet<string>(formItem.Form.Fields, StringComparer.OrdinalIgnoreCase)
                    : null;
                lblFormFieldCount.Text = formFields != null ? $"{formFields.Count} fields" : "";
                SelectedFormId = formItem.Form.FormId;
            }
            else
            {
                lblFormFieldCount.Text = $"{_allAttributes.Count} total";
                SelectedFormId = null;
            }

            // Build existing selection lookup
            var existingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_existingSelection != null)
            {
                foreach (var attr in _existingSelection)
                    existingSet.Add(attr.LogicalName);
            }

            // Filter and display attributes
            var filteredAttrs = _allAttributes
                .Where(a => !IsExcludedAttribute(a))
                .Where(a => formFields == null || formFields.Contains(a.LogicalName) || existingSet.Contains(a.LogicalName))
                .OrderBy(a => a.DisplayName ?? a.LogicalName)
                .ToList();

            foreach (var attr in filteredAttrs)
            {
                var item = new ListViewItem("");
                item.Checked = existingSet.Contains(attr.LogicalName);
                item.SubItems.Add(attr.DisplayName ?? attr.LogicalName);
                item.SubItems.Add(attr.LogicalName);
                item.SubItems.Add(attr.AttributeType ?? "");
                item.Tag = attr;
                item.Name = attr.LogicalName;

                // Gray out attributes not on the form
                if (formFields != null && !formFields.Contains(attr.LogicalName) && !existingSet.Contains(attr.LogicalName))
                {
                    item.ForeColor = Color.LightGray;
                }

                listViewAttributes.Items.Add(item);
            }

            listViewAttributes.EndUpdate();
            _isLoading = false;
            UpdateStatus();
            UpdateWarnings();
        }

        /// <summary>
        /// Excludes system/virtual attributes that shouldn't be expanded.
        /// </summary>
        private bool IsExcludedAttribute(CoreAttributeMetadata attr)
        {
            // Exclude virtual attributes
            if (attr.AttributeType?.Equals("Virtual", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            // Exclude primary key (it's used for the JOIN, not as a display column)
            if (attr.LogicalName.Equals(_targetTablePrimaryKey, StringComparison.OrdinalIgnoreCase))
                return true;

            // Exclude state/status codes
            if (attr.LogicalName.Equals("statecode", StringComparison.OrdinalIgnoreCase) ||
                attr.LogicalName.Equals("statuscode", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void ListViewAttributes_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            if (_isLoading) return;
            UpdateStatus();
            UpdateWarnings();
        }

        private void UpdateStatus()
        {
            var checkedCount = listViewAttributes.CheckedItems.Count;
            lblStatus.Text = checkedCount == 0
                ? "No attributes selected (will remove expansion)."
                : $"{checkedCount} attribute{(checkedCount == 1 ? "" : "s")} selected.";
            btnOk.Enabled = true;
        }

        private void UpdateWarnings()
        {
            var checkedCount = listViewAttributes.CheckedItems.Count;
            var warnings = new List<string>();

            if (checkedCount >= MAX_RECOMMENDED_FIELDS)
            {
                warnings.Add($"\u26a0 {checkedCount} fields selected. Selecting {MAX_RECOMMENDED_FIELDS}+ fields from a single expanded lookup may impact DirectQuery performance.");
            }

            // +1 because adding this expand counts toward the total
            var totalExpands = _currentExpandCount + 1;
            if (totalExpands >= MAX_RECOMMENDED_EXPANDS)
            {
                warnings.Add($"\u26a0 This table has {totalExpands} expanded lookups. {MAX_RECOMMENDED_EXPANDS}+ expanded lookups on a single table may impact performance.");
            }

            if (warnings.Count > 0)
            {
                lblWarning.Text = string.Join("\r\n", warnings);
                pnlWarning.Visible = true;
                pnlWarning.Height = warnings.Count > 1 ? 50 : 35;
            }
            else
            {
                pnlWarning.Visible = false;
            }
        }

        private void BtnSelectAll_Click(object? sender, EventArgs e)
        {
            _isLoading = true;
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                item.Checked = true;
            }
            _isLoading = false;
            UpdateStatus();
            UpdateWarnings();
        }

        private void BtnDeselectAll_Click(object? sender, EventArgs e)
        {
            _isLoading = true;
            foreach (ListViewItem item in listViewAttributes.Items)
            {
                item.Checked = false;
            }
            _isLoading = false;
            UpdateStatus();
            UpdateWarnings();
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            SelectedAttributes = listViewAttributes.CheckedItems.Cast<ListViewItem>()
                .Where(item => item.Tag is CoreAttributeMetadata)
                .Select(item =>
                {
                    var attr = (CoreAttributeMetadata)item.Tag;
                    return new ExpandedLookupAttribute
                    {
                        LogicalName = attr.LogicalName,
                        DisplayName = attr.DisplayName,
                        AttributeType = attr.AttributeType,
                        SchemaName = attr.SchemaName,
                        Targets = attr.Targets,
                        VirtualAttributeName = attr.VirtualAttributeName,
                        IsGlobal = attr.IsGlobal,
                        OptionSetName = attr.OptionSetName
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Helper class for form combo box items.
        /// </summary>
        private class FormComboItem
        {
            public FormMetadata Form { get; }
            public int FieldCount { get; }

            public FormComboItem(FormMetadata form, int fieldCount)
            {
                Form = form;
                FieldCount = fieldCount;
            }

            public override string ToString()
            {
                return $"{Form.Name} ({FieldCount} fields)";
            }
        }
    }
}
