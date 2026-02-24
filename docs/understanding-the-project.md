# Understanding Your Generated Project

The project creates a **Power BI Project (PBIP)** which is a format that allows for editing, change management and collaboration. - A PBIP can be saved as a PBIX file for distribution by simply clicking save-as and choosing the PBIX format.

The tool creates a PBIP folder structure. The exact layout depends on your connection mode:

## Dataverse TDS Mode (Default)

```
YourModelName/
├── YourModelName.pbip              ← Open this file in Power BI Desktop
├── YourModelName.SemanticModel/
│   └── definition/
│       ├── model.tmdl              ← Model configuration and table references
│       ├── relationships.tmdl      ← Table relationships
│       └── tables/                 ← Individual table definitions
│           ├── DataverseURL.tmdl   ← Hidden parameter table (connection URL)
│           ├── Date.tmdl           ← Calendar dimension (if configured)
│           ├── Account.tmdl
│           └── Contact.tmdl ...
└── YourModelName.Report/
    └── (report definition files)
```

## FabricLink Mode

```
YourModelName/
├── YourModelName.pbip              ← Open this file in Power BI Desktop
├── YourModelName.SemanticModel/
│   └── definition/
│       ├── model.tmdl              ← Model configuration and expression references
│       ├── expressions.tmdl        ← DataverseURL, FabricSQLEndpoint, FabricLakehouse
│       ├── relationships.tmdl      ← Table relationships
│       └── tables/                 ← Individual table definitions
│           ├── Date.tmdl           ← Calendar dimension (if configured)
│           ├── Account.tmdl
│           └── Contact.tmdl ...
└── YourModelName.Report/
    └── (report definition files)
```

> **Key difference:** In TDS mode, the Dataverse URL is stored as a hidden parameter *table* (`DataverseURL.tmdl`) — this is required for Power BI Desktop to properly resolve the `CommonDataService.Database` connector. In FabricLink mode, connection details are stored as *expressions* in `expressions.tmdl`.

## About PBIP Format

The PBIP format is a folder-based project that's perfect for:
- Version control with Git
- Collaboration with other developers
- Seeing exactly what changed between versions

## Learn More About PBIP

- [Power BI Project Files Overview](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-overview)
- [Working with PBIP in Power BI Desktop](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-build)
- [Power BI Desktop Developer Mode](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-overview#developer-mode)

## Attribute Grid Behavior (Configuration UI)

The table configuration pane in the plugin includes grouped behavior for lookup and choice fields.

- **Show: Selected mode:** Selecting a grouped parent row shows all child rows for that parent, including both included and excluded child fields.
- **Group expansion defaults:** Groups are collapsed by default. Use **Open all groups** and **Collapse all groups** for bulk control.
- **Persisted group state:** Group open/collapse state is saved in the semantic model configuration and restored on reopen.
- **Expanded child controls:** Expanded lookup child rows support **Include** and **Hidden** toggles directly in the main grid.
- **Include/Hidden rule:** **Hidden** implies **Include**. If Include is turned off for an expanded child row, the row remains visible but is excluded from generated output.
