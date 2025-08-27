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

        ''' <summary>
        ''' Gets the internal notebook widget for tab switching
        ''' </summary>
        Public ReadOnly Property Notebook As Notebook
            Get
                Return pNotebook
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
            
            ' Add columns - FIXED: Use lowercase "text" for attribute name
            pErrorsTreeView.AppendColumn("File", New CellRendererText(), "text", 0)
            pErrorsTreeView.AppendColumn("Line", New CellRendererText(), "text", 1)
            pErrorsTreeView.AppendColumn("Col", New CellRendererText(), "text", 2)
            pErrorsTreeView.AppendColumn("Code", New CellRendererText(), "text", 3)
            
            Dim lMessageRenderer As New CellRendererText()
            ' Remove wrapping to prevent excessive row height
            ' Use ellipsize instead to truncate long messages
            lMessageRenderer.Ellipsize = Pango.EllipsizeMode.End
            pErrorsTreeView.AppendColumn("Message", lMessageRenderer, "text", 4)
            
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
            
            ' Add columns - FIXED: Use lowercase "text" for attribute name
            pWarningsTreeView.AppendColumn("File", New CellRendererText(), "text", 0)
            pWarningsTreeView.AppendColumn("Line", New CellRendererText(), "text", 1)
            pWarningsTreeView.AppendColumn("Col", New CellRendererText(), "text", 2)
            pWarningsTreeView.AppendColumn("Code", New CellRendererText(), "text", 3)
            
            Dim lMessageRenderer As New CellRendererText()
            ' Remove wrapping to prevent excessive row height
            ' Use ellipsize instead to truncate long messages
            lMessageRenderer.Ellipsize = Pango.EllipsizeMode.End
            pWarningsTreeView.AppendColumn("Message", lMessageRenderer, "text", 4)
            
            ' Handle double-click
            AddHandler pWarningsTreeView.RowActivated, AddressOf OnWarningRowActivated
            
            pWarningsScrolledWindow.Add(pWarningsTreeView)
            pNotebook.AppendPage(pWarningsScrolledWindow, New Label("Warnings (0)"))
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
        
        ' Public method to append output
        Public Sub AppendOutput(vText As String)
            
           
            ' FIXED: Use InsertAtCursor to avoid ambiguity with Insert overloads
            pOutputBuffer.PlaceCursor(pOutputBuffer.EndIter)
            pOutputBuffer.InsertAtCursor(vText)
            
            ' Auto-scroll to bottom
            ScrollOutputToBottom()
        End Sub

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
                
        ' Replace: SimpleIDE.Widgets.BuildOutputPanel.ShowBuildResult
        ''' <summary>
        ''' Shows build result by populating error/warning lists WITHOUT adding to output
        ''' </summary>
        ''' <param name="vResult">The build result containing errors and warnings</param>
        ''' <param name="vProjectRoot">The project root path for relative path display</param>
        Public Sub ShowBuildResult(vResult As BuildResult, vProjectRoot As String)
            Console.Writeline($"ShowBuildResult Called!")
            Try
                pProjectRoot = vProjectRoot
                pBuildResult = vResult
                
                ' Clear everything to prevent duplication
                pBuildErrors.Clear()
                pBuildWarnings.Clear()
                pErrorsStore.Clear()
                pWarningsStore.Clear()

                
                ' Add errors from BuildResult and populate TreeView in one pass
                If vResult.Errors IsNot Nothing Then
                    Console.Writeline($"Error Count: " + vResult.Errors.Count.ToString)
                    for each lError As BuildError in vResult.Errors
                        ' Add to internal list
                        pBuildErrors.Add(lError)
                        
                        ' Add directly to TreeView store
                        Dim lFileName As String = System.IO.Path.GetFileName(lError.FilePath)
                        pErrorsStore.AppendValues(lFileName, lError.Line, lError.Column, 
                                                 lError.ErrorCode, lError.Message, lError)
                    Next
                End If
                
                ' Add warnings from BuildResult and populate TreeView in one pass
                If vResult.Warnings IsNot Nothing Then
                    for each lWarning As BuildWarning in vResult.Warnings
                        ' Add to internal list
                        pBuildWarnings.Add(lWarning)
                        
                        ' Add directly to TreeView store
                        Dim lFileName As String = System.IO.Path.GetFileName(lWarning.FilePath)
                        pWarningsStore.AppendValues(lFileName, lWarning.Line, lWarning.Column, 
                                                   lWarning.WarningCode, lWarning.Message, lWarning)
                    Next
                End If
                
                ' Update tab labels with counts
                UpdateTabLabels()
                UpdateCopyButtonState()
                
                ' Auto-switch to errors tab if there are errors
                If vResult.HasErrors Then
                    pNotebook.CurrentPage = 1
                End If
                
                ' Silent - no console output
                
            Catch ex As Exception
                ' Silent error handling - no console output
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
        
        ' Add the missing GetErrorsAsText method
        Public Function GetErrorsAsText() As String
            Try
                Return FormatErrorsForClipboard()
            Catch ex As Exception
                Console.WriteLine($"GetErrorsAsText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
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

        ''' <summary>
        ''' Copies all errors and warnings to the clipboard
        ''' </summary>
        Public Sub CopyErrorsToClipboard()
            Try
                Dim lText As String = FormatErrorsForClipboard()
                If Not String.IsNullOrEmpty(lText) Then
                    ' Use the correct method to get CLIPBOARD atom
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                    lClipboard.Text = lText
                    
                    ' Also try the alternative method for better compatibility
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
        
    End Class
End Namespace
