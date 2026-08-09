' MainWindow.InitializationEnhanced.vb
' Created: 2025-08-06 15:17:30

Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Widgets
Imports SimpleIDE.Utilities
Imports SimpleIDE.Syntax

' MainWindow.InitializationEnhanced.vb - Enhanced initialization with Object Explorer integration
Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Enhanced Initialization Methods =====
    
    ''' <summary>
    ''' Method to integrate Object Explorer with existing project loading
    ''' </summary>
    Private Sub OnProjectLoadedWithObjectExplorer()
        Try
            ' After project loads, ensure Object Explorer shows current file structure
            UpdateObjectExplorerForActiveTab()
            
            ' If multiple files are opened, Object Explorer should show the active one
            #If DEBUG Then
            Console.WriteLine("project loaded - Object Explorer updated")
            #End If
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectLoadedWithObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Debug method to verify Object Explorer integration
    ''' </summary>
    Public Sub DebugObjectExplorerIntegration()
        Try
            #If DEBUG Then
            Console.WriteLine("=== Object Explorer Integration Debug ===")
            #End If
            #If DEBUG Then
            Console.WriteLine($"Object Explorer initialized: {pObjectExplorer IsNot Nothing}")
            #End If
            #If DEBUG Then
            Console.WriteLine($"Open tabs Count: {pOpenTabs.Count}")
            #End If
            
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine($"current Editor: {lCurrentTab.Editor.GetType().Name}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"current file: {lCurrentTab.FilePath}")
                #End If
                
                Dim lStructure As SyntaxNode = lCurrentTab.Editor.GetDocumentStructure()
                #If DEBUG Then
                Console.WriteLine($"document structure available: {lStructure IsNot Nothing}")
                #End If
                If lStructure IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"Structure Children Count: {lStructure.Children.Count}")
                    #End If
                End If
            Else
                #If DEBUG Then
                Console.WriteLine("No active Editor")
                #End If
            End If
            
            #If DEBUG Then
            Console.WriteLine("==========================================")
            #End If
            
        Catch ex As Exception
            Console.WriteLine($"DebugObjectExplorerIntegration error: {ex.Message}")
        End Try
    End Sub
    
End Class
