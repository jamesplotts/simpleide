' Widgets/AIAssistantPanel.vb - AI Assistant integration panel
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks
Imports System.Text
Imports System.Net.Http
Imports System.Text.Json
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Editors

Namespace Widgets
    Public Class AIAssistantPanel
        Inherits Box
        
        ' Private fields
        Private pNotebook As Notebook
        Private pChatView As TextView
        Private pChatBuffer As TextBuffer
        Private pPromptEntry As TextView
        Private pSendButton As Button
        Private pActionButtons As New Dictionary(Of String, Button)
        Private pProjectRoot As String
        Private pCurrentTab As TabInfo
        Private pApiClient As EnhancedClaudeApiClient
        Private pFileSystemBridge As AIFileSystemBridge
        Private pIsProcessing As Boolean = False
        Private pConversationHistory As New List(Of ImprovedAIAssistantPanel.ChatMessage)
        
        ' Action buttons
        Private pCreateProjectButton As Button
        Private pAddFileButton As Button
        Private pModifyCodeButton As Button
        Private pExplainCodeButton As Button
        Private pFixErrorsButton As Button
        Private pRefactorButton As Button
        Private pGenerateTestsButton As Button
        
        ' Events
        Public Event FileCreated(vFilePath As String)
        Public Event FileModified(vFilePath As String)
        Public Event ProjectCreated(vProjectPath As String)
        Public Event BuildRequested()
        Public Event StatusUpdate(vMessage As String)
        
        ' Chat message structure
        Public Class ChatMessage
            Public Property Role As String ' "user" or "assistant"
            Public Property Content As String
            Public Property Timestamp As DateTime
            Public Property Actions As List(Of AIAction)
            
            Public Sub New(vRole As String, vContent As String)
                Role = vRole
                Content = vContent
                Timestamp = DateTime.Now
                Actions = New List(Of AIAction)
            End Sub
        End Class
        
        ' AI action structure
        Public Class AIAction
            Public Property Type As String ' "create_file", "modify_file", "delete_file", etc.
            Public Property FilePath As String
            Public Property Content As String
            Public Property Description As String
            Public Property Executed As Boolean = False
        End Class
        
        Public Sub New(vApiKey As String)
            MyBase.New(Orientation.Vertical, 0)
            
            ' Initialize API client
            pApiClient = New EnhancedClaudeApiClient(vApiKey)
            pFileSystemBridge = New AIFileSystemBridge()
            
            BuildUI()
            ConnectEvents()
            
            ' Add welcome message
            AddAssistantMessage("Hello! i'm your AI coding assistant. i can help you create projects, write code, fix Errors, and more. What would you like to work on today?")
        End Sub

        Public Sub Initialize(vApiKey As String)
            ' Initialize API client
            pApiClient = New EnhancedClaudeApiClient(vApiKey)
            pFileSystemBridge = New AIFileSystemBridge()
        End Sub
        
        Private Sub BuildUI()
            ' Create toolbar
            Dim lToolbar As Widget = CreateToolbar()
            PackStart(lToolbar, False, False, 0)
            
            ' Create main paned for chat and actions
            Dim lPaned As New Paned(Orientation.Vertical)
            
            ' Top: Chat history
            Dim lChatScroll As New ScrolledWindow()
            lChatScroll.SetPolicy(PolicyType.Automatic, PolicyType.Always)
            lChatScroll.HeightRequest = 300
            
            pChatView = New TextView()
            pChatView.Editable = False
            pChatView.WrapMode = WrapMode.Word
            pChatView.LeftMargin = 10
            pChatView.RightMargin = 10
            pChatBuffer = pChatView.Buffer
            
            ' Create text tags for formatting
            CreateChatTags()
            
            lChatScroll.Add(pChatView)
            lPaned.Pack1(lChatScroll, True, True)
            
            ' Bottom: Input area
            Dim lInputBox As New Box(Orientation.Vertical, 6)
            lInputBox.BorderWidth = 6
            
            ' Quick action buttons
            Dim lActionsBox As New Box(Orientation.Horizontal, 6)
            
            pCreateProjectButton = CreateActionButton("New project", "document-New")
            pAddFileButton = CreateActionButton("Add File", "document-New")
            pModifyCodeButton = CreateActionButton("Modify code", "document-edit")
            pExplainCodeButton = CreateActionButton("Explain", "help-about")
            pFixErrorsButton = CreateActionButton("Fix Errors", "dialog-error")
            pRefactorButton = CreateActionButton("Refactor", "view-Refresh")
            pGenerateTestsButton = CreateActionButton("Gen Tests", "emblem-Default")
            
            lActionsBox.PackStart(pCreateProjectButton, False, False, 0)
            lActionsBox.PackStart(pAddFileButton, False, False, 0)
            lActionsBox.PackStart(pModifyCodeButton, False, False, 0)
            lActionsBox.PackStart(pExplainCodeButton, False, False, 0)
            lActionsBox.PackStart(pFixErrorsButton, False, False, 0)
            lActionsBox.PackStart(pRefactorButton, False, False, 0)
            lActionsBox.PackStart(pGenerateTestsButton, False, False, 0)
            
            lInputBox.PackStart(lActionsBox, False, False, 0)
            
            ' Prompt input
            Dim lPromptLabel As New Label("Your prompt:")
            lPromptLabel.Halign = Align.Start
            lInputBox.PackStart(lPromptLabel, False, False, 0)
            
            Dim lPromptScroll As New ScrolledWindow()
            lPromptScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lPromptScroll.HeightRequest = 80
            lPromptScroll.ShadowType = ShadowType.In
            
            pPromptEntry = New TextView()
            pPromptEntry.WrapMode = WrapMode.Word
            pPromptEntry.AcceptsTab = False
            lPromptScroll.Add(pPromptEntry)
            lInputBox.PackStart(lPromptScroll, True, True, 0)
            
            ' Send button
            Dim lButtonBox As New Box(Orientation.Horizontal, 6)
            pSendButton = New Button("Send")
            pSendButton.Sensitive = False
            lButtonBox.PackEnd(pSendButton, False, False, 0)
            lInputBox.PackStart(lButtonBox, False, False, 0)
            
            lPaned.Pack2(lInputBox, False, False)
            
            PackStart(lPaned, True, True, 0)
            
            ShowAll()
        End Sub
        
        Private Function CreateToolbar() As Widget
            Dim lToolbar As New Toolbar()
            lToolbar.ToolbarStyle = ToolbarStyle.Both
            lToolbar.IconSize = IconSize.SmallToolbar
            
            ' Clear conversation
            Dim lClearButton As New ToolButton(Nothing, "Clear")
            lClearButton.IconWidget = Image.NewFromIconName("edit-Clear", IconSize.SmallToolbar)
            lClearButton.TooltipText = "Clear conversation"
            AddHandler lClearButton.Clicked, AddressOf OnClearConversation
            lToolbar.Insert(lClearButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Save conversation
            Dim lSaveButton As New ToolButton(Nothing, "Save")
            lSaveButton.IconWidget = Image.NewFromIconName("document-Save", IconSize.SmallToolbar)
            lSaveButton.TooltipText = "Save conversation"
            AddHandler lSaveButton.Clicked, AddressOf OnSaveConversation
            lToolbar.Insert(lSaveButton, -1)
            
            Return lToolbar
        End Function

        Public Sub UpdateProjectContext(vKnowledgeBuilder As StringBuilder)
            ' TODO: Implement
        End Sub
        
        Private Function CreateActionButton(vLabel As String, vIcon As String) As Button
            Dim lButton As New Button()
            Dim lBox As New Box(Orientation.Vertical, 2)
            
            Dim lImage As New Image()
            lImage.SetFromIconName(vIcon, IconSize.LargeToolbar)
            lBox.PackStart(lImage, False, False, 0)
            
            Dim lLabel As New Label(vLabel)
            lLabel.SetSizeRequest(80, -1)
            lBox.PackStart(lLabel, False, False, 0)
            
            lButton.Add(lBox)
            lButton.TooltipText = vLabel
            
            Return lButton
        End Function
        
        Private Sub CreateChatTags()
            ' User message tag
            Dim lUserTag As New TextTag("user")
            lUserTag.Weight = Pango.Weight.Bold
            lUserTag.Foreground = "#0066CC"
            pChatBuffer.TagTable.Add(lUserTag)
            
            ' Assistant message tag
            Dim lAssistantTag As New TextTag("assistant")
            lAssistantTag.Foreground = "#006600"
            pChatBuffer.TagTable.Add(lAssistantTag)
            
            ' Code tag
            Dim lCodeTag As New TextTag("code")
            lCodeTag.Family = "Monospace"
            lCodeTag.Background = "#F5F5F5"
            lCodeTag.Foreground = "#333333"
            pChatBuffer.TagTable.Add(lCodeTag)
            
            ' Action tag
            Dim lActionTag As New TextTag("action")
            lActionTag.Style = Pango.Style.Italic
            lActionTag.Foreground = "#666666"
            pChatBuffer.TagTable.Add(lActionTag)
            
            ' Error tag
            Dim lErrorTag As New TextTag("error")
            lErrorTag.Foreground = "#CC0000"
            pChatBuffer.TagTable.Add(lErrorTag)
        End Sub
        
        Private Sub ConnectEvents()
            ' Prompt entry events
            AddHandler pPromptEntry.Buffer.Changed, AddressOf OnPromptChanged
            AddHandler pPromptEntry.KeyPressEvent, AddressOf OnPromptKeyPress
            
            ' Send button
            AddHandler pSendButton.Clicked, AddressOf OnSendMessage
            
            ' Action buttons
            AddHandler pCreateProjectButton.Clicked, Sub() SendPredefinedPrompt("Create a New VB.NET project")
            AddHandler pAddFileButton.Clicked, Sub() SendPredefinedPrompt("Add a New file To the project")
            AddHandler pModifyCodeButton.Clicked, Sub() SendPredefinedPrompt("Modify the current code")
            AddHandler pExplainCodeButton.Clicked, AddressOf OnExplainCode
            AddHandler pFixErrorsButton.Clicked, AddressOf OnFixErrors
            AddHandler pRefactorButton.Clicked, Sub() SendPredefinedPrompt("Refactor the selected code")
            AddHandler pGenerateTestsButton.Clicked, Sub() SendPredefinedPrompt("Generate unit tests for this code")
        End Sub
        
        Private Sub OnPromptChanged(vSender As Object, vE As EventArgs)
            pSendButton.Sensitive = Not String.IsNullOrWhiteSpace(pPromptEntry.Buffer.Text) AndAlso Not pIsProcessing
        End Sub
        
        Private Sub OnPromptKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            ' Ctrl+Enter to send
            If (vArgs.Event.State And Gdk.ModifierType.ControlMask) = Gdk.ModifierType.ControlMask AndAlso
               (vArgs.Event.key = Gdk.key.Return OrElse vArgs.Event.key = Gdk.key.KP_Enter) Then
                If pSendButton.Sensitive Then
                    OnSendMessage(Nothing, Nothing)
                End If
                vArgs.RetVal = True
            End If
        End Sub
        
        Private Async Sub OnSendMessage(vSender As Object, vE As EventArgs)
            If pIsProcessing Then Return
            
            Dim lPrompt As String = pPromptEntry.Buffer.Text.Trim()
            If String.IsNullOrEmpty(lPrompt) Then Return
            
            ' Add user message
            AddUserMessage(lPrompt)
            
            ' Clear prompt
            pPromptEntry.Buffer.Text = ""
            
            ' Send to AI
            Await ProcessAIRequest(lPrompt)
        End Sub

        ' Public method to send a message programmatically
        Public Sub SendMessage(vMessage As String)
            Try
                If String.IsNullOrWhiteSpace(vMessage) Then Return
                
                ' Set the prompt text
                pPromptEntry.Buffer.Text = vMessage
                
                ' Trigger the send
                OnSendMessage(Nothing, Nothing)
                
            Catch ex As Exception
                Console.WriteLine($"error in SendMessage: {ex.Message}")
            End Try
        End Sub
        
        Private Async Function ProcessAIRequest(vPrompt As String) As Task
            pIsProcessing = True
            UpdateUI()
            
            Try
                ' Add context about current state
                Dim lContext As String = BuildContextPrompt()
                Dim lFullPrompt As String = lContext & Environment.NewLine & Environment.NewLine & vPrompt
                
                ' Call Claude API
                Dim lResponse As EnhancedClaudeApiClient.ClaudeResponse = Await pApiClient.SendMessageWithArtifactsAsync(lFullPrompt, pConversationHistory)
                
                ' Parse response for actions
                Dim lActions As List(Of AIAction) = ParseAIResponse(lResponse.Content)
                
                ' WITH:
                Dim lConvertedHistory As New List(Of ImprovedAIAssistantPanel.ChatMessage)
                For Each lMsg In pConversationHistory
                    Dim lNewMsg As New ImprovedAIAssistantPanel.ChatMessage("", "")
                    lNewMsg.Role = lMsg.Role
                    lNewMsg.Content = lMsg.Content
                    lNewMsg.Timestamp = lMsg.Timestamp
                    lConvertedHistory.Add(lNewMsg)
                Next
                
                lResponse = Await pApiClient.SendMessageWithArtifactsAsync(lFullPrompt, lConvertedHistory)
                
                ' Parse response for actions
                lActions = ParseAIResponse(lResponse.Content)                
                ' Execute actions if any
                If lActions.Count > 0 Then
                    Await ExecuteAIActions(lActions)
                End If
                
            Catch ex As Exception
                AddErrorMessage($"error: {ex.Message}")
            Finally
                pIsProcessing = False
                UpdateUI()
            End Try
        End Function
        
        Private Function BuildContextPrompt() As String
            Dim lContext As New StringBuilder()
            
            lContext.AppendLine("current Context:")
            lContext.AppendLine($"- project root: {If(String.IsNullOrEmpty(pProjectRoot), "None", pProjectRoot)}")
            
            If pCurrentTab IsNot Nothing Then
                lContext.AppendLine($"- current file: {pCurrentTab.FilePath}")
                lContext.AppendLine($"- File Type: VB.NET")
                
                ' Include current code if not too large
                If pCurrentTab.Editor.CharCount < 5000 Then
                    lContext.AppendLine("- current code:")
                    lContext.AppendLine("```vb")
                    lContext.AppendLine(pCurrentTab.Editor.Text)
                    lContext.AppendLine("```")
                End If
            End If
            
            Return lContext.ToString()
        End Function
        
        Private Function ParseAIResponse(vResponse As String) As List(Of AIAction)
            Dim lActions As New List(Of AIAction)
            
            ' Parse for code blocks and action indicators
            ' This is a simplified parser - you'd want more sophisticated parsing
            
            ' Look for file creation patterns
            If vResponse.Contains("Create file:") OrElse vResponse.Contains("New file:") Then
                ' Extract file creation instructions
                ' Parse filename and content
            End If
            
            ' Look for file modification patterns
            If vResponse.Contains("Modify file:") OrElse vResponse.Contains("Update file:") Then
                ' Extract modification instructions
            End If
            
            Return lActions
        End Function
        
        Private Async Function ExecuteAIActions(vActions As List(Of AIAction)) As Task
            For Each lAction In vActions
                Try
                    Select Case lAction.Type
                        Case "create_file"
                            Await CreateFileAsync(lAction)
                        Case "modify_file"
                            Await ModifyFileAsync(lAction)
                        Case "delete_file"
                            Await DeleteFileAsync(lAction)
                        Case "create_project"
                            Await CreateProjectAsync(lAction)
                    End Select
                    
                    lAction.Executed = True
                    
                Catch ex As Exception
                    AddErrorMessage($"Failed To execute action: {ex.Message}")
                End Try
            Next
        End Function
        
        Private Async Function CreateFileAsync(vAction As AIAction) As Task
            Await Task.Run(Sub()
        Dim lFullPath As String = System.IO.Path.Combine(pProjectRoot, vAction.FilePath)
        Dim lDirectory As String = System.IO.Path.GetDirectoryName(lFullPath)
                
                If Not Directory.Exists(lDirectory) Then
                    Directory.CreateDirectory(lDirectory)
                End If
                
                File.WriteAllText(lFullPath, vAction.Content)
                
                GLib.Idle.Add(Function()
                    RaiseEvent FileCreated(lFullPath)
                    AddActionMessage($"Created file: {vAction.FilePath}")
                    Return False
                End Function)
            End Sub)
        End Function
        
        Private Async Function ModifyFileAsync(vAction As AIAction) As Task
            Await Task.Run(Sub()
                Dim lFullPath As String = System.IO.Path.Combine(pProjectRoot, vAction.FilePath)
                
                If File.Exists(lFullPath) Then
                    File.WriteAllText(lFullPath, vAction.Content)
                    
                    GLib.Idle.Add(Function()
                        RaiseEvent FileModified(lFullPath)
                        AddActionMessage($"Modified file: {vAction.FilePath}")
                        Return False
                    End Function)
                End If
            End Sub)
        End Function
        
        Private Async Function DeleteFileAsync(vAction As AIAction) As Task
            Await Task.Run(Sub()
                Try
                    Dim lFullPath As String = System.IO.Path.Combine(pProjectRoot, vAction.FilePath)
                    If File.Exists(lFullPath) Then
                        File.Delete(lFullPath)
                        
                        GLib.Idle.Add(Function()
                            AddActionMessage($"Deleted file: {vAction.FilePath}")
                            Return False
                        End Function)
                    End If
                Catch ex As Exception
                    Console.WriteLine($"error deleting file: {ex.Message}")
                End Try
            End Sub)
        End Function
        
        Private Async Function CreateProjectAsync(vAction As AIAction) As Task
            Await Task.Run(Sub()
                Try
                    ' Implementation for project creation
                    Dim lProjectPath As String = System.IO.Path.Combine(pProjectRoot, vAction.FilePath)
                    
                    ' Create project structure
                    Directory.CreateDirectory(lProjectPath)
                    
                    ' Create .vbproj file, etc.
                    ' ... implementation details ...
                    
                    GLib.Idle.Add(Function()
                        RaiseEvent ProjectCreated(lProjectPath)
                        AddActionMessage($"Created project: {vAction.FilePath}")
                        Return False
                    End Function)
                Catch ex As Exception
                    Console.WriteLine($"error creating project: {ex.Message}")
                End Try
            End Sub)
        End Function
        
        Private Sub OnExplainCode(vSender As Object, vE As EventArgs)
            If pCurrentTab Is Nothing Then
                AddErrorMessage("No file Is currently open.")
                Return
            End If
            
            ' Get selected text or all text
            Dim lCode As String = ""
            Dim lStartIter As TextIter = Nothing
            Dim lEndIter As TextIter = Nothing
            
            lCode = pCurrentTab.Editor.GetSelectedText
            
            If Not String.IsNullOrEmpty(lCode) Then
                Dim lPrompt As String = $"Please explain this VB.NET code:{Environment.NewLine}```vb{Environment.NewLine}{lCode}{Environment.NewLine}```"
                pPromptEntry.Buffer.Text = lPrompt
                OnSendMessage(Nothing, Nothing)
            End If
        End Sub
        
        Private Sub OnFixErrors(vSender As Object, vE As EventArgs)
            ' Get build errors if any
            ' Send to AI for fixes
        End Sub
        
        Private Sub SendPredefinedPrompt(vPrompt As String)
            pPromptEntry.Buffer.Text = vPrompt
            pPromptEntry.Buffer.PlaceCursor(pPromptEntry.Buffer.EndIter)
        End Sub
        
        Private Sub AddUserMessage(vMessage As String)
            AddChatMessage("You", vMessage, "user")
            pConversationHistory.Add(New ImprovedAIAssistantPanel.ChatMessage("user", vMessage))
        End Sub
        
        Private Sub AddAssistantMessage(vMessage As String, Optional vActions As List(Of AIAction) = Nothing)
            AddChatMessage("Assistant", vMessage, "assistant")
            
            Dim lChatMessage As New ImprovedAIAssistantPanel.ChatMessage("assistant", vMessage)
            If vActions IsNot Nothing Then
                lChatMessage.Actions = vActions
            End If
            pConversationHistory.Add(lChatMessage)
        End Sub
        
        Private Sub AddErrorMessage(vMessage As String)
            AddChatMessage("error", vMessage, "error")
        End Sub
        
        Private Sub AddActionMessage(vMessage As String)
            Dim lEndIter As TextIter = pChatBuffer.EndIter
            pChatBuffer.InsertAtCursor($"â†’ {vMessage}{Environment.NewLine}")
            
            Dim lStartIter As TextIter = pChatBuffer.GetIterAtOffset(pChatBuffer.CharCount - vMessage.Length - 3)
            pChatBuffer.ApplyTag("action", lStartIter, pChatBuffer.EndIter)
            
            ScrollToBottom()
        End Sub
        
        ''' <summary>
        ''' Adds a chat message with proper formatting and iterator handling
        ''' </summary>
        ''' <param name="vSender">The sender name</param>
        ''' <param name="vMessage">The message content</param>
        ''' <param name="vTag">The tag to apply to the sender</param>
        Private Sub AddChatMessage(vSender As String, vMessage As String, vTag As String)
            Try
                ' Build the complete message first
                Dim lTimestamp As String = DateTime.Now.ToString("HH:mm")
                Dim lFullMessage As String = $"[{lTimestamp}] {vSender}:{Environment.NewLine}{vMessage}{Environment.NewLine}"
                
                ' Store the starting offset
                Dim lStartOffset As Integer = pChatBuffer.CharCount
                
                ' Insert the complete message
                pChatBuffer.PlaceCursor(pChatBuffer.EndIter)
                pChatBuffer.InsertAtCursor(lFullMessage)
                
                ' Calculate tag positions based on the message structure
                ' Tag format: "[HH:mm] Sender:\n"
                Dim lSenderStart As Integer = lStartOffset + lTimestamp.Length + 3 ' "[HH:mm] "
                Dim lSenderEnd As Integer = lSenderStart + vSender.Length
                
                ' Apply tag to sender using stable offsets
                Dim lSenderStartIter As TextIter = pChatBuffer.GetIterAtOffset(lSenderStart)
                Dim lSenderEndIter As TextIter = pChatBuffer.GetIterAtOffset(lSenderEnd)
                pChatBuffer.ApplyTag(vTag, lSenderStartIter, lSenderEndIter)
                
                ScrollToBottom()
            Catch ex As Exception
                Console.WriteLine($"AddChatMessage error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Scrolls the chat view to the bottom using marks
        ''' </summary>
        Private Sub ScrollToBottom()
            Try
                GLib.Idle.Add(Function()
                    Try
                        ' Create a mark at the end (marks survive buffer changes)
                        Dim lEndMark As TextMark = pChatBuffer.CreateMark(Nothing, pChatBuffer.EndIter, False)
                        pChatView.ScrollToMark(lEndMark, 0.0, False, 0.0, 0.0)
                        pChatBuffer.DeleteMark(lEndMark)
                    Catch ex As Exception
                        Console.WriteLine($"ScrollToBottom inner error: {ex.Message}")
                    End Try
                    Return False
                End Function)
            Catch ex As Exception
                Console.WriteLine($"ScrollToBottom error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateUI()
            pSendButton.Sensitive = Not String.IsNullOrWhiteSpace(pPromptEntry.Buffer.Text) AndAlso Not pIsProcessing
            pPromptEntry.Sensitive = Not pIsProcessing
            
            ' Update action buttons
            For Each lButton In {pCreateProjectButton, pAddFileButton, pModifyCodeButton, 
                               pExplainCodeButton, pFixErrorsButton, pRefactorButton, pGenerateTestsButton}
                lButton.Sensitive = Not pIsProcessing
            Next
        End Sub
        
        Private Sub OnClearConversation(vSender As Object, vE As EventArgs)
            pChatBuffer.Text = ""
            pConversationHistory.Clear()
            AddAssistantMessage("Conversation cleared. How can i help you?")
        End Sub
        
        Private Sub OnSaveConversation(vSender As Object, vE As EventArgs)
            ' Save conversation to file
        End Sub
        
        ' Public properties
        Public Property ProjectRoot As String
            Get
                Return pProjectRoot
            End Get
            Set(Value As String)
                pProjectRoot = Value
                pFileSystemBridge.ProjectRoot = Value
            End Set
        End Property
        
        Public Property CurrentTab As TabInfo
            Get
                Return pCurrentTab
            End Get
            Set(Value As TabInfo)
                pCurrentTab = Value
            End Set
        End Property

    End Class

End Namespace
