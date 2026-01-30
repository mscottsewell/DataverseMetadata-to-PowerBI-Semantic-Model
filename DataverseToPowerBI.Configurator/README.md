# Dataverse Metadata Extractor - Windows Forms Application

## Overview

Windows Forms application for extracting metadata from Dataverse environments. Provides a native C# application with reliable visual feedback for selecting tables, forms, views, and attributes from Dataverse solutions.

## Features

- **OAuth Authentication**: MSAL-based authentication to Dataverse environments
- **Solution Browser**: Navigate and select from available Dataverse solutions
- **Table Viewer**: Browse tables within selected solutions
- **Form/View Selection**: Choose forms and views for each table with visual editor
- **Attribute Selection**: 
  - View all attributes with display name, logical name, and type
  - Select/deselect individual attributes with visual feedback (checkboxes + row highlighting)
  - Bulk select/deselect all attributes
  - Toggle between "Selected" and "All" attribute views
  - Lookup relationship tracking (Targets property)
- **Metadata Export**: Export selected attributes with table metadata to JSON
- **Caching**: 24-hour metadata cache for improved performance
- **Settings Persistence**: Saves last used environment URL, selections, and preferences

## Architecture

### Models (DataModels.cs)

- `AppSettings`: User preferences and table/attribute selections
- `TableDisplayInfo` / `AttributeDisplayInfo`: Display metadata with JSON serialization
- `MetadataCache`: 24hr cache with validity check
- `DataverseSolution`, `TableInfo`, `TableMetadata`, `AttributeMetadata`: Dataverse objects
- `FormMetadata`, `ViewMetadata`: Form/view definitions with field extraction
- `ExportMetadata`, `ExportTable`, `ExportForm`, `ExportView`: Export package structure

### Services

- `SettingsManager`: JSON persistence to AppData folder
- `DataverseClient`: HTTP client for Dataverse Web API
  - OAuth with MSAL (interactive/silent auth)
  - Solution/table/attribute queries
  - Form metadata with XML parsing
  - View metadata with FetchXML parsing

### UI (Forms/)

- `MainForm`: Main application window with SplitContainer layout
- `TableSelectorDialog`: Dialog for selecting tables from solution
- `FormViewSelectorDialog`: Dialog for selecting form/view per table

## Building & Running

### Quick Start

```batch
..\LaunchExtractor.bat
```

### Manual Build

```powershell
dotnet build --configuration Release
dotnet run
```

### Debug Build

```powershell
dotnet build
.\bin\Debug\net8.0-windows\DataverseMetadataExtractor.exe
```

## Dependencies

- .NET 8.0 Runtime (Windows)
- Microsoft.Identity.Client 4.61.3 (OAuth/MSAL)
- Newtonsoft.Json 13.0.3 (JSON serialization)

## Cached Data Location

```
%APPDATA%\DataverseMetadataExtractor\
├── .dataverse_metadata_settings.json   # User preferences
└── .dataverse_metadata_cache.json      # Metadata cache (auto-expires after 24 hours)
```

## Authentication

Uses the official Dataverse OAuth App Registration:
- **Client ID**: 51f81489-12ee-4a9e-aaae-a2591f45987d
- **Authority**: https://login.microsoftonline.com/organizations
- Interactive browser auth flow for initial login
- Silent token refresh for subsequent requests

