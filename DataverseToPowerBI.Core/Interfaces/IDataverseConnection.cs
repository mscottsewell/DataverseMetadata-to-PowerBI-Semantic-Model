// =============================================================================
// IDataverseConnection.cs
// =============================================================================
// Purpose: Defines the contract for connecting to Microsoft Dataverse.
// 
// This interface provides an abstraction layer that allows the application to
// work with different authentication methods:
//   - MSAL (Microsoft Authentication Library) for standalone applications
//   - IOrganizationService for XrmToolBox plugin hosting
//
// The interface exposes methods to retrieve Dataverse metadata including:
//   - Solutions and their components
//   - Table (entity) definitions and attributes
//   - Forms and Views with their XML definitions
//
// By coding against this interface, business logic can be shared between
// the standalone Configurator app and the XrmToolBox plugin.
// =============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using DataverseToPowerBI.Core.Models;

namespace DataverseToPowerBI.Core.Interfaces
{
    /// <summary>
    /// Abstraction for connecting to Microsoft Dataverse.
    /// Supports both MSAL-based authentication (standalone apps) and 
    /// IOrganizationService (XrmToolBox plugin hosting).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface is the foundation for the adapter pattern used in this project.
    /// Two concrete implementations exist:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>DataverseClientAdapter</term>
    ///     <description>Uses MSAL and HTTP/REST API calls for standalone applications</description>
    ///   </item>
    ///   <item>
    ///     <term>XrmServiceAdapterImpl</term>
    ///     <description>Uses IOrganizationService SDK for XrmToolBox plugins</description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // For standalone applications:
    /// IDataverseConnection connection = new DataverseClientAdapter("https://myorg.crm.dynamics.com");
    /// await connection.AuthenticateAsync();
    /// var solutions = await connection.GetSolutionsAsync();
    /// 
    /// // For XrmToolBox:
    /// IDataverseConnection connection = new XrmServiceAdapterImpl(organizationService, environmentUrl);
    /// // No authentication needed - XrmToolBox handles it
    /// var solutions = await connection.GetSolutionsAsync();
    /// </code>
    /// </example>
    public interface IDataverseConnection
    {
        /// <summary>
        /// Authenticates to the Dataverse environment.
        /// </summary>
        /// <param name="clearCredentials">
        /// When true, forces re-authentication by clearing cached tokens.
        /// Useful when switching users or environments.
        /// </param>
        /// <returns>
        /// The access token string on successful authentication.
        /// For XrmToolBox implementations, returns a placeholder string since
        /// authentication is handled externally by the XrmToolBox framework.
        /// </returns>
        /// <remarks>
        /// MSAL implementations will:
        /// <list type="bullet">
        ///   <item>First attempt silent token acquisition from cache</item>
        ///   <item>Fall back to interactive login if needed</item>
        ///   <item>Store the token for subsequent API calls</item>
        /// </list>
        /// </remarks>
        Task<string> AuthenticateAsync(bool clearCredentials = false);

        /// <summary>
        /// Retrieves all visible solutions from the Dataverse environment.
        /// </summary>
        /// <returns>
        /// A list of <see cref="DataverseSolution"/> objects containing solution metadata
        /// such as unique name, friendly name, version, and whether it's managed.
        /// </returns>
        /// <remarks>
        /// Only visible solutions are returned (isvisible eq true).
        /// Results are ordered by friendly name for display purposes.
        /// </remarks>
        Task<List<DataverseSolution>> GetSolutionsAsync();

        /// <summary>
        /// Retrieves all tables (entities) that belong to a specific solution.
        /// </summary>
        /// <param name="solutionId">
        /// The GUID of the solution to retrieve tables from.
        /// </param>
        /// <returns>
        /// A list of <see cref="TableInfo"/> objects containing table metadata.
        /// </returns>
        /// <remarks>
        /// Filters out Activity and Intersect entities as they are typically
        /// not suitable for Power BI semantic models.
        /// </remarks>
        Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId);

        /// <summary>
        /// Retrieves detailed metadata for a specific table.
        /// </summary>
        /// <param name="logicalName">
        /// The logical name of the table (e.g., "account", "contact").
        /// </param>
        /// <returns>
        /// A <see cref="TableMetadata"/> object containing the table's display name,
        /// schema name, and primary key/name attributes.
        /// </returns>
        Task<TableMetadata> GetTableMetadataAsync(string logicalName);

        /// <summary>
        /// Retrieves all attributes (columns) for a specific table.
        /// </summary>
        /// <param name="tableName">
        /// The logical name of the table to retrieve attributes from.
        /// </param>
        /// <returns>
        /// A list of <see cref="AttributeMetadata"/> objects containing attribute details
        /// such as data type, display name, and whether it's a required field.
        /// </returns>
        Task<List<AttributeMetadata>> GetAttributesAsync(string tableName);

        /// <summary>
        /// Retrieves all Main forms for a specific table.
        /// </summary>
        /// <param name="entityLogicalName">
        /// The logical name of the table to retrieve forms from.
        /// </param>
        /// <param name="includeXml">
        /// When true, includes the FormXML in the results.
        /// FormXML is used to extract field names displayed on the form.
        /// </param>
        /// <returns>
        /// A list of <see cref="FormMetadata"/> objects representing the table's forms.
        /// </returns>
        Task<List<FormMetadata>> GetFormsAsync(string entityLogicalName, bool includeXml = false);

        /// <summary>
        /// Retrieves the FormXML for a specific form by its ID.
        /// </summary>
        /// <param name="formId">The GUID of the form to retrieve.</param>
        /// <returns>
        /// The FormXML string, or null if the form is not found.
        /// </returns>
        /// <remarks>
        /// FormXML contains the layout and field definitions for the form.
        /// This is parsed to extract the list of fields displayed on the form.
        /// </remarks>
        Task<string?> GetFormXmlAsync(string formId);

        /// <summary>
        /// Retrieves all views (saved queries) for a specific table.
        /// </summary>
        /// <param name="entityLogicalName">
        /// The logical name of the table to retrieve views from.
        /// </param>
        /// <param name="includeFetchXml">
        /// When true, includes the FetchXML query definition in the results.
        /// FetchXML is used to generate SQL-like queries for the semantic model.
        /// </param>
        /// <returns>
        /// A list of <see cref="ViewMetadata"/> objects representing the table's views.
        /// </returns>
        Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false);

        /// <summary>
        /// Retrieves the FetchXML query for a specific view.
        /// </summary>
        /// <param name="viewId">The GUID of the view to retrieve.</param>
        /// <returns>
        /// The FetchXML query string, or null if the view is not found.
        /// </returns>
        /// <remarks>
        /// FetchXML is a proprietary query language used by Dataverse.
        /// This project converts FetchXML to SQL-like expressions for Power BI.
        /// </remarks>
        Task<string?> GetViewFetchXmlAsync(string viewId);

        /// <summary>
        /// Gets the URL of the connected Dataverse environment.
        /// </summary>
        /// <returns>
        /// The base URL of the Dataverse environment (e.g., "https://myorg.crm.dynamics.com").
        /// </returns>
        /// <remarks>
        /// Used for display purposes, logging, and cache validation.
        /// </remarks>
        string GetEnvironmentUrl();

        /// <summary>
        /// Gets a value indicating whether the connection is ready for use.
        /// </summary>
        /// <value>
        /// <c>true</c> if authenticated and ready to make API calls; otherwise, <c>false</c>.
        /// </value>
        bool IsConnected { get; }
    }
}
