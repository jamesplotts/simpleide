' Widgets/ThemeEditor.vb - Theme editor panel for creating and editing themes
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
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
        Private pProjectManager As ProjectManager
        Private pCurrentTheme As EditorTheme
        Private pModified As Boolean
        Private pIsUpdatingUI As Boolean = False
        
        ' UI Components
        Private pThemeListStore As ListStore
        Private pPreviewEditor As CustomDrawingEditor
        Private pPreviewTextBox As CustomDrawTextBox
        Private pPreviewButton As CustomDrawButton
        Private pColorSwatches As New Dictionary(Of EditorTheme.Tags, DrawingArea)
        Private pThemeNameLabel As Label
        Private pOpenFolderButton As CustomDrawButton
        Private pImportButton As CustomDrawButton
        Private pSaveButton As CustomDrawButton
        Private pApplyButton As CustomDrawButton
        Private pThemeContextMenu As Menu

        Private pThemeListBox As CustomDrawListBox
        Private pPropertyListBox As CustomDrawListBox
 
        ' Add the new ColorPicker field:
        Private pColorPicker As ColorPicker
        Private pCurrentPropertyTag As EditorTheme.Tags
        
        ' Color state
        Private pCurrentColor As Gdk.RGBA
        Private pColorAreas As New List(Of DrawingArea)

        ' Track the actual selected theme name
        Private pCurrentThemeName As String  
        
        ' Events
        Public Event ThemeChanged(vTheme As EditorTheme)
        Public Event Modified(vModified As Boolean)

        ' Constructor
        ''' <summary>
        ''' Constructor
        ''' </summary>
        Public Sub New(vThemeManager As ThemeManager, vSettingsManager As SettingsManager, Optional vProjectManager As ProjectManager = Nothing)
            MyBase.New(Orientation.Vertical, 5)

            pThemeManager = vThemeManager
            pSettingsManager = vSettingsManager
            pProjectManager = vProjectManager
            pCurrentColor = New Gdk.RGBA() with {.Red = 0.5, .Green = 0.5, .Blue = 0.5, .Alpha = 1.0}
            
            BuildUI()
            LoadThemes()
            
            ' CRITICAL FIX: Ensure selection styles are applied after themes are loaded
            ' This may need to be called again after LoadThemes to ensure the styles persist
            ApplyListBoxSelectionStyles()
        End Sub
        
        ''' <summary>
        ''' Build the complete Theme Editor UI
        ''' </summary>
        Private Sub BuildUI()
            Try
                BorderWidth = 6
                
                ' Main horizontal paned
                Dim lHPaned As New Paned(Orientation.Horizontal)
                
                ' Left side - Theme list and controls
                Dim lLeftBox As New Box(Orientation.Vertical, 5)
                
                ' Theme list
                Dim lThemeFrame As New Frame("Themes")
                Dim lThemeContainer As New Box(Orientation.Vertical, 0)
                
                pThemeListBox = New CustomDrawListBox()
                pThemeListBox.SetSizeRequest(200, 300)
                pThemeListBox.ThemeManager = pThemeManager
                AddHandler pThemeListBox.SelectionChanged, AddressOf OnThemeSelected
                AddHandler pThemeListBox.ContextMenuRequested, AddressOf OnThemeContextMenuRequested
                
                lThemeContainer.PackStart(pThemeListBox, True, True, 0)
                lThemeFrame.Add(lThemeContainer)
                lLeftBox.PackStart(lThemeFrame, True, True, 0)
                
                ' Theme control buttons
                Dim lThemeButtonBox As New Box(Orientation.Horizontal, 5)
                
                Dim lNewButton As New CustomDrawButton("New Theme")
                lNewButton.ThemeManager = pThemeManager
                AddHandler lNewButton.Clicked, AddressOf OnNewTheme
                lThemeButtonBox.PackStart(lNewButton, True, True, 0)

                pOpenFolderButton = New CustomDrawButton("Open Folder")
                pOpenFolderButton.ThemeManager = pThemeManager
                AddHandler pOpenFolderButton.Clicked, AddressOf OnOpenFolder
                lThemeButtonBox.PackStart(pOpenFolderButton, True, True, 0)

                lLeftBox.PackStart(lThemeButtonBox, False, False, 0)

                ' Import button
                pImportButton = New CustomDrawButton("Import Theme...")
                pImportButton.ThemeManager = pThemeManager
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
                Dim lPropertyContainer As New Box(Orientation.Vertical, 0)
                
                pPropertyListBox = New CustomDrawListBox()
                pPropertyListBox.SetSizeRequest(250, -1)
                pPropertyListBox.ThemeManager = pThemeManager
                AddHandler pPropertyListBox.SelectionChanged, AddressOf OnPropertySelected
                
                lPropertyContainer.PackStart(pPropertyListBox, True, True, 0)
                lPropertyFrame.Add(lPropertyContainer)
                lEditorPaned.Pack1(lPropertyFrame, False, False)
                
                ' Color editor and preview
                Dim lPreviewBox As New Box(Orientation.Vertical, 5)
                
                ' Color editing using ColorPicker widget
                Dim lColorFrame As New Frame("Color Editor")
                
                ' Create ColorPicker in horizontal mode for Theme Editor
                pColorPicker = New ColorPicker(ColorPicker.LayoutMode.eHorizontal)
                pColorPicker.BorderWidth = 5
                pColorPicker.ThemeManager = pThemeManager

                ' Subscribe to color change events
                AddHandler pColorPicker.ColorChanged, AddressOf OnColorPickerColorChanged
                AddHandler pColorPicker.ColorSelected, AddressOf OnColorPickerColorSelected
                
                lColorFrame.Add(pColorPicker)
                lPreviewBox.PackStart(lColorFrame, True, True, 0)

                ' Interface preview - a real CustomDrawTextBox and CustomDrawButton themed
                ' from pCurrentTheme directly (via ApplyExplicitTheme, bypassing
                ' ThemeManager), so bevel/text-box/button colors can be checked visually
                ' without leaving the Theme Editor or affecting the app's actual theme.
                ' Everything (controls + swatches) stays on a single horizontal row - the
                ' whole ThemeEditor is hosted inside a ScrolledWindow by MainWindow, and an
                ' earlier two-row version (swatches on their own row underneath) added just
                ' enough height to tip the content past the ScrolledWindow's viewport,
                ' turning on an unwanted scrollbar/border for the whole dialog
                Dim lInterfaceFrame As New Frame("Interface Preview")
                Dim lInterfaceBox As New Box(Orientation.Horizontal, 10)
                lInterfaceBox.BorderWidth = 8

                pPreviewTextBox = New CustomDrawTextBox()
                pPreviewTextBox.Text = "Sample text"
                pPreviewTextBox.InnerEntry.WidthChars = 20
                lInterfaceBox.PackStart(pPreviewTextBox, False, False, 0)

                pPreviewButton = New CustomDrawButton("Sample Button")
                lInterfaceBox.PackStart(pPreviewButton, False, False, 0)

                ' Small color swatches (name shown as a tooltip, not a packed label, to
                ' keep this whole row no taller than the textbox/button already are) for
                ' the remaining theme colors that have no other preview surface in this
                ' dialog (status colors used only by the build panel/error list, tab-strip
                ' colors, and the accent color, whose real in-editor uses - bracket-match
                ' flash, popup borders - don't trigger passively in a static read-only
                ' snippet)
                lInterfaceBox.PackStart(CreateColorSwatch("Error", EditorTheme.Tags.eErrorColor), False, False, 0)
                lInterfaceBox.PackStart(CreateColorSwatch("Warning", EditorTheme.Tags.eWarningColor), False, False, 0)
                lInterfaceBox.PackStart(CreateColorSwatch("Info", EditorTheme.Tags.eInfoColor), False, False, 0)
                lInterfaceBox.PackStart(CreateColorSwatch("Success", EditorTheme.Tags.eSuccessColor), False, False, 0)
                lInterfaceBox.PackStart(CreateColorSwatch("Editor Background", EditorTheme.Tags.eEditorBackgroundColor), False, False, 0)
                lInterfaceBox.PackStart(CreateColorSwatch("Inactive Tab", EditorTheme.Tags.eTabInactiveColor), False, False, 0)
                lInterfaceBox.PackStart(CreateColorSwatch("Tab Hover", EditorTheme.Tags.eTabHoverColor), False, False, 0)
                lInterfaceBox.PackStart(CreateColorSwatch("Accent", EditorTheme.Tags.eAccentColor), False, False, 0)

                lInterfaceFrame.Add(lInterfaceBox)
                lPreviewBox.PackStart(lInterfaceFrame, False, False, 0)

                ' Preview editor
                Dim lPreviewFrame As New Frame("Preview")
                Dim lPreviewScroll As New ScrolledWindow()
                ' Shrunk from 200px - this is itself a ScrolledWindow (PolicyType.Automatic
                ' below), so a smaller minimum only means fewer lines visible without
                ' scrolling within the preview, not any lost content - see the gradient-area
                ' shrink above for why keeping the overall ThemeEditor height down matters
                lPreviewScroll.SetSizeRequest(-1, 130)
                lPreviewScroll.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                ' Create SourceFileInfo using the content constructor which sets IsDemoMode
                Dim lPreviewSourceInfo As New SourceFileInfo("ThemePreview.vb", GetPreviewText())

                ' Without a real ProjectManager, this preview text never actually gets
                ' parsed/tokenized (RequestAsyncParse's own gate silently no-ops when its
                ' private pProjectManager field is Nothing, which it always was here), so
                ' theme syntax colors had nothing to apply to and the sample text rendered
                ' in a single flat foreground color regardless of what the theme specified.
                ' SourceFileInfo's private pProjectManager field can only be set by
                ' responding to its own ProjectManagerRequested event (its public
                ' "ProjectManager" field is a separate, unrelated field that doesn't feed
                ' the private one RequestAsyncParse actually checks)
                If pProjectManager IsNot Nothing Then
                    AddHandler lPreviewSourceInfo.ProjectManagerRequested,
                        Sub(vSender As Object, vArgs As SourceFileInfo.ProjectManagerRequestEventArgs)
                            vArgs.ProjectManager = pProjectManager
                        End Sub
                End If

                ' Create the editor with proper initialization
                pPreviewEditor = New CustomDrawingEditor(lPreviewSourceInfo, pThemeManager, pSettingsManager)
                pPreviewEditor.IsReadOnly = True

                ' Also give the editor itself the ProjectManager so it subscribes to parse-
                ' completed notifications and redraws once the preview text is tokenized
                If pProjectManager IsNot Nothing Then
                    pPreviewEditor.ProjectManager = pProjectManager
                    lPreviewSourceInfo.RequestAsyncParse()
                End If

                ' IMPORTANT: Set the ThemeManager before applying theme
                pPreviewEditor.SetThemeManager(pThemeManager)

                ' Apply current theme
                pPreviewEditor.ApplyTheme()

                ' Start with something selected so Selection Color/Selection Text Color
                ' are visible immediately without requiring the user to click or drag in
                ' the (read-only, but still selectable/navigable) preview - clicking,
                ' arrow-key navigation, and Shift+arrow selection all still work normally
                ' and will move/replace this initial selection
                pPreviewEditor.SetSelection(New EditorPosition(9, 16), New EditorPosition(9, 22))

                lPreviewScroll.Add(pPreviewEditor)
                lPreviewFrame.Add(lPreviewScroll)
                lPreviewBox.PackStart(lPreviewFrame, True, True, 0)
                
                lEditorPaned.Pack2(lPreviewBox, True, False)
                lRightBox.PackStart(lEditorPaned, True, True, 0)
                
                ' Action buttons
                Dim lButtonBox As New Box(Orientation.Horizontal, 5)
                
                pSaveButton = New CustomDrawButton("Save Theme")
                pSaveButton.ThemeManager = pThemeManager
                pSaveButton.Sensitive = False
                AddHandler pSaveButton.Clicked, AddressOf OnSaveTheme
                lButtonBox.PackStart(pSaveButton, False, False, 0)

                pApplyButton = New CustomDrawButton("Apply Theme")
                pApplyButton.ThemeManager = pThemeManager
                pApplyButton.Sensitive = False
                AddHandler pApplyButton.Clicked, AddressOf OnApplyTheme
                lButtonBox.PackStart(pApplyButton, False, False, 0)
                
                lRightBox.PackStart(lButtonBox, False, False, 0)
                
                lHPaned.Pack2(lRightBox, True, False)

                ' Subscribe to theme manager events
                AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeManagerThemeChanged
                AddHandler pThemeManager.ThemeApplied, AddressOf OnThemeManagerThemeApplied
                ' Note: pThemeListBox.SelectionChanged is already wired to OnThemeSelected above -
                ' this was a second, duplicate AddHandler that fired the same theme-load/preview
                ' logic twice per selection (see removed OnThemeListBoxSelectionChanged, which was
                ' just a pass-through wrapper calling OnThemeSelected again)

                ' CRITICAL: Add the main paned to this widget
                PackStart(lHPaned, True, True, 0)
                
                InitializeThemeContextMenu()
                InitializePropertyContextMenu()
                
                ' Apply selection styles
                ApplyListBoxSelectionStyles()
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        
        ' Update theme color from selection
        Private Sub UpdateThemeColorFromSelection()
            Try
                If pCurrentTheme Is Nothing Then Return
                
                Dim lIndex As Integer = pPropertyListBox.SelectedIndex()
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
                    UpdateThemeColorFromSelection()
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnCustomColorClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Load available themes
        ''' </summary>
        Private Sub LoadThemes()
            Try
                ' Clear existing
                pThemeListBox.Clear()
                
                ' Add themes
                for each lThemeName in pThemeManager.GetAvailableThemes()
                    #If DEBUG Then
                    Console.WriteLine($"Adding Theme " + lThemeName)
                    #End If
                    pThemeListBox.AddItem(lThemeName, pThemeManager.IsCustomTheme(lThemeName))
                Next
                
                ' Apply theme colors to the listbox
                ApplyThemeToListBoxes()
                
                ' Select current theme if set
                Dim lCurrentThemeName As String = pSettingsManager.GetString("CurrentTheme", "Default Dark")
                pThemeListBox.SelectByText(lCurrentThemeName)
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.LoadThemes error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Load properties for selected theme
        ''' </summary>
        Private Sub LoadProperties()
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Clear existing
                pPropertyListBox.Clear()
                
                ' Add properties
                for each lTag in [Enum].GetValues(GetType(EditorTheme.Tags))
                    pPropertyListBox.AddItem(GetPropertyDisplayName(lTag), lTag)
                Next
                
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
                Case EditorTheme.Tags.eErrorColor : Return "Error Color"
                Case EditorTheme.Tags.eWarningColor : Return "Warning Color"
                Case EditorTheme.Tags.eInfoColor : Return "Info Color"
                Case EditorTheme.Tags.eSuccessColor : Return "Success Color"
                Case EditorTheme.Tags.eEditorBackgroundColor : Return "Editor Background Color"
                Case EditorTheme.Tags.eTabInactiveColor : Return "Inactive Tab Color"
                Case EditorTheme.Tags.eTabHoverColor : Return "Tab Hover Color"
                Case EditorTheme.Tags.eAccentColor : Return "Accent Color"
                Case EditorTheme.Tags.eBevelLightColor : Return "Bevel Light Edge"
                Case EditorTheme.Tags.eBevelDarkColor : Return "Bevel Dark Edge"
                Case EditorTheme.Tags.eDisabledForegroundColor : Return "Disabled Control Text"
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
        Private Function GetPreviewText() As String
            Dim retval As String = ""
            retval += "' VB.NET Theme Preview" + Environment.NewLine
            retval += "Imports System" + Environment.NewLine
            retval += "Imports System.Collections.Generic" + Environment.NewLine
            retval += "" + Environment.NewLine
            retval += "Namespace Preview" + Environment.NewLine
            retval += "    ''' <summary>" + Environment.NewLine
            retval += "    ''' Sample class for theme preview" + Environment.NewLine
            retval += "    ''' </summary>" + Environment.NewLine
            retval += "    Public Class ThemePreview" + Environment.NewLine
            retval += "        Private pCount As Integer = 42" + Environment.NewLine
            retval += "        Private pName As String = ""Hello World""" + Environment.NewLine
            retval += "        " + Environment.NewLine
            retval += "        Public Sub New()" + Environment.NewLine
            retval += "            ' Constructor comment" + Environment.NewLine
            retval += "            Initialize()" + Environment.NewLine
            retval += "        End Sub" + Environment.NewLine
            retval += "        " + Environment.NewLine
            retval += "        Public Function Calculate(vValue As Double) As Double" + Environment.NewLine
            retval += "            Dim lResult As Double = vValue * 3.14159" + Environment.NewLine
            retval += "            Return lResult" + Environment.NewLine
            retval += "        End Function" + Environment.NewLine
            retval += "        " + Environment.NewLine
            retval += "        #Region ""Properties""" + Environment.NewLine
            retval += "        Public Property Count As Integer" + Environment.NewLine
            retval += "            Get" + Environment.NewLine
            retval += "                Return pCount" + Environment.NewLine
            retval += "            End Get" + Environment.NewLine
            retval += "            Set(Value As Integer)" + Environment.NewLine
            retval += "                pCount = Value" + Environment.NewLine
            retval += "            End Set" + Environment.NewLine
            retval += "        End Property" + Environment.NewLine
            retval += "        #End Region" + Environment.NewLine
            retval += "            " + Environment.NewLine
            retval += "    End Class" + Environment.NewLine
            retval += "End Namespace"
            Return retval
        End Function
        
        ' === Event Handlers ===
        


        
        Private Sub OnRgbChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pIsUpdatingUI Then Return
                    
                    UpdateThemeColorFromSelection()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnRgbChanged error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Updates a theme color property
        ''' </summary>
        Private Sub UpdateThemeColor(vTag As EditorTheme.Tags, vColorHex As String)
            Try
                If pCurrentTheme Is Nothing Then Return
                
                Select Case vTag
                    Case EditorTheme.Tags.eBackgroundColor
                        pCurrentTheme.BackgroundColor = vColorHex
                    Case EditorTheme.Tags.eForegroundColor
                        pCurrentTheme.ForegroundColor = vColorHex
                    Case EditorTheme.Tags.eSelectionColor
                        pCurrentTheme.SelectionColor = vColorHex
                    Case EditorTheme.Tags.eCurrentLineColor
                        pCurrentTheme.CurrentLineColor = vColorHex
                    Case EditorTheme.Tags.eLineNumberColor
                        pCurrentTheme.LineNumberColor = vColorHex
                    Case EditorTheme.Tags.eLineNumberBackgroundColor
                        pCurrentTheme.LineNumberBackgroundColor = vColorHex
                    Case EditorTheme.Tags.eCurrentLineNumberColor
                        pCurrentTheme.CurrentLineNumberColor = vColorHex
                    Case EditorTheme.Tags.eCursorColor
                        pCurrentTheme.CursorColor = vColorHex
                    Case EditorTheme.Tags.eEditorBackgroundColor
                        pCurrentTheme.EditorBackgroundColor = vColorHex
                    Case EditorTheme.Tags.eTabInactiveColor
                        pCurrentTheme.TabInactiveColor = vColorHex
                    Case EditorTheme.Tags.eTabHoverColor
                        pCurrentTheme.TabHoverColor = vColorHex
                    Case EditorTheme.Tags.eAccentColor
                        pCurrentTheme.AccentColor = vColorHex
                    Case EditorTheme.Tags.eBevelLightColor
                        pCurrentTheme.BevelLightColor = vColorHex
                    Case EditorTheme.Tags.eBevelDarkColor
                        pCurrentTheme.BevelDarkColor = vColorHex
                    Case EditorTheme.Tags.eDisabledForegroundColor
                        pCurrentTheme.DisabledForegroundColor = vColorHex
                    Case EditorTheme.Tags.eErrorColor
                        pCurrentTheme.ErrorColor = vColorHex
                    Case EditorTheme.Tags.eWarningColor
                        pCurrentTheme.WarningColor = vColorHex
                    Case EditorTheme.Tags.eInfoColor
                        pCurrentTheme.InfoColor = vColorHex
                    Case EditorTheme.Tags.eSuccessColor
                        pCurrentTheme.SuccessColor = vColorHex
                    Case EditorTheme.Tags.eKeywordText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eKeyword) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = vColorHex
                        End If
                    Case EditorTheme.Tags.eTypeText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eType) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eType) = vColorHex
                        End If
                    Case EditorTheme.Tags.eStringText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eString) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eString) = vColorHex
                        End If
                    Case EditorTheme.Tags.eCommentText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eComment) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eComment) = vColorHex
                        End If
                    Case EditorTheme.Tags.eNumberText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eNumber) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber) = vColorHex
                        End If
                    Case EditorTheme.Tags.eOperatorText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eOperator) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eOperator) = vColorHex
                        End If
                    Case EditorTheme.Tags.ePreprocessorText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.ePreprocessor) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.ePreprocessor) = vColorHex
                        End If
                    Case EditorTheme.Tags.eIdentifierText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eIdentifier) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = vColorHex
                        End If
                    Case EditorTheme.Tags.eSelectionText
                        If pCurrentTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eSelection) Then
                            pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection) = vColorHex
                        End If
                End Select
                
                ' Update preview
                UpdatePreview()
                
                ' Mark as modified
                SetModified(True)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateThemeColor error: {ex.Message}")
            End Try
        End Sub

        
        Private Sub UpdatePreview()
            Try
                If pCurrentTheme Is Nothing Then Return

                ' Apply the theme colors directly to the preview widgets WITHOUT
                ' changing the global theme
                If pPreviewEditor IsNot Nothing Then
                    pPreviewEditor.SetThemeColors(pCurrentTheme)
                    pPreviewEditor.QueueDraw()
                End If

                pPreviewTextBox?.ApplyExplicitTheme(pCurrentTheme)
                pPreviewButton?.ApplyExplicitTheme(pCurrentTheme)
                RefreshColorSwatches()

            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.UpdatePreview error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnNewTheme(vSender As Object, vArgs As EventArgs)
            Try
                ' Create dialog for theme name
                Dim lDialog As New Dialog("New Theme", Me.Toplevel,
                    DialogFlags.Modal Or DialogFlags.DestroyWithParent)

                lDialog.SetDefaultSize(300, 150)

                Dim lContent As Box = CType(lDialog.ContentArea, Box)
                lContent.BorderWidth = 10

                Dim lBox As New Box(Orientation.Vertical, 5)
                lBox.PackStart(New Label("Theme Name:"), False, False, 0)

                Dim lEntry As New CustomDrawTextBox("My Custom Theme")
                lEntry.ThemeManager = pThemeManager
                lBox.PackStart(lEntry, False, False, 0)

                lBox.PackStart(New Label("Base Theme:"), False, False, 0)

                Dim lCombo As New CustomDrawComboBox()
                lCombo.ThemeManager = pThemeManager
                for each lThemeName in pThemeManager.GetAvailableThemes()
                    lCombo.AppendText(lThemeName)
                Next
                lCombo.Active = 0
                lBox.PackStart(lCombo, False, False, 0)

                lContent.PackStart(lBox, True, True, 0)

                Dim lButtonBox As New Box(Orientation.Horizontal, 6)
                lButtonBox.Halign = Align.End
                Dim lCancelButton As New CustomDrawButton("Cancel")
                lCancelButton.ThemeManager = pThemeManager
                AddHandler lCancelButton.Clicked, Sub() lDialog.Respond(ResponseType.Cancel)
                lButtonBox.PackStart(lCancelButton, False, False, 0)
                Dim lCreateButton As New CustomDrawButton("Create")
                lCreateButton.ThemeManager = pThemeManager
                AddHandler lCreateButton.Clicked, Sub() lDialog.Respond(ResponseType.Accept)
                lButtonBox.PackStart(lCreateButton, False, False, 0)
                lContent.PackStart(lButtonBox, False, False, 0)
                AddHandler lEntry.Activated, Sub() lDialog.Respond(ResponseType.Accept)

                lContent.ShowAll()
                lEntry.GrabFocus()

                If lDialog.Run() = CInt(ResponseType.Accept) AndAlso Not String.IsNullOrWhiteSpace(lEntry.Text) Then
                    Dim lNewThemeName As String = lEntry.Text.Trim()
                    Dim lBaseThemeName As String = lCombo.ActiveText

                    ' CreateCustomTheme silently overwrites any existing theme with the same
                    ' name (both in memory and its saved .json file) - confirm with the user
                    ' first rather than losing an existing theme without warning
                    Dim lProceed As Boolean = True
                    If pThemeManager.GetAvailableThemes().Contains(lNewThemeName) Then
                        Dim lOverwriteDialog As New MessageDialog(
                            Me.Toplevel,
                            DialogFlags.Modal,
                            MessageType.Warning,
                            ButtonsType.YesNo,
                            $"A theme named '{lNewThemeName}' already exists. Overwrite it?")
                        lProceed = (lOverwriteDialog.Run() = CInt(ResponseType.Yes))
                        lOverwriteDialog.Destroy()
                    End If

                    If lProceed Then
                        ' Create new theme based on selected base
                        Dim lNewTheme As EditorTheme = pThemeManager.CreateCustomTheme(lBaseThemeName, lNewThemeName)
                        If lNewTheme IsNot Nothing Then
                            LoadThemes()
                            SelectTheme(lNewThemeName)
                        Else
                            ShowError("New Theme", $"Failed to create theme '{lNewThemeName}'.")
                        End If
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnNewTheme error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSaveTheme(vSender As Object, vArgs As EventArgs)
            Try
                If pCurrentTheme Is Nothing OrElse String.IsNullOrEmpty(pCurrentThemeName) Then Return
                
                ' Ensure the theme name matches the tracked name
                pCurrentTheme.Name = pCurrentThemeName
                
                ' Save theme
                Dim lThemesDir As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE", "Themes")
                    
                If Not Directory.Exists(lThemesDir) Then
                    Directory.CreateDirectory(lThemesDir)
                End If
                
                Dim lFilePath As String = System.IO.Path.Combine(lThemesDir, $"{pCurrentThemeName}.json")
                
                If pThemeManager.SaveTheme(pCurrentTheme, lFilePath) Then
                    ' Update the theme in the manager
                    pThemeManager.UpdateCustomTheme(pCurrentThemeName, pCurrentTheme)
                    
                    SetModified(False)
                    
                    ' Show success message
                    Dim lDialog As New MessageDialog(
                        Me.Toplevel,
                        DialogFlags.Modal,
                        MessageType.Info,
                        ButtonsType.Ok,
                        $"Theme '{pCurrentThemeName}' saved successfully.")
                    
                    lDialog.Title = "Theme Saved"
                    lDialog.Run()
                    lDialog.Destroy()
                Else
                    ShowError("Save error", $"Failed to save theme '{pCurrentThemeName}'.")
                End If

            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnSaveTheme error: {ex.Message}")
                ShowError("Save error", ex.Message)
            End Try
        End Sub
        
        Private Sub OnApplyTheme(vSender As Object, vArgs As EventArgs)
            Try
                If pCurrentTheme Is Nothing OrElse String.IsNullOrEmpty(pCurrentThemeName) Then Return
                
                ' Apply theme using the tracked theme name
                pThemeManager.SetTheme(pCurrentThemeName)
                
                ' IMPORTANT: Update the listbox colors after applying the theme
                ApplyThemeToListBoxes()
                
                ' Also update the preview editor
                If pPreviewEditor IsNot Nothing Then
                    pPreviewEditor.ApplyTheme()
                    pPreviewEditor.QueueDraw()
                End If
                
                ' Force the listboxes to redraw with new colors
                pThemeListBox?.QueueDraw()
                pPropertyListBox?.QueueDraw()
                
                ' Raise event with the actual theme object from ThemeManager
                Dim lActualTheme As EditorTheme = pThemeManager.GetTheme(pCurrentThemeName)
                If lActualTheme IsNot Nothing Then
                    RaiseEvent ThemeChanged(lActualTheme)
                End If
                
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
                    Process.Start(New ProcessStartInfo() with {
                        .FileName = lThemesDir,
                        .UseShellExecute = True
                    })
                End If
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnOpenFolder error: {ex.Message}")
            End Try
        End Sub
        
        ' === Helper Methods ===
        
        

        
        Private Sub SetModified(vModified As Boolean)
            pModified = vModified
            pSaveButton.Sensitive = vModified AndAlso pCurrentTheme IsNot Nothing AndAlso IsCustomTheme(pCurrentTheme.Name)
            RaiseEvent Modified(vModified)
        End Sub
        
        Private Sub ShowError(vTitle As String, vMessage As String)
            Dim lDialog As New MessageDialog(
                Me.Toplevel,
                DialogFlags.Modal,
                MessageType.error,
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
        
        ''' <summary>
        ''' Handle color changes from the ColorPicker
        ''' </summary>
        Private Sub OnColorPickerColorChanged(vColor As Gdk.RGBA)
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Update the selected property with the new color
                UpdateSelectedPropertyColor(vColor)
                
                ' Update preview if needed
                UpdatePreview()
                
                ' Mark as modified
                SetModified(True)
                
            Catch ex As Exception
                Console.WriteLine($"OnColorPickerColorChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle color selection (final selection) from the ColorPicker
        ''' </summary>
        Private Sub OnColorPickerColorSelected(vColor As Gdk.RGBA)
            Try
                ' This event fires when user makes a final selection
                ' Could be used to commit changes or update history
                OnColorPickerColorChanged(vColor)
                
            Catch ex As Exception
                Console.WriteLine($"OnColorPickerColorSelected error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle property selection from the property list
        ''' </summary>
        ''' <param name="vIndex">Index of the selected property</param>
        ''' <param name="vItem">The selected ListBoxItem</param>
        Private Sub OnPropertySelected(vIndex As Integer, vItem As ListBoxItem)
            Try
                If vItem Is Nothing OrElse pCurrentTheme Is Nothing Then Return
                
                ' Get property tag from item data
                Dim lTag As EditorTheme.Tags = CType(vItem.Data, EditorTheme.Tags)
                pCurrentPropertyTag = lTag
                
                ' Get current color for this property
                Dim lColorHex As String = GetThemeColor(lTag)
                
                ' Parse color
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(lColorHex) Then
                    ' Use SetColor method for immediate visual update
                    pColorPicker.SetColor(lColor)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnPropertySelected error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Converts hex color string to Gdk.RGBA
        ''' </summary>
        Private Function HexToRgba(vHex As String) As Gdk.RGBA
            Try
                Dim lRgba As New Gdk.RGBA()
                lRgba.Parse(vHex)
                Return lRgba
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.HexToRgba error: {ex.Message}")
                Return New Gdk.RGBA() with {.Red = 0.5, .Green = 0.5, .Blue = 0.5, .Alpha = 1.0}
            End Try
        End Function

        ''' <summary>
        ''' Applies CSS styling to enable visual selection highlighting for ListBoxes
        ''' </summary>
        Private Sub ApplyListBoxSelectionStyles()
            Try
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                Dim lSelectionColor As String = If(lTheme?.SelectionColor, "#007ACC")
                
                ' Create CSS for ListBox selection with higher specificity
                Dim lCss As New System.Text.StringBuilder()
                
                ' Style for selected rows - use multiple selectors for better specificity
                lCss.AppendLine("listbox row:selected,")
                lCss.AppendLine("listbox row.selected,")
                lCss.AppendLine("listbox row:selected label,")
                lCss.AppendLine("listbox row.selected label {")
                lCss.AppendLine($"    background-color: {lSelectionColor} !important;")
                lCss.AppendLine("    color: #FFFFFF !important;")
                lCss.AppendLine("}")
                lCss.AppendLine()
                
                ' Style for hover effect
                lCss.AppendLine("listbox row:hover:Not(:selected) {")
                lCss.AppendLine("    background-color: rgba(0, 122, 204, 0.1);")
                lCss.AppendLine("}")
                lCss.AppendLine()
                
                ' Apply to both list boxes with higher priority
                CssHelper.ApplyCssToWidget(pThemeListBox, lCss.ToString(), 900)
                CssHelper.ApplyCssToWidget(pPropertyListBox, lCss.ToString(), 900)
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.ApplyListBoxSelectionStyles error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks if a theme is a custom (user-created) theme
        ''' </summary>
        Private Function IsCustomTheme(vThemeName As String) As Boolean
            Try
                Return pThemeManager.IsCustomTheme(vThemeName)
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.IsCustomTheme error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Selects a theme in the list by name
        ''' </summary>
        ''' <summary>
        ''' Selects a theme in the list by name
        ''' </summary>
        Private Sub SelectTheme(vThemeName As String)
            Try
                pThemeListBox.SelectByText(vThemeName)
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.SelectTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initializes the context menu for theme list
        ''' </summary>
        Private Sub InitializeThemeContextMenu()
            Try
                pThemeContextMenu = New Menu()
                
                ' Copy theme item
                Dim lCopyItem As New MenuItem("Copy Theme")
                AddHandler lCopyItem.Activated, AddressOf OnContextMenuCopyTheme
                pThemeContextMenu.Append(lCopyItem)
                
                ' Export theme item - ADD THIS
                Dim lExportItem As New MenuItem("Export Theme...")
                AddHandler lExportItem.Activated, AddressOf OnContextMenuExportTheme
                pThemeContextMenu.Append(lExportItem)
                
                pThemeContextMenu.Append(New SeparatorMenuItem())
                
                ' Delete theme item
                Dim lDeleteItem As New MenuItem("Delete Theme")
                AddHandler lDeleteItem.Activated, AddressOf OnContextMenuDeleteTheme
                pThemeContextMenu.Append(lDeleteItem)
                
                pThemeContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.InitializeThemeContextMenu error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Initializes the property list context menu (disabled)
        ''' </summary>
        Private Sub InitializePropertyContextMenu()
            Try
                ' Property list doesn't need a context menu, so we just handle the event
                ' to prevent the default context menu from appearing
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.InitializePropertyContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles delete theme from context menu
        ''' </summary>
        Private Sub OnContextMenuDeleteTheme(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelectedItem As ListBoxItem = pThemeListBox.SelectedItem
                If lSelectedItem Is Nothing Then Return
                
                Dim lThemeName As String = lSelectedItem.Text
                If String.IsNullOrEmpty(lThemeName) Then Return
                
                ' Confirm deletion
                Dim lDialog As New MessageDialog(
                    Me.Toplevel,
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.YesNo,
                    $"Are you sure you want To delete the theme '{lThemeName}'?{Environment.NewLine}{Environment.NewLine}This action cannot be undone.")
                
                lDialog.Title = "Delete Theme"
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    ' Delete the theme
                    If pThemeManager.DeleteTheme(lThemeName) Then
                        ' Reload theme list
                        LoadThemes()
                        
                        ' Clear current selection if it was the deleted theme
                        If pCurrentTheme IsNot Nothing AndAlso pCurrentTheme.Name = lThemeName Then
                            pCurrentTheme = Nothing
                            pThemeNameLabel.Markup = "<b>No Theme Selected</b>"
                            LoadProperties()
                        End If
                        
                        ' Show confirmation
                        UpdateStatusMessage($"Theme '{lThemeName}' deleted successfully.")
                    Else
                        ' Show error
                        Dim lErrorDialog As New MessageDialog(
                            Me.Toplevel,
                            DialogFlags.Modal,
                            MessageType.error,
                            ButtonsType.Ok,
                            $"Failed To delete theme '{lThemeName}'.")
                        
                        lErrorDialog.Title = "Delete Failed"
                        lErrorDialog.Run()
                        lErrorDialog.Destroy()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnContextMenuDeleteTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles duplicate theme from context menu
        ''' </summary>
        Private Sub OnContextMenuCopyTheme(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelectedItem As ListBoxItem = pThemeListBox.SelectedItem
                If lSelectedItem Is Nothing Then Return
                
                Dim lThemeName As String = lSelectedItem.Text
                If String.IsNullOrEmpty(lThemeName) Then Return
                
                ' Create dialog for new theme name
                Dim lDialog As New Dialog("Copy Theme", Me.Toplevel,
                    DialogFlags.Modal Or DialogFlags.DestroyWithParent)

                lDialog.SetDefaultSize(350, 150)

                Dim lContent As Box = CType(lDialog.ContentArea, Box)
                lContent.BorderWidth = 10

                Dim lBox As New Box(Orientation.Vertical, 5)
                lBox.PackStart(New Label("New Theme Name:"), False, False, 0)

                Dim lEntry As New CustomDrawTextBox()
                lEntry.Text = $"{lThemeName} Copy"
                lEntry.ThemeManager = pThemeManager
                lBox.PackStart(lEntry, False, False, 0)

                lContent.PackStart(lBox, True, True, 0)

                Dim lButtonBox As New Box(Orientation.Horizontal, 6)
                lButtonBox.Halign = Align.End
                Dim lCancelButton As New CustomDrawButton("Cancel")
                lCancelButton.ThemeManager = pThemeManager
                AddHandler lCancelButton.Clicked, Sub() lDialog.Respond(ResponseType.Cancel)
                lButtonBox.PackStart(lCancelButton, False, False, 0)
                Dim lCreateButton As New CustomDrawButton("Create")
                lCreateButton.ThemeManager = pThemeManager
                AddHandler lCreateButton.Clicked, Sub() lDialog.Respond(ResponseType.Accept)
                lButtonBox.PackStart(lCreateButton, False, False, 0)
                lContent.PackStart(lButtonBox, False, False, 0)
                AddHandler lEntry.Activated, Sub() lDialog.Respond(ResponseType.Accept)

                lContent.ShowAll()
                lEntry.GrabFocus()
                lEntry.InnerEntry.SelectRegion(0, -1) ' Select all text

                If lDialog.Run() = CInt(ResponseType.Accept) AndAlso Not String.IsNullOrWhiteSpace(lEntry.Text) Then
                    Dim lNewThemeName As String = lEntry.Text.Trim()
                    
                    ' Create duplicate theme
                    Dim lNewTheme As EditorTheme = pThemeManager.CreateCustomTheme(lThemeName, lNewThemeName)
                    If lNewTheme IsNot Nothing Then
                        LoadThemes()
                        SelectTheme(lNewThemeName)
                        UpdateStatusMessage($"Theme '{lNewThemeName}' created successfully.")
                    Else
                        ' Show error
                        Dim lErrorDialog As New MessageDialog(
                            Me.Toplevel,
                            DialogFlags.Modal,
                            MessageType.error,
                            ButtonsType.Ok,
                            $"Failed To create theme '{lNewThemeName}'.")
                        
                        lErrorDialog.Title = "Copy Failed"
                        lErrorDialog.Run()
                        lErrorDialog.Destroy()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnContextMenuCopyTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles export theme from context menu
        ''' </summary>
        Private Sub OnContextMenuExportTheme(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelectedItem As ListBoxItem = pThemeListBox.SelectedItem
                If lSelectedItem Is Nothing Then Return
                
                Dim lThemeName As String = lSelectedItem.Text
                If String.IsNullOrEmpty(lThemeName) Then Return
                
                ' Get the theme
                Dim lTheme As EditorTheme = pThemeManager.GetTheme(lThemeName)
                If lTheme Is Nothing Then Return
                
                ' Create file chooser dialog
                Dim lDialog As New FileChooserDialog(
                    "Export Theme",
                    Me.Toplevel,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Export", ResponseType.Accept)
                
                lDialog.CurrentName = $"{lThemeName}.json"
                
                ' Set default folder to user's Documents
                Dim lDocsPath As String = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                lDialog.SetCurrentFolder(lDocsPath)
                
                ' Add filter for JSON files
                Dim lFilter As New FileFilter()
                lFilter.Name = "JSON Theme Files (*.json)"
                lFilter.AddPattern("*.json")
                lDialog.AddFilter(lFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lFilePath As String = lDialog.Filename
                    
                    ' Ensure .json extension
                    If Not lFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) Then
                        lFilePath &= ".json"
                    End If
                    
                    ' Save theme to file
                    If pThemeManager.SaveTheme(lTheme, lFilePath) Then
                        UpdateStatusMessage($"Theme exported To: {lFilePath}")
                        
                        ' Show success dialog
                        Dim lSuccessDialog As New MessageDialog(
                            Me.Toplevel,
                            DialogFlags.Modal,
                            MessageType.Info,
                            ButtonsType.Ok,
                            $"Theme '{lThemeName}' exported successfully to:{Environment.NewLine}{lFilePath}")
                        
                        lSuccessDialog.Title = "Export Successful"
                        lSuccessDialog.Run()
                        lSuccessDialog.Destroy()
                    Else
                        ' Show error
                        Dim lErrorDialog As New MessageDialog(
                            Me.Toplevel,
                            DialogFlags.Modal,
                            MessageType.error,
                            ButtonsType.Ok,
                            "Failed To export theme.")
                        
                        lErrorDialog.Title = "Export Failed"
                        lErrorDialog.Run()
                        lErrorDialog.Destroy()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnContextMenuExportTheme error: {ex.Message}")
                ShowError("Export error", ex.Message)
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method for status messages
        ''' </summary>
        Private Sub UpdateStatusMessage(vMessage As String)
            Try
                ' TODO: Connect to your main window's status bar
                #If DEBUG Then
                Console.WriteLine($"Status: {vMessage}")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.UpdateStatusMessage error: {ex.Message}")
            End Try
        End Sub
        
        
        ''' <summary>
        ''' Handle theme selection from the theme list
        ''' </summary>
        Private Sub OnThemeSelected(vIndex As Integer, vItem As ListBoxItem)
            Try
                If vItem Is Nothing Then Return
                Dim lThemeName As String = vItem.Text
                #If DEBUG Then
                Console.WriteLine($"OnThemeSelected Called: " + lThemeName )
                #End If
                
                ' Store the actual theme name
                pCurrentThemeName = lThemeName
                
                ' Load the theme
                pCurrentTheme = pThemeManager.GetTheme(lThemeName)
                If pCurrentTheme IsNot Nothing Then
                    ' Clone it for editing if it's a custom theme
                    If IsCustomTheme(lThemeName) Then
                        pCurrentTheme = pCurrentTheme.Clone()
                        ' Keep the correct name after cloning
                        pCurrentTheme.Name = lThemeName
                    End If
                    
                    ' Update UI
                    pThemeNameLabel.Markup = $"<b>{lThemeName}</b>"
                    LoadProperties()
                    
                    ' Reset property selection  
                    pCurrentPropertyTag = EditorTheme.Tags.eBackgroundColor
                    
                    ' Set initial color in ColorPicker (background color by default)
                    Dim lColor As New Gdk.RGBA()
                    If lColor.Parse(pCurrentTheme.BackgroundColor) Then
                        pColorPicker.CurrentColor = lColor
                    End If
                    
                    ' Enable/disable buttons based on theme type
                    Dim lIsCustom As Boolean = IsCustomTheme(lThemeName)
                    pSaveButton.Sensitive = lIsCustom
                    pApplyButton.Sensitive = True
                    
                    ' Load theme colors to custom palette
                    LoadThemeColorsToCustomPalette()
                    
                    ' Apply theme to preview WITHOUT flashing the entire UI
                    If pPreviewEditor IsNot Nothing Then
                        pPreviewEditor.SetThemeColors(pCurrentTheme)
                        pPreviewEditor.QueueDraw()
                        #If DEBUG Then
                        Console.WriteLine($"pPreviewEditor SetThemeColors called in OnThemeSelected")
                        #End If
                    End If
                    pPreviewTextBox?.ApplyExplicitTheme(pCurrentTheme)
                    pPreviewButton?.ApplyExplicitTheme(pCurrentTheme)
                    RefreshColorSwatches()
                End If

            Catch ex As Exception
                Console.WriteLine($"OnThemeSelected error: {ex.Message}")
            End Try
        End Sub



        ''' <summary>
        ''' Gets the color for a specific property tag
        ''' </summary>
        ''' <param name="vTag">The theme property tag</param>
        ''' <returns>Hex color string</returns>
        Private Function GetThemeColor(vTag As EditorTheme.Tags) As String
            Try
                If pCurrentTheme Is Nothing Then Return "#000000"
                
                Select Case vTag
                    Case EditorTheme.Tags.eBackgroundColor
                        Return pCurrentTheme.BackgroundColor
                    Case EditorTheme.Tags.eForegroundColor
                        Return pCurrentTheme.ForegroundColor
                    Case EditorTheme.Tags.eSelectionColor
                        Return pCurrentTheme.SelectionColor
                    Case EditorTheme.Tags.eCurrentLineColor
                        Return pCurrentTheme.CurrentLineColor
                    Case EditorTheme.Tags.eLineNumberColor
                        Return pCurrentTheme.LineNumberColor
                    Case EditorTheme.Tags.eLineNumberBackgroundColor
                        Return pCurrentTheme.LineNumberBackgroundColor
                    Case EditorTheme.Tags.eCurrentLineNumberColor
                        Return pCurrentTheme.CurrentLineNumberColor
                    Case EditorTheme.Tags.eCursorColor
                        Return pCurrentTheme.CursorColor

                    ' Tab colors (NEW)
                    Case EditorTheme.Tags.eEditorBackgroundColor
                        Return pCurrentTheme.EditorBackgroundColor
                    Case EditorTheme.Tags.eTabInactiveColor
                        Return pCurrentTheme.TabInactiveColor
                    Case EditorTheme.Tags.eTabHoverColor
                        Return pCurrentTheme.TabHoverColor
                    Case EditorTheme.Tags.eAccentColor
                        Return pCurrentTheme.AccentColor
                    Case EditorTheme.Tags.eBevelLightColor
                        Return If(String.IsNullOrEmpty(pCurrentTheme.BevelLightColor), pCurrentTheme.BackgroundColor, pCurrentTheme.BevelLightColor)
                    Case EditorTheme.Tags.eBevelDarkColor
                        Return If(String.IsNullOrEmpty(pCurrentTheme.BevelDarkColor), pCurrentTheme.BackgroundColor, pCurrentTheme.BevelDarkColor)
                    Case EditorTheme.Tags.eDisabledForegroundColor
                        Return pCurrentTheme.GetColor(EditorTheme.Tags.eDisabledForegroundColor)

                    Case EditorTheme.Tags.eErrorColor
                        Return pCurrentTheme.ErrorColor
                    Case EditorTheme.Tags.eWarningColor
                        Return pCurrentTheme.WarningColor
                    Case EditorTheme.Tags.eInfoColor
                        Return pCurrentTheme.InfoColor
                    Case EditorTheme.Tags.eSuccessColor
                        Return pCurrentTheme.SuccessColor
                    Case EditorTheme.Tags.eKeywordText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword)
                    Case EditorTheme.Tags.eStringText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eString)
                    Case EditorTheme.Tags.eCommentText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eComment)
                    Case EditorTheme.Tags.eNumberText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber)
                    Case EditorTheme.Tags.eOperatorText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eOperator)
                    Case EditorTheme.Tags.ePreprocessorText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.ePreprocessor)
                    Case EditorTheme.Tags.eIdentifierText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eIdentifier)
                    Case EditorTheme.Tags.eTypeText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eType)
                    Case EditorTheme.Tags.eSelectionText
                        Return pCurrentTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection)
                    Case Else
                        Return "#000000"
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetThemeColor error: {ex.Message}")
                Return "#000000"
            End Try
        End Function

        ''' <summary>
        ''' Handle context menu request for themes
        ''' </summary>
        Private Sub OnThemeContextMenuRequested(vIndex As Integer, vItem As ListBoxItem, vEvent As Gdk.Event)
            Try
                If vItem Is Nothing Then Return
                
                Dim lThemeName As String = vItem.Text
                Dim lIsCustom As Boolean = DirectCast(vItem.Data, Boolean)
                
                ' Update menu items sensitivity
                for each lItem As Widget in pThemeContextMenu.Children
                    If TypeOf lItem Is MenuItem Then
                        Dim lMenuItem As MenuItem = DirectCast(lItem, MenuItem)
                        Select Case lMenuItem.Label
                            Case "Delete Theme"
                                lMenuItem.Sensitive = lIsCustom
                            Case "Copy Theme"
                                lMenuItem.Sensitive = True
                        End Select
                    End If
                Next
                
                ' Show context menu
                pThemeContextMenu.PopupAtPointer(vEvent)
                
            Catch ex As Exception
                Console.WriteLine($"OnThemeContextMenuRequested error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Lightens a color by the specified amount
        ''' </summary>
        Private Function LightenColor(vHexColor As String, vAmount As Double) As String
            Try
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor
                
                ' Lighten the color
                lColor.Red = Math.Min(1.0, lColor.Red + vAmount)
                lColor.Green = Math.Min(1.0, lColor.Green + vAmount)
                lColor.Blue = Math.Min(1.0, lColor.Blue + vAmount)
                
                ' Convert back to hex
                Return String.Format("#{0:X2}{1:X2}{2:X2}", 
                    CInt(lColor.Red * 255), 
                    CInt(lColor.Green * 255), 
                    CInt(lColor.Blue * 255))
                
            Catch ex As Exception
                Console.WriteLine($"LightenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function

        ''' <summary>
        ''' Updates the currently selected property with a new color
        ''' </summary>
        Private Sub UpdateSelectedPropertyColor(vColor As Gdk.RGBA)
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Convert RGBA to hex string
                Dim lRed As Integer = CInt(vColor.Red * 255)
                Dim lGreen As Integer = CInt(vColor.Green * 255)
                Dim lBlue As Integer = CInt(vColor.Blue * 255)
                Dim lColorHex As String = $"#{lRed:X2}{lGreen:X2}{lBlue:X2}"
                
                ' Update theme property based on current selection
                UpdateThemeColor(pCurrentPropertyTag, lColorHex)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSelectedPropertyColor error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Loads theme colors into the ColorPicker's custom color slots
        ''' </summary>
        Private Sub LoadThemeColorsToCustomPalette()
            Try
                If pCurrentTheme Is Nothing Then Return
                
                Dim lColors As New List(Of Gdk.RGBA)
                
                ' Add main theme colors
                AddColorToList(lColors, pCurrentTheme.BackgroundColor)
                AddColorToList(lColors, pCurrentTheme.ForegroundColor)
                AddColorToList(lColors, pCurrentTheme.SelectionColor)
                AddColorToList(lColors, pCurrentTheme.CurrentLineColor)
                AddColorToList(lColors, pCurrentTheme.LineNumberColor)
                AddColorToList(lColors, pCurrentTheme.CursorColor)
                
                ' Add syntax colors
                for each kvp in pCurrentTheme.SyntaxColors
                    AddColorToList(lColors, kvp.Value)
                    If lColors.Count >= 16 Then Exit for ' ColorPicker has 16 custom slots
                Next
                
                ' Set custom colors in ColorPicker
                for i As Integer = 0 To Math.Min(lColors.Count - 1, 15)
                    pColorPicker.SetCustomColor(i, lColors(i))
                Next
                
            Catch ex As Exception
                Console.WriteLine($"LoadThemeColorsToCustomPalette error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper to add color to list if valid
        ''' </summary>
        Private Sub AddColorToList(vList As List(Of Gdk.RGBA), vColorHex As String)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vColorHex) Then
                    vList.Add(lColor)
                End If
            Catch ex As Exception
                ' Ignore invalid colors
            End Try
        End Sub

        ''' <summary>
        ''' Builds a small bordered color swatch for previewing a theme color that has no
        ''' other widget to show it in this dialog - its name is exposed only as a hover
        ''' tooltip (not a packed label) so adding it to the Interface Preview row doesn't
        ''' add any height beyond the row's existing tallest control
        ''' </summary>
        ''' <param name="vLabel">Name shown as this swatch's tooltip</param>
        ''' <param name="vTag">Theme property this swatch tracks</param>
        ''' <returns>The swatch DrawingArea, ready to pack directly</returns>
        Private Function CreateColorSwatch(vLabel As String, vTag As EditorTheme.Tags) As Widget
            Dim lSwatch As New DrawingArea()
            lSwatch.SetSizeRequest(22, 22)
            lSwatch.TooltipText = vLabel
            lSwatch.Valign = Align.Center
            AddHandler lSwatch.Drawn,
                Sub(vSender As Object, vArgs As DrawnArgs)
                    Try
                        Dim lContext As Cairo.Context = vArgs.Cr
                        Dim lColor As New Gdk.RGBA()
                        If lColor.Parse(GetThemeColor(vTag)) Then
                            lContext.SetSourceRGB(lColor.Red, lColor.Green, lColor.Blue)
                        Else
                            lContext.SetSourceRGB(0, 0, 0)
                        End If
                        lContext.Rectangle(0, 0, lSwatch.AllocatedWidth, lSwatch.AllocatedHeight)
                        lContext.Fill()

                        lContext.SetSourceRGB(0.5, 0.5, 0.5)
                        lContext.LineWidth = 1
                        lContext.Rectangle(0.5, 0.5, lSwatch.AllocatedWidth - 1, lSwatch.AllocatedHeight - 1)
                        lContext.Stroke()
                    Catch ex As Exception
                        Console.WriteLine($"ThemeEditor.CreateColorSwatch draw error: {ex.Message}")
                    End Try
                End Sub
            pColorSwatches(vTag) = lSwatch

            Return lSwatch
        End Function

        ''' <summary>
        ''' Redraws every color swatch so it picks up pCurrentTheme's latest values
        ''' </summary>
        Private Sub RefreshColorSwatches()
            for each lSwatch In pColorSwatches.Values
                lSwatch.QueueDraw()
            Next
        End Sub

        ''' <summary>
        ''' Applies current theme colors to both list boxes
        ''' </summary>
        Private Sub ApplyThemeToListBoxes()
            Try
                ' Ensure ThemeManager is set on both listboxes
                If pThemeListBox.ThemeManager Is Nothing Then
                    pThemeListBox.ThemeManager = pThemeManager
                End If
                If pPropertyListBox.ThemeManager Is Nothing Then
                    pPropertyListBox.ThemeManager = pThemeManager
                End If
                
                ' Use the UpdateFromTheme method which gets colors from ThemeManager
                pThemeListBox.UpdateFromTheme()
                pPropertyListBox.UpdateFromTheme()
                
                ' Force immediate redraw
                pThemeListBox.QueueDraw()
                pPropertyListBox.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyThemeToListBoxes error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Refreshes the theme editor when theme changes externally
        ''' </summary>
        Public Sub RefreshTheme()
            Try
                ' Apply theme colors to listboxes
                ApplyThemeToListBoxes()
                
                ' Refresh preview editor if exists
                If pPreviewEditor IsNot Nothing AndAlso pCurrentTheme IsNot Nothing Then
                    ' Set the theme in the theme manager first
                    pThemeManager.SetTheme(pCurrentThemeName)
                    ' Then apply it to the editor
                    pPreviewEditor.ApplyTheme()
                    pPreviewEditor.QueueDraw()
                End If
                
                ' Force redraw of all components
                Me.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.RefreshTheme error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles theme changes from the ThemeManager
        ''' </summary>
        Private Sub OnThemeManagerThemeChanged(vTheme As EditorTheme)
            Try
                ' Update the listbox colors when theme changes
                ApplyThemeToListBoxes()
                
                ' Force redraw
                pThemeListBox?.QueueDraw()
                pPropertyListBox?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnThemeManagerThemeChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles theme applied events from the ThemeManager
        ''' </summary>
        Private Sub OnThemeManagerThemeApplied(vThemeName As String)
            Try
                ' Update the listbox colors when theme is applied
                ApplyThemeToListBoxes()
                
                ' Force redraw
                pThemeListBox?.QueueDraw()
                pPropertyListBox?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ThemeEditor.OnThemeManagerThemeApplied error: {ex.Message}")
            End Try
        End Sub



        ''' </summary>
        ''' <param name="vIndex">The index of the selected item</param>
        ''' <param name="vItem">The selected ListBoxItem</param>
        Private Sub OnPropertyListBoxSelectionChanged(vIndex As Integer, vItem As ListBoxItem)


        End Sub
        
    End Class
    
End Namespace
