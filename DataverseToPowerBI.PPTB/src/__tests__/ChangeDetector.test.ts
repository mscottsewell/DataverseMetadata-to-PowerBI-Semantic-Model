/**
 * ChangeDetector tests - Column parsing and expected column generation
 */

import { describe, it, expect } from 'vitest';
import {
  parseExistingColumns,
  ChangeType,
  ImpactLevel,
} from '../core/tmdl/ChangeDetector';

describe('ChangeDetector', () => {
  describe('parseExistingColumns', () => {
    it('parses a basic column with properties', () => {
      const tmdl = `column 'Account Name'
\tdataType: string
\tsourceColumn: name
\tlineageTag: abc-123
`;
      const result = parseExistingColumns(tmdl);
      expect(result['account name']).toBeDefined();
      expect(result['account name'].displayName).toBe('Account Name');
      expect(result['account name'].dataType).toBe('string');
      expect(result['account name'].sourceColumn).toBe('name');
    });

    it('parses column with logical name comment', () => {
      const tmdl = `/// name
column 'Account Name'
\tdataType: string
\tsourceColumn: name
`;
      const result = parseExistingColumns(tmdl);
      expect(result['account name']).toBeDefined();
      expect(result['account name'].logicalName).toBe('name');
    });

    it('parses multiple columns', () => {
      const tmdl = `/// accountid
column accountid
\tdataType: int64
\tsourceColumn: accountid
\tformatString: 0

/// name
column 'Account Name'
\tdataType: string
\tsourceColumn: name
`;
      const result = parseExistingColumns(tmdl);
      expect(Object.keys(result)).toHaveLength(2);
      expect(result['accountid']).toBeDefined();
      expect(result['account name']).toBeDefined();
      expect(result['accountid'].dataType).toBe('int64');
      expect(result['accountid'].formatString).toBe('0');
    });

    it('returns empty for non-TMDL content', () => {
      const result = parseExistingColumns('not a valid tmdl file');
      expect(Object.keys(result)).toHaveLength(0);
    });

    it('handles double-quoted column names', () => {
      const tmdl = `column "Revenue"
\tdataType: decimal
\tsourceColumn: revenue
`;
      const result = parseExistingColumns(tmdl);
      expect(result['revenue']).toBeDefined();
      expect(result['revenue'].displayName).toBe('Revenue');
    });
  });

  describe('ChangeType enum', () => {
    it('has expected values', () => {
      expect(ChangeType.New).toBe('New');
      expect(ChangeType.Update).toBe('Update');
      expect(ChangeType.Preserve).toBe('Preserve');
      expect(ChangeType.Warning).toBe('Warning');
      expect(ChangeType.Error).toBe('Error');
      expect(ChangeType.Info).toBe('Info');
    });
  });

  describe('ImpactLevel enum', () => {
    it('has expected values', () => {
      expect(ImpactLevel.Safe).toBe('Safe');
      expect(ImpactLevel.Additive).toBe('Additive');
      expect(ImpactLevel.Moderate).toBe('Moderate');
      expect(ImpactLevel.Destructive).toBe('Destructive');
    });
  });
});
