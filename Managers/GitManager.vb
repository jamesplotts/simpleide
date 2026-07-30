' Utilities/GitManager.vb - Git operations manager
Imports System
Imports System.IO
Imports System.Diagnostics
Imports System.Collections.Generic
Imports System.Threading.Tasks

Namespace Managers
    
    Public Class GitManager
        
        ' Git file status enumeration
        Public Enum FileStatus
            eUnmodified
            eModified
            eAdded
            eDeleted
            eRenamed
            eCopied
            eUntracked
            eIgnored
            eConflicted
        End Enum
        
        ' Git file info
        Public Class GitFileInfo
            Public Property Path As String
            Public Property Status As FileStatus
            Public Property IsStaged As Boolean
            Public Property OldPath As String ' for renames
        End Class
        
        ' Git commit info
        Public Class CommitInfo
            Public Property Hash As String
            Public Property Author As String
            Public Property Email As String
            Public Property CommitDate As DateTime
            Public Property Message As String
            Public Property ParentHashes As List(Of String)
        End Class
        
        ' Branch info
        Public Class BranchInfo
            Public Property Name As String
            Public Property IsLocal As Boolean
            Public Property IsRemote As Boolean
            Public Property IsCurrent As Boolean
            Public Property TrackingBranch As String
        End Class
        
        ' Properties
        Private pRepositoryPath As String
        
        Public Property RepositoryPath As String
            Get
                Return pRepositoryPath
            End Get
            Set(Value As String)
                pRepositoryPath = Value
            End Set
        End Property
        
        ' Constructor
        Public Sub New(Optional vRepositoryPath As String = "")
            pRepositoryPath = vRepositoryPath
        End Sub
        
        ' Check if directory is a git repository
        Public Function IsGitRepository(vPath As String) As Boolean
            Try
                If String.IsNullOrEmpty(vPath) Then Return False
                
                Dim lGitDir As String = System.IO.Path.Combine(vPath, ".git")
                Return Directory.Exists(lGitDir)
                
            Catch ex As Exception
                Console.WriteLine($"IsGitRepository error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Initialize a new git repository
        Public Async Function InitializeRepository(vPath As String) As Task(Of Boolean)
            Try
                Dim lResult As String = Await ExecuteGitCommandAsync("init", vPath)
                Return lResult.Contains("Initialized")
                
            Catch ex As Exception
                Console.WriteLine($"InitializeRepository error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get current branch name
        Public Async Function GetCurrentBranch() As Task(Of String)
            Try
                Dim lResult As String = Await ExecuteGitCommandAsync("branch --Show-current")
                Return lResult.Trim()
                
            Catch ex As Exception
                Console.WriteLine($"GetCurrentBranch error: {ex.Message}")
                Return "master"
            End Try
        End Function
        
        ' Get file status
        Public Async Function GetFileStatus() As Task(Of List(Of GitFileInfo))
            Try
                Dim lFiles As New List(Of GitFileInfo)
                
                ' Get status output
                Dim lResult As String = Await ExecuteGitCommandAsync("Status --porcelain")
                
                If String.IsNullOrEmpty(lResult) Then Return lFiles
                
                ' Parse status
                Dim lLines() As String = lResult.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                
                For Each lLine In lLines
                    If lLine.Length < 3 Then Continue For
                    
                    Dim lFile As New GitFileInfo()
                    Dim lStatusCode As String = lLine.Substring(0, 2)
                    lFile.Path = lLine.Substring(3).Trim()
                    
                    ' Parse status code
                    Select Case lStatusCode
                        Case "??"
                            lFile.Status = FileStatus.eUntracked
                            lFile.IsStaged = False
                        Case " M", "M "
                            lFile.Status = FileStatus.eModified
                            lFile.IsStaged = (lStatusCode(0) = "M"c)
                        Case "A ", " A"
                            lFile.Status = FileStatus.eAdded
                            lFile.IsStaged = True
                        Case "D ", " D"
                            lFile.Status = FileStatus.eDeleted
                            lFile.IsStaged = (lStatusCode(0) = "D"c)
                        Case "r "
                            lFile.Status = FileStatus.eRenamed
                            lFile.IsStaged = True
                            ' Parse rename info
                            Dim lParts() As String = lFile.Path.Split({" -> "}, StringSplitOptions.None)
                            If lParts.Length = 2 Then
                                lFile.OldPath = lParts(0)
                                lFile.Path = lParts(1)
                            End If
                        Case "C "
                            lFile.Status = FileStatus.eCopied
                            lFile.IsStaged = True
                        Case "UU"
                            lFile.Status = FileStatus.eConflicted
                            lFile.IsStaged = False
                    End Select
                    
                    lFiles.Add(lFile)
                Next
                
                Return lFiles
                
            Catch ex As Exception
                Console.WriteLine($"GetFileStatus error: {ex.Message}")
                Return New List(Of GitFileInfo)
            End Try
        End Function
        
        ' Stage file
        Public Async Function StageFile(vFilePath As String) As Task(Of Boolean)
            Try
                Await ExecuteGitCommandAsync($"add ""{vFilePath}""")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"StageFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Unstage file
        Public Async Function UnstageFile(vFilePath As String) As Task(Of Boolean)
            Try
                Await ExecuteGitCommandAsync($"reset HEAD ""{vFilePath}""")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"UnstageFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Stage all files
        Public Async Function StageAll() As Task(Of Boolean)
            Try
                Await ExecuteGitCommandAsync("add -A")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"StageAll error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Commit changes
        Public Async Function Commit(vMessage As String, Optional vAmend As Boolean = False) As Task(Of Boolean)
            Try
                Dim lCommand As String = "Commit -m """ & vMessage.Replace("""", "\""") & """"
                If vAmend Then lCommand &= " --amend"
                
                Dim lResult As String = Await ExecuteGitCommandAsync(lCommand)
                Return lResult.Contains("Commit")
                
            Catch ex As Exception
                Console.WriteLine($"Commit error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get commit history
        Public Async Function GetCommitHistory(Optional vLimit As Integer = 50) As Task(Of List(Of CommitInfo))
            Try
                Dim lCommits As New List(Of CommitInfo)
                
                ' Get log with specific format
                Dim lFormat As String = "--pretty=format:%H|%an|%ae|%ad|%s|%P"
                Dim lCommand As String = $"log {lFormat} --date=iso -n {vLimit}"
                
                Dim lResult As String = Await ExecuteGitCommandAsync(lCommand)
                
                If String.IsNullOrEmpty(lResult) Then Return lCommits
                
                ' Parse commits
                Dim lLines() As String = lResult.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                
                For Each lLine In lLines
                    Dim lParts() As String = lLine.Split("|"c)
                    If lParts.Length >= 5 Then
                        Dim lCommit As New CommitInfo()
                        lCommit.Hash = lParts(0)
                        lCommit.Author = lParts(1)
                        lCommit.Email = lParts(2)
                        
                        ' Parse date
                        DateTime.TryParse(lParts(3), lCommit.CommitDate)
                        
                        lCommit.Message = lParts(4)
                        
                        ' Parse parent hashes
                        lCommit.ParentHashes = New List(Of String)
                        If lParts.Length > 5 AndAlso Not String.IsNullOrEmpty(lParts(5)) Then
                            lCommit.ParentHashes.AddRange(lParts(5).Split(" "c))
                        End If
                        
                        lCommits.Add(lCommit)
                    End If
                Next
                
                Return lCommits
                
            Catch ex As Exception
                Console.WriteLine($"GetCommitHistory error: {ex.Message}")
                Return New List(Of CommitInfo)
            End Try
        End Function
        
        ' Get branches
        Public Async Function GetBranches() As Task(Of List(Of BranchInfo))
            Try
                Dim lBranches As New List(Of BranchInfo)
                
                ' Get all branches (local and remote)
                Dim lResult As String = Await ExecuteGitCommandAsync("branch -a -v")
                
                If String.IsNullOrEmpty(lResult) Then Return lBranches
                
                ' Parse branches
                Dim lLines() As String = lResult.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                
                For Each lLine In lLines
                    Dim lBranch As New BranchInfo()
                    Dim lTrimmed As String = lLine.Trim()
                    
                    ' Check if current branch
                    lBranch.IsCurrent = lTrimmed.StartsWith("*")
                    If lBranch.IsCurrent Then
                        lTrimmed = lTrimmed.Substring(1).Trim()
                    End If
                    
                    ' Check if remote branch
                    If lTrimmed.StartsWith("remotes/") Then
                        lBranch.IsRemote = True
                        lBranch.IsLocal = False
                        lTrimmed = lTrimmed.Substring(8) ' Remove "remotes/"
                    Else
                        lBranch.IsLocal = True
                        lBranch.IsRemote = False
                    End If
                    
                    ' Extract branch name
                    Dim lSpaceIndex As Integer = lTrimmed.IndexOf(" ")
                    If lSpaceIndex > 0 Then
                        lBranch.Name = lTrimmed.Substring(0, lSpaceIndex)
                    Else
                        lBranch.Name = lTrimmed
                    End If
                    
                    lBranches.Add(lBranch)
                Next
                
                Return lBranches
                
            Catch ex As Exception
                Console.WriteLine($"GetBranches error: {ex.Message}")
                Return New List(Of BranchInfo)
            End Try
        End Function
        
        ' Create new branch
        Public Async Function CreateBranch(vBranchName As String, Optional vCheckout As Boolean = True) As Task(Of Boolean)
            Try
                Dim lCommand As String = If(vCheckout, $"checkout -b {vBranchName}", $"branch {vBranchName}")
                Await ExecuteGitCommandAsync(lCommand)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"CreateBranch error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Checkout branch
        Public Async Function CheckoutBranch(vBranchName As String) As Task(Of Boolean)
            Try
                Await ExecuteGitCommandAsync($"checkout {vBranchName}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"CheckoutBranch error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Push to remote
        Public Async Function Push(Optional vRemote As String = "origin", Optional vBranch As String = "") As Task(Of Boolean)
            Try
                Dim lCommand As String = $"Push {vRemote}"
                If Not String.IsNullOrEmpty(vBranch) Then
                    lCommand &= $" {vBranch}"
                End If
                
                Await ExecuteGitCommandAsync(lCommand)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"Push error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Pull from remote
        Public Async Function Pull(Optional vRemote As String = "origin", Optional vBranch As String = "") As Task(Of Boolean)
            Try
                Dim lCommand As String = $"Pull {vRemote}"
                If Not String.IsNullOrEmpty(vBranch) Then
                    lCommand &= $" {vBranch}"
                End If
                
                Await ExecuteGitCommandAsync(lCommand)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"Pull error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get diff for file
        Public Async Function GetFileDiff(vFilePath As String, Optional vStaged As Boolean = False) As Task(Of String)
            Try
                Dim lCommand As String = If(vStaged, $"diff --cached ""{vFilePath}""", $"diff ""{vFilePath}""")
                Return Await ExecuteGitCommandAsync(lCommand)
                
            Catch ex As Exception
                Console.WriteLine($"GetFileDiff error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' Get remote URLs
        Public Async Function GetRemotes() As Task(Of Dictionary(Of String, String))
            Try
                Dim lRemotes As New Dictionary(Of String, String)
                
                Dim lResult As String = Await ExecuteGitCommandAsync("remote -v")
                
                If String.IsNullOrEmpty(lResult) Then Return lRemotes
                
                Dim lLines() As String = lResult.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                
                For Each lLine In lLines
                    If lLine.Contains("(fetch)") Then
                        Dim lParts() As String = lLine.Split({vbTab, " "}, StringSplitOptions.RemoveEmptyEntries)
                        If lParts.Length >= 2 Then
                            lRemotes(lParts(0)) = lParts(1)
                        End If
                    End If
                Next
                
                Return lRemotes
                
            Catch ex As Exception
                Console.WriteLine($"GetRemotes error: {ex.Message}")
                Return New Dictionary(Of String, String)
            End Try
        End Function
        
        ' Add remote
        Public Async Function AddRemote(vName As String, vUrl As String) As Task(Of Boolean)
            Try
                Await ExecuteGitCommandAsync($"remote add {vName} {vUrl}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"AddRemote error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Execute git command asynchronously
        Private Async Function ExecuteGitCommandAsync(vCommand As String, Optional vWorkingDirectory As String = "") As Task(Of String)
            Return Await Task.Run(Function() ExecuteGitCommand(vCommand, vWorkingDirectory))
        End Function
        
        ' Execute git command
        Private Function ExecuteGitCommand(vCommand As String, Optional vWorkingDirectory As String = "") As String
            Try
                Dim lWorkDir As String = If(String.IsNullOrEmpty(vWorkingDirectory), pRepositoryPath, vWorkingDirectory)
                
                If String.IsNullOrEmpty(lWorkDir) Then
                    Throw New InvalidOperationException("No repository Path specified")
                End If
                
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "git"
                lProcess.StartInfo.Arguments = vCommand
                lProcess.StartInfo.WorkingDirectory = lWorkDir
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardOutput = True
                lProcess.StartInfo.RedirectStandardError = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                
                Dim lOutput As String = lProcess.StandardOutput.ReadToEnd()
                Dim lError As String = lProcess.StandardError.ReadToEnd()
                
                lProcess.WaitForExit()
                
                If lProcess.ExitCode <> 0 AndAlso Not String.IsNullOrEmpty(lError) Then
                    Console.WriteLine($"git command error: {lError}")
                    ' Some commands return useful info in stderr even on success
                    If String.IsNullOrEmpty(lOutput) Then
                        Return lError
                    End If
                End If
                
                Return lOutput
                
            Catch ex As Exception
                Console.WriteLine($"ExecuteGitCommand error: {ex.Message}")
                Return ""
            End Try
        End Function
        
    End Class
    
End Namespace
