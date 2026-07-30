# SimpleIDE - Project Architecture

## Project Overview
**SimpleIDE** is a lightweight, professional VB.NET IDE built with GTK# 3 on Linux using .NET 8.0. The IDE provides VS Code-like functionality with a focus on VB.NET development.

## Technical Stack
- **Framework**: .NET 8.0
- **UI Toolkit**: GTK# 3.24.24.38
- **Language**: VB.NET
- **Platform**: Linux (primary), Cross-platform capable
- **Build System**: dotnet CLI integration

## Cross-Platform Compatibility

### Verified Platforms
- **Linux** (Primary development platform)
- **Windows 10/11** (Tested and working without modification)

### Architecture Benefits
- **Single codebase** for all platforms
- **GTK# 3** provides consistent UI across OSes
- **.NET 8.0** offers native performance on each platform
- **Path handling** uses platform-agnostic System.IO methods

### Platform-Specific Considerations
- **File paths**: Handled via System.IO.Path (automatically uses `\` on Windows, `/` on Linux)
- **Line endings**: Environment.NewLine provides correct platform-specific endings
- **UI rendering**: GTK# adapts to native look-and-feel on each platform

## Current Architecture

### Core Components

#### 1. Main Application (`MainWindow`)
- **Primary File**: `MainWindow.vb`
- **Architecture**: Partial class structure with feature separation
- **Key Partials**: 
  - `MainWindow.Editors.vb` - Tabbed editor management
  - `MainWindow.Build.vb` - Build system integration
  - `MainWindow.Projects.vb` - Project explorer functionality
  - `MainWindow.Settings.vb` - Settings management

#### 2. Editor System (`Editors/` namespace)
- **CustomDrawingEditor**: Advanced text editor with syntax highlighting
- **Syntax Highlighting**: VB.NET parser integration
- **Line Numbers**: Click-and-drag selection support
- **Undo/Redo**: Per-character tracking system

#### 3. Project Management (`Models/Projects/`)
- **VBProject**: .vbproj file parsing and management
- **ProjectExplorer**: Tree-based project navigation
- **File System Integration**: Real-time file monitoring

#### 4. Build System (`Utilities/Build/`)
- **Async Build Operations**: dotnet CLI integration
- **Output Parsing**: Error and warning extraction
- **Navigation**: Click-to-error functionality

#### 5. Settings & Configuration (`Utilities/Settings/`)
- **SettingsManager**: Centralized persistence
- **Theme Support**: CSS-based styling system
- **Key Bindings**: Customizable keyboard shortcuts

### Key Integration Points

#### Editor ↔ Project System
- File opening/closing coordination
- Dirty state tracking
- Project context awareness

#### Build System ↔ UI
- Async build progress reporting
- Error navigation integration
- Status bar updates

#### Settings ↔ All Components
- Centralized configuration management
- Real-time theme application
- Persistent user preferences

## File Organization

SimpleIDE/
├── MainWindow.*.vb # Primary application window (partial classes)
├── Editors/ # Text editor components
│ ├── CustomDrawingEditor.vb
│ └── Syntax/ # Syntax highlighting
├── Models/ # Data models
│ ├── Projects/ # Project management
│ ├── Settings/ # Configuration models
│ └── Syntax/ # Parser models
├── Utilities/ # Helper classes
│ ├── Build/ # Build system
│ ├── Settings/ # Settings management
│ └── Helpers/ # General utilities
├── Widgets/ # UI components
│ ├── DockPanels/ # Dockable panels
│ └── Dialogs/ # Dialog windows
└── Resources/ # Embedded resources


## Data Flow Patterns

### 1. Event-Driven Communication
- Component separation through events
- Loose coupling between major systems
- Centralized event handling in MainWindow

### 2. Settings Propagation
- SettingsManager as single source of truth
- Change notification through events
- Delayed application for batch updates

### 3. Build Process Flow
1. User triggers build (F6/Ctrl+Shift+B)
2. MainWindow coordinates build start
3. BuildManager executes async dotnet commands
4. Output parsed and formatted
5. Results displayed in Build Output panel
6. Errors become clickable navigation points

## Current Feature Status

### ✅ Fully Implemented
- Multi-file tabbed editor with VB.NET syntax highlighting
- Project Explorer with .vbproj file parsing
- Build system integration with async operations
- Dockable build output panel with error navigation
- Line numbers with selection functionality
- Settings window with theme support
- Enhanced status bar with cursor position
- Keyboard shortcuts system
- Welcome splash screen
- Undo/Redo system with per-character tracking
- API integration for Claude.AI
- Integrated Help System

### 🔄 In Progress
- Microsoft VB Parser integration (refactoring)
- Enhanced IntelliSense engine
- Improved error handling and recovery

### 📋 Planned Enhancements
- Advanced code refactoring tools
- Extended language support
- Plugin system architecture
- Performance optimizations

## Architecture Principles

### 1. Separation of Concerns
- Each component has single responsibility
- Clear boundaries between UI, business logic, and data
- Minimal dependencies between namespaces

### 2. Event-Driven Design
- Components communicate through events
- MainWindow coordinates cross-component interactions
- Asynchronous operations for UI responsiveness

### 3. Extensibility
- Partial class pattern for MainWindow features
- Plugin-ready architecture
- Configurable through settings system

### 4. Maintainability
- Comprehensive XML documentation
- Consistent coding conventions
- Centralized error handling
- Modular testing approach

## Development Philosophy

### Incremental Development
- Each feature perfected before moving to next
- Mental testing before implementation
- Existing architecture pattern preservation
- Working functionality maintenance

### Quality Assurance
- Comprehensive error handling with try-catch blocks
- Console.WriteLine for debugging throughout
- No floating code - all code inside methods/properties
- One class per file organization, exception for large partial class files.


