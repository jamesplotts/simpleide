' MainWindow.ObjectExplorerIntegration.vb - Complete Object Explorer integration
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Widgets
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Object Explorer Integration Methods =====
    
    ' Object Explorer related fields
    'Private pObjectExplorer As ObjectExplorer
    Private pObjectExplorerScrolled As ScrolledWindow

    ' Store reference to root node for Object Explorer
    Private pRootNode As SyntaxNode

    ''' <summary>
    ''' Set up Object Explorer with current active editor
    ''' </summary>
    Private Sub SetupObjectExplorerForEditor(vEditor As IEditor)
        Try
            If pObjectExplorer Is Nothing OrElse vEditor Is Nothing Then Return
            
            ' Set the current editor in Object Explorer
            pObjectExplorer.SetCurrentEditor(vEditor)
            
            ' Hook up the DocumentParsed event if not already connected
            RemoveHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            AddHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            
            ' Get initial document structure if available
            Dim lStructure As SyntaxNode = vEditor.GetDocumentStructure()
            If lStructure IsNot Nothing Then
                pObjectExplorer.UpdateStructure(lStructure)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SetupObjectExplorerForEditor error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle document parsing completion from any editor
    ''' </summary>
    Private Sub OnEditorDocumentParsed(vRootNode As SyntaxNode)
        Try
            If pObjectExplorer Is Nothing OrElse vRootNode Is Nothing Then Return
            
            ' Update Object Explorer with new structure
            pObjectExplorer.UpdateStructure(vRootNode)
            
            ' Debug output
            Console.WriteLine($"Object Explorer updated with structure: {vRootNode.Children.Count} root nodes")
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorDocumentParsed error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Update Object Explorer when tab changes
    ''' </summary>
    Private Sub UpdateObjectExplorerForActiveTab()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                SetupObjectExplorerForEditor(lCurrentTab.Editor)
            Else
                ' Clear Object Explorer if no active editor
                If pObjectExplorer IsNot Nothing Then
                    pObjectExplorer.ClearStructure()
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateObjectExplorerForActiveTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Set up editor for Object Explorer integration
    ''' </summary>
    Private Sub SetupEditorForObjectExplorer(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Set up Object Explorer integration - FIXED: Call SetupObjectExplorerForEditor instead of recursive call
            SetupObjectExplorerForEditor(vEditor)
            
            ' If this is a CustomDrawingEditor, trigger initial parsing
            Dim lCustomEditor As CustomDrawingEditor = TryCast(vEditor, CustomDrawingEditor)
            If lCustomEditor IsNot Nothing Then
                ' Request initial parse if content exists
                lCustomEditor.RefreshSyntaxHighlighting()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SetupEditorForObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Object Explorer toolbar integration
    ''' </summary>
    Private Sub UpdateObjectExplorerToolbarState()
        Try
            If pObjectExplorer Is Nothing Then Return
            
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            Dim lHasActiveEditor As Boolean = (lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing)
            
            ' Enable/disable Object Explorer refresh button based on active editor
            pObjectExplorer.SetRefreshEnabled(lHasActiveEditor)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateObjectExplorerToolbarState error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Initialize Object Explorer 
    ''' </summary>
    Private Sub InitializeObjectExplorer()
        Try
            ' Create Object Explorer
            pObjectExplorer = New CustomDrawObjectExplorer(pSettingsManager)
            
            ' Initialize with project manager if available
            If pProjectManager IsNot Nothing Then
                pObjectExplorer.InitializeWithProjectManager(pProjectManager)
            End If
            
            ' Create scrolled window for Object Explorer
            pObjectExplorerScrolled = New ScrolledWindow()
            pObjectExplorerScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            pObjectExplorerScrolled.Add(pObjectExplorer)
            
            ' Add event handlers
            AddHandler pObjectExplorer.NodeActivated, AddressOf OnObjectExplorerNodeActivated
            AddHandler pObjectExplorer.NodeSelected, AddressOf OnObjectExplorerNodeSelected
            
            Console.WriteLine("Object Explorer initialized")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle file opened from Project Explorer
    ''' </summary>
    Private Sub OnFileOpenedFromProjectExplorer(vEditor As IEditor)
        Try
            If pObjectExplorer Is Nothing OrElse vEditor Is Nothing Then Return
            
            Console.WriteLine("File opened from Project Explorer")
            
            ' Set the current editor
            pObjectExplorer.SetCurrentEditor(vEditor)
            
        Catch ex As Exception
            Console.WriteLine($"OnFileOpenedFromProjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle notebook page switch
    ''' </summary>
    Private Sub OnNotebookSwitchPageFixed(vSender As Object, vArgs As SwitchPageArgs)
        Try
            ' Call existing switch page logic
            OnNotebookSwitchPage(vSender, vArgs)
            
            ' Get the current tab
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                ' Update current editor
                If pObjectExplorer IsNot Nothing Then
                    pObjectExplorer.SetCurrentEditor(lCurrentTab.Editor)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnNotebookSwitchPageFixed error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Parse all project files and build complete project structure
    ''' </summary>
    Public Sub ParseProjectStructure(vProjectPath As String)
        Try
            Console.WriteLine($"ParseProjectStructure: {vProjectPath}")

            pProjectManager.LoadProjectWithParsing(vProjectPath)
            
            ' Get the combined project structure
            Dim lProjectStructure As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
            
            If lProjectStructure IsNot Nothing Then
                Console.WriteLine($"  Project has {lProjectStructure.Children.Count} top-level nodes")
                
                ' Update Object Explorer with complete project structure
                If pObjectExplorer IsNot Nothing Then
                    pObjectExplorer.UpdateStructure(lProjectStructure)
                End If
            Else
                Console.WriteLine("  No project structure available")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ParseProjectStructure error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle node activation in Object Explorer (double-click)
    ''' </summary>
    Private Sub OnObjectExplorerNodeActivated(vNode As SyntaxNode)
        Try
            If vNode Is Nothing Then Return
            
            Console.WriteLine($"Object Explorer node activated: {vNode.Name} at line {vNode.StartLine}")
            
            ' Navigate to the node location in the current editor
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                ' Move cursor to the node's line
                lCurrentTab.Editor.GoToLine(vNode.StartLine)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnObjectExplorerNodeActivated error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle node selection in Object Explorer (single-click)
    ''' </summary>
    Private Sub OnObjectExplorerNodeSelected(vNode As SyntaxNode)
        Try
            If vNode Is Nothing Then Return
            
            ' Could highlight the node in editor or show info in status bar
            Console.WriteLine($"Object Explorer node selected: {vNode.Name}")
            
        Catch ex As Exception
            Console.WriteLine($"OnObjectExplorerNodeSelected error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Updated method to switch to the Object Explorer tab with proper activation
    ''' </summary>
    Private Sub SwitchToObjectExplorerTab()
        Try
            If pLeftNotebook IsNot Nothing AndAlso pObjectExplorer IsNot Nothing Then
                ' Find the Object Explorer page index
                For i As Integer = 0 To pLeftNotebook.NPages - 1
                    Dim lPage As Widget = pLeftNotebook.GetNthPage(i)
                    If lPage Is pObjectExplorer Then
                        pLeftNotebook.CurrentPage = i
                        Console.WriteLine($"Switched to Object Explorer tab (page {i})")
                        
                        ' Ensure it's visible and activated
                        lPage.ShowAll()
                        
                        ' Call OnPageActivated to ensure proper initialization
                        pObjectExplorer.OnPageActivated()
                        
                        Exit For
                    End If
                Next
            End If
        Catch ex As Exception
            Console.WriteLine($"SwitchToObjectExplorerTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Updated handler for left notebook page changes
    ''' </summary>
    Private Sub OnLeftNotebookPageChanged(vSender As Object, vArgs As SwitchPageArgs)
        Try
            Dim lNotebook As Notebook = TryCast(vSender, Notebook)
            If lNotebook Is Nothing Then Return
            
            Dim lNewPage As Widget = lNotebook.GetNthPage(CInt(vArgs.PageNum))
            
            ' If switching to Object Explorer, ensure it's properly activated
            If lNewPage Is pObjectExplorer Then
                Console.WriteLine("Switched to Object Explorer tab")
                
                ' Let GTK process the page switch first
                Application.Invoke(Sub()
                    ' Ensure the page is shown
                    lNewPage.ShowAll()
                    
                    ' Call OnPageActivated to ensure proper initialization
                    pObjectExplorer.OnPageActivated()
                End Sub)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnLeftNotebookPageChanged error: {ex.Message}")
        End Try
    End Sub

End Class
