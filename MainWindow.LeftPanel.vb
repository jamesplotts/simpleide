' MainWindow.LeftPanel.vb
' Created: 2025-08-04 22:27:38
' MainWindow.LeftPanel.vb - Left panel management with Notebook for ProjectExplorer and ObjectExplorer
Imports Gtk
Imports System
Imports SimpleIDE.Widgets
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Syntax
Imports SimpleIDE.Models


Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Private Fields =====
    Private pLeftNotebook As Notebook
    Private pObjectExplorer As CustomDrawObjectExplorer
    
    ' ===== Left Panel Initialization =====
    
    Private Sub InitializeLeftPanel()
        Try
            ' Create the notebook for the left panel
            pLeftNotebook = New Notebook()
            pLeftNotebook.TabPos = PositionType.Top
            pLeftNotebook.Scrollable = False
            
            ' Add Project Explorer tab
            If pProjectExplorer IsNot Nothing Then
                Dim lProjectLabel As New Label("Project")
                pLeftNotebook.AppendPage(pProjectExplorer, lProjectLabel)
            End If
            
            ' Create and add Object Explorer tab
            pObjectExplorer = New CustomDrawObjectExplorer(pSettingsManager)
            AddHandler pObjectExplorer.NodeDoubleClicked, AddressOf OnObjectExplorerNodeDoubleClicked
            AddHandler pObjectExplorer.CloseRequested, AddressOf OnObjectExplorerCloseRequested
            ' Add handler for page switches
            AddHandler pLeftNotebook.SwitchPage, AddressOf OnLeftNotebookPageChanged
            
            Dim lObjectLabel As New Label("Objects")
            pLeftNotebook.AppendPage(pObjectExplorer, lObjectLabel)
            
            ' Pack the notebook into the left pane
            pMainHPaned.Pack1(pLeftNotebook, False, False)
            
        Catch ex As Exception
            Console.WriteLine($"InitializeLeftPanel error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Object Explorer Event Handlers =====
    
    Private Sub OnObjectExplorerNodeDoubleClicked(vNode As SyntaxNode)
        Try
            If vNode Is Nothing Then Return
            
            ' Get the current editor
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab Is Nothing OrElse lCurrentTab.Editor Is Nothing Then Return
            
            ' Navigate to the node's location
            If vNode.StartLine >= 0 Then
                lCurrentTab.Editor.GoToPosition(vNode.StartLine + 1, vNode.StartColumn + 1)
                lCurrentTab.Editor.EnsureCursorVisible()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnObjectExplorerNodeDoubleClicked error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnObjectExplorerCloseRequested()
        Try
            ' Hide the Object Explorer by switching to Project Explorer tab
            If pLeftNotebook IsNot Nothing AndAlso pLeftNotebook.NPages > 0 Then
                pLeftNotebook.Page = 0  ' Switch to project Explorer
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnObjectExplorerCloseRequested error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Editor Focus Changed Handler Update =====
    
    Private Sub OnEditorFocusChanged(vSender As Object, vArgs As EventArgs)
        Try
            ' Get the focused editor
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return
            
            ' Update the object explorer with the current editor
            If pObjectExplorer IsNot Nothing Then
                pObjectExplorer.SetCurrentEditor(lEditor)
            End If
            
            ' Update status bar
            UpdateStatusBar()
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorFocusChanged error: {ex.Message}")
        End Try
    End Sub
    


End Class
