' Dialogs/NewPngDialog.vb - Dialog for creating new PNG files
Imports Gtk
Imports Gdk
Imports System
Imports System.IO

Namespace Dialogs

    Public Class NewPngDialog
        Inherits Dialog
        
        ' Private fields
        Private pFileNameEntry As Entry
        Private pWidthSpinButton As SpinButton
        Private pHeightSpinButton As SpinButton
        Private pTransparentRadio As RadioButton
        Private pWhiteRadio As RadioButton
        Private pBlackRadio As RadioButton
        Private pCustomRadio As RadioButton
        Private pColorButton As ColorButton
        Private pPresetCombo As ComboBoxText
        Private pProjectDirectory As String
        
        ' Preset sizes
        Private Structure PresetSize
            Public Name As String
            Public Width As Integer
            Public Height As Integer
            
            Public Sub New(vName As String, vWidth As Integer, vHeight As Integer)
                Name = vName
                Width = vWidth
                Height = vHeight
            End Sub
        End Structure
        
        Private pPresets() As PresetSize = {
            New PresetSize("IcOn 16x16", 16, 16),
            New PresetSize("IcOn 32x32", 32, 32),
            New PresetSize("IcOn 48x48", 48, 48),
            New PresetSize("IcOn 64x64", 64, 64),
            New PresetSize("IcOn 128x128", 128, 128),
            New PresetSize("IcOn 256x256", 256, 256),
            New PresetSize("Toolbar Icon 24x24", 24, 24),
            New PresetSize("Web Banner 468x60", 468, 60),
            New PresetSize("Web Banner 728x90", 728, 90),
            New PresetSize("Square 512x512", 512, 512),
            New PresetSize("HD 1920x1080", 1920, 1080),
            New PresetSize("4K 3840x2160", 3840, 2160),
            New PresetSize("A4 300dpi 2480x3508", 2480, 3508),
            New PresetSize("Custom", 0, 0)
        }
        
        ' Properties
        Public ReadOnly Property FileName As String
            Get
                Return pFileNameEntry.Text
            End Get
        End Property
        
        Public ReadOnly Property ImageWidth As Integer
            Get
                Return CInt(pWidthSpinButton.Value)
            End Get
        End Property
        
        Public ReadOnly Property ImageHeight As Integer
            Get
                Return CInt(pHeightSpinButton.Value)
            End Get
        End Property
        
        Public ReadOnly Property IsTransparent As Boolean
            Get
                Return pTransparentRadio.Active
            End Get
        End Property
        
        Public ReadOnly Property BackgroundColor As RGBA
            Get
                If pTransparentRadio.Active Then
                    Return New RGBA() With {.Red = 0, .Green = 0, .Blue = 0, .Alpha = 0}
                ElseIf pWhiteRadio.Active Then
                    Return New RGBA() With {.Red = 1, .Green = 1, .Blue = 1, .Alpha = 1}
                ElseIf pBlackRadio.Active Then
                    Return New RGBA() With {.Red = 0, .Green = 0, .Blue = 0, .Alpha = 1}
                Else
                    Return pColorButton.Rgba
                End If
            End Get
        End Property
        
        Public ReadOnly Property FullPath As String
            Get
                Return System.IO.Path.Combine(pProjectDirectory, FileName)
            End Get
        End Property
        
        Public Sub New(vParent As Gtk.Window, vProjectDirectory As String)
            MyBase.New("Create New PNG Image", vParent, DialogFlags.Modal)
            
            pProjectDirectory = vProjectDirectory
            
            SetDefaultSize(450, 400)
            SetPosition(WindowPosition.CenterOnParent)
            BorderWidth = 10
            
            BuildUI()
            
            AddButton("Cancel", ResponseType.Cancel)
            AddButton("Create", ResponseType.Ok)
            
            ' Set default button
            DefaultResponse = ResponseType.Ok
            
            ShowAll()
        End Sub
        
        Private Sub BuildUI()
            Dim lVBox As New Box(Orientation.Vertical, 10)
            
            ' File name section
            Dim lFileFrame As New Frame("File Name")
            Dim lFileBox As New Box(Orientation.Horizontal, 6)
            lFileBox.BorderWidth = 10
            
            pFileNameEntry = New Entry()
            pFileNameEntry.Text = "NewImage.png"
            pFileNameEntry.ActivatesDefault = True
            AddHandler pFileNameEntry.Changed, AddressOf OnFileNameChanged
            lFileBox.PackStart(pFileNameEntry, True, True, 0)
            
            lFileFrame.Add(lFileBox)
            lVBox.PackStart(lFileFrame, False, False, 0)
            
            ' Size section
            Dim lSizeFrame As New Frame("Image Size")
            Dim lSizeVBox As New Box(Orientation.Vertical, 6)
            lSizeVBox.BorderWidth = 10
            
            ' Preset combo
            Dim lPresetBox As New Box(Orientation.Horizontal, 6)
            lPresetBox.PackStart(New Label("Preset:"), False, False, 0)
            
            pPresetCombo = New ComboBoxText()
            For Each lPreset In pPresets
                pPresetCombo.AppendText(lPreset.Name)
            Next
            pPresetCombo.Active = 0 ' Select first preset
            AddHandler pPresetCombo.Changed, AddressOf OnPresetChanged
            lPresetBox.PackStart(pPresetCombo, True, True, 0)
            
            lSizeVBox.PackStart(lPresetBox, False, False, 0)
            
            ' Custom size controls
            Dim lSizeGrid As New Grid()
            lSizeGrid.RowSpacing = 6
            lSizeGrid.ColumnSpacing = 12
            lSizeGrid.MarginTop = 6
            
            ' Width
            Dim lWidthLabel As New Label("Width:")
            lWidthLabel.Halign = Align.End
            lSizeGrid.Attach(lWidthLabel, 0, 0, 1, 1)
            
            pWidthSpinButton = New SpinButton(1, 10000, 1)
            pWidthSpinButton.Value = 16
            AddHandler pWidthSpinButton.ValueChanged, AddressOf OnSizeChanged
            lSizeGrid.Attach(pWidthSpinButton, 1, 0, 1, 1)
            
            Dim lWidthPixelsLabel As New Label("pixels")
            lWidthPixelsLabel.Halign = Align.Start
            lSizeGrid.Attach(lWidthPixelsLabel, 2, 0, 1, 1)
            
            ' Height
            Dim lHeightLabel As New Label("Height:")
            lHeightLabel.Halign = Align.End
            lSizeGrid.Attach(lHeightLabel, 0, 1, 1, 1)
            
            pHeightSpinButton = New SpinButton(1, 10000, 1)
            pHeightSpinButton.Value = 16
            AddHandler pHeightSpinButton.ValueChanged, AddressOf OnSizeChanged
            lSizeGrid.Attach(pHeightSpinButton, 1, 1, 1, 1)
            
            Dim lHeightPixelsLabel As New Label("pixels")
            lHeightPixelsLabel.Halign = Align.Start
            lSizeGrid.Attach(lHeightPixelsLabel, 2, 1, 1, 1)
            
            lSizeVBox.PackStart(lSizeGrid, False, False, 0)
            lSizeFrame.Add(lSizeVBox)
            lVBox.PackStart(lSizeFrame, False, False, 0)
            
            ' Background section
            Dim lBgFrame As New Frame("Background")
            Dim lBgVBox As New Box(Orientation.Vertical, 6)
            lBgVBox.BorderWidth = 10
            
            pTransparentRadio = New RadioButton("Transparent")
            pTransparentRadio.Active = True
            lBgVBox.PackStart(pTransparentRadio, False, False, 0)
            
            pWhiteRadio = New RadioButton(pTransparentRadio, "White")
            lBgVBox.PackStart(pWhiteRadio, False, False, 0)
            
            pBlackRadio = New RadioButton(pTransparentRadio, "Black")
            lBgVBox.PackStart(pBlackRadio, False, False, 0)
            
            Dim lCustomBox As New Box(Orientation.Horizontal, 6)
            pCustomRadio = New RadioButton(pTransparentRadio, "Custom:")
            lCustomBox.PackStart(pCustomRadio, False, False, 0)
            
            pColorButton = New ColorButton()
            pColorButton.Rgba = New RGBA() With {.Red = 0.5, .Green = 0.5, .Blue = 0.5, .Alpha = 1.0}
            pColorButton.Sensitive = False
            AddHandler pColorButton.ColorSet, Sub() pCustomRadio.Active = True
            lCustomBox.PackStart(pColorButton, False, False, 0)
            
            lBgVBox.PackStart(lCustomBox, False, False, 0)
            
            ' Connect radio button events
            AddHandler pCustomRadio.Toggled, Sub()
                pColorButton.Sensitive = pCustomRadio.Active
            End Sub
            
            lBgFrame.Add(lBgVBox)
            lVBox.PackStart(lBgFrame, False, False, 0)
            
            ' Info label
            Dim lInfoLabel As New Label()
            lInfoLabel.Markup = "<small><i>the image will be Created in the current project folder.</i></small>"
            lInfoLabel.Halign = Align.Start
            lVBox.PackEnd(lInfoLabel, False, False, 0)
            
            ContentArea.Add(lVBox)
        End Sub
        
        Private Sub OnFileNameChanged(vSender As Object, vE As EventArgs)
            ' Ensure .png extension
            Dim lFileName As String = pFileNameEntry.Text
            If Not String.IsNullOrEmpty(lFileName) AndAlso Not lFileName.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase) Then
                ' Don't add extension while user is still typing
                If Not lFileName.Contains(".") Then
                    ' User hasn't started typing an extension yet
                    Return
                End If
            End If
            
            ' Validate filename
            ValidateFileName()
        End Sub
        
        Private Sub ValidateFileName()
            Dim lFileName As String = pFileNameEntry.Text
            Dim lIsValid As Boolean = True
            
            If String.IsNullOrWhiteSpace(lFileName) Then
                lIsValid = False
            Else
                ' Check for invalid characters
                Dim lInvalidChars() As Char = System.IO.Path.GetInvalidFileNameChars()
                For Each lChar In lInvalidChars
                    If lFileName.Contains(lChar) Then
                        lIsValid = False
                        Exit For
                    End If
                Next
                
                ' Check if file already exists
                If lIsValid AndAlso File.Exists(FullPath) Then
                    ' Still valid but show warning
                    pFileNameEntry.TooltipText = "Warning: File already exists and will be overwritten"
                    ' Could change entry background color here
                Else
                    pFileNameEntry.TooltipText = ""
                End If
            End If
            
            ' Enable/disable OK button
            Dim lOkButton As Widget = Me.GetWidgetForResponse(ResponseType.Ok)
            If lOkButton IsNot Nothing Then
                lOkButton.Sensitive = lIsValid
            End If
        End Sub
        
        Private Sub OnPresetChanged(vSender As Object, vE As EventArgs)
            Dim lIndex As Integer = pPresetCombo.Active
            If lIndex >= 0 AndAlso lIndex < pPresets.Length Then
                Dim lPreset As PresetSize = pPresets(lIndex)
                
                If lPreset.Width > 0 AndAlso lPreset.Height > 0 Then
                    pWidthSpinButton.Value = lPreset.Width
                    pHeightSpinButton.Value = lPreset.Height
                End If
                
                ' Enable/disable size controls for custom
                Dim lIsCustom As Boolean = (lPreset.Name = "Custom")
                pWidthSpinButton.Sensitive = lIsCustom
                pHeightSpinButton.Sensitive = lIsCustom
            End If
        End Sub
        
        Private Sub OnSizeChanged(vSender As Object, vE As EventArgs)
            ' If size changed manually, switch to Custom preset
            For i As Integer = 0 To pPresets.Length - 1
                If pPresets(i).Name = "Custom" Then
                    pPresetCombo.Active = i
                    Exit For
                End If
            Next
        End Sub
        
        Public Function CreateImage() As Boolean
            Try
                ' Create pixbuf with specified size and background
                Dim lPixbuf As Pixbuf
                
                If IsTransparent Then
                    ' Create with alpha channel
                    lPixbuf = New Pixbuf(Colorspace.Rgb, True, 8, ImageWidth, ImageHeight)
                    lPixbuf.Fill(0) ' Transparent
                Else
                    ' Create without alpha channel
                    lPixbuf = New Pixbuf(Colorspace.Rgb, False, 8, ImageWidth, ImageHeight)
                    
                    ' Fill with background color
                    Dim lColor As RGBA = BackgroundColor
                    Dim lPixel As UInteger = CUInt((CInt(lColor.Red * 255) << 24) Or _
                                                  (CInt(lColor.Green * 255) << 16) Or _
                                                  (CInt(lColor.Blue * 255) << 8) Or _
                                                  255)
                    lPixbuf.Fill(lPixel)
                End If
                
                ' Save to file
                lPixbuf.Save(FullPath, "png")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error creating PNG file: {ex.Message}")
                Return False
            End Try
        End Function
    End Class
End Namespace
 
