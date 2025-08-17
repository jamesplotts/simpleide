' Editors/CustomDrawingEditor.LineTracking.vb - Track line changes for formatting and capitalization
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Line Tracking Fields =====
        Private pPreviousLine As Integer = -1
        Private pLineExitPending As Boolean = False
        
        ' ===== LineExited Event =====
        Public Event LineExited As EventHandler(Of LineExitedEventArgs) Implements IEditor.LineExited
        
        ' ===== Line Change Tracking =====
        
        ' Called when cursor moves to a different line
        Private Sub OnLineChanged(vOldLine As Integer, vNewLine As Integer)
            Try
                ' Format the line we're leaving
                If vOldLine >= 0 AndAlso vOldLine < pLineCount AndAlso vOldLine <> vNewLine Then
                    ProcessLineFormatting(vOldLine)
                    
                    ' Fire LineExited event for capitalization manager
                    RaiseLineExitedEvent(vOldLine)
                End If
                
                ' Update editing line
                SetEditingLine(vNewLine)
                
                ' Update previous line tracker
                pPreviousLine = vNewLine
                
            Catch ex As Exception
                Console.WriteLine($"OnLineChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' Override SetCursorPosition to track line changes
        Private Sub SetCursorPosition_WithTracking(vLine As Integer, vColumn As Integer)
            Try
                Dim lOldLine As Integer = pCursorLine
                
                ' Call the base SetCursorPosition
                SetCursorPosition(vLine, vColumn)
                
                ' Check if line changed
                If lOldLine <> pCursorLine Then
                    OnLineChanged(lOldLine, pCursorLine)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetCursorPosition_WithTracking error: {ex.Message}")
            End Try
        End Sub
        
        ' Handle Enter key - format current line and move to next
        Private Sub HandleEnterKey()
            Try
                ' Format current line before creating new line
                If pEditingLine >= 0 AndAlso pEditingLine < pLineCount Then
                    ProcessLineFormatting(pEditingLine)
                    
                    ' Fire LineExited event before leaving the line
                    RaiseLineExitedEvent(pEditingLine)
                End If
                
                ' Now handle the normal Enter key behavior
                ' (Insert new line, move cursor, etc.)
                
            Catch ex As Exception
                Console.WriteLine($"HandleEnterKey error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== LineExited Event Methods =====
        
        ''' <summary>
        ''' Raise the LineExited event for a specific line
        ''' </summary>
        Private Sub RaiseLineExitedEvent(vLineNumber As Integer)
            Try
                ' Validate line number
                If vLineNumber < 0 OrElse vLineNumber >= pLineCount Then Return
                
                ' Get the text of the line that was exited
                Dim lLineText As String = GetLineText(vLineNumber)
                
                ' Create event args
                Dim lArgs As New LineExitedEventArgs(vLineNumber, lLineText)
                
                ' Raise the event
                RaiseEvent LineExited(Me, lArgs)
                
            Catch ex As Exception
                Console.WriteLine($"RaiseLineExitedEvent error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Force a line exit event for the current line (useful when losing focus)
        ''' </summary>
        Public Sub ForceLineExit()
            Try
                If pPreviousLine >= 0 AndAlso pPreviousLine < pLineCount Then
                    RaiseLineExitedEvent(pPreviousLine)
                ElseIf pCursorLine >= 0 AndAlso pCursorLine < pLineCount Then
                    RaiseLineExitedEvent(pCursorLine)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ForceLineExit error: {ex.Message}")
            End Try
        End Sub
        

        
        ' NOTE: The rest of the original file content remains unchanged
        ' including commented out methods for line highlighting
        
    End Class
    
End Namespace
