' Widgets/CustomDrawObjectExplorer.Toolbar.vb - Toolbar implementation
' Created: 2025-08-18
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing toolbar functionality for the Object Explorer
    ''' </summary>
    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
        
        ' ===== Toolbar Controls =====
        Private pToolbar As Toolbar
        Private pExpandButton As ToolButton
        Private pCollapseButton As ToolButton
        Private pRefreshButton As ToolButton
        Private pSearchEntry As SearchEntry
        Private pSearchItem As ToolItem
        Private pScaleCombo As ComboBoxText
        Private pScaleItem As ToolItem
        
        ' ===== Search State =====
        Private pSearchText As String = String.Empty
        Private pSearchResults As New List(Of VisualNode)
        Private pCurrentSearchIndex As Integer = -1
        
        ''' <summary>
        ''' Creates and initializes the toolbar with all controls
        ''' </summary>
        ''' <summary>
        ''' Creates and initializes the toolbar with all controls
        ''' </summary>
        Private Sub CreateToolbar()
            Try
                ' Create toolbar
                pToolbar = New Toolbar()
                pToolbar.ToolbarStyle = ToolbarStyle.Icons
                pToolbar.IconSize = IconSize.SmallToolbar
                
                ' Create refresh button FIRST (to match Project Explorer order)
                CreateRefreshButton()
                
                ' Add separator
                pToolbar.Add(New SeparatorToolItem())
                
                ' Create collapse all button FIRST
                CreateCollapseButton()
                
                ' Create expand all button SECOND
                CreateExpandButton()
                
                ' Add separator
                pToolbar.Add(New SeparatorToolItem())
                
                ' Create search controls
                CreateSearchControls()
                
                ' Add expanding separator to push scale to right
                Dim lExpandingSeparator As New SeparatorToolItem()
                lExpandingSeparator.Draw = False
                lExpandingSeparator.Expand = True
                pToolbar.Add(lExpandingSeparator)
                
                ' Create scale controls
                CreateScaleControls()
                
                ' Pack toolbar at top
                PackStart(pToolbar, False, False, 0)
                
            Catch ex As Exception
                Console.WriteLine($"CreateToolbar error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the expand all button
        ''' </summary>
        Private Sub CreateExpandButton()
            Try
                pExpandButton = New ToolButton(Stock.Add)
                pExpandButton.TooltipText = "Expand All"
                AddHandler pExpandButton.Clicked, AddressOf OnExpandAllClicked
                pToolbar.Add(pExpandButton)
                
            Catch ex As Exception
                Console.WriteLine($"CreateExpandButton error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the collapse all button
        ''' </summary>
        Private Sub CreateCollapseButton()
            Try
                pCollapseButton = New ToolButton(Stock.Remove)
                pCollapseButton.TooltipText = "Collapse All"
                AddHandler pCollapseButton.Clicked, AddressOf OnCollapseAllClicked
                pToolbar.Add(pCollapseButton)
                
            Catch ex As Exception
                Console.WriteLine($"CreateCollapseButton error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the refresh button
        ''' </summary>
        Private Sub CreateRefreshButton()
            Try
                pRefreshButton = New ToolButton(Stock.Refresh)
                pRefreshButton.TooltipText = "Refresh Structure"
                AddHandler pRefreshButton.Clicked, AddressOf OnRefreshClicked
                pToolbar.Add(pRefreshButton)
                
            Catch ex As Exception
                Console.WriteLine($"CreateRefreshButton error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the search controls
        ''' </summary>
        Private Sub CreateSearchControls()
            Try
                ' Create container for search entry
                pSearchItem = New ToolItem()
                
                ' Create search entry
                pSearchEntry = New SearchEntry()
                pSearchEntry.PlaceholderText = "Search..."
                pSearchEntry.WidthRequest = 200
                
                ' Connect events
                AddHandler pSearchEntry.SearchChanged, AddressOf OnSearchTextChanged
                AddHandler pSearchEntry.Activated, AddressOf OnSearchActivated
                AddHandler pSearchEntry.StopSearch, AddressOf OnSearchStopped
                AddHandler pSearchEntry.NextMatch, AddressOf OnSearchNext
                AddHandler pSearchEntry.PreviousMatch, AddressOf OnSearchPrevious
                
                pSearchItem.Add(pSearchEntry)
                pToolbar.Add(pSearchItem)
                
            Catch ex As Exception
                Console.WriteLine($"CreateSearchControls error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the scale controls
        ''' </summary>
        Private Sub CreateScaleControls()
            Try
                ' Create container
                pScaleItem = New ToolItem()
                
                ' Create horizontal box for scale controls
                Dim lScaleBox As New Box(Orientation.Horizontal, 4)
                
                ' Add scale label
                Dim lScaleLabel As New Label("Scale:")
                lScaleBox.PackStart(lScaleLabel, False, False, 0)
                
                ' Create combo box with preset scales
                pScaleCombo = New ComboBoxText()
                pScaleCombo.AppendText("50%")
                pScaleCombo.AppendText("75%")
                pScaleCombo.AppendText("100%")
                pScaleCombo.AppendText("125%")
                pScaleCombo.AppendText("150%")
                pScaleCombo.AppendText("175%")
                pScaleCombo.AppendText("200%")
                
                ' Set current scale
                UpdateScaleDisplay()
                
                AddHandler pScaleCombo.Changed, AddressOf OnScaleComboChanged
                lScaleBox.PackStart(pScaleCombo, False, False, 0)
                
                pScaleItem.Add(lScaleBox)
                pToolbar.Add(pScaleItem)
                
            Catch ex As Exception
                Console.WriteLine($"CreateScaleControls error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the scale combo box to show current scale
        ''' </summary>
        Private Sub UpdateScaleDisplay()
            Try
                If pScaleCombo Is Nothing Then Return
                
                Dim lCurrentScaleText As String = $"{pCurrentScale}%"
                Dim lIndex As Integer = -1
                
                For i As Integer = 0 To pScaleCombo.Model.IterNChildren() - 1
                    Dim lIter As TreeIter
                    If pScaleCombo.Model.IterNthChild(lIter, TreeIter.Zero, i) Then
                        Dim lText As String = CStr(pScaleCombo.Model.GetValue(lIter, 0))
                        If lText = lCurrentScaleText Then
                            lIndex = i
                            Exit For
                        End If
                    End If
                Next
                
                If lIndex >= 0 Then
                    pScaleCombo.Active = lIndex
                Else
                    ' Add custom value and select it
                    pScaleCombo.AppendText(lCurrentScaleText)
                    pScaleCombo.Active = pScaleCombo.Model.IterNChildren() - 1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScaleDisplay error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handles expand all button click
        ''' </summary>
        Private Sub OnExpandAllClicked(vSender As Object, vE As EventArgs)
            Try
                ExpandAll()
            Catch ex As Exception
                Console.WriteLine($"OnExpandAllClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles collapse all button click
        ''' </summary>
        Private Sub OnCollapseAllClicked(vSender As Object, vE As EventArgs)
            Try
                CollapseAll()
            Catch ex As Exception
                Console.WriteLine($"OnCollapseAllClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles refresh button click
        ''' </summary>
        Private Sub OnRefreshClicked(vSender As Object, vE As EventArgs)
            Try
                RefreshStructure()
            Catch ex As Exception
                Console.WriteLine($"OnRefreshClicked error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles search text changes
        ''' </summary>
        Private Sub OnSearchTextChanged(vSender As Object, vE As EventArgs)
            Try
                pSearchText = pSearchEntry.Text.Trim()
                PerformSearch()
            Catch ex As Exception
                Console.WriteLine($"OnSearchTextChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles search activation (Enter pressed)
        ''' </summary>
        Private Sub OnSearchActivated(vSender As Object, vE As EventArgs)
            Try
                If pSearchResults.Count > 0 Then
                    NavigateToNextSearchResult()
                End If
            Catch ex As Exception
                Console.WriteLine($"OnSearchActivated error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles search stopped (Escape pressed)
        ''' </summary>
        Private Sub OnSearchStopped(vSender As Object, vE As EventArgs)
            Try
                ClearSearch()
            Catch ex As Exception
                Console.WriteLine($"OnSearchStopped error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles navigate to next search result
        ''' </summary>
        Private Sub OnSearchNext(vSender As Object, vE As EventArgs)
            Try
                NavigateToNextSearchResult()
            Catch ex As Exception
                Console.WriteLine($"OnSearchNext error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles navigate to previous search result
        ''' </summary>
        Private Sub OnSearchPrevious(vSender As Object, vE As EventArgs)
            Try
                NavigateToPreviousSearchResult()
            Catch ex As Exception
                Console.WriteLine($"OnSearchPrevious error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles scale combo box change
        ''' </summary>
        Private Sub OnScaleComboChanged(vSender As Object, vE As EventArgs)
            Try
                If pScaleCombo Is Nothing OrElse pScaleCombo.ActiveText Is Nothing Then Return
                
                ' Parse scale from text
                Dim lScaleText As String = pScaleCombo.ActiveText.Replace("%", "")
                Dim lScale As Integer
                
                If Integer.TryParse(lScaleText, lScale) Then
                    ApplyScale(lScale)
                    SaveUnifiedTextScale(lScale)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnScaleComboChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Search Implementation =====
        
        ''' <summary>
        ''' Performs search through the visual tree
        ''' </summary>
        Private Sub PerformSearch()
            Try
                pSearchResults.Clear()
                pCurrentSearchIndex = -1
                
                If String.IsNullOrEmpty(pSearchText) Then
                    pDrawingArea?.QueueDraw()
                    Return
                End If
                
                ' Search through all visual nodes
                SearchVisualNodes(pVisibleNodes, pSearchText.ToLower())
                
                ' Highlight first result if any
                If pSearchResults.Count > 0 Then
                    pCurrentSearchIndex = 0
                    NavigateToSearchResult(0)
                End If
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"PerformSearch error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively searches visual nodes
        ''' </summary>
        Private Sub SearchVisualNodes(vNodes As List(Of VisualNode), vSearchText As String)
            Try
                For Each lNode In vNodes
                    If lNode.Node.Name.ToLower().Contains(vSearchText) Then
                        pSearchResults.Add(lNode)
                    End If
                    
                    ' Search children
                    If lNode.Children.Count > 0 Then
                        SearchVisualNodes(lNode.Children, vSearchText)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SearchVisualNodes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates to next search result
        ''' </summary>
        Private Sub NavigateToNextSearchResult()
            Try
                If pSearchResults.Count = 0 Then Return
                
                pCurrentSearchIndex += 1
                If pCurrentSearchIndex >= pSearchResults.Count Then
                    pCurrentSearchIndex = 0
                End If
                
                NavigateToSearchResult(pCurrentSearchIndex)
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToNextSearchResult error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates to previous search result
        ''' </summary>
        Private Sub NavigateToPreviousSearchResult()
            Try
                If pSearchResults.Count = 0 Then Return
                
                pCurrentSearchIndex -= 1
                If pCurrentSearchIndex < 0 Then
                    pCurrentSearchIndex = pSearchResults.Count - 1
                End If
                
                NavigateToSearchResult(pCurrentSearchIndex)
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToPreviousSearchResult error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Navigates to specific search result
        ''' </summary>
        Private Sub NavigateToSearchResult(vIndex As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pSearchResults.Count Then Return
                
                Dim lNode As VisualNode = pSearchResults(vIndex)
                
                ' Ensure node is visible
                EnsureNodeVisible(lNode)
                
                ' Select the node
                SelectNode(lNode)
                
                ' Scroll to node
                ScrollToNode(lNode)
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToSearchResult error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clears search and results
        ''' </summary>
        Private Sub ClearSearch()
            Try
                pSearchText = String.Empty
                pSearchResults.Clear()
                pCurrentSearchIndex = -1
                pSearchEntry.Text = String.Empty
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ClearSearch error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Unified Scale Management =====
        
        ''' <summary>
        ''' Saves the unified text scale for both explorers
        ''' </summary>
        ''' <param name="vScale">Scale percentage to save</param>
        Private Sub SaveUnifiedTextScale(vScale As Integer)
            Try
                ' Save to unified setting used by both explorers
                pSettingsManager.SetInteger("Explorer.TextScale", vScale)
                pSettingsManager.SaveSettings()
                
                Console.WriteLine($"Saved unified Explorer.TextScale: {vScale}%")
                
            Catch ex As Exception
                Console.WriteLine($"SaveUnifiedTextScale error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
