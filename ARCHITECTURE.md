# Architecture Diagram

## Dual-Host Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      USER INTERACTION LAYER                          │
├──────────────────────┬──────────────────────────────────────────────┤
│                      │                                               │
│  Standalone App      │         XrmToolBox Plugin                    │
│  (WinForms)          │         (.NET Framework 4.7.2)               │
│  (.NET 8.0)          │                                               │
│                      │                                               │
│  ┌─────────────┐     │         ┌──────────────┐                     │
│  │  MainForm   │     │         │ PluginControl│                     │
│  │  .cs        │     │         │  .cs         │                     │
│  └──────┬──────┘     │         └──────┬───────┘                     │
│         │            │                │                              │
│         │Uses        │                │Uses                          │
└─────────┼────────────┴────────────────┼──────────────────────────────┘
          │                             │
          │   ┌─────────────────────────┼───────────────────┐
          │   │                         │                   │
          └───┼───► IDataverseConnection◄───────┘          │
              │         Interface                           │
              │                                              │
              │  ┌────────────────┐   ┌──────────────────┐ │
              │  │                │   │                  │ │
              │  │ DataverseClient│   │ XrmService      │ │
              │  │ Adapter        │   │ Adapter         │ │
              │  │                │   │                  │ │
              │  │ (MSAL Auth)    │   │ (SDK Wrapper)   │ │
              │  │                │   │                  │ │
              │  └────────┬───────┘   └────────┬─────────┘ │
              │           │                    │            │
              │           │Both use:           │            │
              │           │   - DataModels     │            │
              │           │   - Business Logic │            │
              └───────────┼────────────────────┼────────────┘
                          │                    │
                ┌─────────▼────────────────────▼──────────┐
                │                                          │
                │    DATAVERSE CONNECTION LAYER            │
                │                                          │
                ├──────────────────┬───────────────────────┤
                │                  │                       │
                │  Web API         │   Organization        │
                │  (OData/REST)    │   Service (SDK)       │
                │                  │                       │
                └──────────────────┴───────────────────────┘
```

## Key Benefits

### ✅ Single Codebase
- Models defined once in `DataverseToPowerBI.Core/Models`
- Business logic written once, reused everywhere
- Bug fixes propagate to both hosts automatically

### ✅ Testable Architecture
- Mock `IDataverseConnection` for unit tests
- Test business logic without authentication
- Validate both adapters independently

### ✅ Future-Proof
- Easy to add CLI tool: Create new adapter
- Web API support: Implement interface
- VS Code extension: Reuse Core library

### ⚡ Performance Considerations

**MSAL Adapter (Standalone)**:
- ➕ Direct HTTP calls to Web API
- ➕ Efficient for bulk operations
- ➕ Modern authentication with token caching

**SDK Adapter (XrmToolBox)**:
- ➕ Leverages existing connection (no re-auth)
- ➕ Familiar to Dynamics developers
- ➖ May be slower for large result sets

## Data Flow Example

```
User Action: "Load Solutions"
     │
     ├─[Standalone]─────────────────────┐
     │                                   │
     ├─► MainForm.BtnClick()            │
     │       │                           │
     │       ├─► _connection.GetSolutionsAsync()
     │       │        │                  │
     │       │        └─► DataverseClientAdapter
     │       │                 │         │
     │       │                 └─► MSAL Auth
     │       │                       │   │
     │       │                       └─► Web API
     │       │                           │
     │       ◄───── List<Solution> ──────┘
     │       │
     │       └─► Display in UI
     │
     ├─[XrmToolBox]──────────────────────┐
     │                                    │
     └─► PluginControl.BtnClick()        │
             │                            │
             ├─► _connection.GetSolutionsAsync()
             │        │                   │
             │        └─► XrmServiceAdapter
             │                 │          │
             │                 └─► IOrganizationService
             │                       │    │
             │                       └─► Dynamics SDK
             │                            │
             ◄───── List<Solution> ───────┘
             │
             └─► Display in UI

SAME Core.Models.DataverseSolution used in both paths!
```

## Dependencies

### Core Library
- Newtonsoft.Json (JSON serialization)
- Microsoft.Identity.Client (MSAL authentication)
- System.Net.Http (HTTP client)

Target: **.NET Standard 2.0** (compatible with both Framework and Core)

### Standalone App
- Core Library
- Windows Forms
- .NET 8.0

### XrmToolBox Plugin
- Core Library
- XrmToolbox.PluginBase
- Microsoft.CrmSdk.CoreAssemblies
- .NET Framework 4.7.2

## File Organization

```
Core/
├── Interfaces/
│   └── IDataverseConnection.cs        ← Contract
├── Models/
│   └── DataModels.cs                  ← DTOs
└── Services/
    ├── DataverseClientAdapter.cs      ← MSAL impl
    ├── XrmServiceAdapter.cs           ← SDK impl
    ├── SemanticModelBuilder.cs        ← Business logic
    └── SettingsManager.cs             ← Persistence

Configurator/ (Standalone)
├── Forms/
│   └── MainForm.cs                    ← Uses Core
└── Program.cs                         ← Entry point

XrmToolBox/ (Plugin)
├── PluginControl.cs                   ← Uses Core
└── TmdlPluginTool.cs                  ← Plugin metadata
```
