<img width="1248" alt="image" src="https://github.com/user-attachments/assets/97e24821-cf02-4600-ae6f-f45a0c784cc1" />

# Dataverse to Power BI Semantic Model Generator

**Transform your Dataverse data into professional Power BI reports in minutes, not days.**

This XrmToolBox plugin helps you create optimized Power BI data models from your Dataverse tables—no coding required. It guides you through building a proper star-schema design, automatically applies best practices, and generates a ready-to-use Power BI project.

---

## 🎯 What Does This Tool Do?

If you've ever tried to connect Power BI to Dataverse, you know it can be challenging:

- Which tables should you include?
- How do you handle relationships?
- I chose the defaults, why is my report so slow?
- Why can't I see the tables' and fields' Display Names?

This tool solves these problems by:

1. **Guiding you through table selection** from your Dataverse solutions
2. **Automatically building relationships** based on your lookup fields
3. **Creating an optimized star-schema** for fast, intuitive reporting
4. **Generating a complete Power BI project** ready to open and customize
5. **Imports Metadata for Formatting** clean and user-friendly model

---

## 📑 Table of Contents

- [Key Features](#-key-features)
- [Understanding Star-Schema Design](docs/star-schema.md)
- [Getting Started](#-getting-started)
- [Best Practices We Apply Automatically](#️-best-practices-we-apply-automatically)
- [Understanding Your Generated Project](docs/understanding-the-project.md)
- [Connection Modes: TDS vs FabricLink](#-connection-modes-tds-vs-fabriclink)
- [Direct Query vs. Import Mode](docs/direct-query-vs-import.md)
- [Publishing and Deployment](#-publishing-and-deployment)
- [Suggested Next Steps](docs/next-steps.md)
- [Frequently Asked Questions](#-frequently-asked-questions)
- [Troubleshooting](docs/troubleshooting.md)
- [Getting Help](#-getting-help)
- [For Developers](#-for-developers)

---
<img width="1600" alt="Screenshot of application" src="https://github.com/user-attachments/assets/812e27f3-d89f-46d8-bc8e-4887f19fcbb5" />


## ✨ Key Features

| Feature | What It Does For You |
| ------- | ------------------- |
| **Star-Schema Wizard** | Helps you designate fact and dimension tables for optimal performance |
| **Dual Connection Support** | Choose between **Dataverse TDS** (Direct to Dataverse for medium and small datasets) or **FabricLink** (Using the FabricLink Lakehouse for larger volumes of data) — see [Connection Modes](#-connection-modes-tds-vs-fabriclink) |
| **Smart Column Selection** | Uses your Dataverse forms and views to include only relevant fields |
| **Friendly Field Names** | Automatically renames columns to their display names (no more "cai_accountid"!) |
| **Display Name Customization** | Override display names per-attribute with inline double-click editing; automatic conflict detection prevents duplicate names |
| **TMDL Preview** | See the exact TMDL code that will be generated before building, with copy/save capabilities for individual tables or entire model |
| **Relationship Detection** | Finds and creates relationships from your lookup fields |
| **Date Table Generation** | Creates a proper calendar dimension with timezone support |
| **View-Based Filtering** | Applies your Dataverse view filters directly to the data model |
| **Auto-Generated Measures** | Creates a record count and a clickable URL link measure on your fact table |
| **Incremental Updates** | Safely update your model while preserving custom measures and calculations |

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
- Check which dimensions to include and activate the relationship where needed.
- Only one active relationship should exist between the Fact and a Dimension
- Optionally add "snowflake" parent dimensions if needed
- **Tip:** Start with a only a few tables to optimize performance.
- **Finish Selection** when you're done.

#### Step 6: Customize the Queries

- For each table, click the ✏️ icon to select a form and view
- The **form** determines which columns are selected by default to appear in your model
- The **view** determines which rows are included (filtering data to current data helps improve performance.)
- Check/Uncheck Attributes in the right column to include/exclude fields from the query.
- **Double-click any Display Name** to override it with a custom alias (e.g., rename "Name" to "Account Name" to avoid conflicts)
- Overridden names show an asterisk (*) suffix; duplicates are highlighted in red and must be fixed before building
- **Tip:** Start with only the needed columns to optimize performance.
- Memo (text area) fields with lots of text are the slowest fields to retrieve - use sparingly.

#### Step 7: Preview Your Model (Optional)

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

When you select a Dataverse view, its filter criteria are applied directly to the Power Query expression. This means only relevant rows are included (e.g., "Active Accounts Only" or "My Open Cases").

> **Important:** If the view definition changes in Dataverse, your Power BI model won't automatically update. You'll need to run this tool again to refresh the model's metadata and pick up the new view filters.

### 🔗 Referential Integrity

For required lookup fields (where a value must be provided), we enable "Assume Referential Integrity" on relationships. This allows Power BI to use more efficient INNER JOIN operations instead of OUTER JOINs.

### 📅 Date Handling

DateTime fields are converted to Date-only values with proper timezone adjustment. Dataverse stores all dates in UTC, but your reports need local dates for accurate daily analysis. We apply the timezone offset you specify so "January 15th" means January 15th in *your* timezone.

### ↔️ Relationship Cardinality

All relationships are correctly configured as Many-to-One from fact to dimension tables, with proper cross-filter direction for optimal DAX performance.

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

For DirectQuery models connected to Dataverse, you must configure authentication so each report viewer uses their own identity:

1. Go to [Power BI Service](https://app.powerbi.com)
2. Navigate to your **Workspace**
3. Find your **semantic model** (shown with a database icon)
4. Click the **three dots (...)** → **Settings**
5. Expand **Data source credentials**
6. Click **Edit credentials**
7. Set Authentication method to **OAuth2**
8. ✅ **Enable: "Report viewers can only access this data source with their own Power BI identities"**
9. Click **Sign in** and authenticate

This critical setting ensures:

- Each user's Dataverse security roles are respected
- Users only see records they have permission to view
- No shared service account is used

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

## ❓ Frequently Asked Questions

### Q: Can I add more tables after the initial build?

**A:** Yes! Run the tool again, select additional tables, and rebuild. Your existing customizations (measures, formatting, hierarchies) will be preserved.

### Q: What happens if our Dataverse schema changes?

**A:** The tool will detect changes when you run it again. It shows you a preview of what's new, modified, or removed before applying updates—you're always in control.

### Q: Can I use this with Dynamics 365 apps?

**A:** Absolutely! This works with any Dataverse environment, including Dynamics 365 Sales, Customer Service, Field Service, Marketing, and custom Power Apps. It supports both the Dataverse TDS endpoint and FabricLink connections.

### Q: Why are some of my columns missing?

**A:** Columns are pre-selected by default from your selected form. If a field isn't on the form, it won't pre-selected to be in the model by default. You can add columns that aren't on the form by switching to view "All" attributes and checking the selection box beside any additional ones you need.

### Q: How do I handle many-to-many relationships?

**A:** The tool creates standard many-to-one relationships. For many-to-many
scenarios (like Contacts associated with multiple Accounts), you may need to
include the intersection table and create a bridge pattern manually. These can
become complex and require more expertise in proper modeling to ensure your
results are reflective of your intent.

### Q: My report is slow—what can I do?

**A:** See our [Troubleshooting Guide](docs/troubleshooting.md) for detailed optimization steps.

### Q: Can I version control my Power BI project?

**A:** Yes! The PBIP format is designed for Git. Each table, relationship, and report element is a separate text file that shows meaningful changes in version control.

### Q: I see a security warning about "multiple data sources" when opening my project. Is this safe?
> <img width="400" alt="Composite Model Security Warning" src="https://github.com/user-attachments/assets/3ddd7d1a-9e5c-43ed-9528-4368d64a9409" />
**A:** Yes, this is expected and safe to proceed. The warning appears because the model is a composite model—it combines Dataverse tables (DirectQuery) with a parameter table used to store the DataverseURL. You can review all queries before opening by using the **Preview TMDL** feature or inspecting the `.tmdl` files in your project folder. Learn more about [composite models](https://learn.microsoft.com/power-bi/transform-model/desktop-composite-models).

---

## 🤝 Getting Help

- **Report Issues:** [GitHub Issues](https://github.com/microsoft/DataverseMetadata-to-PowerBI-Semantic-Model/issues)
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
