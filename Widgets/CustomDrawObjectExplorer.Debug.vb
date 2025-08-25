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
        ''' Debug method to trace why nodes aren't being displayed
        ''' </summary>
        Public Sub DebugTreeBuilding()
            Try
                Console.WriteLine("========== DEBUG TREE BUILDING ==========")
                Console.WriteLine($"Root Node: {If(pRootNode IsNot Nothing, pRootNode.Name & " (" & pRootNode.NodeType.ToString() & ")", "Nothing")}")
                
                If pRootNode IsNot Nothing Then
                    Console.WriteLine($"Root has {pRootNode.Children.Count} children")
                    
                    ' Check what happens during tree building
                    Console.WriteLine("Attempting to build visual tree...")
                    
                    ' Clear and rebuild with debug output
Console.WriteLine($"DebugTreeBuilding  pVisibleNodesClear()")

                    pVisibleNodes.Clear()
                    pNodeCache.Clear()
                    
                    ' Add debug output to build process
                    BuildVisualNodesDebug(pRootNode, Nothing, 0, "", 0)
                    
                    Console.WriteLine($"After build: {pVisibleNodes.Count} visible nodes")
                    
                    ' List first few nodes
                    for i As Integer = 0 To Math.Min(5, pVisibleNodes.Count - 1)
                        Dim lNode As VisualNode = pVisibleNodes(i)
                        Console.WriteLine($"  [{i}] Level={lNode.Level}, Name={lNode.Node.Name}, Type={lNode.Node.NodeType}")
                    Next
                    
                    ' Calculate positions
                    CalculateNodePositions()
                    Console.WriteLine($"Content size: {pContentWidth}x{pContentHeight}")
                    
                    ' Force a redraw
                    pDrawingArea?.QueueDraw()
                End If
                
                Console.WriteLine("========================================")
                
            Catch ex As Exception
                Console.WriteLine($"DebugTreeBuilding error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Debug version of BuildVisualNodes with console output - Fixed for document roots
        ''' </summary>
        Private Sub BuildVisualNodesDebug(vNode As SyntaxNode, vParent As VisualNode, vLevel As Integer, vParentPath As String, vDepth As Integer)
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
                        BuildVisualNodesDebug(lChild, vParent, vLevel, vParentPath, vDepth)
                    Next
                    Return
                End If
                
                ' Check if node should be displayed
                Dim lShouldDisplay As Boolean = ShouldDisplayNodeDebug(vNode)
                Console.WriteLine($"{lIndent}  ShouldDisplay: {lShouldDisplay}")
                
                If Not lShouldDisplay Then Return
                
                ' Create visual node
                Dim lVisualNode As New VisualNode() with {
                    .Node = vNode,
                    .Level = vLevel,
                    .Parent = vParent,
                    .NodePath = If(String.IsNullOrEmpty(vParentPath), vNode.Name, $"{vParentPath}/{vNode.Name}"),
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
                        BuildVisualNodesDebug(lChild, lVisualNode, vLevel + 1, lVisualNode.NodePath, vDepth + 1)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildVisualNodesDebug error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Debug version of ShouldDisplayNode with more permissive filtering
        ''' </summary>
        Private Function ShouldDisplayNodeDebug(vNode As SyntaxNode) As Boolean
            Try
                If vNode Is Nothing Then Return False
                
                ' Be more permissive for debugging - show almost everything
                Select Case vNode.NodeType
                    ' Always show these
                    Case CodeNodeType.eNamespace, 
                         CodeNodeType.eClass, CodeNodeType.eModule,
                         CodeNodeType.eInterface, CodeNodeType.eStructure,
                         CodeNodeType.eEnum, CodeNodeType.eDelegate
                        Return True
                        
                    ' FIXED: Handle document nodes specially in debug mode
                    Case CodeNodeType.eDocument
                        ' Show if it's the root node or has children
                        Return vNode Is pRootNode OrElse vNode.Children.Count > 0
                        
                    ' Show all members for debugging
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction,
                         CodeNodeType.eConstructor, CodeNodeType.eOperator,
                         CodeNodeType.eProperty, CodeNodeType.eEvent,
                         CodeNodeType.eField, CodeNodeType.eConstant, CodeNodeType.eConst
                        Return True
                        
                    ' Show enum values
                    Case CodeNodeType.eEnumValue
                        Return True
                        
                    ' Show regions if enabled
                    Case CodeNodeType.eRegion
                        Return pShowRegions
                        
                    ' Don't show these
                    Case CodeNodeType.eVariable, CodeNodeType.eParameter, CodeNodeType.eImport
                        Return False
                        
                    ' Default - show it for debugging
                    Case Else
                        Console.WriteLine($"    Unknown node type: {vNode.NodeType}")
                        Return True
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ShouldDisplayNodeDebug error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Force refresh with debugging
        ''' </summary>
        Public Sub ForceRefreshWithDebug()
            Try
                Console.WriteLine("=== FORCE REFRESH WITH DEBUG ===")
                
                ' Report current state
                Console.WriteLine($"Current state:")
                Console.WriteLine($"  Root: {If(pRootNode IsNot Nothing, "Present", "Nothing")}")
                Console.WriteLine($"  Visible nodes: {pVisibleNodes.Count}")
                Console.WriteLine($"  Viewport: {pViewportWidth}x{pViewportHeight}")
                
                ' Clear everything
                pNodeCache.Clear()
Console.WriteLine($"ForceRefreshWithDebug  pVisibleNodesClear()")

                pVisibleNodes.Clear()
                pExpandedNodes.Clear()
                
                ' Auto-expand root for debugging
                If pRootNode IsNot Nothing Then
                    If pRootNode.NodeType = CodeNodeType.eNamespace Then
                        pExpandedNodes.Add(pRootNode.Name)
                        Console.WriteLine($"Auto-expanded root namespace: {pRootNode.Name}")
                    End If
                    
                    ' Rebuild with debug
                    DebugTreeBuilding()
                End If
                
                ' Update display
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"After refresh: {pVisibleNodes.Count} visible nodes")
                Console.WriteLine("=================================")
                
            Catch ex As Exception
                Console.WriteLine($"ForceRefreshWithDebug error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
