' Widgets/CustomDrawProjectExplorer.Toolbar.vb - Toolbar implementation
' Created: 2025-08-17
Imports Gtk
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing toolbar functionality for Project Explorer
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Toolbar Fields =====
        Private pRefreshButton As CustomDrawButton
        Private pCollapseAllButton As CustomDrawButton
        Private pExpandAllButton As CustomDrawButton
        Private pCloseButton As CustomDrawButton
        Private pScaleLabel As Label
        Private pScaleCombo As CustomDrawComboBox
        
        ' ===== Toolbar Initialization =====
        
        ''' <summary>
        ''' Initializes the toolbar with all buttons and controls
        ''' </summary>
        Private Sub InitializeToolbar()
            Try
                pToolbar = New Toolbar()
                pToolbar.ToolbarStyle = ToolbarStyle.Icons
                pToolbar.IconSize = IconSize.SmallToolbar

                ' A click that lands on the toolbar's own background - or on a plain Label
                ' like "Scale:", which doesn't consume button events itself - is otherwise
                ' never claimed by any child widget, so GTK delivers it to the nearest
                ' ancestor that owns an input window, which ends up being the main window
                ' itself. A double-click there is then apparently interpreted as equivalent
                ' to double-clicking the titlebar (toggling maximize/restore) by this
                ' desktop's window manager. Explicitly marking the event handled here stops
                ' it from ever reaching that point.
                AddHandler pToolbar.ButtonPressEvent, AddressOf OnToolbarBackgroundButtonPress

                ' Swallowing the press alone stopped the double-click-to-maximize behavior,
                ' but click-and-drag on the same background still moves the window like a
                ' titlebar - that's very likely the window manager's own X11-level drag
                ' tracking off the raw pointer motion (independent of whether GTK marked the
                ' press "handled"), so also swallow motion here in case it IS GTK-level.
                ' Widgets don't receive motion events by default unless they request the mask.
                pToolbar.Events = pToolbar.Events Or Gdk.EventMask.PointerMotionMask
                AddHandler pToolbar.MotionNotifyEvent, AddressOf OnToolbarBackgroundMotion

                ' Create toolbar items
                CreateRefreshButton()
                pToolbar.Add(New SeparatorToolItem())
                CreateExpandCollapseButtons()
                pToolbar.Add(New SeparatorToolItem())
                CreateScaleControls()
                
                ' Add expanding separator to push close button to the right
                Dim lExpandingSeparator As New SeparatorToolItem()
                lExpandingSeparator.Draw = False
                lExpandingSeparator.Expand = True
                pToolbar.Add(lExpandingSeparator)
                
                'CreateCloseButton()

                ' Wrap in a Gtk.EventBox - a Gtk.ToolItem (the wrapper GTK creates around
                ' each item added to a Toolbar, for its own drag-reorder support) realizes
                ' its own separate input window, so the ButtonPressEvent/MotionNotifyEvent
                ' handlers wired directly on pToolbar above only ever catch truly bare
                ' Toolbar canvas outside every ToolItem (e.g. the trailing gap past the
                ' expanding separator) - anything landing within a ToolItem's own bounds but
                ' not claimed by a deeper interactive child (a button's own padding, the
                ' plain "Scale:" Label) never reaches them. EventBox exists specifically to
                ' give an area a real window so it can reliably claim events like this;
                ' wrapping the whole toolbar in one guarantees the same swallow-handlers
                ' actually see everything not already claimed by a button/combo's own
                ' window, without interfering with those - GTK still delivers directly to
                ' the most specific windowed widget under the cursor first.
                '
                ' KNOWN LIMITATION - see the matching comment in MainWindow.vb's own
                ' EventBox wrap: this fixed double-click-to-maximize but not click-and-drag
                ' still moving the window like a titlebar, believed to be a KDE/KWin-level
                ' behavior below what these handlers can intercept. Accepted as a known
                ' desktop-environment quirk, not something to re-attempt without new
                ' information.
                Dim lToolbarEventBox As New EventBox()
                lToolbarEventBox.Events = Gdk.EventMask.ButtonPressMask Or Gdk.EventMask.PointerMotionMask
                AddHandler lToolbarEventBox.ButtonPressEvent, AddressOf OnToolbarBackgroundButtonPress
                AddHandler lToolbarEventBox.MotionNotifyEvent, AddressOf OnToolbarBackgroundMotion
                lToolbarEventBox.Add(pToolbar)

                ' Add toolbar to container
                PackStart(lToolbarEventBox, False, False, 0)
                
            Catch ex As Exception
                Console.WriteLine($"InitializeToolbar error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates a small icon-only bevel-styled toolbar button and wraps it in a
        ''' ToolItem, matching AIAssistantPanel.CreateActionButton's icon-loading pattern
        ''' </summary>
        ''' <param name="vIconName">Icon theme name to load</param>
        ''' <param name="vTooltip">Tooltip text shown on hover</param>
        Private Function CreateToolbarButton(vIconName As String, vTooltip As String) As CustomDrawButton
            Dim lIconPixbuf As Gdk.Pixbuf = Nothing
            Try
                lIconPixbuf = Gtk.IconTheme.Default.LoadIcon(vIconName, 16, IconLookupFlags.UseBuiltin)
            Catch ex As Exception
                Console.WriteLine($"CreateToolbarButton icon load error: {ex.Message}")
            End Try

            Dim lButton As New CustomDrawButton("", lIconPixbuf)
            lButton.TooltipText = vTooltip
            lButton.ThemeManager = pThemeManager

            Dim lItem As New ToolItem()
            lItem.Add(lButton)
            pToolbar.Add(lItem)

            Return lButton
        End Function

        ''' <summary>
        ''' Creates the refresh button
        ''' </summary>
        Private Sub CreateRefreshButton()
            Try
                pRefreshButton = CreateToolbarButton("view-refresh", "Refresh project tree")
                AddHandler pRefreshButton.Clicked, AddressOf OnRefreshButtonClicked

            Catch ex As Exception
                Console.WriteLine($"CreateRefreshButton error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Creates expand and collapse all buttons
        ''' </summary>
        Private Sub CreateExpandCollapseButtons()
            Try
                ' Collapse all button
                pCollapseAllButton = CreateToolbarButton("list-remove", "Collapse all nodes")
                AddHandler pCollapseAllButton.Clicked, AddressOf OnCollapseAllButtonClicked

                ' Expand all button
                pExpandAllButton = CreateToolbarButton("list-add", "Expand all nodes")
                AddHandler pExpandAllButton.Clicked, AddressOf OnExpandAllButtonClicked

            Catch ex As Exception
                Console.WriteLine($"CreateExpandCollapseButtons error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the scale control combo box (unified with Object Explorer)
        ''' </summary>
        Private Sub CreateScaleControls()
            Try
                ' Create container for scale controls
                Dim lScaleItem As New ToolItem()
                Dim lScaleBox As New Box(Orientation.Horizontal, 4)
                
                ' Create label - a plain Gtk.Label requests/handles no events of its own by
                ' default, so an unclaimed click-and-drag on it was bubbling up and being
                ' interpreted by the window manager as dragging the window, the same root
                ' cause as CustomDrawButton's identical fix. Reuses this file's own
                ' toolbar-background swallow handlers since the behavior needed is identical.
                pScaleLabel = New Label("Scale:")
                pScaleLabel.Events = Gdk.EventMask.ButtonPressMask Or Gdk.EventMask.PointerMotionMask
                AddHandler pScaleLabel.ButtonPressEvent, AddressOf OnToolbarBackgroundButtonPress
                AddHandler pScaleLabel.MotionNotifyEvent, AddressOf OnToolbarBackgroundMotion
                lScaleBox.PackStart(pScaleLabel, False, False, 0)
                
                ' Create combo box with preset scales - CustomDrawComboBox has no built-in
                ' natural-width sizing (it's custom-drawn, so GTK's layout system has no way
                ' to know its content's size on its own) and is packed with Fill/Expand both
                ' False below, so without an explicit WidthRequest it collapses to
                ' essentially nothing - visually just the "Scale:" label with nothing next to it
                pScaleCombo = New CustomDrawComboBox()
                pScaleCombo.ThemeManager = pThemeManager
                pScaleCombo.WidthRequest = 70
                pScaleCombo.AppendText("50%")
                pScaleCombo.AppendText("75%")
                pScaleCombo.AppendText("100%")
                pScaleCombo.AppendText("125%")
                pScaleCombo.AppendText("150%")
                pScaleCombo.AppendText("175%")
                pScaleCombo.AppendText("200%")
                
                ' Set current scale
                Dim lCurrentScaleText As String = $"{pCurrentScale}%"
                Dim lIndex As Integer = pScaleCombo.IndexOf(lCurrentScaleText)

                If lIndex >= 0 Then
                    pScaleCombo.Active = lIndex
                Else
                    pScaleCombo.Active = 2 ' Default to 100%
                End If
                
                AddHandler pScaleCombo.Changed, AddressOf OnScaleComboChanged
                lScaleBox.PackStart(pScaleCombo, False, False, 0)
                
                lScaleItem.Add(lScaleBox)
                pToolbar.Add(lScaleItem)
                
            Catch ex As Exception
                Console.WriteLine($"CreateScaleControls error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the close button
        ''' </summary>
        Private Sub CreateCloseButton()
            Try
                pCloseButton = CreateToolbarButton("window-close", "Close Project Explorer")
                AddHandler pCloseButton.Clicked, AddressOf OnCloseButtonClicked

            Catch ex As Exception
                Console.WriteLine($"CreateCloseButton error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Toolbar Event Handlers =====
        
        ''' <summary>
        ''' Handles refresh button click
        ''' </summary>
        Private Sub OnRefreshButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                RefreshProject()
                
            Catch ex As Exception
                Console.WriteLine($"OnRefreshButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles collapse all button click
        ''' </summary>
        Private Sub OnCollapseAllButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                CollapseAll()
                
            Catch ex As Exception
                Console.WriteLine($"OnCollapseAllButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles expand all button click
        ''' </summary>
        Private Sub OnExpandAllButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                ExpandAll()
                
            Catch ex As Exception
                Console.WriteLine($"OnExpandAllButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles scale combo box change
        ''' </summary>
        Private Sub OnScaleComboChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pScaleCombo.ActiveText IsNot Nothing Then
                    Dim lScaleText As String = pScaleCombo.ActiveText.Replace("%", "")
                    Dim lScale As Integer
                    If Integer.TryParse(lScaleText, lScale) Then
                        ' Apply scale and save to unified setting
                        ApplyScale(lScale)
                        SaveUnifiedTextScale(lScale)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnScaleComboChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles close button click
        ''' </summary>
        Private Sub OnCloseButtonClicked(vSender As Object, vArgs As EventArgs)
            Try
                RaiseEvent CloseRequested()
                
            Catch ex As Exception
                Console.WriteLine($"OnCloseButtonClicked error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Toolbar Actions =====
        
        ''' <summary>
        ''' Collapses all nodes in the tree
        ''' </summary>
        Private Sub CollapseAll()
            Try
                pExpandedNodes.Clear()
                
                ' Keep root expanded
                If pRootNode IsNot Nothing Then
                    pExpandedNodes.Add(GetNodePath(pRootNode))
                End If
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CollapseAll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Expands all nodes in the tree
        ''' </summary>
        Private Sub ExpandAll()
            Try
                If pRootNode IsNot Nothing Then
                    ExpandNodeRecursive(pRootNode)
                End If
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ExpandAll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively expands a node and all its children
        ''' </summary>
        Private Sub ExpandNodeRecursive(vNode As ProjectNode)
            Try
                If vNode Is Nothing Then Return
                
                If vNode.Children.Count > 0 Then
                    pExpandedNodes.Add(GetNodePath(vNode))
                    
                    For Each lChild In vNode.Children
                        ExpandNodeRecursive(lChild)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExpandNodeRecursive error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the scale display in the combo box
        ''' </summary>
        Private Sub UpdateScaleDisplay()
            Try
                Dim lScaleText As String = $"{pCurrentScale}%"

                ' Find and select the matching scale
                Dim lMatchIndex As Integer = pScaleCombo.IndexOf(lScaleText)
                If lMatchIndex >= 0 Then
                    pScaleCombo.Active = lMatchIndex
                    Return
                End If
                
                ' If no exact match, set to custom value
                ' Note: GTK ComboBoxText doesn't easily support custom text
                ' For now, just select closest value
                Dim lClosestIndex As Integer = 2 ' Default to 100%
                Dim lClosestDiff As Integer = Integer.MaxValue
                
                Dim lScaleValues() As Integer = {50, 75, 100, 125, 150, 175, 200}
                For i As Integer = 0 To lScaleValues.Length - 1
                    Dim lDiff As Integer = Math.Abs(lScaleValues(i) - pCurrentScale)
                    If lDiff < lClosestDiff Then
                        lClosestDiff = lDiff
                        lClosestIndex = i
                    End If
                Next
                
                pScaleCombo.Active = lClosestIndex
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScaleDisplay error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Swallows a button press that lands on the toolbar's own background (not on any
        ''' button/control) so it can never propagate further - see the AddHandler site in
        ''' InitializeToolbar for why this exists
        ''' </summary>
        Private Sub OnToolbarBackgroundButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            Try
                vArgs.RetVal = True
            Catch ex As Exception
                Console.WriteLine($"OnToolbarBackgroundButtonPress error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Swallows pointer motion over the toolbar's own background - see the AddHandler
        ''' site in InitializeToolbar for why this exists
        ''' </summary>
        Private Sub OnToolbarBackgroundMotion(vSender As Object, vArgs As MotionNotifyEventArgs)
            Try
                vArgs.RetVal = True
            Catch ex As Exception
                Console.WriteLine($"OnToolbarBackgroundMotion error: {ex.Message}")
            End Try
        End Sub

    End Class
    
End Namespace
