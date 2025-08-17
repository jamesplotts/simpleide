' VBParser.FieldParsing.vb - Enhanced field parsing that correctly handles underscores and array literals
' This partial class file fixes field parsing issues in VBParser
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models

' VBParser.FieldParsing.vb
' Created: 2025-08-14 08:50:42

Namespace Syntax
    
    Partial Public Class VBParser
        
        ''' <summary>
        ''' Enhanced ExtractFieldName that properly handles underscore-prefixed names
        ''' </summary>
        Private Function ExtractFieldName(vLine As String) As String
            Try
                ' Remove leading modifiers more comprehensively
                Dim lLine As String = vLine.Trim()
                
                ' Remove all known modifiers in order
                Dim lModifiers As String() = {
                    "Private Shared ReadOnly ", "Public Shared ReadOnly ", "Protected Shared ReadOnly ", "Friend Shared ReadOnly ",
                    "Private Shared ", "Public Shared ", "Protected Shared ", "Friend Shared ",
                    "Private ReadOnly ", "Public ReadOnly ", "Protected ReadOnly ", "Friend ReadOnly ",
                    "Private WithEvents ", "Public WithEvents ", "Protected WithEvents ", "Friend WithEvents ",
                    "Private Shadows ", "Public Shadows ", "Protected Shadows ", "Friend Shadows ",
                    "Private ", "Public ", "Protected ", "Friend ",
                    "Shared ReadOnly ", "Shared ", "ReadOnly ", "WithEvents ", "Shadows ",
                    "Dim ", "Const "
                }
                
                For Each lModifier In lModifiers
                    If lLine.StartsWith(lModifier, StringComparison.OrdinalIgnoreCase) Then
                        lLine = lLine.Substring(lModifier.Length).Trim()
                        Exit For ' Only remove one modifier phrase
                    End If
                Next
                
                ' Now extract the field name
                ' Field name is everything before: As, =, (, or comma
                Dim lAsIndex As Integer = lLine.IndexOf(" As ", StringComparison.OrdinalIgnoreCase)
                Dim lEqualsIndex As Integer = lLine.IndexOf("="c)
                Dim lParenIndex As Integer = lLine.IndexOf("("c)
                Dim lCommaIndex As Integer = lLine.IndexOf(","c)
                
                ' Find the earliest delimiter
                Dim lEndIndex As Integer = lLine.Length
                If lAsIndex > 0 AndAlso lAsIndex < lEndIndex Then lEndIndex = lAsIndex
                If lEqualsIndex > 0 AndAlso lEqualsIndex < lEndIndex Then lEndIndex = lEqualsIndex
                If lParenIndex > 0 AndAlso lParenIndex < lEndIndex Then lEndIndex = lParenIndex
                If lCommaIndex > 0 AndAlso lCommaIndex < lEndIndex Then lEndIndex = lCommaIndex
                
                If lEndIndex > 0 Then
                    Dim lFieldName As String = lLine.Substring(0, lEndIndex).Trim()
                    
                    ' Validate the field name - must be a valid identifier
                    If IsValidFieldName(lFieldName) Then
                        Return lFieldName
                    End If
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"ExtractFieldNameFixed error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Check if a string is a valid field name (identifier)
        ''' </summary>
        Private Function IsValidFieldName(vName As String) As Boolean
            Try
                If String.IsNullOrEmpty(vName) Then Return False
                
                ' Must start with letter or underscore
                If Not (Char.IsLetter(vName(0)) OrElse vName(0) = "_"c) Then
                    Return False
                End If
                
                ' Rest must be letters, digits, or underscores
                For i As Integer = 1 To vName.Length - 1
                    If Not (Char.IsLetterOrDigit(vName(i)) OrElse vName(i) = "_"c) Then
                        Return False
                    End If
                Next
                
                ' Don't accept string literals as field names
                If vName.StartsWith("""") OrElse vName.EndsWith("""") Then
                    Return False
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidFieldName error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced ParseFieldMember that doesn't parse array content as separate fields
        ''' </summary>
        Private Function ParseFieldMemberFixed(vParentNode As SyntaxNode, vLineIndex As Integer, vLine As String, vModifiers As List(Of String)) As Integer
            Try
                ' Check if this line is a continuation of an array initializer
                If IsArrayContinuation(vLine) Then
                    ' This is array content, not a new field
                    Return vLineIndex
                End If
                
                ' Extract field name from declaration
                Dim lName As String = ExtractFieldName(vLine)
                If String.IsNullOrEmpty(lName) Then Return vLineIndex
                
                Dim lFieldNode As New SyntaxNode(CodeNodeType.eField, lName)
                lFieldNode.StartLine = vLineIndex
                
                ' Check if this is a multi-line field declaration (array initializer)
                lFieldNode.EndLine = FindFieldEnd(vLineIndex)
                
                StoreFileInfo(lFieldNode)
                SetModifiers(lFieldNode, String.Join(" ", vModifiers))
                
                ' Parse field type if present
                ParseFieldType(lFieldNode, vLine)
                
                vParentNode.AddChild(lFieldNode)
                'Console.WriteLine($"    Added field: {lName}")
                
                Return lFieldNode.EndLine
                
            Catch ex As Exception
                Console.WriteLine($"ParseFieldMemberFixed error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        ''' <summary>
        ''' Check if a line is part of an array initializer continuation
        ''' </summary>
        Private Function IsArrayContinuation(vLine As String) As Boolean
            Try
                Dim lTrimmed As String = vLine.Trim()
                
                ' Lines that start with string literals or continuation patterns
                If lTrimmed.StartsWith("""") Then Return True
                If lTrimmed.StartsWith("_") Then Return True ' Line continuation
                If lTrimmed.StartsWith("}") Then Return True ' End of array initializer
                If lTrimmed.StartsWith(",") Then Return True ' Continuation in array
                
                ' Lines that are just operators or literals in quotes
                If Regex.IsMatch(lTrimmed, "^""[^""]+""(?:\s*,\s*""[^""]+"")*\s*[,}]?\s*$") Then
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsArrayContinuation error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Find the end of a field declaration (handling multi-line array initializers)
        ''' </summary>
        Private Function FindFieldEnd(vStartLine As Integer) As Integer
            Try
                Dim lStartLineText As String = pLines(vStartLine)
                
                ' Check if this line has an array initializer start
                If lStartLineText.Contains("= {") Then
                    ' Find the closing brace
                    Dim lBraceLevel As Integer = 0
                    
                    ' Count braces on the start line
                    For Each lChar In lStartLineText
                        If lChar = "{"c Then lBraceLevel += 1
                        If lChar = "}"c Then lBraceLevel -= 1
                    Next
                    
                    ' If braces are balanced, it's a single-line array
                    If lBraceLevel = 0 Then Return vStartLine
                    
                    ' Otherwise, look for the closing brace
                    For i As Integer = vStartLine + 1 To pLines.Length - 1
                        Dim lLine As String = pLines(i)
                        
                        For Each lChar In lLine
                            If lChar = "{"c Then lBraceLevel += 1
                            If lChar = "}"c Then lBraceLevel -= 1
                        Next
                        
                        If lBraceLevel = 0 Then
                            Return i
                        End If
                    Next
                End If
                
                ' Single-line field
                Return vStartLine
                
            Catch ex As Exception
                Console.WriteLine($"FindFieldEnd error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced ParseMember that uses the fixed field parsing
        ''' </summary>
        Private Function ParseMemberEnhanced(vParentNode As SyntaxNode, vLineIndex As Integer) As Integer
            Try
                Dim lLine As String = pLines(vLineIndex)
                Dim lTrimmedLine As String = lLine.Trim()
                
                ' Skip empty lines and comments
                If String.IsNullOrWhiteSpace(lTrimmedLine) OrElse lTrimmedLine.StartsWith("'") Then
                    Return vLineIndex
                End If
                
                ' Skip array continuation lines
                If IsArrayContinuation(lTrimmedLine) Then
                    Return vLineIndex
                End If
                
                ' Split into words
                Dim lWords As String() = lTrimmedLine.Split({" "c, vbTab, "("c}, StringSplitOptions.RemoveEmptyEntries)
                If lWords.Length = 0 Then Return vLineIndex
                
                Dim lIndex As Integer = 0
                Dim lModifiers As New List(Of String)()
                
                ' Collect modifiers
                While lIndex < lWords.Length
                    Select Case lWords(lIndex).ToUpper()
                        Case "PUBLIC", "PRIVATE", "FRIEND", "PROTECTED", "SHARED", _
                             "OVERRIDES", "OVERRIDABLE", "MUSTOVERRIDE", "NOTOVERRIDABLE", _
                             "READONLY", "WRITEONLY", "WITHEVENTS", "DIM", "SHADOWS", _
                             "OVERLOADS", "STATIC", "ASYNC", "ITERATOR"
                            lModifiers.Add(lWords(lIndex))
                            lIndex += 1
                        Case Else
                            Exit While
                    End Select
                End While
                
                If lIndex >= lWords.Length Then Return vLineIndex
                
                ' Check member type and parse accordingly
                Select Case lWords(lIndex).ToUpper()
                    Case "SUB"
                        Return ParseSubMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "FUNCTION"
                        Return ParseFunctionMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "PROPERTY"
                        Return ParsePropertyMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "EVENT"
                        Return ParseEventMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "CONST"
                        Return ParseConstMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "DELEGATE"
                        Return ParseDelegateMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "OPERATOR"
                        Return ParseOperatorMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case Else
                        ' Check if it's a field declaration or enum value
                        If vParentNode.NodeType = CodeNodeType.eEnum Then
                            Return ParseEnumValue(vParentNode, vLineIndex, lTrimmedLine)
                        ElseIf lTrimmedLine.Contains(" As ") OrElse lTrimmedLine.Contains("=") Then
                            ' Use the fixed field parser
                            Return ParseFieldMemberFixed(vParentNode, vLineIndex, lTrimmedLine, lModifiers)
                        End If
                End Select
                
                Return vLineIndex
                
            Catch ex As Exception
                Console.WriteLine($"ParseMemberEnhanced error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
    End Class
    
End Namespace
