// ===================================================================================
// SolutionSelectorForm.cs - Dataverse Solution Selection Dialog
// ===================================================================================
//
// PURPOSE:
// Displays a dropdown of available Dataverse solutions and lets the user select
// one to load tables from. This is typically the first step in configuring a
// new semantic model.
//
// WHY SOLUTIONS:
// Solutions in Dataverse are containers for tables, forms, views, and other
// components. Selecting by solution:
// - Filters to relevant business tables (excludes system entities)
// - Provides a logical grouping of related tables
// - Matches how administrators organize their Dataverse customizations
//
// DISPLAY:
// - Dropdown sorted alphabetically by friendly name
// - Pre-selects the current solution if editing an existing model
// - Simple OK/Cancel dialog pattern
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
    /// Simple dialog to select a solution before showing the table selector
    /// </summary>
    public class SolutionSelectorForm : Form
    {
        private ComboBox cboSolutions = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Label lblInstruction = null!;
        
        public DataverseSolution? SelectedSolution { get; private set; }
        
        public SolutionSelectorForm(List<DataverseSolution> solutions, string currentSolutionId)
        {
            InitializeComponent();
            
            foreach (var solution in solutions.OrderBy(s => s.FriendlyName))
            {
                cboSolutions.Items.Add(solution);
                
                if (solution.SolutionId == currentSolutionId)
                    cboSolutions.SelectedItem = solution;
            }
            
            if (cboSolutions.SelectedIndex < 0 && cboSolutions.Items.Count > 0)
                cboSolutions.SelectedIndex = 0;
        }
        
        private void InitializeComponent()
        {
            this.lblInstruction = new Label();
            this.cboSolutions = new ComboBox();
            this.btnOk = new Button();
            this.btnCancel = new Button();
            
            this.SuspendLayout();
            
            // lblInstruction
            this.lblInstruction.AutoSize = true;
            this.lblInstruction.Location = new Point(12, 15);
            this.lblInstruction.Size = new Size(176, 15);
            this.lblInstruction.Text = "Select a solution to load tables from:";
            
            // cboSolutions
            this.cboSolutions.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cboSolutions.Location = new Point(12, 40);
            this.cboSolutions.Size = new Size(360, 23);
            this.cboSolutions.DisplayMember = "FriendlyName";
            
            // btnOk
            this.btnOk.Location = new Point(216, 80);
            this.btnOk.Size = new Size(75, 28);
            this.btnOk.Text = "OK";
            this.btnOk.DialogResult = DialogResult.OK;
            this.btnOk.Click += BtnOk_Click;
            
            // btnCancel
            this.btnCancel.Location = new Point(297, 80);
            this.btnCancel.Size = new Size(75, 28);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.DialogResult = DialogResult.Cancel;
            
            // Form
            this.ClientSize = new Size(384, 120);
            this.Controls.Add(this.lblInstruction);
            this.Controls.Add(this.cboSolutions);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Select Solution";
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void BtnOk_Click(object sender, EventArgs e)
        {
            SelectedSolution = cboSolutions.SelectedItem as DataverseSolution;
        }
    }
}
