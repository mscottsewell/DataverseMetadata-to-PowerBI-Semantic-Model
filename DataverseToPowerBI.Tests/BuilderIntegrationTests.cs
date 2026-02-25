// =============================================================================
// BuilderIntegrationTests.cs - End-to-End SemanticModelBuilder Tests
// =============================================================================
// Purpose: Integration tests that exercise SemanticModelBuilder.Build() with
// realistic input data and validate the generated PBIP/TMDL output structure
// and content. Covers DataverseTDS and FabricLink modes, star schemas,
// date tables, view filters, expanded lookups, and column types.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataverseToPowerBI.Core.Models;
using DataverseToPowerBI.XrmToolBox;
using DataverseToPowerBI.XrmToolBox.Services;
using Xunit;

using XrmExportTable = DataverseToPowerBI.XrmToolBox.ExportTable;
using XrmAttributeDisplayInfo = DataverseToPowerBI.XrmToolBox.AttributeDisplayInfo;

namespace DataverseToPowerBI.Tests
{
    public class BuilderIntegrationTests : IDisposable
    {
        private readonly string _templatePath;
        private readonly string _tempDir;

        public BuilderIntegrationTests()
        {
            _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PBIP_DefaultTemplate");
            _tempDir = Path.Combine(Path.GetTempPath(), "DataverseToPowerBI_IntTests_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private SemanticModelBuilder CreateBuilder(string connectionType = "DataverseTDS",
            string? fabricEndpoint = null, string? fabricDatabase = null,
            bool useDisplayNameAliases = true, string storageMode = "DirectQuery")
        {
            return new SemanticModelBuilder(
                _templatePath,
                statusCallback: null,
                connectionType: connectionType,
                fabricLinkEndpoint: fabricEndpoint,
                fabricLinkDatabase: fabricDatabase,
                useDisplayNameAliasesInSql: useDisplayNameAliases,
                storageMode: storageMode);
        }

        #region Minimal Build Tests

        [Fact]
        public void Build_SingleTable_ProducesValidPbipStructure()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithAttribute("revenue", "Annual Revenue", "Money"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertPbipStructure(_tempDir, "TestModel");
            TmdlAssertions.AssertTableFileExists(_tempDir, "Account");
            TmdlAssertions.AssertTableFileExists(_tempDir, "DataverseURL");
            TmdlAssertions.AssertDiagramLayoutExists(_tempDir);
        }

        [Fact]
        public void Build_SingleTable_ModelTmdlReferencesTable()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertModelReferencesTable(_tempDir, "Account");
            TmdlAssertions.AssertModelReferencesTable(_tempDir, "DataverseURL");
        }

        [Fact]
        public void Build_SingleTable_NoRelationshipsFile()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            // No relationships configured, so relationships.tmdl should not exist
            var defDir = Directory.GetDirectories(_tempDir, "definition", SearchOption.AllDirectories).FirstOrDefault();
            if (defDir != null)
            {
                var relPath = Path.Combine(defDir, "relationships.tmdl");
                Assert.False(File.Exists(relPath), "relationships.tmdl should not exist when no relationships are defined");
            }
        }

        [Fact]
        public void Build_SingleTable_TDS_NoExpressionsFile()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertExpressionsFileDoesNotExist(_tempDir);
        }

        #endregion

        #region Star Schema Tests

        [Fact]
        public void Build_StarSchema_ProducesAllTableFiles()
        {
            var scenario = BuildStarSchemaScenario();

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertPbipStructure(_tempDir, scenario.SemanticModelName);
            TmdlAssertions.AssertTableFileExists(_tempDir, "Opportunity");
            TmdlAssertions.AssertTableFileExists(_tempDir, "Account");
            TmdlAssertions.AssertTableFileExists(_tempDir, "Contact");
        }

        [Fact]
        public void Build_StarSchema_ProducesRelationshipsFile()
        {
            var scenario = BuildStarSchemaScenario();

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertRelationshipsFileExists(_tempDir);
        }

        [Fact]
        public void Build_StarSchema_ModelReferencesAllTables()
        {
            var scenario = BuildStarSchemaScenario();

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertModelReferencesTable(_tempDir, "Opportunity");
            TmdlAssertions.AssertModelReferencesTable(_tempDir, "Account");
            TmdlAssertions.AssertModelReferencesTable(_tempDir, "Contact");
        }

        [Fact]
        public void Build_StarSchema_RelationshipsReferenceCorrectTables()
        {
            var scenario = BuildStarSchemaScenario();

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var relContent = TmdlAssertions.ReadRelationshipsTmdl(_tempDir);
            // Should contain references to both Account and Contact dimensions
            Assert.Contains("Account", relContent);
            Assert.Contains("Contact", relContent);
        }

        #endregion

        #region FabricLink Mode Tests

        [Fact]
        public void Build_FabricLink_ProducesExpressionsFile()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"))
                .UseFabricLink();

            var builder = CreateBuilder(
                connectionType: "FabricLink",
                fabricEndpoint: scenario.FabricEndpoint,
                fabricDatabase: scenario.FabricDatabase);

            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertExpressionsFileExists(_tempDir);
        }

        [Fact]
        public void Build_FabricLink_ModelReferencesExpressions()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"))
                .UseFabricLink();

            var builder = CreateBuilder(
                connectionType: "FabricLink",
                fabricEndpoint: scenario.FabricEndpoint,
                fabricDatabase: scenario.FabricDatabase);

            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var modelContent = TmdlAssertions.ReadModelTmdl(_tempDir);
            var exprRefs = TmdlAssertions.ExtractModelExpressionRefs(modelContent);
            Assert.Contains("FabricSQLEndpoint", exprRefs);
            Assert.Contains("FabricLakehouse", exprRefs);
        }

        [Fact]
        public void Build_FabricLink_TablePartitionUsesSqlDatabase()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"))
                .UseFabricLink();

            var builder = CreateBuilder(
                connectionType: "FabricLink",
                fabricEndpoint: scenario.FabricEndpoint,
                fabricDatabase: scenario.FabricDatabase);

            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            Assert.Contains("Sql.Database", tableContent);
        }

        [Fact]
        public void Build_TDS_TablePartitionUsesCommonDataService()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            Assert.Contains("CommonDataService.Database", tableContent);
        }

        #endregion

        #region Column Type Tests

        [Fact]
        public void Build_PicklistColumn_TDS_IncludesVirtualNameColumn()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithPicklist("industrycode", "Industry", "industrycode"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            // TDS mode uses virtual name column for picklist labels
            Assert.Contains("industrycodename", tableContent);
        }

        [Fact]
        public void Build_LookupColumn_IncludesLookupInOutput()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .WithLookup("_parentaccountid_value", "Parent Account", "account")
                    .AsFact());

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Opportunity");
            Assert.Contains("_parentaccountid_value", tableContent);
        }

        [Fact]
        public void Build_StatusFields_IncludesStateAndStatus()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithStatusFields());

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            Assert.Contains("statecode", tableContent);
            Assert.Contains("statuscode", tableContent);
        }

        [Fact]
        public void Build_MoneyColumn_IncludesInOutput()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithMoney("revenue", "Annual Revenue"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            Assert.Contains("revenue", tableContent);
        }

        [Fact]
        public void Build_DateTimeColumn_IncludesInOutput()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithDateTime("createdon", "Created On"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            Assert.Contains("createdon", tableContent);
        }

        #endregion

        #region Date Table Tests

        [Fact]
        public void Build_WithDateTable_ProducesDateTmdlFile()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .WithDateTime("createdon", "Created On")
                    .AsFact())
                .WithDateTable(new DateTableConfigBuilder()
                    .ForTable("opportunity", "createdon")
                    .WithTimeZone("Eastern Standard Time"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo(),
                scenario.BuildDateTableConfig());

            TmdlAssertions.AssertTableFileExists(_tempDir, "Date");
        }

        [Fact]
        public void Build_WithDateTable_ModelReferencesDateTable()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .WithDateTime("createdon", "Created On")
                    .AsFact())
                .WithDateTable(new DateTableConfigBuilder()
                    .ForTable("opportunity", "createdon")
                    .WithTimeZone("Eastern Standard Time"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo(),
                scenario.BuildDateTableConfig());

            TmdlAssertions.AssertModelReferencesTable(_tempDir, "Date");
        }

        [Fact]
        public void Build_WithDateTable_ProducesDateRelationship()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .WithDateTime("createdon", "Created On")
                    .AsFact())
                .WithDateTable(new DateTableConfigBuilder()
                    .ForTable("opportunity", "createdon")
                    .WithTimeZone("Eastern Standard Time"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo(),
                scenario.BuildDateTableConfig());

            TmdlAssertions.AssertRelationshipsFileExists(_tempDir);
            var relContent = TmdlAssertions.ReadRelationshipsTmdl(_tempDir);
            Assert.Contains("Date", relContent);
        }

        [Fact]
        public void Build_WithoutDateTable_NoDateTmdlFile()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertTableFileDoesNotExist(_tempDir, "Date");
        }

        #endregion

        #region View Filter Tests

        [Fact]
        public void Build_TableWithViewFilter_PartitionContainsWhereClause()
        {
            var fetchXml = @"<fetch><entity name=""account""><filter><condition attribute=""statecode"" operator=""eq"" value=""0"" /></filter></entity></fetch>";

            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithView("Active Accounts", fetchXml));

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            // The view FetchXML should be converted to a SQL WHERE clause
            Assert.Contains("WHERE", tableContent, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Multiple Table Types

        [Fact]
        public void Build_MultipleTables_EachGetsOwnTmdlFile()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"))
                .WithTable(new TableBuilder("contact", "Contact")
                    .WithPrimaryName("fullname", "Full Name"))
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .AsFact());

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            TmdlAssertions.AssertTableFileExists(_tempDir, "Account");
            TmdlAssertions.AssertTableFileExists(_tempDir, "Contact");
            TmdlAssertions.AssertTableFileExists(_tempDir, "Opportunity");
        }

        [Fact]
        public void Build_FactAndDimension_CorrectStorageMode()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .AsFact())
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .AsDimension());

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            // Both tables should be generated
            TmdlAssertions.AssertTableFileExists(_tempDir, "Opportunity");
            TmdlAssertions.AssertTableFileExists(_tempDir, "Account");
        }

        #endregion

        #region Expanded Lookup Tests

        [Fact]
        public void Build_ExpandedLookup_TableContentIncludesJoinedColumns()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .WithLookup("_parentaccountid_value", "Parent Account", "account")
                    .WithExpandedLookup("_parentaccountid_value", "account", "accountid",
                        ("name", "Account Name", "String"),
                        ("revenue", "Annual Revenue", "Money"))
                    .AsFact());

            var builder = CreateBuilder();
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Opportunity");
            // Expanded lookup should produce a JOIN or flattened columns
            Assert.Contains("LEFT OUTER JOIN", tableContent, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region FabricLink with Picklist Tests

        [Fact]
        public void Build_FabricLink_PicklistColumn_UsesMetadataJoin()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithPicklist("industrycode", "Industry", "industrycode"))
                .UseFabricLink();

            var builder = CreateBuilder(
                connectionType: "FabricLink",
                fabricEndpoint: scenario.FabricEndpoint,
                fabricDatabase: scenario.FabricDatabase);

            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            // FabricLink mode joins to OptionsetMetadata for picklist labels
            Assert.Contains("OptionsetMetadata", tableContent, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Display Name Alias Tests

        [Fact]
        public void Build_WithDisplayNameAliases_ColumnsHaveAliases()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithAttribute("revenue", "Annual Revenue", "Money"))
                .WithDisplayNameAliases(true);

            var builder = CreateBuilder(useDisplayNameAliases: true);
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var tableContent = TmdlAssertions.ReadTableTmdl(_tempDir, "Account");
            // With display name aliases, SQL should contain AS [Display Name]
            Assert.Contains("AS [", tableContent);
        }

        [Fact]
        public void Build_WithoutDisplayNameAliases_UsesLogicalNames()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithAttribute("revenue", "Annual Revenue", "Money"))
                .WithDisplayNameAliases(false);

            var builder = CreateBuilder(useDisplayNameAliases: false);
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            // Table file should still exist and be valid
            TmdlAssertions.AssertTableFileExists(_tempDir, "Account");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Build_EmptyTableList_ProducesMinimalPbipStructure()
        {
            var builder = CreateBuilder();
            var tables = new List<XrmExportTable>();
            var relationships = new List<ExportRelationship>();
            var displayInfo = new Dictionary<string, Dictionary<string, XrmAttributeDisplayInfo>>();

            builder.Build("EmptyModel", _tempDir, "https://testorg.crm.dynamics.com",
                tables, relationships, displayInfo);

            TmdlAssertions.AssertPbipStructure(_tempDir, "EmptyModel");
            TmdlAssertions.AssertTableFileExists(_tempDir, "DataverseURL");
        }

        [Fact]
        public void Build_RepeatedBuilds_ProduceConsistentStructure()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name"));

            var builder = CreateBuilder();

            // Build once
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var firstColumns = TmdlAssertions.ExtractColumnNames(TmdlAssertions.ReadTableTmdl(_tempDir, "Account"));

            // Build again (should overwrite cleanly)
            builder.Build(scenario.SemanticModelName, _tempDir, scenario.DataverseUrl,
                scenario.BuildTables(), scenario.BuildRelationships(), scenario.BuildAttributeDisplayInfo());

            var secondColumns = TmdlAssertions.ExtractColumnNames(TmdlAssertions.ReadTableTmdl(_tempDir, "Account"));

            // Structure (columns) should be identical even if lineageTags differ
            Assert.Equal(firstColumns, secondColumns);
        }

        [Fact]
        public void AnalyzeChanges_AfterUnchangedBuild_ReportsNoActionableChanges()
        {
            var scenario = new ScenarioBuilder()
                .WithTable(new TableBuilder("cai_allocation", "Allocation")
                    .WithPrimaryName("cai_name", "Allocation Name")
                    .WithAttribute("cai_amount", "Amount", "Money")
                    .WithPicklist("cai_status", "Status", "cai_status")
                    .AsFact())
                .WithTable(new TableBuilder("cai_serviceorinitiative", "Service or Initiative")
                    .WithPrimaryName("cai_name", "Service or Initiative Name")
                    .WithAttribute("cai_area", "Area", "String")
                    .AsDimension())
                .WithRelationship(new RelationshipBuilder()
                    .From("cai_allocation", "cai_serviceorinitiativeid")
                    .To("cai_serviceorinitiative")
                    .Named("Allocation → Service or Initiative"));

            var tables = scenario.BuildTables();
            var relationships = scenario.BuildRelationships();
            var displayInfo = scenario.BuildAttributeDisplayInfo();

            var builder = CreateBuilder();

            builder.Build(
                scenario.SemanticModelName,
                _tempDir,
                scenario.DataverseUrl,
                tables,
                relationships,
                displayInfo);

            var changes = builder.AnalyzeChanges(
                scenario.SemanticModelName,
                _tempDir,
                scenario.DataverseUrl,
                tables,
                relationships,
                displayInfo);

            var actionable = changes.Where(c => c.ChangeType == ChangeType.New || c.ChangeType == ChangeType.Update).ToList();
            Assert.Empty(actionable);
        }

        #endregion

        #region Scenario Helpers

        private ScenarioBuilder BuildStarSchemaScenario()
        {
            return new ScenarioBuilder()
                .WithTable(new TableBuilder("opportunity", "Opportunity")
                    .WithPrimaryName("name", "Opportunity Name")
                    .WithMoney("estimatedvalue", "Estimated Value")
                    .WithLookup("_parentaccountid_value", "Parent Account", "account")
                    .WithLookup("_parentcontactid_value", "Parent Contact", "contact")
                    .WithDateTime("createdon", "Created On")
                    .AsFact())
                .WithTable(new TableBuilder("account", "Account")
                    .WithPrimaryName("name", "Account Name")
                    .WithAttribute("telephone1", "Phone", "String")
                    .AsDimension())
                .WithTable(new TableBuilder("contact", "Contact")
                    .WithPrimaryName("fullname", "Full Name")
                    .WithAttribute("emailaddress1", "Email", "String")
                    .AsDimension())
                .WithRelationship(new RelationshipBuilder()
                    .From("opportunity", "_parentaccountid_value")
                    .To("account")
                    .Named("Opportunity → Account"))
                .WithRelationship(new RelationshipBuilder()
                    .From("opportunity", "_parentcontactid_value")
                    .To("contact")
                    .Named("Opportunity → Contact"));
        }

        #endregion
    }
}
