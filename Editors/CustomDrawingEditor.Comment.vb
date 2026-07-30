' Editors/CustomDrawingEditor.Comment.vb - Find and Replace implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor


        ''' <summary>
        ''' Toggles comment on selected lines or current line
        ''' </summary>
        ''' <remarks>
        ''' Uses SourceFileInfo for text manipulation to maintain consistency with the new architecture.
        ''' First captures which lines to process, then squares the selection for visual feedback.
        ''' If a line starts with ' at position 0, it removes the comment. 
        ''' Otherwise adds ' at the very beginning of the line (position 0).
        ''' Ignores any ' marks that are not at the first character of the line.
        ''' </remarks>
        Public Sub ToggleCommentBlock() Implements IEditor.ToggleCommentBlock
            Try
                If pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Begin undo group for the entire operation
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Capture the lines to process BEFORE squaring the selection
                Dim lStartLine As Integer
                Dim lEndLine As Integer
                
                If pHasSelection AndAlso pSelectionActive Then
                    ' Normalize selection bounds
                    lStartLine = Math.Min(pSelectionStartLine, pSelectionEndLine)
                    lEndLine = Math.Max(pSelectionStartLine, pSelectionEndLine)
                    
                    ' Important: If selection ends at column 0 of a line, don't include that line
                    ' This happens when selection was extended to the start of the next line
                    Dim lActualEndLine As Integer = pSelectionEndLine
                    Dim lActualEndColumn As Integer = pSelectionEndColumn
                    If pSelectionEndLine > pSelectionStartLine Then
                        ' Selection spans multiple lines
                        If pSelectionEndColumn = 0 Then
                            ' Selection ends at start of a line - exclude that line
                            lEndLine = lEndLine - 1
                        End If
                    End If
                Else
                    ' No selection - use current line only
                    lStartLine = pCursorLine
                    lEndLine = pCursorLine
                End If
                
                ' Validate bounds
                lStartLine = Math.Max(0, Math.Min(lStartLine, pLineCount - 1))
                lEndLine = Math.Max(0, Math.Min(lEndLine, pLineCount - 1))
                
                ' Now square the selection for visual feedback (if there is a selection)
                If pHasSelection AndAlso pSelectionActive Then
                    SquareSelection()
                End If
                
                ' Check if all selected lines are already commented (at position 0 only)
                Dim lAllCommented As Boolean = True
                for i As Integer = lStartLine To lEndLine
                    If i >= 0 AndAlso i < pLineCount Then
                        Dim lLine As String = pSourceFileInfo.TextLines(i)
                        
                        ' Check if line starts with comment at position 0 (ignore empty lines)
                        If lLine.Length > 0 AndAlso Not lLine.StartsWith("'") Then
                            lAllCommented = False
                            Exit For
                        End If
                    End If
                Next
                
                ' Process each line using SourceFileInfo methods
                For i As Integer = lStartLine To lEndLine
                    If i >= 0 AndAlso i < pLineCount Then
                        Dim lLine As String = pSourceFileInfo.TextLines(i)
                        Dim lNewLine As String
                        
                        If lAllCommented Then
                            ' Remove comment from lines that start with ' at position 0
                            If lLine.StartsWith("'") Then
                                ' Remove the apostrophe and the space after it (if present)
                                If lLine.Length > 1 AndAlso lLine(1) = " " Then
                                    ' Remove apostrophe and one space
                                    pSourceFileInfo.DeleteCharacter(i, 0)
                                    pSourceFileInfo.DeleteCharacter(i, 0)

                                Else
                                    ' Remove just the apostrophe
                                    pSourceFileInfo.DeleteCharacter(i, 0)
                                End If
                            Else
                                ' Line doesn't start with comment, leave it unchanged
                                lNewLine = lLine
                            End If
                        Else
                            ' Add comment to all lines at position 0
                            ' Add apostrophe and space at the very beginning
                            pSourceFileInfo.InsertCharacter(i, 0, "'"c)

                        End If
                        
                    End If
                Next
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Keep the squared selection visible
                ' The SquareSelection call above already set the proper selection bounds
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ToggleCommentBlock error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Squares the selection to include complete lines
        ''' </summary>
        ''' <remarks>
        ''' Changes a selection to include whole lines instead of partial lines.
        ''' The beginning of selection is moved to column 0, and the end is moved
        ''' to column 0 of the next line (or end of last line if at document end).
        ''' Uses SourceFileInfo for accessing text lines to maintain consistency.
        ''' </remarks>
        Public Sub SquareSelection() Implements IEditor.SquareSelection
            Try
                ' Only process if we have a selection
                If Not pHasSelection OrElse Not pSelectionActive Then
                    Return
                End If
                
                ' Ensure SourceFileInfo is available
                If pSourceFileInfo Is Nothing Then
                    Return
                End If
                
                ' Get current selection bounds
                Dim lStartLine As Integer = pSelectionStartLine
                Dim lStartColumn As Integer = pSelectionStartColumn
                Dim lEndLine As Integer = pSelectionEndLine
                Dim lEndColumn As Integer = pSelectionEndColumn
                
                ' Normalize selection (ensure start is before end)
                If lStartLine > lEndLine OrElse (lStartLine = lEndLine AndAlso lStartColumn > lEndColumn) Then
                    ' Swap start and end
                    Dim lTempLine As Integer = lStartLine
                    Dim lTempColumn As Integer = lStartColumn
                    lStartLine = lEndLine
                    lStartColumn = lEndColumn
                    lEndLine = lTempLine
                    lEndColumn = lTempColumn
                End If
                
                ' Square the selection
                ' Start always goes to column 0
                pSelectionStartLine = lStartLine
                pSelectionStartColumn = 0
                
                ' End handling
                If lEndColumn = 0 AndAlso lEndLine > lStartLine Then
                    ' Selection already ends at start of a line - don't include that line
                    pSelectionEndLine = lEndLine
                    pSelectionEndColumn = 0
                ElseIf lEndLine < pLineCount - 1 Then
                    ' Not the last line - extend to start of next line
                    pSelectionEndLine = lEndLine + 1
                    pSelectionEndColumn = 0
                Else
                    ' Last line - extend to end of line using SourceFileInfo
                    pSelectionEndLine = lEndLine
                    pSelectionEndColumn = pSourceFileInfo.TextLines(lEndLine).Length
                End If
                
                ' Update cursor position to end of selection
                SetCursorPosition(pSelectionEndLine, pSelectionEndColumn)
                
                ' Queue redraw to show updated selection
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"SquareSelection: Lines {pSelectionStartLine} To {pSelectionEndLine}")
                
            Catch ex As Exception
                Console.WriteLine($"SquareSelection error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
