' Models/DefinitionInfo.vb
' Created: 2025-01-15

Imports System
Imports SimpleIDE.Syntax

Namespace Models
    
    ''' <summary>
    ''' Contains information about the location of a symbol definition
    ''' </summary>
    ''' <remarks>
    ''' Used by the ProjectManager to return definition location information
    ''' for Go to Definition functionality
    ''' </remarks>
    Public Class DefinitionInfo
        
        ''' <summary>
        ''' Gets or sets the full path to the file containing the definition
        ''' </summary>
        ''' <value>Full file path where the definition is located</value>
        Public Property FilePath As String
        
        ''' <summary>
        ''' Gets or sets the 0-based line number of the definition
        ''' </summary>
        ''' <value>0-based line index in the file</value>
        Public Property Line As Integer
        
        ''' <summary>
        ''' Gets or sets the 0-based column position of the definition
        ''' </summary>
        ''' <value>0-based column index on the line</value>
        Public Property Column As Integer
        
        ''' <summary>
        ''' Gets or sets the SyntaxNode representing the definition
        ''' </summary>
        ''' <value>The syntax node for the defined symbol</value>
        Public Property Node As SyntaxNode
        
        ''' <summary>
        ''' Gets or sets the type of the definition
        ''' </summary>
        ''' <value>The code node type (class, method, property, etc.)</value>
        Public Property NodeType As CodeNodeType
        
        ''' <summary>
        ''' Gets or sets the fully qualified name of the symbol
        ''' </summary>
        ''' <value>Full namespace and class path to the symbol</value>
        Public Property FullyQualifiedName As String
        
        ''' <summary>
        ''' Gets or sets whether this is a partial definition
        ''' </summary>
        ''' <value>True if this is a partial class/module definition</value>
        Public Property IsPartial As Boolean
        
        ''' <summary>
        ''' Creates a new empty DefinitionInfo instance
        ''' </summary>
        Public Sub New()
        End Sub
        
        ''' <summary>
        ''' Creates a new DefinitionInfo with basic location information
        ''' </summary>
        ''' <param name="vFilePath">Full path to the file</param>
        ''' <param name="vLine">0-based line number</param>
        ''' <param name="vColumn">0-based column position</param>
        Public Sub New(vFilePath As String, vLine As Integer, vColumn As Integer)
            FilePath = vFilePath
            Line = vLine
            Column = vColumn
        End Sub
        
        ''' <summary>
        ''' Creates a new DefinitionInfo from a SyntaxNode
        ''' </summary>
        ''' <param name="vNode">The syntax node representing the definition</param>
        ''' <param name="vFilePath">Full path to the file containing the node</param>
        Public Sub New(vNode As SyntaxNode, vFilePath As String)
            If vNode IsNot Nothing Then
                Node = vNode
                FilePath = vFilePath
                Line = vNode.StartLine
                Column = vNode.StartColumn
                NodeType = vNode.NodeType
                FullyQualifiedName = vNode.GetFullyQualifiedName()
                IsPartial = vNode.IsPartial
            End If
        End Sub
        
        ''' <summary>
        ''' Returns a string representation of the definition location
        ''' </summary>
        ''' <returns>String in format "FilePath:Line:Column"</returns>
        Public Overrides Function ToString() As String
            Return $"{FilePath}:{Line + 1}:{Column + 1}"
        End Function
        
    End Class
    
End Namespace