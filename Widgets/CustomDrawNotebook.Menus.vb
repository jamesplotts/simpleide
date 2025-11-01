' CustomDrawNotebook.Menus.vb - Menu functionality for custom notebook
Imports Gtk
Imports System
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    Partial Public Class CustomDrawNotebook
        Implements ICustomNotebook
        
        ' ===== Tab List Menu =====
        
        ''' <summary>
        ''' Closes all tabs to the left of the specified index
        ''' </summary>
        ''' <param name="vToIndex">Index to close to (exclusive)</param>
        Private Sub CloseTabsToLeft(vToIndex As Integer)
            Try
                for i As Integer = vToIndex - 1 To 0 Step -1
                    RemovePage(i)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CloseTabsToLeft error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Copies the tab label to clipboard
        ''' </summary>
        ''' <param name="vTabIndex">Index of the tab</param>
        Private Sub CopyTabLabelToClipboard(vTabIndex As Integer)
            Try
                If vTabIndex >= 0 AndAlso vTabIndex < pTabs.Count Then
                    Dim lClipboard As Clipboard = Clipboard.GetDefault(Display.Default)
                    lClipboard.Text = pTabs(vTabIndex).Label
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CopyTabLabelToClipboard error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Shows a dropdown menu with all tabs sorted alphabetically
        ''' </summary>
        Private Sub ShowTabsMenu()
            Try
                Dim lMenu As New Menu()
                
                ' Create a list of tab indices with their labels for sorting
                Dim lTabInfoList As New List(Of Tuple(Of Integer, String, TabData))
                For i As Integer = 0 To pTabs.Count - 1
                    lTabInfoList.Add(New Tuple(Of Integer, String, TabData)(i, pTabs(i).Label, pTabs(i)))
                Next
                
                ' Sort the list alphabetically by label (case-insensitive)
                lTabInfoList.Sort(Function(a, b) String.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase))
                
                ' Add entry for each tab in alphabetical order
                For Each lTabInfo In lTabInfoList
                    Dim lTabIndex As Integer = lTabInfo.Item1 ' Original index
                    Dim lTab As TabData = lTabInfo.Item3
                    
                    ' Create menu item with icon and label
                    Dim lMenuItem As New MenuItem()
                    
                    ' Create box for icon and label
                    Dim lBox As New Box(Orientation.Horizontal, 6)
                    
                    ' Add icon if present
                    If Not String.IsNullOrEmpty(lTab.IconName) Then
                        Dim lImage As New Image()
                        lImage.SetFromIconName(lTab.IconName, Gtk.IconSize.Menu)
                        lBox.PackStart(lImage, False, False, 0)
                    End If
                    
                    ' Build label with indicators using Pango markup
                    Dim lLabel As String = System.Net.WebUtility.HtmlEncode(lTab.Label) ' Escape special HTML characters
                    
                    ' Add modified indicator in red
                    If lTab.Modified Then
                        lLabel = "<span foreground='#FF0000'>• </span>" & lLabel
                    End If
                    
                    ' Mark current tab with arrow
                    If lTabIndex = pCurrentTabIndex Then
                        lLabel = "▶ " & lLabel
                    End If
                    
                    Dim lLabelWidget As New Label()
                    lLabelWidget.Markup = lLabel ' Use Markup instead of Text
                    lLabelWidget.Xalign = 0 ' Left align the text
                    lBox.PackStart(lLabelWidget, True, True, 0)
                    
                    lMenuItem.Add(lBox)
                    
                    ' Handle activation - need to capture the original index
                    Dim lCapturedIndex As Integer = lTabIndex
                    AddHandler lMenuItem.Activated, Sub()
                        SetCurrentTab(lCapturedIndex)
                    End Sub
                    
                    lMenu.Add(lMenuItem)
                Next
                
                ' Add separator if there are tabs
                If pTabs.Count > 0 Then
                    lMenu.Add(New SeparatorMenuItem())
                End If
                
                ' Add close all option
                Dim lCloseAllItem As New MenuItem("Close All Tabs")
                AddHandler lCloseAllItem.Activated, Sub() CloseAllTabs()
                lMenu.Add(lCloseAllItem)
                
                ' Show menu
                lMenu.ShowAll()
                lMenu.PopupAtWidget(pDropdownButton, Gdk.Gravity.SouthWest, Gdk.Gravity.NorthWest, Nothing)
                
            Catch ex As Exception
                Console.WriteLine($"ShowTabsMenu error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Context Menu =====
        
        ''' <summary>
        ''' Shows the context menu for a tab
        ''' </summary>
        ''' <param name="vTabIndex">The index of the tab</param>
        ''' <param name="vX">X coordinate for menu position</param>
        ''' <param name="vY">Y coordinate for menu position</param>
        Public Sub ShowTabContextMenu(vPageNum As Integer, vX As Double, vY As Double) Implements ICustomNotebook.ShowTabContextMenu
            Try
                If vPageNum < 0 OrElse vPageNum >= pTabs.Count Then Return
                
                Dim lMenu As New Menu()
                
                ' Close tab
                Dim lCloseItem As New MenuItem("Close")
                AddHandler lCloseItem.Activated, Sub()
                    RemovePage(vPageNum)
                End Sub
                lMenu.Add(lCloseItem)
                
                ' Close others
                Dim lCloseOthersItem As New MenuItem("Close Others")
                lCloseOthersItem.Sensitive = pTabs.Count > 1
                AddHandler lCloseOthersItem.Activated, Sub()
                    CloseOtherTabs(vPageNum)
                End Sub
                lMenu.Add(lCloseOthersItem)
                
                ' Close all to the right
                Dim lCloseRightItem As New MenuItem("Close All to the Right")
                lCloseRightItem.Sensitive = vPageNum < pTabs.Count - 1
                AddHandler lCloseRightItem.Activated, Sub()
                    CloseTabsToRight(vPageNum)
                End Sub
                lMenu.Add(lCloseRightItem)
                
                ' Close all to the left
                Dim lCloseLeftItem As New MenuItem("Close All to the Left")
                lCloseLeftItem.Sensitive = vPageNum > 0
                AddHandler lCloseLeftItem.Activated, Sub()
                    CloseTabsToLeft(vPageNum)
                End Sub
                lMenu.Add(lCloseLeftItem)
                
                lMenu.Add(New SeparatorMenuItem())
                
                ' Pin/Unpin (for future implementation)
                Dim lPinItem As New MenuItem("Pin Tab")
                lPinItem.Sensitive = False ' Not implemented yet
                lMenu.Add(lPinItem)
                
                lMenu.Add(New SeparatorMenuItem())
                
                ' Copy tab info
                Dim lCopyLabelItem As New MenuItem("Copy Tab Name")
                AddHandler lCopyLabelItem.Activated, Sub()
                    CopyTabLabelToClipboard(vPageNum)
                End Sub
                lMenu.Add(lCopyLabelItem)
                
                ' Show menu
                lMenu.ShowAll()
                lMenu.PopupAtPointer(Nothing)
                
            Catch ex As Exception
                Console.WriteLine($"ShowTabContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Menu Actions =====
        
        ''' <summary>
        ''' Closes all tabs
        ''' </summary>
        Private Sub CloseAllTabs()
            Try
                ' Close from end to avoid index issues
                for i As Integer = pTabs.Count - 1 To 0 Step -1
                    RemovePage(i)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CloseAllTabs error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Closes all tabs except the specified one
        ''' </summary>
        ''' <param name="vKeepIndex">Index of tab to keep</param>
        Private Sub CloseOtherTabs(vKeepIndex As Integer)
            Try
                ' Close from end to avoid index issues
                for i As Integer = pTabs.Count - 1 To 0 Step -1
                    If i <> vKeepIndex Then
                        RemovePage(i)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CloseOtherTabs error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Closes all tabs to the right of the specified index
        ''' </summary>
        ''' <param name="vFromIndex">Index to close from (exclusive)</param>
        Private Sub CloseTabsToRight(vFromIndex As Integer)
            Try
                for i As Integer = pTabs.Count - 1 To vFromIndex + 1 Step -1
                    RemovePage(i)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CloseTabsToRight error: {ex.Message}")
            End Try
        End Sub
        
        
    End Class
    
End Namespace