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

    ' ===== Auto-Hide Timer Fields =====
    Private pAutoHideTimerId As UInteger = 0
    
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

    ' Replace: SimpleIDE.MainWindow.BuildProject
    ''' <summary>
    ''' Build the current project - Main entry point for F6
    ''' </summary>
    ''' <remarks>
    ''' Simplified to use only instance-level flag without static variable
    ''' </remarks>
    Public Sub BuildProject()
        Try
            ' Check if already building
            If pIsBuildingNow = True Then
                Console.WriteLine("BuildProject: Already building (early exit)")
                Return
            End If
            
            ' Set flag immediately
            pIsBuildingNow = True
            
            ' DEBUG: Simple console output to verify method is called
            Console.WriteLine("===============================================")
            Console.WriteLine("BUILD PROJECT CALLED!")
            Console.WriteLine($"Project Path: {pCurrentProject}")
            Console.WriteLine($"BuildManager Is Nothing: {pBuildManager Is Nothing}")
            Console.WriteLine($"BuildConfiguration Is Nothing: {pBuildConfiguration Is Nothing}")
            Console.WriteLine("===============================================")
             
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before building.")
                pIsBuildingNow = False
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
                pIsBuildingNow = False
                Return
            End If
            
            If pBuildConfiguration Is Nothing Then
                Console.WriteLine("BuildProject: ERROR - BuildConfiguration is Nothing after initialization")
                ShowError("Build Error", "Failed to initialize build configuration")
                pIsBuildingNow = False
                Return
            End If
    
            ' Check if already building - use the BuildManager's IsBuilding property
            If pBuildManager.IsBuilding Then
                Console.WriteLine("BuildProject: Build already in progress (BuildManager check)")
                ShowInfo("Build in Progress", "A build is already in progress.")
                pIsBuildingNow = False
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
                    
                    ' Reset building flag when complete
                    pIsBuildingNow = False
                    
                    Return lResult
                Catch ex As Exception
                    Console.WriteLine($"BuildProject: Async task error: {ex.Message}")
                    pIsBuildingNow = False
                    Return Nothing
                End Try
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"BuildProject error: {ex.Message}")
            ShowError("Build Error", ex.Message)
            SetBuildButtonsEnabled(True)
            pIsBuildingNow = False
        End Try
    End Sub

'     ''' <summary>
'     ''' Build the current project - Main entry point for F6
'     ''' </summary>
'     Public Sub BuildProject()
'         Static bolAlreadyRunning As Boolean
'         If Not bolAlreadyRunning Then
'             bolAlreadyRunning = True
'         Else
'             Exit Sub
'         End If 
'         Try
'             ' Use instance flag with immediate set to prevent race conditions
'             If pIsBuildingNow = True Then 
'                 Console.WriteLine("BuildProject: Already building (prevented duplicate)")
'                 Exit Sub
'             End If
'             pIsBuildingNow = True
' 
'             If pIsBuildingNow = True Then Exit Sub
'             pIsBuildingNow = True
'             ' DEBUG: Simple console output to verify method is called
'             Console.WriteLine("===============================================")
'             Console.WriteLine("BUILD PROJECT CALLED!")
'             Console.WriteLine($"Project Path: {pCurrentProject}")
'             Console.WriteLine($"BuildManager Is Nothing: {pBuildManager Is Nothing}")
'             Console.WriteLine($"BuildConfiguration Is Nothing: {pBuildConfiguration Is Nothing}")
'             Console.WriteLine("===============================================")
'                  
'             If String.IsNullOrEmpty(pCurrentProject) Then
'                 ShowError("No project", "Please open a project before building.")
'                 Return
'             End If
'     
'             ' Initialize build system if needed
'             If pBuildManager Is Nothing OrElse pBuildConfiguration Is Nothing Then
'                 Console.WriteLine("BuildProject: Initializing build system")
'                 InitializeBuildSystem()
'             End If
'             
'             ' Verify initialization succeeded
'             If pBuildManager Is Nothing Then
'                 Console.WriteLine("BuildProject: ERROR - BuildManager is Nothing after initialization")
'                 ShowError("Build Error", "Failed to initialize build system")
'                 Return
'             End If
'             
'             If pBuildConfiguration Is Nothing Then
'                 Console.WriteLine("BuildProject: ERROR - BuildConfiguration is Nothing after initialization")
'                 ShowError("Build Error", "Failed to initialize build configuration")
'                 Return
'             End If
'     
'             ' Check if already building - use the BuildManager's IsBuilding property
'             If pBuildManager.IsBuilding Then
'                 Console.WriteLine("BuildProject: Build already in progress, exiting")
'                 ShowInfo("Build in Progress", "A build is already in progress.")
'                 Return
'             End If
'     
'             ' Auto-increment version if enabled
'             TryIncrementVersionBeforeBuild()
'     
'             ' Start the build
'             SetBuildButtonsEnabled(False)
'             UpdateStatusBar("Building project...")
'     
'             ' Save all open files before building
'             SaveAllFiles()
'     
'             ' Set project path and configuration for build manager
'             Console.WriteLine($"BuildProject: Setting project path = {pCurrentProject}")
'             pBuildManager.ProjectPath = pCurrentProject
'             
'             ' Ensure configuration is set
'             Console.WriteLine($"BuildProject: Setting configuration = {pBuildConfiguration.Configuration}")
'             pBuildManager.Configuration = pBuildConfiguration
'             
' 
' 
'             ' Start async build - Pass the configuration explicitly
'             Console.WriteLine("BuildProject: Starting async build")
'             Task.Run(Async Function() 
'                 Try
'                     Console.WriteLine("BuildProject: Async task started")
'                     Dim lResult = Await pBuildManager.BuildProjectAsync(pBuildConfiguration)
'                     Console.WriteLine($"BuildProject: Async task completed, Success = {lResult?.Success}")
'                     
'                     ' Reset the building flag after completion
'                     pIsBuildingNow = False
'                     
'                     Return lResult
'                 Catch ex As Exception
'                     Console.WriteLine($"BuildProject: Async task error: {ex.Message}")
'                     pIsBuildingNow = False
'                     Return Nothing
'                 End Try
'             End Function)            
' 
'         Catch ex As Exception
'             Console.WriteLine($"BuildProject error: {ex.Message}")
'             ShowError("Build Error", ex.Message)
'             SetBuildButtonsEnabled(True)
'         Finally
'             bolAlreadyRunning = False
'             pIsBuildingNow = False
'         End Try
'     End Sub

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
    
End Class
