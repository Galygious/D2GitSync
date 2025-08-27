using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace D2GitSync.Core
{
    /// <summary>
    /// Manages symbolic links between the D2R saves directory and Git repository.
    /// This is the key innovation - instead of copying files, we create symlinks
    /// so the game writes directly to the Git repository.
    /// </summary>
    public class SymlinkManager
    {
        private readonly ILogger<SymlinkManager> _logger;

        public SymlinkManager(ILogger<SymlinkManager> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SymlinkManager>.Instance;
        }

        /// <summary>
        /// Creates symlinks from the saves directory to the git repository.
        /// This allows the game to write directly to files under Git control.
        /// </summary>
        public async Task CreateSymlinksAsync(string savesPath, string gitRepoPath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Setting up symlinks from {SavesPath} to {GitRepoPath}", savesPath, gitRepoPath);

            // Ensure git repository directory exists
            Directory.CreateDirectory(gitRepoPath);

            // Get all subdirectories in saves path (for mod support)
            var directories = Directory.GetDirectories(savesPath, "*", SearchOption.AllDirectories);
            var allDirs = new List<string> { savesPath };
            allDirs.AddRange(directories);

            foreach (var dir in allDirs)
            {
                await ProcessDirectoryAsync(dir, savesPath, gitRepoPath, cancellationToken);
            }

            _logger.LogInformation("Symlink setup completed");
        }

        /// <summary>
        /// Creates scoped symlinks for specific mod paths only.
        /// This limits syncing to only the current mod's save files.
        /// </summary>
        public async Task CreateScopedSymlinksAsync(string[] scopedSavePaths, string gitRepoPath, string modName, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Setting up scoped symlinks for mod {ModName} from paths: {ScopedPaths}", modName, string.Join(", ", scopedSavePaths));

            // Ensure git repository directory exists
            Directory.CreateDirectory(gitRepoPath);

            foreach (var savePath in scopedSavePaths)
            {
                if (!Directory.Exists(savePath))
                {
                    _logger.LogWarning("Scoped save path does not exist: {SavePath}", savePath);
                    continue;
                }

                // For each scoped path, create a subdirectory in the git repo
                var saveDirName = Path.GetFileName(savePath);
                var gitTargetPath = Path.Combine(gitRepoPath, saveDirName);
                Directory.CreateDirectory(gitTargetPath);

                // Get all subdirectories in this scoped path
                var directories = Directory.GetDirectories(savePath, "*", SearchOption.AllDirectories);
                var allDirs = new List<string> { savePath };
                allDirs.AddRange(directories);

                foreach (var dir in allDirs)
                {
                    await ProcessScopedDirectoryAsync(dir, savePath, gitTargetPath, modName, cancellationToken);
                }
            }

            _logger.LogInformation("Scoped symlink setup completed for mod {ModName}", modName);
        }

        /// <summary>
        /// Removes all symlinks and restores original file structure.
        /// This should be called when disabling sync to restore normal operation.
        /// </summary>
        public async Task RemoveSymlinksAsync(string savesPath, string gitRepoPath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Removing symlinks and restoring original files");

            try
            {
                // Copy files back from git repo to saves directory
                await CopyFilesAsync(gitRepoPath, savesPath, cancellationToken);

                // Remove symlinks
                await RemoveSymlinksInDirectoryAsync(savesPath, cancellationToken);

                _logger.LogInformation("Symlinks removed and files restored");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing symlinks");
                throw;
            }
        }

        private async Task ProcessDirectoryAsync(string currentDir, string savesPath, string gitRepoPath, CancellationToken cancellationToken)
        {
            // Calculate relative path for this directory
            var relativePath = Path.GetRelativePath(savesPath, currentDir);
            var gitTargetDir = relativePath == "." ? gitRepoPath : Path.Combine(gitRepoPath, relativePath);

            // Ensure target directory exists in git repo
            Directory.CreateDirectory(gitTargetDir);

            // Process all save files in this directory
            var saveFilePatterns = new[] { "*.d2s", "*.ma*", "*.d2i", "*.key" };
            
            foreach (var pattern in saveFilePatterns)
            {
                var files = Directory.GetFiles(currentDir, pattern, SearchOption.TopDirectoryOnly);
                
                foreach (var file in files)
                {
                    await ProcessFileAsync(file, currentDir, gitTargetDir, cancellationToken);
                }
            }
        }

        private async Task ProcessScopedDirectoryAsync(string currentDir, string baseSavePath, string gitTargetPath, string modName, CancellationToken cancellationToken)
        {
            // Calculate relative path for this directory within the scoped path
            var relativePath = Path.GetRelativePath(baseSavePath, currentDir);
            var gitTargetDir = relativePath == "." ? gitTargetPath : Path.Combine(gitTargetPath, relativePath);

            // Ensure target directory exists in git repo
            Directory.CreateDirectory(gitTargetDir);

            // Process all save files in this directory with mod-specific filtering
            var saveFilePatterns = new[] { "*.d2s", "*.ma*", "*.d2i", "*.key" };
            
            foreach (var pattern in saveFilePatterns)
            {
                var files = Directory.GetFiles(currentDir, pattern, SearchOption.TopDirectoryOnly);
                
                foreach (var file in files)
                {
                    // Apply mod-specific filtering if needed
                    if (ShouldSyncFileForMod(file, modName))
                    {
                        await ProcessFileAsync(file, currentDir, gitTargetDir, cancellationToken);
                    }
                }
            }
        }

        private bool ShouldSyncFileForMod(string filePath, string modName)
        {
            // For now, sync all save files found in the scoped paths
            // Could be extended to filter based on specific mod requirements
            // For example, some mods might have specific file naming conventions
            
            var fileName = Path.GetFileName(filePath);
            
            // Skip temporary files
            if (fileName.EndsWith(".tmp") || fileName.EndsWith(".bak"))
                return false;
                
            // Skip system files
            if (fileName.StartsWith("."))
                return false;
                
            return true;
        }

        private async Task ProcessFileAsync(string filePath, string savesDir, string gitTargetDir, CancellationToken cancellationToken)
        {
            var fileName = Path.GetFileName(filePath);
            var gitFilePath = Path.Combine(gitTargetDir, fileName);
            
            try
            {
                // If file already exists in git repo, we're good
                if (File.Exists(gitFilePath))
                {
                    _logger.LogDebug("File already exists in git repo: {FileName}", fileName);
                }
                else
                {
                    // Copy original file to git repo first
                    File.Copy(filePath, gitFilePath, overwrite: true);
                    _logger.LogDebug("Copied file to git repo: {FileName}", fileName);
                }

                // Remove original file and create symlink
                if (File.Exists(filePath) && !IsSymlink(filePath))
                {
                    File.Delete(filePath);
                    CreateSymlink(filePath, gitFilePath);
                    _logger.LogDebug("Created symlink: {FileName}", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process file: {FilePath}", filePath);
                // Continue with other files even if one fails
            }
        }

        private async Task CopyFilesAsync(string sourceDir, string targetDir, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(sourceDir))
                return;

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);
                var targetFileDir = Path.GetDirectoryName(targetFile);

                Directory.CreateDirectory(targetFileDir);
                File.Copy(file, targetFile, overwrite: true);
            }
        }

        private async Task RemoveSymlinksInDirectoryAsync(string directory, CancellationToken cancellationToken)
        {
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                if (IsSymlink(file))
                {
                    File.Delete(file);
                    _logger.LogDebug("Removed symlink: {File}", file);
                }
            }
        }

        /// <summary>
        /// Creates a symbolic link from linkPath to targetPath.
        /// </summary>
        private void CreateSymlink(string linkPath, string targetPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CreateSymlinkWindows(linkPath, targetPath);
            }
            else
            {
                CreateSymlinkUnix(linkPath, targetPath);
            }
        }

        /// <summary>
        /// Checks if a file is a symbolic link.
        /// </summary>
        private bool IsSymlink(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                return fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch
            {
                return false;
            }
        }

        #region Platform-specific symlink creation

        private void CreateSymlinkWindows(string linkPath, string targetPath)
        {
            // Use mklink command on Windows
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink \"{linkPath}\" \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Failed to create symlink: {error}");
            }
        }

        private void CreateSymlinkUnix(string linkPath, string targetPath)
        {
            // Use ln command on Unix systems
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ln",
                    Arguments = $"-s \"{targetPath}\" \"{linkPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Failed to create symlink: {error}");
            }
        }

        #endregion
    }
}
