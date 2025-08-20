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
        Private pIsStartingNewSelection As Boolean = False  ' Track if we're starting a new selection
        Private pPointerCursor As Cursor
        Private pTextCursor As Cursor
        Private pDragCursor as Cursor



        ' Add this new field to track if we're in a potential drag operation
        Private pPotentialDrag As Boolean = False
        

        ''' <summary>
        ''' Vertical scroll offset in pixels
        ''' </summary>
        Private pScrollY As Integer = 0

        ''' <summary>
        ''' Gets the horizontal scrollbar adjustment
        ''' </summary>
        Private ReadOnly Property pHAdjustment As Adjustment
            Get
                Return If(pHScrollbar?.Adjustment, Nothing)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the vertical scrollbar adjustment
        ''' </summary>
        Private ReadOnly Property pVAdjustment As Adjustment
            Get
                Return If(pVScrollbar?.Adjustment, Nothing)
            End Get
        End Property
        
        ' ===== Button Press Event =====
        Private Function OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                pDrawingArea.GrabFocus()
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.Event.X, vArgs.Event.Y)
                
                ' Store last click position
                pLastMouseX = vArgs.Event.X
                pLastMouseY = vArgs.Event.Y
                
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
                        ShowTextAreaContextMenu(vArgs.Event.X, vArgs.Event.Y)
                End Select
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function

        
        ''' <summary>
        ''' Fixed OnButtonRelease that resets drag state properly
        ''' </summary>
        Private Function OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                If vArgs.Event.Button = 1 Then
                    ' If we were in potential drag mode but didn't drag
                    If pPotentialDrag AndAlso pIsDragging Then
                        ' It was a click in selection without dragging
                        ' Move cursor to click position
                        Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.Event.X, vArgs.Event.Y)
                        ClearSelection()
                        SetCursorPosition(lPos.Line, lPos.Column)
                    End If
                    
                    ' Reset all drag-related state
                    pIsDragging = False
                    pIsStartingNewSelection = False
                    pPotentialDrag = False
                    pDragStarted = False
                    pIsDragSource = False
                    StopDragScrollTimer()
                    
                    ' Restore normal cursor
                    If pDrawingArea.Window IsNot Nothing Then
                        pDrawingArea.Window.Cursor = pTextCursor
                    End If
                End If
                
                vArgs.RetVal = False
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse motion events - delegates drag operations to DragDrop handlers
        ''' </summary>
        Private Function OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                Dim lCurrentX As Double = vArgs.Event.X
                Dim lCurrentY As Double = vArgs.Event.Y
                
                ' If drag operation already started, let DragDrop handlers manage it
                If pDragStarted Then
                    ' The HandleDragMotion in DragDrop.vb handles cursor changes
                    Return False
                End If
                
                ' Update cursor based on position when not dragging
                If Not pIsDragging Then
                    Dim lPos As EditorPosition = GetPositionFromCoordinates(lCurrentX, lCurrentY)
                    
                    ' Check if over a selection
                    If IsPositionInSelection(lPos.Line, lPos.Column) Then
                        ' Show drag cursor when hovering over selection
                        If pDrawingArea.Window IsNot Nothing Then
                            pDrawingArea.Window.Cursor = pDragCursor
                        End If
                    Else
                        ' Show normal text cursor
                        If pDrawingArea.Window IsNot Nothing Then
                            pDrawingArea.Window.Cursor = pTextCursor
                        End If
                    End If
                End If
                
                ' Check if we should initiate a drag-drop operation
                If pIsDragging AndAlso pPotentialDrag AndAlso pHasSelection Then
                    ' Check if we've moved enough to start a drag
                    Dim lDeltaX As Double = Math.Abs(lCurrentX - pLastMouseX)
                    Dim lDeltaY As Double = Math.Abs(lCurrentY - pLastMouseY)
                    
                    If lDeltaX >= 5.0 OrElse lDeltaY >= 5.0 Then
                        ' Prepare drag data for HandleDragBegin to use
                        pDragData = GetSelectedText()
                        
                        If Not String.IsNullOrEmpty(pDragData) Then
                            ' Set up drag state (HandleDragBegin will read these)
                            pDragStartLine = pSelectionStartLine
                            pDragStartColumn = pSelectionStartColumn
                            pDragEndLine = pSelectionEndLine
                            pDragEndColumn = pSelectionEndColumn

                            ' Start GTK drag - HandleDragBegin will take over
                            Dim lTargetList As New TargetList(DRAG_TARGETS)
                           Gtk.Drag.BeginWithCoordinates(pDrawingArea, 
                                         lTargetList, 
                                         DragAction.Move Or DragAction.Copy,
                                         1,                    ' Button 1 (left mouse)
                                         vArgs.Event,          ' The motion event
                                         CInt(lCurrentX),      ' X coordinate as integer
                                         CInt(lCurrentY))      ' Y coordinate as integer             
                            
                            ' Reset local mouse drag state
                            pIsDragging = False
                            pPotentialDrag = False
                            pIsStartingNewSelection = False
                            Return True
                        End If
                    End If
                    
                    ' Still deciding if it's a drag - don't update selection yet
                    Return True
                End If
                
                ' Store mouse position for next motion event
                pLastMouseX = lCurrentX
                pLastMouseY = lCurrentY
                
                ' Handle normal selection dragging (not drag-drop)
                If pIsDragging AndAlso Not pPotentialDrag Then
                    Dim lPos As EditorPosition = GetPositionFromCoordinates(lCurrentX, lCurrentY)
                    
                    If pIsStartingNewSelection Then
                        ' Creating new selection from drag
                        pSelectionStartLine = pSelectionStartLine  ' Already set in OnButtonPress
                        pSelectionStartColumn = pSelectionStartColumn
                        pSelectionEndLine = lPos.Line
                        pSelectionEndColumn = lPos.Column
                        pHasSelection = True
                        pIsStartingNewSelection = False  ' Only set once
                    ElseIf pHasSelection Then
                        ' Extending existing selection
                        pSelectionEndLine = lPos.Line
                        pSelectionEndColumn = lPos.Column
                    End If
                    
                    ' Auto-scroll if near edges
                    AutoScrollIfNearEdge(lCurrentX, lCurrentY)
                    
                    ' Queue redraw for selection
                    pDrawingArea.QueueDraw()
                End If
                
                Return False
                
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
                    ' Right click - show context menu for line number area
                    ShowLineNumberContextMenu(vArgs.Event.x, vArgs.Event.y)
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnLineNumberButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function OnLineNumberButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                If vArgs.Event.Button = 1 Then
                    pLineNumberDragging = False
                End If
                
                vArgs.RetVal = False
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnLineNumberButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function OnLineNumberMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
               pLineNumberArea.Window.Cursor = pPointerCursor
                If pLineNumberDragging Then
                    Dim lLine As Integer = GetLineFromY(vArgs.Event.y)
                    
                    If lLine >= 0 AndAlso lLine < pLineCount Then
                        ' Extend selection to this line
                        Dim lStartLine As Integer = Math.Min(pSelectionStartLine, lLine)
                        Dim lEndLine As Integer = Math.Max(pSelectionStartLine, lLine)
                        
                        ' Select from start of first line to end of last line
                        If lEndLine < pLineCount - 1 Then
                            SetSelection(lStartLine, 0, lEndLine + 1, 0)
                        Else
                            SetSelection(lStartLine, 0, lEndLine, pTextLines(lEndLine).Length)
                        End If
                    End If
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnLineNumberMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Converts pixel coordinates to editor position (line and column)
        ''' </summary>
        ''' <param name="vX">X coordinate in pixels</param>
        ''' <param name="vY">Y coordinate in pixels</param>
        ''' <returns>EditorPosition with line and column</returns>
        Private Function GetPositionFromCoordinates(vX As Double, vY As Double) As EditorPosition
            Try
                ' FIXED: The coordinates are relative to the drawing area widget,
                ' NOT the entire editor. The line number area is a separate widget.
                
                ' Calculate line from Y coordinate
                ' First subtract top padding to get the actual text area Y
                Dim lTextAreaY As Double = vY - pTopPadding
                
                ' Y is in widget space, so divide by line height and add first visible line
                Dim lWidgetLine As Integer = CInt(Math.Floor(lTextAreaY / pLineHeight))
                Dim lLine As Integer = lWidgetLine + pFirstVisibleLine
                
                ' Clamp to valid range
                lLine = Math.Max(0, Math.Min(lLine, pLineCount - 1))
                
                ' Calculate column from X coordinate
                ' X is already relative to the drawing area (line numbers are in a separate widget)
                ' Just subtract left padding to get position in text area
                Dim lTextAreaX As Double = vX - pLeftPadding
                
                ' If X is before the text area (shouldn't happen in drawing area), return column 0
                If lTextAreaX < 0 Then
                    Return New EditorPosition(lLine, 0)
                End If
                
                ' Convert X position to column, accounting for horizontal scroll
                Dim lWidgetColumn As Integer = CInt(Math.Floor(lTextAreaX / pCharWidth))
                Dim lColumn As Integer = lWidgetColumn + pFirstVisibleColumn
                
                ' Clamp column to line length (allow cursor at end of line)
                If lLine >= 0 AndAlso lLine < pLineCount Then
                    lColumn = Math.Max(0, Math.Min(lColumn, pTextLines(lLine).Length))
                Else
                    lColumn = 0
                End If
                
                Return New EditorPosition(lLine, lColumn)
                
            Catch ex As Exception
                Console.WriteLine($"GetPositionFromCoordinates error: {ex.Message}")
                Return New EditorPosition(0, 0)
            End Try
        End Function
        
        ''' <summary>
        ''' Gets line from Y coordinate in widget area
        ''' </summary>
        Private Function GetLineFromY(vY As Double) As Integer
            Try
                ' FIXED: Account for pTopPadding
                ' Subtract top padding to get actual text area Y
                Dim lTextAreaY As Double = vY - pTopPadding
                
                ' Y is in widget space, so divide by line height and add first visible line
                Dim lLine As Integer = CInt(Math.Floor(lTextAreaY / pLineHeight)) + pFirstVisibleLine
                
                ' Clamp to valid range
                Return Math.Max(0, Math.Min(lLine, pLineCount - 1))
                
            Catch ex As Exception
                Console.WriteLine($"GetLineFromY error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Gets column from X coordinate with proper character width handling
        ''' </summary>
        Private Function GetColumnFromX(vX As Double, vLine As Integer) As Integer
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return 0
                
                Dim lLineText As String = pTextLines(vLine)
                If String.IsNullOrEmpty(lLineText) Then Return 0
                
                ' FIXED: X is relative to the drawing area widget, NOT the entire editor
                ' Only subtract left padding, not line number width
                Dim lTextAreaX As Double = vX - pLeftPadding
                
                ' If we're before the text area, return 0
                If lTextAreaX < 0 Then Return 0
                
                ' Calculate column with horizontal scroll offset
                Dim lColumn As Integer = CInt(Math.Floor(lTextAreaX / pCharWidth)) + pFirstVisibleColumn
                
                ' Clamp to line length
                lColumn = Math.Max(0, Math.Min(lColumn, pTextLines(vLine).Length))
                
                Return lColumn
                
            Catch ex As Exception
                Console.WriteLine($"GetColumnFromX error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Fixed HandleLeftClick that properly sets up for drag operations
        ''' </summary>
        Private Sub HandleLeftClick(vPos As EditorPosition, vModifiers As ModifierType)
            Try
                ' Check if we're clicking within an existing selection
                Dim lClickInSelection As Boolean = False
                If pHasSelection Then
                    lClickInSelection = IsPositionInSelection(vPos.Line, vPos.Column)
                End If
                
                If (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    ' Shift+Click: extend selection
                    pIsStartingNewSelection = False
                    pPotentialDrag = False
                    If Not pSelectionActive Then
                        StartSelection(pCursorLine, pCursorColumn)
                    End If
                    UpdateSelection(vPos.Line, vPos.Column)
                    SetCursorPosition(vPos.Line, vPos.Column)
                    pIsDragging = True  ' Extending current selection
                ElseIf lClickInSelection Then
                    ' Clicking within existing selection - this is a potential drag operation
                    Console.WriteLine("Click in selection - potential drag")
                    pIsStartingNewSelection = False
                    pPotentialDrag = True  ' Mark this as a potential drag
                    pIsDragging = True     ' We're in drag mode
                    ' IMPORTANT: Don't clear selection or move cursor
                    ' Don't update selection - keep it as is
                Else
                    ' Regular click outside selection - start new selection
                    Console.WriteLine("Click outside selection - new selection")
                    pIsStartingNewSelection = True
                    pPotentialDrag = False
                    ClearSelection()
                    SetCursorPosition(vPos.Line, vPos.Column)
                    StartSelection(vPos.Line, vPos.Column)
                    pIsDragging = True  ' For selection dragging
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
                If String.IsNullOrEmpty(lLineText) Then Return
                
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
                
                ' Set selection if we found a word
                If lEnd > lStart Then
                    SetSelection(vLine, lStart, vLine, lEnd)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectWordAt error: {ex.Message}")
            End Try
        End Sub
        
            ''' <summary>
            ''' Auto-scrolls the editor during drag operations
            ''' </summary> 
       Private Function OnDragScrollTimer() As Boolean
            Try
                If Not pIsDragging Then
                    pDragScrollTimer = 0
                    Return False
                End If
                
                ' CRITICAL FIX: Use Display.GetPointer instead of obsolete Widget.GetPointer
                ' Get current mouse position relative to drawing area
                Dim lX, lY As Integer
                
                ' Get the display and then get the pointer position
                Dim lDisplay As Gdk.Display = pDrawingArea.Display
                If lDisplay IsNot Nothing Then
                    ' Get the pointer position on the display
                    Dim lScreenX, lScreenY As Integer
                    lDisplay.GetPointer(lScreenX, lScreenY)
                    
                    ' Convert screen coordinates to widget coordinates
                    If pDrawingArea.Window IsNot Nothing Then
                        Dim lOriginX, lOriginY As Integer
                        pDrawingArea.Window.GetOrigin(lOriginX, lOriginY)
                        lX = lScreenX - lOriginX
                        lY = lScreenY - lOriginY
                    End If
                End If                
                ' Check if we need to scroll
                Dim lNeedScroll As Boolean = False
                Dim lAllocation As Rectangle = pDrawingArea.Allocation
                
                ' Vertical scrolling
                If lY < 20 Then
                    ' Scroll up
                    ScrollBy(0, -pLineHeight)
                    lNeedScroll = True
                ElseIf lY > lAllocation.Height - 20 Then
                    ' Scroll down
                    ScrollBy(0, pLineHeight)
                    lNeedScroll = True
                End If
                
                ' Horizontal scrolling
                If lX < 20 Then
                    ' Scroll left
                    ScrollBy(-pCharWidth * 5, 0)
                    lNeedScroll = True
                ElseIf lX > lAllocation.Width - 20 Then
                    ' Scroll right
                    ScrollBy(pCharWidth * 5, 0)
                    lNeedScroll = True
                End If
                
                ' Update selection if we scrolled
                If lNeedScroll Then
                    Dim lPos As EditorPosition = GetPositionFromCoordinates(lX, lY)
                    UpdateSelection(lPos.Line, lPos.Column)
                    SetCursorPosition(lPos.Line, lPos.Column)
                End If
                
                Return True ' Continue timer
                
            Catch ex As Exception
                Console.WriteLine($"OnDragScrollTimer error: {ex.Message}")
                pDragScrollTimer = 0
                Return False
            End Try
        End Function
        
        Private Sub ScrollBy(vDx As Double, vDy As Double)
            Try
                If pHScrollbar.Adjustment IsNot Nothing Then
                    Dim lNewX As Double = Math.Max(0, Math.Min(pHScrollbar.Adjustment.Value + vDx, pHScrollbar.Adjustment.Upper - pHScrollbar.Adjustment.PageSize))
                    pHScrollbar.Adjustment.Value = lNewX
                End If
                
                If pVScrollbar.Adjustment IsNot Nothing Then
                    Dim lNewY As Double = Math.Max(0, Math.Min(pVScrollbar.Adjustment.Value + vDy, pVScrollbar.Adjustment.Upper - pVScrollbar.Adjustment.PageSize))
                    pVScrollbar.Adjustment.Value = lNewY
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ScrollBy error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Safely adds a timeout
        ''' </summary>
        ''' <param name="vTimerId">Reference to the timer ID variable</param>
        ''' <param name="vInterval">Interval in milliseconds</param>
        ''' <param name="vCallback">Callback function</param>
        Private Sub SafeAddTimeout(ByRef vTimerId As UInteger, vInterval As UInteger, vCallback As GLib.TimeoutHandler)
            Try
                ' Remove existing timer if any
                SafeRemoveTimer(vTimerId)
                
                ' Add new timer
                vTimerId = GLib.Timeout.Add(vInterval, vCallback)
                
            Catch ex As Exception
                Console.WriteLine($"SafeAddTimeout error: {ex.Message}")
                vTimerId = 0
            End Try
        End Sub

        ''' <summary>
        ''' Safely stops the drag scroll timer
        ''' </summary>
        Private Sub StopDragScrollTimer()
            Try
                If pDragScrollTimer > 0 Then
                    Dim lTimerId As UInteger = pDragScrollTimer
                    pDragScrollTimer = 0  ' Clear BEFORE removing
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
            Catch ex As Exception
                Console.WriteLine($"StopDragScrollTimer error: {ex.Message}")
            End Try
        End Sub

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

        ''' <summary>
        ''' Safely removes a timer/timeout
        ''' </summary>
        ''' <param name="vTimerId">The timer ID to remove</param>
        Private Sub SafeRemoveTimer(vTimerId As UInteger)
            Try
                If vTimerId > 0 Then
                    Try
                        GLib.Source.Remove(vTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
            Catch ex As Exception
                Console.WriteLine($"SafeRemoveTimer error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Helper method to create the drag target list
        ''' </summary>
        Private Shared Function CreateDragTargets() As TargetList
            Try
                ' Create target entries array
                Dim lTargets() As TargetEntry = {
                    New TargetEntry("text/plain", 0, 0),
                    New TargetEntry("TEXT", 0, 1),
                    New TargetEntry("STRING", 0, 2),
                    New TargetEntry("UTF8_STRING", 0, 3)
                }
                
                ' Create and return the target list
                Dim lTargetList As New TargetList(lTargets)
                Return lTargetList
                
            Catch ex As Exception
                Console.WriteLine($"CreateDragTargets error: {ex.Message}")
                Return New TargetList(New TargetEntry() {})
            End Try
        End Function
        
        ' ===== Update ScrollY on Vertical Scroll =====
        
        ''' <summary>
        ''' Updates the vertical scroll offset when scrollbar changes
        ''' </summary>
        Private Sub UpdateScrollY()
            Try
                If pVScrollbar IsNot Nothing Then
                    ' Update scroll offset in pixels
                    pScrollY = pFirstVisibleLine * pLineHeight
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateScrollY error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Additional Helper Methods =====
        
        ''' <summary>
        ''' Gets the adjusted line from Y coordinate accounting for scroll
        ''' </summary>
        ''' <param name="vY">Y coordinate in pixels</param>
        ''' <returns>Line number (0-based)</returns>
        Private Function GetLineFromYWithScroll(vY As Double) As Integer
            Try
                ' Account for vertical scroll
                Dim lAdjustedY As Double = vY + (pFirstVisibleLine * pLineHeight)
                
                ' Calculate line
                Dim lLine As Integer = CInt(Math.Floor(lAdjustedY / pLineHeight))
                
                ' Clamp to valid range
                Return Math.Max(0, Math.Min(lLine, pLineCount - 1))
                
            Catch ex As Exception
                Console.WriteLine($"GetLineFromYWithScroll error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the adjusted column from X coordinate accounting for scroll
        ''' </summary>
        ''' <param name="vX">X coordinate in pixels</param>
        ''' <param name="vLine">Line number to get column for</param>
        ''' <returns>Column number (0-based)</returns>
        Private Function GetColumnFromXWithScroll(vX As Double, vLine As Integer) As Integer
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return 0
                
                ' Account for horizontal scroll
                Dim lAdjustedX As Double = vX + (pFirstVisibleColumn * pCharWidth)
                
                ' Calculate column
                Dim lColumn As Integer = CInt(Math.Floor(lAdjustedX / pCharWidth))
                
                ' Clamp to line length
                If vLine >= 0 AndAlso vLine < pLineCount Then
                    lColumn = Math.Max(0, Math.Min(lColumn, pTextLines(vLine).Length))
                Else
                    lColumn = 0
                End If
                
                Return lColumn
                
            Catch ex As Exception
                Console.WriteLine($"GetColumnFromXWithScroll error: {ex.Message}")
                Return 0
            End Try
        End Function
        
    End Class
    
End Namespace
