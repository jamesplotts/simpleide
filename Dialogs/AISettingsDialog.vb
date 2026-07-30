' Dialogs/AISettingsDialog.vb - Settings dialog for AI integration
Imports Gtk
Imports System
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Dialogs
    
    Public Class AISettingsDialog
        Inherits Dialog
        
        ' ===== Private Fields =====
        Private pSettingsManager As SettingsManager
        Private pApiKeyEntry As Entry
        Private pModelCombo As ComboBoxText
        Private pMaxTokensSpinButton As SpinButton
        Private pTemperatureSpinButton As SpinButton
        Private pStreamResponsesCheckButton As CheckButton
        Private pAutoSuggestCheckButton As CheckButton
        Private pSaveHistoryCheckButton As CheckButton
        Private pHistoryLimitSpinButton As SpinButton
        
        ' ===== Constructor =====
        Public Sub New(vParent As Window, vSettingsManager As SettingsManager)
            MyBase.New("AI Settings", vParent, 
                       DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                       Stock.Cancel, ResponseType.Cancel,
                       Stock.Ok, ResponseType.Ok)
            
            pSettingsManager = vSettingsManager
            
            Try
                ' Set dialog properties
                SetDefaultSize(500, 400)
                BorderWidth = 12
                
                ' Build UI
                BuildUI()
                
                ' Load current settings
                LoadSettings()
                
                ' Show all
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"AISettingsDialog constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== UI Building =====
        Private Sub BuildUI()
            Try
                Dim lMainBox As New Box(Orientation.Vertical, 12)
                
                ' API Configuration section
                Dim lApiFrame As New Frame("API Configuration")
                Dim lApiBox As New Box(Orientation.Vertical, 6)
                lApiBox.BorderWidth = 12
                
                ' API Key
                Dim lApiKeyBox As New Box(Orientation.Horizontal, 6)
                Dim lApiKeyLabel As New Label("API key:")
                lApiKeyLabel.WidthRequest = 120
                lApiKeyLabel.Xalign = 0
                lApiKeyBox.PackStart(lApiKeyLabel, False, False, 0)
                
                pApiKeyEntry = New Entry()
                pApiKeyEntry.Visibility = False ' Hide API key
                pApiKeyEntry.PlaceholderText = "Enter your Claude API key"
                lApiKeyBox.PackStart(pApiKeyEntry, True, True, 0)
                
                ' Show/Hide API key button
                Dim lShowKeyButton As New ToggleButton("Show")
                AddHandler lShowKeyButton.Toggled, Sub(s, e)
                    pApiKeyEntry.Visibility = lShowKeyButton.Active
                    lShowKeyButton.Label = If(lShowKeyButton.Active, "Hide", "Show")
                End Sub
                lApiKeyBox.PackStart(lShowKeyButton, False, False, 0)
                
                lApiBox.PackStart(lApiKeyBox, False, False, 0)
                
                ' Model selection
                Dim lModelBox As New Box(Orientation.Horizontal, 6)
                Dim lModelLabel As New Label("Model:")
                lModelLabel.WidthRequest = 120
                lModelLabel.Xalign = 0
                lModelBox.PackStart(lModelLabel, False, False, 0)
                
                pModelCombo = New ComboBoxText()
                pModelCombo.AppendText("claude-3-opus-20240229")
                pModelCombo.AppendText("claude-3-sonnet-20240229")
                pModelCombo.AppendText("claude-3-haiku-20240307")
                pModelCombo.AppendText("claude-2.1")
                pModelCombo.AppendText("claude-2.0")
                lModelBox.PackStart(pModelCombo, True, True, 0)
                
                lApiBox.PackStart(lModelBox, False, False, 0)
                
                ' Help link
                Dim lHelpLabel As New Label()
                lHelpLabel.Markup = "<small>Get your API key from <a href='https://console.anthropic.com/'>Anthropic Console</a></small>"
                lHelpLabel.Xalign = 0
                lApiBox.PackStart(lHelpLabel, False, False, 0)
                
                lApiFrame.Add(lApiBox)
                lMainBox.PackStart(lApiFrame, False, False, 0)
                
                ' Generation Settings section
                Dim lGenFrame As New Frame("Generation Settings")
                Dim lGenBox As New Box(Orientation.Vertical, 6)
                lGenBox.BorderWidth = 12
                
                ' Max tokens
                Dim lMaxTokensBox As New Box(Orientation.Horizontal, 6)
                Dim lMaxTokensLabel As New Label("Max Tokens:")
                lMaxTokensLabel.WidthRequest = 120
                lMaxTokensLabel.Xalign = 0
                lMaxTokensBox.PackStart(lMaxTokensLabel, False, False, 0)
                
                pMaxTokensSpinButton = New SpinButton(100, 100000, 100)
                pMaxTokensSpinButton.Value = 4096
                lMaxTokensBox.PackStart(pMaxTokensSpinButton, False, False, 0)
                
                Dim lMaxTokensHelp As New Label("<small>Maximum response Length</small>")
                lMaxTokensHelp.UseMarkup = True
                lMaxTokensHelp.Xalign = 0
                lMaxTokensBox.PackStart(lMaxTokensHelp, True, True, 0)
                
                lGenBox.PackStart(lMaxTokensBox, False, False, 0)
                
                ' Temperature
                Dim lTemperatureBox As New Box(Orientation.Horizontal, 6)
                Dim lTemperatureLabel As New Label("Temperature:")
                lTemperatureLabel.WidthRequest = 120
                lTemperatureLabel.Xalign = 0
                lTemperatureBox.PackStart(lTemperatureLabel, False, False, 0)
                
                pTemperatureSpinButton = New SpinButton(0.0, 1.0, 0.1)
                pTemperatureSpinButton.Digits = 1
                pTemperatureSpinButton.Value = 0.7
                lTemperatureBox.PackStart(pTemperatureSpinButton, False, False, 0)
                
                Dim lTemperatureHelp As New Label("<small>0.0 = focused, 1.0 = creative</small>")
                lTemperatureHelp.UseMarkup = True
                lTemperatureHelp.Xalign = 0
                lTemperatureBox.PackStart(lTemperatureHelp, True, True, 0)
                
                lGenBox.PackStart(lTemperatureBox, False, False, 0)
                
                ' Stream responses
                pStreamResponsesCheckButton = New CheckButton("Stream responses (Show Text as it's generated)")
                pStreamResponsesCheckButton.Active = True
                lGenBox.PackStart(pStreamResponsesCheckButton, False, False, 0)
                
                lGenFrame.Add(lGenBox)
                lMainBox.PackStart(lGenFrame, False, False, 0)
                
                ' Features section
                Dim lFeaturesFrame As New Frame("Features")
                Dim lFeaturesBox As New Box(Orientation.Vertical, 6)
                lFeaturesBox.BorderWidth = 12
                
                ' Auto-suggest
                pAutoSuggestCheckButton = New CheckButton("Enable auto-suggestions while typing")
                pAutoSuggestCheckButton.Active = False
                lFeaturesBox.PackStart(pAutoSuggestCheckButton, False, False, 0)
                
                ' Save history
                pSaveHistoryCheckButton = New CheckButton("Save conversation history")
                pSaveHistoryCheckButton.Active = True
                lFeaturesBox.PackStart(pSaveHistoryCheckButton, False, False, 0)
                
                ' History limit
                Dim lHistoryBox As New Box(Orientation.Horizontal, 6)
                Dim lHistoryLabel As New Label("History limit:")
                lHistoryLabel.WidthRequest = 120
                lHistoryLabel.Xalign = 0
                lHistoryBox.PackStart(lHistoryLabel, False, False, 0)
                
                pHistoryLimitSpinButton = New SpinButton(0, 100, 1)
                pHistoryLimitSpinButton.Value = 20
                lHistoryBox.PackStart(pHistoryLimitSpinButton, False, False, 0)
                
                Dim lHistoryHelp As New Label("<small>conversations (0 = unlimited)</small>")
                lHistoryHelp.UseMarkup = True
                lHistoryHelp.Xalign = 0
                lHistoryBox.PackStart(lHistoryHelp, True, True, 0)
                
                lFeaturesBox.PackStart(lHistoryBox, False, False, 0)
                
                ' Enable/disable history limit based on save history
                AddHandler pSaveHistoryCheckButton.Toggled, Sub()
                    pHistoryLimitSpinButton.Sensitive = pSaveHistoryCheckButton.Active
                End Sub
                
                lFeaturesFrame.Add(lFeaturesBox)
                lMainBox.PackStart(lFeaturesFrame, False, False, 0)
                
                ' Add to dialog
                ContentArea.Add(lMainBox)
                
            Catch ex As Exception
                Console.WriteLine($"BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Settings Management =====
        Private Sub LoadSettings()
            Try
                ' API settings
                pApiKeyEntry.Text = pSettingsManager.GetString("AI.ApiKey", "")
                
                Dim lModel As String = pSettingsManager.GetString("AI.Model", "claude-3-sonnet-20240229")
                SelectComboBoxItem(pModelCombo, lModel)
                
                ' Generation settings
                pMaxTokensSpinButton.Value = pSettingsManager.GetInteger("AI.MaxTokens", 4096)
                pTemperatureSpinButton.Value = pSettingsManager.GetDouble("AI.Temperature", 0.7)
                pStreamResponsesCheckButton.Active = pSettingsManager.GetBoolean("AI.StreamResponses", True)
                
                ' Features
                pAutoSuggestCheckButton.Active = pSettingsManager.GetBoolean("AI.AutoSuggest", False)
                pSaveHistoryCheckButton.Active = pSettingsManager.GetBoolean("AI.SaveHistory", True)
                pHistoryLimitSpinButton.Value = pSettingsManager.GetInteger("AI.HistoryLimit", 20)
                
                ' Update UI state
                pHistoryLimitSpinButton.Sensitive = pSaveHistoryCheckButton.Active
                
            Catch ex As Exception
                Console.WriteLine($"LoadSettings error: {ex.Message}")
            End Try
        End Sub
        
        ' Save settings when OK is clicked
        Protected Overrides Sub OnResponse(vResponseId As ResponseType)
            Try
                If vResponseId = ResponseType.Ok Then
                    SaveSettings()
                End If
                
                MyBase.OnResponse(vResponseId)
                
            Catch ex As Exception
                Console.WriteLine($"OnResponse error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SaveSettings()
            Try
                ' API settings
                pSettingsManager.SetString("AI.ApiKey", pApiKeyEntry.Text.Trim())
                
                If pModelCombo.ActiveText IsNot Nothing Then
                    pSettingsManager.SetString("AI.Model", pModelCombo.ActiveText)
                End If
                
                ' Generation settings
                pSettingsManager.SetInteger("AI.MaxTokens", CInt(pMaxTokensSpinButton.Value))
                pSettingsManager.SetDouble("AI.Temperature", pTemperatureSpinButton.Value)
                pSettingsManager.SetBoolean("AI.StreamResponses", pStreamResponsesCheckButton.Active)
                
                ' Features
                pSettingsManager.SetBoolean("AI.AutoSuggest", pAutoSuggestCheckButton.Active)
                pSettingsManager.SetBoolean("AI.SaveHistory", pSaveHistoryCheckButton.Active)
                pSettingsManager.SetInteger("AI.HistoryLimit", CInt(pHistoryLimitSpinButton.Value))
                
                ' Save to disk
                pSettingsManager.SaveSettings()
                
            Catch ex As Exception
                Console.WriteLine($"SaveSettings error: {ex.Message}")
            End Try
        End Sub
        
        ' Helper to select item in ComboBoxText
        Private Sub SelectComboBoxItem(vCombo As ComboBoxText, vText As String)
            Try
                Dim lModel As ITreeModel = vCombo.Model
                Dim lIter As TreeIter
                
                If lModel.GetIterFirst(lIter) Then
                    Dim lIndex As Integer = 0
                    Do
                        Dim lValue As String = DirectCast(lModel.GetValue(lIter, 0), String)
                        If lValue = vText Then
                            vCombo.Active = lIndex
                            Exit Do
                        End If
                        lIndex += 1
                    Loop While lModel.IterNext(lIter)
                End If
                
                ' Default to first item if not found
                If vCombo.Active = -1 AndAlso vCombo.Model.IterNChildren() > 0 Then
                    vCombo.Active = 0
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SelectComboBoxItem error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace