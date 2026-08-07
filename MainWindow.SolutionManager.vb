' MainWindow.SolutionManager.vb - Multi-project .sln solution loading
Imports Gtk
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

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
    ''' object concurrently. Multi-root UI/per-tab project association (Phase 2) will replace
    ''' this with a single, solution-aware load path - this is intentionally the simpler,
    ''' lower-risk Phase 1 slice: prove SolutionManager's own loading/dependency-graph logic is
    ''' correct without touching the existing single-project UI machinery at all.
    ''' </remarks>
    Private Sub LoadSolutionEnhanced(vSolutionPath As String)
        Try
            Console.WriteLine($"LoadSolutionEnhanced: Loading solution: {vSolutionPath}")

            pSolutionManager = New SolutionManager()

            If Not pSolutionManager.LoadSolution(vSolutionPath) Then
                pSolutionManager = Nothing
                ShowError("Open Solution Failed", $"Failed to load any projects from: {vSolutionPath}")
                Return
            End If

            Console.WriteLine($"LoadSolutionEnhanced: Loaded {pSolutionManager.AllProjects.Count} project(s), dependency order:")
            for each lMember in pSolutionManager.AllProjects
                Console.WriteLine($"  {lMember.CurrentProjectName} ({lMember.CurrentProjectPath})")
            Next

            UpdateStatusBar($"Solution loaded: {pSolutionManager.AllProjects.Count} project(s)")

            Dim lStartupPath As String = pSolutionManager.StartupProject?.CurrentProjectPath
            If Not String.IsNullOrEmpty(lStartupPath) Then
                ' LoadProjectEnhanced's own async load fires ProjectFileListLoaded partway
                ' through, whose handler (OnProjectFileListLoaded) populates Project Explorer
                ' with just the startup project's single-project tree via
                ' LoadProjectFromManager() - wait for AllFilesParseCompleted (which can only
                ' fire after that already happened) before replacing it with the full
                ' multi-root solution tree, so the override always lands last regardless of
                ' background-task timing
                AddHandler pProjectManager.AllFilesParseCompleted, AddressOf OnStartupProjectAllFilesParsed
                pSolutionStartupLoadPending = True
                LoadProjectEnhanced(lStartupPath)
            End If

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

End Class
