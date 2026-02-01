// =============================================================================
// FactDimensionSelectorDialog.cs - Star-Schema Table Selector
// =============================================================================
// Purpose: Dialog for configuring star-schema model with Fact and Dimension tables.
//
// This is the primary dialog for designing the semantic model structure:
//   1. Select a Dataverse solution
//   2. Choose a Fact table from the solution's entities
//   3. Configure relationships (dimensions) based on lookup fields
//   4. Optionally add snowflake dimensions (dimension-to-dimension links)
//
// Star-Schema Concepts:
//   - Fact Table: Central table containing measurable events (many side)
//   - Dimension Tables: Lookup tables for slicing data (one side)
//   - Snowflake Dimensions: Hierarchical dimensions (e.g., Account -> Parent)
//
// The dialog automatically discovers lookup relationships by analyzing
// the Fact table's lookup attributes that reference other selected tables.
//
// Output:
//   - SelectedFactTable: The chosen fact table
//   - SelectedRelationships: List of configured relationships
//   - AllSelectedTables: Combined list of fact + dimension tables
// =============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Models;
using DataverseToPowerBI.Configurator.Services;

namespace DataverseToPowerBI.Configurator.Forms
{
    /// <summary>
    /// Dialog for selecting a Fact table and its Dimension relationships in a star-schema model.
    /// Combined single-panel view with dropdown for Fact table and ListView for relationships.
    /// Supports snowflake dimensions (one level only).
    /// </summary>
    public class FactDimensionSelectorDialog : Form
    {
        private readonly DataverseClient _client;
        private List<DataverseSolution> _solutions = new();
        private List<TableInfo> _tables = new();
        private Dictionary<string, List<AttributeMetadata>> _tableAttributes = new();

        // Existing configuration (for editing)
        private string? _currentFactTable;
        private List<RelationshipConfig> _currentRelationships;

        // UI Controls
        private ComboBox cmbSolutions = null!;
        private ComboBox cmbFactTable = null!;
        private CheckBox chkIncludeOneToMany = null!;
        private ListView listViewRelationships = null!;
        private Button btnAddSnowflake = null!;
        private Button btnFinish = null!;
        private Button btnCancel = null!;
        private Label lblStatus = null!;
        private ProgressBar progressBar = null!;

        // Results
        public string? SelectedSolutionName { get; private set; }
        public TableInfo? SelectedFactTable { get; private set; }
        public List<RelationshipConfig> SelectedRelationships { get; private set; } = new();
        public List<TableInfo> AllSelectedTables { get; private set; } = new();

        public FactDimensionSelectorDialog(
            DataverseClient client,
            string? currentSolution,
            string? currentFactTable = null,
            List<RelationshipConfig>? currentRelationships = null)
        {
            _client = client;
            SelectedSolutionName = currentSolution;
            _currentFactTable = currentFactTable;
            _currentRelationships = currentRelationships ?? new();
            InitializeComponent();
            LoadSolutions();
        }

        private void InitializeComponent()
        {
            this.Text = "Select Fact Table and Dimensions";
            this.Width = 950;
            this.Height = 700;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Solution selector
            var lblSolution = new Label
            {
                Text = "Solution:",
                Location = new Point(10, 15),
                AutoSize = true
            };
            this.Controls.Add(lblSolution);

            cmbSolutions = new ComboBox
            {
                Location = new Point(80, 12),
                Width = 400,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSolutions.SelectedIndexChanged += CmbSolutions_SelectedIndexChanged;
            this.Controls.Add(cmbSolutions);

            // Fact table selector (dropdown)
            var lblFactTable = new Label
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
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            cmbFactTable.SelectedIndexChanged += CmbFactTable_SelectedIndexChanged;
            this.Controls.Add(cmbFactTable);

            var lblFactHint = new Label
            {
                Text = "(Select the central transactional table)",
                Location = new Point(610, 50),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblFactHint);

            // Dimension relationships section
            var lblDimensions = new Label
            {
                Text = "Dimension Relationships:",
                Location = new Point(10, 85),
                AutoSize = true,
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(lblDimensions);

            var lblDimHint = new Label
            {
                Text = "Check lookups to include as dimensions. Double-click to toggle Active/Inactive. Yellow rows have multiple lookups to same table.",
                Location = new Point(10, 105),
                Width = 900,
                Height = 30,
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblDimHint);

            // One-to-many checkbox
            chkIncludeOneToMany = new CheckBox
            {
                Text = "Include one-to-many relationships (Advanced: tables that reference this fact table)",
                Location = new Point(10, 138),
                Width = 600,
                AutoSize = false,
                Height = 20
            };
            chkIncludeOneToMany.CheckedChanged += ChkIncludeOneToMany_CheckedChanged;
            this.Controls.Add(chkIncludeOneToMany);

            listViewRelationships = new ListView
            {
                Location = new Point(10, 165),
                Width = 910,
                Height = 390,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true
            };
            listViewRelationships.Columns.Add("Include", 55);
            listViewRelationships.Columns.Add("Cardinality", 90);
            listViewRelationships.Columns.Add("Lookup Field", 180);
            listViewRelationships.Columns.Add("Target Table", 180);
            listViewRelationships.Columns.Add("Status", 100);
            listViewRelationships.Columns.Add("Type", 80);
            listViewRelationships.Columns.Add("Target Logical Name", 150);
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
            lblStatus = new Label
            {
                Text = "Ready",
                Location = new Point(10, 610),
                Width = 700
            };
            this.Controls.Add(lblStatus);

            progressBar = new ProgressBar
            {
                Location = new Point(10, 630),
                Width = 700,
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            this.Controls.Add(progressBar);

            // Action buttons
            btnFinish = new Button
            {
                Text = "Finish Selection",
                Location = new Point(730, 620),
                Width = 100,
                Height = 30,
                Enabled = false
            };
            btnFinish.Click += BtnFinish_Click;
            this.Controls.Add(btnFinish);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(840, 620),
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
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
                _solutions = allSolutions.Where(s => !s.IsManaged).ToList();

                cmbSolutions.Items.Clear();
                foreach (var solution in _solutions.OrderBy(s => s.FriendlyName))
                {
                    cmbSolutions.Items.Add(solution.FriendlyName);
                }

                // Select current solution if specified
                if (!string.IsNullOrEmpty(SelectedSolutionName))
                {
                    var index = _solutions.FindIndex(s => s.UniqueName == SelectedSolutionName);
                    if (index >= 0)
                        cmbSolutions.SelectedIndex = index;
                }
                else if (_solutions.Any())
                {
                    // Select most recently modified solution
                    var preferred = _solutions
                        .Where(s => s.ModifiedOn.HasValue)
                        .OrderByDescending(s => s.ModifiedOn)
                        .FirstOrDefault() ?? _solutions.First();

                    var index = _solutions.IndexOf(preferred);
                    if (index >= 0)
                        cmbSolutions.SelectedIndex = index;
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

        private async void CmbSolutions_SelectedIndexChanged(object? sender, EventArgs e)
        {
            await LoadTablesForSelectedSolution();
        }

        private async Task LoadTablesForSelectedSolution()
        {
            if (cmbSolutions.SelectedIndex < 0) return;

            var solution = _solutions[cmbSolutions.SelectedIndex];
            SelectedSolutionName = solution.UniqueName;

            try
            {
                cmbSolutions.Enabled = false;
                cmbFactTable.Enabled = false;
                lblStatus.Text = $"Loading tables from {solution.FriendlyName}...";
                progressBar.Visible = true;

                _tables = await _client.GetSolutionTablesAsync(solution.SolutionId);
                _tableAttributes.Clear();  // Clear cached attributes to force refresh

                // Populate fact table dropdown
                cmbFactTable.Items.Clear();
                cmbFactTable.Items.Add("-- Select Fact Table --");
                foreach (var table in _tables.OrderBy(t => t.DisplayName))
                {
                    cmbFactTable.Items.Add($"{table.DisplayName} ({table.LogicalName})");
                }
                cmbFactTable.SelectedIndex = 0;

                // Pre-select if we have a current fact table
                if (!string.IsNullOrEmpty(_currentFactTable))
                {
                    var factTable = _tables.FirstOrDefault(t => t.LogicalName == _currentFactTable);
                    if (factTable != null)
                    {
                        var index = _tables.OrderBy(t => t.DisplayName).ToList().IndexOf(factTable) + 1;
                        cmbFactTable.SelectedIndex = index;
                    }
                }

                cmbFactTable.Enabled = true;
                lblStatus.Text = $"Loaded {_tables.Count} tables. Select a Fact table.";
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

        private async void CmbFactTable_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbFactTable.SelectedIndex <= 0)
            {
                listViewRelationships.Items.Clear();
                SelectedFactTable = null;
                UpdateFinishButtonState();
                return;
            }

            var sortedTables = _tables.OrderBy(t => t.DisplayName).ToList();
            SelectedFactTable = sortedTables[cmbFactTable.SelectedIndex - 1];

            await LoadFactTableRelationships();
        }

        private bool _suppressOneToManyWarning = false;

        private async void ChkIncludeOneToMany_CheckedChanged(object? sender, EventArgs e)
        {
            // Skip if we're suppressing (used when programmatically changing the checkbox)
            if (_suppressOneToManyWarning)
                return;
            
            if (chkIncludeOneToMany.Checked)
            {
                var result = MessageBox.Show(
                    "⚠️ WARNING: Including one-to-many relationships is an advanced feature.\n\n" +
                    "One-to-many relationships create detail tables (child records) rather than typical dimension tables.\n" +
                    "This can significantly increase the size of your semantic model and may cause performance issues.\n\n" +
                    "Only enable this if:\n" +
                    "• You understand star schema and snowflake schema design\n" +
                    "• You need to analyze child record details alongside fact data\n" +
                    "• You accept the potential performance implications\n\n" +
                    "Do you want to continue?",
                    "Advanced Feature Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result == DialogResult.No)
                {
                    // Suppress event to prevent recursive trigger
                    _suppressOneToManyWarning = true;
                    chkIncludeOneToMany.Checked = false;
                    _suppressOneToManyWarning = false;
                    return;
                }
            }

            // Reload relationships when checkbox changes
            if (SelectedFactTable != null)
            {
                await LoadFactTableRelationships();
            }
            else if (chkIncludeOneToMany.Checked)
            {
                // User checked the box before selecting a fact table
                // Show a helpful message
                MessageBox.Show(
                    "Please select a Fact Table first.\n\n" +
                    "Once you select a fact table, any tables that reference it (one-to-many relationships) will be shown in the list.",
                    "Select Fact Table First",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private async Task LoadFactTableRelationships()
        {
            if (SelectedFactTable == null) return;

            try
            {
                // Restore the one-to-many checkbox state if there are reverse relationships in current config
                // Suppress warning dialog when restoring existing configuration
                if (_currentRelationships.Any(r => r.IsReverse))
                {
                    _suppressOneToManyWarning = true;
                    chkIncludeOneToMany.Checked = true;
                    _suppressOneToManyWarning = false;
                }

                lblStatus.Text = $"Loading lookups for {SelectedFactTable.DisplayName}...";
                progressBar.Visible = true;
                cmbFactTable.Enabled = false;

                // Always fetch fresh attributes from server to ensure we have the latest relationships
                var attrs = await _client.GetAttributesAsync(SelectedFactTable.LogicalName);
                _tableAttributes[SelectedFactTable.LogicalName] = attrs;

                var factAttrs = _tableAttributes[SelectedFactTable.LogicalName];

                // Get lookup attributes - include ALL lookups regardless of target table's solution
                var lookups = factAttrs
                    .Where(a => a.AttributeType == "Lookup" && a.Targets != null && a.Targets.Any())
                    .OrderBy(a => a.DisplayName)
                    .ToList();

                PopulateRelationshipsListView(SelectedFactTable.LogicalName, lookups, isSnowflake: false, isReverse: false);

                // If "Include one-to-many" is checked, also find tables that reference THIS fact table
                if (chkIncludeOneToMany.Checked)
                {
                    lblStatus.Text = $"Loading one-to-many relationships (tables referencing {SelectedFactTable.DisplayName})...";
                    await LoadReverseLookups(SelectedFactTable.LogicalName);
                }

                // Restore snowflake relationships from:
                // 1. Normal dimensions (fact -> dimension -> parent)
                // 2. Reverse relationship detail tables (detail -> fact, detail -> parent)
                var factLookupTargets = lookups
                    .Where(l => l.Targets != null)
                    .SelectMany(l => l.Targets)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                // Also get source tables from reverse relationships
                var reverseRelSourceTables = _currentRelationships
                    .Where(r => r.IsReverse)
                    .Select(r => r.SourceTable)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    
                var snowflakeRels = _currentRelationships
                    .Where(r => r.IsSnowflake && 
                           (factLookupTargets.Contains(r.SourceTable) || reverseRelSourceTables.Contains(r.SourceTable)))
                    .ToList();
                    
                if (snowflakeRels.Any())
                {
                    // Load target tables for snowflake relationships if not already loaded
                    var missingTables = snowflakeRels
                        .Select(r => r.TargetTable)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(t => !_tables.Any(table => table.LogicalName.Equals(t, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (missingTables.Any())
                    {
                        // Load missing tables - they might be from different solutions or system tables
                        foreach (var tableName in missingTables)
                        {
                            try
                            {
                                var tableMetadata = await _client.GetTableMetadataAsync(tableName);
                                if (tableMetadata != null && !_tables.Any(t => t.LogicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    // Convert TableMetadata to TableInfo
                                    _tables.Add(new TableInfo
                                    {
                                        LogicalName = tableMetadata.LogicalName,
                                        DisplayName = tableMetadata.DisplayName,
                                        SchemaName = tableMetadata.SchemaName,
                                        PrimaryIdAttribute = tableMetadata.PrimaryIdAttribute,
                                        PrimaryNameAttribute = tableMetadata.PrimaryNameAttribute
                                    });
                                }
                            }
                            catch
                            {
                                // If we can't load the table, continue anyway - we'll use the logical name
                            }
                        }
                    }

                    // Now add the snowflake relationships to the list
                    foreach (var snowflakeRel in snowflakeRels)
                    {
                        AddSnowflakeRelationshipToList(snowflakeRel);
                    }
                }

                var totalLookups = lookups.Count + snowflakeRels.Count;
                lblStatus.Text = snowflakeRels.Any() 
                    ? $"Found {lookups.Count} lookup fields on {SelectedFactTable.DisplayName} + {snowflakeRels.Count} snowflake relationship(s)."
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

        private async Task LoadReverseLookups(string factTableName)
        {
            try
            {
                var reverseLookups = new List<(string SourceTable, AttributeMetadata Lookup)>();

                // System tables to exclude from one-to-many relationships
                var systemTablesToExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "syncerror",
                    "duplicaterecord",
                    "bulkdeletefailure",
                    "bulkdeleteoperation",
                    "asyncoperation",
                    "workflowlog",
                    "importlog",
                    "importfile",
                    "tracelog",
                    "plugintracelog",
                    "audit",
                    "principalobjectaccess",
                    "principalobjectattributeaccess"
                };

                // First check if we already have reverse relationships in current config - if so, only check those tables
                var knownReverseTables = _currentRelationships
                    .Where(r => r.IsReverse && r.TargetTable.Equals(factTableName, StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.SourceTable)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (knownReverseTables.Any())
                {
                    // Fast path: only check known reverse relationship tables
                    foreach (var tableName in knownReverseTables)
                    {
                        var table = _tables.FirstOrDefault(t => t.LogicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                        if (table == null) continue;

                        // Get or fetch attributes for this table
                        if (!_tableAttributes.ContainsKey(table.LogicalName))
                        {
                            var attrs = await _client.GetAttributesAsync(table.LogicalName);
                            _tableAttributes[table.LogicalName] = attrs;
                        }

                        var tableAttrs = _tableAttributes[table.LogicalName];
                        
                        // Find lookups that target the fact table
                        var lookupsToFact = tableAttrs
                            .Where(a => a.AttributeType == "Lookup" && 
                                       a.Targets != null && 
                                       a.Targets.Any(t => t.Equals(factTableName, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        foreach (var lookup in lookupsToFact)
                        {
                            reverseLookups.Add((table.LogicalName, lookup));
                        }
                    }
                }
                else
                {
                    // Full search: Search all tables in solution for lookups that target the fact table
                    foreach (var table in _tables)
                    {
                        // Skip the fact table itself
                        if (table.LogicalName.Equals(factTableName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Skip system/internal tables
                        if (systemTablesToExclude.Contains(table.LogicalName))
                            continue;

                        // Get or fetch attributes for this table
                        if (!_tableAttributes.ContainsKey(table.LogicalName))
                        {
                            var attrs = await _client.GetAttributesAsync(table.LogicalName);
                            _tableAttributes[table.LogicalName] = attrs;
                        }

                        var tableAttrs = _tableAttributes[table.LogicalName];
                        
                        // Find lookups that target the fact table
                        var lookupsToFact = tableAttrs
                            .Where(a => a.AttributeType == "Lookup" && 
                                       a.Targets != null && 
                                       a.Targets.Any(t => t.Equals(factTableName, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        foreach (var lookup in lookupsToFact)
                        {
                            reverseLookups.Add((table.LogicalName, lookup));
                        }
                    }
                }

                // Add reverse lookups to the list view
                if (reverseLookups.Any())
                {
                    PopulateReverseLookups(reverseLookups);
                    lblStatus.Text += $" Found {reverseLookups.Count} one-to-many relationship(s).";
                }
                else
                {
                    // No one-to-many relationships found
                    lblStatus.Text = "No one-to-many relationships found.";
                    MessageBox.Show(
                        $"The '{SelectedFactTable?.DisplayName ?? SelectedFactTable?.LogicalName}' table has no available one-to-many relationships.\n\n" +
                        "One-to-many relationships occur when other tables have lookup fields that reference this fact table.",
                        "No Relationships Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading reverse lookups: {ex.Message}";
            }
        }

        private void PopulateReverseLookups(List<(string SourceTable, AttributeMetadata Lookup)> reverseLookups)
        {
            foreach (var (sourceTable, lookup) in reverseLookups)
            {
                var sourceTableInfo = _tables.FirstOrDefault(t => t.LogicalName.Equals(sourceTable, StringComparison.OrdinalIgnoreCase));
                var sourceTableDisplay = sourceTableInfo?.DisplayName ?? sourceTable;

                // Each reverse lookup represents: SourceTable (many) -> FactTable (one)
                // From the fact table's perspective, this is a 1:many relationship
                foreach (var target in lookup.Targets!)
                {
                    // Skip if already in the list
                    var existingKey = $"{sourceTable}|{lookup.LogicalName}|{target}";
                    if (listViewRelationships.Items.Cast<ListViewItem>().Any(item => item.Name == existingKey))
                        continue;

                    // Check if this reverse relationship exists in current config
                    var existingRel = _currentRelationships.FirstOrDefault(r =>
                        r.SourceTable.Equals(sourceTable, StringComparison.OrdinalIgnoreCase) &&
                        r.SourceAttribute.Equals(lookup.LogicalName, StringComparison.OrdinalIgnoreCase) &&
                        r.TargetTable.Equals(target, StringComparison.OrdinalIgnoreCase) &&
                        r.IsReverse);

                    var isChecked = existingRel != null;
                    var isActive = existingRel?.IsActive ?? true;
                    var statusText = isActive ? "Active" : "Inactive";

                    var item = new ListViewItem("");
                    item.Name = existingKey;
                    item.Checked = isChecked;
                    item.Tag = new RelationshipConfig
                    {
                        SourceTable = sourceTable,
                        SourceAttribute = lookup.LogicalName,
                        TargetTable = target,
                        DisplayName = lookup.DisplayName,
                        IsSnowflake = false,
                        IsActive = isActive,
                        IsReverse = true,  // Mark as reverse (1:many from fact's perspective)
                        AssumeReferentialIntegrity = lookup.IsRequired  // Set based on whether lookup is required
                    };

                    item.SubItems.Add("Fact:Many");  // Cardinality column - from fact's perspective
                    item.SubItems.Add(lookup.DisplayName);  // Lookup field on child table
                    item.SubItems.Add(sourceTableDisplay);  // Child table that references the fact
                    item.SubItems.Add(statusText);
                    item.SubItems.Add("Direct");
                    item.SubItems.Add(sourceTable);

                    // Mark reverse relationships with a different color
                    item.BackColor = Color.LightCyan;
                    item.ToolTipText = "One-to-many: Multiple records from this table can reference each fact record";

                    listViewRelationships.Items.Add(item);
                }
            }
        }

        private void PopulateRelationshipsListView(string sourceTable, List<AttributeMetadata> lookups, bool isSnowflake, bool isReverse = false)
        {
            // If not snowflake, clear and repopulate; if snowflake, append
            if (!isSnowflake)
            {
                listViewRelationships.Items.Clear();
            }

            // Group by target table to identify multiple lookups to same table
            var targetGroups = lookups
                .SelectMany(l => l.Targets!.Select(t => new { Lookup = l, Target = t }))
                .GroupBy(x => x.Target)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Lookup).ToList());

            foreach (var lookup in lookups)
            {
                foreach (var target in lookup.Targets!)
                {
                    // Skip if this target is the fact table itself (self-reference)
                    if (target == SelectedFactTable?.LogicalName && !isSnowflake) continue;

                    // Skip if this relationship already exists (for snowflake additions)
                    if (listViewRelationships.Items.Cast<ListViewItem>()
                        .Any(i => ((RelationshipConfig)i.Tag).SourceAttribute == lookup.LogicalName &&
                                  ((RelationshipConfig)i.Tag).TargetTable == target))
                        continue;

                    // Find the target table display name
                    var targetTable = _tables.FirstOrDefault(t => t.LogicalName == target);
                    var targetDisplayName = targetTable?.DisplayName ?? target;

                    // Check if there are multiple lookups to this target from this source
                    var multipleLookupsToTarget = targetGroups.ContainsKey(target) && targetGroups[target].Count > 1;

                    // Determine initial status from existing config
                    var existingRel = _currentRelationships.FirstOrDefault(r =>
                        r.SourceTable == sourceTable &&
                        r.SourceAttribute == lookup.LogicalName &&
                        r.TargetTable == target);

                    var isChecked = existingRel != null;
                    var isActive = existingRel?.IsActive ?? !multipleLookupsToTarget;

                    // If no existing config and only one lookup to this target, auto-check
                    if (existingRel == null && !multipleLookupsToTarget && targetTable != null && !isSnowflake)
                    {
                        isChecked = true;
                        isActive = true;
                    }

                    var statusText = isActive ? "Active" : "Inactive";
                    if (multipleLookupsToTarget)
                    {
                        statusText += " ⚠";
                    }

                    var typeText = isSnowflake ? "Snowflake" : "Direct";
                    var cardinalityText = isReverse ? "Fact:Many" : "Many:1";

                    var item = new ListViewItem("");
                    item.Checked = isChecked;
                    item.SubItems.Add(cardinalityText);
                    item.SubItems.Add(lookup.DisplayName ?? lookup.LogicalName);
                    item.SubItems.Add(targetDisplayName);
                    item.SubItems.Add(statusText);
                    item.SubItems.Add(typeText);
                    item.SubItems.Add(target);
                    item.Tag = new RelationshipConfig
                    {
                        SourceTable = sourceTable,
                        SourceAttribute = lookup.LogicalName,
                        TargetTable = target,
                        DisplayName = lookup.DisplayName,
                        IsActive = isActive,
                        IsSnowflake = existingRel?.IsSnowflake ?? isSnowflake,  // Preserve existing snowflake status
                        IsReverse = existingRel?.IsReverse ?? isReverse,  // Preserve existing reverse status
                        AssumeReferentialIntegrity = lookup.IsRequired  // Set based on whether lookup is required
                    };

                    // Color code
                    if (isSnowflake)
                    {
                        item.BackColor = Color.LightCyan;
                    }
                    else if (multipleLookupsToTarget)
                    {
                        item.BackColor = Color.LightYellow;
                    }

                    listViewRelationships.Items.Add(item);
                }
            }

            UpdateFinishButtonState();
        }

        private void ListViewRelationships_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            UpdateFinishButtonState();
            UpdateSnowflakeButtonState();
        }

        private void ListViewRelationships_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateSnowflakeButtonState();
        }

        private void ListViewRelationships_DoubleClick(object? sender, EventArgs e)
        {
            // Toggle Active/Inactive status on double-click
            if (listViewRelationships.SelectedItems.Count == 0) return;

            var item = listViewRelationships.SelectedItems[0];
            var config = (RelationshipConfig)item.Tag;

            // Toggle active state
            config.IsActive = !config.IsActive;

            // If setting to active, set other lookups to same target (from same source) to inactive
            if (config.IsActive)
            {
                foreach (ListViewItem otherItem in listViewRelationships.Items)
                {
                    if (otherItem == item) continue;
                    var otherConfig = (RelationshipConfig)otherItem.Tag;
                    if (otherConfig.SourceTable == config.SourceTable &&
                        otherConfig.TargetTable == config.TargetTable &&
                        otherConfig.IsActive)
                    {
                        otherConfig.IsActive = false;
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }

            UpdateItemStatus(item, config);
        }

        private void UpdateItemStatus(ListViewItem item, RelationshipConfig config)
        {
            // Check if multiple lookups to this target from same source
            var targetCount = listViewRelationships.Items.Cast<ListViewItem>()
                .Count(i => {
                    var c = (RelationshipConfig)i.Tag;
                    return c.SourceTable == config.SourceTable && c.TargetTable == config.TargetTable;
                });

            var statusText = config.IsActive ? "Active" : "Inactive";
            if (targetCount > 1)
            {
                statusText += " ⚠";
            }
            item.SubItems[4].Text = statusText;  // Status is column index 4, not 3
        }

        private void UpdateFinishButtonState()
        {
            // Must have a fact table selected (dimensions are optional)
            var hasFactTable = SelectedFactTable != null;
            btnFinish.Enabled = hasFactTable;
        }

        private void UpdateSnowflakeButtonState()
        {
            // Enable snowflake button only if a non-snowflake, checked dimension is selected
            if (listViewRelationships.SelectedItems.Count == 0)
            {
                btnAddSnowflake.Enabled = false;
                return;
            }

            var item = listViewRelationships.SelectedItems[0];
            var config = (RelationshipConfig)item.Tag;

            // Only allow snowflaking if:
            // 1. Item is checked (included)
            // 2. It's not already a snowflake relationship
            // 3. The table we want to snowflake from exists in our solution (SourceTable for reverse, TargetTable for normal)
            var tableToCheck = config.IsReverse ? config.SourceTable : config.TargetTable;
            var targetExists = _tables.Any(t => t.LogicalName == tableToCheck);
            btnAddSnowflake.Enabled = item.Checked && !config.IsSnowflake && targetExists;
        }

        private async void BtnAddSnowflake_Click(object? sender, EventArgs e)
        {
            if (listViewRelationships.SelectedItems.Count == 0) return;

            var item = listViewRelationships.SelectedItems[0];
            var config = (RelationshipConfig)item.Tag;
            
            // For reverse relationships (Fact:Many), we want to add snowflakes to the SOURCE table (child table)
            // For normal relationships (Many:1), we want to add snowflakes to the TARGET table (dimension)
            var tableToSnowflake = config.IsReverse ? config.SourceTable : config.TargetTable;
            var dimensionTable = _tables.FirstOrDefault(t => t.LogicalName == tableToSnowflake);

            if (dimensionTable == null) return;

            // Load attributes for the dimension table if not cached
            try
            {
                lblStatus.Text = $"Loading lookups for {dimensionTable.DisplayName}...";
                progressBar.Visible = true;

                if (!_tableAttributes.ContainsKey(dimensionTable.LogicalName))
                {
                    var attrs = await _client.GetAttributesAsync(dimensionTable.LogicalName);
                    _tableAttributes[dimensionTable.LogicalName] = attrs;
                }

                var dimAttrs = _tableAttributes[dimensionTable.LogicalName];
                var lookups = dimAttrs
                    .Where(a => a.AttributeType == "Lookup" && a.Targets != null && a.Targets.Any())
                    .OrderBy(a => a.DisplayName)
                    .ToList();

                // Filter out lookups that point back to the fact table, to itself, or to parents already added to THIS dimension
                var existingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                existingTargets.Add(SelectedFactTable!.LogicalName);  // Prevent dimension -> fact
                existingTargets.Add(dimensionTable.LogicalName);       // Prevent self-reference
                
                // Only exclude parent tables already added to THIS specific dimension
                // (Allow multiple dimensions to share the same parent table - common in snowflake schemas)
                var existingParentsForThisDimension = listViewRelationships.Items.Cast<ListViewItem>()
                    .Select(i => (RelationshipConfig)i.Tag)
                    .Where(r => r.IsSnowflake && r.SourceTable == dimensionTable.LogicalName)
                    .Select(r => r.TargetTable);
                foreach (var target in existingParentsForThisDimension)
                {
                    existingTargets.Add(target);
                }

                var availableLookups = lookups
                    .Where(l => l.Targets!.Any(t => !existingTargets.Contains(t)))
                    .ToList();

                if (!availableLookups.Any())
                {
                    MessageBox.Show(
                        $"No additional parent tables available for {dimensionTable.DisplayName}.\n\n" +
                        "All lookup targets are either already included or point back to existing tables.",
                        "No Parent Tables",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Show snowflake dialog
                using var snowflakeDialog = new SnowflakeDimensionDialog(
                    dimensionTable,
                    availableLookups,
                    _tables,
                    _currentRelationships.Where(r => r.IsSnowflake && r.SourceTable == dimensionTable.LogicalName).ToList());

                if (snowflakeDialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Add the selected snowflake relationships to our list
                    foreach (var rel in snowflakeDialog.SelectedRelationships)
                    {
                        rel.IsSnowflake = true;
                        AddSnowflakeRelationshipToList(rel);
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

        private void AddSnowflakeRelationshipToList(RelationshipConfig rel)
        {
            // Check if already exists
            if (listViewRelationships.Items.Cast<ListViewItem>()
                .Any(i => ((RelationshipConfig)i.Tag).SourceAttribute == rel.SourceAttribute &&
                          ((RelationshipConfig)i.Tag).TargetTable == rel.TargetTable))
                return;

            var targetTable = _tables.FirstOrDefault(t => t.LogicalName == rel.TargetTable);
            var targetDisplayName = targetTable?.DisplayName ?? rel.TargetTable;
            var cardinalityText = rel.IsReverse ? "Fact:Many" : "Many:1";

            var item = new ListViewItem("");
            item.Checked = true;
            item.SubItems.Add(cardinalityText);
            item.SubItems.Add(rel.DisplayName ?? rel.SourceAttribute);
            item.SubItems.Add(targetDisplayName);
            item.SubItems.Add(rel.IsActive ? "Active" : "Inactive");
            item.SubItems.Add("Snowflake");
            item.SubItems.Add(rel.TargetTable);
            item.Tag = rel;
            item.BackColor = Color.LightCyan;

            listViewRelationships.Items.Add(item);
            UpdateFinishButtonState();
        }

        private void BtnFinish_Click(object? sender, EventArgs e)
        {
            // Validate: ensure only one active relationship per source-target pair
            var checkedItems = listViewRelationships.CheckedItems.Cast<ListViewItem>().ToList();
            var configs = checkedItems.Select(i => (RelationshipConfig)i.Tag).ToList();

            // Group by source table + target table
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
                        $"No active relationship selected for table '{targetDisplay}'.\n\n" +
                        "Double-click a row to toggle Active/Inactive status.",
                        "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (activeCount > 1)
                {
                    MessageBox.Show(
                        $"Multiple active relationships to table '{targetDisplay}'.\n\n" +
                        "Only one lookup per target table can be marked as Active.\n" +
                        "Double-click a row to toggle Active/Inactive status.",
                        "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Build results
            SelectedRelationships = configs;

            // Build list of all selected tables (Fact + all dimension and detail tables)
            AllSelectedTables = new List<TableInfo> { SelectedFactTable! };
            
            // Collect all unique tables from relationships
            // For reverse relationships (Fact:Many), we need the SourceTable (detail table)
            // For normal relationships (Many:1), we need the TargetTable (dimension)
            // For snowflakes, we need both (source is dimension, target is parent dimension)
            var allTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var config in configs)
            {
                // For reverse relationships, the detail table is the SourceTable
                if (config.IsReverse)
                {
                    allTableNames.Add(config.SourceTable);
                }
                else
                {
                    // For normal Many:1 and snowflakes, add the TargetTable
                    allTableNames.Add(config.TargetTable);
                    
                    // For snowflakes, also ensure the source dimension is included
                    if (config.IsSnowflake)
                    {
                        allTableNames.Add(config.SourceTable);
                    }
                }
            }
            
            foreach (var tableName in allTableNames)
            {
                // Skip the fact table (already added)
                if (tableName.Equals(SelectedFactTable!.LogicalName, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                var table = _tables.FirstOrDefault(t => t.LogicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                if (table != null && !AllSelectedTables.Any(t => t.LogicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase)))
                {
                    AllSelectedTables.Add(table);
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    /// <summary>
    /// Dialog for selecting parent tables for a dimension (snowflake).
    /// One level only - cannot further snowflake.
    /// </summary>
    public class SnowflakeDimensionDialog : Form
    {
        private readonly TableInfo _dimensionTable;
        private readonly List<AttributeMetadata> _lookups;
        private readonly List<TableInfo> _allTables;
        private readonly List<RelationshipConfig> _existingRelationships;

        private ListView listViewParentTables = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;

        public List<RelationshipConfig> SelectedRelationships { get; private set; } = new();

        public SnowflakeDimensionDialog(
            TableInfo dimensionTable,
            List<AttributeMetadata> lookups,
            List<TableInfo> allTables,
            List<RelationshipConfig> existingRelationships)
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

            var lblHeader = new Label
            {
                Text = $"Dimension: {_dimensionTable.DisplayName} ({_dimensionTable.LogicalName})",
                Location = new Point(10, 15),
                AutoSize = true,
                Font = new Font(this.Font, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            this.Controls.Add(lblHeader);

            var lblInstructions = new Label
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
                CheckBoxes = true
            };
            listViewParentTables.Columns.Add("Include", 55);
            listViewParentTables.Columns.Add("Lookup Field", 200);
            listViewParentTables.Columns.Add("Parent Table", 200);
            listViewParentTables.Columns.Add("Status", 80);
            listViewParentTables.Columns.Add("Logical Name", 150);
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
        }

        private void PopulateList()
        {
            listViewParentTables.Items.Clear();

            // Group by target table
            var targetGroups = _lookups
                .SelectMany(l => l.Targets!.Select(t => new { Lookup = l, Target = t }))
                .GroupBy(x => x.Target)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Lookup).ToList());

            foreach (var lookup in _lookups)
            {
                foreach (var target in lookup.Targets!)
                {
                    var targetTable = _allTables.FirstOrDefault(t => t.LogicalName == target);
                    var targetDisplayName = targetTable?.DisplayName ?? target;

                    var multipleLookupsToTarget = targetGroups.ContainsKey(target) && targetGroups[target].Count > 1;

                    var existingRel = _existingRelationships.FirstOrDefault(r =>
                        r.SourceAttribute == lookup.LogicalName && r.TargetTable == target);

                    var isChecked = existingRel != null;
                    var isActive = existingRel?.IsActive ?? !multipleLookupsToTarget;

                    var statusText = isActive ? "Active" : "Inactive";
                    if (multipleLookupsToTarget)
                    {
                        statusText += " ⚠";
                    }

                    var item = new ListViewItem("");
                    item.Checked = isChecked;
                    item.SubItems.Add(lookup.DisplayName ?? lookup.LogicalName);
                    item.SubItems.Add(targetDisplayName);
                    item.SubItems.Add(statusText);
                    item.SubItems.Add(target);
                    item.Tag = new RelationshipConfig
                    {
                        SourceTable = _dimensionTable.LogicalName,
                        SourceAttribute = lookup.LogicalName,
                        TargetTable = target,
                        DisplayName = lookup.DisplayName,
                        IsActive = isActive,
                        IsSnowflake = true,
                        AssumeReferentialIntegrity = lookup.IsRequired  // Set based on whether lookup is required
                    };

                    if (multipleLookupsToTarget)
                    {
                        item.BackColor = Color.LightYellow;
                    }

                    listViewParentTables.Items.Add(item);
                }
            }
        }

        private void ListViewParentTables_DoubleClick(object? sender, EventArgs e)
        {
            if (listViewParentTables.SelectedItems.Count == 0) return;

            var item = listViewParentTables.SelectedItems[0];
            var config = (RelationshipConfig)item.Tag;

            config.IsActive = !config.IsActive;

            // If setting to active, set others to same target to inactive
            if (config.IsActive)
            {
                foreach (ListViewItem otherItem in listViewParentTables.Items)
                {
                    if (otherItem == item) continue;
                    var otherConfig = (RelationshipConfig)otherItem.Tag;
                    if (otherConfig.TargetTable == config.TargetTable && otherConfig.IsActive)
                    {
                        otherConfig.IsActive = false;
                        UpdateItemStatus(otherItem, otherConfig);
                    }
                }
            }

            UpdateItemStatus(item, config);
        }

        private void UpdateItemStatus(ListViewItem item, RelationshipConfig config)
        {
            var targetCount = listViewParentTables.Items.Cast<ListViewItem>()
                .Count(i => ((RelationshipConfig)i.Tag).TargetTable == config.TargetTable);

            var statusText = config.IsActive ? "Active" : "Inactive";
            if (targetCount > 1)
            {
                statusText += " ⚠";
            }
            item.SubItems[3].Text = statusText;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            var checkedItems = listViewParentTables.CheckedItems.Cast<ListViewItem>().ToList();
            var configs = checkedItems.Select(i => (RelationshipConfig)i.Tag).ToList();

            // Validate: one active per target
            var targetGroups = configs.GroupBy(c => c.TargetTable);
            foreach (var group in targetGroups)
            {
                var activeCount = group.Count(c => c.IsActive);
                if (activeCount == 0)
                {
                    var targetDisplay = _allTables.FirstOrDefault(t => t.LogicalName == group.Key)?.DisplayName ?? group.Key;
                    MessageBox.Show(
                        $"No active relationship selected for table '{targetDisplay}'.\n\n" +
                        "Double-click a row to toggle Active/Inactive status.",
                        "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                if (activeCount > 1)
                {
                    var targetDisplay = _allTables.FirstOrDefault(t => t.LogicalName == group.Key)?.DisplayName ?? group.Key;
                    MessageBox.Show(
                        $"Multiple active relationships to table '{targetDisplay}'.\n\n" +
                        "Only one lookup per target table can be marked as Active.",
                        "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            SelectedRelationships = configs;
        }
    }
}
