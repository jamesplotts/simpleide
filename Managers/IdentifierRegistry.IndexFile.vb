' Managers/IdentifierRegistry.IndexFile.vb - Fixed IndexFile method for IdentifierRegistry
' This partial class file replaces the IndexFile method to properly handle multi-line strings

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Text
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

' IdentifierRegistry.IndexFile.vb
' Created: 2025-08-09 23:36:22

Namespace Managers
    
    Partial Public Class IdentifierRegistry
        
        ''' <summary>
        ''' Parse and index a file with proper multi-line string tracking
        ''' </summary>
        Public Sub IndexFile(vFilePath As String, Optional vDocumentModel As DocumentModel = Nothing)
            Try
                Dim lContent As String
                Dim lLines() As String
                
                If vDocumentModel IsNot Nothing Then
                    lContent = vDocumentModel.GetAllText()
                Else
                    lContent = System.IO.File.ReadAllText(vFilePath)
                End If
                
                lLines = lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                
                ' Reset multi-line string tracking for this file
                pMultiLineStringsByFile(vFilePath) = False
                
                ' Track whether we're in a multi-line string as we parse
                Dim lInMultiLineString As Boolean = False
                
                ' Parse declarations and references with multi-line string awareness
                For i As Integer = 0 To lLines.Length - 1
                    Dim lLine As String = lLines(i)
                    
                    ' Check if we're starting this line inside a multi-line string
                    If lInMultiLineString Then
                        ' We're inside a multi-line string from a previous line
                        ' Check if this line ends the string
                        Dim lQuoteCount As Integer = 0
                        Dim j As Integer = 0
                        
                        While j < lLine.Length
                            If lLine(j) = """"c Then
                                ' Check if it's escaped
                                If j + 1 < lLine.Length AndAlso lLine(j + 1) = """"c Then
                                    j += 2 ' Skip escaped quote
                                Else
                                    lQuoteCount += 1
                                    j += 1
                                End If
                            Else
                                j += 1
                            End If
                        End While
                        
                        ' If odd number of quotes, the string ends on this line
                        If (lQuoteCount Mod 2) = 1 Then
                            lInMultiLineString = False
                            ' Parse the part of the line after the closing quote
                            ' Find where the string ends
                            Dim lStringEndPos As Integer = FindClosingQuotePosition(lLine)
                            If lStringEndPos >= 0 AndAlso lStringEndPos < lLine.Length - 1 Then
                                ' Parse only the part after the string
                                Dim lPartialLine As String = lLine.Substring(lStringEndPos + 1)
                                ParseLineForDeclarationsOutsideString(vFilePath, lPartialLine, i, lStringEndPos + 1)
                                ParseLineForReferencesOutsideString(vFilePath, lPartialLine, i, lStringEndPos + 1)
                            End If
                        End If
                        ' Skip to next line - we don't parse inside strings
                        Continue For
                    End If
                    
                    ' Not starting inside a multi-line string
                    ' Parse the line, tracking string boundaries
                    Dim lInString As Boolean = False
                    Dim lLastNonStringEnd As Integer = -1
                    Dim lStringStarted As Boolean = False
                    
                    ' Find where strings are on this line
                    Dim j2 As Integer = 0
                    While j2 < lLine.Length
                        Dim lChar As Char = lLine(j2)
                        
                        ' Check for comment (not in string)
                        If lChar = "'"c AndAlso Not lInString Then
                            ' Rest of line is comment - only parse up to here
                            If lLastNonStringEnd = -1 Then
                                ' Parse the whole line up to the comment
                                Dim lPartialLine As String = lLine.Substring(0, j2)
                                ParseLineForDeclarations(vFilePath, lPartialLine, i, vDocumentModel)
                                ParseLineForReferences(vFilePath, lPartialLine, i)
                            End If
                            Exit While
                        End If
                        
                        ' Check for string delimiter
                        If lChar = """"c Then
                            ' Check if it's escaped
                            If j2 + 1 < lLine.Length AndAlso lLine(j2 + 1) = """"c Then
                                j2 += 2 ' Skip escaped quote
                            Else
                                If lInString Then
                                    ' String ends
                                    lInString = False
                                    lLastNonStringEnd = j2
                                Else
                                    ' String starts
                                    lInString = True
                                    lStringStarted = True
                                End If
                                j2 += 1
                            End If
                        Else
                            j2 += 1
                        End If
                    End While
                    
                    ' Check if we ended in a string (multi-line string)
                    If lInString Then
                        lInMultiLineString = True
                        ' Only parse the part before the string started
                        If lStringStarted AndAlso lLastNonStringEnd >= 0 Then
                            Dim lPartialLine As String = lLine.Substring(0, lLastNonStringEnd + 1)
                            ParseLineForDeclarations(vFilePath, lPartialLine, i, vDocumentModel)
                            ParseLineForReferences(vFilePath, lPartialLine, i)
                        ElseIf Not lStringStarted Then
                            ' No string on this line, parse the whole thing
                            ParseLineForDeclarations(vFilePath, lLine, i, vDocumentModel)
                            ParseLineForReferences(vFilePath, lLine, i)
                        End If
                    Else
                        ' Not in a string at end of line - parse normally
                        ParseLineForDeclarations(vFilePath, lLine, i, vDocumentModel)
                        ParseLineForReferences(vFilePath, lLine, i)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"IndexFile error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Find the position of the closing quote in a line
        ''' </summary>
        Private Function FindClosingQuotePosition(vLine As String) As Integer
            Dim i As Integer = 0
            While i < vLine.Length
                If vLine(i) = """"c Then
                    ' Check if it's escaped
                    If i + 1 < vLine.Length AndAlso vLine(i + 1) = """"c Then
                        i += 2 ' Skip escaped quote
                    Else
                        ' Found the closing quote
                        Return i
                    End If
                Else
                    i += 1
                End If
            End While
            Return -1
        End Function
        
        ''' <summary>
        ''' Parse line for declarations with column offset (for partial lines)
        ''' </summary>
        Private Sub ParseLineForDeclarationsOutsideString(vFilePath As String, vPartialLine As String, vLineNumber As Integer, vColumnOffset As Integer)
            Try
                ' Skip empty lines and comments
                Dim lTrimmed As String = vPartialLine.TrimStart()
                If String.IsNullOrEmpty(lTrimmed) OrElse lTrimmed.StartsWith("'") Then Return
                
                ' Variable declarations
                Dim lVarPattern As New Regex("(?:^|\s)(?:Dim|Private|Public|Protected|Friend)\s+(?:Shared\s+)?(?:ReadOnly\s+)?(\w+)(?:\s*,\s*(\w+))*\s+As\s+", RegexOptions.IgnoreCase)
                Dim lVarMatch As Match = lVarPattern.Match(vPartialLine)
                If lVarMatch.Success Then
                    For i As Integer = 1 To lVarMatch.Groups.Count - 1
                        If lVarMatch.Groups(i).Success AndAlso Not String.IsNullOrWhiteSpace(lVarMatch.Groups(i).Value) Then
                            RegisterDeclaration(lVarMatch.Groups(i).Value, IdentifierScope.eField, vFilePath, vLineNumber, vColumnOffset + lVarMatch.Groups(i).Index)
                        End If
                    Next
                End If
                
                ' Method declarations
                Dim lMethodPattern As New Regex("(?:^|\s)(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:Overrides\s+)?(?:Function|Sub)\s+(\w+)\s*\(", RegexOptions.IgnoreCase)
                Dim lMethodMatch As Match = lMethodPattern.Match(vPartialLine)
                If lMethodMatch.Success Then
                    RegisterDeclaration(lMethodMatch.Groups(1).Value, IdentifierScope.eMethod, vFilePath, vLineNumber, vColumnOffset + lMethodMatch.Groups(1).Index)
                End If
                
                ' Property declarations
                Dim lPropPattern As New Regex("(?:^|\s)(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:ReadOnly\s+|WriteOnly\s+)?Property\s+(\w+)\s*(?:\(|$|\s)", RegexOptions.IgnoreCase)
                Dim lPropMatch As Match = lPropPattern.Match(vPartialLine)
                If lPropMatch.Success Then
                    RegisterDeclaration(lPropMatch.Groups(1).Value, IdentifierScope.eProperty, vFilePath, vLineNumber, vColumnOffset + lPropMatch.Groups(1).Index)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseLineForDeclarationsOutsideString error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parse line for references with column offset (for partial lines)
        ''' </summary>
        Private Sub ParseLineForReferencesOutsideString(vFilePath As String, vPartialLine As String, vLineNumber As Integer, vColumnOffset As Integer)
            Try
                ' Skip empty lines and comments
                Dim lTrimmed As String = vPartialLine.TrimStart()
                If String.IsNullOrEmpty(lTrimmed) OrElse lTrimmed.StartsWith("'") Then Return
                
                ' Parse the line character by character to respect string boundaries
                Dim i As Integer = 0
                Dim lInString As Boolean = False
                
                While i < vPartialLine.Length
                    Dim lChar As Char = vPartialLine(i)
                    
                    ' Check for comment start (not in string)
                    If lChar = "'"c AndAlso Not lInString Then
                        ' Rest of line is comment - stop processing
                        Exit While
                    End If
                    
                    ' Check for string start/end
                    If lChar = """"c Then
                        ' Check if it's escaped
                        If i + 1 < vPartialLine.Length AndAlso vPartialLine(i + 1) = """"c Then
                            ' Escaped quote - skip both
                            i += 2
                            Continue While
                        Else
                            ' Toggle string state
                            lInString = Not lInString
                            i += 1
                            Continue While
                        End If
                    End If
                    
                    ' If we're not in a string, look for identifiers
                    If Not lInString Then
                        ' Check if this starts an identifier
                        If (Char.IsLetter(lChar) OrElse lChar = "_"c) Then
                            Dim lStart As Integer = i
                            
                            ' Find the end of the identifier
                            While i < vPartialLine.Length AndAlso (Char.IsLetterOrDigit(vPartialLine(i)) OrElse vPartialLine(i) = "_"c)
                                i += 1
                            End While
                            
                            Dim lIdentifier As String = vPartialLine.Substring(lStart, i - lStart)
                            
                            ' Skip VB keywords
                            If Not IsKeyword(lIdentifier) Then
                                ' Check if this identifier has a declaration
                                Dim lKey As String = lIdentifier.ToLowerInvariant()
                                If pDeclarations.ContainsKey(lKey) Then
                                    RegisterReference(lIdentifier, vFilePath, vLineNumber, vColumnOffset + lStart, ReferenceKind.eUsage)
                                End If
                            End If
                        Else
                            i += 1
                        End If
                    Else
                        i += 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"ParseLineForReferencesOutsideString error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
