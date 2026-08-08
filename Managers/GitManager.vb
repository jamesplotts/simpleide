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

        ''' <summary>
        ''' Result of running a single git command - carries the process exit code alongside
        ''' its output text, so callers can actually tell whether the command succeeded
        ''' instead of guessing from output text (which used to be the only signal available,
        ''' and which every Boolean-returning method in this class ignored entirely - they all
        ''' unconditionally returned True after firing the command, so e.g. deleting a branch
        ''' git itself refused to delete was reported back to the UI as a success).
        ''' </summary>
        Private Class GitCommandResult
            Public Property Output As String = ""
            Public Property ErrorText As String = ""
            Public Property ExitCode As Integer = -1
            Public ReadOnly Property Success As Boolean
                Get
                    Return ExitCode = 0
                End Get
            End Property
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

        ''' <summary>
        ''' Checks whether vPath is inside a git repository - walks up through every parent
        ''' directory looking for a .git folder, not just vPath itself.
        ''' </summary>
        ''' <remarks>
        ''' The previous version only checked vPath directly, which reports "not a repository"
        ''' for the standard multi-project layout (each project in its own subfolder under one
        ''' shared repo root) even though the project genuinely is inside a repo. Confirmed
        ''' live this caused "Initialize Repository" to create a nested/shadow .git inside the
        ''' project subfolder when the user accepted the (wrongly-shown) prompt.
        ''' </remarks>
        Public Function IsGitRepository(vPath As String) As Boolean
            Return Not String.IsNullOrEmpty(FindRepositoryRoot(vPath))
        End Function

        ''' <summary>
        ''' Walks up from vPath through every parent directory looking for one containing a
        ''' .git folder, and returns that directory - the actual repository root - or an empty
        ''' string if vPath isn't inside a git repository at all.
        ''' </summary>
        Public Function FindRepositoryRoot(vPath As String) As String
            Try
                If String.IsNullOrEmpty(vPath) Then Return ""

                Dim lCurrent As DirectoryInfo = New DirectoryInfo(vPath)
                Do While lCurrent IsNot Nothing
                    If Directory.Exists(System.IO.Path.Combine(lCurrent.FullName, ".git")) Then
                        Return lCurrent.FullName
                    End If
                    lCurrent = lCurrent.Parent
                Loop

                Return ""

            Catch ex As Exception
                Console.WriteLine($"FindRepositoryRoot error: {ex.Message}")
                Return ""
            End Try
        End Function

        ' Initialize a new git repository
        Public Async Function InitializeRepository(vPath As String) As Task(Of Boolean)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync("init", vPath)
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"InitializeRepository error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Get current branch name
        Public Async Function GetCurrentBranch() As Task(Of String)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync("branch --show-current")
                Return lResult.Output.Trim()

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
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync("status --porcelain")
                Dim lOutput As String = lResult.Output

                If String.IsNullOrEmpty(lOutput) Then Return lFiles

                ' Parse status
                Dim lLines() As String = lOutput.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)

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
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync($"add ""{vFilePath}""")
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"StageFile error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Unstage file
        Public Async Function UnstageFile(vFilePath As String) As Task(Of Boolean)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync($"reset HEAD ""{vFilePath}""")
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"UnstageFile error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Stage all files
        Public Async Function StageAll() As Task(Of Boolean)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync("add -A")
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"StageAll error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Unstages every currently-staged file, resetting the index back to HEAD
        ''' </summary>
        ''' <returns>True if the command actually succeeded (exit code 0), False otherwise</returns>
        Public Async Function UnstageAll() As Task(Of Boolean)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync("reset HEAD")
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"UnstageAll error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Commit changes
        Public Async Function Commit(vMessage As String, Optional vAmend As Boolean = False) As Task(Of Boolean)
            Try
                Dim lCommand As String = "commit -m """ & vMessage.Replace("""", "\""") & """"
                If vAmend Then lCommand &= " --amend"

                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync(lCommand)
                Return lResult.Success

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

                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync(lCommand)
                Dim lOutput As String = lResult.Output

                If String.IsNullOrEmpty(lOutput) Then Return lCommits

                ' Parse commits
                Dim lLines() As String = lOutput.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)

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
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync("branch -a -v")
                Dim lOutput As String = lResult.Output

                If String.IsNullOrEmpty(lOutput) Then Return lBranches

                ' Parse branches
                Dim lLines() As String = lOutput.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)

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
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync(lCommand)
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"CreateBranch error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Checkout branch
        Public Async Function CheckoutBranch(vBranchName As String) As Task(Of Boolean)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync($"checkout {vBranchName}")
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"CheckoutBranch error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Deletes a local branch
        ''' </summary>
        ''' <param name="vBranchName">Name of the local branch to delete</param>
        ''' <param name="vForce">True to force-delete even if not fully merged (git branch -D), False to require a merged branch (git branch -d)</param>
        ''' <returns>True if git actually succeeded (exit code 0), False otherwise - e.g. git
        ''' refusing to delete an unmerged branch under the non-force flag now correctly
        ''' comes back as False instead of being reported as a successful delete</returns>
        Public Async Function DeleteBranch(vBranchName As String, Optional vForce As Boolean = False) As Task(Of Boolean)
            Try
                Dim lFlag As String = If(vForce, "-D", "-d")
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync($"branch {lFlag} {vBranchName}")
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"DeleteBranch error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Pushes to a remote. When vRemote is empty (the default), runs a bare "git push"
        ''' with no remote/branch named, so git falls back to the current branch's own
        ''' configured upstream instead of assuming a remote named "origin" exists - this
        ''' previously always pushed to "origin" even for repos where that remote doesn't
        ''' exist (e.g. renamed, or a fork set up with a differently-named remote).
        ''' </summary>
        Public Async Function Push(Optional vRemote As String = "", Optional vBranch As String = "") As Task(Of Boolean)
            Try
                Dim lCommand As String = "push"
                If Not String.IsNullOrEmpty(vRemote) Then
                    lCommand &= $" {vRemote}"
                    If Not String.IsNullOrEmpty(vBranch) Then
                        lCommand &= $" {vBranch}"
                    End If
                End If

                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync(lCommand)
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"Push error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Pulls from a remote. See Push's remarks - an empty vRemote runs a bare "git pull"
        ''' relying on the branch's configured upstream rather than assuming "origin".
        ''' </summary>
        Public Async Function Pull(Optional vRemote As String = "", Optional vBranch As String = "") As Task(Of Boolean)
            Try
                Dim lCommand As String = "pull"
                If Not String.IsNullOrEmpty(vRemote) Then
                    lCommand &= $" {vRemote}"
                    If Not String.IsNullOrEmpty(vBranch) Then
                        lCommand &= $" {vBranch}"
                    End If
                End If

                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync(lCommand)
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"Pull error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Get diff for file
        Public Async Function GetFileDiff(vFilePath As String, Optional vStaged As Boolean = False) As Task(Of String)
            Try
                Dim lCommand As String = If(vStaged, $"diff --cached ""{vFilePath}""", $"diff ""{vFilePath}""")
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync(lCommand)
                Return lResult.Output

            Catch ex As Exception
                Console.WriteLine($"GetFileDiff error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Gets a file's combined staged+unstaged diff against HEAD - unlike GetFileDiff,
        ''' this shows the full change regardless of whether the file is currently staged
        ''' </summary>
        ''' <param name="vFilePath">Path (relative to the repository root) of the file to diff</param>
        ''' <returns>Unified diff text, or empty string if there is no difference</returns>
        Public Async Function GetFileDiffFromHead(vFilePath As String) As Task(Of String)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync($"diff HEAD -- ""{vFilePath}""")
                Return lResult.Output

            Catch ex As Exception
                Console.WriteLine($"GetFileDiffFromHead error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Gets the full diff introduced by a single commit
        ''' </summary>
        ''' <param name="vCommitHash">Hash (full or abbreviated) of the commit to show</param>
        ''' <returns>Unified diff text for the commit, including its header</returns>
        Public Async Function GetCommitDiff(vCommitHash As String) As Task(Of String)
            Try
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync($"show {vCommitHash}")
                Return lResult.Output

            Catch ex As Exception
                Console.WriteLine($"GetCommitDiff error: {ex.Message}")
                Return ""
            End Try
        End Function

        ' Get remote URLs
        Public Async Function GetRemotes() As Task(Of Dictionary(Of String, String))
            Try
                Dim lRemotes As New Dictionary(Of String, String)

                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync("remote -v")
                Dim lOutput As String = lResult.Output

                If String.IsNullOrEmpty(lOutput) Then Return lRemotes

                Dim lLines() As String = lOutput.Split({Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries)

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
                Dim lResult As GitCommandResult = Await ExecuteGitCommandAsync($"remote add {vName} {vUrl}")
                Return lResult.Success

            Catch ex As Exception
                Console.WriteLine($"AddRemote error: {ex.Message}")
                Return False
            End Try
        End Function

        ' Execute git command asynchronously
        Private Async Function ExecuteGitCommandAsync(vCommand As String, Optional vWorkingDirectory As String = "") As Task(Of GitCommandResult)
            Return Await Task.Run(Function() ExecuteGitCommand(vCommand, vWorkingDirectory))
        End Function

        ''' <summary>
        ''' Runs a single git command and returns both its output text and whether it actually
        ''' succeeded. Every caller in this class used to receive only the output string, with
        ''' no way to distinguish success from failure short of guessing from that text - which
        ''' several callers did via fragile Contains() checks that don't hold up against git's
        ''' real output (e.g. Commit() checking for the literal word "Commit", which a
        ''' successful `git commit` doesn't actually print unless the message happens to
        ''' contain it).
        ''' </summary>
        Private Function ExecuteGitCommand(vCommand As String, Optional vWorkingDirectory As String = "") As GitCommandResult
            Dim lResult As New GitCommandResult()
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

                lResult.ExitCode = lProcess.ExitCode
                lResult.ErrorText = lError

                If lProcess.ExitCode <> 0 AndAlso Not String.IsNullOrEmpty(lError) Then
                    Console.WriteLine($"git command error: {lError}")
                End If

                ' Text-content fallback for callers that display/log the output: if the
                ' command failed and produced nothing on stdout, surface stderr instead so
                ' there's still something to show. Success/failure itself is ExitCode-driven
                ' now (see Success above), not inferred from which stream this text came from.
                lResult.Output = If(String.IsNullOrEmpty(lOutput) AndAlso lProcess.ExitCode <> 0, lError, lOutput)

            Catch ex As Exception
                Console.WriteLine($"ExecuteGitCommand error: {ex.Message}")
                lResult.ExitCode = -1
                lResult.ErrorText = ex.Message
            End Try

            Return lResult
        End Function

    End Class

End Namespace
