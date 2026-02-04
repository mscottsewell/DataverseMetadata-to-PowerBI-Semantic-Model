# Changelog

All notable changes to the Dataverse to Power BI Semantic Model XrmToolBox plugin are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

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
