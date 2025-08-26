using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using D2GitSync.Core;
using Microsoft.Extensions.Logging;

namespace D2GitSync.Demo
{
    /// <summary>
    /// Demo console application showing how to use the D2GitSync library.
    /// This demonstrates what Bonesy would integrate into D2RLAN.
    /// </summary>
    class Program
    {
        private static readonly ILogger<Program> _logger = CreateLogger();
        private static D2RLauncherIntegration _integration;
        private static CancellationTokenSource _cancellationTokenSource;

        static async Task Main(string[] args)
        {
            Console.WriteLine("D2GitSync Demo - Save File Synchronization");
            Console.WriteLine("==========================================");
            Console.WriteLine();

            _cancellationTokenSource = new CancellationTokenSource();
            _integration = new D2RLauncherIntegration(_logger);

            // Subscribe to events for demo purposes
            _integration.StatusChanged += OnStatusChanged;
            _integration.ErrorOccurred += OnErrorOccurred;

            try
            {
                // Simulate launcher startup
                await SimulateLauncherFlow();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation cancelled by user.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                _logger.LogError(ex, "Demo application error");
            }
            finally
            {
                await _integration.ShutdownAsync();
                _integration.Dispose();
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task SimulateLauncherFlow()
        {
            Console.WriteLine("1. Checking dependencies...");
            await CheckAndInstallDependencies();

            Console.WriteLine("2. Initializing launcher integration...");
            await _integration.InitializeAsync(_cancellationTokenSource.Token);

            // Get current configuration
            var config = _integration.GetCurrentConfiguration();
            
            Console.WriteLine($"Current configuration:");
            Console.WriteLine($"  Saves Path: {config.SavesPath}");
            Console.WriteLine($"  Git Repo Path: {config.GitRepositoryPath}");
            Console.WriteLine($"  Auto Sync: {config.AutoSyncEnabled}");
            Console.WriteLine($"  Remote URL: {config.RemoteRepositoryUrl ?? "Not configured"}");
            Console.WriteLine();

            // Allow user to modify configuration
            await ConfigureSettings(config);

            if (!config.AutoSyncEnabled)
            {
                Console.WriteLine("Auto-sync is disabled. Demo will exit.");
                return;
            }

            Console.WriteLine("3. Simulating game start...");
            
            // Simulate game starting (this is where D2RLAN would hook in)
            await _integration.OnGameStartingAsync(config.SavesPath, _cancellationTokenSource.Token);

            Console.WriteLine();
            Console.WriteLine("Save sync is now active!");
            Console.WriteLine("Try creating/modifying files in your saves directory to see sync in action.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  's' - Sync now");
            Console.WriteLine("  'r' - Show status");
            Console.WriteLine("  'q' - Quit (simulate game end)");
            Console.WriteLine();

            // Interactive loop
            await InteractiveLoop();

            Console.WriteLine("4. Simulating game end...");
            await _integration.OnGameEndedAsync(_cancellationTokenSource.Token);
        }

        private static async Task CheckAndInstallDependencies()
        {
            try
            {
                Console.WriteLine("Checking dependency status...");
                var status = await _integration.CheckDependenciesAsync(_cancellationTokenSource.Token);

                Console.WriteLine($"Dependency Status:");
                Console.WriteLine($"  Git: {(status.IsGitAvailable ? "✓ Available" : "✗ Missing")}");
                Console.WriteLine($"  GitHub CLI: {(status.IsGitHubCliAvailable ? "✓ Available" : "✗ Missing (optional)")}");
                Console.WriteLine($"  Winget: {(status.IsWingetAvailable ? "✓ Available" : "✗ Missing")}");
                Console.WriteLine($"  Status: {status.GetStatusMessage()}");
                Console.WriteLine();

                if (!status.HasAllRequiredDependencies)
                {
                    if (status.CanAutoInstall)
                    {
                        Console.WriteLine("Missing dependencies detected. Installing automatically...");
                        Console.WriteLine("This may take a few moments and may require administrator privileges.");
                        Console.WriteLine();

                        var success = await _integration.InstallDependenciesAsync(includeOptional: false, _cancellationTokenSource.Token);
                        
                        if (success)
                        {
                            Console.WriteLine("✓ Dependencies installed successfully!");
                        }
                        else
                        {
                            Console.WriteLine("⚠ Some dependencies could not be installed automatically.");
                            Console.WriteLine("You may need to restart your computer or refresh your PATH environment variable.");
                            
                            var instructions = status.GetInstallationInstructions();
                            foreach (var instruction in instructions)
                            {
                                Console.WriteLine($"  • {instruction}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Required dependencies are missing and cannot be installed automatically.");
                        Console.WriteLine("Please install them manually:");
                        
                        var instructions = status.GetInstallationInstructions();
                        foreach (var instruction in instructions)
                        {
                            Console.WriteLine($"  • {instruction}");
                        }
                        
                        Console.WriteLine();
                        Console.WriteLine("Press any key to continue anyway (may cause errors)...");
                        Console.ReadKey();
                    }
                }
                else
                {
                    Console.WriteLine("✓ All required dependencies are available!");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error checking dependencies: {ex.Message}");
                Console.WriteLine("Continuing anyway...");
                Console.WriteLine();
            }
        }

        private static async Task ConfigureSettings(SyncConfiguration config)
        {
            Console.WriteLine("Configuration Setup:");
            Console.WriteLine("===================");

            // Configure saves path
            Console.Write($"Saves path [{config.SavesPath}]: ");
            var savesInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(savesInput))
            {
                config.SavesPath = savesInput;
            }

            // Validate saves path exists
            if (!Directory.Exists(config.SavesPath))
            {
                Console.WriteLine($"Warning: Saves directory does not exist: {config.SavesPath}");
                Console.WriteLine("Creating directory for demo purposes...");
                Directory.CreateDirectory(config.SavesPath);
                
                // Create a sample save file for demo
                var sampleSave = Path.Combine(config.SavesPath, "DemoCharacter.d2s");
                await File.WriteAllTextAsync(sampleSave, $"Demo save file created at {DateTime.Now}");
                Console.WriteLine($"Created sample save file: {sampleSave}");
            }

            // Configure git repo path
            Console.Write($"Git repository path [{config.GitRepositoryPath}]: ");
            var gitInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(gitInput))
            {
                config.GitRepositoryPath = gitInput;
            }

            // Configure remote URL (optional)
            Console.Write($"Remote Git URL (optional) [{config.RemoteRepositoryUrl ?? "none"}]: ");
            var remoteInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(remoteInput))
            {
                config.RemoteRepositoryUrl = remoteInput;
            }

            // Configure auto-sync
            Console.Write($"Enable auto-sync? (y/n) [{(config.AutoSyncEnabled ? "y" : "n")}]: ");
            var autoSyncInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(autoSyncInput))
            {
                config.AutoSyncEnabled = autoSyncInput.ToLower().StartsWith("y");
            }

            // Save updated configuration
            await _integration.UpdateConfigurationAsync(config, _cancellationTokenSource.Token);
            Console.WriteLine("Configuration saved!");
            Console.WriteLine();
        }

        private static async Task InteractiveLoop()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Console.Write("> ");
                var input = Console.ReadKey(true);
                Console.WriteLine();

                try
                {
                    switch (input.KeyChar)
                    {
                        case 's':
                        case 'S':
                            Console.WriteLine("Performing manual sync...");
                            await _integration.SyncNowAsync(_cancellationTokenSource.Token);
                            break;

                        case 'r':
                        case 'R':
                            ShowStatus();
                            break;

                        case 'q':
                        case 'Q':
                            Console.WriteLine("Quitting...");
                            return;

                        default:
                            Console.WriteLine("Unknown command. Use 's' (sync), 'r' (status), or 'q' (quit).");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                Console.WriteLine();
            }
        }

        private static void ShowStatus()
        {
            var status = _integration.GetStatus();
            
            Console.WriteLine("Current Status:");
            Console.WriteLine($"  Running: {status.IsRunning}");
            Console.WriteLine($"  Last Sync: {status.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}");
            Console.WriteLine($"  Uncommitted Changes: {status.HasUncommittedChanges}");
            Console.WriteLine($"  Status Message: {status.StatusMessage ?? "OK"}");
        }

        private static void OnStatusChanged(object sender, SyncStatusEventArgs e)
        {
            Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] Status: {e.Status} - {e.Message}");
        }

        private static void OnErrorOccurred(object sender, SyncErrorEventArgs e)
        {
            Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] ERROR: {e.Message}");
            if (e.Exception != null)
            {
                _logger.LogError(e.Exception, "Sync error occurred");
            }
        }

        private static ILogger<Program> CreateLogger()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information);
            });

            return loggerFactory.CreateLogger<Program>();
        }
    }
}
