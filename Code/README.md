# Code Scripts

## Active Scripts

### extract_fields.py

Python utility to display fields from the Excel metadata.

**Usage:**
```powershell
python Code\extract_fields.py Reports\{ProjectName}\Metadata
```

**Example:**
```powershell
python Code\extract_fields.py Reports\ImaginationWorkshop\Metadata
```

### extract_tables.py

Python utility to display tables from the Excel metadata.

**Usage:**
```powershell
python Code\extract_tables.py Reports\{ProjectName}\Metadata
```

**Example:**
```powershell
python Code\extract_tables.py Reports\ImaginationWorkshop\Metadata
```

**Note:** If no metadata folder is specified, defaults to `Metadata/ImaginationWorkshop` (legacy path)

## Archive Folder

Contains 10 superseded scripts that were used during initial table setup:
- Scripts for generating SQL and Power Query
- Scripts for batch table operations via MCP
- Table update and rebuild scripts

These have been archived as the tables are now directly managed through TMDL files in the Reports folder.
