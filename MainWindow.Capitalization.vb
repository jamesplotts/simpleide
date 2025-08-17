' MainWindow.Capitalization.vb - Simplified classic VB-style integration
Imports Gtk
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Editors

' MainWindow.Capitalization.vb
' Created: 2025-08-07 23:16:36

Partial Public Class MainWindow
    
    ' ===== Private Fields =====
    
    Private pCapitalizationManager As IdentifierCapitalizationManager
    Private pCapitalizationStatusLabel As Label
    
    ' ===== Initialization =====
    
    ''' <summary>
    ''' Initialize capitalization management system
    ''' </summary>
    Private Sub InitializeCapitalizationManager()
        Try
            ' Create manager
            pCapitalizationManager = New IdentifierCapitalizationManager(pProjectManager, Me)
            
            ' Subscribe to events
            AddHandler pCapitalizationManager.IndexingStarted, AddressOf OnIndexingStarted
            AddHandler pCapitalizationManager.IndexingCompleted, AddressOf OnIndexingCompleted
            
            ' Add menu items (simplified)
            AddCapitalizationMenuItems()
            
            ' Add status bar widget
            AddCapitalizationStatusWidget()
            
        Catch ex As Exception
            Console.WriteLine($"InitializeCapitalizationManager error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Add simplified capitalization menu items
    ''' </summary>
    Private Sub AddCapitalizationMenuItems()
        Try
            ' Find or create Tools menu
            Dim lToolsMenu As Menu = Nothing
            Dim lMenuBar As MenuBar = TryCast(pMenuBar, MenuBar)
            
            If lMenuBar IsNot Nothing Then
                For Each lItem In lMenuBar.Children
                    Dim lMenuItem As MenuItem = TryCast(lItem, MenuItem)
                    If lMenuItem IsNot Nothing AndAlso lMenuItem.Label = "_Tools" Then
                        lToolsMenu = TryCast(lMenuItem.Submenu, Menu)
                        Exit For
                    End If
                Next
            End If
            
            ' Create Tools menu if not found
            If lToolsMenu Is Nothing Then
                Dim lToolsMenuItem As New MenuItem("_Tools")
                lToolsMenu = New Menu()
                lToolsMenuItem.Submenu = lToolsMenu
                lMenuBar.Append(lToolsMenuItem)
            End If
            
            ' Add separator if menu has items
            If lToolsMenu.Children.Length > 0 Then
                lToolsMenu.Append(New SeparatorMenuItem())
            End If
            
            ' Add capitalization submenu
            Dim lCapMenu As New MenuItem("_Identifier Capitalization")
            Dim lCapSubmenu As New Menu()
            lCapMenu.Submenu = lCapSubmenu
            
            ' Re-index Project
            Dim lReindexItem As New MenuItem("Re-_index Project")
            lReindexItem.TooltipText = "Rebuild the identifier index for auto-correction"
            AddHandler lReindexItem.Activated, AddressOf OnReindexProjectClicked
            lCapSubmenu.Append(lReindexItem)
            
            ' Toggle Auto-Correction
            Dim lToggleItem As New CheckMenuItem("_Auto-Correction Enabled")
            lToggleItem.Active = True
            lToggleItem.TooltipText = "Enable/disable automatic identifier case correction"
            AddHandler lToggleItem.Toggled, AddressOf OnToggleAutoCorrection
            lCapSubmenu.Append(lToggleItem)
            
            ' Add to Tools menu
            lToolsMenu.Append(lCapMenu)
            lToolsMenu.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"AddCapitalizationMenuItems error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Add capitalization status widget to status bar
    ''' </summary>
    Private Sub AddCapitalizationStatusWidget()
        Try
            If pStatusBar Is Nothing Then Return
            
            ' Create label for status
            pCapitalizationStatusLabel = New Label()
            pCapitalizationStatusLabel.Markup = "<span size='small' color='green'>✓ Auto-correction enabled</span>"
            pCapitalizationStatusLabel.TooltipText = "Identifier Capitalization Status"
            pCapitalizationStatusLabel.MarginStart = 10
            pCapitalizationStatusLabel.MarginEnd = 10
            
            ' Add to status bar
            pStatusBar.PackEnd(pCapitalizationStatusLabel, False, False, 0)
            pStatusBar.PackEnd(New Separator(Orientation.Vertical), False, False, 0)
            
            pCapitalizationStatusLabel.ShowAll()
            
        Catch ex As Exception
            Console.WriteLine($"AddCapitalizationStatusWidget error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Event Handlers =====
    
    Private Sub OnIndexingStarted()
        Gtk.Application.Invoke(Sub()
            UpdateStatusBar("Indexing identifiers...")
        End Sub)
    End Sub
    
    Private Sub OnIndexingCompleted(vTotalIdentifiers As Integer)
        Gtk.Application.Invoke(Sub()
            UpdateStatusBar($"Indexed {vTotalIdentifiers} identifiers for auto-correction")
        End Sub)
    End Sub
    
    Private Sub OnReindexProjectClicked(vSender As Object, vArgs As EventArgs)
        Try
            Task.Run(Async Function()
                Await pCapitalizationManager.IndexProjectAsync()
            End Function)
            
        Catch ex As Exception
            Console.WriteLine($"OnReindexProjectClicked error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnToggleAutoCorrection(vSender As Object, vArgs As EventArgs)
        Try
            Dim lToggleItem As CheckMenuItem = TryCast(vSender, CheckMenuItem)
            If lToggleItem IsNot Nothing Then
                If lToggleItem.Active Then
                    pCapitalizationStatusLabel.Markup = "<span size='small' color='green'>✓ Auto-correction enabled</span>"
                Else
                    pCapitalizationStatusLabel.Markup = "<span size='small' color='gray'>○ Auto-correction disabled</span>"
                End If
                
                ' TODO: Enable/disable auto-correction in editors
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnToggleAutoCorrection error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Hook capitalization manager to new editor
    ''' </summary>
    Private Sub AttachCapitalizationToEditor(vEditor As CustomDrawingEditor, vFilePath As String)
        Try
            If pCapitalizationManager IsNot Nothing Then
                pCapitalizationManager.AttachToEditor(vEditor, vFilePath)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"AttachCapitalizationToEditor error: {ex.Message}")
        End Try
    End Sub
    
End Class
