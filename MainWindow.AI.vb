' MainWindow.AI.vb - AI integration functionality for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Text
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Editors
Imports SimpleIDE.AI

Partial Public Class MainWindow

    ' ===== Private Fields =====
    Private pAIFileSystemBridge As AIFileSystemBridge
    Private pIsAIProcessing As Boolean = False

    ' ===== AI Integration Methods =====

    ''' <summary>
    ''' Builds the AI provider configured in Preferences (Claude API, Claude Code CLI,
    ''' OpenRouter, or a local LLM) and (re)wires it into the AI Assistant panel
    ''' </summary>
    Private Sub InitializeAI()
        Try
            Dim lProvider As IAIProvider = AIProviderFactory.CreateProvider(pSettingsManager)
            Dim lMem0ApiKey As String = AIProviderFactory.GetMem0ApiKey(pSettingsManager)

            pAIFileSystemBridge = New AIFileSystemBridge()

            ' Initialize AI Assistant panel if not already done - Nothing is a valid provider
            ' value here (nothing configured/usable yet), the panel surfaces that as a chat
            ' error the first time the user tries to send a message rather than refusing to
            ' initialize at all
            If pAIAssistantPanel IsNot Nothing Then
                pAIAssistantPanel.Initialize(lProvider, lMem0ApiKey)
            End If

        Catch ex As Exception
            Console.WriteLine($"InitializeAI error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Rescans the current project's structure and target framework/output type and folds
    ''' them into the AI Assistant panel's context (see AIAssistantPanel.UpdateProjectContext /
    ''' BuildContextPrompt) - a directory-tree scan, so it's run on demand from the AI menu
    ''' rather than automatically on every prompt
    ''' </summary>
    Public Sub UpdateProjectKnowledge()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before updating AI knowledge.")
                Return
            End If

            If pAIAssistantPanel Is Nothing Then
                ShowError("AI Not Configured", "Please configure AI settings first.")
                Return
            End If

            pAIFileSystemBridge.ProjectRoot = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lProjectInfo As ProjectFileParser.ProjectInfo = ProjectFileParser.ParseProjectFileEnhanced(pCurrentProject)

            Dim lKnowledge As New StringBuilder()
            lKnowledge.AppendLine($"Project: {System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)}")
            lKnowledge.AppendLine($"Target Framework: {lProjectInfo.TargetFramework}")
            lKnowledge.AppendLine($"Output Type: {lProjectInfo.OutputType}")
            lKnowledge.AppendLine("Structure:")
            lKnowledge.AppendLine(pAIFileSystemBridge.GetProjectStructure())

            pAIAssistantPanel.UpdateProjectContext(lKnowledge)

            UpdateStatusBar("Project knowledge updated")

        Catch ex As Exception
            Console.WriteLine($"UpdateProjectKnowledge error: {ex.Message}")
            ShowError("Update Knowledge Failed", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Show code generation dialog with AI options
    ''' </summary>
    Public Sub ShowGenerateCodeDialog()
        Try
            Dim lDialog As New Dialog("Generate Code with AI", Me,
                                    DialogFlags.Modal Or DialogFlags.DestroyWithParent)

            lDialog.SetDefaultSize(600, 400)
            
            ' Create dialog content
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Context selection
            Dim lContextFrame As New Frame("Context")
            Dim lContextBox As New Box(Orientation.Vertical, 5)
            lContextBox.BorderWidth = 10
            
            Dim lCurrentFile As New RadioButton("Current file")
            Dim lSelectedText As New RadioButton(lCurrentFile, "Selected text")
            Dim lWholeProject As New RadioButton(lCurrentFile, "Whole project")
            
            lContextBox.PackStart(lCurrentFile, False, False, 0)
            lContextBox.PackStart(lSelectedText, False, False, 0)
            lContextBox.PackStart(lWholeProject, False, False, 0)
            lContextFrame.Add(lContextBox)
            lVBox.PackStart(lContextFrame, False, False, 0)
            
            ' Code type selection
            Dim lTypeFrame As New Frame("Code Type")
            Dim lTypeBox As New Box(Orientation.Vertical, 5)
            lTypeBox.BorderWidth = 10
            
            Dim lImplementMethod As New RadioButton("Implement method")
            Dim lRefactorCode As New RadioButton(lImplementMethod, "Refactor code")
            Dim lAddDocumentation As New RadioButton(lImplementMethod, "Add documentation")
            Dim lCreateTests As New RadioButton(lImplementMethod, "Create unit tests")
            Dim lFixErrors As New RadioButton(lImplementMethod, "Fix errors")
            Dim lOptimizeCode As New RadioButton(lImplementMethod, "Optimize code")
            Dim lCreateNewFile As New RadioButton(lImplementMethod, "Create new file")
            
            lTypeBox.PackStart(lImplementMethod, False, False, 0)
            lTypeBox.PackStart(lRefactorCode, False, False, 0)
            lTypeBox.PackStart(lAddDocumentation, False, False, 0)
            lTypeBox.PackStart(lCreateTests, False, False, 0)
            lTypeBox.PackStart(lFixErrors, False, False, 0)
            lTypeBox.PackStart(lOptimizeCode, False, False, 0)
            lTypeBox.PackStart(lCreateNewFile, False, False, 0)
            lTypeFrame.Add(lTypeBox)
            lVBox.PackStart(lTypeFrame, False, False, 0)
            
            ' Additional instructions
            Dim lInstructionsLabel As New Label("Additional Instructions:")
            lInstructionsLabel.Xalign = 0
            lVBox.PackStart(lInstructionsLabel, False, False, 0)
            
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.in
            
            Dim lInstructionsView As New TextView()
            lInstructionsView.WrapMode = WrapMode.Word
            lScrolled.Add(lInstructionsView)
            lVBox.PackStart(lScrolled, True, True, 0)
            
            lDialog.ContentArea.Add(lVBox)

            Dim lButtonBox As New Box(Orientation.Horizontal, 6)
            lButtonBox.Halign = Align.End
            lButtonBox.BorderWidth = 6
            Dim lCancelButton As New CustomDrawButton("Cancel")
            lCancelButton.ThemeManager = pThemeManager
            AddHandler lCancelButton.Clicked, Sub() lDialog.Respond(ResponseType.Cancel)
            lButtonBox.PackStart(lCancelButton, False, False, 0)
            Dim lOkButton As New CustomDrawButton("OK")
            lOkButton.ThemeManager = pThemeManager
            AddHandler lOkButton.Clicked, Sub() lDialog.Respond(ResponseType.Ok)
            lButtonBox.PackStart(lOkButton, False, False, 0)
            Dim lContentBox As Box = TryCast(lDialog.ContentArea, Box)
            If lContentBox IsNot Nothing Then lContentBox.PackStart(lButtonBox, False, False, 0)

            lDialog.ShowAll()

            If lDialog.Run() = CInt(ResponseType.Ok) Then
                ' Build prompt based on selections
                Dim lPrompt As New StringBuilder()
                lPrompt.AppendLine("Please help me with the following code task:")
                
                ' Add context
                If lCurrentFile.Active Then
                    Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
                    If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                        lPrompt.AppendLine()
                        lPrompt.AppendLine($"Current file: {lCurrentTab.FilePath}")
                        lPrompt.AppendLine("```vb")
                        lPrompt.AppendLine(lCurrentTab.Editor.Text())
                        lPrompt.AppendLine("```")
                    End If
                ElseIf lSelectedText.Active Then
                    Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
                    If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                        Dim lSelText As String = lCurrentTab.Editor.GetSelectedText()
                        If Not String.IsNullOrEmpty(lSelText) Then
                            lPrompt.AppendLine()
                            lPrompt.AppendLine("Selected code:")
                            lPrompt.AppendLine("```vb")
                            lPrompt.AppendLine(lSelText)
                            lPrompt.AppendLine("```")
                        End If
                    End If
                End If
                
                ' Add task type
                If lImplementMethod.Active Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Please implement the method stub(s) with complete working code.")
                ElseIf lRefactorCode.Active Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Please refactor this code for better clarity, performance, and maintainability.")
                ElseIf lAddDocumentation.Active Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Please add comprehensive XML documentation comments to all public members.")
                ElseIf lCreateTests.Active Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Please create unit tests for this code.")
                ElseIf lFixErrors.Active Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Please fix any errors or potential issues in this code.")
                ElseIf lOptimizeCode.Active Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Please optimize this code for better performance.")
                ElseIf lCreateNewFile.Active Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Please create a complete new file with proper imports and structure.")
                End If
                
                ' Add additional instructions
                Dim lInstructions As String = lInstructionsView.Buffer.Text
                If Not String.IsNullOrEmpty(lInstructions) Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Additional instructions:")
                    lPrompt.AppendLine(lInstructions)
                End If
                
                ' Send to AI
                If pAIAssistantPanel IsNot Nothing Then
                    pAIAssistantPanel.SendMessage(lPrompt.ToString())
                End If
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"ShowGenerateCodeDialog error: {ex.Message}")
            ShowError("Generate Code Failed", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Shows AI settings - opens (or switches to) the Preferences tab, landing directly on
    ''' its AI sub-tab, same pattern as ShowGitSettings for Git > Settings...
    ''' </summary>
    ''' <remarks>
    ''' Used to open a separate modal AISettingsDialog; consolidated into the Preferences
    ''' tab's own AI page so AI settings live in exactly one place. Reinitializing the AI
    ''' client on save is now handled by ApplyAISettings, called when PreferencesTab raises
    ''' SettingsApplied (see OpenPreferencesTab), rather than here.
    ''' </remarks>
    Public Sub ShowAISettings()
        Try
            OnEditPreferences(Nothing, Nothing)
            pPreferencesTab?.SelectAITab()

        Catch ex As Exception
            Console.WriteLine($"ShowAISettings error: {ex.Message}")
            ShowError("AI Settings Error", ex.Message)
        End Try
    End Sub
    
    ' ===== AI Artifact Fields =====
    Private pAIArtifactTabs As New Dictionary(Of String, TabInfo)  ' Artifact ID -> TabInfo
    Private pComparisonTabs As New Dictionary(Of String, TabInfo)  ' Comparison ID -> TabInfo
    
    ' ===== AI Artifact Methods =====
    
    ''' <summary>
    ''' Show an AI artifact in a dedicated tab
    ''' </summary>
    Public Sub ShowAIArtifact(vArtifactId As String, vArtifactType As String, vArtifactName As String, 
                              vContent As String, Optional vTargetPath As String = "")
        Try
            ' Check if artifact is already open
            If pAIArtifactTabs.ContainsKey(vArtifactId) Then
                ' Switch to existing tab
                SwitchToTabInfo(pAIArtifactTabs(vArtifactId))
                Return
            End If
            
            ' Close welcome tab if present (directly use pNotebook - no casting needed!)
            for i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    pNotebook.RemovePage(i)
                    Exit for
                End If
            Next
            
            ' Create AI artifact editor
            Dim lArtifactEditor As New AIArtifactEditor(pSyntaxColorSet, pSettingsManager, pThemeManager, pProjectManager)
            lArtifactEditor.LoadArtifact(vArtifactId, vArtifactType, vArtifactName, vContent, vTargetPath)
            
            ' Wire up events
            AddHandler lArtifactEditor.ArtifactAccepted, AddressOf OnArtifactAccepted
            AddHandler lArtifactEditor.ArtifactRejected, AddressOf OnArtifactRejected
            AddHandler lArtifactEditor.CompareRequested, AddressOf OnArtifactCompareRequested
            
            ' Create tab info
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = $"ai-artifact:{vArtifactId}"
            lTabInfo.Editor = Nothing  ' AI artifact editor doesn't implement IEditor
            lTabInfo.EditorContainer = lArtifactEditor
            lTabInfo.TabLabel = CreateAIArtifactTabLabel(vArtifactName, vArtifactId)
            lTabInfo.Modified = False
            
            ' Add to notebook directly - no casting needed!
            Dim lPageIndex As Integer = pNotebook.AppendPage(lArtifactEditor, vArtifactName)
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Store in dictionary
            pAIArtifactTabs(vArtifactId) = lTabInfo
            
            ' Update status
            UpdateStatusBar($"AI Artifact: {vArtifactName}")
            
        Catch ex As Exception
            Console.WriteLine($"ShowAIArtifact error: {ex.Message}")
            ShowError("AI Artifact Error", "Failed to show AI artifact: " & ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Show file comparison panel for comparing two files
    ''' </summary>
    Public Sub ShowFileComparison(vLeftPath As String, vRightPath As String, Optional vComparisonId As String = "")
        Try
            ' Generate comparison ID if not provided
            If String.IsNullOrEmpty(vComparisonId) Then
                vComparisonId = $"compare_{System.IO.Path.GetFileNameWithoutExtension(vLeftPath)}_{System.IO.Path.GetFileNameWithoutExtension(vRightPath)}"
            End If
            
            ' Check if comparison is already open
            If pComparisonTabs.ContainsKey(vComparisonId) Then
                ' Switch to existing tab
                SwitchToTabInfo(pComparisonTabs(vComparisonId))
                Return
            End If
            
            ' Close welcome tab if present (directly use pNotebook - no casting needed!)
            for i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    pNotebook.RemovePage(i)
                    Exit for
                End If
            Next
            
            ' Create comparison panel
            Dim lComparisonPanel As New FileComparisonPanel(pSyntaxColorSet, pSettingsManager, pThemeManager, pProjectManager)
            lComparisonPanel.LoadFiles(vLeftPath, vRightPath)
            
            ' Wire up events
            AddHandler lComparisonPanel.FilesSwapped, AddressOf OnComparisonFilesSwapped
            AddHandler lComparisonPanel.DifferenceNavigated, AddressOf OnDifferenceNavigated
            
            ' Create tab info
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = $"comparison:{vComparisonId}"
            lTabInfo.Editor = Nothing  ' Comparison panel doesn't implement IEditor
            lTabInfo.EditorContainer = lComparisonPanel
            lTabInfo.TabLabel = CreateComparisonTabLabel(vLeftPath, vRightPath)
            lTabInfo.Modified = False
            
            Dim lLeftName As String = System.IO.Path.GetFileName(vLeftPath)
            Dim lRightName As String = System.IO.Path.GetFileName(vRightPath)
            
            ' Add to notebook directly - no casting needed!
            Dim lPageIndex As Integer = pNotebook.AppendPage(lComparisonPanel, $"{lLeftName} ⟷ {lRightName}")
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Store in dictionary
            pComparisonTabs(vComparisonId) = lTabInfo
            
            ' Update status
            UpdateStatusBar($"Comparing: {System.IO.Path.GetFileName(vLeftPath)} ⟷ {System.IO.Path.GetFileName(vRightPath)}")
            
        Catch ex As Exception
            Console.WriteLine($"ShowFileComparison error: {ex.Message}")
            ShowError("Comparison Error", "Failed to show file comparison: " & ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Show content comparison for AI artifacts vs originals
    ''' </summary>
    Public Sub ShowContentComparison(vLeftContent As String, vLeftName As String, 
                                    vRightContent As String, vRightName As String, 
                                    Optional vComparisonId As String = "")
        Try
            ' Implementation would be similar to ShowFileComparison but with content strings
            ' This is a placeholder for the content comparison functionality
            
        Catch ex As Exception
            Console.WriteLine($"ShowContentComparison error: {ex.Message}")
            ShowError("Comparison Error", "Failed to show content comparison: " & ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Create tab label for AI artifact tabs
    ''' </summary>
    Private Function CreateAIArtifactTabLabel(vName As String, vArtifactId As String) As Widget
        Try
            Dim lBox As New Box(Orientation.Horizontal, 5)
            
            ' Icon
            Dim lIcon As New Image(Stock.File, IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLabel As New Label(vName)
            lBox.PackStart(lLabel, False, False, 0)
            
            lBox.ShowAll()
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreateAIArtifactTabLabel error: {ex.Message}")
            Return New Label(vName)
        End Try
    End Function
    
    ''' <summary>
    ''' Create tab label for comparison tabs
    ''' </summary>
    Private Function CreateComparisonTabLabel(vLeftPath As String, vRightPath As String) As Widget
        Try
            Dim lBox As New Box(Orientation.Horizontal, 5)
            
            ' Icon
            Dim lIcon As New Image(Stock.File, IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLeftName As String = System.IO.Path.GetFileName(vLeftPath)
            Dim lRightName As String = System.IO.Path.GetFileName(vRightPath)
            Dim lLabel As New Label($"{lLeftName} ⟷ {lRightName}")
            lBox.PackStart(lLabel, False, False, 0)
            
            lBox.ShowAll()
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreateComparisonTabLabel error: {ex.Message}")
            Return New Label("Comparison")
        End Try
    End Function
    
    ''' <summary>
    ''' Switch to a specific TabInfo by finding its page in the notebook
    ''' </summary>
    Private Sub SwitchToTabInfo(vTabInfo As TabInfo)
        Try
            ' Directly use pNotebook - no casting needed!
            for i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is vTabInfo.EditorContainer Then
                    pNotebook.CurrentPage = i
                    Exit for
                End If
            Next
        Catch ex As Exception
            Console.WriteLine($"SwitchToTabInfo error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Close an AI artifact tab
    ''' </summary>
    Private Sub CloseAIArtifactTab(vArtifactId As String)
        Try
            If Not pAIArtifactTabs.ContainsKey(vArtifactId) Then Return
            
            Dim lTabInfo As TabInfo = pAIArtifactTabs(vArtifactId)
            
            ' Find and remove the page (directly use pNotebook - no casting needed!)
            for i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is lTabInfo.EditorContainer Then
                    pNotebook.RemovePage(i)
                    Exit for
                End If
            Next
            
            ' Remove from dictionary
            pAIArtifactTabs.Remove(vArtifactId)
            
            ' Dispose
            lTabInfo.Dispose()
            
            ' Show welcome if no tabs left
            If pNotebook.NPages = 0 Then
                ShowWelcomeTab()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"CloseAIArtifactTab error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Event Handlers =====
    
    ''' <summary>
    ''' Handle artifact acceptance - apply to target file
    ''' </summary>
    Private Sub OnArtifactAccepted(vArtifactId As String, vContent As String, vTargetPath As String)
        Try
            ' Apply the artifact to the target file
            If Not String.IsNullOrEmpty(vTargetPath) Then
                ' Check if target file is already open
                If pOpenTabs.ContainsKey(vTargetPath) Then
                    ' Update existing tab
                    Dim lTabInfo As TabInfo = pOpenTabs(vTargetPath)
                    If lTabInfo.Editor IsNot Nothing Then
                        lTabInfo.Editor.Text = vContent
                        MarkTabModified(lTabInfo.Editor)
                    End If
                Else
                    ' Create new file or overwrite existing
                    File.WriteAllText(vTargetPath, vContent)
                    OpenFile(vTargetPath)
                End If
            End If
            
            ' Close artifact tab
            CloseAIArtifactTab(vArtifactId)
            
            ' Update status
            UpdateStatusBar($"AI artifact applied to {System.IO.Path.GetFileName(vTargetPath)}")
            
        Catch ex As Exception
            Console.WriteLine($"OnArtifactAccepted error: {ex.Message}")
            ShowError("Artifact Application Failed", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle artifact rejection - close tab
    ''' </summary>
    Private Sub OnArtifactRejected(vArtifactId As String)
        Try
            CloseAIArtifactTab(vArtifactId)
            UpdateStatusBar("AI artifact rejected")
            
        Catch ex As Exception
            Console.WriteLine($"OnArtifactRejected error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle comparison request from artifact editor
    ''' </summary>
    Private Sub OnArtifactCompareRequested(vArtifactId As String, vContent As String, vTargetPath As String)
        Try
            If File.Exists(vTargetPath) Then
                ' Read original file content
                Dim lOriginalContent As String = File.ReadAllText(vTargetPath)
                
                ' Show comparison
                ShowContentComparison(lOriginalContent, System.IO.Path.GetFileName(vTargetPath),
                                    vContent, $"AI Artifact: {vArtifactId}",
                                    $"artifact_compare_{vArtifactId}")
            Else
                ShowError("File Not Found", $"Target file does not exist: {vTargetPath}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnArtifactCompareRequested error: {ex.Message}")
            ShowError("Comparison Failed", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle files swapped in comparison panel
    ''' </summary>
    Private Sub OnComparisonFilesSwapped(vLeftPath As String, vRightPath As String)
        Try
            ' Update status bar
            UpdateStatusBar($"Swapped: {System.IO.Path.GetFileName(vLeftPath)} ⟷ {System.IO.Path.GetFileName(vRightPath)}")
            
        Catch ex As Exception
            Console.WriteLine($"OnComparisonFilesSwapped error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle navigation to difference in comparison
    ''' </summary>
    Private Sub OnDifferenceNavigated(vDiffIndex As Integer, vTotalDiffs As Integer)
        Try
            UpdateStatusBar($"Difference {vDiffIndex + 1} of {vTotalDiffs}")
            
        Catch ex As Exception
            Console.WriteLine($"OnDifferenceNavigated error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle sending build errors to AI assistant
    ''' </summary>
    Private Sub OnSendBuildErrorsToAI(vErrors As String)
        Try
            If pAIAssistantPanel IsNot Nothing Then
                Dim lPrompt As New StringBuilder()
                lPrompt.AppendLine("I'm getting these build errors in my VB.NET project:")
                lPrompt.AppendLine()
                lPrompt.AppendLine("```")
                lPrompt.AppendLine(vErrors)
                lPrompt.AppendLine("```")
                lPrompt.AppendLine()
                lPrompt.AppendLine("Please help Me understand and fix these errors.")
                
                pAIAssistantPanel.SendMessage(lPrompt.ToString())
                
                ' Show AI panel
                If pBottomPanelManager IsNot Nothing Then
                    pBottomPanelManager.ShowTabByType(pBottomPanelManager.BottomPanelTab.eAIAssistant)
                End If
            Else
                ShowError("AI Not Configured", "Please configure AI settings first.")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnSendBuildErrorsToAI error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the AI menu's "Ask AI Assistant..." item - shows the AI panel, ready for the
    ''' user to type a question
    ''' </summary>
    Public Sub OnAskAIAssistant(vSender As Object, vArgs As EventArgs)
        Try
            If pAIAssistantPanel Is Nothing Then
                ShowError("AI Not Configured", "Please configure AI settings first.")
                Return
            End If

            pBottomPanelManager?.ShowTabByType(pBottomPanelManager.BottomPanelTab.eAIAssistant)

        Catch ex As Exception
            Console.WriteLine($"OnAskAIAssistant error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the AI menu's "Explain Selected Code" item - same action as the AI panel's own
    ''' Explain button
    ''' </summary>
    Public Sub OnExplainCode(vSender As Object, vArgs As EventArgs)
        Try
            If pAIAssistantPanel Is Nothing Then
                ShowError("AI Not Configured", "Please configure AI settings first.")
                Return
            End If

            pAIAssistantPanel.TriggerExplainCode()
            pBottomPanelManager?.ShowTabByType(pBottomPanelManager.BottomPanelTab.eAIAssistant)

        Catch ex As Exception
            Console.WriteLine($"OnExplainCode error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the AI menu's "Fix Build Errors" item - reuses the exact same errors-to-AI
    ''' flow as the Build Output panel's own "Send to AI" button (OnSendBuildErrorsToAI)
    ''' </summary>
    Public Sub OnFixBuildErrors(vSender As Object, vArgs As EventArgs)
        Try
            Dim lErrorsText As String = pBottomPanelManager?.BuildOutputPanel?.FormatErrorsForClipboard()
            If String.IsNullOrEmpty(lErrorsText) Then
                ShowInfo("No Build Errors", "There are no build errors or warnings to send.")
                Return
            End If

            OnSendBuildErrorsToAI(lErrorsText)

        Catch ex As Exception
            Console.WriteLine($"OnFixBuildErrors error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handle sending TODO item to AI assistant
    ''' </summary>
    Private Sub OnSendTodoToAI(vTodo As TODOItem)
        Try
            If pAIAssistantPanel IsNot Nothing Then
                Dim lPrompt As New StringBuilder()
                lPrompt.AppendLine($"I need help with this TODO item:")
                lPrompt.AppendLine($"Title: {vTodo.Title}")
                lPrompt.AppendLine($"Priority: {vTodo.GetPriorityDisplayText()}")
                lPrompt.AppendLine($"Category: {vTodo.GetCategoryDisplayText()}")
                
                If Not String.IsNullOrEmpty(vTodo.Description) Then
                    lPrompt.AppendLine("Description:")
                    lPrompt.AppendLine(vTodo.Description)
                End If
                
                If vTodo.SourceType = TODOItem.eSourceType.eCodeComment AndAlso Not String.IsNullOrEmpty(vTodo.SourceFile) Then
                    lPrompt.AppendLine()
                    lPrompt.AppendLine($"Source: {System.IO.Path.GetFileName(vTodo.SourceFile)} line {vTodo.SourceLine}")
                End If
                
                lPrompt.AppendLine()
                lPrompt.AppendLine("Please help me address this task.")
                
                pAIAssistantPanel.SendMessage(lPrompt.ToString())
                
                ' Show AI panel
                If pBottomPanelManager IsNot Nothing Then
                    pBottomPanelManager.ShowTabByType(pBottomPanelManager.BottomPanelTab.eAIAssistant)
                End If
            Else
                ShowError("AI Not Configured", "Please configure AI settings first.")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnSendTodoToAI error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles a file the AI assistant created on disk (via BottomPanelManager.AIFileCreated,
    ''' relayed from AIAssistantPanel.FileCreated) - brings the new file into view the same way
    ''' any other newly-created file would be, unless "Automatically show AI artifacts in tabs"
    ''' (AI.ShowArtifacts) is off, in which case it's just reported on the status bar
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file the AI wrote</param>
    Private Sub OnAIFileCreated(vFilePath As String)
        Try
            RefreshProjectExplorer()
            If pSettingsManager.GetBoolean("AI.ShowArtifacts", True) Then
                OpenFile(vFilePath)
            Else
                UpdateStatusBar($"AI created file: {System.IO.Path.GetFileName(vFilePath)}")
            End If
        Catch ex As Exception
            Console.WriteLine($"OnAIFileCreated error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles a file the AI assistant overwrote on disk (via BottomPanelManager.AIFileModified,
    ''' relayed from AIAssistantPanel.FileModified). If the file has an open tab, its buffer is
    ''' now stale (the write went straight to disk, bypassing the editor) so it's always reloaded
    ''' from disk regardless of the setting below - leaving it stale would be worse than showing
    ''' it. Otherwise, whether the file is opened to reveal the change or just reported on the
    ''' status bar follows "Automatically show AI artifacts in tabs" (AI.ShowArtifacts)
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file the AI overwrote</param>
    Private Sub OnAIFileModified(vFilePath As String)
        Try
            Dim lTabInfo As TabInfo = FindOpenTabForAIAction(vFilePath)
            If lTabInfo IsNot Nothing Then
                lTabInfo.Editor.SourceFileInfo.LoadContent()
            ElseIf pSettingsManager.GetBoolean("AI.ShowArtifacts", True) Then
                OpenFile(vFilePath)
            Else
                UpdateStatusBar($"AI modified file: {System.IO.Path.GetFileName(vFilePath)}")
            End If
        Catch ex As Exception
            Console.WriteLine($"OnAIFileModified error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles a project the AI assistant scaffolded on disk (via BottomPanelManager.
    ''' AIProjectCreated, relayed from AIAssistantPanel.ProjectCreated) - refreshes the explorer
    ''' so a sibling project shows up if the current project is part of a solution, without
    ''' switching away from whatever the user is currently working on the way opening it with
    ''' LoadProjectEnhanced would
    ''' </summary>
    ''' <param name="vProjectFilePath">Full path to the new project's .vbproj file</param>
    Private Sub OnAIProjectCreated(vProjectFilePath As String)
        Try
            RefreshProjectExplorer()
            UpdateStatusBar($"AI created project: {vProjectFilePath}")
        Catch ex As Exception
            Console.WriteLine($"OnAIProjectCreated error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles a file the AI assistant deleted from disk (via BottomPanelManager.AIFileDeleted,
    ''' relayed from AIAssistantPanel.FileDeleted) - closes its tab if it was open (CloseTab
    ''' still prompts to save first if the buffer had unsaved changes, which effectively offers
    ''' to recreate the file) and refreshes the explorer either way
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file the AI deleted</param>
    Private Sub OnAIFileDeleted(vFilePath As String)
        Try
            Dim lTabInfo As TabInfo = FindOpenTabForAIAction(vFilePath)
            If lTabInfo IsNot Nothing Then
                CloseTab(lTabInfo)
            End If
            RefreshProjectExplorer()
            UpdateStatusBar($"AI deleted file: {System.IO.Path.GetFileName(vFilePath)}")
        Catch ex As Exception
            Console.WriteLine($"OnAIFileDeleted error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Looks up an open tab by path for the three OnAIOpenTab* handlers below, tolerant of
    ''' formatting differences (e.g. a leading "./", or a differently-built but equivalent path)
    ''' between however the AI's FilePath was combined with the project root and however the
    ''' tab's own key in pOpenTabs was originally set by OpenFile - neither side is guaranteed
    ''' already canonical, so both are normalized via Path.GetFullPath before comparing. A silent
    ''' miss here would report a live, open, unsaved-changes-eligible file as "not open" and let
    ''' an AI action fall through to a disk write that force-reloads (or worse, silently
    ''' desyncs) that same tab. Comparison is case-sensitive, matching Linux filesystem semantics.
    ''' </summary>
    Private Function FindOpenTabForAIAction(vFilePath As String) As TabInfo
        Try
            Dim lTabInfo As TabInfo = Nothing
            If pOpenTabs.TryGetValue(vFilePath, lTabInfo) Then Return lTabInfo

            Dim lNormalizedTarget As String = System.IO.Path.GetFullPath(vFilePath)
            For Each lKvp In pOpenTabs
                If String.Equals(System.IO.Path.GetFullPath(lKvp.Key), lNormalizedTarget, StringComparison.Ordinal) Then
                    Return lKvp.Value
                End If
            Next
            Return Nothing

        Catch ex As Exception
            Console.WriteLine($"FindOpenTabForAIAction error: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' AIAssistantPanel.OpenTabLineReplaceHandler implementation (wired via BottomPanelManager.
    ''' SetOpenTabLineReplaceHandler) - if vFilePath has an open tab, verifies vExpectedContent
    ''' still matches the live buffer's real current text at vStartLine..vEndLine (refusing if
    ''' not - the user may have edited it, possibly while the AI was still generating this
    ''' response), then replaces it with vNewText through the editor's own ReplaceText, so the
    ''' change is undo-able (Ctrl+Z) and the live buffer/redraw stay correct, instead of the AI
    ''' writing straight to disk and the tab being force-reloaded
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file the AI wants to edit</param>
    ''' <param name="vStartLine">1-based inclusive first line to replace</param>
    ''' <param name="vEndLine">1-based inclusive last line to replace</param>
    ''' <param name="vExpectedContent">What the AI expects lines vStartLine-vEndLine currently contain</param>
    ''' <param name="vNewText">Text to replace the range with</param>
    Private Function OnAIOpenTabLineReplace(vFilePath As String, vStartLine As Integer, vEndLine As Integer, vExpectedContent As String, vNewText As String) As AIAssistantPanel.TabActionOutcome
        Try
            Dim lTabInfo As TabInfo = FindOpenTabForAIAction(vFilePath)
            If lTabInfo Is Nothing Then
                Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = False}
            End If

            Dim lEditor As IEditor = lTabInfo.Editor
            Dim lTextLines As List(Of String) = lEditor.SourceFileInfo.TextLines
            Dim lLineCount As Integer = lTextLines.Count

            ' The AI's line numbers are 1-based inclusive; EditorPosition/ReplaceText are 0-based
            Dim lStartLine As Integer = vStartLine - 1
            Dim lEndLine As Integer = vEndLine - 1

            If lStartLine < 0 OrElse lEndLine >= lLineCount OrElse lStartLine > lEndLine Then
                Return New AIAssistantPanel.TabActionOutcome With {
                    .WasOpen = True, .Success = False,
                    .ErrorMessage = $"Lines {vStartLine}-{vEndLine} requested, but the open file has {lLineCount} lines."
                }
            End If

            Dim lActualRange As String = String.Join(Environment.NewLine, lTextLines.GetRange(lStartLine, lEndLine - lStartLine + 1))
            If AIAssistantPanel.NormalizeForLineCompare(lActualRange) <> AIAssistantPanel.NormalizeForLineCompare(vExpectedContent) Then
                Return New AIAssistantPanel.TabActionOutcome With {
                    .WasOpen = True, .Success = False,
                    .ErrorMessage = "The file's current content there doesn't match what the AI expected (it may have changed since the AI last looked)."
                }
            End If

            Dim lEndColumn As Integer = lTextLines(lEndLine).Length
            lEditor.ReplaceText(New EditorPosition(lStartLine, 0), New EditorPosition(lEndLine, lEndColumn), vNewText)

            Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = True, .Success = True}

        Catch ex As Exception
            Console.WriteLine($"OnAIOpenTabLineReplace error: {ex.Message}")
            Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = True, .Success = False, .ErrorMessage = ex.Message}
        End Try
    End Function

    ''' <summary>
    ''' AIAssistantPanel.OpenTabWholeFileReplaceHandler implementation (wired via
    ''' BottomPanelManager.SetOpenTabWholeFileReplaceHandler) - if vFilePath has an open tab,
    ''' verifies vExpectedContent still matches the live buffer's real current full text
    ''' (refusing if not - the user may have edited it, possibly while the AI was still
    ''' generating this response), then replaces it with vNewContent through the editor's own
    ''' ReplaceAllText, so the change is undo-able (Ctrl+Z) instead of the AI writing straight
    ''' to disk and the tab being force-reloaded
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file the AI wants to modify</param>
    ''' <param name="vExpectedContent">What the AI expects the file currently contains</param>
    ''' <param name="vNewContent">Text to replace the whole file's content with</param>
    Private Function OnAIOpenTabWholeFileReplace(vFilePath As String, vExpectedContent As String, vNewContent As String) As AIAssistantPanel.TabActionOutcome
        Try
            Dim lTabInfo As TabInfo = FindOpenTabForAIAction(vFilePath)
            If lTabInfo Is Nothing Then
                Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = False}
            End If

            Dim lEditor As IEditor = lTabInfo.Editor
            Dim lActualContent As String = String.Join(Environment.NewLine, lEditor.SourceFileInfo.TextLines)

            If AIAssistantPanel.NormalizeForLineCompare(lActualContent) <> AIAssistantPanel.NormalizeForLineCompare(vExpectedContent) Then
                Return New AIAssistantPanel.TabActionOutcome With {
                    .WasOpen = True, .Success = False,
                    .ErrorMessage = "The file's current content doesn't match what the AI expected (it may have changed since the AI last looked)."
                }
            End If

            lEditor.ReplaceAllText(vNewContent)

            Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = True, .Success = True}

        Catch ex As Exception
            Console.WriteLine($"OnAIOpenTabWholeFileReplace error: {ex.Message}")
            Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = True, .Success = False, .ErrorMessage = ex.Message}
        End Try
    End Function

    ''' <summary>
    ''' AIAssistantPanel.OpenTabDeleteGuardHandler implementation (wired via BottomPanelManager.
    ''' SetOpenTabDeleteGuardHandler) - if vFilePath has an open tab with unsaved changes, refuses
    ''' rather than letting DeleteFileAsync silently discard them; otherwise closes the tab (it
    ''' won't prompt, since it's not modified) so the disk delete that follows doesn't leave a
    ''' tab open on a file that no longer exists
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file the AI wants to delete</param>
    Private Function OnAIOpenTabDeleteGuard(vFilePath As String) As AIAssistantPanel.TabActionOutcome
        Try
            Dim lTabInfo As TabInfo = FindOpenTabForAIAction(vFilePath)
            If lTabInfo Is Nothing Then
                Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = False}
            End If

            If lTabInfo.Modified Then
                Return New AIAssistantPanel.TabActionOutcome With {
                    .WasOpen = True, .Success = False,
                    .ErrorMessage = "The file is open with unsaved changes - save or discard them first."
                }
            End If

            CloseTab(lTabInfo)

            Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = True, .Success = True}

        Catch ex As Exception
            Console.WriteLine($"OnAIOpenTabDeleteGuard error: {ex.Message}")
            Return New AIAssistantPanel.TabActionOutcome With {.WasOpen = True, .Success = False, .ErrorMessage = ex.Message}
        End Try
    End Function

End Class