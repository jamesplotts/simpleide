
# SimpleIDE Project

## Project Overview
You are assisting with the development of **SimpleIDE** - a lightweight, professional VB.NET IDE built with GTK# 3 on Linux using .NET 8.0. The IDE is similar to VS Code in functionality and includes:

### Current Features (Working)
- Multi-file tabbed editor with VB.NET syntax highlighting
- Project Explorer with .vbproj file parsing
- Build system integration with async dotnet CLI operations
- Dockable build output panel with error/warning navigation
- Line numbers with click-to-select and drag-to-select functionality
- Settings window with theme support and persistence
- Enhanced status bar with cursor position, language mode, and encoding
- Keyboard shortcuts (Ctrl+S, Ctrl+Y, Ctrl+G, Ctrl+A, F6, Shift+F6, Ctrl+Shift+B)
- Welcome splash screen with scaled icon when no files are open
- **Undo/Redo system** (Ctrl+Z, Ctrl+R) with per-character tracking
- API integration for Claude.AI, providing implementation for multiple chats, and Claude projects
- Integrated Help System

## Mandatory Coding Conventions (FOLLOW STRICTLY)

### 1. Hungarian Notation (ENFORCED)
- **Variables**: `l` = Local, `p` = Private field, `v` = Parameter, `g` = Global
- **Types**: `i` = Integer, `b` = Boolean (when needed for clarity)
- Default is String, no prefix needed
- **Objects** get capital letter: `lButton`, `pTreeView`, `vTextBuffer`

### 2. Enum Pattern (MANDATORY) (CRITICAL)
```vb
Public Enum ExampleEnum
    eUnspecified
    eFirstValue
    eSecondValue
    eLastValue
End Enum
```
- Always start with `eUnspecified`
- Always end with `eLastValue`
- All values prefixed with `e`

### 3. Method and Event Patterns
- **Method names**: PascalCase (`SaveSettings`, `LoadColorPreferences`)
- **Events**: `On[Event]` pattern (`OnColorChanged`, `OnSettingsApplied`)
- **Event handlers**: Use `AddHandler` syntax, not `Handles`

### 4. GTK# Specific Rules
- Always fully qualify `System.IO.Path` (GTK has its own Path)
- Use `Environment.NewLine` not `vbNewLine`
- Use CSS providers for fonts, not deprecated `ModifyFont()`
- Always call `ShowAll()` after adding GTK widgets
- Use `StyleProviderPriority` as UInteger values (USER = 800)
- Cast Gdk.Key values when comparing to KeyPressEventArgs.Event.Key to avoid ambiguity

### 5. Code Quality Rules
- `Try-Catch` blocks everywhere with `Console.WriteLine` for debugging
- Comments: Use `' TODO:`, `' FIXED:`, `' NOTE:` prefixes
- No floating code: All code must be inside methods/properties
- One class per file: Each class gets its own .vb file
- DO NOT GUESS METHOD NAMES WHEN PROVIDING CHANGES TO CODE!
- Research the actual method names that exist in the codebase.

### 6. Architecture Principles
- Separation of concerns
- Event-driven communication between forms
- Use partial classes for large forms (`MainWindow.*.vb` pattern)
- Centralized persistence logic in `SettingsManager`


## Technical Stack
- **GTK#** 3.24.24.38 on .NET 8.0
- **VB.NET** with proper namespace structure (root `SimpleIDE` namespace)
- **Linux** file paths (use `/` not `\`)
- Resources embedded in assembly

## Known Issues and Solutions
- **TextBuffer ambiguity**: Use `SelectRange` + `DeleteSelection` instead of `Delete`
- **Line numbers not showing**: Check `NoShowAll`, parent visibility, and size allocation
- **Build output paths**: Handle both absolute and relative paths
- **Icon loading**: Resource name is `SimpleIDE.icon.png` (not `SimpleIDE.Resources.icon.png`)

## Implementation Approach
1. **Incremental development**: Get each feature working perfectly before moving to next
2. **Test mentally** before suggesting implementation
3. **Maintain existing architecture** patterns
4. **Preserve all working functionality**
5. **Use existing Models and Utilities** where possible

## When Providing Solutions
1. Follow ALL coding conventions without exception
2. Use proper partial class structure for MainWindow extensions
3. Implement comprehensive error handling with try-catch blocks
4. Use the existing CssHelper utility for styling
5. Maintain the event-driven architecture
6. Test that new code integrates with existing systems

## VB.NET Namespace Rules (CRITICAL)

### Root Namespace Handling
- This project has a root namespace: `SimpleIDE`
- **NEVER** declare the root namespace in code files
- Files in the root namespace should have NO namespace declaration
- Files in sub-namespaces should declare ONLY the sub-namespace part

### Namespace Declaration Examples:
```vb
' File in root namespace (SimpleIDE) - NO namespace declaration
Public Class MainWindow
    ' This class is automatically in SimpleIDE namespace
End Class

' File in sub-namespace (SimpleIDE.Utilities) - declare ONLY "Utilities"
Namespace Utilities
    Public Class FileHelper
        ' This class is automatically in SimpleIDE.Utilities
    End Class
End Namespace

' File in nested sub-namespace (SimpleIDE.Models.Syntax) - declare ONLY "Models.Syntax"
Namespace Models.Syntax
    Public Class SyntaxNode
        ' This class is automatically in SimpleIDE.Models.Syntax
    End Class
End Namespace
```

Import Rules

ALWAYS use full imports from within the project
NEVER use fully qualified names for project type declarations
The root namespace is automatically available

Import Examples:
```vb
' CORRECT - importing from within the project
Imports SimpleIDE.Utilities     ' include root namespace
Imports SimpleIDE.Models        ' include root namespace

' WRONG - never write these
Imports Utilities           ' Doesn't import SimpleIDE.Utilities
Imports Models             ' Doesn't import SimpleIDE.Models
Imports Widgets.Editors    ' Doesn't import SimpleIDE.Widgets.Editors

' CORRECT - using types from the project
Dim lHelper As New FileHelper()              ' From SimpleIDE.Utilities
Dim lColorSet As New SyntaxColorSet()        ' From SimpleIDE.Models

' WRONG - never use fully qualified names for project types
Dim lHelper As New SimpleIDE.Utilities.FileHelper()     ' Don't do this
Dim lColorSet As New SimpleIDE.Models.SyntaxColorSet()  ' Don't do this
```

Key Rules:

Root namespace (SimpleIDE) is NEVER declared in namespace statements
Sub-namespaces declare ONLY their relative path from root
Imports use full paths with the root namespace
Never fully qualify types from within the project

## Solution Formulation Research (CRITICAL)

### Perform Comprehensive Research
Search ALL project files with every prompt to understand the context of code implementation and suggestions.
Do not use non-existent member declarations in examples.
Be aware of the potential for mistakes and attempt to avoid mistakes that can result in incomplete or incorrect solutions and unnecessarily waste James' tokens.
If your research reveals a better implementation, provide information about it to the user, and allow the user to determine if he wishes you to provide the implementation.

AGAIN - Perform Comprehensive Research

## Debugging compiler errors and warnings:
First, search for and identify the actual compiler error messages before proposing any solutions.

# XML Documentation Convention (ADD TO PROJECT DESCRIPTION)

## Mandatory XML Documentation Comments (ALWAYS REQUIRED)

### 1. Documentation Requirements
**EVERY** public or protected member MUST have XML documentation comments:
- Classes, Structures, Modules, Interfaces
- Properties (including auto-properties)
- Methods, Functions, Subs
- Events
- Fields (if public/protected)
- Enums and their members
- Constructors

### 2. Standard XML Documentation Format
```vb
''' <summary>
''' Brief description of what the member does (REQUIRED)
''' </summary>
''' <param name="vParameterName">Description of parameter purpose and valid values</param>
''' <returns>Description of return value (for Functions only)</returns>
''' <remarks>Optional: Additional implementation details or usage notes</remarks>
''' <example>Optional: Usage example</example>
''' <exception cref="ExceptionType">When this exception is thrown</exception>
```

### 3. Documentation Examples

#### Class/Structure Documentation
```vb
''' <summary>
''' Manages syntax highlighting and code parsing for VB.NET source files
''' </summary>
''' <remarks>
''' This class maintains the document structure and provides real-time parsing
''' </remarks>
Public Class CustomDrawingEditor
```

#### Method Documentation
```vb
''' <summary>
''' Loads a source file from disk and parses its content
''' </summary>
''' <param name="vFilePath">Full path to the source file to load</param>
''' <param name="vEncoding">Text encoding to use (defaults to UTF-8)</param>
''' <returns>True if successfully loaded and parsed, False otherwise</returns>
''' <exception cref="IOException">Thrown when file cannot be accessed</exception>
Public Function LoadFile(vFilePath As String, Optional vEncoding As Encoding = Nothing) As Boolean
```

#### Property Documentation
```vb
''' <summary>
''' Gets or sets whether the editor content has been modified since last save
''' </summary>
''' <value>True if modified, False if pristine</value>
Public Property IsModified As Boolean
```

#### Event Documentation
```vb
''' <summary>
''' Raised when the cursor position changes in the editor
''' </summary>
''' <param name="vLine">New line number (0-based)</param>
''' <param name="vColumn">New column position (0-based)</param>
Public Event CursorPositionChanged(vLine As Integer, vColumn As Integer)
```

#### Enum Documentation
```vb
''' <summary>
''' Specifies the type of syntax node in the parse tree
''' </summary>
Public Enum NodeType
    ''' <summary>Unknown or unspecified node type</summary>
    eUnspecified
    ''' <summary>Class declaration node</summary>
    eClass
    ''' <summary>Method or function node</summary>
    eMethod
    ''' <summary>Property declaration node</summary>
    eProperty
    ''' <summary>Sentinel value for enum bounds checking</summary>
    eLastValue
End Enum
```

### 4. Documentation Style Rules

1. **Summaries must be actionable**:
   - Methods: Start with a verb ("Loads", "Saves", "Calculates", "Validates")
   - Properties: Start with "Gets", "Sets", or "Gets or sets"
   - Classes: Describe the responsibility ("Manages", "Represents", "Provides")

2. **Parameter descriptions must include**:
   - Purpose of the parameter
   - Valid value ranges or constraints
   - Special values (Nothing, empty string, etc.)
   - Units if applicable (pixels, milliseconds, etc.)

3. **Return value descriptions must specify**:
   - What the value represents
   - Special return values (Nothing, -1, empty collection)
   - Conditions for different return values

4. **Be concise but complete**:
   - Summary: One sentence, no period at end
   - Parameters/Returns: Complete sentences with periods
   - Remarks: Use for important implementation details

### 5. IntelliSense Integration Notes

The hover tooltips will display:
- **Summary** as the primary description
- **Parameters** when hovering over method calls
- **Returns** information for functions
- **Remarks** for additional context
- **Exceptions** to warn about error conditions

### 6. Private Member Documentation (REQUIRED)
**ALL members including private ones MUST be documented** - tooltips appear when hovering over ANY identifier in the code:
```vb
''' <summary>
''' Internal helper to validate line boundaries
''' </summary>
''' <param name="vLine">Line number to validate (0-based)</param>
''' <returns>True if line is within document bounds, False otherwise</returns>
Private Function IsValidLine(vLine As Integer) As Boolean
```

**Important**: When a developer hovers over any identifier in the code editor:
- The tooltip displays the XML documentation regardless of access level
- Private members show their documentation when referenced anywhere in the code
- This helps with understanding implementation details while coding

### 7. Critical Implementation Note
These XML comments are parsed by the VBParser and stored in the SyntaxNode structure, then accessed by the IntelliSense engine to provide hover tooltips and parameter hints. Missing documentation will result in no tooltip information being available.

Method Artifact Format (CRITICAL)

When providing code changes for individual methods, ALWAYS create separate artifacts for each method with the following format:

For REPLACING existing methods:

```vb
' Replace: [FullyQualifiedName]
[method code only - no imports, namespace, or class wrapper]
```

Example:

```vb
' Replace: SimpleIDE.Editors.CustomDrawingEditor.JoinLines
Public Sub JoinLines(vLine As Integer)
    ' method implementation
End Sub
```

For ADDING new methods:
```vb
' Add: [FullyQualifiedName]
' To: [PartialFileName]
[method code only - no imports, namespace, or class wrapper]
```

Example:

```vb
' Add: SimpleIDE.Editors.CustomDrawingEditor.GetTextInRange
' To: CustomDrawingEditor.Helpers.vb
Friend Function GetTextInRange(vStartLine As Integer, vStartColumn As Integer,
                              vEndLine As Integer, vEndColumn As Integer) As String
    ' method implementation
End Function
```

Rules:

One method per artifact - Never combine multiple methods in a single artifact
Method name in artifact title - Use a descriptive title like "GetTextInRange Method" or "JoinLines (Updated)"
No wrapper code - Never include Imports, Namespace, or Class declarations
Fully qualified names - Always use complete namespace path to avoid ambiguity
Clear action - Always specify "Replace:" or "Add:"
File hint for additions - When adding new methods, specify which partial file

Workflow Support:
These artifacts are designed to work with the IDE's Object Explorer integration:

User copies the fully qualified name from the comment
User searches in Object Explorer
User copies the entire artifact content
User right-clicks the method node and selects "Replace Method With Clipboard Contents" (for replacements)
Or adds to the specified file (for new methods)

This format will also enable future AI Assistant integration where these methods can be exposed as callable functions.

# Development Environment & Tools

## Available Tools for Claude

### Shell Execution (MCP Shell Server)
Claude has access to execute bash commands directly in the project directory through the MCP shell server configured in Claude Desktop. This tool is available as `shell:shell_exec` and runs commands with `/home/jamesp/Projects/VbIDE` as the working directory.

Common commands:
- `dotnet build` - Build the project
- `dotnet run` - Run the IDE
- `dotnet clean` - Clean build artifacts
- `git status` - Check git status
- Any bash command needed for development

### File System Access
Claude can read/write files in the project directory using the simpleide tools.

## Preferred Workflows

### When James reports a compilation error:
1. First run `dotnet build` to see the current errors
2. Search project files to understand the context
3. Provide complete fixed files (not fragments)
4. Run `dotnet build` again to verify the fix

### When making code changes:
1. Always search for existing implementations first
2. Maintain the established patterns (Hungarian notation, etc.)
3. Test changes by building: `dotnet build`
4. Provide complete files for replacement

### When debugging runtime issues:
1. Run `dotnet run` to reproduce the issue
2. Check relevant log files if any
3. Search codebase for error messages
4. Provide targeted fixes

### Code Style Preferences:
- Complete file replacements preferred over snippets
- Production-ready code, not examples
- Follow existing architecture patterns
- Maintain separation of concerns

## CRITICAL - File Editing Method

**ALWAYS use the `Filesystem:edit_file` tool for modifying existing files.**

DO NOT use `str_replace` - it has issues with file paths in this project.

The correct syntax is:
Filesystem:edit_file with parameters:

path: full path to file (e.g., /home/jamesp/Projects/VbIDE/SomeFile.vb)
edits: array of {oldText: "exact text to replace", newText: "replacement text"}


For file modifications:
1. First use `Filesystem:read_file` or `shell:shell_exec` with grep to see the current content
2. Then use `Filesystem:edit_file` to make changes
3. The tool will show a diff of the changes

Example:
- WRONG: Using str_replace
- RIGHT: Using Filesystem:edit_file with exact text matches

## File Creation
- Use `Filesystem:write_file` or `create_file` for NEW files only
- Use `Filesystem:edit_file` for modifying EXISTING files
- Never add unnecessary files - prefer modifying existing files when possible

  ## Theme System Integration (CRITICAL)

  ### How Themes Work
  - **ThemeManager** holds the current `EditorTheme` object with color definitions
  - `EditorTheme` contains properties like `BackgroundColor`, `ForegroundColor`, `CurrentLineColor`, etc.
  - All custom widgets MUST use `ThemeManager.GetCurrentThemeObject()` to retrieve colors

  ### Theme Integration Pattern for Custom Widgets
  **CORRECT approach:**
  ```vb
  Private pThemeManager As ThemeManager

  Public Sub SetThemeManager(vThemeManager As ThemeManager)
      pThemeManager = vThemeManager
      AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
      ApplyCurrentTheme()
  End Sub

  Private Sub OnThemeChanged(vTheme As EditorTheme)
      ApplyCurrentTheme()
  End Sub

  Private Sub ApplyCurrentTheme()
      Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
      ' Map EditorTheme colors to widget-specific colors
      pBackgroundColor = ParseColor(lTheme.BackgroundColor)
      pTextColor = ParseColor(lTheme.ForegroundColor)
      QueueDraw()
  End Sub
  ```

  WRONG approach - DO NOT hardcode theme colors:

  ```vb
  ' ❌ NEVER do this - hardcoded colors based on theme names
  Select Case lThemeName.ToLower()
      Case "dark"
          pBackgroundColor = New RGBA() with {.Red = 0.15, ...}
  ```

  Theme Color Mapping Guidelines

  - Use ParseColor() to convert hex strings to RGBA
  - Use DarkenColor() and LightenColor() for derived colors
  - Map EditorTheme properties to widget-specific colors consistently
  - Always subscribe to ThemeChanged event for dynamic updates

  ### 2. **Project Structure and Organization**

  ## Project Structure

  ### Folder Organization (Enforced)
  /SimpleIDE
  ├── Program.vb                    # Entry point (root namespace)
  ├── MainWindow.vb                 # Main window (root namespace)
  ├── MainWindow..vb               # Partial classes for MainWindow
  ├── /Editors                      # Text editors and specialized editors
  │   ├── CustomDrawingEditor.vb
  │   └── CustomDrawingEditor..vb  # Partial classes
  ├── /Widgets                      # Reusable UI components
  │   ├── CustomDrawNotebook.vb
  │   ├── CustomDrawProjectExplorer.vb
  │   └── *Panel.vb                 # Various panel widgets
  ├── /Managers                     # Business logic and coordination
  │   ├── ThemeManager.vb
  │   ├── SettingsManager.vb
  │   ├── ProjectManager.vb
  │   └── BuildManager.vb
  ├── /Models                       # Data structures and DTOs
  │   ├── EditorTheme.vb
  │   ├── TabInfo.vb
  │   └── SyntaxColorSet.vb
  ├── /Utilities                    # Helper functions and tools
  │   ├── FileOperations.vb
  │   └── CssHelper.vb
  ├── /Syntax                       # Parsing and highlighting
  │   ├── VBTokenizer.vb
  │   └── CodeSenseEngine.vb
  ├── /Dialogs                      # Modal dialogs
  ├── /Interfaces                   # Interface definitions
  └── /AI                          # AI integration components

  ### File Naming Conventions
  - **Partial classes**: `ClassName.Category.vb` (e.g., `MainWindow.Build.vb`)
  - **Widgets**: `CustomDraw[Name].vb` for custom-drawn controls
  - **Managers**: `[Responsibility]Manager.vb`
  - **One class per file** (except partial classes)

  ## Custom Widget Development

  ### Creating Custom-Drawn Widgets
  When creating widgets with manual Cairo rendering:

  1. **Base Structure**:
     ```vb
     Partial Public Class CustomDrawWidget
         Inherits Box  ' or DrawingArea

         Private pDrawingArea As DrawingArea
         Private pThemeManager As ThemeManager
         Private pSettingsManager As SettingsManager
     ```

  2. Initialization Pattern:
    - Create DrawingArea in constructor
    - Set event masks: ButtonPressMask, PointerMotionMask, etc.
    - Wire up draw handler: AddHandler pDrawingArea.Drawn, AddressOf OnDraw
    - Set CanFocus = True if widget needs keyboard input

  3. Drawing Method Template:

  ```vb
  Private Sub OnDraw(vSender As Object, vArgs As DrawnArgs)
      Try
          Dim lContext As Cairo.Context = vArgs.Cr
          Dim lWidth As Integer = pDrawingArea.AllocatedWidth
          Dim lHeight As Integer = pDrawingArea.AllocatedHeight

          ' Drawing code here

          vArgs.RetVal = True
      Catch ex As Exception
          Console.WriteLine($"OnDraw error: {ex.Message}")
      End Try
  End Sub
  ```

  4. Resource Cleanup:
    - Dispose Cairo contexts when done
    - Unsubscribe from events in Dispose
    - Clear pixbuf references

  5. Theme Integration:
    - MUST implement SetThemeManager() method
    - Subscribe to ThemeChanged event
    - Retrieve colors from EditorTheme object (never hardcode)
    - Call QueueDraw() when theme changes

  6. Partial Class Organization:
    - .vb - Core structure, properties, public API
    - .Drawing.vb - Cairo rendering code
    - .Events.vb - Mouse/keyboard event handlers
    - .Theme.vb - Theme integration
    - .Navigation.vb - Navigation logic (if applicable)

  ### 4. **Dependency Injection Pattern**

  ## Manager Dependency Pattern

  ### How Widgets Get Managers
  Widgets should NOT create their own managers. Instead:

  1. **MainWindow creates managers** in constructor:
     ```vb
     pSettingsManager = New SettingsManager()
     pThemeManager = New ThemeManager(pSettingsManager)
     pProjectManager = New ProjectManager()
     ```

  2. Managers passed to widgets via setter methods:

  ```vb
  ' In MainWindow initialization
  pProjectExplorer.SetThemeManager(pThemeManager)
  pProjectExplorer.SetProjectManager(pProjectManager)
  ```

  3. Widgets store manager references:

  ```vb
  Public Sub SetThemeManager(vThemeManager As ThemeManager)
      pThemeManager = vThemeManager
      AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
      ApplyCurrentTheme()
  End Sub
 ```

  This ensures single instances and proper initialization order.

  ### 5. **Performance Guidelines**


  ## Performance Best Practices

  ### Cairo Drawing
  - **Viewport culling**: Only draw visible items
    ```vb
    If lItemY < pScrollOffset OrElse lItemY > pScrollOffset + lVisibleHeight Then
        Return ' Skip off-screen items
    End If
    ```
  - Context management: Use Save()/Restore() for state isolation
  - Dispose resources: Always dispose Pixbufs, Patterns, Gradients

  GTK Performance

  - Batch updates: Use QueueDraw() instead of multiple redraws
  - Avoid ShowAll() on large hierarchies during updates
  - Event throttling: Use timers for high-frequency events (mouse move)
  - Lazy initialization: Create heavy widgets only when needed

  String Operations

  - Use StringBuilder for multiple concatenations
  - Cache frequently accessed string properties
  - Avoid repeated Split() operations on the same text

  ### 6. **Common Pitfalls**

  ## Common Mistakes to Avoid

  ### 1. TextBuffer Ambiguity
  ❌ `lBuffer.Delete(lStart, lEnd)` - Ambiguous
  ✅ `lBuffer.SelectRange(lStart, lEnd)` then `lBuffer.DeleteSelection(True, True)`

  ### 2. Hardcoded Theme Colors
  ❌ Setting colors based on theme name strings
  ✅ Retrieving colors from `EditorTheme` object via `ThemeManager`

  ### 3. ShowAll() Visibility Issues
  ❌ Calling `ShowAll()` then hiding specific widgets
  ✅ Set `NoShowAll = True` for widgets that should stay hidden

  ### 4. Path Separator Confusion
  ❌ Using `\` for paths (Windows style)
  ✅ Using `/` for paths or `System.IO.Path.Combine()`

  ### 5. Namespace Qualification
  ❌ `Imports Utilities` (without root namespace)
  ✅ `Imports SimpleIDE.Utilities`

  ### 6. Event Handler Memory Leaks
  ❌ Subscribing to events without unsubscribing in Dispose
  ✅ Always `RemoveHandler` in Dispose method

  ### 7. Conflicting Implementations
  ❌ Having multiple methods that do the same thing differently (like LoadThemeColors vs ApplyTheme)
  ✅ Single source of truth - one correct implementation
