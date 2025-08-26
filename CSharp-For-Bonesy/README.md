# D2GitSync for D2RLAN Integration

**Hey Bonesy! Here's your C# implementation ready to drop into D2RLAN! 🚀**

## What You Get

This is a complete C# port of the D2GitSync functionality, designed specifically for integration into your D2RLAN launcher. Using your genius symlink idea instead of file copying!

## 📁 File Structure

```
CSharp-For-Bonesy/
├── D2GitSync.Core/           ← The library you'll integrate
│   ├── D2SaveSyncService.cs  ← Main service
│   ├── LauncherIntegration.cs ← Your integration hooks
│   ├── SymlinkManager.cs     ← Symlink magic (your idea!)
│   ├── GitService.cs         ← Git operations
│   ├── FileWatcherService.cs ← File monitoring
│   └── SyncConfiguration.cs  ← Settings management
├── D2GitSync.Demo/           ← Working example/test app
└── D2GitSync.sln            ← Visual Studio solution
```

## 🔥 Key Innovation: Symlinks > File Copying

Instead of copying files back and forth like the original:
```
❌ Old: D2R Saves ←→ [Copy Files] ←→ Git Repo
✅ New: D2R Saves → [Symlinks] → Git Repo (game writes directly to Git!)
```

**Why this is brilliant:**
- Zero latency - no file copying delays
- Simpler code - no sync timing issues  
- More reliable - game writes directly to tracked files
- Cleaner architecture - one source of truth

## 🎯 Integration is Super Easy

### Step 1: Add to Your Project
```xml
<!-- Add to D2RLAN.csproj -->
<ProjectReference Include="path\to\D2GitSync.Core\D2GitSync.Core.csproj" />
```

### Step 2: Initialize (in your launcher startup)
```csharp
using D2GitSync.Core;

public class D2RLauncher 
{
    private D2RLauncherIntegration _syncIntegration;
    
    public async Task InitializeAsync()
    {
        _syncIntegration = new D2RLauncherIntegration(logger);
        await _syncIntegration.InitializeAsync();
        
        // Hook up events for UI updates
        _syncIntegration.StatusChanged += OnSyncStatusChanged;
        _syncIntegration.ErrorOccurred += OnSyncErrorOccurred;
    }
}
```

### Step 3: Hook Into Game Lifecycle
```csharp
// When user clicks "Launch D2R"
public async Task LaunchGameAsync()
{
    var savesPath = GetCurrentSavesPath(); // You already have this
    
    // Start sync service
    await _syncIntegration.OnGameStartingAsync(savesPath);
    
    // Launch D2R (your existing code)
    StartD2RProcess();
}

// When D2R process ends
public async Task OnGameProcessExitedAsync()
{
    // Stop sync service and do final sync
    await _syncIntegration.OnGameEndedAsync();
}
```

### Step 4: Add UI Settings (optional but recommended)
```csharp
// Get current settings
var config = _syncIntegration.GetCurrentConfiguration();

// Show in your settings UI:
// - config.AutoSyncEnabled (checkbox)
// - config.GitRepositoryPath (folder picker)  
// - config.RemoteRepositoryUrl (text input)

// Save changes
await _syncIntegration.UpdateConfigurationAsync(updatedConfig);
```

### Step 5: Status Updates (for your UI)
```csharp
private void OnSyncStatusChanged(object sender, SyncStatusEventArgs e)
{
    // Update status bar, show notifications, etc.
    UpdateStatusBar($"Sync: {e.Message}");
}

private void OnSyncErrorOccurred(object sender, SyncErrorEventArgs e)
{
    // Show error notification to user
    ShowNotification($"Sync Error: {e.Message}", NotificationType.Error);
}
```

## 🎮 How It Works for Users

1. **First Time Setup**: User sets Git repository path in D2RLAN settings
2. **Optional Remote**: User can configure GitHub/GitLab URL for multi-PC sync  
3. **Launch Game**: D2RLAN starts sync service automatically
4. **Play Normally**: Game saves directly to Git-tracked files via symlinks
5. **Auto Sync**: Changes are committed and pushed in real-time
6. **Multi-PC**: Latest saves pulled when launching on different computer
7. **Game Exit**: Final sync performed when D2R closes

## 🔧 Configuration Settings You Can Expose

```csharp
public class SyncConfiguration
{
    public bool AutoSyncEnabled { get; set; }           // Main on/off toggle
    public string GitRepositoryPath { get; set; }       // Where to store Git repo  
    public string RemoteRepositoryUrl { get; set; }     // Optional: GitHub/GitLab URL
    public int DebounceSeconds { get; set; }            // Advanced: sync delay
    // SavesPath is auto-detected from your launcher
}
```

## 🏃‍♂️ Test It Out

1. Open `D2GitSync.sln` in Visual Studio
2. Run the Demo project to see it in action
3. Try creating files in the saves directory
4. Watch the Git commits happen automatically

## 💡 Benefits Over Original Batch Version

### For Users:
- ✅ **Integrated UI** - Settings in D2RLAN instead of editing batch files
- ✅ **No separate .bat files** - Everything within your launcher  
- ✅ **Auto-configuration** - You already know the saves paths
- ✅ **Real-time status** - Live updates in launcher UI
- ✅ **No admin required** - Runs as normal user
- ✅ **No PowerShell issues** - Pure .NET, no execution policies

### For You (Development):
- ✅ **Type safety** - C# vs. loosely typed scripts
- ✅ **Better error handling** - Proper exception management  
- ✅ **Professional logging** - Structured logging with levels
- ✅ **Unit testable** - Can write proper tests
- ✅ **Easy deployment** - Ships with your launcher
- ✅ **Auto-updates** - Updates with D2RLAN, no separate downloads

## 🚨 Requirements

- .NET 6.0+ (you're probably already using this)
- Git for Windows (automatically installed if missing!)
- Standard D2R saves directory access

### 🎯 Automatic Dependency Installation

**No more "user doesn't have Git" problems!** The C# version can automatically install Git using winget:

```csharp
// Check what's missing
var status = await integration.CheckDependenciesAsync();
Console.WriteLine($"Git available: {status.IsGitAvailable}");
Console.WriteLine($"Can auto-install: {status.CanAutoInstall}");

// Install automatically  
if (!status.HasAllRequiredDependencies && status.CanAutoInstall)
{
    await integration.InstallDependenciesAsync();
}
```

**What gets installed automatically:**
- ✅ **Git for Windows** - Required for all Git operations
- ✅ **GitHub CLI** - Optional, for enhanced GitHub integration

**Fallback:** If winget isn't available, provides clear manual installation instructions.

## 🤝 What I've Made Easy for You

1. **Clean interface** - `ID2RLauncherIntegration` defines exactly what you need
2. **Error handling** - Won't crash your launcher if Git issues occur
3. **Async throughout** - Won't block your UI  
4. **Event-driven** - Easy to wire up to your existing UI patterns
5. **Configuration flexible** - Adapts to however you store settings
6. **Logging integration** - Uses standard .NET logging you probably already have

## 🔥 The Integration Points are CLEAN

The hardest part will be adapting the configuration persistence to match your settings system, but I've made the interface flexible for that. Everything else should drop right in!

Want to see it running? Check out `D2GitSync.Demo` - it shows exactly how the integration points work.

**This should be like a 1-2 day integration tops.** The heavy lifting is all done! 🎯
