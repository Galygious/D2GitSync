using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace D2GitSync.Core
{
    /// <summary>
    /// Integration interface for D2RLAN launcher.
    /// This provides the hooks that Bonesy would need to integrate into the launcher.
    /// </summary>
    public interface ID2RLauncherIntegration
    {
        /// <summary>
        /// Called when the launcher starts up to initialize sync service.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when the launcher is about to start D2R.
        /// This is where the sync service would be started.
        /// </summary>
        Task OnGameStartingAsync(string savesPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when D2R game process has ended.
        /// This is where the sync service would perform final sync and stop.
        /// </summary>
        Task OnGameEndedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when the launcher is shutting down.
        /// </summary>
        Task ShutdownAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current sync configuration from launcher settings.
        /// </summary>
        SyncConfiguration GetCurrentConfiguration();

        /// <summary>
        /// Updates the sync configuration in launcher settings.
        /// </summary>
        Task UpdateConfigurationAsync(SyncConfiguration config, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of the sync service.
        /// </summary>
        SyncServiceStatus GetStatus();

        /// <summary>
        /// Performs an immediate sync operation.
        /// </summary>
        Task SyncNowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event fired when sync status changes (for UI updates).
        /// </summary>
        event EventHandler<SyncStatusEventArgs> StatusChanged;

        /// <summary>
        /// Event fired when sync errors occur (for UI notifications).
        /// </summary>
        event EventHandler<SyncErrorEventArgs> ErrorOccurred;
    }

    /// <summary>
    /// Default implementation of launcher integration.
    /// Bonesy would adapt this to work with D2RLAN's architecture.
    /// </summary>
    public class D2RLauncherIntegration : ID2RLauncherIntegration, IDisposable
    {
        private readonly ILogger<D2RLauncherIntegration> _logger;
        private readonly D2SaveSyncService _syncService;
        private SyncConfiguration _configuration;
        private bool _disposed;

        public event EventHandler<SyncStatusEventArgs> StatusChanged;
        public event EventHandler<SyncErrorEventArgs> ErrorOccurred;

        public D2RLauncherIntegration(ILogger<D2RLauncherIntegration> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<D2RLauncherIntegration>.Instance;
            _syncService = new D2SaveSyncService(_logger);

            // Forward events
            _syncService.StatusChanged += (s, e) => StatusChanged?.Invoke(s, e);
            _syncService.ErrorOccurred += (s, e) => ErrorOccurred?.Invoke(s, e);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing D2R Save Sync integration");

            // Load configuration from launcher settings
            // This would be adapted to use the launcher's settings system
            _configuration = LoadConfigurationFromLauncher();

            _logger.LogInformation("D2R Save Sync integration initialized");
        }

        public async Task OnGameStartingAsync(string savesPath, CancellationToken cancellationToken = default)
        {
            if (_configuration?.AutoSyncEnabled != true)
            {
                _logger.LogDebug("Auto-sync is disabled, skipping sync service startup");
                return;
            }

            _logger.LogInformation("Game starting, initializing save sync for: {SavesPath}", savesPath);

            try
            {
                // Update saves path from launcher
                _configuration.SavesPath = savesPath;

                // Start the sync service
                await _syncService.StartAsync(_configuration, cancellationToken);

                _logger.LogInformation("Save sync service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start save sync service");
                // Don't throw - we don't want to prevent the game from starting
            }
        }

        public async Task OnGameEndedAsync(CancellationToken cancellationToken = default)
        {
            if (_configuration?.AutoSyncEnabled != true)
                return;

            _logger.LogInformation("Game ended, stopping save sync service");

            try
            {
                await _syncService.StopAsync(cancellationToken);
                _logger.LogInformation("Save sync service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping save sync service");
            }
        }

        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Shutting down D2R Save Sync integration");

            try
            {
                await _syncService.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown");
            }
        }

        public SyncConfiguration GetCurrentConfiguration()
        {
            return _configuration?.Clone();
        }

        public async Task UpdateConfigurationAsync(SyncConfiguration config, CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            config.Validate();
            _configuration = config.Clone();

            // Save to launcher settings
            await SaveConfigurationToLauncher(_configuration, cancellationToken);

            _logger.LogInformation("Sync configuration updated");
        }

        public SyncServiceStatus GetStatus()
        {
            return _syncService.GetStatus();
        }

        public async Task SyncNowAsync(CancellationToken cancellationToken = default)
        {
            await _syncService.SyncNowAsync(cancellationToken);
        }

        /// <summary>
        /// This method would be adapted to load from the launcher's settings system.
        /// For example, if D2RLAN uses JSON config files, XML, registry, etc.
        /// </summary>
        private SyncConfiguration LoadConfigurationFromLauncher()
        {
            // Example implementation - Bonesy would adapt this
            try
            {
                var configPath = "D2GitSyncConfig.json"; // Or wherever launcher stores settings
                if (System.IO.File.Exists(configPath))
                {
                    return SyncConfiguration.LoadFromFile(configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load sync configuration, using defaults");
            }

            return SyncConfiguration.CreateDefault();
        }

        /// <summary>
        /// This method would be adapted to save to the launcher's settings system.
        /// </summary>
        private async Task SaveConfigurationToLauncher(SyncConfiguration config, CancellationToken cancellationToken)
        {
            // Example implementation - Bonesy would adapt this
            try
            {
                var configPath = "D2GitSyncConfig.json"; // Or wherever launcher stores settings
                config.SaveToFile(configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save sync configuration");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                ShutdownAsync().Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            _syncService?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Configuration helper methods for launcher UI integration.
    /// These would be useful for creating settings panels in the launcher.
    /// </summary>
    public static class LauncherConfigurationHelpers
    {
        /// <summary>
        /// Detects the default D2R saves path based on common locations.
        /// </summary>
        public static string DetectSavesPath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            var commonPaths = new[]
            {
                System.IO.Path.Combine(userProfile, "Saved Games", "Diablo II Resurrected"),
                System.IO.Path.Combine(userProfile, "Documents", "Diablo II Resurrected"),
                @"C:\Users\Public\Games\Diablo II Resurrected"
            };

            foreach (var path in commonPaths)
            {
                if (System.IO.Directory.Exists(path))
                    return path;
            }

            return commonPaths[0]; // Return first as default
        }

        /// <summary>
        /// Suggests a default Git repository path.
        /// </summary>
        public static string SuggestGitRepositoryPath()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Path.Combine(documentsPath, "D2RSaveSync");
        }

        /// <summary>
        /// Validates a Git repository URL format.
        /// </summary>
        public static bool IsValidGitUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
