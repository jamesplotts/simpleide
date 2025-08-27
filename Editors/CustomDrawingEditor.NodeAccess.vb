' CustomDrawingEditor.NodeAccess.vb - Node graph access methods for CodeSense and Navigation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' NOTE: Field declarations for pDocumentNodes, pRootNode, and pRootNodes 
        ' should be in the main CustomDrawingEditor.vb file as:
        ' Private pDocumentNodes As Dictionary(Of String, DocumentNode)
        ' Private pRootNode As SyntaxNode  
        ' Private pRootNodes As List(Of DocumentNode)
        
        ' Document node graph methods are in the main CustomDrawingEditor.vb file
        ' This file contains only the unique methods not duplicated elsewhere
        
        ' Get containing method/function for a given position
        Public Function GetContainingMethod(vLine As Integer, vColumn As Integer) As DocumentNode
            Try
                ' Start with the node at this position and walk up the hierarchy
                Dim lCurrentNode As DocumentNode = GetNodeAtPosition(vLine, vColumn)
                
                While lCurrentNode IsNot Nothing
                    If lCurrentNode.NodeType = CodeNodeType.eMethod OrElse lCurrentNode.NodeType = CodeNodeType.eFunction Then
                        Return lCurrentNode
                    End If
                    lCurrentNode = lCurrentNode.Parent
                End While
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetContainingMethod error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        Private Function GetNodeAtPosition(vLine As Integer, vColumn As Integer) As DocumentNode
            Try
                If pDocumentNodes Is Nothing Then Return Nothing
                
                For Each lKvp As KeyValuePair(Of String, DocumentNode) In pDocumentNodes
                    Dim lDocumentNode As DocumentNode = CType(lKvp.Value, DocumentNode)
                    If lDocumentNode.StartLine <= vLine AndAlso lDocumentNode.EndLine >= vLine Then 
                        Return lDocumentNode
                    End If
                Next
                
                Return Nothing
            Catch ex As Exception
                Console.WriteLine($"GetNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Update line metadata from parse result
        Private Sub UpdateLineMetadataFromParseResult(vParseResult As ParseResult)
            Try
                ' Update document nodes
                pDocumentNodes = vParseResult.DocumentNodes
                
                ' Store root nodes list (not pRootNode which is SyntaxNode)
                pRootNodes = vParseResult.RootNodes
                
                ' Update line metadata
                If vParseResult.LineMetadata IsNot Nothing Then
                    pLineMetadata = vParseResult.LineMetadata
                End If
                
                Console.WriteLine($"updated document graph with {pDocumentNodes.Count} Nodes and Metadata with Node References for {pLineMetadata.Length} lines")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineMetadataFromParseResult error: {ex.Message}")
            End Try
        End Sub
        
        ' NOTE: RebuildNodeGraph is implemented in the main file
        
        ' NOTE: ConvertObjectTypeToNodeType and ConvertMemberTypeToNodeType 
        ' are now in VBCodeParser to avoid duplication

        ' Public method to get document nodes
        Public Function GetDocumentNodes() As Dictionary(Of String, DocumentNode) Implements IEditor.GetDocumentNodes
            Return pDocumentNodes
        End Function
        
        ' Public method to get root nodes
        Public Function GetRootNodes() As List(Of DocumentNode)
            Return pRootNodes
        End Function
        
        ' Public method to get all nodes as a flat list
        Public Function GetAllNodes() As List(Of DocumentNode)
            Dim lAllNodes As New List(Of DocumentNode)
            
            Try
                ' Add all nodes from the dictionary
                If pDocumentNodes IsNot Nothing Then
                    lAllNodes.AddRange(pDocumentNodes.Values)
                End If
                
                ' Alternative implementation using pRootNodes if needed:
                ' If pRootNodes IsNot Nothing Then
                '     For Each lNode In pRootNodes
                '         AddNodeAndChildrenToList(lAllNodes, lNode)
                '     Next
                ' End If
                
            Catch ex As Exception
                Console.WriteLine($"GetAllNodes error: {ex.Message}")
            End Try
            
            Return lAllNodes
        End Function
        
        ' Helper method to recursively add node and its children to a list
        Private Sub AddNodeAndChildrenToList(vList As List(Of DocumentNode), vNode As DocumentNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Add the node itself
                vList.Add(vNode)
                
                ' Add all children recursively
                If vNode.Children IsNot Nothing Then
                    For Each lChild In vNode.Children
                        AddNodeAndChildrenToList(vList, lChild)
                    Next
                End If
            Catch ex As Exception
                Console.WriteLine($"AddNodeAndChildrenToList error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
    ' Node information helper class
    Public Class NodeInfo
        Public Property ContainingNode As DocumentNode
        Public Property ContainingClass As DocumentNode
        Public Property ContainingMethod As DocumentNode
        Public Property VariablesInScope As List(Of DocumentNode)
        Public Property AvailableMembers As List(Of DocumentNode)
        Public Property IsInClassScope As Boolean
        Public Property IsInMethodScope As Boolean
        
        Public Sub New()
            VariablesInScope = New List(Of DocumentNode)()
            AvailableMembers = New List(Of DocumentNode)()
        End Sub
        
        Public Sub UpdateFlags()
            IsInClassScope = ContainingClass IsNot Nothing
            IsInMethodScope = ContainingMethod IsNot Nothing
        End Sub
        
        ' NOTE: ParseResult is now defined only in VBCodeParser.vb
    
    End Class
    
End Namespace