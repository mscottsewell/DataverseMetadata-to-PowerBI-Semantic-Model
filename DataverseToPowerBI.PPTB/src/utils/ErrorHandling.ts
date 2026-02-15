/**
 * ErrorHandling.ts
 * 
 * Error handling utilities and custom error types.
 */

import { logger } from './Logger';

/**
 * Base error class for application errors.
 */
export class AppError extends Error {
  constructor(
    message: string,
    public readonly code?: string,
    public readonly details?: any
  ) {
    super(message);
    this.name = 'AppError';
    
    // Log error
    logger.error('AppError', message, this);
  }
}

/**
 * Error thrown when Dataverse connection is not available.
 */
export class ConnectionError extends AppError {
  constructor(message = 'No Dataverse connection available') {
    super(message, 'CONNECTION_ERROR');
    this.name = 'ConnectionError';
  }
}

/**
 * Error thrown when Dataverse API calls fail.
 */
export class DataverseApiError extends AppError {
  constructor(message: string, details?: any) {
    super(message, 'DATAVERSE_API_ERROR', details);
    this.name = 'DataverseApiError';
  }
}

/**
 * Error thrown when file operations fail.
 */
export class FileSystemError extends AppError {
  constructor(message: string, details?: any) {
    super(message, 'FILESYSTEM_ERROR', details);
    this.name = 'FileSystemError';
  }
}

/**
 * Error thrown when validation fails.
 */
export class ValidationError extends AppError {
  constructor(message: string, details?: any) {
    super(message, 'VALIDATION_ERROR', details);
    this.name = 'ValidationError';
  }
}

/**
 * Error thrown when TMDL generation fails.
 */
export class TmdlGenerationError extends AppError {
  constructor(message: string, details?: any) {
    super(message, 'TMDL_GENERATION_ERROR', details);
    this.name = 'TmdlGenerationError';
  }
}

/**
 * Handles errors and returns user-friendly messages.
 */
export function handleError(error: unknown): string {
  if (error instanceof AppError) {
    return error.message;
  }
  
  if (error instanceof Error) {
    logger.error('UnhandledError', error.message, error);
    return error.message;
  }
  
  logger.error('UnknownError', String(error));
  return 'An unexpected error occurred';
}

/**
 * Wraps an async function with error handling.
 * Logs errors and returns a user-friendly message.
 */
export function withErrorHandling<T extends (...args: any[]) => Promise<any>>(
  fn: T,
  errorMessage = 'Operation failed'
): T {
  return (async (...args: any[]) => {
    try {
      return await fn(...args);
    } catch (error) {
      const message = handleError(error);
      logger.error('ErrorHandler', `${errorMessage}: ${message}`, error as Error);
      throw error;
    }
  }) as T;
}

/**
 * Safely executes a function and returns result or error.
 * Does not throw - returns error as value.
 */
export async function tryCatch<T>(
  fn: () => Promise<T>
): Promise<{ success: true; value: T } | { success: false; error: Error }> {
  try {
    const value = await fn();
    return { success: true, value };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error : new Error(String(error)),
    };
  }
}

/**
 * Retries an async operation with exponential backoff.
 */
export async function retry<T>(
  fn: () => Promise<T>,
  maxRetries = 3,
  baseDelay = 1000
): Promise<T> {
  let lastError: Error | unknown;
  
  for (let attempt = 0; attempt < maxRetries; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;
      
      if (attempt < maxRetries - 1) {
        const delay = baseDelay * Math.pow(2, attempt);
        logger.warning(
          'Retry',
          `Attempt ${attempt + 1} failed, retrying in ${delay}ms...`
        );
        await new Promise(resolve => setTimeout(resolve, delay));
      }
    }
  }
  
  throw lastError;
}
