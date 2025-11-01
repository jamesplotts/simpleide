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

    ''' <summary>
    ''' Exports the syntax tree diagnostic information to a file
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Event arguments</param>
    ''' <remarks>
    ''' Can be triggered via menu item or keyboard shortcut (e.g., Ctrl+Shift+D)
    ''' </remarks>
    Public Sub OnExportSyntaxTreeDiagnostic(vSender As Object, vArgs As EventArgs)
        Try
            Console.WriteLine("OnExportSyntaxTreeDiagnostic: Starting diagnostic export")
            
            ' Check if project is loaded
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                ShowInfo("No Project", "Please open a project before exporting diagnostics.")
                Return
            End If
            
            ' Update status bar
            UpdateStatusBar("Exporting syntax tree diagnostic...")
            
            ' Export the diagnostic
            Dim lSuccess As Boolean = pProjectManager.ExportSyntaxTreeDiagnostic()
            
            If lSuccess Then
                ' Get the file path for the message
                Dim lFilePath As String = System.IO.Path.Combine(
                    pProjectManager.CurrentProjectInfo.ProjectDirectory, 
                    "syntaxtreestructure.txt"
                )
                
                UpdateStatusBar($"Diagnostic exported to syntaxtreestructure.txt")
                
                ' Show success message with option to open the file
                Dim lMessage As String = $"Syntax tree diagnostic has been exported to:{Environment.NewLine}{Environment.NewLine}" &
                                        $"{lFilePath}{Environment.NewLine}{Environment.NewLine}" &
                                        "You can now review this file to diagnose syntax tree issues."
                
                ShowInfo("Diagnostic Export Complete", lMessage)
                
                Console.WriteLine($"OnExportSyntaxTreeDiagnostic: Successfully exported to {lFilePath}")
            Else
                UpdateStatusBar("Failed to export diagnostic")
                ShowError("Export Failed", "Failed to export syntax tree diagnostic. Check console for details.")
                Console.WriteLine("OnExportSyntaxTreeDiagnostic: Export failed")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnExportSyntaxTreeDiagnostic error: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
            UpdateStatusBar("Error exporting diagnostic")
            ShowError("Export Error", $"Error exporting diagnostic: {ex.Message}")
        End Try
    End Sub
    
End Class


