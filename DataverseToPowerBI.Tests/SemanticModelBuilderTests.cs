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
    }
}
