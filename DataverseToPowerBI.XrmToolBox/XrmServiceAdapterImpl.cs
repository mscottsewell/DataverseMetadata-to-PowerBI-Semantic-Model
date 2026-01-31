using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Messages;
using DataverseToPowerBI.Core.Models;
using DataverseToPowerBI.Core.Interfaces;

// Alias to avoid conflict with SDK AttributeMetadata
using CoreAttributeMetadata = DataverseToPowerBI.Core.Models.AttributeMetadata;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// XrmToolBox-specific implementation of IDataverseConnection using IOrganizationService
    /// This implementation uses the Dataverse SDK for all operations
    /// </summary>
    public class XrmServiceAdapterImpl : IDataverseConnection
    {
        private readonly IOrganizationService _service;
        private readonly string _environmentUrl;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Creates adapter from IOrganizationService (provided by XrmToolBox)
        /// </summary>
        public XrmServiceAdapterImpl(IOrganizationService service, string environmentUrl)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _environmentUrl = environmentUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(environmentUrl));
            _isConnected = service != null;
        }

        public string GetEnvironmentUrl() => _environmentUrl;

        /// <summary>
        /// XrmToolBox handles authentication externally - this is a no-op
        /// </summary>
        public Task<string> AuthenticateAsync(bool clearCredentials = false)
        {
            return Task.FromResult("XrmToolBox-Managed");
        }

        public Task<List<DataverseSolution>> GetSolutionsAsync()
        {
            return Task.FromResult(GetSolutionsSync(_service));
        }

        /// <summary>
        /// Synchronous version for WorkAsync pattern
        /// </summary>
        public List<DataverseSolution> GetSolutionsSync(IOrganizationService service)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version", "ismanaged", "publisherid", "modifiedon"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("isvisible", ConditionOperator.Equal, true)
                    }
                },
                Orders = { new OrderExpression("friendlyname", OrderType.Ascending) }
            };

            var results = service.RetrieveMultiple(query);
            
            return results.Entities.Select(e => new DataverseSolution
            {
                SolutionId = e.GetAttributeValue<Guid>("solutionid").ToString(),
                UniqueName = e.GetAttributeValue<string>("uniquename") ?? "",
                FriendlyName = e.GetAttributeValue<string>("friendlyname") ?? e.GetAttributeValue<string>("uniquename") ?? "",
                Version = e.GetAttributeValue<string>("version") ?? "",
                IsManaged = e.GetAttributeValue<bool>("ismanaged"),
                PublisherId = e.GetAttributeValue<EntityReference>("publisherid")?.Id.ToString(),
                ModifiedOn = e.GetAttributeValue<DateTime?>("modifiedon")
            }).ToList();
        }

        public Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId)
        {
            return Task.FromResult(GetSolutionTablesSync(_service, solutionId));
        }

        /// <summary>
        /// Synchronous version for WorkAsync pattern
        /// </summary>
        public List<TableInfo> GetSolutionTablesSync(IOrganizationService service, string solutionId)
        {
            // Query solution components for entities (component type 1)
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.Equal, new Guid(solutionId)),
                        new ConditionExpression("componenttype", ConditionOperator.Equal, 1) // Entity = 1
                    }
                }
            };

            var results = service.RetrieveMultiple(query);
            var entityIds = results.Entities
                .Select(e => e.GetAttributeValue<Guid>("objectid"))
                .Where(id => id != Guid.Empty)
                .ToList();

            if (entityIds.Count == 0)
                return new List<TableInfo>();

            // Query entity metadata for these entities
            var tables = new List<TableInfo>();
            
            // Use RetrieveAllEntitiesRequest
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };
            
            var response = (RetrieveAllEntitiesResponse)service.Execute(request);
            
            if (response.EntityMetadata != null)
            {
                foreach (var item in response.EntityMetadata)
                {
                    var metadataId = item.MetadataId;
                    
                    if (metadataId.HasValue && entityIds.Contains(metadataId.Value))
                    {
                        string displayName = item.LogicalName;
                        if (item.DisplayName?.UserLocalizedLabel != null)
                        {
                            displayName = item.DisplayName.UserLocalizedLabel.Label ?? item.LogicalName;
                        }
                        
                        tables.Add(new TableInfo
                        {
                            LogicalName = item.LogicalName ?? "",
                            DisplayName = displayName,
                            SchemaName = item.SchemaName,
                            PrimaryIdAttribute = item.PrimaryIdAttribute,
                            PrimaryNameAttribute = item.PrimaryNameAttribute,
                            ObjectTypeCode = item.ObjectTypeCode ?? 0,
                            MetadataId = metadataId.Value.ToString()
                        });
                    }
                }
            }

            return tables.OrderBy(t => t.DisplayName ?? t.LogicalName).ToList();
        }

        public Task<TableMetadata> GetTableMetadataAsync(string logicalName)
        {
            return Task.FromResult(GetTableMetadataSync(_service, logicalName));
        }

        public TableMetadata GetTableMetadataSync(IOrganizationService service, string logicalName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };
            
            var response = (RetrieveEntityResponse)service.Execute(request);
            var metadata = response.EntityMetadata;
            
            string displayName = logicalName;
            if (metadata.DisplayName?.UserLocalizedLabel != null)
            {
                displayName = metadata.DisplayName.UserLocalizedLabel.Label ?? logicalName;
            }
            
            return new TableMetadata
            {
                LogicalName = logicalName,
                DisplayName = displayName,
                SchemaName = metadata.SchemaName,
                PrimaryIdAttribute = metadata.PrimaryIdAttribute,
                PrimaryNameAttribute = metadata.PrimaryNameAttribute
            };
        }

        public Task<List<CoreAttributeMetadata>> GetAttributesAsync(string tableName)
        {
            return Task.FromResult(GetAttributesSync(_service, tableName));
        }

        /// <summary>
        /// Synchronous version for WorkAsync pattern
        /// </summary>
        public List<CoreAttributeMetadata> GetAttributesSync(IOrganizationService service, string tableName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = tableName,
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = true
            };
            
            var response = (RetrieveEntityResponse)service.Execute(request);
            var entityMetadata = response.EntityMetadata;
            
            var attributes = new List<CoreAttributeMetadata>();
            
            if (entityMetadata?.Attributes != null)
            {
                foreach (var attr in entityMetadata.Attributes)
                {
                    var logicalName = attr.LogicalName;
                    var schemaName = attr.SchemaName;
                    var isCustomAttribute = attr.IsCustomAttribute ?? false;
                    var typeValue = attr.AttributeTypeName?.Value;
                    
                    // Get display name
                    string displayName = logicalName ?? "";
                    if (attr.DisplayName?.UserLocalizedLabel != null)
                    {
                        displayName = attr.DisplayName.UserLocalizedLabel.Label ?? logicalName ?? "";
                    }
                    
                    // Check if required
                    bool isRequired = false;
                    if (attr.RequiredLevel?.Value != null)
                    {
                        var level = attr.RequiredLevel.Value;
                        isRequired = level == AttributeRequiredLevel.SystemRequired || 
                                     level == AttributeRequiredLevel.ApplicationRequired;
                    }
                    
                    // Get targets for lookup attributes
                    List<string> targets = null;
                    if (attr is LookupAttributeMetadata lookupAttr)
                    {
                        if (lookupAttr.Targets != null && lookupAttr.Targets.Length > 0)
                        {
                            targets = lookupAttr.Targets.ToList();
                        }
                    }
                    
                    attributes.Add(new CoreAttributeMetadata
                    {
                        LogicalName = logicalName ?? "",
                        DisplayName = displayName,
                        SchemaName = schemaName ?? "",
                        AttributeType = MapAttributeType(typeValue ?? ""),
                        IsCustomAttribute = isCustomAttribute,
                        IsRequired = isRequired,
                        Targets = targets
                    });
                }
            }
            
            return attributes.OrderBy(a => a.DisplayName).ToList();
        }

        private string MapAttributeType(string sdkType)
        {
            switch (sdkType)
            {
                case "StringType": return "String";
                case "MemoType": return "Memo";
                case "IntegerType": return "Integer";
                case "BigIntType": return "BigInt";
                case "DoubleType": return "Double";
                case "DecimalType": return "Decimal";
                case "MoneyType": return "Money";
                case "BooleanType": return "Boolean";
                case "DateTimeType": return "DateTime";
                case "LookupType": return "Lookup";
                case "CustomerType": return "Customer";
                case "OwnerType": return "Owner";
                case "UniqueidentifierType": return "Uniqueidentifier";
                case "PicklistType": return "Picklist";
                case "StateType": return "State";
                case "StatusType": return "Status";
                case "EntityNameType": return "EntityName";
                case "ImageType": return "Image";
                case "FileType": return "File";
                case "VirtualType": return "Virtual";
                default: return sdkType;
            }
        }

        public Task<List<FormMetadata>> GetFormsAsync(string entityLogicalName, bool includeXml = false)
        {
            return Task.FromResult(GetFormsSync(_service, entityLogicalName, includeXml));
        }

        public List<FormMetadata> GetFormsSync(IOrganizationService service, string entityLogicalName, bool includeXml = false)
        {
            var columns = new ColumnSet("formid", "name", "formactivationstate", "formxml", "type");
            
            var query = new QueryExpression("systemform")
            {
                ColumnSet = columns,
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("objecttypecode", ConditionOperator.Equal, entityLogicalName),
                        new ConditionExpression("type", ConditionOperator.In, new[] { 2, 7 }), // Main and QuickView forms
                        new ConditionExpression("formactivationstate", ConditionOperator.Equal, 1) // Active only
                    }
                },
                Orders = { new OrderExpression("name", OrderType.Ascending) }
            };

            var results = service.RetrieveMultiple(query);
            
            return results.Entities.Select(e => 
            {
                var formXml = e.GetAttributeValue<string>("formxml");
                var fields = includeXml && !string.IsNullOrEmpty(formXml) 
                    ? ExtractFieldsFromFormXml(formXml) 
                    : new List<string>();
                
                return new FormMetadata
                {
                    FormId = e.GetAttributeValue<Guid>("formid").ToString(),
                    Name = e.GetAttributeValue<string>("name") ?? "",
                    FormXml = includeXml ? formXml : null,
                    Fields = fields
                };
            }).ToList();
        }

        public Task<string> GetFormXmlAsync(string formId)
        {
            var entity = _service.Retrieve("systemform", new Guid(formId), new ColumnSet("formxml"));
            return Task.FromResult(entity.GetAttributeValue<string>("formxml"));
        }

        public Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false)
        {
            return Task.FromResult(GetViewsSync(_service, entityLogicalName, includeFetchXml));
        }

        public List<ViewMetadata> GetViewsSync(IOrganizationService service, string entityLogicalName, bool includeFetchXml = false)
        {
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "querytype", "fetchxml", "returnedtypecode", "isdefault"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityLogicalName),
                        new ConditionExpression("querytype", ConditionOperator.Equal, 0) // Public views
                    }
                },
                Orders = { new OrderExpression("name", OrderType.Ascending) }
            };

            var results = service.RetrieveMultiple(query);
            
            return results.Entities.Select(e => 
            {
                var fetchXml = e.GetAttributeValue<string>("fetchxml");
                var columns = includeFetchXml && !string.IsNullOrEmpty(fetchXml) 
                    ? ExtractColumnsFromFetchXml(fetchXml) 
                    : new List<string>();
                
                return new ViewMetadata
                {
                    ViewId = e.GetAttributeValue<Guid>("savedqueryid").ToString(),
                    Name = e.GetAttributeValue<string>("name") ?? "",
                    IsDefault = e.GetAttributeValue<bool>("isdefault"),
                    FetchXml = includeFetchXml ? fetchXml : null,
                    Columns = columns
                };
            }).ToList();
        }

        public Task<string> GetViewFetchXmlAsync(string viewId)
        {
            var entity = _service.Retrieve("savedquery", new Guid(viewId), new ColumnSet("fetchxml"));
            return Task.FromResult(entity.GetAttributeValue<string>("fetchxml"));
        }

        private static List<string> ExtractFieldsFromFormXml(string formXml)
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

        private static List<string> ExtractColumnsFromFetchXml(string fetchXml)
        {
            var columns = new List<string>();
            try
            {
                var doc = XDocument.Parse(fetchXml);
                foreach (var attr in doc.Descendants("attribute"))
                {
                    var name = attr.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                        columns.Add(name.ToLower());
                }
            }
            catch { }

            return columns;
        }
    }
}
