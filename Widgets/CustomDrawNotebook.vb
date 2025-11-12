' CustomDrawNotebook.vb - Core custom notebook widget implementation
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Editors
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ''' <summary>
    ''' Custom notebook widget with manually drawn tabs, icons, close buttons, and navigation controls
    ''' </summary>
    ''' <remarks>
    ''' Provides enhanced tab functionality including drag-and-drop reordering, context menus,
    ''' and visual indicators for modified files
    ''' </remarks>
    Partial Public Class CustomDrawNotebook
        Inherits Box
        Implements ICustomNotebook
        
        ' ===== Private Fields =====
        'Private pThemeManager As ThemeManager
        Private pTabBar As DrawingArea
        Private pContentArea As Box
        Private pTabs As New List(Of TabData)
        Private pCurrentTabIndex As Integer = -1
        Private pHoveredTabIndex As Integer = -1
        Private pHoveredCloseIndex As Integer = -1
        Private pTabHeight As Integer = 32
        Private pTabMinWidth As Integer = 80
        Private pTabMaxWidth As Integer = 200
        Private pScrollOffset As Integer = 0
        Private pMaxScrollOffset As Integer = 0
        Private pThemeColors As ThemeColors
        Private pTotalTabsWidth As Integer = 0  ' Total width of all visible tabs for scrolling        
        ' Navigation buttons
        Private pLeftScrollButton As Button
        Private pRightScrollButton As Button
        Private pDropdownButton As Button
        Private pCloseAllButton As Button
        Private pHidePanelButton As Button
        Private pShowHidePanelButton As Boolean = False  
        Private pShowCloseButtons As Boolean = True 
        Private pShowCloseAllButton As Boolean = True
        Private pShowDropdownButton As Boolean = True
        Private pScrollButtonMode As ScrollButtonDisplayMode = ScrollButtonDisplayMode.eAuto

                
        ' Drag and drop
        Private pIsDragging As Boolean = False
        Private pDraggedTabIndex As Integer = -1
        Private pDragStartX As Double = 0
        Private pDragStartY As Double = 0
        Private pDragOffsetX As Double = 0
        Private pDropTargetIndex As Integer = -1
        
        ' Animation
        Private pAnimationTimer As UInteger = 0
        Private pAnimationProgress As Double = 0
        Private pAnimationStartOffset As Integer = 0
        Private pAnimationTargetOffset As Integer = 0
        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when the hide panel button is clicked (for bottom panels)
        ''' </summary>
        Public Event HidePanelRequested() Implements ICustomNotebook.HidePanelRequested

        ''' <summary>
        ''' Raised when the current tab changes
        ''' </summary>
        Public Event CurrentTabChanged(vOldIndex As Integer, vNewIndex As Integer) Implements ICustomNotebook.CurrentTabChanged
        
        ''' <summary>
        ''' Raised before a tab is closed, allowing cancellation or custom handling
        ''' </summary>
        ''' <remarks>
        ''' The parent can set Cancel = True to prevent closing, or Handled = True to handle it custom
        ''' </remarks>
        Public Event TabClosing(vSender As Object, vArgs As TabClosingEventArgs) Implements ICustomNotebook.TabClosing
        
        ''' <summary>
        ''' Raised after a tab is closed
        ''' </summary>
        Public Event TabClosed(vIndex As Integer) Implements ICustomNotebook.TabClosed
        
        ''' <summary>
        ''' Raised when tabs are reordered via drag and drop
        ''' </summary>
        Public Event TabReordered(vOldIndex As Integer, vNewIndex As Integer) Implements ICustomNotebook.TabReordered
        
        ''' <summary>
        ''' Raised when a tab requests a context menu
        ''' </summary>
        Public Event TabContextMenuRequested(vIndex As Integer, vX As Double, vY As Double) Implements ICustomNotebook.TabContextMenuRequested
        
        ''' <summary>
        ''' Raised when tab modified state changes
        ''' </summary>
        Public Event TabModifiedChanged(vIndex As Integer, vModified As Boolean) Implements ICustomNotebook.TabModifiedChanged
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Initializes a new instance of the CustomDrawNotebook class
        ''' </summary>
        Public Sub New(vThemeManager As ThemeManager)
            MyBase.New(Orientation.Vertical, 0)
            
            Try
                pThemeManager = vThemeManager
                InitializeComponents()
                SetupEventHandlers()
                
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawNotebook constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Initialization =====
        
        ''' <summary>
        ''' Initializes the notebook components
        ''' </summary>
        Private Sub InitializeComponents()
            Try
                ' Create top toolbar for navigation buttons and tab bar
                Dim lTopBar As New Box(Orientation.Horizontal, 0)
                
                ' LEFT SIDE: Dropdown menu button (moved to first position)
                pDropdownButton = New Button()
                pDropdownButton.Label = "▼"  ' Use Unicode down arrow as text
                pDropdownButton.Relief = ReliefStyle.None
                pDropdownButton.TooltipText = "Show all tabs"
                ' FIXED: Set NoShowAll initially to prevent ShowAll from showing it
                pDropdownButton.NoShowAll = True
                pDropdownButton.Visible = False ' Hidden by default
                lTopBar.PackStart(pDropdownButton, False, False, 0)
                
                ' Left scroll button (now second)
                pLeftScrollButton = New Button()
                pLeftScrollButton.Label = "◀"  ' Use Unicode left arrow as text
                pLeftScrollButton.Relief = ReliefStyle.None
                pLeftScrollButton.TooltipText = "Scroll left"
                pLeftScrollButton.NoShowAll = True
                pLeftScrollButton.Visible = False ' Hidden by default
                lTopBar.PackStart(pLeftScrollButton, False, False, 0)
                
                ' Tab bar drawing area (expandable)
                pTabBar = New DrawingArea()
                pTabBar.CanFocus = True
                pTabBar.Hexpand = True
                pTabBar.HeightRequest = 30 ' Initial height
                
                ' Enable event mask for mouse interaction
                pTabBar.Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                                 EventMask.PointerMotionMask Or EventMask.LeaveNotifyMask Or
                                 EventMask.EnterNotifyMask
                
                lTopBar.PackStart(pTabBar, True, True, 0)
                
                ' RIGHT SIDE buttons (in order from left to right in the right area)
                
                ' Right scroll button
                pRightScrollButton = New Button()
                pRightScrollButton.Label = "▶"  ' Use Unicode right arrow as text
                pRightScrollButton.Relief = ReliefStyle.None
                pRightScrollButton.TooltipText = "Scroll right"
                pRightScrollButton.NoShowAll = True
                pRightScrollButton.Visible = False ' Hidden by default
                lTopBar.PackEnd(pRightScrollButton, False, False, 0)
                
                ' Close all tabs button
                pCloseAllButton = New Button()
                pCloseAllButton.Label = "✕"  ' Use Unicode X as text
                pCloseAllButton.Relief = ReliefStyle.None
                pCloseAllButton.TooltipText = "Close all tabs"
                pCloseAllButton.NoShowAll = True
                pCloseAllButton.Visible = False ' Hidden by default
                lTopBar.PackEnd(pCloseAllButton, False, False, 0)
                
                ' Hide panel button (rightmost) - only visible when ShowHidePanelButton is True
                pHidePanelButton = New Button()
                pHidePanelButton.Label = "−"  ' Use Unicode minus as text
                pHidePanelButton.Relief = ReliefStyle.None
                pHidePanelButton.TooltipText = "Hide panel"
                pHidePanelButton.NoShowAll = True
                pHidePanelButton.Visible = False ' Hidden by default, controlled by property
                lTopBar.PackEnd(pHidePanelButton, False, False, 0)
                
                ' Hook up hide panel button event
                AddHandler pHidePanelButton.Clicked, Sub()
                    RaiseEvent HidePanelRequested()
                End Sub
                
                ' Add top bar to main container
                PackStart(lTopBar, False, False, 0)
                
                ' Create content area for tab pages
                pContentArea = New Box(Orientation.Vertical, 0)
                pContentArea.Hexpand = True
                pContentArea.Vexpand = True
                PackStart(pContentArea, True, True, 0)
                
                ' Initialize tabs collection
                pTabs = New List(Of TabData)()
                
                ' Set initial states
                pCurrentTabIndex = -1
                pHoveredTabIndex = -1
                pHoveredCloseIndex = -1
                pDraggedTabIndex = -1
                pDropTargetIndex = -1
                pScrollOffset = 0
                
                ' Initialize button visibility properties to false
                pShowDropdownButton = False
                pShowCloseAllButton = False
                pShowHidePanelButton = False
                
                Console.WriteLine("CustomDrawNotebook components initialized with event masks enabled")
                Accent(pThemeManager)
            Catch ex As Exception
                Console.WriteLine($"InitializeComponents error: {ex.Message}")
            End Try
        End Sub
                

        
        ' ===== Public Properties =====
        
        ''' <summary>
        ''' Gets or sets the current page index
        ''' </summary>
        Public Property CurrentPage As Integer Implements ICustomNotebook.CurrentPage
            Get
                Return pCurrentTabIndex
            End Get
            Set(value As Integer)
                If value >= 0 AndAlso value < pTabs.Count Then
                    ' Use False for vEnsureVisible to prevent automatic scrolling
                    ' when programmatically setting the current page
                    SetCurrentTab(value, False)
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Gets the number of pages in the notebook
        ''' </summary>
        Public ReadOnly Property NPages As Integer Implements ICustomNotebook.NPages
            Get
                Return pTabs.Count
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets the theme colors for rendering
        ''' </summary>
        Public Property ThemeColors As ThemeColors
            Get
                Return pThemeColors
            End Get
            Set(value As ThemeColors)
                pThemeColors = value
                QueueDraw()
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets whether the hide panel button should be shown
        ''' </summary>
        Public Property ShowHidePanelButton As Boolean
            Get
                Return pShowHidePanelButton
            End Get
            Set(value As Boolean)
                pShowHidePanelButton = value
                
                ' Update button visibility and refresh layout
                UpdateNavigationButtons()
                UpdateTabBounds()
            End Set
        End Property
        
        ' Replace: SimpleIDE.Widgets.CustomDrawNotebook.ShowScrollButtons
        ''' <summary>
        ''' Gets or sets whether scroll buttons should be shown (deprecated - use ScrollButtonMode instead)
        ''' </summary>
        ''' <remarks>
        ''' This property is maintained for backward compatibility.
        ''' Setting to True sets ScrollButtonMode to eAuto.
        ''' Setting to False sets ScrollButtonMode to eNever.
        ''' Use ScrollButtonMode property for more control.
        ''' </remarks>
        <Obsolete("Use ScrollButtonMode property instead for more control")>
        Public Property ShowScrollButtons As Boolean
            Get
                ' Return true if mode is not eNever
                Return pScrollButtonMode <> ScrollButtonDisplayMode.eNever
            End Get
            Set(value As Boolean)
                ' Map boolean to appropriate mode
                If value Then
                    ScrollButtonMode = ScrollButtonDisplayMode.eAuto
                Else
                    ScrollButtonMode = ScrollButtonDisplayMode.eNever
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether close buttons are shown on tabs
        ''' </summary>
        ''' <remarks>
        ''' When set to False, tabs cannot be closed via the X button.
        ''' Useful for panels where tabs should be permanent.
        ''' </remarks>
        Public Property ShowTabCloseButtons As Boolean
            Get
                Return pShowCloseButtons
            End Get
            Set(value As Boolean)
                pShowCloseButtons = value

                ' Update button visibility and refresh layout
                UpdateNavigationButtons()
                UpdateTabBounds()
            End Set
        End Property      

        ''' <summary>
        ''' Gets or sets whether the close all button should be shown
        ''' </summary>
        Public Property ShowCloseAllButton As Boolean
            Get
                Return pShowCloseAllButton
            End Get
            Set(value As Boolean)
                pShowCloseAllButton = value
                
                ' Update button visibility and refresh layout
                UpdateNavigationButtons()
                UpdateTabBounds()
            End Set
        End Property         

        ' ===== Public Methods =====

        ''' <summary>
        ''' Appends a new page to the notebook
        ''' </summary>
        ''' <param name="vWidget">The widget to display in the page</param>
        ''' <param name="vLabel">The label text for the tab</param>
        ''' <param name="vIconName">Optional icon name for the tab</param>
        ''' <returns>The index of the new page</returns>
        ''' <remarks>
        ''' Adds a new tab to the notebook and focuses it if it's the first tab
        ''' </remarks>
        Public Overloads Function AppendPage(vWidget As Widget, vLabel As String, Optional vIconName As String = Nothing) As Integer Implements ICustomNotebook.AppendPage
            Try
                Dim lTab As New TabData() with {
                    .Widget = vWidget,
                    .Label = vLabel,
                    .IconName = vIconName,
                    .Modified = False,
                    .IsVisible = True,
                    .Bounds = New Cairo.Rectangle()
                }
                
                pTabs.Add(lTab)
                Dim lNewIndex As Integer = pTabs.Count - 1
                
                ' CRITICAL: Set NoShowAll to True to prevent ShowAll from making it visible
                vWidget.NoShowAll = True
                
                ' Add new widget to content area but ensure it's hidden initially
                pContentArea.PackStart(vWidget, True, True, 0)
                vWidget.Hide() ' Explicitly hide
                ' twice-ensure it's hidden
                vWidget.Visible = False 
                
                ' Set as current if it's the first tab
                If pTabs.Count = 1 Then
                    ' Reset scroll offset for first tab
                    pScrollOffset = 0
                    
                    SetCurrentTab(0)
                    
                    ' CRITICAL: For the first tab, we need to ensure it's shown
                    vWidget.NoShowAll = False
                    vWidget.ShowAll()
                    vWidget.NoShowAll = True
                    vWidget.Visible = True
                End If
                
                ' Refresh the entire layout (tab bounds and navigation buttons)
                RefreshLayout()
                
                Console.WriteLine($"AppendPage: Added tab '{vLabel}' at index {lNewIndex}")
                Console.WriteLine($"  Tab bounds: X={lTab.Bounds.X}, Y={lTab.Bounds.Y}, W={lTab.Bounds.Width}, H={lTab.Bounds.Height}")
                Console.WriteLine($"  ScrollOffset={pScrollOffset}, TabCount={pTabs.Count}")
                
                Return lNewIndex
                
            Catch ex As Exception
                Console.WriteLine($"AppendPage error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ''' <summary>
        ''' Removes a page from the notebook with enhanced event handling
        ''' </summary>
        ''' <param name="vIndex">Index of the page to remove</param>
        ''' <remarks>
        ''' This method now raises TabClosing event that allows the parent to:
        ''' 1. Cancel the close (Cancel = True)
        ''' 2. Handle it custom (Handled = True) - e.g., hide panel instead of removing tab
        ''' </remarks>
        Public Sub RemovePage(vIndex As Integer) Implements ICustomNotebook.RemovePage
            Try
                If vIndex < 0 OrElse vIndex >= pTabs.Count Then Return
                
                ' Get tab data before potential removal
                Dim lTab As TabData = pTabs(vIndex)
                
                ' Create and raise the TabClosing event
                Dim lEventArgs As New TabClosingEventArgs(vIndex, lTab.Widget, lTab.Label)
                RaiseEvent TabClosing(Me, lEventArgs)
                
                ' Check if the close was cancelled
                If lEventArgs.Cancel Then 
                    Console.WriteLine($"Tab close cancelled for index {vIndex}")
                    Return
                End If
                
                ' Check if the parent handled the close (e.g., hiding panel instead)
                If lEventArgs.Handled Then
                    Console.WriteLine($"Tab close handled by parent for index {vIndex}")
                    ' Parent handled it, so we don't remove the tab
                    Return
                End If
                
                ' If we get here, proceed with normal tab removal
                Console.WriteLine($"Removing tab at index {vIndex}")
                
                ' Store whether we need to select a new tab
                Dim lNeedNewSelection As Boolean = (vIndex = pCurrentTabIndex)
                Dim lOldTabCount As Integer = pTabs.Count
                
                ' CRITICAL FIX: Ensure widget is properly unparented before removal
                ' This is necessary because GTK sometimes doesn't properly unparent widgets
                If lTab.Widget IsNot Nothing Then
                    ' First, hide the widget
                    lTab.Widget.Hide()
                    lTab.Widget.Visible = False
                    
                    ' Remove from content area
                    pContentArea.Remove(lTab.Widget)
                    
                    ' CRITICAL: Explicitly unparent the widget
                    ' This ensures GTK releases all internal references
                    If lTab.Widget.Parent IsNot Nothing Then
                        lTab.Widget.Unparent()
                    End If
                End If
                
                ' Clean up custom tab labels if any
                If pCustomTabLabels.ContainsKey(lTab.Widget) Then
                    pCustomTabLabels.Remove(lTab.Widget)
                End If
                
                ' Remove from list
                pTabs.RemoveAt(vIndex)
                
                ' Adjust current tab index
                If lOldTabCount = 1 Then
                    ' Last tab was removed
                    pCurrentTabIndex = -1
                ElseIf lNeedNewSelection Then
                    ' Current tab was removed, select a new one
                    If vIndex < pTabs.Count Then
                        ' Select the tab that moved into this position
                        pCurrentTabIndex = -1 ' Reset first to force proper switching
                        SetCurrentTab(vIndex)
                    Else
                        ' We removed the last tab, select the new last tab
                        pCurrentTabIndex = -1 ' Reset first to force proper switching
                        SetCurrentTab(pTabs.Count - 1)
                    End If
                ElseIf pCurrentTabIndex > vIndex Then
                    ' Adjust index if current tab was after the removed one
                    pCurrentTabIndex -= 1
                End If
                
                ' Reset scroll if needed
                If pTabs.Count = 0 Then
                    pScrollOffset = 0
                End If
 
                ' Refresh the entire layout (tab bounds and navigation buttons)
                RefreshLayout()
                
                ' Update UI
                UpdateScrollButtons()
                pTabBar.QueueDraw()
                
                ' Raise TabClosed event
                RaiseEvent TabClosed(vIndex)
                
                Console.WriteLine($"Tab removal completed. Remaining tabs: {pTabs.Count}")
                
            Catch ex As Exception
                Console.WriteLine($"RemovePage error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the widget at the specified page index
        ''' </summary>
        ''' <param name="vIndex">The page index</param>
        ''' <returns>The widget at the specified index, or Nothing if invalid</returns>
        Public Function GetNthPage(vIndex As Integer) As Widget Implements ICustomNotebook.GetNthPage
            Try
                If vIndex >= 0 AndAlso vIndex < pTabs.Count Then
                    Return pTabs(vIndex).Widget
                End If
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetNthPage error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Sets the modified state for a tab
        ''' </summary>
        ''' <param name="vIndex">The tab index</param>
        ''' <param name="vModified">True if modified, False otherwise</param>
        Public Sub SetTabModified(vIndex As Integer, vModified As Boolean) Implements ICustomNotebook.SetTabModified
            Try
                If vIndex >= 0 AndAlso vIndex < pTabs.Count Then
                    If pTabs(vIndex).Modified <> vModified Then
                        pTabs(vIndex).Modified = vModified
                        
                        ' Update tab bounds since modified indicator affects width
                        UpdateTabBounds()
                        
                        ' Redraw tab bar
                        pTabBar.QueueDraw()
                        
                        ' Raise event
                        RaiseEvent TabModifiedChanged(vIndex, vModified)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetTabModified error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the icon for a tab
        ''' </summary>
        ''' <param name="vIndex">The tab index</param>
        ''' <param name="vIconName">The icon name</param>
        Public Sub SetTabIcon(vIndex As Integer, vIconName As String) Implements ICustomNotebook.SetTabIcon
            Try
                If vIndex >= 0 AndAlso vIndex < pTabs.Count Then
                    pTabs(vIndex).IconName = vIconName
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetTabIcon error: {ex.Message}")
            End Try
        End Sub        
        
        
        ''' <summary>
        ''' Finds the page index for a widget
        ''' </summary>
        ''' <param name="vWidget">The widget to find</param>
        ''' <returns>The page index, or -1 if not found</returns>
        Public Function PageNum(vWidget As Widget) As Integer Implements ICustomNotebook.PageNum
            Try
                for i As Integer = 0 To pTabs.Count - 1
                    If pTabs(i).Widget Is vWidget Then
                        Return i
                    End If
                Next
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"PageNum error: {ex.Message}")
                Return -1
            End Try
        End Function        

        
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Sets the current tab and optionally scrolls to make it visible
        ''' </summary>
        ''' <param name="vIndex">Index of the tab to select</param>
        ''' <param name="vEnsureVisible">If True, scrolls to make tab visible (default True)</param>
        Public Sub SetCurrentTab(vIndex As Integer, Optional vEnsureVisible As Boolean = True)
            Try
                If vIndex < 0 OrElse vIndex >= pTabs.Count Then Return
                If vIndex = pCurrentTabIndex Then Return ' Already selected
                
                Dim lOldIndex As Integer = pCurrentTabIndex
                
                ' Hide old tab's widget
                If pCurrentTabIndex >= 0 AndAlso pCurrentTabIndex < pTabs.Count Then
                    Dim lOldWidget As Widget = pTabs(pCurrentTabIndex).Widget
                    If lOldWidget IsNot Nothing Then
                        lOldWidget.Hide()
                        lOldWidget.Visible = False
                    End If
                End If
                
                ' Update current index
                pCurrentTabIndex = vIndex
                
                ' Show new tab's widget
                Dim lNewWidget As Widget = pTabs(vIndex).Widget
                If lNewWidget IsNot Nothing Then
                    lNewWidget.Show()
                    lNewWidget.Visible = True
                    
                    ' Find and focus first focusable widget
                    FocusFirstFocusableChild(lNewWidget)
                End If
                
                ' FIXED: Update tab bounds when current tab changes
                ' This ensures the tab widths are recalculated with the correct font weight
                ' (bold for the new current tab, regular for the old current tab)
                UpdateTabBounds()
                
                ' Only ensure tab is visible if requested
                ' This prevents unwanted scrolling when programmatically switching tabs
                If vEnsureVisible Then
                    EnsureTabVisible(vIndex)
                End If
                
                ' Queue redraw to update the tab bar
                pTabBar.QueueDraw()
                
                ' Raise event
                RaiseEvent CurrentTabChanged(lOldIndex, vIndex)
                
            Catch ex As Exception
                Console.WriteLine($"SetCurrentTab error: {ex.Message}")
            End Try
        End Sub
        
        
        ''' <summary>
        ''' Ensures a tab is visible by scrolling if necessary
        ''' </summary>
        ''' <param name="vIndex">The tab index to make visible</param>
        Private Sub EnsureTabVisible(vIndex As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pTabs.Count Then Return
                
                Dim lTab As TabData = pTabs(vIndex)
                If lTab.Bounds.Width = 0 Then Return ' Not laid out yet
                
                ' Calculate space taken by visible buttons
                Dim lButtonsWidth As Integer = CalculateButtonsWidth()
                
                ' Calculate available width for tabs
                Dim lAvailableWidth As Integer = 0
                If pTabBar IsNot Nothing Then
                    lAvailableWidth = Math.Max(0, pTabBar.AllocatedWidth - lButtonsWidth)
                End If
                
                Dim lTabLeft As Integer = lTab.Bounds.X - pScrollOffset
                Dim lTabRight As Integer = lTabLeft + lTab.Bounds.Width
                
                If lTabLeft < 0 Then
                    ' Tab is off the left edge, scroll right
                    AnimateScrollTo(pScrollOffset + lTabLeft - 10)
                ElseIf lTabRight > lAvailableWidth Then
                    ' Tab is off the right edge (accounting for buttons), scroll left
                    AnimateScrollTo(pScrollOffset + (lTabRight - lAvailableWidth) + 10)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"EnsureTabVisible error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Finds and focuses the first focusable child widget
        ''' </summary>
        ''' <param name="vContainer">Container widget to search</param>
        ''' <returns>True if a focusable widget was found and focused</returns>
        ''' <remarks>
        ''' Recursively searches for a focusable widget and gives it focus.
        ''' Useful for containers like Box, ScrolledWindow, etc.
        ''' </remarks>
        Private Function FocusFirstFocusableChild(vContainer As Widget) As Boolean
            Try
                ' Check if this widget can take focus
                If vContainer.CanFocus Then
                    vContainer.GrabFocus()
                    Return True
                End If
                
                ' Special handling for common containers
                If TypeOf vContainer Is CustomDrawingEditor Then
                    ' For custom editor, focus the main widget
                    Dim lEditor As CustomDrawingEditor = CType(vContainer, CustomDrawingEditor)
                    If lEditor.Widget IsNot Nothing AndAlso lEditor.Widget.CanFocus Then
                        lEditor.Widget.GrabFocus()
                        Return True
                    End If
                    
                ElseIf TypeOf vContainer Is CustomDrawDataGrid Then
                    ' For custom data grid, grab focus directly
                    vContainer.GrabFocus()
                    Return True
                    
                ElseIf TypeOf vContainer Is CustomDrawListBox Then
                    ' For custom list box, grab focus directly
                    vContainer.GrabFocus()
                    Return True
                    
                ElseIf TypeOf vContainer Is CustomDrawObjectExplorer Then
                    ' For object explorer, focus the drawing area
                    Dim lExplorer As CustomDrawObjectExplorer = CType(vContainer, CustomDrawObjectExplorer)
                    lExplorer.GrabFocus()
                    ' The explorer is a Box, so recurse into it
                    Return True
                    
                ElseIf TypeOf vContainer Is CustomDrawProjectExplorer Then
                    ' For project explorer, focus the drawing area
                    Dim lExplorer As CustomDrawProjectExplorer = CType(vContainer, CustomDrawProjectExplorer)
                    ' The explorer is a Box, so recurse into it
                    lExplorer.GrabFocus()
                    Return True
                    
                ElseIf TypeOf vContainer Is TextView Then
                    ' TextView should be directly focusable
                    vContainer.GrabFocus()
                    Return True
                    
                ElseIf TypeOf vContainer Is TreeView Then
                    ' TreeView should be directly focusable
                    vContainer.GrabFocus()
                    Return True
                    
                ElseIf TypeOf vContainer Is Entry Then
                    ' Entry should be directly focusable
                    vContainer.GrabFocus()
                    Return True
                    
                ElseIf TypeOf vContainer Is DrawingArea Then
                    ' DrawingArea might be focusable if configured
                    If vContainer.CanFocus Then
                        vContainer.GrabFocus()
                        Return True
                    End If

                ElseIf TypeOf vContainer Is ScrolledWindow Then
                    Dim lScrolled As ScrolledWindow = CType(vContainer, ScrolledWindow)
                    If lScrolled.Child IsNot Nothing Then
                        Return FocusFirstFocusableChild(lScrolled.Child)
                    End If
                    
                ElseIf TypeOf vContainer Is Box Then
                    Dim lBox As Box = CType(vContainer, Box)
                    for each lChild in lBox.Children
                        If FocusFirstFocusableChild(lChild) Then
                            Return True
                        End If
                    Next
                    
                ElseIf TypeOf vContainer Is Paned Then
                    Dim lPaned As Paned = CType(vContainer, Paned)
                    If lPaned.Child1 IsNot Nothing Then
                        If FocusFirstFocusableChild(lPaned.Child1) Then
                            Return True
                        End If
                    End If
                    If lPaned.Child2 IsNot Nothing Then
                        If FocusFirstFocusableChild(lPaned.Child2) Then
                            Return True
                        End If
                    End If
                    
                ElseIf TypeOf vContainer Is Frame Then
                    Dim lFrame As Frame = CType(vContainer, Frame)
                    If lFrame.Child IsNot Nothing Then
                        Return FocusFirstFocusableChild(lFrame.Child)
                    End If
                    
                ElseIf TypeOf vContainer Is Viewport Then
                    Dim lViewport As Viewport = CType(vContainer, Viewport)
                    If lViewport.Child IsNot Nothing Then
                        Return FocusFirstFocusableChild(lViewport.Child)
                    End If
                    
                ElseIf TypeOf vContainer Is Bin Then
                    ' Generic Bin container (parent of many container types)
                    Dim lBin As Bin = CType(vContainer, Bin)
                    If lBin.Child IsNot Nothing Then
                        Return FocusFirstFocusableChild(lBin.Child)
                    End If
                    
                ElseIf TypeOf vContainer Is Container Then
                    ' Generic container - iterate through children
                    Dim lContainer As Container = CType(vContainer, Container)
                    for each lChild in lContainer.Children
                        If FocusFirstFocusableChild(lChild) Then
                            Return True
                        End If
                    Next
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"FocusFirstFocusableChild error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Updates the visibility of scroll buttons based on tab overflow
        ''' </summary>
        ''' <remarks>
        ''' This method now respects the ShowScrollButtons property setting
        ''' </remarks>
        Private Sub UpdateScrollButtons()
            Try
                ' If scroll buttons are disabled via property, ensure they stay hidden
                If pLeftScrollButton?.NoShowAll AndAlso pRightScrollButton?.NoShowAll Then
                    Return
                End If
                
                Dim lTotalWidth As Integer = CalculateTotalTabWidth()
                Dim lVisibleWidth As Integer = pTabBar.AllocatedWidth
                
                Dim lNeedScroll As Boolean = lTotalWidth > lVisibleWidth
                
                ' Only show buttons if scrolling is needed AND they're not disabled
                If pLeftScrollButton IsNot Nothing AndAlso Not pLeftScrollButton.NoShowAll Then
                    pLeftScrollButton.Visible = lNeedScroll AndAlso pScrollOffset > 0
                End If
                
                If pRightScrollButton IsNot Nothing AndAlso Not pRightScrollButton.NoShowAll Then
                    pRightScrollButton.Visible = lNeedScroll AndAlso pScrollOffset < (lTotalWidth - lVisibleWidth)
                End If
                
                pMaxScrollOffset = Math.Max(0, lTotalWidth - lVisibleWidth)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScrollButtons error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Calculates the total width of all tabs
        ''' </summary>
        ''' <returns>The total width in pixels</returns>
        Private Function CalculateTotalTabWidth() As Integer
            Try
                Dim lTotalWidth As Integer = 0
                for i As Integer = 0 To pTabs.Count - 1
                    lTotalWidth += CalculateTabWidth(i)
                Next
                Return lTotalWidth
                
            Catch ex As Exception
                Console.WriteLine($"CalculateTotalTabWidth error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the width for each tab based on content
        ''' </summary>
        ''' <returns>The tab width in pixels</returns>
        Private Function GetTabWidth() As Integer
            Try
                ' This method should NOT be used for actual tab width calculation
                ' It's only for legacy compatibility
                Return pTabMaxWidth
                
            Catch ex As Exception
                Console.WriteLine($"GetTabWidth error: {ex.Message}")
                Return pTabMaxWidth
            End Try
        End Function
        
        ''' <summary>
        ''' Calculates the width needed for a specific tab based on its content
        ''' </summary>
        ''' <param name="vTabIndex">Index of the tab</param>
        ''' <returns>Required width in pixels</returns>
        Private Function CalculateTabWidth(vTabIndex As Integer) As Integer
            Try
                If vTabIndex < 0 OrElse vTabIndex >= pTabs.Count Then
                    Return pTabMinWidth
                End If
                
                Dim lTab As TabData = pTabs(vTabIndex)
                Dim lWidth As Integer = TAB_PADDING * 2 ' Left and right padding
                
                ' Add icon width if present
                If Not String.IsNullOrEmpty(lTab.IconName) Then
                    lWidth += ICON_SIZE + 4
                End If
                
                ' Add modified indicator width if modified
                If lTab.Modified Then
                    lWidth += MODIFIED_DOT_SIZE + 4
                End If
                
                ' Calculate text width with markup support
                Dim lLayout As Pango.Layout = pTabBar.CreatePangoLayout("")
                
                ' Check if the label contains markup
                Dim lHasMarkup As Boolean = lTab.Label.Contains("<span") OrElse lTab.Label.Contains("<b>") OrElse 
                                            lTab.Label.Contains("<i>") OrElse lTab.Label.Contains("<u>")
                
                If lHasMarkup Then
                    ' Use SetMarkup for text with markup
                    Try
                        lLayout.SetMarkup(lTab.Label)
                    Catch ex As Exception
                        ' Fall back to plain text if markup parsing fails
                        lLayout.SetText(lTab.Label)
                    End Try
                Else
                    ' Use SetText for plain text
                    lLayout.SetText(lTab.Label)
                End If
                
                ' Set font
                Dim lFontDesc As New Pango.FontDescription()
                lFontDesc.Family = "Sans"
                lFontDesc.Size = Pango.Units.FromPixels(10)
                
                ' CRITICAL: Set font weight to bold if this is the current tab
                ' This ensures the calculated width matches the rendered width
                If vTabIndex = pCurrentTabIndex Then
                    lFontDesc.Weight = Pango.Weight.Bold
                Else
                    lFontDesc.Weight = Pango.Weight.Normal
                End If   
                             
                lLayout.FontDescription = lFontDesc
 
                ' CRITICAL: NO WIDTH LIMIT for measurement
                lLayout.Width = -1  ' No width limit
                lLayout.Ellipsize = Pango.EllipsizeMode.None  ' No ellipsizing
                       
                Dim lTextWidth, lTextHeight As Integer
                lLayout.GetPixelSize(lTextWidth, lTextHeight)
                
                ' Add the FULL text width plus padding
                lWidth += lTextWidth + 16  ' Extra padding to ensure no clipping
                        
                ' Add close button width if close buttons are shown
                If pShowCloseButtons Then
                    lWidth += CLOSE_BUTTON_SIZE + 8
                End If
                
                ' Ensure within min/max bounds
                lWidth = Math.Max(pTabMinWidth, Math.Min(pTabMaxWidth * 2, lWidth)) ' Allow up to 2x max for long names
                
                Return lWidth
                
            Catch ex As Exception
                Console.WriteLine($"CalculateTabWidth error: {ex.Message}")
                Return pTabMaxWidth
            End Try
        End Function
        
        
        ''' <summary>
        ''' Loads theme colors from the theme manager
        ''' </summary>
        Private Sub LoadThemeColors()
            Try
                ' Initialize with default colors
                pThemeColors = New ThemeColors()
                
                ' Check if we have a theme manager
                If pThemeManager IsNot Nothing Then
                    Dim lCurrentTheme As String = pThemeManager.GetCurrentTheme
                    
                    Select Case lCurrentTheme.ToLower()
                        Case "dark", "monokai", "dracula", "one-dark"
                            ' Dark theme colors
                            pThemeColors.Background = New Gdk.RGBA() with {.Red = 0.15, .Green = 0.15, .Blue = 0.18, .Alpha = 1}
                            pThemeColors.EditorBackground = New Gdk.RGBA() with {.Red = 0.12, .Green = 0.12, .Blue = 0.14, .Alpha = 1}
                            pThemeColors.TabInactive = New Gdk.RGBA() with {.Red = 0.18, .Green = 0.18, .Blue = 0.22, .Alpha = 1}
                            pThemeColors.TabHover = New Gdk.RGBA() with {.Red = 0.22, .Green = 0.22, .Blue = 0.26, .Alpha = 1}
                            pThemeColors.Border = New Gdk.RGBA() with {.Red = 0.3, .Green = 0.3, .Blue = 0.35, .Alpha = 1}
                            pThemeColors.Text = New Gdk.RGBA() with {.Red = 0.9, .Green = 0.9, .Blue = 0.9, .Alpha = 1}
                            pThemeColors.TextInactive = New Gdk.RGBA() with {.Red = 0.6, .Green = 0.6, .Blue = 0.6, .Alpha = 1}
                            pThemeColors.ModifiedIndicator = New Gdk.RGBA() with {.Red = 0.9, .Green = 0.3, .Blue = 0.3, .Alpha = 1}
                            pThemeColors.Accent = New Gdk.RGBA() with {.Red = 0.3, .Green = 0.6, .Blue = 0.9, .Alpha = 1}
                            
                        Case "solarized-dark"
                            ' Solarized Dark colors
                            pThemeColors.Background = New Gdk.RGBA() with {.Red = 0.0, .Green = 0.17, .Blue = 0.21, .Alpha = 1}
                            pThemeColors.EditorBackground = New Gdk.RGBA() with {.Red = 0.0, .Green = 0.13, .Blue = 0.18, .Alpha = 1}
                            pThemeColors.TabInactive = New Gdk.RGBA() with {.Red = 0.03, .Green = 0.21, .Blue = 0.26, .Alpha = 1}
                            pThemeColors.TabHover = New Gdk.RGBA() with {.Red = 0.05, .Green = 0.25, .Blue = 0.31, .Alpha = 1}
                            pThemeColors.Border = New Gdk.RGBA() with {.Red = 0.03, .Green = 0.31, .Blue = 0.36, .Alpha = 1}
                            pThemeColors.Text = New Gdk.RGBA() with {.Red = 0.51, .Green = 0.58, .Blue = 0.59, .Alpha = 1}
                            pThemeColors.TextInactive = New Gdk.RGBA() with {.Red = 0.35, .Green = 0.43, .Blue = 0.46, .Alpha = 1}
                            pThemeColors.ModifiedIndicator = New Gdk.RGBA() with {.Red = 0.86, .Green = 0.2, .Blue = 0.18, .Alpha = 1}
                            pThemeColors.Accent = New Gdk.RGBA() with {.Red = 0.15, .Green = 0.55, .Blue = 0.82, .Alpha = 1}
                            
                        Case Else
                            ' Light theme colors (default)
                            pThemeColors.Background = New Gdk.RGBA() with {.Red = 0.94, .Green = 0.94, .Blue = 0.94, .Alpha = 1}
                            pThemeColors.EditorBackground = New Gdk.RGBA() with {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1}
                            pThemeColors.TabInactive = New Gdk.RGBA() with {.Red = 0.9, .Green = 0.9, .Blue = 0.9, .Alpha = 1}
                            pThemeColors.TabHover = New Gdk.RGBA() with {.Red = 0.96, .Green = 0.96, .Blue = 0.96, .Alpha = 1}
                            pThemeColors.Border = New Gdk.RGBA() with {.Red = 0.7, .Green = 0.7, .Blue = 0.7, .Alpha = 1}
                            pThemeColors.Text = New Gdk.RGBA() with {.Red = 0.1, .Green = 0.1, .Blue = 0.1, .Alpha = 1}
                            pThemeColors.TextInactive = New Gdk.RGBA() with {.Red = 0.4, .Green = 0.4, .Blue = 0.4, .Alpha = 1}
                            pThemeColors.ModifiedIndicator = New Gdk.RGBA() with {.Red = 0.8, .Green = 0.2, .Blue = 0.2, .Alpha = 1}
                            pThemeColors.Accent = New Gdk.RGBA() with {.Red = 0.2, .Green = 0.5, .Blue = 0.8, .Alpha = 1}
                    End Select
                Else
                    ' Fallback default colors if no theme manager
                    pThemeColors.Background = New Gdk.RGBA() with {.Red = 0.94, .Green = 0.94, .Blue = 0.94, .Alpha = 1}
                    pThemeColors.EditorBackground = New Gdk.RGBA() with {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1}
                    pThemeColors.TabInactive = New Gdk.RGBA() with {.Red = 0.9, .Green = 0.9, .Blue = 0.9, .Alpha = 1}
                    pThemeColors.TabHover = New Gdk.RGBA() with {.Red = 0.96, .Green = 0.96, .Blue = 0.96, .Alpha = 1}
                    pThemeColors.Border = New Gdk.RGBA() with {.Red = 0.7, .Green = 0.7, .Blue = 0.7, .Alpha = 1}
                    pThemeColors.Text = New Gdk.RGBA() with {.Red = 0.1, .Green = 0.1, .Blue = 0.1, .Alpha = 1}
                    pThemeColors.TextInactive = New Gdk.RGBA() with {.Red = 0.4, .Green = 0.4, .Blue = 0.4, .Alpha = 1}
                    pThemeColors.ModifiedIndicator = New Gdk.RGBA() with {.Red = 0.8, .Green = 0.2, .Blue = 0.2, .Alpha = 1}
                    pThemeColors.Accent = New Gdk.RGBA() with {.Red = 0.2, .Green = 0.5, .Blue = 0.8, .Alpha = 1}
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadThemeColors error: {ex.Message}")
                
                ' Ensure we have valid colors even if there's an error
                pThemeColors.Background = New Gdk.RGBA() with {.Red = 0.94, .Green = 0.94, .Blue = 0.94, .Alpha = 1}
                pThemeColors.EditorBackground = New Gdk.RGBA() with {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1}
                pThemeColors.TabInactive = New Gdk.RGBA() with {.Red = 0.9, .Green = 0.9, .Blue = 0.9, .Alpha = 1}
                pThemeColors.TabHover = New Gdk.RGBA() with {.Red = 0.96, .Green = 0.96, .Blue = 0.96, .Alpha = 1}
                pThemeColors.Border = New Gdk.RGBA() with {.Red = 0.7, .Green = 0.7, .Blue = 0.7, .Alpha = 1}
                pThemeColors.Text = New Gdk.RGBA() with {.Red = 0.1, .Green = 0.1, .Blue = 0.1, .Alpha = 1}
                pThemeColors.TextInactive = New Gdk.RGBA() with {.Red = 0.4, .Green = 0.4, .Blue = 0.4, .Alpha = 1}
                pThemeColors.ModifiedIndicator = New Gdk.RGBA() with {.Red = 0.8, .Green = 0.2, .Blue = 0.2, .Alpha = 1}
                pThemeColors.Accent = New Gdk.RGBA() with {.Red = 0.2, .Green = 0.5, .Blue = 0.8, .Alpha = 1}
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the theme manager for dynamic theme updates
        ''' </summary>
        ''' <param name="vThemeManager">The theme manager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                
                ' Subscribe to theme change events if available
                If pThemeManager IsNot Nothing Then
                    AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                End If
                
                ' Load current theme colors
                LoadThemeColors()
                
                ' Redraw with new colors
                pTabBar?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SetThemeManager error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles theme change events
        ''' </summary>
        Private Sub OnThemeChanged(vThemeName As String)
            Try
                LoadThemeColors()
                pTabBar?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnThemeChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets or sets whether the dropdown button should be shown
        ''' </summary>
        ''' <remarks>
        ''' When set to False, the dropdown button is completely hidden and doesn't take up any space
        ''' </remarks>
        Public Property ShowDropdownButton As Boolean
            Get
                Return pShowDropdownButton
            End Get
            Set(value As Boolean)
                pShowDropdownButton = value
                
                If pDropdownButton IsNot Nothing Then
                    If value Then
                        ' Button can be shown (visibility will be controlled by UpdateNavigationButtons)
                        pDropdownButton.NoShowAll = False
                    Else
                        ' Button should never be shown
                        pDropdownButton.Hide()
                        pDropdownButton.Visible = False
                        pDropdownButton.NoShowAll = True
                    End If
                End If
                
                ' Update navigation buttons and then tab bounds
                UpdateNavigationButtons()
                UpdateTabBounds()
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets the display mode for scroll buttons
        ''' </summary>
        ''' <value>The scroll button display mode</value>
        ''' <remarks>
        ''' eNever: Never show scroll buttons
        ''' eAuto: Show only when tabs exceed available space (default)
        ''' eAlways: Always show scroll buttons
        ''' </remarks>
        Public Property ScrollButtonMode As ScrollButtonDisplayMode
            Get
                Return pScrollButtonMode
            End Get
            Set(value As ScrollButtonDisplayMode)
                If value <> pScrollButtonMode Then
                    pScrollButtonMode = value
                    
                    ' Update the button visibility based on new mode
                    UpdateNavigationButtons()
                    UpdateTabBounds()
                    
                    ' Queue redraw
                    If pTabBar IsNot Nothing Then
                        pTabBar.QueueDraw()
                    End If
                End If
            End Set
        End Property

        ''' <summary>
        ''' Refreshes the entire notebook layout including tab bounds and navigation buttons
        ''' </summary>
        ''' <remarks>
        ''' This method coordinates the update of tab bounds and navigation buttons
        ''' in the correct order to avoid circular dependencies
        ''' </remarks>
        Private Sub RefreshLayout()
            Try
                ' First update tab bounds
                UpdateTabBounds()
                
                ' Then update navigation buttons based on new tab bounds
                UpdateNavigationButtons()
                
                ' Finally queue a redraw
                If pTabBar IsNot Nothing Then
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RefreshLayout error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the label text for a specific tab with optional Pango markup support
        ''' </summary>
        ''' <param name="vIndex">Index of the tab</param>
        ''' <param name="vText">Text to set (can include Pango markup)</param>
        ''' <remarks>
        ''' This method allows setting colored text using Pango markup.
        ''' Example: SetTabLabelText(1, "&lt;span foreground='red'&gt;Errors (5)&lt;/span&gt;")
        ''' </remarks>
        Public Sub SetTabLabelText(vIndex As Integer, vText As String)
            Try
                If vIndex < 0 OrElse vIndex >= pTabs.Count Then
                    Console.WriteLine($"SetTabLabelText: Invalid index {vIndex}")
                    Return
                End If
                
                ' Update the tab's label
                pTabs(vIndex).Label = vText
                
                ' Recalculate tab bounds as text width may have changed
                ' especially if markup changes the font weight or size
                UpdateTabBounds()
                
                ' Redraw the tab bar to show the new label
                pTabBar.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SetTabLabelText error: {ex.Message}")
            End Try
        End Sub 


        ''' <summary>
        ''' Diagnostic method to check and report notebook state
        ''' </summary>
        ''' <remarks>
        ''' Call this method to debug visibility and interaction issues
        ''' </remarks>
        Public Sub DiagnoseNotebookState()
            Try
                Console.WriteLine("=== CustomDrawNotebook Diagnostic ===")
                Console.WriteLine($"Notebook visible: {Me.Visible}")
                Console.WriteLine($"Notebook sensitive: {Me.Sensitive}")
                Console.WriteLine($"Tab count: {pTabs.Count}")
                Console.WriteLine($"Current tab index: {pCurrentTabIndex}")
                
                ' Check tab bar
                If pTabBar IsNot Nothing Then
                    Console.WriteLine($"Tab bar visible: {pTabBar.Visible}")
                    Console.WriteLine($"Tab bar sensitive: {pTabBar.Sensitive}")
                    Console.WriteLine($"Tab bar can focus: {pTabBar.CanFocus}")
                    Console.WriteLine($"Tab bar has focus: {pTabBar.HasFocus}")
                    
                    ' Check event mask
                    Dim lEvents As EventMask = pTabBar.Events
                    Console.WriteLine($"Tab bar events mask: {lEvents}")
                    Console.WriteLine($"  ButtonPress: {(lEvents and EventMask.ButtonPressMask) <> 0}")
                    Console.WriteLine($"  ButtonRelease: {(lEvents and EventMask.ButtonReleaseMask) <> 0}")
                    Console.WriteLine($"  PointerMotion: {(lEvents and EventMask.PointerMotionMask) <> 0}")
                Else
                    Console.WriteLine("Tab bar Is Nothing!")
                End If
                
                ' Check each tab
                For i As Integer = 0 To pTabs.Count - 1
                    Dim lTab As TabData = pTabs(i)
                    Console.WriteLine($"Tab {i}: '{lTab.Label}'")
                    Console.WriteLine($"  Widget: {If(lTab.Widget IsNot Nothing, lTab.Widget.GetType().Name, "Nothing")}")
                    Console.WriteLine($"  Visible: {If(lTab.Widget?.Visible, "True", "False")}")
                    Console.WriteLine($"  IsVisible flag: {lTab.IsVisible}")
                    Console.WriteLine($"  Bounds: X={lTab.Bounds.X}, Y={lTab.Bounds.Y}, W={lTab.Bounds.Width}, H={lTab.Bounds.Height}")
                Next
                
                ' Check navigation buttons
                Console.WriteLine("Navigation buttons:")
                Console.WriteLine($"  Dropdown: Visible={pDropdownButton?.Visible}, Setting={pShowDropdownButton}")
                
                Console.WriteLine("=====================================")
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseNotebookState error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Force refresh the notebook to ensure proper visibility
        ''' </summary>
        ''' <remarks>
        ''' Use this method if tabs are not responding to clicks
        ''' </remarks>
        Public Sub ForceRefresh()
            Try
                Console.WriteLine("Forcing CustomDrawNotebook refresh...")
                
                ' Re-enable events on tab bar if needed
                If pTabBar IsNot Nothing Then
                    pTabBar.AddEvents(CInt(EventMask.ButtonPressMask Or _
                                          EventMask.ButtonReleaseMask Or _
                                          EventMask.PointerMotionMask Or _
                                          EventMask.LeaveNotifyMask Or _
                                          EventMask.ScrollMask))
                    
                    ' Force sensitivity
                    pTabBar.Sensitive = True
                    pTabBar.CanFocus = True
                    
                    ' Queue redraw
                    pTabBar.QueueDraw()
                End If
                
                ' Ensure current tab is visible
                If pCurrentTabIndex >= 0 AndAlso pCurrentTabIndex < pTabs.Count Then
                    Dim lCurrentWidget As Widget = pTabs(pCurrentTabIndex).Widget
                    If lCurrentWidget IsNot Nothing Then
                        lCurrentWidget.ShowAll()
                        lCurrentWidget.Visible = True
                    End If
                End If
                
                ' Update navigation buttons
                UpdateNavigationButtons()
                
                Console.WriteLine("Refresh complete")
                
            Catch ex As Exception
                Console.WriteLine($"ForceRefresh error: {ex.Message}")
            End Try
        End Sub     

        ''' <summary>
        ''' Updates the visibility of navigation buttons based on current state and display mode
        ''' </summary>
        Private Sub UpdateNavigationButtons()
            Try
                If pTabBar Is Nothing Then Return
                
                ' Calculate if we need scrolling
                Dim lAvailableWidth As Integer = pTabBar.AllocatedWidth
                Dim lNeedScroll As Boolean = (pTotalTabsWidth > lAvailableWidth)
                
                ' Determine if scroll buttons should be shown based on mode
                Dim lShowScrollButtons As Boolean = False
                Select Case pScrollButtonMode
                    Case ScrollButtonDisplayMode.eNever
                        lShowScrollButtons = False
                    Case ScrollButtonDisplayMode.eAlways
                        lShowScrollButtons = True
                    Case ScrollButtonDisplayMode.eAuto
                        lShowScrollButtons = lNeedScroll
                    Case Else ' eUnspecified or invalid
                        lShowScrollButtons = lNeedScroll ' Default to auto behavior
                End Select
                
                ' Update left scroll button
                If pLeftScrollButton IsNot Nothing Then
                    If lShowScrollButtons Then
                        ' Button is shown, but may be disabled if can't scroll
                        pLeftScrollButton.NoShowAll = False
                        pLeftScrollButton.Visible = True
                        
                        ' Enable/disable based on whether we can scroll left
                        pLeftScrollButton.Sensitive = (pScrollOffset > 0)
                        
                        ' Update tooltip to indicate state
                        If pLeftScrollButton.Sensitive Then
                            pLeftScrollButton.TooltipText = "Scroll left"
                        Else
                            pLeftScrollButton.TooltipText = "At beginning"
                        End If
                    Else
                        ' Hide the button completely
                        pLeftScrollButton.Hide()
                        pLeftScrollButton.Visible = False
                        pLeftScrollButton.NoShowAll = True
                    End If
                End If
                
                ' Update right scroll button
                If pRightScrollButton IsNot Nothing Then
                    If lShowScrollButtons Then
                        ' Button is shown, but may be disabled if can't scroll
                        pRightScrollButton.NoShowAll = False
                        pRightScrollButton.Visible = True
                        
                        ' Enable/disable based on whether we can scroll right
                        Dim lCanScrollRight As Boolean = (pScrollOffset < (pTotalTabsWidth - lAvailableWidth))
                        pRightScrollButton.Sensitive = lCanScrollRight
                        
                        ' Update tooltip to indicate state
                        If pRightScrollButton.Sensitive Then
                            pRightScrollButton.TooltipText = "Scroll right"
                        Else
                            pRightScrollButton.TooltipText = "At end"
                        End If
                    Else
                        ' Hide the button completely
                        pRightScrollButton.Hide()
                        pRightScrollButton.Visible = False
                        pRightScrollButton.NoShowAll = True
                    End If
                End If
                
                ' Update max scroll offset
                pMaxScrollOffset = Math.Max(0, pTotalTabsWidth - lAvailableWidth)
                
                ' Update dropdown button visibility if enabled by property
                If pShowDropdownButton Then
                    If pDropdownButton IsNot Nothing Then
                        ' Show dropdown if we have multiple tabs or tabs are scrolled
                        If pTabs.Count > 1 OrElse lNeedScroll Then
                            pDropdownButton.NoShowAll = False
                            pDropdownButton.Visible = True
                        Else
                            pDropdownButton.Hide()
                            pDropdownButton.Visible = False
                            pDropdownButton.NoShowAll = True
                        End If
                    End If
                Else
                    ' Ensure dropdown is hidden if disabled by property
                    If pDropdownButton IsNot Nothing Then
                        pDropdownButton.Hide()
                        pDropdownButton.Visible = False
                        pDropdownButton.NoShowAll = True
                    End If
                End If
                
                ' Update close all button visibility if enabled by property
                If pShowCloseAllButton Then
                    If pCloseAllButton IsNot Nothing Then
                        ' Only show if we have closeable tabs
                        If pTabs.Count > 0 AndAlso pShowCloseButtons Then
                            pCloseAllButton.NoShowAll = False
                            pCloseAllButton.Visible = True
                        Else
                            pCloseAllButton.Hide()
                            pCloseAllButton.Visible = False
                            pCloseAllButton.NoShowAll = True
                        End If
                    End If
                Else
                    ' Ensure close all is hidden if disabled by property
                    If pCloseAllButton IsNot Nothing Then
                        pCloseAllButton.Hide()
                        pCloseAllButton.Visible = False
                        pCloseAllButton.NoShowAll = True
                    End If
                End If
                
                ' Update hide panel button visibility if enabled by property
                If pShowHidePanelButton Then
                    If pHidePanelButton IsNot Nothing Then
                        pHidePanelButton.NoShowAll = False
                        pHidePanelButton.Visible = True
                    End If
                Else
                    ' Ensure hide panel is hidden if disabled by property
                    If pHidePanelButton IsNot Nothing Then
                        pHidePanelButton.Hide()
                        pHidePanelButton.Visible = False
                        pHidePanelButton.NoShowAll = True
                    End If
                End If
                
                ' DO NOT CALL UpdateTabBounds here - it will cause circular dependency!
                ' Just queue a redraw
                If pTabBar IsNot Nothing Then
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateNavigationButtons error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Calculates the total width taken by currently visible navigation buttons
        ''' </summary>
        ''' <returns>Total width of visible buttons in pixels</returns>
        Private Function CalculateButtonsWidth() As Integer
            Try
                Dim lButtonsWidth As Integer = 0
                Dim lButtonWidth As Integer = 30 ' Approximate width per button
                
                ' Check each button - use both mode/property flags AND actual visibility
                
                ' Dropdown button - only count if property is enabled AND button is visible
                If pShowDropdownButton AndAlso pDropdownButton IsNot Nothing AndAlso pDropdownButton.Visible Then
                    lButtonsWidth += lButtonWidth
                End If
                
                ' Scroll buttons - check based on mode
                Select Case pScrollButtonMode
                    Case ScrollButtonDisplayMode.eNever
                        ' Never count scroll buttons
                        
                    Case ScrollButtonDisplayMode.eAlways
                        ' Always count both scroll buttons if they exist
                        If pLeftScrollButton IsNot Nothing Then
                            lButtonsWidth += lButtonWidth
                        End If
                        If pRightScrollButton IsNot Nothing Then
                            lButtonsWidth += lButtonWidth
                        End If
                        
                    Case ScrollButtonDisplayMode.eAuto
                        ' Count scroll buttons only if they're actually visible
                        If pLeftScrollButton IsNot Nothing AndAlso pLeftScrollButton.Visible Then
                            lButtonsWidth += lButtonWidth
                        End If
                        If pRightScrollButton IsNot Nothing AndAlso pRightScrollButton.Visible Then
                            lButtonsWidth += lButtonWidth
                        End If
                        
                    Case Else ' eUnspecified or invalid
                        ' Default to auto behavior
                        If pLeftScrollButton IsNot Nothing AndAlso pLeftScrollButton.Visible Then
                            lButtonsWidth += lButtonWidth
                        End If
                        If pRightScrollButton IsNot Nothing AndAlso pRightScrollButton.Visible Then
                            lButtonsWidth += lButtonWidth
                        End If
                End Select
                
                ' Close all button - only count if property is enabled AND button is visible
                If pShowCloseAllButton AndAlso pCloseAllButton IsNot Nothing AndAlso pCloseAllButton.Visible Then
                    lButtonsWidth += lButtonWidth
                End If
                
                ' Hide panel button - only count if property is enabled AND button is visible
                If pShowHidePanelButton AndAlso pHidePanelButton IsNot Nothing AndAlso pHidePanelButton.Visible Then
                    lButtonsWidth += lButtonWidth
                End If
                
                Return lButtonsWidth
                
            Catch ex As Exception
                Console.WriteLine($"CalculateButtonsWidth error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ' ===== Inner Classes =====
        
        ''' <summary>
        ''' Represents data for a single tab
        ''' </summary>
        Private Class TabData
            Public Property Widget As Widget
            Public Property Label As String
            Public Property IconName As String
            Public Property Modified As Boolean
            Public Property IsVisible As Boolean
            Public Property Bounds As Cairo.Rectangle
            Public Property CloseBounds As Cairo.Rectangle
        End Class


        
    End Class

    ''' <summary>
    ''' Theme colors for rendering notebook tabs
    ''' </summary>
    Public Class ThemeColors
        Public Property Background As Gdk.RGBA
        Public Property EditorBackground As Gdk.RGBA
        Public Property ActiveTab As Gdk.RGBA
        Public Property InactiveTab As Gdk.RGBA
        Public Property HoverTab As Gdk.RGBA
        Public Property Text As Gdk.RGBA
        Public Property Border As Gdk.RGBA
        Public Property ModifiedIndicator As Gdk.RGBA
        Public Property CloseButton As Gdk.RGBA
        Public Property CloseButtonHover As Gdk.RGBA
        Public Property TabHover As Gdk.RGBA
        Public Property Accent As Gdk.RGBA
        Public Property TabInactive As Gdk.RGBA
        Public Property TextInactive As Gdk.RGBA
    End Class

    ''' <summary>
    ''' Specifies how scroll buttons should be displayed in the tab bar
    ''' </summary>
    Public Enum ScrollButtonDisplayMode
        ''' <summary>Unspecified mode</summary>
        eUnspecified
        ''' <summary>Never show scroll buttons</summary>
        eNever
        ''' <summary>Show scroll buttons only when tabs exceed available space</summary>
        eAuto
        ''' <summary>Always show scroll buttons</summary>
        eAlways
        ''' <summary>Sentinel value for enum bounds checking</summary>
        eLastValue
    End Enum
    
End Namespace