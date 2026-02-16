/**
 * ConfigManagerDialog - Save, load, delete, import/export configurations
 *
 * Manages named configurations stored via SettingsAdapter. Allows users to
 * create new configs, switch between saved configs, and import/export.
 */

import { useState, useEffect, useCallback } from 'react';
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
  Text,
  Badge,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import {
  Save24Regular,
  Delete24Regular,
  Add24Regular,
} from '@fluentui/react-icons';
import { useConfigStore, useUIStore } from '../../stores';
import { SettingsAdapter } from '../../adapters/SettingsAdapter';
import type { ConfigurationEntry, ConfigurationsFile } from '../../types/DataModels';

const useStyles = makeStyles({
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
    maxHeight: '300px',
    overflow: 'auto',
    marginTop: '8px',
  },
  configItem: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '8px 12px',
    borderRadius: '4px',
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  configItemActive: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '8px 12px',
    borderRadius: '4px',
    cursor: 'pointer',
    backgroundColor: tokens.colorBrandBackground2,
  },
  newConfigRow: {
    display: 'flex',
    gap: '8px',
    alignItems: 'end',
    marginTop: '16px',
  },
  actions: {
    display: 'flex',
    gap: '8px',
    marginTop: '12px',
  },
  flex1: {
    flex: 1,
  },
});

export function ConfigManagerDialog() {
  const styles = useStyles();
  const open = useUIStore((s) => s.dialogs.configManager);
  const closeDialog = useUIStore((s) => s.closeDialog);
  const addToast = useUIStore((s) => s.addToast);

  const configName = useConfigStore((s) => s.configName);
  const loadFromSettings = useConfigStore((s) => s.loadFromSettings);
  const toSettings = useConfigStore((s) => s.toSettings);
  const markClean = useConfigStore((s) => s.markClean);
  const setConfigName = useConfigStore((s) => s.setConfigName);
  const reset = useConfigStore((s) => s.reset);

  const [configs, setConfigs] = useState<ConfigurationEntry[]>([]);
  const [newName, setNewName] = useState('');

  const loadConfigs = useCallback(async () => {
    try {
      const adapter = new SettingsAdapter();
      const data = await adapter.getConfigurationsAsync();
      if (data) {
        setConfigs(data.configurations ?? []);
      }
    } catch {
      setConfigs([]);
    }
  }, []);

  useEffect(() => {
    if (open) {
      loadConfigs();
      setNewName('');
    }
  }, [open, loadConfigs]);

  const handleSave = async () => {
    try {
      const adapter = new SettingsAdapter();
      const settings = toSettings();
      const entry: ConfigurationEntry = {
        name: configName,
        lastUsed: new Date().toISOString(),
        settings,
      };
      // Update or add
      const existing = configs.findIndex((c) => c.name === configName);
      const updated = [...configs];
      if (existing >= 0) {
        updated[existing] = entry;
      } else {
        updated.push(entry);
      }
      const file: ConfigurationsFile = {
        configurations: updated,
        lastUsedConfigurationName: configName,
      };
      await adapter.saveConfigurationsAsync(file);
      markClean();
      setConfigs(updated);
      addToast({ type: 'success', title: 'Configuration saved' });
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to save', message: String(err) });
    }
  };

  const handleLoad = (entry: ConfigurationEntry) => {
    loadFromSettings(entry.name, entry.settings);
    addToast({ type: 'info', title: `Loaded: ${entry.name}` });
    closeDialog('configManager');
  };

  const handleDelete = async (name: string) => {
    try {
      const adapter = new SettingsAdapter();
      await adapter.deleteConfigurationAsync(name);
      const remaining = configs.filter((c) => c.name !== name);
      setConfigs(remaining);
      addToast({ type: 'info', title: `Deleted: ${name}` });
    } catch (err) {
      addToast({ type: 'error', title: 'Failed to delete', message: String(err) });
    }
  };

  const handleNew = () => {
    if (!newName.trim()) return;
    reset();
    setConfigName(newName.trim());
    addToast({ type: 'info', title: `New configuration: ${newName.trim()}` });
    setNewName('');
    closeDialog('configManager');
  };

  const handleClose = () => closeDialog('configManager');

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogSurface style={{ maxWidth: '500px' }}>
        <DialogTitle>Configuration Manager</DialogTitle>
        <DialogBody>
          <DialogContent>
            <Text size={300} weight="semibold">Saved Configurations</Text>
            <div className={styles.list}>
              {configs.length === 0 && (
                <Text size={200} style={{ padding: 12, color: tokens.colorNeutralForeground3 }}>
                  No saved configurations.
                </Text>
              )}
              {configs.map((entry) => (
                <div
                  key={entry.name}
                  className={entry.name === configName ? styles.configItemActive : styles.configItem}
                  onClick={() => handleLoad(entry)}
                >
                  <div>
                    <Text size={300} weight="semibold">{entry.name}</Text>
                    <br />
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {entry.settings.selectedTables?.length ?? 0} tables • {new Date(entry.lastUsed).toLocaleDateString()}
                    </Text>
                  </div>
                  <div style={{ display: 'flex', gap: 4 }}>
                    {entry.name === configName && (
                      <Badge appearance="filled" color="brand" size="small">active</Badge>
                    )}
                    <Button
                      size="small"
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={(e) => { e.stopPropagation(); handleDelete(entry.name); }}
                    />
                  </div>
                </div>
              ))}
            </div>

            <div className={styles.newConfigRow}>
              <div className={styles.flex1}>
                <Label htmlFor="newConfigName">New Configuration</Label>
                <Input
                  id="newConfigName"
                  value={newName}
                  onChange={(_, d) => setNewName(d.value)}
                  placeholder="Configuration name"
                />
              </div>
              <Button appearance="secondary" icon={<Add24Regular />} onClick={handleNew} disabled={!newName.trim()}>
                Create
              </Button>
            </div>

            <div className={styles.actions}>
              <Button appearance="primary" icon={<Save24Regular />} onClick={handleSave}>
                Save Current
              </Button>
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={handleClose}>Close</Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
