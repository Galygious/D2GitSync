using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace D2GitSync.Core
{
    /// <summary>
    /// Handles all Git operations for the save sync service.
    /// </summary>
    public class GitService
    {
        private readonly ILogger<GitService> _logger;

        public GitService(ILogger<GitService> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitService>.Instance;
        }

        /// <summary>
        /// Checks if Git is available in the system PATH.
        /// </summary>
        public async Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunGitCommandAsync("--version", Environment.CurrentDirectory, cancellationToken);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Initializes a Git repository at the specified path.
        /// </summary>
        public async Task InitializeRepositoryAsync(string repoPath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing Git repository at {RepoPath}", repoPath);

            // Create directory if it doesn't exist
            Directory.CreateDirectory(repoPath);

            // Check if already a git repository
            if (Directory.Exists(Path.Combine(repoPath, ".git")))
            {
                _logger.LogDebug("Repository already exists at {RepoPath}", repoPath);
                return;
            }

            // Initialize repository
            var result = await RunGitCommandAsync("init --initial-branch=main", repoPath, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to initialize Git repository: {result.StandardError}");
            }

            // Configure git user if not already configured
            await ConfigureGitUserAsync(repoPath, cancellationToken);

            // Create initial gitignore
            await CreateGitIgnoreAsync(repoPath, cancellationToken);

            // Create README
            await CreateReadmeAsync(repoPath, cancellationToken);

            // Initial commit
            await RunGitCommandAsync("add .", repoPath, cancellationToken);
            var commitResult = await RunGitCommandAsync("commit -m \"Initial commit: Setup D2R saves repository\"", repoPath, cancellationToken);

            _logger.LogInformation("Git repository initialized successfully");
        }

        /// <summary>
        /// Pulls latest changes from the remote repository.
        /// </summary>
        public async Task PullFromRemoteAsync(string repoPath, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Pulling changes from remote repository");

            try
            {
                // First fetch to check if remote exists
                var fetchResult = await RunGitCommandAsync("fetch origin", repoPath, cancellationToken);
                if (fetchResult.ExitCode != 0)
                {
                    _logger.LogWarning("No remote repository configured or fetch failed");
                    return;
                }

                // Pull changes with merge strategy that favors local changes
                var pullResult = await RunGitCommandAsync("pull origin main --strategy-option=ours", repoPath, cancellationToken);
                if (pullResult.ExitCode == 0)
                {
                    _logger.LogInformation("Successfully pulled changes from remote repository");
                }
                else
                {
                    _logger.LogWarning("Pull failed: {Error}", pullResult.StandardError);
                    // If standard pull fails, try with rebase to preserve local commits
                    var rebasePullResult = await RunGitCommandAsync("pull --rebase origin main", repoPath, cancellationToken);
                    if (rebasePullResult.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully pulled changes using rebase strategy");
                    }
                    else
                    {
                        _logger.LogError("Both pull strategies failed. Manual intervention may be required.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error pulling from remote repository");
            }
        }

        /// <summary>
        /// Pushes local changes to the remote repository.
        /// </summary>
        public async Task PushToRemoteAsync(string repoPath, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Pushing changes to remote repository");

            try
            {
                var result = await RunGitCommandAsync("push origin main", repoPath, cancellationToken);
                if (result.ExitCode == 0)
                {
                    _logger.LogInformation("Successfully pushed changes to remote repository");
                }
                else
                {
                    _logger.LogWarning("Push failed: {Error}", result.StandardError);
                    throw new InvalidOperationException($"Failed to push to remote: {result.StandardError}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing to remote repository");
                throw;
            }
        }

        /// <summary>
        /// Checks if there are uncommitted changes in the repository.
        /// </summary>
        public async Task<bool> HasUncommittedChangesAsync(string repoPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunGitCommandAsync("status --porcelain", repoPath, cancellationToken);
                return !string.IsNullOrWhiteSpace(result.StandardOutput);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Commits all changes with the specified message.
        /// </summary>
        public async Task CommitChangesAsync(string repoPath, string commitMessage, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Committing changes with message: {CommitMessage}", commitMessage);

            // Add all changes
            var addResult = await RunGitCommandAsync("add .", repoPath, cancellationToken);
            if (addResult.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to add changes: {addResult.StandardError}");
            }

            // Commit changes
            var commitResult = await RunGitCommandAsync($"commit -m \"{commitMessage}\"", repoPath, cancellationToken);
            if (commitResult.ExitCode != 0)
            {
                // Check if it's just "nothing to commit"
                if (commitResult.StandardOutput.Contains("nothing to commit"))
                {
                    _logger.LogDebug("No changes to commit");
                    return;
                }
                throw new InvalidOperationException($"Failed to commit changes: {commitResult.StandardError}");
            }

            _logger.LogInformation("Changes committed successfully");
        }

        /// <summary>
        /// Commits changes only for specific paths/patterns within the repository.
        /// </summary>
        public async Task CommitScopedChangesAsync(string repoPath, string commitMessage, string[] scopedPaths, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Committing scoped changes with message: {CommitMessage}, paths: {ScopedPaths}", commitMessage, string.Join(", ", scopedPaths));

            // Add only the scoped paths
            foreach (var path in scopedPaths)
            {
                var relativePath = Path.GetRelativePath(repoPath, path);
                if (relativePath.StartsWith(".."))
                {
                    _logger.LogWarning("Path {Path} is outside repository {RepoPath}, skipping", path, repoPath);
                    continue;
                }

                var addResult = await RunGitCommandAsync($"add \"{relativePath}\"", repoPath, cancellationToken);
                if (addResult.ExitCode != 0)
                {
                    _logger.LogWarning("Failed to add path {Path}: {Error}", relativePath, addResult.StandardError);
                }
            }

            // Commit changes
            var commitResult = await RunGitCommandAsync($"commit -m \"{commitMessage}\"", repoPath, cancellationToken);
            if (commitResult.ExitCode != 0)
            {
                // Check if it's just "nothing to commit"
                if (commitResult.StandardOutput.Contains("nothing to commit"))
                {
                    _logger.LogDebug("No changes to commit for scoped paths");
                    return;
                }
                throw new InvalidOperationException($"Failed to commit scoped changes: {commitResult.StandardError}");
            }

            _logger.LogInformation("Scoped changes committed successfully");
        }

        /// <summary>
        /// Checks if there are uncommitted changes in specific paths within the repository.
        /// </summary>
        public async Task<bool> HasUncommittedChangesInScopeAsync(string repoPath, string[] scopedPaths, CancellationToken cancellationToken = default)
        {
            try
            {
                foreach (var path in scopedPaths)
                {
                    var relativePath = Path.GetRelativePath(repoPath, path);
                    if (relativePath.StartsWith(".."))
                    {
                        continue; // Skip paths outside repository
                    }

                    var result = await RunGitCommandAsync($"status --porcelain \"{relativePath}\"", repoPath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the timestamp of the last commit.
        /// </summary>
        public DateTime? GetLastCommitTime(string repoPath)
        {
            try
            {
                var result = RunGitCommandAsync("log -1 --format=%ct", repoPath).Result;
                if (result.ExitCode == 0 && long.TryParse(result.StandardOutput.Trim(), out var unixTimestamp))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        /// <summary>
        /// Configures a remote repository URL.
        /// </summary>
        public async Task ConfigureRemoteAsync(string repoPath, string remoteUrl, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Configuring remote repository: {RemoteUrl}", remoteUrl);

            // Remove existing origin if it exists
            await RunGitCommandAsync("remote remove origin", repoPath, cancellationToken);

            // Add new origin
            var result = await RunGitCommandAsync($"remote add origin {remoteUrl}", repoPath, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to configure remote: {result.StandardError}");
            }

            _logger.LogInformation("Remote repository configured successfully");
        }

        private async Task ConfigureGitUserAsync(string repoPath, CancellationToken cancellationToken)
        {
            // Check if user is already configured globally
            var globalUserResult = await RunGitCommandAsync("config --global user.name", repoPath, cancellationToken);
            if (globalUserResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(globalUserResult.StandardOutput))
            {
                _logger.LogDebug("Git user already configured globally");
                return;
            }

            // Configure local user for this repository
            var userName = Environment.UserName;
            var userEmail = $"{userName}@{Environment.MachineName}.local";

            await RunGitCommandAsync($"config user.name \"{userName}\"", repoPath, cancellationToken);
            await RunGitCommandAsync($"config user.email \"{userEmail}\"", repoPath, cancellationToken);

            _logger.LogDebug("Configured Git user: {UserName} <{UserEmail}>", userName, userEmail);
        }

        private async Task CreateGitIgnoreAsync(string repoPath, CancellationToken cancellationToken)
        {
            var gitignorePath = Path.Combine(repoPath, ".gitignore");
            var gitignoreContent = @"# Temporary files
*.tmp
*.bak
*.ctlo
*.keyo
Settings.json
Thumbs.db
.DS_Store

# Log files
*.log
";

            await File.WriteAllTextAsync(gitignorePath, gitignoreContent, cancellationToken);
        }

        private async Task CreateReadmeAsync(string repoPath, CancellationToken cancellationToken)
        {
            var readmePath = Path.Combine(repoPath, "README.md");
            var readmeContent = $@"# Diablo 2 Resurrected Save Files

This repository contains automatically synchronized D2R save files.

- **Generated by**: D2GitSync
- **Machine**: {Environment.MachineName}
- **User**: {Environment.UserName}
- **Created**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

## Files

This repository will contain your character saves (*.d2s), shared stash files (*.ma*), and other save-related files.

**⚠️ Important**: Keep this repository private if you're using a cloud Git service to protect your save data.
";

            await File.WriteAllTextAsync(readmePath, readmeContent, cancellationToken);
        }

        private async Task<GitCommandResult> RunGitCommandAsync(string arguments, string workingDirectory, CancellationToken cancellationToken = default)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _logger.LogTrace("Running git command: git {Arguments} (in {WorkingDirectory})", arguments, workingDirectory);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            var result = new GitCommandResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = output,
                StandardError = error
            };

            if (result.ExitCode != 0)
            {
                _logger.LogDebug("Git command failed with exit code {ExitCode}: {Error}", result.ExitCode, result.StandardError);
            }

            return result;
        }

        private class GitCommandResult
        {
            public int ExitCode { get; set; }
            public string StandardOutput { get; set; }
            public string StandardError { get; set; }
        }
    }
}
