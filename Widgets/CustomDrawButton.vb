' Widgets/CustomDrawButton.vb - Custom-drawn button with a retro (AmigaOS Workbench-style)
' 3D bevel: raised (light top/left, dark bottom/right) when not pressed, inverted when
' pressed or (for CustomDrawToggleButton) toggled on
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Runtime.InteropServices
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' A custom-drawn push button rendered with a retro 3D bevel instead of the native GTK
    ''' button theme
    ''' </summary>
    Public Class CustomDrawButton
        Inherits DrawingArea

        ''' <summary>
        ''' Visual style for a CustomDrawButton
        ''' </summary>
        Public Enum eButtonStyle
            ''' <summary>Unknown or unspecified style</summary>
            eUnspecified
            ''' <summary>Retro AmigaOS Workbench raised/pressed-in 3D bevel (default)</summary>
            eBevel
            ''' <summary>Thin flat outlined face with no 3D bevel edges</summary>
            eFlat
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum

        ' ===== Private Fields =====
        Protected pLabel As String = ""

        ''' <summary>
        ''' The icon actually drawn - either pSourceIconPixbuf as-is, or a contrast-inverted
        ''' copy of it, depending on UpdateDisplayIcon's most recent verdict
        ''' </summary>
        Protected pIconPixbuf As Gdk.Pixbuf

        ''' <summary>
        ''' The icon exactly as supplied via the IconPixbuf property/constructor, before any
        ''' contrast adjustment - what IconPixbuf's getter returns
        ''' </summary>
        Private pSourceIconPixbuf As Gdk.Pixbuf

        ''' <summary>
        ''' Cached average-luminance verdict for pSourceIconPixbuf (True = predominantly dark),
        ''' recomputed only when the source icon itself changes, not on every theme change
        ''' </summary>
        Private pSourceIconIsDark As Boolean

        ''' <summary>
        ''' IsDarkTheme of whichever EditorTheme most recently ran through ApplyThemeColors -
        ''' set from there (not re-queried from pThemeManager) so this stays correct for both
        ''' the live ThemeManager path and the ApplyExplicitTheme preview path, which
        ''' deliberately applies a theme that may differ from ThemeManager's actual current one
        ''' </summary>
        Private pBackgroundIsDark As Boolean = False

        Protected pIsPressed As Boolean = False
        Protected pIsHovering As Boolean = False
        Protected pThemeManager As ThemeManager
        Protected pStyle As eButtonStyle = eButtonStyle.eBevel
        Protected pShowDropdownArrow As Boolean = False

        Protected pFillColor As String = "#C0C0C0"
        Protected pTextColor As String = "#000000"
        Protected pLightEdgeColor As String = "#FFFFFF"
        Protected pDarkEdgeColor As String = "#000000"

        Private Const BEVEL_WIDTH As Integer = 2
        Private Const FLAT_BORDER_WIDTH As Integer = 1
        Private Const HORIZONTAL_PADDING As Integer = 10
        ' Same value as HORIZONTAL_PADDING so an icon-only button (no label) comes out
        ' exactly square - width and height both end up icon-size + 2*padding
        Private Const VERTICAL_PADDING As Integer = 10
        Private Const ICON_TEXT_GAP As Integer = 6
        Private Const MIN_BUTTON_HEIGHT As Integer = 26
        Private Const DROPDOWN_ARROW_AREA_WIDTH As Integer = 18

        ' ===== Events =====
        Public Event Clicked(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

        ''' <summary>
        ''' Gets or sets the visual style (raised bevel vs. thin flat outline)
        ''' </summary>
        Public Property Style As eButtonStyle
            Get
                Return pStyle
            End Get
            Set(value As eButtonStyle)
                pStyle = value
                QueueDraw()
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the button's text label
        ''' </summary>
        Public Property Label As String
            Get
                Return pLabel
            End Get
            Set(value As String)
                pLabel = value
                UpdateRequestedSize()
                QueueDraw()
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets an optional icon drawn before the label - automatically contrast-
        ''' adjusted (inverted) against the current theme's background if the icon's own
        ''' predominant color would otherwise blend into it (e.g. a dark line-art icon on a
        ''' dark theme)
        ''' </summary>
        Public Property IconPixbuf As Gdk.Pixbuf
            Get
                Return pSourceIconPixbuf
            End Get
            Set(value As Gdk.Pixbuf)
                pSourceIconPixbuf = value
                pSourceIconIsDark = ComputeIconIsDark(value)
                UpdateDisplayIcon()
                UpdateRequestedSize()
                QueueDraw()
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets whether a small vector-drawn dropdown-indicator triangle is drawn
        ''' after the label, in a reserved area on the right - for combo/dropdown-trigger
        ''' style buttons (e.g. NavigationDropdowns' Class/Member triggers). Drawn as a
        ''' filled Cairo triangle rather than a "▾" text glyph, matching CustomDrawComboBox's
        ''' own arrow - raw Cairo ShowText has no font-fallback chain, so Unicode arrow
        ''' glyphs render as blank tofu boxes under "Sans" (the same failure already hit and
        ''' fixed for CustomDrawNotebook's nav buttons)
        ''' </summary>
        Public Property ShowDropdownArrow As Boolean
            Get
                Return pShowDropdownArrow
            End Get
            Set(value As Boolean)
                pShowDropdownArrow = value
                UpdateRequestedSize()
                QueueDraw()
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the ThemeManager used to color the button's face
        ''' </summary>
        Public Property ThemeManager As ThemeManager
            Get
                Return pThemeManager
            End Get
            Set(value As ThemeManager)
                If pThemeManager IsNot Nothing Then
                    RemoveHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                pThemeManager = value
                If pThemeManager IsNot Nothing Then
                    AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                ApplyCurrentTheme()
            End Set
        End Property

        ' ===== Constructor =====

        Public Sub New(Optional vLabel As String = "", Optional vIcon As Gdk.Pixbuf = Nothing)
            MyBase.New()
            Try
                pLabel = vLabel
                pSourceIconPixbuf = vIcon
                pSourceIconIsDark = ComputeIconIsDark(vIcon)
                pIconPixbuf = vIcon ' displayed as-is until a ThemeManager is assigned and can decide whether inversion is needed
                CanFocus = True
                Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                         EventMask.EnterNotifyMask Or EventMask.LeaveNotifyMask Or
                         EventMask.PointerMotionMask

                UpdateRequestedSize()

                AddHandler Me.Drawn, AddressOf OnCustomDraw
                AddHandler Me.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler Me.ButtonReleaseEvent, AddressOf OnButtonRelease
                AddHandler Me.EnterNotifyEvent, AddressOf OnEnterNotify
                AddHandler Me.LeaveNotifyEvent, AddressOf OnLeaveNotify

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.New error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Recomputes the widget's requested size from its current label/icon - an
        ''' approximation (not full Pango measurement) that's adequate for short static
        ''' toolbar labels
        ''' </summary>
        Protected Sub UpdateRequestedSize()
            Try
                Dim lWidth As Integer = HORIZONTAL_PADDING * 2
                Dim lIconHeight As Integer = 0
                If pIconPixbuf IsNot Nothing Then
                    lWidth += pIconPixbuf.Width + ICON_TEXT_GAP
                    lIconHeight = pIconPixbuf.Height
                End If
                If Not String.IsNullOrEmpty(pLabel) Then
                    lWidth += CInt(pLabel.Length * 7.5)
                End If
                If pShowDropdownArrow Then
                    lWidth += DROPDOWN_ARROW_AREA_WIDTH
                End If

                ' Scale height with the icon's own pixel size (rather than a fixed value) so
                ' toggling Toolbar Button Size between small/large actually makes buttons
                ' taller too, not just wider - an icon-only button comes out perfectly square
                Dim lHeight As Integer = Math.Max(lIconHeight + VERTICAL_PADDING * 2, MIN_BUTTON_HEIGHT)

                SetSizeRequest(Math.Max(lWidth, 28), lHeight)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.UpdateRequestedSize error: {ex.Message}")
            End Try
        End Sub

        ' ===== Theme =====

        Private Sub OnThemeChanged(vTheme As EditorTheme)
            ApplyCurrentTheme()
        End Sub

        Private Sub ApplyCurrentTheme()
            Try
                If pThemeManager Is Nothing Then Return
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                ApplyThemeColors(lTheme)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Applies colors from a specific EditorTheme directly, bypassing ThemeManager -
        ''' for previewing a theme (e.g. one being edited in ThemeEditor) that isn't
        ''' necessarily the application's currently-active theme, matching the same
        ''' demo/preview pattern CustomDrawingEditor.SetThemeColors already uses
        ''' </summary>
        ''' <param name="vTheme">Theme to preview - has no lasting effect on ThemeManager</param>
        Public Sub ApplyExplicitTheme(vTheme As EditorTheme)
            ApplyThemeColors(vTheme)
        End Sub

        Private Sub ApplyThemeColors(vTheme As EditorTheme)
            Try
                If vTheme Is Nothing Then Return

                pBackgroundIsDark = vTheme.IsDarkTheme
                pFillColor = vTheme.LineNumberBackgroundColor
                pTextColor = vTheme.ForegroundColor

                ' Bevel edges: use the theme's explicit override if set, otherwise derive
                ' relative to the face color (lightened/darkened by a fixed amount) rather
                ' than fixed white/black, so the highlight and shadow edges both stay
                ' visible whether the theme's face color itself lands near the light or
                ' dark end of the range. A theme with an already-extreme face color (e.g.
                ' Solarized Dark/Light) can set BevelLightColor/BevelDarkColor explicitly
                ' to override the auto-derived value that loses contrast on one edge.
                pLightEdgeColor = If(String.IsNullOrEmpty(vTheme.BevelLightColor), LightenColor(pFillColor, 0.30), vTheme.BevelLightColor)
                pDarkEdgeColor = If(String.IsNullOrEmpty(vTheme.BevelDarkColor), DarkenColor(pFillColor, 0.30), vTheme.BevelDarkColor)

                UpdateDisplayIcon()
                QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.ApplyThemeColors error: {ex.Message}")
            End Try
        End Sub

        ' ===== Icon Contrast =====

        ''' <summary>
        ''' Recomputes pIconPixbuf (the icon actually drawn) from pSourceIconPixbuf, inverting
        ''' its colors when the icon's own predominant luminance matches the current theme
        ''' background's - i.e. a dark icon on a dark background, or a light icon on a light
        ''' background - so the icon stays visible without every icon needing a hand-authored
        ''' dark-theme counterpart
        ''' </summary>
        Private Sub UpdateDisplayIcon()
            Try
                If pSourceIconPixbuf Is Nothing Then
                    pIconPixbuf = Nothing
                    Return
                End If

                If pSourceIconIsDark = pBackgroundIsDark Then
                    pIconPixbuf = InvertPixbufColors(pSourceIconPixbuf)
                Else
                    pIconPixbuf = pSourceIconPixbuf
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.UpdateDisplayIcon error: {ex.Message}")
                pIconPixbuf = pSourceIconPixbuf
            End Try
        End Sub

        ''' <summary>
        ''' Samples a pixbuf's opaque pixels and reports whether its average luminance is
        ''' predominantly dark - used to decide whether an icon needs contrast-inverting
        ''' against the current theme background
        ''' </summary>
        ''' <param name="vPixbuf">Icon to sample - Nothing/fully-transparent returns False</param>
        ''' <returns>True if the icon's average luminance is dark (below the midpoint)</returns>
        Private Function ComputeIconIsDark(vPixbuf As Gdk.Pixbuf) As Boolean
            Try
                If vPixbuf Is Nothing Then Return False

                Dim lChannels As Integer = vPixbuf.NChannels
                Dim lHasAlpha As Boolean = vPixbuf.HasAlpha
                Dim lRowstride As Integer = vPixbuf.Rowstride
                Dim lWidth As Integer = vPixbuf.Width
                Dim lHeight As Integer = vPixbuf.Height
                Dim lLength As Integer = lRowstride * lHeight
                If lLength <= 0 Then Return False

                Dim lBytes(lLength - 1) As Byte
                Marshal.Copy(vPixbuf.Pixels, lBytes, 0, lLength)

                Dim lLuminanceSum As Double = 0
                Dim lOpaquePixelCount As Integer = 0

                For lY As Integer = 0 To lHeight - 1
                    Dim lRowStart As Integer = lY * lRowstride
                    For lX As Integer = 0 To lWidth - 1
                        Dim lOffset As Integer = lRowStart + lX * lChannels
                        If lHasAlpha AndAlso lBytes(lOffset + 3) < 32 Then Continue For ' skip near-transparent pixels
                        Dim lR As Byte = lBytes(lOffset)
                        Dim lG As Byte = lBytes(lOffset + 1)
                        Dim lB As Byte = lBytes(lOffset + 2)
                        lLuminanceSum += (0.299 * lR + 0.587 * lG + 0.114 * lB)
                        lOpaquePixelCount += 1
                    Next
                Next

                If lOpaquePixelCount = 0 Then Return False ' fully transparent icon - nothing to judge

                Dim lAverageLuminance As Double = lLuminanceSum / lOpaquePixelCount ' 0 (black) .. 255 (white)
                Return lAverageLuminance < 128.0

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.ComputeIconIsDark error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Returns a copy of a pixbuf with its RGB channels inverted (255-value), leaving
        ''' alpha untouched - turns dark line-art into light line-art (and vice versa) while
        ''' keeping transparent background pixels transparent
        ''' </summary>
        Private Function InvertPixbufColors(vPixbuf As Gdk.Pixbuf) As Gdk.Pixbuf
            Try
                Dim lCopy As Gdk.Pixbuf = vPixbuf.Copy()
                Dim lChannels As Integer = lCopy.NChannels
                Dim lHasAlpha As Boolean = lCopy.HasAlpha
                Dim lRowstride As Integer = lCopy.Rowstride
                Dim lWidth As Integer = lCopy.Width
                Dim lHeight As Integer = lCopy.Height
                Dim lLength As Integer = lRowstride * lHeight
                If lLength <= 0 Then Return lCopy

                Dim lBytes(lLength - 1) As Byte
                Marshal.Copy(lCopy.Pixels, lBytes, 0, lLength)

                For lY As Integer = 0 To lHeight - 1
                    Dim lRowStart As Integer = lY * lRowstride
                    For lX As Integer = 0 To lWidth - 1
                        Dim lOffset As Integer = lRowStart + lX * lChannels
                        If lHasAlpha AndAlso lBytes(lOffset + 3) = 0 Then Continue For ' nothing visible to invert
                        lBytes(lOffset) = CByte(255 - lBytes(lOffset))
                        lBytes(lOffset + 1) = CByte(255 - lBytes(lOffset + 1))
                        lBytes(lOffset + 2) = CByte(255 - lBytes(lOffset + 2))
                    Next
                Next

                Marshal.Copy(lBytes, 0, lCopy.Pixels, lLength)
                Return lCopy

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.InvertPixbufColors error: {ex.Message}")
                Return vPixbuf
            End Try
        End Function

        ' ===== Drawing =====

        ''' <summary>
        ''' Whether the button should currently render with its bevel inverted - overridden
        ''' by CustomDrawToggleButton so an active toggle stays permanently "pressed in"
        ''' </summary>
        Protected Overridable Function IsVisuallyPressed() As Boolean
            Return pIsPressed
        End Function

        Private Function OnCustomDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawButton(vArgs.Cr)
                Return True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.OnCustomDraw error: {ex.Message}")
                Return True
            End Try
        End Function

        Private Sub DrawButton(vContext As Context)
            Try
                Dim lWidth As Integer = AllocatedWidth
                Dim lHeight As Integer = AllocatedHeight
                Dim lPressed As Boolean = IsVisuallyPressed()

                ' Face fill - slightly lightened on hover for basic mouse feedback, and (for
                ' the flat style, which has no bevel to invert) darkened instead while pressed
                ' so a click still gives visual feedback
                Dim lFace As String = pFillColor
                If pStyle = eButtonStyle.eFlat AndAlso lPressed Then
                    lFace = DarkenColor(lFace, 0.12)
                ElseIf pIsHovering AndAlso Sensitive Then
                    lFace = LightenColor(lFace, 0.08)
                End If
                SetSourceColor(vContext, lFace)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

                If pStyle = eButtonStyle.eFlat Then
                    ' Thin flat outline - no 3D bevel edges
                    SetSourceColor(vContext, pDarkEdgeColor)
                    vContext.LineWidth = FLAT_BORDER_WIDTH
                    vContext.Rectangle(0.5, 0.5, lWidth - 1, lHeight - 1)
                    vContext.Stroke()
                Else
                    ' Bevel - light top/left + dark bottom/right when raised, swapped when pressed
                    Dim lTopLeftColor As String = If(lPressed, pDarkEdgeColor, pLightEdgeColor)
                    Dim lBottomRightColor As String = If(lPressed, pLightEdgeColor, pDarkEdgeColor)

                    SetSourceColor(vContext, lTopLeftColor)
                    vContext.Rectangle(0, 0, lWidth, BEVEL_WIDTH)                  ' top
                    vContext.Fill()
                    vContext.Rectangle(0, 0, BEVEL_WIDTH, lHeight)                 ' left
                    vContext.Fill()

                    SetSourceColor(vContext, lBottomRightColor)
                    vContext.Rectangle(0, lHeight - BEVEL_WIDTH, lWidth, BEVEL_WIDTH)      ' bottom
                    vContext.Fill()
                    vContext.Rectangle(lWidth - BEVEL_WIDTH, 0, BEVEL_WIDTH, lHeight)      ' right
                    vContext.Fill()
                End If

                ' Icon/label content - nudged 1px down+right while pressed to reinforce
                ' the "pushed in" feel
                Dim lContentOffset As Integer = If(lPressed, 1, 0)
                Dim lX As Integer = HORIZONTAL_PADDING + lContentOffset

                If pIconPixbuf IsNot Nothing Then
                    Dim lIconY As Integer = (lHeight - pIconPixbuf.Height) \ 2 + lContentOffset
                    vContext.Save()
                    Gdk.CairoHelper.SetSourcePixbuf(vContext, pIconPixbuf, lX, lIconY)
                    If Sensitive Then
                        vContext.Paint()
                    Else
                        ' Dim the icon so a disabled button reads as disabled - native
                        ' Gtk.ToolButton does this automatically for its stock/themed icons,
                        ' but a raw Cairo-painted pixbuf needs it done explicitly
                        vContext.PaintWithAlpha(0.35)
                    End If
                    vContext.Restore()
                    lX += pIconPixbuf.Width + ICON_TEXT_GAP
                End If

                If Not String.IsNullOrEmpty(pLabel) Then
                    SetSourceColor(vContext, If(Sensitive, pTextColor, LightenColor(pTextColor, 0.4)))
                    vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal)
                    vContext.SetFontSize(11)
                    Dim lExtents As TextExtents = vContext.TextExtents(pLabel)
                    Dim lTextY As Integer = (lHeight + CInt(lExtents.Height)) \ 2 + lContentOffset
                    vContext.MoveTo(lX, lTextY)
                    vContext.ShowText(pLabel)
                End If

                If pShowDropdownArrow Then
                    DrawDropdownArrow(vContext, lWidth, lHeight, lContentOffset)
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.DrawButton error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws a small filled triangle in the reserved right-hand area, indicating this
        ''' button opens a dropdown - a vector shape rather than a "▾" text glyph, matching
        ''' CustomDrawComboBox's own arrow (see ShowDropdownArrow's remarks for why)
        ''' </summary>
        Private Sub DrawDropdownArrow(vContext As Context, vWidth As Integer, vHeight As Integer, vContentOffset As Integer)
            Try
                SetSourceColor(vContext, If(Sensitive, pTextColor, LightenColor(pTextColor, 0.4)))

                Dim lArrowCenterX As Double = vWidth - (DROPDOWN_ARROW_AREA_WIDTH / 2.0) + vContentOffset
                Dim lArrowCenterY As Double = vHeight / 2.0 + vContentOffset
                Const ARROW_HALF_WIDTH As Double = 4
                Const ARROW_HEIGHT As Double = 3
                vContext.MoveTo(lArrowCenterX - ARROW_HALF_WIDTH, lArrowCenterY - ARROW_HEIGHT / 2)
                vContext.LineTo(lArrowCenterX + ARROW_HALF_WIDTH, lArrowCenterY - ARROW_HEIGHT / 2)
                vContext.LineTo(lArrowCenterX, lArrowCenterY + ARROW_HEIGHT)
                vContext.ClosePath()
                vContext.Fill()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.DrawDropdownArrow error: {ex.Message}")
            End Try
        End Sub

        ' ===== Mouse Handling =====

        Private Sub OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                If Not Sensitive Then Return
                GrabFocus()
                If vArgs.Event.Button = 1 Then
                    pIsPressed = True
                    QueueDraw()
                End If
                vArgs.RetVal = True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.OnButtonPress error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs)
            Try
                If Not Sensitive Then Return
                If vArgs.Event.Button = 1 AndAlso pIsPressed Then
                    pIsPressed = False
                    QueueDraw()
                    If pIsHovering Then
                        FireClicked()
                    End If
                End If
                vArgs.RetVal = True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.OnButtonRelease error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnEnterNotify(vSender As Object, vArgs As EnterNotifyEventArgs)
            pIsHovering = True
            QueueDraw()
        End Sub

        Private Sub OnLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            pIsHovering = False
            If pIsPressed Then
                pIsPressed = False
            End If
            QueueDraw()
        End Sub

        ''' <summary>
        ''' Raises Clicked - overridden by CustomDrawToggleButton to flip Active instead
        ''' </summary>
        Protected Overridable Sub FireClicked()
            RaiseEvent Clicked(Me, EventArgs.Empty)
        End Sub

        ' ===== Helpers =====

        Protected Sub SetSourceColor(vContext As Context, vHexColor As String)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHexColor) Then
                    vContext.SetSourceRGBA(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.SetSourceColor error: {ex.Message}")
            End Try
        End Sub

        Protected Function LightenColor(vHexColor As String, vAmount As Double) As String
            Try
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor

                Dim lR As Double = Math.Min(1.0, lColor.Red + vAmount)
                Dim lG As Double = Math.Min(1.0, lColor.Green + vAmount)
                Dim lB As Double = Math.Min(1.0, lColor.Blue + vAmount)

                Return $"#{CInt(lR * 255):X2}{CInt(lG * 255):X2}{CInt(lB * 255):X2}"
            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.LightenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

        Protected Function DarkenColor(vHexColor As String, vAmount As Double) As String
            Try
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor

                Dim lR As Double = Math.Max(0.0, lColor.Red - vAmount)
                Dim lG As Double = Math.Max(0.0, lColor.Green - vAmount)
                Dim lB As Double = Math.Max(0.0, lColor.Blue - vAmount)

                Return $"#{CInt(lR * 255):X2}{CInt(lG * 255):X2}{CInt(lB * 255):X2}"
            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.DarkenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

    End Class

    ''' <summary>
    ''' A CustomDrawButton with persistent on/off state - stays rendered "pressed in"
    ''' (inverted bevel) whenever Active is True, matching classic Workbench-style toggle
    ''' gadgets
    ''' </summary>
    Public Class CustomDrawToggleButton
        Inherits CustomDrawButton

        Private pActive As Boolean = False

        Public Event Toggled(vSender As Object, vArgs As EventArgs)

        ''' <summary>
        ''' Gets or sets whether the toggle is currently on
        ''' </summary>
        Public Property Active As Boolean
            Get
                Return pActive
            End Get
            Set(value As Boolean)
                If pActive <> value Then
                    pActive = value
                    QueueDraw()
                    RaiseEvent Toggled(Me, EventArgs.Empty)
                End If
            End Set
        End Property

        Public Sub New(Optional vLabel As String = "", Optional vIcon As Gdk.Pixbuf = Nothing)
            MyBase.New(vLabel, vIcon)
        End Sub

        Protected Overrides Function IsVisuallyPressed() As Boolean
            Return pActive OrElse MyBase.IsVisuallyPressed()
        End Function

        ''' <summary>
        ''' A click flips Active (which raises Toggled) - matching native Gtk.ToggleButton,
        ''' which raises BOTH Clicked and Toggled on a real click, this also still raises
        ''' Clicked via the base implementation, so code written against a plain
        ''' AddHandler ...Clicked... (as if this were a CustomDrawButton) keeps working
        ''' </summary>
        Protected Overrides Sub FireClicked()
            Active = Not Active
            MyBase.FireClicked()
        End Sub

    End Class

End Namespace
