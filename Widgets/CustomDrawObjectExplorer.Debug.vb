' CustomDrawObjectExplorer.Debug.vb
' Created: 2025-08-17 08:42:56

Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Interfaces

Namespace Widgets

    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer


        
        ''' <summary>
        ''' Debug method to display the actual tree structure received from ProjectParser
        ''' </summary>
        Public Sub DebugProjectParserOutput()
            Try
                Console.WriteLine("========================================")
                Console.WriteLine("DEBUG: ProjectParser Output Analysis")
                Console.WriteLine("========================================")
                
                If pRootNode Is Nothing Then
                    Console.WriteLine("ERROR: pRootNode Is Nothing - No tree loaded")
                    Return
                End If
                
                Console.WriteLine($"Root Node: {pRootNode.Name} ({pRootNode.NodeType})")
                Console.WriteLine($"Root has {pRootNode.Children.Count} children")
                
                ' Recursively display the tree structure
                DisplayNodeTree(pRootNode, 0, 5) ' Show up to 5 levels deep
                
                Console.WriteLine()
                Console.WriteLine("--- Analysis ---")
                
                ' Count node types
                Dim lNodeCounts As New Dictionary(Of CodeNodeType, Integer)()
                CountNodeTypes(pRootNode, lNodeCounts)
                
                Console.WriteLine("Node Type Counts:")
                for each lEntry in lNodeCounts.OrderBy(Function(x) x.Key.ToString())
                    Console.WriteLine($"  {lEntry.Key}: {lEntry.Value}")
                Next
                
                ' Check for specific issues
                Console.WriteLine()
                Console.WriteLine("Checking for issues...")
                
                ' Check if namespaces have children
                Dim lEmptyNamespaces As New List(Of String)()
                CheckForEmptyNamespaces(pRootNode, lEmptyNamespaces)
                
                If lEmptyNamespaces.Count > 0 Then
                    Console.WriteLine($"WARNING: Found {lEmptyNamespaces.Count} empty namespaces:")
                    for each lName in lEmptyNamespaces
                        Console.WriteLine($"  - {lName}")
                    Next
                Else
                    Console.WriteLine("All namespaces have children")
                End If
                
                ' Check filtering
                Console.WriteLine()
                Console.WriteLine("Filter Settings:")
                Console.WriteLine($"  ShowPrivateMembers: {pShowPrivateMembers}")
                Console.WriteLine($"  ShowInheritedMembers: {pShowInheritedMembers}")
                Console.WriteLine($"  ShowRegions: {pShowRegions}")
                
                ' Check what would be displayed
                Console.WriteLine()
                Console.WriteLine("Testing ShouldDisplayNode for all nodes:")
                TestDisplayFiltering(pRootNode, 0)
                
                Console.WriteLine()
                Console.WriteLine("========================================")
                
            Catch ex As Exception
                Console.WriteLine($"DebugProjectParserOutput error: {ex.Message}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.BuildVisualNodesxDebug
        ''' <summary>
        ''' Debug version of BuildVisualNodes with console output - Fixed for document roots
        ''' </summary>
        Private Sub BuildVisualNodesxDebug(vNode As SyntaxNode, vParent As VisualNode, vLevel As Integer, vParentPath As String, vDepth As Integer)
            Try
                If vNode Is Nothing Then
                    Console.WriteLine($"{New String(" "c, vDepth * 2)}Node is Nothing")
                    Return
                End If
                
                Dim lIndent As String = New String(" "c, vDepth * 2)
                Console.WriteLine($"{lIndent}Processing: {vNode.Name} ({vNode.NodeType})")
                
                ' FIXED: Special handling for root document nodes in debug mode
                If vNode Is pRootNode AndAlso vNode.NodeType = CodeNodeType.eDocument Then
                    Console.WriteLine($"{lIndent}  Root is eDocument - processing children directly")
                    for each lChild in vNode.Children
                        BuildVisualNodesxDebug(lChild, vParent, vLevel, vParentPath, vDepth)
                    Next
                    Return
                End If
                
                ' Check if node should be displayed
                Dim lShouldDisplay As Boolean = ShouldDisplayNode(vNode)
                Console.WriteLine($"{lIndent}  ShouldDisplay: {lShouldDisplay}")
                
                If Not lShouldDisplay Then Return
                
                ' Create visual node - FIXED: Use dot separator instead of slash
                Dim lVisualNode As New VisualNode() with {
                    .Node = vNode,
                    .Level = vLevel,
                    .Parent = vParent,
                    .NodePath = If(String.IsNullOrEmpty(vParentPath), vNode.Name, $"{vParentPath}.{vNode.Name}"),  ' Changed from "/" to "."
                    .IsVisible = True
                }
                
                ' Check if has displayable children
                lVisualNode.HasChildren = HasDisplayableChildren(vNode)
                Console.WriteLine($"{lIndent}  HasChildren: {lVisualNode.HasChildren}")
                
                ' Check if expanded (for debugging, expand root namespace by default)
                If vNode.NodeType = CodeNodeType.eNamespace AndAlso vLevel = 0 Then
                    lVisualNode.IsExpanded = True
                    pExpandedNodes.Add(lVisualNode.NodePath)
                    Console.WriteLine($"{lIndent}  Auto-expanding root namespace")
                Else
                    lVisualNode.IsExpanded = pExpandedNodes.Contains(lVisualNode.NodePath)
                End If
                Console.WriteLine($"{lIndent}  IsExpanded: {lVisualNode.IsExpanded}")
                
                ' Add to visible list
                pVisibleNodes.Add(lVisualNode)
                Console.WriteLine($"{lIndent}  Added to visible nodes (count={pVisibleNodes.Count})")
                
                ' Cache for quick lookup
                pNodeCache(lVisualNode.NodePath) = lVisualNode
                
                ' Add to parent's children
                If vParent IsNot Nothing Then
                    vParent.Children.Add(lVisualNode)
                End If
                
                ' Process children if expanded
                If lVisualNode.IsExpanded AndAlso lVisualNode.HasChildren Then
                    Console.WriteLine($"{lIndent}  Processing {vNode.Children.Count} children...")
                    for each lChild in vNode.Children
                        BuildVisualNodesxDebug(lChild, lVisualNode, vLevel + 1, lVisualNode.NodePath, vDepth + 1)
                    Next
                Else
                    Console.WriteLine($"{lIndent}  Not processing children (IsExpanded={lVisualNode.IsExpanded}, HasChildren={lVisualNode.HasChildren})")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildVisualNodesxDebug error: {ex.Message}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.ShouldDisplayNode

        
        ''' <summary>
        ''' Recursively displays the node tree structure
        ''' </summary>
        Private Sub DisplayNodeTree(vNode As SyntaxNode, vDepth As Integer, vMaxDepth As Integer)
            Try
                If vNode Is Nothing OrElse vDepth > vMaxDepth Then Return
                
                Dim lIndent As String = New String(" "c, vDepth * 2)
                Dim lNodeInfo As String = $"{vNode.Name} ({vNode.NodeType})"
                
                ' Add additional info for types
                If vNode.NodeType = CodeNodeType.eClass OrElse 
                   vNode.NodeType = CodeNodeType.eModule OrElse
                   vNode.NodeType = CodeNodeType.eInterface OrElse
                   vNode.NodeType = CodeNodeType.eStructure Then
                    lNodeInfo &= $" [Public:{vNode.IsPublic}, Partial:{vNode.IsPartial}, File:{System.IO.Path.GetFileName(vNode.FilePath)}]"
                End If
                
                Console.WriteLine($"{lIndent}{lNodeInfo}")
                
                ' Show children
                If vNode.Children.Count > 0 Then
                    Console.WriteLine($"{lIndent}  Children: {vNode.Children.Count}")
                    for each lChild in vNode.Children
                        DisplayNodeTree(lChild, vDepth + 1, vMaxDepth)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DisplayNodeTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Counts node types in the tree
        ''' </summary>
        Private Sub CountNodeTypes(vNode As SyntaxNode, vCounts As Dictionary(Of CodeNodeType, Integer))
            Try
                If vNode Is Nothing Then Return
                
                ' Count this node
                If Not vCounts.ContainsKey(vNode.NodeType) Then
                    vCounts(vNode.NodeType) = 0
                End If
                vCounts(vNode.NodeType) += 1
                
                ' Count children
                for each lChild in vNode.Children
                    CountNodeTypes(lChild, vCounts)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CountNodeTypes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Checks for namespaces with no children
        ''' </summary>
        Private Sub CheckForEmptyNamespaces(vNode As SyntaxNode, vEmptyList As List(Of String))
            Try
                If vNode Is Nothing Then Return
                
                If vNode.NodeType = CodeNodeType.eNamespace Then
                    Dim lHasNonNamespaceChildren As Boolean = False
                    
                    for each lChild in vNode.Children
                        If lChild.NodeType <> CodeNodeType.eNamespace Then
                            lHasNonNamespaceChildren = True
                            Exit for
                        End If
                    Next
                    
                    If Not lHasNonNamespaceChildren Then
                        vEmptyList.Add(vNode.Name)
                    End If
                End If
                
                ' Check children
                for each lChild in vNode.Children
                    CheckForEmptyNamespaces(lChild, vEmptyList)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CheckForEmptyNamespaces error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Tests which nodes would be displayed with current filter settings
        ''' </summary>
        Private Sub TestDisplayFiltering(vNode As SyntaxNode, vDepth As Integer)
            Try
                If vNode Is Nothing Then Return
                
                Dim lIndent As String = New String(" "c, vDepth * 2)
                Dim lWouldDisplay As Boolean = ShouldDisplayNode(vNode)
                
                If Not lWouldDisplay Then
                    Console.WriteLine($"{lIndent}FILTERED OUT: {vNode.Name} ({vNode.NodeType})")
                End If
                
                ' Check children
                for each lChild in vNode.Children
                    TestDisplayFiltering(lChild, vDepth + 1)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"TestDisplayFiltering error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Diagnoses partial class merge issues in the Object Explorer
        ''' </summary>
        Public Sub DiagnoseMergeIssue()
            Try
                Console.WriteLine("=== OBJECT EXPLORER MERGE DIAGNOSIS ===")
                
                If pRootNode Is Nothing Then
                    Console.WriteLine("No root node loaded")
                    Return
                End If
                
                Console.WriteLine($"Root node: {pRootNode.Name} ({pRootNode.NodeType})")
                Console.WriteLine($"Root children: {pRootNode.Children.Count}")
                
                ' Check for namespaces with duplicate class names
                Dim lNamespaces As New Dictionary(Of String, SyntaxNode)()
                Dim lClassesByNamespace As New Dictionary(Of String, Dictionary(Of String, List(Of SyntaxNode)))()
                
                ' Collect all namespaces and classes
                CollectNamespacesAndClasses(pRootNode, "", lNamespaces, lClassesByNamespace)
                
                ' Report findings
                Console.WriteLine($"Found {lNamespaces.Count} namespaces:")
                
                for each lNsKvp in lNamespaces
                    Dim lNamespaceName As String = lNsKvp.Key
                    Dim lNamespaceNode As SyntaxNode = lNsKvp.Value
                    
                    Console.WriteLine($"  Namespace: {If(String.IsNullOrEmpty(lNamespaceName), "[root]", lNamespaceName)}")
                    
                    If lClassesByNamespace.ContainsKey(lNamespaceName) Then
                        Dim lClasses As Dictionary(Of String, List(Of SyntaxNode)) = lClassesByNamespace(lNamespaceName)
                        
                        for each lClassKvp in lClasses
                            Dim lClassName As String = lClassKvp.Key
                            Dim lClassNodes As List(Of SyntaxNode) = lClassKvp.Value
                            
                            If lClassNodes.Count > 1 Then
                                ' PROBLEM: Multiple nodes with same class name
                                Console.WriteLine($"    *** MERGE ISSUE: Class '{lClassName}' has {lClassNodes.Count} separate nodes!")
                                For Each lNode In lClassNodes
                                    Dim lFileInfo As String = ""
                                    If lNode.Attributes IsNot Nothing Then
                                        If lNode.Attributes.ContainsKey("FilePaths") Then
                                            lFileInfo = $" Files: {lNode.Attributes("FilePaths")}"
                                        ElseIf lNode.Attributes.ContainsKey("FilePath") Then
                                            lFileInfo = $" File: {lNode.Attributes("FilePath")}"
                                        End If
                                    End If
                                    Console.WriteLine($"      - Node with {lNode.Children.Count} members, IsPartial={lNode.IsPartial}{lFileInfo}")
                                Next
                            Else
                                ' Single node - check if it's a properly merged partial class
                                Dim lNode As SyntaxNode = lClassNodes(0)
                                If lNode.IsPartial Then
                                    Dim lFileCount As Integer = 1
                                    If lNode.Attributes IsNot Nothing AndAlso lNode.Attributes.ContainsKey("FilePaths") Then
                                        lFileCount = lNode.Attributes("FilePaths").Split(";"c).Length
                                    End If
                                    Console.WriteLine($"    OK: Class '{lClassName}' [PARTIAL] with {lNode.Children.Count} members from {lFileCount} files")
                                Else
                                    Console.WriteLine($"    OK: Class '{lClassName}' with {lNode.Children.Count} members")
                                End If
                            End If
                        Next
                    End If
                Next
                
                Console.WriteLine("=== End DIAGNOSIS ===")
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseMergeIssue error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper to collect namespaces and classes for diagnosis
        ''' </summary>
        Private Sub CollectNamespacesAndClasses(vNode As SyntaxNode, 
                                               vCurrentNamespace As String,
                                               vNamespaces As Dictionary(Of String, SyntaxNode),
                                               vClasses As Dictionary(Of String, Dictionary(Of String, List(Of SyntaxNode))))
            Try
                If vNode Is Nothing Then Return
                
                Select Case vNode.NodeType
                    Case CodeNodeType.eDocument
                        ' Process document children
                        For Each lChild In vNode.Children
                            CollectNamespacesAndClasses(lChild, vCurrentNamespace, vNamespaces, vClasses)
                        Next
                        
                    Case CodeNodeType.eNamespace
                        ' Track namespace
                        Dim lNamespaceName As String = vNode.Name
                        If Not String.IsNullOrEmpty(vCurrentNamespace) Then
                            lNamespaceName = vCurrentNamespace & "." & lNamespaceName
                        End If
                        
                        If Not vNamespaces.ContainsKey(lNamespaceName) Then
                            vNamespaces(lNamespaceName) = vNode
                        End If
                        
                        ' Process namespace children
                        For Each lChild In vNode.Children
                            CollectNamespacesAndClasses(lChild, lNamespaceName, vNamespaces, vClasses)
                        Next
                        
                    Case CodeNodeType.eClass, CodeNodeType.eModule
                        ' Track class/module
                        If Not vClasses.ContainsKey(vCurrentNamespace) Then
                            vClasses(vCurrentNamespace) = New Dictionary(Of String, List(Of SyntaxNode))()
                        End If
                        
                        If Not vClasses(vCurrentNamespace).ContainsKey(vNode.Name) Then
                            vClasses(vCurrentNamespace)(vNode.Name) = New List(Of SyntaxNode)()
                        End If
                        
                        vClasses(vCurrentNamespace)(vNode.Name).Add(vNode)
                        
                        ' Process nested types
                        For Each lChild In vNode.Children
                            If lChild.NodeType = CodeNodeType.eClass OrElse 
                               lChild.NodeType = CodeNodeType.eModule Then
                                CollectNamespacesAndClasses(lChild, vCurrentNamespace, vNamespaces, vClasses)
                            End If
                        Next
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"CollectNamespacesAndClasses error: {ex.Message}")
            End Try
        End Sub

        ' Add: SimpleIDE.Widgets.CustomDrawObjectExplorer.ForceExpandNamespaces
        ' To: CustomDrawObjectExplorer.Debug.vb
        ''' <summary>
        ''' Debug method to force expand all namespace nodes for testing
        ''' </summary>
        Public Sub ForceExpandNamespaces()
            Try
                Console.WriteLine("ForceExpandNamespaces: Starting...")
                
                If pRootNode Is Nothing Then
                    Console.WriteLine("ForceExpandNamespaces: No root node")
                    Return
                End If
                
                ' Clear and rebuild expanded nodes
                pExpandedNodes.Clear()
                
                ' Recursively add all namespace nodes to expanded set
                AddNamespaceNodesToExpanded(pRootNode, "")
                
                Console.WriteLine($"ForceExpandNamespaces: Added {pExpandedNodes.Count} paths To expanded Set:")
                For Each lPath In pExpandedNodes
                    Console.WriteLine($"  - {lPath}")
                Next
                
                ' Rebuild the visual tree
                RebuildVisualTree()
                
                ' Force redraw
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"ForceExpandNamespaces: Complete. {pVisibleNodes.Count} nodes now visible")
                
            Catch ex As Exception
                Console.WriteLine($"ForceExpandNamespaces error: {ex.Message}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.AddNamespaceNodesToExpanded
        ''' <summary>
        ''' Helper to recursively add namespace nodes to expanded set
        ''' </summary>
        ''' <param name="vNode">Current node to process</param>
        ''' <param name="vParentPath">Path of parent node</param>
        Private Sub AddNamespaceNodesToExpanded(vNode As SyntaxNode, vParentPath As String)
            Try
                If vNode Is Nothing Then Return
                
                ' Special handling for document root
                If vNode.NodeType = CodeNodeType.eDocument Then
                    For Each lChild In vNode.Children
                        AddNamespaceNodesToExpanded(lChild, "")
                    Next
                    Return
                End If
                
                ' Build current path - FIXED: Use dot separator instead of slash
                Dim lCurrentPath As String = If(String.IsNullOrEmpty(vParentPath), 
                                                vNode.Name, 
                                                vParentPath & "." & vNode.Name)  ' Changed from "/" to "."
                
                ' Add namespace nodes and classes with members
                If vNode.NodeType = CodeNodeType.eNamespace OrElse
                   vNode.NodeType = CodeNodeType.eClass OrElse
                   vNode.NodeType = CodeNodeType.eModule OrElse
                   vNode.NodeType = CodeNodeType.eStructure OrElse
                   vNode.NodeType = CodeNodeType.eInterface Then
                    
                    If vNode.Children.Count > 0 Then
                        pExpandedNodes.Add(lCurrentPath)
                        
                        ' Recursively process children
                        For Each lChild In vNode.Children
                            AddNamespaceNodesToExpanded(lChild, lCurrentPath)
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddNamespaceNodesToExpanded error: {ex.Message}")
            End Try
        End Sub

        ' Replace: SimpleIDE.Widgets.CustomDrawObjectExplorer.HasDisplayableChildren


        ' Add: SimpleIDE.Widgets.CustomDrawObjectExplorer.DebugBuildVisualNodes
        ' To: CustomDrawObjectExplorer.Debug.vb
        ''' <summary>
        ''' Debug the BuildVisualNodes process to see why children aren't showing
        ''' </summary>
        Public Sub DebugBuildVisualNodes()
            Try
                Console.WriteLine("=== DEBUG BUILD VISUAL NODES ===")
                Console.WriteLine($"pExpandedNodes contains {pExpandedNodes.Count} paths")
                Console.WriteLine($"ShowPrivateMembers = {pShowPrivateMembers}")
                Console.WriteLine()
                
                If pRootNode Is Nothing Then
                    Console.WriteLine("Root node Is Nothing")
                    Return
                End If
                
                Console.WriteLine($"Root: {pRootNode.Name} ({pRootNode.NodeType})")
                Console.WriteLine($"Root has {pRootNode.Children.Count} children")
                
                ' Check what the root's children look like
                For Each lChild In pRootNode.Children
                    Console.WriteLine($"  Root child: {lChild.Name} ({lChild.NodeType}) - {lChild.Children.Count} children")
                    
                    ' Check if this child would be displayed
                    Dim lShouldDisplay As Boolean = ShouldDisplayNode(lChild)
                    Console.WriteLine($"    ShouldDisplay = {lShouldDisplay}")
                    
                    ' Check first few grandchildren
                    Dim lCount As Integer = 0
                    For Each lGrandchild In lChild.Children
                        If lCount >= 3 Then Exit For
                        Console.WriteLine($"    Child: {lGrandchild.Name} ({lGrandchild.NodeType})")
                        Console.WriteLine($"      ShouldDisplay = {ShouldDisplayNode(lGrandchild)}")
                        Console.WriteLine($"      HasDisplayableChildren = {HasDisplayableChildren(lGrandchild)}")
                        
                        ' Check a few members of this class
                        If lGrandchild.NodeType = CodeNodeType.eClass Then
                            Dim lMemberCount As Integer = 0
                            For Each lMember In lGrandchild.Children
                                If lMemberCount >= 3 Then Exit For
                                Console.WriteLine($"        Member: {lMember.Name} ({lMember.NodeType})")
                                Console.WriteLine($"          ShouldDisplay = {ShouldDisplayNode(lMember)}")
                                lMemberCount += 1
                            Next
                        End If
                        
                        lCount += 1
                    Next
                Next
                
                Console.WriteLine()
                Console.WriteLine("Now rebuilding visual tree...")
                RebuildVisualTree()
                
                Console.WriteLine($"Result: {pVisibleNodes.Count} visible nodes")
                Console.WriteLine("=================================")
                
            Catch ex As Exception
                Console.WriteLine($"DebugBuildVisualNodes error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
