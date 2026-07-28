' Editors/CustomDrawingEditor.IdentifierCaseSync.vb - Identifier case synchronization
' On leaving a line that declares something (Dim/Private/Public/Sub/Function/Property/etc.),
' compares the declared name's casing against the casing last recorded for that same
' declaration. A mismatch means the user retyped the declaration with different casing, so the
' new casing is propagated to every other reference - matching classic VB.NET IDE behavior
' where renaming a declaration's case recases it everywhere.
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' ===== Events for Project-Wide Updates =====

        ' Raised when an identifier's case changes in a declaration
        Public Event IdentifierCaseChanged(vOldName As String, vNewName As String, vScope As IdentifierScope)

        ' ===== Fields =====

        ''' <summary>
        ''' Canonical casing for local-like declarations (locals, parameters, non-field
        ''' variables), keyed by "{containing method/function/constructor/property qualified
        ''' name}::{lowercase identifier name}" so that same-named locals in unrelated methods
        ''' never collide with each other
        ''' </summary>
        Private pLocalIdentifierCaseMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        ' ===== Identifier Case Synchronization =====

        ''' <summary>
        ''' Examines a just-exited line for declarations and, for each one, detects and
        ''' propagates any casing change against the previously recorded canonical casing
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based index of the line that was exited</param>
        Private Sub ProcessLineFormattingWithDeclarationTracking(vLineIndex As Integer)
            Try
                If pSourceFileInfo Is Nothing Then Return
                If vLineIndex < 0 OrElse vLineIndex >= pSourceFileInfo.TextLines.Count Then Return

                Dim lLineText As String = pSourceFileInfo.TextLines(vLineIndex)
                If String.IsNullOrEmpty(lLineText.Trim()) Then Return

                Dim lDeclarations As List(Of IdentifierDeclaration) = ExtractDeclarations(lLineText)
                If lDeclarations.Count = 0 Then Return

                Dim lContainingMember As SyntaxNode = FindContainingMemberNode(vLineIndex)

                for each lDecl in lDeclarations
                    DetectAndApplyDeclarationCaseChange(lDecl, lContainingMember, vLineIndex)
                Next

            Catch ex As Exception
                Console.WriteLine($"ProcessLineFormattingWithDeclarationTracking error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Extract declarations from a line of code
        ''' </summary>
        Private Function ExtractDeclarations(vLine As String) As List(Of IdentifierDeclaration)
            Dim lDeclarations As New List(Of IdentifierDeclaration)

            Try
                ' Patterns for various declarations
                Dim lPatterns As New List(Of DeclarationPattern)
                With lPatterns
                    ' Variable declarations
                    .Add(New DeclarationPattern( "^\s*(?:Public|Private|Protected|Friend|Dim)\s+(?:Shared\s+)?(?:ReadOnly\s+)?(\w+)(?:\s*,\s*(\w+))*\s+As\s+", IdentifierScope.eVariable ))
                    ' Function/Sub declarations
                    .Add(New DeclarationPattern( "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:Overrides\s+)?(?:Function|Sub)\s+(\w+)\s*\(", IdentifierScope.eMethod ))
                    ' Property declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:ReadOnly\s+|WriteOnly\s+)?Property\s+(\w+)",
                        IdentifierScope.eProperty ) )
                    ' Class/Module/Structure declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Partial\s+)?(?:Class|Module|Structure|Interface)\s+(\w+)",
                        IdentifierScope.eType ) )
                    ' Event declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*Event\s+(\w+)",
                        IdentifierScope.eEvent ) )
                    ' Const declarations
                    .Add(New DeclarationPattern(
                        "^\s*(?:Public|Private|Protected|Friend)?\s*Const\s+(\w+)\s*=",
                        IdentifierScope.eConstant ) )
                    ' For loop variables
                    .Add(New DeclarationPattern(
                        "\bFor\s+(?:each\s+)?(\w+)\s+",
                        IdentifierScope.eLocal ) )
                    ' Parameter declarations in method signature
                    .Add(New DeclarationPattern(
                        "(?:ByVal|ByRef)?\s*(\w+)\s+As\s+",
                        IdentifierScope.eParameter ) )
                End With

                ' Check each pattern
                For Each lPattern In lPatterns
                    Dim lRegex As New Regex(lPattern.Pattern, RegexOptions.IgnoreCase)
                    Dim lMatch As Match = lRegex.Match(vLine)

                    If lMatch.Success Then
                        ' Extract all captured identifiers
                        For i As Integer = 1 To lMatch.Groups.Count - 1
                            If lMatch.Groups(i).Success AndAlso Not String.IsNullOrWhiteSpace(lMatch.Groups(i).Value) Then
                                lDeclarations.Add(New IdentifierDeclaration(
                                    lMatch.Groups(i).Value,
                                    lPattern.Scope,
                                    0
                                ))
                            End If
                        Next
                    End If
                Next

                ' Handle multiple variable declarations on same line (Dim x, y, z As Integer)
                Dim lMultiVarPattern As New Regex("^\s*(?:Dim|Private|Public)\s+((?:\w+\s*,\s*)*\w+)\s+As\s+", RegexOptions.IgnoreCase)
                Dim lMultiMatch As Match = lMultiVarPattern.Match(vLine)
                If lMultiMatch.Success Then
                    Dim lVarList As String = lMultiMatch.Groups(1).Value
                    Dim lVars() As String = lVarList.Split(","c)
                    For Each lVar As String In lVars
                        lVar = lVar.Trim()
                        If Not String.IsNullOrWhiteSpace(lVar) Then
                            lDeclarations.Add(New IdentifierDeclaration(lVar, IdentifierScope.eVariable, 0))
                        End If
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"ExtractDeclarations error: {ex.Message}")
            End Try

            Return lDeclarations
        End Function

        ''' <summary>
        ''' Detects whether a single extracted declaration's casing differs from the casing
        ''' previously recorded for it and, if so, propagates the new casing
        ''' </summary>
        ''' <param name="vDecl">The declaration extracted from the exited line</param>
        ''' <param name="vContainingMember">
        ''' The innermost method/function/constructor/property node containing the line, or
        ''' Nothing if the line isn't inside one (e.g. a field declared directly in a class body)
        ''' </param>
        ''' <param name="vLineIndex">Zero-based index of the line the declaration is on</param>
        Private Sub DetectAndApplyDeclarationCaseChange(vDecl As IdentifierDeclaration, vContainingMember As SyntaxNode, vLineIndex As Integer)
            Try
                Dim lIsLocalLike As Boolean = IsLocalLikeScope(vDecl.Scope) AndAlso vContainingMember IsNot Nothing

                If lIsLocalLike Then
                    Dim lScopeKey As String = GetScopeQualifiedKey(vContainingMember)
                    Dim lKey As String = $"{lScopeKey}::{vDecl.Name}"

                    Dim lExistingCase As String = Nothing
                    If pLocalIdentifierCaseMap.TryGetValue(lKey, lExistingCase) Then
                        If Not lExistingCase.Equals(vDecl.Name, StringComparison.Ordinal) Then
                            ' Case changed - propagate within the containing member only, since
                            ' locals/parameters can't be seen outside it
                            pLocalIdentifierCaseMap(lKey) = vDecl.Name
                            UpdateIdentifierCaseInRange(lExistingCase, vDecl.Name, vContainingMember.StartLine, vContainingMember.EndLine)
                        End If
                    Else
                        pLocalIdentifierCaseMap(lKey) = vDecl.Name
                    End If
                Else
                    ' Member-level (type/method/property/field/event/const) - visible beyond
                    ' this file, so use the existing project-wide propagation plumbing
                    Dim lExistingCase As String = Nothing
                    If pIdentifierCaseMap.TryGetValue(vDecl.Name, lExistingCase) Then
                        If Not lExistingCase.Equals(vDecl.Name, StringComparison.Ordinal) Then
                            UpdateIdentifierCaseProjectWide(lExistingCase, vDecl.Name, vDecl.Scope)
                        End If
                    Else
                        UpdateIdentifierCaseMap(vDecl.Name, vDecl.Name)
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"DetectAndApplyDeclarationCaseChange error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' True for declaration scopes that are only ever visible within their containing
        ''' method/function/constructor/property (never referenced from elsewhere), so they need
        ''' their own scope-qualified map entry rather than sharing the flat project-wide map
        ''' </summary>
        Private Function IsLocalLikeScope(vScope As IdentifierScope) As Boolean
            Select Case vScope
                Case IdentifierScope.eLocal, IdentifierScope.eVariable, IdentifierScope.eParameter
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        ''' <summary>
        ''' Finds the innermost method/function/constructor/property node in this file's syntax
        ''' tree whose line range contains vLineIndex
        ''' </summary>
        Private Function FindContainingMemberNode(vLineIndex As Integer) As SyntaxNode
            Try
                If pSourceFileInfo Is Nothing OrElse pSourceFileInfo.SyntaxTree Is Nothing Then Return Nothing
                Return FindContainingMemberNodeRecursive(pSourceFileInfo.SyntaxTree, vLineIndex)
            Catch ex As Exception
                Console.WriteLine($"FindContainingMemberNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        Private Function FindContainingMemberNodeRecursive(vNode As SyntaxNode, vLineIndex As Integer) As SyntaxNode
            If vNode Is Nothing Then Return Nothing

            Dim lBest As SyntaxNode = Nothing
            If IsScopeContainerType(vNode.NodeType) AndAlso vLineIndex >= vNode.StartLine AndAlso vLineIndex <= vNode.EndLine Then
                lBest = vNode
            End If

            If vNode.Children IsNot Nothing Then
                for each lChild As SyntaxNode in vNode.Children
                    Dim lDeeper As SyntaxNode = FindContainingMemberNodeRecursive(lChild, vLineIndex)
                    If lDeeper IsNot Nothing Then lBest = lDeeper ' prefer the innermost match
                Next
            End If

            Return lBest
        End Function

        Private Function IsScopeContainerType(vType As CodeNodeType) As Boolean
            Select Case vType
                Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor, CodeNodeType.eProperty
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        ''' <summary>
        ''' Builds a scope-qualifying key for a member node by joining its own name with every
        ''' containing node's name (e.g. "CustomDrawingEditor.OnLineChanged") - stable across
        ''' cut/paste as long as the code stays within the same member, and naturally treats code
        ''' moved into a different member as a distinct declaration, which is correct
        ''' </summary>
        Private Function GetScopeQualifiedKey(vMemberNode As SyntaxNode) As String
            Dim lParts As New List(Of String)
            Dim lNode As SyntaxNode = vMemberNode
            While lNode IsNot Nothing
                If Not String.IsNullOrEmpty(lNode.Name) Then lParts.Insert(0, lNode.Name)
                lNode = lNode.Parent
            End While
            Return String.Join(".", lParts)
        End Function

        ''' <summary>
        ''' Update identifier case project-wide (member-level declarations)
        ''' </summary>
        Private Sub UpdateIdentifierCaseProjectWide(vOldCase As String, vNewCase As String, vScope As IdentifierScope)
            Try
                ' Update our local case map
                UpdateIdentifierCaseMap(vOldCase, vNewCase)

                ' Raise event for MainWindow to handle project-wide update
                RaiseEvent IdentifierCaseChanged(vOldCase, vNewCase, vScope)

                ' Update all occurrences in current document
                UpdateIdentifierCaseInRange(vOldCase, vNewCase, 0, pLineCount - 1)

            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseProjectWide error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates all occurrences of an identifier within a line range (inclusive), skipping
        ''' the line currently being edited
        ''' </summary>
        Private Sub UpdateIdentifierCaseInRange(vOldCase As String, vNewCase As String, vStartLine As Integer, vEndLine As Integer)
            Try
                If pSourceFileInfo Is Nothing Then Return
                Dim lFirstLine As Integer = Math.Max(0, vStartLine)
                Dim lLastLine As Integer = Math.Min(pSourceFileInfo.TextLines.Count - 1, vEndLine)

                for i As Integer = lFirstLine To lLastLine
                    ' Skip the line being edited
                    If i = pEditingLine Then Continue For

                    Dim lLine As String = pSourceFileInfo.TextLines(i)
                    Dim lUpdatedLine As String = UpdateIdentifierCaseInLine(lLine, vOldCase, vNewCase)

                    If Not lLine.Equals(lUpdatedLine, StringComparison.Ordinal) Then
                        pSourceFileInfo.TextLines(i) = lUpdatedLine
                        pLineMetadata(i).MarkChanged()
                        InvalidateLine(i)
                    End If
                Next

                IsModified = True

            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseInRange error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Replaces whole-identifier occurrences of vOldCase with vNewCase in a single line,
        ''' leaving string/comment/whitespace/operator tokens untouched
        ''' </summary>
        Private Function UpdateIdentifierCaseInLine(vLine As String, vOldCase As String, vNewCase As String) As String
            Try
                Dim lTokenizer As New VBTokenizer()
                Dim lTokens As List(Of Token) = lTokenizer.TokenizeLine(vLine)
                Dim lResult As New System.Text.StringBuilder()

                For Each lToken In lTokens
                    If lToken.Type = TokenType.eIdentifier AndAlso lToken.Text.Equals(vOldCase, StringComparison.OrdinalIgnoreCase) Then
                        lResult.Append(vNewCase)
                    Else
                        lResult.Append(lToken.Text)
                    End If
                Next

                Return lResult.ToString()

            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseInLine error: {ex.Message}")
                Return vLine
            End Try
        End Function

        ' ===== Helper Classes =====

        Private Class DeclarationPattern
            Public Property Pattern As String
            Public Property Scope As IdentifierScope

            Public Sub New(vPattern As String, vScope As IdentifierScope)
                Pattern = vPattern
                Scope = vScope
            End Sub
        End Class

        Private Class IdentifierDeclaration
            Public Property Name As String
            Public Property Scope As IdentifierScope
            Public Property Line As Integer

            Public Sub New(vName As String, vScope As IdentifierScope, vLine As Integer)
                Name = vName
                Scope = vScope
                Line = vLine
            End Sub
        End Class

    End Class

End Namespace
