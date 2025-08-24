' Editors/CustomDrawingEditor.vb - Refactored main editor implementation with manual scrolling
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Syntax
Imports SimpleIDE.Managers

Namespace Editors
    
    ' Custom text editor with syntax highlighting, undo/redo, and document structure support
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Core Components =====
        Private pMainGrid As Grid
        Private pDrawingArea As DrawingArea
        Private pVScrollbar As Scrollbar
        Private pHScrollbar As Scrollbar
        Private pCornerBox As DrawingArea  ' Bottom-right corner
        Private pSourceFileInfo as SourceFileInfo
        
        ' ===== Scrolling State =====
        Private pFirstVisibleLine As Integer = 0
        Private pFirstVisibleColumn As Integer = 0
        Private pTotalVisibleLines As Integer = 0
        Private pTotalVisibleColumns As Integer = 0
        Private pMaxLineWidth As Integer = 0
        
        
        ' ===== Document Data =====
        
        Private pLineCount As Integer = 1
        Private pLineMetadata() As LineMetadata
        Private pFilePath As String = ""
        Private pEncoding As Encoding = Encoding.UTF8
        Private pIsModified As Boolean = False
        Private pIsReadOnly As Boolean = False
        
        ' ===== Character-based rendering data =====
        Private pCharacterColors()() As CharacterColorInfo  ' Array of arrays for each Line's character colors
        
        ' ===== Cursor State =====
        Private pCursorLine As Integer = 0
        Private pCursorColumn As Integer = 0
        Private pDesiredColumn As Integer = 0
        Private pCursorVisible As Boolean = True
        Private pCursorBlink As Boolean = True
        Private pCursorBlinkTimer As UInteger = 0
        
        ' ===== Selection State =====
        Private pSelectionActive As Boolean = False
        Private pSelectionStartLine As Integer = 0
        Private pSelectionStartColumn As Integer = 0
        Private pSelectionEndLine As Integer = 0
        Private pSelectionEndColumn As Integer = 0
        
        ' ===== Syntax Highlighting =====
        Private pSyntaxHighlighter As VBSyntaxHighlighter
        Private pHighlightingEnabled As Boolean = True
        'Private pDocumentParser As VBCodeParser
        Private pParseTimer As UInteger = 0
        Private pNeedsParse As Boolean = False
        Private pSyntaxColorSet As SyntaxColorSet
        Private pSettingsManager As SettingsManager
        Private pBracketHighlightingEnabled As Boolean = False
        
        ' ===== Display Properties =====
        Private pFontDescription As Pango.FontDescription
        Private pTabWidth As Integer = 4
        Private pUseTabs As Boolean = False
        Private pShowLineNumbers As Boolean = True
        Private pWordWrap As Boolean = False
        Private pLineHeight As Integer = 20
        Private pCharWidth As Integer = 10
        Private pLineNumberWidth As Integer = 60
        Private pAutoIndent As Boolean = True
        
        ' ===== Layout =====
        Private pLeftPadding As Integer = 5
        Private pRightPadding As Integer = 5
        Private pTopPadding As Integer = 5
        Private pBottomPadding As Integer = 5
        Private pViewportWidth As Integer = 0
        Private pViewportHeight As Integer = 0
        
        ' ===== Editing Features =====
        Private pUndoRedoManager As UndoRedoManager
        Private pClipboard As Clipboard
        Private pLastAction As EditAction = EditAction.eNone
        Private pLastActionTime As DateTime = DateTime.Now
        Private pInsertMode As Boolean = True
        Private pIndentSize As Integer = 4
        Private pBracketHighlighting As Boolean = True
        Private pMatchingBracketLine As Integer = -1
        Private pMatchingBracketColumn As Integer = -1
        Private pPreservingDesiredColumn As Boolean = False
        
        ' ===== Search State =====
        Private pSearchPattern As String = ""
        Private pSearchCaseSensitive As Boolean = False
        Private pSearchWholeWord As Boolean = False
        Private pSearchRegex As Boolean = False
        Private pSearchMatches As New List(Of EditorPosition)
        Private pCurrentSearchIndex As Integer = -1
        
        ' ===== CodeSense =====
        Private pCodeSenseContext As CodeSenseContext
        Private pCodeSenseActive As Boolean = False
        
        ' ===== Node Graph =====
        Private pRootNode As SyntaxNode
        Private pDocumentNodes As Dictionary(Of String, DocumentNode)
        Private pRootNodes As List(Of DocumentNode)    ' for document Node representation
        
        ' ===== Font Metrics =====
        Private pFontMetrics As Utilities.FontMetrics
        
        ' ===== Update Control =====
        Private pUpdatesPaused As Boolean = False
        Private pBatchEditMode As Boolean = False
        
        ' ===== Track update state for BeginUpdate/EndUpdate =====
        Private pUpdateCount As Integer = 0
        Private pNeedRedrawAfterUpdate As Boolean = False

        ' ===== Has Selection Helper =====
        Private pHasSelection As Boolean = False

        ' Theme to use when in demo mode (for preview)
        Private pDemoTheme As EditorTheme  

        ' Line number widget (NEW - replaces pLineNumberArea)
        Private pLineNumberWidget As Widgets.LineNumberWidget
        Private pLineNumberArea As DrawingArea
        
        ' Line number dragging state (if not already present)
        Private pLineNumberDragging As Boolean = False
        Private pLineNumberDragAnchor As Integer = -1

        
        ' ===== Events =====
        Public Event Modified(vIsModified As Boolean) Implements IEditor.Modified
        Public Event CursorPositionChanged(vLine As Integer, vColumn As Integer) Implements IEditor.CursorPositionChanged
        Public Event SelectionChanged(vHasSelection As Boolean) Implements IEditor.SelectionChanged
        Public Event TextChanged(o As Object, e As EventArgs) Implements IEditor.TextChanged
        Public Event UndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean) Implements IEditor.UndoRedoStateChanged
        Public Event RequestSourceFiles(vSourceFileRequestor as SourceFileRequestor)
'        Public Event DocumentParsed(vRootNode As SyntaxNode) Implements IEditor.DocumentParsed

        Public Class SourceFileRequestor
            Public SourceFileInfo as SourceFileInfo
        End Class

        ' ===== Enums =====
        Private Enum EditAction
            eNone
            eTyping
            eDeleting
            ePasting
        End Enum
        
        ' ===== Constructor =====

        ''' <summary>
        ''' Primary constructor - requires SourceFileInfo
        ''' </summary>
        ''' <param name="vSourceFileInfo">The source file information containing the text to edit</param>
        Public Sub New(vSourceFileInfo As SourceFileInfo)
            MyBase.New(Orientation.Horizontal, 0)
            
            Try
                If vSourceFileInfo Is Nothing Then
                    Throw New ArgumentNullException(NameOf(vSourceFileInfo), "SourceFileInfo cannot be null")
                End If
                
                pSourceFileInfo = vSourceFileInfo
                pFilePath = pSourceFileInfo.FilePath
                
                ' Ensure SourceFileInfo has at least one line
                If pSourceFileInfo.TextLines.Count = 0 Then
                    pSourceFileInfo.TextLines.Add("")
                End If
                
                ' Initialize line count from SourceFileInfo
                pLineCount = pSourceFileInfo.TextLines.Count
                
                ' CRITICAL: Initialize metadata arrays BEFORE calling InitializeComponents/InitializeEditor
                ' This ensures they're properly sized for any operations that may reference them
                ReDim pLineMetadata(pLineCount - 1)
                ReDim pCharacterColors(pLineCount - 1)
                For i As Integer = 0 To pLineCount - 1
                    pLineMetadata(i) = New LineMetadata()
                    pCharacterColors(i) = New CharacterColorInfo() {}
                Next
                
                ' Now initialize components and editor
                InitializeComponents()
                InitializeEditor()
                
                ' REMOVED: Don't process formatting here - RefreshFromSourceFileInfo will do it
                ' This was causing duplicate processing and multiple draws
                ' For i As Integer = 0 To pLineCount - 1
                '     ProcessLineFormatting(i)
                ' Next
                
                ' FIXED: Check if SourceFileInfo already has a parsed SyntaxTree
                If vSourceFileInfo.SyntaxTree IsNot Nothing AndAlso vSourceFileInfo.IsParsed Then
                    ' Use the existing parsed structure
                    pRootNode = vSourceFileInfo.SyntaxTree
                    Console.WriteLine($"Using existing parse tree for {vSourceFileInfo.FileName}")
                    
                    ' REMOVED: Don't apply highlighting here - RefreshFromSourceFileInfo will do it
                    ' For i As Integer = 0 To pLineCount - 1
                    '     ApplySyntaxHighlightingToLine(i)
                    ' Next
                    
                    ' Raise document parsed event with existing structure
                    RaiseEvent DocumentParsed(pRootNode)
                Else
                    ' Only parse if we don't have existing parse data
                    Console.WriteLine($"Scheduling initial parse for {vSourceFileInfo.FileName}")
                    ScheduleParse()
                    ScheduleFullDocumentParse()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor.New error: {ex.Message}")
                Throw
            End Try
        End Sub

        ' Factory method for creating empty editor with new SourceFileInfo
        Public Shared Function CreateEmpty(vFilePath As String) As CustomDrawingEditor
            Try
                ' Create a new SourceFileInfo for the file
                Dim lProjectDir As String = If(String.IsNullOrEmpty(vFilePath), "", System.IO.Path.GetDirectoryName(vFilePath))
                Dim lSourceFileInfo As New SourceFileInfo(vFilePath, lProjectDir)
                
                ' Ensure it has at least one empty line
                If lSourceFileInfo.TextLines.Count = 0 Then
                    lSourceFileInfo.TextLines.Add("")
                End If
                
                Return New CustomDrawingEditor(lSourceFileInfo)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor.CreateEmpty error: {ex.Message}")
                Throw
            End Try
        End Function
        
        Private ReadOnly Property pTextLines As List(Of String)
           Get
               return pSourceFileInfo.TextLines
           End Get
        End Property
        
        ' ===== Initialization =====
        Private Sub InitializeComponents()
            Try
                ' Create main grid for layout
                pMainGrid = New Grid()
                pMainGrid.RowHomogeneous = False
                pMainGrid.ColumnHomogeneous = False
                
                ' Create line number widget (NEW: Using dedicated widget)
                pLineNumberWidget = New Widgets.LineNumberWidget(Me)
                
                ' Create main drawing area
                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                pDrawingArea.FocusOnClick = True
                pDrawingArea.AddEvents(CInt(EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or 
                                           EventMask.PointerMotionMask Or EventMask.KeyPressMask Or 
                                           EventMask.KeyReleaseMask Or EventMask.ScrollMask Or
                                           EventMask.FocusChangeMask))
                
                ' Create scrollbars
                pVScrollbar = New Scrollbar(Gtk.Orientation.Vertical, New Adjustment(0, 0, 100, 1, 10, 10))
                pHScrollbar = New Scrollbar(Gtk.Orientation.Horizontal, New Adjustment(0, 0, 100, 1, 10, 10))
                
                ' Create corner box
                pCornerBox = New DrawingArea()
                pCornerBox.WidthRequest = 15  ' Standard scrollbar width
                pCornerBox.HeightRequest = 15
                
                ' Layout in grid:
                ' [LineNumbers] [DrawingArea] [VScrollbar]
                ' [Empty]       [HScrollbar]  [Corner]
                pMainGrid.Attach(pLineNumberWidget, 0, 0, 1, 1)
                pMainGrid.Attach(pDrawingArea, 1, 0, 1, 1)
                pMainGrid.Attach(pVScrollbar, 2, 0, 1, 1)
                pMainGrid.Attach(pHScrollbar, 1, 1, 1, 1)
                pMainGrid.Attach(pCornerBox, 2, 1, 1, 1)
                
                ' Configure expanding
                pDrawingArea.Hexpand = True
                pDrawingArea.Vexpand = True
                
                ' Connect scrollbar handlers
                AddHandler pVScrollbar.ValueChanged, AddressOf OnVScrollbarValueChanged
                AddHandler pHScrollbar.ValueChanged, AddressOf OnHScrollbarValueChanged
                
                ' Connect size allocation handlers
                AddHandler pDrawingArea.SizeAllocated, AddressOf OnDrawingAreaSizeAllocated
                AddHandler pMainGrid.SizeAllocated, AddressOf OnMainGridSizeAllocated
                AddHandler pDrawingArea.Realized, AddressOf OnDrawingAreaRealized
                
                ' Pack grid into main container
                PackStart(pMainGrid, True, True, 0)
                
                ' Register event handlers
                RegisterEventHandlers()
                ShowAll()
        
                ' Enable line numbers by default
                pShowLineNumbers = True
                
                ' Update line number widget after font metrics are set
                UpdateLineNumberWidget()
                
                ' Make line number widget visible
                If pLineNumberWidget IsNot Nothing Then
                    pLineNumberWidget.Visible = True
                End If
        
                InitializeDragDrop()
                EnsureCursorsCreated()
                pKeyPressHandler = New KeyPressEventHandler(AddressOf OnKeyPress)
        
            Catch ex As Exception
                Console.WriteLine($"InitializeComponents error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Fixed InitializeEditor method to properly size metadata arrays based on actual line count
        ''' </summary>
        Private Sub InitializeEditor()
            Try
                ' Initialize document - ensure pTextLines has at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
                ' Set line count from actual text lines
                pLineCount = pTextLines.Count
                
                ' FIXED: Initialize metadata arrays to match actual line count, not hardcoded to 2
                ReDim pLineMetadata(pLineCount - 1)
                ReDim pCharacterColors(pLineCount - 1)
                
                ' Initialize each metadata element
                For i As Integer = 0 To pLineCount - 1
                    pLineMetadata(i) = New LineMetadata()
                    pCharacterColors(i) = New CharacterColorInfo() {}
                Next
                
                ' Initialize font
                pFontDescription = FontDescription.FromString("Monospace 11")
                UpdateFontMetrics()
        
                ' Initialize syntax highlighting
                pSyntaxColorSet = New SyntaxColorSet()
                pSyntaxHighlighter = New VBSyntaxHighlighter(pSyntaxColorSet)
                
                ' Initialize clipboard
                pClipboard = Clipboard.GetDefault(Display.Default)
                
                ' Start cursor blink timer
                pCursorBlinkTimer = GLib.Timeout.Add(500, AddressOf OnCursorBlink)
        
                ' Initialize context menus
                InitializeContextMenus()
        
                ' Update scrollbars
                UpdateScrollbars()
        
            Catch ex As Exception
                Console.WriteLine($"InitializeEditor error: {ex.Message}")
            End Try
        End Sub

        Public Sub SetThemeManager(vThemeManager As ThemeManager) Implements IEditor.SetThemeManager
            pThemeManager = vThemeManager
        End Sub
        
        ''' <summary>
        ''' Updates the line number widget with current editor state
        ''' </summary>
        Private Sub UpdateLineNumberWidget()
            Try
                If pLineNumberWidget Is Nothing Then Return
                
                ' Update font and metrics
                pLineNumberWidget.UpdateFont(pFontDescription, pLineHeight, pCharWidth)
                
                ' Update theme if available
                If pThemeManager IsNot Nothing Then
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    If lTheme IsNot Nothing Then
                        pLineNumberWidget.UpdateTheme(lTheme)
                    End If
                End If
                
                ' Update width based on line count
                pLineNumberWidget.UpdateWidth()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineNumberWidget error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateLineNumberWidth()
            Try
                If pLineNumberWidget IsNot Nothing Then
                    pLineNumberWidget.UpdateWidth()
                Else
                    ' Fallback for old code compatibility
                    Dim lMaxDigits As Integer = Math.Max(3, pLineCount.ToString().Length)
                    pLineNumberWidth = (lMaxDigits * pCharWidth) + 16
                    
                    If pLineNumberArea IsNot Nothing Then
                        pLineNumberArea.WidthRequest = pLineNumberWidth
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineNumberWidth error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the first visible line in the viewport
        ''' </summary>
        Public ReadOnly Property FirstVisibleLine As Integer
            Get
                Return pFirstVisibleLine
            End Get
        End Property
        
        ''' <summary>
        ''' Starts a line number drag selection
        ''' </summary>
        ''' <param name="vAnchorLine">The anchor line for drag selection</param>
        Public Sub StartLineNumberDrag(vAnchorLine As Integer)
            Try
                pLineNumberDragAnchor = vAnchorLine
                pLineNumberDragging = True
            Catch ex As Exception
                Console.WriteLine($"StartLineNumberDrag error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Ends a line number drag selection
        ''' </summary>
        Public Sub EndLineNumberDrag()
            Try
                pLineNumberDragging = False
            Catch ex As Exception
                Console.WriteLine($"EndLineNumberDrag error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets whether line number dragging is in progress
        ''' </summary>
        Public ReadOnly Property IsLineNumberDragging As Boolean
            Get
                Return pLineNumberDragging
            End Get
        End Property
        
        ''' <summary>
        ''' Updates selection during line number drag
        ''' </summary>
        ''' <param name="vCurrentLine">Current line under mouse</param>
        Public Sub UpdateLineNumberDrag(vCurrentLine As Integer)
            Try
                If Not pLineNumberDragging OrElse vCurrentLine < 0 OrElse vCurrentLine >= pLineCount Then Return
                
                Dim lStartLine As Integer
                Dim lEndLine As Integer
                
                If vCurrentLine < pLineNumberDragAnchor Then
                    ' Dragging upward
                    lStartLine = vCurrentLine
                    lEndLine = pLineNumberDragAnchor
                Else
                    ' Dragging downward or same line
                    lStartLine = pLineNumberDragAnchor
                    lEndLine = vCurrentLine
                End If
                
                ' Select from start of first line to end/start of next line
                If lEndLine < pLineCount - 1 Then
                    SetSelection(New EditorPosition(lStartLine, 0), New EditorPosition(lEndLine + 1, 0))
                Else
                    SetSelection(New EditorPosition(lStartLine, 0), New EditorPosition(lEndLine, pTextLines(lEndLine).Length))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineNumberDrag error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles scroll events from line number widget
        ''' </summary>
        ''' <param name="vArgs">Scroll event arguments</param>
        Public Sub HandleScroll(vArgs As ScrollEventArgs)
            Try
                ' Forward to existing scroll handler
                OnScrollEvent(Me, vArgs)
            Catch ex As Exception
                Console.WriteLine($"HandleScroll error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handler Registration =====
        
        ' ===== Size Allocation Handlers =====
        Private Sub OnDrawingAreaSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            Try
                ' Update viewport dimensions
                pViewportWidth = vArgs.Allocation.Width
                pViewportHeight = vArgs.Allocation.Height
                
                ' Recalculate visible lines and columns
                UpdateVisibleMetrics()
                
                ' Update scrollbars
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaSizeAllocated error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnMainGridSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            Try
                ' Force redraw when main container is resized
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnMainGridSizeAllocated error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDrawingAreaRealized(vSender As Object, vArgs As EventArgs)
            Try
                ' Grab focus when realized
                pDrawingArea.GrabFocus()
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaRealized error: {ex.Message}")
            End Try
        End Sub

        Public Shadows Sub GrabFocus() Implements IEditor.GrabFocus
             Try
                ' Grab focus when realized
                pDrawingArea.GrabFocus()
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaRealized error: {ex.Message}")
            End Try
        End Sub

        Private Function OnCursorBlink() As Boolean
            Try
                pCursorBlink = Not pCursorBlink
                InvalidateCursor()
                Return True ' Continue timer
                
            Catch ex As Exception
                Console.WriteLine($"OnCursorBlink error: {ex.Message}")
                Return True
            End Try
        End Function

        
        ' ===== Cursor Blink Timer =====
        Public Property AutoIndent As Boolean Implements IEditor.AutoIndent
            Get
                Return pAutoIndent
            End Get
            Set(Value As Boolean)
                pAutoIndent = Value
            End Set
        End Property

        ' Use Tabs instead of inserting Spaces
        Public Property UseTabs As Boolean Implements IEditor.UseTabs
            Get
                Return pUseTabs
            End Get
            Set(Value As Boolean)
                pUseTabs = Value
            End Set
        End Property
        
        Public Property TabWidth As Integer Implements IEditor.TabWidth
            Get
                Return pTabWidth
            End Get
            Set(Value As Integer)
                If Value < 1 OrElse Value > 10 Then Value = 4
                pTabWidth = Value
            End Set
        End Property
        
        ' ===== Properties =====
        Public Property FilePath As String Implements IEditor.FilePath
            Get
                Return pFilePath
            End Get
            Set(Value As String)
                pFilePath = Value
            End Set
        End Property
        
        Public Property Encoding As Encoding Implements IEditor.Encoding
            Get
                Return pEncoding
            End Get
            Set(Value As Encoding)
                pEncoding = Value
            End Set
        End Property
        
        Public Property IsModified As Boolean Implements IEditor.IsModified
            Get
                Return pIsModified
            End Get
            Set(Value As Boolean)
                If pIsModified <> Value Then
                    pIsModified = Value
                    RaiseEvent Modified(pIsModified)
                End If
            End Set
        End Property
        
        Public Property IsReadOnly As Boolean Implements IEditor.IsReadOnly
            Get
                Return pIsReadOnly
            End Get
            Set(Value As Boolean)
                pIsReadOnly = Value
            End Set
        End Property
        
        Public ReadOnly Property LineCount As Integer Implements IEditor.LineCount
            Get
                Return pLineCount
            End Get
        End Property
        
        Public ReadOnly Property CharCount As Integer Implements IEditor.CharCount
            Get
                Dim lCount As Integer = 0
                For i As Integer = 0 To pLineCount - 1
                    lCount += pTextLines(i).Length
                    If i < pLineCount - 1 Then
                        lCount += Environment.NewLine.Length
                    End If
                Next
                Return lCount
            End Get
        End Property
        
        Public ReadOnly Property CurrentLine As Integer Implements IEditor.CurrentLine
            Get
                Return pCursorLine
            End Get
        End Property
        
        Public ReadOnly Property CurrentColumn As Integer Implements IEditor.CurrentColumn
            Get
                Return pCursorColumn
            End Get
        End Property
        
        Public ReadOnly Property HasSelection As Boolean Implements IEditor.HasSelection
            Get
                Return pSelectionActive
            End Get
        End Property
        
        Public ReadOnly Property SelectionStart As EditorPosition Implements IEditor.SelectionStart
            Get
                If pSelectionActive Then
                    Dim lStartLine As Integer = pSelectionStartLine
                    Dim lStartColumn As Integer = pSelectionStartColumn
                    Dim lEndLine As Integer = pSelectionEndLine
                    Dim lEndColumn As Integer = pSelectionEndColumn
                    Dim lStartPos As New EditorPosition(lStartLine, lStartColumn)
                    Dim lEndPos As New EditorPosition(lEndLine, lEndColumn)
                    NormalizeSelection(lStartPos, lEndPos)
                    lStartLine = lStartPos.Line
                    lStartColumn = lStartPos.Column
                    lEndLine = lEndPos.Line
                    lEndColumn = lEndPos.Column
                    Return New EditorPosition(lStartLine, lStartColumn)
                Else
                    Return New EditorPosition(pCursorLine, pCursorColumn)
                End If
            End Get
        End Property
        
        Public ReadOnly Property SelectionEnd As EditorPosition Implements IEditor.SelectionEnd
            Get
                If pSelectionActive Then
                    Dim lStartLine As Integer = pSelectionStartLine
                    Dim lStartColumn As Integer = pSelectionStartColumn
                    Dim lEndLine As Integer = pSelectionEndLine
                    Dim lEndColumn As Integer = pSelectionEndColumn
                    Dim lStartPos As New EditorPosition(lStartLine, lStartColumn)
                    Dim lEndPos As New EditorPosition(lEndLine, lEndColumn)
                    NormalizeSelection(lStartPos, lEndPos)
                    lStartLine = lStartPos.Line
                    lStartColumn = lStartPos.Column
                    lEndLine = lEndPos.Line
                    lEndColumn = lEndPos.Column
                    Return New EditorPosition(lEndLine, lEndColumn)
                Else
                    Return New EditorPosition(pCursorLine, pCursorColumn)
                End If
            End Get
        End Property
        
        
        ''' <summary>
        ''' Gets whether undo is available (uses UndoRedoManager)
        ''' </summary>
        Public ReadOnly Property CanUndo As Boolean Implements IEditor.CanUndo
            Get
                If pUndoRedoManager IsNot Nothing Then
                    Return pUndoRedoManager.CanUndo
                Else
                    Return False
                End If
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether redo is available (uses UndoRedoManager)
        ''' </summary>
        Public ReadOnly Property CanRedo As Boolean Implements IEditor.CanRedo
            Get
                If pUndoRedoManager IsNot Nothing Then
                    Return pUndoRedoManager.CanRedo
                Else
                    Return False
                End If
            End Get
        End Property
        
        Public ReadOnly Property SupportsCodesense As Boolean Implements IEditor.SupportsCodeSense
            Get
                Return True
            End Get
        End Property
        
        Public Property WordWrap As Boolean Implements IEditor.WordWrap
            Get
                Return pWordWrap
            End Get
            Set(Value As Boolean)
                If pWordWrap <> Value Then
                    pWordWrap = Value
                    ' TODO: Implement word wrap
                    pDrawingArea.QueueDraw()
                End If
            End Set
        End Property
        
        Public ReadOnly Property Widget As Widget Implements IEditor.Widget
            Get
                Return Me
            End Get
        End Property

        Public ReadOnly Property CanCut As Boolean Implements IEditor.CanCut
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property CanPaste As Boolean Implements IEditor.CanPaste
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property CanCopy As Boolean Implements IEditor.CanCopy
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property SupportsLineNumbers As Boolean Implements IEditor.SupportsLineNumbers
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Refresh editor content from SourceFileInfo
        ''' </summary>
        Public Sub RefreshFromSourceFileInfo()
            Try
                If pSourceFileInfo Is Nothing Then Return
                
                ' Update line count directly from SourceFileInfo
                ' (pTextLines is a ReadOnly property that returns pSourceFileInfo.TextLines)
                pLineCount = pSourceFileInfo.TextLines.Count
                
                ' Ensure at least one line
                If pLineCount = 0 Then
                    pSourceFileInfo.TextLines.Add("")
                    pLineCount = 1
                End If
                
                ' Resize metadata arrays
                ReDim pLineMetadata(pLineCount - 1)
                ReDim pCharacterColors(pLineCount - 1)
                For i As Integer = 0 To pLineCount - 1
                    pLineMetadata(i) = New LineMetadata()
                    pCharacterColors(i) = New CharacterColorInfo() {}
                    ProcessLineFormatting(i)
                Next
                
                ' Reset cursor
                SetCursorPosition(0, 0)
                
                ' Update display
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
                ' Schedule parsing
                ScheduleParsing()
                
            Catch ex As Exception
                Console.WriteLine($"RefreshFromSourceFileInfo error: {ex.Message}")
            End Try
        End Sub

        Public ReadOnly Property ThemeManager As ThemeManager
            Get
                Return pThemeManager
            End Get
        End Property

        Public ReadOnly Property DisplayName() as String Implements IEditor.DisplayName
           Get
                Return pSourceFileInfo.FileName
           End Get
        End Property

        ''' <summary>
        ''' Sets the file path for this editor (used by EditorFactory)
        ''' </summary>
        ''' <param name="vFilePath">The full path to the file this editor represents</param>
        Public Sub SetFilePath(vFilePath As String) Implements IEditor.SetFilePath
            Try
                ' Update the editor's file path
                pFilePath = vFilePath
                
                ' Update the SourceFileInfo if it exists
                If pSourceFileInfo IsNot Nothing Then
                    pSourceFileInfo.FilePath = vFilePath
                    pSourceFileInfo.FileName = System.IO.Path.GetFileName(vFilePath)
                    pSourceFileInfo.ProjectDirectory = System.IO.Path.GetDirectoryName(vFilePath)
                    
                    ' Calculate relative path if we have a project directory
                    If Not String.IsNullOrEmpty(pSourceFileInfo.ProjectDirectory) Then
                        Try
                            pSourceFileInfo.RelativePath = System.IO.Path.GetRelativePath(pSourceFileInfo.ProjectDirectory, vFilePath)
                        Catch
                            ' Fallback to filename only if relative path calculation fails
                            pSourceFileInfo.RelativePath = pSourceFileInfo.FileName
                        End Try
                    End If
                End If
                
                Console.WriteLine($"SetFilePath: Updated file path to {vFilePath}")
                
            Catch ex As Exception
                Console.WriteLine($"SetFilePath error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets whether this editor supports IntelliSense/code completion features
        ''' </summary>
        ''' <value>True if IntelliSense is supported, False otherwise</value>
        ''' <returns>Always returns True for CustomDrawingEditor as it supports VB.NET IntelliSense</returns>
        Public ReadOnly Property SupportsIntellisense As Boolean Implements IEditor.SupportsIntellisense
            Get
                ' CustomDrawingEditor supports IntelliSense for VB.NET files
                Return True
            End Get
        End Property
        
    End Class
    
End Namespace
