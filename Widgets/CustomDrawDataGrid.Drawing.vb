' Widgets/CustomDrawDataGrid.Drawing.vb - Drawing methods for self-contained data grid
Imports Gtk
Imports Cairo
Imports Pango
Imports System
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing drawing methods for CustomDrawDataGrid
    ''' </summary>
    Partial Public Class CustomDrawDataGrid
        Inherits Box
        
        ' ===== Main Drawing Event Handlers =====
        
        ''' <summary>
        ''' Handles drawing of the header area
        ''' </summary>
        Private Function OnHeaderDrawn(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawHeader(vArgs.Cr)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnHeaderDrawn error: {ex.Message}")
                Return True
            End Try
        End Function
        
        ''' <summary>
        ''' Handles drawing of the content area
        ''' </summary>
        Private Function OnContentDrawn(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawContent(vArgs.Cr)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentDrawn error: {ex.Message}")
                Return True
            End Try
        End Function
        
        ' ===== Header Drawing =====
        
        ''' <summary>
        ''' Draws the header row with column titles
        ''' </summary>
        Private Sub DrawHeader(vContext As Cairo.Context)
            Try
                If Not pShowHeaders Then Return
                
                Dim lWidth As Integer = pHeaderArea.AllocatedWidth
                Dim lHeight As Integer = pHeaderArea.AllocatedHeight
                
                ' Clear header background
                SetSourceColor(vContext, pHeaderBackgroundColor)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()
                
                ' Draw column headers
                Dim lX As Integer = -pHorizontalOffset
                for i As Integer = 0 To pColumns.Count - 1
                    Dim lColumn As DataGridColumn = pColumns(i)
                    If Not lColumn.Visible Then Continue for
                    
                    ' Skip columns that are completely off-screen
                    If lX + lColumn.Width < 0 Then
                        lX += lColumn.Width
                        Continue for
                    End If
                    If lX > lWidth Then Exit for
                    
                    ' Draw column header
                    DrawColumnHeader(vContext, lColumn, i, lX, 0, lColumn.Width, lHeight)
                    
                    ' Draw column separator
                    If pShowGridLines Then
                        SetSourceColor(vContext, pGridLineColor)
                        vContext.LineWidth = GRID_LINE_WIDTH
                        vContext.MoveTo(lX + lColumn.Width - 0.5, 0)
                        vContext.LineTo(lX + lColumn.Width - 0.5, lHeight)
                        vContext.Stroke()
                    End If
                    
                    lX += lColumn.Width
                Next
                
                ' Draw bottom border
                If pShowGridLines Then
                    SetSourceColor(vContext, pGridLineColor)
                    vContext.LineWidth = GRID_LINE_WIDTH
                    vContext.MoveTo(0, lHeight - 0.5)
                    vContext.LineTo(lWidth, lHeight - 0.5)
                    vContext.Stroke()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.DrawHeader error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a single column header
        ''' </summary>
        Private Sub DrawColumnHeader(vContext As Cairo.Context, vColumn As DataGridColumn, vIndex As Integer, 
                                    vX As Double, vY As Double, vWidth As Double, vHeight As Double)
            Try
                ' Create text layout - use CairoHelper
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = New FontDescription() with {
                    .Family = pFontFamily,
                    .Size = pFontSize * Pango.Scale.PangoScale,
                    .Weight = Weight.Bold
                }
                
                ' Set text
                lLayout.SetText(vColumn.Title)
                lLayout.Width = CInt((vWidth - CELL_PADDING * 2) * Pango.Scale.PangoScale)
                lLayout.Ellipsize = EllipsizeMode.End
                
                ' Draw text
                SetSourceColor(vContext, pHeaderForegroundColor)
                vContext.MoveTo(vX + CELL_PADDING, vY + (vHeight - GetTextHeight(lLayout)) / 2)
                Pango.CairoHelper.ShowLayout(vContext, lLayout)
                
                ' Draw sort arrow if this is the sorted column
                If pSortColumn = vIndex AndAlso vColumn.Sortable Then
                    DrawSortArrow(vContext, vX + vWidth - SORT_ARROW_SIZE - CELL_PADDING, 
                                vY + (vHeight - SORT_ARROW_SIZE) / 2, pSortAscending)
                End If
                
                lLayout.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.DrawColumnHeader error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Draws a sort arrow indicator
        ''' </summary>
        Private Sub DrawSortArrow(vContext As Cairo.Context, vX As Double, vY As Double, vAscending As Boolean)
            Try
                SetSourceColor(vContext, pHeaderForegroundColor)
                
                If vAscending Then
                    ' Draw up arrow
                    vContext.MoveTo(vX + SORT_ARROW_SIZE / 2, vY)
                    vContext.LineTo(vX, vY + SORT_ARROW_SIZE)
                    vContext.LineTo(vX + SORT_ARROW_SIZE, vY + SORT_ARROW_SIZE)
                    vContext.ClosePath()
                Else
                    ' Draw down arrow
                    vContext.MoveTo(vX, vY)
                    vContext.LineTo(vX + SORT_ARROW_SIZE, vY)
                    vContext.LineTo(vX + SORT_ARROW_SIZE / 2, vY + SORT_ARROW_SIZE)
                    vContext.ClosePath()
                End If
                
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.DrawSortArrow error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Content Drawing =====
        
        ''' <summary>
        ''' Draws the main content area (rows and cells)
        ''' </summary>
        Private Sub DrawContent(vContext As Cairo.Context)
            Try
                Dim lWidth As Integer = pDrawingArea.AllocatedWidth
                Dim lHeight As Integer = pDrawingArea.AllocatedHeight
                
                ' Clear background
                SetSourceColor(vContext, pBackgroundColor)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()
                
                ' Calculate visible row range
                Dim lFirstVisibleRow As Integer = pVerticalOffset
                Dim lVisibleRowCount As Integer = Math.Min((lHeight \ pRowHeight) + 2, pRows.Count - lFirstVisibleRow)
                Dim lLastVisibleRow As Integer = Math.Min(lFirstVisibleRow + lVisibleRowCount - 1, pRows.Count - 1)
                
                ' Draw visible rows
                Dim lY As Integer = -(pScrollY Mod pRowHeight)  ' Handle partial row scrolling
                for lRowIndex As Integer = lFirstVisibleRow To lLastVisibleRow
                    If lRowIndex >= 0 AndAlso lRowIndex < pRows.Count Then
                        DrawRow(vContext, lRowIndex, -pHorizontalOffset, lY, lWidth + pHorizontalOffset, pRowHeight)
                        lY += pRowHeight
                    End If
                Next
                
                ' Draw grid lines if enabled
                If pShowGridLines Then
                    DrawGridLines(vContext, lWidth, lHeight, lFirstVisibleRow, lVisibleRowCount)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.DrawContent error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a single row of data with support for color overrides
        ''' </summary>
        Private Sub DrawRow(vContext As Cairo.Context, vRowIndex As Integer, vX As Double, vY As Double, 
                           vWidth As Double, vHeight As Double)
            Try
                If vRowIndex < 0 OrElse vRowIndex >= pRows.Count Then Return
                
                Dim lRow As DataGridRow = pRows(vRowIndex)
                Dim lIsSelected As Boolean = (vRowIndex = pSelectedRowIndex) OrElse pSelectedRows.Contains(vRowIndex)
                Dim lIsHover As Boolean = (vRowIndex = pHoverRowIndex)
                
                ' Determine row background color
                Dim lBackgroundColor As String = pBackgroundColor
                
                ' First check if there's a custom color provider
                If GetRowBackgroundColor IsNot Nothing Then
                    Dim lCustomColor As String = GetRowBackgroundColor(vRowIndex, lRow, lIsSelected, lIsHover)
                    If Not String.IsNullOrEmpty(lCustomColor) Then
                        lBackgroundColor = lCustomColor
                    End If
                Else
                    ' Use standard logic if no custom provider
                    If lIsSelected Then
                        lBackgroundColor = pSelectionColor
                    ElseIf lIsHover Then
                        lBackgroundColor = pHoverColor
                    ElseIf pAlternateRowColors AndAlso (vRowIndex Mod 2 = 1) Then
                        lBackgroundColor = pAlternateRowColor
                    End If
                End If
                
                ' Draw row background
                If lBackgroundColor <> pBackgroundColor OrElse lIsSelected OrElse lIsHover Then
                    SetSourceColor(vContext, lBackgroundColor)
                    vContext.Rectangle(vX, vY, vWidth, vHeight)
                    vContext.Fill()
                End If
                
                ' Determine text color
                Dim lForegroundColor As String = If(lIsSelected, pSelectionTextColor, pForegroundColor)
                
                ' Draw cells
                Dim lCellX As Double = vX
                for i As Integer = 0 To Math.Min(pColumns.Count - 1, lRow.Cells.Count - 1)
                    Dim lColumn As DataGridColumn = pColumns(i)
                    If Not lColumn.Visible Then Continue for
                    
                    ' Skip cells that are completely off-screen
                    If lCellX + lColumn.Width < 0 Then
                        lCellX += lColumn.Width
                        Continue for
                    End If
                    If lCellX > vX + vWidth Then Exit for
                    
                    ' Draw cell with possible custom foreground color
                    If i < lRow.Cells.Count Then
                        ' Check for custom cell foreground color
                        Dim lCellForeground As String = lForegroundColor
                        If GetCellForegroundColor IsNot Nothing Then
                            Dim lCustomCellColor As String = GetCellForegroundColor(vRowIndex, i, lRow.Cells(i))
                            If Not String.IsNullOrEmpty(lCustomCellColor) Then
                                lCellForeground = lCustomCellColor
                            End If
                        End If
                        
                        DrawCell(vContext, lRow.Cells(i), lColumn, lCellX, vY, lColumn.Width, vHeight, lCellForeground)
                    End If
                    
                    lCellX += lColumn.Width
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.DrawRow error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a single cell in the content area
        ''' </summary>
        ''' <param name="vContext">Cairo context for drawing</param>
        ''' <param name="vCell">The cell data to draw</param>
        ''' <param name="vColumn">The column definition</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vWidth">Width of the cell</param>
        ''' <param name="vHeight">Height of the cell</param>
        ''' <param name="vForegroundColor">Default text color to use</param>
        Private Sub DrawCell(vContext As Cairo.Context, vCell As DataGridCell, vColumn As DataGridColumn,
                            vX As Double, vY As Double, vWidth As Double, vHeight As Double,
                            vForegroundColor As String)
            Try
                ' Handle cell-specific background colors
                If Not String.IsNullOrEmpty(vCell.BackgroundColor) Then
                    SetSourceColor(vContext, vCell.BackgroundColor)
                    vContext.Rectangle(vX, vY, vWidth, vHeight)
                    vContext.Fill()
                End If
                
                ' Handle icon columns - allow custom icon rendering through delegate
                If vColumn.DataType = DataGridColumnType.eIcon Then
                    ' Raise event to allow custom icon rendering
                    Dim lArgs As New IconRenderEventArgs(vContext, vCell, vX, vY, vWidth, vHeight)
                    RaiseEvent RenderIcon(lArgs)
                    
                    ' If not handled by event, just leave blank (generic behavior)
                    If Not lArgs.Handled Then
                        ' Could optionally draw a default icon or placeholder here
                    End If
                    Return ' Don't draw text for icon columns
                End If
                
                ' Create layout for text - use CairoHelper
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = New FontDescription() with {
                    .Family = pFontFamily,
                    .Size = pFontSize * Pango.Scale.PangoScale
                }
                
                ' Apply cell formatting
                If vCell.Bold Then
                    lLayout.FontDescription.Weight = Weight.Bold
                End If
                If vCell.Italic Then
                    lLayout.FontDescription.Style = Pango.Style.Italic
                End If
                
                ' Format cell value based on column type
                Dim lText As String = ""
                If vCell.Value IsNot Nothing Then
                    Select Case vColumn.DataType
                        Case DataGridColumnType.eNumber
                            lText = If(TypeOf vCell.Value Is Integer OrElse TypeOf vCell.Value Is Double,
                                     vCell.Value.ToString(),
                                     vCell.Value.ToString())
                        Case DataGridColumnType.eBoolean
                            ' Safe boolean conversion with error handling
                            Dim lBoolValue As Boolean = False
                            If TypeOf vCell.Value Is Boolean Then
                                lBoolValue = CBool(vCell.Value)
                            ElseIf TypeOf vCell.Value Is String Then
                                Dim lStringValue As String = vCell.Value.ToString().Trim().ToLower()
                                Select Case lStringValue
                                    Case "1", "true", "yes", "y"
                                        lBoolValue = True
                                    Case Else
                                        ' Try to parse, default to False if it fails
                                        Boolean.TryParse(lStringValue, lBoolValue)
                                End Select
                            Else
                                ' Try to convert, default to False if it fails
                                Try
                                    lBoolValue = CBool(vCell.Value)
                                Catch
                                    lBoolValue = False
                                End Try
                            End If
                            lText = If(lBoolValue, "✓", "")
                        Case Else
                            lText = vCell.Value.ToString()
                    End Select
                End If
                
                ' Set text and ellipsize if needed
                lLayout.SetText(lText)
                If vColumn.Ellipsize Then
                    lLayout.Width = CInt((vWidth - CELL_PADDING * 2) * Pango.Scale.PangoScale)
                    lLayout.Ellipsize = EllipsizeMode.End
                End If
                
                ' Determine alignment
                Dim lAlignment As Double = CELL_PADDING
                Select Case vColumn.Alignment
                    Case ColumnAlignment.eCenter
                        lLayout.Alignment = Pango.Alignment.Center
                        lAlignment = 0
                    Case ColumnAlignment.eRight
                        lLayout.Alignment = Pango.Alignment.Right
                        lAlignment = -CELL_PADDING
                End Select
                
                ' Use cell's ForegroundColor if specified, otherwise use the passed default
                Dim lTextColor As String = If(Not String.IsNullOrEmpty(vCell.ForegroundColor), 
                                              vCell.ForegroundColor, 
                                              vForegroundColor)
                
                ' Draw text with the determined color
                SetSourceColor(vContext, lTextColor)
                vContext.MoveTo(vX + lAlignment, vY + (vHeight - GetTextHeight(lLayout)) / 2)
                Pango.CairoHelper.ShowLayout(vContext, lLayout)
                
                lLayout.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.DrawCell error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws an error icon (red circle with X)
        ''' </summary>
        Private Sub DrawErrorIcon(vContext As Cairo.Context, vX As Double, vY As Double, 
                                 vWidth As Double, vHeight As Double)
            Try
                Dim lCenterX As Double = vX + vWidth / 2
                Dim lCenterY As Double = vY + vHeight / 2
                Dim lRadius As Double = Math.Min(vWidth, vHeight) / 3
                
                ' Draw red circle
                vContext.SetSourceRGB(0.8, 0.1, 0.1)  ' Dark red
                vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                vContext.Fill()
                
                ' Draw white X
                vContext.SetSourceRGB(1, 1, 1)  ' White
                vContext.LineWidth = 2
                vContext.MoveTo(lCenterX - lRadius * 0.5, lCenterY - lRadius * 0.5)
                vContext.LineTo(lCenterX + lRadius * 0.5, lCenterY + lRadius * 0.5)
                vContext.MoveTo(lCenterX + lRadius * 0.5, lCenterY - lRadius * 0.5)
                vContext.LineTo(lCenterX - lRadius * 0.5, lCenterY + lRadius * 0.5)
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawErrorIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a warning icon (orange triangle with !)
        ''' </summary>
        Private Sub DrawWarningIcon(vContext As Cairo.Context, vX As Double, vY As Double, 
                                   vWidth As Double, vHeight As Double)
            Try
                Dim lCenterX As Double = vX + vWidth / 2
                Dim lCenterY As Double = vY + vHeight / 2
                Dim lSize As Double = Math.Min(vWidth, vHeight) * 0.7
                
                ' Draw orange triangle
                vContext.SetSourceRGB(1, 0.6, 0)  ' Orange
                vContext.MoveTo(lCenterX, lCenterY - lSize / 2)
                vContext.LineTo(lCenterX - lSize / 2, lCenterY + lSize / 2)
                vContext.LineTo(lCenterX + lSize / 2, lCenterY + lSize / 2)
                vContext.ClosePath()
                vContext.Fill()
                
                ' Draw exclamation mark
                vContext.SetSourceRGB(1, 1, 1)  ' White
                vContext.LineWidth = 2
                vContext.MoveTo(lCenterX, lCenterY - lSize * 0.2)
                vContext.LineTo(lCenterX, lCenterY + lSize * 0.1)
                vContext.Stroke()
                
                ' Draw dot
                vContext.Arc(lCenterX, lCenterY + lSize * 0.25, 1.5, 0, Math.PI * 2)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawWarningIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws an info icon (blue circle with i)
        ''' </summary>
        Private Sub DrawInfoIcon(vContext As Cairo.Context, vX As Double, vY As Double, 
                                vWidth As Double, vHeight As Double)
            Try
                Dim lCenterX As Double = vX + vWidth / 2
                Dim lCenterY As Double = vY + vHeight / 2
                Dim lRadius As Double = Math.Min(vWidth, vHeight) / 3
                
                ' Draw blue circle
                vContext.SetSourceRGB(0.2, 0.4, 0.8)  ' Blue
                vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                vContext.Fill()
                
                ' Draw white "i"
                vContext.SetSourceRGB(1, 1, 1)  ' White
                ' Dot
                vContext.Arc(lCenterX, lCenterY - lRadius * 0.4, 1.5, 0, Math.PI * 2)
                vContext.Fill()
                ' Line
                vContext.LineWidth = 2
                vContext.MoveTo(lCenterX, lCenterY - lRadius * 0.1)
                vContext.LineTo(lCenterX, lCenterY + lRadius * 0.5)
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawInfoIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a success icon (green circle with checkmark)
        ''' </summary>
        Private Sub DrawSuccessIcon(vContext As Cairo.Context, vX As Double, vY As Double, 
                                   vWidth As Double, vHeight As Double)
            Try
                Dim lCenterX As Double = vX + vWidth / 2
                Dim lCenterY As Double = vY + vHeight / 2
                Dim lRadius As Double = Math.Min(vWidth, vHeight) / 3
                
                ' Draw green circle
                vContext.SetSourceRGB(0.2, 0.7, 0.2)  ' Green
                vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                vContext.Fill()
                
                ' Draw white checkmark
                vContext.SetSourceRGB(1, 1, 1)  ' White
                vContext.LineWidth = 2
                vContext.MoveTo(lCenterX - lRadius * 0.5, lCenterY)
                vContext.LineTo(lCenterX - lRadius * 0.1, lCenterY + lRadius * 0.4)
                vContext.LineTo(lCenterX + lRadius * 0.5, lCenterY - lRadius * 0.4)
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawSuccessIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws grid lines between rows and columns
        ''' </summary>
        Private Sub DrawGridLines(vContext As Cairo.Context, vWidth As Integer, vHeight As Integer,
                                 vFirstRow As Integer, vRowCount As Integer)
            Try
                SetSourceColor(vContext, pGridLineColor)
                vContext.LineWidth = GRID_LINE_WIDTH
                
                ' Draw horizontal lines between rows
                Dim lY As Double = -(pScrollY Mod pRowHeight) + pRowHeight - 0.5
                for i As Integer = 0 To vRowCount
                    If lY >= 0 AndAlso lY <= vHeight Then
                        vContext.MoveTo(0, lY)
                        vContext.LineTo(vWidth, lY)
                        vContext.Stroke()
                    End If
                    lY += pRowHeight
                Next
                
                ' Draw vertical lines between columns
                Dim lX As Double = -pHorizontalOffset
                for each lColumn in pColumns
                    If Not lColumn.Visible Then Continue for
                    lX += lColumn.Width
                    
                    If lX >= 0 AndAlso lX <= vWidth Then
                        vContext.MoveTo(lX - 0.5, 0)
                        vContext.LineTo(lX - 0.5, vHeight)
                        vContext.Stroke()
                    End If
                    
                    If lX > vWidth Then Exit for
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.DrawGridLines error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Sets the Cairo context source color from a hex string
        ''' </summary>
        Private Sub SetSourceColor(vContext As Cairo.Context, vColorHex As String)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vColorHex) Then
                    vContext.SetSourceRGBA(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                Else
                    ' Fallback to black
                    vContext.SetSourceRGB(0, 0, 0)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SetSourceColor error: {ex.Message}")
                vContext.SetSourceRGB(0, 0, 0)
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the height of text in a Pango layout
        ''' </summary>
        Private Function GetTextHeight(vLayout As Pango.Layout) As Integer
            Try
                Dim lWidth, lHeight As Integer
                vLayout.GetPixelSize(lWidth, lHeight)
                Return lHeight
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.GetTextHeight error: {ex.Message}")
                Return pRowHeight - 4
            End Try
        End Function

        ' ===== Convenience Methods for Common Scenarios =====
        
        ''' <summary>
        ''' Sets a simple row style mapping for background colors
        ''' </summary>
        Public Sub SetRowStyleColors(vStyleColors As Dictionary(Of RowStyle, String))
            GetRowBackgroundColor = Function(rowIndex, row, isSelected, isHover)
                                        ' Priority: Selection > Hover > Style > Alternate > Default
                                        If isSelected Then Return pSelectionColor
                                        If isHover Then Return pHoverColor
                                        
                                        ' Check if row has a style with a mapped color
                                        If row.Style <> RowStyle.eNormal AndAlso 
                                           vStyleColors IsNot Nothing AndAlso 
                                           vStyleColors.ContainsKey(row.Style) Then
                                            Return vStyleColors(row.Style)
                                        End If
                                        
                                        ' Fall back to alternate row colors
                                        If pAlternateRowColors AndAlso (rowIndex Mod 2 = 1) Then
                                            Return pAlternateRowColor
                                        End If
                                        
                                        Return pBackgroundColor
                                    End Function
        End Sub
        
        ''' <summary>
        ''' Clears all color override delegates
        ''' </summary>
        Public Sub ClearColorOverrides()
            GetRowBackgroundColor = Nothing
            GetCellForegroundColor = Nothing
        End Sub
        
    End Class
    
End Namespace