' Interfaces/IEditor.vb - Fixed interface for editor implementations with stream-based I/O
Imports Gtk
Imports System
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax
Imports SimpleIDE.Editors

Namespace Interfaces
    
    ' Main editor interface that all editors must implement
    Public Interface IEditor
        Inherits IDisposable
        
        ' Core Properties
        ReadOnly Property FilePath As String  ' for display purposes only, not used for i/O
        Property IsModified As Boolean
        ReadOnly Property IsReadOnly As Boolean
        ReadOnly Property CanUndo As Boolean
        ReadOnly Property CanRedo As Boolean
        ReadOnly Property CanCut As Boolean
        ReadOnly Property CanCopy As Boolean
        ReadOnly Property CanPaste As Boolean
        
        ' Content Properties
        Property Text As String
        ReadOnly Property LineCount As Integer
        ReadOnly Property CharCount As Integer
        ReadOnly Property CurrentLine As Integer
        ReadOnly Property CurrentColumn As Integer
        ReadOnly Property HasSelection As Boolean
        ReadOnly Property SelectionStart As EditorPosition
        ReadOnly Property SelectionEnd As EditorPosition
        ReadOnly Property SelectedText As String
        Property TabWidth As Integer
        Property UseTabs As Boolean
        Property AutoIndent As Boolean
        
        ' UI Components (for GTK integration)
        ReadOnly Property Widget As Widget  ' the Main Widget to add to container
        ReadOnly Property SupportsLineNumbers As Boolean
        ReadOnly Property SupportsSyntaxHighlighting As Boolean
        ReadOnly Property SupportsNavigation As Boolean
        
        ' Events
        Event Modified(vIsModified As Boolean)
        Event CursorPositionChanged(vLine As Integer, vColumn As Integer)
        Event SelectionChanged(vHasSelection As Boolean)
        Event TextChanged(o As Object, e As EventArgs)
        Event UndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean)
        Event DocumentParsed(vRootNode As SyntaxNode)  
        Event LineExited As EventHandler(Of CustomDrawingEditor.LineExitedEventArgs) 

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
        
        ' Navigation
        Sub GoToLine(vLine As Integer)
        Sub GoToPosition(vLine As Integer, vColumn As Integer)
        Sub MoveToDocumentStart()
        Sub MoveToDocumentEnd()
        Sub MoveToLineStart()
        Sub MoveToLineEnd()
        Sub PageUp()
        Sub PageDown()
        
        ' Selection
        Sub SetSelection(vStartLine As Integer, vStartColumn As Integer, vEndLine As Integer, vEndColumn As Integer)
        Sub ClearSelection()
        Sub SelectLine(vLine As Integer)
        Sub SelectLines(vStartLine As Integer, vEndLine As Integer)
        Sub SelectWord()
        Function GetSelectedText() As String
        Sub SquareSelection()
        Sub EnsureCursorVisible()
        
        ' Text Manipulation
        Sub InsertText(vText As String)
        Sub InsertTextAtPosition(vLine As Integer, vColumn As Integer, vText As String)
        Sub DeleteRange(vStartLine As Integer, vStartColumn As Integer, vEndLine As Integer, vEndColumn As Integer)
        Sub ReplaceText(vStartLine As Integer, vStartColumn As Integer, vEndLine As Integer, vEndColumn As Integer, vNewText As String)
        Sub ReplaceText(vText As String)
        
        ' Indentation
        Sub IndentSelection()
        Sub OutdentSelection()
        Sub IndentLine(vLine As Integer)
        Sub OutdentLine(vLine As Integer)
        Function GetLineIndentation(vLine As Integer) As String
        
        ' Search
        Function Find(vSearchText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean) As IEnumerable(Of EditorPosition)
        Sub FindNext()
        Sub FindPrevious()
        Sub Replace(vSearchText As String, vReplaceText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean)
        Sub ReplaceAll(vSearchText As String, vReplaceText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean)
        
        ' Formatting
        Sub ApplyFont(vFontDescription As String)
        Sub ApplyTheme()
        Sub SetTabWidth(vSpaces As Integer)
        Sub SetUseTabs(vUseTabs As Boolean)
        Sub SetShowLineNumbers(vShow As Boolean)
        Sub SetWordWrap(vEnable As Boolean)
        
        ' Advanced Features
        Sub RefreshSyntaxHighlighting()
        Sub StartCodeSense(vContext As Models.CodeSenseContext)
        Sub CancelCodeSense()
        Function GetWordAtCursor() As String
        Function GetLineText(vLine As Integer) As String
        Function GetCursorPosition() As EditorPosition
        Function GetDocumentStructure() As SyntaxNode
        Sub RequestParse()
        Sub ZoomIn()
        Sub ZoomOut()
        Sub ZoomReset()
        
        ' Performance
        Sub BeginUpdate()  ' Suspend updates for bulk operations
        Sub EndUpdate()    ' Resume updates and Refresh

'        ' Get the current selection as offsets
'        Function GetSelection(ByRef vStartOffset As Integer, ByRef vEndOffset As Integer) As Boolean
        
'        ' Select text between character offsets
'        Sub SelectText(vStartOffset As Integer, vEndOffset As Integer)
        
        ' Replace the current selection with new text
        Sub ReplaceSelection(vText As String)
        
'        ' Get cursor position as character offset
'        Function GetCursorOffset() As Integer

        ' Commenting for single line or selection
        Sub ToggleCommentBlock()

        Sub GrabFocus()

        ReadOnly Property SupportsCodeSense As Boolean
        Property Encoding As Encoding
        Property ShowLineNumbers As Boolean
        Property WordWrap As Boolean

        ''' <summary>
        ''' Set cursor position to specific line and column
        ''' </summary>
        Sub SetCursorPosition(vLine As Integer, vColumn As Integer)

        ''' <summary>
        ''' Navigates to a line number and positions it as the second visible line from top when possible
        ''' </summary>
        ''' <param name="vLineNumber">Target line number (1-based)</param>
        ''' <remarks>
        ''' This method scrolls the editor so the target line appears as the second visible line
        ''' from the top of the viewport. If there aren't enough lines after the target to fill
        ''' the viewport, it scrolls so the end of the file is at the bottom of the editor.
        ''' </remarks>
        Sub NavigateToLineNumberForPresentment(vLineNumber As Integer)


    End Interface
    
    ' Position in the editor
    Public Structure EditorPosition
        Public Line As Integer       ' 0-based
        Public Column As Integer     ' 0-based
        
        Public Sub New(vLine As Integer, vColumn As Integer)
            Line = vLine
            Column = vColumn
        End Sub
    End Structure
    
End Namespace
