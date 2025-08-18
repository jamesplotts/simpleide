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
    ''' Enhanced MainWindow initialization that includes Object Explorer integration
    ''' Call this instead of or after existing initialization
    ''' </summary>
    Public Sub InitializeWithObjectExplorerIntegration()
        Try
            ' Ensure Object Explorer is properly set up
            EnsureObjectExplorerIntegration()
            
            ' Set up initial Object Explorer state
            UpdateObjectExplorerForActiveTab()
            
            ' Hook up enhanced event handlers for notebook
            If pNotebook IsNot Nothing Then
                RemoveHandler pNotebook.SwitchPage, AddressOf OnNotebookSwitchPage
                AddHandler pNotebook.SwitchPage, AddressOf OnNotebookSwitchPageEnhanced
            End If
            
            Console.WriteLine("MainWindow Object Explorer integration initialized")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeWithObjectExplorerIntegration error: {ex.Message}")
        End Try
    End Sub

    ' Enhanced notebook switch page handler with Object Explorer integration
    Private Sub OnNotebookSwitchPageEnhanced(vSender As Object, vArgs As SwitchPageArgs)
        Try
            ' Call existing switch page logic
            OnNotebookSwitchPage(vSender, vArgs)
            
            ' Update Object Explorer for new active editor
            UpdateObjectExplorerForActiveTab()
            
            ' Update Object Explorer toolbar state
            UpdateObjectExplorerToolbarState()
            
        Catch ex As Exception
            Console.WriteLine($"OnNotebookSwitchPageEnhanced error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Ensure Object Explorer is properly integrated
    ''' </summary>
    Private Sub EnsureObjectExplorerIntegration()
        Try
            If pObjectExplorer Is Nothing Then
                Console.WriteLine("Warning: Object Explorer not initialized")
                Return
            End If
            
            ' Set up refresh button reference in Object Explorer
            ' This assumes Object Explorer has a SetupRefreshButton method
            ' pObjectExplorer.SetupRefreshButton()
            
            ' REMOVED: Don't clear structure on initialization!
            ' This was causing the Object Explorer to lose its content
            ' pObjectExplorer.ClearStructure()
            
            Console.WriteLine("EnsureObjectExplorerIntegration: Completed without clearing structure")
            
        Catch ex As Exception
            Console.WriteLine($"EnsureObjectExplorerIntegration error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced file opening that includes Object Explorer setup
    ''' </summary>
    Private Sub OpenFileWithObjectExplorerIntegration(vFilePath As String)
        Try
            ' Call existing OpenFile method
            ' (This should be the existing OpenFile implementation)
            ' OpenFile(vFilePath)
            
            ' After file is opened, ensure Object Explorer is set up
            UpdateObjectExplorerForActiveTab()
            
        Catch ex As Exception
            Console.WriteLine($"OpenFileWithObjectExplorerIntegration error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced tab creation that includes Object Explorer setup
    ''' </summary>
    Private Function CreateTabWithObjectExplorerIntegration(vFilePath As String, vEditor As IEditor) As Boolean
        Try
            ' This should integrate with existing tab creation logic
            ' The key is to call SetupEditorForObjectExplorer after tab creation
            
            If vEditor Is Nothing Then Return False
            
            ' Hook up events with Object Explorer integration
            HookupEditorEventsForObjectExplorer(vEditor)
            
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"CreateTabWithObjectExplorerIntegration error: {ex.Message}")
            Return False
        End Try
    End Function

    ' Method to replace existing event handler hookups
    Private Sub HookupEditorEventsForObjectExplorer(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Hook up standard editor events
            AddHandler vEditor.Modified, AddressOf OnEditorModified
            AddHandler vEditor.CursorPositionChanged, AddressOf OnEditorCursorPositionChanged
            AddHandler vEditor.SelectionChanged, AddressOf OnEditorSelectionChanged
            AddHandler vEditor.TextChanged, AddressOf OnEditorTextChangedEnhanced
            AddHandler vEditor.UndoRedoStateChanged, AddressOf OnEditorUndoRedoStateChanged
            
            ' Hook up Object Explorer-specific events
            SetupEditorForObjectExplorer(vEditor)
            
        Catch ex As Exception
            Console.WriteLine($"HookupEditorEventsForObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced tab closing that properly unhooks Object Explorer
    ''' </summary>
    Private Sub CloseTabWithObjectExplorerIntegration(vTabId As String)
        Try
            If pOpenTabs.ContainsKey(vTabId) Then
                Dim lTabInfo As TabInfo = pOpenTabs(vTabId)
                
                ' Unhook Object Explorer events
                If lTabInfo.Editor IsNot Nothing Then
                    UnhookEditorEventsForObjectExplorer(lTabInfo.Editor)
                End If
                
                ' Continue with existing close tab logic
                ' CloseTab(vTabId)
                
                ' Update Object Explorer for new active tab
                UpdateObjectExplorerForActiveTab()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"CloseTabWithObjectExplorerIntegration error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Method to integrate Object Explorer with existing project loading
    ''' </summary>
    Private Sub OnProjectLoadedWithObjectExplorer()
        Try
            ' After project loads, ensure Object Explorer shows current file structure
            UpdateObjectExplorerForActiveTab()
            
            ' If multiple files are opened, Object Explorer should show the active one
            Console.WriteLine("project loaded - Object Explorer updated")
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectLoadedWithObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Debug method to verify Object Explorer integration
    ''' </summary>
    Public Sub DebugObjectExplorerIntegration()
        Try
            Console.WriteLine("=== Object Explorer Integration Debug ===")
            Console.WriteLine($"Object Explorer initialized: {pObjectExplorer IsNot Nothing}")
            Console.WriteLine($"Open tabs Count: {pOpenTabs.Count}")
            
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                Console.WriteLine($"current Editor: {lCurrentTab.Editor.GetType().Name}")
                Console.WriteLine($"current file: {lCurrentTab.FilePath}")
                
                Dim lStructure As SyntaxNode = lCurrentTab.Editor.GetDocumentStructure()
                Console.WriteLine($"document structure available: {lStructure IsNot Nothing}")
                If lStructure IsNot Nothing Then
                    Console.WriteLine($"Structure Children Count: {lStructure.Children.Count}")
                End If
            Else
                Console.WriteLine("No active Editor")
            End If
            
            Console.WriteLine("==========================================")
            
        Catch ex As Exception
            Console.WriteLine($"DebugObjectExplorerIntegration error: {ex.Message}")
        End Try
    End Sub
    
End Class
