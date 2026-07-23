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

### 6. Desktop Integration & Icons on Linux (Wayland / KDE)

SimpleIDE shows its own penguin-and-VB icon in three separate places - the taskbar, the
Alt-Tab switcher, and the window's own title bar - and on a Wayland session (most notably
KDE Plasma, which defaults to Wayland) each of those is resolved differently. This section
explains what SimpleIDE does about each one and why, so nobody "cleans up" this code later
thinking it's redundant.

#### Taskbar / Alt-Tab icon - fixed via a `.desktop` file

Wayland compositors only show the correct taskbar/Alt-Tab icon for a GTK window if the
compositor can match the running window to an installed `.desktop` file via a matching
application ID. Without that match, Wayland falls back to a generic icon.

**The app handles this for you automatically.** The first time you run SimpleIDE on Linux,
it will offer to install desktop integration (a `.desktop` file plus a copy of the app icon,
both written to your personal `~/.local/share/` folders - no admin rights needed). Accept
the prompt, then fully quit and relaunch SimpleIDE for it to take effect. If you skip the
prompt, or want to reinstall/repair it later (e.g. after moving where you keep the repo),
use **Help > Install Desktop Integration** from the menu at any time.

**If you'd rather do it manually**, the app writes exactly this:

1. A `.desktop` file at `~/.local/share/applications/simpleide.desktop`:
   ```ini
   [Desktop Entry]
   Type=Application
   Name=SimpleIDE
   Comment=Lightweight VB.NET IDE
   Exec="/path/to/your/SimpleIDE"
   Icon=simpleide
   Terminal=false
   StartupWMClass=simpleide
   Categories=Development;IDE;
   ```
2. A copy of `Resources/icon.png` at `~/.local/share/icons/hicolor/<WxH>/apps/simpleide.png`,
   where `<WxH>` is the icon's *actual* pixel dimensions (read at install time via
   `Gdk.Pixbuf`, not hardcoded) - the freedesktop icon-theme spec requires the file to live
   in a folder matching its real size, and a mismatch here makes `gtk-update-icon-cache`
   reject the whole theme.
3. A call to `update-desktop-database ~/.local/share/applications` (if that tool is
   installed) so the desktop environment picks up the new file immediately rather than
   waiting for its next periodic scan.

The `StartupWMClass` value (`simpleide`) must match the program name the app sets at
startup via `GLib.Global.ProgramName` in `Program.vb` — that's the other half of the fix;
without it, GTK's default window class won't match what the `.desktop` file declares even
if the file itself is installed correctly.

#### Title-bar (window decoration) icon - fixed by forcing the X11 backend

The taskbar fix above does **not** cover the icon in the window's own title bar. On KDE
Plasma's *native* Wayland session, KWin does not resolve a GTK3 window's decoration icon
even when the `.desktop`/app-id match above is set up correctly - this was confirmed by
testing side-by-side under native Wayland (no icon) versus the same binary forced onto the
X11/XWayland backend (icon shows correctly), including with old, unmodified builds of
SimpleIDE from before this was ever an issue. It's a KWin/Wayland limitation, not something
wrong in SimpleIDE's own code - X11 window managers (and XWayland) get the icon directly
from GTK via the standard `_NET_WM_ICON` window property, a mechanism Wayland deliberately
does not expose to native clients.

**The app works around this automatically**: `Program.vb`'s `Sub Main` forces
`GDK_BACKEND=x11` before any other GTK/GLib code runs, which makes GTK render through
XWayland instead of natively. This only kicks in if `GDK_BACKEND` isn't already set in your
environment, so it never overrides a deliberate choice (e.g. exporting
`GDK_BACKEND=wayland` yourself to opt back into native Wayland rendering).

**Implementation note for contributors**: this is set via a direct P/Invoke to libc's own
`setenv()` (see `libc_setenv` in `Program.vb`), *not* `Environment.SetEnvironmentVariable`.
On this runtime, `Environment.SetEnvironmentVariable` was confirmed (by comparing against a
P/Invoke to `getenv()`) to update only .NET's own internal view of the environment, not the
real process environment that GLib/GTK read via `getenv()` - so it silently fails to affect
GTK's backend selection. If you ever need to set another env var that native/GTK code must
observe, use the same `libc_setenv` pattern, not the .NET API.

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

