# Implementation Summary: TMDL Preview

## Overview
Successfully implemented a comprehensive TMDL Preview feature that displays the exact TMDL code that will be generated for the semantic model. This replaced the previous "View SQL" functionality with a full-fidelity preview system that matches build output exactly.

## Current Implementation (February 2026)

### 1. TMDL Preview Dialog (TmdlPreviewDialog.cs)
**Features:**
- Sorted table list with automatic categorization (Fact → Dimension → Date → Expression)
- Connection mode awareness (TDS vs FabricLink)
- Full TMDL content display with syntax highlighting via Consolas font
- Export capabilities: Copy, Save, Save All

**UI Components:**
- Table list (left panel) with Type column showing entry category
- TMDL content viewer (right panel) with read-only TextBox
- Visual distinction: italic blue for Config/Date, bold for Fact, normal for Dimensions
- Buttons: Copy, Save, Save All, Close

**Table Sort Order:**
1. Fact Tables (bold styling)
2. Dimension Tables (normal styling)
3. Date Table (italic blue if configured)
4. Expressions (italic blue - DataverseURL, FabricLink parameters)

### 2. Preview Generation Architecture

**SemanticModelBuilder.cs - Key Methods:**

```csharp
public Dictionary<string, TmdlPreviewEntry> GenerateTmdlPreview(
    exportTables, 
    exportRelationships, 
    attributeDisplayInfo,
    semanticModelName,
    dateTableConfig)
```

**Preview Entry Types (TmdlEntryType enum):**
- `FactTable = 0`
- `DimensionTable = 1`
- `DateTable = 2`
- `Expression = 3`

**Content Generation:**
- Uses actual build methods (`GenerateTableTmdl`, `GenerateDataverseUrlTableTmdl`)
- UTF-8 without BOM encoding
- CRLF line endings for Windows Forms TextBox compatibility
- Identical output to actual build files

### 3. Display Name Aliases Integration

**Features Added (v1.2026.4.16):**
- Per-model toggle: "Use Display Name Aliases in SQL"
- Per-attribute override: Double-click display name to edit
- Auto-override for primary name attributes to avoid duplicates
- Duplicate detection with visual highlighting (light red background)
- Override indicators (asterisk suffix in UI)
- Full TMDL integration across all column types

**SQL Alias Format:**
```sql
SELECT Base.accountid
      ,Base.name AS [Account Name]
      ,Base.primarycontactidname AS [Primary Contact]
```

### 4. Enhanced Column Descriptions

**TMDL Description Format:**
```tmdl
/// {Dataverse Description} | Source: {table}.{attribute} | Targets: {target tables}

column [Display Name]
    dataType: string
    sourceColumn: name
```

**Components:**
1. Dataverse metadata description (if available)
2. Source attribution: `Source: table.attribute`
3. Lookup targets (for lookup fields)

**Example:**
```tmdl
/// The primary contact for the account | Source: account.primarycontactid | Targets: contact

column 'Primary Contact'
    dataType: string
    isHidden
```

### 5. Virtual Column Corrections

**Problem Solved:**
Some virtual column names in metadata don't exist in TDS endpoint.

**Solution:**
Dictionary-based corrections in `SemanticModelBuilder.cs`:

```csharp
private static readonly Dictionary<string, string> VirtualColumnCorrections = new(StringComparer.OrdinalIgnoreCase)
{
    { "contact.donotsendmmname", "donotsendmarketingmaterial" }
};
```

**Format:** `"tablename.incorrectcolumnname" → "correctcolumnname"`

**Applied In:**
- GenerateTableTmdl (main TMDL export)
- GenerateExpectedColumns (change analysis)
- GenerateMQuery (comparison queries)

## Technical Implementation Details

### CRLF Line Ending Fixes
Windows Forms TextBox requires `\r\n` (CRLF) for line breaks. All SQL formatting now uses CRLF:
- SELECT field continuation lines
- JOIN ON conditions
- OUTER APPLY subqueries

**Before:**
```csharp
sqlSelectList.Append($"\n\t\t\t\t        ,{sqlFields[i]}");  // LF only - renders on same line
```

**After:**
```csharp
sqlSelectList.Append($"\r\n\t\t\t\t        ,{sqlFields[i]}");  // CRLF - proper line break
```

### UseDisplayNameAliasesInSql Default Handling

**Problem:**
DataContractJsonSerializer bypasses property initializers, causing old models to default to `false` instead of `true`.

**Solution:**
```csharp
[OnDeserializing]
private void SetDefaults(StreamingContext context)
{
    UseDisplayNameAliasesInSql = true;
}
```

### Metadata Propagation Fixes

**PrepareExportData() Bug Fixes:**
- `HasStateCode` now correctly set by checking for `statecode` attribute
- `IsGlobal` and `OptionSetName` copied from `AttributeMetadata` to `AttributeDisplayInfo`
- Prevents incorrect WHERE clauses and wrong FabricLink metadata table JOINs

## Files Modified

### Core Implementation
1. **TmdlPreviewDialog.cs** (260 lines) - Preview dialog UI
2. **SemanticModelBuilder.cs** (~3,500 lines) - TMDL generation engine
   - `GenerateTmdlPreview()` - Public preview API
   - `GenerateDataverseUrlTableTmdl()` - Extracted content generation
   - `VirtualColumnCorrections` - Correction dictionary
   - `BuildDescription()` - Enhanced description builder
3. **PluginControl.cs** (~3,300 lines) - Main plugin controller
   - `BtnPreviewTmdl_Click()` - Preview invocation
   - `PrepareExportData()` - Shared export data preparation
4. **RibbonIcons.cs** - Added `PreviewIcon` property
5. **DataModels.cs** - Added `Description` property to `AttributeMetadata`
6. **XrmServiceAdapterImpl.cs** - Capture description from SDK metadata

### UI Changes
- Button renamed: "Validate SQL" → "Preview TMDL"
- Icon added: `TMDLPreviewIcon.png` via `RibbonIcons.PreviewIcon`
- Tooltip updated to reflect TMDL preview functionality

## Build Status
✅ **Build Successful** - Release configuration
- ✅ 0 compilation errors
- ✅ 0 warnings (all NuGet conflicts and nullable warnings resolved)
- ✅ All unused NuGet packages removed (17 packages deleted)

## Quality Improvements

### Package Cleanup
Removed all unused dependencies from deleted SqlQueryValidator:
- Azure.Identity, Azure.Core
- Microsoft.Data.SqlClient
- Microsoft.Identity.Client
- System.Text.Json (unused - project uses DataContractJsonSerializer)
- System.ValueTuple (built into .NET 4.8)
- 17 total packages removed
- ~350 MB reduction in project size

### Build Warnings Resolved
- Fixed MSB3277 assembly conflicts (ValueTuple, Text.Json)
- Fixed CS8618 nullable reference warnings (added `= null!` initializers)
- All 8 build warnings eliminated

## Usage Flow

1. **Configure Model** - Select tables, forms, views, relationships
2. **Customize Display Names** - Double-click to override, auto-detect duplicates
3. **Preview TMDL** - Click "Preview TMDL" button
4. **Review Output** - See exact TMDL code organized by table type
5. **Export if Needed** - Copy individual tables or save all to folder
6. **Build Model** - Generate actual PBIP project with identical output

## Preview vs. Build Consistency

**Guarantee:**
Preview output is **identical** to build output because both use the same methods:
- `GenerateTableTmdl()` - Table TMDL generation
- `GenerateDataverseUrlTableTmdl()` - DataverseURL table
- `GenerateMQuery()` - Comparison queries (not shown in preview)

**No Code Duplication:**
Eliminated ~200 lines of divergent inline SQL generation from previous "View SQL" implementation.

## Next Steps for Users

After previewing:
1. Review column descriptions for data lineage clarity
2. Verify display name aliases don't conflict
3. Check FetchXML WHERE clause conversion
4. Inspect relationship definitions
5. Build model with confidence

---
**Current Version:** 1.2026.4.16+  
**Last Updated:** February 10, 2026  
**Status:** ✅ Production Ready
