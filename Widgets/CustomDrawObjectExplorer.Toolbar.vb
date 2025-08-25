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
                pExpandButton = New ToolButton(Nothing, "Expand All")
                pExpandButton.IconWidget = Image.NewFromIconName("list-add", IconSize.SmallToolbar)
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
                pCollapseButton = New ToolButton(Nothing, "Collapse All")
                pCollapseButton.IconWidget = Image.NewFromIconName("list-remove", IconSize.SmallToolbar)
                pCollapseButton.TooltipText = "Collapse All"
                AddHandler pCollapseButton.Clicked, AddressOf OnCollapseAllClicked
                pToolbar.Add(pCollapseButton)
                
            Catch ex As Exception
                Console.WriteLine($"CreateCollapseButton error: {ex.Message}")
            End Try
        End Sub
        

        Private Sub CreateRefreshButton()
            Try
                pRefreshButton = New ToolButton(Nothing, "Refresh")
                pRefreshButton.IconWidget = Image.NewFromIconName("view-refresh", IconSize.SmallToolbar)
                pRefreshButton.TooltipText = "Refresh project tree"
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
                ' Create search entry
                pSearchEntry = New SearchEntry()
                pSearchEntry.PlaceholderText = "Search (use dots for FQN)"
                pSearchEntry.WidthRequest = 200
                pSearchEntry.TooltipText = "Search by name or fully qualified name (e.g., SimpleIDE.ClassName.Method)"
                
                ' Hook up enhanced search events
                AddHandler pSearchEntry.Changed, AddressOf OnSearchTextChangedEnhanced
                AddHandler pSearchEntry.Activated, AddressOf OnSearchActivated
                AddHandler pSearchEntry.StopSearch, AddressOf OnSearchStopped
                
                ' Handle arrow keys for search navigation
                AddHandler pSearchEntry.KeyPressEvent, Sub(sender As Object, e As KeyPressEventArgs)
                    Try
                        If e.Event.Key = Gdk.Key.Down Then
                            NavigateToNextSearchResult()
                            e.RetVal = True
                        ElseIf e.Event.Key = Gdk.Key.Up Then
                            NavigateToPreviousSearchResult()
                            e.RetVal = True
                        ElseIf e.Event.Key = Gdk.Key.Escape Then
                            ClearSearch()
                            e.RetVal = True
                        End If
                    Catch ex As Exception
                        Console.WriteLine($"Search key handling error: {ex.Message}")
                    End Try
                End Sub
                
                ' Create tool item to hold search entry
                pSearchItem = New ToolItem()
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
                
                for i As Integer = 0 To pScaleCombo.Model.IterNChildren() - 1
                    Dim lIter As TreeIter
                    If pScaleCombo.Model.IterNthChild(lIter, TreeIter.Zero, i) Then
                        Dim lText As String = CStr(pScaleCombo.Model.GetValue(lIter, 0))
                        If lText = lCurrentScaleText Then
                            lIndex = i
                            Exit for
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
        ''' Handles search activation (Enter pressed) - expands and navigates to found node
        ''' </summary>
        Private Sub OnSearchActivated(vSender As Object, vE As EventArgs)
            Try
                ' If no search text, do nothing
                If String.IsNullOrEmpty(pSearchText) Then
                    Return
                End If
                
                ' If we have search results from the current search
                If pSearchResults.Count > 0 Then
                    ' Get the current result (or first if none selected)
                    Dim lTargetNode As VisualNode = Nothing
                    
                    If pCurrentSearchIndex >= 0 AndAlso pCurrentSearchIndex < pSearchResults.Count Then
                        lTargetNode = pSearchResults(pCurrentSearchIndex)
                    Else
                        ' Select first result
                        pCurrentSearchIndex = 0
                        lTargetNode = pSearchResults(0)
                    End If
                    
                    If lTargetNode IsNot Nothing Then
                        ' Expand all parent nodes to make this node visible
                        ExpandParentsAndNavigate(lTargetNode)
                        
                        ' Fire the NodeActivated event to actually navigate to the code
                        RaiseEvent NodeActivated(lTargetNode.Node)
                    End If
                Else
                    ' No results found - maybe perform a fresh search
                    PerformSearch()
                    
                    ' If we found results now, navigate to the first one
                    If pSearchResults.Count > 0 Then
                        pCurrentSearchIndex = 0
                        Dim lTargetNode As VisualNode = pSearchResults(0)
                        
                        ' Expand parents and navigate
                        ExpandParentsAndNavigate(lTargetNode)
                        
                        ' Fire the NodeActivated event
                        RaiseEvent NodeActivated(lTargetNode.Node)
                    Else
                        ' Show no results message
                        pSearchEntry.PlaceholderText = "No matches found"
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnSearchActivated error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Expands all parent nodes and navigates to the target node
        ''' </summary>
        ''' <param name="vTargetNode">The node to navigate to</param>
        Private Sub ExpandParentsAndNavigate(vTargetNode As VisualNode)
            Try
                If vTargetNode Is Nothing Then Return
                
                Console.WriteLine($"ExpandParentsAndNavigate: Target = {vTargetNode.Node.Name}, Path = {vTargetNode.NodePath}")
                
                ' Build list of all parent paths
                Dim lParentPaths As New List(Of String)()
                Dim lCurrentPath As String = ""
                Dim lPathParts As String() = vTargetNode.NodePath.Split("/"c)
                
                ' Build parent paths progressively
                for i As Integer = 0 To lPathParts.Length - 2 ' -2 to exclude the target node itself
                    If i = 0 Then
                        lCurrentPath = lPathParts(i)
                    Else
                        lCurrentPath = lCurrentPath & "/" & lPathParts(i)
                    End If
                    lParentPaths.Add(lCurrentPath)
                    Console.WriteLine($"  Parent path to expand: {lCurrentPath}")
                Next
                
                ' Expand all parent paths
                Dim lNeedRebuild As Boolean = False
                for each lPath in lParentPaths
                    If Not pExpandedNodes.Contains(lPath) Then
                        pExpandedNodes.Add(lPath)
                        lNeedRebuild = True
                        Console.WriteLine($"  Expanded: {lPath}")
                    End If
                Next
                
                ' Rebuild the visual tree if we expanded anything
                If lNeedRebuild Then
                    Console.WriteLine("  Rebuilding visual tree with expanded parents...")
                    RebuildVisualTree()
                End If
                
                ' Find the target node in the current visible nodes
                Dim lFoundNode As VisualNode = Nothing
                for each lNode in pVisibleNodes
                    If lNode.NodePath = vTargetNode.NodePath Then
                        lFoundNode = lNode
                        Exit for
                    End If
                Next
                
                ' Now select and scroll to the node
                If lFoundNode IsNot Nothing Then
                    SelectNode(lFoundNode)
                    ScrollToNode(lFoundNode)
                    pDrawingArea?.QueueDraw()
                    Console.WriteLine($"  Successfully navigated to: {lFoundNode.Node.Name}")
                Else
                    Console.WriteLine($"  ERROR: Could not find node after expansion: {vTargetNode.NodePath}")
                    ' Try to at least select the last parent
                    If lParentPaths.Count > 0 Then
                        Dim lLastParentPath As String = lParentPaths(lParentPaths.Count - 1)
                        for each lNode in pVisibleNodes
                            If lNode.NodePath = lLastParentPath Then
                                SelectNode(lNode)
                                ScrollToNode(lNode)
                                pDrawingArea?.QueueDraw()
                                Console.WriteLine($"  Selected parent instead: {lNode.Node.Name}")
                                Exit for
                            End If
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExpandParentsAndNavigate error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Finds a visual node by its path
        ''' </summary>
        ''' <param name="vPath">The node path to search for</param>
        ''' <returns>The visual node if found, Nothing otherwise</returns>
        Private Function FindVisualNodeByPath(vPath As String) As VisualNode
            Try
                If String.IsNullOrEmpty(vPath) Then Return Nothing
                
                ' Check the cache first
                If pNodeCache.ContainsKey(vPath) Then
                    Return pNodeCache(vPath)
                End If
                
                ' Search through visible nodes
                for each lNode in pVisibleNodes
                    If lNode.NodePath = vPath Then
                        Return lNode
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindVisualNodeByPath error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced key press handler for search entry with direct navigation
        ''' </summary>
        Private Sub OnSearchKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
            Try
                Select Case vArgs.Event.Key
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        ' Enter key - navigate to current/first result and activate it
                        OnSearchActivated(vSender, EventArgs.Empty)
                        vArgs.RetVal = True ' Mark as handled
                        
                    Case Gdk.Key.Down
                        ' Down arrow - move to next search result
                        NavigateToNextSearchResult()
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.Up
                        ' Up arrow - move to previous search result
                        NavigateToPreviousSearchResult()
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.Escape
                        ' Escape - clear search
                        ClearSearch()
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.F3
                        ' F3 - find next
                        NavigateToNextSearchResult()
                        vArgs.RetVal = True
                        
                    Case Gdk.Key.F3
                        ' F3 - find next (or Shift+F3 for previous)
                        If (vArgs.Event.State and Gdk.ModifierType.ShiftMask) = Gdk.ModifierType.ShiftMask Then
                            ' Shift+F3 - find previous
                            NavigateToPreviousSearchResult()
                        Else
                            ' F3 - find next
                            NavigateToNextSearchResult()
                        End If
                        vArgs.RetVal = True
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnSearchKeyPress error: {ex.Message}")
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
        ''' Performs search through the visual tree with FQN support
        ''' </summary>
        Private Sub PerformSearch()
            Try
                pSearchResults.Clear()
                pCurrentSearchIndex = -1
                
                If String.IsNullOrEmpty(pSearchText) Then
                    pDrawingArea?.QueueDraw()
                    Return
                End If
                
                Dim lSearchLower As String = pSearchText.ToLower()
                Dim lIsFQNSearch As Boolean = pSearchText.Contains(".")
                
                If lIsFQNSearch Then
                    ' Search by FQN in visible nodes
                    for each lNode in pVisibleNodes
                        Dim lFQN As String = GetNodeFullyQualifiedName(lNode)
                        If Not String.IsNullOrEmpty(lFQN) Then
                            Dim lFQNLower As String = lFQN.ToLower()
                            
                            ' Check for FQN match
                            If lFQNLower.Contains(lSearchLower) Then
                                pSearchResults.Add(lNode)
                            End If
                        End If
                    Next
                Else
                    ' Regular name search in visible nodes
                    SearchVisualNodes(pVisibleNodes, lSearchLower)
                End If
                
                ' Sort results by quality if FQN search
                If lIsFQNSearch AndAlso pSearchResults.Count > 1 Then
                    SortSearchResultsByQuality(lSearchLower)
                End If
                
                ' Update status
                If pSearchResults.Count > 0 Then
                    pCurrentSearchIndex = 0
                    pSearchEntry.PlaceholderText = $"Found {pSearchResults.Count} result(s)"
                    Console.WriteLine($"Search found {pSearchResults.Count} results for '{pSearchText}'")
                Else
                    pSearchEntry.PlaceholderText = "No results found"
                    Console.WriteLine($"No results found for '{pSearchText}'")
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
                for each lNode in vNodes
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



        ''' <summary>
        ''' Recursively searches visual nodes by fully qualified name
        ''' </summary>
        ''' <param name="vNodes">List of nodes to search</param>
        ''' <param name="vSearchText">Search text (lowercase)</param>
        Private Sub SearchVisualNodesByFQN(vNodes As List(Of VisualNode), vSearchText As String)
            Try
                ' Check if this looks like a complete FQN
                Dim lIsCompleteFQN As Boolean = vSearchText.StartsWith("simplide.") OrElse 
                                                vSearchText.Contains(".") AndAlso 
                                                vSearchText.Split("."c).Length >= 3
                
                ' Search through provided nodes only
                for each lNode in vNodes
                    ' Get the FQN for this node
                    Dim lFQN As String = GetNodeFullyQualifiedName(lNode)
                    
                    If Not String.IsNullOrEmpty(lFQN) Then
                        Dim lFQNLower As String = lFQN.ToLower()
                        Dim lIsMatch As Boolean = False
                        
                        If lIsCompleteFQN Then
                            ' For complete FQNs, try exact matching first
                            lIsMatch = (lFQNLower = vSearchText)
                            
                            ' If no exact match, try ending with the search text
                            If Not lIsMatch AndAlso lFQNLower.EndsWith(vSearchText) Then
                                Dim lIndex As Integer = lFQNLower.LastIndexOf(vSearchText)
                                If lIndex > 0 AndAlso lFQNLower(lIndex - 1) = "."c Then
                                    lIsMatch = True
                                ElseIf lIndex = 0 Then
                                    lIsMatch = True
                                End If
                            End If
                        Else
                            ' For partial FQNs, use contains matching
                            If lFQNLower.Contains(vSearchText) Then
                                lIsMatch = True
                            End If
                        End If
                        
                        If lIsMatch Then
                            pSearchResults.Add(lNode)
                        End If
                    End If
                Next
                
                ' Sort results by match quality
                If pSearchResults.Count > 1 Then
                    SortSearchResultsByQuality(vSearchText)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SearchVisualNodesByFQN error: {ex.Message}")
            End Try
        End Sub
        
        
        ''' <summary>
        ''' Enhanced search text change handler with smart FQN detection
        ''' </summary>
        Private Sub OnSearchTextChangedEnhanced(vSender As Object, vE As EventArgs)
            Try
                pSearchText = pSearchEntry.Text.Trim()
                
                ' Provide visual feedback for FQN search mode
                If pSearchText.Contains(".") Then
                    pSearchEntry.TooltipText = "Searching by fully qualified name"
                Else
                    pSearchEntry.TooltipText = "Searching by simple name (use dots for FQN search)"
                End If
                
                ' Perform the enhanced search with auto-expansion
                PerformSearchWithAutoExpansion()
                
            Catch ex As Exception
                Console.WriteLine($"OnSearchTextChangedEnhanced error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets tooltip text for a node, including FQN
        ''' </summary>
        ''' <param name="vNode">The visual node to get tooltip for</param>
        ''' <returns>Formatted tooltip text with FQN and documentation</returns>
        Private Function GetNodeTooltip(vNode As VisualNode) As String
            Try
                If vNode Is Nothing OrElse vNode.Node Is Nothing Then
                    Return String.Empty
                End If
                
                Dim lTooltip As New System.Text.StringBuilder()
                Dim lSyntaxNode As SyntaxNode = vNode.Node
                
                ' Add the fully qualified name at the top
                Dim lFQN As String = GetNodeFullyQualifiedName(vNode)
                If Not String.IsNullOrEmpty(lFQN) Then
                    lTooltip.AppendLine($"<b>{lFQN}</b>")
                    lTooltip.AppendLine()
                End If
                
                ' Add node type
                lTooltip.Append($"<i>{lSyntaxNode.NodeType.ToString().Substring(1)}</i>")
                
                ' Add visibility modifiers
                Dim lModifiers As New List(Of String)()
                If lSyntaxNode.IsPublic Then lModifiers.Add("Public")
                If lSyntaxNode.IsPrivate Then lModifiers.Add("Private")
                If lSyntaxNode.IsProtected Then lModifiers.Add("Protected")
                If lSyntaxNode.IsFriend Then lModifiers.Add("Friend")
                If lSyntaxNode.IsShared Then lModifiers.Add("Shared")
                If lSyntaxNode.IsOverridable Then lModifiers.Add("Overridable")
                If lSyntaxNode.IsOverrides Then lModifiers.Add("Overrides")
                If lSyntaxNode.IsMustOverride Then lModifiers.Add("MustOverride")
                
                If lModifiers.Count > 0 Then
                    lTooltip.Append($" - {String.Join(", ", lModifiers)}")
                End If
                
                ' Add documentation if available
                If Not String.IsNullOrEmpty(lSyntaxNode.Summary) Then
                    lTooltip.AppendLine()
                    lTooltip.AppendLine()
                    lTooltip.Append(lSyntaxNode.Summary)
                End If
                
                ' Add remarks if available
                If Not String.IsNullOrEmpty(lSyntaxNode.Remarks) Then
                    lTooltip.AppendLine()
                    lTooltip.Append($"<small>{lSyntaxNode.Remarks}</small>")
                End If
                
                ' Add file location if available
                If lSyntaxNode.Attributes IsNot Nothing AndAlso 
                   lSyntaxNode.Attributes.ContainsKey("FilePath") Then
                    Dim lFilePath As String = lSyntaxNode.Attributes("FilePath").ToString()
                    lTooltip.AppendLine()
                    lTooltip.AppendLine()
                    lTooltip.Append($"<small>File: {System.IO.Path.GetFileName(lFilePath)}</small>")
                    If lSyntaxNode.StartLine > 0 Then
                        lTooltip.Append($" <small>(Line {lSyntaxNode.StartLine})</small>")
                    End If
                End If
                
                ' Add hint about searchability
                lTooltip.AppendLine()
                lTooltip.AppendLine()
                lTooltip.Append($"<small><i>Searchable as: {lFQN}</i></small>")
                
                Return lTooltip.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"GetNodeTooltip error: {ex.Message}")
                Return String.Empty
            End Try
        End Function

        ''' <summary>
        ''' Sorts search results by match quality (exact matches first)
        ''' </summary>
        ''' <param name="vSearchText">The search text used</param>
        Private Sub SortSearchResultsByQuality(vSearchText As String)
            Try
                pSearchResults.Sort(Function(a, b)
                    Dim lFQNA As String = GetNodeFullyQualifiedName(a).ToLower()
                    Dim lFQNB As String = GetNodeFullyQualifiedName(b).ToLower()
                    
                    ' Exact matches come first
                    Dim lExactA As Boolean = (lFQNA = vSearchText)
                    Dim lExactB As Boolean = (lFQNB = vSearchText)
                    
                    If lExactA AndAlso Not lExactB Then Return -1
                    If lExactB AndAlso Not lExactA Then Return 1
                    
                    ' Then matches at the end (missing root namespace)
                    Dim lEndsA As Boolean = lFQNA.EndsWith(vSearchText)
                    Dim lEndsB As Boolean = lFQNB.EndsWith(vSearchText)
                    
                    If lEndsA AndAlso Not lEndsB Then Return -1
                    If lEndsB AndAlso Not lEndsA Then Return 1
                    
                    ' Then matches at word boundaries
                    Dim lBoundaryA As Boolean = lFQNA.Contains("." & vSearchText)
                    Dim lBoundaryB As Boolean = lFQNB.Contains("." & vSearchText)
                    
                    If lBoundaryA AndAlso Not lBoundaryB Then Return -1
                    If lBoundaryB AndAlso Not lBoundaryA Then Return 1
                    
                    ' Finally, sort by FQN length (shorter is better)
                    Return lFQNA.Length.CompareTo(lFQNB.Length)
                End Function)
                
            Catch ex As Exception
                Console.WriteLine($"SortSearchResultsByQuality error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Performs search with automatic node expansion for progressive FQN searches
        ''' </summary>
        Private Sub PerformSearchWithAutoExpansion()
            Try
                pSearchResults.Clear()
                pCurrentSearchIndex = -1
                
                If String.IsNullOrEmpty(pSearchText) Then
                    pDrawingArea?.QueueDraw()
                    Return
                End If
                
                Dim lSearchLower As String = pSearchText.ToLower()
                Dim lIsFQNSearch As Boolean = pSearchText.Contains(".")
                
                If lIsFQNSearch Then
                    ' Handle progressive FQN search with auto-expansion
                    HandleProgressiveFQNSearch(lSearchLower)
                Else
                    ' Simple search in visible nodes only
                    for each lNode in pVisibleNodes
                        If lNode.Node.Name.ToLower().Contains(lSearchLower) Then
                            pSearchResults.Add(lNode)
                        End If
                    Next
                    
                    ' Select best match and update status
                    If pSearchResults.Count > 0 Then
                        SelectBestSearchMatch()
                    End If
                    UpdateSearchStatus()
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"PerformSearchWithAutoExpansion error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles progressive FQN search with automatic expansion of matching paths
        ''' </summary>
        ''' <param name="vSearchText">The search text in lowercase</param>
        Private Sub HandleProgressiveFQNSearch(vSearchText As String)
            Try
                Console.WriteLine($"HandleProgressiveFQNSearch: '{vSearchText}'")
                
                ' Split the search into parts
                Dim lParts As String() = vSearchText.Split("."c)
                Dim lNeedsRebuild As Boolean = False
                
                ' First, find the actual case-sensitive paths we need to expand
                Dim lPathsToExpand As New List(Of String)()
                
                ' Build a temporary complete tree to find actual paths
                If pRootNode IsNot Nothing Then
                    Dim lTempNodes As New List(Of VisualNode)()
                    BuildCompleteVisualTree(pRootNode, Nothing, 0, "", lTempNodes)
                    
                    ' Now find the actual paths that match our search parts
                    For i As Integer = 0 To Math.Min(lParts.Length - 2, 10) ' -2 because we don't expand the final target
                        ' Build the FQN we're looking for up to this level
                        Dim lSearchFQN As String = String.Join(".", lParts.Take(i + 1)).ToLower()
                        
                        ' Find nodes that match this FQN
                        For Each lNode In lTempNodes
                            Dim lNodeFQN As String = GetNodeFullyQualifiedName(lNode)
                            If Not String.IsNullOrEmpty(lNodeFQN) AndAlso lNodeFQN.ToLower() = lSearchFQN Then
                                ' This is a node we need to expand - use its actual NodePath (case-sensitive)
                                If Not pExpandedNodes.Contains(lNode.NodePath) Then
                                    lPathsToExpand.Add(lNode.NodePath)
                                    Console.WriteLine($"  Will expand: {lNode.NodePath} (FQN: {lNodeFQN})")
                                End If
                                Exit For ' Found the node for this level
                            End If
                        Next
                    Next
                End If
                
                ' Expand all necessary paths
                For Each lPath In lPathsToExpand
                    pExpandedNodes.Add(lPath)
                    lNeedsRebuild = True
                    Console.WriteLine($"  Expanded: {lPath}")
                Next
                
                ' Rebuild tree if we expanded anything
                If lNeedsRebuild Then
                    Console.WriteLine("  Rebuilding visual tree...")
                    RebuildVisualTree()
                End If
                
                ' Now search in the visible nodes
                pSearchResults.Clear()
                
                For Each lNode In pVisibleNodes
                    Dim lFQN As String = GetNodeFullyQualifiedName(lNode)
                    If Not String.IsNullOrEmpty(lFQN) Then
                        Dim lFQNLower As String = lFQN.ToLower()
                        
                        ' Check for match
                        If lFQNLower.Contains(vSearchText) OrElse lFQNLower = vSearchText Then
                            pSearchResults.Add(lNode)
                            Console.WriteLine($"  Found match: {lFQN}")
                        End If
                    End If
                Next
                
                ' Sort and select best match
                If pSearchResults.Count > 0 Then
                    ' Sort by exact match first, then by length
                    pSearchResults.Sort(Function(a, b)
                        Dim lFQNA As String = GetNodeFullyQualifiedName(a).ToLower()
                        Dim lFQNB As String = GetNodeFullyQualifiedName(b).ToLower()
                        
                        ' Exact matches first
                        If lFQNA = vSearchText AndAlso lFQNB <> vSearchText Then Return -1
                        If lFQNB = vSearchText AndAlso lFQNA <> vSearchText Then Return 1
                        
                        ' Then by length (shorter = better)
                        Return lFQNA.Length.CompareTo(lFQNB.Length)
                    End Function)
                    
                    ' Select the best match
                    Dim lBestMatch As VisualNode = pSearchResults(0)
                    SelectNode(lBestMatch)
                    ScrollToNode(lBestMatch)
                    pCurrentSearchIndex = 0
                    
                    Console.WriteLine($"  Selected: {GetNodeFullyQualifiedName(lBestMatch)}")
                Else
                    Console.WriteLine($"  No matches found")
                End If
                
                ' Update status
                UpdateSearchStatus()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleProgressiveFQNSearch error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Checks if a node path exists in the tree
        ''' </summary>
        ''' <param name="vPath">The path to check (e.g., "simplide/widgets")</param>
        ''' <returns>True if the path exists</returns>
        Private Function NodePathExists(vPath As String) As Boolean
            Try
                ' Check in the node cache first
                If pNodeCache.ContainsKey(vPath) Then
                    Return True
                End If
                
                ' Check in current visible nodes (much faster than rebuilding)
                For Each lNode In pVisibleNodes
                    If lNode.NodePath.ToLower() = vPath.ToLower() Then
                        Return True
                    End If
                Next
                
                ' Don't build complete tree - just return false
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"NodePathExists error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Adds immediate children of a parent to search results
        ''' </summary>
        ''' <param name="vNodes">All nodes in the tree</param>
        ''' <param name="vParentFQN">Parent FQN to find children for</param>
        Private Sub ShowImmediateChildren(vParentFQN As String)
            Try
                ' Only search visible nodes
                For Each lNode In pVisibleNodes
                    Dim lFQN As String = GetNodeFullyQualifiedName(lNode)
                    If Not String.IsNullOrEmpty(lFQN) Then
                        Dim lFQNLower As String = lFQN.ToLower()
                        
                        ' Check if this is an immediate child
                        If lFQNLower.StartsWith(vParentFQN & ".") Then
                            ' Count dots to ensure it's immediate child
                            Dim lRemainder As String = lFQNLower.Substring(vParentFQN.Length + 1)
                            If Not lRemainder.Contains(".") Then
                                ' This is an immediate child
                                If Not pSearchResults.Contains(lNode) Then
                                    pSearchResults.Add(lNode)
                                End If
                            End If
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ShowImmediateChildren error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Selects the best match from search results and navigates to it
        ''' </summary>
        Private Sub SelectBestSearchMatch()
            Try
                If pSearchResults.Count = 0 Then Return
                
                ' Find the best match (usually the first one after sorting)
                Dim lBestMatch As VisualNode = pSearchResults(0)
                
                ' Find this node in the visible nodes
                For Each lNode In pVisibleNodes
                    If lNode.NodePath = lBestMatch.NodePath Then
                        SelectNode(lNode)
                        ScrollToNode(lNode)
                        pCurrentSearchIndex = 0
                        Exit For
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SelectBestSearchMatch error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates the search status in the placeholder text
        ''' </summary>
        Private Sub UpdateSearchStatus()
            Try
                If pSearchResults.Count > 0 Then
                    pSearchEntry.PlaceholderText = $"Found {pSearchResults.Count} result(s)"
                    Console.WriteLine($"Search found {pSearchResults.Count} results")
                ElseIf Not String.IsNullOrEmpty(pSearchText) Then
                    pSearchEntry.PlaceholderText = "No results found"
                    Console.WriteLine("No results found")
                Else
                    pSearchEntry.PlaceholderText = "Search (use dots for FQN)"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSearchStatus error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds a complete visual tree for searching (includes all nodes, even in collapsed branches)
        ''' </summary>
        ''' <param name="vNode">The syntax node to process</param>
        ''' <param name="vParent">The parent visual node</param>
        ''' <param name="vLevel">The indentation level</param>
        ''' <param name="vParentPath">The path of the parent node</param>
        ''' <param name="vNodeList">The list to add all nodes to</param>
        Private Sub BuildCompleteVisualTree(vNode As SyntaxNode, vParent As VisualNode, vLevel As Integer, 
                                           vParentPath As String, vNodeList As List(Of VisualNode))
            Try
                If vNode Is Nothing Then Return
                
                ' Handle root document nodes (same as BuildVisualNodes)
                If vNode Is pRootNode AndAlso vNode.NodeType = CodeNodeType.eDocument Then
                    For Each lChild In vNode.Children
                        BuildCompleteVisualTree(lChild, vParent, vLevel, vParentPath, vNodeList)
                    Next
                    Return
                End If
                
                ' Check if node should be displayed (using the same logic as BuildVisualNodes)
                If Not ShouldDisplayNode(vNode) Then Return
                
                ' Create visual node with same structure as BuildVisualNodes
                Dim lVisualNode As New VisualNode() With {
                    .Node = vNode,
                    .Level = vLevel,
                    .Parent = vParent,
                    .NodePath = If(String.IsNullOrEmpty(vParentPath), vNode.Name, $"{vParentPath}/{vNode.Name}"),
                    .IsVisible = True,
                    .HasChildren = HasDisplayableChildren(vNode),
                    .IsExpanded = False ' Not relevant for search tree
                }
                
                ' Add to the provided list instead of pVisibleNodes
                vNodeList.Add(lVisualNode)
                
                ' Add to parent's children if there's a parent
                If vParent IsNot Nothing Then
                    vParent.Children.Add(lVisualNode)
                End If
                
                ' IMPORTANT: Always process ALL children for complete tree
                ' This is different from BuildVisualNodes which only processes if expanded
                For Each lChild In vNode.Children
                    BuildCompleteVisualTree(lChild, lVisualNode, vLevel + 1, lVisualNode.NodePath, vNodeList)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"BuildCompleteVisualTree error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets the fully qualified name for a visual node (FIXED)
        ''' </summary>
        ''' <param name="vVisualNode">The visual node to get the FQN for</param>
        ''' <returns>The fully qualified name (e.g., SimpleIDE.Widgets.ClassName.MethodName)</returns>
        Private Function GetNodeFullyQualifiedName(vVisualNode As VisualNode) As String
            Try
                If vVisualNode Is Nothing OrElse vVisualNode.Node Is Nothing Then
                    Return String.Empty
                End If
                
                Dim lParts As New List(Of String)()
                Dim lCurrentVisual As VisualNode = vVisualNode
                
                ' Walk up the visual tree to build the full name
                While lCurrentVisual IsNot Nothing
                    Dim lNode As SyntaxNode = lCurrentVisual.Node
                    
                    ' Include relevant node types in the FQN
                    Select Case lNode.NodeType
                        Case CodeNodeType.eNamespace,
                             CodeNodeType.eClass,
                             CodeNodeType.eModule,
                             CodeNodeType.eInterface,
                             CodeNodeType.eStructure,
                             CodeNodeType.eEnum
                            ' Always include these container types
                            lParts.Insert(0, lNode.Name)
                            
                        Case CodeNodeType.eMethod,
                             CodeNodeType.eFunction,
                             CodeNodeType.eProperty,
                             CodeNodeType.eField,
                             CodeNodeType.eConstant,
                             CodeNodeType.eEvent,
                             CodeNodeType.eDelegate,
                             CodeNodeType.eConstructor,
                             CodeNodeType.eOperator
                            ' Include members only if this is the target node
                            If lCurrentVisual Is vVisualNode Then
                                lParts.Insert(0, lNode.Name)
                            End If
                            
                        Case CodeNodeType.eDocument, CodeNodeType.eRegion
                            ' Skip document and region nodes
                            
                        Case Else
                            ' For other types, include if it's the target
                            If lCurrentVisual Is vVisualNode Then
                                lParts.Insert(0, lNode.Name)
                            End If
                    End Select
                    
                    lCurrentVisual = lCurrentVisual.Parent
                End While
                
                ' Join parts with dots to create FQN
                Return String.Join(".", lParts)
                
            Catch ex As Exception
                Console.WriteLine($"GetNodeFullyQualifiedName error: {ex.Message}")
                Return String.Empty
            End Try
        End Function
        
        ''' <summary>
        ''' Debug helper to print all visible nodes
        ''' </summary>
        Private Sub DebugPrintVisibleNodes()
            Try
                Console.WriteLine($"=== VISIBLE NODES ({pVisibleNodes.Count}) ===")
                For Each lNode In pVisibleNodes
                    Dim lFQN As String = GetNodeFullyQualifiedName(lNode)
                    Dim lIndent As String = New String(" "c, lNode.Level * 2)
                    Console.WriteLine($"{lIndent}{lNode.Node.Name} -> FQN: {lFQN}")
                Next
                Console.WriteLine("================================")
            Catch ex As Exception
                Console.WriteLine($"DebugPrintVisibleNodes error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Debug helper to show what's in pExpandedNodes
        ''' </summary>
        Private Sub DebugExpandedNodes()
            Try
                Console.WriteLine($"=== EXPANDED NODES ({pExpandedNodes.Count}) ===")
                For Each lPath In pExpandedNodes
                    Console.WriteLine($"  {lPath}")
                Next
                Console.WriteLine("================================")
            Catch ex As Exception
                Console.WriteLine($"DebugExpandedNodes error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Force expands specific paths and refreshes the tree
        ''' </summary>
        ''' <param name="vPaths">List of paths to expand</param>
        Private Sub ForceExpandAndRefresh(vPaths As List(Of String))
            Try
                Console.WriteLine("ForceExpandAndRefresh called")
                
                ' Add all paths to expanded nodes
                For Each lPath In vPaths
                    If Not pExpandedNodes.Contains(lPath) Then
                        pExpandedNodes.Add(lPath)
                        Console.WriteLine($"  Force expanded: {lPath}")
                    End If
                Next
                
                ' Force complete rebuild
                RebuildVisualTree()
                
                ' Force redraw
                pDrawingArea?.QueueDraw()
                
                ' Debug output
                Console.WriteLine($"After expansion: {pVisibleNodes.Count} visible nodes")
                DebugExpandedNodes()
                
            Catch ex As Exception
                Console.WriteLine($"ForceExpandAndRefresh error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
