' Editors/CustomDrawingEditor.Clipboard.vb - Clipboard operations
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor 
        
        ' ===== Clipboard Operations =====
        
        ''' <summary>
        ''' Cuts the selected text to the clipboard
        ''' </summary>
        Public Sub Cut() Implements IEditor.Cut
            Try
                If Not pHasSelection OrElse pIsReadOnly Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Copy to clipboard
                Copy()
                
                ' Delete selected text
                DeleteSelection()
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Cut error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Copies the selected text to the clipboard
        ''' </summary>
        Public Sub Copy() Implements IEditor.Copy
            Try
                If Not pHasSelection Then Return
                
                Dim lSelectedText As String = GetSelectedText()
                If Not String.IsNullOrEmpty(lSelectedText) Then
                    ' Copy to both clipboards
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                    lClipboard.Text = lSelectedText
                    
                    ' Also copy to primary selection (X11 style)
                    Dim lPrimaryClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Primary)
                    lPrimaryClipboard.Text = lSelectedText
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Copy error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Pastes text from the clipboard
        ''' </summary>
        Public Sub Paste() Implements IEditor.Paste
            Try
                If pIsReadOnly Then Return
                
                ' Request clipboard text
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                lClipboard.RequestText(AddressOf OnClipboardTextReceived)
                
            Catch ex As Exception
                Console.WriteLine($"Paste error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles clipboard text when received
        ''' </summary>
        Private Sub OnClipboardTextReceived(vClipboard As Clipboard, vText As String)
            Try
                If String.IsNullOrEmpty(vText) OrElse pIsReadOnly Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Remember the starting line for syntax highlighting
                Dim lStartLine As Integer = pCursorLine
                
                ' Delete selection if any
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Insert text at cursor
                InsertText(vText)
                
                ' Calculate the ending line after insertion
                Dim lEndLine As Integer = pCursorLine
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Apply syntax highlighting immediately to all affected lines
                If pHighlightingEnabled Then
                    For i As Integer = lStartLine To Math.Min(lEndLine, pLineCount - 1)
                        ProcessLineFormatting(i)
                    Next
                    
                    ' Queue redraw to show the highlighting
                    pDrawingArea?.QueueDraw()
                End If
                
                ' Schedule full document parse for structure updates
                If vText.IndexOf(Environment.NewLine) >= 0 Then 
                    ScheduleFullDocumentParse()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnClipboardTextReceived error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles primary clipboard text when received (middle-click paste)
        ''' </summary>
        Private Sub OnPrimaryClipboardReceived(vClipboard As Clipboard, vText As String)
            Try
                ' Same as regular paste but from primary selection
                OnClipboardTextReceived(vClipboard, vText)
                
            Catch ex As Exception
                Console.WriteLine($"OnPrimaryClipboardReceived error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Selection Deletion =====

        ''' <summary>
        ''' Deletes the currently selected text
        ''' </summary>
        ''' <remarks>
        ''' This method handles both single-line and multi-line selections,
        ''' records the operation for undo, and updates all necessary UI elements
        ''' </remarks>
        Public Sub DeleteSelection() Implements IEditor.DeleteSelection
            Try
                ' Exit if no selection or read-only
                If Not pHasSelection OrElse pIsReadOnly Then Return
                
                ' Get the selected text BEFORE deleting it for undo recording
                Dim lSelectedText As String = GetSelectedText()
                If String.IsNullOrEmpty(lSelectedText) Then
                    ClearSelection()
                    Return
                End If
                
                ' Get and normalize selection bounds
                Dim lStartPos As New EditorPosition(pSelectionStartLine, pSelectionStartColumn)
                Dim lEndPos As New EditorPosition(pSelectionEndLine, pSelectionEndColumn)
                NormalizeSelection(lStartPos, lEndPos)
                
                Dim lStartLine As Integer = lStartPos.Line
                Dim lStartColumn As Integer = lStartPos.Column
                Dim lEndLine As Integer = lEndPos.Line
                Dim lEndColumn As Integer = lEndPos.Column
                
                ' Record the deletion for undo (before making changes)
                If pUndoRedoManager IsNot Nothing Then
                    Dim lCursorPos As New EditorPosition(lStartLine, lStartColumn)
                    pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lSelectedText, lCursorPos)
                End If
                
                ' Perform the deletion
                If lStartLine = lEndLine Then
                    ' ===== Single Line Deletion =====
                    DeleteSingleLineSelection(lStartLine, lStartColumn, lEndColumn)
                Else
                    ' ===== Multi-Line Deletion =====
                    DeleteMultiLineSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                End If
                
                ' Clear the selection
                ClearSelection()
                
                ' Set cursor to the start of where the selection was
                SetCursorPosition(lStartLine, lStartColumn)
                
                ' Mark document as modified
                IsModified = True
                
                ' Update line numbers if line count changed
                If lStartLine <> lEndLine Then
                    UpdateLineNumberWidth()
                End If
                
                ' Update scrollbars
                UpdateScrollbars()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
                ' Raise text changed event
                RaiseEvent TextChanged(Me, New EventArgs())
                
                ' Trigger syntax highlighting for affected lines
                If pHighlightingEnabled Then
                    ProcessLineFormatting(lStartLine)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to delete text within a single line
        ''' </summary>
        Private Sub DeleteSingleLineSelection(vLine As Integer, vStartColumn As Integer, vEndColumn As Integer)
            Try
                If vLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(vLine)
                
                ' Ensure columns are within bounds
                vStartColumn = Math.Max(0, Math.Min(vStartColumn, lLine.Length))
                vEndColumn = Math.Max(vStartColumn, Math.Min(vEndColumn, lLine.Length))
                
                ' Calculate the text to remove
                Dim lLengthToRemove As Integer = vEndColumn - vStartColumn
                
                If lLengthToRemove > 0 Then
                    ' Remove the selected portion
                    pTextLines(vLine) = lLine.Remove(vStartColumn, lLengthToRemove)
                    
                    ' Mark line as changed
                    pLineMetadata(vLine).MarkChanged()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteSingleLineSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to delete text spanning multiple lines
        ''' </summary>
        Private Sub DeleteMultiLineSelection(vStartLine As Integer, vStartColumn As Integer, 
                                            vEndLine As Integer, vEndColumn As Integer)
            Try
                ' Validate line indices
                If vStartLine >= pLineCount OrElse vEndLine >= pLineCount Then Return
                
                ' Get the text to keep from first line (before selection)
                Dim lFirstLine As String = pTextLines(vStartLine)
                Dim lKeepFromFirst As String = ""
                If vStartColumn > 0 AndAlso vStartColumn <= lFirstLine.Length Then
                    lKeepFromFirst = lFirstLine.Substring(0, vStartColumn)
                End If
                
                ' Get the text to keep from last line (after selection)
                Dim lLastLine As String = pTextLines(vEndLine)
                Dim lKeepFromLast As String = ""
                If vEndColumn < lLastLine.Length Then
                    lKeepFromLast = lLastLine.Substring(vEndColumn)
                End If
                
                ' Combine the kept portions
                pTextLines(vStartLine) = lKeepFromFirst & lKeepFromLast
                pLineMetadata(vStartLine).MarkChanged()
                
                ' Remove all lines between start and end (including end line)
                Dim lLinesToRemove As Integer = vEndLine - vStartLine
                If lLinesToRemove > 0 Then
                    For i As Integer = 0 To lLinesToRemove - 1
                        ' Always remove at vStartLine + 1 because the list shifts
                        pTextLines.RemoveAt(vStartLine + 1)
                        RemoveLineMetadata(vStartLine + 1)
                    Next
                    
                    ' Update line count
                    pLineCount = pTextLines.Count
                End If
                
                ' Ensure we always have at least one line
                If pLineCount = 0 Then
                    pTextLines.Add("")
                    ReDim Preserve pLineMetadata(0)
                    ReDim Preserve pCharacterColors(0)
                    pLineMetadata(0) = New LineMetadata()
                    pCharacterColors(0) = New CharacterColorInfo() {}
                    pLineCount = 1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteMultiLineSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Alternative simpler implementation if you prefer a more compact version
        ''' </summary>
        Public Sub DeleteSelectionSimple() 
            Try
                If Not HasSelection OrElse IsReadOnly Then Return
                
                ' Get selection bounds
                Dim lStart As EditorPosition = SelectionStart
                Dim lEnd As EditorPosition = SelectionEnd
                Dim lSelectedText As String = GetSelectedText()
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordDeleteText(lStart, lEnd, lSelectedText, lStart)
                End If
                
                ' Use ReplaceText to delete (replace with empty string)
                ReplaceText(lStart, lEnd, "")
                
                ' Clear selection and position cursor
                ClearSelection()
                SetCursorPosition(lStart)
                
            Catch ex As Exception
                Console.WriteLine($"DeleteSelectionSimple error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to remove line metadata when deleting lines
        ''' </summary>
        Private Sub RemoveLineMetadata(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pLineMetadata.Length Then Return
                
                ' Create new arrays without the specified line
                Dim lNewMetadata(pLineMetadata.Length - 2) As LineMetadata
                Dim lNewColors(pCharacterColors.Length - 2)() As CharacterColorInfo
                
                ' Copy before the removed line
                For i As Integer = 0 To vLineIndex - 1
                    If i < pLineMetadata.Length Then
                        lNewMetadata(i) = pLineMetadata(i)
                        lNewColors(i) = pCharacterColors(i)
                    End If
                Next
                
                ' Copy after the removed line
                For i As Integer = vLineIndex + 1 To pLineMetadata.Length - 1
                    If i - 1 < lNewMetadata.Length Then
                        lNewMetadata(i - 1) = pLineMetadata(i)
                        lNewColors(i - 1) = pCharacterColors(i)
                    End If
                Next
                
                ' Update the arrays
                pLineMetadata = lNewMetadata
                pCharacterColors = lNewColors
                
            Catch ex As Exception
                Console.WriteLine($"RemoveLineMetadata error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Select All =====
        

        
    End Class
    
End Namespace
