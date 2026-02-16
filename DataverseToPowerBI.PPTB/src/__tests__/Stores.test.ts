/**
 * Store tests - Zustand stores for configuration, connection, and UI state
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { useConfigStore } from '../stores/useConfigStore';
import { useConnectionStore } from '../stores/useConnectionStore';
import { useUIStore } from '../stores/useUIStore';
import { useMetadataStore } from '../stores/useMetadataStore';
import { TableRole } from '../types/DataModels';
import type { TableInfo, AttributeMetadata } from '../types/DataModels';

// #region Config Store

describe('useConfigStore', () => {
  beforeEach(() => {
    useConfigStore.getState().reset();
  });

  it('initializes with defaults', () => {
    const state = useConfigStore.getState();
    expect(state.configName).toBe('New Configuration');
    expect(state.isDirty).toBe(false);
    expect(state.selectedTables).toEqual([]);
    expect(state.factTable).toBeNull();
    expect(state.relationships).toEqual([]);
  });

  it('tracks dirty state on changes', () => {
    const store = useConfigStore.getState();
    expect(store.isDirty).toBe(false);

    useConfigStore.getState().setConfigName('Test');
    expect(useConfigStore.getState().isDirty).toBe(true);
    expect(useConfigStore.getState().configName).toBe('Test');
  });

  it('markClean clears dirty flag', () => {
    useConfigStore.getState().setConfigName('Test');
    expect(useConfigStore.getState().isDirty).toBe(true);

    useConfigStore.getState().markClean();
    expect(useConfigStore.getState().isDirty).toBe(false);
  });

  describe('table selection', () => {
    it('addTable adds a table', () => {
      useConfigStore.getState().addTable('account');
      expect(useConfigStore.getState().selectedTables).toEqual(['account']);
      expect(useConfigStore.getState().isDirty).toBe(true);
    });

    it('addTable is idempotent', () => {
      useConfigStore.getState().addTable('account');
      useConfigStore.getState().addTable('account');
      expect(useConfigStore.getState().selectedTables).toEqual(['account']);
    });

    it('removeTable removes table and cleans up roles/relationships', () => {
      useConfigStore.getState().addTable('account');
      useConfigStore.getState().setFactTable('account');
      useConfigStore.getState().addRelationship({
        sourceTable: 'account',
        sourceAttribute: 'ownerid',
        targetTable: 'systemuser',
        targetAttribute: 'systemuserid',
        isActive: true,
        isAutoDetected: true,
      });

      useConfigStore.getState().removeTable('account');

      const state = useConfigStore.getState();
      expect(state.selectedTables).toEqual([]);
      expect(state.factTable).toBeNull();
      expect(state.relationships).toEqual([]);
    });

    it('toggleTable adds if missing, removes if present', () => {
      useConfigStore.getState().toggleTable('contact');
      expect(useConfigStore.getState().selectedTables).toContain('contact');

      useConfigStore.getState().toggleTable('contact');
      expect(useConfigStore.getState().selectedTables).not.toContain('contact');
    });
  });

  describe('star schema', () => {
    it('setFactTable sets role and clears previous fact', () => {
      useConfigStore.getState().addTable('account');
      useConfigStore.getState().addTable('contact');

      useConfigStore.getState().setFactTable('account');
      expect(useConfigStore.getState().factTable).toBe('account');
      expect(useConfigStore.getState().tableRoles['account']).toBe(TableRole.Fact);

      useConfigStore.getState().setFactTable('contact');
      expect(useConfigStore.getState().factTable).toBe('contact');
      expect(useConfigStore.getState().tableRoles['contact']).toBe(TableRole.Fact);
      expect(useConfigStore.getState().tableRoles['account']).toBeUndefined();
    });

    it('toggleRelationshipActive flips isActive', () => {
      useConfigStore.getState().addRelationship({
        sourceTable: 'account',
        sourceAttribute: 'ownerid',
        targetTable: 'systemuser',
        targetAttribute: 'systemuserid',
        isActive: true,
        isAutoDetected: true,
      });

      useConfigStore.getState().toggleRelationshipActive('account', 'ownerid', 'systemuser');
      expect(useConfigStore.getState().relationships[0].isActive).toBe(false);
    });

    it('removeRelationship filters by composite key', () => {
      useConfigStore.getState().addRelationship({
        sourceTable: 'account',
        sourceAttribute: 'ownerid',
        targetTable: 'systemuser',
        targetAttribute: 'systemuserid',
        isActive: true,
        isAutoDetected: true,
      });
      useConfigStore.getState().addRelationship({
        sourceTable: 'contact',
        sourceAttribute: 'parentcustomerid',
        targetTable: 'account',
        targetAttribute: 'accountid',
        isActive: true,
        isAutoDetected: true,
      });

      useConfigStore.getState().removeRelationship('account', 'ownerid', 'systemuser');
      expect(useConfigStore.getState().relationships).toHaveLength(1);
      expect(useConfigStore.getState().relationships[0].sourceTable).toBe('contact');
    });
  });

  describe('forms & views', () => {
    it('setTableForm stores formId and formName', () => {
      useConfigStore.getState().setTableForm('account', 'form-123', 'Main Form');
      expect(useConfigStore.getState().tableForms['account']).toBe('form-123');
      expect(useConfigStore.getState().tableFormNames['account']).toBe('Main Form');
    });

    it('clearTableForm removes both entries', () => {
      useConfigStore.getState().setTableForm('account', 'form-123', 'Main Form');
      useConfigStore.getState().clearTableForm('account');
      expect(useConfigStore.getState().tableForms['account']).toBeUndefined();
      expect(useConfigStore.getState().tableFormNames['account']).toBeUndefined();
    });
  });

  describe('attributes', () => {
    it('toggleAttribute adds if missing, removes if present', () => {
      useConfigStore.getState().toggleAttribute('account', 'name');
      expect(useConfigStore.getState().tableAttributes['account']).toContain('name');

      useConfigStore.getState().toggleAttribute('account', 'name');
      expect(useConfigStore.getState().tableAttributes['account']).not.toContain('name');
    });

    it('setAttributeDisplayNameOverride stores override', () => {
      useConfigStore.getState().setAttributeDisplayNameOverride('account', 'name', 'Account Name');
      expect(useConfigStore.getState().attributeDisplayNameOverrides['account']['name']).toBe('Account Name');
    });
  });

  describe('serialization', () => {
    it('toSettings and loadFromSettings round-trip', () => {
      useConfigStore.getState().setConfigName('Test Config');
      useConfigStore.getState().setProjectName('TestProject');
      useConfigStore.getState().addTable('account');
      useConfigStore.getState().addTable('contact');
      useConfigStore.getState().setFactTable('account');
      useConfigStore.getState().setTableAttributes('account', ['name', 'revenue']);

      const settings = useConfigStore.getState().toSettings();
      expect(settings.selectedTables).toEqual(['account', 'contact']);
      expect(settings.factTable).toBe('account');
      expect(settings.projectName).toBe('TestProject');

      // Reset and reload
      useConfigStore.getState().reset();
      expect(useConfigStore.getState().selectedTables).toEqual([]);

      useConfigStore.getState().loadFromSettings('Test Config', settings);
      const state = useConfigStore.getState();
      expect(state.configName).toBe('Test Config');
      expect(state.selectedTables).toEqual(['account', 'contact']);
      expect(state.factTable).toBe('account');
      expect(state.isDirty).toBe(false);
    });
  });
});

// #endregion

// #region Connection Store

describe('useConnectionStore', () => {
  beforeEach(() => {
    useConnectionStore.getState().reset();
  });

  it('initializes as disconnected', () => {
    const state = useConnectionStore.getState();
    expect(state.status).toBe('disconnected');
    expect(state.connection).toBeNull();
  });

  it('setConnection updates status to connected', () => {
    useConnectionStore.getState().setConnection({
      name: 'Test Org',
      url: 'https://test.crm.dynamics.com',
      environment: 'test',
    });
    const state = useConnectionStore.getState();
    expect(state.status).toBe('connected');
    expect(state.connection?.name).toBe('Test Org');
    expect(state.error).toBeNull();
  });

  it('setConnection(null) sets status to disconnected', () => {
    useConnectionStore.getState().setConnection({
      name: 'Test',
      url: 'https://test.crm.dynamics.com',
      environment: 'test',
    });
    useConnectionStore.getState().setConnection(null);
    expect(useConnectionStore.getState().status).toBe('disconnected');
  });

  it('setError sets error status', () => {
    useConnectionStore.getState().setError('Connection failed');
    const state = useConnectionStore.getState();
    expect(state.status).toBe('error');
    expect(state.error).toBe('Connection failed');
  });
});

// #endregion

// #region UI Store

describe('useUIStore', () => {
  beforeEach(() => {
    useUIStore.getState().reset();
  });

  it('initializes with setup tab', () => {
    expect(useUIStore.getState().activeTab).toBe('setup');
  });

  it('setActiveTab changes tab', () => {
    useUIStore.getState().setActiveTab('build');
    expect(useUIStore.getState().activeTab).toBe('build');
  });

  it('addToast creates toast with unique id', () => {
    useUIStore.getState().addToast({ type: 'success', title: 'Done' });
    useUIStore.getState().addToast({ type: 'error', title: 'Failed' });

    const toasts = useUIStore.getState().toasts;
    expect(toasts).toHaveLength(2);
    expect(toasts[0].id).not.toBe(toasts[1].id);
    expect(toasts[0].type).toBe('success');
    expect(toasts[1].type).toBe('error');
  });

  it('removeToast removes by id', () => {
    useUIStore.getState().addToast({ type: 'info', title: 'Test' });
    const id = useUIStore.getState().toasts[0].id;

    useUIStore.getState().removeToast(id);
    expect(useUIStore.getState().toasts).toHaveLength(0);
  });

  it('openDialog and closeDialog toggle visibility', () => {
    expect(useUIStore.getState().dialogs.configManager).toBe(false);

    useUIStore.getState().openDialog('configManager');
    expect(useUIStore.getState().dialogs.configManager).toBe(true);

    useUIStore.getState().closeDialog('configManager');
    expect(useUIStore.getState().dialogs.configManager).toBe(false);
  });

  it('openDialog with context sets tableName', () => {
    useUIStore.getState().openDialog('formPicker', { tableName: 'account' });
    expect(useUIStore.getState().dialogs.formPicker).toBe(true);
    expect(useUIStore.getState().pickerContext.tableName).toBe('account');
  });

  it('setGlobalLoading manages loading state', () => {
    useUIStore.getState().setGlobalLoading(true, 'Loading tables...');
    expect(useUIStore.getState().globalLoading).toBe(true);
    expect(useUIStore.getState().loadingMessage).toBe('Loading tables...');

    useUIStore.getState().setGlobalLoading(false);
    expect(useUIStore.getState().globalLoading).toBe(false);
    expect(useUIStore.getState().loadingMessage).toBeNull();
  });
});

// #endregion

// #region Metadata Store

describe('useMetadataStore', () => {
  beforeEach(() => {
    useMetadataStore.getState().reset();
  });

  it('initializes empty', () => {
    const state = useMetadataStore.getState();
    expect(state.solutions).toEqual([]);
    expect(state.tables).toEqual([]);
  });

  it('setSolutions stores solutions', () => {
    useMetadataStore.getState().setSolutions([
      { uniqueName: 'default', friendlyName: 'Default Solution', solutionId: '1' },
    ]);
    expect(useMetadataStore.getState().solutions).toHaveLength(1);
    expect(useMetadataStore.getState().solutions[0].uniqueName).toBe('default');
  });

  it('setTables stores tables', () => {
    const tables: TableInfo[] = [
      {
        logicalName: 'account',
        displayName: 'Account',
        schemaName: 'Account',
        objectTypeCode: 1,
      },
    ];
    useMetadataStore.getState().setTables(tables);
    expect(useMetadataStore.getState().tables).toHaveLength(1);
    expect(useMetadataStore.getState().tables[0].logicalName).toBe('account');
  });

  it('setTableAttributes stores attributes for a table', () => {
    const attributes: AttributeMetadata[] = [
      {
        logicalName: 'name',
        displayName: 'Name',
        schemaName: 'Name',
        attributeType: 'String',
        isCustomAttribute: false,
        isRequired: false,
      },
    ];
    useMetadataStore.getState().setTableAttributes('account', attributes);
    expect(useMetadataStore.getState().tableAttributes['account']).toHaveLength(1);
    expect(useMetadataStore.getState().tableAttributes['account'][0].logicalName).toBe('name');
  });
});

// #endregion
