import pandas as pd
import sys
import os

# Get metadata folder from command line argument
if len(sys.argv) < 2:
    print("Usage: python extract_fields.py <metadata_folder>")
    print("Example: python extract_fields.py 'Reports/Dynamics 365 Sales/Metadata'")
    sys.exit(1)

metadata_folder = sys.argv[1]

# Find the Excel file in the metadata folder (looks for any .xlsx file)
import glob
excel_files = glob.glob(os.path.join(metadata_folder, '*.xlsx'))
if not excel_files:
    print(f"Error: No Excel files found in {metadata_folder}")
    sys.exit(1)
excel_file = excel_files[0]

# Read the Metadata tab
df = pd.read_excel(excel_file, sheet_name='Metadata')

# Read the Entities list to get our table list
entities_df = pd.read_excel(excel_file, sheet_name=0, skiprows=1)
entities_df.columns = entities_df.iloc[0]
entities_df = entities_df[1:].reset_index(drop=True)
table_list = entities_df[['Entity', 'Schema Name']].dropna()

# Get the relevant columns from Metadata
fields_df = df[['Entity Logical Name', 'Schema Name', 'Display Name']].dropna()

print("=" * 100)
print("POWER BI TABLES AND FIELDS")
print("=" * 100)

# For each table in our list, find matching fields
for idx, table_row in table_list.iterrows():
    table_display_name = table_row['Entity']
    table_schema_name = table_row['Schema Name']
    
    # Find all fields that belong to this table (case-insensitive match)
    table_fields = fields_df[fields_df['Entity Logical Name'].str.lower() == table_schema_name.lower()]
    
    if len(table_fields) > 0:
        print(f"\n\n{'='*100}")
        print(f"TABLE: {table_display_name} (Schema: {table_schema_name})")
        print(f"{'='*100}")
        print(f"{'Field Display Name':<50} {'Field Schema Name':<45}")
        print(f"{'-'*100}")
        
        for field_idx, field_row in table_fields.iterrows():
            field_display = field_row['Display Name']
            field_schema = field_row['Schema Name']
            print(f"{field_display:<50} {field_schema:<45}")
        
        print(f"\nTotal Fields: {len(table_fields)}")
    else:
        print(f"\n\nTABLE: {table_display_name} (Schema: {table_schema_name}) - No fields found")

print("\n\n" + "=" * 100)
print("EXTRACTION COMPLETE")
print("=" * 100)
