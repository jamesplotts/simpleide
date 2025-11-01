' MainWindow.ProjectLoading.vb - Project loading integration with DocumentModel management
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports Gtk
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

' MainWindow.ProjectLoading.vb
' Created: 2025-08-07 21:14:48

Partial Public Class MainWindow
    
    ' ===== Private Fields for Project Loading =====
    
    Private pProgressDialog As Dialog
    'Private pProgressBar As ProgressBar
    Private pProgressLabel As Label
    Private pCancelButton As Button
    Private pLoadCancellationToken As CancellationTokenSource
    
    ' ===== Enhanced Project Loading Methods =====

    ''' <summary>
    ''' Loads a project with progress bar updates in the status bar
    ''' </summary>
    ''' <param name="vProjectPath">Path to the project file</param>
    Private Sub LoadProjectWithProgressBar(vProjectPath As String)
        Try
            Console.WriteLine($"LoadProjectWithProgressBar: Loading {vProjectPath}")
            
            ' Show initial status
            UpdateStatusBar("Loading project structure...")
            ShowProgressBar(True)
            UpdateProgressBar(0)
            
            ' Hook up parsing progress events
            RemoveHandler pProjectManager.ParsingProgress, AddressOf OnProjectParsingProgressWithBar
            AddHandler pProjectManager.ParsingProgress, AddressOf OnProjectParsingProgressWithBar
            
            ' Store total files for progress calculation
            pTotalFilesToParse = 0
            pCurrentFileParsed = 0
            
            ' Load the project
            If pProjectManager.LoadProjectWithParsing(vProjectPath) Then
                ' Success - update UI
                pCurrentProject = vProjectPath
                SetProjectRoot(vProjectPath)
                
                ' Update Project Explorer
                pProjectExplorer?.LoadProjectFromManager()
                
                ' Update UI elements
                UpdateWindowTitle()
                UpdateToolbarButtons()
                UpdateProjectRelatedUIState(True)
                
                ' Add to recent projects
                pSettingsManager?.AddRecentProject(vProjectPath)
                
                ' Final status
                UpdateStatusBar($"Project loaded: {pProjectManager.CurrentProjectName}")
                
                
                Console.WriteLine($"Project loaded successfully: {vProjectPath}")
            Else
                ' Failed
                pCurrentProject = ""
                UpdateStatusBar("Failed to load project")
                ShowError("Project Load Failed", $"Failed to load project: {vProjectPath}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"LoadProjectWithProgressBar error: {ex.Message}")
            pCurrentProject = ""
            UpdateStatusBar("Project load error")
            ShowError("Project Load Error", ex.Message)
        Finally
            ' Hide progress bar after a short delay
            GLib.Timeout.Add(500, Function()
                ShowProgressBar(False)
                Return False
            End Function)
            
            ' Unhook event
            RemoveHandler pProjectManager.ParsingProgress, AddressOf OnProjectParsingProgressWithBar
        End Try
    End Sub

    
    ''' <summary>
    ''' Load project with progress dialog and DocumentModel management
    ''' </summary>
    Private Sub LoadProjectWithProgress(vProjectPath As String)
        Try
            Console.WriteLine($"MainWindow.LoadProjectWithProgress: Loading {vProjectPath}")
            
            ' Create and show progress dialog
            CreateProgressDialog()
            
            ' Set up cancellation token
            pLoadCancellationToken = New CancellationTokenSource()
            
            ' Wire up ProjectManager events
            AddHandler pProjectManager.ProjectLoadProgress, AddressOf OnProjectLoadProgress
            AddHandler pProjectManager.AllDocumentsLoaded, AddressOf OnAllDocumentsLoaded
            AddHandler pProjectManager.ProjectStructureChanged, AddressOf OnProjectStructureChanged
            
            ' Start loading in background task
            Dim lLoadTask As Task(Of Boolean) = Task.Run(Of Boolean)(Function()
                Try
                    ' Load project with all DocumentModels
                    Return pProjectManager.LoadProjectWithDocuments(vProjectPath)
                Catch ex As Exception
                    Console.WriteLine($"project load error: {ex.Message}")
                    Return False
                End Try
            End Function, pLoadCancellationToken.Token)
            
            ' Run dialog (blocks until closed)
            pProgressDialog.Run()
            
            ' Wait for task completion
            lLoadTask.Wait()
            Dim lSuccess As Boolean = CType(lLoadTask.Result, Boolean)    
        
            ' Clean up
            pProgressDialog.Destroy()
            pProgressDialog = Nothing
            
            ' Unwire events
            RemoveHandler pProjectManager.ProjectLoadProgress, AddressOf OnProjectLoadProgress
            RemoveHandler pProjectManager.AllDocumentsLoaded, AddressOf OnAllDocumentsLoaded
            RemoveHandler pProjectManager.ProjectStructureChanged, AddressOf OnProjectStructureChanged
            
            If lSuccess Then
                ' Update UI for loaded project
                UpdateUIForLoadedProject()
                
                ' Update window title
                Title = $"VbIDE - {pProjectManager.CurrentProjectName}"
                
                ' Show success in status bar
                UpdateStatusBar($"project loaded: {pProjectManager.GetAllDocumentModels().Count} files")
            Else
                ' Show error dialog
                ShowErrorDialog("project Load Failed", $"Failed to load project: {vProjectPath}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.LoadProjectWithProgress error: {ex.Message}")
            If pProgressDialog IsNot Nothing Then
                pProgressDialog.Destroy()
            End If
            ShowErrorDialog("project Load error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Create progress dialog for project loading
    ''' </summary>
    Private Sub CreateProgressDialog()
        Try
            ' Create dialog
            pProgressDialog = New Dialog("Loading project", Me, DialogFlags.Modal)
            pProgressDialog.SetDefaultSize(400, 150)
            
            ' Create content area
            Dim lVBox As New Box(Orientation.Vertical, 0)
            lVBox.BorderWidth = 10
            
            ' Add progress label
            pProgressLabel = New Label("Initializing...")
            pProgressLabel.Xalign = 0
            lVBox.PackStart(pProgressLabel, False, False, 0)
            
            ' Add progress bar
            pProgressBar = New ProgressBar()
            lVBox.PackStart(pProgressBar, False, False, 0)
            
            ' Add file count label
            Dim lFileCountLabel As New Label("")
            lFileCountLabel.Name = "FileCountLabel"
            lFileCountLabel.Xalign = 0
            lVBox.PackStart(lFileCountLabel, False, False, 0)
            
            ' Add to dialog content area
            pProgressDialog.ContentArea.PackStart(lVBox, True, True, 0)
            
            ' Add cancel button
            pCancelButton = CType(pProgressDialog.AddButton("Cancel", ResponseType.Cancel), Button)
            AddHandler pCancelButton.Clicked, AddressOf OnProgressCancelClicked
            
            ' Show all widgets
            pProgressDialog.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.CreateProgressDialog error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle project load progress updates
    ''' </summary>
    Private Sub OnProjectLoadProgress(vFilesLoaded As Integer, vTotalFiles As Integer, vCurrentFile As String)
        Try
            ' Update UI on main thread
            Application.Invoke(Sub()
                If pProgressBar IsNot Nothing Then
                    ' Update progress bar
                    Dim lProgress As Double = CDbl(vFilesLoaded) / CDbl(vTotalFiles)
                    pProgressBar.Fraction = lProgress
                    pProgressBar.Text = $"{CInt(lProgress * 100)}%"
                    
                    ' Update labels
                    If pProgressLabel IsNot Nothing Then
                        pProgressLabel.Text = $"Loading: {vCurrentFile}"
                    End If
                    
                    ' Update file count
                    Dim lFileCountLabel As Label = CType(pProgressDialog.ContentArea.Children(0), VBox).Children(2)
                    If lFileCountLabel IsNot Nothing Then
                        lFileCountLabel.Text = $"Files: {vFilesLoaded} / {vTotalFiles}"
                    End If
                End If
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.OnProjectLoadProgress error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle all documents loaded event
    ''' </summary>
    Private Sub OnAllDocumentsLoaded(vDocumentCount As Integer)
        Try
            Console.WriteLine($"All {vDocumentCount} documents loaded")
            
            ' Close progress dialog on main thread
            Application.Invoke(Sub()
                If pProgressDialog IsNot Nothing Then
                    pProgressDialog.Respond(ResponseType.Ok)
                End If
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.OnAllDocumentsLoaded error: {ex.Message}")
        End Try
    End Sub
    
'    ''' <summary>
'    ''' Handle project structure changed event
'    ''' </summary>
'    Private Sub OnProjectStructureChanged(vRootNode As DocumentNode)
'        Try
'            Console.WriteLine("project structure updated")
'            
'            ' Update object explorer on main thread
'            Application.Invoke(Sub()
'                If pObjectExplorer IsNot Nothing Then
'                    Dim lSyntaxNode As SyntaxNode = ConvertDocumentNodeToSyntaxNode(vRootNode)
'                    If lSyntaxNode IsNot Nothing Then
'                        pObjectExplorer.SetProjectStructure(lSyntaxNode)
'                    End If
'                End If
'            End Sub)
'            
'        Catch ex As Exception
'            Console.WriteLine($"MainWindow.OnProjectStructureChanged error: {ex.Message}")
'        End Try
'    End Sub
    
    ''' <summary>
    ''' Handle cancel button click in progress dialog
    ''' </summary>
    Private Sub OnProgressCancelClicked(vSender As Object, vArgs As EventArgs)
        Try
            ' Cancel the loading operation
            If pLoadCancellationToken IsNot Nothing Then
                pLoadCancellationToken.Cancel()
            End If
            
            ' Close dialog
            If pProgressDialog IsNot Nothing Then
                pProgressDialog.Respond(ResponseType.Cancel)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.OnProgressCancelClicked error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Update UI after project is loaded
    ''' </summary>
    Private Sub UpdateUIForLoadedProject()
        Try
            ' Populate project explorer
            If pProjectExplorer IsNot Nothing Then
                Console.WriteLine($"Calling pProjectExplorer.LoadProjectFromManager from MainWindow.UpdateUIForLoadedProject")
                pProjectExplorer.LoadProjectFromManager
            End If
            
            ' Get namespace tree
            Dim lNamespaceTree As DocumentNode = pProjectManager.GetUnifiedNamespaceTree()
            Dim lSyntaxTree As SyntaxNode = ConvertDocumentNodeToSyntaxNode(lNamespaceTree)
            
            ' Update object explorer
            If pObjectExplorer IsNot Nothing AndAlso lNamespaceTree IsNot Nothing Then
                pObjectExplorer.SetProjectStructure(lSyntaxTree)
            End If
            
            ' Enable project-related menu items
            EnableProjectMenuItems(True)
            
            ' Show project info in status bar
            Dim lDocCount As Integer = pProjectManager.GetAllDocumentModels().Count
            Dim lSymbolCount As Integer = 0
            If lNamespaceTree IsNot Nothing Then
                lSymbolCount = CountNodesRecursive(lNamespaceTree)
            End If
            
            UpdateStatusBar($"project loaded: {lDocCount} files, {lSymbolCount} symbols")
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.UpdateUIForLoadedProject error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Open file in editor using existing DocumentModel
    ''' </summary>
    Private Sub OpenFileWithDocumentModel(vFilePath As String)
        Try
            ' Check if DocumentModel exists
            Dim lDocModel As DocumentModel = pProjectManager.GetDocumentModel(vFilePath)
            
            If lDocModel Is Nothing Then
                ' File not in project - create new DocumentModel
                lDocModel = pProjectManager.AddFileToProject(vFilePath)
                If lDocModel Is Nothing Then
                    ShowErrorDialog("Open File Failed", $"Failed to open: {vFilePath}")
                    Return
                End If
            End If
            
            ' Check if already open in a tab
            for i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If TypeOf lPage Is ScrolledWindow Then
                    Dim lScrolled As ScrolledWindow = CType(lPage, ScrolledWindow)
                    If lScrolled.Child IsNot Nothing AndAlso TypeOf lScrolled.Child Is IEditor Then
                        Dim lEditor As IEditor = CType(lScrolled.Child, IEditor)
                        If lEditor.FilePath() = vFilePath Then
                            ' Already open - just switch to it
                            pNotebook.CurrentPage = i
                            Return
                        End If
                    End If
                End If
            Next
            
            ' Create new editor
            Dim lNewEditor As IEditor = New CustomDrawingEditor(New SourceFileInfo(vFilePath, ""), pThemeManager, pSettingsManager)
            lNewEditor.SettingsManager = pSettingsManager

            ' Pass the DocumentModel to the editor
            If TypeOf lNewEditor Is CustomDrawingEditor Then
                Dim lCustomEditor As CustomDrawingEditor = CType(lNewEditor, CustomDrawingEditor)
                lCustomEditor.SetDocumentModel(lDocModel)
            End If
            
            ' Register editor with ProjectManager
            pProjectManager.RegisterEditorForDocument(vFilePath, lNewEditor)
            
'            ' Create scrolled window for editor
'            Dim lScrolledWindow As New ScrolledWindow()
'            lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
'            lScrolledWindow.Add(CType(lNewEditor, Widget))
            
            ' Add to notebook
            Dim lPageIndex As Integer = pNotebook.AppendPage(lNewEditor, System.IO.Path.GetFileName(vFilePath))
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Focus the editor
            CType(lNewEditor, Widget).GrabFocus()
            
            ' Update status bar
            UpdateStatusBar($"Opened: {System.IO.Path.GetFileName(vFilePath)}")
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.OpenFileWithDocumentModel error: {ex.Message}")
            ShowErrorDialog("Open File error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Count nodes recursively in tree
    ''' </summary>
    Private Function CountNodesRecursive(vNode As DocumentNode) As Integer
        If vNode Is Nothing Then Return 0
        
        Dim lCount As Integer = 1
        
        for each lChild in vNode.Children
            lCount += CountNodesRecursive(lChild)
        Next
        
        Return lCount
    End Function
    
    ''' <summary>
    ''' Enable or disable project-related menu items
    ''' </summary>
    Private Sub EnableProjectMenuItems(vEnabled As Boolean)
        Try
            ' This would enable/disable menu items related to project operations
            ' Implementation depends on your menu structure
            
            ' For now, just log the state
            Console.WriteLine($"project menu items enabled: {vEnabled}")
            
        Catch ex As Exception
            Console.WriteLine($"MainWindow.EnableProjectMenuItems error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Show error dialog
    ''' </summary>
    Private Sub ShowErrorDialog(vTitle As String, vMessage As String)
        Try
            Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, MessageType.error, ButtonsType.Ok, vMessage)
            lDialog.Title = vTitle
            lDialog.Run()
            lDialog.Destroy()
        Catch ex As Exception
            Console.WriteLine($"MainWindow.ShowErrorDialog error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Helper method to convert DocumentNode to SyntaxNode
    ''' </summary>
    Private Function ConvertDocumentNodeToSyntaxNode(vDocNode As DocumentNode) As SyntaxNode
        Try
            If vDocNode Is Nothing Then Return Nothing
            
            Dim lSyntaxNode As New SyntaxNode(vDocNode.NodeType, vDocNode.Name)
            lSyntaxNode.StartLine = vDocNode.StartLine
            lSyntaxNode.EndLine = vDocNode.EndLine
            lSyntaxNode.StartColumn = vDocNode.StartColumn
            lSyntaxNode.EndColumn = vDocNode.EndColumn
            
            ' Copy attributes
            for each lAttr in vDocNode.Attributes
                If Not lSyntaxNode.Attributes.ContainsKey(lAttr.key) Then
                    lSyntaxNode.Attributes(lAttr.key) = lAttr.Value.ToString()
                End If
            Next
            
            ' Recursively convert children
            for each lChildNode in vDocNode.Children
                Dim lChildSyntaxNode As SyntaxNode = ConvertDocumentNodeToSyntaxNode(lChildNode)
                If lChildSyntaxNode IsNot Nothing Then
                    lSyntaxNode.AddChild(lChildSyntaxNode)
                End If
            Next
            
            Return lSyntaxNode
            
        Catch ex As Exception
            Console.WriteLine($"ConvertDocumentNodeToSyntaxNode error: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ' Handle project structure changes
    Private Sub OnProjectStructureChanged(vRootNode As DocumentNode)
        Try
            ' Refresh Object Explorer when structure changes
            RefreshObjectExplorer()
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectStructureChanged error: {ex.Message}")
        End Try
    End Sub  

    ''' <summary>
    ''' Fixed version of RefreshObjectExplorer
    ''' </summary>
    Private Sub RefreshObjectExplorer()
        Try
            If pObjectExplorer Is Nothing OrElse pProjectManager Is Nothing Then Return
            
            ' Get the project syntax tree from ProjectManager
            Dim lProjectRoot As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
            
            If lProjectRoot IsNot Nothing Then
                ' Store in RootNode property (FIX: was trying to assign ProjectRoot string)
                'pRootNode = lProjectRoot  ' This should be the SyntaxNode, not ProjectRoot string
                
                ' Load the structure into Object Explorer
                pObjectExplorer.LoadProjectStructure(lProjectRoot)
                Console.WriteLine("Object Explorer refreshed with project structure")
            Else
                ' Clear the Object Explorer if no project
                pObjectExplorer.ClearStructure()
                Console.WriteLine("Object Explorer cleared - no project structure")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"RefreshObjectExplorer error: {ex.Message}")
        End Try
    End Sub  

    ''' <summary>
    ''' Handles parsing progress updates with progress bar
    ''' </summary>
    ''' <param name="vCurrent">Current file number being parsed</param>
    ''' <param name="vTotal">Total number of files to parse</param>
    ''' <param name="vFileName">Name of the file being parsed</param>
    Private Sub OnProjectParsingProgressWithBar(vCurrent As Integer, vTotal As Integer, vFileName As String)
        Try
            ' Store totals
            pTotalFilesToParse = vTotal
            pCurrentFileParsed = vCurrent
            
            ' Calculate percentage based on phase
            Dim lPercentage As Double = 0
            Dim lStatusMessage As String = ""
            
            If vTotal > 0 Then
                ' Check what phase we're in based on the filename/message
                If vFileName.Contains("Starting parse") OrElse vFileName.Contains("Initializing") Then
                    ' Initial phase
                    lPercentage = 0
                    lStatusMessage = "Initializing parser..."
                ElseIf vFileName.Contains("Theme application complete") Then
                    ' Final completion - ensure we're at 100%
                    lPercentage = 100
                    lStatusMessage = "Complete!"
                ElseIf vFileName.Contains("Applying theme") OrElse vFileName.Contains("Applying colors") Then
                    ' Theme application phase (95-99%)
                    lPercentage = 95
                    lStatusMessage = "Applying syntax colors..."
                Else
                    ' Main parsing phase (5-95%)
                    ' Scale the file progress to fit in 5-95% range
                    lPercentage = 5 + (vCurrent / CDbl(vTotal)) * 90
                    
                    ' Create status message
                    Dim lShortName As String = System.IO.Path.GetFileName(vFileName)
                    lStatusMessage = $"Parsing files ({vCurrent}/{vTotal}): {lShortName}"
                End If
            Else
                ' No files, just show 100%
                lPercentage = 100
                lStatusMessage = "No files to parse"
            End If
            
            ' Update status and progress bar
            UpdateStatusBar(lStatusMessage)
            UpdateProgressBar(lPercentage)
            
            ' Process GTK events periodically to keep UI responsive
            ' But not on every update to avoid slowing down
            If vCurrent Mod 5 = 0 OrElse vCurrent = vTotal OrElse vFileName.Contains("complete") Then
                While Application.EventsPending()
                    Application.RunIteration(False)
                End While
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectParsingProgressWithBar error: {ex.Message}")
        End Try
    End Sub

End Class
