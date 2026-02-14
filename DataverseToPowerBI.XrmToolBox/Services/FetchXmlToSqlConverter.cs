// ===================================================================================
// FetchXmlToSqlConverter.cs - FetchXML to SQL WHERE Clause Translation
// ===================================================================================
//
// PURPOSE:
// Converts Dataverse FetchXML filter conditions to SQL WHERE clauses for use in
// Power BI DirectQuery partition expressions. This enables view-based filtering
// to be applied directly in the TMDL table definitions.
//
// INTEGRATION:
// Used by SemanticModelBuilder to embed view filters in TMDL partition queries.
//
// SUPPORTED OPERATORS:
// Basic Comparison: eq, ne, gt, ge, lt, le
// Null Checking: null, not-null
// String Matching: like, not-like, begins-with, ends-with
// Date Relative: today, yesterday, this-week, last-month, etc.
// Date Dynamic: last-x-days, next-x-months, older-x-years, etc.
// List Operations: in, not-in
// User Context: eq-userid, ne-userid, eq-userteams, ne-userteams (TDS only, not FabricLink)
//
// TIMEZONE HANDLING:
// All date comparisons include UTC offset adjustment using DATEADD(hour, offset, column)
// to convert UTC-stored dates to the user's local timezone.
//
// FABRICLINK LIMITATIONS:
// User context operators (eq-userid, ne-userid, eq-userteams, ne-userteams) are NOT
// supported in FabricLink mode because Direct Lake queries cannot use CURRENT_USER
// constructs. These filters are skipped and logged as unsupported when isFabricLink=true.
// Use DataverseTDS connection mode if row-level security based on current user is required.
//
// OUTPUT FORMAT:
// Returns a ConversionResult containing:
// - SqlWhereClause: The generated SQL WHERE clause
// - IsFullySupported: False if any operators couldn't be translated
// - UnsupportedFeatures: List of operators that weren't converted
// - DebugLog: Detailed conversion trace for troubleshooting
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DataverseToPowerBI.XrmToolBox.Services
{
    /// <summary>
    /// Converts FetchXML filter conditions to SQL WHERE clauses
    /// </summary>
    public class FetchXmlToSqlConverter
    {
        private readonly List<string> _debugLog = new();
        private readonly List<string> _unsupportedFeatures = new();
        private bool _hasUnsupportedFeatures = false;
        private readonly int _utcOffsetHours;
        private readonly bool _isFabricLink;
        private readonly bool _isImportMode;

        public FetchXmlToSqlConverter(int utcOffsetHours = -6, bool isFabricLink = false, bool isImportMode = false)
        {
            _utcOffsetHours = utcOffsetHours;
            _isFabricLink = isFabricLink;
            _isImportMode = isImportMode;
        }

        /// <summary>
        /// Securely parses XML to prevent XXE (XML External Entity) attacks.
        /// </summary>
        private static XDocument ParseXmlSecurely(string xml)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader);
        }

        public class ConversionResult
        {
            public string SqlWhereClause { get; set; } = "";
            public bool IsFullySupported { get; set; } = true;
            public List<string> UnsupportedFeatures { get; set; } = new();
            public List<string> DebugLog { get; set; } = new();
            public string Summary { get; set; } = "";
        }

        /// <summary>
        /// Converts FetchXML to SQL WHERE clause
        /// </summary>
        public ConversionResult ConvertToWhereClause(string fetchXml, string tableAlias = "Base")
        {
            _debugLog.Clear();
            _unsupportedFeatures.Clear();
            _hasUnsupportedFeatures = false;

            try
            {
                _debugLog.Add($"Starting FetchXML conversion for table alias: {tableAlias}");
                
                if (string.IsNullOrWhiteSpace(fetchXml))
                {
                    _debugLog.Add("FetchXML is empty");
                    return CreateResult("", true);
                }

                var doc = ParseXmlSecurely(fetchXml);
                var entity = doc.Root?.Element("entity");
                
                if (entity == null)
                {
                    _debugLog.Add("No entity element found in FetchXML");
                    return CreateResult("", true);
                }

                var entityName = entity.Attribute("name")?.Value ?? "unknown";
                _debugLog.Add($"Entity: {entityName}");

                // Find all filter elements
                var filters = entity.Elements("filter").ToList();
                var linkEntities = entity.Elements("link-entity").ToList();

                var whereClauses = new List<string>();
                
                // Process main entity filters
                if (filters.Any())
                {
                    _debugLog.Add($"Processing {filters.Count} main entity filter(s)");
                    foreach (var filter in filters)
                    {
                        var clause = ProcessFilter(filter, tableAlias);
                        if (!string.IsNullOrWhiteSpace(clause))
                        {
                            whereClauses.Add(clause);
                        }
                    }
                }
                
                // Process link-entity filters
                if (linkEntities.Any())
                {
                    _debugLog.Add($"Processing {linkEntities.Count} link-entity filter(s)");
                    foreach (var linkEntity in linkEntities)
                    {
                        var linkClauses = ProcessLinkEntityFilters(linkEntity, tableAlias);
                        if (linkClauses.Any())
                        {
                            whereClauses.AddRange(linkClauses);
                        }
                    }
                }

                var finalClause = whereClauses.Any() 
                    ? string.Join(" AND ", whereClauses.Select(c => $"({c})"))
                    : "";

                _debugLog.Add($"Final WHERE clause: {finalClause}");

                return CreateResult(finalClause, !_hasUnsupportedFeatures);
            }
            catch (Exception ex)
            {
                _debugLog.Add($"ERROR: {ex.Message}");
                _debugLog.Add($"Stack: {ex.StackTrace}");
                LogUnsupported($"Failed to parse FetchXML: {ex.Message}");
                return CreateResult("", false);
            }
        }

        private string ProcessFilter(XElement filter, string tableAlias)
        {
            var filterType = filter.Attribute("type")?.Value ?? "and";
            _debugLog.Add($"Processing filter with type: {filterType}");

            var conditions = filter.Elements("condition").ToList();
            var nestedFilters = filter.Elements("filter").ToList();

            var clauses = new List<string>();

            // Process conditions
            foreach (var condition in conditions)
            {
                var clause = ProcessCondition(condition, tableAlias);
                if (!string.IsNullOrWhiteSpace(clause))
                {
                    clauses.Add(clause);
                }
            }

            // Process nested filters
            foreach (var nestedFilter in nestedFilters)
            {
                var clause = ProcessFilter(nestedFilter, tableAlias);
                if (!string.IsNullOrWhiteSpace(clause))
                {
                    clauses.Add($"({clause})");
                }
            }

            if (!clauses.Any())
                return "";

            var separator = filterType.Equals("or", StringComparison.OrdinalIgnoreCase) ? " OR " : " AND ";
            return string.Join(separator, clauses);
        }

        private string ProcessCondition(XElement condition, string tableAlias)
        {
            var attribute = condition.Attribute("attribute")?.Value;
            var operatorValue = condition.Attribute("operator")?.Value;
            var value = condition.Attribute("value")?.Value;

            if (string.IsNullOrWhiteSpace(attribute) || string.IsNullOrWhiteSpace(operatorValue))
            {
                _debugLog.Add("Condition missing attribute or operator - skipping");
                return "";
            }

            _debugLog.Add($"  Condition: {attribute} {operatorValue} {value ?? "(no value)"}");

            var columnRef = $"{tableAlias}.{attribute}";
            var safeValue = value ?? "";
            var operatorKey = operatorValue!;

            try
            {
                return operatorKey.ToLowerInvariant() switch
                {
                    // Basic comparison operators
                    "eq" => $"{columnRef} = {FormatValue(safeValue)}",
                    "ne" => $"{columnRef} <> {FormatValue(safeValue)}",
                    "gt" => $"{columnRef} > {FormatValue(safeValue)}",
                    "ge" => $"{columnRef} >= {FormatValue(safeValue)}",
                    "lt" => $"{columnRef} < {FormatValue(safeValue)}",
                    "le" => $"{columnRef} <= {FormatValue(safeValue)}",
                    
                    // Null operators
                    "null" => $"{columnRef} IS NULL",
                    "not-null" => $"{columnRef} IS NOT NULL",
                    
                    // String operators
                    "like" => $"{columnRef} LIKE {FormatValue(safeValue)}",
                    "not-like" => $"{columnRef} NOT LIKE {FormatValue(safeValue)}",
                    "begins-with" => $"{columnRef} LIKE {FormatValue(safeValue + "%")}",
                    "not-begin-with" => $"{columnRef} NOT LIKE {FormatValue(safeValue + "%")}",
                    "ends-with" => $"{columnRef} LIKE {FormatValue("%" + safeValue)}",
                    "not-end-with" => $"{columnRef} NOT LIKE {FormatValue("%" + safeValue)}",
                    
                    // Date operators - absolute
                    "today" => ConvertDateOperator(columnRef, "today"),
                    "yesterday" => ConvertDateOperator(columnRef, "yesterday"),
                    "tomorrow" => ConvertDateOperator(columnRef, "tomorrow"),
                    "this-week" => ConvertDateOperator(columnRef, "this-week"),
                    "last-week" => ConvertDateOperator(columnRef, "last-week"),
                    "this-month" => ConvertDateOperator(columnRef, "this-month"),
                    "last-month" => ConvertDateOperator(columnRef, "last-month"),
                    "this-year" => ConvertDateOperator(columnRef, "this-year"),
                    "last-year" => ConvertDateOperator(columnRef, "last-year"),
                    "next-week" => ConvertDateOperator(columnRef, "next-week"),
                    "next-month" => ConvertDateOperator(columnRef, "next-month"),
                    "next-year" => ConvertDateOperator(columnRef, "next-year"),
                    
                    // Date operators - relative with value parameter
                    "last-x-hours" => ConvertRelativeDateOperator(columnRef, "hour", safeValue, -1),
                    "last-x-days" => ConvertRelativeDateOperator(columnRef, "day", safeValue, -1),
                    "last-x-weeks" => ConvertRelativeDateOperator(columnRef, "week", safeValue, -1),
                    "last-x-months" => ConvertRelativeDateOperator(columnRef, "month", safeValue, -1),
                    "last-x-years" => ConvertRelativeDateOperator(columnRef, "year", safeValue, -1),
                    "next-x-hours" => ConvertRelativeDateOperator(columnRef, "hour", safeValue, 1),
                    "next-x-days" => ConvertRelativeDateOperator(columnRef, "day", safeValue, 1),
                    "next-x-weeks" => ConvertRelativeDateOperator(columnRef, "week", safeValue, 1),
                    "next-x-months" => ConvertRelativeDateOperator(columnRef, "month", safeValue, 1),
                    "next-x-years" => ConvertRelativeDateOperator(columnRef, "year", safeValue, 1),
                    "older-x-months" => ConvertOlderThanOperator(columnRef, "month", safeValue),
                    "older-x-years" => ConvertOlderThanOperator(columnRef, "year", safeValue),
                    
                    // Date comparison operators (with timezone adjustment)
                    "on" => $"CAST(DATEADD(hour, {_utcOffsetHours}, {columnRef}) AS DATE) = CAST({FormatValue(safeValue)} AS DATE)",
                    "on-or-after" => $"DATEADD(hour, {_utcOffsetHours}, {columnRef}) >= {FormatValue(safeValue)}",
                    "on-or-before" => $"DATEADD(hour, {_utcOffsetHours}, {columnRef}) <= {FormatValue(safeValue)}",
                    
                    // User context operators (not supported in FabricLink or Import mode)
                    "eq-userid" => (_isFabricLink || _isImportMode) ? UnsupportedUserContextOp("eq-userid", attribute) : $"{columnRef} = CURRENT_USER",
                    "ne-userid" => (_isFabricLink || _isImportMode) ? UnsupportedUserContextOp("ne-userid", attribute) : $"{columnRef} <> CURRENT_USER",
                    "eq-userteams" => (_isFabricLink || _isImportMode) ? UnsupportedUserContextOp("eq-userteams", attribute) : ConvertUserTeamsOperator(columnRef, true),
                    "ne-userteams" => (_isFabricLink || _isImportMode) ? UnsupportedUserContextOp("ne-userteams", attribute) : ConvertUserTeamsOperator(columnRef, false),
                    
                    // List operators
                    "in" => ProcessInOperator(condition, columnRef),
                    "not-in" => ProcessNotInOperator(condition, columnRef),
                    
                    // Unsupported operators that we log
                    _ => UnsupportedOperator(operatorKey, attribute, safeValue)
                };
            }
            catch (Exception ex)
            {
                _debugLog.Add($"  ERROR processing condition: {ex.Message}");
                LogUnsupported($"Failed to process operator '{operatorKey}' for attribute '{attribute}'");
                return "";
            }
        }

        private string ConvertDateOperator(string columnRef, string dateOperator)
        {
            // Convert FetchXML date operators to SQL equivalents
            // Using GETUTCDATE() with timezone adjustment for current date/time
            var adjustedNow = $"DATEADD(hour, {_utcOffsetHours}, GETUTCDATE())";
            var adjustedColumn = $"DATEADD(hour, {_utcOffsetHours}, {columnRef})";
            
            return dateOperator switch
            {
                "today" => $"CAST({adjustedColumn} AS DATE) = CAST({adjustedNow} AS DATE)",
                "yesterday" => $"CAST({adjustedColumn} AS DATE) = CAST(DATEADD(day, -1, {adjustedNow}) AS DATE)",
                "tomorrow" => $"CAST({adjustedColumn} AS DATE) = CAST(DATEADD(day, 1, {adjustedNow}) AS DATE)",
                
                "this-week" => $"DATEPART(week, {adjustedColumn}) = DATEPART(week, {adjustedNow}) AND DATEPART(year, {adjustedColumn}) = DATEPART(year, {adjustedNow})",
                "last-week" => $"DATEPART(week, {adjustedColumn}) = DATEPART(week, DATEADD(week, -1, {adjustedNow})) AND DATEPART(year, {adjustedColumn}) = DATEPART(year, DATEADD(week, -1, {adjustedNow}))",
                "next-week" => $"DATEPART(week, {adjustedColumn}) = DATEPART(week, DATEADD(week, 1, {adjustedNow})) AND DATEPART(year, {adjustedColumn}) = DATEPART(year, DATEADD(week, 1, {adjustedNow}))",
                
                "this-month" => $"DATEPART(month, {adjustedColumn}) = DATEPART(month, {adjustedNow}) AND DATEPART(year, {adjustedColumn}) = DATEPART(year, {adjustedNow})",
                "last-month" => $"DATEPART(month, {adjustedColumn}) = DATEPART(month, DATEADD(month, -1, {adjustedNow})) AND DATEPART(year, {adjustedColumn}) = DATEPART(year, DATEADD(month, -1, {adjustedNow}))",
                "next-month" => $"DATEPART(month, {adjustedColumn}) = DATEPART(month, DATEADD(month, 1, {adjustedNow})) AND DATEPART(year, {adjustedColumn}) = DATEPART(year, DATEADD(month, 1, {adjustedNow}))",
                
                "this-year" => $"DATEPART(year, {adjustedColumn}) = DATEPART(year, {adjustedNow})",
                "last-year" => $"DATEPART(year, {adjustedColumn}) = DATEPART(year, DATEADD(year, -1, {adjustedNow}))",
                "next-year" => $"DATEPART(year, {adjustedColumn}) = DATEPART(year, DATEADD(year, 1, {adjustedNow}))",
                
                _ => UnsupportedOperator(dateOperator, columnRef, null)
            };
        }

        private string ConvertRelativeDateOperator(string columnRef, string datepart, string? value, int direction)
        {
            // direction: -1 for "last", 1 for "next"
            if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out int units))
            {
                LogUnsupported($"Invalid value '{value ?? ""}' for relative date operator");
                return "";
            }

            // Apply timezone adjustment to both column and current time
            var adjustedColumn = $"DATEADD(hour, {_utcOffsetHours}, {columnRef})";
            var adjustedNow = $"DATEADD(hour, {_utcOffsetHours}, GETUTCDATE())";
            
            // Create range queries with both lower and upper bounds
            // Using DATEDIFF to get period count from epoch, then DATEADD to get boundary dates
            
            if (direction == -1) // last-x
            {
                // Example: last-4-months means >= start of (current-4) AND < start of (current+1)
                // Lower bound: DATEDIFF gives current period count, subtract units, DATEADD converts back to date
                var lowerBound = $"DATEADD({datepart}, DATEDIFF({datepart}, 0, {adjustedNow}) - {units}, 0)";
                // Upper bound: start of next period (current + 1)
                var upperBound = $"DATEADD({datepart}, DATEDIFF({datepart}, 0, {adjustedNow}) + 1, 0)";
                return $"({adjustedColumn} >= {lowerBound} AND {adjustedColumn} < {upperBound})";
            }
            else // next-x
            {
                // Example: next-4-months means >= start of (current+1) AND < start of (current+x+1)
                // Lower bound: start of next period
                var lowerBound = $"DATEADD({datepart}, DATEDIFF({datepart}, 0, {adjustedNow}) + 1, 0)";
                // Upper bound: start of x+1 periods from now
                var upperBound = $"DATEADD({datepart}, DATEDIFF({datepart}, 0, {adjustedNow}) + {units + 1}, 0)";
                return $"({adjustedColumn} >= {lowerBound} AND {adjustedColumn} < {upperBound})";
            }
        }

        private string ConvertOlderThanOperator(string columnRef, string datepart, string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out int units))
            {
                LogUnsupported($"Invalid value '{value ?? ""}' for older-than operator");
                return "";
            }

            // Apply timezone adjustment
            var adjustedColumn = $"DATEADD(hour, {_utcOffsetHours}, {columnRef})";
            var adjustedNow = $"DATEADD(hour, {_utcOffsetHours}, GETUTCDATE())";
            
            // older-x-months: < start of x months ago
            var threshold = $"DATEADD({datepart}, DATEDIFF({datepart}, 0, {adjustedNow}) - {units}, 0)";
            return $"{adjustedColumn} < {threshold}";
        }

        private string ConvertUserTeamsOperator(string columnRef, bool isEqual)
        {
            // User teams require checking if the value is in the user's teams
            var comparison = isEqual ? "IN" : "NOT IN";
            var userTeamsQuery = $"SELECT TeamId FROM TeamMembership WHERE SystemUserId = CURRENT_USER";
            LogUnsupported($"User teams operator - may require TeamMembership table access");
            return $"{columnRef} {comparison} ({userTeamsQuery})";
        }

        private string ProcessInOperator(XElement condition, string columnRef)
        {
            var values = condition.Elements("value").Select(v => v.Value).ToList();
            if (!values.Any())
            {
                var singleValue = condition.Attribute("value")?.Value;
                if (!string.IsNullOrWhiteSpace(singleValue))
                {
                    var safeSingleValue = singleValue!;
                    values = safeSingleValue.Split(',').Select(v => v.Trim()).ToList();
                }
            }

            if (!values.Any())
            {
                _debugLog.Add("  IN operator has no values - skipping");
                return "";
            }

            values = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList();
            var formattedValues = string.Join(", ", values.Select(FormatValue));
            return $"{columnRef} IN ({formattedValues})";
        }

        private string ProcessNotInOperator(XElement condition, string columnRef)
        {
            var inClause = ProcessInOperator(condition, columnRef);
            if (string.IsNullOrWhiteSpace(inClause))
                return "";
            
            return inClause.Replace(" IN (", " NOT IN (");
        }

        private string FormatValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NULL";

            var safeValue = value!;

            // Try to detect value type and format accordingly
            
            // Integer
            if (int.TryParse(safeValue, out _))
                return safeValue;
            
            // Boolean (Dataverse uses 0/1)
            if (safeValue == "0" || safeValue == "1")
                return safeValue;
            
            // Guid
            if (Guid.TryParse(safeValue, out _))
                return $"'{safeValue}'";
            
            // DateTime (basic ISO format detection)
            if (DateTime.TryParse(safeValue, out _))
                return $"'{safeValue}'";
            
            // Default: treat as string and escape single quotes
            var escapedValue = safeValue.Replace("'", "''");
            return $"'{escapedValue}'";
        }

        private string UnsupportedOperator(string operatorValue, string? attribute, string? value)
        {
            var message = $"Operator '{operatorValue}' for attribute '{attribute ?? ""}'";
            LogUnsupported(message);
            _debugLog.Add($"  UNSUPPORTED: {message}");
            return "";
        }

        private string UnsupportedInFabricLink(string operatorValue, string? attribute)
        {
            var message = $"Operator '{operatorValue}' for attribute '{attribute ?? ""}' - not supported in FabricLink (use TDS for current user filters)";
            LogUnsupported(message);
            _debugLog.Add($"  UNSUPPORTED IN FABRICLINK: {message}");
            return "";
        }

        private string UnsupportedUserContextOp(string operatorValue, string? attribute)
        {
            var reason = _isImportMode ? "Import mode" : "FabricLink";
            var message = $"Operator '{operatorValue}' for attribute '{attribute ?? ""}' - not supported in {reason} (current user filters require DirectQuery)";
            LogUnsupported(message);
            _debugLog.Add($"  UNSUPPORTED ({reason.ToUpperInvariant()}): {message}");
            return "";
        }

        private List<string> ProcessLinkEntityFilters(XElement linkEntity, string baseTableAlias)
        {
            var clauses = new List<string>();
            var linkEntityName = linkEntity.Attribute("name")?.Value ?? "unknown";
            var alias = linkEntity.Attribute("alias")?.Value ?? linkEntityName;
            var linkType = linkEntity.Attribute("link-type")?.Value ?? "inner";
            var fromAttr = linkEntity.Attribute("from")?.Value;
            var toAttr = linkEntity.Attribute("to")?.Value;
            
            _debugLog.Add($"  Link-entity: {linkEntityName} (alias: {alias}, type: {linkType})");
            _debugLog.Add($"    Join: {baseTableAlias}.{toAttr} = {alias}.{fromAttr}");
            
            // Process filters within this link-entity
            var linkFilters = linkEntity.Elements("filter").ToList();
            if (linkFilters.Any())
            {
                _debugLog.Add($"    Processing {linkFilters.Count} filter(s) in link-entity");
                
                // For link-entity filters, we need to express them as subquery EXISTS conditions
                // since DirectQuery SQL doesn't support JOINs in the partition query
                foreach (var filter in linkFilters)
                {
                    var filterClause = ProcessFilter(filter, alias);
                    if (!string.IsNullOrWhiteSpace(filterClause))
                    {
                        // Create an EXISTS subquery for the link-entity filter
                        var existsClause = $"EXISTS (SELECT 1 FROM {linkEntityName} AS {alias} WHERE {alias}.{fromAttr} = {baseTableAlias}.{toAttr} AND ({filterClause}))";
                        clauses.Add(existsClause);
                        _debugLog.Add($"    Generated EXISTS clause: {existsClause}");
                    }
                }
            }
            
            // Process nested link-entities recursively
            var nestedLinkEntities = linkEntity.Elements("link-entity").ToList();
            if (nestedLinkEntities.Any())
            {
                _debugLog.Add($"    Found {nestedLinkEntities.Count} nested link-entity elements");
                foreach (var nested in nestedLinkEntities)
                {
                    var nestedClauses = ProcessLinkEntityFilters(nested, alias);
                    clauses.AddRange(nestedClauses);
                }
            }
            
            return clauses;
        }

        private void LogUnsupported(string feature)
        {
            _hasUnsupportedFeatures = true;
            if (!_unsupportedFeatures.Contains(feature))
            {
                _unsupportedFeatures.Add(feature);
            }
        }

        private ConversionResult CreateResult(string sqlClause, bool isFullySupported)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"FetchXML Conversion Summary:");
            summary.AppendLine($"  Fully Supported: {isFullySupported}");
            
            if (_unsupportedFeatures.Any())
            {
                summary.AppendLine($"  Unsupported Features ({_unsupportedFeatures.Count}):");
                foreach (var feature in _unsupportedFeatures)
                {
                    summary.AppendLine($"    - {feature}");
                }
            }

            if (!string.IsNullOrWhiteSpace(sqlClause))
            {
                summary.AppendLine($"  Generated SQL: {sqlClause}");
            }
            else
            {
                summary.AppendLine($"  No SQL generated");
            }

            return new ConversionResult
            {
                SqlWhereClause = sqlClause,
                IsFullySupported = isFullySupported,
                UnsupportedFeatures = new List<string>(_unsupportedFeatures),
                DebugLog = new List<string>(_debugLog),
                Summary = summary.ToString()
            };
        }

        /// <summary>
        /// Logs detailed debugging information to a file
        /// </summary>
        public static void LogConversionDebug(string viewName, string fetchXml, ConversionResult result, string outputPath)
        {
            try
            {
                var debugFolder = Path.Combine(outputPath, "FetchXML_Debug");
                Directory.CreateDirectory(debugFolder);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{SanitizeFileName(viewName)}_{timestamp}.txt";
                var filePath = Path.Combine(debugFolder, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine($"FetchXML to SQL Conversion Debug Log");
                sb.AppendLine($"View: {viewName}");
                sb.AppendLine($"Timestamp: {DateTime.Now}");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();

                sb.AppendLine("INPUT FetchXML:");
                sb.AppendLine("-".PadRight(80, '-'));
                sb.AppendLine(FormatXml(fetchXml));
                sb.AppendLine();

                sb.AppendLine("CONVERSION RESULT:");
                sb.AppendLine("-".PadRight(80, '-'));
                sb.AppendLine(result.Summary);
                sb.AppendLine();

                if (result.DebugLog.Any())
                {
                    sb.AppendLine("DEBUG LOG:");
                    sb.AppendLine("-".PadRight(80, '-'));
                    foreach (var log in result.DebugLog)
                    {
                        sb.AppendLine(log);
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("=".PadRight(80, '='));

                File.WriteAllText(filePath, sb.ToString());
                DebugLogger.Log($"FetchXML conversion debug saved to: {filePath}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to save FetchXML debug log: {ex.Message}");
            }
        }

        private static string FormatXml(string xml)
        {
            try
            {
                var doc = ParseXmlSecurely(xml);
                return doc.ToString();
            }
            catch
            {
                return xml;
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}
