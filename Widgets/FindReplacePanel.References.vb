' Widgets/FindReplacePanel.References.vb - Find All References across every project in a
' loaded solution, reusing the existing in-memory project search infrastructure
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Widgets

    Partial Public Class FindReplacePanel

        ''' <summary>
        ''' Finds every whole-word occurrence of a symbol across every project in a loaded
        ''' solution (or just the current project when no solution is loaded), showing
        ''' results in this panel's existing results grid/Find Results tab
        ''' </summary>
        ''' <remarks>
        ''' Reuses the same per-file matcher (SearchInMemoryFile) that the single-project
        ''' SearchInProjectOptimized already uses, looped across every solution member
        ''' project's already-loaded SourceFiles instead of just pProjectManager's. File
        ''' paths are already fully-qualified absolute paths, so results from different
        ''' projects remain naturally distinguishable in the grid without any extra
        ''' attribution - unlike Build Solution's merged error/warning lists, there's no
        ''' filename-collision risk here since the grid already shows full directory paths.
        ''' </remarks>
        ''' <param name="vWord">The identifier to search for (whole-word, case-insensitive)</param>
        ''' <param name="vSolutionManager">The loaded solution, or Nothing for a plain single-project search</param>
        Public Sub FindAllReferences(vWord As String, vSolutionManager As SolutionManager)
            Try
                If String.IsNullOrWhiteSpace(vWord) Then
                    pStatusLabel.Text = "No symbol to search for"
                    Return
                End If

                pFindEntry.Text = vWord
                pWholeWordCheck.Active = True
                pInProjectRadio.Active = True

                pLastSearchOptions = New SearchOptions With {
                    .SearchText = vWord,
                    .MatchCase = False,
                    .WholeWord = True,
                    .UseRegex = False,
                    .Scope = SearchScope.eProject
                }

                pResultsGrid.ClearRows()
                pSearchResults.Clear()
                pCurrentMatches = Nothing

                If vSolutionManager Is Nothing OrElse vSolutionManager.AllProjects.Count <= 1 Then
                    ' No solution (or a single-project one) - the existing single-project
                    ' in-memory search already does exactly what's needed here
                    SearchInProjectOptimized(Sub() UpdateSearchHighlights())
                    Return
                End If

                pProgressBar.Visible = True
                pStatusLabel.Text = "Searching solution..."

                Dim lProjects As New List(Of ProjectManager)(vSolutionManager.AllProjects)

                Task.Run(Sub()
                    Dim lAllResults As New List(Of FindResult)
                    Try
                        for each lProject In lProjects
                            Dim lSourceFiles As Dictionary(Of String, SourceFileInfo) = lProject.SourceFiles
                            If lSourceFiles Is Nothing Then Continue for

                            for each lFileEntry In lSourceFiles
                                Dim lSourceFile As SourceFileInfo = lFileEntry.Value
                                If Not lSourceFile.IsLoaded OrElse lSourceFile.TextLines Is Nothing Then Continue for

                                Dim lFileResults As List(Of FindResult) = SearchInMemoryFile(lSourceFile, lFileEntry.Key, pLastSearchOptions)
                                lAllResults.AddRange(lFileResults)
                            Next
                        Next
                    Catch ex As Exception
                        Console.WriteLine($"FindAllReferences solution search error: {ex.Message}")
                    End Try

                    lAllResults.Sort(Function(a, b)
                        Dim lFileCompare As Integer = String.Compare(a.FilePath, b.FilePath)
                        If lFileCompare <> 0 Then Return lFileCompare
                        Return a.LineNumber.CompareTo(b.LineNumber)
                    End Function)

                    Gtk.Application.Invoke(Sub()
                        Try
                            pSearchResults = lAllResults
                            PopulateSortableResults(pSearchResults)
                            pStatusLabel.Text = $"Found {lAllResults.Count} reference(s) of '{vWord}' across {lProjects.Count} project(s)"
                            pProgressBar.Visible = False
                            UpdateSearchHighlights()
                        Catch ex As Exception
                            Console.WriteLine($"FindAllReferences completion error: {ex.Message}")
                        End Try
                    End Sub)
                End Sub)

            Catch ex As Exception
                Console.WriteLine($"FindAllReferences error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
