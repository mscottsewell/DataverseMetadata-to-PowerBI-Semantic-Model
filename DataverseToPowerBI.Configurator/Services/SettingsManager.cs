using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using DataverseToPowerBI.Configurator.Models;

namespace DataverseToPowerBI.Configurator.Services
{
    public class SettingsManager
    {
        private const string ConfigurationsFileName = ".dataverse_configurations.json";
        private readonly string _configurationsPath;
        private readonly string _appFolder;
        private ConfigurationsFile? _configurationsFile;

        public SettingsManager()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appFolder = Path.Combine(appDataPath, "DataverseToPowerBI.Configurator");
            Directory.CreateDirectory(_appFolder);
            
            _configurationsPath = Path.Combine(_appFolder, ConfigurationsFileName);
        }

        private string GetCachePath(string configurationName)
        {
            // Sanitize configuration name for use in filename
            var sanitized = string.Join("_", configurationName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_appFolder, $".dataverse_metadata_cache_{sanitized}.json");
        }

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
                // If loading fails, return new file
            }
            
            return new ConfigurationsFile();
        }

        private void SaveConfigurationsFile(ConfigurationsFile configurationsFile)
        {
            try
            {
                // DEBUG: Check what's being serialized
                var currentConfig = configurationsFile.Configurations
                    .FirstOrDefault(c => c.Name == configurationsFile.LastUsedConfigurationName);
                var attrInfoCount = currentConfig?.Settings?.AttributeDisplayInfo?.Sum(t => t.Value.Count) ?? 0;
                DebugLogger.LogSection("SaveConfigurationsFile - Before JSON Write",
                    $"Config: {configurationsFile.LastUsedConfigurationName}\n" +
                    $"AttributeDisplayInfo: {attrInfoCount} attrs across {currentConfig?.Settings?.AttributeDisplayInfo?.Count ?? 0} tables\n" +
                    $"File: {_configurationsPath}");
                
                var json = JsonConvert.SerializeObject(configurationsFile, Formatting.Indented);
                File.WriteAllText(_configurationsPath, json);
                _configurationsFile = configurationsFile;
                
                // DEBUG: Verify what was written
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

        public AppSettings LoadSettings()
        {
            _configurationsFile = LoadConfigurationsFile();
            
            // If there are no configurations, create a default one
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

            // Load the last used configuration
            var lastUsed = _configurationsFile.Configurations
                .FirstOrDefault(c => c.Name == _configurationsFile.LastUsedConfigurationName)
                ?? _configurationsFile.Configurations
                    .OrderByDescending(c => c.LastUsed)
                    .First();

            return lastUsed.Settings;
        }

        public void SaveSettings(AppSettings settings)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            var currentConfig = _configurationsFile.Configurations
                .FirstOrDefault(c => c.Name == _configurationsFile.LastUsedConfigurationName);

            if (currentConfig != null)
            {
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

        public string GetCurrentConfigurationName()
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }
            return _configurationsFile.LastUsedConfigurationName ?? "Default";
        }

        public List<string> GetConfigurationNames()
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }
            return _configurationsFile.Configurations.Select(c => c.Name).ToList();
        }

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

            config.LastUsed = DateTime.Now;
            _configurationsFile.LastUsedConfigurationName = configurationName;
            SaveConfigurationsFile(_configurationsFile);
        }

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

        public void CreateNewConfiguration(string configurationName, AppSettings? settings = null)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            if (_configurationsFile.Configurations.Any(c => c.Name == configurationName))
            {
                throw new Exception($"Configuration '{configurationName}' already exists.");
            }

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

            config.Name = newName;
            if (_configurationsFile.LastUsedConfigurationName == oldName)
            {
                _configurationsFile.LastUsedConfigurationName = newName;
            }

            SaveConfigurationsFile(_configurationsFile);
        }

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

        public string GetSettingsFolderPath()
        {
            return _appFolder;
        }

        public string? GetMostRecentConfigurationForEnvironment(string environmentUrl)
        {
            if (_configurationsFile == null)
            {
                _configurationsFile = LoadConfigurationsFile();
            }

            var matchingConfigs = _configurationsFile.Configurations
                .Where(c => c.Settings.LastEnvironmentUrl?.Equals(environmentUrl, StringComparison.OrdinalIgnoreCase) == true)
                .OrderByDescending(c => c.LastUsed)
                .ToList();

            return matchingConfigs.FirstOrDefault()?.Name;
        }

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
                // If loading fails, return null
            }
            
            return null;
        }

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
                // Ignore errors when clearing cache
            }
        }
    }
}
