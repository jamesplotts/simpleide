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
        ''' Inserts a character at the specified position using atomic operation
        ''' </summary>
        ''' <param name="vLine">Line number (0-based)</param>
        ''' <param name="vColumn">Column position (0-based)</param>
        ''' <param name="vChar">Character to insert</param>
        ''' <remarks>
        ''' Refactored to use SourceFileInfo.InsertCharacter atomic method
        ''' </remarks>
        Private Sub InsertCharacterAt(vLine As Integer, vColumn As Integer, vChar As Char)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                If pIsReadOnly Then Return
                
                ' Get current line for validation
                Dim lLine As String = pSourceFileInfo.TextLines(vLine)
                
                ' Clamp column to valid range
                vColumn = Math.Max(0, Math.Min(vColumn, lLine.Length))
                
                ' Record for undo BEFORE the operation
                If pUndoRedoManager IsNot Nothing Then
                    Dim lStartPos As New EditorPosition(vLine, vColumn)
                    Dim lEndPos As New EditorPosition(vLine, vColumn + 1)
                    pUndoRedoManager.RecordInsertText(lStartPos, vChar.ToString(), lEndPos)
                End If
                
                ' Use atomic InsertCharacter method
                pSourceFileInfo.InsertCharacter(vLine, vColumn, vChar)
                
                ' Update state after operation
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertCharacterAt error: {ex.Message}")
            End Try
        End Sub     
   
        ''' <summary>
        ''' Deletes a character at the specified position using atomic operation
        ''' </summary>
        ''' <param name="vLine">Line number (0-based)</param>
        ''' <param name="vColumn">Column position (0-based)</param>
        ''' <remarks>
        ''' Refactored to use SourceFileInfo.DeleteCharacter atomic method
        ''' </remarks>
        Private Sub DeleteCharacterAt(vLine As Integer, vColumn As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                If pIsReadOnly Then Return
                
                Dim lLine As String = pSourceFileInfo.TextLines(vLine)
                If vColumn < 0 OrElse vColumn >= lLine.Length Then Return
                
                ' Get the character that will be deleted for undo
                Dim lDeletedChar As Char = lLine(vColumn)
                
                ' Record for undo BEFORE the operation
                If pUndoRedoManager IsNot Nothing Then
                    Dim lStartPos As New EditorPosition(vLine, vColumn)
                    Dim lEndPos As New EditorPosition(vLine, vColumn + 1)
                    Dim lCursorPos As New EditorPosition(vLine, vColumn)
                    pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lDeletedChar.ToString(), lCursorPos)
                End If
                
                ' Use atomic DeleteCharacter method
                pSourceFileInfo.DeleteCharacter(vLine, vColumn)
                
                ' Update state after operation
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteCharacterAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Line Operations =====
        
        ''' <summary>
        ''' Inserts a new line at cursor position using atomic operation
        ''' </summary>
        ''' <remarks>
        ''' Refactored to use SourceFileInfo.InsertText atomic method for newline insertion
        ''' </remarks>
        Private Sub InsertNewLine()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                ' Delete selection first if exists
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    Dim lStartPos As New EditorPosition(pCursorLine, pCursorColumn)
                    ' After newline, cursor will be at start of next line
                    Dim lEndPos As New EditorPosition(pCursorLine + 1, 0)
                    pUndoRedoManager.RecordInsertText(lStartPos, Environment.NewLine, lEndPos)
                End If
                
                ' Use atomic InsertText method for newline
                pSourceFileInfo.InsertText(pCursorLine, pCursorColumn, Environment.NewLine)
                
                ' Move cursor to beginning of next line
                SetCursorPosition(pCursorLine + 1, 0)
                
                ' Apply auto-indent if enabled
                If pSettingsManager IsNot Nothing AndAlso pSettingsManager.AutoIndent Then
                    ApplyAutoIndent(pCursorLine)
                End If
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertNewLine error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Delete Operations =====
        
        ''' <summary>
        ''' Deletes backward from cursor (for compatibility)
        ''' </summary>
        ''' <remarks>
        ''' Now just calls HandleBackspace which uses atomic operations
        ''' </remarks>
        Private Sub DeleteBackward()
            HandleBackspace()
        End Sub
        
        ''' <summary>
        ''' Deletes forward from cursor (for compatibility)
        ''' </summary>
        ''' <remarks>
        ''' Now just calls HandleDelete which uses atomic operations
        ''' </remarks>
        Private Sub DeleteForward()
            HandleDelete()
        End Sub
        
        ' ===== Helper Key Handlers =====
        
        ''' <summary>
        ''' Handles the Return/Enter key press using atomic operation
        ''' </summary>
        ''' <remarks>
        ''' Refactored to use InsertNewLine which uses atomic InsertText
        ''' </remarks>
        Private Sub HandleReturn()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                ' Simply call InsertNewLine which handles everything
                InsertNewLine()
                
            Catch ex As Exception
                Console.WriteLine($"HandleReturn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the Backspace key using atomic operations
        ''' </summary>
        ''' <remarks>
        ''' Refactored to use atomic DeleteCharacter and DeleteText methods
        ''' </remarks>
        Private Sub HandleBackspace()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                If pHasSelection Then
                    ' Delete selection
                    DeleteSelection()
                ElseIf pCursorColumn > 0 Then
                    ' Delete character before cursor
                    
                    ' Get the character being deleted for undo
                    Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    Dim lDeletedChar As Char = lLine(pCursorColumn - 1)
                    
                    ' Record for undo
                    If pUndoRedoManager IsNot Nothing Then
                        Dim lStartPos As New EditorPosition(pCursorLine, pCursorColumn - 1)
                        Dim lEndPos As New EditorPosition(pCursorLine, pCursorColumn)
                        Dim lCursorPos As New EditorPosition(pCursorLine, pCursorColumn - 1)
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lDeletedChar.ToString(), lCursorPos)
                    End If
                    
                    ' Use atomic DeleteCharacter
                    pSourceFileInfo.DeleteCharacter(pCursorLine, pCursorColumn - 1)
                    
                    ' Move cursor back
                    SetCursorPosition(pCursorLine, pCursorColumn - 1)
                    
                ElseIf pCursorLine > 0 Then
                    ' Join with previous line
                    Dim lPrevLine As String = pSourceFileInfo.TextLines(pCursorLine - 1)
                    Dim lPrevLineLength As Integer = lPrevLine.Length
                    Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    
                    ' Record for undo
                    If pUndoRedoManager IsNot Nothing Then
                        Dim lStartPos As New EditorPosition(pCursorLine - 1, lPrevLineLength)
                        Dim lEndPos As New EditorPosition(pCursorLine, 0)
                        Dim lCursorPos As New EditorPosition(pCursorLine - 1, lPrevLineLength)
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, Environment.NewLine, lCursorPos)
                    End If
                    
                    ' Use atomic DeleteText to remove the newline between lines
                    pSourceFileInfo.DeleteText(pCursorLine - 1, lPrevLineLength, pCursorLine, 0)
                    
                    ' Move cursor to join position
                    SetCursorPosition(pCursorLine - 1, lPrevLineLength)
                End If
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleBackspace error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Handles the Delete key using atomic operations
        ''' </summary>
        ''' <remarks>
        ''' Refactored to use atomic DeleteCharacter and DeleteText methods
        ''' </remarks>
        Private Sub HandleDelete()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                If pHasSelection Then
                    ' Delete selection
                    DeleteSelection()
                ElseIf pCursorColumn < pSourceFileInfo.TextLines(pCursorLine).Length Then
                    ' Delete character at cursor
                    
                    ' Get the character being deleted for undo
                    Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    Dim lDeletedChar As Char = lLine(pCursorColumn)
                    
                    ' Record for undo
                    If pUndoRedoManager IsNot Nothing Then
                        Dim lStartPos As New EditorPosition(pCursorLine, pCursorColumn)
                        Dim lEndPos As New EditorPosition(pCursorLine, pCursorColumn + 1)
                        Dim lCursorPos As New EditorPosition(pCursorLine, pCursorColumn)
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lDeletedChar.ToString(), lCursorPos)
                    End If
                    
                    ' Use atomic DeleteCharacter
                    pSourceFileInfo.DeleteCharacter(pCursorLine, pCursorColumn)
                    
                ElseIf pCursorLine < pSourceFileInfo.TextLines.Count - 1 Then
                    ' Join with next line
                    Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                    Dim lNextLine As String = pSourceFileInfo.TextLines(pCursorLine + 1)
                    
                    ' Record for undo
                    If pUndoRedoManager IsNot Nothing Then
                        Dim lStartPos As New EditorPosition(pCursorLine, lCurrentLine.Length)
                        Dim lEndPos As New EditorPosition(pCursorLine + 1, 0)
                        Dim lCursorPos As New EditorPosition(pCursorLine, lCurrentLine.Length)
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, Environment.NewLine, lCursorPos)
                    End If
                    
                    ' Use atomic DeleteText to remove the newline between lines
                    pSourceFileInfo.DeleteText(pCursorLine, lCurrentLine.Length, pCursorLine + 1, 0)
                End If
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
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
        ''' Inserts text at the specified position using atomic operation
        ''' </summary>
        ''' <param name="vPosition">Position to insert at</param>
        ''' <param name="vText">Text to insert (can be multi-line)</param>
        ''' <remarks>
        ''' Refactored to use SourceFileInfo.InsertText atomic method
        ''' </remarks>
        Public Sub InsertTextAtPosition(vPosition As EditorPosition, vText As String) Implements IEditor.InsertTextAtPosition
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing OrElse String.IsNullOrEmpty(vText) Then Return
                
                ' Validate position
                Dim lLine As Integer = Math.Max(0, Math.Min(vPosition.Line, pLineCount - 1))
                Dim lColumn As Integer = Math.Max(0, Math.Min(vPosition.Column, pSourceFileInfo.TextLines(lLine).Length))
                
                ' Calculate end position for undo
                Dim lEndPos As EditorPosition
                Dim lLines() As String = vText.Split({Environment.NewLine, vbLf}, StringSplitOptions.None)
                
                If lLines.Length = 1 Then
                    ' Single line insertion
                    lEndPos = New EditorPosition(lLine, lColumn + vText.Length)
                Else
                    ' Multi-line insertion
                    Dim lLastLineLength As Integer = lLines(lLines.Length - 1).Length
                    lEndPos = New EditorPosition(lLine + lLines.Length - 1, lLastLineLength)
                End If
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordInsertText(New EditorPosition(lLine, lColumn), vText, lEndPos)
                End If
                
                ' Use atomic InsertText method
                pSourceFileInfo.InsertText(lLine, lColumn, vText)
                
                ' Set cursor to end of inserted text
                SetCursorPosition(lEndPos.Line, lEndPos.Column)
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextAtPosition error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Replaces the current selection with the specified text
        ''' </summary>
        ''' <param name="vText">Text to replace selection with</param>
        ''' <remarks>
        ''' Uses atomic operations for both deletion and insertion
        ''' </remarks>
        Public Sub ReplaceSelection(vText As String) Implements IEditor.ReplaceSelection
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                If pHasSelection Then
                    ' Get selection bounds
                    Dim lStart As EditorPosition = GetSelectionStart()
                    Dim lEnd As EditorPosition = GetSelectionEnd()
                    
                    ' Get text being replaced for undo
                    Dim lOldText As String = GetSelectedText()
                    
                    ' Begin undo group for replace operation
                    If pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.BeginUserAction()
                    End If
                    
                    ' Delete selection using atomic operation
                    pSourceFileInfo.DeleteText(lStart.Line, lStart.Column, lEnd.Line, lEnd.Column)
                    
                    ' Insert new text using atomic operation
                    If Not String.IsNullOrEmpty(vText) Then
                        pSourceFileInfo.InsertText(lStart.Line, lStart.Column, vText)
                    End If
                    
                    ' End undo group
                    If pUndoRedoManager IsNot Nothing Then
                        ' Calculate new cursor position
                        Dim lNewCursorPos As EditorPosition
                        If vText.Contains(Environment.NewLine) Then
                            Dim lLines() As String = vText.Split({Environment.NewLine}, StringSplitOptions.None)
                            lNewCursorPos = New EditorPosition(lStart.Line + lLines.Length - 1, 
                                                               lLines(lLines.Length - 1).Length)
                        Else
                            lNewCursorPos = New EditorPosition(lStart.Line, lStart.Column + vText.Length)
                        End If
                        
                        pUndoRedoManager.RecordReplaceText(lStart, lEnd, lOldText, vText, lNewCursorPos)
                        pUndoRedoManager.EndUserAction()
                    End If
                    
                    ' Clear selection
                    ClearSelection()
                    
                    ' Position cursor at end of inserted text
                    If vText.Contains(Environment.NewLine) Then
                        Dim lLines() As String = vText.Split({Environment.NewLine}, StringSplitOptions.None)
                        SetCursorPosition(lStart.Line + lLines.Length - 1, lLines(lLines.Length - 1).Length)
                    Else
                        SetCursorPosition(lStart.Line, lStart.Column + vText.Length)
                    End If
                Else
                    ' No selection, just insert
                    InsertText(vText)
                End If
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
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
                    InsertText(vText)
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
                pSourceFileInfo.InsertText(pCursorLine, pCursorColumn, vText)
                
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
        ''' Helper to insert text within a single line
        ''' </summary>
        Private Sub InsertTextInLine(vLine As Integer, vColumn As Integer, vText As String)
            Try
                pSourceFileInfo.InsertText(vLine, vColumn, vText)
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
        ''' Enhanced to ensure immediate visual feedback and proper update chain
        ''' </remarks>
        Private Sub OnTextModified()
            Try
                ' Mark as modified
                IsModified = True
                
                ' Update UI elements if needed
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
                ' CRITICAL: Ensure immediate redraw for visual feedback
                ' The SourceFileInfo should have already requested async parsing
                ' when we called its text manipulation methods
                
                ' Force immediate redraw to show text changes (even with default colors)
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.QueueDraw()
                End If
                
                ' Also redraw line numbers if visible
                If pLineNumberWidget IsNot Nothing Then
                    pLineNumberWidget.QueueDraw()
                End If
                
                ' Raise text changed event for MainWindow
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Log for debugging
                If pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.NeedsParsing Then
                    Console.WriteLine($"OnTextModified: Text changed, parse requested for {pFilePath}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTextModified error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Insert a character at the cursor position
        ''' </summary>
        ''' <param name="vChar">Character to insert</param>
        ''' <remarks>
        ''' Public method for inserting at cursor, handles selection replacement
        ''' </remarks>
        Public Sub InsertCharacter(vChar As Char)
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                ' Handle selection replacement first
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Insert the character using atomic operation
                InsertCharacterAt(pCursorLine, pCursorColumn, vChar)
                
                ' Move cursor forward
                SetCursorPosition(pCursorLine, pCursorColumn + 1)
                
                ' Ensure cursor visible
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"InsertCharacter error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Delete the character at the cursor position
        ''' </summary>
        ''' <remarks>
        ''' Public method for deleting at cursor, handles selection deletion
        ''' </remarks>
        Public Sub DeleteCharacter()
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                ' Handle selection deletion first
                If pHasSelection Then
                    DeleteSelection()
                Else
                    ' Delete character at cursor position
                    DeleteCharacterAt(pCursorLine, pCursorColumn)
                End If
                
                ' Ensure cursor visible
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteCharacter error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Inserts text at the current cursor position
        ''' </summary>
        ''' <param name="vText">Text to insert</param>
        ''' <remarks>
        ''' Public method that uses InsertTextAtPosition internally
        ''' </remarks>
        Public Sub InsertText(vText As String) Implements IEditor.InsertText
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing OrElse String.IsNullOrEmpty(vText) Then Return
                
                ' Delete selection first if exists
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Insert at cursor position
                InsertTextAtPosition(New EditorPosition(pCursorLine, pCursorColumn), vText)
                
            Catch ex As Exception
                Console.WriteLine($"InsertText error: {ex.Message}")
            End Try
        End Sub


' Replace: SimpleIDE.Editors.CustomDrawingEditor.Paste
''' <summary>
''' Pastes text from clipboard using atomic operations
''' </summary>
''' <remarks>
''' Uses atomic InsertText method to ensure metadata is properly updated
''' </remarks>
Public Sub Paste() Implements IEditor.Paste
    Try
        If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
        
        ' Request clipboard text asynchronously for better UI responsiveness
        Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
        lClipboard.RequestText(AddressOf OnClipboardTextReceived)
        
    Catch ex As Exception
        Console.WriteLine($"Paste error: {ex.Message}")
    End Try
End Sub


        
    End Class
    
End Namespace
