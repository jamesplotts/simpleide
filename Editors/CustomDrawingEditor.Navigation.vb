' Editors/CustomDrawingEditor.Navigation.vb - Navigation method implementations for IEditor interface
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== IEditor Navigation Method Implementations =====
        
        ' Navigate to a specific line number (1-based for user interface)
        Public Sub GoToLine(vLine As Integer) Implements IEditor.GoToLine
            Try
                ' Convert to 0-based index
                Dim lTargetLine As Integer = vLine - 1
                
                ' Validate line number
                If lTargetLine < 0 Then
                    lTargetLine = 0
                ElseIf lTargetLine >= pLineCount Then
                    lTargetLine = pLineCount - 1
                End If
                
                ' Clear any existing selection
                ClearSelection()
                
                ' Position cursor at beginning of the line
                SetCursorPosition(lTargetLine, 0)
                
                ' Ensure the line is visible
                EnsureCursorVisible()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"GoToLine error: {ex.Message}")
            End Try
        End Sub
        
        ' Navigate to a specific position (line and column)
        Public Sub GoToPosition(vLine As Integer, vColumn As Integer) Implements IEditor.GoToPosition
            Try
                ' Convert to 0-based indices
                Dim lTargetLine As Integer = vLine - 1
                Dim lTargetColumn As Integer = vColumn - 1
                
                ' Validate line number
                If lTargetLine < 0 Then
                    lTargetLine = 0
                ElseIf lTargetLine >= pLineCount Then
                    lTargetLine = pLineCount - 1
                End If
                
                ' Validate column for the target line
                If lTargetLine < pLineCount AndAlso pTextLines(lTargetLine) IsNot Nothing Then
                    Dim lLineLength As Integer = pTextLines(lTargetLine).Length
                    If lTargetColumn < 0 Then
                        lTargetColumn = 0
                    ElseIf lTargetColumn > lLineLength Then
                        lTargetColumn = lLineLength
                    End If
                Else
                    lTargetColumn = 0
                End If
                
                ' Clear any existing selection
                ClearSelection()
                
                ' Set cursor position
                SetCursorPosition(lTargetLine, lTargetColumn)
                
                ' Ensure the position is visible
                EnsureCursorVisible()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"GoToPosition error: {ex.Message}")
            End Try
        End Sub
        
'        ' Move cursor to the beginning of the document
        Public Sub MoveToDocumentStart() Implements IEditor.MoveToDocumentStart
            Try
                ' Clear any existing selection
                ClearSelection()
                
                ' Move to start of document
                SetCursorPosition(0, 0)
                
                ' Reset desired column
                pDesiredColumn = 0
                
                ' Ensure position is visible
                EnsureCursorVisible()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"MoveToDocumentStart error: {ex.Message}")
            End Try
        End Sub
        
        ' Move cursor to the end of the document
        Public Sub MoveToDocumentEnd() Implements IEditor.MoveToDocumentEnd
            Try
                ' Clear any existing selection
                ClearSelection()
                
                ' Find last line
                Dim lLastLine As Integer = Math.Max(0, pLineCount - 1)
                Dim lLastColumn As Integer = 0
                
                ' Get length of last line
                If lLastLine < pLineCount AndAlso pTextLines(lLastLine) IsNot Nothing Then
                    lLastColumn = pTextLines(lLastLine).Length
                End If
                
                ' Move to end of last line
                SetCursorPosition(lLastLine, lLastColumn)
                
                ' Update desired column
                pDesiredColumn = lLastColumn
                
                ' Ensure position is visible
                EnsureCursorVisible()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"MoveToDocumentEnd error: {ex.Message}")
            End Try
        End Sub
        
        ' Move cursor to the beginning of the current line
        Public Sub MoveToLineStart() Implements IEditor.MoveToLineStart
            Try
                ' Clear any existing selection
                ClearSelection()
                
                ' Move to start of current line
                SetCursorPosition(pCursorLine, 0)
                
                ' Reset desired column
                pDesiredColumn = 0
                
                ' Ensure position is visible
                EnsureCursorVisible()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"MoveToLineStart error: {ex.Message}")
            End Try
        End Sub
        
        ' Move cursor to the end of the current line
        Public Sub MoveToLineEnd() Implements IEditor.MoveToLineEnd
            Try
                ' Clear any existing selection
                ClearSelection()
                
                ' Get current line length
                Dim lLineLength As Integer = 0
                If pCursorLine < pLineCount AndAlso pTextLines(pCursorLine) IsNot Nothing Then
                    lLineLength = pTextLines(pCursorLine).Length
                End If
                
                ' Move to end of current line
                SetCursorPosition(pCursorLine, lLineLength)
                
                ' Update desired column
                pDesiredColumn = lLineLength
                
                ' Ensure position is visible
                EnsureCursorVisible()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"MoveToLineEnd error: {ex.Message}")
            End Try
        End Sub
        

'        
    End Class
    
End Namespace
