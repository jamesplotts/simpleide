' Widgets/ColorPicker.vb - Advanced color picker widget with gradient selector and color palette
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Collections.Generic

' ColorPicker.vb
' Created: 2025-08-19 05:53:32

Namespace Widgets
    
    ''' <summary>
    ''' A flexible color picker widget that supports both horizontal and vertical layouts
    ''' similar to Microsoft's color picker with gradient area and color palette
    ''' </summary>
    Public Class ColorPicker
        Inherits Box
        
        ' Layout options
        Public Enum LayoutMode
            eUnspecified
            eHorizontal  ' Side by side (for Theme Editor)
            eVertical    ' Stacked (for PngEditor side panel)
            eLastValue
        End Enum
        
        ' Private fields
        Private pLayoutMode As LayoutMode
        Private pCurrentColor As Gdk.RGBA
        Private pIsUpdating As Boolean = False
        
        ' Color gradient components
        Private pGradientArea As DrawingArea
        Private pHueSlider As DrawingArea
        Private pGradientWidth As Integer = 256
        Private pGradientHeight As Integer = 256
        Private pHueSliderWidth As Integer = 30
        Private pCurrentHue As Double = 0.0
        Private pCurrentSaturation As Double = 1.0
        Private pCurrentLightness As Double = 0.5
        
        ' Gradient interaction
        Private pGradientDragging As Boolean = False
        Private pHueDragging As Boolean = False
        Private pGradientCrosshairX As Double = 0
        Private pGradientCrosshairY As Double = 0
        
        ' Color palette components
        Private pBasicColorsGrid As Grid
        Private pCustomColorsGrid As Grid
        Private pBasicColorAreas As New List(Of DrawingArea)
        Private pCustomColorAreas As New List(Of DrawingArea)
        Private pCustomColors As New List(Of Gdk.RGBA)
        Private pSelectedColorArea As DrawingArea
        
        ' Value entry components
        Private pHueEntry As SpinButton
        Private pSatEntry As SpinButton
        Private pLumEntry As SpinButton
        Private pRedEntry As SpinButton
        Private pGreenEntry As SpinButton
        Private pBlueEntry As SpinButton
        Private pHexEntry As Entry
        Private pColorPreview As DrawingArea
        Private pAddToCustomButton As Button
        
        ' Events
        Public Event ColorChanged(vColor As Gdk.RGBA)
        Public Event ColorSelected(vColor As Gdk.RGBA)
        
        ' Properties
        ''' <summary>
        ''' Gets or sets the current selected color
        ''' </summary>
        Public Property CurrentColor As Gdk.RGBA
            Get
                Return pCurrentColor
            End Get
            Set(value As Gdk.RGBA)
                If Not ColorsEqual(pCurrentColor, value) Then
                    pCurrentColor = value
                    UpdateFromColor(value)
                    RaiseEvent ColorChanged(value)
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Constructor for the color picker
        ''' </summary>
        ''' <param name="vLayoutMode">Horizontal or Vertical layout mode</param>
        Public Sub New(Optional vLayoutMode As LayoutMode = LayoutMode.eHorizontal)
            MyBase.New(If(vLayoutMode = LayoutMode.eHorizontal, Orientation.Horizontal, Orientation.Vertical), 5)
            
            pLayoutMode = vLayoutMode
            pCurrentColor = New Gdk.RGBA() With {.Red = 1.0, .Green = 0.0, .Blue = 0.0, .Alpha = 1.0}
            
            ' Initialize custom colors with empty slots
            For i As Integer = 0 To 15
                pCustomColors.Add(New Gdk.RGBA() With {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1.0})
            Next
            
            BuildUI()
            UpdateFromColor(pCurrentColor)
        End Sub
        
        ''' <summary>
        ''' Builds the complete UI based on layout mode
        ''' </summary>
        Private Sub BuildUI()
            Try
                BorderWidth = 5
                
                If pLayoutMode = LayoutMode.eHorizontal Then
                    BuildHorizontalLayout()
                Else
                    BuildVerticalLayout()
                End If
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"ColorPicker.BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Builds horizontal layout (for Theme Editor)
        ''' </summary>
        Private Sub BuildHorizontalLayout()
            Try
                ' Left side - Color palette
                Dim lPaletteBox As New Box(Orientation.Vertical, 5)
                
                ' Basic colors
                Dim lBasicFrame As New Frame("Basic colors:")
                pBasicColorsGrid = CreateBasicColorsGrid()
                lBasicFrame.Add(pBasicColorsGrid)
                lPaletteBox.PackStart(lBasicFrame, False, False, 0)
                
                ' Custom colors
                Dim lCustomFrame As New Frame("Custom colors:")
                Dim lCustomBox As New Box(Orientation.Vertical, 5)
                lCustomBox.BorderWidth = 5
                
                pCustomColorsGrid = CreateCustomColorsGrid()
                lCustomBox.PackStart(pCustomColorsGrid, False, False, 0)
                
                ' Add to custom colors button
                pAddToCustomButton = New Button("Add to Custom Colors")
                AddHandler pAddToCustomButton.Clicked, AddressOf OnAddToCustomColors
                lCustomBox.PackStart(pAddToCustomButton, False, False, 0)
                
                lCustomFrame.Add(lCustomBox)
                lPaletteBox.PackStart(lCustomFrame, False, False, 0)
                
                PackStart(lPaletteBox, False, False, 0)
                
                ' Right side - Gradient selector and values
                Dim lGradientBox As New Box(Orientation.Vertical, 5)
                
                ' Gradient area with hue slider
                Dim lGradientContainer As New Box(Orientation.Horizontal, 5)
                
                pGradientArea = CreateGradientArea()
                lGradientContainer.PackStart(pGradientArea, False, False, 0)
                
                pHueSlider = CreateHueSlider()
                lGradientContainer.PackStart(pHueSlider, False, False, 0)
                
                lGradientBox.PackStart(lGradientContainer, False, False, 0)
                
                ' Color values section
                Dim lValuesBox As New Box(Orientation.Horizontal, 10)
                
                ' HSL values
                Dim lHslBox As New Box(Orientation.Vertical, 2)
                lHslBox.PackStart(CreateHslControls(), False, False, 0)
                lValuesBox.PackStart(lHslBox, False, False, 0)
                
                ' RGB values
                Dim lRgbBox As New Box(Orientation.Vertical, 2)
                lRgbBox.PackStart(CreateRgbControls(), False, False, 0)
                lValuesBox.PackStart(lRgbBox, False, False, 0)
                
                lGradientBox.PackStart(lValuesBox, False, False, 0)
                
                ' Color preview and hex
                Dim lPreviewBox As New Box(Orientation.Horizontal, 5)
                
                ' Color swatch
                pColorPreview = New DrawingArea()
                pColorPreview.SetSizeRequest(60, 30)
                AddHandler pColorPreview.Drawn, AddressOf OnColorPreviewDrawn
                
                Dim lColorSolidLabel As New Label("Color|Solid")
                Dim lColorBox As New Box(Orientation.Vertical, 2)
                lColorBox.PackStart(lColorSolidLabel, False, False, 0)
                lColorBox.PackStart(pColorPreview, False, False, 0)
                lPreviewBox.PackStart(lColorBox, False, False, 0)
                
                ' Hex value
                Dim lHexBox As New Box(Orientation.Vertical, 2)
                lHexBox.PackStart(New Label("Hex:"), False, False, 0)
                pHexEntry = New Entry()
                pHexEntry.WidthChars = 8
                AddHandler pHexEntry.Changed, AddressOf OnHexChanged
                lHexBox.PackStart(pHexEntry, False, False, 0)
                lPreviewBox.PackStart(lHexBox, False, False, 0)
                
                lGradientBox.PackStart(lPreviewBox, False, False, 0)
                
                PackStart(lGradientBox, True, True, 0)
                
            Catch ex As Exception
                Console.WriteLine($"BuildHorizontalLayout error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Builds vertical layout (for PngEditor side panel)
        ''' </summary>
        Private Sub BuildVerticalLayout()
            Try
                ' Top section - Gradient selector
                Dim lGradientFrame As New Frame("Color Selector")
                Dim lGradientBox As New Box(Orientation.Vertical, 5)
                lGradientBox.BorderWidth = 5
                
                ' Gradient area with hue slider
                Dim lGradientContainer As New Box(Orientation.Horizontal, 5)
                
                ' Make gradient area smaller for vertical layout
                pGradientWidth = 180
                pGradientHeight = 180
                pGradientArea = CreateGradientArea()
                lGradientContainer.PackStart(pGradientArea, False, False, 0)
                
                pHueSlider = CreateHueSlider()
                lGradientContainer.PackStart(pHueSlider, False, False, 0)
                
                lGradientBox.PackStart(lGradientContainer, False, False, 0)
                
                ' Compact value controls
                Dim lValuesGrid As New Grid()
                lValuesGrid.ColumnSpacing = 5
                lValuesGrid.RowSpacing = 2
                
                ' RGB controls in grid
                lValuesGrid.Attach(New Label("R:"), 0, 0, 1, 1)
                pRedEntry = New SpinButton(0, 255, 1)
                pRedEntry.Value = 255
                AddHandler pRedEntry.ValueChanged, AddressOf OnRgbValueChanged
                lValuesGrid.Attach(pRedEntry, 1, 0, 1, 1)
                
                lValuesGrid.Attach(New Label("G:"), 2, 0, 1, 1)
                pGreenEntry = New SpinButton(0, 255, 1)
                AddHandler pGreenEntry.ValueChanged, AddressOf OnRgbValueChanged
                lValuesGrid.Attach(pGreenEntry, 3, 0, 1, 1)
                
                lValuesGrid.Attach(New Label("B:"), 4, 0, 1, 1)
                pBlueEntry = New SpinButton(0, 255, 1)
                AddHandler pBlueEntry.ValueChanged, AddressOf OnRgbValueChanged
                lValuesGrid.Attach(pBlueEntry, 5, 0, 1, 1)
                
                ' Hex and preview
                lValuesGrid.Attach(New Label("Hex:"), 0, 1, 1, 1)
                pHexEntry = New Entry()
                pHexEntry.WidthChars = 7
                AddHandler pHexEntry.Changed, AddressOf OnHexChanged
                lValuesGrid.Attach(pHexEntry, 1, 1, 2, 1)
                
                pColorPreview = New DrawingArea()
                pColorPreview.SetSizeRequest(50, 25)
                AddHandler pColorPreview.Drawn, AddressOf OnColorPreviewDrawn
                lValuesGrid.Attach(pColorPreview, 3, 1, 3, 1)
                
                lGradientBox.PackStart(lValuesGrid, False, False, 0)
                lGradientFrame.Add(lGradientBox)
                PackStart(lGradientFrame, False, False, 0)
                
                ' Bottom section - Color palettes
                Dim lPaletteFrame As New Frame("Color Palette")
                Dim lPaletteBox As New Box(Orientation.Vertical, 5)
                lPaletteBox.BorderWidth = 5
                
                ' Basic colors
                Dim lBasicLabel As New Label("Basic colors:")
                lBasicLabel.Xalign = 0
                lPaletteBox.PackStart(lBasicLabel, False, False, 0)
                pBasicColorsGrid = CreateBasicColorsGrid()
                lPaletteBox.PackStart(pBasicColorsGrid, False, False, 0)
                
                ' Custom colors
                Dim lCustomLabel As New Label("Custom colors:")
                lCustomLabel.Xalign = 0
                lPaletteBox.PackStart(lCustomLabel, False, False, 0)
                pCustomColorsGrid = CreateCustomColorsGrid()
                lPaletteBox.PackStart(pCustomColorsGrid, False, False, 0)
                
                ' Add to custom button
                pAddToCustomButton = New Button("Add to Custom")
                pAddToCustomButton.HeightRequest = 25
                AddHandler pAddToCustomButton.Clicked, AddressOf OnAddToCustomColors
                lPaletteBox.PackStart(pAddToCustomButton, False, False, 0)
                
                lPaletteFrame.Add(lPaletteBox)
                PackStart(lPaletteFrame, False, False, 0)
                
            Catch ex As Exception
                Console.WriteLine($"BuildVerticalLayout error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the gradient selection area
        ''' </summary>
        Private Function CreateGradientArea() As DrawingArea
            Dim lArea As New DrawingArea()
            lArea.SetSizeRequest(pGradientWidth, pGradientHeight)
            lArea.CanFocus = True
            lArea.Events = lArea.Events Or Gdk.EventMask.ButtonPressMask Or Gdk.EventMask.ButtonReleaseMask Or Gdk.EventMask.PointerMotionMask
            
            AddHandler lArea.Drawn, AddressOf OnGradientAreaDrawn
            AddHandler lArea.ButtonPressEvent, AddressOf OnGradientAreaButtonPress
            AddHandler lArea.ButtonReleaseEvent, AddressOf OnGradientAreaButtonRelease
            AddHandler lArea.MotionNotifyEvent, AddressOf OnGradientAreaMotion
            
            Return lArea
        End Function
        
        ''' <summary>
        ''' Creates the hue slider
        ''' </summary>
        Private Function CreateHueSlider() As DrawingArea
            Dim lSlider As New DrawingArea()
            lSlider.SetSizeRequest(pHueSliderWidth, pGradientHeight)
            lSlider.CanFocus = True
            lSlider.Events = lSlider.Events Or Gdk.EventMask.ButtonPressMask Or Gdk.EventMask.ButtonReleaseMask Or Gdk.EventMask.PointerMotionMask
            
            AddHandler lSlider.Drawn, AddressOf OnHueSliderDrawn
            AddHandler lSlider.ButtonPressEvent, AddressOf OnHueSliderButtonPress
            AddHandler lSlider.ButtonReleaseEvent, AddressOf OnHueSliderButtonRelease
            AddHandler lSlider.MotionNotifyEvent, AddressOf OnHueSliderMotion
            
            Return lSlider
        End Function
        
        ''' <summary>
        ''' Creates HSL value controls
        ''' </summary>
        Private Function CreateHslControls() As Widget
            Dim lGrid As New Grid()
            lGrid.ColumnSpacing = 5
            lGrid.RowSpacing = 2
            
            ' Hue
            lGrid.Attach(New Label("Hue:"), 0, 0, 1, 1)
            pHueEntry = New SpinButton(0, 360, 1)
            AddHandler pHueEntry.ValueChanged, AddressOf OnHslValueChanged
            lGrid.Attach(pHueEntry, 1, 0, 1, 1)
            
            ' Saturation
            lGrid.Attach(New Label("Sat:"), 0, 1, 1, 1)
            pSatEntry = New SpinButton(0, 255, 1)
            AddHandler pSatEntry.ValueChanged, AddressOf OnHslValueChanged
            lGrid.Attach(pSatEntry, 1, 1, 1, 1)
            
            ' Luminance
            lGrid.Attach(New Label("Lum:"), 0, 2, 1, 1)
            pLumEntry = New SpinButton(0, 255, 1)
            AddHandler pLumEntry.ValueChanged, AddressOf OnHslValueChanged
            lGrid.Attach(pLumEntry, 1, 2, 1, 1)
            
            Return lGrid
        End Function
        
        ''' <summary>
        ''' Creates RGB value controls
        ''' </summary>
        Private Function CreateRgbControls() As Widget
            Dim lGrid As New Grid()
            lGrid.ColumnSpacing = 5
            lGrid.RowSpacing = 2
            
            ' Red
            lGrid.Attach(New Label("Red:"), 0, 0, 1, 1)
            pRedEntry = New SpinButton(0, 255, 1)
            AddHandler pRedEntry.ValueChanged, AddressOf OnRgbValueChanged
            lGrid.Attach(pRedEntry, 1, 0, 1, 1)
            
            ' Green
            lGrid.Attach(New Label("Green:"), 0, 1, 1, 1)
            pGreenEntry = New SpinButton(0, 255, 1)
            AddHandler pGreenEntry.ValueChanged, AddressOf OnRgbValueChanged
            lGrid.Attach(pGreenEntry, 1, 1, 1, 1)
            
            ' Blue
            lGrid.Attach(New Label("Blue:"), 0, 2, 1, 1)
            pBlueEntry = New SpinButton(0, 255, 1)
            AddHandler pBlueEntry.ValueChanged, AddressOf OnRgbValueChanged
            lGrid.Attach(pBlueEntry, 1, 2, 1, 1)
            
            Return lGrid
        End Function
        
        ''' <summary>
        ''' Creates the basic colors grid
        ''' </summary>
        Private Function CreateBasicColorsGrid() As Grid
            Dim lGrid As New Grid()
            lGrid.ColumnSpacing = 2
            lGrid.RowSpacing = 2
            
            ' Define basic color palette (similar to Windows color picker)
            ' Use a single-dimensional array with calculated indices to avoid jagged array syntax issues
            Dim lColors() As String = {
                "#FF8080", "#FFFF80", "#80FF80", "#00FF80", "#80FFFF", "#0080FF", "#FF80C0", "#FF80FF",
                "#FF0000", "#FFFF00", "#80FF00", "#00FF40", "#00FFFF", "#0080C0", "#8080C0", "#FF00FF",
                "#804040", "#FF8040", "#00FF00", "#008080", "#004080", "#8080FF", "#800040", "#FF0080",
                "#800000", "#FF8000", "#008000", "#008040", "#0000FF", "#0000A0", "#800080", "#8000FF",
                "#400000", "#804000", "#004000", "#004040", "#000080", "#000040", "#400040", "#400080",
                "#000000", "#808000", "#808040", "#808080", "#408080", "#C0C0C0", "#400040", "#FFFFFF"
            }
            
            Dim lColorIndex As Integer = 0
            For row As Integer = 0 To 5
                For col As Integer = 0 To 7
                    If lColorIndex < lColors.Length Then
                        Dim lColorArea As DrawingArea = CreateColorArea(lColors(lColorIndex))
                        lGrid.Attach(lColorArea, col, row, 1, 1)
                        pBasicColorAreas.Add(lColorArea)
                        lColorIndex += 1
                    End If
                Next
            Next
            
            Return lGrid
        End Function        
        ''' <summary>
        ''' Creates the custom colors grid
        ''' </summary>
        Private Function CreateCustomColorsGrid() As Grid
            Dim lGrid As New Grid()
            lGrid.ColumnSpacing = 2
            lGrid.RowSpacing = 2
            
            ' Create 2 rows of 8 custom color slots
            Dim lIndex As Integer = 0
            For row As Integer = 0 To 1
                For col As Integer = 0 To 7
                    Dim lColorArea As DrawingArea = CreateCustomColorArea(lIndex)
                    lGrid.Attach(lColorArea, col, row, 1, 1)
                    pCustomColorAreas.Add(lColorArea)
                    lIndex += 1
                Next
            Next
            
            Return lGrid
        End Function
        
        ''' <summary>
        ''' Creates a color area for the palette
        ''' </summary>
        Private Function CreateColorArea(vHexColor As String) As DrawingArea
            Dim lArea As New DrawingArea()
            lArea.SetSizeRequest(25, 25)
            lArea.CanFocus = True
            lArea.Events = lArea.Events Or Gdk.EventMask.ButtonPressMask
            
            Dim lColor As New Gdk.RGBA()
            If lColor.Parse(vHexColor) Then
                lArea.Data("Color") = lColor
            End If
            
            AddHandler lArea.Drawn, AddressOf OnColorAreaDrawn
            AddHandler lArea.ButtonPressEvent, AddressOf OnColorAreaClicked
            
            Return lArea
        End Function
        
        ''' <summary>
        ''' Creates a custom color area
        ''' </summary>
        Private Function CreateCustomColorArea(vIndex As Integer) As DrawingArea
            Dim lArea As New DrawingArea()
            lArea.SetSizeRequest(25, 25)
            lArea.CanFocus = True
            lArea.Events = lArea.Events Or Gdk.EventMask.ButtonPressMask
            lArea.Data("CustomIndex") = vIndex
            
            AddHandler lArea.Drawn, AddressOf OnCustomColorAreaDrawn
            AddHandler lArea.ButtonPressEvent, AddressOf OnCustomColorAreaClicked
            
            Return lArea
        End Function
        
        ' ===== Drawing Event Handlers =====
        
        ''' <summary>
        ''' Draws the gradient area
        ''' </summary>
        Private Sub OnGradientAreaDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lContext As Context = vArgs.Cr
                Dim lWidth As Integer = pGradientArea.AllocatedWidth
                Dim lHeight As Integer = pGradientArea.AllocatedHeight
                
                ' Draw saturation-lightness gradient for current hue
                For x As Integer = 0 To lWidth - 1
                    For y As Integer = 0 To lHeight - 1
                        Dim lSaturation As Double = x / CDbl(lWidth - 1)
                        Dim lLightness As Double = 1.0 - (y / CDbl(lHeight - 1))
                        
                        Dim lRgb As (Double, Double, Double) = HslToRgb(pCurrentHue, lSaturation, lLightness)
                        lContext.SetSourceRgb(lRgb.Item1, lRgb.Item2, lRgb.Item3)
                        lContext.Rectangle(x, y, 1, 1)
                        lContext.Fill()
                    Next
                Next
                
                ' Draw crosshair at current position
                lContext.SetSourceRgb(0, 0, 0)
                lContext.LineWidth = 1
                lContext.Arc(pGradientCrosshairX, pGradientCrosshairY, 5, 0, 2 * Math.PI)
                lContext.Stroke()
                
                lContext.SetSourceRgb(1, 1, 1)
                lContext.Arc(pGradientCrosshairX, pGradientCrosshairY, 4, 0, 2 * Math.PI)
                lContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"OnGradientAreaDrawn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the hue slider
        ''' </summary>
        Private Sub OnHueSliderDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lContext As Context = vArgs.Cr
                Dim lWidth As Integer = pHueSlider.AllocatedWidth
                Dim lHeight As Integer = pHueSlider.AllocatedHeight
                
                ' Draw hue gradient
                For y As Integer = 0 To lHeight - 1
                    Dim lHue As Double = (y / CDbl(lHeight - 1)) * 360
                    Dim lRgb As (Double, Double, Double) = HslToRgb(lHue, 1.0, 0.5)
                    lContext.SetSourceRgb(lRgb.Item1, lRgb.Item2, lRgb.Item3)
                    lContext.Rectangle(0, y, lWidth, 1)
                    lContext.Fill()
                Next
                
                ' Draw position indicator
                Dim lIndicatorY As Double = (pCurrentHue / 360.0) * lHeight
                
                ' Draw arrow indicators on both sides
                lContext.SetSourceRgb(0, 0, 0)
                lContext.LineWidth = 1
                
                ' Left arrow
                lContext.MoveTo(0, lIndicatorY)
                lContext.LineTo(5, lIndicatorY - 3)
                lContext.LineTo(5, lIndicatorY + 3)
                lContext.ClosePath()
                lContext.Fill()
                
                ' Right arrow
                lContext.MoveTo(lWidth, lIndicatorY)
                lContext.LineTo(lWidth - 5, lIndicatorY - 3)
                lContext.LineTo(lWidth - 5, lIndicatorY + 3)
                lContext.ClosePath()
                lContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"OnHueSliderDrawn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a color area in the palette
        ''' </summary>
        Private Sub OnColorAreaDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lArea As DrawingArea = TryCast(vSender, DrawingArea)
                If lArea Is Nothing Then Return
                
                Dim lContext As Context = vArgs.Cr
                Dim lColor As Gdk.RGBA = CType(lArea.Data("Color"), Gdk.RGBA)
                
                ' Fill with color
                lContext.SetSourceRgba(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                lContext.Rectangle(0, 0, lArea.AllocatedWidth, lArea.AllocatedHeight)
                lContext.Fill()
                
                ' Draw border
                If lArea Is pSelectedColorArea Then
                    lContext.SetSourceRgb(0, 0, 0)
                    lContext.LineWidth = 2
                Else
                    lContext.SetSourceRgb(0.5, 0.5, 0.5)
                    lContext.LineWidth = 1
                End If
                lContext.Rectangle(0.5, 0.5, lArea.AllocatedWidth - 1, lArea.AllocatedHeight - 1)
                lContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"OnColorAreaDrawn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a custom color area
        ''' </summary>
        Private Sub OnCustomColorAreaDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lArea As DrawingArea = TryCast(vSender, DrawingArea)
                If lArea Is Nothing Then Return
                
                Dim lContext As Context = vArgs.Cr
                Dim lIndex As Integer = CInt(lArea.Data("CustomIndex"))
                Dim lColor As Gdk.RGBA = pCustomColors(lIndex)
                
                ' Fill with color
                lContext.SetSourceRgba(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                lContext.Rectangle(0, 0, lArea.AllocatedWidth, lArea.AllocatedHeight)
                lContext.Fill()
                
                ' Draw border
                If lArea Is pSelectedColorArea Then
                    lContext.SetSourceRgb(0, 0, 0)
                    lContext.LineWidth = 2
                Else
                    lContext.SetSourceRgb(0.5, 0.5, 0.5)
                    lContext.LineWidth = 1
                End If
                lContext.Rectangle(0.5, 0.5, lArea.AllocatedWidth - 1, lArea.AllocatedHeight - 1)
                lContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"OnCustomColorAreaDrawn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the color preview
        ''' </summary>
        Private Sub OnColorPreviewDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lContext As Context = vArgs.Cr
                
                ' Draw color
                lContext.SetSourceRgba(pCurrentColor.Red, pCurrentColor.Green, pCurrentColor.Blue, pCurrentColor.Alpha)
                lContext.Rectangle(0, 0, pColorPreview.AllocatedWidth, pColorPreview.AllocatedHeight)
                lContext.Fill()
                
                ' Draw border
                lContext.SetSourceRgb(0, 0, 0)
                lContext.LineWidth = 1
                lContext.Rectangle(0.5, 0.5, pColorPreview.AllocatedWidth - 1, pColorPreview.AllocatedHeight - 1)
                lContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"OnColorPreviewDrawn error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Interaction Event Handlers =====
        
        ''' <summary>
        ''' Handles gradient area mouse button press
        ''' </summary>
        Private Function OnGradientAreaButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                If vArgs.Event.Button = 1 Then ' Left button
                    pGradientDragging = True
                    UpdateGradientPosition(vArgs.Event.X, vArgs.Event.Y)
                    Return True
                End If
                Return False
            Catch ex As Exception
                Console.WriteLine($"OnGradientAreaButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles gradient area mouse button release
        ''' </summary>
        Private Function OnGradientAreaButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            pGradientDragging = False
            Return False
        End Function
        
        ''' <summary>
        ''' Handles gradient area mouse motion
        ''' </summary>
        Private Function OnGradientAreaMotion(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                If pGradientDragging Then
                    UpdateGradientPosition(vArgs.Event.X, vArgs.Event.Y)
                    Return True
                End If
                Return False
            Catch ex As Exception
                Console.WriteLine($"OnGradientAreaMotion error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Updates position in gradient area
        ''' </summary>
        Private Sub UpdateGradientPosition(vX As Double, vY As Double)
            Try
                ' Clamp values
                vX = Math.Max(0, Math.Min(vX, pGradientArea.AllocatedWidth - 1))
                vY = Math.Max(0, Math.Min(vY, pGradientArea.AllocatedHeight - 1))
                
                pGradientCrosshairX = vX
                pGradientCrosshairY = vY
                
                ' Calculate saturation and lightness
                pCurrentSaturation = vX / CDbl(pGradientArea.AllocatedWidth - 1)
                pCurrentLightness = 1.0 - (vY / CDbl(pGradientArea.AllocatedHeight - 1))
                
                ' Update color
                UpdateColorFromHsl()
                
                ' Redraw gradient area
                pGradientArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateGradientPosition error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles hue slider button press
        ''' </summary>
        Private Function OnHueSliderButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                If vArgs.Event.Button = 1 Then ' Left button
                    pHueDragging = True
                    UpdateHuePosition(vArgs.Event.Y)
                    Return True
                End If
                Return False
            Catch ex As Exception
                Console.WriteLine($"OnHueSliderButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles hue slider button release
        ''' </summary>
        Private Function OnHueSliderButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            pHueDragging = False
            Return False
        End Function
        
        ''' <summary>
        ''' Handles hue slider motion
        ''' </summary>
        Private Function OnHueSliderMotion(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                If pHueDragging Then
                    UpdateHuePosition(vArgs.Event.Y)
                    Return True
                End If
                Return False
            Catch ex As Exception
                Console.WriteLine($"OnHueSliderMotion error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Updates hue position
        ''' </summary>
        Private Sub UpdateHuePosition(vY As Double)
            Try
                ' Clamp value
                vY = Math.Max(0, Math.Min(vY, pHueSlider.AllocatedHeight - 1))
                
                ' Calculate hue
                pCurrentHue = (vY / CDbl(pHueSlider.AllocatedHeight - 1)) * 360
                
                ' Update color
                UpdateColorFromHsl()
                
                ' Redraw
                pHueSlider.QueueDraw()
                pGradientArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateHuePosition error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles clicking on a basic color
        ''' </summary>
        Private Function OnColorAreaClicked(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                Dim lArea As DrawingArea = TryCast(vSender, DrawingArea)
                If lArea Is Nothing Then Return False
                
                Dim lColor As Gdk.RGBA = CType(lArea.Data("Color"), Gdk.RGBA)
                
                ' Update selected color area
                If pSelectedColorArea IsNot Nothing Then
                    pSelectedColorArea.QueueDraw()
                End If
                pSelectedColorArea = lArea
                lArea.QueueDraw()
                
                ' Set current color
                CurrentColor = lColor
                
                ' Fire selected event
                RaiseEvent ColorSelected(lColor)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnColorAreaClicked error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles clicking on a custom color
        ''' </summary>
        Private Function OnCustomColorAreaClicked(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                Dim lArea As DrawingArea = TryCast(vSender, DrawingArea)
                If lArea Is Nothing Then Return False
                
                Dim lIndex As Integer = CInt(lArea.Data("CustomIndex"))
                Dim lColor As Gdk.RGBA = pCustomColors(lIndex)
                
                ' Update selected color area
                If pSelectedColorArea IsNot Nothing Then
                    pSelectedColorArea.QueueDraw()
                End If
                pSelectedColorArea = lArea
                lArea.QueueDraw()
                
                ' Set current color
                CurrentColor = lColor
                
                ' Fire selected event
                RaiseEvent ColorSelected(lColor)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnCustomColorAreaClicked error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles Add to Custom Colors button click
        ''' </summary>
        Private Sub OnAddToCustomColors(vSender As Object, vArgs As EventArgs)
            Try
                ' Find first white slot or wrap around
                Static lNextCustomIndex As Integer = 0
                
                ' Store current color in next slot
                pCustomColors(lNextCustomIndex) = pCurrentColor
                
                ' Redraw that custom color area
                pCustomColorAreas(lNextCustomIndex).QueueDraw()
                
                ' Move to next slot
                lNextCustomIndex = (lNextCustomIndex + 1) Mod 16
                
            Catch ex As Exception
                Console.WriteLine($"OnAddToCustomColors error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles HSL value changes
        ''' </summary>
        Private Sub OnHslValueChanged(vSender As Object, vArgs As EventArgs)
            If pIsUpdating Then Return
            
            Try
                pIsUpdating = True
                
                ' Get HSL values
                pCurrentHue = pHueEntry.Value
                pCurrentSaturation = pSatEntry.Value / 255.0
                pCurrentLightness = pLumEntry.Value / 255.0
                
                ' Update color
                UpdateColorFromHsl()
                
                ' Update gradient position
                UpdateGradientCrosshair()
                
                ' Redraw
                pGradientArea.QueueDraw()
                pHueSlider.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnHslValueChanged error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles RGB value changes
        ''' </summary>
        Private Sub OnRgbValueChanged(vSender As Object, vArgs As EventArgs)
            If pIsUpdating Then Return
            
            Try
                pIsUpdating = True
                
                ' Get RGB values
                Dim lRed As Double = pRedEntry.Value / 255.0
                Dim lGreen As Double = pGreenEntry.Value / 255.0
                Dim lBlue As Double = pBlueEntry.Value / 255.0
                
                ' Update current color
                pCurrentColor = New Gdk.RGBA() With {
                    .Red = lRed,
                    .Green = lGreen,
                    .Blue = lBlue,
                    .Alpha = 1.0
                }
                
                ' Convert to HSL
                Dim lHsl As (Double, Double, Double) = RgbToHsl(lRed, lGreen, lBlue)
                pCurrentHue = lHsl.Item1
                pCurrentSaturation = lHsl.Item2
                pCurrentLightness = lHsl.Item3
                
                ' Update UI
                UpdateUI()
                
                ' Fire event
                RaiseEvent ColorChanged(pCurrentColor)
                
            Catch ex As Exception
                Console.WriteLine($"OnRgbValueChanged error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles hex value changes
        ''' </summary>
        Private Sub OnHexChanged(vSender As Object, vArgs As EventArgs)
            If pIsUpdating Then Return
            
            Try
                pIsUpdating = True
                
                Dim lHex As String = pHexEntry.Text.Trim()
                
                ' Add # if missing
                If Not lHex.StartsWith("#") Then
                    lHex = "#" & lHex
                End If
                
                ' Try to parse color
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(lHex) Then
                    pCurrentColor = lColor
                    
                    ' Convert to HSL
                    Dim lHsl As (Double, Double, Double) = RgbToHsl(lColor.Red, lColor.Green, lColor.Blue)
                    pCurrentHue = lHsl.Item1
                    pCurrentSaturation = lHsl.Item2
                    pCurrentLightness = lHsl.Item3
                    
                    ' Update UI
                    UpdateUI()
                    
                    ' Fire event
                    RaiseEvent ColorChanged(pCurrentColor)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnHexChanged error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Updates color from HSL values
        ''' </summary>
        Private Sub UpdateColorFromHsl()
            Try
                ' Convert HSL to RGB
                Dim lRgb As (Double, Double, Double) = HslToRgb(pCurrentHue, pCurrentSaturation, pCurrentLightness)
                
                ' Update current color
                pCurrentColor = New Gdk.RGBA() With {
                    .Red = lRgb.Item1,
                    .Green = lRgb.Item2,
                    .Blue = lRgb.Item3,
                    .Alpha = 1.0
                }
                
                ' Update UI
                UpdateUI()
                
                ' Fire event
                RaiseEvent ColorChanged(pCurrentColor)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateColorFromHsl error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates all UI elements
        ''' </summary>
        Private Sub UpdateUI()
            If pIsUpdating Then Return
            
            Try
                pIsUpdating = True
                
                ' Update HSL entries
                If pHueEntry IsNot Nothing Then pHueEntry.Value = pCurrentHue
                If pSatEntry IsNot Nothing Then pSatEntry.Value = pCurrentSaturation * 255
                If pLumEntry IsNot Nothing Then pLumEntry.Value = pCurrentLightness * 255
                
                ' Update RGB entries
                If pRedEntry IsNot Nothing Then pRedEntry.Value = pCurrentColor.Red * 255
                If pGreenEntry IsNot Nothing Then pGreenEntry.Value = pCurrentColor.Green * 255
                If pBlueEntry IsNot Nothing Then pBlueEntry.Value = pCurrentColor.Blue * 255
                
                ' Update hex entry
                If pHexEntry IsNot Nothing Then
                    pHexEntry.Text = String.Format("#{0:X2}{1:X2}{2:X2}",
                        CInt(pCurrentColor.Red * 255),
                        CInt(pCurrentColor.Green * 255),
                        CInt(pCurrentColor.Blue * 255))
                End If
                
                ' Update preview
                If pColorPreview IsNot Nothing Then pColorPreview.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateUI error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates UI from a color value
        ''' </summary>
        Private Sub UpdateFromColor(vColor As Gdk.RGBA)
            Try
                pIsUpdating = True
                
                pCurrentColor = vColor
                
                ' Convert to HSL
                Dim lHsl As (Double, Double, Double) = RgbToHsl(vColor.Red, vColor.Green, vColor.Blue)
                pCurrentHue = lHsl.Item1
                pCurrentSaturation = lHsl.Item2
                pCurrentLightness = lHsl.Item3
                
                ' Update gradient crosshair position
                UpdateGradientCrosshair()
                
                ' Update all UI elements
                UpdateUI()
                
                ' Redraw gradient and slider
                If pGradientArea IsNot Nothing Then pGradientArea.QueueDraw()
                If pHueSlider IsNot Nothing Then pHueSlider.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFromColor error: {ex.Message}")
            Finally
                pIsUpdating = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates gradient crosshair position based on current saturation and lightness
        ''' </summary>
        Private Sub UpdateGradientCrosshair()
            If pGradientArea Is Nothing Then Return
            
            pGradientCrosshairX = pCurrentSaturation * (pGradientArea.AllocatedWidth - 1)
            pGradientCrosshairY = (1.0 - pCurrentLightness) * (pGradientArea.AllocatedHeight - 1)
        End Sub
        
        ''' <summary>
        ''' Converts HSL to RGB color values
        ''' </summary>
        Private Function HslToRgb(vHue As Double, vSaturation As Double, vLightness As Double) As (Double, Double, Double)
            Dim lH As Double = vHue / 360.0
            Dim lS As Double = vSaturation
            Dim lL As Double = vLightness
            
            Dim lR, lG, lB As Double
            
            If lS = 0 Then
                ' Achromatic
                lR = lL
                lG = lL
                lB = lL
            Else
                Dim lQ As Double = If(lL < 0.5, lL * (1 + lS), lL + lS - lL * lS)
                Dim lP As Double = 2 * lL - lQ
                
                lR = HueToRgb(lP, lQ, lH + 1.0/3.0)
                lG = HueToRgb(lP, lQ, lH)
                lB = HueToRgb(lP, lQ, lH - 1.0/3.0)
            End If
            
            Return (lR, lG, lB)
        End Function
        
        ''' <summary>
        ''' Helper function for HSL to RGB conversion
        ''' </summary>
        Private Function HueToRgb(vP As Double, vQ As Double, vT As Double) As Double
            Dim lT As Double = vT
            If lT < 0 Then lT += 1
            If lT > 1 Then lT -= 1
            
            If lT < 1.0/6.0 Then Return vP + (vQ - vP) * 6 * lT
            If lT < 1.0/2.0 Then Return vQ
            If lT < 2.0/3.0 Then Return vP + (vQ - vP) * (2.0/3.0 - lT) * 6
            
            Return vP
        End Function
        
        ''' <summary>
        ''' Converts RGB to HSL color values
        ''' </summary>
        Private Function RgbToHsl(vRed As Double, vGreen As Double, vBlue As Double) As (Double, Double, Double)
            Dim lMax As Double = Math.Max(vRed, Math.Max(vGreen, vBlue))
            Dim lMin As Double = Math.Min(vRed, Math.Min(vGreen, vBlue))
            Dim lDelta As Double = lMax - lMin
            
            Dim lH, lS, lL As Double
            lL = (lMax + lMin) / 2.0
            
            If lDelta = 0 Then
                ' Achromatic
                lH = 0
                lS = 0
            Else
                lS = If(lL < 0.5, lDelta / (lMax + lMin), lDelta / (2 - lMax - lMin))
                
                If lMax = vRed Then
                    lH = ((vGreen - vBlue) / lDelta + If(vGreen < vBlue, 6, 0)) / 6.0
                ElseIf lMax = vGreen Then
                    lH = ((vBlue - vRed) / lDelta + 2) / 6.0
                Else
                    lH = ((vRed - vGreen) / lDelta + 4) / 6.0
                End If
            End If
            
            Return (lH * 360, lS, lL)
        End Function
        
        ''' <summary>
        ''' Checks if two colors are equal
        ''' </summary>
        Private Function ColorsEqual(vColor1 As Gdk.RGBA, vColor2 As Gdk.RGBA) As Boolean
            Return Math.Abs(vColor1.Red - vColor2.Red) < 0.001 AndAlso
                   Math.Abs(vColor1.Green - vColor2.Green) < 0.001 AndAlso
                   Math.Abs(vColor1.Blue - vColor2.Blue) < 0.001 AndAlso
                   Math.Abs(vColor1.Alpha - vColor2.Alpha) < 0.001
        End Function

        ''' <summary>
        ''' Sets a custom color at the specified index
        ''' </summary>
        ''' <param name="vIndex">Index of the custom color slot (0-15)</param>
        ''' <param name="vColor">The color to set</param>
        Public Sub SetCustomColor(vIndex As Integer, vColor As Gdk.RGBA)
            Try
                If vIndex < 0 OrElse vIndex >= pCustomColors.Count Then Return
                
                ' Update the color in the list
                pCustomColors(vIndex) = vColor
                
                ' Update the visual representation
                If vIndex < pCustomColorAreas.Count Then
                    pCustomColorAreas(vIndex).QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ColorPicker.SetCustomColor error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets a custom color at the specified index
        ''' </summary>
        ''' <param name="vIndex">Index of the custom color slot (0-15)</param>
        ''' <returns>The color at the specified index</returns>
        Public Function GetCustomColor(vIndex As Integer) As Gdk.RGBA
            Try
                If vIndex >= 0 AndAlso vIndex < pCustomColors.Count Then
                    Return pCustomColors(vIndex)
                End If
                
                Return New Gdk.RGBA() With {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1.0}
                
            Catch ex As Exception
                Console.WriteLine($"ColorPicker.GetCustomColor error: {ex.Message}")
                Return New Gdk.RGBA() With {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1.0}
            End Try
        End Function
        
        ''' <summary>
        ''' Clears all custom colors to white
        ''' </summary>
        Public Sub ClearCustomColors()
            Try
                For i As Integer = 0 To pCustomColors.Count - 1
                    pCustomColors(i) = New Gdk.RGBA() With {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1.0}
                Next
                
                ' Redraw all custom color areas
                For Each lArea In pCustomColorAreas
                    lArea.QueueDraw()
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ColorPicker.ClearCustomColors error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
