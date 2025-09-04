' Editors/CustomDrawingEditor.TextManipulation.vb - Missing IEditor method implementations
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        



        ' ===== Updated Text Property to sync with SourceFileInfo =====

        Public Property Text As String Implements IEditor.Text
            Get
                Return pSourceFileInfo.GetAllText()
            End Get
            Set(Value As String)
                Try
                    pSourceFileInfo.SetAllText(Value)
                    
                    ' Reset cursor and selection
                    pCursorLine = 0
                    pCursorColumn = 0
                    pSelectionActive = False
                    
                    ' Mark as modified
                    IsModified = True
                    
                    ' Trigger events
                    RaiseEvent TextChanged(Me, New EventArgs)
                    RaiseEvent CursorPositionChanged(pCursorLine, pCursorColumn)
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()

                    UpdateScrollbars()
                    
                Catch ex As Exception
                    Console.WriteLine($"Text.Set error: {ex.Message}")
                End Try
            End Set
        End Property
        
        ' SelectedText property - Gets the currently selected text
        Public ReadOnly Property SelectedText As String Implements IEditor.SelectedText
            Get
                Try
                    Return GetSelectedText()
                Catch ex As Exception
                    Console.WriteLine($"SelectedText error: {ex.Message}")
                    Return ""
                End Try
            End Get
        End Property

        Public Sub DeleteRange(vStartPosition As EditorPosition, vEndPosition As EditorPosition) Implements IEditor.DeleteRange
            Try
                If pIsReadOnly Then Return
                If vStartPosition.Line < 0 OrElse vStartPosition.Line >= pLineCount Then Return
                If vEndPosition.Line < 0 OrElse vEndPosition.Line >= pLineCount Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Set selection to the range
                SetSelection(vStartPosition, vEndPosition)
                
                ' Delete the selection
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteRange error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Replaces text in the specified range with new text using EditorPosition parameters
        ''' </summary>
        ''' <param name="vStartPosition">The start position of the text to replace</param>
        ''' <param name="vEndPosition">The end position of the text to replace</param>
        ''' <param name="vNewText">The new text to insert in place of the selected range</param>
        Public Sub ReplaceText(vStartPosition As EditorPosition, vEndPosition As EditorPosition, vNewText As String) Implements IEditor.ReplaceText
            Try
                If pIsReadOnly Then Return
                If vStartPosition.Line < 0 OrElse vStartPosition.Line >= pLineCount Then Return
                If vEndPosition.Line < 0 OrElse vEndPosition.Line >= pLineCount Then Return
                
                ' Normalize positions to ensure start comes before end
                Dim lStartPos As EditorPosition = vStartPosition
                Dim lEndPos As EditorPosition = vEndPosition
                
                If lStartPos.Line > lEndPos.Line OrElse 
                   (lStartPos.Line = lEndPos.Line AndAlso lStartPos.Column > lEndPos.Column) Then
                    ' Swap positions
                    Dim lTemp As EditorPosition = lStartPos
                    lStartPos = lEndPos
                    lEndPos = lTemp
                End If
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Delete the range first
                DeleteRange(lStartPos, lEndPos)
                
                ' Insert new text at the start position
                InsertTextAtPosition(lStartPos, vNewText)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Schedule parsing if the change spans multiple lines or is significant
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Replaces the entire document text with the specified text
        ''' </summary>
        ''' <param name="vText">The new text content for the entire document</param>
        Public Sub ReplaceAllText(vText As String) Implements IEditor.ReplaceAllText
            Try
                If pIsReadOnly Then Return
                
                ' Begin undo group for complete text replacement
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Update the Text property which will handle all the necessary updates
                Text = vText
                
                ' Reset cursor position to start of document
                SetCursorPosition(0, 0)
                
                ' Clear any selection
                ClearSelection()
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Mark as modified and raise events
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                
                ' Update display
                UpdateLineNumberWidth()
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"ReplaceText: Replaced entire document with {vText.Length} characters")
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceText error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Missing IEditor Indentation Methods =====
        

        

        
        Public Function GetLineIndentation(vLine As Integer) As String Implements IEditor.GetLineIndentation
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return ""
                
                Dim lLine As String = TextLines(vLine)
                Dim lIndentation As New System.Text.StringBuilder()
                
                ' Extract leading whitespace
                for each lChar As Char in lLine
                    If lChar = " "c OrElse lChar = vbTab Then
                        lIndentation.Append(lChar)
                    Else
                        Exit for
                    End If
                Next
                
                Return lIndentation.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"GetLineIndentation error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Private method to add indentation to a line
        ''' </summary>
        ''' <param name="vLine">Line number to indent (0-based)</param>
        Private Sub IndentLinePrivate(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Get current line
                Dim lLine As String = TextLines(vLine)
                
                ' Add indentation (4 spaces or tab based on settings)
                Dim lIndent As String = If(pUseTabs, vbTab, New String(" "c, pTabWidth))
                TextLines(vLine) = lIndent & lLine
                
                ' Update character colors array
                If pCharacterColors IsNot Nothing AndAlso vLine < pCharacterColors.Length Then
                    Dim lNewColors(TextLines(vLine).Length - 1) As CharacterColorInfo
                    If pCharacterColors(vLine) IsNot Nothing Then
                        ' Shift existing colors by indent length
                        for i As Integer = 0 To pCharacterColors(vLine).Length - 1
                            If i + lIndent.Length < lNewColors.Length Then
                                lNewColors(i + lIndent.Length) = pCharacterColors(vLine)(i)
                            End If
                        Next
                    End If
                    pCharacterColors(vLine) = lNewColors
                End If
                
                ' If cursor is on this line, adjust cursor position
                If pCursorLine = vLine Then
                    pCursorColumn += lIndent.Length
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IndentLinePrivate error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Private method to remove indentation from a line
        ''' </summary>
        ''' <param name="vLine">Line number to outdent (0-based)</param>
        Private Sub OutdentLinePrivate(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Get current line
                Dim lLine As String = TextLines(vLine)
                If String.IsNullOrEmpty(lLine) Then Return
                
                ' Determine how much to remove
                Dim lRemoveCount As Integer = 0
                
                If lLine.StartsWith(vbTab) Then
                    ' Remove one tab
                    lRemoveCount = 1
                ElseIf lLine.StartsWith(" ") Then
                    ' Remove up to TabWidth spaces
                    for i As Integer = 0 To Math.Min(pTabWidth - 1, lLine.Length - 1)
                        If lLine(i) = " "c Then
                            lRemoveCount += 1
                        Else
                            Exit for
                        End If
                    Next
                End If
                
                If lRemoveCount > 0 Then
                    ' Update the line text
                    TextLines(vLine) = lLine.Substring(lRemoveCount)
                    
                    ' Update character colors array
                    If pCharacterColors IsNot Nothing AndAlso vLine < pCharacterColors.Length Then
                        Dim lNewColors(TextLines(vLine).Length - 1) As CharacterColorInfo
                        If pCharacterColors(vLine) IsNot Nothing AndAlso pCharacterColors(vLine).Length > lRemoveCount Then
                            ' Shift colors left
                            for i As Integer = lRemoveCount To pCharacterColors(vLine).Length - 1
                                If i - lRemoveCount < lNewColors.Length Then
                                    lNewColors(i - lRemoveCount) = pCharacterColors(vLine)(i)
                                End If
                            Next
                        End If
                        pCharacterColors(vLine) = lNewColors
                    End If
                    
                    
                    ' If cursor is on this line, adjust cursor position
                    If pCursorLine = vLine AndAlso pCursorColumn > 0 Then
                        pCursorColumn = Math.Max(0, pCursorColumn - lRemoveCount)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OutdentLinePrivate error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Updated Public Interface Methods (REPLACE EXISTING) =====
        
        ''' <summary>
        ''' Indents a single line at the specified position
        ''' </summary>
        ''' <param name="vLine">Line number to indent (0-based)</param>
        Public Sub IndentLine(vLine As Integer) Implements IEditor.IndentLine
            Try
                If pIsReadOnly Then Return
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Call the private implementation
                IndentLinePrivate(vLine)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"IndentLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Outdents (removes indentation from) a single line
        ''' </summary>
        ''' <param name="vLine">Line number to outdent (0-based)</param>
        Public Sub OutdentLine(vLine As Integer) Implements IEditor.OutdentLine
            Try
                If pIsReadOnly Then Return
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Call the private implementation
                OutdentLinePrivate(vLine)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OutdentLine error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Updated Selection Methods (REPLACE EXISTING) =====
        
        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.IndentSelection
        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.IndentSelection
        ''' <summary>
        ''' Indents the selected lines or current line
        ''' </summary>
        ''' <remarks>
        ''' When selection ends at column 0 of a line, that line is excluded from indentation
        ''' to match standard editor behavior
        ''' </remarks>
        Public Sub IndentSelection() Implements IEditor.IndentSelection
            Try
                If pIsReadOnly Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Check both selection flags for compatibility
                If pSelectionActive OrElse pHasSelection Then
                    ' Get selection bounds
                    Dim lStartLine As Integer = Math.Min(pSelectionStartLine, pSelectionEndLine)
                    Dim lEndLine As Integer = Math.Max(pSelectionStartLine, pSelectionEndLine)
                    
                    ' CRITICAL FIX: If selection ends at column 0 of a line, don't include that line
                    ' This matches standard editor behavior where selecting to the beginning of a line
                    ' doesn't include that line in the operation
                    If pSelectionEndColumn = 0 AndAlso pSelectionEndLine <> pSelectionStartLine Then
                        lEndLine = lEndLine - 1
                    End If
                    
                    ' Validate bounds
                    lStartLine = Math.Max(0, Math.Min(lStartLine, pLineCount - 1))
                    lEndLine = Math.Max(0, Math.Min(lEndLine, pLineCount - 1))
                    
                    ' Store original cursor position relative to line start
                    Dim lCursorWasAtColumn As Integer = pCursorColumn
                    Dim lCursorWasAtLine As Integer = pCursorLine
                    
                    ' Indent each line in selection
                    for i As Integer = lStartLine To lEndLine
                        IndentLinePrivate(i)
                    Next
                    
                    ' Adjust selection to include the indentation
                    ' After indenting, select from start of first line to end of last line
                    SetSelection(New EditorPosition(lStartLine, 0), 
                                New EditorPosition(lEndLine, TextLines(lEndLine).Length))
                    
                    ' If cursor was at column 0 of the line after the selection, keep it there
                    If pSelectionEndColumn = 0 AndAlso lCursorWasAtLine > lEndLine Then
                        SetCursorPosition(lCursorWasAtLine, 0)
                    End If
                Else
                    ' No selection - indent current line
                    Dim lOldColumn As Integer = pCursorColumn
                    IndentLinePrivate(pCursorLine)
                    
                    ' Adjust cursor position to maintain relative position
                    Dim lIndentSize As Integer = If(pUseTabs, 1, pTabWidth)
                    SetCursorPosition(pCursorLine, lOldColumn + lIndentSize)
                End If
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"IndentSelection error: {ex.Message}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.OutdentSelection
        ''' <summary>
        ''' Outdents the selected lines or current line
        ''' </summary>
        ''' <remarks>
        ''' When selection ends at column 0 of a line, that line is excluded from outdenting
        ''' to match standard editor behavior
        ''' </remarks>
        Public Sub OutdentSelection() Implements IEditor.OutdentSelection
            Try
                If pIsReadOnly Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Check if there's a selection (using same check as IndentSelection)
                If pSelectionActive OrElse pHasSelection Then
                    ' Get selection bounds
                    Dim lStartLine As Integer = Math.Min(pSelectionStartLine, pSelectionEndLine)
                    Dim lEndLine As Integer = Math.Max(pSelectionStartLine, pSelectionEndLine)
                    
                    ' CRITICAL FIX: If selection ends at column 0 of a line, don't include that line
                    ' This matches standard editor behavior where selecting to the beginning of a line
                    ' doesn't include that line in the operation
                    If pSelectionEndColumn = 0 AndAlso pSelectionEndLine <> pSelectionStartLine Then
                        lEndLine = lEndLine - 1
                    End If
                    
                    ' Validate bounds
                    lStartLine = Math.Max(0, Math.Min(lStartLine, pLineCount - 1))
                    lEndLine = Math.Max(0, Math.Min(lEndLine, pLineCount - 1))
                    
                    ' Store original cursor position
                    Dim lCursorWasAtColumn As Integer = pCursorColumn
                    Dim lCursorWasAtLine As Integer = pCursorLine
                    
                    ' Outdent each line in selection
                    for i As Integer = lStartLine To lEndLine
                        OutdentLinePrivate(i)
                    Next
                    
                    ' Adjust selection to full lines after outdenting
                    SetSelection(New EditorPosition(lStartLine, 0), 
                                New EditorPosition(lEndLine, TextLines(lEndLine).Length))
                    
                    ' If cursor was at column 0 of the line after the selection, keep it there
                    If pSelectionEndColumn = 0 AndAlso lCursorWasAtLine > lEndLine Then
                        SetCursorPosition(lCursorWasAtLine, 0)
                    End If
                Else
                    ' No selection - outdent current line
                    OutdentLinePrivate(pCursorLine)
                    
                    ' Adjust cursor position if it was in the removed indentation
                    If pCursorColumn > 0 AndAlso pCursorColumn <= GetLineIndentation(pCursorLine).Length Then
                        ' Move cursor to start of text after outdenting
                        Dim lNewIndent As String = GetLineIndentation(pCursorLine)
                        SetCursorPosition(pCursorLine, lNewIndent.Length)
                    End If
                End If
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OutdentSelection error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
