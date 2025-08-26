using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace D2GitSync.Core
{
    /// <summary>
    /// Handles automatic installation of required dependencies using winget.
    /// This ensures users don't need to manually install Git.
    /// </summary>
    public class DependencyInstaller
    {
        private readonly ILogger<DependencyInstaller> _logger;

        public DependencyInstaller(ILogger<DependencyInstaller> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DependencyInstaller>.Instance;
        }

        /// <summary>
        /// Checks if Git is available and installs it if missing.
        /// </summary>
        public async Task<bool> EnsureGitInstalledAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Checking if Git is installed...");

            // First check if Git is already available
            if (await IsGitAvailableAsync(cancellationToken))
            {
                _logger.LogInformation("Git is already installed and available");
                return true;
            }

            _logger.LogInformation("Git not found, attempting to install via winget...");

            // Check if winget is available
            if (!await IsWingetAvailableAsync(cancellationToken))
            {
                _logger.LogWarning("winget is not available. User will need to install Git manually.");
                return false;
            }

            // Install Git using winget
            return await InstallGitAsync(cancellationToken);
        }

        /// <summary>
        /// Checks if GitHub CLI is available and installs it if missing (optional).
        /// </summary>
        public async Task<bool> EnsureGitHubCliInstalledAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Checking if GitHub CLI is installed...");

            if (await IsGitHubCliAvailableAsync(cancellationToken))
            {
                _logger.LogInformation("GitHub CLI is already installed and available");
                return true;
            }

            _logger.LogInformation("GitHub CLI not found, attempting to install via winget...");

            if (!await IsWingetAvailableAsync(cancellationToken))
            {
                _logger.LogWarning("winget is not available. GitHub CLI installation skipped.");
                return false;
            }

            return await InstallGitHubCliAsync(cancellationToken);
        }

        /// <summary>
        /// Checks if Git is available in the system PATH.
        /// </summary>
        public async Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunCommandAsync("git", "--version", cancellationToken);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if GitHub CLI is available in the system PATH.
        /// </summary>
        public async Task<bool> IsGitHubCliAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunCommandAsync("gh", "--version", cancellationToken);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if winget is available for package installation.
        /// </summary>
        public async Task<bool> IsWingetAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunCommandAsync("winget", "--version", cancellationToken);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Installs Git using winget.
        /// </summary>
        private async Task<bool> InstallGitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Installing Git for Windows via winget...");

                var result = await RunCommandAsync("winget", "install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements", cancellationToken);

                if (result.ExitCode == 0)
                {
                    _logger.LogInformation("Git installation completed successfully");
                    
                    // Wait a moment for PATH to update, then verify
                    await Task.Delay(2000, cancellationToken);
                    
                    if (await IsGitAvailableAsync(cancellationToken))
                    {
                        _logger.LogInformation("Git is now available and ready to use");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Git was installed but may require a restart or PATH refresh to be available");
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("Git installation failed. Exit code: {ExitCode}, Error: {Error}", result.ExitCode, result.StandardError);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Git installation");
                return false;
            }
        }

        /// <summary>
        /// Installs GitHub CLI using winget.
        /// </summary>
        private async Task<bool> InstallGitHubCliAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Installing GitHub CLI via winget...");

                var result = await RunCommandAsync("winget", "install --id GitHub.cli -e --source winget --accept-package-agreements --accept-source-agreements", cancellationToken);

                if (result.ExitCode == 0)
                {
                    _logger.LogInformation("GitHub CLI installation completed successfully");
                    
                    // Wait a moment for PATH to update, then verify
                    await Task.Delay(2000, cancellationToken);
                    
                    if (await IsGitHubCliAvailableAsync(cancellationToken))
                    {
                        _logger.LogInformation("GitHub CLI is now available and ready to use");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("GitHub CLI was installed but may require a restart or PATH refresh to be available");
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("GitHub CLI installation failed. Exit code: {ExitCode}, Error: {Error}", result.ExitCode, result.StandardError);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during GitHub CLI installation");
                return false;
            }
        }

        /// <summary>
        /// Gets detailed information about missing dependencies and installation options.
        /// </summary>
        public async Task<DependencyStatus> GetDependencyStatusAsync(CancellationToken cancellationToken = default)
        {
            var status = new DependencyStatus
            {
                IsGitAvailable = await IsGitAvailableAsync(cancellationToken),
                IsGitHubCliAvailable = await IsGitHubCliAvailableAsync(cancellationToken),
                IsWingetAvailable = await IsWingetAvailableAsync(cancellationToken)
            };

            return status;
        }

        private async Task<CommandResult> RunCommandAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _logger.LogTrace("Running command: {FileName} {Arguments}", fileName, arguments);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = output,
                StandardError = error
            };
        }

        private class CommandResult
        {
            public int ExitCode { get; set; }
            public string StandardOutput { get; set; }
            public string StandardError { get; set; }
        }
    }

    /// <summary>
    /// Status of required dependencies.
    /// </summary>
    public class DependencyStatus
    {
        public bool IsGitAvailable { get; set; }
        public bool IsGitHubCliAvailable { get; set; }
        public bool IsWingetAvailable { get; set; }

        public bool CanAutoInstall => IsWingetAvailable;
        public bool HasAllRequiredDependencies => IsGitAvailable;
        public bool HasAllOptionalDependencies => IsGitAvailable && IsGitHubCliAvailable;

        public string GetStatusMessage()
        {
            if (HasAllRequiredDependencies)
            {
                return "All required dependencies are available";
            }

            if (CanAutoInstall)
            {
                return "Missing dependencies can be automatically installed";
            }

            return "Manual installation of dependencies required";
        }

        public string[] GetMissingDependencies()
        {
            var missing = new System.Collections.Generic.List<string>();
            
            if (!IsGitAvailable)
                missing.Add("Git for Windows");
            
            if (!IsGitHubCliAvailable)
                missing.Add("GitHub CLI (optional)");

            return missing.ToArray();
        }

        public string[] GetInstallationInstructions()
        {
            var instructions = new System.Collections.Generic.List<string>();

            if (!IsGitAvailable)
            {
                if (CanAutoInstall)
                {
                    instructions.Add("Git will be automatically installed using winget");
                }
                else
                {
                    instructions.Add("Please install Git for Windows from: https://git-scm.com/download/win");
                }
            }

            if (!IsGitHubCliAvailable)
            {
                if (CanAutoInstall)
                {
                    instructions.Add("GitHub CLI can be automatically installed using winget (optional)");
                }
                else
                {
                    instructions.Add("GitHub CLI can be installed from: https://cli.github.com/ (optional)");
                }
            }

            return instructions.ToArray();
        }
    }
}
