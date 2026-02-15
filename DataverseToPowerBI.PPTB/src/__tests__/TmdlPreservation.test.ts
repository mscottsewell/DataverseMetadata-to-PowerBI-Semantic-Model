/**
 * TmdlPreservation.test.ts - Tests for TMDL preservation logic
 */

import { describe, it, expect } from 'vitest';
import {
  parseExistingLineageTags,
  parseExistingColumnMetadata,
  parseExistingRelationshipGuids,
  parseExistingRelationshipBlocks,
  extractUserRelationships,
  extractUserMeasuresSection,
  insertUserMeasures,
} from '../core/tmdl/TmdlPreservation';

describe('TmdlPreservation', () => {
  // #region LineageTag Parsing

  describe('parseExistingLineageTags', () => {
    it('extracts table lineageTag', () => {
      const tmdl = `table Account\r\n\tlineageTag: abc-123\r\n`;
      const tags = parseExistingLineageTags(tmdl);
      expect(tags['table']).toBe('abc-123');
    });

    it('extracts column lineageTag with sourceColumn', () => {
      const tmdl = [
        'table Account',
        '\tlineageTag: table-guid',
        '',
        '\tcolumn Name',
        '\t\tdataType: string',
        '\t\tlineageTag: col-guid-1',
        '\t\tsourceColumn: name',
        '',
      ].join('\r\n');
      const tags = parseExistingLineageTags(tmdl);
      expect(tags['col:name']).toBe('col-guid-1');
    });

    it('extracts column lineageTag when lineageTag is before sourceColumn', () => {
      const tmdl = [
        'table Account',
        '\tlineageTag: table-guid',
        '',
        '\tcolumn Name',
        '\t\tdataType: string',
        '\t\tlineageTag: col-guid-1',
        '\t\tsummarizeBy: none',
        '\t\tsourceColumn: name',
        '',
      ].join('\r\n');
      const tags = parseExistingLineageTags(tmdl);
      expect(tags['col:name']).toBe('col-guid-1');
    });

    it('extracts measure lineageTag with quoted name', () => {
      const tmdl = [
        "table Account",
        "\tlineageTag: table-guid",
        "",
        "\tmeasure 'Account Count' = COUNTROWS('Account')",
        "\t\tlineageTag: measure-guid-1",
        "",
      ].join('\r\n');
      const tags = parseExistingLineageTags(tmdl);
      expect(tags['measure:Account Count']).toBe('measure-guid-1');
    });

    it('extracts expression lineageTag', () => {
      const tmdl = `expression DataverseURL = "test.crm.dynamics.com"\r\n\tlineageTag: expr-guid-1\r\n`;
      const tags = parseExistingLineageTags(tmdl);
      expect(tags['expr:DataverseURL']).toBe('expr-guid-1');
    });

    it('stops tracking at partition', () => {
      const tmdl = [
        'table Account',
        '\tlineageTag: table-guid',
        '',
        '\tpartition Account = m',
        '\t\tlineageTag: should-not-be-tracked',
        '',
      ].join('\r\n');
      const tags = parseExistingLineageTags(tmdl);
      expect(tags['table']).toBe('table-guid');
      expect(Object.keys(tags)).toHaveLength(1);
    });
  });

  // #endregion

  // #region Column Metadata Parsing

  describe('parseExistingColumnMetadata', () => {
    it('extracts column metadata', () => {
      const tmdl = [
        '\tcolumn Revenue',
        '\t\tdataType: double',
        '\t\tformatString: #,0.00',
        '\t\tsummarizeBy: sum',
        '\t\tsourceColumn: revenue',
        '\t\t',
        '\t\tannotation SummarizationSetBy = Automatic',
        '\t\t',
      ].join('\r\n');
      const columns = parseExistingColumnMetadata(tmdl);
      expect(columns['revenue']).toBeDefined();
      expect(columns['revenue'].dataType).toBe('double');
      expect(columns['revenue'].formatString).toBe('#,0.00');
      expect(columns['revenue'].summarizeBy).toBe('sum');
      expect(columns['revenue'].annotations['SummarizationSetBy']).toBe('Automatic');
    });

    it('handles multiple columns', () => {
      const tmdl = [
        '\tcolumn Name',
        '\t\tdataType: string',
        '\t\tsourceColumn: name',
        '\t\t',
        '\tcolumn Revenue',
        '\t\tdataType: double',
        '\t\tsourceColumn: revenue',
        '\t\t',
      ].join('\r\n');
      const columns = parseExistingColumnMetadata(tmdl);
      expect(Object.keys(columns)).toHaveLength(2);
    });
  });

  // #endregion

  // #region Relationship Parsing

  describe('parseExistingRelationshipGuids', () => {
    it('extracts relationship GUIDs', () => {
      const content = 'relationship abc-123\r\n\tfromColumn: Account.transactioncurrencyid\r\n\ttoColumn: Currency.transactioncurrencyid\r\n\r\n';
      const guids = parseExistingRelationshipGuids(content);
      const keys = Object.keys(guids);
      expect(keys.length).toBe(1);
      expect(keys[0]).toContain('Account.transactioncurrencyid');
      expect(keys[0]).toContain('Currency.transactioncurrencyid');
      expect(Object.values(guids)[0]).toBe('abc-123');
    });
  });

  describe('parseExistingRelationshipBlocks', () => {
    it('extracts full relationship blocks', () => {
      const content = [
        'relationship abc-123',
        '\trelyOnReferentialIntegrity',
        '\tfromColumn: Account.transactioncurrencyid',
        '\ttoColumn: Currency.transactioncurrencyid',
        '',
      ].join('\r\n');
      const blocks = parseExistingRelationshipBlocks(content);
      const key = 'Account.transactioncurrencyid→Currency.transactioncurrencyid';
      expect(blocks[key]).toBeDefined();
      expect(blocks[key]).toContain('relyOnReferentialIntegrity');
    });
  });

  // #endregion

  // #region User Relationships

  describe('extractUserRelationships', () => {
    it('identifies user-added relationships', () => {
      const existingBlocks: Record<string, string> = {
        'Account.ownerid→User.systemuserid': 'relationship user-rel\n\tfromColumn: Account.ownerid\n\ttoColumn: User.systemuserid\n',
        'Account.currency→Currency.id': 'relationship tool-rel\n\tfromColumn: Account.currency\n\ttoColumn: Currency.id\n',
      };
      const toolKeys = new Set(['Account.currency→Currency.id']);
      const result = extractUserRelationships(existingBlocks, toolKeys);
      expect(result).not.toBeNull();
      expect(result).toContain('User-added relationship');
      expect(result).toContain('Account.ownerid');
    });

    it('skips stale date relationships', () => {
      const existingBlocks: Record<string, string> = {
        'Account.createdon→Date.Date': 'relationship old-date\n\tfromColumn: Account.createdon\n\ttoColumn: Date.Date\n',
      };
      const toolKeys = new Set<string>();
      const result = extractUserRelationships(existingBlocks, toolKeys);
      expect(result).toBeNull();
    });

    it('returns null when no user relationships exist', () => {
      const existingBlocks: Record<string, string> = {
        'A.col→B.col': 'relationship r1\n\tfromColumn: A.col\n\ttoColumn: B.col\n',
      };
      const toolKeys = new Set(['A.col→B.col']);
      const result = extractUserRelationships(existingBlocks, toolKeys);
      expect(result).toBeNull();
    });
  });

  // #endregion

  // #region User Measures

  describe('extractUserMeasuresSection', () => {
    it('extracts user measures and excludes auto-generated ones', () => {
      const tmdl = [
        "\tmeasure 'Link to Account' = 1",
        '\t\tlineageTag: auto-1',
        '\t\tformatString: 0',
        '',
        "\tmeasure 'Account Count' = COUNTROWS('Account')",
        '\t\tlineageTag: auto-2',
        '\t\tformatString: 0',
        '',
        "\tmeasure 'Custom Measure' = SUM('Account'[Revenue])",
        '\t\tlineageTag: user-1',
        '\t\tformatString: #,0',
        '',
      ].join('\r\n');
      const result = extractUserMeasuresSection(tmdl, {
        logicalName: 'account',
        displayName: 'Account',
        objectTypeCode: 1,
        role: 'Fact',
        hasStateCode: true,
        forms: [],
        attributes: [],
      });
      expect(result).not.toBeNull();
      expect(result).toContain('Custom Measure');
      expect(result).not.toContain('Link to Account');
      expect(result).not.toContain('Account Count');
    });

    it('returns null when only auto-generated measures exist', () => {
      const tmdl = [
        "\tmeasure 'Link to Account' = 1",
        '\t\tlineageTag: auto-1',
        '\t\tformatString: 0',
        '',
      ].join('\r\n');
      const result = extractUserMeasuresSection(tmdl, {
        logicalName: 'account',
        displayName: 'Account',
        objectTypeCode: 1,
        role: 'Fact',
        hasStateCode: true,
        forms: [],
        attributes: [],
      });
      expect(result).toBeNull();
    });
  });

  describe('insertUserMeasures', () => {
    it('inserts measures before partition', () => {
      const tmdl = '\tcolumn Name\r\n\t\tdataType: string\r\n\r\n\tpartition Account = m\r\n\t\tmode: directQuery\r\n';
      const measures = "\tmeasure 'Custom' = 1\r\n\t\tlineageTag: guid\r\n\r\n";
      const result = insertUserMeasures(tmdl, measures);
      expect(result.indexOf('Custom')).toBeLessThan(result.indexOf('partition'));
    });

    it('inserts before annotation if no partition', () => {
      const tmdl = '\tcolumn Name\r\n\t\tdataType: string\r\n\r\n\tannotation PBI = test\r\n';
      const measures = "\tmeasure 'Custom' = 1\r\n";
      const result = insertUserMeasures(tmdl, measures);
      expect(result.indexOf('Custom')).toBeLessThan(result.indexOf('annotation'));
    });
  });

  // #endregion
});
