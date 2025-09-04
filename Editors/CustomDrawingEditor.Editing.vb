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
        ''' Inserts a character at the specified position
        ''' </summary>
        ''' <param name="vLine">Line number (0-based)</param>
        ''' <param name="vColumn">Column position (0-based)</param>
        ''' <param name="vChar">Character to insert</param>
        ''' <remarks>
        ''' Uses SourceFileInfo for text manipulation and requests async parsing
        ''' </remarks>
        Private Sub InsertCharacterAt(vLine As Integer, vColumn As Integer, vChar As Char)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Record for undo using EditorPosition
                If pUndoRedoManager IsNot Nothing Then
                    Dim lStartPos As New EditorPosition(vLine, vColumn)
                    Dim lEndPos As New EditorPosition(vLine, vColumn + 1)
                    pUndoRedoManager.RecordInsertText(lStartPos, vChar.ToString(), lEndPos)
                End If
                
                ' Insert through SourceFileInfo
                pSourceFileInfo.InsertTextInLine(vLine, vColumn, vChar.ToString())
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"InsertCharacterAt error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes a character at the specified position
        ''' </summary>
        ''' <remarks>
        ''' Updated to use SourceFileInfo for text manipulation
        ''' </remarks>
        Private Sub DeleteCharacterAt(vLine As Integer, vColumn As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                Dim lLine As String = pSourceFileInfo.TextLines(vLine)
                If vColumn >= 0 AndAlso vColumn < lLine.Length Then
                    Dim lDeletedChar As Char = lLine(vColumn)
                    
                    ' Record for undo using EditorPosition
                    If pUndoRedoManager IsNot Nothing Then
                        Dim lStartPos As New EditorPosition(vLine, vColumn)
                        Dim lEndPos As New EditorPosition(vLine, vColumn + 1)
                        Dim lCursorPos As New EditorPosition(vLine, vColumn)
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lDeletedChar.ToString(), lCursorPos)
                    End If
                    
                    ' Delete through SourceFileInfo
                    pSourceFileInfo.DeleteTextInLine(vLine, vColumn, vColumn + 1)
                    
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteCharacterAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Line Operations =====
        
        ''' <summary>
        ''' Inserts a new line at cursor position
        ''' </summary>
        ''' <remarks>
        ''' Simplified to update SourceFileInfo and let async parsing handle colors
        ''' </remarks>
        Private Sub InsertNewLine()
            Try
                If pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Get current line
                Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                
                ' Split at cursor position
                Dim lBeforeCursor As String = lCurrentLine.Substring(0, pCursorColumn)
                Dim lAfterCursor As String = If(pCursorColumn < lCurrentLine.Length, 
                                                lCurrentLine.Substring(pCursorColumn), 
                                                "")
                
                ' Update current line with text before cursor
                pSourceFileInfo.UpdateTextLine(pCursorLine, lBeforeCursor)
                
                ' Insert new line with text after cursor
                pSourceFileInfo.InsertLine(pCursorLine + 1, lAfterCursor)
                
                ' Move cursor to beginning of next line
                SetCursorPosition(pCursorLine + 1, 0)
                
                ' Apply auto-indent if enabled
                If pSettingsManager IsNot Nothing AndAlso pSettingsManager.AutoIndent Then
                    ApplyAutoIndent(pCursorLine)
                End If
                
                ' Mark as modified
                pIsModified = True
                
                ' Trigger events
                OnTextModified()
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertNewLine error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Delete Operations =====
        
        ''' <summary>
        ''' Deletes backward from cursor (Backspace)
        ''' </summary>
        ''' <remarks>
        ''' Simplified to update SourceFileInfo and let async parsing handle colors
        ''' </remarks>
        Private Sub DeleteBackward()
            Try
                If pIsReadOnly Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                Else
                    If pCursorColumn > 0 Then
                        ' Delete character before cursor
                        If pSourceFileInfo IsNot Nothing Then
                            pSourceFileInfo.DeleteTextInLine(pCursorLine, pCursorColumn - 1, pCursorColumn)
                            
                            ' Move cursor back
                            pCursorColumn -= 1
                            
                            ' Mark as modified
                            pIsModified = True
                        End If
                    ElseIf pCursorLine > 0 Then
                        ' Join with previous line
                        If pSourceFileInfo IsNot Nothing Then
                            Dim lPrevLineLength As Integer = pSourceFileInfo.TextLines(pCursorLine - 1).Length
                            pSourceFileInfo.JoinLines(pCursorLine - 1)
                            
                            ' Move cursor to join position
                            pCursorLine -= 1
                            pCursorColumn = lPrevLineLength
                            
                            ' Mark as modified
                            pIsModified = True
                        End If
                    End If
                End If
                
                ' Trigger events
                OnTextModified()
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteBackward error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes forward from cursor (Delete key)
        ''' </summary>
        ''' <remarks>
        ''' Simplified to update SourceFileInfo and let async parsing handle colors
        ''' </remarks>
        Private Sub DeleteForward()
            Try
                If pIsReadOnly Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                Else
                    If pSourceFileInfo IsNot Nothing Then
                        Dim lLineText As String = pSourceFileInfo.TextLines(pCursorLine)
                        
                        If pCursorColumn < lLineText.Length Then
                            ' Delete character at cursor
                            pSourceFileInfo.DeleteTextInLine(pCursorLine, pCursorColumn, pCursorColumn + 1)
                            
                            ' Mark as modified
                            pIsModified = True
                        ElseIf pCursorLine < pLineCount - 1 Then
                            ' Join with next line
                            pSourceFileInfo.JoinLines(pCursorLine)
                            
                            ' Mark as modified
                            pIsModified = True
                        End If
                    End If
                End If
                
                ' Trigger events
                OnTextModified()
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteForward error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Text Insertion =====
        

        
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
                Dim lCurrentLine As String = TextLines(vLine)
                Dim lBeforeInsert As String = If(vColumn > 0, lCurrentLine.Substring(0, Math.Min(vColumn, lCurrentLine.Length)), "")
                Dim lAfterInsert As String = If(vColumn < lCurrentLine.Length, lCurrentLine.Substring(vColumn), "")
                
                If lLines.Length = 1 Then
                    ' Single line insert
                    TextLines(vLine) = lBeforeInsert & lLines(0) & lAfterInsert
                    pLineMetadata(vLine).MarkChanged()
                    
                    ' Move cursor to end of inserted text
                    SetCursorPosition(vLine, vColumn + lLines(0).Length)
                Else
                    ' Multi-line insert
                    ' First line
                    TextLines(vLine) = lBeforeInsert & lLines(0)
                    pLineMetadata(vLine).MarkChanged()
                    
                    ' Insert middle lines
                    for i As Integer = 1 To lLines.Length - 2
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
                
                Dim lLine As String = TextLines(vLine)
                
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
                
                TextLines(vLine) = lLine
                pLineMetadata(vLine).MarkChanged()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Key Handlers =====
        
        ''' <summary>
        ''' Handles the Return/Enter key press
        ''' </summary>
        ''' <remarks>
        ''' Uses SourceFileInfo for line operations and triggers async parsing
        ''' </remarks>
        Private Sub HandleReturn()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                ' Handle selection replacement
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Get current line text from SourceFileInfo
                Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                
                ' Split line at cursor position
                Dim lBeforeCursor As String = If(pCursorColumn > 0, lCurrentLine.Substring(0, Math.Min(pCursorColumn, lCurrentLine.Length)), "")
                Dim lAfterCursor As String = If(pCursorColumn < lCurrentLine.Length, lCurrentLine.Substring(pCursorColumn), "")
                
                ' Update current line with text before cursor
                pSourceFileInfo.UpdateTextLine(pCursorLine, lBeforeCursor)
                
                ' Insert new line with text after cursor
                pSourceFileInfo.InsertLine(pCursorLine + 1, lAfterCursor)
                
                
                ' Move cursor to beginning of next line
                SetCursorPosition(pCursorLine + 1, 0)
                
                ' Apply auto-indent if enabled
                If pSettingsManager IsNot Nothing AndAlso pSettingsManager.AutoIndent Then
                    ApplyAutoIndent(pCursorLine)
                End If
                
                ' Request async parsing
                pSourceFileInfo.RequestAsyncParse()
                
                ' Mark as modified
                IsModified = True
                
                ' Trigger events
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleReturn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the Backspace key
        ''' </summary>
        ''' <remarks>
        ''' Uses SourceFileInfo for all text operations
        ''' </remarks>
        Private Sub HandleBackspace()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                ElseIf pCursorColumn > 0 Then
                    ' Delete character before cursor
                    Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    Dim lNewLine As String = lLine.Remove(pCursorColumn - 1, 1)
                    
                    ' Update through SourceFileInfo
                    pSourceFileInfo.UpdateTextLine(pCursorLine, lNewLine)
                    
                    ' Move cursor back
                    SetCursorPosition(pCursorLine, pCursorColumn - 1)
                    
                    ' Request async parsing
                    pSourceFileInfo.RequestAsyncParse()
                    
                    ' Mark as modified
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                    
                ElseIf pCursorLine > 0 Then
                    ' Join with previous line
                    Dim lPrevLine As String = pSourceFileInfo.TextLines(pCursorLine - 1)
                    Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    
                    ' Join the lines
                    pSourceFileInfo.UpdateTextLine(pCursorLine - 1, lPrevLine & lCurrentLine)
                    pSourceFileInfo.DeleteLine(pCursorLine)
                    
                    
                    ' Move cursor to join point
                    SetCursorPosition(pCursorLine - 1, lPrevLine.Length)
                    
                    ' Request async parsing
                    pSourceFileInfo.RequestAsyncParse()
                    
                    ' Mark as modified
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                End If
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleBackspace error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the Delete key
        ''' </summary>
        ''' <remarks>
        ''' Uses SourceFileInfo for all text operations
        ''' </remarks>
        Private Sub HandleDelete()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                ElseIf pCursorColumn < pSourceFileInfo.TextLines(pCursorLine).Length Then
                    ' Delete character at cursor
                    Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    Dim lNewLine As String = lLine.Remove(pCursorColumn, 1)
                    
                    ' Update through SourceFileInfo
                    pSourceFileInfo.UpdateTextLine(pCursorLine, lNewLine)
                    
                    ' Request async parsing
                    pSourceFileInfo.RequestAsyncParse()
                    
                    ' Mark as modified
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                    
                ElseIf pCursorLine < pLineCount - 1 Then
                    ' Join with next line
                    Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    Dim lNextLine As String = pSourceFileInfo.TextLines(pCursorLine + 1)
                    
                    ' Join the lines
                    pSourceFileInfo.UpdateTextLine(pCursorLine, lCurrentLine & lNextLine)
                    pSourceFileInfo.DeleteLine(pCursorLine + 1)
                    
                    
                    ' Request async parsing
                    pSourceFileInfo.RequestAsyncParse()
                    
                    ' Mark as modified
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                End If
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleDelete error: {ex.Message}")
            End Try
        End Sub

        ' ===== Cut Line Implementation (Ctrl+Y) =====

        ''' <summary>
        ''' Cuts the entire current line to clipboard (VB classic Ctrl+Y behavior)
        ''' </summary>
        ''' <remarks>
        ''' This method preserves all UndoRedoManager functionality while using
        ''' SourceFileInfo for text manipulation and requesting async parsing
        ''' </remarks>
        Friend Sub CutLine()
            Try
                If pIsReadOnly OrElse pLineCount = 0 Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Begin undo group for the entire operation
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Get the current line text including line ending
                Dim lLineText As String = pSourceFileInfo.TextLines(pCursorLine)
                Dim lIncludesNewLine As Boolean = (pCursorLine < pLineCount - 1)
                
                If lIncludesNewLine Then
                    ' Not the last line, include the line ending
                    lLineText &= Environment.NewLine
                End If
                
                ' Copy line text to clipboard
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                lClipboard.Text = lLineText
                
                ' Also copy to primary selection (X11 style)
                Dim lPrimary As Clipboard = Clipboard.Get(Gdk.Selection.Primary)
                lPrimary.Text = lLineText
                
                ' Handle deletion based on line count
                If pLineCount > 1 Then
                    ' Multiple lines - remove this line entirely
                    
                    ' Record the deletion for undo
                    If pUndoRedoManager IsNot Nothing Then
                        ' Calculate positions for undo recording
                        Dim lStartPos As New EditorPosition(pCursorLine, 0)
                        Dim lEndPos As EditorPosition
                        
                        If lIncludesNewLine Then
                            ' Deleting up to start of next line
                            lEndPos = New EditorPosition(pCursorLine + 1, 0)
                        Else
                            ' Last line - delete to end of line
                            lEndPos = New EditorPosition(pCursorLine, lLineText.Length)
                        End If
                        
                        ' Determine where cursor will be after deletion
                        Dim lNewCursorPos As EditorPosition
                        If pCursorLine >= pLineCount - 1 Then
                            ' Deleting last line - cursor moves to start of new last line
                            lNewCursorPos = New EditorPosition(pLineCount - 2, 0)
                        Else
                            ' Cursor stays at same line number (next line moves up)
                            lNewCursorPos = New EditorPosition(pCursorLine, 0)
                        End If
                        
                        ' Record the delete operation
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lLineText, lNewCursorPos)
                    End If
                    
                    ' Delete the line through SourceFileInfo
                    pSourceFileInfo.DeleteLine(pCursorLine)
                    
                    ' Update line count after deletion
                    
                    ' Adjust cursor position
                    If pCursorLine >= pLineCount Then
                        ' Was on last line, move to new last line
                        SetCursorPosition(pLineCount - 1, 0)
                    Else
                        ' Stay on same line number (which now has the next line's content)
                        SetCursorPosition(pCursorLine, 0)
                    End If
                    
                Else
                    ' Only one line - just clear it (don't remove the line itself)
                    
                    ' Record as delete text, not delete line
                    If pUndoRedoManager IsNot Nothing Then
                        Dim lStartPos As New EditorPosition(0, 0)
                        Dim lEndPos As New EditorPosition(0, lLineText.Length)
                        Dim lCursorPos As New EditorPosition(0, 0)
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lLineText, lCursorPos)
                    End If
                    
                    ' Clear the line through SourceFileInfo
                    pSourceFileInfo.TextLines(0) = ""
                    
                    ' Set cursor to beginning of cleared line
                    SetCursorPosition(0, 0)
                End If
                
                ' Clear any selection
                ClearSelection()
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Request async parse for the affected area
                pSourceFileInfo.RequestAsyncParse()
                
                ' Update UI elements
                IsModified = True
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
                ' Queue redraw - colors will update when parse completes
                pDrawingArea?.QueueDraw()
                
                ' Raise text changed event
                RaiseEvent TextChanged(Me, New EventArgs())
                
                Console.WriteLine($"CutLine: Cut line {pCursorLine + 1} to clipboard")
                
            Catch ex As Exception
                Console.WriteLine($"CutLine error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                
                ' End undo group even on error to prevent corruption
                If pUndoRedoManager IsNot Nothing Then
                    Try
                        pUndoRedoManager.EndUserAction()
                    Catch
                        ' Ignore errors ending group
                    End Try
                End If
            End Try
        End Sub

        ''' <summary>
        ''' Inserts text at the specified position using EditorPosition parameter
        ''' </summary>
        ''' <param name="vPosition">The position where text should be inserted</param>
        ''' <param name="vText">The text to insert</param>
        ''' <remarks>
        ''' This method is used by UndoRedoManager during undo/redo operations.
        ''' It properly handles multi-line insertions and updates all state.
        ''' </remarks>
        Public Sub InsertTextAtPosition(vPosition As EditorPosition, vText As String) Implements IEditor.InsertTextAtPosition
            Try
                If pIsReadOnly OrElse String.IsNullOrEmpty(vText) Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Validate position
                Dim lLine As Integer = Math.Max(0, Math.Min(vPosition.Line, pLineCount - 1))
                Dim lColumn As Integer = Math.Max(0, vPosition.Column)
                
                ' For insertions beyond current line length, pad with spaces
                If lLine < pLineCount Then
                    Dim lLineText As String = pSourceFileInfo.TextLines(lLine)
                    If lColumn > lLineText.Length Then
                        ' Pad the line to reach the insertion point
                        Dim lPadding As String = New String(" "c, lColumn - lLineText.Length)
                        pSourceFileInfo.InsertTextInLine(lLine, lLineText.Length, lPadding)
                    End If
                End If
                
                ' Set cursor to insertion position
                SetCursorPosition(lLine, lColumn)
                
                ' Check if this is being called by undo/redo
                Dim lIsUndoRedo As Boolean = pUndoRedoManager IsNot Nothing AndAlso pUndoRedoManager.IsUndoingOrRedoing
                
                ' If not undo/redo, record for undo
                If Not lIsUndoRedo AndAlso pUndoRedoManager IsNot Nothing Then
                    ' Calculate end position after insertion
                    Dim lNewCursorPos As EditorPosition
                    Dim lLines() As String = vText.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                    
                    If lLines.Length = 1 Then
                        lNewCursorPos = New EditorPosition(lLine, lColumn + vText.Length)
                    Else
                        lNewCursorPos = New EditorPosition(lLine + lLines.Length - 1, lLines(lLines.Length - 1).Length)
                    End If
                    
                    pUndoRedoManager.RecordInsertText(vPosition, vText, lNewCursorPos)
                End If
                
                ' Use InsertTextDirect for the actual insertion
                InsertTextDirect(vText)
                
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
                pLineNumberWidget?.QueueDraw()
                
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
        ''' Directly inserts text at the current cursor position without recording undo
        ''' </summary>
        ''' <param name="vText">Text to insert (can be multi-line)</param>
        ''' <remarks>
        ''' This is used by paste operations and undo/redo.
        ''' It must NOT record undo actions to avoid infinite loops.
        ''' Enhanced to ensure proper syntax coloring for large pastes.
        ''' </remarks>
        Friend Sub InsertTextDirect(vText As String)
            Try
                If pSourceFileInfo Is Nothing Then Return
                
                ' Track if this is a large insertion for special handling
                Dim lIsLargeInsertion As Boolean = vText.Length > 500
                Dim lStartLine As Integer = pCursorLine
                
                ' Check if text contains newlines
                If vText.Contains(Environment.NewLine) OrElse vText.Contains(vbLf) OrElse vText.Contains(vbCr) Then
                    ' Multi-line insertion
                    pSourceFileInfo.InsertMultiLineText(pCursorLine, pCursorColumn, vText)
                    
                    ' Calculate new cursor position
                    Dim lLines() As String = vText.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                    Dim lEndLine As Integer = pCursorLine + lLines.Length - 1
                    Dim lEndColumn As Integer
                    
                    If lLines.Length = 1 Then
                        lEndColumn = pCursorColumn + lLines(0).Length
                    Else
                        ' For multi-line, cursor goes to end of last line
                        lEndColumn = lLines(lLines.Length - 1).Length
                    End If
                    
                    ' Update cursor
                    SetCursorPosition(lEndLine, lEndColumn)
                    
                    ' Log for debugging large pastes
                    If lIsLargeInsertion Then
                        Console.WriteLine($"InsertTextDirect: Large multi-line insertion ({lLines.Length} lines, {vText.Length} chars)")
                        Console.WriteLine($"  Lines affected: {lStartLine} to {lEndLine}")
                    End If
                    
                Else
                    ' Single line insertion
                    pSourceFileInfo.InsertTextInLine(pCursorLine, pCursorColumn, vText)
                    
                    ' Update cursor
                    SetCursorPosition(pCursorLine, pCursorColumn + vText.Length)
                    
                    ' Log for debugging large pastes
                    If lIsLargeInsertion Then
                        Console.WriteLine($"InsertTextDirect: Large single-line insertion ({vText.Length} chars)")
                    End If
                End If
                
                ' Request async parse for syntax coloring
                pSourceFileInfo.RequestAsyncParse()
                
                ' CRITICAL FIX: For large insertions, ensure immediate visual feedback
                ' The async parse may take time, so we need to ensure the text is visible
                If lIsLargeInsertion Then
                    Console.WriteLine("InsertTextDirect: Large insertion detected, ensuring immediate display")
                    
                    ' Force a redraw to show the text immediately (even without colors)
                    pDrawingArea?.QueueDraw()
                    
                    ' Schedule a forced recolorization after a short delay
                    ' This gives the async parse a chance to start but ensures coloring happens
                    Gtk.Application.Invoke(Sub()
                        Try
                            System.Threading.Thread.Sleep(100) ' Small delay
                            Console.WriteLine("InsertTextDirect: Forcing recolorization after large paste")
                            ForceRecolorization()
                            pDrawingArea?.QueueDraw()
                        Catch ex As Exception
                            Console.WriteLine($"InsertTextDirect delayed recolor error: {ex.Message}")
                        End Try
                    End Sub)
                End If
                
                ' Mark as modified
                IsModified = True
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
                ' Raise events
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextDirect error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub

        ''' <summary>
        ''' Helper to insert text within a single line
        ''' </summary>
        Private Sub InsertTextInLine(vLine As Integer, vColumn As Integer, vText As String)
            Try
                pSourceFileInfo.InsertTextInLine(vLine, vColumn, vText)
            Catch ex As Exception
                Console.WriteLine($"InsertTextInLine error: {ex.Message}")
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

        ' ===== Text Change Hooks for Parsing =====
        
        ''' <summary>
        ''' Called when text is modified to trigger parsing and update state
        ''' </summary>
        ''' <remarks>
        ''' Updated to work with the new architecture where SourceFileInfo handles parsing requests
        ''' </remarks>
        Private Sub OnTextModified()
            Try
                ' Mark as modified
                IsModified = True
                
               
                ' Update UI elements if needed
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
                ' The SourceFileInfo should have already requested async parsing
                ' when we called its text manipulation methods (InsertTextInLine, DeleteTextInLine, etc.)
                ' So we don't need to explicitly request parsing here
                
                ' Raise text changed event
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
            Catch ex As Exception
                Console.WriteLine($"OnTextModified error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Insert a character at the cursor position
        ''' </summary>
        ''' <param name="vChar">Character to insert</param>
        Public Sub InsertCharacter(vChar As Char)
            Try
                ' Handle selection replacement first
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Insert the character using our internal implementation
                InsertCharacterAt(pCursorLine, pCursorColumn, vChar)
                
                ' Move cursor forward
                SetCursorPosition(pCursorLine, pCursorColumn + 1)
                
                ' Trigger text modified
                OnTextModified()
                
            Catch ex As Exception
                Console.WriteLine($"InsertCharacter error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Delete the character at the cursor position
        ''' </summary>
        Public Sub DeleteCharacter()
            Try
                ' Handle selection deletion first
                If pHasSelection Then
                    DeleteSelection()
                Else
                    ' Delete character at cursor position
                    DeleteCharacterAt(pCursorLine, pCursorColumn)
                End If
                
                ' Trigger text modified
                OnTextModified()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteCharacter error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Insert text at the cursor position
        ''' </summary>
        ''' <param name="vText">Text to insert</param>
        ''' <remarks>
        ''' Updated to handle multi-line text and use SourceFileInfo for all text manipulation
        ''' </remarks>
        Public Sub InsertText(vText As String) Implements IEditor.InsertText
            Try
                If String.IsNullOrEmpty(vText) OrElse pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Handle selection replacement first
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Check if text contains newlines
                If vText.Contains(Environment.NewLine) OrElse vText.Contains(vbLf) OrElse vText.Contains(vbCr) Then
                    ' Multi-line insertion
                    Dim lLines() As String = vText.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                    
                    ' Get current line
                    Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    
                    ' Split current line at cursor position
                    Dim lBeforeCursor As String = If(pCursorColumn > 0, lCurrentLine.Substring(0, Math.Min(pCursorColumn, lCurrentLine.Length)), "")
                    Dim lAfterCursor As String = If(pCursorColumn < lCurrentLine.Length, lCurrentLine.Substring(pCursorColumn), "")
                    
                    ' Update first line with text before cursor + first line of inserted text
                    pSourceFileInfo.UpdateTextLine(pCursorLine, lBeforeCursor & lLines(0))
                    
                    ' Insert middle lines
                    for i As Integer = 1 To lLines.Length - 2
                        pSourceFileInfo.InsertLine(pCursorLine + i, lLines(i))
                    Next
                    
                    ' Insert last line with remaining text + text after cursor
                    If lLines.Length > 1 Then
                        pSourceFileInfo.InsertLine(pCursorLine + lLines.Length - 1, lLines(lLines.Length - 1) & lAfterCursor)
                    End If
                    
                    ' Update cursor position to end of inserted text
                    Dim lNewLine As Integer = pCursorLine + lLines.Length - 1
                    Dim lNewColumn As Integer = lLines(lLines.Length - 1).Length
                    SetCursorPosition(lNewLine, lNewColumn)
                    
                    
                Else
                    ' Single line insertion
                    pSourceFileInfo.InsertTextInLine(pCursorLine, pCursorColumn, vText)
                    pSourceFileInfo.UpdateTextLineWithCaseCorrection(pCursorLine)
                    
                    ' Move cursor forward
                    SetCursorPosition(pCursorLine, pCursorColumn + vText.Length)
                End If
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Request async parsing
                pSourceFileInfo.RequestAsyncParse()
                
                ' Mark as modified
                pIsModified = True
                
                ' Trigger text modified event
                OnTextModified()
                
                ' Queue redraw to show changes
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertText error: {ex.Message}")
                
                ' Try to end undo group on error
                If pUndoRedoManager IsNot Nothing Then
                    Try
                        pUndoRedoManager.EndUserAction()
                    Catch
                        ' Ignore
                    End Try
                End If
            End Try
        End Sub

        ''' <summary>
        ''' Paste text from clipboard at cursor position
        ''' </summary>
        Public Sub Paste() Implements IEditor.Paste
            Try
                ' Get clipboard text and handle through OnClipboardTextReceived
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                lClipboard.RequestText(AddressOf OnClipboardTextReceived)
                
                ' Note: OnClipboardTextReceived will call OnTextModified
                
            Catch ex As Exception
                Console.WriteLine($"Paste error: {ex.Message}")
            End Try
        End Sub


        
    End Class
    
End Namespace
