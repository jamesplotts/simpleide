' Editors/CustomDrawingEditor.Cursor.vb - Cursor and selection management
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        Private pVisibleLines As Integer

        ' ===== Cursor Management =====
        
        Private Sub StartCursorBlink()
            Try
                ' Stop existing timer if any
                StopCursorBlink()
                
                ' Start new timer for cursor blinking (500ms interval)
                pCursorBlinkTimer = GLib.Timeout.Add(500, AddressOf OnCursorBlink)
                pCursorVisible = True
                pCursorBlink = True
                
            Catch ex As Exception
                Console.WriteLine($"StartCursorBlink error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub StopCursorBlink()
            Try
                If pCursorBlinkTimer > 0 Then
                    GLib.Source.Remove(pCursorBlinkTimer)
                    pCursorBlinkTimer = 0
                End If
                pCursorVisible = True ' Always Show cursor when stopping blink
                pCursorBlink = True
                
            Catch ex As Exception
                Console.WriteLine($"StopCursorBlink error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Selection Management =====
        
        Private Sub StartSelection(vLine As Integer, vColumn As Integer)
            Try
                ' Ensure valid range
                vLine = Math.Max(0, Math.Min(vLine, pLineCount - 1))
                vColumn = Math.Max(0, vColumn)
                If vLine < pLineCount Then
                    vColumn = Math.Min(vColumn, pTextLines(vLine).Length)
                End If
                
                pSelectionStartLine = vLine
                pSelectionStartColumn = vColumn
                pSelectionEndLine = vLine
                pSelectionEndColumn = vColumn
                pSelectionActive = True
                pHasSelection = False ' No selection yet until drag/move
                
            Catch ex As Exception
                Console.WriteLine($"StartSelection error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateSelection(vEndLine As Integer, vEndColumn As Integer)
            Try
                ' Ensure valid range
                vEndLine = Math.Max(0, Math.Min(vEndLine, pLineCount - 1))
                vEndColumn = Math.Max(0, vEndColumn)
                If vEndLine < pLineCount Then
                    vEndColumn = Math.Min(vEndColumn, pTextLines(vEndLine).Length)
                End If
                
                pSelectionEndLine = vEndLine
                pSelectionEndColumn = vEndColumn
                
                ' Check if we have a real selection
                pHasSelection = (pSelectionStartLine <> pSelectionEndLine) OrElse (pSelectionStartColumn <> pSelectionEndColumn)
                
                ' Raise event
                RaiseEvent SelectionChanged(pHasSelection)
                
                ' Redraw
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSelection error: {ex.Message}")
            End Try
        End Sub
        
        ' Clear any active selection
        Private Sub ClearSelection() Implements IEditor.ClearSelection
            Try
                If pSelectionActive OrElse pHasSelection Then
                    pSelectionActive = False
                    pHasSelection = False
                    pSelectionStartLine = pCursorLine
                    pSelectionStartColumn = pCursorColumn
                    pSelectionEndLine = pCursorLine
                    pSelectionEndColumn = pCursorColumn
                    
                    ' Raise selection changed event
                    RaiseEvent SelectionChanged(False)
                    
                    ' Queue redraw to clear selection highlight
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ClearSelection error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Cursor Movement Methods =====
        
        Private Sub MoveCursorLeft(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                If pCursorColumn > 0 Then
                    SetCursorPosition(pCursorLine, pCursorColumn - 1)
                ElseIf pCursorLine > 0 Then
                    ' Move to end of previous line
                    SetCursorPosition(pCursorLine - 1, pTextLines(pCursorLine - 1).Length)
                End If
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorLeft error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveCursorRight(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                If pCursorLine < pLineCount AndAlso pCursorColumn < pTextLines(pCursorLine).Length Then
                    SetCursorPosition(pCursorLine, pCursorColumn + 1)
                ElseIf pCursorLine < pLineCount - 1 Then
                    ' Move to start of next line
                    SetCursorPosition(pCursorLine + 1, 0)
                End If
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorRight error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveCursorUp(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                If pCursorLine > 0 Then
                    PreserveDesiredColumn(Sub() SetCursorPosition(pCursorLine - 1, pDesiredColumn))
                End If
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorUp error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveCursorDown(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                If pCursorLine < pLineCount - 1 Then
                    PreserveDesiredColumn(Sub() SetCursorPosition(pCursorLine + 1, pDesiredColumn))
                End If
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorDown error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveCursorHome(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                SetCursorPosition(pCursorLine, 0)
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorHome error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveCursorEnd(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                If pCursorLine < pLineCount Then
                    SetCursorPosition(pCursorLine, pTextLines(pCursorLine).Length)
                End If
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorEnd error: {ex.Message}")
            End Try
        End Sub
        
        ' Page navigation
        Private Sub MoveCursorPageUp(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                Dim lNewLine As Integer = Math.Max(0, pCursorLine - pTotalVisibleLines)
                PreserveDesiredColumn(Sub() SetCursorPosition(lNewLine, pDesiredColumn))
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorPageUp error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveCursorPageDown(vExtendSelection As Boolean)
            Try
                If vExtendSelection Then
                    ' Start selection if needed
                    If Not pHasSelection Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                End If
                
                Dim lNewLine As Integer = Math.Min(pLineCount - 1, pCursorLine + pTotalVisibleLines)
                PreserveDesiredColumn(Sub() SetCursorPosition(lNewLine, pDesiredColumn))
                
                If vExtendSelection Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                ElseIf pHasSelection Then
                    ClearSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveCursorPageDown error: {ex.Message}")
            End Try
        End Sub

        ' Get the word at the current cursor position
        Public Function GetWordAtCursor() As String Implements IEditor.GetWordAtCursor
            Try
                If pCursorLine >= pLineCount Then Return ""
                
                Dim lLine As String = pTextLines(pCursorLine)
                If String.IsNullOrEmpty(lLine) OrElse pCursorColumn > lLine.Length Then
                    Return ""
                End If
                
                ' Find word boundaries (reuse logic from SelectWordAtCursor)
                Dim lStartCol As Integer = pCursorColumn
                Dim lEndCol As Integer = pCursorColumn
                
                ' Handle case where cursor is at end of line
                If lStartCol >= lLine.Length Then
                    lStartCol = lLine.Length - 1
                    If lStartCol < 0 Then Return ""
                End If
                
                ' Handle case where cursor is on a non-word character
                If lStartCol < lLine.Length AndAlso Not IsWordChar(lLine(lStartCol)) Then
                    ' Check if previous character is a word character
                    If lStartCol > 0 AndAlso IsWordChar(lLine(lStartCol - 1)) Then
                        lStartCol -= 1
                        lEndCol = lStartCol
                    Else
                        Return ""
                    End If
                End If
                
                ' Move start back to beginning of word
                While lStartCol > 0 AndAlso IsWordChar(lLine(lStartCol - 1))
                    lStartCol -= 1
                End While
                
                ' Move end forward to end of word
                While lEndCol < lLine.Length AndAlso IsWordChar(lLine(lEndCol))
                    lEndCol += 1
                End While
                
                ' Extract the word
                If lEndCol > lStartCol Then
                    Return lLine.Substring(lStartCol, lEndCol - lStartCol)
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetWordAtCursor error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' Get the current cursor position as an EditorPosition
        Public Function GetCursorPosition() As EditorPosition Implements IEditor.GetCursorPosition
            Try
                Return New EditorPosition(pCursorLine, pCursorColumn)
                
            Catch ex As Exception
                Console.WriteLine($"GetCursorPosition error: {ex.Message}")
                Return New EditorPosition(0, 0)
            End Try
        End Function
        
        ' ===== Selection Operations =====
        
        Public Sub SelectAll() Implements IEditor.SelectAll
            Try
                If pLineCount > 0 Then
                    pSelectionStartLine = 0
                    pSelectionStartColumn = 0
                    pSelectionEndLine = pLineCount - 1
                    pSelectionEndColumn = pTextLines(pLineCount - 1).Length
                    pSelectionActive = True
                    pHasSelection = True
                    
                    SetCursorPosition(pSelectionEndLine, pSelectionEndColumn)
                    
                    RaiseEvent SelectionChanged(True)
                    pDrawingArea.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectAll error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub SetSelection(vStartPosition as EditorPosition, vEndPosition as EditorPosition) Implements IEditor.SetSelection
            Try
                ' Validate and clamp ranges
                Dim vStartLine As Integer = Math.Max(0, Math.Min(vStartPosition.Line, pLineCount - 1))
                Dim vEndLine As Integer = Math.Max(0, Math.Min(vEndPosition.Line, pLineCount - 1))
                Dim vStartColumn as Integer = vStartPosition.Column
                Dim vEndColumn As Integer = vEndPosition.Column
                If vStartLine < pLineCount Then
                    vStartColumn = Math.Max(0, Math.Min(vStartColumn, pTextLines(vStartLine).Length))
                End If
                
                If vEndLine < pLineCount Then
                    vEndColumn = Math.Max(0, Math.Min(vEndColumn, pTextLines(vEndLine).Length))
                End If
                
                ' Set selection
                pSelectionStartLine = vStartLine
                pSelectionStartColumn = vStartColumn
                pSelectionEndLine = vEndLine
                pSelectionEndColumn = vEndColumn
                pSelectionActive = True
                pHasSelection = True
                
                ' Move cursor to end of selection
                SetCursorPosition(vEndLine, vEndColumn)
                
                ' Raise event and redraw
                RaiseEvent SelectionChanged(True)
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SetSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Selects an entire line including the newline character
        ''' </summary>
        ''' <param name="vLine">Line number to select (0-based)</param>
        Public Sub SelectLine(vLine As Integer) Implements IEditor.SelectLine
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Clear any drag state to prevent interference
                pIsDragging = False
                pPotentialDrag = False
                pIsStartingNewSelection = False
                
                ' Select entire line including newline
                pSelectionStartLine = vLine
                pSelectionStartColumn = 0
                
                If vLine < pLineCount - 1 Then
                    ' Not last line - select to start of next line (includes newline)
                    pSelectionEndLine = vLine + 1
                    pSelectionEndColumn = 0
                    ' Move cursor to start of next line
                    SetCursorPosition(vLine + 1, 0)
                Else
                    ' Last line - select to end
                    pSelectionEndLine = vLine
                    pSelectionEndColumn = pTextLines(vLine).Length
                    ' Move cursor to end of line
                    SetCursorPosition(vLine, pTextLines(vLine).Length)
                End If
                
                ' Mark as having selection
                pSelectionActive = True
                pHasSelection = True
                
                ' Raise event and redraw
                RaiseEvent SelectionChanged(True)
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SelectLine error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub SelectLines(vStartLine As Integer, vEndLine As Integer) Implements IEditor.SelectLines
            Try
                ' Validate range
                vStartLine = Math.Max(0, Math.Min(vStartLine, pLineCount - 1))
                vEndLine = Math.Max(0, Math.Min(vEndLine, pLineCount - 1))
                
                ' Ensure proper order
                If vStartLine > vEndLine Then
                    Dim lTemp As Integer = vStartLine
                    vStartLine = vEndLine
                    vEndLine = lTemp
                End If
                
                ' Select from start of first line
                StartSelection(vStartLine, 0)
                
                If vEndLine < pLineCount - 1 Then
                    ' Not including last line - select to start of line after end
                    UpdateSelection(vEndLine + 1, 0)
                    SetCursorPosition(vEndLine + 1, 0)
                Else
                    ' Including last line - select to end of last line
                    Dim lLength As Integer = pTextLines(vEndLine).Length
                    UpdateSelection(vEndLine, lLength)
                    SetCursorPosition(vEndLine, lLength)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectLines error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== GetSelectedText Implementation =====
        
        Public Function GetSelectedText() As String Implements IEditor.GetSelectedText
            Try
                If Not pHasSelection Then Return ""
                
                ' Normalize selection
                Dim lStartLine As Integer = pSelectionStartLine
                Dim lStartColumn As Integer = pSelectionStartColumn
                Dim lEndLine As Integer = pSelectionEndLine
                Dim lEndColumn As Integer = pSelectionEndColumn
                Dim lStartPos As New EditorPosition(lStartLine, lStartColumn)
                Dim lEndPos As New EditorPosition(lEndLine, lEndColumn)
                NormalizeSelection(lStartPos, lEndPos)
                lStartLine = lStartPos.Line
                lStartColumn = lStartPos.Column
                lEndLine = lEndPos.Line
                lEndColumn = lEndPos.Column
                
                Dim lText As New System.Text.StringBuilder()
                
                If lStartLine = lEndLine Then
                    ' Single line selection
                    If lStartLine < pLineCount Then
                        Dim lLine As String = pTextLines(lStartLine)
                        Dim lLength As Integer = Math.Min(lEndColumn - lStartColumn, lLine.Length - lStartColumn)
                        If lLength > 0 Then
                            lText.Append(lLine.Substring(lStartColumn, lLength))
                        End If
                    End If
                Else
                    ' Multi-line selection
                    ' First line
                    If lStartLine < pLineCount Then
                        Dim lLine As String = pTextLines(lStartLine)
                        If lStartColumn < lLine.Length Then
                            lText.AppendLine(lLine.Substring(lStartColumn))
                        Else
                            lText.AppendLine()
                        End If
                    End If
                    
                    ' Middle lines
                    For i As Integer = lStartLine + 1 To lEndLine - 1
                        If i < pLineCount Then
                            lText.AppendLine(pTextLines(i))
                        End If
                    Next
                    
                    ' Last line
                    If lEndLine < pLineCount Then
                        Dim lLine As String = pTextLines(lEndLine)
                        If lEndColumn > 0 Then
                            lText.Append(lLine.Substring(0, Math.Min(lEndColumn, lLine.Length)))
                        End If
                    End If
                End If
                
                Return lText.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"GetSelectedText error: {ex.Message}")
                Return ""
            End Try
        End Function

        ' Get cursor X position in screen coordinates
        Private Function GetCursorScreenX() As Integer
            Try
                ' Calculate based on current column and horizontal scroll
                Dim lX As Integer = (pCursorColumn - pFirstVisibleColumn) * pCharWidth
                Return Math.Max(0, lX)
                
            Catch ex As Exception
                Console.WriteLine($"GetCursorScreenX error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ' Get cursor Y position in screen coordinates
        Private Function GetCursorScreenY() As Integer
            Try
                ' Calculate based on current line and vertical scroll
                Dim lY As Integer = (pCursorLine - pFirstVisibleLine) * pLineHeight
                Return Math.Max(0, lY)
                
            Catch ex As Exception
                Console.WriteLine($"GetCursorScreenY error: {ex.Message}")
                Return 0
            End Try
        End Function

        Private Sub EnsureCursorsCreated()
            If pPointerCursor Is Nothing Then
                pPointerCursor = New Cursor(Display.Default, CursorType.Arrow)
            End If
            
            If pTextCursor Is Nothing Then
                pTextCursor = New Cursor(Display.Default, CursorType.Xterm)
            End If
            If pDragCursor Is Nothing Then
                pDragCursor = New Cursor(Display.Default, CursorType.Crosshair)
            End If
            pLineNumberArea.Window.Cursor = pPointerCursor
            pDrawingArea.Window.Cursor = pTextCursor
        End Sub

        ''' <summary>
        ''' Gets the current cursor line position (0-based)
        ''' </summary>
        Public ReadOnly Property CursorLine As Integer
            Get
                Return pCursorLine
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the current cursor column position (0-based)
        ''' </summary>
        Public ReadOnly Property CursorColumn As Integer
            Get
                Return pCursorColumn
            End Get
        End Property            
        
    End Class
    
End Namespace
