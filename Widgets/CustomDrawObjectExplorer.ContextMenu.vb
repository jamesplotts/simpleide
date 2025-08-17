' CustomDrawObjectExplorer.ContextMenu.vb
' Created: 2025-08-16 14:15:54

Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Widgets

    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
        
        ' Private field for tooltip
        Private pTooltipWindow As Gtk.Window
        Private pTooltipLabel As Label
        
        ''' <summary>
        ''' Shows a tooltip for the hovered node
        ''' </summary>
        ''' <returns>True to continue the timer, False to stop it</returns>
        ''' <remarks>
        ''' Called by the tooltip timer to display node information
        ''' </remarks>
        Private Function ShowTooltip() As Boolean
            Try
                ' Clear the timer
                pTooltipTimer = 0
                
                ' Check if we have a hovered node
                If pHoveredNode Is Nothing OrElse pHoveredNode.Node Is Nothing Then
                    Return False
                End If
                
                ' Create tooltip window if needed
                If pTooltipWindow Is Nothing Then
                    pTooltipWindow = New Gtk.Window(Gtk.WindowType.Popup)
                    pTooltipWindow.TypeHint = WindowTypeHint.Tooltip
                    pTooltipWindow.Decorated = False
                    pTooltipWindow.SkipTaskbarHint = True
                    pTooltipWindow.SkipPagerHint = True
                    
                    Dim lFrame As New Frame()
                    lFrame.BorderWidth = 1
                    
                    pTooltipLabel = New Label()
                    pTooltipLabel.Margin = 4
                    lFrame.Add(pTooltipLabel)
                    
                    pTooltipWindow.Add(lFrame)
                End If
                
                ' Build tooltip text
                Dim lTooltipText As String = BuildTooltipText(pHoveredNode.Node)
                
                If String.IsNullOrEmpty(lTooltipText) Then
                    Return False
                End If
                
                ' Set tooltip text
                pTooltipLabel.Markup = lTooltipText
                
                ' Position the tooltip near the mouse
                Dim lScreen As Screen = pDrawingArea.Screen
                Dim lRootX, lRootY As Integer
                pDrawingArea.Window.GetOrigin(lRootX, lRootY)
                
                Dim lTooltipX As Integer = lRootX + pMouseX + 10
                Dim lTooltipY As Integer = lRootY + pMouseY + 20
                
                ' Ensure tooltip stays on screen
                Dim lScreenWidth As Integer = lScreen.Width
                Dim lScreenHeight As Integer = lScreen.Height
                
                pTooltipWindow.ShowAll()
                
                ' Get tooltip size
                Dim lRequisition As Requisition = Nothing
                pTooltipWindow.GetPreferredSize(lRequisition, lRequisition)
                
                If lTooltipX + lRequisition.Width > lScreenWidth Then
                    lTooltipX = lScreenWidth - lRequisition.Width - 10
                End If
                
                If lTooltipY + lRequisition.Height > lScreenHeight Then
                    lTooltipY = lRootY + pMouseY - lRequisition.Height - 5
                End If
                
                pTooltipWindow.Move(lTooltipX, lTooltipY)
                
                Return False ' Don't repeat the timer
                
            Catch ex As Exception
                Console.WriteLine($"ShowTooltip error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Builds the tooltip text for a node
        ''' </summary>
        ''' <param name="vNode">The syntax node to build tooltip for</param>
        ''' <returns>Formatted tooltip text with markup</returns>
        Private Function BuildTooltipText(vNode As SyntaxNode) As String
            Try
                Dim lText As New System.Text.StringBuilder()
                
                ' Node type and name
                lText.Append($"<b>{vNode.NodeType.ToString().Substring(1)}: {vNode.Name}</b>")
                lText.AppendLine()
                
                ' Visibility
                If vNode.IsPublic Then
                    lText.AppendLine("Visibility: Public")
                ElseIf vNode.IsProtected Then
                    lText.AppendLine("Visibility: Protected")
                ElseIf vNode.IsFriend Then
                    lText.AppendLine("Visibility: Friend")
                Else
                    lText.AppendLine("Visibility: Private")
                End If
                
                ' Modifiers
                If vNode.IsShared Then
                    lText.AppendLine("Modifier: Shared")
                End If
                If vNode.IsOverrides Then
                    lText.AppendLine("Modifier: Overrides")
                End If
                
                ' Location
                If Not String.IsNullOrEmpty(vNode.FilePath) Then
                    Dim lFileName As String = System.IO.Path.GetFileName(vNode.FilePath)
                    lText.AppendLine($"File: {lFileName}")
                    lText.AppendLine($"Line: {vNode.StartLine + 1}")
                End If
                
                ' XML documentation if available
                If vNode.Attributes.ContainsKey("XmlDoc") Then
                    lText.AppendLine()
                    lText.AppendLine("<i>" & vNode.Attributes("XmlDoc") & "</i>")
                End If
                
                Return lText.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"BuildTooltipText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Hides the tooltip window
        ''' </summary>
        Private Sub HideTooltip()
            Try
                If pTooltipWindow IsNot Nothing AndAlso pTooltipWindow.Visible Then
                    pTooltipWindow.Hide()
                End If
                
                ' Cancel any pending tooltip timer
                If pTooltipTimer <> 0 Then
                    GLib.Source.Remove(pTooltipTimer)
                    pTooltipTimer = 0
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HideTooltip error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the context menu for node operations
        ''' </summary>
        ''' <remarks>
        ''' Sets up the right-click context menu with various node operations
        ''' </remarks>
        Private Sub CreateContextMenu()
            Try
                pContextMenu = New Menu()
                
                ' Go to Definition
                Dim lGoToItem As New MenuItem("Go to Definition")
                AddHandler lGoToItem.Activated, Sub(s, e)
                    If pSelectedNode?.Node IsNot Nothing Then
                        HandleNodeActivation(pSelectedNode)
                    End If
                End Sub
                pContextMenu.Append(lGoToItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Expand/Collapse
                Dim lExpandItem As New MenuItem("Expand")
                AddHandler lExpandItem.Activated, Sub(s, e)
                    If pSelectedNode IsNot Nothing AndAlso pSelectedNode.HasChildren Then
                        If Not pSelectedNode.IsExpanded Then
                            ToggleNodeExpansion(pSelectedNode)
                        End If
                    End If
                End Sub
                pContextMenu.Append(lExpandItem)
                
                Dim lCollapseItem As New MenuItem("Collapse")
                AddHandler lCollapseItem.Activated, Sub(s, e)
                    If pSelectedNode IsNot Nothing AndAlso pSelectedNode.HasChildren Then
                        If pSelectedNode.IsExpanded Then
                            ToggleNodeExpansion(pSelectedNode)
                        End If
                    End If
                End Sub
                pContextMenu.Append(lCollapseItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Expand All / Collapse All
                Dim lExpandAllItem As New MenuItem("Expand All")
                AddHandler lExpandAllItem.Activated, Sub(s, e)
                    ExpandAll()
                End Sub
                pContextMenu.Append(lExpandAllItem)
                
                Dim lCollapseAllItem As New MenuItem("Collapse All")
                AddHandler lCollapseAllItem.Activated, Sub(s, e)
                    CollapseAll()
                End Sub
                pContextMenu.Append(lCollapseAllItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Sort options
                Dim lSortMenu As New Menu()
                Dim lSortItem As New MenuItem("Sort By")
                lSortItem.Submenu = lSortMenu
                
                Dim lSortDefaultItem As New RadioMenuItem("Default Order")
                AddHandler lSortDefaultItem.Activated, Sub(s, e)
                    SortMode = ObjectExplorerSortMode.eDefault
                End Sub
                lSortMenu.Append(lSortDefaultItem)
                
                Dim lSortAlphaItem As New RadioMenuItem(lSortDefaultItem.Group, "Alphabetical")
                AddHandler lSortAlphaItem.Activated, Sub(s, e)
                    SortMode = ObjectExplorerSortMode.eAlphabetic
                End Sub
                lSortMenu.Append(lSortAlphaItem)
                
                Dim lSortTypeItem As New RadioMenuItem(lSortDefaultItem.Group, "By Type")
                AddHandler lSortTypeItem.Activated, Sub(s, e)
                    SortMode = ObjectExplorerSortMode.eByType
                End Sub
                lSortMenu.Append(lSortTypeItem)
                
                Dim lSortVisibilityItem As New RadioMenuItem(lSortDefaultItem.Group, "By Visibility")
                AddHandler lSortVisibilityItem.Activated, Sub(s, e)
                    SortMode = ObjectExplorerSortMode.eByVisibility
                End Sub
                lSortMenu.Append(lSortVisibilityItem)
                
                pContextMenu.Append(lSortItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Filter options
                Dim lShowPrivateItem As New CheckMenuItem("Show Private Members")
                lShowPrivateItem.Active = pShowPrivateMembers
                AddHandler lShowPrivateItem.Toggled, Sub(s, e)
                    pShowPrivateMembers = lShowPrivateItem.Active
                    RebuildVisualTree()
                    pDrawingArea?.QueueDraw()
                End Sub
                pContextMenu.Append(lShowPrivateItem)
                
                Dim lShowRegionsItem As New CheckMenuItem("Show Regions")
                lShowRegionsItem.Active = pShowRegions
                AddHandler lShowRegionsItem.Toggled, Sub(s, e)
                    pShowRegions = lShowRegionsItem.Active
                    RebuildVisualTree()
                    pDrawingArea?.QueueDraw()
                End Sub
                pContextMenu.Append(lShowRegionsItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Refresh
                Dim lRefreshItem As New MenuItem("Refresh")
                AddHandler lRefreshItem.Activated, Sub(s, e)
                    RefreshStructure()
                End Sub
                pContextMenu.Append(lRefreshItem)
                
                ' Show all items
                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"CreateContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows the context menu at the specified position
        ''' </summary>
        ''' <param name="vNode">The node that was right-clicked</param>
        ''' <param name="vEventButton">The button event containing position information</param>
        ''' <remarks>
        ''' Displays the context menu and updates menu items based on the selected node
        ''' </remarks>
        Private Sub ShowContextMenu(vNode As VisualNode, vEventButton As EventButton)
            Try
                If pContextMenu Is Nothing Then
                    CreateContextMenu()
                End If
                
                ' Update menu items based on selected node
                If vNode IsNot Nothing Then
                    ' Enable/disable expand/collapse based on whether node has children
                    Dim lHasChildren As Boolean = vNode.HasChildren
                    Dim lIsExpanded As Boolean = vNode.IsExpanded
                    
                    ' Find and update menu items
                    For Each lItem As Widget In pContextMenu.Children
                        If TypeOf lItem Is MenuItem Then
                            Dim lMenuItem As MenuItem = DirectCast(lItem, MenuItem)
                            Select Case lMenuItem.Label
                                Case "Expand"
                                    lMenuItem.Sensitive = lHasChildren AndAlso Not lIsExpanded
                                Case "Collapse"
                                    lMenuItem.Sensitive = lHasChildren AndAlso lIsExpanded
                            End Select
                        End If
                    Next
                End If
                
                ' Show the context menu
                pContextMenu.PopupAtPointer(vEventButton)
                
            Catch ex As Exception
                Console.WriteLine($"ShowContextMenu error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
