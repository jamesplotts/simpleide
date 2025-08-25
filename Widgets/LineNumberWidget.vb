' Widgets/LineNumberWidget.vb - Dedicated widget for line number display
Imports System
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

' LineNumberWidget.vb
' Created: 2025-08-24 00:08:22

Namespace Widgets
    
    ''' <summary>
    ''' Dedicated widget for rendering line numbers in the code editor
    ''' </summary>
    Public Class LineNumberWidget
        Inherits DrawingArea
        
        ' ===== Private Fields =====
        Private pEditor As Editors.CustomDrawingEditor
        Private pFontDescription As FontDescription
        Private pLineHeight As Integer
        Private pCharWidth As Integer
        Private pTopPadding As Integer = 4
        Private pRightPadding As Integer = 8
        Private pWidth As Integer = 50
        
        ' Theme colors
        Private pBackgroundColor As String = "#1E1E1E"
        Private pForegroundColor As String = "#858585"  
        Private pCurrentLineColor As String = "#C6C6C6"
        Private pSeparatorColor As String = "#3C3C3C"
        
        ' ===== Constructor =====
        ''' <summary>
        ''' Creates a new LineNumberWidget instance
        ''' </summary>
        ''' <param name="vEditor">The parent editor to track</param>
        Public Sub New(vEditor As Editors.CustomDrawingEditor)
            Try
                pEditor = vEditor
                
                ' Set initial size
                WidthRequest = pWidth
                
                ' Configure widget
                CanFocus = False
                AddEvents(CInt(EventMask.ButtonPressMask Or 
                              EventMask.ButtonReleaseMask Or 
                              EventMask.PointerMotionMask Or 
                              EventMask.ScrollMask))
                
                ' Connect event handlers
                AddHandler Me.Drawn, AddressOf OnDraw
                AddHandler Me.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler Me.ButtonReleaseEvent, AddressOf OnButtonRelease
                AddHandler Me.MotionNotifyEvent, AddressOf OnMotionNotify
                AddHandler Me.ScrollEvent, AddressOf OnScroll
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Updates the font used for line numbers
        ''' </summary>
        ''' <param name="vFontDescription">Font description to use</param>
        ''' <param name="vLineHeight">Height of each line in pixels</param>
        ''' <param name="vCharWidth">Width of each character in pixels</param>
        Public Sub UpdateFont(vFontDescription As FontDescription, vLineHeight As Integer, vCharWidth As Integer)
            Try
                pFontDescription = vFontDescription
                pLineHeight = vLineHeight
                pCharWidth = vCharWidth
                
                ' Update width based on line count
                UpdateWidth()
                
                ' Redraw
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.UpdateFont error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the width of the widget based on line count
        ''' </summary>
        Public Sub UpdateWidth()
            Try
                If pEditor Is Nothing Then Return
                
                Dim lLineCount As Integer = pEditor.LineCount
                Dim lMaxDigits As Integer = Math.Max(3, lLineCount.ToString().Length)
                Dim lNewWidth As Integer = (lMaxDigits * pCharWidth) + pRightPadding + 8
                
                If lNewWidth <> pWidth Then
                    pWidth = lNewWidth
                    WidthRequest = pWidth
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.UpdateWidth error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates theme colors for the widget
        ''' </summary>
        ''' <param name="vTheme">Editor theme to apply</param>
        Public Sub UpdateTheme(vTheme As EditorTheme)
            Try
                If vTheme Is Nothing Then Return
                
                ' Get colors from theme
                pBackgroundColor = vTheme.GetColor(EditorTheme.Tags.eLineNumberBackgroundColor)
                pForegroundColor = vTheme.GetColor(EditorTheme.Tags.eLineNumberColor)
                pCurrentLineColor = vTheme.GetColor(EditorTheme.Tags.eCurrentLineNumberColor)
                
                ' Redraw with new colors
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.UpdateTheme error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handles the draw event to render line numbers
        ''' </summary>
        Private Function OnDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                
                ' Draw background
                DrawBackground(lContext)
                
                ' Draw line numbers
                DrawLineNumbers(lContext)
                
                ' Draw separator
                DrawSeparator(lContext)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.OnDraw error: {ex.Message}")
                Return True
            End Try
        End Function
        
        ''' <summary>
        ''' Draws the background of the line number area
        ''' </summary>
        Private Sub DrawBackground(vContext As Cairo.Context)
            Try
                ' Parse and set background color
                Dim lColor As RGBA = New RGBA()
                If lColor.Parse(pBackgroundColor) Then
                    vContext.SetSourceRgba(lColor.Red, lColor.Green, lColor.Blue, 1.0)
                Else
                    vContext.SetSourceRgba(0.12, 0.12, 0.12, 1.0) ' Fallback dark
                End If
                
                ' Fill background
                vContext.Rectangle(0, 0, AllocatedWidth, AllocatedHeight)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.DrawBackground error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the line numbers
        ''' </summary>
        Private Sub DrawLineNumbers(vContext As Cairo.Context)
            Try
                If pEditor Is Nothing OrElse pLineHeight <= 0 Then Return
                
                ' Get editor state
                Dim lLineCount As Integer = pEditor.LineCount
                Dim lFirstVisibleLine As Integer = pEditor.FirstVisibleLine
                Dim lCurrentLine As Integer = pEditor.CurrentLine
                
                ' Calculate visible range
                Dim lVisibleLines As Integer = (AllocatedHeight \ pLineHeight) + 2
                Dim lLastLine As Integer = Math.Min(lLineCount - 1, lFirstVisibleLine + lVisibleLines)
                
                ' Create layout for text
                Using lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                    If pFontDescription IsNot Nothing Then
                        lLayout.FontDescription = pFontDescription
                    End If
                    lLayout.Alignment = Pango.Alignment.Right
                    lLayout.Width = Pango.Units.FromPixels(pWidth - pRightPadding)
                    
                    ' Set default text color
                    Dim lFgColor As New RGBA()
                    If lFgColor.Parse(pForegroundColor) Then
                        vContext.SetSourceRgba(lFgColor.Red, lFgColor.Green, lFgColor.Blue, 1.0)
                    Else
                        vContext.SetSourceRgba(0.52, 0.52, 0.52, 1.0) ' Fallback gray
                    End If
                    
                    ' Draw each visible line number
                    for i As Integer = lFirstVisibleLine To lLastLine
                        ' Calculate Y position (account for scroll)
                        Dim lY As Integer = ((i - lFirstVisibleLine) * pLineHeight) + pTopPadding - 4
                        
                        ' Set text (1-based line numbers)
                        lLayout.SetText((i + 1).ToString())
                        
                        ' Highlight current line number if needed
                        If i = lCurrentLine Then
                            Dim lCurrentColor As New RGBA()
                            If lCurrentColor.Parse(pCurrentLineColor) Then
                                vContext.SetSourceRgba(lCurrentColor.Red, lCurrentColor.Green, lCurrentColor.Blue, 1.0)
                            Else
                                vContext.SetSourceRgba(0.78, 0.78, 0.78, 1.0) ' Fallback light gray
                            End If
                            
                            vContext.MoveTo(0, lY)
                            Pango.CairoHelper.ShowLayout(vContext, lLayout)
                            
                            ' Restore default color
                            vContext.SetSourceRgba(lFgColor.Red, lFgColor.Green, lFgColor.Blue, 1.0)
                        Else
                            vContext.MoveTo(0, lY)
                            Pango.CairoHelper.ShowLayout(vContext, lLayout)
                        End If
                    Next
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.DrawLineNumbers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the separator line between line numbers and editor
        ''' </summary>
        Private Sub DrawSeparator(vContext As Cairo.Context)
            Try
                ' Parse separator color
                Dim lColor As New RGBA()
                If lColor.Parse(pSeparatorColor) Then
                    vContext.SetSourceRgba(lColor.Red, lColor.Green, lColor.Blue, 1.0)
                Else
                    vContext.SetSourceRgba(0.3, 0.3, 0.3, 1.0) ' Fallback dark gray
                End If
                
                ' Draw vertical line
                vContext.LineWidth = 1.0
                vContext.MoveTo(AllocatedWidth - 0.5, 0)
                vContext.LineTo(AllocatedWidth - 0.5, AllocatedHeight)
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.DrawSeparator error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Mouse Event Handlers =====
        
        ''' <summary>
        ''' Handles mouse button press events
        ''' </summary>
        Private Function OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                If pEditor Is Nothing OrElse pLineHeight <= 0 Then Return False
                
                ' Calculate which line was clicked
                Dim lY As Integer = CInt(vArgs.Event.Y) - pTopPadding
                Dim lClickedLine As Integer = (lY \ pLineHeight) + pEditor.FirstVisibleLine
                
                ' Validate line
                If lClickedLine >= 0 AndAlso lClickedLine < pEditor.LineCount Then
                    If vArgs.Event.Button = 1 Then
                        ' Left click - select line
                        pEditor.SelectLine(lClickedLine)
                        pEditor.StartLineNumberDrag(lClickedLine)
                    ElseIf vArgs.Event.Button = 3 Then
                        ' Right click - show context menu
                        pEditor.ShowLineNumberContextMenu(CInt(vArgs.Event.X), CInt(vArgs.Event.Y))
                    End If
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.OnButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse button release events
        ''' </summary>
        Private Function OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                If pEditor IsNot Nothing Then
                    pEditor.EndLineNumberDrag()
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.OnButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse motion events
        ''' </summary>
        Private Function OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                If pEditor Is Nothing OrElse pLineHeight <= 0 Then Return False
                
                ' Calculate which line the mouse is over
                Dim lY As Integer = CInt(vArgs.Event.Y) - pTopPadding
                Dim lHoverLine As Integer = (lY \ pLineHeight) + pEditor.FirstVisibleLine
                
                ' Update drag selection if dragging
                If pEditor.IsLineNumberDragging AndAlso lHoverLine >= 0 AndAlso lHoverLine < pEditor.LineCount Then
                    pEditor.UpdateLineNumberDrag(lHoverLine)
                End If
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.OnMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles scroll events for line number area
        ''' </summary>
        Private Function OnScroll(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                If pEditor Is Nothing Then Return False
                
                ' Forward scroll event to editor
                pEditor.HandleScroll(vArgs)
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.OnScroll error: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Class
    
End Namespace
