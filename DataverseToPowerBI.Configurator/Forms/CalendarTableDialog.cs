// =============================================================================
// CalendarTableDialog.cs - Date/Calendar Table Configuration Dialog
// =============================================================================
// Purpose: Configures the Date dimension table for the Power BI semantic model.
//
// The Date table is essential for time intelligence calculations in Power BI.
// This dialog allows users to:
//   - Select the primary date field that links to the calendar dimension
//   - Choose a timezone for UTC-to-local date conversion
//   - Set the date range (start/end year) for the generated calendar
//   - Select additional DateTime fields for timezone adjustment
//
// Key Features:
//   - Auto-detection of DateTime fields across all selected tables
//   - Timezone selection with base UTC offset (not DST-aware)
//   - Support for restoring existing configuration
//   - Validation of user inputs before saving
//
// Output:
//   DateTableConfig object containing all calendar configuration
// =============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Models;

namespace DataverseToPowerBI.Configurator.Forms
{
    /// <summary>
    /// Dialog for configuring the Date/Calendar dimension table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Power BI analytics rely on a proper Date dimension for time intelligence
    /// functions like TOTALYTD, SAMEPERIODLASTYEAR, etc. This dialog configures:
    /// </para>
    /// <list type="bullet">
    ///   <item>Primary date field for calendar relationship</item>
    ///   <item>Timezone for UTC-to-local conversion</item>
    ///   <item>Date range for the generated calendar rows</item>
    ///   <item>Additional DateTime fields to adjust for timezone</item>
    /// </list>
    /// </remarks>
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
                // DisplayName already includes UTC offset, e.g., "(UTC-06:00) Central Time (US & Canada)"
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
                // Get selected attributes for this table
                if (!_selectedAttributes.TryGetValue(tableName, out var selectedAttrNames))
                    continue;

                if (_attributeDisplayInfo.TryGetValue(tableName, out var attrs))
                {
                    // More robust DateTime field detection - only include selected attributes
                    var dateTimeAttrs = attrs.Values
                        .Where(a => selectedAttrNames.Contains(a.LogicalName) &&
                                   !string.IsNullOrWhiteSpace(a.AttributeType) && 
                                   (a.AttributeType.Equals("DateTime", StringComparison.OrdinalIgnoreCase) ||
                                    a.AttributeType.Equals("DateTimeType", StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(a => a.DisplayName ?? a.LogicalName);

                    foreach (var attr in dateTimeAttrs)
                    {
                        var tableDisplay = _selectedTables.TryGetValue(tableName, out var tbl)
                            ? (tbl.DisplayName ?? tableName)
                            : tableName;
                        var fieldDisplay = attr.DisplayName ?? attr.LogicalName;
                        var itemText = $"{tableDisplay}.{fieldDisplay}";

                        _allDateTimeFields.Add((tableName, attr.LogicalName, itemText));
                        lstDateTimeFields.Items.Add(itemText);
                    }
                }
            }
        }

        private void RestoreExistingConfig()
        {
            // Restore table selection
            for (int i = 0; i < cboTable.Items.Count; i++)
            {
                if (((ComboItem)cboTable.Items[i]).Value == _existingConfig!.PrimaryDateTable)
                {
                    cboTable.SelectedIndex = i;
                    break;
                }
            }

            // Restore timezone
            for (int i = 0; i < cboTimeZone.Items.Count; i++)
            {
                if (((TimeZoneItem)cboTimeZone.Items[i]).TimeZone.Id == _existingConfig.TimeZoneId)
                {
                    cboTimeZone.SelectedIndex = i;
                    break;
                }
            }

            // Restore year range
            numStartYear.Value = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, _existingConfig.StartYear));
            numEndYear.Value = Math.Max(numEndYear.Minimum, Math.Min(numEndYear.Maximum, _existingConfig.EndYear));

            // Restore checked fields
            foreach (var field in _existingConfig.WrappedFields)
            {
                var idx = _allDateTimeFields.FindIndex(f =>
                    f.TableName == field.TableName && f.FieldName == field.FieldName);
                if (idx >= 0)
                {
                    lstDateTimeFields.SetItemChecked(idx, true);
                }
            }

            // Date field will be loaded when table is selected - restore it after the form loads
            this.Load += (s, e) =>
            {
                for (int i = 0; i < cboDateField.Items.Count; i++)
                {
                    if (((ComboItem)cboDateField.Items[i]).Value == _existingConfig.PrimaryDateField)
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
                Services.DebugLogger.Log($"CalendarTableDialog: Table selected = {tableName}");
                
                if (_attributeDisplayInfo.TryGetValue(tableName, out var attrs))
                {
                    Services.DebugLogger.Log($"  Found {attrs.Count} attributes in _attributeDisplayInfo");
                    
                    // More robust DateTime field detection - check for various DateTime type strings
                    var dateTimeAttrs = attrs.Values
                        .Where(a => !string.IsNullOrWhiteSpace(a.AttributeType) && 
                                   IsDateTimeType(a.AttributeType))
                        .OrderBy(a => a.DisplayName ?? a.LogicalName)
                        .ToList();

                    Services.DebugLogger.Log($"  Found {dateTimeAttrs.Count} DateTime attributes");
                    
                    foreach (var attr in dateTimeAttrs)
                    {
                        Services.DebugLogger.Log($"    - {attr.LogicalName} (Type: {attr.AttributeType})");
                        var displayText = string.IsNullOrEmpty(attr.DisplayName)
                            ? attr.LogicalName
                            : $"{attr.DisplayName} ({attr.LogicalName})";
                        cboDateField.Items.Add(new ComboItem(attr.LogicalName, displayText));
                    }
                    
                    if (dateTimeAttrs.Count == 0)
                    {
                        // Debug: Show ALL attribute types for this table to help diagnose
                        Services.DebugLogger.Log("  WARNING: No DateTime attributes found! All attribute types for this table:");
                        var attrList = new System.Text.StringBuilder();
                        attrList.AppendLine($"Table: {tableName}");
                        attrList.AppendLine($"Total attributes: {attrs.Values.Count}");
                        attrList.AppendLine();
                        
                        foreach (var attr in attrs.Values.OrderBy(a => a.LogicalName))
                        {
                            var logMsg = $"    - {attr.LogicalName} (Display: {attr.DisplayName}): Type='{attr.AttributeType}'";
                            Services.DebugLogger.Log(logMsg);
                            attrList.AppendLine($"{attr.LogicalName}: {attr.AttributeType}");
                        }
                        
                        // Show a warning to the user WITH the attribute types
                        MessageBox.Show(
                            $"No DateTime fields found for table '{_selectedTables[tableName].DisplayName ?? tableName}'.\n\n" +
                            $"Attribute types found:\n" +
                            attrList.ToString() +
                            $"\nExpecting: DateTime, DateTimeType, Date, or DateOnly",
                            "No DateTime Fields",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    Services.DebugLogger.Log($"  Table '{tableName}' not found in _attributeDisplayInfo!");
                    Services.DebugLogger.Log($"  Available tables in _attributeDisplayInfo: {string.Join(", ", _attributeDisplayInfo.Keys)}");
                    
                    // Show available table keys for debugging
                    foreach (var key in _attributeDisplayInfo.Keys)
                    {
                        var tableInfo = _selectedTables.ContainsKey(key) ? _selectedTables[key] : null;
                        Services.DebugLogger.Log($"    - Key: '{key}', Display: '{tableInfo?.DisplayName ?? "N/A"}'");
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
            }

            UpdatePrimaryFieldInList();
        }

        private void UpdatePrimaryFieldInList()
        {
            // Make sure the primary field is checked and indicated
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
            // Keep primary field checked
            UpdatePrimaryFieldInList();
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            // Validate
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

        /// <summary>
        /// Check if an AttributeType string represents a DateTime field
        /// </summary>
        private bool IsDateTimeType(string attributeType)
        {
            if (string.IsNullOrWhiteSpace(attributeType))
                return false;
            
            // Check for common DateTime type variations from Dataverse API
            // Dataverse may return full type names like "DateTimeAttributeMetadata"
            var type = attributeType.Trim();
            return type.Equals("DateTime", StringComparison.OrdinalIgnoreCase) ||
                   type.Equals("DateTimeType", StringComparison.OrdinalIgnoreCase) ||
                   type.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                   type.Equals("DateOnly", StringComparison.OrdinalIgnoreCase) ||
                   type.Contains("DateTime", StringComparison.OrdinalIgnoreCase) ||
                   (type.EndsWith("AttributeMetadata", StringComparison.OrdinalIgnoreCase) && type.Contains("Date", StringComparison.OrdinalIgnoreCase));
        }

        // Helper class for ComboBox items
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

        // Helper class for timezone ComboBox items
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
