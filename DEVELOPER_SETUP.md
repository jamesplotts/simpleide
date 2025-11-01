# SimpleIDE - Developer Setup Guide

## Prerequisites

### 1. Linux Environment (Primary)
- MX Linux (primary development platform) 
- Other Debian-based distributions: Ubuntu, Debian, Linux Mint
- Desktop Environment: XFCE (MX Linux default), GNOME, KDE with GTK# support

### 2. Windows Environment (Verified Working)
- Windows 10/11 - Tested and working without code modifications
- Same codebase as Linux version
- Automatic path handling works cross-platform

### 3. .NET 8.0 SDK

#### MX Linux Installation:
Method 1: Official Microsoft install script
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

Method 2: Package manager (if available in MX repos)
sudo apt update
sudo apt install dotnet-sdk-8.0

Add to PATH (add to ~/.bashrc)
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools

#### Windows Installation:
Download from Microsoft .NET Downloads page
Run installer with ".NET 8.0 Runtime" and ".NET 8.0 SDK" selected

### 4. GTK# 3.0 Development Libraries

#### MX Linux (Debian-based):
sudo apt update
sudo apt install gtk-sharp3 libgtk-3-dev libgirepository1.0-dev

#### Windows:
Automatically handled via NuGet during build
No manual installation required

### 5. Additional MX Linux Dependencies
Ensure all build essentials are available
sudo apt install build-essential
sudo apt install pkg-config

For additional VB.NET tooling (optional)
sudo apt install mono-devel mono-vbnc

## Project Setup

### 1. Clone Repository
git clone https://github.com/jamesplotts/simpleide.git
cd simpleide

### 2. Verify Project Structure
simpleide/
├── SimpleIDE.sln           # Main solution file
├── SimpleIDE.vbproj        # Main project file
├── MainWindow.vb           # Primary application window
├── Editors/                # Text editor components
├── Models/                 # Data models
├── Utilities/              # Helper classes
├── Widgets/                # UI components
└── Resources/              # Embedded resources

### 3. Restore Dependencies
dotnet restore SimpleIDE.sln

### 4. Build Project
Debug build
dotnet build SimpleIDE.sln --configuration Debug

Release build  
dotnet build SimpleIDE.sln --configuration Release

Clean rebuild (if needed)
dotnet clean SimpleIDE.sln
dotnet build SimpleIDE.sln --configuration Debug

### 5. Run Application
Development run
dotnet run --project SimpleIDE.vbproj

Or run built executable
./bin/Debug/net8.0/SimpleIDE

## Development Environment

### Recommended IDEs/Editors for MX Linux

#### 1. Visual Studio Code
Install via MX Package Installer or:
sudo apt install code

Recommended Extensions:
- VB.NET Language Support
- .NET Extension Pack
- GitLens

#### 2. MonoDevelop (Optional)
sudo apt install monodevelop

#### 3. Vim/Neovim with VB.NET plugins
Suitable for lightweight editing alongside main IDE

### MX Linux Specific Tips

#### XFCE Integration
GTK# applications integrate naturally with XFCE desktop
Font rendering optimized for XFCE's GTK3 environment
Window management works seamlessly with XFCE compositor

#### Performance on MX Linux
Excellent performance on Debian-stable base
Minimal resource usage compared to heavier IDEs
Fast build times with .NET 8.0 Native AOT potential

## Cross-Platform Development

### Verified Compatibility
MX Linux: Primary development platform, fully supported
Windows 10/11: Tested and working without modification
Other Linux: Should work on any .NET 8.0 + GTK# supported distribution

### Platform-Agnostic Code Patterns
Your codebase demonstrates excellent cross-platform design:
Path handling: System.IO.Path methods work on all platforms
Line endings: Environment.NewLine provides correct platform-specific endings
GTK# portability: Same UI framework across platforms
File system: .NET abstractions handle platform differences

## Build System Integration

### Project File Structure
Uses modern SDK-style project format with GTK# support.

### Build Commands
Development build with debugging
dotnet build --configuration Debug

Production build
dotnet build --configuration Release

Build with specific runtime (experimental)
dotnet build --runtime linux-x64

## Common Issues and Solutions

### MX Linux Specific Issues

#### 1. GTK# Not Found
Ensure GTK# runtime is installed
sudo apt install gtk-sharp3
Verify installation
ls /usr/lib/cli/gtk-sharp-3.0/

#### 2. Missing Dependencies
Install all potential missing dependencies
sudo apt install libgtk-3-0 libgirepository1.0-dev libwebkit2gtk-4.0-dev

#### 3. Font Rendering Issues
Install Microsoft core fonts
sudo apt install ttf-mscorefonts-installer

Or set fallback font in application settings

### General Build Issues

#### 1. NuGet Package Restoration
Clear NuGet cache
dotnet nuget locals all --clear

Restore packages
dotnet restore --force

#### 2. File Permission Issues
Ensure execute permissions on scripts
chmod +x *.sh

Check build output permissions
ls -la bin/Debug/net8.0/

## Testing on MX Linux

### Basic Functionality Tests
1. Editor: Open and edit VB.NET files with syntax highlighting
2. Build System: Compile sample VB.NET projects
3. Project Explorer: Navigate project structure
4. Settings: Persistence across application restarts

### XFCE Integration Tests
Window management (minimize, maximize, close)
Menu bar integration
File dialog native integration
Theme consistency with XFCE appearance settings

## Performance Optimization for MX Linux

### Build Optimization
Parallel build
dotnet build --maxCpuCount

Incremental build (development)
dotnet build --no-incremental false

### Runtime Optimization
Use Release configuration for performance testing
Monitor memory usage with system monitor
Profile with dotnet-counters for performance analysis

## Contributing from MX Linux

### Development Workflow
1. Code in preferred editor (VS Code recommended)
2. Build and test locally
3. Run comprehensive functionality tests
4. Submit pull requests with detailed MX Linux testing notes

### MX Linux Specific Testing
Verify XFCE desktop integration
Test with multiple XFCE themes
Confirm proper font rendering
Validate system resource usage

## Additional Resources

### MX Linux Specific
MX Linux Documentation: https://mxlinux.org/wiki/
Debian Package Search: https://packages.debian.org/search

### .NET on Linux
.NET on Linux Documentation: https://docs.microsoft.com/dotnet/core/install/linux
GTK# for .NET: https://github.com/GtkSharp/GtkSharp

### Community Support
.NET Foundation: https://dotnetfoundation.org/
MX Linux Forums: https://forum.mxlinux.org/

