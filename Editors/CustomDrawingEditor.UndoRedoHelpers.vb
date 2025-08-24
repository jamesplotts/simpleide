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
                Dim lLine As String = pTextLines(pCursorLine)
                
                ' Ensure cursor column is valid
                If pCursorColumn > lLine.Length Then
                    pCursorColumn = lLine.Length
                End If
                
                ' Insert the character
                pTextLines(pCursorLine) = lLine.Insert(pCursorColumn, vChar.ToString())
                pLineMetadata(pCursorLine).MarkChanged()
                
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
        Private Sub InsertNewLineDirect()
            Try
                ' Get current line
                Dim lLine As String = pTextLines(pCursorLine)
                
                ' Split the line at cursor position
                Dim lFirstPart As String = lLine.Substring(0, Math.Min(pCursorColumn, lLine.Length))
                Dim lSecondPart As String = If(pCursorColumn < lLine.Length, lLine.Substring(pCursorColumn), "")
                
                ' Update current line
                pTextLines(pCursorLine) = lFirstPart
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
                pLineNumberArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertNewLineDirect error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes text directly without recording undo (EditorPosition overload)
        ''' </summary>
        ''' <param name="vStartPosition">Start position of text to delete</param>
        ''' <param name="vEndPosition">End position of text to delete</param>
        Friend Sub DeleteTextDirect(vStartPosition As EditorPosition, vEndPosition As EditorPosition) Implements IEditor.DeleteTextDirect
            Try
                ' Extract line and column values
                Dim lStartLine As Integer = vStartPosition.Line
                Dim lStartColumn As Integer = vStartPosition.Column
                Dim lEndLine As Integer = vEndPosition.Line
                Dim lEndColumn As Integer = vEndPosition.Column
                
                ' Normalize the range
                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                
                If lStartLine = lEndLine Then
                    ' Single line deletion
                    Dim lLine As String = pTextLines(lStartLine)
                    Dim lStart As Integer = Math.Max(0, Math.Min(lStartColumn, lLine.Length))
                    Dim lEnd As Integer = Math.Max(lStart, Math.Min(lEndColumn, lLine.Length))
                    
                    pTextLines(lStartLine) = lLine.Remove(lStart, lEnd - lStart)
                    pLineMetadata(lStartLine).MarkChanged()
                Else
                    ' Multi-line deletion
                    Dim lFirstLine As String = pTextLines(lStartLine)
                    Dim lLastLine As String = pTextLines(lEndLine)
                    
                    ' Combine first and last line parts
                    Dim lNewLine As String = If(lStartColumn < lFirstLine.Length, lFirstLine.Substring(0, lStartColumn), lFirstLine) &
                                           If(lEndColumn < lLastLine.Length, lLastLine.Substring(lEndColumn), "")
                    
                    ' Update first line
                    pTextLines(lStartLine) = lNewLine
                    pLineMetadata(lStartLine).MarkChanged()
                    
                    ' Remove middle lines
                    RemoveLines(lStartLine + 1, lEndLine - lStartLine)
                End If
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteTextDirect (EditorPosition) error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method for single-line deletion without undo
        ''' </summary>
        Private Sub DeleteTextDirectSingleLine(vLine As Integer, vStartColumn As Integer, vEndColumn As Integer)
            Try
                If vLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(vLine)
                
                ' Ensure columns are within bounds
                vStartColumn = Math.Max(0, Math.Min(vStartColumn, lLine.Length))
                vEndColumn = Math.Max(vStartColumn, Math.Min(vEndColumn, lLine.Length))
                
                ' Calculate length to remove
                Dim lLengthToRemove As Integer = vEndColumn - vStartColumn
                
                If lLengthToRemove > 0 Then
                    ' Remove the text
                    pTextLines(vLine) = lLine.Remove(vStartColumn, lLengthToRemove)
                    
                    ' Mark line as changed
                    pLineMetadata(vLine).MarkChanged()
                    
                    ' Update syntax highlighting for this line
                    If pHighlightingEnabled Then
                        ProcessLineFormatting(vLine)
                    End If
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
                Dim lFirstLine As String = pTextLines(vStartLine)
                Dim lKeepFromFirst As String = ""
                If vStartColumn > 0 Then
                    lKeepFromFirst = lFirstLine.Substring(0, Math.Min(vStartColumn, lFirstLine.Length))
                End If
                
                ' Get text to keep from last line (after deletion point)
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
                    ' Remove lines in reverse order to maintain indices
                    For i As Integer = vEndLine To vStartLine + 1 Step -1
                        pTextLines.RemoveAt(i)
                        RemoveLineMetadata(i)
                    Next
                    
                    ' Update line count
                    pLineCount = pTextLines.Count
                End If
                
                ' Ensure we always have at least one line
                If pLineCount = 0 Then
                    pTextLines.Add("")
                    EnsureLineMetadata(0)
                    pLineCount = 1
                End If
                
                ' Update syntax highlighting for the merged line
                If pHighlightingEnabled AndAlso vStartLine < pLineCount Then
                    ProcessLineFormatting(vStartLine)
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
        
        ''' <summary>
        ''' Ensures line metadata exists for the specified line
        ''' </summary>
        Private Sub EnsureLineMetadata(vLineIndex As Integer)
            Try
                ' Resize metadata arrays if needed
                If vLineIndex >= pLineMetadata.Length Then
                    ReDim Preserve pLineMetadata(vLineIndex)
                    ReDim Preserve pCharacterColors(vLineIndex)
                End If
                
                ' Create metadata if it doesn't exist
                If pLineMetadata(vLineIndex) Is Nothing Then
                    pLineMetadata(vLineIndex) = New LineMetadata()
                End If
                
                If pCharacterColors(vLineIndex) Is Nothing Then
                    pCharacterColors(vLineIndex) = New CharacterColorInfo() {}
                End If
                
            Catch ex As Exception
                Console.WriteLine($"EnsureLineMetadata error: {ex.Message}")
            End Try
        End Sub
        

    End Class

End Namespace
