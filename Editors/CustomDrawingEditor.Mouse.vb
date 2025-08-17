' Editors/CustomDrawingEditor.Mouse.vb - Mouse event handlers with fixed GetColumnFromX
Imports System
Imports Gtk
Imports Gdk
Imports SimpleIDE.Interfaces

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Mouse State =====
        Private pIsDragging As Boolean = False
        Private pLineNumberDragging As Boolean = False
        Private pLastMouseX As Double = 0
        Private pLastMouseY As Double = 0
        Private pDragScrollTimer As UInteger = 0
        
        ' ===== Button Press Event =====
        Private Function OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                ' Grab focus
                pDrawingArea.GrabFocus()
                
                ' Get position
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.Event.x, vArgs.Event.y)
                
                Select Case vArgs.Event.Button
                    Case 1 ' Left button
                        If vArgs.Event.Type = EventType.ButtonPress Then
                            ' Single click
                            HandleLeftClick(lPos, vArgs.Event.State)
                        ElseIf vArgs.Event.Type = EventType.TwoButtonPress Then
                            ' Double click - select word
                            SelectWordAt(lPos.Line, lPos.Column)
                        ElseIf vArgs.Event.Type = EventType.ThreeButtonPress Then
                            ' Triple click - select line
                            SelectLine(lPos.Line)
                        End If
                        
                    Case 3 ' Right button
                        ' Show context menu for text area
                        ShowTextAreaContextMenu(vArgs.Event.x, vArgs.Event.y)
                End Select
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Button Release Event =====
        Private Function OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                ' Stop dragging
                pIsDragging = False
                
                ' Stop drag scroll timer
                If pDragScrollTimer > 0 Then
                    GLib.Source.Remove(pDragScrollTimer)
                    pDragScrollTimer = 0
                End If
                
                ' Clear selection if no drag occurred
                If pSelectionActive AndAlso Not pHasSelection Then
                    ClearSelection()
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' CRITICAL FIX: Integrate drag-drop detection with selection dragging
        Private Function OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                ' Store current mouse position
                Dim lCurrentX As Double = vArgs.Event.x
                Dim lCurrentY As Double = vArgs.Event.y
                
                ' Check if we should start a drag-drop operation
                ' This happens when:
                ' 1. We're dragging (mouse button down)
                ' 2. We have a selection
                ' 3. The mouse started within the selection
                ' 4. We've moved enough pixels to start a drag
                If pIsDragging AndAlso pHasSelection Then
                    ' Check if the drag started within the existing selection
                    Dim lStartPos As EditorPosition = GetPositionFromCoordinates(pLastMouseX, pLastMouseY)
                    If IsPositionInSelection(lStartPos.Line, lStartPos.Column) Then
                        ' Check if we've moved enough to start a drag (5 pixel threshold)
                        Dim lDeltaX As Double = Math.Abs(lCurrentX - pLastMouseX)
                        Dim lDeltaY As Double = Math.Abs(lCurrentY - pLastMouseY)
                        
                        If lDeltaX >= 5 OrElse lDeltaY >= 5 Then
                            ' Start drag-drop operation
                            Console.WriteLine("Starting drag-drop from selection")
                            
                            ' Store drag data
                            pDragData = GetSelectedText()
                            If Not String.IsNullOrEmpty(pDragData) Then
                                ' Set drag source information
                                pDragStarted = True
                                pIsDragSource = True
                                pDragStartLine = pSelectionStartLine
                                pDragStartColumn = pSelectionStartColumn
                                pDragEndLine = pSelectionEndLine
                                pDragEndColumn = pSelectionEndColumn
                                
                                ' Normalize selection for proper drag behavior
                                Dim lStartLine As Integer = pDragStartLine
                                Dim lStartColumn As Integer = pDragStartColumn
                                Dim lEndLine As Integer = pDragEndLine
                                Dim lEndColumn As Integer = pDragEndColumn
                                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                                pDragStartLine = lStartLine
                                pDragStartColumn = lStartColumn
                                pDragEndLine = lEndLine
                                pDragEndColumn = lEndColumn
                                
                                ' Create target list for drag operation
                                ' CRITICAL: Use the same targets as defined in DragDrop.vb
                                Dim lTargets() As TargetEntry = {
                                    New TargetEntry("text/plain", TargetFlags.App Or TargetFlags.OtherWidget, 0),
                                    New TargetEntry("application/vb-code", TargetFlags.App, 1)
                                }
                                Dim lTargetList As New TargetList(lTargets)
                                
                                ' Start the drag operation
                                Dim lContext As Gdk.DragContext = Gtk.Drag.Begin(
                                    pDrawingArea,
                                    lTargetList,
                                    DragAction.Copy Or DragAction.Move,
                                    1, ' Button 1
                                    Gtk.Global.CurrentEvent) ' Use current event
                                
                                If lContext IsNot Nothing Then
                                    Console.WriteLine($"Drag-drop started with {pDragData.Length} characters")
                                    ' Stop normal selection dragging
                                    pIsDragging = False
                                    
                                    ' Stop drag scroll timer if active
                                    If pDragScrollTimer > 0 Then
                                        GLib.Source.Remove(pDragScrollTimer)
                                        pDragScrollTimer = 0
                                    End If
                                    
                                    vArgs.RetVal = True
                                    Return True
                                End If
                            End If
                        End If
                    End If
                End If
                
                ' Store mouse position for next motion event
                pLastMouseX = lCurrentX
                pLastMouseY = lCurrentY
                
                ' Handle normal selection dragging
                If pIsDragging Then
                    ' Get position under mouse
                    Dim lPos As EditorPosition = GetPositionFromCoordinates(lCurrentX, lCurrentY)
                    
                    ' Update selection
                    UpdateSelection(lPos.Line, lPos.Column)
                    SetCursorPosition(lPos.Line, lPos.Column)
                    
                    ' Start auto-scroll timer if near edges
                    If pDragScrollTimer = 0 Then
                        pDragScrollTimer = GLib.Timeout.Add(50, AddressOf OnDragScrollTimer)
                    End If
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        

        
        ' ===== Line Number Area Mouse Events =====
        Private Function OnLineNumberButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                ' IMPORTANT: Grab focus to the drawing area so keyboard shortcuts work
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.GrabFocus()
                End If

                If vArgs.Event.Button = 1 Then
                    ' Left click - existing line selection logic
                    Dim lLine As Integer = GetLineFromY(vArgs.Event.y)
                    
                    If lLine >= 0 AndAlso lLine < pLineCount Then
                        ' Select entire line
                        SelectLine(lLine)
                        pLineNumberDragging = True
                    End If
                ElseIf vArgs.Event.Button = 3 Then
                    ' Right click - show line number context menu
                    Console.WriteLine($"Right click detected in line number area at Y: {vArgs.Event.y}")
                    ShowLineNumberContextMenu(vArgs.Event.x, vArgs.Event.y)
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnLineNumberButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function OnLineNumberMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                If pLineNumberDragging Then
                    Dim lLine As Integer = GetLineFromY(vArgs.Event.y)
                    
                    If lLine >= 0 AndAlso lLine < pLineCount Then
                        ' Extend selection to include this line
                        ExtendLineSelection(lLine)
                    End If
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnLineNumberMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function OnLineNumberButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                pLineNumberDragging = False
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnLineNumberButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Helper Methods =====
        Private Function GetPositionFromCoordinates(vX As Double, vY As Double) As EditorPosition
            Try
                ' Calculate line from Y coordinate
                Dim lLine As Integer = GetLineFromY(vY) 
                
                ' Calculate column from X coordinate - FIXED: pass the line!
                Dim lColumn As Integer = GetColumnFromX(vX, lLine)
                
                Return New EditorPosition(lLine, lColumn)
                
            Catch ex As Exception
                Console.WriteLine($"GetPositionFromCoordinates error: {ex.Message}")
                Return New EditorPosition(0, 0)
            End Try
        End Function
        
        Private Function GetLineFromY(vY As Double) As Integer
            Try
                ' Get font ascent for consistent positioning with drawing
                Dim lAscent As Integer = 0
                If pFontMetrics IsNot Nothing Then
                    lAscent = pFontMetrics.Ascent
                Else
                    lAscent = CInt(pLineHeight * 0.75)
                End If
                
                ' Account for top padding, ascent offset, and scroll position
                ' This matches the drawing calculation in DrawContent
                Dim lAdjustedY As Double = vY - pTopPadding - lAscent + pLineHeight
                Dim lLine As Integer = CInt(Math.Floor(lAdjustedY / pLineHeight)) + pFirstVisibleLine
                
                ' Clamp to valid range
                Return Math.Max(0, Math.Min(lLine, pLineCount - 1))
                
            Catch ex As Exception
                Console.WriteLine($"GetLineFromY error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        Private Function GetColumnFromX(vX As Double, vLine As Integer) As Integer
            Try
                ' Account for left padding and scroll position
                Dim lColumn As Integer = CInt(Math.Floor((vX - pLeftPadding) / pCharWidth)) + pFirstVisibleColumn
                
                ' Clamp to valid range for the specified line (FIXED: not cursor line!)
                If vLine >= 0 AndAlso vLine < pLineCount Then
                    lColumn = Math.Max(0, Math.Min(lColumn, pTextLines(vLine).Length))
                Else
                    lColumn = 0
                End If
                
                Return lColumn
                
            Catch ex As Exception
                Console.WriteLine($"GetColumnFromX error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        Private Sub HandleLeftClick(vPos As EditorPosition, vModifiers As ModifierType)
            Try
                If (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    ' Shift+Click: extend selection
                    If Not pSelectionActive Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                    UpdateSelection(vPos.Line, vPos.Column)
                    SetCursorPosition(vPos.Line, vPos.Column)
                Else
                    ' Regular click: clear selection and move cursor
                    ClearSelection()
                    SetCursorPosition(vPos.Line, vPos.Column)
                    
                    ' Start potential drag selection
                    StartSelection(vPos.Line, vPos.Column)
                    pIsDragging = True
                End If
                
                ' Ensure cursor is visible
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"HandleLeftClick error: {ex.Message}")
            End Try
        End Sub

        Private Sub SelectWordAt(vLine As Integer, vColumn As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                Dim lLineText As String = pTextLines(vLine)
                If vColumn < 0 OrElse vColumn > lLineText.Length Then Return
                
                ' Find word boundaries
                Dim lStart As Integer = vColumn
                Dim lEnd As Integer = vColumn
                
                ' Move start back to beginning of word
                While lStart > 0 AndAlso Char.IsLetterOrDigit(lLineText(lStart - 1))
                    lStart -= 1
                End While
                
                ' Move end forward to end of word
                While lEnd < lLineText.Length AndAlso Char.IsLetterOrDigit(lLineText(lEnd))
                    lEnd += 1
                End While
                
                ' Select word
                If lEnd > lStart Then
                    StartSelection(vLine, lStart)
                    UpdateSelection(vLine, lEnd)
                    SetCursorPosition(vLine, lEnd)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectWordAt error: {ex.Message}")
            End Try
        End Sub
        
        
        Private Sub ExtendLineSelection(vToLine As Integer)
            Try
                ' Determine selection direction
                If vToLine >= pSelectionStartLine Then
                    ' Selecting downward
                    UpdateSelection(vToLine, pTextLines(vToLine).Length)
                    SetCursorPosition(vToLine, pTextLines(vToLine).Length)
                Else
                    ' Selecting upward
                    UpdateSelection(vToLine, 0)
                    SetCursorPosition(vToLine, 0)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExtendLineSelection error: {ex.Message}")
            End Try
        End Sub
        
        
        ' ===== Drag Scroll Timer =====
        Private Function OnDragScrollTimer() As Boolean
            Try
                If Not pIsDragging Then
                    pDragScrollTimer = 0
                    Return False ' Stop timer
                End If
                
                ' Check if mouse is near edges and scroll accordingly
                AutoScrollIfNearEdge(pLastMouseX, pLastMouseY)
                
                ' Update selection to current mouse position
                Dim lPos As EditorPosition = GetPositionFromCoordinates(pLastMouseX, pLastMouseY)
                UpdateSelection(lPos.Line, lPos.Column)
                SetCursorPosition(lPos.Line, lPos.Column)
                
                Return True ' Continue timer
                
            Catch ex As Exception
                Console.WriteLine($"OnDragScrollTimer error: {ex.Message}")
                Return False
            End Try
        End Function

        ' SelectWord - Select the word at the current cursor position
        Public Sub SelectWord() Implements IEditor.SelectWord
            Try
                If pIsReadOnly Then Return
                
                ' Get current line
                If pCursorLine >= pLineCount Then Return
                Dim lLine As String = pTextLines(pCursorLine)
                
                ' Find word boundaries
                Dim lStartColumn As Integer = pCursorColumn
                Dim lEndColumn As Integer = pCursorColumn
                
                ' If cursor is at end of line or on whitespace, try to find a word
                If pCursorColumn >= lLine.Length OrElse (pCursorColumn < lLine.Length AndAlso Char.IsWhiteSpace(lLine(pCursorColumn))) Then
                    ' Look backward for a word
                    lStartColumn = pCursorColumn - 1
                    While lStartColumn >= 0 AndAlso Char.IsWhiteSpace(lLine(lStartColumn))
                        lStartColumn -= 1
                    End While
                    
                    If lStartColumn < 0 Then
                        ' No word found
                        Return
                    End If
                    
                    lEndColumn = lStartColumn + 1
                End If
                
                ' Find start of word
                While lStartColumn > 0 AndAlso IsWordCharacter(lLine(lStartColumn - 1))
                    lStartColumn -= 1
                End While
                
                ' Find end of word
                While lEndColumn < lLine.Length AndAlso IsWordCharacter(lLine(lEndColumn))
                    lEndColumn += 1
                End While
                
                ' Select the word
                If lEndColumn > lStartColumn Then
                    SetSelection(pCursorLine, lStartColumn, pCursorLine, lEndColumn)
                    
                    ' Move cursor to end of selection
                    SetCursorPosition(pCursorLine, lEndColumn)
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                    
                    ' Raise selection changed event
                    RaiseEvent SelectionChanged(True)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectWord error: {ex.Message}")
            End Try
        End Sub
        
        ' Helper method to determine if a character is part of a word
        Private Function IsWordCharacter(vChar As Char) As Boolean
            Return Char.IsLetterOrDigit(vChar) OrElse vChar = "_"c
        End Function
        
    End Class
    
End Namespace
