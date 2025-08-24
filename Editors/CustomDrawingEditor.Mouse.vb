' Editors/CustomDrawingEditor.Mouse.vb - Mouse event handlers with fixed GetColumnFromX
Imports System
Imports Gtk
Imports Gdk
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Mouse State =====
        Private pIsDragging As Boolean = False
        Private pTextDragAnchorLine As Integer = -1     
        Private pTextDragAnchorColumn As Integer = -1  
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
        
        ''' <summary>
        ''' Handles button press events on the drawing area
        ''' </summary>
        Private Function OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                pDrawingArea.GrabFocus()
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.Event.X, vArgs.Event.Y)
                
                ' Store last click position for drag detection
                pLastMouseX = vArgs.Event.X
                pLastMouseY = vArgs.Event.Y
                
                Select Case vArgs.Event.Button
                    Case 1 ' Left button
                        ' IMPORTANT: Handle multi-click events properly
                        ' GTK fires all events in sequence (single, double, triple)
                        ' We need to handle the final event type only
                        
                        If vArgs.Event.Type = EventType.ThreeButtonPress Then
                            ' Triple click - select entire line
                            ' Don't set up drag state for triple-click
                            pIsDragging = False
                            pPotentialDrag = False
                            pIsStartingNewSelection = False
                            SelectLine(lPos.Line)
                            
                        ElseIf vArgs.Event.Type = EventType.TwoButtonPress Then
                            ' Double click - select word
                            ' Don't set up drag state for double-click
                            pIsDragging = False
                            pPotentialDrag = False
                            pIsStartingNewSelection = False
                            SelectWordAt(lPos.Line, lPos.Column)
                            
                        ElseIf vArgs.Event.Type = EventType.ButtonPress Then
                            ' Single click only - handle normally
                            HandleLeftClick(lPos, vArgs.Event.State)
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
                    pTextDragAnchorLine = -1     ' CRITICAL FIX: Reset text area anchors
                    pTextDragAnchorColumn = -1
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
        
        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.OnMotionNotify
        ''' <summary>
        ''' Handles mouse motion events - delegates drag operations to DragDrop handlers
        ''' </summary>
        Private Function OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                Dim lCurrentX As Double = vArgs.Event.X
                Dim lCurrentY As Double = vArgs.Event.Y
                
                ' Check if left button is pressed
                Dim lLeftButtonPressed As Boolean = (vArgs.Event.State And ModifierType.Button1Mask) = ModifierType.Button1Mask
                
                ' Debug logging
                If pPotentialDrag OrElse pIsStartingNewSelection Then
                    Console.WriteLine($"OnMotionNotify: potential drag={pPotentialDrag}, starting selection={pIsStartingNewSelection}, LeftButton={lLeftButtonPressed}")
                End If
                
                ' If drag operation already started, let DragDrop handlers manage it
                If pDragStarted Then
                    ' The HandleDragMotion in DragDrop.vb handles cursor changes
                    Return False
                End If
                
                ' Update cursor based on position when not dragging
                If Not pIsDragging AndAlso Not pIsStartingNewSelection Then
                    Dim lPos As EditorPosition = GetPositionFromCoordinates(lCurrentX, lCurrentY)
                    
                    ' Check if over a selection - use the correct overload
                    If pHasSelection AndAlso IsPositionInSelection(lPos.Line, lPos.Column,
                                                                   pSelectionStartLine, pSelectionStartColumn,
                                                                   pSelectionEndLine, pSelectionEndColumn) Then
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
                ' Only if left button is pressed, we have a potential drag, and there's a selection
                If lLeftButtonPressed AndAlso pPotentialDrag AndAlso pHasSelection Then
                    ' Check if we've moved enough to start drag
                    Dim lDeltaX As Double = Math.Abs(lCurrentX - pLastMouseX)
                    Dim lDeltaY As Double = Math.Abs(lCurrentY - pLastMouseY)
                    
                    Console.WriteLine($"Checking drag threshold: deltaX={lDeltaX}, deltaY={lDeltaY}")
                    
                    If lDeltaX >= 5.0 OrElse lDeltaY >= 5.0 Then
                        ' Prepare drag data for HandleDragBegin to use
                        pDragData = GetSelectedText()
                        
                        Console.WriteLine($"Starting drag with {pDragData.Length} characters")
                        
                        If Not String.IsNullOrEmpty(pDragData) Then
                            ' Set up drag state (HandleDragBegin will read these)
                            pDragStartLine = pSelectionStartLine
                            pDragStartColumn = pSelectionStartColumn
                            pDragEndLine = pSelectionEndLine
                            pDragEndColumn = pSelectionEndColumn
                            
                            ' Start GTK drag - HandleDragBegin will take over
                            Dim lTargetList As New TargetList(DRAG_TARGETS)
                            Dim lContext As DragContext = Gtk.Drag.BeginWithCoordinates(
                                pDrawingArea, 
                                lTargetList, 
                                DragAction.Move Or DragAction.Copy,
                                1,                    ' Button 1 (left mouse)
                                vArgs.Event,          ' The motion event
                                CInt(lCurrentX),      ' X coordinate as integer
                                CInt(lCurrentY))      ' Y coordinate as integer
                            
                            If lContext IsNot Nothing Then
                                Console.WriteLine("Drag context created successfully")
                            Else
                                Console.WriteLine("Failed to create drag context")
                            End If
                            
                            ' Reset local mouse drag state
                            pIsDragging = False
                            pPotentialDrag = False
                            pIsStartingNewSelection = False
                            Return True
                        End If
                    End If
                    
                    ' Still waiting for drag threshold - don't process as selection
                    Return True
                End If
                
                ' Handle text selection dragging
                If lLeftButtonPressed AndAlso (pIsStartingNewSelection OrElse (pIsDragging AndAlso Not pPotentialDrag)) Then
                    Dim lPos As EditorPosition = GetPositionFromCoordinates(lCurrentX, lCurrentY)
                    
                    ' CRITICAL FIX: Use text drag anchors for selection
                    If pTextDragAnchorLine >= 0 AndAlso pTextDragAnchorColumn >= 0 Then
                        ' Check if we've moved enough to start selecting
                        Dim lDeltaX As Double = Math.Abs(lCurrentX - pLastMouseX)
                        Dim lDeltaY As Double = Math.Abs(lCurrentY - pLastMouseY)
                        
                        ' Start selection if we've moved at least 2 pixels OR already selecting
                        If pHasSelection OrElse lDeltaX >= 2.0 OrElse lDeltaY >= 2.0 Then
                            If Not pHasSelection Then
                                ' First significant motion after button press - start selection
                                pHasSelection = True
                                pSelectionActive = True  ' CRITICAL: Set this flag for drawing!
                                pIsDragging = True
                                pIsStartingNewSelection = False
                                Console.WriteLine($"Started text selection from ({pTextDragAnchorLine},{pTextDragAnchorColumn})")
                            End If
                            
                            ' Always use anchor as start, current position as end
                            pSelectionStartLine = pTextDragAnchorLine
                            pSelectionStartColumn = pTextDragAnchorColumn
                            pSelectionEndLine = lPos.Line
                            pSelectionEndColumn = lPos.Column
                            
                            Console.WriteLine($"Selection now from ({pSelectionStartLine},{pSelectionStartColumn}) to ({pSelectionEndLine},{pSelectionEndColumn})")
                            
                            ' Auto-scroll if near edges
                            AutoScrollIfNearEdge(lCurrentX, lCurrentY)
                            
                            ' Queue redraw for selection
                            pDrawingArea.QueueDraw()
                        End If
                    End If
                End If
                
                ' Update last mouse position for next motion event
                pLastMouseX = lCurrentX
                pLastMouseY = lCurrentY
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnMotionNotify error: {ex.Message}")
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
        ''' Gets line from Y coordinate in the line number area widget
        ''' </summary>
        ''' <param name="vY">Y coordinate in pixels (relative to line number widget)</param>
        ''' <returns>Line number (0-based)</returns>
        Private Function GetLineFromY(vY As Double) As Integer
            Try
                ' CRITICAL FIX: When called from line number area events,
                ' the Y coordinate is relative to the line number widget (starts at 0),
                ' NOT the drawing area. We should NOT subtract pTopPadding here.
                
                ' The line number area is a separate widget with its own coordinate system.
                ' Y=0 is the top of the line number area widget.
                ' We need to account for scrolling but NOT padding.
                
                ' Calculate which line this Y coordinate represents
                ' Directly divide by line height without subtracting padding
                Dim lWidgetLine As Integer = CInt(Math.Floor(vY / pLineHeight))
                
                ' Add the first visible line to account for vertical scrolling
                Dim lLine As Integer = lWidgetLine + pFirstVisibleLine
                
                ' Clamp to valid range
                lLine = Math.Max(0, Math.Min(lLine, pLineCount - 1))
                
                ' Debug output to verify the calculation
                Console.WriteLine($"GetLineFromY: Y={vY:F1}, WidgetLine={lWidgetLine}, FirstVisible={pFirstVisibleLine}, Result={lLine}, LineHeight={pLineHeight}")
                
                Return lLine
                
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
        
        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.HandleLeftClick
        ''' <summary>
        ''' Fixed HandleLeftClick that properly sets up for drag operations
        ''' </summary>
        Private Sub HandleLeftClick(vPos As EditorPosition, vModifiers As ModifierType)
            Try
                ' Check if we're clicking within an existing selection
                Dim lClickInSelection As Boolean = False
                If pHasSelection Then
                    ' Use the correct overload with all parameters
                    lClickInSelection = IsPositionInSelection(vPos.Line, vPos.Column,
                                                             pSelectionStartLine, pSelectionStartColumn,
                                                             pSelectionEndLine, pSelectionEndColumn)
                    Console.WriteLine($"HandleLeftClick: HasSelection=True, ClickInSelection={lClickInSelection}")
                End If
                
                If (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    ' Shift+Click: extend selection
                    pIsStartingNewSelection = False
                    pPotentialDrag = False
                    pTextDragAnchorLine = -1    ' Not using anchors for shift-click
                    pTextDragAnchorColumn = -1
                    
                    If Not pHasSelection Then
                        ' Start new selection from cursor
                        pSelectionStartLine = pCursorLine
                        pSelectionStartColumn = pCursorColumn
                    End If
                    
                    ' Extend to clicked position
                    pSelectionEndLine = vPos.Line
                    pSelectionEndColumn = vPos.Column
                    pHasSelection = True
                    
                    ' Move cursor to click position
                    SetCursorPosition(vPos.Line, vPos.Column)
                ElseIf (vModifiers And ModifierType.ControlMask) = ModifierType.ControlMask Then
                    ' Ctrl+Click: word selection
                    pIsStartingNewSelection = False
                    pPotentialDrag = False
                    pTextDragAnchorLine = -1
                    pTextDragAnchorColumn = -1
                    
                    SelectWordAt(vPos.Line, vPos.Column)
                    SetCursorPosition(vPos.Line, vPos.Column)
                ElseIf lClickInSelection Then
                    ' Clicking in selection - potential drag operation
                    Console.WriteLine("HandleLeftClick: Setting up potential drag")
                    pPotentialDrag = True
                    pIsDragging = True  ' IMPORTANT: Set this to true for drag detection
                    pIsStartingNewSelection = False
                    pTextDragAnchorLine = vPos.Line
                    pTextDragAnchorColumn = vPos.Column
                    ' Don't clear selection yet - wait to see if it's a drag
                Else
                    ' Regular click - clear selection and prepare for new one
                    ClearSelection()
                    SetCursorPosition(vPos.Line, vPos.Column)
                    
                    ' Set up for potential drag-to-select
                    pIsStartingNewSelection = True
                    pPotentialDrag = False
                    pTextDragAnchorLine = vPos.Line
                    pTextDragAnchorColumn = vPos.Column
                    pSelectionStartLine = vPos.Line
                    pSelectionStartColumn = vPos.Column
                    pSelectionEndLine = vPos.Line
                    pSelectionEndColumn = vPos.Column
                    pHasSelection = False  ' Will be set true when dragging starts
                End If
                
                pDrawingArea.QueueDraw()
                
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
                    SetSelection(New EditorPosition(vLine, lStart), New EditorPosition(vLine, lEnd))
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
                    SetSelection(New EditorPosition(pCursorLine, lStartColumn), New EditorPosition(pCursorLine, lEndColumn))
                    
                    ' Move cursor to end of selection
                    SetCursorPosition(New EditorPosition(pCursorLine, lEndColumn))
                    
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

