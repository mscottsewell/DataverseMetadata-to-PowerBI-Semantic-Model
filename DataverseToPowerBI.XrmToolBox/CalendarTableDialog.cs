// ===================================================================================
// CalendarTableDialog.cs - Date Table Configuration Dialog for XrmToolBox
// ===================================================================================
//
// PURPOSE:
// This dialog configures the Date (calendar) dimension table for Power BI semantic
// models. It enables timezone-aware date handling and establishes relationships
// between datetime fields and the Date dimension.
//
// DATE TABLE CONFIGURATION:
// Power BI semantic models benefit from a dedicated Date table for time intelligence
// calculations. This dialog allows users to:
//
// 1. PRIMARY DATE FIELD:
//    Select the main date field (e.g., "Created On") that will have an active
//    relationship to the Date dimension. This drives default time analysis.
//
// 2. TIMEZONE ADJUSTMENT:
//    Dataverse stores all dates in UTC. This setting adjusts dates to the user's
//    local timezone for accurate daily reporting. The offset is applied via
//    DATEADD() in the generated SQL queries.
//    
//    Note: Daylight saving time is NOT handled - uses base UTC offset only.
//
// 3. DATE RANGE:
//    Defines the year range for the generated Date table (default: current year +/- 5).
//
// 4. ADDITIONAL DATETIME FIELDS:
//    Other datetime fields can be wrapped to convert them to date-only values
//    with the same timezone adjustment, enabling filtering and relationships.
//
// OUTPUT:
// Returns a DateTableConfig object containing all settings, which is used by
// SemanticModelBuilder to generate the Date table and relationships.
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DataverseToPowerBI.XrmToolBox.Services;
using DataverseToPowerBI.Core.Models;

namespace DataverseToPowerBI.XrmToolBox
{
    public class CalendarTableDialog : Form
    {
        // UI Controls
        private ComboBox cboTable = null!;
        private ComboBox cboDateField = null!;
        private ComboBox cboTimeZone = null!;
        private NumericUpDown numStartYear = null!;
        private NumericUpDown numEndYear = null!;
        private CheckedListBox lstDateTimeFields = null!;
        private Button btnSelectAll = null!;
        private Button btnClearAll = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Label lblTableHelp = null!;
        private Label lblTimezoneHelp = null!;

        // Input data
        private readonly Dictionary<string, TableInfo> _selectedTables;
        private readonly Dictionary<string, Dictionary<string, AttributeDisplayInfo>> _attributeDisplayInfo;
        private readonly Dictionary<string, HashSet<string>> _selectedAttributes;
        private readonly string? _factTableName;
        private readonly DateTableConfig? _existingConfig;

        // All datetime fields across all tables
        private List<(string TableName, string FieldName, string DisplayName)> _allDateTimeFields = new();

        // Output
        public DateTableConfig? Config { get; private set; }

        public CalendarTableDialog(
            Dictionary<string, TableInfo> selectedTables,
            Dictionary<string, Dictionary<string, AttributeDisplayInfo>> attributeDisplayInfo,
            Dictionary<string, HashSet<string>> selectedAttributes,
            string? factTableName,
            DateTableConfig? existingConfig = null)
        {
            _selectedTables = selectedTables;
            _attributeDisplayInfo = attributeDisplayInfo;
            _selectedAttributes = selectedAttributes;
            _factTableName = factTableName;
            _existingConfig = existingConfig;

            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "Calendar Table Configuration";
            this.Size = new Size(550, 580);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 20;

            // Primary Date Field Section
            var lblPrimarySection = new Label
            {
                Text = "Primary Date Field",
                Location = new Point(20, y),
                Font = new Font(this.Font, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblPrimarySection);
            y += 25;

            var lblTable = new Label
            {
                Text = "Table:",
                Location = new Point(20, y),
                AutoSize = true
            };
            this.Controls.Add(lblTable);

            cboTable = new ComboBox
            {
                Location = new Point(100, y - 3),
                Size = new Size(150, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboTable.SelectedIndexChanged += CboTable_SelectedIndexChanged;
            this.Controls.Add(cboTable);

            var lblField = new Label
            {
                Text = "Date Field:",
                Location = new Point(270, y),
                AutoSize = true
            };
            this.Controls.Add(lblField);

            cboDateField = new ComboBox
            {
                Location = new Point(345, y - 3),
                Size = new Size(180, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(cboDateField);
            y += 30;

            lblTableHelp = new Label
            {
                Text = "Select the main table and date field that will link to the Date dimension.",
                Location = new Point(20, y),
                Size = new Size(500, 15),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblTableHelp);
            y += 30;

            // Timezone Section
            var lblTimezoneSection = new Label
            {
                Text = "Time Zone Adjustment",
                Location = new Point(20, y),
                Font = new Font(this.Font, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblTimezoneSection);
            y += 25;

            var lblTimeZone = new Label
            {
                Text = "Time Zone:",
                Location = new Point(20, y),
                AutoSize = true
            };
            this.Controls.Add(lblTimeZone);

            cboTimeZone = new ComboBox
            {
                Location = new Point(100, y - 3),
                Size = new Size(425, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(cboTimeZone);
            y += 30;

            lblTimezoneHelp = new Label
            {
                Text = "Dataverse stores dates in UTC. Select your timezone to adjust dates for reporting.\n" +
                       "Note: Daylight saving time changes are NOT applied - uses base UTC offset only.",
                Location = new Point(20, y),
                Size = new Size(500, 35),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblTimezoneHelp);
            y += 45;

            // Date Range Section
            var lblRangeSection = new Label
            {
                Text = "Date Range",
                Location = new Point(20, y),
                Font = new Font(this.Font, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblRangeSection);
            y += 25;

            var lblStartYear = new Label
            {
                Text = "Start Year:",
                Location = new Point(20, y),
                AutoSize = true
            };
            this.Controls.Add(lblStartYear);

            numStartYear = new NumericUpDown
            {
                Location = new Point(100, y - 3),
                Size = new Size(80, 23),
                Minimum = 1900,
                Maximum = 2100,
                Value = DateTime.Now.Year - 5
            };
            this.Controls.Add(numStartYear);

            var lblEndYear = new Label
            {
                Text = "End Year:",
                Location = new Point(200, y),
                AutoSize = true
            };
            this.Controls.Add(lblEndYear);

            numEndYear = new NumericUpDown
            {
                Location = new Point(275, y - 3),
                Size = new Size(80, 23),
                Minimum = 1900,
                Maximum = 2100,
                Value = DateTime.Now.Year + 5
            };
            this.Controls.Add(numEndYear);
            y += 30;

            var lblRangeHelp = new Label
            {
                Text = $"Default: {DateTime.Now.Year - 5} to {DateTime.Now.Year + 5} (current year +/- 5)",
                Location = new Point(20, y),
                Size = new Size(500, 15),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblRangeHelp);
            y += 30;

            // Additional DateTime Fields Section
            var lblAdditionalSection = new Label
            {
                Text = "Additional DateTime Fields to Adjust",
                Location = new Point(20, y),
                Font = new Font(this.Font, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblAdditionalSection);
            y += 25;

            lstDateTimeFields = new CheckedListBox
            {
                Location = new Point(20, y),
                Size = new Size(505, 140),
                CheckOnClick = true
            };
            this.Controls.Add(lstDateTimeFields);
            y += 145;

            btnSelectAll = new Button
            {
                Text = "Select All",
                Location = new Point(20, y),
                Size = new Size(80, 25)
            };
            btnSelectAll.Click += BtnSelectAll_Click;
            this.Controls.Add(btnSelectAll);

            btnClearAll = new Button
            {
                Text = "Clear All",
                Location = new Point(110, y),
                Size = new Size(80, 25)
            };
            btnClearAll.Click += BtnClearAll_Click;
            this.Controls.Add(btnClearAll);

            var lblAdditionalHelp = new Label
            {
                Text = "These fields will also be adjusted to the selected timezone and converted to date-only.",
                Location = new Point(200, y + 5),
                Size = new Size(325, 15),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblAdditionalHelp);
            y += 40;

            // OK / Cancel buttons
            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(340, y),
                Size = new Size(80, 28),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;
            this.Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(430, y),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void LoadData()
        {
            // Load tables into dropdown
            var tables = _selectedTables.Values
                .OrderBy(t => t.DisplayName ?? t.LogicalName)
                .ToList();

            foreach (var table in tables)
            {
                var displayText = string.IsNullOrEmpty(table.DisplayName)
                    ? table.LogicalName
                    : $"{table.DisplayName} ({table.LogicalName})";
                cboTable.Items.Add(new ComboItem(table.LogicalName, displayText));
            }

            // Select fact table by default, or first table
            if (!string.IsNullOrEmpty(_factTableName))
            {
                for (int i = 0; i < cboTable.Items.Count; i++)
                {
                    if (((ComboItem)cboTable.Items[i]).Value == _factTableName)
                    {
                        cboTable.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cboTable.SelectedIndex < 0 && cboTable.Items.Count > 0)
            {
                cboTable.SelectedIndex = 0;
            }

            // Load timezones
            LoadTimeZones();

            // Load all datetime fields from all tables
            LoadAllDateTimeFields();

            // If editing existing config, restore values
            if (_existingConfig != null)
            {
                RestoreExistingConfig();
            }
        }

        private void LoadTimeZones()
        {
            var timeZones = TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(tz => tz.BaseUtcOffset)
                .ToList();

            foreach (var tz in timeZones)
            {
                cboTimeZone.Items.Add(new TimeZoneItem(tz, tz.DisplayName));
            }

            // Select local timezone by default
            var localTz = TimeZoneInfo.Local;
            for (int i = 0; i < cboTimeZone.Items.Count; i++)
            {
                if (((TimeZoneItem)cboTimeZone.Items[i]).TimeZone.Id == localTz.Id)
                {
                    cboTimeZone.SelectedIndex = i;
                    break;
                }
            }
            if (cboTimeZone.SelectedIndex < 0 && cboTimeZone.Items.Count > 0)
            {
                cboTimeZone.SelectedIndex = 0;
            }
        }

        private void LoadAllDateTimeFields()
        {
            _allDateTimeFields.Clear();
            lstDateTimeFields.Items.Clear();

            foreach (var tableName in _selectedTables.Keys.OrderBy(k => k))
            {
                if (!_selectedAttributes.TryGetValue(tableName, out var selectedAttrNames))
                    continue;

                if (_attributeDisplayInfo.TryGetValue(tableName, out var attrs))
                {
                    var dateTimeAttrs = new List<AttributeDisplayInfo>();
                    foreach (var attr in attrs.Values)
                    {
                        var logicalName = attr.LogicalName;
                        var attributeType = attr.AttributeType;
                        if (string.IsNullOrWhiteSpace(logicalName) || string.IsNullOrWhiteSpace(attributeType))
                            continue;
                        var safeLogicalName = logicalName!;
                        var safeAttributeType = attributeType!;
                        if (!selectedAttrNames.Contains(safeLogicalName) || !IsDateTimeType(safeAttributeType))
                            continue;

                        dateTimeAttrs.Add(attr);
                    }

                    foreach (var attr in dateTimeAttrs.OrderBy(a => a.DisplayName ?? a.LogicalName))
                    {
                        var logicalName = attr.LogicalName ?? "";
                        var tableDisplay = _selectedTables.TryGetValue(tableName, out var tbl)
                            ? (tbl.DisplayName ?? tableName)
                            : tableName;
                        var fieldDisplay = attr.DisplayName ?? logicalName;
                        var itemText = $"{tableDisplay}.{fieldDisplay}";

                        _allDateTimeFields.Add((tableName, logicalName, itemText));
                        lstDateTimeFields.Items.Add(itemText);
                    }
                }
            }
        }

        private void RestoreExistingConfig()
        {
            var existingConfig = _existingConfig;
            if (existingConfig == null)
            {
                return;
            }

            // Restore table selection
            for (int i = 0; i < cboTable.Items.Count; i++)
            {
                if (((ComboItem)cboTable.Items[i]).Value == existingConfig.PrimaryDateTable)
                {
                    cboTable.SelectedIndex = i;
                    break;
                }
            }

            // Restore timezone
            for (int i = 0; i < cboTimeZone.Items.Count; i++)
            {
                if (((TimeZoneItem)cboTimeZone.Items[i]).TimeZone.Id == existingConfig.TimeZoneId)
                {
                    cboTimeZone.SelectedIndex = i;
                    break;
                }
            }

            // Restore year range
            numStartYear.Value = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, existingConfig.StartYear));
            numEndYear.Value = Math.Max(numEndYear.Minimum, Math.Min(numEndYear.Maximum, existingConfig.EndYear));

            // Restore checked fields
            foreach (var field in existingConfig.WrappedFields)
            {
                var idx = _allDateTimeFields.FindIndex(f =>
                    f.TableName == field.TableName && f.FieldName == field.FieldName);
                if (idx >= 0)
                {
                    lstDateTimeFields.SetItemChecked(idx, true);
                }
            }

            // Date field will be loaded when table is selected
            this.Load += (s, e) =>
            {
                for (int i = 0; i < cboDateField.Items.Count; i++)
                {
                    if (((ComboItem)cboDateField.Items[i]).Value == existingConfig.PrimaryDateField)
                    {
                        cboDateField.SelectedIndex = i;
                        break;
                    }
                }
            };
        }

        private void CboTable_SelectedIndexChanged(object? sender, EventArgs e)
        {
            cboDateField.Items.Clear();

            if (cboTable.SelectedItem is ComboItem selected)
            {
                var tableName = selected.Value;
                
                // VALIDATION: Only show date fields that are included in the selected attributes
                if (_attributeDisplayInfo.TryGetValue(tableName, out var attrs) &&
                    _selectedAttributes.TryGetValue(tableName, out var selectedAttrNames))
                {
                    var dateTimeAttrs = new List<AttributeDisplayInfo>();
                    foreach (var attr in attrs.Values)
                    {
                        var logicalName = attr.LogicalName;
                        var attributeType = attr.AttributeType;
                        if (string.IsNullOrWhiteSpace(logicalName) || string.IsNullOrWhiteSpace(attributeType))
                            continue;
                        var safeLogicalName = logicalName!;
                        var safeAttributeType = attributeType!;
                        if (!selectedAttrNames.Contains(safeLogicalName) || !IsDateTimeType(safeAttributeType))
                            continue;

                        dateTimeAttrs.Add(attr);
                    }

                    foreach (var attr in dateTimeAttrs.OrderBy(a => a.DisplayName ?? a.LogicalName))
                    {
                        var logicalName = attr.LogicalName ?? "";
                        var displayText = string.IsNullOrEmpty(attr.DisplayName)
                            ? logicalName
                            : $"{attr.DisplayName} ({logicalName})";
                        cboDateField.Items.Add(new ComboItem(logicalName, displayText));
                    }
                }

                if (cboDateField.Items.Count > 0)
                {
                    // Try to select createdon by default
                    var createdonIdx = -1;
                    for (int i = 0; i < cboDateField.Items.Count; i++)
                    {
                        if (((ComboItem)cboDateField.Items[i]).Value.Equals("createdon", StringComparison.OrdinalIgnoreCase))
                        {
                            createdonIdx = i;
                            break;
                        }
                    }
                    cboDateField.SelectedIndex = createdonIdx >= 0 ? createdonIdx : 0;
                }
                else
                {
                    // No date fields available - show helpful message
                    MessageBox.Show(
                        $"No date fields are selected for the '{selected.Display}' table.\n\n" +
                        "Please ensure you have included at least one date/datetime field in your query " +
                        "before configuring the Date table relationship.",
                        "No Date Fields Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            UpdatePrimaryFieldInList();
        }

        private void UpdatePrimaryFieldInList()
        {
            if (cboTable.SelectedItem is ComboItem tableItem &&
                cboDateField.SelectedItem is ComboItem fieldItem)
            {
                var idx = _allDateTimeFields.FindIndex(f =>
                    f.TableName == tableItem.Value && f.FieldName == fieldItem.Value);
                if (idx >= 0)
                {
                    lstDateTimeFields.SetItemChecked(idx, true);
                }
            }
        }

        private void BtnSelectAll_Click(object? sender, EventArgs e)
        {
            for (int i = 0; i < lstDateTimeFields.Items.Count; i++)
            {
                lstDateTimeFields.SetItemChecked(i, true);
            }
        }

        private void BtnClearAll_Click(object? sender, EventArgs e)
        {
            for (int i = 0; i < lstDateTimeFields.Items.Count; i++)
            {
                lstDateTimeFields.SetItemChecked(i, false);
            }
            UpdatePrimaryFieldInList();
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (cboTable.SelectedItem is not ComboItem tableItem)
            {
                MessageBox.Show("Please select a table.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (cboDateField.SelectedItem is not ComboItem fieldItem)
            {
                MessageBox.Show("Please select a date field.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (cboTimeZone.SelectedItem is not TimeZoneItem tzItem)
            {
                MessageBox.Show("Please select a timezone.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            if (numEndYear.Value < numStartYear.Value)
            {
                MessageBox.Show("End year must be greater than or equal to start year.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Build config
            var wrappedFields = new List<DateTimeFieldConfig>();
            for (int i = 0; i < lstDateTimeFields.Items.Count; i++)
            {
                if (lstDateTimeFields.GetItemChecked(i))
                {
                    var field = _allDateTimeFields[i];
                    wrappedFields.Add(new DateTimeFieldConfig
                    {
                        TableName = field.TableName,
                        FieldName = field.FieldName,
                        ConvertToDateOnly = true
                    });
                }
            }

            // Ensure primary field is included
            if (!wrappedFields.Any(f => f.TableName == tableItem.Value && f.FieldName == fieldItem.Value))
            {
                wrappedFields.Insert(0, new DateTimeFieldConfig
                {
                    TableName = tableItem.Value,
                    FieldName = fieldItem.Value,
                    ConvertToDateOnly = true
                });
            }

            Config = new DateTableConfig
            {
                PrimaryDateTable = tableItem.Value,
                PrimaryDateField = fieldItem.Value,
                TimeZoneId = tzItem.TimeZone.Id,
                UtcOffsetHours = tzItem.TimeZone.BaseUtcOffset.TotalHours,
                StartYear = (int)numStartYear.Value,
                EndYear = (int)numEndYear.Value,
                WrappedFields = wrappedFields
            };
        }

        private bool IsDateTimeType(string attributeType)
        {
            if (string.IsNullOrWhiteSpace(attributeType))
                return false;
            
            var type = attributeType.Trim();
            return type.Equals("DateTime", StringComparison.OrdinalIgnoreCase) ||
                   type.Equals("DateTimeType", StringComparison.OrdinalIgnoreCase) ||
                   type.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                   type.Equals("DateOnly", StringComparison.OrdinalIgnoreCase) ||
                   type.IndexOf("DateTime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (type.EndsWith("AttributeMetadata", StringComparison.OrdinalIgnoreCase) && 
                    type.IndexOf("Date", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private class ComboItem
        {
            public string Value { get; }
            public string Display { get; }

            public ComboItem(string value, string display)
            {
                Value = value;
                Display = display;
            }

            public override string ToString() => Display;
        }

        private class TimeZoneItem
        {
            public TimeZoneInfo TimeZone { get; }
            public string Display { get; }

            public TimeZoneItem(TimeZoneInfo timeZone, string display)
            {
                TimeZone = timeZone;
                Display = display;
            }

            public override string ToString() => Display;
        }
    }
}
