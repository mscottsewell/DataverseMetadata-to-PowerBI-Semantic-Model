# Changelog

All notable changes to the Dataverse to Power BI Semantic Model XrmToolBox plugin are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.2026.5.21] - 2026-02-14

### Added

- **Storage Mode Support** — Full storage mode selection for semantic models: Direct Query, Dual - All Dimensions, Dual - Select Tables, and Import
  - **Direct Query**: All tables use `directQuery` partition mode (existing default behavior)
  - **Dual - All Dimensions**: Fact tables remain `directQuery`, all dimension tables use `dual` mode
  - **Dual - Select Tables**: Per-table storage mode overrides — individually toggle dimension tables between `directQuery` and `dual`
  - **Import**: All Dataverse/FabricLink tables use `import` mode; automatically deletes `cache.abf` when switching away from Import
  - Storage mode persisted per semantic model configuration
  - Auto-strips `current_user` view filters when appropriate (Import: always; Dual: on dimensions; DualSelect: on dual tables)

- **Per-Table Storage Mode Editing** — Mode column in Selected Tables list shows each table's storage mode
  - Always visible with values: "Direct Query", "Dual", or "Import"
  - Click a dimension table's Mode cell to toggle between Direct Query ↔ Dual
  - Automatically switches model to "Dual - Select Tables" mode when individual table modes are changed, with user notification
  - Fact tables and Import mode tables are read-only

- **Export/Import Configurations** — Export or import individual semantic model configurations as JSON files
  - Export saves selected model's full configuration (settings, table selections, storage modes) to a `.json` file
  - Import loads a configuration file, auto-renames if name conflicts, and reminds user to update folder paths
  - Enables sharing configurations between machines or team members

- **Vista Folder Picker** — Modern Windows Explorer-style folder selection dialogs
  - Replaced legacy `FolderBrowserDialog` with Vista `IFileOpenDialog` (COM interop, no external dependencies)
  - Full navigation, search, breadcrumb bar, and favorites pane
  - Applied to all 4 folder selection locations (Working Folder and PBIP Template in both Manager and New Model dialogs)

- **Attribute List Performance** — Pre-built sorted attribute cache during metadata loading
  - Attributes sorted once during refresh, cached for instant display when switching tables or filter modes
  - Uses `Items.AddRange()` batch insertion instead of individual adds
  - Added `_isLoading` guard to prevent `ItemChecked` events from corrupting selection state during list population

### Changed

- **Semantic Model Manager Dialog** — Widened from 935px to 1050px for better content display
  - Added Storage Mode column to model list (Name, Fact Table, Tables, Mode, Connection, Last Used)
  - Storage Mode moved above Connection Type in details panel
  - Last Used column now shows date-only (was date+time) to prevent truncation
  - Column widths optimized to fit within list pane without horizontal scrolling

- **Selected Tables Column Widths** — Adjusted for better proportions: Table=90, Mode=90, Form=90, Filter=100, Attrs=30
  - Fixed `ResizeTableColumns()` resize handler to respect new proportions (was overriding Designer widths)
  - Mode column included in proportional resize calculations

### Fixed

- **DATEDIFF Bug in FetchXML Converter** — Fixed `older-than-x-*` operators passing integer where date expression was expected
  - `DATEDIFF(day, column, 30)` → `DATEDIFF(day, column, GETDATE()) > 30`

- **Date Table Relationship Display Names** — Date table relationships now honor user display name overrides
  - Previously used logical name even when display name alias was configured

- **Font GDI Resource Leaks** — Cached Font objects in `PluginControl` and `TmdlPreviewDialog` to prevent GDI handle exhaustion
  - Added proper `Dispose` overrides to clean up cached fonts

---

## [1.2026.5.1] - 2026-02-12

### Added

- **Visual Grouping for Multiple Relationships** — Dimensions with multiple relationships are now visually grouped in the relationship selector
  - Group headers display dimension name with "(Multiple Relationships)" label
  - Only tables with 2+ relationships are grouped; single relationships remain ungrouped for clarity
  - Applied to both main Fact/Dimension selector dialog and "Add Parent Tables" snowflake dialog
  - Makes it easier to identify which relationships belong to the same dimension

- **Solution Tables Filter** — Added "Solution tables only" checkbox next to Solution dropdown
  - Enabled by default to show only relationships to tables in the current solution
  - Unchecking shows relationships to all tables (including those outside the solution)
  - Works seamlessly with search filter
  - Reduces clutter when working with specific solutions

- **Relationship Search** — Added search box to filter relationships by field names and dimension table names
  - Real-time filtering as you type
  - Searches across: Lookup Field name, Lookup Logical Name, Target Table name, Target Logical Name
  - Validation still checks all relationships (not just filtered ones)
  - Preserves checked state when filtering

### Changed

- **Default Relationship Status** — All relationships now default to "Active" status
  - Previous behavior: Multi-relationship dimensions defaulted to "Inactive"
  - New behavior: All relationships start as "Active" for clearer initial state
  - Users explicitly choose which relationship to make inactive when multiple exist

- **Smart Relationship Selection** — Enhanced automatic inactivation when selecting relationships
  - Checking a relationship automatically marks ALL other relationships to that same dimension as "Inactive"
  - Applied whether the other relationships are checked or not
  - Double-clicking a relationship to make it Active automatically inactivates all others to that dimension
  - Prevents conflicts and ensures only one active relationship per dimension

- **Column Width Optimization** — Adjusted column widths for better data visibility
  - **Main grid (Selected Tables & Forms)**: Type column increased from 100px to 140px; Filter column now gets 50% of flexible space (up from 33.33%)
  - **Main grid (Attributes)**: Type column increased from 100px to 140px to prevent truncation of "Uniqueidentifier" and other long type names
  - **Fact/Dimension Selector**: Reduced width from 1050px to 900px (150px narrower); column widths adjusted to fit
  - **Add Parent Tables dialog**: Lookup Field and Parent Table columns reduced to 150px (75% of original 200px) to fit Logical Name column without scrolling

- **Dialog Layout Improvements** — Reorganized Fact/Dimension selector for better usability
  - "Include one-to-many relationships" checkbox moved to left (first position)
  - Search box moved to far right, aligned with grid's right edge
  - Finish and Cancel buttons repositioned to remain fully visible at new dialog width

- **Improved Relationship Conflict Highlighting** — Enhanced visual indicators for relationship status
  - Type column now displays "(Inactive)" suffix for inactive relationships (e.g., "Direct (Inactive)")
  - Red highlighting (light salmon) now only appears when multiple ACTIVE relationships exist to the same dimension (conflict state)
  - Inactive relationships no longer highlighted — clean white background for clarity
  - Applied to both main Fact/Dimension selector and "Add Parent Tables" snowflake dialog
  - Makes it immediately clear which relationships have conflicts vs. which are safely inactive

### Fixed

- **NullReferenceException in ItemChecked Event** — Fixed crash when opening Fact/Dimension selector dialog
  - Added `_suppressItemCheckedEvent` flag to prevent events during list manipulation
  - Added comprehensive null checks in all event handlers (`ItemChecked`, `DoubleClick`, `UpdateItemStatus`, `UpdateSnowflakeButtonState`)
  - Protected LINQ queries with `.Where(i => i.Tag != null)` filters
  - Fixed iteration to use `_allRelationshipItems` instead of `listViewRelationships.Items` for complete coverage
  - Applied same safety features to snowflake "Add Parent Tables" dialog

- **FabricLink Current User Filter** — FetchXML filters with current user operators now handled correctly for FabricLink
  - User context operators (`eq-userid`, `ne-userid`, `eq-userteams`, `ne-userteams`) are now skipped in FabricLink mode
  - These operators continue to work with DataverseTDS connection mode
  - Skipped operators are logged as "not supported in FabricLink (use TDS for current user filters)"
  - Prevents SQL syntax errors in Direct Lake scenarios that don't support `CURRENT_USER` constructs
  - Debug logs clearly indicate which operators were skipped due to FabricLink limitations

---

## [1.2026.4.16] - 2026-02-10

### Added

- **TMDL Preview Icon** — Preview TMDL toolbar button now displays with a dedicated preview icon for better visual identification
  - Icon image: `TMDLPreviewIcon.png` loaded via `RibbonIcons.PreviewIcon`
  - All ribbon toolbar buttons now have consistent icon styling

- **Virtual Column Name Corrections** — Table-scoped correction dictionary for problematic virtual columns that don't exist in TDS endpoint
  - Format: `"tablename.incorrectcolumnname" → "correctcolumnname"`
  - Example: `"contact.donotsendmmname" → "donotsendmarketingmaterial"`
  - Prevents SQL errors from bad metadata by applying corrections automatically
  - Applied across all three generation methods (TMDL export, change analysis, comparison queries)
  - Add new corrections to `VirtualColumnCorrections` dictionary in `SemanticModelBuilder.cs`

- **Enhanced Column Descriptions** — TMDL column descriptions now include comprehensive metadata
  - **Dataverse Description**: User-provided description from attribute metadata (if available) shown first
  - **Source Attribution**: `Source: {tableLogicalName}.{attributeLogicalName}` format for clear data lineage
  - **Lookup Targets**: Target table names for lookup/polymorphic lookup columns
  - Example: `"The primary contact for the account | Source: account.primarycontactid | Targets: contact"`
  - Applied to all column types (regular, lookup, choice/boolean, multi-select, primary keys, relationship-required lookups)
  - `Description` property added to `AttributeMetadata` model, populated from XRM SDK metadata
  - `BuildDescription()` method refactored to accept table/attribute names and Dataverse description

### Changed

- **TMDL Preview Sort Order** — Reordered preview list for better usability and logical flow
  - **New order**: Fact Tables → Dimension Tables → Date Table → Expressions (alphabetically within each category)
  - **Previous order**: Expressions → Date Table → Fact Table → Dimension Tables
  - `TmdlEntryType` enum values changed: `FactTable=0, DimensionTable=1, DateTable=2, Expression=3`
  - More intuitive for users to find their primary data tables first

- **TMDL Preview SQL Formatting** — Fixed line breaks in Windows Forms TextBox display
  - Replaced all embedded `\n` (LF) with `\r\n` (CRLF) in SQL SELECT list, JOIN clauses, and OUTER APPLY subqueries
  - Windows Forms TextBox requires CRLF for proper line rendering (LF-only appears on same line)
  - Affected areas: SELECT field continuation, State/Status/Boolean/Picklist JOINs, multi-select OUTER APPLY
  - Fixed: `FROM {table} as Base` → `FROM {table} AS Base` (uppercase AS for consistency)

- **UseDisplayNameAliasesInSql Default Handling** — Added `[OnDeserializing]` callback to ensure correct default value
  - `DataContractJsonSerializer` bypasses constructors and property initializers
  - Models saved before the aliasing feature existed were loading with `false` instead of declared `true` default
  - `SetDefaults()` method in `SemanticModelConfig` sets `UseDisplayNameAliasesInSql = true` before deserialization

### Fixed

- **PrepareExportData Bug Fixes** — Critical metadata propagation issues resolved
  - `HasStateCode` now correctly set on `ExportTable` by checking if `_tableAttributes` contains `statecode` attribute
  - `IsGlobal` and `OptionSetName` now copied from `AttributeMetadata` to `AttributeDisplayInfo`
  - Prevents missing WHERE clause in SQL queries (when `HasStateCode` was false incorrectly)
  - Ensures FabricLink JOINs use correct metadata table (`GlobalOptionsetMetadata` vs `OptionsetMetadata`)

- **Build Warnings Eliminated** — Removed all NuGet package conflicts and nullable reference warnings
  - Fixed 2× MSB3277 assembly version conflicts (System.ValueTuple, System.Text.Json)
  - Fixed 6× CS8618 non-nullable field warnings in `TmdlPreviewDialog.cs` (added `= null!` initialization)
  - Removed `System.ValueTuple` NuGet package (built into .NET Framework 4.8)
  - Upgraded `System.Text.Json` from 8.0.0 to 8.0.5 (then removed entirely as unused)
  - Removed ALL 17 unused NuGet packages left from deleted `SqlQueryValidator` (Azure.Identity, Azure.Core, Microsoft.Data.SqlClient, etc.)
  - Emptied `packages.config` and deleted all package folders
  - **Build now succeeds with 0 warnings and 0 errors**

---

## [1.2026.4.10] - 2026-02-10

### Added

- **TMDL Preview Feature** — Replaced "View SQL" with "Preview TMDL" showing the exact TMDL statements that will be written to the semantic model
  - Displays sorted preview list with automatic categorization: Expressions → Date Table → Fact Table → Dimensions (alphabetically)
  - **Connection Mode Awareness**: 
    - TDS mode: Shows "DataverseURL" parameter table as Expression type
    - FabricLink mode: Shows both "Expressions" (FabricSQLEndpoint + FabricLakehouse parameters) and "DataverseURL" table
  - **Date Table Integration**: Date table entry appears automatically if configured, positioned after Expressions and before data tables; omitted if date table is not configured
  - **Preview-to-Build Consistency**: Output is identical to actual build — uses the real `SemanticModelBuilder.GenerateTmdlPreview()` API instead of divergent inline generation
  - Visual distinction by entry type: italic blue text for Config/Date entries, bold for Fact table, normal for Dimensions
  - Type labels in second column: "Config", "Date", "Fact", "Dimension"
  - **Export Capabilities**:
    - Copy selected TMDL to clipboard with one click
    - Save individual `.tmdl` file with sanitized filename based on entry name
    - Save All button exports all entries as separate `.tmdl` files to chosen folder
  - Full TMDL content with UTF-8 encoding (no BOM): complete table declarations, column definitions with data types, measures, hierarchies, and M partition expressions

- **Display Name Aliases in SQL** — Columns can now use display names as SQL aliases (`AS [Display Name]`) for human-readable column names in the semantic model
  - **Per-model toggle**: "Use Display Name Aliases in SQL" checkbox in Semantic Model Settings (enabled by default for new models)
  - **Per-attribute override**: Double-click the Display Name column in the attributes list to set a custom alias for any column; setting it back to the original removes the override
  - **Auto-override for primary name attributes**: Primary name columns automatically get "{Table Name} {Column Name}" aliases to avoid duplicates across tables (e.g., "Account Name" instead of "Name")
  - **Duplicate detection**: Conflicting display name aliases are highlighted in light red in the attributes list; build and preview are blocked with a descriptive error listing all conflicts
  - **Override indicators**: Overridden display names show an asterisk (*) suffix in the attributes list
  - **Full TMDL integration**: Aliases are applied consistently across `GenerateTableTmdl`, `GenerateMQuery`, and `GenerateExpectedColumns` for all column types (regular, lookup, choice/boolean, multi-select, datetime-wrapped)
  - **Settings persistence**: Overrides are saved per-table and restored when loading a semantic model

### Changed

- **SQL Preview → TMDL Preview** — Renamed toolbar button from "Validate SQL" (btnValidateSql) to "Preview TMDL" (btnPreviewTmdl)
  - Shows actual TMDL output that matches the build output exactly
  - No longer shows divergent inline SQL generation (eliminated ~200 lines of out-of-sync code)
  - Uses the real `SemanticModelBuilder.GenerateTmdlPreview()` API instead of duplicated preview logic

- **SemanticModelBuilder Refactoring** — Extracted reusable components for preview generation
  - Added public `GenerateTmdlPreview()` method to generate full TMDL preview with all entry types
  - Extracted private `GenerateDataverseUrlTableTmdl()` method to decouple content generation from file I/O
  - `WriteDataverseUrlTable()` now calls the extracted method for content generation

- **Build Pipeline Optimization** — Extracted `PrepareExportData()` helper in PluginControl
  - Shared method builds `exportTables`, `exportRelationships`, and `attributeDisplayInfo` structures
  - Used by both incremental build (`BuildSemanticModel`) and TMDL preview flows
  - Eliminates code duplication for export data preparation

### Removed

- **Obsolete SQL Validation Infrastructure** — Removed all dependencies and code related to the old SQL validation feature:
  - Deleted `SqlQueryValidator.cs` service class
  - Deleted `SqlQueryValidationDialog.cs` form (replaced by `TmdlPreviewDialog.cs`)
  - Removed NuGet package references: Azure.Identity, Azure.Core, Microsoft.Data.SqlClient, Microsoft.Identity.Client, Microsoft.IdentityModel.Abstractions, System.Memory.Data, Microsoft.Identity.Client.Extensions.Msal
  - Deleted entire `packages/` folder containing NuGet assemblies
  - Reduced project bloat by ~350 MB of unused dependencies

---

## [1.2026.3.10] - 2026-02-08

### Added

- **Pre-Build Integrity Validation** — Automatic detection of incomplete PBIP structures before incremental builds
  - Validates presence of critical files: `.pbip`, `.platform`, `definition.pbism`, `model.tmdl`, `tables/` folder, Report folder
  - If structural files are missing, automatically performs full rebuild instead of failing on incremental update
  - Adds Warning-type changes to preview dialog showing each missing element
  - Prevents `DirectoryNotFoundException` and other build failures from corrupted project folders

- **Relationship-Required Column Locking** — Lookup fields used by active relationships are now automatically locked in the attribute selection UI
  - Lookup columns required for dimension relationships cannot be unchecked while the target dimension is in the model
  - Shows 🔒 icon with blue/bold styling for locked relationship columns
  - Survives metadata refresh and deselect-all operations
  - Prevents relationship breakage from accidental column deselection

- **FabricLink Connection Mode** — Full support for building semantic models that query Dataverse data via Microsoft Fabric Link (Lakehouse SQL endpoint)
  - FabricLink queries use `Sql.Database(FabricSQLEndpoint, FabricLakehouse, [Query="..."])` connector
  - Automatic JOINs to `OptionsetMetadata`, `GlobalOptionsetMetadata`, and `StatusMetadata` tables for human-readable choice/status labels
  - Separate handling of global vs. entity-specific optionsets
  - `INNER JOIN` for state/status metadata; `LEFT OUTER JOIN` for optional choice fields
  - Connection parameters stored as expressions in `expressions.tmdl`

- **Auto-Generated Fact Table Measures** — Two starter measures are automatically created on the fact table:
  - `{TableName} Count` — `COUNTROWS` for quick record counts
  - `Link to {TableName}` — Clickable URL to open records in Dataverse using `WEBURL` DAX function
  - Auto-generated measures are excluded from user measure preservation (they regenerate on each build)

- **Virtual Attribute Support** — Picklist/Boolean attributes now use actual virtual attribute names from metadata instead of assuming `{attributename}name` pattern
  - Handles edge cases like `donotsendmm` → `donotsendmarketingmaterial` (not `donotsendmmname`)
  - Global vs. entity-specific optionset detection for correct FabricLink metadata table JOINs

- **Language Code Parameter** — Builder now accepts a `languageCode` parameter (default 1033/English) for localizing metadata labels in FabricLink queries

### Changed

- **DataverseURL Architecture (TDS)** — DataverseURL is now stored as a hidden parameter *table* with `mode: import` and `IsParameterQuery=true` metadata (Enable Load pattern) instead of an expression
  - Resolves `KeyNotFoundException` that occurred during `CommonDataService.Database` refresh when DataverseURL was an expression
  - The parameter table pattern matches how Power BI Desktop natively handles Power Query parameters

- **DataverseURL Architecture (FabricLink)** — DataverseURL is now a hidden parameter *table* (not an expression in `expressions.tmdl`)
  - Both TDS and FabricLink now use identical DataverseURL table pattern for consistency
  - FabricLink `expressions.tmdl` contains only `FabricSQLEndpoint` and `FabricLakehouse` (DataverseURL moved to table)
  - Enables DAX measure references to work correctly in both connection modes

- **DAX Measure Table References** — Auto-generated measures now always use single-quoted table names: `'{displayName}'` instead of conditional quoting
  - Ensures consistent DAX syntax regardless of table name characters
  - Prevents syntax errors in measures when table names contain spaces or special characters

- **Money/Decimal Data Type Mapping** — Both TDS and FabricLink now map `money` and `decimal` types to `double` (matching Power BI Desktop's runtime behavior) to eliminate false "dataType changed" notifications on incremental rebuilds

- **Status/State Metadata JOINs (FabricLink)** — `statecode` uses `Base.statecode` (not `Base.statecodename`) and JOINs on `State` column (not `Option`); `statuscode` JOINs on both `State` and `Option` columns for correct label resolution

- **Stale Artifact Cleanup** — When switching between TDS and FabricLink modes, the builder now removes stale artifacts from the previous mode (e.g., removes `expressions.tmdl` when building TDS, removes `DataverseURL.tmdl` when building FabricLink)

### Fixed

- **Duplicate Column Generation** — Fixed bug where selecting both a lookup/choice ID field AND its virtual name column caused duplicate columns in TMDL
  - Example: Selecting `pbi_reportedbyid` and `pbi_reportedbyidname` no longer generates `pbi_reportedbyidname` twice
  - Applies to lookup name columns, choice/boolean label columns, and multi-select choice columns
  - All three generation methods (`GenerateTableTmdl`, `GenerateExpectedColumns`, `GenerateMQuery`) now check if synthesized name columns already exist before adding them
  - Prevents SQL query errors from duplicate column names in SELECT list

- Fixed `DataSource.Error` caused by virtual attribute name mismatch (e.g., `donotsendmmname` did not exist — actual name is `donotsendmarketingmaterial`)
- Fixed `KeyNotFoundException` during TDS model refresh by replacing expression-based DataverseURL with parameter table pattern
- Fixed false change detection on incremental rebuild where `double` columns were regenerated as `decimal`
- Fixed FabricLink `statuscode` label resolution to use compound JOIN on both `State + Option` columns

---

## [1.2026.2.47] - 2026-02-04

### Fixed
- Patched a bug that arose when a relationship was tied to a table outside the solution,
and when the Date table referenced a date attribute that wasn't 'selected' in the list of 
Attributes.


## [1.3.22] - 2026-01-31

### Added

- Date table now displays in the "Selected Tables & Forms" list after configuration
- Calendar icon (📅) and green styling for easy identification
- Year range shown in Filter column (e.g., "2020-2030")
- `AddDateTableToDisplay()` method that mirrors the Configurator's implementation

### Changed

- Date table entry positioned after Fact table for logical grouping
- UI refresh flow updated to show Date table when loading existing models

---

## [1.3.21] - 2026-01-31

### Fixed

- Date table relationship now displays in Relationships list after configuration
- `UpdateRelationshipsDisplay()` called after CalendarTableDialog returns successfully
- Relationship shows as: `{SourceTable}.{DateField}` → `Date Table 📅` (Active Date)

---

## [1.3.20] - 2025-01-20

### Added

- **Calendar/Date Table Configuration Dialog** (698 lines) with:
  - Primary date field selection (combo box with table + field selection)
  - Full Windows timezone support with UTC offset display
  - Year range configuration (start/end year with numeric controls)
  - Additional DateTime field wrapping across all selected tables
  - Multi-select checkbox list for DateTime field preview
  - Select All / Clear All buttons

- **Configuration Persistence** - DateTableConfig saved in PluginSettings

### Changed

- **Unified Type System**:
  - Removed duplicate `DateTableConfig` class from `XrmToolBox.Models`
  - Now uses `Core.Models.DateTableConfig` throughout the codebase
  - Added type aliasing with `using XrmModels = DataverseToPowerBI.XrmToolBox.Models`

- **Build Integration**:
  - `BuildSemanticModel` now passes `dateTableConfig` to semantic model builder
  - `AnalyzeChanges` and `ApplyChanges` accept DateTableConfig parameter

### Fixed

- `string.Contains()` overload compatibility with .NET Framework 4.8
- Type conversion errors between Core.Models and XrmToolBox.Models

---

## [1.3.0] - 2025-01-15

### Added

- Initial XrmToolBox plugin release
- Star-schema configuration with fact and dimension tables
- Form and view selection for column filtering
- FetchXML to SQL WHERE clause conversion
- TMDL generation for Power BI semantic models
- Relationship auto-detection from lookup fields
- Incremental update support with user measure preservation

---
