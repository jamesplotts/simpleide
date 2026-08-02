' MainWindow.Scratchpad.vb
' Created: 2025-08-05 20:34:07
' MainWindow.Scratchpad.vb - MainWindow integration for scratchpad feature
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Private Fields =====
    Private pScratchpadManager As ScratchpadManager
    Private pScratchpadPanels As New Dictionary(Of String, ScratchpadPanel)()
    Private pScratchpadButton As CustomDrawButton
    
    ' ===== Initialization =====
    
    Private Sub InitializeScratchpad()
        Try
            ' Create scratchpad manager
            pScratchpadManager = New ScratchpadManager(pSettingsManager)
            
            ' Set project path if project is loaded
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                pScratchpadManager.SetProjectPath(pCurrentProject)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializeScratchpad error: {ex.Message}")
        End Try
    End Sub

    Private Function CreateScratchpadTabLabel(vTitle As String, vTabId As String) As Widget
        Try
            Dim lBox As New Box(Orientation.Horizontal, 5)
            
            ' Icon
            Dim lIcon As New Image()
            lIcon.SetFromIconName("accessories-text-editor", IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLabel As New Label(vTitle)
            lBox.PackStart(lLabel, False, False, 0)
            
            ' Close button
            Dim lCloseButton As New Button()
            lCloseButton.Relief = ReliefStyle.None
            lCloseButton.FocusOnClick = False
            
            Dim lCloseIcon As New Image()
            lCloseIcon.SetFromIconName("window-close", IconSize.Menu)
            lCloseButton.Add(lCloseIcon)
            
            AddHandler lCloseButton.Clicked, Sub() CloseScratchpadTab(vTabId)
            
            lBox.PackStart(lCloseButton, False, False, 0)
            lBox.ShowAll()
            
            Return lBox
            
        Catch ex As Exception
            Console.WriteLine($"CreateScratchpadTabLabel error: {ex.Message}")
            Return New Label(vTitle)
        End Try
    End Function    

    ' ===== Toolbar Button Creation =====
    
    Private Sub CreateScratchpadToolbarButton(vPixelSize As Integer, vShowLabel As Boolean)
        Try
            ' Add separator before scratchpad button
            pToolbar.Insert(New SeparatorToolItem(), -1)

            pScratchpadButton = AddToolbarButton("pencil", False, "accessories-text-editor", "Scratchpad",
                "Toggle Scratchpad", vPixelSize, vShowLabel)
            AddHandler pScratchpadButton.Clicked, AddressOf OnOpenScratchpad

        Catch ex As Exception
            Console.WriteLine($"CreateScratchpadToolbarButton error: {ex.Message}")
        End Try
    End Sub

    
    ' ===== Event Handlers =====
    
    Private Sub AddScratchpadMenuItem(vViewMenu As Menu)
        Try
            ' Add separator
            vViewMenu.Append(New SeparatorMenuItem())
            
            ' Scratchpad menu item
            Dim lScratchpad As MenuItem = CreateMenuItemWithIcon("_Scratchpad", "accessories-text-editor")
            AddHandler lScratchpad.Activated, AddressOf OnOpenScratchpad
            vViewMenu.Append(lScratchpad)
            
        Catch ex As Exception
            Console.WriteLine($"AddScratchpadMenuItem error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnOpenScratchpad(vSender As Object, vArgs As EventArgs)
        Try
            ToggleScratchpad()
        Catch ex As Exception
            Console.WriteLine($"OnOpenScratchpad error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Public Methods =====

    ' NEW METHOD: Toggle scratchpad functionality
    Public Sub ToggleScratchpad(Optional vScratchpadId As String = "")
        Try
            ' Generate a unique tab ID for the scratchpad
            Dim lTabId As String = "scratchpad-Main"
            
            ' Check if scratchpad is already open
            If IsScratchpadOpen(lTabId) Then
                ' Close the scratchpad if it's already open
                CloseScratchpadTab(lTabId)
                UpdateScratchpadButtonTooltip(False)
            Else
                ' Open the scratchpad if it's not open
                OpenScratchpad(vScratchpadId)
                UpdateScratchpadButtonTooltip(True)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ToggleScratchpad error: {ex.Message}")
        End Try
    End Sub

    ' NEW METHOD: Check if scratchpad is currently open
    Private Function IsScratchpadOpen(vTabId As String) As Boolean
        Try
            ' Check both the panels dictionary and open tabs
            Dim lKey As String = $"scratchpad:{vTabId}"
            Return pScratchpadPanels.ContainsKey(vTabId) AndAlso pOpenTabs.ContainsKey(lKey)
        Catch ex As Exception
            Console.WriteLine($"IsScratchpadOpen error: {ex.Message}")
            Return False
        End Try
    End Function

    ' NEW METHOD: Update toolbar button tooltip based on state
    Private Sub UpdateScratchpadButtonTooltip(vIsOpen As Boolean)
        Try
            If pScratchpadButton IsNot Nothing Then
                If vIsOpen Then
                    pScratchpadButton.TooltipText = "Close Scratchpad"
                Else
                    pScratchpadButton.TooltipText = "Open Scratchpad"
                End If
            End If
        Catch ex As Exception
            Console.WriteLine($"UpdateScratchpadButtonTooltip error: {ex.Message}")
        End Try
    End Sub
    
    ' MODIFY EXISTING METHOD: Update OpenScratchpad to call UpdateTooltip
    Public Sub OpenScratchpad(Optional vScratchpadId As String = "")
        Try
            ' Check if scratchpad manager is initialized
            If pScratchpadManager Is Nothing Then
                InitializeScratchpad()
            End If
            
            ' Generate a unique tab ID for the scratchpad
            Dim lTabId As String = "scratchpad-Main"
            
            ' Check if scratchpad is already open
            If pScratchpadPanels.ContainsKey(lTabId) Then
                ' Switch to existing tab
                SwitchToScratchpadTab(lTabId)
                UpdateScratchpadButtonTooltip(True)
                Return
            End If
            
            ' Create new scratchpad panel
            Dim lScratchpadPanel As New ScratchpadPanel(pScratchpadManager)

            ' Set project path if available
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                lScratchpadPanel.SetProjectPath(pCurrentProject)
            End If

            ' Wire up events
            AddHandler lScratchpadPanel.CloseRequested, Sub() CloseScratchpadTab(lTabId)

            ' Create tab info
            Dim lTabInfo As New TabInfo()
            lTabInfo.FilePath = $"scratchpad:{lTabId}"
            lTabInfo.Editor = Nothing  ' Scratchpad doesn't implement IEditor
            lTabInfo.EditorContainer = lScratchpadPanel
            lTabInfo.TabLabel = CreateScratchpadTabLabel("Scratchpad", lTabId)
            lTabInfo.Modified = False

            ' Add to notebook
            pNotebook.AppendPage(lScratchpadPanel, "Scratchpad")
            pNotebook.ShowAll()

            ' Close the welcome tab (if any) now that the scratchpad tab is already in
            ' place - closing it first would transiently drop the notebook to 0 pages,
            ' which OnCustomNotebookTabClosed treats as "all tabs closed" and reacts to
            ' by clearing the Object Explorer's entire tree/expanded-node state
            CloseWelcomeTab()

            pNotebook.CurrentPage = pNotebook.NPages - 1

            ' Store in dictionary
            pScratchpadPanels(lTabId) = lScratchpadPanel

            ' This panel is created lazily, so if the theme was already set before this ran,
            ' it needs to be brought up to date immediately
            If pThemeManager IsNot Nothing Then
                lScratchpadPanel.SetThemeManager(pThemeManager)
            End If

            ' Store tab info (use special key to avoid conflicts with file paths)
            pOpenTabs($"scratchpad:{lTabId}") = lTabInfo
            
            ' Update UI
            UpdateWindowTitle()
            UpdateToolbarButtons()
            UpdateScratchpadButtonTooltip(True)
            
            Console.WriteLine($"Opened scratchpad: {lTabId}")
            
        Catch ex As Exception
            Console.WriteLine($"OpenScratchpad error: {ex.Message}")
            ShowError("Scratchpad Error", $"Failed To open scratchpad: {ex.Message}")
        End Try
    End Sub
    
    Private Sub SwitchToScratchpadTab(vTabId As String)
        Try
            Dim lKey As String = $"scratchpad:{vTabId}"
            If Not pOpenTabs.ContainsKey(lKey) Then Return
            
            Dim lTabInfo As TabInfo = pOpenTabs(lKey)
            
            ' Find the page index
            For i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is lTabInfo.EditorContainer Then
                    pNotebook.CurrentPage = i
                    Exit For
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"SwitchToScratchpadTab error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub CloseScratchpadTab(vTabId As String)
        Try
            Dim lKey As String = $"scratchpad:{vTabId}"
            
            If Not pOpenTabs.ContainsKey(lKey) Then Return
            
            Dim lTabInfo As TabInfo = pOpenTabs(lKey)
            
            ' Force save before closing
            If pScratchpadPanels.ContainsKey(vTabId) Then
                pScratchpadPanels(vTabId).ForceSave()
                pScratchpadPanels.Remove(vTabId)
            End If
            
            ' Find and remove the page
            For i As Integer = 0 To pNotebook.NPages - 1
                Dim lPage As Widget = pNotebook.GetNthPage(i)
                If lPage Is lTabInfo.EditorContainer Then
                    pNotebook.RemovePage(i)
                    Exit For
                End If
            Next
            
            ' Remove from open tabs
            pOpenTabs.Remove(lKey)
            
            ' Update tooltip
            UpdateScratchpadButtonTooltip(False)
            
            ' Show welcome tab if no tabs left
            If pNotebook.NPages = 0 Then
                ShowWelcomeTab()
            End If
            
            Console.WriteLine($"Closed scratchpad: {vTabId}")
            
        Catch ex As Exception
            Console.WriteLine($"CloseScratchpadTab error: {ex.Message}")
        End Try
    End Sub
    
    
    ' ===== Project Integration =====
    
    Private Sub UpdateScratchpadProjectContext()
        Try
            If pScratchpadManager IsNot Nothing Then
                pScratchpadManager.SetProjectPath(pCurrentProject)
                
                ' Update all open scratchpad panels
                For Each lPanel In pScratchpadPanels.Values
                    lPanel.SetProjectPath(pCurrentProject)
                Next
            End If
            
        Catch ex As Exception
            Console.WriteLine($"UpdateScratchpadProjectContext error: {ex.Message}")
        End Try
    End Sub
    
    ' Call this when project changes
    Private Sub OnProjectChangedUpdateScratchpad()
        Try
            UpdateScratchpadProjectContext()
        Catch ex As Exception
            Console.WriteLine($"OnProjectChangedUpdateScratchpad error: {ex.Message}")
        End Try
    End Sub
    
    ' Call this when closing the application
    Private Sub SaveAllScratchpads()
        Try
            ' Force save all open scratchpads
            For Each lPanel In pScratchpadPanels.Values
                lPanel.ForceSave()
            Next
            
        Catch ex As Exception
            Console.WriteLine($"SaveAllScratchpads error: {ex.Message}")
        End Try
    End Sub
    
End Class