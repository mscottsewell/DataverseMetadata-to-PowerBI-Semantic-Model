/**
 * SetupTab - Configuration setup: project name, connection mode, solution selector, output folder
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
} from '@fluentui/react-components';
import { FolderOpen24Regular, CalendarLtr24Regular } from '@fluentui/react-icons';
import { useConfigStore, useConnectionStore, useMetadataStore, useUIStore } from '../../stores';
import { ConnectionMode, StorageMode } from '../../types/Constants';
import { useFetchSolutions, useFetchTables } from '../../hooks';

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
  } = useConfigStore();
  const solutions = useMetadataStore((s) => s.solutions);
  const loadingSolutions = useMetadataStore((s) => s.loading.solutions);
  const fetchSolutions = useFetchSolutions();
  const fetchTables = useFetchTables();
  const dateTableConfig = useConfigStore((s) => s.dateTableConfig);
  const openDialog = useUIStore((s) => s.openDialog);

  // Auto-fetch solutions when connected
  useEffect(() => {
    if (status === 'connected' && solutions.length === 0) {
      fetchSolutions();
    }
  }, [status, solutions.length, fetchSolutions]);

  // Fetch tables when solution changes
  const handleSolutionChange = (value: string | null) => {
    setSelectedSolution(value);
    if (value) fetchTables(value);
  };

  return (
    <div className={styles.container}>
      <Title2>Project Setup</Title2>

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
              disabled={loadingSolutions}
            >
              <option value="">-- Select Solution --</option>
              {solutions.map((s) => (
                <option key={s.solutionId} value={s.uniqueName}>
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
                onChange={(_, d) => setOutputFolder(d.value || null)}
                placeholder="Select output folder..."
                readOnly
              />
            </div>
            <Button icon={<FolderOpen24Regular />} appearance="secondary">
              Browse
            </Button>
          </div>
        </div>
      </Card>

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

      <Card className={styles.card}>
        <CardHeader header={<Text weight="semibold" size={400}>Calendar Table</Text>} />
        <div className={styles.fieldGroup}>
          <Text size={300}>
            {dateTableConfig
              ? `Enabled: ${dateTableConfig.startYear}–${dateTableConfig.endYear}, UTC${dateTableConfig.utcOffsetHours >= 0 ? '+' : ''}${dateTableConfig.utcOffsetHours}`
              : 'No calendar table configured.'}
          </Text>
          <Button
            appearance="secondary"
            icon={<CalendarLtr24Regular />}
            onClick={() => openDialog('calendarTable')}
          >
            {dateTableConfig ? 'Edit Calendar Table' : 'Add Calendar Table'}
          </Button>
        </div>
      </Card>
    </div>
  );
}
