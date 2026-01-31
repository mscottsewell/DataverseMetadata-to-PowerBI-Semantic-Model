using System.Collections.Generic;
using System.Threading.Tasks;
using DataverseToPowerBI.Core.Models;

namespace DataverseToPowerBI.Core.Interfaces
{
    /// <summary>
    /// Abstraction for connecting to Dataverse, supporting both MSAL (standalone) and IOrganizationService (XrmToolBox)
    /// </summary>
    public interface IDataverseConnection
    {
        /// <summary>
        /// Authenticates to Dataverse (MSAL only - XrmToolBox handles this externally)
        /// </summary>
        Task<string> AuthenticateAsync(bool clearCredentials = false);

        /// <summary>
        /// Gets all solutions available in the environment
        /// </summary>
        Task<List<DataverseSolution>> GetSolutionsAsync();

        /// <summary>
        /// Gets all tables in a specific solution
        /// </summary>
        Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId);

        /// <summary>
        /// Gets metadata for a specific table
        /// </summary>
        Task<TableMetadata> GetTableMetadataAsync(string logicalName);

        /// <summary>
        /// Gets all attributes for a table
        /// </summary>
        Task<List<AttributeMetadata>> GetAttributesAsync(string tableName);

        /// <summary>
        /// Gets all forms for a table
        /// </summary>
        Task<List<FormMetadata>> GetFormsAsync(string entityLogicalName, bool includeXml = false);

        /// <summary>
        /// Gets form XML by form ID
        /// </summary>
        Task<string?> GetFormXmlAsync(string formId);

        /// <summary>
        /// Gets all views for a table
        /// </summary>
        Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false);

        /// <summary>
        /// Gets FetchXML for a specific view
        /// </summary>
        Task<string?> GetViewFetchXmlAsync(string viewId);

        /// <summary>
        /// Gets the environment URL (for display/logging purposes)
        /// </summary>
        string GetEnvironmentUrl();

        /// <summary>
        /// Indicates if this connection is ready to use (authenticated/connected)
        /// </summary>
        bool IsConnected { get; }
    }
}
