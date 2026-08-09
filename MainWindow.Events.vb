' MainWindow.Events.vb - Event handlers for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers

Partial Public Class MainWindow

    
    ' ===== Window Events =====

    ' Handle TODO selection from BottomPanelManager
    Private Sub OnTodoSelected(vTodo As TODOItem)
        Try
            If vTodo IsNot Nothing AndAlso Not String.IsNullOrEmpty(vTodo.SourceFile) Then
                OpenSpecificFile(vTodo.SourceFile, vTodo.SourceLine, vTodo.SourceColumn)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnTodoSelected error: {ex.Message}")
        End Try
    End Sub

    ' Handle TODO double-click from BottomPanelManager
    Private Sub OnTodoDoubleClicked(vTodo As TODOItem)
        Try
            If vTodo IsNot Nothing AndAlso Not String.IsNullOrEmpty(vTodo.SourceFile) Then
                OpenSpecificFile(vTodo.SourceFile, vTodo.SourceLine, vTodo.SourceColumn)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnTodoDoubleClicked error: {ex.Message}")
        End Try
    End Sub

    ' Handle "Go to Source" context menu action from BottomPanelManager
    Private Sub OnTodoNavigateToCode(vFilePath As String, vLine As Integer, vColumn As Integer)
        Try
            If Not String.IsNullOrEmpty(vFilePath) Then
                OpenSpecificFile(vFilePath, vLine, vColumn)
            End If
        Catch ex As Exception
            Console.WriteLine($"OnTodoNavigateToCode error: {ex.Message}")
        End Try
    End Sub


    
    Public Sub OnSaveAll(vSender As Object, vArgs As EventArgs)
        Try
            If SaveAllFiles() Then
                ShowInfo("Save All", "All files have been saved.")
            Else
                ShowError("Save All", "One or more files could not be saved.")
            End If
        Catch ex As Exception
            Console.WriteLine($"OnSaveAll error: {ex.Message}")
            ShowError("Save All failed", ex.Message)
        End Try
    End Sub
    
    Public Sub OnRecentFile(vSender As Object, vArgs As EventArgs)
        Try
            If TypeOf vSender Is MenuItem Then
                Dim lMenuItem As MenuItem = DirectCast(vSender, MenuItem)
                If lMenuItem.Child IsNot Nothing AndAlso TypeOf lMenuItem.Child Is Label Then
                    Dim lLabel As Label = DirectCast(lMenuItem.Child, Label)
                    Dim lFilePath As String = lLabel.Text
                    
                    If File.Exists(lFilePath) Then
                        If lFilePath.EndsWith(".vbproj") Then
                            LoadProjectEnhanced(lFilePath)
                        Else
                            OpenSpecificFile(lFilePath, 1, 1)
                        End If
                    Else
                        ShowError("File Not Found", $"The file '{lFilePath}' no longer exists.")
                    End If
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"OnRecentFile error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnExit(vSender As Object, vArgs As EventArgs)
        Try
            ' Reuse the same window-close logic (prompts to save, respects Cancel) that
            ' actually handles the title bar close button
            OnWindowDelete(vSender, New DeleteEventArgs())
        Catch ex As Exception
            Console.WriteLine($"OnExit error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Edit Menu Events =====
    Public Sub OnUndo(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Undo()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnUndo error: {ex.Message}")
            ShowError("Undo failed", ex.Message)
        End Try
    End Sub

    Public Sub OnRedo(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Redo()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnRedo error: {ex.Message}")
            ShowError("Redo failed", ex.Message)
        End Try
    End Sub

    Public Sub OnCut(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Cut()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnCut error: {ex.Message}")
            ShowError("Cut failed", ex.Message)
        End Try
    End Sub

    Public Sub OnCopy(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Copy()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnCopy error: {ex.Message}")
            ShowError("Copy failed", ex.Message)
        End Try
    End Sub

    Public Sub OnPaste(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.Paste()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnPaste error: {ex.Message}")
            ShowError("Paste failed", ex.Message)
        End Try
    End Sub

    Public Sub OnSelectAll(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.SelectAll()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnSelectAll error: {ex.Message}")
            ShowError("Select All failed", ex.Message)
        End Try
    End Sub
    
    Public Sub OnFind(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.FindReplace.vb
            ShowFindPanel()
        Catch ex As Exception
            Console.WriteLine($"OnFind error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnReplace(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.FindReplace.vb
            ShowReplacePanel()
        Catch ex As Exception
            Console.WriteLine($"OnReplace error: {ex.Message}")
        End Try
    End Sub
    
    ' Replace: SimpleIDE.MainWindow.OnGoToLine
    ''' <summary>
    ''' Handles the Go To Line command by focusing the line number entry in the status bar
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Event arguments</param>
    Public Sub OnGoToLine(vSender As Object, vArgs As EventArgs)
        Try
            ' Focus the line number entry in the status bar instead of showing a dialog
            FocusLineNumberEntry()
        Catch ex As Exception
            Console.WriteLine($"OnGoToLine error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== View Menu Events =====
    ''' <summary>
    ''' Delegates to ToggleProjectExplorer (MainWindow.Keyboard.vb) - see its doc comment.
    ''' Previously toggled pProjectExplorer.Visible directly here, which only blanked the
    ''' current notebook page rather than reclaiming the panel's width, and tracked state
    ''' independently of Ctrl+E's implementation, so the two could disagree about whether
    ''' the panel was actually shown.
    ''' </summary>
    Public Sub OnToggleProjectExplorer(vSender As Object, vArgs As EventArgs)
        Try
            ToggleProjectExplorer()
        Catch ex As Exception
            Console.WriteLine($"OnToggleProjectExplorer error: {ex.Message}")
            ShowError("Toggle Project Explorer failed", ex.Message)
        End Try
    End Sub

    Public Sub OnToggleBottomPanel(vSender As Object, vArgs As EventArgs)
        Try
            pBottomPanelVisible = Not pBottomPanelVisible
            pSettingsManager.ShowBottomPanel = pBottomPanelVisible
            UpdatePanelVisibility()
        Catch ex As Exception
            Console.WriteLine($"OnToggleBottomPanel error: {ex.Message}")
            ShowError("Toggle Bottom Panel failed", ex.Message)
        End Try
    End Sub
    
    Public Sub OnZoomIn(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.ZoomIn()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnZoomIn error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnZoomOut(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.ZoomOut()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnZoomOut error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnZoomReset(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentEditor As IEditor = GetCurrentEditor()
            If lCurrentEditor IsNot Nothing Then
                lCurrentEditor.ZoomReset()
            End If
        Catch ex As Exception
            Console.WriteLine($"OnZoomReset error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Build Menu Events =====
    
    
    
    
    ''' <summary>
    ''' Handles Run button click - NO build checking for F5 
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Event arguments</param>
    ''' <remarks>
    ''' This should ONLY trigger build when called from Run button, not F5
    ''' To prevent double builds, we check if we're already in a build/run cycle
    ''' </remarks>
    Public Sub OnRunProject(vSender As Object, vArgs As EventArgs)
        Try
            #If DEBUG Then
            Console.WriteLine($"OnRunProject called - Sender type: {vSender?.GetType()?.Name}")
            #End If
            
            ' CRITICAL: Check if we're already building/running to prevent cascade
            If pIsBuildingNow OrElse pRunAfterBuild Then
                #If DEBUG Then
                Console.WriteLine("OnRunProject: Skipping - already in build/run cycle")
                #End If
                Return
            End If
            
            If pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding Then
                #If DEBUG Then
                Console.WriteLine("OnRunProject: Skipping - build in progress")
                #End If
                Return
            End If
            
            ' Check if this is from the Run button (not F5)
            ' F5 calls OnBuildAndRun directly, so OnRunProject should only handle button clicks.
            ' The toolbar's Run button (and BuildOutputPanel's copy of it) are CustomDrawButton,
            ' not the native ToolButton this check originally targeted - it silently stopped
            ' matching when the toolbar was converted to CustomDrawButton earlier this session.
            Dim lIsFromButton As Boolean = TypeOf vSender Is CustomDrawButton OrElse _
                                           TypeOf vSender Is MenuItem
            
            If Not lIsFromButton Then
                #If DEBUG Then
                Console.WriteLine("OnRunProject: Not from button/menu - skipping To prevent duplicate")
                #End If
                Return
            End If
            
            ' Now proceed with normal run logic
            #If DEBUG Then
            Console.WriteLine("OnRunProject: Proceeding with run logic")
            #End If
            
            ' Check if we should build before run based on settings
            If pSettingsManager IsNot Nothing AndAlso pSettingsManager.BuildBeforeRun Then
                #If DEBUG Then
                Console.WriteLine("BuildBeforeRun Is enabled - checking If build Is needed")
                #End If
                
                ' Check if project needs building
                Dim lNeedsBuild As Boolean = False
                
                ' Check if any files have been modified since last build
                If HasModifiedFiles() Then
                    lNeedsBuild = True
                    #If DEBUG Then
                    Console.WriteLine("Project has modified files - build needed")
                    #End If
                End If
                
                ' Check if no build output exists
                If Not lNeedsBuild AndAlso Not HasBuildOutput() Then
                    lNeedsBuild = True
                    #If DEBUG Then
                    Console.WriteLine("No build output found - build needed")
                    #End If
                End If
                
                If lNeedsBuild Then
                    ' Call BuildAndRun instead of just RunProject
                    #If DEBUG Then
                    Console.WriteLine("Calling BuildAndRun from OnRunProject")
                    #End If
                    BuildAndRun()
                Else
                    ' Project is up to date, just run it
                    #If DEBUG Then
                    Console.WriteLine("Project Is up To Date - running without build")
                    #End If
                    Task.Run(Async Function()
                        Await RunProject()
                        Return Nothing
                    End Function)
                End If
            Else
                ' BuildBeforeRun is disabled or not set, just run
                #If DEBUG Then
                Console.WriteLine("BuildBeforeRun Is disabled - running without build check")
                #End If
                Task.Run(Async Function()
                    Await RunProject()
                    Return Nothing
                End Function)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnRunProject error: {ex.Message}")
            ShowError("Run error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles Build and Run command (F5) - prevents multiple builds
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Event arguments</param>
    Public Sub OnBuildAndRun(vSender As Object, vArgs As EventArgs)
        Try
            #If DEBUG Then
            Console.WriteLine("OnBuildAndRun called")
            #End If
            
            ' Check if already building using both flags
            If pIsBuildingNow Then
                #If DEBUG Then
                Console.WriteLine("OnBuildAndRun: Already building (pIsBuildingNow check)")
                #End If
                Return
            End If
            
            If pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding Then
                #If DEBUG Then
                Console.WriteLine("OnBuildAndRun: Build already in progress (BuildManager check)")
                #End If
                ShowInfo("Build in Progress", "A build Is already in progress.")
                Return
            End If
            
            ' Call BuildAndRun which handles everything
            BuildAndRun()
            
        Catch ex As Exception
            Console.WriteLine($"OnBuildAndRun error: {ex.Message}")
            ShowError("Build and Run error", ex.Message)
        End Try
    End Sub
        
    Public Sub OnConfigureBuild(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Build.vb
            ConfigureBuild()
        Catch ex As Exception
            Console.WriteLine($"OnConfigureBuild error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Tools Menu Events =====
    Public Sub OnExternalTools(vSender As Object, vArgs As EventArgs)
        Try
            ' TODO: Implement external tools
            ShowInfo("External Tools", "External tools Not yet implemented.")
        Catch ex As Exception
            Console.WriteLine($"OnExternalTools error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnTerminal(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Terminal.vb
            ShowTerminalPanel()
        Catch ex As Exception
            Console.WriteLine($"OnTerminal error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnGitCommit(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Git.vb
            ShowGitCommitDialog()
        Catch ex As Exception
            Console.WriteLine($"OnGitCommit error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnGitStatus(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.Git.vb
            ShowGitStatus()
        Catch ex As Exception
            Console.WriteLine($"OnGitStatus error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnUpdateProjectKnowledge(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.AI.vb
            UpdateProjectKnowledge()
        Catch ex As Exception
            Console.WriteLine($"OnUpdateProjectKnowledge error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnGenerateCode(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.AI.vb
            ShowGenerateCodeDialog()
        Catch ex As Exception
            Console.WriteLine($"OnGenerateCode error: {ex.Message}")
        End Try
    End Sub
    
    Public Sub OnAISettings(vSender As Object, vArgs As EventArgs)
        Try
            ' Implemented in MainWindow.AI.vb
            ShowAISettings()
        Catch ex As Exception
            Console.WriteLine($"OnAISettings error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Help Menu Events =====

    ''' <summary>
    ''' Handles View Help menu item - opens help in a center tab
    ''' </summary>
    Public Sub OnViewHelp(vSender As Object, vArgs As EventArgs)
        Try
            ' Open help in a new tab instead of bottom panel
            OpenHelpTab()
        Catch ex As Exception
            Console.WriteLine($"OnViewHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Shows API documentation in a help tab
    ''' </summary>
    Public Sub OnApiDocumentation(vSender As Object, vArgs As EventArgs)
        Try
            ShowDotNetApiHelp()
        Catch ex As Exception
            Console.WriteLine($"OnApiDocumentation error: {ex.Message}")
        End Try
    End Sub
   
    Public Sub OnAbout(vSender As Object, vArgs As EventArgs)
        Try
            Dim lAboutDialog As New AboutDialog()
            
            ' FIXED: Use actual application version instead of hardcoded
            lAboutDialog.ProgramName = ApplicationVersion.Title
            lAboutDialog.Version = ApplicationVersion.VersionString  ' Shows Major.Minor.Build from actual AssemblyInfo
            lAboutDialog.Copyright = ApplicationVersion.Copyright
            lAboutDialog.Comments = StringResources.Instance.GetString(StringResources.KEY_ABOUT_TEXT)
            lAboutDialog.Website = "https://github.com/jamesplotts/simpleide"
            lAboutDialog.WebsiteLabel = "Project Website"
            lAboutDialog.LicenseType = Gtk.License.MitX11
            lAboutDialog.Authors = {"James Duane Plotts"}
            
            ' Set icon
            Try
                Using lStream As System.IO.Stream = GetType(MainWindow).Assembly.GetManifestResourceStream("SimpleIDE.icon.png")
                    If lStream IsNot Nothing Then
                        lAboutDialog.Logo = New Gdk.Pixbuf(lStream)
                    End If
                End Using
            Catch ex As Exception
                Console.WriteLine($"error loading about dialog logo: {ex.Message}")
            End Try
            
            lAboutDialog.TransientFor = Me
            lAboutDialog.Run()
            lAboutDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnAbout error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the "Project -> Add Existing Item..." menu command
    ''' </summary>
    ''' <remarks>
    ''' Delegates to CustomDrawProjectExplorer.OnContextMenuAddExistingItem instead of
    ''' maintaining a separate implementation here - the old standalone implementation called
    ''' pProjectExplorer.LoadProjectFromManager directly, which (same bug as OnAddNewItem in
    ''' MainWindow.Project.vb) discards the multi-project solution tree whenever a .sln is
    ''' loaded, since it never checks whether a SolutionManager is active.
    ''' </remarks>
    Private Sub OnAddExistingItem(vSender As Object, vArgs As EventArgs)
        Try
            pProjectExplorer?.OnContextMenuAddExistingItem(vSender, vArgs)
        Catch ex As Exception
            Console.WriteLine($"OnAddExistingItem error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnProjectProperties(vSender As Object, vArgs As EventArgs)
        Try
            ' AssemblySettingsEditor (Title/Description/Company/Version/OutputType/
            ' TargetFramework/etc.) already covers what this menu item promises - it was
            ' otherwise completely unreachable from any menu, button, or shortcut
            ShowAssemblySettings()
        Catch ex As Exception
            Console.WriteLine($"OnProjectProperties error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnToggleFullScreen(vSender As Object, vArgs As EventArgs)
        Try
            If pIsFullScreen Then
                Unfullscreen()
                pIsFullScreen = False
            Else
                Fullscreen()
                pIsFullScreen = True
            End If
        Catch ex As Exception
            Console.WriteLine($"OnToggleFullScreen error: {ex.Message}")
            ShowError("Toggle Full Screen failed", ex.Message)
        End Try
    End Sub

        
    ''' <summary>
    ''' Handles Quick Find from Clipboard button click - opens Find panel, pastes clipboard text, and executes Find All
    ''' </summary>
    ''' <param name="vSender">The sender of the event</param>
    ''' <param name="vArgs">Event arguments</param>
    Private Sub OnQuickFindFromClipboard(vSender As Object, vArgs As EventArgs)
        Try
            #If DEBUG Then
            Console.WriteLine("OnQuickFindFromClipboard: Starting quick find from clipboard operation")
            #End If
            
            ' Get clipboard text
            Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
            Dim lClipboardText As String = lClipboard.WaitForText()
            
            If String.IsNullOrEmpty(lClipboardText) Then
                #If DEBUG Then
                Console.WriteLine("OnQuickFindFromClipboard: Clipboard Is empty")
                #End If
                ShowError("Quick Find", "Clipboard Is empty. Please copy some text To search for.")
                Return
            End If
            
            #If DEBUG Then
            Console.WriteLine($"OnQuickFindFromClipboard: Got clipboard text: {lClipboardText.Substring(0, Math.Min(50, lClipboardText.Length))}...")
            #End If
            
            ' Show bottom panel if hidden
            If Not pBottomPanelVisible Then
                #If DEBUG Then
                Console.WriteLine("OnQuickFindFromClipboard: Showing bottom panel")
                #End If
                ToggleBottomPanel()
            End If
            
            ' Switch to Find Results tab
            If pBottomPanelManager IsNot Nothing AndAlso pFindPanel IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine("OnQuickFindFromClipboard: Switching To Find Results tab")
                #End If
                pBottomPanelManager.ShowTabForPanel(pFindPanel)
            End If
            
            ' Set project root if available
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
                #If DEBUG Then
                Console.WriteLine($"OnQuickFindFromClipboard: Setting project root To {lProjectDir}")
                #End If
                pFindPanel.SetProjectRoot(lProjectDir)
            End If
            
            ' Set the clipboard text into the Find field
            #If DEBUG Then
            Console.WriteLine("OnQuickFindFromClipboard: Setting search text from clipboard")
            #End If
            pFindPanel.SetSearchText(lClipboardText)
            
            ' Set search scope to Entire Project for better results
            #If DEBUG Then
            Console.WriteLine("OnQuickFindFromClipboard: Setting search scope To Entire Project")
            #End If
            pFindPanel.SetSearchScope(FindReplacePanel.SearchScope.eProject)
            
            ' Focus the find panel (without selecting text since we want to keep what we just set)
            pFindPanel.FocusSearchEntryNoSelect()
            
            ' Execute Find All operation
            #If DEBUG Then
            Console.WriteLine("OnQuickFindFromClipboard: Executing Find All")
            #End If
            pFindPanel.OnFind(Nothing, Nothing)
            
            #If DEBUG Then
            Console.WriteLine("OnQuickFindFromClipboard: Quick find from clipboard completed successfully")
            #End If
            
        Catch ex As Exception
            Console.WriteLine($"OnQuickFindFromClipboard error: {ex.Message}")
            ShowError("Quick Find error", $"Failed To perform quick find from clipboard: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the Shown event when no project is specified
    ''' </summary>
    ''' <param name="sender">Event sender</param>
    ''' <param name="e">Event arguments</param>
    Private Sub OnWindowShownNoProject(sender As Object, e As EventArgs)
        Try
            ' Unhook the event so it doesn't fire again
            RemoveHandler Me.Shown, AddressOf OnWindowShownNoProject
            
            ' Check for auto-detect project in current directory
            Dim lCurrentDir As String = Directory.GetCurrentDirectory()
            Dim lProjectFiles() As String = Directory.GetFiles(lCurrentDir, "*.vbproj")
            
            If lProjectFiles.Length = 1 Then
                ' Single project found - auto-load it
                #If DEBUG Then
                Console.WriteLine($"Auto-detected project: {lProjectFiles(0)}")
                #End If
                
                ' Use idle handler to let UI settle first
                GLib.Idle.Add(Function()
                    LoadProjectEnhanced(lProjectFiles(0))
                    Return False
                End Function)
                
            ElseIf lProjectFiles.Length > 1 Then
                ' Multiple projects - let user choose
                #If DEBUG Then
                Console.WriteLine($"Multiple projects found in {lCurrentDir}")
                #End If
                ' Could show a selection dialog here
                
            Else
                ' No projects found - check for recent projects
                If pSettingsManager IsNot Nothing AndAlso pSettingsManager.RecentProjects.Count > 0 Then
                    Dim lMostRecent As String = pSettingsManager.RecentProjects(0)
                    If File.Exists(lMostRecent) Then
                        ' Optionally auto-load most recent project
                        ' For now, just log it
                        #If DEBUG Then
                        Console.WriteLine($"Most recent project: {lMostRecent}")
                        #End If
                    End If
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowShownNoProject error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Shows online help resources in a help tab
    ''' </summary>
    Public Sub OnShowOnlineHelp(vSender As Object, vArgs As EventArgs)
        Try
            OpenHelpTab()
        Catch ex As Exception
            Console.WriteLine($"OnShowOnlineHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Shows GTK# documentation in a help tab
    ''' </summary>
    Public Sub OnShowGtkHelp(vSender As Object, vArgs As EventArgs)
        Try
            ShowGtkSharpHelp()
        Catch ex As Exception
            Console.WriteLine($"OnShowGtkHelp error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Shows .NET documentation in a help tab
    ''' </summary>
    Public Sub OnShowDotNetHelp(vSender As Object, vArgs As EventArgs)
        Try
            ShowVBNetHelp()
        Catch ex As Exception
            Console.WriteLine($"OnShowDotNetHelp error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Ensures all notebooks are properly initialized and visible
    ''' </summary>
    ''' <remarks>
    ''' Called after window is realized to fix any notebook visibility issues
    ''' </remarks>
    Private Sub EnsureNotebooksReady()
        Try
            #If DEBUG Then
            Console.WriteLine("=== EnsureNotebooksReady Starting ===")
            #End If
            
            ' Fix main notebook (editor tabs)
            If TypeOf pNotebook Is CustomDrawNotebook Then
                Dim lMainNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                
                #If DEBUG Then
                Console.WriteLine($"Main notebook: {lMainNotebook.NPages} pages")
                #End If
                
                ' Force refresh to ensure events are enabled
                lMainNotebook.ForceRefresh()
                
                ' Show all to ensure visibility
                lMainNotebook.ShowAll()
                
                ' Diagnose state for debugging
                #If DEBUG Then
                Console.WriteLine("Main notebook state:")
                #End If
                lMainNotebook.DiagnoseNotebookState()
                
                ' Ensure Welcome tab is visible if it's the only tab
                If lMainNotebook.NPages = 1 Then
                    Dim lTabLabel As String = lMainNotebook.GetTabLabel(0)
                    If lTabLabel = "Welcome" Then
                        #If DEBUG Then
                        Console.WriteLine("Ensuring Welcome tab Is active")
                        #End If
                        lMainNotebook.CurrentPage = 0
                        
                        ' Force the widget to be visible
                        Dim lWidget As Widget = lMainNotebook.GetNthPage(0)
                        If lWidget IsNot Nothing Then
                            lWidget.ShowAll()
                            lWidget.Visible = True
                        End If
                    End If
                ElseIf lMainNotebook.NPages = 0 Then
                    ' No tabs at all - show welcome
                    #If DEBUG Then
                    Console.WriteLine("No tabs in main notebook - showing Welcome")
                    #End If
                    ShowWelcomeTab()
                End If
            End If
            
            ' Fix left notebook (project/object explorers)
            If TypeOf pLeftNotebook Is CustomDrawNotebook Then
                Dim lLeftNotebook As CustomDrawNotebook = DirectCast(pLeftNotebook, CustomDrawNotebook)
                
                #If DEBUG Then
                Console.WriteLine($"Left notebook: {lLeftNotebook.NPages} pages")
                #End If
                
                ' Force refresh to ensure events are enabled
                lLeftNotebook.ForceRefresh()
                
                ' Show all to ensure visibility
                lLeftNotebook.ShowAll()
                
                ' Diagnose state for debugging
                #If DEBUG Then
                Console.WriteLine("Left notebook state:")
                #End If
                lLeftNotebook.DiagnoseNotebookState()
                
                ' Ensure Project tab is active by default
                If lLeftNotebook.NPages > 0 Then
                    #If DEBUG Then
                    Console.WriteLine("Setting Project tab As active")
                    #End If
                    lLeftNotebook.CurrentPage = 0
                    
                    ' Force the widget to be visible
                    Dim lWidget As Widget = lLeftNotebook.GetNthPage(0)
                    If lWidget IsNot Nothing Then
                        lWidget.ShowAll()
                        lWidget.Visible = True
                    End If
                End If
            End If
            
            ' Fix bottom panel notebook
            If pBottomPanelManager IsNot Nothing Then
                Dim lBottomNotebook As CustomDrawNotebook = TryCast(pBottomPanelManager.GetNotebook(), CustomDrawNotebook)
                If lBottomNotebook IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"Bottom notebook: {lBottomNotebook.NPages} pages")
                    #End If
                    
                    ' Force refresh
                    lBottomNotebook.ForceRefresh()
                    
                    ' Show all tabs (panel itself may be hidden)
                    lBottomNotebook.ShowAll()
                    
                    ' Diagnose state
                    #If DEBUG Then
                    Console.WriteLine("Bottom notebook state:")
                    #End If
                    lBottomNotebook.DiagnoseNotebookState()
                    
                    ' Ensure first tab is active
                    If lBottomNotebook.NPages > 0 Then
                        lBottomNotebook.CurrentPage = 0
                    End If
                End If
            End If
            
            #If DEBUG Then
            Console.WriteLine("=== EnsureNotebooksReady Complete ===")
            #End If
            
        Catch ex As Exception
            Console.WriteLine($"EnsureNotebooksReady error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Hook this to window Realized event to fix notebook issues
    ''' </summary>
    Private Sub OnWindowRealizedForNotebooks(vSender As Object, vArgs As EventArgs)
        Try
            #If DEBUG Then
            Console.WriteLine("Window realized - ensuring notebooks are ready")
            #End If
            
            ' Use idle handler to let GTK complete initialization
            GLib.Idle.Add(Function()
                EnsureNotebooksReady()
                Return False ' Run only once
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowRealizedForNotebooks error: {ex.Message}")
        End Try
    End Sub
    
End Class