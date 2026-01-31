# XrmToolBox Plugin Deployment - Important Notes

## ⚠️ Why Prototype Can't Be Deployed Yet

The error you encountered:
```
System.Reflection.ReflectionTypeLoadException
Unable to load one or more of the requested types.
```

This occurred because:
1. **XrmToolBox scans ALL DLLs** in the Plugins folder for MEF exports
2. **Core library has external dependencies** (Microsoft.Identity.Client, Newtonsoft.Json) that aren't available in XrmToolBox's context
3. **No plugin DLL exists yet** - we only have the Core library

## ✅ What Was Done

The Core library (`DataverseToPowerBI.Core.dll`) has been removed from the XrmToolBox Plugins folder. XrmToolBox should now load correctly.

## 🚀 Proper Deployment Requirements

To deploy a working XrmToolBox plugin, you need:

### 1. Complete Plugin DLL
A properly compiled `DataverseToPowerBI.XrmToolBox.dll` that:
- References XrmToolBox SDK correctly
- Implements proper MEF export attributes
- Doesn't have C# compilation errors

### 2. Include All Dependencies
When deploying, copy to a subfolder:
```
C:\Users\misewell\AppData\Roaming\MscrmTools\XrmToolBox\Plugins\
└── DataverseToPowerBI\
    ├── DataverseToPowerBI.XrmToolBox.dll    ← Plugin DLL (must be in root)
    ├── DataverseToPowerBI.Core.dll          ← Core library
    ├── Microsoft.Identity.Client.dll        ← MSAL (if needed)
    ├── Newtonsoft.Json.dll                  ← JSON library
    └── (other dependencies)
```

**Critical:** XrmToolBox only scans DLLs in the `Plugins` folder root, not subfolders. So dependencies can safely go in a subfolder.

### 3. Proper Plugin Structure

The plugin DLL must:
```csharp
[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "Your Plugin Name")]
// ... other metadata
public class YourPlugin : PluginFactory
{
    public override IXrmToolBoxPluginControl GetControl()
    {
        return new YourPluginControl();
    }
}
```

## 🔧 Current Blocker

The XrmToolBox plugin code has compilation errors due to:
1. **Missing correct base class references** - Need to find the right XrmToolBox base classes
2. **XrmToolBox SDK not on NuGet** - Requires local XrmToolBox installation
3. **Framework mismatch** - Plugin needs .NET Framework 4.8, XrmToolBox SDK might be 4.8

## 📋 Corrected Deployment Steps

Once the plugin is fully implemented:

### Step 1: Build Plugin
```powershell
cd DataverseToPowerBI.XrmToolBox
dotnet build -c Release  # or use MSBuild
```

### Step 2: Create Plugin Folder Structure
```powershell
$pluginRoot = "C:\Users\misewell\AppData\Roaming\MscrmTools\XrmToolBox\Plugins"

# Plugin DLL goes in root
Copy-Item "bin\Release\DataverseToPowerBI.XrmToolBox.dll" "$pluginRoot\"

# Dependencies go in subfolder
$depFolder = "$pluginRoot\DataverseToPowerBI"
New-Item -ItemType Directory -Path $depFolder -Force

Copy-Item "..\DataverseToPowerBI.Core\bin\Release\netstandard2.0\*.dll" $depFolder
```

### Step 3: Restart XrmToolBox
Close and reopen XrmToolBox - plugin should appear in the list.

## 🎯 Why This Architecture Is Still Correct

Despite the deployment hiccup, the architecture is sound:
- ✅ Core library builds successfully
- ✅ Interface abstraction works
- ✅ .NET Standard 2.0 compatible
- ✅ Zero code duplication achieved

**The only remaining work is:**
1. Fix XrmToolBox SDK references in the plugin project
2. Compile the plugin DLL successfully
3. Deploy using proper folder structure

## 🔗 Next Steps

1. **Research XrmToolBox Plugin Development**
   - Find correct base classes and interfaces
   - Study working plugin examples
   - Understand MEF export requirements

2. **Fix Compilation Errors**
   - Update PluginControl.cs with correct base class
   - Update TmdlPluginTool.cs with correct factory pattern
   - Ensure all references resolve

3. **Test Build**
   - Ensure plugin DLL compiles without errors
   - Verify all dependencies are copied

4. **Deploy & Test**
   - Use proper folder structure
   - Test in XrmToolBox
   - Verify functionality

## 📚 Resources

- XrmToolBox Plugin Development: https://www.xrmtoolbox.com/documentation/for-developers/
- MEF Documentation: https://docs.microsoft.com/en-us/dotnet/framework/mef/
- Sample Plugins: https://github.com/MscrmTools/XrmToolBox/tree/master/Plugins

---

**Status:** Deployment blocked until plugin compilation succeeds  
**Next Action:** Fix XrmToolBox SDK references in plugin project  
**Estimated Time:** 1-2 days for a working plugin
