import pandas as pd
import sys
import os

# Get metadata folder from command line argument or use default
metadata_folder = sys.argv[1] if len(sys.argv) > 1 else 'Metadata/ImaginationWorkshop'
excel_file = os.path.join(metadata_folder, 'ImaginationWorkshop Metadata Dictionary.xlsx')

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
