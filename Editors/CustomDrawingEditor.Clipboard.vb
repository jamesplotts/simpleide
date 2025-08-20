Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor 

 
        ' ===== Clipboard Operations =====
        
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
        
        Private Sub OnPrimaryClipboardReceived(vClipboard As Clipboard, vText As String)
            Try
                ' Same as regular paste but from primary selection
                OnClipboardTextReceived(vClipboard, vText)
                
            Catch ex As Exception
                Console.WriteLine($"OnPrimaryClipboardReceived error: {ex.Message}")
            End Try
        End Sub

        ' ===== SelectAll Implementation =====
'        Public Sub SelectAll() Implements IEditor.SelectAll
'            Try
'                If pLineCount > 0 Then
'                    pSelectionStartLine = 0
'                    pSelectionStartColumn = 0
'                    pSelectionEndLine = pLineCount - 1
'                    pSelectionEndColumn = pTextLines(pLineCount - 1).Length
'                    pSelectionActive = True
'                    
'                    RaiseEvent SelectionChanged(True)
'                    'SetCursorPosition(pSelectionEndLine, pSelectionEndColumn)
'                    'EnsureCursorVisible()
'                    pDrawingArea.QueueDraw()
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"SelectAll error: {ex.Message}")
'            End Try
'        End Sub
        
'        ' ===== Delete Implementation =====
'        Public Sub Delete() Implements IEditor.Delete
'            Try
'                If pIsReadOnly Then Return
'                
'                If pSelectionActive Then
'                    ' Delete selection
'                    DeleteSelection()
'                ElseIf pCursorColumn < pTextLines(pCursorLine).Length Then
'                    ' Delete character at cursor
'                    DeleteCharacterAt(pCursorLine, pCursorColumn)
'                ElseIf pCursorLine < pLineCount - 1 Then
'                    ' Join with next line
'                    JoinLines(pCursorLine)
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"Delete error: {ex.Message}")
'            End Try
'        End Sub


        ' Appends the next line of text to the cursor's line and deletes the next line from the array of lines. 
'        Public Sub JoinLines(vCursorLine as Integer)
'            ' TODO: Implement CustomDrawingEditor.JoinLines
'        End Sub
        
        ' ===== Selection Operations Implementation =====
        
'        Public Sub SetSelection(vStartLine As Integer, vStartColumn As Integer, 
'                                vEndLine As Integer, vEndColumn As Integer) Implements IEditor.SetSelection
'            Try
'                ' Validate and clamp ranges
'                vStartLine = Math.Max(0, Math.Min(vStartLine, pLineCount - 1))
'                vEndLine = Math.Max(0, Math.Min(vEndLine, pLineCount - 1))
'                
'                If vStartLine < pLineCount Then
'                    vStartColumn = Math.Max(0, Math.Min(vStartColumn, pTextLines(vStartLine).Length))
'                End If
'                
'                If vEndLine < pLineCount Then
'                    vEndColumn = Math.Max(0, Math.Min(vEndColumn, pTextLines(vEndLine).Length))
'                End If
'                
'                ' Set selection
'                pSelectionStartLine = vStartLine
'                pSelectionStartColumn = vStartColumn
'                pSelectionEndLine = vEndLine
'                pSelectionEndColumn = vEndColumn
'                pHasSelection = True
'                
'                ' Move cursor to end of selection
'                SetCursorPosition(vEndLine, vEndColumn)
'                
'                ' Raise event and redraw
'                RaiseEvent SelectionChanged(True)
'                pDrawingArea.QueueDraw()
'                
'            Catch ex As Exception
'                Console.WriteLine($"SetSelection error: {ex.Message}")
'            End Try
'        End Sub
        
'        Public Sub SelectLine(vLine As Integer) Implements IEditor.SelectLine
'            Try
'                If vLine < 0 OrElse vLine >= pLineCount Then Return
'                
'                ' Select entire line including newline
'                StartSelection(vLine, 0)
'                
'                If vLine < pLineCount - 1 Then
'                    ' Not last line - select to start of next line
'                    UpdateSelection(vLine + 1, 0)
'                    SetCursorPosition(vLine + 1, 0)
'                Else
'                    ' Last line - select to end
'                    Dim lLength As Integer = pTextLines(vLine).Length
'                    UpdateSelection(vLine, lLength)
'                    SetCursorPosition(vLine, lLength)
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"SelectLine error: {ex.Message}")
'            End Try
'        End Sub
        
'        Public Sub SelectLines(vStartLine As Integer, vEndLine As Integer) Implements IEditor.SelectLines
'            Try
'                ' Validate range
'                vStartLine = Math.Max(0, Math.Min(vStartLine, pLineCount - 1))
'                vEndLine = Math.Max(0, Math.Min(vEndLine, pLineCount - 1))
'                
'                ' Ensure proper order
'                If vStartLine > vEndLine Then
'                    Dim lTemp As Integer = vStartLine
'                    vStartLine = vEndLine
'                    vEndLine = lTemp
'                End If
'                
'                ' Select from start of first line
'                StartSelection(vStartLine, 0)
'                
'                If vEndLine < pLineCount - 1 Then
'                    ' Not including last line - select to start of line after end
'                    UpdateSelection(vEndLine + 1, 0)
'                    SetCursorPosition(vEndLine + 1, 0)
'                Else
'                    ' Including last line - select to end of last line
'                    Dim lLength As Integer = pTextLines(vEndLine).Length
'                    UpdateSelection(vEndLine, lLength)
'                    SetCursorPosition(vEndLine, lLength)
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"SelectLines error: {ex.Message}")
'            End Try
'        End Sub
        
        ''' <summary>
        ''' Deletes the currently selected text
        ''' </summary>
        Private Sub DeleteSelection()
            Try
                If Not pHasSelection OrElse pIsReadOnly Then Return
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordDelete(GetSelectedText(), 
                                                  GetSelectionStartOffset(), 
                                                  GetSelectionEndOffset())
                End If
                
                ' Normalize selection
                Dim lStartLine As Integer = pSelectionStartLine
                Dim lStartColumn As Integer = pSelectionStartColumn
                Dim lEndLine As Integer = pSelectionEndLine
                Dim lEndColumn As Integer = pSelectionEndColumn
                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                
                If lStartLine = lEndLine Then
                    ' Single line deletion
                    If lStartLine < pLineCount Then
                        Dim lLine As String = pTextLines(lStartLine)
                        If lStartColumn < lLine.Length Then
                            pTextLines(lStartLine) = lLine.Remove(lStartColumn, Math.Min(lEndColumn - lStartColumn, lLine.Length - lStartColumn))
                            pLineMetadata(lStartLine).MarkChanged()
                        End If
                    End If
                Else
                    ' Multi-line deletion
                    ' Combine first and last line
                    Dim lNewLine As String = ""
                    If lStartLine < pLineCount Then
                        lNewLine = pTextLines(lStartLine).Substring(0, Math.Min(lStartColumn, pTextLines(lStartLine).Length))
                    End If
                    If lEndLine < pLineCount AndAlso lEndColumn < pTextLines(lEndLine).Length Then
                        lNewLine &= pTextLines(lEndLine).Substring(lEndColumn)
                    End If
                    
                    ' Set the combined line
                    If lStartLine < pLineCount Then
                        pTextLines(lStartLine) = lNewLine
                        pLineMetadata(lStartLine).MarkChanged()
                    End If
                    
                    ' Remove lines in between
                    Dim lLinesToRemove As Integer = lEndLine - lStartLine
                    If lLinesToRemove > 0 Then
                        RemoveLines(lStartLine + 1, lLinesToRemove)
                    End If
                End If
                
                ' Clear selection and move cursor
                ClearSelection()
                SetCursorPosition(lStartLine, lStartColumn)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea.QueueDraw()
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"DeleteSelection error: {ex.Message}")
            End Try
        End Sub
        
'        Private Function GetSelectionStartOffset() As Integer
'            Try
'                Dim lStartLine As Integer = pSelectionStartLine
'                Dim lStartColumn As Integer = pSelectionStartColumn
'                Dim lEndLine As Integer = pSelectionEndLine
'                Dim lEndColumn As Integer = pSelectionEndColumn
'                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
'                
'                Return GetOffsetFromPosition(lStartLine, lStartColumn)
'                
'            Catch ex As Exception
'                Console.WriteLine($"GetSelectionStartOffset error: {ex.Message}")
'                Return 0
'            End Try
'        End Function
        
'        Private Function GetSelectionEndOffset() As Integer
'            Try
'                Dim lStartLine As Integer = pSelectionStartLine
'                Dim lStartColumn As Integer = pSelectionStartColumn
'                Dim lEndLine As Integer = pSelectionEndLine
'                Dim lEndColumn As Integer = pSelectionEndColumn
'                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
'                
'                Return GetOffsetFromPosition(lEndLine, lEndColumn)
'                
'            Catch ex As Exception
'                Console.WriteLine($"GetSelectionEndOffset error: {ex.Message}")
'                Return 0
'            End Try
'        End Function
        
'        Private Function GetOffsetFromPosition(vLine As Integer, vColumn As Integer) As Integer
'            Try
'                Dim lOffset As Integer = 0
'                
'                ' Add lengths of all previous lines
'                For i As Integer = 0 To Math.Min(vLine - 1, pLineCount - 1)
'                    lOffset += pTextLines(i).Length + Environment.NewLine.Length
'                Next
'                
'                ' Add column offset in current line
'                If vLine < pLineCount Then
'                    lOffset += Math.Min(vColumn, pTextLines(vLine).Length)
'                End If
'                
'                Return lOffset
'                
'            Catch ex As Exception
'                Console.WriteLine($"GetOffsetFromPosition error: {ex.Message}")
'                Return 0
'            End Try
'        End Function
        
    End Class
    
End Namespace
