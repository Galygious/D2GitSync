# Original D2GitSync Implementation

This folder contains the original PowerShell/Batch implementation for reference.

## Files

- **`D2GitSync.bat`** - Original main launcher script
- **`D2SaveWatcher.ps1`** - Original PowerShell file monitoring service
- **`README.md`** - Original project documentation
- **`SETUP_GUIDE.md`** - Original setup instructions  
- **`RELEASE_NOTES.md`** - Original release notes

## Purpose

These files are kept for reference to understand the original implementation and approach. The C# version in `../CSharp-For-Bonesy/` replaces all of this functionality with a cleaner, more integrated approach.

## Key Differences

### Original Approach:
- Batch file orchestration
- PowerShell file monitoring service
- File copying between saves directory and Git repository
- Separate executables user must run
- Manual configuration via editing batch files

### New C# Approach:
- Integrated C# library
- Native .NET file monitoring
- Symlinks (no file copying needed!)
- Embedded in D2RLAN launcher
- UI-based configuration

The C# version eliminates the complexity while providing a much better user experience.