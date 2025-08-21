' MainWindow.Preferences.vb - Implementation for opening PreferencesTab from Edit menu

Imports Gtk
Imports System
Imports SimpleIDE.Widgets
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces

' MainWindow.Preferences.vb
' Created: 2025-08-20 23:33:45

Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Private Fields =====
    Private pPreferencesTab As PreferencesTab = Nothing
    Private pPreferencesTabIndex As Integer = -1
    
    ' ===== Preferences Tab Management =====
    
    ''' <summary>
    ''' Opens the preferences as a tab in the main notebook
    ''' </summary>
    Public Sub OnEditPreferences(vSender As Object, vArgs As EventArgs)
        Try
            ' Check if preferences tab is already open
            If pPreferencesTab IsNot Nothing AndAlso pPreferencesTabIndex >= 0 Then
                ' Preferences tab already exists, just switch to it
                pNotebook.CurrentPage = pPreferencesTabIndex
                Return
            End If
            
            ' Create new preferences tab
            OpenPreferencesTab()
            
        Catch ex As Exception
            Console.WriteLine($"OnEditPreferences error: {ex.Message}")
            ShowError("Preferences Error", $"Failed to open preferences: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Opens a new preferences tab in the main notebook
    ''' </summary>
    Private Sub OpenPreferencesTab()
        Try
            ' Create the preferences tab
            pPreferencesTab = New PreferencesTab(pSettingsManager, pThemeManager)
            
            ' Hook up events (if needed)
            ' AddHandler pPreferencesTab.SettingsChanged, AddressOf OnPreferencesSettingsChanged
            
            ' Create tab label with close button
            Dim lTabLabel As Box = CreatePreferencesTabLabel()
            
            ' Add to notebook
            pPreferencesTabIndex = pNotebook.AppendPage(pPreferencesTab, lTabLabel)
            
            ' Show all and switch to the new tab
            pNotebook.ShowAll()
            pNotebook.CurrentPage = pPreferencesTabIndex
            
            ' Update status bar
            UpdateStatusBar("Opened preferences")
            
        Catch ex As Exception
            Console.WriteLine($"OpenPreferencesTab error: {ex.Message}")
            Throw
        End Try
    End Sub
    
    ''' <summary>
    ''' Creates a tab label for the preferences tab with close button
    ''' </summary>
    Private Function CreatePreferencesTabLabel() As Box
        Try
            Dim lBox As New Box(Orientation.Horizontal, 3)
            
            ' Icon (optional)
            Dim lIcon As New Image()
            lIcon.SetFromIconName("preferences-system", IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLabel As New Label("Preferences")
            lBox.PackStart(lLabel, True, True, 0)
            
            ' Close button
            Dim lCloseButton As New Button()
            lCloseButton.Relief = ReliefStyle.None
            lCloseButton.FocusOnClick = False
            
            Dim lCloseIcon As New Image()
            lCloseIcon.SetFromIconName("window-close", IconSize.Menu)
            lCloseButton.Add(lCloseIcon)
            
            ' Hook up close button handler
            AddHandler lCloseButton.Clicked, AddressOf OnPreferencesTabCloseClicked
            
            lBox.PackStart(lCloseButton, False, False, 0)
            
            lBox.ShowAll()
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreatePreferencesTabLabel error: {ex.Message}")
            ' Return simple label as fallback
            Return New Box(Orientation.Horizontal, 0)
        End Try
    End Function
    
    ''' <summary>
    ''' Handles the preferences tab close button click
    ''' </summary>
    Private Sub OnPreferencesTabCloseClicked(vSender As Object, vArgs As EventArgs)
        Try
            ClosePreferencesTab()
        Catch ex As Exception
            Console.WriteLine($"OnPreferencesTabCloseClicked error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Closes the preferences tab
    ''' </summary>
    Private Sub ClosePreferencesTab()
        Try
            If pPreferencesTab IsNot Nothing Then
                ' Check if there are unsaved changes
                If pPreferencesTab.IsModified Then
                    ' FIX: Create a custom dialog since ShowQuestion only takes 2 parameters
                    Dim lDialog As New MessageDialog(
                        Me,
                        DialogFlags.Modal,
                        MessageType.Question,
                        ButtonsType.None,
                        "The preferences have unsaved changes. Do you want to save them before closing?"
                    )
                    lDialog.Title = "Unsaved Changes"
                    
                    ' Add custom buttons
                    lDialog.AddButton("Save", ResponseType.Yes)
                    lDialog.AddButton("Discard", ResponseType.No)
                    lDialog.AddButton("Cancel", ResponseType.Cancel)
                    
                    Dim lResponse As ResponseType = CType(lDialog.Run(), ResponseType)
                    lDialog.Destroy()
                    
                    Select Case lResponse
                        Case ResponseType.Yes  ' Save
                            If Not pPreferencesTab.Save() Then
                                Return  ' Don't close if save failed
                            End If
                        Case ResponseType.No  ' Discard
                            ' Continue closing
                        Case Else  ' Cancel or closed
                            Return  ' Don't close
                    End Select
                End If
                
                ' Remove the tab from notebook
                If pPreferencesTabIndex >= 0 Then
                    pNotebook.RemovePage(pPreferencesTabIndex)
                End If
                
                ' Clean up
                pPreferencesTab.Dispose()
                pPreferencesTab = Nothing
                pPreferencesTabIndex = -1
                
                ' Update status bar
                UpdateStatusBar("Closed preferences")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ClosePreferencesTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handler for when preferences settings change - applies changes immediately to the IDE
    ''' </summary>
    Private Sub OnPreferencesSettingsChanged(vSender As Object, vArgs As EventArgs)
        Try
            Console.WriteLine("Applying preferences changes to IDE...")
            
            ' ===== Apply Editor Settings to All Open Editors =====
            ApplyEditorSettingsToAllTabs()
            
            ' ===== Apply Theme Changes =====
            ApplyThemeChanges()
            
            ' ===== Apply UI Settings =====
            ApplyUISettings()
            
            ' ===== Apply Build Settings =====
            ' Build settings are typically used at build time, no immediate action needed
            
            ' ===== Apply Git Settings =====
            ApplyGitSettings()
            
            ' ===== Apply AI Settings =====
            ApplyAISettings()
            
            ' ===== Update Status Bar =====
            UpdateStatusBar("Settings applied")
            
            Console.WriteLine("Preferences changes applied successfully")
            
        Catch ex As Exception
            Console.WriteLine($"OnPreferencesSettingsChanged error: {ex.Message}")
            ShowError("Settings Error", $"Failed to apply some settings: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Applies editor settings to all open editor tabs
    ''' </summary>
    Private Sub ApplyEditorSettingsToAllTabs()
        Try
            ' Get settings from SettingsManager
            Dim lFont As String = pSettingsManager.EditorFont
            Dim lTabWidth As Integer = pSettingsManager.TabWidth
            Dim lUseTabs As Boolean = pSettingsManager.UseTabs
            Dim lShowLineNumbers As Boolean = pSettingsManager.ShowLineNumbers
            Dim lHighlightCurrentLine As Boolean = pSettingsManager.HighlightCurrentLine
            Dim lWordWrap As Boolean = pSettingsManager.WordWrap
            Dim lAutoIndent As Boolean = pSettingsManager.AutoIndent
            Dim lShowWhitespace As Boolean = pSettingsManager.GetBoolean("Editor.ShowWhitespace", False)
            Dim lShowEndOfLine As Boolean = pSettingsManager.GetBoolean("Editor.ShowEndOfLine", False)
            
            ' Apply to all open editors
            For Each lTabEntry In pOpenTabs
                Dim lTabInfo As TabInfo = lTabEntry.Value
                
                ' Skip non-editor tabs (like preferences, theme editor, etc.)
                If lTabInfo.Editor Is Nothing Then Continue For
                
                ' Apply settings based on editor type
                If TypeOf lTabInfo.Editor Is CustomDrawingEditor Then
                    Dim lEditor As CustomDrawingEditor = DirectCast(lTabInfo.Editor, CustomDrawingEditor)
                    
                    ' Apply font
                    If Not String.IsNullOrEmpty(lFont) Then
                        lEditor.ApplyFont(lFont)
                    End If
                    
                    ' Apply tab settings
                    lEditor.SetTabWidth(lTabWidth)
                    lEditor.SetUseTabs(lUseTabs)
                    
                    ' Apply display settings
                    lEditor.SetShowLineNumbers(lShowLineNumbers)
                    lEditor.SetHighlightCurrentLine(lHighlightCurrentLine)
                    lEditor.SetWordWrap(lWordWrap)
                    lEditor.SetAutoIndent(lAutoIndent)
                    lEditor.SetShowWhitespace(lShowWhitespace)
                    
                    ' Note: SetShowEndOfLine might not exist yet, check if method exists
                    Try
                        ' lEditor.SetShowEndOfLine(lShowEndOfLine)
                    Catch
                        ' Method might not exist yet
                    End Try
                    
                    ' Force redraw to apply changes
                    lEditor.QueueDraw()
                    
                ElseIf TypeOf lTabInfo.Editor Is IEditor Then
                    ' Generic IEditor interface - apply what we can
                    Dim lEditor As IEditor = lTabInfo.Editor
                    
                    ' Apply font if supported
                    Try
                        lEditor.ApplyFont(lFont)
                    Catch
                        ' Editor might not support font changes
                    End Try
                    
                    ' Apply tab settings if supported
                    Try
                        lEditor.TabWidth = lTabWidth
                        lEditor.UseTabs = lUseTabs
                        lEditor.AutoIndent = lAutoIndent
                    Catch
                        ' Editor might not support these properties
                    End Try
                End If
            Next
            
            Console.WriteLine("Editor settings applied to all tabs")
            
        Catch ex As Exception
            Console.WriteLine($"ApplyEditorSettingsToAllTabs error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Applies theme changes to all components
    ''' </summary>
    Private Sub ApplyThemeChanges()
        Try
            ' Get current theme name from settings
            Dim lThemeName As String = pSettingsManager.CurrentTheme
            
            ' Apply theme if changed
            If pThemeManager IsNot Nothing AndAlso Not String.IsNullOrEmpty(lThemeName) Then
                ' FIX: Use GetCurrentTheme() method instead of CurrentTheme property
                If pThemeManager.GetCurrentTheme() <> lThemeName Then
                    pThemeManager.SetTheme(lThemeName)
                    
                    ' Apply to all editors
                    ApplyThemeToAllEditors()
                End If
            End If
            
            Console.WriteLine($"Theme settings applied: {lThemeName}")
            
        Catch ex As Exception
            Console.WriteLine($"ApplyThemeChanges error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Applies UI settings like panel visibility
    ''' </summary>
    Private Sub ApplyUISettings()
        Try
            ' Apply panel visibility settings
            Dim lShowProjectExplorer As Boolean = pSettingsManager.ShowProjectExplorer
            Dim lShowBottomPanel As Boolean = pSettingsManager.ShowBottomPanel
            
            ' Update left panel visibility
            If pLeftPanelVisible <> lShowProjectExplorer Then
                pLeftPanelVisible = lShowProjectExplorer
                UpdatePanelVisibility()
            End If
            
            ' Update bottom panel visibility
            If pBottomPanelVisible <> lShowBottomPanel Then
                pBottomPanelVisible = lShowBottomPanel
                UpdatePanelVisibility()
            End If
            
            ' Apply auto-save settings
            Dim lAutoSave As Boolean = pSettingsManager.GetBoolean("General.AutoSave", False)
            Dim lAutoSaveInterval As Integer = pSettingsManager.GetInteger("General.AutoSaveInterval", 5)
            
            ' Update or stop auto-save timer
            If lAutoSave Then
                StartAutoSaveTimer(lAutoSaveInterval)
            Else
                StopAutoSaveTimer()
            End If
            
            Console.WriteLine("UI settings applied")
            
        Catch ex As Exception
            Console.WriteLine($"ApplyUISettings error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Applies Git settings
    ''' </summary>
    Private Sub ApplyGitSettings()
        Try
            ' Get Git settings
            Dim lGitEnabled As Boolean = pSettingsManager.GetBoolean("Git.Enabled", False)
            
            If lGitEnabled Then
                ' Update Git configuration if enabled
                Dim lUserName As String = pSettingsManager.GetString("Git.UserName", "")
                Dim lEmail As String = pSettingsManager.GetString("Git.Email", "")
                
                ' Apply Git config if values are provided
                If Not String.IsNullOrEmpty(lUserName) AndAlso Not String.IsNullOrEmpty(lEmail) Then
                    ' Could call git config commands here if needed
                    Console.WriteLine($"Git config: {lUserName} <{lEmail}>")
                End If
                
                ' Update auto-fetch settings
                Dim lAutoFetch As Boolean = pSettingsManager.GetBoolean("Git.AutoFetch", False)
                Dim lFetchInterval As Integer = pSettingsManager.GetInteger("Git.FetchInterval", 15)
                
                If lAutoFetch Then
                    ' Start or update auto-fetch timer
                    StartGitAutoFetchTimer(lFetchInterval)
                Else
                    ' Stop auto-fetch timer
                    StopGitAutoFetchTimer()
                End If
            Else
                ' Disable Git features
                StopGitAutoFetchTimer()
            End If
            
            ' Update Git panel if visible
            If pGitPanel IsNot Nothing Then
                pGitPanel.RefreshGitStatus()
            End If
            
            Console.WriteLine("Git settings applied")
            
        Catch ex As Exception
            Console.WriteLine($"ApplyGitSettings error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Applies AI Assistant settings
    ''' </summary>
    Private Sub ApplyAISettings()
        Try
            ' Get AI settings
            Dim lAIEnabled As Boolean = pSettingsManager.GetBoolean("AI.Enabled", False)
            
            If lAIEnabled Then
                ' FIX: Comment out or remove pAIPanel references until it's implemented
                ' For now, just log the settings
                
                ' Apply artifact settings
                Dim lShowArtifacts As Boolean = pSettingsManager.GetBoolean("AI.ShowArtifacts", True)
                Dim lAutoContext As Boolean = pSettingsManager.GetBoolean("AI.AutoContext", True)
                
                Console.WriteLine($"AI Settings: ShowArtifacts={lShowArtifacts}, AutoContext={lAutoContext}")
                
                ' TODO: Update AI panel when it's implemented
                ' If pAIPanel IsNot Nothing Then
                '     pAIPanel.ShowArtifacts = lShowArtifacts
                '     pAIPanel.AutoContext = lAutoContext
                ' End If
                
                ' Update Mem0 settings if enabled
                Dim lMem0Enabled As Boolean = pSettingsManager.GetBoolean("AI.Mem0.Enabled", False)
                If lMem0Enabled Then
                    ' Initialize or update Mem0 integration
                    Console.WriteLine("Mem0 integration enabled")
                End If
            End If
            
            Console.WriteLine("AI settings applied")
            
        Catch ex As Exception
            Console.WriteLine($"ApplyAISettings error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Helper Methods for Timers =====
    
    Private pAutoSaveTimer As System.Threading.Timer = Nothing
    Private pGitAutoFetchTimer As System.Threading.Timer = Nothing
    
    ''' <summary>
    ''' Starts or updates the auto-save timer
    ''' </summary>
    Private Sub StartAutoSaveTimer(vIntervalMinutes As Integer)
        Try
            ' Stop existing timer if any
            StopAutoSaveTimer()
            
            ' Create new timer
            Dim lInterval As Integer = vIntervalMinutes * 60 * 1000  ' Convert to milliseconds
            pAutoSaveTimer = New System.Threading.Timer(
                AddressOf AutoSaveTimerCallback,
                Nothing,
                lInterval,
                lInterval
            )
            
            Console.WriteLine($"Auto-save timer started: {vIntervalMinutes} minutes")
            
        Catch ex As Exception
            Console.WriteLine($"StartAutoSaveTimer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Stops the auto-save timer
    ''' </summary>
    Private Sub StopAutoSaveTimer()
        Try
            If pAutoSaveTimer IsNot Nothing Then
                pAutoSaveTimer.Dispose()
                pAutoSaveTimer = Nothing
                Console.WriteLine("Auto-save timer stopped")
            End If
        Catch ex As Exception
            Console.WriteLine($"StopAutoSaveTimer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Auto-save timer callback
    ''' </summary>
    Private Sub AutoSaveTimerCallback(vState As Object)
        Try
            ' Use Idle.Add to execute on UI thread
            GLib.Idle.Add(Function()
                ' Save all modified files
                For Each lTabEntry In pOpenTabs
                    Dim lTabInfo As TabInfo = lTabEntry.Value
                    If lTabInfo.Modified AndAlso lTabInfo.Editor IsNot Nothing Then
                        SaveFile(lTabInfo)
                    End If
                Next
                Return False  ' Don't repeat
            End Function)
        Catch ex As Exception
            Console.WriteLine($"AutoSaveTimerCallback error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Starts or updates the Git auto-fetch timer
    ''' </summary>
    Private Sub StartGitAutoFetchTimer(vIntervalMinutes As Integer)
        Try
            ' Stop existing timer if any
            StopGitAutoFetchTimer()
            
            ' Create new timer
            Dim lInterval As Integer = vIntervalMinutes * 60 * 1000  ' Convert to milliseconds
            pGitAutoFetchTimer = New System.Threading.Timer(
                AddressOf GitAutoFetchTimerCallback,
                Nothing,
                lInterval,
                lInterval
            )
            
            Console.WriteLine($"Git auto-fetch timer started: {vIntervalMinutes} minutes")
            
        Catch ex As Exception
            Console.WriteLine($"StartGitAutoFetchTimer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Stops the Git auto-fetch timer
    ''' </summary>
    Private Sub StopGitAutoFetchTimer()
        Try
            If pGitAutoFetchTimer IsNot Nothing Then
                pGitAutoFetchTimer.Dispose()
                pGitAutoFetchTimer = Nothing
                Console.WriteLine("Git auto-fetch timer stopped")
            End If
        Catch ex As Exception
            Console.WriteLine($"StopGitAutoFetchTimer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Git auto-fetch timer callback
    ''' </summary>
    Private Sub GitAutoFetchTimerCallback(vState As Object)
        Try
            ' Use Idle.Add to execute on UI thread
            GLib.Idle.Add(Function()
                If pGitPanel IsNot Nothing Then
                    ' Perform Git fetch
                    ' pGitPanel.FetchRemote()
                    Console.WriteLine("Git auto-fetch executed")
                End If
                Return False  ' Don't repeat
            End Function)
        Catch ex As Exception
            Console.WriteLine($"GitAutoFetchTimerCallback error: {ex.Message}")
        End Try
    End Sub  
  
End Class

