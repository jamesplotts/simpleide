' Widgets/BuildOutputPanel.vb - Build output panel with CustomDrawDataGrid for errors/warnings
' Modified: 2025-01-02 - Converted to use CustomDrawDataGrid
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports Gtk
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

Namespace Widgets
    
    ''' <summary>
    ''' Build output panel with separate tabs for output, errors, and warnings
    ''' </summary>
    Public Class BuildOutputPanel
        Inherits Box
        
        ' ===== Events =====
        Public Event CloseRequested()
        Public Event ErrorSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event WarningSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event ErrorsCopied()
        Public Event SendErrorsToAI(vErrorsText As String)
        Public Event ErrorDoubleClicked(vError As BuildError)
        
        ' ===== Private Fields =====
        Private pNotebook As CustomDrawNotebook
        Private pCopyButton As Button
        Private pSendToAIButton As Button
        Private pThemeManager As ThemeManager

        
        ' Output tab (still uses ScrolledWindow for text)
        Private pOutputScrolledWindow As ScrolledWindow
        Private pOutputTextView As TextView
        Private pOutputBuffer As TextBuffer
        
        ' Errors tab with DataGrid (NO ScrolledWindow)
        Private pErrorsDataGrid As CustomDrawDataGrid
        
        ' Warnings tab with DataGrid (NO ScrolledWindow)
        Private pWarningsDataGrid As CustomDrawDataGrid
        
        ' Build results
        Private pBuildResult As BuildResult
        Private pBuildErrors As New List(Of BuildError)
        Private pBuildWarnings As New List(Of BuildWarning)
        Private pProjectRoot As String = ""
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new BuildOutputPanel using CustomDrawDataGrid
        ''' </summary>
        Public Sub New(vThemeManager As ThemeManager)
            MyBase.New(Orientation.Vertical, 0)
            pThemeManager = vThemeManager
            
            CreateUI()
            ShowAll()
            
            ' Make sure the notebook shows the Output tab
            If pNotebook IsNot Nothing Then
                pNotebook.CurrentPage = 0
                
                ' Force the notebook to update its display
                If TypeOf pNotebook Is CustomDrawNotebook Then
                    Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                    lCustomNotebook.QueueDraw()
                End If
            End If
        End Sub
        
        ' ===== Public Properties =====
        
        ''' <summary>
        ''' Gets the errors data grid
        ''' </summary>
        Public ReadOnly Property ErrorsDataGrid() As CustomDrawDataGrid
            Get
                Return pErrorsDataGrid
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the warnings data grid
        ''' </summary>
        Public ReadOnly Property WarningsDataGrid() As CustomDrawDataGrid
            Get
                Return pWarningsDataGrid
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the internal notebook widget for tab switching
        ''' </summary>
        Public ReadOnly Property Notebook As CustomDrawNotebook
            Get
                Return pNotebook
            End Get
        End Property
        
        ' ===== UI Creation =====
        
        ''' <summary>
        ''' Creates the UI components
        ''' </summary>
        Private Sub CreateUI()
            Try
                ' Create header bar with buttons
                Dim lHeaderBox As New Box(Orientation.Horizontal, 6)
                lHeaderBox.HeightRequest = 32
                lHeaderBox.MarginStart = 6
                lHeaderBox.MarginEnd = 6
                lHeaderBox.MarginTop = 4
                lHeaderBox.MarginBottom = 4
                
                ' Create title label
                Dim lTitle As New Label("Build output")
                lTitle.Halign = Align.Start
                lHeaderBox.PackStart(lTitle, True, True, 0)
                
                ' Create copy button
                pCopyButton = New Button()
                pCopyButton.Label = "Copy Errors"
                pCopyButton.TooltipText = "Copy all Errors and Warnings to clipboard"
                pCopyButton.Sensitive = False
                AddHandler pCopyButton.Clicked, AddressOf OnCopyButtonClicked
                lHeaderBox.PackStart(pCopyButton, False, False, 0)
                
                ' Create send to AI button
                pSendToAIButton = New Button()
                pSendToAIButton.Label = "Send to AI"
                pSendToAIButton.TooltipText = "Send Errors to AI assistant for help"
                pSendToAIButton.Sensitive = False
                AddHandler pSendToAIButton.Clicked, AddressOf OnSendToAIButtonClicked
                lHeaderBox.PackStart(pSendToAIButton, False, False, 0)
                
                Me.PackStart(lHeaderBox, False, False, 0)
                
                ' Create notebook
                pNotebook = New CustomDrawNotebook(pThemeManager)
                pNotebook.ShowHidePanelButton = False ' Bottom panel needs hide button
                pNotebook.ShowDropDownButton = False  ' No dropdown needed - all tabs fit
                pNotebook.ShowScrollButtons = False    ' No scroll buttons needed - all tabs fit
                pNotebook.ShowTabCloseButtons = False   ' CHANGED: Show close buttons on tabs
                Me.PackStart(pNotebook, True, True, 0)
                
                ' Create tabs
                CreateOutputTab()
                CreateErrorsTab()
                CreateWarningsTab()
                
            Catch ex As Exception
                Console.WriteLine($"BuildOutputPanel.CreateUI error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the output tab with TextView
        ''' </summary>
        Private Sub CreateOutputTab()
            Try
                pOutputScrolledWindow = New ScrolledWindow()
                pOutputScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                pOutputScrolledWindow.ShadowType = Gtk.ShadowType.None
                
                pOutputTextView = New TextView()
                pOutputTextView.Editable = False
                'pOutputTextView.Selectable = True
                pOutputTextView.WrapMode = WrapMode.Word
                pOutputTextView.Monospace = True
                
                pOutputBuffer = pOutputTextView.Buffer
                
                pOutputScrolledWindow.Add(pOutputTextView)
                
                ' Add tags for colored output
                Dim lErrorTag As New TextTag("error")
                lErrorTag.Foreground = "red"
                pOutputBuffer.TagTable.Add(lErrorTag)
                
                Dim lWarningTag As New TextTag("warning")
                lWarningTag.Foreground = "orange"
                pOutputBuffer.TagTable.Add(lWarningTag)
                
                pNotebook.AppendPage(pOutputScrolledWindow,"Output")
                
            Catch ex As Exception
                Console.WriteLine($"CreateOutputTab error: {ex.Message}")
            End Try
        End Sub
        
        
        ' ===== Column Configuration =====
        
        ''' <summary>
        ''' Configures the columns for the errors data grid with auto-expanding message column
        ''' </summary>
        Private Sub ConfigureErrorColumns()
            Try
                pErrorsDataGrid.Columns.Clear()
                
                ' Icon column (if using icons)
                Dim lIconColumn As New DataGridColumn()
                lIconColumn.Name = "Icon"
                lIconColumn.Title = ""
                lIconColumn.Width = 24
                lIconColumn.MinWidth = 24
                lIconColumn.Resizable = False
                lIconColumn.Sortable = False
                lIconColumn.DataType = DataGridColumnType.eIcon
                pErrorsDataGrid.Columns.Add(lIconColumn)
                
                ' File column
                Dim lFileColumn As New DataGridColumn()
                lFileColumn.Name = "File"
                lFileColumn.Title = "File"
                lFileColumn.Width = 200
                lFileColumn.MinWidth = 100
                lFileColumn.Resizable = True
                lFileColumn.Sortable = True
                lFileColumn.DataType = DataGridColumnType.eText
                lFileColumn.Ellipsize = True
                pErrorsDataGrid.Columns.Add(lFileColumn)
                
                ' Line column
                Dim lLineColumn As New DataGridColumn()
                lLineColumn.Name = "Line"
                lLineColumn.Title = "Line"
                lLineColumn.Width = 60
                lLineColumn.MinWidth = 40
                lLineColumn.Resizable = True
                lLineColumn.Sortable = True
                lLineColumn.DataType = DataGridColumnType.eNumber
                lLineColumn.Alignment = ColumnAlignment.eRight
                pErrorsDataGrid.Columns.Add(lLineColumn)
                
                ' Column column
                Dim lColColumn As New DataGridColumn()
                lColColumn.Name = "Column"
                lColColumn.Title = "Col"
                lColColumn.Width = 50
                lColColumn.MinWidth = 40
                lColColumn.Resizable = True
                lColColumn.Sortable = True
                lColColumn.DataType = DataGridColumnType.eNumber
                lColColumn.Alignment = ColumnAlignment.eRight
                pErrorsDataGrid.Columns.Add(lColColumn)
                
                ' Code column
                Dim lCodeColumn As New DataGridColumn()
                lCodeColumn.Name = "Code"
                lCodeColumn.Title = "Code"
                lCodeColumn.Width = 100
                lCodeColumn.MinWidth = 60
                lCodeColumn.Resizable = True
                lCodeColumn.Sortable = True
                lCodeColumn.DataType = DataGridColumnType.eText
                pErrorsDataGrid.Columns.Add(lCodeColumn)
                
                ' Message column - AUTO-EXPAND AND WORD WRAP
                Dim lMessageColumn As New DataGridColumn()
                lMessageColumn.Name = "Message"
                lMessageColumn.Title = "Message"
                lMessageColumn.Width = 400  ' Initial width, will be overridden by AutoExpand
                lMessageColumn.MinWidth = 200
                lMessageColumn.Resizable = True
                lMessageColumn.Sortable = True
                lMessageColumn.DataType = DataGridColumnType.eText
                lMessageColumn.AutoExpand = True      ' Enable auto-expand to fill space
                lMessageColumn.WordWrap = True         ' Enable word wrapping
                lMessageColumn.MaxHeight = 120         ' Maximum height when wrapped
                lMessageColumn.Ellipsize = False       ' Don't ellipsize when word wrapping
                pErrorsDataGrid.Columns.Add(lMessageColumn)
                
                ' Enable word wrap on the grid for the message column
                pErrorsDataGrid.EnableWordWrap(5, True, 120)  ' Column index 5 (Message)
                
            Catch ex As Exception
                Console.WriteLine($"ConfigureErrorColumns error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Configures the columns for the warnings data grid with auto-expanding message column
        ''' </summary>
        Private Sub ConfigureWarningColumns()
            Try
                pWarningsDataGrid.Columns.Clear()
                
                ' Icon column (if using icons)
                Dim lIconColumn As New DataGridColumn()
                lIconColumn.Name = "Icon"
                lIconColumn.Title = ""
                lIconColumn.Width = 24
                lIconColumn.MinWidth = 24
                lIconColumn.Resizable = False
                lIconColumn.Sortable = False
                lIconColumn.DataType = DataGridColumnType.eIcon
                pWarningsDataGrid.Columns.Add(lIconColumn)
                
                ' File column
                Dim lFileColumn As New DataGridColumn()
                lFileColumn.Name = "File"
                lFileColumn.Title = "File"
                lFileColumn.Width = 200
                lFileColumn.MinWidth = 100
                lFileColumn.Resizable = True
                lFileColumn.Sortable = True
                lFileColumn.DataType = DataGridColumnType.eText
                lFileColumn.Ellipsize = True
                pWarningsDataGrid.Columns.Add(lFileColumn)
                
                ' Line column
                Dim lLineColumn As New DataGridColumn()
                lLineColumn.Name = "Line"
                lLineColumn.Title = "Line"
                lLineColumn.Width = 60
                lLineColumn.MinWidth = 40
                lLineColumn.Resizable = True
                lLineColumn.Sortable = True
                lLineColumn.DataType = DataGridColumnType.eNumber
                lLineColumn.Alignment = ColumnAlignment.eRight
                pWarningsDataGrid.Columns.Add(lLineColumn)
                
                ' Column column
                Dim lColColumn As New DataGridColumn()
                lColColumn.Name = "Column"
                lColColumn.Title = "Col"
                lColColumn.Width = 50
                lColColumn.MinWidth = 40
                lColColumn.Resizable = True
                lColColumn.Sortable = True
                lColColumn.DataType = DataGridColumnType.eNumber
                lColColumn.Alignment = ColumnAlignment.eRight
                pWarningsDataGrid.Columns.Add(lColColumn)
                
                ' Code column
                Dim lCodeColumn As New DataGridColumn()
                lCodeColumn.Name = "Code"
                lCodeColumn.Title = "Code"
                lCodeColumn.Width = 100
                lCodeColumn.MinWidth = 60
                lCodeColumn.Resizable = True
                lCodeColumn.Sortable = True
                lCodeColumn.DataType = DataGridColumnType.eText
                pWarningsDataGrid.Columns.Add(lCodeColumn)
                
                ' Message column - AUTO-EXPAND AND WORD WRAP
                Dim lMessageColumn As New DataGridColumn()
                lMessageColumn.Name = "Message"
                lMessageColumn.Title = "Message"
                lMessageColumn.Width = 400  ' Initial width, will be overridden by AutoExpand
                lMessageColumn.MinWidth = 200
                lMessageColumn.Resizable = True
                lMessageColumn.Sortable = True
                lMessageColumn.DataType = DataGridColumnType.eText
                lMessageColumn.AutoExpand = True      ' Enable auto-expand to fill space
                lMessageColumn.WordWrap = True         ' Enable word wrapping
                lMessageColumn.MaxHeight = 120         ' Maximum height when wrapped
                lMessageColumn.Ellipsize = False       ' Don't ellipsize when word wrapping
                pWarningsDataGrid.Columns.Add(lMessageColumn)
                
                ' Enable word wrap on the grid for the message column
                pWarningsDataGrid.EnableWordWrap(5, True, 120)  ' Column index 5 (Message)
                
            Catch ex As Exception
                Console.WriteLine($"ConfigureWarningColumns error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Shows build result by populating error/warning grids
        ''' </summary>
        Public Sub ShowBuildResult(vResult As BuildResult, vProjectRoot As String)
            Try
                Console.WriteLine($"ShowBuildResult Called! UseDataGrid={pUseDataGrid}")
                Console.WriteLine($"Errors: {vResult?.Errors?.Count}, Warnings: {vResult?.Warnings?.Count}")
                
                ShowBuildResultDataGrid(vResult, vProjectRoot)
                
            Catch ex As Exception
                Console.WriteLine($"ShowBuildResult error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Appends text to the output tab with thread-safe buffer handling
        ''' </summary>
        ''' <param name="vText">Text to append</param>
        Public Sub AppendOutput(vText As String)
            Try
                ' Ensure we're on the UI thread
                If Not Application.EventsPending() Then
                    Application.Invoke(Sub() AppendOutput(vText))
                    Return
                End If
                
                ' Use marks instead of iterators for position tracking
                Dim lEndMark As TextMark = pOutputBuffer.CreateMark(Nothing, pOutputBuffer.EndIter, False)
                pOutputBuffer.PlaceCursor(pOutputBuffer.GetIterAtMark(lEndMark))
                pOutputBuffer.InsertAtCursor(vText)
                
                ' Scroll using the mark (which survives buffer changes)
                ScrollOutputToBottom()
                
                ' Clean up the mark
                pOutputBuffer.DeleteMark(lEndMark)
                
            Catch ex As Exception
                Console.WriteLine($"AppendOutput error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Appends text with a specific tag to the output tab
        ''' </summary>
        ''' <param name="vText">Text to append</param>
        ''' <param name="vTag">Tag name to apply</param>
        Public Sub AppendOutput(vText As String, vTag As String)
            Try
                ' Ensure we're on the UI thread
                If Not Application.EventsPending() Then
                    Application.Invoke(Sub() AppendOutput(vText, vTag))
                    Return
                End If
                
                Dim lTag As TextTag = pOutputBuffer.TagTable.Lookup(vTag)
                
                ' Store the offset before inserting
                Dim lStartOffset As Integer = pOutputBuffer.CharCount
                
                ' Insert the text
                pOutputBuffer.PlaceCursor(pOutputBuffer.EndIter)
                pOutputBuffer.InsertAtCursor(vText)
                
                ' Apply the tag if found using offsets (which are stable)
                If lTag IsNot Nothing Then
                    Dim lStartIter As TextIter = pOutputBuffer.GetIterAtOffset(lStartOffset)
                    Dim lEndIter As TextIter = pOutputBuffer.EndIter
                    pOutputBuffer.ApplyTag(lTag, lStartIter, lEndIter)
                End If
                
                ScrollOutputToBottom()
                pNotebook.SetCurrentTab(0, True)
            Catch ex As Exception
                Console.WriteLine($"AppendOutput(tag) error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Appends a line of text to the output
        ''' </summary>
        Public Sub AppendOutputLine(vText As String)
            AppendOutput(vText & Environment.NewLine)
        End Sub
        
        ''' <summary>
        ''' Clears all output and error/warning lists
        ''' </summary>
        Public Sub ClearOutput()
            Try
                pOutputBuffer.Clear()
                pBuildErrors.Clear()
                pBuildWarnings.Clear()
                pErrorsDataGrid.ClearRows()
                pWarningsDataGrid.ClearRows()
                UpdateTabLabels()
                UpdateCopyButtonState()
            Catch ex As Exception
                Console.WriteLine($"ClearOutput error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Switches to the output tab
        ''' </summary>
        Public Sub SwitchToOutputTab()
            pNotebook.CurrentPage = 0
        End Sub
        
        ''' <summary>
        ''' Switches to the build output tab (alias)
        ''' </summary>
        Public Sub SwitchToBuildOutput()
            SwitchToOutputTab()
        End Sub
        
        ''' <summary>
        ''' Sets the project root path
        ''' </summary>
        Public Sub SetProjectRoot(vProjectRoot As String)
            pProjectRoot = vProjectRoot
        End Sub
        
        ''' <summary>
        ''' Gets the errors as formatted text
        ''' </summary>
        Public Function GetErrorsAsText() As String
            Try
                Return FormatErrorsForClipboard()
            Catch ex As Exception
                Console.WriteLine($"GetErrorsAsText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the list of build errors
        ''' </summary>
        Public Function GetErrors() As List(Of BuildError)
            Return pBuildErrors
        End Function
        
        ''' <summary>
        ''' Gets the list of build warnings
        ''' </summary>
        Public Function GetWarnings() As List(Of BuildWarning)
            Return pBuildWarnings
        End Function
        
        ''' <summary>
        ''' Copies all errors and warnings to the clipboard
        ''' </summary>
        Public Sub CopyErrorsToClipboard()
            Try
                Dim lText As String = FormatErrorsForClipboard()
                If Not String.IsNullOrEmpty(lText) Then
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                    lClipboard.Text = lText
                    
                    Console.WriteLine($"Copied {pBuildErrors.Count} errors and {pBuildWarnings.Count} warnings to clipboard")
                    RaiseEvent ErrorsCopied()
                Else
                    Console.WriteLine("No errors or warnings to copy")
                End If
            Catch ex As Exception
                Console.WriteLine($"CopyErrorsToClipboard error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Creates a text cell for the data grid
        ''' </summary>
        Private Function CreateTextCell(vText As String) As DataGridCell
            Return New DataGridCell(vText, vText)
        End Function

        ''' <summary>
        ''' Creates a number cell for the data grid
        ''' </summary>
        Private Function CreateNumberCell(vNumber As Integer) As DataGridCell
            Return New DataGridCell(vNumber, vNumber.ToString())
        End Function 
       
        ''' <summary>
        ''' Updates the tab labels with error and warning counts using Pango markup for colors
        ''' </summary>
        Private Sub UpdateTabLabels()
            Try
                ' Get theme colors
                Dim lErrorColor As String = "#FF0000"  ' Default red
                Dim lWarningColor As String = "#FFA500"  ' Default orange
                
                ' Try to get theme colors if available
                If pThemeManager IsNot Nothing Then
                    Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                    If lTheme IsNot Nothing Then
                        lErrorColor = lTheme.ErrorColor
                        lWarningColor = lTheme.WarningColor
                    End If
                End If
                
                ' Update error tab label with color using CustomDrawNotebook's markup support
                If pNotebook IsNot Nothing AndAlso TypeOf pNotebook Is CustomDrawNotebook Then
                    Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                    
                    ' Update Errors tab (index 1)
                    If lCustomNotebook.NPages > 1 Then
                        If pBuildErrors.Count > 0 Then
                            ' Use markup to color the text
                            lCustomNotebook.SetTabLabelText(1, $"<span foreground='{lErrorColor}'>Errors ({pBuildErrors.Count})</span>")
                        Else
                            ' No errors - use normal text
                            lCustomNotebook.SetTabLabelText(1, "Errors (0)")
                        End If
                    End If
                    
                    ' Update Warnings tab (index 2)
                    If lCustomNotebook.NPages > 2 Then
                        If pBuildWarnings.Count > 0 Then
                            ' Use markup to color the text
                            lCustomNotebook.SetTabLabelText(2, $"<span foreground='{lWarningColor}'>Warnings ({pBuildWarnings.Count})</span>")
                        Else
                            ' No warnings - use normal text
                            lCustomNotebook.SetTabLabelText(2, "Warnings (0)")
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateTabLabels error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the enabled state of copy and send buttons
        ''' </summary>
        Private Sub UpdateCopyButtonState()
            Try
                Dim lHasContent As Boolean = (pBuildErrors.Count > 0 OrElse pBuildWarnings.Count > 0)
                pCopyButton.Sensitive = lHasContent
                pSendToAIButton.Sensitive = lHasContent
            Catch ex As Exception
                Console.WriteLine($"UpdateCopyButtonState error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Scrolls the output view to the bottom using marks
        ''' </summary>
        Private Sub ScrollOutputToBottom()
            Try
                ' Use a mark to track the end position
                Dim lEndMark As TextMark = pOutputBuffer.CreateMark(Nothing, pOutputBuffer.EndIter, False)
                pOutputTextView.ScrollToMark(lEndMark, 0.0, False, 0.0, 0.0)
                pOutputBuffer.DeleteMark(lEndMark)
            Catch ex As Exception
                Console.WriteLine($"ScrollOutputToBottom error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Formats errors and warnings for clipboard
        ''' </summary>
        Private Function FormatErrorsForClipboard() As String
            Try
                Dim lBuilder As New System.Text.StringBuilder()
                
                If pBuildErrors.Count > 0 Then
                    lBuilder.AppendLine($"=== Errors ({pBuildErrors.Count}) ===")
                    for each lError As BuildError in pBuildErrors
                        lBuilder.AppendLine($"{lError.FilePath}({lError.Line},{lError.Column}): error {lError.ErrorCode}: {lError.Message}")
                    Next
                    lBuilder.AppendLine()
                End If
                
                If pBuildWarnings.Count > 0 Then
                    lBuilder.AppendLine($"=== Warnings ({pBuildWarnings.Count}) ===")
                    for each lWarning As BuildWarning in pBuildWarnings
                        lBuilder.AppendLine($"{lWarning.FilePath}({lWarning.Line},{lWarning.Column}): warning {lWarning.WarningCode}: {lWarning.Message}")
                    Next
                End If
                
                Return lBuilder.ToString()
            Catch ex As Exception
                Console.WriteLine($"FormatErrorsForClipboard error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handles selection change in error grid
        ''' </summary>
        Private Sub OnErrorSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer, vRow As DataGridRow)
            Try
                If vRow?.Tag IsNot Nothing Then
                    Dim lError As BuildError = TryCast(vRow.Tag, BuildError)
                    If lError IsNot Nothing Then
                        RaiseEvent ErrorSelected(lError.FilePath, lError.Line, lError.Column)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"OnErrorRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles double-click on warning row
        ''' </summary>
        Private Sub OnWarningRowDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
            Try
                If vRow?.Tag IsNot Nothing Then
                    Dim lWarning As BuildWarning = TryCast(vRow.Tag, BuildWarning)
                    If lWarning IsNot Nothing Then
                        RaiseEvent WarningSelected(lWarning.FilePath, lWarning.Line, lWarning.Column)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"OnWarningRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles selection change in warning grid
        ''' </summary>
        Private Sub OnWarningSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer, vRow As DataGridRow)
            Try
                If vRow?.Tag IsNot Nothing Then
                    Dim lError As BuildError = TryCast(vRow.Tag, BuildError)
                    If lError IsNot Nothing Then
                        RaiseEvent ErrorSelected(lError.FilePath, lError.Line, lError.Column)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"OnErrorRowDoubleClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles copy button click
        ''' </summary>
        Private Sub OnCopyButtonClicked(vSender As Object, vArgs As EventArgs)
            CopyErrorsToClipboard()
        End Sub
        
        ''' <summary>
        ''' Handles send to AI button click
        ''' </summary>
        Private Sub OnSendToAIButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                Dim lText As String = FormatErrorsForClipboard()
                If Not String.IsNullOrEmpty(lText) Then
                    RaiseEvent SendErrorsToAI(lText)
                End If
            Catch ex As Exception
                Console.WriteLine($"OnSendToAIButtonClicked error: {ex.Message}")
            End Try
        End Sub


        
        ''' <summary>
        ''' Applies theme colors to the output text view
        ''' </summary>
        ''' <param name="vTheme">The theme to apply</param>
        Private Sub ApplyThemeToOutputView(vTheme As EditorTheme)
            Try
                If pOutputTextView Is Nothing OrElse vTheme Is Nothing Then Return
                
                ' Apply CSS to style the TextView
                Dim lCss As String = String.Format(
                    "textview {{ background-color: {0}; color: {1}; font-family: monospace; font-size: 10pt; }}",
                    vTheme.BackgroundColor,
                    vTheme.ForegroundColor)
                
                CssHelper.ApplyCssToWidget(pOutputTextView, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                
            Catch ex As Exception
                Console.WriteLine($"BuildOutputPanel.ApplyThemeToOutputView error: {ex.Message}")
            End Try
        End Sub

        ' ===== Icon Rendering Methods =====
        
        ''' <summary>
        ''' Handles icon rendering for the error data grid
        ''' </summary>
        Private Sub OnErrorGridRenderIcon(vArgs As IconRenderEventArgs)
            Try
                Dim lIconType As String = If(vArgs.Cell.Value?.ToString(), "").ToLower()
                
                Select Case lIconType
                    Case "error"
                        DrawErrorIcon(vArgs.Context, vArgs.X, vArgs.Y, vArgs.Width, vArgs.Height)
                        vArgs.Handled = True
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnErrorGridRenderIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles icon rendering for the warning data grid
        ''' </summary>
        Private Sub OnWarningGridRenderIcon(vArgs As IconRenderEventArgs)
            Try
                Dim lIconType As String = If(vArgs.Cell.Value?.ToString(), "").ToLower()
                
                Select Case lIconType
                    Case "warning"
                        DrawWarningIcon(vArgs.Context, vArgs.X, vArgs.Y, vArgs.Width, vArgs.Height)
                        vArgs.Handled = True
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnWarningGridRenderIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws an error icon (red circle with X)
        ''' </summary>
        Private Sub DrawErrorIcon(vContext As Cairo.Context, vX As Double, vY As Double, 
                                 vWidth As Double, vHeight As Double)
            Try
                Dim lCenterX As Double = vX + vWidth / 2
                Dim lCenterY As Double = vY + vHeight / 2
                Dim lRadius As Double = Math.Min(vWidth, vHeight) / 3
                
                ' Draw red circle
                vContext.SetSourceRGB(0.8, 0.1, 0.1)  ' Dark red
                vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                vContext.Fill()
                
                ' Draw white X
                vContext.SetSourceRGB(1, 1, 1)  ' White
                vContext.LineWidth = 2
                vContext.MoveTo(lCenterX - lRadius * 0.5, lCenterY - lRadius * 0.5)
                vContext.LineTo(lCenterX + lRadius * 0.5, lCenterY + lRadius * 0.5)
                vContext.MoveTo(lCenterX + lRadius * 0.5, lCenterY - lRadius * 0.5)
                vContext.LineTo(lCenterX - lRadius * 0.5, lCenterY + lRadius * 0.5)
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawErrorIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a warning icon (orange triangle with !)
        ''' </summary>
        Private Sub DrawWarningIcon(vContext As Cairo.Context, vX As Double, vY As Double, 
                                   vWidth As Double, vHeight As Double)
            Try
                Dim lCenterX As Double = vX + vWidth / 2
                Dim lCenterY As Double = vY + vHeight / 2
                Dim lSize As Double = Math.Min(vWidth, vHeight) * 0.7
                
                ' Draw orange triangle
                vContext.SetSourceRGB(1, 0.6, 0)  ' Orange
                vContext.MoveTo(lCenterX, lCenterY - lSize / 2)
                vContext.LineTo(lCenterX - lSize / 2, lCenterY + lSize / 2)
                vContext.LineTo(lCenterX + lSize / 2, lCenterY + lSize / 2)
                vContext.ClosePath()
                vContext.Fill()
                
                ' Draw exclamation mark
                vContext.SetSourceRGB(1, 1, 1)  ' White
                vContext.LineWidth = 2
                vContext.MoveTo(lCenterX, lCenterY - lSize * 0.2)
                vContext.LineTo(lCenterX, lCenterY + lSize * 0.1)
                vContext.Stroke()
                
                ' Draw dot
                vContext.Arc(lCenterX, lCenterY + lSize * 0.25, 1.5, 0, Math.PI * 2)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawWarningIcon error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Configures subtle row highlighting for errors (OPTIONAL)
        ''' </summary>
        Private Sub ConfigureErrorGridColors()
            Try
                ' Option 1: Simple style-based colors (if you want subtle backgrounds)
                ' Dim lStyleColors As New Dictionary(Of RowStyle, String)
                ' lStyleColors.Add(RowStyle.eError, "#3D1414")    ' Very subtle dark red
                ' pErrorsDataGrid.SetRowStyleColors(lStyleColors)
                
                ' Option 2: More complex custom logic
                pErrorsDataGrid.GetRowBackgroundColor = AddressOf GetErrorRowColor
                
                ' Option 3: Just use normal colors (current approach - recommended)
                ' Don't set any overrides - let the grid use standard colors
                
            Catch ex As Exception
                Console.WriteLine($"ConfigureErrorGridColors error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Custom row color logic for errors (OPTIONAL - example only)
        ''' </summary>
        Private Function GetErrorRowColor(vRowIndex As Integer, vRow As DataGridRow, 
                                         vIsSelected As Boolean, vIsHover As Boolean) As String
            ' Priority order
            If vIsSelected Then Return pErrorsDataGrid.SelectionColor
            If vIsHover Then Return pErrorsDataGrid.HoverColor
            
            ' Example: Color based on error severity (if stored in Tag)
            If vRow.Tag IsNot Nothing Then
                Dim lError As BuildError = TryCast(vRow.Tag, BuildError)
                If lError IsNot Nothing Then
                    ' Could check error code or other properties
                    If lError.ErrorCode.StartsWith("BC4") Then
                        ' Critical errors - slightly red tinted
                        Return If(IsDarkTheme(), "#2D1414", "#FFE5E5")
                    End If
                End If
            End If
            
            ' Default alternating rows
            If pErrorsDataGrid.AlternateRowColors AndAlso (vRowIndex Mod 2 = 1) Then
                Return pErrorsDataGrid.AlternateRowColor
            End If
            
            Return pErrorsDataGrid.BackgroundColor
        End Function
        
        ''' <summary>
        ''' Helper to determine if using dark theme
        ''' </summary>
        Private Function IsDarkTheme() As Boolean
            Try
                ' Check the grid's background color brightness
                Dim lBgColor As New Gdk.RGBA()
                If lBgColor.Parse(pErrorsDataGrid.BackgroundColor) Then
                    Dim lBrightness As Double = (lBgColor.Red * 0.299 + lBgColor.Green * 0.587 + lBgColor.Blue * 0.114)
                    Return lBrightness < 0.5
                End If
                Return False
            Catch
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Clears only the output text, preserving error/warning lists and tab labels
        ''' </summary>
        Public Sub ClearOutputOnly()
            Try
                ' Clear only the output text buffer
                pOutputBuffer.Clear()
                
                ' Do NOT clear errors/warnings
                ' Do NOT clear data grids  
                ' Do NOT update tab labels (preserve counts and colors)
                ' Do NOT update copy button state (preserve enabled state)
                
                ' Just clear the text output, nothing else
                
            Catch ex As Exception
                Console.WriteLine($"ClearOutputOnly error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the ThemeManager for accessing theme colors
        ''' </summary>
        ''' <param name="vThemeManager">The ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
               
                pNotebook.SetThemeManager(vThemeManager)
                
                ' Pass theme manager to data grids if they support it
                If pErrorsDataGrid IsNot Nothing Then
                    pErrorsDataGrid.SetThemeManager(vThemeManager)
                End If
                
                If pWarningsDataGrid IsNot Nothing Then
                    pWarningsDataGrid.SetThemeManager(vThemeManager)
                End If
                
                ' Apply theme to the output TextView
                If vThemeManager IsNot Nothing Then
                    Dim lTheme As EditorTheme = vThemeManager.GetCurrentThemeObject()
                    If lTheme IsNot Nothing Then
                        ApplyThemeToOutputView(lTheme)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildOutputPanel.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        
    End Class

End Namespace