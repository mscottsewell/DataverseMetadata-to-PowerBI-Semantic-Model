/**
 * BuildTab - TMDL preview, generation, and change detection
 *
 * Wires the useBuild hook to generate TMDL preview and save output files.
 */

import { useState, useCallback } from 'react';
import {
  makeStyles,
  Card,
  CardHeader,
  Title2,
  Text,
  Button,
  Badge,
  tokens,
} from '@fluentui/react-components';
import {
  BuildingFactory24Regular,
  Eye24Regular,
  ArrowDownload24Regular,
  ArrowSync24Regular,
  Copy24Regular,
} from '@fluentui/react-icons';
import { useConfigStore, useUIStore } from '../../stores';
import { useBuild } from '../../hooks';
import { EmptyState, LoadingOverlay } from '../shared';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: '16px',
  },
  card: {
    padding: '20px',
  },
  actions: {
    display: 'flex',
    gap: '12px',
    flexWrap: 'wrap',
  },
  previewArea: {
    marginTop: '16px',
  },
  fileTree: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
    marginBottom: '12px',
    padding: '12px',
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: '4px',
    fontFamily: 'monospace',
    fontSize: '13px',
    maxHeight: '200px',
    overflow: 'auto',
  },
  fileItem: {
    padding: '2px 8px',
    cursor: 'pointer',
    borderRadius: '4px',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  fileItemSelected: {
    padding: '2px 8px',
    cursor: 'pointer',
    borderRadius: '4px',
    backgroundColor: tokens.colorBrandBackground2,
  },
  codeBlock: {
    fontFamily: 'Consolas, "Courier New", monospace',
    fontSize: '12px',
    lineHeight: '1.5',
    whiteSpace: 'pre',
    overflow: 'auto',
    maxHeight: '400px',
    padding: '16px',
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: '4px',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  summary: {
    display: 'flex',
    gap: '12px',
    flexWrap: 'wrap',
    marginBottom: '12px',
  },
  codeHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: '8px',
  },
});

export function BuildTab() {
  const styles = useStyles();
  const selectedTables = useConfigStore((s) => s.selectedTables);
  const projectName = useConfigStore((s) => s.projectName);
  const connectionMode = useConfigStore((s) => s.connectionMode);
  const { generatePreview, generateAndSave } = useBuild();
  const addToast = useUIStore((s) => s.addToast);

  const [previewFiles, setPreviewFiles] = useState<Map<string, string>>(new Map());
  const [selectedFile, setSelectedFile] = useState<string | null>(null);
  const [building, setBuilding] = useState(false);
  const [buildResult, setBuildResult] = useState<'success' | 'error' | null>(null);
  const [, setStatusMessages] = useState<string[]>([]);

  const openDialog = useUIStore((s) => s.openDialog);
  const openChangePreview = useCallback(() => openDialog('changePreview'), [openDialog]);

  const handlePreview = useCallback(() => {
    setBuilding(true);
    setBuildResult(null);
    try {
      const result = generatePreview();
      if (result && result.files.size > 0) {
        setPreviewFiles(result.files);
        setStatusMessages(result.statusMessages);
        setBuildResult('success');
        // Auto-select first file
        const firstKey = Array.from(result.files.keys())[0];
        if (firstKey) setSelectedFile(firstKey);
        addToast({ type: 'success', title: `Preview generated: ${result.files.size} files` });
      } else {
        setBuildResult('error');
        addToast({ type: 'error', title: 'No files generated' });
      }
    } catch {
      setBuildResult('error');
    } finally {
      setBuilding(false);
    }
  }, [generatePreview, addToast]);

  const handleSave = useCallback(async () => {
    setBuilding(true);
    try {
      await generateAndSave();
      setBuildResult('success');
    } catch {
      setBuildResult('error');
    } finally {
      setBuilding(false);
    }
  }, [generateAndSave]);

  if (selectedTables.length === 0) {
    return (
      <EmptyState
        icon={<BuildingFactory24Regular />}
        title="No Tables Selected"
        description="Select tables and configure your schema before building."
      />
    );
  }

  const fileNames = Array.from(previewFiles.keys());

  return (
    <div className={styles.container}>
      <Title2>Build & Preview</Title2>

      <Card className={styles.card}>
        <CardHeader header={<Text weight="semibold" size={400}>Generation Summary</Text>} />
        <div className={styles.summary}>
          <Badge appearance="filled" color="brand">{selectedTables.length} tables</Badge>
          <Badge appearance="tint">{projectName || 'Unnamed Project'}</Badge>
          <Badge appearance="tint">{connectionMode}</Badge>
          {buildResult === 'success' && <Badge appearance="filled" color="success">Build Successful</Badge>}
          {buildResult === 'error' && <Badge appearance="filled" color="danger">Build Failed</Badge>}
          {previewFiles.size > 0 && <Badge appearance="tint" color="informative">{previewFiles.size} files</Badge>}
        </div>

        <div className={styles.actions}>
          <Button
            appearance="primary"
            icon={<Eye24Regular />}
            disabled={building}
            onClick={handlePreview}
          >
            Preview TMDL
          </Button>
          <Button
            appearance="primary"
            icon={<ArrowDownload24Regular />}
            disabled={building || selectedTables.length === 0}
            onClick={handleSave}
          >
            Generate & Save
          </Button>
          <Button
            appearance="secondary"
            icon={<ArrowSync24Regular />}
            disabled={building}
            onClick={openChangePreview}
          >
            Preview Changes
          </Button>
        </div>
      </Card>

      {fileNames.length > 0 && (
        <Card className={styles.card}>
          <CardHeader header={<Text weight="semibold" size={400}>TMDL Preview</Text>} />
          <div className={styles.previewArea}>
            <div className={styles.fileTree}>
              {fileNames.map((name) => (
                <div
                  key={name}
                  className={selectedFile === name ? styles.fileItemSelected : styles.fileItem}
                  onClick={() => setSelectedFile(name)}
                >
                  📄 {name.split('/').pop()}
                </div>
              ))}
            </div>
            {selectedFile && previewFiles.get(selectedFile) && (
              <>
                <div className={styles.codeHeader}>
                  <Text weight="semibold" size={300}>{selectedFile}</Text>
                  <Button
                    size="small"
                    appearance="subtle"
                    icon={<Copy24Regular />}
                    onClick={() => navigator.clipboard.writeText(previewFiles.get(selectedFile)!)}
                  >
                    Copy
                  </Button>
                </div>
                <div className={styles.codeBlock}>
                  {previewFiles.get(selectedFile)}
                </div>
              </>
            )}
          </div>
        </Card>
      )}

      {building && <LoadingOverlay message="Generating semantic model..." />}
    </div>
  );
}
