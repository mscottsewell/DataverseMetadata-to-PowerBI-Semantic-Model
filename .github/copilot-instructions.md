# Copilot Instructions

## Project Overview

XrmToolBox plugin that generates Power BI semantic models (PBIP/TMDL format) from Dataverse metadata. Users select Dataverse tables via a star-schema wizard, and the tool outputs a complete Power BI project with optimized DirectQuery expressions.

## Build

```powershell
# Restore and build
dotnet build -c Release

# Build only the Core library
dotnet build DataverseToPowerBI.Core -c Release

# Build only the XrmToolBox plugin
dotnet build DataverseToPowerBI.XrmToolBox -c Release
```

Target framework is **.NET Framework 4.8** with **C# 9.0** language version. Both projects must target `net48` for XrmToolBox compatibility.

There are no automated tests. Testing is manual via XrmToolBox connected to a Dataverse environment.

## Architecture

Two-project solution with a strict dependency direction:

```
XrmToolBox (UI + Services + Plugin hosting)
    └── references → Core (Models + Interfaces)
```

- **DataverseToPowerBI.Core** — Shared library containing data models (`DataModels.cs`) and the `IDataverseConnection` interface. Framework-agnostic; no UI dependencies.
- **DataverseToPowerBI.XrmToolBox** — The plugin itself. Contains all UI forms, the TMDL generation engine (`SemanticModelBuilder`), FetchXML-to-SQL converter, configuration persistence, and the Dataverse SDK adapter.

### Data Flow

User dialogs → `PluginControl` (orchestrator) → `XrmServiceAdapterImpl` (Dataverse queries) → `SemanticModelBuilder` (TMDL generation) → PBIP folder output on disk.

### Key Components

| Component | Role |
|-----------|------|
| `SemanticModelBuilder.cs` | Core TMDL generation engine (~3,000 lines). Handles template management, dual connection modes, table/relationship generation, change detection, and user code preservation. |
| `FetchXmlToSqlConverter.cs` | Translates Dataverse view FetchXML filters into T-SQL WHERE clauses. Supports TDS and FabricLink modes with different operator support. |
| `SemanticModelManager.cs` | JSON-based configuration persistence to `%APPDATA%\MscrmTools\XrmToolBox\Settings\DataverseToPowerBI\`. |
| `XrmServiceAdapterImpl.cs` | Implements `IDataverseConnection` using the Dataverse SDK (`IOrganizationService`). Wraps sync SDK calls in async Task signatures. |
| `PluginControl.cs` | Main XrmToolBox UI control. Uses `WorkAsync` pattern for background operations. |

### Dual Connection Modes

The builder generates different TMDL output depending on connection mode:
- **DataverseTDS** — Uses `CommonDataService.Database` connector with `Value.NativeQuery` and SQL via the TDS endpoint.
- **FabricLink** — Uses `Sql.Database` connector against a Fabric Lakehouse SQL endpoint. Generates JOINs to `OptionsetMetadata`/`GlobalOptionsetMetadata` tables for display names.

## Code Conventions

- **Naming**: PascalCase for public members, `_camelCase` for private fields, ALL_CAPS for constants.
- **XML doc comments** (`///`) on all public types and methods. File-level header blocks describe PURPOSE, SUPPORTED FEATURES, etc.
- **Nullable reference types** are enabled. Null-coalescing (`?? throw new ArgumentNullException()`) and null-conditional operators are standard.
- **`#region`** blocks organize large files (>500 lines).
- **Security**: All XML parsing uses `ParseXmlSecurely()` with `DtdProcessing.Prohibit` and `XmlResolver = null` to prevent XXE attacks. Never parse XML without these settings.
- **Logging**: Use `DebugLogger` (thread-safe, static, lock-based) for diagnostic output. Use `Action<string>? statusCallback` for UI status updates.
- **Async pattern**: Methods on `IDataverseConnection` are async (`Task<T>`), but implementations typically wrap synchronous SDK calls with `Task.FromResult()` since XrmToolBox manages threading via `WorkAsync`.

## Version Numbering

Format: `Major.Year.Minor.Patch` (e.g., `1.2026.3.0`).

## Dependencies

- **Core**: `Newtonsoft.Json` (NuGet)
- **XrmToolBox**: References XrmToolBox SDK assemblies (`XrmToolBox.Extensibility`, `Microsoft.Xrm.Sdk`, `McTools.Xrm.Connection`) from local paths — not NuGet. Authentication is handled entirely by XrmToolBox.
