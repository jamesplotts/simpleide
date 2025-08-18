' CustomDrawProjectExplorer.Tooltip.vb - Tooltip functionality only
' Created: 2025-08-18 07:25:40

Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets

    Partial Public Class CustomDrawProjectExplorer
        Inherits Box

        ' ===== Tooltip Management =====
        
        ''' <summary>
        ''' Starts the tooltip timer for hover tooltips
        ''' </summary>
        Private Sub StartTooltipTimer()
            Try
                StopTooltipTimer()
                pTooltipTimer = GLib.Timeout.Add(HOVER_TOOLTIP_DELAY, AddressOf ShowTooltip)
                
            Catch ex As Exception
                Console.WriteLine($"StartTooltipTimer error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Stops the tooltip timer and hides any visible tooltip
        ''' </summary>
        Private Sub StopTooltipTimer()
            Try
                If pTooltipTimer > 0 Then
                    GLib.Source.Remove(pTooltipTimer)
                    pTooltipTimer = 0
                End If
                
                HideTooltip()
                
            Catch ex As Exception
                Console.WriteLine($"StopTooltipTimer error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows tooltip for the currently hovered node
        ''' </summary>
        Private Function ShowTooltip() As Boolean
            Try
                If pHoveredNode Is Nothing Then Return False
                
                ' Create tooltip window if needed
                If pTooltipWindow Is Nothing Then
                    pTooltipWindow = New Gtk.Window(Gtk.WindowType.Popup)
                    pTooltipWindow.Decorated = False
                    pTooltipWindow.SkipTaskbarHint = True
                    pTooltipWindow.SkipPagerHint = True
                    pTooltipWindow.TypeHint = Gdk.WindowTypeHint.Tooltip
                   
                    pTooltipLabel = New Label()
                    pTooltipLabel.UseMarkup = True
                    pTooltipLabel.Margin = 8
                    
                    Dim lFrame As New Frame()
                    lFrame.ShadowType = ShadowType.Out
                    lFrame.Add(pTooltipLabel)
                    pTooltipWindow.Add(lFrame)
                End If
                
                ' Set tooltip content
                Dim lTooltipText As String = BuildTooltipText(pHoveredNode)
                pTooltipLabel.Markup = lTooltipText
                
                ' Position tooltip near the mouse
                Dim lScreen As Gdk.Screen = pDrawingArea.Screen
                Dim lRootX, lRootY As Integer
                pDrawingArea.Window.GetOrigin(lRootX, lRootY)
                
                Dim lTooltipX As Integer = lRootX + pMouseX + 10
                Dim lTooltipY As Integer = lRootY + pMouseY + 20
                
                ' Ensure tooltip stays on screen
                Dim lScreenWidth As Integer = lScreen.Width
                Dim lScreenHeight As Integer = lScreen.Height
                
                pTooltipWindow.ShowAll()
                
                ' Get tooltip size after showing
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
        ''' Hides the tooltip window
        ''' </summary>
        Private Sub HideTooltip()
            Try
                If pTooltipWindow IsNot Nothing Then
                    pTooltipWindow.Hide()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HideTooltip error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Builds tooltip text for a project node
        ''' </summary>
        ''' <param name="vNode">The visual project node to build tooltip for</param>
        ''' <returns>Formatted tooltip text with markup</returns>
        Private Function BuildTooltipText(vNode As VisualProjectNode) As String
            Try
                If vNode?.Node Is Nothing Then Return "Invalid node"
                
                Dim lText As New System.Text.StringBuilder()
                
                ' Node type and name
                lText.Append($"<b>{vNode.Node.NodeType.ToString().Substring(1)}: {vNode.Node.Name}</b>")
                lText.AppendLine()
                
                ' File path for file nodes
                If Not String.IsNullOrEmpty(vNode.Node.Path) Then
                    lText.AppendLine($"Path: {vNode.Node.Path}")
                End If
                
                ' Child count for folders - use the Node.Children property
                If vNode.HasChildren AndAlso vNode.Node.Children IsNot Nothing Then
                    lText.AppendLine($"Items: {vNode.Node.Children.Count}")
                End If
                
                Return lText.ToString().Trim()
                
            Catch ex As Exception
                Console.WriteLine($"BuildTooltipText error: {ex.Message}")
                Return "Error building tooltip"
            End Try
        End Function

    End Class

End Namespace