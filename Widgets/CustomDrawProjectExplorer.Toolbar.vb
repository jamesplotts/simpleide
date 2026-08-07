' Widgets/CustomDrawProjectExplorer.Toolbar.vb - Toolbar implementation
' Created: 2025-08-17
Imports Gtk
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing toolbar functionality for Project Explorer
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Toolbar Fields =====
        Private pRefreshButton As CustomDrawButton
        Private pCollapseAllButton As CustomDrawButton
        Private pExpandAllButton As CustomDrawButton
        Private pCloseButton As CustomDrawButton
        Private pScaleLabel As Label
        Private pScaleCombo As CustomDrawComboBox
        
        ' ===== Toolbar Initialization =====
        
        ''' <summary>
        ''' Initializes the toolbar with all buttons and controls
        ''' </summary>
        Private Sub InitializeToolbar()
            Try
                pToolbar = New Toolbar()
                pToolbar.ToolbarStyle = ToolbarStyle.Icons
                pToolbar.IconSize = IconSize.SmallToolbar
                
                ' Create toolbar items
                CreateRefreshButton()
                pToolbar.Add(New SeparatorToolItem())
                CreateExpandCollapseButtons()
                pToolbar.Add(New SeparatorToolItem())
                CreateScaleControls()
                
                ' Add expanding separator to push close button to the right
                Dim lExpandingSeparator As New SeparatorToolItem()
                lExpandingSeparator.Draw = False
                lExpandingSeparator.Expand = True
                pToolbar.Add(lExpandingSeparator)
                
                'CreateCloseButton()
                
                ' Add toolbar to container
                PackStart(pToolbar, False, False, 0)
                
            Catch ex As Exception
                Console.WriteLine($"InitializeToolbar error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates a small icon-only bevel-styled toolbar button and wraps it in a
        ''' ToolItem, matching AIAssistantPanel.CreateActionButton's icon-loading pattern
        ''' </summary>
        ''' <param name="vIconName">Icon theme name to load</param>
        ''' <param name="vTooltip">Tooltip text shown on hover</param>
        Private Function CreateToolbarButton(vIconName As String, vTooltip As String) As CustomDrawButton
            Dim lIconPixbuf As Gdk.Pixbuf = Nothing
            Try
                lIconPixbuf = Gtk.IconTheme.Default.LoadIcon(vIconName, 16, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"CreateToolbarButton icon load error: {ex.Message}")
            End Try

            Dim lButton As New CustomDrawButton("", lIconPixbuf)
            lButton.TooltipText = vTooltip
            lButton.ThemeManager = pThemeManager

            Dim lItem As New ToolItem()
            lItem.Add(lButton)
            pToolbar.Add(lItem)

            Return lButton
        End Function

        ''' <summary>
        ''' Creates the refresh button
        ''' </summary>
        Private Sub CreateRefreshButton()
            Try
                pRefreshButton = CreateToolbarButton("view-refresh", "Refresh project tree")
                AddHandler pRefreshButton.Clicked, AddressOf OnRefreshButtonClicked

            Catch ex As Exception
                Console.WriteLine($"CreateRefreshButton error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Creates expand and collapse all buttons
        ''' </summary>
        Private Sub CreateExpandCollapseButtons()
            Try
                ' Collapse all button
                pCollapseAllButton = CreateToolbarButton("list-remove", "Collapse all nodes")
                AddHandler pCollapseAllButton.Clicked, AddressOf OnCollapseAllButtonClicked

                ' Expand all button
                pExpandAllButton = CreateToolbarButton("list-add", "Expand all nodes")
                AddHandler pExpandAllButton.Clicked, AddressOf OnExpandAllButtonClicked

            Catch ex As Exception
                Console.WriteLine($"CreateExpandCollapseButtons error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the scale control combo box (unified with Object Explorer)
        ''' </summary>
        Private Sub CreateScaleControls()
            Try
                ' Create container for scale controls
                Dim lScaleItem As New ToolItem()
                Dim lScaleBox As New Box(Orientation.Horizontal, 4)
                
                ' Create label
                pScaleLabel = New Label("Scale:")
                lScaleBox.PackStart(pScaleLabel, False, False, 0)
                
                ' Create combo box with preset scales - CustomDrawComboBox has no built-in
                ' natural-width sizing (it's custom-drawn, so GTK's layout system has no way
                ' to know its content's size on its own) and is packed with Fill/Expand both
                ' False below, so without an explicit WidthRequest it collapses to
                ' essentially nothing - visually just the "Scale:" label with nothing next to it
                pScaleCombo = New CustomDrawComboBox()
                pScaleCombo.ThemeManager = pThemeManager
                pScaleCombo.WidthRequest = 70
                pScaleCombo.AppendText("50%")
                pScaleCombo.AppendText("75%")
                pScaleCombo.AppendText("100%")
                pScaleCombo.AppendText("125%")
                pScaleCombo.AppendText("150%")
                pScaleCombo.AppendText("175%")
                pScaleCombo.AppendText("200%")
                
                ' Set current scale
                Dim lCurrentScaleText As String = $"{pCurrentScale}%"
                Dim lIndex As Integer = pScaleCombo.IndexOf(lCurrentScaleText)

                If lIndex >= 0 Then
                    pScaleCombo.Active = lIndex
                Else
                    pScaleCombo.Active = 2 ' Default to 100%
                End If
                
                AddHandler pScaleCombo.Changed, AddressOf OnScaleComboChanged
                lScaleBox.PackStart(pScaleCombo, False, False, 0)
                
                lScaleItem.Add(lScaleBox)
                pToolbar.Add(lScaleItem)
                
            Catch ex As Exception
                Console.WriteLine($"CreateScaleControls error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the close button
        ''' </summary>
        Private Sub CreateCloseButton()
            Try
                pCloseButton = CreateToolbarButton("window-close", "Close Project Explorer")
                AddHandler pCloseButton.Clicked, AddressOf OnCloseButtonClicked

            Catch ex As Exception
                Console.WriteLine($"CreateCloseButton error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Toolbar Event Handlers =====
        
        ''' <summary>
        ''' Handles refresh button click
        ''' </summary>
        Private Sub OnRefreshButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                RefreshProject()
                
            Catch ex As Exception
                Console.WriteLine($"OnRefreshButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles collapse all button click
        ''' </summary>
        Private Sub OnCollapseAllButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                CollapseAll()
                
            Catch ex As Exception
                Console.WriteLine($"OnCollapseAllButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles expand all button click
        ''' </summary>
        Private Sub OnExpandAllButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                ExpandAll()
                
            Catch ex As Exception
                Console.WriteLine($"OnExpandAllButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles scale combo box change
        ''' </summary>
        Private Sub OnScaleComboChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pScaleCombo.ActiveText IsNot Nothing Then
                    Dim lScaleText As String = pScaleCombo.ActiveText.Replace("%", "")
                    Dim lScale As Integer
                    If Integer.TryParse(lScaleText, lScale) Then
                        ' Apply scale and save to unified setting
                        ApplyScale(lScale)
                        SaveUnifiedTextScale(lScale)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnScaleComboChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles close button click
        ''' </summary>
        Private Sub OnCloseButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                RaiseEvent CloseRequested()
                
            Catch ex As Exception
                Console.WriteLine($"OnCloseButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Toolbar Actions =====
        
        ''' <summary>
        ''' Collapses all nodes in the tree
        ''' </summary>
        Private Sub CollapseAll()
            Try
                pExpandedNodes.Clear()
                
                ' Keep root expanded
                If pRootNode IsNot Nothing Then
                    pExpandedNodes.Add(GetNodePath(pRootNode))
                End If
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CollapseAll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Expands all nodes in the tree
        ''' </summary>
        Private Sub ExpandAll()
            Try
                If pRootNode IsNot Nothing Then
                    ExpandNodeRecursive(pRootNode)
                End If
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ExpandAll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively expands a node and all its children
        ''' </summary>
        Private Sub ExpandNodeRecursive(vNode As ProjectNode)
            Try
                If vNode Is Nothing Then Return
                
                If vNode.Children.Count > 0 Then
                    pExpandedNodes.Add(GetNodePath(vNode))
                    
                    For Each lChild In vNode.Children
                        ExpandNodeRecursive(lChild)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExpandNodeRecursive error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the scale display in the combo box
        ''' </summary>
        Private Sub UpdateScaleDisplay()
            Try
                Dim lScaleText As String = $"{pCurrentScale}%"

                ' Find and select the matching scale
                Dim lMatchIndex As Integer = pScaleCombo.IndexOf(lScaleText)
                If lMatchIndex >= 0 Then
                    pScaleCombo.Active = lMatchIndex
                    Return
                End If
                
                ' If no exact match, set to custom value
                ' Note: GTK ComboBoxText doesn't easily support custom text
                ' For now, just select closest value
                Dim lClosestIndex As Integer = 2 ' Default to 100%
                Dim lClosestDiff As Integer = Integer.MaxValue
                
                Dim lScaleValues() As Integer = {50, 75, 100, 125, 150, 175, 200}
                For i As Integer = 0 To lScaleValues.Length - 1
                    Dim lDiff As Integer = Math.Abs(lScaleValues(i) - pCurrentScale)
                    If lDiff < lClosestDiff Then
                        lClosestDiff = lDiff
                        lClosestIndex = i
                    End If
                Next
                
                pScaleCombo.Active = lClosestIndex
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScaleDisplay error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
