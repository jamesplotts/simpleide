' Interfaces/IEditor.vb - Updated interface using EditorPosition for location parameters
Imports Gtk
Imports System
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax
Imports SimpleIDE.Editors

Namespace Interfaces
    
    ''' <summary>
    ''' Main editor interface that all editors must implement
    ''' </summary>
    Public Interface IEditor
        Inherits IDisposable
        
        ' Core Properties
        ReadOnly Property FilePath As String  ' for display purposes only, not used for I/O
        Property IsModified As Boolean
        ReadOnly Property IsReadOnly As Boolean
        ReadOnly Property CanUndo As Boolean
        ReadOnly Property CanRedo As Boolean
        ReadOnly Property CanCut As Boolean
        ReadOnly Property CanCopy As Boolean
        ReadOnly Property CanPaste As Boolean
        
        ' Content Properties
        Property Text As String
        ReadOnly Property TextLines As List(Of String)
        ReadOnly Property LineCount As Integer
        ReadOnly Property CharCount As Integer
        ReadOnly Property CurrentLine As Integer
        ReadOnly Property CurrentColumn As Integer
        ReadOnly Property HasSelection As Boolean
        ReadOnly Property SelectionStart As EditorPosition
        ReadOnly Property SelectionEnd As EditorPosition
        ReadOnly Property SelectedText As String
        
        ' UI Components (for GTK integration)
        ReadOnly Property Widget As Widget  ' the Main Widget to add to container
        ReadOnly Property SupportsLineNumbers As Boolean
        ReadOnly Property SupportsSyntaxHighlighting As Boolean
        ReadOnly Property SupportsNavigation As Boolean
        ReadOnly Property RootNode As SyntaxNode
        ReadOnly Property SourceFileInfo As SourceFileInfo
        
        ' Events
        Event Modified(vSender As Object, vIsModified As Boolean)
        Event CursorPositionChanged(vLine As Integer, vColumn As Integer)
        Event SelectionChanged(vHasSelection As Boolean)
        Event TextChanged(o As Object, e As EventArgs)
        Event UndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean)
        Event DocumentParsed(vRootNode As SyntaxNode)
        Event LineExited As EventHandler(Of CustomDrawingEditor.LineExitedEventArgs)
        Event ProjectManagerRequested(o As Object, e As ProjectManagerRequestEventArgs)
        Event RequestGotoDefinition(o as Object, e as GoToDefinitionEventArgs)  
        
        ' Helper method for setting file path (used by EditorFactory)
        Sub SetThemeManager(vThemeManager As ThemeManager)
        
        ' Core Operations
        Sub Undo()
        Sub Redo()
        Sub Cut()
        Sub Copy()
        Sub Paste()
        Sub SelectAll()
        Sub Delete()
        Sub ReplaceAllText(vText As String)
        Sub DeleteTextDirect(vStartLine As Integer, vStartColumn As Integer,
                                   vEndLine As Integer, vEndColumn As Integer)
        Sub DeleteTextDirect(vStartPosition As EditorPosition, vEndPosition As EditorPosition)
        
        ' Navigation - Updated to use EditorPosition
        Sub GoToLine(vLine As Integer)
        Sub GoToPosition(vPosition As EditorPosition)
        Sub MoveToDocumentStart()
        Sub MoveToDocumentEnd()
        Sub MoveToLineStart()
        Sub MoveToLineEnd()
        Sub PageUp()
        Sub PageDown()
        
        ' Selection - Updated to use EditorPosition
        Sub SetSelection(vStartPosition As EditorPosition, vEndPosition As EditorPosition)
        Sub DeleteSelection()
        Sub ReplaceSelection(vText As String)
        Sub ClearSelection()
        Sub SelectLine(vLine As Integer)
        Sub SelectLines(vStartLine As Integer, vEndLine As Integer)
        Sub SelectWord()
        Function GetSelectedText() As String
        Sub SquareSelection()
        Sub EnsureCursorVisible()
        Function GetCursorPosition() As EditorPosition
        Sub OnShown()
        
        ' Text Manipulation - Updated to use EditorPosition
        Sub InsertText(vText As String)
        Sub InsertTextAtPosition(vPosition As EditorPosition, vText As String)
        Sub DeleteRange(vStartPosition As EditorPosition, vEndPosition As EditorPosition)
        Sub ReplaceText(vStartPosition As EditorPosition, vEndPosition As EditorPosition, vNewText As String)
        
        ' Cursor positioning - Updated to use EditorPosition
        Sub SetCursorPosition(vPosition As EditorPosition)
        
        ' Indentation
        Sub IndentSelection()
        Sub OutdentSelection()
        Sub IndentLine(vLine As Integer)
        Sub OutdentLine(vLine As Integer)
        Function GetLineIndentation(vLine As Integer) As String
        
        ' Search - Updated to use EditorPosition
        Function Find(vSearchText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean) As IEnumerable(Of EditorPosition)
        Sub FindNext()
        Sub FindPrevious()
        Sub Replace(vSearchText As String, vReplaceText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean)
        Sub ReplaceAll(vSearchText As String, vReplaceText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean)
        
        ' Formatting
        Sub ApplyTheme()
        
        ' Advanced Features
        Function GetWordAtCursor() As String
        Function GetLineText(vLine As Integer) As String
        Function GetDocumentStructure() As SyntaxNode
        Sub ZoomIn()
        Sub ZoomOut()
        Sub ZoomReset()
        
        ' Performance
        Sub BeginUpdate()  ' Suspend updates for bulk operations
        Sub EndUpdate()    ' Resume updates and Refresh
        
        
        ' Commenting for single line or selection
        Sub ToggleCommentBlock()
        
        Sub GrabFocus()
        
        ReadOnly Property SupportsIntellisense As Boolean
        Property Encoding As Encoding
        ReadOnly Property DisplayName() As String
        ReadOnly Property SupportsCodesense() As Boolean
        Sub StartCodeSense(vContext As CodeSenseContext)
        Sub CancelCodeSense()
        Sub NavigateToLineNumberForPresentment(vLineNumber As Integer)
        Function FindAll(vFindText As String) As List(Of EditorPosition)
        Function GetDocumentNodes() As Dictionary(Of String, DocumentNode)

        ''' <summary>
        ''' Performs a smart paste operation with comment stripping and auto-indentation
        ''' </summary>
        ''' <remarks>
        ''' This method:
        ''' 1. Strips leading artifact comment lines (lines starting with ')
        ''' 2. Strips leading empty lines
        ''' 3. Adjusts indentation to match the current context
        ''' Used with Ctrl+Shift+V shortcut
        ''' </remarks>
        Sub SmartPaste()
        Function SaveContent() As Boolean
        Function LoadContent() As Boolean
        Property SettingsManager() As SettingsManager

    End Interface
    
End Namespace