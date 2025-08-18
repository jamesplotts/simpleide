' MainWindow.Theme.vb - Theme-related functionality for MainWindow
Imports Gtk
Imports System
Imports SimpleIDE.Widgets
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Editors

Partial Public Class MainWindow
    
    ' Private fields for theme editor
    Private pThemeEditorTab As TabInfo
    Private pThemeEditor As ThemeEditor
    
    ' Show theme editor
    ' Show theme editor in a tab
    Public Sub ShowThemeEditor()
        Try
            ' Check if theme editor is already open
            for each lTabEntry in pOpenTabs
                If lTabEntry.Value.IsThemeEditor Then
                    ' Switch to existing theme editor tab
                    SwitchToTab(lTabEntry.key)
                    Return
                End If
            Next
            
            ' Create new theme editor
            pThemeEditor = New ThemeEditor(pThemeManager, pSettingsManager)
            
            ' Subscribe to events
            AddHandler pThemeEditor.ThemeChanged, AddressOf OnThemeEditorThemeChanged
            
            ' Create scrolled window for the theme editor
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.Add(pThemeEditor)
            
            ' Create tab info
            pThemeEditorTab = New TabInfo() with {
                .FilePath = "ThemeEditor",
                .IsThemeEditor = True,
                .Editor = Nothing,
                .EditorContainer = lScrolled,  ' the scrolled window IS the container
                .Modified = False
            }
            
            ' Create tab label
            Dim lBox As New Box(Orientation.Horizontal, 5)
            
            ' Icon
            Dim lIcon As New Image()
            lIcon.SetFromIconName("preferences-desktop-theme", IconSize.Menu)
            lBox.PackStart(lIcon, False, False, 0)
            
            ' Label
            Dim lLabel As New Label("Theme Editor")
            lBox.PackStart(lLabel, False, False, 0)
            
            ' Close button
            Dim lCloseButton As New Button()
            lCloseButton.Relief = ReliefStyle.None
            lCloseButton.Add(New Image(Stock.Close, IconSize.Menu))
            AddHandler lCloseButton.Clicked, Sub() CloseThemeEditor()
            lBox.PackEnd(lCloseButton, False, False, 0)
            
            lBox.ShowAll()
            pThemeEditorTab.TabLabel = lBox
            
            ' Add to notebook
            Dim lPageIndex As Integer = pNotebook.AppendPage(lScrolled, lBox)
            pNotebook.ShowAll()
            pNotebook.CurrentPage = lPageIndex
            
            ' Add to open tabs
            pOpenTabs("ThemeEditor") = pThemeEditorTab
            
        Catch ex As Exception
            Console.WriteLine($"ShowThemeEditor error: {ex.Message}")
            ShowError("Theme Editor error", "Failed to open theme Editor: " & ex.Message)
        End Try
    End Sub
    
    ' Close theme editor
    Private Sub CloseThemeEditor()
        Try
            If pThemeEditorTab IsNot Nothing Then
                ' Find and remove the tab
                for i As Integer = 0 To pNotebook.NPages - 1
                    Dim lPage As Widget = pNotebook.GetNthPage(i)
                    If lPage Is pThemeEditorTab.EditorContainer Then
                        pNotebook.RemovePage(i)
                        Exit for
                    End If
                Next
                
                ' Remove from open tabs
                If pOpenTabs.ContainsKey("ThemeEditor") Then
                    pOpenTabs.Remove("ThemeEditor")
                End If
                
                ' Clean up
                pThemeEditorTab.Dispose()
                pThemeEditorTab = Nothing
                pThemeEditor = Nothing
            End If
            
        Catch ex As Exception
            Console.WriteLine($"CloseThemeEditor error: {ex.Message}")
        End Try
    End Sub
    
    ' Apply theme to all editors and refresh all widgets
    Private Sub ApplyThemeToAllEditors()
        Try
            Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
            If lTheme Is Nothing Then Return
            
            ' First, apply the global theme CSS
            pThemeManager.ApplyCurrentTheme()
            
            ' Update all open editors
            For Each lTabInfo In pOpenTabs.Values
                If lTabInfo.Editor IsNot Nothing Then
                    Dim lEditor As CustomDrawingEditor = TryCast(lTabInfo.Editor, CustomDrawingEditor)
                    If lEditor IsNot Nothing Then
                        lEditor.ApplyTheme()
                    End If
                End If
            Next
            
            ' UPDATE: Apply theme to Object Explorer
            If pObjectExplorer IsNot Nothing Then
                pObjectExplorer.OnThemeChanged()
            End If
            
            ' UPDATE: Apply theme to Project Explorer if it has a refresh method
            If pProjectExplorer IsNot Nothing Then
                pProjectExplorer.QueueDraw()
            End If
            
            ' Force refresh of all widgets by queuing draw
            QueueDraw()
            
            ' Refresh specific widgets that might need extra attention
            RefreshWidgetThemes()
            
        Catch ex As Exception
            Console.WriteLine($"ApplyThemeToAllEditors error: {ex.Message}")
        End Try
    End Sub

    ' New method to refresh specific widgets
    Private Sub RefreshWidgetThemes()
        Try
            ' Refresh the main window
            If Me IsNot Nothing Then
                Me.QueueDraw()
            End If
            
            ' Refresh the main VBox
            If pMainVBox IsNot Nothing Then
                pMainVBox.QueueDraw()
            End If
            
            ' Refresh the menu bar
            If pMenuBar IsNot Nothing Then
                pMenuBar.QueueDraw()
            End If
            
            ' Refresh the toolbar
            If pToolBar IsNot Nothing Then
                pToolBar.QueueDraw()
            End If
            
            ' Refresh the notebook
            If pNotebook IsNot Nothing Then
                pNotebook.QueueDraw()
            End If
            
            ' Refresh the status bar
            If pStatusBar IsNot Nothing Then
                pStatusBar.QueueDraw()
            End If
            
            ' Refresh the project explorer
            If pProjectExplorer IsNot Nothing Then
                pProjectExplorer.QueueDraw()
            End If
            
            ' UPDATE: Refresh the Object Explorer
            If pObjectExplorer IsNot Nothing Then
                pObjectExplorer.QueueDraw()
            End If
            
            ' UPDATE: Refresh the left notebook containing both explorers
            If pLeftNotebook IsNot Nothing Then
                pLeftNotebook.QueueDraw()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"RefreshWidgetThemes error: {ex.Message}")
        End Try
    End Sub
    
    ' Initialize theme system
    Private Sub InitializeThemeSystem()
        Try
            ' Subscribe to theme events
            AddHandler pThemeManager.ThemeApplied, AddressOf OnThemeApplied
            AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeManagerThemeChanged
            
            ' Apply initial theme - this should happen after the UI is built
            ' Use a small delay to ensure all widgets are properly initialized
            GLib.Timeout.Add(100, Function()
                Try
                    ' Apply the theme
                    pThemeManager.ApplyCurrentTheme()
                    
                    ' Apply to all editors
                    ApplyThemeToAllEditors()
                    
                    ' Update the theme menu to reflect current theme
                    UpdateThemeMenuSelection()
                    
                Catch ex As Exception
                    Console.WriteLine($"Delayed theme application error: {ex.Message}")
                End Try
                Return False ' Don't repeat
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"InitializeThemeSystem error: {ex.Message}")
        End Try
    End Sub
    
    ' New method to update theme menu selection
    Private Sub UpdateThemeMenuSelection()
        Try
            If pThemeMenu Is Nothing Then Return
            
            Dim lCurrentThemeName As String = pThemeManager.GetCurrentTheme()
            
            ' Iterate through theme menu items and check the current one
            For Each lMenuItem As Widget In pThemeMenu.Children
                Dim lRadioItem As RadioMenuItem = TryCast(lMenuItem, RadioMenuItem)
                If lRadioItem IsNot Nothing Then
                    Dim lThemeName As String = CStr(lRadioItem.Data("ThemeName"))
                    If lThemeName = lCurrentThemeName Then
                        lRadioItem.Active = True
                        Exit For
                    End If
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"UpdateThemeMenuSelection error: {ex.Message}")
        End Try
    End Sub
    
    ' Theme changed event handler
    Private Sub OnThemeManagerThemeChanged(vTheme As EditorTheme)
        Try
            ' Apply theme to all editors and refresh widgets
            ApplyThemeToAllEditors()
            
            ' UPDATE: Notify Object Explorer of theme change
            If pObjectExplorer IsNot Nothing Then
                pObjectExplorer.OnThemeChanged()
            End If
            
            ' Update status bar
            If vTheme IsNot Nothing Then
                UpdateStatusBar($"Theme changed to: {vTheme.Name}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnThemeManagerThemeChanged error: {ex.Message}")
        End Try
    End Sub


    
    ' Theme applied event handler
    Private Sub OnThemeApplied(vThemeName As String)
        Try
            ' Update status bar or show notification
            UpdateStatusBar($"Theme '{vThemeName}' applied")
        Catch ex As Exception
            Console.WriteLine($"OnThemeApplied error: {ex.Message}")
        End Try
    End Sub

    ' Handle theme saved from theme editor
    Private Sub OnThemeEditorThemeSaved(vThemeName As String)
        Try
            ' Update theme menu
            UpdateThemeMenu()
            
            UpdateStatusBar($"Theme saved: {vThemeName}")
            
        Catch ex As Exception
            Console.WriteLine($"OnThemeEditorThemeSaved error: {ex.Message}")
        End Try
    End Sub

    ' Also update the theme menu handler to use the enhanced method
    Private Sub OnThemeMenuItemActivated(vSender As Object, vArgs As EventArgs)
        Try
            Dim lMenuItem As MenuItem = TryCast(vSender, MenuItem)
            If lMenuItem Is Nothing Then Return
            
            Dim lThemeName As String = CStr(lMenuItem.Data("ThemeName"))
            If String.IsNullOrEmpty(lThemeName) Then Return
            
            ' Set the theme
            pThemeManager.SetTheme(lThemeName)
            
            ' Apply to all editors and refresh widgets
            ApplyThemeToAllEditors()
            
            ' UPDATE: Ensure Object Explorer gets the theme change
            If pObjectExplorer IsNot Nothing Then
                pObjectExplorer.OnThemeChanged()
            End If
            
            ' Update status bar
            UpdateStatusBar($"Theme changed to: {lThemeName}")
            
        Catch ex As Exception
            Console.WriteLine($"OnThemeMenuItemActivated error: {ex.Message}")
        End Try
    End Sub

    ' Theme changed event handler
    Private Sub OnThemeEditorThemeChanged(vTheme As EditorTheme)
        Try
            ' Apply theme to all open editors
            ApplyThemeToAllEditors()
            
            ' UPDATE: Apply to Object Explorer
            If pObjectExplorer IsNot Nothing Then
                pObjectExplorer.OnThemeChanged()
            End If
            
            UpdateStatusBar($"Theme changed to: {vTheme.Name}")
            
        Catch ex As Exception
            Console.WriteLine($"OnThemeEditorThemeChanged error: {ex.Message}")
        End Try
    End Sub
    
End Class

