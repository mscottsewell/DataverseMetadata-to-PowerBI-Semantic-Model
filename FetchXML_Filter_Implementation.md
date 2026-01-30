# FetchXML View Filter Implementation

## Overview
The Dataverse Metadata Extractor now automatically converts FetchXML view filters to SQL WHERE clauses, applying them to the Power Query M expressions in your semantic model.

## What's Implemented

### Phase 1 - Current Support

#### Basic Comparison Operators
- `eq` → `=` (equals)
- `ne` → `<>` (not equals)
- `gt` → `>` (greater than)
- `ge` → `>=` (greater than or equal)
- `lt` → `<` (less than)
- `le` → `<=` (less than or equal)

#### Null Operators
- `null` → `IS NULL`
- `not-null` → `IS NOT NULL`

#### String Operators
- `like` → `LIKE`
- `not-like` → `NOT LIKE`
- `begins-with` → `LIKE 'value%'`
- `not-begin-with` → `NOT LIKE 'value%'`
- `ends-with` → `LIKE '%value'`
- `not-end-with` → `NOT LIKE '%value'`

#### Date Operators (Relative)
- `today` - Today's date
- `yesterday` - Yesterday
- `tomorrow` - Tomorrow
- `this-week` - Current week
- `last-week` - Previous week
- `next-week` - Next week
- `this-month` - Current month
- `last-month` - Previous month
- `next-month` - Next month
- `this-year` - Current year
- `last-year` - Previous year
- `next-year` - Next year

#### Date Comparison Operators
- `on` → Exact date match
- `on-or-after` → Greater than or equal
- `on-or-before` → Less than or equal

#### List Operators
- `in` → `IN (val1, val2, ...)`
- `not-in` → `NOT IN (val1, val2, ...)`

#### Complex Features
- ✅ Nested filter groups (`<filter type="and">` / `<filter type="or">`)
- ✅ Mixed AND/OR logic
- ⚠️ Link entities (logged as unsupported but main entity filters are applied)

## How It Works

### 1. View Selection
When you select a view for a table in the UI, the FetchXML is retrieved and stored with the table configuration.

### 2. Build Process
During semantic model build:
1. FetchXML is parsed for each table with a view
2. Filter conditions are converted to SQL
3. SQL is combined with existing filters (e.g., `statecode = 0`)
4. Results are embedded in the Power Query M expression

### 3. Visual Indicators
**In the UI:**
- View name appears in the "Filter" column
- `*` appears next to view name if filter is partially supported

**In the Semantic Model:**
- Table description shows: `"Filtered by view: [ViewName] *"`
- SQL comments document the filter in the M expression
- Comments list any unsupported features

### Example Output

```m
partition Position = m
    mode: directQuery
    source =
        let
            Dataverse = CommonDataService.Database(DataverseURL,[CreateNavigationProperties=false]),
            Source = Value.NativeQuery(Dataverse,"
            
            -- View Filter: Active Positions
            -- Filter: ((Base.statecode = 0) AND ((DATEPART(year, Base.createdon) = DATEPART(year, GETDATE())) OR (DATEPART(year, Base.createdon) = DATEPART(year, DATEADD(year, -1, GETDATE())))))
            
            SELECT Base.tov2_positionid
                  ,Base.tov2_positionnumber
                  ,Base.tov2_positionstatus
                  ...
            FROM tov2_position as Base
            WHERE (Base.statecode = 0) AND ((Base.statecode = 0) AND ((DATEPART(year, Base.createdon) = DATEPART(year, GETDATE())) OR (DATEPART(year, Base.createdon) = DATEPART(year, DATEADD(year, -1, GETDATE())))))
            
            " ,null ,[EnableFolding=true])
        in
            Source
```

## Debugging & Troubleshooting

### Debug Logs
Every view filter conversion generates a detailed debug log in:
```
[OutputFolder]/FetchXML_Debug/[ViewName]_[Timestamp].txt
```

**Log Contents:**
- Input FetchXML (formatted)
- Conversion summary
- List of unsupported features
- Step-by-step processing log
- Final SQL WHERE clause

### Unsupported Feature Notifications
If a filter contains unsupported features:
1. A summary appears in the conversion result
2. The table description includes `*` marker
3. SQL comments list what wasn't translated
4. Debug log contains full details

### Common Issues

**Issue:** No filter applied despite selecting a view
- Check if view has any `<filter>` elements in FetchXML
- Verify FetchXML was retrieved (check debug log)

**Issue:** Partial filter applied
- Review debug log to see which operators were unsupported
- Check if link-entity filters were involved (not yet supported)

**Issue:** SQL syntax error in Power BI
- Check debug log for the generated SQL
- Verify attribute names match schema
- Report issue with debug log for investigation

## Sharing Debug Information

To share a filter conversion issue:

1. Build your semantic model
2. Locate the debug file in `[OutputFolder]/FetchXML_Debug/`
3. Share the `[ViewName]_[Timestamp].txt` file
4. Include any Power BI error messages

**The debug file contains:**
- FetchXML input (sanitized of sensitive data)
- Conversion logic trace
- Generated SQL

## Future Enhancements

### Planned Support
- More date operators (last-x-days, next-x-months, etc.)
- User context operators (eq-userid, eq-userteams, etc.)
- Link-entity filter translation
- Between operator
- Attribute-to-attribute comparisons

### Known Limitations
- Link-entity filters not supported (only main entity)
- Some advanced date operators not implemented
- No support for calculated values in conditions

## Testing Your Filters

**Recommended Approach:**
1. Start with a simple view filter
2. Build semantic model
3. Check table description for `*` marker
4. Review debug log if issues occur
5. Test in Power BI Desktop
6. Gradually add complexity

**Example Test Progression:**
1. Simple: `statecode = 0`
2. Add date: `createdon = this-year`
3. Add OR logic: `this-year OR last-year`
4. Add nested filters

## Technical Details

**Classes:**
- `FetchXmlToSqlConverter` - Core conversion logic
- Located in: `Services/FetchXmlToSqlConverter.cs`

**Integration Points:**
- `SemanticModelBuilder.GenerateTableTmdl()` - Applies filters during build
- `ExportTable.View` - Contains FetchXML for each table

**SQL Generation:**
- Uses `Base` as table alias
- Combines with existing WHERE conditions using AND
- Wraps complex conditions in parentheses
- Preserves operator precedence

## Version History

**v1.0 (Current)**
- Basic operators (eq, ne, gt, ge, lt, le)
- Null operators
- String operators (like, begins-with, ends-with)
- Date comparison operators (on, on-or-after, on-or-before)
- Relative date operators (today, this-year, last-month, etc.)
- List operators (in, not-in)
- Nested AND/OR filter groups
- Debug logging
- Partial support indicators

---

*For questions or issues, provide the debug log file from FetchXML_Debug folder*
