// =============================================================================
// DataverseClient.cs - Dataverse Web API Client for Configurator
// =============================================================================
// Purpose: Provides HTTP-based access to the Dataverse Web API for the 
// standalone Configurator WinForms application.
//
// Authentication:
//   Uses MSAL (Microsoft.Identity.Client) with interactive browser login.
//   The client ID is registered for delegated permissions to Dataverse.
//   Token caching is handled by MSAL; silent token renewal is attempted first.
//
// API Endpoints Used:
//   - solutions: List available solutions
//   - solutioncomponents: Get entities in a solution
//   - EntityDefinitions: Table and attribute metadata
//   - systemforms: Form definitions (Main forms, type=2)
//   - savedqueries: View definitions (public views, querytype=0)
//
// Similar Implementation:
//   This class is similar to Core/Services/DataverseClientAdapter.cs but is
//   specific to the Configurator project. The Core version implements the
//   IDataverseConnection interface for abstraction.
//
// Note: This class uses HttpClient directly rather than the Dataverse SDK
// for broader compatibility and simpler deployment (no SDK dependencies).
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
using DataverseToPowerBI.Configurator.Models;

namespace DataverseToPowerBI.Configurator.Services
{
    /// <summary>
    /// HTTP client for Dataverse Web API operations in the Configurator application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client provides authenticated access to Dataverse to retrieve:
    /// </para>
    /// <list type="bullet">
    ///   <item>Solutions and solution components</item>
    ///   <item>Table (entity) metadata and attributes</item>
    ///   <item>Form definitions and field layouts</item>
    ///   <item>View definitions and FetchXML queries</item>
    /// </list>
    /// <para>
    /// Authentication uses OAuth 2.0 with interactive login via MSAL.
    /// Uses the "organizations" authority for multi-tenant support.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var client = new DataverseClient("https://org.crm.dynamics.com");
    /// await client.AuthenticateAsync();
    /// var solutions = await client.GetSolutionsAsync();
    /// var tables = await client.GetSolutionTablesAsync(solutions[0].SolutionId);
    /// </code>
    /// </example>
    public class DataverseClient
    {
        #region Private Fields

        /// <summary>
        /// The Dataverse environment base URL (without trailing slash).
        /// </summary>
        private readonly string _environmentUrl;

        /// <summary>
        /// HTTP client configured for Dataverse Web API calls.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Azure AD application (client) ID for authentication.
        /// This is the "Dataverse Metadata Extractor" app registration.
        /// </summary>
        private const string ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

        /// <summary>
        /// Azure AD authority for multi-tenant authentication.
        /// Uses "organizations" to allow any Azure AD tenant.
        /// </summary>
        private const string Authority = "https://login.microsoftonline.com/organizations";

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new Dataverse client for the specified environment.
        /// </summary>
        /// <param name="environmentUrl">
        /// The Dataverse environment URL (e.g., "https://org.crm.dynamics.com").
        /// </param>
        public DataverseClient(string environmentUrl)
        {
            _environmentUrl = environmentUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"{_environmentUrl}/api/data/v9.2/"),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Authenticates to Dataverse using interactive browser login.
        /// </summary>
        /// <param name="clearCredentials">
        /// When true, clears cached tokens and forces re-authentication.
        /// Useful for switching users or resolving auth issues.
        /// </param>
        /// <returns>The access token for API calls.</returns>
        /// <remarks>
        /// <para>Authentication flow:</para>
        /// <list type="number">
        ///   <item>Attempt silent token acquisition from cache</item>
        ///   <item>If cache miss/expired, launch interactive browser login</item>
        ///   <item>Configure HttpClient with bearer token and OData headers</item>
        /// </list>
        /// </remarks>
        public async Task<string> AuthenticateAsync(bool clearCredentials = false)
        {
            var resource = $"{_environmentUrl}/";
            var scopes = new[] { $"{resource}.default" };

            var app = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(Authority)
                .WithRedirectUri("http://localhost")
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
                // Try silent acquisition first (from cache)
                var accounts = await app.GetAccountsAsync();
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                // Cache miss or token expired - launch interactive login
                result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
            }

            // Configure HttpClient with auth token and OData headers
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            _httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            _httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return result.AccessToken;
        }

        #endregion

        #region Solution Methods

        /// <summary>
        /// Retrieves all visible solutions in the environment.
        /// </summary>
        /// <returns>List of solutions ordered by friendly name.</returns>
        /// <remarks>
        /// Filters to visible solutions only (excludes system solutions).
        /// Returns solution metadata including version and managed status.
        /// </remarks>
        public async Task<List<DataverseSolution>> GetSolutionsAsync()
        {
            var response = await _httpClient.GetStringAsync("solutions?$select=solutionid,uniquename,friendlyname,version,ismanaged,_publisherid_value,modifiedon&$filter=isvisible eq true&$orderby=friendlyname");
            var json = JObject.Parse(response);
            
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

        /// <summary>
        /// Retrieves all tables (entities) that are components of a solution.
        /// </summary>
        /// <param name="solutionId">GUID of the solution to query.</param>
        /// <returns>List of tables ordered by display name.</returns>
        /// <remarks>
        /// <para>
        /// Two-phase operation:
        /// </para>
        /// <list type="number">
        ///   <item>Query solutioncomponents for entities (componenttype=1)</item>
        ///   <item>Batch-fetch EntityDefinitions metadata (50 at a time)</item>
        /// </list>
        /// <para>
        /// Excludes Activity and Intersect entities (system tables).
        /// </para>
        /// </remarks>
        public async Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId)
        {
            // Phase 1: Get entity IDs from solution components
            var response = await _httpClient.GetStringAsync($"solutioncomponents?$filter=_solutionid_value eq {solutionId} and componenttype eq 1&$select=objectid");
            var json = JObject.Parse(response);
            var entityIds = json["value"]!.Select(c => c["objectid"]!.ToString()).ToList();

            if (!entityIds.Any()) return new List<TableInfo>();

            // Phase 2: Fetch entity metadata in batches of 50
            var tables = new List<TableInfo>();
            var batchSize = 50;

            for (int i = 0; i < entityIds.Count; i += batchSize)
            {
                var batch = entityIds.Skip(i).Take(batchSize).ToList();
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
                            // Skip system tables (Activities and Intersect/relationship tables)
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
                catch { /* Skip batch on error */ }
            }

            return tables.OrderBy(t => t.DisplayName ?? t.LogicalName).ToList();
        }

        #endregion

        #region Form Methods

        /// <summary>
        /// Retrieves Main forms for a table.
        /// </summary>
        /// <param name="entityLogicalName">Table logical name.</param>
        /// <param name="includeXml">Whether to include FormXML in response.</param>
        /// <returns>List of Main forms (type=2) ordered by name.</returns>
        /// <remarks>
        /// Main forms (type=2) are used for record editing. When includeXml is true,
        /// the form field list is extracted from the FormXML for column selection.
        /// </remarks>
        public async Task<List<FormMetadata>> GetFormsAsync(string entityLogicalName, bool includeXml = false)
        {
            var selectFields = "formid,name";
            if (includeXml) selectFields += ",formxml";

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

                // Extract field names from FormXML if available
                if (!string.IsNullOrEmpty(formData.FormXml))
                {
                    formData.Fields = ExtractFieldsFromFormXml(formData.FormXml);
                }

                forms.Add(formData);
            }

            return forms;
        }

        /// <summary>
        /// Retrieves the FormXML for a specific form.
        /// </summary>
        /// <param name="formId">GUID of the form.</param>
        /// <returns>FormXML string, or null if not found.</returns>
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
                return null;
            }
        }

        #endregion

        #region View Methods

        /// <summary>
        /// Retrieves public views for a table.
        /// </summary>
        /// <param name="entityLogicalName">Table logical name.</param>
        /// <param name="includeFetchXml">Whether to include FetchXML in response.</param>
        /// <returns>List of public views (querytype=0) ordered by name.</returns>
        /// <remarks>
        /// Public views (querytype=0) are user-visible views. FetchXML is used
        /// to extract filter conditions for Power BI partition queries.
        /// </remarks>
        public async Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false)
        {
            var selectFields = "savedqueryid,name,isdefault,querytype";
            if (includeFetchXml) selectFields += ",fetchxml";

            var response = await _httpClient.GetStringAsync($"savedqueries?$filter=returnedtypecode eq '{entityLogicalName}' and statecode eq 0&$select={selectFields}&$orderby=name");
            var json = JObject.Parse(response);

            var views = new List<ViewMetadata>();
            foreach (var view in json["value"]!)
            {
                // Filter to public views only (querytype 0)
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
        /// Retrieves the FetchXML for a specific view.
        /// </summary>
        /// <param name="viewId">GUID of the view.</param>
        /// <returns>FetchXML string, or null if not found.</returns>
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
                return null;
            }
        }

        #endregion

        #region Metadata Methods

        /// <summary>
        /// Retrieves metadata for a single table.
        /// </summary>
        /// <param name="logicalName">Logical name of the table.</param>
        /// <returns>Table metadata including display name and key attributes.</returns>
        public async Task<TableMetadata> GetTableMetadataAsync(string logicalName)
        {
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

        /// <summary>
        /// Retrieves all attributes (columns) for a table.
        /// </summary>
        /// <param name="tableName">Logical name of the table.</param>
        /// <returns>List of attributes ordered by display name.</returns>
        /// <remarks>
        /// Does not use $select to ensure Lookup target information is included.
        /// The Targets property is only present on LookupAttributeMetadata.
        /// </remarks>
        public async Task<List<AttributeMetadata>> GetAttributesAsync(string tableName)
        {
            // Note: Don't use $select - Targets is a property on LookupAttributeMetadata only
            var response = await _httpClient.GetStringAsync($"EntityDefinitions(LogicalName='{tableName}')/Attributes");
            var json = JObject.Parse(response);

            return json["value"]!.Select(a =>
            {
                // Parse RequiredLevel - can be None, SystemRequired, ApplicationRequired, or Recommended
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
                    Targets = a["Targets"]?.ToObject<List<string>>()  // Lookup target tables
                };
            }).OrderBy(a => a.DisplayName ?? a.LogicalName).ToList();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Extracts the localized label from a Dataverse DisplayName property.
        /// </summary>
        /// <param name="token">The DisplayName JSON token.</param>
        /// <returns>The user's localized label, or null if not available.</returns>
        /// <remarks>
        /// Dataverse returns complex Label objects with UserLocalizedLabel
        /// containing the label in the current user's language.
        /// </remarks>
        private static string? GetLocalizedLabel(JToken? token)
        {
            try
            {
                if (token == null || token.Type == JTokenType.Null)
                    return null;
                
                if (token is JObject obj)
                {
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
        /// Extracts field names from a Form XML document.
        /// </summary>
        /// <param name="formXml">The FormXML string.</param>
        /// <returns>
        /// Sorted list of unique field logical names found on the form.
        /// </returns>
        /// <remarks>
        /// Parses the FormXML and finds all control elements with datafieldname
        /// attributes. This represents the fields visible on the form.
        /// </remarks>
        public static List<string> ExtractFieldsFromFormXml(string formXml)
        {
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var doc = XDocument.Parse(formXml);
                foreach (var control in doc.Descendants("control"))
                {
                    var fieldName = control.Attribute("datafieldname")?.Value;
                    if (!string.IsNullOrEmpty(fieldName))
                        fields.Add(fieldName.ToLower());
                }
            }
            catch { /* Ignore XML parsing errors */ }

            return fields.OrderBy(f => f).ToList();
        }

        #endregion
    }
}
