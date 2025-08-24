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
                Dim lNewFirstLine As Integer = CInt(pVScrollbar.Value)
                If lNewFirstLine <> pFirstVisibleLine Then
                    pFirstVisibleLine = lNewFirstLine
                    pDrawingArea.QueueDraw()
                    
                    ' Update line number widget
                    If pLineNumberWidget IsNot Nothing Then
                        pLineNumberWidget.QueueDraw()
                    ElseIf pLineNumberArea IsNot Nothing Then
                        ' Fallback for old widget
                        pLineNumberArea.QueueDraw()
                    End If
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
                Dim lNewFirstColumn As Integer = CInt(pHScrollbar.Value)
                If lNewFirstColumn <> pFirstVisibleColumn Then
                    pFirstVisibleColumn = lNewFirstColumn
                    pDrawingArea.QueueDraw()
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
        ''' Updates scrollbar ranges and visibility
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
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScrollbars error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates vertical scrollbar adjustment values
        ''' </summary>
        Private Sub UpdateVerticalScrollbar()
            Try
                ' FIXED: Check for valid viewport height to prevent infinity
                If pViewportHeight <= 0 Then
                    Return
                End If
                
                Dim lAdjustment As Adjustment = pVScrollbar.Adjustment
                
                ' Calculate values
                Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                Dim lPageSize As Double = pTotalVisibleLines
                Dim lThumbSize As Double = If(pLineCount > 0, CDbl(pTotalVisibleLines) / pLineCount, 1.0)
                
                ' Ensure minimum thumb size
                If lThumbSize * pViewportHeight < MINIMUM_THUMB_SIZE AndAlso pLineCount > pTotalVisibleLines Then
                    lPageSize = CDbl(MINIMUM_THUMB_SIZE * pLineCount) / pViewportHeight
                End If
                
                ' FIXED: Ensure page size is not infinity or NaN
                If Double.IsInfinity(lPageSize) OrElse Double.IsNaN(lPageSize) Then
                    lPageSize = 1.0
                End If
                
                ' Temporarily remove handler to prevent recursive calls
                RemoveHandler pVScrollbar.ValueChanged, AddressOf OnVScrollbarValueChanged
                
                ' Update adjustment
                lAdjustment.Lower = 0
                lAdjustment.Upper = pLineCount
                lAdjustment.PageSize = lPageSize
                lAdjustment.StepIncrement = 1
                lAdjustment.PageIncrement = Math.Max(1, CInt(pTotalVisibleLines * PAGE_SCROLL_FACTOR))
                
                ' Clamp current value
                If pFirstVisibleLine > lMaxFirstLine Then
                    pFirstVisibleLine = lMaxFirstLine
                End If
                lAdjustment.Value = pFirstVisibleLine
                
                ' Re-add handler
                AddHandler pVScrollbar.ValueChanged, AddressOf OnVScrollbarValueChanged
                
                ' Show/hide scrollbar based on need
                pVScrollbar.Visible = (pLineCount > pTotalVisibleLines)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateVerticalScrollbar error: {ex.Message}")
            End Try
        End Sub
            
        ''' <summary>
        ''' Updates horizontal scrollbar adjustment values
        ''' </summary>
        Private Sub UpdateHorizontalScrollbar()
            Try
                ' FIXED: Check for valid viewport width to prevent infinity
                If pViewportWidth <= 0 Then
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
                
                ' Temporarily remove handler to prevent recursive calls
                RemoveHandler pHScrollbar.ValueChanged, AddressOf OnHScrollbarValueChanged
                
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
                
                ' Re-add handler
                AddHandler pHScrollbar.ValueChanged, AddressOf OnHScrollbarValueChanged
                
                ' Show/hide scrollbar based on need
                pHScrollbar.Visible = (lMaxColumns > pTotalVisibleColumns)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateHorizontalScrollbar error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Scroll Event =====
        ''' <summary>
        ''' Handles mouse scroll wheel events
        ''' </summary>
        Public Shadows Function OnScrollEvent(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                Dim lLines As Integer = SCROLL_WHEEL_LINES
                
                ' Check for horizontal scrolling (Shift+Scroll)
                If (vArgs.Event.State And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    ' Horizontal scroll
                    Select Case vArgs.Event.Direction
                        Case ScrollDirection.Up, ScrollDirection.Left
                            ScrollLeft(lLines * pCharWidth)
                        Case ScrollDirection.Down, ScrollDirection.Right
                            ScrollRight(lLines * pCharWidth)
                    End Select
                Else
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
                                ' Get pointer position
                                Dim lScreen As Gdk.Screen = Nothing
                                Dim lX, lY As Integer
                                Dim lMask As Gdk.ModifierType = Nothing
                                
                                pDrawingArea.Window.GetDevicePosition(lPointer, lX, lY, lMask)
                                
                                ' The coordinates are already relative to the widget
                                ' Get the position at current mouse coordinates after scroll
                                Dim lPos As EditorPosition = GetPositionFromCoordinates(CDbl(lX), CDbl(lY))
                                
                                ' Update selection to this position
                                If pIsStartingNewSelection Then
                                    ' Creating new selection from drag
                                    pSelectionEndLine = lPos.Line
                                    pSelectionEndColumn = lPos.Column
                                    pHasSelection = True
                                    pIsStartingNewSelection = False
                                ElseIf pHasSelection Then
                                    ' Extending existing selection
                                    pSelectionEndLine = lPos.Line
                                    pSelectionEndColumn = lPos.Column
                                End If
                                
                                ' Queue redraw for selection
                                pDrawingArea.QueueDraw()
                            End If
                        End If
                    End If
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnScrollEvent error: {ex.Message}")
                Return False
            End Try
        End Function

        ' ===== Scrolling Methods =====
        ''' <summary>
        ''' Scrolls the editor up by the specified number of lines
        ''' </summary>
        Private Sub ScrollUp(vLines As Integer)
            Try
                If pFirstVisibleLine > 0 Then
                    pFirstVisibleLine = Math.Max(0, pFirstVisibleLine - vLines)
                    UpdateScrollbars()
                    pDrawingArea.QueueDraw()
                    pLineNumberArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollUp error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Scrolls the editor down by the specified number of lines
        ''' </summary>
        Private Sub ScrollDown(vLines As Integer)
            Try
                Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                If pFirstVisibleLine < lMaxFirstLine Then
                    pFirstVisibleLine = Math.Min(lMaxFirstLine, pFirstVisibleLine + vLines)
                    UpdateScrollbars()
                    pDrawingArea.QueueDraw()
                    pLineNumberArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollDown error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Scrolls the editor left by the specified number of columns
        ''' </summary>
        Private Sub ScrollLeft(vColumns As Integer)
            Try
                If pFirstVisibleColumn > 0 Then
                    pFirstVisibleColumn = Math.Max(0, pFirstVisibleColumn - vColumns)
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
        Private Sub ScrollRight(vColumns As Integer)
            Try
                Dim lMaxColumns As Integer = If(pCharWidth > 0, 
                                               CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth)), 
                                               80)
                Dim lMaxFirstColumn As Integer = Math.Max(0, lMaxColumns - pTotalVisibleColumns)
                If pFirstVisibleColumn < lMaxFirstColumn Then
                    pFirstVisibleColumn = Math.Min(lMaxFirstColumn, pFirstVisibleColumn + vColumns)
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
                    pDrawingArea.QueueDraw()
                    pLineNumberArea.QueueDraw()
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
                    pFirstVisibleColumn = Math.Max(0, pCursorColumn - 5) ' Leave some Context
                    pHScrollbar.Value = pFirstVisibleColumn
                ElseIf pCursorColumn >= pFirstVisibleColumn + pTotalVisibleColumns Then
                    ' Cursor to the right of visible area
                    pFirstVisibleColumn = Math.Max(0, pCursorColumn - pTotalVisibleColumns + 5)
                    pHScrollbar.Value = pFirstVisibleColumn
                End If
                
                ' Queue redraw
                pDrawingArea.QueueDraw()
                pLineNumberArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"EnsureCursorVisible error: {ex.Message}")
            End Try
        End Sub

        ' ===== Scroll to specific line =====
        Public Sub ScrollToLine(vLine As Integer)
            Try
                ' Validate line
                vLine = Math.Max(0, Math.Min(vLine, pLineCount - 1))
                
                ' Center the line if possible

                Dim lTargetFirstLine As Integer = Math.Max(0, vLine - pTotalVisibleLines \ 2)
                lTargetFirstLine = Math.Min(lTargetFirstLine, Math.Max(0, pLineCount - pTotalVisibleLines))
                
                If lTargetFirstLine <> pFirstVisibleLine Then
                    pFirstVisibleLine = lTargetFirstLine
                    pVScrollbar.Value = pFirstVisibleLine
                    pDrawingArea.QueueDraw()
                    pLineNumberArea.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ScrollToLine error: {ex.Message}")
            End Try
        End Sub

        ' ===== Update Maximum Line Width =====
        Private Sub UpdateMaxLineWidth()
            Try
                pMaxLineWidth = 0
                For i As Integer = 0 To pLineCount - 1
                    If pTextLines(i) IsNot Nothing Then
                        pMaxLineWidth = Math.Max(pMaxLineWidth, pTextLines(i).Length)
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine($"UpdateMaxLineWidth error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace

