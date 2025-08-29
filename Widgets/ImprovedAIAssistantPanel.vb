' ImprovedAIAssistantPanel.vb
' Created: 2025-08-05 17:03:15
' Widgets/ImprovedAIAssistantPanel.vb - Enhanced AI Assistant with artifact support
Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks
Imports System.Text
Imports System.Net.Http
Imports System.Text.Json
Imports SimpleIDE.AI
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Widgets
Imports SimpleIDE.Syntax

Namespace Widgets
    
    ''' <summary>
    ''' Enhanced AI Assistant panel that integrates with the artifact system
    ''' </summary>
    Public Class ImprovedAIAssistantPanel
        Inherits Box
        
        ' ===== Private Fields =====
        Private pMainPaned As Paned
        Private pChatScroll As ScrolledWindow
        Private pChatView As TextView
        Private pChatBuffer As TextBuffer
        Private pPromptBox As Box
        Private pPromptEntry As TextView
        Private pPromptScroll As ScrolledWindow
        Private pSendButton As Button
        Private pStatusLabel As Label
        Private pContextLabel As Label
        
        ' Quick action toolbar
        Private pActionToolbar As Toolbar
        Private pContextButton As ToolButton
        Private pCurrentFileButton As ToggleToolButton
        Private pProjectFilesButton As ToggleToolButton
        Private pBuildErrorsButton As ToggleToolButton
        Private pClearButton As ToolButton
        
        ' State
        Private pApiClient As EnhancedClaudeApiClient
        Private pMainWindow As MainWindow
        Private pCurrentEditor As IEditor
        Private pProjectRoot As String
        Private pIsProcessing As Boolean = False
        Private pConversationHistory As New List(Of ChatMessage)
        Private pIncludeCurrentFile As Boolean = True
        Private pIncludeProjectFiles As Boolean = False
        Private pIncludeBuildErrors As Boolean = False
        Private pBuildErrors As List(Of BuildError)
        
        ' ===== Events =====
        Public Event StatusUpdate(vMessage As String)
        Public Event ArtifactCreated(vArtifactId As String, vFilePath As String)
        
        ' ===== Classes =====
        Public Class ChatMessage

            Public Sub New(vRole As String, vContent As String)
                Role = vRole
                Content = vContent
                Timestamp = DateTime.Now()
                Artifacts = New List(Of ArtifactInfo)
                Actions = New List(Of AIAssistantPanel.AIAction)
            End Sub

            Public Property Role As String
            Public Property Content As String
            Public Property Timestamp As DateTime
            Public Property Artifacts As List(Of ArtifactInfo)
            Public Property Actions As List(Of AIAssistantPanel.AIAction)
        End Class
        
        Public Class ArtifactInfo
            Public Property Id As String
            Public Property Type As String
            Public Property Title As String
            Public Property FilePath As String
            Public Property Content As String
        End Class
        
        ' ===== Constructor =====
        Public Sub New(vApiKey As String, vMainWindow As MainWindow)
            MyBase.New(Orientation.Vertical, 0)
            
            pApiClient = New EnhancedClaudeApiClient(vApiKey)
            pMainWindow = vMainWindow
            pBuildErrors = New List(Of BuildError)()
            
            BuildUI()
            ApplyStyling()
            
            ' Add welcome message
            AddMessage("assistant", "Hello! I'm your AI coding assistant. I can help you write code, fix Errors, explain concepts, and more. 

I'll create Artifacts for any code I generate, which you can:
• Review and edit before applying
• Compare with your existing code
• Accept to update your files

What would you like to work on?")
        End Sub
        
        ' ===== UI Construction =====
        Private Sub BuildUI()
            Try
                ' Create main paned
                pMainPaned = New Paned(Orientation.Vertical)
                'pMainPaned.Position = 300
                
                ' Top: Chat area
                CreateChatArea()
                pMainPaned.Pack1(pChatScroll, True, True)
                
                ' Bottom: Input area
                Dim lInputContainer As New Box(Orientation.Vertical, 0)
                CreateActionToolbar()
                CreatePromptArea()
                CreateStatusBar()
                
                lInputContainer.PackStart(pActionToolbar, False, False, 0)
                lInputContainer.PackStart(pPromptBox, True, True, 0)
                lInputContainer.PackStart(pStatusLabel, False, False, 0)
                
                pMainPaned.Pack2(lInputContainer, False, True)
                
                ' Add to main box
                PackStart(pMainPaned, True, True, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateChatArea()
            Try
                pChatScroll = New ScrolledWindow()
                pChatScroll.SetPolicy(PolicyType.Automatic, PolicyType.Always)
                
                pChatView = New TextView()
                pChatView.Editable = False
                pChatView.WrapMode = WrapMode.Word
                pChatView.LeftMargin = 10
                pChatView.RightMargin = 10
                pChatView.TopMargin = 10
                pChatView.BottomMargin = 10
                
                pChatBuffer = pChatView.Buffer
                CreateTextTags()
                
                pChatScroll.Add(pChatView)
                
            Catch ex As Exception
                Console.WriteLine($"CreateChatArea error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateActionToolbar()
            Try
                pActionToolbar = New Toolbar()
                pActionToolbar.ToolbarStyle = ToolbarStyle.Icons
                
                ' Context info button (shows what's included)
                pContextButton = New ToolButton(Nothing, "Info")
                pContextButton.TooltipText = "Show Context information"
                AddHandler pContextButton.Clicked, AddressOf OnContextButtonClicked
                pActionToolbar.Insert(pContextButton, -1)
                
                pActionToolbar.Insert(New SeparatorToolItem(), -1)
                
                ' Toggle buttons for context
                pCurrentFileButton = New ToggleToolButton()
                pCurrentFileButton.IconName = "Text-x-generic"
                pCurrentFileButton.Label = "current File"
                pCurrentFileButton.TooltipText = "Include current file in Context"
                pCurrentFileButton.Active = pIncludeCurrentFile
                AddHandler pCurrentFileButton.Toggled, AddressOf OnCurrentFileToggled
                pActionToolbar.Insert(pCurrentFileButton, -1)
                
                pProjectFilesButton = New ToggleToolButton()
                pProjectFilesButton.IconName = "folder"
                pProjectFilesButton.Label = "Project"
                pProjectFilesButton.TooltipText = "Include project structure in Context"
                pProjectFilesButton.Active = pIncludeProjectFiles
                AddHandler pProjectFilesButton.Toggled, AddressOf OnProjectFilesToggled
                pActionToolbar.Insert(pProjectFilesButton, -1)
                
                pBuildErrorsButton = New ToggleToolButton()
                pBuildErrorsButton.IconName = "dialog-error"
                pBuildErrorsButton.Label = "Errors"
                pBuildErrorsButton.TooltipText = "Include build Errors in Context"
                pBuildErrorsButton.Active = pIncludeBuildErrors
                pBuildErrorsButton.Sensitive = False
                AddHandler pBuildErrorsButton.Toggled, AddressOf OnBuildErrorsToggled
                pActionToolbar.Insert(pBuildErrorsButton, -1)
                
                ' Expanding separator
                Dim lSeparator As New SeparatorToolItem()
                lSeparator.Draw = False
                lSeparator.Expand = True
                pActionToolbar.Insert(lSeparator, -1)
                
                ' Clear button
                pClearButton = New ToolButton(Nothing, "Clear")
                pClearButton.TooltipText = "Clear conversation"
                AddHandler pClearButton.Clicked, AddressOf OnClearClicked
                pActionToolbar.Insert(pClearButton, -1)
                
            Catch ex As Exception
                Console.WriteLine($"CreateActionToolbar error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreatePromptArea()
            Try
                pPromptBox = New Box(Orientation.Horizontal, 5)
                pPromptBox.BorderWidth = 5
                
                ' Create scrolled window for prompt
                pPromptScroll = New ScrolledWindow()
                pPromptScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                pPromptScroll.HeightRequest = 60
                pPromptScroll.ShadowType = ShadowType.in
                
                pPromptEntry = New TextView()
                pPromptEntry.WrapMode = WrapMode.Word
                pPromptEntry.AcceptsTab = False
                AddHandler pPromptEntry.KeyPressEvent, AddressOf OnPromptKeyPress
                
                pPromptScroll.Add(pPromptEntry)
                pPromptBox.PackStart(pPromptScroll, True, True, 0)
                
                ' Send button
                pSendButton = New Button("Send")
                pSendButton.HeightRequest = 60
                Dim lSendImage As New Image()
                lSendImage.SetFromIconName("mail-send", IconSize.Button)
                pSendButton.Image = lSendImage
                AddHandler pSendButton.Clicked, AddressOf OnSendClicked
                pPromptBox.PackEnd(pSendButton, False, False, 0)
                
            Catch ex As Exception
                Console.WriteLine($"CreatePromptArea error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateStatusBar()
            Try
                pStatusLabel = New Label("Ready")
                pStatusLabel.Halign = Align.Start
                pStatusLabel.MarginStart = 5
                pStatusLabel.MarginEnd = 5
                pStatusLabel.MarginTop = 2
                pStatusLabel.MarginBottom = 2
                
            Catch ex As Exception
                Console.WriteLine($"CreateStatusBar error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateTextTags()
            Try
                ' User message
                Dim lUserTag As New TextTag("user")
                lUserTag.Weight = Pango.Weight.Bold
                lUserTag.Foreground = "#0066CC"
                lUserTag.ParagraphBackground = "#F0F8FF"
                lUserTag.LeftMargin = 5
                lUserTag.RightMargin = 5
                lUserTag.PixelsAboveLines = 5
                lUserTag.PixelsBelowLines = 5
                pChatBuffer.TagTable.Add(lUserTag)
                
                ' Assistant message
                Dim lAssistantTag As New TextTag("assistant")
                lAssistantTag.Foreground = "#006600"
                lAssistantTag.LeftMargin = 5
                lAssistantTag.RightMargin = 5
                lAssistantTag.PixelsAboveLines = 5
                lAssistantTag.PixelsBelowLines = 5
                pChatBuffer.TagTable.Add(lAssistantTag)
                
                ' Code block
                Dim lCodeTag As New TextTag("code")
                lCodeTag.Family = "Monospace"
                lCodeTag.Background = "#F5F5F5"
                lCodeTag.Foreground = "#333333"
                lCodeTag.LeftMargin = 20
                lCodeTag.RightMargin = 20
                pChatBuffer.TagTable.Add(lCodeTag)
                
                ' Artifact link
                Dim lArtifactTag As New TextTag("artifact")
                lArtifactTag.Foreground = "#0000FF"
                lArtifactTag.Underline = Pango.Underline.Single
                lArtifactTag.Weight = Pango.Weight.Bold
                pChatBuffer.TagTable.Add(lArtifactTag)
                
                ' Error
                Dim lErrorTag As New TextTag("error")
                lErrorTag.Foreground = "#CC0000"
                lErrorTag.Weight = Pango.Weight.Bold
                pChatBuffer.TagTable.Add(lErrorTag)
                
                ' Context info
                Dim lContextTag As New TextTag("Context")
                lContextTag.Foreground = "#666666"
                lContextTag.Style = Pango.Style.Italic
                lContextTag.Scale = 0.9
                pChatBuffer.TagTable.Add(lContextTag)
                
            Catch ex As Exception
                Console.WriteLine($"CreateTextTags error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        Public Sub SetCurrentEditor(vEditor As IEditor)
            pCurrentEditor = vEditor
            UpdateContextInfo()
        End Sub
        
        Public Sub SetProjectRoot(vProjectRoot As String)
            pProjectRoot = vProjectRoot
            UpdateContextInfo()
        End Sub
        
        Public Sub SetBuildErrors(vErrors As List(Of BuildError))
            pBuildErrors = If(vErrors, New List(Of BuildError))
            pBuildErrorsButton.Sensitive = pBuildErrors.Count > 0
            If pBuildErrors.Count = 0 Then
                pBuildErrorsButton.Active = False
                pIncludeBuildErrors = False
            End If
            UpdateContextInfo()
        End Sub
        
        Public Async Function SendMessage(vMessage As String) As Task
            If pIsProcessing OrElse String.IsNullOrWhiteSpace(vMessage) Then Return
            
            Try
                pIsProcessing = True
                UpdateUI()
                
                ' Add user message
                AddMessage("user", vMessage)
                pPromptEntry.Buffer.Text = ""
                
                ' Build context
                Dim lContext As String = BuildContext()
                Dim lFullPrompt As String = If(String.IsNullOrEmpty(lContext), vMessage, lContext & Environment.NewLine & Environment.NewLine & vMessage)
                
                ' Send to API
                UpdateStatus("Thinking...")
                Dim lResponse As EnhancedClaudeApiClient.ClaudeResponse = Await pApiClient.SendMessageWithArtifactsAsync(lFullPrompt, pConversationHistory)
                
                ' Process response
                ProcessAIResponse(lResponse)
                
            Catch ex As Exception
                AddMessage("error", $"error: {ex.Message}")
            Finally
                pIsProcessing = False
                UpdateUI()
                UpdateStatus("Ready")
            End Try
        End Function
        
        ' ===== Private Helper Methods =====
        
        Private Sub ProcessAIResponse(vResponse As EnhancedClaudeApiClient.ClaudeResponse)
            Try
                ' Add assistant message
                AddMessage("assistant", vResponse.Content)
                
                ' Process artifacts if any
                If vResponse.Artifacts IsNot Nothing AndAlso vResponse.Artifacts.Count > 0 Then
                    for each lArtifact in vResponse.Artifacts
                        CreateArtifact(lArtifact)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProcessAIResponse error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateArtifact(vArtifact As EnhancedClaudeApiClient.ClaudeArtifact)
            Try
                ' Determine target file path based on context
                Dim lTargetPath As String = ""
                If pCurrentEditor IsNot Nothing AndAlso pIncludeCurrentFile Then
                    lTargetPath = pCurrentEditor.FilePath
                End If
                
                ' Show artifact in main window
                pMainWindow.ShowAIArtifact(vArtifact.Id, vArtifact.Type, 
                                          vArtifact.Title, vArtifact.Content, lTargetPath)
                
                ' Add artifact link to chat
                AddArtifactLink(vArtifact)
                
                ' Raise event
                RaiseEvent ArtifactCreated(vArtifact.Id, lTargetPath)
                
            Catch ex As Exception
                Console.WriteLine($"CreateArtifact error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AddArtifactLink(vArtifact As EnhancedClaudeApiClient.ClaudeArtifact)
            Try
                Dim lIter As TextIter = pChatBuffer.EndIter
                pChatBuffer.InsertAtCursor(Environment.NewLine & "📄 ")
                
                ' Create clickable artifact link
                Dim lStartMark As TextMark = pChatBuffer.CreateMark(Nothing, pChatBuffer.EndIter, True)
                pChatBuffer.InsertAtCursor($"[Artifact: {vArtifact.Title}]")
                Dim lEndMark As TextMark = pChatBuffer.CreateMark(Nothing, pChatBuffer.EndIter, True)
                
                Dim lStartIter As TextIter = pChatBuffer.GetIterAtMark(lStartMark)
                Dim lEndIter As TextIter = pChatBuffer.GetIterAtMark(lEndMark)
                pChatBuffer.ApplyTag("artifact", lStartIter, lEndIter)
                
                pChatBuffer.InsertAtCursor(Environment.NewLine)
                
                ' Scroll to bottom
                pChatView.ScrollToMark(pChatBuffer.InsertMark, 0.0, False, 0.0, 0.0)
                
            Catch ex As Exception
                Console.WriteLine($"AddArtifactLink error: {ex.Message}")
            End Try
        End Sub
        
        Private Function BuildContext() As String
            Try
                Dim lContext As New StringBuilder()
                Dim lHasContext As Boolean = False
                
                ' Current file context
                If pIncludeCurrentFile AndAlso pCurrentEditor IsNot Nothing Then
                    lContext.AppendLine("=== current FILE ===")
                    lContext.AppendLine($"File: {pCurrentEditor.FilePath}")
                    lContext.AppendLine("Content:")
                    lContext.AppendLine("```vb")
                    lContext.AppendLine(pCurrentEditor.Text)
                    lContext.AppendLine("```")
                    lContext.AppendLine()
                    lHasContext = True
                End If
                
                ' Project structure context
                If pIncludeProjectFiles AndAlso Not String.IsNullOrEmpty(pProjectRoot) Then
                    lContext.AppendLine("=== project STRUCTURE ===")
                    lContext.AppendLine(GetProjectStructure())
                    lContext.AppendLine()
                    lHasContext = True
                End If
                
                ' Build errors context
                If pIncludeBuildErrors AndAlso pBuildErrors.Count > 0 Then
                    lContext.AppendLine("=== BUILD Errors ===")
                    for each lError in pBuildErrors
                        lContext.AppendLine($"{lError.Severity}: {lError.Message}")
                        lContext.AppendLine($"  File: {lError.FilePath}:{lError.Line}")
                    Next
                    lContext.AppendLine()
                    lHasContext = True
                End If
                
                If lHasContext Then
                    lContext.Insert(0, "=== Context ===" & Environment.NewLine)
                    Return lContext.ToString()
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"BuildContext error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        Private Function GetProjectStructure() As String
            ' TODO: Implement project structure extraction
            Return "project structure would be listed here..."
        End Function
        
        Private Sub AddMessage(vRole As String, vContent As String)
            Try
                ' Add to history
                pConversationHistory.Add(New ChatMessage("", "") with {
                    .Role = vRole,
                    .Content = vContent,
                    .Timestamp = DateTime.Now
                })
                
                ' Add to chat view
                Dim lIter As TextIter = pChatBuffer.EndIter
                
                ' Add role header
                Select Case vRole
                    Case "user"
                        pChatBuffer.InsertWithTagsByName(lIter, "You: ", "user")
                    Case "assistant"
                        pChatBuffer.InsertWithTagsByName(lIter, "Assistant: ", "assistant")
                    Case "error"
                        pChatBuffer.InsertWithTagsByName(lIter, "error: ", "error")
                End Select
                
                ' Add content
                pChatBuffer.InsertAtCursor(vContent & Environment.NewLine & Environment.NewLine)
                
                ' Scroll to bottom
                pChatView.ScrollToMark(pChatBuffer.InsertMark, 0.0, False, 0.0, 0.0)
                
            Catch ex As Exception
                Console.WriteLine($"AddMessage error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateContextInfo()
            Try
                Dim lContextParts As New List(Of String)
                
                If pIncludeCurrentFile AndAlso pCurrentEditor IsNot Nothing Then
                    lContextParts.Add($"current: {System.IO.Path.GetFileName(pCurrentEditor.FilePath)}")
                End If
                
                If pIncludeProjectFiles AndAlso Not String.IsNullOrEmpty(pProjectRoot) Then
                    lContextParts.Add("Project")
                End If
                
                If pIncludeBuildErrors AndAlso pBuildErrors.Count > 0 Then
                    lContextParts.Add($"{pBuildErrors.Count} Errors")
                End If
                
                Dim lContextInfo As String = If(lContextParts.Count > 0, 
                                               "Context: " & String.Join(", ", lContextParts),
                                               "No Context included")
                
                ' Update UI to show context (could add a label for this)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateContextInfo error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateStatus(vMessage As String)
            Try
                pStatusLabel.Text = vMessage
            Catch ex As Exception
                Console.WriteLine($"UpdateStatus error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateUI()
            Try
                pSendButton.Sensitive = Not pIsProcessing
                pPromptEntry.Sensitive = Not pIsProcessing
                pActionToolbar.Sensitive = Not pIsProcessing
            Catch ex As Exception
                Console.WriteLine($"UpdateUI error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        Private Sub OnSendClicked(vSender As Object, vArgs As EventArgs)
            Dim lMessage As String = pPromptEntry.Buffer.Text.Trim()
            If Not String.IsNullOrWhiteSpace(lMessage) Then
                Task.Run(Function() SendMessage(lMessage))
            End If
        End Sub
        
        Private Function OnPromptKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
            ' Send on Ctrl+Enter
            If vArgs.Event.State.HasFlag(ModifierType.ControlMask) AndAlso 
               (vArgs.Event.key = Gdk.key.Return OrElse vArgs.Event.key = Gdk.key.KP_Enter) Then
                OnSendClicked(Nothing, Nothing)
                Return True
            End If
            Return False
        End Function
        
        Private Sub OnCurrentFileToggled(vSender As Object, vArgs As EventArgs)
            pIncludeCurrentFile = pCurrentFileButton.Active
            UpdateContextInfo()
        End Sub
        
        Private Sub OnProjectFilesToggled(vSender As Object, vArgs As EventArgs)
            pIncludeProjectFiles = pProjectFilesButton.Active
            UpdateContextInfo()
        End Sub
        
        Private Sub OnBuildErrorsToggled(vSender As Object, vArgs As EventArgs)
            pIncludeBuildErrors = pBuildErrorsButton.Active
            UpdateContextInfo()
        End Sub
        
        Private Sub OnContextButtonClicked(vSender As Object, vArgs As EventArgs)
            ' Show context information dialog
            Dim lContext As String = BuildContext()
            If String.IsNullOrEmpty(lContext) Then
                lContext = "No Context is currently included. Use the toolbar buttons to Include Context."
            End If
            
            ' Could show in a dialog or add to chat
            AddMessage("Context", lContext)
        End Sub
        
        Private Sub OnClearClicked(vSender As Object, vArgs As EventArgs)
            pConversationHistory.Clear()
            pChatBuffer.Clear()
            AddMessage("assistant", "Conversation cleared. How can i help you?")
        End Sub
        
        ' ===== Styling =====
        Private Sub ApplyStyling()
            Try
                Dim lCss As String = "
                    textview.chat-view {
                        font-size: 11pt;
                    }
                    .Status-bar {
                        background-color: #f0f0f0;
                        padding: 2px;
                        font-size: 9pt;
                    }
                "
                
                CssHelper.ApplyCssToWidget(pChatView, lCss, "chat-view")
                CssHelper.ApplyCssToWidget(pStatusLabel.Parent, lCss, "Status-bar")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyStyling error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
