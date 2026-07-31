' Widgets/CustomDrawButton.vb - Custom-drawn button with a retro (AmigaOS Workbench-style)
' 3D bevel: raised (light top/left, dark bottom/right) when not pressed, inverted when
' pressed or (for CustomDrawToggleButton) toggled on
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' A custom-drawn push button rendered with a retro 3D bevel instead of the native GTK
    ''' button theme
    ''' </summary>
    Public Class CustomDrawButton
        Inherits DrawingArea

        ' ===== Private Fields =====
        Protected pLabel As String = ""
        Protected pIconPixbuf As Gdk.Pixbuf
        Protected pIsPressed As Boolean = False
        Protected pIsHovering As Boolean = False
        Protected pThemeManager As ThemeManager

        Protected pFillColor As String = "#C0C0C0"
        Protected pTextColor As String = "#000000"
        Protected pLightEdgeColor As String = "#FFFFFF"
        Protected pDarkEdgeColor As String = "#000000"

        Private Const BEVEL_WIDTH As Integer = 2
        Private Const HORIZONTAL_PADDING As Integer = 10
        Private Const ICON_TEXT_GAP As Integer = 6

        ' ===== Events =====
        Public Event Clicked(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

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
        ''' Gets or sets an optional icon drawn before the label
        ''' </summary>
        Public Property IconPixbuf As Gdk.Pixbuf
            Get
                Return pIconPixbuf
            End Get
            Set(value As Gdk.Pixbuf)
                pIconPixbuf = value
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
                pIconPixbuf = vIcon
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
                If pIconPixbuf IsNot Nothing Then
                    lWidth += pIconPixbuf.Width + ICON_TEXT_GAP
                End If
                If Not String.IsNullOrEmpty(pLabel) Then
                    lWidth += CInt(pLabel.Length * 7.5)
                End If
                SetSizeRequest(Math.Max(lWidth, 28), 26)

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
                If lTheme Is Nothing Then Return

                pFillColor = lTheme.LineNumberBackgroundColor
                pTextColor = lTheme.ForegroundColor

                ' Bevel edges derived relative to the face color (lightened/darkened by a
                ' fixed amount) rather than fixed white/black, so the highlight and shadow
                ' edges both stay visible whether the theme's face color itself lands near
                ' the light or dark end of the range
                pLightEdgeColor = LightenColor(pFillColor, 0.30)
                pDarkEdgeColor = DarkenColor(pFillColor, 0.30)
                QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

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

                ' Face fill - slightly lightened on hover for basic mouse feedback
                Dim lFace As String = If(pIsHovering AndAlso Sensitive, LightenColor(pFillColor, 0.08), pFillColor)
                SetSourceColor(vContext, lFace)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

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

                ' Icon/label content - nudged 1px down+right while pressed to reinforce
                ' the "pushed in" feel
                Dim lContentOffset As Integer = If(lPressed, 1, 0)
                Dim lX As Integer = HORIZONTAL_PADDING + lContentOffset

                If pIconPixbuf IsNot Nothing Then
                    Dim lIconY As Integer = (lHeight - pIconPixbuf.Height) \ 2 + lContentOffset
                    vContext.Save()
                    Gdk.CairoHelper.SetSourcePixbuf(vContext, pIconPixbuf, lX, lIconY)
                    vContext.Paint()
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

            Catch ex As Exception
                Console.WriteLine($"CustomDrawButton.DrawButton error: {ex.Message}")
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
        ''' A click flips Active (which raises Toggled) instead of raising Clicked
        ''' </summary>
        Protected Overrides Sub FireClicked()
            Active = Not Active
        End Sub

    End Class

End Namespace
