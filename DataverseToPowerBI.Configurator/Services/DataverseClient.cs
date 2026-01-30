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
    public class DataverseClient
    {
        private readonly string _environmentUrl;
        private readonly HttpClient _httpClient;
        
        private const string ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
        private const string Authority = "https://login.microsoftonline.com/organizations";

        public DataverseClient(string environmentUrl)
        {
            _environmentUrl = environmentUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"{_environmentUrl}/api/data/v9.2/"),
                Timeout = TimeSpan.FromMinutes(5)
            };
        }

        public async Task<string> AuthenticateAsync()
        {
            var resource = $"{_environmentUrl}/";
            var scopes = new[] { $"{resource}.default" };

            var app = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(Authority)
                .WithRedirectUri("http://localhost")
                .Build();

            AuthenticationResult? result;
            try
            {
                var accounts = await app.GetAccountsAsync();
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            _httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            _httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return result.AccessToken;
        }

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

        public async Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId)
        {
            var response = await _httpClient.GetStringAsync($"solutioncomponents?$filter=_solutionid_value eq {solutionId} and componenttype eq 1&$select=objectid");
            var json = JObject.Parse(response);
            var entityIds = json["value"]!.Select(c => c["objectid"]!.ToString()).ToList();

            if (!entityIds.Any()) return new List<TableInfo>();

            // Fetch entities in batches of 50
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
                            // Skip system tables
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
                catch { }
            }

            return tables.OrderBy(t => t.DisplayName ?? t.LogicalName).ToList();
        }

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

                if (!string.IsNullOrEmpty(formData.FormXml))
                {
                    formData.Fields = ExtractFieldsFromFormXml(formData.FormXml);
                }

                forms.Add(formData);
            }

            return forms;
        }

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

        public async Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false)
        {
            var selectFields = "savedqueryid,name,isdefault,querytype";
            if (includeFetchXml) selectFields += ",fetchxml";

            var response = await _httpClient.GetStringAsync($"savedqueries?$filter=returnedtypecode eq '{entityLogicalName}' and statecode eq 0&$select={selectFields}&$orderby=name");
            var json = JObject.Parse(response);

            var views = new List<ViewMetadata>();
            foreach (var view in json["value"]!)
            {
                // Filter to public views (querytype 0)
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

        public async Task<List<AttributeMetadata>> GetAttributesAsync(string tableName)
        {
            // Note: Targets is a property on LookupAttributeMetadata - don't use $select to allow all properties
            var response = await _httpClient.GetStringAsync($"EntityDefinitions(LogicalName='{tableName}')/Attributes");
            var json = JObject.Parse(response);

            return json["value"]!.Select(a => new AttributeMetadata
            {
                LogicalName = a["LogicalName"]!.ToString(),
                DisplayName = GetLocalizedLabel(a["DisplayName"]) ?? a["SchemaName"]?.ToString() ?? a["LogicalName"]!.ToString(),
                SchemaName = a["SchemaName"]?.ToString(),
                AttributeType = a["AttributeType"]?.ToString(),
                IsCustomAttribute = a["IsCustomAttribute"]?.ToObject<bool>() ?? false,
                Targets = a["Targets"]?.ToObject<List<string>>()  // Lookup target tables
            }).OrderBy(a => a.DisplayName ?? a.LogicalName).ToList();
        }

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
            catch { }

            return fields.OrderBy(f => f).ToList();
        }
    }
}
