# Changelog

All notable changes to the Dataverse to Power BI Semantic Model XrmToolBox plugin are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.2026.3.0] - 2026-02-16

### Added

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

- **Money/Decimal Data Type Mapping** — Both TDS and FabricLink now map `money` and `decimal` types to `double` (matching Power BI Desktop's runtime behavior) to eliminate false "dataType changed" notifications on incremental rebuilds

- **Status/State Metadata JOINs (FabricLink)** — `statecode` uses `Base.statecode` (not `Base.statecodename`) and JOINs on `State` column (not `Option`); `statuscode` JOINs on both `State` and `Option` columns for correct label resolution

- **Stale Artifact Cleanup** — When switching between TDS and FabricLink modes, the builder now removes stale artifacts from the previous mode (e.g., removes `expressions.tmdl` when building TDS, removes `DataverseURL.tmdl` when building FabricLink)

### Fixed

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
