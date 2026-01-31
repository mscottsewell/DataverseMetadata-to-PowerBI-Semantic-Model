# XrmToolBox Integration Prototype

This prototype demonstrates the architecture for creating an XrmToolBox version of the Dataverse to Power BI Semantic Model tool while maintaining a **single codebase**.

## 🎯 Prototype Goals

This prototype validates the following architectural decisions:

1. **IDataverseConnection Interface** - Abstracts authentication between MSAL (standalone) and IOrganizationService (XrmToolBox)
2. **Core Library** - Shared business logic works in both hosting environments
3. **Adapter Pattern** - Clean separation between connection mechanisms
4. **Zero Code Duplication** - Business logic written once, hosted twice

## 📁 Project Structure

```
DataverseMetadata-to-PowerBI-Semantic-Model/
│
├── DataverseToPowerBI.Core/                 # ✅ NEW: Shared business logic
│   ├── Interfaces/
│   │   └── IDataverseConnection.cs         # Connection abstraction
│   ├── Models/
│   │   └── DataModels.cs                   # Shared data models
│   └── Services/
│       ├── DataverseClientAdapter.cs       # MSAL implementation
│       └── XrmServiceAdapter.cs            # IOrganizationService wrapper (stub)
│
├── DataverseToPowerBI.Configurator/         # Standalone WinForms app
│   ├── Forms/                               # (Will reference Core)
│   ├── Models/                              # (To be deprecated - moved to Core)
│   └── Services/                            # (To be deprecated - moved to Core)
│
├── DataverseToPowerBI.XrmToolBox/           # ✅ NEW: XrmToolBox plugin
│   ├── PluginControl.cs                    # XrmToolBox UI
│   └── TmdlPluginTool.cs                   # Plugin metadata
│
└── DataverseMetadata-to-PowerBI-Semantic-Model.sln
```

## 🏗️ Architecture Highlights

### 1. IDataverseConnection Interface

Located in: `DataverseToPowerBI.Core/Interfaces/IDataverseConnection.cs`

**Purpose**: Provides a single API for accessing Dataverse, regardless of authentication mechanism

```csharp
public interface IDataverseConnection
{
    Task<string> AuthenticateAsync(bool clearCredentials = false);
    Task<List<DataverseSolution>> GetSolutionsAsync();
    Task<List<TableInfo>> GetSolutionTablesAsync(string solutionId);
    Task<List<AttributeMetadata>> GetAttributesAsync(string tableName);
    // ... more methods
    bool IsConnected { get; }
}
```

**Why it matters**: Business logic can depend on this interface instead of concrete implementations, enabling:
- Easy unit testing with mock connections
- Swapping between MSAL and IOrganizationService transparently
- Future support for other authentication mechanisms (Azure AD, service principals, etc.)

### 2. DataverseClientAdapter

Located in: `DataverseToPowerBI.Core/Services/DataverseClientAdapter.cs`

**Purpose**: Wraps the existing MSAL-based HTTP client for standalone applications

**Key features**:
- ✅ Implements  `IDataverseConnection`
- ✅ Uses MSAL interactive authentication
- ✅ Calls Dataverse Web API (OData)
- ⚙️ **Status**: Fully implemented and functional

### 3. XrmServiceAdapter (Stub)

Located in: `DataverseToPowerBI.Core/Services/XrmServiceAdapter.cs`

**Purpose**: Will wrap IOrganizationService provided by XrmToolBox

**Current state**: STUB implementation
- ✅ Implements `IDataverseConnection` interface  
- ⏸️ Methods throw `NotImplementedException` (will be completed in full implementation)
- ⚙️ Will use SDK's `RetrieveMultipleRequest`, `RetrieveEntityRequest`, etc.

**Why stub is valuable**: Demonstrates the architecture is sound even without full implementation

### 4. XrmToolBox Plugin

Located in: `DataverseToPowerBI.XrmToolBox/`

**Components**:
- `PluginControl.cs` - User interface (inherits from `PluginControlBase`)
- `TmdlPluginTool.cs` - Plugin metadata and factory

**Key integration points**:

```csharp
public override void UpdateConnection(IOrganizationService newService, 
    ConnectionDetail detail, string actionName, object parameter)
{
    // XrmToolBox provides IOrganizationService
    var environmentUrl = detail.WebApplicationUrl;
    
    // Wrap it in our interface
    _connection = new XrmServiceAdapter(newService, environmentUrl);
    
    // Now use _connection just like standalone app!
}
```

## 🧪 What the Prototype Demonstrates

### ✅ Successfully Demonstrated

1. **Core Library Compiles** - .NET Standard 2.0 builds successfully
2. **Solution Structure** - All projects properly referenced in solution
3. **Interface Abstraction** - Clean separation between connection types
4. **Adapter Pattern** - Both adapters implement same interface
5. **Project References** - XrmToolBox plugin references Core library
6. **Namespace Organization** - Clear separation of concerns

### ⏸️ Not Yet Implemented (intentional for prototype)

1. **XrmServiceAdapter Logic** - SDK calls (will be added in full implementation)
2. **Complete UI** - XrmToolBox plugin has minimal UI (demonstrates concept)
3. **Business Logic Migration** - SemanticModelBuilder, SettingsManager, etc. (next phase)
4. **UserControl Extraction** - Reusable UI components (next phase)

## 🚀 Next Steps for Full Implementation

### Phase 1: Complete Core Migration
- [ ] Move `SemanticModelBuilder.cs` to Core
- [ ] Move `FetchXmlToSqlConverter.cs` to Core
- [ ] Move `DebugLogger.cs` to Core (with environment awareness)
- [ ] Update `SettingsManager.cs` for multi-host support

### Phase 2: Implement XrmServiceAdapter
- [ ] Add Microsoft.CrmSdk.CoreAssemblies NuGet package
- [ ] Implement `GetSolutionsAsync()` using SDK
- [ ] Implement `GetSolutionTablesAsync()` using SDK
- [ ] Implement `GetAttributesAsync()` using SDK
- [ ] Implement form/view retrieval methods

### Phase 3: Build XrmToolBox UI
- [ ] Extract table selector as UserControl
- [ ] Extract attribute list as UserControl
- [ ] Compose controls in XrmToolBox PluginControl
- [ ] Implement progress reporting via `WorkAsync()`

### Phase 4: NuGet Packaging
- [ ] Create `.nuspec` file
- [ ] Configure post-build event to create package
- [ ] Test installation in XrmToolBox
- [ ] Document plugin installation process

## 💡 Key Architectural Insights

### Why This Approach Works

1. **Single Source of Truth**: Models live in Core, referenced by both hosts
2. **Polymorphism**: `IDataverseConnection` enables different authentication without changing business logic
3. **Testability**: Mock `IDataverseConnection` in unit tests
4. **Maintainability**: Bug fixes in Core automatically benefit both apps
5. **Extensibility**: Easy to add CLI tool, web API, VS Code extension, etc.

### Comparison: Before vs After

| Aspect | Before (Single App) | After (Dual Host) |
|--------|-------------------|-------------------|
| Authentication | MSAL hardcoded | Interface-based |
| Code Reuse | N/A | 100% business logic |
| Testing | Difficult (MSAL required) | Easy (mock interface) |
| Deployment | Single .exe | .exe + NuGet package |
| Maintenance | Update one codebase | Update one codebase ✅ |

## 🔧 Building the Prototype

### Prerequisites
- .NET SDK 6.0 or later
- Visual Studio 2022 or VS Code
- (Optional) XrmToolBox for testing plugin

### Build Commands

```powershell
# Build Core library
dotnet build DataverseToPowerBI.Core

# Build entire solution
dotnet build

# Restore packages
dotnet restore
```

### Expected Warnings

When building Core, you may see nullable reference warnings:
```
warning CS8602: Dereference of a possibly null reference.
warning CS8604: Possible null reference argument...
```

These are **expected** and will be addressed in full implementation (adding null checks or non-null assertions).

## 📊 Prototype Success Metrics

✅ **Architecture Validation**
- [x] Core library targets .NET Standard 2.0 (compatible with Framework + Core)
- [x] Interface abstraction compiles and makes sense
- [x] Both adapters implement the same contract
- [x] XrmToolBox plugin project structure is valid

✅ **Proof of Concept**
- [x] Demonstrates zero code duplication strategy
- [x] Shows clear separation of concerns
- [x] Validates NuGet packaging approach feasible
- [x] Confirms solution can scale to multiple hosts

## 📝 Notes for Full Implementation

### Important Considerations

1. **Threading in XrmToolBox**: Use `WorkAsync()` for long operations
2. **Error Handling**: Wrap SDK calls in try/catch (different exceptions than Web API)
3. **Performance**: SDK may be slower than Web API for bulk operations
4. **Caching**: SettingsManager paths differ by host environment
5. **UI Patterns**: XrmToolBox users expect certain interaction patterns

### Potential Challenges

- **Entity Metadata Differences**: SDK returns different object types than Web API
  - *Solution*: Translate in XrmServiceAdapter to Core models
- **.NET Framework vs .NET Core**: XrmToolBox requires Framework 4.7.2
  - *Solution*: Core targets .NET Standard 2.0 (compatible with both)
- **NuGet Dependencies**: XrmToolBox may conflict with newer packages
  - *Solution*: Test thoroughly, use binding redirects if needed

## 🎓 Learning Resources

- [XrmToolBox Plugin Development](https://www.xrmtoolbox.com/documentation/plugin-development/)
- [.NET Standard compatibility](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
- [Microsoft Dynamics SDK](https://docs.microsoft.com/en-us/powerapps/developer/data-platform/org-service/overview)

## 📄 License

MIT License (same as parent project)

---

**Questions or feedback?** Open an issue in the repository.

**Ready to proceed?** Start with Phase 1: Complete Core Migration
