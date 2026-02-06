// ===================================================================================
// DebugLogger.cs - Thread-Safe Diagnostic Logging for XrmToolBox Plugin
// ===================================================================================
//
// PURPOSE:
// Provides a simple, thread-safe file-based logging utility for debugging and
// troubleshooting the XrmToolBox plugin during development and support scenarios.
//
// LOG FILE LOCATION:
//   %APPDATA%\DataverseToPowerBI\debug_log.txt
//
// FEATURES:
// - Thread-safe logging using lock synchronization
// - Millisecond-precision timestamps for performance analysis
// - Log sections for grouping related entries (e.g., FetchXML conversion)
// - Auto-clear on startup to prevent log file growth
// - Silent failure on logging errors (won't crash the application)
//
// USAGE:
//   DebugLogger.Log("Processing table: account");
//   DebugLogger.LogSection("FetchXML", fetchXmlContent);
//   string path = DebugLogger.GetLogPath();
//
// NOTE:
// This is intentionally simple and lightweight. For production scenarios,
// consider integrating with XrmToolBox's built-in tracing/logging.
//
// ===================================================================================

using System;
using System.IO;

namespace DataverseToPowerBI.XrmToolBox.Services
{
    public static class DebugLogger
    {
        private static readonly string LogPath;
        private static readonly object _lock = new object();

        static DebugLogger()
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DataverseToPowerBI"
            );
            Directory.CreateDirectory(appFolder);
            LogPath = Path.Combine(appFolder, "debug_log.txt");
            
            // Clear log on startup
            File.WriteAllText(LogPath, $"=== Debug Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    File.AppendAllText(LogPath, $"[{timestamp}] {message}\n");
                }
                catch
                {
                    // Ignore logging errors
                }
            }
        }

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
                    // Ignore logging errors
                }
            }
        }

        public static string GetLogPath()
        {
            return LogPath;
        }
    }
}
