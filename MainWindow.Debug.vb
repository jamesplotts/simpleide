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

    Public Sub StopDebugging()
        ' TODO: Imp lement
    End Sub

    ''' <summary>
    ''' Stops the running/debugging process (Shift+F5)
    ''' </summary>
    Private Sub OnStopDebugging(vSender As Object, vArgs As EventArgs)
        StopProject()
    End Sub


'     Private Sub OnStopDebugging(vSender As Object, vArgs As EventArgs)
'         Try
'             If pIsDebugging Then
'                 ' Stop the debug process
'                 If pDebugProcess IsNot Nothing AndAlso Not pDebugProcess.HasExited Then
'                     pDebugProcess.Kill()
'                     pDebugProcess = Nothing
'                 End If
'                 pIsDebugging = False
'                 
'                 ' Update UI
'                 UpdateDebugButtonStates()
'             End If
'         Catch ex As Exception
'             Console.WriteLine($"OnStopDebugging error: {ex.Message}")
'         End Try
'     End Sub
    
    ' Helper method to update debug button states
    Private Sub UpdateDebugButtonStates()
        Try
            ' TODO: Update toolbar button states based on debug state
        Catch ex As Exception
            Console.WriteLine($"UpdateDebugButtonStates error: {ex.Message}")
        End Try
    End Sub

    
End Class


