# Implementation Spec: Lookup Field Grouping + Polymorphic Display Name Bug Fix

## Project Context

**Codebase:** `c:\GitHub\XRMToolBox Utilities\DataverseToPowerBI`
**Solution structure:**
- `DataverseToPowerBI.Core` — shared models (`DataModels.cs`)
- `DataverseToPowerBI.XrmToolBox` — WinForms plugin UI (`PluginControl.cs`), generation service (`Services/SemanticModelBuilder.cs`)
- `DataverseToPowerBI.Tests` — xUnit tests (`SemanticModelBuilderTests.cs`)

**Key data model facts you need before reading further:**
- `ExportTable` (Core `DataModels.cs:1077`) — the table object passed to `SemanticModelBuilder`. Has `Attributes` (selected `AttributeMetadata` list) and `ExpandedLookups` (`List<ExpandedLookupConfig>`).
- `AttributeMetadata` (Core `DataModels.cs:797`) — `LogicalName`, `DisplayName`, `AttributeType`, `Targets` (`List<string>`), `SchemaName`, etc.
- `ExpandedLookupConfig` (Core `DataModels.cs:1205`) — per-lookup expansion config: `LookupAttributeName`, `TargetTableLogicalName`, `TargetTablePrimaryKey`, `Attributes` (list of `ExpandedLookupAttribute`).
- `AttributeDisplayInfo` (Core `DataModels.cs:480`) — passed to builder per attribute. Has `OverrideDisplayName` (`string?`), `DisplayName`, `AttributeType`, `Targets`, etc.
- `PluginControl` state: `_selectedAttributes[tableLogicalName]` = `HashSet<string>` of selected attribute logical names; `_attributeDisplayNameOverrides[tableLogicalName][attrLogicalName]` = user display name override; `_expandedLookups[tableLogicalName]` = `List<ExpandedLookupConfig>`.
- **Two `ExportTable` classes exist**: one in `PluginControl.cs:4258` (`[DataContract]`, used for serialization to JSON), one in `Core/DataModels.cs:1077` (used by `SemanticModelBuilder`). Both need the new `LookupSubColumnConfigs` property. The `PrepareExportData()` method at `PluginControl.cs:3615` builds the Core `ExportTable` from the serialization one.
- `GetEffectiveDisplayName` (`SemanticModelBuilder.cs`) — returns `OverrideDisplayName` whenever present, else returns `fallbackDisplayName`. Display-name overrides are model-level naming and remain active even when SQL alias toggles are off.
- `processedColumns` (`HashSet<string>`) — prevents double-processing attributes within the generation loop.
- `isHidden` on `ColumnInfo` (SemanticModelBuilder) maps directly to `isHidden` in TMDL output at line 4309–4312. This is the Power BI "visible in field list" flag.

---

## Part 1 — Bug Fix: Polymorphic Virtual Column Display Name

### Problem

For **Owner** and **Customer** attribute types, Dataverse exposes virtual name columns (`owneridname`, `owneridtype`, `owneridyominame`, `customeridname`, etc.) as **separate metadata attributes** that appear in `_tableAttributes` and can be selected by users. For regular **Lookup** types, `{field}name` is never a separate metadata attribute.

In `SemanticModelBuilder`, the main attribute processing loop guards against double-processing via `processedColumns`. The intent is: when `ownerid` (Owner type) is processed, it adds `owneridname` to `processedColumns` and generates the name column using `ownerid`'s `effectiveName`. When `owneridname` arrives later in the loop, it is skipped.

**The bug**: this is order-sensitive. If `owneridname` appears in `table.Attributes` before `ownerid` (i.e., the user selected both, and Dataverse returned `owneridname` first in metadata ordering), then:
1. `owneridname` is processed as a `String` attribute — its `effectiveName` = `owneridname.DisplayName ?? owneridname.LogicalName`. Dataverse typically returns null or an empty display name for these virtual columns, so `effectiveName = "owneridname"`. The column is added to `columns`/`ColumnInfo` as `DisplayName = "owneridname"` (the logical name, not `"Owner"`).
2. `"owneridname"` is added to `processedColumns`.
3. When `ownerid` arrives, `processedColumns.Contains("owneridname")` is true → **name column generation is skipped**. The parent's display name and override are never applied.

This cannot happen with regular Lookup types (`productid`) because `productidname` is never in `table.Attributes`.

### Fix

**Location**: Every attribute processing loop in `SemanticModelBuilder.cs` that has the `if (processedColumns.Contains(attr.LogicalName)) continue;` guard at the top. This pattern appears in at least three places (the SQL+ColumnInfo generation paths and the change-detection path).

**Change**: After the existing `processedColumns` check, add a **polymorphic virtual column guard**:

```csharp
// Guard: if this attribute is a known virtual sub-column of a polymorphic lookup
// (e.g. owneridname for ownerid Owner type, customeridtype for customerid Customer type),
// and the parent lookup attribute is also in table.Attributes, skip it here.
// The parent lookup's processing will generate this column with the correct display name.
if (IsPolymorphicVirtualSubColumn(attr.LogicalName, table.Attributes))
    continue;
```

Implement `IsPolymorphicVirtualSubColumn` as a private static method:

```csharp
private static readonly HashSet<string> PolymorphicVirtualSuffixes =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "type", "yominame" };

private static readonly HashSet<string> PolymorphicParentTypes =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Owner", "Customer" };

private static bool IsPolymorphicVirtualSubColumn(
    string attrLogicalName,
    IEnumerable<AttributeMetadata> tableAttributes)
{
    foreach (var suffix in PolymorphicVirtualSuffixes)
    {
        if (!attrLogicalName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            continue;

        var parentName = attrLogicalName[..^suffix.Length]; // strip suffix
        if (string.IsNullOrEmpty(parentName)) continue;

        var parent = tableAttributes.FirstOrDefault(a =>
            a.LogicalName.Equals(parentName, StringComparison.OrdinalIgnoreCase));

        if (parent != null && PolymorphicParentTypes.Contains(parent.AttributeType ?? ""))
            return true;
    }
    return false;
}
```

Apply this guard in all three attribute processing loops:
1. `SemanticModelBuilder.cs` ~line 1824 (TDS change-detection path)
2. `SemanticModelBuilder.cs` ~line 2119 (SQL generation path)
3. `SemanticModelBuilder.cs` ~line 3792 (FabricLink TMDL generation path)

**Also**: The existing `owningusername`/`owningteamname`/`owningbusinessunitname` skip at lines 1834–1838, 2127–2131, 3804–3810 can remain as-is; the new guard above would additionally catch any ordering issues for those, but they are skipped anyway by the explicit check.

---

## Part 2 — New Feature: Lookup Field Grouping with Include/Hidden Control

### 2.1 Overview

**Goal**: Replace the current flat-row treatment of lookup fields in the attribute list with a collapsible group row. Under each lookup group, show configurable sub-rows for the ID field and name field (and for Owner/Customer polymorphic types, also type and yomi sub-rows). Each sub-row has its own **Include** checkbox (whether the column is in the model) and **Hidden** checkbox (whether it is visible to report consumers — maps to `isHidden` in TMDL).

**Affected attribute types:**

| Attribute Type | Group sub-rows | Notes |
|---|---|---|
| `Lookup` | ID sub-row + Name sub-row | Name column (`{field}name`) synthesized by tool, not in Dataverse metadata |
| `Owner` | ID + Name + Type + Yomi sub-rows | Virtual columns are separate Dataverse metadata attributes; detect by suffix convention |
| `Customer` | ID + Name + Type + Yomi sub-rows | Same pattern as Owner |
| `owninguser`, `owningteam`, `owningbusinessunit` | ID sub-row only | Name columns not available in TDS/FabricLink endpoints |

**Unaffected**: All non-lookup attribute types keep their current flat-row treatment.

---

### 2.2 Smart Defaults

When a `LookupSubColumnConfig` has `null` values, computed (smart) defaults are used. Defaults are re-evaluated whenever the relationship configuration changes (retroactive).

**Relationship detection rule**: A lookup field is in "relationship mode" if there exists at least one explicitly configured relationship in `_relationships` (active OR inactive) where `SourceAttribute` equals the lookup's logical name on the same source table. The target table being in the model is NOT sufficient alone — there must be an explicit relationship.

**Non-relationship mode defaults** (no explicit relationship using this field):

| Sub-row | Include | Hidden |
|---|---|---|
| ID (GUID) | `false` | `false` (n/a, not included) |
| Name | `true` | `false` (visible) |
| Type (Owner/Customer only) | `false` | `false` |
| Yomi (Owner/Customer only) | `false` | `false` |

**Relationship mode defaults** (explicit relationship exists using this field):

| Sub-row | Include | Hidden |
|---|---|---|
| ID (GUID) | `true` | `true` (in model, hidden — required for relationship) |
| Name | `false` | `false` (excluded — redundant to related table) |
| Type (Owner/Customer only) | `false` | `false` |
| Yomi (Owner/Customer only) | `false` | `false` |

**Hidden implies Include**: If `Hidden = true`, `Include` must be `true`. Checking Hidden auto-checks Include. Unchecking Include auto-unchecks Hidden.

---

### 2.3 New Data Model

#### In `DataverseToPowerBI.Core/Models/DataModels.cs`

Add after `ExpandedLookupConfig`:

```csharp
/// <summary>
/// Per-sub-column configuration for a lookup field's ID and name columns.
/// Null values mean "use smart defaults based on relationship detection."
/// </summary>
public class LookupSubColumnConfig
{
    /// <summary>Logical name of the parent lookup attribute (e.g. "productid").</summary>
    public string LookupAttributeLogicalName { get; set; } = "";

    // ID (GUID) sub-row
    public bool? IncludeIdField { get; set; }     // null = smart default
    public bool? IdFieldHidden { get; set; }       // null = smart default

    // Name sub-row ({lookup}name)
    public bool? IncludeNameField { get; set; }    // null = smart default
    public bool? NameFieldHidden { get; set; }     // null = smart default

    // Type sub-row ({lookup}type) — Owner/Customer only
    public bool? IncludeTypeField { get; set; }    // null = default false (excluded)
    public bool? TypeFieldHidden { get; set; }

    // Yomi sub-row ({lookup}yominame) — Owner/Customer only
    public bool? IncludeYomiField { get; set; }    // null = default false (excluded)
    public bool? YomiFieldHidden { get; set; }
}
```

#### In `DataverseToPowerBI.Core/Models/DataModels.cs` — `ExportTable` class

Add property:

```csharp
/// <summary>
/// Per-lookup sub-column configurations. Keyed by lookup attribute logical name.
/// Null config or missing key = use smart defaults.
/// </summary>
public Dictionary<string, LookupSubColumnConfig>? LookupSubColumnConfigs { get; set; }
```

---

### 2.4 PluginControl Runtime State

#### New field in `PluginControl.cs`:

```csharp
// Lookup sub-column configs: key = table logical name, value = dict of lookup logical name → config
private Dictionary<string, Dictionary<string, LookupSubColumnConfig>> _lookupSubColumnConfigs =
    new Dictionary<string, Dictionary<string, LookupSubColumnConfig>>(StringComparer.OrdinalIgnoreCase);
```

#### New serialization class (alongside `SerializedExpandedLookup`):

```csharp
[DataContract]
public class SerializedLookupSubColumnConfig
{
    [DataMember] public string LookupAttributeLogicalName { get; set; } = "";
    [DataMember] public bool? IncludeIdField { get; set; }
    [DataMember] public bool? IdFieldHidden { get; set; }
    [DataMember] public bool? IncludeNameField { get; set; }
    [DataMember] public bool? NameFieldHidden { get; set; }
    [DataMember] public bool? IncludeTypeField { get; set; }
    [DataMember] public bool? TypeFieldHidden { get; set; }
    [DataMember] public bool? IncludeYomiField { get; set; }
    [DataMember] public bool? YomiFieldHidden { get; set; }
}
```

#### Add to `PluginSettings`:

```csharp
[DataMember]
public Dictionary<string, List<SerializedLookupSubColumnConfig>> LookupSubColumnConfigs { get; set; }
    = new Dictionary<string, List<SerializedLookupSubColumnConfig>>();
```

#### Save/restore in `SaveSettings()`/`LoadSettings()` — follow the same pattern as `ExpandedLookups`.

---

### 2.5 Retroactive Default Recalculation

Implement a method called when the relationship list changes (i.e., whenever `_relationships` is modified):

```csharp
private void RecalculateLookupDefaults()
{
    // Null-valued config fields have their smart defaults recomputed at generation time
    // (via ResolveDefaults in PrepareExportData). No stored values are modified here.
    // This method exists to trigger a UI refresh so computed default indicators update.
    if (_currentTableLogicalName != null)
        UpdateAttributesDisplay(_currentTableLogicalName);
}
```

Call `RecalculateLookupDefaults()` at the end of any method that modifies `_relationships`.

---

### 2.6 `PrepareExportData()` Changes

In `PluginControl.cs:PrepareExportData()`, when building each `ExportTable`, populate `LookupSubColumnConfigs` with **resolved** (non-null) configs using `ResolveDefaults()`:

```csharp
private LookupSubColumnConfig ResolveDefaults(
    string tableLogicalName,
    string lookupLogicalName,
    LookupSubColumnConfig? stored)
{
    var inRelationship = _relationships.Any(r =>
        r.SourceTable.Equals(tableLogicalName, StringComparison.OrdinalIgnoreCase) &&
        r.SourceAttribute.Equals(lookupLogicalName, StringComparison.OrdinalIgnoreCase));

    return new LookupSubColumnConfig
    {
        LookupAttributeLogicalName = lookupLogicalName,
        IncludeIdField   = stored?.IncludeIdField   ?? (inRelationship ? true  : false),
        IdFieldHidden    = stored?.IdFieldHidden     ?? (inRelationship ? true  : false),
        IncludeNameField = stored?.IncludeNameField  ?? (inRelationship ? false : true),
        NameFieldHidden  = stored?.NameFieldHidden   ?? false,
        IncludeTypeField = stored?.IncludeTypeField  ?? false,
        TypeFieldHidden  = stored?.TypeFieldHidden   ?? false,
        IncludeYomiField = stored?.IncludeYomiField  ?? false,
        YomiFieldHidden  = stored?.YomiFieldHidden   ?? false,
    };
}
```

In `PrepareExportData()`, for each table and each Lookup/Owner/Customer attribute in the table's selected attributes:

```csharp
var tableConfigs = _lookupSubColumnConfigs.ContainsKey(t.LogicalName)
    ? _lookupSubColumnConfigs[t.LogicalName]
    : new Dictionary<string, LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase);

table.LookupSubColumnConfigs = new Dictionary<string, LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase);

foreach (var attr in table.Attributes ?? new List<AttributeMetadata>())
{
    var attrType = attr.AttributeType ?? "";
    var isLookupType = attrType.Equals("Lookup", StringComparison.OrdinalIgnoreCase)
                    || attrType.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                    || attrType.Equals("Customer", StringComparison.OrdinalIgnoreCase);
    if (!isLookupType) continue;

    var stored = tableConfigs.ContainsKey(attr.LogicalName) ? tableConfigs[attr.LogicalName] : null;
    table.LookupSubColumnConfigs[attr.LogicalName] = ResolveDefaults(t.LogicalName, attr.LogicalName, stored);
}
```

---

### 2.7 Backward Compatibility / Migration

When loading an existing model config that has no `LookupSubColumnConfigs` entry for a given lookup, the null/missing config produces smart defaults via `ResolveDefaults()`. Since:
- Smart defaults for a **relationship** lookup → `ID: included+hidden, Name: excluded` — matches current behavior
- Smart defaults for a **non-relationship** lookup → `ID: excluded, Name: included+visible` — matches current behavior

No explicit migration is needed for the common case.

**Exception — orphaned polymorphic virtual columns**: If an existing model has `owneridname`, `owneridtype`, or `owneridyominame` as individually selected flat attributes (checked separately in the old UI), run a migration on load:

```csharp
private void MigratePolymorphicVirtualColumns(string tableLogicalName)
{
    if (!_selectedAttributes.ContainsKey(tableLogicalName)) return;
    if (!_tableAttributes.ContainsKey(tableLogicalName)) return;

    var selected = _selectedAttributes[tableLogicalName];
    var tableAttrs = _tableAttributes[tableLogicalName];

    var toRemove = new List<string>();
    foreach (var attrLogicalName in selected.ToList())
    {
        foreach (var suffix in new[] { "name", "type", "yominame" })
        {
            if (!attrLogicalName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            var parentName = attrLogicalName[..^suffix.Length];
            if (string.IsNullOrEmpty(parentName)) continue;

            var parent = tableAttrs.FirstOrDefault(a =>
                a.LogicalName.Equals(parentName, StringComparison.OrdinalIgnoreCase) &&
                (a.AttributeType?.Equals("Owner", StringComparison.OrdinalIgnoreCase) == true ||
                 a.AttributeType?.Equals("Customer", StringComparison.OrdinalIgnoreCase) == true));

            if (parent == null) continue;

            // Migrate: remove from flat selection, create a config entry for the parent lookup
            toRemove.Add(attrLogicalName);
            if (!_lookupSubColumnConfigs.ContainsKey(tableLogicalName))
                _lookupSubColumnConfigs[tableLogicalName] = new Dictionary<string, LookupSubColumnConfig>(StringComparer.OrdinalIgnoreCase);

            if (!_lookupSubColumnConfigs[tableLogicalName].ContainsKey(parentName))
                _lookupSubColumnConfigs[tableLogicalName][parentName] = new LookupSubColumnConfig
                    { LookupAttributeLogicalName = parentName };

            var config = _lookupSubColumnConfigs[tableLogicalName][parentName];
            if (suffix.Equals("name", StringComparison.OrdinalIgnoreCase))
                config.IncludeNameField = true;
            else if (suffix.Equals("type", StringComparison.OrdinalIgnoreCase))
                config.IncludeTypeField = true;
            else if (suffix.Equals("yominame", StringComparison.OrdinalIgnoreCase))
                config.IncludeYomiField = true;

            break;
        }
    }

    foreach (var name in toRemove)
        selected.Remove(name);
}
```

Call `MigratePolymorphicVirtualColumns(tableLogicalName)` during `LoadSettings()` for each table after restoring `_selectedAttributes`.

---

### 2.8 Display Name Override for Name Sub-Row

The existing `_attributeDisplayNameOverrides[tableLogicalName][lookupLogicalName]` override (keyed by the **parent lookup's** logical name, e.g. `"productid"`) continues to be used as the display name of the **Name sub-row** column in the generated model. This is unchanged from the current behavior.

In the new UI, double-clicking the **Name sub-row** opens the rename dialog, which stores the override under the parent lookup's logical name (unchanged storage key).

- The **ID sub-row** always uses the logical name as its display name (no override).
- **Type and Yomi sub-rows** use their logical names as display names (no override in this release).
- The **group header** always displays the lookup's Dataverse display name — not user-overridable at the header level.

---

### 2.9 UI Changes — ListView Columns

**New columns added to `listViewAttributes`:**

| # | Name | Width | Purpose |
|---|---|---|---|
| 0 | (Sel) | existing | Checkbox. For non-lookup: select/deselect (unchanged). For group header: clicking applies smart defaults (sets all config fields to null). |
| 1 | Form | existing | Lock/checkmark |
| 2 | Display Name | existing | |
| 3 | Logical Name | existing | |
| 4 | Type | existing | |
| **5** | **Incl** | **30px** | **NEW — Include checkbox, visible only on lookup sub-rows** |
| **6** | **Hid** | **30px** | **NEW — Hidden checkbox, visible only on lookup sub-rows** |
| 7 | Expand | existing | Moved from col 5 to col 7 |

For Incl and Hid cells: use text symbols `"☑"` / `"☐"` in the sub-item text (consistent with existing expanded-lookup visual style).

Update all hardcoded column index references — e.g. `subItemIndex != 5` in the mouse click handler — to use the new indices.

---

### 2.10 Group Header Row Rendering

- **Display Name cell**: `"▼ {lookup.DisplayName}"` (expanded) or `"▶ {lookup.DisplayName}"` (collapsed)
- **Logical Name cell**: lookup logical name (unchanged)
- **Type cell**: `"Lookup"` / `"Owner"` / `"Customer"` (unchanged)
- **Sel checkbox**: checked = all sub-rows included; unchecked = none included; indeterminate = partial. Clicking applies smart defaults.
- **Expand button (col 7)**: same behavior as today's col-5 Expand — opens `ExpandLookupForm` for the lookup
- **Incl (col 5)** and **Hid (col 6)**: blank for group headers
- Background/foreground: same as current lookup rows (no special color)
- `item.Tag`: the lookup's logical name (unchanged from today)

---

### 2.11 Sub-Row Rendering

Sub-rows appear indented under their group header when the group is expanded.

**Sub-row display name format**: `"    ↳ {subRowDisplayName}"` (4 spaces + arrow, consistent with existing expanded attribute sub-rows)

**Sub-row display names by sub-type:**
- ID: the lookup's logical name (e.g. `"productid"`)
- Name: effective display name — `_attributeDisplayNameOverrides[table][lookupLogicalName]` if set, else `lookup.DisplayName`
- Type: `"{lookupLogicalName}type"` (logical name)
- Yomi: `"{lookupLogicalName}yominame"` (logical name)

**Sub-row columns:**
- **Sel (col 0)**: blank (no checkbox)
- **Form (col 1)**: blank
- **Display Name (col 2)**: indented name as above
- **Logical Name (col 3)**: the sub-row's actual logical name (e.g. `"productidname"`)
- **Type (col 4)**: `"GUID"` for ID sub-row; `"String"` for Name/Yomi; `"EntityName"` for Type
- **Incl (col 5)**: `"☑"` if included, `"☐"` if not
- **Hid (col 6)**: `"☑"` if hidden, `"☐"` if not; use grey text when Include = false
- **Expand (col 7)**: blank

**Background**: `Color.FromArgb(240, 245, 255)` (light blue, same as existing expanded attribute sub-rows)

**Tag encoding**: `"__sublookup__{parentLogicalName}__{subType}"` where subType is `"id"`, `"name"`, `"type"`, or `"yomi"`

**Existing expanded attribute sub-rows** (from `ExpandedLookupConfig`) render below the ID/Name/Type/Yomi sub-rows within the same group, unchanged.

---

### 2.12 Attribute List Population Changes (`UpdateAttributesDisplay`)

**Skip standalone polymorphic virtual columns**: Before rendering any attribute as a flat row, check `IsPolymorphicVirtualSubColumn(attr.LogicalName, sortedList)`. If true, skip it — it is rendered as a sub-row of its parent group instead.

**For each Lookup/Owner/Customer type attribute:**
1. Render the group header row.
2. If not collapsed: render sub-rows in this order:
   - ID sub-row (always)
   - Name sub-row (always, except `owninguser`/`owningteam`/`owningbusinessunit` which have no name column)
   - Type sub-row (Owner/Customer only)
   - Yomi sub-row (Owner/Customer only)
   - Existing expanded attribute sub-rows (from `ExpandedLookupConfig`, unchanged)

**Collapse state**: stored in `_collapsedLookupGroups` (`HashSet<string>`, keyed by `"{tableLogicalName}.{lookupLogicalName}"`). Default: expanded. Reset when switching tables.

```csharp
private HashSet<string> _collapsedLookupGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
```

**Group header Sel checkbox aggregate state**: before rendering, count how many sub-rows of the lookup have `Include = true`. If all: checked. If none: unchecked. If partial: indeterminate (set `item.StateImageIndex` for indeterminate state, or use a custom rendering approach consistent with the existing ListView).

---

### 2.13 Click Handling

In `ListViewAttributes_MouseClick`, handle new click targets:

- **Group header row, col 0 (Sel checkbox)**: Apply smart defaults — remove config entry for this lookup from `_lookupSubColumnConfigs[table]` (null = smart defaults). Refresh the group's sub-rows.
- **Group header row, col 2 (Display Name, for triangle click)**: Toggle collapse. Rebuild the ListView items for the current table (or use `BeginUpdate`/`EndUpdate` with targeted add/remove for performance).
- **Group header row, col 7 (Expand)**: Existing expand dialog logic (unchanged, just updating the column index from 5 to 7).
- **Sub-row, col 5 (Incl)**: Toggle the relevant `Include*Field` on the config. Enforce: if unchecking Include, also uncheck Hidden. Save to `_lookupSubColumnConfigs`. Refresh sub-row display.
- **Sub-row, col 6 (Hid)**: Toggle the relevant `*FieldHidden` on the config. Enforce: if checking Hidden, also check Include. Save to `_lookupSubColumnConfigs`. Refresh sub-row display.

Determine which sub-type is being clicked from the item's Tag (`"__sublookup__{parent}__{subType}"`).

---

### 2.14 `SemanticModelBuilder` Generation Changes

The builder receives resolved (non-null) `LookupSubColumnConfig` for each lookup via `table.LookupSubColumnConfigs`. Replace the current unconditional ID+name column generation logic with config-driven logic in all three processing paths.

**Pattern to apply** (replace current `if (isLookup)` block):

```csharp
if (isLookup)
{
    var config = table.LookupSubColumnConfigs?.GetValueOrDefault(attr.LogicalName);
    // Fallback to non-relationship defaults if config somehow absent
    bool includeId   = config?.IncludeIdField   ?? false;
    bool idHidden    = config?.IdFieldHidden     ?? false;
    bool includeName = config?.IncludeNameField  ?? true;
    bool nameHidden  = config?.NameFieldHidden   ?? false;
    bool includeType = config?.IncludeTypeField  ?? false;
    bool typeHidden  = config?.TypeFieldHidden   ?? false;
    bool includeYomi = config?.IncludeYomiField  ?? false;
    bool yomiHidden  = config?.YomiFieldHidden   ?? false;

    var isOwningLookup = attr.LogicalName.Equals("owninguser", StringComparison.OrdinalIgnoreCase)
                      || attr.LogicalName.Equals("owningteam", StringComparison.OrdinalIgnoreCase)
                      || attr.LogicalName.Equals("owningbusinessunit", StringComparison.OrdinalIgnoreCase);
    var isPolymorphic = attrType.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                     || attrType.Equals("Customer", StringComparison.OrdinalIgnoreCase);

    // ID column
    if (includeId)
    {
        // Add ID column with IsHidden = idHidden
        // SQL: add Base.{attr.LogicalName} to sqlFields
        // ColumnInfo: LogicalName = attr.LogicalName, DisplayName = attr.LogicalName, IsHidden = idHidden
    }

    // Name column
    var nameColumn = attr.LogicalName + "name";
    if (includeName && !isOwningLookup && !processedColumns.Contains(nameColumn))
    {
        // Add name column with IsHidden = nameHidden
        // DisplayName = effectiveName (from GetEffectiveDisplayName, same as today)
        // SQL: add Base.{nameColumn} with alias if useDisplayNames
    }

    // Type column (Owner/Customer only)
    var typeColumn = attr.LogicalName + "type";
    if (includeType && isPolymorphic && !processedColumns.Contains(typeColumn))
    {
        // Add type column with IsHidden = typeHidden
        // DisplayName = typeColumn (logical name, no override)
        // SQL: add Base.{typeColumn}
    }

    // Yomi column (Owner/Customer only)
    var yomiColumn = attr.LogicalName + "yominame";
    if (includeYomi && isPolymorphic && !processedColumns.Contains(yomiColumn))
    {
        // Add yomi column with IsHidden = yomiHidden
        // DisplayName = yomiColumn (logical name, no override)
        // SQL: add Base.{yomiColumn}
    }

    // Mark all sub-columns as processed regardless of include state,
    // to prevent them being double-added if they appear as separate attributes in table.Attributes
    processedColumns.Add(attr.LogicalName);
    processedColumns.Add(nameColumn);
    if (isPolymorphic) { processedColumns.Add(typeColumn); processedColumns.Add(yomiColumn); }
}
```

Apply this pattern in all three code paths:
1. TDS change-detection path (~line 1849–1882)
2. SQL generation path (~line 2144–2157)
3. FabricLink TMDL generation path (~line 3827–3867)

**`isHidden` in TMDL**: `ColumnInfo.IsHidden = idHidden` (or `nameHidden`, etc.) flows through the existing TMDL writer at line 4309–4312 without any further changes needed.

---

### 2.15 Feature Flag

The `FeatureFlags.EnableExpandLookup` flag currently gates:
- Column 5 (Expand) width/visibility
- Mouse click handling for Expand

With the new design, lookup grouping is always on (not behind a flag). The Expand feature for related-table attribute selection continues to exist but is now always available for lookup type attributes.

**Action**: Remove `FeatureFlags.EnableExpandLookup` checks. The Expand button (now col 7) is always visible for Lookup/Owner/Customer group header rows.

---

### 2.16 Tests Required

Add to `SemanticModelBuilderTests.cs`:

1. **`GenerateTableTmdl_LookupIncludeIdFalse_NoIdColumnInOutput`** — config has `IncludeIdField=false`, verify no ID column in TMDL or SQL.
2. **`GenerateTableTmdl_LookupIncludeIdTrueHidden_IdColumnIsHidden`** — config has `IncludeIdField=true, IdFieldHidden=true`, verify `isHidden` on the ID column.
3. **`GenerateTableTmdl_LookupIncludeNameFalse_NoNameColumnInOutput`** — config has `IncludeNameField=false`, verify no name column.
4. **`GenerateTableTmdl_LookupRelationshipDefaults_IdHiddenNameExcluded`** — resolved defaults for relationship mode: ID present+hidden, name absent.
5. **`GenerateTableTmdl_LookupNonRelationshipDefaults_IdExcludedNameVisible`** — resolved defaults for non-relationship mode: ID absent, name present+visible.
6. **`GenerateTableTmdl_OwnerType_TypeAndYomiDefaultExcluded`** — Owner type with null config, verify type and yomi columns not in output.
7. **`GenerateTableTmdl_OwnerType_TypeIncluded_TypeColumnPresent`** — Owner type with `IncludeTypeField=true`, verify `owneridtype` column in output.
8. **`GenerateTableTmdl_PolymorphicVirtualColumn_OrderingBugFixed`** — `owneridname` appears before `ownerid` in `table.Attributes`; verify the name column uses `ownerid`'s display name (not `"owneridname"`).
9. **`GenerateTableTmdl_CustomerType_VirtualColumnBeforeParent_CorrectDisplayName`** — same ordering test for Customer type.

---

### 2.17 Files to Modify

| File | Changes |
|---|---|
| `DataverseToPowerBI.Core/Models/DataModels.cs` | Add `LookupSubColumnConfig` class; add `LookupSubColumnConfigs` property to `ExportTable` |
| `DataverseToPowerBI.XrmToolBox/PluginControl.cs` | Runtime state field, `SerializedLookupSubColumnConfig`, `PluginSettings` property, save/restore, `PrepareExportData()`, `ResolveDefaults()`, `MigratePolymorphicVirtualColumns()`, `RecalculateLookupDefaults()`, `UpdateAttributesDisplay()`, click handler, `_collapsedLookupGroups` |
| `DataverseToPowerBI.XrmToolBox/Services/SemanticModelBuilder.cs` | Bug fix guard + `IsPolymorphicVirtualSubColumn()`, generation logic changes in all three loops |
| `DataverseToPowerBI.Tests/SemanticModelBuilderTests.cs` | New test cases listed in section 2.16 |

---

### Implementation Notes for Opus

- **Do Part 1 (bug fix) first**, run existing tests, confirm they pass before starting Part 2.
- The `PluginControl.ExportTable` (DataContract at `PluginControl.cs:4258`) is the serialized/persisted form; `DataverseToPowerBI.Core.Models.ExportTable` (`DataModels.cs:1077`) is the runtime form passed to `SemanticModelBuilder`. Both need `LookupSubColumnConfigs` added. `PrepareExportData()` maps from the runtime `_lookupSubColumnConfigs` dictionary to the Core `ExportTable` property.
- `GetEffectiveDisplayName` (`SemanticModelBuilder.cs:866`) is unchanged. The display name for Name sub-rows continues to come from the parent lookup's `attrDisplayInfo`, keyed by the parent lookup's logical name — same as today.
- For the ListView, the new Incl/Hid columns are inserted at indices 5 and 6, shifting the existing Expand column to index 7. Update all hardcoded column index references (e.g. the `subItemIndex != 5` check in `ListViewAttributes_MouseClick`) accordingly.
- The collapse/expand `_collapsedLookupGroups` HashSet lives in `PluginControl` as a regular field — not persisted. On table switch (changing the selected table in the table list), the set is cleared (all groups expand for the new table).
- `owninguser`, `owningteam`, `owningbusinessunit`: these are Lookup type (not Owner type), so they get an ID sub-row but no Name sub-row (their `{name}` columns don't exist at the query layer). Do not render a Name sub-row for these three specific fields. The `isOwningLookup` check already in the generation code handles this correctly.
- The `PolymorphicVirtualSuffixes` and `PolymorphicParentTypes` static sets used in `IsPolymorphicVirtualSubColumn` should be defined once in `SemanticModelBuilder` and reused across all three processing loops. The same logic (checking suffix + parent type) is also used in `UpdateAttributesDisplay` to suppress standalone virtual column rows — consider defining it as a static utility on a shared class, or duplicating it with a comment referencing the source.
