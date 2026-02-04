// ===================================================================================
// FormViewSelectorForm.cs - Form and View Selection Dialog for XrmToolBox
// ===================================================================================
//
// PURPOSE:
// This dialog allows users to select a specific System Form and Saved View for
// a table when configuring the semantic model. This enables:
//
// FORM SELECTION (Column Source):
// - System forms define which fields appear in the UI
// - Using form columns ensures only expose user-facing fields
// - Reduces model size by excluding internal/system fields
//
// VIEW SELECTION (Row Filter):
// - Saved views contain FetchXML filters that limit which rows are returned
// - The FetchXML is converted to SQL WHERE clauses for DirectQuery
// - Enables pre-filtered tables (e.g., "Active Accounts Only")
//
// DISPLAY:
// Shows available forms and views in dropdowns with:
// - Form/View name and type
// - Number of columns/fields for forms
// - Preview information to help users choose
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
    /// Dialog for selecting form and view for a table
    /// </summary>
    public class FormViewSelectorForm : Form
    {
        private ComboBox cboForm = null!;
        private ComboBox cboView = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Label lblFormFields = null!;
        private Label lblViewColumns = null!;
        
        private List<FormMetadata> _forms;
        private List<ViewMetadata> _views;
        
        public string? SelectedFormId { get; private set; }
        public string? SelectedViewId { get; private set; }
        
        public FormViewSelectorForm(
            string tableName, 
            List<FormMetadata> forms, 
            List<ViewMetadata> views,
            string? currentFormId,
            string? currentViewId)
        {
            _forms = forms;
            _views = views;
            SelectedFormId = currentFormId;
            SelectedViewId = currentViewId;
            
            InitializeComponent(tableName);
            PopulateDropdowns();
        }
        
        private void InitializeComponent(string tableName)
        {
            this.Text = $"Select Form & View - {tableName}";
            this.Size = new Size(450, 280);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            var lblForm = new Label { AutoSize = true, Location = new Point(20, 25), Text = "Form:" };
            cboForm = new ComboBox
            {
                Location = new Point(100, 22),
                Size = new Size(300, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboForm.SelectedIndexChanged += CboForm_SelectedIndexChanged;
            
            lblFormFields = new Label
            {
                AutoSize = true,
                Location = new Point(100, 52),
                ForeColor = Color.Gray,
                Text = ""
            };
            
            var lblView = new Label { AutoSize = true, Location = new Point(20, 90), Text = "View (Filter):" };
            cboView = new ComboBox
            {
                Location = new Point(100, 87),
                Size = new Size(300, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboView.SelectedIndexChanged += CboView_SelectedIndexChanged;
            
            lblViewColumns = new Label
            {
                AutoSize = true,
                Location = new Point(100, 117),
                ForeColor = Color.Gray,
                Text = ""
            };
            
            var lblHint = new Label
            {
                Location = new Point(20, 150),
                Size = new Size(400, 50),
                Text = "Form: Used to determine which attributes to include in the model.\n" +
                       "View: Used to filter records exported to the semantic model."
            };
            
            btnOk = new Button
            {
                Location = new Point(240, 210),
                Size = new Size(90, 28),
                Text = "OK"
            };
            btnOk.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            
            btnCancel = new Button
            {
                Location = new Point(340, 210),
                Size = new Size(90, 28),
                Text = "Cancel"
            };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            
            this.Controls.AddRange(new Control[] {
                lblForm, cboForm, lblFormFields,
                lblView, cboView, lblViewColumns,
                lblHint, btnOk, btnCancel
            });
            
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
        
        private void PopulateDropdowns()
        {
            // Forms
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
            
            // Views
            cboView.Items.Add(new ViewItem(null, "(None - No filter)"));
            foreach (var view in _views.OrderBy(v => v.Name))
            {
                var suffix = view.IsDefault ? " (Default)" : "";
                cboView.Items.Add(new ViewItem(view, view.Name + suffix));
            }
            
            // Select current view
            if (!string.IsNullOrEmpty(SelectedViewId))
            {
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
        
        private void CboView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboView.SelectedItem is ViewItem vi)
            {
                SelectedViewId = vi.View?.ViewId;
                lblViewColumns.Text = vi.View?.Columns != null 
                    ? $"{vi.View.Columns.Count} columns" 
                    : "";
            }
        }
        
        private class FormItem
        {
            public FormMetadata Form { get; }
            private string _display;
            public FormItem(FormMetadata form, string display) { Form = form; _display = display; }
            public override string ToString() => _display;
        }
        
        private class ViewItem
        {
            public ViewMetadata View { get; }
            private string _display;
            public ViewItem(ViewMetadata view, string display) { View = view; _display = display; }
            public override string ToString() => _display;
        }
    }
}
