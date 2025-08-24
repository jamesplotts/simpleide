' Widgets/CustomDrawProjectExplorer.Events.vb - Event handling for Project Explorer
' Created: 2025-08-17
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing event handling for the Project Explorer
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Drawing Area Size Events =====
        
        ''' <summary>
        ''' Handles drawing area size allocation
        ''' </summary>
        Private Sub OnDrawingAreaSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            Try
                pViewportWidth = vArgs.Allocation.Width
                pViewportHeight = vArgs.Allocation.Height
                
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaSizeAllocated error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Mouse Events =====
        
        ''' <summary>
        ''' Handles mouse button press events
        ''' </summary>
        ''' <summary>
        ''' Handles mouse button press events with fixed horizontal scroll preservation
        ''' </summary>
        Private Function OnDrawingAreaButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                pDrawingArea.GrabFocus()
                
                ' Store current horizontal scroll position
                Dim lPreserveHScroll As Double = pScrollX
                
                Dim lX As Integer = CInt(vArgs.Event.X) + pScrollX
                Dim lY As Integer = CInt(vArgs.Event.Y) + pScrollY
                
                ' Find node at position
                Dim lNode As VisualProjectNode = GetNodeAtPosition(lX, lY)
                If lNode Is Nothing Then
                    ' Click on empty space - deselect
                    pSelectedNode = Nothing
                    pDrawingArea.QueueDraw()
                    
                    ' Restore horizontal scroll
                    If pHScrollBar IsNot Nothing Then
                        pHScrollBar.Value = lPreserveHScroll
                        pScrollX = lPreserveHScroll
                    End If
                    Return True
                End If
                
                ' Check what was clicked
                Dim lZone As ClickZone = GetClickZone(lNode, lX)
                
                Select Case vArgs.Event.Button
                    Case 1 ' Left click
                        Select Case lZone
                            Case ClickZone.ePlusMinus
                                ToggleNodeExpansion(lNode)
                                
                            Case ClickZone.eIcon, ClickZone.eText
                                SelectNode(lNode)
                                
                                ' Handle double-click
                                If vArgs.Event.Type = EventType.TwoButtonPress Then
                                    HandleNodeDoubleClick(lNode)
                                End If
                        End Select
                        
                    Case 3 ' Right click
                        SelectNode(lNode)
                        ShowContextMenu( vArgs.Event)
                End Select
                
                ' Always restore horizontal scroll after any action
                If pHScrollBar IsNot Nothing Then
                    pHScrollBar.Value = lPreserveHScroll
                    pScrollX = lPreserveHScroll
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaButtonPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse button release events
        ''' </summary>
        Private Function OnDrawingAreaButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs) As Boolean
            Try
                pIsDragging = False
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaButtonRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse motion events
        ''' </summary>
        Private Function OnDrawingAreaMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                pMouseX = CInt(vArgs.Event.X)
                pMouseY = CInt(vArgs.Event.Y)
                
                ' Handle dragging
                If pIsDragging Then
                    ' Could implement drag-and-drop here
                    Return True
                End If
                
                ' Update hover state
                Dim lX As Integer = pMouseX + pScrollX
                Dim lY As Integer = pMouseY + pScrollY
                Dim lNode As VisualProjectNode = GetNodeAtPosition(lX, lY)
                
                If lNode IsNot pHoveredNode Then
                    pHoveredNode = lNode
                    pDrawingArea.QueueDraw()
                    
                    ' Start tooltip timer if hovering over a node
                    If lNode IsNot Nothing Then
                        StartTooltipTimer()
                    Else
                        StopTooltipTimer()
                    End If
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse leave events
        ''' </summary>
        Private Function OnDrawingAreaLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs) As Boolean
            Try
                pHoveredNode = Nothing
                StopTooltipTimer()
                pDrawingArea.QueueDraw()
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaLeaveNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles scroll events on the drawing area
        ''' </summary>
        Private Function OnDrawingAreaScroll(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                If (vArgs.Event.State And ModifierType.ControlMask) = ModifierType.ControlMask Then
                    ' Ctrl+Scroll: Zoom in/out
                    If vArgs.Event.Direction = ScrollDirection.Up Then
                        ' Zoom in
                        Dim lNewScale As Integer = Math.Min(pCurrentScale + 10, MAX_SCALE)
                        If lNewScale <> pCurrentScale Then
                            ApplyScale(lNewScale)
                            SaveUnifiedTextScale(lNewScale)
                        End If
                    ElseIf vArgs.Event.Direction = ScrollDirection.Down Then
                        ' Zoom out
                        Dim lNewScale As Integer = Math.Max(pCurrentScale - 10, MIN_SCALE)
                        If lNewScale <> pCurrentScale Then
                            ApplyScale(lNewScale)
                            SaveUnifiedTextScale(lNewScale)
                        End If
                    End If
                    Return True
                End If
                
                ' Regular scrolling (without Ctrl)
                Select Case vArgs.Event.Direction
                    Case ScrollDirection.Up
                        pVScrollBar.Value = Math.Max(pVScrollBar.Adjustment.Lower, 
                                                    pVScrollBar.Value - pRowHeight * 3)
                    Case ScrollDirection.Down
                        pVScrollBar.Value = Math.Min(pVScrollBar.Adjustment.Upper - pVScrollBar.Adjustment.PageSize,
                                                    pVScrollBar.Value + pRowHeight * 3)
                    Case ScrollDirection.Left
                        pHScrollBar.Value = Math.Max(pHScrollBar.Adjustment.Lower,
                                                    pHScrollBar.Value - 20)
                    Case ScrollDirection.Right
                        pHScrollBar.Value = Math.Min(pHScrollBar.Adjustment.Upper - pHScrollBar.Adjustment.PageSize,
                                                    pHScrollBar.Value + 20)
                End Select
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaScroll error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Keyboard Events =====
        
        ''' <summary>
        ''' Handles key press events with fixed navigation
        ''' </summary>
        Private Function OnDrawingAreaKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
            Try
                ' Store current horizontal scroll position
                Dim lPreserveHScroll As Double = pScrollX
                
                Select Case CType(vArgs.Event.Key, UInteger)
                    ' Navigation keys
                    Case CType(Gdk.Key.Up, UInteger), CType(Gdk.Key.Down, UInteger)
                        NavigateUpDown(CType(vArgs.Event.Key, UInteger) = CType(Gdk.Key.Down, UInteger))
                        
                        ' Restore horizontal scroll
                        If pHScrollBar IsNot Nothing Then
                            pHScrollBar.Value = lPreserveHScroll
                            pScrollX = lPreserveHScroll
                        End If
                        Return True
                        
                    Case CType(Gdk.Key.Left, UInteger), CType(Gdk.Key.minus, UInteger)
                        If pSelectedNode IsNot Nothing Then
                            CollapseNode(pSelectedNode)
                        End If
                        Return True
                        
                    Case CType(Gdk.Key.Right, UInteger), CType(Gdk.Key.plus, UInteger)
                        If pSelectedNode IsNot Nothing Then
                            ExpandNode(pSelectedNode)
                        End If
                        Return True
                        
                    Case CType(Gdk.Key.Return, UInteger), CType(Gdk.Key.space, UInteger)
                        If pSelectedNode IsNot Nothing Then
                            HandleNodeDoubleClick(pSelectedNode)
                        End If
                        Return True
                        
                    Case CType(Gdk.Key.Home, UInteger), CType(Gdk.Key.KP_Home, UInteger)
                        If pVisibleNodes.Count > 0 Then
                            SelectNode(pVisibleNodes(0))
                            
                            ' Restore horizontal scroll
                            If pHScrollBar IsNot Nothing Then
                                pHScrollBar.Value = lPreserveHScroll
                                pScrollX = lPreserveHScroll
                            End If
                        End If
                        Return True
                        
                    Case CType(Gdk.Key.End, UInteger)
                        If pVisibleNodes.Count > 0 Then
                            SelectNode(pVisibleNodes(pVisibleNodes.Count - 1))
                            
                            ' Restore horizontal scroll
                            If pHScrollBar IsNot Nothing Then
                                pHScrollBar.Value = lPreserveHScroll
                                pScrollX = lPreserveHScroll
                            End If
                        End If
                        Return True
                        
                    Case CType(Gdk.Key.Page_Up, UInteger), CType(Gdk.Key.KP_Page_Up, UInteger)
                        NavigatePage(True)
                        
                        ' Restore horizontal scroll
                        If pHScrollBar IsNot Nothing Then
                            pHScrollBar.Value = lPreserveHScroll
                            pScrollX = lPreserveHScroll
                        End If
                        Return True
                        
                    Case CType(Gdk.Key.Page_Down, UInteger), CType(Gdk.Key.KP_Page_Down, UInteger)
                        NavigatePage(False)
                        
                        ' Restore horizontal scroll
                        If pHScrollBar IsNot Nothing Then
                            pHScrollBar.Value = lPreserveHScroll
                            pScrollX = lPreserveHScroll
                        End If
                        Return True
                End Select
                
                ' Ctrl+Key combinations (zoom controls etc)
                If (vArgs.Event.State And ModifierType.ControlMask) = ModifierType.ControlMask Then
                    ' Handle zoom and other Ctrl+ combinations
                    ' ... existing zoom code ...
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaKeyPress error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Scrollbar Events =====
        
        ''' <summary>
        ''' Handles horizontal scrollbar value changes
        ''' </summary>
        Private Sub OnHScrollBarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                pScrollX = CInt(pHScrollBar.Value)
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnHScrollBarValueChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles vertical scrollbar value changes (alternate name for compatibility)
        ''' </summary>
        Private Sub OnVScrollBarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                pScrollY = CInt(pVScrollBar.Value)
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnVScrollBarValueChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Settings Events =====
        
        ''' <summary>
        ''' Handles settings change notifications from SettingsManager
        ''' </summary>
        ''' <param name="vSettingName">Name of the setting that changed</param>
        ''' <param name="vOldValue">Previous value of the setting</param>
        ''' <param name="vNewValue">New value of the setting</param>
        Private Sub OnSettingsChanged(vSettingName As String, vOldValue As Object, vNewValue As Object)
            Try
                Select Case vSettingName
                    Case "Explorer.TextScale"
                        ' Handle unified text scale changes
                        If TypeOf vNewValue Is Integer Then
                            Dim lNewScale As Integer = CInt(vNewValue)
                            If lNewScale <> pCurrentScale Then
                                ApplyScale(lNewScale)
                            End If
                        End If
                        
                    Case "ColorTheme", "CurrentTheme"
                        ' Handle theme changes
                        ApplyTheme()
                        pDrawingArea?.QueueDraw()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnSettingsChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Node Interaction Helpers =====
        
        ''' <summary>
        ''' Handles node double-click without affecting horizontal scroll
        ''' </summary>
        Private Sub HandleNodeDoubleClick(vNode As VisualProjectNode)
            Try
                If vNode?.Node Is Nothing Then Return
                
                ' Store current horizontal scroll position
                Dim lPreserveHScroll As Double = pScrollX
                
                If vNode.Node.IsFile Then
                    ' Raise file selected event
                    RaiseEvent FileSelected(vNode.Node.Path)
                    
                    ' Special handling for project file
                    If vNode.Node.NodeType = ProjectNodeType.eProject Then
                        RaiseEvent ProjectFileSelected(vNode.Node.Path)
                    End If
                    
                    ' Special handling for manifest file
                    If vNode.Node.NodeType = ProjectNodeType.eManifest Then
                        RaiseEvent ManifestSelected()
                    End If
                Else
                    ' Toggle folder expansion
                    ToggleNodeExpansion(vNode)
                End If
                
                ' Raise generic double-click event
                RaiseEvent NodeDoubleClicked(vNode.Node)
                
                ' Restore horizontal scroll position
                If pHScrollBar IsNot Nothing Then
                    Application.Invoke(Sub()
                        pHScrollBar.Value = lPreserveHScroll
                        pScrollX = lPreserveHScroll
                        pDrawingArea?.QueueDraw()
                    End Sub)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandleNodeDoubleClick error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
