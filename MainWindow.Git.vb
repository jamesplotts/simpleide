' MainWindow.Git.vb - Git integration for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports System.Diagnostics
Imports SimpleIDE.Editors
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Partial Public Class MainWindow
    
    ' ===== Git Integration =====
    Private pGitManager As GitManager
    ' In MainWindow private fields:
    Private pGitStatusLabel As Label
    Private pGitStatusTimer As UInteger = 0    
    

   Private Sub OnShowGitStatus(vSender As Object, vE As EventArgs)
        Try
            ' Update Git panel with current project root
            pBottomPanelManager.SetProjectRoot(pCurrentProject)
            
            ' Show the panel and switch to Git tab
            pBottomPanelManager.ShowTabByType(BottomPanelManager.BottomPanelTab.eGit)
            
            
            UpdateStatusBar("git Status shown")
            
        Catch ex As Exception
            Console.WriteLine($"error showing git Status: {ex.Message}")
            ShowError($"git error", $"error showing git Status: {ex.Message}")
        End Try
    End Sub

    ' Initialize Git repository for current project
    Public Sub GitInitRepository()
        Try
            Dim lProjectDir As String = GetActiveGitRepositoryDirectory()
            If String.IsNullOrEmpty(lProjectDir) Then
                ShowError("No project", "Please open a project before initializing git.")
                Return
            End If

            ' Check if already a git repository - walks up through parent directories, not
            ' just lProjectDir itself, so a project living in its own subfolder under a
            ' shared repo root (the standard multi-project layout) is correctly recognized
            ' as already being in a repository instead of prompting to create a nested one
            If pGitManager Is Nothing Then pGitManager = New GitManager()
            If pGitManager.IsGitRepository(lProjectDir) Then
                ShowInfo("git Repository", "this project is already a git repository.")
                Return
            End If
            
            ' Confirm initialization
            Dim lResponse As Integer = ShowQuestion(
                "Initialize git Repository",
                "Initialize a git repository for this project?"
            )
            
            If lResponse = CInt(ResponseType.Yes) Then
                ' Initialize repository
                ExecuteGitCommand("init", lProjectDir, Sub(output, ExitCode)
                    Application.Invoke(Sub()
                        If ExitCode = 0 Then
                            ' Create .gitignore if it doesn't exist
                            CreateGitIgnore(lProjectDir)
                            
                            ' Initial commit
                            Dim lCommitResponse As Integer = ShowQuestion(
                                "Initial Commit",
                                "Create an initial Commit?"
                            )
                            
                            If lCommitResponse = CInt(ResponseType.Yes) Then
                                ' Add all files
                                ExecuteGitCommand("add .", lProjectDir, Sub(addOutput, addExitCode)
                                    If addExitCode = 0 Then
                                        ' Commit
                                        ExecuteGitCommand("commit -m ""Initial Commit""", lProjectDir, Sub(commitOutput, commitExitCode)
                                            Application.Invoke(Sub()
                                                If commitExitCode = 0 Then
                                                    ShowInfo("git", "Repository initialized with initial Commit.")
                                                    RefreshGitStatus()
                                                Else
                                                    ShowError("git error", "Failed to create initial Commit.")
                                                End If
                                            End Sub)
                                        End Sub)
                                    End If
                                End Sub)
                            Else
                                ShowInfo("git", "Repository initialized.")
                                RefreshGitStatus()
                            End If
                        Else
                            ShowError("git error", $"Failed to Initialize repository: {output}")
                        End If
                    End Sub)
                End Sub)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"GitInitRepository error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ' Show Git status
    Public Sub ShowGitStatus()
        Try
            If Not EnsureGitRepository() Then Return
            
            ' Show Git panel in bottom panel
            ShowBottomPanel(CInt(BottomPanelManager.BottomPanelTab.eGit))
            pBottomPanelManager.GitPanel?.ShowChangesTab()

            ' Refresh status
            RefreshGitStatus()
            
        Catch ex As Exception
            Console.WriteLine($"ShowGitStatus error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ' Add all files to staging
    Public Sub GitAddAll()
        Try
            If Not EnsureGitRepository() Then Return

            Dim lProjectDir As String = GetActiveGitRepositoryDirectory()
            
            ExecuteGitCommand("add .", lProjectDir, Sub(output, ExitCode)
                Application.Invoke(Sub()
                    If ExitCode = 0 Then
                        UpdateStatusBar("All files staged")
                        RefreshGitStatus()
                    Else
                        ShowError("git error", $"Failed to stage files: {output}")
                    End If
                End Sub)
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"GitAddAll error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ' Show commit dialog
    Public Sub ShowGitCommitDialog()
        Try
            If Not EnsureGitRepository() Then Return

            InitializeGitManager() ' GitCommitDialog takes a GitManager - never populated otherwise, since nothing else called this
            Dim lProjectDir As String = GetActiveGitRepositoryDirectory()
            
            ' Check if there are changes to commit
            ExecuteGitCommand("status --porcelain", lProjectDir, Sub(output, ExitCode)
                Application.Invoke(Sub()
                    If ExitCode = 0 AndAlso String.IsNullOrWhiteSpace(output) Then
                        ShowInfo("git", "No Changes to Commit.")
                        Return
                    End If
                    
                    ' Show commit dialog
                    Dim lDialog As New GitCommitDialog(Me, pGitManager)
                    
                    If lDialog.Run() = CInt(ResponseType.Ok) Then
                        Dim lMessage As String = lDialog.CommitMessage

                        If Not String.IsNullOrWhiteSpace(lMessage) Then
                            ' Perform commit - Amend/Sign-off checkboxes previously had zero
                            ' effect on the actual git invocation; now threaded through as the
                            ' real --amend/--signoff flags
                            Dim lCommand As String = $"commit -m ""{lMessage.Replace("""", """""")}"""
                            If lDialog.AmendCommit Then lCommand &= " --amend"
                            If lDialog.SignOff Then lCommand &= " --signoff"

                            ExecuteGitCommand(lCommand, lProjectDir, Sub(commitOutput, commitExitCode)
                                Application.Invoke(Sub()
                                    If commitExitCode = 0 Then
                                        ShowInfo("git", "Changes committed successfully.")
                                        RefreshGitStatus()
                                    Else
                                        ShowError("git error", $"Commit failed: {commitOutput}")
                                    End If
                                End Sub)
                            End Sub)
                        End If
                    End If
                    
                    lDialog.Destroy()
                End Sub)
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"ShowGitCommitDialog error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ' Git push
    Public Sub GitPush()
        Try
            If Not EnsureGitRepository() Then Return

            Dim lProjectDir As String = GetActiveGitRepositoryDirectory()

            ' Check if remote is configured
            ExecuteGitCommand("remote -v", lProjectDir, Sub(remoteOutput, remoteExitCode)
                Application.Invoke(Sub()
                    If remoteExitCode = 0 AndAlso String.IsNullOrWhiteSpace(remoteOutput) Then
                        ShowError("git", "No remote repository configured. Add a remote first.")
                        Return
                    End If
                    
                    ' Get current branch
                    ExecuteGitCommand("branch --show-current", lProjectDir, Sub(branchOutput, branchExitCode)
                        If branchExitCode = 0 Then
                            Dim lBranch As String = branchOutput.Trim()

                            UpdateStatusBar("Pushing to remote...")

                            ' Push to remote
                            ExecuteGitCommand($"push origin {lBranch}", lProjectDir, Sub(pushOutput, pushExitCode)
                                Application.Invoke(Sub()
                                    If pushExitCode = 0 Then
                                        ShowInfo("git", "Successfully pushed to remote.")
                                        RefreshGitStatus()
                                    Else
                                        ShowError("git error", $"Push failed: {pushOutput}")
                                    End If
                                    UpdateStatusBar("Ready")
                                End Sub)
                            End Sub)
                        End If
                    End Sub)
                End Sub)
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"GitPush error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced GitPull that properly handles file reloading
    ''' </summary>
    Public Sub GitPull()
        Try
            If Not EnsureGitRepository() Then Return

            Dim lProjectDir As String = GetActiveGitRepositoryDirectory()

            UpdateStatusBar("Pulling from remote...")
            
            ExecuteGitCommand("pull", lProjectDir, Sub(output, exitCode)
                Application.Invoke(Sub()
                    If exitCode = 0 Then
                        ShowInfo("Git Pull", "Successfully pulled from remote.")
                        RefreshGitStatus()
                        
                        ' Use enhanced reload that uses ReloadFile
                        ReloadModifiedFiles()
                        
                        ' Refresh project explorer to show any new files
                        If pProjectExplorer IsNot Nothing Then
                            pProjectExplorer.RefreshProject()
                        End If
                    Else
                        ShowError("Git Error", $"Pull failed: {output}")
                    End If
                    UpdateStatusBar("Ready")
                End Sub)
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"GitPullEnhanced error: {ex.Message}")
            ShowError("Git Error", ex.Message)
        End Try
    End Sub   

    ''' <summary>
    ''' Git checkout with file reloading
    ''' </summary>
    ''' <param name="vBranch">Branch name to checkout</param>
    Public Sub GitCheckout(vBranch As String)
        Try
            If Not EnsureGitRepository() Then Return

            Dim lProjectDir As String = GetActiveGitRepositoryDirectory()

            ' Check for uncommitted changes first
            ExecuteGitCommand("status --porcelain", lProjectDir, Sub(statusOutput, statusExitCode)
                Application.Invoke(Sub()
                    If Not String.IsNullOrWhiteSpace(statusOutput) Then
                        ' Has uncommitted changes
                        Dim lResponse As Integer = ShowQuestion(
                            "Uncommitted Changes",
                            "You have uncommitted changes. Checking out another branch will discard them. Continue?"
                        )
                        
                        If lResponse <> CInt(ResponseType.Yes) Then
                            Return
                        End If
                    End If
                    
                    ' Proceed with checkout
                    UpdateStatusBar($"Checking out branch '{vBranch}'...")
                    
                    ExecuteGitCommand($"checkout {vBranch}", lProjectDir, Sub(output, exitCode)
                        Application.Invoke(Sub()
                            If exitCode = 0 Then
                                ShowInfo("Git Checkout", $"Successfully checked out branch '{vBranch}'.")
                                RefreshGitStatus()
                                
                                ' Reload all modified files using ReloadFile
                                ReloadModifiedFiles()
                                
                                ' Refresh project explorer
                                If pProjectExplorer IsNot Nothing Then
                                    pProjectExplorer.RefreshProject()
                                End If
                            Else
                                ShowError("Git Error", $"Checkout failed: {output}")
                            End If
                            UpdateStatusBar("Ready")
                        End Sub)
                    End Sub)
                End Sub)
            End Sub)
            
        Catch ex As Exception
            Console.WriteLine($"GitCheckout error: {ex.Message}")
            ShowError("Git Error", ex.Message)
        End Try
    End Sub 

    ' Show branch dialog
    Public Sub ShowGitBranchDialog()
        Try
            If Not EnsureGitRepository() Then Return

            InitializeGitManager()

            Using lDialog As New GitBranchDialog(Me, pGitManager, pThemeManager)
                If lDialog.Run() = CInt(ResponseType.Ok) AndAlso Not String.IsNullOrEmpty(lDialog.SelectedBranch) Then
                    GitCheckout(lDialog.SelectedBranch)
                End If
                lDialog.Destroy()
            End Using

        Catch ex As Exception
            Console.WriteLine($"ShowGitBranchDialog error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ' Show Git history
    Public Sub ShowGitHistory()
        Try
            If Not EnsureGitRepository() Then Return

            ' Show Git panel and switch to history tab
            ShowBottomPanel(CInt(BottomPanelManager.BottomPanelTab.eGit))
            pBottomPanelManager.GitPanel?.ShowHistoryTab()

        Catch ex As Exception
            Console.WriteLine($"ShowGitHistory error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ' Show Git settings
    Public Sub ShowGitSettings()
        Try
            ' Open (or switch to) the Preferences tab, then land on its Git sub-tab
            OnEditPreferences(Nothing, Nothing)
            pPreferencesTab?.SelectGitTab()

        Catch ex As Exception
            Console.WriteLine($"ShowGitSettings error: {ex.Message}")
            ShowError("git error", ex.Message)
        End Try
    End Sub
    
    ' ===== Helper Methods =====

    ''' <summary>
    ''' Returns the directory the Git menu commands below should operate on - the repo
    ''' GitPanel is currently showing (whatever the user picked via its repo picker, when
    ''' a multi-repo solution is loaded) if available, otherwise the startup project's own
    ''' directory
    ''' </summary>
    ''' <remarks>
    ''' Before this, every command here (GitPush, GitPull, GitAddAll, ShowGitCommitDialog,
    ''' GitInitRepository, GitCheckout, ShowGitBranchDialog) derived its working directory
    ''' straight from pCurrentProject - the startup project - regardless of which repo
    ''' GitPanel itself was showing. For this exact repo (SimpleIDE / SimpleIDE.Widgets /
    ''' SimpleIDE.WebKitGtk are three separate git repositories joined by one .sln), that
    ''' meant every Git menu command was silently only ever able to touch SimpleIDE's own
    ''' repo, with no way to push/pull/commit/branch the other two projects' repos at all.
    ''' </remarks>
    Private Function GetActiveGitRepositoryDirectory() As String
        If pGitPanel IsNot Nothing AndAlso Not String.IsNullOrEmpty(pGitPanel.ProjectRoot) Then
            Return pGitPanel.ProjectRoot
        End If
        Return If(String.IsNullOrEmpty(pCurrentProject), "", System.IO.Path.GetDirectoryName(pCurrentProject))
    End Function

    ' Ensure project is a Git repository
    Private Function EnsureGitRepository() As Boolean
        Try
            Dim lProjectDir As String = GetActiveGitRepositoryDirectory()
            If String.IsNullOrEmpty(lProjectDir) Then
                ShowError("No project", "Please open a project first.")
                Return False
            End If

            If pGitManager Is Nothing Then pGitManager = New GitManager()
            If Not pGitManager.IsGitRepository(lProjectDir) Then
                Dim lResponse As Integer = ShowQuestion(
                    "Not a git Repository",
                    "this project is not a git repository. Initialize one?"
                )
                
                If lResponse = CInt(ResponseType.Yes) Then
                    GitInitRepository()
                    Return False ' Will be initialized asynchronously
                Else
                    Return False
                End If
            End If
            
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"EnsureGitRepository error: {ex.Message}")
            Return False
        End Try
    End Function
    
    ' Execute Git command
    Private Sub ExecuteGitCommand(vCommand As String, vWorkingDir As String, vCallback As Action(Of String, Integer))
        Task.Run(Sub()
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "git"
                lProcess.StartInfo.Arguments = vCommand
                lProcess.StartInfo.WorkingDirectory = vWorkingDir
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardOutput = True
                lProcess.StartInfo.RedirectStandardError = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                
                Dim lOutput As String = lProcess.StandardOutput.ReadToEnd()
                Dim lError As String = lProcess.StandardError.ReadToEnd()
                
                lProcess.WaitForExit()
                
                ' Combine output and error
                Dim lFullOutput As String = lOutput
                If Not String.IsNullOrEmpty(lError) Then
                    lFullOutput = If(String.IsNullOrEmpty(lFullOutput), lError, lFullOutput & Environment.NewLine & lError)
                End If
                
                vCallback(lFullOutput, lProcess.ExitCode)
                
            Catch ex As Exception
                vCallback(ex.Message, -1)
            End Try
        End Sub)
    End Sub
    
    ' Refresh Git status
    Private Sub RefreshGitStatus()
        Try
            If pGitPanel IsNot Nothing Then
                pGitPanel.Refresh()
            End If
            
            ' Update toolbar Git button
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"RefreshGitStatus error: {ex.Message}")
        End Try
    End Sub
    
    ' Create .gitignore file
    Private Sub CreateGitIgnore(vProjectDir As String)
        Try
            Dim lGitIgnorePath As String = System.IO.Path.Combine(vProjectDir, ".gitignore")
            
            If Not File.Exists(lGitIgnorePath) Then
                Dim lContent As String = "# Build outputs" & Environment.NewLine &
                                       "bin/" & Environment.NewLine &
                                       "obj/" & Environment.NewLine &
                                       Environment.NewLine &
                                       "# User files" & Environment.NewLine &
                                       "*.user" & Environment.NewLine &
                                       "*.suo" & Environment.NewLine &
                                       Environment.NewLine &
                                       "# IDE files" & Environment.NewLine &
                                       ".vs/" & Environment.NewLine &
                                       ".vscode/" & Environment.NewLine &
                                       Environment.NewLine &
                                       "# Temporary files" & Environment.NewLine &
                                       "*.tmp" & Environment.NewLine &
                                       "*.temp" & Environment.NewLine &
                                       "~*" & Environment.NewLine &
                                       Environment.NewLine &
                                       "# Logs" & Environment.NewLine &
                                       "*.log" & Environment.NewLine &
                                       Environment.NewLine &
                                       "# OS files" & Environment.NewLine &
                                       ".DS_Store" & Environment.NewLine &
                                       "Thumbs.db"
                
                File.WriteAllText(lGitIgnorePath, lContent)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"CreateGitIgnore error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Reload files that were modified by git operations (pull, checkout, revert, etc.)
    ''' </summary>
    Private Sub ReloadModifiedFiles()
        Try
            ' Track files that need reloading
            Dim lFilesToReload As New List(Of TabInfo)()
            Dim lFilesWithConflicts As New List(Of TabInfo)()
            
            ' Check each open file to see if it was modified
            for each lTab in pOpenTabs.Values
                If System.IO.File.Exists(lTab.FilePath) Then
                    Dim lFileTime As DateTime = System.IO.File.GetLastWriteTime(lTab.FilePath)
                    
                    ' Check if file was modified after last save
                    If lFileTime > lTab.LastSaved Then
                        If lTab.Modified Then
                            ' File has unsaved changes and was also modified externally
                            lFilesWithConflicts.Add(lTab)
                        Else
                            ' File has no unsaved changes, can reload automatically
                            lFilesToReload.Add(lTab)
                        End If
                    End If
                End If
            Next
            
            ' Automatically reload files without conflicts
            for each lTab in lFilesToReload
                Console.WriteLine($"Auto-reloading {lTab.FilePath} after git operation")
                ReloadFileAfterGitOperation(lTab, False) ' Don't prompt, just reload
            Next
            
            ' Handle files with conflicts (unsaved changes + external modifications)
            If lFilesWithConflicts.Count > 0 Then
                HandleGitConflictedFiles(lFilesWithConflicts)
            End If
            
            ' Show summary if files were reloaded
            If lFilesToReload.Count > 0 OrElse lFilesWithConflicts.Count > 0 Then
                Dim lMessage As String = ""
                If lFilesToReload.Count > 0 Then
                    lMessage = $"Reloaded {lFilesToReload.Count} file(s) modified by git operation."
                End If
                If lFilesWithConflicts.Count > 0 Then
                    If lMessage.Length > 0 Then lMessage &= Environment.NewLine
                    lMessage &= $"Handled {lFilesWithConflicts.Count} file(s) with conflicts."
                End If
                UpdateStatusBar(lMessage)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ReloadModifiedFiles error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handle files that have both unsaved changes and external git modifications
    ''' </summary>
    ''' <param name="vConflictedFiles">List of files with conflicts</param>
    Private Sub HandleGitConflictedFiles(vConflictedFiles As List(Of TabInfo))
        Try
            for each lTab in vConflictedFiles
                Dim lResponse As ResponseType = ShowCustomButtonDialog(
                    MessageType.Warning,
                    $"The file '{System.IO.Path.GetFileName(lTab.FilePath)}' has unsaved changes " &
                    $"but was also modified by the git operation.{Environment.NewLine}{Environment.NewLine}" &
                    "What would you Like To Do?",
                    New String() {"Keep My Changes", "Reload from Git", "Save As..."},
                    New ResponseType() {ResponseType.No, ResponseType.Yes, ResponseType.Apply})

                Select Case lResponse
                    Case ResponseType.Yes
                        ' Reload from git (discard local changes)
                        ReloadFileAfterGitOperation(lTab, False)

                    Case ResponseType.No
                        ' Keep local changes
                        Console.WriteLine($"Keeping local changes for {lTab.FilePath}")
                        ' File remains modified

                    Case ResponseType.Apply
                        ' Save local changes with a different name
                        SaveFileAs(lTab)
                        ' Then reload original from git
                        ReloadFileAfterGitOperation(lTab, False)
                End Select
            Next
            
        Catch ex As Exception
            Console.WriteLine($"HandleGitConflictedFiles error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Reload a single file after a git operation using ReloadFile
    ''' </summary>
    ''' <param name="vTabInfo">The tab to reload</param>
    ''' <param name="vPrompt">Whether to prompt the user before reloading</param>
    Private Sub ReloadFileAfterGitOperation(vTabInfo As TabInfo, vPrompt As Boolean)
        Try
            If vTabInfo Is Nothing OrElse String.IsNullOrEmpty(vTabInfo.FilePath) Then
                Return
            End If
            
            ' Prompt if requested
            If vPrompt Then
                Dim lResponse As Integer = ShowQuestion(
                    "File Modified by Git",
                    $"The file '{System.IO.Path.GetFileName(vTabInfo.FilePath)}' was modified by the git operation.{Environment.NewLine}Reload it from disk?"
                )
                
                If lResponse <> CInt(ResponseType.Yes) Then
                    Return
                End If
            End If

            vTabInfo.Editor.SourceFileInfo.LoadContent()
            
        Catch ex As Exception
            Console.WriteLine($"ReloadFileAfterGitOperation error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnGitPush(vSender As Object, vArgs As EventArgs)
        Try
            GitPush()
        Catch ex As Exception
            Console.WriteLine($"OnGitPush error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnGitPull(vSender As Object, vArgs As EventArgs)
        Try
            GitPull()
        Catch ex As Exception
            Console.WriteLine($"OnGitPull error: {ex.Message}")
        End Try
    End Sub
    
    ' Initialize Git manager
    Private Sub InitializeGitManager()
        Try
            If pGitManager Is Nothing Then
                pGitManager = New GitManager()
            End If

            ' ShowGitBranchDialog set this explicitly itself right after calling this method
            ' - ShowGitCommitDialog called this same method expecting the same thing but never
            ' set it, so GitCommitDialog's GitManager had no RepositoryPath at all. Setting it
            ' here covers every caller in one place - via GetActiveGitRepositoryDirectory so
            ' it follows whichever repo GitPanel is currently showing, not always the
            ' startup project's
            Dim lActiveDir As String = GetActiveGitRepositoryDirectory()
            If Not String.IsNullOrEmpty(lActiveDir) Then
                pGitManager.RepositoryPath = lActiveDir
            End If

        Catch ex As Exception
            Console.WriteLine($"InitializeGitManager error: {ex.Message}")
        End Try
    End Sub
    
End Class
