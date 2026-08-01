' Widgets/CustomDrawCheckBox.vb - Checkbox rendered with the same retro 3D bevel language
' as CustomDrawButton: a small square indicator (raised when unchecked, pressed-in when
' checked, with a checkmark drawn inside) followed by an optional text label
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' A checkbox with a custom-drawn retro 3D bevel indicator instead of the native GTK
    ''' checkbox theme
    ''' </summary>
    Public Class CustomDrawCheckBox
        Inherits DrawingArea

        ''' <summary>
        ''' Visual style for a CustomDrawCheckBox
        ''' </summary>
        Public Enum eCheckBoxStyle
            ''' <summary>Unknown or unspecified style</summary>
            eUnspecified
            ''' <summary>Retro AmigaOS Workbench raised/pressed-in 3D bevel square (default)</summary>
            eBevel
            ''' <summary>Thin flat outlined square, filled solid with the accent color when checked</summary>
            eFlat
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum

        ' ===== Private Fields =====
        Private pLabel As String = ""
        Private pActive As Boolean = False
        Private pIsPressed As Boolean = False
        Private pIsHovering As Boolean = False
        Private pThemeManager As ThemeManager
        Private pStyle As eCheckBoxStyle = eCheckBoxStyle.eBevel

        Private pFillColor As String = "#C0C0C0"
        Private pTextColor As String = "#000000"
        Private pLightEdgeColor As String = "#FFFFFF"
        Private pDarkEdgeColor As String = "#000000"
        Private pFlatFillColor As String = "#1E1E1E"
        Private pFlatBorderColor As String = "#808080"
        Private pFlatAccentColor As String = "#007ACC"

        Private Const BOX_SIZE As Integer = 14
        Private Const BEVEL_WIDTH As Integer = 2
        Private Const FLAT_BORDER_WIDTH As Integer = 2
        Private Const FLAT_RADIUS As Integer = 3
        Private Const LABEL_GAP As Integer = 6

        ' ===== Events =====
        Public Event Toggled(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

        ''' <summary>
        ''' Gets or sets the visual style (raised bevel vs. thin flat outline)
        ''' </summary>
        Public Property Style As eCheckBoxStyle
            Get
                Return pStyle
            End Get
            Set(value As eCheckBoxStyle)
                pStyle = value
                QueueDraw()
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets whether the checkbox is currently checked
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

        ''' <summary>
        ''' Gets or sets the label drawn to the right of the checkbox square
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

        Public Sub New(Optional vLabel As String = "")
            MyBase.New()
            Try
                pLabel = vLabel
                CanFocus = True
                Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                         EventMask.EnterNotifyMask Or EventMask.LeaveNotifyMask

                UpdateRequestedSize()

                AddHandler Me.Drawn, AddressOf OnCustomDraw
                AddHandler Me.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler Me.ButtonReleaseEvent, AddressOf OnButtonRelease
                AddHandler Me.EnterNotifyEvent, AddressOf OnEnterNotify
                AddHandler Me.LeaveNotifyEvent, AddressOf OnLeaveNotify

            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.New error: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateRequestedSize()
            Try
                Dim lWidth As Integer = BOX_SIZE
                If Not String.IsNullOrEmpty(pLabel) Then
                    lWidth += LABEL_GAP + CInt(pLabel.Length * 7.5)
                End If
                SetSizeRequest(lWidth, Math.Max(BOX_SIZE, 20))

            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.UpdateRequestedSize error: {ex.Message}")
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

                pLightEdgeColor = If(String.IsNullOrEmpty(lTheme.BevelLightColor), LightenColor(pFillColor, 0.30), lTheme.BevelLightColor)
                pDarkEdgeColor = If(String.IsNullOrEmpty(lTheme.BevelDarkColor), DarkenColor(pFillColor, 0.30), lTheme.BevelDarkColor)

                pFlatFillColor = If(String.IsNullOrEmpty(lTheme.EditorBackgroundColor), lTheme.BackgroundColor, lTheme.EditorBackgroundColor)
                pFlatBorderColor = LightenColor(pFlatFillColor, 0.35)
                pFlatAccentColor = If(String.IsNullOrEmpty(lTheme.AccentColor), pFillColor, lTheme.AccentColor)
                QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        ' ===== Drawing =====

        Private Function OnCustomDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawCheckBox(vArgs.Cr)
                Return True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.OnCustomDraw error: {ex.Message}")
                Return True
            End Try
        End Function

        Private Sub DrawCheckBox(vContext As Context)
            Try
                Dim lHeight As Integer = AllocatedHeight
                Dim lBoxY As Integer = (lHeight - BOX_SIZE) \ 2

                If pStyle = eCheckBoxStyle.eFlat Then
                    DrawFlatBox(vContext, lBoxY)
                Else
                    DrawBevelBox(vContext, lBoxY)
                End If

                ' Label
                If Not String.IsNullOrEmpty(pLabel) Then
                    SetSourceColor(vContext, If(Sensitive, pTextColor, LightenColor(pTextColor, 0.4)))
                    vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal)
                    vContext.SetFontSize(11)
                    Dim lExtents As TextExtents = vContext.TextExtents(pLabel)
                    Dim lTextY As Integer = (lHeight + CInt(lExtents.Height)) \ 2
                    vContext.MoveTo(BOX_SIZE + LABEL_GAP, lTextY)
                    vContext.ShowText(pLabel)
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.DrawCheckBox error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws the retro AmigaOS-style raised/pressed-in bevel square with a checkmark
        ''' </summary>
        Private Sub DrawBevelBox(vContext As Context, vBoxY As Integer)
            Try
                ' Checked or momentarily pressed both render "pressed in" (inverted bevel),
                ' matching CustomDrawToggleButton's convention
                Dim lPressed As Boolean = pActive OrElse pIsPressed

                Dim lFace As String = If(pIsHovering AndAlso Sensitive, LightenColor(pFillColor, 0.08), pFillColor)
                SetSourceColor(vContext, lFace)
                vContext.Rectangle(0, vBoxY, BOX_SIZE, BOX_SIZE)
                vContext.Fill()

                Dim lTopLeftColor As String = If(lPressed, pDarkEdgeColor, pLightEdgeColor)
                Dim lBottomRightColor As String = If(lPressed, pLightEdgeColor, pDarkEdgeColor)

                SetSourceColor(vContext, lTopLeftColor)
                vContext.Rectangle(0, vBoxY, BOX_SIZE, BEVEL_WIDTH)                      ' top
                vContext.Fill()
                vContext.Rectangle(0, vBoxY, BEVEL_WIDTH, BOX_SIZE)                      ' left
                vContext.Fill()

                SetSourceColor(vContext, lBottomRightColor)
                vContext.Rectangle(0, vBoxY + BOX_SIZE - BEVEL_WIDTH, BOX_SIZE, BEVEL_WIDTH)   ' bottom
                vContext.Fill()
                vContext.Rectangle(BOX_SIZE - BEVEL_WIDTH, vBoxY, BEVEL_WIDTH, BOX_SIZE)        ' right
                vContext.Fill()

                If pActive Then
                    DrawCheckmark(vContext, vBoxY, pTextColor, BEVEL_WIDTH + 2)
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.DrawBevelBox error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws a thin flat outlined square - unchecked shows just the outline, checked
        ''' fills solid with the accent color and draws a light checkmark on top
        ''' </summary>
        Private Sub DrawFlatBox(vContext As Context, vBoxY As Integer)
            Try
                DrawRoundedRect(vContext, 0, vBoxY, BOX_SIZE, BOX_SIZE, FLAT_RADIUS)

                If pActive Then
                    Dim lFace As String = If(pIsHovering AndAlso Sensitive, LightenColor(pFlatAccentColor, 0.1), pFlatAccentColor)
                    SetSourceColor(vContext, lFace)
                    vContext.FillPreserve()
                    DrawCheckmark(vContext, vBoxY, pFlatFillColor, 4)
                Else
                    SetSourceColor(vContext, pFlatFillColor)
                    vContext.FillPreserve()

                    Dim lBorder As String = If(pIsHovering AndAlso Sensitive, pFlatAccentColor, pFlatBorderColor)
                    SetSourceColor(vContext, lBorder)
                    vContext.LineWidth = FLAT_BORDER_WIDTH
                    vContext.Stroke()
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.DrawFlatBox error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws the checkmark polyline inside the box in the given color
        ''' </summary>
        Private Sub DrawCheckmark(vContext As Context, vBoxY As Integer, vColor As String, vPad As Integer)
            SetSourceColor(vContext, vColor)
            vContext.LineWidth = 2
            vContext.LineCap = LineCap.Round
            vContext.LineJoin = LineJoin.Round
            vContext.MoveTo(vPad, vBoxY + BOX_SIZE \ 2)
            vContext.LineTo(BOX_SIZE \ 2, vBoxY + BOX_SIZE - vPad)
            vContext.LineTo(BOX_SIZE - vPad, vBoxY + vPad)
            vContext.Stroke()
        End Sub

        ''' <summary>
        ''' Traces a rounded-rectangle path (does not fill/stroke)
        ''' </summary>
        Private Sub DrawRoundedRect(vContext As Context, vX As Double, vY As Double, vWidth As Double, vHeight As Double, vRadius As Double)
            Dim lRadius As Double = Math.Max(0, Math.Min(vRadius, Math.Min(vWidth, vHeight) / 2.0))
            vContext.NewPath()
            vContext.Arc(vX + vWidth - lRadius, vY + lRadius, lRadius, -Math.PI / 2, 0)
            vContext.Arc(vX + vWidth - lRadius, vY + vHeight - lRadius, lRadius, 0, Math.PI / 2)
            vContext.Arc(vX + lRadius, vY + vHeight - lRadius, lRadius, Math.PI / 2, Math.PI)
            vContext.Arc(vX + lRadius, vY + lRadius, lRadius, Math.PI, 3 * Math.PI / 2)
            vContext.ClosePath()
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
                Console.WriteLine($"CustomDrawCheckBox.OnButtonPress error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs)
            Try
                If Not Sensitive Then Return
                If vArgs.Event.Button = 1 AndAlso pIsPressed Then
                    pIsPressed = False
                    If pIsHovering Then
                        Active = Not Active
                    Else
                        QueueDraw()
                    End If
                End If
                vArgs.RetVal = True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.OnButtonRelease error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnEnterNotify(vSender As Object, vArgs As EnterNotifyEventArgs)
            pIsHovering = True
            QueueDraw()
        End Sub

        Private Sub OnLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            pIsHovering = False
            pIsPressed = False
            QueueDraw()
        End Sub

        ' ===== Helpers =====

        Private Sub SetSourceColor(vContext As Context, vHexColor As String)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHexColor) Then
                    vContext.SetSourceRGBA(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.SetSourceColor error: {ex.Message}")
            End Try
        End Sub

        Private Function LightenColor(vHexColor As String, vAmount As Double) As String
            Try
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor
                Dim lR As Double = Math.Min(1.0, lColor.Red + vAmount)
                Dim lG As Double = Math.Min(1.0, lColor.Green + vAmount)
                Dim lB As Double = Math.Min(1.0, lColor.Blue + vAmount)
                Return $"#{CInt(lR * 255):X2}{CInt(lG * 255):X2}{CInt(lB * 255):X2}"
            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.LightenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

        Private Function DarkenColor(vHexColor As String, vAmount As Double) As String
            Try
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor
                Dim lR As Double = Math.Max(0.0, lColor.Red - vAmount)
                Dim lG As Double = Math.Max(0.0, lColor.Green - vAmount)
                Dim lB As Double = Math.Max(0.0, lColor.Blue - vAmount)
                Return $"#{CInt(lR * 255):X2}{CInt(lG * 255):X2}{CInt(lB * 255):X2}"
            Catch ex As Exception
                Console.WriteLine($"CustomDrawCheckBox.DarkenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

    End Class

End Namespace
