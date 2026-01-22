import pandas as pd
import sys
import os

# Get metadata folder from command line argument
if len(sys.argv) < 2:
    print("Usage: python extract_tables.py <metadata_folder>")
    print("Example: python extract_tables.py 'Reports/Dynamics 365 Sales/Metadata'")
    sys.exit(1)

metadata_folder = sys.argv[1]

# Find the Excel file in the metadata folder (looks for any .xlsx file)
import glob
excel_files = glob.glob(os.path.join(metadata_folder, '*.xlsx'))
if not excel_files:
    print(f"Error: No Excel files found in {metadata_folder}")
    sys.exit(1)
excel_file = excel_files[0]

# Read the Excel file
df = pd.read_excel(excel_file, sheet_name=0, skiprows=1)

# The first row contains the actual column headers
df.columns = df.iloc[0]
df = df[1:].reset_index(drop=True)

# Extract Entity and Schema Name columns
result = df[['Entity', 'Schema Name']].dropna()

print("Power BI Tables List:")
print("=" * 80)
print(f"\n{'Display Name':<35} {'Schema Name':<40}")
print("-" * 80)

for idx, row in result.iterrows():
    entity = row['Entity']
    schema = row['Schema Name']
    print(f"{entity:<35} {schema:<40}")

print("\n" + "=" * 80)
print(f"Total Tables: {len(result)}")
