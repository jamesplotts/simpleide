# SimpleIDE - Coding Conventions

## Mandatory Conventions (STRICTLY ENFORCED)

## 1. Hungarian Notation Prefixes

### Variable Scope Prefixes
- l = Local variable (Dim lVariable As String)
- p = Private field (Private pField As Integer)  
- g = Global (rarely used) (Public gGlobal As Boolean)
- v = Parameter (Sub Method(vParameter As String))

### Type Prefixes (When Needed for Clarity)
- i = Integer (Dim iCount As Integer)
- b = Boolean (Dim bFlag As Boolean)
- d = Double (Dim dValue As Double)
- Default is String - no prefix needed: Dim Name As String

### Object Variables
- Capital letter for objects: lButton, pTreeView, vTextBuffer
- Always specify the type: Dim lButton As Gtk.Button

## 2. Enum Pattern (CRITICAL)

### Standard Enum Structure
All enums must follow this exact pattern:
- ALWAYS start with eUnspecified
- ALWAYS end with eLastValue  
- ALL values prefixed with e
- ALL members require XML documentation

Example enum structure:
Public Enum NodeType
    eUnspecified
    eClass
    eMethod
    eProperty
    eLastValue
End Enum

## 3. Method and Event Patterns

### Method Naming
- Use PascalCase: SaveFile, LoadSettings, UpdateStatus
- Methods should be action-oriented verbs
- Always provide comprehensive XML documentation

### Event Patterns
- Event declaration: Public Event OnSettingsChanged(vSettings As AppSettings)
- Event handler attachment: Use AddHandler, NOT Handles keyword
- Event handler method: Private Sub OnSettingsChanged(vSettings As AppSettings)

## 4. GTK# Specific Rules

### Path Handling
- ALWAYS fully qualify System.IO.Path
- NEVER use just Path (conflicts with Gtk.Path)
- Example: Dim lFullPath As String = System.IO.Path.Combine(vDirectory, vFileName)

### New Line Characters
- USE Environment.NewLine
- NEVER use vbNewLine or vbCrLf
- Example: Dim lText As String = "Line 1" & Environment.NewLine & "Line 2"

### Font Styling
- USE CSS providers for fonts, not deprecated ModifyFont()
- Example CSS provider usage for font settings

### Widget Visibility
- ALWAYS call ShowAll() after adding widgets to containers
- Critical for making all child widgets visible

### Key Event Handling
- CAST Gdk.Key values to avoid ambiguity
- Example: If CType(vArgs.Event.Key, Gdk.Key) = Gdk.Key.Return Then

## 5. XML Documentation (REQUIRED FOR ALL MEMBERS)

### Class Documentation
Every class must have:
- Summary: Brief description of responsibility
- Remarks: Additional implementation details

### Method Documentation
Every method must have:
- Summary: What the method does (start with verb)
- Parameters: Description of each parameter purpose and valid values
- Returns: Description of return value (for functions)
- Exceptions: When exceptions are thrown

### Property Documentation
Every property must have:
- Summary: "Gets", "Sets", or "Gets or sets" description
- Value: What the property represents

### Private Member Documentation
ALL members including private ones must be documented since tooltips appear when hovering over any identifier in the code.

## 6. Code Quality Rules

### Error Handling
- Try-Catch blocks everywhere with Console.WriteLine for debugging
- Always log exceptions with message and stack trace
- Re-throw only when appropriate

### Comment Standards
- Use TODO: for planned implementations
- Use FIXED: for resolved issues
- Use NOTE: for important implementation details

### Organization
- NO floating code - all code inside methods/properties
- ONE class per file
- USE partial classes for large forms (MainWindow.*.vb pattern)

## 7. Namespace Rules (CRITICAL)

### Root Namespace Handling
- Root namespace: SimpleIDE
- NEVER declare root namespace in code files
- Files in root namespace should have NO namespace declaration

### Correct Namespace Examples:
- File in root namespace (MainWindow.vb): NO namespace declaration
- File in Utilities namespace: Declare ONLY "Namespace Utilities"

### Import Rules
- CORRECT: Imports SimpleIDE.Utilities, Imports SimpleIDE.Models
- WRONG: Imports Utilities (missing root namespace), Imports Models
- CORRECT: Dim lHelper As New FileHelper()
- WRONG: Dim lHelper As New SimpleIDE.Utilities.FileHelper()

## 8. Method Artifact Format (AI Development)

### For REPLACING Existing Methods:
Format: ' Replace: Fully.Qualified.Method.Name
Then the method code only - no imports, namespace, or class wrapper

### For ADDING New Methods:
Format: ' Add: Fully.Qualified.Method.Name
' To: PartialFileName.vb
Then the method code only - no imports, namespace, or class wrapper

## 9. Architecture Principles

### Separation of Concerns
- UI logic in MainWindow and Widgets
- Business logic in Models and Utilities
- Data persistence in SettingsManager

### Event-Driven Communication
- Use events for cross-component communication
- MainWindow coordinates complex interactions
- Avoid direct dependencies between unrelated components

### Centralized Persistence
- All settings through SettingsManager
- Consistent serialization/deserialization
- Change notification system

## 10. Solution Formulation Research

### Comprehensive Research Required
- Search ALL project files before implementing changes
- Understand context of code implementation
- Use existing member declarations - don't guess names
- If research reveals better implementation, inform user first

### Debugging Approach
- First identify actual compiler error messages
- Then propose targeted solutions
- Never guess at error causes

