# XrmToolBox Plugin Deployment

## Deployment Structure

The XrmToolBox plugin is deployed with the following structure:

```
%APPDATA%\MscrmTools\XrmToolBox\
├── Plugins\
│   ├── DataverseToPowerBI.XrmToolBox.dll    # Main plugin DLL (in Plugins root)
│   └── DataverseToPowerBI\                   # Plugin subfolder for dependencies
│       ├── DataverseToPowerBI.Core.dll       # Shared Core library
│       └── PBIP_DefaultTemplate\             # Power BI template files
│           ├── DateTable.tmdl
│           ├── Template.pbip
│           ├── Template.Report\
│           └── Template.SemanticModel\
```

## Key Design Decisions

### 1. Plugin DLL in Plugins Root
XrmToolBox requires plugin DLLs to be in the `Plugins\` root folder for discovery via MEF.

### 2. Dependencies in Subfolder
Dependencies are placed in a `Plugins\DataverseToPowerBI\` subfolder to:
- Avoid polluting the shared Plugins folder
- Prevent version conflicts with other plugins
- Keep all plugin-specific files organized together

### 3. Template Location
The PBIP_DefaultTemplate folder is deployed to the subfolder and the plugin discovers it at runtime:
```csharp
var pluginFolder = Path.GetDirectoryName(GetType().Assembly.Location);
_templatePath = Path.Combine(pluginFolder, "PBIP_DefaultTemplate");
```

## Building from Source

### Prerequisites
- Visual Studio 18 or later with .NET Framework 4.8 SDK
- XrmToolBox installed (provides SDK assemblies)

### Build Commands

1. Build Core library:
```powershell
dotnet build DataverseToPowerBI.Core -c Release
```

2. Build XrmToolBox plugin:
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" `
    DataverseToPowerBI.XrmToolBox\DataverseToPowerBI.XrmToolBox.csproj `
    /p:Configuration=Release /t:Build
```

### Deployment Script

```powershell
# Create plugin subfolder
$pluginFolder = "$env:APPDATA\MscrmTools\XrmToolBox\Plugins\DataverseToPowerBI"
if (-not (Test-Path $pluginFolder)) { New-Item -ItemType Directory -Path $pluginFolder -Force }

# Deploy plugin DLL to Plugins root
Copy-Item "DataverseToPowerBI.XrmToolBox\bin\Release\DataverseToPowerBI.XrmToolBox.dll" `
    -Destination "$env:APPDATA\MscrmTools\XrmToolBox\Plugins\" -Force

# Deploy dependencies to subfolder
Copy-Item "DataverseToPowerBI.XrmToolBox\bin\Release\DataverseToPowerBI.Core.dll" `
    -Destination $pluginFolder -Force

# Deploy template folder
Copy-Item "PBIP_DefaultTemplate" -Destination "$pluginFolder\PBIP_DefaultTemplate" -Recurse -Force
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                           XrmToolBox                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                 TmdlPluginTool : PluginBase                  │   │
│  │  [Export(typeof(IXrmToolBoxPlugin))]                         │   │
│  │  GetControl() → PluginControl                                │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              PluginControl : PluginControlBase              │   │
│  │  UpdateConnection(IOrganizationService, ConnectionDetail)  │   │
│  │  → Creates XrmServiceAdapterImpl                            │   │
│  │  → Uses WorkAsync for background operations                 │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │          XrmServiceAdapterImpl : IDataverseConnection       │   │
│  │  Uses IOrganizationService for SDK operations               │   │
│  │  - GetSolutionsSync()                                        │   │
│  │  - GetSolutionTablesSync()                                   │   │
│  │  - GetAttributesSync()                                       │   │
│  │  - GetFormsSync() / GetViewsSync()                          │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                       │
│                              ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              DataverseToPowerBI.Core (.NET Standard 2.0)    │   │
│  │  Shared Models:                                              │   │
│  │  - DataverseSolution, TableInfo, AttributeMetadata          │   │
│  │  - ExportTable, ExportRelationship, etc.                    │   │
│  │  Shared Services:                                            │   │
│  │  - SemanticModelBuilder                                      │   │
│  │  - FetchXmlToSqlConverter                                   │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

## Current Status

### Implemented
- ✅ Plugin loads in XrmToolBox
- ✅ Connection integration with XrmToolBox connection manager
- ✅ Load solutions using SDK
- ✅ Load solution tables using SDK
- ✅ Get attributes using SDK
- ✅ Get forms/views using SDK
- ✅ Template folder deployment

### Not Yet Implemented
- ⏳ Full UI matching standalone configurator
- ⏳ Table selection with checkbox list
- ⏳ Form/View selector integration
- ⏳ Star-schema configuration
- ⏳ Calendar table configuration
- ⏳ Build semantic model button

## NuGet Packaging (Future)

To package as NuGet for XrmToolBox Plugin Store:

1. Create `.nuspec` file with plugin metadata
2. Include all required files:
   - Plugin DLL
   - Core DLL
   - PBIP_DefaultTemplate folder
   - Icon image
3. Publish to nuget.org

```xml
<?xml version="1.0"?>
<package>
  <metadata>
    <id>DataverseToPowerBI.XrmToolBox</id>
    <version>1.0.0</version>
    <authors>Your Name</authors>
    <description>Extract Dataverse metadata to Power BI TMDL/PBIP semantic models</description>
    <tags>XrmToolBox Dataverse PowerBI TMDL PBIP</tags>
  </metadata>
  <files>
    <file src="bin\Release\DataverseToPowerBI.XrmToolBox.dll" target="lib\net48\" />
    <file src="bin\Release\DataverseToPowerBI.Core.dll" target="lib\net48\DataverseToPowerBI\" />
    <file src="..\PBIP_DefaultTemplate\**\*" target="lib\net48\DataverseToPowerBI\PBIP_DefaultTemplate\" />
  </files>
</package>
```
