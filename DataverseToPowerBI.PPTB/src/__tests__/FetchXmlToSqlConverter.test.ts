/**
 * FetchXmlToSqlConverter.test.ts - Tests for FetchXML to SQL conversion
 */

import { describe, it, expect } from 'vitest';
import { FetchXmlToSqlConverter } from '../core/converters/FetchXmlToSqlConverter';

describe('FetchXmlToSqlConverter', () => {
  // #region Basic Comparison Operators

  describe('basic comparison operators', () => {
    it('converts eq operator', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="eq" value="test" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain("Base.name = 'test'");
      expect(result.isFullySupported).toBe(true);
    });

    it('converts ne operator', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="ne" value="test" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain("Base.name <> 'test'");
    });

    it('converts gt/ge/lt/le operators', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="revenue" operator="gt" value="1000" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain('Base.revenue > 1000');
    });

    it('formats integer values without quotes', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="statecode" operator="eq" value="0" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain('Base.statecode = 0');
    });

    it('formats GUID values with quotes', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="accountid" operator="eq" value="12345678-1234-1234-1234-123456789012" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain("Base.accountid = '12345678-1234-1234-1234-123456789012'");
    });
  });

  // #endregion

  // #region Null Operators

  describe('null operators', () => {
    it('converts null operator', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="null" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain('Base.name IS NULL');
    });

    it('converts not-null operator', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="not-null" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain('Base.name IS NOT NULL');
    });
  });

  // #endregion

  // #region String Operators

  describe('string operators', () => {
    it('converts begins-with', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="begins-with" value="Contoso" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain("Base.name LIKE 'Contoso%'");
    });

    it('converts ends-with', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="ends-with" value="Inc" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain("Base.name LIKE '%Inc'");
    });

    it('escapes single quotes in string values', () => {
      const xml = `<fetch><entity name="account"><filter><condition attribute="name" operator="eq" value="O'Brien" /></filter></entity></fetch>`;
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain("O''Brien");
    });
  });

  // #endregion

  // #region Date Operators

  describe('date operators', () => {
    it('converts today operator with timezone adjustment', () => {
      const result = new FetchXmlToSqlConverter(-6).convertToWhereClause(
        '<fetch><entity name="account"><filter><condition attribute="createdon" operator="today" /></filter></entity></fetch>'
      );
      expect(result.sqlWhereClause).toContain('DATEADD(hour, -6');
      expect(result.sqlWhereClause).toContain('GETUTCDATE()');
      expect(result.isFullySupported).toBe(true);
    });

    it('converts this-year operator', () => {
      const result = new FetchXmlToSqlConverter().convertToWhereClause(
        '<fetch><entity name="account"><filter><condition attribute="createdon" operator="this-year" /></filter></entity></fetch>'
      );
      expect(result.sqlWhereClause).toContain('DATEPART(year');
    });

    it('converts last-x-days operator', () => {
      const result = new FetchXmlToSqlConverter().convertToWhereClause(
        '<fetch><entity name="account"><filter><condition attribute="createdon" operator="last-x-days" value="30" /></filter></entity></fetch>'
      );
      expect(result.sqlWhereClause).toContain('DATEDIFF(day');
      expect(result.isFullySupported).toBe(true);
    });

    it('converts on operator with date cast', () => {
      const result = new FetchXmlToSqlConverter().convertToWhereClause(
        '<fetch><entity name="account"><filter><condition attribute="createdon" operator="on" value="2024-01-15" /></filter></entity></fetch>'
      );
      expect(result.sqlWhereClause).toContain('CAST(');
      expect(result.sqlWhereClause).toContain('AS DATE');
    });
  });

  // #endregion

  // #region List Operators

  describe('list operators', () => {
    it('converts in operator with value elements', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="statecode" operator="in"><value>0</value><value>1</value></condition></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain('Base.statecode IN (0, 1)');
    });

    it('converts not-in operator', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="statecode" operator="not-in"><value>2</value><value>3</value></condition></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain('Base.statecode NOT IN (2, 3)');
    });
  });

  // #endregion

  // #region Filter Logic

  describe('filter logic', () => {
    it('handles AND filter type', () => {
      const xml = '<fetch><entity name="account"><filter type="and"><condition attribute="name" operator="eq" value="test" /><condition attribute="statecode" operator="eq" value="0" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain(' AND ');
    });

    it('handles OR filter type', () => {
      const xml = '<fetch><entity name="account"><filter type="or"><condition attribute="name" operator="eq" value="A" /><condition attribute="name" operator="eq" value="B" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain(' OR ');
    });

    it('handles nested filters', () => {
      const xml = '<fetch><entity name="account"><filter type="and"><condition attribute="statecode" operator="eq" value="0" /><filter type="or"><condition attribute="name" operator="eq" value="A" /><condition attribute="name" operator="eq" value="B" /></filter></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain(' AND ');
      expect(result.sqlWhereClause).toContain(' OR ');
    });
  });

  // #endregion

  // #region User Context Operators

  describe('user context operators', () => {
    it('supports eq-userid in TDS mode', () => {
      const result = new FetchXmlToSqlConverter(-6, false).convertToWhereClause(
        '<fetch><entity name="account"><filter><condition attribute="ownerid" operator="eq-userid" /></filter></entity></fetch>'
      );
      expect(result.sqlWhereClause).toContain('CURRENT_USER');
      expect(result.isFullySupported).toBe(true);
    });

    it('rejects eq-userid in FabricLink mode', () => {
      const result = new FetchXmlToSqlConverter(-6, true).convertToWhereClause(
        '<fetch><entity name="account"><filter><condition attribute="ownerid" operator="eq-userid" /></filter></entity></fetch>'
      );
      expect(result.sqlWhereClause).toBe('');
      expect(result.isFullySupported).toBe(false);
      expect(result.unsupportedFeatures.length).toBeGreaterThan(0);
    });

    it('rejects eq-userid in import mode', () => {
      const result = new FetchXmlToSqlConverter(-6, false, true).convertToWhereClause(
        '<fetch><entity name="account"><filter><condition attribute="ownerid" operator="eq-userid" /></filter></entity></fetch>'
      );
      expect(result.isFullySupported).toBe(false);
    });
  });

  // #endregion

  // #region Link-Entity Filters

  describe('link-entity filters', () => {
    it('converts link-entity filter to EXISTS subquery', () => {
      const xml = `<fetch><entity name="account">
        <link-entity name="contact" from="parentcustomerid" to="accountid" alias="c">
          <filter><condition attribute="firstname" operator="eq" value="John" /></filter>
        </link-entity>
      </entity></fetch>`;
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.sqlWhereClause).toContain('EXISTS');
      expect(result.sqlWhereClause).toContain('SELECT 1 FROM contact');
      expect(result.sqlWhereClause).toContain("c.firstname = 'John'");
    });
  });

  // #endregion

  // #region Edge Cases

  describe('edge cases', () => {
    it('handles empty fetchXml', () => {
      const result = new FetchXmlToSqlConverter().convertToWhereClause('');
      expect(result.sqlWhereClause).toBe('');
      expect(result.isFullySupported).toBe(true);
    });

    it('handles missing entity element', () => {
      const result = new FetchXmlToSqlConverter().convertToWhereClause('<fetch></fetch>');
      expect(result.sqlWhereClause).toBe('');
    });

    it('handles invalid XML', () => {
      const result = new FetchXmlToSqlConverter().convertToWhereClause('not xml');
      expect(result.isFullySupported).toBe(false);
    });

    it('logs unsupported operators', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="unknown-op" value="x" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml);
      expect(result.isFullySupported).toBe(false);
      expect(result.unsupportedFeatures).toContain("Operator 'unknown-op' for attribute 'name'");
    });

    it('uses custom table alias', () => {
      const xml = '<fetch><entity name="account"><filter><condition attribute="name" operator="eq" value="test" /></filter></entity></fetch>';
      const result = new FetchXmlToSqlConverter().convertToWhereClause(xml, 'T1');
      expect(result.sqlWhereClause).toContain("T1.name = 'test'");
    });
  });

  // #endregion
});
