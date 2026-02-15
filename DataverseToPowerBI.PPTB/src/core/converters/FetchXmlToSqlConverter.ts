/**
 * FetchXmlToSqlConverter.ts - FetchXML to SQL WHERE Clause Translation
 *
 * PURPOSE:
 * Converts Dataverse FetchXML filter conditions to SQL WHERE clauses for use in
 * Power BI DirectQuery partition expressions.
 *
 * SUPPORTED OPERATORS:
 * Basic Comparison: eq, ne, gt, ge, lt, le
 * Null Checking: null, not-null
 * String Matching: like, not-like, begins-with, ends-with
 * Date Relative: today, yesterday, this-week, last-month, etc.
 * Date Dynamic: last-x-days, next-x-months, older-x-years, etc.
 * List Operations: in, not-in
 * User Context: eq-userid, ne-userid, eq-userteams, ne-userteams (TDS only)
 *
 * TIMEZONE HANDLING:
 * All date comparisons include UTC offset adjustment using DATEADD(hour, offset, column).
 *
 * FABRICLINK LIMITATIONS:
 * User context operators are NOT supported in FabricLink mode.
 */

import { logger } from '../../utils/Logger';

// #region Models

/** Result of a FetchXML to SQL conversion */
export interface ConversionResult {
  /** The generated SQL WHERE clause */
  sqlWhereClause: string;
  /** True if all operators were successfully converted */
  isFullySupported: boolean;
  /** List of operators that couldn't be translated */
  unsupportedFeatures: string[];
  /** Detailed conversion trace for troubleshooting */
  debugLog: string[];
  /** Human-readable summary */
  summary: string;
}

// #endregion

// #region Converter Class

export class FetchXmlToSqlConverter {
  private readonly debugLog: string[] = [];
  private readonly unsupportedFeatures: string[] = [];
  private hasUnsupportedFeatures = false;
  private readonly utcOffsetHours: number;
  private readonly isFabricLink: boolean;
  private readonly isImportMode: boolean;

  constructor(utcOffsetHours = -6, isFabricLink = false, isImportMode = false) {
    this.utcOffsetHours = utcOffsetHours;
    this.isFabricLink = isFabricLink;
    this.isImportMode = isImportMode;
  }

  /** Converts FetchXML to SQL WHERE clause */
  convertToWhereClause(fetchXml: string, tableAlias = 'Base'): ConversionResult {
    this.debugLog.length = 0;
    this.unsupportedFeatures.length = 0;
    this.hasUnsupportedFeatures = false;

    try {
      this.debugLog.push(`Starting FetchXML conversion for table alias: ${tableAlias}`);

      if (!fetchXml?.trim()) {
        this.debugLog.push('FetchXML is empty');
        return this.createResult('', true);
      }

      const doc = this.parseXml(fetchXml);
      const entity = doc.querySelector('entity');

      if (!entity) {
        this.debugLog.push('No entity element found in FetchXML');
        return this.createResult('', true);
      }

      const entityName = entity.getAttribute('name') ?? 'unknown';
      this.debugLog.push(`Entity: ${entityName}`);

      const filters = Array.from(entity.querySelectorAll(':scope > filter'));
      const linkEntities = Array.from(entity.querySelectorAll(':scope > link-entity'));

      const whereClauses: string[] = [];

      // Process main entity filters
      if (filters.length > 0) {
        this.debugLog.push(`Processing ${filters.length} main entity filter(s)`);
        for (const filter of filters) {
          const clause = this.processFilter(filter, tableAlias);
          if (clause) {
            whereClauses.push(clause);
          }
        }
      }

      // Process link-entity filters
      if (linkEntities.length > 0) {
        this.debugLog.push(`Processing ${linkEntities.length} link-entity filter(s)`);
        for (const linkEntity of linkEntities) {
          const linkClauses = this.processLinkEntityFilters(linkEntity, tableAlias);
          whereClauses.push(...linkClauses);
        }
      }

      const finalClause = whereClauses.length > 0
        ? whereClauses.map(c => `(${c})`).join(' AND ')
        : '';

      this.debugLog.push(`Final WHERE clause: ${finalClause}`);

      return this.createResult(finalClause, !this.hasUnsupportedFeatures);
    } catch (ex) {
      const msg = ex instanceof Error ? ex.message : String(ex);
      this.debugLog.push(`ERROR: ${msg}`);
      this.logUnsupported(`Failed to parse FetchXML: ${msg}`);
      return this.createResult('', false);
    }
  }

  // #region Filter Processing

  private processFilter(filter: Element, tableAlias: string): string {
    const filterType = filter.getAttribute('type') ?? 'and';
    this.debugLog.push(`Processing filter with type: ${filterType}`);

    const conditions = Array.from(filter.querySelectorAll(':scope > condition'));
    const nestedFilters = Array.from(filter.querySelectorAll(':scope > filter'));

    const clauses: string[] = [];

    for (const condition of conditions) {
      const clause = this.processCondition(condition, tableAlias);
      if (clause) {
        clauses.push(clause);
      }
    }

    for (const nestedFilter of nestedFilters) {
      const clause = this.processFilter(nestedFilter, tableAlias);
      if (clause) {
        clauses.push(`(${clause})`);
      }
    }

    if (clauses.length === 0) return '';

    const separator = filterType.toLowerCase() === 'or' ? ' OR ' : ' AND ';
    return clauses.join(separator);
  }

  private processCondition(condition: Element, tableAlias: string): string {
    const attribute = condition.getAttribute('attribute');
    const operatorValue = condition.getAttribute('operator');
    const value = condition.getAttribute('value');

    if (!attribute || !operatorValue) {
      this.debugLog.push('Condition missing attribute or operator - skipping');
      return '';
    }

    this.debugLog.push(`  Condition: ${attribute} ${operatorValue} ${value ?? '(no value)'}`);

    const columnRef = `${tableAlias}.${attribute}`;
    const safeValue = value ?? '';
    const operatorKey = operatorValue.toLowerCase();

    try {
      switch (operatorKey) {
        // Basic comparison operators
        case 'eq': return `${columnRef} = ${this.formatValue(safeValue)}`;
        case 'ne': return `${columnRef} <> ${this.formatValue(safeValue)}`;
        case 'gt': return `${columnRef} > ${this.formatValue(safeValue)}`;
        case 'ge': return `${columnRef} >= ${this.formatValue(safeValue)}`;
        case 'lt': return `${columnRef} < ${this.formatValue(safeValue)}`;
        case 'le': return `${columnRef} <= ${this.formatValue(safeValue)}`;

        // Null operators
        case 'null': return `${columnRef} IS NULL`;
        case 'not-null': return `${columnRef} IS NOT NULL`;

        // String operators
        case 'like': return `${columnRef} LIKE ${this.formatValue(safeValue)}`;
        case 'not-like': return `${columnRef} NOT LIKE ${this.formatValue(safeValue)}`;
        case 'begins-with': return `${columnRef} LIKE ${this.formatValue(safeValue + '%')}`;
        case 'not-begin-with': return `${columnRef} NOT LIKE ${this.formatValue(safeValue + '%')}`;
        case 'ends-with': return `${columnRef} LIKE ${this.formatValue('%' + safeValue)}`;
        case 'not-end-with': return `${columnRef} NOT LIKE ${this.formatValue('%' + safeValue)}`;

        // Date operators - absolute
        case 'today': return this.convertDateOperator(columnRef, 'today');
        case 'yesterday': return this.convertDateOperator(columnRef, 'yesterday');
        case 'tomorrow': return this.convertDateOperator(columnRef, 'tomorrow');
        case 'this-week': return this.convertDateOperator(columnRef, 'this-week');
        case 'last-week': return this.convertDateOperator(columnRef, 'last-week');
        case 'next-week': return this.convertDateOperator(columnRef, 'next-week');
        case 'this-month': return this.convertDateOperator(columnRef, 'this-month');
        case 'last-month': return this.convertDateOperator(columnRef, 'last-month');
        case 'next-month': return this.convertDateOperator(columnRef, 'next-month');
        case 'this-year': return this.convertDateOperator(columnRef, 'this-year');
        case 'last-year': return this.convertDateOperator(columnRef, 'last-year');
        case 'next-year': return this.convertDateOperator(columnRef, 'next-year');

        // Date operators - relative with value parameter
        case 'last-x-hours': return this.convertRelativeDateOperator(columnRef, 'hour', safeValue, -1);
        case 'last-x-days': return this.convertRelativeDateOperator(columnRef, 'day', safeValue, -1);
        case 'last-x-weeks': return this.convertRelativeDateOperator(columnRef, 'week', safeValue, -1);
        case 'last-x-months': return this.convertRelativeDateOperator(columnRef, 'month', safeValue, -1);
        case 'last-x-years': return this.convertRelativeDateOperator(columnRef, 'year', safeValue, -1);
        case 'next-x-hours': return this.convertRelativeDateOperator(columnRef, 'hour', safeValue, 1);
        case 'next-x-days': return this.convertRelativeDateOperator(columnRef, 'day', safeValue, 1);
        case 'next-x-weeks': return this.convertRelativeDateOperator(columnRef, 'week', safeValue, 1);
        case 'next-x-months': return this.convertRelativeDateOperator(columnRef, 'month', safeValue, 1);
        case 'next-x-years': return this.convertRelativeDateOperator(columnRef, 'year', safeValue, 1);
        case 'older-x-months': return this.convertOlderThanOperator(columnRef, 'month', safeValue);
        case 'older-x-years': return this.convertOlderThanOperator(columnRef, 'year', safeValue);

        // Date comparison operators (with timezone adjustment)
        case 'on':
          return `CAST(DATEADD(hour, ${this.utcOffsetHours}, ${columnRef}) AS DATE) = CAST(${this.formatValue(safeValue)} AS DATE)`;
        case 'on-or-after':
          return `DATEADD(hour, ${this.utcOffsetHours}, ${columnRef}) >= ${this.formatValue(safeValue)}`;
        case 'on-or-before':
          return `DATEADD(hour, ${this.utcOffsetHours}, ${columnRef}) <= ${this.formatValue(safeValue)}`;

        // User context operators (not supported in FabricLink or Import mode)
        case 'eq-userid':
          return (this.isFabricLink || this.isImportMode)
            ? this.unsupportedUserContextOp('eq-userid', attribute)
            : `${columnRef} = CURRENT_USER`;
        case 'ne-userid':
          return (this.isFabricLink || this.isImportMode)
            ? this.unsupportedUserContextOp('ne-userid', attribute)
            : `${columnRef} <> CURRENT_USER`;
        case 'eq-userteams':
          return (this.isFabricLink || this.isImportMode)
            ? this.unsupportedUserContextOp('eq-userteams', attribute)
            : this.convertUserTeamsOperator(columnRef, true);
        case 'ne-userteams':
          return (this.isFabricLink || this.isImportMode)
            ? this.unsupportedUserContextOp('ne-userteams', attribute)
            : this.convertUserTeamsOperator(columnRef, false);

        // List operators
        case 'in': return this.processInOperator(condition, columnRef);
        case 'not-in': return this.processNotInOperator(condition, columnRef);

        // Unsupported operators
        default: return this.unsupportedOperator(operatorKey, attribute, safeValue);
      }
    } catch (ex) {
      const msg = ex instanceof Error ? ex.message : String(ex);
      this.debugLog.push(`  ERROR processing condition: ${msg}`);
      this.logUnsupported(`Failed to process operator '${operatorKey}' for attribute '${attribute}'`);
      return '';
    }
  }

  // #endregion

  // #region Date Operators

  private convertDateOperator(columnRef: string, dateOperator: string): string {
    const adjustedNow = `DATEADD(hour, ${this.utcOffsetHours}, GETUTCDATE())`;
    const adjustedColumn = `DATEADD(hour, ${this.utcOffsetHours}, ${columnRef})`;

    switch (dateOperator) {
      case 'today':
        return `CAST(${adjustedColumn} AS DATE) = CAST(${adjustedNow} AS DATE)`;
      case 'yesterday':
        return `CAST(${adjustedColumn} AS DATE) = CAST(DATEADD(day, -1, ${adjustedNow}) AS DATE)`;
      case 'tomorrow':
        return `CAST(${adjustedColumn} AS DATE) = CAST(DATEADD(day, 1, ${adjustedNow}) AS DATE)`;

      case 'this-week':
        return `DATEPART(week, ${adjustedColumn}) = DATEPART(week, ${adjustedNow}) AND DATEPART(year, ${adjustedColumn}) = DATEPART(year, ${adjustedNow})`;
      case 'last-week':
        return `DATEPART(week, ${adjustedColumn}) = DATEPART(week, DATEADD(week, -1, ${adjustedNow})) AND DATEPART(year, ${adjustedColumn}) = DATEPART(year, DATEADD(week, -1, ${adjustedNow}))`;
      case 'next-week':
        return `DATEPART(week, ${adjustedColumn}) = DATEPART(week, DATEADD(week, 1, ${adjustedNow})) AND DATEPART(year, ${adjustedColumn}) = DATEPART(year, DATEADD(week, 1, ${adjustedNow}))`;

      case 'this-month':
        return `DATEPART(month, ${adjustedColumn}) = DATEPART(month, ${adjustedNow}) AND DATEPART(year, ${adjustedColumn}) = DATEPART(year, ${adjustedNow})`;
      case 'last-month':
        return `DATEPART(month, ${adjustedColumn}) = DATEPART(month, DATEADD(month, -1, ${adjustedNow})) AND DATEPART(year, ${adjustedColumn}) = DATEPART(year, DATEADD(month, -1, ${adjustedNow}))`;
      case 'next-month':
        return `DATEPART(month, ${adjustedColumn}) = DATEPART(month, DATEADD(month, 1, ${adjustedNow})) AND DATEPART(year, ${adjustedColumn}) = DATEPART(year, DATEADD(month, 1, ${adjustedNow}))`;

      case 'this-year':
        return `DATEPART(year, ${adjustedColumn}) = DATEPART(year, ${adjustedNow})`;
      case 'last-year':
        return `DATEPART(year, ${adjustedColumn}) = DATEPART(year, DATEADD(year, -1, ${adjustedNow}))`;
      case 'next-year':
        return `DATEPART(year, ${adjustedColumn}) = DATEPART(year, DATEADD(year, 1, ${adjustedNow}))`;

      default:
        return this.unsupportedOperator(dateOperator, columnRef, null);
    }
  }

  private convertRelativeDateOperator(columnRef: string, datepart: string, value: string, direction: number): string {
    const units = parseInt(value, 10);
    if (isNaN(units)) {
      this.logUnsupported(`Invalid value '${value}' for relative date operator`);
      return '';
    }

    const adjustedColumn = `DATEADD(hour, ${this.utcOffsetHours}, ${columnRef})`;
    const adjustedNow = `DATEADD(hour, ${this.utcOffsetHours}, GETUTCDATE())`;

    if (direction === -1) {
      // last-x: >= start of (current-units) AND < start of (current+1)
      const lowerBound = `DATEADD(${datepart}, DATEDIFF(${datepart}, 0, ${adjustedNow}) - ${units}, 0)`;
      const upperBound = `DATEADD(${datepart}, DATEDIFF(${datepart}, 0, ${adjustedNow}) + 1, 0)`;
      return `(${adjustedColumn} >= ${lowerBound} AND ${adjustedColumn} < ${upperBound})`;
    } else {
      // next-x: >= start of (current+1) AND < start of (current+units+1)
      const lowerBound = `DATEADD(${datepart}, DATEDIFF(${datepart}, 0, ${adjustedNow}) + 1, 0)`;
      const upperBound = `DATEADD(${datepart}, DATEDIFF(${datepart}, 0, ${adjustedNow}) + ${units + 1}, 0)`;
      return `(${adjustedColumn} >= ${lowerBound} AND ${adjustedColumn} < ${upperBound})`;
    }
  }

  private convertOlderThanOperator(columnRef: string, datepart: string, value: string): string {
    const units = parseInt(value, 10);
    if (isNaN(units)) {
      this.logUnsupported(`Invalid value '${value}' for older-than operator`);
      return '';
    }

    const adjustedColumn = `DATEADD(hour, ${this.utcOffsetHours}, ${columnRef})`;
    const adjustedNow = `DATEADD(hour, ${this.utcOffsetHours}, GETUTCDATE())`;
    const threshold = `DATEADD(${datepart}, DATEDIFF(${datepart}, 0, ${adjustedNow}) - ${units}, 0)`;
    return `${adjustedColumn} < ${threshold}`;
  }

  // #endregion

  // #region User Context Operators

  private convertUserTeamsOperator(columnRef: string, isEqual: boolean): string {
    const comparison = isEqual ? 'IN' : 'NOT IN';
    const userTeamsQuery = 'SELECT TeamId FROM TeamMembership WHERE SystemUserId = CURRENT_USER';
    this.logUnsupported('User teams operator - may require TeamMembership table access');
    return `${columnRef} ${comparison} (${userTeamsQuery})`;
  }

  // #endregion

  // #region List Operators

  private processInOperator(condition: Element, columnRef: string): string {
    let values = Array.from(condition.querySelectorAll(':scope > value')).map(v => v.textContent ?? '');

    if (values.length === 0) {
      const singleValue = condition.getAttribute('value');
      if (singleValue?.trim()) {
        values = singleValue.split(',').map(v => v.trim());
      }
    }

    values = values.filter(v => v.trim() !== '');

    if (values.length === 0) {
      this.debugLog.push('  IN operator has no values - skipping');
      return '';
    }

    const formattedValues = values.map(v => this.formatValue(v)).join(', ');
    return `${columnRef} IN (${formattedValues})`;
  }

  private processNotInOperator(condition: Element, columnRef: string): string {
    const inClause = this.processInOperator(condition, columnRef);
    if (!inClause) return '';
    return inClause.replace(' IN (', ' NOT IN (');
  }

  // #endregion

  // #region Value Formatting

  private formatValue(value: string | null): string {
    if (!value?.trim()) return 'NULL';

    // Integer
    if (/^-?\d+$/.test(value)) return value;

    // Boolean (Dataverse uses 0/1) - already handled by integer check

    // GUID
    if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)) {
      return `'${value}'`;
    }

    // DateTime (basic ISO format detection)
    if (/^\d{4}-\d{2}-\d{2}/.test(value)) {
      return `'${value}'`;
    }

    // Default: treat as string, escape single quotes
    const escapedValue = value.replace(/'/g, "''");
    return `'${escapedValue}'`;
  }

  // #endregion

  // #region Link-Entity Processing

  private processLinkEntityFilters(linkEntity: Element, baseTableAlias: string): string[] {
    const clauses: string[] = [];
    const linkEntityName = linkEntity.getAttribute('name') ?? 'unknown';
    const alias = linkEntity.getAttribute('alias') ?? linkEntityName;
    const linkType = linkEntity.getAttribute('link-type') ?? 'inner';
    const fromAttr = linkEntity.getAttribute('from');
    const toAttr = linkEntity.getAttribute('to');

    this.debugLog.push(`  Link-entity: ${linkEntityName} (alias: ${alias}, type: ${linkType})`);
    this.debugLog.push(`    Join: ${baseTableAlias}.${toAttr} = ${alias}.${fromAttr}`);

    // Process filters within this link-entity
    const linkFilters = Array.from(linkEntity.querySelectorAll(':scope > filter'));
    if (linkFilters.length > 0) {
      this.debugLog.push(`    Processing ${linkFilters.length} filter(s) in link-entity`);

      for (const filter of linkFilters) {
        const filterClause = this.processFilter(filter, alias);
        if (filterClause) {
          const existsClause = `EXISTS (SELECT 1 FROM ${linkEntityName} AS ${alias} WHERE ${alias}.${fromAttr} = ${baseTableAlias}.${toAttr} AND (${filterClause}))`;
          clauses.push(existsClause);
          this.debugLog.push(`    Generated EXISTS clause: ${existsClause}`);
        }
      }
    }

    // Process nested link-entities recursively
    const nestedLinkEntities = Array.from(linkEntity.querySelectorAll(':scope > link-entity'));
    if (nestedLinkEntities.length > 0) {
      this.debugLog.push(`    Found ${nestedLinkEntities.length} nested link-entity elements`);
      for (const nested of nestedLinkEntities) {
        const nestedClauses = this.processLinkEntityFilters(nested, alias);
        clauses.push(...nestedClauses);
      }
    }

    return clauses;
  }

  // #endregion

  // #region Helpers

  private unsupportedOperator(operatorValue: string, attribute: string | null, _value: string | null): string {
    const message = `Operator '${operatorValue}' for attribute '${attribute ?? ''}'`;
    this.logUnsupported(message);
    this.debugLog.push(`  UNSUPPORTED: ${message}`);
    return '';
  }

  private unsupportedUserContextOp(operatorValue: string, attribute: string | null): string {
    const reason = this.isImportMode ? 'Import mode' : 'FabricLink';
    const message = `Operator '${operatorValue}' for attribute '${attribute ?? ''}' - not supported in ${reason} (current user filters require DirectQuery)`;
    this.logUnsupported(message);
    this.debugLog.push(`  UNSUPPORTED (${reason.toUpperCase()}): ${message}`);
    return '';
  }

  private logUnsupported(feature: string): void {
    this.hasUnsupportedFeatures = true;
    if (!this.unsupportedFeatures.includes(feature)) {
      this.unsupportedFeatures.push(feature);
    }
  }

  private createResult(sqlClause: string, isFullySupported: boolean): ConversionResult {
    const lines: string[] = [
      'FetchXML Conversion Summary:',
      `  Fully Supported: ${isFullySupported}`,
    ];

    if (this.unsupportedFeatures.length > 0) {
      lines.push(`  Unsupported Features (${this.unsupportedFeatures.length}):`);
      for (const feature of this.unsupportedFeatures) {
        lines.push(`    - ${feature}`);
      }
    }

    if (sqlClause) {
      lines.push(`  Generated SQL: ${sqlClause}`);
    } else {
      lines.push('  No SQL generated');
    }

    return {
      sqlWhereClause: sqlClause,
      isFullySupported,
      unsupportedFeatures: [...this.unsupportedFeatures],
      debugLog: [...this.debugLog],
      summary: lines.join('\n'),
    };
  }

  /** Parses XML using DOMParser (browser-native, no XXE risk) */
  private parseXml(xml: string): Document {
    const parser = new DOMParser();
    const doc = parser.parseFromString(xml, 'application/xml');
    const parseError = doc.querySelector('parsererror');
    if (parseError) {
      throw new Error(`XML parse error: ${parseError.textContent}`);
    }
    return doc;
  }

  // #endregion

  // #region Static Debug Logging

  /** Logs detailed debugging information */
  static logConversionDebug(viewName: string, fetchXml: string, result: ConversionResult): void {
    try {
      const lines: string[] = [
        '='.repeat(80),
        'FetchXML to SQL Conversion Debug Log',
        `View: ${viewName}`,
        `Timestamp: ${new Date().toISOString()}`,
        '='.repeat(80),
        '',
        'INPUT FetchXML:',
        '-'.repeat(80),
        fetchXml,
        '',
        'CONVERSION RESULT:',
        '-'.repeat(80),
        result.summary,
        '',
      ];

      if (result.debugLog.length > 0) {
        lines.push('DEBUG LOG:', '-'.repeat(80), ...result.debugLog, '');
      }

      lines.push('='.repeat(80));

      logger.debug('FetchXmlToSqlConverter', lines.join('\n'));
    } catch (ex) {
      const msg = ex instanceof Error ? ex.message : String(ex);
      logger.error('FetchXmlToSqlConverter', `Failed to log debug: ${msg}`);
    }
  }

  // #endregion
}
