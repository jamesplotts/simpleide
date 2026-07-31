' Widgets/CustomDrawComboBox.vb - Dropdown selector with a retro raised 3D bevel (or a
' modern flat style), wrapping a real Gtk.ComboBoxText for full native dropdown behavior -
' keyboard navigation, popup positioning, item rendering - rather than reimplementing a
' popup list from scratch; only the surrounding chrome is custom-drawn
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    ''' <summary>
    ''' A dropdown combo box rendered with either a retro raised AmigaOS-style 3D bevel or
    ''' a thin flat modern style, wrapping a real Gtk.ComboBoxText (via Gtk.Overlay) for
    ''' full native dropdown/keyboard/popup behavior
    ''' </summary>
    Public Class CustomDrawComboBox
        Inherits Overlay

        ''' <summary>
        ''' Visual style for a CustomDrawComboBox
        ''' </summary>
        Public Enum eComboBoxStyle
            ''' <summary>Unknown or unspecified style</summary>
            eUnspecified
            ''' <summary>Retro AmigaOS Workbench raised 3D bevel (default)</summary>
            eBevel
            ''' <summary>Thin flat style with an accent-colored underline instead of a bevel</summary>
            eFlat
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum

        ' ===== Private Fields =====
        Private pBackgroundArea As DrawingArea
        Private pCombo As ComboBoxText
        Private pThemeManager As ThemeManager
        Private pStyle As eComboBoxStyle = eComboBoxStyle.eBevel
        Private pIsHovering As Boolean = False

        Private pFillColor As String = "#C0C0C0"
        Private pTextColor As String = "#000000"
        Private pLightEdgeColor As String = "#FFFFFF"
        Private pDarkEdgeColor As String = "#000000"
        Private pFlatFillColor As String = "#1E1E1E"
        Private pFlatAccentColor As String = "#007ACC"

        Private Const BEVEL_WIDTH As Integer = 2
        Private Const FLAT_UNDERLINE_WIDTH As Integer = 2

        ' ===== Events =====
        Public Event Changed(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

        ''' <summary>Gets or sets the visual style (raised bevel vs. thin flat underline)</summary>
        Public Property Style As eComboBoxStyle
            Get
                Return pStyle
            End Get
            Set(value As eComboBoxStyle)
                pStyle = value
                pBackgroundArea?.QueueDraw()
            End Set
        End Property

        ''' <summary>Gets or sets the index of the selected item, or -1 if none</summary>
        Public Property Active As Integer
            Get
                Return pCombo.Active
            End Get
            Set(value As Integer)
                pCombo.Active = value
            End Set
        End Property

        ''' <summary>Gets the text of the selected item, or Nothing if none is selected</summary>
        Public ReadOnly Property ActiveText As String
            Get
                Return pCombo.ActiveText
            End Get
        End Property

        ''' <summary>Gets the wrapped native ComboBoxText, for anything not exposed here
        ''' directly (e.g. Model, IdColumn, RemoveText)</summary>
        Public ReadOnly Property InnerCombo As ComboBoxText
            Get
                Return pCombo
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
                ApplyCurrentTheme()
            End Set
        End Property

        ' ===== Constructor =====

        Public Sub New()
            MyBase.New()
            Try
                pBackgroundArea = New DrawingArea()
                AddHandler pBackgroundArea.Drawn, AddressOf OnCustomDraw
                Add(pBackgroundArea)

                pCombo = New ComboBoxText()
                pCombo.Halign = Align.Fill
                pCombo.Valign = Align.Fill
                pCombo.MarginStart = BEVEL_WIDTH
                pCombo.MarginEnd = BEVEL_WIDTH
                pCombo.MarginTop = BEVEL_WIDTH
                pCombo.MarginBottom = BEVEL_WIDTH
                pCombo.StyleContext.AddClass("customdraw-combobox")
                AddHandler pCombo.Changed, Sub(vSender As Object, vArgs As EventArgs) RaiseEvent Changed(Me, vArgs)
                AddHandler pCombo.EnterNotifyEvent, AddressOf OnComboEnterNotify
                AddHandler pCombo.LeaveNotifyEvent, AddressOf OnComboLeaveNotify
                AddOverlay(pCombo)

                EnsureGlobalCssRegistered()
                ApplyEntryCss()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.New error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Appends a text item to the combo box - proxies Gtk.ComboBoxText.AppendText
        ''' </summary>
        ''' <param name="vText">Text of the item to append</param>
        Public Sub AppendText(vText As String)
            pCombo.AppendText(vText)
        End Sub

        ''' <summary>
        ''' Removes all items from the combo box - proxies Gtk.ComboBoxText.RemoveAll
        ''' </summary>
        Public Sub RemoveAll()
            pCombo.RemoveAll()
        End Sub

        Private Sub OnComboEnterNotify(vSender As Object, vArgs As EnterNotifyEventArgs)
            pIsHovering = True
            pBackgroundArea?.QueueDraw()
        End Sub

        Private Sub OnComboLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            pIsHovering = False
            pBackgroundArea?.QueueDraw()
        End Sub

        ''' <summary>
        ''' Sets the text color for the arrow/selected-item text. This is a per-widget
        ''' provider (correctly matches the combo's own top-level CSS node) rather than
        ''' global CSS - unlike the button's background, "color" inherits down through
        ''' composite child nodes regardless of which context registered it, so this alone
        ''' is enough to tint the internal cellview/arrow correctly
        ''' </summary>
        Private Sub ApplyEntryCss()
            Try
                Dim lCss As String = ".customdraw-combobox { color: " & pTextColor & "; }"
                CssHelper.ApplyCssToWidget(pCombo, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.ApplyEntryCss error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Registers the shared structural CSS, once per process, that strips GTK's
        ''' default combobox-button chrome (border/background/shadow/focus-ring) down to
        ''' fully transparent. A per-widget provider added directly to the combo's own
        ''' StyleContext cannot reach its internal composite "button" child node, so this
        ''' has to be a global (screen-level) provider to correctly cascade into it - but
        ''' since the button becomes fully transparent rather than needing a specific fill
        ''' color, one shared global rule works for every instance and every theme: our own
        ''' pBackgroundArea (the Overlay's base child, underneath the real ComboBoxText)
        ''' shows through and supplies the actual bevel/flat fill
        ''' </summary>
        Private Shared pGlobalCssRegistered As Boolean = False
        Private Sub EnsureGlobalCssRegistered()
            Try
                If pGlobalCssRegistered Then Return
                pGlobalCssRegistered = True

                Dim lCss As String =
                    ".customdraw-combobox, .customdraw-combobox button {" &
                    " background-color: transparent; background-image: none;" &
                    " border: none; box-shadow: none; padding: 0px 4px; min-height: 0px; }" &
                    ".customdraw-combobox button:focus { outline: none; }"
                CssHelper.ApplyCssGlobally(lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.EnsureGlobalCssRegistered error: {ex.Message}")
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
                pFlatAccentColor = If(String.IsNullOrEmpty(lTheme.AccentColor), pFillColor, lTheme.AccentColor)

                ApplyEntryCss()
                pBackgroundArea?.QueueDraw()

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
                Dim lWidth As Integer = pBackgroundArea.AllocatedWidth
                Dim lHeight As Integer = pBackgroundArea.AllocatedHeight
                If lWidth <= 0 OrElse lHeight <= 0 Then Return

                Dim lFace As String = If(pIsHovering, LightenColor(pFillColor, 0.08), pFillColor)
                SetSourceColor(vContext, lFace)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

                ' Bounds check: clamp edge thickness to at most half the available
                ' dimension so a very short/narrow allocation can't make opposite edges
                ' overlap or spill past the widget
                Dim lVBevel As Integer = Math.Min(BEVEL_WIDTH, lHeight \ 2)
                Dim lHBevel As Integer = Math.Min(BEVEL_WIDTH, lWidth \ 2)
                If lVBevel <= 0 OrElse lHBevel <= 0 Then Return

                SetSourceColor(vContext, pLightEdgeColor)
                vContext.Rectangle(0, 0, lWidth, lVBevel)                      ' top
                vContext.Fill()
                vContext.Rectangle(0, 0, lHBevel, lHeight)                     ' left
                vContext.Fill()

                SetSourceColor(vContext, pDarkEdgeColor)
                vContext.Rectangle(0, lHeight - lVBevel, lWidth, lVBevel)               ' bottom
                vContext.Fill()
                vContext.Rectangle(lWidth - lHBevel, 0, lHBevel, lHeight)               ' right
                vContext.Fill()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.DrawBevel error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Draws a flat face with a thin accent-colored underline instead of a bevel -
        ''' the underline brightens on hover, similar in spirit to CustomDrawScrollbar's
        ''' flat style
        ''' </summary>
        Private Sub DrawFlat(vContext As Context)
            Try
                Dim lWidth As Integer = pBackgroundArea.AllocatedWidth
                Dim lHeight As Integer = pBackgroundArea.AllocatedHeight
                If lWidth <= 0 OrElse lHeight <= 0 Then Return

                SetSourceColor(vContext, pFlatFillColor)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

                Dim lUnderlineWidth As Integer = Math.Min(FLAT_UNDERLINE_WIDTH, lHeight \ 2)
                If lUnderlineWidth <= 0 Then Return

                SetSourceColor(vContext, pFlatAccentColor, If(pIsHovering, 1.0, 0.6))
                vContext.Rectangle(0, lHeight - lUnderlineWidth, lWidth, lUnderlineWidth)
                vContext.Fill()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.DrawFlat error: {ex.Message}")
            End Try
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
