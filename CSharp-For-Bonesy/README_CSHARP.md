# D2GitSync C# Port

This is a complete C# port of the original PowerShell/Batch D2GitSync tool, designed for integration with the D2RLAN launcher.

## Key Innovation: Symlinks Instead of File Copying

The brilliant insight from Bonesy about using symlinks transforms this from a file-copying solution to a seamless integration:

- **No file copying** - Game writes directly to Git-tracked files
- **Real-time sync** - Changes are immediately in the Git repository  
- **Zero latency** - No synchronization delays or temporary files
- **Cleaner architecture** - Much simpler than the original approach

## Architecture

### Core Library (`D2GitSync.Core`)

- **`D2SaveSyncService`** - Main orchestration service
- **`SymlinkManager`** - Creates/manages symlinks between saves dir and Git repo
- **`GitService`** - Handles all Git operations (init, commit, push, pull)
- **`FileWatcherService`** - Monitors save files with debouncing
- **`LauncherIntegration`** - Integration interface for D2RLAN

### Demo Application (`D2GitSync.Demo`)

- Console app demonstrating the library usage
- Shows the integration points Bonesy would use
- Interactive testing of sync functionality

## Integration Points for D2RLAN

Bonesy would integrate this by:

1. **Add the NuGet package** to D2RLAN project
2. **Initialize on startup:**
   ```csharp
   var integration = new D2RLauncherIntegration(logger);
   await integration.InitializeAsync();
   ```

3. **Hook into game lifecycle:**
   ```csharp
   // When starting D2R
   await integration.OnGameStartingAsync(savesPath);
   
   // When D2R process ends  
   await integration.OnGameEndedAsync();
   ```

4. **Add UI settings:**
   - Enable/disable auto-sync checkbox
   - Git repository path configuration
   - Remote repository URL setting
   - "Sync Now" button

5. **Handle events:**
   ```csharp
   integration.StatusChanged += (s, e) => UpdateStatusBar(e.Message);
   integration.ErrorOccurred += (s, e) => ShowNotification(e.Message);
   ```

## Benefits Over Original

### Technical Improvements
- **Type safety** - C# vs. loosely typed scripts
- **Better error handling** - Proper exception management
- **Professional logging** - Structured logging with levels
- **Async/await** - Modern async patterns vs. blocking operations
- **Unit testable** - Can be properly tested

### User Experience Improvements  
- **Integrated UI** - Settings in launcher instead of editing batch files
- **Real-time status** - Live status updates in launcher
- **Auto-configuration** - Launcher already knows save paths
- **Better error reporting** - User-friendly error messages
- **No separate executables** - Everything within launcher

### Operational Improvements
- **No PowerShell dependencies** - Pure .NET, no execution policy issues
- **No administrator privileges** - Runs as normal user
- **Cross-platform ready** - Works on Linux/Mac if needed
- **Auto-updates** - Updates with launcher, no separate downloads

## Configuration Example

```json
{
  "savesPath": "C:\\Users\\User\\Saved Games\\Diablo II Resurrected",
  "gitRepositoryPath": "D:\\D2RSaveSync", 
  "remoteRepositoryUrl": "https://github.com/user/d2r-saves.git",
  "autoSyncEnabled": true,
  "debounceSeconds": 3,
  "filePatterns": ["*.d2s", "*.ma*", "*.d2i"]
}
```

## How It Works

1. **Symlink Setup**: Creates symlinks from save directory to Git repository
2. **File Monitoring**: Watches for changes to save files
3. **Debounced Sync**: Waits for quiet period, then commits and pushes changes
4. **Cross-Machine Sync**: Pulls changes on startup to get latest saves

## Testing the Demo

1. Build the solution: `dotnet build`
2. Run the demo: `dotnet run --project D2GitSync.Demo`
3. Follow the interactive prompts to configure paths
4. Create/modify files in the saves directory to see sync in action

## Integration Effort

This should be **very easy** for Bonesy to integrate:

- **Core library** is complete and self-contained
- **Integration interface** provides clear hooks
- **Configuration system** can adapt to D2RLAN's settings
- **Error handling** is robust and user-friendly

The hardest part will be adapting the configuration persistence to match D2RLAN's settings system, but the interface is designed to make that straightforward.

## Deployment

The core library can be:
- **Embedded directly** in D2RLAN executable
- **Distributed as NuGet package** for easier updates
- **Side-by-side DLL** if preferred

All dependencies are standard .NET libraries that won't conflict with existing D2RLAN dependencies.
