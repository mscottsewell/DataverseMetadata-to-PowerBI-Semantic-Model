/**
 * FileSystemAdapter.ts
 * 
 * Wraps PowerPlatformToolBox's file system APIs to provide file operations.
 * Handles saving PBIP/TMDL files to disk.
 */

/**
 * Options for saving a file.
 */
export interface SaveFileOptions {
  /** Suggested file name */
  suggestedName?: string;
  
  /** File extension filter */
  extensions?: string[];
  
  /** File content as string or Uint8Array */
  content: string | Uint8Array;
  
  /** MIME type */
  mimeType?: string;
}

/**
 * Options for reading a file.
 */
export interface ReadFileOptions {
  /** File extension filter */
  extensions?: string[];
  
  /** Whether to read as binary */
  binary?: boolean;
}

/**
 * Result of a file read operation.
 */
export interface ReadFileResult {
  /** File name */
  name: string;
  
  /** File content */
  content: string | Uint8Array;
  
  /** File path (if available) */
  path?: string;
}

/**
 * File system adapter for PowerPlatformToolBox.
 * Wraps window.toolboxAPI.fileSystem methods.
 */
export class FileSystemAdapter {
  /**
   * Get the file system API instance.
   * Throws if PPTB APIs are not available.
   */
  private get api(): any {
    if (typeof window === 'undefined' || !window.toolboxAPI?.fileSystem) {
      throw new Error('window.toolboxAPI.fileSystem is not available. This tool must run in PowerPlatformToolBox.');
    }
    return window.toolboxAPI.fileSystem;
  }

  /**
   * Saves a file to disk with a file picker dialog.
   * 
   * @param options Save file options
   * @returns Promise that resolves with the saved file path, or null if canceled
   */
  async saveFile(options: SaveFileOptions): Promise<string | null> {
    try {
      const result = await this.api.saveFile({
        suggestedName: options.suggestedName,
        extensions: options.extensions,
        content: options.content,
        mimeType: options.mimeType,
      });

      return result?.path || null;
    } catch (error) {
      console.error('Failed to save file:', error);
      throw new Error(`Failed to save file: ${error}`);
    }
  }

  /**
   * Reads a file from disk with a file picker dialog.
   * 
   * @param options Read file options
   * @returns Promise that resolves with the file content, or null if canceled
   */
  async readFile(options: ReadFileOptions = {}): Promise<ReadFileResult | null> {
    try {
      const result = await this.api.readFile({
        extensions: options.extensions,
        binary: options.binary || false,
      });

      if (!result) {
        return null;
      }

      return {
        name: result.name,
        content: result.content,
        path: result.path,
      };
    } catch (error) {
      console.error('Failed to read file:', error);
      throw new Error(`Failed to read file: ${error}`);
    }
  }

  /**
   * Selects a folder with a folder picker dialog.
   * 
   * @returns Promise that resolves with the folder path, or null if canceled
   */
  async selectFolder(): Promise<string | null> {
    try {
      const result = await this.api.selectFolder();
      return result?.path || null;
    } catch (error) {
      console.error('Failed to select folder:', error);
      throw new Error(`Failed to select folder: ${error}`);
    }
  }

  /**
   * Writes multiple files to a selected folder.
   * Useful for saving complete PBIP projects with multiple TMDL files.
   * 
   * @param files Map of file paths to content
   * @param folderPath Optional pre-selected folder path
   * @returns Promise that resolves with the folder path where files were written
   */
  async writeFilesToFolder(
    files: Map<string, string | Uint8Array>,
    folderPath?: string
  ): Promise<string | null> {
    try {
      // If no folder path provided, ask user to select one
      const targetFolder = folderPath || await this.selectFolder();
      
      if (!targetFolder) {
        return null;
      }

      // Write each file
      const promises = Array.from(files.entries()).map(async ([relativePath, content]) => {
        const fullPath = `${targetFolder}/${relativePath}`;
        
        // Use saveFile with explicit path
        await this.api.writeFile({
          path: fullPath,
          content,
        });
      });

      await Promise.all(promises);

      return targetFolder;
    } catch (error) {
      console.error('Failed to write files to folder:', error);
      throw new Error(`Failed to write files to folder: ${error}`);
    }
  }

  /**
   * Checks if a file exists at the given path.
   * 
   * @param path File path to check
   * @returns Promise that resolves with true if file exists, false otherwise
   */
  async fileExists(path: string): Promise<boolean> {
    try {
      const result = await this.api.fileExists(path);
      return result === true;
    } catch (error) {
      console.error('Failed to check file existence:', error);
      return false;
    }
  }
}
