# D2GitSync

**Automatic save file synchronization for Diablo 2 Resurrected**

D2GitSync automatically backs up and synchronizes your Diablo 2 Resurrected save files using Git, allowing you to:
- 🔄 **Never lose progress** - Real-time backup of all save changes
- 🖥️ **Play across multiple PCs** - Seamlessly continue on any computer
- 📖 **Track save history** - Complete version history of your characters
- 🎮 **Support all mods** - Works with modded saves and subdirectories
- ⚡ **Zero maintenance** - Automatic operation while you play

## Files

- **`D2GitSync.bat`** - Main launcher that handles git sync and starts D2R
- **`D2SaveWatcher.ps1`** - PowerShell service that monitors save file changes
- **`README.md`** - This file with quick start guide
- **`SETUP_GUIDE.md`** - Complete setup and configuration guide  
- **`RELEASE_NOTES.md`** - Version history and release information

## Quick Start

1. **First Time Setup**: Run `D2GitSync.bat` to initialize git repository
2. **Configure Remote**: Set up your GitHub/GitLab repository for sync
3. **Daily Use**: Run `D2GitSync.bat` to play with automatic save sync

## Configuration

Update these paths in `D2GitSync.bat` if needed:
```batch
set "SAVES_PATH=C:\Users\%USERNAME%\Saved Games\Diablo II Resurrected"
set "GIT_REPO_PATH=D:\D2RSaves"
set "LAUNCHER_PATH=D2R.exe"
```

## Features

✅ **Real-time sync** - Saves backed up within seconds of changes  
✅ **Modded save support** - Handles subdirectories for different mods  
✅ **Multi-machine sync** - Same saves across all your computers  
✅ **Automatic operation** - No manual intervention required  
✅ **Offline capable** - Works without internet, syncs when available  

## Usage Options

**Manual Launch**
- Run `D2GitSync.bat` when playing
- File watcher runs in background
- Close window when done to stop monitoring

For detailed setup instructions, see `SETUP_GUIDE.md`.
