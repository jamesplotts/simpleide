' CustomDrawObjectExplorer.Navigation.vb
' Created: 2025-08-16 14:11:15

Imports System
Imports Gtk
Imports SimpleIDE.Interfaces

Namespace Widgets

    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer

        ''' <summary>
        ''' Scrolls the tree view to ensure the specified node is visible
        ''' </summary>
        ''' <param name="vNode">The visual node to scroll to</param>
        ''' <remarks>
        ''' Adjusts both horizontal and vertical scrollbars to bring the node into view
        ''' </remarks>
        Private Sub ScrollToNode(vNode As VisualNode)
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
        ''' Navigates to the previous node in the tree (up arrow key)
        ''' </summary>
        ''' <remarks>
        ''' Moves selection to the previous visible node in the tree
        ''' </remarks>
        Private Sub NavigateUp()
            Try
                ' If no selection, select the last visible node
                If pSelectedNode Is Nothing Then
                    If pVisibleNodes.Count > 0 Then
                        SelectNode(pVisibleNodes(pVisibleNodes.Count - 1))
                    End If
                    Return
                End If
                
                ' Find the current node's index
                Dim lCurrentIndex As Integer = pVisibleNodes.IndexOf(pSelectedNode)
                If lCurrentIndex > 0 Then
                    ' Select the previous node
                    Dim lPreviousNode As VisualNode = pVisibleNodes(lCurrentIndex - 1)
                    SelectNode(lPreviousNode)
                    ScrollToNode(lPreviousNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigateUp error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates to the next node in the tree (down arrow key)
        ''' </summary>
        ''' <remarks>
        ''' Moves selection to the next visible node in the tree
        ''' </remarks>
        Private Sub NavigateDown()
            Try
                ' If no selection, select the first visible node
                If pSelectedNode Is Nothing Then
                    If pVisibleNodes.Count > 0 Then
                        SelectNode(pVisibleNodes(0))
                    End If
                    Return
                End If
                
                ' Find the current node's index
                Dim lCurrentIndex As Integer = pVisibleNodes.IndexOf(pSelectedNode)
                If lCurrentIndex >= 0 AndAlso lCurrentIndex < pVisibleNodes.Count - 1 Then
                    ' Select the next node
                    Dim lNextNode As VisualNode = pVisibleNodes(lCurrentIndex + 1)
                    SelectNode(lNextNode)
                    ScrollToNode(lNextNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NavigateDown error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace