' MainWindow.Console.vb
' Created: 2025-08-04 08:05:40

Imports Gtk
Imports System



Partial Public Class MainWindow
    

    ' Clear console using BottomPanelManager
    Public Sub ClearConsole()
        Try
            pBottomPanelManager?.ClearConsole()
        Catch ex As Exception
            Console.WriteLine($"ClearConsole error: {ex.Message}")
        End Try
    End Sub
    
    ' Append to console using BottomPanelManager
    Public Sub ConsoleLineOut(vText As String)
        Try
            pBottomPanelManager?.AppendToConsole(vText + vbCrLf)
        Catch ex As Exception
            Console.WriteLine($"AppendToConsole error: {ex.Message}")
        End Try
    End Sub
    
    Private ReadOnly Property pOutputTextView As TextView
        Get
            Return pBottomPanelManager?.ConsoleTextView
        End Get
    End Property

    Public Sub ShowError(vCaption As String, vMessage As String)
        ' TODO: Add formatting for coloring
        ConsoleLineOut(vCaption + ": " + vMessage)
    End Sub

    Public Sub ShowInfo(vCaption As String, vMessage As String)
        ' TODO: Add formatting for coloring
        ConsoleLineOut(vCaption + ": " + vMessage)
    End Sub

    Private Sub ShowTerminalPanel()
        Try
            If pBottomPanelManager IsNot Nothing Then
                pBottomPanelManager.ShowConsole()
            End If
        Catch ex As Exception
            Console.WriteLine($"ShowTerminalPanel error: {ex.Message}")
        End Try
    End Sub

End Class


