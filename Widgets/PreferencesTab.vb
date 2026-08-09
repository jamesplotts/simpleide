' Widgets/PreferencesTab.vb - Preferences displayed as a tab in the main notebook
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces

' PreferencesTab.vb
' Created: 2025-08-20 23:04:57

Namespace Widgets
    
    ''' <summary>
    ''' Manages preferences display as a tab in the main notebook instead of a dialog
    ''' </summary>
    Public Class PreferencesTab
        Inherits Box
        'Implements IEditor  ' Implement IEditor so it can be used as a tab
        
        ' ===== Private Fields =====
        Private pNotebook As Notebook
        Private pSettingsManager As SettingsManager
        Private pThemeManager As ThemeManager
        Private pHasUnsavedChanges As Boolean = False
        Private pFilePath As String = "Preferences"  ' Virtual file path for tab
        
        ' General tab controls
        Private pShowSplashCheck As CustomDrawCheckBox
        Private pRestoreLayoutCheck As CheckButton
        Private pAutoSaveCheck As CheckButton
        Private pAutoSaveIntervalSpin As SpinButton
        Private pRecentFilesCountSpin As SpinButton
        Private pPreferWebKitCheck As CustomDrawCheckBox
        
        ' Editor tab controls
        Private pFontButton As FontButton
        Private pTabWidthSpin As SpinButton
        Private pUndoHistorySizeSpin As SpinButton
        Private pUseTabsRadio As RadioButton
        Private pUseSpacesRadio As RadioButton
        Private pShowLineNumbersCheck As CheckButton
        Private pHighlightCurrentLineCheck As CheckButton
        Private pWordWrapCheck As CheckButton
        Private pAutoIndentCheck As CheckButton
        Private pShowWhitespaceCheck As CheckButton
        Private pShowEndOfLineCheck As CheckButton
        
        ' Build tab controls
        Private pDefaultConfigCombo As CustomDrawComboBox
        Private pDefaultPlatformCombo As CustomDrawComboBox
        Private pVerbosityCombo As CustomDrawComboBox
        Private pParallelBuildCheck As CheckButton
        Private pRestorePackagesCheck As CheckButton
        Private pShowOutputCheck As CheckButton
        Private pClearOutputCheck As CheckButton
        
        ' Git tab controls
        Private pGitEnabledCheck As CheckButton
        Private pGitUserNameEntry As CustomDrawTextBox
        Private pGitEmailEntry As CustomDrawTextBox
        Private pDefaultBranchEntry As CustomDrawTextBox
        Private pAutoFetchCheck As CheckButton
        Private pFetchIntervalSpin As SpinButton
        Private pGitCredentialStorageCombo As CustomDrawComboBox
        Private pGitCredentialTypeCombo As CustomDrawComboBox
        Private pGitTokenEntry As CustomDrawTextBox
        Private pGitTokenVisibleCheck As CheckButton
        Private pGitRemoteUrlEntry As CustomDrawTextBox
        Private pCredentialManager As CredentialManager
        
        ' AI tab controls
        Private pAIEnabledCheck As CheckButton
        Private pShowArtifactsCheck As CheckButton
        Private pAutoContextCheck As CheckButton
        Private pMem0EnabledCheck As CheckButton
        Private pAIProviderCombo As CustomDrawComboBox
        Private pAIProviderHelpLabel As Label
        Private pApiKeyLabel As Label
        Private pApiKeyEntry As CustomDrawTextBox
        Private pApiKeyVisibleCheck As CheckButton
        Private pAIBaseUrlLabel As Label
        Private pAIBaseUrlEntry As CustomDrawTextBox
        Private pClaudeCodePathLabel As Label
        Private pClaudeCodePathEntry As CustomDrawTextBox
        Private pAIModelEntry As CustomDrawTextBox
        Private pMaxTokensSpin As SpinButton
        Private pTemperatureSpin As SpinButton
        Private pStreamResponsesCheck As CheckButton
        Private pAutoSuggestCheck As CheckButton
        Private pSaveHistoryCheck As CheckButton
        Private pHistoryLimitSpin As SpinButton
        
        ' Advanced tab controls
        Private pEnableLoggingCheck As CheckButton
        Private pLogLevelCombo As CustomDrawComboBox
        Private pMaxLogSizeSpin As SpinButton
        Private pEnableTelemetryCheck As CheckButton
        Private pCheckUpdatesCheck As CheckButton
        Private pBetaUpdatesCheck As CheckButton

        ' Excluded Directories tab controls
        Private pExcludedDirEntry As CustomDrawTextBox
        Private pExcludedDirListBox As CustomDrawListBox
        Private pAddExcludedDirButton As CustomDrawButton
        Private pRemoveExcludedDirButton As CustomDrawButton

        ' ===== IDE Version Auto-Increment Settings =====
        

								Private pAutoIncrementCheck As  CheckButton

        ' Add these as class-level fields
        Private pVersionControls As List(Of Widget)
        Private pAutoIncrementEnableCheck As CheckButton
        
        ' ===== Events (IEditor Implementation) =====
        Public Event Modified As EventHandler
        Public Event CursorPositionChanged(vLine As Integer, vColumn As Integer)

        ''' <summary>
        ''' Raised after settings are written to pSettingsManager (Save or Apply) - lets
        ''' MainWindow re-apply the new values live (theme, AI client, etc.) instead of
        ''' requiring an app restart
        ''' </summary>
        Public Event SettingsApplied()
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new preferences tab instance
        ''' </summary>
        ''' <param name="vSettingsManager">The settings manager instance</param>
        ''' <param name="vThemeManager">The theme manager instance</param>
        Public Sub New(vSettingsManager As SettingsManager, vThemeManager As ThemeManager)
            MyBase.New(Orientation.Vertical, 0)
            
            pSettingsManager = vSettingsManager
            pThemeManager = vThemeManager
            
            InitializeUI()
            InitializeCredentialManager()
            LoadSettings()
        End Sub

        ''' <summary>
        ''' Initializes the credential manager based on saved settings
        ''' </summary>
        Private Sub InitializeCredentialManager()
            Try
                ' Get saved storage method from settings
                Dim lSavedMethod As String = pSettingsManager.GetString("Git.CredentialStorage", "")
                
                If Not String.IsNullOrEmpty(lSavedMethod) Then
                    ' Try to parse the saved method
                    Dim lMethod As Utilities.CredentialManager.eStorageMethod
                    If [Enum].TryParse(Of Utilities.CredentialManager.eStorageMethod)(lSavedMethod, lMethod) Then
                        pCredentialManager = New Utilities.CredentialManager(lMethod)
                        
                        ' Set the combo box to the saved method
                        Dim lAvailableMethods As List(Of Utilities.CredentialManager.eStorageMethod) = 
                            Utilities.CredentialManager.GetAvailableMethods()
                            
                        Dim lIndex As Integer = lAvailableMethods.IndexOf(lMethod)
                        If lIndex >= 0 AndAlso pGitCredentialStorageCombo IsNot Nothing Then
                            pGitCredentialStorageCombo.Active = lIndex
                        End If
                    End If
                End If
                
                ' If no saved method or invalid, use default
                If pCredentialManager Is Nothing Then
                    pCredentialManager = New Utilities.CredentialManager()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InitializeCredentialManager error: {ex.Message}")
                ' Fallback to default
                pCredentialManager = New Utilities.CredentialManager()
            End Try
        End Sub
        
        ' ===== IEditor Implementation =====
        

        
        ''' <summary>
        ''' Saves the preferences
        ''' </summary>
        Public Function Save() As Boolean 
            Try
                SaveSettings()
                IsModified = False
                Return True
            Catch ex As Exception
                Console.WriteLine($"PreferencesTab.Save error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Save As is not applicable for preferences
        ''' </summary>
        Public Function SaveAs(vFilePath As String) As Boolean 
            Return Save()
        End Function
        
        ''' <summary>
        ''' Loads a file (not applicable for preferences)
        ''' </summary>
        Public Function LoadFile(vFilePath As String) As Boolean 
            LoadSettings()
            Return True
        End Function

        ''' <summary>
        ''' Gets or sets whether the preferences have been modified
        ''' </summary>
        Public Property IsModified As Boolean
            Get
                Return pHasUnsavedChanges
            End Get
            Set(value As Boolean)
                pHasUnsavedChanges = value
            End Set
        End Property
        
        ''' <summary>
        ''' Switches the category notebook to the Git tab - used by MainWindow.ShowGitSettings
        ''' (the Git > Settings... menu command) so it lands directly on Git settings instead
        ''' of just opening Preferences to whatever tab was last active
        ''' </summary>
        Public Sub SelectGitTab()
            Try
                pNotebook.CurrentPage = 3 ' Git tab - see InitializeUI's AppendPage order
            Catch ex As Exception
                Console.WriteLine($"PreferencesTab.SelectGitTab error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Switches the category notebook to the AI tab - used by MainWindow.ShowAISettings
        ''' (the AI > AI Settings... menu command) so it lands directly on AI settings instead
        ''' of just opening Preferences to whatever tab was last active
        ''' </summary>
        Public Sub SelectAITab()
            Try
                pNotebook.CurrentPage = 4 ' AI tab - see InitializeUI's AppendPage order
            Catch ex As Exception
                Console.WriteLine($"PreferencesTab.SelectAITab error: {ex.Message}")
            End Try
        End Sub

        ' ===== UI Initialization =====

        ''' <summary>
        ''' Initializes the preferences UI
        ''' </summary>
        Private Sub InitializeUI()
            Try
                ' Create header with title and buttons
                Dim lHeaderBox As New Box(Orientation.Horizontal, 10)
                lHeaderBox.BorderWidth = 10
                
                Dim lTitle As New Label("<b>Preferences</b>")
                lTitle.UseMarkup = True
                lTitle.Xalign = 0
                lHeaderBox.PackStart(lTitle, True, True, 0)
                
                ' Apply button
                Dim lApplyButton As New CustomDrawButton("Apply")
                lApplyButton.ThemeManager = pThemeManager
                AddHandler lApplyButton.Clicked, AddressOf OnApplyClicked
                lHeaderBox.PackEnd(lApplyButton, False, False, 0)

                ' Save button
                Dim lSaveButton As New CustomDrawButton("Save")
                lSaveButton.ThemeManager = pThemeManager
                AddHandler lSaveButton.Clicked, AddressOf OnSaveClicked
                lHeaderBox.PackEnd(lSaveButton, False, False, 5)
                
                PackStart(lHeaderBox, False, False, 0)
                
                ' Create separator
                PackStart(New Separator(Orientation.Horizontal), False, False, 0)
                
                ' Create notebook for category tabs
                pNotebook = New Notebook()
                pNotebook.BorderWidth = 10
                
                ' Add category tabs - each wrapped in a vertically-scrolling ScrolledWindow so a
                ' tab taller than the available window height (a shorter screen, or just a tab
                ' that's grown over time, like AI has) stays reachable instead of clipping its
                ' bottom controls with no way to get to them
                pNotebook.AppendPage(WrapInScrolledWindow(CreateGeneralTab()), New Label("General"))
                pNotebook.AppendPage(WrapInScrolledWindow(CreateEditorTab()), New Label("Editor"))
                pNotebook.AppendPage(WrapInScrolledWindow(CreateBuildTab()), New Label("Build"))
                pNotebook.AppendPage(WrapInScrolledWindow(CreateGitTab()), New Label("Git"))
                pNotebook.AppendPage(WrapInScrolledWindow(CreateAITab()), New Label("AI"))
                pNotebook.AppendPage(WrapInScrolledWindow(CreateAdvancedTab()), New Label("Advanced"))
                pNotebook.AppendPage(WrapInScrolledWindow(CreateExcludedDirectoriesTab()), New Label("Excluded Directories"))
                pNotebook.AppendPage(WrapInScrolledWindow(CreateVersionTab()), New Label("Project Version"))
                
                PackStart(pNotebook, True, True, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"PreferencesTab.InitializeUI error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Wraps a Preferences tab's content in a vertically-scrolling ScrolledWindow, so the
        ''' page stays fully reachable when its content is taller than the available window
        ''' height instead of silently clipping whatever falls below the fold
        ''' </summary>
        ''' <param name="vContent">The tab content widget returned by a CreateXxxTab method</param>
        Private Function WrapInScrolledWindow(vContent As Widget) As Widget
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Never, PolicyType.Automatic)
            lScrolled.Add(vContent)
            Return lScrolled
        End Function

        ''' <summary>
        ''' Unwraps a notebook page back to the Box a CreateXxxTab method originally returned -
        ''' GetNthPage() now returns the ScrolledWindow WrapInScrolledWindow put around it (with
        ''' GTK3 auto-inserting a Viewport in between for a non-Gtk.IScrollable child), not the
        ''' Box directly, so any code that walks a tab's widget tree needs this instead of
        ''' assuming GetNthPage()'s result IS the Box
        ''' </summary>
        ''' <param name="vPageWidget">The widget returned by pNotebook.GetNthPage()</param>
        ''' <returns>The tab's content Box, or Nothing if the page isn't shaped as expected</returns>
        Private Function GetTabContentBox(vPageWidget As Widget) As Box
            Dim lChild As Widget = vPageWidget
            If TypeOf lChild Is ScrolledWindow Then
                lChild = CType(lChild, ScrolledWindow).Child
            End If
            If TypeOf lChild Is Viewport Then
                lChild = CType(lChild, Viewport).Child
            End If
            Return TryCast(lChild, Box)
        End Function

        ' ===== Tab Creation Methods =====

        ''' <summary>
        ''' Create the IDE Version settings tab (simplified - auto-increment only)
        ''' </summary>
        Private Function CreateVersionTab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.MarginStart = 20
            lBox.MarginEnd = 20
            lBox.MarginTop = 20
            lBox.MarginBottom = 20
            
            ' Title
            Dim lTitleLabel As New Label()
            lTitleLabel.Markup = "<b>Project Version Auto-Increment Settings</b>"
            lTitleLabel.Xalign = 0
            lBox.PackStart(lTitleLabel, False, False, 0)
            
            ' Current version display
            Dim lCurrentVersionBox As New Box(Orientation.Horizontal, 10)
            Dim lCurrentLabel As New Label("Current Project Version:")
            lCurrentVersionBox.PackStart(lCurrentLabel, False, False, 0)
            
            Dim lVersionLabel As New Label(ApplicationVersion.FullVersionString)
            lVersionLabel.Markup = $"<b>{ApplicationVersion.FullVersionString}</b>"
            lCurrentVersionBox.PackStart(lVersionLabel, False, False, 0)
            lBox.PackStart(lCurrentVersionBox, False, False, 0)
            
            ' Separator
            lBox.PackStart(New Separator(Orientation.Horizontal), False, False, 0)
            
            ' Auto-increment enable checkbox
            pAutoIncrementCheck = New CheckButton("Enable auto-increment IDE version on build")
            pAutoIncrementCheck.Active = pSettingsManager.AutoIncrementVersion
            pAutoIncrementCheck.TooltipText = "When enabled, the Project version number will be incremented automatically on every build"
            AddHandler pAutoIncrementCheck.Toggled, Sub()
                pSettingsManager.AutoIncrementVersion = pAutoIncrementCheck.Active
                OnSettingChanged(Nothing, Nothing)
            End Sub
            lBox.PackStart(pAutoIncrementCheck, False, False, 0)
            
            
            ' Info label about the behavior
            Dim lInfoLabel As New Label()
            lInfoLabel.Markup = "<small>The version will increment on every build when enabled.</small>"
            lInfoLabel.Xalign = 0
            lInfoLabel.MarginTop = 20
            lBox.PackStart(lInfoLabel, False, False, 0)
            
            Return lBox
        End Function

        ''' <summary>
        ''' Creates the General settings tab
        ''' </summary>
        Private Function CreateGeneralTab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.BorderWidth = 10
            
            ' Application Settings
            Dim lAppFrame As New Frame("Application Settings")
            Dim lAppBox As New Box(Orientation.Vertical, 5)
            lAppBox.BorderWidth = 10
            
            pShowSplashCheck = New CustomDrawCheckBox("Show splash screen on startup")
            pShowSplashCheck.ThemeManager = pThemeManager
            AddHandler pShowSplashCheck.Toggled, AddressOf OnSettingChanged
            lAppBox.PackStart(pShowSplashCheck, False, False, 0)
            
            pRestoreLayoutCheck = New CheckButton("Restore window layout on startup")
            AddHandler pRestoreLayoutCheck.Toggled, AddressOf OnSettingChanged
            lAppBox.PackStart(pRestoreLayoutCheck, False, False, 0)
            
            ' Auto-save settings
            Dim lAutoSaveBox As New Box(Orientation.Horizontal, 5)
            pAutoSaveCheck = New CheckButton("Auto-save files every")
            AddHandler pAutoSaveCheck.Toggled, AddressOf OnAutoSaveToggled
            lAutoSaveBox.PackStart(pAutoSaveCheck, False, False, 0)
            
            pAutoSaveIntervalSpin = New SpinButton(1, 60, 1)
            lAutoSaveBox.PackStart(pAutoSaveIntervalSpin, False, False, 0)
            lAutoSaveBox.PackStart(New Label("minutes"), False, False, 0)
            AddHandler pAutoSaveIntervalSpin.ValueChanged, AddressOf OnSettingChanged
            
            lAppBox.PackStart(lAutoSaveBox, False, False, 0)
            
            ' Recent files
            Dim lRecentBox As New Box(Orientation.Horizontal, 5)
            lRecentBox.PackStart(New Label("Recent files count:"), False, False, 0)
            pRecentFilesCountSpin = New SpinButton(5, 50, 1)
            AddHandler pRecentFilesCountSpin.ValueChanged, AddressOf OnSettingChanged
            lRecentBox.PackStart(pRecentFilesCountSpin, False, False, 0)
            lAppBox.PackStart(lRecentBox, False, False, 0)
            
            lAppFrame.Add(lAppBox)
            lBox.PackStart(lAppFrame, False, False, 0)

            ' Help / Documentation rendering
            Dim lHelpFrame As New Frame("Help Tab")
            Dim lHelpBox As New Box(Orientation.Vertical, 5)
            lHelpBox.BorderWidth = 10

            pPreferWebKitCheck = New CustomDrawCheckBox("Prefer native WebKit rendering when available")
            pPreferWebKitCheck.ThemeManager = pThemeManager
            pPreferWebKitCheck.TooltipText = "Real, JavaScript-capable page rendering via the system's WebKitGTK. " &
                "Turn off to force the built-in litehtml renderer (no JavaScript) even when WebKitGTK is installed - " &
                "useful for troubleshooting."
            AddHandler pPreferWebKitCheck.Toggled, AddressOf OnSettingChanged
            lHelpBox.PackStart(pPreferWebKitCheck, False, False, 0)

            lHelpFrame.Add(lHelpBox)
            lBox.PackStart(lHelpFrame, False, False, 0)

            Return lBox
        End Function
        
        ''' <summary>
        ''' Creates the Editor settings tab
        ''' </summary>
        Private Function CreateEditorTab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.BorderWidth = 10
            
            ' Font Settings
            Dim lFontFrame As New Frame("Font Settings")
            Dim lFontBox As New Box(Orientation.Horizontal, 5)
            lFontBox.BorderWidth = 10
            
            lFontBox.PackStart(New Label("Editor font:"), False, False, 0)
            pFontButton = New FontButton()
            AddHandler pFontButton.FontSet, AddressOf OnSettingChanged
            lFontBox.PackStart(pFontButton, True, True, 0)
            
            lFontFrame.Add(lFontBox)
            lBox.PackStart(lFontFrame, False, False, 0)
            
            ' Tab Settings
            Dim lTabFrame As New Frame("Tab Settings")
            Dim lTabBox As New Box(Orientation.Vertical, 5)
            lTabBox.BorderWidth = 10
            
            Dim lTabWidthBox As New Box(Orientation.Horizontal, 5)
            lTabWidthBox.PackStart(New Label("Tab width:"), False, False, 0)
            pTabWidthSpin = New SpinButton(1, 8, 1)
            AddHandler pTabWidthSpin.ValueChanged, AddressOf OnSettingChanged
            lTabWidthBox.PackStart(pTabWidthSpin, False, False, 0)
            lTabBox.PackStart(lTabWidthBox, False, False, 0)
            
            pUseTabsRadio = New RadioButton("Use tabs")
            AddHandler pUseTabsRadio.Toggled, AddressOf OnSettingChanged
            lTabBox.PackStart(pUseTabsRadio, False, False, 0)
            
            pUseSpacesRadio = New RadioButton(pUseTabsRadio, "Use spaces")
            AddHandler pUseSpacesRadio.Toggled, AddressOf OnSettingChanged
            lTabBox.PackStart(pUseSpacesRadio, False, False, 0)
            
            lTabFrame.Add(lTabBox)
            lBox.PackStart(lTabFrame, False, False, 0)

            ' Undo Settings
            Dim lUndoFrame As New Frame("Undo Settings")
            Dim lUndoBox As New Box(Orientation.Horizontal, 5)
            lUndoBox.BorderWidth = 10

            lUndoBox.PackStart(New Label("Undo history size:"), False, False, 0)
            pUndoHistorySizeSpin = New SpinButton(100, 100000, 100)
            AddHandler pUndoHistorySizeSpin.ValueChanged, AddressOf OnSettingChanged
            lUndoBox.PackStart(pUndoHistorySizeSpin, False, False, 0)

            lUndoFrame.Add(lUndoBox)
            lBox.PackStart(lUndoFrame, False, False, 0)

            ' Display Settings
            Dim lDisplayFrame As New Frame("Display Settings")
            Dim lDisplayBox As New Box(Orientation.Vertical, 5)
            lDisplayBox.BorderWidth = 10
            
            pShowLineNumbersCheck = New CheckButton("Show line numbers")
            AddHandler pShowLineNumbersCheck.Toggled, AddressOf OnSettingChanged
            lDisplayBox.PackStart(pShowLineNumbersCheck, False, False, 0)
            
            pHighlightCurrentLineCheck = New CheckButton("Highlight current line")
            AddHandler pHighlightCurrentLineCheck.Toggled, AddressOf OnSettingChanged
            lDisplayBox.PackStart(pHighlightCurrentLineCheck, False, False, 0)
            
            pWordWrapCheck = New CheckButton("Word wrap")
            AddHandler pWordWrapCheck.Toggled, AddressOf OnSettingChanged
            lDisplayBox.PackStart(pWordWrapCheck, False, False, 0)
            
            pAutoIndentCheck = New CheckButton("Auto indent")
            AddHandler pAutoIndentCheck.Toggled, AddressOf OnSettingChanged
            lDisplayBox.PackStart(pAutoIndentCheck, False, False, 0)
            
            pShowWhitespaceCheck = New CheckButton("Show whitespace")
            AddHandler pShowWhitespaceCheck.Toggled, AddressOf OnSettingChanged
            lDisplayBox.PackStart(pShowWhitespaceCheck, False, False, 0)
            
            pShowEndOfLineCheck = New CheckButton("Show end of line")
            AddHandler pShowEndOfLineCheck.Toggled, AddressOf OnSettingChanged
            lDisplayBox.PackStart(pShowEndOfLineCheck, False, False, 0)
            
            lDisplayFrame.Add(lDisplayBox)
            lBox.PackStart(lDisplayFrame, False, False, 0)
            
            Return lBox
        End Function
        
        ''' <summary>
        ''' Creates the Build settings tab
        ''' </summary>
        Private Function CreateBuildTab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.BorderWidth = 10
            
            ' Build Configuration
            Dim lConfigFrame As New Frame("Build Configuration")
            Dim lConfigBox As New Box(Orientation.Vertical, 5)
            lConfigBox.BorderWidth = 10
            
            Dim lDefaultConfigBox As New Box(Orientation.Horizontal, 10)
            lDefaultConfigBox.PackStart(New Label("Default configuration:"), False, False, 0)
            pDefaultConfigCombo = New CustomDrawComboBox()
            pDefaultConfigCombo.ThemeManager = pThemeManager
            pDefaultConfigCombo.AppendText("Debug")
            pDefaultConfigCombo.AppendText("Release")
            AddHandler pDefaultConfigCombo.Changed, AddressOf OnSettingChanged
            lDefaultConfigBox.PackStart(pDefaultConfigCombo, True, True, 0)
            lConfigBox.PackStart(lDefaultConfigBox, False, False, 0)

            Dim lDefaultPlatformBox As New Box(Orientation.Horizontal, 10)
            lDefaultPlatformBox.PackStart(New Label("Default platform:"), False, False, 0)
            pDefaultPlatformCombo = New CustomDrawComboBox()
            pDefaultPlatformCombo.ThemeManager = pThemeManager
            pDefaultPlatformCombo.AppendText("Any CPU")
            pDefaultPlatformCombo.AppendText("x86")
            pDefaultPlatformCombo.AppendText("x64")
            AddHandler pDefaultPlatformCombo.Changed, AddressOf OnSettingChanged
            lDefaultPlatformBox.PackStart(pDefaultPlatformCombo, True, True, 0)
            lConfigBox.PackStart(lDefaultPlatformBox, False, False, 0)

            Dim lVerbosityBox As New Box(Orientation.Horizontal, 10)
            lVerbosityBox.PackStart(New Label("Verbosity:"), False, False, 0)
            pVerbosityCombo = New CustomDrawComboBox()
            pVerbosityCombo.ThemeManager = pThemeManager
            pVerbosityCombo.AppendText("Quiet")
            pVerbosityCombo.AppendText("Minimal")
            pVerbosityCombo.AppendText("Normal")
            pVerbosityCombo.AppendText("Detailed")
            pVerbosityCombo.AppendText("Diagnostic")
            AddHandler pVerbosityCombo.Changed, AddressOf OnSettingChanged
            lVerbosityBox.PackStart(pVerbosityCombo, True, True, 0)
            lConfigBox.PackStart(lVerbosityBox, False, False, 0)
            
            lConfigFrame.Add(lConfigBox)
            lBox.PackStart(lConfigFrame, False, False, 0)
            
            ' Build Options
            Dim lOptionsFrame As New Frame("Build Options")
            Dim lOptionsBox As New Box(Orientation.Vertical, 5)
            lOptionsBox.BorderWidth = 10
            
            pParallelBuildCheck = New CheckButton("Enable parallel build")
            AddHandler pParallelBuildCheck.Toggled, AddressOf OnSettingChanged
            lOptionsBox.PackStart(pParallelBuildCheck, False, False, 0)
            
            pRestorePackagesCheck = New CheckButton("Restore NuGet packages before build")
            AddHandler pRestorePackagesCheck.Toggled, AddressOf OnSettingChanged
            lOptionsBox.PackStart(pRestorePackagesCheck, False, False, 0)
            
            pShowOutputCheck = New CheckButton("Show build output")
            AddHandler pShowOutputCheck.Toggled, AddressOf OnSettingChanged
            lOptionsBox.PackStart(pShowOutputCheck, False, False, 0)
            
            pClearOutputCheck = New CheckButton("Clear output before build")
            AddHandler pClearOutputCheck.Toggled, AddressOf OnSettingChanged
            lOptionsBox.PackStart(pClearOutputCheck, False, False, 0)
            
            lOptionsFrame.Add(lOptionsBox)
            lBox.PackStart(lOptionsFrame, False, False, 0)
            
            Return lBox
        End Function
        

        ''' <summary>
        ''' Creates the Git settings tab
        ''' </summary>
        Private Function CreateGitTab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.BorderWidth = 10
            
            ' Git Configuration
            Dim lConfigFrame As New Frame("Git Configuration")
            Dim lConfigBox As New Box(Orientation.Vertical, 5)
            lConfigBox.BorderWidth = 10
            
            pGitEnabledCheck = New CheckButton("Enable Git integration")
            AddHandler pGitEnabledCheck.Toggled, AddressOf OnGitEnabledToggled
            lConfigBox.PackStart(pGitEnabledCheck, False, False, 0)
            
            Dim lUserBox As New Box(Orientation.Horizontal, 10)
            lUserBox.PackStart(New Label("User name:"), False, False, 0)
            pGitUserNameEntry = New CustomDrawTextBox()
            pGitUserNameEntry.ThemeManager = pThemeManager
            pGitUserNameEntry.TooltipText = "Your name for Git commits"
            AddHandler pGitUserNameEntry.Changed, AddressOf OnSettingChanged
            lUserBox.PackStart(pGitUserNameEntry, True, True, 0)
            lConfigBox.PackStart(lUserBox, False, False, 0)
            
            Dim lEmailBox As New Box(Orientation.Horizontal, 10)
            lEmailBox.PackStart(New Label("Email:"), False, False, 0)
            pGitEmailEntry = New CustomDrawTextBox()
            pGitEmailEntry.ThemeManager = pThemeManager
            pGitEmailEntry.TooltipText = "Your email for Git commits"
            AddHandler pGitEmailEntry.Changed, AddressOf OnSettingChanged
            lEmailBox.PackStart(pGitEmailEntry, True, True, 0)
            lConfigBox.PackStart(lEmailBox, False, False, 0)
            
            Dim lBranchBox As New Box(Orientation.Horizontal, 10)
            lBranchBox.PackStart(New Label("Default branch:"), False, False, 0)
            pDefaultBranchEntry = New CustomDrawTextBox()
            pDefaultBranchEntry.ThemeManager = pThemeManager
            pDefaultBranchEntry.Text = "main"
            AddHandler pDefaultBranchEntry.Changed, AddressOf OnSettingChanged
            lBranchBox.PackStart(pDefaultBranchEntry, True, True, 0)
            lConfigBox.PackStart(lBranchBox, False, False, 0)
            
            lConfigFrame.Add(lConfigBox)
            lBox.PackStart(lConfigFrame, False, False, 0)
            
            ' Git Credentials
            Dim lCredFrame As New Frame("Git Credentials (for Push/Pull)")
            Dim lCredBox As New Box(Orientation.Vertical, 5)
            lCredBox.BorderWidth = 10
            
            ' Remote URL
            Dim lRemoteBox As New Box(Orientation.Horizontal, 10)
            lRemoteBox.PackStart(New Label("Remote URL:"), False, False, 0)
            pGitRemoteUrlEntry = New CustomDrawTextBox()
            pGitRemoteUrlEntry.ThemeManager = pThemeManager
            pGitRemoteUrlEntry.TooltipText = "Git remote URL (e.g., https://github.com/username/repo.git)"
            pGitRemoteUrlEntry.WidthRequest = 350
            AddHandler pGitRemoteUrlEntry.Changed, AddressOf OnSettingChanged
            lRemoteBox.PackStart(pGitRemoteUrlEntry, True, True, 0)
            lCredBox.PackStart(lRemoteBox, False, False, 0)
            
            ' Credential Storage Method
            Dim lStorageBox As New Box(Orientation.Horizontal, 10)
            lStorageBox.PackStart(New Label("Storage method:"), False, False, 0)
            pGitCredentialStorageCombo = New CustomDrawComboBox()
            pGitCredentialStorageCombo.ThemeManager = pThemeManager

            ' Detect and populate available storage methods
            DetectAndPopulateStorageMethods()
            
            AddHandler pGitCredentialStorageCombo.Changed, AddressOf OnGitStorageMethodChanged
            lStorageBox.PackStart(pGitCredentialStorageCombo, True, True, 0)
            lCredBox.PackStart(lStorageBox, False, False, 0)
            
            ' Credential Type
            Dim lCredTypeBox As New Box(Orientation.Horizontal, 10)
            lCredTypeBox.PackStart(New Label("Credential type:"), False, False, 0)
            pGitCredentialTypeCombo = New CustomDrawComboBox()
            pGitCredentialTypeCombo.ThemeManager = pThemeManager
            pGitCredentialTypeCombo.AppendText("None (use system)")
            pGitCredentialTypeCombo.AppendText("Personal Access Token")
            pGitCredentialTypeCombo.AppendText("OAuth Token")
            pGitCredentialTypeCombo.Active = 0
            AddHandler pGitCredentialTypeCombo.Changed, AddressOf OnGitCredentialTypeChanged
            lCredTypeBox.PackStart(pGitCredentialTypeCombo, True, True, 0)
            lCredBox.PackStart(lCredTypeBox, False, False, 0)
            
            ' Token/Password
            Dim lTokenBox As New Box(Orientation.Horizontal, 10)
            lTokenBox.PackStart(New Label("Token/Password:"), False, False, 0)
            pGitTokenEntry = New CustomDrawTextBox()
            pGitTokenEntry.ThemeManager = pThemeManager
            pGitTokenEntry.InnerEntry.Visibility = False  ' Hide password by default
            pGitTokenEntry.TooltipText = "Personal access token or OAuth token for authentication"
            pGitTokenEntry.Sensitive = False  ' Disabled by default
            AddHandler pGitTokenEntry.Changed, AddressOf OnSettingChanged
            lTokenBox.PackStart(pGitTokenEntry, True, True, 0)
            
            ' Show/Hide password checkbox
            pGitTokenVisibleCheck = New CheckButton("Show")
            pGitTokenVisibleCheck.Sensitive = False  ' Disabled by default
            AddHandler pGitTokenVisibleCheck.Toggled, AddressOf OnGitTokenVisibleToggled
            lTokenBox.PackStart(pGitTokenVisibleCheck, False, False, 0)
            
            lCredBox.PackStart(lTokenBox, False, False, 0)
            
            ' Security notice - updated to reflect actual security
            Dim lSecurityLabel As New Label()
            lSecurityLabel.UseMarkup = True
            lSecurityLabel.Xalign = 0
            lSecurityLabel.MarginTop = 5
            UpdateSecurityLabel(lSecurityLabel)
            lCredBox.PackStart(lSecurityLabel, False, False, 0)
            
            lCredFrame.Add(lCredBox)
            lBox.PackStart(lCredFrame, False, False, 0)
            
            ' Auto-fetch Settings
            Dim lFetchFrame As New Frame("Auto-fetch Settings")
            Dim lFetchBox As New Box(Orientation.Vertical, 5)
            lFetchBox.BorderWidth = 10
            
            Dim lAutoFetchBox As New Box(Orientation.Horizontal, 5)
            pAutoFetchCheck = New CheckButton("Auto-fetch every")
            AddHandler pAutoFetchCheck.Toggled, AddressOf OnAutoFetchToggled
            lAutoFetchBox.PackStart(pAutoFetchCheck, False, False, 0)
            
            pFetchIntervalSpin = New SpinButton(5, 60, 5)
            AddHandler pFetchIntervalSpin.ValueChanged, AddressOf OnSettingChanged
            lAutoFetchBox.PackStart(pFetchIntervalSpin, False, False, 0)
            lAutoFetchBox.PackStart(New Label("minutes"), False, False, 0)
            
            lFetchBox.PackStart(lAutoFetchBox, False, False, 0)
            
            lFetchFrame.Add(lFetchBox)
            lBox.PackStart(lFetchFrame, False, False, 0)
            
            Return lBox
        End Function

        ''' <summary>
        ''' Detects available credential storage methods and populates combo
        ''' </summary>
        Private Sub DetectAndPopulateStorageMethods()
            Try
                ' Get available methods
                Dim lAvailableMethods As List(Of Utilities.CredentialManager.eStorageMethod) = 
                    Utilities.CredentialManager.GetAvailableMethods()
                
                ' Create temporary manager to get method names
                Dim lTempManager As New Utilities.CredentialManager()
                
                for each lMethod in lAvailableMethods
                    lTempManager = New Utilities.CredentialManager(lMethod)
                    pGitCredentialStorageCombo.AppendText(lTempManager.GetStorageMethodName())
                Next
                
                ' Set default to first available
                If lAvailableMethods.Count > 0 Then
                    pGitCredentialStorageCombo.Active = 0
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DetectAndPopulateStorageMethods error: {ex.Message}")
                ' Fallback - add encrypted file option
                pGitCredentialStorageCombo.AppendText("Encrypted File")
                pGitCredentialStorageCombo.Active = 0
            End Try
        End Sub

        ''' <summary>
        ''' Updates the security label based on selected storage method
        ''' </summary>
        Private Sub UpdateSecurityLabel(vLabel As Label)
            Try
                Dim lText As String = ""
                
                If pGitCredentialStorageCombo IsNot Nothing AndAlso pGitCredentialStorageCombo.ActiveText IsNot Nothing Then
                    Select Case pGitCredentialStorageCombo.ActiveText
                        Case "GNOME Keyring"
                            lText = "<i>Note: Credentials are stored securely in GNOME Keyring (requires keyring password on boot).</i>"
                        Case "LibSecret"
                            lText = "<i>Note: Credentials are stored securely using LibSecret.</i>"
                        Case "KDE Wallet"
                            lText = "<i>Note: Credentials are stored securely in KDE Wallet.</i>"
                        Case "Encrypted File"
                            lText = "<i>Note: Credentials are stored in an AES-encrypted file with machine-specific key.</i>"
                        Case Else
                            lText = "<i>Note: Select a storage method to securely save credentials.</i>"
                    End Select
                Else
                    lText = "<i>Note: Select a storage method to securely save credentials.</i>"
                End If
                
                vLabel.Markup = lText
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSecurityLabel error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the AI settings tab
        ''' </summary>
        Private Function CreateAITab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.BorderWidth = 10
            
            ' AI Configuration
            Dim lConfigFrame As New Frame("AI Assistant Configuration")
            Dim lConfigBox As New Box(Orientation.Vertical, 5)
            lConfigBox.BorderWidth = 10
            
            pAIEnabledCheck = New CheckButton("Enable AI Assistant")
            AddHandler pAIEnabledCheck.Toggled, AddressOf OnAIEnabledToggled
            lConfigBox.PackStart(pAIEnabledCheck, False, False, 0)
            
            pShowArtifactsCheck = New CheckButton("Automatically show AI artifacts in tabs")
            pShowArtifactsCheck.MarginStart = 20
            AddHandler pShowArtifactsCheck.Toggled, AddressOf OnSettingChanged
            lConfigBox.PackStart(pShowArtifactsCheck, False, False, 0)
            
            pAutoContextCheck = New CheckButton("Include project context automatically")
            pAutoContextCheck.MarginStart = 20
            AddHandler pAutoContextCheck.Toggled, AddressOf OnSettingChanged
            lConfigBox.PackStart(pAutoContextCheck, False, False, 0)
            
            pMem0EnabledCheck = New CheckButton("Enable Mem0 memory system")
            pMem0EnabledCheck.MarginStart = 20
            AddHandler pMem0EnabledCheck.Toggled, AddressOf OnSettingChanged
            lConfigBox.PackStart(pMem0EnabledCheck, False, False, 0)

            lConfigFrame.Add(lConfigBox)
            lBox.PackStart(lConfigFrame, False, False, 0)

            ' API Configuration - was a separate modal AISettingsDialog opened via a
            ' "Configure AI Connection..." button; consolidated directly into this tab.
            ' Supports four backends (see AI.AIProviderFactory): Anthropic's Claude API, the
            ' locally-installed Claude Code CLI, OpenRouter, or a local LLM server (Ollama, LM
            ' Studio, etc.) - each needs a different subset of the fields below, so
            ' OnAIProviderChanged grays out whichever don't apply to the current selection
            Dim lApiFrame As New Frame("Provider Configuration")
            Dim lApiBox As New Box(Orientation.Vertical, 5)
            lApiBox.BorderWidth = 10

            Dim lProviderBox As New Box(Orientation.Horizontal, 10)
            lProviderBox.PackStart(New Label("Provider:"), False, False, 0)
            pAIProviderCombo = New CustomDrawComboBox()
            pAIProviderCombo.ThemeManager = pThemeManager
            pAIProviderCombo.AppendText("Claude API")
            pAIProviderCombo.AppendText("Claude Code CLI")
            pAIProviderCombo.AppendText("OpenRouter")
            pAIProviderCombo.AppendText("Local LLM (OpenAI-compatible)")
            pAIProviderCombo.Active = 0
            AddHandler pAIProviderCombo.Changed, AddressOf OnAIProviderChanged
            lProviderBox.PackStart(pAIProviderCombo, True, True, 0)
            lApiBox.PackStart(lProviderBox, False, False, 0)

            Dim lApiKeyBox As New Box(Orientation.Horizontal, 10)
            pApiKeyLabel = New Label("API key:")
            lApiKeyBox.PackStart(pApiKeyLabel, False, False, 0)
            pApiKeyEntry = New CustomDrawTextBox()
            pApiKeyEntry.ThemeManager = pThemeManager
            pApiKeyEntry.InnerEntry.Visibility = False ' Hide API key by default
            pApiKeyEntry.TooltipText = "Your Claude API key"
            AddHandler pApiKeyEntry.Changed, AddressOf OnSettingChanged
            lApiKeyBox.PackStart(pApiKeyEntry, True, True, 0)

            pApiKeyVisibleCheck = New CheckButton("Show")
            AddHandler pApiKeyVisibleCheck.Toggled, AddressOf OnApiKeyVisibleToggled
            lApiKeyBox.PackStart(pApiKeyVisibleCheck, False, False, 0)
            lApiBox.PackStart(lApiKeyBox, False, False, 0)

            Dim lBaseUrlBox As New Box(Orientation.Horizontal, 10)
            pAIBaseUrlLabel = New Label("Base URL:")
            lBaseUrlBox.PackStart(pAIBaseUrlLabel, False, False, 0)
            pAIBaseUrlEntry = New CustomDrawTextBox()
            pAIBaseUrlEntry.ThemeManager = pThemeManager
            pAIBaseUrlEntry.TooltipText = "OpenAI-compatible base URL, e.g. https://openrouter.ai/api/v1 or http://localhost:11434/v1"
            AddHandler pAIBaseUrlEntry.Changed, AddressOf OnSettingChanged
            lBaseUrlBox.PackStart(pAIBaseUrlEntry, True, True, 0)
            lApiBox.PackStart(lBaseUrlBox, False, False, 0)

            Dim lCliPathBox As New Box(Orientation.Horizontal, 10)
            pClaudeCodePathLabel = New Label("CLI path:")
            lCliPathBox.PackStart(pClaudeCodePathLabel, False, False, 0)
            pClaudeCodePathEntry = New CustomDrawTextBox()
            pClaudeCodePathEntry.ThemeManager = pThemeManager
            pClaudeCodePathEntry.TooltipText = "Path to the claude executable, or just ""claude"" to resolve it via PATH"
            AddHandler pClaudeCodePathEntry.Changed, AddressOf OnSettingChanged
            lCliPathBox.PackStart(pClaudeCodePathEntry, True, True, 0)
            lApiBox.PackStart(lCliPathBox, False, False, 0)

            Dim lModelBox As New Box(Orientation.Horizontal, 10)
            lModelBox.PackStart(New Label("Model:"), False, False, 0)
            pAIModelEntry = New CustomDrawTextBox()
            pAIModelEntry.ThemeManager = pThemeManager
            AddHandler pAIModelEntry.Changed, AddressOf OnSettingChanged
            lModelBox.PackStart(pAIModelEntry, True, True, 0)
            lApiBox.PackStart(lModelBox, False, False, 0)

            Dim lApiHelpLabel As New Label()
            lApiHelpLabel.UseMarkup = True
            lApiHelpLabel.Xalign = 0
            lApiBox.PackStart(lApiHelpLabel, False, False, 0)
            UpdateAIProviderHelpLabel(lApiHelpLabel)
            pAIProviderHelpLabel = lApiHelpLabel

            lApiFrame.Add(lApiBox)
            lBox.PackStart(lApiFrame, False, False, 0)

            ' Generation Settings
            Dim lGenFrame As New Frame("Generation Settings")
            Dim lGenBox As New Box(Orientation.Vertical, 5)
            lGenBox.BorderWidth = 10

            Dim lMaxTokensBox As New Box(Orientation.Horizontal, 10)
            lMaxTokensBox.PackStart(New Label("Max tokens:"), False, False, 0)
            pMaxTokensSpin = New SpinButton(100, 100000, 100)
            AddHandler pMaxTokensSpin.ValueChanged, AddressOf OnSettingChanged
            lMaxTokensBox.PackStart(pMaxTokensSpin, False, False, 0)
            Dim lMaxTokensHelp As New Label("<small>Maximum response length</small>")
            lMaxTokensHelp.UseMarkup = True
            lMaxTokensHelp.Xalign = 0
            lMaxTokensBox.PackStart(lMaxTokensHelp, True, True, 0)
            lGenBox.PackStart(lMaxTokensBox, False, False, 0)

            Dim lTemperatureBox As New Box(Orientation.Horizontal, 10)
            lTemperatureBox.PackStart(New Label("Temperature:"), False, False, 0)
            pTemperatureSpin = New SpinButton(0.0, 1.0, 0.1)
            pTemperatureSpin.Digits = 1
            AddHandler pTemperatureSpin.ValueChanged, AddressOf OnSettingChanged
            lTemperatureBox.PackStart(pTemperatureSpin, False, False, 0)
            Dim lTemperatureHelp As New Label("<small>0.0 = focused, 1.0 = creative</small>")
            lTemperatureHelp.UseMarkup = True
            lTemperatureHelp.Xalign = 0
            lTemperatureBox.PackStart(lTemperatureHelp, True, True, 0)
            lGenBox.PackStart(lTemperatureBox, False, False, 0)

            pStreamResponsesCheck = New CheckButton("Stream responses (show text as it's generated)")
            AddHandler pStreamResponsesCheck.Toggled, AddressOf OnSettingChanged
            lGenBox.PackStart(pStreamResponsesCheck, False, False, 0)

            lGenFrame.Add(lGenBox)
            lBox.PackStart(lGenFrame, False, False, 0)

            ' Conversation Features
            Dim lConvFrame As New Frame("Conversation Features")
            Dim lConvBox As New Box(Orientation.Vertical, 5)
            lConvBox.BorderWidth = 10

            pAutoSuggestCheck = New CheckButton("Enable auto-suggestions while typing")
            AddHandler pAutoSuggestCheck.Toggled, AddressOf OnSettingChanged
            lConvBox.PackStart(pAutoSuggestCheck, False, False, 0)

            pSaveHistoryCheck = New CheckButton("Save conversation history")
            AddHandler pSaveHistoryCheck.Toggled, AddressOf OnSaveHistoryToggled
            lConvBox.PackStart(pSaveHistoryCheck, False, False, 0)

            Dim lHistoryBox As New Box(Orientation.Horizontal, 10)
            lHistoryBox.PackStart(New Label("History limit:"), False, False, 0)
            pHistoryLimitSpin = New SpinButton(0, 100, 1)
            AddHandler pHistoryLimitSpin.ValueChanged, AddressOf OnSettingChanged
            lHistoryBox.PackStart(pHistoryLimitSpin, False, False, 0)
            Dim lHistoryHelp As New Label("<small>conversations (0 = unlimited)</small>")
            lHistoryHelp.UseMarkup = True
            lHistoryHelp.Xalign = 0
            lHistoryBox.PackStart(lHistoryHelp, True, True, 0)
            lConvBox.PackStart(lHistoryBox, False, False, 0)

            lConvFrame.Add(lConvBox)
            lBox.PackStart(lConvFrame, False, False, 0)

            ' AI Features Info
            Dim lInfoFrame As New Frame("AI Features")
            Dim lInfoBox As New Box(Orientation.Vertical, 5)
            lInfoBox.BorderWidth = 10
            
            Dim lFeatures() As String = {
                "• Get intelligent code suggestions",
                "• Explain and refactor existing code",
                "• Generate documentation and comments",
                "• Create unit tests automatically",
                "• Remember your coding patterns with Mem0"
            }
            
            for each lFeature in lFeatures
                Dim lLabel As New Label(lFeature)
                lLabel.Xalign = 0
                lInfoBox.PackStart(lLabel, False, False, 0)
            Next
            
            lInfoFrame.Add(lInfoBox)
            lBox.PackStart(lInfoFrame, True, True, 0)
            
            Return lBox
        End Function
        
        ''' <summary>
        ''' Creates the Advanced settings tab
        ''' </summary>
        Private Function CreateAdvancedTab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.BorderWidth = 10
            
            ' Logging Settings
            Dim lLoggingFrame As New Frame("Logging Settings")
            Dim lLoggingBox As New Box(Orientation.Vertical, 5)
            lLoggingBox.BorderWidth = 10
            
            pEnableLoggingCheck = New CheckButton("Enable logging")
            AddHandler pEnableLoggingCheck.Toggled, AddressOf OnEnableLoggingToggled
            lLoggingBox.PackStart(pEnableLoggingCheck, False, False, 0)
            
            Dim lLogLevelBox As New Box(Orientation.Horizontal, 10)
            lLogLevelBox.PackStart(New Label("Log level:"), False, False, 0)
            pLogLevelCombo = New CustomDrawComboBox()
            pLogLevelCombo.ThemeManager = pThemeManager
            pLogLevelCombo.AppendText("Error")
            pLogLevelCombo.AppendText("Warning")
            pLogLevelCombo.AppendText("Info")
            pLogLevelCombo.AppendText("Debug")
            pLogLevelCombo.AppendText("Verbose")
            AddHandler pLogLevelCombo.Changed, AddressOf OnSettingChanged
            lLogLevelBox.PackStart(pLogLevelCombo, True, True, 0)
            lLoggingBox.PackStart(lLogLevelBox, False, False, 0)
            
            Dim lMaxLogSizeBox As New Box(Orientation.Horizontal, 10)
            lMaxLogSizeBox.PackStart(New Label("Max log size (MB):"), False, False, 0)
            pMaxLogSizeSpin = New SpinButton(1, 100, 1)
            AddHandler pMaxLogSizeSpin.ValueChanged, AddressOf OnSettingChanged
            lMaxLogSizeBox.PackStart(pMaxLogSizeSpin, False, False, 0)
            lLoggingBox.PackStart(lMaxLogSizeBox, False, False, 0)
            
            lLoggingFrame.Add(lLoggingBox)
            lBox.PackStart(lLoggingFrame, False, False, 0)
            
            ' Update Settings
            Dim lUpdateFrame As New Frame("Update Settings")
            Dim lUpdateBox As New Box(Orientation.Vertical, 5)
            lUpdateBox.BorderWidth = 10
            
            pCheckUpdatesCheck = New CheckButton("Check for updates automatically")
            AddHandler pCheckUpdatesCheck.Toggled, AddressOf OnSettingChanged
            lUpdateBox.PackStart(pCheckUpdatesCheck, False, False, 0)
            
            pBetaUpdatesCheck = New CheckButton("Include beta versions")
            pBetaUpdatesCheck.MarginStart = 20
            AddHandler pBetaUpdatesCheck.Toggled, AddressOf OnSettingChanged
            lUpdateBox.PackStart(pBetaUpdatesCheck, False, False, 0)
            
            lUpdateFrame.Add(lUpdateBox)
            lBox.PackStart(lUpdateFrame, False, False, 0)
            
            ' Privacy Settings
            Dim lPrivacyFrame As New Frame("Privacy Settings")
            Dim lPrivacyBox As New Box(Orientation.Vertical, 5)
            lPrivacyBox.BorderWidth = 10
            
            pEnableTelemetryCheck = New CheckButton("Send anonymous usage statistics")
            AddHandler pEnableTelemetryCheck.Toggled, AddressOf OnSettingChanged
            lPrivacyBox.PackStart(pEnableTelemetryCheck, False, False, 0)
            
            lPrivacyFrame.Add(lPrivacyBox)
            lBox.PackStart(lPrivacyFrame, False, False, 0)

            Return lBox
        End Function

        ''' <summary>
        ''' Builds the tab for managing which directory names are skipped when SimpleIDE
        ''' scans a project for source files
        ''' </summary>
        Private Function CreateExcludedDirectoriesTab() As Widget
            Dim lBox As New Box(Orientation.Vertical, 10)
            lBox.BorderWidth = 10

            ' Built-in defaults - not editable here, just explained
            Dim lDefaultsFrame As New Frame("Always Excluded (built-in)")
            Dim lDefaultsBox As New Box(Orientation.Vertical, 5)
            lDefaultsBox.BorderWidth = 10

            Dim lDefaultsExplain As New Label(
                "These directories are always skipped whenever SimpleIDE scans a project " &
                "for source files - the TODO list, Find/Replace-in-files, and the AI " &
                "context bridge all share this same scan. They hold version control " &
                "internals, compiled build output, or IDE/tooling data rather than your " &
                "own source, so walking into them only produces duplicate or irrelevant " &
                "results (a nested git worktree under .claude, for instance, can make " &
                "every file appear to exist twice, possibly at a different, stale commit).")
            lDefaultsExplain.Wrap = True
            lDefaultsExplain.Xalign = 0
            lDefaultsBox.PackStart(lDefaultsExplain, False, False, 0)

            Dim lDefaultsList As New Label(String.Join(", ", ProjectFileScanner.DefaultExcludedDirectories))
            lDefaultsList.Wrap = True
            lDefaultsList.Xalign = 0
            lDefaultsList.MarginTop = 5
            lDefaultsList.OverrideFont(Pango.FontDescription.FromString("Monospace 10"))
            lDefaultsBox.PackStart(lDefaultsList, False, False, 0)

            lDefaultsFrame.Add(lDefaultsBox)
            lBox.PackStart(lDefaultsFrame, False, False, 0)

            ' User-configured additions
            Dim lCustomFrame As New Frame("Additional Excluded Directories")
            Dim lCustomBox As New Box(Orientation.Vertical, 5)
            lCustomBox.BorderWidth = 10

            Dim lCustomExplain As New Label(
                "Add directory names (not full paths) here to also skip them during " &
                "project scans - for example a vendor folder, a generated-code " &
                "directory, or another tool's cache directory specific to your own " &
                "projects. Changes below apply immediately, independent of this " &
                "window's Save/Apply buttons.")
            lCustomExplain.Wrap = True
            lCustomExplain.Xalign = 0
            lCustomBox.PackStart(lCustomExplain, False, False, 0)

            Dim lAddRow As New Box(Orientation.Horizontal, 5)
            lAddRow.MarginTop = 5

            pExcludedDirEntry = New CustomDrawTextBox("Directory name, e.g. ThirdParty")
            pExcludedDirEntry.ThemeManager = pThemeManager
            AddHandler pExcludedDirEntry.Activated, AddressOf OnAddExcludedDirectory
            lAddRow.PackStart(pExcludedDirEntry, True, True, 0)

            pAddExcludedDirButton = New CustomDrawButton("Add")
            pAddExcludedDirButton.ThemeManager = pThemeManager
            AddHandler pAddExcludedDirButton.Clicked, AddressOf OnAddExcludedDirectory
            lAddRow.PackStart(pAddExcludedDirButton, False, False, 0)

            lCustomBox.PackStart(lAddRow, False, False, 0)

            pExcludedDirListBox = New CustomDrawListBox()
            pExcludedDirListBox.ThemeManager = pThemeManager
            pExcludedDirListBox.SetSizeRequest(-1, 150)
            lCustomBox.PackStart(pExcludedDirListBox, True, True, 5)

            pRemoveExcludedDirButton = New CustomDrawButton("Remove Selected")
            pRemoveExcludedDirButton.ThemeManager = pThemeManager
            AddHandler pRemoveExcludedDirButton.Clicked, AddressOf OnRemoveExcludedDirectory
            lCustomBox.PackStart(pRemoveExcludedDirButton, False, False, 0)

            lCustomFrame.Add(lCustomBox)
            lBox.PackStart(lCustomFrame, True, True, 0)

            RefreshExcludedDirectoriesList()

            Return lBox
        End Function

        ''' <summary>
        ''' Repopulates the custom-excluded-directories list from ProjectFileScanner's
        ''' current in-memory state
        ''' </summary>
        Private Sub RefreshExcludedDirectoriesList()
            Try
                If pExcludedDirListBox Is Nothing Then Return
                pExcludedDirListBox.Clear()
                For Each lDirectoryName In ProjectFileScanner.CustomExcludedDirectories.OrderBy(Function(d) d, StringComparer.OrdinalIgnoreCase)
                    pExcludedDirListBox.AddItem(lDirectoryName)
                Next
            Catch ex As Exception
                Console.WriteLine($"RefreshExcludedDirectoriesList error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnAddExcludedDirectory(vSender As Object, vArgs As EventArgs)
            Try
                Dim lName As String = pExcludedDirEntry.Text.Trim()
                If String.IsNullOrEmpty(lName) Then Return
                ProjectFileScanner.AddExcludedDirectory(pSettingsManager, lName)
                pExcludedDirEntry.Text = ""
                RefreshExcludedDirectoriesList()
            Catch ex As Exception
                Console.WriteLine($"OnAddExcludedDirectory error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnRemoveExcludedDirectory(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelected As ListBoxItem = pExcludedDirListBox.SelectedItem
                If lSelected Is Nothing Then Return
                ProjectFileScanner.RemoveExcludedDirectory(pSettingsManager, lSelected.Text)
                RefreshExcludedDirectoriesList()
            Catch ex As Exception
                Console.WriteLine($"OnRemoveExcludedDirectory error: {ex.Message}")
            End Try
        End Sub

        ' ===== Load/Save Settings =====
        
        ''' <summary>
        ''' Loads settings from the settings manager
        ''' </summary>
        Private Sub LoadSettings()
            Try
                ' General
                pShowSplashCheck.Active = pSettingsManager.GetBoolean("General.ShowSplash", True)
                pRestoreLayoutCheck.Active = pSettingsManager.GetBoolean("General.RestoreLayout", True)
                pAutoSaveCheck.Active = pSettingsManager.GetBoolean("General.AutoSave", False)
                pAutoSaveIntervalSpin.Value = pSettingsManager.GetInteger("General.AutoSaveInterval", 10)
                pRecentFilesCountSpin.Value = pSettingsManager.GetInteger("General.RecentFilesCount", 10)
                pPreferWebKitCheck.Active = pSettingsManager.GetBoolean("General.PreferWebKitRendering", True)

                ' Editor
                pFontButton.Font = pSettingsManager.EditorFont
                pTabWidthSpin.Value = pSettingsManager.TabWidth
                pUndoHistorySizeSpin.Value = pSettingsManager.UndoHistorySize
                pUseTabsRadio.Active = pSettingsManager.UseTabs
                pUseSpacesRadio.Active = Not pSettingsManager.UseTabs
                pShowLineNumbersCheck.Active = pSettingsManager.ShowLineNumbers
                pHighlightCurrentLineCheck.Active = pSettingsManager.HighlightCurrentLine
                pWordWrapCheck.Active = pSettingsManager.WordWrap
                pAutoIndentCheck.Active = pSettingsManager.AutoIndent
                pShowWhitespaceCheck.Active = pSettingsManager.ShowWhitespace
                pShowEndOfLineCheck.Active = pSettingsManager.GetBoolean("Editor.ShowEndOfLine", False)
                
                ' Build - Configuration/Platform read from the same typed settings the toolbar's
                ' configuration dropdown and the Configure Build dialog already use (previously
                ' this combo read/wrote separate "Build.Default*" keys that nothing else ever
                ' read, so it had no effect regardless of what was selected here)
                pDefaultConfigCombo.Active = If(pSettingsManager.BuildConfiguration = "Release", 1, 0)

                Dim lPlatform As String = pSettingsManager.BuildPlatform
                Select Case lPlatform.ToLower()
                    Case "x86"
                        pDefaultPlatformCombo.Active = 1
                    Case "x64"
                        pDefaultPlatformCombo.Active = 2
                    Case Else
                        pDefaultPlatformCombo.Active = 0
                End Select

                Dim lVerbosity As String = pSettingsManager.GetString("Build.Verbosity", "Normal")
                Select Case lVerbosity
                    Case "Quiet"
                        pVerbosityCombo.Active = 0
                    Case "Minimal"
                        pVerbosityCombo.Active = 1
                    Case "Detailed"
                        pVerbosityCombo.Active = 3
                    Case "Diagnostic"
                        pVerbosityCombo.Active = 4
                    Case Else
                        pVerbosityCombo.Active = 2  ' Normal
                End Select

                pParallelBuildCheck.Active = pSettingsManager.GetBoolean("Build.ParallelBuild", True)
                pRestorePackagesCheck.Active = pSettingsManager.GetBoolean("Build.RestorePackages", True)

                ' Show/Clear output bind to the same typed settings MainWindow.Build.vb/
                ' MainWindow.Run.vb already read (ShowBuildOutput/ClearOutputOnBuild) -
                ' previously this pair wrote separate "Build.Show/ClearOutput" keys nobody read,
                ' while the real settings that actually governed this behavior had no UI at all
                pShowOutputCheck.Active = pSettingsManager.ShowBuildOutput
                pClearOutputCheck.Active = pSettingsManager.ClearOutputOnBuild
                
                ' Git
                pGitEnabledCheck.Active = pSettingsManager.GetBoolean("Git.Enabled", False)
                pGitUserNameEntry.Text = pSettingsManager.GetString("Git.UserName", "")
                pGitEmailEntry.Text = pSettingsManager.GetString("Git.Email", "")
                pDefaultBranchEntry.Text = pSettingsManager.GetString("Git.DefaultBranch", "main")
                pAutoFetchCheck.Active = pSettingsManager.GetBoolean("Git.AutoFetch", False)
                pFetchIntervalSpin.Value = pSettingsManager.GetInteger("Git.FetchInterval", 10)
                pGitRemoteUrlEntry.Text = pSettingsManager.GetString("Git.RemoteUrl", "")
                
                ' Git Credentials
                Dim lCredentialType As String = pSettingsManager.GetString("Git.CredentialType", "None")
                Select Case lCredentialType
                    Case "PAT"
                        pGitCredentialTypeCombo.Active = 1
                    Case "OAuth"
                        pGitCredentialTypeCombo.Active = 2
                    Case Else
                        pGitCredentialTypeCombo.Active = 0
                End Select
                
                ' Token is stored securely via CredentialManager (OS keyring, or the encrypted-
                ' file fallback) under the selected storage method above - never in plain settings
                If pCredentialManager IsNot Nothing Then
                    pGitTokenEntry.Text = pCredentialManager.RetrieveCredential("SimpleIDE-Git", "token")
                End If
                
                ' AI
                pAIEnabledCheck.Active = pSettingsManager.GetBoolean("AI.Enabled", False)
                pShowArtifactsCheck.Active = pSettingsManager.GetBoolean("AI.ShowArtifacts", True)
                pAutoContextCheck.Active = pSettingsManager.GetBoolean("AI.AutoContext", False)
                pMem0EnabledCheck.Active = pSettingsManager.GetBoolean("AI.Mem0.Enabled", False)

                Dim lProviderName As String = pSettingsManager.GetString("AI.Provider", "ClaudeAPI")
                Select Case lProviderName
                    Case "ClaudeCodeCLI"
                        pAIProviderCombo.Active = 1
                    Case "OpenRouter"
                        pAIProviderCombo.Active = 2
                    Case "LocalLLM"
                        pAIProviderCombo.Active = 3
                    Case Else
                        pAIProviderCombo.Active = 0
                End Select

                ' API keys are stored securely via CredentialManager (OS keyring, or the
                ' encrypted-file fallback), one per provider account so switching providers
                ' doesn't require re-entering a key already saved for another - never in plain
                ' settings, which is where AI.ApiKey used to live
                If pCredentialManager IsNot Nothing Then
                    Dim lKeyAccount As String = If(pAIProviderCombo.Active = 2, "OpenRouter", "ClaudeAPI")
                    pApiKeyEntry.Text = pCredentialManager.RetrieveCredential("SimpleIDE-AI", lKeyAccount)
                End If

                pAIBaseUrlEntry.Text = pSettingsManager.GetString("AI.BaseUrl", "")
                pClaudeCodePathEntry.Text = pSettingsManager.GetString("AI.ClaudeCodePath", "claude")
                pAIModelEntry.Text = pSettingsManager.GetString("AI.Model", "")

                pMaxTokensSpin.Value = pSettingsManager.GetInteger("AI.MaxTokens", 4096)
                pTemperatureSpin.Value = pSettingsManager.GetDouble("AI.Temperature", 0.7)
                pStreamResponsesCheck.Active = pSettingsManager.GetBoolean("AI.StreamResponses", True)
                pAutoSuggestCheck.Active = pSettingsManager.GetBoolean("AI.AutoSuggest", False)
                pSaveHistoryCheck.Active = pSettingsManager.GetBoolean("AI.SaveHistory", True)
                pHistoryLimitSpin.Value = pSettingsManager.GetInteger("AI.HistoryLimit", 20)

                ' Update UI sensitivity state to match loaded values
                Dim lAIEnabled As Boolean = pAIEnabledCheck.Active
                pShowArtifactsCheck.Sensitive = lAIEnabled
                pAutoContextCheck.Sensitive = lAIEnabled
                pMem0EnabledCheck.Sensitive = lAIEnabled
                pAIProviderCombo.Sensitive = lAIEnabled
                pAIModelEntry.Sensitive = lAIEnabled
                pMaxTokensSpin.Sensitive = lAIEnabled
                pTemperatureSpin.Sensitive = lAIEnabled
                pStreamResponsesCheck.Sensitive = lAIEnabled
                pAutoSuggestCheck.Sensitive = lAIEnabled
                pSaveHistoryCheck.Sensitive = lAIEnabled
                pHistoryLimitSpin.Sensitive = lAIEnabled AndAlso pSaveHistoryCheck.Active

                ' Advanced
                pEnableLoggingCheck.Active = pSettingsManager.GetBoolean("Advanced.EnableLogging", False)
                
                Dim lLogLevel As String = pSettingsManager.GetString("Advanced.LogLevel", "Info")
                Select Case lLogLevel
                    Case "Error"
                        pLogLevelCombo.Active = 0
                    Case "Warning"
                        pLogLevelCombo.Active = 1
                    Case "Debug"
                        pLogLevelCombo.Active = 3
                    Case "Verbose"
                        pLogLevelCombo.Active = 4
                    Case Else
                        pLogLevelCombo.Active = 2  ' Info
                End Select
                
                pMaxLogSizeSpin.Value = pSettingsManager.GetInteger("Advanced.MaxLogSize", 10)
                pCheckUpdatesCheck.Active = pSettingsManager.GetBoolean("Advanced.CheckUpdates", True)
                pBetaUpdatesCheck.Active = pSettingsManager.GetBoolean("Advanced.BetaUpdates", False)
                pEnableTelemetryCheck.Active = pSettingsManager.GetBoolean("Advanced.EnableTelemetry", False)
        
                ' Version settings (simplified - just the checkbox)
                pAutoIncrementCheck.Active = pSettingsManager.AutoIncrementVersion
                
                ' Update UI states
                OnAutoSaveToggled(Nothing, Nothing)
                OnGitEnabledToggled(Nothing, Nothing)
                OnAutoFetchToggled(Nothing, Nothing)
                OnAIEnabledToggled(Nothing, Nothing)
                OnEnableLoggingToggled(Nothing, Nothing)
                
                ' Reset modified flag after loading
                IsModified = False
                
            Catch ex As Exception
                Console.WriteLine($"PreferencesTab.LoadSettings error: {ex.Message}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Widgets.PreferencesTab.SaveSettings
        ''' <summary>
        ''' Saves settings to the settings manager
        ''' </summary>
        Private Sub SaveSettings()
            Try
                ' General
                pSettingsManager.SetBoolean("General.ShowSplash", pShowSplashCheck.Active)
                pSettingsManager.SetBoolean("General.RestoreLayout", pRestoreLayoutCheck.Active)
                pSettingsManager.SetBoolean("General.AutoSave", pAutoSaveCheck.Active)
                pSettingsManager.SetInteger("General.AutoSaveInterval", CInt(pAutoSaveIntervalSpin.Value))
                pSettingsManager.SetInteger("General.RecentFilesCount", CInt(pRecentFilesCountSpin.Value))
                pSettingsManager.SetBoolean("General.PreferWebKitRendering", pPreferWebKitCheck.Active)
                
                ' Editor
                pSettingsManager.EditorFont = pFontButton.Font
                pSettingsManager.TabWidth = CInt(pTabWidthSpin.Value)
                pSettingsManager.UndoHistorySize = CInt(pUndoHistorySizeSpin.Value)
                pSettingsManager.UseTabs = pUseTabsRadio.Active
                pSettingsManager.ShowLineNumbers = pShowLineNumbersCheck.Active
                pSettingsManager.HighlightCurrentLine = pHighlightCurrentLineCheck.Active
                pSettingsManager.WordWrap = pWordWrapCheck.Active
                pSettingsManager.AutoIndent = pAutoIndentCheck.Active
                pSettingsManager.ShowWhitespace = pShowWhitespaceCheck.Active
                pSettingsManager.SetBoolean("Editor.ShowEndOfLine", pShowEndOfLineCheck.Active)
                
                ' Build - Configuration/Platform and Show/Clear output write the same typed
                ' settings the rest of the IDE already reads (see LoadSettings' remarks above)
                pSettingsManager.BuildConfiguration = If(pDefaultConfigCombo.Active = 1, "Release", "Debug")
                pSettingsManager.BuildPlatform = pDefaultPlatformCombo.ActiveText
                pSettingsManager.SetString("Build.Verbosity", pVerbosityCombo.ActiveText)
                pSettingsManager.SetBoolean("Build.ParallelBuild", pParallelBuildCheck.Active)
                pSettingsManager.SetBoolean("Build.RestorePackages", pRestorePackagesCheck.Active)
                pSettingsManager.ShowBuildOutput = pShowOutputCheck.Active
                pSettingsManager.ClearOutputOnBuild = pClearOutputCheck.Active
                
                ' Git
                pSettingsManager.SetBoolean("Git.Enabled", pGitEnabledCheck.Active)
                pSettingsManager.SetString("Git.UserName", pGitUserNameEntry.Text)
                pSettingsManager.SetString("Git.Email", pGitEmailEntry.Text)
                pSettingsManager.SetString("Git.DefaultBranch", pDefaultBranchEntry.Text)
                pSettingsManager.SetBoolean("Git.AutoFetch", pAutoFetchCheck.Active)
                pSettingsManager.SetInteger("Git.FetchInterval", CInt(pFetchIntervalSpin.Value))
                
                ' Git Credentials
                pSettingsManager.SetString("Git.RemoteUrl", pGitRemoteUrlEntry.Text)
                
                ' Save credential type
                Select Case pGitCredentialTypeCombo.Active
                    Case 0
                        pSettingsManager.SetString("Git.CredentialType", "None")
                    Case 1
                        pSettingsManager.SetString("Git.CredentialType", "PAT")
                    Case 2
                        pSettingsManager.SetString("Git.CredentialType", "OAuth")
                End Select
                
                ' Store the token securely via CredentialManager (OS keyring, or the encrypted-
                ' file fallback) under the selected storage method, rather than in plain settings
                If pCredentialManager IsNot Nothing Then
                    If Not String.IsNullOrEmpty(pGitTokenEntry.Text) AndAlso pGitCredentialTypeCombo.Active > 0 Then
                        pCredentialManager.StoreCredential("SimpleIDE-Git", "token", pGitTokenEntry.Text)
                    Else
                        pCredentialManager.DeleteCredential("SimpleIDE-Git", "token")
                    End If
                End If
                
                ' AI
                pSettingsManager.SetBoolean("AI.Enabled", pAIEnabledCheck.Active)
                pSettingsManager.SetBoolean("AI.ShowArtifacts", pShowArtifactsCheck.Active)
                pSettingsManager.SetBoolean("AI.AutoContext", pAutoContextCheck.Active)
                pSettingsManager.SetBoolean("AI.Mem0.Enabled", pMem0EnabledCheck.Active)

                Dim lProviderName As String
                Select Case pAIProviderCombo.Active
                    Case 1
                        lProviderName = "ClaudeCodeCLI"
                    Case 2
                        lProviderName = "OpenRouter"
                    Case 3
                        lProviderName = "LocalLLM"
                    Case Else
                        lProviderName = "ClaudeAPI"
                End Select
                pSettingsManager.SetString("AI.Provider", lProviderName)

                ' Store the key securely via CredentialManager under the account for whichever
                ' provider is currently selected - other providers' previously-saved keys are
                ' left untouched, so switching providers doesn't lose them
                If pCredentialManager IsNot Nothing AndAlso (pAIProviderCombo.Active = 0 OrElse pAIProviderCombo.Active = 2) Then
                    Dim lKeyAccount As String = If(pAIProviderCombo.Active = 2, "OpenRouter", "ClaudeAPI")
                    Dim lApiKey As String = pApiKeyEntry.Text.Trim()
                    If Not String.IsNullOrEmpty(lApiKey) Then
                        pCredentialManager.StoreCredential("SimpleIDE-AI", lKeyAccount, lApiKey)
                    Else
                        pCredentialManager.DeleteCredential("SimpleIDE-AI", lKeyAccount)
                    End If
                End If

                ' Base URL - fall back to a sensible per-provider default if left blank, so a
                ' blank field doesn't get persisted as an explicit empty override that would
                ' otherwise shadow AIProviderFactory's own fallback default forever
                Dim lBaseUrl As String = pAIBaseUrlEntry.Text.Trim()
                If String.IsNullOrEmpty(lBaseUrl) Then
                    lBaseUrl = If(pAIProviderCombo.Active = 3, "http://localhost:11434/v1", "https://openrouter.ai/api/v1")
                End If
                pSettingsManager.SetString("AI.BaseUrl", lBaseUrl)

                pSettingsManager.SetString("AI.ClaudeCodePath", If(String.IsNullOrWhiteSpace(pClaudeCodePathEntry.Text), "claude", pClaudeCodePathEntry.Text.Trim()))
                pSettingsManager.SetString("AI.Model", pAIModelEntry.Text.Trim())

                pSettingsManager.SetInteger("AI.MaxTokens", CInt(pMaxTokensSpin.Value))
                pSettingsManager.SetDouble("AI.Temperature", pTemperatureSpin.Value)
                pSettingsManager.SetBoolean("AI.StreamResponses", pStreamResponsesCheck.Active)
                pSettingsManager.SetBoolean("AI.AutoSuggest", pAutoSuggestCheck.Active)
                pSettingsManager.SetBoolean("AI.SaveHistory", pSaveHistoryCheck.Active)
                pSettingsManager.SetInteger("AI.HistoryLimit", CInt(pHistoryLimitSpin.Value))

                ' Advanced
                pSettingsManager.SetBoolean("Advanced.EnableLogging", pEnableLoggingCheck.Active)
                pSettingsManager.SetString("Advanced.LogLevel", pLogLevelCombo.ActiveText)
                pSettingsManager.SetInteger("Advanced.MaxLogSize", CInt(pMaxLogSizeSpin.Value))
                pSettingsManager.SetBoolean("Advanced.CheckUpdates", pCheckUpdatesCheck.Active)
                pSettingsManager.SetBoolean("Advanced.BetaUpdates", pBetaUpdatesCheck.Active)
                pSettingsManager.SetBoolean("Advanced.EnableTelemetry", pEnableTelemetryCheck.Active)
                
                ' Version settings (simplified - just save the checkbox state)
                pSettingsManager.SetBoolean("AutoIncrementVersion", pAutoIncrementCheck.Active)
                
                ' Save to disk
                pSettingsManager.Save()

                RaiseEvent SettingsApplied()

            Catch ex As Exception
                Console.WriteLine($"PreferencesTab.SaveSettings error: {ex.Message}")
                Throw
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handles any setting change to mark as modified
        ''' </summary>
        Private Sub OnSettingChanged(vSender As Object, vArgs As EventArgs)
            IsModified = True
        End Sub
        
        ''' <summary>
        ''' Handles the Save button click
        ''' </summary>
        Private Sub OnSaveClicked(vSender As Object, vArgs As EventArgs)
            Try
                SaveSettings()
                IsModified = False
                
                ' Show confirmation - FIX: Use GetParentWindow() instead of Me
                Dim lDialog As New MessageDialog(GetParentWindow(), DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "Settings saved successfully.")
                lDialog.Run()
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnSaveClicked error: {ex.Message}")
                ' FIX: Use GetParentWindow() instead of Me
                Dim lDialog As New MessageDialog(GetParentWindow(), DialogFlags.Modal, MessageType.error, ButtonsType.Ok, "Failed to save settings: " & ex.Message)
                lDialog.Run()
                lDialog.Destroy()
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the Apply button click
        ''' </summary>
        Private Sub OnApplyClicked(vSender As Object, vArgs As EventArgs)
            Try
                SaveSettings()
                ' Don't reset IsModified - user might want to continue editing
                
            Catch ex As Exception
                Console.WriteLine($"OnApplyClicked error: {ex.Message}")
                ' FIX: Use GetParentWindow() instead of Me
                Dim lDialog As New MessageDialog(GetParentWindow(), DialogFlags.Modal, MessageType.error, ButtonsType.Ok, "Failed to apply settings: " & ex.Message)
                lDialog.Run()
                lDialog.Destroy()
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles auto-save checkbox toggle
        ''' </summary>
        Private Sub OnAutoSaveToggled(vSender As Object, vArgs As EventArgs)
            pAutoSaveIntervalSpin.Sensitive = pAutoSaveCheck.Active
            OnSettingChanged(vSender, vArgs)
        End Sub
        
        ''' <summary>
        ''' Handles Git enabled checkbox toggle
        ''' </summary>
        Private Sub OnGitEnabledToggled(vSender As Object, vArgs As EventArgs)
            Dim lEnabled As Boolean = pGitEnabledCheck.Active
            pGitUserNameEntry.Sensitive = lEnabled
            pGitEmailEntry.Sensitive = lEnabled
            pDefaultBranchEntry.Sensitive = lEnabled
            pAutoFetchCheck.Sensitive = lEnabled
            pFetchIntervalSpin.Sensitive = lEnabled AndAlso pAutoFetchCheck.Active
            pGitRemoteUrlEntry.Sensitive = lEnabled
            pGitCredentialTypeCombo.Sensitive = lEnabled
            pGitTokenEntry.Sensitive = lEnabled AndAlso pGitCredentialTypeCombo.Active > 0
            pGitTokenVisibleCheck.Sensitive = lEnabled AndAlso pGitCredentialTypeCombo.Active > 0
            OnSettingChanged(vSender, vArgs)
        End Sub
        
        ''' <summary>
        ''' Handles Git credential type change
        ''' </summary>
        Private Sub OnGitCredentialTypeChanged(vSender As Object, vArgs As EventArgs)
            Dim lUseCredentials As Boolean = pGitCredentialTypeCombo.Active > 0
            pGitTokenEntry.Sensitive = lUseCredentials AndAlso pGitEnabledCheck.Active
            pGitTokenVisibleCheck.Sensitive = lUseCredentials AndAlso pGitEnabledCheck.Active
            
            ' Update placeholder text based on type
            Select Case pGitCredentialTypeCombo.Active
                Case 1  ' Personal Access Token
                    pGitTokenEntry.PlaceholderText = "Enter your personal access token"
                Case 2  ' OAuth Token
                    pGitTokenEntry.PlaceholderText = "Enter your OAuth token"
                Case Else
                    pGitTokenEntry.PlaceholderText = ""
            End Select
            
            OnSettingChanged(vSender, vArgs)
        End Sub
        
        ''' <summary>
        ''' Handles Git token visibility toggle
        ''' </summary>
        Private Sub OnGitTokenVisibleToggled(vSender As Object, vArgs As EventArgs)
            pGitTokenEntry.InnerEntry.Visibility = pGitTokenVisibleCheck.Active
        End Sub
        
        ''' <summary>
        ''' Handles auto-fetch checkbox toggle
        ''' </summary>
        Private Sub OnAutoFetchToggled(vSender As Object, vArgs As EventArgs)
            pFetchIntervalSpin.Sensitive = pAutoFetchCheck.Active AndAlso pGitEnabledCheck.Active
            OnSettingChanged(vSender, vArgs)
        End Sub
        
        ''' <summary>
        ''' Handles AI enabled checkbox toggle
        ''' </summary>
        Private Sub OnAIEnabledToggled(vSender As Object, vArgs As EventArgs)
            Dim lEnabled As Boolean = pAIEnabledCheck.Active
            pShowArtifactsCheck.Sensitive = lEnabled
            pAutoContextCheck.Sensitive = lEnabled
            pMem0EnabledCheck.Sensitive = lEnabled
            pAIProviderCombo.Sensitive = lEnabled
            pAIModelEntry.Sensitive = lEnabled
            pMaxTokensSpin.Sensitive = lEnabled
            pTemperatureSpin.Sensitive = lEnabled
            pStreamResponsesCheck.Sensitive = lEnabled
            pAutoSuggestCheck.Sensitive = lEnabled
            pSaveHistoryCheck.Sensitive = lEnabled
            pHistoryLimitSpin.Sensitive = lEnabled AndAlso pSaveHistoryCheck.Active
            OnAIProviderChanged(vSender, vArgs)
        End Sub

        ''' <summary>
        ''' Handles AI provider selection change - grays out whichever of API key/Base URL/CLI
        ''' path don't apply to the newly-selected provider, and updates the model field's
        ''' placeholder and the help link underneath
        ''' </summary>
        Private Sub OnAIProviderChanged(vSender As Object, vArgs As EventArgs)
            Dim lEnabled As Boolean = pAIEnabledCheck.Active
            Dim lProviderIndex As Integer = pAIProviderCombo.Active

            ' 0=Claude API, 1=Claude Code CLI, 2=OpenRouter, 3=Local LLM
            Dim lNeedsApiKey As Boolean = (lProviderIndex = 0 OrElse lProviderIndex = 2)
            Dim lNeedsBaseUrl As Boolean = (lProviderIndex = 2 OrElse lProviderIndex = 3)
            Dim lNeedsCliPath As Boolean = (lProviderIndex = 1)

            pApiKeyLabel.Sensitive = lEnabled AndAlso lNeedsApiKey
            pApiKeyEntry.Sensitive = lEnabled AndAlso lNeedsApiKey
            pApiKeyVisibleCheck.Sensitive = lEnabled AndAlso lNeedsApiKey

            pAIBaseUrlLabel.Sensitive = lEnabled AndAlso lNeedsBaseUrl
            pAIBaseUrlEntry.Sensitive = lEnabled AndAlso lNeedsBaseUrl

            pClaudeCodePathLabel.Sensitive = lEnabled AndAlso lNeedsCliPath
            pClaudeCodePathEntry.Sensitive = lEnabled AndAlso lNeedsCliPath

            Select Case lProviderIndex
                Case 0
                    pApiKeyEntry.TooltipText = "Your Claude API key"
                    pAIModelEntry.PlaceholderText = "e.g. claude-sonnet-4-5 (blank = current default)"
                Case 1
                    pAIModelEntry.PlaceholderText = "e.g. claude-sonnet-4-5 (blank = CLI's own default)"
                Case 2
                    pApiKeyEntry.TooltipText = "Your OpenRouter API key"
                    pAIBaseUrlEntry.PlaceholderText = "https://openrouter.ai/api/v1"
                    pAIModelEntry.PlaceholderText = "e.g. anthropic/claude-3.5-sonnet"
                Case 3
                    pAIBaseUrlEntry.PlaceholderText = "http://localhost:11434/v1"
                    pAIModelEntry.PlaceholderText = "e.g. llama3.1:8b (must match a model loaded on your server)"
            End Select

            If pAIProviderHelpLabel IsNot Nothing Then UpdateAIProviderHelpLabel(pAIProviderHelpLabel)

            OnSettingChanged(vSender, vArgs)
        End Sub

        ''' <summary>
        ''' Updates the small help link under the provider fields to match the selected provider
        ''' </summary>
        Private Sub UpdateAIProviderHelpLabel(vLabel As Label)
            Select Case pAIProviderCombo.Active
                Case 0
                    vLabel.Markup = "<small>Get your API key from <a href='https://console.anthropic.com/'>Anthropic Console</a></small>"
                Case 1
                    vLabel.Markup = "<small>Uses the Claude Code CLI's own login - run ""claude"" once in a terminal to sign in. No API key needed here.</small>"
                Case 2
                    vLabel.Markup = "<small>Get your API key from <a href='https://openrouter.ai/keys'>OpenRouter</a></small>"
                Case 3
                    vLabel.Markup = "<small>Point this at any OpenAI-compatible local server (Ollama, LM Studio, etc.) - most don't need an API key.</small>"
                Case Else
                    vLabel.Markup = ""
            End Select
        End Sub
        
        ''' <summary>
        ''' Handles logging enabled checkbox toggle
        ''' </summary>
        Private Sub OnEnableLoggingToggled(vSender As Object, vArgs As EventArgs)
            Dim lEnabled As Boolean = pEnableLoggingCheck.Active
            pLogLevelCombo.Sensitive = lEnabled
            pMaxLogSizeSpin.Sensitive = lEnabled
            OnSettingChanged(vSender, vArgs)
        End Sub
        
        ''' <summary>
        ''' Handles the API key show/hide checkbox toggle
        ''' </summary>
        Private Sub OnApiKeyVisibleToggled(vSender As Object, vArgs As EventArgs)
            pApiKeyEntry.InnerEntry.Visibility = pApiKeyVisibleCheck.Active
        End Sub

        ''' <summary>
        ''' Handles save-history checkbox toggle
        ''' </summary>
        Private Sub OnSaveHistoryToggled(vSender As Object, vArgs As EventArgs)
            pHistoryLimitSpin.Sensitive = pSaveHistoryCheck.Active
            OnSettingChanged(vSender, vArgs)
        End Sub

        ' Get the parent window properly
        Private Function GetParentWindow() As Window
            Try
                ' Walk up the widget hierarchy to find the parent window
                Dim lParent As Widget = Me.Parent
                While lParent IsNot Nothing
                    If TypeOf lParent Is Window Then
                        Return CType(lParent, Window)
                    End If
                    lParent = lParent.Parent
                End While
                
                ' If no parent window found, return Nothing (which is valid for MessageDialog)
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetParentWindow error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Handles Git storage method change - updates security label
        ''' </summary>
        Private Sub OnGitStorageMethodChanged(vSender As Object, vArgs As EventArgs)
            Try
                ' Update the security label with information about the selected storage method
                Dim lSecurityLabel As Label = Nothing
                
                ' Find the security label in the Git tab
                ' It should be the last label added to the credentials box
                Dim lBox As Box = GetTabContentBox(pNotebook.GetNthPage(3))  ' Git is the 4th tab (0-indexed)
                If lBox IsNot Nothing Then
                    ' Search through the box hierarchy to find the security label
                    for each lChild in lBox.Children
                        If TypeOf lChild Is Frame Then
                            Dim lFrame As Frame = CType(lChild, Frame)
                            If lFrame.Label = "Git Credentials" Then
                                Dim lCredBox As Widget = lFrame.Child
                                If lCredBox IsNot Nothing AndAlso TypeOf lCredBox Is Box Then
                                    Dim lChildren = CType(lCredBox, Box).Children
                                    ' Get the last label which should be the security label
                                    for i As Integer = lChildren.Length - 1 To 0 Step -1
                                        If TypeOf lChildren(i) Is Label Then
                                            lSecurityLabel = CType(lChildren(i), Label)
                                            Exit for
                                        End If
                                    Next
                                End If
                                Exit for
                            End If
                        End If
                    Next
                End If
                
                ' Update the security label if found
                If lSecurityLabel IsNot Nothing Then
                    UpdateSecurityLabel(lSecurityLabel)
                End If
                
                ' Update credential manager with new storage method
                If pCredentialManager IsNot Nothing Then
                    Dim lSelectedIndex As Integer = pGitCredentialStorageCombo.Active
                    If lSelectedIndex >= 0 Then
                        ' Get available methods to map combo index to storage method
                        Dim lAvailableMethods As List(Of Utilities.CredentialManager.eStorageMethod) = 
                            Utilities.CredentialManager.GetAvailableMethods()
                            
                        If lSelectedIndex < lAvailableMethods.Count Then
                            Dim lNewMethod As Utilities.CredentialManager.eStorageMethod = lAvailableMethods(lSelectedIndex)
                            
                            ' Create new credential manager with selected method
                            pCredentialManager = New Utilities.CredentialManager(lNewMethod)
                            
                            ' Save the selected storage method to settings
                            pSettingsManager.SetString("Git.CredentialStorage", lNewMethod.ToString())
                        End If
                    End If
                End If
                
                ' Mark as modified
                OnSettingChanged(vSender, vArgs)
                
            Catch ex As Exception
                Console.WriteLine($"OnGitStorageMethodChanged error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Find the .vbproj file
        ''' </summary>
        Private Function FindProjectFile() As String
            Try
                ' Start from the executable's directory
                Dim lExePath As String = Reflection.Assembly.GetExecutingAssembly().Location
                Dim lCurrentDir As New IO.DirectoryInfo(IO.Path.GetDirectoryName(lExePath))
                
                ' Search up the directory tree
                While lCurrentDir IsNot Nothing
                    ' Check for SimpleIDE.vbproj
                    Dim lProjectPath As String = IO.Path.Combine(lCurrentDir.FullName, "SimpleIDE.vbproj")
                    If IO.File.Exists(lProjectPath) Then
                        Return lProjectPath
                    End If
                    
                    ' Also check for VbIDE.vbproj (alternate name)
                    lProjectPath = IO.Path.Combine(lCurrentDir.FullName, "VbIDE.vbproj")
                    If IO.File.Exists(lProjectPath) Then
                        Return lProjectPath
                    End If
                    
                    ' Check parent directory
                    lCurrentDir = lCurrentDir.Parent
                End While
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"FindIdeProjectFile error: {ex.Message}")
                Return ""
            End Try
        End Function

        ' Helper method to manually increment
        Private Sub IncrementVersionManually()
            Try
                Dim lIdeProjectPath As String = FindProjectFile()
                If String.IsNullOrEmpty(lIdeProjectPath) Then
                    #If DEBUG Then
                    Console.WriteLine("Project Not Found", "Could Not find *.vbproj")
                    #End If
                    Return
                End If
                
                Dim lVersionManager As New AssemblyVersionManager(lIdeProjectPath)
                Dim lCurrentVersion As Version = lVersionManager.GetCurrentVersion()
                
                Dim lNewVersion As New Version(
                    lCurrentVersion.Major,
                    lCurrentVersion.Minor,
                    lCurrentVersion.Build + 1,
                    lCurrentVersion.Revision)
                
                If lVersionManager.SetVersion(lNewVersion) Then
                    ' Clear cache and update UI
                    ApplicationVersion.ClearCache()
                    
                   
                Else
                    #If DEBUG Then
                    Console.WriteLine("Increment Failed", "Failed To increment Project version")
                    #End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IncrementVersionManually error: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateVersionOptionsState()
            If pVersionControls IsNot Nothing Then
                for each lControl in pVersionControls
                    lControl.Sensitive = pAutoIncrementEnableCheck.Active
                Next
            End If
        End Sub

    End Class
    
End Namespace
