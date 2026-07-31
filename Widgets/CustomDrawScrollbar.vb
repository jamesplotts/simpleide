' Widgets/CustomDrawScrollbar.vb - Vertical or horizontal scrollbar with a retro 3D bevel
' thumb over a recessed track, replacing the native GTK scrollbar's flat/themed look
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' A custom-drawn scrollbar (vertical or horizontal): a raised-bevel thumb (pressed-in
    ''' while being dragged) sliding within a recessed track. Wraps a real Gtk.Adjustment
    ''' internally so it's a drop-in replacement for Gtk.Scrollbar wherever only
    ''' .Value/.Adjustment/.Visible/.ValueChanged are used
    ''' </summary>
    Public Class CustomDrawScrollbar
        Inherits DrawingArea

        ' ===== Private Fields =====
        Private pOrientation As Orientation
        Private pAdjustment As Adjustment
        Private pThemeManager As ThemeManager

        Private pIsDraggingThumb As Boolean = False
        Private pDragStartMousePos As Double = 0
        Private pDragStartValue As Double = 0
        Private pIsHoveringThumb As Boolean = False

        Private pThumbColor As String = "#C0C0C0"
        Private pTrackColor As String = "#808080"
        Private pTextColor As String = "#000000"
        Private pLightEdgeColor As String = "#FFFFFF"
        Private pDarkEdgeColor As String = "#000000"

        Private Const BEVEL_WIDTH As Integer = 2
        Private Const MIN_THUMB_SIZE As Integer = 20
        Private Const SCROLLBAR_THICKNESS As Integer = 14

        ' ===== Events =====
        Public Event ValueChanged(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

        ''' <summary>Gets the underlying Adjustment - set its Lower/Upper/PageSize/
        ''' StepIncrement/PageIncrement exactly as with a native Gtk.Scrollbar</summary>
        Public ReadOnly Property Adjustment As Adjustment
            Get
                Return pAdjustment
            End Get
        End Property

        Public Property Value As Double
            Get
                Return pAdjustment.Value
            End Get
            Set(value As Double)
                Dim lMax As Double = Math.Max(pAdjustment.Lower, pAdjustment.Upper - pAdjustment.PageSize)
                pAdjustment.Value = Math.Max(pAdjustment.Lower, Math.Min(value, lMax))
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

        ''' <summary>
        ''' Creates a new CustomDrawScrollbar
        ''' </summary>
        ''' <param name="vOrientation">Vertical (default) or Horizontal</param>
        Public Sub New(Optional vOrientation As Orientation = Orientation.Vertical)
            MyBase.New()
            Try
                pOrientation = vOrientation
                pAdjustment = New Adjustment(0, 0, 100, 1, 10, 10)
                AddHandler pAdjustment.ValueChanged, AddressOf OnAdjustmentValueChanged

                CanFocus = False
                Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                         EventMask.PointerMotionMask Or EventMask.EnterNotifyMask Or
                         EventMask.LeaveNotifyMask

                If pOrientation = Orientation.Vertical Then
                    SetSizeRequest(SCROLLBAR_THICKNESS, -1)
                Else
                    SetSizeRequest(-1, SCROLLBAR_THICKNESS)
                End If

                AddHandler Me.Drawn, AddressOf OnCustomDraw
                AddHandler Me.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler Me.ButtonReleaseEvent, AddressOf OnButtonRelease
                AddHandler Me.MotionNotifyEvent, AddressOf OnMotionNotify
                AddHandler Me.EnterNotifyEvent, AddressOf OnEnterNotify
                AddHandler Me.LeaveNotifyEvent, AddressOf OnLeaveNotify

            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.New error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnAdjustmentValueChanged(vSender As Object, vArgs As EventArgs)
            QueueDraw()
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
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

                pThumbColor = lTheme.LineNumberBackgroundColor
                pTrackColor = DarkenColor(lTheme.BackgroundColor, 0.08)
                pTextColor = lTheme.ForegroundColor

                pLightEdgeColor = If(String.IsNullOrEmpty(lTheme.BevelLightColor), LightenColor(pThumbColor, 0.30), lTheme.BevelLightColor)
                pDarkEdgeColor = If(String.IsNullOrEmpty(lTheme.BevelDarkColor), DarkenColor(pThumbColor, 0.30), lTheme.BevelDarkColor)
                QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        ' ===== Layout =====

        ''' <summary>Gets the track's extent along the scroll axis (Height if vertical,
        ''' Width if horizontal)</summary>
        Private ReadOnly Property TrackExtent As Integer
            Get
                Return If(pOrientation = Orientation.Vertical, AllocatedHeight, AllocatedWidth)
            End Get
        End Property

        ''' <summary>
        ''' Computes the thumb's current position and size along the scroll axis,
        ''' proportional to PageSize/range the same way a native scrollbar would
        ''' </summary>
        Private Function GetThumbBounds() As ValueTuple(Of Integer, Integer)
            Try
                Dim lTrackExtent As Integer = TrackExtent
                Dim lRange As Double = pAdjustment.Upper - pAdjustment.Lower
                If lRange <= 0 OrElse lTrackExtent <= 0 Then Return (0, lTrackExtent)

                Dim lThumbSize As Integer = Math.Max(MIN_THUMB_SIZE, CInt(lTrackExtent * (pAdjustment.PageSize / lRange)))
                lThumbSize = Math.Min(lThumbSize, lTrackExtent)

                Dim lMaxThumbPos As Integer = lTrackExtent - lThumbSize
                Dim lScrollableRange As Double = lRange - pAdjustment.PageSize
                Dim lThumbPos As Integer = If(lScrollableRange > 0,
                    CInt(lMaxThumbPos * ((pAdjustment.Value - pAdjustment.Lower) / lScrollableRange)), 0)

                Return (lThumbPos, lThumbSize)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.GetThumbBounds error: {ex.Message}")
                Return (0, TrackExtent)
            End Try
        End Function

        ''' <summary>Extracts the mouse coordinate relevant to this scrollbar's axis</summary>
        Private Function GetMousePos(vX As Double, vY As Double) As Double
            Return If(pOrientation = Orientation.Vertical, vY, vX)
        End Function

        ' ===== Drawing =====

        Private Function OnCustomDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawScrollbar(vArgs.Cr)
                Return True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.OnCustomDraw error: {ex.Message}")
                Return True
            End Try
        End Function

        Private Sub DrawScrollbar(vContext As Context)
            Try
                Dim lWidth As Integer = AllocatedWidth
                Dim lHeight As Integer = AllocatedHeight

                ' Recessed track
                SetSourceColor(vContext, pTrackColor)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

                Dim lBounds = GetThumbBounds()
                Dim lThumbPos As Integer = lBounds.Item1
                Dim lThumbSize As Integer = lBounds.Item2
                If lThumbSize >= TrackExtent Then Return ' nothing to scroll - track only

                Dim lPressed As Boolean = pIsDraggingThumb

                Dim lFace As String = If(pIsHoveringThumb AndAlso Not lPressed, LightenColor(pThumbColor, 0.08), pThumbColor)
                SetSourceColor(vContext, lFace)

                Dim lThumbX, lThumbY, lThumbW, lThumbH As Integer
                If pOrientation = Orientation.Vertical Then
                    lThumbX = 0 : lThumbY = lThumbPos : lThumbW = lWidth : lThumbH = lThumbSize
                Else
                    lThumbX = lThumbPos : lThumbY = 0 : lThumbW = lThumbSize : lThumbH = lHeight
                End If
                vContext.Rectangle(lThumbX, lThumbY, lThumbW, lThumbH)
                vContext.Fill()

                Dim lTopLeftColor As String = If(lPressed, pDarkEdgeColor, pLightEdgeColor)
                Dim lBottomRightColor As String = If(lPressed, pLightEdgeColor, pDarkEdgeColor)

                SetSourceColor(vContext, lTopLeftColor)
                vContext.Rectangle(lThumbX, lThumbY, lThumbW, BEVEL_WIDTH)                                    ' top
                vContext.Fill()
                vContext.Rectangle(lThumbX, lThumbY, BEVEL_WIDTH, lThumbH)                                    ' left
                vContext.Fill()

                SetSourceColor(vContext, lBottomRightColor)
                vContext.Rectangle(lThumbX, lThumbY + lThumbH - BEVEL_WIDTH, lThumbW, BEVEL_WIDTH)            ' bottom
                vContext.Fill()
                vContext.Rectangle(lThumbX + lThumbW - BEVEL_WIDTH, lThumbY, BEVEL_WIDTH, lThumbH)            ' right
                vContext.Fill()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.DrawScrollbar error: {ex.Message}")
            End Try
        End Sub

        ' ===== Mouse Handling =====

        Private Sub OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                If vArgs.Event.Button <> 1 Then Return

                Dim lBounds = GetThumbBounds()
                Dim lThumbPos As Integer = lBounds.Item1
                Dim lThumbSize As Integer = lBounds.Item2
                Dim lMousePos As Double = GetMousePos(vArgs.Event.X, vArgs.Event.Y)

                If lMousePos >= lThumbPos AndAlso lMousePos < lThumbPos + lThumbSize Then
                    ' Grabbed the thumb - start a drag
                    pIsDraggingThumb = True
                    pDragStartMousePos = lMousePos
                    pDragStartValue = pAdjustment.Value
                    QueueDraw()
                ElseIf lMousePos < lThumbPos Then
                    Value = Value - pAdjustment.PageIncrement
                Else
                    Value = Value + pAdjustment.PageIncrement
                End If

                vArgs.RetVal = True

            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.OnButtonPress error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs)
            Try
                If vArgs.Event.Button = 1 AndAlso pIsDraggingThumb Then
                    pIsDraggingThumb = False
                    QueueDraw()
                End If
                vArgs.RetVal = True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.OnButtonRelease error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs)
            Try
                Dim lBounds = GetThumbBounds()
                Dim lThumbPos As Integer = lBounds.Item1
                Dim lThumbSize As Integer = lBounds.Item2
                Dim lMousePos As Double = GetMousePos(vArgs.Event.X, vArgs.Event.Y)

                Dim lNowHovering As Boolean = lMousePos >= lThumbPos AndAlso lMousePos < lThumbPos + lThumbSize
                If lNowHovering <> pIsHoveringThumb Then
                    pIsHoveringThumb = lNowHovering
                    QueueDraw()
                End If

                If pIsDraggingThumb Then
                    Dim lTrackExtent As Integer = TrackExtent
                    Dim lRange As Double = pAdjustment.Upper - pAdjustment.Lower
                    Dim lScrollableRange As Double = lRange - pAdjustment.PageSize
                    If lScrollableRange > 0 Then
                        Dim lMaxThumbPos As Integer = lTrackExtent - lThumbSize
                        If lMaxThumbPos > 0 Then
                            Dim lDeltaPos As Double = lMousePos - pDragStartMousePos
                            Dim lDeltaValue As Double = (lDeltaPos / lMaxThumbPos) * lScrollableRange
                            Value = pDragStartValue + lDeltaValue
                        End If
                    End If
                End If

                vArgs.RetVal = True

            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.OnMotionNotify error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnEnterNotify(vSender As Object, vArgs As EnterNotifyEventArgs)
        End Sub

        Private Sub OnLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            If pIsHoveringThumb Then
                pIsHoveringThumb = False
                QueueDraw()
            End If
        End Sub

        ' ===== Helpers =====

        Private Sub SetSourceColor(vContext As Context, vHexColor As String)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHexColor) Then
                    vContext.SetSourceRGBA(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawScrollbar.SetSourceColor error: {ex.Message}")
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
                Console.WriteLine($"CustomDrawScrollbar.LightenColor error: {ex.Message}")
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
                Console.WriteLine($"CustomDrawScrollbar.DarkenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

    End Class

End Namespace
