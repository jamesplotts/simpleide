' MainWindow.ManifestIntegration.vb - Application manifest integration for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports System.Xml
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow
    
    ' ===== Manifest Management =====
    
    Private pManifestEditor As ManifestEditor = Nothing
    Private pManifestTabIndex As Integer = -1
    
    ' Handle manifest selection from project explorer
    Private Sub OnManifestSelected()
        Try
            ' Check if manifest exists
            If Not ManifestExists() Then
                Dim lResponse As Integer = ShowQuestion(
                    "Create Manifest?",
                    "No application manifest found. would you like to create one?"
                )
                
                If lResponse = CInt(ResponseType.Yes) Then
                    If CreateDefaultManifest() Then
                        ' Refresh project explorer
                        pProjectExplorer?.RefreshManifestNode()
                        ' Open the newly created manifest
                        OpenManifestEditor()
                    End If
                End If
            Else
                ' Open existing manifest
                OpenManifestEditor()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnManifestSelected error: {ex.Message}")
            ShowError("Manifest error", ex.Message)
        End Try
    End Sub
    
    ' Open manifest editor
    Private Sub OpenManifestEditor()
        Try
            ' Check if already open
            If pManifestTabIndex >= 0 AndAlso pManifestTabIndex < pNotebook.NPages Then
                pNotebook.CurrentPage = pManifestTabIndex
                Return
            End If
            
            ' Create new manifest editor - FIXED: Pass correct parameters
            pManifestEditor = New ManifestEditor(Me, pCurrentProject, pSettingsManager)
            
            ' Handle events
            AddHandler pManifestEditor.Modified, AddressOf OnManifestModified
            AddHandler pManifestEditor.SaveRequested, AddressOf OnManifestSaveRequested
            
            ' Create tab
            Dim lTabLabel As Box = CreateTabLabel("app.manifest", True)
            pManifestTabIndex = pNotebook.AppendPage(pManifestEditor, lTabLabel)
            
            ' Show the tab
            pNotebook.ShowAll()
            pNotebook.CurrentPage = pManifestTabIndex
            
            ' Update status
            UpdateStatusBar("Opened application manifest")
            
        Catch ex As Exception
            Console.WriteLine($"OpenManifestEditor error: {ex.Message}")
            ShowError("Open Manifest error", ex.Message)
        End Try
    End Sub
    
    Private Sub OnManifestModified(vIsModified As Boolean)
        Try
            ' Update tab label if needed
            If pManifestTabIndex >= 0 Then
                Dim lTabLabel As Widget = pNotebook.GetTabLabel(pManifestEditor)
                If lTabLabel IsNot Nothing AndAlso TypeOf lTabLabel Is Box Then
                    ' FIXED: Call the overloaded version that takes Box and Boolean
                    UpdateTabModifiedState(CType(lTabLabel, Box), vIsModified)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnManifestModified error: {ex.Message}")
        End Try
    End Sub
    
    ' Handle manifest save request
    Private Sub OnManifestSaveRequested()
        Try
            SaveManifest()
        Catch ex As Exception
            Console.WriteLine($"OnManifestSaveRequested error: {ex.Message}")
        End Try
    End Sub
    
    ' Save manifest
    Private Sub SaveManifest()
        Try
            If pManifestEditor IsNot Nothing AndAlso pManifestEditor.IsModified Then
                ' FIXED: Call SaveManifest instead of Save
                pManifestEditor.SaveManifest()
                UpdateStatusBar("Manifest saved")
                ' Update project explorer
                pProjectExplorer?.RefreshManifestNode()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SaveManifest error: {ex.Message}")
            ShowError("Save Manifest error", ex.Message)
        End Try
    End Sub
    
    ' Close manifest editor
    Private Sub CloseManifestEditor()
        Try
            If pManifestEditor IsNot Nothing Then
                ' Check for unsaved changes
                If pManifestEditor.IsModified Then
                    Dim lResponse As Integer = ShowQuestion(
                        "Save Changes?",
                        "the manifest has unsaved Changes. Do you want to Save them?"
                    )
                    
                    If lResponse = CInt(ResponseType.Yes) Then
                        SaveManifest()
                    ElseIf lResponse = CInt(ResponseType.Cancel) Then
                        Return
                    End If
                End If
                
                ' Remove from notebook
                If pManifestTabIndex >= 0 Then
                    pNotebook.RemovePage(pManifestTabIndex)
                    pManifestTabIndex = -1
                End If
                
                ' Clean up
                pManifestEditor.Dispose()
                pManifestEditor = Nothing
            End If
            
        Catch ex As Exception
            Console.WriteLine($"CloseManifestEditor error: {ex.Message}")
        End Try
    End Sub
    
    ' Check if manifest exists
    Private Function ManifestExists() As Boolean
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then Return False
            
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
            Dim lManifestPath As String = System.IO.Path.Combine(lProjectDir, "app.manifest")
            
            Return File.Exists(lManifestPath)
            
        Catch ex As Exception
            Console.WriteLine($"ManifestExists error: {ex.Message}")
            Return False
        End Try
    End Function

'        ' Check if app.manifest exists
'        Public Function ManifestExists() As Boolean
'            Try
'                Dim lPath As String = GetManifestPath()
'                Return Not String.IsNullOrEmpty(lPath) AndAlso File.Exists(lPath)
'                
'            Catch ex As Exception
'                Console.WriteLine($"ManifestExists error: {ex.Message}")
'                Return False
'            End Try
'        End Function

    
'    ' Create default manifest
'    Private Function CreateDefaultManifest() As Boolean
'        Try
'            If String.IsNullOrEmpty(pCurrentProject) Then Return False
'            
'            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
'            Dim lManifestPath As String = System.IO.Path.Combine(lProjectDir, "app.manifest")
'            
'            ' Create default manifest content
'            Dim lContent As String = "<?xml Version=""1.0"" Encoding=""utf-8""?>" & Environment.NewLine & _
'                                   "<assembly manifestVersion=""1.0"" xmlns=""urn:schemas-microsoft-com:asm.v1"">" & Environment.NewLine & _
'                                   "  <assemblyIdentity Version=""1.0.0.0"" Name=""MyApplication.app""/>" & Environment.NewLine & _
'                                   "  <trustInfo xmlns=""urn:schemas-microsoft-com:asm.v2"">" & Environment.NewLine & _
'                                   "    <security>" & Environment.NewLine & _
'                                   "      <requestedPrivileges xmlns=""urn:schemas-microsoft-com:asm.v3"">" & Environment.NewLine & _
'                                   "        <requestedExecutionLevel Level=""asInvoker"" uiAccess=""false"" />" & Environment.NewLine & _
'                                   "      </requestedPrivileges>" & Environment.NewLine & _
'                                   "    </security>" & Environment.NewLine & _
'                                   "  </trustInfo>" & Environment.NewLine & _
'                                   "</assembly>"
'            
'            File.WriteAllText(lManifestPath, lContent)
'            Return True
'            
'        Catch ex As Exception
'            Console.WriteLine($"CreateDefaultManifest error: {ex.Message}")
'            ShowError("Create Manifest error", ex.Message)
'            Return False
'        End Try
'    End Function
    
    ' Toggle manifest embedding
    Public Sub ToggleManifestEmbedding(vEmbed As Boolean)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then Return
            
            Dim lVersionManager As New AssemblyVersionManager(pCurrentProject)
            
            If vEmbed Then
                ' Ensure manifest exists
                If Not ManifestExists() Then
                    If Not CreateDefaultManifest() Then
                        ShowError("Embed Failed", "Failed to create manifest file")
                        Return
                    End If
                End If
                
                ' Enable embedding by setting manifest path
                lVersionManager.SetManifestPath("app.manifest")
                UpdateStatusBar("Manifest embedding enabled")
            Else
                ' Disable embedding by clearing manifest path
                lVersionManager.SetManifestPath("")
                UpdateStatusBar("Manifest embedding disabled")
            End If
            
            ' Update project explorer
            pProjectExplorer?.RefreshManifestNode()
            
        Catch ex As Exception
            Console.WriteLine($"ToggleManifestEmbedding error: {ex.Message}")
            ShowError("Manifest Embedding error", ex.Message)
        End Try
    End Sub
    
    ' Initialize manifest integration
    Private Sub InitializeManifestIntegration()
        Try
            If pProjectExplorer IsNot Nothing Then
                ' Handle manifest selection event
                AddHandler pProjectExplorer.ManifestSelected, AddressOf OnManifestSelected
                
                ' Create manifest node if project is loaded
                If Not String.IsNullOrEmpty(pCurrentProject) Then
                    pProjectExplorer.CreateManifestNode()
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializeManifestIntegration error: {ex.Message}")
        End Try
    End Sub
    
    ' Update tab modified state
    Private Sub UpdateTabModifiedState(vEditor As IEditor)
        Try
            ' Find the tab for this editor
            For Each lTabEntry In pOpenTabs
                Dim lTabInfo As TabInfo = lTabEntry.Value
                If lTabInfo.Editor Is vEditor Then
                    ' Get the tab widget
                    Dim lTabWidget As Widget = pNotebook.GetTabLabel(vEditor.Widget)
                    If lTabWidget IsNot Nothing AndAlso TypeOf lTabWidget Is Box Then
                        UpdateTabModifiedState(CType(lTabWidget, Box), lTabInfo.Modified)
                    End If
                    Exit For
                End If
            Next
        Catch ex As Exception
            Console.WriteLine($"UpdateTabModifiedState error: {ex.Message}")
        End Try
    End Sub
    
    ' Update tab modified state
    Private Sub UpdateTabModifiedState(vTabLabel As Box, vIsModified As Boolean)
        Try
            ' Find the label in the box
            For Each lChild In vTabLabel.Children
                If TypeOf lChild Is Label Then
                    Dim lLabel As Label = CType(lChild, Label)
                    Dim lText As String = lLabel.Text
                    
                    ' Remove existing asterisk if any
                    If lText.EndsWith(" *") Then
                        lText = lText.Substring(0, lText.Length - 2)
                    End If
                    
                    ' Add asterisk if modified
                    If vIsModified Then
                        lText &= " *"
                    End If
                    
                    lLabel.Text = lText
                    Exit For
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"UpdateTabModifiedState error: {ex.Message}")
        End Try
    End Sub

    
End Class
