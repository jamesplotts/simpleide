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
        Public Property ParseState As LineParseState
        
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
            ' DON'T clear SyntaxTokens - preserve them until new parsing completes!
            ' SyntaxTokens.Clear()  ' <-- REMOVED: This was causing loss of coloring
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

        ''' <summary>
        ''' Returns a byte array with encoded CharacterToken values for this line
        ''' </summary>
        ''' <param name="vLineLength">The length of the line text this metadata represents</param>
        ''' <returns>Byte array with encoded token information for each character position</returns>
        ''' <remarks>
        ''' This method encodes the SyntaxTokens list into a byte array where each byte
        ''' represents the token type and style information for a character position.
        ''' If no syntax tokens are available, returns an array filled with default tokens.
        ''' </remarks>
        Public Function GetEncodedTokens(vLineLength As Integer) As Byte()
            Try
                ' Validate line length
                If vLineLength <= 0 Then
                    Return New Byte() {}
                End If
                
                ' Create the result array
                Dim lResult(vLineLength - 1) As Byte
                
                ' First, initialize all positions with default token
                Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                for i As Integer = 0 To vLineLength - 1
                    lResult(i) = lDefaultToken
                Next
                
                ' If we have no syntax tokens, return the default array
                If SyntaxTokens Is Nothing OrElse SyntaxTokens.Count = 0 Then
                    Return lResult
                End If
                
                ' Apply each syntax token to the appropriate character positions
                for each lToken As SyntaxToken in SyntaxTokens
                    ' Validate token bounds
                    If lToken Is Nothing Then Continue for
                    
                    ' Calculate the actual start and end positions
                    Dim lStartPos As Integer = Math.Max(0, lToken.StartColumn)
                    Dim lEndPos As Integer = Math.Min(vLineLength - 1, lToken.StartColumn + lToken.Length - 1)
                    
                    ' Skip if token is completely out of bounds
                    If lStartPos >= vLineLength OrElse lEndPos < 0 Then
                        Continue for
                    End If
                    
                    ' Encode the token with its type and style information
                    Dim lEncodedToken As Byte = CharacterToken.Encode(
                        lToken.TokenType,
                        lToken.IsBold,
                        lToken.IsItalic
                    )
                    
                    ' Apply the encoded token to each character in its range
                    for i As Integer = lStartPos To lEndPos
                        If i >= 0 AndAlso i < vLineLength Then
                            lResult(i) = lEncodedToken
                        End If
                    Next
                Next
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"LineMetadata.GetEncodedTokens error: {ex.Message}")
                
                ' Return default tokens array on error
                If vLineLength > 0 Then
                    Dim lDefaultArray(vLineLength - 1) As Byte
                    Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                    for i As Integer = 0 To vLineLength - 1
                        lDefaultArray(i) = lDefaultToken
                    Next
                    Return lDefaultArray
                Else
                    Return New Byte() {}
                End If
            End Try
        End Function

        ''' <summary>
        ''' Gets or sets whether this line starts a foldable region
        ''' </summary>
        Public Property IsFoldStart As Boolean = False
        
        ''' <summary>
        ''' Gets or sets the line number where the fold region ends (if IsFoldStart is True)
        ''' </summary>
        Public Property FoldEndLine As Integer = -1
        
        ''' <summary>
        ''' Gets or sets whether this line is currently folded (collapsed)
        ''' </summary>
        Public Property IsFolded As Boolean = False
        
        ''' <summary>
        ''' Gets or sets whether this line is hidden due to a parent fold
        ''' </summary>
        Public Property IsHiddenByFold As Boolean = False
        
        ''' <summary>
        ''' Gets or sets the fold level (nesting depth)
        ''' </summary>
        Public Property FoldLevel As Integer = 0
        
        ''' <summary>
        ''' Gets or sets whether this line is a #Region directive
        ''' </summary>
        Public Property IsRegion As Boolean = False
        
        ''' <summary>
        ''' Gets or sets the region name (if IsRegion is True)
        ''' </summary>
        Public Property RegionName As String = ""
        
        ''' <summary>
        ''' Gets or sets whether this line is an #End Region directive
        ''' </summary>
        Public Property IsEndRegion As Boolean = False
        
        ''' <summary>
        ''' Gets or sets the type of fold (for icon display)
        ''' </summary>
        Public Property FoldType As FoldRegionType = FoldRegionType.eUnspecified
        
        ''' <summary>
        ''' Types of foldable regions
        ''' </summary>
        Public Enum FoldRegionType
            eUnspecified
            eClass
            eMethod
            eProperty
            eRegion
            eNamespace
            eInterface
            eModule
            eStructure
            eEnum
            eIfBlock
            eForLoop
            eWhileLoop
            eDoLoop
            eTryBlock
            eSelectBlock
            eWithBlock
            eUsingBlock
            eLastValue
        End Enum
        
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

    ''' <summary>
    ''' Represents the parse state of a line
    ''' </summary>
    Public Enum LineParseState
        ''' <summary>Unknown or unspecified state</summary>
        eUnspecified
        ''' <summary>Line has not been parsed yet</summary>
        eUnparsed
        ''' <summary>Line is currently being parsed</summary>
        eParsing
        ''' <summary>Line has been successfully parsed</summary>
        eParsed
        ''' <summary>Line had errors during parsing</summary>
        eError
        ''' <summary>Sentinel value for enum bounds checking</summary>
        eLastValue
    End Enum
    
End Namespace
