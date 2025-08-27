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
                ' Add all nodes with children to expanded set
                AddNodesToExpanded(pRootNode, "")
                
                ' Rebuild tree
                RebuildVisualTree()
                
                ' Redraw
                pDrawingArea?.QueueDraw()
                
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
                pExpandedNodes.Clear()
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
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
                
                ' Sort each group
                for each lGroup in lNodeGroups
                    lGroup.Value.Sort(Function(a, b) String.Compare(a.Node.Name, b.Node.Name, StringComparison.OrdinalIgnoreCase))
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
                    {CodeNodeType.eConstant, 51},
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
                        
                    ' Methods and functions - always show
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction, 
                         CodeNodeType.eConstructor, CodeNodeType.eOperator
                        Return True
                        
                    ' Properties - check visibility settings
                    Case CodeNodeType.eProperty
                        Return vNode.IsPublic OrElse vNode.IsFriend OrElse 
                               vNode.IsProtected OrElse pShowPrivateMembers
                        
                    ' Events - check visibility settings
                    Case CodeNodeType.eEvent
                        Return vNode.IsPublic OrElse vNode.IsFriend OrElse 
                               vNode.IsProtected OrElse pShowPrivateMembers
                        
                    ' Fields and constants - check visibility settings
                    Case CodeNodeType.eField, CodeNodeType.eConstant, CodeNodeType.eConst
                        Return vNode.IsPublic OrElse vNode.IsFriend OrElse 
                               vNode.IsProtected OrElse pShowPrivateMembers
                        
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
        ''' Simplified RebuildVisualTree that doesn't swap list instances
        ''' </summary>
        Public Sub RebuildVisualTree()
            Try
                Console.WriteLine($"RebuildVisualTree: Starting rebuild, current count = {pVisibleNodes.Count}")
                
                ' Ensure we never have Nothing list
                If pVisibleNodes Is Nothing Then
                    pVisibleNodes = New List(Of VisualNode)()
                End If
                
                ' Clear existing nodes
                pVisibleNodes.Clear()
                pNodeCache.Clear()
                
                If pRootNode Is Nothing Then 
                    Console.WriteLine("RebuildVisualTree: No root node")
                    pDrawingArea?.QueueDraw()
                    Return
                End If
                
                Console.WriteLine($"RebuildVisualTree: Root '{pRootNode.Name}' ({pRootNode.NodeType})")
                
                ' Auto-expand namespace children if root is a document
                If pRootNode.NodeType = CodeNodeType.eDocument Then
                    for each lChild in pRootNode.Children
                        If lChild.NodeType = CodeNodeType.eNamespace Then
                            If Not pExpandedNodes.Contains(lChild.Name) Then
                                pExpandedNodes.Add(lChild.Name)
                                Console.WriteLine($"Auto-expanded namespace: {lChild.Name}")
                            End If
                        End If
                    Next
                ElseIf pRootNode.NodeType = CodeNodeType.eNamespace Then
                    If Not pExpandedNodes.Contains(pRootNode.Name) Then
                        pExpandedNodes.Add(pRootNode.Name)
                        Console.WriteLine($"Auto-expanded root namespace: {pRootNode.Name}")
                    End If
                End If
                
                ' Build visual nodes directly into pVisibleNodes
                BuildVisualNodes(pRootNode, Nothing, 0, "")
                
                ' Apply sorting if needed
                If pSortMode <> ObjectExplorerSortMode.eDefault Then
                    ApplySorting()
                End If
                
                ' Calculate positions
                CalculateNodePositions()
                
                ' Update scroll ranges
                UpdateScrollbars()
                
                Console.WriteLine($"RebuildVisualTree: Built {pVisibleNodes.Count} visible nodes")
                
                ' Force immediate redraw if we have nodes
                If pVisibleNodes.Count > 0 Then
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RebuildVisualTree error: {ex.Message}")
                ' Ensure pVisibleNodes is never left as Nothing
                If pVisibleNodes Is Nothing Then
                    pVisibleNodes = New List(Of VisualNode)()
                End If
            End Try
        End Sub

        ''' <summary>
        ''' Ensure visible nodes list is never Nothing
        ''' </summary>
        Private Sub EnsureVisibleNodesList()
            If pVisibleNodes Is Nothing Then
                pVisibleNodes = New List(Of VisualNode)()
                Console.WriteLine("Created new pVisibleNodes list")
            End If
        End Sub
        
        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.BuildVisualNodes
        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.BuildVisualNodes
        ''' <summary>
        ''' Builds visual nodes recursively from syntax nodes with alphabetical sorting
        ''' </summary>
        ''' <param name="vNode">The syntax node to process</param>
        ''' <param name="vParent">The parent visual node</param>
        ''' <param name="vLevel">The indentation level</param>
        ''' <param name="vParentPath">The path of the parent node</param>
        Private Sub BuildVisualNodes(vNode As SyntaxNode, vParent As VisualNode, vLevel As Integer, vParentPath As String)
            Try
                If vNode Is Nothing Then Return
                
                ' FIXED: Special handling for root document nodes
                If vNode Is pRootNode AndAlso vNode.NodeType = CodeNodeType.eDocument Then
                    Console.WriteLine($"Root is eDocument type with {vNode.Children.Count} children")
                    
                    ' Sort children alphabetically before processing
                    Dim lSortedChildren As List(Of SyntaxNode) = vNode.Children.OrderBy(
                        Function(c) c.Name, StringComparer.OrdinalIgnoreCase).ToList()
                    
                    for each lChild in lSortedChildren
                        BuildVisualNodes(lChild, Nothing, 0, "")
                    Next
                    Return
                End If
                
                ' Skip display based on settings
                If Not ShouldDisplayNode(vNode) Then Return
                
                ' Create visual node
                Dim lVisualNode As New VisualNode()
                lVisualNode.Node = vNode
                lVisualNode.Parent = vParent
                lVisualNode.Level = vLevel
                lVisualNode.NodePath = If(String.IsNullOrEmpty(vParentPath), 
                                          vNode.Name,
                                          vParentPath & "." & vNode.Name)
                
                ' Add to visible list
                pVisibleNodes.Add(lVisualNode)
                
                ' Cache the node
                If Not String.IsNullOrEmpty(lVisualNode.NodePath) Then
                    pNodeCache(lVisualNode.NodePath) = lVisualNode
                End If
                
                ' Process children if expanded or special cases
                If vNode.Children.Count > 0 Then
                    Dim lShouldShowChildren As Boolean = False
                    
                    ' Always show document and root namespace children
                    If vNode.NodeType = CodeNodeType.eDocument OrElse
                       (vNode.NodeType = CodeNodeType.eNamespace AndAlso vLevel = 0) Then
                        lShouldShowChildren = True
                    ElseIf pExpandedNodes.Contains(lVisualNode.NodePath) Then
                        lShouldShowChildren = True
                    End If
                    
                    If lShouldShowChildren Then
                        ' Sort children alphabetically by name (case-insensitive)
                        Dim lSortedChildren As List(Of SyntaxNode) = vNode.Children.OrderBy(
                            Function(c) c.Name, StringComparer.OrdinalIgnoreCase).ToList()
                        
                        for each lChild in lSortedChildren
                            BuildVisualNodes(lChild, lVisualNode, vLevel + 1, lVisualNode.NodePath)
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildVisualNodes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Checks if a node has any displayable children
        ''' </summary>
        ''' <param name="vNode">The node to check</param>
        ''' <returns>True if the node has at least one displayable child</returns>
        Private Function HasDisplayableChildren(vNode As SyntaxNode) As Boolean
            Try
                If vNode Is Nothing OrElse vNode.Children.Count = 0 Then Return False
                
                ' Check if any child should be displayed
                for each lChild in vNode.Children
                    If ShouldDisplayNode(lChild) Then Return True
                    ' Recursively check children of non-displayed nodes
                    If HasDisplayableChildren(lChild) Then Return True
                Next
                
                Return False
                
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
        
    End Class
    
End Namespace