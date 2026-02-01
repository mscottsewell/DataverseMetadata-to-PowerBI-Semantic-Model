// =============================================================================
// SettingsManager.cs - Configuration and Cache Management
// =============================================================================
// Purpose: Manages application settings, configurations, and metadata caching.
//
// This service handles:
//   - Multiple named configurations (for different projects/environments)
//   - Automatic saving/loading of settings from JSON files
//   - Metadata caching to reduce API calls to Dataverse
//   - Cache lifecycle management (creation, cleanup, diagnostics)
//
// Data Storage:
//   - Settings: %APPDATA%\DataverseToPowerBI.Configurator\.dataverse_configurations.json
//   - Cache: %APPDATA%\DataverseToPowerBI.Configurator\.dataverse_metadata_cache_{name}.json
//
// Key Features:
//   - Named configurations for multiple projects
//   - Automatic last-used configuration tracking
//   - Per-configuration metadata caching
//   - Orphaned cache file cleanup
// =============================================================================

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using DataverseToPowerBI.Configurator.Models;

namespace DataverseToPowerBI.Configurator.Services
{
    /// <summary>
    /// Manages application settings, configurations, and metadata caching.
    /// Provides persistence for user preferences and Dataverse metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The SettingsManager supports multiple named configurations, allowing users
    /// to switch between different Dataverse environments or projects without
    /// losing their settings.
    /// </para>
    /// <para>
    /// Each configuration has its own metadata cache, ensuring that cached data
    /// matches the environment it was retrieved from.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var manager = new SettingsManager();
    /// var settings = manager.LoadSettings();
    /// settings.ProjectName = "My Project";
    /// manager.SaveSettings(settings);
    /// </code>
    /// </example>
    public class SettingsManager
    {
        #region Constants and Fields

        /// <summary>
        /// Name of the main configurations file.
        /// Hidden file (dot prefix) to reduce clutter in the folder.
        /// </summary>
        private const string ConfigurationsFileName = ".dataverse_configurations.json";

        /// <summary>
        /// Full path to the configurations JSON file.
        /// </summary>
        private readonly string _configurationsPath;

        /// <summary>
        /// Path to the application data folder.
        /// All settings and cache files are stored here.
        /// </summary>
        private readonly string _appFolder;

        /// <summary>
        /// In-memory representation of the configurations file.
        /// Cached to avoid repeated file I/O.
        /// </summary>
        private ConfigurationsFile? _configurationsFile;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the SettingsManager class.
        /// Creates the application data folder if it doesn't exist.
        /// </summary>
        public SettingsManager()
        {
            // Use the standard Windows AppData folder for settings
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appFolder = Path.Combine(appDataPath, "DataverseToPowerBI.Configurator");
            
            // Ensure the folder exists
            Directory.CreateDirectory(_appFolder);
            
            // Build the full path to the configurations file
            _configurationsPath = Path.Combine(_appFolder, ConfigurationsFileName);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Generates the file path for a configuration's metadata cache.
        /// </summary>
        /// <param name="configurationName">The configuration name.</param>
        /// <returns>Full path to the cache file for this configuration.</returns>
        /// <remarks>
        /// The configuration name is sanitized to remove invalid filename characters.
        /// Format: .dataverse_metadata_cache_{sanitized_name}.json
        /// </remarks>
        private string GetCachePath(string configurationName)
        {
            // Sanitize configuration name for use in filename
            // Replace invalid characters with underscores
            var sanitized = string.Join("_", configurationName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_appFolder, $".dataverse_metadata_cache_{sanitized}.json");
        }

        /// <summary>
        /// Loads the configurations file from disk.
        /// </summary>
        /// <returns>
        /// The deserialized ConfigurationsFile, or a new empty one if the file
        /// doesn't exist or can't be read.
        /// </returns>
        private ConfigurationsFile LoadConfigurationsFile()
        {
            try
            {
                if (File.Exists(_configurationsPath))
                {
                    var json = File.ReadAllText(_configurationsPath);
                    return JsonConvert.DeserializeObject<ConfigurationsFile>(json) ?? new ConfigurationsFile();
                }
            }
            catch
            {
                // If loading fails, return a new empty file
                // This handles corrupted files gracefully
            }
            
            return new ConfigurationsFile();
        }

        /// <summary>
        /// Saves the configurations file to disk.
        /// </summary>
        /// <param name="configurationsFile">The configurations to save.</param>
        /// <exception cref="Exception">Thrown if the file cannot be saved.</exception>
        /// <remarks>
        /// Includes debug logging to help troubleshoot settings persistence issues.
        /// The file is written with indented JSON for human readability.
        /// </remarks>
        private void SaveConfigurationsFile(ConfigurationsFile configurationsFile)
        {
            try
            {
                // DEBUG: Log what's being saved for troubleshooting
                var currentConfig = configurationsFile.Configurations
                    .FirstOrDefault(c => c.Name == configurationsFile.LastUsedConfigurationName);
                var attrInfoCount = currentConfig?.Settings?.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
                DebugLogger.LogSection("SaveConfigurationsFile - Before JSON Write",
                    $"Config: {configurationsFile.LastUsedConfigurationName}\n" +
                    $"AttributeDisplayInfo: {attrInfoCount} attrs across {currentConfig?.Settings?.AttributeDisplayInfo?.Count ?? 0} tables\n" +
                    $"File: {_configurationsPath}");
                
                // Serialize with indentation for readability
                var json = JsonConvert.SerializeObject(configurationsFile, Formatting.Indented);
                File.WriteAllText(_configurationsPath, json);
                _configurationsFile = configurationsFile;
                
                // DEBUG: Verify what was actually written (round-trip test)
                var verifyJson = File.ReadAllText(_configurationsPath);
                var verifyFile = JsonConvert.DeserializeObject<ConfigurationsFile>(verifyJson);
                var verifyConfig = verifyFile?.Configurations
                    .FirstOrDefault(c => c.Name == configurationsFile.LastUsedConfigurationName);
                var verifyAttrCount = verifyConfig?.Settings?.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
                DebugLogger.LogSection("SaveConfigurationsFile - After JSON Write (Verified)",
                    $"AttributeDisplayInfo: {verifyAttrCount} attrs across {verifyConfig?.Settings?.AttributeDisplayInfo?.Count ?? 0} tables");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save configurations: {ex.Message}", ex);
            }
        }

        #endregion

        #region Settings Operations

        /// <summary>
        /// Loads the current/last-used configuration settings.
        /// </summary>
        /// <returns>
        /// The AppSettings for the last-used configuration.
        /// Creates a default configuration if none exist.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Selection priority:
        /// </para>
        /// <list type="number">
        ///   <item>The configuration named in LastUsedConfigurationName</item>
        ///   <item>The most recently used configuration (by LastUsed timestamp)</item>
        ///   <item>A newly created "Default" configuration</item>
        /// </list>
        /// </remarks>
        public AppSettings LoadSettings()
        {
            _configurationsFile = LoadConfigurationsFile();
            
            // Create a default configuration if none exist
            if (_configurationsFile.Configurations.Count == 0)
            {
                var defaultConfig = new ConfigurationEntry
                {
                    Name = "Default",
                    LastUsed = DateTime.Now,
                    Settings = new AppSettings()
                };
                _configurationsFile.Configurations.Add(defaultConfig);
                _configurationsFile.LastUsedConfigurationName = "Default";
                SaveConfigurationsFile(_configurationsFile);
                return defaultConfig.Settings;
            }

            // Find the last used configuration
            var lastUsed = _configurationsFile.Configurations
                .FirstOrDefault(c => c.Name == _configurationsFile.LastUsedConfigurationName)
                ?? _configurationsFile.Configurations
                    .OrderByDescending(c => c.LastUsed)
                    .First();

            return lastUsed.Settings;
        }

        /// <summary>
        /// Saves settings to the current configuration.
        /// </summary>
        /// <param name="settings">The settings to save.</param>
        /// <remarks>
        /// Updates the LastUsed timestamp of the current configuration.
        /// If no current configuration exists, creates a new "Default" one.
        /// </remarks>
        public void SaveSettings(AppSettings settings)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            // Find the current configuration
            var currentConfig = _configurationsFile.Configurations
                .FirstOrDefault(c => c.Name == _configurationsFile.LastUsedConfigurationName);

            if (currentConfig != null)
            {
                // Update existing configuration
                currentConfig.Settings = settings;
                currentConfig.LastUsed = DateTime.Now;
            }
            else
            {
                // Create a new configuration if current one doesn't exist
                var newConfig = new ConfigurationEntry
                {
                    Name = "Default",
                    LastUsed = DateTime.Now,
                    Settings = settings
                };
                _configurationsFile.Configurations.Add(newConfig);
                _configurationsFile.LastUsedConfigurationName = "Default";
            }

            SaveConfigurationsFile(_configurationsFile);
        }

        #endregion

        #region Configuration Management

        /// <summary>
        /// Gets the name of the currently active configuration.
        /// </summary>
        /// <returns>The current configuration name, or "Default" if not set.</returns>
        public string GetCurrentConfigurationName()
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }
            return _configurationsFile.LastUsedConfigurationName ?? "Default";
        }

        /// <summary>
        /// Gets a list of all configuration names.
        /// </summary>
        /// <returns>List of configuration names.</returns>
        public List<string> GetConfigurationNames()
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }
            return _configurationsFile.Configurations.Select(c => c.Name).ToList();
        }

        /// <summary>
        /// Switches to a different configuration.
        /// </summary>
        /// <param name="configurationName">Name of the configuration to switch to.</param>
        /// <exception cref="Exception">Thrown if the configuration doesn't exist.</exception>
        public void SwitchToConfiguration(string configurationName)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            var config = _configurationsFile.Configurations.FirstOrDefault(c => c.Name == configurationName);
            if (config == null)
            {
                throw new Exception($"Configuration '{configurationName}' not found.");
            }

            // Update timestamp and set as current
            config.LastUsed = DateTime.Now;
            _configurationsFile.LastUsedConfigurationName = configurationName;
            SaveConfigurationsFile(_configurationsFile);
        }

        /// <summary>
        /// Gets a specific configuration's settings by name.
        /// </summary>
        /// <param name="configurationName">Name of the configuration to retrieve.</param>
        /// <returns>The AppSettings for the specified configuration.</returns>
        /// <exception cref="Exception">Thrown if the configuration doesn't exist.</exception>
        public AppSettings GetConfiguration(string configurationName)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            var config = _configurationsFile.Configurations.FirstOrDefault(c => c.Name == configurationName);
            if (config == null)
            {
                throw new Exception($"Configuration '{configurationName}' not found.");
            }

            return config.Settings;
        }

        /// <summary>
        /// Creates a new named configuration.
        /// </summary>
        /// <param name="configurationName">Name for the new configuration.</param>
        /// <param name="settings">Optional initial settings (defaults to empty AppSettings).</param>
        /// <exception cref="Exception">Thrown if a configuration with this name already exists.</exception>
        public void CreateNewConfiguration(string configurationName, AppSettings? settings = null)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            // Ensure name is unique
            if (_configurationsFile.Configurations.Any(c => c.Name == configurationName))
            {
                throw new Exception($"Configuration '{configurationName}' already exists.");
            }

            // Create and add the new configuration
            var newConfig = new ConfigurationEntry
            {
                Name = configurationName,
                LastUsed = DateTime.Now,
                Settings = settings ?? new AppSettings()
            };

            _configurationsFile.Configurations.Add(newConfig);
            _configurationsFile.LastUsedConfigurationName = configurationName;
            SaveConfigurationsFile(_configurationsFile);
        }

        /// <summary>
        /// Renames an existing configuration.
        /// </summary>
        /// <param name="oldName">Current name of the configuration.</param>
        /// <param name="newName">New name for the configuration.</param>
        /// <exception cref="Exception">
        /// Thrown if the old name doesn't exist or the new name is already taken.
        /// </exception>
        public void RenameConfiguration(string oldName, string newName)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            var config = _configurationsFile.Configurations.FirstOrDefault(c => c.Name == oldName);
            if (config == null)
            {
                throw new Exception($"Configuration '{oldName}' not found.");
            }

            if (_configurationsFile.Configurations.Any(c => c.Name == newName))
            {
                throw new Exception($"Configuration '{newName}' already exists.");
            }

            // Rename the configuration
            config.Name = newName;
            
            // Update LastUsedConfigurationName if this was the current configuration
            if (_configurationsFile.LastUsedConfigurationName == oldName)
            {
                _configurationsFile.LastUsedConfigurationName = newName;
            }

            SaveConfigurationsFile(_configurationsFile);
        }

        /// <summary>
        /// Deletes a configuration.
        /// </summary>
        /// <param name="configurationName">Name of the configuration to delete.</param>
        /// <exception cref="Exception">
        /// Thrown if the configuration doesn't exist or is the last remaining configuration.
        /// </exception>
        public void DeleteConfiguration(string configurationName)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            var config = _configurationsFile.Configurations.FirstOrDefault(c => c.Name == configurationName);
            if (config == null)
            {
                throw new Exception($"Configuration '{configurationName}' not found.");
            }

            // Prevent deleting the last configuration
            if (_configurationsFile.Configurations.Count == 1)
            {
                throw new Exception("Cannot delete the last configuration.");
            }

            _configurationsFile.Configurations.Remove(config);

            // If we deleted the current configuration, switch to the most recent one
            if (_configurationsFile.LastUsedConfigurationName == configurationName)
            {
                var mostRecent = _configurationsFile.Configurations
                    .OrderByDescending(c => c.LastUsed)
                    .First();
                _configurationsFile.LastUsedConfigurationName = mostRecent.Name;
            }

            SaveConfigurationsFile(_configurationsFile);
        }

        /// <summary>
        /// Gets the path to the settings storage folder.
        /// </summary>
        /// <returns>The full path to the application data folder.</returns>
        public string GetSettingsFolderPath()
        {
            return _appFolder;
        }

        /// <summary>
        /// Finds the most recently used configuration for a specific Dataverse environment.
        /// </summary>
        /// <param name="environmentUrl">The Dataverse environment URL to match.</param>
        /// <returns>
        /// The name of the most recent matching configuration, or null if none found.
        /// </returns>
        /// <remarks>
        /// Useful for automatically selecting an appropriate configuration when
        /// connecting to an environment that was previously used.
        /// </remarks>
        public string? GetMostRecentConfigurationForEnvironment(string environmentUrl)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            // Find configurations matching this environment, ordered by recency
            var matchingConfigs = _configurationsFile.Configurations
                .Where(c => c.Settings.LastEnvironmentUrl?.Equals(environmentUrl, StringComparison.OrdinalIgnoreCase) == true)
                .OrderByDescending(c => c.LastUsed)
                .ToList();

            return matchingConfigs.FirstOrDefault()?.Name;
        }

        #endregion

        #region Cache Operations

        /// <summary>
        /// Loads the metadata cache for a configuration.
        /// </summary>
        /// <param name="configurationName">
        /// The configuration to load cache for. Defaults to current configuration.
        /// </param>
        /// <returns>The cached metadata, or null if no cache exists.</returns>
        public MetadataCache? LoadCache(string? configurationName = null)
        {
            try
            {
                var cachePath = GetCachePath(configurationName ?? GetCurrentConfigurationName());
                if (File.Exists(cachePath))
                {
                    var json = File.ReadAllText(cachePath);
                    return JsonConvert.DeserializeObject<MetadataCache>(json);
                }
            }
            catch
            {
                // If loading fails, return null (cache miss)
            }
            
            return null;
        }

        /// <summary>
        /// Saves metadata cache for a configuration.
        /// </summary>
        /// <param name="cache">The cache data to save.</param>
        /// <param name="configurationName">
        /// The configuration to save cache for. Defaults to current configuration.
        /// </param>
        /// <exception cref="Exception">Thrown if the cache cannot be saved.</exception>
        public void SaveCache(MetadataCache cache, string? configurationName = null)
        {
            try
            {
                var cachePath = GetCachePath(configurationName ?? GetCurrentConfigurationName());
                var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                File.WriteAllText(cachePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save cache: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clears (deletes) the cache file for a configuration.
        /// </summary>
        /// <param name="configurationName">
        /// The configuration to clear cache for. Defaults to current configuration.
        /// </param>
        public void ClearCache(string? configurationName = null)
        {
            try
            {
                var cachePath = GetCachePath(configurationName ?? GetCurrentConfigurationName());
                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }
            }
            catch
            {
                // Ignore errors when clearing cache - not critical
            }
        }

        /// <summary>
        /// Removes cache files for configurations that no longer exist.
        /// </summary>
        /// <returns>The number of orphaned cache files removed.</returns>
        /// <remarks>
        /// Call this periodically to clean up disk space from deleted configurations.
        /// The method compares existing cache files against the configuration list.
        /// </remarks>
        public int CleanupOrphanedCacheFiles()
        {
            try
            {
                if (_configurationsFile == null)
                {
                    _configurationsFile = LoadConfigurationsFile();
                }

                // Build set of valid configuration names
                var validConfigNames = _configurationsFile.Configurations
                    .Select(c => c.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Find all cache files
                var cacheFiles = Directory.GetFiles(_appFolder, ".dataverse_metadata_cache_*.json");
                int removedCount = 0;

                foreach (var cacheFile in cacheFiles)
                {
                    var fileName = Path.GetFileName(cacheFile);
                    
                    // Extract config name from filename
                    // Format: .dataverse_metadata_cache_{configname}.json
                    var configName = fileName
                        .Replace(".dataverse_metadata_cache_", "")
                        .Replace(".json", "")
                        .Replace("_", " ");  // Unsanitize the name

                    // Delete if no matching configuration exists
                    if (!validConfigNames.Contains(configName))
                    {
                        DebugLogger.Log($"Cleaning up orphaned cache file: {fileName} (config '{configName}' not found)");
                        File.Delete(cacheFile);
                        removedCount++;
                    }
                }

                return removedCount;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Error cleaning up orphaned cache files: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Generates a diagnostic report about settings storage.
        /// </summary>
        /// <returns>
        /// A formatted string containing information about all configurations,
        /// cache files, and storage locations.
        /// </returns>
        /// <remarks>
        /// Useful for troubleshooting settings and cache issues.
        /// Includes information about orphaned cache files.
        /// </remarks>
        public string GetSettingsDiagnostics()
        {
            try
            {
                if (_configurationsFile == null)
                {
                    _configurationsFile = LoadConfigurationsFile();
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== Settings Storage Diagnostics ===");
                sb.AppendLine();
                sb.AppendLine($"Settings Folder: {_appFolder}");
                sb.AppendLine($"Configurations File: {_configurationsPath}");
                sb.AppendLine($"File Exists: {File.Exists(_configurationsPath)}");
                sb.AppendLine();
                sb.AppendLine($"Total Configurations: {_configurationsFile.Configurations.Count}");
                sb.AppendLine($"Last Used Configuration: {_configurationsFile.LastUsedConfigurationName}");
                sb.AppendLine();

                // List each configuration with details
                foreach (var config in _configurationsFile.Configurations)
                {
                    sb.AppendLine($"Configuration: {config.Name}");
                    sb.AppendLine($"  Last Used: {config.LastUsed:g}");
                    sb.AppendLine($"  Environment URL: {config.Settings.LastEnvironmentUrl ?? "(not set)"}");
                    sb.AppendLine($"  Solution: {config.Settings.LastSolution ?? "(not set)"}");
                    sb.AppendLine($"  Project Name: {config.Settings.ProjectName ?? "(not set)"}");
                    sb.AppendLine($"  Output Folder: {config.Settings.OutputFolder ?? "(not set)"}");
                    sb.AppendLine($"  Selected Tables: {config.Settings.SelectedTables.Count}");
                    
                    // Cache file info
                    var cachePath = GetCachePath(config.Name);
                    sb.AppendLine($"  Cache File: {Path.GetFileName(cachePath)}");
                    sb.AppendLine($"  Cache Exists: {File.Exists(cachePath)}");
                    if (File.Exists(cachePath))
                    {
                        var fileInfo = new FileInfo(cachePath);
                        sb.AppendLine($"  Cache Size: {fileInfo.Length:N0} bytes");
                        sb.AppendLine($"  Cache Modified: {fileInfo.LastWriteTime:g}");
                    }
                    sb.AppendLine();
                }

                // Check for orphaned cache files
                var cacheFiles = Directory.GetFiles(_appFolder, ".dataverse_metadata_cache_*.json");
                var validConfigNames = _configurationsFile.Configurations
                    .Select(c => c.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var orphanedFiles = new List<string>();
                foreach (var cacheFile in cacheFiles)
                {
                    var fileName = Path.GetFileName(cacheFile);
                    var configName = fileName
                        .Replace(".dataverse_metadata_cache_", "")
                        .Replace(".json", "")
                        .Replace("_", " ");

                    if (!validConfigNames.Contains(configName))
                    {
                        orphanedFiles.Add(fileName);
                    }
                }

                // Report orphaned files or confirm none
                if (orphanedFiles.Any())
                {
                    sb.AppendLine("⚠️ Orphaned Cache Files (no matching configuration):");
                    foreach (var file in orphanedFiles)
                    {
                        sb.AppendLine($"  - {file}");
                    }
                }
                else
                {
                    sb.AppendLine("✅ No orphaned cache files found");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generating diagnostics: {ex.Message}";
            }
        }

        #endregion
    }
}
