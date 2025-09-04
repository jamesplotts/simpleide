' Refactored AIArtifactEditor.vb - Updated to use SourceFileInfo architecture
Imports System
Imports System.IO
Imports System.Text
Imports Gtk
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Editors
    
    ''' <summary>
    ''' Specialized editor for displaying and editing AI-generated artifacts
    ''' </summary>
    Public Class AIArtifactEditor
        Inherits Box
        
        ' ===== Private Fields =====
        Private pMainBox As Box
        Private pHeaderBox As Box
        Private pEditor As CustomDrawingEditor
        Private pArtifactTypeLabel As Label
        Private pArtifactNameLabel As Label
        Private pAcceptButton As Button
        Private pRejectButton As Button
        Private pCompareButton As Button
        Private pStatusLabel As Label
        
        ' SourceFileInfo for the editor
        Private pSourceFileInfo As SourceFileInfo
        
        ' Dependencies
        Private pSyntaxColorSet As SyntaxColorSet
        Private pSettingsManager As SettingsManager
        Private pProjectManager As ProjectManager
        Private pThemeManager As ThemeManager
        
        ' Artifact metadata
        Private pArtifactType As String = ""
        Private pArtifactName As String = ""
        Private pArtifactId As String = ""
        Private pOriginalContent As String = ""
        Private pTargetFilePath As String = ""
        
        ' ===== Events =====
        Public Event ArtifactAccepted(vArtifactId As String, vContent As String, vTargetPath As String)
        Public Event ArtifactRejected(vArtifactId As String)
        Public Event CompareRequested(vArtifactId As String, vContent As String, vTargetPath As String)
        
        ' ===== Constructor =====
        Public Sub New(vSyntaxColorSet As SyntaxColorSet, vSettingsManager As SettingsManager, vThemeManager As ThemeManager, Optional vProjectManager As ProjectManager = Nothing)
            MyBase.New(Orientation.Vertical, 0)
            
            Try
                pThemeManager = vThemeManager
                pSyntaxColorSet = vSyntaxColorSet
                pSettingsManager = vSettingsManager
                pProjectManager = vProjectManager
                InitializeComponents()
                ApplyStyling()
            Catch ex As Exception
                Console.WriteLine($"AIArtifactEditor.New error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Initialization =====
        Private Sub InitializeComponents()
            Try
                ' Create main container
                pMainBox = New Box(Orientation.Vertical, 5)
                pMainBox.BorderWidth = 5
                
                ' Create header area
                CreateHeaderArea()
                
                ' Create initial empty SourceFileInfo
                pSourceFileInfo = New SourceFileInfo("", "", "")
                pSourceFileInfo.TextLines.Add("")
                pSourceFileInfo.IsLoaded = True
                
                ' Create editor with SourceFileInfo
                pEditor = New CustomDrawingEditor(pSourceFileInfo, pThemeManager)
                pEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                
                ' Pack components
                pMainBox.PackStart(pHeaderBox, False, False, 0)
                pMainBox.PackStart(pEditor, True, True, 0)
                
                ' Create status bar
                CreateStatusBar()
                pMainBox.PackStart(pStatusLabel, False, False, 0)
                
                ' Add to main container
                PackStart(pMainBox, True, True, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeComponents error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateHeaderArea()
            Try
                pHeaderBox = New Box(Orientation.Horizontal, 5)
                pHeaderBox.BorderWidth = 5
                
                ' Artifact type label
                pArtifactTypeLabel = New Label("Type: Unknown")
                pArtifactTypeLabel.Halign = Align.Start
                pHeaderBox.PackStart(pArtifactTypeLabel, False, False, 0)
                
                ' Separator
                pHeaderBox.PackStart(New Separator(Orientation.Vertical), False, False, 5)
                
                ' Artifact name label
                pArtifactNameLabel = New Label("Artifact")
                pArtifactNameLabel.Halign = Align.Start
                pHeaderBox.PackStart(pArtifactNameLabel, True, True, 0)
                
                ' Action buttons
                pCompareButton = New Button("Compare")
                AddHandler pCompareButton.Clicked, AddressOf OnCompareClicked
                pHeaderBox.PackEnd(pCompareButton, False, False, 0)
                
                pRejectButton = New Button("Reject")
                AddHandler pRejectButton.Clicked, AddressOf OnRejectClicked
                pHeaderBox.PackEnd(pRejectButton, False, False, 5)
                
                pAcceptButton = New Button("Accept")
                AddHandler pAcceptButton.Clicked, AddressOf OnAcceptClicked
                pHeaderBox.PackEnd(pAcceptButton, False, False, 0)
                
            Catch ex As Exception
                Console.WriteLine($"CreateHeaderArea error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateStatusBar()
            Try
                pStatusLabel = New Label("Ready")
                pStatusLabel.Halign = Align.Start
                pStatusLabel.MarginStart = 5
                
            Catch ex As Exception
                Console.WriteLine($"CreateStatusBar error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Load an AI artifact for display and editing
        ''' </summary>
        Public Sub LoadArtifact(vArtifactId As String, vArtifactType As String, vArtifactName As String, vContent As String, Optional vTargetPath As String = "")
            Try
                ' Store metadata
                pArtifactId = vArtifactId
                pArtifactType = vArtifactType
                pArtifactName = vArtifactName
                pOriginalContent = vContent
                pTargetFilePath = vTargetPath
                
                ' Update UI
                UpdateArtifactInfo()
                
                ' Update SourceFileInfo with the artifact content
                Dim lSuggestedPath As String = GetSuggestedFilePath()
                
                ' Create new SourceFileInfo for the artifact
                pSourceFileInfo = New SourceFileInfo(lSuggestedPath, "", "")
                pSourceFileInfo.Content = vContent
                pSourceFileInfo.TextLines = New List(Of String)(vContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                If pSourceFileInfo.TextLines.Count = 0 Then
                    pSourceFileInfo.TextLines.Add("")
                End If
                pSourceFileInfo.IsLoaded = True
                
                ' Remove old editor and create new one with updated SourceFileInfo
                If pEditor IsNot Nothing Then
                    pMainBox.Remove(pEditor)
                End If
                
                pEditor = New CustomDrawingEditor(pSourceFileInfo, pThemeManager)
                pEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                
                ' Pack the new editor (before status bar)
                pMainBox.PackStart(pEditor, True, True, 0)
                pMainBox.ReorderChild(pEditor, 1)  ' After header, before status
                
                ShowAll()
                
                ' Enable/disable compare button
                pCompareButton.Sensitive = Not String.IsNullOrEmpty(vTargetPath) AndAlso File.Exists(vTargetPath)
                
                ' Update status
                UpdateStatus("AI artifact loaded")
                
            Catch ex As Exception
                Console.WriteLine($"LoadArtifact error: {ex.Message}")
                UpdateStatus($"Error loading artifact: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get the current content of the artifact
        ''' </summary>
        Public Function GetContent() As String
            Try
                If pEditor IsNot Nothing Then
                    Return pEditor.Text
                End If
                Return ""
            Catch ex As Exception
                Console.WriteLine($"GetContent error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Check if the artifact has been modified
        ''' </summary>
        Public Function IsModified() As Boolean
            Try
                If pEditor IsNot Nothing Then
                    Return pEditor.IsModified
                End If
                Return False
            Catch ex As Exception
                Console.WriteLine($"IsModified error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Private Helper Methods =====
        
        Private Sub UpdateArtifactInfo()
            Try
                ' Update type label
                pArtifactTypeLabel.Text = $"Type: {pArtifactType}"
                
                ' Update name label
                pArtifactNameLabel.Text = pArtifactName
                pArtifactNameLabel.TooltipText = pArtifactId
                
            Catch ex As Exception
                Console.WriteLine($"UpdateArtifactInfo error: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetSuggestedFilePath() As String
            Try
                ' Generate a suggested file path based on artifact type and name
                Dim lExtension As String = ".txt"
                
                Select Case pArtifactType.ToLower()
                    Case "code", "vb", "vbnet"
                        lExtension = ".vb"
                    Case "xml"
                        lExtension = ".xml"
                    Case "json"
                        lExtension = ".json"
                    Case "markdown", "md"
                        lExtension = ".md"
                    Case "html"
                        lExtension = ".html"
                    Case "css"
                        lExtension = ".css"
                    Case "javascript", "js"
                        lExtension = ".js"
                End Select
                
                ' Clean the artifact name for use as filename
                Dim lCleanName As String = pArtifactName
                for each lChar in System.IO.Path.GetInvalidFileNameChars()
                    lCleanName = lCleanName.Replace(lChar, "_"c)
                Next
                
                Return lCleanName & lExtension
                
            Catch ex As Exception
                Console.WriteLine($"GetSuggestedFilePath error: {ex.Message}")
                Return "artifact.txt"
            End Try
        End Function
        
        Private Sub UpdateStatus(vMessage As String)
            Try
                If pStatusLabel IsNot Nothing Then
                    pStatusLabel.Text = vMessage
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateStatus error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        Private Sub OnAcceptClicked(vSender As Object, vArgs As EventArgs)
            Try
                Dim lContent As String = GetContent()
                RaiseEvent ArtifactAccepted(pArtifactId, lContent, pTargetFilePath)
                UpdateStatus("Artifact accepted")
            Catch ex As Exception
                Console.WriteLine($"OnAcceptClicked error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnRejectClicked(vSender As Object, vArgs As EventArgs)
            Try
                RaiseEvent ArtifactRejected(pArtifactId)
                UpdateStatus("Artifact rejected")
            Catch ex As Exception
                Console.WriteLine($"OnRejectClicked error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnCompareClicked(vSender As Object, vArgs As EventArgs)
            Try
                Dim lContent As String = GetContent()
                RaiseEvent CompareRequested(pArtifactId, lContent, pTargetFilePath)
                UpdateStatus("Opening comparison view...")
            Catch ex As Exception
                Console.WriteLine($"OnCompareClicked error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Styling =====
        Private Sub ApplyStyling()
            Try
                ' Apply CSS styling for the header area
                Dim lCss As String = "
                    .artifact-header {
                        background-color: #f0f0f0;
                        border-radius: 5px;
                        padding: 5px;
                    }
                    .artifact-header:backdrop {
                        background-color: #e8e8e8;
                    }
                "
                
                CssHelper.ApplyCssToWidget(pHeaderBox, lCss, "artifact-header")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyStyling error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
