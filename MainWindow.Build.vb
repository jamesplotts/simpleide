' MainWindow.Build.vb - Enhanced build system with version integration
Imports Gtk
Imports System
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces


Partial Public Class MainWindow
    
    ' =x==== Build System Integration =====
    
    ''' <summary>
    ''' Event raised when build comp letes
    ''' </summary>
    Public Event BuildCompleted(vSuccess As Boolean)
    
    Private pBuildConfiguration As BuildConfiguration = Nothing
    Private pIsDebugging As Boolean = False
    Private pDebugProcess As Process = Nothing
    Private pBuildManager As BuildManager = Nothing

    Private pVersionIncrementedThisSession As Boolean = False

    
    ''' <summary>
    ''' Initialize build system components
    ''' </summary>
    Private Sub InitializeBuildSystem()
        Try
            #If DEBUG Then
            Console.WriteLine("InitializeBuildSystem: Starting...")
            #End If
            
            ' Create build manager if needed
            If pBuildManager Is Nothing Then
                #If DEBUG Then
                Console.WriteLine("InitializeBuildSystem: Creating BuildManager")
                #End If
                pBuildManager = New BuildManager()
                
                ' Add event handlers for build manager
                AddHandler pBuildManager.BuildStarted, AddressOf OnBuildStarted
                AddHandler pBuildManager.BuildCompleted, AddressOf OnBuildCompleted
                AddHandler pBuildManager.OutputReceived, AddressOf OnBuildOutput
                AddHandler pBuildManager.ErrorReceived, AddressOf OnBuildError
            End If
            
            ' Create build configuration if needed
            If pBuildConfiguration Is Nothing Then
                #If DEBUG Then
                Console.WriteLine("InitializeBuildSystem: Creating BuildConfiguration")
                #End If
                pBuildConfiguration = New BuildConfiguration()
                LoadBuildConfiguration()
            End If
            
            ' CRITICAL: Set the configuration on the build manager
            If pBuildManager IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine($"InitializeBuildSystem: Setting BuildManager.Configuration")
                #End If
                pBuildManager.Configuration = pBuildConfiguration
            End If
            
            #If DEBUG Then
            Console.WriteLine("InitializeBuildSystem: Complete")
            #End If
        Catch ex As Exception
            Console.WriteLine($"InitializeBuildSystem error: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
        End Try
    End Sub

    Private pIsBuildingNow As Boolean

    ''' <summary>
    ''' Name of the operation currently running against pBuildManager ("Build", "Rebuild",
    ''' or "Clean") - set immediately before each Task.Run(...) kicks off so the shared
    ''' OnBuildCompleted handler can report the right verb instead of always saying "Build",
    ''' since CleanProject reuses the same BuildManager.BuildCompleted event as an actual build.
    ''' </summary>
    Private pCurrentBuildOperation As String = "Build"

    ''' <summary>
    ''' True if a build/rebuild/clean is currently running via either BuildManager instance
    ''' this class uses - the single-project pBuildManager (shared by BuildProject/
    ''' RebuildProject/CleanProject) or the separate pSolutionBuildManager (used only by
    ''' BuildSolution, kept as its own instance deliberately - see pSolutionBuildManager's
    ''' doc comment - specifically so its per-project BuildStarted/BuildCompleted don't fire
    ''' pBuildManager's single-project handlers).
    ''' </summary>
    ''' <remarks>
    ''' Each of the four build entry points used to guard reentrancy with only its own
    ''' flag/manager: BuildSolution checked only pIsBuildingNow (never pBuildManager.
    ''' IsBuilding), while RebuildProject/CleanProject checked only pBuildManager.IsBuilding
    ''' (never pIsBuildingNow, and never pSolutionBuildManager.IsBuilding either, since that
    ''' manager didn't exist yet from their point of view). Net effect: Build Solution could
    ''' start while a Rebuild or Clean was already running, and a Rebuild or Clean could start
    ''' while a Build Solution was already running - two builds running concurrently against
    ''' overlapping project directories, with real risk of file-lock errors or corrupted
    ''' obj/bin state. All four entry points now check this single, complete condition.
    ''' </remarks>
    Private Function IsAnyBuildInProgress() As Boolean
        Return pIsBuildingNow OrElse
               (pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding) OrElse
               (pSolutionBuildManager IsNot Nothing AndAlso pSolutionBuildManager.IsBuilding)
    End Function

    ''' <summary>
    ''' Build the current project - Main entry point for F6 and build operations
    ''' </summary>
    ''' <remarks>
    ''' Ensures pIsBuildingNow flag is properly managed on all exit paths
    ''' </remarks>
    Public Sub BuildProject()
        Try
            ' Check if already building - see IsAnyBuildInProgress's remarks for why this
            ' needs to check more than just this method's own flag
            If IsAnyBuildInProgress() Then
                #If DEBUG Then
                Console.WriteLine("BuildProject: Already building (early exit)")
                #End If
                Return
            End If

            ' Set flag immediately
            pIsBuildingNow = True
            pCurrentBuildOperation = "Build"

            ' DEBUG: Simple console output to verify method is called
            #If DEBUG Then
            Console.WriteLine("===============================================")
            #End If
            #If DEBUG Then
            Console.WriteLine("BUILD PROJECT CALLED!")
            #End If
            #If DEBUG Then
            Console.WriteLine($"Time: {DateTime.Now:HH:mm:ss.fff}")
            #End If
            #If DEBUG Then
            Console.WriteLine($"Project Path: {pCurrentProject}")
            #End If
            #If DEBUG Then
            Console.WriteLine($"BuildManager Is Nothing: {pBuildManager Is Nothing}")
            #End If
            #If DEBUG Then
            Console.WriteLine($"BuildConfiguration Is Nothing: {pBuildConfiguration Is Nothing}")
            #End If
            #If DEBUG Then
            Console.WriteLine("===============================================")
            #End If
             
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before building.")
                pIsBuildingNow = False ' Reset flag on early exit
                Return
            End If
    
            ' Initialize build system if needed
            If pBuildManager Is Nothing OrElse pBuildConfiguration Is Nothing Then
                #If DEBUG Then
                Console.WriteLine("BuildProject: Initializing build system")
                #End If
                InitializeBuildSystem()
            End If
            
            ' Verify initialization succeeded
            If pBuildManager Is Nothing Then
                #If DEBUG Then
                Console.WriteLine("BuildProject: ERROR - BuildManager is Nothing after initialization")
                #End If
                ShowError("Build Error", "Failed to initialize build system")
                pIsBuildingNow = False ' Reset flag on error
                Return
            End If
            
            If pBuildConfiguration Is Nothing Then
                #If DEBUG Then
                Console.WriteLine("BuildProject: ERROR - BuildConfiguration is Nothing after initialization")
                #End If
                ShowError("Build Error", "Failed to initialize build configuration")
                pIsBuildingNow = False ' Reset flag on error
                Return
            End If
    
            ' Check if already building - use the BuildManager's IsBuilding property
            If pBuildManager.IsBuilding Then
                #If DEBUG Then
                Console.WriteLine("BuildProject: Build already in progress (BuildManager check)")
                #End If
                ShowInfo("Build in Progress", "A build is already in progress.")
                pIsBuildingNow = False ' Reset flag since we're not starting a new build
                Return
            End If
    
            ' Auto-increment version if enabled
            TryIncrementVersionBeforeBuild()
    
            ' Start the build
            SetBuildButtonsEnabled(False)
            UpdateStatusBar("Building project...")
    
            ' Save all open files before building
            SaveAllFiles()
    
            ' Set project path and configuration for build manager
            #If DEBUG Then
            Console.WriteLine($"BuildProject: Setting project path = {pCurrentProject}")
            #End If
            pBuildManager.ProjectPath = pCurrentProject
            
            ' Ensure configuration is set
            #If DEBUG Then
            Console.WriteLine($"BuildProject: Setting configuration = {pBuildConfiguration.Configuration}")
            #End If
            pBuildManager.Configuration = pBuildConfiguration
    
            ' Start async build - Pass the configuration explicitly
            #If DEBUG Then
            Console.WriteLine("BuildProject: Starting async build")
            #End If
            Task.Run(Async Function() 
                Try
                    #If DEBUG Then
                    Console.WriteLine("BuildProject: Async task started")
                    #End If
                    Dim lResult = Await pBuildManager.BuildProjectAsync(pBuildConfiguration)
                    #If DEBUG Then
                    Console.WriteLine($"BuildProject: Async task completed, Success = {lResult?.Success}")
                    #End If
                    
                    ' Reset building flag when complete (on UI thread for safety)
                    Application.Invoke(Sub()
                        pIsBuildingNow = False
                        #If DEBUG Then
                        Console.WriteLine("BuildProject: pIsBuildingNow flag reset to False")
                        #End If
                    End Sub)
                    
                    Return lResult
                Catch ex As Exception
                    Console.WriteLine($"BuildProject: Async task error: {ex.Message}")
                    ' Reset flag on error (on UI thread for safety)
                    Application.Invoke(Sub()
                        pIsBuildingNow = False
                        Console.WriteLine("BuildProject: pIsBuildingNow flag reset to False (error path)")
                    End Sub)
                    Return Nothing
                End Try
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"BuildProject error: {ex.Message}")
            ShowError("Build Error", ex.Message)
            SetBuildButtonsEnabled(True)
            pIsBuildingNow = False ' Reset flag on exception
        End Try
    End Sub

    ' ===== Solution Build Orchestration (Phase 3) =====

    ''' <summary>
    ''' Dedicated BuildManager instance for solution builds - kept separate from pBuildManager
    ''' so the existing single-project handlers (OnBuildStarted/OnBuildCompleted, wired to
    ''' pBuildManager in InitializeBuildSystem) don't fire once per solution project. Those
    ''' handlers replace the Build Output panel's error/warning grids on every BuildCompleted
    ''' and unconditionally reset pIsBuildingNow to False - both wrong for a multi-project
    ''' sequential build, which needs to accumulate results across projects and only clear
    ''' pIsBuildingNow once the whole solution finishes
    ''' </summary>
    Private pSolutionBuildManager As BuildManager = Nothing

    ''' <summary>
    ''' Lazily creates the solution build manager, wiring only OutputReceived (safe to share -
    ''' it just appends text) and leaving BuildStarted/BuildCompleted unwired
    ''' </summary>
    Private Sub InitializeSolutionBuildManager()
        Try
            If pSolutionBuildManager Is Nothing Then
                pSolutionBuildManager = New BuildManager()
                AddHandler pSolutionBuildManager.OutputReceived, AddressOf OnBuildOutput
            End If

            If pBuildConfiguration Is Nothing Then
                pBuildConfiguration = New BuildConfiguration()
                LoadBuildConfiguration()
            End If

            pSolutionBuildManager.Configuration = pBuildConfiguration

        Catch ex As Exception
            Console.WriteLine($"InitializeSolutionBuildManager error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Builds every project in the currently loaded solution, in dependency order (own
    ''' project first, then whatever it references), using the existing unmodified
    ''' BuildManager.BuildProjectAsync once per project - fails fast on the first project
    ''' whose build doesn't succeed. Falls back to the plain single-project BuildProject()
    ''' when no solution is loaded, so Ctrl+Shift+B behaves sensibly either way
    ''' </summary>
    Public Sub BuildSolution()
        Try
            If IsAnyBuildInProgress() Then
                #If DEBUG Then
                Console.WriteLine("BuildSolution: Already building (early exit)")
                #End If
                Return
            End If

            If pSolutionManager Is Nothing OrElse pSolutionManager.AllProjects.Count = 0 Then
                #If DEBUG Then
                Console.WriteLine("BuildSolution: No solution loaded, falling back to BuildProject")
                #End If
                BuildProject()
                Return
            End If

            InitializeSolutionBuildManager()

            If pSolutionBuildManager Is Nothing OrElse pBuildConfiguration Is Nothing Then
                ShowError("Build Error", "Failed to initialize build system")
                Return
            End If

            pIsBuildingNow = True

            Dim lProjects As New List(Of ProjectManager)(pSolutionManager.AllProjects)

            SetBuildButtonsEnabled(False)
            UpdateStatusBar($"Building solution ({lProjects.Count} project(s))...")
            SaveAllFiles()

            If pSettingsManager IsNot Nothing AndAlso pSettingsManager.ClearOutputOnBuild Then
                pBuildOutputPanel?.ClearOutputOnly()
            End If

            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.Show()
                pBottomPanelManager.ShowTab(0) ' Build output is tab 0
            End If
            pBuildOutputPanel?.SwitchToOutputTab()

            Task.Run(Async Function() As Task
                Dim lMergedResult As New BuildResult() With {.Success = True}
                Dim lFailedProjectName As String = Nothing

                Try
                    for each lProject In lProjects
                        Dim lProjectPath As String = lProject.CurrentProjectPath
                        Dim lProjectName As String = lProject.CurrentProjectName

                        Application.Invoke(Sub()
                            UpdateStatusBar($"Building {lProjectName}...")
                            pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== Building {lProjectName} =========={Environment.NewLine}")
                        End Sub)

                        pSolutionBuildManager.ProjectPath = lProjectPath
                        pSolutionBuildManager.Configuration = pBuildConfiguration

                        Dim lResult As BuildResult = Await pSolutionBuildManager.BuildProjectAsync(pBuildConfiguration)
                        If lResult Is Nothing Then Continue for

                        ' Stamp the owning project onto each error/warning so the merged
                        ' result can attribute rows back to their project (BuildOutputPanel
                        ' folds this into the file-name cell text)
                        for each lError In lResult.Errors
                            lError.project = lProjectName
                            lMergedResult.Errors.Add(lError)
                        Next
                        for each lWarning In lResult.Warnings
                            lWarning.project = lProjectName
                            lMergedResult.Warnings.Add(lWarning)
                        Next

                        If Not lResult.Success Then
                            lMergedResult.Success = False
                            lFailedProjectName = lProjectName
                            Exit for
                        End If
                    Next

                Catch ex As Exception
                    Console.WriteLine($"BuildSolution build loop error: {ex.Message}")
                    lMergedResult.Success = False
                End Try

                lMergedResult.ErrorCount = lMergedResult.Errors.Count
                lMergedResult.WarningCount = lMergedResult.Warnings.Count

                Application.Invoke(Sub()
                    Try
                        pBuildOutputPanel?.ShowBuildResult(lMergedResult, pCurrentProject)

                        If lMergedResult.Success Then
                            UpdateStatusBar($"Solution build succeeded ({lProjects.Count} project(s))")
                            pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== Solution build succeeded =========={Environment.NewLine}")
                        Else
                            UpdateStatusBar($"Solution build failed ({lFailedProjectName}): {lMergedResult.Errors.Count} error(s)")
                            pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== Solution build failed ({lFailedProjectName}): {lMergedResult.Errors.Count} error(s), {lMergedResult.Warnings.Count} warning(s) =========={Environment.NewLine}")

                            If lMergedResult.Errors.Count > 0 Then
                                pBuildOutputPanel.Notebook.CurrentPage = 1
                            ElseIf lMergedResult.Warnings.Count > 0 Then
                                pBuildOutputPanel.Notebook.CurrentPage = 2
                            End If
                        End If

                        SetBuildButtonsEnabled(True)

                    Catch ex As Exception
                        Console.WriteLine($"BuildSolution completion error: {ex.Message}")
                    Finally
                        pIsBuildingNow = False
                    End Try
                End Sub)
            End Function)

        Catch ex As Exception
            Console.WriteLine($"BuildSolution error: {ex.Message}")
            ShowError("Build Solution Error", ex.Message)
            pIsBuildingNow = False
        End Try
    End Sub

    ''' <summary>
    ''' Handles the "Build Solution" menu command and Ctrl+Shift+B
    ''' </summary>
    Public Sub OnBuildSolution(vSender As Object, vArgs As EventArgs)
        Try
            #If DEBUG Then
            Console.WriteLine("OnBuildSolution called (Ctrl+Shift+B - Build Solution)")
            #End If
            BuildSolution()
        Catch ex As Exception
            Console.WriteLine($"OnBuildSolution error: {ex.Message}")
            ShowError("Build Error", ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Try to increment version before build if enabled
    ''' </summary>
    Private Sub TryIncrementVersionBeforeBuild()
        Try
            ' First, check if we should increment the IDE's own version
            TryIncrementVersion()
            
            ' Then increment the current project's version (existing functionality)
            If String.IsNullOrEmpty(pCurrentProject) Then Return
            
            ' Create version manager for current project
            Dim lVersionManager As New AssemblyVersionManager(pCurrentProject, pSettingsManager)

            ' Try to increment (will only do so if auto-increment is enabled)
            If lVersionManager.IncrementBuildNumberIfEnabled() Then
                #If DEBUG Then
                Console.WriteLine("Project version incremented before build")
                #End If
                
                ' Refresh any open AssemblyInfo editors
                RefreshAssemblyRelatedEditors()
                
                ' Update status
                UpdateStatusBar("Project version incremented - building...")
                
                ' Log the new version
                Dim lNewVersion As Version = lVersionManager.GetCurrentVersion()
                pBuildOutputPanel?.AppendOutput($"Project version incremented to: {lNewVersion}{Environment.NewLine}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"TryIncrementVersionBeforeBuild error: {ex.Message}")
            ' Don't fail the build if version increment fails
        End Try
    End Sub

    ''' <summary>
    ''' Try to increment the Project's version if auto-increment is enabled
    ''' </summary>
    Private Sub TryIncrementVersion()
        Try
            ' Check if auto-increment is enabled in settings
            If Not pSettingsManager.AutoIncrementVersion Then
                Return
            End If
            
            ' Find the *.vbproj file
            Dim lIdeProjectPath As String = FindProjectFile()
            If String.IsNullOrEmpty(lIdeProjectPath) Then
                #If DEBUG Then
                Console.WriteLine("Could not find the *.vbproj for version increment")
                #End If
                Return
            End If
            
            ' Create version manager for project
            Dim lVersionManager As New AssemblyVersionManager(lIdeProjectPath, pSettingsManager)

            ' Get current version
            Dim lCurrentVersion As Version = lVersionManager.GetCurrentVersion()
            
            ' Check if we should increment (e.g., only once per session or once per day)
            If ShouldIncrementVersion(lCurrentVersion) Then
                ' Increment the build number
                Dim lNewVersion As New Version(
                    lCurrentVersion.Major,
                    lCurrentVersion.Minor,
                    lCurrentVersion.Build + 1,
                    lCurrentVersion.Revision)
                
                ' Set the new version
                If lVersionManager.SetVersion(lNewVersion) Then
                    #If DEBUG Then
                    Console.WriteLine($"Project version incremented from {lCurrentVersion} to {lNewVersion}")
                    #End If
                    
                    
                    ' Clear cached version so UI updates
                    ApplicationVersion.ClearCache()
                    
                    ' Update window title to show new version
                    UpdateWindowTitle()
                    
                    ' Update status bar
                    UpdateStatusBar($"Project version incremented to {lNewVersion.Major}.{lNewVersion.Minor}.{lNewVersion.Build}")
                    
                    ' Log to build output
                    pBuildOutputPanel?.AppendOutput($"Project version incremented to: {lNewVersion}{Environment.NewLine}")
                    
                    ' Store that we've incremented this session
                    pVersionIncrementedThisSession = True
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"TryIncrementVersion error: {ex.Message}")
            ' Don't fail the build if project version increment fails
        End Try
    End Sub
    
    ''' <summary>
    ''' Determine if we should increment the IDE version
    ''' </summary>
    Private Function ShouldIncrementVersion(vCurrentVersion As Version) As Boolean
        Try
            ' Increment on every build (default)
            Return pSettingsManager.AutoIncrementVersion
            
        Catch ex As Exception
            Console.WriteLine($"ShouldIncrementVersion error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ''' <summary>
    ''' Find the .vbproj file
    ''' </summary>
    Private Function FindProjectFile() As String
        Try
            ' Start from the executable's directory
            Dim lExePath As String = Reflection.Assembly.GetExecutingAssembly().Location
            Dim lCurrentDir As New IO.DirectoryInfo(IO.Path.GetDirectoryName(lExePath))
            
            ' Search up the directory tree
            While lCurrentDir IsNot Nothing
                ' Check for SimpleIDE.vbproj
                Dim lProjectPath As String = IO.Path.Combine(lCurrentDir.FullName, "SimpleIDE.vbproj")
                If IO.File.Exists(lProjectPath) Then
                    Return lProjectPath
                End If
                
                ' Also check for VbIDE.vbproj (alternate name)
                lProjectPath = IO.Path.Combine(lCurrentDir.FullName, "VbIDE.vbproj")
                If IO.File.Exists(lProjectPath) Then
                    Return lProjectPath
                End If
                
                ' Check parent directory
                lCurrentDir = lCurrentDir.Parent
            End While
            
            Return ""
            
        Catch ex As Exception
            Console.WriteLine($"FindIdeProjectFile error: {ex.Message}")
            Return ""
        End Try
    End Function
    
    
    ' Rebuild the current project
    Public Sub RebuildProject()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before rebuilding.")
                Return
            End If
            
            If pBuildManager Is Nothing Then
                InitializeBuildSystem()
            End If

            ' See IsAnyBuildInProgress's remarks - this used to check only pBuildManager.
            ' IsBuilding, which stays False during a Build Solution (that uses a separate
            ' BuildManager instance), so a Rebuild could start concurrently with an
            ' already-running Build Solution
            If IsAnyBuildInProgress() Then
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If

            ' Auto-increment version for rebuild too
            TryIncrementVersionBeforeBuild()

            ' Start the rebuild
            pIsBuildingNow = True
            pCurrentBuildOperation = "Rebuild"
            SetBuildButtonsEnabled(False)
            UpdateStatusBar("Rebuilding project...")
            
            ' Save all open files before building
            SaveAllFiles()
            
            ' Start async rebuild
            pBuildManager.ProjectPath = pCurrentProject
            pBuildManager.Configuration = pBuildConfiguration
            Task.Run(Async Function() Await pBuildManager.BuildProjectAsync(pBuildConfiguration))
            
        Catch ex As Exception
            Console.WriteLine($"RebuildProject error: {ex.Message}")
            ShowError("Rebuild error", ex.Message)
            pIsBuildingNow = False
            SetBuildButtonsEnabled(True)
        End Try
    End Sub

    ' Clean the current project
    Public Sub CleanProject()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before cleaning.")
                Return
            End If

            If pBuildManager Is Nothing Then
                InitializeBuildSystem()
            End If

            ' See IsAnyBuildInProgress's remarks - this used to check only pBuildManager.
            ' IsBuilding, which stays False during a Build Solution (that uses a separate
            ' BuildManager instance), so a Clean could start concurrently with an
            ' already-running Build Solution
            If IsAnyBuildInProgress() Then
                ShowInfo("Build in Progress", "Please wait for the current build to finish.")
                Return
            End If

            ' Start the clean
            pIsBuildingNow = True
            pCurrentBuildOperation = "Clean"
            SetBuildButtonsEnabled(False)
            UpdateStatusBar("Cleaning project...")

            ' Start async clean
            pBuildManager.ProjectPath = pCurrentProject
            pBuildManager.Configuration = pBuildConfiguration
            Task.Run(Async Function() Await pBuildManager.CleanProjectAsync())

        Catch ex As Exception
            Console.WriteLine($"CleanProject error: {ex.Message}")
            ShowError("Clean error", ex.Message)
            pIsBuildingNow = False
            SetBuildButtonsEnabled(True)
        End Try
    End Sub


    Private Sub OnCleanProject(vSender As Object, vArgs As EventArgs)
        Try
            CleanProject()  ' Call the existing CleanProjectFixed method
        Catch ex As Exception
            Console.WriteLine($"OnCleanProject error: {ex.Message}")
        End Try
    End Sub 
    
    ' Configure build settings
    Public Sub ConfigureBuild()
        Try
            Using lDialog As New BuildConfigurationDialog(Me, pBuildConfiguration, pThemeManager)
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    ' Update configuration
                    pBuildConfiguration = lDialog.BuildConfiguration
                    SaveBuildConfiguration()
                    
                    ' Update build manager
                    If pBuildManager IsNot Nothing Then
                        pBuildManager.Configuration = pBuildConfiguration
                    End If

                    ' Keep the Build > Configuration Debug/Release radio buttons in sync -
                    ' they're a separate, narrower control (Debug/Release only, vs. this
                    ' dialog's full Debug/Release/Test) that would otherwise silently show
                    ' the pre-dialog selection after this changes it. Leave them alone (not
                    ' forced to Debug) if the dialog picked "Test" - neither radio represents
                    ' that configuration, and re-firing OnConfigurationChanged("Debug") here
                    ' would overwrite the Test selection this dialog just saved
                    If pReleaseConfigMenuItem IsNot Nothing AndAlso pDebugConfigMenuItem IsNot Nothing Then
                        If String.Equals(pBuildConfiguration.Configuration, "Release", StringComparison.OrdinalIgnoreCase) Then
                            pReleaseConfigMenuItem.Active = True
                        ElseIf String.Equals(pBuildConfiguration.Configuration, "Debug", StringComparison.OrdinalIgnoreCase) Then
                            pDebugConfigMenuItem.Active = True
                        End If
                    End If
                End If
                lDialog.Destroy()
            End Using
            
        Catch ex As Exception
            Console.WriteLine($"ConfigureBuild error: {ex.Message}")
            ShowError("Configuration error", ex.Message)
        End Try
    End Sub
    
    ' ===== Build Event Handlers =====
    
    ''' <summary>
    ''' Build event handler - build started
    ''' </summary>
    Private Sub OnBuildStarted(vSender As Object, vE As EventArgs)
        Try
            #If DEBUG Then
            Console.WriteLine("OnBuildStarted: Starting")
            #End If
            
            Application.Invoke(Sub()
                Try
                    Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
                    UpdateStatusBar($"Building {lProjectName}...")
                    
                    ' Clear build output with safe check
                    ' CHANGED: Use ClearOutputOnly to preserve error/warning counts
                    Try
                        If pSettingsManager IsNot Nothing AndAlso pSettingsManager.ClearOutputOnBuild Then
                            pBuildOutputPanel?.ClearOutputOnly()  ' Changed from ClearOutput
                        End If
                    Catch ex As Exception
                        Console.WriteLine($"Error checking ClearOutputOnBuild: {ex.Message}")
                        ' Continue anyway - don't fail the build for this
                    End Try
                    
                    ' Show build output panel
                    If pBottomPanelManager IsNot Nothing Then
                        pBottomPanelManager.Show()
                        pBottomPanelManager.ShowTab(0) ' Build output is tab 0
                    End If
                    
                    ' ADDED: Switch BuildOutputPanel's internal notebook to Output tab
                    If pBuildOutputPanel IsNot Nothing Then
                        pBuildOutputPanel.SwitchToOutputTab()
                    End If
                    
                    SetBuildButtonsEnabled(False)
                    
                Catch ex As Exception
                    Console.WriteLine($"OnBuildStarted invoke error: {ex.Message}")
                End Try
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"OnBuildStarted error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Build event handler - build completed with error/warning population (no double output)
    ''' </summary>
    Private Sub OnBuildCompleted(vSender As Object, vArgs As BuildEventArgs)
        Try
            #If DEBUG Then
            Console.WriteLine($"OnBuildCompleted called - Success = {vArgs?.Result?.Success}")
            #End If
            #If DEBUG Then
            Console.WriteLine($"OnBuildCompleted - Errors: {vArgs?.Result?.Errors?.Count}, Warnings: {vArgs?.Result?.Warnings?.Count}")
            #End If
            
            Application.Invoke(Sub()
                Try
                    If vArgs.Result IsNot Nothing Then
                        ' IMPORTANT: Don't append the raw output here - it's already been added via OnBuildOutput
                        ' Just update the error/warning lists from the parsed BuildResult
                        
                        ' Update the build output panel with the parsed errors and warnings
                        ' This will populate the error and warning tabs WITHOUT adding to the output tab
                        If pBuildOutputPanel IsNot Nothing Then
                            ' Pass the BuildResult to populate error/warning lists
                            ' This should NOT append any text to the output tab
                            pBuildOutputPanel.ShowBuildResult(vArgs.Result, pCurrentProject)
                        End If
                        
                        If vArgs.Result.Success Then
                            UpdateStatusBar($"{pCurrentBuildOperation} succeeded")
                            ' Only append the summary line, not the full output
                            pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== {pCurrentBuildOperation} succeeded =========={Environment.NewLine}")
                        Else
                            Dim lErrorText As String = If(vArgs.Result.Errors.Count = 1, "error", "errors")
                            Dim lWarningText As String = If(vArgs.Result.Warnings.Count = 1, "warning", "warnings")

                            UpdateStatusBar($"{pCurrentBuildOperation} failed with {vArgs.Result.Errors.Count} {lErrorText}, {vArgs.Result.Warnings.Count} {lWarningText}")
                            ' Only append the summary line, not the full output
                            pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== {pCurrentBuildOperation} failed: {vArgs.Result.Errors.Count} {lErrorText}, {vArgs.Result.Warnings.Count} {lWarningText} =========={Environment.NewLine}")
                            
                            ' Switch to errors tab if there are errors
                            If vArgs.Result.Errors.Count > 0 AndAlso pBuildOutputPanel IsNot Nothing Then
                                ' Switch to the Errors tab (index 1)
                                pBuildOutputPanel.Notebook.CurrentPage = 1
                            ElseIf vArgs.Result.Warnings.Count > 0 AndAlso pBuildOutputPanel IsNot Nothing Then
                                ' Switch to the Warnings tab (index 2) if only warnings
                                pBuildOutputPanel.Notebook.CurrentPage = 2
                            End If
                        End If
                    End If
                    
                    SetBuildButtonsEnabled(True)
                    
                    ' Check if we should run after build (for F5)
                    If pRunAfterBuild AndAlso vArgs.Result?.Success = True Then
                        pRunAfterBuild = False
                        Task.Run(Async Function() 
                            Await RunProject()
                            Return Nothing
                        End Function)
                    End If
                    
                    ' Raise our build completed event
                    RaiseEvent BuildCompleted(vArgs.Result?.Success)
                    
                Catch ex As Exception
                    Console.WriteLine($"OnBuildCompleted invoke error: {ex.Message}")
                End Try
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"OnBuildCompleted error: {ex.Message}")
        End Try
        pIsBuildingNow = False
    End Sub
    
    ''' <summary>
    ''' Build error handler
    ''' </summary>
    Private Sub OnBuildError(vSender As Object, vError As String)
        Try
            Application.Invoke(Sub()
                'pBuildOutputPanel?.AppendOutput($"ERROR: {vError}")
            End Sub)
        Catch ex As Exception
            Console.WriteLine($"OnBuildError error: {ex.Message}")
        End Try
    End Sub

    
    ' ===== Helper Methods =====
    
    Private Sub SetBuildButtonsEnabled(vEnabled As Boolean)
        Try
            ' This would enable/disable build-related toolbar buttons and menu items
            ' Implementation depends on how the UI is structured
            
        Catch ex As Exception
            Console.WriteLine($"SetBuildButtonsEnabled error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub StartProcess(vExecutable As String, vArguments As String)
        Try
            Dim lStartInfo As New ProcessStartInfo()
            lStartInfo.FileName = vExecutable
            lStartInfo.Arguments = vArguments
            lStartInfo.UseShellExecute = False
            lStartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(vExecutable)
            
            Dim lProcess As Process = Process.Start(lStartInfo)
            
            ' Store reference if we need to track it
            If lProcess IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine($"Started process: {vExecutable}")
                #End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"StartProcess error: {ex.Message}")
            ShowError("Process error", ex.Message)
        End Try
    End Sub
    
    Private Sub UpdateErrorList(vResult As BuildResult)
        Try
            ' This would update the error list panel with build results
            ' Implementation depends on error list widget structure
            
        Catch ex As Exception
            Console.WriteLine($"UpdateErrorList error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Loads the active build configuration from settings - Configuration/Platform/
    ''' BuildBeforeRun come from the toolbar/Configure Build dialog's own persisted state;
    ''' Verbosity/ParallelBuild/RestorePackages come from Preferences' Build tab, which
    ''' previously saved all three but nothing anywhere ever read them back, so they had zero
    ''' effect on an actual build regardless of what the user configured
    ''' </summary>
    Private Sub LoadBuildConfiguration()
        Try
            ' Load build configuration from settings
            If pBuildConfiguration IsNot Nothing Then
                pBuildConfiguration.Configuration = pSettingsManager.BuildConfiguration
                pBuildConfiguration.Platform = pSettingsManager.BuildPlatform
                pBuildConfiguration.BuildBeforeRun = pSettingsManager.BuildBeforeRun

                Dim lVerbosity As BuildVerbosity
                If [Enum].TryParse(Of BuildVerbosity)(pSettingsManager.GetString("Build.Verbosity", "Normal"), lVerbosity) Then
                    pBuildConfiguration.Verbosity = lVerbosity
                End If
                pBuildConfiguration.ParallelBuild = pSettingsManager.GetBoolean("Build.ParallelBuild", True)
                pBuildConfiguration.RestorePackages = pSettingsManager.GetBoolean("Build.RestorePackages", True)
            End If

        Catch ex As Exception
            Console.WriteLine($"LoadBuildConfiguration error: {ex.Message}")
        End Try
    End Sub

    Private Sub SaveBuildConfiguration()
        Try
            ' Save build configuration to settings
            If pBuildConfiguration IsNot Nothing Then
                pSettingsManager.BuildConfiguration = pBuildConfiguration.Configuration
                pSettingsManager.BuildPlatform = pBuildConfiguration.Platform
                pSettingsManager.BuildBeforeRun = pBuildConfiguration.BuildBeforeRun

                ' Also persist Verbosity/ParallelBuild/RestorePackages here, in addition to
                ' Preferences saving them - so a change made via the Configure Build dialog
                ' (which edits these same BuildConfiguration fields) survives a restart too,
                ' under the same keys Preferences reads/writes
                pSettingsManager.SetString("Build.Verbosity", pBuildConfiguration.Verbosity.ToString())
                pSettingsManager.SetBoolean("Build.ParallelBuild", pBuildConfiguration.ParallelBuild)
                pSettingsManager.SetBoolean("Build.RestorePackages", pBuildConfiguration.RestorePackages)
            End If

        Catch ex As Exception
            Console.WriteLine($"SaveBuildConfiguration error: {ex.Message}")
        End Try
    End Sub

    ' ===== Fixed Build Event Handlers =====
    
    ' Fixed: Change event handlers to match EventHandler(Of String) signature
    Private Sub OnBuildOutput(vSender As Object, vOutput As String)
        Try
            Application.Invoke(Sub()
                pBuildOutputPanel?.AppendOutput(vOutput)
            End Sub)
        Catch ex As Exception
            Console.WriteLine($"OnBuildOutput error: {ex.Message}")
        End Try
    End Sub

    ' ===== BuildOutputPanel's own Build/Run/Stop button row =====

    Private Sub OnBuildOutputPanelBuildRequested()
        Try
            OnBuildProject(Nothing, EventArgs.Empty)
        Catch ex As Exception
            Console.WriteLine($"OnBuildOutputPanelBuildRequested error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnBuildOutputPanelRunRequested()
        Try
            ' OnRunProject gates on the sender being a CustomDrawButton/MenuItem to
            ' distinguish a genuine button/menu click from other trigger paths (e.g. F5,
            ' which calls BuildAndRun directly instead). Passing the real toolbar Run
            ' button as a stand-in sender is honest here - this genuinely is an equivalent
            ' user-initiated run request, just from BuildOutputPanel's own copy of the button.
            OnRunProject(pRunButton, EventArgs.Empty)
        Catch ex As Exception
            Console.WriteLine($"OnBuildOutputPanelRunRequested error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnBuildOutputPanelStopRequested()
        Try
            OnStopDebugging(Nothing, EventArgs.Empty)
        Catch ex As Exception
            Console.WriteLine($"OnBuildOutputPanelStopRequested error: {ex.Message}")
        End Try
    End Sub



    ''' <summary>
    ''' Checks if any open files have been modified since last save
    ''' </summary>
    ''' <returns>True if any files are modified, False otherwise</returns>
    Private Function HasModifiedFiles() As Boolean
        Try
            ' Check all open editors for modifications
            If pNotebook IsNot Nothing Then
                for i As Integer = 0 To pNotebook.NPages - 1
                    Dim lPage As Widget = pNotebook.GetNthPage(i)
                    Dim lEditor As IEditor = TryCast(lPage, IEditor)
                    
                    If lEditor IsNot Nothing AndAlso lEditor.IsModified Then
                        Return True
                    End If
                Next
            End If
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"HasModifiedFiles error: {ex.Message}")
            ' If we can't determine, assume files are modified to be safe
            Return True
        End Try
    End Function
    
    ''' <summary>
    ''' Updates the project's dirty state based on all open files
    ''' </summary>
    ''' <remarks>
    ''' The project is dirty if ANY file has unsaved changes.
    ''' The project is clean only when ALL files are saved.
    ''' </remarks>
    Private Sub UpdateProjectDirtyState()
        Try
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                Return
            End If
            
            ' Check if any files are modified
            Dim lHasModifiedFiles As Boolean = HasModifiedFiles()
            
            ' Update the project manager's dirty state
            If lHasModifiedFiles Then
                ' Mark project as dirty if any file is modified
                If Not pProjectManager.IsDirty Then
                    pProjectManager.MarkDirty()
                    #If DEBUG Then
                    Console.WriteLine("UpdateProjectDirtyState: Project marked as dirty (files have unsaved changes)")
                    #End If
                End If
            Else
                ' Mark project as clean if all files are saved
                If pProjectManager.IsDirty Then
                    pProjectManager.MarkClean()
                    #If DEBUG Then
                    Console.WriteLine("UpdateProjectDirtyState: All files saved, project marked clean")
                    #End If
                End If
            End If
            
            ' Update window title to reflect the project state
            UpdateWindowTitle()
            
        Catch ex As Exception
            Console.WriteLine($"UpdateProjectDirtyState error: {ex.Message}")
        End Try
    End Sub
    
    ' Add: SimpleIDE.MainWindow.HasBuildOutput
    ' To: MainWindow.Build.vb
    
    ''' <summary>
    ''' Checks if build output exists for the current project
    ''' </summary>
    ''' <returns>True if build output exists, False otherwise</returns>
    Private Function HasBuildOutput() As Boolean
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                Return False
            End If
            
            ' Get the project directory
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            If String.IsNullOrEmpty(lProjectDir) Then
                Return False
            End If
            
            ' Check for build output based on current configuration
            Dim lConfiguration As String = "Debug"  ' Default
            If pBuildConfiguration IsNot Nothing Then
                lConfiguration = pBuildConfiguration.Configuration
            End If
            
            ' Check for typical .NET build output paths
            Dim lBuildPaths As String() = {
                System.IO.Path.Combine(lProjectDir, "bin", lConfiguration),
                System.IO.Path.Combine(lProjectDir, "bin", lConfiguration, "net8.0"),
                System.IO.Path.Combine(lProjectDir, "bin", lConfiguration, "net7.0"),
                System.IO.Path.Combine(lProjectDir, "bin", lConfiguration, "net6.0")
            }
            
            ' Check if any build path exists and contains assemblies
            for each lPath As String in lBuildPaths
                If Directory.Exists(lPath) Then
                    ' Look for .dll or .exe files
                    Dim lDllFiles As String() = Directory.GetFiles(lPath, "*.dll")
                    Dim lExeFiles As String() = Directory.GetFiles(lPath, "*.exe")
                    
                    If lDllFiles.Length > 0 OrElse lExeFiles.Length > 0 Then
                        ' Check if the main project output exists
                        Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
                        Dim lMainDll As String = System.IO.Path.Combine(lPath, $"{lProjectName}.dll")
                        Dim lMainExe As String = System.IO.Path.Combine(lPath, $"{lProjectName}.exe")
                        
                        If File.Exists(lMainDll) OrElse File.Exists(lMainExe) Then
                            ' Check if it's newer than the project file
                            Dim lProjectTime As DateTime = File.GetLastWriteTime(pCurrentProject)
                            Dim lOutputTime As DateTime = DateTime.MinValue
                            
                            If File.Exists(lMainDll) Then
                                lOutputTime = File.GetLastWriteTime(lMainDll)
                            ElseIf File.Exists(lMainExe) Then
                                lOutputTime = File.GetLastWriteTime(lMainExe)
                            End If
                            
                            ' If output is newer than project file, we have a build
                            Return lOutputTime > lProjectTime
                        End If
                    End If
                End If
            Next
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"HasBuildOutput error: {ex.Message}")
            ' If we can't determine, assume no build output
            Return False
        End Try
    End Function
    
End Class
