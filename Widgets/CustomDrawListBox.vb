' Widgets/CustomDrawListBox.vb - Custom-drawn ListBox widget
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Utilities


' CustomDrawListBox.vb
' Created: 2025-08-19 06:03:01

Namespace Widgets
    
    ''' <summary>
    ''' Custom-drawn ListBox widget with selection support
    ''' </summary>
    Public Class CustomDrawListBox
        Inherits Box
        
        ' ===== Private Fields =====
        
        Private pDrawingArea As DrawingArea
        Private pVScrollbar As Scrollbar
        Private pItems As New List(Of ListBoxItem)
        Private pSelectedIndex As Integer = -1
        Private pHoverIndex As Integer = -1
        Private pScrollOffset As Integer = 0
        Private pItemHeight As Integer = 24
        Private pVisibleItems As Integer = 0
        Private pNeedsRedraw As Boolean = True
        Private pThemeContextMenu as Menu
        
        ' Colors
        Private pBackgroundColor As String = "#FFFFFF"
        Private pForegroundColor As String = "#000000"
        Private pSelectionColor As String = "#007ACC"
        Private pSelectionTextColor As String = "#FFFFFF"
        Private pHoverColor As String = "#E5F1FB"
        
        ' Font settings
        Private pFontFamily As String = "Monospace"
        Private pFontSize As Integer = 10
        
        ' Events
        Public Event SelectionChanged(vIndex As Integer, vItem As ListBoxItem)
        Public Event ItemDoubleClicked(vIndex As Integer, vItem As ListBoxItem)
        Public Event ContextMenuRequested(vIndex As Integer, vItem As ListBoxItem, vEvent As EventButton)
        
        ' ===== Public Properties =====
        
        ''' <summary>
        ''' Gets or sets the selected index
        ''' </summary>
        Public Property SelectedIndex As Integer
            Get
                Return pSelectedIndex
            End Get
            Set(value As Integer)
                If value <> pSelectedIndex AndAlso value >= -1 AndAlso value < pItems.Count Then
                    pSelectedIndex = value
                    pNeedsRedraw = True
                    pDrawingArea?.QueueDraw()
                    
                    If pSelectedIndex >= 0 Then
                        RaiseEvent SelectionChanged(pSelectedIndex, pItems(pSelectedIndex))
                    Else
                        RaiseEvent SelectionChanged(-1, Nothing)
                    End If
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Gets the selected item
        ''' </summary>
        Public ReadOnly Property SelectedItem As ListBoxItem
            Get
                If pSelectedIndex >= 0 AndAlso pSelectedIndex < pItems.Count Then
                    Return pItems(pSelectedIndex)
                End If
                Return Nothing
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the items collection
        ''' </summary>
        Public ReadOnly Property Items As List(Of ListBoxItem)
            Get
                Return pItems
            End Get
        End Property
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new CustomDrawListBox
        ''' </summary>
        Public Sub New()
            MyBase.New(Orientation.Horizontal, 0)
            
            BuildUI()
            SetupEventHandlers()
        End Sub
        
        ' ===== UI Building =====
        
        ''' <summary>
        ''' Builds the UI components
        ''' </summary>
        Private Sub BuildUI()
            Try
                ' Create drawing area
                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                pDrawingArea.Events = EventMask.AllEventsMask
                pDrawingArea.Expand = True
                PackStart(pDrawingArea, True, True, 0)
                
                ' Create scrollbar
                pVScrollbar = New Scrollbar(Orientation.Vertical, New Adjustment(0, 0, 100, 1, 10, 10))
                PackStart(pVScrollbar, False, False, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets up event handlers
        ''' </summary>
        Private Sub SetupEventHandlers()
            Try
                ' Drawing events
                'AddHandler pDrawingArea.SizeAllocated, AddressOf OnSizeAllocated
                
                ' Mouse events
                AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnButtonPress
                AddHandler pDrawingArea.ButtonReleaseEvent, AddressOf OnButtonRelease
                AddHandler pDrawingArea.MotionNotifyEvent, AddressOf OnMotionNotify
                AddHandler pDrawingArea.LeaveNotifyEvent, AddressOf OnLeaveNotify
                
                ' Keyboard events
                AddHandler pDrawingArea.KeyPressEvent, AddressOf OnKeyPress
                
                ' Scroll events
                AddHandler pDrawingArea.ScrollEvent, AddressOf OnScroll
                AddHandler pVScrollbar.ValueChanged, AddressOf OnScrollbarValueChanged
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.SetupEventHandlers error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Drawing =====
        
        ''' <summary>
        ''' Handles the draw event
        ''' </summary>
        Protected Overrides Function OnDrawn(vContext As Context) As Boolean
            Try
                Dim lWidth As Integer = AllocatedWidth
                Dim lHeight As Integer = AllocatedHeight
                
                ' Clear background
                SetSourceColor(vContext, pBackgroundColor)
                vContext.Rectangle(0, 0, lWidth, lHeight)
                vContext.Fill()
                
                ' Calculate visible range
                Dim lStartIndex As Integer = pScrollOffset \ pItemHeight
                Dim lEndIndex As Integer = Math.Min(lStartIndex + pVisibleItems + 1, pItems.Count - 1)
                
                ' Draw items
                For i As Integer = lStartIndex To lEndIndex
                    DrawItem(vContext, i, lWidth)
                Next
                
                pNeedsRedraw = False
                Return True
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.OnDrawn error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Draws a single item
        ''' </summary>
        Private Sub DrawItem(vContext As Context, vIndex As Integer, vWidth As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pItems.Count Then Return
                
                Dim lItem As ListBoxItem = pItems(vIndex)
                Dim lY As Integer = (vIndex * pItemHeight) - pScrollOffset
                
                ' Skip if outside visible area
                If lY + pItemHeight < 0 OrElse lY > pDrawingArea.AllocatedHeight Then Return
                
                ' Draw selection background
                If vIndex = pSelectedIndex Then
                    SetSourceColor(vContext, pSelectionColor)
                    vContext.Rectangle(0, lY, vWidth, pItemHeight)
                    vContext.Fill()
                ElseIf vIndex = pHoverIndex Then
                    SetSourceColor(vContext, pHoverColor)
                    vContext.Rectangle(0, lY, vWidth, pItemHeight)
                    vContext.Fill()
                End If
                
                ' Draw text
                vContext.SelectFontFace(pFontFamily, FontSlant.Normal, FontWeight.Normal)
                vContext.SetFontSize(pFontSize)
                
                If vIndex = pSelectedIndex Then
                    SetSourceColor(vContext, pSelectionTextColor)
                Else
                    SetSourceColor(vContext, pForegroundColor)
                End If
                
                vContext.MoveTo(5, lY + pItemHeight - 6)
                vContext.ShowText(lItem.Text)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.DrawItem error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Mouse Handling =====
        
        ''' <summary>
        ''' Handles mouse button press
        ''' </summary>
        Private Sub OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                pDrawingArea.GrabFocus()
                
                Dim lIndex As Integer = GetItemIndexAt(CInt(vArgs.Event.Y))
                
                If vArgs.Event.Button = 1 Then ' Left click
                    If lIndex >= 0 Then
                        SelectedIndex = lIndex
                        
                        ' Check for double-click
                        If vArgs.Event.Type = EventType.TwoButtonPress Then
                            RaiseEvent ItemDoubleClicked(lIndex, pItems(lIndex))
                        End If
                    End If
                ElseIf vArgs.Event.Button = 3 Then ' Right click
                    If lIndex >= 0 Then
                        SelectedIndex = lIndex
                        RaiseEvent ContextMenuRequested(lIndex, pItems(lIndex), vArgs.Event)
                    End If
                End If
                
                vArgs.RetVal = True
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.OnButtonPress error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles mouse button release
        ''' </summary>
        Private Sub OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs)
            ' Currently not needed, but here for future use
            vArgs.RetVal = True
        End Sub
        
        ''' <summary>
        ''' Handles mouse motion
        ''' </summary>
        Private Sub OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs)
            Try
                Dim lIndex As Integer = GetItemIndexAt(CInt(vArgs.Event.Y))
                
                If lIndex <> pHoverIndex Then
                    pHoverIndex = lIndex
                    pDrawingArea.QueueDraw()
                End If
                
                vArgs.RetVal = True
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.OnMotionNotify error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles mouse leave
        ''' </summary>
        Private Sub OnLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            Try
                If pHoverIndex >= 0 Then
                    pHoverIndex = -1
                    pDrawingArea.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.OnLeaveNotify error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Keyboard Handling =====
        
        ''' <summary>
        ''' Handles keyboard input
        ''' </summary>
        Private Sub OnKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Up, Gdk.Key.KP_Up
                        If pSelectedIndex > 0 Then
                            SelectedIndex = pSelectedIndex - 1
                            EnsureVisible(pSelectedIndex)
                        End If
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.Down, Gdk.Key.KP_Down
                        If pSelectedIndex < pItems.Count - 1 Then
                            SelectedIndex = pSelectedIndex + 1
                            EnsureVisible(pSelectedIndex)
                        End If
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.Home, Gdk.Key.KP_Home
                        If pItems.Count > 0 Then
                            SelectedIndex = 0
                            EnsureVisible(0)
                        End If
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.End, Gdk.Key.KP_End
                        If pItems.Count > 0 Then
                            SelectedIndex = pItems.Count - 1
                            EnsureVisible(pSelectedIndex)
                        End If
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.Page_Up, Gdk.Key.KP_Page_Up
                        Dim lNewIndex As Integer = Math.Max(0, pSelectedIndex - pVisibleItems)
                        SelectedIndex = lNewIndex
                        EnsureVisible(lNewIndex)
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.Page_Down, Gdk.Key.KP_Page_Down
                        Dim lNewIndex As Integer = Math.Min(pItems.Count - 1, pSelectedIndex + pVisibleItems)
                        SelectedIndex = lNewIndex
                        EnsureVisible(lNewIndex)
                        vArgs.RetVal = True
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.OnKeyPress error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Scrolling =====
        
        ''' <summary>
        ''' Handles scroll events
        ''' </summary>
        Private Sub OnScroll(vSender As Object, vArgs As ScrollEventArgs)
            Try
                Dim lDelta As Integer = If(vArgs.Event.Direction = ScrollDirection.Up, -3, 3)
                Dim lNewValue As Double = pVScrollbar.Value + (lDelta * pItemHeight)
                
                pVScrollbar.Value = Math.Max(pVScrollbar.Adjustment.Lower, 
                                           Math.Min(lNewValue, pVScrollbar.Adjustment.Upper - pVScrollbar.Adjustment.PageSize))
                
                vArgs.RetVal = True
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.OnScroll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles scrollbar value changes
        ''' </summary>
        Private Sub OnScrollbarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                pScrollOffset = CInt(pVScrollbar.Value)
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.OnScrollbarValueChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Size Handling =====
        
        
        ''' <summary>
        ''' Updates scrollbar settings
        ''' </summary>
        Private Sub UpdateScrollbar()
            Try
                Dim lTotalHeight As Integer = pItems.Count * pItemHeight
                Dim lVisibleHeight As Integer = pDrawingArea.AllocatedHeight
                
                pVScrollbar.Adjustment.Lower = 0
                pVScrollbar.Adjustment.Upper = lTotalHeight
                pVScrollbar.Adjustment.PageSize = lVisibleHeight
                pVScrollbar.Adjustment.StepIncrement = pItemHeight
                pVScrollbar.Adjustment.PageIncrement = lVisibleHeight
                
                pVScrollbar.Visible = lTotalHeight > lVisibleHeight
                
                ' Ensure scroll position is valid
                If pScrollOffset + lVisibleHeight > lTotalHeight Then
                    pScrollOffset = Math.Max(0, lTotalHeight - lVisibleHeight)
                    pVScrollbar.Value = pScrollOffset
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.UpdateScrollbar error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Adds an item to the list
        ''' </summary>
        Public Sub AddItem(vText As String, Optional vData As Object = Nothing)
            Try
                pItems.Add(New ListBoxItem(vText, vData))
                UpdateScrollbar()
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.AddItem error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Removes an item at the specified index
        ''' </summary>
        Public Sub RemoveItem(vIndex As Integer)
            Try
                If vIndex >= 0 AndAlso vIndex < pItems.Count Then
                    pItems.RemoveAt(vIndex)
                    
                    ' Adjust selection
                    If pSelectedIndex >= pItems.Count Then
                        pSelectedIndex = pItems.Count - 1
                    End If
                    
                    UpdateScrollbar()
                    pDrawingArea.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.RemoveItem error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clears all items
        ''' </summary>
        Public Sub Clear()
            Try
                pItems.Clear()
                pSelectedIndex = -1
                pHoverIndex = -1
                pScrollOffset = 0
                UpdateScrollbar()
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.Clear error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the theme colors
        ''' </summary>
        Public Sub SetColors(vBackground As String, vForeground As String, vSelection As String, vSelectionText As String, vHover As String)
            pBackgroundColor = vBackground
            pForegroundColor = vForeground
            pSelectionColor = vSelection
            pSelectionTextColor = vSelectionText
            pHoverColor = vHover
            pDrawingArea?.QueueDraw()
        End Sub
        
        ''' <summary>
        ''' Ensures an item is visible
        ''' </summary>
        Public Sub EnsureVisible(vIndex As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pItems.Count Then Return
                
                Dim lItemTop As Integer = vIndex * pItemHeight
                Dim lItemBottom As Integer = lItemTop + pItemHeight
                Dim lVisibleTop As Integer = pScrollOffset
                Dim lVisibleBottom As Integer = pScrollOffset + pDrawingArea.AllocatedHeight
                
                If lItemTop < lVisibleTop Then
                    ' Scroll up
                    pVScrollbar.Value = lItemTop
                ElseIf lItemBottom > lVisibleBottom Then
                    ' Scroll down
                    pVScrollbar.Value = lItemBottom - pDrawingArea.AllocatedHeight
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.EnsureVisible error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Selects an item by text
        ''' </summary>
        Public Function SelectByText(vText As String) As Boolean
            Try
                For i As Integer = 0 To pItems.Count - 1
                    If pItems(i).Text = vText Then
                        SelectedIndex = i
                        EnsureVisible(i)
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.SelectByText error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Gets the item index at the specified Y coordinate
        ''' </summary>
        Private Function GetItemIndexAt(vY As Integer) As Integer
            Try
                Dim lAdjustedY As Integer = vY + pScrollOffset
                Dim lIndex As Integer = lAdjustedY \ pItemHeight
                
                If lIndex >= 0 AndAlso lIndex < pItems.Count Then
                    Return lIndex
                End If
                
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.GetItemIndexAt error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ''' <summary>
        ''' Sets Cairo source color from hex string
        ''' </summary>
        Private Sub SetSourceColor(vContext As Context, vHexColor As String)
            Try
                Dim lColor As New Gdk.RGBA()
                If lColor.Parse(vHexColor) Then
                    vContext.SetSourceRGBA(lColor.Red, lColor.Green, lColor.Blue, lColor.Alpha)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawListBox.SetSourceColor error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnThemeContextMenuRequested(vIndex As Integer, vItem As ListBoxItem, vEvent As Gdk.Event)
            Try
                If vItem Is Nothing Then Return
                
                Dim lThemeName As String = vItem.Text
                Dim lIsCustom As Boolean = DirectCast(vItem.Data, Boolean)
                
                ' Update menu items sensitivity
                For Each lItem As Widget In pThemeContextMenu.Children
                    If TypeOf lItem Is MenuItem Then
                        Dim lMenuItem As MenuItem = DirectCast(lItem, MenuItem)
                        Select Case lMenuItem.Label
                            Case "Delete Theme"
                                lMenuItem.Sensitive = lIsCustom
                            Case "Copy Theme"
                                lMenuItem.Sensitive = True
                        End Select
                    End If
                Next
                
                ' Show context menu
                pThemeContextMenu.PopupAtPointer(vEvent)
                
            Catch ex As Exception
                Console.WriteLine($"OnThemeContextMenuRequested error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
    
End Namespace
