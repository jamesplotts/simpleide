' Editors/CustomDrawingEditor.Search.vb - Find and Replace implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' Search state
        Private pLastSearchText As String = ""
        Private pLastSearchResults As List(Of EditorPosition)
        
        ' ===== Find Implementation =====
        
        Public Function Find(vSearchText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean) As IEnumerable(Of EditorPosition) Implements IEditor.Find
            Try
                Dim lResults As New List(Of EditorPosition)()
                
                If String.IsNullOrEmpty(vSearchText) Then
                    Return lResults
                End If
                
                ' Store for FindNext/FindPrevious
                pLastSearchText = vSearchText
                pLastSearchResults = lResults
                pCurrentSearchIndex = -1
                
                If vRegex Then
                    ' Regex search
                    FindWithRegex(vSearchText, vCaseSensitive, lResults)
                Else
                    ' Plain text search
                    FindPlainText(vSearchText, vCaseSensitive, vWholeWord, lResults)
                End If
                
                Return lResults
                
            Catch ex As Exception
                Console.WriteLine($"Find error: {ex.Message}")
                Return New List(Of EditorPosition)()
            End Try
        End Function
        
        Private Sub FindPlainText(vSearchText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vResults As List(Of EditorPosition))
            Try
                Dim lComparison As StringComparison = If(vCaseSensitive, 
                    StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)
                
                For lLineIndex As Integer = 0 To pLineCount - 1
                    Dim lLine As String = pTextLines(lLineIndex)
                    Dim lIndex As Integer = 0
                    
                    While lIndex >= 0
                        lIndex = lLine.IndexOf(vSearchText, lIndex, lComparison)
                        
                        If lIndex >= 0 Then
                            ' Check whole word if required
                            If Not vWholeWord OrElse IsWholeWordMatch(lLine, lIndex, vSearchText) Then
                                vResults.Add(New EditorPosition(lLineIndex, lIndex))
                            End If
                            
                            lIndex += 1  ' Move past this match
                        End If
                    End While
                Next
                
            Catch ex As Exception
                Console.WriteLine($"FindPlainText error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub FindWithRegex(vPattern As String, vCaseSensitive As Boolean, vResults As List(Of EditorPosition))
            Try
                Dim lOptions As RegexOptions = If(vCaseSensitive, RegexOptions.None, RegexOptions.IgnoreCase)
                Dim lRegex As New Regex(vPattern, lOptions)
                
                For lLineIndex As Integer = 0 To pLineCount - 1
                    Dim lLine As String = pTextLines(lLineIndex)
                    
                    For Each lMatch As Match In lRegex.Matches(lLine)
                        vResults.Add(New EditorPosition(lLineIndex, lMatch.Index))
                    Next
                Next
                
            Catch ex As RegexMatchTimeoutException
                Console.WriteLine($"Regex timeout: {ex.Message}")
            Catch ex As ArgumentException
                Console.WriteLine($"Invalid regex Pattern: {ex.Message}")
            Catch ex As Exception
                Console.WriteLine($"FindWithRegex error: {ex.Message}")
            End Try
        End Sub
        
        Private Function IsWholeWordMatch(vLine As String, vIndex As Integer, vSearchText As String) As Boolean
            Try
                ' Check character before match
                If vIndex > 0 Then
                    Dim lCharBefore As Char = vLine(vIndex - 1)
                    If Char.IsLetterOrDigit(lCharBefore) OrElse lCharBefore = "_"c Then
                        Return False
                    End If
                End If
                
                ' Check character after match
                Dim lEndIndex As Integer = vIndex + vSearchText.Length
                If lEndIndex < vLine.Length Then
                    Dim lCharAfter As Char = vLine(lEndIndex)
                    If Char.IsLetterOrDigit(lCharAfter) OrElse lCharAfter = "_"c Then
                        Return False
                    End If
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsWholeWordMatch error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== FindNext/FindPrevious Implementation =====
        
        Public Sub FindNext() Implements IEditor.FindNext
            Try
                If pLastSearchResults Is Nothing OrElse pLastSearchResults.Count = 0 Then
                    Return
                End If
                
                ' Get current cursor position
                Dim lCursorPos As New EditorPosition(pCursorLine, pCursorColumn)
                
                ' Find next match after cursor
                Dim lNextIndex As Integer = -1
                For i As Integer = 0 To pLastSearchResults.Count - 1
                    Dim lMatch As EditorPosition = pLastSearchResults(i)
                    If ComparePositions(lMatch, lCursorPos) > 0 Then
                        lNextIndex = i
                        Exit For
                    End If
                Next
                
                ' Wrap around to beginning if no match found
                If lNextIndex = -1 Then
                    lNextIndex = 0
                End If
                
                ' Navigate to match
                If lNextIndex >= 0 AndAlso lNextIndex < pLastSearchResults.Count Then
                    pCurrentSearchIndex = lNextIndex
                    Dim lMatch As EditorPosition = pLastSearchResults(lNextIndex)
                    
                    ' Move cursor and select match
                    GoToPosition(lMatch.Line, lMatch.Column)
                    
                    ' Select the match
                    Dim lEndColumn As Integer = lMatch.Column + pLastSearchText.Length
                    SetSelection(lMatch.Line, lMatch.Column, lMatch.Line, lEndColumn)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"FindNext error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub FindPrevious() Implements IEditor.FindPrevious
            Try
                If pLastSearchResults Is Nothing OrElse pLastSearchResults.Count = 0 Then
                    Return
                End If
                
                ' Get current cursor position
                Dim lCursorPos As New EditorPosition(pCursorLine, pCursorColumn)
                
                ' Find previous match before cursor
                Dim lPrevIndex As Integer = -1
                For i As Integer = pLastSearchResults.Count - 1 To 0 Step -1
                    Dim lMatch As EditorPosition = pLastSearchResults(i)
                    If ComparePositions(lMatch, lCursorPos) < 0 Then
                        lPrevIndex = i
                        Exit For
                    End If
                Next
                
                ' Wrap around to end if no match found
                If lPrevIndex = -1 Then
                    lPrevIndex = pLastSearchResults.Count - 1
                End If
                
                ' Navigate to match
                If lPrevIndex >= 0 AndAlso lPrevIndex < pLastSearchResults.Count Then
                    pCurrentSearchIndex = lPrevIndex
                    Dim lMatch As EditorPosition = pLastSearchResults(lPrevIndex)
                    
                    ' Move cursor and select match
                    GoToPosition(lMatch.Line, lMatch.Column)
                    
                    ' Select the match
                    Dim lEndColumn As Integer = lMatch.Column + pLastSearchText.Length
                    SetSelection(lMatch.Line, lMatch.Column, lMatch.Line, lEndColumn)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"FindPrevious error: {ex.Message}")
            End Try
        End Sub
        
        Private Function ComparePositions(vPos1 As EditorPosition, vPos2 As EditorPosition) As Integer
            If vPos1.Line < vPos2.Line Then Return -1
            If vPos1.Line > vPos2.Line Then Return 1
            Return vPos1.Column.CompareTo(vPos2.Column)
        End Function
        
        ' ===== Replace Implementation =====
        
        Public Sub Replace(vSearchText As String, vReplaceText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean) Implements IEditor.Replace
            Try
                ' Check if we have a selection that matches the search text
                If Not pHasSelection Then
                    ' Find next occurrence
                    Dim lMatchesList As List(Of EditorPosition) = New List(Of EditorPosition)(
                        Find(vSearchText, vCaseSensitive, vWholeWord, vRegex))
                    
                    If lMatchesList.Count > 0 Then
                        FindNext()
                    End If
                    Return
                End If
                
                ' Get selected text
                Dim lSelectedText As String = GetSelectedText()
                Dim lMatchesSelection As Boolean = False
                
                ' Check if selection matches search criteria
                If vRegex Then
                    Try
                        Dim lRegex As New Regex(vSearchText, If(vCaseSensitive, RegexOptions.None, RegexOptions.IgnoreCase))
                        lMatchesSelection = lRegex.IsMatch(lSelectedText) AndAlso lRegex.Match(lSelectedText).Value = lSelectedText
                    Catch
                        Return
                    End Try
                ElseIf vWholeWord Then
                    ' For whole word, the selection should be exactly the search text
                    If vCaseSensitive Then
                        lMatchesSelection = lSelectedText = vSearchText
                    Else
                        lMatchesSelection = String.Equals(lSelectedText, vSearchText, StringComparison.OrdinalIgnoreCase)
                    End If
                Else
                    ' Plain text match
                    If vCaseSensitive Then
                        lMatchesSelection = lSelectedText = vSearchText
                    Else
                        lMatchesSelection = String.Equals(lSelectedText, vSearchText, StringComparison.OrdinalIgnoreCase)
                    End If
                End If
                
                If lMatchesSelection Then
                    ' Replace the selection
                    ReplaceSelection(vReplaceText)
                    
                    ' Clear search results as text has changed
                    pLastSearchResults = Nothing
                    pCurrentSearchIndex = -1
                End If
                
                ' Find next occurrence
                FindNext()
                
            Catch ex As Exception
                Console.WriteLine($"Replace error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub ReplaceAll(vSearchText As String, vReplaceText As String, vCaseSensitive As Boolean, vWholeWord As Boolean, vRegex As Boolean) Implements IEditor.ReplaceAll
            Try
                ' Find all matches
                Dim lMatches As List(Of EditorPosition) = New List(Of EditorPosition)(
                    Find(vSearchText, vCaseSensitive, vWholeWord, vRegex))
                
                If lMatches.Count = 0 Then
                    Return
                End If
                
                ' Begin bulk update
                BeginUpdate()
                
                Try
                    ' Begin undo group for replace all operation
                    If pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.BeginUserAction()
                    End If
                    
                    ' Replace from end to beginning to maintain positions
                    For i As Integer = lMatches.Count - 1 To 0 Step -1
                        Dim lMatch As EditorPosition = lMatches(i)
                        
                        If vRegex Then
                            ' For regex, we need to get the actual match length
                            Try
                                Dim lRegex As New Regex(vSearchText, If(vCaseSensitive, RegexOptions.None, RegexOptions.IgnoreCase))
                                Dim lLine As String = pTextLines(lMatch.Line)
                                Dim lRegexMatch As Match = lRegex.Match(lLine, lMatch.Column)
                                
                                If lRegexMatch.Success Then
                                    ' Calculate replacement text (may include captures)
                                    Dim lReplacementText As String = lRegexMatch.Result(vReplaceText)
                                    
                                    ' Replace the match
                                    ReplaceText(lMatch.Line, lMatch.Column, 
                                              lMatch.Line, lMatch.Column + lRegexMatch.Length, 
                                              lReplacementText)
                                End If
                            Catch
                                ' Skip this match if regex fails
                            End Try
                        Else
                            ' Plain text replacement
                            ReplaceText(lMatch.Line, lMatch.Column, 
                                      lMatch.Line, lMatch.Column + vSearchText.Length, 
                                      vReplaceText)
                        End If
                    Next
                    
                    ' End undo group
                    If pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.EndUserAction()
                    End If
                    
                    ' Clear search results
                    pLastSearchResults = Nothing
                    pCurrentSearchIndex = -1
                    
                    ' Mark as modified
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                    
                Finally
                    EndUpdate()
                End Try
                
                ' Redraw
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceAll error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
