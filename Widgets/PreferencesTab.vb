' Widgets/PreferencesTab.vb - Preferences displayed as a tab in the main notebook
Imports Gtk
Imports System
Imports System.Collections.Generic
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
        Private pShowSplashCheck As CheckButton
        Private pRestoreLayoutCheck As CheckButton
        Private pAutoSaveCheck As CheckButton
        Private pAutoSaveIntervalSpin As SpinButton
        Private pRecentFilesCountSpin As SpinButton
        
        ' Editor tab controls
        Private pFontButton As FontButton
        Private pTabWidthSpin As SpinButton
        Private pUseTabsRadio As RadioButton
        Private pUseSpacesRadio As RadioButton
        Private pShowLineNumbersCheck As CheckButton
        Private pHighlightCurrentLineCheck As CheckButton
        Private pWordWrapCheck As CheckButton
        Private pAutoIndentCheck As CheckButton
        Private pShowWhitespaceCheck As CheckButton
        Private pShowEndOfLineCheck As CheckButton
        
        ' Build tab controls
        Private pDefaultConfigCombo As ComboBoxText
        Private pDefaultPlatformCombo As ComboBoxText
        Private pVerbosityCombo As ComboBoxText
        Private pParallelBuildCheck As CheckButton
        Private pRestorePackagesCheck As CheckButton
        Private pShowOutputCheck As CheckButton
        Private pClearOutputCheck As CheckButton
        
        ' Git tab controls
        Private pGitEnabledCheck As CheckButton
        Private pGitUserNameEntry As Entry
        Private pGitEmailEntry As Entry
        Private pDefaultBranchEntry As Entry
        Private pAutoFetchCheck As CheckButton
        Private pFetchIntervalSpin As SpinButton
        Private pGitCredentialStorageCombo As ComboBoxText
        Private pGitCredentialTypeCombo As ComboBoxText
        Private pGitTokenEntry As Entry
        Private pGitTokenVisibleCheck As CheckButton
        Private pGitRemoteUrlEntry As Entry
        Private pCredentialManager As CredentialManager
        
        ' AI tab controls
        Private pAIEnabledCheck As CheckButton
        Private pShowArtifactsCheck As CheckButton
        Private pAutoContextCheck As CheckButton
        Private pMem0EnabledCheck As CheckButton
        Private pAISettingsButton As Button
        
        ' Advanced tab controls
        Private pEnableLoggingCheck As CheckButton
        Private pLogLevelCombo As ComboBoxText
        Private pMaxLogSizeSpin As SpinButton
        Private pEnableTelemetryCheck As CheckButton
        Private pCheckUpdatesCheck As CheckButton
        Private pBetaUpdatesCheck As CheckButton

        ' ===== IDE Version Auto-Increment Settings =====
        

								Private pAutoIncrementCheck As  CheckButton

        ' Add these as class-level fields
        Private pVersionControls As List(Of Widget)
        Private pAutoIncrementEnableCheck As CheckButton
        
        ' ===== Events (IEditor Implementation) =====
        Public Event Modified As EventHandler  
        Public Event CursorPositionChanged(vLine As Integer, vColumn As Integer) 
        
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
                Dim lApplyButton As New Button("_Apply")
                lApplyButton.UseUnderline = True
                AddHandler lApplyButton.Clicked, AddressOf OnApplyClicked
                lHeaderBox.PackEnd(lApplyButton, False, False, 0)
                
                ' Save button
                Dim lSaveButton As New Button("_Save")
                lSaveButton.UseUnderline = True
                AddHandler lSaveButton.Clicked, AddressOf OnSaveClicked
                lHeaderBox.PackEnd(lSaveButton, False, False, 5)
                
                PackStart(lHeaderBox, False, False, 0)
                
                ' Create separator
                PackStart(New Separator(Orientation.Horizontal), False, False, 0)
                
                ' Create notebook for category tabs
                pNotebook = New Notebook()
                pNotebook.BorderWidth = 10
                
                ' Add category tabs
                pNotebook.AppendPage(CreateGeneralTab(), New Label("General"))
                pNotebook.AppendPage(CreateEditorTab(), New Label("Editor"))
                pNotebook.AppendPage(CreateBuildTab(), New Label("Build"))
                pNotebook.AppendPage(CreateGitTab(), New Label("Git"))
                pNotebook.AppendPage(CreateAITab(), New Label("AI"))
                pNotebook.AppendPage(CreateAdvancedTab(), New Label("Advanced"))
                pNotebook.AppendPage(CreateVersionTab(), New Label("Project Version"))
                
                PackStart(pNotebook, True, True, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"PreferencesTab.InitializeUI error: {ex.Message}")
            End Try
        End Sub
        
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
            
            pShowSplashCheck = New CheckButton("Show splash screen on startup")
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
            pDefaultConfigCombo = New ComboBoxText()
            pDefaultConfigCombo.AppendText("Debug")
            pDefaultConfigCombo.AppendText("Release")
            AddHandler pDefaultConfigCombo.Changed, AddressOf OnSettingChanged
            lDefaultConfigBox.PackStart(pDefaultConfigCombo, True, True, 0)
            lConfigBox.PackStart(lDefaultConfigBox, False, False, 0)
            
            Dim lDefaultPlatformBox As New Box(Orientation.Horizontal, 10)
            lDefaultPlatformBox.PackStart(New Label("Default platform:"), False, False, 0)
            pDefaultPlatformCombo = New ComboBoxText()
            pDefaultPlatformCombo.AppendText("Any CPU")
            pDefaultPlatformCombo.AppendText("x86")
            pDefaultPlatformCombo.AppendText("x64")
            AddHandler pDefaultPlatformCombo.Changed, AddressOf OnSettingChanged
            lDefaultPlatformBox.PackStart(pDefaultPlatformCombo, True, True, 0)
            lConfigBox.PackStart(lDefaultPlatformBox, False, False, 0)
            
            Dim lVerbosityBox As New Box(Orientation.Horizontal, 10)
            lVerbosityBox.PackStart(New Label("Verbosity:"), False, False, 0)
            pVerbosityCombo = New ComboBoxText()
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
            pGitUserNameEntry = New Entry()
            pGitUserNameEntry.TooltipText = "Your name for Git commits"
            AddHandler pGitUserNameEntry.Changed, AddressOf OnSettingChanged
            lUserBox.PackStart(pGitUserNameEntry, True, True, 0)
            lConfigBox.PackStart(lUserBox, False, False, 0)
            
            Dim lEmailBox As New Box(Orientation.Horizontal, 10)
            lEmailBox.PackStart(New Label("Email:"), False, False, 0)
            pGitEmailEntry = New Entry()
            pGitEmailEntry.TooltipText = "Your email for Git commits"
            AddHandler pGitEmailEntry.Changed, AddressOf OnSettingChanged
            lEmailBox.PackStart(pGitEmailEntry, True, True, 0)
            lConfigBox.PackStart(lEmailBox, False, False, 0)
            
            Dim lBranchBox As New Box(Orientation.Horizontal, 10)
            lBranchBox.PackStart(New Label("Default branch:"), False, False, 0)
            pDefaultBranchEntry = New Entry()
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
            pGitRemoteUrlEntry = New Entry()
            pGitRemoteUrlEntry.TooltipText = "Git remote URL (e.g., https://github.com/username/repo.git)"
            pGitRemoteUrlEntry.WidthRequest = 350
            AddHandler pGitRemoteUrlEntry.Changed, AddressOf OnSettingChanged
            lRemoteBox.PackStart(pGitRemoteUrlEntry, True, True, 0)
            lCredBox.PackStart(lRemoteBox, False, False, 0)
            
            ' Credential Storage Method
            Dim lStorageBox As New Box(Orientation.Horizontal, 10)
            lStorageBox.PackStart(New Label("Storage method:"), False, False, 0)
            pGitCredentialStorageCombo = New ComboBoxText()
            
            ' Detect and populate available storage methods
            DetectAndPopulateStorageMethods()
            
            AddHandler pGitCredentialStorageCombo.Changed, AddressOf OnGitStorageMethodChanged
            lStorageBox.PackStart(pGitCredentialStorageCombo, True, True, 0)
            lCredBox.PackStart(lStorageBox, False, False, 0)
            
            ' Credential Type
            Dim lCredTypeBox As New Box(Orientation.Horizontal, 10)
            lCredTypeBox.PackStart(New Label("Credential type:"), False, False, 0)
            pGitCredentialTypeCombo = New ComboBoxText()
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
            pGitTokenEntry = New Entry()
            pGitTokenEntry.Visibility = False  ' Hide password by default
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
            
            ' AI Settings Button
            pAISettingsButton = New Button("Configure AI Connection...")
            AddHandler pAISettingsButton.Clicked, AddressOf OnAISettingsClicked
            lConfigBox.PackStart(pAISettingsButton, False, False, 10)
            
            lConfigFrame.Add(lConfigBox)
            lBox.PackStart(lConfigFrame, False, False, 0)
            
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
            pLogLevelCombo = New ComboBoxText()
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
                
                ' Editor
                pFontButton.Font = pSettingsManager.EditorFont
                pTabWidthSpin.Value = pSettingsManager.TabWidth
                pUseTabsRadio.Active = pSettingsManager.UseTabs
                pUseSpacesRadio.Active = Not pSettingsManager.UseTabs
                pShowLineNumbersCheck.Active = pSettingsManager.ShowLineNumbers
                pHighlightCurrentLineCheck.Active = pSettingsManager.HighlightCurrentLine
                pWordWrapCheck.Active = pSettingsManager.WordWrap
                pAutoIndentCheck.Active = pSettingsManager.AutoIndent
                pShowWhitespaceCheck.Active = pSettingsManager.GetBoolean("Editor.ShowWhitespace", False)
                pShowEndOfLineCheck.Active = pSettingsManager.GetBoolean("Editor.ShowEndOfLine", False)
                
                ' Build
                pDefaultConfigCombo.Active = If(pSettingsManager.GetString("Build.DefaultConfiguration", "Debug") = "Release", 1, 0)
                
                Dim lPlatform As String = pSettingsManager.GetString("Build.DefaultPlatform", "Any CPU")
                Select Case lPlatform
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
                pShowOutputCheck.Active = pSettingsManager.GetBoolean("Build.ShowOutput", True)
                pClearOutputCheck.Active = pSettingsManager.GetBoolean("Build.ClearOutput", True)
                
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
                
                ' Try to decrypt stored token
                Dim lEncryptedToken As String = pSettingsManager.GetString("Git.Token", "")
                If Not String.IsNullOrEmpty(lEncryptedToken) Then
                    Try
                        Dim lBytes() As Byte = Convert.FromBase64String(lEncryptedToken)
                        pGitTokenEntry.Text = System.Text.Encoding.UTF8.GetString(lBytes)
                    Catch
                        pGitTokenEntry.Text = ""
                    End Try
                End If
                
                ' AI
                pAIEnabledCheck.Active = pSettingsManager.GetBoolean("AI.Enabled", False)
                pShowArtifactsCheck.Active = pSettingsManager.GetBoolean("AI.ShowArtifacts", True)
                pAutoContextCheck.Active = pSettingsManager.GetBoolean("AI.AutoContext", False)
                pMem0EnabledCheck.Active = pSettingsManager.GetBoolean("AI.Mem0.Enabled", False)
                
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
                
                ' Editor
                pSettingsManager.EditorFont = pFontButton.Font
                pSettingsManager.TabWidth = CInt(pTabWidthSpin.Value)
                pSettingsManager.UseTabs = pUseTabsRadio.Active
                pSettingsManager.ShowLineNumbers = pShowLineNumbersCheck.Active
                pSettingsManager.HighlightCurrentLine = pHighlightCurrentLineCheck.Active
                pSettingsManager.WordWrap = pWordWrapCheck.Active
                pSettingsManager.AutoIndent = pAutoIndentCheck.Active
                pSettingsManager.SetBoolean("Editor.ShowWhitespace", pShowWhitespaceCheck.Active)
                pSettingsManager.SetBoolean("Editor.ShowEndOfLine", pShowEndOfLineCheck.Active)
                
                ' Build
                pSettingsManager.SetString("Build.DefaultConfiguration", If(pDefaultConfigCombo.Active = 1, "Release", "Debug"))
                pSettingsManager.SetString("Build.DefaultPlatform", pDefaultPlatformCombo.ActiveText)
                pSettingsManager.SetString("Build.Verbosity", pVerbosityCombo.ActiveText)
                pSettingsManager.SetBoolean("Build.ParallelBuild", pParallelBuildCheck.Active)
                pSettingsManager.SetBoolean("Build.RestorePackages", pRestorePackagesCheck.Active)
                pSettingsManager.SetBoolean("Build.ShowOutput", pShowOutputCheck.Active)
                pSettingsManager.SetBoolean("Build.ClearOutput", pClearOutputCheck.Active)
                
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
                
                ' Encrypt and save token if provided
                If Not String.IsNullOrEmpty(pGitTokenEntry.Text) AndAlso pGitCredentialTypeCombo.Active > 0 Then
                    Try
                        ' Simple Base64 encoding for now (should use proper encryption)
                        Dim lBytes() As Byte = System.Text.Encoding.UTF8.GetBytes(pGitTokenEntry.Text)
                        Dim lEncrypted As String = Convert.ToBase64String(lBytes)
                        pSettingsManager.SetString("Git.Token", lEncrypted)
                    Catch
                        pSettingsManager.SetString("Git.Token", "")
                    End Try
                Else
                    pSettingsManager.SetString("Git.Token", "")
                End If
                
                ' AI
                pSettingsManager.SetBoolean("AI.Enabled", pAIEnabledCheck.Active)
                pSettingsManager.SetBoolean("AI.ShowArtifacts", pShowArtifactsCheck.Active)
                pSettingsManager.SetBoolean("AI.AutoContext", pAutoContextCheck.Active)
                pSettingsManager.SetBoolean("AI.Mem0.Enabled", pMem0EnabledCheck.Active)
                
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
            pGitTokenEntry.Visibility = pGitTokenVisibleCheck.Active
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
            pAISettingsButton.Sensitive = lEnabled
            OnSettingChanged(vSender, vArgs)
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
        ''' Handles AI settings button click
        ''' </summary>
        Private Sub OnAISettingsClicked(vSender As Object, vArgs As EventArgs)
            Try
                ' Show AI settings dialog
                Dim lDialog As New Dialogs.AISettingsDialog(GetParentWindow, pSettingsManager)
                If lDialog.Run() = ResponseType.Ok Then
                    OnSettingChanged(vSender, vArgs)
                End If
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAISettingsClicked error: {ex.Message}")
            End Try
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
                Dim lGitTabWidget As Widget = pNotebook.GetNthPage(3)  ' Git is the 4th tab (0-indexed)
                If lGitTabWidget IsNot Nothing AndAlso TypeOf lGitTabWidget Is Box Then
                    ' Search through the box hierarchy to find the security label
                    Dim lBox As Box = CType(lGitTabWidget, Box)
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
                    Console.WriteLine("Project Not Found", "Could Not find *.vbproj")
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
                    Console.WriteLine("Increment Failed", "Failed To increment Project version")
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
