' Widgets/CustomDrawObjectExplorer.Events.vb - Event handlers for Object Explorer
' Created: 2025-08-16
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing all event handlers for the Object Explorer
    ''' </summary>
    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
       
        ' ===== Event Handler Setup =====
        
        ''' <summary>
        ''' Sets up event handlers for all UI components
        ''' </summary>
        Private Sub SetupEventHandlers()
            Try
                ' Drawing area events
                AddHandler pDrawingArea.Drawn, AddressOf OnDrawingAreaDraw
                AddHandler pDrawingArea.SizeAllocated, AddressOf OnDrawingAreaSizeAllocated
                AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnDrawingAreaButtonPress
                AddHandler pDrawingArea.ButtonReleaseEvent, AddressOf OnDrawingAreaButtonRelease
                AddHandler pDrawingArea.MotionNotifyEvent, AddressOf OnDrawingAreaMotionNotify
                AddHandler pDrawingArea.ScrollEvent, AddressOf OnDrawingAreaScroll
                AddHandler pDrawingArea.KeyPressEvent, AddressOf OnDrawingAreaKeyPress
                AddHandler pDrawingArea.EnterNotifyEvent, AddressOf OnDrawingAreaEnter
                AddHandler pDrawingArea.LeaveNotifyEvent, AddressOf OnDrawingAreaLeaveNotify
                
                ' Scrollbar events - FIXED: Use consistent naming
                AddHandler pHScrollBar.ValueChanged, AddressOf OnHScrollBarValueChanged
                AddHandler pVScrollBar.ValueChanged, AddressOf OnVScrollBarValueChanged
                
                ' Corner box drawing
                AddHandler pCornerBox.Drawn, AddressOf OnCornerBoxDraw
                
            Catch ex As Exception
                Console.WriteLine($"SetupEventHandlers error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Size Allocation =====
        
        ''' <summary>
        ''' Handles size allocation changes for the drawing area
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
        Private Function OnDrawingAreaButtonPress(vSender As Object, vArgs As ButtonPressEventArgs) As Boolean
            Try
                pDrawingArea.GrabFocus()
                
                Dim lX As Integer = CInt(vArgs.Event.X) + pScrollX
                Dim lY As Integer = CInt(vArgs.Event.Y) + pScrollY
                
                ' Find node at position
                Dim lNode As VisualNode = GetNodeAtPosition(lX, lY)
                If lNode Is Nothing Then
                    ' Click on empty space - deselect
                    pSelectedNode = Nothing
                    pDrawingArea.QueueDraw()
                    Return True
                End If
                
                ' Check what was clicked
                Dim lZone As ClickZone = GetClickZone(lX, lY, lNode)
                
                Select Case vArgs.Event.Button
                    Case 1 ' Left click
                        Select Case lZone
                            Case ClickZone.ePlusMinus
                                ToggleNodeExpansion(lNode)
                                
                            Case ClickZone.eIcon, ClickZone.eText
                                SelectNode(lNode)
                                
                                ' Check for double-click
                                If vArgs.Event.Type = EventType.TwoButtonPress Then
                                    HandleNodeDoubleClick(lNode)
                                End If
                        End Select
                        
                    Case 3 ' Right click
                        If lZone = ClickZone.eIcon OrElse lZone = ClickZone.eText Then
                            SelectNode(lNode)
                            ShowContextMenu(lNode, vArgs.Event)
                        End If
                End Select
                
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
            ' Currently unused but available for drag operations
            Return False
        End Function
        
        ''' <summary>
        ''' Handles mouse motion events with proper timer management
        ''' </summary>
        Private Function OnDrawingAreaMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs) As Boolean
            Try
                pMouseX = CInt(vArgs.Event.X)
                pMouseY = CInt(vArgs.Event.Y)
                
                Dim lX As Integer = pMouseX + pScrollX
                Dim lY As Integer = pMouseY + pScrollY
                
                ' Find node at position
                Dim lNode As VisualNode = GetNodeAtPosition(lX, lY)
                
                ' Update hover state
                If lNode IsNot pHoveredNode Then
                    pHoveredNode = lNode
                    pDrawingArea.QueueDraw()
                    
                    ' CRITICAL FIX: Reset tooltip timer with proper cleanup
                    If pTooltipTimer <> 0 Then
                        Dim lTimerId As UInteger = pTooltipTimer
                        pTooltipTimer = 0  ' Clear BEFORE removing
                        Try
                            GLib.Source.Remove(lTimerId)
                        Catch
                            ' Timer may have already expired - this is OK
                        End Try
                    End If
                    
                    ' Hide tooltip if no longer hovering over a node
                    If lNode Is Nothing Then
                        HideTooltip()
                    Else
                        ' Start new tooltip timer if hovering over a node
                        pTooltipTimer = GLib.Timeout.Add(HOVER_TOOLTIP_DELAY, AddressOf ShowTooltip)
                    End If
                End If
                
                ' Update cursor based on zone
                If lNode IsNot Nothing Then
                    Dim lZone As ClickZone = GetClickZone(lX, lY, lNode)
                    Select Case lZone
                        Case ClickZone.ePlusMinus
                            Dim lDisplay As Gdk.Display = Gdk.Display.Default
                            pDrawingArea.Window.Cursor = New Cursor(lDisplay, CursorType.Hand1)
                        Case ClickZone.eIcon, ClickZone.eText
                            Dim lDisplay As Gdk.Display = Gdk.Display.Default
                            pDrawingArea.Window.Cursor = New Cursor(lDisplay, CursorType.Hand2)
                        Case Else
                            pDrawingArea.Window.Cursor = Nothing
                    End Select
                Else
                    pDrawingArea.Window.Cursor = Nothing
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaMotionNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles mouse enter events
        ''' </summary>
        Private Function OnDrawingAreaEnter(vSender As Object, vArgs As EnterNotifyEventArgs) As Boolean
            ' Could be used to show/hide elements
            Return False
        End Function
        
        ''' <summary>
        ''' Handles mouse leave events with proper timer management
        ''' </summary>
        Private Function OnDrawingAreaLeave(vSender As Object, vArgs As LeaveNotifyEventArgs) As Boolean
            Try
                ' Clear hover state
                pHoveredNode = Nothing
                pDrawingArea.QueueDraw()
                
                ' CRITICAL FIX: Cancel tooltip timer with proper cleanup
                If pTooltipTimer <> 0 Then
                    Dim lTimerId As UInteger = pTooltipTimer
                    pTooltipTimer = 0  ' Clear BEFORE removing
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
        
                ' Hide the tooltip
                HideTooltip()
                
                ' Reset cursor
                pDrawingArea.Window.Cursor = Nothing
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaLeave error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Scroll Events =====
        
        ''' <summary>
        ''' Handles scroll events (mouse wheel)
        ''' </summary>
        Private Function OnDrawingAreaScroll(vSender As Object, vArgs As ScrollEventArgs) As Boolean
            Try
                ' Debug output to verify scroll events are received
                Console.WriteLine($"OnDrawingAreaScroll: Direction={vArgs.Event.Direction}, Ctrl={vArgs.Event.State And ModifierType.ControlMask}")
                
                ' Check for Ctrl key for zoom
                If vArgs.Event.State and ModifierType.ControlMask Then
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
                        ' Scroll up by 3 rows
                        Dim lNewValue As Double = Math.Max(pVScrollBar.Adjustment.Lower, 
                                                          pVScrollBar.Value - (pRowHeight * 3))
                        pVScrollBar.Value = lNewValue
                        Console.WriteLine($"Scrolled up to {lNewValue}")
                        
                    Case ScrollDirection.Down
                        ' Scroll down by 3 rows
                        Dim lMaxValue As Double = pVScrollBar.Adjustment.Upper - pVScrollBar.Adjustment.PageSize
                        Dim lNewValue As Double = Math.Min(lMaxValue, 
                                                          pVScrollBar.Value + (pRowHeight * 3))
                        pVScrollBar.Value = lNewValue
                        Console.WriteLine($"Scrolled down to {lNewValue}")
                        
                    Case ScrollDirection.Left
                        ' Horizontal scroll left
                        pHScrollBar.Value = Math.Max(pHScrollBar.Adjustment.Lower,
                                                    pHScrollBar.Value - 20)
                        
                    Case ScrollDirection.Right
                        ' Horizontal scroll right
                        pHScrollBar.Value = Math.Min(pHScrollBar.Adjustment.Upper - pHScrollBar.Adjustment.PageSize,
                                                    pHScrollBar.Value + 20)
                End Select
                
                ' Return True to indicate we handled the event
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaScroll error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Keyboard Events =====
        
        ''' <summary>
        ''' Handles key press events
        ''' </summary>
        Private Function OnDrawingAreaKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
            Try
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Up
                        NavigateUp()
                        Return True
                        
                    Case Gdk.Key.Down
                        NavigateDown()
                        Return True
                        
                    Case Gdk.Key.Left
                        If pSelectedNode?.IsExpanded Then
                            ToggleNodeExpansion(pSelectedNode)
                        ElseIf pSelectedNode?.Parent IsNot Nothing Then
                            SelectNode(pSelectedNode.Parent)
                        End If
                        Return True
                        
                    Case Gdk.Key.Right
                        If pSelectedNode IsNot Nothing AndAlso Not pSelectedNode.IsExpanded AndAlso pSelectedNode.HasChildren Then
                            ToggleNodeExpansion(pSelectedNode)
                        ElseIf pSelectedNode?.Children.Count > 0 Then
                            SelectNode(pSelectedNode.Children(0))
                        End If
                        Return True
                        
                    Case Gdk.Key.space
                        If pSelectedNode IsNot Nothing AndAlso pSelectedNode.HasChildren Then
                            ToggleNodeExpansion(pSelectedNode)
                        End If
                        Return True
                        
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        If pSelectedNode IsNot Nothing Then
                            HandleNodeActivation(pSelectedNode)
                        End If
                        Return True
                        
                    Case CType(70, Gdk.Key), CType(102, Gdk.Key)  ' 70 = F, 102 = f
                        If (vArgs.Event.State and ModifierType.ControlMask) = ModifierType.ControlMask Then
                            ' Ctrl+F - Start search
                            StartTypeAheadSearch()
                            Return True
                        End If
                        
                    Case Gdk.Key.Escape
                        ' Clear type-ahead buffer
                        pTypeAheadBuffer = ""
                        Return True
                        
                    Case Else
                        ' Type-ahead search
                        If Char.IsLetterOrDigit(Convert.ToChar(vArgs.Event.Key)) Then
                            AddToTypeAhead(Convert.ToChar(vArgs.Event.Key))
                            Return True
                        End If
                End Select
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaKeyPress error: {ex.Message}")
                Return False
            End Try
        End Function

        Public Sub StartTypeAheadSearch()
            ' TODO: Implement StartTypeAheadSearch
        End Sub

        Public Sub AddToTypeAhead(vChar As Char)
            ' TODO: Implement AddToTypeAhead
        End Sub
        
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
        
        ' ===== Event Raising Methods =====
        
        ''' <summary>
        ''' Handles node selection
        ''' </summary>
        Private Sub SelectNode(vNode As VisualNode)
            Try
                If vNode Is Nothing Then Return
                
                pSelectedNode = vNode
                pDrawingArea.QueueDraw()
                
                ' Raise event
                RaiseEvent NodeSelected(vNode.Node)
                
            Catch ex As Exception
                Console.WriteLine($"SelectNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Simplified node double-click handler - only calls HandleNodeActivation
        ''' </summary>
        Private Sub HandleNodeDoubleClick(vNode As VisualNode)
            Try
                If vNode?.Node Is Nothing Then Return
                
                ' Just handle as activation - don't fire NodeDoubleClicked event
                HandleNodeActivation(vNode)
                
            Catch ex As Exception
                Console.WriteLine($"HandleNodeDoubleClick error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles node activation (double-click or Enter key) with fixed field navigation
        ''' </summary>
        ''' <param name="vNode">The visual node that was activated</param>
        ''' <remarks>
        ''' Fixed to navigate to the correct declaration line for fields and other members
        ''' </remarks>
        Private Sub HandleNodeActivation(vNode As VisualNode)
            Try
                If vNode Is Nothing OrElse vNode.Node Is Nothing Then Return
                
                Console.WriteLine($"Node activated: {vNode.Node.Name} ({vNode.Node.NodeType})")
                
                ' Check if this is a partial class/module/structure - don't navigate for these
                ' Partial types have multiple definitions across files, so navigation doesn't make sense
                If vNode.Node.IsPartial Then
                    Console.WriteLine($"Skipping navigation for partial type: {vNode.Node.Name}")
                    
                    ' You could optionally show a message or list of files where this partial type is defined
                    If vNode.Node.Attributes IsNot Nothing AndAlso vNode.Node.Attributes.ContainsKey("FilePaths") Then
                        Dim lFilePaths As String = vNode.Node.Attributes("FilePaths")
                        Console.WriteLine($"  Partial type defined in: {lFilePaths}")
                    End If
                    
                    Return  ' Don't navigate for partial types
                End If
                
                ' Check if the node has a file path (it's from a file)
                If Not String.IsNullOrEmpty(vNode.Node.FilePath) Then
                    ' Determine the line to navigate to based on node type
                    Dim lNavigationLine As Integer = vNode.Node.StartLine
                    
                    ' Adjust for different node types
                    Select Case vNode.Node.NodeType
                        Case CodeNodeType.eClass, 
                             CodeNodeType.eModule,
                             CodeNodeType.eInterface,
                             CodeNodeType.eStructure,
                             CodeNodeType.eEnum
                            ' For type declarations, StartLine should already point to the declaration
                            ' Use it as-is
                            Console.WriteLine($"Navigating to type declaration: {vNode.Node.Name} at line {lNavigationLine + 1}")
                            
                        Case CodeNodeType.eMethod, 
                             CodeNodeType.eFunction, 
                             CodeNodeType.eProperty,
                             CodeNodeType.eConstructor,
                             CodeNodeType.eOperator
                            ' For methods and similar constructs, the parser might point to the first line
                            ' of code inside the method rather than the declaration line
                            ' We need to go back to find the actual declaration
                            
                            ' Check if this is likely pointing to the body rather than the declaration
                            ' For the "New" constructor, if StartLine points to "MyBase.New", we need to go back
                            If lNavigationLine > 0 Then
                                ' Go back one line to get to the declaration
                                ' This handles cases like:
                                ' Line 65: Public Sub New()
                                ' Line 66:     MyBase.New(WINDOW_TITLE)  <-- StartLine points here
                                lNavigationLine -= 1
                            End If
                            
                            Console.WriteLine($"Navigating to method: {vNode.Node.Name} at line {lNavigationLine + 1}")
                            
                        Case CodeNodeType.eField
                            ' Fields often have StartLine pointing to the line after declaration
                            ' Go back one line to get the actual declaration
                            If lNavigationLine > 0 Then
                                lNavigationLine -= 1
                            End If
                            Console.WriteLine($"Navigating to field: {vNode.Node.Name} at line {lNavigationLine + 1}")
                            
                        Case CodeNodeType.eEvent
                            ' Events might also need adjustment
                            If lNavigationLine > 0 Then
                                lNavigationLine -= 1
                            End If
                            Console.WriteLine($"Navigating to event: {vNode.Node.Name} at line {lNavigationLine + 1}")
                            
                        Case Else
                            ' For other node types (delegates, etc.), use StartLine as-is
                            Console.WriteLine($"Navigating to {vNode.Node.NodeType}: {vNode.Node.Name} at line {lNavigationLine + 1}")
                    End Select
                    
                    ' Raise the NavigateToFile event with the adjusted line number
                    ' EditorPosition uses 0-based line numbers
                    RaiseEvent NavigateToFile(vNode.Node.FilePath, 
                                             New EditorPosition(lNavigationLine, 
                                                               vNode.Node.StartColumn))
                End If
                
                ' Don't fire NodeActivated - NavigateToFile is enough
                
            Catch ex As Exception
                Console.WriteLine($"HandleNodeActivation error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Hit Testing =====
        
        ''' <summary>
        ''' Gets the node at the specified position
        ''' </summary>
        Private Function GetNodeAtPosition(vX As Integer, vY As Integer) As VisualNode
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
        
        ''' <summary>
        ''' Determines which zone of a node was clicked
        ''' </summary>
        Private Function GetClickZone(vX As Integer, vY As Integer, vNode As VisualNode) As ClickZone
            Try
                If vNode Is Nothing Then Return ClickZone.eNone
                
                Dim lRelativeX As Integer = vX - vNode.X
                
                ' Check plus/minus zone
                If vNode.HasChildren AndAlso lRelativeX < pPlusMinusSize Then
                    Return ClickZone.ePlusMinus
                End If
                
                ' Adjust for plus/minus space
                If vNode.HasChildren Then
                    lRelativeX -= (pPlusMinusSize + ICON_SPACING)
                End If
                
                ' Check icon zone
                If lRelativeX >= 0 AndAlso lRelativeX < pIconSize Then
                    Return ClickZone.eIcon
                End If
                
                ' Check text zone
                If lRelativeX >= pIconSize + ICON_SPACING Then
                    Return ClickZone.eText
                End If
                
                Return ClickZone.eNone
                
            Catch ex As Exception
                Console.WriteLine($"GetClickZone error: {ex.Message}")
                Return ClickZone.eNone
            End Try
        End Function


        ''' <summary>
        ''' Handles theme changes from the theme manager
        ''' </summary>
        Public Sub OnThemeChanged()
            Try
                ' Simply redraw with new theme colors
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine("ObjectExplorer theme updated")
                
            Catch ex As Exception
                Console.WriteLine($"OnThemeChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles mouse leave events for the drawing area
        ''' </summary>
        ''' <param name="vSender">The drawing area widget</param>
        ''' <param name="vArgs">Leave notify event arguments</param>
        ''' <returns>True if event was handled, False otherwise</returns>
        Private Function OnDrawingAreaLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs) As Boolean
            Try
                ' Clear hover state
                pHoveredNode = Nothing
                
                ' Cancel tooltip timer if running
                If pTooltipTimer <> 0 Then
                    Dim lTimerId As UInteger = pTooltipTimer
                    pTooltipTimer = 0  ' Clear ID before removing to prevent double-removal
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK and expected
                    End Try
                End If
                
                ' Hide any visible tooltip
                HideTooltip()
                
                ' Reset cursor to default
                If pDrawingArea IsNot Nothing AndAlso pDrawingArea.Window IsNot Nothing Then
                    pDrawingArea.Window.Cursor = Nothing
                End If
                
                ' Queue redraw to remove any hover highlighting
                pDrawingArea?.QueueDraw()
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaLeaveNotify error: {ex.Message}")
                Return False
            End Try
        End Function
        

    End Class
    
End Namespace
