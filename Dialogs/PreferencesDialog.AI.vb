' PreferencesDialog.AI.vb
' Created: 2025-08-05 17:06:12
' Dialogs/PreferencesDialog.AI.vb - AI tab implementation for PreferencesDialog
Imports Gtk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers
Imports SimpleIDE.Dialogs

Namespace Dialogs
    
    Partial Public Class PreferencesDialog
        Inherits Dialog
        
        ' ===== AI Tab Controls =====
        Private pAIFrame As Frame
        Private pAISettingsButton As Button
        Private pAIQuickSettingsBox As Box
        'Private pAIEnabledCheck As CheckButton
        Private pShowArtifactsCheck As CheckButton
        Private pAutoContextCheck As CheckButton
        Private pMem0EnabledCheck As CheckButton
        Private pAIStatusLabel As Label
        
        ' ===== Create Enhanced AI Tab =====
        Private Function CreateAITab() As Widget
            Dim lMainBox As New Box(Orientation.Vertical, 10)
            lMainBox.BorderWidth = 10
            
            ' Quick Settings Frame
            pAIFrame = New Frame("AI Assistant Configuration")
            Dim lFrameBox As New Box(Orientation.Vertical, 10)
            lFrameBox.BorderWidth = 10
            
            ' Enable AI
            pAIEnabledCheck = New CheckButton("Enable AI Assistant")
            pAIEnabledCheck.TooltipText = "Enable Claude AI integration in the IDE"
            AddHandler pAIEnabledCheck.Toggled, AddressOf OnAIEnabledToggled
            lFrameBox.PackStart(pAIEnabledCheck, False, False, 0)
            
            ' Quick settings that are commonly toggled
            pAIQuickSettingsBox = New Box(Orientation.Vertical, 6)
            pAIQuickSettingsBox.MarginStart = 20
            
            ' Show artifacts
            pShowArtifactsCheck = New CheckButton("Automatically Show AI Artifacts in tabs")
            pShowArtifactsCheck.TooltipText = "Open AI-generated code in artifact Editor tabs"
            pAIQuickSettingsBox.PackStart(pShowArtifactsCheck, False, False, 0)
            
            ' Auto context
            pAutoContextCheck = New CheckButton("Include current file Context automatically")
            pAutoContextCheck.TooltipText = "Always Include the current file when asking AI questions"
            pAIQuickSettingsBox.PackStart(pAutoContextCheck, False, False, 0)
            
            ' Mem0 enabled
            pMem0EnabledCheck = New CheckButton("Enable Mem0 Memory system")
            pMem0EnabledCheck.TooltipText = "Use Mem0 to remember your coding patterns and preferences"
            pAIQuickSettingsBox.PackStart(pMem0EnabledCheck, False, False, 0)
            
            lFrameBox.PackStart(pAIQuickSettingsBox, False, False, 0)
            
            ' Separator
            lFrameBox.PackStart(New Separator(Orientation.Horizontal), False, False, 5)
            
            ' Status label
            pAIStatusLabel = New Label("")
            pAIStatusLabel.Xalign = 0
            pAIStatusLabel.UseMarkup = True
            lFrameBox.PackStart(pAIStatusLabel, False, False, 0)
            
            ' Advanced settings button
            Dim lButtonBox As New Box(Orientation.Horizontal, 0)
            
            pAISettingsButton = New Button("Advanced AI Settings...")
            pAISettingsButton.TooltipText = "Configure API keys, models, Artifacts, and more"
            AddHandler pAISettingsButton.Clicked, AddressOf OnAISettingsClicked
            lButtonBox.PackStart(pAISettingsButton, False, False, 0)
            
            lFrameBox.PackStart(lButtonBox, False, False, 0)
            
            pAIFrame.Add(lFrameBox)
            lMainBox.PackStart(pAIFrame, False, False, 0)
            
            ' Information Frame
            Dim lInfoFrame As New Frame("AI Features")
            Dim lInfoBox As New Box(Orientation.Vertical, 6)
            lInfoBox.BorderWidth = 10
            
            Dim lFeatures As String() = {
                "• Generate code from natural Language descriptions",
                "• Fix Errors and bugs with AI assistance",
                "• Refactor and improve existing code",
                "• Generate documentation and comments",
                "• Create unit tests automatically",
                "• Remember your coding patterns with Mem0",
                "• Compare AI suggestions with your code"
            }
            
            For Each lFeature In lFeatures
                Dim lLabel As New Label(lFeature)
                lLabel.Xalign = 0
                lInfoBox.PackStart(lLabel, False, False, 0)
            Next
            
            lInfoFrame.Add(lInfoBox)
            lMainBox.PackStart(lInfoFrame, True, True, 0)
            
            Return lMainBox
        End Function
        
        ' ===== Load AI Settings =====
        Private Sub LoadAISettings()
            Try
                ' Basic settings
                pAIEnabledCheck.Active = pSettingsManager.GetBoolean("AI.Enabled", False)
                pShowArtifactsCheck.Active = pSettingsManager.GetBoolean("AI.ShowArtifacts", True)
                pAutoContextCheck.Active = pSettingsManager.GetBoolean("AI.AutoContext", True)
                pMem0EnabledCheck.Active = pSettingsManager.GetBoolean("AI.Mem0.Enabled", False)
                
                ' Update UI state
                OnAIEnabledToggled(Nothing, Nothing)
                UpdateAIStatus()
                
            Catch ex As Exception
                Console.WriteLine($"LoadAISettings error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Save AI Settings =====
        Private Sub SaveAISettings()
            Try
                ' Basic settings
                pSettingsManager.SetBoolean("AI.Enabled", pAIEnabledCheck.Active)
                pSettingsManager.SetBoolean("AI.ShowArtifacts", pShowArtifactsCheck.Active)
                pSettingsManager.SetBoolean("AI.AutoContext", pAutoContextCheck.Active)
                pSettingsManager.SetBoolean("AI.Mem0.Enabled", pMem0EnabledCheck.Active)
                
            Catch ex As Exception
                Console.WriteLine($"SaveAISettings error: {ex.Message}")
                Throw
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        Private Sub OnAIEnabledToggled(vSender As Object, vArgs As EventArgs)
            Try
                Dim lEnabled As Boolean = pAIEnabledCheck.Active
                pAIQuickSettingsBox.Sensitive = lEnabled
                pAISettingsButton.Sensitive = lEnabled
                
                If lEnabled Then
                    UpdateAIStatus()
                Else
                    pAIStatusLabel.Markup = "<span color='gray'>AI Assistant is disabled</span>"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnAIEnabledToggled error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnAISettingsClicked(vSender As Object, vArgs As EventArgs)
            Try
                ' Show the enhanced AI settings dialog
                Dim lDialog As New EnhancedAISettingsDialog(CType(Me.Toplevel, Window), pSettingsManager)
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    ' Reload settings to reflect any changes
                    LoadAISettings()
                    
                    ' Notify that settings may have changed
                    pAIStatusLabel.Markup = "<span color='green'>AI settings updated</span>"
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAISettingsClicked error: {ex.Message}")
                ShowError("error", "Failed to open AI settings: " & ex.Message)
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        Private Sub UpdateAIStatus()
            Try
                If Not pAIEnabledCheck.Active Then
                    pAIStatusLabel.Markup = "<span color='gray'>AI Assistant is disabled</span>"
                    Return
                End If
                
                ' Check if API key is configured
                Dim lApiKey As String = pSettingsManager.GetString("AI.Claude.ApiKey", "")
                Dim lMem0Key As String = pSettingsManager.GetString("AI.Mem0.ApiKey", "")
                
                If String.IsNullOrEmpty(lApiKey) Then
                    pAIStatusLabel.Markup = "<span color='orange'>⚠ Claude API key not configured</span>"
                ElseIf pMem0EnabledCheck.Active AndAlso String.IsNullOrEmpty(lMem0Key) Then
                    pAIStatusLabel.Markup = "<span color='orange'>⚠ Mem0 enabled but API key not configured</span>"
                Else
                    Dim lModel As String = pSettingsManager.GetString("AI.Claude.Model", "claude-3-sonnet")
                    If pMem0EnabledCheck.Active Then
                        pAIStatusLabel.Markup = $"<span color='green'>✓ Ready with {lModel} + Mem0</span>"
                    Else
                        pAIStatusLabel.Markup = $"<span color='green'>✓ Ready with {lModel}</span>"
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateAIStatus error: {ex.Message}")
                pAIStatusLabel.Markup = "<span color='red'>error checking AI Status</span>"
            End Try
        End Sub
        
        Private Sub ShowError(vTitle As String, vMessage As String)
            Try
                Dim lDialog As New MessageDialog(CType(Me.Toplevel, Window),
                                               DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                               MessageType.Error,
                                               ButtonsType.Ok,
                                               vMessage)
                lDialog.Title = vTitle
                lDialog.Run()
                lDialog.Destroy()
            Catch ex As Exception
                Console.WriteLine($"ShowError error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
