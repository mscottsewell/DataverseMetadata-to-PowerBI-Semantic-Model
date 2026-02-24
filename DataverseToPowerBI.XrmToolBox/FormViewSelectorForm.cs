// ===================================================================================
// FormViewSelectorForm.cs - Form and View Selection Dialog for XrmToolBox
// ===================================================================================
//
// PURPOSE:
// This dialog allows users to select a Saved View (for row filtering) and a
// "Default Field Selection" mode for a table. The three modes are:
//
// USE VIEW:
// - Uses columns from the selected view as the default field list.
// - If the view includes link-entity columns (related entity fields), they
//   can be added as expanded lookup attributes.
//
// SELECT FORM:
// - Uses fields from the selected System Form as the default field list.
// - The traditional approach: form fields define which columns to include.
//
// ADD CUSTOM:
// - No automatic field selection; the user manually picks individual fields.
//
// VIEW SELECTION (Row Filter):
// - Saved views contain FetchXML filters that limit which rows are returned.
// - The FetchXML is converted to SQL WHERE clauses for DirectQuery.
// - Always shown regardless of the field-selection mode.
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DataverseToPowerBI.Core.Models;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Dialog for selecting view (row filter) and default field selection mode for a table.
    /// </summary>
    public class FormViewSelectorForm : Form
    {
        #region Fields

        private ComboBox cboView = null!;
        private ComboBox cboFieldView = null!;
        private ComboBox cboForm = null!;
        private RadioButton radioUseView = null!;
        private RadioButton radioUseDifferentView = null!;
        private RadioButton radioSelectForm = null!;
        private RadioButton radioAddCustom = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Label lblViewColumns = null!;
        private Label lblFieldViewColumns = null!;
        private Label lblFormFields = null!;
        private GroupBox grpFieldSelection = null!;

        private readonly List<FormMetadata> _forms;
        private readonly List<ViewMetadata> _views;

        #endregion

        #region Public Properties

        /// <summary>Selected form ID (null if not using form mode).</summary>
        public string? SelectedFormId { get; private set; }

        /// <summary>Selected view ID (null if "All records").</summary>
        public string? SelectedViewId { get; private set; }

        /// <summary>Selected field-source view ID (null if using row filter view or not in different-view mode).</summary>
        public string? SelectedFieldViewId { get; private set; }

        /// <summary>The chosen field selection mode.</summary>
        public FieldSelectionMode FieldSelectionMode { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new form/view selector dialog.
        /// </summary>
        public FormViewSelectorForm(
            string tableName,
            List<FormMetadata> forms,
            List<ViewMetadata> views,
            string? currentFormId,
            string? currentViewId,
            string? currentFieldViewId,
            FieldSelectionMode currentMode = FieldSelectionMode.View)
        {
            _forms = forms;
            _views = views;
            SelectedFormId = currentFormId;
            SelectedViewId = currentViewId;
            SelectedFieldViewId = currentFieldViewId;
            FieldSelectionMode = currentMode;

            InitializeComponent(tableName);
            PopulateDropdowns();
            ApplyCurrentMode(currentMode);
        }

        #endregion

        #region UI Initialization

        private void InitializeComponent(string tableName)
        {
            this.Text = $"Select View & Default Fields - {tableName}";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Use the form's actual font to measure text so layout works at any DPI
            var font = this.Font;
            var hintFont = font; // hints use same font, just gray color
            int pad = 16;       // outer padding
            int innerPad = 30;  // indent inside group box for hints/dropdowns

            // Measure the widest hint text to determine minimum form width
            var hintTexts = new[]
            {
                "Selects fields based on the columns defined in the view above.",
                "Selects fields based on the columns defined in a different Dataverse view.",
                "Selects fields based on the fields displayed on a Dataverse form.",
                "No automatic field selection. Select fields manually from the list."
            };
            int maxHintWidth = 0;
            foreach (var t in hintTexts)
            {
                var sz = TextRenderer.MeasureText(t, hintFont);
                if (sz.Width > maxHintWidth) maxHintWidth = sz.Width;
            }
            // Group box inner width = innerPad + hint text + some margin
            int grpInnerWidth = innerPad + maxHintWidth + 20;
            int grpWidth = Math.Max(grpInnerWidth, 440);
            int formClientWidth = grpWidth + pad * 2;

            // Measure one line height for spacing calculations
            int lineHeight = TextRenderer.MeasureText("Ay", font).Height;
            int hintLineHeight = TextRenderer.MeasureText("Ay", hintFont).Height;
            int comboHeight = lineHeight + 6;
            int radioHeight = lineHeight + 4;
            int spacing = 6;

            int y = pad;

            // --- View (Row Filter) section ---
            var lblView = new Label { AutoSize = true, Location = new Point(pad + 4, y + 3), Text = "View (Row Filter):" };
            int cboViewLeft = pad + 4 + TextRenderer.MeasureText(lblView.Text, font).Width + 8;
            cboView = new ComboBox
            {
                Location = new Point(cboViewLeft, y),
                Size = new Size(formClientWidth - cboViewLeft - pad, comboHeight),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboView.SelectedIndexChanged += CboView_SelectedIndexChanged;
            y += comboHeight + spacing;

            lblViewColumns = new Label
            {
                AutoSize = true,
                Location = new Point(cboViewLeft, y),
                ForeColor = Color.Gray,
                Text = ""
            };
            y += hintLineHeight + spacing;

            // --- Default Field Selection group ---
            grpFieldSelection = new GroupBox
            {
                Text = "Default Field Selection",
                Location = new Point(pad, y),
                Width = grpWidth
            };
            int gy = lineHeight + spacing; // start below group header

            radioUseView = new RadioButton
            {
                Text = "Use Row Filter View's Columns",
                Location = new Point(12, gy),
                AutoSize = true
            };
            radioUseView.CheckedChanged += RadioMode_CheckedChanged;
            gy += radioHeight + spacing;

            var lblViewHint = new Label
            {
                Text = hintTexts[0],
                Location = new Point(innerPad, gy),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            gy += hintLineHeight + spacing + 2;

            radioUseDifferentView = new RadioButton
            {
                Text = "Use a Different View's Columns",
                Location = new Point(12, gy),
                AutoSize = true
            };
            radioUseDifferentView.CheckedChanged += RadioMode_CheckedChanged;
            gy += radioHeight + spacing;

            var lblDifferentViewHint = new Label
            {
                Text = hintTexts[1],
                Location = new Point(innerPad, gy),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            gy += hintLineHeight + spacing;

            cboFieldView = new ComboBox
            {
                Location = new Point(innerPad, gy),
                Size = new Size(grpWidth - innerPad - 16, comboHeight),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboFieldView.SelectedIndexChanged += CboFieldView_SelectedIndexChanged;
            gy += comboHeight + spacing;

            lblFieldViewColumns = new Label
            {
                AutoSize = true,
                Location = new Point(innerPad, gy),
                ForeColor = Color.Gray,
                Text = ""
            };
            gy += hintLineHeight + spacing + 2;

            radioSelectForm = new RadioButton
            {
                Text = "Select Form",
                Location = new Point(12, gy),
                AutoSize = true
            };
            radioSelectForm.CheckedChanged += RadioMode_CheckedChanged;
            gy += radioHeight + spacing;

            var lblFormHint = new Label
            {
                Text = hintTexts[2],
                Location = new Point(innerPad, gy),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            gy += hintLineHeight + spacing;

            cboForm = new ComboBox
            {
                Location = new Point(innerPad, gy),
                Size = new Size(grpWidth - innerPad - 16, comboHeight),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboForm.SelectedIndexChanged += CboForm_SelectedIndexChanged;
            gy += comboHeight + spacing;

            lblFormFields = new Label
            {
                AutoSize = true,
                Location = new Point(innerPad, gy),
                ForeColor = Color.Gray,
                Text = ""
            };
            gy += hintLineHeight + spacing + 2;

            radioAddCustom = new RadioButton
            {
                Text = "Add Custom",
                Location = new Point(12, gy),
                AutoSize = true
            };
            radioAddCustom.CheckedChanged += RadioMode_CheckedChanged;
            gy += radioHeight + spacing;

            var lblCustomHint = new Label
            {
                Text = hintTexts[3],
                Location = new Point(innerPad, gy),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            gy += hintLineHeight + spacing;

            grpFieldSelection.Height = gy + spacing;

            grpFieldSelection.Controls.AddRange(new Control[] {
                radioUseView, lblViewHint,
                radioUseDifferentView, lblDifferentViewHint, cboFieldView, lblFieldViewColumns,
                radioSelectForm, lblFormHint, cboForm, lblFormFields,
                radioAddCustom, lblCustomHint
            });

            y += grpFieldSelection.Height + spacing * 2;

            int btnWidth = 90;
            int btnHeight = lineHeight + 12;
            btnOk = new Button
            {
                Location = new Point(formClientWidth - pad - btnWidth * 2 - spacing, y),
                Size = new Size(btnWidth, btnHeight),
                Text = "OK"
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button
            {
                Location = new Point(formClientWidth - pad - btnWidth, y),
                Size = new Size(btnWidth, btnHeight),
                Text = "Cancel"
            };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            this.Controls.AddRange(new Control[] {
                lblView, cboView, lblViewColumns,
                grpFieldSelection,
                btnOk, btnCancel
            });

            this.ClientSize = new Size(formClientWidth, y + btnHeight + pad);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        #endregion

        #region Data Population

        private void PopulateDropdowns()
        {
            // --- Views ---
            cboView.Items.Add(new ViewItem(null, "(All records - No filter)"));

            // System views first, then personal views
            var systemViews = _views.Where(v => !v.IsPersonal).OrderBy(v => v.Name);
            var personalViews = _views.Where(v => v.IsPersonal).OrderBy(v => v.Name);

            foreach (var view in systemViews)
            {
                var suffix = view.IsDefault ? " (Default)" : "";
                cboView.Items.Add(new ViewItem(view, view.Name + suffix));
                cboFieldView.Items.Add(new ViewItem(view, view.Name + suffix));
            }

            if (personalViews.Any())
            {
                // Separator-style label for personal views
                foreach (var view in personalViews)
                {
                    cboView.Items.Add(new ViewItem(view, $"[Personal] {view.Name}"));
                    cboFieldView.Items.Add(new ViewItem(view, $"[Personal] {view.Name}"));
                }
            }

            // Select current view
            if (SelectedViewId != null)
            {
                if (SelectedViewId.Length == 0)
                {
                    cboView.SelectedIndex = 0;
                }

                for (int i = 1; i < cboView.Items.Count; i++)
                {
                    if (cboView.Items[i] is ViewItem vi && vi.View?.ViewId == SelectedViewId)
                    {
                        cboView.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cboView.SelectedIndex < 0)
            {
                // Auto-select default view
                for (int i = 1; i < cboView.Items.Count; i++)
                {
                    if (cboView.Items[i] is ViewItem vi && vi.View?.IsDefault == true)
                    {
                        cboView.SelectedIndex = i;
                        break;
                    }
                }
                if (cboView.SelectedIndex < 0)
                    cboView.SelectedIndex = 0;
            }

            // --- Field Source Views (always require an actual view) ---
            if (!string.IsNullOrEmpty(SelectedFieldViewId))
            {
                for (int i = 0; i < cboFieldView.Items.Count; i++)
                {
                    if (cboFieldView.Items[i] is ViewItem vi && vi.View?.ViewId == SelectedFieldViewId)
                    {
                        cboFieldView.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cboFieldView.SelectedIndex < 0)
            {
                var fallbackViewId = SelectedViewId;
                if (!string.IsNullOrEmpty(fallbackViewId))
                {
                    for (int i = 0; i < cboFieldView.Items.Count; i++)
                    {
                        if (cboFieldView.Items[i] is ViewItem vi && vi.View?.ViewId == fallbackViewId)
                        {
                            cboFieldView.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            if (cboFieldView.SelectedIndex < 0)
            {
                var defaultFieldView = _views.FirstOrDefault(v => v.IsDefault) ?? _views.FirstOrDefault();
                if (defaultFieldView != null)
                {
                    for (int i = 0; i < cboFieldView.Items.Count; i++)
                    {
                        if (cboFieldView.Items[i] is ViewItem vi && vi.View?.ViewId == defaultFieldView.ViewId)
                        {
                            cboFieldView.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            // --- Forms ---
            cboForm.Items.Add(new FormItem(null, "(None)"));
            foreach (var form in _forms.OrderBy(f => f.Name))
            {
                cboForm.Items.Add(new FormItem(form, form.Name));
            }

            // Select current form
            if (!string.IsNullOrEmpty(SelectedFormId))
            {
                for (int i = 1; i < cboForm.Items.Count; i++)
                {
                    if (cboForm.Items[i] is FormItem fi && fi.Form?.FormId == SelectedFormId)
                    {
                        cboForm.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cboForm.SelectedIndex < 0)
                cboForm.SelectedIndex = 0;
        }

        private void ApplyCurrentMode(FieldSelectionMode mode)
        {
            switch (mode)
            {
                case FieldSelectionMode.View:
                    radioUseView.Checked = true;
                    break;
                case FieldSelectionMode.DifferentView:
                    radioUseDifferentView.Checked = true;
                    break;
                case FieldSelectionMode.Form:
                    radioSelectForm.Checked = true;
                    break;
                case FieldSelectionMode.Custom:
                    radioAddCustom.Checked = true;
                    break;
            }
            UpdateFormDropdownState();
        }

        #endregion

        #region Event Handlers

        private void RadioMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFormDropdownState();
        }

        private void UpdateFormDropdownState()
        {
            bool formMode = radioSelectForm.Checked;
            bool differentViewMode = radioUseDifferentView.Checked;

            cboForm.Enabled = formMode;
            lblFormFields.Visible = formMode;
            cboFieldView.Enabled = differentViewMode;
            lblFieldViewColumns.Visible = differentViewMode;

            if (differentViewMode && cboFieldView.SelectedItem is ViewItem vi)
            {
                SelectedFieldViewId = vi.View?.ViewId;
            }
            else if (!differentViewMode)
            {
                SelectedFieldViewId = null;
            }
        }

        private void CboView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboView.SelectedItem is ViewItem vi)
            {
                SelectedViewId = vi.View?.ViewId;
                if (vi.View != null)
                {
                    var colCount = vi.View.Columns.Count;
                    var linkedCount = vi.View.LinkedColumns.Count;
                    lblViewColumns.Text = linkedCount > 0
                        ? $"{colCount} columns, {linkedCount} from related tables"
                        : $"{colCount} columns";
                }
                else
                {
                    lblViewColumns.Text = "";
                }
            }
        }

        private void CboForm_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboForm.SelectedItem is FormItem fi)
            {
                SelectedFormId = fi.Form?.FormId;
                lblFormFields.Text = fi.Form?.Fields != null
                    ? $"{fi.Form.Fields.Count} fields on form"
                    : "";
            }
        }

        private void CboFieldView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboFieldView.SelectedItem is ViewItem vi)
            {
                SelectedFieldViewId = vi.View?.ViewId;
                if (vi.View != null)
                {
                    var colCount = vi.View.Columns.Count;
                    var linkedCount = vi.View.LinkedColumns.Count;
                    lblFieldViewColumns.Text = linkedCount > 0
                        ? $"{colCount} columns, {linkedCount} from related tables"
                        : $"{colCount} columns";
                }
                else
                {
                    lblFieldViewColumns.Text = "";
                }
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (cboView.SelectedItem is ViewItem selectedFilterView)
                SelectedViewId = selectedFilterView.View?.ViewId;

            if (cboFieldView.SelectedItem is ViewItem selectedFieldView)
                SelectedFieldViewId = selectedFieldView.View?.ViewId;

            if (radioUseView.Checked)
                FieldSelectionMode = FieldSelectionMode.View;
            else if (radioUseDifferentView.Checked)
                FieldSelectionMode = FieldSelectionMode.DifferentView;
            else if (radioSelectForm.Checked)
                FieldSelectionMode = FieldSelectionMode.Form;
            else
                FieldSelectionMode = FieldSelectionMode.Custom;

            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion

        #region Helper Types

        private class FormItem
        {
            public FormMetadata? Form { get; }
            private readonly string _display;
            public FormItem(FormMetadata? form, string display) { Form = form; _display = display; }
            public override string ToString() => _display;
        }

        private class ViewItem
        {
            public ViewMetadata? View { get; }
            private readonly string _display;
            public ViewItem(ViewMetadata? view, string display) { View = view; _display = display; }
            public override string ToString() => _display;
        }

        #endregion
    }
}
