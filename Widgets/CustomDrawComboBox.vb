' Widgets/CustomDrawComboBox.vb - Dropdown selector with a retro raised 3D bevel (or a
' modern flat style), applied via CSS directly to a real Gtk.ComboBoxText for full native
' dropdown/keyboard/popup behavior - no Overlay wrapping or Cairo-drawn background, since
' those risked interfering with GTK's internal grab-based popup mechanism and made GTK's
' own font-metric text centering unreliable
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    ''' <summary>
    ''' A dropdown combo box styled with either a retro raised AmigaOS-style 3D bevel or a
    ''' thin flat modern underline, applied via CSS directly to a real Gtk.ComboBoxText -
    ''' the wrapped widget is otherwise untouched, so dropdown popup, keyboard navigation,
    ''' and text centering all stay fully native
    ''' </summary>
    Public Class CustomDrawComboBox
        Inherits Box

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
        Private pCombo As ComboBoxText
        Private pThemeManager As ThemeManager
        Private pStyle As eComboBoxStyle = eComboBoxStyle.eBevel

        Private pFillColor As String = "#C0C0C0"
        Private pTextColor As String = "#000000"
        Private pLightEdgeColor As String = "#FFFFFF"
        Private pDarkEdgeColor As String = "#000000"
        Private pFlatFillColor As String = "#1E1E1E"
        Private pFlatAccentColor As String = "#007ACC"

        Private Const BEVEL_WIDTH As Integer = 2

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
                ApplyStyleCss()
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
            MyBase.New(Orientation.Horizontal, 0)
            Try
                pCombo = New ComboBoxText()
                pCombo.StyleContext.AddClass("customdraw-combobox")
                AddHandler pCombo.Changed, Sub(vSender As Object, vArgs As EventArgs) RaiseEvent Changed(Me, vArgs)
                PackStart(pCombo, True, True, 0)

                EnsureGlobalCssRegistered()
                ApplyStyleCss()

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

        ''' <summary>
        ''' Registers the shared structural CSS, once per process, that strips only the
        ''' internal composite "button" child's own background/border/shadow (letting our
        ''' bevel/flat styling on the outer combobox node show through it) - deliberately
        ''' leaves background-image and padding/min-height alone. A prior version also
        ''' zeroed background-image (which broke the dropdown arrow on themes that render it
        ''' as a background layer) and padding/min-height (which threw off GTK's own
        ''' font-metric-based vertical text centering, since the theme's centering math
        ''' assumes its own baseline padding). A per-widget provider added directly to the
        ''' combo's own StyleContext cannot reach this internal "button" child node, so this
        ''' has to be a global (screen-level) provider to correctly cascade into it
        ''' </summary>
        Private Shared pGlobalCssRegistered As Boolean = False
        Private Sub EnsureGlobalCssRegistered()
            Try
                If pGlobalCssRegistered Then Return
                pGlobalCssRegistered = True

                Dim lCss As String =
                    ".customdraw-combobox button {" &
                    " background-color: transparent; border: none; box-shadow: none; }" &
                    ".customdraw-combobox button:focus { outline: none; }"
                CssHelper.ApplyCssGlobally(lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.EnsureGlobalCssRegistered error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Applies the bevel/flat face color and CSS box-shadow (a raised-bevel or
        ''' underline effect, faked via inset shadows since we're not doing any Cairo
        ''' drawing here) to the combobox's own top-level CSS node - reachable via a normal
        ''' per-widget provider, since (unlike the internal button) this is the combo's own
        ''' leaf node
        ''' </summary>
        Private Sub ApplyStyleCss()
            Try
                Dim lFace As String = If(pStyle = eComboBoxStyle.eFlat, pFlatFillColor, pFillColor)
                Dim lShadow As String
                If pStyle = eComboBoxStyle.eFlat Then
                    lShadow = $"inset 0 -{BEVEL_WIDTH}px 0 0 {pFlatAccentColor}"
                Else
                    lShadow = $"inset -{BEVEL_WIDTH}px -{BEVEL_WIDTH}px 0 0 {pDarkEdgeColor}, " &
                               $"inset {BEVEL_WIDTH}px {BEVEL_WIDTH}px 0 0 {pLightEdgeColor}"
                End If

                Dim lCss As String =
                    ".customdraw-combobox {" &
                    $" background-color: {lFace}; background-image: none; border: none;" &
                    $" box-shadow: {lShadow}; color: {pTextColor}; }}"
                CssHelper.ApplyCssToWidget(pCombo, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.ApplyStyleCss error: {ex.Message}")
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

                ApplyStyleCss()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawComboBox.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        ' ===== Helpers =====

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
