' Widgets/ThemeEditor.vb - Theme editor panel for creating and editing themes
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Diagnostics
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Editors
Imports SimpleIDE.Managers

Namespace Widgets
    
    Public Class ThemeEditor
        Inherits Box
        
        ' Private fields
        Private pThemeManager As ThemeManager
        Private pSettingsManager As SettingsManager
        Private pCurrentTheme As EditorTheme
        Private pModified As Boolean
        Private pIsUpdatingUI As Boolean = False
        
        ' UI Components
        Private pThemeListBox As ListBox
        Private pThemeListStore As ListStore
        Private pPropertyListBox As ListBox
        Private pColorGrid As Grid  ' Custom Color grid
        Private pRgbEntry As Entry
        Private pHexEntry As Entry
        Private pColorPreview As DrawingArea
        Private pPreviewEditor As CustomDrawingEditor
        Private pThemeNameLabel As Label
        Private pOpenFolderButton As Button
        Private pImportButton As Button
        Private pSaveButton As Button
        Private pApplyButton As Button
        
        ' Color state
        Private pCurrentColor As Gdk.RGBA
        Private pColorAreas As New List(Of DrawingArea)
        
        ' Events
        Public Event ThemeChanged(vTheme As EditorTheme)
        Public Event Modified(vModified As Boolean)
        
        ' Constructor
        Public Sub New(vThemeManager As ThemeManager, vSettingsManager As SettingsManager)
            MyBase.New(Orientation.Vertical, 5)
            
            pThemeManager = vThemeManager
            pSettingsManager = vSettingsManager
            pCurrentColor = New Gdk.RGBA() With {.Red = 0.5, .Green = 0.5, .Blue = 0.5, .Alpha = 1.0}
            
            BuildUI()
            LoadThemes()
        End Sub
        
        ' Build the UI
        Private Sub BuildUI()
            Try
                BorderWidth = 10
                
                ' Main horizontal paned
                Dim lHPaned As New Paned(Orientation.Horizontal)
                
                ' Left side - Theme list and controls
                Dim lLeftBox As New Box(Orientation.Vertical, 5)
                
                ' Theme list
                Dim lThemeFrame As New Frame("Themes")
                Dim lThemeScrolled As New ScrolledWindow()
                lThemeScrolled.SetSizeRequest(200, 300)
                lThemeScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                pThemeListBox = New ListBox()
                AddHandler pThemeListBox.RowSelected, AddressOf OnThemeSelected
                
                lThemeScrolled.Add(pThemeListBox)
                lThemeFrame.Add(lThemeScrolled)
                lLeftBox.PackStart(lThemeFrame, True, True, 0)
                
                ' Theme control buttons
                Dim lThemeButtonBox As New Box(Orientation.Horizontal, 5)
                
                Dim lNewButton As New Button("New Theme")
                AddHandler lNewButton.Clicked, AddressOf OnNewTheme
                lThemeButtonBox.PackStart(lNewButton, True, True, 0)
                
                pOpenFolderButton = New Button("Open Folder")
                AddHandler pOpenFolderButton.Clicked, AddressOf OnOpenFolder
                lThemeButtonBox.PackStart(pOpenFolderButton, True, True, 0)
                
                lLeftBox.PackStart(lThemeButtonBox, False, False, 0)
                
                ' Import button
                pImportButton = New Button("Import Theme...")
                AddHandler pImportButton.Clicked, AddressOf OnImportTheme
                lLeftBox.PackStart(pImportButton, False, False, 0)
                
                lHPaned.Pack1(lLeftBox, False, False)
                
                ' Right side - Property editor and preview
                Dim lRightBox As New Box(Orientation.Vertical, 5)
                
                ' Current theme name
                pThemeNameLabel = New Label("")
                pThemeNameLabel.Markup = "<b>No Theme Selected</b>"
                pThemeNameLabel.Xalign = 0
                lRightBox.PackStart(pThemeNameLabel, False, False, 0)
                
                ' Property editor and preview in horizontal paned
                Dim lEditorPaned As New Paned(Orientation.Horizontal)
                
                ' Property list
                Dim lPropertyFrame As New Frame("Theme Properties")
                Dim lPropertyScrolled As New ScrolledWindow()
                lPropertyScrolled.SetSizeRequest(250, -1)
                lPropertyScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                pPropertyListBox = New ListBox()
                AddHandler pPropertyListBox.RowSelected, AddressOf OnPropertySelected
                
                lPropertyScrolled.Add(pPropertyListBox)
                lPropertyFrame.Add(lPropertyScrolled)
                lEditorPaned.Pack1(lPropertyFrame, False, False)
                
                ' Color editor and preview
                Dim lPreviewBox As New Box(Orientation.Vertical, 5)
                
                ' Color picker section
                Dim lColorFrame As New Frame("Color Selection")
                Dim lColorBox As New Box(Orientation.Horizontal, 5)  ' Changed to Horizontal
                lColorBox.BorderWidth = 10
                
                ' Left side - Color grid
                Dim lGridBox As New Box(Orientation.Vertical, 5)
                
                ' Quick colors label
                Dim lQuickColorsLabel As New Label("Quick Colors:")
                lQuickColorsLabel.Xalign = 0
                lGridBox.PackStart(lQuickColorsLabel, False, False, 0)
                
                ' Create custom color grid
                CreateColorGrid(lGridBox)
                
                lColorBox.PackStart(lGridBox, True, True, 0)
                
                ' Right side - Controls
                Dim lControlsBox As New Box(Orientation.Vertical, 5)
                lControlsBox.BorderWidth = 10
                
                ' Current color preview
                Dim lCurrentColorBox As New Box(Orientation.Vertical, 5)
                lCurrentColorBox.PackStart(New Label("current Color:") With {.Xalign = 0}, False, False, 0)
                
                pColorPreview = New DrawingArea()
                pColorPreview.SetSizeRequest(120, 40)
                AddHandler pColorPreview.Drawn, AddressOf OnColorPreviewDrawn
                lCurrentColorBox.PackStart(pColorPreview, False, False, 0)
                
                lControlsBox.PackStart(lCurrentColorBox, False, False, 0)
                
                ' Separator
                lControlsBox.PackStart(New Separator(Orientation.Horizontal), False, False, 5)
                
                ' Custom color button
                Dim lCustomButton As New Button("Pick Color...")
                AddHandler lCustomButton.Clicked, AddressOf OnCustomColorClicked
                lControlsBox.PackStart(lCustomButton, False, False, 0)
                
                ' Separator
                lControlsBox.PackStart(New Separator(Orientation.Horizontal), False, False, 5)
                
                ' RGB/Hex entries
                Dim lEntryGrid As New Grid()
                lEntryGrid.RowSpacing = 5
                lEntryGrid.ColumnSpacing = 5
                
                ' RGB entry
                lEntryGrid.Attach(New Label("RGB:") With {.Xalign = 0}, 0, 0, 1, 1)
                pRgbEntry = New Entry()
                pRgbEntry.PlaceholderText = "255, 128, 64"
                pRgbEntry.WidthChars = 12
                AddHandler pRgbEntry.Changed, AddressOf OnRgbChanged
                lEntryGrid.Attach(pRgbEntry, 1, 0, 1, 1)
                
                ' Hex entry
                lEntryGrid.Attach(New Label("Hex:") With {.Xalign = 0}, 0, 1, 1, 1)
                pHexEntry = New Entry()
                pHexEntry.PlaceholderText = "#RRGGBB"
                pHexEntry.WidthChars = 12
                AddHandler pHexEntry.Changed, AddressOf OnHexChanged
                lEntryGrid.Attach(pHexEntry, 1, 1, 1, 1)
                
                lControlsBox.PackStart(lEntryGrid, False, False, 0)
                
                lColorBox.PackStart(lControlsBox, False, False, 0)
                
                lColorFrame.Add(lColorBox)
                lPreviewBox.PackStart(lColorFrame, False, False, 0)
                
                ' Preview editor
                Dim lPreviewFrame As New Frame("Preview")
                Dim lPreviewScrolled As New ScrolledWindow()
                lPreviewScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                

                Dim lDemoSourceFileInfo As New SourceFileInfo("Demo.vb", "")
                lDemoSourceFileInfo.Content = GetPreviewCode() 
                lDemoSourceFileInfo.TextLines = New List(Of String)(lDemoSourceFileInfo.Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                If lDemoSourceFileInfo.TextLines.Count = 0 Then
                    lDemoSourceFileInfo.TextLines.Add("")
                End If
                lDemoSourceFileInfo.IsLoaded = True
                
                pPreviewEditor = New CustomDrawingEditor(lDemoSourceFileInfo)

                pPreviewEditor.SetDependencies(New SyntaxColorSet(), pSettingsManager)
                pPreviewEditor.SetThemeManager(pThemeManager)
                pPreviewEditor.SetSizeRequest(500, 400)  
                pPreviewEditor.IsReadOnly = True

                
                lPreviewScrolled.Add(pPreviewEditor)
                lPreviewFrame.Add(lPreviewScrolled)
                lPreviewBox.PackStart(lPreviewFrame, True, True, 0)
                
                lEditorPaned.Pack2(lPreviewBox, True, False)
                lRightBox.PackStart(lEditorPaned, True, True, 0)
                
                ' Bottom buttons
                Dim lButtonBox As New Box(Orientation.Horizontal, 5)
                lButtonBox.Halign = Align.End
                
                pSaveButton = New Button("Save Theme")
                pSaveButton.Sensitive = False
                AddHandler pSaveButton.Clicked, AddressOf OnSaveTheme
                lButtonBox.PackStart(pSaveButton, False, False, 0)
                
                pApplyButton = New Button("Apply Theme")
                pApplyButton.Sensitive = False
                AddHandler pApplyButton.Clicked, AddressOf OnApplyTheme
                lButtonBox.PackStart(pApplyButton, False, False, 0)
                
                lRightBox.PackStart(lButtonBox, False, False, 0)
                
                lHPaned.Pack2(lRightBox, True, False)
                
                PackStart(lHPaned, True, True, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ' Create custom color grid
        Private Sub CreateColorGrid(vContainer As Box)
            Try
                ' Create a scrolled window for the color grid
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetSizeRequest(280, 180)  ' Reduced Height for better layout
                lScrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic)
                
                ' Create color grid
                pColorGrid = New Grid()
                pColorGrid.ColumnSpacing = 5
                pColorGrid.RowSpacing = 5
                pColorGrid.BorderWidth = 5
                
                ' Define color palette
                Dim lColors As (String, String)() = {
                    ("#000000", "Black"),
                    ("#FFFFFF", "White"),
                    ("#808080", "Gray"),
                    ("#C0C0C0", "Silver"),
                    ("#FF0000", "Red"),
                    ("#800000", "Maroon"),
                    ("#FFFF00", "Yellow"),
                    ("#808000", "Olive"),
                    ("#00FF00", "Lime"),
                    ("#008000", "Green"),
                    ("#00FFFF", "Aqua"),
                    ("#008080", "Teal"),
                    ("#0000FF", "Blue"),
                    ("#000080", "Navy"),
                    ("#FF00FF", "Fuchsia"),
                    ("#800080", "Purple"),
                    ("#1E1E1E", "VS Dark Background"),
                    ("#252526", "Editor Background"),
                    ("#CCCCCC", "Light Text"),
                    ("#569CD6", "Blue (Keywords)"),
                    ("#CE9178", "Orange (Strings)"),
                    ("#6A9955", "Green (Comments)"),
                    ("#4EC9B0", "Cyan (Types)"),
                    ("#C586C0", "Purple (Keywords Alt)"),
                    ("#D4D4D4", "Default Text"),
                    ("#DCDCAA", "Yellow (Functions)"),
                    ("#B5CEA8", "Light Green (Numbers)"),
                    ("#D7BA7D", "Brown (Escape)"),
                    ("#9CDCFE", "Light Blue (Variables)"),
                    ("#F44747", "error Red"),
                    ("#608B4E", "Dark Green"),
                    ("#646695", "Dark Blue")
                }
                
                ' Clear previous color areas
                pColorAreas.Clear()
                
                ' Add colors to grid
                Dim lRow As Integer = 0
                Dim lCol As Integer = 0
                Const COLS_PER_ROW As Integer = 8
                
                For Each lColorInfo In lColors
                    Dim lHexColor As String = lColorInfo.Item1
                    Dim lColorName As String = lColorInfo.Item2
                    
                    ' Create event box for click handling
                    Dim lEventBox As New EventBox()
                    lEventBox.TooltipText = $"{lColorName} ({lHexColor})"
                    
                    ' Create drawing area for color
                    Dim lColorArea As New DrawingArea()
                    lColorArea.SetSizeRequest(30, 30)
                    
                    ' Store the color in the drawing area's data
                    lColorArea.Data("Color") = lHexColor
                    
                    ' Add border styling
                    lColorArea.StyleContext.AddClass("Color-swatch")
                    
                    ' Draw the color
                    AddHandler lColorArea.Drawn, AddressOf OnColorSwatchDrawn
                    
                    ' Handle click
                    AddHandler lEventBox.ButtonPressEvent, Sub(sender, args)
                        SelectColor(lHexColor)
                    End Sub
                    
                    lEventBox.Add(lColorArea)
                    pColorGrid.Attach(lEventBox, lCol, lRow, 1, 1)
                    pColorAreas.Add(lColorArea)
                    
                    ' Move to next position
                    lCol += 1
                    If lCol >= COLS_PER_ROW Then
                        lCol = 0
                        lRow += 1
                    End If
                Next
                
                lScrolled.Add(pColorGrid)
                vContainer.PackStart(lScrolled, True, True, 0)
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.CreateColorGrid error: {ex.Message}")
            End Try
        End Sub
        
        ' Draw color swatch
        Private Sub OnColorSwatchDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lArea As DrawingArea = DirectCast(vSender, DrawingArea)
                Dim lHexColor As String = TryCast(lArea.Data("Color"), String)
                If String.IsNullOrEmpty(lHexColor) Then Return
                
                Dim lCtx As Cairo.Context = vArgs.Cr
                Dim lWidth As Integer = lArea.AllocatedWidth
                Dim lHeight As Integer = lArea.AllocatedHeight
                
                ' Parse color
                Dim lRgba As New Gdk.RGBA()
                If lRgba.Parse(lHexColor) Then
                    ' Draw filled rectangle
                    lCtx.SetSourceRGBA(lRgba.Red, lRgba.Green, lRgba.Blue, lRgba.Alpha)
                    lCtx.Rectangle(1, 1, lWidth - 2, lHeight - 2)
                    lCtx.Fill()
                    
                    ' Draw border
                    lCtx.SetSourceRGBA(0.3, 0.3, 0.3, 1.0)
                    lCtx.LineWidth = 1
                    lCtx.Rectangle(0.5, 0.5, lWidth - 1, lHeight - 1)
                    lCtx.Stroke()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnColorSwatchDrawn error: {ex.Message}")
            End Try
        End Sub
        
        ' Draw color preview
        Private Sub OnColorPreviewDrawn(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lCtx As Cairo.Context = vArgs.Cr
                Dim lWidth As Integer = pColorPreview.AllocatedWidth
                Dim lHeight As Integer = pColorPreview.AllocatedHeight
                
                ' Draw filled rectangle with current color
                lCtx.SetSourceRGBA(pCurrentColor.Red, pCurrentColor.Green, pCurrentColor.Blue, pCurrentColor.Alpha)
                lCtx.Rectangle(1, 1, lWidth - 2, lHeight - 2)
                lCtx.Fill()
                
                ' Draw border
                lCtx.SetSourceRGBA(0.3, 0.3, 0.3, 1.0)
                lCtx.LineWidth = 1
                lCtx.Rectangle(0.5, 0.5, lWidth - 1, lHeight - 1)
                lCtx.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnColorPreviewDrawn error: {ex.Message}")
            End Try
        End Sub
        
        ' Select a color
        Private Sub SelectColor(vHexColor As String)
            Try
                Dim lRgba As New Gdk.RGBA()
                If lRgba.Parse(vHexColor) Then
                    pCurrentColor = lRgba
                    UpdateColorControls()
                    UpdateThemeColorFromSelection()
                End If
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.SelectColor error: {ex.Message}")
            End Try
        End Sub
        
        ' Update color controls
        Private Sub UpdateColorControls()
            Try
                pIsUpdatingUI = True
                
                ' Update RGB entry
                pRgbEntry.Text = $"{CInt(pCurrentColor.Red * 255)}, {CInt(pCurrentColor.Green * 255)}, {CInt(pCurrentColor.Blue * 255)}"
                
                ' Update Hex entry
                pHexEntry.Text = $"#{CInt(pCurrentColor.Red * 255):X2}{CInt(pCurrentColor.Green * 255):X2}{CInt(pCurrentColor.Blue * 255):X2}"
                
                ' Update preview
                pColorPreview.QueueDraw()
                
                pIsUpdatingUI = False
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.UpdateColorControls error: {ex.Message}")
            End Try
        End Sub
        
        ' Update theme color from selection
        Private Sub UpdateThemeColorFromSelection()
            Try
                If pCurrentTheme Is Nothing Then Return
                
                Dim lSelectedRow As ListBoxRow = pPropertyListBox.SelectedRow()
                If lSelectedRow Is Nothing Then Return
                
                Dim lIndex As Integer = lSelectedRow.Index()
                If lIndex < 0 Then Return
                
                Dim lTag As EditorTheme.Tags = CType(lIndex, EditorTheme.Tags)
                Dim lHexColor As String = $"#{CInt(pCurrentColor.Red * 255):X2}{CInt(pCurrentColor.Green * 255):X2}{CInt(pCurrentColor.Blue * 255):X2}"
                
                UpdateThemeColor(lTag, lHexColor)
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.UpdateThemeColorFromSelection error: {ex.Message}")
            End Try
        End Sub
        
        ' Custom color picker
        Private Sub OnCustomColorClicked(vSender As Object, vArgs As EventArgs)
            Try
                Dim lDialog As New ColorChooserDialog("Pick a Color", Me.Toplevel)
                lDialog.UseAlpha = False
                lDialog.Rgba = pCurrentColor
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    pCurrentColor = lDialog.Rgba
                    UpdateColorControls()
                    UpdateThemeColorFromSelection()
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnCustomColorClicked error: {ex.Message}")
            End Try
        End Sub
        
        ' Load available themes
        Private Sub LoadThemes()
            Try
                ' Clear existing
                For Each lChild In pThemeListBox.Children
                    pThemeListBox.Remove(lChild)
                Next
                
                ' Add themes
                For Each lThemeName In pThemeManager.GetAvailableThemes()
                    Dim lRow As New ListBoxRow()
                    Dim lLabel As New Label(lThemeName)
                    lLabel.Xalign = 0
                    lRow.Add(lLabel)
                    lRow.ShowAll()
                    pThemeListBox.Add(lRow)
                Next
                
                pThemeListBox.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.LoadThemes error: {ex.Message}")
            End Try
        End Sub
        
        ' Load properties for selected theme
        Private Sub LoadProperties()
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Clear existing
                For Each lChild In pPropertyListBox.Children
                    pPropertyListBox.Remove(lChild)
                Next
                
                ' Add properties
                For Each lTag In [Enum].GetValues(GetType(EditorTheme.Tags))
                    Dim lRow As New ListBoxRow()
                    Dim lLabel As New Label(GetPropertyDisplayName(lTag))
                    lLabel.Xalign = 0
                    lRow.Add(lLabel)
                    lRow.ShowAll()
                    pPropertyListBox.Add(lRow)
                Next
                
                pPropertyListBox.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.LoadProperties error: {ex.Message}")
            End Try
        End Sub
        
        ' Get display name for property
        Private Function GetPropertyDisplayName(vTag As EditorTheme.Tags) As String
            Select Case vTag
                Case EditorTheme.Tags.eBackgroundColor : Return "Background Color"
                Case EditorTheme.Tags.eForegroundColor : Return "Foreground Color"
                Case EditorTheme.Tags.eSelectionColor : Return "Selection Color"
                Case EditorTheme.Tags.eCurrentLineColor : Return "current Line Color"
                Case EditorTheme.Tags.eLineNumberColor : Return "Line Number Color"
                Case EditorTheme.Tags.eLineNumberBackgroundColor : Return "Line Number Background"
                Case EditorTheme.Tags.eCurrentLineNumberColor : Return "current Line Number Color"
                Case EditorTheme.Tags.eCursorColor : Return "Cursor Color"
                Case EditorTheme.Tags.eKeywordText : Return "Keyword Color"
                Case EditorTheme.Tags.eTypeText : Return "Type Color"
                Case EditorTheme.Tags.eStringText : Return "String Color"
                Case EditorTheme.Tags.eCommentText : Return "Comment Color"
                Case EditorTheme.Tags.eNumberText : Return "Number Color"
                Case EditorTheme.Tags.eOperatorText : Return "Operator Color"
                Case EditorTheme.Tags.ePreprocessorText : Return "Preprocessor Color"
                Case EditorTheme.Tags.eIdentifierText : Return "Identifier Color"
                Case EditorTheme.Tags.eSelectionText : Return "Selection Text Color"
                Case Else : Return vTag.ToString()
            End Select
        End Function
        
        ' Get preview code
        Private Function GetPreviewCode() As String
            Return "' VB.NET Theme Preview
Imports System
Imports System.Collections.Generic

Namespace Preview
    ''' <summary>
    ''' Sample class for theme preview
    ''' </summary>
    Public Class ThemePreview
        Private pCount As Integer = 42
        Private pName As String = ""Hello World""
        
        Public Sub New()
            ' Constructor comment
            Initialize()
        End Sub
        
        Public Function Calculate(vValue As Double) As Double
            Dim lResult As Double = vValue * 3.14159
            Return lResult
        End Function
        
        #Region ""Properties""
        Public Property Count As Integer
            Get
                Return pCount
            End Get
            Set(Value As Integer)
                pCount = Value
            End Set
        End Property
        #End Region
    End Class
End Namespace"
        End Function
        
        ' === Event Handlers ===
        
        Private Sub OnThemeSelected(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelectedRow As ListBoxRow = pThemeListBox.SelectedRow
                If lSelectedRow Is Nothing Then Return
                
                Dim lIndex As Integer = lSelectedRow.Index()
                If lIndex < 0 Then Return
                
                Dim lThemes As List(Of String) = pThemeManager.GetAvailableThemes()
                If lIndex >= lThemes.Count Then Return
                
                Dim lThemeName As String = lThemes(lIndex)
                pCurrentTheme = pThemeManager.GetTheme(lThemeName)
                
                If pCurrentTheme IsNot Nothing Then
                    pThemeNameLabel.Markup = $"<b>{lThemeName}</b>"
                    LoadProperties()
                    UpdatePreview()
                    pApplyButton.Sensitive = True
                    pSaveButton.Sensitive = IsCustomTheme(lThemeName)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnThemeSelected error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnPropertySelected(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelectedRow As ListBoxRow = pPropertyListBox.SelectedRow
                If lSelectedRow Is Nothing OrElse pCurrentTheme Is Nothing Then Return
                
                Dim lIndex As Integer = lSelectedRow.Index()
                If lIndex < 0 Then Return
                
                Dim lTag As EditorTheme.Tags = CType(lIndex, EditorTheme.Tags)
                Dim lColor As String = pCurrentTheme.StringColor(lTag)
                
                ' Update current color
                Dim lRgba As New Gdk.RGBA()
                If lRgba.Parse(lColor) Then
                    pCurrentColor = lRgba
                    UpdateColorControls()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnPropertySelected error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnRgbChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pIsUpdatingUI Then Return
                If String.IsNullOrWhiteSpace(pRgbEntry.Text) Then Return
                
                ' Parse RGB values
                Dim lParts() As String = pRgbEntry.Text.Split(","c)
                If lParts.Length <> 3 Then Return
                
                Dim lR, lG, lB As Integer
                If Integer.TryParse(lParts(0).Trim(), lR) AndAlso
                   Integer.TryParse(lParts(1).Trim(), lG) AndAlso
                   Integer.TryParse(lParts(2).Trim(), lB) Then
                    
                    ' Validate range
                    lR = Math.Max(0, Math.Min(255, lR))
                    lG = Math.Max(0, Math.Min(255, lG))
                    lB = Math.Max(0, Math.Min(255, lB))
                    
                    ' Update color
                    pCurrentColor = New Gdk.RGBA() With {
                        .Red = lR / 255.0,
                        .Green = lG / 255.0,
                        .Blue = lB / 255.0,
                        .Alpha = 1.0
                    }
                    
                    UpdateColorControls()
                    UpdateThemeColorFromSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnRgbChanged error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnHexChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pIsUpdatingUI Then Return
                If String.IsNullOrWhiteSpace(pHexEntry.Text) Then Return
                
                Dim lHexColor As String = pHexEntry.Text.Trim()
                
                ' Validate hex color
                If Not lHexColor.StartsWith("#") Then
                    lHexColor = "#" & lHexColor
                End If
                
                ' Parse color
                Dim lRgba As New Gdk.RGBA()
                If lRgba.Parse(lHexColor) Then
                    pCurrentColor = lRgba
                    UpdateColorControls()
                    UpdateThemeColorFromSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnHexChanged error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateThemeColor(vTag As EditorTheme.Tags, vColor As String)
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Update the theme color based on tag
                Select Case vTag
                    Case EditorTheme.Tags.eBackgroundColor
                        pCurrentTheme.BackgroundColor = vColor
                    Case EditorTheme.Tags.eForegroundColor
                        pCurrentTheme.ForegroundColor = vColor
                    Case EditorTheme.Tags.eSelectionColor
                        pCurrentTheme.SelectionColor = vColor
                    Case EditorTheme.Tags.eCurrentLineColor
                        pCurrentTheme.CurrentLineColor = vColor
                    Case EditorTheme.Tags.eLineNumberBackgroundColor
                        pCurrentTheme.LineNumberBackgroundColor = vColor
                    Case EditorTheme.Tags.eCurrentLineNumberColor
                        pCurrentTheme.CurrentLineNumberColor = vColor
                    Case EditorTheme.Tags.eCursorColor
                        pCurrentTheme.CursorColor = vColor
                    Case EditorTheme.Tags.eKeywordText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = vColor
                    Case EditorTheme.Tags.eTypeText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eType) = vColor
                    Case EditorTheme.Tags.eStringText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eString) = vColor
                    Case EditorTheme.Tags.eCommentText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eComment) = vColor
                    Case EditorTheme.Tags.eNumberText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber) = vColor
                    Case EditorTheme.Tags.eOperatorText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eOperator) = vColor
                    Case EditorTheme.Tags.ePreprocessorText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.ePreprocessor) = vColor
                    Case EditorTheme.Tags.eIdentifierText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = vColor
                    Case EditorTheme.Tags.eSelectionText
                        pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection) = vColor
                End Select
                
                ' Update preview
                UpdatePreview()
                SetModified(True)
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.UpdateThemeColor error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdatePreview()
            Try
                If pCurrentTheme Is Nothing OrElse pPreviewEditor Is Nothing Then Return
                
                ' Apply theme to preview editor
                pPreviewEditor.ApplyTheme()
                pPreviewEditor.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.UpdatePreview error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnNewTheme(vSender As Object, vArgs As EventArgs)
            Try
                ' Create dialog for theme name
                Dim lDialog As New Dialog("New Theme", Me.Toplevel, 
                    DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                    "Cancel", ResponseType.Cancel,
                    "Create", ResponseType.Accept)
                
                lDialog.SetDefaultSize(300, 150)
                
                Dim lContent As Box = CType(lDialog.ContentArea, Box)
                lContent.BorderWidth = 10
                
                Dim lBox As New Box(Orientation.Vertical, 5)
                lBox.PackStart(New Label("Theme Name:"), False, False, 0)
                
                Dim lEntry As New Entry()
                lEntry.PlaceholderText = "My Custom Theme"
                lBox.PackStart(lEntry, False, False, 0)
                
                lBox.PackStart(New Label("Base Theme:"), False, False, 0)
                
                Dim lCombo As New ComboBoxText()
                For Each lThemeName In pThemeManager.GetAvailableThemes()
                    lCombo.AppendText(lThemeName)
                Next
                lCombo.Active = 0
                lBox.PackStart(lCombo, False, False, 0)
                
                lContent.PackStart(lBox, True, True, 0)
                lContent.ShowAll()
                
                If lDialog.Run() = CInt(ResponseType.Accept) AndAlso Not String.IsNullOrWhiteSpace(lEntry.Text) Then
                    Dim lNewThemeName As String = lEntry.Text.Trim()
                    Dim lBaseThemeName As String = lCombo.ActiveText
                    
                    ' Create new theme based on selected base
                    Dim lNewTheme As EditorTheme = pThemeManager.CreateCustomTheme(lBaseThemeName, lNewThemeName)
                    If lNewTheme IsNot Nothing Then
                        LoadThemes()
                        SelectTheme(lNewThemeName)
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnNewTheme error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSaveTheme(vSender As Object, vArgs As EventArgs)
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Get themes directory
                Dim lThemesDir As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE", "Themes")
                
                ' Ensure directory exists
                If Not Directory.Exists(lThemesDir) Then
                    Directory.CreateDirectory(lThemesDir)
                End If
                
                ' Save theme file
                Dim lFilePath As String = System.IO.Path.Combine(lThemesDir, $"{pCurrentTheme.Name}.json")
                If pThemeManager.SaveTheme(pCurrentTheme, lFilePath) Then
                    SetModified(False)
                    
                    ' Show confirmation
                    Dim lDialog As New MessageDialog(
                        Me.Toplevel,
                        DialogFlags.Modal,
                        MessageType.Info,
                        ButtonsType.Ok,
                        $"Theme '{pCurrentTheme.Name}' saved successfully."
                    )
                    lDialog.Run()
                    lDialog.Destroy()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnSaveTheme error: {ex.Message}")
                ShowError("Save error", ex.Message)
            End Try
        End Sub
        
        Private Sub OnApplyTheme(vSender As Object, vArgs As EventArgs)
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Apply theme
                pThemeManager.SetTheme(pCurrentTheme.Name)
                
                ' Raise event
                RaiseEvent ThemeChanged(pCurrentTheme)
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnApplyTheme error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnImportTheme(vSender As Object, vArgs As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Import Theme",
                    Me.Toplevel,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Import", ResponseType.Accept
                )
                
                ' Add filters
                Dim lJsonFilter As New FileFilter()
                lJsonFilter.Name = "Theme Files (*.json)"
                lJsonFilter.AddPattern("*.json")
                lDialog.AddFilter(lJsonFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lTheme As EditorTheme = pThemeManager.ImportTheme(lDialog.FileName)
                    If lTheme IsNot Nothing Then
                        LoadThemes()
                        SelectTheme(lTheme.Name)
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnImportTheme error: {ex.Message}")
                ShowError("Import error", ex.Message)
            End Try
        End Sub
        
        Private Sub OnOpenFolder(vSender As Object, vArgs As EventArgs)
            Try
                ' Get themes directory
                Dim lThemesDir As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE", "Themes")
                    
                ' Ensure directory exists
                If Not Directory.Exists(lThemesDir) Then
                    Directory.CreateDirectory(lThemesDir)
                End If
                
                ' Open in file manager
                If Directory.Exists(lThemesDir) Then
                    Process.Start(New ProcessStartInfo() With {
                        .FileName = lThemesDir,
                        .UseShellExecute = True
                    })
                End If
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnOpenFolder error: {ex.Message}")
            End Try
        End Sub
        
        ' === Helper Methods ===
        
        Private Function IsCustomTheme(vThemeName As String) As Boolean
            ' Built-in themes cannot be saved
            Return Not {"Default Dark", "Default Light", "Visual Studio Dark", "Monokai", "VS code Dark+", "Light", "Solarized Dark"}.Contains(vThemeName)
        End Function
        
        Private Sub SelectTheme(vThemeName As String)
            Try
                Dim lIndex As Integer = 0
                For Each lRow As ListBoxRow In pThemeListBox.Children
                    Dim lLabel As Label = TryCast(lRow.Child, Label)
                    If lLabel IsNot Nothing AndAlso lLabel.Text = vThemeName Then
                        pThemeListBox.SelectRow(lRow)
                        Exit For
                    End If
                    lIndex += 1
                Next
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.SelectTheme error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SetModified(vModified As Boolean)
            pModified = vModified
            pSaveButton.Sensitive = vModified AndAlso pCurrentTheme IsNot Nothing AndAlso IsCustomTheme(pCurrentTheme.Name)
            RaiseEvent Modified(vModified)
        End Sub
        
        Private Sub ShowError(vTitle As String, vMessage As String)
            Dim lDialog As New MessageDialog(
                Me.Toplevel,
                DialogFlags.Modal,
                MessageType.Error,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Title = vTitle
            lDialog.Run()
            lDialog.Destroy()
        End Sub
        
        ' === Public Methods ===
        
        Public Sub RefreshThemes()
            LoadThemes()
        End Sub
        
    End Class
    
End Namespace
