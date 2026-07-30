' Syntax/VBTokenizer.vb - VB.NET code tokenizer for syntax highlighting
' Created: 2025-08-29

Imports System
Imports System.Collections.Generic
Imports System.Text

Namespace Syntax
    
    ''' <summary>
    ''' Tokenizes VB.NET source code into syntactic tokens for parsing and highlighting
    ''' </summary>
    Public Class VBTokenizer
        
        ' ===== Private Fields =====
        Private pKeywords As HashSet(Of String)
        Private pTypes As HashSet(Of String)
        Private pOperators As HashSet(Of String)
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Initializes a new instance of the VBTokenizer class
        ''' </summary>
        Public Sub New()
            InitializeKeywords()
            InitializeTypes()
            InitializeOperators()
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Tokenizes a line of VB.NET code
        ''' </summary>
        ''' <param name="vLine">The line of code to tokenize</param>
        ''' <returns>List of tokens found in the line</returns>
        Public Function TokenizeLine(vLine As String) As List(Of Token)
            Dim lTokens As New List(Of Token)()
            
            Try
                If String.IsNullOrEmpty(vLine) Then Return lTokens
                
                Dim i As Integer = 0
                Dim lLength As Integer = vLine.Length
                
                While i < lLength
                    Dim lStartPos As Integer = i
                    
                    ' Skip whitespace (but track it for accurate column positions)
                    If Char.IsWhiteSpace(vLine(i)) Then
                        While i < lLength AndAlso Char.IsWhiteSpace(vLine(i))
                            i += 1
                        End While
                        ' Create whitespace token for position tracking
                        lTokens.Add(New Token(TokenType.eWhitespace, vLine.Substring(lStartPos, i - lStartPos), lStartPos, i - 1))
                        Continue While
                    End If
                    
                    ' Check for comment
                    If vLine(i) = "'"c Then
                        ' Rest of line is a comment
                        Dim lCommentText As String = vLine.Substring(i)
                        lTokens.Add(New Token(TokenType.eComment, lCommentText, i, vLine.Length - 1))
                        Exit While
                    End If
                    
                    ' Check for REM comment
                    If i + 2 < lLength AndAlso vLine.Substring(i, 3).Equals("REM", StringComparison.OrdinalIgnoreCase) Then
                        If i = 0 OrElse Char.IsWhiteSpace(vLine(i - 1)) Then
                            If i + 3 >= lLength OrElse Char.IsWhiteSpace(vLine(i + 3)) Then
                                ' Rest of line after REM is a comment
                                Dim lCommentText As String = vLine.Substring(i)
                                lTokens.Add(New Token(TokenType.eComment, lCommentText, i, vLine.Length - 1))
                                Exit While
                            End If
                        End If
                    End If
                    
                    ' Check for string literal
                    If vLine(i) = """"c Then
                        Dim lStringStart As Integer = i
                        i += 1
                        
                        ' Find end of string (handle escaped quotes)
                        While i < lLength
                            If vLine(i) = """"c Then
                                If i + 1 < lLength AndAlso vLine(i + 1) = """"c Then
                                    ' Escaped quote
                                    i += 2
                                Else
                                    ' End of string
                                    i += 1
                                    Exit While
                                End If
                            Else
                                i += 1
                            End If
                        End While
                        
                        Dim lStringText As String = vLine.Substring(lStringStart, i - lStringStart)
                        lTokens.Add(New Token(TokenType.eStringLiteral, lStringText, lStringStart, i - 1))
                        Continue While
                    End If
                    
                    ' Check for date literal #
                    If vLine(i) = "#"c Then
                        Dim lDateStart As Integer = i
                        i += 1
                        
                        ' Find end of date literal
                        While i < lLength AndAlso vLine(i) <> "#"c
                            i += 1
                        End While
                        
                        If i < lLength AndAlso vLine(i) = "#"c Then
                            i += 1
                        End If
                        
                        Dim lDateText As String = vLine.Substring(lDateStart, i - lDateStart)
                        lTokens.Add(New Token(TokenType.eStringLiteral, lDateText, lDateStart, i - 1))
                        Continue While
                    End If
                    
                    ' Check for number
                    If Char.IsDigit(vLine(i)) OrElse 
                       (vLine(i) = "."c AndAlso i + 1 < lLength AndAlso Char.IsDigit(vLine(i + 1))) OrElse
                       (vLine(i) = "&"c AndAlso i + 1 < lLength AndAlso "HhOoBb".Contains(vLine(i + 1))) Then
                        
                        Dim lNumStart As Integer = i
                        
                        ' Handle hex/octal/binary literals
                        If vLine(i) = "&"c AndAlso i + 1 < lLength Then
                            Select Case Char.ToUpper(vLine(i + 1))
                                Case "H"c ' Hex
                                    i += 2
                                    While i < lLength AndAlso "0123456789ABCDEFabcdef".Contains(vLine(i))
                                        i += 1
                                    End While
                                Case "O"c ' Octal
                                    i += 2
                                    While i < lLength AndAlso "01234567".Contains(vLine(i))
                                        i += 1
                                    End While
                                Case "B"c ' Binary
                                    i += 2
                                    While i < lLength AndAlso "01".Contains(vLine(i))
                                        i += 1
                                    End While
                            End Select
                        Else
                            ' Regular number (including decimals and scientific notation)
                            Dim lHasDecimal As Boolean = False
                            Dim lHasExponent As Boolean = False
                            
                            While i < lLength
                                If Char.IsDigit(vLine(i)) Then
                                    i += 1
                                ElseIf vLine(i) = "."c AndAlso Not lHasDecimal AndAlso Not lHasExponent Then
                                    lHasDecimal = True
                                    i += 1
                                ElseIf (vLine(i) = "E"c OrElse vLine(i) = "e"c) AndAlso Not lHasExponent Then
                                    lHasExponent = True
                                    i += 1
                                    If i < lLength AndAlso (vLine(i) = "+"c OrElse vLine(i) = "-"c) Then
                                        i += 1
                                    End If
                                Else
                                    Exit While
                                End If
                            End While
                            
                            ' Check for type suffix (L, F, D, S, I, UI, UL, US, R, C, @)
                            If i < lLength Then
                                Select Case Char.ToUpper(vLine(i))
                                    Case "L"c, "F"c, "D"c, "S"c, "I"c, "R"c, "C"c, "@"c
                                        i += 1
                                    Case "U"c
                                        If i + 1 < lLength Then
                                            Select Case Char.ToUpper(vLine(i + 1))
                                                Case "I"c, "L"c, "S"c
                                                    i += 2
                                            End Select
                                        End If
                                End Select
                            End If
                        End If
                        
                        Dim lNumText As String = vLine.Substring(lNumStart, i - lNumStart)
                        lTokens.Add(New Token(TokenType.eNumber, lNumText, lNumStart, i - 1))
                        Continue While
                    End If
                    
                    ' Check for escaped identifier [Something]
                    If vLine(i) = "["c Then
                        Dim lIdStart As Integer = i
                        i += 1
                        
                        While i < lLength AndAlso vLine(i) <> "]"c
                            i += 1
                        End While
                        
                        If i < lLength AndAlso vLine(i) = "]"c Then
                            i += 1
                        End If
                        
                        Dim lIdText As String = vLine.Substring(lIdStart, i - lIdStart)
                        lTokens.Add(New Token(TokenType.eIdentifier, lIdText, lIdStart, i - 1))
                        Continue While
                    End If
                    
                    ' Check for identifier or keyword
                    If Char.IsLetter(vLine(i)) OrElse vLine(i) = "_"c Then
                        Dim lIdStart As Integer = i
                        
                        ' Read identifier
                        While i < lLength AndAlso (Char.IsLetterOrDigit(vLine(i)) OrElse vLine(i) = "_"c)
                            i += 1
                        End While
                        
                        Dim lIdText As String = vLine.Substring(lIdStart, i - lIdStart)
                        
                        ' Determine token type
                        Dim lTokenType As TokenType = TokenType.eIdentifier
                        
                        If IsKeyword(lIdText) Then
                            lTokenType = TokenType.eKeyword
                        ElseIf IsType(lIdText) Then
                            lTokenType = TokenType.eType
                        End If
                        
                        lTokens.Add(New Token(lTokenType, lIdText, lIdStart, i - 1))
                        Continue While
                    End If
                    
                    ' Check for operators (including multi-character operators)
                    If IsOperatorChar(vLine(i)) Then
                        Dim lOpStart As Integer = i
                        Dim lOpText As String = ""
                        
                        ' Check for multi-character operators
                        If i + 1 < lLength Then
                            Dim lTwoChar As String = vLine.Substring(i, 2)
                            Select Case lTwoChar
                                Case "<=", ">=", "<>", ":=", "<<", ">>", "\\"
                                    lOpText = lTwoChar
                                    i += 2
                            End Select
                        End If
                        
                        ' If not a multi-char operator, just take single char
                        If lOpText = "" Then
                            lOpText = vLine(i).ToString()
                            i += 1
                        End If
                        
                        lTokens.Add(New Token(TokenType.eOperator, lOpText, lOpStart, i - 1))
                        Continue While
                    End If
                    
                    ' Unknown character - treat as other
                    lTokens.Add(New Token(TokenType.eOther, vLine(i).ToString(), i, i))
                    i += 1
                End While
                
            Catch ex As Exception
                Console.WriteLine($"VBTokenizer.TokenizeLine error: {ex.Message}")
            End Try
            
            Return lTokens
        End Function
        
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Initializes the VB.NET keywords set
        ''' </summary>
        Private Sub InitializeKeywords()
            pKeywords = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "AddHandler", "AddressOf", "Alias", "and", "AndAlso", "As", "Async", "Await",
                "ByRef", "ByVal", "Call", "Case", "Catch", "Class", "Const", "Continue", 
                "CType", "CUInt", "CULng", "CUShort", "CSByte", "CBool", "CDate", "CObj", "CStr",
                "CDbl", "CDec", "CInt", "CLng", "CSng", "CShort", "CChar", "CByte",
                "Declare", "Default", "Delegate", "Dim", "DirectCast", "Do", "each", "Else", 
                "ElseIf", "End", "EndIf", "Enum", "Erase", "error", "Event", "Exit", "False", 
                "Finally", "for", "Friend", "Function", "Get", "GetType", "GetXMLNamespace", 
                "Global", "GoSub", "GoTo", "Handles", "If", "Implements", "Imports", "in", 
                "Inherits", "Interface", "Is", "IsNot", "Iterator", "Let", "Lib", "Like", 
                "Loop", "Me", "Mod", "Module", "MustInherit", "MustOverride", "MyBase", 
                "MyClass", "NameOf", "Namespace", "Narrowing", "New", "Next", "Not", "Nothing", 
                "NotInheritable", "NotOverridable", "Of", "On", "Operator", "Option", 
                "Optional", "Or", "OrElse", "Out", "Overloads", "Overridable", "Overrides", 
                "ParamArray", "Partial", "Private", "Property", "Protected", "Public", 
                "RaiseEvent", "ReadOnly", "ReDim", "REM", "RemoveHandler", "Resume", "Return", 
                "Select", "Set", "Shadows", "Shared", "Skip", "Static", "Step", "Stop", 
                "Structure", "Sub", "SyncLock", "Take", "Then", "Throw", "To", "True", "Try", 
                "TryCast", "TypeOf", "Using", "When", "Where", "While", "Widening", "with", 
                "WithEvents", "WriteOnly", "Xor", "Yield"
            }
        End Sub
        
        ''' <summary>
        ''' Initializes the VB.NET built-in types set
        ''' </summary>
        Private Sub InitializeTypes()
            pTypes = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "Boolean", "Byte", "Char", "Date", "DateTime", "Decimal", "Double", 
                "Integer", "Long", "Object", "SByte", "Short", "Single", "String", 
                "UInteger", "ULong", "UShort", "Variant", "Void"
            }
        End Sub
        
        ''' <summary>
        ''' Initializes the operators set
        ''' </summary>
        Private Sub InitializeOperators()
            pOperators = New HashSet(Of String)() From {
                "+", "-", "*", "/", "\", "^", "=", "<", ">", "&", 
                "<=", ">=", "<>", ":=", "<<", ">>", "\\",
                "(", ")", "{", "}", "[", "]", ",", ".", ":", ";", "?", "!"
            }
        End Sub
        
        ''' <summary>
        ''' Checks if a word is a VB.NET keyword
        ''' </summary>
        ''' <param name="vWord">The word to check</param>
        ''' <returns>True if the word is a keyword, False otherwise</returns>
        Private Function IsKeyword(vWord As String) As Boolean
            Return pKeywords.Contains(vWord)
        End Function
        
        ''' <summary>
        ''' Checks if a word is a VB.NET built-in type
        ''' </summary>
        ''' <param name="vWord">The word to check</param>
        ''' <returns>True if the word is a type, False otherwise</returns>
        Private Function IsType(vWord As String) As Boolean
            Return pTypes.Contains(vWord)
        End Function
        
        ''' <summary>
        ''' Checks if a character is an operator character
        ''' </summary>
        ''' <param name="vChar">The character to check</param>
        ''' <returns>True if the character could be part of an operator, False otherwise</returns>
        Private Function IsOperatorChar(vChar As Char) As Boolean
            Select Case vChar
                Case "+"c, "-"c, "*"c, "/"c, "\"c, "^"c, "="c, "<"c, ">"c, "&"c,
                     "("c, ")"c, "{"c, "}"c, "["c, "]"c, ","c, "."c, ":"c, ";"c, "?"c, "!"c
                    Return True
                Case Else
                    Return False
            End Select
        End Function
        
    End Class
    
End Namespace