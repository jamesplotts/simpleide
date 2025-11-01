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
    
    ''' <summary>
    ''' Handles file selection from Project Explorer
    ''' </summary>
    ''' <param name="vFilePath">Full path to the file to open</param>
    Private Sub OnProjectExplorerFileSelected(vFilePath As String)
        Try
            Console.WriteLine($"OnProjectExplorerFileSelected: {vFilePath}")
            
            If String.IsNullOrEmpty(vFilePath) Then 
                Console.WriteLine("OnProjectExplorerFileSelected: Empty file path")
                Return
            End If
            
            ' Check if file exists
            If Not System.IO.File.Exists(vFilePath) Then
                Console.WriteLine($"OnProjectExplorerFileSelected: File not found - {vFilePath}")
                ShowError("File Not Found", $"The file '{vFilePath}' does not exist.")
                Return
            End If
            
            ' Use the standard OpenFile method which handles everything
            ' including checking if file is already open, creating tabs, etc.
            OpenFile(vFilePath)
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectExplorerFileSelected error: {ex.Message}")
            ShowError("Open File Error", $"Failed to open file: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Add Refresh Project Menu Handler =====
    ' Add this as a menu item handler
    
    Private Sub OnRefreshProjectStructure(vSender As Object, vArgs As EventArgs)
        Try
            RefreshProjectStructure()
            
        Catch ex As Exception
            Console.WriteLine($"OnRefreshProjectStructure error: {ex.Message}")
        End Try
    End Sub
    
    
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
