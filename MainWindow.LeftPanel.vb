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
            #If DEBUG Then
            Console.WriteLine("InitializeLeftPanel: Starting initialization")
            #End If
            
            ' Create the CustomDrawNotebook for the left panel
            pLeftNotebook = New CustomDrawNotebook(pThemeManager)
            
            ' IMPORTANT: Set minimum width ONLY, not both parameters
            ' This ensures minimum width but allows GTK to manage visibility properly
            pLeftNotebook.SetSizeRequest(LEFT_PANEL_MINIMUM_WIDTH, -1)
            #If DEBUG Then
            Console.WriteLine($"Set left notebook minimum width to {LEFT_PANEL_MINIMUM_WIDTH}")
            #End If
            
            ' Configure the notebook
            Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pLeftNotebook, CustomDrawNotebook)
            'lCustomNotebook.SetThemeManager(pThemeManager)
            ' The hide-panel button's own handler (OnLeftPanelHideRequested below) doesn't
            ' actually do anything (ToggleLeftPanel() call is commented out) - James wants
            ' it hidden for this tab group regardless
            lCustomNotebook.ShowHidePanelButton = False
            lCustomNotebook.ShowDropdownButton = False ' Left panel needs this hidden
            lCustomNotebook.ShowScrollButtons = False
            lCustomNotebook.ShowTabCloseButtons = False ' Project/Object Explorer tabs aren't individually closable
            
            ' Wire up events for the left notebook
            AddHandler lCustomNotebook.CurrentTabChanged, AddressOf OnLeftNotebookPageChanged
            AddHandler lCustomNotebook.HidePanelRequested, AddressOf OnLeftPanelHideRequested
            
            ' Add Project Explorer tab
            If pProjectExplorer IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine("  Adding Project Explorer tab")
                #End If
                Dim lProjectIndex As Integer = lCustomNotebook.AppendPage(pProjectExplorer, "Project", "folder-open")
                #If DEBUG Then
                Console.WriteLine($"  Project Explorer added at index {lProjectIndex}")
                #End If
            End If
            
            ' Create and add Object Explorer tab with ThemeManager
            #If DEBUG Then
            Console.WriteLine("  Creating Object Explorer")
            #End If
            pObjectExplorer = New CustomDrawObjectExplorer(pSettingsManager, pThemeManager)
            AddHandler pObjectExplorer.NodeDoubleClicked, AddressOf OnObjectExplorerNodeDoubleClicked
            AddHandler pObjectExplorer.CloseRequested, AddressOf OnObjectExplorerCloseRequested
            
            ' CRITICAL: Initialize Object Explorer with ProjectManager for parsing integration
            If pProjectManager IsNot Nothing Then
                #If DEBUG Then
                Console.WriteLine("  Initializing Object Explorer with ProjectManager")
                #End If
                pObjectExplorer.InitializeWithProjectManager(pProjectManager)
            Else
                #If DEBUG Then
                Console.WriteLine("  WARNING: ProjectManager not available for Object Explorer")
                #End If
            End If
            
            #If DEBUG Then
            Console.WriteLine("  Adding Object Explorer tab")
            #End If
            ' "file-code" is not a real icon name in most system icon themes (including the
            ' active KDE theme this was tested against), so it silently fell back to a
            ' generic blank-document icon - "view-list-tree" is a standard freedesktop icon
            ' name present in Breeze, Papirus, and other common themes, and fits a class/
            ' member hierarchy browser better anyway
            Dim lObjectIndex As Integer = lCustomNotebook.AppendPage(pObjectExplorer, "Objects", "view-list-tree")
            #If DEBUG Then
            Console.WriteLine($"  Object Explorer added at index {lObjectIndex}")
            #End If
            
            ' CRITICAL FIX: Pack the notebook with shrink:=False to prevent it from disappearing
            ' resize:=False means it won't grow when window grows (keeps its set width)
            ' shrink:=False means it won't shrink below its minimum size when window shrinks
            pMainHPaned.Pack1(pLeftNotebook, resize:=False, shrink:=False)
            
            ' CRITICAL: Ensure the left notebook is visible
            pLeftNotebook.ShowAll()
            
            ' Set the first tab as active
            If lCustomNotebook.NPages > 0 Then
                #If DEBUG Then
                Console.WriteLine($"  Setting tab 0 as current (Project Explorer)")
                #End If
                lCustomNotebook.CurrentPage = 0
            End If
            
            #If DEBUG Then
            Console.WriteLine($"InitializeLeftPanel: Completed with {lCustomNotebook.NPages} tabs")
            #End If
            
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
            #If DEBUG Then
            Console.WriteLine("Left panel hide requested from CustomDrawNotebook")
            #End If
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
            #If DEBUG Then
            Console.WriteLine($"Left notebook page changed from {vOldIndex} to {vNewIndex}")
            #End If
            
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
            #If DEBUG Then
            Console.WriteLine($"EnsureLeftPanelWidth: Set position to {lSavedWidth}")
            #End If
            
        Catch ex As Exception
            Console.WriteLine($"EnsureLeftPanelWidth error: {ex.Message}")
        End Try
    End Sub

        ' Add: SimpleIDE.MainWindow.ForceShowLeftPanel
        ' To: MainWindow.LeftPanel.vb
        ''' <summary>
        ''' Force the left panel to be visible with proper width
        ''' </summary>
        Public Sub ForceShowLeftPanel()
            Try
                #If DEBUG Then
                Console.WriteLine("ForceShowLeftPanel: Starting...")
                #End If
                
                ' Check if notebook exists
                If pLeftNotebook Is Nothing Then
                    #If DEBUG Then
                    Console.WriteLine("ERROR: Left notebook doesn't exist - attempting to recreate")
                    #End If
                    InitializeLeftPanel()
                End If
                
                ' Ensure notebook is visible
                If pLeftNotebook IsNot Nothing Then
                    pLeftNotebook.ShowAll()
                    pLeftNotebook.Visible = True
                    
                    ' Set minimum size
                    pLeftNotebook.SetSizeRequest(LEFT_PANEL_MINIMUM_WIDTH, -1)
                    
                    #If DEBUG Then
                    Console.WriteLine($"Left notebook forced visible with {pLeftNotebook.NPages} pages")
                    #End If
                End If
                
                ' Ensure HPaned position
                If pMainHPaned IsNot Nothing Then
                    If pMainHPaned.Position < LEFT_PANEL_MINIMUM_WIDTH Then
                        pMainHPaned.Position = LEFT_PANEL_MINIMUM_WIDTH
                        #If DEBUG Then
                        Console.WriteLine($"Set HPaned position To {LEFT_PANEL_MINIMUM_WIDTH}")
                        #End If
                    End If
                    
                    ' Force redraw
                    pMainHPaned.QueueDraw()
                End If
                
                #If DEBUG Then
                Console.WriteLine("ForceShowLeftPanel: Complete")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"ForceShowLeftPanel error: {ex.Message}")
            End Try
        End Sub

End Class
