' Widgets/CustomDrawTextOutput.vb - Custom-drawn, line-based console-style text output
' widget. Replaces a native Gtk.TextView + Gtk.ScrolledWindow (used for BuildOutputPanel's
' Output tab), which becomes very slow to lay out and redraw once a build streams more than
' a few thousand lines - GtkTextView does expensive line-height caching/glyph shaping across
' the WHOLE buffer as it grows. This widget only ever measures/draws the visible slice of
' wrapped lines (viewport culling), so per-frame cost stays proportional to the visible area
' regardless of total scrollback size. Word-wrap is recomputed incrementally: only the newly
' appended line(s) are wrapped on AppendOutput, and the full buffer is only ever re-wrapped
' when the widget's width actually changes.
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Widgets

    ''' <summary>
    ''' Read-only, append-only console-style text output with word-wrap and a sunken bevel
    ''' border - the gutter-less analog of CustomDrawingEditor's client area, sized for
    ''' streaming build/run output rather than editing source code
    ''' </summary>
    Public Class CustomDrawTextOutput
        Inherits Box

        ''' <summary>
        ''' Color/style variant for one logical output line
        ''' </summary>
        Public Enum eOutputLineStyle
            ''' <summary>Unknown or unspecified style</summary>
            eUnspecified
            ''' <summary>Default foreground color</summary>
            eNormal
            ''' <summary>Rendered in the theme's error color</summary>
            eError
            ''' <summary>Rendered in the theme's warning color</summary>
            eWarning
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum

        ''' <summary>
        ''' One logical line of output text exactly as appended by the caller, plus its
        ''' color style - what word-wrap splits into one or more VisualLine rows
        ''' </summary>
        Private Class OutputLine
            Public Text As String = ""
            Public Style As eOutputLineStyle = eOutputLineStyle.eNormal
        End Class

        ''' <summary>
        ''' One wrapped visual row - what's actually measured and drawn. Several VisualLines
        ''' can point back at the same OutputLine (via LogicalIndex) when that line wrapped
        ''' </summary>
        Private Class VisualLine
            Public LogicalIndex As Integer = 0
            Public Text As String = ""
        End Class

        ' ===== Layout Constants =====
        Private Const BEVEL_WIDTH As Integer = 4
        Private Const LEFT_PADDING As Integer = 4
        Private Const TOP_PADDING As Integer = 2
        Private Const SCROLL_LINES_PER_NOTCH As Integer = 3

        ' ===== Private Fields =====
        Private pLines As New List(Of OutputLine)
        Private pVisualLines As New List(Of VisualLine)
        Private pTopVisualLine As Integer = 0
        Private pLastWrapWidth As Integer = -1
        Private pMetricsReady As Boolean = False

        Private pClientAreaBox As Box
        Private pDrawingArea As DrawingArea
        Private pVScrollbar As CustomDrawScrollbar
        Private pScrollbarValueChangedHandler As EventHandler

        Private pThemeManager As ThemeManager
        Private pFontDescription As Pango.FontDescription
        Private pFontMetrics As Utilities.FontMetrics
        Private pCharWidth As Integer = 8
        Private pLineHeight As Integer = 16

        ' ===== Constructor =====

        Public Sub New()
            MyBase.New(Orientation.Horizontal, 0)
            Try
                pFontDescription = Pango.FontDescription.FromString("Monospace 10")

                ' Wrap the drawing area with reserved interior padding (BorderWidth) so a
                ' sunken bevel border can be painted around just the text - not the
                ' scrollbar, which stays a separate sibling outside this box
                pClientAreaBox = New Box(Orientation.Horizontal, 0)
                pClientAreaBox.BorderWidth = BEVEL_WIDTH

                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                pDrawingArea.AddEvents(CInt(EventMask.ScrollMask))
                pClientAreaBox.PackStart(pDrawingArea, True, True, 0)
                AddHandler pClientAreaBox.Drawn, AddressOf OnClientAreaBevelDrawn

                pVScrollbar = New CustomDrawScrollbar(Orientation.Vertical)
                pScrollbarValueChangedHandler = New EventHandler(AddressOf OnScrollbarValueChanged)
                AddHandler pVScrollbar.ValueChanged, pScrollbarValueChangedHandler

                PackStart(pClientAreaBox, True, True, 0)
                PackStart(pVScrollbar, False, False, 0)

                AddHandler pDrawingArea.Drawn, AddressOf OnDrawingAreaDrawn
                AddHandler pDrawingArea.SizeAllocated, AddressOf OnDrawingAreaSizeAllocated
                AddHandler pDrawingArea.ScrollEvent, AddressOf OnDrawingAreaScrollEvent

                ShowAll()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.New error: {ex.Message}")
            End Try
        End Sub

        ' ===== Public API =====

        ''' <summary>
        ''' Appends text to the output, splitting it into one or more logical lines on any
        ''' embedded newlines, and scrolls to show the newly-appended content
        ''' </summary>
        ''' <param name="vText">Text to append - may contain embedded newlines</param>
        ''' <param name="vStyle">Color style applied to every line this call adds</param>
        Public Sub AppendOutput(vText As String, Optional vStyle As eOutputLineStyle = eOutputLineStyle.eNormal)
            Try
                If String.IsNullOrEmpty(vText) Then Return

                Dim lNormalized As String = vText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
                Dim lSegments() As String = lNormalized.Split(New Char() {ControlChars.Lf})

                ' A trailing newline produces one trailing empty split segment that isn't a
                ' real extra line - drop it so "foo" & Environment.NewLine adds exactly one
                ' logical line, not two
                Dim lCount As Integer = lSegments.Length
                If lCount > 0 AndAlso lSegments(lCount - 1) = "" AndAlso lNormalized.EndsWith(vbLf) Then
                    lCount -= 1
                End If
                If lCount <= 0 Then Return

                Dim lMaxChars As Integer = ComputeMaxCharsPerLine()
                for i As Integer = 0 To lCount - 1
                    pLines.Add(New OutputLine() With {.Text = lSegments(i), .Style = vStyle})
                    AppendVisualLinesFor(pLines.Count - 1, lMaxChars)
                Next

                UpdateScrollbarRange()
                ScrollToBottom()
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.AppendOutput error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Removes all output and resets scroll position to the top
        ''' </summary>
        Public Sub Clear()
            Try
                pLines.Clear()
                pVisualLines.Clear()
                pTopVisualLine = 0
                UpdateScrollbarRange()
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.Clear error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the ThemeManager used for background/foreground/error/warning/bevel colors
        ''' </summary>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                If pThemeManager IsNot Nothing Then
                    RemoveHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                pThemeManager = vThemeManager
                If pThemeManager IsNot Nothing Then
                    AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                pDrawingArea?.QueueDraw()
                pClientAreaBox?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ' ===== Theme =====

        Private Sub OnThemeChanged(vTheme As EditorTheme)
            pDrawingArea?.QueueDraw()
            pClientAreaBox?.QueueDraw()
        End Sub

        Private Function GetActiveTheme() As EditorTheme
            Return pThemeManager?.GetCurrentThemeObject()
        End Function

        ' ===== Word Wrap =====

        ''' <summary>
        ''' Wraps one logical line (by index into pLines) into VisualLines using the current
        ''' max-characters-per-row, appending them to the end of pVisualLines
        ''' </summary>
        Private Sub AppendVisualLinesFor(vLogicalIndex As Integer, vMaxChars As Integer)
            Dim lLine As OutputLine = pLines(vLogicalIndex)
            Dim lWrapped As List(Of String) = WrapText(lLine.Text, vMaxChars)
            for each lSegment in lWrapped
                pVisualLines.Add(New VisualLine() With {.LogicalIndex = vLogicalIndex, .Text = lSegment})
            Next
        End Sub

        ''' <summary>
        ''' Re-wraps every logical line from scratch - only needed when the drawing area's
        ''' width actually changes (word-wrap position depends on it)
        ''' </summary>
        Private Sub RewrapAll()
            Try
                pVisualLines.Clear()
                Dim lMaxChars As Integer = ComputeMaxCharsPerLine()
                for lIndex As Integer = 0 To pLines.Count - 1
                    AppendVisualLinesFor(lIndex, lMaxChars)
                Next

                Dim lVisibleRows As Integer = GetVisibleRowCount()
                Dim lMaxTop As Integer = Math.Max(0, pVisualLines.Count - lVisibleRows)
                If pTopVisualLine > lMaxTop Then pTopVisualLine = lMaxTop

                UpdateScrollbarRange()
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.RewrapAll error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Greedily word-wraps a single line of text to at most vMaxChars per row, breaking
        ''' at the last space within the limit, or hard-breaking mid-word if a single token
        ''' exceeds vMaxChars on its own. Always returns at least one entry, even for an
        ''' empty line, so blank lines still occupy a visual row.
        ''' </summary>
        Private Function WrapText(vText As String, vMaxChars As Integer) As List(Of String)
            Dim lResult As New List(Of String)
            Try
                If vMaxChars <= 0 OrElse String.IsNullOrEmpty(vText) OrElse vText.Length <= vMaxChars Then
                    lResult.Add(vText)
                    Return lResult
                End If

                Dim lRemaining As String = vText
                While lRemaining.Length > vMaxChars
                    Dim lBreakAt As Integer = -1
                    for i As Integer = vMaxChars To 1 Step -1
                        If lRemaining(i) = " "c Then
                            lBreakAt = i
                            Exit for
                        End If
                    Next

                    If lBreakAt <= 0 Then
                        lResult.Add(lRemaining.Substring(0, vMaxChars))
                        lRemaining = lRemaining.Substring(vMaxChars)
                    Else
                        lResult.Add(lRemaining.Substring(0, lBreakAt))
                        lRemaining = lRemaining.Substring(lBreakAt + 1) ' skip the space
                    End If
                End While
                If lRemaining.Length > 0 Then lResult.Add(lRemaining)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.WrapText error: {ex.Message}")
                lResult.Clear()
                lResult.Add(vText)
            End Try
            Return lResult
        End Function

        Private Function ComputeMaxCharsPerLine() As Integer
            Dim lWidth As Integer = If(pDrawingArea IsNot Nothing, pDrawingArea.AllocatedWidth, 0)
            If lWidth <= 0 OrElse pCharWidth <= 0 Then Return 80 ' reasonable fallback before first real layout
            Return Math.Max(1, (lWidth - LEFT_PADDING * 2) \ pCharWidth)
        End Function

        ' ===== Drawing =====

        Private Sub OnDrawingAreaDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                DrawContent(vArgs.Cr)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.OnDrawingAreaDrawn error: {ex.Message}")
            End Try
        End Sub

        Private Sub DrawContent(vContext As Cairo.Context)
            Dim lWidth As Integer = pDrawingArea.AllocatedWidth
            Dim lHeight As Integer = pDrawingArea.AllocatedHeight
            If lWidth <= 0 OrElse lHeight <= 0 Then Return

            EnsureFontMetrics(vContext)

            Dim lTheme As EditorTheme = GetActiveTheme()

            Dim lBgColor As Cairo.Color = ParseColor(If(lTheme?.BackgroundColor, "#1E1E1E"))
            vContext.SetSourceRGB(lBgColor.R, lBgColor.G, lBgColor.B)
            vContext.Rectangle(0, 0, lWidth, lHeight)
            vContext.Fill()

            If pLineHeight <= 0 OrElse pVisualLines.Count = 0 Then Return

            Dim lNormalColor As Cairo.Color = ParseColor(If(lTheme?.ForegroundColor, "#D4D4D4"))
            Dim lErrorColor As Cairo.Color = ParseColor(If(lTheme?.ErrorColor, "#F48771"))
            Dim lWarningColor As Cairo.Color = ParseColor(If(lTheme?.WarningColor, "#CCA700"))

            Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
            lLayout.FontDescription = pFontDescription

            Dim lVisibleRows As Integer = GetVisibleRowCount()
            Dim lStartIndex As Integer = Math.Max(0, Math.Min(pTopVisualLine, pVisualLines.Count - 1))
            Dim lEndIndex As Integer = Math.Min(pVisualLines.Count - 1, lStartIndex + lVisibleRows)

            for i As Integer = lStartIndex To lEndIndex
                Dim lVisual As VisualLine = pVisualLines(i)
                Dim lLogical As OutputLine = pLines(lVisual.LogicalIndex)

                Dim lColor As Cairo.Color = lNormalColor
                Select Case lLogical.Style
                    Case eOutputLineStyle.eError
                        lColor = lErrorColor
                    Case eOutputLineStyle.eWarning
                        lColor = lWarningColor
                End Select

                Dim lY As Integer = (i - lStartIndex) * pLineHeight + TOP_PADDING
                vContext.SetSourceRGB(lColor.R, lColor.G, lColor.B)
                vContext.MoveTo(LEFT_PADDING, lY)
                lLayout.SetText(lVisual.Text)
                Pango.CairoHelper.ShowLayout(vContext, lLayout)
            Next
        End Sub

        Private Sub EnsureFontMetrics(vContext As Cairo.Context)
            If pMetricsReady Then Return
            Try
                pFontMetrics = New Utilities.FontMetrics(pFontDescription, vContext)
                pCharWidth = Math.Max(1, pFontMetrics.CharWidth)
                pLineHeight = Math.Max(1, pFontMetrics.CharHeight)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.EnsureFontMetrics error: {ex.Message}")
                pCharWidth = 8
                pLineHeight = 16
            End Try
            pMetricsReady = True
            ' Real metrics just became available - re-wrap using the accurate char width
            ' rather than whatever fallback earlier appends/allocations used
            RewrapAll()
        End Sub

        ''' <summary>
        ''' Paints a sunken 3D bevel (dark top/left, light bottom/right) into
        ''' pClientAreaBox's reserved BorderWidth padding, matching
        ''' CustomDrawingEditor.OnClientAreaBevelDrawn's convention
        ''' </summary>
        Private Sub OnClientAreaBevelDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lWidth As Integer = pClientAreaBox.AllocatedWidth
                Dim lHeight As Integer = pClientAreaBox.AllocatedHeight
                If lWidth <= 0 OrElse lHeight <= 0 Then Return

                Dim lTheme As EditorTheme = GetActiveTheme()
                If lTheme Is Nothing Then Return

                Dim lContext As Cairo.Context = vArgs.Cr
                Dim lBevel As Integer = Math.Min(BEVEL_WIDTH, Math.Min(lWidth, lHeight) \ 2)
                If lBevel <= 0 Then Return

                Dim lDarkColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eBevelDarkColor)
                Dim lLightColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eBevelLightColor)

                lContext.SetSourceRGB(lDarkColor.R, lDarkColor.G, lDarkColor.B)
                lContext.Rectangle(0, 0, lWidth, lBevel)                      ' top
                lContext.Fill()
                lContext.Rectangle(0, 0, lBevel, lHeight)                     ' left
                lContext.Fill()

                lContext.SetSourceRGB(lLightColor.R, lLightColor.G, lLightColor.B)
                lContext.Rectangle(0, lHeight - lBevel, lWidth, lBevel)       ' bottom
                lContext.Fill()
                lContext.Rectangle(lWidth - lBevel, 0, lBevel, lHeight)       ' right
                lContext.Fill()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.OnClientAreaBevelDrawn error: {ex.Message}")
            End Try
        End Sub

        Private Function ParseColor(vHex As String) As Cairo.Color
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHex) Then
                    Return New Cairo.Color(lColor.Red, lColor.Green, lColor.Blue)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.ParseColor error: {ex.Message}")
            End Try
            Return New Cairo.Color(1, 1, 1)
        End Function

        ' ===== Scrolling =====

        Private Function GetVisibleRowCount() As Integer
            If pDrawingArea Is Nothing OrElse pLineHeight <= 0 Then Return 1
            Return Math.Max(1, pDrawingArea.AllocatedHeight \ pLineHeight)
        End Function

        Private Sub ScrollToBottom()
            Try
                Dim lVisibleRows As Integer = GetVisibleRowCount()
                pTopVisualLine = Math.Max(0, pVisualLines.Count - lVisibleRows)
                SyncScrollbarPosition()
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.ScrollToBottom error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates the scrollbar's Lower/Upper/PageSize/Increments from the current visual
        ''' line count, without moving the current scroll position beyond clamping it into
        ''' range (SyncScrollbarPosition/ScrollToBottom handle actually moving Value)
        ''' </summary>
        Private Sub UpdateScrollbarRange()
            Try
                If pVScrollbar Is Nothing Then Return
                Dim lVisibleRows As Integer = GetVisibleRowCount()
                Dim lAdjustment As Adjustment = pVScrollbar.Adjustment

                RemoveHandler pVScrollbar.ValueChanged, pScrollbarValueChangedHandler
                lAdjustment.Lower = 0
                lAdjustment.Upper = Math.Max(pVisualLines.Count, lVisibleRows)
                lAdjustment.PageSize = lVisibleRows
                lAdjustment.StepIncrement = 1
                lAdjustment.PageIncrement = Math.Max(1, lVisibleRows - 1)

                Dim lMaxTop As Integer = Math.Max(0, pVisualLines.Count - lVisibleRows)
                If pTopVisualLine > lMaxTop Then pTopVisualLine = lMaxTop
                lAdjustment.Value = pTopVisualLine

                pVScrollbar.Visible = pVisualLines.Count > lVisibleRows
                AddHandler pVScrollbar.ValueChanged, pScrollbarValueChangedHandler

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.UpdateScrollbarRange error: {ex.Message}")
            End Try
        End Sub

        Private Sub SyncScrollbarPosition()
            Try
                If pVScrollbar Is Nothing Then Return
                RemoveHandler pVScrollbar.ValueChanged, pScrollbarValueChangedHandler
                pVScrollbar.Adjustment.Value = pTopVisualLine
                AddHandler pVScrollbar.ValueChanged, pScrollbarValueChangedHandler
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.SyncScrollbarPosition error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnScrollbarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                pTopVisualLine = CInt(pVScrollbar.Adjustment.Value)
                pDrawingArea?.QueueDraw()
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.OnScrollbarValueChanged error: {ex.Message}")
            End Try
        End Sub

        Private Function OnDrawingAreaScrollEvent(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                Dim lVisibleRows As Integer = GetVisibleRowCount()
                Dim lMaxTop As Integer = Math.Max(0, pVisualLines.Count - lVisibleRows)

                If vArgs.Event.Direction = Gdk.ScrollDirection.Up Then
                    pTopVisualLine = Math.Max(0, pTopVisualLine - SCROLL_LINES_PER_NOTCH)
                ElseIf vArgs.Event.Direction = Gdk.ScrollDirection.Down Then
                    pTopVisualLine = Math.Min(lMaxTop, pTopVisualLine + SCROLL_LINES_PER_NOTCH)
                Else
                    Return False
                End If

                SyncScrollbarPosition()
                pDrawingArea?.QueueDraw()
                Return True

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.OnDrawingAreaScrollEvent error: {ex.Message}")
                Return False
            End Try
        End Function

        Private Sub OnDrawingAreaSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            Try
                Dim lNewWidth As Integer = vArgs.Allocation.Width
                If lNewWidth > 0 AndAlso lNewWidth <> pLastWrapWidth Then
                    pLastWrapWidth = lNewWidth
                    If pMetricsReady Then RewrapAll()
                Else
                    UpdateScrollbarRange()
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextOutput.OnDrawingAreaSizeAllocated error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
