# Changelog

All notable changes to the Dataverse to Power BI Semantic Model XrmToolBox plugin are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [1.2026.4.16] - 2026-02-10

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
