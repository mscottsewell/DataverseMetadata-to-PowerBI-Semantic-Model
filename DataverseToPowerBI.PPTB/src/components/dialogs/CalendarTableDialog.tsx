/**
 * CalendarTableDialog - Configuration for date/calendar table generation
 *
 * Allows users to configure a date dimension table with:
 * - Date range (start/end year)
 * - UTC offset for timezone conversion
 * - Primary date field selection
 */

import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Button,
  Input,
  Label,
  Select,
  Switch,
  makeStyles,
  Text,
  tokens,
} from '@fluentui/react-components';
import { useConfigStore, useMetadataStore, useUIStore } from '../../stores';
import { DEFAULT_START_YEAR, DEFAULT_END_YEAR, DEFAULT_UTC_OFFSET } from '../../types/Constants';
import type { DateTableConfig } from '../../types/DataModels';

const useStyles = makeStyles({
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: '16px',
    marginTop: '12px',
  },
  field: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
  },
  row: {
    display: 'flex',
    gap: '16px',
  },
  halfField: {
    flex: 1,
  },
});

export function CalendarTableDialog() {
  const styles = useStyles();
  const open = useUIStore((s) => s.dialogs.calendarTable);
  const closeDialog = useUIStore((s) => s.closeDialog);

  const existingConfig = useConfigStore((s) => s.dateTableConfig);
  const setDateTableConfig = useConfigStore((s) => s.setDateTableConfig);
  const selectedTables = useConfigStore((s) => s.selectedTables);
  const getTableDisplayName = useMetadataStore((s) => s.getTableDisplayName);
  const metaAttributes = useMetadataStore((s) => s.tableAttributes);

  // Local form state
  const [enabled, setEnabled] = useState(!!existingConfig);
  const [primaryTable, setPrimaryTable] = useState(existingConfig?.primaryDateTable ?? '');
  const [primaryField, setPrimaryField] = useState(existingConfig?.primaryDateField ?? '');
  const [startYear, setStartYear] = useState(existingConfig?.startYear ?? DEFAULT_START_YEAR);
  const [endYear, setEndYear] = useState(existingConfig?.endYear ?? DEFAULT_END_YEAR);
  const [utcOffset, setUtcOffset] = useState(existingConfig?.utcOffsetHours ?? DEFAULT_UTC_OFFSET);

  // Reset form when dialog opens
  useEffect(() => {
    if (open) {
      setEnabled(!!existingConfig);
      setPrimaryTable(existingConfig?.primaryDateTable ?? selectedTables[0] ?? '');
      setPrimaryField(existingConfig?.primaryDateField ?? '');
      setStartYear(existingConfig?.startYear ?? DEFAULT_START_YEAR);
      setEndYear(existingConfig?.endYear ?? DEFAULT_END_YEAR);
      setUtcOffset(existingConfig?.utcOffsetHours ?? DEFAULT_UTC_OFFSET);
    }
  }, [open, existingConfig, selectedTables]);

  // Get datetime attributes for selected table
  const dateAttributes = (metaAttributes[primaryTable] ?? []).filter(
    (a) => a.attributeType === 'DateTime'
  );

  const handleSave = () => {
    if (!enabled) {
      setDateTableConfig(null);
    } else {
      const config: DateTableConfig = {
        primaryDateTable: primaryTable,
        primaryDateField: primaryField,
        timeZoneId: 'UTC',
        utcOffsetHours: utcOffset,
        startYear,
        endYear,
        wrappedFields: [],
      };
      setDateTableConfig(config);
    }
    closeDialog('calendarTable');
  };

  const handleClose = () => closeDialog('calendarTable');

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogSurface>
        <DialogTitle>Calendar Table Configuration</DialogTitle>
        <DialogBody>
          <DialogContent>
            <div className={styles.form}>
              <Switch
                checked={enabled}
                onChange={(_, d) => setEnabled(d.checked)}
                label="Enable Date/Calendar Table"
              />

              {enabled && (
                <>
                  <div className={styles.field}>
                    <Label htmlFor="calPrimaryTable">Primary Date Table</Label>
                    <Select
                      id="calPrimaryTable"
                      value={primaryTable}
                      onChange={(_, d) => { setPrimaryTable(d.value); setPrimaryField(''); }}
                    >
                      <option value="">-- Select Table --</option>
                      {selectedTables.map((t) => (
                        <option key={t} value={t}>{getTableDisplayName(t)}</option>
                      ))}
                    </Select>
                  </div>

                  <div className={styles.field}>
                    <Label htmlFor="calPrimaryField">Primary Date Field</Label>
                    <Select
                      id="calPrimaryField"
                      value={primaryField}
                      onChange={(_, d) => setPrimaryField(d.value)}
                      disabled={!primaryTable}
                    >
                      <option value="">-- Select Field --</option>
                      {dateAttributes.map((a) => (
                        <option key={a.logicalName} value={a.logicalName}>
                          {a.displayName ?? a.logicalName}
                        </option>
                      ))}
                    </Select>
                    {primaryTable && dateAttributes.length === 0 && (
                      <Text size={200} style={{ color: tokens.colorPaletteRedForeground1 }}>
                        No DateTime attributes found. Load attributes first.
                      </Text>
                    )}
                  </div>

                  <div className={styles.row}>
                    <div className={`${styles.field} ${styles.halfField}`}>
                      <Label htmlFor="calStartYear">Start Year</Label>
                      <Input
                        id="calStartYear"
                        type="number"
                        value={String(startYear)}
                        onChange={(_, d) => setStartYear(Number(d.value) || DEFAULT_START_YEAR)}
                      />
                    </div>
                    <div className={`${styles.field} ${styles.halfField}`}>
                      <Label htmlFor="calEndYear">End Year</Label>
                      <Input
                        id="calEndYear"
                        type="number"
                        value={String(endYear)}
                        onChange={(_, d) => setEndYear(Number(d.value) || DEFAULT_END_YEAR)}
                      />
                    </div>
                  </div>

                  <div className={styles.field}>
                    <Label htmlFor="calUtcOffset">UTC Offset (hours)</Label>
                    <Input
                      id="calUtcOffset"
                      type="number"
                      value={String(utcOffset)}
                      onChange={(_, d) => setUtcOffset(Number(d.value) || 0)}
                    />
                    <Text size={200}>Adjusts datetime fields for timezone. Use 0 for UTC.</Text>
                  </div>
                </>
              )}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={handleClose}>Cancel</Button>
            <Button appearance="primary" onClick={handleSave}>
              {enabled ? 'Save' : 'Disable Calendar Table'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
