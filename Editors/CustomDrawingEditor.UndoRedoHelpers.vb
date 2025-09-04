Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        
        
        ''' <summary>
        ''' Helper method to insert a character directly without recording undo
        ''' </summary>
        Private Sub InsertCharacterDirect(vChar As Char)
            Try
                ' Ensure we have a valid line
                If pCursorLine >= pLineCount Then
                    ' Add empty lines if needed
                    While pLineCount <= pCursorLine
                        AddNewLine("")
                    End While
                End If
                
                ' Get current line
                Dim lLine As String = TextLines(pCursorLine)
                
                ' Ensure cursor column is valid
                If pCursorColumn > lLine.Length Then
                    pCursorColumn = lLine.Length
                End If
                
                ' Insert the character
                TextLines(pCursorLine) = lLine.Insert(pCursorColumn, vChar.ToString())
                
                ' Move cursor forward
                SetCursorPosition(pCursorLine, pCursorColumn + 1)
                
                ' Update display
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertCharacterDirect error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to insert a newline directly without recording undo
        ''' </summary>
        Private Sub InsertNxewLineDirect()
            Try
                ' Get current line
                Dim lLine As String = TextLines(pCursorLine)
                
                ' Split the line at cursor position
                Dim lFirstPart As String = lLine.Substring(0, Math.Min(pCursorColumn, lLine.Length))
                Dim lSecondPart As String = If(pCursorColumn < lLine.Length, lLine.Substring(pCursorColumn), "")
                
                ' Update current line
                TextLines(pCursorLine) = lFirstPart
                pLineMetadata(pCursorLine).MarkChanged()
                
                ' Insert new line
                InsertLineAt(pCursorLine + 1, lSecondPart)
                
                ' Move cursor to new line
                SetCursorPosition(pCursorLine + 1, 0)
                
                ' Apply auto-indent
                ApplyAutoIndent(pCursorLine)
                
                ' Update display
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea.QueueDraw()

                
            Catch ex As Exception
                Console.WriteLine($"InsertNewLineDirect error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes text directly without recording undo (EditorPosition overload)
        ''' </summary>
        ''' <param name="vStartPosition">Start position of text to delete</param>
        ''' <param name="vEndPosition">End position of text to delete</param>
        ''' <remarks>
        ''' This method is called by UndoRedoManager during undo/redo operations.
        ''' It must NOT record undo actions to avoid infinite loops.
        ''' </remarks>
        Friend Sub DeleteTextDirect(vStartPosition As EditorPosition, vEndPosition As EditorPosition) Implements IEditor.DeleteTextDirect
            Try
                If pSourceFileInfo Is Nothing Then Return
                
                ' Extract line and column values
                Dim lStartLine As Integer = vStartPosition.Line
                Dim lStartColumn As Integer = vStartPosition.Column
                Dim lEndLine As Integer = vEndPosition.Line
                Dim lEndColumn As Integer = vEndPosition.Column
                
                ' Normalize the range
                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                
                If lStartLine = lEndLine Then
                    ' Single line deletion
                    pSourceFileInfo.DeleteTextInLine(lStartLine, lStartColumn, lEndColumn)
                Else
                    ' Multi-line deletion
                    ' Get text to keep from first and last lines
                    Dim lFirstLine As String = pSourceFileInfo.TextLines(lStartLine)
                    Dim lLastLine As String = pSourceFileInfo.TextLines(lEndLine)
                    
                    Dim lKeepFromFirst As String = If(lStartColumn > 0, lFirstLine.Substring(0, lStartColumn), "")
                    Dim lKeepFromLast As String = If(lEndColumn < lLastLine.Length, lLastLine.Substring(lEndColumn), "")
                    
                    ' Combine the kept portions
                    pSourceFileInfo.UpdateTextLine(lStartLine, lKeepFromFirst & lKeepFromLast)
                   
                    ' Remove all lines between start and end
                    for i As Integer = lEndLine To lStartLine + 1 Step -1
                        pSourceFileInfo.DeleteLine(i)
                    Next
                End If
                
                
                ' Ensure we always have at least one line
                If pLineCount = 0 Then
                    pSourceFileInfo.TextLines.Add("")
                End If
                
                ' Set cursor to deletion start position
                SetCursorPosition(lStartLine, lStartColumn)
                
                ' Mark as modified
                IsModified = True
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
                ' Raise events
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"DeleteTextDirect error: {ex.Message}")
            End Try
        End Sub
        
'         ''' <summary>
'         ''' Alternative overload for backward compatibility with integer parameters
'         ''' </summary>
'         Public Sub DeleteTextDirect(vStartLine As Integer, vStartColumn As Integer,
'                                    vEndLine As Integer, vEndColumn As Integer) Implements IEditor.DeleteTextDirect
'             DeleteTextDirect(New EditorPosition(vStartLine, vStartColumn),
'                             New EditorPosition(vEndLine, vEndColumn))
'         End Sub
        
        ''' <summary>
        ''' Helper method for single-line deletion without undo
        ''' </summary>
        Private Sub DeleteTextDirectSingleLine(vLine As Integer, vStartColumn As Integer, vEndColumn As Integer)
            Try
                If vLine >= pLineCount Then Return
                
                Dim lLine As String = TextLines(vLine)
                
                ' Ensure columns are within bounds
                vStartColumn = Math.Max(0, Math.Min(vStartColumn, lLine.Length))
                vEndColumn = Math.Max(vStartColumn, Math.Min(vEndColumn, lLine.Length))
                
                ' Calculate length to remove
                Dim lLengthToRemove As Integer = vEndColumn - vStartColumn
                
                If lLengthToRemove > 0 Then
                    ' Remove the text
                    TextLines(vLine) = lLine.Remove(vStartColumn, lLengthToRemove)
                    
                    ' Mark line as changed
                    pLineMetadata(vLine).MarkChanged()
                    
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteTextDirectSingleLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method for multi-line deletion without undo
        ''' </summary>
        Private Sub DeleteTextDirectMultiLine(vStartLine As Integer, vStartColumn As Integer, 
                                              vEndLine As Integer, vEndColumn As Integer)
            Try
                ' Validate line indices
                If vStartLine >= pLineCount OrElse vEndLine >= pLineCount Then Return
                
                ' Get text to keep from first line (before deletion point)
                Dim lFirstLine As String = TextLines(vStartLine)
                Dim lKeepFromFirst As String = ""
                If vStartColumn > 0 Then
                    lKeepFromFirst = lFirstLine.Substring(0, Math.Min(vStartColumn, lFirstLine.Length))
                End If
                
                ' Get text to keep from last line (after deletion point)
                Dim lLastLine As String = TextLines(vEndLine)
                Dim lKeepFromLast As String = ""
                If vEndColumn < lLastLine.Length Then
                    lKeepFromLast = lLastLine.Substring(vEndColumn)
                End If
                
                ' Combine the kept portions
                TextLines(vStartLine) = lKeepFromFirst & lKeepFromLast
                pLineMetadata(vStartLine).MarkChanged()
                
                ' Remove all lines between start and end (including end line)
                Dim lLinesToRemove As Integer = vEndLine - vStartLine
                If lLinesToRemove > 0 Then
                    ' Remove lines in reverse order to maintain indices
                    for i As Integer = vEndLine To vStartLine + 1 Step -1
                        TextLines.RemoveAt(i)
                    Next
                    
                End If
                
                ' Ensure we always have at least one line
                If pLineCount = 0 Then
                    TextLines.Add("")
                End If
                
                
            Catch ex As Exception
                Console.WriteLine($"DeleteTextDirectMultiLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Alternative overload for backward compatibility with integer parameters
        ''' </summary>
        Public Sub DeleteTextDirect(vStartLine As Integer, vStartColumn As Integer,
                                   vEndLine As Integer, vEndColumn As Integer) Implements IEditor.DeleteTextDirect
            DeleteTextDirect(New EditorPosition(vStartLine, vStartColumn),
                            New EditorPosition(vEndLine, vEndColumn))
        End Sub
        
        

    End Class

End Namespace
