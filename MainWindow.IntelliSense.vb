' MainWindow.CodeSense.vb - Complete fixed implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Editors
Imports SimpleIDE.Syntax

Partial Public Class MainWindow
    
    ' CodeSense components
    'Private pCodeSenseEngine As CodeSenseEngine
    Private pCodeSenseWindow As Window
    Private pCodeSenseTreeView As TreeView
    Private pCodeSenseListStore As ListStore
    Private pCodeSenseTimer As UInteger = 0
    
    ' Initialize CodeSense system
    Private Sub InitializeCodeSense()
        Try
            ' Create CodeSense engine
            pCodeSenseEngine = New CodeSenseEngine()
            
            ' Update references when project changes
            AddHandler Me.ProjectChanged, AddressOf OnProjectChangedForCodeSense
            
        Catch ex As Exception
            Console.WriteLine($"InitializeCodeSense error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle project change for CodeSense
    Private Sub OnProjectChangedForCodeSense(vProjectFile As String)
        Try
            HideBottomPanel()
            UpdateCodeSenseReferences()
        Catch ex As Exception
            Console.WriteLine($"OnProjectChangedForCodeSense error: {ex.Message}")
        End Try
    End Sub
    
    ' Update CodeSense references from project
    Private Sub UpdateCodeSenseReferences()
        Try
            If pCodeSenseEngine Is Nothing Then Return
            
            ' Clear existing references
            pCodeSenseEngine.ClearReferences()
            
            ' Add core references
            pCodeSenseEngine.AddReference("System")
            pCodeSenseEngine.AddReference("System.Core")
            pCodeSenseEngine.AddReference("Microsoft.VisualBasic")
            
            ' Add project references
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                ' Parse project file to get references
                Dim lProjectInfo As ProjectFileParser.ProjectInfo = ProjectFileParser.ParseProjectFile(pCurrentProject)
                
                ' Add assembly references
                For Each lRef In lProjectInfo.References
                    Try
                        pCodeSenseEngine.AddReference(lRef.Name)
                    Catch ex As Exception
                        Console.WriteLine($"Failed to add Reference {lRef.Name}: {ex.Message}")
                    End Try
                Next
                
                ' Add package references
                For Each lPackage In lProjectInfo.PackageReferences
                    Try
                        pCodeSenseEngine.AddReference(lPackage.Name)
                    Catch ex As Exception
                        Console.WriteLine($"Failed to add Package Reference {lPackage.Name}: {ex.Message}")
                    End Try
                Next
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateCodeSenseReferences error: {ex.Message}")
        End Try
    End Sub
    
'    ' Handle text changed in editor for CodeSense
'    Public Sub OnEditorTextChanged(vSender As Object, vArgs As EventArgs)
'        OnEditorTextChangedEnhanced(vSender, vArgs)
'    End Sub
    
    ' Get CodeSense context from editor
    Private Function GetCodeSenseContext(vEditor As IEditor) As CodeSenseContext
        Try
            Dim lContext As New CodeSenseContext()
            
            ' Get basic context
            lContext.TriggerPosition = vEditor.GetCursorPosition()
            lContext.FileType = "vb"
            
            ' Get text and cursor position
            Dim lText As String = vEditor.Text
            Dim lCursorPos As Integer = GetOffsetFromPosition(lText, lContext.TriggerPosition.Line, lContext.TriggerPosition.Column)
            
            ' Get current line
            Dim lLines() As String = lText.Split({vbLf, vbCr}, StringSplitOptions.None)
            If lContext.TriggerPosition.Line < lLines.Length Then
                lContext.LineText = lLines(lContext.TriggerPosition.Line)
            Else
                lContext.LineText = ""
            End If
            
            ' Determine trigger character
            If lCursorPos > 0 AndAlso lCursorPos <= lText.Length Then
                lContext.TriggerChar = lText(lCursorPos - 1)
            End If
            
            ' Extract current word
            Dim lWordStart As Integer = lCursorPos
            Dim lWordEnd As Integer = lCursorPos
            
            ' Find word start
            While lWordStart > 0 AndAlso (Char.IsLetterOrDigit(lText(lWordStart - 1)) OrElse lText(lWordStart - 1) = "_"c)
                lWordStart -= 1
            End While
            
            ' Find word end
            While lWordEnd < lText.Length AndAlso (Char.IsLetterOrDigit(lText(lWordEnd)) OrElse lText(lWordEnd) = "_"c)
                lWordEnd += 1
            End While
            
            lContext.CurrentWord = lText.Substring(lWordStart, lWordEnd - lWordStart)
            
            ' Store word start offset in a local variable for later use
            Dim lWordStartOffset As Integer = lWordStart
            
            ' Get prefix (text before current word) - store in local variable
            Dim lPrefix As String = ""
            If lWordStart > 0 Then
                Dim lLineStart As Integer = lText.LastIndexOf(vbLf, lWordStart - 1) + 1
                lPrefix = lText.Substring(lLineStart, lWordStart - lLineStart)
            End If
            
            ' Update document nodes if available
            Dim lTabInfo As TabInfo = GetCurrentTabInfo()
            If lTabInfo IsNot Nothing AndAlso TypeOf lTabInfo.Editor Is CustomDrawingEditor Then
                Dim lCustomEditor As CustomDrawingEditor = DirectCast(lTabInfo.Editor, CustomDrawingEditor)
                pCodeSenseEngine.UpdateDocumentNodes(lCustomEditor.GetDocumentNodes(), lCustomEditor.GetRootNodes())
            End If
            
            Return lContext
            
        Catch ex As Exception
            Console.WriteLine($"GetCodeSenseContext error: {ex.Message}")
            Return Nothing
        End Try
    End Function
    
    ' Helper method to convert text offset to line/column
    Private Function GetOffsetFromPosition(vText As String, vLine As Integer, vColumn As Integer) As Integer
        Dim lLines() As String = vText.Split({vbLf, vbCr}, StringSplitOptions.None)
        Dim lOffset As Integer = 0
        
        ' Add lengths of all lines before the current line
        For i As Integer = 0 To Math.Min(vLine - 1, lLines.Length - 1)
            lOffset += lLines(i).Length + 1 ' +1 for Line break
        Next
        
        ' Add column offset
        If vLine < lLines.Length Then
            lOffset += Math.Min(vColumn, lLines(vLine).Length)
        End If
        
        Return lOffset
    End Function
    
    ' Helper method to convert text offset to EditorPosition
    Private Function GetPositionFromOffset(vText As String, vOffset As Integer) As EditorPosition
        Dim lLines() As String = vText.Split({vbLf, vbCr}, StringSplitOptions.None)
        Dim lCurrentOffset As Integer = 0
        
        For lLine As Integer = 0 To lLines.Length - 1
            Dim lLineLength As Integer = lLines(lLine).Length
            
            If lCurrentOffset + lLineLength >= vOffset Then
                ' Found the line
                Dim lColumn As Integer = vOffset - lCurrentOffset
                Return New EditorPosition(lLine, lColumn)
            End If
            
            lCurrentOffset += lLineLength + 1 ' +1 for Line break
        Next
        
        ' Past end of text
        Return New EditorPosition(lLines.Length - 1, If(lLines.Length > 0, lLines(lLines.Length - 1).Length, 0))
    End Function
    
    ' Show CodeSense for given context
    Private Sub ShowCodeSenseForContext(vEditor As IEditor, vContext As CodeSenseContext)
        Try
            ' Get suggestions
            Dim lSuggestions As List(Of CodeSenseSuggestion) = pCodeSenseEngine.GetSuggestions(vContext)
            
            If lSuggestions Is Nothing OrElse lSuggestions.Count = 0 Then
                HideCodeSense()
                Return
            End If
            
            ' Create or update CodeSense window
            If pCodeSenseWindow Is Nothing Then
                CreateCodeSenseWindow()
            End If
            
            ' Update list store
            pCodeSenseListStore.Clear()
            
            For Each lSuggestion In lSuggestions
                pCodeSenseListStore.AppendValues(
                    lSuggestion.Icon,
                    lSuggestion.Text,           ' Use Text instead of Name
                    lSuggestion.Description     ' Use Description instead of TypeName
                )
            Next
            
            ' Position and show window
            Dim lPosition As Gdk.Point = CalculateCodeSensePosition(vContext)
            pCodeSenseWindow.Move(lPosition.x, lPosition.y)
            pCodeSenseWindow.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"ShowCodeSenseForContext error: {ex.Message}")
        End Try
    End Sub
    
    ' Create CodeSense window
    Private Sub CreateCodeSenseWindow()
        Try
            ' Create window
            pCodeSenseWindow = New Window(WindowType.Popup)
            pCodeSenseWindow.TypeHint = Gdk.WindowTypeHint.PopupMenu
            pCodeSenseWindow.Decorated = False
            pCodeSenseWindow.SkipTaskbarHint = True
            pCodeSenseWindow.SkipPagerHint = True
            
            ' Create scrolled window
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.SetSizeRequest(300, 200)
            
            ' Create list store and tree view
            pCodeSenseListStore = New ListStore(GetType(String), GetType(String), GetType(String))
            pCodeSenseTreeView = New TreeView(pCodeSenseListStore)
            pCodeSenseTreeView.HeadersVisible = False
            
            ' Add columns
            Dim lIconColumn As New TreeViewColumn()
            Dim lIconRenderer As New CellRendererText()
            lIconColumn.PackStart(lIconRenderer, False)
            lIconColumn.AddAttribute(lIconRenderer, "text", 0)
            pCodeSenseTreeView.AppendColumn(lIconColumn)
            
            Dim lTextColumn As New TreeViewColumn()
            Dim lTextRenderer As New CellRendererText()
            lTextColumn.PackStart(lTextRenderer, True)
            lTextColumn.AddAttribute(lTextRenderer, "text", 1)
            pCodeSenseTreeView.AppendColumn(lTextColumn)
            
            ' Handle selection
            AddHandler pCodeSenseTreeView.RowActivated, AddressOf OnCodeSenseItemActivated
            
            ' Handle key press
            AddHandler pCodeSenseWindow.KeyPressEvent, AddressOf OnCodeSenseKeyPress
            
            ' Add to window
            lScrolled.Add(pCodeSenseTreeView)
            pCodeSenseWindow.Add(lScrolled)
            
        Catch ex As Exception
            Console.WriteLine($"CreateCodeSenseWindow error: {ex.Message}")
        End Try
    End Sub
    
    ' Calculate CodeSense window position
    Private Function CalculateCodeSensePosition(vContext As CodeSenseContext) As Gdk.Point
        Try
            ' Get window position
            Dim lWindowX, lWindowY As Integer
            Me.GetPosition(lWindowX, lWindowY)
            
            ' Estimate character position (rough approximation)
            Dim lLineHeight As Integer = 20 ' Approximate
            Dim lCharWidth As Integer = 8   ' Approximate
            
            Dim lX As Integer = lWindowX + (vContext.TriggerPosition.Column * lCharWidth)
            Dim lY As Integer = lWindowY + ((vContext.TriggerPosition.Line + 1) * lLineHeight)
            
            Return New Gdk.Point(lX, lY)
            
        Catch ex As Exception
            Console.WriteLine($"CalculateCodeSensePosition error: {ex.Message}")
            Return New Gdk.Point(100, 100) ' Default position
        End Try
    End Function
    
    ' Hide CodeSense window
    Private Sub HideCodeSense()
        Try
            If pCodeSenseWindow IsNot Nothing AndAlso pCodeSenseWindow.Visible Then
                pCodeSenseWindow.Hide()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"HideCodeSense error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle CodeSense item activation
    Private Sub OnCodeSenseItemActivated(vSender As Object, vArgs As RowActivatedArgs)
        Try
            Dim lIter As TreeIter
            If pCodeSenseListStore.GetIter(lIter, vArgs.Path) Then
                Dim lItemName As String = CStr(pCodeSenseListStore.GetValue(lIter, 1))
                InsertCodeSenseItem(lItemName)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnCodeSenseItemActivated error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle CodeSense window key press
    Private Sub OnCodeSenseKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
        Try
            Select Case vArgs.Event.key
                Case Gdk.key.Escape
                    ' Hide CodeSense
                    HideCodeSense()
                    vArgs.RetVal = True
                    
                Case Gdk.key.Return, Gdk.key.Tab
                    ' Insert selected item
                    Dim lSelection As TreeSelection = pCodeSenseTreeView.Selection
                    Dim lIter As TreeIter
                    If lSelection.GetSelected(lIter) Then
                        Dim lItemName As String = CStr(pCodeSenseListStore.GetValue(lIter, 1))
                        InsertCodeSenseItem(lItemName)
                    End If
                    
                    ' Hide CodeSense
                    HideCodeSense()
                    vArgs.RetVal = True
                    
                Case Gdk.key.Up, Gdk.key.Down, Gdk.key.Page_Up, Gdk.key.Page_Down
                    ' Let TreeView handle navigation
                    vArgs.RetVal = False
                    
                Case Else
                    ' Pass other keys to editor
                    HideCodeSense()
                    vArgs.RetVal = False
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"OnCodeSenseKeyPress error: {ex.Message}")
        End Try
    End Sub
    
    ' Insert CodeSense item into editor
    Private Sub InsertCodeSenseItem(vItemName As String)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab?.Editor Is Nothing Then Return
            
            If TypeOf lCurrentTab.Editor Is IEditor Then
                Dim lEditor As IEditor = DirectCast(lCurrentTab.Editor, IEditor)
                
                ' Get current context
                Dim lContext As CodeSenseContext = GetCodeSenseContext(lEditor)
                If lContext Is Nothing Then Return
                
                ' Calculate word boundaries
                Dim lText As String = lEditor.Text
                Dim lCursorPos As Integer = GetOffsetFromPosition(lText, lContext.TriggerPosition.Line, lContext.TriggerPosition.Column)
                
                ' Find word start
                Dim lWordStart As Integer = lCursorPos
                While lWordStart > 0 AndAlso (Char.IsLetterOrDigit(lText(lWordStart - 1)) OrElse lText(lWordStart - 1) = "_"c)
                    lWordStart -= 1
                End While
                
                ' Find word end
                Dim lWordEnd As Integer = lCursorPos
                While lWordEnd < lText.Length AndAlso (Char.IsLetterOrDigit(lText(lWordEnd)) OrElse lText(lWordEnd) = "_"c)
                    lWordEnd += 1
                End While
                
                ' Convert offsets to positions
                Dim lStartPos As EditorPosition = GetPositionFromOffset(lText, lWordStart)
                Dim lEndPos As EditorPosition = GetPositionFromOffset(lText, lWordEnd)
                
                ' Select the current word
                lEditor.SetSelection(lStartPos.Line, lStartPos.Column, lEndPos.Line, lEndPos.Column)
                
                ' Insert the new text (this will replace the selection)
                lEditor.InsertText(vItemName)
                
                ' Hide CodeSense
                HideCodeSense()
                
                ' Mark as modified
                If lCurrentTab.Modified = False Then
                    lCurrentTab.Modified = True
                    UpdateTabLabel(lCurrentTab)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InsertCodeSenseItem error: {ex.Message}")
        End Try
    End Sub
    
    ' Cleanup CodeSense resources
    Private Sub CleanupCodeSense()
        Try
            If pCodeSenseTimer <> 0 Then
                GLib.Source.Remove(pCodeSenseTimer)
                pCodeSenseTimer = 0
            End If
            
            pCodeSenseWindow?.Destroy()
            pCodeSenseEngine?.Dispose()
            
        Catch ex As Exception
            Console.WriteLine($"CleanupCodeSense error: {ex.Message}")
        End Try
    End Sub
    
End Class