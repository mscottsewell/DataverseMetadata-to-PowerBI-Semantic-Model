// =============================================================================
// DebugLogger.cs - Debug Logging Utility
// =============================================================================
// Purpose: Provides thread-safe debug logging capabilities for the application.
//
// The logger writes to a text file in the application data folder, making it
// easy to troubleshoot issues without needing a debugger attached.
//
// Log Location: %APPDATA%\DataverseToPowerBI.Configurator\debug_log.txt
//
// Features:
//   - Thread-safe logging with lock synchronization
//   - Timestamped entries for easy debugging
//   - Section logging for grouping related log entries
//   - Log file is cleared on application startup
//
// Note: Logging errors are silently ignored to prevent logging issues
// from affecting the main application functionality.
// =============================================================================

using System;
using System.IO;

namespace DataverseToPowerBI.Configurator.Services
{
    /// <summary>
    /// Static utility class for debug logging.
    /// Provides thread-safe logging to a text file for troubleshooting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This logger is designed for development and troubleshooting purposes.
    /// All log entries are written to a file in the user's application data folder.
    /// </para>
    /// <para>
    /// Usage example:
    /// </para>
    /// <code>
    /// DebugLogger.Log("Loading configuration...");
    /// DebugLogger.LogSection("Configuration Details", $"Tables: {count}");
    /// </code>
    /// </remarks>
    public static class DebugLogger
    {
        #region Private Fields

        /// <summary>
        /// Full path to the debug log file.
        /// Located in %APPDATA%\DataverseToPowerBI.Configurator\debug_log.txt
        /// </summary>
        private static readonly string LogPath;

        /// <summary>
        /// Lock object for thread-safe file access.
        /// Prevents concurrent writes from corrupting the log file.
        /// </summary>
        private static readonly object _lock = new object();

        #endregion

        #region Constructor

        /// <summary>
        /// Static constructor - initializes the log file on first use.
        /// </summary>
        /// <remarks>
        /// Creates the application folder if it doesn't exist and
        /// clears the log file with a startup header.
        /// This ensures each application session starts with a fresh log.
        /// </remarks>
        static DebugLogger()
        {
            // Build path to application data folder
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DataverseToPowerBI.Configurator"
            );
            
            // Ensure the folder exists
            Directory.CreateDirectory(appFolder);
            
            // Set the log file path
            LogPath = Path.Combine(appFolder, "debug_log.txt");
            
            // Clear log on startup with a header showing the session start time
            File.WriteAllText(LogPath, $"=== Debug Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Logs a simple message with timestamp.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <remarks>
        /// Format: [HH:mm:ss.fff] message
        /// </remarks>
        /// <example>
        /// <code>
        /// DebugLogger.Log("Starting connection to Dataverse...");
        /// DebugLogger.Log($"Loaded {tables.Count} tables");
        /// </code>
        /// </example>
        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    // Format timestamp with milliseconds for precise timing
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    File.AppendAllText(LogPath, $"[{timestamp}] {message}\n");
                }
                catch
                {
                    // Silently ignore logging errors to prevent affecting main app
                }
            }
        }

        /// <summary>
        /// Logs a section with a title and multi-line content.
        /// Useful for logging detailed information in a formatted block.
        /// </summary>
        /// <param name="title">The section title (displayed in === markers).</param>
        /// <param name="content">The content to log (can be multi-line).</param>
        /// <remarks>
        /// <para>
        /// Format:
        /// </para>
        /// <code>
        /// [HH:mm:ss.fff] === title ===
        /// content
        /// </code>
        /// </remarks>
        /// <example>
        /// <code>
        /// DebugLogger.LogSection("Configuration Loaded", 
        ///     $"Environment: {url}\nTables: {count}\nProject: {name}");
        /// </code>
        /// </example>
        public static void LogSection(string title, string content)
        {
            lock (_lock)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    File.AppendAllText(LogPath, $"\n[{timestamp}] === {title} ===\n{content}\n\n");
                }
                catch
                {
                    // Silently ignore logging errors to prevent affecting main app
                }
            }
        }

        /// <summary>
        /// Gets the full path to the log file.
        /// Useful for displaying to users or opening in a text editor.
        /// </summary>
        /// <returns>The absolute path to the debug log file.</returns>
        /// <example>
        /// <code>
        /// var logPath = DebugLogger.GetLogPath();
        /// Process.Start("notepad.exe", logPath);
        /// </code>
        /// </example>
        public static string GetLogPath()
        {
            return LogPath;
        }

        #endregion
    }
}
