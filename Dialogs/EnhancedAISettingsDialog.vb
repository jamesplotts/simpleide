' EnhancedAISettingsDialog.vb
' Created: 2025-08-05 17:21:32
' Dialogs/EnhancedAISettingsDialog.vb - Enhanced settings dialog for AI integration with Mem0 support
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers
Imports SimpleIDE.AI
Imports SimpleIDE.Widgets

Namespace Dialogs
    
    ''' <summary>
    ''' Enhanced AI Settings dialog with Mem0 integration and artifact configuration
    ''' </summary>
    Public Class EnhancedAISettingsDialog
        Inherits Dialog
        
        ' ===== Private Fields =====
        Private pSettingsManager As SettingsManager
        Private pNotebook As Notebook
        
        ' Claude API tab controls
        Private pClaudeApiKeyEntry As Entry
        Private pClaudeApiKeyShowButton As ToggleButton
        Private pModelCombo As ComboBoxText
        Private pMaxTokensSpinButton As SpinButton
        Private pTemperatureSpinButton As SpinButton
        Private pStreamResponsesCheckButton As CheckButton
        Private pTestClaudeButton As Button
        Private pClaudeStatusLabel As Label
        
        ' Mem0 tab controls
        Private pMem0EnabledCheck As CheckButton
        Private pMem0ApiKeyEntry As Entry
        Private pMem0ApiKeyShowButton As ToggleButton
        Private pMem0UserIdEntry As Entry
        Private pMem0AppIdEntry As Entry
        Private pAutoStoreInteractionsCheck As CheckButton
        Private pStoreCodePatternsCheck As CheckButton
        Private pMaxMemoriesSpinButton As SpinButton
        Private pTestMem0Button As Button
        Private pMem0StatusLabel As Label
        Private pClearMem0Button As Button
        
        ' Artifact tab controls
        Private pAutoAcceptArtifactsCheck As CheckButton
        Private pPreferArtifactsCheck As CheckButton
        Private pArtifactAutoSaveCheck As CheckButton
        Private pArtifactSavePathEntry As Entry
        Private pArtifactBrowseButton As Button
        Private pShowDiffByDefaultCheck As CheckButton
        Private pMaxArtifactSizeSpinButton As SpinButton
        
        ' Context tab controls
        Private pIncludeProjectStructureCheck As CheckButton
        Private pIncludeOpenFilesCheck As CheckButton
        Private pIncludeBuildErrorsCheck As CheckButton
        Private pIncludeGitStatusCheck As CheckButton
        Private pMaxContextLinesSpinButton As SpinButton
        Private pContextTemplateTextView As TextView
        
        ' Features tab controls
        Private pAutoSuggestCheck As CheckButton
        Private pSuggestDelaySpinButton As SpinButton
        Private pInlineCodeCompletionCheck As CheckButton
        Private pErrorFixSuggestionsCheck As CheckButton
        Private pRefactoringSuggestionsCheck As CheckButton
        Private pDocGenerationCheck As CheckButton
        Private pTestGenerationCheck As CheckButton
        
        ' ===== Constructor =====
        Public Sub New(vParent As Window, vSettingsManager As SettingsManager)
            MyBase.New("AI Assistant Settings", vParent, 
                       DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                       Stock.Cancel, ResponseType.Cancel,
                       Stock.Apply, ResponseType.Apply,
                       Stock.Ok, ResponseType.Ok)
            
            pSettingsManager = vSettingsManager
            
            Try
                ' Set dialog properties
                SetDefaultSize(600, 500)
                BorderWidth = 5
                
                ' Build UI
                BuildUI()
                
                ' Load current settings
                LoadSettings()
                
                ' Show all
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"EnhancedAISettingsDialog constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== UI Building =====
        Private Sub BuildUI()
            Try
                ' Create notebook for tabs
                pNotebook = New Notebook()
                pNotebook.BorderWidth = 5
                
                ' Add tabs
                pNotebook.AppendPage(CreateClaudeApiTab(), New Label("Claude API"))
                pNotebook.AppendPage(CreateMem0Tab(), New Label("Mem0 Memory"))
                pNotebook.AppendPage(CreateArtifactTab(), New Label("Artifacts"))
                pNotebook.AppendPage(CreateContextTab(), New Label("Context"))
                pNotebook.AppendPage(CreateFeaturesTab(), New Label("Features"))
                
                ' Add to dialog
                ContentArea.PackStart(pNotebook, True, True, 0)
                
            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Claude API Tab =====
        Private Function CreateClaudeApiTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' API Key section
            Dim lApiFrame As New Frame("API Configuration")
            Dim lApiBox As New Box(Orientation.Vertical, 6)
            lApiBox.BorderWidth = 10
            
            ' API Key with show/hide button
            Dim lApiKeyBox As New Box(Orientation.Horizontal, 6)
            lApiKeyBox.PackStart(New Label("API key:"), False, False, 0)
            
            pClaudeApiKeyEntry = New Entry()
            pClaudeApiKeyEntry.Visibility = False
            pClaudeApiKeyEntry.WidthRequest = 300
            lApiKeyBox.PackStart(pClaudeApiKeyEntry, True, True, 0)
            
            pClaudeApiKeyShowButton = New ToggleButton("Show")
            AddHandler pClaudeApiKeyShowButton.Toggled, Sub()
                pClaudeApiKeyEntry.Visibility = pClaudeApiKeyShowButton.Active
                pClaudeApiKeyShowButton.Label = If(pClaudeApiKeyShowButton.Active, "Hide", "Show")
            End Sub
            lApiKeyBox.PackStart(pClaudeApiKeyShowButton, False, False, 0)
            
            pTestClaudeButton = New Button("Test")
            AddHandler pTestClaudeButton.Clicked, AddressOf OnTestClaudeClicked
            lApiKeyBox.PackStart(pTestClaudeButton, False, False, 0)
            
            lApiBox.PackStart(lApiKeyBox, False, False, 0)
            
            ' Status label
            pClaudeStatusLabel = New Label("")
            pClaudeStatusLabel.Xalign = 0
            lApiBox.PackStart(pClaudeStatusLabel, False, False, 0)
            
            ' Model selection
            Dim lModelBox As New Box(Orientation.Horizontal, 6)
            lModelBox.PackStart(New Label("Model:"), False, False, 0)
            
            pModelCombo = New ComboBoxText()
            pModelCombo.AppendText("claude-3-opus-20240229")
            pModelCombo.AppendText("claude-3-sonnet-20240229")
            pModelCombo.AppendText("claude-3-haiku-20240307")
            pModelCombo.Active = 1
            lModelBox.PackStart(pModelCombo, False, False, 0)
            
            lApiBox.PackStart(lModelBox, False, False, 0)
            
            lApiFrame.Add(lApiBox)
            lVBox.PackStart(lApiFrame, False, False, 0)
            
            ' Generation Settings
            Dim lGenFrame As New Frame("Generation Settings")
            Dim lGenBox As New Box(Orientation.Vertical, 6)
            lGenBox.BorderWidth = 10
            
            ' Max tokens
            Dim lTokensBox As New Box(Orientation.Horizontal, 6)
            lTokensBox.PackStart(New Label("Max Tokens:"), False, False, 0)
            
            pMaxTokensSpinButton = New SpinButton(100, 8192, 100)
            pMaxTokensSpinButton.Value = 4096
            lTokensBox.PackStart(pMaxTokensSpinButton, False, False, 0)
            
            lGenBox.PackStart(lTokensBox, False, False, 0)
            
            ' Temperature
            Dim lTempBox As New Box(Orientation.Horizontal, 6)
            lTempBox.PackStart(New Label("Temperature:"), False, False, 0)
            
            pTemperatureSpinButton = New SpinButton(0.0, 1.0, 0.1)
            pTemperatureSpinButton.Value = 0.7
            pTemperatureSpinButton.Digits = 1
            lTempBox.PackStart(pTemperatureSpinButton, False, False, 0)
            
            Dim lTempHelp As New Label("<small>(0.0 = focused, 1.0 = creative)</small>")
            lTempHelp.UseMarkup = True
            lTempBox.PackStart(lTempHelp, False, False, 0)
            
            lGenBox.PackStart(lTempBox, False, False, 0)
            
            ' Stream responses
            pStreamResponsesCheckButton = New CheckButton("Stream responses in real-time")
            pStreamResponsesCheckButton.Active = True
            lGenBox.PackStart(pStreamResponsesCheckButton, False, False, 0)
            
            lGenFrame.Add(lGenBox)
            lVBox.PackStart(lGenFrame, False, False, 0)
            
            Return lVBox
        End Function
        
        ' ===== Mem0 Tab =====
        Private Function CreateMem0Tab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Enable Mem0
            pMem0EnabledCheck = New CheckButton("Enable Mem0 Memory System")
            pMem0EnabledCheck.Active = False
            AddHandler pMem0EnabledCheck.Toggled, AddressOf OnMem0EnabledToggled
            lVBox.PackStart(pMem0EnabledCheck, False, False, 0)
            
            ' Mem0 Configuration
            Dim lConfigFrame As New Frame("Mem0 Configuration")
            Dim lConfigBox As New Box(Orientation.Vertical, 6)
            lConfigBox.BorderWidth = 10
            
            ' API Key
            Dim lApiKeyBox As New Box(Orientation.Horizontal, 6)
            lApiKeyBox.PackStart(New Label("Mem0 API key:"), False, False, 0)
            
            pMem0ApiKeyEntry = New Entry()
            pMem0ApiKeyEntry.Visibility = False
            pMem0ApiKeyEntry.WidthRequest = 250
            lApiKeyBox.PackStart(pMem0ApiKeyEntry, True, True, 0)
            
            pMem0ApiKeyShowButton = New ToggleButton("Show")
            AddHandler pMem0ApiKeyShowButton.Toggled, Sub()
                pMem0ApiKeyEntry.Visibility = pMem0ApiKeyShowButton.Active
                pMem0ApiKeyShowButton.Label = If(pMem0ApiKeyShowButton.Active, "Hide", "Show")
            End Sub
            lApiKeyBox.PackStart(pMem0ApiKeyShowButton, False, False, 0)
            
            pTestMem0Button = New Button("Test")
            AddHandler pTestMem0Button.Clicked, AddressOf OnTestMem0Clicked
            lApiKeyBox.PackStart(pTestMem0Button, False, False, 0)
            
            lConfigBox.PackStart(lApiKeyBox, False, False, 0)
            
            ' Status label
            pMem0StatusLabel = New Label("")
            pMem0StatusLabel.Xalign = 0
            lConfigBox.PackStart(pMem0StatusLabel, False, False, 0)
            
            ' User ID (optional)
            Dim lUserIdBox As New Box(Orientation.Horizontal, 6)
            lUserIdBox.PackStart(New Label("User Id:"), False, False, 0)
            
            pMem0UserIdEntry = New Entry()
            pMem0UserIdEntry.PlaceholderText = "Optional - defaults to system user"
            lUserIdBox.PackStart(pMem0UserIdEntry, True, True, 0)
            
            lConfigBox.PackStart(lUserIdBox, False, False, 0)
            
            ' App ID (optional)
            Dim lAppIdBox As New Box(Orientation.Horizontal, 6)
            lAppIdBox.PackStart(New Label("App Id:"), False, False, 0)
            
            pMem0AppIdEntry = New Entry()
            pMem0AppIdEntry.PlaceholderText = "Optional - defaults to VbIDE"
            lAppIdBox.PackStart(pMem0AppIdEntry, True, True, 0)
            
            lConfigBox.PackStart(lAppIdBox, False, False, 0)
            
            lConfigFrame.Add(lConfigBox)
            lVBox.PackStart(lConfigFrame, False, False, 0)
            
            ' Memory Settings
            Dim lMemFrame As New Frame("Memory Settings")
            Dim lMemBox As New Box(Orientation.Vertical, 6)
            lMemBox.BorderWidth = 10
            
            ' Auto-store interactions
            pAutoStoreInteractionsCheck = New CheckButton("Automatically store AI interactions")
            pAutoStoreInteractionsCheck.Active = True
            lMemBox.PackStart(pAutoStoreInteractionsCheck, False, False, 0)
            
            ' Store code patterns
            pStoreCodePatternsCheck = New CheckButton("Learn from code patterns")
            pStoreCodePatternsCheck.Active = True
            lMemBox.PackStart(pStoreCodePatternsCheck, False, False, 0)
            
            ' Max memories
            Dim lMaxMemBox As New Box(Orientation.Horizontal, 6)
            lMaxMemBox.PackStart(New Label("Max memories to retrieve:"), False, False, 0)
            
            pMaxMemoriesSpinButton = New SpinButton(1, 20, 1)
            pMaxMemoriesSpinButton.Value = 5
            lMaxMemBox.PackStart(pMaxMemoriesSpinButton, False, False, 0)
            
            lMemBox.PackStart(lMaxMemBox, False, False, 0)
            
            lMemFrame.Add(lMemBox)
            lVBox.PackStart(lMemFrame, False, False, 0)
            
            ' Clear memories button
            Dim lButtonBox As New Box(Orientation.Horizontal, 6)
            lButtonBox.PackStart(New Label(""), True, True, 0) ' Spacer
            
            pClearMem0Button = New Button("Clear All Memories")
            pClearMem0Button.TooltipText = "Delete all stored memories from Mem0"
            AddHandler pClearMem0Button.Clicked, AddressOf OnClearMem0Clicked
            lButtonBox.PackStart(pClearMem0Button, False, False, 0)
            
            lVBox.PackEnd(lButtonBox, False, False, 0)
            
            Return lVBox
        End Function
        
        ' ===== Artifact Tab =====
        Private Function CreateArtifactTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Artifact Behavior
            Dim lBehaviorFrame As New Frame("Artifact Behavior")
            Dim lBehaviorBox As New Box(Orientation.Vertical, 6)
            lBehaviorBox.BorderWidth = 10
            
            pAutoAcceptArtifactsCheck = New CheckButton("Auto-accept Artifacts for New files")
            pAutoAcceptArtifactsCheck.Active = False
            lBehaviorBox.PackStart(pAutoAcceptArtifactsCheck, False, False, 0)
            
            pPreferArtifactsCheck = New CheckButton("Prefer Artifacts over inline code in responses")
            pPreferArtifactsCheck.Active = True
            lBehaviorBox.PackStart(pPreferArtifactsCheck, False, False, 0)
            
            pShowDiffByDefaultCheck = New CheckButton("Show comparison view by default")
            pShowDiffByDefaultCheck.Active = True
            lBehaviorBox.PackStart(pShowDiffByDefaultCheck, False, False, 0)
            
            lBehaviorFrame.Add(lBehaviorBox)
            lVBox.PackStart(lBehaviorFrame, False, False, 0)
            
            ' Artifact Storage
            Dim lStorageFrame As New Frame("Artifact Storage")
            Dim lStorageBox As New Box(Orientation.Vertical, 6)
            lStorageBox.BorderWidth = 10
            
            pArtifactAutoSaveCheck = New CheckButton("Auto-Save Artifacts to disk")
            pArtifactAutoSaveCheck.Active = False
            AddHandler pArtifactAutoSaveCheck.Toggled, AddressOf OnArtifactAutoSaveToggled
            lStorageBox.PackStart(pArtifactAutoSaveCheck, False, False, 0)
            
            Dim lPathBox As New Box(Orientation.Horizontal, 6)
            lPathBox.PackStart(New Label("Save Path:"), False, False, 0)
            
            pArtifactSavePathEntry = New Entry()
            pArtifactSavePathEntry.Text = "~/Documents/VbIDE/Artifacts"
            pArtifactSavePathEntry.Sensitive = False
            lPathBox.PackStart(pArtifactSavePathEntry, True, True, 0)
            
            pArtifactBrowseButton = New Button("Browse...")
            pArtifactBrowseButton.Sensitive = False
            AddHandler pArtifactBrowseButton.Clicked, AddressOf OnArtifactBrowseClicked
            lPathBox.PackStart(pArtifactBrowseButton, False, False, 0)
            
            lStorageBox.PackStart(lPathBox, False, False, 0)
            
            ' Max size
            Dim lSizeBox As New Box(Orientation.Horizontal, 6)
            lSizeBox.PackStart(New Label("Max artifact size (KB):"), False, False, 0)
            
            pMaxArtifactSizeSpinButton = New SpinButton(10, 1000, 10)
            pMaxArtifactSizeSpinButton.Value = 100
            lSizeBox.PackStart(pMaxArtifactSizeSpinButton, False, False, 0)
            
            lStorageBox.PackStart(lSizeBox, False, False, 0)
            
            lStorageFrame.Add(lStorageBox)
            lVBox.PackStart(lStorageFrame, False, False, 0)
            
            Return lVBox
        End Function
        
        ' ===== Context Tab =====
        Private Function CreateContextTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Context Inclusion
            Dim lIncludeFrame As New Frame("Include in Context")
            Dim lIncludeBox As New Box(Orientation.Vertical, 6)
            lIncludeBox.BorderWidth = 10
            
            pIncludeProjectStructureCheck = New CheckButton("project structure and file list")
            pIncludeProjectStructureCheck.Active = True
            lIncludeBox.PackStart(pIncludeProjectStructureCheck, False, False, 0)
            
            pIncludeOpenFilesCheck = New CheckButton("Currently open files")
            pIncludeOpenFilesCheck.Active = False
            lIncludeBox.PackStart(pIncludeOpenFilesCheck, False, False, 0)
            
            pIncludeBuildErrorsCheck = New CheckButton("Build Errors and Warnings")
            pIncludeBuildErrorsCheck.Active = True
            lIncludeBox.PackStart(pIncludeBuildErrorsCheck, False, False, 0)
            
            pIncludeGitStatusCheck = New CheckButton("git Status and Changes")
            pIncludeGitStatusCheck.Active = False
            lIncludeBox.PackStart(pIncludeGitStatusCheck, False, False, 0)
            
            ' Max context lines
            Dim lLinesBox As New Box(Orientation.Horizontal, 6)
            lLinesBox.PackStart(New Label("Max Context lines per file:"), False, False, 0)
            
            pMaxContextLinesSpinButton = New SpinButton(50, 1000, 50)
            pMaxContextLinesSpinButton.Value = 200
            lLinesBox.PackStart(pMaxContextLinesSpinButton, False, False, 0)
            
            lIncludeBox.PackStart(lLinesBox, False, False, 0)
            
            lIncludeFrame.Add(lIncludeBox)
            lVBox.PackStart(lIncludeFrame, False, False, 0)
            
            ' Context Template
            Dim lTemplateFrame As New Frame("Context Template")
            Dim lTemplateBox As New Box(Orientation.Vertical, 6)
            lTemplateBox.BorderWidth = 10
            
            Dim lTemplateLabel As New Label("Custom Context Template (use {project}, {file}, {Errors} Placeholders):")
            lTemplateLabel.Xalign = 0
            lTemplateBox.PackStart(lTemplateLabel, False, False, 0)
            
            Dim lTemplateScroll As New ScrolledWindow()
            lTemplateScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lTemplateScroll.HeightRequest = 100
            
            pContextTemplateTextView = New TextView()
            pContextTemplateTextView.WrapMode = WrapMode.Word
            pContextTemplateTextView.Buffer.Text = "project: {project}" & Environment.NewLine & 
                                                   "current File: {file}" & Environment.NewLine & 
                                                   "Errors: {Errors}"
            
            lTemplateScroll.Add(pContextTemplateTextView)
            lTemplateBox.PackStart(lTemplateScroll, True, True, 0)
            
            lTemplateFrame.Add(lTemplateBox)
            lVBox.PackStart(lTemplateFrame, True, True, 0)
            
            Return lVBox
        End Function
        
        ' ===== Features Tab =====
        Private Function CreateFeaturesTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Auto-suggestions
            Dim lSuggestFrame As New Frame("Auto-Suggestions")
            Dim lSuggestBox As New Box(Orientation.Vertical, 6)
            lSuggestBox.BorderWidth = 10
            
            pAutoSuggestCheck = New CheckButton("Enable AI-powered auto-suggestions")
            pAutoSuggestCheck.Active = False
            AddHandler pAutoSuggestCheck.Toggled, AddressOf OnAutoSuggestToggled
            lSuggestBox.PackStart(pAutoSuggestCheck, False, False, 0)
            
            Dim lDelayBox As New Box(Orientation.Horizontal, 6)
            lDelayBox.PackStart(New Label("Suggestion delay (ms):"), False, False, 0)
            
            pSuggestDelaySpinButton = New SpinButton(100, 2000, 100)
            pSuggestDelaySpinButton.Value = 500
            pSuggestDelaySpinButton.Sensitive = False
            lDelayBox.PackStart(pSuggestDelaySpinButton, False, False, 0)
            
            lSuggestBox.PackStart(lDelayBox, False, False, 0)
            
            pInlineCodeCompletionCheck = New CheckButton("Show inline code completions")
            pInlineCodeCompletionCheck.Active = False
            pInlineCodeCompletionCheck.Sensitive = False
            lSuggestBox.PackStart(pInlineCodeCompletionCheck, False, False, 0)
            
            lSuggestFrame.Add(lSuggestBox)
            lVBox.PackStart(lSuggestFrame, False, False, 0)
            
            ' Code Assistance
            Dim lAssistFrame As New Frame("code Assistance")
            Dim lAssistBox As New Box(Orientation.Vertical, 6)
            lAssistBox.BorderWidth = 10
            
            pErrorFixSuggestionsCheck = New CheckButton("Suggest fixes for Errors")
            pErrorFixSuggestionsCheck.Active = True
            lAssistBox.PackStart(pErrorFixSuggestionsCheck, False, False, 0)
            
            pRefactoringSuggestionsCheck = New CheckButton("Suggest refactoring opportunities")
            pRefactoringSuggestionsCheck.Active = False
            lAssistBox.PackStart(pRefactoringSuggestionsCheck, False, False, 0)
            
            pDocGenerationCheck = New CheckButton("Generate documentation comments")
            pDocGenerationCheck.Active = True
            lAssistBox.PackStart(pDocGenerationCheck, False, False, 0)
            
            pTestGenerationCheck = New CheckButton("Generate unit tests")
            pTestGenerationCheck.Active = False
            lAssistBox.PackStart(pTestGenerationCheck, False, False, 0)
            
            lAssistFrame.Add(lAssistBox)
            lVBox.PackStart(lAssistFrame, False, False, 0)
            
            Return lVBox
        End Function
        
        ' ===== Settings Management =====
        Private Sub LoadSettings()
            Try
                ' Claude API settings
                pClaudeApiKeyEntry.Text = pSettingsManager.GetString("AI.Claude.ApiKey", "")
                SelectComboBoxItem(pModelCombo, pSettingsManager.GetString("AI.Claude.Model", "claude-3-sonnet-20240229"))
                pMaxTokensSpinButton.Value = pSettingsManager.GetInteger("AI.Claude.MaxTokens", 4096)
                pTemperatureSpinButton.Value = pSettingsManager.GetDouble("AI.Claude.Temperature", 0.7)
                pStreamResponsesCheckButton.Active = pSettingsManager.GetBoolean("AI.Claude.StreamResponses", True)
                
                ' Mem0 settings
                pMem0EnabledCheck.Active = pSettingsManager.GetBoolean("AI.Mem0.Enabled", False)
                pMem0ApiKeyEntry.Text = pSettingsManager.GetString("AI.Mem0.ApiKey", "")
                pMem0UserIdEntry.Text = pSettingsManager.GetString("AI.Mem0.UserId", "")
                pMem0AppIdEntry.Text = pSettingsManager.GetString("AI.Mem0.AppId", "VbIDE")
                pAutoStoreInteractionsCheck.Active = pSettingsManager.GetBoolean("AI.Mem0.AutoStore", True)
                pStoreCodePatternsCheck.Active = pSettingsManager.GetBoolean("AI.Mem0.StorePatterns", True)
                pMaxMemoriesSpinButton.Value = pSettingsManager.GetInteger("AI.Mem0.MaxMemories", 5)
                
                ' Artifact settings
                pAutoAcceptArtifactsCheck.Active = pSettingsManager.GetBoolean("AI.Artifacts.AutoAccept", False)
                pPreferArtifactsCheck.Active = pSettingsManager.GetBoolean("AI.Artifacts.Prefer", True)
                pShowDiffByDefaultCheck.Active = pSettingsManager.GetBoolean("AI.Artifacts.ShowDiff", True)
                pArtifactAutoSaveCheck.Active = pSettingsManager.GetBoolean("AI.Artifacts.AutoSave", False)
                pArtifactSavePathEntry.Text = pSettingsManager.GetString("AI.Artifacts.SavePath", "~/Documents/VbIDE/Artifacts")
                pMaxArtifactSizeSpinButton.Value = pSettingsManager.GetInteger("AI.Artifacts.MaxSize", 100)
                
                ' Context settings
                pIncludeProjectStructureCheck.Active = pSettingsManager.GetBoolean("AI.Context.ProjectStructure", True)
                pIncludeOpenFilesCheck.Active = pSettingsManager.GetBoolean("AI.Context.OpenFiles", False)
                pIncludeBuildErrorsCheck.Active = pSettingsManager.GetBoolean("AI.Context.BuildErrors", True)
                pIncludeGitStatusCheck.Active = pSettingsManager.GetBoolean("AI.Context.GitStatus", False)
                pMaxContextLinesSpinButton.Value = pSettingsManager.GetInteger("AI.Context.MaxLines", 200)
                pContextTemplateTextView.Buffer.Text = pSettingsManager.GetString("AI.Context.Template", 
                    "project: {project}" & Environment.NewLine & "current File: Environment.NewLine}" & Environment.NewLine & "Errors: {Errors}")
                
                ' Features settings
                pAutoSuggestCheck.Active = pSettingsManager.GetBoolean("AI.Features.AutoSuggest", False)
                pSuggestDelaySpinButton.Value = pSettingsManager.GetInteger("AI.Features.SuggestDelay", 500)
                pInlineCodeCompletionCheck.Active = pSettingsManager.GetBoolean("AI.Features.InlineCompletion", False)
                pErrorFixSuggestionsCheck.Active = pSettingsManager.GetBoolean("AI.Features.ErrorFix", True)
                pRefactoringSuggestionsCheck.Active = pSettingsManager.GetBoolean("AI.Features.Refactoring", False)
                pDocGenerationCheck.Active = pSettingsManager.GetBoolean("AI.Features.DocGeneration", True)
                pTestGenerationCheck.Active = pSettingsManager.GetBoolean("AI.Features.TestGeneration", False)
                
                ' Update UI state
                OnMem0EnabledToggled(Nothing, Nothing)
                OnArtifactAutoSaveToggled(Nothing, Nothing)
                OnAutoSuggestToggled(Nothing, Nothing)
                
            Catch ex As Exception
                Console.WriteLine($"LoadSettings error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SaveSettings()
            Try
                ' Claude API settings
                pSettingsManager.SetString("AI.Claude.ApiKey", pClaudeApiKeyEntry.Text.Trim())
                pSettingsManager.SetString("AI.Claude.Model", pModelCombo.ActiveText)
                pSettingsManager.SetInteger("AI.Claude.MaxTokens", CInt(pMaxTokensSpinButton.Value))
                pSettingsManager.SetDouble("AI.Claude.Temperature", pTemperatureSpinButton.Value)
                pSettingsManager.SetBoolean("AI.Claude.StreamResponses", pStreamResponsesCheckButton.Active)
                
                ' Mem0 settings
                pSettingsManager.SetBoolean("AI.Mem0.Enabled", pMem0EnabledCheck.Active)
                pSettingsManager.SetString("AI.Mem0.ApiKey", pMem0ApiKeyEntry.Text.Trim())
                pSettingsManager.SetString("AI.Mem0.UserId", pMem0UserIdEntry.Text.Trim())
                pSettingsManager.SetString("AI.Mem0.AppId", pMem0AppIdEntry.Text.Trim())
                pSettingsManager.SetBoolean("AI.Mem0.AutoStore", pAutoStoreInteractionsCheck.Active)
                pSettingsManager.SetBoolean("AI.Mem0.StorePatterns", pStoreCodePatternsCheck.Active)
                pSettingsManager.SetInteger("AI.Mem0.MaxMemories", CInt(pMaxMemoriesSpinButton.Value))
                
                ' Artifact settings
                pSettingsManager.SetBoolean("AI.Artifacts.AutoAccept", pAutoAcceptArtifactsCheck.Active)
                pSettingsManager.SetBoolean("AI.Artifacts.Prefer", pPreferArtifactsCheck.Active)
                pSettingsManager.SetBoolean("AI.Artifacts.ShowDiff", pShowDiffByDefaultCheck.Active)
                pSettingsManager.SetBoolean("AI.Artifacts.AutoSave", pArtifactAutoSaveCheck.Active)
                pSettingsManager.SetString("AI.Artifacts.SavePath", pArtifactSavePathEntry.Text.Trim())
                pSettingsManager.SetInteger("AI.Artifacts.MaxSize", CInt(pMaxArtifactSizeSpinButton.Value))
                
                ' Context settings
                pSettingsManager.SetBoolean("AI.Context.ProjectStructure", pIncludeProjectStructureCheck.Active)
                pSettingsManager.SetBoolean("AI.Context.OpenFiles", pIncludeOpenFilesCheck.Active)
                pSettingsManager.SetBoolean("AI.Context.BuildErrors", pIncludeBuildErrorsCheck.Active)
                pSettingsManager.SetBoolean("AI.Context.GitStatus", pIncludeGitStatusCheck.Active)
                pSettingsManager.SetInteger("AI.Context.MaxLines", CInt(pMaxContextLinesSpinButton.Value))
                pSettingsManager.SetString("AI.Context.Template", pContextTemplateTextView.Buffer.Text)
                
                ' Features settings
                pSettingsManager.SetBoolean("AI.Features.AutoSuggest", pAutoSuggestCheck.Active)
                pSettingsManager.SetInteger("AI.Features.SuggestDelay", CInt(pSuggestDelaySpinButton.Value))
                pSettingsManager.SetBoolean("AI.Features.InlineCompletion", pInlineCodeCompletionCheck.Active)
                pSettingsManager.SetBoolean("AI.Features.ErrorFix", pErrorFixSuggestionsCheck.Active)
                pSettingsManager.SetBoolean("AI.Features.Refactoring", pRefactoringSuggestionsCheck.Active)
                pSettingsManager.SetBoolean("AI.Features.DocGeneration", pDocGenerationCheck.Active)
                pSettingsManager.SetBoolean("AI.Features.TestGeneration", pTestGenerationCheck.Active)
                
                ' Save to disk
                pSettingsManager.SaveSettings()
                
            Catch ex As Exception
                Console.WriteLine($"SaveSettings error: {ex.Message}")
                Throw
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        Protected Overrides Sub OnResponse(vResponseId As ResponseType)
            Try
                If vResponseId = ResponseType.Ok OrElse vResponseId = ResponseType.Apply Then
                    SaveSettings()
                End If
                
                If vResponseId = ResponseType.Ok OrElse vResponseId = ResponseType.Cancel Then
                    MyBase.OnResponse(vResponseId)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnResponse error: {ex.Message}")
                ShowError("Save error", "Failed to Save settings: " & ex.Message)
            End Try
        End Sub
        
        Private Sub OnMem0EnabledToggled(vSender As Object, vArgs As EventArgs)
            Dim lEnabled As Boolean = pMem0EnabledCheck.Active
            pMem0ApiKeyEntry.Sensitive = lEnabled
            pMem0ApiKeyShowButton.Sensitive = lEnabled
            pMem0UserIdEntry.Sensitive = lEnabled
            pMem0AppIdEntry.Sensitive = lEnabled
            pAutoStoreInteractionsCheck.Sensitive = lEnabled
            pStoreCodePatternsCheck.Sensitive = lEnabled
            pMaxMemoriesSpinButton.Sensitive = lEnabled
            pTestMem0Button.Sensitive = lEnabled
            pClearMem0Button.Sensitive = lEnabled
        End Sub
        
        Private Sub OnArtifactAutoSaveToggled(vSender As Object, vArgs As EventArgs)
            Dim lEnabled As Boolean = pArtifactAutoSaveCheck.Active
            pArtifactSavePathEntry.Sensitive = lEnabled
            pArtifactBrowseButton.Sensitive = lEnabled
        End Sub
        
        Private Sub OnAutoSuggestToggled(vSender As Object, vArgs As EventArgs)
            Dim lEnabled As Boolean = pAutoSuggestCheck.Active
            pSuggestDelaySpinButton.Sensitive = lEnabled
            pInlineCodeCompletionCheck.Sensitive = lEnabled
        End Sub
        
        Private Async Sub OnTestClaudeClicked(vSender As Object, vArgs As EventArgs)
            Try
                pClaudeStatusLabel.Text = "Testing connection..."
                pTestClaudeButton.Sensitive = False
                
                ' Test the API key
                Dim lApiKey As String = pClaudeApiKeyEntry.Text.Trim()
                If String.IsNullOrEmpty(lApiKey) Then
                    pClaudeStatusLabel.Markup = "<span Color='red'>API key is required</span>"
                    Return
                End If
                
                ' Create temporary client
                Dim lClient As New EnhancedClaudeApiClient(lApiKey)
                
                ' Send test message
                Dim lResponse As EnhancedClaudeApiClient.ClaudeResponse = 
                    Await lClient.SendMessageWithArtifactsAsync("Hello, please respond with 'Connection successful!'", 
                                                               New List(Of ImprovedAIAssistantPanel.ChatMessage))
                
                If Not String.IsNullOrEmpty(lResponse.Content) Then
                    pClaudeStatusLabel.Markup = "<span Color='green'>✓ Connection successful</span>"
                Else
                    pClaudeStatusLabel.Markup = "<span Color='red'>Connection failed</span>"
                End If
                
            Catch ex As Exception
                pClaudeStatusLabel.Markup = $"<span Color='red'>error: {ex.Message}</span>"
            Finally
                pTestClaudeButton.Sensitive = True
            End Try
        End Sub
        
        Private Async Sub OnTestMem0Clicked(vSender As Object, vArgs As EventArgs)
            Try
                pMem0StatusLabel.Text = "Testing connection..."
                pTestMem0Button.Sensitive = False
                
                ' Test the API key
                Dim lApiKey As String = pMem0ApiKeyEntry.Text.Trim()
                If String.IsNullOrEmpty(lApiKey) Then
                    pMem0StatusLabel.Markup = "<span Color='red'>API key is required</span>"
                    Return
                End If
                
                ' Create temporary client
                Dim lClient As New Mem0Client(lApiKey)
                
                ' Test by storing and retrieving a test memory
                Dim lTestKey As String = $"test_{DateTime.Now.Ticks}"
                Dim lTestValue As String = "VbIDE connection Test"
                
                Dim lStoreResult As Boolean = Await lClient.StoreMemoryAsync(lTestKey, lTestValue)
                
                If lStoreResult Then
                    ' Try to retrieve it
                    Dim lRetrievedValue As String = Await lClient.RetrieveMemoryAsync(lTestKey)
                    
                    If lRetrievedValue = lTestValue Then
                        pMem0StatusLabel.Markup = "<span Color='green'>✓ Connection successful</span>"
                        
                        ' Clean up test memory
                        Await lClient.DeleteMemoryAsync(lTestKey)
                    Else
                        pMem0StatusLabel.Markup = "<span Color='orange'>Connection works but retrieval failed</span>"
                    End If
                Else
                    pMem0StatusLabel.Markup = "<span Color='red'>Failed to store Test Memory</span>"
                End If
                
            Catch ex As Exception
                pMem0StatusLabel.Markup = $"<span Color='red'>error: {ex.Message}</span>"
            Finally
                pTestMem0Button.Sensitive = True
            End Try
        End Sub
        
        Private Async Sub OnClearMem0Clicked(vSender As Object, vArgs As EventArgs)
            Try
                Await Task.Delay(1)  ' Add a minimal await
                ' Confirm dialog
                Dim lDialog As New MessageDialog(Me, 
                                               DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                               MessageType.Warning,
                                               ButtonsType.YesNo,
                                               "Are you sure you want to Clear all Mem0 memories? this cannot be undone.")
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    pClearMem0Button.Sensitive = False
                    pMem0StatusLabel.Text = "Clearing memories..."
                    
                    ' Create client and clear memories
                    Dim lApiKey As String = pMem0ApiKeyEntry.Text.Trim()
                    If Not String.IsNullOrEmpty(lApiKey) Then
                        Dim lClient As New Mem0Client(lApiKey)
                        ' Note: This would need to be implemented in Mem0Client
                        ' For now, we'll show a message
                        pMem0StatusLabel.Markup = "<span Color='orange'>Clear function not yet implemented</span>"
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                pMem0StatusLabel.Markup = $"<span Color='red'>error: {ex.Message}</span>"
            Finally
                pClearMem0Button.Sensitive = True
            End Try
        End Sub
        
        Private Sub OnArtifactBrowseClicked(vSender As Object, vArgs As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog("Select Artifact Save Directory",
                                                    Me,
                                                    FileChooserAction.SelectFolder,
                                                    Stock.Cancel, ResponseType.Cancel,
                                                    Stock.Open, ResponseType.Accept)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    pArtifactSavePathEntry.Text = lDialog.FileName
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnArtifactBrowseClicked error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        Private Sub SelectComboBoxItem(vCombo As ComboBoxText, vText As String)
            Try
                Dim lModel As ITreeModel = vCombo.Model
                Dim lIter As TreeIter
                
                If lModel.GetIterFirst(lIter) Then
                    Do
                        Dim lValue As String = lModel.GetValue(lIter, 0).ToString()
                        If lValue = vText Then
                            vCombo.SetActiveIter(lIter)
                            Return
                        End If
                    Loop While lModel.IterNext(lIter)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectComboBoxItem error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ShowError(vTitle As String, vMessage As String)
            Dim lDialog As New MessageDialog(Me,
                                           DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                           MessageType.Error,
                                           ButtonsType.Ok,
                                           vMessage)
            lDialog.Title = vTitle
            lDialog.Run()
            lDialog.Destroy()
        End Sub
        
    End Class
    
End Namespace
