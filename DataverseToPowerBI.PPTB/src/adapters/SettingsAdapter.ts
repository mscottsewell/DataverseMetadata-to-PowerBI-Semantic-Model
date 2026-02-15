/**
 * SettingsAdapter.ts
 * 
 * Wraps PowerPlatformToolBox's settings API to persist tool configuration.
 * Stores semantic model configurations, preferences, and cached metadata.
 */

import { ConfigurationsFile, ConfigurationEntry, MetadataCache } from '../types/DataModels';

/**
 * Settings keys used by this tool.
 */
const SETTINGS_KEYS = {
  /** All saved configurations */
  CONFIGURATIONS: 'configurations',
  
  /** Metadata cache */
  METADATA_CACHE: 'metadataCache',
  
  /** User preferences */
  PREFERENCES: 'preferences',
} as const;

/**
 * User preferences for the tool.
 */
export interface UserPreferences {
  /** Whether to auto-load cached metadata on startup */
  autoloadCache: boolean;
  
  /** Whether to show all attributes by default */
  showAllAttributes: boolean;
  
  /** Whether to use display names as aliases in SQL */
  useDisplayNameAliasesInSql: boolean;
  
  /** Theme preference (if applicable) */
  theme?: 'light' | 'dark' | 'auto';
  
  /** Last used output folder */
  lastOutputFolder?: string;
}

/**
 * Default user preferences.
 */
const DEFAULT_PREFERENCES: UserPreferences = {
  autoloadCache: true,
  showAllAttributes: false,
  useDisplayNameAliasesInSql: true,
  theme: 'auto',
};

/**
 * Settings adapter for PowerPlatformToolBox.
 * Wraps window.toolboxAPI.settings methods.
 */
export class SettingsAdapter {
  /**
   * Get the settings API instance.
   * Throws if PPTB APIs are not available.
   */
  private get api(): any {
    if (typeof window === 'undefined' || !window.toolboxAPI?.settings) {
      throw new Error('window.toolboxAPI.settings is not available. This tool must run in PowerPlatformToolBox.');
    }
    return window.toolboxAPI.settings;
  }

  // ============================================================================
  // Configurations
  // ============================================================================

  /**
   * Gets all saved configurations.
   */
  async getConfigurationsAsync(): Promise<ConfigurationsFile> {
    try {
      const data = await this.api.get(SETTINGS_KEYS.CONFIGURATIONS);
      
      if (!data) {
        return {
          configurations: [],
          lastUsedConfigurationName: undefined,
        };
      }

      return data as ConfigurationsFile;
    } catch (error) {
      console.error('Failed to get configurations:', error);
      return {
        configurations: [],
        lastUsedConfigurationName: undefined,
      };
    }
  }

  /**
   * Saves all configurations.
   */
  async saveConfigurationsAsync(configs: ConfigurationsFile): Promise<void> {
    try {
      await this.api.set(SETTINGS_KEYS.CONFIGURATIONS, configs);
    } catch (error) {
      console.error('Failed to save configurations:', error);
      throw new Error(`Failed to save configurations: ${error}`);
    }
  }

  /**
   * Gets a specific configuration by name.
   */
  async getConfigurationAsync(name: string): Promise<ConfigurationEntry | null> {
    const configs = await this.getConfigurationsAsync();
    return configs.configurations.find(c => c.name === name) || null;
  }

  /**
   * Saves or updates a configuration.
   */
  async saveConfigurationAsync(config: ConfigurationEntry): Promise<void> {
    const configs = await this.getConfigurationsAsync();
    
    // Find existing config with same name
    const existingIndex = configs.configurations.findIndex(c => c.name === config.name);
    
    if (existingIndex >= 0) {
      // Update existing
      configs.configurations[existingIndex] = config;
    } else {
      // Add new
      configs.configurations.push(config);
    }
    
    // Update last used
    configs.lastUsedConfigurationName = config.name;
    
    await this.saveConfigurationsAsync(configs);
  }

  /**
   * Deletes a configuration by name.
   */
  async deleteConfigurationAsync(name: string): Promise<void> {
    const configs = await this.getConfigurationsAsync();
    configs.configurations = configs.configurations.filter(c => c.name !== name);
    
    // Clear last used if it was deleted
    if (configs.lastUsedConfigurationName === name) {
      configs.lastUsedConfigurationName = undefined;
    }
    
    await this.saveConfigurationsAsync(configs);
  }

  /**
   * Renames a configuration.
   */
  async renameConfigurationAsync(oldName: string, newName: string): Promise<void> {
    const configs = await this.getConfigurationsAsync();
    const config = configs.configurations.find(c => c.name === oldName);
    
    if (!config) {
      throw new Error(`Configuration '${oldName}' not found`);
    }
    
    config.name = newName;
    
    // Update last used if it was renamed
    if (configs.lastUsedConfigurationName === oldName) {
      configs.lastUsedConfigurationName = newName;
    }
    
    await this.saveConfigurationsAsync(configs);
  }

  // ============================================================================
  // Metadata Cache
  // ============================================================================

  /**
   * Gets the cached metadata.
   */
  async getMetadataCacheAsync(): Promise<MetadataCache | null> {
    try {
      const data = await this.api.get(SETTINGS_KEYS.METADATA_CACHE);
      return data as MetadataCache | null;
    } catch (error) {
      console.error('Failed to get metadata cache:', error);
      return null;
    }
  }

  /**
   * Saves the metadata cache.
   */
  async saveMetadataCacheAsync(cache: MetadataCache): Promise<void> {
    try {
      await this.api.set(SETTINGS_KEYS.METADATA_CACHE, cache);
    } catch (error) {
      console.error('Failed to save metadata cache:', error);
      throw new Error(`Failed to save metadata cache: ${error}`);
    }
  }

  /**
   * Clears the metadata cache.
   */
  async clearMetadataCacheAsync(): Promise<void> {
    try {
      await this.api.remove(SETTINGS_KEYS.METADATA_CACHE);
    } catch (error) {
      console.error('Failed to clear metadata cache:', error);
      throw new Error(`Failed to clear metadata cache: ${error}`);
    }
  }

  // ============================================================================
  // User Preferences
  // ============================================================================

  /**
   * Gets user preferences.
   */
  async getPreferencesAsync(): Promise<UserPreferences> {
    try {
      const data = await this.api.get(SETTINGS_KEYS.PREFERENCES);
      
      if (!data) {
        return { ...DEFAULT_PREFERENCES };
      }

      // Merge with defaults to handle new preferences
      return {
        ...DEFAULT_PREFERENCES,
        ...data,
      };
    } catch (error) {
      console.error('Failed to get preferences:', error);
      return { ...DEFAULT_PREFERENCES };
    }
  }

  /**
   * Saves user preferences.
   */
  async savePreferencesAsync(preferences: UserPreferences): Promise<void> {
    try {
      await this.api.set(SETTINGS_KEYS.PREFERENCES, preferences);
    } catch (error) {
      console.error('Failed to save preferences:', error);
      throw new Error(`Failed to save preferences: ${error}`);
    }
  }

  /**
   * Updates a single preference value.
   */
  async updatePreferenceAsync<K extends keyof UserPreferences>(
    key: K,
    value: UserPreferences[K]
  ): Promise<void> {
    const preferences = await this.getPreferencesAsync();
    preferences[key] = value;
    await this.savePreferencesAsync(preferences);
  }

  // ============================================================================
  // Utility Methods
  // ============================================================================

  /**
   * Clears all tool settings.
   * Use with caution - this deletes everything!
   */
  async clearAllAsync(): Promise<void> {
    try {
      await this.api.clear();
    } catch (error) {
      console.error('Failed to clear all settings:', error);
      throw new Error(`Failed to clear all settings: ${error}`);
    }
  }

  /**
   * Exports all settings as JSON string.
   * Useful for backup or migration.
   */
  async exportSettingsAsync(): Promise<string> {
    try {
      const configs = await this.getConfigurationsAsync();
      const cache = await this.getMetadataCacheAsync();
      const preferences = await this.getPreferencesAsync();

      const exportData = {
        configurations: configs,
        metadataCache: cache,
        preferences,
        exportedAt: new Date().toISOString(),
      };

      return JSON.stringify(exportData, null, 2);
    } catch (error) {
      console.error('Failed to export settings:', error);
      throw new Error(`Failed to export settings: ${error}`);
    }
  }

  /**
   * Imports settings from JSON string.
   * Useful for restore or migration.
   */
  async importSettingsAsync(json: string): Promise<void> {
    try {
      const importData = JSON.parse(json);

      if (importData.configurations) {
        await this.saveConfigurationsAsync(importData.configurations);
      }

      if (importData.metadataCache) {
        await this.saveMetadataCacheAsync(importData.metadataCache);
      }

      if (importData.preferences) {
        await this.savePreferencesAsync(importData.preferences);
      }
    } catch (error) {
      console.error('Failed to import settings:', error);
      throw new Error(`Failed to import settings: ${error}`);
    }
  }
}
