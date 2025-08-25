' Editors/CustomDrawingEditor.IO.vb - Fixed stream-based I/O operations for CustomDrawingEditor
Imports Gtk
Imports System
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        


        
        ' ===== Helper Methods for File Operations =====
        
        
        ' Set text content directly (helper method for backward compatibility)
        Public Sub SetText(vText As String)
            Try
                ' Clear undo/redo stacks when setting new content
                
                ' Split into lines
                Dim lLines As String() = If(String.IsNullOrEmpty(vText), {""}, vText.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                
                ' Update text lines
                pTextLines.Clear()
                pTextLines.AddRange(lLines)
                
                ' Ensure at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
                ' Update line count and metadata
                pLineCount = pTextLines.Count
                ReDim pLineMetadata(pLineCount - 1)
                For i As Integer = 0 To pLineCount - 1
                    pLineMetadata(i) = New LineMetadata()
                Next
                
                ' Reset cursor and selection
                pCursorLine = 0
                pCursorColumn = 0
                pSelectionActive = False
                               
                ' Mark as unmodified
                IsModified = False


                For i As Integer = 0 To pLineCount -1
                    ProcessLineFormatting(i)
                Next

                ' Schedule syntax highlighting
                ScheduleParse()

                ScheduleFullDocumentParse()
                
                ' Trigger events
                RaiseEvent TextChanged(Me, New EventArgs)
                RaiseEvent CursorPositionChanged(pCursorLine, pCursorColumn)
                RaiseEvent UndoRedoStateChanged(False, False)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                UpdateScrollbars()
               
            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor.SetText error: {ex.Message}")
            End Try
        End Sub
        
        ' Get text content directly (helper method for backward compatibility)
        Private Function GetText() As String
            Try
                Return String.Join(Environment.NewLine, pTextLines)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor.GetText error: {ex.Message}")
                Return ""
            End Try
        End Function
        



        
    End Class
    
End Namespace
