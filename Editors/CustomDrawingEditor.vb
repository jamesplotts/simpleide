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
        Private pSourceFileInfo As SourceFileInfo
        
        ' ===== Scrolling State =====
        Private pFirstVisibleLine As Integer = 0
        Private pFirstVisibleColumn As Integer = 0
        Private pTotalVisibleLines As Integer = 0
        Private pTotalVisibleColumns As Integer = 0
        Private pMaxLineWidth As Integer = 0
        
        ' ===== Document Data =====
        Private pFilePath As String = ""
        Private pEncoding As Encoding = Encoding.UTF8
        Private pIsModified As Boolean = False
        Private pIsReadOnly As Boolean = False
        
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
        Private pHighlightingEnabled As Boolean = True
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
        Private pInitialFormattingPending As Boolean = False
        
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
        Private pRootNodes As List(Of DocumentNode)    ' for document Node representation
        Private pDocumentNodes As Dictionary(Of String, DocumentNode)
        
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

        ' ===== Theme Related =====
        Private pDemoTheme As EditorTheme  
        Private pThemeApplied As Boolean = False

        ' ===== Line number widget =====
        Private pLineNumberWidget As Widgets.LineNumberWidget
        
        ' ===== Line number dragging state =====
        Private pLineNumberDragging As Boolean = False
        Private pLineNumberDragAnchor As Integer = -1

        ' ===== Properties =====

        ''' <summary>
        ''' Reference to the centralized ProjectManager for parsing operations
        ''' </summary>
        Private pProjectManager As ProjectManager

        Private ReadOnly Property pLineCount() As Integer
            Get
                Return pSourceFileInfo.TextLines.Count
            End Get
        End Property

        Public ReadOnly Property SourceFileInfo As SourceFileInfo Implements IEditor.SourceFileInfo
            Get
                Return pSourceFileInfo
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets the ProjectManager for parse notifications
        ''' </summary>
        ''' <value>The ProjectManager instance</value>
        ''' <remarks>
        ''' The editor only subscribes to parse notifications.
        ''' It doesn't trigger parsing - that's handled by SourceFileInfo.
        ''' </remarks>
        Public Property ProjectManager As ProjectManager
            Get
                Return pProjectManager
            End Get
            Set(value As ProjectManager)
                Try
                    ' Unsubscribe from old manager if exists
                    If pProjectManager IsNot Nothing Then
                        RemoveHandler pProjectManager.ParseCompleted, AddressOf OnProjectManagerParseCompleted
                        RemoveHandler pProjectManager.IdentifierMapUpdated, AddressOf OnProjectManagerIdentifierMapUpdated
                        RemoveHandler pProjectManager.ProjectClosed, AddressOf OnProjectManagerProjectClosed
                        Console.WriteLine($"CustomDrawingEditor: Unsubscribed from old ProjectManager")
                    End If
                    
                    ' Set new manager
                    pProjectManager = value
                    
                    ' Subscribe to new manager if not null
                    If pProjectManager IsNot Nothing Then
                        ' Only subscribe to parse notifications
                        ' We don't trigger parsing - SourceFileInfo does that
                        AddHandler pProjectManager.ParseCompleted, AddressOf OnProjectManagerParseCompleted
                        AddHandler pProjectManager.IdentifierMapUpdated, AddressOf OnProjectManagerIdentifierMapUpdated
                        AddHandler pProjectManager.ProjectClosed, AddressOf OnProjectManagerProjectClosed
                        Console.WriteLine($"CustomDrawingEditor: Subscribed to ProjectManager for notifications")
                        
                        ' If we have a SourceFileInfo that needs colors, it will request parsing itself
                        ' We just wait for the notification when parsing completes
                    End If
                    
                Catch ex As Exception
                    Console.WriteLine($"CustomDrawingEditor.ProjectManager setter error: {ex.Message}")
                End Try
            End Set
        End Property

        Public ReadOnly Property RootNode() As SyntaxNode Implements IEditor.RootNode
            Get
                Return pRootNode
            End Get
        End Property
        
        Public ReadOnly Property TextLines As List(Of String) Implements IEditor.TextLines
           Get
               Return pSourceFileInfo.TextLines
           End Get
        End Property

        
        ''' <summary>
        ''' Gets the line metadata array from SourceFileInfo
        ''' </summary>
        ''' <value>Reference to the LineMetadata array in SourceFileInfo</value>
        Private ReadOnly Property pLineMetadata As LineMetadata()
            Get
                If pSourceFileInfo IsNot Nothing Then
                    Return pSourceFileInfo.LineMetadata
                End If
                Return Nothing
            End Get
        End Property

        ''' <summary>
        ''' Flag indicating if a parse has been requested
        ''' </summary>
        Private pParseRequested As Boolean = False
        
        ''' <summary>
        ''' Timer ID for debounced parse scheduling
        ''' </summary>
        Private pParseTimerId As UInteger = 0
        
        ' Event handler fields for proper disposal
        Private pFocusInHandler As FocusInEventHandler
        Private pFocusOutHandler As FocusOutEventHandler
        Private pVScrollValueChangedHandler As EventHandler
        Private pHScrollValueChangedHandler As EventHandler
        
        ' ===== Events =====

        Public Event Modified(vIsModified As Boolean) Implements IEditor.Modified
        Public Event CursorPositionChanged(vLine As Integer, vColumn As Integer) Implements IEditor.CursorPositionChanged
        Public Event SelectionChanged(vHasSelection As Boolean) Implements IEditor.SelectionChanged
        Public Event TextChanged(o As Object, e As EventArgs) Implements IEditor.TextChanged
        Public Event UndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean) Implements IEditor.UndoRedoStateChanged
        Public Event RequestSourceFiles(vSourceFileRequestor As SourceFileRequestor)
        Public Event ProjectManagerRequested(o As Object, e As ProjectManagerRequestEventArgs) Implements IEditor.ProjectManagerRequested

        ''' <summary>
        ''' Raised when the navigation dropdowns need to be updated due to cursor context change
        ''' </summary>
        ''' <remarks>
        ''' This event is fired when the cursor moves to a different context (class/method)
        ''' to trigger navigation dropdown updates in MainWindow
        ''' </remarks>
        Public Event NavigationUpdateRequested As EventHandler

        ' ===== Event Args =====

        Public Class SourceFileRequestor
            Public SourceFileInfo As SourceFileInfo
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
        ''' Create a new editor instance for a SourceFileInfo
        ''' </summary>
        ''' <param name="vSourceFileInfo">The source file to edit</param>
        ''' <remarks>
        ''' Updated to work with centralized ProjectParser instead of local VBParser
        ''' </remarks>
        Public Sub New(vSourceFileInfo As SourceFileInfo, vThemeManager As ThemeManager)
            MyBase.New(Orientation.Vertical, 0)
            If vThemeManager Is Nothing Then Throw New Exception("CustomDrawingEditor.New: Null Object Reference - Parameter vThemeManager Is Nothing")
            Try
                ' Store the source file info
                pSourceFileInfo = vSourceFileInfo
                pFilePath = If(vSourceFileInfo?.FilePath, "")
                
                ' Store the theme manager
                pThemeManager = vThemeManager    

                ' Initialize theme color cache for rendering
                InitializeThemeColorCache()
                
                ' Subscribe to source file events before initialization
                If pSourceFileInfo IsNot Nothing Then
                    AddHandler pSourceFileInfo.ContentChanged, AddressOf OnSourceFileContentChanged
                    AddHandler pSourceFileInfo.TextLinesChanged, AddressOf OnTextLinesChanged
                    AddHandler pSourceFileInfo.RenderingChanged, AddressOf OnRenderingChanged
                End If            

                ' Now initialize components and editor with error handling
                Try
                    InitializeComponents()
                Catch ex As Exception
                    Console.WriteLine($"Error in InitializeComponents: {ex.Message}")
                    ' Continue with minimal setup
                End Try
                
                Try
                    InitializeEditor()
                Catch ex As Exception
                    Console.WriteLine($"Error in InitializeEditor: {ex.Message}")
                    ' Continue with what we have
                End Try
                
                ' REMOVED: Don't try to get ProjectManager here - it will be set by CreateNewTab
                Console.WriteLine($"CustomDrawingEditor created for {vSourceFileInfo.FileName}, awaiting ProjectManager")
               
                ' FIXED: Check if SourceFileInfo already has a parsed SyntaxTree
                If vSourceFileInfo.SyntaxTree IsNot Nothing AndAlso vSourceFileInfo.IsParsed Then
                    ' Use the existing parsed structure
                    pRootNode = vSourceFileInfo.SyntaxTree
                    Console.WriteLine($"Using existing parse tree for {vSourceFileInfo.FileName}")
                Else
                    ' REMOVED: Don't request parse here - wait for ProjectManager to be set
                    Console.WriteLine($"No existing parse tree for {vSourceFileInfo.FileName}, will parse when ProjectManager is available")
                End If
                
                ' Mark as not modified for existing files
                If vSourceFileInfo.IsLoaded Then
                    pIsModified = False
                End If
                
                Console.WriteLine($"Editor initialized for: {pFilePath}")
                Console.WriteLine($"  Lines: {pLineCount}, Loaded: {vSourceFileInfo.IsLoaded}")
                Console.WriteLine($"  Using tokens: {If(vSourceFileInfo.CharacterTokens IsNot Nothing, "Yes", "No")}")
        
                SetCursorPosition(0,0)
                EnsureCursorVisible
                GrabFocus

            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor constructor error: {ex.Message}")
                Console.WriteLine($"Stack: {ex.StackTrace}")
            End Try
        End Sub
        
        
        ' ===== Initialization =====

        Private Sub InitializeComponents()
            Try
                ' Initialize critical objects first to prevent null references
                If pSourceFileInfo Is Nothing Then
                    Console.WriteLine("InitializeComponents: WARNING - pSourceFileInfo is Nothing, creating minimal instance")
                    pSourceFileInfo = New SourceFileInfo("", "", "")
                    pSourceFileInfo.TextLines.Add("")
                End If
                
                ' Create main grid for layout
                pMainGrid = New Grid()
                pMainGrid.RowHomogeneous = False
                pMainGrid.ColumnHomogeneous = False
                
                ' Create line number widget with error handling
                Try
                    pLineNumberWidget = New Widgets.LineNumberWidget(Me)
                Catch ex As Exception
                    Console.WriteLine($"InitializeComponents: Failed to create LineNumberWidget: {ex.Message}")
                    pLineNumberWidget = Nothing
                End Try
                
                ' Create main drawing area
                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                pDrawingArea.FocusOnClick = True
                pDrawingArea.AddEvents(CInt(EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or 
                                           EventMask.PointerMotionMask Or EventMask.KeyPressMask Or 
                                           EventMask.KeyReleaseMask Or EventMask.ScrollMask Or
                                           EventMask.FocusChangeMask))
                
                pVScrollbar = New Scrollbar(Gtk.Orientation.Vertical, New Adjustment(0, 0, 1, 1, 1, 1))
                pHScrollbar = New Scrollbar(Gtk.Orientation.Horizontal, New Adjustment(0, 0, 1, 1, 1, 1))
                
                ' Create corner box
                pCornerBox = New DrawingArea()
                pCornerBox.WidthRequest = 15  ' Standard scrollbar width
                pCornerBox.HeightRequest = 15
                
                ' Layout in grid:
                ' [LineNumbers] [DrawingArea] [VScrollbar]
                ' [Empty]       [HScrollbar]  [Corner]
                If pLineNumberWidget IsNot Nothing Then
                    pMainGrid.Attach(pLineNumberWidget, 0, 0, 1, 1)
                End If
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
        
                ' Initialize drag and drop
                Try
                    InitializeDragDrop()
                Catch ex As Exception
                    Console.WriteLine($"InitializeComponents: Failed to initialize drag/drop: {ex.Message}")
                End Try
                
                ' Create cursors
                Try
                    EnsureCursorsCreated()
                Catch ex As Exception
                    Console.WriteLine($"InitializeComponents: Failed to create cursors: {ex.Message}")
                End Try
                pDrawingArea.GrabFocus()
                OnVScrollbarValueChanged(Nothing, Nothing) 
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeComponents error: {ex.Message}")
                ' Don't re-throw - let the editor continue with minimal UI
            End Try
        End Sub
        
        ''' <summary>
        ''' Fixed InitializeEditor method to properly size metadata arrays based on actual line count
        ''' </summary>
        Private Sub InitializeEditor()
            Try
                ' Initialize font
                pFontDescription = FontDescription.FromString("Monospace 11")
                UpdateFontMetrics()

                ' Initialize syntax highlighting with theme colors if available        
                If pThemeManager IsNot Nothing Then
                    ' Get the current theme
                    Dim lCurrentTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                    
                    If lCurrentTheme IsNot Nothing AndAlso lCurrentTheme.SyntaxColors IsNot Nothing Then
                        ' Create SyntaxColorSet from theme
                        pSyntaxColorSet = New SyntaxColorSet()
                        pSyntaxColorSet.UpdateFromTheme(lCurrentTheme)
                        Console.WriteLine("InitializeEditor: SyntaxColorSet initialized from current theme")
                    Else
                        ' Fall back to creating default SyntaxColorSet
                        pSyntaxColorSet = New SyntaxColorSet()
                    End If
                Else
                    ' No theme manager available, use defaults
                    pSyntaxColorSet = New SyntaxColorSet()
                End If

                
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
        
        ''' <summary>
        ''' Updates the line number widget width based on the number of lines
        ''' </summary>
        ''' <remarks>
        ''' Calculates the required width to display the maximum line number
        ''' </remarks>
        Private Sub UpdateLineNumberWidth()
            Try
                If pLineNumberWidget Is Nothing Then Return
                
                ' Calculate number of digits needed for line count
                Dim lDigits As Integer = Math.Max(3, pLineCount.ToString().Length)
                
                ' Calculate width needed (digits * char width + padding)
                Dim lNewWidth As Integer = (lDigits * pCharWidth) + 16  ' 16 pixels padding
                
                ' Only update if width changed to avoid unnecessary redraws
                If pLineNumberWidth <> lNewWidth Then
                    pLineNumberWidth = lNewWidth
                    
                    ' Request size for line number widget
                    pLineNumberWidget.SetSizeRequest(pLineNumberWidth, -1)
                    
                    ' Queue redraw of line numbers
                    pLineNumberWidget?.QueueDraw()
                    
                    Console.WriteLine($"UpdateLineNumberWidth: Set to {pLineNumberWidth}px for {pLineCount} lines")
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
                    SetSelection(New EditorPosition(lStartLine, 0), New EditorPosition(lEndLine, TextLines(lEndLine).Length))
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
                for i As Integer = 0 To pLineCount - 1
                    lCount += TextLines(i).Length
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

        Public ReadOnly Property ThemeManager As ThemeManager
            Get
                Return pThemeManager
            End Get
        End Property

        Public ReadOnly Property DisplayName() As String Implements IEditor.DisplayName
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
                
                ' CRITICAL: Update scrollbars after any file path change
                ' This ensures proper scrollbar initialization even if content hasn't loaded yet
                UpdateScrollbars()
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

        ''' <summary>
        ''' Handles parse completion notification from ProjectManager
        ''' </summary>
        ''' <param name="vFile">The source file that was parsed</param>
        ''' <param name="vResult">The parse result (SyntaxNode)</param>
        ''' <remarks>
        ''' This handler updates the local pRootNode, notifies parsing completion,
        ''' and triggers a redraw to show updated syntax colors.
        ''' </remarks>
        Private Sub OnProjectManagerParseCompleted(vFile As SourceFileInfo, vResult As Object)
            Try
                ' Verify this is for our file
                If vFile Is Nothing OrElse vFile IsNot pSourceFileInfo Then
                    Return
                End If
                
                Console.WriteLine($"CustomDrawingEditor: ParseCompleted received for {pFilePath}")
                
                ' Update the root node from the parse result
                If TypeOf vResult Is SyntaxNode Then
                    pRootNode = DirectCast(vResult, SyntaxNode)
                    Console.WriteLine($"CustomDrawingEditor: Updated pRootNode from parse result")
                ElseIf vResult IsNot Nothing Then
                    ' Try to extract SyntaxNode from other result types
                    Dim lResultType = vResult.GetType()
                    Dim lRootNodeProperty = lResultType.GetProperty("RootNode")
                    
                    If lRootNodeProperty IsNot Nothing Then
                        Dim lNode = lRootNodeProperty.GetValue(vResult)
                        If TypeOf lNode Is SyntaxNode Then
                            pRootNode = DirectCast(lNode, SyntaxNode)
                            Console.WriteLine($"CustomDrawingEditor: Extracted pRootNode from parse result")
                        End If
                    End If
                End If
                
                ' The SourceFileInfo should now have updated CharacterColors
                ' These are used directly by the drawing code for syntax highlighting
                Console.WriteLine($"CustomDrawingEditor: CharacterColors updated, ready to draw new colors")
                
                ' Notify that parsing is complete (raises DocumentParsed event)
                NotifyParsingComplete()
                
                ' Queue redraw to show the updated colors
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"CustomDrawingEditor: Redraw queued for {pFilePath}")
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectManagerParseCompleted error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles identifier map updates from ProjectManager
        ''' </summary>
        ''' <remarks>
        ''' Simply refreshes the display to show updated colors
        ''' </remarks>
        Private Sub OnProjectManagerIdentifierMapUpdated()
            Try
                Console.WriteLine("CustomDrawingEditor: Identifier map updated - refreshing display")
                
                ' The ProjectManager will re-parse and update CharacterColors
                ' We just need to redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectManagerIdentifierMapUpdated error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Clears the identifier case map
        ''' </summary>
        ''' <remarks>
        ''' Called by MainWindow before reloading the map from ProjectManager
        ''' </remarks>
        Public Sub ClearIdentifierCaseMap()
            Try
                pIdentifierCaseMap.Clear()
                Console.WriteLine("Cleared identifier case map")
            Catch ex As Exception
                Console.WriteLine($"ClearIdentifierCaseMap error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles project closed event from ProjectManager
        ''' </summary>
        ''' <remarks>
        ''' Clears any cached data when project closes
        ''' </remarks>
        Private Sub OnProjectManagerProjectClosed()
            Try
                Console.WriteLine($"CustomDrawingEditor: Project closed notification")
                
                ' Clear any cached parse data
                pRootNode = Nothing
                
                ' The SourceFileInfo should handle clearing its own data
                ' We just need to redraw to reflect any changes
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectManagerProjectClosed error: {ex.Message}")
            End Try
        End Sub

        ' ===== ProjectManager Helper Methods =====
        
        ''' <summary>
        ''' Internal method to apply theme settings without null checks
        ''' </summary>
        ''' <param name="vTheme">The theme to apply</param>
        Private Sub ApplyThemeInternal(vTheme As EditorTheme)
            Try
                If vTheme Is Nothing Then Return
                
                Console.WriteLine($"ApplyThemeInternal: Applying theme '{vTheme.Name}'")
                InitializeThemeColorCache()
                ' Update background colors FIRST
                pBackgroundColor = vTheme.BackgroundColor
                pForegroundColor = vTheme.ForegroundColor
                
                ' Update line number colors
                pLineNumberBgColor = vTheme.LineNumberBackgroundColor
                pLineNumberFgColor = vTheme.LineNumberColor
                
                ' Update selection and cursor colors
                pSelectionColor = vTheme.SelectionColor
                pCursorColor = vTheme.CursorColor
                pCurrentLineBgColor = vTheme.CurrentLineColor
                
                ' Set find highlight color
                If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eSelection) Then
                    pFindHighlightColor = vTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection)
                Else
                    pFindHighlightColor = vTheme.SelectionColor
                End If
                
                ' Update syntax colors if available
                If pSyntaxColorSet IsNot Nothing AndAlso vTheme.SyntaxColors IsNot Nothing Then
                    pSyntaxColorSet.UpdateFromTheme(vTheme)
                    ' Create syntax highlighter with the color set
                End If
                
                ' CRITICAL: Update line number widget theme IMMEDIATELY
                ' This ensures the line number background updates with the editor background
                If pLineNumberWidget IsNot Nothing Then
                    pLineNumberWidget.UpdateTheme(vTheme)
                    ' Force immediate redraw of line number widget
                    pLineNumberWidget.QueueDraw()
                End If
                
                ' Apply CSS theme to drawing area for immediate background update
                ApplyThemeToWidget(vTheme)
                
                ' Queue redraw of main drawing area
                pDrawingArea?.QueueDraw()
                
                pThemeApplied = True
                
                Console.WriteLine($"ApplyThemeInternal: Applied theme colors immediately")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyThemeInternal error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Called when the editor becomes visible (tab switched to this editor)
        ''' </summary>
        ''' <remarks>
        ''' This ensures syntax highlighting is properly applied when switching tabs
        ''' </remarks>
        Public Shadows Sub OnShown() Implements IEditor.OnShown
            Try
                Console.WriteLine($"CustomDrawingEditor.OnShown: Editor shown for {pFilePath}")
                
                
                ' Ensure the drawing area is focused for keyboard input
                If pDrawingArea IsNot Nothing AndAlso pDrawingArea.CanFocus Then
                    pDrawingArea.GrabFocus()
                End If
                
                ' Queue a redraw to ensure everything is displayed properly
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnShown error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles theme changes by updating color cache and redrawing
        ''' </summary>
        Public Sub OnThemeChanged()
            Try
                ' Update the cached colors
                InitializeThemeColorCache()
                
                ' Queue a redraw to show new colors
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine("OnThemeChanged: Theme cache updated and redraw queued")
                
            Catch ex As Exception
                Console.WriteLine($"OnThemeChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates character tokens from existing LineMetadata when parse data exists but tokens don't
        ''' </summary>
        Private Sub UpdateCharacterTokensFromMetadata()
            Try
                If pSourceFileInfo Is Nothing OrElse pSourceFileInfo.LineMetadata Is Nothing Then Return
                
                ' Ensure token array exists
                pSourceFileInfo.EnsureCharacterTokens()
                
                ' Update tokens for each line that has metadata
                For i As Integer = 0 To Math.Min(pSourceFileInfo.TextLines.Count - 1, pSourceFileInfo.LineMetadata.Length - 1)
                    Dim lMetadata As LineMetadata = pSourceFileInfo.GetLineMetadata(i)
                    If lMetadata?.SyntaxTokens IsNot Nothing AndAlso lMetadata.SyntaxTokens.Count > 0 Then
                        pSourceFileInfo.UpdateCharacterTokens(i, lMetadata.SyntaxTokens)
                    End If
                Next
                
                Console.WriteLine($"UpdateCharacterTokensFromMetadata: Updated tokens for {pSourceFileInfo.FileName}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateCharacterTokensFromMetadata error: {ex.Message}")
            End Try
        End Sub
    
        ''' <summary>
        ''' Handles text lines changed events from SourceFileInfo
        ''' </summary>
        ''' <param name="vSender">The SourceFileInfo that raised the event</param>
        ''' <param name="vArgs">Event arguments containing change details</param>
        Private Sub OnTextLinesChanged(vSender As Object, vArgs As SourceFileInfo.TextLinesChangedEventArgs)
            Try
                If vArgs Is Nothing Then Return
                
                
                ' Ensure CharacterTokens array is properly sized
                pSourceFileInfo.EnsureCharacterTokens()
                
                ' Update UI components
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                pLineNumberWidget?.QueueDraw()
                
                ' Raise text changed event for MainWindow
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                Console.WriteLine($"OnTextLinesChanged: Updated for {vArgs.ChangeType}, lines affected: {vArgs.LinesAffected}")
                
            Catch ex As Exception
                Console.WriteLine($"OnTextLinesChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles rendering changed events from SourceFileInfo
        ''' </summary>
        ''' <param name="vSender">The SourceFileInfo that raised the event</param>
        ''' <param name="vArgs">Event arguments</param>
        ''' <remarks>
        ''' Queues a redraw of the drawing area when character tokens or
        ''' syntax highlighting needs to be updated
        ''' </remarks>
        Private Sub OnRenderingChanged(vSender As Object, vArgs As EventArgs)
            Try
                ' Simply queue a redraw when rendering changes
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine("OnRenderingChanged: Redraw queued")
                
            Catch ex As Exception
                Console.WriteLine($"OnRenderingChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Hooks up event handlers for SourceFileInfo events
        ''' </summary>
        ''' <remarks>
        ''' This method connects the editor to SourceFileInfo events for
        ''' responding to text changes and rendering updates. Should be called
        ''' in the constructor after pSourceFileInfo is set.
        ''' </remarks>
        Private Sub HookSourceFileInfoEvents()
            Try
                If pSourceFileInfo Is Nothing Then Return
                
                ' Hook up events
                AddHandler pSourceFileInfo.TextLinesChanged, AddressOf OnTextLinesChanged
                AddHandler pSourceFileInfo.RenderingChanged, AddressOf OnRenderingChanged
                
                Console.WriteLine("HookSourceFileInfoEvents: Events connected")
                
            Catch ex As Exception
                Console.WriteLine($"HookSourceFileInfoEvents error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Unhooks event handlers from SourceFileInfo events
        ''' </summary>
        ''' <remarks>
        ''' This method should be called during disposal to prevent memory leaks.
        ''' Removes all event handlers that were connected by HookSourceFileInfoEvents.
        ''' </remarks>
        Private Sub UnhookSourceFileInfoEvents()
            Try
                If pSourceFileInfo Is Nothing Then Return
                
                ' Unhook events
                RemoveHandler pSourceFileInfo.TextLinesChanged, AddressOf OnTextLinesChanged
                RemoveHandler pSourceFileInfo.RenderingChanged, AddressOf OnRenderingChanged
                
                Console.WriteLine("UnhookSourceFileInfoEvents: Events disconnected")
                
            Catch ex As Exception
                Console.WriteLine($"UnhookSourceFileInfoEvents error: {ex.Message}")
            End Try
        End Sub
    
    End Class
    
End Namespace