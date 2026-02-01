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

## ✨ Key Features

| Feature | What It Does For You |
|---------|---------------------|
| **Star-Schema Wizard** | Helps you designate fact and dimension tables for optimal performance |
| **Smart Column Selection** | Uses your Dataverse forms and views to include only relevant fields |
| **Friendly Field Names** | Automatically renames columns to their display names (no more "cai_accountid"!) |
| **Relationship Detection** | Finds and creates relationships from your lookup fields |
| **Date Table Generation** | Creates a proper calendar dimension with timezone support |
| **View-Based Filtering** | Applies your Dataverse view filters directly to the data model |
| **Incremental Updates** | Safely update your model while preserving custom measures and calculations |

---

## 📊 Understanding Star-Schema Design

This tool guides you into building a **star-schema** data model, which is the recommended design pattern for Power BI. Understanding this concept will help you build better reports.

### What is a Star-Schema?

A star-schema organizes your data into two types of tables:

**Fact Tables** (the center of the star ⭐)
- Contains your transactional or event data
- Examples: Orders, Opportunities, Cases, Appointments
- Has many rows that grow over time
- Contains the numbers you want to analyze (amounts, quantities, counts)

**Dimension Tables** (the points of the star)
- Contains descriptive attributes for filtering and grouping
- Examples: Customers, Products, Employees, Territories
- Changes less frequently
- Provides the "who, what, where, when" context for your facts

### Why Star-Schema Matters

- ✅ **Faster Performance** — Power BI's engine is optimized for this pattern  
- ✅ **Simpler DAX** — Calculations are easier to write and understand  
- ✅ **Intuitive Filtering** — Filters flow naturally from dimensions to facts  
- ✅ **Smaller Model Size** — Less data duplication than flat tables

### A Note About Snowflake Schemas

Sometimes a dimension table has its own lookup to another table (for example, Customer → Territory → Region). This creates a "snowflake" pattern where dimensions branch into sub-dimensions.

⚠️ **Be cautious with snowflaking:**
- Each additional level adds complexity
- Filters must travel through more relationships
- Performance can degrade with deep hierarchies

**Our recommendation:** Keep your schema as flat as possible. Only add parent dimensions when you truly need to filter or group by those attributes.

📚 **Learn More:**
- [Star Schema in Power BI](https://learn.microsoft.com/power-bi/guidance/star-schema)
- [Understand Star Schema Design](https://learn.microsoft.com/power-bi/guidance/star-schema#star-schema-overview)
- [Importance of Star Schema](https://learn.microsoft.com/power-bi/guidance/star-schema#importance-of-star-schema-design)
- I like this explanation from [Brian Julius](https://www.linkedin.com/in/brianjuliusdc/): 
<img width="2048" alt="image of a Star Schema" src="https://github.com/user-attachments/assets/d558cd15-5b8d-4ae4-9a91-3d8413783ba1" />

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

#### Step 3: Select Your Solution and Tables
- Click **Select Tables** to open the solution picker
- **Choose your unmanaged solution** — this filters the list to tables relevant to your business app
- Select the tables you want in your model from that solution
- **Tip:** Start with a only a few tables to optimize performance.

> **Why solutions matter:** Dataverse contains hundreds of system tables. By selecting your solution first, you'll only see the tables your team has customized—making it much easier to find what you need.

#### Step 4: Configure the Star-Schema
- Click **Star Schema** to designate your fact table
- The tool will show all lookup relationships from your fact table
- Check which dimensions to include
- Optionally add "snowflake" parent dimensions if needed

#### Step 5: Customize Columns
- For each table, click the ✏️ icon to select a form and view
- The **form** determines which columns appear in your model
- The **view** determines which rows are included (filtering data to current data helps improve performance.)

#### Step 6: Add a Date Table (Recommended)
- Click **Calendar Table** to configure your date dimension
- Select your primary date field (e.g., "Created On")
- Set the timezone adjustment for accurate daily reporting
- Choose the year range for your date table

#### Step 7: Build Your Model
- Click **Build Semantic Model**
- Review the changes that will be made
- Click **Apply** to generate your Power BI project

#### Step 8: Open in Power BI Desktop
- Navigate to your working folder
- Open the `.pbip` file in Power BI Desktop
- Start building your reports!

---

## 🛠️ Best Practices We Apply Automatically

This tool implements several Power BI best practices behind the scenes:

### 📝 Friendly Column Names
All columns are renamed from their logical names (like `cai_primarycontactid`) to their display names (like `Primary Contact`). This makes your reports much easier to understand and your field list cleaner to navigate.

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

---

## 📁 Understanding Your Generated Project

The tool creates a **Power BI Project (PBIP)** folder structure:

```
YourModelName/
├── YourModelName.pbip           ← Open this file in Power BI Desktop
├── YourModelName.SemanticModel/
│   └── definition/
│       ├── model.tmdl          ← Model configuration
│       ├── relationships.tmdl  ← Table relationships
│       └── tables/             ← Individual table definitions
│           ├── Account.tmdl
│           ├── Contact.tmdl
│           ├── Date.tmdl
│           └── DataverseURL.tmdl
└── YourModelName.Report/
    └── (report definition files)
```

The PBIP format is a folder-based project that's perfect for:
- Version control with Git
- Collaboration with other developers
- Seeing exactly what changed between versions

📚 **Learn More About PBIP:**
- [Power BI Project Files Overview](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-overview)
- [Working with PBIP in Power BI Desktop](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-build)
- [Power BI Desktop Developer Mode](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-overview#developer-mode)

---

## ⚡ Direct Query vs. Import Mode

Your generated model uses **DirectQuery** by default. Here's what that means and how to choose the right mode for your needs.

### DirectQuery Mode (Default)

**How it works:** Every time you interact with your report, Power BI sends a query to Dataverse and retrieves fresh data.

| ✅ Advantages | ⚠️ Considerations |
|--------------|-------------------|
| Always shows the latest data | Slower interactivity (1-5 second delays) |
| Respects Dataverse row-level security | Some DAX functions not available |
| No scheduled refresh needed | Subject to Dataverse API limits |
| Smaller .pbix file size | Complex calculations may timeout |
| Users see only their permitted data | Best with fewer concurrent users |

**Best for:** 
- Real-time operational dashboards  
- Security-sensitive data where each user should only see their records
- Data that changes frequently throughout the day

### Import Mode

**How it works:** Data is copied into the Power BI file and stored in memory. Reports query this in-memory cache.

| ✅ Advantages | ⚠️ Considerations |
|--------------|-------------------|
| Blazing fast interactivity | Data can be stale between refreshes |
| Full DAX functionality | Requires scheduled refresh (up to 8x/day) |
| Works offline | Larger file sizes |
| No per-query API limits | Security must be applied at report level (RLS) |
| Handles many concurrent users | Dataverse security not automatic |

**Best for:** 
- Historical trend analysis
- Complex calculations requiring full DAX
- Published reports with many users
- Executive dashboards refreshed daily

### Dual Mode (The Best of Both Worlds)

You can use **different storage modes for different tables**—this is often the optimal approach:

- Keep your large, frequently-changing **fact table in DirectQuery**
- Import smaller, stable **dimension tables** for fast filtering

| ✅ Advantages | ⚠️ Considerations |
|--------------|-------------------|
| Fast dimension filtering and slicing | Requires refresh schedule for dimensions |
| Always-current fact data | Slightly more complex to configure |
| Efficient for large fact tables | Must plan which tables to import |
| Reduces Dataverse API calls | |

**To switch a table to Import mode:**
1. Open your model in Power BI Desktop
2. Go to **Model** view
3. Select the table you want to change
4. In the **Properties** pane, change **Storage mode** from "DirectQuery" to "Import"
5. Configure a refresh schedule when you publish to Power BI Service

📚 **Learn More:**
- [DirectQuery in Power BI](https://learn.microsoft.com/en-us/power-bi/connect-data/desktop-directquery-about)
- [Storage Mode in Power BI Desktop](https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-storage-mode)
- [Composite Models in Power BI](https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-composite-models)

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

## 📈 Suggested Next Steps

Once you've built and opened your model in Power BI Desktop, here are recommended enhancements:

### 1. Create Key Measures
Add calculated measures for your most important metrics:
```dax
Total Revenue = SUM(Orders[Amount])
Order Count = COUNTROWS(Orders)
Average Order Value = DIVIDE([Total Revenue], [Order Count])
Win Rate = DIVIDE(
    COUNTROWS(FILTER(Opportunities, Opportunities[Status] = "Won")),
    COUNTROWS(Opportunities)
)
```

### 2. Build Hierarchies
Create drill-down paths in your dimension tables:
- **Date:** Year → Quarter → Month → Week → Day
- **Geography:** Country → State/Province → City
- **Organization:** Business Unit → Team → Owner

### 3. Apply Formatting
- Set currency columns to display with $ symbols
- Set percentage fields to show as percentages
- Add column descriptions to help other report builders
- Mark your key date column as the "Date table" for time intelligence

### 4. Hide Technical Columns
By default, this utility follows the best practice of hiding columns that end users don't need:
- GUID/ID columns (like `accountid`) 
- They are kept in the model for relationships, but hidden from report view
- If you find that you have additional values that are only needed for formulas, you can hide them as well.

### 5. Create a Model Documentation Page
Add a report page that shows:
- Your data model diagram
- Key measure definitions
- Data refresh information
- Known limitations

### 6. Consider Row-Level Security (for Import Mode)
If using Import mode, implement RLS to control data access:
1. Go to **Modeling** → **Manage roles**
2. Create roles that filter data based on user context
3. Assign users to roles in Power BI Service

📚 **Learn More:**
- [Create Measures in Power BI](https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-measures)
- [Create Hierarchies](https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-create-and-manage-relationships)
- [Row-Level Security in Power BI](https://learn.microsoft.com/en-us/power-bi/enterprise/service-admin-rls)

---

## ❓ Frequently Asked Questions

### Q: Can I add more tables after the initial build?
**A:** Yes! Run the tool again, select additional tables, and rebuild. Your existing customizations (measures, formatting, hierarchies) will be preserved.

### Q: What happens if our Dataverse schema changes?
**A:** The tool will detect changes when you run it again. It shows you a preview of what's new, modified, or removed before applying updates—you're always in control.

### Q: Can I use this with Dynamics 365 apps?
**A:** Absolutely! This works with any Dataverse environment, including Dynamics 365 Sales, Customer Service, Field Service, Marketing, and custom Power Apps.

### Q: Why are some of my columns missing?
**A:** Columns are pre-selected by default from your selected form. If a field isn't on the form, it won't pre-selected to be in the model by default. You can add columns that aren't on the form by switching to view "All" attributes and checking the selection box beside any additional ones you need.

### Q: How do I handle many-to-many relationships?
**A:** The tool creates standard many-to-one relationships. For many-to-many scenarios (like Contacts associated with multiple Accounts), you may need to include the intersection table and create a bridge pattern manually. - These can become complex and require more expertise in proper modeling to ensure your results are reflective of your intent.

### Q: My report is slow—what can I do?
**A:** Try these optimizations:
1. Reduce the number of columns (only include what you need)
2. Remove long text fields such as text area fields.
3. Use view more aggressive filters to limit rows to only those typically relevant to the user.
4. Switch dimension tables to Dual mode
5. Simplify visuals (fewer visuals per page)
6. Use aggregations for large fact tables
7. For larger datasets, moving the fact table to Import Mode will make the interaction more responsive.
8. Filtering on large Date Ranges using a Direct Query source with the Date Table can cause timeouts based on the way the query is constructed. You may want to apply the query to the date field directly from within the query pane rather than relying on the Date Table.

### Q: Can I version control my Power BI project?
**A:** Yes! The PBIP format is designed for Git. Each table, relationship, and report element is a separate text file that shows meaningful changes in version control.

---

## 🔧 Troubleshooting

### "Cannot connect to Dataverse"
- Verify your XrmToolBox connection is active
- Check that you have read permissions in Dataverse
- Try disconnecting and reconnecting
- Ensure your access to the TDS endpoint is not blocked by either permissions inside dataverse or via network policy
- See more here: https://learn.microsoft.com/power-apps/developer/data-platform/dataverse-sql-query

### "Build failed with errors"
- Check the working folder path is valid and writable
- Ensure you have selected at least one table
- Review the error message for specific guidance

### "Power BI Desktop won't open the .pbip file"
- Ensure you have Power BI Desktop (November 2023 or later)
- Enable Developer Mode: File → Options → Preview features → Power BI Project files
- Check that all files in the folder exist and aren't corrupted

### "Missing relationships in the model"
- Verify your lookup columns were included in the selected form
- Check that both source and target tables are in the model
- Re-run the table selection and view the created tables

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
