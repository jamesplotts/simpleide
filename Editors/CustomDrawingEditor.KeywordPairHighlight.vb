' Editors/CustomDrawingEditor.KeywordPairHighlight.vb - Highlights the matching block
' keyword pair (Class/End Class, If/End If, Select Case/End Select, etc.) for whatever
' line the cursor is currently on
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' ===== Keyword Pair Highlight State =====
        Private pKeywordPairActive As Boolean = False
        Private pKeywordPairLine1 As Integer = -1
        Private pKeywordPairStartCol1 As Integer = 0
        Private pKeywordPairEndCol1 As Integer = 0
        Private pKeywordPairLine2 As Integer = -1
        Private pKeywordPairStartCol2 As Integer = 0
        Private pKeywordPairEndCol2 As Integer = 0

        ''' <summary>
        ''' Roles a line can play when scanning for a control-flow keyword pair
        ''' </summary>
        Private Enum ControlFlowRole
            eNone
            eOpener
            eCloser
            eContinuation
            ''' <summary>A declaration-level Sub/Function/Class/etc. opener or closer -
            ''' declarations never nest inside a statement block in VB.NET, so hitting one
            ''' mid-scan means the scan has left the intended scope (or the source is
            ''' mid-edit/malformed) and should stop rather than keep searching</summary>
            eDeclarationBoundary
        End Enum

        ''' <summary>
        ''' Recomputes which two lines (if any) should show the matching block-keyword
        ''' highlight, based on the current cursor line. Declaration-level constructs
        ''' (Class/Module/Namespace/Structure/Interface/Enum/Sub/Function/Property/Get/Set)
        ''' are resolved from the syntax tree - the same StartLine/EndLine data folding
        ''' already relies on, so it already correctly excludes things like auto-implemented
        ''' properties that have no End Property. Control-flow constructs the tree doesn't
        ''' model (If/For/Do/While/Try/With/Select Case) are resolved with a nesting-aware
        ''' text scan instead.
        ''' </summary>
        ''' <remarks>Called from OnLineChanged whenever the cursor moves to a new line</remarks>
        Private Sub UpdateKeywordPairHighlight()
            Try
                pKeywordPairActive = False

                If pCursorLine < 0 OrElse pCursorLine >= pLineCount Then
                    pDrawingArea?.QueueDraw()
                    Return
                End If

                If TryFindDeclarationPair(pCursorLine) Then
                    pDrawingArea?.QueueDraw()
                    Return
                End If

                TryFindControlFlowPair(pCursorLine)
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"UpdateKeywordPairHighlight error: {ex.Message}")
            End Try
        End Sub

        ' ===== Declaration-level pairs (tree-based) =====

        Private Function TryFindDeclarationPair(vLine As Integer) As Boolean
            Try
                If pRootNode Is Nothing Then Return False

                Dim lNode As SyntaxNode = FindDeclarationNodeAtLine(pRootNode, vLine)
                If lNode Is Nothing Then Return False

                Dim lKeyword As String = DeclarationKeywordFor(lNode.NodeType)
                If String.IsNullOrEmpty(lKeyword) Then Return False

                Dim lStartCol As Integer, lEndCol As Integer
                If Not FindKeywordSpanOnLine(lNode.StartLine, lKeyword, True, lStartCol, lEndCol) Then Return False

                Dim lCloseStart As Integer, lCloseEnd As Integer
                If Not FindEndOrLoopKeywordSpan(lNode.EndLine, lCloseStart, lCloseEnd) Then Return False

                pKeywordPairActive = True
                pKeywordPairLine1 = lNode.StartLine
                pKeywordPairStartCol1 = lStartCol
                pKeywordPairEndCol1 = lEndCol
                pKeywordPairLine2 = lNode.EndLine
                pKeywordPairStartCol2 = lCloseStart
                pKeywordPairEndCol2 = lCloseEnd
                Return True

            Catch ex As Exception
                Console.WriteLine($"TryFindDeclarationPair error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Finds a foldable declaration node whose StartLine or EndLine is exactly vLine
        ''' </summary>
        Private Function FindDeclarationNodeAtLine(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            If vNode Is Nothing Then Return Nothing

            If IsDeclarationBlockType(vNode.NodeType) AndAlso vNode.IsFoldable AndAlso
               (vNode.StartLine = vLine OrElse vNode.EndLine = vLine) Then
                Return vNode
            End If

            If vNode.Children IsNot Nothing Then
                For Each lChild As SyntaxNode In vNode.Children
                    Dim lResult As SyntaxNode = FindDeclarationNodeAtLine(lChild, vLine)
                    If lResult IsNot Nothing Then Return lResult
                Next
            End If

            Return Nothing
        End Function

        Private Function IsDeclarationBlockType(vType As CodeNodeType) As Boolean
            Select Case vType
                Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eNamespace,
                     CodeNodeType.eStructure, CodeNodeType.eInterface, CodeNodeType.eEnum,
                     CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eProperty,
                     CodeNodeType.eGetAccessor, CodeNodeType.eSetAccessor
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Function DeclarationKeywordFor(vType As CodeNodeType) As String
            Select Case vType
                Case CodeNodeType.eClass : Return "Class"
                Case CodeNodeType.eModule : Return "Module"
                Case CodeNodeType.eNamespace : Return "Namespace"
                Case CodeNodeType.eStructure : Return "Structure"
                Case CodeNodeType.eInterface : Return "Interface"
                Case CodeNodeType.eEnum : Return "Enum"
                Case CodeNodeType.eMethod : Return "Sub"
                Case CodeNodeType.eFunction : Return "Function"
                Case CodeNodeType.eProperty : Return "Property"
                Case CodeNodeType.eGetAccessor : Return "Get"
                Case CodeNodeType.eSetAccessor : Return "Set"
                Case Else : Return ""
            End Select
        End Function

        ' ===== Control-flow pairs (text-scan based) =====

        Private Function TryFindControlFlowPair(vLine As Integer) As Boolean
            Try
                Dim lRole As ControlFlowRole = ClassifyControlFlowLine(vLine)

                If lRole = ControlFlowRole.eOpener Then
                    Dim lCloserLine As Integer = ScanForCloser(vLine)
                    If lCloserLine < 0 Then Return False
                    Return SetControlFlowHighlight(vLine, lCloserLine)

                ElseIf lRole = ControlFlowRole.eCloser Then
                    Dim lOpenerLine As Integer = ScanForOpener(vLine)
                    If lOpenerLine < 0 Then Return False
                    Return SetControlFlowHighlight(lOpenerLine, vLine)
                End If

                Return False

            Catch ex As Exception
                Console.WriteLine($"TryFindControlFlowPair error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Scans forward from just after an opener line, tracking nesting depth, to find
        ''' the line that closes it. Aborts (-1) if a declaration boundary is hit first,
        ''' since a Sub/Class/etc. can't legitimately appear nested inside a statement block.
        ''' </summary>
        Private Function ScanForCloser(vOpenerLine As Integer) As Integer
            Const MAX_SCAN_LINES As Integer = 3000
            Dim lDepth As Integer = 1
            Dim lLimit As Integer = Math.Min(pLineCount - 1, vOpenerLine + MAX_SCAN_LINES)

            for lLine As Integer = vOpenerLine + 1 To lLimit
                Select Case ClassifyControlFlowLine(lLine)
                    Case ControlFlowRole.eDeclarationBoundary
                        Return -1
                    Case ControlFlowRole.eOpener
                        lDepth += 1
                    Case ControlFlowRole.eCloser
                        lDepth -= 1
                        If lDepth = 0 Then Return lLine
                End Select
            Next

            Return -1
        End Function

        ''' <summary>
        ''' Scans backward from just before a closer line, tracking nesting depth, to find
        ''' the line that opened it. Mirrors ScanForCloser.
        ''' </summary>
        Private Function ScanForOpener(vCloserLine As Integer) As Integer
            Const MAX_SCAN_LINES As Integer = 3000
            Dim lDepth As Integer = 1
            Dim lLimit As Integer = Math.Max(0, vCloserLine - MAX_SCAN_LINES)

            for lLine As Integer = vCloserLine - 1 To lLimit Step -1
                Select Case ClassifyControlFlowLine(lLine)
                    Case ControlFlowRole.eDeclarationBoundary
                        Return -1
                    Case ControlFlowRole.eCloser
                        lDepth += 1
                    Case ControlFlowRole.eOpener
                        lDepth -= 1
                        If lDepth = 0 Then Return lLine
                End Select
            Next

            Return -1
        End Function

        Private Function SetControlFlowHighlight(vOpenerLine As Integer, vCloserLine As Integer) As Boolean
            Dim lOpenStart As Integer, lOpenEnd As Integer
            Dim lCloseStart As Integer, lCloseEnd As Integer

            If Not FindControlFlowOpenerSpan(vOpenerLine, lOpenStart, lOpenEnd) Then Return False
            If Not FindEndOrLoopKeywordSpan(vCloserLine, lCloseStart, lCloseEnd) Then Return False

            pKeywordPairActive = True
            pKeywordPairLine1 = vOpenerLine
            pKeywordPairStartCol1 = lOpenStart
            pKeywordPairEndCol1 = lOpenEnd
            pKeywordPairLine2 = vCloserLine
            pKeywordPairStartCol2 = lCloseStart
            pKeywordPairEndCol2 = lCloseEnd
            Return True
        End Function

        ''' <summary>
        ''' Classifies a line for control-flow nesting purposes: an opener (If/For/Do/
        ''' While/Try/With/Select Case), a closer (End If/Next/Loop/End While/End Try/
        ''' End With/End Select), a continuation that doesn't change nesting depth
        ''' (ElseIf/Else/Case/Catch/Finally), a declaration boundary, or none of the above
        ''' </summary>
        Private Function ClassifyControlFlowLine(vLine As Integer) As ControlFlowRole
            If vLine < 0 OrElse vLine >= pLineCount Then Return ControlFlowRole.eNone

            Dim lTokens As List(Of Token) = TokenizeSignificant(vLine)

            Dim lIdx As Integer = 0
            While lIdx < lTokens.Count AndAlso AutoEndModifierKeywords.Contains(lTokens(lIdx).Text)
                lIdx += 1
            End While
            If lIdx >= lTokens.Count Then Return ControlFlowRole.eNone

            Dim lFirst As String = lTokens(lIdx).Text
            Dim lSecond As String = If(lIdx + 1 < lTokens.Count, lTokens(lIdx + 1).Text, "")

            Select Case True
                Case IsAnyKeyword(lFirst, "Class", "Module", "Namespace", "Structure", "Interface",
                                   "Enum", "Sub", "Function", "Property", "Get", "Set")
                    Return ControlFlowRole.eDeclarationBoundary

                Case String.Equals(lFirst, "End", StringComparison.OrdinalIgnoreCase)
                    If IsAnyKeyword(lSecond, "If", "While", "Try", "With", "Select") Then
                        Return ControlFlowRole.eCloser
                    End If
                    ' End Class/Sub/Function/Property/Module/Namespace/Structure/Interface/
                    ' Enum/Get/Set
                    Return ControlFlowRole.eDeclarationBoundary

                Case String.Equals(lFirst, "Select", StringComparison.OrdinalIgnoreCase) AndAlso
                     String.Equals(lSecond, "Case", StringComparison.OrdinalIgnoreCase)
                    Return ControlFlowRole.eOpener

                Case String.Equals(lFirst, "If", StringComparison.OrdinalIgnoreCase)
                    ' Only the multi-line form ("If x Then" with nothing after Then) opens a
                    ' block - matches TryAutoCompleteBlockStatement's own check
                    If lTokens.Count > 0 AndAlso
                       String.Equals(lTokens(lTokens.Count - 1).Text, "Then", StringComparison.OrdinalIgnoreCase) Then
                        Return ControlFlowRole.eOpener
                    End If
                    Return ControlFlowRole.eNone

                Case IsAnyKeyword(lFirst, "For", "Do", "While", "Try", "With")
                    Return ControlFlowRole.eOpener

                Case IsAnyKeyword(lFirst, "Next", "Loop")
                    Return ControlFlowRole.eCloser

                Case IsAnyKeyword(lFirst, "ElseIf", "Else", "Case", "Catch", "Finally")
                    Return ControlFlowRole.eContinuation

                Case Else
                    Return ControlFlowRole.eNone
            End Select
        End Function

        ''' <summary>
        ''' Tokenizes a line and strips whitespace tokens, so token index 0 is always the
        ''' first significant token regardless of indentation
        ''' </summary>
        Private Function TokenizeSignificant(vLine As Integer) As List(Of Token)
            Dim lTokenizer As New VBTokenizer()
            Dim lAllTokens As List(Of Token) = lTokenizer.TokenizeLine(TextLines(vLine))
            Return lAllTokens.Where(Function(t) t.Type <> TokenType.eWhitespace).ToList()
        End Function

        Private Function IsAnyKeyword(vText As String, ParamArray vCandidates() As String) As Boolean
            For Each lCandidate In vCandidates
                If String.Equals(vText, lCandidate, StringComparison.OrdinalIgnoreCase) Then Return True
            Next
            Return False
        End Function

        ''' <summary>Finds the highlight span for a line already classified as a control-flow opener</summary>
        Private Function FindControlFlowOpenerSpan(vLine As Integer, ByRef vStartCol As Integer, ByRef vEndCol As Integer) As Boolean
            vStartCol = 0 : vEndCol = 0
            If vLine < 0 OrElse vLine >= pLineCount Then Return False

            Dim lTokens As List(Of Token) = TokenizeSignificant(vLine)
            If lTokens.Count = 0 Then Return False

            ' "Select Case" is the only two-word opener
            If String.Equals(lTokens(0).Text, "Select", StringComparison.OrdinalIgnoreCase) AndAlso
               lTokens.Count > 1 AndAlso String.Equals(lTokens(1).Text, "Case", StringComparison.OrdinalIgnoreCase) Then
                vStartCol = lTokens(0).StartColumn
                vEndCol = lTokens(1).EndColumn + 1
                Return True
            End If

            ' Single-word openers: If, For, Do, While, Try, With
            vStartCol = lTokens(0).StartColumn
            vEndCol = lTokens(0).EndColumn + 1
            Return True
        End Function

        ''' <summary>
        ''' Finds the highlight span for an "End X" closer (declaration or control-flow) or
        ''' a single-word "Next"/"Loop" closer
        ''' </summary>
        Private Function FindEndOrLoopKeywordSpan(vLine As Integer, ByRef vStartCol As Integer, ByRef vEndCol As Integer) As Boolean
            vStartCol = 0 : vEndCol = 0
            If vLine < 0 OrElse vLine >= pLineCount Then Return False

            Dim lTokens As List(Of Token) = TokenizeSignificant(vLine)
            If lTokens.Count = 0 Then Return False

            If String.Equals(lTokens(0).Text, "End", StringComparison.OrdinalIgnoreCase) AndAlso lTokens.Count > 1 Then
                vStartCol = lTokens(0).StartColumn
                vEndCol = lTokens(1).EndColumn + 1
                Return True
            End If

            If String.Equals(lTokens(0).Text, "Next", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(lTokens(0).Text, "Loop", StringComparison.OrdinalIgnoreCase) Then
                vStartCol = lTokens(0).StartColumn
                vEndCol = lTokens(0).EndColumn + 1
                Return True
            End If

            Return False
        End Function

        ''' <summary>
        ''' Finds the span of a single expected keyword on a line, optionally skipping
        ''' leading modifier keywords first (Public Shared ReadOnly Property ...)
        ''' </summary>
        Private Function FindKeywordSpanOnLine(vLine As Integer, vKeyword As String, vSkipModifiers As Boolean, ByRef vStartCol As Integer, ByRef vEndCol As Integer) As Boolean
            vStartCol = 0 : vEndCol = 0
            If vLine < 0 OrElse vLine >= pLineCount Then Return False

            Dim lTokens As List(Of Token) = TokenizeSignificant(vLine)

            Dim lIdx As Integer = 0
            If vSkipModifiers Then
                While lIdx < lTokens.Count AndAlso AutoEndModifierKeywords.Contains(lTokens(lIdx).Text)
                    lIdx += 1
                End While
            End If
            If lIdx >= lTokens.Count Then Return False

            If String.Equals(lTokens(lIdx).Text, vKeyword, StringComparison.OrdinalIgnoreCase) Then
                vStartCol = lTokens(lIdx).StartColumn
                vEndCol = lTokens(lIdx).EndColumn + 1
                Return True
            End If

            Return False
        End Function

    End Class

End Namespace
