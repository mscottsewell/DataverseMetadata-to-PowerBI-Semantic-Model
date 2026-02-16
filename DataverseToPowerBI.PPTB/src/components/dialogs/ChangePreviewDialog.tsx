/**
 * ChangePreviewDialog - Displays change analysis before incremental builds
 *
 * Shows added, modified, and removed items grouped by category (tables,
 * columns, relationships) with impact level color coding.
 */

import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Button,
  Text,
  Badge,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import {
  Add24Regular,
  Edit24Regular,
  Warning24Regular,
  Checkmark24Regular,
} from '@fluentui/react-icons';
import { useUIStore } from '../../stores';
import { EmptyState } from '../shared';
import { ChangeType, ImpactLevel, type SemanticModelChange } from '../../core/tmdl/ChangeDetector';

const useStyles = makeStyles({
  changeList: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
    maxHeight: '500px',
    overflow: 'auto',
    marginTop: '12px',
  },
  changeItem: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '8px 12px',
    borderRadius: '4px',
    borderLeft: '3px solid transparent',
  },
  safe: {
    borderLeftColor: tokens.colorPaletteGreenBorder1,
    backgroundColor: tokens.colorPaletteGreenBackground1,
  },
  additive: {
    borderLeftColor: tokens.colorPaletteBlueBorderActive,
    backgroundColor: tokens.colorPaletteBlueBorderActive,
  },
  moderate: {
    borderLeftColor: tokens.colorPaletteYellowBorder1,
    backgroundColor: tokens.colorPaletteYellowBackground1,
  },
  destructive: {
    borderLeftColor: tokens.colorPaletteRedBorder1,
    backgroundColor: tokens.colorPaletteRedBackground1,
  },
  summary: {
    display: 'flex',
    gap: '12px',
    flexWrap: 'wrap',
    padding: '12px',
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: '4px',
  },
  details: {
    flex: 1,
    minWidth: 0,
  },
});

interface ChangePreviewDialogProps {
  changes?: SemanticModelChange[];
}

const impactStyle: Record<string, string> = {
  [ImpactLevel.Safe]: 'safe',
  [ImpactLevel.Additive]: 'additive',
  [ImpactLevel.Moderate]: 'moderate',
  [ImpactLevel.Destructive]: 'destructive',
};

const impactColor: Record<string, 'success' | 'informative' | 'warning' | 'danger'> = {
  [ImpactLevel.Safe]: 'success',
  [ImpactLevel.Additive]: 'informative',
  [ImpactLevel.Moderate]: 'warning',
  [ImpactLevel.Destructive]: 'danger',
};

const changeIcon: Record<string, JSX.Element> = {
  [ChangeType.New]: <Add24Regular />,
  [ChangeType.Update]: <Edit24Regular />,
  [ChangeType.Warning]: <Warning24Regular />,
};

export function ChangePreviewDialog({ changes = [] }: ChangePreviewDialogProps) {
  const open = useUIStore((s) => s.dialogs.changePreview);
  const closeDialog = useUIStore((s) => s.closeDialog);
  const styles = useStyles();

  const handleClose = () => closeDialog('changePreview');

  const added = changes.filter((c) => c.changeType === ChangeType.New);
  const modified = changes.filter((c) => c.changeType === ChangeType.Update);
  const warnings = changes.filter((c) => c.changeType === ChangeType.Warning || c.changeType === ChangeType.Error);
  const hasDestructive = changes.some((c) => c.impact === ImpactLevel.Destructive);

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogSurface style={{ maxWidth: '700px' }}>
        <DialogTitle>
          {hasDestructive && <Warning24Regular style={{ color: tokens.colorPaletteRedForeground1, marginRight: 8 }} />}
          Change Preview
        </DialogTitle>
        <DialogBody>
          <DialogContent>
            {changes.length === 0 ? (
              <EmptyState
                icon={<Checkmark24Regular />}
                title="No Changes Detected"
                description="The semantic model is up to date with your current configuration."
              />
            ) : (
              <>
                <div className={styles.summary}>
                  {added.length > 0 && <Badge appearance="filled" color="success">+{added.length} new</Badge>}
                  {modified.length > 0 && <Badge appearance="filled" color="warning">{modified.length} updated</Badge>}
                  {warnings.length > 0 && <Badge appearance="filled" color="danger">{warnings.length} warnings</Badge>}
                </div>

                <div className={styles.changeList}>
                  {changes.map((change, i) => (
                    <div
                      key={i}
                      className={`${styles.changeItem} ${styles[impactStyle[change.impact] as keyof typeof styles] || ''}`}
                    >
                      {changeIcon[change.changeType] ?? <Edit24Regular />}
                      <div className={styles.details}>
                        <Text weight="semibold" size={300}>
                          {change.objectType}: {change.objectName}
                        </Text>
                        {change.description && (
                          <Text size={200} style={{ display: 'block', marginTop: 2 }}>
                            {change.description}
                          </Text>
                        )}
                      </div>
                      <Badge appearance="tint" color={impactColor[change.impact]} size="small">
                        {change.impact}
                      </Badge>
                    </div>
                  ))}
                </div>
              </>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={handleClose}>Close</Button>
            {changes.length > 0 && !hasDestructive && (
              <Button appearance="primary" onClick={handleClose}>Apply Changes</Button>
            )}
            {hasDestructive && (
              <Button appearance="primary" style={{ backgroundColor: tokens.colorPaletteRedBackground3 }} onClick={handleClose}>
                Apply Anyway
              </Button>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
