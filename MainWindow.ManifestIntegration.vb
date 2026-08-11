' MainWindow.ManifestIntegration.vb - Application manifest integration for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports System.Xml
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Widgets

Partial Public Class MainWindow
    
    ' ===== Manifest Management =====
    
    Private pManifestEditor As ManifestEditor = Nothing
    ''' <summary>Special pOpenTabs key for the manifest editor tab - registering it there
    ''' (like Scratchpad/Theme Editor do) is what makes CheckUnsavedChanges/CloseAllTabs
    ''' prompt for it and OnCustomNotebookTabClosed clean it up when its own close button
    ''' is used, instead of a raw notebook-page-index field that goes stale the moment any
    ''' other tab is added/removed/reordered</summary>
    Private Const MANIFEST_TAB_KEY As String = "manifest:app.manifest"
    
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
            ' Check if already open - find its page by widget identity (the tab may have
            ' been reordered, or other tabs added/removed, since it was opened)
            If pOpenTabs.ContainsKey(MANIFEST_TAB_KEY) Then
                Dim lExisting As TabInfo = pOpenTabs(MANIFEST_TAB_KEY)
                For i As Integer = 0 To pNotebook.NPages - 1
                    If pNotebook.GetNthPage(i) Is lExisting.EditorContainer Then
                        pNotebook.CurrentPage = i
                        Return
                    End If
                Next
                ' Tab widget no longer in the notebook (closed through some other path) -
                ' drop the stale entry and fall through to recreate it
                pOpenTabs.Remove(MANIFEST_TAB_KEY)
            End If

            ' Create new manifest editor - FIXED: Pass correct parameters
            pManifestEditor = New ManifestEditor(Me, pCurrentProject, pSettingsManager)

            ' Handle events
            'AddHandler pManifestEditor.Modified, AddressOf OnManifestModified
            AddHandler pManifestEditor.SaveRequested, AddressOf OnManifestSaveRequested

            ' Create tab
            pNotebook.AppendPage(pManifestEditor, "app.manifest")

            ' Show the tab
            pNotebook.ShowAll()
            pNotebook.CurrentPage = pNotebook.NPages - 1

            ' Register in pOpenTabs (like Scratchpad/Theme Editor tabs) so
            ' CheckUnsavedChanges/CloseAllTabs prompt for unsaved manifest edits, and
            ' OnCustomNotebookTabClosed cleans this up correctly when the tab's own close
            ' button is used
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = MANIFEST_TAB_KEY
            lTabInfo.Editor = Nothing  ' ManifestEditor doesn't implement IEditor
            lTabInfo.EditorContainer = pManifestEditor
            lTabInfo.Modified = False
            pOpenTabs(MANIFEST_TAB_KEY) = lTabInfo

            ' Update status
            UpdateStatusBar("Opened application manifest")

        Catch ex As Exception
            Console.WriteLine($"OpenManifestEditor error: {ex.Message}")
            ShowError("Open Manifest error", ex.Message)
        End Try
    End Sub
    
    Private Sub OnManifestModified(vIsModified As Boolean)
        Try
'             ' Update tab label if needed
'             If pManifestTabIndex >= 0 Then
'                 Dim lTabLabel As Widget = pNotebook.GetTabLabel(pManifestEditor)
'                 If lTabLabel IsNot Nothing AndAlso TypeOf lTabLabel Is Box Then
'                     ' FIXED: Call the overloaded version that takes Box and Boolean
'                     UpdateTabModifiedState(CType(lTabLabel, Box), vIsModified)
'                 End If
'             End If
            
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
                For i As Integer = 0 To pNotebook.NPages - 1
                    If pNotebook.GetNthPage(i) Is pManifestEditor Then
                        pNotebook.RemovePage(i)
                        Exit For
                    End If
                Next
                pOpenTabs.Remove(MANIFEST_TAB_KEY)

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

    ' Toggle manifest embedding
    Public Sub ToggleManifestEmbedding(vEmbed As Boolean)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then Return
            
            Dim lVersionManager As New AssemblyVersionManager(pCurrentProject, pSettingsManager)

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
        ''' <summary>
        ''' Updates the tab's modified indicator based on editor state
        ''' </summary>
        ''' <param name="vEditor">The editor whose state changed</param>
        Private Sub UpdateTabModifiedState(vEditor As IEditor)
            Try
                ' Find the tab containing this editor
                for each lTabEntry in pOpenTabs
                    Dim lTabInfo As TabInfo = lTabEntry.Value
                    If lTabInfo.Editor Is vEditor Then
                        ' Update the TabInfo modified state
                        lTabInfo.Modified = vEditor.IsModified
                        
                        ' Find the tab index in the notebook
                        Dim lTabIndex As Integer = -1
                        for i As Integer = 0 To pNotebook.NPages - 1
                            Dim lPage As Widget = pNotebook.GetNthPage(i)
                            If lPage Is lTabInfo.EditorContainer Then
                                lTabIndex = i
                                Exit for
                            End If
                        Next
                        
                        ' Update the CustomDrawNotebook tab modified state
                        If lTabIndex >= 0 Then
                            If TypeOf pNotebook Is CustomDrawNotebook Then
                                DirectCast(pNotebook, CustomDrawNotebook).SetTabModified(lTabIndex, vEditor.IsModified)
                            End If
                        End If
                        
                        ' Update window title if this is the current tab
                        If lTabIndex = pNotebook.CurrentPage Then
                            UpdateWindowTitle()
                        End If
                        
                        Exit for
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
            for each lChild in vTabLabel.Children
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
                    Exit for
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"UpdateTabModifiedState error: {ex.Message}")
        End Try
    End Sub

    
End Class
