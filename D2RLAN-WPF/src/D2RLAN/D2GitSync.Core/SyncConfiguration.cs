using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace D2GitSync.Core
{
    /// <summary>
    /// Configuration for the D2R save sync service.
    /// This would be managed by the launcher's settings system.
    /// </summary>
    public class SyncConfiguration
    {
        /// <summary>
        /// Path to the D2R saves directory. The launcher should auto-detect this.
        /// </summary>
        public string SavesPath { get; set; }

        /// <summary>
        /// Path where the Git repository will be created. User configurable.
        /// </summary>
        public string GitRepositoryPath { get; set; }

        /// <summary>
        /// URL of the remote Git repository (GitHub, GitLab, etc.). Optional.
        /// </summary>
        public string RemoteRepositoryUrl { get; set; }

        /// <summary>
        /// Whether auto-sync is enabled. This would be a checkbox in the launcher.
        /// </summary>
        public bool D2GitSyncEnabled { get; set; } = true;

        /// <summary>
        /// How long to wait after file changes before syncing (seconds).
        /// </summary>
        public int DebounceSeconds { get; set; } = 3;

        /// <summary>
        /// File patterns to monitor for changes.
        /// </summary>
        public string[] FilePatterns { get; set; } = { "*.d2s", "*.ma*", "*.d2i" };

        /// <summary>
        /// Git user name for commits. Can be auto-configured.
        /// </summary>
        public string GitUserName { get; set; } = Environment.UserName;

        /// <summary>
        /// Git user email for commits. Can be auto-configured.
        /// </summary>
        public string GitUserEmail { get; set; } = $"{Environment.UserName}@{Environment.MachineName}.local";

        /// <summary>
        /// Current mod name for scoped syncing. Only files related to this mod will be synced.
        /// </summary>
        public string CurrentModName { get; set; }

        /// <summary>
        /// Whether to scope syncing to only the current mod. If false, syncs entire saves directory.
        /// </summary>
        public bool EnableModScoping { get; set; } = true;

        /// <summary>
        /// Subdirectory patterns within the git repo to sync for each mod.
        /// Key is mod name, value is array of subdirectory patterns.
        /// </summary>
        public Dictionary<string, string[]> ModScopePaths { get; set; } = new Dictionary<string, string[]>();

        /// <summary>
        /// Gets the effective save paths for the current mod scope.
        /// </summary>
        public string[] GetScopedSavePaths()
        {
            if (!EnableModScoping || string.IsNullOrEmpty(CurrentModName))
            {
                return new[] { SavesPath };
            }

            var paths = new List<string>();
            
            // Main mod saves path
            var modSavePath = Path.Combine(SavesPath, "Mods", CurrentModName);
            if (Directory.Exists(modSavePath))
            {
                paths.Add(modSavePath);
            }

            // Check if mod uses retail location (some mods don't create mod-specific folders)
            if (paths.Count == 0 && Directory.Exists(SavesPath))
            {
                paths.Add(SavesPath);
            }

            return paths.ToArray();
        }

        /// <summary>
        /// Gets the git repository subdirectory for the current mod.
        /// </summary>
        public string GetModRepositoryPath()
        {
            if (!EnableModScoping || string.IsNullOrEmpty(CurrentModName))
            {
                return GitRepositoryPath;
            }

            return Path.Combine(GitRepositoryPath, "mods", CurrentModName);
        }

        /// <summary>
        /// Creates a default configuration with sensible defaults.
        /// The launcher would call this and then override with user preferences.
        /// </summary>
        public static SyncConfiguration CreateDefault()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var defaultSavesPath = Path.Combine(userProfile, "Saved Games", "Diablo II Resurrected");
            var defaultGitPath = Path.Combine(Path.GetTempPath(), "D2RSaveSync");

            return new SyncConfiguration
            {
                SavesPath = defaultSavesPath,
                GitRepositoryPath = defaultGitPath,
                D2GitSyncEnabled = true,
                DebounceSeconds = 3
            };
        }

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SavesPath))
                throw new ArgumentException("SavesPath cannot be empty");

            if (string.IsNullOrWhiteSpace(GitRepositoryPath))
                throw new ArgumentException("GitRepositoryPath cannot be empty");

            if (DebounceSeconds < 1 || DebounceSeconds > 60)
                throw new ArgumentException("DebounceSeconds must be between 1 and 60");

            if (FilePatterns == null || FilePatterns.Length == 0)
                throw new ArgumentException("FilePatterns cannot be empty");

            if (EnableModScoping && string.IsNullOrWhiteSpace(CurrentModName))
                throw new ArgumentException("CurrentModName cannot be empty when mod scoping is enabled");
        }

        /// <summary>
        /// Saves configuration to JSON file.
        /// The launcher might use this for persistence.
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Loads configuration from JSON file.
        /// </summary>
        public static SyncConfiguration LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return CreateDefault();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SyncConfiguration>(json, options);
        }

        /// <summary>
        /// Creates a copy of the configuration.
        /// </summary>
        public SyncConfiguration Clone()
        {
            var json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<SyncConfiguration>(json);
        }
    }

    /// <summary>
    /// Status information about the sync service.
    /// The launcher can use this for status displays and health monitoring.
    /// </summary>
    public class SyncServiceStatus
    {
        public bool IsRunning { get; set; }
        public SyncConfiguration Configuration { get; set; }
        public DateTime? LastSyncTime { get; set; }
        public bool HasUncommittedChanges { get; set; }
        public string StatusMessage { get; set; }
    }

    /// <summary>
    /// Event args for status change notifications.
    /// </summary>
    public class SyncStatusEventArgs : EventArgs
    {
        public SyncStatus Status { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public SyncStatusEventArgs(SyncStatus status, string message)
        {
            Status = status;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Event args for error notifications.
    /// </summary>
    public class SyncErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception Exception { get; }
        public DateTime Timestamp { get; }

        public SyncErrorEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Possible sync service states.
    /// </summary>
    public enum SyncStatus
    {
        Stopped,
        Initializing,
        Running,
        Syncing,
        Stopping,
        Error
    }
}
