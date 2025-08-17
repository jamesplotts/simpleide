' Editors/CustomDrawingEditor.ScrollManual.vb - Manual scrollbar implementation
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Interfaces

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Scrollbar Constants =====
        Private Const MINIMUM_THUMB_SIZE As Integer = 20
        Private Const SCROLL_WHEEL_LINES As Integer = 3
        Private Const PAGE_SCROLL_FACTOR As Double = 0.9
        
        ' ===== Scrollbar Value Changed Handlers =====
        Private Sub OnVScrollbarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                Dim lNewFirstLine As Integer = CInt(pVScrollbar.Value)
                If lNewFirstLine <> pFirstVisibleLine Then
                    pFirstVisibleLine = lNewFirstLine
                    pDrawingArea.QueueDraw()
                    pLineNumberArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"OnVScrollbarValueChanged error: {ex.Message}")
            End Try
        End Sub
        
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
        Private Sub UpdateVisibleMetrics()
            Try
                ' Calculate visible lines
                If pLineHeight > 0 Then
                    pTotalVisibleLines = pViewportHeight \ pLineHeight
                Else
                    pTotalVisibleLines = 1
                End If
                
                ' Calculate visible columns
                If pCharWidth > 0 Then
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
        Public Sub UpdateScrollbars()
            Try
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
        
        Private Sub UpdateVerticalScrollbar()
            Try
                Dim lAdjustment As Adjustment = pVScrollbar.Adjustment
                
                ' Calculate values
                Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                Dim lPageSize As Double = pTotalVisibleLines
                Dim lThumbSize As Double = If(pLineCount > 0, CDbl(pTotalVisibleLines) / pLineCount, 1.0)
                
                ' Ensure minimum thumb size
                If lThumbSize * pViewportHeight < MINIMUM_THUMB_SIZE AndAlso pLineCount > pTotalVisibleLines Then
                    lPageSize = CDbl(MINIMUM_THUMB_SIZE * pLineCount) / pViewportHeight
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
            
        Private Sub UpdateHorizontalScrollbar()
            Try
                Dim lAdjustment As Adjustment = pHScrollbar.Adjustment
                
                ' Calculate values
                Dim lMaxColumns As Integer = CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth))
                Dim lMaxFirstColumn As Integer = Math.Max(0, lMaxColumns - pTotalVisibleColumns)
                Dim lPageSize As Double = pTotalVisibleColumns
                Dim lThumbSize As Double = If(lMaxColumns > 0, CDbl(pTotalVisibleColumns) / lMaxColumns, 1.0)
                
                ' Ensure minimum thumb size
                If lThumbSize * pViewportWidth < MINIMUM_THUMB_SIZE AndAlso lMaxColumns > pTotalVisibleColumns Then
                    lPageSize = CDbl(MINIMUM_THUMB_SIZE * lMaxColumns) / pViewportWidth
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
        
'        Private Sub UpdateMaxLineWidth()
'            Try
'                pMaxLineWidth = 0
'                For Each lLine In pTextLines
'                    Dim lWidth As Integer = lLine.Length * pCharWidth + pLeftPadding + pRightPadding
'                    If lWidth > pMaxLineWidth Then
'                        pMaxLineWidth = lWidth
'                    End If
'                Next
'            Catch ex As Exception
'                Console.WriteLine($"UpdateMaxLineWidth error: {ex.Message}")
'            End Try
'        End Sub
        
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


        ' ===== Mouse Wheel Scrolling =====
        Private Shadows Function OnScrollEvent(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                Select Case vArgs.Event.Direction
                    Case ScrollDirection.Up
                        ScrollUp(SCROLL_WHEEL_LINES)
                        vArgs.RetVal = True
                        
                    Case ScrollDirection.Down
                        ScrollDown(SCROLL_WHEEL_LINES)
                        vArgs.RetVal = True
                        
                    Case ScrollDirection.Left
                        ScrollLeft(SCROLL_WHEEL_LINES)
                        vArgs.RetVal = True
                        
                    Case ScrollDirection.Right
                        ScrollRight(SCROLL_WHEEL_LINES)
                        vArgs.RetVal = True
                        
                    Case ScrollDirection.Smooth
                        ' Handle smooth scrolling (touchpad)
                        Dim lDeltaX As Double = 0
                        Dim lDeltaY As Double = 0
                        
                        ' GTK# 3 doesn't have GetScrollDeltas method, use DeltaX and DeltaY properties
                        lDeltaX = vArgs.Event.DeltaX
                        lDeltaY = vArgs.Event.DeltaY
                        
                        If Math.Abs(lDeltaY) > Math.Abs(lDeltaX) Then
                            ' Vertical scrolling
                            If lDeltaY < 0 Then
                                ScrollUp(Math.Max(1, CInt(Math.Abs(lDeltaY))))
                            Else
                                ScrollDown(Math.Max(1, CInt(Math.Abs(lDeltaY))))
                            End If
                        Else
                            ' Horizontal scrolling
                            If lDeltaX < 0 Then
                                ScrollLeft(Math.Max(1, CInt(Math.Abs(lDeltaX))))
                            Else
                                ScrollRight(Math.Max(1, CInt(Math.Abs(lDeltaX))))
                            End If
                        End If
                        vArgs.RetVal = True
                End Select
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnScrollEvent error: {ex.Message}")
                Return False
            End Try
        End Function

'        ' ===== Scroll Event =====
'        Private Function OnScrollEvent(vSender As Object, vArgs As ScrollEventArgs) As Boolean
'            Try
'                Dim lLines As Integer = 3 ' Lines to scroll
'                
'                ' Check for horizontal scrolling (Shift+Scroll)
'                If (vArgs.Event.State And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
'                    ' Horizontal scroll
'                    Select Case vArgs.Event.Direction
'                        Case ScrollDirection.Up, ScrollDirection.Left
'                            pHScrollbar.Value = Math.Max(0, pHScrollbar.Value - (lLines * pCharWidth))
'                        Case ScrollDirection.Down, ScrollDirection.Right
'                            pHScrollbar.Value = Math.Min(pHScrollbar.Adjustment.Upper - pHScrollbar.Adjustment.PageSize, 
'                                                        pHScrollbar.Value + (lLines * pCharWidth))
'                    End Select
'                Else
'                    ' Vertical scroll
'                    Select Case vArgs.Event.Direction
'                        Case ScrollDirection.Up
'                            pVScrollbar.Value = Math.Max(0, pVScrollbar.Value - lLines)
'                        Case ScrollDirection.Down
'                            pVScrollbar.Value = Math.Min(pVScrollbar.Adjustment.Upper - pVScrollbar.Adjustment.PageSize, 
'                                                        pVScrollbar.Value + lLines)
'                    End Select
'                End If
'                
'                vArgs.RetVal = True
'                Return True
'                
'            Catch ex As Exception
'                Console.WriteLine($"OnScrollEvent error: {ex.Message}")
'                Return False
'            End Try
'        End Function
        
        ' ===== Scrolling Methods =====
        Private Sub ScrollUp(vLines As Integer)
            Try
                If pFirstVisibleLine > 0 Then
                    pFirstVisibleLine = Math.Max(0, pFirstVisibleLine - vLines)
                    pVScrollbar.Value = pFirstVisibleLine
                    pDrawingArea.QueueDraw()
                    pLineNumberArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollUp error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ScrollDown(vLines As Integer)
            Try
                Dim lMaxFirstLine As Integer = Math.Max(0, pLineCount - pTotalVisibleLines)
                If pFirstVisibleLine < lMaxFirstLine Then
                    pFirstVisibleLine = Math.Min(lMaxFirstLine, pFirstVisibleLine + vLines)
                    pVScrollbar.Value = pFirstVisibleLine
                    pDrawingArea.QueueDraw()
                    pLineNumberArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollDown error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ScrollLeft(vColumns As Integer)
            Try
                If pFirstVisibleColumn > 0 Then
                    pFirstVisibleColumn = Math.Max(0, pFirstVisibleColumn - vColumns)
                    pHScrollbar.Value = pFirstVisibleColumn
                    pDrawingArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollLeft error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ScrollRight(vColumns As Integer)
            Try
                Dim lMaxColumns As Integer = CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth))
                Dim lMaxFirstColumn As Integer = Math.Max(0, lMaxColumns - pTotalVisibleColumns)
                If pFirstVisibleColumn < lMaxFirstColumn Then
                    pFirstVisibleColumn = Math.Min(lMaxFirstColumn, pFirstVisibleColumn + vColumns)
                    pHScrollbar.Value = pFirstVisibleColumn
                    pDrawingArea.QueueDraw()
                End If
            Catch ex As Exception
                Console.WriteLine($"ScrollRight error: {ex.Message}")
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
        
        ' ===== Calculate Visible Range =====
        Public Function GetFirstVisibleLine() As Integer
            Return pFirstVisibleLine
        End Function
        
        Public Function GetLastVisibleLine() As Integer
            Return Math.Min(pFirstVisibleLine + pTotalVisibleLines - 1, pLineCount - 1)
        End Function
        
        Public Function GetFirstVisibleColumn() As Integer
            Return pFirstVisibleColumn
        End Function
        
        Public Function GetLastVisibleColumn() As Integer
            Return pFirstVisibleColumn + pTotalVisibleColumns - 1
        End Function
        
        ' ===== Auto-scroll during selection =====
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
                    Dim lMaxColumns As Integer = CInt(Math.Ceiling(CDbl(pMaxLineWidth) / pCharWidth))
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

'        ' ===== Auto-scroll when dragging near edges =====
'        Private Sub AutoScrollIfNearEdge(vX As Double, vY As Double)
'            Try
'                Dim lScrollMargin As Integer = 30
'                Dim lScrollSpeed As Integer = 3
'                
'                ' Check vertical scrolling
'                If vY < lScrollMargin Then
'                    ' Scroll up
'                    pVScrollbar.Value = Math.Max(0, pVScrollbar.Value - 1)
'                ElseIf vY > pViewportHeight - lScrollMargin Then
'                    ' Scroll down
'                    pVScrollbar.Value = Math.Min(pVScrollbar.Adjustment.Upper - pVScrollbar.Adjustment.PageSize, 
'                                                pVScrollbar.Value + 1)
'                End If
'                
'                ' Check horizontal scrolling
'                If vX < pLeftPadding + lScrollMargin Then
'                    ' Scroll left
'                    pHScrollbar.Value = Math.Max(0, pHScrollbar.Value - lScrollSpeed)
'                ElseIf vX > pViewportWidth - lScrollMargin Then
'                    ' Scroll right
'                    pHScrollbar.Value = Math.Min(pHScrollbar.Adjustment.Upper - pHScrollbar.Adjustment.PageSize, 
'                                                pHScrollbar.Value + lScrollSpeed)
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"AutoScrollIfNearEdge error: {ex.Message}")
'            End Try
'        End Sub

        
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
        
    End Class

End Namespace
