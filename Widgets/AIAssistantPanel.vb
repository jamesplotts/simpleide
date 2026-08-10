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
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces

Namespace Widgets
    Public Class AIAssistantPanel
        Inherits Box
        
        ' Private fields
        Private pNotebook As Notebook
        Private pChatView As TextView
        Private pChatBuffer As TextBuffer
        Private pPromptEntry As TextView
        Private pSendButton As CustomDrawButton
        Private pActionButtons As New Dictionary(Of String, CustomDrawButton)
        Private pProjectRoot As String
        ''' <summary>Set via UpdateProjectContext - project-wide structure/reference knowledge folded into BuildContextPrompt</summary>
        Private pProjectKnowledge As String = ""
        Private pCurrentTab As TabInfo
        Private pApiClient As AIChatClient
        Private pFileSystemBridge As AIFileSystemBridge
        Private pIsProcessing As Boolean = False
        Private pConversationHistory As New List(Of ChatHistoryMessage)

        ' Theme support
        Private pThemeManager As ThemeManager
        Private pChatViewCssProvider As CssProvider
        Private pPromptEntryCssProvider As CssProvider

        ' Settings (used for the "Stream responses" preference)
        Private pSettingsManager As SettingsManager

        ''' <summary>Set via SetProjectManager - reapplied to pFileSystemBridge/pApiClient whenever either is (re)created, since it feeds the "```lookup```" symbol-search capability</summary>
        Private pProjectManager As ProjectManager

        ''' <summary>
        ''' Set via SetOpenTabLineReplaceHandler (wired by MainWindow through BottomPanelManager)
        ''' - given (fullFilePath, 1-based startLine, 1-based endLine, newText), performs the
        ''' replace through the live editor if that file is open (so it's undo-able via Ctrl+Z
        ''' and the buffer/redraw stay in sync) and reports the outcome; Nothing (the default,
        ''' or if the file isn't open) means ReplaceLinesAsync falls back to splicing the range
        ''' directly on disk instead
        ''' </summary>
        Private pOpenTabLineReplaceHandler As Func(Of String, Integer, Integer, String, LineReplaceOutcome)

        ' Action buttons
        Private pCreateProjectButton As CustomDrawButton
        Private pAddFileButton As CustomDrawButton
        Private pModifyCodeButton As CustomDrawButton
        Private pExplainCodeButton As CustomDrawButton
        Private pFixErrorsButton As CustomDrawButton
        Private pRefactorButton As CustomDrawButton
        Private pGenerateTestsButton As CustomDrawButton
        Private pClearButton As CustomDrawButton
        Private pSaveButton As CustomDrawButton
        
        ' Events
        Public Event FileCreated(vFilePath As String)
        Public Event FileModified(vFilePath As String)
        Public Event FileDeleted(vFilePath As String)
        Public Event ProjectCreated(vProjectPath As String)
        Public Event BuildRequested()
        Public Event StatusUpdate(vMessage As String)
        ''' <summary>Raised by the Fix Errors button - MainWindow relays the current build
        ''' errors back in via the same flow as its AI menu's "Fix Build Errors" item
        ''' (OnFixBuildErrors), since this panel has no direct reference to BuildOutputPanel</summary>
        Public Event FixErrorsRequested()
        
        ' AI action structure
        Public Class AIAction
            Public Property Type As String ' "create_file", "modify_file", "delete_file", "create_project", "replace_lines"
            Public Property FilePath As String
            Public Property Content As String
            Public Property Description As String
            ''' <summary>"Console", "Library", or "Gtk" - only set when Type = "create_project"</summary>
            Public Property ProjectType As String
            ''' <summary>1-based inclusive line range - only set when Type = "replace_lines"</summary>
            Public Property StartLine As Integer
            ''' <summary>1-based inclusive line range - see StartLine</summary>
            Public Property EndLine As Integer
            Public Property Executed As Boolean = False
        End Class

        ''' <summary>Result of AIAssistantPanel.OpenTabLineReplaceHandler - see SetOpenTabLineReplaceHandler</summary>
        Public Class LineReplaceOutcome
            ''' <summary>True if the target file had an open tab (whether or not the replace itself succeeded)</summary>
            Public Property WasOpen As Boolean
            ''' <summary>Only meaningful when WasOpen is True</summary>
            Public Property Success As Boolean
            ''' <summary>Only meaningful when WasOpen is True and Success is False</summary>
            Public Property ErrorMessage As String
        End Class
        
        ''' <param name="vProvider">The configured AI backend, or Nothing if none is usable yet</param>
        ''' <param name="vMem0ApiKey">Optional Mem0 API key (see AIProviderFactory.GetMem0ApiKey) to enable persistent memory context</param>
        Public Sub New(vProvider As IAIProvider, Optional vMem0ApiKey As String = "")
            MyBase.New(Orientation.Vertical, 0)

            ' Initialize API client - vProvider may be Nothing if AI isn't configured yet
            ' (no key set for the selected provider); SendMessage below surfaces that as an
            ' error rather than failing to construct the panel at all
            pApiClient = New AIChatClient(vProvider, vMem0ApiKey)
            pFileSystemBridge = New AIFileSystemBridge()
            ApplySymbolLookupWiring()
            LoadMem0UserContext()

            BuildUI()
            ConnectEvents()

            ' The welcome message (or a restored conversation in its place) is added from
            ' SetSettingsManager instead of here - AI.SaveHistory can't be checked until
            ' pSettingsManager is wired up, which happens right after construction
        End Sub

        ''' <summary>
        ''' (Re)configures which AI backend this panel talks to - called from
        ''' MainWindow.InitializeAI() both at startup and whenever Preferences' AI settings
        ''' are saved
        ''' </summary>
        ''' <param name="vProvider">The configured AI backend, or Nothing if none is usable yet</param>
        ''' <param name="vMem0ApiKey">Optional Mem0 API key (see AIProviderFactory.GetMem0ApiKey) to enable persistent memory context</param>
        Public Sub Initialize(vProvider As IAIProvider, Optional vMem0ApiKey As String = "")
            pApiClient = New AIChatClient(vProvider, vMem0ApiKey)
            pFileSystemBridge = New AIFileSystemBridge()
            ApplySymbolLookupWiring()
            LoadMem0UserContext()
        End Sub

        ''' <summary>
        ''' Wires up the shared ProjectManager, whose symbol index backs the "```lookup```"
        ''' capability the AI can use to find a symbol's location or pull its declaration/full
        ''' source (see AIChatClient.BuildEnhancedPrompt / AIFileSystemBridge.FindSymbolLocations
        ''' / GetSymbolSource) - called from MainWindow via BottomPanelManager once at startup
        ''' </summary>
        Public Sub SetProjectManager(vProjectManager As ProjectManager)
            pProjectManager = vProjectManager
            ApplySymbolLookupWiring()
        End Sub

        ''' <summary>
        ''' Wires up the open-tab, undo-safe line-replace path a "replace_lines" action tries
        ''' before falling back to an on-disk splice - see pOpenTabLineReplaceHandler and
        ''' ReplaceLinesAsync
        ''' </summary>
        Public Sub SetOpenTabLineReplaceHandler(vHandler As Func(Of String, Integer, Integer, String, LineReplaceOutcome))
            pOpenTabLineReplaceHandler = vHandler
        End Sub

        ''' <summary>
        ''' (Re)applies pProjectManager to pFileSystemBridge and wires pApiClient.
        ''' SymbolLookupHandler - called after any of pFileSystemBridge, pApiClient, or
        ''' pProjectManager is (re)assigned, since New/Initialize/SetProjectManager can each run
        ''' independently and in any order
        ''' </summary>
        Private Sub ApplySymbolLookupWiring()
            pFileSystemBridge.SetProjectManager(pProjectManager)
            pApiClient.SymbolLookupHandler = AddressOf HandleSymbolLookup
        End Sub

        ''' <summary>
        ''' AIChatClient.SymbolLookupHandler implementation - dispatches a "```lookup```"
        ''' block's Query type to the matching AIFileSystemBridge method
        ''' </summary>
        Private Function HandleSymbolLookup(vQueryType As String, vName As String) As String
            Try
                Select Case vQueryType.Trim().ToLowerInvariant()
                    Case "findlocation"
                        Return pFileSystemBridge.FindSymbolLocations(vName)
                    Case "getsource"
                        Return pFileSystemBridge.GetSymbolSource(vName)
                    Case Else
                        Return $"Unknown lookup Query type '{vQueryType}' - use FindLocation or GetSource."
                End Select
            Catch ex As Exception
                Console.WriteLine($"AIAssistantPanel.HandleSymbolLookup error: {ex.Message}")
                Return $"error performing lookup: {ex.Message}"
            End Try
        End Function

        ''' <summary>
        ''' Fires off AIChatClient.LoadUserContext() in the background so previously-stored Mem0
        ''' user preferences/code patterns are folded into the prompt context (see
        ''' AIChatClient.BuildEnhancedPrompt) for every message sent afterwards - a no-op if Mem0
        ''' wasn't enabled/configured, since pApiClient's own Mem0 client is then Nothing
        ''' </summary>
        Private Async Sub LoadMem0UserContext()
            Try
                Await pApiClient.LoadUserContext()
            Catch ex As Exception
                Console.WriteLine($"AIAssistantPanel.LoadMem0UserContext error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Applies the app's color theme to this panel's CustomDraw controls and to the
        ''' chat/prompt TextViews' background and foreground colors. The per-message role tags
        ''' (user/assistant/code/action/error, see CreateChatTags) keep their own fixed accent
        ''' colors regardless of theme - they're message-content styling, not panel chrome.
        ''' </summary>
        ''' <param name="vThemeManager">The shared ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                If pThemeManager IsNot Nothing Then
                    RemoveHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                pThemeManager = vThemeManager
                If pThemeManager IsNot Nothing Then
                    AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If

                pSendButton.ThemeManager = vThemeManager
                pCreateProjectButton.ThemeManager = vThemeManager
                pAddFileButton.ThemeManager = vThemeManager
                pModifyCodeButton.ThemeManager = vThemeManager
                pExplainCodeButton.ThemeManager = vThemeManager
                pFixErrorsButton.ThemeManager = vThemeManager
                pRefactorButton.ThemeManager = vThemeManager
                pGenerateTestsButton.ThemeManager = vThemeManager
                pClearButton.ThemeManager = vThemeManager
                pSaveButton.ThemeManager = vThemeManager

                ApplyCurrentTheme()

            Catch ex As Exception
                Console.WriteLine($"AIAssistantPanel.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Wires up the shared SettingsManager, used to check the "Stream responses"/"Save
        ''' history"/"Include project context automatically" preferences. Also where the initial
        ''' chat content is decided - a restored conversation (if AI.SaveHistory is on and one
        ''' was saved) or the generic welcome message otherwise - since AI.SaveHistory can't be
        ''' checked until this runs
        ''' </summary>
        Public Sub SetSettingsManager(vSettingsManager As SettingsManager)
            pSettingsManager = vSettingsManager

            If Not LoadConversationHistory() Then
                AddAssistantMessage("Hello! i'm your AI coding assistant. i can help you create projects, write code, fix Errors, and more. What would you like to work on today?")
            End If
        End Sub

        ''' <summary>
        ''' Handles live theme changes - the CustomDraw buttons redraw themselves via their
        ''' own ThemeChanged subscriptions, this only needs to refresh the TextViews' CSS
        ''' </summary>
        Private Sub OnThemeChanged(vTheme As EditorTheme)
            ApplyCurrentTheme()
        End Sub

        ''' <summary>
        ''' Applies the current theme's background/foreground to the chat and prompt TextViews
        ''' </summary>
        Private Sub ApplyCurrentTheme()
            Try
                If pThemeManager Is Nothing Then Return

                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                If lTheme Is Nothing Then Return

                Dim lCss As String = String.Format(
                    "textview {{ background-color: {0}; color: {1}; }}",
                    lTheme.EditorBackgroundColor, lTheme.ForegroundColor)

                If pChatView IsNot Nothing Then
                    If pChatViewCssProvider IsNot Nothing Then
                        pChatView.StyleContext.RemoveProvider(pChatViewCssProvider)
                    End If
                    pChatViewCssProvider = New CssProvider()
                    pChatViewCssProvider.LoadFromData(lCss)
                    pChatView.StyleContext.AddProvider(pChatViewCssProvider, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                End If

                If pPromptEntry IsNot Nothing Then
                    If pPromptEntryCssProvider IsNot Nothing Then
                        pPromptEntry.StyleContext.RemoveProvider(pPromptEntryCssProvider)
                    End If
                    pPromptEntryCssProvider = New CssProvider()
                    pPromptEntryCssProvider.LoadFromData(lCss)
                    pPromptEntry.StyleContext.AddProvider(pPromptEntryCssProvider, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                End If

            Catch ex As Exception
                Console.WriteLine($"AIAssistantPanel.ApplyCurrentTheme error: {ex.Message}")
            End Try
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
            
            pCreateProjectButton = CreateActionButton("New project", "document-new")
            pAddFileButton = CreateActionButton("Add File", "document-new")
            pModifyCodeButton = CreateActionButton("Modify code", "document-edit")
            pExplainCodeButton = CreateActionButton("Explain", "help-about")
            ' "dialog-error" is a status icon (bold/high-contrast by design) rather than an
            ' action icon like the others here, so it visually reads much larger than its
            ' siblings at the same pixel size - load it at half size to match
            pFixErrorsButton = CreateActionButton("Fix Errors", "dialog-error", 12)
            pRefactorButton = CreateActionButton("Refactor", "view-refresh")
            pGenerateTestsButton = CreateActionButton("Gen Tests", "emblem-default")
            
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
            pSendButton = New CustomDrawButton("Send")
            pSendButton.Sensitive = False
            lButtonBox.PackEnd(pSendButton, False, False, 0)
            lInputBox.PackStart(lButtonBox, False, False, 0)
            
            lPaned.Pack2(lInputBox, False, False)
            
            PackStart(lPaned, True, True, 0)
            
            ShowAll()
        End Sub
        
        ''' <summary>
        ''' Builds the beveled Clear/Save toolbar - was a native Gtk.Toolbar (ToolbarStyle.Both,
        ''' i.e. icon+label) with ToolButtons (flat, system icon-theme contrast), now matches
        ''' this panel's own CreateActionButton/CustomDrawButton convention used everywhere
        ''' else in this file
        ''' </summary>
        Private Function CreateToolbar() As Widget
            Dim lBox As New Box(Orientation.Horizontal, 2)

            ' Clear conversation
            pClearButton = New CustomDrawButton("Clear", LoadToolIconPixbuf("edit-clear"))
            pClearButton.TooltipText = "Clear conversation"
            AddHandler pClearButton.Clicked, AddressOf OnClearConversation
            lBox.PackStart(pClearButton, False, False, 0)

            lBox.PackStart(New Separator(Orientation.Vertical), False, False, 4)

            ' Save conversation
            pSaveButton = New CustomDrawButton("Save", LoadToolIconPixbuf("document-save"))
            pSaveButton.TooltipText = "Save conversation"
            AddHandler pSaveButton.Clicked, AddressOf OnSaveConversation
            lBox.PackStart(pSaveButton, False, False, 0)

            Return lBox
        End Function

        ''' <summary>
        ''' Loads a 16px icon-theme icon for a small toolbar button - CustomDrawButton's own
        ''' IconContrastHelper auto-inverts it for dark/light contrast
        ''' </summary>
        ''' <param name="vIconName">Icon-theme name to look up</param>
        Private Function LoadToolIconPixbuf(vIconName As String) As Gdk.Pixbuf
            Try
                Return Gtk.IconTheme.Default.LoadIcon(vIconName, 16, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"AIAssistantPanel.LoadToolIconPixbuf error ({vIconName}): {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Replaces the project-wide knowledge (structure, references, etc.) folded into every
        ''' prompt's context - called from MainWindow.UpdateProjectKnowledge, wired to the AI
        ''' menu's "Update Project Knowledge" item, since that's a potentially large/slow scan
        ''' the user asks for explicitly rather than something rebuilt on every keystroke
        ''' </summary>
        ''' <param name="vKnowledgeBuilder">The freshly-gathered project knowledge text, or Nothing to clear it</param>
        Public Sub UpdateProjectContext(vKnowledgeBuilder As StringBuilder)
            pProjectKnowledge = If(vKnowledgeBuilder Is Nothing, "", vKnowledgeBuilder.ToString())
        End Sub
        
        Private Function CreateActionButton(vLabel As String, vIcon As String, Optional vIconSize As Integer = 24) As CustomDrawButton
            Dim lIconPixbuf As Gdk.Pixbuf = Nothing
            Try
                lIconPixbuf = Gtk.IconTheme.Default.LoadIcon(vIcon, vIconSize, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"CreateActionButton icon load error: {ex.Message}")
            End Try

            Dim lButton As New CustomDrawButton(vLabel, lIconPixbuf)
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

            Dim lStreaming As Boolean = False

            Try
                ' "Include project context automatically" preference - defaults to on (matching
                ' the behavior before this was wired to a setting at all)
                Dim lAutoContext As Boolean = If(pSettingsManager Is Nothing, True, pSettingsManager.GetBoolean("AI.AutoContext", True))
                Dim lContext As String = If(lAutoContext, BuildContextPrompt(), "")
                Dim lFullPrompt As String = lContext & Environment.NewLine & Environment.NewLine & vPrompt

                ' "Stream responses" preference - defaults to on, matching the Preferences
                ' checkbox's own default (see PreferencesTab.CreateAITab)
                lStreaming = If(pSettingsManager Is Nothing, True, pSettingsManager.GetBoolean("AI.StreamResponses", True))

                Dim lResponse As AIChatClient.ClaudeResponse

                If lStreaming Then
                    BeginStreamingAssistantMessage()
                    lResponse = Await pApiClient.SendMessageWithArtifactsAsync(lFullPrompt, pConversationHistory, AddressOf OnStreamingChunkReceived)
                    Dim lActions As List(Of AIAction) = ParseAIResponse(lResponse.Artifacts)
                    EndStreamingAssistantMessage(lResponse.Content, lActions)

                    If lActions.Count > 0 Then
                        Await ExecuteAIActions(lActions)
                    End If
                Else
                    lResponse = Await pApiClient.SendMessageWithArtifactsAsync(lFullPrompt, pConversationHistory)
                    Dim lActions As List(Of AIAction) = ParseAIResponse(lResponse.Artifacts)
                    AddAssistantMessage(lResponse.Content, lActions)

                    If lActions.Count > 0 Then
                        Await ExecuteAIActions(lActions)
                    End If
                End If

            Catch ex As Exception
                ' A streaming response that already started printing to the chat view still
                ' needs its trailing newline/history entry closed off, even though it failed -
                ' otherwise the next message would run on immediately after the partial text
                If lStreaming AndAlso pStreamingMessageOpen Then
                    EndStreamingAssistantMessage(pStreamingAccumulatedText.ToString(), Nothing)
                End If
                AddErrorMessage($"error: {ex.Message}")
            Finally
                pIsProcessing = False
                UpdateUI()
            End Try
        End Function

        ' ===== Streaming Response Display =====

        Private pStreamingMessageOpen As Boolean = False
        Private pStreamingAccumulatedText As New StringBuilder()

        ''' <summary>
        ''' Inserts the "[HH:mm] Assistant:" header for a streaming response and leaves the
        ''' cursor ready for incremental text - call once, before the streaming request starts
        ''' </summary>
        Private Sub BeginStreamingAssistantMessage()
            Try
                pStreamingAccumulatedText.Clear()
                pStreamingMessageOpen = True

                Dim lTimestamp As String = DateTime.Now.ToString("HH:mm")
                Const lSenderName As String = "Assistant"
                Dim lHeader As String = $"[{lTimestamp}] {lSenderName}:{Environment.NewLine}"

                Dim lStartOffset As Integer = pChatBuffer.CharCount
                pChatBuffer.PlaceCursor(pChatBuffer.EndIter)
                pChatBuffer.InsertAtCursor(lHeader)

                Dim lSenderStart As Integer = lStartOffset + lTimestamp.Length + 3 ' "[HH:mm] "
                Dim lSenderEnd As Integer = lSenderStart + lSenderName.Length
                pChatBuffer.ApplyTag("assistant", pChatBuffer.GetIterAtOffset(lSenderStart), pChatBuffer.GetIterAtOffset(lSenderEnd))

                ScrollToBottom()

            Catch ex As Exception
                Console.WriteLine($"BeginStreamingAssistantMessage error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' IAIProvider.SendMessageStreamingAsync's per-chunk callback - appends the chunk to
        ''' the chat view immediately. Marshaled through GLib.Idle.Add since the underlying
        ''' HTTP/process read loop this is called from is not guaranteed to resume on the GTK
        ''' main thread.
        ''' </summary>
        Private Sub OnStreamingChunkReceived(vChunk As String)
            pStreamingAccumulatedText.Append(vChunk)
            GLib.Idle.Add(Function()
                Try
                    pChatBuffer.PlaceCursor(pChatBuffer.EndIter)
                    pChatBuffer.InsertAtCursor(vChunk)
                    ScrollToBottom()
                Catch ex As Exception
                    Console.WriteLine($"OnStreamingChunkReceived error: {ex.Message}")
                End Try
                Return False
            End Function)
        End Sub

        ''' <summary>
        ''' Closes off a streaming response: adds the trailing blank line AddChatMessage would
        ''' normally add, and records the complete text in pConversationHistory - call once,
        ''' after the streaming request completes (successfully or not)
        ''' </summary>
        ''' <param name="vFullText">The complete accumulated response text</param>
        ''' <param name="vActions">Actions parsed from the response, if any</param>
        Private Sub EndStreamingAssistantMessage(vFullText As String, vActions As List(Of AIAction))
            Try
                pChatBuffer.PlaceCursor(pChatBuffer.EndIter)
                pChatBuffer.InsertAtCursor(Environment.NewLine)

                Dim lChatMessage As New ChatHistoryMessage("assistant", vFullText)
                If vActions IsNot Nothing Then
                    lChatMessage.Actions = vActions
                End If
                pConversationHistory.Add(lChatMessage)
                SaveConversationHistory()

                ScrollToBottom()

            Catch ex As Exception
                Console.WriteLine($"EndStreamingAssistantMessage error: {ex.Message}")
            Finally
                pStreamingMessageOpen = False
                pStreamingAccumulatedText.Clear()
            End Try
        End Sub
        
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

            If Not String.IsNullOrEmpty(pProjectKnowledge) Then
                lContext.AppendLine("- project knowledge:")
                lContext.AppendLine(pProjectKnowledge)
            End If

            Return lContext.ToString()
        End Function
        
        ''' <summary>
        ''' Turns the artifacts AIChatClient already extracted from the response into file
        ''' actions - only artifacts the model tagged with a FilePath (see AIChatClient.
        ''' BuildEnhancedPrompt) are actionable; untagged artifacts are just code shown in
        ''' the chat. Whether an artifact becomes a create or a modify is decided here, from
        ''' whether the target file currently exists.
        ''' </summary>
        ''' <param name="vArtifacts">Artifacts parsed from the assistant's response</param>
        Private Function ParseAIResponse(vArtifacts As List(Of AIChatClient.ClaudeArtifact)) As List(Of AIAction)
            Dim lActions As New List(Of AIAction)
            If vArtifacts Is Nothing Then Return lActions

            For Each lArtifact In vArtifacts
                If String.IsNullOrWhiteSpace(lArtifact.FilePath) Then Continue For

                If String.IsNullOrEmpty(pProjectRoot) Then
                    AddErrorMessage($"Cannot write '{lArtifact.FilePath}': no project is open.")
                    Continue For
                End If

                If String.Equals(lArtifact.Type, "project", StringComparison.OrdinalIgnoreCase) Then
                    lActions.Add(New AIAction With {
                        .Type = "create_project",
                        .FilePath = lArtifact.FilePath,
                        .ProjectType = If(String.IsNullOrWhiteSpace(lArtifact.ProjectType), "Console", lArtifact.ProjectType),
                        .Description = lArtifact.Title
                    })
                    Continue For
                End If

                If lArtifact.IsDelete Then
                    lActions.Add(New AIAction With {
                        .Type = "delete_file",
                        .FilePath = lArtifact.FilePath,
                        .Description = lArtifact.Title
                    })
                    Continue For
                End If

                If lArtifact.StartLine > 0 AndAlso lArtifact.EndLine > 0 Then
                    lActions.Add(New AIAction With {
                        .Type = "replace_lines",
                        .FilePath = lArtifact.FilePath,
                        .Content = lArtifact.Content,
                        .StartLine = lArtifact.StartLine,
                        .EndLine = lArtifact.EndLine,
                        .Description = lArtifact.Title
                    })
                    Continue For
                End If

                Dim lFullPath As String = System.IO.Path.Combine(pProjectRoot, lArtifact.FilePath)
                lActions.Add(New AIAction With {
                    .Type = If(File.Exists(lFullPath), "modify_file", "create_file"),
                    .FilePath = lArtifact.FilePath,
                    .Content = lArtifact.Content,
                    .Description = lArtifact.Title
                })
            Next

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
                        Case "replace_lines"
                            Await ReplaceLinesAsync(lAction)
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
                            RaiseEvent FileDeleted(lFullPath)
                            AddActionMessage($"Deleted file: {vAction.FilePath}")
                            Return False
                        End Function)
                    End If
                Catch ex As Exception
                    Console.WriteLine($"error deleting file: {ex.Message}")
                End Try
            End Sub)
        End Function

        ''' <summary>
        ''' Replaces vAction.StartLine..vAction.EndLine (1-based, inclusive) in vAction.FilePath
        ''' with vAction.Content. Tries pOpenTabLineReplaceHandler first (runs on this calling
        ''' thread, not inside Task.Run, since it touches live editor/UI state) so an open file
        ''' is edited through the editor itself - undo-able via Ctrl+Z, buffer/redraw stay in
        ''' sync. Falls back to splicing the range directly on disk when the file isn't open (or
        ''' no handler is wired) - there's no live document open in that case for undo to apply to
        ''' </summary>
        Private Async Function ReplaceLinesAsync(vAction As AIAction) As Task
            Try
                Dim lFullPath As String = System.IO.Path.Combine(pProjectRoot, vAction.FilePath)

                If Not File.Exists(lFullPath) Then
                    AddErrorMessage($"Cannot replace lines in '{vAction.FilePath}': file does not exist.")
                    Return
                End If

                If pOpenTabLineReplaceHandler IsNot Nothing Then
                    Dim lOutcome As LineReplaceOutcome = pOpenTabLineReplaceHandler(lFullPath, vAction.StartLine, vAction.EndLine, vAction.Content)
                    If lOutcome IsNot Nothing AndAlso lOutcome.WasOpen Then
                        If lOutcome.Success Then
                            AddActionMessage($"Replaced lines {vAction.StartLine}-{vAction.EndLine} in: {vAction.FilePath}")
                        Else
                            AddErrorMessage($"Failed to replace lines in '{vAction.FilePath}': {lOutcome.ErrorMessage}")
                        End If
                        Return
                    End If
                End If

                Await Task.Run(Sub()
                    Try
                        Dim lLines As List(Of String) = File.ReadAllLines(lFullPath).ToList()
                        Dim lStartIndex As Integer = vAction.StartLine - 1
                        Dim lEndIndex As Integer = vAction.EndLine - 1

                        If lStartIndex < 0 OrElse lEndIndex >= lLines.Count OrElse lStartIndex > lEndIndex Then
                            GLib.Idle.Add(Function()
                                AddErrorMessage($"Cannot replace lines {vAction.StartLine}-{vAction.EndLine} in '{vAction.FilePath}': file has {lLines.Count} lines.")
                                Return False
                            End Function)
                            Return
                        End If

                        Dim lNewLines As New List(Of String)
                        lNewLines.AddRange(lLines.Take(lStartIndex))
                        lNewLines.AddRange(vAction.Content.Replace(vbCrLf, vbLf).Split(vbLf))
                        lNewLines.AddRange(lLines.Skip(lEndIndex + 1))

                        File.WriteAllText(lFullPath, String.Join(Environment.NewLine, lNewLines))

                        GLib.Idle.Add(Function()
                            RaiseEvent FileModified(lFullPath)
                            AddActionMessage($"Replaced lines {vAction.StartLine}-{vAction.EndLine} in: {vAction.FilePath}")
                            Return False
                        End Function)

                    Catch ex As Exception
                        Console.WriteLine($"error replacing lines on disk: {ex.Message}")
                    End Try
                End Sub)

            Catch ex As Exception
                Console.WriteLine($"error replacing lines: {ex.Message}")
            End Try
        End Function

        ''' <summary>
        ''' Scaffolds a whole new VB.NET project via AIFileSystemBridge.CreateProject - the same
        ''' StringResources-driven templates MainWindow.CreateNewProject uses, so the result
        ''' builds with `dotnet build` like any project created through the New Project dialog
        ''' </summary>
        ''' <param name="vAction">A "create_project" action - FilePath is the new project's
        ''' folder name (created directly under pProjectRoot), ProjectType is Console/Library/Gtk</param>
        Private Async Function CreateProjectAsync(vAction As AIAction) As Task
            Await Task.Run(Sub()
                Try
                    Dim lResult As String = pFileSystemBridge.CreateProject(vAction.FilePath, pProjectRoot, vAction.ProjectType)

                    If lResult.StartsWith("error", StringComparison.OrdinalIgnoreCase) Then
                        GLib.Idle.Add(Function()
                            AddErrorMessage(lResult)
                            Return False
                        End Function)
                        Return
                    End If

                    GLib.Idle.Add(Function()
                        RaiseEvent ProjectCreated(lResult)
                        AddActionMessage($"Created project: {vAction.FilePath}")
                        Return False
                    End Function)
                Catch ex As Exception
                    Console.WriteLine($"error creating project: {ex.Message}")
                End Try
            End Sub)
        End Function
        
        ''' <summary>
        ''' Public entry point for the MainWindow AI menu's "Explain Selected Code" item - same
        ''' action as clicking the panel's own Explain button
        ''' </summary>
        Public Sub TriggerExplainCode()
            OnExplainCode(Me, EventArgs.Empty)
        End Sub

        Private Sub OnExplainCode(vSender As Object, vE As EventArgs)
            If pCurrentTab Is Nothing Then
                AddErrorMessage("No file Is currently open.")
                Return
            End If

            ' Explain the selection if there is one, otherwise fall back to the whole file
            ' rather than silently doing nothing
            Dim lCode As String = pCurrentTab.Editor.GetSelectedText
            If String.IsNullOrEmpty(lCode) Then
                lCode = pCurrentTab.Editor.Text
            End If

            If String.IsNullOrEmpty(lCode) Then
                AddErrorMessage("Nothing to explain - the current file is empty.")
                Return
            End If

            Dim lPrompt As String = $"Please explain this VB.NET code:{Environment.NewLine}```vb{Environment.NewLine}{lCode}{Environment.NewLine}```"
            pPromptEntry.Buffer.Text = lPrompt
            OnSendMessage(Nothing, Nothing)
        End Sub
        
        Private Sub OnFixErrors(vSender As Object, vE As EventArgs)
            RaiseEvent FixErrorsRequested()
        End Sub
        
        Private Sub SendPredefinedPrompt(vPrompt As String)
            pPromptEntry.Buffer.Text = vPrompt
            pPromptEntry.Buffer.PlaceCursor(pPromptEntry.Buffer.EndIter)
        End Sub
        
        Private Sub AddUserMessage(vMessage As String)
            AddChatMessage("You", vMessage, "user")
            pConversationHistory.Add(New ChatHistoryMessage("user", vMessage))
            SaveConversationHistory()
        End Sub

        Private Sub AddAssistantMessage(vMessage As String, Optional vActions As List(Of AIAction) = Nothing)
            AddChatMessage("Assistant", vMessage, "assistant")

            Dim lChatMessage As New ChatHistoryMessage("assistant", vMessage)
            If vActions IsNot Nothing Then
                lChatMessage.Actions = vActions
            End If
            pConversationHistory.Add(lChatMessage)
            SaveConversationHistory()
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
            DeleteConversationHistory()
            AddAssistantMessage("Conversation cleared. How can i help you?")
        End Sub

        ''' <summary>One persisted conversation turn - Role/Content/Timestamp only, no Actions/Artifacts (those aren't safe to silently re-execute on restore)</summary>
        Private Class PersistedMessage
            Public Property Role As String
            Public Property Content As String
            Public Property Timestamp As DateTime
        End Class

        Private Function GetHistoryFilePath() As String
            Dim lAppDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            Return System.IO.Path.Combine(lAppDataPath, "SimpleIDE", "ai_conversation_history.json")
        End Function

        ''' <summary>
        ''' Restores a previously-saved conversation from disk into the chat view and
        ''' pConversationHistory, if AI.SaveHistory is enabled and a saved conversation exists
        ''' </summary>
        ''' <returns>True if any messages were restored - the caller skips the generic welcome message in that case</returns>
        Private Function LoadConversationHistory() As Boolean
            Try
                If pSettingsManager Is Nothing OrElse Not pSettingsManager.GetBoolean("AI.SaveHistory", True) Then Return False

                Dim lPath As String = GetHistoryFilePath()
                If Not File.Exists(lPath) Then Return False

                Dim lJson As String = File.ReadAllText(lPath)
                If String.IsNullOrWhiteSpace(lJson) Then Return False

                Dim lMessages As List(Of PersistedMessage) = JsonSerializer.Deserialize(Of List(Of PersistedMessage))(lJson)
                If lMessages Is Nothing OrElse lMessages.Count = 0 Then Return False

                Dim lLimit As Integer = pSettingsManager.GetInteger("AI.HistoryLimit", 20)
                If lMessages.Count > lLimit Then
                    lMessages = lMessages.Skip(lMessages.Count - lLimit).ToList()
                End If

                For Each lMessage In lMessages
                    Dim lIsUser As Boolean = String.Equals(lMessage.Role, "user", StringComparison.OrdinalIgnoreCase)
                    AddChatMessage(If(lIsUser, "You", "Assistant"), lMessage.Content, If(lIsUser, "user", "assistant"))
                    pConversationHistory.Add(New ChatHistoryMessage(lMessage.Role, lMessage.Content))
                Next

                Return True

            Catch ex As Exception
                Console.WriteLine($"LoadConversationHistory error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Persists the current conversation to disk, trimmed to AI.HistoryLimit messages -
        ''' called after every message is added; a no-op unless AI.SaveHistory is enabled
        ''' </summary>
        Private Sub SaveConversationHistory()
            Try
                If pSettingsManager Is Nothing OrElse Not pSettingsManager.GetBoolean("AI.SaveHistory", True) Then Return

                Dim lLimit As Integer = pSettingsManager.GetInteger("AI.HistoryLimit", 20)
                Dim lToSave As List(Of ChatHistoryMessage) = pConversationHistory
                If lToSave.Count > lLimit Then
                    lToSave = lToSave.Skip(lToSave.Count - lLimit).ToList()
                End If

                Dim lPersisted As List(Of PersistedMessage) = lToSave.Select(
                    Function(m) New PersistedMessage With {.Role = m.Role, .Content = m.Content, .Timestamp = m.Timestamp}).ToList()

                Dim lPath As String = GetHistoryFilePath()
                Dim lDir As String = System.IO.Path.GetDirectoryName(lPath)
                If Not Directory.Exists(lDir) Then Directory.CreateDirectory(lDir)

                File.WriteAllText(lPath, JsonSerializer.Serialize(lPersisted))

            Catch ex As Exception
                Console.WriteLine($"SaveConversationHistory error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>Deletes the saved conversation file, if any - called from OnClearConversation so a cleared chat doesn't come back on next launch</summary>
        Private Sub DeleteConversationHistory()
            Try
                Dim lPath As String = GetHistoryFilePath()
                If File.Exists(lPath) Then File.Delete(lPath)
            Catch ex As Exception
                Console.WriteLine($"DeleteConversationHistory error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Saves the visible chat transcript (the same text shown in pChatView, timestamps and
        ''' all) to a plain-text file the user picks
        ''' </summary>
        Private Sub OnSaveConversation(vSender As Object, vE As EventArgs)
            Dim lDialog As FileChooserDialog = Nothing
            Try
                Dim lParentWindow As Window = TryCast(Me.Toplevel, Window)
                Dim lDefaultName As String = $"AI Conversation - {DateTime.Now:yyyy-MM-dd HHmm}.txt"
                lDialog = FileOperations.CreateExportDialog(lParentWindow, lDefaultName)
                If lDialog Is Nothing Then Return

                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    File.WriteAllText(lDialog.Filename, pChatBuffer.Text)
                    AddActionMessage($"Conversation saved to {lDialog.Filename}")
                End If

            Catch ex As Exception
                Console.WriteLine($"OnSaveConversation error: {ex.Message}")
                AddErrorMessage($"Failed to save conversation: {ex.Message}")
            Finally
                lDialog?.Destroy()
            End Try
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
