' Models/SyntaxToken.vb - Syntax highlighting token (moved from CodeTypes.vb duplicate)
Imports System

Namespace Models
    
    ' Represents a syntax highlighted token
    Public Class SyntaxToken
        
        Public Property StartColumn As Integer = 0
        Public Property Length As Integer = 0
        Public Property TokenType As SyntaxTokenType = SyntaxTokenType.eNormal
        Public Property Color As String = "#000000"
        Public Property IsBold As Boolean = False
        Public Property IsItalic As Boolean = False
        Public Property IsUnderline As Boolean = False
        
        Public Sub New()
        End Sub
        
        Public Sub New(vStartColumn As Integer, vLength As Integer, vTokenType As SyntaxTokenType)
            StartColumn = vStartColumn
            Length = vLength
            TokenType = vTokenType
        End Sub
        
        Public Sub New(vStartColumn As Integer, vLength As Integer, vTokenType As SyntaxTokenType, vColor As String)
            StartColumn = vStartColumn
            Length = vLength
            TokenType = vTokenType
            Color = vColor
        End Sub
        
        Public ReadOnly Property EndColumn As Integer
            Get
                Return StartColumn + Length
            End Get
        End Property
        
    End Class

    ' Syntax token types
    Public Enum SyntaxTokenType
        eNormal
        eKeyword
        eString
        eComment
        eNumber
        eOperator
        eIdentifier
        eType
    End Enum

    
End Namespace
