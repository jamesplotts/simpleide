' Widgets/CustomDrawComboBoxPopup.vb - Custom-drawn floating list popup for
' CustomDrawComboBox, adapted from the proven NavigationDropdownPopup mechanics
' (Popup-type window, focus-loss dismissal, keyboard nav, custom scrollbar) but simplified
' to a plain string list with no icons/tree indentation
Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' A custom-drawn floating popup list of plain text items, shown below a trigger
    ''' widget - used by CustomDrawComboBox instead of GTK's native ComboBox popup, which
    ''' proved unreliable when the combo was wrapped for custom chrome (see
    ''' CustomDrawComboBox's own remarks)
    ''' </summary>
    ''' <remarks>
    ''' Dismissed on focus loss, Escape, or a committed selection - deliberately avoids an
    ''' explicit pointer/device grab since that can't be interactively verified without real
    ''' mouse input; focus-loss dismissal is a well-established, simpler alternative used by
    ''' many custom GTK popups (including NavigationDropdownPopup, which this is adapted from)
    ''' </remarks>
    Public Class CustomDrawComboBoxPopup
        Inherits Gtk.Window

        Private Const MAX_VISIBLE_ITEMS As Integer = 8
        Private Const ROW_HEIGHT As Integer = 22
        Private Const SCROLLBAR_WIDTH As Integer = 8
        Private Const TEXT_PADDING_LEFT As Integer = 8
        Private Const TEXT_PADDING_RIGHT As Integer = 10
        Private Const MAX_POPUP_WIDTH As Integer = 520

        Private pDrawingArea As DrawingArea
        Private pItems As New List(Of String)
        Private pHighlightedIndex As Integer = -1
        Private pHoveredIndex As Integer = -1
        Private pScrollOffset As Integer = 0
        Private pThemeManager As ThemeManager
        Private pIsDraggingScrollbar As Boolean = False
        Private pDragStartY As Integer = 0
        Private pDragStartOffset As Integer = 0

        ''' <summary>Raised when the user commits a selection (Enter or click), with the
        ''' selected item's index into the list passed to ShowFor</summary>
        Public Event ItemSelected(vIndex As Integer)
        ''' <summary>Raised when the popup closes without a selection (Escape or focus loss)</summary>
        Public Event PopupCancelled()

        Public Sub New()
            MyBase.New(Gtk.WindowType.Popup)
            Try
                Decorated = False
                SkipTaskbarHint = True
                SkipPagerHint = True
                TypeHint = WindowTypeHint.Combo
                Resizable = False
                CanFocus = True

                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                pDrawingArea.Events = pDrawingArea.Events Or
                    EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                    EventMask.PointerMotionMask Or EventMask.ScrollMask Or
                    EventMask.KeyPressMask Or EventMask.LeaveNotifyMask
                Add(pDrawingArea)

                AddHandler pDrawingArea.Drawn, AddressOf OnDraw
                AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler pDrawingArea.ButtonReleaseEvent, AddressOf OnButtonRelease
                AddHandler pDrawingArea.MotionNotifyEvent, AddressOf OnMotionNotify
                AddHandler pDrawingArea.ScrollEvent, AddressOf OnScroll
                AddHandler pDrawingArea.LeaveNotifyEvent, AddressOf OnLeaveNotify
                AddHandler Me.KeyPressEvent, AddressOf OnKeyPress
                AddHandler Me.FocusOutEvent, AddressOf OnFocusOut

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBoxPopup constructor error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the theme manager used for background/foreground/selection colors
        ''' </summary>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            pThemeManager = vThemeManager
        End Sub

        ''' <summary>
        ''' Shows the popup below vTrigger, populated with vItems, with vInitialIndex (if
        ''' valid) initially highlighted
        ''' </summary>
        Public Sub ShowFor(vTrigger As Widget, vItems As List(Of String), vInitialIndex As Integer)
            Try
                pItems = If(vItems, New List(Of String))
                pScrollOffset = 0

                pHighlightedIndex = If(vInitialIndex >= 0 AndAlso vInitialIndex < pItems.Count, vInitialIndex, 0)
                EnsureHighlightVisible()

                Dim lWidth As Integer = ComputeWidth(vTrigger)
                Dim lVisibleCount As Integer = Math.Min(pItems.Count, MAX_VISIBLE_ITEMS)
                Dim lHeight As Integer = Math.Max(lVisibleCount, 1) * ROW_HEIGHT + 2

                pDrawingArea.SetSizeRequest(lWidth, lHeight)
                Resize(lWidth, lHeight)

                Dim lToplevel As Gtk.Window = TryCast(vTrigger.Toplevel, Gtk.Window)
                If lToplevel IsNot Nothing AndAlso lToplevel.Window IsNot Nothing Then
                    Dim lOriginX, lOriginY As Integer
                    lToplevel.Window.GetOrigin(lOriginX, lOriginY)

                    Dim lRelX, lRelY As Integer
                    vTrigger.TranslateCoordinates(lToplevel, 0, 0, lRelX, lRelY)

                    Move(lOriginX + lRelX, lOriginY + lRelY + vTrigger.AllocatedHeight)
                End If

                ShowAll()
                pDrawingArea.GrabFocus()
                Me.GrabFocus()

            Catch ex As Exception
                Console.WriteLine($"ShowFor error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Hides the popup, optionally raising PopupCancelled
        ''' </summary>
        Public Sub HidePopup(vRaiseCancelled As Boolean)
            Try
                If Not Visible Then Return
                Hide()
                If vRaiseCancelled Then RaiseEvent PopupCancelled()

            Catch ex As Exception
                Console.WriteLine($"HidePopup error: {ex.Message}")
            End Try
        End Sub

        ' ===== Sizing =====

        Private Function ComputeWidth(vTrigger As Widget) As Integer
            Try
                Dim lMaxTextWidth As Integer = 0
                Using lSurface As New Cairo.ImageSurface(Cairo.Format.Argb32, 1, 1)
                    Using lContext As New Cairo.Context(lSurface)
                        lContext.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal)
                        lContext.SetFontSize(11)
                        for each lItem in pItems
                            Dim lExtents As Cairo.TextExtents = lContext.TextExtents(lItem)
                            Dim lRowWidth As Integer = TEXT_PADDING_LEFT + CInt(lExtents.Width) + TEXT_PADDING_RIGHT
                            If lRowWidth > lMaxTextWidth Then lMaxTextWidth = lRowWidth
                        Next
                    End Using
                End Using

                Dim lWidth As Integer = Math.Max(lMaxTextWidth + SCROLLBAR_WIDTH, vTrigger.AllocatedWidth)
                Return Math.Min(lWidth, MAX_POPUP_WIDTH)

            Catch ex As Exception
                Console.WriteLine($"ComputeWidth error: {ex.Message}")
                Return If(vTrigger?.AllocatedWidth, 200)
            End Try
        End Function

        ' ===== Navigation state =====

        Private Sub EnsureHighlightVisible()
            If pHighlightedIndex < pScrollOffset Then
                pScrollOffset = pHighlightedIndex
            ElseIf pHighlightedIndex >= pScrollOffset + MAX_VISIBLE_ITEMS Then
                pScrollOffset = pHighlightedIndex - MAX_VISIBLE_ITEMS + 1
            End If
            Dim lMaxOffset As Integer = Math.Max(0, pItems.Count - MAX_VISIBLE_ITEMS)
            pScrollOffset = Math.Max(0, Math.Min(pScrollOffset, lMaxOffset))
        End Sub

        Private Sub MoveHighlight(vDelta As Integer)
            If pItems.Count = 0 Then Return
            pHighlightedIndex = Math.Max(0, Math.Min(pHighlightedIndex + vDelta, pItems.Count - 1))
            EnsureHighlightVisible()
            pDrawingArea.QueueDraw()
        End Sub

        Private Sub CommitSelection()
            If pHighlightedIndex < 0 OrElse pHighlightedIndex >= pItems.Count Then
                HidePopup(True)
                Return
            End If
            Dim lIndex As Integer = pHighlightedIndex
            Hide()
            RaiseEvent ItemSelected(lIndex)
        End Sub

        ' ===== Keyboard =====

        Private Function OnKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
            Try
                Select Case CType(vArgs.Event.Key, Gdk.Key)
                    Case Gdk.Key.Up
                        MoveHighlight(-1)
                        Return True
                    Case Gdk.Key.Down
                        MoveHighlight(1)
                        Return True
                    Case Gdk.Key.Page_Up
                        MoveHighlight(-MAX_VISIBLE_ITEMS)
                        Return True
                    Case Gdk.Key.Page_Down
                        MoveHighlight(MAX_VISIBLE_ITEMS)
                        Return True
                    Case Gdk.Key.Home
                        MoveHighlight(-pItems.Count)
                        Return True
                    Case Gdk.Key.End
                        MoveHighlight(pItems.Count)
                        Return True
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        CommitSelection()
                        Return True
                    Case Gdk.Key.Escape
                        HidePopup(True)
                        Return True
                End Select
                Return False

            Catch ex As Exception
                Console.WriteLine($"OnKeyPress error: {ex.Message}")
                Return False
            End Try
        End Function

        Private Sub OnFocusOut(vSender As Object, vArgs As FocusOutEventArgs)
            HidePopup(True)
        End Sub

        ' ===== Mouse =====

        Private Function RowIndexAtY(vY As Double) As Integer
            Dim lRow As Integer = pScrollOffset + CInt(vY) \ ROW_HEIGHT
            If lRow < 0 OrElse lRow >= pItems.Count Then Return -1
            Return lRow
        End Function

        Private Function OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                Dim lWidth As Integer = pDrawingArea.AllocatedWidth
                If vArgs.Event.X >= lWidth - SCROLLBAR_WIDTH AndAlso pItems.Count > MAX_VISIBLE_ITEMS Then
                    pIsDraggingScrollbar = True
                    pDragStartY = CInt(vArgs.Event.Y)
                    pDragStartOffset = pScrollOffset
                    Return True
                End If

                Dim lRow As Integer = RowIndexAtY(vArgs.Event.Y)
                If lRow >= 0 Then
                    pHighlightedIndex = lRow
                    CommitSelection()
                End If
                Return True

            Catch ex As Exception
                Console.WriteLine($"OnButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function

        Private Function OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            pIsDraggingScrollbar = False
            Return True
        End Function

        Private Function OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                If pIsDraggingScrollbar Then
                    Dim lTrackHeight As Integer = Math.Min(pItems.Count, MAX_VISIBLE_ITEMS) * ROW_HEIGHT
                    Dim lMaxOffset As Integer = Math.Max(0, pItems.Count - MAX_VISIBLE_ITEMS)
                    If lMaxOffset > 0 AndAlso lTrackHeight > 0 Then
                        Dim lDeltaY As Integer = CInt(vArgs.Event.Y) - pDragStartY
                        Dim lDeltaOffset As Integer = CInt(Math.Round(lDeltaY * pItems.Count / lTrackHeight))
                        pScrollOffset = Math.Max(0, Math.Min(pDragStartOffset + lDeltaOffset, lMaxOffset))
                        pDrawingArea.QueueDraw()
                    End If
                    Return True
                End If

                Dim lRow As Integer = RowIndexAtY(vArgs.Event.Y)
                If lRow <> pHoveredIndex Then
                    pHoveredIndex = lRow
                    pDrawingArea.QueueDraw()
                End If
                Return True

            Catch ex As Exception
                Console.WriteLine($"OnMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function

        Private Sub OnLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            pHoveredIndex = -1
            pDrawingArea.QueueDraw()
        End Sub

        Private Function OnScroll(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                Dim lMaxOffset As Integer = Math.Max(0, pItems.Count - MAX_VISIBLE_ITEMS)
                If vArgs.Event.Direction = ScrollDirection.Up Then
                    pScrollOffset = Math.Max(0, pScrollOffset - 1)
                ElseIf vArgs.Event.Direction = ScrollDirection.Down Then
                    pScrollOffset = Math.Min(lMaxOffset, pScrollOffset + 1)
                End If
                pDrawingArea.QueueDraw()
                Return True

            Catch ex As Exception
                Console.WriteLine($"OnScroll error: {ex.Message}")
                Return False
            End Try
        End Function

        ' ===== Drawing =====

        Private Sub OnDraw(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                Dim lWidth As Integer = pDrawingArea.AllocatedWidth
                Dim lHeight As Integer = pDrawingArea.AllocatedHeight
                Dim lTheme As EditorTheme = pThemeManager?.GetCurrentThemeObject()

                Dim lBgColor As Cairo.Color = HexToCairoColor(If(lTheme?.BackgroundColor, "#1E1E1E"))
                Dim lFgColor As Cairo.Color = HexToCairoColor(If(lTheme?.ForegroundColor, "#D4D4D4"))
                Dim lSelColor As Cairo.Color = HexToCairoColor(If(lTheme?.SelectionColor, "#264F78"))
                Dim lHoverColor As Cairo.Color = HexToCairoColor(If(lTheme?.CurrentLineColor, "#2A2D2E"))
                Dim lBorderColor As Cairo.Color = HexToCairoColor(If(lTheme?.AccentColor, "#007ACC"))
                Dim lTrackColor As Cairo.Color = HexToCairoColor(If(lTheme Is Nothing OrElse lTheme.IsDarkTheme, "#5A5A5A", "#B0B0B0"))

                ' Background
                lContext.SetSourceRGB(lBgColor.R, lBgColor.G, lBgColor.B)
                lContext.Rectangle(0, 0, lWidth, lHeight)
                lContext.Fill()

                ' Rows
                Dim lVisibleCount As Integer = Math.Min(pItems.Count - pScrollOffset, MAX_VISIBLE_ITEMS)
                for i As Integer = 0 To lVisibleCount - 1
                    Dim lIndex As Integer = pScrollOffset + i
                    Dim lText As String = pItems(lIndex)
                    Dim lY As Integer = i * ROW_HEIGHT

                    If lIndex = pHighlightedIndex Then
                        lContext.SetSourceRGB(lSelColor.R, lSelColor.G, lSelColor.B)
                        lContext.Rectangle(0, lY, lWidth, ROW_HEIGHT)
                        lContext.Fill()
                    ElseIf lIndex = pHoveredIndex Then
                        lContext.SetSourceRGB(lHoverColor.R, lHoverColor.G, lHoverColor.B)
                        lContext.Rectangle(0, lY, lWidth, ROW_HEIGHT)
                        lContext.Fill()
                    End If

                    lContext.SetSourceRGB(lFgColor.R, lFgColor.G, lFgColor.B)
                    lContext.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal)
                    lContext.SetFontSize(11)
                    Dim lExtents As Cairo.TextExtents = lContext.TextExtents(lText)
                    Dim lTextY As Integer = lY + CInt((ROW_HEIGHT + lExtents.Height) / 2)
                    lContext.MoveTo(TEXT_PADDING_LEFT, lTextY)
                    lContext.ShowText(lText)
                Next

                ' Border
                lContext.SetSourceRGB(lBorderColor.R, lBorderColor.G, lBorderColor.B)
                lContext.LineWidth = 1
                lContext.Rectangle(0.5, 0.5, lWidth - 1, lHeight - 1)
                lContext.Stroke()

                ' Custom scrollbar (no GTK step-arrow buttons)
                If pItems.Count > MAX_VISIBLE_ITEMS Then
                    DrawScrollbar(lContext, lWidth, lHeight, lTrackColor)
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBoxPopup.OnDraw error: {ex.Message}")
            End Try
        End Sub

        Private Sub DrawScrollbar(vContext As Cairo.Context, vWidth As Integer, vHeight As Integer, vTrackColor As Cairo.Color)
            Dim lTrackX As Double = vWidth - SCROLLBAR_WIDTH
            Dim lThumbHeight As Double = Math.Max(16.0, vHeight * MAX_VISIBLE_ITEMS / pItems.Count)
            Dim lMaxOffset As Integer = Math.Max(1, pItems.Count - MAX_VISIBLE_ITEMS)
            Dim lThumbY As Double = (vHeight - lThumbHeight) * pScrollOffset / lMaxOffset

            vContext.SetSourceRGBA(vTrackColor.R, vTrackColor.G, vTrackColor.B, 0.7)
            DrawRoundedRect(vContext, lTrackX + 1, lThumbY + 1, SCROLLBAR_WIDTH - 2, lThumbHeight - 2, 2)
            vContext.Fill()
        End Sub

        Private Sub DrawRoundedRect(vContext As Cairo.Context, vX As Double, vY As Double, vWidth As Double, vHeight As Double, vRadius As Double)
            vContext.NewPath()
            vContext.Arc(vX + vRadius, vY + vRadius, vRadius, Math.PI, Math.PI * 1.5)
            vContext.Arc(vX + vWidth - vRadius, vY + vRadius, vRadius, Math.PI * 1.5, Math.PI * 2)
            vContext.Arc(vX + vWidth - vRadius, vY + vHeight - vRadius, vRadius, 0, Math.PI * 0.5)
            vContext.Arc(vX + vRadius, vY + vHeight - vRadius, vRadius, Math.PI * 0.5, Math.PI)
            vContext.ClosePath()
        End Sub

        Private Shared Function HexToCairoColor(vHex As String) As Cairo.Color
            Try
                Dim lHex As String = vHex.TrimStart("#"c)
                Dim lR As Byte = Convert.ToByte(lHex.Substring(0, 2), 16)
                Dim lG As Byte = Convert.ToByte(lHex.Substring(2, 2), 16)
                Dim lB As Byte = Convert.ToByte(lHex.Substring(4, 2), 16)
                Return New Cairo.Color(lR / 255.0, lG / 255.0, lB / 255.0)

            Catch ex As Exception
                Console.WriteLine($"HexToCairoColor error: {ex.Message}")
                Return New Cairo.Color(0.5, 0.5, 0.5)
            End Try
        End Function

    End Class

End Namespace
