' CustomDrawNotebook.Events.vb - Event handling for custom notebook widget
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    Partial Public Class CustomDrawNotebook
        
        ' ===== Event Setup =====
        
        ''' <summary>
        ''' Sets up all event handlers for the notebook
        ''' </summary>
        Private Sub SetupEventHandlers()
            Try
                ' Tab bar events
                AddHandler pTabBar.Drawn, AddressOf OnTabBarDraw
                AddHandler pTabBar.ButtonPressEvent, AddressOf OnTabBarButtonPress
                AddHandler pTabBar.ButtonReleaseEvent, AddressOf OnTabBarButtonRelease
                AddHandler pTabBar.MotionNotifyEvent, AddressOf OnTabBarMotionNotify
                AddHandler pTabBar.LeaveNotifyEvent, AddressOf OnTabBarLeaveNotify
                AddHandler pTabBar.ScrollEvent, AddressOf OnTabBarScroll
                
                ' Navigation button events
                AddHandler pLeftScrollButton.Clicked, AddressOf OnLeftScrollClicked
                AddHandler pRightScrollButton.Clicked, AddressOf OnRightScrollClicked
                AddHandler pDropdownButton.Clicked, AddressOf OnDropdownClicked
                AddHandler pCloseAllButton.Clicked, AddressOf OnCloseAllClicked
                
                ' Window allocation changed
                AddHandler pTabBar.SizeAllocated, AddressOf OnTabBarSizeAllocated
                
            Catch ex As Exception
                Console.WriteLine($"SetupEventHandlers error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Mouse Events =====
        
        ''' <summary>
        ''' Handles mouse button press on the tab bar
        ''' </summary>
        Private Sub OnTabBarButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                vArgs.RetVal = True
                
                Dim lX As Double = vArgs.Event.X
                Dim lY As Double = vArgs.Event.Y
                
                ' Check for right-click (context menu)
                If vArgs.Event.Button = 3 Then
                    Dim lTabIndex As Integer = GetTabAtPosition(lX, lY)
                    If lTabIndex >= 0 Then
                        RaiseEvent TabContextMenuRequested(lTabIndex, lX, lY)
                    End If
                    Return
                End If
                
                ' Left click
                If vArgs.Event.Button = 1 Then
                    ' Check if clicking on a close button
                    Dim lCloseIndex As Integer = GetCloseButtonAtPosition(lX, lY)
                    If lCloseIndex >= 0 Then
                        ' Handle close button click
                        RemovePage(lCloseIndex)
                        Return
                    End If
                    
                    ' Check if clicking on a tab
                    Dim lTabIndex As Integer = GetTabAtPosition(lX, lY)
                    If lTabIndex >= 0 Then
                        ' Switch to the clicked tab
                        SetCurrentTab(lTabIndex)
                        
                        ' Start drag operation
                        pDraggedTabIndex = lTabIndex
                        pDragStartX = lX
                        pDragStartY = lY
                        pDragOffsetX = lX - pTabs(lTabIndex).Bounds.X
                        pIsDragging = False ' Will become true when mouse moves
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTabBarButtonPress error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles mouse button release on the tab bar
        ''' </summary>
        Private Sub OnTabBarButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs)
            Try
                vArgs.RetVal = True
                
                If vArgs.Event.Button = 1 Then
                    ' End drag operation
                    If pIsDragging AndAlso pDraggedTabIndex >= 0 AndAlso pDropTargetIndex >= 0 Then
                        ' Perform the tab reorder
                        If pDropTargetIndex <> pDraggedTabIndex Then
                            ReorderTab(pDraggedTabIndex, pDropTargetIndex)
                        End If
                    End If
                    
                    pIsDragging = False
                    pDraggedTabIndex = -1
                    pDropTargetIndex = -1
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTabBarButtonRelease error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles mouse motion over the tab bar
        ''' </summary>
        Private Sub OnTabBarMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs)
            Try
                vArgs.RetVal = True
                
                Dim lX As Double = vArgs.Event.X
                Dim lY As Double = vArgs.Event.Y
                
                ' Handle dragging
                If pDraggedTabIndex >= 0 Then
                    Dim lDistance As Double = Math.Abs(lX - pDragStartX) + Math.Abs(lY - pDragStartY)
                    If lDistance > 5 Then ' Drag threshold
                        pIsDragging = True
                        
                        ' Calculate drop target
                        pDropTargetIndex = GetDropTargetIndex(lX)
                        pTabBar.QueueDraw()
                    End If
                Else
                    ' Update hover states
                    Dim lOldHovered As Integer = pHoveredTabIndex
                    Dim lOldHoveredClose As Integer = pHoveredCloseIndex
                    
                    pHoveredTabIndex = GetTabAtPosition(lX, lY)
                    pHoveredCloseIndex = GetCloseButtonAtPosition(lX, lY)
                    
                    If lOldHovered <> pHoveredTabIndex OrElse lOldHoveredClose <> pHoveredCloseIndex Then
                        pTabBar.QueueDraw()
                        
                        ' Update cursor
                        If pHoveredCloseIndex >= 0 Then
                            pTabBar.Window.Cursor = New Cursor(CursorType.Hand2)
                        ElseIf pHoveredTabIndex >= 0 Then
                            pTabBar.Window.Cursor = New Cursor(CursorType.Hand2)
                        Else
                            pTabBar.Window.Cursor = Nothing
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTabBarMotionNotify error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles mouse leaving the tab bar
        ''' </summary>
        Private Sub OnTabBarLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            Try
                vArgs.RetVal = True
                
                If pHoveredTabIndex >= 0 OrElse pHoveredCloseIndex >= 0 Then
                    pHoveredTabIndex = -1
                    pHoveredCloseIndex = -1
                    pTabBar.Window.Cursor = Nothing
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTabBarLeaveNotify error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles scroll wheel events on the tab bar
        ''' </summary>
        Private Sub OnTabBarScroll(vSender As Object, vArgs As ScrollEventArgs)
            Try
                vArgs.RetVal = True
                
                ' Scroll through tabs with mouse wheel
                Select Case vArgs.Event.Direction
                    Case ScrollDirection.Up, ScrollDirection.Left
                        ScrollTabs(-30)
                    Case ScrollDirection.Down, ScrollDirection.Right
                        ScrollTabs(30)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnTabBarScroll error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Navigation Button Events =====
        
        ''' <summary>
        ''' Handles left scroll button click
        ''' </summary>
        Private Sub OnLeftScrollClicked(vSender As Object, vArgs As EventArgs)
            Try
                ScrollTabs(-50)
                
            Catch ex As Exception
                Console.WriteLine($"OnLeftScrollClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles right scroll button click
        ''' </summary>
        Private Sub OnRightScrollClicked(vSender As Object, vArgs As EventArgs)
            Try
                ScrollTabs(50)
                
            Catch ex As Exception
                Console.WriteLine($"OnRightScrollClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles dropdown button click to show all tabs menu
        ''' </summary>
        Private Sub OnDropdownClicked(vSender As Object, vArgs As EventArgs)
            Try
                ShowTabsMenu()
                
            Catch ex As Exception
                Console.WriteLine($"OnDropdownClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles close all button click
        ''' </summary>
        Private Sub OnCloseAllClicked(vSender As Object, vArgs As EventArgs)
            Try
                CloseAllTabs()
                
            Catch ex As Exception
                Console.WriteLine($"OnCloseAllClicked error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Size Allocation =====
        
        ''' <summary>
        ''' Handles tab bar size allocation changes
        ''' </summary>
        Private Sub OnTabBarSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            Try
                UpdateScrollButtons()
                UpdateTabBounds()
                
            Catch ex As Exception
                Console.WriteLine($"OnTabBarSizeAllocated error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Gets the tab index at the specified position
        ''' </summary>
        ''' <param name="vX">X coordinate</param>
        ''' <param name="vY">Y coordinate</param>
        ''' <returns>Tab index or -1 if no tab at position</returns>
        Private Function GetTabAtPosition(vX As Double, vY As Double) As Integer
            Try
                ' Account for buttons - don't detect tabs in button area
                Dim lButtonsWidth As Integer = CalculateButtonsWidth()
                Dim lAvailableWidth As Integer = pTabBar.AllocatedWidth - lButtonsWidth
                
                ' If clicking in the button area, return no tab
                If vX > lAvailableWidth Then
                    Return -1
                End If
                
                ' Adjust X for scroll offset
                Dim lAdjustedX As Double = vX + pScrollOffset
                
                ' Find tab at position
                for i As Integer = 0 To pTabs.Count - 1
                    Dim lTab As TabData = pTabs(i)
                    If lTab.IsVisible Then
                        If lAdjustedX >= lTab.Bounds.X AndAlso _
                           lAdjustedX < lTab.Bounds.X + lTab.Bounds.Width AndAlso _
                           vY >= lTab.Bounds.Y AndAlso _
                           vY < lTab.Bounds.Y + lTab.Bounds.Height Then
                            Return i
                        End If
                    End If
                Next
                
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"GetTabAtPosition error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the close button index at the specified position
        ''' </summary>
        ''' <param name="vX">X coordinate</param>
        ''' <param name="vY">Y coordinate</param>
        ''' <returns>Tab index with close button or -1 if none</returns>
        Private Function GetCloseButtonAtPosition(vX As Double, vY As Double) As Integer
            Try
                If Not pShowCloseButtons Then Return -1
                
                ' Account for buttons
                Dim lButtonsWidth As Integer = CalculateButtonsWidth()
                Dim lAvailableWidth As Integer = pTabBar.AllocatedWidth - lButtonsWidth
                
                ' If clicking in the button area, return no close button
                If vX > lAvailableWidth Then
                    Return -1
                End If
                
                ' Adjust X for scroll offset
                Dim lAdjustedX As Double = vX + pScrollOffset
                
                ' Find close button at position
                for i As Integer = 0 To pTabs.Count - 1
                    Dim lTab As TabData = pTabs(i)
                    If lTab.IsVisible AndAlso lTab.CloseBounds.Width > 0 Then
                        ' Adjust close bounds for scroll offset
                        Dim lCloseBounds As Cairo.Rectangle = lTab.CloseBounds
                        If lAdjustedX >= lCloseBounds.X AndAlso _
                           lAdjustedX < lCloseBounds.X + lCloseBounds.Width AndAlso _
                           vY >= lCloseBounds.Y AndAlso _
                           vY < lCloseBounds.Y + lCloseBounds.Height Then
                            Return i
                        End If
                    End If
                Next
                
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"GetCloseButtonAtPosition error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the drop target index for tab reordering
        ''' </summary>
        ''' <param name="vX">X coordinate</param>
        ''' <returns>Target index for dropping the dragged tab</returns>
        Private Function GetDropTargetIndex(vX As Double) As Integer
            Try
                ' Adjust for scroll and find insertion point
                Dim lAdjustedX As Double = vX + pScrollOffset
                
                for i As Integer = 0 To pTabs.Count - 1
                    Dim lTab As TabData = pTabs(i)
                    If lTab.IsVisible Then
                        Dim lTabCenter As Double = lTab.Bounds.X + lTab.Bounds.Width / 2
                        If lAdjustedX < lTabCenter Then
                            Return i
                        End If
                    End If
                Next
                
                Return pTabs.Count - 1
                
            Catch ex As Exception
                Console.WriteLine($"GetDropTargetIndex error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        
        ''' <summary>
        ''' Scrolls the tabs by the specified amount
        ''' </summary>
        ''' <param name="vDelta">Amount to scroll (negative = left, positive = right)</param>
        Private Sub ScrollTabs(vDelta As Integer)
            Try
                Dim lNewOffset As Integer = pScrollOffset + vDelta
                
                ' Clamp to valid range
                lNewOffset = Math.Max(0, Math.Min(lNewOffset, pMaxScrollOffset))
                
                If lNewOffset <> pScrollOffset Then
                    pScrollOffset = lNewOffset
                    UpdateNavigationButtons()
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ScrollTabs error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Reorders a tab from one position to another
        ''' </summary>
        ''' <param name="vFromIndex">Source index</param>
        ''' <param name="vToIndex">Destination index</param>
        Private Sub ReorderTab(vFromIndex As Integer, vToIndex As Integer)
            Try
                If vFromIndex < 0 OrElse vFromIndex >= pTabs.Count Then Return
                If vToIndex < 0 OrElse vToIndex >= pTabs.Count Then Return
                If vFromIndex = vToIndex Then Return
                
                Dim lTab As TabData = pTabs(vFromIndex)
                pTabs.RemoveAt(vFromIndex)
                
                If vToIndex > vFromIndex Then
                    pTabs.Insert(vToIndex, lTab)
                Else
                    pTabs.Insert(vToIndex, lTab)
                End If
                
                ' Adjust current tab index
                If pCurrentTabIndex = vFromIndex Then
                    pCurrentTabIndex = vToIndex
                ElseIf vFromIndex < pCurrentTabIndex AndAlso vToIndex >= pCurrentTabIndex Then
                    pCurrentTabIndex -= 1
                ElseIf vFromIndex > pCurrentTabIndex AndAlso vToIndex <= pCurrentTabIndex Then
                    pCurrentTabIndex += 1
                End If
                
                UpdateTabBounds()
                pTabBar.QueueDraw()
                
                RaiseEvent TabReordered(vFromIndex, vToIndex)
                
            Catch ex As Exception
                Console.WriteLine($"ReorderTab error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace