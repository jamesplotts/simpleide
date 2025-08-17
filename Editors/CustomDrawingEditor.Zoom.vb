' Editors/CustomDrawingEditor.Zoom.vb - Zoom functionality implementation for IEditor interface
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

' CustomDrawingEditor.Zoom.vb
' Created: 2025-08-11 06:06:08

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Zoom Constants =====
        Private Const ZOOM_FACTOR As Double = 1.2
        Private Const MIN_FONT_SIZE As Integer = 6
        Private Const MAX_FONT_SIZE As Integer = 72
        Private Const DEFAULT_FONT_SIZE As Integer = 11
        
        ' ===== Zoom State =====
        Private pCurrentFontSize As Integer = DEFAULT_FONT_SIZE
        Private pBaseFontSize As Integer = DEFAULT_FONT_SIZE
        
        ' ===== IEditor Zoom Method Implementations =====
        
        ''' <summary>
        ''' Increase the zoom level (font size) of the editor
        ''' </summary>
        Public Sub ZoomIn() Implements IEditor.ZoomIn
            Try
                ' Calculate new font size
                Dim lNewSize As Integer = CInt(Math.Round(pCurrentFontSize * ZOOM_FACTOR))
                
                ' Apply maximum limit
                If lNewSize > MAX_FONT_SIZE Then
                    lNewSize = MAX_FONT_SIZE
                End If
                
                ' Only update if size changed
                If lNewSize <> pCurrentFontSize Then
                    ApplyZoomLevel(lNewSize)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ZoomIn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Decrease the zoom level (font size) of the editor
        ''' </summary>
        Public Sub ZoomOut() Implements IEditor.ZoomOut
            Try
                ' Calculate new font size
                Dim lNewSize As Integer = CInt(Math.Round(pCurrentFontSize / ZOOM_FACTOR))
                
                ' Apply minimum limit
                If lNewSize < MIN_FONT_SIZE Then
                    lNewSize = MIN_FONT_SIZE
                End If
                
                ' Only update if size changed
                If lNewSize <> pCurrentFontSize Then
                    ApplyZoomLevel(lNewSize)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ZoomOut error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Reset zoom to default level
        ''' </summary>
        Public Sub ZoomReset() Implements IEditor.ZoomReset
            Try
                ' Reset to base font size
                If pCurrentFontSize <> pBaseFontSize Then
                    ApplyZoomLevel(pBaseFontSize)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ZoomReset error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Internal Zoom Helper Methods =====
        
        ''' <summary>
        ''' Apply a specific zoom level (font size) to the editor
        ''' </summary>
        Private Sub ApplyZoomLevel(vFontSize As Integer)
            Try
                ' Validate font size
                If vFontSize < MIN_FONT_SIZE OrElse vFontSize > MAX_FONT_SIZE Then
                    Console.WriteLine($"ApplyZoomLevel: Invalid font size {vFontSize}")
                    Return
                End If
                
                ' Update current font size
                pCurrentFontSize = vFontSize
                
                ' Update font description if exists
                If pFontDescription IsNot Nothing Then
                    ' Get current font family
                    Dim lFontFamily As String = pFontDescription.Family
                    If String.IsNullOrEmpty(lFontFamily) Then
                        lFontFamily = "Monospace"
                    End If
                    
                    ' Create new font description with updated size
                    Dim lNewFontDesc As String = $"{lFontFamily} {vFontSize}"
                    
                    ' Apply the new font
                    ApplyFont(lNewFontDesc)
                    
                    ' Log the zoom change
                    Console.WriteLine($"Zoom applied: {vFontSize}pt (from {pBaseFontSize}pt base)")
                Else
                    ' No font description yet, just store the size
                    Console.WriteLine($"Zoom level stored: {vFontSize}pt (font not yet initialized)")
                End If
                
                ' Update line number width if showing
                If pShowLineNumbers Then
                    UpdateLineNumberWidth()
                End If
                
                ' Force full redraw
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
                ' Update scrollbars to reflect new content size
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyZoomLevel error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initialize zoom settings from current font
        ''' </summary>
        Private Sub InitializeZoomSettings()
            Try
                If pFontDescription IsNot Nothing Then
                    ' Get size from font description (in points)
                    Dim lSizeInPango As Integer = pFontDescription.Size
                    
                    ' Convert from Pango units to points if needed
                    If pFontDescription.SizeIsAbsolute Then
                        pBaseFontSize = lSizeInPango
                    Else
                        ' Pango uses 1024 units per point
                        pBaseFontSize = CInt(Math.Round(lSizeInPango / Pango.Scale.PangoScale))
                    End If
                    
                    ' Ensure within valid range
                    If pBaseFontSize < MIN_FONT_SIZE Then
                        pBaseFontSize = MIN_FONT_SIZE
                    ElseIf pBaseFontSize > MAX_FONT_SIZE Then
                        pBaseFontSize = MAX_FONT_SIZE
                    End If
                    
                    pCurrentFontSize = pBaseFontSize
                    
                    Console.WriteLine($"Zoom initialized: Base font size = {pBaseFontSize}pt")
                Else
                    ' Use defaults if no font description
                    pBaseFontSize = DEFAULT_FONT_SIZE
                    pCurrentFontSize = DEFAULT_FONT_SIZE
                    Console.WriteLine("Zoom initialized with defaults: 11pt")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InitializeZoomSettings error: {ex.Message}")
                ' Fall back to defaults on error
                pBaseFontSize = DEFAULT_FONT_SIZE
                pCurrentFontSize = DEFAULT_FONT_SIZE
            End Try
        End Sub
        
        ''' <summary>
        ''' Get current zoom percentage relative to base
        ''' </summary>
        Private Function GetZoomPercentage() As Integer
            Try
                If pBaseFontSize > 0 Then
                    Return CInt(Math.Round((pCurrentFontSize / CDbl(pBaseFontSize)) * 100))
                Else
                    Return 100
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetZoomPercentage error: {ex.Message}")
                Return 100
            End Try
        End Function
        
        ''' <summary>
        ''' Set zoom to a specific percentage
        ''' </summary>
        Private Sub SetZoomPercentage(vPercentage As Integer)
            Try
                ' Validate percentage
                If vPercentage < 10 OrElse vPercentage > 500 Then
                    Console.WriteLine($"SetZoomPercentage: Invalid percentage {vPercentage}%")
                    Return
                End If
                
                ' Calculate new font size
                Dim lNewSize As Integer = CInt(Math.Round(pBaseFontSize * (vPercentage / 100.0)))
                
                ' Apply limits
                If lNewSize < MIN_FONT_SIZE Then
                    lNewSize = MIN_FONT_SIZE
                ElseIf lNewSize > MAX_FONT_SIZE Then
                    lNewSize = MAX_FONT_SIZE
                End If
                
                ' Apply the zoom
                ApplyZoomLevel(lNewSize)
                
            Catch ex As Exception
                Console.WriteLine($"SetZoomPercentage error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
