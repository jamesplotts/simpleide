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
        ''' Starts the tooltip timer for hover tooltips with proper cleanup
        ''' </summary>
        Private Sub StartTooltipTimer()
            Try
                ' Stop any existing timer first
                StopTooltipTimer()
                
                ' Start new timer
                pTooltipTimer = GLib.Timeout.Add(HOVER_TOOLTIP_DELAY, AddressOf ShowTooltip)
                
            Catch ex As Exception
                Console.WriteLine($"StartTooltipTimer error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Stops the tooltip timer and hides any visible tooltip with proper cleanup
        ''' </summary>
        Private Sub StopTooltipTimer()
            Try
                ' CRITICAL FIX: Clear timer ID BEFORE attempting removal
                If pTooltipTimer > 0 Then
                    Dim lTimerId As UInteger = pTooltipTimer
                    pTooltipTimer = 0  ' Clear BEFORE removing to prevent double-removal
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK and expected
                    End Try
                End If
                
                ' Hide the tooltip
                HideTooltip()
                
            Catch ex As Exception
                Console.WriteLine($"StopTooltipTimer error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows tooltip for the currently hovered node with proper timer management
        ''' </summary>
        ''' <returns>False to stop the timer</returns>
        Private Function ShowTooltip() As Boolean
            Try
                ' CRITICAL FIX: Clear timer ID immediately since we're returning False
                ' This prevents other code from trying to remove an already-removed timer
                pTooltipTimer = 0
                
                ' Check if we still have a hovered node
                If pHoveredNode Is Nothing Then 
                    Return False  ' Timer is auto-removed when returning False
                End If
                
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
                If String.IsNullOrEmpty(lTooltipText) Then
                    Return False  ' Timer is auto-removed when returning False
                End If
                
                pTooltipLabel.Markup = lTooltipText
                
                ' Position tooltip near the mouse
                Dim lScreen As Gdk.Screen = pDrawingArea.Screen
                Dim lRootX, lRootY As Integer
                pDrawingArea.Window.GetOrigin(lRootX, lRootY)
                
                Dim lTooltipX As Integer = lRootX + pMouseX + 10
                Dim lTooltipY As Integer = lRootY + pMouseY + 20
                
                ' Ensure tooltip stays on screen
                Dim lDisplay As Gdk.Display = lScreen.Display
                Dim lMonitor As Gdk.Monitor = Nothing
                
                If lDisplay IsNot Nothing AndAlso pDrawingArea.Window IsNot Nothing Then
                    lMonitor = lDisplay.GetMonitorAtWindow(pDrawingArea.Window)
                End If
                
                If lMonitor Is Nothing Then
                    lMonitor = lDisplay.GetMonitor(0)
                End If
                
                Dim lMonitorGeometry As Rectangle = lMonitor.Geometry
                
                ' FIXED: Account for monitor position in multi-monitor setups
                ' Calculate actual screen boundaries using monitor position + dimensions
                Dim lMonitorLeft As Integer = lMonitorGeometry.X
                Dim lMonitorTop As Integer = lMonitorGeometry.Y
                Dim lMonitorRight As Integer = lMonitorGeometry.X + lMonitorGeometry.Width
                Dim lMonitorBottom As Integer = lMonitorGeometry.Y + lMonitorGeometry.Height
                
                pTooltipWindow.ShowAll()
                
                ' Get tooltip size after showing
                Dim lRequisition As Requisition = Nothing
                pTooltipWindow.GetPreferredSize(lRequisition, lRequisition)
                
                ' Ensure tooltip stays within the monitor boundaries
                If lTooltipX + lRequisition.Width > lMonitorRight Then
                    lTooltipX = lMonitorRight - lRequisition.Width - 10
                End If
                
                ' Keep tooltip on the same monitor (don't let it go too far left)
                If lTooltipX < lMonitorLeft Then
                    lTooltipX = lMonitorLeft + 10
                End If
                
                If lTooltipY + lRequisition.Height > lMonitorBottom Then
                    lTooltipY = lRootY + pMouseY - lRequisition.Height - 5
                End If
                
                ' Keep tooltip on the same monitor (don't let it go too far up)
                If lTooltipY < lMonitorTop Then
                    lTooltipY = lMonitorTop + 10
                End If
                
                pTooltipWindow.Move(lTooltipX, lTooltipY)
                
                Return False  ' Don't repeat - timer is auto-removed
                
            Catch ex As Exception
                Console.WriteLine($"ShowTooltip error: {ex.Message}")
                ' Timer ID already cleared at the top
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Hides the tooltip window without touching the timer
        ''' </summary>
        Private Sub HideTooltip()
            Try
                ' Just hide the window - don't touch the timer here
                ' Timer management should be done in StopTooltipTimer
                If pTooltipWindow IsNot Nothing AndAlso pTooltipWindow.Visible Then
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