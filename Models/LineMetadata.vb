' Models/LineMetadata.vb - Metadata for syntax highlighting and parsing per line
Imports System
Imports System.Collections.Generic

Namespace Models
    
    ' Metadata stored for each line of text
    Public Class LineMetadata
        
        Public Property SyntaxTokens As List(Of SyntaxToken)
        Public Property FoldingLevel As Integer
        Public Property NodeReferences As List(Of NodeReference)
        Public Property ParseErrors As List(Of ParseError)
        Public Property IsChanged As Boolean
        Public Property LineHash As Integer  ' To detect Content Changes
        Public Property HasHighlighting As Boolean = True
        
        Public Sub New()
            SyntaxTokens = New List(Of SyntaxToken)()
            NodeReferences = New List(Of NodeReference)()
            ParseErrors = New List(Of ParseError)()
            IsChanged = True
            FoldingLevel = 0
            LineHash = 0
        End Sub
        
        ' Mark this line as needing reparsing
        Public Sub MarkChanged()
            IsChanged = True
            SyntaxTokens.Clear()
            ParseErrors.Clear()
            ' Keep NodeReferences - they'll be updated by document parser
        End Sub
        
        ' Calculate hash of line content for change detection
        Public Sub UpdateHash(vLineText As String)
            LineHash = If(vLineText?.GetHashCode(), 0)
        End Sub
        
        ' Check if line content has changed
        Public Function HasContentChanged(vLineText As String) As Boolean
            Dim lNewHash As Integer = If(vLineText?.GetHashCode(), 0)
            Return lNewHash <> LineHash
        End Function
        
    End Class
    
    ' Represents a colored/styled token within a line
    ' NOTE: Moved to CodeTypes.vb to avoid duplication
    ' Public Class SyntaxToken - see CodeTypes.vb
    
    ' Reference to a node in the document node graph
    Public Class NodeReference
        
        Public Property NodeId As String
        Public Property NodeType As CodeNodeType
        Public Property StartColumn As Integer
        Public Property EndColumn As Integer
        Public Property IsDefinition As Boolean  ' True if this Line defines the Node
        
        Public Sub New(vNodeId As String, vNodeType As CodeNodeType, vStartColumn As Integer, vEndColumn As Integer, vIsDefinition As Boolean)
            NodeId = vNodeId
            NodeType = vNodeType
            StartColumn = vStartColumn
            EndColumn = vEndColumn
            IsDefinition = vIsDefinition
        End Sub
        
    End Class
    
    ' Types of parse errors
    Public Enum ParseErrorType
        eUnspecified
        eSyntaxError
        eUnmatchedBracket
        eUnterminatedString
        eInvalidKeyword
        eMissingEndStatement
        eInvalidIdentifier
        eLastValue
    End Enum
    
    ' Error severity levels
    Public Enum ErrorSeverity
        eUnspecified
        eInfo
        eWarning
        eError
        eLastValue
    End Enum
    
End Namespace
