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
                    
                    ' Set default text color
                    Dim lFgColor As New RGBA()
                    If lFgColor.Parse(pForegroundColor) Then
                        vContext.SetSourceRgba(lFgColor.Red, lFgColor.Green, lFgColor.Blue, 1.0)
                    Else
                        vContext.SetSourceRgba(0.52, 0.52, 0.52, 1.0) ' Fallback gray
                    End If
                    
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
                        
                        ' DEBUG LOGGING
                        If lVisualIndex < 5 Then
                            Try
                                Using writer As New System.IO.StreamWriter("/home/jamesp/.gemini/debug_folding.log", True)
                                    writer.WriteLine($"[{DateTime.Now}] Widget: Visual={lVisualIndex}, Source={lSourceLine}, FirstVisible={lFirstVisibleLine}")
                                End Using
                            Catch
                            End Try
                        End If
                        
                        ' Fallback: If we get 0 for a non-zero visual line, and the map seems broken, use visual line
                        ' This handles the case where pVisualLineMap might be truncated or invalid
                        If lSourceLine = 0 AndAlso lVisualIndex > 0 AndAlso pEditor.GetVisualLineCount() <= 1 Then
                             lSourceLine = lVisualIndex
                        End If
                        
                        ' Set text (1-based source line numbers)
                        lLayout.SetText((lSourceLine + 1).ToString())
                        
                        ' Highlight current line number if needed
                        If lSourceLine = lCurrentSourceLine Then
                            Dim lCurrentColor As New RGBA()
                            If lCurrentColor.Parse(pCurrentLineColor) Then
                                vContext.SetSourceRgba(lCurrentColor.Red, lCurrentColor.Green, lCurrentColor.Blue, 1.0)
                            Else
                                vContext.SetSourceRgba(0.78, 0.78, 0.78, 1.0) ' Fallback light gray
                            End If
                            
                            ' Draw line number right-aligned within the text area (left of padding)
                            ' pWidth - pRightPadding is the boundary. We subtract text width from there.
                            Dim lTextWidth As Integer
                            Dim lTextHeight As Integer
                            lLayout.GetPixelSize(lTextWidth, lTextHeight)
                            Dim lX As Integer = pWidth - pRightPadding - lTextWidth - 5 ' Extra 5px buffer
                            
                            vContext.MoveTo(lX, lY)
                            Pango.CairoHelper.ShowLayout(vContext, lLayout)
                            
                            ' Restore default color
                            vContext.SetSourceRGB(0.5, 0.5, 0.5)
                        Else
                            ' Draw line number right-aligned within the text area (left of padding)
                            ' pWidth - pRightPadding is the boundary. We subtract text width from there.
                            Dim lTextWidth As Integer
                            Dim lTextHeight As Integer
                            lLayout.GetPixelSize(lTextWidth, lTextHeight)
                            Dim lX As Integer = pWidth - pRightPadding - lTextWidth - 5 ' Extra 5px buffer
                            
                            vContext.MoveTo(lX, lY)
                            Pango.CairoHelper.ShowLayout(vContext, lLayout)
                        End If
                        
                        ' Draw fold icon if needed
                        ' Center it in the right padding area
                        Dim lFoldNode As SyntaxNode = pEditor.GetFoldableNodeAtLine(lSourceLine)
                        If lFoldNode IsNot Nothing Then
                            Dim lIconX As Integer = pWidth - (pRightPadding / 2) - 4 ' Center 8px icon
                            DrawFoldIcon(vContext, lIconX, lLineTop + (pLineHeight / 2) - 4, lFoldNode.IsExpanded)
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
                Dim lY As Double = vArgs.Event.Y - pTopPadding
                Dim lClickedVisualLine As Integer = CInt(Math.Floor(lY / pLineHeight)) + pEditor.FirstVisibleLine
                Dim lClickedSourceLine As Integer = pEditor.VisualToSourceLine(lClickedVisualLine)
                
                Console.WriteLine($"LineNumberWidget.OnButtonPress: Clicked visual line {lClickedVisualLine}, source line {lClickedSourceLine}")
                
                ' Check for fold toggle click (right side of widget)
                If vArgs.Event.X > pWidth - pRightPadding Then
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
                Dim lY As Double = vArgs.Event.Y - pTopPadding
                Dim lHoverLine As Integer = CInt(Math.Floor(lY / pLineHeight)) + pEditor.FirstVisibleLine
                
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

        ' Replace: SimpleIDE.Widgets.LineNumberWidget.HandleDoubleClick
        ''' <summary>
        ''' Handles double-click on a line number to select entire method if applicable
        ''' </summary>
        ''' <param name="vLineIndex">The line that was double-clicked (0-based)</param>
        Private Sub HandleDoubleClick(vLineIndex As Integer)
            Try
                Console.WriteLine($"LineNumberWidget.HandleDoubleClick: Processing line {vLineIndex}")
                
                If pEditor Is Nothing OrElse vLineIndex < 0 OrElse vLineIndex >= pEditor.LineCount Then
                    Console.WriteLine($"LineNumberWidget.HandleDoubleClick: Invalid line index {vLineIndex} or no editor")
                    ' Invalid line - just select the single line
                    pEditor.SelectLine(vLineIndex)
                    ' Grab focus for the drawing area
                    pEditor.GrabFocus()
                    Return
                End If
                
                ' Get the line text
                Dim lLineText As String = pEditor.GetLineText(vLineIndex)
                If String.IsNullOrWhiteSpace(lLineText) Then
                    Console.WriteLine("LineNumberWidget.HandleDoubleClick: Empty line")
                    ' Empty line - just select it
                    pEditor.SelectLine(vLineIndex)
                    ' Grab focus for the drawing area
                    pEditor.GrabFocus()
                    Return
                End If
                
                ' Check if this line is a method/property declaration
                Dim lTrimmed As String = lLineText.Trim().ToUpper()
                Console.WriteLine($"LineNumberWidget.HandleDoubleClick: Checking line: '{lTrimmed}'")
                
                ' Check for method/property declaration patterns
                If IsMethodDeclaration(lTrimmed) Then
                    Console.WriteLine("LineNumberWidget.HandleDoubleClick: Is a method declaration!")
                    ' Determine if we're in an interface
                    Dim lIsInterface As Boolean = IsInInterface(vLineIndex)
                    Console.WriteLine($"LineNumberWidget.HandleDoubleClick: IsInterface = {lIsInterface}")
                    
                    If lIsInterface Then
                        ' In an interface - just select the declaration line plus any XML docs
                        Console.WriteLine("LineNumberWidget.HandleDoubleClick: in Interface - selecting declaration with XML docs")
                        Dim lStartLine As Integer = FindXmlDocumentationStart(vLineIndex)
                        pEditor.SelectLines(lStartLine, vLineIndex)
                    Else
                        ' Not in an interface - find and select the entire method including XML docs
                        Dim lEndLine As Integer = FindMethodEndLine(vLineIndex, lTrimmed)
                        Console.WriteLine($"LineNumberWidget.HandleDoubleClick: End line found at {lEndLine}")
                        
                        If lEndLine >= vLineIndex Then
                            ' Find the start of XML documentation comments
                            Dim lStartLine As Integer = FindXmlDocumentationStart(vLineIndex)
                            Console.WriteLine($"LineNumberWidget.HandleDoubleClick: XML docs start at line {lStartLine}")
                            
                            ' Select from XML docs start to end line (inclusive)
                            Console.WriteLine($"LineNumberWidget.HandleDoubleClick: Selecting lines {lStartLine} To {lEndLine}")
                            pEditor.SelectLines(lStartLine, lEndLine)
                        Else
                            ' Couldn't find end - select current line with XML docs
                            Console.WriteLine("LineNumberWidget.HandleDoubleClick: No End found - selecting declaration with XML docs")
                            Dim lStartLine As Integer = FindXmlDocumentationStart(vLineIndex)
                            pEditor.SelectLines(lStartLine, vLineIndex)
                        End If
                    End If
                Else
                    Console.WriteLine("LineNumberWidget.HandleDoubleClick: Not a method declaration")
                    ' Not a method declaration - just select the line
                    pEditor.SelectLine(vLineIndex)
                End If
                
                ' CRITICAL: Grab focus for the drawing area after any selection
                pEditor.GrabFocus()
                Console.WriteLine("LineNumberWidget.HandleDoubleClick: Focus grabbed for drawing area")
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.HandleDoubleClick error: {ex.Message}")
            End Try
        End Sub
        

        ''' <summary>
        ''' Checks if a line contains a method, function, or property declaration
        ''' </summary>
        ''' <param name="vTrimmedLine">The trimmed uppercase line text to check</param>
        ''' <returns>True if the line is a method/function/property declaration</returns>
        Private Function IsMethodDeclaration(vTrimmedLine As String) As Boolean
            Try
                ' Skip empty lines and comments
                If String.IsNullOrWhiteSpace(vTrimmedLine) OrElse vTrimmedLine.StartsWith("'") Then
                    Return False
                End If
                
                ' Skip END statements
                If vTrimmedLine.StartsWith("END ") Then
                    Return False
                End If
                
                ' Skip EXIT statements
                If vTrimmedLine.StartsWith("EXIT ") Then
                    Return False
                End If
                
                ' Skip DECLARE statements (external declarations)
                If vTrimmedLine.StartsWith("DECLARE ") Then
                    Return False
                End If
                
                ' Check for GET/SET property accessors (these are not top-level declarations)
                If vTrimmedLine = "GET" OrElse vTrimmedLine = "SET" OrElse
                   vTrimmedLine.StartsWith("GET ") OrElse vTrimmedLine.StartsWith("SET ") Then
                    ' But only if they're actually property accessors, not method names
                    Dim lWords As String() = vTrimmedLine.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
                    If lWords.Length > 0 AndAlso (lWords(0) = "GET" OrElse lWords(0) = "SET") Then
                        Return False
                    End If
                End If
                
                ' Check if the line contains SUB, FUNCTION, or PROPERTY keywords
                ' These could be preceded by access modifiers
                If vTrimmedLine.Contains(" SUB ") OrElse vTrimmedLine.StartsWith("SUB ") OrElse
                   vTrimmedLine.Contains(" FUNCTION ") OrElse vTrimmedLine.StartsWith("FUNCTION ") OrElse
                   vTrimmedLine.Contains(" PROPERTY ") OrElse vTrimmedLine.StartsWith("PROPERTY ") Then
                    
                    Console.WriteLine($"IsMethodDeclaration: Found method keyword in '{vTrimmedLine}'")
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.IsMethodDeclaration error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Checks if a given line is inside an interface declaration
        ''' </summary>
        ''' <param name="vLineIndex">The line index to check (0-based)</param>
        ''' <returns>True if the line is inside an interface</returns>
        Private Function IsInInterface(vLineIndex As Integer) As Boolean
            Try
                ' Search backwards for Interface or Class/Module/Structure declarations
                for i As Integer = vLineIndex - 1 To 0 Step -1
                    Dim lLine As String = pEditor.GetLineText(i)
                    If String.IsNullOrWhiteSpace(lLine) Then Continue for
                    
                    Dim lTrimmed As String = lLine.Trim().ToUpper()
                    
                    ' Skip comments
                    If lTrimmed.StartsWith("'") Then Continue for
                    
                    ' Check for Interface start
                    If lTrimmed.StartsWith("Interface ") OrElse
                       lTrimmed.Contains(" Interface ") Then
                        ' Make sure it's not "End Interface"
                        If Not lTrimmed.StartsWith("End ") Then
                            Return True
                        End If
                    End If
                    
                    ' Check for End Interface (means we're not in an interface)
                    If lTrimmed.StartsWith("End Interface") Then
                        Return False
                    End If
                    
                    ' Check for Class/Module/Structure (means we're not in an interface)
                    If lTrimmed.StartsWith("Class ") OrElse lTrimmed.Contains(" Class ") OrElse
                       lTrimmed.StartsWith("Module ") OrElse lTrimmed.Contains(" Module ") OrElse
                       lTrimmed.StartsWith("Structure ") OrElse lTrimmed.Contains(" Structure ") Then
                        ' Make sure it's not an End statement
                        If Not lTrimmed.StartsWith("End ") Then
                            Return False
                        End If
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.IsInInterface error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Finds the end line of a method/function/property declaration
        ''' </summary>
        ''' <param name="vStartLine">The line where the method starts (0-based)</param>
        ''' <param name="vDeclarationLine">The trimmed uppercase declaration line</param>
        ''' <returns>The line index of the End statement, or vStartLine if not found</returns>
        Private Function FindMethodEndLine(vStartLine As Integer, vDeclarationLine As String) As Integer
            Try
                ' Determine what type of block we're looking for
                Dim lBlockType As String = ""
                
                ' Check what type of method this is
                If vDeclarationLine.Contains(" SUB ") OrElse vDeclarationLine.StartsWith("SUB ") Then
                    lBlockType = "SUB"
                ElseIf vDeclarationLine.Contains(" FUNCTION ") OrElse vDeclarationLine.StartsWith("FUNCTION ") Then
                    lBlockType = "FUNCTION"
                ElseIf vDeclarationLine.Contains(" PROPERTY ") OrElse vDeclarationLine.StartsWith("PROPERTY ") Then
                    lBlockType = "PROPERTY"
                Else
                    Console.WriteLine($"FindMethodEndLine: Unknown block type for '{vDeclarationLine}'")
                    Return vStartLine
                End If
                
                Console.WriteLine($"FindMethodEndLine: Looking for End {lBlockType} starting from line {vStartLine}")
                
                ' For properties, check if it's auto-implemented (single line)
                If lBlockType = "Property" Then
                    ' Check the next line to see if it's GET or SET
                    If vStartLine + 1 < pEditor.LineCount Then
                        Dim lNextLine As String = pEditor.GetLineText(vStartLine + 1).Trim().ToUpper()
                        If Not lNextLine.StartsWith("Get") AndAlso Not lNextLine.StartsWith("Set") Then
                            ' Auto-implemented property - single line
                            Console.WriteLine("FindMethodEndLine: Auto-implemented Property")
                            Return vStartLine
                        End If
                    Else
                        ' Last line of file
                        Return vStartLine
                    End If
                End If
                
                ' Track nesting level for nested blocks
                Dim lNestLevel As Integer = 1
                
                ' Search forward for the matching END statement
                For i As Integer = vStartLine + 1 To pEditor.LineCount - 1
                    Dim lLine As String = pEditor.GetLineText(i)
                    If String.IsNullOrWhiteSpace(lLine) Then Continue For
                    
                    Dim lTrimmed As String = lLine.Trim()
                    
                    ' Skip comments
                    If lTrimmed.StartsWith("'") Then Continue For
                    
                    ' Skip lines inside strings (very basic check)
                    If lTrimmed.Contains("""") Then Continue for
                    
                    ' Split into words to check first and second word
                    Dim lWords As String() = lTrimmed.Split({" "c, vbTab}, StringSplitOptions.RemoveEmptyEntries)
                    If lWords.Length = 0 Then Continue for
                    
                    ' Convert first word to uppercase for comparison
                    Dim lFirstWord As String = lWords(0).ToUpper()
                    
                    ' Check if this is an END statement
                    If lFirstWord = "END" AndAlso lWords.Length >= 2 Then
                        Dim lSecondWord As String = lWords(1).ToUpper()
                        
                        ' Check if this matches our block type
                        If lSecondWord = lBlockType Then
                            lNestLevel -= 1
                            Console.WriteLine($"  Found 'END {lBlockType}' at line {i}, nest level now {lNestLevel}")
                            If lNestLevel = 0 Then
                                ' Found the matching END statement
                                Console.WriteLine($"FindMethodEndLine: Found End at line {i}")
                                Return i
                            End If
                        End If
                    ' Check for nested blocks (but not EXIT statements)
                    ElseIf lFirstWord <> "Exit" AndAlso lFirstWord <> "Declare" Then
                        ' Look for the block type keyword in the line
                        ' Must be careful to only match actual declarations, not references
                        Dim lIsDeclaration As Boolean = False
                        
                        For j As Integer = 0 To lWords.Length - 1
                            Dim lWord As String = lWords(j).ToUpper()
                            
                            ' Check if this word is our block type and it's preceded by a modifier or is first
                            If lWord = lBlockType Then
                                ' Check if it's preceded by a valid modifier or is the first word
                                If j = 0 OrElse IsAccessModifier(lWords(j - 1).ToUpper()) Then
                                    lIsDeclaration = True
                                    Exit For
                                End If
                            End If
                        Next
                        
                        If lIsDeclaration Then
                            lNestLevel += 1
                            Console.WriteLine($"  Found nested {lBlockType} at line {i}, nest level now {lNestLevel}")
                        End If
                    End If
                Next
                
                ' If we couldn't find the end, return the start line
                Console.WriteLine($"FindMethodEndLine: No End found, returning start line {vStartLine}")
                Return vStartLine
                
            Catch ex As Exception
                Console.WriteLine($"LineNumberWidget.FindMethodEndLine error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a word is an access modifier
        ''' </summary>
        ''' <param name="vWord">The word to check (uppercase)</param>
        ''' <returns>True if it's an access modifier</returns>
        Private Function IsAccessModifier(vWord As String) As Boolean
            Select Case vWord
                Case "Public", "Private", "Protected", "Friend", "Partial", "Shared", "Overrides", "Overridable", "MustOverride", "NotOverridable", "Shadows", "Overloads"
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        ''' <summary>
        ''' Finds the starting line of XML documentation comments before a declaration
        ''' </summary>
        ''' <param name="vDeclarationLine">The line containing the method/property declaration (0-based)</param>
        ''' <returns>The first line of XML documentation, or vDeclarationLine if no docs found</returns>
        Private Function FindXmlDocumentationStart(vDeclarationLine As Integer) As Integer
            Try
                Dim lStartLine As Integer = vDeclarationLine
                
                ' Search backwards for XML documentation lines
                For i As Integer = vDeclarationLine - 1 To 0 Step -1
                    Dim lLine As String = pEditor.GetLineText(i)
                    
                    ' Check if this is an XML documentation line
                    If Not String.IsNullOrWhiteSpace(lLine) Then
                        Dim lTrimmed As String = lLine.TrimStart()
                        If lTrimmed.StartsWith("'''") Then
                            ' This is an XML doc line - update start
                            lStartLine = i
                            Console.WriteLine($"  Found XML doc at line {i}: '{lTrimmed.Substring(0, Math.Min(50, lTrimmed.Length))}'")
                        Else
                            ' Not an XML doc line - stop searching
                            Exit For
                        End If
                    ElseIf i < vDeclarationLine - 1 Then
                        ' Empty line after we've already found XML docs - stop
                        Exit For
                    End If
                Next
                
                Return lStartLine
                
            Catch ex As Exception
                Console.WriteLine($"FindXmlDocumentationStart error: {ex.Message}")
                Return vDeclarationLine
            End Try
        End Function
        
        ''' <summary>
        ''' Draws a fold icon (plus or minus)
        ''' </summary>
        Private Sub DrawFoldIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vIsExpanded As Boolean)
            Try
                Dim lSize As Integer = 9
                
                ' Draw box
                vContext.SetSourceRGB(0.5, 0.5, 0.5)
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
