using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataverseToPowerBI.XrmToolBox;
using DataverseToPowerBI.XrmToolBox.Services;
using Xunit;

namespace DataverseToPowerBI.Tests
{
    public class SemanticModelBuilderTests : IDisposable
    {
        private readonly SemanticModelBuilder _builder;
        private readonly string _fixturesPath;
        private readonly string _tempDir;

        public SemanticModelBuilderTests()
        {
            _fixturesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            _tempDir = Path.Combine(Path.GetTempPath(), "DataverseToPowerBI_Tests_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempDir);
            _builder = new SemanticModelBuilder(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string FixturePath(string name) => Path.Combine(_fixturesPath, name);

        #region ParseExistingLineageTags Tests

        [Fact]
        public void ParseExistingLineageTags_ExtractsTableTag()
        {
            var tags = _builder.ParseExistingLineageTags(FixturePath("SampleTable.tmdl"));
            Assert.True(tags.ContainsKey("table"));
            Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", tags["table"]);
        }

        [Fact]
        public void ParseExistingLineageTags_ExtractsColumnTagsBySourceColumn()
        {
            var tags = _builder.ParseExistingLineageTags(FixturePath("SampleTable.tmdl"));
            Assert.Equal("11111111-2222-3333-4444-555555555555", tags["col:accountid"]);
            Assert.Equal("22222222-3333-4444-5555-666666666666", tags["col:name"]);
            Assert.Equal("33333333-4444-5555-6666-777777777777", tags["col:revenue"]);
            Assert.Equal("44444444-5555-6666-7777-888888888888", tags["col:createdon"]);
        }

        [Fact]
        public void ParseExistingLineageTags_ExtractsMeasureTags()
        {
            var tags = _builder.ParseExistingLineageTags(FixturePath("SampleTable.tmdl"));
            Assert.Equal("55555555-6666-7777-8888-999999999999", tags["measure:Custom Measure"]);
            Assert.Equal("66666666-7777-8888-9999-aaaaaaaaaaaa", tags["measure:Link to Account"]);
            Assert.Equal("77777777-8888-9999-aaaa-bbbbbbbbbbbb", tags["measure:Account Count"]);
        }

        [Fact]
        public void ParseExistingLineageTags_ReturnsEmptyForMissingFile()
        {
            var tags = _builder.ParseExistingLineageTags(Path.Combine(_tempDir, "nonexistent.tmdl"));
            Assert.Empty(tags);
        }

        [Fact]
        public void ParseExistingLineageTags_ExtractsExpressionTags()
        {
            var tags = _builder.ParseExistingLineageTags(FixturePath("SampleExpressions.tmdl"));
            Assert.Equal("expr-1111-2222-3333-444444444444", tags["expr:FabricSQLEndpoint"]);
            Assert.Equal("expr-2222-3333-4444-555555555555", tags["expr:FabricLakehouse"]);
        }

        [Fact]
        public void ParseExistingLineageTags_DataverseURL_ExtractsTableAndColumnTags()
        {
            var tags = _builder.ParseExistingLineageTags(FixturePath("SampleDataverseURL.tmdl"));
            Assert.Equal("dvurl-1111-2222-3333-444444444444", tags["table"]);
            Assert.Equal("dvurl-2222-3333-4444-555555555555", tags["col:DataverseURL"]);
        }

        #endregion

        #region ParseExistingColumnMetadata Tests

        [Fact]
        public void ParseExistingColumnMetadata_ExtractsDescriptions()
        {
            var cols = _builder.ParseExistingColumnMetadata(FixturePath("SampleTable.tmdl"));
            Assert.Equal("The primary name of the account", cols["name"].Description);
            Assert.Equal("User-edited description here", cols["createdon"].Description);
        }

        [Fact]
        public void ParseExistingColumnMetadata_ExtractsFormatStrings()
        {
            var cols = _builder.ParseExistingColumnMetadata(FixturePath("SampleTable.tmdl"));
            Assert.Contains("$#,0.00", cols["revenue"].FormatString);
            Assert.Equal("Short Date", cols["createdon"].FormatString);
        }

        [Fact]
        public void ParseExistingColumnMetadata_ExtractsSummarizeBy()
        {
            var cols = _builder.ParseExistingColumnMetadata(FixturePath("SampleTable.tmdl"));
            Assert.Equal("sum", cols["revenue"].SummarizeBy);
            Assert.Equal("none", cols["name"].SummarizeBy);
        }

        [Fact]
        public void ParseExistingColumnMetadata_ExtractsDataType()
        {
            var cols = _builder.ParseExistingColumnMetadata(FixturePath("SampleTable.tmdl"));
            Assert.Equal("string", cols["name"].DataType);
            Assert.Equal("decimal", cols["revenue"].DataType);
            Assert.Equal("dateTime", cols["createdon"].DataType);
        }

        [Fact]
        public void ParseExistingColumnMetadata_ExtractsUserAnnotations()
        {
            var cols = _builder.ParseExistingColumnMetadata(FixturePath("SampleTable.tmdl"));
            Assert.True(cols["name"].Annotations.ContainsKey("UserCustomAnnotation"));
            Assert.Equal("MyValue", cols["name"].Annotations["UserCustomAnnotation"]);
        }

        [Fact]
        public void ParseExistingColumnMetadata_IncludesToolAnnotations()
        {
            var cols = _builder.ParseExistingColumnMetadata(FixturePath("SampleTable.tmdl"));
            Assert.True(cols["name"].Annotations.ContainsKey("SummarizationSetBy"));
        }

        [Fact]
        public void ParseExistingColumnMetadata_ReturnsEmptyForMissingFile()
        {
            var cols = _builder.ParseExistingColumnMetadata(Path.Combine(_tempDir, "nonexistent.tmdl"));
            Assert.Empty(cols);
        }

        #endregion

        #region ParseExistingRelationshipGuids Tests

        [Fact]
        public void ParseExistingRelationshipGuids_MapsKeysToGuids()
        {
            var guids = _builder.ParseExistingRelationshipGuids(FixturePath("SampleRelationships.tmdl"));
            Assert.Equal("aaaaaaaa-1111-2222-3333-444444444444", guids["Account.parentaccountid→Account.accountid"]);
            Assert.Equal("bbbbbbbb-1111-2222-3333-444444444444", guids["'Opportunity'.accountid→Account.accountid"]);
        }

        [Fact]
        public void ParseExistingRelationshipGuids_IncludesUserAddedRelationships()
        {
            var guids = _builder.ParseExistingRelationshipGuids(FixturePath("SampleRelationships.tmdl"));
            Assert.Equal("cccccccc-1111-2222-3333-444444444444", guids["Account.'Custom Field'→'Custom Table'.'Custom Id'"]);
        }

        [Fact]
        public void ParseExistingRelationshipGuids_IncludesDateRelationship()
        {
            var guids = _builder.ParseExistingRelationshipGuids(FixturePath("SampleRelationships.tmdl"));
            Assert.Equal("dddddddd-1111-2222-3333-444444444444", guids["Account.createdon→Date.Date"]);
        }

        #endregion

        #region ParseExistingRelationshipBlocks Tests

        [Fact]
        public void ParseExistingRelationshipBlocks_ExtractsFullBlocks()
        {
            var blocks = _builder.ParseExistingRelationshipBlocks(FixturePath("SampleRelationships.tmdl"));
            Assert.Equal(4, blocks.Count);
        }

        [Fact]
        public void ParseExistingRelationshipBlocks_BlockContainsProperties()
        {
            var blocks = _builder.ParseExistingRelationshipBlocks(FixturePath("SampleRelationships.tmdl"));
            var selfRefBlock = blocks["Account.parentaccountid→Account.accountid"];
            Assert.Contains("relyOnReferentialIntegrity", selfRefBlock);
        }

        [Fact]
        public void ParseExistingRelationshipBlocks_BlockContainsInactiveFlag()
        {
            var blocks = _builder.ParseExistingRelationshipBlocks(FixturePath("SampleRelationships.tmdl"));
            var inactiveBlock = blocks["'Opportunity'.accountid→Account.accountid"];
            Assert.Contains("isActive: false", inactiveBlock);
        }

        #endregion

        #region ExtractUserRelationships Tests

        [Fact]
        public void ExtractUserRelationships_IdentifiesUserAddedRelationships()
        {
            var blocks = _builder.ParseExistingRelationshipBlocks(FixturePath("SampleRelationships.tmdl"));
            
            // Simulate tool generating only the first two relationships
            var toolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Account.parentaccountid→Account.accountid",
                "'Opportunity'.accountid→Account.accountid",
                "Account.createdon→Date.Date"
            };

            var userRels = _builder.ExtractUserRelationships(blocks, toolKeys);
            Assert.NotNull(userRels);
            Assert.Contains("Custom Field", userRels);
            Assert.Contains("Custom Table", userRels);
        }

        [Fact]
        public void ExtractUserRelationships_ReturnsNullWhenAllToolManaged()
        {
            var blocks = _builder.ParseExistingRelationshipBlocks(FixturePath("SampleRelationships.tmdl"));
            
            // All keys are tool-managed
            var toolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Account.parentaccountid→Account.accountid",
                "'Opportunity'.accountid→Account.accountid",
                "Account.'Custom Field'→'Custom Table'.'Custom Id'",
                "Account.createdon→Date.Date"
            };

            var userRels = _builder.ExtractUserRelationships(blocks, toolKeys);
            Assert.Null(userRels);
        }

        [Fact]
        public void ExtractUserRelationships_AddsMarkerComment()
        {
            var blocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["A.b→C.d"] = "relationship some-guid\r\n\tfromColumn: A.b\r\n\ttoColumn: C.d\r\n"
            };
            var toolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var result = _builder.ExtractUserRelationships(blocks, toolKeys);
            Assert.NotNull(result);
            Assert.Contains("/// User-added relationship", result);
        }

        [Fact]
        public void ExtractUserRelationships_DoesNotDuplicateMarker()
        {
            var blocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["A.b→C.d"] = "/// User-added relationship (preserved by DataverseToPowerBI)\r\nrelationship some-guid\r\n\tfromColumn: A.b\r\n\ttoColumn: C.d\r\n"
            };
            var toolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var result = _builder.ExtractUserRelationships(blocks, toolKeys);
            // Count occurrences of marker
            var count = result!.Split(new[] { "/// User-added relationship" }, StringSplitOptions.None).Length - 1;
            Assert.Equal(1, count);
        }

        [Fact]
        public void ExtractUserRelationships_SkipsRelationshipsWithMissingColumnReferences()
        {
            var blocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Account.accountid→Contact.parentcustomerid"] = "relationship keep-guid\r\n\tfromColumn: Account.accountid\r\n\ttoColumn: Contact.parentcustomerid\r\n",
                ["DeletedTable.deletedid→Account.accountid"] = "relationship stale-guid\r\n\tfromColumn: DeletedTable.deletedid\r\n\ttoColumn: Account.accountid\r\n"
            };

            var toolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Account|accountid",
                "Contact|parentcustomerid"
            };

            var result = _builder.ExtractUserRelationships(blocks, toolKeys, validRefs);

            Assert.NotNull(result);
            Assert.Contains("keep-guid", result);
            Assert.DoesNotContain("stale-guid", result);
        }

        [Fact]
        public void FilterInvalidRelationshipBlocks_RemovesBlocksWithMissingToColumn()
        {
            var tmdl =
                "relationship valid-guid\r\n" +
                "\tfromColumn: Account.accountid\r\n" +
                "\ttoColumn: Contact.parentcustomerid\r\n\r\n" +
                "relationship invalid-guid\r\n" +
                "\tfromColumn: Account.accountid\r\n" +
                "\ttoColumn: DeletedTable.deletedid\r\n\r\n";

            var validRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Account|accountid",
                "Contact|parentcustomerid"
            };

            var result = _builder.FilterInvalidRelationshipBlocks(tmdl, validRefs);

            Assert.Contains("valid-guid", result);
            Assert.DoesNotContain("invalid-guid", result);
        }

        [Fact]
        public void RepairRelationshipsFile_RemovesInvalidRelationshipBlocks()
        {
            var projectName = "TestModel";
            var pbipFolder = Path.Combine(_tempDir, "env", projectName);
            var definitionFolder = Path.Combine(pbipFolder, $"{projectName}.SemanticModel", "definition");
            var tablesFolder = Path.Combine(definitionFolder, "tables");
            Directory.CreateDirectory(tablesFolder);

            File.WriteAllText(Path.Combine(tablesFolder, "Account.tmdl"),
                "table Account\r\n\tcolumn accountid\r\n");
            File.WriteAllText(Path.Combine(tablesFolder, "Contact.tmdl"),
                "table Contact\r\n\tcolumn parentcustomerid\r\n");

            var relationshipsPath = Path.Combine(definitionFolder, "relationships.tmdl");
            File.WriteAllText(relationshipsPath,
                "relationship valid-guid\r\n" +
                "\tfromColumn: Account.accountid\r\n" +
                "\ttoColumn: Contact.parentcustomerid\r\n\r\n" +
                "relationship invalid-guid\r\n" +
                "\tfromColumn: Account.accountid\r\n" +
                "\ttoColumn: MissingTable.missingid\r\n\r\n");

            var removed = _builder.RepairRelationshipsFile(pbipFolder, projectName);
            var repaired = File.ReadAllText(relationshipsPath);

            Assert.Equal(1, removed);
            Assert.Contains("valid-guid", repaired);
            Assert.DoesNotContain("invalid-guid", repaired);
        }

        #endregion

        #region GetOrNewLineageTag Tests

        [Fact]
        public void GetOrNewLineageTag_ReturnsExistingTag()
        {
            var tags = new Dictionary<string, string> { ["col:name"] = "existing-tag" };
            Assert.Equal("existing-tag", _builder.GetOrNewLineageTag(tags, "col:name"));
        }

        [Fact]
        public void GetOrNewLineageTag_GeneratesNewGuidWhenMissing()
        {
            var tags = new Dictionary<string, string>();
            var result = _builder.GetOrNewLineageTag(tags, "col:newcolumn");
            Assert.True(Guid.TryParse(result, out _));
        }

        [Fact]
        public void GetOrNewLineageTag_GeneratesNewGuidWhenNull()
        {
            var result = _builder.GetOrNewLineageTag(null, "col:any");
            Assert.True(Guid.TryParse(result, out _));
        }

        #endregion

        #region ExtractUserMeasuresSection Tests

        [Fact]
        public void ExtractUserMeasuresSection_ExtractsUserMeasure()
        {
            var table = new ExportTable
            {
                LogicalName = "account",
                DisplayName = "Account"
            };

            var result = _builder.ExtractUserMeasuresSection(FixturePath("SampleTable.tmdl"), table);
            Assert.NotNull(result);
            Assert.Contains("Custom Measure", result);
        }

        [Fact]
        public void ExtractUserMeasuresSection_ExcludesAutoGeneratedMeasures()
        {
            var table = new ExportTable
            {
                LogicalName = "account",
                DisplayName = "Account"
            };

            var result = _builder.ExtractUserMeasuresSection(FixturePath("SampleTable.tmdl"), table);
            Assert.DoesNotContain("Link to Account", result ?? "");
            Assert.DoesNotContain("Account Count", result ?? "");
        }

        [Fact]
        public void ExtractUserMeasuresSection_ReturnsNullForMissingFile()
        {
            var result = _builder.ExtractUserMeasuresSection(Path.Combine(_tempDir, "nonexistent.tmdl"));
            Assert.Null(result);
        }

        #endregion

        #region InsertUserMeasures Tests

        [Fact]
        public void InsertUserMeasures_InsertsBeforePartition()
        {
            var tmdl = "table Test\r\n\tcolumn Col1\r\n\tpartition Test = m\r\n\t\tmode: directQuery\r\n";
            var measures = "\tmeasure 'My Measure' = 42\r\n\r\n";

            var result = _builder.InsertUserMeasures(tmdl, measures);
            var partitionIndex = result.IndexOf("partition");
            var measureIndex = result.IndexOf("My Measure");
            Assert.True(measureIndex < partitionIndex, "Measure should appear before partition");
        }

        [Fact]
        public void InsertUserMeasures_InsertsBeforeAnnotationWhenNoPartition()
        {
            var tmdl = "table Test\r\n\tcolumn Col1\r\n\tannotation Key = Value\r\n";
            var measures = "\tmeasure 'My Measure' = 42\r\n\r\n";

            var result = _builder.InsertUserMeasures(tmdl, measures);
            var annotationIndex = result.IndexOf("annotation");
            var measureIndex = result.IndexOf("My Measure");
            Assert.True(measureIndex < annotationIndex, "Measure should appear before annotation");
        }

        #endregion

        #region GenerateDataverseUrlTableTmdl Tests

        [Fact]
        public void GenerateDataverseUrlTableTmdl_PreservesExistingTags()
        {
            var existingTags = new Dictionary<string, string>
            {
                ["table"] = "preserved-table-tag",
                ["col:DataverseURL"] = "preserved-col-tag"
            };

            var result = _builder.GenerateDataverseUrlTableTmdl("myorg.crm.dynamics.com", existingTags);
            Assert.Contains("preserved-table-tag", result);
            Assert.Contains("preserved-col-tag", result);
        }

        [Fact]
        public void GenerateDataverseUrlTableTmdl_GeneratesNewTagsWithoutExisting()
        {
            var result = _builder.GenerateDataverseUrlTableTmdl("myorg.crm.dynamics.com");
            Assert.Contains("lineageTag:", result);
            // Should have 2 lineageTags (table + column)
            var count = result.Split(new[] { "lineageTag:" }, StringSplitOptions.None).Length - 1;
            Assert.Equal(2, count);
        }

        [Fact]
        public void GenerateDataverseUrlTableTmdl_ContainsUrl()
        {
            var result = _builder.GenerateDataverseUrlTableTmdl("myorg.crm.dynamics.com");
            Assert.Contains("myorg.crm.dynamics.com", result);
        }

        #endregion

        #region GenerateFabricLinkExpressions Tests

        [Fact]
        public void GenerateFabricLinkExpressions_PreservesExistingTags()
        {
            var existingTags = new Dictionary<string, string>
            {
                ["expr:FabricSQLEndpoint"] = "preserved-endpoint-tag",
                ["expr:FabricLakehouse"] = "preserved-lakehouse-tag"
            };

            var result = _builder.GenerateFabricLinkExpressions("endpoint", "database", existingTags);
            Assert.Contains("preserved-endpoint-tag", result);
            Assert.Contains("preserved-lakehouse-tag", result);
        }

        [Fact]
        public void GenerateFabricLinkExpressions_GeneratesNewTagsWithoutExisting()
        {
            var result = _builder.GenerateFabricLinkExpressions("endpoint", "database");
            var count = result.Split(new[] { "lineageTag:" }, StringSplitOptions.None).Length - 1;
            Assert.Equal(2, count);
        }

        #endregion

        #region Round-Trip Preservation Tests

        [Fact]
        public void RoundTrip_LineageTagsStableAcrossRegeneration()
        {
            // Parse tags from sample, use them to generate, then parse again — tags should match
            var originalTags = _builder.ParseExistingLineageTags(FixturePath("SampleDataverseURL.tmdl"));
            var generated = _builder.GenerateDataverseUrlTableTmdl("newurl.crm.dynamics.com", originalTags);

            var tempFile = Path.Combine(_tempDir, "roundtrip.tmdl");
            File.WriteAllText(tempFile, generated);

            var regeneratedTags = _builder.ParseExistingLineageTags(tempFile);
            Assert.Equal(originalTags["table"], regeneratedTags["table"]);
            Assert.Equal(originalTags["col:DataverseURL"], regeneratedTags["col:DataverseURL"]);
        }

        [Fact]
        public void RoundTrip_RelationshipGuidsStableAcrossRegeneration()
        {
            var originalGuids = _builder.ParseExistingRelationshipGuids(FixturePath("SampleRelationships.tmdl"));
            Assert.True(originalGuids.Count >= 4);

            // Each GUID should be unique
            var uniqueGuids = new HashSet<string>(originalGuids.Values);
            Assert.Equal(originalGuids.Count, uniqueGuids.Count);
        }

        #endregion

        #region Date Relationship Dedup Tests

        [Fact]
        public void ExtractUserRelationships_SkipsStaleDateRelationships()
        {
            // A relationship ending in →Date.Date that is NOT in toolKeys should be skipped (not preserved)
            var blocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Account.createdon→Date.Date"] = "relationship some-guid\r\n\tfromColumn: createdon\r\n\ttoColumn: Date\r\n",
                ["A.customfield→B.id"] = "relationship other-guid\r\n\tfromColumn: customfield\r\n\ttoColumn: id\r\n"
            };
            var toolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // empty = nothing is tool-managed

            var result = _builder.ExtractUserRelationships(blocks, toolKeys);
            Assert.NotNull(result);
            // The date relationship should be skipped
            Assert.DoesNotContain("createdon", result);
            // The non-date relationship should be preserved
            Assert.Contains("customfield", result);
        }

        [Fact]
        public void ExtractUserRelationships_PreservesDateRelationshipWhenInToolKeys()
        {
            // A date relationship that IS in toolKeys should not appear in user rels (it's tool-managed)
            var blocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Account.createdon→Date.Date"] = "relationship some-guid\r\n\tfromColumn: createdon\r\n\ttoColumn: Date\r\n"
            };
            var toolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Account.createdon→Date.Date"
            };

            var result = _builder.ExtractUserRelationships(blocks, toolKeys);
            Assert.Null(result);
        }

        #endregion

        #region Table Rename Detection Tests

        [Fact]
        public void TableRenameDetection_ParsesSourceCommentFromTmdl()
        {
            // Create a TMDL file with a /// Source: comment
            var dir = Path.Combine(_tempDir, "tables");
            Directory.CreateDirectory(dir);
            var oldFile = Path.Combine(dir, "Old Name.tmdl");
            File.WriteAllText(oldFile,
                "/// Source: account\r\ntable 'Old Name'\r\n\tlineageTag: abc-123\r\n\tcolumn Name\r\n");

            // Verify we can read the source comment
            var firstLines = File.ReadLines(oldFile).Take(3).ToList();
            var sourceComment = firstLines.FirstOrDefault(l => l.StartsWith("/// Source:"));
            Assert.NotNull(sourceComment);
            var logicalName = sourceComment.Substring("/// Source:".Length).Trim();
            Assert.Equal("account", logicalName);
        }

        [Fact]
        public void TableRenameDetection_LineageTagsCarriedFromOldFile()
        {
            // Create an old TMDL file that would exist under a previous display name
            var dir = Path.Combine(_tempDir, "tables");
            Directory.CreateDirectory(dir);
            var oldFile = Path.Combine(dir, "Old Account Name.tmdl");
            File.WriteAllText(oldFile,
                "/// Source: account\r\ntable 'Old Account Name'\r\n\tlineageTag: preserved-tag-from-old\r\n" +
                "\tcolumn accountid\r\n\t\tdataType: string\r\n\t\tlineageTag: col-tag-from-old\r\n\t\tsourceColumn: accountid\r\n");

            // The builder should be able to parse tags from the old file
            var tags = _builder.ParseExistingLineageTags(oldFile);
            Assert.True(tags.ContainsKey("table"));
            Assert.Equal("preserved-tag-from-old", tags["table"]);
            Assert.True(tags.ContainsKey("col:accountid"));
            Assert.Equal("col-tag-from-old", tags["col:accountid"]);
        }

        #endregion

        #region FabricLink Multi-Select SQL Tests

        [Fact]
        public void GenerateTableTmdl_FabricLinkMultiSelect_UsesSemicolonSplitAndAttributeOptionSetName()
        {
            var fabricBuilder = new SemanticModelBuilder(
                _tempDir,
                connectionType: "FabricLink",
                fabricLinkEndpoint: "test-endpoint",
                fabricLinkDatabase: "test-db",
                languageCode: 1033,
                useDisplayNameAliasesInSql: false);

            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "pbi_categories",
                        DisplayName = "Categories",
                        SchemaName = "pbi_categories",
                        AttributeType = "MultiSelectPicklist",
                        IsGlobal = true,
                        OptionSetName = "connectionrole_category"
                    }
                }
            };

            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pbi_categories"] = new AttributeDisplayInfo
                    {
                        LogicalName = "pbi_categories",
                        DisplayName = "Categories",
                        AttributeType = "MultiSelectPicklist",
                        IsGlobal = true,
                        OptionSetName = "connectionrole_category"
                    }
                }
            };

            var tmdl = fabricBuilder.GenerateTableTmdl(
                table,
                attributeDisplayInfo,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("STRING_SPLIT(CAST(Base.pbi_categories AS VARCHAR(4000)), ';') AS split", tmdl);
            Assert.DoesNotContain("STRING_SPLIT(CAST(Base.pbi_categories AS VARCHAR(4000)), ',') AS split", tmdl);
            Assert.Contains("meta_pbi_categories.[OptionSetName] = 'pbi_categories'", tmdl);
            Assert.DoesNotContain("meta_pbi_categories.[OptionSetName] = 'connectionrole_category'", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_FabricLinkPicklist_WhenIsGlobalUnknown_InfersGlobalFromOptionSetName()
        {
            var fabricBuilder = new SemanticModelBuilder(
                _tempDir,
                connectionType: "FabricLink",
                fabricLinkEndpoint: "test-endpoint",
                fabricLinkDatabase: "test-db",
                languageCode: 1033,
                useDisplayNameAliasesInSql: false);

            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "prioritycode",
                        DisplayName = "Priority",
                        SchemaName = "prioritycode",
                        AttributeType = "Picklist",
                        OptionSetName = "incident_priority"
                    }
                }
            };

            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["prioritycode"] = new AttributeDisplayInfo
                    {
                        LogicalName = "prioritycode",
                        DisplayName = "Priority",
                        AttributeType = "Picklist",
                        OptionSetName = "incident_priority"
                    }
                }
            };

            var tmdl = fabricBuilder.GenerateTableTmdl(
                table,
                attributeDisplayInfo,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("LEFT JOIN [GlobalOptionsetMetadata] incident_prioritycode", tmdl);
            Assert.DoesNotContain("LEFT JOIN [OptionsetMetadata] incident_prioritycode", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_FabricLinkPicklist_WhenIsGlobalUnknown_UsesLocalMetadataForAttributeNamedOptionSet()
        {
            var fabricBuilder = new SemanticModelBuilder(
                _tempDir,
                connectionType: "FabricLink",
                fabricLinkEndpoint: "test-endpoint",
                fabricLinkDatabase: "test-db",
                languageCode: 1033,
                useDisplayNameAliasesInSql: false);

            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "severitycode",
                        DisplayName = "Severity",
                        SchemaName = "severitycode",
                        AttributeType = "Picklist",
                        OptionSetName = "severitycode"
                    }
                }
            };

            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["severitycode"] = new AttributeDisplayInfo
                    {
                        LogicalName = "severitycode",
                        DisplayName = "Severity",
                        AttributeType = "Picklist",
                        OptionSetName = "severitycode"
                    }
                }
            };

            var tmdl = fabricBuilder.GenerateTableTmdl(
                table,
                attributeDisplayInfo,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("LEFT JOIN [OptionsetMetadata] incident_severitycode", tmdl);
            Assert.DoesNotContain("LEFT JOIN [GlobalOptionsetMetadata] incident_severitycode", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_FabricLinkBoolean_UsesAttributeLogicalNameForOptionSetJoin()
        {
            var fabricBuilder = new SemanticModelBuilder(
                _tempDir,
                connectionType: "FabricLink",
                fabricLinkEndpoint: "test-endpoint",
                fabricLinkDatabase: "test-db",
                languageCode: 1033,
                useDisplayNameAliasesInSql: false);

            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "pbi_booleanfield",
                        DisplayName = "BooleanField",
                        AttributeType = "Boolean",
                        OptionSetName = "some_other_boolean_name",
                        IsGlobal = false
                    }
                }
            };

            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pbi_booleanfield"] = new AttributeDisplayInfo
                    {
                        LogicalName = "pbi_booleanfield",
                        DisplayName = "BooleanField",
                        AttributeType = "Boolean",
                        OptionSetName = "some_other_boolean_name",
                        IsGlobal = false
                    }
                }
            };

            var tmdl = fabricBuilder.GenerateTableTmdl(
                table,
                attributeDisplayInfo,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("[OptionSetName] = 'pbi_booleanfield'", tmdl);
            Assert.DoesNotContain("[OptionSetName] = 'some_other_boolean_name'", tmdl);
            Assert.Contains("LEFT JOIN [OptionsetMetadata] incident_pbi_booleanfield", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_FabricLinkPicklist_UsesAttributeLogicalNameForOptionSetJoin_WhenMetadataNameDiffers()
        {
            var fabricBuilder = new SemanticModelBuilder(
                _tempDir,
                connectionType: "FabricLink",
                fabricLinkEndpoint: "test-endpoint",
                fabricLinkDatabase: "test-db",
                languageCode: 1033,
                useDisplayNameAliasesInSql: false);

            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "pbi_choicefield_differentchoicename",
                        DisplayName = "ChoiceField_DifferentChoiceName",
                        AttributeType = "Picklist",
                        OptionSetName = "pbi_samplechoice",
                        IsGlobal = true
                    }
                }
            };

            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pbi_choicefield_differentchoicename"] = new AttributeDisplayInfo
                    {
                        LogicalName = "pbi_choicefield_differentchoicename",
                        DisplayName = "ChoiceField_DifferentChoiceName",
                        AttributeType = "Picklist",
                        OptionSetName = "pbi_samplechoice",
                        IsGlobal = true
                    }
                }
            };

            var tmdl = fabricBuilder.GenerateTableTmdl(
                table,
                attributeDisplayInfo,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("LEFT JOIN [GlobalOptionsetMetadata] incident_pbi_choicefield_differentchoicename", tmdl);
            Assert.Contains("[OptionSetName] = 'pbi_choicefield_differentchoicename'", tmdl);
            Assert.DoesNotContain("[OptionSetName] = 'pbi_samplechoice'", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_FabricLinkStatecode_Selected_IncludesStateLabelColumn()
        {
            var fabricBuilder = new SemanticModelBuilder(
                _tempDir,
                connectionType: "FabricLink",
                fabricLinkEndpoint: "test-endpoint",
                fabricLinkDatabase: "test-db",
                languageCode: 1033,
                useDisplayNameAliasesInSql: false);

            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "statecode",
                        DisplayName = "Status",
                        AttributeType = "State"
                    }
                }
            };

            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["statecode"] = new AttributeDisplayInfo
                    {
                        LogicalName = "statecode",
                        DisplayName = "Status",
                        AttributeType = "State"
                    }
                }
            };

            var tmdl = fabricBuilder.GenerateTableTmdl(
                table,
                attributeDisplayInfo,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("JOIN [StateMetadata] incident_statecode", tmdl);
            Assert.Contains("incident_statecode.[LocalizedLabel] statecodename", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_ExpandedLookupJoin_UsesTargetLogicalNameNotDisplayName()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "customerid",
                        DisplayName = "Customer",
                        SchemaName = "customerid",
                        AttributeType = "Lookup"
                    }
                },
                ExpandedLookups = new List<DataverseToPowerBI.Core.Models.ExpandedLookupConfig>
                {
                    new DataverseToPowerBI.Core.Models.ExpandedLookupConfig
                    {
                        LookupAttributeName = "customerid",
                        TargetTableLogicalName = "account",
                        TargetTableDisplayName = "Account Display Name",
                        TargetTablePrimaryKey = "accountid",
                        Attributes = new List<DataverseToPowerBI.Core.Models.ExpandedLookupAttribute>
                        {
                            new DataverseToPowerBI.Core.Models.ExpandedLookupAttribute
                            {
                                LogicalName = "name",
                                DisplayName = "Name",
                                AttributeType = "String"
                            }
                        }
                    }
                }
            };

            var attributeDisplayInfo = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new AttributeDisplayInfo
                    {
                        LogicalName = "customerid",
                        DisplayName = "Customer",
                        AttributeType = "Lookup"
                    }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(
                table,
                attributeDisplayInfo,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("LEFT OUTER JOIN account exp_customerid ON exp_customerid.accountid = Base.customerid", tmdl);
            Assert.DoesNotContain("LEFT OUTER JOIN Account Display Name exp_customerid", tmdl);
            Assert.Contains("Customer : Name", tmdl);
            Assert.DoesNotContain("Account Display Name : Name", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_LookupIncludeIdFalse_NoIdColumnInOutput()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "customerid",
                        DisplayName = "Customer",
                        AttributeType = "Lookup"
                    }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "customerid",
                        IncludeIdField = false,
                        IncludeNameField = true
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new AttributeDisplayInfo { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.DoesNotContain("column customerid", tmdl);
            Assert.DoesNotContain("sourceColumn: customerid", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_LookupIncludeIdTrueHidden_IdColumnIsHidden()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "customerid",
                        IncludeIdField = true,
                        IdFieldHidden = true,
                        IncludeNameField = false
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new AttributeDisplayInfo { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.Contains("column customerid", tmdl);
            Assert.Contains("isHidden", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_LookupIncludeNameFalse_NoNameColumnInOutput()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "customerid",
                        IncludeIdField = true,
                        IncludeNameField = false
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new AttributeDisplayInfo { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.DoesNotContain("sourceColumn: customeridname", tmdl);
            Assert.DoesNotContain("Base.customeridname", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_LookupRelationshipDefaults_IdHiddenNameExcluded()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "customerid",
                        IncludeIdField = true,
                        IdFieldHidden = true,
                        IncludeNameField = false
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new AttributeDisplayInfo { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.Contains("column customerid", tmdl);
            Assert.DoesNotContain("sourceColumn: customeridname", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_LookupNonRelationshipDefaults_IdExcludedNameVisible()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "customerid",
                        IncludeIdField = false,
                        IncludeNameField = true,
                        NameFieldHidden = false
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new AttributeDisplayInfo { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Lookup" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.DoesNotContain("column customerid", tmdl);
            Assert.Contains("sourceColumn: Customer", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_OwnerType_TypeAndYomiDefaultExcluded()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "ownerid", DisplayName = "Owner", AttributeType = "Owner" }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ownerid"] = new AttributeDisplayInfo { LogicalName = "ownerid", DisplayName = "Owner", AttributeType = "Owner" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.DoesNotContain("owneridtype", tmdl);
            Assert.DoesNotContain("owneridyominame", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_OwnerType_TypeIncluded_TypeColumnPresent()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "ownerid", DisplayName = "Owner", AttributeType = "Owner" }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ownerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "ownerid",
                        IncludeNameField = true,
                        IncludeTypeField = true,
                        IncludeYomiField = false
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ownerid"] = new AttributeDisplayInfo { LogicalName = "ownerid", DisplayName = "Owner", AttributeType = "Owner" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.Contains("owneridtype", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_PolymorphicVirtualColumn_OrderingBugFixed()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "owneridname", DisplayName = null, AttributeType = "String" },
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "ownerid", DisplayName = "Owner", AttributeType = "Owner" }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ownerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "ownerid",
                        IncludeIdField = false,
                        IncludeNameField = true
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ownerid"] = new AttributeDisplayInfo { LogicalName = "ownerid", DisplayName = "Owner", AttributeType = "Owner" },
                    ["owneridname"] = new AttributeDisplayInfo { LogicalName = "owneridname", DisplayName = null, AttributeType = "String" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.Contains("column Owner", tmdl);
            Assert.DoesNotContain("column owneridname", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_CustomerType_VirtualColumnBeforeParent_CorrectDisplayName()
        {
            var table = new ExportTable
            {
                LogicalName = "contact",
                DisplayName = "Contact",
                PrimaryIdAttribute = "contactid",
                PrimaryNameAttribute = "fullname",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "customeridname", DisplayName = null, AttributeType = "String" },
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Customer" }
                },
                LookupSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new DataverseToPowerBI.Core.Models.LookupSubColumnConfig
                    {
                        LookupAttributeLogicalName = "customerid",
                        IncludeIdField = false,
                        IncludeNameField = true
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["contact"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["customerid"] = new AttributeDisplayInfo { LogicalName = "customerid", DisplayName = "Customer", AttributeType = "Customer" },
                    ["customeridname"] = new AttributeDisplayInfo { LogicalName = "customeridname", DisplayName = null, AttributeType = "String" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.Contains("column Customer", tmdl);
            Assert.DoesNotContain("column customeridname", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_RelationshipRequiredLookup_SelectedWithoutConfig_StillIncludesIdColumn()
        {
            var table = new ExportTable
            {
                LogicalName = "product",
                DisplayName = "Product",
                PrimaryIdAttribute = "productid",
                PrimaryNameAttribute = "name",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "createdby",
                        DisplayName = "Created By",
                        AttributeType = "Lookup"
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["product"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["createdby"] = new AttributeDisplayInfo
                    {
                        LogicalName = "createdby",
                        DisplayName = "Created By",
                        AttributeType = "Lookup"
                    }
                }
            };

            var requiredLookupColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "createdby" };
            var tmdl = _builder.GenerateTableTmdl(table, info, requiredLookupColumns);

            Assert.Contains("column createdby", tmdl);
            Assert.Contains("sourceColumn: createdby", tmdl);
            Assert.Contains("isHidden", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_ChoiceValueColumns_UseSummarizeByNone_ForPicklistStateStatusBoolean()
        {
            var table = new ExportTable
            {
                LogicalName = "incident",
                DisplayName = "Incident",
                PrimaryIdAttribute = "incidentid",
                PrimaryNameAttribute = "title",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "prioritycode",
                        DisplayName = "Priority",
                        AttributeType = "Picklist",
                        VirtualAttributeName = "prioritycodename"
                    },
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "statecode",
                        DisplayName = "Status",
                        AttributeType = "State",
                        VirtualAttributeName = "statecodename"
                    },
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "statuscode",
                        DisplayName = "Status Reason",
                        AttributeType = "Status",
                        VirtualAttributeName = "statuscodename"
                    },
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "donotemail",
                        DisplayName = "Do Not Email",
                        AttributeType = "Boolean",
                        VirtualAttributeName = "donotemailname"
                    }
                },
                ChoiceSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.ChoiceSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["prioritycode"] = new DataverseToPowerBI.Core.Models.ChoiceSubColumnConfig
                    {
                        AttributeLogicalName = "prioritycode",
                        IncludeValueField = true,
                        ValueFieldHidden = true,
                        IncludeLabelField = true,
                        LabelFieldHidden = false
                    },
                    ["statecode"] = new DataverseToPowerBI.Core.Models.ChoiceSubColumnConfig
                    {
                        AttributeLogicalName = "statecode",
                        IncludeValueField = true,
                        ValueFieldHidden = true,
                        IncludeLabelField = true,
                        LabelFieldHidden = false
                    },
                    ["statuscode"] = new DataverseToPowerBI.Core.Models.ChoiceSubColumnConfig
                    {
                        AttributeLogicalName = "statuscode",
                        IncludeValueField = true,
                        ValueFieldHidden = true,
                        IncludeLabelField = true,
                        LabelFieldHidden = false
                    },
                    ["donotemail"] = new DataverseToPowerBI.Core.Models.ChoiceSubColumnConfig
                    {
                        AttributeLogicalName = "donotemail",
                        IncludeValueField = true,
                        ValueFieldHidden = true,
                        IncludeLabelField = true,
                        LabelFieldHidden = false
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["incident"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["prioritycode"] = new AttributeDisplayInfo { LogicalName = "prioritycode", DisplayName = "Priority", AttributeType = "Picklist", VirtualAttributeName = "prioritycodename" },
                    ["statecode"] = new AttributeDisplayInfo { LogicalName = "statecode", DisplayName = "Status", AttributeType = "State", VirtualAttributeName = "statecodename" },
                    ["statuscode"] = new AttributeDisplayInfo { LogicalName = "statuscode", DisplayName = "Status Reason", AttributeType = "Status", VirtualAttributeName = "statuscodename" },
                    ["donotemail"] = new AttributeDisplayInfo { LogicalName = "donotemail", DisplayName = "Do Not Email", AttributeType = "Boolean", VirtualAttributeName = "donotemailname" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            AssertColumnUsesSummarizeByNone(tmdl, "prioritycode");
            AssertColumnUsesSummarizeByNone(tmdl, "statecode");
            AssertColumnUsesSummarizeByNone(tmdl, "statuscode");
            AssertColumnUsesSummarizeByNone(tmdl, "donotemail");
        }

        [Fact]
        public void GenerateTableTmdl_MultiSelectChoiceValueColumn_UsesSummarizeByNone()
        {
            var table = new ExportTable
            {
                LogicalName = "account",
                DisplayName = "Account",
                PrimaryIdAttribute = "accountid",
                PrimaryNameAttribute = "name",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata
                    {
                        LogicalName = "preferredcontactmethods",
                        DisplayName = "Preferred Contact Methods",
                        AttributeType = "MultiSelectPicklist",
                        VirtualAttributeName = "preferredcontactmethodsname"
                    }
                },
                ChoiceSubColumnConfigs = new Dictionary<string, DataverseToPowerBI.Core.Models.ChoiceSubColumnConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["preferredcontactmethods"] = new DataverseToPowerBI.Core.Models.ChoiceSubColumnConfig
                    {
                        AttributeLogicalName = "preferredcontactmethods",
                        IncludeValueField = true,
                        ValueFieldHidden = true,
                        IncludeLabelField = true,
                        LabelFieldHidden = false
                    }
                }
            };

            var info = new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["account"] = new Dictionary<string, AttributeDisplayInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["preferredcontactmethods"] = new AttributeDisplayInfo
                    {
                        LogicalName = "preferredcontactmethods",
                        DisplayName = "Preferred Contact Methods",
                        AttributeType = "MultiSelectPicklist",
                        VirtualAttributeName = "preferredcontactmethodsname"
                    }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(table, info, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            AssertColumnUsesSummarizeByNone(tmdl, "preferredcontactmethods");
        }

        private static void AssertColumnUsesSummarizeByNone(string tmdl, string columnName)
        {
            var marker = $"\tcolumn {columnName}";
            var start = tmdl.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Column '{columnName}' was not found in generated TMDL.");

            var end = tmdl.IndexOf("\n\tcolumn ", start + marker.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                end = tmdl.IndexOf("\n\tpartition ", start + marker.Length, StringComparison.Ordinal);
            }
            if (end < 0)
            {
                end = tmdl.Length;
            }

            var block = tmdl.Substring(start, end - start);
            Assert.Contains("summarizeBy: none", block);
        }

        [Fact]
        public void GenerateTableTmdl_DimensionTable_DefaultAutoMeasuresDisabled()
        {
            var table = new ExportTable
            {
                LogicalName = "account",
                DisplayName = "Account",
                SchemaName = "Account",
                PrimaryIdAttribute = "accountid",
                PrimaryNameAttribute = "name",
                Role = "Dimension",
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "accountid", DisplayName = "Account", AttributeType = "Uniqueidentifier" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(
                table,
                new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.DoesNotContain("measure 'Link to Account'", tmdl);
            Assert.DoesNotContain("measure 'Account Count'", tmdl);
        }

        [Fact]
        public void GenerateTableTmdl_DimensionTable_WithMeasureOptions_RespectsSelectedMeasures()
        {
            var table = new ExportTable
            {
                LogicalName = "account",
                DisplayName = "Account",
                SchemaName = "Account",
                PrimaryIdAttribute = "accountid",
                PrimaryNameAttribute = "name",
                Role = "Dimension",
                IncludeCountMeasure = true,
                IncludeRecordLinkMeasure = false,
                Attributes = new List<DataverseToPowerBI.Core.Models.AttributeMetadata>
                {
                    new DataverseToPowerBI.Core.Models.AttributeMetadata { LogicalName = "accountid", DisplayName = "Account", AttributeType = "Uniqueidentifier" }
                }
            };

            var tmdl = _builder.GenerateTableTmdl(
                table,
                new Dictionary<string, Dictionary<string, AttributeDisplayInfo>>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Contains("measure 'Account Count'", tmdl);
            Assert.DoesNotContain("measure 'Link to Account'", tmdl);
        }

        #endregion

        #region Auto-Measure Cleanup Tests

        [Fact]
        public void ExtractUserMeasuresSection_ExcludesAutoMeasuresForRoleChange()
        {
            // When a table changes from Fact to Dimension, "Link to X" and "X Count" are auto-measures
            // They should still be excluded even if the table's role changes
            var table = new ExportTable
            {
                LogicalName = "account",
                DisplayName = "Account"
            };

            var result = _builder.ExtractUserMeasuresSection(FixturePath("SampleTable.tmdl"), table);
            // Auto-generated measures should always be excluded
            Assert.DoesNotContain("Link to Account", result ?? "");
            Assert.DoesNotContain("Account Count", result ?? "");
            // User measures should still be preserved
            if (result != null)
            {
                Assert.Contains("Custom Measure", result);
            }
        }

        #endregion

        #region Path Traversal Guard Tests

        [Fact]
        public void CopyDirectory_ProjectNameWithPathTraversal_ThrowsInvalidOperation()
        {
            // CopyDirectory is private, so invoke it via reflection to test the guard directly
            var method = typeof(SemanticModelBuilder).GetMethod("CopyDirectory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // Create a minimal source directory with one file containing the template name
            var sourceDir = Path.Combine(_tempDir, "src");
            var targetDir = Path.Combine(_tempDir, "target");
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "Template.txt"), "placeholder");

            // A project name containing ".." causes path traversal when replacing template name
            var projectName = @"..\..\..\malicious";
            var templateName = "Template";

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method!.Invoke(_builder, new object[] { sourceDir, targetDir, projectName, templateName }));

            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Contains("Path traversal", ex.InnerException!.Message);
        }

        #endregion
    }
}
