' Models/ProjectNode.vb
Imports System.Collections.Generic

Namespace Models
    
    ''' <summary>
    ''' Represents a node in the project tree structure
    ''' </summary>
    Public Class ProjectNode
        
        ''' <summary>
        ''' Gets or sets the display name of the node
        ''' </summary>
        Public Property Name As String
        
        ''' <summary>
        ''' Gets or sets the file system path for this node
        ''' </summary>
        Public Property Path As String
        
        ''' <summary>
        ''' Gets or sets the type of project node
        ''' </summary>
        Public Property NodeType As ProjectNodeType
        
        ''' <summary>
        ''' Gets or sets whether this node represents a file
        ''' </summary>
        Public Property IsFile As Boolean
        
        ''' <summary>
        ''' Gets or sets whether this node is expanded in the tree view
        ''' </summary>
        Public Property IsExpanded As Boolean
        
        ''' <summary>
        ''' Gets or sets the collection of child nodes
        ''' </summary>
        Public Property Children As New List(Of ProjectNode)
        
        ''' <summary>
        ''' Gets or sets the parent node reference
        ''' </summary>
        Public Property Parent As ProjectNode
        
        ''' <summary>
        ''' Gets or sets the icon name for this node
        ''' </summary>
        Public Property IconName As String
        
        ''' <summary>
        ''' Gets or sets the tooltip text for this node
        ''' </summary>
        Public Property ToolTip As String
        
        ''' <summary>
        ''' Adds a child node to this node
        ''' </summary>
        ''' <param name="vChild">The child node to add</param>
        Public Sub AddChild(vChild As ProjectNode)
            If vChild IsNot Nothing Then
                vChild.Parent = Me
                Children.Add(vChild)
            End If
        End Sub
        
        ''' <summary>
        ''' Sorts children with References first, then folders, then files alphabetically
        ''' </summary>
        Public Sub SortChildren()
            Children.Sort(Function(a, b)
                ' References node always comes first
                If a.NodeType = ProjectNodeType.eReferences Then Return -1
                If b.NodeType = ProjectNodeType.eReferences Then Return 1
                
                ' Then special folders (Resources, My Project, Manifest)
                Dim aIsSpecial As Boolean = IsSpecialFolder(a.NodeType)
                Dim bIsSpecial As Boolean = IsSpecialFolder(b.NodeType)
                
                If aIsSpecial AndAlso Not bIsSpecial Then Return -1
                If Not aIsSpecial AndAlso bIsSpecial Then Return 1
                
                ' If both special, sort by predefined order
                If aIsSpecial AndAlso bIsSpecial Then
                    Return GetSpecialFolderOrder(a.NodeType).CompareTo(GetSpecialFolderOrder(b.NodeType))
                End If
                
                ' Regular folders before files
                If a.IsFile <> b.IsFile Then
                    Return If(a.IsFile, 1, -1)
                End If
                
                ' Then alphabetical
                Return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
            End Function)
            
            ' Recursively sort children
            For Each lChild In Children
                lChild.SortChildren()
            Next
        End Sub
        
        ''' <summary>
        ''' Determines if a node type is a special folder
        ''' </summary>
        ''' <param name="vNodeType">The node type to check</param>
        ''' <returns>True if it's a special folder, False otherwise</returns>
        Private Function IsSpecialFolder(vNodeType As ProjectNodeType) As Boolean
            Select Case vNodeType
                Case ProjectNodeType.eReferences,
                     ProjectNodeType.eResources,
                     ProjectNodeType.eMyProject,
                     ProjectNodeType.eManifest
                    Return True
                Case Else
                    Return False
            End Select
        End Function
        
        ''' <summary>
        ''' Gets the sort order for special folders
        ''' </summary>
        ''' <param name="vNodeType">The node type</param>
        ''' <returns>Sort order (lower = higher priority)</returns>
        Private Function GetSpecialFolderOrder(vNodeType As ProjectNodeType) As Integer
            Select Case vNodeType
                Case ProjectNodeType.eReferences
                    Return 0  ' Always first
                Case ProjectNodeType.eResources
                    Return 1
                Case ProjectNodeType.eMyProject
                    Return 2
                Case ProjectNodeType.eManifest
                    Return 3
                Case Else
                    Return 99
            End Select
        End Function
        
    End Class
    
End Namespace
