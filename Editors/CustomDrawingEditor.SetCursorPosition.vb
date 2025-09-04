' Editors/CustomDrawingEditor.SetCursorPosition.vb - SetCursorPosition implementation with line tracking
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== SetCursorPosition Implementation =====
        
        ' Set the cursor position to the specified line and column
        Public Sub SetCursorPosition(vLine As Integer, vColumn As Integer) 
            Try
                ' Store previous position for events and line tracking
                Dim lOldLine As Integer = pCursorLine
                Dim lOldColumn As Integer = pCursorColumn
                
                ' Validate and clamp line
                If vLine < 0 Then
                    vLine = 0
                ElseIf vLine >= pLineCount Then
                    vLine = Math.Max(0, pLineCount - 1)
                End If
                
                ' Validate and clamp column based on line length
                If vLine < pLineCount AndAlso TextLines(vLine) IsNot Nothing Then
                    Dim lLineLength As Integer = TextLines(vLine).Length
                    
                    ' Allow cursor at end of line (after last character)
                    If vColumn < 0 Then
                        vColumn = 0
                    ElseIf vColumn > lLineLength Then
                        vColumn = lLineLength
                    End If
                Else
                    vColumn = 0
                End If
                
                ' Update cursor position
                pCursorLine = vLine
                pCursorColumn = vColumn
                
                ' Update desired column for vertical navigation
                ' (unless explicitly preserving it for up/down movement)
                If Not pPreservingDesiredColumn Then
                    pDesiredColumn = vColumn
                End If
                
                ' Stop cursor blink and make it visible
                StopCursorBlink()
                pCursorVisible = True
                
                ' Check if we've changed lines (for line tracking and LineExited event)
                If lOldLine <> pCursorLine Then
                    ' Call OnLineChanged which handles formatting and fires LineExited
                    OnLineChanged(lOldLine, pCursorLine)
                End If
                
                ' Invalidate old cursor position for redraw
                If lOldLine <> pCursorLine OrElse lOldColumn <> pCursorColumn Then
                    ' Invalidate old cursor position
                    InvalidateLine(lOldLine)
                    
                    ' Invalidate new cursor position
                    InvalidateLine(pCursorLine)
                    
                    ' Raise cursor position changed event (1-based for UI)
                    RaiseEvent CursorPositionChanged(pCursorLine + 1, pCursorColumn + 1)
                End If
                
                ' Restart cursor blink
                StartCursorBlink()
                
            Catch ex As Exception
                Console.WriteLine($"SetCursorPosition error: {ex.Message}")
            End Try
        End Sub
        
        ' Public overload for IEditor interface if needed
        Public Sub SetCursorPosition(vPosition As EditorPosition) Implements IEditor.SetCursorPosition
            SetCursorPosition(vPosition.Line, vPosition.Column)
        End Sub
        
        ' ===== Helper Methods for Cursor Management =====
        
        ' Invalidate a specific line for redrawing
        Private Sub InvalidateLine(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Calculate the y position of this line
                Dim lY As Integer = (vLine - pFirstVisibleLine) * pLineHeight
                
                ' Only invalidate if line is visible
                If lY >= 0 AndAlso lY < pDrawingArea.AllocatedHeight Then
                    ' Queue draw for the specific line region
                    pDrawingArea?.QueueDrawArea(0, lY, pDrawingArea.AllocatedWidth, pLineHeight)
                    
                    ' Also update line number area if needed
                    pLineNumberWidget?.QueueDrawArea(0, lY, pLineNumberWidget.AllocatedWidth, pLineHeight)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InvalidateLine error: {ex.Message}")
            End Try
        End Sub
        
        ' Preserve desired column during vertical cursor movement
        Private Sub PreserveDesiredColumn(vAction As System.Action)
            Try
                pPreservingDesiredColumn = True
                vAction.Invoke()
            Finally
                pPreservingDesiredColumn = False
            End Try
        End Sub
        
        ' Get the actual cursor column position (handling tabs, etc.)
        Private Function GetActualCursorColumn() As Integer
            Try
                If pCursorLine >= pLineCount Then Return 0
                
                Dim lLine As String = TextLines(pCursorLine)
                If String.IsNullOrEmpty(lLine) Then Return 0
                
                ' For now, return the stored column
                ' In future, this could handle tab expansion, etc.
                Return pCursorColumn
                
            Catch ex As Exception
                Console.WriteLine($"GetActualCursorColumn error: {ex.Message}")
                Return 0
            End Try
        End Function

        
        ' Validate cursor position and adjust if necessary
        Private Sub ValidateCursorPosition()
            Try
                ' Ensure cursor is within document bounds
                If pCursorLine >= pLineCount Then
                    pCursorLine = Math.Max(0, pLineCount - 1)
                End If
                
                If pCursorLine < 0 Then
                    pCursorLine = 0
                End If
                
                ' Ensure cursor column is valid for current line
                If pCursorLine < pLineCount AndAlso TextLines(pCursorLine) IsNot Nothing Then
                    Dim lLineLength As Integer = TextLines(pCursorLine).Length
                    If pCursorColumn > lLineLength Then
                        pCursorColumn = lLineLength
                    End If
                End If
                
                If pCursorColumn < 0 Then
                    pCursorColumn = 0
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ValidateCursorPosition error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
