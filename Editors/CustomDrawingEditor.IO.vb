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
        
' Replace: SimpleIDE.Editors.CustomDrawingEditor.SaveContent
Public Function SaveContent() As Boolean Implements IEditor.SaveContent
    Try
        ' Save through SourceFileInfo
        Dim lResult As Boolean = pSourceFileInfo.SaveContent()
        
        ' CRITICAL: If save was successful, sync the modified state
        If lResult Then
            ' Update the editor's modified state to match SourceFileInfo
            IsModified = False
            Console.WriteLine($"CustomDrawingEditor.SaveContent: Saved and cleared modified flag for {pFilePath}")
        End If
        
        Return lResult
        
    Catch ex As Exception
        Console.WriteLine($"CustomDrawingEditor.SaveContent error: {ex.Message}")
        Return False
    End Try
End Function

        
' Replace: SimpleIDE.Editors.CustomDrawingEditor.LoadContent
Public Function LoadContent() As Boolean Implements IEditor.LoadContent
    Try
        ' Load through SourceFileInfo (it's the source of truth for file content)
        Dim lResult As Boolean = pSourceFileInfo.LoadContent()
        
        If lResult Then
            ' CRITICAL: After loading from disk, content is not modified
            IsModified = False
            
            ' Reset cursor to safe position
            If pCursorLine >= pLineCount Then
                SetCursorPosition(Math.Max(0, pLineCount - 1), 0)
            ElseIf pCursorColumn > 0 AndAlso pCursorLine < pLineCount Then
                Dim lLineLength As Integer = pSourceFileInfo.TextLines(pCursorLine).Length
                If pCursorColumn > lLineLength Then
                    SetCursorPosition(pCursorLine, lLineLength)
                End If
            Else
                SetCursorPosition(0, 0)
            End If
            
            ' Clear selection
            ClearSelection()
            
            ' Clear undo/redo history since we've reloaded from disk
            If pUndoRedoManager IsNot Nothing Then
                pUndoRedoManager.Clear()
                RaiseEvent UndoRedoStateChanged(False, False)
            End If
            
            ' Update UI components
            UpdateLineNumberWidth()
            UpdateScrollbars()
            pDrawingArea?.QueueDraw()
            
            Console.WriteLine($"CustomDrawingEditor.LoadContent: Loaded and cleared modified flag for {pFilePath}")
        Else
            Console.WriteLine($"CustomDrawingEditor.LoadContent: Failed to load {pFilePath}")
        End If
        
        Return lResult
        
    Catch ex As Exception
        Console.WriteLine($"CustomDrawingEditor.LoadContent error: {ex.Message}")
        Return False
    End Try
End Function

    End Class
    
End Namespace