' CustomDrawNotebook.Compatibility.vb - GTK Notebook compatibility layer
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    Partial Public Class CustomDrawNotebook
        Implements ICustomNotebook
        
        ' Dictionary to track custom tab label widgets
        Private pCustomTabLabels As New Dictionary(Of Widget, Widget)()
        
        ' ===== ICustomNotebook Implementation =====
        
        ''' <summary>
        ''' Gets the tab label text for a page
        ''' </summary>
        ''' <param name="vPageNum">Page index</param>
        ''' <returns>Tab label text (may contain markup)</returns>
        Public Function GetTabLabel(vPageNum As Integer) As String Implements ICustomNotebook.GetTabLabel
            Try
                If vPageNum >= 0 AndAlso vPageNum < pTabs.Count Then
                    Return pTabs(vPageNum).Label
                End If
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetTabLabel error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Sets the tab label text for a page
        ''' </summary>
        ''' <param name="vPageNum">Page index</param>
        ''' <param name="vTabText">New tab label text (can contain Pango markup)</param>
        Public Sub SetTabLabel(vPageNum As Integer, vTabText As String) Implements ICustomNotebook.SetTabLabel
            Try
                If vPageNum >= 0 AndAlso vPageNum < pTabs.Count Then
                    pTabs(vPageNum).Label = vTabText
                    
                    ' Update tab bounds since text change might affect width
                    UpdateTabBounds()
                    
                    ' Redraw tab bar
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetTabLabel error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the tab label text for a page by widget
        ''' </summary>
        ''' <param name="vChild">Child widget of the page</param>
        ''' <param name="vTabText">New tab label text (can contain Pango markup)</param>
        Public Sub SetTabLabelText(vChild As Widget, vTabText As String) Implements ICustomNotebook.SetTabLabelText
            Try
                Dim lIndex As Integer = PageNum(vChild)
                If lIndex >= 0 Then
                    SetTabLabel(lIndex, vTabText)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetTabLabelText error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Additional Compatibility Methods =====
        
        ''' <summary>
        ''' Override ShowAll to properly manage tab and button visibility
        ''' </summary>
        ''' <remarks>
        ''' This method ensures all components are visible while respecting tab visibility rules
        ''' and button display modes
        ''' </remarks>
        Public Shadows Sub ShowAll()
            Try
                Console.WriteLine($"CustomDrawNotebook.ShowAll called with {pTabs.Count} tabs")
                
                ' CRITICAL: Call base ShowAll first to ensure the container and all structural
                ' children (pTabBar, pContentArea, buttons) are shown
                MyBase.ShowAll()
                
                ' Now override specific visibility settings for navigation buttons
                ' based on their configuration properties
                
                ' Dropdown button visibility controlled by property
                If pDropdownButton IsNot Nothing Then
                    If pShowDropdownButton Then
                        pDropdownButton.NoShowAll = False
                        pDropdownButton.Show()
                        pDropdownButton.Visible = True
                        pDropdownButton.NoShowAll = True
                    Else
                        pDropdownButton.Hide()
                        pDropdownButton.Visible = False
                        pDropdownButton.NoShowAll = True
                    End If
                End If
                
                ' Scroll buttons visibility controlled by ScrollButtonMode
                Select Case pScrollButtonMode
                    Case ScrollButtonDisplayMode.eNever
                        ' Never show scroll buttons
                        If pLeftScrollButton IsNot Nothing Then
                            pLeftScrollButton.Hide()
                            pLeftScrollButton.Visible = False
                            pLeftScrollButton.NoShowAll = True
                        End If
                        If pRightScrollButton IsNot Nothing Then
                            pRightScrollButton.Hide()
                            pRightScrollButton.Visible = False
                            pRightScrollButton.NoShowAll = True
                        End If
                        
                    Case ScrollButtonDisplayMode.eAlways
                        ' Always show scroll buttons
                        If pLeftScrollButton IsNot Nothing Then
                            pLeftScrollButton.NoShowAll = False
                            pLeftScrollButton.Show()
                            pLeftScrollButton.Visible = True
                            pLeftScrollButton.NoShowAll = True
                        End If
                        If pRightScrollButton IsNot Nothing Then
                            pRightScrollButton.NoShowAll = False
                            pRightScrollButton.Show()
                            pRightScrollButton.Visible = True
                            pRightScrollButton.NoShowAll = True
                        End If
                        
                    Case ScrollButtonDisplayMode.eAuto, ScrollButtonDisplayMode.eUnspecified
                        ' Auto mode - let UpdateNavigationButtons handle visibility
                        ' For now, hide them and let UpdateNavigationButtons show them if needed
                        If pLeftScrollButton IsNot Nothing Then
                            pLeftScrollButton.Hide()
                            pLeftScrollButton.Visible = False
                            pLeftScrollButton.NoShowAll = True
                        End If
                        If pRightScrollButton IsNot Nothing Then
                            pRightScrollButton.Hide()
                            pRightScrollButton.Visible = False
                            pRightScrollButton.NoShowAll = True
                        End If
                End Select
                
                ' Close all button visibility controlled by property
                If pCloseAllButton IsNot Nothing Then
                    If pShowCloseAllButton Then
                        pCloseAllButton.NoShowAll = False
                        pCloseAllButton.Show()
                        pCloseAllButton.Visible = True
                        pCloseAllButton.NoShowAll = True
                    Else
                        pCloseAllButton.Hide()
                        pCloseAllButton.Visible = False
                        pCloseAllButton.NoShowAll = True
                    End If
                End If
                
                ' Hide panel button visibility controlled by property
                If pHidePanelButton IsNot Nothing Then
                    If pShowHidePanelButton Then
                        pHidePanelButton.NoShowAll = False
                        pHidePanelButton.Show()
                        pHidePanelButton.Visible = True
                        pHidePanelButton.NoShowAll = True
                    Else
                        pHidePanelButton.Hide()
                        pHidePanelButton.Visible = False
                        pHidePanelButton.NoShowAll = True
                    End If
                End If
                
                ' Now handle tab content visibility - only show current tab
                for i As Integer = 0 To pTabs.Count - 1
                    If pTabs(i).Widget IsNot Nothing Then
                        If i = pCurrentTabIndex Then
                            ' Show current tab content
                            Console.WriteLine($"  Showing tab {i}: '{pTabs(i).Label}'")
                            pTabs(i).Widget.NoShowAll = False
                            pTabs(i).Widget.ShowAll()  ' Use ShowAll to ensure all children are shown
                            pTabs(i).Widget.NoShowAll = True ' Reset for next time
                            
                            ' Make sure it's actually visible
                            pTabs(i).Widget.Visible = True
                            pTabs(i).Widget.Show()
                        Else
                            ' Hide all other tabs
                            pTabs(i).Widget.Hide()
                            pTabs(i).Widget.Visible = False
                        End If
                    End If
                Next
                
                ' If no current tab is set and we have tabs, set the first one
                If pCurrentTabIndex < 0 AndAlso pTabs.Count > 0 Then
                    Console.WriteLine("  No current tab Set - selecting first tab")
                    SetCurrentTab(0)
                End If
                
                ' Update navigation buttons to apply the correct visibility based on mode and state
                UpdateNavigationButtons()
                
                ' Force a redraw of the tab bar to ensure tabs are visible
                If pTabBar IsNot Nothing Then
                    pTabBar.QueueDraw()
                End If
                
                Console.WriteLine($"  ShowAll completed: {pTabs.Count} tabs, current index: {pCurrentTabIndex}")
                
            Catch ex As Exception
                Console.WriteLine($"ShowAll error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets whether a tab is reorderable (stub for compatibility)
        ''' </summary>
        ''' <param name="vChild">Child widget of the page</param>
        ''' <param name="vReorderable">Whether the tab can be reordered</param>
        Public Sub SetTabReorderable(vChild As Widget, vReorderable As Boolean)
            ' All tabs are reorderable in our implementation
            ' This is just for compatibility
        End Sub
        
        ''' <summary>
        ''' Sets whether a tab is detachable (stub for compatibility)
        ''' </summary>
        ''' <param name="vChild">Child widget of the page</param>
        ''' <param name="vDetachable">Whether the tab can be detached</param>
        Public Sub SetTabDetachable(vChild As Widget, vDetachable As Boolean)
            ' Not implemented yet, just for compatibility
        End Sub
        
        ''' <summary>
        ''' Gets the tab position type (stub for compatibility)
        ''' </summary>
        ''' <returns>Always returns Top</returns>
        Public ReadOnly Property TabPos As PositionType
            Get
                Return PositionType.Top
            End Get
        End Property
        
        ''' <summary>
        ''' Sets whether to show tabs (stub for compatibility)
        ''' </summary>
        Public Property ShowTabs As Boolean
            Get
                Return True
            End Get
            Set(value As Boolean)
                ' Always show tabs in our implementation
            End Set
        End Property
        
        ''' <summary>
        ''' Sets whether to show border (stub for compatibility)
        ''' </summary>
        Public Property ShowBorder As Boolean
            Get
                Return True
            End Get
            Set(value As Boolean)
                ' Always show border in our implementation
            End Set
        End Property
        
        ''' <summary>
        ''' Sets whether tabs are scrollable (always true for us)
        ''' </summary>
        Public Property Scrollable As Boolean
            Get
                Return True
            End Get
            Set(value As Boolean)
                ' Always scrollable in our implementation
            End Set
        End Property
        
    End Class
    
End Namespace 
