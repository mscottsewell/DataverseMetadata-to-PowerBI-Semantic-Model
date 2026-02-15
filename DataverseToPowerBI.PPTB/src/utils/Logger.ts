/**
 * Logger.ts
 * 
 * TypeScript port of DebugLogger.cs
 * Thread-safe logging utility for debugging and diagnostics.
 */

/**
 * Log levels for categorizing messages.
 */
export enum LogLevel {
  Debug = 'DEBUG',
  Info = 'INFO',
  Warning = 'WARNING',
  Error = 'ERROR',
}

/**
 * Log entry structure.
 */
interface LogEntry {
  timestamp: string;
  level: LogLevel;
  category: string;
  message: string;
}

/**
 * Logger class for diagnostic output.
 * 
 * Provides thread-safe logging with timestamp, level, and category support.
 * In-memory log buffer can be exported for troubleshooting.
 */
export class Logger {
  private static logs: LogEntry[] = [];
  private static maxLogs = 1000; // Keep last 1000 entries
  private static enabled = true;

  /**
   * Enables or disables logging.
   */
  static setEnabled(enabled: boolean): void {
    Logger.enabled = enabled;
  }

  /**
   * Sets the maximum number of log entries to keep in memory.
   */
  static setMaxLogs(max: number): void {
    Logger.maxLogs = max;
    
    // Trim if necessary
    if (Logger.logs.length > max) {
      Logger.logs = Logger.logs.slice(-max);
    }
  }

  /**
   * Logs a debug message.
   */
  static debug(category: string, message: string): void {
    Logger.log(LogLevel.Debug, category, message);
  }

  /**
   * Logs an informational message.
   */
  static info(category: string, message: string): void {
    Logger.log(LogLevel.Info, category, message);
  }

  /**
   * Logs a warning message.
   */
  static warning(category: string, message: string): void {
    Logger.log(LogLevel.Warning, category, message);
  }

  /**
   * Logs an error message.
   */
  static error(category: string, message: string, error?: Error): void {
    let fullMessage = message;
    if (error) {
      fullMessage += `\nError: ${error.message}\nStack: ${error.stack}`;
    }
    Logger.log(LogLevel.Error, category, fullMessage);
  }

  /**
   * Core logging method.
   */
  private static log(level: LogLevel, category: string, message: string): void {
    if (!Logger.enabled) {
      return;
    }

    const entry: LogEntry = {
      timestamp: new Date().toISOString(),
      level,
      category,
      message,
    };

    // Add to in-memory buffer
    Logger.logs.push(entry);
    
    // Trim if exceeds max
    if (Logger.logs.length > Logger.maxLogs) {
      Logger.logs.shift(); // Remove oldest
    }

    // Output to console with appropriate method
    const formattedMessage = `[${entry.timestamp}] [${level}] [${category}] ${message}`;
    
    switch (level) {
      case LogLevel.Debug:
        console.debug(formattedMessage);
        break;
      case LogLevel.Info:
        console.info(formattedMessage);
        break;
      case LogLevel.Warning:
        console.warn(formattedMessage);
        break;
      case LogLevel.Error:
        console.error(formattedMessage);
        break;
    }
  }

  /**
   * Gets all log entries.
   */
  static getLogs(): LogEntry[] {
    return [...Logger.logs]; // Return copy
  }

  /**
   * Gets log entries filtered by level.
   */
  static getLogsByLevel(level: LogLevel): LogEntry[] {
    return Logger.logs.filter(entry => entry.level === level);
  }

  /**
   * Gets log entries filtered by category.
   */
  static getLogsByCategory(category: string): LogEntry[] {
    return Logger.logs.filter(entry => entry.category === category);
  }

  /**
   * Clears all log entries.
   */
  static clear(): void {
    Logger.logs = [];
  }

  /**
   * Exports logs as formatted text.
   */
  static export(): string {
    return Logger.logs
      .map(entry => `[${entry.timestamp}] [${entry.level}] [${entry.category}] ${entry.message}`)
      .join('\n');
  }

  /**
   * Exports logs as JSON.
   */
  static exportJson(): string {
    return JSON.stringify(Logger.logs, null, 2);
  }

  /**
   * Downloads logs as a text file.
   * Uses browser download mechanism.
   */
  static downloadLogs(filename = 'dataverse-to-powerbi-logs.txt'): void {
    const content = Logger.export();
    const blob = new Blob([content], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    
    URL.revokeObjectURL(url);
  }
}

// Export singleton instance for convenience
export const logger = Logger;
