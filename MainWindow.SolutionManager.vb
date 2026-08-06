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
                LoadProjectEnhanced(lStartupPath)
            End If

        Catch ex As Exception
            Console.WriteLine($"LoadSolutionEnhanced error: {ex.Message}")
            ShowError("Open Solution Error", ex.Message)
        End Try
    End Sub

End Class
