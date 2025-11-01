' FindReplacePanel.InMemorySearch.vb
' Optimized in-memory search implementation using ProjectManager's loaded files
' Created: 2025-08-24

Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Widgets
    
    Partial Public Class FindReplacePanel
        
        ' New fields for in-memory search
        Private pProjectManager As ProjectManager
        Private pSearchCancellation As CancellationTokenSource
        
        ' Event to request ProjectManager from MainWindow
        Public Event OnRequestProjectManager As EventHandler(Of ProjectManagerEventArgs)
        
        Public Class ProjectManagerEventArgs
            Inherits EventArgs
            Public Property ProjectManager As ProjectManager
        End Class
        
        ''' <summary>
        ''' Sets the ProjectManager for in-memory searching
        ''' </summary>
        ''' <param name="vProjectManager">The project manager instance</param>
        Public Sub SetProjectManager(vProjectManager As ProjectManager)
            pProjectManager = vProjectManager
            Console.WriteLine($"FindReplacePanel: ProjectManager set - {If(pProjectManager IsNot Nothing, "Success", "Nothing")}")
        End Sub
        
        ''' <summary>
        ''' Enhanced search that uses in-memory SourceFileInfo instances
        ''' </summary>
        Private Sub SearchInProjectOptimized()
            Try
                ' First try to get ProjectManager if we don't have it
                If pProjectManager Is Nothing Then
                    Dim lArgs As New ProjectManagerEventArgs()
                    RaiseEvent OnRequestProjectManager(Me, lArgs)
                    pProjectManager = lArgs.ProjectManager
                End If
                
                ' Fall back to file-based search if no ProjectManager
                If pProjectManager Is Nothing Then
                    Console.WriteLine("FindReplacePanel: No ProjectManager available, using file-based search")
                    SearchInProject() ' Call original method
                    Return
                End If
                
                ' Check if we have source files loaded
                Dim lSourceFiles As Dictionary(Of String, SourceFileInfo) = pProjectManager.SourceFiles
                If lSourceFiles Is Nothing OrElse lSourceFiles.Count = 0 Then
                    pStatusLabel.Text = "No project files loaded"
                    Return
                End If
                
                ' Show progress
                pProgressBar.Visible = True
                pCancelButton.Visible = True
                pIsSearching = True
                
                ' Create cancellation token
                pSearchCancellation = New CancellationTokenSource()
                Dim lCancellationToken As CancellationToken = pSearchCancellation.Token
                
                ' Concurrent collection for results
                Dim lConcurrentResults As New ConcurrentBag(Of FindResult)()
                Dim lTotalMatches As Integer = 0
                Dim lFilesSearched As Integer = 0
                Dim lTotalFiles As Integer = lSourceFiles.Count
                
                ' Create search tasks for each file
                Dim lSearchTasks As New List(Of Task)()
                
                For Each lFileEntry In lSourceFiles
                    Dim lSourceFile As SourceFileInfo = lFileEntry.Value
                    Dim lFilePath As String = lFileEntry.Key
                    
                    ' Skip files that aren't loaded or don't have text lines
                    If Not lSourceFile.IsLoaded OrElse lSourceFile.TextLines Is Nothing Then
                        Continue For
                    End If
                    
                    ' Create task for this file
                    Dim lTask As Task = Task.Run(Sub()
                        Try
                            
                            ' Search in this file's TextLines
                            Dim lFileResults As List(Of FindResult) = SearchInMemoryFile(
                                lSourceFile, 
                                lFilePath, 
                                pLastSearchOptions
                            )
                            
                            ' Add results to concurrent collection
                            For Each lResult In lFileResults
                                lConcurrentResults.Add(lResult)
                            Next
                            
                            ' Update progress (thread-safe)
                            Interlocked.Increment(lFilesSearched)
                            Interlocked.Add(lTotalMatches, lFileResults.Count)
                            
                            ' Update UI on main thread
                            Gtk.Application.Invoke(Sub()
                                If Not lCancellationToken.IsCancellationRequested Then
                                    pProgressBar.Fraction = CDbl(lFilesSearched) / CDbl(lTotalFiles)
                                    pStatusLabel.Text = $"Searching... ({lFilesSearched}/{lTotalFiles} files)"
                                End If
                            End Sub)
                            
                        Catch ex As Exception
                            Console.WriteLine($"Search task error for {lFilePath}: {ex.Message}")
                        End Try
                    End Sub, lCancellationToken)
                    
                    lSearchTasks.Add(lTask)
                Next
                
                ' Wait for all tasks to complete
                Task.Run(Async Function()
                    Try
                        Await Task.WhenAll(lSearchTasks)
                        
                        ' Update UI on completion
                        Gtk.Application.Invoke(Sub()
                            If Not lCancellationToken.IsCancellationRequested Then
                                ' Convert concurrent results to list
                                pSearchResults = lConcurrentResults.ToList()
                                
                                ' Sort results by file path and line number
                                pSearchResults.Sort(Function(a, b)
                                    Dim lFileCompare As Integer = String.Compare(a.FilePath, b.FilePath)
                                    If lFileCompare <> 0 Then Return lFileCompare
                                    Return a.LineNumber.CompareTo(b.LineNumber)
                                End Function)
                                
                                ' Update tree view
                                pResultsStore.Clear()
                                For Each lResult In pSearchResults
                                    pResultsStore.AppendValues(
                                        System.IO.Path.GetFileName(lResult.FilePath),
                                        lResult.LineText,
                                        lResult.LineNumber,
                                        lResult.ColumnNumber,
                                        lResult.MatchText
                                    )
                                Next
                                
                                ' Update status
                                pStatusLabel.Text = $"Found {lTotalMatches} match(es) in {lFilesSearched} file(s)"
                            Else
                                pStatusLabel.Text = "Search cancelled"
                            End If
                            
                            ' Hide progress
                            pProgressBar.Visible = False
                            pCancelButton.Visible = False
                            pIsSearching = False
                        End Sub)
                        
                    Catch ex As Exception
                        Gtk.Application.Invoke(Sub()
                            Console.WriteLine($"Search completion error: {ex.Message}")
                            pStatusLabel.Text = "Search error: " & ex.Message
                            pProgressBar.Visible = False
                            pCancelButton.Visible = False
                            pIsSearching = False
                        End Sub)
                    End Try
                    
                    Return Nothing
                End Function)
                
            Catch ex As Exception
                Console.WriteLine($"SearchInProjectOptimized error: {ex.Message}")
                pStatusLabel.Text = "Search error: " & ex.Message
                pProgressBar.Visible = False
                pCancelButton.Visible = False
                pIsSearching = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Searches within a single SourceFileInfo's TextLines array
        ''' </summary>
        Private Function SearchInMemoryFile(vSourceFile As SourceFileInfo, 
                                           vFilePath As String,
                                           vOptions As SearchOptions) As List(Of FindResult)
            
            Dim lResults As New List(Of FindResult)()
            
            Try
                ' Prepare regex if needed
                Dim lRegex As Regex = Nothing
                If vOptions.UseRegex Then
                    Dim lRegexOptions As RegexOptions = If(vOptions.MatchCase, 
                        RegexOptions.None, RegexOptions.IgnoreCase)
                    lRegex = New Regex(vOptions.SearchText, lRegexOptions)
                End If
                
                ' Search through each line
                For lLineIndex As Integer = 0 To vSourceFile.TextLines.Count - 1
                    
                    Dim lLine As String = vSourceFile.TextLines(lLineIndex)
                    If String.IsNullOrEmpty(lLine) Then Continue For
                    
                    ' Find matches in this line
                    Dim lMatches As List(Of Integer) = FindMatchesInLineOptimized(
                        lLine, vOptions, lRegex
                    )
                    
                    ' Create FindResult for each match
                    For Each lColumn In lMatches
                        Dim lResult As New FindResult With {
                            .FilePath = vFilePath,
                            .LineNumber = lLineIndex + 1,  ' Convert to 1-based
                            .ColumnNumber = lColumn + 1,   ' Convert to 1-based
                            .LineText = lLine.Trim(),
                            .MatchText = vOptions.SearchText,
                            .MatchLength = vOptions.SearchText.Length
                        }
                        
                        ' For regex matches, get actual match length
                        If vOptions.UseRegex AndAlso lRegex IsNot Nothing Then
                            Dim lMatch As Match = lRegex.Match(lLine, lColumn)
                            If lMatch.Success Then
                                lResult.MatchLength = lMatch.Length
                                lResult.MatchText = lMatch.Value
                            End If
                        End If
                        
                        lResults.Add(lResult)
                    Next
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SearchInMemoryFile error for {vFilePath}: {ex.Message}")
            End Try
            
            Return lResults
        End Function
        
        ''' <summary>
        ''' Optimized version of FindMatchesInLine that reuses regex
        ''' </summary>
        Private Function FindMatchesInLineOptimized(vLine As String, 
                                                   vOptions As SearchOptions,
                                                   vRegex As Regex) As List(Of Integer)
            
            Dim lMatches As New List(Of Integer)()
            
            Try
                If vOptions.UseRegex AndAlso vRegex IsNot Nothing Then
                    ' Regex search
                    For Each lMatch As Match In vRegex.Matches(vLine)
                        lMatches.Add(lMatch.Index)
                    Next
                Else
                    ' Plain text search
                    Dim lComparison As StringComparison = If(vOptions.MatchCase, 
                        StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)
                    
                    Dim lSearchText As String = vOptions.SearchText
                    Dim lIndex As Integer = 0
                    
                    While lIndex >= 0
                        lIndex = vLine.IndexOf(lSearchText, lIndex, lComparison)
                        If lIndex >= 0 Then
                            ' Check whole word if needed
                            If vOptions.WholeWord Then
                                Dim lIsWholeWord As Boolean = True
                                
                                ' Check character before
                                If lIndex > 0 Then
                                    Dim lCharBefore As Char = vLine(lIndex - 1)
                                    If Char.IsLetterOrDigit(lCharBefore) OrElse lCharBefore = "_"c Then
                                        lIsWholeWord = False
                                    End If
                                End If
                                
                                ' Check character after
                                If lIsWholeWord AndAlso lIndex + lSearchText.Length < vLine.Length Then
                                    Dim lCharAfter As Char = vLine(lIndex + lSearchText.Length)
                                    If Char.IsLetterOrDigit(lCharAfter) OrElse lCharAfter = "_"c Then
                                        lIsWholeWord = False
                                    End If
                                End If
                                
                                If lIsWholeWord Then
                                    lMatches.Add(lIndex)
                                End If
                            Else
                                lMatches.Add(lIndex)
                            End If
                            
                            lIndex += 1
                        End If
                    End While
                End If
                
            Catch ex As Exception
                Console.WriteLine($"FindMatchesInLineOptimized error: {ex.Message}")
            End Try
            
            Return lMatches
        End Function
        
        ''' <summary>
        ''' Override the original ExecuteSearch to use optimized version
        ''' </summary>
        Private Sub ExecuteSearchOptimized()
            Try
                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    pStatusLabel.Text = "Please enter search text"
                    Return
                End If
                
                ' Save search options
                pLastSearchOptions = New SearchOptions With {
                    .SearchText = pFindEntry.Text,
                    .ReplaceText = pReplaceEntry.Text,
                    .MatchCase = pCaseSensitiveCheck.Active,
                    .WholeWord = pWholeWordCheck.Active,
                    .UseRegex = pRegexCheck.Active,
                    .Scope = If(pInProjectRadio.Active, SearchScope.eProject, SearchScope.eCurrentFile)
                }
                
                ' Clear previous results
                pResultsStore.Clear()
                pSearchResults.Clear()
                pCurrentMatches = Nothing
                pCurrentMatchIndex = -1
                
                If pInFileRadio.Active Then
                    SearchInCurrentFile()  ' Use existing method for current file
                Else
                    SearchInProjectOptimized()  ' Use new optimized method
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExecuteSearchOptimized error: {ex.Message}")
                pStatusLabel.Text = "Search error: " & ex.Message
            End Try
        End Sub
        
        ''' <summary>
        ''' Modified OnCancel to handle task cancellation
        ''' </summary>
        Private Sub OnCancelOptimized(vSender As Object, vE As EventArgs)
            Try
                ' Cancel any running search tasks
                If pSearchCancellation IsNot Nothing Then
                    pSearchCancellation.Cancel()
                End If
                
                pIsSearching = False
                pCancelButton.Visible = False
                pProgressBar.Visible = False
                pStatusLabel.Text = "Search cancelled"
                
            Catch ex As Exception
                Console.WriteLine($"OnCancelOptimized error: {ex.Message}")
            End Try
        End Sub

'        ''' <summary>
'        ''' Ensures all project files are loaded before searching
'        ''' </summary>
'        ''' <returns>True if files are loaded and ready, False otherwise</returns>
'        Private Function EnsureFilesLoadedBeforeSearch() As Boolean
'            Try
'                ' Check if we have ProjectManager
'                If pProjectManager Is Nothing Then
'                    Dim lArgs As New ProjectManagerEventArgs()
'                    RaiseEvent OnRequestProjectManager(Me, lArgs)
'                    pProjectManager = lArgs.ProjectManager
'                End If
'                
'                If pProjectManager Is Nothing Then
'                    Console.WriteLine("FindReplacePanel: No ProjectManager available")
'                    Return False
'                End If
'                
'                ' Ensure all files are loaded
'                pStatusLabel.Text = "Loading project files..."
'                Gtk.Application.Invoke(Sub()
'                    While Gtk.Application.EventsPending()
'                        Gtk.Application.RunIteration()
'                    End While
'                End Sub)
'                
'                Dim lLoadedCount As Integer = pProjectManager.EnsureAllFilesLoaded()
'                
'                If lLoadedCount = 0 Then
'                    pStatusLabel.Text = "No project files to search"
'                    Return False
'                End If
'                
'                ' Get statistics
'                Dim lStats = pProjectManager.GetLoadedFileStats()
'                Console.WriteLine($"Files ready for search: {lStats.LoadedFiles}/{lStats.TotalFiles} files, {lStats.TotalLines} total lines")
'                
'                Return lStats.LoadedFiles > 0
'                
'            Catch ex As Exception
'                Console.WriteLine($"EnsureFilesLoadedBeforeSearch error: {ex.Message}")
'                Return False
'            End Try
'        End Function
        
'        ''' <summary>
'        ''' Enhanced search that ensures files are loaded first
'        ''' </summary>
'        Private Sub SearchInProjectOptimizedWithPreload()
'            Try
'                ' Ensure files are loaded
'                If Not EnsureFilesLoadedBeforeSearch() Then
'                    pStatusLabel.Text = "Unable to load project files"
'                    Return
'                End If
'                
'                ' Now perform the optimized search
'                SearchInProjectOptimized()
'                
'            Catch ex As Exception
'                Console.WriteLine($"SearchInProjectOptimizedWithPreload error: {ex.Message}")
'                pStatusLabel.Text = "Search error: " & ex.Message
'            End Try
'        End Sub
        
    End Class
    
End Namespace
