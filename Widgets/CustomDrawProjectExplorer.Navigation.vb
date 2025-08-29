' Widgets/CustomDrawProjectExplorer.Navigation.vb - Navigation and tree management
' Created: 2025-08-17
Imports System
Imports System.Collections.Generic
Imports Gtk
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing navigation and tree management functionality
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Visual Tree Building =====
        
        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.RebuildVisualTree
        ' Replace: SimpleIDE.Widgets.CustomDrawProjectExplorer.RebuildVisualTree
        ''' <summary>
        ''' Rebuilds the visual tree from the data model
        ''' </summary>
        Private Sub RebuildVisualTree()
            Try
                ' Store the path of the currently selected node before clearing
                Dim lSelectedPath As String = Nothing
                If pSelectedNode IsNot Nothing Then
                    lSelectedPath = GetNodePath(pSelectedNode.Node)
                    Console.WriteLine($"RebuildVisualTree: Preserving selection for path '{lSelectedPath}'")
                End If
                
                ' Clear the visual nodes
                pVisibleNodes.Clear()
                pNodeCache.Clear()
                
                ' Reset selection reference (will be restored later)
                pSelectedNode = Nothing
                
                If pRootNode Is Nothing Then
                    UpdateScrollbars()
                    Return
                End If
                
                ' Build visible nodes recursively
                Dim lY As Integer = 0
                BuildVisualNodes(pRootNode, 0, lY)
                
                ' Calculate content dimensions
                pContentHeight = lY
                pContentWidth = 0
                For Each lNode In pVisibleNodes
                    pContentWidth = Math.Max(pContentWidth, lNode.X + lNode.Width)
                Next
                
                ' Restore selection if we had one
                If Not String.IsNullOrEmpty(lSelectedPath) Then
                    ' Find the node with the matching path
                    For Each lNode In pVisibleNodes
                        If GetNodePath(lNode.Node) = lSelectedPath Then
                            pSelectedNode = lNode
                            Console.WriteLine($"RebuildVisualTree: Restored selection for '{lNode.Node.Name}'")
                            Exit for
                        End If
                    Next
                    
                    If pSelectedNode Is Nothing Then
                        Console.WriteLine($"RebuildVisualTree: Could not restore selection for path '{lSelectedPath}'")
                    End If
                End If
                
                ' Update scrollbars
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"RebuildVisualTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively builds visual nodes with proper alignment
        ''' </summary>
        ''' <param name="vNode">The project node to build from</param>
        ''' <param name="vDepth">The depth level in the tree</param>
        ''' <param name="vY">The current Y position (passed by reference)</param>
        Private Sub BuildVisualNodes(vNode As ProjectNode, vDepth As Integer, ByRef vY As Integer)
            Try
                If vNode Is Nothing Then 
                    Console.WriteLine("BuildVisualNodes: Node Is Nothing")
                    Return
                End If
                
                Console.WriteLine($"BuildVisualNodes: {vNode.Name} (Depth={vDepth}, Y={vY})")
                
                ' Calculate the actual depth for rendering
                ' Special nodes at root level should be indented the same as regular folders
                Dim lRenderDepth As Integer = vDepth
                
                ' Calculate base position using the render depth
                Dim lBaseX As Integer = lRenderDepth * pIndentWidth
                Dim lX As Integer = lBaseX
                Dim lHasChildren As Boolean = vNode.Children.Count > 0
                
                ' Create visual node with correct depth
                Dim lVisualNode As New VisualProjectNode() With {
                    .Node = vNode,
                    .X = lBaseX,
                    .Y = vY,
                    .Height = pRowHeight,
                    .Depth = vDepth,  ' Store actual depth for tree logic
                    .IsVisible = True,
                    .HasChildren = lHasChildren
                }
                
                ' Check if node is expanded
                Dim lNodePath As String = GetNodePath(vNode)
                lVisualNode.IsExpanded = pExpandedNodes.Contains(lNodePath)
                
                Console.WriteLine($"  Path: {lNodePath}, IsExpanded: {lVisualNode.IsExpanded}, HasChildren: {lHasChildren}")
                
                ' Calculate component rectangles with proper alignment
                ' The rectangles are relative to the node's X position
                If lHasChildren Then
                    ' Node has children - show plus/minus box
                    lVisualNode.PlusMinusRect = New Gdk.Rectangle(0, 0, pPlusMinusSize, pRowHeight)
                    lX = pPlusMinusSize + ICON_SPACING
                Else
                    ' Node has no children - no plus/minus box, but reserve space for alignment
                    lVisualNode.PlusMinusRect = New Gdk.Rectangle(0, 0, 0, 0)
                    lX = pPlusMinusSize + ICON_SPACING
                End If
                
                ' Icon rectangle (relative to node's X position)
                lVisualNode.IconRect = New Gdk.Rectangle(lX, 0, pIconSize, pRowHeight)
                lX += pIconSize + ICON_SPACING
                
                ' Text rectangle (relative to node's X position)
                lVisualNode.TextRect = New Gdk.Rectangle(lX, 0, 200, pRowHeight)
                
                ' Calculate actual text width for the node
                lVisualNode.Width = lBaseX + lX + (vNode.Name.Length * CInt(pFontSize * 0.6))
                
                ' Add to visible nodes
                pVisibleNodes.Add(lVisualNode)
                Console.WriteLine($"  Added To visible nodes (count={pVisibleNodes.Count})")
                
                ' Cache node
                pNodeCache(lNodePath) = lVisualNode
                
                ' Move to next Y position
                vY += pRowHeight
                
                ' Process children if expanded
                If lVisualNode.IsExpanded AndAlso lHasChildren Then
                    Console.WriteLine($"  Processing {vNode.Children.Count} children...")
                    
                    ' Children are always at the next depth level
                    For Each lChild In vNode.Children
                        BuildVisualNodes(lChild, vDepth + 1, vY)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildVisualNodes error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Node Selection and Expansion =====
 
        ''' <summary>
        ''' Toggles the expansion state of a node
        ''' </summary>
        Private Sub ToggleNodeExpansion(vNode As VisualProjectNode)
            Try
                If vNode Is Nothing OrElse Not vNode.HasChildren Then Return
                
                Dim lNodePath As String = GetNodePath(vNode.Node)
                
                If vNode.IsExpanded Then
                    pExpandedNodes.Remove(lNodePath)
                    vNode.IsExpanded = False
                Else
                    pExpandedNodes.Add(lNodePath)
                    vNode.IsExpanded = True
                End If
                
                ' Save expanded state
                SaveExpandedNodes()
                
                ' Rebuild the visual tree
                RebuildVisualTree()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ToggleNodeExpansion error: {ex.Message}")
            End Try
        End Sub
       
        ''' <summary>
        ''' Selects a node and raises the appropriate events
        ''' </summary>
        Private Sub SelectNode(vNode As VisualProjectNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Update selection
                pSelectedNode = vNode
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
                ' Raise appropriate event based on node type
                Select Case vNode.Node.NodeType
                    Case ProjectNodeType.eVBFile
                        If vNode.Node.Path.ToLower().EndsWith(".vb") Then
                            RaiseEvent FileSelected(vNode.Node.Path)
                        End If
                        
                    Case ProjectNodeType.eManifest
                        RaiseEvent ManifestSelected()
                        
                    Case ProjectNodeType.eReferences
                        ' Could open reference manager
                        
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"SelectNode error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Expands a node
        ''' </summary>
        ''' <param name="vNode">The node to expand</param>
        Private Sub ExpandNode(vNode As VisualProjectNode)
            Try
                If vNode Is Nothing OrElse Not vNode.HasChildren OrElse vNode.IsExpanded Then Return
                
                Dim lPath As String = GetNodePath(vNode.Node)
                pExpandedNodes.Add(lPath)
                vNode.IsExpanded = True
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ExpandNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Collapses a node
        ''' </summary>
        ''' <param name="vNode">The node to collapse</param>
        Private Sub CollapseNode(vNode As VisualProjectNode)
            Try
                If vNode Is Nothing OrElse Not vNode.HasChildren OrElse Not vNode.IsExpanded Then Return
                
                Dim lPath As String = GetNodePath(vNode.Node)
                pExpandedNodes.Remove(lPath)
                vNode.IsExpanded = False
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CollapseNode error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Navigation Methods =====
        
        ''' <summary>
        ''' Navigates to the previous node
        ''' </summary>
        Private Sub NavigateUp()
            Try
                If pVisibleNodes.Count = 0 Then Return
                
                If pSelectedNode Is Nothing Then
                    SelectNode(pVisibleNodes(pVisibleNodes.Count - 1))
                    Return
                End If
                
                Dim lIndex As Integer = pVisibleNodes.IndexOf(pSelectedNode)
                If lIndex > 0 Then
                    SelectNode(pVisibleNodes(lIndex - 1))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigateUp error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates to the next node
        ''' </summary>
        Private Sub NavigateDown()
            Try
                If pVisibleNodes.Count = 0 Then Return
                
                If pSelectedNode Is Nothing Then
                    SelectNode(pVisibleNodes(0))
                    Return
                End If
                
                Dim lIndex As Integer = pVisibleNodes.IndexOf(pSelectedNode)
                If lIndex >= 0 AndAlso lIndex < pVisibleNodes.Count - 1 Then
                    SelectNode(pVisibleNodes(lIndex + 1))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigateDown error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles keyboard navigation for Home key
        ''' </summary>
        Private Sub NavigateToFirstWithScroll()
            Try
                If pVisibleNodes.Count > 0 Then
                    Dim lNode As VisualProjectNode = pVisibleNodes(0)
                    SelectNode(lNode)
                    ScrollToNodeVerticalOnly(lNode)
                End If
            Catch ex As Exception
                Console.WriteLine($"NavigateToFirstWithScroll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles keyboard navigation for End key
        ''' </summary>
        Private Sub NavigateToLastWithScroll()
            Try
                If pVisibleNodes.Count > 0 Then
                    Dim lNode As VisualProjectNode = pVisibleNodes(pVisibleNodes.Count - 1)
                    SelectNode(lNode)
                    ScrollToNodeVerticalOnly(lNode)
                End If
            Catch ex As Exception
                Console.WriteLine($"NavigateToLastWithScroll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates to the first node
        ''' </summary>
        Private Sub NavigateToFirst()
            Try
                If pVisibleNodes.Count > 0 Then
                    SelectNode(pVisibleNodes(0))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToFirst error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates to the last node
        ''' </summary>
        Private Sub NavigateToLast()
            Try
                If pVisibleNodes.Count > 0 Then
                    SelectNode(pVisibleNodes(pVisibleNodes.Count - 1))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToLast error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Ensures the current horizontal scroll position is preserved
        ''' </summary>
        ''' <remarks>
        ''' Call this after any operation that might change the scroll position
        ''' </remarks>
        Private Sub PreserveHorizontalScroll()
            Try
                If pHScrollBar IsNot Nothing Then
                    ' Force the horizontal scrollbar to maintain its position
                    Dim lCurrentHScroll As Double = pScrollX
                    Application.Invoke(Sub()
                        pHScrollBar.Value = lCurrentHScroll
                        pScrollX = lCurrentHScroll
                    End Sub)
                End If
            Catch ex As Exception
                Console.WriteLine($"PreserveHorizontalScroll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates up by one page
        ''' </summary>
        Private Sub NavigatePageUp()
            Try
                If pSelectedNode Is Nothing OrElse pVisibleNodes.Count = 0 Then Return
                
                Dim lCurrentIndex As Integer = pVisibleNodes.IndexOf(pSelectedNode)
                Dim lPageSize As Integer = pViewportHeight \ pRowHeight
                Dim lNewIndex As Integer = Math.Max(0, lCurrentIndex - lPageSize)
                
                SelectNode(pVisibleNodes(lNewIndex))
                
            Catch ex As Exception
                Console.WriteLine($"NavigatePageUp error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates down by one page
        ''' </summary>
        Private Sub NavigatePageDown()
            Try
                If pSelectedNode Is Nothing OrElse pVisibleNodes.Count = 0 Then Return
                
                Dim lCurrentIndex As Integer = pVisibleNodes.IndexOf(pSelectedNode)
                Dim lPageSize As Integer = pViewportHeight \ pRowHeight
                Dim lNewIndex As Integer = Math.Min(pVisibleNodes.Count - 1, lCurrentIndex + lPageSize)
                
                SelectNode(pVisibleNodes(lNewIndex))
                
            Catch ex As Exception
                Console.WriteLine($"NavigatePageDown error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Scrolling Methods =====
        
        ''' <summary>
        ''' Scrolls to ensure a node is visible (both horizontal and vertical)
        ''' </summary>
        ''' <param name="vNode">The node to scroll to</param>
        ''' <remarks>
        ''' This method is only used when explicitly needed, not for selection
        ''' </remarks>
        Private Sub ScrollToNode(vNode As VisualProjectNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Check vertical scrolling
                If vNode.Y < pScrollY Then
                    ' Node is above viewport - scroll up
                    pVScrollBar.Value = vNode.Y
                ElseIf vNode.Y + vNode.Height > pScrollY + pViewportHeight Then
                    ' Node is below viewport - scroll down
                    pVScrollBar.Value = vNode.Y - pViewportHeight + vNode.Height + pRowHeight
                End If
                
                ' Check horizontal scrolling
                If vNode.X < pScrollX Then
                    ' Node is to the left of viewport - scroll left
                    pHScrollBar.Value = vNode.X
                ElseIf vNode.X + vNode.Width > pScrollX + pViewportWidth Then
                    ' Node is to the right of viewport - scroll right
                    pHScrollBar.Value = vNode.X - pViewportWidth + vNode.Width + 20
                End If
                
                ' Force redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ScrollToNode error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Scrolls to ensure a node is visible vertically only (preserves horizontal scroll)
        ''' </summary>
        ''' <param name="vNode">The node to scroll to</param>
        Private Sub ScrollToNodeVerticalOnly(vNode As VisualProjectNode)
            Try
                If vNode Is Nothing Then Return
                
                ' ONLY check vertical scrolling - preserve horizontal position
                If vNode.Y < pScrollY Then
                    ' Node is above viewport - scroll up
                    pVScrollBar.Value = vNode.Y
                ElseIf vNode.Y + vNode.Height > pScrollY + pViewportHeight Then
                    ' Node is below viewport - scroll down
                    pVScrollBar.Value = vNode.Y - pViewportHeight + vNode.Height + pRowHeight
                End If
                
                ' Force redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ScrollToNodeVerticalOnly error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates scrollbars based on content size
        ''' </summary>
        Private Sub UpdateScrollbars()
            Try
                If pHScrollBar Is Nothing OrElse pVScrollBar Is Nothing Then Return
                
                ' Update horizontal scrollbar
                pHScrollBar.Adjustment.Lower = 0
                pHScrollBar.Adjustment.Upper = Math.Max(pContentWidth, pViewportWidth)
                pHScrollBar.Adjustment.PageSize = pViewportWidth
                pHScrollBar.Adjustment.StepIncrement = 20
                pHScrollBar.Adjustment.PageIncrement = pViewportWidth
                
                ' Update vertical scrollbar
                pVScrollBar.Adjustment.Lower = 0
                pVScrollBar.Adjustment.Upper = Math.Max(pContentHeight, pViewportHeight)
                pVScrollBar.Adjustment.PageSize = pViewportHeight
                pVScrollBar.Adjustment.StepIncrement = pRowHeight
                pVScrollBar.Adjustment.PageIncrement = pViewportHeight
                
                ' Ensure scroll positions are valid
                pScrollX = Math.Min(pScrollX, CInt(pHScrollBar.Adjustment.Upper - pHScrollBar.Adjustment.PageSize))
                pScrollY = Math.Min(pScrollY, CInt(pVScrollBar.Adjustment.Upper - pVScrollBar.Adjustment.PageSize))
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScrollbars error: {ex.Message}")
            End Try
        End Sub

        
        ' ===== Hit Testing Methods =====
        
        ''' <summary>
        ''' Finds a visual node at the given coordinates
        ''' </summary>
        Private Function FindNodeAtPosition(vX As Integer, vY As Integer) As VisualProjectNode
            Try
                for each lNode in pVisibleNodes
                    If vY >= lNode.Y AndAlso vY < lNode.Y + lNode.Height Then
                        Return lNode
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Determines which zone of a node was clicked
        ''' </summary>
        Private Function GetClickZone(vNode As VisualProjectNode, vX As Integer) As ClickZone
            Try
                If vNode Is Nothing Then Return ClickZone.eUnspecified
                
                ' Calculate relative X position within the node
                Dim lRelativeX As Integer = vX - vNode.X
                
                ' Check plus/minus area (if node has children)
                If vNode.HasChildren Then
                    ' The PlusMinusRect.X is relative to the node's X position
                    If lRelativeX >= vNode.PlusMinusRect.X AndAlso 
                       lRelativeX < vNode.PlusMinusRect.X + vNode.PlusMinusRect.Width Then
                        Return ClickZone.ePlusMinus
                    End If
                End If
                
                ' Check icon area
                ' The IconRect.X is relative to the node's X position
                If lRelativeX >= vNode.IconRect.X AndAlso 
                   lRelativeX < vNode.IconRect.X + vNode.IconRect.Width Then
                    Return ClickZone.eIcon
                End If
                
                ' Check text area
                ' The TextRect.X is relative to the node's X position
                If lRelativeX >= vNode.TextRect.X Then
                    Return ClickZone.eText
                End If
                
                ' If we're here, the click was in the indentation area
                Return ClickZone.eUnspecified
                
            Catch ex As Exception
                Console.WriteLine($"GetClickZone error: {ex.Message}")
                Return ClickZone.eUnspecified
            End Try
        End Function

        ''' <summary>
        ''' Navigates up or down in the visible node list
        ''' </summary>
        ''' <param name="vDown">True to navigate down, False to navigate up</param>
        Private Sub NavigateUpDown(vDown As Boolean)
            Try
                If pVisibleNodes.Count = 0 Then Return
                
                Dim lCurrentIndex As Integer = -1
                If pSelectedNode IsNot Nothing Then
                    lCurrentIndex = pVisibleNodes.IndexOf(pSelectedNode)
                End If
                
                Dim lNewIndex As Integer
                If vDown Then
                    lNewIndex = If(lCurrentIndex < 0, 0, Math.Min(lCurrentIndex + 1, pVisibleNodes.Count - 1))
                Else
                    lNewIndex = If(lCurrentIndex < 0, pVisibleNodes.Count - 1, Math.Max(lCurrentIndex - 1, 0))
                End If
                
                If lNewIndex >= 0 AndAlso lNewIndex < pVisibleNodes.Count Then
                    SelectNode(pVisibleNodes(lNewIndex))
                    ScrollToNode(pVisibleNodes(lNewIndex))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigateUpDown error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates by page up or down
        ''' </summary>
        ''' <param name="vUp">True for page up, False for page down</param>
        Private Sub NavigatePage(vUp As Boolean)
            Try
                If pVisibleNodes.Count = 0 Then Return
                
                Dim lPageSize As Integer = Math.Max(1, pViewportHeight \ pRowHeight)
                
                Dim lCurrentIndex As Integer = -1
                If pSelectedNode IsNot Nothing Then
                    lCurrentIndex = pVisibleNodes.IndexOf(pSelectedNode)
                End If
                
                Dim lNewIndex As Integer
                If vUp Then
                    lNewIndex = If(lCurrentIndex < 0, 0, Math.Max(lCurrentIndex - lPageSize, 0))
                Else
                    lNewIndex = If(lCurrentIndex < 0, 0, Math.Min(lCurrentIndex + lPageSize, pVisibleNodes.Count - 1))
                End If
                
                If lNewIndex >= 0 AndAlso lNewIndex < pVisibleNodes.Count Then
                    SelectNode(pVisibleNodes(lNewIndex))
                    ScrollToNode(pVisibleNodes(lNewIndex))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigatePage error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Ensures a node is visible vertically without affecting horizontal scroll
        ''' </summary>
        ''' <param name="vNode">The node to make visible</param>
        Private Sub EnsureNodeVisibleVertically(vNode As VisualProjectNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Only adjust vertical scrolling
                Dim lNeedsRedraw As Boolean = False
                
                If vNode.Y < pScrollY Then
                    ' Node is above viewport - scroll up
                    pVScrollBar.Value = vNode.Y
                    lNeedsRedraw = True
                ElseIf vNode.Y + vNode.Height > pScrollY + pViewportHeight Then
                    ' Node is below viewport - scroll down
                    pVScrollBar.Value = vNode.Y - pViewportHeight + vNode.Height + pRowHeight
                    lNeedsRedraw = True
                End If
                
                If lNeedsRedraw Then
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"EnsureNodeVisibleVertically error: {ex.Message}")
            End Try
        End Sub

        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Checks if a node type is a special node that should appear at project root level
        ''' </summary>
        ''' <param name="vNodeType">The node type to check</param>
        ''' <returns>True if this is a special node type, False otherwise</returns>
        Private Function IsSpecialNode(vNodeType As ProjectNodeType) As Boolean
            Select Case vNodeType
                Case ProjectNodeType.eReferences,
                     ProjectNodeType.eResources,
                     ProjectNodeType.eMyProject,
                     ProjectNodeType.eManifest
                    Return True
                Case Else
                    Return False
            End Select
        End Function
        
        ''' <summary>
        ''' Gets the node path for expanded state tracking
        ''' </summary>
        Private Function GetNodePath(vNode As ProjectNode) As String
            Try
                If vNode Is Nothing Then Return ""
                
                Dim lPath As New List(Of String)
                Dim lCurrent As ProjectNode = vNode
                
                While lCurrent IsNot Nothing
                    lPath.Insert(0, lCurrent.Name)
                    lCurrent = lCurrent.Parent
                End While
                
                Return String.Join("/", lPath)
                
            Catch ex As Exception
                Console.WriteLine($"GetNodePath error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the node at the specified position
        ''' </summary>
        ''' <param name="vX">The X coordinate (adjusted for horizontal scroll)</param>
        ''' <param name="vY">The Y coordinate (adjusted for vertical scroll)</param>
        ''' <returns>The VisualProjectNode at the specified position, or Nothing if no node found</returns>
        Private Function GetNodeAtPosition(vX As Integer, vY As Integer) As VisualProjectNode
            Try
                for each lNode in pVisibleNodes
                    If vY >= lNode.Y AndAlso vY < lNode.Y + lNode.Height Then
                        Return lNode
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
    End Class
    
End Namespace
