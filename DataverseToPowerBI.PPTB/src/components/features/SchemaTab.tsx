/**
 * SchemaTab - Star-schema configuration: fact table picker and dimension selector
 *
 * Uses relationship detection from Dataverse lookup attributes to auto-populate
 * the dimension tree. Users can toggle relationships active/inactive.
 */

import { useEffect, useMemo, useRef } from 'react';
import {
  makeStyles,
  Card,
  CardHeader,
  Title2,
  Text,
  Radio,
  RadioGroup,
  Switch,
  Button,
  Badge,
  tokens,
} from '@fluentui/react-components';
import { Organization24Regular, ArrowSync24Regular } from '@fluentui/react-icons';
import { useConfigStore, useMetadataStore } from '../../stores';
import { useRelationshipDetection } from '../../hooks/useRelationshipDetection';
import { useFetchAttributes } from '../../hooks/useDataverse';
import { EmptyState } from '../shared';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: '16px',
  },
  columns: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: '16px',
  },
  card: {
    padding: '20px',
  },
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
    maxHeight: 'calc(100vh - 320px)',
    overflow: 'auto',
    marginTop: '12px',
  },
  relGroup: {
    marginLeft: '24px',
    padding: '8px 0',
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
  },
  relItem: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '4px 8px',
    borderRadius: '4px',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  dimHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: '8px',
    marginBottom: '8px',
  },
  solutionFilter: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    marginTop: '8px',
  },
});

export function SchemaTab() {
  const styles = useStyles();
  const selectedTables = useConfigStore((s) => s.selectedTables);
  const factTable = useConfigStore((s) => s.factTable);
  const setFactTable = useConfigStore((s) => s.setFactTable);
  const relationships = useConfigStore((s) => s.relationships);
  const toggleRelationshipActive = useConfigStore((s) => s.toggleRelationshipActive);
  const getTableDisplayName = useMetadataStore((s) => s.getTableDisplayName);
  const tableAttributes = useMetadataStore((s) => s.tableAttributes);

  const { detectedRelationships, autoDetect } = useRelationshipDetection();
  const fetchAttributes = useFetchAttributes();
  const autoDetectedForRef = useRef<string | null>(null);

  // Auto-fetch attributes for fact table when it changes
  useEffect(() => {
    if (factTable && !tableAttributes[factTable]) {
      fetchAttributes(factTable);
    }
  }, [factTable, tableAttributes, fetchAttributes]);

  // Auto-detect relationships when fact table attributes are loaded (once per fact table)
  useEffect(() => {
    if (factTable && tableAttributes[factTable] && autoDetectedForRef.current !== factTable) {
      autoDetectedForRef.current = factTable;
      autoDetect();
    }
  }, [factTable, tableAttributes, autoDetect]);

  // Group current relationships by target table
  const groupedRelationships = useMemo(() => {
    const groups: Record<string, typeof relationships> = {};
    for (const rel of relationships) {
      const key = rel.targetTable;
      if (!groups[key]) groups[key] = [];
      groups[key].push(rel);
    }
    return groups;
  }, [relationships]);

  if (selectedTables.length === 0) {
    return (
      <EmptyState
        icon={<Organization24Regular />}
        title="No Tables Selected"
        description="Select tables in the Tables tab first."
      />
    );
  }

  return (
    <div className={styles.container}>
      <Title2>Star Schema Configuration</Title2>

      <div className={styles.columns}>
        {/* Fact Table Picker */}
        <Card className={styles.card}>
          <CardHeader header={<Text weight="semibold" size={400}>Fact Table</Text>} />
          <Text size={200}>Select the central fact table for your star schema.</Text>
          <RadioGroup
            value={factTable ?? ''}
            onChange={(_, data) => setFactTable(data.value || null)}
          >
            <div className={styles.list}>
              {selectedTables.map((table) => (
                <Radio
                  key={table}
                  value={table}
                  label={getTableDisplayName(table)}
                />
              ))}
            </div>
          </RadioGroup>
        </Card>

        {/* Dimension Tables */}
        <Card className={styles.card}>
          <div className={styles.dimHeader}>
            <CardHeader header={<Text weight="semibold" size={400}>Dimensions & Relationships</Text>} />
            {factTable && (
              <Button
                size="small"
                appearance="secondary"
                icon={<ArrowSync24Regular />}
                onClick={autoDetect}
              >
                Detect
              </Button>
            )}
          </div>
          <Text size={200}>
            {factTable
              ? `${detectedRelationships.length} lookup relationships found from ${getTableDisplayName(factTable)}`
              : 'Select a fact table to detect relationships'}
          </Text>

          <div className={styles.list}>
            {Object.entries(groupedRelationships).map(([targetTable, rels]) => {
              const isInSolution = selectedTables.includes(targetTable);
              return (
                <div key={targetTable}>
                  <Text weight="semibold" size={300}>
                    {getTableDisplayName(targetTable)}
                    <Badge appearance="tint" size="tiny" style={{ marginLeft: 8 }}>
                      {rels.length}
                    </Badge>
                    {!isInSolution && (
                      <Badge appearance="tint" color="warning" size="tiny" style={{ marginLeft: 4 }}>
                        external
                      </Badge>
                    )}
                  </Text>
                  <div className={styles.relGroup}>
                    {rels.map((rel) => (
                      <div key={`${rel.sourceAttribute}-${rel.targetTable}`} className={styles.relItem}>
                        <Switch
                          checked={rel.isActive}
                          onChange={() =>
                            toggleRelationshipActive(rel.sourceTable, rel.sourceAttribute, rel.targetTable)
                          }
                          label={`${rel.sourceAttribute} → ${rel.targetTable}`}
                        />
                        {!rel.isActive && (
                          <Badge appearance="tint" color="subtle" size="tiny">inactive</Badge>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              );
            })}

            {Object.keys(groupedRelationships).length === 0 && factTable && (
              <EmptyState
                title="No Relationships Found"
                description="Click Detect to scan for lookup relationships, or no lookup attributes found."
              />
            )}
          </div>
        </Card>
      </div>
    </div>
  );
}
