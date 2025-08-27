' MainWindow.vb - Main window implementation (FIXED)
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.AI
Imports SimpleIDE.Managers

' Main window class - uses partial classes to organize functionality
Partial Public Class MainWindow
    Inherits Window

    
    ' ===== Project Manager Event Handlers =====
    Private Sub OnProjectManagerProjectLoaded(vProjectPath As String)
        Try
            SetProjectRoot(vProjectPath)
            ' Update UI to reflect loaded project
            UpdateWindowTitle()
            UpdateToolbarButtons()
            
            ' Update status
            Dim lStatusContext As UInteger = pStatusBar.GetContextId("Main")
            pStatusBar.Pop(lStatusContext)
            pStatusBar.Push(lStatusContext, $"loaded project: {System.IO.Path.GetFileName(vProjectPath)}")
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerProjectLoaded error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnProjectManagerProjectClosed()
        Try
            ' Update UI
            UpdateWindowTitle()
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerProjectClosed error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnProjectManagerProjectModified()
        Try
            ' Mark project as modified in UI
            UpdateWindowTitle()
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerProjectModified error: {ex.Message}")
        End Try
    End Sub
    

    

    
    Private Sub OnProjectManagerIdentifierMapUpdated()
        Try
            ' Update all open editors with new identifier map
            Dim lIdentifierMap As Dictionary(Of String, String) = pProjectManager.GetIdentifierCaseMap()
            
            for each lTabEntry in pOpenTabs
                Dim lEditor As CustomDrawingEditor = TryCast(lTabEntry.Value.Editor, CustomDrawingEditor)
                If lEditor IsNot Nothing Then
                    ' Clear and reload identifier map
                    for each kvp in lIdentifierMap
                        lEditor.UpdateIdentifierCaseMap(kvp.key, kvp.Value)
                    Next
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerIdentifierMapUpdated error: {ex.Message}")
        End Try
    End Sub
    

    
    ' ===== Project Structure Event Handlers =====
    
    ''' <summary>
    ''' Handles requests for ProjectManager reference from editors
    ''' </summary>
    ''' <param name="sender">The requesting object (usually an editor)</param>
    ''' <param name="e">EventArgs containing the ProjectManager property to set</param>
    Private Sub OnEditorProjectManagerRequested(sender As Object, e As ProjectManagerRequestEventArgs)
        Try
            ' Provide the ProjectManager reference
            If pProjectManager IsNot Nothing Then
                e.ProjectManager = pProjectManager
                Console.WriteLine($"MainWindow provided ProjectManager to {sender.GetType().Name}")
            Else
                Console.WriteLine("MainWindow: ProjectManager not available for request")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorProjectManagerRequested error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Wire up ProjectManager request event when creating a new editor
    ''' </summary>
    ''' <param name="vEditor">The editor to wire up</param>
    Private Sub WireUpEditorProjectManagerRequest(vEditor As CustomDrawingEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Subscribe to the ProjectManagerRequested event
            AddHandler vEditor.ProjectManagerRequested, AddressOf OnEditorProjectManagerRequested
            
            Console.WriteLine("Wired up ProjectManagerRequested event for editor")
            
        Catch ex As Exception
            Console.WriteLine($"WireUpEditorProjectManagerRequest error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Unwire ProjectManager request event when closing an editor
    ''' </summary>
    ''' <param name="vEditor">The editor to unwire</param>
    Private Sub UnwireEditorProjectManagerRequest(vEditor As CustomDrawingEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Unsubscribe from the ProjectManagerRequested event
            RemoveHandler vEditor.ProjectManagerRequested, AddressOf OnEditorProjectManagerRequested
            
            Console.WriteLine("Unwired ProjectManagerRequested event for editor")
            
        Catch ex As Exception
            Console.WriteLine($"UnwireEditorProjectManagerRequest error: {ex.Message}")
        End Try
    End Sub


    
    ''' <summary>
    ''' Handle individual file parsed event
    ''' </summary>
    Private Sub OnProjectFileParsed(vFileInfo As SourceFileInfo)
        Try
            ' Check if this file is currently open in an editor
            for each lTab in pOpenTabs
                If lTab.Value.FilePath = vFileInfo.FilePath Then
                    ' Update the editor's syntax tree
                    If TypeOf lTab.Value.Editor Is CustomDrawingEditor Then
                        ' The editor should use the parsed structure
                        ' This maintains consistency between project view and editor view
                        Console.WriteLine($"File parsed: {vFileInfo.FileName} (open in Editor)")
                    End If
                    Exit for
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectFileParsed error: {ex.Message}")
        End Try
    End Sub

        ''' <summary>
    ''' Handle parsing progress updates
    ''' </summary>
    Private Sub OnProjectParsingProgress(vCurrent As Integer, vTotal As Integer, vFileName As String)
        Try
            ' Update status bar with progress
            Dim lProgress As String = $"Parsing project files... ({vCurrent}/{vTotal}): {vFileName}"
            UpdateStatusBar(lProgress)
            
            ' Process GTK events to keep UI responsive
            While Application.EventsPending()
                Application.RunIteration(False)
            End While
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectParsingProgress error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Count total nodes in a syntax tree
    ''' </summary>
    Private Function CountTotalNodes(vNode As SyntaxNode) As Integer
        Try
            If vNode Is Nothing Then Return 0
            
            Dim lCount As Integer = 1 ' Count this Node
            
            ' Count all children recursively
            for each lChild in vNode.Children
                lCount += CountTotalNodes(lChild)
            Next
            
            Return lCount
            
        Catch ex As Exception
            Console.WriteLine($"CountTotalNodes error: {ex.Message}")
            Return 0
        End Try
    End Function
    
    ' ===== Window Event Handlers =====
    Private Function OnWindowDelete(vSender As Object, vArgs As DeleteEventArgs) As Boolean
        Try
            ' Check for unsaved changes
            If Not CheckUnsavedChanges() Then
                vArgs.RetVal = True
                Return True ' Cancel close
            End If
            
            ' Close project if open
            If pProjectManager.IsProjectOpen Then
                pProjectManager.CloseProject()
            End If
            
            ' Save settings
            SaveWindowState()
            
            ' Dispose managers
            If pProjectManager IsNot Nothing Then
                pProjectManager.Dispose()
            End If

            ' Clean up
            CleanUp()
            
            ' Quit application
            Application.Quit()
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowDelete error: {ex.Message}")
            Return False
        End Try
    End Function

    ' ===== Project Management =====
    Public Sub LoadProjexct(vProjectFile As String)
        Try
            ' Check if file exists
            If Not File.Exists(vProjectFile) Then
                ShowError("project Not Found", $"the project file '{vProjectFile}' does not exist.")
                Return
            End If
            
            ' Close all open files
            CloseAllTabs()
            
            ' Load project through manager
            If pProjectManager.LoadProjectWithParsing(vProjectFile) Then
                ' Update current project path
                pCurrentProject = vProjectFile
                
                ' Load project in explorer
                pProjectExplorer.LoadProjectFromManager

' After loading the project structure, call:
'pObjectExplorer.ForceRefreshWithDebug()


                ' After opening a file and creating a tab
                UpdateObjectExplorerForActiveTab()                
                
                ' Update window title
                UpdateWindowTitle()

                ' Add to recent projects
                pSettingsManager.AddRecentProject(vProjectFile)
                
                ' Update toolbar
                UpdateToolbarButtons()
                OnProjectChangedUpdateScratchpad


                ' Raise event
                RaiseEvent ProjectChanged(vProjectFile)
            Else
                ShowError("Load project error", "Failed To load the project file.")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"LoadProject error: {ex.Message}")
            ShowError("Load project error", ex.Message)
        End Try
    End Sub

    
    ' ===== Helper Methods =====
    Private Sub UpdateWindowTitle(Optional vMarkDirty As Boolean = False)
        Try
            If pProjectManager.IsProjectOpen Then
                Dim lTitle As String = $"{pProjectManager.CurrentProjectName} - {WINDOW_TITLE}"
                If pProjectManager.IsDirty Then
                    lTitle = $"*{lTitle}"
                End If
                Title = lTitle
            Else
                Title = WINDOW_TITLE
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateWindowTitle error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub SaveWindowState()
        Try
            ' CRITICAL: Save the maximized state FIRST
            If Window IsNot Nothing Then
                Dim lWindowState As Gdk.WindowState = Window.State
                pSettingsManager.WindowMaximized = (lWindowState And Gdk.WindowState.Maximized) = Gdk.WindowState.Maximized
            End If
            
            ' Only save dimensions if NOT maximized
            If Not pSettingsManager.WindowMaximized Then
                Dim lWidth As Integer, lHeight As Integer
                GetSize(lWidth, lHeight)
                pSettingsManager.WindowWidth = lWidth
                pSettingsManager.WindowHeight = lHeight
            End If
            
            ' Save panel states
            pSettingsManager.ShowProjectExplorer = pLeftPanelVisible
            pSettingsManager.ShowBottomPanel = pBottomPanelVisible
            pSettingsManager.LeftPanelWidth = pMainHPaned.Position
            
            ' Save settings
            pSettingsManager.SaveSettings()
            
        Catch ex As Exception
            Console.WriteLine($"SaveWindowState error: {ex.Message}")
        End Try
    End Sub

    Private Sub SetupWindow()
        Try
            ' Set a small default size - will be overridden by RestoreWindowSizeAndState in Program.vb
            ' This prevents the window from starting too large
            SetDefaultSize(800, 600)
            
            ' Set icon
            Try
                Dim lIconPath As String = "SimpleIDE.icon.png"
                Dim lIcon As Gdk.Pixbuf = New Gdk.Pixbuf(GetType(MainWindow).Assembly, lIconPath)
                Icon = lIcon
            Catch ex As Exception
                Console.WriteLine($"Failed To load Icon: {ex.Message}")
            End Try
            
            ' Connect event handlers
            AddHandler DeleteEvent, AddressOf OnWindowDelete
            
            ' Enable required event masks
            AddEvents(CInt(Gdk.EventMask.KeyPressMask))
            
        Catch ex As Exception
            Console.WriteLine($"SetupWindow error: {ex.Message}")
            Throw
        End Try
    End Sub

    ' Mark the current project as modified
    Private Sub MarkProjectModified()
        Try
            ' Update window title to show modified state
            If pProjectManager.IsProjectOpen Then
                ' Project manager handles the dirty flag internally
                UpdateWindowTitle()
            End If
            
            ' Update toolbar buttons (Save All becomes enabled when project is modified)
            UpdateToolbarButtons()
            
            ' Update status bar with modification indicator
            Dim lStatusContext As UInteger = pStatusBar.GetContextId("project")
            pStatusBar.Pop(lStatusContext)
            pStatusBar.Push(lStatusContext, "project Modified")
            
            ' Fire project modified event for other components
            OnProjectModified()
            
            ' TODO: Could add auto-save logic here if enabled in settings
            ' If pSettingsManager.GetSetting("Project.AutoSave", "False") = "True" Then
            '     SaveProject()
            ' End If
            
        Catch ex As Exception
            Console.WriteLine($"MarkProjectModified error: {ex.Message}")
        End Try
    End Sub

    
    Private Sub OnProjectFileSelected(vFilePath As String)
        Try
            OpenFile(vFilePath)
        Catch ex As Exception
            Console.WriteLine($"OnProjectFileSelected error: {ex.Message}")
            ShowError("Failed To open file", ex.Message)
        End Try
    End Sub
    
    Private Sub OnProjectFileDoubleClicked(vProjectPath As String)
        Try
            LoadProjectEnhanced(vProjectPath)
        Catch ex As Exception
            Console.WriteLine($"OnProjectFileDoubleClicked error: {ex.Message}")
            ShowError("Failed To load project", ex.Message)
        End Try
    End Sub
    
    Private Sub OnProjectModified()
        UpdateWindowTitle(True)
        ' After opening a file and creating a tab
        UpdateObjectExplorerForActiveTab()
    End Sub
    
    Private Sub OnProjectExplorerCloseRequested()
        pLeftPanelVisible = False
        UpdatePanelVisibility()
    End Sub

    ' ===== Enhanced Project Loading =====
    
    Private Sub LoadProjectEnhanced(vProjectPath As String)
        Try
            Console.WriteLine($"Loading project with full parsing: {vProjectPath}")
            
            ' CRITICAL FIX: Set the current project path!
            pCurrentProject = vProjectPath
            Console.WriteLine($"Set pCurrentProject = {pCurrentProject}")
            
            ' Show progress dialog or status
            UpdateStatusBar("Loading project Structure...")
            
            ' Hook up ProjectManager events
            RemoveHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
            RemoveHandler pProjectManager.FileParsed, AddressOf OnProjectFileParsed
            RemoveHandler pProjectManager.ParsingProgress, AddressOf OnProjectParsingProgress
            
            AddHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
            AddHandler pProjectManager.FileParsed, AddressOf OnProjectFileParsed
            AddHandler pProjectManager.ParsingProgress, AddressOf OnProjectParsingProgress
            
            ' Load project with parsing
            If pProjectManager.LoadProjectWithParsing(vProjectPath) Then
                ' Set project root for all components
                SetProjectRoot(vProjectPath)
                
                ' Update UI - Use the new method!
                ' pProjectExplorer.LoadProject(vPropProjectExplorer.LoadProjectFromManagerjectPath)  ' OLD WAY
                Console.WriteLine($"Calling pProjectExplorer.LoadProjectFromManager from MainWindow.LoadProjectEnhanced")
                pProjectExplorer.LoadProjectFromManager
                
                UpdateWindowTitle()
                UpdateStatusBar($"Project loaded: {pProjectManager.CurrentProjectName}")
               
                ' Enable project-related menu items and toolbar buttons
                UpdateProjectRelatedUIState(True)
                
                ' Add to recent projects
                If pSettingsManager IsNot Nothing Then
                    pSettingsManager.AddRecentProject(vProjectPath)
                End If
                
                Console.WriteLine($"Project loaded successfully: {vProjectPath}")
            Else
                ' Reset if loading failed
                pCurrentProject = ""
                ShowError("Project Load Failed", $"Failed To load project: {vProjectPath}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"LoadProjectEnhanced error: {ex.Message}")
            pCurrentProject = "" ' Reset on error
            ShowError("Project Load error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Helper method to enable/disable project-related UI elements
    ''' </summary>
    Private Sub UpdateProjectRelatedUIState(vEnabled As Boolean)
        Try
            ' Enable/disable build buttons
            If pBuildButton IsNot Nothing Then pBuildButton.Sensitive = vEnabled
            If pRunButton IsNot Nothing Then pRunButton.Sensitive = vEnabled
            If pStopButton IsNot Nothing Then pStopButton.Sensitive = vEnabled
            
            ' Enable/disable menu items (if they exist)
            ' This would need to be expanded based on your actual menu structure
            
        Catch ex As Exception
            Console.WriteLine($"UpdateProjectRelatedUIState error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Also fix the SetProjectRoot method to set pCurrentProject
    ''' </summary>
    Private Sub SetProjectRoot(vProjectPath As String)
        Try
            ' ENSURE pCurrentProject is set
            If Not String.IsNullOrEmpty(vProjectPath) Then
                pCurrentProject = vProjectPath
                Console.WriteLine($"SetProjectRoot: pCurrentProject = {pCurrentProject}")
            End If
            
            ' Set project root for bottom panel manager
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.SetProjectRoot(vProjectPath)
            End If
            
            ' Set for other components as needed
            ' ...
            
        Catch ex As Exception
            Console.WriteLine($"SetProjectRoot error: {ex.Message}")
        End Try
    End Sub
    
    
    ' ===== Enhanced File Opening =====
    
    ''' <summary>
    ''' Enhanced file opening that integrates with project structure
    ''' </summary>
    Private Sub OpenFileWithProjectIntegration(vFilePath As String)
        Try
            ' Get file info from project manager if available
            Dim lSourceFileInfo As SourceFileInfo = Nothing
            If pProjectManager.IsProjectOpen Then
                lSourceFileInfo = pProjectManager.GetSourceFileInfo(vFilePath)
            End If
            ' Don't update status here - let the caller handle status messages
            ' UpdateStatusBar("Loading: " + vFilePath)
    
            ' Open the file normally
            OpenFile(vFilePath)
            
            ' Get the newly created tab
            Dim lTab As TabInfo = GetCurrentTabInfo()
            If lTab Is Nothing OrElse lTab.Editor Is Nothing Then Return
            
            ' If we have project file info, sync it with the editor
            If lSourceFileInfo IsNot Nothing Then
                'lSourceFileInfo.IsLoaded = True
                lSourceFileInfo.Editor = lTab.Editor
                
                ' FIXED: Don't update Object Explorer with single file structure
                ' Always maintain the full project structure when a project is open
                If pProjectManager.IsProjectOpen Then
                    ' Get the full project structure
                    Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                    If lProjectTree IsNot Nothing Then
                        pObjectExplorer?.UpdateStructure(lProjectTree)
                        Console.WriteLine("Object Explorer maintained with full project Structure after file open")
                    End If
                ElseIf lSourceFileInfo.SyntaxTree IsNot Nothing Then
                    ' Only update with single file if no project is open
                    pObjectExplorer?.UpdateStructure(lSourceFileInfo.SyntaxTree)
                End If
            End If
            
            ' Set up Object Explorer for this editor
            SetupEditorForObjectExplorer(lTab.Editor)
            
        Catch ex As Exception
            Console.WriteLine($"OpenFileWithProjectIntegration error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Enhanced Tab Switching =====
    
    ''' <summary>
    ''' Enhanced notebook page switch that updates from project structure
    ''' </summary>
    Private Sub OnNotebookSwitchPageWithProjectIntegration(vSender As Object, vArgs As SwitchPageArgs)
        Try
            ' Call original handler
            OnNotebookSwitchPage(vSender, vArgs)
            
            ' Get the current tab
            Dim lTab As TabInfo = GetCurrentTabInfo()
            If lTab Is Nothing Then Return
            
            ' If project is open, check if we have structure for this file
            If pProjectManager.IsProjectOpen AndAlso Not String.IsNullOrEmpty(lTab.FilePath) Then
                Dim lSourceFileInfo As SourceFileInfo = pProjectManager.GetSourceFileInfo(lTab.FilePath)
                
                If lSourceFileInfo IsNot Nothing AndAlso lSourceFileInfo.SyntaxTree IsNot Nothing Then
                    ' Update Object Explorer with project structure for this file
                    ' This ensures consistency even if the editor hasn't parsed yet
                    If pObjectExplorer IsNot Nothing Then
                        ' We could show just this file's structure or the whole project
                        ' For now, show the whole project structure
                        Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                        If lProjectTree IsNot Nothing Then
                            pObjectExplorer.UpdateStructure(lProjectTree)
                        End If
                    End If
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnNotebookSwitchPageWithProjectIntegration error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Editor Content Changes =====
    
    ''' <summary>
    ''' Handle editor content changes to update project structure
    ''' </summary>
    Private Sub OnEditorContentChangedWithProjectUpdate(vSender As Object, vArgs As EventArgs)
        Try
            ' Get the editor that changed
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return
            
            ' Find the tab for this editor
            Dim lTab As TabInfo = Nothing
            For Each lT In pOpenTabs
                If lT.Value.Editor Is lEditor Then
                    lTab = lT.Value
                    Exit For
                End If
            Next
            
            If lTab Is Nothing OrElse String.IsNullOrEmpty(lTab.FilePath) Then Return
            
            ' If project is open, update the file structure
            If pProjectManager.IsProjectOpen Then
                ' This will trigger a reparse and update events
                if pProjectManager.LoadProjectStructure() Then pProjectManager.DiagnoseProjectStructure() 
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorContentChangedWithProjectUpdate error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Refresh Commands =====
    
    ''' <summary>
    ''' Refresh the entire project structure
    ''' </summary>
    Private Sub RefreshProjectStructure()
        Try
            If Not pProjectManager.IsProjectOpen Then
                ShowInfo("", "No project Is currently open")
                Return
            End If
            
            UpdateStatusBar("Refreshing project Structure...")
            
            ' Refresh all files in the project
            pProjectManager.RefreshProjectStructure()
            
            UpdateStatusBar("project Structure refreshed")
            
        Catch ex As Exception
            Console.WriteLine($"RefreshProjectStructure error: {ex.Message}")
            ShowError("", $"error refreshing project: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Helper Methods =====
    
    
    ''' <summary>
    ''' Enable or disable project-related menu items
    ''' </summary>
    Private Sub EnableProjectMenus(vEnabled As Boolean)
        Try
            ' Enable/disable project menu items
            ' This would be implemented based on your menu structure
            ' For example:
            ' pCloseProjectMenuItem.Sensitive = vEnabled
            ' pBuildMenuItem.Sensitive = vEnabled
            ' pRefreshProjectMenuItem.Sensitive = vEnabled
            
        Catch ex As Exception
            Console.WriteLine($"EnableProjectMenus error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Override Project Opening =====
    
    ''' <summary>
    ''' Override the existing OpenProject method to use enhanced loading
    ''' </summary>
    Public Sub OpenProjectWithFullParsing(vProjectPath As String)
        Try
            ' Use the enhanced loading method
            LoadProjectEnhanced(vProjectPath)
            
        Catch ex As Exception
            Console.WriteLine($"OpenProjectWithFullParsing error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Initialization Enhancement =====
    
    ''' <summary>
    ''' Enhanced initialization for project integration
    ''' </summary>
    Private Sub InitializeProjectIntegration()
        Try
            ' Ensure Object Explorer is ready
            If pObjectExplorer Is Nothing Then
                Console.WriteLine("Warning: Object Explorer Not initialized for project integration")
                Return
            End If
            
            ' Set up refresh command in Object Explorer toolbar
            ' This could be connected to RefreshProjectStructure method
            
            ' Hook up to editor events for all open tabs
            For Each lTab In pOpenTabs
                If lTab.Value.Editor IsNot Nothing Then
                    RemoveHandler lTab.Value.Editor.DocumentParsed, AddressOf OnEditorDocumentParsed
                    AddHandler lTab.Value.Editor.DocumentParsed, AddressOf OnEditorDocumentParsed
                End If
            Next
            
            Console.WriteLine("project integration initialized")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeProjectIntegration error: {ex.Message}")
        End Try
    End Sub


    
    ''' <summary>
    ''' Simplified diagnostics to check if TreeView is working
    ''' </summary>
    Private Sub RunSimplifiedDiagnostics()
        Try
            If pObjectExplorer Is Nothing Then Return
            
            ' Get basic status
            Dim lTreeViewStatus As String = pObjectExplorer.GetTreeViewStatus()
            
            ' Only output if there's a problem
            If lTreeViewStatus.Contains("NO ITEMS") OrElse lTreeViewStatus.Contains("Not visible") Then
                Console.WriteLine("=== TreeView Issue Detected ===")
                Console.WriteLine(lTreeViewStatus)
                
                ' Try auto-fix
                Console.WriteLine("Attempting auto-recovery...")
                pObjectExplorer.ForceCompleteRefresh()
            Else
                ' Everything looks good - just report item count
                Console.WriteLine($"Object Explorer: {lTreeViewStatus}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"RunSimplifiedDiagnostics error: {ex.Message}")
        End Try
    End Sub

    
    ''' <summary>
    ''' Handle project structure loaded event - populate Object Explorer
    ''' </summary>
    Private Sub OnProjectStructureLoaded(vRootNode As SyntaxNode)
        Try
            Console.WriteLine($"=== OnProjectStructureLoaded START ===")
            Console.WriteLine($"Project Structure loaded with root: {vRootNode?.Name} ({vRootNode?.NodeType})")
            Console.WriteLine($"Root has {vRootNode?.Children.Count} children")
            
            ' Update Object Explorer with complete project structure
            If pObjectExplorer IsNot Nothing Then
                Console.WriteLine("Updating Object Explorer...")
                
                ' Auto-expand the root namespace for visibility
                If vRootNode?.NodeType = CodeNodeType.eNamespace Then
                    ' Get the expanded nodes set from the Object Explorer
                    ' and add the root namespace to auto-expand it
                    Console.WriteLine($"Auto-expanding root Namespace: {vRootNode.Name}")
                End If
                
                ' Load the project structure
                pObjectExplorer.LoadProjectStructure(vRootNode)
                
                ' Force refresh with auto-expansion
                Application.Invoke(Sub()
                    ' Use the debug method to get detailed output
                    If TypeOf pObjectExplorer Is CustomDrawObjectExplorer Then
                        Dim lCustomExplorer As CustomDrawObjectExplorer = DirectCast(pObjectExplorer, CustomDrawObjectExplorer)
                        lCustomExplorer.RebuildVisualTree()
                    End If
                    
                    ' Switch to Object Explorer tab
                    SwitchToObjectExplorerTab()
                End Sub)
                
                Console.WriteLine("Object Explorer update completed")
            Else
                Console.WriteLine("WARNING: pObjectExplorer Is Nothing!")
            End If
            
            Console.WriteLine($"=== OnProjectStructureLoaded End ===")
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectStructureLoaded error: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
        End Try
    End Sub
    

    
    ''' <summary>
    ''' Initialize Object Explorer with the project manager
    ''' </summary>
    Private Sub InitializeObjectExplorerWithProjectManager()
        Try
            If pObjectExplorer IsNot Nothing AndAlso pProjectManager IsNot Nothing Then
                Console.WriteLine("Initializing Object Explorer with ProjectManager...")
                
                ' Initialize the Object Explorer with the project manager
                pObjectExplorer.InitializeWithProjectManager(pProjectManager)
                
                ' Check if we already have a project loaded
                Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                If lProjectTree IsNot Nothing Then
                    Console.WriteLine($"Found existing project tree: {lProjectTree.Name}")
                    OnProjectStructureLoaded(lProjectTree)
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"InitializeObjectExplorerWithProjectManager error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Call this after both ProjectManager and ObjectExplorer are created
    ''' </summary>
    Private Sub CompleteObjectExplorerSetup()
        Try
            ' Initialize with project manager
            InitializeObjectExplorerWithProjectManager()
            
            ' Hook up the event handler if not already done
            If pProjectManager IsNot Nothing Then
                RemoveHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
                AddHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded

                Console.WriteLine("ProjectStructureLoaded Event handler connected")
            End If

        Catch ex As Exception
            Console.WriteLine($"CompleteObjectExplorerSetup error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Verifies the project structure after loading to ensure proper namespace merging
    ''' </summary>
    Private Sub VerifyProjectStructure()
        Try
            Console.WriteLine("=== VERIFYING PROJECT Structure ===")
            
            ' Run verification on ProjectManager
            If pProjectManager IsNot Nothing Then
                Dim lIsValid As Boolean = pProjectManager.VerifyNamespaceMerge()
                
                If Not lIsValid Then
                    Console.WriteLine("Project Structure has duplicates - attempting rebuild...")
                    pProjectManager.RebuildProjectTree()
                    
                    ' Verify again after rebuild
                    lIsValid = pProjectManager.VerifyNamespaceMerge()
                    If lIsValid Then
                        Console.WriteLine("Rebuild successful - Structure Is now valid")
                        
                        ' Refresh Object Explorer
                        If pObjectExplorer IsNot Nothing Then
                            pObjectExplorer.LoadProjectStructure(pProjectManager.GetProjectSyntaxTree())
                        End If
                    Else
                        Console.WriteLine("Rebuild failed - duplicates still present")
                    End If
                Else
                    Console.WriteLine("Project Structure Is valid - no duplicates found")
                End If
            End If
            
            Console.WriteLine("=== End VERIFICATION ===")
            
        Catch ex As Exception
            Console.WriteLine($"VerifyProjectStructure error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Add this call to the project loaded event handler
    ''' </summary>
    Private Sub OnProjectLoadedWithVerification(vProjectInfo As ProjectInfo)
        Try
            ' Existing project loaded logic...
            
            ' Add verification after project is fully loaded
            Application.Invoke(Sub()
                System.Threading.Thread.Sleep(500) ' Small delay to ensure everything is loaded
                VerifyProjectStructure()
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectLoadedWithVerification error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Initialize reference management events from ProjectManager
    ''' </summary>
    Private Sub InitializeProjectManagerReferences()
        Try
            If pProjectManager Is Nothing Then Return
            
            ' Wire up reference-related events
            AddHandler pProjectManager.ReferencesChanged, AddressOf OnProjectManagerReferencesChanged
            AddHandler pProjectManager.ReferenceAdded, AddressOf OnProjectManagerReferenceAdded
            AddHandler pProjectManager.ReferenceRemoved, AddressOf OnProjectManagerReferenceRemoved
            
            Console.WriteLine("ProjectManager reference events initialized")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeProjectManagerReferences error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handle references changed from ProjectManager
    ''' </summary>
    Private Sub OnProjectManagerReferencesChanged(vReferences As List(Of ReferenceManager.ReferenceInfo))
        Try
            Console.WriteLine($"Project references changed: {vReferences.Count} references")
            
            ' Update UI if needed
            ' Could refresh project explorer or other UI elements here
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerReferencesChanged error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle reference added from ProjectManager
    ''' </summary>
    Private Sub OnProjectManagerReferenceAdded(vReference As ReferenceManager.ReferenceInfo)
        Try
            Console.WriteLine($"Reference added: {vReference.Name} ({vReference.Type})")
            UpdateStatusBar($"Added reference: {vReference.Name}")
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerReferenceAdded error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle reference removed from ProjectManager
    ''' </summary>
    Private Sub OnProjectManagerReferenceRemoved(vReferenceName As String, vReferenceType As ReferenceManager.ReferenceType)
        Try
            Console.WriteLine($"Reference removed: {vReferenceName} ({vReferenceType})")
            UpdateStatusBar($"Removed reference: {vReferenceName}")
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerReferenceRemoved error: {ex.Message}")
        End Try
    End Sub
    
End Class
