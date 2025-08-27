' Managers/BuildManager.vb - Build management functionality (FIXED)
Imports System
Imports System.IO
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Collections.Generic
Imports SimpleIDE.Models


Namespace Managers
    
    ' Build manager class
    Public Class BuildManager
        
        ' Events
        Public Event BuildStarted As EventHandler
        Public Event BuildCompleted As EventHandler(Of BuildEventArgs)
        Public Event OutputReceived As EventHandler(Of String)
        Public Event ErrorReceived As EventHandler(Of String)
        
        ' Private fields
        Private pProjectPath As String = ""
        Private pIsBuilding As Boolean = False
        Private pCurrentProcess As Process = Nothing
        Private pCancellationTokenSource As CancellationTokenSource = Nothing
        Private pOutputBuilder As New StringBuilder()
        Private pErrorBuilder As New StringBuilder()
        Private pBuildConfiguration As BuildConfiguration
        
        ' Properties
        Public Property ProjectPath As String
            Get
                Return pProjectPath
            End Get
            Set(value As String)
                pProjectPath = value
            End Set
        End Property
        
        Public ReadOnly Property IsBuilding As Boolean
            Get
                Return pIsBuilding
            End Get
        End Property

        Public Property Configuration As BuildConfiguration
            Get
                If pBuildConfiguration Is Nothing Then
                    pBuildConfiguration = New BuildConfiguration()
                    pBuildConfiguration.Verbosity = BuildVerbosity.Normal
                End If
                Return pBuildConfiguration
            End Get
            Set(value As BuildConfiguration)
                If Not pIsBuilding Then
                    pBuildConfiguration = value
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Build the project asynchronously
        ''' </summary>
        Public Async Function BuildProjectAsync(vConfiguration As BuildConfiguration) As Task(Of BuildResult)
            Console.WriteLine($"BuildProjectAsync: Starting - ProjectPath = {pProjectPath}")
            
            If pIsBuilding Then
                Console.WriteLine("BuildProjectAsync: Already building")
                Return New BuildResult() with {
                    .Success = False,
                    .Message = "Build already in progress"
                }
            End If
            
            If String.IsNullOrEmpty(pProjectPath) OrElse Not File.Exists(pProjectPath) Then
                Console.WriteLine($"BuildProjectAsync: Invalid project path - {pProjectPath}")
                Return New BuildResult() with {
                    .Success = False,
                    .Message = $"Invalid project path: {pProjectPath}"
                }
            End If
            
            Try
                pIsBuilding = True
                pOutputBuilder.Clear()
                pErrorBuilder.Clear()
                pCancellationTokenSource = New CancellationTokenSource()
                
                ' Raise build started event
                Console.WriteLine("BuildProjectAsync: Raising BuildStarted event")
                RaiseEvent BuildStarted(Me, EventArgs.Empty)
                
                ' Execute restore if needed
                If vConfiguration.RestorePackages Then
                    Console.WriteLine("BuildProjectAsync: Executing restore")
                    Await ExecuteRestoreAsync()
                End If
                
                ' Execute build
                Console.WriteLine("BuildProjectAsync: Executing build")
                Dim lResult As BuildResult = Await ExecuteBuildAsync(vConfiguration)
                
                ' Raise build completed event
                Console.WriteLine($"BuildProjectAsync: Build complete - Success = {lResult.Success}")
                RaiseEvent BuildCompleted(Me, New BuildEventArgs(lResult))
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"BuildProjectAsync error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
                
                Dim lErrorResult As New BuildResult() with {
                    .Success = False,
                    .Message = $"Build failed: {ex.Message}",
                    .ErrorOutput = ex.ToString()
                }
                RaiseEvent BuildCompleted(Me, New BuildEventArgs(lErrorResult))
                Return lErrorResult
                
            Finally
                pIsBuilding = False
                pCurrentProcess = Nothing
                pCancellationTokenSource?.Dispose()
                pCancellationTokenSource = Nothing
            End Try
        End Function

        ''' <summary>
        ''' Execute the actual build command
        ''' </summary>
        Private Async Function ExecuteBuildAsync(vConfiguration As BuildConfiguration) As Task(Of BuildResult)
            Try
                Console.WriteLine("ExecuteBuildAsync: Starting")
                
                Dim lDotnetPath As String = FindDotnetExecutable()
                Console.WriteLine($"ExecuteBuildAsync: Using dotnet at: {lDotnetPath}")
                
                Dim lArguments As String = vConfiguration.GetBuildArguments(pProjectPath)
                Console.WriteLine($"ExecuteBuildAsync: Arguments: {lArguments}")
                
                Dim lStartInfo As New ProcessStartInfo()
                lStartInfo.FileName = lDotnetPath
                lStartInfo.Arguments = lArguments
                lStartInfo.UseShellExecute = False
                lStartInfo.RedirectStandardOutput = True
                lStartInfo.RedirectStandardError = True
                lStartInfo.CreateNoWindow = True
                lStartInfo.WorkingDirectory = Path.GetDirectoryName(pProjectPath)
                
                ' Add environment variables
                for each lVar in vConfiguration.EnvironmentVariables
                    lStartInfo.EnvironmentVariables(lVar.Key) = lVar.Value
                Next
                
                ' Send command to output
                Dim lCommandLine As String = $"{lStartInfo.FileName} {lStartInfo.Arguments}"
                Console.WriteLine($"ExecuteBuildAsync: Executing: {lCommandLine}")
                RaiseEvent OutputReceived(Me, $"Executing: {lCommandLine}{Environment.NewLine}")
                RaiseEvent OutputReceived(Me, $"Working Directory: {lStartInfo.WorkingDirectory}{Environment.NewLine}")
                RaiseEvent OutputReceived(Me, $"========================================{Environment.NewLine}")
                
                Dim lResult As New BuildResult()
                
                ' Start the process
                Console.WriteLine("ExecuteBuildAsync: Starting process")
                Using lProcess As Process = Process.Start(lStartInfo)
                    If lProcess Is Nothing Then
                        Throw New Exception("Failed to start dotnet process")
                    End If
                    
                    pCurrentProcess = lProcess
                    Console.WriteLine($"ExecuteBuildAsync: Process started - PID = {lProcess.Id}")
                    
                    ' Read output asynchronously
                    Dim lOutputTask As Task = Task.Run(Sub() ReadProcessOutput(lProcess.StandardOutput))
                    'Dim lErrorTask As Task = Task.Run(Sub() ReadProcessError(lProcess.StandardError))
                    
                    ' Wait for process to exit (compatible with .NET Standard 2.0)
                    Console.WriteLine("ExecuteBuildAsync: Waiting for process to exit")
                    Await Task.Run(Sub()
                        lProcess.WaitForExit()
                    End Sub)
                    
                    ' Wait for output reading to complete
                    Await Task.WhenAll(lOutputTask)
                    
                    Dim lExitCode As Integer = lProcess.ExitCode
                    Console.WriteLine($"ExecuteBuildAsync: Process exited with code {lExitCode}")
                    
                    lResult.Success = (lExitCode = 0)
                    lResult.ExitCode = lExitCode
                    lResult.Message = If(lResult.Success, "Build succeeded", $"Build failed with exit code {lExitCode}")
                    lResult.Output = pOutputBuilder.ToString()
                    lResult.ErrorOutput = pErrorBuilder.ToString()

                    Console.WriteLine($"pOutputBuilder contents: " + pOutputBuilder.ToString())
                    
                    ' Parse errors and warnings
                    ParseBuildOutput(lResult)
                    
                    pCurrentProcess = Nothing
                End Using
                
                Console.WriteLine($"ExecuteBuildAsync: Complete - Success = {lResult.Success}")
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"ExecuteBuildAsync error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
                
                Return New BuildResult() with {
                    .Success = False,
                    .Message = $"Build error: {ex.Message}",
                    .ErrorOutput = ex.ToString()
                }
            End Try
        End Function

        ' Clean project - FIXED: Now properly parses errors
        Public Async Function CleanProjectAsync() As Task(Of BuildResult)
            If pIsBuilding Then
                Return New BuildResult() with {
                    .Success = False,
                    .Message = "Build already in progress"
                }
            End If
            
            If String.IsNullOrEmpty(pProjectPath) OrElse Not File.Exists(pProjectPath) Then
                Return New BuildResult() with {
                    .Success = False,
                    .Message = "Invalid project path"
                }
            End If
            
            Try
                pIsBuilding = True
                pOutputBuilder.Clear()
                pErrorBuilder.Clear()
                pCancellationTokenSource = New CancellationTokenSource()
                
                ' Raise build started event
                RaiseEvent BuildStarted(Me, EventArgs.Empty)
                
                ' Execute clean
                Await ExecuteCleanAsync()
                
                Dim lResult As New BuildResult() with {
                    .Success = True,
                    .Message = "Clean completed successfully",
                    .Output = pOutputBuilder.ToString(),
                    .ErrorOutput = pErrorBuilder.ToString()  ' FIXED: Use ErrorOutput property
                }
                
                ' Parse any errors/warnings from clean output
                ParseBuildOutput(lResult)
                
                ' Raise build completed event
                RaiseEvent BuildCompleted(Me, New BuildEventArgs(lResult))
                
                Return lResult
                
            Catch ex As Exception
                Dim lErrorResult As New BuildResult() with {
                    .Success = False,
                    .Message = $"Clean failed: {ex.Message}"
                }
                RaiseEvent BuildCompleted(Me, New BuildEventArgs(lErrorResult))
                Return lErrorResult
                
            Finally
                pIsBuilding = False
                pCurrentProcess = Nothing
                pCancellationTokenSource?.Dispose()
                pCancellationTokenSource = Nothing
            End Try
        End Function
        
        ' Cancel build
        Public Sub CancelBuild()
            Try
                If pIsBuilding AndAlso pCurrentProcess IsNot Nothing Then
                    pCancellationTokenSource?.Cancel()
                    
                    If Not pCurrentProcess.HasExited Then
                        pCurrentProcess.Kill()
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"Error cancelling build: {ex.Message}")
            End Try
        End Sub
        
        ' Execute clean command
        Private Async Function ExecuteCleanAsync() As Task
            Try
                RaiseEvent OutputReceived(Me, "Cleaning project..." & Environment.NewLine)
                
                Dim lStartInfo As New ProcessStartInfo()
                lStartInfo.FileName = FindDotnetExecutable()
                lStartInfo.Arguments = $"clean ""{pProjectPath}"""
                lStartInfo.UseShellExecute = False
                lStartInfo.RedirectStandardOutput = True
                lStartInfo.RedirectStandardError = True
                lStartInfo.CreateNoWindow = True
                lStartInfo.WorkingDirectory = Path.GetDirectoryName(pProjectPath)
                
                Using lProcess As Process = Process.Start(lStartInfo)
                    pCurrentProcess = lProcess
                    
                    ' Read output asynchronously
                    Dim lOutputTask As Task = Task.Run(Sub() ReadProcessOutput(lProcess.StandardOutput))
                    'Dim lErrorTask As Task = Task.Run(Sub() ReadProcessError(lProcess.StandardError))
                    
                    Await lProcess.WaitForExitAsync(pCancellationTokenSource.Token)
                    Await Task.WhenAll(lOutputTask)
                    
                    pCurrentProcess = Nothing
                End Using
                
            Catch ex As Exception
                RaiseEvent ErrorReceived(Me, $"Clean failed: {ex.Message}" & Environment.NewLine)
            End Try
        End Function
        
        ''' <summary>
        ''' Execute restore command
        ''' </summary>
        Private Async Function ExecuteRestoreAsync() As Task
            Try
                Console.WriteLine("ExecuteRestoreAsync: Starting")
                RaiseEvent OutputReceived(Me, "Restoring packages..." & Environment.NewLine)
                
                Dim lDotnetPath As String = FindDotnetExecutable()
                Dim lStartInfo As New ProcessStartInfo()
                lStartInfo.FileName = lDotnetPath
                lStartInfo.Arguments = $"restore ""{pProjectPath}"""
                lStartInfo.UseShellExecute = False
                lStartInfo.RedirectStandardOutput = True
                lStartInfo.RedirectStandardError = True
                lStartInfo.CreateNoWindow = True
                lStartInfo.WorkingDirectory = Path.GetDirectoryName(pProjectPath)
                
                Console.WriteLine($"ExecuteRestoreAsync: {lStartInfo.FileName} {lStartInfo.Arguments}")
                
                Using lProcess As Process = Process.Start(lStartInfo)
                    If lProcess Is Nothing Then
                        Throw New Exception("Failed to start dotnet restore process")
                    End If
                    
                    pCurrentProcess = lProcess
                    
                    ' Read output asynchronously
                    Dim lOutputTask As Task = Task.Run(Sub() ReadProcessOutput(lProcess.StandardOutput))
                    'Dim lErrorTask As Task = Task.Run(Sub() ReadProcessError(lProcess.StandardError))
                    
                    ' Wait for process to exit
                    Await Task.Run(Sub()
                        lProcess.WaitForExit()
                    End Sub)
                    
                    Await Task.WhenAll(lOutputTask)
                    
                    pCurrentProcess = Nothing
                End Using
                
                Console.WriteLine("ExecuteRestoreAsync: Complete")
                RaiseEvent OutputReceived(Me, "Restoring packages complete." & Environment.NewLine)

            Catch ex As Exception
                Console.WriteLine($"ExecuteRestoreAsync error: {ex.Message}")
                RaiseEvent ErrorReceived(Me, $"Restore failed: {ex.Message}" & Environment.NewLine)
            End Try
        End Function
        
        ''' <summary>
        ''' Read process standard output
        ''' </summary>
        Private Sub ReadProcessOutput(vReader As StreamReader)
            Try
                Dim lLine As String = vReader.ReadLine()
                While lLine IsNot Nothing
                    Console.Writeline($"Appending Output To pOutputBuilder: " + lLine)
                    pOutputBuilder.AppendLine(lLine)
                    RaiseEvent OutputReceived(Me, lLine & Environment.NewLine)
                    
                    If pCancellationTokenSource?.IsCancellationRequested = True Then
                        Exit While
                    End If
                    
                    lLine = vReader.ReadLine()
                End While
            Catch ex As Exception
                Console.WriteLine($"ReadProcessOutput error: {ex.Message}")
            End Try
        End Sub
        
'         ''' <summary>
'         ''' Read process error output
'         ''' </summary>
'         Private Sub ReadProcessError(vReader As StreamReader)
'             Try
'                  Console.Writeline($"ReadProcessError Called")
'                 Dim lLine As String = vReader.ReadLine()
'                 While lLine IsNot Nothing
'  Console.Writeline($"Appending Output To pErrorBuilder: " + lLine)
'                     pErrorBuilder.AppendLine(lLine)
'                     RaiseEvent ErrorReceived(Me, lLine & Environment.NewLine)
'                     
'                     If pCancellationTokenSource?.IsCancellationRequested = True Then
'                         Exit While
'                     End If
'                     
'                     lLine = vReader.ReadLine()
'                 End While
'             Catch ex As Exception
'                 Console.WriteLine($"ReadProcessError error: {ex.Message}")
'             End Try
'         End Sub
        

        
        ' Parse error/warning line - FIXED: Return proper typed object
        Private Function ParseErrorWarningLine(vLine As String, vIsError As Boolean) As Object
            Try
                ' Pattern: filename(line,column): error/warning CODE: message
                Dim lPattern As String = "^(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+(\w+):\s+(.*)$"
                Dim lMatch As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(vLine, lPattern)
                
                If lMatch.Success Then
                    If vIsError Then
                        Dim lBuildError As New BuildError()
                        lBuildError.FilePath = lMatch.Groups(1).Value.Trim()
                        lBuildError.Line = Integer.Parse(lMatch.Groups(2).Value)
                        lBuildError.Column = Integer.Parse(lMatch.Groups(3).Value)
                        lBuildError.ErrorCode = lMatch.Groups(5).Value.Trim()
                        lBuildError.Message = lMatch.Groups(6).Value.Trim()
                        Return lBuildError
                    Else
                        Dim lBuildWarning As New BuildWarning()
                        lBuildWarning.FilePath = lMatch.Groups(1).Value.Trim()
                        lBuildWarning.Line = Integer.Parse(lMatch.Groups(2).Value)
                        lBuildWarning.Column = Integer.Parse(lMatch.Groups(3).Value)
                        lBuildWarning.WarningCode = lMatch.Groups(5).Value.Trim()
                        lBuildWarning.Message = lMatch.Groups(6).Value.Trim()
                        Return lBuildWarning
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Error parsing error/warning line: {ex.Message}")
            End Try
            
            Return Nothing
        End Function
        
        ''' <summary>
        ''' Find the dotnet executable
        ''' </summary>
        Private Function FindDotnetExecutable() As String
            Try
                ' First try 'which dotnet' command
                Try
                    Using lProcess As New Process()
                        lProcess.StartInfo.FileName = "which"
                        lProcess.StartInfo.Arguments = "dotnet"
                        lProcess.StartInfo.UseShellExecute = False
                        lProcess.StartInfo.RedirectStandardOutput = True
                        lProcess.StartInfo.CreateNoWindow = True
                        lProcess.Start()
                        
                        Dim lPath As String = lProcess.StandardOutput.ReadToEnd().Trim()
                        lProcess.WaitForExit()
                        
                        If Not String.IsNullOrEmpty(lPath) AndAlso File.Exists(lPath) Then
                            Console.WriteLine($"FindDotnetExecutable: Found via 'which': {lPath}")
                            Return lPath
                        End If
                    End Using
                Catch
                    ' Ignore errors from 'which' command
                End Try
                
                ' Common paths to check
                Dim lPossiblePaths As New List(Of String) From {
                    "/usr/bin/dotnet",
                    "/usr/local/bin/dotnet",
                    "/usr/share/dotnet/dotnet",
                    "/opt/dotnet/dotnet",
                    "/snap/dotnet-sdk/current/dotnet"
                }
                
                ' Check PATH environment variable
                Dim lPathEnv As String = Environment.GetEnvironmentVariable("PATH")
                If Not String.IsNullOrEmpty(lPathEnv) Then
                    Dim lPaths() As String = lPathEnv.Split(":"c)
                    For Each lDir As String In lPaths
                        If Not String.IsNullOrEmpty(lDir) Then
                            lPossiblePaths.Add(Path.Combine(lDir, "dotnet"))
                        End If
                    Next
                End If
                
                ' Find first existing executable
                For Each lExePath As String In lPossiblePaths
                    If File.Exists(lExePath) Then
                        Console.WriteLine($"FindDotnetExecutable: Found at: {lExePath}")
                        Return lExePath
                    End If
                Next
                
                ' Default to just "dotnet" and hope it's in PATH
                Console.WriteLine("FindDotnetExecutable: Using Default 'dotnet' command")
                Return "dotnet"
                
            Catch ex As Exception
                Console.WriteLine($"FindDotnetExecutable error: {ex.Message}")
                Return "dotnet"
            End Try
        End Function
        

        ''' <summary>
        ''' Parse build output for errors and warnings with deduplication
        ''' </summary>
        ''' <param name="vResult">The BuildResult to populate with parsed errors and warnings</param>
        ''' <remarks>
        ''' Deduplicates errors and warnings based on file, line, column, and message
        ''' </remarks>
        Private Sub ParseBuildOutput(vResult As BuildResult)
            Try
                Dim lAllOutput As String = pOutputBuilder.ToString() & Environment.NewLine & pErrorBuilder.ToString()
                Dim lLines() As String = lAllOutput.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                
                ' Use HashSets to track unique errors and warnings
                Dim lProcessedErrors As New HashSet(Of String)
                Dim lProcessedWarnings As New HashSet(Of String)
                
                for each lLine As String in lLines
                    ' Parse error pattern: file(line,column): error CODE: message
                    Dim lErrorMatch As Match = Regex.Match(lLine, "^(.+?)\((\d+),(\d+)\):\s+error\s+(\w+):\s+(.+)$")
                    If lErrorMatch.Success Then
                        Dim lError As New BuildError()
                        lError.FilePath = lErrorMatch.Groups(1).Value.Trim()
                        lError.Line = Integer.Parse(lErrorMatch.Groups(2).Value)
                        lError.Column = Integer.Parse(lErrorMatch.Groups(3).Value)
                        lError.ErrorCode = lErrorMatch.Groups(4).Value.Trim()
                        lError.Message = lErrorMatch.Groups(5).Value.Trim()
                        
                        ' Create a unique key for this error
                        Dim lErrorKey As String = $"{lError.FilePath}|{lError.Line}|{lError.Column}|{lError.ErrorCode}|{lError.Message}"
                        
                        ' Only add if we haven't seen this exact error before
                        If lProcessedErrors.Add(lErrorKey) Then
                            vResult.Errors.Add(lError)
                            Console.WriteLine($"Added error: {lError.FilePath}({lError.Line},{lError.Column}): {lError.ErrorCode}")
                        Else
                            Console.WriteLine($"Skipped duplicate error: {lErrorKey}")
                        End If
                        
                        Continue for
                    End If
                    
                    ' Parse warning pattern: file(line,column): warning CODE: message
                    Dim lWarningMatch As Match = Regex.Match(lLine, "^(.+?)\((\d+),(\d+)\):\s+warning\s+(\w+):\s+(.+)$")
                    If lWarningMatch.Success Then
                        Dim lWarning As New BuildWarning()
                        lWarning.FilePath = lWarningMatch.Groups(1).Value.Trim()
                        lWarning.Line = Integer.Parse(lWarningMatch.Groups(2).Value)
                        lWarning.Column = Integer.Parse(lWarningMatch.Groups(3).Value)
                        lWarning.WarningCode = lWarningMatch.Groups(4).Value.Trim()
                        lWarning.Message = lWarningMatch.Groups(5).Value.Trim()
                        
                        ' Create a unique key for this warning
                        Dim lWarningKey As String = $"{lWarning.FilePath}|{lWarning.Line}|{lWarning.Column}|{lWarning.WarningCode}|{lWarning.Message}"
                        
                        ' Only add if we haven't seen this exact warning before
                        If lProcessedWarnings.Add(lWarningKey) Then
                            vResult.Warnings.Add(lWarning)
                            Console.WriteLine($"Added warning: {lWarning.FilePath}({lWarning.Line},{lWarning.Column}): {lWarning.WarningCode}")
                        Else
                            Console.WriteLine($"Skipped duplicate warning: {lWarningKey}")
                        End If
                    End If
                Next
                
                ' Update counts
                vResult.ErrorCount = vResult.Errors.Count
                vResult.WarningCount = vResult.Warnings.Count
                
                Console.WriteLine($"ParseBuildOutput complete: {vResult.ErrorCount} unique errors, {vResult.WarningCount} unique warnings")
                
            Catch ex As Exception
                Console.WriteLine($"ParseBuildOutput error: {ex.Message}")
            End Try
        End Sub

     End Class
   
    
End Namespace
