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
    
    ' ===== Build System Integration =====
    
    ''' <summary>
    ''' Event raised when build completes
    ''' </summary>
    Public Event BuildCompleted(vSuccess As Boolean)
    
    Private pBuildConfiguration As BuildConfiguration = Nothing
    Private pIsDebugging As Boolean = False
    Private pDebugProcess As Process = Nothing
    Private pBuildManager As BuildManager = Nothing

    ' ===== Auto-Hide Timer Fields =====
    Private pAutoHideTimerId As UInteger = 0

    Private pVersionIncrementedThisSession As Boolean = False

    
    ''' <summary>
    ''' Initialize build system components
    ''' </summary>
    Private Sub InitializeBuildSystem()
        Try
            Console.WriteLine("InitializeBuildSystem: Starting...")
            
            ' Create build manager if needed
            If pBuildManager Is Nothing Then
                Console.WriteLine("InitializeBuildSystem: Creating BuildManager")
                pBuildManager = New BuildManager()
                
                ' Add event handlers for build manager
                AddHandler pBuildManager.BuildStarted, AddressOf OnBuildStarted
                AddHandler pBuildManager.BuildCompleted, AddressOf OnBuildCompleted
                AddHandler pBuildManager.OutputReceived, AddressOf OnBuildOutput
                AddHandler pBuildManager.ErrorReceived, AddressOf OnBuildError
            End If
            
            ' Create build configuration if needed
            If pBuildConfiguration Is Nothing Then
                Console.WriteLine("InitializeBuildSystem: Creating BuildConfiguration")
                pBuildConfiguration = New BuildConfiguration()
                LoadBuildConfiguration()
            End If
            
            ' CRITICAL: Set the configuration on the build manager
            If pBuildManager IsNot Nothing Then
                Console.WriteLine($"InitializeBuildSystem: Setting BuildManager.Configuration")
                pBuildManager.Configuration = pBuildConfiguration
            End If
            
            Console.WriteLine("InitializeBuildSystem: Complete")
        Catch ex As Exception
            Console.WriteLine($"InitializeBuildSystem error: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
        End Try
    End Sub

    Private pIsBuildingNow As Boolean

    ''' <summary>
    ''' Build the current project - Main entry point for F6 and build operations
    ''' </summary>
    ''' <remarks>
    ''' Ensures pIsBuildingNow flag is properly managed on all exit paths
    ''' </remarks>
    Public Sub BuildProject()
        Try
            ' Check if already building
            If pIsBuildingNow = True Then
                Console.WriteLine("BuildProject: Already building (early exit)")
                Return
            End If
            
            ' Also check BuildManager's state
            If pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding Then
                Console.WriteLine("BuildProject: BuildManager reports build in progress")
                Return
            End If
            
            ' Set flag immediately
            pIsBuildingNow = True
            
            ' DEBUG: Simple console output to verify method is called
            Console.WriteLine("===============================================")
            Console.WriteLine("BUILD PROJECT CALLED!")
            Console.WriteLine($"Time: {DateTime.Now:HH:mm:ss.fff}")
            Console.WriteLine($"Project Path: {pCurrentProject}")
            Console.WriteLine($"BuildManager Is Nothing: {pBuildManager Is Nothing}")
            Console.WriteLine($"BuildConfiguration Is Nothing: {pBuildConfiguration Is Nothing}")
            Console.WriteLine("===============================================")
             
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before building.")
                pIsBuildingNow = False ' Reset flag on early exit
                Return
            End If
    
            ' Initialize build system if needed
            If pBuildManager Is Nothing OrElse pBuildConfiguration Is Nothing Then
                Console.WriteLine("BuildProject: Initializing build system")
                InitializeBuildSystem()
            End If
            
            ' Verify initialization succeeded
            If pBuildManager Is Nothing Then
                Console.WriteLine("BuildProject: ERROR - BuildManager is Nothing after initialization")
                ShowError("Build Error", "Failed to initialize build system")
                pIsBuildingNow = False ' Reset flag on error
                Return
            End If
            
            If pBuildConfiguration Is Nothing Then
                Console.WriteLine("BuildProject: ERROR - BuildConfiguration is Nothing after initialization")
                ShowError("Build Error", "Failed to initialize build configuration")
                pIsBuildingNow = False ' Reset flag on error
                Return
            End If
    
            ' Check if already building - use the BuildManager's IsBuilding property
            If pBuildManager.IsBuilding Then
                Console.WriteLine("BuildProject: Build already in progress (BuildManager check)")
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
            Console.WriteLine($"BuildProject: Setting project path = {pCurrentProject}")
            pBuildManager.ProjectPath = pCurrentProject
            
            ' Ensure configuration is set
            Console.WriteLine($"BuildProject: Setting configuration = {pBuildConfiguration.Configuration}")
            pBuildManager.Configuration = pBuildConfiguration
    
            ' Start async build - Pass the configuration explicitly
            Console.WriteLine("BuildProject: Starting async build")
            Task.Run(Async Function() 
                Try
                    Console.WriteLine("BuildProject: Async task started")
                    Dim lResult = Await pBuildManager.BuildProjectAsync(pBuildConfiguration)
                    Console.WriteLine($"BuildProject: Async task completed, Success = {lResult?.Success}")
                    
                    ' Reset building flag when complete (on UI thread for safety)
                    Application.Invoke(Sub()
                        pIsBuildingNow = False
                        Console.WriteLine("BuildProject: pIsBuildingNow flag reset to False")
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
            Dim lVersionManager As New AssemblyVersionManager(pCurrentProject)
            
            ' Try to increment (will only do so if auto-increment is enabled)
            If lVersionManager.IncrementBuildNumberIfEnabled() Then
                Console.WriteLine("Project version incremented before build")
                
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
                Console.WriteLine("Could not find the *.vbproj for version increment")
                Return
            End If
            
            ' Create version manager for project
            Dim lVersionManager As New AssemblyVersionManager(lIdeProjectPath)
            
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
                    Console.WriteLine($"Project version incremented from {lCurrentVersion} to {lNewVersion}")
                    
                    ' Record the increment time
                    pSettingsManager.LastVersionIncrement = DateTime.Now
                    
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
            ' Option 1: Only increment once per session
            If pSettingsManager.IncrementOncePerSession Then
                Return Not pVersionIncrementedThisSession
            End If
            
            ' Option 2: Only increment once per day
            If pSettingsManager.IncrementOncePerDay Then
                Dim lLastIncrement As DateTime = pSettingsManager.LastVersionIncrement
                Return lLastIncrement.Date < DateTime.Today
            End If
            
            ' Option 3: Increment on every build (default)
            Return True
            
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
            
            If pBuildManager.IsBuilding Then
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If
            
            ' Auto-increment version for rebuild too
            TryIncrementVersionBeforeBuild()
            
            ' Start the rebuild
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
            
            If pBuildManager.IsBuilding Then
                ShowInfo("Build in Progress", "Please wait for the current build to finish.")
                Return
            End If
            
            ' Start the clean
            SetBuildButtonsEnabled(False)
            UpdateStatusBar("Cleaning project...")
            
            ' Start async clean
            pBuildManager.ProjectPath = pCurrentProject
            pBuildManager.Configuration = pBuildConfiguration
            Task.Run(Async Function() Await pBuildManager.CleanProjectAsync())
            
        Catch ex As Exception
            Console.WriteLine($"CleanProject error: {ex.Message}")
            ShowError("Clean error", ex.Message)
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
    
    ' Handle build completion when building for run
    Private Sub OnBuildCompletedForRun(vSuccess As Boolean)
        Try
            ' Check if we should run after build
            If pRunAfterBuild Then
                pRunAfterBuild = False
                
                ' Check if build was successful
                If vSuccess Then
                    ' Run the project after successful build
                    Task.Run(Async Function() 
                        Await RunProject()
                        Return Nothing  ' Add this return statement
                    End Function)
                Else
                    UpdateStatusBar("Build failed - run cancelled")
                End If
            End If
            pIsBuildingNow = False
        Catch ex As Exception
            Console.WriteLine($"OnBuildCompletedForRun error: {ex.Message}")
        End Try
    End Sub
    
    ' Configure build settings
    Public Sub ConfigureBuild()
        Try
            Using lDialog As New BuildConfigurationDialog(Me, pBuildConfiguration)
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    ' Update configuration
                    pBuildConfiguration = lDialog.BuildConfiguration
                    SaveBuildConfiguration()
                    
                    ' Update build manager
                    If pBuildManager IsNot Nothing Then
                        pBuildManager.Configuration = pBuildConfiguration
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
            Console.WriteLine("OnBuildStarted: Starting")
            
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
            Console.WriteLine($"OnBuildCompleted called - Success = {vArgs?.Result?.Success}")
            Console.WriteLine($"OnBuildCompleted - Errors: {vArgs?.Result?.Errors?.Count}, Warnings: {vArgs?.Result?.Warnings?.Count}")
            
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
                            UpdateStatusBar("Build succeeded")
                            ' Only append the summary line, not the full output
                            pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== Build succeeded =========={Environment.NewLine}")
                            
                            ' Start timer to auto-hide bottom panel after 5 seconds
                            StartAutoHideBottomPanelTimer()
                        Else
                            Dim lErrorText As String = If(vArgs.Result.Errors.Count = 1, "error", "errors")
                            Dim lWarningText As String = If(vArgs.Result.Warnings.Count = 1, "warning", "warnings")
                            
                            UpdateStatusBar($"Build failed with {vArgs.Result.Errors.Count} {lErrorText}, {vArgs.Result.Warnings.Count} {lWarningText}")
                            ' Only append the summary line, not the full output
                            pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== Build failed: {vArgs.Result.Errors.Count} {lErrorText}, {vArgs.Result.Warnings.Count} {lWarningText} =========={Environment.NewLine}")
                            
                            ' Switch to errors tab if there are errors
                            If vArgs.Result.Errors.Count > 0 AndAlso pBuildOutputPanel IsNot Nothing Then
                                ' Switch to the Errors tab (index 1)
                                pBuildOutputPanel.Notebook.CurrentPage = 1
                            ElseIf vArgs.Result.Warnings.Count > 0 AndAlso pBuildOutputPanel IsNot Nothing Then
                                ' Switch to the Warnings tab (index 2) if only warnings
                                pBuildOutputPanel.Notebook.CurrentPage = 2
                            End If
                            
                            ' Cancel any pending auto-hide timer since build failed
                            CancelAutoHideBottomPanelTimer()
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
                Console.WriteLine($"Started process: {vExecutable}")
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
    
    Private Sub LoadBuildConfiguration()
        Try
            ' Load build configuration from settings
            If pBuildConfiguration IsNot Nothing Then
                pBuildConfiguration.Configuration = pSettingsManager.BuildConfiguration
                pBuildConfiguration.Platform = pSettingsManager.BuildPlatform
                pBuildConfiguration.BuildBeforeRun = pSettingsManager.BuildBeforeRun
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
    
    
    ''' <summary>
    ''' Starts a timer to auto-hide the bottom panel after 5 seconds
    ''' </summary>
    Private Sub StartAutoHideBottomPanelTimer()
        Try
            ' Cancel any existing timer
            CancelAutoHideBottomPanelTimer()
            
            ' Start new timer for 5 seconds (5000 milliseconds)
            pAutoHideTimerId = GLib.Timeout.Add(5000, AddressOf OnAutoHideBottomPanelTimeout)
            Console.WriteLine("Started auto-hide timer for bottom panel (5 seconds)")
            
        Catch ex As Exception
            Console.WriteLine($"StartAutoHideBottomPanelTimer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Cancels the auto-hide timer if it's running
    ''' </summary>
    Private Sub CancelAutoHideBottomPanelTimer()
        Try
            If pAutoHideTimerId > 0 Then
                GLib.Source.Remove(pAutoHideTimerId)
                pAutoHideTimerId = 0
                Console.WriteLine("Cancelled auto-hide timer for bottom panel")
            End If
        Catch ex As Exception
            Console.WriteLine($"CancelAutoHideBottomPanelTimer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Timer callback to hide the bottom panel and return focus to editor
    ''' </summary>
    ''' <returns>False to stop the timer</returns>
    ''' <remarks>
    ''' Hides the bottom panel after timeout and returns focus to the current editor
    ''' </remarks>
    Private Function OnAutoHideBottomPanelTimeout() As Boolean
        Try
            ' Hide the bottom panel (this will also return focus to editor)
            HideBottomPanel()
            UpdateStatusBar("Build output hidden (build succeeded)")
            
            ' Clear the timer ID
            pAutoHideTimerId = 0
            
            ' Return False to stop the timer (one-shot timer)
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"OnAutoHideBottomPanelTimeout error: {ex.Message}")
            pAutoHideTimerId = 0
            Return False
        End Try
    End Function

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
