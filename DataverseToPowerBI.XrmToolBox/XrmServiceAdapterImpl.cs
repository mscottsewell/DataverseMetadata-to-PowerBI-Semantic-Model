// ===================================================================================
// XrmServiceAdapterImpl.cs - XrmToolBox IDataverseConnection Implementation
// ===================================================================================
//
// PURPOSE:
// This class implements the IDataverseConnection interface using the Dataverse SDK
// (IOrganizationService) instead of the REST API. This allows the XrmToolBox plugin
// to use the same shared Core library logic while leveraging XrmToolBox's built-in
// connection management.
//
// ARCHITECTURE:
// - Authentication: Handled externally by XrmToolBox (no MSAL required)
// - API: Uses IOrganizationService (SDK) instead of HttpClient (Web API)
// - Synchronous Methods: Provides sync versions for XrmToolBox WorkAsync pattern
//
// IMPLEMENTATION PATTERN:
// The adapter wraps IOrganizationService calls to match the IDataverseConnection
// interface contract:
//   - GetSolutionsAsync() → QueryExpression on "solution" entity
//   - GetSolutionTablesAsync() → QueryExpression on "solutioncomponent" entity
//   - GetAttributesSync() → RetrieveEntityRequest for attribute metadata
//   - GetFormsSync() → QueryExpression on "systemform" entity
//   - GetViewsSync() → QueryExpression on "savedquery" entity
//
// THREAD SAFETY:
// Methods are not thread-safe. In XrmToolBox, these should be called from
// WorkAsync() callbacks which execute on a background thread.
//
// ===================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
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
        private const int MAX_XML_SIZE_BYTES = 5 * 1024 * 1024; // 5MB

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Securely parses XML to prevent XXE (XML External Entity) attacks.
        /// </summary>
        private static XDocument ParseXmlSecurely(string xml)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader);
        }

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
                        new ConditionExpression("isvisible", ConditionOperator.Equal, true),
                        new ConditionExpression("ismanaged", ConditionOperator.Equal, false)
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
        /// Retrieves all tables (entities) from the Dataverse environment.
        /// Use this when the user doesn't have permission to view solutions (prvReadSolution).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method can return hundreds of tables. Activity and Intersect entities
        /// are filtered out as they're typically not suitable for Power BI semantic models.
        /// Only tables that have at least one form are included.
        /// </para>
        /// <para>
        /// Consider this a fallback when GetSolutionsSync fails due to privilege errors.
        /// </para>
        /// </remarks>
        public List<TableInfo> GetAllTablesSync(IOrganizationService service)
        {
            var tables = new List<TableInfo>();
            
            // First, get all entity logical names that have at least one form
            var entitiesWithForms = GetEntitiesWithFormsSync(service);
            
            // Use RetrieveAllEntitiesRequest to get all entities
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
                    // Skip Activity entities (tasks, emails, etc.) - these have special handling
                    // Skip Intersect entities (N:N relationship tables) - not useful for reporting
                    if (item.IsActivity == true || item.IsIntersect == true)
                        continue;
                    
                    // Skip system entities that are typically not useful for reporting
                    // These include internal entities that users rarely need
                    var logicalName = item.LogicalName ?? "";
                    if (logicalName.StartsWith("msdyn_") && logicalName.Contains("_migration"))
                        continue;
                    
                    // Skip tables that don't have any forms
                    if (!entitiesWithForms.Contains(logicalName))
                        continue;
                    
                    string displayName = logicalName;
                    if (item.DisplayName?.UserLocalizedLabel != null)
                    {
                        displayName = item.DisplayName.UserLocalizedLabel.Label ?? logicalName;
                    }
                    
                    var metadataId = item.MetadataId;
                    
                    tables.Add(new TableInfo
                    {
                        LogicalName = logicalName,
                        DisplayName = displayName,
                        SchemaName = item.SchemaName,
                        PrimaryIdAttribute = item.PrimaryIdAttribute,
                        PrimaryNameAttribute = item.PrimaryNameAttribute,
                        ObjectTypeCode = item.ObjectTypeCode ?? 0,
                        MetadataId = metadataId?.ToString()
                    });
                }
            }

            return tables.OrderBy(t => t.DisplayName ?? t.LogicalName).ToList();
        }
        
        /// <summary>
        /// Gets a set of entity logical names that have at least one form defined.
        /// Used to filter out tables that don't have UI forms.
        /// </summary>
        private HashSet<string> GetEntitiesWithFormsSync(IOrganizationService service)
        {
            var entitiesWithForms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Query systemform to get distinct entity logical names that have forms
            // Form type 2 = Main form (the most common type used for data entry)
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("objecttypecode"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("type", ConditionOperator.Equal, 2) // Main form
                    }
                }
            };
            
            var results = service.RetrieveMultiple(query);
            
            foreach (var entity in results.Entities)
            {
                var objectTypeCode = entity.GetAttributeValue<string>("objecttypecode");
                if (!string.IsNullOrEmpty(objectTypeCode))
                {
                    entitiesWithForms.Add(objectTypeCode);
                }
            }
            
            return entitiesWithForms;
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
                        string displayName = item.LogicalName ?? "";
                        if (item.DisplayName?.UserLocalizedLabel != null)
                        {
                            displayName = item.DisplayName.UserLocalizedLabel.Label ?? item.LogicalName ?? "";
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
                // First pass: Build a dictionary of all attributes by logical name
                var allAttributes = new Dictionary<string, Microsoft.Xrm.Sdk.Metadata.AttributeMetadata>(StringComparer.OrdinalIgnoreCase);
                foreach (var attr in entityMetadata.Attributes)
                {
                    if (!string.IsNullOrEmpty(attr.LogicalName))
                    {
                        allAttributes[attr.LogicalName] = attr;
                    }
                }
                
                // Second pass: Process attributes and find virtual attribute mappings
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
                    
                    // Get description
                    string? description = null;
                    if (attr.Description?.UserLocalizedLabel != null)
                    {
                        description = attr.Description.UserLocalizedLabel.Label;
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
                    List<string> targets = new List<string>();
                    if (attr is LookupAttributeMetadata lookupAttr && lookupAttr.Targets != null)
                    {
                        targets = lookupAttr.Targets.ToList();
                    }
                    
                    // For Picklist, Boolean, State, and Status attributes, find the associated virtual attribute
                    string? virtualAttributeName = null;
                    bool? isGlobal = null;
                    string? optionSetName = null;

                    if (attr is PicklistAttributeMetadata || attr is MultiSelectPicklistAttributeMetadata ||
                        attr is BooleanAttributeMetadata ||
                        attr is StateAttributeMetadata || attr is StatusAttributeMetadata)
                    {
                        // Capture IsGlobal and OptionSetName for FabricLink queries
                        if (attr is PicklistAttributeMetadata picklistAttr && picklistAttr.OptionSet != null)
                        {
                            isGlobal = picklistAttr.OptionSet.IsGlobal;
                            optionSetName = picklistAttr.OptionSet.Name;
                        }
                        else if (attr is MultiSelectPicklistAttributeMetadata multiPicklistAttr && multiPicklistAttr.OptionSet != null)
                        {
                            isGlobal = multiPicklistAttr.OptionSet.IsGlobal;
                            optionSetName = multiPicklistAttr.OptionSet.Name;
                        }

                        // Try standard pattern first: {logicalname}name
                        var standardVirtualName = logicalName + "name";
                        if (allAttributes.ContainsKey(standardVirtualName))
                        {
                            virtualAttributeName = standardVirtualName;
                        }
                        else
                        {
                            // Search for any virtual attribute that might be associated
                            // Virtual attributes often have names related to the base attribute
                            // For example: donotsendmm -> donotsendmarketingmaterial
                            string? fallbackMatch = null;
                            
                            foreach (var kvp in allAttributes)
                            {
                                var candidateAttr = kvp.Value;
                                var candidateName = candidateAttr.LogicalName ?? "";
                                
                                // Skip the base attribute itself
                                if (candidateName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                
                                // Check if this starts with the base attribute name
                                if (candidateName.StartsWith(logicalName ?? "", StringComparison.OrdinalIgnoreCase) &&
                                    candidateName.Length > (logicalName?.Length ?? 0))
                                {
                                    // Check if this is explicitly marked as a virtual attribute
                                    var isVirtual = candidateAttr.AttributeTypeName?.Value == "VirtualType";
                                    
                                    // Prefer attributes with "name" in the logical name
                                    if (candidateName.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        if (isVirtual)
                                        {
                                            // Best match: virtual attribute with "name" in it
                                            virtualAttributeName = candidateName;
                                            break;
                                        }
                                        else if (fallbackMatch == null)
                                        {
                                            // Good match: attribute with "name" (may not be explicitly virtual)
                                            fallbackMatch = candidateName;
                                        }
                                    }
                                    // Match without "name"
                                    else if (isVirtual && virtualAttributeName == null)
                                    {
                                        virtualAttributeName = candidateName;
                                    }
                                    else if (!isVirtual && fallbackMatch == null && virtualAttributeName == null)
                                    {
                                        fallbackMatch = candidateName;
                                    }
                                }
                            }
                            
                            // Use fallback if no virtual attribute was found
                            if (virtualAttributeName == null && fallbackMatch != null)
                            {
                                virtualAttributeName = fallbackMatch;
                            }
                        }
                        
                        // If we still haven't found a virtual attribute, only use the standard
                        // pattern if it actually exists in metadata to avoid referencing non-existent columns
                        if (virtualAttributeName == null)
                        {
                            if (allAttributes.ContainsKey(standardVirtualName))
                            {
                                virtualAttributeName = standardVirtualName;
                            }
                            else
                            {
                                Services.DebugLogger.Log($"WARNING: Virtual attribute '{standardVirtualName}' for '{logicalName}' not found in metadata. Skipping virtual attribute.");
                            }
                        }
                        else if (virtualAttributeName != standardVirtualName)
                        {
                            // Log when using non-standard virtual attribute name
                            System.Diagnostics.Debug.WriteLine($"VirtualAttribute: {logicalName} -> {virtualAttributeName} (found)");
                        }
                    }
                    
                    attributes.Add(new CoreAttributeMetadata
                    {
                        LogicalName = logicalName ?? "",
                        DisplayName = displayName,
                        SchemaName = schemaName ?? "",
                        Description = description,
                        AttributeType = MapAttributeType(typeValue ?? ""),
                        IsCustomAttribute = isCustomAttribute,
                        IsRequired = isRequired,
                        Targets = targets.Count > 0 ? targets : null,
                        VirtualAttributeName = virtualAttributeName,
                        IsGlobal = isGlobal,
                        OptionSetName = optionSetName
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
                case "MultiSelectPicklistType": return "MultiSelectPicklist";
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

        public Task<string?> GetFormXmlAsync(string formId)
        {
            var entity = _service.Retrieve("systemform", new Guid(formId), new ColumnSet("formxml"));
            return Task.FromResult<string?>(entity.GetAttributeValue<string>("formxml"));
        }

        public Task<List<ViewMetadata>> GetViewsAsync(string entityLogicalName, bool includeFetchXml = false)
        {
            return Task.FromResult(GetViewsSync(_service, entityLogicalName, includeFetchXml));
        }

        public List<ViewMetadata> GetViewsSync(IOrganizationService service, string entityLogicalName, bool includeFetchXml = false)
        {
            var views = new List<ViewMetadata>();

            // System (public) views from savedquery
            var systemQuery = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "querytype", "fetchxml", "layoutxml", "returnedtypecode", "isdefault"),
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

            var systemResults = service.RetrieveMultiple(systemQuery);
            
            views.AddRange(systemResults.Entities.Select(e => 
            {
                var fetchXml = e.GetAttributeValue<string>("fetchxml");
                var layoutXml = e.GetAttributeValue<string>("layoutxml");
                var extractResult = includeFetchXml
                    ? ExtractViewColumns(fetchXml, layoutXml)
                    : new ViewColumnExtractionResult();
                
                return new ViewMetadata
                {
                    ViewId = e.GetAttributeValue<Guid>("savedqueryid").ToString(),
                    Name = e.GetAttributeValue<string>("name") ?? "",
                    IsDefault = e.GetAttributeValue<bool>("isdefault"),
                    FetchXml = includeFetchXml ? fetchXml : null,
                    Columns = extractResult.VisibleColumns,
                    LinkedColumns = extractResult.LinkedColumns,
                    IsPersonal = false
                };
            }));

            // Personal (user) views from userquery
            try
            {
                var personalQuery = new QueryExpression("userquery")
                {
                    ColumnSet = new ColumnSet("userqueryid", "name", "querytype", "fetchxml", "layoutxml", "returnedtypecode"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("returnedtypecode", ConditionOperator.Equal, entityLogicalName),
                            new ConditionExpression("querytype", ConditionOperator.Equal, 0),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0) // Active only
                        }
                    },
                    Orders = { new OrderExpression("name", OrderType.Ascending) }
                };

                var personalResults = service.RetrieveMultiple(personalQuery);
                
                views.AddRange(personalResults.Entities.Select(e => 
                {
                    var fetchXml = e.GetAttributeValue<string>("fetchxml");
                    var layoutXml = e.GetAttributeValue<string>("layoutxml");
                    var extractResult = includeFetchXml
                        ? ExtractViewColumns(fetchXml, layoutXml)
                        : new ViewColumnExtractionResult();
                    
                    return new ViewMetadata
                    {
                        ViewId = e.GetAttributeValue<Guid>("userqueryid").ToString(),
                        Name = e.GetAttributeValue<string>("name") ?? "",
                        IsDefault = false,
                        FetchXml = includeFetchXml ? fetchXml : null,
                        Columns = extractResult.VisibleColumns,
                        LinkedColumns = extractResult.LinkedColumns,
                        IsPersonal = true
                    };
                }));
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Fault"))
            {
                // Personal views may not be accessible due to permissions - log and continue with system views only
                Services.DebugLogger.Log($"Expected error retrieving personal views: {ex}");
            }

            return views;
        }

        public Task<string?> GetViewFetchXmlAsync(string viewId)
        {
            var entity = _service.Retrieve("savedquery", new Guid(viewId), new ColumnSet("fetchxml"));
            return Task.FromResult<string?>(entity.GetAttributeValue<string>("fetchxml"));
        }

        private static List<string> ExtractFieldsFromFormXml(string? formXml)
        {
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (string.IsNullOrWhiteSpace(formXml))
                {
                    return fields.ToList();
                }

                if (formXml!.Length > MAX_XML_SIZE_BYTES)
                {
                    Services.DebugLogger.Log($"WARNING: FormXML exceeds {MAX_XML_SIZE_BYTES} bytes ({formXml.Length} bytes). Skipping parse.");
                    return fields.ToList();
                }

                var doc = ParseXmlSecurely(formXml!);
                foreach (var control in doc.Descendants("control"))
                {
                    var fieldName = control.Attribute("datafieldname")?.Value;
                    if (fieldName == null)
                        continue;
                    if (!string.IsNullOrEmpty(fieldName))
                    {
                        fields.Add(fieldName.ToLower());
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - return partial results
                Services.DebugLogger.Log($"XML parsing error in FormXml: {ex.Message}");
            }

            return fields.OrderBy(f => f).ToList();
        }

        private static List<string> ExtractColumnsFromFetchXml(string? fetchXml)
        {
            return ExtractViewColumnsFromFetchXml(fetchXml).Item1;
        }

        private sealed class ViewColumnExtractionResult
        {
            public List<string> VisibleColumns { get; set; } = new List<string>();
            public List<ViewLinkedColumn> LinkedColumns { get; set; } = new List<ViewLinkedColumn>();
        }

        /// <summary>
        /// Extracts visible view columns from LayoutXML and FetchXML columns used by the query.
        /// If LayoutXML has no columns, falls back to FetchXML direct attributes as visible columns.
        /// </summary>
        private static ViewColumnExtractionResult ExtractViewColumns(string? fetchXml, string? layoutXml)
        {
            var fetchResult = ExtractViewColumnsFromFetchXml(fetchXml);
            var layoutColumns = ExtractVisibleColumnsFromLayoutXml(layoutXml);

            var visibleColumns = layoutColumns.Count > 0
                ? layoutColumns
                : fetchResult.Item1;

            return new ViewColumnExtractionResult
            {
                VisibleColumns = visibleColumns,
                LinkedColumns = fetchResult.Item2
            };
        }

        /// <summary>
        /// Extracts visible column names from Dataverse view LayoutXML (&lt;cell name="..." /&gt;).
        /// </summary>
        private static List<string> ExtractVisibleColumnsFromLayoutXml(string? layoutXml)
        {
            var columns = new List<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(layoutXml))
                {
                    return columns;
                }

                if (layoutXml!.Length > MAX_XML_SIZE_BYTES)
                {
                    Services.DebugLogger.Log($"WARNING: LayoutXML exceeds {MAX_XML_SIZE_BYTES} bytes ({layoutXml.Length} bytes). Skipping parse.");
                    return columns;
                }

                var doc = ParseXmlSecurely(layoutXml!);
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var cell in doc.Descendants("cell"))
                {
                    var name = cell.Attribute("name")?.Value;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (unique.Add(name))
                    {
                        columns.Add(name.ToLower());
                    }
                }
            }
            catch (Exception ex)
            {
                Services.DebugLogger.Log($"XML parsing error in LayoutXml: {ex.Message}");
            }

            return columns;
        }

        /// <summary>
        /// Extracts both direct columns and link-entity columns from FetchXML.
        /// Direct columns are attributes on the primary entity.
        /// Linked columns are attributes from link-entity elements (related entities via lookups).
        /// </summary>
        private static (List<string>, List<ViewLinkedColumn>) ExtractViewColumnsFromFetchXml(string? fetchXml)
        {
            var columns = new List<string>();
            var linkedColumns = new List<ViewLinkedColumn>();
            try
            {
                if (string.IsNullOrWhiteSpace(fetchXml))
                {
                    return (columns, linkedColumns);
                }

                if (fetchXml!.Length > MAX_XML_SIZE_BYTES)
                {
                    Services.DebugLogger.Log($"WARNING: FetchXML exceeds {MAX_XML_SIZE_BYTES} bytes ({fetchXml.Length} bytes). Skipping parse.");
                    return (columns, linkedColumns);
                }

                var doc = ParseXmlSecurely(fetchXml!);

                // Get the primary entity element
                var entityElement = doc.Descendants("entity").FirstOrDefault();
                if (entityElement == null)
                    return (columns, linkedColumns);

                // Direct attributes: only those directly under <entity>, not inside <link-entity>
                foreach (var attr in entityElement.Elements("attribute"))
                {
                    var name = attr.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        columns.Add(name.ToLower());
                    }
                }

                // Link-entity attributes: attributes inside <link-entity> elements
                foreach (var linkEntity in entityElement.Elements("link-entity"))
                {
                    var linkedEntityName = linkEntity.Attribute("name")?.Value;
                    var toAttribute = linkEntity.Attribute("to")?.Value; // lookup field on current entity
                    var alias = linkEntity.Attribute("alias")?.Value;

                    if (string.IsNullOrEmpty(linkedEntityName) || string.IsNullOrEmpty(toAttribute))
                        continue;

                    foreach (var attr in linkEntity.Elements("attribute"))
                    {
                        var attrName = attr.Attribute("name")?.Value;
                        if (!string.IsNullOrEmpty(attrName))
                        {
                            linkedColumns.Add(new ViewLinkedColumn
                            {
                                LookupAttribute = toAttribute!.ToLower(),
                                LinkedEntityName = linkedEntityName!.ToLower(),
                                AttributeName = attrName.ToLower(),
                                Alias = alias
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - return partial results
                Services.DebugLogger.Log($"XML parsing error in FetchXml: {ex.Message}");
            }

            return (columns, linkedColumns);
        }

        /// <summary>
        /// Gets one-to-many relationships for a table (tables that reference this table via lookup).
        /// </summary>
        public List<OneToManyRelationshipInfo> GetOneToManyRelationshipsSync(IOrganizationService service, string tableName)
        {
            var relationships = new List<OneToManyRelationshipInfo>();

            try
            {
                var request = new RetrieveEntityRequest
                {
                    LogicalName = tableName,
                    EntityFilters = EntityFilters.Relationships,
                    RetrieveAsIfPublished = true
                };

                var response = (RetrieveEntityResponse)service.Execute(request);
                var entityMetadata = response.EntityMetadata;

                if (entityMetadata?.OneToManyRelationships != null)
                {
                    foreach (var rel in entityMetadata.OneToManyRelationships)
                    {
                        // Skip self-references
                        if (rel.ReferencingEntity == tableName)
                            continue;

                        // Skip system entities that are typically not useful
                        var referencingEntity = rel.ReferencingEntity ?? "";
                        if (referencingEntity.StartsWith("msdyn_") && referencingEntity.Contains("_migration"))
                            continue;

                        // Get lookup display name
                        string lookupDisplayName = rel.ReferencingAttribute ?? "";
                        // Note: Would need additional metadata call to get lookup display name

                        relationships.Add(new OneToManyRelationshipInfo
                        {
                            ReferencingEntity = rel.ReferencingEntity ?? "",
                            ReferencingAttribute = rel.ReferencingAttribute ?? "",
                            ReferencedEntity = rel.ReferencedEntity ?? "",
                            SchemaName = rel.SchemaName ?? "",
                            LookupDisplayName = lookupDisplayName
                        });
                    }
                }
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Fault"))
            {
                Services.DebugLogger.Log($"Expected error getting one-to-many relationships for {tableName}: {ex}");
            }

            return relationships.OrderBy(r => r.ReferencingEntity).ToList();
        }

        /// <summary>
        /// Gets a dictionary of all entity logical names to their display names.
        /// </summary>
        public Dictionary<string, string> GetAllEntityDisplayNamesSync(IOrganizationService service)
        {
            var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var request = new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity,
                    RetrieveAsIfPublished = true
                };

                var response = (RetrieveAllEntitiesResponse)service.Execute(request);

                if (response.EntityMetadata != null)
                {
                    foreach (var entity in response.EntityMetadata)
                    {
                        var logicalName = entity.LogicalName ?? "";
                        var displayName = entity.DisplayName?.UserLocalizedLabel?.Label ?? logicalName;
                        
                        if (!string.IsNullOrEmpty(logicalName))
                        {
                            displayNames[logicalName] = displayName;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Fault"))
            {
                Services.DebugLogger.Log($"Expected error getting all entity display names: {ex}");
            }

            return displayNames;
        }

        /// <summary>
        /// Retrieves the organization's base language code (LCID).
        /// Used for FabricLink queries to filter metadata tables by language.
        /// </summary>
        /// <param name="service">The organization service connection.</param>
        /// <returns>The organization's base LCID, defaults to 1033 (US English) if not found.</returns>
        public int GetBaseLanguageCodeSync(IOrganizationService service)
        {
            try
            {
                var query = new QueryExpression("organization")
                {
                    ColumnSet = new ColumnSet("languagecode"),
                    TopCount = 1
                };
                var results = service.RetrieveMultiple(query);
                if (results.Entities.Count > 0)
                {
                    return results.Entities[0].GetAttributeValue<int>("languagecode");
                }
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Fault"))
            {
                Services.DebugLogger.Log($"Expected error getting base language code: {ex}");
            }
            return 1033; // Default to US English
        }
    }

    /// <summary>
    /// Represents a one-to-many relationship where another table references this table.
    /// </summary>
    public class OneToManyRelationshipInfo
    {
        /// <summary>
        /// The entity that contains the lookup field (the "many" side).
        /// </summary>
        public string ReferencingEntity { get; set; } = "";

        /// <summary>
        /// The lookup attribute name on the referencing entity.
        /// </summary>
        public string ReferencingAttribute { get; set; } = "";

        /// <summary>
        /// The entity being referenced (the "one" side).
        /// </summary>
        public string ReferencedEntity { get; set; } = "";

        /// <summary>
        /// The schema name of the relationship.
        /// </summary>
        public string SchemaName { get; set; } = "";

        /// <summary>
        /// Display name of the lookup field.
        /// </summary>
        public string LookupDisplayName { get; set; } = "";
    }
}
