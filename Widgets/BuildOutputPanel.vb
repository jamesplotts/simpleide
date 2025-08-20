' Widgets/BuildOutputPanel.vb
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports Gtk
Imports SimpleIDE.Models

Namespace Widgets
    Public Class BuildOutputPanel
        Inherits Box
        
        ' Events
        Public Event CloseRequested()
        Public Event ErrorSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event WarningSelected(vFilePath As String, vLine As Integer, vColumn As Integer)
        Public Event ErrorsCopied() ' New event for Status bar update
        Public Event SendErrorsToAI(vErrorsText As String) ' New event to send Errors to AI

        
        ' Add missing events
        Public Event ErrorDoubleClicked(vError As BuildError)
        
        ' Private fields
        Private pNotebook As Notebook
        Private pCopyButton As Button ' New Copy button
        Private pSendToAIButton As Button ' New send to AI button
        
        ' Output tab
        Private pOutputScrolledWindow As ScrolledWindow
        Private pOutputTextView As TextView
        Private pOutputBuffer As TextBuffer
        
        ' Errors tab
        Private pErrorsScrolledWindow As ScrolledWindow
        Private pErrorsTreeView As TreeView
        Private pErrorsStore As ListStore
        
        ' Warnings tab
        Private pWarningsScrolledWindow As ScrolledWindow
        Private pWarningsTreeView As TreeView
        Private pWarningsStore As ListStore
        
        ' Build results
        Private pBuildResult As BuildResult
        Private pBuildErrors As New List(Of BuildError)
        Private pBuildWarnings As New List(Of BuildWarning)
        Private pProjectRoot As String = ""
        
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            
            CreateUI()
            
            ShowAll()
        End Sub

        Public ReadOnly Property ErrorListView() As TreeView
            Get
                Return pErrorsTreeView
            End Get
        End Property

        
        Private Sub CreateUI()
            ' Create header bar with copy button
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
            pNotebook = New Notebook()
            Me.PackStart(pNotebook, True, True, 0)
            
            ' Create output tab
            CreateOutputTab()
            
            ' Create errors tab
            CreateErrorsTab()
            
            ' Create warnings tab
            CreateWarningsTab()
        End Sub
        
        Private Sub CreateOutputTab()
            pOutputScrolledWindow = New ScrolledWindow()
            pOutputScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            pOutputScrolledWindow.ShadowType = Gtk.ShadowType.None
            pOutputTextView = New TextView()
            pOutputTextView.Editable = False
            pOutputTextView.WrapMode = WrapMode.Word
            pOutputTextView.Monospace = True
            
            pOutputBuffer = pOutputTextView.Buffer
            
            pOutputScrolledWindow.Add(pOutputTextView)
            
            ' Add tag for errors
            Dim lErrorTag As New TextTag("error")
            lErrorTag.Foreground = "red"
            pOutputBuffer.TagTable.Add(lErrorTag)
            
            ' Add tag for warnings
            Dim lWarningTag As New TextTag("warning")
            lWarningTag.Foreground = "orange"
            pOutputBuffer.TagTable.Add(lWarningTag)
            
            pNotebook.AppendPage(pOutputScrolledWindow, New Label("output"))
        End Sub
        
        Private Sub CreateErrorsTab()
            pErrorsScrolledWindow = New ScrolledWindow()
            pErrorsScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            ' Create list store: File, Line, Column, Code, Message
            pErrorsStore = New ListStore(GetType(String), GetType(Integer), GetType(Integer), 
                                        GetType(String), GetType(String), GetType(Object))
            
            pErrorsTreeView = New TreeView(pErrorsStore)
            pErrorsTreeView.HeadersVisible = True
            
            ' Add columns
            pErrorsTreeView.AppendColumn("File", New CellRendererText(), "Text", 0)
            pErrorsTreeView.AppendColumn("Line", New CellRendererText(), "Text", 1)
            pErrorsTreeView.AppendColumn("Col", New CellRendererText(), "Text", 2)
            pErrorsTreeView.AppendColumn("code", New CellRendererText(), "Text", 3)
            
            Dim lMessageRenderer As New CellRendererText()
            lMessageRenderer.WrapMode = Pango.WrapMode.Word
            lMessageRenderer.WrapWidth = 400
            pErrorsTreeView.AppendColumn("Message", lMessageRenderer, "Text", 4)
            
            ' Handle double-click
            AddHandler pErrorsTreeView.RowActivated, AddressOf OnErrorRowActivated
            
            pErrorsScrolledWindow.Add(pErrorsTreeView)
            pNotebook.AppendPage(pErrorsScrolledWindow, New Label("Errors (0)"))
        End Sub
        
        Private Sub CreateWarningsTab()
            pWarningsScrolledWindow = New ScrolledWindow()
            pWarningsScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            ' Create list store: File, Line, Column, Code, Message
            pWarningsStore = New ListStore(GetType(String), GetType(Integer), GetType(Integer), 
                                          GetType(String), GetType(String), GetType(Object))
            
            pWarningsTreeView = New TreeView(pWarningsStore)
            pWarningsTreeView.HeadersVisible = True
            
            ' Add columns
            pWarningsTreeView.AppendColumn("File", New CellRendererText(), "Text", 0)
            pWarningsTreeView.AppendColumn("Line", New CellRendererText(), "Text", 1)
            pWarningsTreeView.AppendColumn("Col", New CellRendererText(), "Text", 2)
            pWarningsTreeView.AppendColumn("code", New CellRendererText(), "Text", 3)
            
            Dim lMessageRenderer As New CellRendererText()
            lMessageRenderer.WrapMode = Pango.WrapMode.Word
            lMessageRenderer.WrapWidth = 400
            pWarningsTreeView.AppendColumn("Message", lMessageRenderer, "Text", 4)
            
            ' Handle double-click
            AddHandler pWarningsTreeView.RowActivated, AddressOf OnWarningRowActivated
            
            pWarningsScrolledWindow.Add(pWarningsTreeView)
            pNotebook.AppendPage(pWarningsScrolledWindow, New Label("Warnings (0)"))
        End Sub
        
        ' Public method to append output
        Public Sub AppendOutput(vText As String)
            ' FIXED: Use InsertAtCursor to avoid ambiguity with Insert overloads
            pOutputBuffer.PlaceCursor(pOutputBuffer.EndIter)
            pOutputBuffer.InsertAtCursor(vText)
            
            ' Auto-scroll to bottom
            ScrollOutputToBottom()
        End Sub

        Public Sub AppendOutputLine(vText As String)
            AppendOutput(vText & Environment.NewLine)
        End Sub

        Public Function GetErrors() As List(Of BuildError)
            Return pBuildErrors
        End Function
        
        Public Function GetWarnings() As List(Of BuildWarning)
            Return pBuildWarnings
        End Function
        
        ' Public method to append output with tag
        Public Sub AppendOutput(vText As String, vTag As String)
            Dim lIter As TextIter = pOutputBuffer.EndIter
            Dim lTag As TextTag = pOutputBuffer.TagTable.Lookup(vTag)
            
            ' FIXED: Use simpler approach to avoid ambiguity with InsertWithTags overloads
            ' First insert the text
            pOutputBuffer.PlaceCursor(lIter)
            pOutputBuffer.InsertAtCursor(vText)
            
            ' Then apply the tag
            If lTag IsNot Nothing Then
                Dim lStartIter As TextIter = pOutputBuffer.GetIterAtOffset(lIter.Offset)
                Dim lEndIter As TextIter = pOutputBuffer.EndIter
                pOutputBuffer.ApplyTag(lTag, lStartIter, lEndIter)
            End If
            
            ' Auto-scroll to bottom
            ScrollOutputToBottom()
        End Sub
        
        ' Public method to clear output
        Public Sub ClearOutput()
            pOutputBuffer.Clear()
            pBuildErrors.Clear()
            pBuildWarnings.Clear()
            pErrorsStore.Clear()
            pWarningsStore.Clear()
            UpdateTabLabels()
            UpdateCopyButtonState()
        End Sub
        
        ' Public method to show build result
        Public Sub ShowBuildResult(vResult As BuildResult, vProjectRoot As String)
            Try
                pProjectRoot = vProjectRoot
                UpdateBuildResults(vResult)
                
                ' Auto-switch to errors tab if there are errors
                If vResult.HasErrors Then
                    pNotebook.CurrentPage = 1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ShowBuildResult error: {ex.Message}")
            End Try
        End Sub
        
        ' Alias for SwitchToOutputTab
        Public Sub SwitchToOutputTab()
            ' This is called when showing build output - switch to output tab
            pNotebook.CurrentPage = 0
        End Sub
        
        ' Alias for SwitchToOutputTab
        Public Sub SwitchToBuildOutput()
            ' This is called when showing build output - switch to output tab
            pNotebook.CurrentPage = 0
        End Sub
        
        ' Public method to set project root
        Public Sub SetProjectRoot(vProjectRoot As String)
            pProjectRoot = vProjectRoot
        End Sub
        
        ' Public method to update build results
        Public Sub UpdateBuildResults(vBuildResult As BuildResult)
            Try
                pBuildResult = vBuildResult
                
                ' Parse errors and warnings using regex patterns
                ParseMSBuildOutput(pBuildResult.output)
                
                ' Update tree views
                PopulateErrorsTreeView()
                PopulateWarningsTreeView()
                
                ' Update tab labels
                UpdateTabLabels()
                UpdateCopyButtonState()
                
                ' Auto-scroll to bottom after adding content
                ScrollOutputToBottom()
                
            Catch ex As Exception
                Console.WriteLine($"error parsing build output: {ex.Message}")
            End Try
        End Sub
        
        ' Add the missing GetErrorsAsText method
        Public Function GetErrorsAsText() As String
            Try
                Return FormatErrorsForClipboard()
            Catch ex As Exception
                Console.WriteLine($"GetErrorsAsText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        Private Sub ParseMSBuildOutput(vOutput As String)
            ' Clear existing errors and warnings
            pBuildErrors.Clear()
            pBuildWarnings.Clear()
            
            ' Use HashSets to track unique errors and warnings
            Dim lUniqueErrors As New HashSet(Of String)
            Dim lUniqueWarnings As New HashSet(Of String)
            
            ' Split output into lines
            Dim lLines() As String = vOutput.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
            
            For Each lLine As String In lLines
                ' Skip empty lines
                If String.IsNullOrWhiteSpace(lLine) Then Continue For
                
                ' Parse error pattern: file(line,column): error CS1234: message
                Dim lErrorMatch As Match = Regex.Match(lLine, "^(.+?)\((\d+),(\d+)\):\s*error\s+([^:]+):\s*(.+)$", RegexOptions.IgnoreCase)
                If lErrorMatch.Success Then
                    Dim lError As New BuildError()
                    lError.FilePath = lErrorMatch.Groups(1).Value.Trim()
                    lError.Line = Integer.Parse(lErrorMatch.Groups(2).Value)
                    lError.Column = Integer.Parse(lErrorMatch.Groups(3).Value)
                    lError.ErrorCode = lErrorMatch.Groups(4).Value.Trim()
                    lError.Message = lErrorMatch.Groups(5).Value.Trim()
                    
                    ' Create unique key for this error
                    Dim lErrorKey As String = $"{lError.FilePath}|{lError.Line}|{lError.Column}|{lError.ErrorCode}|{lError.Message}"
                    
                    ' Only add if not already seen
                    If lUniqueErrors.Add(lErrorKey) Then
                        pBuildErrors.Add(lError)
                    End If
                    Continue For
                End If
                
                ' Parse warning pattern: file(line,column): warning CS1234: message
                Dim lWarningMatch As Match = Regex.Match(lLine, "^(.+?)\((\d+),(\d+)\):\s*warning\s+([^:]+):\s*(.+)$", RegexOptions.IgnoreCase)
                If lWarningMatch.Success Then
                    Dim lWarning As New BuildWarning()
                    lWarning.FilePath = lWarningMatch.Groups(1).Value.Trim()
                    lWarning.Line = Integer.Parse(lWarningMatch.Groups(2).Value)
                    lWarning.Column = Integer.Parse(lWarningMatch.Groups(3).Value)
                    lWarning.WarningCode = lWarningMatch.Groups(4).Value.Trim()
                    lWarning.Message = lWarningMatch.Groups(5).Value.Trim()
                    
                    ' Create unique key for this warning
                    Dim lWarningKey As String = $"{lWarning.FilePath}|{lWarning.Line}|{lWarning.Column}|{lWarning.WarningCode}|{lWarning.Message}"
                    
                    ' Only add if not already seen
                    If lUniqueWarnings.Add(lWarningKey) Then
                        pBuildWarnings.Add(lWarning)
                    End If
                    Continue For
                End If
                
                ' Parse alternative error pattern: file: error: message
                Dim lSimpleErrorMatch As Match = Regex.Match(lLine, "^(.+?):\s*error:\s*(.+)$", RegexOptions.IgnoreCase)
                If lSimpleErrorMatch.Success Then
                    Dim lError As New BuildError()
                    lError.FilePath = lSimpleErrorMatch.Groups(1).Value.Trim()
                    lError.Line = 1
                    lError.Column = 1
                    lError.ErrorCode = ""
                    lError.Message = lSimpleErrorMatch.Groups(2).Value.Trim()
                    
                    ' Create unique key for this error
                    Dim lErrorKey As String = $"{lError.FilePath}|{lError.Line}|{lError.Column}|{lError.ErrorCode}|{lError.Message}"
                    
                    ' Only add if not already seen
                    If lUniqueErrors.Add(lErrorKey) Then
                        pBuildErrors.Add(lError)
                    End If
                    Continue For
                End If
                
                ' Parse alternative warning pattern: file: warning: message
                Dim lSimpleWarningMatch As Match = Regex.Match(lLine, "^(.+?):\s*warning:\s*(.+)$", RegexOptions.IgnoreCase)
                If lSimpleWarningMatch.Success Then
                    Dim lWarning As New BuildWarning()
                    lWarning.FilePath = lSimpleWarningMatch.Groups(1).Value.Trim()
                    lWarning.Line = 1
                    lWarning.Column = 1
                    lWarning.WarningCode = ""
                    lWarning.Message = lSimpleWarningMatch.Groups(2).Value.Trim()
                    
                    ' Create unique key for this warning
                    Dim lWarningKey As String = $"{lWarning.FilePath}|{lWarning.Line}|{lWarning.Column}|{lWarning.WarningCode}|{lWarning.Message}"
                    
                    ' Only add if not already seen
                    If lUniqueWarnings.Add(lWarningKey) Then
                        pBuildWarnings.Add(lWarning)
                    End If
                    Continue For
                End If
            Next
            
            ' Update the counts in the BuildResult if available
            If pBuildResult IsNot Nothing Then
                pBuildResult.Errors = pBuildErrors
                pBuildResult.Warnings = pBuildWarnings
            End If
        End Sub
        
        Private Sub PopulateErrorsTreeView()
            pErrorsStore.Clear()
            
            For Each lError As BuildError In pBuildErrors
                Dim lFileName As String = System.IO.Path.GetFileName(lError.FilePath)
                pErrorsStore.AppendValues(lFileName, lError.Line, lError.Column, 
                                         lError.ErrorCode, lError.Message, lError)
            Next
        End Sub
        
        Private Sub PopulateWarningsTreeView()
            pWarningsStore.Clear()
            
            For Each lWarning As BuildWarning In pBuildWarnings
                Dim lFileName As String = System.IO.Path.GetFileName(lWarning.FilePath)
                pWarningsStore.AppendValues(lFileName, lWarning.Line, lWarning.Column, 
                                           lWarning.WarningCode, lWarning.Message, lWarning)
            Next
        End Sub
        
        Private Sub UpdateTabLabels()
            ' Update error tab label
            Dim lErrorLabel As Label = TryCast(pNotebook.GetTabLabel(pErrorsScrolledWindow), Label)
            If lErrorLabel IsNot Nothing Then
                lErrorLabel.Text = $"Errors ({pBuildErrors.Count})"
            End If
            
            ' Update warning tab label
            Dim lWarningLabel As Label = TryCast(pNotebook.GetTabLabel(pWarningsScrolledWindow), Label)
            If lWarningLabel IsNot Nothing Then
                lWarningLabel.Text = $"Warnings ({pBuildWarnings.Count})"
            End If
        End Sub
        
        Private Sub UpdateCopyButtonState()
            ' Enable copy button if there are errors or warnings
            pCopyButton.Sensitive = (pBuildErrors.Count > 0 OrElse pBuildWarnings.Count > 0)
            pSendToAIButton.Sensitive = (pBuildErrors.Count > 0 OrElse pBuildWarnings.Count > 0)
        End Sub
        
        Private Sub ScrollOutputToBottom()
            ' Schedule scroll to bottom after UI update
            GLib.Timeout.Add(50, Function()
                Dim lMark As TextMark = pOutputBuffer.CreateMark(Nothing, pOutputBuffer.EndIter, False)
                pOutputTextView.ScrollToMark(lMark, 0, False, 0, 0)
                pOutputBuffer.DeleteMark(lMark)
                Return False
            End Function)
        End Sub
        
        ' Event handlers - FIXED: Use proper delegate signature
        Private Sub OnErrorRowActivated(vSender As Object, vArgs As RowActivatedArgs)
            Try
                Dim lIter As TreeIter
                If pErrorsStore.GetIter(lIter, vArgs.Path) Then
                    Dim lError As BuildError = CType(pErrorsStore.GetValue(lIter, 5), BuildError)
                    If lError IsNot Nothing Then
                        RaiseEvent ErrorSelected(lError.FilePath, lError.Line, lError.Column)
                        RaiseEvent ErrorDoubleClicked(lError)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"error row activated error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnWarningRowActivated(vSender As Object, vArgs As RowActivatedArgs)
            Try
                Dim lIter As TreeIter
                If pWarningsStore.GetIter(lIter, vArgs.Path) Then
                    Dim lWarning As BuildWarning = CType(pWarningsStore.GetValue(lIter, 5), BuildWarning)
                    If lWarning IsNot Nothing Then
                        RaiseEvent WarningSelected(lWarning.FilePath, lWarning.Line, lWarning.Column)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"Warning row activated error: {ex.Message}")
            End Try
        End Sub

        Public Sub CopyErrorsToClipboard()
            Try
                Dim lText As String = FormatErrorsForClipboard()
                If Not String.IsNullOrEmpty(lText) Then
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", False))
                    lClipboard.Text = lText
                    RaiseEvent ErrorsCopied()
                End If
            Catch ex As Exception
                Console.WriteLine($"CopyErrorsToClipboard error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnCopyButtonClicked(sender As Object, e As EventArgs)
            CopyErrorsToClipboard()
        End Sub
        
        Private Sub OnSendToAIButtonClicked(sender As Object, e As EventArgs)
            Try
                Dim lText As String = FormatErrorsForClipboard()
                If Not String.IsNullOrEmpty(lText) Then
                    RaiseEvent SendErrorsToAI(lText)
                End If
            Catch ex As Exception
                Console.WriteLine($"Send to AI button clicked error: {ex.Message}")
            End Try
        End Sub
        
        Private Function FormatErrorsForClipboard() As String
            Try
                Dim lBuilder As New System.Text.StringBuilder()
                
                If pBuildErrors.Count > 0 Then
                    lBuilder.AppendLine($"=== Errors ({pBuildErrors.Count}) ===")
                    For Each lError As BuildError In pBuildErrors
                        lBuilder.AppendLine($"{lError.FilePath}({lError.Line},{lError.Column}): error {lError.ErrorCode}: {lError.Message}")
                    Next
                    lBuilder.AppendLine()
                End If
                
                If pBuildWarnings.Count > 0 Then
                    lBuilder.AppendLine($"=== Warnings ({pBuildWarnings.Count}) ===")
                    For Each lWarning As BuildWarning In pBuildWarnings
                        lBuilder.AppendLine($"{lWarning.FilePath}({lWarning.Line},{lWarning.Column}): warning {lWarning.WarningCode}: {lWarning.Message}")
                    Next
                End If
                
                Return lBuilder.ToString()
            Catch ex As Exception
                Console.WriteLine($"FormatErrorsForClipboard error: {ex.Message}")
                Return ""
            End Try
        End Function
        
    End Class
End Namespace
