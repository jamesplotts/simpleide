' Editors/CustomDrawingEditor.CodeSensePopup.vb - CodeSense suggestion popup, drawn directly
' on the editor's own Cairo surface instead of a separate GTK popup window
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' ===== CodeSense Popup State =====

        Private pCodeSenseSuggestions As New List(Of CodeSenseSuggestion)
        Private pCodeSenseSelectedIndex As Integer = 0
        Private pCodeSenseScrollOffset As Integer = 0
        Private pCodeSenseHoverIndex As Integer = -1
        Private pCodeSenseScrollbarDragging As Boolean = False

        Private Const CodeSensePopupVisibleItems As Integer = 7
        Private Const CodeSensePopupMinWidth As Integer = 180
        Private Const CodeSensePopupMaxWidth As Integer = 420
        Private Const CodeSensePopupScrollbarWidth As Integer = 12

        ''' <summary>
        ''' Matches the empirical +13 correction DrawContent applies when drawing the actual
        ''' text cursor (CustomDrawingEditor.Drawing.vb) - pTopPadding alone is a glyph-layout
        ''' anchor, not the visual top of a rendered row. Box/border/swatch/scrollbar geometry
        ''' needs this correction to align with the row a human actually sees; suggestion TEXT
        ''' does not, because it already lands correctly via the "+lAscent" glyph-position
        ''' formula applied to the *uncorrected* row top - so this constant is added when
        ''' computing box geometry and subtracted back out before positioning text
        ''' </summary>
        Private Const CodeSenseRowTopCorrection As Integer = 13

        ' ===== Public Entry Points =====

        ''' <summary>
        ''' Shows the CodeSense suggestion popup for the given suggestions, drawn on the
        ''' editor's own surface at the cursor position
        ''' </summary>
        ''' <param name="vSuggestions">Suggestions to display, already filtered/sorted by CodeSenseEngine</param>
        ''' <param name="vContext">The context this suggestion list was generated for</param>
        Public Sub ShowCodeSenseSuggestions(vSuggestions As List(Of CodeSenseSuggestion), vContext As CodeSenseContext)
            Try
                If vSuggestions Is Nothing OrElse vSuggestions.Count = 0 Then
                    HideCodeSensePopup()
                    Return
                End If

                pCodeSenseSuggestions = vSuggestions
                pCodeSenseContext = vContext
                pCodeSenseSelectedIndex = 0
                pCodeSenseScrollOffset = 0
                pCodeSenseHoverIndex = -1
                pCodeSenseActive = True

                EnsureRoomForCodeSensePopup()

                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"ShowCodeSenseSuggestions error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Hides the CodeSense popup and clears its state
        ''' </summary>
        Private Sub HideCodeSensePopup()
            Try
                If Not pCodeSenseActive Then Return

                pCodeSenseActive = False
                pCodeSenseSuggestions = New List(Of CodeSenseSuggestion)()
                pCodeSenseSelectedIndex = 0
                pCodeSenseScrollOffset = 0
                pCodeSenseHoverIndex = -1
                pCodeSenseScrollbarDragging = False
                pCodeSenseContext = Nothing

                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"HideCodeSensePopup error: {ex.Message}")
            End Try
        End Sub

        ' ===== Geometry =====

        ''' <summary>
        ''' Scrolls the editor viewport (vertically and/or horizontally) to make room for
        ''' the popup before it is drawn, if the cursor is too close to a viewport edge
        ''' </summary>
        ''' <remarks>
        ''' This runs before GetCodeSensePopupBounds's own flip/clamp logic, which remains
        ''' the fallback for cases where there simply isn't anywhere left to scroll to
        ''' (e.g. the cursor is on the last line of the document)
        ''' </remarks>
        Private Sub EnsureRoomForCodeSensePopup()
            Try
                If pDrawingArea Is Nothing Then Return

                Dim lVisibleCount As Integer = Math.Min(CodeSensePopupVisibleItems, Math.Max(1, pCodeSenseSuggestions.Count))
                Dim lNeededLines As Integer = lVisibleCount + 1 ' +1 for the cursor's own line

                Dim lCursorVisualLine As Integer = SourceToVisualLine(pCursorLine)
                Dim lLinesBelow As Integer = (pFirstVisibleLine + pTotalVisibleLines) - lCursorVisualLine

                If lLinesBelow < lNeededLines AndAlso pVScrollbar IsNot Nothing Then
                    Dim lDeficit As Integer = lNeededLines - lLinesBelow
                    Dim lVisualLineCount As Integer = GetVisualLineCount()
                    Dim lMaxFirstLine As Integer = Math.Max(0, lVisualLineCount - pTotalVisibleLines)
                    Dim lNewFirstLine As Integer = Math.Min(lMaxFirstLine, pFirstVisibleLine + lDeficit)

                    If lNewFirstLine <> pFirstVisibleLine Then
                        pFirstVisibleLine = lNewFirstLine
                        pVScrollbar.Value = pFirstVisibleLine
                    End If
                End If

                Dim lPopupWidth As Integer = MeasureCodeSensePopupWidth()
                Dim lNeededColumns As Integer = CInt(Math.Ceiling(lPopupWidth / CDbl(Math.Max(1, pCharWidth)))) + 1
                Dim lColumnsRight As Integer = (pFirstVisibleColumn + pTotalVisibleColumns) - pCursorColumn

                If lColumnsRight < lNeededColumns AndAlso pHScrollbar IsNot Nothing Then
                    Dim lColumnDeficit As Integer = lNeededColumns - lColumnsRight
                    Dim lNewFirstColumn As Integer = Math.Max(0, pFirstVisibleColumn + lColumnDeficit)

                    If lNewFirstColumn <> pFirstVisibleColumn Then
                        pFirstVisibleColumn = lNewFirstColumn
                        pHScrollbar.Value = pFirstVisibleColumn
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"EnsureRoomForCodeSensePopup error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Computes the popup's current on-screen bounds, flipping vertically/horizontally
        ''' as needed to stay within the drawing area's viewport
        ''' </summary>
        ''' <returns>Popup bounds in drawing-area-local pixel coordinates</returns>
        Private Function GetCodeSensePopupBounds() As Gdk.Rectangle
            Try
                Dim lVisibleCount As Integer = Math.Min(CodeSensePopupVisibleItems, Math.Max(1, pCodeSenseSuggestions.Count))
                Dim lHeight As Integer = lVisibleCount * pLineHeight + 4
                Dim lWidth As Integer = MeasureCodeSensePopupWidth()

                Dim lFirstLine As Integer = Math.Max(0, pFirstVisibleLine)
                Dim lCursorVisualLine As Integer = SourceToVisualLine(pCursorLine)

                Dim lAnchorX As Integer = pLeftPadding + (pCursorColumn - pFirstVisibleColumn) * pCharWidth
                Dim lBelowY As Integer = (lCursorVisualLine - lFirstLine + 1) * pLineHeight + pTopPadding + CodeSenseRowTopCorrection
                Dim lAboveY As Integer = (lCursorVisualLine - lFirstLine) * pLineHeight + pTopPadding + CodeSenseRowTopCorrection

                Dim lViewportWidth As Integer = If(pDrawingArea IsNot Nothing, pDrawingArea.AllocatedWidth, 0)
                Dim lViewportHeight As Integer = If(pDrawingArea IsNot Nothing, pDrawingArea.AllocatedHeight, 0)

                Dim lY As Integer
                If lBelowY + lHeight <= lViewportHeight Then
                    lY = lBelowY
                ElseIf lAboveY - lHeight >= 0 Then
                    lY = lAboveY - lHeight
                Else
                    lY = Math.Max(0, lViewportHeight - lHeight)
                End If

                Dim lX As Integer = lAnchorX
                If lX + lWidth > lViewportWidth Then
                    lX = Math.Max(0, lViewportWidth - lWidth)
                End If

                Return New Gdk.Rectangle(lX, lY, lWidth, lHeight)

            Catch ex As Exception
                Console.WriteLine($"GetCodeSensePopupBounds error: {ex.Message}")
                Return New Gdk.Rectangle(0, 0, 0, 0)
            End Try
        End Function

        ''' <summary>
        ''' Measures the popup's width from the longest suggestion's insertable text
        ''' </summary>
        ''' <remarks>
        ''' The editor renders text at a fixed pCharWidth per character (monospace), so a
        ''' character-count estimate is exact rather than approximate here - no need for a
        ''' Pango text measurement pass, which would require a live Cairo context this method
        ''' doesn't have when called ahead of drawing (see EnsureRoomForCodeSensePopup)
        ''' </remarks>
        Private Function MeasureCodeSensePopupWidth() As Integer
            Try
                Dim lMaxChars As Integer = 20

                for each lSuggestion in pCodeSenseSuggestions
                    Dim lLen As Integer = If(lSuggestion.Text, "").Length
                    If lLen > lMaxChars Then lMaxChars = lLen
                Next

                Dim lWidth As Integer = lMaxChars * pCharWidth + (pLeftPadding * 2) + CodeSensePopupScrollbarWidth + 8
                Return Math.Max(CodeSensePopupMinWidth, Math.Min(CodeSensePopupMaxWidth, lWidth))

            Catch ex As Exception
                Console.WriteLine($"MeasureCodeSensePopupWidth error: {ex.Message}")
                Return CodeSensePopupMinWidth
            End Try
        End Function

        ' ===== Drawing =====

        ''' <summary>
        ''' Draws the CodeSense suggestion popup on top of the editor content
        ''' </summary>
        ''' <param name="vContext">Cairo context from the main OnDraw pass</param>
        ''' <remarks>
        ''' Must be called last in the drawing sequence so it paints over previously-drawn text
        ''' </remarks>
        Private Sub DrawCodeSensePopup(vContext As Cairo.Context)
            Try
                If Not pCodeSenseActive OrElse pCodeSenseSuggestions Is Nothing OrElse pCodeSenseSuggestions.Count = 0 Then Return

                Dim lBounds As Gdk.Rectangle = GetCodeSensePopupBounds()
                If lBounds.Width <= 0 OrElse lBounds.Height <= 0 Then Return

                Dim lTheme As EditorTheme = GetActiveTheme()
                Dim lVisibleCount As Integer = Math.Min(CodeSensePopupVisibleItems, pCodeSenseSuggestions.Count)
                Dim lHasScrollbar As Boolean = pCodeSenseSuggestions.Count > CodeSensePopupVisibleItems
                Dim lListWidth As Integer = lBounds.Width - If(lHasScrollbar, CodeSensePopupScrollbarWidth, 0)

                vContext.Save()

                ' Background
                Dim lBgColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eCurrentLineColor)
                Dim lBgPattern As New Cairo.SolidPattern(lBgColor.R, lBgColor.G, lBgColor.B)
                vContext.SetSource(lBgPattern)
                vContext.Rectangle(lBounds.X, lBounds.Y, lBounds.Width, lBounds.Height)
                vContext.Fill()
                lBgPattern.Dispose()

                ' Border
                Dim lBorderColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eAccentColor)
                vContext.SetSourceRGB(lBorderColor.R, lBorderColor.G, lBorderColor.B)
                vContext.LineWidth = 1.0
                vContext.Rectangle(lBounds.X + 0.5, lBounds.Y + 0.5, lBounds.Width - 1, lBounds.Height - 1)
                vContext.Stroke()

                ' Rows
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription

                Dim lAscent As Integer
                If pFontMetrics IsNot Nothing AndAlso pFontMetrics.Ascent > 0 Then
                    lAscent = pFontMetrics.Ascent
                Else
                    lAscent = CInt(pLineHeight * 0.75)
                End If

                Dim lSelColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eSelectionColor)
                Dim lTextColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eForegroundColor)
                Dim lSelectedTextColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eSelectionText)

                Dim lSwatchSize As Integer = 8
                Dim lTextX As Integer = lBounds.X + 6 + lSwatchSize + 6

                For i As Integer = 0 To lVisibleCount - 1
                    Dim lIndex As Integer = pCodeSenseScrollOffset + i
                    If lIndex >= pCodeSenseSuggestions.Count Then Exit For

                    Dim lSuggestion As CodeSenseSuggestion = pCodeSenseSuggestions(lIndex)
                    Dim lRowY As Integer = lBounds.Y + 2 + i * pLineHeight

                    If lIndex = pCodeSenseSelectedIndex Then
                        Dim lSelPattern As New Cairo.SolidPattern(lSelColor.R, lSelColor.G, lSelColor.B)
                        vContext.SetSource(lSelPattern)
                        vContext.Rectangle(lBounds.X + 1, lRowY, lListWidth - 2, pLineHeight)
                        vContext.Fill()
                        lSelPattern.Dispose()
                    ElseIf lIndex = pCodeSenseHoverIndex Then
                        Dim lHoverColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eLineNumberBackgroundColor)
                        Dim lHoverPattern As New Cairo.SolidPattern(lHoverColor.R, lHoverColor.G, lHoverColor.B)
                        vContext.SetSource(lHoverPattern)
                        vContext.Rectangle(lBounds.X + 1, lRowY, lListWidth - 2, pLineHeight)
                        vContext.Fill()
                        lHoverPattern.Dispose()
                    End If

                    ' Kind swatch
                    Dim lKindColor As Cairo.Color = GetCodeSenseKindColor(lSuggestion.Kind, lTheme.IsDarkTheme)
                    vContext.SetSourceRGB(lKindColor.R, lKindColor.G, lKindColor.B)
                    vContext.Rectangle(lBounds.X + 6, lRowY + (pLineHeight - lSwatchSize) \ 2, lSwatchSize, lSwatchSize)
                    vContext.Fill()

                    ' Text - use the theme's selection-contrast text color on the highlighted
                    ' row so it stays legible against the selection background
                    Dim lRowTextColor As Cairo.Color = If(lIndex = pCodeSenseSelectedIndex, lSelectedTextColor, lTextColor)
                    Dim lTextPattern As New Cairo.SolidPattern(lRowTextColor.R, lRowTextColor.G, lRowTextColor.B)
                    vContext.SetSource(lTextPattern)
                    lLayout.SetText(If(lSuggestion.Text, ""))
                    vContext.MoveTo(lTextX, lRowY + lAscent - CodeSenseRowTopCorrection)
                    Pango.CairoHelper.ShowLayout(vContext, lLayout)
                    lTextPattern.Dispose()
                Next

                lLayout.Dispose()

                If lHasScrollbar Then
                    DrawCodeSensePopupScrollbar(vContext, lBounds, lTheme)
                End If

                vContext.Restore()

            Catch ex As Exception
                Console.WriteLine($"DrawCodeSensePopup error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws the vertical scrollbar track and thumb on the right edge of the popup
        ''' </summary>
        Private Sub DrawCodeSensePopupScrollbar(vContext As Cairo.Context, vBounds As Gdk.Rectangle, vTheme As EditorTheme)
            Try
                Dim lTrackX As Integer = vBounds.X + vBounds.Width - CodeSensePopupScrollbarWidth

                Dim lTrackColor As Cairo.Color = vTheme.CairoColor(EditorTheme.Tags.eLineNumberBackgroundColor)
                vContext.SetSourceRGB(lTrackColor.R, lTrackColor.G, lTrackColor.B)
                vContext.Rectangle(lTrackX, vBounds.Y, CodeSensePopupScrollbarWidth, vBounds.Height)
                vContext.Fill()

                Dim lThumbHeight As Integer
                Dim lThumbY As Integer
                GetCodeSenseScrollbarThumbGeometry(vBounds, lThumbHeight, lThumbY)

                Dim lThumbColor As Cairo.Color = vTheme.CairoColor(EditorTheme.Tags.eAccentColor)
                vContext.SetSourceRGB(lThumbColor.R, lThumbColor.G, lThumbColor.B)
                vContext.Rectangle(lTrackX + 2, lThumbY, CodeSensePopupScrollbarWidth - 4, lThumbHeight)
                vContext.Fill()

            Catch ex As Exception
                Console.WriteLine($"DrawCodeSensePopupScrollbar error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Computes the scrollbar thumb's height and Y position for the current scroll state
        ''' </summary>
        Private Sub GetCodeSenseScrollbarThumbGeometry(vBounds As Gdk.Rectangle, ByRef vThumbHeight As Integer, ByRef vThumbY As Integer)
            Dim lTotalCount As Integer = Math.Max(1, pCodeSenseSuggestions.Count)
            Dim lVisibleCount As Integer = Math.Min(CodeSensePopupVisibleItems, lTotalCount)
            Dim lMaxScrollOffset As Integer = Math.Max(1, lTotalCount - lVisibleCount)

            vThumbHeight = Math.Max(16, CInt(vBounds.Height * (lVisibleCount / CDbl(lTotalCount))))
            Dim lThumbTravel As Integer = Math.Max(1, vBounds.Height - vThumbHeight)
            vThumbY = vBounds.Y + CInt(lThumbTravel * (pCodeSenseScrollOffset / CDbl(lMaxScrollOffset)))
        End Sub

        ''' <summary>
        ''' Maps a suggestion kind to a swatch color, matching the exact palette Object Explorer
        ''' uses for the equivalent CodeNodeType (see DrawFallbackIcon in
        ''' Widgets/CustomDrawObjectExplorer.Drawing.vb) so the two stay visually consistent
        ''' </summary>
        ''' <param name="vKind">The suggestion's kind</param>
        ''' <param name="vIsDarkTheme">Whether the active theme is dark (Object Explorer's palette differs by theme)</param>
        Private Function GetCodeSenseKindColor(vKind As CodeSenseSuggestionKind, vIsDarkTheme As Boolean) As Cairo.Color
            Select Case vKind
                Case CodeSenseSuggestionKind.eNamespace
                    Return If(vIsDarkTheme, HexToCairoColor("#C77DFF"), HexToCairoColor("#9D4EDD"))

                Case CodeSenseSuggestionKind.eClass
                    Return If(vIsDarkTheme, HexToCairoColor("#4EC9B0"), HexToCairoColor("#2B91AF"))

                Case CodeSenseSuggestionKind.eInterface
                    Return If(vIsDarkTheme, HexToCairoColor("#B8D7A3"), HexToCairoColor("#6B8E23"))

                Case CodeSenseSuggestionKind.eMethod
                    Return If(vIsDarkTheme, HexToCairoColor("#DCDCAA"), HexToCairoColor("#795E26"))

                Case CodeSenseSuggestionKind.eProperty
                    Return If(vIsDarkTheme, HexToCairoColor("#9CDCFE"), HexToCairoColor("#0070C0"))

                Case CodeSenseSuggestionKind.eField
                    Return If(vIsDarkTheme, HexToCairoColor("#51CF66"), HexToCairoColor("#2B8A3E"))

                Case CodeSenseSuggestionKind.eEvent
                    Return If(vIsDarkTheme, HexToCairoColor("#CE9178"), HexToCairoColor("#A31515"))

                Case CodeSenseSuggestionKind.eSnippet
                    ' No Object Explorer equivalent - reuses its eDelegate color
                    Return If(vIsDarkTheme, HexToCairoColor("#C586C0"), HexToCairoColor("#9B4F96"))

                Case CodeSenseSuggestionKind.eLocalVariable, CodeSenseSuggestionKind.eParameter
                    ' No Object Explorer equivalent (locals/parameters aren't project members) -
                    ' reuses its eOperator neutral color
                    Return If(vIsDarkTheme, HexToCairoColor("#D4D4D4"), HexToCairoColor("#000000"))

                Case CodeSenseSuggestionKind.eKeyword
                    ' No Object Explorer equivalent - keeps the editor's own keyword syntax color
                    Return If(vIsDarkTheme, HexToCairoColor("#569CD6"), HexToCairoColor("#0000FF"))

                Case Else
                    Return If(vIsDarkTheme, HexToCairoColor("#808080"), HexToCairoColor("#606060"))
            End Select
        End Function

        ' ===== Selection / Commit =====

        ''' <summary>
        ''' Moves the popup selection by vDelta items, keeping a "sticky" scroll window so
        ''' the highlighted row stays at least one row away from the top/bottom edge unless
        ''' it's genuinely the first/last item in the list
        ''' </summary>
        ''' <param name="vDelta">Items to move by (negative = up, positive = down)</param>
        Private Sub MoveCodeSenseSelection(vDelta As Integer)
            Try
                If pCodeSenseSuggestions Is Nothing OrElse pCodeSenseSuggestions.Count = 0 Then Return

                Dim lCount As Integer = pCodeSenseSuggestions.Count
                pCodeSenseSelectedIndex = Math.Max(0, Math.Min(lCount - 1, pCodeSenseSelectedIndex + vDelta))

                Dim lVisibleCount As Integer = Math.Min(CodeSensePopupVisibleItems, lCount)
                Dim lMaxScrollOffset As Integer = Math.Max(0, lCount - lVisibleCount)
                Dim lPadding As Integer = If(lVisibleCount > 2, 1, 0)

                If pCodeSenseSelectedIndex = 0 Then
                    pCodeSenseScrollOffset = 0
                ElseIf pCodeSenseSelectedIndex = lCount - 1 Then
                    pCodeSenseScrollOffset = lMaxScrollOffset
                ElseIf pCodeSenseSelectedIndex - lPadding < pCodeSenseScrollOffset Then
                    pCodeSenseScrollOffset = Math.Max(0, pCodeSenseSelectedIndex - lPadding)
                ElseIf pCodeSenseSelectedIndex + lPadding > pCodeSenseScrollOffset + lVisibleCount - 1 Then
                    pCodeSenseScrollOffset = Math.Min(lMaxScrollOffset, pCodeSenseSelectedIndex + lPadding - lVisibleCount + 1)
                End If

                pCodeSenseScrollOffset = Math.Max(0, Math.Min(lMaxScrollOffset, pCodeSenseScrollOffset))

                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"MoveCodeSenseSelection error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Replaces the word at the cursor with the currently-selected suggestion's text
        ''' </summary>
        Private Sub CommitCodeSenseSelection()
            Try
                If pCodeSenseSuggestions Is Nothing OrElse pCodeSenseSelectedIndex < 0 OrElse pCodeSenseSelectedIndex >= pCodeSenseSuggestions.Count Then
                    HideCodeSensePopup()
                    Return
                End If

                Dim lSuggestion As CodeSenseSuggestion = pCodeSenseSuggestions(pCodeSenseSelectedIndex)
                Dim lInsertText As String = lSuggestion.Text

                If pSourceFileInfo IsNot Nothing AndAlso pCursorLine < pSourceFileInfo.TextLines.Count AndAlso Not String.IsNullOrEmpty(lInsertText) Then
                    Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)

                    Dim lWordStart As Integer = pCursorColumn
                    While lWordStart > 0 AndAlso lWordStart <= lLine.Length AndAlso (Char.IsLetterOrDigit(lLine(lWordStart - 1)) OrElse lLine(lWordStart - 1) = "_"c)
                        lWordStart -= 1
                    End While

                    Dim lWordEnd As Integer = pCursorColumn
                    While lWordEnd < lLine.Length AndAlso (Char.IsLetterOrDigit(lLine(lWordEnd)) OrElse lLine(lWordEnd) = "_"c)
                        lWordEnd += 1
                    End While

                    If lWordEnd > lWordStart Then
                        SetSelection(New EditorPosition(pCursorLine, lWordStart), New EditorPosition(pCursorLine, lWordEnd))
                    End If

                    InsertText(lInsertText)
                End If

                HideCodeSensePopup()

            Catch ex As Exception
                Console.WriteLine($"CommitCodeSenseSelection error: {ex.Message}")
            End Try
        End Sub

        ' ===== Keyboard Interaction =====

        ''' <summary>
        ''' Handles a key press while the popup is active
        ''' </summary>
        ''' <param name="vKey">The key that was pressed</param>
        ''' <returns>True if the key was consumed by the popup and should not reach normal editing</returns>
        Private Function HandleCodeSensePopupKeyPress(vKey As Gdk.Key) As Boolean
            Try
                Select Case vKey
                    Case Gdk.Key.Up, Gdk.Key.KP_Up
                        MoveCodeSenseSelection(-1)
                        Return True

                    Case Gdk.Key.Down, Gdk.Key.KP_Down
                        MoveCodeSenseSelection(1)
                        Return True

                    Case Gdk.Key.Page_Up, Gdk.Key.KP_Page_Up
                        MoveCodeSenseSelection(-CodeSensePopupVisibleItems)
                        Return True

                    Case Gdk.Key.Page_Down, Gdk.Key.KP_Page_Down
                        MoveCodeSenseSelection(CodeSensePopupVisibleItems)
                        Return True

                    Case Gdk.Key.Return, Gdk.Key.KP_Enter, Gdk.Key.Tab, Gdk.Key.ISO_Left_Tab
                        CommitCodeSenseSelection()
                        Return True

                    Case Gdk.Key.space, Gdk.Key.period
                        ' "Commit characters" - accept the highlighted suggestion, then let
                        ' the space/period itself still reach normal character handling below
                        ' (so "Str" + space completes to "String " and "Str" + "." completes
                        ' to "String." and immediately opens member-list for it)
                        CommitCodeSenseSelection()
                        Return False

                    Case Gdk.Key.Escape
                        HideCodeSensePopup()
                        Return True

                    Case Gdk.Key.Left, Gdk.Key.KP_Left, Gdk.Key.Right, Gdk.Key.KP_Right,
                         Gdk.Key.Home, Gdk.Key.KP_Home, Gdk.Key.End, Gdk.Key.KP_End
                        ' Caret navigation dismisses the popup, but the key itself still
                        ' needs to move the caret, so fall through to normal handling
                        HideCodeSensePopup()
                        Return False
                End Select

                Return False

            Catch ex As Exception
                Console.WriteLine($"HandleCodeSensePopupKeyPress error: {ex.Message}")
                Return False
            End Try
        End Function

        ' ===== Mouse Interaction =====

        ''' <summary>
        ''' Handles a button press while the popup is active
        ''' </summary>
        ''' <returns>True if the click was consumed by the popup</returns>
        Private Function HandleCodeSensePopupButtonPress(vX As Double, vY As Double) As Boolean
            Try
                If Not pCodeSenseActive Then Return False

                Dim lBounds As Gdk.Rectangle = GetCodeSensePopupBounds()

                If vX < lBounds.X OrElse vX > lBounds.X + lBounds.Width OrElse vY < lBounds.Y OrElse vY > lBounds.Y + lBounds.Height Then
                    ' Click outside the popup - dismiss it and let the click proceed normally
                    HideCodeSensePopup()
                    Return False
                End If

                Dim lHasScrollbar As Boolean = pCodeSenseSuggestions.Count > CodeSensePopupVisibleItems
                Dim lScrollbarX As Integer = lBounds.X + lBounds.Width - CodeSensePopupScrollbarWidth

                If lHasScrollbar AndAlso vX >= lScrollbarX Then
                    pCodeSenseScrollbarDragging = True
                    UpdateCodeSenseScrollFromDragY(vY, lBounds)
                    Return True
                End If

                Dim lRowIndex As Integer = CInt(Math.Floor((vY - lBounds.Y - 2) / CDbl(pLineHeight)))
                Dim lIndex As Integer = pCodeSenseScrollOffset + lRowIndex
                If lIndex >= 0 AndAlso lIndex < pCodeSenseSuggestions.Count Then
                    pCodeSenseSelectedIndex = lIndex
                    CommitCodeSenseSelection()
                End If

                Return True

            Catch ex As Exception
                Console.WriteLine($"HandleCodeSensePopupButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Handles button release while the popup is active (ends scrollbar dragging)
        ''' </summary>
        ''' <returns>True if the release was consumed by the popup</returns>
        Private Function HandleCodeSensePopupButtonRelease() As Boolean
            If pCodeSenseScrollbarDragging Then
                pCodeSenseScrollbarDragging = False
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' Handles mouse motion while the popup is active (hover highlight, scrollbar drag)
        ''' </summary>
        ''' <returns>True if the motion was consumed by the popup</returns>
        Private Function HandleCodeSensePopupMotion(vX As Double, vY As Double) As Boolean
            Try
                If Not pCodeSenseActive Then Return False

                Dim lBounds As Gdk.Rectangle = GetCodeSensePopupBounds()

                If pCodeSenseScrollbarDragging Then
                    UpdateCodeSenseScrollFromDragY(vY, lBounds)
                    Return True
                End If

                If vX >= lBounds.X AndAlso vX <= lBounds.X + lBounds.Width AndAlso
                   vY >= lBounds.Y AndAlso vY <= lBounds.Y + lBounds.Height Then

                    ' Use the normal arrow pointer over the popup instead of the editor's
                    ' text (I-beam) cursor - this isn't text the user is editing
                    If pDrawingArea?.Window IsNot Nothing AndAlso pPointerCursor IsNot Nothing Then
                        pDrawingArea.Window.Cursor = pPointerCursor
                    End If

                    Dim lHasScrollbar As Boolean = pCodeSenseSuggestions.Count > CodeSensePopupVisibleItems
                    Dim lScrollbarX As Integer = lBounds.X + lBounds.Width - CodeSensePopupScrollbarWidth

                    If Not (lHasScrollbar AndAlso vX >= lScrollbarX) Then
                        Dim lRowIndex As Integer = CInt(Math.Floor((vY - lBounds.Y - 2) / CDbl(pLineHeight)))
                        Dim lIndex As Integer = pCodeSenseScrollOffset + lRowIndex
                        Dim lNewHover As Integer = If(lIndex >= 0 AndAlso lIndex < pCodeSenseSuggestions.Count, lIndex, -1)
                        If lNewHover <> pCodeSenseHoverIndex Then
                            pCodeSenseHoverIndex = lNewHover
                            pDrawingArea?.QueueDraw()
                        End If
                    End If
                    Return True
                Else
                    If pCodeSenseHoverIndex <> -1 Then
                        pCodeSenseHoverIndex = -1
                        pDrawingArea?.QueueDraw()
                    End If
                End If

                Return False

            Catch ex As Exception
                Console.WriteLine($"HandleCodeSensePopupMotion error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Handles scroll wheel events while the popup is active
        ''' </summary>
        ''' <returns>True if the scroll was consumed by the popup (cursor was over it)</returns>
        Private Function HandleCodeSensePopupScroll(vX As Double, vY As Double, vDirection As Gdk.ScrollDirection) As Boolean
            Try
                If Not pCodeSenseActive Then Return False

                Dim lBounds As Gdk.Rectangle = GetCodeSensePopupBounds()
                If vX < lBounds.X OrElse vX > lBounds.X + lBounds.Width OrElse vY < lBounds.Y OrElse vY > lBounds.Y + lBounds.Height Then
                    Return False
                End If

                Dim lTotalCount As Integer = pCodeSenseSuggestions.Count
                Dim lVisibleCount As Integer = Math.Min(CodeSensePopupVisibleItems, lTotalCount)
                Dim lMaxScrollOffset As Integer = Math.Max(0, lTotalCount - lVisibleCount)

                Select Case vDirection
                    Case Gdk.ScrollDirection.Up
                        pCodeSenseScrollOffset = Math.Max(0, pCodeSenseScrollOffset - 1)
                    Case Gdk.ScrollDirection.Down
                        pCodeSenseScrollOffset = Math.Min(lMaxScrollOffset, pCodeSenseScrollOffset + 1)
                End Select

                pDrawingArea?.QueueDraw()
                Return True

            Catch ex As Exception
                Console.WriteLine($"HandleCodeSensePopupScroll error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Updates the scroll offset from a scrollbar drag/click Y position
        ''' </summary>
        Private Sub UpdateCodeSenseScrollFromDragY(vY As Double, vBounds As Gdk.Rectangle)
            Try
                Dim lTotalCount As Integer = Math.Max(1, pCodeSenseSuggestions.Count)
                Dim lVisibleCount As Integer = Math.Min(CodeSensePopupVisibleItems, lTotalCount)
                Dim lMaxScrollOffset As Integer = Math.Max(1, lTotalCount - lVisibleCount)

                Dim lThumbHeight As Integer
                Dim lUnusedY As Integer
                GetCodeSenseScrollbarThumbGeometry(vBounds, lThumbHeight, lUnusedY)

                Dim lThumbTravel As Integer = Math.Max(1, vBounds.Height - lThumbHeight)
                Dim lRelativeY As Double = vY - vBounds.Y - (lThumbHeight / 2.0)
                Dim lRatio As Double = Math.Max(0, Math.Min(1, lRelativeY / lThumbTravel))

                pCodeSenseScrollOffset = CInt(Math.Round(lRatio * lMaxScrollOffset))
                pCodeSenseScrollOffset = Math.Max(0, Math.Min(lMaxScrollOffset, pCodeSenseScrollOffset))

                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"UpdateCodeSenseScrollFromDragY error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
