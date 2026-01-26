using System;
using System.IO;

namespace DataverseMetadataExtractor.Services
{
    public static class DebugLogger
    {
        private static readonly string LogPath;
        private static readonly object _lock = new object();

        static DebugLogger()
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DataverseMetadataExtractor"
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
