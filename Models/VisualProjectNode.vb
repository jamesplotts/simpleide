' Models/VisualProjectNode.vb

Namespace Models
    
    ''' <summary>
    ''' Represents a visual node with layout information for rendering in the project explorer
    ''' </summary>
    Public Class VisualProjectNode
        
        ''' <summary>
        ''' Gets or sets the underlying project node data
        ''' </summary>
        Public Property Node As ProjectNode
        
        ''' <summary>
        ''' Gets or sets the X coordinate for rendering
        ''' </summary>
        Public Property X As Integer
        
        ''' <summary>
        ''' Gets or sets the Y coordinate for rendering
        ''' </summary>
        Public Property Y As Integer
        
        ''' <summary>
        ''' Gets or sets the width of the node when rendered
        ''' </summary>
        Public Property Width As Integer
        
        ''' <summary>
        ''' Gets or sets the height of the node when rendered
        ''' </summary>
        Public Property Height As Integer
        
        ''' <summary>
        ''' Gets or sets the depth level in the tree hierarchy
        ''' </summary>
        Public Property Depth As Integer
        
        ''' <summary>
        ''' Gets or sets whether this node is currently visible
        ''' </summary>
        Public Property IsVisible As Boolean
        
        ''' <summary>
        ''' Gets or sets whether this node has child nodes
        ''' </summary>
        Public Property HasChildren As Boolean
        
        ''' <summary>
        ''' Gets or sets whether this node is currently expanded
        ''' </summary>
        Public Property IsExpanded As Boolean
        
        ''' <summary>
        ''' Gets or sets the rectangle area for the expand/collapse button
        ''' </summary>
        Public Property PlusMinusRect As Gdk.Rectangle
        
        ''' <summary>
        ''' Gets or sets the rectangle area for the node icon
        ''' </summary>
        Public Property IconRect As Gdk.Rectangle
        
        ''' <summary>
        ''' Gets or sets the rectangle area for the node text
        ''' </summary>
        Public Property TextRect As Gdk.Rectangle
        
    End Class
    
End Namespace