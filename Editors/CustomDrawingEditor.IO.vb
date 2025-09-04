' File: CustomDrawingEditor.IO.vb
' This file should be DELETED or contain only helper methods for text manipulation
' NO FILE I/O METHODS should exist in CustomDrawingEditor

Imports Gtk
Imports System
Imports System.Text
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Helper Methods for Text Operations (NOT File I/O) =====
        
        ''' <summary>
        ''' Sets text content directly into SourceFileInfo
        ''' </summary>
        ''' <param name="vText">The text to set</param>
        ''' <remarks>
        ''' Helper method for setting content programmatically (e.g., from paste operations)
        ''' This does NOT involve file I/O
        ''' </remarks>
        Public Sub SetText(vText As String)
            Try
                If pSourceFileInfo Is Nothing Then Return
                
                ' Update SourceFileInfo's TextLines
                pSourceFileInfo.SetText(vText)
                
                
                ' Reset cursor and selection
                pCursorLine = 0
                pCursorColumn = 0
                pSelectionActive = False
                
                ' Mark as modified (in-memory change, not file I/O)
                IsModified = True
                
                ' Trigger events
                RaiseEvent TextChanged(Me, New EventArgs)
                RaiseEvent CursorPositionChanged(pCursorLine, pCursorColumn)
                RaiseEvent UndoRedoStateChanged(pUndoRedoManager?.CanUndo, pUndoRedoManager?.CanRedo)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor.SetText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets text content from SourceFileInfo
        ''' </summary>
        ''' <returns>The complete text content</returns>
        ''' <remarks>
        ''' Helper method for getting content programmatically (e.g., for copy operations)
        ''' This does NOT involve file I/O
        ''' </remarks>
        Private Function GetText() As String
            Try
                If pSourceFileInfo Is Nothing Then Return ""
                Return String.Join(Environment.NewLine, pSourceFileInfo.TextLines)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor.GetText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all text as a single string
        ''' </summary>
        ''' <returns>Complete document text</returns>
        ''' <remarks>
        ''' Used for clipboard operations, NOT file I/O
        ''' </remarks>
        Public Function GetAllText() As String
            Return GetText()
        End Function
        
        ' NO LoadFile method
        ' NO Save method  
        ' NO SaveAs method
        ' NO LoadStream method
        ' NO SaveStream method
        ' These should all be removed from IEditor interface as well
        
    End Class
    
End Namespace