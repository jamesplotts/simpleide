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
Imports SimpleIDE.Utilities


Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Private Fields =====
    Private pLeftNotebook As CustomDrawNotebook
    Private pObjectExplorer As CustomDrawObjectExplorer
    Private Const LEFT_PANEL_MINIMUM_WIDTH = 310
    Private pLastLeftPanelWidth As Integer = LEFT_PANEL_MINIMUM_WIDTH

    
    ' ===== Left Panel Initialization =====
    
    
    ''' <summary>
    ''' Initializes the left panel with proper shrink settings to prevent hiding on resize
    ''' </summary>
    Private Sub InitializeLeftPanel()
        Try
            Console.WriteLine("InitializeLeftPanel: Starting initialization")
            
            ' Create the CustomDrawNotebook for the left panel
            pLeftNotebook = New CustomDrawNotebook()
            
            ' IMPORTANT: Set minimum width ONLY, not both parameters
            ' This ensures minimum width but allows GTK to manage visibility properly
            pLeftNotebook.SetSizeRequest(LEFT_PANEL_MINIMUM_WIDTH, -1)
            Console.WriteLine($"Set left notebook minimum width to {LEFT_PANEL_MINIMUM_WIDTH}")
            
            ' Configure the notebook
            Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pLeftNotebook, CustomDrawNotebook)
            lCustomNotebook.SetThemeManager(pThemeManager)
            lCustomNotebook.ShowHidePanelButton = True ' Left panel needs hide button
            lCustomNotebook.ShowDropdownButton = False ' Left panel needs this hidden
            lCustomNotebook.ShowScrollButtons = False
            
            ' Wire up events for the left notebook
            AddHandler lCustomNotebook.CurrentTabChanged, AddressOf OnLeftNotebookPageChanged
            AddHandler lCustomNotebook.HidePanelRequested, AddressOf OnLeftPanelHideRequested
            
            ' Add Project Explorer tab
            If pProjectExplorer IsNot Nothing Then
                Console.WriteLine("  Adding Project Explorer tab")
                Dim lProjectIndex As Integer = lCustomNotebook.AppendPage(pProjectExplorer, "Project", "folder-open")
                Console.WriteLine($"  Project Explorer added at index {lProjectIndex}")
            End If
            
            ' Create and add Object Explorer tab with ThemeManager
            Console.WriteLine("  Creating Object Explorer")
            pObjectExplorer = New CustomDrawObjectExplorer(pSettingsManager, pThemeManager)
            AddHandler pObjectExplorer.NodeDoubleClicked, AddressOf OnObjectExplorerNodeDoubleClicked
            AddHandler pObjectExplorer.CloseRequested, AddressOf OnObjectExplorerCloseRequested
            
            Console.WriteLine("  Adding Object Explorer tab")
            Dim lObjectIndex As Integer = lCustomNotebook.AppendPage(pObjectExplorer, "Objects", "file-code")
            Console.WriteLine($"  Object Explorer added at index {lObjectIndex}")
            
            ' CRITICAL FIX: Pack the notebook with shrink:=False to prevent it from disappearing
            ' resize:=False means it won't grow when window grows (keeps its set width)
            ' shrink:=False means it won't shrink below its minimum size when window shrinks
            pMainHPaned.Pack1(pLeftNotebook, resize:=False, shrink:=False)
            
            ' CRITICAL: Ensure the left notebook is visible
            pLeftNotebook.ShowAll()
            
            ' Set the first tab as active
            If lCustomNotebook.NPages > 0 Then
                Console.WriteLine($"  Setting tab 0 as current (Project Explorer)")
                lCustomNotebook.CurrentPage = 0
            End If
            
            Console.WriteLine($"InitializeLeftPanel: Completed with {lCustomNotebook.NPages} tabs")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeLeftPanel error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Object Explorer Event Handlers =====

    ''' <summary>
    ''' Handle left panel resize and save to settings
    ''' </summary>
    Private Sub OnLeftPanelResized(vSender As Object, vArgs As EventArgs)
        Try
            If pMainHPaned Is Nothing OrElse pSettingsManager Is Nothing Then Return
            
            ' Get current position (width)
            Dim lCurrentWidth As Integer = pMainHPaned.Position
            
            ' Only save if it's a reasonable width
            If lCurrentWidth >= LEFT_PANEL_MINIMUM_WIDTH AndAlso lCurrentWidth <= 800 Then
                pSettingsManager.SetInteger("LeftPanelWidth", lCurrentWidth)
                ' Don't save immediately on every pixel change, settings will save on app close
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnLeftPanelResized error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnObjectExplorerNodeDoubleClicked(vNode As SyntaxNode)
        Try
            If vNode Is Nothing Then Return
            
            ' Get the current editor
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab Is Nothing OrElse lCurrentTab.Editor Is Nothing Then Return
            
            ' Navigate to the node's location
            If vNode.StartLine >= 0 Then
                lCurrentTab.Editor.GoToPosition(New EditorPosition(vNode.StartLine + 1, vNode.StartColumn + 1))
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
                pLeftNotebook.SetCurrentTab(0)  ' Switch to project Explorer
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
    
    ''' <summary>
    ''' Prevent auto-resize when nodes are expanded
    ''' </summary>
    ''' <remarks>
    ''' This should be called from object explorer/project explorer when nodes expand
    ''' to prevent the panel from auto-resizing
    ''' </remarks>
    Public Sub PreventAutoResize()
        Try
            If pMainHPaned Is Nothing Then Return
            
            ' Store current position
            Dim lCurrentPosition As Integer = pMainHPaned.Position
            
            ' Ensure it doesn't change
            ' Note: GTK# doesn't auto-resize paned widgets, but this is defensive
            pMainHPaned.Position = lCurrentPosition
            
        Catch ex As Exception
            Console.WriteLine($"PreventAutoResize error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles the hide panel request from the left CustomDrawNotebook
    ''' </summary>
    Private Sub OnLeftPanelHideRequested()
        Try
            'ToggleLeftPanel()
            Console.WriteLine("Left panel hide requested from CustomDrawNotebook")
        Catch ex As Exception
            Console.WriteLine($"OnLeftPanelHideRequested error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles page changes in the left notebook (CustomDrawNotebook version)
    ''' </summary>
    ''' <param name="vOldIndex">Previous tab index</param>
    ''' <param name="vNewIndex">New tab index</param>
    Private Sub OnLeftNotebookPageChanged(vOldIndex As Integer, vNewIndex As Integer)
        Try
            Console.WriteLine($"Left notebook page changed from {vOldIndex} to {vNewIndex}")
            
            ' If switching to Object Explorer tab (index 1)
            If vNewIndex = 1 Then
                ' Update Object Explorer for current editor
                UpdateObjectExplorerForActiveTab()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnLeftNotebookPageChanged error: {ex.Message}")
        End Try
    End Sub
    

    Private Sub EnsureLeftPanelWidth()
        Try
            If pMainHPaned Is Nothing Then Return
            
            Dim lSavedWidth As Integer = LEFT_PANEL_MINIMUM_WIDTH
            If pSettingsManager IsNot Nothing Then
                lSavedWidth = pSettingsManager.GetInteger("leftpanelwidth", LEFT_PANEL_MINIMUM_WIDTH)
                If lSavedWidth < LEFT_PANEL_MINIMUM_WIDTH Then lSavedWidth = LEFT_PANEL_MINIMUM_WIDTH
                If lSavedWidth > 500 Then lSavedWidth = 500
            End If
        
            pMainHPaned.Position = lSavedWidth
            Console.WriteLine($"EnsureLeftPanelWidth: Set position to {lSavedWidth}")
            
        Catch ex As Exception
            Console.WriteLine($"EnsureLeftPanelWidth error: {ex.Message}")
        End Try
    End Sub

        ' Add: SimpleIDE.MainWindow.DiagnoseLeftPanelVisibility
        ' To: MainWindow.LeftPanel.vb
        ''' <summary>
        ''' Diagnostic method to check and fix left panel visibility issues
        ''' </summary>
        Public Sub DiagnoseLeftPanelVisibility()
            Try
                Console.WriteLine("=== LEFT PANEL DIAGNOSTIC START ===")
                
                ' Check main HPaned
                If pMainHPaned Is Nothing Then
                    Console.WriteLine("ERROR: pMainHPaned is Nothing")
                    Return
                Else
                    Console.WriteLine($"pMainHPaned exists - Position: {pMainHPaned.Position}")
                    Console.WriteLine($"  Visible: {pMainHPaned.Visible}")
                    Console.WriteLine($"  Allocated Width: {pMainHPaned.AllocatedWidth}")
                    Console.WriteLine($"  Allocated Height: {pMainHPaned.AllocatedHeight}")
                End If
                
                ' Check left notebook
                If pLeftNotebook Is Nothing Then
                    Console.WriteLine("ERROR: pLeftNotebook is Nothing")
                Else
                    Console.WriteLine($"pLeftNotebook exists:")
                    Console.WriteLine($"  Visible: {pLeftNotebook.Visible}")
                    Console.WriteLine($"  Pages: {pLeftNotebook.NPages}")
                    Console.WriteLine($"  Allocated Width: {pLeftNotebook.AllocatedWidth}")
                    Console.WriteLine($"  Allocated Height: {pLeftNotebook.AllocatedHeight}")
                    
                    ' Check notebook type
                    If TypeOf pLeftNotebook Is CustomDrawNotebook Then
                        Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pLeftNotebook, CustomDrawNotebook)
                        Console.WriteLine($"  Current Page: {lCustomNotebook.CurrentPage}")
                    End If
                End If
                
                ' Check ProjectExplorer
                If pProjectExplorer Is Nothing Then
                    Console.WriteLine("ERROR: pProjectExplorer is Nothing")
                Else
                    Console.WriteLine($"pProjectExplorer exists:")
                    Console.WriteLine($"  Visible: {pProjectExplorer.Visible}")
                    Console.WriteLine($"  Parent: {If(pProjectExplorer.Parent IsNot Nothing, pProjectExplorer.Parent.GetType().Name, "Nothing")}")
                End If
                
                ' Check ObjectExplorer
                If pObjectExplorer Is Nothing Then
                    Console.WriteLine("ERROR: pObjectExplorer is Nothing")
                Else
                    Console.WriteLine($"pObjectExplorer exists:")
                    Console.WriteLine($"  Visible: {pObjectExplorer.Visible}")
                    Console.WriteLine($"  Parent: {If(pObjectExplorer.Parent IsNot Nothing, pObjectExplorer.Parent.GetType().Name, "Nothing")}")
                End If
                
                ' Check if left panel child 1 of HPaned is set
                Dim lChild1 As Widget = pMainHPaned.Child1
                If lChild1 Is Nothing Then
                    Console.WriteLine("ERROR: pMainHPaned.Child1 is Nothing - Left panel not packed!")
                Else
                    Console.WriteLine($"pMainHPaned.Child1: {lChild1.GetType().Name}")
                    Console.WriteLine($"  Child1 Visible: {lChild1.Visible}")
                End If
                
                ' Attempt to fix visibility issues
                Console.WriteLine("")
                Console.WriteLine("Attempting to fix visibility...")
                
                ' 1. Ensure left notebook is visible
                If pLeftNotebook IsNot Nothing Then
                    pLeftNotebook.ShowAll()
                    pLeftNotebook.Visible = True
                    Console.WriteLine("  Called ShowAll() on pLeftNotebook")
                End If
                
                ' 2. Ensure HPaned position is not 0
                If pMainHPaned IsNot Nothing AndAlso pMainHPaned.Position < 50 Then
                    pMainHPaned.Position = LEFT_PANEL_MINIMUM_WIDTH
                    Console.WriteLine($"  Reset pMainHPaned.Position to {LEFT_PANEL_MINIMUM_WIDTH}")
                End If
                
                ' 3. Force a redraw
                If pMainHPaned IsNot Nothing Then
                    pMainHPaned.QueueDraw()
                    Console.WriteLine("  Queued redraw for pMainHPaned")
                End If
                
                Console.WriteLine("=== LEFT PANEL DIAGNOSTIC END ===")
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseLeftPanelVisibility error: {ex.Message}")
            End Try
        End Sub
        
        ' Add: SimpleIDE.MainWindow.ForceShowLeftPanel
        ' To: MainWindow.LeftPanel.vb
        ''' <summary>
        ''' Force the left panel to be visible with proper width
        ''' </summary>
        Public Sub ForceShowLeftPanel()
            Try
                Console.WriteLine("ForceShowLeftPanel: Starting...")
                
                ' Check if notebook exists
                If pLeftNotebook Is Nothing Then
                    Console.WriteLine("ERROR: Left notebook doesn't exist - attempting to recreate")
                    InitializeLeftPanel()
                End If
                
                ' Ensure notebook is visible
                If pLeftNotebook IsNot Nothing Then
                    pLeftNotebook.ShowAll()
                    pLeftNotebook.Visible = True
                    
                    ' Set minimum size
                    pLeftNotebook.SetSizeRequest(LEFT_PANEL_MINIMUM_WIDTH, -1)
                    
                    Console.WriteLine($"Left notebook forced visible with {pLeftNotebook.NPages} pages")
                End If
                
                ' Ensure HPaned position
                If pMainHPaned IsNot Nothing Then
                    If pMainHPaned.Position < LEFT_PANEL_MINIMUM_WIDTH Then
                        pMainHPaned.Position = LEFT_PANEL_MINIMUM_WIDTH
                        Console.WriteLine($"Set HPaned position to {LEFT_PANEL_MINIMUM_WIDTH}")
                    End If
                    
                    ' Force redraw
                    pMainHPaned.QueueDraw()
                End If
                
                Console.WriteLine("ForceShowLeftPanel: Complete")
                
            Catch ex As Exception
                Console.WriteLine($"ForceShowLeftPanel error: {ex.Message}")
            End Try
        End Sub

End Class
