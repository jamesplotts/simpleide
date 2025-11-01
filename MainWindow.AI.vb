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

Partial Public Class MainWindow
    
    ' ===== Private Fields =====
    Private pClaudeClient As ClaudeApiClient
    Private pAIFileSystemBridge As AIFileSystemBridge
    Private pIsAIProcessing As Boolean = False
    
    ' ===== AI Integration Methods =====
    
    ''' <summary>
    ''' Initialize AI components with API key from settings
    ''' </summary>
    Private Sub InitializeAI()
        Try
            ' Get API key from settings
            Dim lApiKey As String = pSettingsManager.GetString("AI.ApiKey", "")
            
            If Not String.IsNullOrEmpty(lApiKey) Then
                pClaudeClient = New ClaudeApiClient(lApiKey)
                pAIFileSystemBridge = New AIFileSystemBridge()
                
                ' Initialize AI Assistant panel if not already done
                If pAIAssistantPanel IsNot Nothing Then
                    pAIAssistantPanel.Initialize(lApiKey)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializeAI error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Update project knowledge base for AI context
    ''' </summary>
    Public Sub UpdateProjectKnowledge()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before updating AI knowledge.")
                Return
            End If
            
            ' Placeholder for knowledge base update logic
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
                                    DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                    Stock.Cancel, ResponseType.Cancel,
                                    Stock.Ok, ResponseType.Ok)
            
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
    ''' Show AI settings dialog for configuration
    ''' </summary>
    Public Sub ShowAISettings()
        Try
            Dim lDialog As New AISettingsDialog(Me, pSettingsManager)
            
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                ' Settings saved, reinitialize AI
                InitializeAI()
                
                ' Update status
                UpdateStatusBar("AI settings updated")
            End If
            
            lDialog.Destroy()
            
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
    
End Class