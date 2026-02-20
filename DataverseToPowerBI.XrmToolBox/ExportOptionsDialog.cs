// ===================================================================================
// ExportOptionsDialog.cs - CSV Export Table Selection
// ===================================================================================
//
// PURPOSE:
// Provides a dialog for selecting which tables to include in a CSV
// documentation export. CSV files are human-readable and export-only
// (not importable). For full JSON import/export, no dialog is needed
// because the entire model is serialized.
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// Dialog for selecting which tables to include in a CSV documentation export.
    /// </summary>
    public class ExportOptionsDialog : Form
    {
        private ListView listViewTables = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;
        private Button btnExport = null!;
        private Button btnCancel = null!;
        private Label lblStatus = null!;

        private readonly SemanticModelConfig _model;
        private bool _isLoading;

        /// <summary>
        /// The tables the user selected for export.
        /// </summary>
        public HashSet<string> SelectedTables { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ExportOptionsDialog(SemanticModelConfig model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
            PopulateTables();
        }

        private void InitializeComponent()
        {
            Text = $"CSV Export - {_model.Name}";
            Size = new Size(480, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 15;

            // Header
            var lblHeader = new Label
            {
                Text = "Select tables to include in CSV documentation:",
                Location = new Point(15, y),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };
            Controls.Add(lblHeader);
            y += 25;

            listViewTables = new ListView
            {
                Location = new Point(15, y),
                Size = new Size(430, 240),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true
            };
            listViewTables.Columns.Add("", 30);
            listViewTables.Columns.Add("Display Name", 180);
            listViewTables.Columns.Add("Logical Name", 140);
            listViewTables.Columns.Add("Role", 60);
            listViewTables.ItemChecked += ListViewTables_ItemChecked;
            Controls.Add(listViewTables);
            y += 245;

            // Select All / Deselect All
            btnSelectAll = new Button
            {
                Text = "Select All",
                Location = new Point(15, y + 5),
                Size = new Size(80, 28)
            };
            btnSelectAll.Click += (s, e) => SetAllChecked(true);
            Controls.Add(btnSelectAll);

            btnDeselectAll = new Button
            {
                Text = "Deselect All",
                Location = new Point(100, y + 5),
                Size = new Size(85, 28)
            };
            btnDeselectAll.Click += (s, e) => SetAllChecked(false);
            Controls.Add(btnDeselectAll);

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(195, y + 10),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            Controls.Add(lblStatus);
            y += 45;

            // Export / Cancel
            btnExport = new Button
            {
                Text = "Export...",
                Location = new Point(280, y),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            btnExport.Click += BtnExport_Click;
            Controls.Add(btnExport);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(365, y),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = btnExport;
            CancelButton = btnCancel;
        }

        private void PopulateTables()
        {
            _isLoading = true;
            listViewTables.BeginUpdate();

            var settings = _model.PluginSettings;
            var tableNames = settings?.SelectedTableNames ?? new List<string>();
            var displayInfo = settings?.TableDisplayInfo ?? new Dictionary<string, TableDisplayInfo>();
            var factTable = settings?.FactTable;

            foreach (var tableName in tableNames.OrderBy(t => displayInfo.ContainsKey(t) ? displayInfo[t].DisplayName ?? t : t))
            {
                var info = displayInfo.ContainsKey(tableName) ? displayInfo[tableName] : null;
                var display = info?.DisplayName ?? tableName;
                var role = tableName.Equals(factTable, StringComparison.OrdinalIgnoreCase) ? "Fact" : "Dim";

                var item = new ListViewItem("");
                item.Checked = true;
                item.SubItems.Add(display);
                item.SubItems.Add(tableName);
                item.SubItems.Add(role);
                item.Tag = tableName;
                listViewTables.Items.Add(item);
            }

            listViewTables.EndUpdate();
            _isLoading = false;
            UpdateStatus();
        }

        private void SetAllChecked(bool isChecked)
        {
            _isLoading = true;
            foreach (ListViewItem item in listViewTables.Items)
                item.Checked = isChecked;
            _isLoading = false;
            UpdateStatus();
        }

        private void ListViewTables_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            if (_isLoading) return;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var count = listViewTables.CheckedItems.Count;
            var total = listViewTables.Items.Count;
            lblStatus.Text = $"{count} of {total} tables selected";
            btnExport.Enabled = count > 0;
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            SelectedTables.Clear();
            foreach (ListViewItem item in listViewTables.CheckedItems)
            {
                if (item.Tag is string tableName)
                    SelectedTables.Add(tableName);
            }
        }

        /// <summary>
        /// Filters a SemanticModelConfig to only include the specified tables.
        /// Returns a deep copy with filtered PluginSettings.
        /// </summary>
        public static SemanticModelConfig FilterModelByTables(SemanticModelConfig source, HashSet<string> tables)
        {
            var settings = source.PluginSettings ?? new PluginSettings();

            var filtered = new SemanticModelConfig
            {
                Name = source.Name,
                DataverseUrl = source.DataverseUrl,
                WorkingFolder = source.WorkingFolder,
                TemplatePath = source.TemplatePath,
                CreatedDate = source.CreatedDate,
                LastUsed = source.LastUsed,
                ConnectionType = source.ConnectionType,
                FabricLinkSQLEndpoint = source.FabricLinkSQLEndpoint,
                FabricLinkSQLDatabase = source.FabricLinkSQLDatabase,
                UseDisplayNameAliasesInSql = source.UseDisplayNameAliasesInSql,
                StorageMode = source.StorageMode,
                PluginSettings = new PluginSettings
                {
                    LastSolutionId = settings.LastSolutionId,
                    LastSolutionName = settings.LastSolutionName,
                    FactTable = settings.FactTable,
                    ShowAllAttributes = settings.ShowAllAttributes,
                    LanguageCode = settings.LanguageCode,
                    DateTableConfig = settings.DateTableConfig,
                    SelectedTableNames = settings.SelectedTableNames
                        .Where(t => tables.Contains(t))
                        .ToList(),
                    SelectedAttributes = settings.SelectedAttributes
                        .Where(kv => tables.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
                    SelectedFormIds = settings.SelectedFormIds
                        .Where(kv => tables.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value),
                    SelectedViewIds = settings.SelectedViewIds
                        .Where(kv => tables.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value),
                    Relationships = settings.Relationships
                        .Where(r => tables.Contains(r.SourceTable ?? "") && tables.Contains(r.TargetTable ?? ""))
                        .Select(r => new SerializedRelationship
                        {
                            SourceTable = r.SourceTable,
                            SourceAttribute = r.SourceAttribute,
                            TargetTable = r.TargetTable,
                            IsActive = r.IsActive,
                            IsSnowflake = r.IsSnowflake,
                            AssumeReferentialIntegrity = r.AssumeReferentialIntegrity
                        }).ToList(),
                    TableDisplayInfo = settings.TableDisplayInfo
                        .Where(kv => tables.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => new TableDisplayInfo
                        {
                            DisplayName = kv.Value.DisplayName,
                            SchemaName = kv.Value.SchemaName,
                            PrimaryIdAttribute = kv.Value.PrimaryIdAttribute,
                            PrimaryNameAttribute = kv.Value.PrimaryNameAttribute
                        }),
                    AttributeDisplayNameOverrides = settings.AttributeDisplayNameOverrides
                        .Where(kv => tables.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => new Dictionary<string, string>(kv.Value)),
                    TableStorageModes = settings.TableStorageModes
                        ?.Where(kv => tables.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value)
                        ?? new Dictionary<string, string>(),
                    ExpandedLookups = settings.ExpandedLookups
                        .Where(kv => tables.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value.Select(e => new SerializedExpandedLookup
                        {
                            LookupAttributeName = e.LookupAttributeName,
                            TargetTableLogicalName = e.TargetTableLogicalName,
                            TargetTableDisplayName = e.TargetTableDisplayName,
                            TargetTablePrimaryKey = e.TargetTablePrimaryKey,
                            FormId = e.FormId,
                            Attributes = e.Attributes.Select(a => new SerializedExpandedLookupAttribute
                            {
                                LogicalName = a.LogicalName,
                                DisplayName = a.DisplayName,
                                AttributeType = a.AttributeType,
                                SchemaName = a.SchemaName,
                                Targets = a.Targets?.ToList(),
                                VirtualAttributeName = a.VirtualAttributeName,
                                IsGlobal = a.IsGlobal,
                                OptionSetName = a.OptionSetName
                            }).ToList()
                        }).ToList())
                }
            };

            return filtered;
        }

        /// <summary>
        /// Exports the model configuration as CSV files in the specified folder.
        /// Creates: Tables.csv, Attributes.csv, Relationships.csv, and optionally ExpandedLookups.csv.
        /// </summary>
        public static void ExportAsCsv(SemanticModelConfig model, string folderPath)
        {
            var settings = model.PluginSettings ?? new PluginSettings();
            var displayInfo = settings.TableDisplayInfo ?? new Dictionary<string, TableDisplayInfo>();
            var overrides = settings.AttributeDisplayNameOverrides ?? new Dictionary<string, Dictionary<string, string>>();

            Directory.CreateDirectory(folderPath);

            // Tables.csv
            var sb = new StringBuilder();
            sb.AppendLine("Logical Name,Display Name,Schema Name,Role,Storage Mode,Form ID,View ID");
            foreach (var tableName in settings.SelectedTableNames.OrderBy(t => t))
            {
                var info = displayInfo.ContainsKey(tableName) ? displayInfo[tableName] : null;
                var role = tableName.Equals(settings.FactTable, StringComparison.OrdinalIgnoreCase) ? "Fact" : "Dimension";
                var storageMode = settings.TableStorageModes != null && settings.TableStorageModes.ContainsKey(tableName)
                    ? settings.TableStorageModes[tableName] : "";
                var formId = settings.SelectedFormIds.ContainsKey(tableName) ? settings.SelectedFormIds[tableName] : "";
                var viewId = settings.SelectedViewIds.ContainsKey(tableName) ? settings.SelectedViewIds[tableName] : "";

                sb.AppendLine($"{CsvEscape(tableName)},{CsvEscape(info?.DisplayName ?? "")},{CsvEscape(info?.SchemaName ?? "")},{role},{CsvEscape(storageMode)},{CsvEscape(formId)},{CsvEscape(viewId)}");
            }
            File.WriteAllText(Path.Combine(folderPath, "Tables.csv"), sb.ToString(), Encoding.UTF8);

            // Attributes.csv
            sb.Clear();
            sb.AppendLine("Table,Attribute Logical Name,Display Name Override,Attribute Type");
            foreach (var tableName in settings.SelectedTableNames.OrderBy(t => t))
            {
                var attrs = settings.SelectedAttributes.ContainsKey(tableName)
                    ? settings.SelectedAttributes[tableName] : new List<string>();
                var tableOverrides = overrides.ContainsKey(tableName) ? overrides[tableName] : new Dictionary<string, string>();

                foreach (var attr in attrs.OrderBy(a => a))
                {
                    var overrideName = tableOverrides.ContainsKey(attr) ? tableOverrides[attr] : "";
                    sb.AppendLine($"{CsvEscape(tableName)},{CsvEscape(attr)},{CsvEscape(overrideName)},");
                }
            }
            File.WriteAllText(Path.Combine(folderPath, "Attributes.csv"), sb.ToString(), Encoding.UTF8);

            // Relationships.csv
            sb.Clear();
            sb.AppendLine("Source Table,Source Attribute,Target Table,Active,Snowflake,Referential Integrity");
            foreach (var rel in settings.Relationships.OrderBy(r => r.SourceTable).ThenBy(r => r.SourceAttribute))
            {
                sb.AppendLine($"{CsvEscape(rel.SourceTable ?? "")},{CsvEscape(rel.SourceAttribute ?? "")},{CsvEscape(rel.TargetTable ?? "")},{rel.IsActive},{rel.IsSnowflake},{rel.AssumeReferentialIntegrity}");
            }
            File.WriteAllText(Path.Combine(folderPath, "Relationships.csv"), sb.ToString(), Encoding.UTF8);

            // ExpandedLookups.csv (only if any exist)
            if (settings.ExpandedLookups != null && settings.ExpandedLookups.Count > 0)
            {
                sb.Clear();
                sb.AppendLine("Source Table,Lookup Attribute,Target Table,Target Display Name,Expanded Attribute,Attribute Display Name,Attribute Type");
                foreach (var kvp in settings.ExpandedLookups.OrderBy(kv => kv.Key))
                {
                    foreach (var expand in kvp.Value)
                    {
                        foreach (var attr in expand.Attributes.OrderBy(a => a.LogicalName))
                        {
                            sb.AppendLine($"{CsvEscape(kvp.Key)},{CsvEscape(expand.LookupAttributeName)},{CsvEscape(expand.TargetTableLogicalName)},{CsvEscape(expand.TargetTableDisplayName ?? "")},{CsvEscape(attr.LogicalName)},{CsvEscape(attr.DisplayName ?? "")},{CsvEscape(attr.AttributeType ?? "")}");
                        }
                    }
                }
                File.WriteAllText(Path.Combine(folderPath, "ExpandedLookups.csv"), sb.ToString(), Encoding.UTF8);
            }

            // Summary.csv - model-level metadata
            sb.Clear();
            sb.AppendLine("Property,Value");
            sb.AppendLine($"Model Name,{CsvEscape(model.Name)}");
            sb.AppendLine($"Environment URL,{CsvEscape(model.DataverseUrl)}");
            sb.AppendLine($"Connection Type,{CsvEscape(model.ConnectionType)}");
            sb.AppendLine($"Storage Mode,{CsvEscape(model.StorageMode)}");
            sb.AppendLine($"Use Display Name Aliases,{model.UseDisplayNameAliasesInSql}");
            sb.AppendLine($"Language Code,{settings.LanguageCode}");
            sb.AppendLine($"Total Tables,{settings.SelectedTableNames.Count}");
            sb.AppendLine($"Total Relationships,{settings.Relationships.Count}");
            if (!string.IsNullOrEmpty(model.FabricLinkSQLEndpoint))
                sb.AppendLine($"Fabric SQL Endpoint,{CsvEscape(model.FabricLinkSQLEndpoint)}");
            if (!string.IsNullOrEmpty(model.FabricLinkSQLDatabase))
                sb.AppendLine($"Fabric SQL Database,{CsvEscape(model.FabricLinkSQLDatabase)}");
            sb.AppendLine($"Exported,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            File.WriteAllText(Path.Combine(folderPath, "Summary.csv"), sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Escapes a value for CSV output (RFC 4180 compliant).
        /// </summary>
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
