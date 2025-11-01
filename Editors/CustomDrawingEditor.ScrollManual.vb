' Editors/CustomDrawingEditor.ScrollManual.vb - Manual scrollbar implementation (FIXED)
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Scrollbar Constants =====
        Private Const MINIMUM_THUMB_SIZE As Integer = 20
        Private Const SCROLL_WHEEL_LINES As Integer = 3
        Private Const PAGE_SCROLL_FACTOR As Double = 0.9
        
        ' ===== Scrollbar Value Changed Handlers =====

        ''' <summary>
        ''' Handles vertical scrollbar value changes
        ''' </summary>
        Private Sub OnVScrollbarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                ' The scrollbar value represents the first visible line
                Dim lNewFirstLine As Integer = CInt(Math.Floor(pVScrollbar.Value))
                
                ' Calculate the maximum valid first visible line
                ' This ensures we can see all lines including the last one
                Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                
                ' CRITICAL FIX: Also handle the case where we have fewer lines than visible area
                ' In this case, we should always start at line 0
                If pLineCount <= pTotalVisibleLines Then
                    lNewFirstLine = 0
                Else
                    ' Clamp to valid range - ensure we can scroll to show the last line
                    lNewFirstLine = Math.Max(0, Math.Min(lNewFirstLine, lMaxFirstLine))
                End If
                
                ' Only update if the value actually changed
                If lNewFirstLine <> pFirstVisibleLine Then
                    pFirstVisibleLine = lNewFirstLine
                    
                    ' Update scroll position for deferred formatting
                    pScrollY = pFirstVisibleLine * pLineHeight
                    
                    ' Trigger deferred formatting for newly visible lines
                    If pDeferredFormattingEnabled Then
                        OnScrollChanged()
                    End If
                    
                    ' Queue redraw for main drawing area
                    pDrawingArea?.QueueDraw()
                    
                    ' Update line number widget
                    If pLineNumberWidget IsNot Nothing Then
                        pLineNumberWidget.QueueDraw()
                    End If
                    
                    ' Debug output to track scrolling
                    Console.WriteLine($"OnVScrollbarValueChanged: FirstLine={pFirstVisibleLine}, " & _
                                    $"MaxFirstLine={lMaxFirstLine}, ScrollValue={pVScrollbar.Value}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnVScrollbarValueChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles horizontal scrollbar value changes
        ''' </summary>
        Private Sub OnHScrollbarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                ' CRITICAL FIX: The scrollbar value represents the first visible column directly
                Dim lNewFirstColumn As Integer = CInt(Math.Floor(pHScrollbar.Value))
                
                ' Calculate max first column based on content width
                Dim lMaxColumns As Integer = If(pCharWidth > 0, 
                                               CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth)), 
                                               80)
                Dim lMaxFirstColumn As Integer = Math.Max(0, lMaxColumns - pTotalVisibleColumns)
                
                ' Clamp to valid range
                lNewFirstColumn = Math.Max(0, Math.Min(lNewFirstColumn, lMaxFirstColumn))
                
                If lNewFirstColumn <> pFirstVisibleColumn Then
                    pFirstVisibleColumn = lNewFirstColumn
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"OnHScrollbarValueChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Update Visible Metrics =====
        ''' <summary>
        ''' Updates visible line and column counts based on viewport size
        ''' </summary>
        Private Sub UpdateVisibleMetrics()
            Try
                ' Calculate visible lines
                If pLineHeight > 0 AndAlso pViewportHeight > 0 Then
                    pTotalVisibleLines = pViewportHeight \ pLineHeight
                Else
                    pTotalVisibleLines = 1
                End If
                
                ' Calculate visible columns
                If pCharWidth > 0 AndAlso pViewportWidth > 0 Then
                    pTotalVisibleColumns = (pViewportWidth - pLeftPadding - pRightPadding) \ pCharWidth
                Else
                    pTotalVisibleColumns = 80
                End If
                
                ' Ensure minimums
                pTotalVisibleLines = Math.Max(1, pTotalVisibleLines)
                pTotalVisibleColumns = Math.Max(1, pTotalVisibleColumns)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateVisibleMetrics error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Update Scrollbars =====

        
        
        ''' <summary>
        ''' Updates vertical scrollbar adjustment values
        ''' </summary>
        Private Sub UpdateVerticalScrollbar()
            Try
                ' FIXED: Check for valid viewport height to prevent infinity
                If pViewportHeight <= 0 OrElse pVScrollbar Is Nothing Then
                    Return
                End If
                
                Dim lAdjustment As Adjustment = pVScrollbar.Adjustment
                
                ' Calculate the actual number of lines that can be scrolled to
                ' The maximum first visible line is total lines minus visible lines
                Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                
                ' Store the handler reference if we don't have it yet
                If pVScrollbarHandler Is Nothing Then
                    pVScrollbarHandler = New EventHandler(AddressOf OnVScrollbarValueChanged)
                End If
                
                ' Temporarily remove handler to prevent recursive calls
                RemoveHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                
                ' CRITICAL FIX: Set the scrollbar range correctly
                ' The Upper value should be set so that when Value = Upper - PageSize,
                ' we're showing the last page of content
                lAdjustment.Lower = 0
                
                ' Set Upper to line count (total content)
                ' But we need to ensure that Value can never exceed the maximum first visible line
                lAdjustment.Upper = Math.Max(pTotalVisibleLines, pLineCount)
                
                ' PageSize represents how many lines are visible at once
                ' This affects the thumb size and how far we can scroll
                lAdjustment.PageSize = pTotalVisibleLines
                
                ' Step increment for arrow keys/mouse wheel
                lAdjustment.StepIncrement = 1
                
                ' Page increment for page up/down or clicking in scrollbar track
                lAdjustment.PageIncrement = Math.Max(1, CInt(pTotalVisibleLines * PAGE_SCROLL_FACTOR))
                
                ' Clamp current value to valid range
                ' The maximum value should be Upper - PageSize
                Dim lMaxValue As Double = Math.Max(0, lAdjustment.Upper - lAdjustment.PageSize)
                If pFirstVisibleLine > lMaxValue Then
                    pFirstVisibleLine = CInt(Math.Floor(lMaxValue))
                End If
                
                ' Set the current value
                lAdjustment.Value = pFirstVisibleLine
                
                ' Re-add handler - this ensures it's always connected
                AddHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                
                ' Show/hide scrollbar based on need
                pVScrollbar.Visible = (pLineCount > pTotalVisibleLines)
                
                ' Debug output to verify scrollbar settings
                Console.WriteLine($"UpdateVerticalScrollbar: Lines={pLineCount}, VisibleLines={pTotalVisibleLines}, " & _
                                 $"Upper={lAdjustment.Upper}, PageSize={lAdjustment.PageSize}, MaxValue={lMaxValue}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateVerticalScrollbar error: {ex.Message}")
                ' Ensure handler is reconnected even on error
                If pVScrollbar IsNot Nothing AndAlso pVScrollbarHandler IsNot Nothing Then
                    RemoveHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                    AddHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                End If
            End Try
        End Sub

        ''' <summary>
        ''' Updates scrollbar ranges and visibility with deferred formatting support
        ''' </summary>
        Public Sub UpdateScrollbars()
            Try
                ' Don't update scrollbars if viewport is not yet initialized
                If pViewportHeight <= 0 OrElse pViewportWidth <= 0 Then
                    Return
                End If
                
                ' Update maximum line width
                UpdateMaxLineWidth()
                
                ' Update visible metrics first
                UpdateVisibleMetrics()
                
                ' Update vertical scrollbar
                UpdateVerticalScrollbar()
                
                ' Update horizontal scrollbar
                UpdateHorizontalScrollbar()
                
                ' Hook up deferred formatting if enabled
                If pDeferredFormattingEnabled Then
                    ' Trigger formatting check for current view
                    OnScrollChanged()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScrollbars error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handle vertical scroll value changes for deferred formatting
        ''' </summary>
        Private Sub OnVerticalScrollValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                ' Update scroll position
                pScrollY = CInt(pVScrollbar.Adjustment.Value)
                
                ' Trigger deferred formatting for newly visible lines
                OnScrollChanged()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnVerticalScrollValueChanged error: {ex.Message}")
            End Try
        End Sub
            
        ''' <summary>
        ''' Updates horizontal scrollbar adjustment values
        ''' </summary>
        Private Sub UpdateHorizontalScrollbar()
            Try
                ' FIXED: Check for valid viewport width to prevent infinity
                If pViewportWidth <= 0 OrElse pHScrollbar Is Nothing Then
                    Return
                End If
                
                Dim lAdjustment As Adjustment = pHScrollbar.Adjustment
                
                ' Calculate values
                Dim lMaxColumns As Integer = If(pCharWidth > 0, 
                                               CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth)), 
                                               80)
                Dim lMaxFirstColumn As Integer = Math.Max(0, lMaxColumns - pTotalVisibleColumns)
                Dim lPageSize As Double = pTotalVisibleColumns
                Dim lThumbSize As Double = If(lMaxColumns > 0, CDbl(pTotalVisibleColumns) / lMaxColumns, 1.0)
                
                ' Ensure minimum thumb size
                If lThumbSize * pViewportWidth < MINIMUM_THUMB_SIZE AndAlso lMaxColumns > pTotalVisibleColumns Then
                    lPageSize = CDbl(MINIMUM_THUMB_SIZE * lMaxColumns) / pViewportWidth
                End If
                
                ' FIXED: Ensure page size is not infinity or NaN
                If Double.IsInfinity(lPageSize) OrElse Double.IsNaN(lPageSize) Then
                    lPageSize = 1.0
                End If
                
                ' Store the handler reference if we don't have it yet
                If pHScrollbarHandler Is Nothing Then
                    pHScrollbarHandler = New EventHandler(AddressOf OnHScrollbarValueChanged)
                End If
                
                ' Temporarily remove handler to prevent recursive calls
                RemoveHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                
                ' Update adjustment
                lAdjustment.Lower = 0
                lAdjustment.Upper = lMaxColumns
                lAdjustment.PageSize = lPageSize
                lAdjustment.StepIncrement = 1
                lAdjustment.PageIncrement = Math.Max(1, CInt(pTotalVisibleColumns * PAGE_SCROLL_FACTOR))
                
                ' Clamp current value
                If pFirstVisibleColumn > lMaxFirstColumn Then
                    pFirstVisibleColumn = lMaxFirstColumn
                End If
                lAdjustment.Value = pFirstVisibleColumn
                
                ' Re-add handler - this ensures it's always connected
                AddHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                
                ' Show/hide scrollbar based on need
                pHScrollbar.Visible = (lMaxColumns > pTotalVisibleColumns)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateHorizontalScrollbar error: {ex.Message}")
                ' Ensure handler is reconnected even on error
                If pHScrollbar IsNot Nothing AndAlso pHScrollbarHandler IsNot Nothing Then
                    RemoveHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                    AddHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                End If
            End Try
        End Sub
        
        ' ===== Scroll Event =====

        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.OnScrollEvent
        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.OnScrollEvent
        ''' <summary>
        ''' Handles mouse scroll wheel events with debug output and Ctrl+zoom support
        ''' </summary>
        Public Shadows Function OnScrollEvent(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                Console.WriteLine($"OnScrollEvent called: Direction={vArgs.Event.Direction}, State={vArgs.Event.State}")
                
                ' Check for Ctrl+Scroll for zoom functionality
                If (vArgs.Event.State And ModifierType.ControlMask) = ModifierType.ControlMask Then
                    Console.WriteLine("Ctrl modifier detected!")
                    
                    ' Ctrl+Scroll: Zoom in/out via SettingsManager
                    Select Case vArgs.Event.Direction
                        Case ScrollDirection.Up
                            Console.WriteLine("Ctrl+ScrollUp: Calling ZoomIn()")
                            ZoomIn()
                            
                            ' CRITICAL: Force immediate redraw after zoom
                            Application.Invoke(Sub()
                                ' Force metrics update immediately
                                pFontMetrics = Nothing  ' Clear cached metrics
                                UpdateFontMetrics()
                                
                                ' Queue redraws
                                pDrawingArea?.QueueDraw()
                                pLineNumberWidget?.QueueDraw()
                                
                                ' Process pending events to force immediate redraw
                                While Application.EventsPending()
                                    Application.RunIteration(False)
                                End While
                            End Sub)
                            
                        Case ScrollDirection.Down
                            Console.WriteLine("Ctrl+ScrollDown: Calling ZoomOut()")
                            ZoomOut()
                            
                            ' CRITICAL: Force immediate redraw after zoom
                            Application.Invoke(Sub()
                                ' Force metrics update immediately
                                pFontMetrics = Nothing  ' Clear cached metrics
                                UpdateFontMetrics()
                                
                                ' Queue redraws
                                pDrawingArea?.QueueDraw()
                                pLineNumberWidget?.QueueDraw()
                                
                                ' Process pending events to force immediate redraw
                                While Application.EventsPending()
                                    Application.RunIteration(False)
                                End While
                            End Sub)
                    End Select
                    
                    ' Return True to indicate we handled the event
                    vArgs.RetVal = True
                    Return True
                End If
                
                Console.WriteLine("No Ctrl modifier - handling normal scroll")
                Dim lLines As Integer = SCROLL_WHEEL_LINES
                
                ' Check for horizontal scrolling (Shift+Scroll)
                If (vArgs.Event.State And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    Console.WriteLine("Shift modifier detected - horizontal scroll")
                    ' Horizontal scroll
                    Select Case vArgs.Event.Direction
                        Case ScrollDirection.Up, ScrollDirection.Left
                            ScrollLeft(lLines * pCharWidth)
                        Case ScrollDirection.Down, ScrollDirection.Right
                            ScrollRight(lLines * pCharWidth)
                    End Select
                Else
                    Console.WriteLine("Regular vertical scroll")
                    ' Vertical scroll
                    Select Case vArgs.Event.Direction
                        Case ScrollDirection.Up
                            ScrollUp(lLines)
                        Case ScrollDirection.Down
                            ScrollDown(lLines)
                    End Select
                End If
                
                ' CRITICAL FIX: Update selection if we're currently dragging
                ' This keeps the selection end under the mouse pointer during scroll
                If pIsDragging AndAlso Not pPotentialDrag Then
                    ' Get current mouse position relative to the drawing area
                    Dim lDisplay As Gdk.Display = pDrawingArea.Display
                    If lDisplay IsNot Nothing Then
                        ' Get the default seat and then the pointer
                        Dim lSeat As Gdk.Seat = lDisplay.DefaultSeat
                        If lSeat IsNot Nothing Then
                            Dim lPointer As Gdk.Device = lSeat.Pointer
                            If lPointer IsNot Nothing Then
                                ' Get the window coordinates
                                Dim lWindow As Gdk.Window = pDrawingArea.Window
                                If lWindow IsNot Nothing Then
                                    Dim lX, lY As Integer
                                    Dim lMask As ModifierType
                                    lWindow.GetDevicePosition(lPointer, lX, lY, lMask)
                                    
                                    ' Update selection to current mouse position
                                    ' Get the position at current mouse coordinates
                                    Dim lPos As EditorPosition = GetPositionFromCoordinates(CDbl(lX), CDbl(lY))
                                    
                                    ' Update selection end to this position
                                    If pTextDragAnchorLine >= 0 AndAlso pTextDragAnchorColumn >= 0 Then
                                        pSelectionStartLine = pTextDragAnchorLine
                                        pSelectionStartColumn = pTextDragAnchorColumn
                                        pSelectionEndLine = lPos.Line
                                        pSelectionEndColumn = lPos.Column
                                        
                                        ' Move cursor to current drag position
                                        SetCursorPosition(lPos.Line, lPos.Column)
                                        
                                        ' Queue redraw for selection
                                        pDrawingArea.QueueDraw()
                                    End If
                                End If
                            End If
                        End If
                    End If
                End If
                
                ' Return True to indicate we handled the event
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnScrollEvent error: {ex.Message}")
                vArgs.RetVal = False
                Return False
            End Try
        End Function

        ' ===== Scrolling Methods =====

        ''' <summary>
        ''' Scrolls the editor up by the specified number of lines
        ''' </summary>
        ''' <param name="vLines">Number of lines to scroll up</param>
        Private Sub ScrollUp(vLines As Integer)
            Try
                If pFirstVisibleLine > 0 Then
                    ' Calculate new first visible line
                    Dim lNewFirstLine As Integer = Math.Max(0, pFirstVisibleLine - vLines)
                    
                    If lNewFirstLine <> pFirstVisibleLine Then
                        pFirstVisibleLine = lNewFirstLine
                        
                        ' Update the scrollbar value to match
                        If pVScrollbar IsNot Nothing Then
                            pVScrollbar.Value = pFirstVisibleLine
                        End If
                        
                        ' Update scrollbars and redraw
                        UpdateScrollbars()
                        pDrawingArea?.QueueDraw()
                        pLineNumberWidget?.QueueDraw()
                        
                        Console.WriteLine($"ScrollUp: Scrolled to line {pFirstVisibleLine}")
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollUp error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Scrolls the editor down by the specified number of lines
        ''' </summary>
        ''' <param name="vLines">Number of lines to scroll down</param>
        Private Sub ScrollDown(vLines As Integer)
            Try
                ' Calculate the maximum valid first visible line
                ' This ensures the last line can be visible at the bottom of the viewport
                Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                
                If pFirstVisibleLine < lMaxFirstLine Then
                    ' Calculate new first visible line
                    Dim lNewFirstLine As Integer = Math.Min(lMaxFirstLine, pFirstVisibleLine + vLines)
                    
                    If lNewFirstLine <> pFirstVisibleLine Then
                        pFirstVisibleLine = lNewFirstLine
                        
                        ' Update the scrollbar value to match
                        If pVScrollbar IsNot Nothing Then
                            pVScrollbar.Value = pFirstVisibleLine
                        End If
                        
                        ' Update scrollbars and redraw
                        UpdateScrollbars()
                        pDrawingArea?.QueueDraw()
                        pLineNumberWidget?.QueueDraw()
                        
                        Console.WriteLine($"ScrollDown: Scrolled to line {pFirstVisibleLine}, Max={lMaxFirstLine}")
                    End If
                ElseIf pLineCount > pTotalVisibleLines Then
                    ' CRITICAL FIX: If we think we're at the bottom but we're not showing all lines,
                    ' try to scroll to the absolute maximum to show the last lines
                    Dim lAbsoluteMax As Integer = pLineCount - pTotalVisibleLines
                    If pFirstVisibleLine < lAbsoluteMax Then
                        pFirstVisibleLine = lAbsoluteMax
                        
                        If pVScrollbar IsNot Nothing Then
                            pVScrollbar.Value = pFirstVisibleLine
                        End If
                        
                        UpdateScrollbars()
                        pDrawingArea?.QueueDraw()
                        pLineNumberWidget?.QueueDraw()
                        
                        Console.WriteLine($"ScrollDown: Force scrolled to absolute max {pFirstVisibleLine}")
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollDown error: {ex.Message}")
            End Try
        End Sub

     
        ''' <summary>
        ''' Scrolls the editor left by the specified number of columns
        ''' </summary>
        ''' <param name="vColumns">Number of columns to scroll left</param>
        Private Sub ScrollLeft(vColumns As Integer)
            Try
                If pFirstVisibleColumn > 0 Then
                    pFirstVisibleColumn = Math.Max(0, pFirstVisibleColumn - vColumns)
                    
                    ' Update the scrollbar value to match
                    If pHScrollbar IsNot Nothing Then
                        pHScrollbar.Value = pFirstVisibleColumn
                    End If
                    
                    UpdateScrollbars()
                    pDrawingArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollLeft error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Scrolls the editor right by the specified number of columns
        ''' </summary>
        ''' <param name="vColumns">Number of columns to scroll right</param>
        Private Sub ScrollRight(vColumns As Integer)
            Try
                Dim lMaxColumns As Integer = If(pCharWidth > 0, 
                                               CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth)), 
                                               80)
                Dim lMaxFirstColumn As Integer = Math.Max(0, lMaxColumns - pTotalVisibleColumns)
                If pFirstVisibleColumn < lMaxFirstColumn Then
                    pFirstVisibleColumn = Math.Min(lMaxFirstColumn, pFirstVisibleColumn + vColumns)
                    
                    ' Update the scrollbar value to match
                    If pHScrollbar IsNot Nothing Then
                        pHScrollbar.Value = pFirstVisibleColumn
                    End If
                    
                    UpdateScrollbars()
                    pDrawingArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollRight error: {ex.Message}")
            End Try
        End Sub

        
        ' ===== Helper methods =====
        ''' <summary>
        ''' Gets the first visible line index
        ''' </summary>
        Public Function GetFirstVisibleLine() As Integer
            Return pFirstVisibleLine
        End Function
        
        ''' <summary>
        ''' Gets the last visible line index
        ''' </summary>
        Public Function GetLastVisibleLine() As Integer
            Return Math.Min(pFirstVisibleLine + pTotalVisibleLines - 1, pLineCount - 1)
        End Function
        
        ''' <summary>
        ''' Gets the first visible column index
        ''' </summary>
        Public Function GetFirstVisibleColumn() As Integer
            Return pFirstVisibleColumn
        End Function
        
        ''' <summary>
        ''' Gets the last visible column index
        ''' </summary>
        Public Function GetLastVisibleColumn() As Integer
            Return pFirstVisibleColumn + pTotalVisibleColumns - 1
        End Function
        
        ' ===== Auto-scroll during selection =====
        ''' <summary>
        ''' Automatically scrolls the editor if the mouse is near the edge during selection
        ''' </summary>
        Public Sub AutoScrollIfNearEdge(vMouseX As Double, vMouseY As Double)
            Try
                Const SCROLL_MARGIN As Integer = 20
                Const SCROLL_SPEED As Integer = 1
                
                Dim lNeedsRedraw As Boolean = False
                
                ' Check vertical scrolling
                If vMouseY < SCROLL_MARGIN Then
                    ' Near top edge
                    If pFirstVisibleLine > 0 Then
                        ScrollUp(SCROLL_SPEED)
                        lNeedsRedraw = True
                    End If
                ElseIf vMouseY > pViewportHeight - SCROLL_MARGIN Then
                    ' Near bottom edge
                    Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                    If pFirstVisibleLine < lMaxFirstLine Then
                        ScrollDown(SCROLL_SPEED)
                        lNeedsRedraw = True
                    End If
                End If
                
                ' Check horizontal scrolling
                If vMouseX < pLineNumberWidth + SCROLL_MARGIN Then
                    ' Near left edge
                    If pFirstVisibleColumn > 0 Then
                        ScrollLeft(SCROLL_SPEED * 3)
                        lNeedsRedraw = True
                    End If
                ElseIf vMouseX > pViewportWidth - SCROLL_MARGIN Then
                    ' Near right edge
                    Dim lMaxColumns As Integer = If(pCharWidth > 0, 
                                                   CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth)), 
                                                   80)
                    Dim lMaxFirstColumn As Integer = Math.Max(0, lMaxColumns - pTotalVisibleColumns)
                    If pFirstVisibleColumn < lMaxFirstColumn Then
                        ScrollRight(SCROLL_SPEED * 3)
                        lNeedsRedraw = True
                    End If
                End If
                
                If lNeedsRedraw Then
                    ' Queue redraw for main drawing area
                    If pDrawingArea IsNot Nothing Then
                        pDrawingArea.QueueDraw()
                    End If
                    
                    ' Queue redraw for line number widget - check both new and old widgets
                    If pLineNumberWidget IsNot Nothing Then
                        pLineNumberWidget.QueueDraw()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AutoScrollIfNearEdge error: {ex.Message}")
            End Try
        End Sub

        ' ===== Page Up/Down =====
        Public Sub PageUp() Implements IEditor.PageUp
            Try
                ScrollUp(CInt(pTotalVisibleLines * PAGE_SCROLL_FACTOR))
                
                ' Move cursor up by visible lines
                Dim lNewLine As Integer = Math.Max(0, pCursorLine - CInt(pTotalVisibleLines * PAGE_SCROLL_FACTOR))
                SetCursorPosition(lNewLine, pCursorColumn)
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"PageUp error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub PageDown() Implements IEditor.PageDown
            Try
                ScrollDown(CInt(pTotalVisibleLines * PAGE_SCROLL_FACTOR))
                
                ' Move cursor down by visible lines
                Dim lNewLine As Integer = Math.Min(pLineCount - 1, pCursorLine + CInt(pTotalVisibleLines * PAGE_SCROLL_FACTOR))
                SetCursorPosition(lNewLine, pCursorColumn)
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"PageDown error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Ensure Cursor Visible =====
        Public Sub EnsureCursorVisible() Implements IEditor.EnsureCursorVisible
            Try
                ' Check if scrollbars are initialized
                If pVScrollbar Is Nothing OrElse pHScrollbar Is Nothing Then
                    Return
                End If
                
                ' Vertical scrolling
                If pCursorLine < pFirstVisibleLine Then
                    ' Cursor above visible area
                    pFirstVisibleLine = pCursorLine
                    pVScrollbar.Value = pFirstVisibleLine
                ElseIf pCursorLine >= pFirstVisibleLine + pTotalVisibleLines Then
                    ' Cursor below visible area
                    pFirstVisibleLine = Math.Max(0, pCursorLine - pTotalVisibleLines + 1)
                    pVScrollbar.Value = pFirstVisibleLine
                End If
                
                ' Horizontal scrolling
                If pCursorColumn < pFirstVisibleColumn Then
                    ' Cursor to the left of visible area
                    pFirstVisibleColumn = Math.Max(0, pCursorColumn - 5) ' Leave some context
                    pHScrollbar.Value = pFirstVisibleColumn
                ElseIf pCursorColumn >= pFirstVisibleColumn + pTotalVisibleColumns Then
                    ' Cursor to the right of visible area
                    pFirstVisibleColumn = Math.Max(0, pCursorColumn - pTotalVisibleColumns + 5)
                    pHScrollbar.Value = pFirstVisibleColumn
                End If
                
                ' Queue redraw for main drawing area
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.QueueDraw()
                End If
                
                ' Queue redraw for line number widget - check both new and old widgets
                If pLineNumberWidget IsNot Nothing Then
                    pLineNumberWidget.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"EnsureCursorVisible error: {ex.Message}")
            End Try
        End Sub

        ' ===== Scroll to specific line =====

        ''' <summary>
        ''' Scrolls to a specific line in the editor
        ''' </summary>
        ''' <param name="vLine">The line number to scroll to</param>
        Public Sub ScrollToLine(vLine As Integer)
            Try
                ' Check if scrollbar is initialized
                If pVScrollbar Is Nothing Then
                    Return
                End If
                
                ' Validate line
                vLine = Math.Max(0, Math.Min(vLine, pLineCount - 1))
                
                ' Center the line if possible
                Dim lTargetFirstLine As Integer = Math.Max(0, vLine - pTotalVisibleLines \ 2)
                lTargetFirstLine = Math.Min(lTargetFirstLine, Math.Max(0, pLineCount - pTotalVisibleLines))
                
                If lTargetFirstLine <> pFirstVisibleLine Then
                    pFirstVisibleLine = lTargetFirstLine
                    pVScrollbar.Value = pFirstVisibleLine
                    
                    ' Queue redraw for main drawing area
                    If pDrawingArea IsNot Nothing Then
                        pDrawingArea.QueueDraw()
                    End If
                    
                    ' Queue redraw for line number widget - check both new and old widgets
                    If pLineNumberWidget IsNot Nothing Then
                        pLineNumberWidget.QueueDraw()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ScrollToLine error: {ex.Message}")
            End Try
        End Sub

        ' ===== Update Maximum Line Width =====
        Private Sub UpdateMaxLineWidth()
            Try
                pMaxLineWidth = 0
                for i As Integer = 0 To pLineCount - 1
                    If TextLines(i) IsNot Nothing Then
                        pMaxLineWidth = Math.Max(pMaxLineWidth, TextLines(i).Length)
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine($"UpdateMaxLineWidth error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Scrolls the editor to the top
        ''' </summary>
        Private Sub ScrollToTop()
            Try
                ' Check if scrollbar is initialized
                If pVScrollbar Is Nothing Then Return
                
                ' Set to top
                pFirstVisibleLine = 0
                pVScrollbar.Value = 0
                
                ' Force immediate redraw
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.QueueDraw()
                End If
                
                If pLineNumberWidget IsNot Nothing Then
                    pLineNumberWidget.QueueDraw()
                End If
                
                Console.WriteLine("ScrollToTop: Scrolled to line 0")
                
            Catch ex As Exception
                Console.WriteLine($"ScrollToTop error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Scrolls the editor to the bottom, accounting for visible viewport
        ''' </summary>
        Private Sub ScrollToBottom()
            Try
                ' Check if scrollbar is initialized
                If pVScrollbar Is Nothing Then Return
                
                ' Update visible metrics to ensure we have current viewport size
                UpdateVisibleMetrics()
                
                ' Calculate the target first visible line to show the bottom
                ' We want the last line to be at the bottom of the viewport
                Dim lTargetFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                
                ' If we're already at the bottom, force a complete scroll
                If pFirstVisibleLine = lTargetFirstLine AndAlso lTargetFirstLine < pLineCount - 1 Then
                    ' Try to scroll one more line if possible to ensure we're truly at bottom
                    lTargetFirstLine = Math.Min(pLineCount - 1, lTargetFirstLine + 1)
                End If
                
                ' Set the scroll position
                pFirstVisibleLine = lTargetFirstLine
                pVScrollbar.Value = pFirstVisibleLine
                
                ' Force immediate redraw
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.QueueDraw()
                End If
                
                If pLineNumberWidget IsNot Nothing Then
                    pLineNumberWidget.QueueDraw()
                End If
                
                Console.WriteLine($"ScrollToBottom: Lines={pLineCount}, VisibleLines={pTotalVisibleLines}, FirstLine={pFirstVisibleLine}")
                
            Catch ex As Exception
                Console.WriteLine($"ScrollToBottom error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace

