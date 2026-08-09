' FindReplacePanel.Events.vb
' Simplified version without live tracking - user must click Find to refresh results

Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

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

        ''' <summary>
        ''' Key press handler for FindReplacePanel widgets
        ''' </summary>
        Private Sub OnFindPanelKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                If vArgs.Event.Key = Gdk.Key.Escape Then
                    ' Try to handle internally first
                    If HandleEscapeKey() Then
                        vArgs.RetVal = True
                        Return
                    End If
                    
                    ' Not handled internally - request close
                    RaiseEvent CloseRequested()
                    vArgs.RetVal = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnFindPanelKeyPress error: {ex.Message}")
            End Try
        End Sub    
  
        Private Sub OnFindEntryChanged(vSender As Object, vE As EventArgs)
            Try
                ' Update button states when find text changes
                UpdateButtonStates()
                
                ' Clear previous results if text is empty
                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    pSearchResults.Clear()
                    pResultsGrid?.ClearRows()
                    pCurrentMatches = Nothing
                    pCurrentMatchIndex = -1
                    pStatusLabel.Text = "Ready"
                    GetCurrentTab()?.Editor?.ClearSearchHighlights()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnFindEntryChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Performs a fresh search, then advances to the next match once results are ready.
        ''' Runs the search's completion via a callback rather than assuming ExecuteSearch is
        ''' synchronous, since project-scope search completes on a background task.
        ''' </summary>
        Private Sub OnFindNext(vSender As Object, vE As EventArgs)
            Try
                ExecuteSearch(Sub()
                    If pSearchResults.Count = 0 Then
                        UpdateStatus("No matches found")
                        Return
                    End If

                    ' Move to next match
                    If pCurrentMatchIndex < 0 OrElse pCurrentMatchIndex >= pSearchResults.Count Then
                        pCurrentMatchIndex = 0
                    Else
                        pCurrentMatchIndex = (pCurrentMatchIndex + 1) Mod pSearchResults.Count
                    End If

                    NavigateToSearchResult(pCurrentMatchIndex)
                    UpdateStatus($"Match {pCurrentMatchIndex + 1} of {pSearchResults.Count}")
                End Sub)

            Catch ex As Exception
                Console.WriteLine($"OnFindNext error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Performs a fresh search, then advances to the previous match once results are
        ''' ready. Runs the search's completion via a callback rather than assuming
        ''' ExecuteSearch is synchronous, since project-scope search completes on a
        ''' background task.
        ''' </summary>
        Private Sub OnFindPrevious(vSender As Object, vE As EventArgs)
            Try
                ExecuteSearch(Sub()
                    If pSearchResults.Count = 0 Then
                        UpdateStatus("No matches found")
                        Return
                    End If

                    ' Move to previous match
                    If pCurrentMatchIndex < 0 OrElse pCurrentMatchIndex >= pSearchResults.Count Then
                        pCurrentMatchIndex = pSearchResults.Count - 1
                    Else
                        pCurrentMatchIndex -= 1
                        If pCurrentMatchIndex < 0 Then
                            pCurrentMatchIndex = pSearchResults.Count - 1  ' Wrap around
                        End If
                    End If

                    NavigateToSearchResult(pCurrentMatchIndex)
                    UpdateStatus($"Match {pCurrentMatchIndex + 1} of {pSearchResults.Count}")
                End Sub)

            Catch ex As Exception
                Console.WriteLine($"OnFindPrevious error: {ex.Message}")
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
        ''' Keeps pInFileRadio/pInProjectRadio mutually exclusive - CustomDrawCheckBox has no
        ''' built-in radio-group concept, unlike the Gtk.RadioButton this replaced - then
        ''' re-executes the search with the new scope
        ''' </summary>
        Private Sub OnScopeToggled(vSender As Object, vE As EventArgs)
            Try
                If pUpdatingScopeToggle Then Return
                pUpdatingScopeToggle = True
                Try
                    If vSender Is pInFileRadio Then
                        If pInFileRadio.Active Then
                            pInProjectRadio.Active = False
                        Else
                            ' Don't allow turning both off - always keep exactly one active
                            pInFileRadio.Active = True
                        End If
                    ElseIf vSender Is pInProjectRadio Then
                        If pInProjectRadio.Active Then
                            pInFileRadio.Active = False
                        Else
                            pInProjectRadio.Active = True
                        End If
                    End If
                Finally
                    pUpdatingScopeToggle = False
                End Try

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
                Console.WriteLine($"OnScopeToggled error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Search Implementation (Simplified) =====
        
        ''' <summary>
        ''' Replaces the current/next match in the active file's editor tab. Delegates to
        ''' IEditor.Replace, which replaces the current selection if it matches the search
        ''' text, then advances to the next match - the standard single-button Replace UX.
        ''' </summary>
        Private Sub OnReplace(vSender As Object, vE As EventArgs)
            Try
                Dim lTab As TabInfo = GetCurrentTab()
                If lTab Is Nothing OrElse lTab.Editor Is Nothing Then
                    UpdateStatus("No file open to replace in")
                    Return
                End If

                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    UpdateStatus("Please enter search text")
                    Return
                End If

                Dim lReplaced As Boolean = lTab.Editor.Replace(pFindEntry.Text, pReplaceEntry.Text,
                                    pCaseSensitiveCheck.Active, pWholeWordCheck.Active, pRegexCheck.Active)
                If lReplaced Then
                    UpdateStatus("Replaced")
                Else
                    UpdateStatus("No match at cursor - moved to next occurrence")
                End If

            Catch ex As Exception
                Console.WriteLine($"OnReplace error: {ex.Message}")
                UpdateStatus("Replace error: " & ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Replaces every match, either in the current file or across the whole project
        ''' depending on the current scope selection
        ''' </summary>
        Private Sub OnReplaceAll(vSender As Object, vE As EventArgs)
            Try
                If String.IsNullOrEmpty(pFindEntry.Text) Then
                    UpdateStatus("Please enter search text")
                    Return
                End If

                If pInFileRadio.Active Then
                    ReplaceAllInCurrentFile()
                Else
                    ReplaceAllInProject()
                End If

            Catch ex As Exception
                Console.WriteLine($"OnReplaceAll error: {ex.Message}")
                UpdateStatus("Replace All error: " & ex.Message)
            End Try
        End Sub

        Private Sub UpdateButtonStates()
            Dim lHasText As Boolean = Not String.IsNullOrWhiteSpace(pFindEntry.Text)
            pFindButton.Sensitive = lHasText
            pFindNextButton.Sensitive = lHasText
            pFindPreviousButton.Sensitive = lHasText
            pReplaceButton.Sensitive = lHasText
            pReplaceAllButton.Sensitive = lHasText
        End Sub  
        
        Private Sub UpdateStatus(vMessage As String)
            pStatusLabel.Text = vMessage
            Console.WriteLine($"Find/Replace: {vMessage}")
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

        ''' <summary>
        ''' Handles single-click/keyboard selection in the results grid
        ''' </summary>
        ''' <remarks>
        ''' The old TreeView-based version had to re-run a fresh in-file search on every
        ''' single click just to re-locate the "current" position, matching by filename +
        ''' match text + closest line distance (an approximation that could pick the wrong
        ''' result if the project had two files with the same name, or duplicate match text
        ''' in the same file). pResultsGrid's rows carry the exact originating FindResult
        ''' directly (DataGridRow.Tag), so navigation is now a direct lookup - no re-search,
        ''' no approximation.
        ''' </remarks>
        Private Sub OnResultsSelectionChanged(vRowIndex As Integer, vColumnIndex As Integer, vRow As DataGridRow)
            Try
                Dim lResult As FindResult = TryCast(vRow?.Tag, FindResult)
                If lResult Is Nothing Then Return

                RaiseEvent ResultSelected(lResult.FilePath, lResult.LineNumber, lResult.ColumnNumber)
                UpdateStatus($"Navigated to Line {lResult.LineNumber}, Column {lResult.ColumnNumber}")

            Catch ex As Exception
                Console.WriteLine($"OnResultsSelectionChanged error: {ex.Message}")
            End Try
        End Sub
        
        
    End Class

End Namespace
