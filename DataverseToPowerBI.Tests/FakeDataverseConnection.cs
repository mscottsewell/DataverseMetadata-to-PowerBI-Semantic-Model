// =============================================================================
// FakeDataverseConnection.cs - Test Double for IDataverseConnection
// =============================================================================
// Purpose: In-memory implementation of IDataverseConnection for testing flows
// that start from Dataverse metadata retrieval. Allows tests to configure
// canned responses for solutions, tables, attributes, forms, and views
// without requiring a live Dataverse connection.
//
// Usage:
//   var fake = new FakeDataverseConnection("https://testorg.crm.dynamics.com")
//       .WithSolution("MySolution", "My Solution")
//       .WithTable("account", "Account", "accountid", "name")
//       .WithAttributes("account",
//           new AttributeMetadata { LogicalName = "name", DisplayName = "Name", AttributeType = "String" })
//       .WithForm("account", "main-form-id", "Main Form")
//       .WithView("account", "active-view-id", "Active Accounts", fetchXml);
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataverseToPowerBI.Core.Interfaces;
using DataverseToPowerBI.Core.Models;

namespace DataverseToPowerBI.Tests
{
    /// <summary>
    /// In-memory test double for <see cref="IDataverseConnection"/>.
    /// Configure with fluent methods, then pass to code that depends on the interface.
    /// </summary>
    public class FakeDataverseConnection : IDataverseConnection
    {
        private readonly string _environmentUrl;
        private readonly List<DataverseSolution> _solutions = new();
        private readonly Dictionary<string, List<TableInfo>> _solutionTables = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TableMetadata> _tableMetadata = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<AttributeMetadata>> _tableAttributes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<FormMetadata>> _tableForms = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _formXml = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ViewMetadata>> _tableViews = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _viewFetchXml = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new fake connection for the given environment URL.
        /// </summary>
        public FakeDataverseConnection(string environmentUrl = "https://testorg.crm.dynamics.com")
        {
            _environmentUrl = environmentUrl;
        }

        #region Fluent Configuration

        /// <summary>Adds a solution to the fake.</summary>
        public FakeDataverseConnection WithSolution(string uniqueName, string friendlyName, string? version = null)
        {
            _solutions.Add(new DataverseSolution
            {
                SolutionId = Guid.NewGuid().ToString(),
                UniqueName = uniqueName,
                FriendlyName = friendlyName,
                Version = version ?? "1.0.0.0"
            });
            return this;
        }

        /// <summary>Adds a table to a solution.</summary>
        public FakeDataverseConnection WithTable(string solutionUniqueName, string logicalName, string displayName,
            string primaryIdAttribute, string? primaryNameAttribute = null, int objectTypeCode = 0)
        {
            if (!_solutionTables.ContainsKey(solutionUniqueName))
                _solutionTables[solutionUniqueName] = new List<TableInfo>();

            var tableInfo = new TableInfo
            {
                LogicalName = logicalName,
                DisplayName = displayName,
                SchemaName = logicalName.Length > 0 ? char.ToUpper(logicalName[0]) + logicalName.Substring(1) : logicalName,
                PrimaryIdAttribute = primaryIdAttribute,
                PrimaryNameAttribute = primaryNameAttribute ?? "name",
                ObjectTypeCode = objectTypeCode
            };
            _solutionTables[solutionUniqueName].Add(tableInfo);

            _tableMetadata[logicalName] = new TableMetadata
            {
                LogicalName = logicalName,
                DisplayName = displayName,
                SchemaName = tableInfo.SchemaName,
                PrimaryIdAttribute = primaryIdAttribute,
                PrimaryNameAttribute = primaryNameAttribute ?? "name"
            };

            return this;
        }

        /// <summary>Adds attributes to a table.</summary>
        public FakeDataverseConnection WithAttributes(string tableName, params AttributeMetadata[] attributes)
        {
            if (!_tableAttributes.ContainsKey(tableName))
                _tableAttributes[tableName] = new List<AttributeMetadata>();
            _tableAttributes[tableName].AddRange(attributes);
            return this;
        }

        /// <summary>Adds a form to a table.</summary>
        public FakeDataverseConnection WithForm(string tableName, string formId, string formName, string? formXml = null, List<string>? fields = null)
        {
            if (!_tableForms.ContainsKey(tableName))
                _tableForms[tableName] = new List<FormMetadata>();

            _tableForms[tableName].Add(new FormMetadata
            {
                FormId = formId,
                Name = formName,
                FormXml = formXml,
                Fields = fields
            });

            if (formXml != null)
                _formXml[formId] = formXml;

            return this;
        }

        /// <summary>Adds a view to a table.</summary>
        public FakeDataverseConnection WithView(string tableName, string viewId, string viewName, string? fetchXml = null)
        {
            if (!_tableViews.ContainsKey(tableName))
                _tableViews[tableName] = new List<ViewMetadata>();

            _tableViews[tableName].Add(new ViewMetadata
            {
                ViewId = viewId,
                Name = viewName,
                FetchXml = fetchXml
            });

            if (fetchXml != null)
                _viewFetchXml[viewId] = fetchXml;

            return this;
        }

        #endregion

        #region IDataverseConnection Implementation

        /// <inheritdoc/>
        public bool IsConnected => true;

        /// <inheritdoc/>
        public Task<string> AuthenticateAsync(bool clearCredentials = false)
        {
            return Task.FromResult("fake-token");
        }

        /// <inheritdoc/>
        public string GetEnvironmentUrl() => _environmentUrl;

        /// <inheritdoc/>
        public Task<List<DataverseSolution>> GetSolutionsAsync()
        {
            return Task.FromResult(new List<DataverseSolution>(_solutions));
        }

        /// <inheritdoc/>
        public Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId)
        {
            // Look up by solution ID first, then by unique name
            var solution = _solutions.FirstOrDefault(s => s.SolutionId == solutionId);
            var key = solution?.UniqueName ?? solutionId;

            if (_solutionTables.TryGetValue(key, out var tables))
                return Task.FromResult(new List<TableInfo>(tables));

            return Task.FromResult(new List<TableInfo>());
        }

        /// <inheritdoc/>
        public Task<TableMetadata> GetTableMetadataAsync(string logicalName)
        {
            if (_tableMetadata.TryGetValue(logicalName, out var metadata))
                return Task.FromResult(metadata);

            return Task.FromResult(new TableMetadata { LogicalName = logicalName });
        }

        /// <inheritdoc/>
        public Task<List<AttributeMetadata>> GetAttributesAsync(string tableName)
        {
            if (_tableAttributes.TryGetValue(tableName, out var attributes))
                return Task.FromResult(new List<AttributeMetadata>(attributes));

            return Task.FromResult(new List<AttributeMetadata>());
        }

        /// <inheritdoc/>
        public Task<List<FormMetadata>> GetFormsAsync(string entityLogicalName, bool includeXml = false)
        {
            if (_tableForms.TryGetValue(entityLogicalName, out var forms))
                return Task.FromResult(new List<FormMetadata>(forms));

            return Task.FromResult(new List<FormMetadata>());
        }

        /// <inheritdoc/>
        public Task<string?> GetFormXmlAsync(string formId)
        {
            _formXml.TryGetValue(formId, out var xml);
            return Task.FromResult<string?>(xml);
        }

        /// <inheritdoc/>
        public Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false)
        {
            if (_tableViews.TryGetValue(entityLogicalName, out var views))
                return Task.FromResult(new List<ViewMetadata>(views));

            return Task.FromResult(new List<ViewMetadata>());
        }

        /// <inheritdoc/>
        public Task<string?> GetViewFetchXmlAsync(string viewId)
        {
            _viewFetchXml.TryGetValue(viewId, out var xml);
            return Task.FromResult<string?>(xml);
        }

        #endregion
    }
}
