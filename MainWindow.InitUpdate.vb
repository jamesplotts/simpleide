' MainWindow.InitUpdate.vb - Updates to MainWindow initialization for enhanced project loading
' This file shows the modifications needed to existing MainWindow methods

Imports Gtk
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Models

' MainWindow.InitUpdate.vb
' Created: 2025-08-06 17:29:52


Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Modified OpenProject Method =====
    ' Replace the existing OpenProject method with this enhanced version
    
    Private Sub OpenProject(vProjectPath As String)
        Try
            ' Validate project file
            If Not System.IO.File.Exists(vProjectPath) Then
                ShowError("", $"project file not found: {vProjectPath}")
                Return
            End If
            
            ' Close current project if open
            If pProjectManager.IsProjectOpen Then
                ' Ask to save changes if needed
                If Not CheckUnsavedChanges() Then Return
                pProjectManager.CloseProject()
            End If
            
            ' Use enhanced loading with full parsing
            LoadProjectEnhanced(vProjectPath)
            
        Catch ex As Exception
            Console.WriteLine($"OpenProject error: {ex.Message}")
            ShowError("", $"error opening project: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Modified OnProjectExplorerFileSelected =====
    ' Update the file selection handler to use enhanced opening
    
    Private Sub OnProjectExplorerFileSelected(vFilePath As String)
        Try
            If String.IsNullOrEmpty(vFilePath) Then Return
            
            ' Use enhanced file opening that integrates with project structure
            OpenFileWithProjectIntegration(vFilePath)
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectExplorerFileSelected error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Add to InitializeMainWindow =====
    ' Add this to the end of your existing InitializeMainWindow method
    
'     Private Sub InitializeMainWindowAdditions()
'         Try
'             ' Initialize enhanced project integration
'             InitializeProjectIntegration()
'             
'             ' Replace notebook switch handler with enhanced version
'             If pNotebook IsNot Nothing Then
'                 RemoveHandler pNotebook.SwitchPage, AddressOf OnNotebookSwitchPage
'                 AddHandler pNotebook.SwitchPage, AddressOf OnNotebookSwitchPageWithProjectIntegration
'             End If
'             
'             ' Add refresh project structure menu item or toolbar button
'             ' This would be added to your existing menu/toolbar setup
'             
'         Catch ex As Exception
'             Console.WriteLine($"InitializeMainWindowAdditions error: {ex.Message}")
'         End Try
'     End Sub
    
    ' ===== Add Refresh Project Menu Handler =====
    ' Add this as a menu item handler
    
    Private Sub OnRefreshProjectStructure(vSender As Object, vArgs As EventArgs)
        Try
            RefreshProjectStructure()
            
        Catch ex As Exception
            Console.WriteLine($"OnRefreshProjectStructure error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Modified CreateTab Method =====
    ' Update the existing CreateTab method to register with project manager
    
    Private Function CreateTabEnhanced(vFilePath As String, vContent As String, vIsNew As Boolean) As TabInfo
        Try
            ' Call existing CreateTab implementation
            Dim lTab As TabInfo = CreateTabEnhanced(vFilePath, vContent, vIsNew)
            
            If Not lTab Is Nothing AndAlso Not lTab.Editor Is Nothing Then
                ' Register with project manager if project is open
                If pProjectManager.IsProjectOpen AndAlso Not String.IsNullOrEmpty(vFilePath) Then
                    ' Update project file structure
                    pProjectManager.UpdateFileStructure(vFilePath, lTab.Editor)
                End If
                
                ' Hook up document parsed event
                AddHandler lTab.Editor.DocumentParsed, AddressOf OnEditorDocumentParsed
                
                ' Hook up content changed for project updates
                AddHandler lTab.Editor.TextChanged, AddressOf OnEditorContentChangedWithProjectUpdate
            End If
            
            Return lTab
            
        Catch ex As Exception
            Console.WriteLine($"CreateTabEnhanced error: {ex.Message}")
            Return Nothing
        End Try
    End Function
    
    ''' <summary>
    ''' Handles editor content changes with project structure update
    ''' </summary>
    ''' <param name="vSender">The editor that changed</param>
    ''' <param name="vArgs">Event arguments</param>
    ''' <remarks>
    ''' Updates the project structure when editor content changes
    ''' </remarks>
    Private Sub OnEditorContentChangedWithProjectUpdate(vSender As Object, vArgs As EventArgs)
        Try
            ' Get the editor that changed
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return
            
            ' Find the tab for this editor
            Dim lTab As TabInfo = Nothing
            for each lT in pOpenTabs
                If lT.Value.Editor Is lEditor Then
                    lTab = lT.Value
                    Exit for
                End If
            Next
            
            If lTab Is Nothing OrElse String.IsNullOrEmpty(lTab.FilePath) Then Return
            
            ' If project is open, update the file in the project manager
            If pProjectManager.IsProjectOpen Then
                ' Get or create SourceFileInfo for this file
                Dim lSourceFileInfo As SourceFileInfo = pProjectManager.GetSourceFileInfo(lTab.FilePath)
                
                If lSourceFileInfo IsNot Nothing Then
                    ' Update the content in SourceFileInfo
                    lSourceFileInfo.SetAllText(lEditor.Text)
                    
                    ' Request async parse which will update the project structure
                    lSourceFileInfo.RequestAsyncParse()
                    
                    'Console.WriteLine($"OnEditorContentChangedWithProjectUpdate: Updated {lTab.FileName}")
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorContentChangedWithProjectUpdate error: {ex.Message}")
        End Try
    End Sub
    
End Class
