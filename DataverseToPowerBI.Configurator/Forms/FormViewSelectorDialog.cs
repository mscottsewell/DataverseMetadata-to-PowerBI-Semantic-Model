using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Models;

namespace DataverseToPowerBI.Configurator.Forms
{
    public class FormViewSelectorDialog : Form
    {
        private ComboBox cmbForm;
        private ComboBox cmbView;
        private Button btnOK;
        private Button btnCancel;

        public FormMetadata? SelectedForm { get; private set; }
        public ViewMetadata? SelectedView { get; private set; }

        public FormViewSelectorDialog(
            List<FormMetadata> forms,
            List<ViewMetadata> views,
            FormMetadata? currentForm = null,
            ViewMetadata? currentView = null)
        {
            InitializeComponent();
            LoadForms(forms, currentForm);
            LoadViews(views, currentView);
        }

        private void InitializeComponent()
        {
            this.Text = "Select Form and View";
            this.Width = 500;
            this.Height = 200;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblForm = new Label
            {
                Text = "Form:",
                Location = new System.Drawing.Point(10, 15),
                AutoSize = true
            };

            cmbForm = new ComboBox
            {
                Location = new System.Drawing.Point(10, 35),
                Width = 460,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblView = new Label
            {
                Text = "View:",
                Location = new System.Drawing.Point(10, 75),
                AutoSize = true
            };

            cmbView = new ComboBox
            {
                Location = new System.Drawing.Point(10, 95),
                Width = 460,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            btnOK = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(300, 130),
                Width = 80,
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(390, 130),
                Width = 80,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblForm);
            this.Controls.Add(cmbForm);
            this.Controls.Add(lblView);
            this.Controls.Add(cmbView);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void LoadForms(List<FormMetadata> forms, FormMetadata? currentForm)
        {
            cmbForm.Items.Clear();

            if (!forms.Any())
            {
                cmbForm.Items.Add("(No forms available)");
                cmbForm.SelectedIndex = 0;
                cmbForm.Enabled = false;
                return;
            }

            foreach (var form in forms.OrderBy(f => f.Name))
            {
                cmbForm.Items.Add(form);
            }

            cmbForm.DisplayMember = "Name";

            // Select current form if specified
            if (currentForm != null)
            {
                var index = forms.FindIndex(f => f.FormId == currentForm.FormId);
                if (index >= 0)
                    cmbForm.SelectedIndex = index;
            }
            else if (cmbForm.Items.Count > 0)
            {
                cmbForm.SelectedIndex = 0;
            }
        }

        private void LoadViews(List<ViewMetadata> views, ViewMetadata? currentView)
        {
            cmbView.Items.Clear();

            if (!views.Any())
            {
                cmbView.Items.Add("(No views available)");
                cmbView.SelectedIndex = 0;
                cmbView.Enabled = false;
                return;
            }

            foreach (var view in views.OrderBy(v => v.Name))
            {
                cmbView.Items.Add(view);
            }

            cmbView.DisplayMember = "Name";

            // Select current view if specified
            if (currentView != null)
            {
                var index = views.FindIndex(v => v.ViewId == currentView.ViewId);
                if (index >= 0)
                    cmbView.SelectedIndex = index;
            }
            else if (cmbView.Items.Count > 0)
            {
                cmbView.SelectedIndex = 0;
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            // Get selected form (if enabled)
            if (cmbForm.Enabled && cmbForm.SelectedItem is FormMetadata form)
            {
                SelectedForm = form;
            }

            // Get selected view (if enabled)
            if (cmbView.Enabled && cmbView.SelectedItem is ViewMetadata view)
            {
                SelectedView = view;
            }
        }
    }
}
