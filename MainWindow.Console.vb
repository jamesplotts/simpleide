' MainWindow.Console.vb
' Created: 2025-08-04 08:05:40

Imports Gtk
Imports System



Partial Public Class MainWindow
    
    ' Clear csonsole using BottomPanelManager


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

    ''' <summary>
    ''' Shows an error to the user via a modal dialog, and also logs it to the console panel
    ''' </summary>
    ''' <remarks>
    ''' This is the only 2-argument ShowError overload in the project and is called from
    ''' ~190 sites across the MainWindow.*.vb partial files for user-facing error conditions
    ''' (e.g. "No project is currently open"). It used to only call ConsoleLineOut, so every
    ''' one of those calls was silently swallowed whenever the bottom panel was hidden or a
    ''' different bottom-panel tab was active - the user would click something, nothing
    ''' visible would happen, and the actual error text was sitting unseen in the console
    ''' buffer. A modal dialog is what every one of those call sites' message text ("Please
    ''' open a project first", etc.) is actually written for.
    ''' </remarks>
    Public Sub ShowError(vCaption As String, vMessage As String)
        ConsoleLineOut(vCaption + ": " + vMessage)
        Try
            ' Marshal to the GTK main thread - this project installs no SynchronizationContext,
            ' so Await continuations in Async Function callers (e.g. MainWindow.Run.vb's
            ' RunProject/RunProjectAsync Catch blocks) resume on a thread-pool thread, and a
            ' MessageDialog can only be safely created/Run/Destroyed on the thread owning the
            ' GTK main loop
            Application.Invoke(Sub()
                Try
                    Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, vMessage)
                    lDialog.Title = vCaption
                    lDialog.Run()
                    lDialog.Destroy()
                Catch ex As Exception
                    Console.WriteLine($"ShowError dialog error: {ex.Message}")
                End Try
            End Sub)
        Catch ex As Exception
            Console.WriteLine($"ShowError error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Shows an informational message to the user via a modal dialog, and also logs it to
    ''' the console panel - see ShowError's remarks for why this can't just log to console
    ''' </summary>
    Public Sub ShowInfo(vCaption As String, vMessage As String)
        ConsoleLineOut(vCaption + ": " + vMessage)
        Try
            ' See ShowError's remarks on why this must marshal to the GTK main thread
            Application.Invoke(Sub()
                Try
                    Dim lDialog As New MessageDialog(Me, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, vMessage)
                    lDialog.Title = vCaption
                    lDialog.Run()
                    lDialog.Destroy()
                Catch ex As Exception
                    Console.WriteLine($"ShowInfo dialog error: {ex.Message}")
                End Try
            End Sub)
        Catch ex As Exception
            Console.WriteLine($"ShowInfo error: {ex.Message}")
        End Try
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


