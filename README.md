# D2R Git Save Sync System

This directory contains all the components for the Diablo 2 Resurrected save file synchronization system.

## Files

- **`D2RLaunch.bat`** - Main launcher that handles git sync and starts D2R
- **`D2SaveWatcher.ps1`** - PowerShell service that monitors save file changes
- **`InstallService.bat`** - Installs the file watcher as a Windows service
- **`ServiceManager.bat`** - GUI tool to manage the Windows service
- **`TestWatcher.ps1`** - Test script to validate functionality
- **`SETUP_GUIDE.md`** - Complete setup and configuration guide

## Quick Start

1. **First Time Setup**: Run `D2RLaunch.bat` to initialize git repository
2. **Configure Remote**: Set up your GitHub/GitLab repository for sync
3. **Daily Use**: Run `D2RLaunch.bat` to play with automatic save sync

## Configuration

Update these paths in `D2RLaunch.bat` if needed:
```batch
set "SAVES_PATH=C:\Users\%USERNAME%\Saved Games\Diablo II Resurrected"
set "GIT_REPO_PATH=D:\D2RSaves"
set "LAUNCHER_PATH=D:\D2RLaunch\D2RLAN\Launcher\D2RLAN.exe"
```

## Features

✅ **Real-time sync** - Saves backed up within seconds of changes  
✅ **Modded save support** - Handles subdirectories for different mods  
✅ **Multi-machine sync** - Same saves across all your computers  
✅ **Automatic operation** - No manual intervention required  
✅ **Offline capable** - Works without internet, syncs when available  

## Usage Options

**Option 1: Manual Launch (Recommended)**
- Run `D2RLaunch.bat` when playing
- File watcher runs in background
- Close window when done to stop monitoring

**Option 2: Windows Service (Advanced)**
- Run `InstallService.bat` to install as service
- Always monitoring, starts with Windows
- Use `ServiceManager.bat` to control

For detailed setup instructions, see `SETUP_GUIDE.md`.
