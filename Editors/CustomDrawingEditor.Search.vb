' Editors/CustomDrawingEditor.Search.vb - Find and Replace implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' Search state
        Private pLastSearchText As String = ""
        Private pLastSearchResults As List(Of EditorPosition)

        Public Sub OnContextMenuFind(o As Object, e As EventArgs)
            ' TODO: Implement CustomDrawingEditor.OnContextMenuFind in CustomDrawingEditor.Search.vb
        End Sub
        
        Public Sub OnContextMenuReplace(o As Object, e As EventArgs)
            ' TODO: Implement CustomDrawingEditor.OnContextMenuReplace in CustomDrawingEditor.Search.vb
        End Sub

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
                
                for lLineIndex As Integer = 0 To pLineCount - 1
                    Dim lLine As String = TextLines(lLineIndex)
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
                
                for lLineIndex As Integer = 0 To pLineCount - 1
                    Dim lLine As String = TextLines(lLineIndex)
                    
                    for each lMatch As Match in lRegex.Matches(lLine)
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
                for i As Integer = 0 To pLastSearchResults.Count - 1
                    Dim lMatch As EditorPosition = pLastSearchResults(i)
                    If ComparePositions(lMatch, lCursorPos) > 0 Then
                        lNextIndex = i
                        Exit for
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
                    GoToPosition(New EditorPosition(lMatch.Line, lMatch.Column))
                    
                    ' Select the match
                    Dim lEndColumn As Integer = lMatch.Column + pLastSearchText.Length
                    SetSelection(New EditorPosition(lMatch.Line, lMatch.Column), New EditorPosition(lMatch.Line, lEndColumn))
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
                for i As Integer = pLastSearchResults.Count - 1 To 0 Step -1
                    Dim lMatch As EditorPosition = pLastSearchResults(i)
                    If ComparePositions(lMatch, lCursorPos) < 0 Then
                        lPrevIndex = i
                        Exit for
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
                    GoToPosition(New EditorPosition(lMatch.Line, lMatch.Column))
                    
                    ' Select the match
                    Dim lEndColumn As Integer = lMatch.Column + pLastSearchText.Length
                    SetSelection(New EditorPosition(lMatch.Line, lMatch.Column), New EditorPosition(lMatch.Line, lEndColumn))
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
                    for i As Integer = lMatches.Count - 1 To 0 Step -1
                        Dim lMatch As EditorPosition = lMatches(i)
                        
                        If vRegex Then
                            ' For regex, we need to get the actual match length
                            Try
                                Dim lRegex As New Regex(vSearchText, If(vCaseSensitive, RegexOptions.None, RegexOptions.IgnoreCase))
                                Dim lLine As String = TextLines(lMatch.Line)
                                Dim lRegexMatch As Match = lRegex.Match(lLine, lMatch.Column)
                                
                                If lRegexMatch.Success Then
                                    ' Calculate replacement text (may include captures)
                                    Dim lReplacementText As String = lRegexMatch.Result(vReplaceText)
                                    
                                    ' Replace the match
                                    ReplaceText(New EditorPosition(lMatch.Line, lMatch.Column), 
                                              New EditorPosition(lMatch.Line, lMatch.Column + lRegexMatch.Length), 
                                              lReplacementText)
                                End If
                            Catch
                                ' Skip this match if regex fails
                            End Try
                        Else
                            ' Plain text replacement
                            ReplaceText(New EditorPosition(lMatch.Line, lMatch.Column), 
                                      New EditorPosition(lMatch.Line, lMatch.Column + vSearchText.Length), 
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


        ''' <summary>
        ''' Finds all occurrences of the specified text in the document
        ''' </summary>
        ''' <param name="vFindText">Text to search for</param>
        ''' <returns>List of all positions where the text was found</returns>
        ''' <remarks>
        ''' This is a simple case-sensitive, non-regex search.
        ''' For more options, use the Find method with additional parameters.
        ''' </remarks>
        Public Function FindAll(vFindText As String) As List(Of EditorPosition) Implements IEditor.FindAll
            Try
                ' Use the full Find method with default options (case-sensitive, no whole word, no regex)
                Return New List(Of EditorPosition)(Find(vFindText, True, False, False))
                
            Catch ex As Exception
                Console.WriteLine($"FindAll error: {ex.Message}")
                Return New List(Of EditorPosition)()
            End Try
        End Function
        
        ''' <summary>
        ''' Finds all occurrences with simple options
        ''' </summary>
        ''' <param name="vFindText">Text to search for</param>
        ''' <param name="vCaseSensitive">Whether search is case-sensitive</param>
        ''' <returns>List of all positions where the text was found</returns>
        Public Function FindAll(vFindText As String, vCaseSensitive As Boolean) As List(Of EditorPosition)
            Try
                Return New List(Of EditorPosition)(Find(vFindText, vCaseSensitive, False, False))
                
            Catch ex As Exception
                Console.WriteLine($"FindAll error: {ex.Message}")
                Return New List(Of EditorPosition)()
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced FindAll implementation with full control
        ''' </summary>
        ''' <param name="vFindText">Text to search for</param>
        ''' <param name="vCaseSensitive">Whether search is case-sensitive</param>
        ''' <param name="vWholeWord">Whether to match whole words only</param>
        ''' <param name="vRegex">Whether to use regular expressions</param>
        ''' <param name="vSearchInSelection">Whether to search only in selected text</param>
        ''' <returns>List of all positions where the text was found</returns>
        Public Function FindAllExtended(vFindText As String, 
                                        vCaseSensitive As Boolean,
                                        vWholeWord As Boolean,
                                        vRegex As Boolean,
                                        Optional vSearchInSelection As Boolean = False) As List(Of EditorPosition)
            Try
                Dim lResults As New List(Of EditorPosition)()
                
                ' Validate input
                If String.IsNullOrEmpty(vFindText) Then
                    Return lResults
                End If
                
                ' Determine search range
                Dim lStartLine As Integer = 0
                Dim lStartColumn As Integer = 0
                Dim lEndLine As Integer = pLineCount - 1
                Dim lEndColumn As Integer = If(pLineCount > 0, TextLines(pLineCount - 1).Length, 0)
                
                If vSearchInSelection AndAlso pHasSelection Then
                    ' Search only in selection
                    Dim lSelStart As EditorPosition = SelectionStart
                    Dim lSelEnd As EditorPosition = SelectionEnd
                    NormalizeSelection(lSelStart, lSelEnd)
                    
                    lStartLine = lSelStart.Line
                    lStartColumn = lSelStart.Column
                    lEndLine = lSelEnd.Line
                    lEndColumn = lSelEnd.Column
                End If
                
                ' Perform search based on type
                If vRegex Then
                    FindAllWithRegex(vFindText, vCaseSensitive, lStartLine, lStartColumn, 
                                   lEndLine, lEndColumn, lResults)
                Else
                    FindAllPlainText(vFindText, vCaseSensitive, vWholeWord, lStartLine, 
                                   lStartColumn, lEndLine, lEndColumn, lResults)
                End If
                
                Return lResults
                
            Catch ex As Exception
                Console.WriteLine($"FindAllExtended error: {ex.Message}")
                Return New List(Of EditorPosition)()
            End Try
        End Function
        
        ''' <summary>
        ''' Helper method for plain text search
        ''' </summary>
        Private Sub FindAllPlainText(vSearchText As String, vCaseSensitive As Boolean, vWholeWord As Boolean,
                                     vStartLine As Integer, vStartColumn As Integer,
                                     vEndLine As Integer, vEndColumn As Integer,
                                     vResults As List(Of EditorPosition))
            Try
                Dim lComparison As StringComparison = If(vCaseSensitive, 
                    StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)
                
                for lLineIndex As Integer = vStartLine To Math.Min(vEndLine, pLineCount - 1)
                    If lLineIndex >= pLineCount Then Exit for
                    
                    Dim lLine As String = TextLines(lLineIndex)
                    Dim lSearchStart As Integer = If(lLineIndex = vStartLine, vStartColumn, 0)
                    Dim lSearchEnd As Integer = If(lLineIndex = vEndLine, Math.Min(vEndColumn, lLine.Length), lLine.Length)
                    
                    If lSearchStart >= lSearchEnd Then Continue for
                    
                    Dim lIndex As Integer = lSearchStart
                    
                    While lIndex >= 0 AndAlso lIndex <= lSearchEnd - vSearchText.Length
                        ' Find next occurrence
                        lIndex = lLine.IndexOf(vSearchText, lIndex, lSearchEnd - lIndex, lComparison)
                        
                        If lIndex >= 0 Then
                            ' Check whole word if required
                            If Not vWholeWord OrElse IsWholeWordMatch(lLine, lIndex, vSearchText) Then
                                vResults.Add(New EditorPosition(lLineIndex, lIndex))
                            End If
                            
                            lIndex += 1  ' Move past this match to find next
                        End If
                    End While
                Next
                
            Catch ex As Exception
                Console.WriteLine($"FindAllPlainText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method for regex search
        ''' </summary>
        Private Sub FindAllWithRegex(vPattern As String, vCaseSensitive As Boolean,
                                     vStartLine As Integer, vStartColumn As Integer,
                                     vEndLine As Integer, vEndColumn As Integer,
                                     vResults As List(Of EditorPosition))
            Try
                Dim lOptions As RegexOptions = If(vCaseSensitive, RegexOptions.None, RegexOptions.IgnoreCase)
                Dim lRegex As Regex
                
                Try
                    lRegex = New Regex(vPattern, lOptions)
                Catch ex As ArgumentException
                    Console.WriteLine($"Invalid regex pattern: {vPattern}")
                    Return
                End Try
                
                for lLineIndex As Integer = vStartLine To Math.Min(vEndLine, pLineCount - 1)
                    If lLineIndex >= pLineCount Then Exit for
                    
                    Dim lLine As String = TextLines(lLineIndex)
                    Dim lSearchStart As Integer = If(lLineIndex = vStartLine, vStartColumn, 0)
                    Dim lSearchEnd As Integer = If(lLineIndex = vEndLine, Math.Min(vEndColumn, lLine.Length), lLine.Length)
                    
                    If lSearchStart >= lSearchEnd Then Continue for
                    
                    ' Get substring to search if not searching entire line
                    Dim lSearchText As String = lLine
                    If lSearchStart > 0 OrElse lSearchEnd < lLine.Length Then
                        lSearchText = lLine.Substring(lSearchStart, lSearchEnd - lSearchStart)
                    End If
                    
                    ' Find all matches in the line
                    for each lMatch As Match in lRegex.Matches(lSearchText)
                        ' Adjust column position if we're searching a substring
                        Dim lColumn As Integer = lMatch.Index + lSearchStart
                        vResults.Add(New EditorPosition(lLineIndex, lColumn))
                    Next
                Next
                
            Catch ex As RegexMatchTimeoutException
                Console.WriteLine($"Regex timeout: {ex.Message}")
            Catch ex As Exception
                Console.WriteLine($"FindAllWithRegex error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Finds all occurrences and highlights them
        ''' </summary>
        Public Function FindAllAndHighlight(vFindText As String, vCaseSensitive As Boolean) As Integer
            Try
                ' Find all occurrences
                Dim lMatches As List(Of EditorPosition) = FindAll(vFindText, vCaseSensitive)
                
                ' Store for navigation
                pLastSearchResults = lMatches
                pCurrentSearchIndex = -1
                pLastSearchText = vFindText
                
                ' Highlight all matches (if you have highlighting support)
                HighlightSearchResults(lMatches, vFindText.Length)
                
                ' Return count of matches
                Return lMatches.Count
                
            Catch ex As Exception
                Console.WriteLine($"FindAllAndHighlight error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Highlights search results visually
        ''' </summary>
        Private Sub HighlightSearchResults(vMatches As List(Of EditorPosition), vMatchLength As Integer)
            Try
                ' Clear previous highlights
                ' TODO: ClearSearchHighlights()
                
                ' Apply highlight color to each match
                for each lMatch in vMatches
                    If lMatch.Line < pLineCount Then
                        ' TODO: Search Highlighting: Mark the line for special rendering
                        ' You would need to implement a way to store and render these highlights
                        ' For example, add to a list of highlighted regions
                        ' AddSearchHighlight(lMatch.Line, lMatch.Column, vMatchLength)
                    End If
                Next
                
                ' Queue redraw to show highlights
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HighlightSearchResults error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Example usage for various scenarios
        ''' </summary>
        Private Sub ExampleUsage()
            ' Simple find all
            Dim lMatches As List(Of EditorPosition) = FindAll("TODO")
            
            ' Case-insensitive search
            Dim lCaseInsensitive As List(Of EditorPosition) = FindAll("error", False)
            
            ' Find whole words only
            Dim lWholeWords As List(Of EditorPosition) = FindAllExtended("End", True, True, False)
            
            ' Regex search for VB.NET method declarations
            Dim lMethods As List(Of EditorPosition) = FindAllExtended(
                "^\s*(Public|Private|Protected|Friend)\s+(Sub|Function)\s+\w+", 
                True, False, True)
            
            ' Search in selection only
            Dim lInSelection As List(Of EditorPosition) = FindAllExtended(
                "Dim", True, True, False, True)
            
            ' Process all matches
            for each lPosition in lMatches
                Console.WriteLine($"Found at Line {lPosition.Line + 1}, Column {lPosition.Column + 1}")
            Next
        End Sub
        
    End Class
    
End Namespace
