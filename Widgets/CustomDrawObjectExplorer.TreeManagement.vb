' Widgets/CustomDrawObjectExplorer.TreeManagement.vb - Tree building and management for Object Explorer
' Created: 2025-08-16
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing tree building and management methods for the Object Explorer
    ''' </summary>
    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
        
        ' ===== Visual Tree Building =====
        

        
        ''' <summary>
        ''' Gets merged children from all partial class definitions
        ''' </summary>
        Private Function GetMergedPartialChildren(vNode As SyntaxNode) As List(Of SyntaxNode)
            Try
                Dim lMergedChildren As New List(Of SyntaxNode)
                Dim lAddedNames As New HashSet(Of String)
                
                ' Add children from primary node
                for each lChild in vNode.Children
                    lMergedChildren.Add(lChild)
                    lAddedNames.Add(lChild.Name)
                Next
                
                ' Find and merge children from other partial definitions
                ' This would require access to all partial class nodes
                ' For now, just return the primary children
                
                Return lMergedChildren
                
            Catch ex As Exception
                Console.WriteLine($"GetMergedPartialChildren error: {ex.Message}")
                Return New List(Of SyntaxNode)
            End Try
        End Function
        
        ' ===== Node Filtering =====
        

        
        ' ===== Node Position Calculation =====
        
        ''' <summary>
        ''' Calculates the position of each visible node
        ''' </summary>
        Private Sub CalculateNodePositions()
            Try
                Dim lY As Integer = 0
                Dim lMaxWidth As Integer = 0
                
                for each lNode in pVisibleNodes
                    ' Calculate X based on level
                    lNode.X = lNode.Level * pIndentWidth
                    lNode.Y = lY
                    lNode.Height = pRowHeight
                    
                    ' Calculate width (approximate based on text length)
                    Dim lTextWidth As Integer = lNode.Node.Name.Length * 8 ' Approximate char width
                    lNode.Width = lNode.X + pIconSize + 4 + lTextWidth
                    
                    If lNode.Width > lMaxWidth Then
                        lMaxWidth = lNode.Width
                    End If
                    
                    lY += pRowHeight
                Next
                
                ' Update content dimensions
                pContentWidth = lMaxWidth + 20 ' Add padding
                pContentHeight = lY
                
            Catch ex As Exception
                Console.WriteLine($"CalculateNodePositions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the display text for a node including modifiers
        ''' </summary>
        Private Function GetNodeDisplayText(vNode As SyntaxNode) As String
            Try
                Dim lText As String = vNode.Name
                
                ' Add modifiers
                If vNode.IsShared Then
                    lText = lText & " (Shared)"
                End If
                
                ' Add overload/shadow indicators for methods
                If vNode.NodeType = CodeNodeType.eMethod OrElse vNode.NodeType = CodeNodeType.eFunction Then
                    If vNode.IsOverrides Then
                        lText = lText & " (Overrides)"
                    ElseIf vNode.Attributes.ContainsKey("Shadows") Then
                        lText = lText & " (Shadows)"
                    ElseIf vNode.Attributes.ContainsKey("Overloads") Then
                        lText = lText & " (Overloads)"
                    End If
                End If
                
                Return lText
                
            Catch ex As Exception
                Console.WriteLine($"GetNodeDisplayText error: {ex.Message}")
                If vNode IsNot Nothing Then
                    Return vNode.Name
                Else
                    Return ""
                End If
            End Try
        End Function
        
        ''' <summary>
        ''' Estimates text width for layout calculations
        ''' </summary>
        Private Function EstimateTextWidth(vText As String) As Integer
            Try
                ' Rough estimate based on character count and font size
                Return CInt(vText.Length * pFontSize * 0.6)
                
            Catch ex As Exception
                Console.WriteLine($"EstimateTextWidth error: {ex.Message}")
                Return 100
            End Try
        End Function
        
        ' ===== Node Expansion/Collapse =====
        
        ''' <summary>
        ''' Toggles the expansion state of a node
        ''' </summary>
        Private Sub ToggleNodeExpansion(vNode As VisualNode)
            Try
                If vNode Is Nothing OrElse Not vNode.HasChildren Then Return
                
                vNode.IsExpanded = Not vNode.IsExpanded
                
                ' Update expanded nodes set
                If vNode.IsExpanded Then
                    pExpandedNodes.Add(vNode.NodePath)
                Else
                    pExpandedNodes.Remove(vNode.NodePath)
                End If
                
                ' Rebuild tree to show/hide children
                RebuildVisualTree()
                
                ' Redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ToggleNodeExpansion error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Expands all nodes in the tree
        ''' </summary>
        Public Sub ExpandAll()
            Try
                Console.WriteLine("ExpandAll: Expanding all nodes in the tree")
                
                ' Use the namespace-aware expansion
                ExpandAllNamespaces()
                
            Catch ex As Exception
                Console.WriteLine($"ExpandAll error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Recursively adds nodes to expanded set
        ''' </summary>
        Private Sub AddNodesToExpanded(vNode As SyntaxNode, vParentPath As String)
            Try
                If vNode Is Nothing Then Return
                
                Dim lPath As String = If(String.IsNullOrEmpty(vParentPath), vNode.Name, $"{vParentPath}/{vNode.Name}")
                
                If HasDisplayableChildren(vNode) Then
                    pExpandedNodes.Add(lPath)
                End If
                
                for each lChild in vNode.Children
                    If ShouldDisplayNode(lChild) Then
                        AddNodesToExpanded(lChild, lPath)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"AddNodesToExpanded error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Collapses all nodes in the tree
        ''' </summary>
        Public Sub CollapseAll()
            Try
                Console.WriteLine("CollapseAll: Collapsing all nodes in the tree")
                
                ' Use the namespace-aware collapse
                CollapseAllNamespaces()
                
            Catch ex As Exception
                Console.WriteLine($"CollapseAll error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Ensures a node is visible by expanding parent nodes
        ''' </summary>
        Private Sub EnsureNodeVisible(vNode As VisualNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Expand all parent nodes
                Dim lParent As VisualNode = vNode.Parent
                While lParent IsNot Nothing
                    If Not lParent.IsExpanded Then
                        pExpandedNodes.Add(lParent.NodePath)
                    End If
                    lParent = lParent.Parent
                End While
                
                ' Rebuild if needed
                If vNode.Parent IsNot Nothing Then
                    RebuildVisualTree()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"EnsureNodeVisible error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Sorting =====
        
        ''' <summary>
        ''' Applies sorting to the visual tree
        ''' </summary>
        Private Sub ApplySorting()
            Try
                Select Case pSortMode
                    Case ObjectExplorerSortMode.eDefault
                        ' For default, use alphabetic with namespaces first
                        SortAlphabetically()
                    Case ObjectExplorerSortMode.eAlphabetic
                        SortAlphabetically()
                    Case ObjectExplorerSortMode.eByType
                        SortByType()
                    Case ObjectExplorerSortMode.eByVisibility
                        SortByVisibility()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ApplySorting error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sorts nodes alphabetically by name
        ''' </summary>
        Private Sub SortAlphabetically()
            Try
                ' Group nodes by level and parent
                Dim lNodeGroups As New Dictionary(Of String, List(Of VisualNode))
                
                for each lNode in pVisibleNodes
                    Dim lKey As String = If(lNode.Parent?.NodePath, "root")
                    If Not lNodeGroups.ContainsKey(lKey) Then
                        lNodeGroups(lKey) = New List(Of VisualNode)
                    End If
                    lNodeGroups(lKey).Add(lNode)
                Next
                
                ' Sort each group - namespaces first, then alphabetically
                for each lGroup in lNodeGroups
                    lGroup.Value.Sort(Function(a, b)
                        ' Namespaces always come first
                        Dim aIsNamespace As Boolean = (a.Node.NodeType = CodeNodeType.eNamespace)
                        Dim bIsNamespace As Boolean = (b.Node.NodeType = CodeNodeType.eNamespace)
                        
                        If aIsNamespace AndAlso Not bIsNamespace Then
                            Return -1 ' a (namespace) comes before b
                        ElseIf Not aIsNamespace AndAlso bIsNamespace Then
                            Return 1 ' b (namespace) comes before a
                        Else
                            ' Both same type (both namespaces or both not), sort alphabetically
                            Return String.Compare(a.Node.Name, b.Node.Name, StringComparison.OrdinalIgnoreCase)
                        End If
                    End Function)
                Next
                
                ' Rebuild sorted list
Console.WriteLine($"SortAlphabetically  pVisibleNodesClear()")

                pVisibleNodes.Clear()
                RebuildSortedList(lNodeGroups, Nothing, "root")
                
            Catch ex As Exception
                Console.WriteLine($"SortAlphabetically error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sorts nodes by type (methods, properties, fields, etc.)
        ''' </summary>
        Private Sub SortByType()
            Try
                ' Define sort order for node types
                Dim lTypeOrder As New Dictionary(Of CodeNodeType, Integer) From {
                    {CodeNodeType.eNamespace, 0},
                    {CodeNodeType.eClass, 1},
                    {CodeNodeType.eModule, 2},
                    {CodeNodeType.eInterface, 3},
                    {CodeNodeType.eStructure, 4},
                    {CodeNodeType.eEnum, 5},
                    {CodeNodeType.eDelegate, 6},
                    {CodeNodeType.eConstructor, 10},
                    {CodeNodeType.eProperty, 20},
                    {CodeNodeType.eMethod, 30},
                    {CodeNodeType.eFunction, 31},
                    {CodeNodeType.eEvent, 40},
                    {CodeNodeType.eField, 50},
                    {CodeNodeType.eConst, 51},
                    {CodeNodeType.eOperator, 60}
                }
                
                ' Sort visible nodes
                pVisibleNodes.Sort(Function(a, b)
                    ' Keep hierarchy
                    If a.Level <> b.Level Then Return a.Level.CompareTo(b.Level)
                    
                    ' Sort by type order
                    Dim lOrderA As Integer = If(lTypeOrder.ContainsKey(a.Node.NodeType), lTypeOrder(a.Node.NodeType), 99)
                    Dim lOrderB As Integer = If(lTypeOrder.ContainsKey(b.Node.NodeType), lTypeOrder(b.Node.NodeType), 99)
                    
                    If lOrderA <> lOrderB Then Return lOrderA.CompareTo(lOrderB)
                    
                    ' Then by name
                    Return String.Compare(a.Node.Name, b.Node.Name, StringComparison.OrdinalIgnoreCase)
                End Function)
                
            Catch ex As Exception
                Console.WriteLine($"SortByType error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sorts nodes by visibility (public, protected, private)
        ''' </summary>
        Private Sub SortByVisibility()
            Try
                pVisibleNodes.Sort(Function(a, b)
                    ' Keep hierarchy
                    If a.Level <> b.Level Then Return a.Level.CompareTo(b.Level)
                    
                    ' Sort by visibility
                    Dim lVisA As Integer = GetVisibilityOrder(a.Node)
                    Dim lVisB As Integer = GetVisibilityOrder(b.Node)
                    
                    If lVisA <> lVisB Then Return lVisA.CompareTo(lVisB)
                    
                    ' Then by name
                    Return String.Compare(a.Node.Name, b.Node.Name, StringComparison.OrdinalIgnoreCase)
                End Function)
                
            Catch ex As Exception
                Console.WriteLine($"SortByVisibility error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets visibility sort order for a node
        ''' </summary>
        Private Function GetVisibilityOrder(vNode As SyntaxNode) As Integer
            Try
                If vNode.IsPublic Then Return 0
                If vNode.IsFriend Then Return 1
                If vNode.IsProtected Then Return 2
                Return 3 ' Private
                
            Catch ex As Exception
                Console.WriteLine($"GetVisibilityOrder error: {ex.Message}")
                Return 99
            End Try
        End Function
        
        ''' <summary>
        ''' Rebuilds sorted list maintaining hierarchy
        ''' </summary>
        Private Sub RebuildSortedList(vGroups As Dictionary(Of String, List(Of VisualNode)), 
                                      vParent As VisualNode, 
                                      vKey As String)
            Try
                If Not vGroups.ContainsKey(vKey) Then Return
                
                for each lNode in vGroups(vKey)
                    pVisibleNodes.Add(lNode)
                    
                    If lNode.IsExpanded AndAlso lNode.HasChildren Then
                        RebuildSortedList(vGroups, lNode, lNode.NodePath)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"RebuildSortedList error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Node Finding =====
        
        ''' <summary>
        ''' Finds a visual node corresponding to a syntax node
        ''' </summary>
        Private Function FindVisualNode(vSyntaxNode As SyntaxNode) As VisualNode
            Try
                If vSyntaxNode Is Nothing Then Return Nothing
                
                for each lVisualNode in pVisibleNodes
                    If lVisualNode.Node Is vSyntaxNode Then
                        Return lVisualNode
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindVisualNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.ShouldDisplayNode
        ''' <summary>
        ''' Determines if a node should be displayed based on current filter settings
        ''' </summary>
        ''' <param name="vNode">The syntax node to check</param>
        ''' <returns>True if the node should be displayed, False otherwise</returns>
        Private Function ShouldDisplayNode(vNode As SyntaxNode) As Boolean
            Try
                If vNode Is Nothing Then Return False
                
                ' Check node type and apply filtering rules
                Select Case vNode.NodeType
                    ' Always show namespaces
                    Case CodeNodeType.eNamespace
                        Return True
                        
                    ' FIXED: Also show document nodes if they're the root
                    ' The root namespace is sometimes marked as eDocument type
                    Case CodeNodeType.eDocument
                        ' Show if it's the root node or has children
                        Return vNode Is pRootNode OrElse vNode.Children.Count > 0
                        
                    ' Always show type declarations
                    Case CodeNodeType.eClass, CodeNodeType.eModule,
                         CodeNodeType.eInterface, CodeNodeType.eStructure
                        Return True
                        
                    Case CodeNodeType.eEnum
                        Return True
                        
                    Case CodeNodeType.eEnumValue
                        Return True ' Always show enum values
                        
                    Case CodeNodeType.eDelegate
                        Return True
                        
                    ' Regions based on settings
                    Case CodeNodeType.eRegion
                        Return pShowRegions
                        
                    ' Methods and functions - check visibility or ShowPrivateMembers
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction, 
                         CodeNodeType.eConstructor, CodeNodeType.eOperator
                        ' FIXED: If ShowPrivateMembers is true, show all methods
                        ' Otherwise check visibility flags
                        If pShowPrivateMembers Then
                            Return True
                        Else
                            Return vNode.IsPublic OrElse vNode.IsFriend OrElse vNode.IsProtected
                        End If
                        
                    ' Properties - check visibility or ShowPrivateMembers
                    Case CodeNodeType.eProperty
                        ' FIXED: If ShowPrivateMembers is true, show all properties
                        If pShowPrivateMembers Then
                            Return True
                        Else
                            Return vNode.IsPublic OrElse vNode.IsFriend OrElse vNode.IsProtected
                        End If
                        
                    ' Events - check visibility or ShowPrivateMembers
                    Case CodeNodeType.eEvent
                        ' FIXED: If ShowPrivateMembers is true, show all events
                        If pShowPrivateMembers Then
                            Return True
                        Else
                            Return vNode.IsPublic OrElse vNode.IsFriend OrElse vNode.IsProtected
                        End If
                        
                    ' Fields and constants - check visibility or ShowPrivateMembers
                    Case CodeNodeType.eField, CodeNodeType.eConst
                        ' FIXED: If ShowPrivateMembers is true, show all fields
                        If pShowPrivateMembers Then
                            Return True
                        Else
                            Return vNode.IsPublic OrElse vNode.IsFriend OrElse vNode.IsProtected
                        End If
                        
                    ' Don't show local variables, parameters, or imports
                    Case CodeNodeType.eVariable, CodeNodeType.eParameter, CodeNodeType.eImport
                        Return False
                        
                    ' Default case - don't show unknown types
                    Case Else
                        Console.WriteLine($"ShouldDisplayNode: Unknown node type {vNode.NodeType} for node '{vNode.Name}'")
                        Return False
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ShouldDisplayNode error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Rebuilds the visual tree from the syntax nodes with proper position calculation
        ''' </summary>
        Public Sub RebuildVisualTree()
            Try
                ' Prevent recursive rebuilds
                If pIsRebuildingTree Then
                    'Console.WriteLine("RebuildVisualTree: Already rebuilding, skipping")
                    Return
                End If
                
                ' Check if rebuild is actually needed
                If Not IsRebuildNeeded() Then
                    'Console.WriteLine($"RebuildVisualTree: Skipped - no changes detected")
                    Return
                End If
                
                pIsRebuildingTree = True
                'Console.WriteLine($"RebuildVisualTree: Starting rebuild, current count = {pVisibleNodes.Count}")
                
                ' Store the path of the currently selected node before clearing
                Dim lSelectedPath As String = Nothing
                If pSelectedNode IsNot Nothing Then
                    lSelectedPath = pSelectedNode.NodePath
                End If
                
                ' Clear the visual nodes
                pVisibleNodes.Clear()
                pNodeCache.Clear()
                
                ' Reset selection reference (will be restored later)
                pSelectedNode = Nothing
                
                If pRootNode Is Nothing Then
                   ' Console.WriteLine("RebuildVisualTree: No root node")
                    UpdateScrollbars()
                    pIsRebuildingTree = False
                    Return
                End If
                
                'Console.WriteLine($"RebuildVisualTree: Root '{pRootNode.Name}' ({pRootNode.NodeType})")
                
                ' Build visible nodes recursively with proper parent path (empty string for root)
                BuildVisualNodes(pRootNode, Nothing, 0, "")
                
                ' CRITICAL FIX: Calculate node positions after building the tree structure
                ' This sets the Y positions for all nodes
                CalculateNodePositions()
                
                ' Content dimensions are now set by CalculateNodePositions
                ' pContentHeight and pContentWidth are updated there
                
                ' Restore selection if we had one
                If Not String.IsNullOrEmpty(lSelectedPath) Then
                    for each lNode in pVisibleNodes
                        If lNode.NodePath = lSelectedPath Then
                            pSelectedNode = lNode
                           ' Console.WriteLine($"RebuildVisualTree: Restored selection to {lNode.Node.Name}")
                            Exit for
                        End If
                    Next
                    
                    If pSelectedNode Is Nothing Then
                        'Console.WriteLine($"RebuildVisualTree: Could not restore selection for path '{lSelectedPath}'")
                    End If
                End If
                
                ' Update tracking
                pLastRebuildRoot = pRootNode
                pLastRebuildHash = GetTreeHash()
                pNeedsRebuild = False
                
                ' Update scrollbars to reflect new content dimensions
                UpdateScrollbars()
                
               ' Console.WriteLine($"RebuildVisualTree: Built {pVisibleNodes.Count} visible nodes, height={pContentHeight}")
                
            Catch ex As Exception
                Console.WriteLine($"RebuildVisualTree error: {ex.Message}")
            Finally
                pIsRebuildingTree = False
            End Try
        End Sub

        Private Function IsRebuildNeeded() As Boolean
            Try
                ' Force rebuild if explicitly marked
                If pNeedsRebuild Then
                    Return True
                End If
                
                ' Rebuild if root changed
                If pRootNode IsNot pLastRebuildRoot Then
                    Return True
                End If
                
                ' Rebuild if no visible nodes but we have a root
                If pVisibleNodes.Count = 0 AndAlso pRootNode IsNot Nothing Then
                    Return True
                End If
                
                ' Check if tree structure changed (using simple hash)
                Dim lCurrentHash As Integer = GetTreeHash()
                If lCurrentHash <> pLastRebuildHash Then
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsRebuildNeeded error: {ex.Message}")
                Return True  ' Rebuild on error to be safe
            End Try
        End Function
        
        Private Function GetTreeHash() As Integer
            Try
                If pRootNode Is Nothing Then Return 0
                
                ' Simple hash based on expanded nodes and root structure
                Dim lHash As Integer = pRootNode.GetHashCode()
                lHash = lHash Xor (pExpandedNodes.Count << 16)
                
                for each lPath in pExpandedNodes
                    lHash = lHash Xor lPath.GetHashCode()
                Next
                
                Return lHash
                
            Catch ex As Exception
                Return 0
            End Try
        End Function

        ''' <summary>
        ''' Ensure visible nodes list is never Nothing
        ''' </summary>
        Private Sub EnsureVisibleNodesList()
            If pVisibleNodes Is Nothing Then
                pVisibleNodes = New List(Of VisualNode)()
                Console.WriteLine("Created new pVisibleNodes list")
            End If
        End Sub
        
        ''' <summary>
        ''' Builds visual nodes recursively from syntax nodes with proper child detection
        ''' </summary>
        ''' <param name="vNode">The syntax node to process</param>
        ''' <param name="vParent">The parent visual node</param>
        ''' <param name="vLevel">The indentation level</param>
        ''' <param name="vParentPath">The path of the parent node</param>
        ''' <remarks>
        ''' Fixed to ensure all nodes are processed and HasChildren is set correctly
        ''' </remarks>
        Private Sub BuildVisualNodes(vNode As SyntaxNode, vParent As VisualNode, vLevel As Integer, vParentPath As String)
            Try
                If vNode Is Nothing Then Return
                
                ' Special handling for root document nodes
                If vNode Is pRootNode AndAlso vNode.NodeType = CodeNodeType.eDocument Then
                    'Console.WriteLine($"BuildVisualNodes: Root is eDocument with {vNode.Children.Count} children")
                    
                    ' Sort children alphabetically before processing
                    Dim lSortedChildren As List(Of SyntaxNode) = vNode.Children.OrderBy(
                        Function(c) c.Name, StringComparer.OrdinalIgnoreCase).ToList()
                    
                    ' Process all children of the document root
                    for each lChild in lSortedChildren
                        BuildVisualNodes(lChild, Nothing, 0, "")
                    Next
                    Return
                End If
                
                ' Check if this node should be displayed
                If Not ShouldDisplayNode(vNode) Then 
                   ' Console.WriteLine($"BuildVisualNodes: Skipping {vNode.Name} ({vNode.NodeType}) - filtered out")
                    Return
                End If
                
                ' Create the visual node
                Dim lVisualNode As New VisualNode()
                lVisualNode.Node = vNode
                lVisualNode.Parent = vParent
                lVisualNode.Level = vLevel
                lVisualNode.NodePath = If(String.IsNullOrEmpty(vParentPath), 
                                          vNode.Name,
                                          vParentPath & "." & vNode.Name)
                lVisualNode.IsVisible = True
                
                ' CRITICAL: Set HasChildren based on whether there are any children at all
                ' This ensures expand/collapse buttons appear
                lVisualNode.HasChildren = HasDisplayableChildren(vNode)
                
                ' Check if this node is expanded
                lVisualNode.IsExpanded = pExpandedNodes.Contains(lVisualNode.NodePath)
                
                ' Add to visible nodes list
                pVisibleNodes.Add(lVisualNode)
                
                ' Add to cache for quick lookup
                If Not String.IsNullOrEmpty(lVisualNode.NodePath) Then
                    pNodeCache(lVisualNode.NodePath) = lVisualNode
                End If
                
                ' Add to parent's children collection if there's a parent
                If vParent IsNot Nothing Then
                    vParent.Children.Add(lVisualNode)
                End If
                
                ' Log node creation for debugging
                If vLevel <= 1 Then  ' Only log top-level nodes to reduce noise
                    Console.WriteLine($"  Created node: {vNode.Name} (Level={vLevel}, HasChildren={lVisualNode.HasChildren}, IsExpanded={lVisualNode.IsExpanded})")
                End If
                
                ' Process children if this node is expanded AND has children
                If lVisualNode.IsExpanded AndAlso vNode.Children.Count > 0 Then
                    Console.WriteLine($"  Expanding {vNode.Name} with {vNode.Children.Count} children")
                    
                    ' Sort children alphabetically for consistent display
                    Dim lSortedChildren As List(Of SyntaxNode) = vNode.Children.OrderBy(
                        Function(c) c.Name, StringComparer.OrdinalIgnoreCase).ToList()
                    
                    ' Recursively process each child
                    for each lChild in lSortedChildren
                        BuildVisualNodes(lChild, lVisualNode, vLevel + 1, lVisualNode.NodePath)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildVisualNodes error: {ex.Message}")
                Console.WriteLine($"  Node: {vNode?.Name}, Level: {vLevel}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.HasDisplayableChildren
        ''' <summary>
        ''' Checks if a node has any displayable children (direct or indirect)
        ''' </summary>
        ''' <param name="vNode">The node to check</param>
        ''' <returns>True if the node has at least one displayable child or descendant</returns>
        ''' <remarks>
        ''' This method now checks both direct children and descendants recursively
        ''' to ensure expand/collapse buttons appear for all container nodes
        ''' </remarks>
        Private Function HasDisplayableChildren(vNode As SyntaxNode) As Boolean
            Try
                If vNode Is Nothing OrElse vNode.Children.Count = 0 Then 
                    Return False
                End If
                
                ' For container types (namespaces, classes, etc.), always show expand/collapse
                ' if they have ANY children, even if those children might be filtered out
                Select Case vNode.NodeType
                    Case CodeNodeType.eNamespace, 
                         CodeNodeType.eClass, 
                         CodeNodeType.eModule,
                         CodeNodeType.eInterface, 
                         CodeNodeType.eStructure,
                         CodeNodeType.eEnum
                        ' Container types should always show expand/collapse if they have children
                        ' This allows users to expand them to see if there's anything inside
                        Return vNode.Children.Count > 0
                        
                    Case CodeNodeType.eDocument
                        ' Document nodes should show children if they have any
                        Return vNode.Children.Count > 0
                        
                    Case Else
                        ' For other node types, check if any child should be displayed
                        for each lChild in vNode.Children
                            If ShouldDisplayNode(lChild) Then 
                                Return True
                            End If
                            
                            ' Also recursively check if this child has displayable descendants
                            ' This ensures we show expand buttons for nodes that have nested content
                            If HasDisplayableChildren(lChild) Then 
                                Return True
                            End If
                        Next
                        
                        Return False
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"HasDisplayableChildren error: {ex.Message}")
                Return False
            End Try
        End Function
        
        
    
        ''' <summary>
        ''' Applies the current theme to the drawing area
        ''' </summary>
        Public Sub ApplyTheme() 
            Try
                If pDrawingArea Is Nothing Then Return
                
                ' IMPORTANT: Store current state before applying theme
                Dim lSavedRoot As SyntaxNode = pRootNode
                Dim lSavedVisibleNodes As List(Of VisualNode) = pVisibleNodes
                Dim lSavedLastValid As SyntaxNode = pLastValidRootNode
                
                ' Get current theme from settings
                Dim lThemeName As String = ""
                If pSettingsManager IsNot Nothing Then
                    lThemeName = pSettingsManager.GetString("CurrentTheme", "Default Dark")
                End If
                
                Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                
                ' Apply CSS to drawing area for background
                Dim lCss As String
                If lIsDark Then
                    lCss = "drawingarea { background-color: #1E1E1E; }"
                Else
                    lCss = "drawingarea { background-color: #FFFFFF; }"
                End If
                
                CssHelper.ApplyCssToWidget(pDrawingArea, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                
                ' IMPORTANT: Ensure state is preserved after CSS application
                If pRootNode Is Nothing AndAlso lSavedRoot IsNot Nothing Then
                    Console.WriteLine("ApplyTheme: Restoring root after CSS application")
                    pRootNode = lSavedRoot
                End If
                
                If pVisibleNodes Is Nothing OrElse pVisibleNodes.Count = 0 Then
                    If lSavedVisibleNodes IsNot Nothing AndAlso lSavedVisibleNodes.Count > 0 Then
                        Console.WriteLine("ApplyTheme: Restoring visible nodes after CSS application")
                        pVisibleNodes = lSavedVisibleNodes
                    End If
                End If
                
                If pLastValidRootNode Is Nothing AndAlso lSavedLastValid IsNot Nothing Then
                    pLastValidRootNode = lSavedLastValid
                End If
                
                ' Force redraw with the preserved state
                pDrawingArea.QueueDraw()
                
                Console.WriteLine($"CustomDrawObjectExplorer.ApplyTheme: Applied theme to Object Explorer: {lThemeName}")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initializes the widget after it's added to parent
        ''' </summary>
        Public Sub Initialize()
            Try
                ' Apply theme on initialization
                ApplyTheme()
                
                ' Ensure drawing area is realized
                If pDrawingArea IsNot Nothing AndAlso Not pDrawingArea.IsRealized Then
                    pDrawingArea.Realize()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Initialize error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handle parse completion from centralized ProjectManager.Parser
        ''' </summary>
        ''' <param name="vFile">The file that was parsed</param>
        ''' <param name="vResult">The parse result as SyntaxNode</param>
        ''' <remarks>
        ''' This handler receives parse results from ProjectManager's centralized
        ''' ProjectParser instead of performing local parsing
        ''' </remarks>
        Private Sub OnProjectParseCompleted(vFile As SourceFileInfo, vResult As SyntaxNode)
            Try
                ' For single file updates, we might want to update just that file's node
                ' For now, if we get a full project tree, update the whole structure
                If vResult IsNot Nothing AndAlso vResult.NodeType = CodeNodeType.eProject Then
                    Console.WriteLine($"ObjectExplorer received project parse from ProjectParser")
                    UpdateStructure(vResult)
                ElseIf vFile IsNot Nothing Then
                    ' Single file update - could update just that file's nodes
                    Console.WriteLine($"ObjectExplorer received file parse for {vFile.FileName}")
                    ' For now, request full project structure update
                    If pProjectManager IsNot Nothing Then
                        Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                        If lProjectTree IsNot Nothing Then
                            UpdateStructure(lProjectTree)
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectParseCompleted error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle project structure load from ProjectManager
        ''' </summary>
        ''' <param name="vProjectPath">Path to the loaded project</param>
        ''' <param name="vRootNode">Root node of the project structure from ProjectParser</param>
        Private Sub OnProjectStructureLoaded(vProjectPath As String, vRootNode As SyntaxNode)
            Try
                Console.WriteLine($"ObjectExplorer received project structure from ProjectParser: {vProjectPath}")
                
                If vRootNode IsNot Nothing Then
                    ' Update the display with the ProjectParser's structure
                    UpdateStructure(vRootNode)
                Else
                    Console.WriteLine("ObjectExplorer: No root node in project structure")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectStructureLoaded error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handle project structure load from ProjectManager
        ''' </summary>
        ''' <param name="vRootNode">Root node of the project structure from ProjectParser</param>
        ''' <remarks>
        ''' The signature matches ProjectManager.ProjectStructureLoaded event which only takes SyntaxNode
        ''' </remarks>
        Private Sub OnProjectStructureLoaded(vRootNode As SyntaxNode)
            Try
                Console.WriteLine($"ObjectExplorer received project structure from ProjectParser")
                
                If vRootNode IsNot Nothing Then
                    ' Update the display with the ProjectParser's structure
                    UpdateStructure(vRootNode)
                Else
                    Console.WriteLine("ObjectExplorer: No root node in project structure")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectStructureLoaded error: {ex.Message}")
            End Try
        End Sub

        ' Add: SimpleIDE.Widgets.CustomDrawObjectExplorer.ExpandAllNamespaces
        ' To: CustomDrawObjectExplorer.TreeManagement.vb
        ''' <summary>
        ''' Expands all namespace nodes in the tree for better visibility
        ''' </summary>
        ''' <remarks>
        ''' This is useful for getting an overview of the entire project structure
        ''' </remarks>
        Public Sub ExpandAllNamespaces()
            Try
                Console.WriteLine("ExpandAllNamespaces: Starting...")
                
                If pRootNode Is Nothing Then
                    Console.WriteLine("ExpandAllNamespaces: No root node")
                    Return
                End If
                
                ' Store current expanded count for comparison
                Dim lInitialCount As Integer = pExpandedNodes.Count
                
                ' Recursively add all namespace nodes to expanded set
                AddNamespaceNodesToExpanded(pRootNode, "")
                
                Dim lAddedCount As Integer = pExpandedNodes.Count - lInitialCount
                Console.WriteLine($"ExpandAllNamespaces: Added {lAddedCount} namespace paths")
                
                ' Only rebuild if we actually expanded something
                If lAddedCount > 0 Then
                    ' Rebuild the visual tree
                    RebuildVisualTree()
                    
                    ' Force redraw
                    pDrawingArea?.QueueDraw()
                End If
                
                Console.WriteLine($"ExpandAllNamespaces: Complete. {pVisibleNodes.Count} nodes now visible")
                
            Catch ex As Exception
                Console.WriteLine($"ExpandAllNamespaces error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Collapses all namespace nodes in the tree
        ''' </summary>
        ''' <remarks>
        ''' Useful for reducing clutter and focusing on specific areas
        ''' </remarks>
        Public Sub CollapseAllNamespaces()
            Try
                Console.WriteLine("CollapseAllNamespaces: Starting...")
                
                If pRootNode Is Nothing Then
                    Console.WriteLine("CollapseAllNamespaces: No root node")
                    Return
                End If
                
                ' Clear all expanded nodes
                Dim lInitialCount As Integer = pExpandedNodes.Count
                pExpandedNodes.Clear()
                
                Console.WriteLine($"CollapseAllNamespaces: Cleared {lInitialCount} expanded paths")
                
                ' Rebuild the visual tree
                RebuildVisualTree()
                
                ' Force redraw
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"CollapseAllNamespaces: Complete. {pVisibleNodes.Count} nodes now visible")
                
            Catch ex As Exception
                Console.WriteLine($"CollapseAllNamespaces error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Collapses all nodes then expands only the root namespace
        ''' </summary>
        ''' <remarks>
        ''' Provides a clean initial view showing just the top-level structure
        ''' with expand/collapse buttons visible for all containers
        ''' </remarks>
        Public Sub ExpandRootOnly()
            Try
                Console.WriteLine("ExpandRootOnly: Starting...")
                
                If pRootNode Is Nothing Then
                    Console.WriteLine("ExpandRootOnly: No root node")
                    Return
                End If
                
                ' First, clear all expanded nodes
                pExpandedNodes.Clear()
                Console.WriteLine("ExpandRootOnly: Collapsed all nodes")
                
                ' Now determine what to expand
                Dim lPathsToExpand As New List(Of String)()
                
                If pRootNode.NodeType = CodeNodeType.eDocument Then
                    ' Root is a document - expand its namespace children
                    for each lChild in pRootNode.Children
                        If lChild.NodeType = CodeNodeType.eNamespace Then
                            lPathsToExpand.Add(lChild.Name)
                            Console.WriteLine($"  Will expand namespace: {lChild.Name}")
                            
                            ' Only expand the first namespace if there are multiple
                            Exit for
                        End If
                    Next
                ElseIf pRootNode.NodeType = CodeNodeType.eNamespace Then
                    ' Root is a namespace - expand it
                    lPathsToExpand.Add(pRootNode.Name)
                    Console.WriteLine($"  Will expand root namespace: {pRootNode.Name}")
                End If
                
                ' Add the paths to the expanded set
                for each lPath in lPathsToExpand
                    pExpandedNodes.Add(lPath)
                Next
                
                Console.WriteLine($"ExpandRootOnly: Expanded {lPathsToExpand.Count} root node(s)")
                
                ' Rebuild the visual tree with the new expansion state
                RebuildVisualTree()
                
                ' Force redraw
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"ExpandRootOnly: Complete. {pVisibleNodes.Count} nodes visible")
                
            Catch ex As Exception
                Console.WriteLine($"ExpandRootOnly error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates the visible nodes list based on current expansion state
        ''' </summary>
        ''' <remarks>
        ''' This helper method rebuilds just the visible nodes list without
        ''' recreating the entire visual tree, preserving object references
        ''' </remarks>
        Private Sub UpdateVisibleNodes()
            Try
                pVisibleNodes.Clear()
                
                ' Start from root's children if root is a document node
                If pRootNode IsNot Nothing Then
                    If pRootNode.NodeType = CodeNodeType.eDocument Then
                        ' Add all root children
                        for each lChild in pRootNode.Children
                            Dim lPath As String = lChild.Name
                            Dim lVisualNode As VisualNode = GetOrCreateVisualNode(lChild, Nothing, 0, "")
                            If lVisualNode IsNot Nothing Then
                                pVisibleNodes.Add(lVisualNode)
                                
                                ' If expanded, add its children
                                If pExpandedNodes.Contains(lPath) Then
                                    AddVisibleChildren(lVisualNode, lPath)
                                End If
                            End If
                        Next
                    Else
                        ' Single root node case
                        Dim lVisualNode As VisualNode = GetOrCreateVisualNode(pRootNode, Nothing, 0, "")
                        If lVisualNode IsNot Nothing Then
                            pVisibleNodes.Add(lVisualNode)
                            
                            If pExpandedNodes.Contains(pRootNode.Name) Then
                                AddVisibleChildren(lVisualNode, pRootNode.Name)
                            End If
                        End If
                    End If
                End If
                
                ' Update node positions
                CalculateNodePositions()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateVisibleNodes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively adds visible children of an expanded node
        ''' </summary>
        ''' <param name="vParentVisual">Parent visual node</param>
        ''' <param name="vParentPath">Path of the parent for building child paths</param>
        Private Sub AddVisibleChildren(vParentVisual As VisualNode, vParentPath As String)
            Try
                If vParentVisual?.Node Is Nothing Then Return
                
                for each lChildSyntax in vParentVisual.Node.Children
                    If ShouldDisplayNode(lChildSyntax) Then
                        Dim lChildPath As String = vParentPath & "." & lChildSyntax.Name
                        Dim lChildVisual As VisualNode = GetOrCreateVisualNode(
                            lChildSyntax, 
                            vParentVisual, 
                            vParentVisual.Level + 1, 
                            vParentPath)
                            
                        If lChildVisual IsNot Nothing Then
                            pVisibleNodes.Add(lChildVisual)
                            
                            ' If this child is also expanded, add its children
                            If pExpandedNodes.Contains(lChildPath) AndAlso lChildSyntax.Children.Count > 0 Then
                                AddVisibleChildren(lChildVisual, lChildPath)
                            End If
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"AddVisibleChildren error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets an existing visual node from cache or creates a new one
        ''' </summary>
        Private Function GetOrCreateVisualNode(vSyntaxNode As SyntaxNode, 
                                              vParent As VisualNode, 
                                              vLevel As Integer, 
                                              vParentPath As String) As VisualNode
            Try
                Dim lPath As String = If(String.IsNullOrEmpty(vParentPath), 
                                         vSyntaxNode.Name, 
                                         vParentPath & "." & vSyntaxNode.Name)
                
                ' Check cache first
                If pNodeCache.ContainsKey(lPath) Then
                    Return pNodeCache(lPath)
                End If
                
                ' Create new visual node
                Dim lVisualNode As New VisualNode()
                lVisualNode.Node = vSyntaxNode
                lVisualNode.Parent = vParent
                lVisualNode.Level = vLevel
                lVisualNode.NodePath = lPath
                lVisualNode.HasChildren = vSyntaxNode.Children.Count > 0 AndAlso
                                         vSyntaxNode.Children.Any(AddressOf ShouldDisplayNode)
                lVisualNode.IsExpanded = pExpandedNodes.Contains(lPath)
                
                ' Add to cache
                pNodeCache(lPath) = lVisualNode
                
                Return lVisualNode
                
            Catch ex As Exception
                Console.WriteLine($"GetOrCreateVisualNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
    End Class
    
End Namespace