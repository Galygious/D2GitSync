using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace D2GitSync.Core
{
    /// <summary>
    /// Main service for D2R save synchronization using Git and symlinks.
    /// This can be embedded directly into the D2RLAN launcher.
    /// </summary>
    public class D2SaveSyncService : IDisposable
    {
        private readonly ILogger<D2SaveSyncService> _logger;
        private readonly GitService _gitService;
        private readonly SymlinkManager _symlinkManager;
        private readonly FileWatcherService _fileWatcher;
        private readonly SemaphoreSlim _syncSemaphore;
        
        private SyncConfiguration _config;
        private bool _isRunning;
        private bool _disposed;

        public event EventHandler<SyncStatusEventArgs> StatusChanged;
        public event EventHandler<SyncErrorEventArgs> ErrorOccurred;

        public D2SaveSyncService(ILogger<D2SaveSyncService> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<D2SaveSyncService>.Instance;
            _gitService = new GitService(_logger);
            _symlinkManager = new SymlinkManager(_logger);
            _fileWatcher = new FileWatcherService(_logger);
            _syncSemaphore = new SemaphoreSlim(1, 1);

            _fileWatcher.FileChanged += OnFileChanged;
        }

        /// <summary>
        /// Starts the sync service with the provided configuration.
        /// This would be called by the launcher when the user enables sync.
        /// </summary>
        public async Task StartAsync(SyncConfiguration config, CancellationToken cancellationToken = default)
        {
            if (_isRunning)
                throw new InvalidOperationException("Service is already running");

            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            try
            {
                _logger.LogInformation("Starting D2R Save Sync Service");
                OnStatusChanged(SyncStatus.Initializing, "Starting sync service...");

                // Step 1: Validate configuration
                await ValidateConfigurationAsync(cancellationToken);
                
                // Step 2: Initialize Git repository
                await _gitService.InitializeRepositoryAsync(_config.GitRepositoryPath, cancellationToken);
                
                // Step 3: Pull latest changes from remote
                if (!string.IsNullOrEmpty(_config.RemoteRepositoryUrl))
                {
                    await _gitService.PullFromRemoteAsync(_config.GitRepositoryPath, cancellationToken);
                }
                
                // Step 4: Set up symlinks (this is the magic!)
                await _symlinkManager.CreateSymlinksAsync(_config.SavesPath, _config.GitRepositoryPath, cancellationToken);
                
                // Step 5: Start file monitoring
                await _fileWatcher.StartAsync(_config.SavesPath, _config.DebounceSeconds, cancellationToken);
                
                _isRunning = true;
                OnStatusChanged(SyncStatus.Running, "Sync service is active");
                _logger.LogInformation("D2R Save Sync Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start sync service");
                OnErrorOccurred("Failed to start sync service", ex);
                throw;
            }
        }

        /// <summary>
        /// Stops the sync service.
        /// This would be called by the launcher when shutting down or when user disables sync.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                return;

            try
            {
                _logger.LogInformation("Stopping D2R Save Sync Service");
                OnStatusChanged(SyncStatus.Stopping, "Stopping sync service...");

                // Stop file monitoring
                await _fileWatcher.StopAsync(cancellationToken);
                
                // Perform final sync
                await SyncNowAsync(cancellationToken);
                
                _isRunning = false;
                OnStatusChanged(SyncStatus.Stopped, "Sync service stopped");
                _logger.LogInformation("D2R Save Sync Service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping sync service");
                OnErrorOccurred("Error while stopping sync service", ex);
            }
        }

        /// <summary>
        /// Performs an immediate sync operation.
        /// This could be exposed as a "Sync Now" button in the launcher.
        /// </summary>
        public async Task SyncNowAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Service is not running");

            await _syncSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Starting manual sync operation");
                OnStatusChanged(SyncStatus.Syncing, "Syncing saves...");

                // Since we're using symlinks, the files are already in the git repo
                // We just need to commit and push any changes
                var hasChanges = await _gitService.HasUncommittedChangesAsync(_config.GitRepositoryPath, cancellationToken);
                
                if (hasChanges)
                {
                    var commitMessage = $"Auto-sync: Save files updated on {Environment.MachineName} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    await _gitService.CommitChangesAsync(_config.GitRepositoryPath, commitMessage, cancellationToken);
                    
                    if (!string.IsNullOrEmpty(_config.RemoteRepositoryUrl))
                    {
                        await _gitService.PushToRemoteAsync(_config.GitRepositoryPath, cancellationToken);
                    }
                    
                    OnStatusChanged(SyncStatus.Running, "Sync completed successfully");
                    _logger.LogInformation("Sync operation completed - changes committed and pushed");
                }
                else
                {
                    OnStatusChanged(SyncStatus.Running, "No changes to sync");
                    _logger.LogDebug("Sync operation completed - no changes detected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync operation");
                OnErrorOccurred("Error during sync operation", ex);
                OnStatusChanged(SyncStatus.Running, "Sync failed - see logs for details");
                throw;
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the current status of the sync service.
        /// The launcher can use this for status displays.
        /// </summary>
        public SyncServiceStatus GetStatus()
        {
            return new SyncServiceStatus
            {
                IsRunning = _isRunning,
                Configuration = _config,
                LastSyncTime = _gitService.GetLastCommitTime(_config?.GitRepositoryPath),
                HasUncommittedChanges = _config != null ? _gitService.HasUncommittedChangesAsync(_config.GitRepositoryPath).Result : false
            };
        }

        private async Task ValidateConfigurationAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_config.SavesPath))
                throw new ArgumentException("SavesPath cannot be empty");
            
            if (string.IsNullOrEmpty(_config.GitRepositoryPath))
                throw new ArgumentException("GitRepositoryPath cannot be empty");

            if (!System.IO.Directory.Exists(_config.SavesPath))
                throw new System.IO.DirectoryNotFoundException($"Saves directory not found: {_config.SavesPath}");

            // Ensure Git is available, install if needed
            var dependencyInstaller = new DependencyInstaller(_logger);
            if (!await dependencyInstaller.EnsureGitInstalledAsync(cancellationToken))
            {
                throw new InvalidOperationException("Git is required but could not be installed automatically. Please install Git for Windows manually from https://git-scm.com/download/win");
            }
        }

        private async void OnFileChanged(object sender, FileChangedEventArgs e)
        {
            if (!_isRunning)
                return;

            try
            {
                // Debouncing is handled by FileWatcherService
                await SyncNowAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file change event");
                OnErrorOccurred("Error handling file change", ex);
            }
        }

        private void OnStatusChanged(SyncStatus status, string message)
        {
            StatusChanged?.Invoke(this, new SyncStatusEventArgs(status, message));
        }

        private void OnErrorOccurred(string message, Exception exception)
        {
            ErrorOccurred?.Invoke(this, new SyncErrorEventArgs(message, exception));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (_isRunning)
                {
                    StopAsync().Wait(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            _fileWatcher?.Dispose();
            _syncSemaphore?.Dispose();
            _disposed = true;
        }
    }
}
