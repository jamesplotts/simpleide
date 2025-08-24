' Editors/CustomDrawingEditor.Editing.vb - Text editing operations
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' CodeSense events (if not defined elsewhere)
        Public Event CodeSenseRequested(sender As Object, Context As CodeSenseContext)
        Public Event CodeSenseCancelled(sender As Object, e As EventArgs)

        ' ===== Character Operations =====
        

        
        ''' <summary>
        ''' Deletes a character at the specified position
        ''' </summary>
        Private Sub DeleteCharacterAt(vLine As Integer, vColumn As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(vLine)
                If vColumn >= 0 AndAlso vColumn < lLine.Length Then
                    Dim lDeletedChar As Char = lLine(vColumn)
                    
                    ' Record for undo using EditorPosition
                    If pUndoRedoManager IsNot Nothing Then
                        Dim lStartPos As New EditorPosition(vLine, vColumn)
                        Dim lEndPos As New EditorPosition(vLine, vColumn + 1)
                        Dim lCursorPos As New EditorPosition(vLine, vColumn)
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lDeletedChar.ToString(), lCursorPos)
                    End If
                    
                    pTextLines(vLine) = lLine.Remove(vColumn, 1)
                    pLineMetadata(vLine).MarkChanged()
                    
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteCharacterAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Line Operations =====
        
        ''' <summary>
        ''' Inserts a new line at the cursor position
        ''' </summary>
        Private Sub InsertNewLine()
            Try
                If pIsReadOnly Then Return
                
                ' Ensure we have a valid line
                If pCursorLine >= pLineCount Then
                    AddNewLine("")
                    Return
                End If
                
                ' Split current line at cursor position
                Dim lCurrentLine As String = pTextLines(pCursorLine)
                Dim lBeforeCursor As String = lCurrentLine.Substring(0, Math.Min(pCursorColumn, lCurrentLine.Length))
                Dim lAfterCursor As String = If(pCursorColumn < lCurrentLine.Length, 
                                                lCurrentLine.Substring(pCursorColumn), "")
                
                ' Save the colors for the part after cursor (if any)
                Dim lAfterCursorColors() As CharacterColorInfo = Nothing
                If pCharacterColors IsNot Nothing AndAlso pCursorLine < pCharacterColors.Length AndAlso 
                   pCharacterColors(pCursorLine) IsNot Nothing AndAlso pCursorColumn < lCurrentLine.Length Then
                    Dim lOriginalColors() As CharacterColorInfo = pCharacterColors(pCursorLine)
                    If lOriginalColors.Length > pCursorColumn Then
                        ' Copy colors for the part that will move to the new line
                        ReDim lAfterCursorColors(lAfterCursor.Length - 1)
                        For i As Integer = 0 To lAfterCursor.Length - 1
                            If pCursorColumn + i < lOriginalColors.Length Then
                                lAfterCursorColors(i) = lOriginalColors(pCursorColumn + i)
                            Else
                                lAfterCursorColors(i) = New CharacterColorInfo()
                            End If
                        Next
                    End If
                End If
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                    
                    ' Record replacing current line with the part before cursor
                    Dim lReplaceStart As New EditorPosition(pCursorLine, 0)
                    Dim lReplaceEnd As New EditorPosition(pCursorLine, lCurrentLine.Length)
                    Dim lTempCursorPos As New EditorPosition(pCursorLine, lBeforeCursor.Length)
                    pUndoRedoManager.RecordReplaceText(lReplaceStart, lReplaceEnd, lCurrentLine, lBeforeCursor, lTempCursorPos)
                    
                    ' Record inserting new line with the part after cursor
                    Dim lInsertPos As New EditorPosition(pCursorLine + 1, 0)
                    Dim lNewCursorPos As New EditorPosition(pCursorLine + 1, 0)
                    pUndoRedoManager.RecordInsertLine(lInsertPos, lAfterCursor, lNewCursorPos)
                    
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Update current line
                pTextLines(pCursorLine) = lBeforeCursor
                pLineMetadata(pCursorLine).MarkChanged()
                
                ' Resize color array for current line
                If pCharacterColors IsNot Nothing AndAlso pCursorLine < pCharacterColors.Length AndAlso
                   pCharacterColors(pCursorLine) IsNot Nothing Then
                    If lBeforeCursor.Length > 0 Then
                        ReDim Preserve pCharacterColors(pCursorLine)(lBeforeCursor.Length - 1)
                    Else
                        pCharacterColors(pCursorLine) = New CharacterColorInfo() {}
                    End If
                End If
                
                ' Insert new line
                InsertLineAt(pCursorLine + 1, lAfterCursor)
                
                ' Set colors for the new line if we saved them
                If lAfterCursorColors IsNot Nothing AndAlso pCursorLine + 1 < pCharacterColors.Length Then
                    pCharacterColors(pCursorLine + 1) = lAfterCursorColors
                End If
                
                ' Move cursor to beginning of next line
                SetCursorPosition(pCursorLine + 1, 0)
                
                ' Apply auto-indent if enabled
                If pSettingsManager IsNot Nothing AndAlso pSettingsManager.AutoIndent Then
                    ApplyAutoIndent(pCursorLine)
                End If
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"InsertNewLine error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Delete Operations =====
        
        ''' <summary>
        ''' Deletes backward from cursor (Backspace)
        ''' </summary>
        Private Sub DeleteBackward()
            Try
                If pIsReadOnly Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                Else
                    If pCursorColumn > 0 Then
                        ' Delete character before cursor
                        Dim lLine As String = pTextLines(pCursorLine)
                        Dim lDeletedChar As Char = lLine(pCursorColumn - 1)
                        
                        ' Record for undo using EditorPosition
                        If pUndoRedoManager IsNot Nothing Then
                            Dim lStartPos As New EditorPosition(pCursorLine, pCursorColumn - 1)
                            Dim lEndPos As New EditorPosition(pCursorLine, pCursorColumn)
                            Dim lNewCursorPos As New EditorPosition(pCursorLine, pCursorColumn - 1)
                            pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lDeletedChar.ToString(), lNewCursorPos)
                        End If
                        
                        pTextLines(pCursorLine) = lLine.Remove(pCursorColumn - 1, 1)
                        pLineMetadata(pCursorLine).MarkChanged()
                        
                        ' Move cursor back
                        SetCursorPosition(pCursorLine, pCursorColumn - 1)
                        
                        IsModified = True
                        RaiseEvent TextChanged(Me, New EventArgs)
                    ElseIf pCursorLine > 0 Then
                        ' Join with previous line
                        Dim lPrevLine As String = pTextLines(pCursorLine - 1)
                        Dim lCurrentLine As String = pTextLines(pCursorLine)
                        Dim lNewColumn As Integer = lPrevLine.Length
                        
                        ' Record for undo using EditorPosition
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.BeginUserAction()
                            
                            ' Record the text replacement on the previous line
                            Dim lReplaceStart As New EditorPosition(pCursorLine - 1, 0)
                            Dim lReplaceEnd As New EditorPosition(pCursorLine - 1, lPrevLine.Length)
                            Dim lNewCursorPos As New EditorPosition(pCursorLine - 1, lNewColumn)
                            pUndoRedoManager.RecordReplaceText(lReplaceStart, lReplaceEnd, 
                                                              lPrevLine, lPrevLine & lCurrentLine, lNewCursorPos)
                            
                            ' Record the line deletion
                            pUndoRedoManager.RecordDeleteLine(pCursorLine, lCurrentLine, lNewCursorPos)
                            
                            pUndoRedoManager.EndUserAction()
                        End If
                        
                        ' Combine lines
                        pTextLines(pCursorLine - 1) = lPrevLine & lCurrentLine
                        pLineMetadata(pCursorLine - 1).MarkChanged()
                        
                        ' Remove current line
                        RemoveLines(pCursorLine, 1)
                        
                        ' Move cursor to join position
                        SetCursorPosition(pCursorLine - 1, lNewColumn)
                        
                        IsModified = True
                        RaiseEvent TextChanged(Me, New EventArgs)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteBackward error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes forward from cursor (Delete key)
        ''' </summary>
        Private Sub DeleteForward()
            Try
                If pIsReadOnly Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                Else
                    If pCursorColumn < pTextLines(pCursorLine).Length Then
                        ' Delete character at cursor
                        DeleteCharacterAt(pCursorLine, pCursorColumn)
                    ElseIf pCursorLine < pLineCount - 1 Then
                        ' Join with next line
                        Dim lCurrentLine As String = pTextLines(pCursorLine)
                        Dim lNextLine As String = pTextLines(pCursorLine + 1)
                        
                        ' Record for undo using EditorPosition
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.BeginUserAction()
                            
                            ' Record the text replacement
                            Dim lReplaceStart As New EditorPosition(pCursorLine, 0)
                            Dim lReplaceEnd As New EditorPosition(pCursorLine, lCurrentLine.Length)
                            Dim lCursorPos As New EditorPosition(pCursorLine, pCursorColumn)
                            pUndoRedoManager.RecordReplaceText(lReplaceStart, lReplaceEnd,
                                                              lCurrentLine, lCurrentLine & lNextLine, lCursorPos)
                            
                            ' Record the line deletion
                            pUndoRedoManager.RecordDeleteLine(pCursorLine + 1, lNextLine, lCursorPos)
                            
                            pUndoRedoManager.EndUserAction()
                        End If
                        
                        ' Combine lines
                        pTextLines(pCursorLine) = lCurrentLine & lNextLine
                        pLineMetadata(pCursorLine).MarkChanged()
                        
                        ' Remove next line
                        RemoveLines(pCursorLine + 1, 1)
                        
                        IsModified = True
                        RaiseEvent TextChanged(Me, New EventArgs)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteForward error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Text Insertion =====
        
        ''' <summary>
        ''' Inserts text at the current cursor position
        ''' </summary>
        Public Sub InsertText(vText As String) Implements IEditor.InsertText
            Try
                If pIsReadOnly OrElse String.IsNullOrEmpty(vText) Then Return
                
                ' Begin undo group for multi-character insert
                If pUndoRedoManager IsNot Nothing AndAlso vText.Length > 1 Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Process each character/line
                For Each lChar As Char In vText
                    If lChar = vbLf OrElse lChar = vbCr Then
                        ' Skip CR in CRLF pairs
                        If lChar = vbCr AndAlso vText.IndexOf(lChar) < vText.Length - 1 AndAlso 
                           vText(vText.IndexOf(lChar) + 1) = vbLf Then
                            Continue For
                        End If
                        InsertNewLine()
                    Else
                        InsertCharacter(lChar)
                    End If
                Next
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing AndAlso vText.Length > 1 Then
                    pUndoRedoManager.EndUserAction()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InsertText error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Multi-line Text Operations =====
        
        ''' <summary>
        ''' Inserts multi-line text at a specific position
        ''' </summary>
        Friend Sub InsertMultiLineTextAt(vLine As Integer, vColumn As Integer, vText As String)
            Try
                ' Split text into lines
                Dim lLines() As String = vText.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                
                If lLines.Length = 0 Then Return
                
                ' Get the current line
                Dim lCurrentLine As String = pTextLines(vLine)
                Dim lBeforeInsert As String = If(vColumn > 0, lCurrentLine.Substring(0, Math.Min(vColumn, lCurrentLine.Length)), "")
                Dim lAfterInsert As String = If(vColumn < lCurrentLine.Length, lCurrentLine.Substring(vColumn), "")
                
                If lLines.Length = 1 Then
                    ' Single line insert
                    pTextLines(vLine) = lBeforeInsert & lLines(0) & lAfterInsert
                    pLineMetadata(vLine).MarkChanged()
                    
                    ' Move cursor to end of inserted text
                    SetCursorPosition(vLine, vColumn + lLines(0).Length)
                Else
                    ' Multi-line insert
                    ' First line
                    pTextLines(vLine) = lBeforeInsert & lLines(0)
                    pLineMetadata(vLine).MarkChanged()
                    
                    ' Insert middle lines
                    For i As Integer = 1 To lLines.Length - 2
                        InsertLineAt(vLine + i, lLines(i))
                    Next
                    
                    ' Last line
                    InsertLineAt(vLine + lLines.Length - 1, lLines(lLines.Length - 1) & lAfterInsert)
                    
                    ' Move cursor to end of inserted text
                    SetCursorPosition(vLine + lLines.Length - 1, lLines(lLines.Length - 1).Length)
                End If
                
                ' Update line count
                UpdateLineNumberWidth()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertMultiLineTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Replace Operations =====
        
        ''' <summary>
        ''' Replaces text at a specific position
        ''' </summary>
        Friend Sub ReplaceTextAt(vLine As Integer, vColumn As Integer, vLength As Integer, vNewText As String)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(vLine)
                
                ' Ensure column is within valid range
                If vColumn < 0 Then vColumn = 0
                If vColumn > lLine.Length Then vColumn = lLine.Length
                
                ' Calculate actual length to replace
                Dim lActualLength As Integer = Math.Min(vLength, lLine.Length - vColumn)
                
                ' Replace the text
                If lActualLength > 0 Then
                    lLine = lLine.Remove(vColumn, lActualLength)
                End If
                lLine = lLine.Insert(vColumn, vNewText)
                
                pTextLines(vLine) = lLine
                pLineMetadata(vLine).MarkChanged()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Key Handlers =====
        
        ''' <summary>
        ''' Handles the Return/Enter key
        ''' </summary>
        Private Sub HandleReturn()
            Try
                If pIsReadOnly Then Return
                
                ' Delete selection if any
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Insert new line
                InsertNewLine()
                
            Catch ex As Exception
                Console.WriteLine($"HandleReturn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the Backspace key
        ''' </summary>
        Private Sub HandleBackspace()
            Try
                If pIsReadOnly Then Return
                DeleteBackward()
            Catch ex As Exception
                Console.WriteLine($"HandleBackspace error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the Delete key
        ''' </summary>
        Private Sub HandleDelete()
            Try
                If pIsReadOnly Then Return
                DeleteForward()
            Catch ex As Exception
                Console.WriteLine($"HandleDelete error: {ex.Message}")
            End Try
        End Sub



        ''' <summary>
        ''' Inserts text at the specified position using EditorPosition
        ''' </summary>
        ''' <param name="vPosition">The position to insert the text at</param>
        ''' <param name="vText">The text to insert</param>
        Public Sub InsertTextAtPosition(vPosition As EditorPosition, vText As String) Implements IEditor.InsertTextAtPosition
            Try
                If pIsReadOnly Then Return
                If String.IsNullOrEmpty(vText) Then Return
                
                ' Validate position
                If vPosition.Line < 0 OrElse vPosition.Line >= pLineCount Then Return
                If vPosition.Column < 0 Then Return
                
                ' Begin undo group for multi-character operations
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Handle multi-line text insertion
                If vText.Contains(Environment.NewLine) OrElse vText.Contains(vbLf) OrElse vText.Contains(vbCrLf) Then
                    ' Split text into lines
                    Dim lLines() As String = vText.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                    
                    If lLines.Length = 0 Then Return
                    
                    ' Get the current line
                    Dim lCurrentLine As String = pTextLines(vPosition.Line)
                    Dim lBeforeInsert As String = If(vPosition.Column > 0 AndAlso vPosition.Column <= lCurrentLine.Length, 
                                                   lCurrentLine.Substring(0, vPosition.Column), 
                                                   If(vPosition.Column > lCurrentLine.Length, lCurrentLine.PadRight(vPosition.Column), ""))
                    Dim lAfterInsert As String = If(vPosition.Column < lCurrentLine.Length, 
                                                  lCurrentLine.Substring(vPosition.Column), "")
                    
                    ' Replace current line with first line of inserted text
                    pTextLines(vPosition.Line) = lBeforeInsert & lLines(0)
                    pLineMetadata(vPosition.Line).MarkChanged()
                    
                    ' Insert additional lines if needed
                    For i As Integer = 1 To lLines.Length - 1
                        Dim lNewLineContent As String = lLines(i)
                        
                        ' For the last line, append the remainder of the original line
                        If i = lLines.Length - 1 Then
                            lNewLineContent &= lAfterInsert
                        End If
                        
                        ' Insert the new line
                        InsertLineAt(vPosition.Line + i, lNewLineContent)
                    Next
                    
                    ' Set cursor to end of inserted text
                    Dim lNewLine As Integer = vPosition.Line + lLines.Length - 1
                    Dim lNewColumn As Integer = lLines(lLines.Length - 1).Length
                    If lLines.Length = 1 Then
                        lNewColumn = vPosition.Column + lLines(0).Length
                    End If
                    SetCursorPosition(lNewLine, lNewColumn)
                Else
                    ' Single line text insertion
                    Dim lLine As String = pTextLines(vPosition.Line)
                    
                    If vPosition.Column <= lLine.Length Then
                        pTextLines(vPosition.Line) = lLine.Insert(vPosition.Column, vText)
                    Else
                        ' Pad line if column is beyond current line length
                        pTextLines(vPosition.Line) = lLine.PadRight(vPosition.Column) & vText
                    End If
                    
                    pLineMetadata(vPosition.Line).MarkChanged()
                    
                    ' Set cursor to end of inserted text
                    SetCursorPosition(vPosition.Line, vPosition.Column + vText.Length)
                End If
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Mark as modified and raise events
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Process syntax highlighting for affected lines
                ProcessLineFormatting(vPosition.Line)
                If vText.Contains(Environment.NewLine) Then
                    ScheduleFullDocumentParse()
                End If
                
                ' Update display
                UpdateLineNumberWidth()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextAtPosition error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Replaces the current selection with the specified text
        ''' </summary>
        ''' <param name="vText">Text to replace the selection with</param>
        ''' <remarks>
        ''' If there is no selection, the text is inserted at the cursor position.
        ''' This method records the operation for undo/redo.
        ''' </remarks>
        Public Sub ReplaceSelection(vText As String) Implements IEditor.ReplaceSelection
            Try
                ' Check if read-only
                If pIsReadOnly Then Return
                
                ' If no text provided, this is equivalent to delete
                If vText Is Nothing Then vText = ""
                
                ' Begin undo group for the replacement operation
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                Try
                    If pHasSelection Then
                        ' ===== Replace Selected Text =====
                        ReplaceSelectedText(vText)
                    Else
                        ' ===== No Selection - Insert at Cursor =====
                        InsertAtCursor(vText)
                    End If
                    
                Finally
                    ' End undo group
                    If pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.EndUserAction()
                    End If
                End Try
                
                ' Mark document as modified
                IsModified = True
                
                ' Update UI
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
                ' Raise text changed event
                RaiseEvent TextChanged(Me, New EventArgs())
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to replace selected text
        ''' </summary>
        Private Sub ReplaceSelectedText(vText As String)
            Try
                ' Get selection bounds
                Dim lStartPos As New EditorPosition(pSelectionStartLine, pSelectionStartColumn)
                Dim lEndPos As New EditorPosition(pSelectionEndLine, pSelectionEndColumn)
                
                ' Normalize selection
                NormalizeSelection(lStartPos, lEndPos)
                
                ' Get the selected text for undo
                Dim lSelectedText As String = GetSelectedText()
                
                ' Calculate new cursor position after replacement
                Dim lNewCursorPos As EditorPosition = CalculateNewCursorPosition(lStartPos, vText)
                
                ' Record the replacement for undo
                If pUndoRedoManager IsNot Nothing AndAlso Not pUndoRedoManager.IsUndoingOrRedoing Then
                    pUndoRedoManager.RecordReplaceText(lStartPos, lEndPos, lSelectedText, vText, lNewCursorPos)
                End If
                
                ' Perform the replacement
                If String.IsNullOrEmpty(vText) Then
                    ' Empty replacement - just delete
                    DeleteTextDirect(lStartPos, lEndPos)
                    SetCursorPosition(lStartPos)
                Else
                    ' Delete old text and insert new
                    DeleteTextDirect(lStartPos, lEndPos)
                    SetCursorPosition(lStartPos)
                    InsertTextDirect(vText)
                End If
                
                ' Clear selection
                ClearSelection()
                
                ' Set final cursor position
                SetCursorPosition(lNewCursorPos)
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceSelectedText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to insert text at cursor when no selection exists
        ''' </summary>
        Private Sub InsertAtCursor(vText As String)
            Try
                ' Get current cursor position
                Dim lCursorPos As New EditorPosition(pCursorLine, pCursorColumn)
                
                ' Calculate new cursor position after insertion
                Dim lNewCursorPos As EditorPosition = CalculateNewCursorPosition(lCursorPos, vText)
                
                ' Record the insertion for undo
                If pUndoRedoManager IsNot Nothing AndAlso Not pUndoRedoManager.IsUndoingOrRedoing Then
                    pUndoRedoManager.RecordInsertText(lCursorPos, vText, lNewCursorPos)
                End If
                
                ' Insert the text
                InsertTextDirect(vText)
                
                ' Cursor position is already updated by InsertTextDirect
                
            Catch ex As Exception
                Console.WriteLine($"InsertAtCursor error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Calculates the new cursor position after inserting text
        ''' </summary>
        Private Function CalculateNewCursorPosition(vStartPos As EditorPosition, vText As String) As EditorPosition
            Try
                If String.IsNullOrEmpty(vText) Then
                    Return vStartPos
                End If
                
                ' Check if text contains newlines
                Dim lLines() As String = vText.Split({Environment.NewLine, vbLf.ToString()}, StringSplitOptions.None)
                
                If lLines.Length = 1 Then
                    ' Single line - cursor at end of inserted text
                    Return New EditorPosition(vStartPos.Line, vStartPos.Column + vText.Length)
                Else
                    ' Multi-line - cursor at end of last line
                    Dim lNewLine As Integer = vStartPos.Line + lLines.Length - 1
                    Dim lNewColumn As Integer = lLines(lLines.Length - 1).Length
                    Return New EditorPosition(lNewLine, lNewColumn)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CalculateNewCursorPosition error: {ex.Message}")
                Return vStartPos
            End Try
        End Function
        
        ''' <summary>
        ''' Insert text directly without recording undo (helper for ReplaceSelection)
        ''' </summary>
        Private Sub InsertTextDirect(vText As String)
            Try
                If String.IsNullOrEmpty(vText) Then Return
                
                ' Save current position
                Dim lStartLine As Integer = pCursorLine
                Dim lStartColumn As Integer = pCursorColumn
                
                ' Split text into lines
                Dim lLines() As String = vText.Split({Environment.NewLine, vbLf.ToString()}, StringSplitOptions.None)
                
                If lLines.Length = 1 Then
                    ' Single line insertion
                    InsertTextInLine(pCursorLine, pCursorColumn, vText)
                    SetCursorPosition(pCursorLine, pCursorColumn + vText.Length)
                Else
                    ' Multi-line insertion
                    InsertMultiLineText(lLines)
                End If
                
                ' Update syntax highlighting for affected lines
                If pHighlightingEnabled Then
                    Dim lEndLine As Integer = pCursorLine
                    For i As Integer = lStartLine To lEndLine
                        If i < pLineCount Then
                            ProcessLineFormatting(i)
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextDirect error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper to insert text within a single line
        ''' </summary>
        Private Sub InsertTextInLine(vLine As Integer, vColumn As Integer, vText As String)
            Try
                ' Ensure line exists
                While vLine >= pLineCount
                    pTextLines.Add("")
                    EnsureLineMetadata(pLineCount)
                    pLineCount += 1
                End While
                
                ' Get the line
                Dim lLine As String = pTextLines(vLine)
                
                ' Ensure column is valid
                If vColumn > lLine.Length Then
                    ' Pad with spaces if needed
                    lLine = lLine.PadRight(vColumn)
                End If
                
                ' Insert the text
                pTextLines(vLine) = lLine.Insert(vColumn, vText)
                pLineMetadata(vLine).MarkChanged()
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextInLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper to insert multi-line text
        ''' </summary>
        Private Sub InsertMultiLineText(vLines() As String)
            Try
                If vLines.Length = 0 Then Return
                
                ' Get current line
                Dim lCurrentLine As String = If(pCursorLine < pLineCount, pTextLines(pCursorLine), "")
                
                ' Split current line at cursor
                Dim lBeforeCursor As String = If(pCursorColumn <= lCurrentLine.Length, 
                                                 lCurrentLine.Substring(0, pCursorColumn), 
                                                 lCurrentLine)
                Dim lAfterCursor As String = If(pCursorColumn < lCurrentLine.Length,
                                                lCurrentLine.Substring(pCursorColumn),
                                                "")
                
                ' First line: combine with text before cursor
                pTextLines(pCursorLine) = lBeforeCursor & vLines(0)
                pLineMetadata(pCursorLine).MarkChanged()
                
                ' Insert middle lines
                For i As Integer = 1 To vLines.Length - 2
                    InsertLineAt(pCursorLine + i, vLines(i))
                Next
                
                ' Last line: add and combine with text after cursor
                If vLines.Length > 1 Then
                    InsertLineAt(pCursorLine + vLines.Length - 1, vLines(vLines.Length - 1) & lAfterCursor)
                    ' Set cursor to end of inserted text (before the after-cursor text)
                    SetCursorPosition(pCursorLine + vLines.Length - 1, vLines(vLines.Length - 1).Length)
                End If
                
                ' Update line count
                UpdateLineNumberWidth()
                
            Catch ex As Exception
                Console.WriteLine($"InsertMultiLineText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Alternative simpler implementation using existing ReplaceText method
        ''' </summary>
        Public Sub ReplaceSelectionSimple(vText As String) 
            Try
                If pIsReadOnly Then Return
                
                If pHasSelection Then
                    ' Replace selected text
                    Dim lStartPos As EditorPosition = SelectionStart
                    Dim lEndPos As EditorPosition = SelectionEnd
                    ReplaceText(lStartPos, lEndPos, vText)
                    ClearSelection()
                Else
                    ' Insert at cursor
                    Dim lCursorPos As EditorPosition = GetCursorPosition()
                    InsertTextAtPosition(lCursorPos, vText)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceSelectionSimple error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
