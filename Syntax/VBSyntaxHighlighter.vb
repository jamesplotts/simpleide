' VBSyntaxHighlighter.vb - VB.NET syntax highlighting engine
Imports System
Imports System.Reflection
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Syntax


Namespace Syntax
    
    Public Class VBSyntaxHighlighter
        Implements IDisposable
        
        ' ===== Private Fields =====
        Private pColorSet As SyntaxColorSet
        Private pKeywordPatterns As New Dictionary(Of String, Regex)()
        Private pCompiledPatterns As New Dictionary(Of SyntaxTokenType, Regex)()
        Private pIsInitialized As Boolean = False
        
        ' ===== Constructor =====
        Public Sub New(vColorSet As SyntaxColorSet)
            Try
                pColorSet = vColorSet
                InitializePatterns()
                pIsInitialized = True
                
            Catch ex As Exception
                Console.WriteLine($"VBSyntaxHighlighter constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ' Highlight a single line of VB.NET code
        Public Function HighlightLine(vLineText As String, vLineIndex As Integer) As List(Of SyntaxToken)
            Dim lTokens As New List(Of SyntaxToken)()
            
            Try
                If Not pIsInitialized OrElse String.IsNullOrEmpty(vLineText) Then
                    Return lTokens
                End If
                
                ' Process line in order of precedence:
                ' 1. Comments (highest priority - they override everything else)
                ' 2. Strings (second priority)
                ' 3. Numbers
                ' 4. Keywords
                ' 5. Operators
                ' 6. Identifiers
                
                Dim lProcessedRanges As New List(Of Integer())() ' Track processed character ranges
                
                ' 1. Find and highlight comments first
                HighlightComments(vLineText, lTokens, lProcessedRanges)
                
                ' 2. Find and highlight strings (avoid already processed ranges)
                HighlightStrings(vLineText, lTokens, lProcessedRanges)
                
                ' 3. Find and highlight numbers
                HighlightNumbers(vLineText, lTokens, lProcessedRanges)
                
                ' 4. Find and highlight keywords
                HighlightKeywords(vLineText, lTokens, lProcessedRanges)
                
                ' 5. Find and highlight operators
                HighlightOperators(vLineText, lTokens, lProcessedRanges)
                
                ' Sort tokens by start position
                lTokens.Sort(Function(a, b) a.StartColumn.CompareTo(b.StartColumn))
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"HighlightLine error: {ex.Message}")
                Return lTokens
            End Try
        End Function
        
        ' Update color scheme
        Public Sub UpdateColorSet(vColorSet As SyntaxColorSet)
            Try
                pColorSet = vColorSet
                
            Catch ex As Exception
                Console.WriteLine($"UpdateColorSet error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods =====
        
        ' Initialize regex patterns for syntax highlighting
        Private Sub InitializePatterns()
            Try
                ' Compile regex patterns for better performance
                InitializeCommentPatterns()
                InitializeStringPatterns()
                InitializeNumberPatterns()
                InitializeKeywordPatterns()
                InitializeOperatorPatterns()
                
            Catch ex As Exception
                Console.WriteLine($"InitializePatterns error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InitializeCommentPatterns()
            Try
                ' Single line comments: ' or REM
                pCompiledPatterns(SyntaxTokenType.eComment) = New Regex(
                    "'.*$|^\s*REM\s+.*$",
                    RegexOptions.IgnoreCase Or RegexOptions.Compiled
                )
                
            Catch ex As Exception
                Console.WriteLine($"InitializeCommentPatterns error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InitializeStringPatterns()
            Try
                ' String literals: "text" and character literals: "c"c
                pCompiledPatterns(SyntaxTokenType.eString) = New Regex(
                    """([^""]|"""")*""[cC]?",
                    RegexOptions.Compiled
                )
                
            Catch ex As Exception
                Console.WriteLine($"InitializeStringPatterns error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InitializeNumberPatterns()
            Try
                ' Numbers: integers, decimals, hex, octal, binary
                Dim lNumberPattern As String = String.Join("|", VBLanguageDefinition.NumberPatterns)
                pCompiledPatterns(SyntaxTokenType.eNumber) = New Regex(
                    lNumberPattern,
                    RegexOptions.IgnoreCase Or RegexOptions.Compiled
                )
                
            Catch ex As Exception
                Console.WriteLine($"InitializeNumberPatterns error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InitializeKeywordPatterns()
            Try
                ' Build keyword pattern from VBLanguageDefinition
                Dim lKeywords As String() = VBLanguageDefinition.Keywords
                Dim lKeywordPattern As String = "\b(" & String.Join("|", lKeywords) & ")\b"
                
                pCompiledPatterns(SyntaxTokenType.eKeyword) = New Regex(
                    lKeywordPattern,
                    RegexOptions.IgnoreCase Or RegexOptions.Compiled
                )
                
                ' Build type pattern
                Dim lTypes As New List(Of String)

                For Each lAssembly As Assembly In AppDomain.CurrentDomain.GetAssemblies()
                    For Each lType As Type In lAssembly.GetTypes()
                        lTypes.Add(lType.FullName)
                    Next
                Next 

                Dim lTypePattern As String = "\b(" & String.Join("|", lTypes) & ")\b"
                
                pCompiledPatterns(SyntaxTokenType.eType) = New Regex(
                    lTypePattern,
                    RegexOptions.IgnoreCase Or RegexOptions.Compiled
                )
                
            Catch ex As Exception
                Console.WriteLine($"InitializeKeywordPatterns error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InitializeOperatorPatterns()
            Try
                ' Escape special regex characters in operators
                Dim lOperators As String() = VBLanguageDefinition.Operators
                Dim lEscapedOperators As New List(Of String)()
                
                For Each lOp In lOperators
                    lEscapedOperators.Add(Regex.Escape(lOp))
                Next
                
                ' Sort by length (descending) to match longer operators first
                lEscapedOperators.Sort(Function(a, b) b.Length.CompareTo(a.Length))
                
                Dim lOperatorPattern As String = String.Join("|", lEscapedOperators)
                pCompiledPatterns(SyntaxTokenType.eOperator) = New Regex(
                    lOperatorPattern,
                    RegexOptions.Compiled
                )
                
            Catch ex As Exception
                Console.WriteLine($"InitializeOperatorPatterns error: {ex.Message}")
            End Try
        End Sub
        
        ' Highlight comments in the line
        Private Sub HighlightComments(vLineText As String, vTokens As List(Of SyntaxToken), vProcessedRanges As List(Of Integer()))
            Try
                Dim lCommentRegex As Regex = pCompiledPatterns(SyntaxTokenType.eComment)
                Dim lMatches As MatchCollection = lCommentRegex.Matches(vLineText)
                
                For Each lMatch As Match In lMatches
                    Dim lToken As New SyntaxToken(
                        lMatch.Index,
                        lMatch.Length,
                        SyntaxTokenType.eComment,
                        pColorSet.GetColor(SyntaxColorSet.Tags.eComment)
                    )
                    vTokens.Add(lToken)
                    
                    ' Mark this range as processed (comments have highest priority)
                    vProcessedRanges.Add({lMatch.Index, lMatch.Index + lMatch.Length - 1})
                Next
                
            Catch ex As Exception
                Console.WriteLine($"HighlightComments error: {ex.Message}")
            End Try
        End Sub
        
        ' Highlight strings in the line
        Private Sub HighlightStrings(vLineText As String, vTokens As List(Of SyntaxToken), vProcessedRanges As List(Of Integer()))
            Try
                Dim lStringRegex As Regex = pCompiledPatterns(SyntaxTokenType.eString)
                Dim lMatches As MatchCollection = lStringRegex.Matches(vLineText)
                
                For Each lMatch As Match In lMatches
                    ' Skip if this range is already processed (e.g., inside a comment)
                    If IsRangeProcessed(lMatch.Index, lMatch.Index + lMatch.Length - 1, vProcessedRanges) Then
                        Continue For
                    End If
                    
                    Dim lToken As New SyntaxToken(
                        lMatch.Index,
                        lMatch.Length,
                        SyntaxTokenType.eString,
                        pColorSet.GetColor(SyntaxColorSet.Tags.eString)
                    )
                    vTokens.Add(lToken)
                    
                    ' Mark this range as processed
                    vProcessedRanges.Add({lMatch.Index, lMatch.Index + lMatch.Length - 1})
                Next
                
            Catch ex As Exception
                Console.WriteLine($"HighlightStrings error: {ex.Message}")
            End Try
        End Sub
        
        ' Highlight numbers in the line
        Private Sub HighlightNumbers(vLineText As String, vTokens As List(Of SyntaxToken), vProcessedRanges As List(Of Integer()))
            Try
                Dim lNumberRegex As Regex = pCompiledPatterns(SyntaxTokenType.eNumber)
                Dim lMatches As MatchCollection = lNumberRegex.Matches(vLineText)
                
                For Each lMatch As Match In lMatches
                    ' Skip if this range is already processed
                    If IsRangeProcessed(lMatch.Index, lMatch.Index + lMatch.Length - 1, vProcessedRanges) Then
                        Continue For
                    End If
                    
                    Dim lToken As New SyntaxToken(
                        lMatch.Index,
                        lMatch.Length,
                        SyntaxTokenType.eNumber,
                        pColorSet.GetColor(SyntaxColorSet.Tags.eNumber)
                    )
                    vTokens.Add(lToken)
                    
                    ' Mark this range as processed
                    vProcessedRanges.Add({lMatch.Index, lMatch.Index + lMatch.Length - 1})
                Next
                
            Catch ex As Exception
                Console.WriteLine($"HighlightNumbers error: {ex.Message}")
            End Try
        End Sub
        
        ' Highlight keywords in the line
        Private Sub HighlightKeywords(vLineText As String, vTokens As List(Of SyntaxToken), vProcessedRanges As List(Of Integer()))
            Try
                ' Highlight VB.NET keywords
                Dim lKeywordRegex As Regex = pCompiledPatterns(SyntaxTokenType.eKeyword)
                Dim lMatches As MatchCollection = lKeywordRegex.Matches(vLineText)
                
                For Each lMatch As Match In lMatches
                    ' Skip if this range is already processed
                    If IsRangeProcessed(lMatch.Index, lMatch.Index + lMatch.Length - 1, vProcessedRanges) Then
                        Continue For
                    End If
                    
                    Dim lToken As New SyntaxToken(
                        lMatch.Index,
                        lMatch.Length,
                        SyntaxTokenType.eKeyword,
                        pColorSet.GetColor(SyntaxColorSet.Tags.eKeyword)
                    )
                    lToken.IsBold = True
                    vTokens.Add(lToken)
                    
                    ' Mark this range as processed
                    vProcessedRanges.Add({lMatch.Index, lMatch.Index + lMatch.Length - 1})
                Next
                
                ' Highlight types
                Dim lTypeRegex As Regex = pCompiledPatterns(SyntaxTokenType.eType)
                Dim lTypeMatches As MatchCollection = lTypeRegex.Matches(vLineText)
                
                For Each lMatch As Match In lTypeMatches
                    ' Skip if this range is already processed
                    If IsRangeProcessed(lMatch.Index, lMatch.Index + lMatch.Length - 1, vProcessedRanges) Then
                        Continue For
                    End If
                    
                    Dim lToken As New SyntaxToken(
                        lMatch.Index,
                        lMatch.Length,
                        SyntaxTokenType.eType,
                        pColorSet.GetColor(SyntaxColorSet.Tags.eType)
                    )
                    vTokens.Add(lToken)
                    
                    ' Mark this range as processed
                    vProcessedRanges.Add({lMatch.Index, lMatch.Index + lMatch.Length - 1})
                Next
                
            Catch ex As Exception
                Console.WriteLine($"HighlightKeywords error: {ex.Message}")
            End Try
        End Sub
        
        ' Highlight operators in the line
        Private Sub HighlightOperators(vLineText As String, vTokens As List(Of SyntaxToken), vProcessedRanges As List(Of Integer()))
            Try
                Dim lOperatorRegex As Regex = pCompiledPatterns(SyntaxTokenType.eOperator)
                Dim lMatches As MatchCollection = lOperatorRegex.Matches(vLineText)
                
                For Each lMatch As Match In lMatches
                    ' Skip if this range is already processed
                    If IsRangeProcessed(lMatch.Index, lMatch.Index + lMatch.Length - 1, vProcessedRanges) Then
                        Continue For
                    End If
                    
                    Dim lToken As New SyntaxToken(
                        lMatch.Index,
                        lMatch.Length,
                        SyntaxTokenType.eOperator,
                        pColorSet.GetColor(SyntaxColorSet.Tags.eOperator)
                    )
                    vTokens.Add(lToken)
                    
                    ' Mark this range as processed
                    vProcessedRanges.Add({lMatch.Index, lMatch.Index + lMatch.Length - 1})
                Next
                
            Catch ex As Exception
                Console.WriteLine($"HighlightOperators error: {ex.Message}")
            End Try
        End Sub
        
        ' Check if a character range has already been processed
        Private Function IsRangeProcessed(vStart As Integer, vEnd As Integer, vProcessedRanges As List(Of Integer())) As Boolean
            Try
                For Each lRange In vProcessedRanges
                    ' Check for any overlap
                    If Not (vEnd < lRange(0) OrElse vStart > lRange(1)) Then
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsRangeProcessed error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Updates the color set used by the syntax highlighter
        ''' </summary>
        ''' <param name="vColorSet">The new color set to use</param>
        Public Sub SetColorSet(vColorSet As SyntaxColorSet)
            Try
                If vColorSet IsNot Nothing Then
                    pColorSet = vColorSet
                    Console.WriteLine("VBSyntaxHighlighter: Color Set updated")
                End If
            Catch ex As Exception
                Console.WriteLine($"SetColorSet error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== IDisposable Implementation =====
        
        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                ' Clean up compiled regex patterns
                If pCompiledPatterns IsNot Nothing Then
                    pCompiledPatterns.Clear()
                    pCompiledPatterns = Nothing
                End If
                
                If pKeywordPatterns IsNot Nothing Then
                    pKeywordPatterns.Clear()
                    pKeywordPatterns = Nothing
                End If
                
                pColorSet = Nothing
                pIsInitialized = False
                
            Catch ex As Exception
                Console.WriteLine($"VBSyntaxHighlighter.Dispose error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
