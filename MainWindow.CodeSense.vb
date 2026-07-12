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

    Private pCodeSenseWindow As Window
    Private pCodeSenseTreeView As TreeView
    Private pCodeSenseListStore As ListStore
    Private pCodeSenseTimer As UInteger = 0
    
    ' Initialize CodeS ense system
    Private Sub InitializeCodeSense()
        Try
            Static bolAlreadyRun As Boolean
            If Not bolAlreadyRun Then
                bolAlreadyRun = True
            Else
																RemoveHandler pProjectManager.ProjectChanged, AddressOf OnProjectChangedForCodeSense
            End If
            ' Create CodeSense engine
            pCodeSenseEngine = New CodeSenseEngine()
            
            ' Update references when project changes
            AddHandler pProjectManager.ProjectChanged, AddressOf OnProjectChangedForCodeSense
            
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
    
    ''' <summary>
    ''' Update CodeSense references from project using ProjectManager's parser
    ''' </summary>
    ''' <remarks>
    ''' This method now works with ProjectManager's centralized ProjectParser
    ''' instead of creating its own parser instance
    ''' </remarks>
    Private Sub UpdateCodeSenseReferences()
        Try
            If pCodeSenseEngine Is Nothing Then Return
            
            ' Clear existing references
            pCodeSenseEngine.ClearReferences()
            
            ' Add core references
            pCodeSenseEngine.AddReference("System")
            pCodeSenseEngine.AddReference("System.Core")
            pCodeSenseEngine.AddReference("Microsoft.VisualBasic")
            
            ' Add project references from ProjectManager
            If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
                ' Get project info from ProjectManager - using the actual property
                If pProjectManager.CurrentProjectInfo IsNot Nothing Then
                    Dim lProjectInfo = pProjectManager.CurrentProjectInfo
                    
                    ' Add assembly references
                    for each lRef in lProjectInfo.References
                        Try
                            pCodeSenseEngine.AddReference(lRef.Name)
                        Catch ex As Exception
                            Console.WriteLine($"Failed to add Reference {lRef.Name}: {ex.Message}")
                        End Try
                    Next
                    
                    ' Add package references
                    for each lRef in lProjectInfo.PackageReferences
                        Try
                            pCodeSenseEngine.AddReference(lRef.Name)
                        Catch ex As Exception
                            Console.WriteLine($"Failed to add PackageReference {lRef.Name}: {ex.Message}")
                        End Try
                    Next
                End If
                
                ' Update CodeSenseEngine with project structure from ProjectParser
                Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                If lProjectTree IsNot Nothing Then
                    pCodeSenseEngine.UpdateFromSyntaxTree(lProjectTree, True)
                    Console.WriteLine("CodeSense updated with ProjectParser structure")
                End If
            End If
            
            Console.WriteLine($"CodeSense references updated from ProjectManager")
            
        Catch ex As Exception
            Console.WriteLine($"UpdateCodeSenseReferences error: {ex.Message}")
        End Try
    End Sub
    
    
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
            
            ' Determine TriggerKind and Target
            If lContext.TriggerChar = "."c Then
                lContext.TriggerKind = CodeSenseTriggerKind.eDot
                
                ' Find the word before the dot
                Dim lWordEnd As Integer = lCursorPos - 1
                Dim lWordStart As Integer = lWordEnd
                
                ' Skip whitespace before dot if any (not strictly valid in VB but good for robustness)
                While lWordStart > 0 AndAlso Char.IsWhiteSpace(lText(lWordStart - 1))
                    lWordStart -= 1
                End While
                lWordEnd = lWordStart
                
                ' Find start of word
                While lWordStart > 0 AndAlso (Char.IsLetterOrDigit(lText(lWordStart - 1)) OrElse lText(lWordStart - 1) = "_"c)
                    lWordStart -= 1
                End While
                
                If lWordEnd > lWordStart Then
                    lContext.MemberAccessTarget = lText.Substring(lWordStart, lWordEnd - lWordStart)
                    
                    ' Handle "Me" or "MyBase" case-insensitively
                    If lContext.MemberAccessTarget.Equals("me", StringComparison.OrdinalIgnoreCase) Then
                         lContext.MemberAccessTarget = "Me"
                    ElseIf lContext.MemberAccessTarget.Equals("mybase", StringComparison.OrdinalIgnoreCase) Then
                         lContext.MemberAccessTarget = "MyBase"
                    End If
                End If
                
            ElseIf lContext.TriggerChar = "("c Then
                lContext.TriggerKind = CodeSenseTriggerKind.eOpenParen
                
            ElseIf Char.IsWhiteSpace(lContext.TriggerChar) Then
                lContext.TriggerKind = CodeSenseTriggerKind.eSpace
                
            ElseIf Char.IsLetterOrDigit(lContext.TriggerChar) Then
                lContext.TriggerKind = CodeSenseTriggerKind.eManual ' Typing a word
                
                ' Extract current word being typed
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
            Else
                 lContext.TriggerKind = CodeSenseTriggerKind.eManual
            End If
            
            ' Extract current context (Class/Method name) for "Me" support
            Dim lContextStack As New Stack(Of String)
            
            ' Traverse up syntax tree to find containing class for "Me" context
            If vEditor.RootNode IsNot Nothing Then
                Dim lCurrentNode As SyntaxNode = vEditor.RootNode
                Dim lFound As Boolean = True
                
                While lFound
                    lFound = False
                    If lCurrentNode.Children IsNot Nothing Then
                        For Each lChild As SyntaxNode In lCurrentNode.Children
                            If lChild.StartLine <= lContext.TriggerPosition.Line AndAlso lChild.EndLine >= lContext.TriggerPosition.Line Then
                                ' This child contains the cursor
                                If lChild.NodeType = CodeNodeType.eClass OrElse 
                                   lChild.NodeType = CodeNodeType.eModule OrElse 
                                   lChild.NodeType = CodeNodeType.eStructure Then
                                    lContextStack.Push(lChild.Name)
                                End If
                                lCurrentNode = lChild
                                lFound = True
                                Exit For
                            End If
                        Next
                    End If
                End While
            End If
            
            If lContextStack.Count > 0 Then
                lContext.ContainingClass = lContextStack.Peek()
                Dim lScopeItems = lContextStack.ToArray()
                Array.Reverse(lScopeItems)
                lContext.CurrentScope = String.Join(".", lScopeItems)
            End If
            
            pCodeSenseEngine.UpdateDocumentNodes(vEditor.RootNode)
            
            ' Extract Imports statements
            ' Read first 50 lines to find imports
            Dim lImportText As String = ""
            Dim lImportLines = lText.Split(New Char() {vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
            Dim lLimit As Integer = Math.Min(lImportLines.Length - 1, 50)
            
            Dim lSb As New System.Text.StringBuilder()
            For i As Integer = 0 To lLimit
                lSb.AppendLine(lImportLines(i))
            Next
            lImportText = lSb.ToString()
            
            lContext.ImportsContext = pCodeSenseEngine.ParseImports(lImportText)
            
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
        for i As Integer = 0 To Math.Min(vLine - 1, lLines.Length - 1)
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
        
        for lLine As Integer = 0 To lLines.Length - 1
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
            
            for each lSuggestion in lSuggestions
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
            pCodeSenseWindow.TransientFor = Me
            pCodeSenseWindow.AcceptFocus = False
            pCodeSenseWindow.FocusOnMap = False
            
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
            ' Get current tab editor
            Dim lTabInfo = GetCurrentTabInfo()
            If lTabInfo?.Editor Is Nothing Then 
                Console.WriteLine("CalculateCodeSensePosition: No active editor found")
                Return New Gdk.Point(0, 0)
            End If
            
            Dim lEditor As IEditor = DirectCast(lTabInfo.Editor, IEditor)
            Dim lPos As Gdk.Point = lEditor.GetCursorScreenPosition()
            
            Console.WriteLine($"CalculateCodeSensePosition: Editor returned Screen Position: {lPos.X}, {lPos.Y}")
            
            Return lPos
            
        Catch ex As Exception
            Console.WriteLine($"CalculateCodeSensePosition error: {ex.Message}")
            Return New Gdk.Point(0, 0)
        End Try
    End Function
    
    ' Hide CodeSense window
    Private Sub HideCodeSense()
        Try
            If pCodeSenseWindow IsNot Nothing AndAlso pCodeSenseWindow.Visible Then
                pCodeSenseWindow.Hide()
                
                ' Notify current editor that CodeSense is closed
                Dim lTabInfo = GetCurrentTabInfo()
                If lTabInfo IsNot Nothing AndAlso lTabInfo.Editor IsNot Nothing Then
                    If TypeOf lTabInfo.Editor Is CustomDrawingEditor Then
                        DirectCast(lTabInfo.Editor, CustomDrawingEditor).SetCodeSenseActive(False)
                    End If
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"HideCodeSense error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Manually handle a key press from the editor to control the CodeSense window
    ''' </summary>
    ''' <param name="vKey">The key that was pressed</param>
    ''' <returns>True if the key was handled by CodeSense, False otherwise</returns>
    Public Function HandleCodeSenseKeyPress(vKey As Gdk.Key) As Boolean
        Try
            If pCodeSenseWindow Is Nothing OrElse Not pCodeSenseWindow.Visible Then
                Return False
            End If
            
            Select Case vKey
                Case Gdk.Key.Escape
                    HideCodeSense()
                    Return True
                    
                Case Gdk.Key.Return, Gdk.Key.Tab, Gdk.Key.ISO_Left_Tab
                    ' Insert selected item
                    Dim lSelection As TreeSelection = pCodeSenseTreeView.Selection
                    Dim lIter As TreeIter
                    If lSelection.GetSelected(lIter) Then
                        Dim lItemName As String = CStr(pCodeSenseListStore.GetValue(lIter, 1))
                        InsertCodeSenseItem(lItemName)
                    End If
                    
                    HideCodeSense()
                    Return True
                    
                Case Gdk.Key.Up, Gdk.Key.KP_Up
                    ' Move selection up
                    Dim lSelection As TreeSelection = pCodeSenseTreeView.Selection
                    Dim lIter As TreeIter
                    If lSelection.GetSelected(lIter) Then
                        Dim lPath As TreePath = pCodeSenseListStore.GetPath(lIter)
                        If lPath.Prev() Then
                            pCodeSenseTreeView.SetCursor(lPath, pCodeSenseTreeView.Columns(0), False)
                        End If
                    End If
                    Return True
                    
                Case Gdk.Key.Down, Gdk.Key.KP_Down
                    ' Move selection down
                    Dim lSelection As TreeSelection = pCodeSenseTreeView.Selection
                    Dim lIter As TreeIter
                    If lSelection.GetSelected(lIter) Then
                        Dim lPath As TreePath = pCodeSenseListStore.GetPath(lIter)
                        lPath.Next() 
                        ' Check if next exists
                         If pCodeSenseListStore.GetIter(lIter, lPath) Then
                            pCodeSenseTreeView.SetCursor(lPath, pCodeSenseTreeView.Columns(0), False)
                        End If
                    End If
                    Return True

                Case Gdk.Key.Page_Up, Gdk.Key.KP_Page_Up
                    ' Simple page up - move 10 items up
                     Dim lSelection As TreeSelection = pCodeSenseTreeView.Selection
                    Dim lIter As TreeIter
                    If lSelection.GetSelected(lIter) Then
                        Dim lPath As TreePath = pCodeSenseListStore.GetPath(lIter)
                        ' Move up 10 times or until top
                         for i As Integer = 1 To 10
                            If Not lPath.Prev() Then Exit For
                         Next
                        pCodeSenseTreeView.SetCursor(lPath, pCodeSenseTreeView.Columns(0), False)
                    End If
                    Return True
                    
                Case Gdk.Key.Page_Down, Gdk.Key.KP_Page_Down
                    ' Simple page down - move 10 items down
                    Dim lSelection As TreeSelection = pCodeSenseTreeView.Selection
                    Dim lIter As TreeIter
                    If lSelection.GetSelected(lIter) Then
                        Dim lPath As TreePath = pCodeSenseListStore.GetPath(lIter)
                        ' Move down 10 times or until bottom
                         for i As Integer = 1 To 10
                            If Not pCodeSenseListStore.GetIter(lIter, lPath) Then Exit For
                            lPath.Next()
                         Next
                         ' Go back one if we fell off
                         If Not pCodeSenseListStore.GetIter(lIter, lPath) Then
                            lPath.Prev()
                         End If
                        pCodeSenseTreeView.SetCursor(lPath, pCodeSenseTreeView.Columns(0), False)
                    End If
                    Return True
            End Select
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"HandleCodeSenseKeyPress error: {ex.Message}")
            Return False
        End Try
    End Function

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
            If HandleCodeSenseKeyPress(vArgs.Event.Key) Then
                vArgs.RetVal = True
            Else
                 ' Pass other keys to editor? No, usually we just close if it's typing
                 ' Use a whitelist of allowed navigation keys, otherwise close?
                 ' Actually, if it's text, we might want to let it go to the editor and Update the list...
                 ' But since the window doesn't have focus, this event handler might not even fire properly 
                 ' for typing if we rely on the editor having focus.
                 ' The main interaction is handled by HandleCodeSenseKeyPress called from Editor.
                 
                 ' But if the window DOES have focus for some reason:
                HideCodeSense()
                vArgs.RetVal = False
            End If
            
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
                
                ' Find word start - CAUTIOUS: We need to respect the TriggerKind.
                ' If it was a Dot trigger, we are inserting AFTER the dot.
                ' If it was Manual trigger on a word, we replace the word.
                
                Dim lWordStart As Integer = lCursorPos
                Dim lWordEnd As Integer = lCursorPos
                
                 If lContext.TriggerKind = CodeSenseTriggerKind.eDot Then
                    ' Starting a new member after dot - existing text is just what we typed so far
                     While lWordStart > 0 AndAlso (Char.IsLetterOrDigit(lText(lWordStart - 1)) OrElse lText(lWordStart - 1) = "_"c)
                        lWordStart -= 1
                    End While
                    ' Ensure we don't go past the dot
                    ' (This logic is a bit simplistic, assumes we haven't moved cursor away)
                 Else
                    ' Standard word replacement
                    While lWordStart > 0 AndAlso (Char.IsLetterOrDigit(lText(lWordStart - 1)) OrElse lText(lWordStart - 1) = "_"c)
                        lWordStart -= 1
                    End While
                 End If

                
                ' Find word end
                While lWordEnd < lText.Length AndAlso (Char.IsLetterOrDigit(lText(lWordEnd)) OrElse lText(lWordEnd) = "_"c)
                    lWordEnd += 1
                End While
                
                ' Convert offsets to positions
                Dim lStartPos As EditorPosition = GetPositionFromOffset(lText, lWordStart)
                Dim lEndPos As EditorPosition = GetPositionFromOffset(lText, lWordEnd)
                
                ' Select the current word
                lEditor.SetSelection(New EditorPosition(lStartPos.Line, lStartPos.Column), New EditorPosition(lEndPos.Line, lEndPos.Column))
                
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

    ''' <summary>
    ''' Handle CodeSense request from editor using ProjectParser data
    ''' </summary>
    ''' <remarks>
    ''' Now uses parse results from ProjectManager's centralized ProjectParser
    ''' instead of triggering local parsing
    ''' </remarks>
    Private Sub OnCodeSenseRequested(vSender As Object, vContext As CodeSenseContext)
        Try
            If pCodeSenseEngine Is Nothing OrElse vContext Is Nothing Then Return
            
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return
            
            ' Find the TabInfo for this editor
            Dim lTabInfo As TabInfo = Nothing
            for each lTabEntry in pOpenTabs
                If lTabEntry.Value.Editor Is lEditor Then
                    lTabInfo = lTabEntry.Value
                    Exit for
                End If
            Next
            
            If lTabInfo IsNot Nothing Then
                ' Get SourceFileInfo from ProjectManager
                Dim lSourceFileInfo As SourceFileInfo = Nothing
                If pProjectManager IsNot Nothing Then
                    lSourceFileInfo = pProjectManager.GetSourceFileInfo(lTabInfo.FilePath)
                End If
                
                ' Ensure we have the latest parse from ProjectParser
                If lSourceFileInfo IsNot Nothing Then
                    Dim lSyntaxTree As SyntaxNode = lSourceFileInfo.SyntaxTree
                    If lSyntaxTree Is Nothing AndAlso pProjectManager IsNot Nothing Then
                        ' Request parse through ProjectManager if needed
                        pProjectManager.ParseFile(lSourceFileInfo)
                        lSyntaxTree = lSourceFileInfo.SyntaxTree
                    End If
                    
                    ' Update CodeSense with the parsed structure
                    If lSyntaxTree IsNot Nothing Then
                        pCodeSenseEngine.UpdateDocumentNodes(lSyntaxTree)
                    End If
                End If
            End If
            
            ' Get suggestions from CodeSenseEngine
            Dim lSuggestions As List(Of CodeSenseSuggestion) = pCodeSenseEngine.GetSuggestions(vContext)
            
            ' Convert to CompletionItems for CodeSenseContext
            vContext.SuggestedCompletions = New List(Of CompletionItem)()
            for each lSuggestion in lSuggestions
                Dim lItem As New CompletionItem()
                lItem.Text = lSuggestion.Text
                lItem.DisplayText = lSuggestion.DisplayText
                lItem.Description = lSuggestion.Description
                lItem.Icon = lSuggestion.Icon
                vContext.SuggestedCompletions.Add(lItem)
            Next
            
            ' Show CodeSense window if we have suggestions
            If lSuggestions.Count > 0 Then
                ShowCodeSenseWindow(lEditor, vContext)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnCodeSenseRequested error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Shows the CodeSense window for an editor
    ''' </summary>
    Private Sub ShowCodeSenseWindow(vEditor As IEditor, vContext As CodeSenseContext)
        Try
            ' Use existing ShowCodeSense method or create new implementation
            If pCodeSenseWindow Is Nothing Then
                CreateCodeSenseWindow()
            End If
            
            ' Update CodeSense list
            UpdateCodeSenseList(vContext.SuggestedCompletions)
            
            ' Position and show window
            If TypeOf vEditor Is Widget Then
                Dim lWidget As Widget = DirectCast(vEditor, Widget)
                ' Position relative to cursor position
                pCodeSenseWindow.ShowAll()
                
                ' Notify editor that CodeSense is active (for Backspace handling)
                If TypeOf vEditor Is CustomDrawingEditor Then
                    DirectCast(vEditor, CustomDrawingEditor).SetCodeSenseActive(True)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ShowCodeSenseWindow error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handle parse completion from ProjectManager for CodeSense
    ''' </summary>
    Private Sub OnProjectParseCompletedForCodeSense(vFile As SourceFileInfo, vResult As SyntaxNode)
        Try
            ' Update CodeSense with the latest parse results
            If pCodeSenseEngine IsNot Nothing AndAlso vResult IsNot Nothing Then
                ' If this is the current file, update CodeSense immediately
                Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
                
                ' Check if this file matches current tab
                If lCurrentTab IsNot Nothing AndAlso lCurrentTab.FilePath = vFile.FilePath Then
                    pCodeSenseEngine.UpdateDocumentNodes(vResult)
                    Console.WriteLine($"CodeSense updated with parse results for {vFile.FileName}")
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectParseCompletedForCodeSense error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handle project structure load from ProjectManager for CodeSense
    ''' </summary>
    Private Sub OnProjectStructureLoadedForCodeSense(vRootNode As SyntaxNode)
        Try
            If pCodeSenseEngine IsNot Nothing AndAlso vRootNode IsNot Nothing Then
                ' Update CodeSense with the complete project structure
                pCodeSenseEngine.UpdateFromSyntaxTree(vRootNode, True)
                Console.WriteLine($"CodeSense updated with project structure from ProjectParser")
                
                ' Update references as well
                UpdateCodeSenseReferences()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectStructureLoadedForCodeSense error: {ex.Message}")
        End Try
    End Sub


    ''' <summary>
    ''' Initialize CodeSense to work with ProjectManager's centralized parser
    ''' </summary>
    ''' <remarks>
    ''' Sets up CodeSense to consume parse results from ProjectManager.Parser
    ''' instead of performing its own parsing
    ''' </remarks>
    Private Sub InitializeCodeSenseWithProjectManager()
        Try
            If pCodeSenseEngine Is Nothing Then
                pCodeSenseEngine = New CodeSenseEngine()
            End If
            
            If pProjectManager IsNot Nothing Then
                ' Subscribe to ProjectManager parse events
                RemoveHandler pProjectManager.ParseCompleted, AddressOf OnProjectParseCompletedForCodeSense
                AddHandler pProjectManager.ParseCompleted, AddressOf OnProjectParseCompletedForCodeSense
                
                RemoveHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoadedForCodeSense
                AddHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoadedForCodeSense
                
                Console.WriteLine("CodeSense subscribed to ProjectManager parse events")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializeCodeSenseWithProjectManager error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Updates the CodeSense list with completion items
    ''' </summary>
    Private Sub UpdateCodeSenseList(vCompletions As List(Of CompletionItem))
        Try
            If pCodeSenseListStore Is Nothing Then Return
            
            pCodeSenseListStore.Clear()
            
            for each lItem in vCompletions
                Dim lIter As TreeIter = pCodeSenseListStore.Append()
                pCodeSenseListStore.SetValue(lIter, 0, lItem.Icon)
                pCodeSenseListStore.SetValue(lIter, 1, lItem.DisplayText)
                pCodeSenseListStore.SetValue(lIter, 2, lItem.Description)
            Next
            
        Catch ex As Exception
            Console.WriteLine($"UpdateCodeSenseList error: {ex.Message}")
        End Try
    End Sub
    
End Class