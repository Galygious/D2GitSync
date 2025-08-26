# D2R Save Sync Setup Guide

This system automatically monitors your Diablo 2 Resurrected save files and syncs them to a git repository for backup and multi-machine synchronization.

## Quick Start

1. **Update Configuration** in `D2RLAN.bat`:
   ```batch
   set "SAVES_PATH=C:\Users\%USERNAME%\Saved Games\Diablo II Resurrected"
   set "GIT_REPO_PATH=D:\D2RSaves"
   ```

2. **Run the launcher** for the first time:
   ```cmd
   D2RLAN.bat
   ```

3. **Set up remote repository** (if desired):
   ```cmd
   cd D:\D2RSaves
   git remote add origin https://github.com/yourusername/d2r-saves.git
   git push -u origin main
   ```

## Files Created

- `D2SaveWatcher.ps1` - PowerShell file monitoring service
- `D2RLAN.bat` - Enhanced launcher with git integration
- `InstallService.bat` - Optional Windows service installer
- `ServiceManager.bat` - Service management utility

## How It Works

### Startup Process
1. **Git Repository Check**: Creates or initializes the git repository
2. **Pull Updates**: Downloads latest saves from remote repository
3. **Copy to Game**: Copies saves from git repo to game directory
4. **Start Watcher**: Launches file monitoring service in background
5. **Launch Game**: Starts D2RLAN.exe

### File Monitoring
- Monitors: `*.d2s` (characters), `*.ma*` (maps), `*.d2i` (items)
- **Debounced**: Waits 3 seconds after last change before syncing
- **Auto-commit**: Creates timestamped commits with computer name
- **Auto-push**: Pushes changes to remote repository

### File Types Monitored
- `.d2s` - Character save files
- `.ma*` - Map files (shared stash, etc.)
- `.d2i` - Item files

## Configuration Options

### Paths Configuration
Edit these variables in `D2RLAN.bat`:

```batch
:: Default Diablo 2 save location
set "SAVES_PATH=C:\Users\%USERNAME%\Saved Games\Diablo II Resurrected"

:: Where to store the git repository
set "GIT_REPO_PATH=D:\D2RSaves"

:: PowerShell watcher script location
set "WATCHER_SCRIPT=%~dp0D2SaveWatcher.ps1"
```

### Watcher Configuration
Edit these variables in `D2SaveWatcher.ps1`:

```powershell
$FileTypes = @("*.d2s", "*.ma*", "*.d2i")  # File types to monitor
$DebounceSeconds = 3  # Wait time before syncing after changes
```

## Usage Options

### Option 1: Manual Launch (Recommended)
- Run `D2RLAN.bat` when you want to play
- File watcher runs while the batch window is open
- Close the batch window when done playing

### Option 2: Windows Service (Advanced)
- Run `InstallService.bat` to install as Windows service
- Service runs automatically at startup
- Use `ServiceManager.bat` to control the service

## Remote Repository Setup

### GitHub Example
```bash
# Create repository on GitHub, then:
cd D:\D2RSaves
git remote add origin https://github.com/yourusername/d2r-saves.git
git branch -M main
git push -u origin main
```

### GitLab Example
```bash
cd D:\D2RSaves
git remote add origin https://gitlab.com/yourusername/d2r-saves.git
git branch -M main
git push -u origin main
```

### Private Git Server
```bash
cd D:\D2RSaves
git remote add origin user@yourserver.com:/path/to/d2r-saves.git
git push -u origin main
```

## Troubleshooting

### Common Issues

1. **Git not found**: Install Git for Windows
2. **PowerShell execution policy**: Run as administrator or use `InstallService.bat`
3. **Path doesn't exist**: Update `SAVES_PATH` in `D2RLAN.bat`
4. **Permission denied**: Run launcher as administrator

### Log Files
- `D2SaveWatcher.log` - Main watcher activity log
- `D2SaveWatcher_service.log` - Service output (if using service)
- `D2SaveWatcher_service_error.log` - Service errors (if using service)

### Manual Testing
Test the file watcher manually:
```powershell
powershell -ExecutionPolicy Bypass -File "D2SaveWatcher.ps1" -SavesPath "C:\Users\%USERNAME%\Saved Games\Diablo II Resurrected" -GitRepoPath "D:\D2RSaves"
```

## Security Considerations

- Keep your git repository private if it contains personal save data
- Consider using SSH keys instead of HTTPS for authentication
- The file watcher only monitors and copies save files, not executables

## Benefits

✅ **Real-time backup**: Saves are backed up within seconds of changes  
✅ **Multi-machine sync**: Play on different computers with same saves  
✅ **Version history**: Git provides complete history of save changes  
✅ **Automatic operation**: No manual intervention required  
✅ **Offline capable**: Works without internet, syncs when available  
✅ **Lightweight**: Minimal performance impact while gaming  

## Advanced Features

### Customizing Commit Messages
Edit the commit message format in `D2SaveWatcher.ps1`:
```powershell
$commitMessage = "Auto-sync: Save files updated on $env:COMPUTERNAME at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
```

### Adding More File Types
Add additional file patterns to monitor:
```powershell
$FileTypes = @("*.d2s", "*.ma*", "*.d2i", "*.key", "*.cfg")
```

### Changing Debounce Time
Adjust how long to wait before syncing:
```powershell
$DebounceSeconds = 5  # Wait 5 seconds instead of 3
```
