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
    
    ' Initialize AI components
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
    
    ' Update project knowledge base
    Public Sub UpdateProjectKnowledge()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before updating AI knowledge.")
                Return
            End If
            
            ' Show progress dialog
            Dim lProgressDialog As New Dialog("Updating project Knowledge", Me, 
                                            DialogFlags.Modal Or DialogFlags.DestroyWithParent)
            lProgressDialog.SetDefaultSize(400, 150)
            
            Dim lVBox As New Box(Orientation.Vertical, 12)
            lVBox.BorderWidth = 12
            
            Dim lLabel As New Label("Scanning project files And updating AI knowledge base...")
            lVBox.PackStart(lLabel, False, False, 0)
            
            Dim lProgressBar As New ProgressBar()
            lProgressBar.PulseStep = 0.1
            lVBox.PackStart(lProgressBar, False, False, 0)
            
            lProgressDialog.ContentArea.Add(lVBox)
            lProgressDialog.ShowAll()
            
            ' Start async update
            Task.Run(Async Function()
                Try
                    ' Get project directory
                    Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
                    Dim lFiles As New List(Of String)
                    
                    ' Collect all VB files
                    CollectProjectFiles(lProjectDir, lFiles)
                    
                    ' Create knowledge document
                    Dim lKnowledgeBuilder As New StringBuilder()
                    lKnowledgeBuilder.AppendLine($"# project: {System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)}")
                    lKnowledgeBuilder.AppendLine($"Location: {lProjectDir}")
                    lKnowledgeBuilder.AppendLine($"Generated: {DateTime.Now}")
                    lKnowledgeBuilder.AppendLine()
                    lKnowledgeBuilder.AppendLine("## project Structure")
                    lKnowledgeBuilder.AppendLine()
                    
                    ' Add file structure
                    For Each lFile In lFiles
                        Dim lRelativePath As String = lFile.Replace(lProjectDir & System.IO.Path.DirectorySeparatorChar, "")
                        lKnowledgeBuilder.AppendLine($"- {lRelativePath}")
                    Next
                    
                    lKnowledgeBuilder.AppendLine()
                    lKnowledgeBuilder.AppendLine("## code Analysis")
                    lKnowledgeBuilder.AppendLine()
                    
                    ' Analyze each file
                    Dim lFileCount As Integer = 0
                    For Each lFile In lFiles
                        lFileCount += 1
                        
                        ' Update progress
                        Application.Invoke(Sub()
                            lProgressBar.Fraction = lFileCount / lFiles.Count
                            lLabel.Text = $"Analyzing: {System.IO.Path.GetFileName(lFile)}"
                        End Sub)
                        
                        ' Read and analyze file
                        Dim lContent As String = Await System.IO.File.ReadAllTextAsync(lFile)
                        Dim lRelativePath As String = lFile.Replace(lProjectDir & System.IO.Path.DirectorySeparatorChar, "")
                        
                        lKnowledgeBuilder.AppendLine($"### {lRelativePath}")
                        lKnowledgeBuilder.AppendLine()
                        
                        ' Extract key information
                        Dim lAnalysis As String = AnalyzeVBFile(lContent)
                        lKnowledgeBuilder.AppendLine(lAnalysis)
                        lKnowledgeBuilder.AppendLine()
                    Next
                    
                    ' Save knowledge file
                    Dim lKnowledgeFile As String = System.IO.Path.Combine(lProjectDir, ".ai-knowledge.md")
                    Await System.IO.File.WriteAllTextAsync(lKnowledgeFile, lKnowledgeBuilder.ToString())
                    
                    ' Update AI context if available
                    If pAIAssistantPanel IsNot Nothing Then
                        Application.Invoke(Sub()
                            pAIAssistantPanel.UpdateProjectContext(lKnowledgeBuilder)
                        End Sub)
                    End If
                    
                    ' Close dialog
                    Application.Invoke(Sub()
                        lProgressDialog.Destroy()
                        ShowInfo("Knowledge updated", $"project knowledge base updated successfully.{Environment.NewLine}Analyzed {lFiles.Count} files.")
                    End Sub)
                    
                Catch ex As Exception
                    Application.Invoke(Sub()
                        lProgressDialog.Destroy()
                        ShowError("Update Failed", $"Failed to update project knowledge: {ex.Message}")
                    End Sub)
                End Try
                
                Return True
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateProjectKnowledge error: {ex.Message}")
            ShowError("Update Failed", ex.Message)
        End Try
    End Sub
    
    ' Collect all VB files in project
    Private Sub CollectProjectFiles(vDirectory As String, vFiles As List(Of String))
        Try
            ' Add VB files
            vFiles.AddRange(Directory.GetFiles(vDirectory, "*.vb", SearchOption.AllDirectories))
            
            ' Skip certain directories
            Dim lSkipDirs As String() = {"bin", "obj", ".git", ".vs", "Packages"}
            
            For Each lSubDir In Directory.GetDirectories(vDirectory)
                Dim lDirName As String = System.IO.Path.GetFileName(lSubDir)
                If Not lSkipDirs.Contains(lDirName.ToLower()) Then
                    CollectProjectFiles(lSubDir, vFiles)
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"CollectProjectFiles error: {ex.Message}")
        End Try
    End Sub
    
    ' Analyze VB file content
    Private Function AnalyzeVBFile(vContent As String) As String
        Try
            Dim lAnalysis As New StringBuilder()
            
            ' Extract imports
            Dim lImports As New List(Of String)
            For Each lLine In vContent.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                Dim lTrimmed As String = lLine.Trim()
                If lTrimmed.StartsWith("Imports ", StringComparison.OrdinalIgnoreCase) Then
                    lImports.Add(lTrimmed.Substring(8))
                End If
            Next
            
            If lImports.Count > 0 Then
                lAnalysis.AppendLine("**Imports:**")
                For Each lImport In lImports
                    lAnalysis.AppendLine($"- {lImport}")
                Next
                lAnalysis.AppendLine()
            End If
            
            ' Extract classes, modules, interfaces
            Dim lTypes As New List(Of String)
            Dim lMembers As New List(Of String)
            
            ' Simple pattern matching for major constructs
            Dim lLines As String() = vContent.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
            For Each lLine In lLines
                Dim lTrimmed As String = lLine.Trim()
                
                ' Classes
                If lTrimmed.Contains("Class ") AndAlso Not lTrimmed.StartsWith("'") Then
                    Dim lMatch = System.Text.RegularExpressions.Regex.Match(lTrimmed, "(?:Public |Private |Friend |Protected )?(?:Partial )?Class\s+(\w+)")
                    If lMatch.Success Then
                        lTypes.Add($"Class: {lMatch.Groups(1).Value}")
                    End If
                End If
                
                ' Modules
                If lTrimmed.Contains("Module ") AndAlso Not lTrimmed.StartsWith("'") Then
                    Dim lMatch = System.Text.RegularExpressions.Regex.Match(lTrimmed, "(?:Public |Private |Friend )?Module\s+(\w+)")
                    If lMatch.Success Then
                        lTypes.Add($"Module: {lMatch.Groups(1).Value}")
                    End If
                End If
                
                ' Methods
                If (lTrimmed.Contains("Sub ") OrElse lTrimmed.Contains("Function ")) AndAlso Not lTrimmed.StartsWith("'") Then
                    Dim lMatch = System.Text.RegularExpressions.Regex.Match(lTrimmed, "(?:Public |Private |Protected |Friend )?(?:Shared )?(?:Sub|Function)\s+(\w+)")
                    If lMatch.Success Then
                        lMembers.Add(lMatch.Groups(1).Value)
                    End If
                End If
            Next
            
            If lTypes.Count > 0 Then
                lAnalysis.AppendLine("**Types:**")
                For Each lType In lTypes
                    lAnalysis.AppendLine($"- {lType}")
                Next
                lAnalysis.AppendLine()
            End If
            
            If lMembers.Count > 0 Then
                lAnalysis.AppendLine("**key members:**")
                For Each lMember In lMembers.Take(10) ' Limit to first 10
                    lAnalysis.AppendLine($"- {lMember}")
                Next
                If lMembers.Count > 10 Then
                    lAnalysis.AppendLine($"- ... and {lMembers.Count - 10} more")
                End If
            End If
            
            Return lAnalysis.ToString()
            
        Catch ex As Exception
            Console.WriteLine($"AnalyzeVBFile error: {ex.Message}")
            Return "error analyzing file"
        End Try
    End Function
    
    ' Explain selected code
    Public Sub ExplainSelectedCode()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab?.Editor Is Nothing Then
                ShowError("No code Selected", "Please select some code to explain.")
                Return
            End If
            
            ' Get selected text
            Dim lSelectedText As String = ""
            If TypeOf lCurrentTab.Editor Is IEditor Then
                Dim lEditor As IEditor = DirectCast(lCurrentTab.Editor, IEditor)
                lSelectedText = lEditor.SelectedText()
            End If
            
            If String.IsNullOrWhiteSpace(lSelectedText) Then
                ShowError("No code Selected", "Please select some code to explain.")
                Return
            End If
            
            ' Show AI Assistant panel
            ShowBottomPanel(4) ' AI Assistant tab
            
            ' Send request to explain code
            If pAIAssistantPanel IsNot Nothing Then
                Dim lPrompt As String = $"Please explain the following VB.NET code:{Environment.NewLine}{Environment.NewLine}```vb{Environment.NewLine}{lSelectedText}{Environment.NewLine}```"
                pAIAssistantPanel.SendMessage(lPrompt)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ExplainSelectedCode error: {ex.Message}")
            ShowError("Explain Failed", ex.Message)
        End Try
    End Sub
    
    ' Fix build errors using AI
    Public Sub FixBuildErrors()
        Try
            ' Get build errors from error list
            Dim lErrors As New List(Of String)
            
            If pErrorListView IsNot Nothing AndAlso pErrorListView.Model IsNot Nothing Then
                Dim lModel As TreeStore = DirectCast(pErrorListView.Model, TreeStore)
                Dim lIter As TreeIter
                
                If lModel.GetIterFirst(lIter) Then
                    Do
                        Dim lSeverity As String = DirectCast(lModel.GetValue(lIter, 0), String)
                        Dim lDescription As String = DirectCast(lModel.GetValue(lIter, 1), String)
                        Dim lFile As String = DirectCast(lModel.GetValue(lIter, 2), String)
                        Dim lLine As String = DirectCast(lModel.GetValue(lIter, 3), String)
                        
                        If lSeverity = "error" Then
                            lErrors.Add($"{lFile}({lLine}): {lDescription}")
                        End If
                    Loop While lModel.IterNext(lIter)
                End If
            End If
            
            If lErrors.Count = 0 Then
                ShowInfo("No Errors", "No build Errors to fix.")
                Return
            End If
            
            ' Show AI Assistant panel
            ShowBottomPanel(4)
            
            ' Build prompt
            Dim lPrompt As New StringBuilder()
            lPrompt.AppendLine("i have the following build Errors in my VB.NET project:")
            lPrompt.AppendLine()
            
            For Each lError In lErrors
                lPrompt.AppendLine($"- {lError}")
            Next
            
            lPrompt.AppendLine()
            lPrompt.AppendLine("Please help me fix these Errors. Provide the corrected code and explain what was wrong.")
            
            ' Send to AI
            If pAIAssistantPanel IsNot Nothing Then
                pAIAssistantPanel.SendMessage(lPrompt.ToString())
            End If
            
        Catch ex As Exception
            Console.WriteLine($"FixBuildErrors error: {ex.Message}")
            ShowError("Fix Errors Failed", ex.Message)
        End Try
    End Sub
    
    ' Show generate code dialog
    Public Sub ShowGenerateCodeDialog()
        Try
            Dim lDialog As New Dialog("Generate code with AI", Me, 
                                    DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                    Stock.Cancel, ResponseType.Cancel,
                                    Stock.Ok, ResponseType.Ok)
            
            lDialog.SetDefaultSize(600, 400)
            
            Dim lVBox As New Box(Orientation.Vertical, 12)
            lVBox.BorderWidth = 12
            
            ' Instructions
            Dim lLabel As New Label("Describe what code you want to generate:")
            lLabel.Xalign = 0
            lVBox.PackStart(lLabel, False, False, 0)
            
            ' Text view for description
            Dim lScrolledWindow As New ScrolledWindow()
            lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            Dim lTextView As New TextView()
            lTextView.WrapMode = WrapMode.Word
            lTextView.Buffer.Text = "Create a "
            
            lScrolledWindow.Add(lTextView)
            lVBox.PackStart(lScrolledWindow, True, True, 0)
            
            ' Options
            Dim lOptionsBox As New Box(Orientation.Horizontal, 6)
            
            Dim lAddToCurrentFile As New CheckButton("Add to current file")
            lAddToCurrentFile.Active = True
            lOptionsBox.PackStart(lAddToCurrentFile, False, False, 0)
            
            Dim lCreateNewFile As New CheckButton("Create New file")
            lOptionsBox.PackStart(lCreateNewFile, False, False, 0)
            
            lVBox.PackStart(lOptionsBox, False, False, 0)
            
            lDialog.ContentArea.Add(lVBox)
            lDialog.ShowAll()
            
            ' Handle response
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                Dim lDescription As String = lTextView.Buffer.Text.Trim()
                
                If Not String.IsNullOrEmpty(lDescription) Then
                    ' Show AI Assistant
                    ShowBottomPanel(4)
                    
                    ' Build prompt
                    Dim lPrompt As New StringBuilder()
                    lPrompt.AppendLine("Please generate VB.NET code based on this Description:")
                    lPrompt.AppendLine()
                    lPrompt.AppendLine(lDescription)
                    lPrompt.AppendLine()
                    lPrompt.AppendLine("Follow these coding conventions:")
                    lPrompt.AppendLine("- Use Hungarian notation (l=Local, p=Private, v=Parameter)")
                    lPrompt.AppendLine("- Enums start with eUnspecified and end with eLastValue")
                    lPrompt.AppendLine("- Use Try-Catch blocks for error handling")
                    lPrompt.AppendLine("- Add appropriate comments")
                    
                    If lAddToCurrentFile.Active Then
                        lPrompt.AppendLine()
                        lPrompt.AppendLine("the code should be designed to integrate with an existing file.")
                    ElseIf lCreateNewFile.Active Then
                        lPrompt.AppendLine()
                        lPrompt.AppendLine("Please create a complete New file with proper imports and structure.")
                    End If
                    
                    ' Send to AI
                    If pAIAssistantPanel IsNot Nothing Then
                        pAIAssistantPanel.SendMessage(lPrompt.ToString())
                    End If
                End If
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"ShowGenerateCodeDialog error: {ex.Message}")
            ShowError("Generate code Failed", ex.Message)
        End Try
    End Sub
    
    ' Show AI settings dialog
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
            ShowError("AI Settings error", ex.Message)
        End Try
    End Sub

    
    ' ===== AI Artifact Fields =====
    Private pAIArtifactTabs As New Dictionary(Of String, TabInfo)  ' Artifact Id -> TabInfo
    Private pComparisonTabs As New Dictionary(Of String, TabInfo)  ' Comparison Id -> TabInfo
    
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
            
            ' Close welcome tab if present
            For i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    pNotebook.RemovePage(i)
                    Exit For
                End If
            Next
            
            ' Create AI artifact editor
            Dim lArtifactEditor As New AIArtifactEditor(pSyntaxColorSet, pSettingsManager,pProjectManager)
            lArtifactEditor.LoadArtifact(vArtifactId, vArtifactType, vArtifactName, vContent, vTargetPath)
            
            ' Wire up events
            AddHandler lArtifactEditor.ArtifactAccepted, AddressOf OnArtifactAccepted
            AddHandler lArtifactEditor.ArtifactRejected, AddressOf OnArtifactRejected
            AddHandler lArtifactEditor.CompareRequested, AddressOf OnArtifactCompareRequested
            
            ' Create tab info
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = $"ai-artifact:{vArtifactId}"
            lTabInfo.Editor = Nothing  ' AI artifact Editor doesn't implement IEditor
            lTabInfo.EditorContainer = lArtifactEditor
            lTabInfo.TabLabel = CreateAIArtifactTabLabel(vArtifactName, vArtifactId)
            lTabInfo.Modified = False
            
            ' Add to notebook
            Dim lPageIndex As Integer = pNotebook.AppendPage(lArtifactEditor, lTabInfo.TabLabel)
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Store in dictionary
            pAIArtifactTabs(vArtifactId) = lTabInfo
            
            ' Update status
            UpdateStatusBar($"AI Artifact: {vArtifactName}")
            
        Catch ex As Exception
            Console.WriteLine($"ShowAIArtifact error: {ex.Message}")
            ShowError("AI Artifact error", "Failed to Show AI artifact: " & ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Show file comparison panel
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
            
            ' Close welcome tab if present
            For i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    pNotebook.RemovePage(i)
                    Exit For
                End If
            Next
            
            ' Create comparison panel
            Dim lComparisonPanel As New FileComparisonPanel(pSyntaxColorSet, pSettingsManager,pProjectManager)
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
            
            ' Add to notebook
            Dim lPageIndex As Integer = pNotebook.AppendPage(lComparisonPanel, lTabInfo.TabLabel)
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Store in dictionary
            pComparisonTabs(vComparisonId) = lTabInfo
            
            ' Update status
            UpdateStatusBar($"Comparing: {System.IO.Path.GetFileName(vLeftPath)} ⟷ {System.IO.Path.GetFileName(vRightPath)}")
            
        Catch ex As Exception
            Console.WriteLine($"ShowFileComparison error: {ex.Message}")
            ShowError("Comparison error", "Failed to Show file comparison: " & ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Show content comparison (for AI artifacts vs originals)
    ''' </summary>
    Public Sub ShowContentComparison(vLeftContent As String, vLeftName As String, 
                                    vRightContent As String, vRightName As String, 
                                    Optional vComparisonId As String = "")
        Try
            ' Generate comparison ID if not provided
            If String.IsNullOrEmpty(vComparisonId) Then
                vComparisonId = $"compare_content_{DateTime.Now.Ticks}"
            End If
            
            ' Check if comparison is already open
            If pComparisonTabs.ContainsKey(vComparisonId) Then
                ' Update existing comparison
                Dim lExistingPanel As FileComparisonPanel = CType(pComparisonTabs(vComparisonId).EditorContainer, FileComparisonPanel)
                lExistingPanel.LoadContent(vLeftContent, vLeftName, vRightContent, vRightName)
                SwitchToTabInfo(pComparisonTabs(vComparisonId))
                Return
            End If
            
            ' Create comparison panel
            Dim lComparisonPanel As New FileComparisonPanel(pSyntaxColorSet, pSettingsManager, pProjectManager)
            lComparisonPanel.LoadContent(vLeftContent, vLeftName, vRightContent, vRightName)
            
            ' Allow editing in the comparison view for AI artifacts
            lComparisonPanel.SetReadOnly(False, False)
            
            ' Wire up events
            AddHandler lComparisonPanel.FilesSwapped, AddressOf OnComparisonFilesSwapped
            AddHandler lComparisonPanel.DifferenceNavigated, AddressOf OnDifferenceNavigated
            
            ' Create tab info
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = $"comparison:{vComparisonId}"
            lTabInfo.Editor = Nothing
            lTabInfo.EditorContainer = lComparisonPanel
            lTabInfo.TabLabel = CreateComparisonTabLabel(vLeftName, vRightName, True)
            lTabInfo.Modified = False
            
            ' Add to notebook
            Dim lPageIndex As Integer = pNotebook.AppendPage(lComparisonPanel, lTabInfo.TabLabel)
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Store in dictionary
            pComparisonTabs(vComparisonId) = lTabInfo
            
            ' Update status
            UpdateStatusBar($"Comparing: {vLeftName} ⟷ {vRightName}")
            
        Catch ex As Exception
            Console.WriteLine($"ShowContentComparison error: {ex.Message}")
            ShowError("Comparison error", "Failed to Show Content comparison: " & ex.Message)
        End Try
    End Sub
    
    ' ===== Private Helper Methods =====
    
    Private Function CreateAIArtifactTabLabel(vArtifactName As String, vArtifactId As String) As Widget
        Try
            Dim lBox As New Box(Orientation.Horizontal, 5)
            
            ' AI icon
            Dim lIcon As New Image()
            lIcon.SetFromIconName("applications-science", IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLabel As New Label($"AI: {vArtifactName}")
            lLabel.TooltipText = $"AI Artifact: {vArtifactId}"
            lBox.PackStart(lLabel, False, False, 0)
            
            ' Close button
            Dim lCloseButton As New Button()
            lCloseButton.Relief = ReliefStyle.None
            lCloseButton.Add(New Image(Stock.Close, IconSize.Menu))
            AddHandler lCloseButton.Clicked, Sub() CloseAIArtifactTab(vArtifactId)
            lBox.PackEnd(lCloseButton, False, False, 0)
            
            lBox.ShowAll()
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreateAIArtifactTabLabel error: {ex.Message}")
            Return New Label(vArtifactName)
        End Try
    End Function
    
    Private Function CreateComparisonTabLabel(vLeft As String, vRight As String, Optional vIsContent As Boolean = False) As Widget
        Try
            Dim lBox As New Box(Orientation.Horizontal, 5)
            
            ' Comparison icon
            Dim lIcon As New Image()
            lIcon.SetFromIconName("view-sort-ascending", IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLeftName As String = If(vIsContent, vLeft, System.IO.Path.GetFileName(vLeft))
            Dim lRightName As String = If(vIsContent, vRight, System.IO.Path.GetFileName(vRight))
            Dim lLabel As New Label($"{lLeftName} ⟷ {lRightName}")
            lLabel.TooltipText = If(vIsContent, "Content comparison", $"{vLeft} ⟷ {vRight}")
            lBox.PackStart(lLabel, False, False, 0)
            
            ' Close button
            Dim lCloseButton As New Button()
            lCloseButton.Relief = ReliefStyle.None
            lCloseButton.Add(New Image(Stock.Close, IconSize.Menu))
            AddHandler lCloseButton.Clicked, AddressOf OnComparisonCloseClicked
            lBox.PackEnd(lCloseButton, False, False, 0)
            
            lBox.ShowAll()
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreateComparisonTabLabel error: {ex.Message}")
            Return New Label("Comparison")
        End Try
    End Function
    
    Private Sub SwitchToTabInfo(vTabInfo As TabInfo)
        Try
            For i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is vTabInfo.EditorContainer Then
                    pNotebook.CurrentPage = i
                    Exit For
                End If
            Next
        Catch ex As Exception
            Console.WriteLine($"SwitchToTabInfo error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub CloseAIArtifactTab(vArtifactId As String)
        Try
            If Not pAIArtifactTabs.ContainsKey(vArtifactId) Then Return
            
            Dim lTabInfo As TabInfo = pAIArtifactTabs(vArtifactId)
            
            ' Find and remove the page
            For i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is lTabInfo.EditorContainer Then
                    pNotebook.RemovePage(i)
                    Exit For
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
    
    Private Sub OnArtifactAccepted(vArtifactId As String, vContent As String, vTargetPath As String)
        Try
            ' Apply the artifact to the target file
            If Not String.IsNullOrEmpty(vTargetPath) Then
                ' Check if target file is already open
                If pOpenTabs.ContainsKey(vTargetPath) Then
                    ' Update the open file
                    Dim lEditor As IEditor = pOpenTabs(vTargetPath).Editor
                    If lEditor IsNot Nothing Then
                        lEditor.Text = vContent
                        SwitchToTab(vTargetPath)
                    End If
                Else
                    ' Save to file and open it
                    File.WriteAllText(vTargetPath, vContent)
                    OpenFile(vTargetPath)
                End If
                
                ' Close the artifact tab
                CloseAIArtifactTab(vArtifactId)
                
                ShowNotification("AI Artifact Applied", $"the AI artifact has been applied to {System.IO.Path.GetFileName(vTargetPath)}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnArtifactAccepted error: {ex.Message}")
            ShowError("Apply error", "Failed to apply AI artifact: " & ex.Message)
        End Try
    End Sub

    ' Add this method to MainWindow:
    Private Sub ShowNotification(vTitle As String, vMessage As String)
        Dim lStatusContext As UInteger = pStatusBar.GetContextId("notification")
        pStatusBar.Push(lStatusContext, $"{vTitle}: {vMessage}")
    End Sub
    
    Private Sub OnArtifactRejected(vArtifactId As String)
        Try
            ' Close the artifact tab
            CloseAIArtifactTab(vArtifactId)
            ShowNotification("AI Artifact Rejected", "the AI artifact has been discarded")
            
        Catch ex As Exception
            Console.WriteLine($"OnArtifactRejected error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnArtifactCompareRequested(vArtifactId As String, vContent As String, vTargetPath As String)
        Try
            If Not String.IsNullOrEmpty(vTargetPath) AndAlso File.Exists(vTargetPath) Then
                ' Read the target file
                Dim lOriginalContent As String = File.ReadAllText(vTargetPath)
                
                ' Show comparison
                ShowContentComparison(lOriginalContent, System.IO.Path.GetFileName(vTargetPath), 
                                    vContent, $"AI: {System.IO.Path.GetFileName(vTargetPath)}", 
                                    $"ai_compare_{vArtifactId}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnArtifactCompareRequested error: {ex.Message}")
            ShowError("Compare error", "Failed to compare artifact: " & ex.Message)
        End Try
    End Sub
    
    Private Sub OnComparisonCloseClicked(vSender As Object, vArgs As EventArgs)
        Try
            ' Find the comparison tab containing this button
            Dim lButton As Button = CType(vSender, Button)
            Dim lTabLabel As Widget = lButton.Parent
            
            ' Find and close the tab
            For i As Integer = 0 To pNotebook.NPages - 1
                If pNotebook.GetTabLabel(pNotebook.GetNthPage(i)) Is lTabLabel Then
                    ' Find in comparison tabs dictionary
                    Dim lTabToRemove As String = ""
                    For Each lEntry In pComparisonTabs
                        If lEntry.Value.TabLabel Is lTabLabel Then
                            lTabToRemove = lEntry.key
                            Exit For
                        End If
                    Next
                    
                    If Not String.IsNullOrEmpty(lTabToRemove) Then
                        pNotebook.RemovePage(i)
                        pComparisonTabs(lTabToRemove).Dispose()
                        pComparisonTabs.Remove(lTabToRemove)
                    End If
                    
                    Exit For
                End If
            Next
            
            ' Show welcome if no tabs left
            If pNotebook.NPages = 0 Then
                ShowWelcomeTab()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnComparisonCloseClicked error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnComparisonFilesSwapped()
        Try
            UpdateStatusBar("Files swapped in comparison view")
        Catch ex As Exception
            Console.WriteLine($"OnComparisonFilesSwapped error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnDifferenceNavigated(vDifferenceIndex As Integer, vTotalDifferences As Integer)
        Try
            UpdateStatusBar($"Difference {vDifferenceIndex + 1} of {vTotalDifferences}")
        Catch ex As Exception
            Console.WriteLine($"OnDifferenceNavigated error: {ex.Message}")
        End Try
    End Sub
    
End Class
