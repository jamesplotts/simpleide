' Widgets/NavigationDropdownPopup.vb - Custom-drawn floating list popup for NavigationDropdowns
Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' A custom-drawn floating popup list used by NavigationDropdowns for both the Class
    ''' dropdown (an always-expanded indented tree with connector lines) and the Member
    ''' dropdown (a flat sorted list) - replaces GTK's native ComboBoxText popup rendering
    ''' </summary>
    ''' <remarks>
    ''' Dismissed on Escape, a committed selection, or a click anywhere outside the popup -
    ''' the outside-click case uses a GTK (not X-server) grab via Gtk.Grab.Add: while held,
    ''' GTK redirects any button-press event whose natural target isn't this window or a
    ''' descendant of it (e.g. a click in the main editor window) to this window's own
    ''' ButtonPressEvent instead, which is how GtkMenu and other same-process popups dismiss
    ''' on outside click without needing a raw pointer/device grab. FocusOutEvent is kept as
    ''' a secondary trigger (e.g. Alt-Tab to another application, which the in-process grab
    ''' above doesn't cover).
    ''' </remarks>
    Public Class NavigationDropdownPopup
        Inherits Gtk.Window

        Private Const MAX_VISIBLE_ITEMS As Integer = 7
        Private Const ROW_HEIGHT As Integer = 22
        Private Const ICON_SIZE As Integer = 14
        Private Const ICON_SPACING As Integer = 6
        Private Const INDENT_WIDTH As Integer = 16
        Private Const SCROLLBAR_WIDTH As Integer = 8
        Private Const TEXT_PADDING_LEFT As Integer = 6
        Private Const TEXT_PADDING_RIGHT As Integer = 10
        Private Const MAX_POPUP_WIDTH As Integer = 520

        ''' <summary>
        ''' A single entry in a NavigationDropdownPopup list
        ''' </summary>
        Public Class Item
            ''' <summary>Display text (already formatted, e.g. "Function IsGitRepository()")</summary>
            Public Property Text As String = ""
            ''' <summary>Set for Class-dropdown items; determines the icon shape/color</summary>
            Public Property ObjectType As CodeObjectType = CodeObjectType.eUnspecified
            ''' <summary>Set for Member-dropdown items; determines the icon shape/color</summary>
            Public Property MemberType As CodeMemberType = CodeMemberType.eUnspecified
            ''' <summary>True if ObjectType should be used for the icon, False for MemberType</summary>
            Public Property IsClassItem As Boolean = False
            ''' <summary>Tree depth - 0 for top-level, used only by the Class dropdown</summary>
            Public Property IndentLevel As Integer = 0
            ''' <summary>
            ''' For each ancestor level (0..IndentLevel-1), True if that ancestor still has
            ''' more siblings after it (so a continuing vertical bar should be drawn in that
            ''' column), False if that ancestor was its parent's last child (blank column)
            ''' </summary>
            Public Property AncestorContinues As New List(Of Boolean)
            ''' <summary>Whether this item is the last among its own siblings (└ vs ├)</summary>
            Public Property IsLastSibling As Boolean = True
            ''' <summary>Opaque payload returned via ItemSelected - the underlying CodeObject/CodeMember</summary>
            Public Property Tag As Object = Nothing
        End Class

        Private pDrawingArea As DrawingArea
        Private pItems As New List(Of Item)
        Private pHighlightedIndex As Integer = -1
        Private pHoveredIndex As Integer = -1
        Private pScrollOffset As Integer = 0
        Private pThemeManager As ThemeManager
        Private pIsDraggingScrollbar As Boolean = False
        Private pDragStartY As Integer = 0
        Private pDragStartOffset As Integer = 0

        ''' <summary>Raised when the user commits a selection (Enter or click)</summary>
        Public Event ItemSelected(vTag As Object)
        ''' <summary>Raised when the popup closes without a selection (Escape or focus loss)</summary>
        Public Event PopupCancelled()

        Private Shared ReadOnly pIconColorsDark As New Dictionary(Of String, Cairo.Color)
        Private Shared ReadOnly pIconColorsLight As New Dictionary(Of String, Cairo.Color)

        Shared Sub New()
            Try
                Dim lAdd = Sub(vKey As String, vDarkHex As String, vLightHex As String)
                               pIconColorsDark(vKey) = HexToCairoColor(vDarkHex)
                               pIconColorsLight(vKey) = HexToCairoColor(vLightHex)
                           End Sub

                ' Mirrors CustomDrawObjectExplorer's fallback icon palette for visual
                ' consistency between the Object Explorer tree and these dropdowns
                lAdd("Class", "#4EC9B0", "#2B91AF")
                lAdd("Module", "#4CC9F0", "#4361EE")
                lAdd("Interface", "#B8D7A3", "#6B8E23")
                lAdd("Structure", "#7209B7", "#560BAD")
                lAdd("Enum", "#F72585", "#B5179E")
                lAdd("Method", "#DCDCAA", "#795E26")
                lAdd("Function", "#DCDCAA", "#795E26")
                lAdd("Property", "#9CDCFE", "#0070C0")
                lAdd("Field", "#51CF66", "#2B8A3E")
                lAdd("Event", "#CE9178", "#A31515")
                lAdd("Constructor", "#4CC9F0", "#4361EE")
                lAdd("Default", "#808080", "#606060")

            Catch ex As Exception
                Console.WriteLine($"NavigationDropdownPopup palette init error: {ex.Message}")
            End Try
        End Sub

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
                AddHandler Me.ButtonPressEvent, AddressOf OnOutsideButtonPress

            Catch ex As Exception
                Console.WriteLine($"NavigationDropdownPopup constructor error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the theme manager used for background/foreground/selection colors
        ''' </summary>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            pThemeManager = vThemeManager
        End Sub

        ''' <summary>
        ''' Shows the popup below vTrigger, populated with vItems, with the item matching
        ''' vInitialTag (if any) initially highlighted
        ''' </summary>
        Public Sub ShowFor(vTrigger As Widget, vItems As List(Of Item), vInitialTag As Object)
            Try
                pItems = If(vItems, New List(Of Item))
                pScrollOffset = 0

                pHighlightedIndex = pItems.FindIndex(Function(i) i.Tag Is vInitialTag)
                If pHighlightedIndex < 0 Then pHighlightedIndex = 0
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
                Gtk.Grab.Add(Me)

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
                Gtk.Grab.Remove(Me)
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
                        lContext.SelectFontFace("monospace", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal)
                        lContext.SetFontSize(12)
                        for each lItem in pItems
                            Dim lRowLeft As Integer = RowContentLeft(lItem)
                            Dim lExtents As Cairo.TextExtents = lContext.TextExtents(lItem.Text)
                            Dim lRowWidth As Integer = lRowLeft + CInt(lExtents.Width) + TEXT_PADDING_RIGHT
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

        ''' <summary>Left edge (in pixels) where a row's icon begins, after tree indentation</summary>
        Private Function RowContentLeft(vItem As Item) As Integer
            Return TEXT_PADDING_LEFT + vItem.IndentLevel * INDENT_WIDTH + ICON_SIZE + ICON_SPACING
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
            Dim lTag As Object = pItems(pHighlightedIndex).Tag
            Gtk.Grab.Remove(Me)
            Hide()
            RaiseEvent ItemSelected(lTag)
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

        ''' <summary>
        ''' Fires only for a button-press whose natural target is outside this window's own
        ''' hierarchy, redirected here by the Gtk.Grab.Add held while the popup is open (see
        ''' class remarks) - a click on pDrawingArea itself is dispatched there directly and
        ''' never reaches this handler
        ''' </summary>
        Private Sub OnOutsideButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
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

        Private Function IsDarkTheme() As Boolean
            Return pThemeManager Is Nothing OrElse pThemeManager.GetCurrentThemeObject() Is Nothing OrElse
                   pThemeManager.GetCurrentThemeObject().IsDarkTheme
        End Function

        Private Sub OnDraw(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                Dim lWidth As Integer = pDrawingArea.AllocatedWidth
                Dim lHeight As Integer = pDrawingArea.AllocatedHeight
                Dim lTheme As EditorTheme = pThemeManager?.GetCurrentThemeObject()

                Dim lBgHex As String = If(lTheme?.BackgroundColor, "#1E1E1E")
                Dim lBgColor As Cairo.Color = HexToCairoColor(lBgHex)
                Dim lFgColor As Cairo.Color = HexToCairoColor(If(lTheme?.ForegroundColor, "#D4D4D4"))
                Dim lSelColor As Cairo.Color = HexToCairoColor(If(lTheme?.SelectionColor, "#264F78"))
                Dim lHoverColor As Cairo.Color = HexToCairoColor(If(lTheme?.CurrentLineColor, "#2A2D2E"))
                Dim lLineColor As Cairo.Color = HexToCairoColor(If(IsDarkTheme(), "#5A5A5A", "#B0B0B0"))

                ' Raised-bevel edges (light top/left, dark bottom/right), matching the same
                ' theme BevelLightColor/BevelDarkColor override convention as the rest of
                ' the CustomDraw* control library - auto-derived off the background color
                ' when not explicitly overridden
                Dim lLightEdgeColor As Cairo.Color = HexToCairoColor(
                    If(String.IsNullOrEmpty(lTheme?.BevelLightColor), LightenColor(lBgHex, 0.30), lTheme.BevelLightColor))
                Dim lDarkEdgeColor As Cairo.Color = HexToCairoColor(
                    If(String.IsNullOrEmpty(lTheme?.BevelDarkColor), DarkenColor(lBgHex, 0.30), lTheme.BevelDarkColor))

                ' Background
                lContext.SetSourceRGB(lBgColor.R, lBgColor.G, lBgColor.B)
                lContext.Rectangle(0, 0, lWidth, lHeight)
                lContext.Fill()

                ' Rows
                Dim lVisibleCount As Integer = Math.Min(pItems.Count - pScrollOffset, MAX_VISIBLE_ITEMS)
                for i As Integer = 0 To lVisibleCount - 1
                    Dim lIndex As Integer = pScrollOffset + i
                    Dim lItem As Item = pItems(lIndex)
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

                    DrawTreeLines(lContext, lItem, lY, lLineColor)

                    Dim lIconX As Integer = TEXT_PADDING_LEFT + lItem.IndentLevel * INDENT_WIDTH
                    DrawItemIcon(lContext, lItem, lIconX, lY)

                    lContext.SetSourceRGB(lFgColor.R, lFgColor.G, lFgColor.B)
                    lContext.SelectFontFace("monospace", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal)
                    lContext.SetFontSize(12)
                    Dim lTextX As Integer = RowContentLeft(lItem)
                    Dim lExtents As Cairo.TextExtents = lContext.TextExtents(lItem.Text)
                    Dim lTextY As Integer = lY + CInt((ROW_HEIGHT + lExtents.Height) / 2)
                    lContext.MoveTo(lTextX, lTextY)
                    lContext.ShowText(lItem.Text)
                Next

                ' Raised bevel border (light top/left, dark bottom/right) instead of a flat
                ' single-color outline, matching the rest of the CustomDraw* control library
                Const BEVEL_WIDTH As Integer = 2
                lContext.SetSourceRGB(lLightEdgeColor.R, lLightEdgeColor.G, lLightEdgeColor.B)
                lContext.Rectangle(0, 0, lWidth, BEVEL_WIDTH)                       ' top
                lContext.Fill()
                lContext.Rectangle(0, 0, BEVEL_WIDTH, lHeight)                      ' left
                lContext.Fill()

                lContext.SetSourceRGB(lDarkEdgeColor.R, lDarkEdgeColor.G, lDarkEdgeColor.B)
                lContext.Rectangle(0, lHeight - BEVEL_WIDTH, lWidth, BEVEL_WIDTH)    ' bottom
                lContext.Fill()
                lContext.Rectangle(lWidth - BEVEL_WIDTH, 0, BEVEL_WIDTH, lHeight)    ' right
                lContext.Fill()

                ' Custom scrollbar (no GTK step-arrow buttons)
                If pItems.Count > MAX_VISIBLE_ITEMS Then
                    DrawScrollbar(lContext, lWidth, lHeight, lLineColor)
                End If

            Catch ex As Exception
                Console.WriteLine($"NavigationDropdownPopup.OnDraw error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws the tree connector lines for a Class-dropdown item: a continuing vertical
        ''' bar for each ancestor level that still has later siblings, and a final
        ''' "├──"/"└──" style connector into the item itself
        ''' </summary>
        Private Sub DrawTreeLines(vContext As Cairo.Context, vItem As Item, vRowY As Integer, vLineColor As Cairo.Color)
            If vItem.IndentLevel = 0 Then Return

            vContext.SetSourceRGB(vLineColor.R, vLineColor.G, vLineColor.B)
            vContext.LineWidth = 1

            Dim lMidY As Integer = vRowY + ROW_HEIGHT \ 2

            for lLevel As Integer = 0 To vItem.IndentLevel - 1
                Dim lColumnX As Integer = TEXT_PADDING_LEFT + lLevel * INDENT_WIDTH + INDENT_WIDTH \ 2
                Dim lIsFinalLevel As Boolean = (lLevel = vItem.IndentLevel - 1)

                If Not lIsFinalLevel Then
                    ' An earlier ancestor's column - draw a full-height continuation bar
                    ' only if that ancestor still has more siblings coming after it
                    If lLevel < vItem.AncestorContinues.Count AndAlso vItem.AncestorContinues(lLevel) Then
                        vContext.MoveTo(lColumnX, vRowY)
                        vContext.LineTo(lColumnX, vRowY + ROW_HEIGHT)
                        vContext.Stroke()
                    End If
                Else
                    ' This item's own connector: vertical from top to mid, then a stub
                    ' continuing to the bottom too if this item is NOT the last sibling
                    ' (so the bar continues down to the next sibling), plus the
                    ' horizontal branch into the item
                    vContext.MoveTo(lColumnX, vRowY)
                    vContext.LineTo(lColumnX, lMidY)
                    vContext.Stroke()

                    If Not vItem.IsLastSibling Then
                        vContext.MoveTo(lColumnX, lMidY)
                        vContext.LineTo(lColumnX, vRowY + ROW_HEIGHT)
                        vContext.Stroke()
                    End If

                    vContext.MoveTo(lColumnX, lMidY)
                    vContext.LineTo(lColumnX + INDENT_WIDTH \ 2, lMidY)
                    vContext.Stroke()
                End If
            Next
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

        ''' <summary>
        ''' Draws a colored shape icon for an item, matching the palette/shapes used by
        ''' CustomDrawObjectExplorer's fallback icons for visual consistency
        ''' </summary>
        Private Sub DrawItemIcon(vContext As Cairo.Context, vItem As Item, vX As Integer, vRowY As Integer)
            Try
                Dim lKey As String
                If vItem.IsClassItem Then
                    lKey = vItem.ObjectType.ToString().Substring(1) ' eClass -> Class
                Else
                    lKey = vItem.MemberType.ToString().Substring(1)
                End If

                Dim lPalette As Dictionary(Of String, Cairo.Color) = If(IsDarkTheme(), pIconColorsDark, pIconColorsLight)
                Dim lColor As Cairo.Color = Nothing
                If Not lPalette.TryGetValue(lKey, lColor) Then
                    lColor = lPalette("Default")
                End If

                Dim lCenterY As Integer = vRowY + ROW_HEIGHT \ 2
                Dim lCenterX As Integer = vX + ICON_SIZE \ 2
                Dim lRadius As Integer = ICON_SIZE \ 3

                vContext.SetSourceRGB(lColor.R, lColor.G, lColor.B)

                If vItem.IsClassItem Then
                    Select Case vItem.ObjectType
                        Case CodeObjectType.eInterface
                            vContext.MoveTo(lCenterX, lCenterY - lRadius)
                            vContext.LineTo(lCenterX + lRadius, lCenterY)
                            vContext.LineTo(lCenterX, lCenterY + lRadius)
                            vContext.LineTo(lCenterX - lRadius, lCenterY)
                            vContext.ClosePath()
                            vContext.Fill()
                        Case CodeObjectType.eEnum
                            for i As Integer = -1 To 1
                                vContext.Rectangle(vX + 3, lCenterY + i * 5 - 1, ICON_SIZE - 6, 2)
                            Next
                            vContext.Fill()
                        Case CodeObjectType.eStructure
                            vContext.Rectangle(lCenterX - lRadius, lCenterY - lRadius, lRadius * 2, lRadius * 2)
                            vContext.Fill()
                            vContext.SetSourceRGB(1, 1, 1)
                            vContext.LineWidth = 1
                            vContext.MoveTo(lCenterX, lCenterY - lRadius)
                            vContext.LineTo(lCenterX, lCenterY + lRadius)
                            vContext.MoveTo(lCenterX - lRadius, lCenterY)
                            vContext.LineTo(lCenterX + lRadius, lCenterY)
                            vContext.Stroke()
                        Case Else ' eClass, eModule
                            Dim lSize As Integer = lRadius * 2 - 2
                            vContext.Rectangle(lCenterX - lRadius + 1, lCenterY - lRadius + 1, lSize, lSize)
                            vContext.Fill()
                    End Select
                Else
                    Select Case vItem.MemberType
                        Case CodeMemberType.eMethod, CodeMemberType.eFunction, CodeMemberType.eConstructor
                            vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                            vContext.Fill()
                            vContext.SetSourceRGB(1, 1, 1)
                            vContext.Rectangle(lCenterX - 2, lCenterY - 2, 4, 4)
                            vContext.Fill()
                        Case CodeMemberType.eProperty, CodeMemberType.eField
                            Dim lSmallSize As Integer = lRadius * 3 \ 2
                            vContext.Rectangle(lCenterX - lSmallSize \ 2, lCenterY - lSmallSize \ 2, lSmallSize, lSmallSize)
                            vContext.Fill()
                        Case CodeMemberType.eEvent
                            vContext.MoveTo(lCenterX + 2, lCenterY - lRadius)
                            vContext.LineTo(lCenterX - 2, lCenterY)
                            vContext.LineTo(lCenterX, lCenterY)
                            vContext.LineTo(lCenterX - 2, lCenterY + lRadius)
                            vContext.Stroke()
                        Case Else
                            vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                            vContext.Fill()
                    End Select
                End If

            Catch ex As Exception
                Console.WriteLine($"DrawItemIcon error: {ex.Message}")
            End Try
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

        Private Shared Function LightenColor(vHexColor As String, vAmount As Double) As String
            Try
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor
                Dim lR As Double = Math.Min(1.0, lColor.Red + vAmount)
                Dim lG As Double = Math.Min(1.0, lColor.Green + vAmount)
                Dim lB As Double = Math.Min(1.0, lColor.Blue + vAmount)
                Return $"#{CInt(lR * 255):X2}{CInt(lG * 255):X2}{CInt(lB * 255):X2}"
            Catch ex As Exception
                Console.WriteLine($"LightenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

        Private Shared Function DarkenColor(vHexColor As String, vAmount As Double) As String
            Try
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor
                Dim lR As Double = Math.Max(0.0, lColor.Red - vAmount)
                Dim lG As Double = Math.Max(0.0, lColor.Green - vAmount)
                Dim lB As Double = Math.Max(0.0, lColor.Blue - vAmount)
                Return $"#{CInt(lR * 255):X2}{CInt(lG * 255):X2}{CInt(lB * 255):X2}"
            Catch ex As Exception
                Console.WriteLine($"DarkenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

    End Class

End Namespace
