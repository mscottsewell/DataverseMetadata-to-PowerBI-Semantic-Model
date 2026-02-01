// =============================================================================
// DataverseClientAdapter.cs
// =============================================================================
// Purpose: Provides Dataverse connectivity for standalone applications using
//          Microsoft Authentication Library (MSAL) and HTTP/REST API calls.
//
// This adapter implements IDataverseConnection and is designed for scenarios
// where the application runs independently (not hosted in XrmToolBox).
//
// Authentication Flow:
//   1. Uses MSAL Public Client Application flow
//   2. First attempts silent token acquisition from cache
//   3. Falls back to interactive login if needed (browser-based OAuth)
//   4. Stores token for subsequent API calls
//
// The Dataverse Web API (OData v4) is used for all data operations.
// API responses are JSON and parsed using Newtonsoft.Json.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using DataverseToPowerBI.Core.Models;
using DataverseToPowerBI.Core.Interfaces;

namespace DataverseToPowerBI.Core.Services
{
    /// <summary>
    /// Adapter that wraps the MSAL-based HTTP client for standalone applications.
    /// Implements <see cref="IDataverseConnection"/> for compatibility with shared business logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This adapter is used by the standalone Configurator application.
    /// It handles its own authentication via MSAL and communicates with Dataverse
    /// using the OData Web API.
    /// </para>
    /// <para>
    /// Key features:
    /// </para>
    /// <list type="bullet">
    ///   <item>Interactive OAuth authentication via browser</item>
    ///   <item>Token caching for seamless re-authentication</item>
    ///   <item>Batch processing for large metadata requests</item>
    ///   <item>5-minute HTTP timeout for long-running requests</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var adapter = new DataverseClientAdapter("https://myorg.crm.dynamics.com");
    /// await adapter.AuthenticateAsync();
    /// var solutions = await adapter.GetSolutionsAsync();
    /// </code>
    /// </example>
    public class DataverseClientAdapter : IDataverseConnection
    {
        #region Private Fields

        /// <summary>
        /// The base Dataverse environment URL (without trailing slash).
        /// </summary>
        private readonly string _environmentUrl;

        /// <summary>
        /// HTTP client configured for Dataverse Web API calls.
        /// Reused across all requests for connection pooling.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Tracks whether authentication has been completed.
        /// </summary>
        private bool _isConnected;

        #endregion

        #region Constants

        /// <summary>
        /// Azure AD application (client) ID for authentication.
        /// This is the well-known Dynamics 365 first-party app ID.
        /// </summary>
        private const string ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

        /// <summary>
        /// Azure AD authority URL for organizational (work/school) accounts.
        /// The "organizations" endpoint allows any Azure AD tenant.
        /// </summary>
        private const string Authority = "https://login.microsoftonline.com/organizations";

        #endregion

        #region Constructor

        /// <summary>
        /// Gets a value indicating whether the connection is authenticated and ready.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataverseClientAdapter"/> class.
        /// </summary>
        /// <param name="environmentUrl">
        /// The Dataverse environment URL (e.g., "https://myorg.crm.dynamics.com").
        /// Trailing slashes are automatically removed.
        /// </param>
        public DataverseClientAdapter(string environmentUrl)
        {
            _environmentUrl = environmentUrl.TrimEnd('/');
            
            // Configure HTTP client with Dataverse Web API base address
            // Use v9.2 of the API for latest features
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"{_environmentUrl}/api/data/v9.2/"),
                Timeout = TimeSpan.FromMinutes(5) // Allow time for large metadata requests
            };
            _isConnected = false;
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Authenticates to Dataverse using MSAL (Microsoft Authentication Library).
        /// </summary>
        /// <param name="clearCredentials">
        /// When true, clears cached tokens and forces interactive login.
        /// Useful when switching accounts or troubleshooting authentication issues.
        /// </param>
        /// <returns>The access token on successful authentication.</returns>
        /// <remarks>
        /// <para>
        /// Authentication flow:
        /// </para>
        /// <list type="number">
        ///   <item>Build a Public Client Application with the Dynamics 365 client ID</item>
        ///   <item>If clearCredentials is true, remove all cached accounts</item>
        ///   <item>Attempt silent token acquisition from cache</item>
        ///   <item>If silent fails (MsalUiRequiredException), prompt interactive login</item>
        ///   <item>Configure HTTP client with the bearer token and OData headers</item>
        /// </list>
        /// </remarks>
        public async Task<string> AuthenticateAsync(bool clearCredentials = false)
        {
            // Build the resource scope for Dataverse API access
            var resource = $"{_environmentUrl}/";
            var scopes = new[] { $"{resource}.default" };

            // Create MSAL public client application
            // Public client (no client secret) is appropriate for desktop apps
            var app = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(Authority)
                .WithRedirectUri("http://localhost") // Required for interactive auth
                .Build();

            // Clear cached credentials if requested (force re-authentication)
            if (clearCredentials)
            {
                var accounts = await app.GetAccountsAsync();
                foreach (var account in accounts)
                {
                    await app.RemoveAsync(account);
                }
            }

            AuthenticationResult? result;
            try
            {
                // Try silent token acquisition first (uses cached token if valid)
                var accounts = await app.GetAccountsAsync();
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                // Silent acquisition failed - need interactive login
                // Opens browser for user to sign in
                result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
            }

            // Configure HTTP client with the access token
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            
            // Add OData headers required by Dataverse Web API
            _httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            _httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _isConnected = true;
            return result.AccessToken;
        }

        #endregion

        #region Solution Operations

        /// <summary>
        /// Retrieves all visible solutions from the Dataverse environment.
        /// </summary>
        /// <returns>
        /// A list of solutions, ordered by friendly name.
        /// Includes solution ID, names, version, and managed status.
        /// </returns>
        public async Task<List<DataverseSolution>> GetSolutionsAsync()
        {
            // Query solutions entity with filter for visible solutions only
            // Order by friendlyname for consistent display
            var response = await _httpClient.GetStringAsync("solutions?$select=solutionid,uniquename,friendlyname,version,ismanaged,_publisherid_value,modifiedon&$filter=isvisible eq true&$orderby=friendlyname");
            var json = JObject.Parse(response);
            
            // Parse OData response - data is in "value" array
            return json["value"]!.Select(s => new DataverseSolution
            {
                SolutionId = s["solutionid"]!.ToString(),
                UniqueName = s["uniquename"]!.ToString(),
                FriendlyName = s["friendlyname"]?.ToString() ?? "",
                Version = s["version"]?.ToString(),
                IsManaged = s["ismanaged"]?.ToObject<bool>() ?? false,
                PublisherId = s["_publisherid_value"]?.ToString(),
                ModifiedOn = s["modifiedon"]?.ToObject<DateTime?>()
            }).ToList();
        }

        #endregion

        #region Table Operations

        /// <summary>
        /// Retrieves all tables (entities) belonging to a specific solution.
        /// </summary>
        /// <param name="solutionId">The GUID of the solution to query.</param>
        /// <returns>
        /// A list of tables in the solution, excluding Activity and Intersect entities.
        /// Returns empty list if no tables are found.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method uses a two-step process:
        /// </para>
        /// <list type="number">
        ///   <item>Query solutioncomponents to get entity metadata IDs in the solution</item>
        ///   <item>Batch query EntityDefinitions to get full table metadata</item>
        /// </list>
        /// <para>
        /// Batching (50 entities at a time) prevents URL length limits and timeout issues.
        /// Activity and Intersect entities are filtered out as they're typically not
        /// suitable for Power BI semantic models.
        /// </para>
        /// </remarks>
        public async Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId)
        {
            // Step 1: Get solution component records for entity type (componenttype = 1)
            var response = await _httpClient.GetStringAsync($"solutioncomponents?$filter=_solutionid_value eq {solutionId} and componenttype eq 1&$select=objectid");
            var json = JObject.Parse(response);
            var entityIds = json["value"]!.Select(c => c["objectid"]!.ToString()).ToList();

            if (!entityIds.Any()) return new List<TableInfo>();

            // Step 2: Fetch entity definitions in batches of 50
            // This prevents excessively long URLs and request timeouts
            var tables = new List<TableInfo>();
            var batchSize = 50;

            for (int i = 0; i < entityIds.Count; i += batchSize)
            {
                var batch = entityIds.Skip(i).Take(batchSize).ToList();
                
                // Build OData filter with OR conditions for each metadata ID
                var filter = string.Join(" or ", batch.Select(id => $"MetadataId eq {id}"));
                
                try
                {
                    response = await _httpClient.GetStringAsync($"EntityDefinitions?$filter={filter}&$select=LogicalName,SchemaName,DisplayName,ObjectTypeCode,PrimaryIdAttribute,PrimaryNameAttribute,IsActivity,IsIntersect,MetadataId");
                    json = JObject.Parse(response);
                    var entities = json["value"];

                    if (entities != null)
                    {
                        foreach (var entity in entities)
                        {
                            // Skip Activity entities (tasks, emails, etc.) - these have special handling
                            // Skip Intersect entities (N:N relationship tables) - not useful for reporting
                            if (entity["IsActivity"]?.ToObject<bool>() == true ||
                                entity["IsIntersect"]?.ToObject<bool>() == true)
                                continue;

                            tables.Add(new TableInfo
                            {
                                LogicalName = entity["LogicalName"]!.ToString(),
                                DisplayName = GetLocalizedLabel(entity["DisplayName"]) ?? entity["SchemaName"]?.ToString() ?? entity["LogicalName"]!.ToString(),
                                SchemaName = entity["SchemaName"]?.ToString(),
                                ObjectTypeCode = entity["ObjectTypeCode"]?.ToObject<int>() ?? 0,
                                PrimaryIdAttribute = entity["PrimaryIdAttribute"]?.ToString(),
                                PrimaryNameAttribute = entity["PrimaryNameAttribute"]?.ToString(),
                                MetadataId = entity["MetadataId"]?.ToString()
                            });
                        }
                    }
                }
                catch { } // Silently skip batch on error - some tables may still be accessible
            }

            // Return sorted by display name for consistent UI ordering
            return tables.OrderBy(t => t.DisplayName ?? t.LogicalName).ToList();
        }

        /// <summary>
        /// Retrieves detailed metadata for a specific table.
        /// </summary>
        /// <param name="logicalName">The logical name of the table (e.g., "account").</param>
        /// <returns>Table metadata including names and primary attributes.</returns>
        public async Task<TableMetadata> GetTableMetadataAsync(string logicalName)
        {
            // Query EntityDefinitions using the logical name selector
            var response = await _httpClient.GetStringAsync($"EntityDefinitions(LogicalName='{logicalName}')?$select=LogicalName,DisplayName,SchemaName,PrimaryIdAttribute,PrimaryNameAttribute");
            var json = JObject.Parse(response);

            return new TableMetadata
            {
                LogicalName = json["LogicalName"]!.ToString(),
                DisplayName = GetLocalizedLabel(json["DisplayName"]) ?? json["SchemaName"]?.ToString() ?? json["LogicalName"]!.ToString(),
                SchemaName = json["SchemaName"]?.ToString(),
                PrimaryIdAttribute = json["PrimaryIdAttribute"]?.ToString(),
                PrimaryNameAttribute = json["PrimaryNameAttribute"]?.ToString()
            };
        }

        #endregion

        #region Attribute Operations

        /// <summary>
        /// Retrieves all attributes (columns) for a specific table.
        /// </summary>
        /// <param name="tableName">The logical name of the table.</param>
        /// <returns>
        /// A list of all attributes for the table, including type information
        /// and lookup targets. Sorted by display name.
        /// </returns>
        /// <remarks>
        /// The RequiredLevel property is checked to determine if the attribute is required.
        /// SystemRequired and ApplicationRequired levels are both treated as required.
        /// This information is used for referential integrity settings in relationships.
        /// </remarks>
        public async Task<List<AttributeMetadata>> GetAttributesAsync(string tableName)
        {
            // Use the Attributes navigation property to get all attributes for the entity
            var response = await _httpClient.GetStringAsync($"EntityDefinitions(LogicalName='{tableName}')/Attributes");
            var json = JObject.Parse(response);

            return json["value"]!.Select(a =>
            {
                // Check required level - SystemRequired and ApplicationRequired mean the field is required
                var requiredLevel = a["RequiredLevel"]?["Value"]?.ToString();
                var isRequired = requiredLevel == "SystemRequired" || requiredLevel == "ApplicationRequired";

                return new AttributeMetadata
                {
                    LogicalName = a["LogicalName"]!.ToString(),
                    DisplayName = GetLocalizedLabel(a["DisplayName"]) ?? a["SchemaName"]?.ToString() ?? a["LogicalName"]!.ToString(),
                    SchemaName = a["SchemaName"]?.ToString(),
                    AttributeType = a["AttributeType"]?.ToString(),
                    IsCustomAttribute = a["IsCustomAttribute"]?.ToObject<bool>() ?? false,
                    IsRequired = isRequired,
                    Targets = a["Targets"]?.ToObject<List<string>>() // Lookup target tables
                };
            }).OrderBy(a => a.DisplayName ?? a.LogicalName).ToList();
        }

        #endregion

        #region Form Operations

        /// <summary>
        /// Retrieves all Main forms for a specific table.
        /// </summary>
        /// <param name="entityLogicalName">The logical name of the table.</param>
        /// <param name="includeXml">
        /// When true, includes the FormXML in the results.
        /// FormXML is parsed to extract the list of fields on the form.
        /// </param>
        /// <returns>A list of Main forms for the table.</returns>
        /// <remarks>
        /// Only Main forms (type eq 2) are returned. Other form types (Quick Create,
        /// Quick View, etc.) are not useful for determining the semantic model structure.
        /// </remarks>
        public async Task<List<FormMetadata>> GetFormsAsync(string entityLogicalName, bool includeXml = false)
        {
            // Build select list - optionally include formxml (expensive to retrieve)
            var selectFields = "formid,name";
            if (includeXml) selectFields += ",formxml";

            // Query systemforms for Main forms (type = 2) only
            var response = await _httpClient.GetStringAsync($"systemforms?$filter=objecttypecode eq '{entityLogicalName}' and type eq 2&$select={selectFields}&$orderby=name");
            var json = JObject.Parse(response);

            var forms = new List<FormMetadata>();
            foreach (var form in json["value"]!)
            {
                var formData = new FormMetadata
                {
                    FormId = form["formid"]!.ToString(),
                    Name = form["name"]!.ToString(),
                    FormXml = form["formxml"]?.ToString()
                };

                // If FormXML was included, parse it to extract field names
                if (!string.IsNullOrEmpty(formData.FormXml))
                {
                    formData.Fields = ExtractFieldsFromFormXml(formData.FormXml);
                }

                forms.Add(formData);
            }

            return forms;
        }

        /// <summary>
        /// Retrieves the FormXML for a specific form by its ID.
        /// </summary>
        /// <param name="formId">The GUID of the form.</param>
        /// <returns>The FormXML string, or null if not found or on error.</returns>
        public async Task<string?> GetFormXmlAsync(string formId)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"systemforms({formId})?$select=formxml");
                var json = JObject.Parse(response);
                return json["formxml"]?.ToString();
            }
            catch
            {
                return null; // Form not found or access denied
            }
        }

        #endregion

        #region View Operations

        /// <summary>
        /// Retrieves all views (saved queries) for a specific table.
        /// </summary>
        /// <param name="entityLogicalName">The logical name of the table.</param>
        /// <param name="includeFetchXml">
        /// When true, includes the FetchXML query in the results.
        /// FetchXML is converted to SQL for the semantic model.
        /// </param>
        /// <returns>A list of views for the table.</returns>
        /// <remarks>
        /// Only active views (statecode eq 0) with querytype eq 0 (public views) are returned.
        /// System views and personal views are excluded.
        /// </remarks>
        public async Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false)
        {
            // Build select list - optionally include fetchxml
            var selectFields = "savedqueryid,name,isdefault,querytype";
            if (includeFetchXml) selectFields += ",fetchxml";

            // Query savedqueries for public views (active, querytype = 0)
            var response = await _httpClient.GetStringAsync($"savedqueries?$filter=returnedtypecode eq '{entityLogicalName}' and statecode eq 0&$select={selectFields}&$orderby=name");
            var json = JObject.Parse(response);

            var views = new List<ViewMetadata>();
            foreach (var view in json["value"]!)
            {
                // Only include public views (querytype = 0)
                // Skip other query types (Associated Views, Quick Find, etc.)
                if (view["querytype"]?.ToObject<int>() != 0)
                    continue;

                views.Add(new ViewMetadata
                {
                    ViewId = view["savedqueryid"]!.ToString(),
                    Name = view["name"]!.ToString(),
                    IsDefault = view["isdefault"]?.ToObject<bool>() ?? false,
                    FetchXml = view["fetchxml"]?.ToString()
                });
            }

            return views;
        }

        /// <summary>
        /// Retrieves the FetchXML for a specific view by its ID.
        /// </summary>
        /// <param name="viewId">The GUID of the view.</param>
        /// <returns>The FetchXML string, or null if not found or on error.</returns>
        public async Task<string?> GetViewFetchXmlAsync(string viewId)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"savedqueries({viewId})?$select=fetchxml");
                var json = JObject.Parse(response);
                return json["fetchxml"]?.ToString();
            }
            catch
            {
                return null; // View not found or access denied
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the environment URL for this connection.
        /// </summary>
        /// <returns>The base Dataverse environment URL.</returns>
        public string GetEnvironmentUrl()
        {
            return _environmentUrl;
        }

        /// <summary>
        /// Extracts the localized label from a Dataverse DisplayName property.
        /// </summary>
        /// <param name="token">The DisplayName JSON token from the API response.</param>
        /// <returns>The user's localized label, or null if not available.</returns>
        /// <remarks>
        /// Dataverse stores localized labels in a structure like:
        /// { "UserLocalizedLabel": { "Label": "Display Name" } }
        /// </remarks>
        private static string? GetLocalizedLabel(JToken? token)
        {
            try
            {
                if (token == null || token.Type == JTokenType.Null)
                    return null;
                
                if (token is JObject obj)
                {
                    // Get the user's localized label (based on their language settings)
                    var userLabel = obj["UserLocalizedLabel"];
                    if (userLabel is JObject labelObj)
                    {
                        return labelObj["Label"]?.ToString();
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts field logical names from FormXML.
        /// </summary>
        /// <param name="formXml">The FormXML string to parse.</param>
        /// <returns>
        /// A sorted, deduplicated list of field logical names found in the form.
        /// </returns>
        /// <remarks>
        /// FormXML contains control elements with a "datafieldname" attribute
        /// that indicates which field the control is bound to. This method
        /// extracts all unique field names from the form definition.
        /// </remarks>
        private static List<string> ExtractFieldsFromFormXml(string formXml)
        {
            // Use HashSet for automatic deduplication (same field may appear multiple times)
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var doc = XDocument.Parse(formXml);
                
                // Find all control elements and extract their datafieldname attribute
                foreach (var control in doc.Descendants("control"))
                {
                    var fieldName = control.Attribute("datafieldname")?.Value;
                    if (!string.IsNullOrEmpty(fieldName))
                        fields.Add(fieldName.ToLower()); // Normalize to lowercase
                }
            }
            catch { } // Ignore XML parsing errors - return whatever fields we found

            return fields.OrderBy(f => f).ToList();
        }

        #endregion
    }
}
