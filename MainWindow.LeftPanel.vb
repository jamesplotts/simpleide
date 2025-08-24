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
            
            ' Create and add Object Explorer tab with ThemeManager
            pObjectExplorer = New CustomDrawObjectExplorer(pSettingsManager, pThemeManager)
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

    ''' <summary>
    ''' Initialize left panel with saved width from settings
    ''' </summary>
    Private Sub InitializeLeftPanelWidth()
        Try
            If pMainHPaned Is Nothing Then Return
            
            ' Get saved width from settings, default to 250
            Dim lSavedWidth As Integer = pSettingsManager.GetInteger("LeftPanelWidth", 250)
            
            ' Apply minimum width constraint
            If lSavedWidth < 50 Then lSavedWidth = 50
            
            ' Set the position (width of left panel)
            pMainHPaned.Position = lSavedWidth
            
            ' CRITICAL FIX: Use a timer to poll for position changes instead
            Dim lLastPosition As Integer = pMainHPaned.Position
            GLib.Timeout.Add(500, Function()
                Try
                    If pMainHPaned Is Nothing Then Return False ' Stop timer if disposed
                    
                    Dim lCurrentPosition As Integer = pMainHPaned.Position
                    If lCurrentPosition <> lLastPosition Then
                        lLastPosition = lCurrentPosition
                        OnLeftPanelResized(pMainHPaned, EventArgs.Empty)
                    End If
                    Return True ' Continue timer
                Catch ex As Exception
                    Console.WriteLine($"Position check error: {ex.Message}")
                    Return False ' Stop timer on error
                End Try
            End Function)            
            Console.WriteLine($"Left panel width set to: {lSavedWidth}")
            
        Catch ex As Exception
            Console.WriteLine($"InitializeLeftPanelWidth error: {ex.Message}")
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
            If lCurrentWidth >= 50 AndAlso lCurrentWidth <= 800 Then
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

End Class
