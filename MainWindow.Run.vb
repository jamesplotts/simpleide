' MainWindow.Run.vb
' Created: 2025-08-20 14:29:54
' MainWindow.Run.vb - Project running functionality
Imports Gtk
Imports System
Imports System.IO
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Managers

Partial Public Class MainWindow
    
    ' ===== Private Fields =====
    Private pRunProcess As Process = Nothing
    Private pIsRunning As Boolean = False
    Private pRunCancellationTokenSource As CancellationTokenSource = Nothing
    
    ' ===== Events =====
    
    ''' <summary>
    ''' Event raised when the project starts running
    ''' </summary>
    Public Event RunStarted As EventHandler
    
    ''' <summary>
    ''' Event raised when the project stops running
    ''' </summary>
    Public Event RunCompleted As EventHandler(Of RunEventArgs)
    
    ' ===== Event Args =====
    
    ''' <summary>
    ''' Event arguments for run completed event
    ''' </summary>
    Public Class RunEventArgs
        Inherits EventArgs
        
        Public Property Success As Boolean
        Public Property ExitCode As Integer
        Public Property Message As String
        Public Property Output As String
        Public Property ErrorOutput As String
        Public Property RunTime As TimeSpan
    End Class
    
    ' ===== Public Methods =====
    
    ''' <summary>
    ''' Run the current project using dotnet run
    ''' </summary>
    Public Async Function RunProject() As Task
        Try
            Console.WriteLine("RunProject: Starting")
            
            ' Check if project is open
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No Project", "Please open a project before running.")
                Return
            End If
            
            ' Check if already running
            If pIsRunning Then
                ShowInfo("Already Running", "The project is already running. Stop it first to run again.")
                Return
            End If
            
            ' Save all files before running
            SaveAllFiles()
            
            ' Get project directory
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            If String.IsNullOrEmpty(lProjectDir) OrElse Not Directory.Exists(lProjectDir) Then
                ShowError("Invalid Project", "Cannot determine project directory.")
                Return
            End If
            
            ' Show output panel if configured
            If pSettingsManager IsNot Nothing AndAlso pSettingsManager.ShowBuildOutput Then
                ShowBottomPanel()
                If pBottomPanelManager IsNot Nothing Then
                    ' Switch to build output tab using the enum
                    pBottomPanelManager.ShowTabByType(BottomPanelManager.BottomPanelTab.eBuildOutput)
                End If
            End If
            
            ' Clear output if configured
            If pSettingsManager IsNot Nothing AndAlso pSettingsManager.ClearOutputOnBuild Then
                If pBuildOutputPanel IsNot Nothing Then
                    pBuildOutputPanel.ClearOutput()
                End If
            End If
            
            ' Start the run
            Await RunProjectAsync(lProjectDir)
            
        Catch ex As Exception
            Console.WriteLine($"RunProject error: {ex.Message}")
            ShowError("Run Error", ex.Message)
        End Try
    End Function
    
    ''' <summary>
    ''' Run the project asynchronously
    ''' </summary>
    Private Async Function RunProjectAsync(vProjectDirectory As String) As Task
        Dim lStartTime As DateTime = DateTime.Now
        
        Try
            pIsRunning = True
            pRunCancellationTokenSource = New CancellationTokenSource()
            
            ' Update UI
            UpdateStatusBar("Running project...")
            SetRunButtonsEnabled(False)
            
            ' Raise run started event
            RaiseEvent RunStarted(Me, EventArgs.Empty)
            
            ' Output run command
            OutputToPanel("========================================")
            OutputToPanel($"Running project in: {vProjectDirectory}")
            OutputToPanel($"Command: dotnet run")
            OutputToPanel("========================================")
            OutputToPanel("")
            
            ' Create process start info
            Dim lStartInfo As New ProcessStartInfo()
            lStartInfo.FileName = FindDotnetExecutable()
            lStartInfo.Arguments = "run"
            lStartInfo.WorkingDirectory = vProjectDirectory
            lStartInfo.UseShellExecute = False
            lStartInfo.RedirectStandardOutput = True
            lStartInfo.RedirectStandardError = True
            lStartInfo.RedirectStandardInput = True  ' Allow input for interactive programs
            lStartInfo.CreateNoWindow = True
            
            ' Add any configured environment variables
            If pBuildConfiguration IsNot Nothing Then
                for each lVar in pBuildConfiguration.EnvironmentVariables
                    lStartInfo.EnvironmentVariables(lVar.Key) = lVar.Value
                Next
            End If
            
            ' Start the process
            pRunProcess = Process.Start(lStartInfo)
            
            If pRunProcess Is Nothing Then
                Throw New Exception("Failed to start dotnet run process")
            End If
            
            Console.WriteLine($"RunProjectAsync: Process started with PID {pRunProcess.Id}")
            
            ' Read output asynchronously
            Dim lOutputTask As Task = Task.Run(Sub() ReadRunOutput(pRunProcess.StandardOutput))
            Dim lErrorTask As Task = Task.Run(Sub() ReadRunError(pRunProcess.StandardError))
            
            ' Wait for process to exit
            Await Task.Run(Sub()
                pRunProcess.WaitForExit()
            End Sub, pRunCancellationTokenSource.Token)
            
            ' Wait for output reading to complete
            Await Task.WhenAll(lOutputTask, lErrorTask)
            
            ' Get exit code
            Dim lExitCode As Integer = pRunProcess.ExitCode
            Dim lRunTime As TimeSpan = DateTime.Now - lStartTime
            
            ' Output completion message
            OutputToPanel("")
            OutputToPanel("========================================")
            OutputToPanel($"Process exited with code {lExitCode}")
            OutputToPanel($"Run time: {lRunTime.TotalSeconds:F2} seconds")
            OutputToPanel("========================================")
            
            ' Create run result
            Dim lResult As New RunEventArgs() with {
                .Success = (lExitCode = 0),
                .ExitCode = lExitCode,
                .Message = If(lExitCode = 0, "Run completed successfully", $"Run failed with exit code {lExitCode}"),
                .RunTime = lRunTime
            }
            
            ' Raise run completed event
            RaiseEvent RunCompleted(Me, lResult)
            
            ' Update status bar
            If lExitCode = 0 Then
                UpdateStatusBar($"Run completed successfully in {lRunTime.TotalSeconds:F2}s")
            Else
                UpdateStatusBar($"Run failed with exit code {lExitCode}")
            End If
            
        Catch ex As OperationCanceledException
            Console.WriteLine("RunProjectAsync: Cancelled")
            OutputToPanel("")
            OutputToPanel("========================================")
            OutputToPanel("Run cancelled by user")
            OutputToPanel("========================================")
            UpdateStatusBar("Run cancelled")
            
        Catch ex As Exception
            Console.WriteLine($"RunProjectAsync error: {ex.Message}")
            OutputErrorToPanel($"Run error: {ex.Message}")
            UpdateStatusBar($"Run error: {ex.Message}")
            ShowError("Run Error", ex.Message)
            
        Finally
            pIsRunning = False
            pRunProcess = Nothing
            pRunCancellationTokenSource?.Dispose()
            pRunCancellationTokenSource = Nothing
            SetRunButtonsEnabled(True)
        End Try
    End Function
    
    ''' <summary>
    ''' Stop the running project
    ''' </summary>
    Public Sub StopProject()
        Try
            If Not pIsRunning OrElse pRunProcess Is Nothing Then
                Return
            End If
            
            Console.WriteLine("StopProject: Stopping running process")
            
            ' Cancel the token
            pRunCancellationTokenSource?.Cancel()
            
            ' Try graceful shutdown first
            If pRunProcess IsNot Nothing AndAlso Not pRunProcess.HasExited Then
                Try
                    ' Send Ctrl+C to the process
                    pRunProcess.CloseMainWindow()
                    
                    ' Wait briefly for graceful shutdown
                    If Not pRunProcess.WaitForExit(2000) Then
                        ' Force kill if not exited
                        pRunProcess.Kill()
                    End If
                Catch
                    ' If graceful shutdown fails, force kill
                    Try
                        pRunProcess.Kill()
                    Catch
                        ' Process may have already exited
                    End Try
                End Try
            End If
            
            OutputToPanel("")
            OutputToPanel("========================================")
            OutputToPanel("Process stopped by user")
            OutputToPanel("========================================")
            
            UpdateStatusBar("Run stopped")
            
        Catch ex As Exception
            Console.WriteLine($"StopProject error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Build and run the current project
    ''' </summary>
    Public Sub BuildAndRun()
        Try
            Console.WriteLine("BuildAndRun called")
            
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No Project", "Please open a project before building.")
                Return
            End If
            
            ' Initialize build system if needed
            If pBuildManager Is Nothing OrElse pBuildConfiguration Is Nothing Then
                Console.WriteLine("BuildAndRun: Initializing build system")
                InitializeBuildSystem()
            End If
            
            ' Check if already building
            If pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding Then
                Console.WriteLine("BuildAndRun: Build already in progress")
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If
            
            ' Store flag to run after build
            pRunAfterBuild = True
            
            ' Start the build
            Console.WriteLine("BuildAndRun: Calling BuildProject")
            BuildProject()
            
        Catch ex As Exception
            Console.WriteLine($"BuildAndRun error: {ex.Message}")
            ShowError("Build and Run Error", ex.Message)
            pRunAfterBuild = False
        End Try
    End Sub
    
    Private pRunAfterBuild As Boolean = False
    
    ''' <summary>
    ''' Handle build completed for run
    ''' </summary>
    Private Sub OnBuildCompletedForRun(vSender As Object, vArgs As BuildEventArgs)
        Try
            ' Check if we should run after build
            If Not pRunAfterBuild Then
                Return
            End If
            
            pRunAfterBuild = False
            
            ' Check if build was successful
            If vArgs.Result IsNot Nothing AndAlso vArgs.Result.Success Then
                ' Run the project after successful build
                Task.Run(Async Function() 
                    Await RunProject()
                    Return Nothing 
                End Function)
            Else
                UpdateStatusBar("Build failed - run cancelled")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnBuildCompletedForRun error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Helper Methods =====
    
    ''' <summary>
    ''' Read output from the running process
    ''' </summary>
    Private Sub ReadRunOutput(vReader As IO.StreamReader)
        Try
            Dim lLine As String = vReader.ReadLine()
            While lLine IsNot Nothing AndAlso Not pRunCancellationTokenSource.IsCancellationRequested
                ' Output to panel on UI thread
                Application.Invoke(Sub()
                    OutputToPanel(lLine)
                End Sub)
                
                lLine = vReader.ReadLine()
            End While
        Catch ex As Exception
            Console.WriteLine($"ReadRunOutput error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Read error output from the running process
    ''' </summary>
    Private Sub ReadRunError(vReader As IO.StreamReader)
        Try
            Dim lLine As String = vReader.ReadLine()
            While lLine IsNot Nothing AndAlso Not pRunCancellationTokenSource.IsCancellationRequested
                ' Output errors to panel on UI thread
                Application.Invoke(Sub()
                    OutputErrorToPanel(lLine)
                End Sub)
                
                lLine = vReader.ReadLine()
            End While
        Catch ex As Exception
            Console.WriteLine($"ReadRunError error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Output text to the build output panel
    ''' </summary>
    Private Sub OutputToPanel(vText As String)
        Try
            If pBuildOutputPanel IsNot Nothing Then
                pBuildOutputPanel.AppendOutput($"ERROR: {vText}" & Environment.NewLine, "error")
            End If
        Catch ex As Exception
            Console.WriteLine($"OutputToPanel error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Output error text to the build output panel
    ''' </summary>
    Private Sub OutputErrorToPanel(vText As String)
        Try
            If pBuildOutputPanel IsNot Nothing Then
                ' Could format as error (e.g., red text) if supported
                pBuildOutputPanel.AppendOutput($"ERROR: {vText}" & Environment.NewLine)
            End If
        Catch ex As Exception
            Console.WriteLine($"OutputErrorToPanel error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Find the dotnet executable
    ''' </summary>
    Private Function FindDotnetExecutable() As String
        Try
            ' Try common locations
            Dim lPaths() As String = {
                "/usr/bin/dotnet",
                "/usr/local/bin/dotnet",
                "/usr/share/dotnet/dotnet",
                "/opt/dotnet/dotnet"
            }
            
            for each lPath in lPaths
                If File.Exists(lPath) Then
                    Return lPath
                End If
            Next
            
            ' Try using 'which' command
            Dim lWhichProcess As New ProcessStartInfo("which", "dotnet")
            lWhichProcess.RedirectStandardOutput = True
            lWhichProcess.UseShellExecute = False
            lWhichProcess.CreateNoWindow = True
            
            Using lProcess As Process = Process.Start(lWhichProcess)
                Dim lResult As String = lProcess.StandardOutput.ReadToEnd().Trim()
                If Not String.IsNullOrEmpty(lResult) AndAlso File.Exists(lResult) Then
                    Return lResult
                End If
            End Using
            
            ' Default to just "dotnet" and hope it's in PATH
            Return "dotnet"
            
        Catch ex As Exception
            Console.WriteLine($"FindDotnetExecutable error: {ex.Message}")
            Return "dotnet"
        End Try
    End Function
    
    ''' <summary>
    ''' Set the enabled state of run-related buttons
    ''' </summary>
    Private Sub SetRunButtonsEnabled(vEnabled As Boolean)
        Try
            ' Update toolbar buttons if they exist
            ' TODO: Update Run button to Stop button when running
            
            ' Update menu items if they exist
            ' TODO: Update menu items
            
        Catch ex As Exception
            Console.WriteLine($"SetRunButtonsEnabled error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Check if a project is currently running
    ''' </summary>
    Public ReadOnly Property IsProjectRunning As Boolean
        Get
            Return pIsRunning AndAlso pRunProcess IsNot Nothing AndAlso Not pRunProcess.HasExited
        End Get
    End Property
    
End Class
