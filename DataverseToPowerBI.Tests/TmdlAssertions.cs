// =============================================================================
// TmdlAssertions.cs - TMDL and PBIP Output Assertion Helpers
// =============================================================================
// Purpose: Provides structured assertion helpers for validating generated
// PBIP folder structure and TMDL file content. Parses TMDL line-by-line
// to extract tables, columns, partitions, measures, and relationships
// for easy assertion in integration tests.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DataverseToPowerBI.Tests
{
    /// <summary>
    /// Assertion helpers for validating PBIP/TMDL output from SemanticModelBuilder.
    /// </summary>
    public static class TmdlAssertions
    {
        #region PBIP Structure Assertions

        /// <summary>
        /// Asserts that the PBIP folder has the expected structure for a given project name.
        /// </summary>
        public static void AssertPbipStructure(string outputFolder, string projectName)
        {
            var pbipFile = FindPbipFile(outputFolder);
            Assert.True(pbipFile != null, $"No .pbip file found under {outputFolder}");

            var projectDir = Path.GetDirectoryName(pbipFile)!;
            var smFolder = FindSemanticModelFolder(projectDir);
            Assert.True(smFolder != null, $"No .SemanticModel folder found under {projectDir}");

            var definitionDir = Path.Combine(smFolder!, "definition");
            Assert.True(Directory.Exists(definitionDir), $"Missing definition/ folder at {definitionDir}");

            var tablesDir = Path.Combine(definitionDir, "tables");
            Assert.True(Directory.Exists(tablesDir), $"Missing definition/tables/ folder at {tablesDir}");

            // Core files
            AssertFileExists(definitionDir, "model.tmdl");
            AssertFileExists(smFolder!, "definition.pbism");
        }

        /// <summary>
        /// Asserts that a specific table TMDL file exists.
        /// </summary>
        public static void AssertTableFileExists(string outputFolder, string tableName)
        {
            var tablesDir = FindTablesDir(outputFolder);
            Assert.True(tablesDir != null, "Could not find tables directory");
            var tablePath = Path.Combine(tablesDir!, tableName + ".tmdl");
            Assert.True(File.Exists(tablePath), $"Table file not found: {tablePath}");
        }

        /// <summary>
        /// Asserts that a specific table TMDL file does NOT exist.
        /// </summary>
        public static void AssertTableFileDoesNotExist(string outputFolder, string tableName)
        {
            var tablesDir = FindTablesDir(outputFolder);
            if (tablesDir == null) return; // No tables dir = no table file
            var tablePath = Path.Combine(tablesDir, tableName + ".tmdl");
            Assert.False(File.Exists(tablePath), $"Table file should not exist: {tablePath}");
        }

        /// <summary>
        /// Asserts that relationships.tmdl exists.
        /// </summary>
        public static void AssertRelationshipsFileExists(string outputFolder)
        {
            var defDir = FindDefinitionDir(outputFolder);
            Assert.True(defDir != null, "Could not find definition directory");
            AssertFileExists(defDir!, "relationships.tmdl");
        }

        /// <summary>
        /// Asserts that expressions.tmdl exists (FabricLink mode).
        /// </summary>
        public static void AssertExpressionsFileExists(string outputFolder)
        {
            var defDir = FindDefinitionDir(outputFolder);
            Assert.True(defDir != null, "Could not find definition directory");
            AssertFileExists(defDir!, "expressions.tmdl");
        }

        /// <summary>
        /// Asserts that expressions.tmdl does NOT exist (DataverseTDS mode).
        /// </summary>
        public static void AssertExpressionsFileDoesNotExist(string outputFolder)
        {
            var defDir = FindDefinitionDir(outputFolder);
            if (defDir == null) return;
            var path = Path.Combine(defDir, "expressions.tmdl");
            Assert.False(File.Exists(path), "expressions.tmdl should not exist in DataverseTDS mode");
        }

        /// <summary>
        /// Asserts that diagramLayout.json exists at the SemanticModel root.
        /// </summary>
        public static void AssertDiagramLayoutExists(string outputFolder)
        {
            var smFolder = FindSemanticModelFolderFromOutput(outputFolder);
            Assert.True(smFolder != null, "Could not find SemanticModel folder");
            var path = Path.Combine(smFolder!, "diagramLayout.json");
            Assert.True(File.Exists(path), $"diagramLayout.json not found at {path}");
        }

        #endregion

        #region TMDL Content Parsing

        /// <summary>
        /// Reads a table TMDL file and returns its content as a string.
        /// </summary>
        public static string ReadTableTmdl(string outputFolder, string tableName)
        {
            var tablesDir = FindTablesDir(outputFolder);
            Assert.True(tablesDir != null, "Could not find tables directory");
            var path = Path.Combine(tablesDir!, tableName + ".tmdl");
            Assert.True(File.Exists(path), $"Table file not found: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Reads model.tmdl content.
        /// </summary>
        public static string ReadModelTmdl(string outputFolder)
        {
            var defDir = FindDefinitionDir(outputFolder);
            Assert.True(defDir != null, "Could not find definition directory");
            var path = Path.Combine(defDir!, "model.tmdl");
            Assert.True(File.Exists(path), $"model.tmdl not found at {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Reads relationships.tmdl content.
        /// </summary>
        public static string ReadRelationshipsTmdl(string outputFolder)
        {
            var defDir = FindDefinitionDir(outputFolder);
            Assert.True(defDir != null, "Could not find definition directory");
            var path = Path.Combine(defDir!, "relationships.tmdl");
            Assert.True(File.Exists(path), $"relationships.tmdl not found at {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Extracts column names from a table TMDL file.
        /// Looks for lines matching: [whitespace]column [Name|'Name']
        /// </summary>
        public static List<string> ExtractColumnNames(string tmdlContent)
        {
            var columns = new List<string>();
            foreach (var line in tmdlContent.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("column ") || trimmed.StartsWith("column\t"))
                {
                    var name = ExtractTmdlName(trimmed, "column");
                    if (name != null) columns.Add(name);
                }
            }
            return columns;
        }

        /// <summary>
        /// Extracts measure names from a table TMDL file.
        /// </summary>
        public static List<string> ExtractMeasureNames(string tmdlContent)
        {
            var measures = new List<string>();
            foreach (var line in tmdlContent.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("measure ") || trimmed.StartsWith("measure\t"))
                {
                    var name = ExtractTmdlName(trimmed, "measure");
                    if (name != null) measures.Add(name);
                }
            }
            return measures;
        }

        /// <summary>
        /// Extracts the partition expression (M query) from a table TMDL file.
        /// Returns the content between "expression =\r\n" and the next top-level block.
        /// </summary>
        public static string? ExtractPartitionExpression(string tmdlContent)
        {
            var lines = tmdlContent.Split('\n');
            var capturing = false;
            var expressionLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r');

                if (trimmed.Trim().StartsWith("expression =") || trimmed.Trim() == "expression")
                {
                    capturing = true;
                    // If expression = ... on same line, capture remainder
                    var eqIdx = trimmed.IndexOf('=');
                    if (eqIdx >= 0 && eqIdx + 1 < trimmed.Length)
                    {
                        var remainder = trimmed.Substring(eqIdx + 1).Trim();
                        if (!string.IsNullOrEmpty(remainder))
                            expressionLines.Add(remainder);
                    }
                    continue;
                }

                if (capturing)
                {
                    // Stop at next top-level keyword (not indented continuation)
                    if (!string.IsNullOrWhiteSpace(trimmed) &&
                        !trimmed.StartsWith("\t") && !trimmed.StartsWith("  ") &&
                        !trimmed.StartsWith("```"))
                    {
                        break;
                    }
                    expressionLines.Add(trimmed);
                }
            }

            return expressionLines.Count > 0 ? string.Join("\n", expressionLines).Trim() : null;
        }

        /// <summary>
        /// Extracts table references from model.tmdl (lines matching "ref table [Name]").
        /// </summary>
        public static List<string> ExtractModelTableRefs(string modelTmdlContent)
        {
            var refs = new List<string>();
            foreach (var line in modelTmdlContent.Split('\n'))
            {
                var trimmed = line.Trim().TrimEnd('\r');
                if (trimmed.StartsWith("ref table "))
                {
                    var name = trimmed.Substring("ref table ".Length).Trim();
                    // Remove quotes if present
                    name = name.Trim('\'');
                    refs.Add(name);
                }
            }
            return refs;
        }

        /// <summary>
        /// Extracts expression references from model.tmdl (lines matching "ref expression [Name]").
        /// </summary>
        public static List<string> ExtractModelExpressionRefs(string modelTmdlContent)
        {
            var refs = new List<string>();
            foreach (var line in modelTmdlContent.Split('\n'))
            {
                var trimmed = line.Trim().TrimEnd('\r');
                if (trimmed.StartsWith("ref expression "))
                {
                    var name = trimmed.Substring("ref expression ".Length).Trim();
                    name = name.Trim('\'');
                    refs.Add(name);
                }
            }
            return refs;
        }

        #endregion

        #region Content Assertions

        /// <summary>
        /// Asserts that a table TMDL contains a column with the given name.
        /// </summary>
        public static void AssertTableHasColumn(string outputFolder, string tableName, string columnName)
        {
            var content = ReadTableTmdl(outputFolder, tableName);
            var columns = ExtractColumnNames(content);
            Assert.Contains(columnName, columns);
        }

        /// <summary>
        /// Asserts that a table TMDL contains specific text.
        /// </summary>
        public static void AssertTableContains(string outputFolder, string tableName, string expectedText)
        {
            var content = ReadTableTmdl(outputFolder, tableName);
            Assert.Contains(expectedText, content);
        }

        /// <summary>
        /// Asserts that a table TMDL does NOT contain specific text.
        /// </summary>
        public static void AssertTableDoesNotContain(string outputFolder, string tableName, string unexpectedText)
        {
            var content = ReadTableTmdl(outputFolder, tableName);
            Assert.DoesNotContain(unexpectedText, content);
        }

        /// <summary>
        /// Asserts that model.tmdl references the given table name.
        /// </summary>
        public static void AssertModelReferencesTable(string outputFolder, string tableName)
        {
            var content = ReadModelTmdl(outputFolder);
            var refs = ExtractModelTableRefs(content);
            Assert.Contains(tableName, refs);
        }

        /// <summary>
        /// Asserts that model.tmdl references the given expression name.
        /// </summary>
        public static void AssertModelReferencesExpression(string outputFolder, string expressionName)
        {
            var content = ReadModelTmdl(outputFolder);
            var refs = ExtractModelExpressionRefs(content);
            Assert.Contains(expressionName, refs);
        }

        /// <summary>
        /// Asserts that the partition expression for a table contains the expected SQL fragment.
        /// </summary>
        public static void AssertPartitionContainsSql(string outputFolder, string tableName, string sqlFragment)
        {
            var content = ReadTableTmdl(outputFolder, tableName);
            var expr = ExtractPartitionExpression(content);
            Assert.NotNull(expr);
            Assert.Contains(sqlFragment, expr);
        }

        /// <summary>
        /// Asserts that a table has the specified storage mode.
        /// </summary>
        public static void AssertTableStorageMode(string outputFolder, string tableName, string expectedMode)
        {
            var content = ReadTableTmdl(outputFolder, tableName);
            Assert.Contains($"mode: {expectedMode}", content, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region File System Helpers

        private static string? FindPbipFile(string outputFolder)
        {
            // Recursively search for .pbip file
            try
            {
                var files = Directory.GetFiles(outputFolder, "*.pbip", SearchOption.AllDirectories);
                return files.FirstOrDefault();
            }
            catch { return null; }
        }

        private static string? FindSemanticModelFolder(string projectDir)
        {
            try
            {
                return Directory.GetDirectories(projectDir, "*.SemanticModel", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        private static string? FindSemanticModelFolderFromOutput(string outputFolder)
        {
            try
            {
                return Directory.GetDirectories(outputFolder, "*.SemanticModel", SearchOption.AllDirectories)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        private static string? FindDefinitionDir(string outputFolder)
        {
            var smFolder = FindSemanticModelFolderFromOutput(outputFolder);
            if (smFolder == null) return null;
            var defDir = Path.Combine(smFolder, "definition");
            return Directory.Exists(defDir) ? defDir : null;
        }

        private static string? FindTablesDir(string outputFolder)
        {
            var defDir = FindDefinitionDir(outputFolder);
            if (defDir == null) return null;
            var tablesDir = Path.Combine(defDir, "tables");
            return Directory.Exists(tablesDir) ? tablesDir : null;
        }

        private static void AssertFileExists(string directory, string fileName)
        {
            var path = Path.Combine(directory, fileName);
            Assert.True(File.Exists(path), $"Expected file not found: {path}");
        }

        /// <summary>
        /// Extracts a TMDL object name from a declaration line.
        /// Handles both unquoted (column Name) and quoted (column 'My Name') forms.
        /// </summary>
        private static string? ExtractTmdlName(string line, string keyword)
        {
            var rest = line.Substring(keyword.Length).Trim();
            if (rest.StartsWith("'"))
            {
                // Quoted name — find closing quote
                var endQuote = rest.IndexOf('\'', 1);
                return endQuote > 0 ? rest.Substring(1, endQuote - 1) : null;
            }
            else
            {
                // Unquoted — take until whitespace or end
                var space = rest.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
                return space > 0 ? rest.Substring(0, space) : rest;
            }
        }

        #endregion
    }
}
