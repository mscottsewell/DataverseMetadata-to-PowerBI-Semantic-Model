// ===================================================================================
// PasteAttributesDialog.cs - Bulk Attribute Selection via Pasted Logical Names
// ===================================================================================
//
// PURPOSE:
// Provides a dialog where users can paste a list of attribute logical names
// (comma-separated or one per line) to quickly select multiple attributes
// on the current table without clicking each checkbox individually.
//
// UI FLOW:
// 1. User clicks the clipboard button in the attribute filter panel
// 2. Dialog opens with a multi-line text box for pasting logical names
// 3. User clicks "Select" to match names against the current table's attributes
// 4. Dialog reports how many were matched and lists any unrecognized names
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Dialog for pasting a list of attribute logical names to bulk-select attributes.
    /// </summary>
    public class PasteAttributesDialog : Form
    {
        private Label lblInstructions = null!;
        private TextBox txtAttributeNames = null!;
        private Button btnSelect = null!;
        private Button btnCancel = null!;

        /// <summary>
        /// The parsed attribute logical names entered by the user.
        /// </summary>
        public List<string> ParsedAttributeNames { get; private set; } = new List<string>();

        public PasteAttributesDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Quick Select List of Attributes";
            this.Size = new Size(480, 380);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblInstructions = new Label
            {
                Text = "Paste attribute logical names below, separated by comma, or one per line.\nThese will be added to the 'selected' attributes for the current entity \nNo attributes will be de-selected.",
                Location = new Point(15, 15),
                Size = new Size(435, 50),
                AutoSize = false
            };
            this.Controls.Add(lblInstructions);

            txtAttributeNames = new TextBox
            {
                Location = new Point(15, 65),
                Size = new Size(435, 225),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                Font = new Font("Consolas", 9f)
            };
            this.Controls.Add(txtAttributeNames);

            btnSelect = new Button
            {
                Text = "Select",
                Location = new Point(290, 300),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK
            };
            btnSelect.Click += BtnSelect_Click;
            this.Controls.Add(btnSelect);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(375, 300),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSelect;
            this.CancelButton = btnCancel;
        }

        private void BtnSelect_Click(object? sender, EventArgs e)
        {
            // Parse the text: split by commas and newlines, trim whitespace, remove empties
            var raw = txtAttributeNames.Text;
            var names = raw
                .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ParsedAttributeNames = names;
        }
    }
}
