' Widgets/CustomDrawTextBox.vb - Text entry with a retro sunken 3D bevel, matching
' CustomDrawButton's raised bevel but inverted (dark top/left, light bottom/right) and
' double width, so a text-entry "well" reads as visually distinct from a raised button
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    ''' <summary>
    ''' A text entry rendered with a retro sunken 3D bevel instead of the native GTK entry
    ''' frame. Wraps a real Gtk.Entry (via Gtk.Overlay) for full native text editing -
    ''' cursor, selection, clipboard, IME - rather than reimplementing text editing; only
    ''' the surrounding chrome is custom-drawn
    ''' </summary>
    Public Class CustomDrawTextBox
        Inherits Overlay

        ' ===== Private Fields =====
        Private pBackgroundArea As DrawingArea
        Private pEntry As Entry
        Private pThemeManager As ThemeManager

        Private pFillColor As String = "#FFFFFF"
        Private pTopLeftColor As String = "#808080"     ' dark edge (sunken look)
        Private pBottomRightColor As String = "#FFFFFF" ' light edge (sunken look)

        ' Double CustomDrawButton's 2px bevel, per James's request to make a text-entry
        ' "well" read as visually distinct from a raised button
        Private Const BEVEL_WIDTH As Integer = 4

        ' ===== Events =====
        Public Event Changed(vSender As Object, vArgs As EventArgs)
        Public Event Activated(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

        Public Property Text As String
            Get
                Return pEntry.Text
            End Get
            Set(value As String)
                pEntry.Text = value
            End Set
        End Property

        Public Property PlaceholderText As String
            Get
                Return pEntry.PlaceholderText
            End Get
            Set(value As String)
                pEntry.PlaceholderText = value
            End Set
        End Property

        ''' <summary>Gets the wrapped native Entry, for anything not exposed here directly
        ''' (e.g. WidthRequest, MaxLength, SelectRegion, Position, GetSelectionBounds,
        ''' KeyPressEvent)</summary>
        Public ReadOnly Property InnerEntry As Entry
            Get
                Return pEntry
            End Get
        End Property

        ''' <summary>Whether the wrapped Entry (not the outer Overlay) currently has
        ''' keyboard focus - shadows Widget.HasFocus, which would otherwise reflect the
        ''' Overlay's own focus state rather than the Entry's</summary>
        Public Shadows ReadOnly Property HasFocus As Boolean
            Get
                Return pEntry IsNot Nothing AndAlso pEntry.HasFocus
            End Get
        End Property

        ''' <summary>Focuses the wrapped Entry - shadows Widget.GrabFocus, which would
        ''' otherwise try to focus the outer Overlay itself rather than the Entry</summary>
        Public Shadows Sub GrabFocus()
            pEntry?.GrabFocus()
        End Sub

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

        Public Sub New(Optional vPlaceholder As String = "")
            MyBase.New()
            Try
                pBackgroundArea = New DrawingArea()
                AddHandler pBackgroundArea.Drawn, AddressOf OnCustomDraw
                Add(pBackgroundArea)

                pEntry = New Entry()
                pEntry.PlaceholderText = vPlaceholder
                pEntry.Halign = Align.Fill
                pEntry.Valign = Align.Fill
                pEntry.MarginStart = BEVEL_WIDTH
                pEntry.MarginEnd = BEVEL_WIDTH
                pEntry.MarginTop = BEVEL_WIDTH
                pEntry.MarginBottom = BEVEL_WIDTH
                AddHandler pEntry.Changed, Sub(vSender As Object, vArgs As EventArgs) RaiseEvent Changed(Me, vArgs)
                AddHandler pEntry.Activated, Sub(vSender As Object, vArgs As EventArgs) RaiseEvent Activated(Me, vArgs)
                AddOverlay(pEntry)

                ApplyEntryCss()
                UpdateMinimumSize()

                ' GetPreferredSize() can under-report before the widget has a real GDK
                ' window and resolved style/font metrics (confirmed via diagnostic: the
                ' pre-realize estimate came in smaller than the true post-realize natural
                ' size), so re-measure once realized and force a fresh layout pass
                AddHandler Me.Realized, Sub()
                    UpdateMinimumSize()
                    Me.QueueResize()
                End Sub

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextBox.New error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gtk.Overlay's own preferred-size negotiation with its parent container does not
        ''' reliably reflect an overlay child's (here, the real pEntry's) actual size need
        ''' just by giving the main child (pBackgroundArea) a size request - confirmed via
        ''' a diagnostic where pBackgroundArea's request was set correctly but the Overlay
        ''' still ended up allocated smaller than pEntry's own measured natural height, and
        ''' pEntry (an overlay child) rendered past the Overlay's own bounds as a result.
        ''' Calling SetSizeRequest directly on Me (the Overlay itself) sidesteps that
        ''' ambiguity entirely: it unconditionally clamps the minimum size this widget
        ''' reports to whatever container it's packed into, regardless of how Overlay
        ''' negotiates internally between its main and overlay children
        ''' </summary>
        Private Sub UpdateMinimumSize()
            Try
                Dim lMinSize As Requisition = Nothing
                Dim lNatSize As Requisition = Nothing
                pEntry.GetPreferredSize(lMinSize, lNatSize)

                Dim lRequiredHeight As Integer = Math.Max(lMinSize.Height, lNatSize.Height) + (BEVEL_WIDTH * 2)
                If lRequiredHeight > 0 Then
                    Me.SetSizeRequest(-1, lRequiredHeight)
                End If

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextBox.UpdateMinimumSize error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Strips the Entry's native border/background so only our custom-drawn bevel
        ''' shows, and paints its own fill to match the current face color
        ''' </summary>
        Private Sub ApplyEntryCss()
            Try
                ' GTK's default entry theming reserves its own padding and a minimum
                ' content height on top of our outer bevel margins - left alone, that
                ' extra reserved space pushes the entry's natural height past what a
                ' compact host (e.g. a toolbar row) allocates, so the real Entry gets
                ' squeezed and its text/placeholder renders low enough to clip into the
                ' bottom bevel line instead of sitting centered. Zeroing both here makes
                ' our own margins the only spacing, so text stays centered in whatever
                ' room is actually available
                Dim lCss As String =
                    "entry { border: none; background-image: none; box-shadow: none; " &
                    "padding: 0px 2px; min-height: 0px; " &
                    $"background-color: {pFillColor}; }}"
                CssHelper.ApplyCssToWidget(pEntry, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextBox.ApplyEntryCss error: {ex.Message}")
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

                pFillColor = lTheme.BackgroundColor

                ' Sunken look: dark top/left, light bottom/right - the inverse of
                ' CustomDrawButton's raised bevel. Uses the theme's explicit
                ' BevelDarkColor/BevelLightColor override if set (the same pair
                ' CustomDrawButton reads, just assigned to opposite corners since this
                ' widget is sunken rather than raised), otherwise derives relative to the
                ' face color as CustomDrawButton does, for the same reasoning
                pTopLeftColor = If(String.IsNullOrEmpty(lTheme.BevelDarkColor), DarkenColor(pFillColor, 0.30), lTheme.BevelDarkColor)
                pBottomRightColor = If(String.IsNullOrEmpty(lTheme.BevelLightColor), LightenColor(pFillColor, 0.30), lTheme.BevelLightColor)

                ApplyEntryCss()
                UpdateMinimumSize()
                pBackgroundArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextBox.ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        ' ===== Drawing =====

        Private Function OnCustomDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawBevel(vArgs.Cr)
                Return True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextBox.OnCustomDraw error: {ex.Message}")
                Return True
            End Try
        End Function

        Private Sub DrawBevel(vContext As Context)
            Try
                Dim lWidth As Integer = pBackgroundArea.AllocatedWidth
                Dim lHeight As Integer = pBackgroundArea.AllocatedHeight
                If lWidth <= 0 OrElse lHeight <= 0 Then Return

                SetSourceColor(vContext, pFillColor)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()

                ' Bounds check: on a very short/narrow allocation (e.g. a compact toolbar
                ' row) a full-width bevel edge would overlap its opposite edge or spill
                ' past the widget entirely, so clamp the edge thickness to at most half
                ' the available dimension rather than assuming BEVEL_WIDTH always fits
                Dim lVBevel As Integer = Math.Min(BEVEL_WIDTH, lHeight \ 2)
                Dim lHBevel As Integer = Math.Min(BEVEL_WIDTH, lWidth \ 2)
                If lVBevel <= 0 OrElse lHBevel <= 0 Then Return

                SetSourceColor(vContext, pTopLeftColor)
                vContext.Rectangle(0, 0, lWidth, lVBevel)                      ' top
                vContext.Fill()
                vContext.Rectangle(0, 0, lHBevel, lHeight)                     ' left
                vContext.Fill()

                SetSourceColor(vContext, pBottomRightColor)
                vContext.Rectangle(0, lHeight - lVBevel, lWidth, lVBevel)               ' bottom
                vContext.Fill()
                vContext.Rectangle(lWidth - lHBevel, 0, lHBevel, lHeight)               ' right
                vContext.Fill()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextBox.DrawBevel error: {ex.Message}")
            End Try
        End Sub

        ' ===== Helpers =====

        Private Sub SetSourceColor(vContext As Context, vHexColor As String)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHexColor) Then
                    vContext.SetSourceRGBA(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawTextBox.SetSourceColor error: {ex.Message}")
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
                Console.WriteLine($"CustomDrawTextBox.LightenColor error: {ex.Message}")
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
                Console.WriteLine($"CustomDrawTextBox.DarkenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

    End Class

End Namespace
