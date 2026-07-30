' Widgets/GitPanel.RefreshStatus.vb 
' Add RefreshStatus method to GitPanel
' This partial class adds the missing RefreshStatus method to GitPanel

Imports Gtk
Imports System

Namespace Widgets
    
    Partial Public Class GitPanel
        
        ' Public method to refresh git status
        Public Sub RefreshStatus()
            Try
                ' Call the existing RefreshGitStatus method
                RefreshGitStatus()
            Catch ex As Exception
                Console.WriteLine($"RefreshStatus error: {ex.Message}")
            End Try
        End Sub
        
        ' Alias for SetProjectRoot to match BottomPanelManager interface
        Public Sub SetProjectRoot(vPath As String)
            Try
                ' Set the ProjectRoot property which will trigger refresh
                ProjectRoot = vPath
            Catch ex As Exception
                Console.WriteLine($"SetProjectRoot error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace