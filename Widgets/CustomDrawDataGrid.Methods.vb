' Widgets/CustomDrawDataGrid.Methods.vb - Helper methods and utilities for the data grid
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing helper methods and utilities for the data grid
    ''' </summary>
    Partial Public Class CustomDrawDataGrid
        Inherits Box
        
        ' ===== Layout Methods =====
        
        
        ''' <summary>
        ''' Updates the total content width based on column widths
        ''' </summary>
        Private Sub UpdateContentWidth()
            Try
                pContentWidth = 0
                for each lColumn in pColumns
                    If lColumn.Visible Then
                        pContentWidth += lColumn.Width
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateContentWidth error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the total content height based on row count
        ''' </summary>
        Private Sub UpdateContentHeight()
            Try
                pContentHeight = pVisibleRows.Count * pRowHeight
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateContentHeight error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates scrollbar adjustments based on content and viewport sizes
        ''' </summary>
        Private Sub UpdateScrollbars()
            Try
                ' Update vertical scrollbar
                pVScrollbar.Adjustment.Lower = 0
                pVScrollbar.Adjustment.Upper = pContentHeight
                pVScrollbar.Adjustment.PageSize = pViewportHeight
                pVScrollbar.Adjustment.StepIncrement = pRowHeight
                pVScrollbar.Adjustment.PageIncrement = pViewportHeight
                
                pVScrollbar.Visible = pContentHeight > pViewportHeight
                
                ' Ensure scroll position is valid
                If pScrollY + pViewportHeight > pContentHeight Then
                    pScrollY = Math.Max(0, pContentHeight - pViewportHeight)
                    pVScrollbar.Value = pScrollY
                End If
                
                ' Update horizontal scrollbar
                pHScrollbar.Adjustment.Lower = 0
                pHScrollbar.Adjustment.Upper = pContentWidth
                pHScrollbar.Adjustment.PageSize = pViewportWidth
                pHScrollbar.Adjustment.StepIncrement = 20
                pHScrollbar.Adjustment.PageIncrement = pViewportWidth
                
                pHScrollbar.Visible = pContentWidth > pViewportWidth
                
                ' Ensure scroll position is valid
                If pScrollX + pViewportWidth > pContentWidth Then
                    pScrollX = Math.Max(0, pContentWidth - pViewportWidth)
                    pHScrollbar.Value = pScrollX
                End If
                
                ' Update corner box visibility
                pCornerBox.Visible = pVScrollbar.Visible AndAlso pHScrollbar.Visible
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateScrollbars error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Position Calculation Methods =====
        
        ''' <summary>
        ''' Gets the column index at the specified X coordinate
        ''' </summary>
        Private Function GetColumnAtX(vX As Integer) As Integer
            Try
                Dim lCurrentX As Integer = 0
                
                for i As Integer = 0 To pColumns.Count - 1
                    If Not pColumns(i).Visible Then Continue for
                    
                    If vX >= lCurrentX AndAlso vX < lCurrentX + pColumns(i).Width Then
                        Return i
                    End If
                    
                    lCurrentX += pColumns(i).Width
                Next
                
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.GetColumnAtX error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the right edge X coordinate of a column
        ''' </summary>
        Private Function GetColumnRight(vColumnIndex As Integer) As Integer
            Try
                Dim lX As Integer = 0
                
                for i As Integer = 0 To vColumnIndex
                    If pColumns(i).Visible Then
                        lX += pColumns(i).Width
                    End If
                Next
                
                Return lX
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.GetColumnRight error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the row index at the specified Y coordinate
        ''' </summary>
        Private Function GetRowAtY(vY As Integer) As Integer
            Try
                If pRowHeight <= 0 Then Return -1
                
                Dim lRowIndex As Integer = vY \ pRowHeight
                
                If lRowIndex >= 0 AndAlso lRowIndex < pVisibleRows.Count Then
                    Return lRowIndex
                End If
                
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.GetRowAtY error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ''' <summary>
        ''' Ensures the specified row is visible by scrolling if necessary
        ''' </summary>
        Public Sub EnsureRowVisible(vRowIndex As Integer)
            Try
                ' Find the row in visible rows
                Dim lVisibleIndex As Integer = -1
                for i As Integer = 0 To pVisibleRows.Count - 1
                    If pVisibleRows(i) = vRowIndex Then
                        lVisibleIndex = i
                        Exit for
                    End If
                Next
                
                If lVisibleIndex < 0 Then Return
                
                Dim lRowTop As Integer = lVisibleIndex * pRowHeight
                Dim lRowBottom As Integer = lRowTop + pRowHeight
                
                If lRowTop < pScrollY Then
                    ' Scroll up
                    pVScrollbar.Value = lRowTop
                ElseIf lRowBottom > pScrollY + pViewportHeight Then
                    ' Scroll down
                    pVScrollbar.Value = lRowBottom - pViewportHeight
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.EnsureRowVisible error: {ex.Message}")
            End Try
        End Sub

        ' Replace: SimpleIDE.Widgets.CustomDrawDataGrid.GetItemAt
        ''' <summary>
        ''' Gets the row and column at the specified point
        ''' </summary>
        Public Function GetItemAt(vX As Integer, vY As Integer) As Gdk.Point
            Try
                ' Adjust for scrolling
                Dim lAdjustedX As Integer = vX + pHorizontalOffset
                Dim lAdjustedY As Integer = vY + pVerticalOffset
                
                ' Calculate row
                Dim lRow As Integer = lAdjustedY \ pRowHeight
                If lRow < 0 OrElse lRow >= pRows.Count Then
                    Return New Gdk.Point(-1, -1)
                End If
                
                ' Calculate column
                Dim lColumn As Integer = -1
                Dim lCurrentX As Integer = 0
                
                for i As Integer = 0 To pColumns.Count - 1
                    If Not pColumns(i).Visible Then Continue for
                    
                    If lAdjustedX >= lCurrentX AndAlso lAdjustedX < lCurrentX + pColumns(i).Width Then
                        lColumn = i
                        Exit for
                    End If
                    
                    lCurrentX += pColumns(i).Width
                Next
                
                Return New Gdk.Point(lColumn, lRow)
                
            Catch ex As Exception
                Console.WriteLine($"GetItemAt error: {ex.Message}")
                Return New Gdk.Point(-1, -1)
            End Try
        End Function
        
        ' ===== Sorting Methods =====
        
        ''' <summary>
        ''' Performs the actual sort operation
        ''' </summary>
        Private Sub PerformSort()
            Try
                If pSortColumn < 0 OrElse pSortColumn >= pColumns.Count Then Return
                If pVisibleRows.Count = 0 Then Return
                
                ' Create a list of row indices with their sort values
                Dim lSortList As New List(Of Tuple(Of Integer, IComparable))
                
                for each lRowIndex in pVisibleRows
                    Dim lRow As DataGridRow = pRows(lRowIndex)
                    Dim lSortValue As IComparable = Nothing
                    
                    If pSortColumn < lRow.Cells.Count Then
                        Dim lCellValue As Object = lRow.Cells(pSortColumn).Value
                        
                        ' Convert to comparable value based on column type
                        Select Case pColumns(pSortColumn).ColumnType
                            Case DataGridColumnType.eNumber
                                If lCellValue IsNot Nothing Then
                                    Try
                                        lSortValue = Convert.ToDouble(lCellValue)
                                    Catch
                                        lSortValue = 0.0
                                    End Try
                                Else
                                    lSortValue = 0.0
                                End If
                                
                            Case DataGridColumnType.eDate
                                If TypeOf lCellValue Is DateTime Then
                                    lSortValue = CType(lCellValue, DateTime)
                                Else
                                    lSortValue = DateTime.MinValue
                                End If
                                
                            Case DataGridColumnType.eBoolean
                                If lCellValue IsNot Nothing Then
                                    lSortValue = Convert.ToBoolean(lCellValue)
                                Else
                                    lSortValue = False
                                End If
                                
                            Case Else
                                lSortValue = If(lCellValue?.ToString(), "")
                        End Select
                    Else
                        lSortValue = ""
                    End If
                    
                    lSortList.Add(New Tuple(Of Integer, IComparable)(lRowIndex, lSortValue))
                Next
                
                ' Sort the list
                If pSortAscending Then
                    lSortList.Sort(Function(a, b) Comparer(Of IComparable).Default.Compare(a.Item2, b.Item2))
                Else
                    lSortList.Sort(Function(a, b) Comparer(Of IComparable).Default.Compare(b.Item2, a.Item2))
                End If
                
                ' Update visible rows with sorted order
                pVisibleRows.Clear()
                for each lTuple in lSortList
                    pVisibleRows.Add(lTuple.Item1)
                Next
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.PerformSort error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Data Manipulation Methods =====
        
        ''' <summary>
        ''' Updates a cell value
        ''' </summary>
        Public Sub UpdateCell(vRowIndex As Integer, vColumnIndex As Integer, vValue As Object)
            Try
                If vRowIndex < 0 OrElse vRowIndex >= pRows.Count Then Return
                If vColumnIndex < 0 OrElse vColumnIndex >= pColumns.Count Then Return
                
                Dim lRow As DataGridRow = pRows(vRowIndex)
                
                ' Ensure cell exists
                While lRow.Cells.Count <= vColumnIndex
                    lRow.Cells.Add(New DataGridCell())
                End While
                
                Dim lOldValue As Object = lRow.Cells(vColumnIndex).Value
                lRow.Cells(vColumnIndex).Value = vValue
                lRow.Cells(vColumnIndex).DisplayText = If(vValue?.ToString(), "")
                
                pDrawingArea?.QueueDraw()
                
                RaiseEvent CellEdited(vRowIndex, vColumnIndex, lOldValue, vValue)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateCell error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Removes a row from the grid
        ''' </summary>
        Public Sub RemoveRow(vRowIndex As Integer)
            Try
                If vRowIndex < 0 OrElse vRowIndex >= pRows.Count Then Return
                
                ' Remove from main list
                pRows.RemoveAt(vRowIndex)
                
                ' Update indices
                for i As Integer = vRowIndex To pRows.Count - 1
                    pRows(i).Index = i
                Next
                
                ' Rebuild visible rows
                RebuildVisibleRows()
                
                ' Adjust selection
                If pSelectedRowIndex >= pRows.Count Then
                    pSelectedRowIndex = pRows.Count - 1
                End If
                
                UpdateLayout()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.RemoveRow error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Rebuilds the visible rows list (used after filtering or deletion)
        ''' </summary>
        Private Sub RebuildVisibleRows()
            Try
                pVisibleRows.Clear()
                
                for i As Integer = 0 To pRows.Count - 1
                    If pRows(i).Visible Then
                        pVisibleRows.Add(i)
                    End If
                Next
                
                ' Re-apply sort if active
                If pSortColumn >= 0 Then
                    PerformSort()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.RebuildVisibleRows error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Filtering Methods =====
        
        ''' <summary>
        ''' Applies a filter to show only matching rows
        ''' </summary>
        Public Sub ApplyFilter(vFilterText As String, Optional vColumnIndex As Integer = -1)
            Try
                If String.IsNullOrEmpty(vFilterText) Then
                    ' Clear filter
                    for each lRow in pRows
                        lRow.Visible = True
                    Next
                Else
                    ' Apply filter
                    Dim lFilterLower As String = vFilterText.ToLower()
                    
                    for each lRow in pRows
                        Dim lMatch As Boolean = False
                        
                        If vColumnIndex >= 0 AndAlso vColumnIndex < lRow.Cells.Count Then
                            ' Filter specific column
                            Dim lCellText As String = lRow.Cells(vColumnIndex).DisplayText.ToLower()
                            lMatch = lCellText.Contains(lFilterLower)
                        Else
                            ' Filter all columns
                            for each lCell in lRow.Cells
                                If lCell.DisplayText.ToLower().Contains(lFilterLower) Then
                                    lMatch = True
                                    Exit for
                                End If
                            Next
                        End If
                        
                        lRow.Visible = lMatch
                    Next
                End If
                
                RebuildVisibleRows()
                UpdateLayout()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.ApplyFilter error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Selection Methods =====
        
        ''' <summary>
        ''' Selects all visible rows
        ''' </summary>
        Public Sub SelectAll()
            Try
                If Not pMultiSelectEnabled Then Return
                
                pSelectedRows.Clear()
                for each lRowIndex in pVisibleRows
                    pSelectedRows.Add(lRowIndex)
                Next
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SelectAll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clears all selections
        ''' </summary>
        Public Sub ClearSelection()
            Try
                pSelectedRowIndex = -1
                pSelectedColumnIndex = -1
                pSelectedRows.Clear()
                pFocusedCell = New Gdk.Point(-1, -1)
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.ClearSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the currently selected rows
        ''' </summary>
        Public Function GetSelectedRows() As List(Of DataGridRow)
            Try
                Dim lResult As New List(Of DataGridRow)
                
                If pSelectedRows.Count > 0 Then
                    for each lIndex in pSelectedRows
                        If lIndex < pRows.Count Then
                            lResult.Add(pRows(lIndex))
                        End If
                    Next
                ElseIf pSelectedRowIndex >= 0 AndAlso pSelectedRowIndex < pRows.Count Then
                    lResult.Add(pRows(pSelectedRowIndex))
                End If
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.GetSelectedRows error: {ex.Message}")
                Return New List(Of DataGridRow)
            End Try
        End Function
        
        ' ===== Column Management =====
        
        ''' <summary>
        ''' Shows or hides a column
        ''' </summary>
        Public Sub SetColumnVisible(vColumnIndex As Integer, vVisible As Boolean)
            Try
                If vColumnIndex < 0 OrElse vColumnIndex >= pColumns.Count Then Return
                
                pColumns(vColumnIndex).Visible = vVisible
                UpdateLayout()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SetColumnVisible error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the width of a column
        ''' </summary>
        Public Sub SetColumnWidth(vColumnIndex As Integer, vWidth As Integer)
            Try
                If vColumnIndex < 0 OrElse vColumnIndex >= pColumns.Count Then Return
                
                pColumns(vColumnIndex).Width = Math.Max(MIN_COLUMN_WIDTH, vWidth)
                UpdateLayout()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SetColumnWidth error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Auto-sizes a column to fit its content
        ''' </summary>
        Public Sub AutoSizeColumn(vColumnIndex As Integer)
            Try
                If vColumnIndex < 0 OrElse vColumnIndex >= pColumns.Count Then Return
                
                ' Measure all cell contents
                Dim lMaxWidth As Integer = MIN_COLUMN_WIDTH
                
                ' Measure header
                ' (Would need Cairo context to measure properly - simplified for now)
                lMaxWidth = Math.Max(lMaxWidth, pColumns(vColumnIndex).Title.Length * 8 + CELL_PADDING * 2)
                
                ' Measure cells
                for each lRow in pRows
                    If vColumnIndex < lRow.Cells.Count Then
                        Dim lText As String = lRow.Cells(vColumnIndex).DisplayText
                        lMaxWidth = Math.Max(lMaxWidth, lText.Length * 7 + CELL_PADDING * 2)
                    End If
                Next
                
                pColumns(vColumnIndex).Width = Math.Min(lMaxWidth, pColumns(vColumnIndex).MaxWidth)
                UpdateLayout()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.AutoSizeColumn error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates the header cursor based on mouse position
        ''' </summary>
        Private Sub UpdateHeaderCursor(vX As Integer)
            Try
                Dim lDisplay As Gdk.Display = pHeaderArea.Display
                
                If IsOverColumnBorder(vX) <> -1 AndAlso pAllowColumnResize Then
                    ' Use resize cursor - directly create with CursorType
                    Dim lCursor As New Gdk.Cursor(lDisplay, Gdk.CursorType.SbHDoubleArrow)
                    pHeaderArea.Window.Cursor = lCursor
                Else
                    ' Use default cursor
                    pHeaderArea.Window.Cursor = Nothing
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateHeaderCursor error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates the content cursor based on hover state
        ''' </summary>
        Private Sub UpdateContentCursor(vX As Integer, vY As Integer)
            Try
                Dim lDisplay As Gdk.Display = pDrawingArea.Display
                Dim lRowIndex As Integer = GetRowAtPoint(vY)
                
                If lRowIndex >= 0 AndAlso lRowIndex < pRows.Count Then
                    ' Use hand cursor for hovering over rows - directly create with CursorType
                    Dim lCursor As New Gdk.Cursor(lDisplay, Gdk.CursorType.Hand1)
                    pDrawingArea.Window.Cursor = lCursor
                Else
                    ' Use default cursor
                    pDrawingArea.Window.Cursor = Nothing
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateContentCursor error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks if the X coordinate is over a column border for resizing
        ''' </summary>
        Private Function IsOverColumnBorder(vX As Integer) As Integer
            Try
                Dim lCurrentX As Integer = -pScrollX
                
                for i As Integer = 0 To pColumns.Count - 1
                    If Not pColumns(i).Visible Then Continue for
                    
                    lCurrentX += pColumns(i).Width
                    
                    ' Check if near the right edge of this column
                    If Math.Abs(vX - lCurrentX) <= 3 Then
                        Return i
                    End If
                Next
                
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"IsOverColumnBorder error: {ex.Message}")
                Return -1
            End Try
        End Function

        ''' <summary>
        ''' Gets the row index at the specified Y coordinate (for content area)
        ''' </summary>
        Private Function GetRowAtPoint(vY As Integer) As Integer
            Try
                ' This is an alias for GetRowAtY but adjusted for scrolling
                Return GetRowAtY(vY + pScrollY)
                
            Catch ex As Exception
                Console.WriteLine($"GetRowAtPoint error: {ex.Message}")
                Return -1
            End Try
        End Function

        ''' <summary>
        ''' Formats a cell value based on column type for display
        ''' </summary>
        ''' <param name="vValue">The raw value to format</param>
        ''' <param name="vColumnType">The type of the column</param>
        ''' <returns>Formatted string representation of the value</returns>
        Private Function FormatCellValue(vValue As Object, vColumnType As DataGridColumnType) As String
            Try
                If vValue Is Nothing Then Return ""
                
                Select Case vColumnType
                    Case DataGridColumnType.eText
                        Return vValue.ToString()
                        
                    Case DataGridColumnType.eNumber
                        ' Format numbers with appropriate precision
                        If TypeOf vValue Is Integer Then
                            Return CInt(vValue).ToString()
                        ElseIf TypeOf vValue Is Double Then
                            Return CDbl(vValue).ToString("F2")  ' 2 decimal places
                        ElseIf TypeOf vValue Is Decimal Then
                            Return CDec(vValue).ToString("F2")
                        Else
                            Return vValue.ToString()
                        End If
                        
                    Case DataGridColumnType.eBoolean
                        ' Format boolean as Yes/No or True/False
                        ' FIXED: Handle empty strings and non-boolean values properly
                        If TypeOf vValue Is Boolean Then
                            Return If(CBool(vValue), "Yes", "No")
                        ElseIf TypeOf vValue Is String Then
                            ' Handle string representations of boolean values
                            Dim lStringValue As String = vValue.ToString().Trim().ToLower()
                            Select Case lStringValue
                                Case "", "0", "false", "no", "n"
                                    Return "No"
                                Case "1", "true", "yes", "y"
                                    Return "Yes"
                                Case Else
                                    ' Try to parse as boolean, default to "No" if it fails
                                    Dim lBoolValue As Boolean
                                    If Boolean.TryParse(lStringValue, lBoolValue) Then
                                        Return If(lBoolValue, "Yes", "No")
                                    Else
                                        Return "No"  ' Default for unparseable values
                                    End If
                            End Select
                        Else
                            ' For other types, try to convert safely
                            Try
                                Return If(CBool(vValue), "Yes", "No")
                            Catch
                                Return "No"  ' Default if conversion fails
                            End Try
                        End If
                        
                    Case DataGridColumnType.eDate
                        ' Format dates in standard format
                        If TypeOf vValue Is DateTime Then
                            Return CDate(vValue).ToString("yyyy-MM-dd HH:mm:ss")
                        ElseIf TypeOf vValue Is DateOnly Then
                            Return CType(vValue, DateOnly).ToString("yyyy-MM-dd")
                        Else
                            Return vValue.ToString()
                        End If
                        
                    Case DataGridColumnType.eIcon
                        ' Icons are typically rendered, not displayed as text
                        Return ""
                        
                    Case Else
                        ' Default formatting
                        Return vValue.ToString()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.FormatCellValue error: {ex.Message}")
                Return If(vValue?.ToString(), "")
            End Try
        End Function

        ''' <summary>
        ''' Updates column widths, handling auto-expand columns
        ''' </summary>
        Private Sub UpdateColumnWidths()
            Try
                If pColumns.Count = 0 Then Return
                
                ' Calculate total fixed width and count auto-expand columns
                Dim lFixedWidth As Integer = 0
                Dim lAutoExpandColumns As New List(Of DataGridColumn)
                
                for each lColumn As DataGridColumn in pColumns
                    If lColumn.Visible Then
                        If lColumn.AutoExpand Then
                            lAutoExpandColumns.Add(lColumn)
                        Else
                            lFixedWidth += lColumn.Width
                        End If
                    End If
                Next
                
                ' If we have auto-expand columns, distribute remaining space
                If lAutoExpandColumns.Count > 0 AndAlso pViewportWidth > 0 Then
                    Dim lAvailableWidth As Integer = pViewportWidth - lFixedWidth - SCROLLBAR_WIDTH
                    If lAvailableWidth > 0 Then
                        Dim lExpandWidth As Integer = lAvailableWidth \ lAutoExpandColumns.Count
                        
                        for each lColumn As DataGridColumn in lAutoExpandColumns
                            lColumn.Width = Math.Max(lColumn.MinWidth, lExpandWidth)
                        Next
                    End If
                End If
                
                ' Update content width
                UpdateContentWidth()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateColumnWidths error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Calculates the height needed for a row based on word wrapping
        ''' </summary>
        ''' <param name="vRowIndex">Index of the row</param>
        ''' <returns>Required height in pixels</returns>
        Private Function CalculateRowHeight(vRowIndex As Integer) As Integer
            Try
                If vRowIndex < 0 OrElse vRowIndex >= pRows.Count Then
                    Return pRowHeight
                End If
                
                Dim lRow As DataGridRow = pRows(vRowIndex)
                Dim lMaxHeight As Integer = pRowHeight
                
                ' Check each cell for word-wrapped content
                for i As Integer = 0 To Math.Min(pColumns.Count - 1, lRow.Cells.Count - 1)
                    Dim lColumn As DataGridColumn = pColumns(i)
                    
                    If lColumn.Visible AndAlso lColumn.WordWrap AndAlso i < lRow.Cells.Count Then
                        Dim lCell As DataGridCell = lRow.Cells(i)
                        Dim lText As String = If(lCell.DisplayText, "")
                        
                        If Not String.IsNullOrEmpty(lText) Then
                            ' Calculate wrapped height (simplified - would need Cairo context for accurate measurement)
                            Dim lCharsPerLine As Integer = Math.Max(1, (lColumn.Width - CELL_PADDING * 2) \ 7)
                            Dim lLines As Integer = Math.Ceiling(lText.Length / lCharsPerLine)
                            Dim lCellHeight As Integer = (lLines * (pFontSize + 4)) + CELL_PADDING * 2
                            
                            ' Apply maximum height constraint
                            lCellHeight = Math.Min(lCellHeight, lColumn.MaxHeight)
                            lMaxHeight = Math.Max(lMaxHeight, lCellHeight)
                        End If
                    End If
                Next
                
                ' Store calculated height
                lRow.CalculatedHeight = lMaxHeight
                Return lMaxHeight
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.CalculateRowHeight error: {ex.Message}")
                Return pRowHeight
            End Try
        End Function
        
    End Class
    
End Namespace 
