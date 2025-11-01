' MainWindow.WindowState.vb - Fixed window state management for SimpleIDE
Imports Gtk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax

Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Private Fields for Window State =====
    Private pLastNormalWidth As Integer = 1024
    Private pLastNormalHeight As Integer = 768
    Private pIsMaximized As Boolean = False
    Private pWindowStateEventConnected As Boolean = False
    
    ' ===== Window State Management Methods =====
    
    ''' <summary>
    ''' Sets up window state tracking to properly handle maximize/restore
    ''' </summary>
    Private Sub SetupWindowStateTracking()
        Try
            ' Connect to window state event if not already connected
            If Not pWindowStateEventConnected Then
                AddHandler WindowStateEvent, AddressOf OnWindowStateChanged
                pWindowStateEventConnected = True
                Console.WriteLine("Window state tracking initialized")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SetupWindowStateTracking error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handles window state changes to track maximized state properly
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Window state event arguments</param>
    Private Sub OnWindowStateChanged(vSender As Object, vArgs As WindowStateEventArgs)
        Try
            Dim lNewState As Gdk.WindowState = vArgs.Event.NewWindowState
            Dim lChangedMask As Gdk.WindowState = vArgs.Event.ChangedMask
            
            ' Check if maximized state changed
            If (lChangedMask and Gdk.WindowState.Maximized) = Gdk.WindowState.Maximized Then
                Dim lWasMaximized As Boolean = pIsMaximized
                pIsMaximized = (lNewState and Gdk.WindowState.Maximized) = Gdk.WindowState.Maximized
                
                If pIsMaximized <> lWasMaximized Then
                    Console.WriteLine($"Window maximized state changed: {pIsMaximized}")
                    
                    If Not pIsMaximized Then
                        ' Window was unmaximized - restore to saved normal size
                        ' Use idle handler to ensure proper GTK event processing
                        GLib.Idle.Add(Function()
                            RestoreNormalSize()
                            Return False
                        End Function)
                    Else
                        ' Window was maximized - save current normal size first
                        SaveNormalSize()
                    End If
                End If
            End If
            
            ' Don't mark as handled to allow normal processing
            vArgs.RetVal = False
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowStateChanged error: {ex.Message}")
            vArgs.RetVal = False
        End Try
    End Sub
    
    ''' <summary>
    ''' Saves the current window size as the normal (non-maximized) size
    ''' </summary>
    Private Sub SaveNormalSize()
        Try
            ' Only save if not currently maximized
            If Not pIsMaximized AndAlso Window IsNot Nothing Then
                Dim lWidth As Integer, lHeight As Integer
                GetSize(lWidth, lHeight)
                
                ' Only save if dimensions are reasonable
                If lWidth > 400 AndAlso lHeight > 300 Then
                    pLastNormalWidth = lWidth
                    pLastNormalHeight = lHeight
                    Console.WriteLine($"Saved normal window size: {lWidth}x{lHeight}")
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SaveNormalSize error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Restores the window to its saved normal size after unmaximizing
    ''' </summary>
    Private Sub RestoreNormalSize()
        Try
            ' Get screen work area to ensure window fits
            Dim lScreen As Gdk.Screen = Screen
            If lScreen Is Nothing Then Return
            
            Dim lDisplay As Gdk.Display = lScreen.Display
            If lDisplay Is Nothing Then Return
            
            Dim lMonitor As Gdk.Monitor = Nothing
            If Window IsNot Nothing Then
                lMonitor = lDisplay.GetMonitorAtWindow(Window)
            End If
            
            If lMonitor Is Nothing Then
                lMonitor = lDisplay.GetMonitor(0)  ' Get first monitor as primary
            End If
            
            If lMonitor Is Nothing Then Return
            
            Dim lWorkArea As Gdk.Rectangle = lMonitor.Workarea
            
            ' Ensure saved size fits in work area
            Dim lWidth As Integer = Math.Min(pLastNormalWidth, lWorkArea.Width - 100)
            Dim lHeight As Integer = Math.Min(pLastNormalHeight, lWorkArea.Height - 100)
            
            ' Ensure minimum size
            If lWidth < 800 Then lWidth = 800
            If lHeight < 600 Then lHeight = 600
            
            Console.WriteLine($"Restoring to normal size: {lWidth}x{lHeight}")
            
            ' Apply the size
            Resize(lWidth, lHeight)
            
            ' Center the window
            Dim lX As Integer = lWorkArea.X + (lWorkArea.Width - lWidth) \ 2
            Dim lY As Integer = lWorkArea.Y + (lWorkArea.Height - lHeight) \ 2
            
            Move(lX, lY)
            
        Catch ex As Exception
            Console.WriteLine($"RestoreNormalSize error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced SaveWindowState that properly handles maximized windows
    ''' </summary>
    Private Sub SaveWindowStateEnhanced()
        Try
            ' Save the maximized state
            If Window IsNot Nothing Then
                Dim lWindowState As Gdk.WindowState = Window.State
                pIsMaximized = (lWindowState and Gdk.WindowState.Maximized) = Gdk.WindowState.Maximized
                pSettingsManager.WindowMaximized = pIsMaximized
                
                Console.WriteLine($"Saving window state - Maximized: {pIsMaximized}")
            End If
            
            ' Only save dimensions if NOT maximized
            If Not pIsMaximized Then
                Dim lWidth As Integer, lHeight As Integer
                GetSize(lWidth, lHeight)
                
                ' Only save if dimensions are reasonable (not too large)
                Dim lScreen As Gdk.Screen = Screen
                If lScreen IsNot Nothing Then
                    Dim lDisplay As Gdk.Display = lScreen.Display
                    If lDisplay IsNot Nothing Then
                        Dim lMonitor As Gdk.Monitor = lDisplay.GetMonitor(0)
                        If lMonitor IsNot Nothing Then
                            Dim lWorkArea As Gdk.Rectangle = lMonitor.Workarea
                            
                            ' Don't save dimensions that are too close to screen size
                            If lWidth < lWorkArea.Width - 50 AndAlso lHeight < lWorkArea.Height - 50 Then
                                pSettingsManager.WindowWidth = lWidth
                                pSettingsManager.WindowHeight = lHeight
                                Console.WriteLine($"Saved window dimensions: {lWidth}x{lHeight}")
                            Else
                                Console.WriteLine("Window dimensions too large, not saving")
                            End If
                        End If
                    End If
                End If
            Else
                ' When maximized, save the last known normal size if available
                If pLastNormalWidth > 0 AndAlso pLastNormalHeight > 0 Then
                    pSettingsManager.WindowWidth = pLastNormalWidth
                    pSettingsManager.WindowHeight = pLastNormalHeight
                    Console.WriteLine($"Saved last normal dimensions: {pLastNormalWidth}x{pLastNormalHeight}")
                End If
            End If
            
            ' Save panel states
            pSettingsManager.ShowProjectExplorer = pLeftPanelVisible
            pSettingsManager.ShowBottomPanel = pBottomPanelVisible
            pSettingsManager.LeftPanelWidth = pMainHPaned.Position
            
            ' Save settings
            pSettingsManager.SaveSettings()
            
        Catch ex As Exception
            Console.WriteLine($"SaveWindowStateEnhanced error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Override SetupWindow to include state tracking
    ''' </summary>
    Private Sub SetupWindowEnhanced()
        Try
            ' Set a reasonable default size
            SetDefaultSize(1024, 768)
            
            ' Set icon
            Try
                Dim lIconPath As String = "SimpleIDE.icon.png"
                Dim lIcon As Gdk.Pixbuf = New Gdk.Pixbuf(GetType(MainWindow).Assembly, lIconPath)
                Icon = lIcon
            Catch ex As Exception
                Console.WriteLine($"Failed to load Icon: {ex.Message}")
            End Try
            
            ' Connect event handlers
            AddHandler DeleteEvent, AddressOf OnWindowDelete
            
            ' Setup window state tracking
            SetupWindowStateTracking()
            
            ' Enable required event masks
            AddEvents(CInt(Gdk.EventMask.KeyPressMask))
            
        Catch ex As Exception
            Console.WriteLine($"SetupWindowEnhanced error: {ex.Message}")
            Throw
        End Try
    End Sub
    
    ''' <summary>
    ''' Call this instead of SaveWindowState when closing
    ''' </summary>
    Public Sub PrepareForClose()
        Try
            ' Use the enhanced save method
            SaveWindowStateEnhanced()
            
        Catch ex As Exception
            Console.WriteLine($"PrepareForClose error: {ex.Message}")
        End Try
    End Sub

    ' ===== Enhanced Window State Handling =====
    
    ''' <summary>
    ''' Override the maximize behavior to ensure proper window bounds
    ''' </summary>
    Public Sub MaximizeProper()
        Try
            ' First ensure the window is at a reasonable size
            Dim lScreen As Gdk.Screen = Screen
            If lScreen Is Nothing Then
                Maximize()
                Return
            End If
            
            Dim lDisplay As Gdk.Display = lScreen.Display
            If lDisplay Is Nothing Then
                Maximize()
                Return
            End If
            
            ' Get the monitor where the window is
            Dim lMonitor As Gdk.Monitor = Nothing
            If Window IsNot Nothing Then
                lMonitor = lDisplay.GetMonitorAtWindow(Window)
            End If
            
            If lMonitor Is Nothing Then
                lMonitor = lDisplay.GetMonitor(0)
            End If
            
            If lMonitor Is Nothing Then
                Maximize()
                Return
            End If
            
            ' Get work area (excludes panels/taskbars)
            Dim lWorkArea As Gdk.Rectangle = lMonitor.Workarea
            
            ' Set window to work area size BEFORE maximizing
            ' This helps ensure GTK respects the bounds
            Move(lWorkArea.X, lWorkArea.Y)
            Resize(lWorkArea.Width, lWorkArea.Height)
            
            ' Now maximize - should respect the work area we just set
            GLib.Idle.Add(Function()
                Maximize()
                Return False
            End Function)
            
            Console.WriteLine($"Maximized to work area: {lWorkArea.Width}x{lWorkArea.Height} at ({lWorkArea.X},{lWorkArea.Y})")
            
        Catch ex As Exception
            Console.WriteLine($"MaximizeProper error: {ex.Message}")
            ' Fall back to standard maximize
            Maximize()
        End Try
    End Sub
    
    ' ===== Fix Explorer Visibility Issues =====
'     
'     ''' <summary>
'     ''' Ensures both Project Explorer and Object Explorer are properly visible and populated
'     ''' </summary>
'     Public Sub RefreshExplorers()
'         Try
'             Console.WriteLine("=== RefreshExplorers START ===")
'             
'             ' Refresh Project Explorer
'             If pProjectExplorer IsNot Nothing Then
'                 pProjectExplorer.ShowAll()
'                 
'                 ' If project is loaded, refresh the tree
'                 If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
'                     pProjectExplorer.RefreshTree()
'                 End If
'                 
'                 Console.WriteLine("Project Explorer refreshed")
'             End If
'             
'             ' Refresh Object Explorer
'             If pObjectExplorer IsNot Nothing Then
'                 pObjectExplorer.ShowAll()
'                 
'                 ' If we have a current editor, update the object explorer
'                 Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
'                 If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
'                     ' Update Object Explorer for current editor
'                     If TypeOf lCurrentTab.Editor Is IEditor Then
'                         Dim lParser As New VBParser()
'                         Dim lText As String = lCurrentTab.Editor.Text()
'                         Dim lSyntaxTree As SyntaxNode = lParser.Parse(lText, "", lCurrentTab.FilePath).RootNode
'                         pObjectExplorer.UpdateStructure(lSyntaxTree)
'                     End If
'                 End If
'                 
'                 Console.WriteLine("Object Explorer refreshed")
'             End If
'             
'             ' Ensure the left notebook is visible
'             If pLeftNotebook IsNot Nothing Then
'                 pLeftNotebook.ShowAll()
'                 Console.WriteLine($"Left notebook visible with {pLeftNotebook.NPages} pages")
'             End If
'             
'             ' Force a redraw of the main paned
'             If pMainHPaned IsNot Nothing Then
'                 pMainHPaned.QueueDraw()
'             End If
'             
'             Console.WriteLine("=== RefreshExplorers END ===")
'             
'         Catch ex As Exception
'             Console.WriteLine($"RefreshExplorers error: {ex.Message}")
'         End Try
'     End Sub
    
'     ''' <summary>
'     ''' Fix for empty left panel - ensures content is loaded and visible
'     ''' </summary>
'     Public Sub FixEmptyLeftPanel()
'         Try
'             Console.WriteLine("=== FixEmptyLeftPanel START ===")
'             
'             ' Check if the notebook exists and has pages
'             If pLeftNotebook Is Nothing Then
'                 Console.WriteLine("ERROR: Left notebook is Nothing")
'                 Return
'             End If
'             
'             Console.WriteLine($"Left notebook has {pLeftNotebook.NPages} pages")
'             
'             ' Check each page
'             for i As Integer = 0 To pLeftNotebook.NPages - 1
'                 Dim lPage As Widget = pLeftNotebook.GetNthPage(i)
'                 Dim lTabLabel As Widget = pLeftNotebook.GetTabLabel(lPage)
'                 
'                 If lTabLabel IsNot Nothing AndAlso TypeOf lTabLabel Is Label Then
'                     Dim lLabel As Label = CType(lTabLabel, Label)
'                     Console.WriteLine($"Page {i}: {lLabel.Text} - Visible: {lPage.Visible}")
'                 End If
'                 
'                 ' Force visibility
'                 lPage.ShowAll()
'             Next
'             
'             ' Ensure the paned has proper position
'             If pMainHPaned IsNot Nothing Then
'                 If pMainHPaned.Position < 50 Then
'                     pMainHPaned.Position = 250  ' Default left panel width
'                     Console.WriteLine("Reset left panel width to 250")
'                 End If
'             End If
'             
'             ' Refresh the explorers
'             RefreshExplorers()
'             
'             Console.WriteLine("=== FixEmptyLeftPanel END ===")
'             
'         Catch ex As Exception
'             Console.WriteLine($"FixEmptyLeftPanel error: {ex.Message}")
'         End Try
'     End Sub
    
    ' ===== Override Window Delete Event =====
    
    ''' <summary>
    ''' Enhanced window delete handler that saves proper state
    ''' </summary>
    Private Sub OnWindowDeleteEnhanced(vSender As Object, vArgs As DeleteEventArgs)
        Try
            ' Save window state before closing
            SaveWindowStateEnhanced()
            
            ' Check for unsaved changes
            If Not CheckUnsavedChanges() Then
                vArgs.RetVal = True  ' Cancel close
                Return
            End If
            
'            ' Close AI assistant if it exists
'            If pAIAssistant IsNot Nothing Then
'                pAIAssistant.CloseConnection()
'            End If
            
            ' Stop file watcher if it exists
            If pFileSystemWatcher IsNot Nothing Then
                pFileSystemWatcher.Dispose()
            End If
            
            ' Clean shutdown
            Application.Quit()
            vArgs.RetVal = False
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowDeleteEnhanced error: {ex.Message}")
            vArgs.RetVal = False
        End Try
    End Sub
    
    ' ===== Debug/Diagnostic Menu Items =====
    
'     ''' <summary>
'     ''' Adds menu items to help diagnose and fix window/panel issues
'     ''' </summary>
'     Public Sub AddDiagnosticMenuItems()
'         Try
'             ' Find View menu
'             Dim lViewMenu As Menu = Nothing
'             for each lMenuItem As Widget in pMenuBar.Children
'                 If TypeOf lMenuItem Is MenuItem Then
'                     Dim lMenu As MenuItem = CType(lMenuItem, MenuItem)
'                     If lMenu.Label?.Contains("View") Then
'                         lViewMenu = CType(lMenu.Submenu, Menu)
'                         Exit for
'                     End If
'                 End If
'             Next
'             
'             If lViewMenu Is Nothing Then Return
'             
'             ' Add separator
'             lViewMenu.Append(New SeparatorMenuItem())
'             
'             ' Add "Fix Window Size" item
'             Dim lFixSize As New MenuItem("Fix Window Size")
'             AddHandler lFixSize.Activated, Sub()
'                 Try
'                     ' Get current state
'                     Dim lIsMaximized As Boolean = (Window.State and Gdk.WindowState.Maximized) = Gdk.WindowState.Maximized
'                     
'                     If lIsMaximized Then
'                         ' Unmaximize first
'                         Unmaximize()
'                         
'                         ' Then re-maximize properly
'                         GLib.Idle.Add(Function()
'                             MaximizeProper()
'                             Return False
'                         End Function)
'                     Else
'                         ' Just fix the current size
'                         Dim lScreen As Gdk.Screen = Screen
'                         If lScreen IsNot Nothing Then
'                             Dim lDisplay As Gdk.Display = lScreen.Display
'                             If lDisplay IsNot Nothing Then
'                                 Dim lMonitor As Gdk.Monitor = lDisplay.GetMonitor(0)
'                                 If lMonitor IsNot Nothing Then
'                                     Dim lWorkArea As Gdk.Rectangle = lMonitor.Workarea
'                                     
'                                     Dim lWidth As Integer, lHeight As Integer
'                                     GetSize(lWidth, lHeight)
'                                     
'                                     ' Ensure window fits in work area
'                                     If lWidth > lWorkArea.Width - 50 Then
'                                         lWidth = lWorkArea.Width - 50
'                                     End If
'                                     If lHeight > lWorkArea.Height - 50 Then
'                                         lHeight = lWorkArea.Height - 50
'                                     End If
'                                     
'                                     Resize(lWidth, lHeight)
'                                     
'                                     ' Center the window
'                                     Dim lX As Integer = lWorkArea.X + (lWorkArea.Width - lWidth) \ 2
'                                     Dim lY As Integer = lWorkArea.Y + (lWorkArea.Height - lHeight) \ 2
'                                     Move(lX, lY)
'                                 End If
'                             End If
'                         End If
'                     End If
'                     
'                     ShowInfo("Window Size Fixed", "Window has been resized to fit the screen properly.")
'                 Catch ex As Exception
'                     ShowError("Error", $"Failed to fix window size: {ex.Message}")
'                 End Try
'             End Sub
'             lViewMenu.Append(lFixSize)
'             
'             ' Add "Refresh Explorers" item
'             Dim lRefreshExplorers As New MenuItem("Refresh Explorers")
'             AddHandler lRefreshExplorers.Activated, Sub()
'                 Try
'                     FixEmptyLeftPanel()
'                     ShowInfo("Explorers Refreshed", "Project and Object explorers have been refreshed.")
'                 Catch ex As Exception
'                     ShowError("Error", $"Failed to refresh explorers: {ex.Message}")
'                 End Try
'             End Sub
'             lViewMenu.Append(lRefreshExplorers)
'             
'             ' Add "Reset Layout" item
'             Dim lResetLayout As New MenuItem("Reset Layout")
'             AddHandler lResetLayout.Activated, Sub()
'                 Try
'                     ' Reset paned positions
'                     If pMainHPaned IsNot Nothing Then
'                         pMainHPaned.Position = 250
'                     End If
'                     
'                     If pCenterVPaned IsNot Nothing Then
'                         Dim lHeight As Integer = pCenterVPaned.AllocatedHeight
'                         If lHeight > 0 Then
'                             pCenterVPaned.Position = lHeight - 200
'                         End If
'                     End If
'                     
'                     ' Show panels
'                     If pLeftPanelVisible Then
'                         pProjectExplorer?.Show()
'                     End If
'                     HideBottomPanel()
'                     
'                     ' Refresh
'                     RefreshExplorers()
'                     
'                     ShowInfo("Layout Reset", "Window layout has been reset to defaults.")
'                 Catch ex As Exception
'                     ShowError("Error", $"Failed to reset layout: {ex.Message}")
'                 End Try
'             End Sub
'             lViewMenu.Append(lResetLayout)
'             
'             Console.WriteLine("Diagnostic menu items added")
'             
'         Catch ex As Exception
'             Console.WriteLine($"AddDiagnosticMenuItems error: {ex.Message}")
'         End Try
'     End Sub


End Class