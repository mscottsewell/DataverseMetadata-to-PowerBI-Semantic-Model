/**
 * AppLayout - Main application shell with header, tab navigation, and content area
 */

import { makeStyles, tokens } from '@fluentui/react-components';
import { Header } from './Header';
import { TabNavigation } from './TabNavigation';
import { useUIStore } from '../../stores';
import { ErrorBoundary, LoadingOverlay } from '../shared';
import { SetupTab } from '../features/SetupTab';
import { TablesTab } from '../features/TablesTab';
import { SchemaTab } from '../features/SchemaTab';
import { AttributesTab } from '../features/AttributesTab';
import { BuildTab } from '../features/BuildTab';
import { FormPickerDialog, ViewPickerDialog, CalendarTableDialog, ChangePreviewDialog, ConfigManagerDialog } from '../dialogs';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
    overflow: 'hidden',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  content: {
    flex: 1,
    overflow: 'auto',
    position: 'relative',
    padding: '16px 24px',
  },
});

export function AppLayout() {
  const styles = useStyles();
  const activeTab = useUIStore((s) => s.activeTab);
  const globalLoading = useUIStore((s) => s.globalLoading);
  const loadingMessage = useUIStore((s) => s.loadingMessage);

  const tabContent = {
    setup: <SetupTab />,
    tables: <TablesTab />,
    schema: <SchemaTab />,
    attributes: <AttributesTab />,
    build: <BuildTab />,
  }[activeTab];

  return (
    <div className={styles.root}>
      <Header />
      <TabNavigation />
      <div className={styles.content}>
        <ErrorBoundary>
          {tabContent}
        </ErrorBoundary>
        {globalLoading && <LoadingOverlay message={loadingMessage ?? undefined} />}
      </div>
      <FormPickerDialog />
      <ViewPickerDialog />
      <CalendarTableDialog />
      <ChangePreviewDialog />
      <ConfigManagerDialog />
    </div>
  );
}
