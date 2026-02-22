<img width="1248" height="307" alt="image" src="https://github.com/user-attachments/assets/e5061c91-c7d8-4dba-bd84-72ed2825bd39" />

# Dataverse to Power BI Semantic Model Generator

**The fastest way to build production-ready Power BI semantic models from Dataverse — in minutes, not days.**

This XrmToolBox plugin generates optimized Power BI data models (PBIP/TMDL format) directly from your Dataverse metadata. It guides you through building a proper star-schema design, automatically applies best practices, and produces a complete Power BI project you can open, customize, and publish immediately.

---

## 🎯 What Does This Tool Do?

Building Power BI reports on Dataverse is harder than it should be:

- Which tables should you include? There are hundreds.
- How do you handle relationships between them?
- Why is my DirectQuery report so slow?
- Why can't I see the display names for tables and fields?
- How do I switch between DirectQuery, Import, and Dual storage modes?

This tool eliminates all of that complexity:

1. **Guides you through table selection** from your Dataverse solutions
2. **Automatically builds relationships** based on your lookup fields
3. **Creates an optimized star-schema** for fast, intuitive reporting
4. **Generates a complete Power BI project** (PBIP) ready to open and customize
5. **Imports metadata for formatting** — display names, descriptions, and choice labels
6. **Safely updates your model** — preserving your custom measures, formatting, and relationships

---

## 📑 Table of Contents

- [Key Features](#-key-features)
- [Latest Changes](#-latest-changes)
- [Understanding Star-Schema Design](docs/star-schema.md)
- [Getting Started](#-getting-started)
- [Storage Modes](#-storage-modes)
- [Best Practices We Apply Automatically](#️-best-practices-we-apply-automatically)
- [Understanding Your Generated Project](docs/understanding-the-project.md)
- [Connection Modes: TDS vs FabricLink](#-connection-modes-tds-vs-fabriclink)
- [Direct Query vs. Import Mode](docs/direct-query-vs-import.md)
- [Expand Lookups (Denormalization)](#-expand-lookups-denormalization)
- [Model Configuration Management](#-model-configuration-management)
- [Change Preview & Impact Analysis](#-change-preview--impact-analysis)
- [Incremental Updates: What's Preserved](#-incremental-updates-whats-preserved)
- [Publishing and Deployment](#-publishing-and-deployment)
- [Suggested Next Steps](docs/next-steps.md)
- [Frequently Asked Questions](#-frequently-asked-questions)
- [Troubleshooting](docs/troubleshooting.md)
- [Getting Help](#-getting-help)
- [For Developers](#-for-developers)

---
<img width="1840" height="857" alt="image" src="https://github.com/user-attachments/assets/b8dac286-9da7-49b1-bac2-37fbd00ea620" />


## ✨ Key Features

| Feature | What It Does For You |
| ------- | ------------------- |
| **Star-Schema Wizard** | Helps you designate fact and dimension tables for optimal performance |
| **Dual Connection Support** | Choose between **Dataverse TDS** (direct to Dataverse) or **FabricLink** (via Fabric Lakehouse) — see [Connection Modes](#-connection-modes-tds-vs-fabriclink) |
| **Storage Mode Control** | DirectQuery, Import, Dual (All), or Dual (Select) — set globally or per-table. See [Storage Modes](#-storage-modes) |
| **Smart Column Selection** | Uses your Dataverse forms and views to include only relevant fields |
| **Lookup Sub-Column Controls** | Per-lookup Include/Hidden toggles for ID/Name and polymorphic Type/Yomi (Owner/Customer), with smart defaults tied to relationship usage |
| **Collapsible Lookup Groups** | Lookup attributes render as expandable groups so sub-columns are easy to inspect without clutter |
| **Friendly Field Names** | Automatically renames columns to their display names (no more "cai_accountid"!) |
| **Display Name Customization** | Override display names per-attribute with inline double-click editing; automatic conflict detection prevents duplicate names |
| **TMDL Preview** | See the exact TMDL code that will be generated before building, with copy/save capabilities for individual tables or entire model |
| **Relationship Detection** | Finds and creates relationships from your lookup fields, with search/filter and multi-relationship management |
| **Date Table Generation** | Creates a proper calendar dimension with timezone support |
| **View-Based Filtering** | Applies your Dataverse view filters (FetchXML) directly to the data model — supports 20+ filter operators |
| **Auto-Generated Measures** | Creates a record count and a clickable URL link measure on your fact table |
| **Incremental Updates** | Safely update your model while preserving custom measures, descriptions, formatting, and relationships |
| **Change Preview** | TreeView-based change preview with impact analysis (Safe/Additive/Moderate/Destructive) before applying any changes |
| **Expand Lookups** | Denormalize fields from related tables directly into a parent table's query — no extra dimension needed |
| **Configuration Management** | Save, load, export, and import model configurations; CSV documentation export for review |

---

## 📌 Latest Changes

For the most up-to-date release details, see [CHANGELOG.md](CHANGELOG.md).

---

## 🚀 Getting Started

### Installation

1. Open **XrmToolBox**
2. Go to **Tool Library** (Configuration → Tool Library)
3. Search for "**Dataverse to Power BI**"
4. Click **Install**
5. Restart XrmToolBox when prompted

### Creating Your First Model

#### Step 1: Connect to Your Environment

- In XrmToolBox, connect to your Dataverse environment
- Open the "**Dataverse to Power BI Semantic Model**" plugin

#### Step 2: Create or Select a Configuration

- Click **Semantic Model** to create a new configuration
- Give it a meaningful name (e.g., "Sales Analytics" or "Case Management")
- Set your **Working Folder** where the Power BI project will be saved

#### Step 3: Select Your Solution and tables

- Click **Select Tables** to open the solution and table picker
- **Choose your unmanaged solution** — this filters the list to tables relevant to your business app

> **Why solutions matter:** Dataverse contains hundreds of system tables. By selecting your solution first, you'll only see the tables your team has customized—making it much easier to find what you need.

#### Step 4: Choose your Fact Table

- Select your **Fact Table** to designate the central entity of your model
- Select the tables you want in your model from that solution

#### Step 5: Choose your Dimension Tables

- The tool will show all lookup relationships from your fact table
- The selector includes a **Source Table** column so you can see where each relationship originates

><img width="600" alt="Relationship selector with Source Table context and grouped relationships." src="https://github.com/user-attachments/assets/ce2216ad-1656-4e83-a26f-7974e8d0760f" />

- Check which dimensions to include
- **Multiple relationships to the same dimension** are automatically grouped together with a visual header for easy identification
- **Only one relationship can be active** between the Fact and each Dimension—when you check or double-click a relationship, all others to that dimension automatically become "Inactive"
- Inactive relationships can still be used in DAX with the `USERELATIONSHIP()` function
- Use the **"Solution tables only"** checkbox (enabled by default) to focus on tables in your solution
- Use the **Search box** to filter relationships by field or table names
- If the same target dimension is reachable through multiple active chains, the tool flags **cross-chain ambiguity** and prompts you to resolve it before finishing

> <img width="700" alt="Cross-chain ambiguity highlight and finish-time warning prompt" src="https://github.com/user-attachments/assets/f77ddacc-c267-4134-b72e-f49e7af0f170" />

- Optionally add "snowflake" parent dimensions if needed
- **Tip:** Start with only a few tables to optimize performance.
- **Finish Selection** when you're done.

> **Understanding Multiple Relationships:** It's common to have multiple lookup fields pointing to the same table (e.g., "Primary Contact" and "Secondary Contact" both reference the Contact table). Power BI requires exactly one "Active" relationship between two tables—others must be "Inactive." The tool makes this easy by grouping these relationships together and automatically managing their Active/Inactive status as you make selections. See [Managing Multiple Relationships](#-managing-multiple-relationships) below for details.

#### Step 6: Customize the Queries

- For each table, click the ✏️ icon to select a form and view
- The **form** determines which columns are selected by default to appear in your model
- The **view** determines which rows are included (filtering data to current data helps improve performance.)
- Check/Uncheck Attributes in the right column to include/exclude fields from the query.
- Lookup attributes can be expanded into sub-columns with per-sub-column **Include**/**Hidden** toggles.
- Owner/Customer lookups support additional **Type** and **Yomi** sub-columns.

> <img width="600" alt="Expanded lookup sub-column controls (ID/Name/Type/Yomi) with Include/Hidden states." src="https://github.com/user-attachments/assets/a858a00f-c431-4cde-80c9-34909282521c" />

- **Double-click any Display Name** to override it with a custom alias (e.g., rename "Name" to "Account Name" to avoid conflicts)
- Overridden names show an asterisk (*) suffix; duplicates are highlighted in red and must be fixed before building
- **Tip:** Start with only the needed columns to optimize performance.
- Memo (text area) fields with lots of text are the slowest fields to retrieve - use sparingly.

#### Step 7: Preview Your Model (Optional)

> <img width="600" alt="TMDL Preview" src="https://github.com/user-attachments/assets/7684bd40-46ba-4874-8445-afb37748f153" />

- Click **Preview TMDL** to see the exact code that will be generated
- Review the TMDL definitions for tables, columns, relationships, and expressions
- Copy individual table definitions or save all .tmdl files to a folder for inspection
- Tables are shown in logical order: Fact tables first, then Dimensions, Date table, and configuration Expressions

#### Step 8: Add a Date Table (Recommended)

- Click **Dates** to configure your date dimension
- Select your primary date field (e.g., "Created On")
- Choose the year range for your date table
- Set the timezone adjustment to adjust the GMT date/time stored to a standardized timezone.
- Identify any other fields that you want standardized to the chosen timezone.

#### Step 9: Build Your Model

- Click **Build Semantic Model**
- Review the changes that will be made
- Click **Apply** to generate your Power BI project
- Once built, it will ask if you want to open the project.
- Start building your reports!

> **⚠️ Important: Security Warning on First Open**
> 
> When you first open the generated Power BI project, you may see a security warning stating *"This file uses multiple data sources. Information in one data source might be shared with other data sources without your knowledge."*
> <img width="800" alt="Composite Model Security Warning" src="https://github.com/user-attachments/assets/3ddd7d1a-9e5c-43ed-9528-4368d64a9409" />
> 
> **This is expected behavior** — you can safely click **OK** to proceed.
> 
> **Why this happens:** The generated model is a composite model by design. It combines:
> - Your Dataverse tables (via DirectQuery to the Dataverse/Fabric endpoint)
> - A static parameter table containing the DataverseURL
> - (optionally) A DAX-calculated Date table
> 
> You can review all queries before opening the project by:
> - Using the **Preview TMDL** feature in the tool
> - Browsing the `.tmdl` files in your project's `{ModelName}.SemanticModel/definition/tables/` folder
> 
> 📚 **Learn more:** [Composite models in Power BI Desktop](https://learn.microsoft.com/power-bi/transform-model/desktop-composite-models)
> 


---

## ⚡ Storage Modes

The tool supports four storage modes that control how Power BI accesses your Dataverse data. You can set the mode globally or override it per-table.

| Mode | Description | Best For |
|------|-------------|----------|
| **DirectQuery** (default) | All queries go live to Dataverse — always up-to-date, no refresh needed | Real-time dashboards, smaller datasets, row-level security |
| **Import** | Data is cached locally in Power BI — fast performance but requires scheduled refresh | Large lookup tables, offline analysis, complex calculations |
| **Dual (All)** | Fact tables stay DirectQuery; all dimensions use Dual mode | Fast dimension slicing while preserving live fact-table querying |
| **Dual (Select)** | Fact tables stay DirectQuery; selected dimensions use Dual mode | Fine-grained optimization for only the dimensions that benefit from caching |

### Per-Table Storage Mode

When using **Dual** mode, you can configure individual dimension tables with different storage modes:

- **Dual (All)** — All dimension tables use Dual mode, fact tables stay DirectQuery
- **Dual (Select)** — Choose which dimension tables use Dual mode; unselected dimensions stay DirectQuery

> <img width="300" alt="image" src="https://github.com/user-attachments/assets/88af5332-8b09-429a-9e3a-ac657c078632" />
> <img width="400" alt="image" src="https://github.com/user-attachments/assets/ede93c85-8c03-4ba2-8ee8-0e939f07d149" />


This is ideal when you have some large dimension tables (like Product or Account) that benefit from Import caching, while smaller or frequently-changing dimensions should stay DirectQuery.

> **Tip:** Start with DirectQuery for simplicity. If you notice performance issues with specific dimension tables, switch to Dual mode for those tables. Import mode is best reserved for large static lookup tables.

📚 **Learn More:** [Direct Query vs. Import Mode](docs/direct-query-vs-import.md)

---

## 🛠️ Best Practices We Apply Automatically

This tool implements several Power BI best practices behind the scenes:

### 📝 Friendly Column Names

All columns are renamed from their logical names (like `cai_primarycontactid`) to their display names (like `Primary Contact`). This makes your reports much easier to understand and your field list cleaner to navigate.

**Advanced:** You can override any display name by double-clicking it in the attributes list. For example, rename "Name" to "Account Name" to differentiate it from other "Name" columns in your model. The tool prevents duplicate names and highlights conflicts before you build.

### 📋 Rich Column Metadata

Each column in your TMDL model includes comprehensive descriptions:

- **Dataverse Description**: If the attribute has a description in Dataverse metadata, it appears first
- **Source Attribution**: Shows the exact source table and field (e.g., `Source: account.primarycontactid`)
- **Lookup Targets**: For lookup fields, lists which tables can be referenced

Example: `"The primary contact for the account | Source: account.primarycontactid | Targets: contact"`

This metadata makes it easy for report builders to understand where data comes from and how to use it correctly.

### 🎯 Optimized Queries

We only include the columns you selected—no unnecessary data is pulled from Dataverse. This keeps your model lean and improves query performance.

### 🔍 View-Based Filtering

When you select a Dataverse view, its filter criteria are automatically translated to SQL WHERE clauses in your Power Query expression. This means only relevant rows are included (e.g., "Active Accounts Only" or "My Open Cases").

**Supported FetchXML operators:**

| Category | Operators |
|----------|-----------|
| **Comparison** | `eq`, `ne`, `gt`, `ge`, `lt`, `le` |
| **Null checks** | `null`, `not-null` |
| **String matching** | `like`, `not-like`, `begins-with`, `ends-with`, `contains`, `not-contain` |
| **Date (relative)** | `today`, `yesterday`, `this-week`, `this-month`, `this-year`, `last-week`, `last-month`, `last-year` |
| **Date (dynamic)** | `last-x-days`, `next-x-days`, `last-x-months`, `next-x-months`, `last-x-years`, `next-x-years`, `older-than-x-days` |
| **Lists** | `in`, `not-in` |
| **User context** | `eq-userid`, `ne-userid`, `eq-userteams`, `ne-userteams` (TDS/DirectQuery only) |
| **Logical** | `AND`/`OR` grouping via FetchXML filter `type` attribute |

> **Important:** If the view definition changes in Dataverse, your Power BI model won't automatically update. You'll need to run this tool again to refresh the model's metadata and pick up the new view filters.

> **FabricLink limitation:** User context operators (`eq-userid`, etc.) are not available in FabricLink mode because Direct Lake does not support row-level user filtering at the query level. These conditions are automatically skipped.

### 🔗 Referential Integrity

For required lookup fields (where a value must be provided), we enable "Assume Referential Integrity" on relationships. This allows Power BI to use more efficient INNER JOIN operations instead of OUTER JOINs.

### 📅 Date Handling

DateTime fields are converted to Date-only values with proper timezone adjustment. Dataverse stores all dates in UTC, but your reports need local dates for accurate daily analysis. We apply the timezone offset you specify so "January 15th" means January 15th in *your* timezone.

### ↔️ Relationship Cardinality

All relationships are correctly configured as Many-to-One from fact to dimension tables, with proper cross-filter direction for optimal DAX performance.

### 🔀 Managing Multiple Relationships

When multiple lookup fields point to the same dimension table (e.g., "Primary Contact," "Secondary Contact," and "Responsible Contact" all referencing the Contact table), the tool helps you manage them intelligently:

- **Visual Grouping**: Relationships are grouped under headers like "Contact (Multiple Relationships)" for easy identification
- **Source Context**: A dedicated **Source Table** column makes relationship origin clear when snowflake paths introduce multiple sources
- **Smart Selection**: Checking any relationship automatically marks ALL other relationships to that dimension as "Inactive"
- **Active by Default**: All relationships start as "Active" for clarity—you choose which one to keep active
- **Double-Click Toggle**: Double-click any relationship to toggle its Active/Inactive status
- **Automatic Conflict Prevention**: When you activate one relationship, all others to that dimension become inactive automatically (even unchecked ones)
- **Conflict Detection**: Red highlighting appears if multiple ACTIVE relationships exist to the same dimension—you must resolve these before building
- **Cross-Chain Ambiguity Detection**: Amber/orange warnings highlight dimensions reachable through multiple active paths from different sources, with a finish-time prompt to resolve ambiguity
- **Visual Clarity**: Inactive relationships show "(Inactive)" in the Type column with white background; active conflicts show red background

This ensures your model always has exactly one active relationship per dimension pair, while preserving inactive relationships for use with the DAX `USERELATIONSHIP()` function.

**Example:** If your Case table has "Primary Contact," "Reported By," and "Modified By" lookups—all pointing to Contact—you might:
1. Check "Primary Contact" and "Reported By" to include both
2. The tool automatically makes "Primary Contact" Active and "Reported By" Inactive
3. In your DAX measures, use `CALCULATE([Total Cases], USERELATIONSHIP(Case[reportedbyid], Contact[contactid]))` to analyze by Reported By

### 🏷️ Hidden Technical Columns

Primary key columns (like `accountid`) are included for relationships but hidden from the report view. This keeps your field list clean while maintaining proper data model structure.

### 📊 Auto-Generated Measures

For your fact table, the tool automatically creates two starter measures:

- **{TableName} Count** — `COUNTROWS` of the fact table for quick record counts
- **Link to {TableName}** — A clickable URL that opens each record directly in Dataverse, using the `WEBURL` DAX function

These measures are regenerated on each build. Your own custom measures are always preserved.

---

## 🔌 Connection Modes: TDS vs FabricLink

This tool supports two different connection modes for accessing Dataverse data. Your choice affects how queries are generated and how the semantic model connects to your data.

### Dataverse TDS (Default)

Uses the **Dataverse TDS Endpoint** — a SQL-compatible interface built directly into Dataverse.

| Aspect | Detail |
| ------ | ------ |
| **Connector** | `CommonDataService.Database` |
| **Query Style** | Native SQL via `Value.NativeQuery(...)` with `[EnableFolding=true]` |
| **Best For** | Direct Dataverse access without Fabric infrastructure |
| **Requirements** | TDS endpoint enabled in your Dataverse environment |

### FabricLink

Uses **Microsoft Fabric Link for Dataverse** — data is synced to a Fabric Lakehouse and queried via the Fabric SQL endpoint.

| Aspect | Detail |
| ------ | ------ |
| **Connector** | `Sql.Database(FabricSQLEndpoint, FabricLakehouse)` |
| **Query Style** | Standard SQL queries with metadata JOINs for display names |
| **Best For** | Large datasets, advanced analytics, when Fabric is already in use |
| **Requirements** | Fabric workspace with Dataverse Link configured |

> **FabricLink queries** automatically JOIN to `OptionsetMetadata` / `GlobalOptionsetMetadata` and `StatusMetadata` tables for human-readable choice labels and status values. TDS mode uses virtual "name" attributes for the same purpose.
>
> **Multi-select choices in FabricLink:** Label resolution splits values on semicolons (`;`) and uses the attribute logical name for `OptionSetName` in metadata joins.

---

## 🚢 Publishing and Deployment

### Publishing to Power BI Service

1. Open your `.pbip` file in Power BI Desktop
2. Sign in to your Power BI account
3. Click **Publish** in the Home ribbon
4. Select your destination workspace
5. Wait for the upload to complete

Your semantic model (dataset) and report are now available in the cloud!

### Configuring DirectQuery Authentication

For DirectQuery models connected to Dataverse, you must configure authentication so each report viewer uses their own identity.

#### For DataverseTDS Connections (DirectQuery)

**⚠️ Critical: You MUST enable Single Sign-On (SSO) for TDS-based DirectQuery reports** to ensure that:
- Reports are filtered based on each user's credentials
- Dataverse row-level security is enforced
- View filters using current user context (e.g., "My Opportunities") work correctly

**Steps to configure SSO:**

1. Go to [Power BI Service](https://app.powerbi.com)
2. Navigate to your **Workspace**
3. Find your **semantic model** (shown with a database icon)
4. Click the **three dots (...)** → **Settings**
5. Expand **Data source credentials**
6. Click **Edit credentials**
7. Set Authentication method to **OAuth2**
8. ✅ **REQUIRED: Check "End users use their own OAuth2 credentials when accessing this data source via DirectQuery"** (Single Sign-On)
9. Click **Sign in** and authenticate

This critical setting ensures:

- Each user's Dataverse security roles are respected
- Users only see records they have permission to view
- Current user filters in views work correctly
- No shared service account is used

📚 **Learn More:** [Enable Single Sign-On for DirectQuery](https://learn.microsoft.com/power-bi/connect-data/service-azure-sql-database-with-direct-connect#single-sign-on)

#### For FabricLink Connections (Direct Lake)

FabricLink uses Direct Lake storage mode and authenticates differently:
- Authentication is handled automatically through Fabric workspace permissions
- No additional SSO configuration is required
- Users must have appropriate Fabric workspace roles

### Setting Up Scheduled Refresh (Import/Dual Models)

If you've switched any tables to Import or Dual mode:

1. In Power BI Service, go to your semantic model **Settings**
2. Expand **Scheduled refresh**
3. Toggle **Keep your data up to date** to On
4. Set your refresh frequency (e.g., daily at 6 AM)
5. Configure failure notifications

📚 **Learn More:**

- [Publish from Power BI Desktop](https://learn.microsoft.com/en-us/power-bi/create-reports/desktop-upload-desktop-files)
- [Configure Scheduled Refresh](https://learn.microsoft.com/en-us/power-bi/connect-data/refresh-scheduled-refresh)
- [Configure Data Source Credentials](https://learn.microsoft.com/en-us/power-bi/connect-data/service-gateway-data-sources)

---

## ✳️ Expand Lookups (Denormalization)

Sometimes you need a few fields from a related table without adding it as a full dimension. **Expand Lookups** lets you pull attributes from a related table directly into the parent table's query via a LEFT OUTER JOIN — denormalizing the data at the query level.

### How It Works

1. In the attribute list, click the **▶️** button on any lookup field
2. The **Expand Lookup** dialog opens, showing attributes from the related table (filtered by its form)
3. Check the attributes you want to include
4. Click **OK** — the selected attributes appear as child rows under the lookup field, prefixed with the target table name (e.g., "Employee : Badge Number")

### What Gets Generated

The builder adds a `LEFT OUTER JOIN` to the related table and selects the chosen attributes:

- **Regular attributes** — Selected directly from the joined table
- **Lookup attributes** — Selects the name column (e.g., `manageridname`) for human-readable values
- **Choice/Boolean attributes** — In TDS mode, uses the virtual name column; in FabricLink mode, adds metadata JOINs for display labels
- **Multi-select choices** — In TDS mode, uses the virtual name column. In FabricLink mode, uses `OUTER APPLY` with `STRING_SPLIT` / `STRING_AGG` and metadata JOINs, matching the same label resolution pattern used by regular multi-select fields - **Note, this is a complex query pattern that can impact performance, so use multi-select expansions sparingly.**

Expanded attributes use the naming convention `"{TargetTable} : {AttributeDisplayName}"` in the generated TMDL. You can override these display names just like any other attribute.

### Managing Expanded Lookups

- **Add/modify**: Click ▶️ on a lookup to open the dialog and change selections
- **Remove**: Click ▶️ and uncheck all attributes, then click OK (the dialog allows zero selections)
- **Persistence**: Expanded lookup configurations are saved with your semantic model and restored when you reload

> **When to use Expand Lookups vs. Dimensions:**
> - Use **Expand Lookups** when you need 1–3 fields from a related table mostly for display purposes (e.g., showing a manager's name on an employee record)
> - Use a **Dimension table** when you need to filter, group, or slice by the related table's attributes across multiple measures or when you need to analyze the related table as its own entity (e.g., analyzing sales by product category) - or when adding attributes from multiple related table, as each additional join is additional performance overhead.

**⚠️ Performance Impact:** The expand relationships generate an additional LEFT OUTER JOIN. This adds additional overhead to the SQL query, which will affect DirectQuery performance on large tables. - Use this feature sparingly, especially on large fact tables or when expanding multiple attributes from the same related table.


---

## 📦 Model Configuration Management

The tool saves your complete model configuration — tables, columns, relationships, forms, views, storage mode, and display name overrides — so you can pick up right where you left off.

### Managing Configurations

- **Multiple models per environment** — Create separate configurations for different reporting needs (e.g., "Sales Analytics", "Case Management")
- **Auto-save on build** — Your configuration is saved automatically each time you build
- **Working directory & PBIP directory** — Configure where the generated Power BI project files are saved

### Export & Import

Share configurations across machines or team members:

- **JSON Export** — Saves the complete model configuration as a standalone JSON file (full schema, importable)
- **CSV Export** — Generates human-readable CSV documentation files for review purposes (export only, not importable)
- **Import** — Loads a JSON configuration file, adding it to your configuration list with automatic name conflict resolution

#### JSON Export

Exports the entire semantic model configuration including all table selections, attributes, relationships, display name overrides, expanded lookups, storage modes, and connection settings. The JSON file can be imported on another machine to recreate the exact same configuration.

#### CSV Documentation Export

Generates a folder of CSV files for documentation and review:

| File | Contents |
|------|----------|
| **Summary.csv** | Model name, environment URL, connection type, storage mode, language code, table/relationship counts |
| **Tables.csv** | Each table's logical name, display name, schema name, role (Fact/Dimension), storage mode, form ID, view ID |
| **Attributes.csv** | Per-table attribute selections with display name overrides |
| **Relationships.csv** | All relationships: source table, source attribute, target table, active/inactive, snowflake, referential integrity |
| **ExpandedLookups.csv** | Expanded lookup configurations: source table, lookup field, target table, expanded attributes with types |

The CSV export includes a per-table selection dialog — you can choose which tables to include in the documentation. This is useful for:
- Reviewing model structure with stakeholders who don't use the tool
- Creating audit documentation of your semantic model design
- Comparing configurations side-by-side in a spreadsheet

#### Import

Loads a previously exported JSON file and creates a new model configuration. If a model with the same name already exists, a numeric suffix is appended (e.g., "Sales Analytics (2)"). After import, you may need to update the Working Folder and Template Path for the current machine.

---

## 🔍 Change Preview & Impact Analysis

Before applying any changes, the tool shows a detailed preview of exactly what will happen — grouped by category with impact indicators so you can evaluate changes before committing.

### Preview Features

- **Grouped TreeView** — Changes organized under Warnings, Tables, Relationships, and Data Sources
- **Expand/collapse** — Table nodes expand to show column-level detail; preserved items collapse by default
- **Impact indicators** — Each change tagged as **Safe**, **Additive**, **Moderate**, or **Destructive**
- **Filter toggles** — Show/hide Warnings, New, Updated, or Preserved items
- **Detail pane** — Click any change to see expanded context, before/after values, and guidance

<img width="1840" height="950" alt="Change Preview with grouped TreeView, impact badges, and detail pane." src="https://github.com/user-attachments/assets/8374e8f5-f80a-4e27-b3ea-8520ae956062" />

### Impact Levels

| Level | Meaning | Examples |
|-------|---------|----------|
| **Safe** | No risk to existing work | Preserved tables, unchanged relationships |
| **Additive** | New content being added | New tables, new columns, new relationships |
| **Moderate** | Existing content modified, user data preserved | Column type changes, query updates, connection changes |
| **Destructive** | Structural change with potential impact | Connection type switch, incomplete model rebuild |

### Backup Option

Before applying changes, you can check **"Create backup"** to save a timestamped copy of your entire PBIP folder — providing a recovery point if anything goes wrong.

---

## 🔄 Incremental Updates: What's Preserved

When you rebuild an existing model, the tool performs an **incremental update** that preserves your customizations while regenerating metadata from Dataverse. Understanding what survives an update helps you work confidently with the generated model.

### ✅ Preserved During Updates

| Customization | How It's Preserved |
|---|---|
| **User-created measures** | Extracted before rebuild and re-inserted (auto-generated measures like "Link to X" and "X Count" are regenerated fresh) |
| **User-added relationships** | Relationships not matching Dataverse metadata are detected and preserved with a `/// User-added relationship` marker |
| **Column descriptions** | User-edited descriptions (those not matching the tool's `Source:` pattern) are preserved; tool descriptions are regenerated |
| **Column formatting** | User changes to `formatString` and `summarizeBy` are preserved when the column's data type hasn't changed |
| **User annotations** | Custom annotations on columns are preserved; tool annotations (`SummarizationSetBy`, `UnderlyingDateTimeDataType`) are regenerated |
| **LineageTags & IDs** | Table, column, measure, relationship, and expression lineageTags are preserved across updates — report visuals and refresh history stay connected |
| **Platform logicalIds** | `.platform` file IDs are preserved during incremental updates |
| **Date table** | Existing date tables (detected by `dataCategory: Time`) are never overwritten |
| **RLS roles** | The `definition/roles/` folder is not modified by the tool |
| **Cultures/translations** | The `definition/cultures/` folder is not modified by the tool |

### ⚠️ Regenerated on Each Update

| Content | Why |
|---|---|
| **SQL queries & partitions** | This is the tool's core purpose — queries are regenerated from current metadata |
| **Column definitions** | Columns are regenerated from Dataverse attributes (new columns added, removed columns deleted) |
| **Tool-managed relationships** | Relationships matching Dataverse metadata are regenerated (with preserved GUIDs) |
| **model.tmdl** | Table references, annotations, and query order are regenerated |
| **Auto-generated measures** | "Link to X" and "X Count" measures are always regenerated |

### ❌ Not Managed (Use With Caution)

| Content | Notes |
|---|---|
| **Perspectives** | Not preserved if added to model.tmdl — will be overwritten |
| **Model-level measures** | Place measures in table files (not model.tmdl) for preservation |
| **Calculated tables/columns** | Not managed by the tool — may survive in table files but are not guaranteed |

### 🔀 Change Scenarios

| Scenario | Behavior |
|---|---|
| **Table renamed in Dataverse** | Detected via `/// Source:` comment — lineage tags, user measures, and metadata carried over from old file; old file deleted |
| **Date field changed** | Old date→Date relationship removed automatically; new date relationship created |
| **Storage mode change** | Warning shown in change preview; `cache.abf` deleted to prevent stale data |
| **Connection type change (TDS↔FabricLink)** | Warning shown in change preview; all table queries restructured; user measures and relationships preserved |
| **Table role change (Fact↔Dimension)** | Auto-generated measures (Link to X, X Count) are excluded from preservation; user measures kept |
| **Column added/removed** | New columns added; removed columns dropped from output; existing column metadata preserved |
| **Column type changed** | Column regenerated with new type; user formatting reset (formatString/summarizeBy) |

## ❓ Frequently Asked Questions

### Q: Can I add more tables after the initial build?

**A:** Yes! Run the tool again, select additional tables, and rebuild. Your existing customizations (user measures, descriptions, formatting, relationships) are automatically preserved. See [Incremental Updates: What's Preserved](#-incremental-updates-whats-preserved) for full details.

### Q: What happens if our Dataverse schema changes?

**A:** The tool will detect changes when you run it again. It shows you a preview of what's new, modified, or removed before applying updates—you're always in control.

### Q: Can I use this with Dynamics 365 apps?

**A:** Absolutely! This works with any Dataverse environment, including Dynamics 365 Sales, Customer Service, Field Service, Marketing, and custom Power Apps. It supports both the Dataverse TDS endpoint and FabricLink connections.

### Q: Why are some of my columns missing?

**A:** Columns are pre-selected by default from your selected form. If a field isn't on the form, it won't pre-selected to be in the model by default. You can add columns that aren't on the form by switching to view "All" attributes and checking the selection box beside any additional ones you need.

### Q: Multi-select choice labels look wrong in FabricLink mode. What should I check?

**A:** Verify your tool is up to date and rebuild the model. FabricLink multi-select label joins should split values on semicolons (`;`) and use the attribute logical name for `OptionSetName` in metadata joins.

### Q: How do I handle many-to-many relationships?

**A:** The tool creates standard many-to-one relationships. For many-to-many
scenarios (like Contacts associated with multiple Accounts), you may need to
include the intersection table and create a bridge pattern manually. These can
become complex and require more expertise in proper modeling to ensure your
results are reflective of your intent.

### Q: What if I have multiple lookup fields pointing to the same table?

**A:** This is very common (e.g., \"Primary Contact,\" \"Reported By,\" and \"Modified By\" all pointing to Contact). The tool handles this automatically:

- Multiple relationships to the same dimension are **visually grouped** together for easy identification
- **Only one can be Active**—when you check or double-click a relationship, all others to that dimension automatically become Inactive
- You can still use inactive relationships in DAX with `USERELATIONSHIP()` function
- Example: `CALCULATE([Total Cases], USERELATIONSHIP(Case[reportedbyid], Contact[contactid]))`

See [Managing Multiple Relationships](#-managing-multiple-relationships) for more details.

### Q: My report is slow—what can I do?

**A:** See our [Troubleshooting Guide](docs/troubleshooting.md) for detailed optimization steps.

### Q: Can I version control my Power BI project?

**A:** Yes! The PBIP format is designed for Git. Each table, relationship, and report element is a separate text file that shows meaningful changes in version control.

### Q: Can I share my model configuration with my team?

**A:** Yes! Use the **Export** button in the Semantic Model Manager. Choose **JSON** to save the full configuration as a file that team members can **Import** on their machine. Choose **CSV** to generate human-readable documentation files for review in a spreadsheet. The JSON export includes all table selections, column choices, display name overrides, expanded lookups, relationship settings, and storage mode preferences.

### Q: What storage mode should I use?

**A:** Start with **DirectQuery** (the default) for simplicity and real-time data. If you notice performance issues with large dimension tables, switch to **Dual** mode for those tables — Power BI will cache them locally while keeping fact tables live. Use **Import** only for very large static lookup tables or when you need offline access. See [Storage Modes](#-storage-modes) for details.

### Q: I see a security warning about "multiple data sources" when opening my project. Is this safe?
> <img width="400" alt="Composite Model Security Warning" src="https://github.com/user-attachments/assets/3ddd7d1a-9e5c-43ed-9528-4368d64a9409" />
**A:** Yes, this is expected and safe to proceed. The warning appears because the model is a composite model—it combines Dataverse tables (DirectQuery) with a parameter table used to store the DataverseURL. You can review all queries before opening by using the **Preview TMDL** feature or inspecting the `.tmdl` files in your project folder. Learn more about [composite models](https://learn.microsoft.com/power-bi/transform-model/desktop-composite-models).

---

## 🤝 Getting Help

- **Report Issues:** [GitHub Issues](https://github.com/microsoft/DataverseToPowerBI/issues)
- **Power BI Community:** [Power BI Forums](https://community.powerbi.com/)
- **XrmToolBox:** [XrmToolBox Forums](https://www.xrmtoolbox.com/)
- **Dataverse Docs:** [Microsoft Dataverse Documentation](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/)

---

## 👩‍💻 For Developers

Want to contribute, extend, or build from source? See our [Developer Guide](CONTRIBUTING.md) for:

- Repository structure
- Build instructions
- Architecture overview
- Contribution guidelines

---

## 📄 License

This project is open source under the MIT License. See [LICENSE](LICENSE) for details.
