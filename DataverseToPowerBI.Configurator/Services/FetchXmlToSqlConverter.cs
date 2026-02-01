// =============================================================================
// FetchXmlToSqlConverter.cs - FetchXML to SQL Converter
// =============================================================================
// Purpose: Converts Dataverse FetchXML filter conditions to SQL WHERE clauses.
//
// FetchXML is the proprietary query language used by Dataverse. When users
// select a view for a table, the view's FetchXML filter needs to be converted
// to SQL so it can be included in the Power BI DirectQuery partition.
//
// Supported Operators:
//   Basic Comparison: eq, ne, gt, ge, lt, le
//   Null Checks: null, not-null
//   String Matching: like, not-like, begins-with, ends-with
//   Date Absolute: today, yesterday, this-week, this-month, this-year, etc.
//   Date Relative: last-x-days, next-x-months, older-x-years, etc.
//   Date Comparison: on, on-or-after, on-or-before
//   List Operators: in, not-in
//   User Context: eq-userid, ne-userid
//
// Timezone Handling:
//   Dataverse stores all DateTime values in UTC. This converter applies
//   timezone adjustment using DATEADD(hour, offset, column) to convert
//   UTC times to the user's local timezone before comparison.
//
// Limitations:
//   - Some complex operators may not be fully supported
//   - Link-entity filters are converted to EXISTS subqueries
//   - Unsupported operators are logged for manual review
//
// Usage:
//   var converter = new FetchXmlToSqlConverter(utcOffsetHours: -5);
//   var result = converter.ConvertToWhereClause(fetchXml, "Base");
//   if (result.IsFullySupported)
//       // Use result.SqlWhereClause in your query
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DataverseToPowerBI.Configurator.Services
{
    /// <summary>
    /// Converts FetchXML filter conditions to SQL WHERE clauses for Power BI DirectQuery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FetchXML is Dataverse's proprietary query language. When generating Power BI
    /// semantic models, view filters expressed in FetchXML must be converted to SQL
    /// for use in DirectQuery partitions.
    /// </para>
    /// <para>
    /// The converter handles:
    /// </para>
    /// <list type="bullet">
    ///   <item>Basic comparisons (eq, ne, gt, lt, etc.)</item>
    ///   <item>Date operators with timezone adjustment</item>
    ///   <item>String pattern matching (like, begins-with, etc.)</item>
    ///   <item>Nested filter groups with AND/OR logic</item>
    ///   <item>Link-entity filters as EXISTS subqueries</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var converter = new FetchXmlToSqlConverter(-5); // EST timezone
    /// var result = converter.ConvertToWhereClause(fetchXml, "Base");
    /// 
    /// if (result.IsFullySupported)
    ///     Console.WriteLine($"WHERE {result.SqlWhereClause}");
    /// else
    ///     Console.WriteLine($"Partial: {string.Join(", ", result.UnsupportedFeatures)}");
    /// </code>
    /// </example>
    public class FetchXmlToSqlConverter
    {
        #region Private Fields

        /// <summary>
        /// Debug log entries captured during conversion for troubleshooting.
        /// </summary>
        private readonly List<string> _debugLog = new();

        /// <summary>
        /// Features encountered that could not be converted to SQL.
        /// </summary>
        private readonly List<string> _unsupportedFeatures = new();

        /// <summary>
        /// Flag indicating whether any unsupported features were encountered.
        /// </summary>
        private bool _hasUnsupportedFeatures = false;

        /// <summary>
        /// UTC offset in hours for timezone adjustment.
        /// Negative for timezones west of UTC (Americas), positive for east.
        /// </summary>
        private readonly int _utcOffsetHours;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FetchXmlToSqlConverter class.
        /// </summary>
        /// <param name="utcOffsetHours">
        /// UTC offset in hours for timezone adjustment.
        /// Use -5 for EST, -8 for PST, 0 for UTC, +1 for CET, etc.
        /// Default is -6 (Central Time).
        /// </param>
        public FetchXmlToSqlConverter(int utcOffsetHours = -6)
        {
            _utcOffsetHours = utcOffsetHours;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Result of a FetchXML to SQL conversion operation.
        /// Contains the generated SQL and diagnostic information.
        /// </summary>
        public class ConversionResult
        {
            /// <summary>
            /// The generated SQL WHERE clause (without the "WHERE" keyword).
            /// Empty string if no conditions could be converted.
            /// </summary>
            public string SqlWhereClause { get; set; } = "";

            /// <summary>
            /// Whether all FetchXML conditions were successfully converted.
            /// False if any operators or features were not supported.
            /// </summary>
            public bool IsFullySupported { get; set; } = true;

            /// <summary>
            /// List of features that could not be converted to SQL.
            /// Useful for warning users about potential filter gaps.
            /// </summary>
            public List<string> UnsupportedFeatures { get; set; } = new();

            /// <summary>
            /// Detailed conversion debug log for troubleshooting.
            /// </summary>
            public List<string> DebugLog { get; set; } = new();

            /// <summary>
            /// Human-readable summary of the conversion result.
            /// </summary>
            public string Summary { get; set; } = "";
        }

        #endregion

        #region Main Conversion Method

        /// <summary>
        /// Converts FetchXML to a SQL WHERE clause.
        /// </summary>
        /// <param name="fetchXml">The FetchXML query to convert.</param>
        /// <param name="tableAlias">
        /// SQL table alias to use in column references (default: "Base").
        /// </param>
        /// <returns>
        /// A ConversionResult containing the SQL WHERE clause and diagnostic info.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The conversion process:
        /// </para>
        /// <list type="number">
        ///   <item>Parse the FetchXML document</item>
        ///   <item>Extract filter elements from the main entity</item>
        ///   <item>Process each condition and nested filter</item>
        ///   <item>Handle link-entity filters as EXISTS subqueries</item>
        ///   <item>Combine all clauses with appropriate AND/OR logic</item>
        /// </list>
        /// </remarks>
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

                var doc = XDocument.Parse(fetchXml);
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

        #endregion

        #region Filter Processing Methods

        /// <summary>
        /// Processes a filter element and returns the SQL clause.
        /// </summary>
        /// <param name="filter">The FetchXML filter element.</param>
        /// <param name="tableAlias">SQL table alias for column references.</param>
        /// <returns>SQL clause combining all conditions with AND/OR logic.</returns>
        /// <remarks>
        /// FetchXML filters contain conditions and can be nested. Each filter has a type
        /// attribute (and/or) that determines how child conditions are combined.
        /// </remarks>
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

            // Process nested filters (recursive)
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

        /// <summary>
        /// Processes a single condition element and returns the SQL expression.
        /// </summary>
        /// <param name="condition">The FetchXML condition element.</param>
        /// <param name="tableAlias">SQL table alias for column references.</param>
        /// <returns>SQL expression for this condition, or empty string if unsupported.</returns>
        /// <remarks>
        /// This method handles all supported FetchXML operators including comparisons,
        /// string matching, date operators (both absolute and relative), and list operators.
        /// </remarks>
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

            try
            {
                return operatorValue.ToLowerInvariant() switch
                {
                    // Basic comparison operators
                    "eq" => $"{columnRef} = {FormatValue(value)}",
                    "ne" => $"{columnRef} <> {FormatValue(value)}",
                    "gt" => $"{columnRef} > {FormatValue(value)}",
                    "ge" => $"{columnRef} >= {FormatValue(value)}",
                    "lt" => $"{columnRef} < {FormatValue(value)}",
                    "le" => $"{columnRef} <= {FormatValue(value)}",
                    
                    // Null operators
                    "null" => $"{columnRef} IS NULL",
                    "not-null" => $"{columnRef} IS NOT NULL",
                    
                    // String operators
                    "like" => $"{columnRef} LIKE {FormatValue(value)}",
                    "not-like" => $"{columnRef} NOT LIKE {FormatValue(value)}",
                    "begins-with" => $"{columnRef} LIKE {FormatValue(value + "%")}",
                    "not-begin-with" => $"{columnRef} NOT LIKE {FormatValue(value + "%")}",
                    "ends-with" => $"{columnRef} LIKE {FormatValue("%" + value)}",
                    "not-end-with" => $"{columnRef} NOT LIKE {FormatValue("%" + value)}",
                    
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
                    "last-x-hours" => ConvertRelativeDateOperator(columnRef, "hour", value, -1),
                    "last-x-days" => ConvertRelativeDateOperator(columnRef, "day", value, -1),
                    "last-x-weeks" => ConvertRelativeDateOperator(columnRef, "week", value, -1),
                    "last-x-months" => ConvertRelativeDateOperator(columnRef, "month", value, -1),
                    "last-x-years" => ConvertRelativeDateOperator(columnRef, "year", value, -1),
                    "next-x-hours" => ConvertRelativeDateOperator(columnRef, "hour", value, 1),
                    "next-x-days" => ConvertRelativeDateOperator(columnRef, "day", value, 1),
                    "next-x-weeks" => ConvertRelativeDateOperator(columnRef, "week", value, 1),
                    "next-x-months" => ConvertRelativeDateOperator(columnRef, "month", value, 1),
                    "next-x-years" => ConvertRelativeDateOperator(columnRef, "year", value, 1),
                    "older-x-months" => ConvertOlderThanOperator(columnRef, "month", value),
                    "older-x-years" => ConvertOlderThanOperator(columnRef, "year", value),
                    
                    // Date comparison operators (with timezone adjustment)
                    "on" => $"CAST(DATEADD(hour, {_utcOffsetHours}, {columnRef}) AS DATE) = CAST({FormatValue(value)} AS DATE)",
                    "on-or-after" => $"DATEADD(hour, {_utcOffsetHours}, {columnRef}) >= {FormatValue(value)}",
                    "on-or-before" => $"DATEADD(hour, {_utcOffsetHours}, {columnRef}) <= {FormatValue(value)}",
                    
                    // User context operators
                    "eq-userid" => $"{columnRef} = CURRENT_USER",
                    "ne-userid" => $"{columnRef} <> CURRENT_USER",
                    "eq-userteams" => ConvertUserTeamsOperator(columnRef, true),
                    "ne-userteams" => ConvertUserTeamsOperator(columnRef, false),
                    
                    // List operators
                    "in" => ProcessInOperator(condition, columnRef),
                    "not-in" => ProcessNotInOperator(condition, columnRef),
                    
                    // Unsupported operators that we log
                    _ => UnsupportedOperator(operatorValue, attribute, value)
                };
            }
            catch (Exception ex)
            {
                _debugLog.Add($"  ERROR processing condition: {ex.Message}");
                LogUnsupported($"Failed to process operator '{operatorValue}' for attribute '{attribute}'");
                return "";
            }
        }

        #endregion

        #region Date Operator Conversion Methods

        /// <summary>
        /// Converts absolute date operators (today, this-week, this-month, etc.) to SQL.
        /// </summary>
        /// <param name="columnRef">Fully qualified column reference (alias.column).</param>
        /// <param name="dateOperator">The FetchXML date operator name.</param>
        /// <returns>SQL expression that evaluates the date condition.</returns>
        /// <remarks>
        /// All date comparisons apply timezone adjustment to convert UTC stored values
        /// to local time before comparison. The adjustment uses DATEADD with the
        /// _utcOffsetHours value provided at construction time.
        /// </remarks>
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

        /// <summary>
        /// Converts relative date operators (last-x-days, next-x-months, etc.) to SQL.
        /// </summary>
        /// <param name="columnRef">Fully qualified column reference (alias.column).</param>
        /// <param name="datepart">SQL DATEPART value (day, week, month, year).</param>
        /// <param name="value">Number of units (e.g., "7" for last-7-days).</param>
        /// <param name="direction">-1 for "last" (past), 1 for "next" (future).</param>
        /// <returns>SQL expression with range bounds for the relative period.</returns>
        /// <remarks>
        /// Relative date operators create range queries. For example, "last-4-months"
        /// generates a condition that matches dates from the start of 4 months ago
        /// up to (but not including) the start of next month.
        /// </remarks>
        private string ConvertRelativeDateOperator(string columnRef, string datepart, string value, int direction)
        {
            // direction: -1 for "last", 1 for "next"
            if (!int.TryParse(value, out int units))
            {
                LogUnsupported($"Invalid value '{value}' for relative date operator");
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

        /// <summary>
        /// Converts "older-x-months/years" operators to SQL.
        /// </summary>
        /// <param name="columnRef">Fully qualified column reference.</param>
        /// <param name="datepart">SQL DATEPART value (month, year).</param>
        /// <param name="value">Number of units threshold.</param>
        /// <returns>SQL expression matching dates older than the threshold.</returns>
        private string ConvertOlderThanOperator(string columnRef, string datepart, string value)
        {
            if (!int.TryParse(value, out int units))
            {
                LogUnsupported($"Invalid value '{value}' for older-than operator");
                return "";
            }

            // Apply timezone adjustment
            var adjustedColumn = $"DATEADD(hour, {_utcOffsetHours}, {columnRef})";
            var adjustedNow = $"DATEADD(hour, {_utcOffsetHours}, GETUTCDATE())";
            
            // older-x-months: < start of x months ago
            var threshold = $"DATEADD({datepart}, DATEDIFF({datepart}, {units}, {adjustedNow}), 0)";
            return $"{adjustedColumn} < {threshold}";
        }

        #endregion

        #region User Context and List Operators

        /// <summary>
        /// Converts user team membership operators to SQL subquery.
        /// </summary>
        /// <param name="columnRef">Fully qualified column reference.</param>
        /// <param name="isEqual">True for eq-userteams, false for ne-userteams.</param>
        /// <returns>SQL expression with subquery checking team membership.</returns>
        /// <remarks>
        /// User team operators require access to the TeamMembership table which may
        /// not be available in the DirectQuery context. This is logged as a partially
        /// supported feature.
        /// </remarks>
        private string ConvertUserTeamsOperator(string columnRef, bool isEqual)
        {
            // User teams require checking if the value is in the user's teams
            var comparison = isEqual ? "IN" : "NOT IN";
            var userTeamsQuery = $"SELECT TeamId FROM TeamMembership WHERE SystemUserId = CURRENT_USER";
            LogUnsupported($"User teams operator - may require TeamMembership table access");
            return $"{columnRef} {comparison} ({userTeamsQuery})";
        }

        /// <summary>
        /// Processes the IN operator for list membership checks.
        /// </summary>
        /// <param name="condition">The FetchXML condition element containing values.</param>
        /// <param name="columnRef">Fully qualified column reference.</param>
        /// <returns>SQL IN expression, or empty string if no values found.</returns>
        /// <remarks>
        /// Values can be specified either as child &lt;value&gt; elements or as a
        /// comma-separated list in the value attribute.
        /// </remarks>
        private string ProcessInOperator(XElement condition, string columnRef)
        {
            var values = condition.Elements("value").Select(v => v.Value).ToList();
            if (!values.Any())
            {
                var singleValue = condition.Attribute("value")?.Value;
                if (!string.IsNullOrWhiteSpace(singleValue))
                {
                    values = singleValue.Split(',').Select(v => v.Trim()).ToList();
                }
            }

            if (!values.Any())
            {
                _debugLog.Add("  IN operator has no values - skipping");
                return "";
            }

            var formattedValues = string.Join(", ", values.Select(FormatValue));
            return $"{columnRef} IN ({formattedValues})";
        }

        /// <summary>
        /// Processes the NOT IN operator (inverse of IN).
        /// </summary>
        /// <param name="condition">The FetchXML condition element.</param>
        /// <param name="columnRef">Fully qualified column reference.</param>
        /// <returns>SQL NOT IN expression.</returns>
        private string ProcessNotInOperator(XElement condition, string columnRef)
        {
            var inClause = ProcessInOperator(condition, columnRef);
            if (string.IsNullOrWhiteSpace(inClause))
                return "";
            
            return inClause.Replace(" IN (", " NOT IN (");
        }

        #endregion

        #region Value Formatting and Helpers

        /// <summary>
        /// Formats a value for use in SQL based on its detected type.
        /// </summary>
        /// <param name="value">The raw value string from FetchXML.</param>
        /// <returns>
        /// SQL-formatted value: unquoted for numbers, quoted for strings and dates.
        /// </returns>
        /// <remarks>
        /// <para>Type detection order:</para>
        /// <list type="number">
        ///   <item>Integer: returned as-is</item>
        ///   <item>Boolean (0/1): returned as-is</item>
        ///   <item>GUID: quoted with single quotes</item>
        ///   <item>DateTime: quoted with single quotes</item>
        ///   <item>String: quoted with escaped single quotes</item>
        /// </list>
        /// </remarks>
        private string FormatValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NULL";

            // Try to detect value type and format accordingly
            
            // Integer
            if (int.TryParse(value, out _))
                return value;
            
            // Boolean (Dataverse uses 0/1)
            if (value == "0" || value == "1")
                return value;
            
            // Guid
            if (Guid.TryParse(value, out _))
                return $"'{value}'";
            
            // DateTime (basic ISO format detection)
            if (DateTime.TryParse(value, out _))
                return $"'{value}'";
            
            // Default: treat as string and escape single quotes
            var escapedValue = value.Replace("'", "''");
            return $"'{escapedValue}'";
        }

        /// <summary>
        /// Records an unsupported operator and returns empty string.
        /// </summary>
        /// <param name="operatorValue">The unsupported FetchXML operator.</param>
        /// <param name="attribute">The attribute being filtered.</param>
        /// <param name="value">The filter value (if any).</param>
        /// <returns>Empty string (unsupported conditions are skipped).</returns>
        private string UnsupportedOperator(string operatorValue, string attribute, string value)
        {
            var message = $"Operator '{operatorValue}' for attribute '{attribute}'";
            LogUnsupported(message);
            _debugLog.Add($"  UNSUPPORTED: {message}");
            return "";
        }

        #endregion

        #region Link-Entity Processing

        /// <summary>
        /// Processes link-entity elements and generates EXISTS subqueries.
        /// </summary>
        /// <param name="linkEntity">The FetchXML link-entity element.</param>
        /// <param name="baseTableAlias">Alias of the parent table.</param>
        /// <returns>List of SQL EXISTS clauses for the link-entity filters.</returns>
        /// <remarks>
        /// <para>
        /// Link-entities represent JOIN relationships in FetchXML. Since Power BI
        /// DirectQuery partitions cannot include JOINs directly, filters on linked
        /// entities are converted to EXISTS subqueries.
        /// </para>
        /// <para>
        /// For example, a filter on a related account would become:
        /// EXISTS (SELECT 1 FROM account WHERE account.accountid = Base.accountid AND ...)
        /// </para>
        /// </remarks>
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

        #endregion

        #region Result Building and Logging

        /// <summary>
        /// Records an unsupported feature for inclusion in the result.
        /// </summary>
        /// <param name="feature">Description of the unsupported feature.</param>
        private void LogUnsupported(string feature)
        {
            _hasUnsupportedFeatures = true;
            if (!_unsupportedFeatures.Contains(feature))
            {
                _unsupportedFeatures.Add(feature);
            }
        }

        /// <summary>
        /// Creates a ConversionResult with current state.
        /// </summary>
        /// <param name="sqlClause">The generated SQL WHERE clause.</param>
        /// <param name="isFullySupported">Whether all features were supported.</param>
        /// <returns>A complete ConversionResult with summary and debug info.</returns>
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
        /// Logs detailed debugging information to a file for troubleshooting.
        /// </summary>
        /// <param name="viewName">Name of the view being converted.</param>
        /// <param name="fetchXml">The original FetchXML query.</param>
        /// <param name="result">The conversion result with debug info.</param>
        /// <param name="outputPath">Base output path for debug files.</param>
        /// <remarks>
        /// Creates a FetchXML_Debug subfolder and writes a timestamped file
        /// containing the original FetchXML, conversion summary, and debug log.
        /// Useful for diagnosing conversion issues.
        /// </remarks>
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

        /// <summary>
        /// Formats XML for readable output in debug logs.
        /// </summary>
        /// <param name="xml">Raw XML string.</param>
        /// <returns>Pretty-printed XML, or original string if parsing fails.</returns>
        private static string FormatXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                return doc.ToString();
            }
            catch
            {
                return xml;
            }
        }

        /// <summary>
        /// Removes invalid filename characters from a string.
        /// </summary>
        /// <param name="fileName">Original filename.</param>
        /// <returns>Sanitized filename safe for file system use.</returns>
        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        #endregion
    }
}
