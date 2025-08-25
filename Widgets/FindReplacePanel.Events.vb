' FindReplacePanel.Events.vb
' Simplified version without live tracking - user must click Find to refresh results

Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Linq
Imports SimpleIDE.Models

Namespace Widgets
    Partial Public Class FindReplacePanel

        ' Replace: SimpleIDE.Widgets.FindReplacePanel.OnFindEntryKeyPress
        ''' <summary>
        ''' Handles key press events in the find entry field
        ''' </summary>
        ''' <param name="vSender">The sender of the event</param>
        ''' <param name="vArgs">Key press event arguments</param>
        Private Sub OnFindEntryKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        ' Enter key - Execute Find All
                        Console.WriteLine($"OnFindEntryKeyPress: Enter pressed - executing Find All!")
                        
                        ' Call OnFind which uses ExecuteSearchOptimized for Find All
                        OnFind(Nothing, Nothing)
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.Escape
                        ' Escape - Clear search or close panel
                        If Not String.IsNullOrEmpty(pFindEntry.Text) Then
                            pFindEntry.Text = ""
                        Else
                            RaiseEvent CloseRequested()
                        End If
                        vArgs.RetVal = True
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnFindEntryKeyPress error: {ex.Message}")
            End Try
        End Sub      
  
        Private Sub OnReplaceEntryKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        ' Enter in replace field - perform replace and find next
                        If pReplaceButton.Sensitive Then
                            OnReplace(Nothing, Nothing)
                        End If
                        vArgs.RetVal = True
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnReplaceEntryKeyPress error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFindEntryChanged(vSender As Object, vE As EventArgs)
            Try
                ' Update button states when find text changes
                UpdateButtonStates()
                
                ' Clear previous results if text is empty
                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    pSearchResults.Clear()
                    pResultsStore?.Clear()
                    pCurrentMatches = Nothing
                    pCurrentMatchIndex = -1
                    pStatusLabel.Text = "Ready"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnFindEntryChanged error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFindAll(vSender As Object, vE As EventArgs)
            Try
                ExecuteSearch()
            Catch ex As Exception
                Console.WriteLine($"OnFindAll error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFindNext(vSender As Object, vE As EventArgs)
            Try
                If pCurrentMatches Is Nothing OrElse pCurrentMatches.Count = 0 Then
                    ' No results - perform search first
                    ExecuteSearch()
                    Return
                End If
                
                ' Move to next match
                pCurrentMatchIndex += 1
                If pCurrentMatchIndex >= pCurrentMatches.Count Then
                    pCurrentMatchIndex = 0  ' Wrap around
                End If
                
                NavigateToMatch(pCurrentMatchIndex)
                
            Catch ex As Exception
                Console.WriteLine($"OnFindNext error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFindPrevious(vSender As Object, vE As EventArgs)
            Try
                If pCurrentMatches Is Nothing OrElse pCurrentMatches.Count = 0 Then
                    ' No results - perform search first
                    ExecuteSearch()
                    Return
                End If
                
                ' Move to previous match
                pCurrentMatchIndex -= 1
                If pCurrentMatchIndex < 0 Then
                    pCurrentMatchIndex = pCurrentMatches.Count - 1  ' Wrap around
                End If
                
                NavigateToMatch(pCurrentMatchIndex)
                
            Catch ex As Exception
                Console.WriteLine($"OnFindPrevious error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnResultActivated(vSender As Object, vArgs As RowActivatedArgs)
            Try
                Dim lSelection As TreeSelection = pResultsView.Selection
                Dim lModel As ITreeModel = Nothing
                Dim lIter As TreeIter = Nothing
                
                If lSelection.GetSelected(lModel, lIter) Then
                    ' Get result details
                    Dim lFileName As String = CStr(lModel.GetValue(lIter, 0))
                    Dim lLineNumber As Integer = CInt(lModel.GetValue(lIter, 2))
                    Dim lColumnNumber As Integer = CInt(lModel.GetValue(lIter, 3))
                    
                    ' Find the full path from results
                    Dim lResult As FindResult = Nothing
                    For Each lRes In pSearchResults
                        If lRes.LineNumber = lLineNumber AndAlso 
                           lRes.ColumnNumber = lColumnNumber AndAlso
                           lRes.FileName = lFileName Then
                            lResult = lRes
                            Exit For
                        End If
                    Next
                    
                    If lResult IsNot Nothing Then
                        ' Navigate to result
                        RaiseEvent ResultSelected(lResult.FilePath, lResult.LineNumber, lResult.ColumnNumber)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnResultActivated error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles search option changes and re-executes search
        ''' </summary>
        Private Sub OnOptionsChanged(vSender As Object, vE As EventArgs)
            Try
                ' If we have search text, re-execute the search with new options
                If Not String.IsNullOrEmpty(pFindEntry.Text) Then
                    OnFind(Nothing, Nothing)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnOptionsChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles scope radio button changes and re-executes search
        ''' </summary>
        Private Sub OnScopeChanged(vSender As Object, vE As EventArgs)
            Try
                ' Only process if this is the radio button being activated (not deactivated)
                Dim lRadio As RadioButton = TryCast(vSender, RadioButton)
                If lRadio Is Nothing OrElse Not lRadio.Active Then
                    Return
                End If
                
                ' Update status to show new scope
                If pInFileRadio.Active Then
                    pStatusLabel.Text = "Scope: Current file"
                Else
                    pStatusLabel.Text = "Scope: Entire project"
                End If
                
                ' If we have search text, re-execute the search with new scope
                If Not String.IsNullOrEmpty(pFindEntry.Text) Then
                    OnFind(Nothing, Nothing)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnScopeChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Search Implementation (Simplified) =====
        
'        ''' <summary>
'        ''' Performs search in current file using editor's Find method
'        ''' </summary>
'        Private Sub SearchInCurrentFileSimple()
'            Try
'                ' Get current tab
'                Dim lTab As TabInfo = GetCurrentTab()
'                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then
'                    pStatusLabel.Text = "No file open to search"
'                    Return
'                End If
'                
'                ' Store current tab reference
'                pCurrentTab = lTab
'                pCurrentEditor = lTab.Editor
'                pCurrentFilePath = lTab.FilePath
'                
'                ' Find all matches using editor's Find method
'                pCurrentMatches = New List(Of EditorPosition)(
'                    lTab.Editor.Find(pLastSearchOptions.SearchText, 
'                                   pLastSearchOptions.MatchCase, 
'                                   pLastSearchOptions.WholeWord, 
'                                   pLastSearchOptions.UseRegex))
'                
'                If pCurrentMatches.Count = 0 Then
'                    pStatusLabel.Text = "No matches found"
'                    Return
'                End If
'                
'                ' Convert to FindResult objects for display
'                pSearchResults.Clear()
'                For Each lMatch In pCurrentMatches
'                    Dim lLineText As String = lTab.Editor.GetLineText(lMatch.Line)
'                    
'                    Dim lResult As New FindResult() With {
'                        .FilePath = lTab.FilePath,
'                        .LineNumber = lMatch.Line + 1,  ' Convert to 1-based
'                        .ColumnNumber = lMatch.Column + 1,
'                        .LineText = If(lLineText IsNot Nothing, lLineText.Trim(), ""),
'                        .MatchText = pLastSearchOptions.SearchText,
'                        .MatchLength = pLastSearchOptions.SearchText.Length
'                    }
'                    
'                    pSearchResults.Add(lResult)
'                Next
'                
'                ' Populate results in tree view
'                PopulateResults()
'                
'                ' Update status
'                pStatusLabel.Text = $"Found {pCurrentMatches.Count} match(es) in current file"
'                
'                ' Navigate to first result
'                If pCurrentMatches.Count > 0 Then
'                    pCurrentMatchIndex = 0
'                    NavigateToMatch(0)
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"SearchInCurrentFileSimple error: {ex.Message}")
'                pStatusLabel.Text = "Search error: " & ex.Message
'            End Try
'        End Sub
        
        ''' <summary>
        ''' Populates the results tree view from pSearchResults
        ''' </summary>
        Private Sub PopulateResults()
            Try
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
                
            Catch ex As Exception
                Console.WriteLine($"PopulateResults error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Searches in a file by reading from disk
        ''' </summary>
        Private Function SearchInFile(vFilePath As String) As List(Of FindResult)
            Dim lResults As New List(Of FindResult)()
            
            Try
                Dim lSourceFileInfo As SourceFileInfo = pProjectManager.GetSourceFileInfo(vFilePath)
                If lSourceFileInfo is Nothing then return lResults
                
                ' Search each line
                For lLineIndex As Integer = 0 To lSourceFileInfo.TextLines.Count - 1
                    Dim lLine As String = lSourceFileInfo.TextLines(lLineIndex)
                    Dim lMatches As List(Of Integer) = FindMatchesInLine(lLine, pLastSearchOptions)
                    
                    
                    For Each lColumn In lMatches
                        Dim lResult As New FindResult() With {
                            .FilePath = vFilePath,
                            .LineNumber = lLineIndex + 1,
                            .ColumnNumber = lColumn + 1,
                            .LineText = lLine.Trim(),
                            .MatchText = pLastSearchOptions.SearchText,
                            .MatchLength = pLastSearchOptions.SearchText.Length
                        }
                        
                        lResults.Add(lResult)
                    Next
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SearchInFile error: {ex.Message}")
            End Try
            
            Return lResults
        End Function
        
        ''' <summary>
        ''' Refreshes search results by re-executing the last search
        ''' </summary>
        Public Sub RefreshResults()
            Try
                If pLastSearchOptions.SearchText IsNot Nothing Then
                    ExecuteSearch()
                    pStatusLabel.Text = "Results refreshed"
                Else
                    pStatusLabel.Text = "No previous search to refresh"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RefreshResults error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnReplace(vSender As Object, vE As EventArgs)
            ' TODO: Implement replace functionality
            UpdateStatus("Replace functionality not yet implemented")
        End Sub
        
        Private Sub OnReplaceAll(vSender As Object, vE As EventArgs)
            ' TODO: Implement replace all functionality
            UpdateStatus("Replace All functionality not yet implemented")
        End Sub

        Private Sub OnRefresh(vSender As Object, vE As EventArgs)
            Try
                If pSearchResults.Count > 0 Then
                    ' Re-run the last search
                    ExecuteSearchAndNavigate()
                    UpdateStatus("Search results refreshed")
                End If
            Catch ex As Exception
                Console.WriteLine($"Error refreshing results: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateButtonStates()
            Dim lHasText As Boolean = Not String.IsNullOrWhiteSpace(pFindEntry.Text)
            pFindButton.Sensitive = lHasText
            pFindNextButton.Sensitive = lHasText
            pFindPreviousButton.Sensitive = lHasText
            pReplaceButton.Sensitive = lHasText
            pReplaceAllButton.Sensitive = lHasText
            pRefreshButton.Sensitive = (pSearchResults.Count > 0)  ' NEW: Enable refresh when results exist
        End Sub  
        
        Private Sub UpdateStatus(vMessage As String)
            pStatusLabel.Text = vMessage
            Console.WriteLine($"Find/Replace: {vMessage}")
        End Sub

        ' Core Search Logic
        Private Sub ExecuteSearchAndNavigate()
            Try
                If pCurrentTab Is Nothing Then
                    UpdateStatus("No active editor tab")
                    Return
                End If
                
                ' Clear previous results and modified file tracking
                pResultsStore.Clear()
                pSearchResults.Clear()
                pCurrentResultIndex = -1
                pModifiedFiles.Clear()  ' NEW: Clear stale tracking
                
                ' Get search options
                Dim lOptions As SearchOptions = GetCurrentSearchOptions()
                
                If lOptions.Scope = SearchScope.eCurrentFile Then
                    ' Search in current file
                    PerformFileSearch(lOptions)
                ElseIf lOptions.Scope = SearchScope.eProject Then
                    ' Search in project with enhanced tracking
                    PerformProjectSearch(lOptions)
                End If
                
                ' Navigate to first result if found
                If pSearchResults.Count > 0 Then
                    NavigateToResult(0)
                Else
                    UpdateStatus($"'{lOptions.SearchText}' not found")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error executing search: {ex.Message}")
                UpdateStatus($"Search error: {ex.Message}")
            End Try
        End Sub

        ' Perform search in current file
        Private Sub PerformFileSearch(vOptions As SearchOptions)
            Try
                If pCurrentTab Is Nothing Then Return
                pLastSearchOptions = vOptions
                SearchInFile(pCurrentTab.FilePath)
            Catch ex As Exception
                Console.WriteLine($"error performing file search: {ex.Message}")
            End Try
        End Sub
        
        ' Enhanced project search with TextMark support
        Private Sub PerformProjectSearch(vOptions As SearchOptions)
            Try
                If String.IsNullOrEmpty(pProjectRoot) Then
                    UpdateStatus("No project root Set")
                    Return
                End If
                
                If Not Directory.Exists(pProjectRoot) Then
                    UpdateStatus("Project root directory does Not exist")
                    Return
                End If
                
                UpdateStatus("Searching entire project...")
                
                ' Clear modified files tracking
                pModifiedFiles.Clear()
                
                ' Get all VB files in project
                Dim lVBFiles() As String = Directory.GetFiles(pProjectRoot, "*.vb", SearchOption.AllDirectories)
                
                If lVBFiles.Length = 0 Then
                    UpdateStatus("No VB files found in project")
                    Return
                End If
                
                ' Clear previous results
                pSearchResults.Clear()
                pResultsStore.Clear()
                
                Dim lTotalMatches As Integer = 0
                Dim lFilesSearched As Integer = 0
                Dim lFilesWithMatches As Integer = 0
                
                ' Search each file
                For Each lFilePath In lVBFiles
                    Try
                        ' Skip binary or backup files
                        If lFilePath.EndsWith(".designer.vb", StringComparison.OrdinalIgnoreCase) OrElse
                           lFilePath.Contains("\bin\") OrElse lFilePath.Contains("\obj\") OrElse
                           lFilePath.Contains("\.vs\") Then
                            Continue For
                        End If
                        
                        lFilesSearched += 1
                        Dim lFileMatches As List(Of FindResult) = SearchInFile(lFilePath)
                        
                        If lFileMatches.Count > 0 Then
                            lTotalMatches += lFileMatches.Count
                            lFilesWithMatches += 1
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"error searching file {lFilePath}: {ex.Message}")
                    End Try
                Next
                
                ' Update results in UI
                For Each lResult In pSearchResults
                    Dim lCurrentPos = lResult.LineNumber()
                    Dim lDisplayText As String = lResult.LineText.Trim()
                    
                    ' Add live tracking indicator for open files
                    'If lResult.IsLiveTracked() Then
                      '  lDisplayText = "📍 " & lDisplayText  ' Live tracking indicator
                    'End If
                    
                    pResultsStore.AppendValues(
                        lResult.LineNumber.ToString(),
                        lResult.ColumnNumber.ToString(),
                        lDisplayText,
                        System.IO.Path.GetFileName(lResult.FilePath)
                    )
                Next
                
                ' Update status with results
                If lTotalMatches > 0 Then
                    'Dim lLiveTrackedResults = pSearchResults.Where(Function(r) r.IsLiveTracked()).ToList()
                    'Dim lLiveCount As Integer = lLiveTrackedResults.Count
                    'UpdateStatus($"Found {lTotalMatches} occurrences in {lFilesWithMatches} Of {lFilesSearched} files ({lLiveCount} live-tracked)")
                Else
                    UpdateStatus($"'{vOptions.SearchText}' not found in {lFilesSearched} files")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Error performing project search: {ex.Message}")
                UpdateStatus($"Project search error: {ex.Message}")
            End Try
        End Sub

        ' Enhanced navigation that handles live tracking
        Private Sub NavigateToResult(vIndex As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pSearchResults.Count Then Return
                
                pCurrentResultIndex = vIndex
                Dim lResult As FindResult = pSearchResults(vIndex)
                
                ' Get current position (live if available)
                Dim lCurrentPos = lResult.LineNumber()
                
                ' Warn if result is stale
                'If lResult.IsStale Then
                   ' UpdateStatus($"⚠ Result may be outdated - Line {lCurrentPos.Line}, Column {lCurrentPos.Column}")
                'Else
                   '' Dim lTrackingStatus = If(lResult.IsLiveTracked(), "📍", "")
                   ' UpdateStatus($"{lTrackingStatus}Result {vIndex + 1} Of {pSearchResults.Count} - Line {lCurrentPos.Line}, Column {lCurrentPos.Column}")
                'End If
                
                ' Raise event to navigate to result
                RaiseEvent ResultSelected(lResult.FilePath, lResult.LineNumber, lResult.ColumnNumber)
                
            Catch ex As Exception
                Console.WriteLine($"error navigating To result: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles the Activated event (Enter key) for the replace entry
        ''' </summary>
        Private Sub OnReplaceEntryActivated(vSender As Object, vArgs As EventArgs)
            Try
                Console.WriteLine("OnReplaceEntryActivated: Enter pressed in Replace field!")
                
                ' Perform replace if button is enabled
                If pReplaceButton.Sensitive Then
                    OnReplace(Nothing, Nothing)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnReplaceEntryActivated error: {ex.Message}")
            End Try
        End Sub
        
    End Class

End Namespace
