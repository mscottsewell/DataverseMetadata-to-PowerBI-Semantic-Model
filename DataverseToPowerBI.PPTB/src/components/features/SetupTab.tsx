/**
 * SetupTab - Configuration setup: project name, solution selector, connection mode,
 * storage mode, template path, and output folder.
 */

import { useEffect } from 'react';
import {
  makeStyles,
  Card,
  CardHeader,
  Title2,
  Text,
  Input,
  Label,
  Select,
  Button,
  Divider,
  Switch,
} from '@fluentui/react-components';
import { FolderOpen24Regular } from '@fluentui/react-icons';
import { useConfigStore, useConnectionStore, useMetadataStore } from '../../stores';
import { ConnectionMode, StorageMode } from '../../types/Constants';
import { useFetchSolutions, useFetchTables } from '../../hooks';
import { FileSystemAdapter } from '../../adapters/FileSystemAdapter';
import { SettingsAdapter } from '../../adapters/SettingsAdapter';

const BUILTIN_TEMPLATE_LABEL = 'Built-in (Default)';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: '16px',
    maxWidth: '800px',
  },
  card: {
    padding: '20px',
  },
  fieldGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
    marginTop: '12px',
  },
  field: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
  },
  row: {
    display: 'flex',
    gap: '12px',
    alignItems: 'end',
  },
  flex1: {
    flex: 1,
  },
});

export function SetupTab() {
  const styles = useStyles();
  const status = useConnectionStore((s) => s.status);
  const {
    projectName, setProjectName,
    connectionMode, setConnectionMode,
    storageMode, setStorageMode,
    selectedSolution, setSelectedSolution,
    outputFolder, setOutputFolder,
    fabricLinkEndpoint, setFabricLinkEndpoint,
    fabricLinkDatabase, setFabricLinkDatabase,
    useDisplayNameAliasesInSql, setUseDisplayNameAliases,
  } = useConfigStore();
  const templatePath = useConfigStore((s) => s.templatePath);
  const setTemplatePath = useConfigStore((s) => s.setTemplatePath);
  const solutions = useMetadataStore((s) => s.solutions);
  const loadingSolutions = useMetadataStore((s) => s.loading.solutions);
  const fetchSolutions = useFetchSolutions();
  const fetchTables = useFetchTables();

  // Auto-fetch solutions when connected
  useEffect(() => {
    if (status === 'connected' && solutions.length === 0) {
      fetchSolutions();
    }
  }, [status, solutions.length, fetchSolutions]);

  // Initialize default paths on first load
  useEffect(() => {
    const initDefaults = async () => {
      try {
        const settings = new SettingsAdapter();

        // Set default output folder if not already set
        if (!outputFolder) {
          const defaultFolder = await settings.resolveDefaultOutputFolder();
          if (defaultFolder) setOutputFolder(defaultFolder);
        }

        // Set default template path if not already set
        if (!templatePath) {
          const prefs = await settings.getPreferencesAsync();
          setTemplatePath(prefs.lastTemplatePath ?? BUILTIN_TEMPLATE_LABEL);
        }
      } catch {
        // Settings API unavailable (e.g. dev mode) - set template default only
        if (!templatePath) setTemplatePath(BUILTIN_TEMPLATE_LABEL);
      }
    };
    initDefaults();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Fetch tables when solution changes
  const handleSolutionChange = (solutionId: string | null) => {
    setSelectedSolution(solutionId);
    if (solutionId) fetchTables(solutionId);
  };

  const handleBrowseOutput = async () => {
    try {
      const fs = new FileSystemAdapter();
      const folder = await fs.selectFolder('Select Output Folder');
      if (folder) {
        setOutputFolder(folder);
        // Persist as default for future sessions
        try {
          const settings = new SettingsAdapter();
          await settings.updatePreferenceAsync('lastOutputFolder', folder);
        } catch { /* settings unavailable */ }
      }
    } catch { /* user cancelled */ }
  };

  const handleBrowseTemplate = async () => {
    try {
      const fs = new FileSystemAdapter();
      const folder = await fs.selectFolder('Select PBIP Template Folder');
      if (folder) {
        setTemplatePath(folder);
        // Persist as default for future sessions
        try {
          const settings = new SettingsAdapter();
          await settings.updatePreferenceAsync('lastTemplatePath', folder);
        } catch { /* settings unavailable */ }
      }
    } catch { /* user cancelled */ }
  };

  return (
    <div className={styles.container}>
      <Title2>Project Setup</Title2>

      {/* General Settings */}
      <Card className={styles.card}>
        <CardHeader header={<Text weight="semibold" size={400}>General Settings</Text>} />
        <div className={styles.fieldGroup}>
          <div className={styles.field}>
            <Label htmlFor="projectName" required>Project Name</Label>
            <Input
              id="projectName"
              value={projectName}
              onChange={(_, d) => setProjectName(d.value)}
              placeholder="My Semantic Model"
            />
          </div>

          <div className={styles.field}>
            <Label htmlFor="solution">Solution</Label>
            <Select
              id="solution"
              value={selectedSolution ?? ''}
              onChange={(_, d) => handleSolutionChange(d.value || null)}
              disabled={loadingSolutions || status !== 'connected'}
            >
              <option value="">
                {loadingSolutions ? 'Loading solutions...' : status !== 'connected' ? 'Connect to load solutions' : '-- Select Solution --'}
              </option>
              {solutions.map((s) => (
                <option key={s.solutionId} value={s.solutionId}>
                  {s.friendlyName} ({s.uniqueName})
                </option>
              ))}
            </Select>
          </div>

          <div className={styles.row}>
            <div className={`${styles.field} ${styles.flex1}`}>
              <Label htmlFor="outputFolder">Output Folder</Label>
              <Input
                id="outputFolder"
                value={outputFolder ?? ''}
                placeholder="Select output folder..."
                readOnly
              />
            </div>
            <Button icon={<FolderOpen24Regular />} appearance="secondary" onClick={handleBrowseOutput}>
              Browse
            </Button>
          </div>

          <div className={styles.row}>
            <div className={`${styles.field} ${styles.flex1}`}>
              <Label htmlFor="templatePath">PBIP Template</Label>
              <Input
                id="templatePath"
                value={templatePath ?? ''}
                placeholder="Select PBIP template folder..."
                readOnly
              />
              <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
                Uses built-in template by default. Browse to override with your own PBIP project template.
              </Text>
            </div>
            <Button icon={<FolderOpen24Regular />} appearance="secondary" onClick={handleBrowseTemplate}>
              Browse
            </Button>
          </div>
        </div>
      </Card>

      {/* Connection Settings */}
      <Card className={styles.card}>
        <CardHeader header={<Text weight="semibold" size={400}>Connection Settings</Text>} />
        <div className={styles.fieldGroup}>
          <div className={styles.field}>
            <Label htmlFor="connectionMode">Connection Mode</Label>
            <Select
              id="connectionMode"
              value={connectionMode}
              onChange={(_, d) => setConnectionMode(d.value as ConnectionMode)}
            >
              <option value={ConnectionMode.DataverseTDS}>Dataverse TDS Endpoint</option>
              <option value={ConnectionMode.FabricLink}>Fabric Link</option>
            </Select>
          </div>

          {connectionMode === ConnectionMode.FabricLink && (
            <>
              <Divider />
              <Text size={300} italic>Fabric Link Settings</Text>
              <div className={styles.field}>
                <Label htmlFor="fabricEndpoint">SQL Endpoint</Label>
                <Input
                  id="fabricEndpoint"
                  value={fabricLinkEndpoint ?? ''}
                  onChange={(_, d) => setFabricLinkEndpoint(d.value || null)}
                  placeholder="your-endpoint.datawarehouse.fabric.microsoft.com"
                />
              </div>
              <div className={styles.field}>
                <Label htmlFor="fabricDb">Database Name</Label>
                <Input
                  id="fabricDb"
                  value={fabricLinkDatabase ?? ''}
                  onChange={(_, d) => setFabricLinkDatabase(d.value || null)}
                  placeholder="your_database"
                />
              </div>
            </>
          )}
        </div>
      </Card>

      {/* Storage Mode */}
      <Card className={styles.card}>
        <CardHeader header={<Text weight="semibold" size={400}>Storage Mode</Text>} />
        <div className={styles.fieldGroup}>
          <div className={styles.field}>
            <Label htmlFor="storageMode">Default Storage Mode</Label>
            <Select
              id="storageMode"
              value={storageMode}
              onChange={(_, d) => setStorageMode(d.value as StorageMode)}
            >
              <option value={StorageMode.DirectQuery}>DirectQuery</option>
              <option value={StorageMode.Import}>Import</option>
              <option value={StorageMode.Dual}>Dual</option>
            </Select>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
              Per-table overrides can be configured on the Tables tab.
            </Text>
          </div>

          <Switch
            label="Use display names as SQL column aliases"
            checked={useDisplayNameAliasesInSql}
            onChange={(_, d) => setUseDisplayNameAliases(d.checked)}
          />
        </div>
      </Card>
    </div>
  );
}
