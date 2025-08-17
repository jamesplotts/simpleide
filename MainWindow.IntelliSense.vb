' MainWindow.IntelliSense.vb - Complete fixed implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Editors
Imports SimpleIDE.Syntax

Partial Public Class MainWindow
    
    ' IntelliSense components
    'Private pIntelliSenseEngine As IntelliSenseEngine
    Private pIntelliSenseWindow As Window
    Private pIntelliSenseTreeView As TreeView
    Private pIntelliSenseListStore As ListStore
    Private pIntelliSenseTimer As UInteger = 0
    
    ' Initialize IntelliSense system
    Private Sub InitializeIntelliSense()
        Try
            ' Create IntelliSense engine
            pIntelliSenseEngine = New IntelliSenseEngine()
            
            ' Update references when project changes
            AddHandler Me.ProjectChanged, AddressOf OnProjectChangedForIntelliSense
            
        Catch ex As Exception
            Console.WriteLine($"InitializeIntelliSense error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle project change for IntelliSense
    Private Sub OnProjectChangedForIntelliSense(vProjectFile As String)
        Try
            HideBottomPanel()
            UpdateIntelliSenseReferences()
        Catch ex As Exception
            Console.WriteLine($"OnProjectChangedForIntelliSense error: {ex.Message}")
        End Try
    End Sub
    
    ' Update IntelliSense references from project
    Private Sub UpdateIntelliSenseReferences()
        Try
            If pIntelliSenseEngine Is Nothing Then Return
            
            ' Clear existing references
            pIntelliSenseEngine.ClearReferences()
            
            ' Add core references
            pIntelliSenseEngine.AddReference("System")
            pIntelliSenseEngine.AddReference("System.Core")
            pIntelliSenseEngine.AddReference("Microsoft.VisualBasic")
            
            ' Add project references
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                ' Parse project file to get references
                Dim lProjectInfo As ProjectFileParser.ProjectInfo = ProjectFileParser.ParseProjectFile(pCurrentProject)
                
                ' Add assembly references
                For Each lRef In lProjectInfo.References
                    Try
                        pIntelliSenseEngine.AddReference(lRef.Name)
                    Catch ex As Exception
                        Console.WriteLine($"Failed to add Reference {lRef.Name}: {ex.Message}")
                    End Try
                Next
                
                ' Add package references
                For Each lPackage In lProjectInfo.PackageReferences
                    Try
                        pIntelliSenseEngine.AddReference(lPackage.Name)
                    Catch ex As Exception
                        Console.WriteLine($"Failed to add Package Reference {lPackage.Name}: {ex.Message}")
                    End Try
                Next
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateIntelliSenseReferences error: {ex.Message}")
        End Try
    End Sub
    
'    ' Handle text changed in editor for IntelliSense
'    Public Sub OnEditorTextChanged(vSender As Object, vArgs As EventArgs)
'        OnEditorTextChangedEnhanced(vSender, vArgs)
'    End Sub
    
    ' Get IntelliSense context from editor
    Private Function GetIntelliSenseContext(vEditor As IEditor) As IntelliSenseContext
        Try
            Dim lContext As New IntelliSenseContext()
            
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
                pIntelliSenseEngine.UpdateDocumentNodes(lCustomEditor.GetDocumentNodes(), lCustomEditor.GetRootNodes())
            End If
            
            Return lContext
            
        Catch ex As Exception
            Console.WriteLine($"GetIntelliSenseContext error: {ex.Message}")
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
    
    ' Show IntelliSense for given context
    Private Sub ShowIntelliSenseForContext(vEditor As IEditor, vContext As IntelliSenseContext)
        Try
            ' Get suggestions
            Dim lSuggestions As List(Of IntelliSenseSuggestion) = pIntelliSenseEngine.GetSuggestions(vContext)
            
            If lSuggestions Is Nothing OrElse lSuggestions.Count = 0 Then
                HideIntelliSense()
                Return
            End If
            
            ' Create or update IntelliSense window
            If pIntelliSenseWindow Is Nothing Then
                CreateIntelliSenseWindow()
            End If
            
            ' Update list store
            pIntelliSenseListStore.Clear()
            
            For Each lSuggestion In lSuggestions
                pIntelliSenseListStore.AppendValues(
                    lSuggestion.Icon,
                    lSuggestion.Text,           ' Use Text instead of Name
                    lSuggestion.Description     ' Use Description instead of TypeName
                )
            Next
            
            ' Position and show window
            Dim lPosition As Gdk.Point = CalculateIntelliSensePosition(vContext)
            pIntelliSenseWindow.Move(lPosition.x, lPosition.y)
            pIntelliSenseWindow.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"ShowIntelliSenseForContext error: {ex.Message}")
        End Try
    End Sub
    
    ' Create IntelliSense window
    Private Sub CreateIntelliSenseWindow()
        Try
            ' Create window
            pIntelliSenseWindow = New Window(WindowType.Popup)
            pIntelliSenseWindow.TypeHint = Gdk.WindowTypeHint.PopupMenu
            pIntelliSenseWindow.Decorated = False
            pIntelliSenseWindow.SkipTaskbarHint = True
            pIntelliSenseWindow.SkipPagerHint = True
            
            ' Create scrolled window
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.SetSizeRequest(300, 200)
            
            ' Create list store and tree view
            pIntelliSenseListStore = New ListStore(GetType(String), GetType(String), GetType(String))
            pIntelliSenseTreeView = New TreeView(pIntelliSenseListStore)
            pIntelliSenseTreeView.HeadersVisible = False
            
            ' Add columns
            Dim lIconColumn As New TreeViewColumn()
            Dim lIconRenderer As New CellRendererText()
            lIconColumn.PackStart(lIconRenderer, False)
            lIconColumn.AddAttribute(lIconRenderer, "text", 0)
            pIntelliSenseTreeView.AppendColumn(lIconColumn)
            
            Dim lTextColumn As New TreeViewColumn()
            Dim lTextRenderer As New CellRendererText()
            lTextColumn.PackStart(lTextRenderer, True)
            lTextColumn.AddAttribute(lTextRenderer, "text", 1)
            pIntelliSenseTreeView.AppendColumn(lTextColumn)
            
            ' Handle selection
            AddHandler pIntelliSenseTreeView.RowActivated, AddressOf OnIntelliSenseItemActivated
            
            ' Handle key press
            AddHandler pIntelliSenseWindow.KeyPressEvent, AddressOf OnIntelliSenseKeyPress
            
            ' Add to window
            lScrolled.Add(pIntelliSenseTreeView)
            pIntelliSenseWindow.Add(lScrolled)
            
        Catch ex As Exception
            Console.WriteLine($"CreateIntelliSenseWindow error: {ex.Message}")
        End Try
    End Sub
    
    ' Calculate IntelliSense window position
    Private Function CalculateIntelliSensePosition(vContext As IntelliSenseContext) As Gdk.Point
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
            Console.WriteLine($"CalculateIntelliSensePosition error: {ex.Message}")
            Return New Gdk.Point(100, 100) ' Default position
        End Try
    End Function
    
    ' Hide IntelliSense window
    Private Sub HideIntelliSense()
        Try
            If pIntelliSenseWindow IsNot Nothing AndAlso pIntelliSenseWindow.Visible Then
                pIntelliSenseWindow.Hide()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"HideIntelliSense error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle IntelliSense item activation
    Private Sub OnIntelliSenseItemActivated(vSender As Object, vArgs As RowActivatedArgs)
        Try
            Dim lIter As TreeIter
            If pIntelliSenseListStore.GetIter(lIter, vArgs.Path) Then
                Dim lItemName As String = CStr(pIntelliSenseListStore.GetValue(lIter, 1))
                InsertIntelliSenseItem(lItemName)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnIntelliSenseItemActivated error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle IntelliSense window key press
    Private Sub OnIntelliSenseKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
        Try
            Select Case vArgs.Event.key
                Case Gdk.key.Escape
                    ' Hide IntelliSense
                    HideIntelliSense()
                    vArgs.RetVal = True
                    
                Case Gdk.key.Return, Gdk.key.Tab
                    ' Insert selected item
                    Dim lSelection As TreeSelection = pIntelliSenseTreeView.Selection
                    Dim lIter As TreeIter
                    If lSelection.GetSelected(lIter) Then
                        Dim lItemName As String = CStr(pIntelliSenseListStore.GetValue(lIter, 1))
                        InsertIntelliSenseItem(lItemName)
                    End If
                    
                    ' Hide IntelliSense
                    HideIntelliSense()
                    vArgs.RetVal = True
                    
                Case Gdk.key.Up, Gdk.key.Down, Gdk.key.Page_Up, Gdk.key.Page_Down
                    ' Let TreeView handle navigation
                    vArgs.RetVal = False
                    
                Case Else
                    ' Pass other keys to editor
                    HideIntelliSense()
                    vArgs.RetVal = False
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"OnIntelliSenseKeyPress error: {ex.Message}")
        End Try
    End Sub
    
    ' Insert IntelliSense item into editor
    Private Sub InsertIntelliSenseItem(vItemName As String)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab?.Editor Is Nothing Then Return
            
            If TypeOf lCurrentTab.Editor Is IEditor Then
                Dim lEditor As IEditor = DirectCast(lCurrentTab.Editor, IEditor)
                
                ' Get current context
                Dim lContext As IntelliSenseContext = GetIntelliSenseContext(lEditor)
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
                
                ' Hide IntelliSense
                HideIntelliSense()
                
                ' Mark as modified
                If lCurrentTab.Modified = False Then
                    lCurrentTab.Modified = True
                    UpdateTabLabel(lCurrentTab)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InsertIntelliSenseItem error: {ex.Message}")
        End Try
    End Sub
    
    ' Cleanup IntelliSense resources
    Private Sub CleanupIntelliSense()
        Try
            If pIntelliSenseTimer <> 0 Then
                GLib.Source.Remove(pIntelliSenseTimer)
                pIntelliSenseTimer = 0
            End If
            
            pIntelliSenseWindow?.Destroy()
            pIntelliSenseEngine?.Dispose()
            
        Catch ex As Exception
            Console.WriteLine($"CleanupIntelliSense error: {ex.Message}")
        End Try
    End Sub
    
End Class