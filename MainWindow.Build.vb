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
    
    ''' <summary>
    ''' Build the current project - Main entry point for F5
    ''' </summary>
    Public Sub BuildProject()
        Try
            ' DEBUG: Simple console output to verify method is called
            Console.WriteLine("===============================================")
            Console.WriteLine("BUILD PROJECT CALLED!")
            Console.WriteLine($"Project Path: {pCurrentProject}")
            Console.WriteLine($"BuildManager Is Nothing: {pBuildManager Is Nothing}")
            Console.WriteLine($"BuildConfiguration Is Nothing: {pBuildConfiguration Is Nothing}")
            Console.WriteLine("===============================================")
             
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before building.")
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
                Return
            End If
            
            If pBuildConfiguration Is Nothing Then
                Console.WriteLine("BuildProject: ERROR - BuildConfiguration is Nothing after initialization")
                ShowError("Build Error", "Failed to initialize build configuration")
                Return
            End If

            If pBuildManager.IsBuilding Then
                ShowInfo("Build in Progress", "A build is already in progress.")
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
                             Console.WriteLine($"BuildProject: Async task completed - Success = {lResult.Success}")
                             Return lResult
                         Catch ex As Exception
                             Console.WriteLine($"BuildProject: Async task error - {ex.Message}")
                             Console.WriteLine($"Stack trace: {ex.StackTrace}")
                             Throw
                         End Try
                     End Function)

        Catch ex As Exception
            Console.WriteLine($"BuildProject error: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
            ShowError("Build error", ex.Message)
            SetBuildButtonsEnabled(True)
        End Try
    End Sub

    ''' <summary>
    ''' Try to increment version before build if enabled
    ''' </summary>
    Private Sub TryIncrementVersionBeforeBuild()
        Try
            ' Only increment if we have a current project
            If String.IsNullOrEmpty(pCurrentProject) Then Return
            
            ' Create version manager for current project
            Dim lVersionManager As New AssemblyVersionManager(pCurrentProject)
            
            ' Try to increment (will only do so if auto-increment is enabled)
            If lVersionManager.IncrementBuildNumberIfEnabled() Then
                Console.WriteLine("Assembly version incremented before build")
                
                ' Refresh any open AssemblyInfo editors
                RefreshAssemblyRelatedEditors()
                
                ' Update status
                UpdateStatusBar("Version incremented - building project...")
                
                ' Log the new version
                Dim lNewVersion As Version = lVersionManager.GetCurrentVersion()
                pBuildOutputPanel?.AppendOutput($"Version incremented to: {lNewVersion}{Environment.NewLine}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"TryIncrementVersionBeforeBuild error: {ex.Message}")
            ' Don't fail the build if version increment fails
        End Try
    End Sub
    
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
    
    ' Run the current project
    Public Sub RunProject()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before running.")
                Return
            End If
            
            ' Check if executable exists
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lBinDir As String = System.IO.Path.Combine(lProjectDir, "bin", "Debug", "net8.0")
            Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pCurrentProject)
            Dim lExecutable As String = System.IO.Path.Combine(lBinDir, lProjectName)
            
            If Not File.Exists(lExecutable) Then
                ShowError("Executable Not Found", "Please build the project first.")
                Return
            End If
            
            ' Run the executable
            StartProcess(lExecutable, "")
            
        Catch ex As Exception
            Console.WriteLine($"RunProject error: {ex.Message}")
            ShowError("Run error", ex.Message)
        End Try
    End Sub
    
    ' Build and run the current project
    Public Sub BuildAndRun()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before building and running.")
                Return
            End If
            
            ' Wire up build completed event to run after build
            AddHandler BuildCompleted, AddressOf OnBuildCompletedForRun
            
            ' Start the build
            BuildProject()
            
        Catch ex As Exception
            Console.WriteLine($"BuildAndRun error: {ex.Message}")
            ShowError("Build and run error", ex.Message)
        End Try
    End Sub
    
    ' Handle build completion when building for run
    Private Sub OnBuildCompletedForRun(vSuccess As Boolean)
        Try
            ' Remove the handler
            RemoveHandler BuildCompleted, AddressOf OnBuildCompletedForRun
            
            If vSuccess Then
                ' Run the project
                Application.Invoke(Sub() RunProject())
            End If
            
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
                    Try
                        If pSettingsManager IsNot Nothing AndAlso pSettingsManager.ClearOutputOnBuild Then
                            pBuildOutputPanel?.ClearOutput()
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
    ''' Build event handler - build completed
    ''' </summary>
    Private Sub OnBuildCompleted(vSender As Object, vArgs As BuildEventArgs)
        Try
            Console.WriteLine($"OnBuildCompleted called - Success = {vArgs?.Result?.Success}")
            Application.Invoke(Sub()
                If vArgs.Result.Success Then
                    UpdateStatusBar("Build succeeded")
                    pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== Build succeeded =========={Environment.NewLine}")
                Else
                    UpdateStatusBar($"Build failed with {vArgs.Result.Errors.Count} error(s)")
                    pBuildOutputPanel?.AppendOutput($"{Environment.NewLine}========== Build failed =========={Environment.NewLine}")
                    
                    ' Update error list
                    UpdateErrorList(vArgs.Result)
                End If
                
                SetBuildButtonsEnabled(True)
                
                ' Raise our build completed event
                RaiseEvent BuildCompleted(vArgs.Result.Success)
            End Sub)
        Catch ex As Exception
            Console.WriteLine($"OnBuildCompleted error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Build error handler
    ''' </summary>
    Private Sub OnBuildError(vSender As Object, vError As String)
        Try
            Application.Invoke(Sub()
                pBuildOutputPanel?.AppendOutput($"ERROR: {vError}")
            End Sub)
        Catch ex As Exception
            Console.WriteLine($"OnBuildError error: {ex.Message}")
        End Try
    End Sub
'    
'    Private Sub OnBuildError(vSender As Object, vE As BuildOutputEventArgs)
'        Try
'            Application.Invoke(Sub()
'                pBuildOutputPanel?.AppendOutput($"ERROR: {vE.Text}")
'            End Sub)
'        Catch ex As Exception
'            Console.WriteLine($"OnBuildError error: {ex.Message}")
'        End Try
'    End Sub
    
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
    
'    Private Sub OnBuildError(vSender As Object, vError As String)
'        Try
'            Application.Invoke(Sub()
'                pBuildOutputPanel?.AppendOutput($"ERROR: {vError}")
'            End Sub)
'        Catch ex As Exception
'            Console.WriteLine($"OnBuildError error: {ex.Message}")
'        End Try
'    End Sub
    
    ' Re-initialize build system with corrected event handlers
    Private Sub InitializeBuildSystemFixed()
        Try
            ' Create build manager if needed
            If pBuildManager Is Nothing Then
                pBuildManager = New BuildManager()
                
                ' Add event handlers with correct signatures
                AddHandler pBuildManager.BuildStarted, AddressOf OnBuildStarted
                AddHandler pBuildManager.BuildCompleted, AddressOf OnBuildCompleted
                AddHandler pBuildManager.OutputReceived, AddressOf OnBuildOutput
                AddHandler pBuildManager.ErrorReceived, AddressOf OnBuildError
            End If
            
            ' Create build configuration
            If pBuildConfiguration Is Nothing Then
                pBuildConfiguration = New BuildConfiguration()
                LoadBuildConfiguration()
            End If
        Catch ex As Exception
            Console.WriteLine($"InitializeBuildSystemFixed error: {ex.Message}")
        End Try
    End Sub
    
    ' Fixed BuildProject method
    Public Sub BuildProjectFixed()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before building.")
                Return
            End If

            ' Initialize build system if needed
            If pBuildManager Is Nothing Then
                InitializeBuildSystemFixed()
            End If

            If pBuildManager.IsBuilding Then
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If
            
            ' Save all modified files
            SaveAllFiles()
            
            ' Set project path for build manager
            pBuildManager.ProjectPath = pCurrentProject
            
            ' Start build async
            Task.Run(Async Function() Await pBuildManager.BuildProjectAsync(pBuildManager.Configuration))
            
        Catch ex As Exception
            Console.WriteLine($"BuildProjectFixed error: {ex.Message}")
            ShowError("Build error", ex.Message)
        End Try
    End Sub
    
    ' Fixed RebuildProject method
    Public Sub RebuildProjectFixed()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before rebuilding.")
                Return
            End If
            
            ' Initialize build system if needed
            If pBuildManager Is Nothing Then
                InitializeBuildSystemFixed()
            End If
            
            If pBuildManager.IsBuilding Then
                ShowInfo("Build in progress", "A build is already in progress.")
                Return
            End If
            
            ' Save all modified files
            SaveAllFiles()
            
            ' Set project path for build manager
            pBuildManager.ProjectPath = pCurrentProject
            
            ' Start rebuild async
            Task.Run(Async Function() Await pBuildManager.BuildProjectAsync(pBuildManager.Configuration))
            
        Catch ex As Exception
            Console.WriteLine($"RebuildProjectFixed error: {ex.Message}")
            ShowError("Rebuild error", ex.Message)
        End Try
    End Sub
    
    ' Fixed CleanProject method
    Public Sub CleanProjectFixed()
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before cleaning.")
                Return
            End If
            
            ' Initialize build system if needed
            If pBuildManager Is Nothing Then
                InitializeBuildSystemFixed()
            End If
            
            If pBuildManager.IsBuilding Then
                ShowInfo("Build in progress", "A build is already in progress.")
                Return
            End If
            
            ' Set project path for build manager
            pBuildManager.ProjectPath = pCurrentProject
            
            ' Start clean async
            Task.Run(Async Function() Await pBuildManager.CleanProjectAsync())
            
        Catch ex As Exception
            Console.WriteLine($"CleanProjectFixed error: {ex.Message}")
            ShowError("Clean error", ex.Message)
        End Try
    End Sub
    
End Class
