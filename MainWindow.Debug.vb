' MainWindow.Debug.vb - Build system integration for MainWindow
Imports Gtk
Imports Gdk
Imports System
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Dialogs

Partial Public Class MainWindow

    ''' <summary>
    ''' Stops the running/debugging process (Shift+F5)
    ''' </summary>
    Private Sub OnStopDebugging(vSender As Object, vArgs As EventArgs)
        Try
            StopProject()
        Catch ex As Exception
            Console.WriteLine($"OnStopDebugging error: {ex.Message}")
            ShowError("Stop error", ex.Message)
        End Try
    End Sub

End Class


