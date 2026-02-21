// ===================================================================================
// FactDimensionSelectorForm.cs - Star Schema Configuration for XrmToolBox
// ===================================================================================
//
// PURPOSE:
// This dialog enables users to configure a star-schema data model by selecting a
// central fact table and defining its relationships to dimension tables. It supports
// both direct relationships and snowflake dimensions (dimension-to-parent-dimension).
//
// STAR SCHEMA MODELING:
// - FACT TABLE: The central transactional table (e.g., Orders, Opportunities)
// - DIMENSION TABLES: Reference tables linked via lookup fields (e.g., Account, Product)
// - SNOWFLAKE: Parent dimensions linked from other dimensions (e.g., Account → Territory)
//
// RELATIONSHIP DETECTION:
// The dialog automatically discovers relationships by:
// 1. Loading all lookup-type attributes from the selected fact table
// 2. For each lookup, identifying the target table(s) it references
// 3. Grouping lookups by target to detect multiple paths to same table
//
// MULTIPLE RELATIONSHIPS TO SAME TABLE:
// When multiple lookups point to the same table (e.g., "Primary Contact" and
// "Secondary Contact" both pointing to Contact), only one can be "Active" in
// Power BI. Others become "Inactive" and require USERELATIONSHIP() in DAX.
// These are highlighted in yellow and marked with ⚠.
//
// USER INTERACTIONS:
// - Checkbox: Include/exclude relationship from model
// - Double-click: Toggle Active/Inactive status
// - Add Snowflake: Recursively add parent dimensions to selected dimension
//
// OUTPUT:
// - SelectedFactTable: The chosen fact table
// - SelectedRelationships: List of ExportRelationship configurations
// - AllSelectedTables: Complete list of tables to include in the model
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Xrm.Sdk;
using DataverseToPowerBI.Core.Models;

// Aliases to avoid conflicts with Microsoft.Xrm.Sdk types
using WinLabel = System.Windows.Forms.Label;
using CoreAttributeMetadata = DataverseToPowerBI.Core.Models.AttributeMetadata;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Dialog for selecting a Fact table and its Dimension relationships in a star-schema model.
    /// Ported from FactDimensionSelectorDialog for XrmToolBox.
    /// </summary>
    public class FactDimensionSelectorForm : Form
    {
        private readonly XrmServiceAdapterImpl _adapter;
        private readonly IOrganizationService _service;
        private List<TableInfo> _tables;
        private Dictionary<string, List<CoreAttributeMetadata>> _tableAttributes = new Dictionary<string, List<CoreAttributeMetadata>>();
        private Dictionary<string, string> _allEntityDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private List<DataverseSolution> _allSolutions = null!;
        private string _currentSolutionId = null!;

        // Existing configuration (for editing)
        private string _currentFactTable = null!;
        private List<ExportRelationship> _currentRelationships;

        // UI Controls
        private WinLabel lblSolution = null!;
        private ComboBox cmbSolution = null!;
        private CheckBox chkSolutionTablesOnly = null!;
        private WinLabel lblFactTable = null!;
        private ComboBox cmbFactTable = null!;
        private WinLabel lblFactHint = null!;
        private WinLabel lblDimensions = null!;
        private WinLabel lblDimHint = null!;
        private WinLabel lblSearch = null!;
        private TextBox txtSearch = null!;
        private CheckBox chkIncludeOneToMany = null!;
        private ListView listViewRelationships = null!;
        private Button btnAddSnowflake = null!;
        private Button btnFinish = null!;
        private Button btnCancel = null!;
        private WinLabel lblStatus = null!;
        private ProgressBar progressBar = null!;
        
        // Search and filter state
        private List<ListViewItem> _allRelationshipItems = new List<ListViewItem>();
        private bool _filterSolutionTablesOnly = true;
        private bool _suppressItemCheckedEvent = false;

        // Results
        public string SelectedSolutionName { get; private set; }
        public string SelectedSolutionId { get; private set; } = null!;
        public TableInfo SelectedFactTable { get; private set; } = null!;
        public List<ExportRelationship> SelectedRelationships { get; private set; } = new List<ExportRelationship>();
        public List<TableInfo> AllSelectedTables { get; private set; } = new List<TableInfo>();

        // Callback for when solution changes and tables need to be reloaded
        public Action<string, string, Action<List<TableInfo>>>? OnSolutionChangeRequested;

        public FactDimensionSelectorForm(
            XrmServiceAdapterImpl adapter,
            IOrganizationService service,
            string? solutionName,
            List<TableInfo> tables,
            string? currentFactTable = null,
            List<ExportRelationship>? currentRelationships = null,
            List<DataverseSolution>? allSolutions = null,
            string? currentSolutionId = null)
        {
            _adapter = adapter;
            _service = service;
            SelectedSolutionName = solutionName ?? "All Tables";
            SelectedSolutionId = currentSolutionId;
            _tables = tables;
            _currentFactTable = currentFactTable;
            _currentRelationships = currentRelationships ?? new List<ExportRelationship>();
            _allSolutions = allSolutions;
            _currentSolutionId = currentSolutionId;
            
            // Load all entity display names for resolving target tables outside of the solution
            _allEntityDisplayNames = adapter.GetAllEntityDisplayNamesSync(service);
            
            InitializeComponent();
            LoadSolutionDropdown();
            LoadFactTableDropdown();
            
            // Set focus to solution dropdown when form loads
            this.Shown += (s, e) => cmbSolution.Focus();
        }

        private void InitializeComponent()
        {
            this.Text = "Select Fact Table and Dimensions";
            this.Width = 900;
            this.Height = 720;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Solution dropdown (editable - allows changing solution)
            lblSolution = new WinLabel
            {
                Text = "Solution:",
                Location = new Point(10, 15),
                AutoSize = true
            };
            this.Controls.Add(lblSolution);

            cmbSolution = new ComboBox
            {
                Location = new Point(80, 12),
                Width = 400,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSolution.SelectedIndexChanged += CmbSolution_SelectedIndexChanged;
            this.Controls.Add(cmbSolution);

            chkSolutionTablesOnly = new CheckBox
            {
                Text = "Solution tables only",
                Location = new Point(490, 14),
                Width = 140,
                Checked = true
            };
            chkSolutionTablesOnly.CheckedChanged += ChkSolutionTablesOnly_CheckedChanged;
            this.Controls.Add(chkSolutionTablesOnly);

            // Fact table selector (dropdown)
            lblFactTable = new WinLabel
            {
                Text = "Fact Table:",
                Location = new Point(10, 50),
                AutoSize = true,
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(lblFactTable);

            cmbFactTable = new ComboBox
            {
                Location = new Point(100, 47),
                Width = 500,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFactTable.SelectedIndexChanged += CmbFactTable_SelectedIndexChanged;
            this.Controls.Add(cmbFactTable);

            lblFactHint = new WinLabel
            {
                Text = "(Select the central transactional table)",
                Location = new Point(610, 50),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblFactHint);

            // Dimension relationships section
            lblDimensions = new WinLabel
            {
                Text = "Dimension Relationships:",
                Location = new Point(10, 85),
                AutoSize = true,
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(lblDimensions);

            lblDimHint = new WinLabel
            {
                Text = "Check lookups to include as dimensions. Double-click to toggle Active/Inactive.",
                Location = new Point(10, 105),
                Width = 750,
                Height = 20,
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblDimHint);

            // One-to-many checkbox (moved to left, first position)
            chkIncludeOneToMany = new CheckBox
            {
                Text = "Include one-to-many relationships (Advanced: tables that reference this fact table)",
                Location = new Point(10, 132),
                Width = 580,
                AutoSize = false,
                Height = 20
            };
            chkIncludeOneToMany.CheckedChanged += ChkIncludeOneToMany_CheckedChanged;
            this.Controls.Add(chkIncludeOneToMany);

            // Search box (moved to far right, aligned with grid)
            lblSearch = new WinLabel
            {
                Text = "Search:",
                Location = new Point(615, 133),
                AutoSize = true
            };
            this.Controls.Add(lblSearch);

            txtSearch = new TextBox
            {
                Location = new Point(670, 130),
                Width = 200
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            this.Controls.Add(txtSearch);

            listViewRelationships = new ListView
            {
                Location = new Point(10, 165),
                Width = 860,
                Height = 390,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                ShowGroups = true
            };
            listViewRelationships.Columns.Add("Include", 55);
            listViewRelationships.Columns.Add("Cardinality", 70);
            listViewRelationships.Columns.Add("Lookup Field", 150);
            listViewRelationships.Columns.Add("Lookup Logical Name", 120);
            listViewRelationships.Columns.Add("Target Table", 145);
            listViewRelationships.Columns.Add("Target Logical Name", 120);
            listViewRelationships.Columns.Add("Status", 65);
            listViewRelationships.Columns.Add("Type", 130);
            listViewRelationships.ItemChecked += ListViewRelationships_ItemChecked;
            listViewRelationships.DoubleClick += ListViewRelationships_DoubleClick;
            listViewRelationships.SelectedIndexChanged += ListViewRelationships_SelectedIndexChanged;
            this.Controls.Add(listViewRelationships);

            // Snowflake button
            btnAddSnowflake = new Button
            {
                Text = "Add Parent Tables to Selected Dimension...",
                Location = new Point(10, 565),
                Width = 280,
                Height = 30,
                Enabled = false
            };
            btnAddSnowflake.Click += BtnAddSnowflake_Click;
            this.Controls.Add(btnAddSnowflake);

            // Status and progress
            lblStatus = new WinLabel
            {
                Text = "Select a Fact Table to begin.",
                Location = new Point(10, 610),
                Width = 600
            };
            this.Controls.Add(lblStatus);

            progressBar = new ProgressBar
            {
                Location = new Point(10, 640),
                Width = 600,
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            this.Controls.Add(progressBar);

            // Action buttons
            btnFinish = new Button
            {
                Text = "Finish Selection",
                Location = new Point(650, 630),
                Width = 105,
                Height = 30,
                Enabled = false
            };
            btnFinish.Click += BtnFinish_Click;
            this.Controls.Add(btnFinish);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(765, 630),
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);
            
            this.CancelButton = btnCancel;
        }

        private void LoadSolutionDropdown()
        {
            cmbSolution.Items.Clear();

            if (_allSolutions == null || _allSolutions.Count == 0)
            {
                // No solutions available (user doesn't have privilege) - show current name as only option
                cmbSolution.Items.Add(SelectedSolutionName ?? "All Tables");
                cmbSolution.SelectedIndex = 0;
                cmbSolution.Enabled = false;
                return;
            }

            int selectedIndex = 0;
            int index = 0;
            foreach (var solution in _allSolutions.OrderBy(s => s.FriendlyName))
            {
                cmbSolution.Items.Add(solution);
                if (solution.SolutionId == _currentSolutionId)
                {
                    selectedIndex = index;
                }
                index++;
            }

            if (cmbSolution.Items.Count > 0)
            {
                cmbSolution.SelectedIndex = selectedIndex;
            }
        }

        private bool _suppressSolutionChange = false;

        private void CmbSolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSolutionChange) return;

            var selectedSolution = cmbSolution.SelectedItem as DataverseSolution;
            if (selectedSolution == null) return;
            if (selectedSolution.SolutionId == _currentSolutionId) return;

            // Solution changed - need to reload tables
            _currentSolutionId = selectedSolution.SolutionId;
            SelectedSolutionName = selectedSolution.FriendlyName;
            SelectedSolutionId = selectedSolution.SolutionId;

            // Clear current selection
            listViewRelationships.Items.Clear();
            cmbFactTable.Items.Clear();
            cmbFactTable.Items.Add("-- Loading... --");
            cmbFactTable.SelectedIndex = 0;
            cmbFactTable.Enabled = false;
            SelectedFactTable = null;
            _currentFactTable = null;
            _currentRelationships = new List<ExportRelationship>();

            lblStatus.Text = $"Loading tables from {SelectedSolutionName}...";

            // Request table reload via callback
            if (OnSolutionChangeRequested != null)
            {
                OnSolutionChangeRequested(selectedSolution.SolutionId, selectedSolution.FriendlyName, (tables) =>
                {
                    // Called when tables are loaded
                    _tables = tables;
                    _tableAttributes.Clear();
                    cmbFactTable.Enabled = true;
                    LoadFactTableDropdown();
                });
            }
            else
            {
                // Fallback - load synchronously (not recommended)
                try
                {
                    _tables = _adapter.GetSolutionTablesSync(_service, selectedSolution.SolutionId);
                    _tableAttributes.Clear();
                    cmbFactTable.Enabled = true;
                    LoadFactTableDropdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading tables: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    cmbFactTable.Enabled = true;
                    LoadFactTableDropdown();
                }
            }
        }

        private void LoadFactTableDropdown()
        {
            cmbFactTable.Items.Clear();
            cmbFactTable.Items.Add("-- Select Fact Table --");
            
            foreach (var table in _tables.OrderBy(t => t.DisplayName ?? t.LogicalName))
            {
                cmbFactTable.Items.Add($"{table.DisplayName ?? table.LogicalName} ({table.LogicalName})");
            }
            cmbFactTable.SelectedIndex = 0;

            // Pre-select if we have a current fact table
            if (!string.IsNullOrEmpty(_currentFactTable))
            {
                var factTable = _tables.FirstOrDefault(t => t.LogicalName == _currentFactTable);
                if (factTable != null)
                {
                    var sortedTables = _tables.OrderBy(t => t.DisplayName ?? t.LogicalName).ToList();
                    var index = sortedTables.IndexOf(factTable) + 1;
                    cmbFactTable.SelectedIndex = index;
                }
            }

            lblStatus.Text = $"Loaded {_tables.Count} tables. Select a Fact table.";
        }

        private void CmbFactTable_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbFactTable.SelectedIndex <= 0)
            {
                listViewRelationships.Items.Clear();
                SelectedFactTable = null;
                UpdateFinishButtonState();
                return;
            }

            var sortedTables = _tables.OrderBy(t => t.DisplayName ?? t.LogicalName).ToList();
            SelectedFactTable = sortedTables[cmbFactTable.SelectedIndex - 1];

            LoadFactTableRelationships();
        }

        private bool _suppressOneToManyWarning = false;

        private void ChkIncludeOneToMany_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressOneToManyWarning) return;

            if (chkIncludeOneToMany.Checked)
            {
                var result = MessageBox.Show(
                    "⚠️ WARNING: Including one-to-many relationships is an advanced feature.\n\n" +
                    "One-to-many relationships create detail tables (child records) rather than typical dimension tables.\n" +
                    "This can significantly increase the size of your semantic model and may cause performance issues.\n\n" +
                    "Do you want to continue?",
                    "Advanced Feature Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result == DialogResult.No)
                {
                    _suppressOneToManyWarning = true;
                    chkIncludeOneToMany.Checked = false;
                    _suppressOneToManyWarning = false;
                    return;
                }
            }

            if (SelectedFactTable != null)
            {
                LoadFactTableRelationships();
            }
        }

        private void LoadFactTableRelationships()
        {
            if (SelectedFactTable == null) return;

            try
            {
                lblStatus.Text = $"Loading lookups for {SelectedFactTable.DisplayName}...";
                progressBar.Visible = true;
                cmbFactTable.Enabled = false;
                Application.DoEvents();

                // Restore one-to-many checkbox state from existing config
                if (_currentRelationships.Any(r => r.IsSnowflake == false && string.IsNullOrEmpty(r.SourceAttribute) == false))
                {
                    // Check if any existing relationship is a reverse (has SourceTable != FactTable)
                    // This is a heuristic - in the Core model we don't have explicit IsReverse
                }

                // Fetch attributes for the fact table
                var attrs = _adapter.GetAttributesSync(_service, SelectedFactTable.LogicalName);
                _tableAttributes[SelectedFactTable.LogicalName] = attrs;

                // Get lookup attributes
                var lookups = attrs
                    .Where(a => a.AttributeType == "Lookup" && a.Targets != null && a.Targets.Any())
                    .OrderBy(a => a.DisplayName)
                    .ToList();

                PopulateRelationshipsListView(SelectedFactTable.LogicalName, lookups, isSnowflake: false);

                // Add one-to-many relationships if enabled
                if (chkIncludeOneToMany.Checked)
                {
                    LoadOneToManyRelationships();
                }

                // Restore snowflake relationships (level 1: dimension → parent)
                var factLookupTargets = lookups
                    .Where(l => l.Targets != null)
                    .SelectMany(l => l.Targets)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var snowflakeRels = _currentRelationships
                    .Where(r => r.IsSnowflake && factLookupTargets.Contains(r.SourceTable))
                    .ToList();

                foreach (var snowflakeRel in snowflakeRels)
                {
                    AddSnowflakeRelationshipToList(snowflakeRel, 1);
                }

                // Restore double-snowflake relationships (level 2: parent → grandparent)
                var level1Targets = snowflakeRels
                    .Select(r => r.TargetTable)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var doubleSnowflakeRels = _currentRelationships
                    .Where(r => r.IsSnowflake && level1Targets.Contains(r.SourceTable))
                    .ToList();

                foreach (var doubleSnowflakeRel in doubleSnowflakeRels)
                {
                    AddSnowflakeRelationshipToList(doubleSnowflakeRel, 2);
                }

                var totalSnowflake = snowflakeRels.Count + doubleSnowflakeRels.Count;
                var totalLookups = lookups.Count + totalSnowflake;
                lblStatus.Text = totalSnowflake > 0
                    ? $"Found {lookups.Count} lookup fields on {SelectedFactTable.DisplayName} + {totalSnowflake} snowflake relationship(s)."
                    : $"Found {lookups.Count} lookup fields on {SelectedFactTable.DisplayName}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to load lookups:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
                cmbFactTable.Enabled = true;
                UpdateFinishButtonState();
            }
        }

        private void LoadOneToManyRelationships()
        {
            if (SelectedFactTable == null) return;

            try
            {
                lblStatus.Text = $"Loading one-to-many relationships for {SelectedFactTable.DisplayName}...";
                Application.DoEvents();

                // Get one-to-many relationships from the adapter
                var oneToManyRels = _adapter.GetOneToManyRelationshipsSync(_service, SelectedFactTable.LogicalName);

                foreach (var rel in oneToManyRels)
                {
                    // Skip if already exists
                    if (_allRelationshipItems.Cast<ListViewItem>()
                        .Any(i => i != null && i.Tag != null && 
                                  ((RelationshipTag)i.Tag).SourceTable == rel.ReferencingEntity &&
                                  ((RelationshipTag)i.Tag).SourceAttribute == rel.ReferencingAttribute))
                        continue;

                    // Find referencing table display name
                    var referencingTable = _tables.FirstOrDefault(t => t.LogicalName == rel.ReferencingEntity);
                    var referencingDisplayName = referencingTable?.DisplayName 
                        ?? (_allEntityDisplayNames.TryGetValue(rel.ReferencingEntity, out var refDisplayName) ? refDisplayName : rel.ReferencingEntity);

                    var item = new ListViewItem("");
                    item.Checked = false; // One-to-many not auto-selected
                    item.SubItems.Add("1:Many");
                    item.SubItems.Add(rel.LookupDisplayName ?? rel.ReferencingAttribute);
                    item.SubItems.Add(rel.ReferencingAttribute);
                    item.SubItems.Add(referencingDisplayName);
                    item.SubItems.Add(rel.ReferencingEntity);
                    item.SubItems.Add("Active");
                    item.SubItems.Add("Reverse");
                    item.Tag = new RelationshipTag
                    {
                        SourceTable = rel.ReferencingEntity,
                        SourceAttribute = rel.ReferencingAttribute,
                        TargetTable = SelectedFactTable.LogicalName,
                        DisplayName = rel.LookupDisplayName ?? rel.ReferencingAttribute,
                        IsActive = true,
                        IsSnowflake = false,
                        IsOneToMany = true,
                        AssumeReferentialIntegrity = false,
                        IsChecked = false
                    };
                    item.BackColor = Color.LightGreen;
                    _allRelationshipItems.Add(item);
                }

                // Apply current search filter if any
                FilterRelationships();
                
                // Update status (check for null items in collection)
                var lookupCount = _allRelationshipItems.Cast<ListViewItem>()
                    .Count(i => i != null && i.Tag != null && !((RelationshipTag)i.Tag).IsOneToMany);
                var oneToManyCount = _allRelationshipItems.Cast<ListViewItem>()
                    .Count(i => i != null && i.Tag != null && ((RelationshipTag)i.Tag).IsOneToMany);
                
                var factDisplay = SelectedFactTable?.DisplayName ?? SelectedFactTable?.LogicalName ?? "fact table";
                lblStatus.Text = $"Found {lookupCount} lookup fields + {oneToManyCount} one-to-many relationships on {factDisplay}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading one-to-many: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading one-to-many relationships: {ex}");
            }
        }

        private void PopulateRelationshipsListView(string sourceTable, List<CoreAttributeMetadata> lookups, bool isSnowflake)
        {
            if (!isSnowflake)
            {
                _suppressItemCheckedEvent = true;
                listViewRelationships.Items.Clear();
                _allRelationshipItems.Clear();
                _suppressItemCheckedEvent = false;
            }

            // Group by target table to identify multiple lookups to same table
            var targetGroups = lookups
                .SelectMany(l => (l.Targets ?? new List<string>()).Select(t => new { Lookup = l, Target = t }))
                .GroupBy(x => x.Target)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Lookup).ToList());

            foreach (var lookup in lookups)
            {
                foreach (var target in lookup.Targets ?? new List<string>())
                {
                    // Skip if this target is the fact table itself (self-reference)
                    if (target == SelectedFactTable?.LogicalName && !isSnowflake) continue;

                    // Skip if already exists
                    if (_allRelationshipItems.Cast<ListViewItem>()
                        .Any(i => i != null && i.Tag != null && 
                                  ((RelationshipTag)i.Tag).SourceAttribute == lookup.LogicalName &&
                                  ((RelationshipTag)i.Tag).TargetTable == target))
                        continue;

                    // Find target table display name
                    var targetTable = _tables.FirstOrDefault(t => t.LogicalName == target);
                    var targetDisplayName = targetTable?.DisplayName 
                        ?? (_allEntityDisplayNames.TryGetValue(target, out var entityDisplayName) ? entityDisplayName : target);

                    // Check for multiple lookups to same target
                    var multipleLookupsToTarget = targetGroups.ContainsKey(target) && targetGroups[target].Count > 1;

                    // Check existing configuration
                    var existingRel = _currentRelationships.FirstOrDefault(r =>
                        r.SourceTable == sourceTable &&
                        r.SourceAttribute == lookup.LogicalName &&
                        r.TargetTable == target);

                    var isChecked = existingRel != null;
                    var isActive = existingRel?.IsActive ?? true; // Default to Active

                    // Auto-check single lookups to in-solution tables
                    if (existingRel == null && !multipleLookupsToTarget && targetTable != null && !isSnowflake)
                    {
                        isChecked = true;
                        isActive = true;
                    }

                    var statusText = isActive ? "Active" : "Inactive";
                    if (multipleLookupsToTarget) statusText += " ⚠";

                    var typeText = isSnowflake ? "Snowflake" : "Direct";
                    var cardinalityText = "Many:1";

                    var item = new ListViewItem("");
                    item.Checked = isChecked;
                    item.SubItems.Add(cardinalityText);
                    item.SubItems.Add(lookup.DisplayName ?? lookup.LogicalName);
                    item.SubItems.Add(lookup.LogicalName);
                    item.SubItems.Add(targetDisplayName);
                    item.SubItems.Add(target);
                    item.SubItems.Add(statusText);
                    item.SubItems.Add(typeText);
                    item.Tag = new RelationshipTag
                    {
                        SourceTable = sourceTable,
                        SourceAttribute = lookup.LogicalName,
                        TargetTable = target,
                        DisplayName = lookup.DisplayName ?? lookup.LogicalName,
                        IsActive = isActive,
                        IsSnowflake = existingRel?.IsSnowflake ?? isSnowflake,
                        AssumeReferentialIntegrity = lookup.IsRequired,
                        IsChecked = isChecked
                    };

                    // Color coding - highlight only if multiple ACTIVE relationships to same target
                    if (isSnowflake)
                    {
                        item.BackColor = Color.LightCyan;
                    }
                    // Color will be set in UpdateItemStatus based on active conflict check

                    _allRelationshipItems.Add(item);
                }
            }
            
            // Update all item statuses to apply correct colors and text
            foreach (var item in _allRelationshipItems)
            {
                if (item.Tag != null)
                {
                    UpdateItemStatus(item, (RelationshipTag)item.Tag);
                }
            }
            
            // Apply current search filter if any
            FilterRelationships();
            UpdateFinishButtonState();
        }

        private void ListViewRelationships_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            // Ignore if we're suppressing events during list manipulation
            if (_suppressItemCheckedEvent) return;
            
            // Null check to prevent crashes
            if (e?.Item?.Tag == null) return;
            
            var config = (RelationshipTag)e.Item.Tag;
            
            // Update the checked state in the tag
            config.IsChecked = e.Item.Checked;
            
            // When checking an item, set it to active and mark other relationships to the same target as inactive
            if (e.Item.Checked)
            {
                var conflictingActive = _allRelationshipItems
                    .Where(item => item != null && item != e.Item && item.Tag is RelationshipTag)
                    .Select(item => (Item: item, Config: (RelationshipTag)item.Tag))
                    .Where(x => x.Config.IsChecked && x.Config.IsActive &&
                                x.Config.SourceTable == config.SourceTable &&
                                x.Config.TargetTable == config.TargetTable)
                    .ToList();

                var keepCurrentActive = true;
                if (conflictingActive.Count > 0)
                {
                    var sourceDisplay = GetTableDisplayName(config.SourceTable);
                    var targetDisplay = GetTableDisplayName(config.TargetTable);
                    var existingFields = string.Join(", ", conflictingActive.Select(x => x.Config.DisplayName));

                    var promptResult = MessageBox.Show(
                        $"Ambiguous path detected between '{sourceDisplay}' and '{targetDisplay}'.\n\n" +
                        $"Current active relationship(s): {existingFields}\n" +
                        $"Newly selected relationship: {config.DisplayName}\n\n" +
                        "Yes = Make newly selected relationship Active and mark the other one(s) Inactive\n" +
                        "No = Keep existing relationship Active and mark this one Inactive\n" +
                        "Cancel = Undo this selection",
                        "Resolve Ambiguous Relationship Path",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1);

                    if (promptResult == DialogResult.Cancel)
                    {
                        _suppressItemCheckedEvent = true;
                        e.Item.Checked = false;
                        _suppressItemCheckedEvent = false;
                        config.IsChecked = false;
                        config.IsActive = false;
                        UpdateItemStatus(e.Item, config);
                        UpdateFinishButtonState();
                        UpdateSnowflakeButtonState();
                        return;
                    }

                    keepCurrentActive = promptResult == DialogResult.Yes;
                }

                config.IsActive = keepCurrentActive;

                if (keepCurrentActive)
                {
                    // Mark other relationships to the same target table as inactive
                    // Use _allRelationshipItems to affect all items, not just filtered ones
                    foreach (var otherItem in _allRelationshipItems)
                    {
                        if (otherItem == e.Item || otherItem.Tag == null) continue;
                        var otherConfig = (RelationshipTag)otherItem.Tag;
                        if (otherConfig.SourceTable == config.SourceTable &&
                            otherConfig.TargetTable == config.TargetTable)
                        {
                            otherConfig.IsActive = false;
                            UpdateItemStatus(otherItem, otherConfig);
                        }
                    }
                }
                
                // Update this item and all items to same target (to refresh conflict highlighting)
                UpdateItemStatus(e.Item, config);
                foreach (var otherItem in _allRelationshipItems)
                {
                    if (otherItem == e.Item || otherItem.Tag == null) continue;
                    var otherConfig = (RelationshipTag)otherItem.Tag;
                    if (otherConfig.SourceTable == config.SourceTable &&
                        otherConfig.TargetTable == config.TargetTable)
                    {
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }
            else
            {
                // When unchecking, normalize Active/Inactive states for this dimension
                // Find all checked relationships to the same target
                // Use the IsChecked property from the tag instead of item.Checked to avoid index errors
                var checkedToSameTarget = _allRelationshipItems
                    .Where(item => item != null && item.Tag != null)
                    .Where(item =>
                    {
                        var otherConfig = (RelationshipTag)item.Tag;
                        return otherConfig.IsChecked && otherConfig.SourceTable == config.SourceTable &&
                               otherConfig.TargetTable == config.TargetTable;
                    })
                    .ToList();
                
                // If exactly one relationship remains checked, make it Active; others Inactive
                if (checkedToSameTarget.Count == 1)
                {
                    var singleItem = checkedToSameTarget[0];
                    var singleConfig = (RelationshipTag)singleItem.Tag;
                    singleConfig.IsActive = true;
                    UpdateItemStatus(singleItem, singleConfig);
                    
                    // Mark all other relationships to this target as Inactive
                    foreach (var otherItem in _allRelationshipItems)
                    {
                        if (otherItem == singleItem || otherItem.Tag == null) continue;
                        var otherConfig = (RelationshipTag)otherItem.Tag;
                        if (otherConfig.SourceTable == config.SourceTable &&
                            otherConfig.TargetTable == config.TargetTable)
                        {
                            otherConfig.IsActive = false;
                            UpdateItemStatus(otherItem, otherConfig);
                        }
                    }
                }
                else if (checkedToSameTarget.Count == 0)
                {
                    // All relationships to this target are unchecked - mark all as Active
                    // (so whichever one user checks next will be ready to use)
                    foreach (var otherItem in _allRelationshipItems)
                    {
                        if (otherItem.Tag == null) continue;
                        var otherConfig = (RelationshipTag)otherItem.Tag;
                        if (otherConfig.SourceTable == config.SourceTable &&
                            otherConfig.TargetTable == config.TargetTable)
                        {
                            otherConfig.IsActive = true;
                            UpdateItemStatus(otherItem, otherConfig);
                        }
                    }
                }
                else
                {
                    // Multiple relationships remain checked - auto-resolve by making first one Active, others Inactive
                    var firstItem = checkedToSameTarget[0];
                    var firstConfig = (RelationshipTag)firstItem.Tag;
                    firstConfig.IsActive = true;
                    UpdateItemStatus(firstItem, firstConfig);
                    
                    // Mark all other relationships to this target as Inactive
                    foreach (var otherItem in _allRelationshipItems)
                    {
                        if (otherItem == firstItem || otherItem.Tag == null) continue;
                        var otherConfig = (RelationshipTag)otherItem.Tag;
                        if (otherConfig.SourceTable == config.SourceTable &&
                            otherConfig.TargetTable == config.TargetTable)
                        {
                            otherConfig.IsActive = false;
                            UpdateItemStatus(otherItem, otherConfig);
                        }
                    }
                }
                
                // Refresh display for all relationships to this target
                foreach (var otherItem in _allRelationshipItems)
                {
                    if (otherItem.Tag == null) continue;
                    var otherConfig = (RelationshipTag)otherItem.Tag;
                    if (otherConfig.SourceTable == config.SourceTable &&
                        otherConfig.TargetTable == config.TargetTable)
                    {
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }
            
            UpdateFinishButtonState();
            UpdateSnowflakeButtonState();
        }

        private void ListViewRelationships_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSnowflakeButtonState();
        }

        private void ListViewRelationships_DoubleClick(object sender, EventArgs e)
        {
            // Get the item at the click location
            var hitTest = listViewRelationships.HitTest(listViewRelationships.PointToClient(Cursor.Position));
            if (hitTest.Item == null || hitTest.Item.Tag == null) return;

            var item = hitTest.Item;
            var config = (RelationshipTag)item.Tag;

            // If item is not checked, check it and set to active
            if (!config.IsChecked)
            {
                item.Checked = true;
                return;
            }
            else
            {
                // Toggle Active/Inactive
                config.IsActive = !config.IsActive;
            }

            // If setting to active, set ALL other relationships to same target as inactive (whether selected or not)
            if (config.IsActive)
            {
                var conflictingActive = _allRelationshipItems
                    .Where(otherItem => otherItem != null && otherItem != item && otherItem.Tag is RelationshipTag)
                    .Select(otherItem => (Item: otherItem, Config: (RelationshipTag)otherItem.Tag))
                    .Where(x => x.Config.IsChecked && x.Config.IsActive &&
                                x.Config.SourceTable == config.SourceTable &&
                                x.Config.TargetTable == config.TargetTable)
                    .ToList();

                if (conflictingActive.Count > 0)
                {
                    var sourceDisplay = GetTableDisplayName(config.SourceTable);
                    var targetDisplay = GetTableDisplayName(config.TargetTable);
                    var existingFields = string.Join(", ", conflictingActive.Select(x => x.Config.DisplayName));

                    var promptResult = MessageBox.Show(
                        $"Ambiguous path detected between '{sourceDisplay}' and '{targetDisplay}'.\n\n" +
                        $"Current active relationship(s): {existingFields}\n" +
                        $"Relationship you activated: {config.DisplayName}\n\n" +
                        "Yes = Keep this relationship Active and mark the other one(s) Inactive\n" +
                        "No = Keep existing relationship Active and mark this one Inactive\n" +
                        "Cancel = Revert this change",
                        "Resolve Ambiguous Relationship Path",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1);

                    if (promptResult == DialogResult.Cancel)
                    {
                        config.IsActive = false;
                        UpdateItemStatus(item, config);
                        return;
                    }

                    if (promptResult == DialogResult.No)
                    {
                        config.IsActive = false;
                        UpdateItemStatus(item, config);
                        return;
                    }
                }

                foreach (var otherItem in _allRelationshipItems)
                {
                    if (otherItem == item) continue;
                    var otherConfig = (RelationshipTag)otherItem.Tag;
                    if (otherConfig.SourceTable == config.SourceTable &&
                        otherConfig.TargetTable == config.TargetTable)
                    {
                        otherConfig.IsActive = false;
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }

            UpdateItemStatus(item, config);
        }

        private string GetTableDisplayName(string logicalName)
        {
            var table = _tables?.FirstOrDefault(t => t.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase));
            return table?.DisplayName ?? logicalName;
        }

        private void UpdateItemStatus(ListViewItem item, RelationshipTag config)
        {
            if (item == null || config == null) return;
            
            // Only update display properties if item is currently in the ListView
            // Items in _allRelationshipItems might be filtered out and not in the control
            if (item.ListView == null) return;
            
            if (item.SubItems.Count < 8) return;
            
            // Count total relationships to this target
            var allToTarget = _allRelationshipItems.Cast<ListViewItem>()
                .Where(i => i != null && i.Tag != null)
                .Where(i =>
                {
                    var c = (RelationshipTag)i.Tag;
                    return c.SourceTable == config.SourceTable && c.TargetTable == config.TargetTable;
                })
                .ToList();

            var targetCount = allToTarget.Count;
            
            // Count ACTIVE relationships to this target (must be CHECKED to count)
            // Use IsChecked from tag to avoid accessing ListView state
            // allToTarget already filtered for non-null items and tags
            var activeCount = allToTarget.Count(i =>
            {
                var c = (RelationshipTag)i.Tag;
                return c.IsChecked && c.IsActive;
            });

            // Update Status column
            var statusText = config.IsActive ? "Active" : "Inactive";
            if (targetCount > 1) statusText += " ⚠";
            item.SubItems[6].Text = statusText;
            
            // Update Type column with snowflake level indicators
            string baseType;
            if (config.SnowflakeLevel >= 2)
                baseType = "\u2744\u2744 Snowflake";
            else if (config.SnowflakeLevel == 1)
                baseType = "\u2744 Snowflake";
            else
                baseType = "Direct";
            var typeText = config.IsActive ? baseType : $"{baseType} (Inactive)";
            item.SubItems[7].Text = typeText;

            // Update background color - only highlight if multiple ACTIVE relationships exist
            if (config.SnowflakeLevel >= 2)
            {
                item.BackColor = Color.FromArgb(200, 225, 255); // Deeper blue for double snowflake
            }
            else if (config.IsSnowflake)
            {
                item.BackColor = Color.LightCyan;
            }
            else if (activeCount > 1 && config.IsActive)
            {
                // Multiple active relationships to same target - CONFLICT!
                item.BackColor = Color.FromArgb(255, 200, 200); // Light red/salmon
            }
            else
            {
                item.BackColor = Color.White; // Normal
            }
        }

        private void UpdateFinishButtonState()
        {
            btnFinish.Enabled = SelectedFactTable != null;
        }

        private void UpdateSnowflakeButtonState()
        {
            if (listViewRelationships.SelectedItems.Count == 0)
            {
                btnAddSnowflake.Enabled = false;
                return;
            }

            var item = listViewRelationships.SelectedItems[0];
            if (item == null || item.Tag == null)
            {
                btnAddSnowflake.Enabled = false;
                return;
            }
            
            var config = (RelationshipTag)item.Tag;

            var targetExists = _tables.Any(t => t.LogicalName == config.TargetTable);
            btnAddSnowflake.Enabled = config.IsChecked && !config.IsOneToMany && config.SnowflakeLevel < 2 && targetExists;
        }

        private void BtnAddSnowflake_Click(object sender, EventArgs e)
        {
            if (listViewRelationships.SelectedItems.Count == 0) return;

            var item = listViewRelationships.SelectedItems[0];
            var config = (RelationshipTag)item.Tag;
            var newSnowflakeLevel = config.SnowflakeLevel + 1;
            var dimensionTable = _tables.FirstOrDefault(t => t.LogicalName == config.TargetTable);

            if (dimensionTable == null) return;

            try
            {
                lblStatus.Text = $"Loading lookups for {dimensionTable.DisplayName}...";
                progressBar.Visible = true;
                Application.DoEvents();

                if (!_tableAttributes.ContainsKey(dimensionTable.LogicalName))
                {
                    var attrs = _adapter.GetAttributesSync(_service, dimensionTable.LogicalName);
                    _tableAttributes[dimensionTable.LogicalName] = attrs;
                }

                var dimAttrs = _tableAttributes[dimensionTable.LogicalName];
                var lookups = dimAttrs
                    .Where(a => a.AttributeType == "Lookup" && a.Targets != null && a.Targets.Any())
                    .OrderBy(a => a.DisplayName)
                    .ToList();

                // Filter out lookups that point back to fact table, itself, or existing parents
                var existingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                existingTargets.Add(SelectedFactTable.LogicalName);
                existingTargets.Add(dimensionTable.LogicalName);

                // For snowflake items, also exclude the source table to prevent circular chains
                if (config.IsSnowflake)
                {
                    existingTargets.Add(config.SourceTable);
                }

                var existingParents = _allRelationshipItems.Cast<ListViewItem>()
                    .Where(i => i != null && i.Tag != null)
                    .Select(i => (RelationshipTag)i.Tag)
                    .Where(r => r.IsSnowflake && r.SourceTable == dimensionTable.LogicalName)
                    .Select(r => r.TargetTable);
                foreach (var t in existingParents) existingTargets.Add(t);

                var availableLookups = lookups
                    .Where(l => l.Targets.Any(t => !existingTargets.Contains(t)))
                    .ToList();

                if (!availableLookups.Any())
                {
                    MessageBox.Show(
                        $"No additional parent tables available for {dimensionTable.DisplayName}.",
                        "No Parent Tables",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Show snowflake dialog
                using (var snowflakeDialog = new SnowflakeDimensionForm(
                    dimensionTable,
                    availableLookups,
                    _tables,
                    _currentRelationships.Where(r => r.IsSnowflake && r.SourceTable == dimensionTable.LogicalName).ToList()))
                {
                    if (snowflakeDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        foreach (var rel in snowflakeDialog.SelectedRelationships)
                        {
                            rel.IsSnowflake = true;
                            AddSnowflakeRelationshipToList(rel, newSnowflakeLevel);
                        }
                    }
                }

                lblStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to load dimension lookups:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private void AddSnowflakeRelationshipToList(ExportRelationship rel, int snowflakeLevel = 1)
        {
            // Check if already exists (must match SourceTable to distinguish direct vs snowflake relationships,
            // since different tables can have lookup fields with the same logical name targeting the same table)
            if (_allRelationshipItems.Cast<ListViewItem>()
                .Any(i => i != null && i.Tag != null &&
                          ((RelationshipTag)i.Tag).SourceTable == rel.SourceTable &&
                          ((RelationshipTag)i.Tag).SourceAttribute == rel.SourceAttribute &&
                          ((RelationshipTag)i.Tag).TargetTable == rel.TargetTable))
                return;

            var targetTable = _tables.FirstOrDefault(t => t.LogicalName == rel.TargetTable);
            var targetDisplayName = targetTable?.DisplayName
                ?? (_allEntityDisplayNames.TryGetValue(rel.TargetTable, out var entityDisplayName) ? entityDisplayName : rel.TargetTable);

            var relationshipDisplayName = rel.DisplayName ?? rel.SourceAttribute ?? "";

            var snowflakeTypeText = snowflakeLevel >= 2 ? "\u2744\u2744 Snowflake" : "\u2744 Snowflake";
            var item = new ListViewItem("");
            item.Checked = true;
            item.SubItems.Add("Many:1");
            item.SubItems.Add(relationshipDisplayName);
            item.SubItems.Add(rel.SourceAttribute ?? "");
            item.SubItems.Add(targetDisplayName);
            item.SubItems.Add(rel.TargetTable);
            item.SubItems.Add(rel.IsActive ? "Active" : "Inactive");
            item.SubItems.Add(rel.IsActive ? snowflakeTypeText : $"{snowflakeTypeText} (Inactive)");
            item.Tag = new RelationshipTag
            {
                SourceTable = rel.SourceTable,
                SourceAttribute = rel.SourceAttribute ?? "",
                TargetTable = rel.TargetTable,
                DisplayName = relationshipDisplayName,
                IsActive = rel.IsActive,
                IsSnowflake = true,
                AssumeReferentialIntegrity = rel.AssumeReferentialIntegrity,
                IsChecked = true,
                SnowflakeLevel = snowflakeLevel
            };
            item.BackColor = snowflakeLevel >= 2 ? Color.FromArgb(200, 225, 255) : Color.LightCyan;

            _allRelationshipItems.Add(item);

            // Apply current search filter if any
            FilterRelationships();
            UpdateFinishButtonState();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            FilterRelationships();
        }

        private void ChkSolutionTablesOnly_CheckedChanged(object sender, EventArgs e)
        {
            _filterSolutionTablesOnly = chkSolutionTablesOnly.Checked;
            FilterRelationships();
        }

        private void FilterRelationships()
        {
            // Safety check - don't filter if control is disposing or disposed
            if (listViewRelationships == null || listViewRelationships.IsDisposed || listViewRelationships.Disposing)
                return;
            
            var searchText = txtSearch?.Text?.Trim().ToLowerInvariant() ?? "";
            
            // Suppress ItemChecked events while we manipulate the list
            _suppressItemCheckedEvent = true;
            
            try
            {
                listViewRelationships.BeginUpdate();
                listViewRelationships.Items.Clear();
                listViewRelationships.Groups.Clear();
            
            // Filter items based on search and solution filter
            var filteredItems = new List<ListViewItem>();
            
            foreach (var item in _allRelationshipItems)
            {
                if (item.Tag == null) continue; // Skip items without tags
                
                var config = (RelationshipTag)item.Tag;
                
                // Apply solution filter
                if (_filterSolutionTablesOnly)
                {
                    var isInSolution = _tables.Any(t => t.LogicalName.Equals(config.TargetTable, StringComparison.OrdinalIgnoreCase));
                    var isSelected = config.IsChecked;
                    if (!isInSolution && !config.IsOneToMany && !isSelected)
                    {
                        continue; // Skip out-of-solution relationships unless already selected
                    }
                }
                
                // Apply search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    var lookupField = item.SubItems[2].Text.ToLowerInvariant();
                    var lookupLogical = item.SubItems[3].Text.ToLowerInvariant();
                    var targetTable = item.SubItems[4].Text.ToLowerInvariant();
                    var targetLogical = item.SubItems[5].Text.ToLowerInvariant();
                    
                    if (!lookupField.Contains(searchText) &&
                        !lookupLogical.Contains(searchText) &&
                        !targetTable.Contains(searchText) &&
                        !targetLogical.Contains(searchText))
                    {
                        continue; // Skip items that don't match search
                    }
                }
                
                filteredItems.Add(item);
            }
            
            // Group by target table - only create groups for tables with multiple relationships
            var targetGroups = filteredItems
                .Where(item => item.Tag != null)
                .GroupBy(item => ((RelationshipTag)item.Tag).TargetTable)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // Create ListView groups for tables with multiple relationships
            var groupsByTarget = new Dictionary<string, ListViewGroup>();
            foreach (var kvp in targetGroups.Where(g => g.Value.Count > 1))
            {
                var targetTable = kvp.Key;
                var items = kvp.Value;
                var displayName = items.First().SubItems[4].Text; // Target Table display name
                
                var group = new ListViewGroup(
                    displayName,
                    $"{displayName} (Multiple Relationships)")
                {
                    HeaderAlignment = HorizontalAlignment.Left
                };
                
                listViewRelationships.Groups.Add(group);
                groupsByTarget[targetTable] = group;
            }
            
            // Add items to the ListView, assigning to groups where applicable
            foreach (var item in filteredItems)
            {
                var config = (RelationshipTag)item.Tag;
                
                if (groupsByTarget.TryGetValue(config.TargetTable, out var group))
                {
                    item.Group = group;
                }
                else
                {
                    item.Group = null; // No group for single relationships
                }
                
                listViewRelationships.Items.Add(item);
            }
            
                listViewRelationships.EndUpdate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FilterRelationships: {ex.Message}");
            }
            finally
            {
                // Re-enable ItemChecked events
                _suppressItemCheckedEvent = false;
            }
        }

        private void BtnFinish_Click(object sender, EventArgs e)
        {
            // Get all checked items from the full list (not just filtered view)
            // Use IsChecked from tag to avoid accessing ListView state
            var checkedItems = _allRelationshipItems.Where(item => item.Tag != null && ((RelationshipTag)item.Tag).IsChecked).ToList();
            var configs = checkedItems.Select(i => (RelationshipTag)i.Tag).ToList();

            // Validate: one active relationship per source-target pair
            var pairGroups = configs.GroupBy(c => $"{c.SourceTable}|{c.TargetTable}");
            foreach (var group in pairGroups)
            {
                var activeCount = group.Count(c => c.IsActive);
                var parts = group.Key.Split('|');
                var targetName = parts[1];
                var targetDisplay = _tables.FirstOrDefault(t => t.LogicalName == targetName)?.DisplayName ?? targetName;

                if (activeCount == 0)
                {
                    MessageBox.Show(
                        $"No active relationship selected for table '{targetDisplay}'.\n\nDouble-click a row to toggle Active/Inactive status.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (activeCount > 1)
                {
                    MessageBox.Show(
                        $"Multiple active relationships to table '{targetDisplay}'.\n\nOnly one lookup per target table can be marked as Active.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Build results
            SelectedRelationships = configs.Select(c => new ExportRelationship
            {
                SourceTable = c.SourceTable,
                SourceAttribute = c.SourceAttribute,
                TargetTable = c.TargetTable,
                DisplayName = c.DisplayName,
                IsActive = c.IsActive,
                IsSnowflake = c.IsSnowflake,
                AssumeReferentialIntegrity = c.AssumeReferentialIntegrity
            }).ToList();

            // Build list of all selected tables
            AllSelectedTables = new List<TableInfo> { SelectedFactTable };

            var allTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var config in configs)
            {
                allTableNames.Add(config.TargetTable);
                if (config.IsSnowflake) allTableNames.Add(config.SourceTable);
                if (config.IsOneToMany) allTableNames.Add(config.SourceTable); // For 1:N, add the child table
            }

            foreach (var tableName in allTableNames)
            {
                if (tableName.Equals(SelectedFactTable.LogicalName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check if already added to avoid duplicates
                if (AllSelectedTables.Any(t => t.LogicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var table = _tables.FirstOrDefault(t => t.LogicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                
                // If table not in current solution, create a minimal TableInfo from known metadata
                if (table == null)
                {
                    var displayName = _allEntityDisplayNames.TryGetValue(tableName, out var entityDisplayName) 
                        ? entityDisplayName 
                        : tableName;
                    
                    table = new TableInfo
                    {
                        LogicalName = tableName,
                        DisplayName = displayName,
                        SchemaName = tableName, // Best guess - will be updated when metadata loads
                        PrimaryIdAttribute = tableName + "id", // Dataverse convention: tablename + "id"
                        PrimaryNameAttribute = "name" // Common default - will be updated when metadata loads
                    };
                }
                
                AllSelectedTables.Add(table);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // Internal class for tag data
        private class RelationshipTag
        {
            public string SourceTable { get; set; } = "";
            public string SourceAttribute { get; set; } = "";
            public string TargetTable { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public bool IsActive { get; set; }
            public bool IsSnowflake { get; set; }
            public bool IsOneToMany { get; set; }
            public bool AssumeReferentialIntegrity { get; set; }
            public bool IsChecked { get; set; }
            /// <summary>0 = Direct, 1 = Snowflake, 2 = Double Snowflake.</summary>
            public int SnowflakeLevel { get; set; }
        }
    }

    /// <summary>
    /// Dialog for selecting parent tables for a dimension (snowflake).
    /// </summary>
    public class SnowflakeDimensionForm : Form
    {
        private readonly TableInfo _dimensionTable;
        private readonly List<CoreAttributeMetadata> _lookups;
        private readonly List<TableInfo> _allTables;
        private readonly List<ExportRelationship> _existingRelationships;

        private ListView listViewParentTables = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;
        
        private bool _suppressItemCheckedEvent = false;

        public List<ExportRelationship> SelectedRelationships { get; private set; } = new List<ExportRelationship>();

        public SnowflakeDimensionForm(
            TableInfo dimensionTable,
            List<CoreAttributeMetadata> lookups,
            List<TableInfo> allTables,
            List<ExportRelationship> existingRelationships)
        {
            _dimensionTable = dimensionTable;
            _lookups = lookups;
            _allTables = allTables;
            _existingRelationships = existingRelationships;
            InitializeComponent();
            PopulateList();
        }

        private void InitializeComponent()
        {
            this.Text = $"Add Parent Tables to {_dimensionTable.DisplayName}";
            this.Width = 700;
            this.Height = 450;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblHeader = new WinLabel
            {
                Text = $"Dimension: {_dimensionTable.DisplayName} ({_dimensionTable.LogicalName})",
                Location = new Point(10, 15),
                AutoSize = true,
                Font = new Font(this.Font, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            this.Controls.Add(lblHeader);

            var lblInstructions = new WinLabel
            {
                Text = "Select parent tables to add (snowflake). These create Dimension → Parent Dimension relationships.",
                Location = new Point(10, 45),
                Width = 660,
                Height = 30
            };
            this.Controls.Add(lblInstructions);

            listViewParentTables = new ListView
            {
                Location = new Point(10, 80),
                Width = 660,
                Height = 280,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                ShowGroups = true
            };
            listViewParentTables.Columns.Add("Include", 55);
            listViewParentTables.Columns.Add("Lookup Field", 150);
            listViewParentTables.Columns.Add("Parent Table", 150);
            listViewParentTables.Columns.Add("Status", 80);
            listViewParentTables.Columns.Add("Logical Name", 150);
            listViewParentTables.ItemChecked += ListViewParentTables_ItemChecked;
            listViewParentTables.DoubleClick += ListViewParentTables_DoubleClick;
            this.Controls.Add(listViewParentTables);

            btnOK = new Button
            {
                Text = "Add Selected",
                Location = new Point(500, 370),
                Width = 90,
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;
            this.Controls.Add(btnOK);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(600, 370),
                Width = 70,
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);
            
            this.CancelButton = btnCancel;
        }

        private void PopulateList()
        {
            _suppressItemCheckedEvent = true;
            
            listViewParentTables.BeginUpdate();
            listViewParentTables.Items.Clear();
            listViewParentTables.Groups.Clear();

            var targetGroups = _lookups
                .SelectMany(l => (l.Targets ?? new List<string>()).Select(t => new { Lookup = l, Target = t }))
                .GroupBy(x => x.Target)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Lookup).ToList());
            
            // Collect all items first
            var allItems = new List<ListViewItem>();

            foreach (var lookup in _lookups)
            {
                foreach (var target in lookup.Targets ?? new List<string>())
                {
                    var targetTable = _allTables.FirstOrDefault(t => t.LogicalName == target);
                    var targetDisplayName = targetTable?.DisplayName ?? target;

                    var multipleLookupsToTarget = targetGroups.ContainsKey(target) && targetGroups[target].Count > 1;

                    var existingRel = _existingRelationships.FirstOrDefault(r =>
                        r.SourceAttribute == lookup.LogicalName && r.TargetTable == target);

                    var isChecked = existingRel != null;
                    var isActive = existingRel?.IsActive ?? true; // Default to Active

                    var statusText = isActive ? "Active" : "Inactive";
                    if (multipleLookupsToTarget) statusText += " ⚠";

                    var sourceDisplayName = lookup.DisplayName ?? lookup.LogicalName ?? "";

                    var item = new ListViewItem("");
                    item.Checked = isChecked;
                    item.SubItems.Add(sourceDisplayName);
                    item.SubItems.Add(targetDisplayName);
                    item.SubItems.Add(statusText);
                    item.SubItems.Add(target);
                    item.Tag = new SnowflakeTag
                    {
                        SourceTable = _dimensionTable.LogicalName ?? "",
                        SourceAttribute = lookup.LogicalName ?? "",
                        TargetTable = target,
                        DisplayName = sourceDisplayName,
                        IsActive = isActive,
                        AssumeReferentialIntegrity = lookup.IsRequired,
                        IsChecked = isChecked
                    };

                    // Color will be set in UpdateItemStatus based on active conflict check

                    allItems.Add(item);
                }
            }
            
            // Group by target table - only create groups for tables with multiple relationships
            var itemGroups = allItems
                .GroupBy(item => ((SnowflakeTag)item.Tag).TargetTable)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // Create ListView groups for tables with multiple relationships
            var groupsByTarget = new Dictionary<string, ListViewGroup>();
            foreach (var kvp in itemGroups.Where(g => g.Value.Count > 1))
            {
                var targetTable = kvp.Key;
                var items = kvp.Value;
                var displayName = items.First().SubItems[2].Text; // Parent Table display name
                
                var group = new ListViewGroup(
                    displayName,
                    $"{displayName} (Multiple Relationships)")
                {
                    HeaderAlignment = HorizontalAlignment.Left
                };
                
                listViewParentTables.Groups.Add(group);
                groupsByTarget[targetTable] = group;
            }
            
            // Add items to the ListView, assigning to groups where applicable
            foreach (var item in allItems)
            {
                var config = (SnowflakeTag)item.Tag;
                
                if (groupsByTarget.TryGetValue(config.TargetTable, out var group))
                {
                    item.Group = group;
                }
                else
                {
                    item.Group = null; // No group for single relationships
                }
                
                listViewParentTables.Items.Add(item);
            }
            
            // Update all item statuses to apply correct colors and text
            foreach (ListViewItem item in listViewParentTables.Items)
            {
                if (item?.Tag is SnowflakeTag statusConfig)
                {
                    UpdateItemStatus(item, statusConfig);
                }
            }
            
            listViewParentTables.EndUpdate();
            _suppressItemCheckedEvent = false;
        }

        private void ListViewParentTables_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            // Ignore if we're suppressing events during list manipulation
            if (_suppressItemCheckedEvent) return;
            
            if (!(e?.Item?.Tag is SnowflakeTag config)) return;
            if (listViewParentTables == null) return;
            
            // Update the checked state in the tag
            config.IsChecked = e.Item.Checked;
            
            // When checking an item, set it to active and mark other relationships to the same target as inactive
            if (e.Item.Checked)
            {
                config.IsActive = true;
                
                // Mark other relationships to the same target table as inactive
                foreach (ListViewItem otherItem in listViewParentTables.Items)
                {
                    if (otherItem == e.Item) continue;
                    if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                    if (otherConfig.TargetTable == config.TargetTable)
                    {
                        otherConfig.IsActive = false;
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
                
                // Update this item and all items to same target (to refresh conflict highlighting)
                UpdateItemStatus(e.Item, config);
                foreach (ListViewItem otherItem in listViewParentTables.Items)
                {
                    if (otherItem == e.Item) continue;
                    if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                    if (otherConfig.TargetTable == config.TargetTable)
                    {
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }
            else
            {
                // When unchecking, normalize Active/Inactive states for this dimension
                // Find all checked relationships to the same target
                // Use IsChecked from tag to avoid accessing ListView state on items not in the control
                // Consolidate null checks into single Where clause to prevent race conditions
                var checkedToSameTarget = listViewParentTables.Items.Cast<ListViewItem>()
                    .Where(item => item?.Tag is SnowflakeTag otherConfig &&
                                   otherConfig.IsChecked &&
                                   otherConfig.TargetTable == config.TargetTable)
                    .ToList();
                
                // If exactly one relationship remains checked, make it Active; others Inactive
                if (checkedToSameTarget.Count == 1)
                {
                    var singleItem = checkedToSameTarget[0];
                    if (!(singleItem?.Tag is SnowflakeTag singleConfig)) return;
                    singleConfig.IsActive = true;
                    UpdateItemStatus(singleItem, singleConfig);
                    
                    // Mark all other relationships to this target as Inactive
                    foreach (ListViewItem otherItem in listViewParentTables.Items)
                    {
                        if (otherItem == singleItem) continue;
                        if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                        if (otherConfig.TargetTable == config.TargetTable)
                        {
                            otherConfig.IsActive = false;
                            UpdateItemStatus(otherItem, otherConfig);
                        }
                    }
                }
                else if (checkedToSameTarget.Count == 0)
                {
                    // All relationships to this target are unchecked - mark all as Active
                    // (so whichever one user checks next will be ready to use)
                    foreach (ListViewItem otherItem in listViewParentTables.Items)
                    {
                        if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                        if (otherConfig.TargetTable == config.TargetTable)
                        {
                            otherConfig.IsActive = true;
                            UpdateItemStatus(otherItem, otherConfig);
                        }
                    }
                }
                else
                {
                    // Multiple relationships remain checked - auto-resolve by making first one Active, others Inactive
                    var firstItem = checkedToSameTarget[0];
                    if (!(firstItem?.Tag is SnowflakeTag firstConfig)) return;
                    firstConfig.IsActive = true;
                    UpdateItemStatus(firstItem, firstConfig);
                    
                    // Mark all other relationships to this target as Inactive
                    foreach (ListViewItem otherItem in listViewParentTables.Items)
                    {
                        if (otherItem == firstItem) continue;
                        if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                        if (otherConfig.TargetTable == config.TargetTable)
                        {
                            otherConfig.IsActive = false;
                            UpdateItemStatus(otherItem, otherConfig);
                        }
                    }
                }
                
                // Refresh display for all relationships to this target
                foreach (ListViewItem otherItem in listViewParentTables.Items)
                {
                    if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                    if (otherConfig.TargetTable == config.TargetTable)
                    {
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }
        }

        private void ListViewParentTables_DoubleClick(object sender, EventArgs e)
        {
            if (listViewParentTables.SelectedItems.Count == 0) return;

            var item = listViewParentTables.SelectedItems[0];
            if (!(item?.Tag is SnowflakeTag config)) return;

            // If item is not checked, check it and set to active
            if (!config.IsChecked)
            {
                item.Checked = true;
                config.IsChecked = true;
                config.IsActive = true;
            }
            else
            {
                // Toggle Active/Inactive
                config.IsActive = !config.IsActive;
            }

            // If setting to active, set ALL other relationships to same target as inactive (whether selected or not)
            if (config.IsActive)
            {
                foreach (ListViewItem otherItem in listViewParentTables.Items)
                {
                    if (otherItem == item) continue;
                    if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                    if (otherConfig.TargetTable == config.TargetTable)
                    {
                        otherConfig.IsActive = false;
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }

            // Update this item and all items to same target (to refresh conflict highlighting)
            UpdateItemStatus(item, config);
            foreach (ListViewItem otherItem in listViewParentTables.Items)
            {
                if (otherItem == item) continue;
                if (!(otherItem?.Tag is SnowflakeTag otherConfig)) continue;
                if (otherConfig.TargetTable == config.TargetTable)
                {
                    UpdateItemStatus(otherItem, otherConfig);
                }
            }
        }

        private void UpdateItemStatus(ListViewItem item, SnowflakeTag config)
        {
            if (item == null || config == null || item.SubItems.Count < 4) return;
            
            // Count total relationships to this target
            var allToTarget = listViewParentTables.Items.Cast<ListViewItem>()
                .Where(i => i?.Tag is SnowflakeTag tag && tag.TargetTable == config.TargetTable)
                .ToList();

            var targetCount = allToTarget.Count;
            
            // Count ACTIVE relationships to this target (must be CHECKED to count)
            // Use IsChecked from tag to avoid accessing ListView state
            // allToTarget already filtered for non-null items and tags
            var activeCount = allToTarget.Count(i => i?.Tag is SnowflakeTag tag && tag.IsChecked && tag.IsActive);

            // Update Status column to include (Inactive) suffix
            var statusText = config.IsActive ? "Active" : "Inactive";
            if (targetCount > 1) statusText += " ⚠";
            item.SubItems[3].Text = statusText;
            
            // Update background color - only highlight if multiple ACTIVE relationships exist
            if (activeCount > 1 && config.IsActive)
            {
                // Multiple active relationships to same target - CONFLICT!
                item.BackColor = Color.FromArgb(255, 200, 200); // Light red/salmon
            }
            else
            {
                item.BackColor = Color.White; // Normal
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            var checkedItems = listViewParentTables.CheckedItems.Cast<ListViewItem>().ToList();
            var configs = checkedItems
                .Where(i => i?.Tag is SnowflakeTag)
                .Select(i => (SnowflakeTag)i.Tag)
                .ToList();

            // Validate
            var targetGroups = configs.GroupBy(c => c.TargetTable);
            foreach (var group in targetGroups)
            {
                var activeCount = group.Count(c => c.IsActive);
                if (activeCount == 0)
                {
                    var targetDisplay = _allTables.FirstOrDefault(t => t.LogicalName == group.Key)?.DisplayName ?? group.Key;
                    MessageBox.Show($"No active relationship selected for table '{targetDisplay}'.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                if (activeCount > 1)
                {
                    var targetDisplay = _allTables.FirstOrDefault(t => t.LogicalName == group.Key)?.DisplayName ?? group.Key;
                    MessageBox.Show($"Multiple active relationships to table '{targetDisplay}'.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            SelectedRelationships = configs.Select(c => new ExportRelationship
            {
                SourceTable = c.SourceTable,
                SourceAttribute = c.SourceAttribute,
                TargetTable = c.TargetTable,
                DisplayName = c.DisplayName,
                IsActive = c.IsActive,
                IsSnowflake = true,
                AssumeReferentialIntegrity = c.AssumeReferentialIntegrity
            }).ToList();
        }

        private class SnowflakeTag
        {
            public string SourceTable { get; set; } = "";
            public string SourceAttribute { get; set; } = "";
            public string TargetTable { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public bool IsActive { get; set; }
            public bool AssumeReferentialIntegrity { get; set; }
            public bool IsChecked { get; set; }
        }
    }
}
