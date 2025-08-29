' Widgets/CustomDrawDataGrid.Events.vb - Event handlers for self-contained data grid
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing event handlers for CustomDrawDataGrid
    ''' </summary>
    Partial Public Class CustomDrawDataGrid
        Inherits Box
        
        ' ===== Mouse Events - Header =====
        
        ''' <summary>
        ''' Handles mouse button press on header
        ''' </summary>
        Private Function OnHeaderButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                Dim lX As Integer = CInt(vArgs.Event.X) + pHorizontalOffset
                Dim lColumnIndex As Integer = GetColumnAtX(lX)
                
                If lColumnIndex >= 0 Then
                    Dim lColumn As DataGridColumn = pColumns(lColumnIndex)
                    Dim lColumnRight As Integer = GetColumnRight(lColumnIndex)
                    
                    ' Check if clicking on resize handle (within 3 pixels of right edge)
                    If pAllowColumnResize AndAlso lColumn.Resizable AndAlso 
                       Math.Abs(lX - lColumnRight) <= 3 Then
                        ' Start column resize
                        pResizingColumn = lColumnIndex
                        pResizeStartX = CInt(vArgs.Event.X)
                        pResizeStartWidth = lColumn.Width
                        Return True
                    ElseIf pAllowSort AndAlso lColumn.Sortable Then
                        ' Handle column sort
                        If pSortColumn = lColumnIndex Then
                            pSortAscending = Not pSortAscending
                        Else
                            pSortColumn = lColumnIndex
                            pSortAscending = True
                        End If
                        
                        SortRows()
                        QueueDraw()
                        RaiseEvent SortChanged(lColumnIndex, pSortAscending)
                        Return True
                    End If
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnHeaderButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse button release on header
        ''' </summary>
        Private Function OnHeaderButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                If pResizingColumn >= 0 Then
                    pResizingColumn = -1
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnHeaderButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse motion on header
        ''' </summary>
        Private Function OnHeaderMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                If pResizingColumn >= 0 Then
                    ' Currently resizing
                    Dim lDelta As Integer = CInt(vArgs.Event.X) - pResizeStartX
                    Dim lNewWidth As Integer = Math.Max(MIN_COLUMN_WIDTH, pResizeStartWidth + lDelta)
                    
                    pColumns(pResizingColumn).Width = lNewWidth
                    UpdateLayout()
                    QueueDraw()
                    
                    RaiseEvent ColumnResized(pResizingColumn, lNewWidth)
                Else
                    ' Check if near column edge for resize cursor
                    Dim lX As Integer = CInt(vArgs.Event.X) + pHorizontalOffset
                    Dim lNearResize As Boolean = False
                    
                    Dim lCurrentX As Integer = 0
                    for i As Integer = 0 To pColumns.Count - 1
                        If Not pColumns(i).Visible Then Continue for
                        
                        lCurrentX += pColumns(i).Width
                        
                        ' Check if near the right edge of this column
                        If pColumns(i).Resizable AndAlso Math.Abs(lX - lCurrentX) <= 3 Then
                            lNearResize = True
                            Exit for
                        End If
                    Next
                    
                    ' Set appropriate cursor
                    Dim lWindow As Gdk.Window = pHeaderArea.Window
                    If lWindow IsNot Nothing Then
                        If lNearResize Then
                            ' Create resize cursor using CursorType enum
                            Dim lDisplay As Gdk.Display = lWindow.Display
                            Dim lCursor As New Gdk.Cursor(lDisplay, Gdk.CursorType.SbHDoubleArrow)
                            lWindow.Cursor = lCursor
                        Else
                            lWindow.Cursor = Nothing
                        End If
                    End If
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnHeaderMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse leave on header
        ''' </summary>
        Private Function OnHeaderLeave(vSender As Object, vArgs As LeaveNotifyEventArgs) As Boolean
            Try
                ' Reset cursor
                Dim lWindow As Gdk.Window = pHeaderArea.Window
                If lWindow IsNot Nothing Then
                    lWindow.Cursor = Nothing
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnHeaderLeave error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Mouse Events - Content =====
        
        ''' <summary>
        ''' Handles mouse button press on content
        ''' </summary>
        Private Function OnContentButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                pDrawingArea.GrabFocus()
                
                Dim lRowIndex As Integer = GetRowAtY(CInt(vArgs.Event.Y))
                Dim lColumnIndex As Integer = GetColumnAtX(CInt(vArgs.Event.X) + pHorizontalOffset)
                
                If lRowIndex >= 0 AndAlso lRowIndex < pRows.Count Then
                    ' Handle selection
                    If vArgs.Event.Type = EventType.TwoButtonPress Then
                        ' Double-click
                        RaiseEvent RowDoubleClicked(lRowIndex, pRows(lRowIndex))
                        If lColumnIndex >= 0 Then
                            Dim lCell As DataGridCell = Nothing
                            If lColumnIndex < pRows(lRowIndex).Cells.Count Then
                                lCell = pRows(lRowIndex).Cells(lColumnIndex)
                            End If
                            RaiseEvent CellDoubleClicked(lRowIndex, lColumnIndex, lCell?.Value)
                        End If
                        Return True
                    Else
                        ' Single click - select row
                        If pMultiSelectEnabled AndAlso (vArgs.Event.State and ModifierType.ControlMask) <> 0 Then
                            ' Control-click for multi-select
                            If pSelectedRows.Contains(lRowIndex) Then
                                pSelectedRows.Remove(lRowIndex)
                            Else
                                pSelectedRows.Add(lRowIndex)
                            End If
                        Else
                            ' Normal selection
                            pSelectedRows.Clear()
                            pSelectedRowIndex = lRowIndex
                        End If
                        
                        pSelectedColumnIndex = lColumnIndex
                        QueueDraw()
                        RaiseEvent SelectionChanged(lRowIndex, lColumnIndex)
                        Return True
                    End If
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse button release on content
        ''' </summary>
        Private Function OnContentButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse motion on content
        ''' </summary>
        Private Function OnContentMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                Dim lRowIndex As Integer = GetRowAtY(CInt(vArgs.Event.Y))
                
                If lRowIndex <> pHoverRowIndex Then
                    pHoverRowIndex = lRowIndex
                    pDrawingArea.QueueDraw()
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse leave on content
        ''' </summary>
        Private Function OnContentLeave(vSender As Object, vArgs As LeaveNotifyEventArgs) As Boolean
            Try
                If pHoverRowIndex >= 0 Then
                    pHoverRowIndex = -1
                    pDrawingArea.QueueDraw()
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentLeave error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles scroll events on content
        ''' </summary>
        Private Function OnContentScroll(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                Select Case vArgs.Event.Direction
                    Case ScrollDirection.Up
                        pVScrollbar.Value = Math.Max(0, pVScrollbar.Value - pRowHeight * 3)
                        Return True
                        
                    Case ScrollDirection.Down
                        pVScrollbar.Value = Math.Min(pMaxVerticalScroll, pVScrollbar.Value + pRowHeight * 3)
                        Return True
                        
                    Case ScrollDirection.Left
                        If pHScrollbar.Visible Then
                            pHScrollbar.Value = Math.Max(0, pHScrollbar.Value - 20)
                        End If
                        Return True
                        
                    Case ScrollDirection.Right
                        If pHScrollbar.Visible Then
                            pHScrollbar.Value = Math.Min(pHScrollbar.Adjustment.Upper - pHScrollbar.Adjustment.PageSize,
                                                       pHScrollbar.Value + 20)
                        End If
                        Return True
                End Select
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentScroll error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Keyboard Events =====
        
        ''' <summary>
        ''' Handles key press events
        ''' </summary>
        Private Function OnContentKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
            Try
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Up
                        MoveSelection(-1)
                        Return True
                        
                    Case Gdk.Key.Down
                        MoveSelection(1)
                        Return True
                        
                    Case Gdk.Key.Page_Up
                        MoveSelection(-(pViewportHeight \ pRowHeight))
                        Return True
                        
                    Case Gdk.Key.Page_Down
                        MoveSelection(pViewportHeight \ pRowHeight)
                        Return True
                        
                    Case Gdk.Key.Home
                        If pRows.Count > 0 Then
                            SelectRow(0)
                        End If
                        Return True
                        
                    Case Gdk.Key.End
                        If pRows.Count > 0 Then
                            SelectRow(pRows.Count - 1)
                        End If
                        Return True
                End Select
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentKeyPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Helper Methods =====

        
        ''' <summary>
        ''' Moves the selection by the specified delta
        ''' </summary>
        Private Sub MoveSelection(vDelta As Integer)
            Try
                If pRows.Count = 0 Then Return
                
                Dim lNewIndex As Integer = Math.Max(0, Math.Min(pRows.Count - 1, pSelectedRowIndex + vDelta))
                SelectRow(lNewIndex)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.MoveSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Selects a specific row and ensures it's visible
        ''' </summary>
        Private Sub SelectRow(vRowIndex As Integer)
            Try
                If vRowIndex < 0 OrElse vRowIndex >= pRows.Count Then Return
                
                pSelectedRowIndex = vRowIndex
                pSelectedRows.Clear()
                
                ' Ensure row is visible
                EnsureRowVisible(vRowIndex)
                
                QueueDraw()
                RaiseEvent SelectionChanged(vRowIndex, pSelectedColumnIndex)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SelectRow error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Sorts rows based on the current sort column
        ''' </summary>
        Private Sub SortRows()
            Try
                If pSortColumn < 0 OrElse pSortColumn >= pColumns.Count Then Return
                
                ' TODO: Implement actual sorting logic based on column type
                ' For now, just trigger a redraw
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SortRows error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace