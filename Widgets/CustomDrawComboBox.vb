' Widgets/CustomDrawComboBox.vb - Dropdown selector rendered as a sunken text-box-style
' bevel (matching CustomDrawTextBox's "well" look) with a dropdown arrow on the right side.
' Clicking anywhere in the box opens a custom-drawn scrollable popup list
' (CustomDrawComboBoxPopup) below it - fully custom-drawn, no wrapped Gtk.ComboBox/Entry at
' all. An earlier version wrapped a real Gtk.ComboBoxText for native popup behavior, but
' that broke in real use (dropdown arrow vanishing depending on CSS reset aggressiveness,
' then the popup not reliably opening on a real click and text not centering correctly) -
' GtkComboBox's popup depends on an internal pointer grab tied to its own click sequence
' that didn't survive being wrapped for custom chrome. This mirrors NavigationDropdowns'
' already-proven custom trigger+popup pattern instead of fighting GTK's composite internals
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    ''' <summary>
    ''' A dropdown combo box styled as a sunken text-box with a dropdown arrow on the right,
    ''' opening a custom-drawn scrollable popup list on click - either a retro raised-well
    ''' AmigaOS-style bevel or a thin flat modern underline
    ''' </summary>
    Public Class CustomDrawComboBox
        Inherits DrawingArea

        ''' <summary>
        ''' Visual style for a CustomDrawComboBox
        ''' </summary>
        Public Enum eComboBoxStyle
            ''' <summary>Unknown or unspecified style</summary>
            eUnspecified
            ''' <summary>Retro AmigaOS Workbench sunken 3D "well" bevel (default)</summary>
            eBevel
            ''' <summary>Thin flat style with an accent-colored underline instead of a bevel</summary>
            eFlat
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum

        ' ===== Private Fields =====
        Private pItems As New List(Of String)
        Private pActiveIndex As Integer = -1
        Private pThemeManager As ThemeManager
        Private pStyle As eComboBoxStyle = eComboBoxStyle.eBevel
        Private pIsHovering As Boolean = False
        Private pPopup As CustomDrawComboBoxPopup

        Private pFillColor As String = "#FFFFFF"
        Private pTextColor As String = "#000000"
        Private pTopLeftColor As String = "#808080"      ' dark edge (sunken look)
        Private pBottomRightColor As String = "#FFFFFF"  ' light edge (sunken look)
        Private pFlatFillColor As String = "#1E1E1E"
        Private pFlatAccentColor As String = "#007ACC"

        ' Double CustomDrawButton's bevel width, matching CustomDrawTextBox's convention,
        ' so this reads as a text-entry "well" rather than a raised button
        Private Const BEVEL_WIDTH As Integer = 4
        Private Const TEXT_PADDING_LEFT As Integer = 6
        Private Const ARROW_AREA_WIDTH As Integer = 20
        Private Const MIN_HEIGHT As Integer = 24

        ' ===== Events =====
        Public Event Changed(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

        ''' <summary>Gets or sets the visual style (sunken bevel well vs. thin flat underline)</summary>
        Public Property Style As eComboBoxStyle
            Get
                Return pStyle
            End Get
            Set(value As eComboBoxStyle)
                pStyle = value
                QueueDraw()
            End Set
        End Property

        ''' <summary>Gets or sets the index of the selected item, or -1 if none</summary>
        Public Property Active As Integer
            Get
                Return pActiveIndex
            End Get
            Set(value As Integer)
                Dim lNewIndex As Integer = If(value >= 0 AndAlso value < pItems.Count, value, -1)
                If lNewIndex <> pActiveIndex Then
                    pActiveIndex = lNewIndex
                    QueueDraw()
                    RaiseEvent Changed(Me, EventArgs.Empty)
                End If
            End Set
        End Property

        ''' <summary>Gets the text of the selected item, or Nothing if none is selected</summary>
        Public ReadOnly Property ActiveText As String
            Get
                Return If(pActiveIndex >= 0 AndAlso pActiveIndex < pItems.Count, pItems(pActiveIndex), Nothing)
            End Get
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
                pPopup?.SetThemeManager(value)
                ApplyCurrentTheme()
            End Set
        End Property

        ' ===== Constructor =====

        Public Sub New()
            MyBase.New()
            Try
                CanFocus = True
                Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                         EventMask.EnterNotifyMask Or EventMask.LeaveNotifyMask

                SetSizeRequest(-1, MIN_HEIGHT)

                AddHandler Me.Drawn, AddressOf OnCustomDraw
                AddHandler Me.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler Me.EnterNotifyEvent, AddressOf OnEnterNotify
                AddHandler Me.LeaveNotifyEvent, AddressOf OnLeaveNotify

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.New error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Appends a text item to the combo box
        ''' </summary>
        ''' <param name="vText">Text of the item to append</param>
        Public Sub AppendText(vText As String)
            pItems.Add(vText)
            If pActiveIndex < 0 Then pActiveIndex = 0
            QueueDraw()
        End Sub

        ''' <summary>
        ''' Removes all items from the combo box
        ''' </summary>
        Public Sub RemoveAll()
            pItems.Clear()
            pActiveIndex = -1
            QueueDraw()
        End Sub

        ''' <summary>
        ''' Returns the index of the first item matching vText, or -1 if not found
        ''' </summary>
        Public Function IndexOf(vText As String) As Integer
            Return pItems.IndexOf(vText)
        End Function

        ''' <summary>
        ''' Selects the first item matching vText, if found - no-op otherwise
        ''' </summary>
        Public Sub SelectByText(vText As String)
            Dim lIndex As Integer = pItems.IndexOf(vText)
            If lIndex >= 0 Then Active = lIndex
        End Sub

        ' ===== Popup =====

        Private Sub EnsurePopup()
            If pPopup IsNot Nothing Then Return
            pPopup = New CustomDrawComboBoxPopup()
            pPopup.SetThemeManager(pThemeManager)
            AddHandler pPopup.ItemSelected, AddressOf OnPopupItemSelected
            AddHandler pPopup.PopupCancelled, AddressOf OnPopupCancelled
        End Sub

        Private Sub OpenPopup()
            Try
                If Not Sensitive Then Return
                EnsurePopup()
                pPopup.ShowFor(Me, pItems, pActiveIndex)
                QueueDraw()
            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.OpenPopup error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnPopupItemSelected(vIndex As Integer)
            Active = vIndex
            QueueDraw()
        End Sub

        Private Sub OnPopupCancelled()
            QueueDraw()
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

                pFillColor = lTheme.BackgroundColor
                pTextColor = lTheme.ForegroundColor

                pTopLeftColor = If(String.IsNullOrEmpty(lTheme.BevelDarkColor), DarkenColor(pFillColor, 0.30), lTheme.BevelDarkColor)
                pBottomRightColor = If(String.IsNullOrEmpty(lTheme.BevelLightColor), LightenColor(pFillColor, 0.30), lTheme.BevelLightColor)

                pFlatFillColor = If(String.IsNullOrEmpty(lTheme.EditorBackgroundColor), lTheme.BackgroundColor, lTheme.EditorBackgroundColor)
                pFlatAccentColor = If(String.IsNullOrEmpty(lTheme.AccentColor), pFillColor, lTheme.AccentColor)

                QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        ' ===== Drawing =====

        Private Function OnCustomDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                If pStyle = eComboBoxStyle.eFlat Then
                    DrawFlat(vArgs.Cr)
                Else
                    DrawBevel(vArgs.Cr)
                End If
                Return True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.OnCustomDraw error: {ex.Message}")
                Return True
            End Try
        End Function

        Private Sub DrawBevel(vContext As Context)
            Try
                Dim lWidth As Integer = AllocatedWidth
                Dim lHeight As Integer = AllocatedHeight
                If lWidth <= 0 OrElse lHeight <= 0 Then Return

                Dim lFace As String = If(pIsHovering AndAlso Sensitive, LightenColor(pFillColor, 0.05), pFillColor)
                SetSourceColor(vContext, lFace)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

                ' Bounds check: clamp edge thickness to at most half the available
                ' dimension so a very short/narrow allocation can't make opposite edges
                ' overlap or spill past the widget
                Dim lVBevel As Integer = Math.Min(BEVEL_WIDTH, lHeight \ 2)
                Dim lHBevel As Integer = Math.Min(BEVEL_WIDTH, lWidth \ 2)
                If lVBevel > 0 AndAlso lHBevel > 0 Then
                    SetSourceColor(vContext, pTopLeftColor)
                    vContext.Rectangle(0, 0, lWidth, lVBevel)                      ' top
                    vContext.Fill()
                    vContext.Rectangle(0, 0, lHBevel, lHeight)                     ' left
                    vContext.Fill()

                    SetSourceColor(vContext, pBottomRightColor)
                    vContext.Rectangle(0, lHeight - lVBevel, lWidth, lVBevel)           ' bottom
                    vContext.Fill()
                    vContext.Rectangle(lWidth - lHBevel, 0, lHBevel, lHeight)           ' right
                    vContext.Fill()
                End If

                DrawTextAndArrow(vContext, lWidth, lHeight, pTextColor)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.DrawBevel error: {ex.Message}")
            End Try
        End Sub

        Private Sub DrawFlat(vContext As Context)
            Try
                Dim lWidth As Integer = AllocatedWidth
                Dim lHeight As Integer = AllocatedHeight
                If lWidth <= 0 OrElse lHeight <= 0 Then Return

                SetSourceColor(vContext, pFlatFillColor)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

                Const UNDERLINE_WIDTH As Integer = 2
                Dim lUnderlineWidth As Integer = Math.Min(UNDERLINE_WIDTH, lHeight \ 2)
                If lUnderlineWidth > 0 Then
                    SetSourceColor(vContext, pFlatAccentColor, If(pIsHovering, 1.0, 0.6))
                    vContext.Rectangle(0, lHeight - lUnderlineWidth, lWidth, lUnderlineWidth)
                    vContext.Fill()
                End If

                DrawTextAndArrow(vContext, lWidth, lHeight, pTextColor)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.DrawFlat error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws the selected item's text (left-aligned, vertically centered) and a
        ''' dropdown arrow indicator in a reserved area on the right
        ''' </summary>
        Private Sub DrawTextAndArrow(vContext As Context, vWidth As Integer, vHeight As Integer, vColor As String)
            SetSourceColor(vContext, If(Sensitive, vColor, LightenColor(vColor, 0.4)))
            vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal)
            vContext.SetFontSize(11)

            Dim lText As String = If(ActiveText, "")
            If Not String.IsNullOrEmpty(lText) Then
                Dim lExtents As TextExtents = vContext.TextExtents(lText)
                Dim lTextY As Integer = (vHeight + CInt(lExtents.Height)) \ 2
                vContext.MoveTo(BEVEL_WIDTH + TEXT_PADDING_LEFT, lTextY)
                vContext.ShowText(lText)
            End If

            ' Dropdown arrow, centered in the reserved right-hand area
            Dim lArrowCenterX As Double = vWidth - BEVEL_WIDTH - (ARROW_AREA_WIDTH / 2.0)
            Dim lArrowCenterY As Double = vHeight / 2.0
            Const ARROW_HALF_WIDTH As Double = 4
            Const ARROW_HEIGHT As Double = 3
            vContext.MoveTo(lArrowCenterX - ARROW_HALF_WIDTH, lArrowCenterY - ARROW_HEIGHT / 2)
            vContext.LineTo(lArrowCenterX + ARROW_HALF_WIDTH, lArrowCenterY - ARROW_HEIGHT / 2)
            vContext.LineTo(lArrowCenterX, lArrowCenterY + ARROW_HEIGHT)
            vContext.ClosePath()
            vContext.Fill()
        End Sub

        ' ===== Mouse Handling =====

        Private Sub OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                If Not Sensitive Then Return
                GrabFocus()
                If vArgs.Event.Button = 1 Then
                    OpenPopup()
                End If
                vArgs.RetVal = True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.OnButtonPress error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnEnterNotify(vSender As Object, vArgs As EnterNotifyEventArgs)
            pIsHovering = True
            QueueDraw()
        End Sub

        Private Sub OnLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            pIsHovering = False
            QueueDraw()
        End Sub

        ' ===== Helpers =====

        Private Sub SetSourceColor(vContext As Context, vHexColor As String, Optional vAlpha As Double = 1.0)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHexColor) Then
                    vContext.SetSourceRGBA(lColor.Red, lColor.Green, lColor.Blue, vAlpha)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.SetSourceColor error: {ex.Message}")
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
                Console.WriteLine($"CustomDrawComboBox.LightenColor error: {ex.Message}")
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
                Console.WriteLine($"CustomDrawComboBox.DarkenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

    End Class

End Namespace
