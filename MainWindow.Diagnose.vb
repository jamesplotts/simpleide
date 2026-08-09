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
            #If DEBUG Then
            Console.WriteLine("========== NOTEBOOK DIAGNOSTICS ==========")
            #End If
            #If DEBUG Then
            Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            #End If
            #If DEBUG Then
            Console.WriteLine()
            #End If
            
            ' Main notebook (editor tabs)
            #If DEBUG Then
            Console.WriteLine("MAIN NOTEBOOK (Editor):")
            #End If
            If pNotebook IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine($"  Type: {pNotebook.GetType().Name}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Pages: {pNotebook.NPages}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Current Page: {pNotebook.CurrentPage}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Visible: {pNotebook.Visible}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Realized: {pNotebook.IsRealized}")
                #End If
                
                If TypeOf pNotebook Is CustomDrawNotebook Then
                    Dim lCustom As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                    for i As Integer = 0 To lCustom.NPages - 1
                        Dim lLabel As String = lCustom.GetTabLabel(i)
                        Dim lWidget As Widget = lCustom.GetNthPage(i)
                        #If DEBUG Then
                        Console.WriteLine($"  Tab {i}: '{lLabel}' - Widget Visible: {lWidget?.Visible}")
                        #End If
                    Next
                End If
            Else
                #If DEBUG Then
                Console.WriteLine("  Not INITIALIZED")
                #End If
            End If
            #If DEBUG Then
            Console.WriteLine()
            #End If
            
            ' Left notebook (project/object explorers)
            #If DEBUG Then
            Console.WriteLine("LEFT NOTEBOOK (Explorers):")
            #End If
            If pLeftNotebook IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine($"  Type: {pLeftNotebook.GetType().Name}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Pages: {pLeftNotebook.NPages}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Current Page: {pLeftNotebook.CurrentPage}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Visible: {pLeftNotebook.Visible}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Realized: {pLeftNotebook.IsRealized}")
                #End If
                
                If TypeOf pLeftNotebook Is CustomDrawNotebook Then
                    Dim lCustom As CustomDrawNotebook = DirectCast(pLeftNotebook, CustomDrawNotebook)
                    For i As Integer = 0 To lCustom.NPages - 1
                        Dim lLabel As String = lCustom.GetTabLabel(i)
                        Dim lWidget As Widget = lCustom.GetNthPage(i)
                        #If DEBUG Then
                        Console.WriteLine($"  Tab {i}: '{lLabel}' - Widget Visible: {lWidget?.Visible}")
                        #End If
                    Next
                End If
            Else
                #If DEBUG Then
                Console.WriteLine("  NOT INITIALIZED")
                #End If
            End If
            #If DEBUG Then
            Console.WriteLine()
            #End If
            
            ' Bottom panel notebook
            #If DEBUG Then
            Console.WriteLine("BOTTOM NOTEBOOK (Panels):")
            #End If
            If pBottomPanelManager IsNot Nothing Then
                Dim lBottomNotebook As Widget = pBottomPanelManager.GetNotebook()
                If lBottomNotebook IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"  Type: {lBottomNotebook.GetType().Name}")
                    #End If
                    
                    If TypeOf lBottomNotebook Is CustomDrawNotebook Then
                        Dim lCustom As CustomDrawNotebook = DirectCast(lBottomNotebook, CustomDrawNotebook)
                        #If DEBUG Then
                        Console.WriteLine($"  Pages: {lCustom.NPages}")
                        #End If
                        #If DEBUG Then
                        Console.WriteLine($"  Current Page: {lCustom.CurrentPage}")
                        #End If
                        #If DEBUG Then
                        Console.WriteLine($"  Visible: {lCustom.Visible}")
                        #End If
                        #If DEBUG Then
                        Console.WriteLine($"  Realized: {lCustom.IsRealized}")
                        #End If
                        
                        for i As Integer = 0 To lCustom.NPages - 1
                            Dim lLabel As String = lCustom.GetTabLabel(i)
                            Dim lWidget As Widget = lCustom.GetNthPage(i)
                            #If DEBUG Then
                            Console.WriteLine($"  Tab {i}: '{lLabel}' - Widget Visible: {lWidget?.Visible}")
                            #End If
                        Next
                    End If
                Else
                    #If DEBUG Then
                    Console.WriteLine("  Not INITIALIZED")
                    #End If
                End If
            Else
                #If DEBUG Then
                Console.WriteLine("  MANAGER Not INITIALIZED")
                #End If
            End If
            
            #If DEBUG Then
            Console.WriteLine()
            #End If
            #If DEBUG Then
            Console.WriteLine("========== End DIAGNOSTICS ==========")
            #End If
            
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
                #If DEBUG Then
                Console.WriteLine("F12 pressed - Running notebook diagnostics")
                #End If
                DiagnoseAllNotebooks()
                
                ' Also run EnsureNotebooksReady to attempt a fix
                #If DEBUG Then
                Console.WriteLine("Attempting To fix notebook visibility...")
                #End If
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
                #If DEBUG Then
                Console.WriteLine("AutoDiagnose: Left notebook is Nothing")
                #End If
                lNeedsFix = True
            ElseIf Not pLeftNotebook.Visible Then
                #If DEBUG Then
                Console.WriteLine("AutoDiagnose: Left notebook not visible")
                #End If
                lNeedsFix = True
            ElseIf pMainHPaned IsNot Nothing AndAlso pMainHPaned.Position < 50 Then
                #If DEBUG Then
                Console.WriteLine($"AutoDiagnose: HPaned position too small ({pMainHPaned.Position})")
                #End If
                lNeedsFix = True
            End If
            
            ' If issues detected, attempt automatic fix
            If lNeedsFix Then
                #If DEBUG Then
                Console.WriteLine("AutoDiagnose: Issues detected, attempting automatic fix...")
                #End If
                ForceShowLeftPanel()
            Else
                #If DEBUG Then
                Console.WriteLine("AutoDiagnose: Left panel appears OK")
                #End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"AutoDiagnoseOnStartup error: {ex.Message}")
        End Try
    End Sub    

End Class