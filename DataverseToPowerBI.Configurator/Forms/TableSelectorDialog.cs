// =============================================================================
// TableSelectorDialog.cs - Solution and Table Selection Dialog
// =============================================================================
// Purpose: Dialog for selecting Dataverse tables from a solution.
//
// This dialog provides a two-step selection process:
//   1. Select a solution from the dropdown (loads unmanaged solutions)
//   2. Select tables from the solution's entities (checkbox list)
//
// Features:
//   - Loads solutions asynchronously with progress indicator
//   - Filters to unmanaged solutions only
//   - Dynamically loads tables when solution is selected
//   - Supports column sorting (click column header)
//   - Pre-checks tables that are currently selected (for editing)
//   - Returns list of selected TableInfo objects
//
// Note: This is a basic table selector. For star-schema configuration,
// use FactDimensionSelectorDialog instead.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Models;
using DataverseToPowerBI.Configurator.Services;

namespace DataverseToPowerBI.Configurator.Forms
{
    /// <summary>
    /// Dialog for selecting Dataverse tables from a solution.
    /// </summary>
    /// <remarks>
    /// Allows multi-selection of tables with checkbox list.
    /// Tables are loaded dynamically when a solution is selected.
    /// </remarks>
    public class TableSelectorDialog : Form
    {
        private readonly DataverseClient _client;
        private List<DataverseSolution> _solutions = new();
        private List<TableInfo> _tables = new();
        private List<string> _currentlySelectedTables = new();
        
        private ComboBox cmbSolutions;
        private ListView listViewTables;
        private Button btnOK;
        private Button btnCancel;
        private Label lblStatus;
        private ProgressBar progressBar;

        // Sorting state
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        public string? SelectedSolutionName { get; private set; }
        public List<TableInfo> SelectedTables { get; private set; } = new();

        public TableSelectorDialog(DataverseClient client, string? currentSolution, List<string>? currentlySelectedTables = null)
        {
            _client = client;
            SelectedSolutionName = currentSolution;
            _currentlySelectedTables = currentlySelectedTables ?? new();
            InitializeComponent();
            LoadSolutions();
        }

        private void InitializeComponent()
        {
            this.Text = "Select Solution and Tables";
            this.Width = 900;
            this.Height = 600;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblSolution = new Label
            {
                Text = "Solution:",
                Location = new System.Drawing.Point(10, 15),
                AutoSize = true
            };

            cmbSolutions = new ComboBox
            {
                Location = new System.Drawing.Point(10, 35),
                Width = 620,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSolutions.SelectedIndexChanged += CmbSolutions_SelectedIndexChanged;

            var lblTables = new Label
            {
                Text = "Available Tables (select multiple):",
                Location = new System.Drawing.Point(10, 70),
                AutoSize = true
            };

            listViewTables = new ListView
            {
                Location = new System.Drawing.Point(10, 95),
                Width = 860,
                Height = 370,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true
            };
            listViewTables.Columns.Add("Display Name", 400);
            listViewTables.Columns.Add("Logical Name", 400);
            listViewTables.ColumnClick += ListViewTables_ColumnClick;

            lblStatus = new Label
            {
                Text = "Ready",
                Location = new System.Drawing.Point(10, 475),
                Width = 860
            };

            progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(10, 500),
                Width = 860,
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            btnOK = new Button
            {
                Text = "Add Selected",
                Location = new System.Drawing.Point(690, 520),
                Width = 90,
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(790, 520),
                Width = 80,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblSolution);
            this.Controls.Add(cmbSolutions);
            this.Controls.Add(lblTables);
            this.Controls.Add(listViewTables);
            this.Controls.Add(lblStatus);
            this.Controls.Add(progressBar);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
        }

        private async void LoadSolutions()
        {
            try
            {
                lblStatus.Text = "Loading solutions...";
                progressBar.Visible = true;
                cmbSolutions.Enabled = false;

                var allSolutions = await _client.GetSolutionsAsync();
                
                // Filter to only show unmanaged solutions
                _solutions = allSolutions.Where(s => !s.IsManaged).ToList();

                cmbSolutions.Items.Clear();
                foreach (var solution in _solutions.OrderBy(s => s.FriendlyName))
                {
                    cmbSolutions.Items.Add(solution.FriendlyName);
                }

                // Select current solution if specified (restoring from previous session)
                if (!string.IsNullOrEmpty(SelectedSolutionName))
                {
                    var index = _solutions.FindIndex(s => s.UniqueName == SelectedSolutionName);
                    if (index >= 0)
                        cmbSolutions.SelectedIndex = index;
                }
                else
                {
                    // Try to select a preferred solution (most recently modified)
                    var preferred = _solutions
                        .Where(s => s.ModifiedOn.HasValue)
                        .OrderByDescending(s => s.ModifiedOn)
                        .FirstOrDefault();
                    
                    if (preferred != null)
                    {
                        var index = _solutions.IndexOf(preferred);
                        if (index >= 0)
                            cmbSolutions.SelectedIndex = index;
                    }
                }

                lblStatus.Text = $"Loaded {_solutions.Count} solutions.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to load solutions:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
                cmbSolutions.Enabled = true;
            }
        }

        private async void CmbSolutions_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Auto-load tables when solution is selected
            await LoadTablesForSelectedSolution();
        }

        private async Task LoadTablesForSelectedSolution()
        {
            if (cmbSolutions.SelectedIndex < 0)
            {
                return;
            }

            var solution = _solutions[cmbSolutions.SelectedIndex];
            SelectedSolutionName = solution.UniqueName;

            try
            {
                cmbSolutions.Enabled = false;
                lblStatus.Text = $"Loading tables from {solution.FriendlyName}...";
                progressBar.Visible = true;

                _tables = await _client.GetSolutionTablesAsync(solution.SolutionId);

                listViewTables.Items.Clear();
                foreach (var table in _tables.OrderBy(t => t.DisplayName))
                {
                    var item = new ListViewItem(table.DisplayName ?? table.LogicalName);
                    item.SubItems.Add(table.LogicalName);
                    item.Tag = table;
                    
                    // Pre-check if this table is currently selected
                    if (_currentlySelectedTables.Contains(table.LogicalName))
                        item.Checked = true;
                    
                    listViewTables.Items.Add(item);
                }

                lblStatus.Text = $"Loaded {_tables.Count} tables.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to load tables:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cmbSolutions.Enabled = true;
                progressBar.Visible = false;
            }
        }

        private void ListViewTables_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Toggle sort direction if same column, otherwise sort ascending
            if (e.Column == _sortColumn)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }

            // Get all items as array
            var items = listViewTables.Items.Cast<ListViewItem>().ToArray();
            
            // Sort items
            var sorted = _sortAscending
                ? items.OrderBy(item => item.SubItems[e.Column].Text)
                : items.OrderByDescending(item => item.SubItems[e.Column].Text);

            // Clear and re-add sorted items
            listViewTables.Items.Clear();
            listViewTables.Items.AddRange(sorted.ToArray());
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            SelectedTables = listViewTables.CheckedItems
                .Cast<ListViewItem>()
                .Select(i => (TableInfo)i.Tag)
                .ToList();

            if (!SelectedTables.Any())
            {
                MessageBox.Show("Please select at least one table.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
            }
        }
    }
}
