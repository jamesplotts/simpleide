' MainWindow.SolutionManager.vb - Multi-project .sln solution loading
Imports Gtk
Imports System
Imports System.Threading.Tasks
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models

Partial Public Class MainWindow

    ''' <summary>
    ''' Owns every ProjectManager instance for the currently loaded solution (one per member
    ''' project) once a .sln has been opened; Nothing when only a plain single .vbproj is open
    ''' </summary>
    Private pSolutionManager As SolutionManager

    ''' <summary>
    ''' One-shot flag consumed by OnProjectFileListLoaded (MainWindow.ProjectManager.vb) - set
    ''' just before the startup project's own LoadProjectEnhanced call inside
    ''' LoadSolutionEnhanced, so that handler can skip populating Project Explorer with the
    ''' startup project's single-project tree (which would otherwise be visible for the ~1
    ''' second before OnStartupProjectAllFilesParsed replaces it with the real multi-root
    ''' solution tree - a visible "loads the project, then the solution" flash). Consumed
    ''' (reset to False) the moment it's checked, so any LATER plain single-project open
    ''' (even after a solution was previously loaded) is unaffected
    ''' </summary>
    Private pSolutionStartupLoadPending As Boolean = False

    ''' <summary>
    ''' Handles the "Open Solution..." menu command
    ''' </summary>
    Public Sub OnOpenSolution(vSender As Object, vArgs As EventArgs)
        Try
            ' Close all open editors from the current project (prompts to save/discard, may cancel)
            If Not CloseAllTabs() Then Return

            Dim lDialog As New FileChooserDialog(
                "Select Solution File",
                Me,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept
            )

            Dim lSolutionFilter As New FileFilter()
            lSolutionFilter.Name = "Solution Files (*.sln)"
            lSolutionFilter.AddPattern("*.sln")
            lDialog.AddFilter(lSolutionFilter)

            If lDialog.Run() = CInt(ResponseType.Accept) Then
                LoadSolutionEnhanced(lDialog.FileName)
            End If

            lDialog.Destroy()

        Catch ex As Exception
            Console.WriteLine($"OnOpenSolution error: {ex.Message}")
            ShowError("Open solution error", ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Loads a .sln solution - every member project into its own SolutionManager-owned
    ''' ProjectManager instance, plus the startup (first-listed) project through the existing,
    ''' unmodified single-project open flow so every already-wired widget/handler (Project
    ''' Explorer, Object Explorer, editor tabs) behaves exactly as it does for a plain project
    ''' open today
    ''' </summary>
    ''' <remarks>
    ''' Deliberately loads the startup project TWICE - once (lightly, synchronously) inside
    ''' SolutionManager purely to compute the cross-project dependency graph, and once (fully,
    ''' asynchronously) via the existing LoadProjectEnhanced, which is what actually drives the
    ''' visible UI. Doing this instead of reusing MainWindow's own pProjectManager instance
    ''' inside SolutionManager avoids a race between the two independent load paths (one
    ''' synchronous on the UI thread, one asynchronous on a background Task) touching the same
    ''' object concurrently.
    '''
    ''' SolutionManager.LoadSolution() itself is NOT lightweight - ProjectManager.LoadProject()
    ''' (called once per member project) fully Roslyn-parses every one of that project's source
    ''' files synchronously (EnsureAllFilesLoaded), so calling it directly on the UI thread
    ''' would freeze the window for the whole solution load and never let the "Loading
    ''' projects..." placeholder (ShowSolutionLoadingPlaceholder) actually paint. Run on a
    ''' background Task instead, with a progress callback marshaled back to the UI thread via
    ''' Application.Invoke for status-bar feedback; pSolutionManager itself is only assigned
    ''' once back on the UI thread with a fully-populated instance, so nothing else in the app
    ''' can observe a half-initialized SolutionManager mid-load.
    ''' </remarks>
    Private Sub LoadSolutionEnhanced(vSolutionPath As String)
        Try
            #If DEBUG Then
            Console.WriteLine($"LoadSolutionEnhanced: Loading solution: {vSolutionPath}")
            #End If

            Dim lSolutionName As String = System.IO.Path.GetFileNameWithoutExtension(vSolutionPath)
            UpdateStatusBar($"Opening solution '{lSolutionName}'...")
            pProjectExplorer?.ShowSolutionLoadingPlaceholder(lSolutionName)
            ShowProgressBar(True)
            UpdateProgressBar(0)

            Dim lNewSolutionManager As New SolutionManager()

            Task.Run(Sub()
                Try
                    Dim lSuccess As Boolean = lNewSolutionManager.LoadSolution(vSolutionPath, Nothing,
                        Sub(vProjectName As String, vIndex As Integer, vTotal As Integer)
                            Application.Invoke(Sub()
                                Try
                                    UpdateStatusBar($"Loading project {vIndex} of {vTotal}: {vProjectName}...")
                                    UpdateProgressBar(CInt((vIndex - 1) * 100.0 / vTotal))
                                Catch ex As Exception
                                    Console.WriteLine($"LoadSolutionEnhanced progress callback error: {ex.Message}")
                                End Try
                            End Sub)
                        End Sub)

                    Application.Invoke(Sub()
                        Try
                            If Not lSuccess OrElse lNewSolutionManager.AllProjects.Count = 0 Then
                                ShowProgressBar(False)
                                pProjectExplorer?.ClearProject()
                                UpdateStatusBar("Open Solution Failed")
                                ShowError("Open Solution Failed", $"Failed to load any projects from: {vSolutionPath}")
                                Return
                            End If

                            pSolutionManager = lNewSolutionManager

                            #If DEBUG Then
                            Console.WriteLine($"LoadSolutionEnhanced: Loaded {pSolutionManager.AllProjects.Count} project(s), dependency order:")
                            #End If
                            for each lMember in pSolutionManager.AllProjects
                                #If DEBUG Then
                                Console.WriteLine($"  {lMember.CurrentProjectName} ({lMember.CurrentProjectPath})")
                                #End If
                            Next

                            UpdateProgressBar(100)
                            ShowProgressBar(False)
                            UpdateStatusBar($"Solution loaded: {pSolutionManager.AllProjects.Count} project(s) - parsing source files...")

                            Dim lStartupPath As String = pSolutionManager.StartupProject?.CurrentProjectPath
                            If Not String.IsNullOrEmpty(lStartupPath) Then
                                ' LoadProjectEnhanced's own async load fires ProjectFileListLoaded
                                ' partway through - OnProjectFileListLoaded skips populating
                                ' Project Explorer with the startup project's own single-project
                                ' tree while pSolutionStartupLoadPending is set (see its own doc
                                ' comment), leaving the "Loading projects..." placeholder up
                                ' until AllFilesParseCompleted fires and the real multi-root
                                ' solution tree replaces it wholesale
                                ' RemoveHandler first: if a prior solution load's own AddHandler
                                ' here was never matched by a RemoveHandler (e.g. that load's
                                ' AllFilesParseCompleted never fired), this call would otherwise
                                ' double-subscribe OnStartupProjectAllFilesParsed
                                RemoveHandler pProjectManager.AllFilesParseCompleted, AddressOf OnStartupProjectAllFilesParsed
                                AddHandler pProjectManager.AllFilesParseCompleted, AddressOf OnStartupProjectAllFilesParsed
                                pSolutionStartupLoadPending = True
                                LoadProjectEnhanced(lStartupPath)
                            End If

                        Catch ex As Exception
                            Console.WriteLine($"LoadSolutionEnhanced (completion) error: {ex.Message}")
                            ShowProgressBar(False)
                            ShowError("Open Solution Error", ex.Message)
                        End Try
                    End Sub)

                Catch ex As Exception
                    Application.Invoke(Sub()
                        Console.WriteLine($"LoadSolutionEnhanced (background) error: {ex.Message}")
                        ShowProgressBar(False)
                        pProjectExplorer?.ClearProject()
                        UpdateStatusBar("Open Solution Failed")
                        ShowError("Open Solution Error", ex.Message)
                    End Sub)
                End Try
            End Sub)

        Catch ex As Exception
            Console.WriteLine($"LoadSolutionEnhanced error: {ex.Message}")
            ShowError("Open Solution Error", ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' One-shot handler that applies the full multi-root solution tree to Project Explorer
    ''' once the startup project's own single-project load (kicked off by LoadSolutionEnhanced)
    ''' has fully finished
    ''' </summary>
    ''' <remarks>
    ''' Confirmed via direct testing (stack-trace-dumping LoadProjectFromManager itself) that
    ''' a plain Application.Invoke here is NOT sufficient: OnProjectFileListLoaded's own
    ''' Application.Invoke-queued LoadProjectFromManager() call (queued much earlier, when
    ''' ProjectFileListLoaded first raises) does not reliably run before this one, because
    ''' several existing code paths in this app (ThemeManager.ForceGlobalRefresh,
    ''' UpdateProgressBar) call Gtk.Application.RunIteration() reentrantly from inside other
    ''' Application.Invoke callbacks to force synchronous UI updates during long operations -
    ''' this breaks the normal FIFO ordering GTK's invoke queue would otherwise guarantee, so
    ''' a callback queued earlier can end up processed later than one queued after it. A short
    ''' settling delay (matching the existing GLib.Timeout.Add(1000, ...) already used in
    ''' OnAllFilesParseCompleted for the same "let the reentrant pumping finish" reason) is
    ''' the pragmatic fix - reordering the reentrant RunIteration calls themselves would be a
    ''' much larger, riskier change unrelated to solution support.
    ''' </remarks>
    Private Sub OnStartupProjectAllFilesParsed(vFileCount As Integer, vTotalMilliseconds As Double)
        Try
            RemoveHandler pProjectManager.AllFilesParseCompleted, AddressOf OnStartupProjectAllFilesParsed
            Dim lSolutionManager As SolutionManager = pSolutionManager
            If lSolutionManager IsNot Nothing Then
                GLib.Timeout.Add(750, New GLib.TimeoutHandler(Function()
                    Try
                        pProjectExplorer?.LoadSolutionFromManager(lSolutionManager)
                        pObjectExplorer?.SetSolutionManager(lSolutionManager)
                        pFindPanel?.SetSolutionManager(lSolutionManager)
                        pGitPanel?.SetSolutionManager(lSolutionManager)
                        UpdateStatusBar($"Solution ready: {lSolutionManager.AllProjects.Count} project(s) loaded")
                    Catch ex As Exception
                        Console.WriteLine($"OnStartupProjectAllFilesParsed (settled apply) error: {ex.Message}")
                    End Try
                    Return False
                End Function))
            End If
        Catch ex As Exception
            Console.WriteLine($"OnStartupProjectAllFilesParsed error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles the "Solution Settings..." context-menu request from Project Explorer's
    ''' solution root node
    ''' </summary>
    Private Sub OnSolutionSettingsRequested()
        Try
            OpenSolutionSettingsTab()
        Catch ex As Exception
            Console.WriteLine($"OnSolutionSettingsRequested error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Opens (or switches to) the read-only Solution Settings tab showing every loaded
    ''' project in build/dependency order
    ''' </summary>
    ''' <remarks>
    ''' Follows the same pattern as OpenAssemblySettingsEditor (MainWindow.AssemblySettings.vb) -
    ''' a settings view, not a text editor, so it deliberately does NOT implement IEditor;
    ''' registered in pOpenTabs with Editor = Nothing and IsSpecialTab = True purely so it
    ''' participates in normal tab close/dispose handling
    ''' </remarks>
    Private Sub OpenSolutionSettingsTab()
        Try
            If pSolutionManager Is Nothing Then
                ShowError("No Solution Loaded", "Open a solution first (File > Open Solution...).")
                Return
            End If

            for each lTabEntry in pOpenTabs
                If lTabEntry.Key = "Solution Settings" Then
                    SwitchToTab(lTabEntry.Key)
                    Return
                End If
            Next

            Dim lSettingsTab As New SolutionSettingsTab(pSolutionManager, pThemeManager)

            Dim lTabInfo As New TabInfo() With {
                .FilePath = "Solution Settings",
                .Editor = Nothing,
                .EditorContainer = lSettingsTab,
                .Modified = False,
                .IsSpecialTab = True
            }

            Dim lPageIndex As Integer = pNotebook.AppendPage(lSettingsTab, "Solution Settings")
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex

            pOpenTabs("Solution Settings") = lTabInfo
            UpdateStatusBar("Opened solution settings")

        Catch ex As Exception
            Console.WriteLine($"OpenSolutionSettingsTab error: {ex.Message}")
            ShowError("Solution Settings Error", ex.Message)
        End Try
    End Sub

End Class
