' MainWindow.ProjectExplorerIntegration.vb - Integration between ProjectExplorer and ProjectManager
' Created: 2025-08-21
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Widgets

' MainWindow.ProjectExplorerIntegration.vb
' Created: 2025-08-21 06:15:32

Partial Public Class MainWindow
    
    ''' <summary>
    ''' Initializes the Project Explorer with ProjectManager integration
    ''' </summary>
    Private Sub InitializeProjectExplorerWithManager()
        Try
            ' Ensure both components exist
            If pProjectExplorer Is Nothing OrElse pProjectManager Is Nothing Then
                Console.WriteLine("ProjectExplorer or ProjectManager not initialized")
                Return
            End If
            
            ' Set the project manager in the project explorer
            pProjectExplorer.SetProjectManager(pProjectManager)
            
            ' Wire up ProjectManager events to refresh Project Explorer
            AddHandler pProjectManager.FileAdded, AddressOf OnProjectManagerFileAdded
            AddHandler pProjectManager.FileRemoved, AddressOf OnProjectManagerFileRemoved
            AddHandler pProjectManager.FileRenamed, AddressOf OnProjectManagerFileRenamed
            AddHandler pProjectManager.ProjectModified, AddressOf OnProjectManagerModified
            
            Console.WriteLine("ProjectExplorer integrated with ProjectManager")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeProjectExplorerWithManager error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles file added event from ProjectManager
    ''' </summary>
    ''' <param name="vFilePath">Path of added file</param>
    Private Sub OnProjectManagerFileAdded(vFilePath As String)
        Try
            Console.WriteLine($"File added to project: {vFilePath}")
            
            ' Refresh the project explorer
            If pProjectExplorer IsNot Nothing AndAlso pProjectManager IsNot Nothing Then
                pProjectManager.LoadProject(pProjectManager.CurrentProjectPath)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerFileAdded error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles file removed event from ProjectManager
    ''' </summary>
    ''' <param name="vFilePath">Path of removed file</param>
    Private Sub OnProjectManagerFileRemoved(vFilePath As String)
        Try
            Console.WriteLine($"File removed from project: {vFilePath}")
            
            ' Refresh the project explorer
            If pProjectExplorer IsNot Nothing AndAlso pProjectManager IsNot Nothing Then
                pProjectManager.LoadProject(pProjectManager.CurrentProjectPath)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerFileRemoved error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles file renamed event from ProjectManager
    ''' </summary>
    ''' <param name="vOldPath">Old file path</param>
    ''' <param name="vNewPath">New file path</param>
    Private Sub OnProjectManagerFileRenamed(vOldPath As String, vNewPath As String)
        Try
            Console.WriteLine($"File renamed: {vOldPath} -> {vNewPath}")
            
            ' Refresh the project explorer
            If pProjectExplorer IsNot Nothing AndAlso pProjectManager IsNot Nothing Then
                pProjectManager.LoadProject(pProjectManager.CurrentProjectPath)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerFileRenamed error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles project modified event from ProjectManager
    ''' </summary>
    Private Sub OnProjectManagerModified()
        Try
            Console.WriteLine("Project structure modified")
            
            ' Update title bar to show modified state
            UpdateWindowTitle()
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerModified error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Call this from your existing InitializeMainWindow method
    ''' </summary>
    ''' <remarks>
    ''' Add this line after both pProjectExplorer and pProjectManager are created:
    ''' InitializeProjectExplorerWithManager()
    ''' </remarks>
    Private Sub EnsureProjectExplorerIntegration()
        ' This should be called from InitializeMainWindow after creating components
        InitializeProjectExplorerWithManager()
    End Sub
    
End Class
