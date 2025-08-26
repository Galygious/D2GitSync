using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace D2GitSync.Core
{
    /// <summary>
    /// Monitors the save files directory for changes and triggers sync events.
    /// Implements debouncing to avoid excessive sync operations.
    /// </summary>
    public class FileWatcherService : IDisposable
    {
        private readonly ILogger<FileWatcherService> _logger;
        private FileSystemWatcher _watcher;
        private System.Timers.Timer _debounceTimer;
        private readonly object _lock = new object();
        
        private string _watchPath;
        private int _debounceSeconds;
        private bool _disposed;

        public event EventHandler<FileChangedEventArgs> FileChanged;

        public FileWatcherService(ILogger<FileWatcherService> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileWatcherService>.Instance;
        }

        /// <summary>
        /// Starts monitoring the specified directory for save file changes.
        /// </summary>
        public async Task StartAsync(string watchPath, int debounceSeconds = 3, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(watchPath))
                throw new ArgumentException("Watch path cannot be empty", nameof(watchPath));

            if (!Directory.Exists(watchPath))
                throw new DirectoryNotFoundException($"Directory not found: {watchPath}");

            _watchPath = watchPath;
            _debounceSeconds = debounceSeconds;

            _logger.LogInformation("Starting file watcher for directory: {WatchPath}", watchPath);

            // Initialize the debounce timer
            _debounceTimer = new System.Timers.Timer(_debounceSeconds * 1000);
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += OnDebounceTimerElapsed;

            // Initialize the file system watcher
            _watcher = new FileSystemWatcher(_watchPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            // Subscribe to events
            _watcher.Created += OnFileSystemEvent;
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnFileSystemEvent;

            // Start watching
            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation("File watcher started successfully");
        }

        /// <summary>
        /// Stops the file watcher.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Stopping file watcher");

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }

            _logger.LogInformation("File watcher stopped");
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Only monitor save file types
                if (!IsSaveFile(e.FullPath))
                    return;

                _logger.LogDebug("File system event: {EventType} - {FilePath}", e.ChangeType, e.FullPath);

                lock (_lock)
                {
                    // Reset the debounce timer
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file system event for {FilePath}", e.FullPath);
            }
        }

        private void OnDebounceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _logger.LogDebug("Debounce timer elapsed, triggering file changed event");

                // Trigger the file changed event
                FileChanged?.Invoke(this, new FileChangedEventArgs(_watchPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in debounce timer elapsed handler");
            }
        }

        /// <summary>
        /// Checks if the file is a D2R save file that we should monitor.
        /// </summary>
        private bool IsSaveFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
                return false;

            // D2R save file patterns
            var saveFilePatterns = new[]
            {
                "*.d2s",    // Character saves
                "*.ma*",    // Map files (shared stash, etc.)
                "*.d2i",    // Item files
                "*.key"     // Key files
            };

            return saveFilePatterns.Any(pattern => IsFileMatchingPattern(fileName, pattern));
        }

        /// <summary>
        /// Simple pattern matching for file names (supports * wildcard).
        /// </summary>
        private bool IsFileMatchingPattern(string fileName, string pattern)
        {
            if (pattern == "*")
                return true;

            if (!pattern.Contains("*"))
                return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);

            // Handle patterns like "*.d2s" or "*.ma*"
            if (pattern.StartsWith("*."))
            {
                var extension = pattern.Substring(2);
                if (extension == "*")
                    return true;
                
                if (extension.EndsWith("*"))
                {
                    var partialExtension = extension.Substring(0, extension.Length - 1);
                    return fileName.Split('.').LastOrDefault()?.StartsWith(partialExtension, StringComparison.OrdinalIgnoreCase) == true;
                }
                
                return fileName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase);
            }

            // For more complex patterns, we could use regex, but the above should handle D2R files
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                StopAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Event arguments for file change notifications.
    /// </summary>
    public class FileChangedEventArgs : EventArgs
    {
        public string WatchPath { get; }
        public DateTime Timestamp { get; }

        public FileChangedEventArgs(string watchPath)
        {
            WatchPath = watchPath;
            Timestamp = DateTime.Now;
        }
    }
}
