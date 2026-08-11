' MainWindow.Preferences.vb - Implementation for opening PreferencesTab from Edit menu

Imports Gtk
Imports System
Imports System.Threading.Tasks
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
            ' Check if preferences tab is already open - find its current page by widget
            ' identity rather than trusting pPreferencesTabIndex, which is only ever set
            ' once at creation time and goes stale the moment any other tab positioned
            ' before it is opened/closed and shifts every later page's index
            If pPreferencesTab IsNot Nothing Then
                For i As Integer = 0 To pNotebook.NPages - 1
                    If pNotebook.GetNthPage(i) Is pPreferencesTab Then
                        pNotebook.CurrentPage = i
                        Return
                    End If
                Next
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

            ' Re-apply settings live (theme, AI client, Git, UI) whenever Save/Apply writes
            ' new values, instead of requiring an app restart to take effect
            AddHandler pPreferencesTab.SettingsApplied, AddressOf OnPreferencesSettingsChanged

            ' Add to notebook
            pPreferencesTabIndex = pNotebook.AppendPage(pPreferencesTab, "Preferences")
            
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
    ''' Handler for when preferences settings change - applies changes immediately to the IDE
    ''' </summary>
    Private Sub OnPreferencesSettingsChanged()
        Try
            #If DEBUG Then
            Console.WriteLine("Applying preferences changes to IDE...")
            #End If
            
            ' ===== Apply Theme Changes =====
            ApplyThemeChanges()
            
            ' ===== Apply UI Settings =====
            ApplyUISettings()
            
            ' ===== Apply Build Settings =====
            ' Re-reads pBuildConfiguration from settings (Configuration/Platform/Verbosity/
            ' ParallelBuild/RestorePackages) so a change just saved in Preferences' Build tab
            ' takes effect on the very next build, without restarting the IDE
            LoadBuildConfiguration()
            
            ' ===== Apply Git Settings =====
            ApplyGitSettings()
            
            ' ===== Apply AI Settings =====
            ApplyAISettings()
            
            ' ===== Update Status Bar =====
            UpdateStatusBar("Settings applied")
            
            #If DEBUG Then
            Console.WriteLine("Preferences changes applied successfully")
            #End If
            
        Catch ex As Exception
            Console.WriteLine($"OnPreferencesSettingsChanged error: {ex.Message}")
            ShowError("Settings Error", $"Failed to apply some settings: {ex.Message}")
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
            
            #If DEBUG Then
            Console.WriteLine($"Theme settings applied: {lThemeName}")
            #End If
            
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
            
            #If DEBUG Then
            Console.WriteLine("UI settings applied")
            #End If
            
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
                
                ' Apply Git config if values are provided - --local (this repo's own
                ' .git/config) only, never --global, so this never silently rewrites the
                ' user's system-wide git identity
                If Not String.IsNullOrEmpty(lUserName) AndAlso Not String.IsNullOrEmpty(lEmail) Then
                    Dim lRepoDir As String = GetActiveGitRepositoryDirectory()
                    If Not String.IsNullOrEmpty(lRepoDir) Then
                        Dim lEscapedName As String = lUserName.Replace("""", """""")
                        Dim lEscapedEmail As String = lEmail.Replace("""", """""")
                        ExecuteGitCommand($"config --local user.name ""{lEscapedName}""", lRepoDir, Sub(lNameOutput, lNameExitCode)
                            #If DEBUG Then
                            If lNameExitCode <> 0 Then Console.WriteLine($"ApplyGitSettings: Failed to set user.name: {lNameOutput}")
                            #End If
                        End Sub)
                        ExecuteGitCommand($"config --local user.email ""{lEscapedEmail}""", lRepoDir, Sub(lEmailOutput, lEmailExitCode)
                            #If DEBUG Then
                            If lEmailExitCode <> 0 Then Console.WriteLine($"ApplyGitSettings: Failed to set user.email: {lEmailOutput}")
                            #End If
                        End Sub)
                    End If
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

            ' Re-apply the configured PAT/OAuth credential (if any) so a change made just now
            ' in the Git Credentials tab takes effect immediately, without restarting the IDE
            ApplyGitCredentials()

            ' Update Git panel if visible
            If pGitPanel IsNot Nothing Then
                pGitPanel.RefreshGitStatus()
            End If

            #If DEBUG Then
            Console.WriteLine("Git settings applied")
            #End If
            
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
                ' Rebuild the Claude client/AI Assistant panel with whatever API key/settings
                ' were just saved (was only done by the old modal AISettingsDialog on OK;
                ' now that AI settings live in the Preferences tab, this is the live-apply
                ' path instead)
                InitializeAI()

                ' Apply artifact settings
                Dim lShowArtifacts As Boolean = pSettingsManager.GetBoolean("AI.ShowArtifacts", True)
                Dim lAutoContext As Boolean = pSettingsManager.GetBoolean("AI.AutoContext", True)

                #If DEBUG Then
                Console.WriteLine($"AI Settings: ShowArtifacts={lShowArtifacts}, AutoContext={lAutoContext}")
                #End If

                ' TODO: Update AI panel when it's implemented
                ' If pAIPanel IsNot Nothing Then
                '     pAIPanel.ShowArtifacts = lShowArtifacts
                '     pAIPanel.AutoContext = lAutoContext
                ' End If
                
                ' Update Mem0 settings if enabled
                Dim lMem0Enabled As Boolean = pSettingsManager.GetBoolean("AI.Mem0.Enabled", False)
                If lMem0Enabled Then
                    ' Initialize or update Mem0 integration
                    #If DEBUG Then
                    Console.WriteLine("Mem0 integration enabled")
                    #End If
                End If
            End If
            
            #If DEBUG Then
            Console.WriteLine("AI settings applied")
            #End If
            
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
            
            #If DEBUG Then
            Console.WriteLine($"Auto-save timer started: {vIntervalMinutes} minutes")
            #End If
            
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
                #If DEBUG Then
                Console.WriteLine("Auto-save timer stopped")
                #End If
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
            
            #If DEBUG Then
            Console.WriteLine($"Git auto-fetch timer started: {vIntervalMinutes} minutes")
            #End If
            
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
                #If DEBUG Then
                Console.WriteLine("Git auto-fetch timer stopped")
                #End If
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
            Dim lRepoDir As String = GetActiveGitRepositoryDirectory()
            If String.IsNullOrEmpty(lRepoDir) Then Return

            Dim lFetchManager As New GitManager(lRepoDir)
            Task.Run(Async Function()
                Try
                    Dim lSuccess As Boolean = Await lFetchManager.Fetch()
                    Gtk.Application.Invoke(Sub()
                        If lSuccess Then
                            UpdateStatusBar("Git auto-fetch completed")
                            pGitPanel?.RefreshGitStatus()
                        Else
                            #If DEBUG Then
                            Console.WriteLine("Git auto-fetch failed")
                            #End If
                        End If
                    End Sub)
                Catch ex As Exception
                    Console.WriteLine($"GitAutoFetchTimerCallback (background) error: {ex.Message}")
                End Try
                Return Nothing
            End Function)
        Catch ex As Exception
            Console.WriteLine($"GitAutoFetchTimerCallback error: {ex.Message}")
        End Try
    End Sub
  
End Class

