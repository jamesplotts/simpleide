' MainWindow.Diagnose.vb - Diagnostic methods for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow

    ''' <summary>
    ''' Diagnostic method to check the state of all notebooks
    ''' </summary>
    Public Sub DiagnoseAllNotebooks()
        Try
            Console.WriteLine("========== NOTEBOOK DIAGNOSTICS ==========")
            Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            Console.WriteLine()
            
            ' Main notebook (editor tabs)
            Console.WriteLine("MAIN NOTEBOOK (Editor):")
            If pNotebook IsNot Nothing Then
                Console.WriteLine($"  Type: {pNotebook.GetType().Name}")
                Console.WriteLine($"  Pages: {pNotebook.NPages}")
                Console.WriteLine($"  Current Page: {pNotebook.CurrentPage}")
                Console.WriteLine($"  Visible: {pNotebook.Visible}")
                Console.WriteLine($"  Realized: {pNotebook.IsRealized}")
                
                If TypeOf pNotebook Is CustomDrawNotebook Then
                    Dim lCustom As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                    for i As Integer = 0 To lCustom.NPages - 1
                        Dim lLabel As String = lCustom.GetTabLabel(i)
                        Dim lWidget As Widget = lCustom.GetNthPage(i)
                        Console.WriteLine($"  Tab {i}: '{lLabel}' - Widget Visible: {lWidget?.Visible}")
                    Next
                End If
            Else
                Console.WriteLine("  Not INITIALIZED")
            End If
            Console.WriteLine()
            
            ' Left notebook (project/object explorers)
            Console.WriteLine("LEFT NOTEBOOK (Explorers):")
            If pLeftNotebook IsNot Nothing Then
                Console.WriteLine($"  Type: {pLeftNotebook.GetType().Name}")
                Console.WriteLine($"  Pages: {pLeftNotebook.NPages}")
                Console.WriteLine($"  Current Page: {pLeftNotebook.CurrentPage}")
                Console.WriteLine($"  Visible: {pLeftNotebook.Visible}")
                Console.WriteLine($"  Realized: {pLeftNotebook.IsRealized}")
                
                If TypeOf pLeftNotebook Is CustomDrawNotebook Then
                    Dim lCustom As CustomDrawNotebook = DirectCast(pLeftNotebook, CustomDrawNotebook)
                    For i As Integer = 0 To lCustom.NPages - 1
                        Dim lLabel As String = lCustom.GetTabLabel(i)
                        Dim lWidget As Widget = lCustom.GetNthPage(i)
                        Console.WriteLine($"  Tab {i}: '{lLabel}' - Widget Visible: {lWidget?.Visible}")
                    Next
                End If
            Else
                Console.WriteLine("  NOT INITIALIZED")
            End If
            Console.WriteLine()
            
            ' Bottom panel notebook
            Console.WriteLine("BOTTOM NOTEBOOK (Panels):")
            If pBottomPanelManager IsNot Nothing Then
                Dim lBottomNotebook As Widget = pBottomPanelManager.GetNotebook()
                If lBottomNotebook IsNot Nothing Then
                    Console.WriteLine($"  Type: {lBottomNotebook.GetType().Name}")
                    
                    If TypeOf lBottomNotebook Is CustomDrawNotebook Then
                        Dim lCustom As CustomDrawNotebook = DirectCast(lBottomNotebook, CustomDrawNotebook)
                        Console.WriteLine($"  Pages: {lCustom.NPages}")
                        Console.WriteLine($"  Current Page: {lCustom.CurrentPage}")
                        Console.WriteLine($"  Visible: {lCustom.Visible}")
                        Console.WriteLine($"  Realized: {lCustom.IsRealized}")
                        
                        for i As Integer = 0 To lCustom.NPages - 1
                            Dim lLabel As String = lCustom.GetTabLabel(i)
                            Dim lWidget As Widget = lCustom.GetNthPage(i)
                            Console.WriteLine($"  Tab {i}: '{lLabel}' - Widget Visible: {lWidget?.Visible}")
                        Next
                    End If
                Else
                    Console.WriteLine("  Not INITIALIZED")
                End If
            Else
                Console.WriteLine("  MANAGER Not INITIALIZED")
            End If
            
            Console.WriteLine()
            Console.WriteLine("========== End DIAGNOSTICS ==========")
            
        Catch ex As Exception
            Console.WriteLine($"DiagnoseAllNotebooks error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles F12 key press for diagnostics
    ''' </summary>
    Private Sub OnKeyPressForDiagnostics(vSender As Object, vArgs As KeyPressEventArgs)
        Try
            ' Check for F12 key
            If vArgs.Event.Key = Gdk.Key.F12 Then
                Console.WriteLine("F12 pressed - Running notebook diagnostics")
                DiagnoseAllNotebooks()
                
                ' Also run EnsureNotebooksReady to attempt a fix
                Console.WriteLine("Attempting To fix notebook visibility...")
                EnsureNotebooksReady()
                
                vArgs.RetVal = True ' Mark as handled
            End If
        Catch ex As Exception
            Console.WriteLine($"OnKeyPressForDiagnostics error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Automatically diagnose and fix left panel issues on startup if needed
    ''' </summary>
    Private Sub AutoDiagnoseOnStartup()
        Try
            ' Check if left panel is properly visible
            Dim lNeedsFix As Boolean = False
            
            ' Check various conditions that indicate a problem
            If pLeftNotebook Is Nothing Then
                Console.WriteLine("AutoDiagnose: Left notebook is Nothing")
                lNeedsFix = True
            ElseIf Not pLeftNotebook.Visible Then
                Console.WriteLine("AutoDiagnose: Left notebook not visible")
                lNeedsFix = True
            ElseIf pMainHPaned IsNot Nothing AndAlso pMainHPaned.Position < 50 Then
                Console.WriteLine($"AutoDiagnose: HPaned position too small ({pMainHPaned.Position})")
                lNeedsFix = True
            End If
            
            ' If issues detected, attempt automatic fix
            If lNeedsFix Then
                Console.WriteLine("AutoDiagnose: Issues detected, attempting automatic fix...")
                ForceShowLeftPanel()
            Else
                Console.WriteLine("AutoDiagnose: Left panel appears OK")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"AutoDiagnoseOnStartup error: {ex.Message}")
        End Try
    End Sub    

End Class