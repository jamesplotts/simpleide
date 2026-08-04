' Widgets/LineNumberWidget.vb - Dedicated widget for line number display
Imports System
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Syntax

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
        Private pTopPadding As Integer = -10
        Private pRightPadding As Integer = 24 ' Increased to accommodate fold icons
        Private pWidth As Integer = 60
        ' Kept clear of x=0 because GTK's Paned divider (between the explorer panels and the
        ' editor) has its own drag-hit region that extends a few pixels into this widget's left
        ' edge - too close and the two hit regions fight over the same clicks
        Private ReadOnly pFoldIconLeft As Integer = 8 ' X offset of the fold icon from the far left of the gutter
        Private ReadOnly pFoldIconAreaWidth As Integer = 20 ' Click hit-test width for the fold icon column
        Private pLastAscent As Integer = 0 ' Ascent used by the last DrawLineNumbers call, reused for hit-testing
        
        ' Theme colors
        Private pBackgroundColor As String = "#1E1E1E"
        Private pForegroundColor As String = "#858585"  
        Private pCurrentLineColor As String = "#C6C6C6"
        Private pSeparatorColor As String = "#3C3C3C"
        
        ''' <summary>
        ''' Creates a new LineNumberWidget instance
        ''' </summary>
        ''' <param name="vEditor">The parent editor to track</param>
        Public Sub New(vEditor As Editors.CustomDrawingEditor)
            Try
                pEditor = vEditor

                ' Set initial size
                WidthRequest = pWidth

                ' Configure widget - ensure it can receive events
                CanFocus = True  ' Changed from False to True to ensure events are received
                FocusOnClick = True  ' Add this to ensure the widget gets focus when clicked

                ' Set event mask - ButtonPressMask covers single, double, and triple clicks
                Events = EventMask.ButtonPressMask Or
                        EventMask.ButtonReleaseMask Or
                        EventMask.PointerMotionMask Or
                        EventMask.ScrollMask Or
                        EventMask.ExposureMask  ' Add this for proper drawing

                ' Connect event handlers
                AddHandler Me.Drawn, AddressOf OnDraw
                AddHandler Me.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler Me.ButtonReleaseEvent, AddressOf OnButtonRelease
                AddHandler Me.MotionNotifyEvent, AddressOf OnMotionNotify
                AddHandler Me.ScrollEvent, AddressOf OnScroll

                Console.WriteLine("LineNumberWidget: Initialized with double-click support (CanFocus=True)")

            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Updates the font and metrics from the editor
        ''' </summary>
        ''' <param name="vFontDescription">New font description</param>
        ''' <param name="vLineHeight">Updated line height</param>
        ''' <param name="vCharWidth">Updated character width</param>
        Public Sub UpdateFont(vFontDescription As Pango.FontDescription, vLineHeight As Integer, vCharWidth As Integer)
            Try
                ' Update font description
                pFontDescription = vFontDescription
                
                ' Update metrics
                pLineHeight = vLineHeight
                pCharWidth = vCharWidth
                
                ' Recalculate width based on new character width
                If pEditor IsNot Nothing Then
                    Dim lMaxLineNumber As Integer = pEditor.LineCount
                    Dim lDigits As Integer = Math.Max(3, lMaxLineNumber.ToString().Length)
                    Dim lNewWidth As Integer = (lDigits * pCharWidth) + pRightPadding + 20 ' Ensure ample space
                    
                    ' Update width if changed
                    If lNewWidth <> pWidth Then
                        pWidth = lNewWidth
                        WidthRequest = pWidth
                        Console.WriteLine($"LineNumberWidget.UpdateFont: Width updated to {pWidth}px")
                    End If
                End If
                
                ' Force redraw
                QueueDraw()
                
                Console.WriteLine($"LineNumberWidget.UpdateFont: Updated with LineHeight={vLineHeight}, CharWidth={vCharWidth}")
                
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
                Dim lNewWidth As Integer = (lMaxDigits * pCharWidth) + pRightPadding + 20
                
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
        ''' Draws the line numbers with proper font alignment
        ''' </summary>
        Private Sub DrawLineNumbers(vContext As Cairo.Context)
            Try
                If pEditor Is Nothing OrElse pLineHeight <= 0 Then Return
                
                ' Get editor state
                Dim lVisualLineCount As Integer = pEditor.GetVisualLineCount()
                Dim lFirstVisibleLine As Integer = Math.Max(0, pEditor.FirstVisibleLine)
                Dim lCurrentLine As Integer = pEditor.CurrentLine
                Dim lCurrentSourceLine As Integer = pEditor.VisualToSourceLine(lCurrentLine)
                
                ' Calculate visible range - add extra lines to ensure we draw everything visible
                Dim lVisibleLines As Integer = (AllocatedHeight \ pLineHeight) + 3
                Dim lLastLine As Integer = Math.Min(lVisualLineCount - 1, lFirstVisibleLine + lVisibleLines)
                
                ' Create layout for text
                Using lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                    If pFontDescription IsNot Nothing Then
                        lLayout.FontDescription = pFontDescription
                    End If
                    ' Remove Pango alignment/width to rely on manual positioning
                    ' lLayout.Alignment = Pango.Alignment.Right
                    ' lLayout.Width = Pango.Units.FromPixels(pWidth - pRightPadding)
                    
                    ' Resolve default text color once - re-applied explicitly per row below
                    ' (not left as leftover Cairo state) so it's never clobbered by the
                    ' current-line row's color switch
                    Dim lFgRed As Double = 0.52
                    Dim lFgGreen As Double = 0.52
                    Dim lFgBlue As Double = 0.52
                    Dim lFgColor As New RGBA()
                    If lFgColor.Parse(pForegroundColor) Then
                        lFgRed = lFgColor.Red
                        lFgGreen = lFgColor.Green
                        lFgBlue = lFgColor.Blue
                    End If
                    vContext.SetSourceRgba(lFgRed, lFgGreen, lFgBlue, 1.0)
                    
                    ' Get font metrics for proper baseline alignment
                    Dim lAscent As Integer = 0
                    Try
                        ' Measure a sample character to get the actual text height
                        lLayout.SetText("8")  ' Use a full-height digit
                        Dim lInkRect, lLogicalRect As Pango.Rectangle
                        lLayout.GetPixelExtents(lInkRect, lLogicalRect)
                        
                        ' The ascent is roughly where we want to position the baseline
                        ' Use logical height as approximation if we can't get real metrics
                        lAscent = CInt(lLogicalRect.Height * 0.8)  ' Slightly above center for better alignment
                    Catch
                        ' Fallback to approximate ascent
                        lAscent = CInt(pLineHeight * 0.75)
                    End Try

                    ' Cache for hit-testing (OnButtonPress/OnMotionNotify need to know where the
                    ' glyph actually renders within its row, not just the row's nominal top)
                    pLastAscent = lAscent

                    ' Draw each visible line number (including partially visible ones)
                    For lVisualIndex As Integer = lFirstVisibleLine To lLastLine
                        ' Calculate Y position to match editor text
                        ' The editor draws at: (line - firstLine) * lineHeight + topPadding
                        Dim lLineIndex As Integer = lVisualIndex - lFirstVisibleLine
                        Dim lLineTop As Integer = lLineIndex * pLineHeight + pTopPadding
                        
                        ' Add ascent to get baseline position
                        Dim lY As Integer = lLineTop + lAscent
                        
                        ' Skip if completely outside visible area
                        If lY < -pLineHeight OrElse lY > AllocatedHeight + pLineHeight Then
                            Continue For
                        End If
                        
                        ' Map visual line to source line
                        Dim lSourceLine As Integer = pEditor.VisualToSourceLine(lVisualIndex)

                        ' Fallback: If we get 0 for a non-zero visual line, and the map seems broken, use visual line
                        ' This handles the case where pVisualLineMap might be truncated or invalid
                        If lSourceLine = 0 AndAlso lVisualIndex > 0 AndAlso pEditor.GetVisualLineCount() <= 1 Then
                             lSourceLine = lVisualIndex
                        End If
                        
                        ' Set text (1-based source line numbers)
                        lLayout.SetText((lSourceLine + 1).ToString())
                        
                        ' Measure the glyph once - both branches below draw identical text/font,
                        ' and the fold icon centering after them needs this same measurement
                        Dim lTextWidth As Integer
                        Dim lTextHeight As Integer
                        lLayout.GetPixelSize(lTextWidth, lTextHeight)
                        Dim lX As Integer = pWidth - pRightPadding - lTextWidth - 5 ' Extra 5px buffer

                        ' Highlight current line number if needed - color is set explicitly for
                        ' every row (not left as sticky Cairo state from the previous row), so
                        ' rows after the current line can't inherit the wrong color
                        If lSourceLine = lCurrentSourceLine Then
                            Dim lCurrentColor As New RGBA()
                            If lCurrentColor.Parse(pCurrentLineColor) Then
                                vContext.SetSourceRgba(lCurrentColor.Red, lCurrentColor.Green, lCurrentColor.Blue, 1.0)
                            Else
                                vContext.SetSourceRgba(0.78, 0.78, 0.78, 1.0) ' Fallback light gray
                            End If
                        Else
                            vContext.SetSourceRgba(lFgRed, lFgGreen, lFgBlue, 1.0)
                        End If

                        vContext.MoveTo(lX, lY)
                        Pango.CairoHelper.ShowLayout(vContext, lLayout)

                        ' Draw fold icon if needed, at the far left of the gutter
                        ' lY marks the TOP of the glyph box (Pango draws top-down from the
                        ' MoveTo point), not its vertical center, so the icon must be offset
                        ' down by half the measured glyph height to land centered on the row
                        Dim lFoldNode As SyntaxNode = pEditor.GetFoldableNodeAtLine(lSourceLine)
                        If lFoldNode IsNot Nothing Then
                            DrawFoldIcon(vContext, pFoldIconLeft, lY + (lTextHeight \ 2) - 4, lFoldNode.IsExpanded)
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
        
        ' Replace: SimpleIDE.Widgets.LineNumberWidget.OnButtonPress
        ''' <summary>
        ''' Handles mouse button press events
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Button press event arguments</param>
        Private Function OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                Console.WriteLine($"LineNumberWidget.OnButtonPress: Button={vArgs.Event.Button}, Type={vArgs.Event.Type}")
                
                If pEditor Is Nothing OrElse pLineHeight <= 0 Then 
                    Console.WriteLine("LineNumberWidget.OnButtonPress: Editor is Nothing or invalid line height")
                    Return False
                End If
                
                ' Calculate which line was clicked
                ' Subtract pLastAscent so the hit-test band for a row starts at the same Y its
                ' glyph actually renders at (lLineTop + lAscent), not the row's nominal empty top -
                ' otherwise clicking on the visible digit/icon (which renders low in its row) can
                ' resolve to the row below it
                Dim lY As Double = vArgs.Event.Y - pTopPadding - pLastAscent
                Dim lClickedVisualLine As Integer = CInt(Math.Floor(lY / pLineHeight)) + pEditor.FirstVisibleLine
                Dim lClickedSourceLine As Integer = pEditor.VisualToSourceLine(lClickedVisualLine)
                
                Console.WriteLine($"LineNumberWidget.OnButtonPress: Clicked visual line {lClickedVisualLine}, source line {lClickedSourceLine}")
                
                ' Check for fold toggle click (far left of widget)
                If vArgs.Event.X <= pFoldIconAreaWidth Then
                    Dim lNode As SyntaxNode = pEditor.GetFoldableNodeAtLine(lClickedSourceLine)
                    If lNode IsNot Nothing Then
                        pEditor.ToggleFold(lNode)
                        Return True
                    End If
                End If
                
                ' Validate line
                If lClickedSourceLine >= 0 AndAlso lClickedSourceLine < pEditor.LineCount Then
                    If vArgs.Event.Button = 1 Then
                        ' Check for multi-click events
                        If vArgs.Event.Type = EventType.ThreeButtonPress Then
                            Console.WriteLine($"LineNumberWidget.OnButtonPress: TRIPLE-CLICK detected on line {lClickedSourceLine}")
                            ' Triple-click - select entire line (GTK standard behavior)
                            pEditor.SelectLine(lClickedSourceLine)
                            ' Grab focus for the drawing area after selection
                            pEditor.GrabFocus()
                        ElseIf vArgs.Event.Type = EventType.TwoButtonPress Then
                            Console.WriteLine($"LineNumberWidget.OnButtonPress: DOUBLE-CLICK detected on line {lClickedSourceLine}")
                            ' Double-click - check if it's a method declaration and select entire method
                            HandleDoubleClick(lClickedSourceLine)
                            ' Grab focus for the drawing area after selection
                            pEditor.GrabFocus()
                        ElseIf vArgs.Event.Type = EventType.ButtonPress Then
                            Console.WriteLine($"LineNumberWidget.OnButtonPress: Single-click on line {lClickedSourceLine}")
                            ' Single click - select line (only if not double-click)
                            pEditor.SelectLine(lClickedSourceLine)
                            pEditor.StartLineNumberDrag(lClickedSourceLine)
                            ' Grab focus for the drawing area after selection
                            pEditor.GrabFocus()
                        End If
                    ElseIf vArgs.Event.Button = 3 Then
                        Console.WriteLine($"LineNumberWidget.OnButtonPress: Right-click on line {lClickedSourceLine}")
                        ' Right click - show context menu
                        pEditor.ShowLineNumberContextMenu(CInt(vArgs.Event.X), CInt(vArgs.Event.Y))
                        ' Also grab focus for context menu operations
                        pEditor.GrabFocus()
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
                ' pEditor.FirstVisibleLine and the resulting index are visual line numbers, so
                ' they must be mapped back to the actual source line to account for any lines
                ' currently hidden by a collapsed fold. Also subtract pLastAscent so the hit-test
                ' band matches where the glyph actually renders (see OnButtonPress).
                Dim lY As Double = vArgs.Event.Y - pTopPadding - pLastAscent
                Dim lHoverVisualLine As Integer = CInt(Math.Floor(lY / pLineHeight)) + pEditor.FirstVisibleLine
                Dim lHoverLine As Integer = pEditor.VisualToSourceLine(lHoverVisualLine)

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

        ''' <summary>
        ''' Handles double-click on a line number: selects the enclosing method, property,
        ''' or type declaration if the clicked line is inside one, otherwise just the line
        ''' </summary>
        ''' <param name="vLineIndex">The line that was double-clicked (0-based)</param>
        Private Sub HandleDoubleClick(vLineIndex As Integer)
            Try
                If pEditor Is Nothing Then Return

                If vLineIndex < 0 OrElse vLineIndex >= pEditor.LineCount Then
                    pEditor.SelectLine(vLineIndex)
                Else
                    pEditor.SelectContainingBlock(vLineIndex)
                End If

                pEditor.GrabFocus()

            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.HandleDoubleClick error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws a fold icon (plus or minus)
        ''' </summary>
        Private Sub DrawFoldIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vIsExpanded As Boolean)
            Try
                Dim lSize As Integer = 9

                ' Use the same foreground color as the rest of the gutter
                Dim lColor As New RGBA()
                If lColor.Parse(pForegroundColor) Then
                    vContext.SetSourceRgba(lColor.Red, lColor.Green, lColor.Blue, 1.0)
                Else
                    vContext.SetSourceRGB(0.5, 0.5, 0.5) ' Fallback gray
                End If
                vContext.LineWidth = 1.0
                vContext.Rectangle(vX, vY, lSize, lSize)
                vContext.Stroke()
                
                ' Draw horizontal line (minus)
                vContext.MoveTo(vX + 2, vY + (lSize \ 2))
                vContext.LineTo(vX + lSize - 2, vY + (lSize \ 2))
                vContext.Stroke()
                
                ' Draw vertical line (plus) if collapsed
                If Not vIsExpanded Then
                    vContext.MoveTo(vX + (lSize \ 2), vY + 2)
                    vContext.LineTo(vX + (lSize \ 2), vY + lSize - 2)
                    vContext.Stroke()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DrawFoldIcon error: {ex.Message}")
            End Try
        End Sub

    End Class
    
End Namespace
