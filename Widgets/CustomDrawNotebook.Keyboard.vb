' CustomDrawNotebook.Keyboard.vb - Keyboard navigation for custom notebook
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    Partial Public Class CustomDrawNotebook
        Implements ICustomNotebook
        
        ''' <summary>
        ''' Sets up keyboard navigation for the notebook
        ''' </summary>
        Private Sub SetupKeyboardNavigation()
            Try
                AddHandler KeyPressEvent, AddressOf OnKeyPress
                CanFocus = True
                
            Catch ex As Exception
                Console.WriteLine($"SetupKeyboardNavigation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles key press events for navigation
        ''' </summary>
        Private Sub OnKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Dim lHandled As Boolean = False
                
                ' Check for modifier keys
                Dim lControl As Boolean = (vArgs.Event.State and ModifierType.ControlMask) <> 0
                Dim lShift As Boolean = (vArgs.Event.State and ModifierType.ShiftMask) <> 0
                Dim lAlt As Boolean = (vArgs.Event.State and ModifierType.Mod1Mask) <> 0
                
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Tab
                        If lControl Then
                            ' Ctrl+Tab - next tab
                            If lShift Then
                                ' Ctrl+Shift+Tab - previous tab
                                SwitchToPreviousTab()
                            Else
                                SwitchToNextTab()
                            End If
                            lHandled = True
                        End If
                        
                    Case Gdk.Key.Page_Down
                        If lControl Then
                            ' Ctrl+PageDown - next tab
                            SwitchToNextTab()
                            lHandled = True
                        End If
                        
                    Case Gdk.Key.Page_Up
                        If lControl Then
                            ' Ctrl+PageUp - previous tab
                            SwitchToPreviousTab()
                            lHandled = True
                        End If
                        
                    Case CType(87, Gdk.Key)
                        If lControl Then
                            ' Ctrl+W - close current tab
                            If pCurrentTabIndex >= 0 Then
                                RemovePage(pCurrentTabIndex)
                            End If
                            lHandled = True
                        End If
                        
                    Case Gdk.Key.F4
                        If lControl Then
                            ' Ctrl+F4 - close current tab
                            If pCurrentTabIndex >= 0 Then
                                RemovePage(pCurrentTabIndex)
                            End If
                            lHandled = True
                        End If
                        
                    Case Gdk.Key.Key_1 To Gdk.Key.Key_9
                        If lControl Then
                            ' Ctrl+1 through Ctrl+9 - switch to specific tab
                            Dim lTabIndex As Integer = CInt(vArgs.Event.Key) - CInt(Gdk.Key.Key_1)
                            If lTabIndex < pTabs.Count Then
                                SetCurrentTab(lTabIndex)
                                lHandled = True
                            End If
                        End If
                        
                    Case Gdk.Key.Left
                        If lAlt Then
                            ' Alt+Left - scroll tabs left
                            ScrollTabs(-50)
                            lHandled = True
                        End If
                        
                    Case Gdk.Key.Right
                        If lAlt Then
                            ' Alt+Right - scroll tabs right
                            ScrollTabs(50)
                            lHandled = True
                        End If
                        
                End Select
                
                vArgs.RetVal = lHandled
                
            Catch ex As Exception
                Console.WriteLine($"OnKeyPress error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Switches to the next tab
        ''' </summary>
        ''' <remarks>
        ''' Cycles through tabs forward and ensures the new tab's content receives focus
        ''' </remarks>
        Public Sub SwitchToNextTab() Implements ICustomNotebook.SwitchToNextTab
            Try
                If pTabs.Count = 0 Then Return
                
                Dim lNextIndex As Integer = pCurrentTabIndex + 1
                If lNextIndex >= pTabs.Count Then
                    lNextIndex = 0 ' Wrap around
                End If
                
                SetCurrentTab(lNextIndex)
                ' Focus is handled by SetCurrentTab
                
            Catch ex As Exception
                Console.WriteLine($"SwitchToNextTab error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Switches to the previous tab
        ''' </summary>
        ''' <remarks>
        ''' Cycles through tabs backward and ensures the new tab's content receives focus
        ''' </remarks>
        Public Sub SwitchToPreviousTab() Implements ICustomNotebook.SwitchToPreviousTab
            Try
                If pTabs.Count = 0 Then Return
                
                Dim lPrevIndex As Integer = pCurrentTabIndex - 1
                If lPrevIndex < 0 Then
                    lPrevIndex = pTabs.Count - 1 ' Wrap around
                End If
                
                SetCurrentTab(lPrevIndex)
                ' Focus is handled by SetCurrentTab
                
            Catch ex As Exception
                Console.WriteLine($"SwitchToPreviousTab error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the index of the tab by its widget
        ''' </summary>
        ''' <param name="vWidget">Widget to find</param>
        ''' <returns>Tab index or -1 if not found</returns>
        Public Function GetTabIndexByWidget(vWidget As Widget) As Integer
            Try
                for i As Integer = 0 To pTabs.Count - 1
                    If pTabs(i).Widget Is vWidget Then
                        Return i
                    End If
                Next
                
                Return -1
                
            Catch ex As Exception
                Console.WriteLine($"GetTabIndexByWidget error: {ex.Message}")
                Return -1
            End Try
        End Function
        
        ''' <summary>
        ''' Switches to a specific tab by its widget
        ''' </summary>
        ''' <param name="vWidget">Widget of the tab to switch to</param>
        ''' <remarks>
        ''' Finds the tab containing the widget and activates it with focus
        ''' </remarks>
        Public Sub SwitchToTabByWidget(vWidget As Widget)
            Try
                Dim lIndex As Integer = GetTabIndexByWidget(vWidget)
                If lIndex >= 0 Then
                    SetCurrentTab(lIndex)
                    ' Focus is handled by SetCurrentTab
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SwitchToTabByWidget error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace