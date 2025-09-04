
' Syntax/Token.vb - Represents a token in VB.NET source code
' Created: 2025-08-29

Imports System

Namespace Syntax


    
    ''' <summary>
    ''' Represents a single token in VB.NET source code
    ''' </summary>
    Public Class Token
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets or sets the type of the token
        ''' </summary>
        ''' <value>The TokenType enumeration value</value>
        Public Property Type As TokenType
        
        ''' <summary>
        ''' Gets or sets the text content of the token
        ''' </summary>
        ''' <value>The actual text from the source code</value>
        Public Property Text As String
        
        ''' <summary>
        ''' Gets or sets the starting column position of the token
        ''' </summary>
        ''' <value>Zero-based column index where the token starts</value>
        Public Property StartColumn As Integer
        
        ''' <summary>
        ''' Gets or sets the ending column position of the token
        ''' </summary>
        ''' <value>Zero-based column index where the token ends</value>
        Public Property EndColumn As Integer
        
        ' ===== Constructors =====
        
        ''' <summary>
        ''' Initializes a new instance of the Token class
        ''' </summary>
        Public Sub New()
            Type = TokenType.eUnspecified
            Text = String.Empty
            StartColumn = 0
            EndColumn = 0
        End Sub
        
        ''' <summary>
        ''' Initializes a new instance of the Token class with specified values
        ''' </summary>
        ''' <param name="vType">The type of the token</param>
        ''' <param name="vText">The text content of the token</param>
        ''' <param name="vStartColumn">The starting column position</param>
        ''' <param name="vEndColumn">The ending column position</param>
        Public Sub New(vType As TokenType, vText As String, vStartColumn As Integer, vEndColumn As Integer)
            Type = vType
            Text = If(vText, String.Empty)
            StartColumn = vStartColumn
            EndColumn = vEndColumn
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Gets the length of the token
        ''' </summary>
        ''' <returns>The number of characters in the token</returns>
        Public Function GetLength() As Integer
            Return EndColumn - StartColumn + 1
        End Function
        
        ''' <summary>
        ''' Checks if the token contains a specific position
        ''' </summary>
        ''' <param name="vColumn">The column position to check</param>
        ''' <returns>True if the position is within the token, False otherwise</returns>
        Public Function ContainsColumn(vColumn As Integer) As Boolean
            Return vColumn >= StartColumn AndAlso vColumn <= EndColumn
        End Function
        
        ''' <summary>
        ''' Returns a string representation of the token
        ''' </summary>
        ''' <returns>A string containing token information</returns>
        Public Overrides Function ToString() As String
            Return $"{Type}: '{Text}' [{StartColumn}-{EndColumn}]"
        End Function
        
        ''' <summary>
        ''' Creates a copy of this token
        ''' </summary>
        ''' <returns>A new Token instance with the same values</returns>
        Public Function Clone() As Token
            Return New Token(Type, Text, StartColumn, EndColumn)
        End Function
        
    End Class
    
    ''' <summary>
    ''' Specifies the type of a token
    ''' </summary>
    Public Enum TokenType
        ''' <summary>Unknown or unspecified token type</summary>
        eUnspecified
        ''' <summary>VB.NET keyword (e.g., Dim, If, Then, Class)</summary>
        eKeyword
        ''' <summary>Identifier (variable, method, class name, etc.)</summary>
        eIdentifier
        ''' <summary>String literal enclosed in quotes</summary>
        eStringLiteral
        ''' <summary>Numeric literal (integer, decimal, hex, etc.)</summary>
        eNumber
        ''' <summary>Comment starting with apostrophe or REM</summary>
        eComment
        ''' <summary>Operator (+, -, =, <>, etc.)</summary>
        eOperator
        ''' <summary>Built-in type name (Integer, String, Boolean, etc.)</summary>
        eType
        ''' <summary>Whitespace characters (space, tab, etc.)</summary>
        eWhitespace
        ''' <summary>Other/unknown token</summary>
        eOther
        ''' <summary>Sentinel value for enum bounds checking</summary>
        eLastValue
    End Enum
    
End Namespace