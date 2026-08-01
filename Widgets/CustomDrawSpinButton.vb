' Widgets/CustomDrawSpinButton.vb - Numeric entry with retro-bevel increment/decrement
' buttons, composed from the already-proven CustomDrawTextBox (sunken well) and
' CustomDrawButton (raised/flat face) rather than wrapping Gtk.SpinButton, whose native
' theming can't be reached the same way a plain Gtk.Entry's could (see the
' CustomDrawComboBox lesson: fighting a native composite widget's own internal chrome
' with CSS/Overlay tricks is fragile - building the whole thing from already-themed
' primitives is not)
Imports Gtk
Imports System
Imports SimpleIDE.Managers

Namespace Widgets

    ''' <summary>
    ''' A numeric spin entry rendered with a retro sunken text well plus stacked
    ''' raised/flat increment and decrement buttons, instead of the native GTK
    ''' Gtk.SpinButton theme
    ''' </summary>
    Public Class CustomDrawSpinButton
        Inherits Box

        ''' <summary>
        ''' Visual style for a CustomDrawSpinButton's +/- buttons
        ''' </summary>
        Public Enum eSpinButtonStyle
            ''' <summary>Unknown or unspecified style</summary>
            eUnspecified
            ''' <summary>Retro AmigaOS Workbench raised/pressed-in 3D bevel buttons (default)</summary>
            eBevel
            ''' <summary>Thin flat outlined buttons with no 3D bevel edges</summary>
            eFlat
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum

        ' ===== Private Fields =====
        Private pTextBox As CustomDrawTextBox
        Private pUpButton As CustomDrawButton
        Private pDownButton As CustomDrawButton
        Private pThemeManager As ThemeManager
        Private pStyle As eSpinButtonStyle = eSpinButtonStyle.eBevel

        Private pValue As Double = 0
        Private pMinimum As Double = 0
        Private pMaximum As Double = 100
        Private pIncrement As Double = 1
        Private pDigits As Integer = 0
        Private pIsUpdating As Boolean = False

        Private Const ARROW_WIDTH As Integer = 18
        Private Const ARROW_MIN_HEIGHT As Integer = 11

        ' ===== Events =====

        ''' <summary>
        ''' Raised whenever Value changes, whether from an arrow click, mouse-wheel scroll,
        ''' or the entry being committed (Enter or losing focus)
        ''' </summary>
        Public Event ValueChanged(vSender As Object, vArgs As EventArgs)

        ' ===== Public Properties =====

        ''' <summary>
        ''' Gets or sets the visual style (raised bevel vs. thin flat outline) of the
        ''' increment/decrement buttons
        ''' </summary>
        Public Property Style As eSpinButtonStyle
            Get
                Return pStyle
            End Get
            Set(value As eSpinButtonStyle)
                pStyle = value
                Dim lButtonStyle As CustomDrawButton.eButtonStyle =
                    If(value = eSpinButtonStyle.eFlat, CustomDrawButton.eButtonStyle.eFlat, CustomDrawButton.eButtonStyle.eBevel)
                pUpButton.Style = lButtonStyle
                pDownButton.Style = lButtonStyle
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the current numeric value, clamped to [Minimum, Maximum]
        ''' </summary>
        Public Property Value As Double
            Get
                Return pValue
            End Get
            Set(value As Double)
                SetValue(value, True)
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the lowest value Value may be set to
        ''' </summary>
        Public Property Minimum As Double
            Get
                Return pMinimum
            End Get
            Set(value As Double)
                pMinimum = value
                If pValue < pMinimum Then SetValue(pMinimum, True)
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the highest value Value may be set to
        ''' </summary>
        Public Property Maximum As Double
            Get
                Return pMaximum
            End Get
            Set(value As Double)
                pMaximum = value
                If pValue > pMaximum Then SetValue(pMaximum, True)
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the amount Value changes by per arrow click or scroll step
        ''' </summary>
        Public Property Increment As Double
            Get
                Return pIncrement
            End Get
            Set(value As Double)
                pIncrement = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the number of decimal places displayed and rounded to
        ''' </summary>
        Public Property Digits As Integer
            Get
                Return pDigits
            End Get
            Set(value As Integer)
                pDigits = Math.Max(0, value)
                UpdateEntryText()
            End Set
        End Property

        ''' <summary>Gets the wrapped CustomDrawTextBox, for anything not exposed here
        ''' directly (e.g. WidthRequest via InnerEntry)</summary>
        Public ReadOnly Property InnerTextBox As CustomDrawTextBox
            Get
                Return pTextBox
            End Get
        End Property

        Public Property ThemeManager As ThemeManager
            Get
                Return pThemeManager
            End Get
            Set(value As ThemeManager)
                pThemeManager = value
                pTextBox.ThemeManager = value
                pUpButton.ThemeManager = value
                pDownButton.ThemeManager = value
            End Set
        End Property

        ' ===== Constructor =====

        ''' <summary>
        ''' Constructs a CustomDrawSpinButton - matches Gtk.SpinButton's (vMin, vMax, vStep)
        ''' constructor shape so it's a drop-in replacement at call sites
        ''' </summary>
        ''' <param name="vMin">Lowest allowed value</param>
        ''' <param name="vMax">Highest allowed value</param>
        ''' <param name="vStep">Amount Value changes by per arrow click or scroll step</param>
        Public Sub New(Optional vMin As Double = 0, Optional vMax As Double = 100, Optional vStep As Double = 1)
            MyBase.New(Orientation.Horizontal, 0)
            Try
                pMinimum = vMin
                pMaximum = vMax
                pIncrement = vStep
                pValue = vMin

                pTextBox = New CustomDrawTextBox()
                pTextBox.InnerEntry.WidthChars = 4
                pTextBox.InnerEntry.Events = pTextBox.InnerEntry.Events Or Gdk.EventMask.ScrollMask
                AddHandler pTextBox.Activated, AddressOf OnEntryCommit
                AddHandler pTextBox.InnerEntry.FocusOutEvent, AddressOf OnEntryFocusOut
                AddHandler pTextBox.InnerEntry.ScrollEvent, AddressOf OnEntryScroll
                PackStart(pTextBox, True, True, 0)

                Dim lArrowBox As New Box(Orientation.Vertical, 1)
                lArrowBox.MarginStart = 2

                pUpButton = New CustomDrawButton("+")
                pUpButton.SetSizeRequest(ARROW_WIDTH, ARROW_MIN_HEIGHT)
                AddHandler pUpButton.Clicked, AddressOf OnUpClicked
                lArrowBox.PackStart(pUpButton, True, True, 0)

                pDownButton = New CustomDrawButton("-")
                pDownButton.SetSizeRequest(ARROW_WIDTH, ARROW_MIN_HEIGHT)
                AddHandler pDownButton.Clicked, AddressOf OnDownClicked
                lArrowBox.PackStart(pDownButton, True, True, 0)

                PackStart(lArrowBox, False, False, 0)

                UpdateEntryText()

            Catch ex As Exception
                Console.WriteLine($"CustomDrawSpinButton.New error: {ex.Message}")
            End Try
        End Sub

        ' ===== Value handling =====

        ''' <summary>
        ''' Clamps and rounds vNewValue, applies it, refreshes the entry text, and raises
        ''' ValueChanged if it actually changed
        ''' </summary>
        Private Sub SetValue(vNewValue As Double, vUpdateText As Boolean)
            If pIsUpdating Then Return
            Try
                pIsUpdating = True

                Dim lClamped As Double = Math.Max(pMinimum, Math.Min(pMaximum, vNewValue))
                lClamped = Math.Round(lClamped, pDigits)

                Dim lChanged As Boolean = Math.Abs(lClamped - pValue) > 0.0000001
                pValue = lClamped

                If vUpdateText Then UpdateEntryText()

                If lChanged Then RaiseEvent ValueChanged(Me, EventArgs.Empty)

            Catch ex As Exception
                Console.WriteLine($"CustomDrawSpinButton.SetValue error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub

        Private Sub UpdateEntryText()
            If pTextBox Is Nothing Then Return
            pTextBox.Text = pValue.ToString("F" & pDigits)
        End Sub

        ' ===== Arrow buttons =====

        Private Sub OnUpClicked(vSender As Object, vArgs As EventArgs)
            SetValue(pValue + pIncrement, True)
        End Sub

        Private Sub OnDownClicked(vSender As Object, vArgs As EventArgs)
            SetValue(pValue - pIncrement, True)
        End Sub

        ' ===== Entry commit / scroll =====

        Private Sub OnEntryCommit(vSender As Object, vArgs As EventArgs)
            CommitEntryText()
        End Sub

        Private Sub OnEntryFocusOut(vSender As Object, vArgs As FocusOutEventArgs)
            CommitEntryText()
        End Sub

        ''' <summary>
        ''' Parses the entry's current text and applies it as Value - reverts the
        ''' displayed text back to the last valid Value if it doesn't parse, rather than
        ''' leaving unparseable text sitting in the entry
        ''' </summary>
        Private Sub CommitEntryText()
            Try
                Dim lParsed As Double
                If Double.TryParse(pTextBox.Text.Trim(), lParsed) Then
                    SetValue(lParsed, True)
                Else
                    UpdateEntryText()
                End If
            Catch ex As Exception
                Console.WriteLine($"CustomDrawSpinButton.CommitEntryText error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnEntryScroll(vSender As Object, vArgs As ScrollEventArgs)
            Try
                Select Case vArgs.Event.Direction
                    Case Gdk.ScrollDirection.Up
                        SetValue(pValue + pIncrement, True)
                        vArgs.RetVal = True
                    Case Gdk.ScrollDirection.Down
                        SetValue(pValue - pIncrement, True)
                        vArgs.RetVal = True
                End Select
            Catch ex As Exception
                Console.WriteLine($"CustomDrawSpinButton.OnEntryScroll error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
