' PngEditor.vb - PNG file editor with viewing and basic editing
Imports Gtk
Imports Gdk
Imports Cairo
Imports System.IO
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers


Namespace Editors
    Public Class PngEditor
        Inherits Box
        
        ' Private fields
        Private pFilePath As String
        Private pPixbuf As Pixbuf
        Private pOriginalPixbuf As Pixbuf
        Private pIsModified As Boolean = False
        Private pScrolledWindow As ScrolledWindow
        Private pDrawingArea As DrawingArea
        Private pZoomLevel As Double = 1.0
        Private pMinZoom As Double = 0.1
        Private pMaxZoom As Double = 10.0
        Private pShowGrid As Boolean = False
        Private pGridSize As Integer = 10
        Private pShowTransparency As Boolean = True
        Private pStatusLabel As Label
        Private pZoomCombo As ComboBoxText
        Private pSettingsManager As SettingsManager
        
        ' Events
        Public Event Modified(vIsModified As Boolean)
        Public Event SaveRequested()
        
        ' Properties
        Public ReadOnly Property FilePath As String
            Get
                Return pFilePath
            End Get
        End Property
        
        Public Property IsModified As Boolean
            Get
                Return pIsModified
            End Get
            Set(Value As Boolean)
                If pIsModified <> Value Then
                    pIsModified = Value
                    RaiseEvent Modified(pIsModified)
                End If
            End Set
        End Property
        
        Public Sub New(vFilePath As String, vSettingsManager As SettingsManager)
            MyBase.New(Orientation.Vertical, 0)
            
            pFilePath = vFilePath
            pSettingsManager = vSettingsManager
            
            BuildUI()
            LoadImage()
        End Sub
        
        Private Sub BuildUI()
            ' Create toolbar
            Dim lToolbar As Widget = CreateToolbar()
            PackStart(lToolbar, False, False, 0)
            
            ' Create scrolled window
            pScrolledWindow = New ScrolledWindow()
            pScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            ' Create drawing area
            pDrawingArea = New DrawingArea()
            pDrawingArea.CanFocus = True
            pDrawingArea.Events = pDrawingArea.Events Or EventMask.ScrollMask Or 
                                 EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or 
                                 EventMask.PointerMotionMask
            
            ' Add to scrolled window with viewport for proper scrolling
            Dim lViewport As New Viewport()
            lViewport.Add(pDrawingArea)
            pScrolledWindow.Add(lViewport)
            
            PackStart(pScrolledWindow, True, True, 0)
            
            ' Create status bar
            Dim lStatusBox As New Box(Orientation.Horizontal, 6)
            lStatusBox.BorderWidth = 3
            
            pStatusLabel = New Label("Ready")
            pStatusLabel.Halign = Align.Start
            lStatusBox.PackStart(pStatusLabel, True, True, 0)
            
            PackStart(lStatusBox, False, False, 0)
            
            ' Connect events
            AddHandler pDrawingArea.Drawn, AddressOf OnDrawingAreaDrawn
            AddHandler pDrawingArea.ScrollEvent, AddressOf OnScrollEvent
            AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnButtonPress
            
            ShowAll()
        End Sub
        
        Private Function CreateToolbar() As Widget
            Dim lToolbar As New Toolbar()
            lToolbar.ToolbarStyle = ToolbarStyle.Icons
            lToolbar.IconSize = IconSize.SmallToolbar
            
            ' Save button
            Dim lSaveButton As New ToolButton(Nothing, "Save")
            lSaveButton.IconWidget = Image.NewFromIconName("document-Save", IconSize.SmallToolbar)
            lSaveButton.TooltipText = "Save image (Ctrl+S)"
            AddHandler lSaveButton.Clicked, AddressOf OnSave
            lToolbar.Insert(lSaveButton, -1)
            
            ' Save As button
            Dim lSaveAsButton As New ToolButton(Nothing, "Save As")
            lSaveAsButton.IconWidget = Image.NewFromIconName("document-Save-as", IconSize.SmallToolbar)
            lSaveAsButton.TooltipText = "Save image as..."
            AddHandler lSaveAsButton.Clicked, AddressOf OnSaveAs
            lToolbar.Insert(lSaveAsButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Zoom controls
            Dim lZoomOutButton As New ToolButton(Nothing, "Zoom Out")
            lZoomOutButton.IconWidget = Image.NewFromIconName("zoom-out", IconSize.SmallToolbar)
            lZoomOutButton.TooltipText = "Zoom out"
            AddHandler lZoomOutButton.Clicked, AddressOf OnZoomOut
            lToolbar.Insert(lZoomOutButton, -1)
            
            ' Zoom combo
            Dim lZoomItem As New ToolItem()
            pZoomCombo = New ComboBoxText()
            pZoomCombo.AppendText("10%")
            pZoomCombo.AppendText("25%")
            pZoomCombo.AppendText("50%")
            pZoomCombo.AppendText("75%")
            pZoomCombo.AppendText("100%")
            pZoomCombo.AppendText("150%")
            pZoomCombo.AppendText("200%")
            pZoomCombo.AppendText("400%")
            pZoomCombo.AppendText("800%")
            pZoomCombo.Active = 4 ' 100%
            AddHandler pZoomCombo.Changed, AddressOf OnZoomChanged
            lZoomItem.Add(pZoomCombo)
            lToolbar.Insert(lZoomItem, -1)
            
            Dim lZoomInButton As New ToolButton(Nothing, "Zoom in")
            lZoomInButton.IconWidget = Image.NewFromIconName("zoom-in", IconSize.SmallToolbar)
            lZoomInButton.TooltipText = "Zoom in"
            AddHandler lZoomInButton.Clicked, AddressOf OnZoomIn
            lToolbar.Insert(lZoomInButton, -1)
            
            Dim lZoomFitButton As New ToolButton(Nothing, "Zoom Fit")
            lZoomFitButton.IconWidget = Image.NewFromIconName("zoom-fit-best", IconSize.SmallToolbar)
            lZoomFitButton.TooltipText = "Fit to window"
            AddHandler lZoomFitButton.Clicked, AddressOf OnZoomFit
            lToolbar.Insert(lZoomFitButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Toggle grid
            Dim lGridButton As New ToggleToolButton()
            lGridButton.IconWidget = Image.NewFromIconName("view-grid", IconSize.SmallToolbar)
            lGridButton.TooltipText = "Show grid"
            AddHandler lGridButton.Toggled, AddressOf OnToggleGrid
            lToolbar.Insert(lGridButton, -1)
            
            ' Toggle transparency
            Dim lTransparencyButton As New ToggleToolButton()
            lTransparencyButton.IconWidget = Image.NewFromIconName("dialog-information", IconSize.SmallToolbar)
            lTransparencyButton.TooltipText = "Show transparency Pattern"
            lTransparencyButton.Active = True
            AddHandler lTransparencyButton.Toggled, AddressOf OnToggleTransparency
            lToolbar.Insert(lTransparencyButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Image operations
            Dim lRotateLeftButton As New ToolButton(Nothing, "Rotate Left")
            lRotateLeftButton.IconWidget = Image.NewFromIconName("object-rotate-left", IconSize.SmallToolbar)
            lRotateLeftButton.TooltipText = "Rotate 90° counter-clockwise"
            AddHandler lRotateLeftButton.Clicked, AddressOf OnRotateLeft
            lToolbar.Insert(lRotateLeftButton, -1)
            
            Dim lRotateRightButton As New ToolButton(Nothing, "Rotate Right")
            lRotateRightButton.IconWidget = Image.NewFromIconName("object-rotate-right", IconSize.SmallToolbar)
            lRotateRightButton.TooltipText = "Rotate 90° clockwise"
            AddHandler lRotateRightButton.Clicked, AddressOf OnRotateRight
            lToolbar.Insert(lRotateRightButton, -1)
            
            Dim lFlipHButton As New ToolButton(Nothing, "Flip H")
            lFlipHButton.IconWidget = Image.NewFromIconName("object-flip-horizontal", IconSize.SmallToolbar)
            lFlipHButton.TooltipText = "Flip horizontally"
            AddHandler lFlipHButton.Clicked, AddressOf OnFlipHorizontal
            lToolbar.Insert(lFlipHButton, -1)
            
            Dim lFlipVButton As New ToolButton(Nothing, "Flip V")
            lFlipVButton.IconWidget = Image.NewFromIconName("object-flip-vertical", IconSize.SmallToolbar)
            lFlipVButton.TooltipText = "Flip vertically"
            AddHandler lFlipVButton.Clicked, AddressOf OnFlipVertical
            lToolbar.Insert(lFlipVButton, -1)
            
            lToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Resize button
            Dim lResizeButton As New ToolButton(Nothing, "Resize")
            lResizeButton.IconWidget = Image.NewFromIconName("view-fullscreen", IconSize.SmallToolbar)
            lResizeButton.TooltipText = "Resize image..."
            AddHandler lResizeButton.Clicked, AddressOf OnResize
            lToolbar.Insert(lResizeButton, -1)
            
            Return lToolbar
        End Function
        
        Private Sub LoadImage()
            Try
                If File.Exists(pFilePath) Then
                    pPixbuf = New Pixbuf(pFilePath)
                    pOriginalPixbuf = pPixbuf.Copy()
                    
                    UpdateDrawingAreaSize()
                    UpdateStatusBar()
                    
                    pIsModified = False
                End If
            Catch ex As Exception
                Console.WriteLine($"error loading PNG file: {ex.Message}")
                ShowError($"error loading PNG file: {ex.Message}")
            End Try
        End Sub
        
        Public Sub SaveFile()
            Try
                If pPixbuf IsNot Nothing Then
                    pPixbuf.Save(pFilePath, "png")
                    pOriginalPixbuf = pPixbuf.Copy()
                    IsModified = False
                    ShowMessage("Image saved successfully")
                End If
            Catch ex As Exception
                Console.WriteLine($"error saving PNG file: {ex.Message}")
                ShowError($"error saving PNG file: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateDrawingAreaSize()
            If pPixbuf IsNot Nothing Then
                Dim lWidth As Integer = CInt(pPixbuf.Width * pZoomLevel)
                Dim lHeight As Integer = CInt(pPixbuf.Height * pZoomLevel)
                pDrawingArea.SetSizeRequest(lWidth, lHeight)
                pDrawingArea.QueueDraw()
            End If
        End Sub
        
        Private Sub UpdateStatusBar()
            If pPixbuf IsNot Nothing Then
                Dim lInfo As String = $"{pPixbuf.Width}x{pPixbuf.Height} pixels, "
                lInfo &= If(pPixbuf.HasAlpha, "RGBA", "RGB") & ", "
                lInfo &= $"Zoom: {CInt(pZoomLevel * 100)}%"
                pStatusLabel.Text = lInfo
            End If
        End Sub
        
        Private Sub OnDrawingAreaDrawn(vSender As Object, vArgs As DrawnArgs)
            Dim lContext As Context = vArgs.Cr
            
            If pPixbuf Is Nothing Then Return
            
            ' Save context state
            lContext.Save()
            
            ' Apply zoom
            lContext.Scale(pZoomLevel, pZoomLevel)
            
            ' Draw transparency pattern if needed
            If pShowTransparency AndAlso pPixbuf.HasAlpha Then
                DrawTransparencyPattern(lContext, pPixbuf.Width, pPixbuf.Height)
            End If
            
            ' Draw the image
            Gdk.CairoHelper.SetSourcePixbuf(lContext, pPixbuf, 0, 0)
            lContext.Paint()
            
            ' Draw grid if enabled
            If pShowGrid Then
                DrawGrid(lContext, pPixbuf.Width, pPixbuf.Height)
            End If
            
            ' Restore context state
            lContext.Restore()
            
            vArgs.RetVal = True
        End Sub
        
        Private Sub DrawTransparencyPattern(vContext As Context, vWidth As Integer, vHeight As Integer)
            Dim lCheckSize As Integer = 10
            
            vContext.SetSourceRgb(0.8, 0.8, 0.8)
            vContext.Rectangle(0, 0, vWidth, vHeight)
            vContext.Fill()
            
            vContext.SetSourceRgb(0.6, 0.6, 0.6)
            
            For y As Integer = 0 To vHeight Step lCheckSize
                For x As Integer = 0 To vWidth Step lCheckSize
                    If ((x \ lCheckSize) + (y \ lCheckSize)) Mod 2 = 0 Then
                        vContext.Rectangle(x, y, lCheckSize, lCheckSize)
                    End If
                Next
            Next
            
            vContext.Fill()
        End Sub
        
        Private Sub DrawGrid(vContext As Context, vWidth As Integer, vHeight As Integer)
            vContext.SetSourceRgba(0, 0, 0, 0.2)
            vContext.LineWidth = 1.0 / pZoomLevel
            
            ' Vertical lines
            For x As Integer = 0 To vWidth Step pGridSize
                vContext.MoveTo(x, 0)
                vContext.LineTo(x, vHeight)
            Next
            
            ' Horizontal lines
            For y As Integer = 0 To vHeight Step pGridSize
                vContext.MoveTo(0, y)
                vContext.LineTo(vWidth, y)
            Next
            
            vContext.Stroke()
        End Sub
        
        Private Shadows Sub OnScrollEvent(vSender As Object, vArgs As ScrollEventArgs)
            ' Ctrl + Scroll for zoom
            If (vArgs.Event.State And ModifierType.ControlMask) = ModifierType.ControlMask Then
                If vArgs.Event.Direction = ScrollDirection.Up Then
                    ZoomIn()
                ElseIf vArgs.Event.Direction = ScrollDirection.Down Then
                    ZoomOut()
                End If
                vArgs.RetVal = True
            End If
        End Sub
        
        Private Sub OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            ' Could implement pixel editing here in the future
            pDrawingArea.GrabFocus()
        End Sub
        
        Private Sub OnSave(vSender As Object, vE As EventArgs)
            SaveFile()
            RaiseEvent SaveRequested()
        End Sub
        
        Private Sub OnSaveAs(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Save Image As",
                    Nothing,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Save", ResponseType.Accept
                )
                
                ' Add filters
                Dim lPngFilter As New FileFilter()
                lPngFilter.Name = "PNG Images (*.png)"
                lPngFilter.AddMimeType("image/png")
                lPngFilter.AddPattern("*.png")
                lDialog.AddFilter(lPngFilter)
                
                lDialog.CurrentName = System.IO.Path.GetFileName(pFilePath)
                lDialog.DoOverwriteConfirmation = True
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    pPixbuf.Save(lDialog.FileName, "png")
                    ShowMessage($"Image saved as: {System.IO.Path.GetFileName(lDialog.FileName)}")
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                ShowError($"error saving image: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnZoomIn(vSender As Object, vE As EventArgs)
            ZoomIn()
        End Sub
        
        Private Sub OnZoomOut(vSender As Object, vE As EventArgs)
            ZoomOut()
        End Sub
        
        Private Sub OnZoomFit(vSender As Object, vE As EventArgs)
            If pPixbuf Is Nothing Then Return
            
            ' Calculate zoom to fit
            Dim lViewWidth As Integer = pScrolledWindow.AllocatedWidth - 20
            Dim lViewHeight As Integer = pScrolledWindow.AllocatedHeight - 20
            
            Dim lZoomX As Double = lViewWidth / pPixbuf.Width
            Dim lZoomY As Double = lViewHeight / pPixbuf.Height
            
            pZoomLevel = Math.Min(lZoomX, lZoomY)
            pZoomLevel = Math.Max(pMinZoom, Math.Min(pMaxZoom, pZoomLevel))
            
            UpdateZoomCombo()
            UpdateDrawingAreaSize()
            UpdateStatusBar()
        End Sub
        
        Private Sub OnZoomChanged(vSender As Object, vE As EventArgs)
            Dim lText As String = pZoomCombo.ActiveText
            If String.IsNullOrEmpty(lText) Then Return
            
            lText = lText.Replace("%", "")
            Dim lZoom As Double
            If Double.TryParse(lText, lZoom) Then
                pZoomLevel = lZoom / 100.0
                UpdateDrawingAreaSize()
                UpdateStatusBar()
            End If
        End Sub
        
        Private Sub ZoomIn()
            pZoomLevel = Math.Min(pZoomLevel * 1.25, pMaxZoom)
            UpdateZoomCombo()
            UpdateDrawingAreaSize()
            UpdateStatusBar()
        End Sub
        
        Private Sub ZoomOut()
            pZoomLevel = Math.Max(pZoomLevel / 1.25, pMinZoom)
            UpdateZoomCombo()
            UpdateDrawingAreaSize()
            UpdateStatusBar()
        End Sub
        
        Private Sub UpdateZoomCombo()
            Dim lZoomPercent As Integer = CInt(pZoomLevel * 100)
            pZoomCombo.RemoveAll()
            
            ' Add standard zoom levels
            Dim lStandardLevels() As Integer = {10, 25, 50, 75, 100, 150, 200, 400, 800}
            Dim lFoundStandard As Boolean = False
            
            For Each lLevel In lStandardLevels
                pZoomCombo.AppendText($"{lLevel}%")
                If lLevel = lZoomPercent Then
                    lFoundStandard = True
                End If
            Next
            
            ' Add current zoom if not standard
            If Not lFoundStandard Then
                pZoomCombo.AppendText($"{lZoomPercent}%")
            End If
            
            ' Select current zoom
            For i As Integer = 0 To pZoomCombo.Model.IterNChildren() - 1
                Dim lIter As TreeIter = Nothing
                If pZoomCombo.Model.IterNthChild(lIter, Nothing, i) Then
                    If pZoomCombo.Model.GetValue(lIter, 0).ToString() = $"{lZoomPercent}%" Then
                        pZoomCombo.Active = i
                        Exit For
                    End If
                End If
            Next
        End Sub
        
        Private Sub OnToggleGrid(vSender As Object, vE As EventArgs)
            pShowGrid = CType(vSender, ToggleToolButton).Active
            pDrawingArea.QueueDraw()
        End Sub
        
        Private Sub OnToggleTransparency(vSender As Object, vE As EventArgs)
            pShowTransparency = CType(vSender, ToggleToolButton).Active
            pDrawingArea.QueueDraw()
        End Sub
        
        Private Sub OnRotateLeft(vSender As Object, vE As EventArgs)
            If pPixbuf Is Nothing Then Return
            
            pPixbuf = pPixbuf.RotateSimple(PixbufRotation.Counterclockwise)
            IsModified = True
            UpdateDrawingAreaSize()
            UpdateStatusBar()
        End Sub
        
        Private Sub OnRotateRight(vSender As Object, vE As EventArgs)
            If pPixbuf Is Nothing Then Return
            
            pPixbuf = pPixbuf.RotateSimple(PixbufRotation.Clockwise)
            IsModified = True
            UpdateDrawingAreaSize()
            UpdateStatusBar()
        End Sub
        
        Private Sub OnFlipHorizontal(vSender As Object, vE As EventArgs)
            If pPixbuf Is Nothing Then Return
            
            pPixbuf = pPixbuf.RotateSimple(Gdk.PixbufRotation.Upsidedown)
            'pPixbuf = pPixbuf.RotateSimple(Gdk.PixbufRotation.Upsidedown)

            IsModified = True
            pDrawingArea.QueueDraw()
        End Sub
        
        Private Sub OnFlipVertical(vSender As Object, vE As EventArgs)
            If pPixbuf Is Nothing Then Return
            
            ' TODO: Implement
            IsModified = True
            pDrawingArea.QueueDraw()
        End Sub
        
        Private Sub OnResize(vSender As Object, vE As EventArgs)
            If pPixbuf Is Nothing Then Return
            
            Dim lDialog As New ResizeImageDialog(Nothing, pPixbuf.Width, pPixbuf.Height)
            
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                Try
                    Dim lNewPixbuf As Pixbuf = pPixbuf.ScaleSimple(
                        lDialog.NewWidth,
                        lDialog.NewHeight,
                        If(lDialog.HighQuality, InterpType.Bilinear, InterpType.Nearest)
                    )
                    
                    pPixbuf = lNewPixbuf
                    IsModified = True
                    UpdateDrawingAreaSize()
                    UpdateStatusBar()
                    
                Catch ex As Exception
                    ShowError($"error resizing image: {ex.Message}")
                End Try
            End If
            
            lDialog.Destroy()
        End Sub
        
        Private Sub ShowMessage(vMessage As String)
            Console.WriteLine($"PngEditor: {vMessage}")
            ' TODO: Connect to main window status bar
        End Sub
        
        Private Sub ShowError(vMessage As String)
            Dim lDialog As New MessageDialog(
                Nothing,
                DialogFlags.Modal,
                MessageType.Error,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub
    End Class
    
    ' Dialog for resizing images
    Public Class ResizeImageDialog
        Inherits Dialog
        
        Private pWidthSpinButton As SpinButton
        Private pHeightSpinButton As SpinButton
        Private pMaintainAspectCheck As CheckButton
        Private pHighQualityCheck As CheckButton
        Private pOriginalWidth As Integer
        Private pOriginalHeight As Integer
        Private pAspectRatio As Double
        Private pUpdating As Boolean = False
        
        Public ReadOnly Property NewWidth As Integer
            Get
                Return CInt(pWidthSpinButton.Value)
            End Get
        End Property
        
        Public ReadOnly Property NewHeight As Integer
            Get
                Return CInt(pHeightSpinButton.Value)
            End Get
        End Property
        
        Public ReadOnly Property HighQuality As Boolean
            Get
                Return pHighQualityCheck.Active
            End Get
        End Property
        
        Public Sub New(vParent As Gtk.Window, vWidth As Integer, vHeight As Integer)
            MyBase.New("Resize Image", vParent, DialogFlags.Modal)
            
            pOriginalWidth = vWidth
            pOriginalHeight = vHeight
            pAspectRatio = vWidth / CDbl(vHeight)
            
            SetDefaultSize(300, 200)
            SetPosition(WindowPosition.CenterOnParent)
            
            BuildUI()
            
            AddButton("Cancel", ResponseType.Cancel)
            AddButton("Resize", ResponseType.Ok)
            
            ShowAll()
        End Sub
        
        Private Sub BuildUI()
            Dim lVBox As New Box(Orientation.Vertical, 6)
            lVBox.BorderWidth = 10
            
            ' Current size label
            Dim lCurrentLabel As New Label($"current size: {pOriginalWidth} x {pOriginalHeight} pixels")
            lCurrentLabel.Halign = Align.Start
            lVBox.PackStart(lCurrentLabel, False, False, 0)
            
            lVBox.PackStart(New Separator(Orientation.Horizontal), False, False, 6)
            
            ' Size controls
            Dim lSizeGrid As New Grid()
            lSizeGrid.RowSpacing = 6
            lSizeGrid.ColumnSpacing = 12
            
            ' Width
            Dim lWidthLabel As New Label("Width:")
            lWidthLabel.Halign = Align.End
            lSizeGrid.Attach(lWidthLabel, 0, 0, 1, 1)
            
            pWidthSpinButton = New SpinButton(1, 10000, 1)
            pWidthSpinButton.Value = pOriginalWidth
            AddHandler pWidthSpinButton.ValueChanged, AddressOf OnWidthChanged
            lSizeGrid.Attach(pWidthSpinButton, 1, 0, 1, 1)
            
            Dim lWidthPixelsLabel As New Label("pixels")
            lWidthPixelsLabel.Halign = Align.Start
            lSizeGrid.Attach(lWidthPixelsLabel, 2, 0, 1, 1)
            
            ' Height
            Dim lHeightLabel As New Label("Height:")
            lHeightLabel.Halign = Align.End
            lSizeGrid.Attach(lHeightLabel, 0, 1, 1, 1)
            
            pHeightSpinButton = New SpinButton(1, 10000, 1)
            pHeightSpinButton.Value = pOriginalHeight
            AddHandler pHeightSpinButton.ValueChanged, AddressOf OnHeightChanged
            lSizeGrid.Attach(pHeightSpinButton, 1, 1, 1, 1)
            
            Dim lHeightPixelsLabel As New Label("pixels")
            lHeightPixelsLabel.Halign = Align.Start
            lSizeGrid.Attach(lHeightPixelsLabel, 2, 1, 1, 1)
            
            lVBox.PackStart(lSizeGrid, False, False, 0)
            
            ' Options
            pMaintainAspectCheck = New CheckButton("Maintain aspect ratio")
            pMaintainAspectCheck.Active = True
            lVBox.PackStart(pMaintainAspectCheck, False, False, 0)
            
            pHighQualityCheck = New CheckButton("High quality (slower)")
            pHighQualityCheck.Active = True
            lVBox.PackStart(pHighQualityCheck, False, False, 0)
            
            ContentArea.Add(lVBox)
        End Sub
        
        Private Sub OnWidthChanged(vSender As Object, vE As EventArgs)
            If pUpdating Then Return
            
            If pMaintainAspectCheck.Active Then
                pUpdating = True
                pHeightSpinButton.Value = Math.Round(pWidthSpinButton.Value / pAspectRatio)
                pUpdating = False
            End If
        End Sub
        
        Private Sub OnHeightChanged(vSender As Object, vE As EventArgs)
            If pUpdating Then Return
            
            If pMaintainAspectCheck.Active Then
                pUpdating = True
                pWidthSpinButton.Value = Math.Round(pHeightSpinButton.Value * pAspectRatio)
                pUpdating = False
            End If
        End Sub
    End Class
End Namespace
 
