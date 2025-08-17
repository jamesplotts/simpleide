' Editors/CustomDrawingEditor.Helpers.vb - Helper methods and utilities
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Update Line Number Width =====
        Private Sub UpdateLineNumberWidth()
            Try
                If pFontMetrics IsNot Nothing Then
                    pLineNumberWidth = pFontMetrics.CalculateLineNumberWidth(pLineCount, pLeftPadding)
                    
                    ' Apply the new width to the line number area
                    If pLineNumberArea IsNot Nothing Then
                        pLineNumberArea.WidthRequest = pLineNumberWidth
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateLineNumberWidth error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Join Lines =====
        Public Sub JoinLines(vLine As Integer)
            Try
                If pIsReadOnly Then Return
                If vLine < 0 OrElse vLine >= pLineCount - 1 Then Return
                
                ' Get the two lines to join
                Dim lCurrentLine As String = pTextLines(vLine)
                Dim lNextLine As String = pTextLines(vLine + 1)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                    pUndoRedoManager.RecordReplaceText(vLine, 0, lCurrentLine, lCurrentLine & lNextLine)
                    pUndoRedoManager.RecordDeleteLine(vLine + 1, lNextLine, vLine, lCurrentLine.Length)
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Join the lines
                pTextLines(vLine) = lCurrentLine & lNextLine
                pLineMetadata(vLine).MarkChanged()
                
                ' Remove the next line
                RemoveLines(vLine + 1, 1)
                
                ' Update cursor position if needed
                If pCursorLine > vLine + 1 Then
                    SetCursorPosition(pCursorLine - 1, pCursorColumn)
                ElseIf pCursorLine = vLine + 1 Then
                    SetCursorPosition(vLine, lCurrentLine.Length + pCursorColumn)
                End If
                ScheduleFullDocumentParse()
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"JoinLines error: {ex.Message}")
            End Try
        End Sub
        

        
        ' ===== Delete Implementation =====
        Public Sub Delete() Implements IEditor.Delete
            Try
                If pIsReadOnly Then Return
                
                If pSelectionActive AndAlso pHasSelection Then
                    ' Delete selection
                    DeleteSelection()
                ElseIf pCursorColumn < pTextLines(pCursorLine).Length Then
                    ' Delete character at cursor
                    DeleteCharacterAt(pCursorLine, pCursorColumn)
                ElseIf pCursorLine < pLineCount - 1 Then
                    ' Join with next line
                    JoinLines(pCursorLine)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Delete error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Get Selection Offset Helpers =====
        Private Function GetSelectionStartOffset() As Integer
            Try
                Dim lStartLine As Integer = pSelectionStartLine
                Dim lStartColumn As Integer = pSelectionStartColumn
                Dim lEndLine As Integer = pSelectionEndLine
                Dim lEndColumn As Integer = pSelectionEndColumn
                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                
                Return GetOffsetFromPosition(lStartLine, lStartColumn)
                
            Catch ex As Exception
                Console.WriteLine($"GetSelectionStartOffset error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        Private Function GetSelectionEndOffset() As Integer
            Try
                Dim lStartLine As Integer = pSelectionStartLine
                Dim lStartColumn As Integer = pSelectionStartColumn
                Dim lEndLine As Integer = pSelectionEndLine
                Dim lEndColumn As Integer = pSelectionEndColumn
                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                
                Return GetOffsetFromPosition(lEndLine, lEndColumn)
                
            Catch ex As Exception
                Console.WriteLine($"GetSelectionEndOffset error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        Private Function GetOffsetFromPosition(vLine As Integer, vColumn As Integer) As Integer
            Try
                Dim lOffset As Integer = 0
                
                ' Add lengths of all previous lines
                For i As Integer = 0 To Math.Min(vLine - 1, pLineCount - 1)
                    lOffset += pTextLines(i).Length + Environment.NewLine.Length
                Next
                
                ' Add column offset in current line
                If vLine < pLineCount Then
                    lOffset += Math.Min(vColumn, pTextLines(vLine).Length)
                End If
                
                Return lOffset
                
            Catch ex As Exception
                Console.WriteLine($"GetOffsetFromPosition error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ' ===== Navigation Implementations =====
        
        
        
        
        

        
    End Class
    
End Namespace
