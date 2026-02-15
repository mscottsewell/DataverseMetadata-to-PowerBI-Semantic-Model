/**
 * Validation.ts
 * 
 * Validation utilities for user input and data integrity.
 */

import { ValidationError } from './ErrorHandling';
import {
  MAX_TABLE_NAME_LENGTH,
  MAX_ATTRIBUTE_NAME_LENGTH,
  MAX_DISPLAY_NAME_LENGTH,
} from '../types/Constants';

/**
 * Validates that a string is not empty.
 */
export function validateNotEmpty(value: string | undefined | null, fieldName: string): void {
  if (!value || value.trim().length === 0) {
    throw new ValidationError(`${fieldName} cannot be empty`);
  }
}

/**
 * Validates that a string does not exceed maximum length.
 */
export function validateMaxLength(
  value: string,
  maxLength: number,
  fieldName: string
): void {
  if (value.length > maxLength) {
    throw new ValidationError(
      `${fieldName} cannot exceed ${maxLength} characters (currently ${value.length})`
    );
  }
}

/**
 * Validates a table name.
 */
export function validateTableName(name: string): void {
  validateNotEmpty(name, 'Table name');
  validateMaxLength(name, MAX_TABLE_NAME_LENGTH, 'Table name');
  
  // Must start with letter or underscore
  if (!/^[a-zA-Z_]/.test(name)) {
    throw new ValidationError('Table name must start with a letter or underscore');
  }
  
  // Must contain only alphanumeric and underscores
  if (!/^[a-zA-Z0-9_]+$/.test(name)) {
    throw new ValidationError('Table name can only contain letters, numbers, and underscores');
  }
}

/**
 * Validates an attribute name.
 */
export function validateAttributeName(name: string): void {
  validateNotEmpty(name, 'Attribute name');
  validateMaxLength(name, MAX_ATTRIBUTE_NAME_LENGTH, 'Attribute name');
  
  // Must start with letter or underscore
  if (!/^[a-zA-Z_]/.test(name)) {
    throw new ValidationError('Attribute name must start with a letter or underscore');
  }
  
  // Must contain only alphanumeric and underscores
  if (!/^[a-zA-Z0-9_]+$/.test(name)) {
    throw new ValidationError('Attribute name can only contain letters, numbers, and underscores');
  }
}

/**
 * Validates a display name.
 */
export function validateDisplayName(name: string): void {
  validateNotEmpty(name, 'Display name');
  validateMaxLength(name, MAX_DISPLAY_NAME_LENGTH, 'Display name');
}

/**
 * Validates a GUID string.
 */
export function validateGuid(value: string, fieldName: string): void {
  const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  
  if (!guidPattern.test(value)) {
    throw new ValidationError(`${fieldName} is not a valid GUID`);
  }
}

/**
 * Validates a URL string.
 */
export function validateUrl(value: string, fieldName: string): void {
  try {
    new URL(value);
  } catch {
    throw new ValidationError(`${fieldName} is not a valid URL`);
  }
}

/**
 * Validates that an array is not empty.
 */
export function validateArrayNotEmpty<T>(
  value: T[] | undefined | null,
  fieldName: string
): void {
  if (!value || value.length === 0) {
    throw new ValidationError(`${fieldName} must contain at least one item`);
  }
}

/**
 * Validates that a value is one of allowed values.
 */
export function validateEnum<T>(
  value: T,
  allowedValues: readonly T[],
  fieldName: string
): void {
  if (!allowedValues.includes(value)) {
    throw new ValidationError(
      `${fieldName} must be one of: ${allowedValues.join(', ')}`
    );
  }
}

/**
 * Validates a year value.
 */
export function validateYear(value: number, fieldName: string): void {
  const currentYear = new Date().getFullYear();
  
  if (!Number.isInteger(value)) {
    throw new ValidationError(`${fieldName} must be an integer`);
  }
  
  if (value < 1900 || value > currentYear + 100) {
    throw new ValidationError(
      `${fieldName} must be between 1900 and ${currentYear + 100}`
    );
  }
}

/**
 * Validates a date range (start year <= end year).
 */
export function validateYearRange(startYear: number, endYear: number): void {
  validateYear(startYear, 'Start year');
  validateYear(endYear, 'End year');
  
  if (startYear > endYear) {
    throw new ValidationError('Start year must be less than or equal to end year');
  }
}

/**
 * Checks if a string contains special characters that need quoting in TMDL.
 */
export function needsQuotingInTmdl(name: string): boolean {
  // Quote if contains spaces, special chars, or starts with number
  return (
    /\s/.test(name) || // Contains whitespace
    /[^a-zA-Z0-9_]/.test(name) || // Contains special chars
    /^[0-9]/.test(name) // Starts with number
  );
}

/**
 * Quotes a name for TMDL if needed.
 */
export function quoteTmdlName(name: string): string {
  if (needsQuotingInTmdl(name)) {
    // Escape single quotes by doubling them
    const escaped = name.replace(/'/g, "''");
    return `'${escaped}'`;
  }
  return name;
}

/**
 * Validates a configuration object has required fields.
 */
export function validateConfiguration(config: any): void {
  validateNotEmpty(config.projectName, 'Project name');
  validateArrayNotEmpty(config.selectedTables, 'Selected tables');
  
  if (config.dateTableConfig) {
    validateYearRange(
      config.dateTableConfig.startYear,
      config.dateTableConfig.endYear
    );
  }
}
