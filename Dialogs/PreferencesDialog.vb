' Dialogs/PreferencesDialog.vb - Comprehensive preferences dialog
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Dialogs
    
    Public Class PreferencesDialog
        Inherits Dialog
        
        ' Private fields
        Private pNotebook As Notebook
        Private pSettingsManager As SettingsManager
        Private pThemeManager As ThemeManager
        
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
        
        ' Theme tab controls
        Private pThemeCombo As ComboBoxText
        Private pThemePreview As TextView
        Private pCustomizeThemeButton As Button
        
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
        
        ' AI tab controls
        Private pAIEnabledCheck As CheckButton
        Private pApiKeyEntry As Entry
        Private pModelCombo As ComboBoxText
        Private pMaxTokensSpin As SpinButton
        Private pTemperatureSpin As SpinButton
        
        ' Advanced tab controls
        Private pEnableLoggingCheck As CheckButton
        Private pLogLevelCombo As ComboBoxText
        Private pMaxLogSizeSpin As SpinButton
        Private pEnableTelemetryCheck As CheckButton
        Private pCheckUpdatesCheck As CheckButton
        Private pBetaUpdatesCheck As CheckButton

        Private pLeftPanelStartupCombo As ComboBoxText

        
        ' Constructor
        Public Sub New(vParent As Window, vSettingsManager As SettingsManager, vThemeManager As ThemeManager)
            MyBase.New("Preferences", vParent, DialogFlags.Modal)
            
            pSettingsManager = vSettingsManager
            pThemeManager = vThemeManager
            
            ' Window setup
            SetDefaultSize(700, 500)
            SetPosition(WindowPosition.CenterOnParent)
            BorderWidth = 5
            
            ' Build UI
            BuildUI()
            
            ' Load current settings
            LoadSettings()
            
            ' Add buttons
            AddButton("Cancel", ResponseType.Cancel)
            AddButton("Apply", ResponseType.Apply)
            AddButton("OK", ResponseType.Ok)
            
        End Sub
        
        Private Sub BuildUI()
            Try
                ' Create main vbox
                Dim lVBox As New Box(Orientation.Vertical, 5)
                
                ' Create notebook
                pNotebook = New Notebook()
                pNotebook.BorderWidth = 5
                
                ' Add tabs
                pNotebook.AppendPage(CreateGeneralTab(), New Label("General"))
                pNotebook.AppendPage(CreateEditorTab(), New Label("Editor"))
                pNotebook.AppendPage(CreateThemeTab(), New Label("Theme"))
                pNotebook.AppendPage(CreateBuildTab(), New Label("Build"))
                pNotebook.AppendPage(CreateGitTab(), New Label("git"))
                pNotebook.AppendPage(CreateAITab(), New Label("AI Assistant"))
                pNotebook.AppendPage(CreateAdvancedTab(), New Label("Advanced"))
                
                lVBox.PackStart(pNotebook, True, True, 0)
                
                ' Add to content area
                ContentArea.PackStart(lVBox, True, True, 0)
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ' Create General tab
        Private Function CreateGeneralTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Startup section
            Dim lStartupFrame As New Frame("Startup")
            Dim lStartupBox As New Box(Orientation.Vertical, 5)
            lStartupBox.BorderWidth = 10
            
            pShowSplashCheck = New CheckButton("Show splash screen on startup")
            lStartupBox.PackStart(pShowSplashCheck, False, False, 0)
            
            pRestoreLayoutCheck = New CheckButton("Restore window layout on startup")
            lStartupBox.PackStart(pRestoreLayoutCheck, False, False, 0)
            
            lStartupFrame.Add(lStartupBox)
            lVBox.PackStart(lStartupFrame, False, False, 0)
            
            ' Auto-save section
            Dim lAutoSaveFrame As New Frame("Auto-Save")
            Dim lAutoSaveBox As New Box(Orientation.Vertical, 5)
            lAutoSaveBox.BorderWidth = 10
            
            pAutoSaveCheck = New CheckButton("Enable auto-Save")
            AddHandler pAutoSaveCheck.Toggled, AddressOf OnAutoSaveToggled
            lAutoSaveBox.PackStart(pAutoSaveCheck, False, False, 0)
            
            Dim lIntervalBox As New Box(Orientation.Horizontal, 5)
            lIntervalBox.PackStart(New Label("Auto-Save interval (minutes):"), False, False, 0)
            
            pAutoSaveIntervalSpin = New SpinButton(1, 60, 1)
            lIntervalBox.PackStart(pAutoSaveIntervalSpin, False, False, 0)
            
            lAutoSaveBox.PackStart(lIntervalBox, False, False, 0)
            
            lAutoSaveFrame.Add(lAutoSaveBox)
            lVBox.PackStart(lAutoSaveFrame, False, False, 0)
            
            ' Recent files section
            Dim lRecentFrame As New Frame("Recent Files")
            Dim lRecentBox As New Box(Orientation.Horizontal, 5)
            lRecentBox.BorderWidth = 10
            
            lRecentBox.PackStart(New Label("Maximum recent files:"), False, False, 0)
            
            pRecentFilesCountSpin = New SpinButton(5, 50, 1)
            lRecentBox.PackStart(pRecentFilesCountSpin, False, False, 0)
            
            lRecentFrame.Add(lRecentBox)
            lVBox.PackStart(lRecentFrame, False, False, 0)
            
            Return lVBox
        End Function
        
        ' Create Editor tab
        Private Function CreateEditorTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Font section
            Dim lFontFrame As New Frame("Font")
            Dim lFontBox As New Box(Orientation.Horizontal, 5)
            lFontBox.BorderWidth = 10
            
            lFontBox.PackStart(New Label("Editor font:"), False, False, 0)
            
            pFontButton = New FontButton()
            lFontBox.PackStart(pFontButton, False, False, 0)
            
            lFontFrame.Add(lFontBox)
            lVBox.PackStart(lFontFrame, False, False, 0)
            
            ' Indentation section
            Dim lIndentFrame As New Frame("Indentation")
            Dim lIndentBox As New Box(Orientation.Vertical, 5)
            lIndentBox.BorderWidth = 10
            
            Dim lTabWidthBox As New Box(Orientation.Horizontal, 5)
            lTabWidthBox.PackStart(New Label("Tab Width:"), False, False, 0)
            
            pTabWidthSpin = New SpinButton(1, 16, 1)
            lTabWidthBox.PackStart(pTabWidthSpin, False, False, 0)
            
            lIndentBox.PackStart(lTabWidthBox, False, False, 0)
            
            pUseTabsRadio = New RadioButton("Use tabs")
            lIndentBox.PackStart(pUseTabsRadio, False, False, 0)
            
            pUseSpacesRadio = New RadioButton(pUseTabsRadio, "Use spaces")
            lIndentBox.PackStart(pUseSpacesRadio, False, False, 0)
            
            pAutoIndentCheck = New CheckButton("Enable auto-indent")
            lIndentBox.PackStart(pAutoIndentCheck, False, False, 0)
            
            lIndentFrame.Add(lIndentBox)
            lVBox.PackStart(lIndentFrame, False, False, 0)
            
            ' Display section
            Dim lDisplayFrame As New Frame("display")
            Dim lDisplayBox As New Box(Orientation.Vertical, 5)
            lDisplayBox.BorderWidth = 10
            
            pShowLineNumbersCheck = New CheckButton("Show Line numbers")
            lDisplayBox.PackStart(pShowLineNumbersCheck, False, False, 0)
            
            pHighlightCurrentLineCheck = New CheckButton("Highlight current Line")
            lDisplayBox.PackStart(pHighlightCurrentLineCheck, False, False, 0)
            
            pWordWrapCheck = New CheckButton("Word wrap")
            lDisplayBox.PackStart(pWordWrapCheck, False, False, 0)
            
            pShowWhitespaceCheck = New CheckButton("Show whitespace")
            lDisplayBox.PackStart(pShowWhitespaceCheck, False, False, 0)
            
            pShowEndOfLineCheck = New CheckButton("Show end of Line markers")
            lDisplayBox.PackStart(pShowEndOfLineCheck, False, False, 0)
            
            lDisplayFrame.Add(lDisplayBox)
            lVBox.PackStart(lDisplayFrame, False, False, 0)
            
            Return lVBox
        End Function
        
        ' Create Theme tab
        Private Function CreateThemeTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Theme selection
            Dim lSelectionBox As New Box(Orientation.Horizontal, 5)
            lSelectionBox.PackStart(New Label("Theme:"), False, False, 0)
            
            pThemeCombo = New ComboBoxText()
            ' Populate with available themes
            For Each lTheme In pThemeManager.GetAvailableThemes()
                pThemeCombo.AppendText(lTheme)
            Next
            AddHandler pThemeCombo.Changed, AddressOf OnThemeChanged
            lSelectionBox.PackStart(pThemeCombo, False, False, 0)
            
            pCustomizeThemeButton = New Button("Customize...")
            AddHandler pCustomizeThemeButton.Clicked, AddressOf OnCustomizeTheme
            lSelectionBox.PackStart(pCustomizeThemeButton, False, False, 0)
            
            lVBox.PackStart(lSelectionBox, False, False, 0)
            
            ' Preview
            Dim lPreviewFrame As New Frame("Preview")
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.In
            
            pThemePreview = New TextView()
            pThemePreview.Editable = False
            pThemePreview.Buffer.Text = GetPreviewText()
            lScrolled.Add(pThemePreview)
            
            lPreviewFrame.Add(lScrolled)
            lVBox.PackStart(lPreviewFrame, True, True, 0)
            
            Return lVBox
        End Function
        
        ' Create Build tab
        Private Function CreateBuildTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Default configuration
            Dim lConfigFrame As New Frame("Default Configuration")
            Dim lConfigBox As New Box(Orientation.Vertical, 5)
            lConfigBox.BorderWidth = 10
            
            Dim lDefaultConfigBox As New Box(Orientation.Horizontal, 5)
            lDefaultConfigBox.PackStart(New Label("Configuration:"), False, False, 0)
            
            pDefaultConfigCombo = New ComboBoxText()
            pDefaultConfigCombo.AppendText("Debug")
            pDefaultConfigCombo.AppendText("Release")
            lDefaultConfigBox.PackStart(pDefaultConfigCombo, False, False, 0)
            
            lConfigBox.PackStart(lDefaultConfigBox, False, False, 0)
            
            Dim lDefaultPlatformBox As New Box(Orientation.Horizontal, 5)
            lDefaultPlatformBox.PackStart(New Label("Platform:"), False, False, 0)
            
            pDefaultPlatformCombo = New ComboBoxText()
            pDefaultPlatformCombo.AppendText("any CPU")
            pDefaultPlatformCombo.AppendText("x86")
            pDefaultPlatformCombo.AppendText("x64")
            lDefaultPlatformBox.PackStart(pDefaultPlatformCombo, False, False, 0)
            
            lConfigBox.PackStart(lDefaultPlatformBox, False, False, 0)
            
            lConfigFrame.Add(lConfigBox)
            lVBox.PackStart(lConfigFrame, False, False, 0)
            
            ' Build options
            Dim lOptionsFrame As New Frame("Build Options")
            Dim lOptionsBox As New Box(Orientation.Vertical, 5)
            lOptionsBox.BorderWidth = 10
            
            Dim lVerbosityBox As New Box(Orientation.Horizontal, 5)
            lVerbosityBox.PackStart(New Label("Verbosity:"), False, False, 0)
            
            pVerbosityCombo = New ComboBoxText()
            pVerbosityCombo.AppendText("Quiet")
            pVerbosityCombo.AppendText("Minimal")
            pVerbosityCombo.AppendText("Normal")
            pVerbosityCombo.AppendText("Detailed")
            pVerbosityCombo.AppendText("Diagnostic")
            lVerbosityBox.PackStart(pVerbosityCombo, False, False, 0)
            
            lOptionsBox.PackStart(lVerbosityBox, False, False, 0)
            
            pParallelBuildCheck = New CheckButton("Enable parallel build")
            lOptionsBox.PackStart(pParallelBuildCheck, False, False, 0)
            
            pRestorePackagesCheck = New CheckButton("Restore NuGet Packages before build")
            lOptionsBox.PackStart(pRestorePackagesCheck, False, False, 0)
            
            pShowOutputCheck = New CheckButton("Show build output automatically")
            lOptionsBox.PackStart(pShowOutputCheck, False, False, 0)
            
            pClearOutputCheck = New CheckButton("Clear output before build")
            lOptionsBox.PackStart(pClearOutputCheck, False, False, 0)
            
            lOptionsFrame.Add(lOptionsBox)
            lVBox.PackStart(lOptionsFrame, False, False, 0)
            
            Return lVBox
        End Function
        
        ' Create Git tab
        Private Function CreateGitTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Git settings
            pGitEnabledCheck = New CheckButton("Enable git integration")
            AddHandler pGitEnabledCheck.Toggled, AddressOf OnGitEnabledToggled
            lVBox.PackStart(pGitEnabledCheck, False, False, 0)
            
            ' User configuration
            Dim lUserFrame As New Frame("User Configuration")
            Dim lUserBox As New Box(Orientation.Vertical, 5)
            lUserBox.BorderWidth = 10
            
            Dim lNameBox As New Box(Orientation.Horizontal, 5)
            lNameBox.PackStart(New Label("User Name:"), False, False, 0)
            
            pGitUserNameEntry = New Entry()
            lNameBox.PackStart(pGitUserNameEntry, True, True, 0)
            
            lUserBox.PackStart(lNameBox, False, False, 0)
            
            Dim lEmailBox As New Box(Orientation.Horizontal, 5)
            lEmailBox.PackStart(New Label("Email:"), False, False, 0)
            
            pGitEmailEntry = New Entry()
            lEmailBox.PackStart(pGitEmailEntry, True, True, 0)
            
            lUserBox.PackStart(lEmailBox, False, False, 0)
            
            lUserFrame.Add(lUserBox)
            lVBox.PackStart(lUserFrame, False, False, 0)
            
            ' Repository settings
            Dim lRepoFrame As New Frame("Repository Settings")
            Dim lRepoBox As New Box(Orientation.Vertical, 5)
            lRepoBox.BorderWidth = 10
            
            Dim lBranchBox As New Box(Orientation.Horizontal, 5)
            lBranchBox.PackStart(New Label("Default branch:"), False, False, 0)
            
            pDefaultBranchEntry = New Entry()
            pDefaultBranchEntry.Text = "Main"
            lBranchBox.PackStart(pDefaultBranchEntry, False, False, 0)
            
            lRepoBox.PackStart(lBranchBox, False, False, 0)
            
            pAutoFetchCheck = New CheckButton("Auto-fetch from remote")
            AddHandler pAutoFetchCheck.Toggled, AddressOf OnAutoFetchToggled
            lRepoBox.PackStart(pAutoFetchCheck, False, False, 0)
            
            Dim lFetchBox As New Box(Orientation.Horizontal, 5)
            lFetchBox.PackStart(New Label("Fetch interval (minutes):"), False, False, 0)
            
            pFetchIntervalSpin = New SpinButton(5, 60, 5)
            lFetchBox.PackStart(pFetchIntervalSpin, False, False, 0)
            
            lRepoBox.PackStart(lFetchBox, False, False, 0)
            
            lRepoFrame.Add(lRepoBox)
            lVBox.PackStart(lRepoFrame, False, False, 0)
            
            Return lVBox
        End Function
        
'        ' Create AI tab
'        Private Function CreateAITab() As Widget
'            Dim lVBox As New Box(Orientation.Vertical, 10)
'            lVBox.BorderWidth = 10
'            
'            ' AI settings
'            pAIEnabledCheck = New CheckButton("Enable AI Assistant")
'            AddHandler pAIEnabledCheck.Toggled, AddressOf OnAIEnabledToggled
'            lVBox.PackStart(pAIEnabledCheck, False, False, 0)
'            
'            ' API configuration
'            Dim lAPIFrame As New Frame("API Configuration")
'            Dim lAPIBox As New Box(Orientation.Vertical, 5)
'            lAPIBox.BorderWidth = 10
'            
'            Dim lKeyBox As New Box(Orientation.Horizontal, 5)
'            lKeyBox.PackStart(New Label("API Key:"), False, False, 0)
'            
'            pAPIKeyEntry = New Entry()
'            pAPIKeyEntry.Visibility = False ' Hide the API key
'            lKeyBox.PackStart(pAPIKeyEntry, True, True, 0)
'            
'            lAPIBox.PackStart(lKeyBox, False, False, 0)
'            
'            Dim lModelBox As New Box(Orientation.Horizontal, 5)
'            lModelBox.PackStart(New Label("Model:"), False, False, 0)
'            
'            pModelCombo = New ComboBoxText()
'            pModelCombo.AppendText("claude-3-opus-20240229")
'            pModelCombo.AppendText("claude-3-sonnet-20240229")
'            pModelCombo.AppendText("claude-3-haiku-20240307")
'            lModelBox.PackStart(pModelCombo, False, False, 0)
'            
'            lAPIBox.PackStart(lModelBox, False, False, 0)
'            
'            lAPIFrame.Add(lAPIBox)
'            lVBox.PackStart(lAPIFrame, False, False, 0)
'            
'            ' Generation settings
'            Dim lGenFrame As New Frame("Generation Settings")
'            Dim lGenBox As New Box(Orientation.Vertical, 5)
'            lGenBox.BorderWidth = 10
'            
'            Dim lTokensBox As New Box(Orientation.Horizontal, 5)
'            lTokensBox.PackStart(New Label("Max tokens:"), False, False, 0)
'            
'            pMaxTokensSpin = New SpinButton(100, 4000, 100)
'            lTokensBox.PackStart(pMaxTokensSpin, False, False, 0)
'            
'            lGenBox.PackStart(lTokensBox, False, False, 0)
'            
'            Dim lTempBox As New Box(Orientation.Horizontal, 5)
'            lTempBox.PackStart(New Label("Temperature:"), False, False, 0)
'            
'            pTemperatureSpin = New SpinButton(0, 1, 0.1)
'            pTemperatureSpin.Digits = 1
'            lTempBox.PackStart(pTemperatureSpin, False, False, 0)
'            
'            lGenBox.PackStart(lTempBox, False, False, 0)
'            
'            lGenFrame.Add(lGenBox)
'            lVBox.PackStart(lGenFrame, False, False, 0)
'            
'            Return lVBox
'        End Function
        
        ' Create Advanced tab
        Private Function CreateAdvancedTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 10)
            lVBox.BorderWidth = 10
            
            ' Logging section
            Dim lLoggingFrame As New Frame("Logging")
            Dim lLoggingBox As New Box(Orientation.Vertical, 5)
            lLoggingBox.BorderWidth = 10
            
            pEnableLoggingCheck = New CheckButton("Enable logging")
            AddHandler pEnableLoggingCheck.Toggled, AddressOf OnEnableLoggingToggled
            lLoggingBox.PackStart(pEnableLoggingCheck, False, False, 0)
            
            Dim lLevelBox As New Box(Orientation.Horizontal, 5)
            lLevelBox.PackStart(New Label("Log Level:"), False, False, 0)
            
            pLogLevelCombo = New ComboBoxText()
            pLogLevelCombo.AppendText("error")
            pLogLevelCombo.AppendText("Warning")
            pLogLevelCombo.AppendText("Info")
            pLogLevelCombo.AppendText("Debug")
            pLogLevelCombo.AppendText("Trace")
            lLevelBox.PackStart(pLogLevelCombo, False, False, 0)
            
            lLoggingBox.PackStart(lLevelBox, False, False, 0)
            
            Dim lSizeBox As New Box(Orientation.Horizontal, 5)
            lSizeBox.PackStart(New Label("Max log file size (MB):"), False, False, 0)
            
            pMaxLogSizeSpin = New SpinButton(1, 100, 1)
            lSizeBox.PackStart(pMaxLogSizeSpin, False, False, 0)
            
            lLoggingBox.PackStart(lSizeBox, False, False, 0)
            
            lLoggingFrame.Add(lLoggingBox)
            lVBox.PackStart(lLoggingFrame, False, False, 0)
            
            ' Updates section
            Dim lUpdatesFrame As New Frame("updates")
            Dim lUpdatesBox As New Box(Orientation.Vertical, 5)
            lUpdatesBox.BorderWidth = 10
            
            pCheckUpdatesCheck = New CheckButton("Check for updates automatically")
            lUpdatesBox.PackStart(pCheckUpdatesCheck, False, False, 0)
            
            pBetaUpdatesCheck = New CheckButton("Include beta updates")
            lUpdatesBox.PackStart(pBetaUpdatesCheck, False, False, 0)
            
            lUpdatesFrame.Add(lUpdatesBox)
            lVBox.PackStart(lUpdatesFrame, False, False, 0)
            
            ' Privacy section
            Dim lPrivacyFrame As New Frame("Privacy")
            Dim lPrivacyBox As New Box(Orientation.Vertical, 5)
            lPrivacyBox.BorderWidth = 10
            
            pEnableTelemetryCheck = New CheckButton("Send anonymous Usage statistics")
            lPrivacyBox.PackStart(pEnableTelemetryCheck, False, False, 0)
            
            lPrivacyFrame.Add(lPrivacyBox)
            lVBox.PackStart(lPrivacyFrame, False, False, 0)
            
            ' Reset button
            Dim lResetButton As New Button("Reset All Settings to Defaults")
            AddHandler lResetButton.Clicked, AddressOf OnResetSettings
            lVBox.PackEnd(lResetButton, False, False, 0)
            
            Return lVBox
        End Function
        
        ' Load current settings
        Private Sub LoadSettings()
            Try
                ' General
                pShowSplashCheck.Active = pSettingsManager.GetBooleanSetting("General.ShowSplash", True)
                pRestoreLayoutCheck.Active = pSettingsManager.GetBooleanSetting("General.RestoreLayout", True)
                pAutoSaveCheck.Active = pSettingsManager.GetBooleanSetting("General.AutoSave", False)
                pAutoSaveIntervalSpin.Value = pSettingsManager.GetIntegerSetting("General.AutoSaveInterval", 5)
                pRecentFilesCountSpin.Value = pSettingsManager.GetIntegerSetting("General.RecentFilesCount", 10)
                
                ' Editor
                pFontButton.Font = pSettingsManager.EditorFont
                pTabWidthSpin.Value = pSettingsManager.TabWidth
                If pSettingsManager.UseTabs Then
                    pUseTabsRadio.Active = True
                Else
                    pUseSpacesRadio.Active = True
                End If
                pShowLineNumbersCheck.Active = pSettingsManager.ShowLineNumbers
                pHighlightCurrentLineCheck.Active = pSettingsManager.HighlightCurrentLine
                pWordWrapCheck.Active = pSettingsManager.GetBooleanSetting("Editor.WordWrap", False)
                pAutoIndentCheck.Active = pSettingsManager.AutoIndent
                pShowWhitespaceCheck.Active = pSettingsManager.GetBooleanSetting("Editor.ShowWhitespace", False)
                pShowEndOfLineCheck.Active = pSettingsManager.GetBooleanSetting("Editor.ShowEndOfLine", False)
                
                ' Theme
                pThemeCombo.Active = GetThemeIndexInComboBox(pSettingsManager.ColorTheme)
                UpdateThemePreview()
                
                ' Build
                pDefaultConfigCombo.Active = If(pSettingsManager.GetSetting("Build.DefaultConfiguration") = "Release", 1, 0)
                Dim lPlatform As String = pSettingsManager.GetSetting("Build.DefaultPlatform", "any CPU")
                Select Case lPlatform
                    Case "x86"
                        pDefaultPlatformCombo.Active = 1
                    Case "x64"
                        pDefaultPlatformCombo.Active = 2
                    Case Else
                        pDefaultPlatformCombo.Active = 0
                End Select
                
                Dim lVerbosity As String = pSettingsManager.GetSetting("Build.Verbosity", "Normal")
                Select Case lVerbosity
                    Case "Quiet" : pVerbosityCombo.Active = 0
                    Case "Minimal" : pVerbosityCombo.Active = 1
                    Case "Normal" : pVerbosityCombo.Active = 2
                    Case "Detailed" : pVerbosityCombo.Active = 3
                    Case "Diagnostic" : pVerbosityCombo.Active = 4
                    Case Else : pVerbosityCombo.Active = 2
                End Select
                
                pParallelBuildCheck.Active = pSettingsManager.GetBooleanSetting("Build.ParallelBuild", True)
                pRestorePackagesCheck.Active = pSettingsManager.GetBooleanSetting("Build.RestorePackages", True)
                pShowOutputCheck.Active = pSettingsManager.GetBooleanSetting("Build.ShowOutput", True)
                pClearOutputCheck.Active = pSettingsManager.GetBooleanSetting("Build.ClearOutput", True)
                
                ' Git
                pGitEnabledCheck.Active = pSettingsManager.GetBooleanSetting("git.Enabled", True)
                pGitUserNameEntry.Text = pSettingsManager.GetSetting("git.UserName", "")
                pGitEmailEntry.Text = pSettingsManager.GetSetting("git.Email", "")
                pDefaultBranchEntry.Text = pSettingsManager.GetSetting("git.DefaultBranch", "Main")
                pAutoFetchCheck.Active = pSettingsManager.GetBooleanSetting("git.AutoFetch", False)
                pFetchIntervalSpin.Value = pSettingsManager.GetIntegerSetting("git.FetchInterval", 15)
                UpdateGitControls()
                
                ' AI
                pAIEnabledCheck.Active = pSettingsManager.GetBooleanSetting("AI.Enabled", False)
                pApiKeyEntry.Text = pSettingsManager.GetSetting("AI.APIKey", "")
                Dim lModel As String = pSettingsManager.GetSetting("AI.Model", "claude-3-sonnet-20240229")
                For i As Integer = 0 To pModelCombo.Model.IterNChildren() - 1
                    pModelCombo.Active = i
                    If pModelCombo.ActiveText = lModel Then
                        Exit For
                    End If
                Next
                pMaxTokensSpin.Value = pSettingsManager.GetIntegerSetting("AI.MaxTokens", 1000)
                pTemperatureSpin.Value = pSettingsManager.GetDoubleSetting("AI.Temperature", 0.7)
                UpdateAIControls()
                
                ' Advanced
                pEnableLoggingCheck.Active = pSettingsManager.GetBooleanSetting("Advanced.EnableLogging", False)
                Dim lLogLevel As String = pSettingsManager.GetSetting("Advanced.LogLevel", "Info")
                Select Case lLogLevel
                    Case "error" : pLogLevelCombo.Active = 0
                    Case "Warning" : pLogLevelCombo.Active = 1
                    Case "Info" : pLogLevelCombo.Active = 2
                    Case "Debug" : pLogLevelCombo.Active = 3
                    Case "Trace" : pLogLevelCombo.Active = 4
                    Case Else : pLogLevelCombo.Active = 2
                End Select
                pMaxLogSizeSpin.Value = pSettingsManager.GetIntegerSetting("Advanced.MaxLogSize", 10)
                pCheckUpdatesCheck.Active = pSettingsManager.GetBooleanSetting("Advanced.CheckUpdates", True)
                pBetaUpdatesCheck.Active = pSettingsManager.GetBooleanSetting("Advanced.BetaUpdates", False)
                pEnableTelemetryCheck.Active = pSettingsManager.GetBooleanSetting("Advanced.EnableTelemetry", False)
                UpdateLoggingControls()
                
            Catch ex As Exception
                Console.WriteLine($"LoadSettings error: {ex.Message}")
            End Try
        End Sub
        
        ' Save settings
        Private Sub SaveSettings()
            Try
                ' General
                pSettingsManager.SetSetting("General.ShowSplash", pShowSplashCheck.Active.ToString())
                pSettingsManager.SetSetting("General.RestoreLayout", pRestoreLayoutCheck.Active.ToString())
                pSettingsManager.SetSetting("General.AutoSave", pAutoSaveCheck.Active.ToString())
                pSettingsManager.SetSetting("General.AutoSaveInterval", CInt(pAutoSaveIntervalSpin.Value).ToString())
                pSettingsManager.SetSetting("General.RecentFilesCount", CInt(pRecentFilesCountSpin.Value).ToString())
                
                ' Editor
                pSettingsManager.EditorFont = pFontButton.Font
                pSettingsManager.TabWidth = CInt(pTabWidthSpin.Value)
                pSettingsManager.UseTabs = pUseTabsRadio.Active
                pSettingsManager.ShowLineNumbers = pShowLineNumbersCheck.Active
                pSettingsManager.HighlightCurrentLine = pHighlightCurrentLineCheck.Active
                pSettingsManager.SetSetting("Editor.WordWrap", pWordWrapCheck.Active.ToString())
                pSettingsManager.AutoIndent = pAutoIndentCheck.Active
                pSettingsManager.SetSetting("Editor.ShowWhitespace", pShowWhitespaceCheck.Active.ToString())
                pSettingsManager.SetSetting("Editor.ShowEndOfLine", pShowEndOfLineCheck.Active.ToString())
                
                ' Theme
                If pThemeCombo.ActiveText IsNot Nothing Then
                    pSettingsManager.ColorTheme = pThemeCombo.ActiveText
                End If
                
                ' Build
                pSettingsManager.SetSetting("Build.DefaultConfiguration", If(pDefaultConfigCombo.Active = 1, "Release", "Debug"))
                pSettingsManager.SetSetting("Build.DefaultPlatform", pDefaultPlatformCombo.ActiveText)
                pSettingsManager.SetSetting("Build.Verbosity", pVerbosityCombo.ActiveText)
                pSettingsManager.SetSetting("Build.ParallelBuild", pParallelBuildCheck.Active.ToString())
                pSettingsManager.SetSetting("Build.RestorePackages", pRestorePackagesCheck.Active.ToString())
                pSettingsManager.SetSetting("Build.ShowOutput", pShowOutputCheck.Active.ToString())
                pSettingsManager.SetSetting("Build.ClearOutput", pClearOutputCheck.Active.ToString())
                
                ' Git
                pSettingsManager.SetSetting("git.Enabled", pGitEnabledCheck.Active.ToString())
                pSettingsManager.SetSetting("git.UserName", pGitUserNameEntry.Text)
                pSettingsManager.SetSetting("git.Email", pGitEmailEntry.Text)
                pSettingsManager.SetSetting("git.DefaultBranch", pDefaultBranchEntry.Text)
                pSettingsManager.SetSetting("git.AutoFetch", pAutoFetchCheck.Active.ToString())
                pSettingsManager.SetSetting("git.FetchInterval", CInt(pFetchIntervalSpin.Value).ToString())
                
                ' AI
                pSettingsManager.SetSetting("AI.Enabled", pAIEnabledCheck.Active.ToString())
                pSettingsManager.SetSetting("AI.APIKey", pApiKeyEntry.Text)
                If pModelCombo.ActiveText IsNot Nothing Then
                    pSettingsManager.SetSetting("AI.Model", pModelCombo.ActiveText)
                End If
                pSettingsManager.SetSetting("AI.MaxTokens", CInt(pMaxTokensSpin.Value).ToString())
                pSettingsManager.SetSetting("AI.Temperature", pTemperatureSpin.Value.ToString())
                
                ' Advanced
                pSettingsManager.SetSetting("Advanced.EnableLogging", pEnableLoggingCheck.Active.ToString())
                pSettingsManager.SetSetting("Advanced.LogLevel", pLogLevelCombo.ActiveText)
                pSettingsManager.SetSetting("Advanced.MaxLogSize", CInt(pMaxLogSizeSpin.Value).ToString())
                pSettingsManager.SetSetting("Advanced.CheckUpdates", pCheckUpdatesCheck.Active.ToString())
                pSettingsManager.SetSetting("Advanced.BetaUpdates", pBetaUpdatesCheck.Active.ToString())
                pSettingsManager.SetSetting("Advanced.EnableTelemetry", pEnableTelemetryCheck.Active.ToString())
                
                ' Save to disk
                pSettingsManager.Save()
                
            Catch ex As Exception
                Console.WriteLine($"SaveSettings error: {ex.Message}")
                Throw
            End Try
        End Sub
        
        ' Event handlers
        Protected Overrides Sub OnResponse(vResponseId As ResponseType)
            Try
                Select Case vResponseId
                    Case ResponseType.Ok
                        SaveSettings()
                        MyBase.OnResponse(vResponseId) ' Call base to close dialog
                        
                    Case ResponseType.Apply
                        SaveSettings()
                        ' Don't call base - keeps dialog open
                        
                    Case ResponseType.Cancel
                        MyBase.OnResponse(vResponseId) ' Call base to close dialog
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnResponse error: {ex.Message}")
                ShowError("Save error", "Failed to Save settings: " & ex.Message)
                ' Don't call base - keeps dialog open on error
            End Try
        End Sub
        
        Private Sub OnAutoSaveToggled(vSender As Object, vArgs As EventArgs)
            pAutoSaveIntervalSpin.Sensitive = pAutoSaveCheck.Active
        End Sub
        
        Private Sub OnGitEnabledToggled(vSender As Object, vArgs As EventArgs)
            UpdateGitControls()
        End Sub
        
        Private Sub OnAutoFetchToggled(vSender As Object, vArgs As EventArgs)
            pFetchIntervalSpin.Sensitive = pAutoFetchCheck.Active
        End Sub
        
'        Private Sub OnAIEnabledToggled(vSender As Object, vArgs As EventArgs)
'            UpdateAIControls()
'        End Sub
        
        Private Sub OnEnableLoggingToggled(vSender As Object, vArgs As EventArgs)
            UpdateLoggingControls()
        End Sub
        
        Private Sub OnThemeChanged(vSender As Object, vArgs As EventArgs)
            UpdateThemePreview()
        End Sub
        
        Private Sub OnCustomizeTheme(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Show theme customization dialog
                ShowInfo("Theme Customization", "Theme customization dialog not yet implemented.")
                
            Catch ex As Exception
                Console.WriteLine($"OnCustomizeTheme error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnResetSettings(vSender As Object, vArgs As EventArgs)
            Try
                Dim lDialog As New MessageDialog(
                    Me,
                    DialogFlags.Modal,
                    MessageType.Warning,
                    ButtonsType.YesNo,
                    "Are you sure you want to reset all settings to their default values? this action cannot be undone."
                )
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    pSettingsManager.ResetToDefaults()
                    LoadSettings()
                    ShowInfo("Settings Reset", "All settings have been reset to their default values.")
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnResetSettings error: {ex.Message}")
                ShowError("Reset error", "Failed to reset settings: " & ex.Message)
            End Try
        End Sub
        
        ' Update control states
        Private Sub UpdateGitControls()
            Dim lEnabled As Boolean = pGitEnabledCheck.Active
            pGitUserNameEntry.Sensitive = lEnabled
            pGitEmailEntry.Sensitive = lEnabled
            pDefaultBranchEntry.Sensitive = lEnabled
            pAutoFetchCheck.Sensitive = lEnabled
            pFetchIntervalSpin.Sensitive = lEnabled AndAlso pAutoFetchCheck.Active
        End Sub
        
        Private Sub UpdateAIControls()
            Dim lEnabled As Boolean = pAIEnabledCheck.Active
            pApiKeyEntry.Sensitive = lEnabled
            pModelCombo.Sensitive = lEnabled
            pMaxTokensSpin.Sensitive = lEnabled
            pTemperatureSpin.Sensitive = lEnabled
        End Sub
        
        Private Sub UpdateLoggingControls()
            Dim lEnabled As Boolean = pEnableLoggingCheck.Active
            pLogLevelCombo.Sensitive = lEnabled
            pMaxLogSizeSpin.Sensitive = lEnabled
        End Sub
        
        Private Sub UpdateThemePreview()
            Try
                If pThemeCombo.ActiveText IsNot Nothing Then
                    Dim lTheme As EditorTheme = pThemeManager.GetTheme(pThemeCombo.ActiveText)
                    If lTheme IsNot Nothing Then
                        ' Apply theme colors to preview
                        ApplyThemeToTextView(pThemePreview, lTheme)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateThemePreview error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ApplyThemeToTextView(vTextView As TextView, vTheme As EditorTheme)
            Try
                ' Create CSS for the text view
                Dim lCss As String = $"
                    textview {{
                        background-Color: {vTheme.BackgroundColor};
                        Color: {vTheme.ForegroundColor};
                        font-family: {vTheme.FontFamily};
                        font-size: {vTheme.FontSize}pt;
                    }}
                    textview Text {{
                        background-Color: {vTheme.BackgroundColor};
                        Color: {vTheme.ForegroundColor};
                    }}
                    textview Text selection {{
                        background-Color: {vTheme.SelectionColor};
                    }}
                "
                
                CssHelper.ApplyCssToWidget(vTextView, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                
            Catch ex As Exception
                Console.WriteLine($"ApplyThemeToTextView error: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetPreviewText() As String
            Return "' VB.NET code Preview
Imports System
Imports System.Collections.Generic

Public Class SampleClass
    Private pValue As Integer
    
    Public Property Value As Integer
        Get
            Return pValue
        End Get
        Set(Value As Integer)
            pValue = Value
        End Set
    End Property
    
    Public Sub New()
        ' Constructor
        pValue = 0
    End Sub
    
    Public Function Calculate(vInput As Double) As Double
        ' This is a comment
        Dim lResult As Double = vInput * 2.5
        Return lResult
    End Function
End Class

' TODO: Add more sample code
"
        End Function
        
        ' Helper methods
        Private Sub ShowInfo(vTitle As String, vMessage As String)
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.Info,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Title = vTitle
            lDialog.Run()
            lDialog.Destroy()
        End Sub
        
'        Private Sub ShowError(vTitle As String, vMessage As String)
'            Dim lDialog As New MessageDialog(
'                Me,
'                DialogFlags.Modal,
'                MessageType.Error,
'                ButtonsType.Ok,
'                vMessage
'            )
'            lDialog.Title = vTitle
'            lDialog.Run()
'            lDialog.Destroy()
'        End Sub

        Private Function GetThemeIndexInComboBox(vThemeName As String) As Integer
            Try
                ' Search through the combo box items
                Dim lModel As ITreeModel = pThemeCombo.Model
                Dim lIter As TreeIter
                Dim lIndex As Integer = 0
                
                If lModel.GetIterFirst(lIter) Then
                    Do
                        Dim lText As String = CStr(lModel.GetValue(lIter, 0))
                        If lText = vThemeName Then
                            Return lIndex
                        End If
                        lIndex += 1
                    Loop While lModel.IterNext(lIter)
                End If
                
                ' If not found, return 0 for default
                Return 0
                
            Catch ex As Exception
                Console.WriteLine($"GetThemeIndexInComboBox error: {ex.Message}")
                Return 0
            End Try
        End Function
        
    End Class
    
End Namespace
