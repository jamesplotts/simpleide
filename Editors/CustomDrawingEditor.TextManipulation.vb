' Editors/CustomDrawingEditor.TextManipulation.vb - Missing IEditor method implementations
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        



        ' ===== Updated Text Property to sync with SourceFileInfo =====

        Public Property Text As String Implements IEditor.Text
            Get
                Return String.Join(Environment.NewLine, pSourceFileInfo.TextLines)
            End Get
            Set(value As String)
                ReplaceAllText(value)
            End Set
        End Property
        
        ' SelectedText property - Gets the currently selected text
        Public ReadOnly Property SelectedText As String Implements IEditor.SelectedText
            Get
                Try
                    Return GetSelectedText()
                Catch ex As Exception
                    Console.WriteLine($"SelectedText error: {ex.Message}")
                    Return ""
                End Try
            End Get
        End Property

        ''' <summary>
        ''' Deletes text in the specified range using atomic operation
        ''' </summary>
        ''' <param name="vStartPosition">Start position of range</param>
        ''' <param name="vEndPosition">End position of range</param>
        ''' <remarks>
        ''' Refactored to use atomic DeleteText method
        ''' </remarks>
        Public Sub DeleteRange(vStartPosition As EditorPosition, vEndPosition As EditorPosition) Implements IEditor.DeleteRange
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                ' Normalize positions
                EditorPosition.Normalize(vStartPosition, vEndPosition)
                
                ' Validate positions
                If vStartPosition.Line < 0 OrElse vStartPosition.Line >= pLineCount Then Return
                If vEndPosition.Line < 0 OrElse vEndPosition.Line >= pLineCount Then Return
                
                ' Get text being deleted for undo
                Dim lDeletedText As String = GetTextInRange(vStartPosition.Line, vStartPosition.Column, vEndPosition.Line, vEndPosition.Column)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordDeleteText(vStartPosition, vEndPosition, lDeletedText, vStartPosition)
                End If
                
                ' Use atomic DeleteText
                pSourceFileInfo.DeleteText(vStartPosition.Line, vStartPosition.Column, 
                                          vEndPosition.Line, vEndPosition.Column)
                
                ' Set cursor to deletion point
                SetCursorPosition(vStartPosition.Line, vStartPosition.Column)
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteRange error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Replaces text in a range with new text using atomic operations
        ''' </summary>
        ''' <param name="vStartPosition">Start of range to replace</param>
        ''' <param name="vEndPosition">End of range to replace</param>
        ''' <param name="vNewText">Text to insert</param>
        ''' <remarks>
        ''' Refactored to use atomic DeleteText followed by InsertText
        ''' </remarks>
        Public Sub ReplaceText(vStartPosition As EditorPosition, vEndPosition As EditorPosition, 
                              vNewText As String) Implements IEditor.ReplaceText
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                ' Normalize positions
                EditorPosition.Normalize(vStartPosition, vEndPosition)
                
                ' Get old text for undo
                Dim lOldText As String = GetTextInRange(vStartPosition.Line, vStartPosition.Column, vEndPosition.Line, vEndPosition.Column)
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Delete existing text using atomic operation
                pSourceFileInfo.DeleteText(vStartPosition.Line, vStartPosition.Column,
                                          vEndPosition.Line, vEndPosition.Column)
                
                ' Insert new text using atomic operation (if not empty)
                If Not String.IsNullOrEmpty(vNewText) Then
                    pSourceFileInfo.InsertText(vStartPosition.Line, vStartPosition.Column, vNewText)
                End If
                
                ' Calculate new cursor position
                Dim lNewCursorPos As EditorPosition
                If String.IsNullOrEmpty(vNewText) Then
                    lNewCursorPos = vStartPosition
                ElseIf vNewText.Contains(Environment.NewLine) Then
                    Dim lLines() As String = vNewText.Split({Environment.NewLine}, StringSplitOptions.None)
                    lNewCursorPos = New EditorPosition(vStartPosition.Line + lLines.Length - 1,
                                                      lLines(lLines.Length - 1).Length)
                Else
                    lNewCursorPos = New EditorPosition(vStartPosition.Line, 
                                                      vStartPosition.Column + vNewText.Length)
                End If
                
                ' End undo group and record
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordReplaceText(vStartPosition, vEndPosition, 
                                                      lOldText, vNewText, lNewCursorPos)
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Set cursor to end of inserted text
                SetCursorPosition(lNewCursorPos.Line, lNewCursorPos.Column)
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceText error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Replaces the entire document text with the specified text
        ''' </summary>
        ''' <param name="vText">The new text content for the entire document</param>
        Public Sub ReplaceAllText(vText As String) Implements IEditor.ReplaceAllText
            Try
                If pIsReadOnly Then Return
                
                ' Begin undo group for complete text replacement
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                Dim lLastLine As Integer = pSourceFileInfo.TextLines.Count - 1
                ' Update the Text property which will handle all the necessary updates
                pSourceFileInfo.DeleteText(0, 0, lLastLine, pSourceFileInfo.TextLines(lLastLine).Length - 1)
                pSourceFileinfo.InsertText(0, 0, vText)
                
                ' Clear any selection
                ClearSelection()
                
                ' Reset cursor position to start of document
                SetCursorPosition(0, 0)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Mark as modified and raise events
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                
                ' Update display
                UpdateLineNumberWidth()
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"ReplaceText: Replaced entire document with {vText.Length} characters")
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceText error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Missing IEditor Indentation Methods =====
        

        

        
        Public Function GetLineIndentation(vLine As Integer) As String Implements IEditor.GetLineIndentation
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return ""
                
                Dim lLine As String = TextLines(vLine)
                Dim lIndentation As New System.Text.StringBuilder()
                
                ' Extract leading whitespace
                for each lChar As Char in lLine
                    If lChar = " "c OrElse lChar = vbTab Then
                        lIndentation.Append(lChar)
                    Else
                        Exit for
                    End If
                Next
                
                Return lIndentation.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"GetLineIndentation error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Private method to add indentation to a line
        ''' </summary>
        ''' <param name="vLine">Line number to indent (0-based)</param>
        Private Sub IndentLinePrivate(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                pSourceFileInfo.InsertText(vLine, 0, "    ")

                ' If cursor is on this line, adjust cursor position
                If pCursorLine = vLine Then
                    pCursorColumn += 4
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IndentLinePrivate error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Private method to remove indentation from a line
        ''' </summary>
        ''' <param name="vLine">Line number to outdent (0-based)</param>
        Private Sub OutdentLinePrivate(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Get current line
                Dim lLine As String = TextLines(vLine)
                If String.IsNullOrEmpty(lLine) Then Return
                
                ' Determine how much to remove
                Dim lRemoveCount As Integer = 0
                
                If lLine.StartsWith(vbTab) Then
                    ' Remove one tab
                    lRemoveCount = 1
                ElseIf lLine.StartsWith(" ") Then
                    ' Remove up to TabWidth spaces
                    for i As Integer = 0 To Math.Min(pTabWidth - 1, lLine.Length - 1)
                        If lLine(i) = " "c Then
                            lRemoveCount += 1
                        Else
                            Exit for
                        End If
                    Next
                End If
                
                If lRemoveCount > 0 Then
                    ' Update the line text
                    TextLines(vLine) = lLine.Substring(lRemoveCount)
                    
                    ' If cursor is on this line, adjust cursor position
                    If pCursorLine = vLine AndAlso pCursorColumn > 0 Then
                        pCursorColumn = Math.Max(0, pCursorColumn - lRemoveCount)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OutdentLinePrivate error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Updated Public Interface Methods (REPLACE EXISTING) =====
        
' Replace: SimpleIDE.Editors.CustomDrawingEditor.IndentLine
' Replace: SimpleIDE.Editors.CustomDrawingEditor.IndentLine
''' <summary>
''' Indents a line by inserting tabs/spaces at the beginning
''' </summary>
''' <param name="vLine">Line to indent (0-based)</param>
''' <remarks>
''' Uses atomic InsertText operation for proper metadata updates
''' </remarks>
Public Sub IndentLine(vLine As Integer) Implements IEditor.IndentLine
    Try
        If vLine < 0 OrElse vLine >= pLineCount Then Return
        If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
        
        Console.WriteLine($"IndentLine: Line {vLine}")
        
        ' Ensure we have settings
        'EnsureSettingsManager()
        
        ' Get indent string (tab or spaces based on settings)
        Dim lIndentString As String = vbTab
        Dim lIndentLength As Integer = 1
        
        If pSettingsManager IsNot Nothing AndAlso Not pSettingsManager.UseTabs Then
            Dim lTabWidth As Integer = pSettingsManager.TabWidth
            If lTabWidth <= 0 Then lTabWidth = 4 ' Default to 4 if not set
            lIndentString = New String(" "c, lTabWidth)
            lIndentLength = lTabWidth
            Console.WriteLine($"IndentLine: Using {lTabWidth} spaces")
        Else
            Console.WriteLine("IndentLine: Using tab")
        End If
        
        ' Record for undo
        If pUndoRedoManager IsNot Nothing Then
            Dim lStartPos As New EditorPosition(vLine, 0)
            Dim lEndPos As New EditorPosition(vLine, lIndentLength)
            pUndoRedoManager.RecordInsertText(lStartPos, lIndentString, lEndPos)
        End If
        
        ' Use atomic InsertText at beginning of line
        pSourceFileInfo.InsertText(vLine, 0, lIndentString)
        
        ' Adjust cursor if on this line
        If pCursorLine = vLine Then
            SetCursorPosition(pCursorLine, pCursorColumn + lIndentLength)
            Console.WriteLine($"IndentLine: Cursor adjusted to column {pCursorColumn}")
        End If
        
        ' Update state
        IsModified = True
        RaiseEvent TextChanged(Me, EventArgs.Empty)
        
        ' Update UI
        pDrawingArea?.QueueDraw()
        
        Console.WriteLine("IndentLine: Completed")
        
    Catch ex As Exception
        Console.WriteLine($"IndentLine error: {ex.Message}")
    End Try
End Sub
        
' Replace: SimpleIDE.Editors.CustomDrawingEditor.OutdentLine
' Replace: SimpleIDE.Editors.CustomDrawingEditor.OutdentLine
''' <summary>
''' Outdents a line by removing leading tabs/spaces
''' </summary>
''' <param name="vLine">Line to outdent (0-based)</param>
''' <remarks>
''' Uses atomic DeleteText operation for proper metadata updates
''' </remarks>
Public Sub OutdentLine(vLine As Integer) Implements IEditor.OutdentLine
    Try
        If vLine < 0 OrElse vLine >= pLineCount Then Return
        If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
        
        Dim lLine As String = pSourceFileInfo.TextLines(vLine)
        If String.IsNullOrEmpty(lLine) Then Return
        
        Console.WriteLine($"OutdentLine: Line {vLine}, content='{lLine}'")
        
        ' Ensure we have settings
        'EnsureSettingsManager()
        
        ' Determine how much to outdent
        Dim lCharsToRemove As Integer = 0
        Dim lTabWidth As Integer = 4 ' Default
        
        If pSettingsManager IsNot Nothing Then
            lTabWidth = pSettingsManager.TabWidth
            If lTabWidth <= 0 Then lTabWidth = 4
        End If
        
        If lLine.StartsWith(vbTab) Then
            ' Remove one tab
            lCharsToRemove = 1
            Console.WriteLine("OutdentLine: Removing one tab")
        ElseIf lLine.StartsWith(" ") Then
            ' Remove up to TabWidth spaces
            While lCharsToRemove < lLine.Length AndAlso 
                  lCharsToRemove < lTabWidth AndAlso 
                  lLine(lCharsToRemove) = " "c
                lCharsToRemove += 1
            End While
            Console.WriteLine($"OutdentLine: Removing {lCharsToRemove} spaces")
        End If
        
        If lCharsToRemove > 0 Then
            ' Get text being removed for undo
            Dim lRemovedText As String = lLine.Substring(0, lCharsToRemove)
            
            ' Record for undo
            If pUndoRedoManager IsNot Nothing Then
                Dim lStartPos As New EditorPosition(vLine, 0)
                Dim lEndPos As New EditorPosition(vLine, lCharsToRemove)
                pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lRemovedText, lStartPos)
            End If
            
            ' Use atomic DeleteText
            pSourceFileInfo.DeleteText(vLine, 0, vLine, lCharsToRemove)
            
            ' Adjust cursor if on this line
            If pCursorLine = vLine AndAlso pCursorColumn > 0 Then
                SetCursorPosition(pCursorLine, Math.Max(0, pCursorColumn - lCharsToRemove))
                Console.WriteLine($"OutdentLine: Cursor adjusted To column {pCursorColumn}")
            End If
            
            ' Update state
            IsModified = True
            RaiseEvent TextChanged(Me, EventArgs.Empty)
            
            ' Update UI
            pDrawingArea?.QueueDraw()
            
            Console.WriteLine("OutdentLine: Completed")
        Else
            Console.WriteLine("OutdentLine: Nothing To remove")
        End If
        
    Catch ex As Exception
        Console.WriteLine($"OutdentLine error: {ex.Message}")
    End Try
End Sub
        
        ' ===== Updated Selection Methods (REPLACE EXISTING) =====
        
' Replace: SimpleIDE.Editors.CustomDrawingEditor.IndentSelection
' Replace: SimpleIDE.Editors.CustomDrawingEditor.IndentSelection
''' <summary>
''' Indents all lines in the current selection
''' </summary>
''' <remarks>
''' Uses atomic operations for each line to ensure proper metadata updates
''' </remarks>
Public Sub IndentSelection() Implements IEditor.IndentSelection
    Try
        If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
        
        Console.WriteLine("IndentSelection: Starting")
        
        Dim lStartLine As Integer
        Dim lEndLine As Integer
        
        If pHasSelection Then
            ' Get selection bounds
            Dim lStart As EditorPosition = GetSelectionStart()
            Dim lEnd As EditorPosition = GetSelectionEnd()
            lStartLine = lStart.Line
            lEndLine = lEnd.Line
            Console.WriteLine($"IndentSelection: Selection from line {lStartLine} To {lEndLine}")
        Else
            ' Just indent current line
            lStartLine = pCursorLine
            lEndLine = pCursorLine
            Console.WriteLine($"IndentSelection: Single line {lStartLine}")
        End If
        
        ' Validate line bounds
        lStartLine = Math.Max(0, Math.Min(lStartLine, pLineCount - 1))
        lEndLine = Math.Max(0, Math.Min(lEndLine, pLineCount - 1))
        
        ' Get indent string (tab or spaces based on settings)
        Dim lIndentLength As Integer = pSettingsManager.TabWidth
        Console.WriteLine("IndentSelection: TabWidth in SettinsManager = " + lIndentLength.ToString)
        Dim lIndentString As String  = New String(" "c, lIndentLength)
        
        If pSettingsManager IsNot Nothing AndAlso Not pSettingsManager.UseTabs Then
            Dim lTabWidth As Integer = pSettingsManager.TabWidth
            If lTabWidth <= 0 Then lTabWidth = 4 ' Default to 4 if not set
            lIndentString = New String(" "c, lTabWidth)
            lIndentLength = lTabWidth
            Console.WriteLine($"IndentSelection: Using {lTabWidth} spaces for indent")
        End If
        
        ' Begin undo group
        If pUndoRedoManager IsNot Nothing Then
            pUndoRedoManager.BeginUserAction()
        End If
        
        ' Indent each line using atomic operations
        Dim lLinesIndented As Integer = 0
        For i As Integer = lStartLine To lEndLine
            If i < pSourceFileInfo.TextLines.Count Then
                Console.WriteLine($"IndentSelection: Indenting line {i}")
                
                ' Use atomic InsertText at beginning of line
                pSourceFileInfo.InsertText(i, 0, lIndentString)
                lLinesIndented += 1
            End If
        Next
        
        Console.WriteLine($"IndentSelection: Indented {lLinesIndented} lines")
        
        ' End undo group
        If pUndoRedoManager IsNot Nothing Then
            pUndoRedoManager.EndUserAction()
        End If
        
        ' Adjust cursor position if it's on an indented line
        If pCursorLine >= lStartLine AndAlso pCursorLine <= lEndLine Then
            SetCursorPosition(pCursorLine, pCursorColumn + lIndentLength)
            Console.WriteLine($"IndentSelection: Cursor moved To ({pCursorLine},{pCursorColumn})")
        End If
        
        ' Maintain and adjust selection if we had one
        If pHasSelection Then
            ' After indenting, the selection should still cover the same lines
            ' but columns need to be adjusted
            pSelectionStartLine = lStartLine
            pSelectionStartColumn = 0
            pSelectionEndLine = lEndLine
            
            ' Get the length of the last line after indenting
            If lEndLine < pSourceFileInfo.TextLines.Count Then
                pSelectionEndColumn = pSourceFileInfo.TextLines(lEndLine).Length
            Else
                pSelectionEndColumn = 0
            End If
            
            ' Ensure selection flags are set
            pSelectionActive = True
            pHasSelection = True
            
            Console.WriteLine($"IndentSelection: Selection adjusted To ({pSelectionStartLine},{pSelectionStartColumn}) - ({pSelectionEndLine},{pSelectionEndColumn})")
        End If
        
        ' Update state
        IsModified = True
        RaiseEvent TextChanged(Me, EventArgs.Empty)
        
        ' Update UI
        UpdateLineNumberWidth()
        UpdateScrollbars()
        EnsureCursorVisible()
        pDrawingArea?.QueueDraw()
        
        Console.WriteLine("IndentSelection: Completed")
        
    Catch ex As Exception
        Console.WriteLine($"IndentSelection error: {ex.Message}")
        Console.WriteLine($"  Stack: {ex.StackTrace}")
        
        ' Ensure undo group is ended even on error
        If pUndoRedoManager IsNot Nothing Then
            Try
                pUndoRedoManager.EndUserAction()
            Catch
                ' Ignore errors ending undo group
            End Try
        End If
    End Try
End Sub
        
' Replace: SimpleIDE.Editors.CustomDrawingEditor.OutdentSelection
' Replace: SimpleIDE.Editors.CustomDrawingEditor.OutdentSelection
''' <summary>
''' Outdents all lines in the current selection
''' </summary>
''' <remarks>
''' Uses atomic operations for each line to ensure proper metadata updates
''' </remarks>
Public Sub OutdentSelection() Implements IEditor.OutdentSelection
    Try
        If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
        
        Console.WriteLine("OutdentSelection: Starting")
        
        Dim lStartLine As Integer
        Dim lEndLine As Integer
        
        If pHasSelection Then
            ' Get selection bounds
            Dim lStart As EditorPosition = GetSelectionStart()
            Dim lEnd As EditorPosition = GetSelectionEnd()
            lStartLine = lStart.Line
            lEndLine = lEnd.Line
            Console.WriteLine($"OutdentSelection: Selection from line {lStartLine} To {lEndLine}")
        Else
            ' Just outdent current line
            lStartLine = pCursorLine
            lEndLine = pCursorLine
            Console.WriteLine($"OutdentSelection: Single line {lStartLine}")
        End If
        
        ' Validate line bounds
        lStartLine = Math.Max(0, Math.Min(lStartLine, pLineCount - 1))
        lEndLine = Math.Max(0, Math.Min(lEndLine, pLineCount - 1))
        
        ' Ensure we have settings
        'EnsureSettingsManager()
        
        Dim lTabWidth As Integer = 4 ' Default
        If pSettingsManager IsNot Nothing Then
            lTabWidth = pSettingsManager.TabWidth
            If lTabWidth <= 0 Then lTabWidth = 4
        End If
        
        Console.WriteLine($"OutdentSelection: Using tab width Of {lTabWidth}")
        
        ' Begin undo group
        If pUndoRedoManager IsNot Nothing Then
            pUndoRedoManager.BeginUserAction()
        End If
        
        ' Track total characters removed from each line for cursor adjustment
        Dim lRemovedPerLine As New Dictionary(Of Integer, Integer)
        Dim lLinesOutdented As Integer = 0
        
        ' Outdent each line using atomic operations
        For i As Integer = lStartLine To lEndLine
            If i < pSourceFileInfo.TextLines.Count Then
                Dim lLine As String = pSourceFileInfo.TextLines(i)
                Dim lCharsToRemove As Integer = 0
                
                ' Determine how much to outdent
                If lLine.StartsWith(vbTab) Then
                    lCharsToRemove = 1
                    Console.WriteLine($"OutdentSelection: Line {i} - removing tab")
                ElseIf lLine.StartsWith(" ") Then
                    While lCharsToRemove < lLine.Length AndAlso 
                          lCharsToRemove < lTabWidth AndAlso 
                          lLine(lCharsToRemove) = " "c
                        lCharsToRemove += 1
                    End While
                    Console.WriteLine($"OutdentSelection: Line {i} - removing {lCharsToRemove} spaces")
                End If
                
                If lCharsToRemove > 0 Then
                    ' Use atomic DeleteText
                    pSourceFileInfo.DeleteText(i, 0, i, lCharsToRemove)
                    lRemovedPerLine(i) = lCharsToRemove
                    lLinesOutdented += 1
                End If
            End If
        Next
        
        Console.WriteLine($"OutdentSelection: Outdented {lLinesOutdented} lines")
        
        ' End undo group
        If pUndoRedoManager IsNot Nothing Then
            pUndoRedoManager.EndUserAction()
        End If
        
        ' Adjust cursor if on an outdented line
        If lRemovedPerLine.ContainsKey(pCursorLine) Then
            Dim lRemoved As Integer = lRemovedPerLine(pCursorLine)
            If pCursorColumn > 0 Then
                SetCursorPosition(pCursorLine, Math.Max(0, pCursorColumn - lRemoved))
                Console.WriteLine($"OutdentSelection: Cursor adjusted To column {pCursorColumn}")
            End If
        End If
        
        ' Maintain selection if we had one
        If pHasSelection Then
            ' After outdenting, the selection should still cover the same lines
            pSelectionStartLine = lStartLine
            pSelectionStartColumn = 0
            pSelectionEndLine = lEndLine
            
            ' Get the length of the last line after outdenting
            If lEndLine < pSourceFileInfo.TextLines.Count Then
                pSelectionEndColumn = pSourceFileInfo.TextLines(lEndLine).Length
            Else
                pSelectionEndColumn = 0
            End If
            
            ' Ensure selection flags are set
            pSelectionActive = True
            pHasSelection = True
            
            Console.WriteLine($"OutdentSelection: Selection adjusted To ({pSelectionStartLine},{pSelectionStartColumn}) - ({pSelectionEndLine},{pSelectionEndColumn})")
        End If
        
        ' Update state
        IsModified = True
        RaiseEvent TextChanged(Me, EventArgs.Empty)
        
        ' Update UI
        UpdateLineNumberWidth()
        UpdateScrollbars()
        EnsureCursorVisible()
        pDrawingArea?.QueueDraw()
        
        Console.WriteLine("OutdentSelection: Completed")
        
    Catch ex As Exception
        Console.WriteLine($"OutdentSelection error: {ex.Message}")
        Console.WriteLine($"  Stack: {ex.StackTrace}")
        
        ' Ensure undo group is ended even on error
        If pUndoRedoManager IsNot Nothing Then
            Try
                pUndoRedoManager.EndUserAction()
            Catch
                ' Ignore errors ending undo group
            End Try
        End If
    End Try
End Sub
        
    End Class
    
End Namespace
