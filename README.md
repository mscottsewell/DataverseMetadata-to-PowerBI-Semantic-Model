# Dataverse Metadata to Power BI Semantic Model Quickstart

## Project Structure

```
ImaginationWorkship-PowerBI/
├── Reports/                # Power BI Reports with project-specific metadata
│   └── ImaginationWorkshop/
│       ├── Metadata/       # Excel and JSON metadata files
│       │   ├── ImaginationWorkshop Metadata Dictionary.xlsx
│       │   └── ImaginationWorkshop Metadata Dictionary.json
│       └── PBIP/           # Power BI Project files
│           ├── CoreAI Imagination Workshop.pbip
│           ├── CoreAI Imagination Workshop.Report/
│           └── CoreAI Imagination Workshop.SemanticModel/
├── Code/                   # Utility scripts
│   ├── extract_fields.py
│   ├── extract_tables.py
│   └── Archive/            # Legacy scripts
├── Extract-PowerBIMetadata.ps1  # Main metadata extraction tool
└── README.md
```

## Extracting Metadata

- Use the XrmToolBox "Metadata Document Generator" utility to export Dataverse table metadata to an Excel file. 
- Use the 'Load Entities from Solution' option to include only relevant tables.
- Choose the "All Attributes contained in forms", and select a form for each entity you want added to the semantic model in Power BI. (Be sure to select each entity row and select the form for it, one by one.)
- Select the two checkboxes in the lower right corner to create the two summarized tabs.
- **Hint** Use the "Generation settings" option to save a configuration file so that you can return and edit/re-create the file easily if needed.
- Generate the file and save it into the `Reports/[ProjectName]/Metadata/` folder.
- If your organization uses IRM, be sure to set the Excel file's sensitivity label to 'General' to avoid access issues. <img width="800" alt="Metadata Document Generator Screenshot" src="https://github.com/user-attachments/assets/e437ec8c-4513-4db0-bdf2-e6d32a07b802" />

## Main Tool

### Extract-PowerBIMetadata.ps1

Extracts metadata from Excel files into JSON format for Power BI semantic model generation.

**Usage:**

```powershell
# Use default project (ImaginationWorkshop)
.\Extract-PowerBIMetadata.ps1

# Specify a different project
.\Extract-PowerBIMetadata.ps1 -ProjectName "ImaginationWorkshop"

# Specify custom Excel file name
.\Extract-PowerBIMetadata.ps1 -ProjectName "ImaginationWorkshop" -ExcelFileName "CustomMetadata.xlsx"

# Create a new project structure (if project doesn't exist)
.\Extract-PowerBIMetadata.ps1 -ProjectName "NewProject"
```

**Parameters:**
- `ProjectName` (optional): Name of the project folder under Reports/. Default: `ImaginationWorkshop`
- `ExcelFileName` (optional): Name of Excel file. Default: `{ProjectName} Metadata Dictionary.xlsx`

**Output:**
- Creates a JSON file in Reports/{ProjectName}/Metadata/
- JSON contains all tables and their fields with schema information

**Auto-Create New Projects:**
- If the project folder doesn't exist, the script will create:
  - `Reports/{ProjectName}/Metadata/` folder
  - `Reports/{ProjectName}/PBIP/` folder
  - Prompts you to add Excel file and set sensitivity to 'General'

## Utility Scripts

See [Code/README.md](Code/README.md) for Python utility scripts that display metadata contents.

## Working with Projects

### Adding a New Project

1. Run the extraction script with a new project name:
   ```powershell
   .\Extract-PowerBIMetadata.ps1 -ProjectName "NewProject"
   ```

2. The script will create:
   - `Reports/NewProject/Metadata/` folder
   - `Reports/NewProject/PBIP/` folder

3. Add your Excel metadata file to `Reports/NewProject/Metadata/`
   - Filename should be: `NewProject Metadata Dictionary.xlsx`
   - Set sensitivity label to 'General'

4. Run the script again to generate JSON:
   ```powershell
   .\Extract-PowerBIMetadata.ps1 -ProjectName "NewProject"
   ```

5. Create your PBIP files in `Reports/NewProject/PBIP/`

### Existing Projects

- **ImaginationWorkshop**: CoreAI Imagination Workshop report with 12 Dataverse tables
  - Metadata: [Reports/ImaginationWorkshop/Metadata/](Reports/ImaginationWorkshop/Metadata/)
  - PBIP: [Reports/ImaginationWorkshop/PBIP/](Reports/ImaginationWorkshop/PBIP/)
