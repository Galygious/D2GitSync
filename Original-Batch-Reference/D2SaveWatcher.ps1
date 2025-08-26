param(
    [string]$SavesPath,
    [string]$GitRepoPath
)

# Set console title for easy identification
$Host.UI.RawUI.WindowTitle = "D2SaveWatcher - Monitoring: $SavesPath"

# Configuration
$FileTypes = @("*.d2s", "*.ma*", "*.d2i")  # Diablo 2 save file types
$DebounceSeconds = 3  # Wait 3 seconds after last change before committing
$LogFile = Join-Path $PSScriptRoot "D2SaveWatcher.log"

# Function to write timestamped logs
function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] $Message"
    Write-Host $logMessage -ForegroundColor Green
    Add-Content -Path $LogFile -Value $logMessage
}

# Function to copy saves to git repo and commit
function Sync-SavesToGit {
    try {
        Write-Log "Detected save file changes. Starting sync..."
        
        # Copy save files to git repository (including subdirectories for mods)
        foreach ($fileType in $FileTypes) {
            $sourceFiles = Get-ChildItem -Path $SavesPath -Filter $fileType -Recurse -ErrorAction SilentlyContinue
            foreach ($file in $sourceFiles) {
                # Calculate relative path to preserve directory structure
                $relativePath = $file.FullName.Substring($SavesPath.Length).TrimStart('\')
                $destinationPath = Join-Path $GitRepoPath $relativePath
                $destinationDir = Split-Path $destinationPath -Parent
                
                # Create destination directory if it doesn't exist
                if (-not (Test-Path $destinationDir)) {
                    New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
                }
                
                Copy-Item -Path $file.FullName -Destination $destinationPath -Force
                Write-Log "Copied: $relativePath"
            }
        }
        
        # Change to git repository directory
        Set-Location $GitRepoPath
        
        # Check if there are any changes
        $gitStatus = git status --porcelain 2>$null
        if ($gitStatus) {
            # Add all changes
            git add . 2>$null
            
            # Create commit message with timestamp and computer name
            $commitMessage = "Auto-sync: Save files updated on $env:COMPUTERNAME at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
            
            # Commit changes
            $commitResult = git commit -m $commitMessage 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Log "Committed changes to local repository"
                
                # Push to remote repository
                $pushResult = git push origin main 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-Log "Successfully pushed changes to remote repository"
                } else {
                    Write-Log "Warning: Failed to push to remote repository (working offline?)"
                }
            } else {
                Write-Log "Warning: Failed to commit changes"
            }
        } else {
            Write-Log "No changes detected in git repository"
        }
    }
    catch {
        Write-Log "Error during sync: $($_.Exception.Message)"
    }
}

# Initialize
Write-Log "D2R Save Watcher started"
Write-Log "Monitoring: $SavesPath"
Write-Log "Git Repository: $GitRepoPath"
Write-Log "File types: $($FileTypes -join ', ')"

# Verify paths exist
if (-not (Test-Path $SavesPath)) {
    Write-Log "ERROR: Saves path does not exist: $SavesPath"
    exit 1
}

if (-not (Test-Path $GitRepoPath)) {
    Write-Log "ERROR: Git repository path does not exist: $GitRepoPath"
    exit 1
}

# Create file system watcher
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $SavesPath
$watcher.Filter = "*.*"
$watcher.IncludeSubdirectories = $true
$watcher.EnableRaisingEvents = $true

# Variables for debouncing
$lastChangeTime = Get-Date
$timer = New-Object System.Timers.Timer
$timer.Interval = ($DebounceSeconds * 1000)  # Convert to milliseconds
$timer.AutoReset = $false

# Timer event handler (triggers the actual sync)
$timerAction = {
    Sync-SavesToGit
}

# File change event handler
$changeAction = {
    $path = $Event.SourceEventArgs.FullPath
    $changeType = $Event.SourceEventArgs.ChangeType
    $fileName = Split-Path $path -Leaf
    
    # Only process save files
    $isRelevantFile = $false
    foreach ($fileType in $FileTypes) {
        if ($fileName -like $fileType) {
            $isRelevantFile = $true
            break
        }
    }
    
    if ($isRelevantFile) {
        $script:lastChangeTime = Get-Date
        Write-Log "Detected change: $fileName ($changeType)"
        
        # Reset the timer (debounce multiple rapid changes)
        $timer.Stop()
        $timer.Start()
    }
}

# Register event handlers
Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $changeAction
Register-ObjectEvent -InputObject $watcher -EventName Created -Action $changeAction
Register-ObjectEvent -InputObject $watcher -EventName Deleted -Action $changeAction
Register-ObjectEvent -InputObject $watcher -EventName Renamed -Action $changeAction
Register-ObjectEvent -InputObject $timer -EventName Elapsed -Action $timerAction

Write-Log "File watcher is now active. Press Ctrl+C to stop."

# Keep the script running
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    # Cleanup
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    $timer.Dispose()
    Write-Log "File watcher stopped"
}
