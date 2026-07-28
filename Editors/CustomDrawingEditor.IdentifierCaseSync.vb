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
        ''' Scans the file's content once, right after it loads, and records each declaration's
        ''' AS-LOADED casing as canonical - before the user has had any chance to edit anything
        ''' </summary>
        ''' <remarks>
        ''' Without this, the map is only ever seeded lazily the first time a given declaration
        ''' line is exited (see ProcessLineFormattingWithDeclarationTracking). If that first exit
        ''' happens to be right after the user's own rename - which is exactly what happens for
        ''' any declaration nobody has visited-and-left yet this session - there's nothing to
        ''' compare the new casing against, so it silently gets recorded as the baseline instead
        ''' of being recognized as a change. Eager seeding closes that gap.
        ''' </remarks>
        Private Sub SeedIdentifierCaseMapsFromCurrentContent()
            Try
                If pSourceFileInfo Is Nothing Then Return
                Dim lLines As IList(Of String) = pSourceFileInfo.TextLines
                If lLines Is Nothing Then Return

                for i As Integer = 0 To lLines.Count - 1
                    Dim lDeclarations As List(Of IdentifierDeclaration) = ExtractDeclarations(lLines(i))
                    If lDeclarations.Count = 0 Then Continue For

                    Dim lContainingMember As MemberScope = FindContainingMemberScope(i)

                    for each lDecl in lDeclarations
                        Dim lIsLocalLike As Boolean = IsLocalLikeScope(lDecl.Scope) AndAlso lContainingMember IsNot Nothing
                        If lIsLocalLike Then
                            Dim lKey As String = $"{lContainingMember.ScopeKey}::{lDecl.Name}"
                            If Not pLocalIdentifierCaseMap.ContainsKey(lKey) Then
                                pLocalIdentifierCaseMap(lKey) = lDecl.Name
                            End If
                        Else
                            If Not pIdentifierCaseMap.ContainsKey(lDecl.Name) Then
                                UpdateIdentifierCaseMap(lDecl.Name, lDecl.Name)
                            End If
                        End If
                    Next
                Next

            Catch ex As Exception
                Console.WriteLine($"SeedIdentifierCaseMapsFromCurrentContent error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Examines a just-exited line: if it's a declaration, detects and propagates any
        ''' casing change against the previously recorded canonical casing; either way, also
        ''' corrects any identifier reference on the line whose casing has drifted out of sync
        ''' with whatever's already known to be canonical (e.g. a usage that was retyped with
        ''' the wrong case, rather than the declaration itself being renamed)
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based index of the line that was exited</param>
        Private Sub ProcessLineFormattingWithDeclarationTracking(vLineIndex As Integer)
            Try
                If pSourceFileInfo Is Nothing Then Return
                If vLineIndex < 0 OrElse vLineIndex >= pSourceFileInfo.TextLines.Count Then Return

                Dim lLineText As String = pSourceFileInfo.TextLines(vLineIndex)
                If String.IsNullOrEmpty(lLineText.Trim()) Then Return

                Dim lContainingMember As MemberScope = FindContainingMemberScope(vLineIndex)

                Dim lDeclarations As List(Of IdentifierDeclaration) = ExtractDeclarations(lLineText)
                for each lDecl in lDeclarations
                    DetectAndApplyDeclarationCaseChange(lDecl, lContainingMember)
                Next

                ' Whether or not this line declared anything, conform any identifier reference
                ' on it to already-known canonical casing (this also re-checks the declared
                ' name itself, harmlessly, since propagation above already made it canonical)
                CorrectUsageCasingOnLine(vLineIndex, lContainingMember)

            Catch ex As Exception
                Console.WriteLine($"ProcessLineFormattingWithDeclarationTracking error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Rewrites any identifier token on the line whose casing doesn't match a
        ''' already-known canonical casing - the containing member's local map is checked
        ''' first (locals/parameters shadow same-named members, same as real VB.NET scoping),
        ''' falling back to the project-wide member map
        ''' </summary>
        ''' <remarks>
        ''' This does NOT update either map or propagate anywhere else - it only ever conforms
        ''' this one line to what's already canonical, so retyping a mere reference with the
        ''' wrong case corrects itself instead of being mistaken for a rename
        ''' </remarks>
        Private Sub CorrectUsageCasingOnLine(vLineIndex As Integer, vContainingMember As MemberScope)
            Try
                If pSourceFileInfo Is Nothing Then Return
                If vLineIndex < 0 OrElse vLineIndex >= pSourceFileInfo.TextLines.Count Then Return

                Dim lLineText As String = pSourceFileInfo.TextLines(vLineIndex)
                If String.IsNullOrEmpty(lLineText.Trim()) Then Return

                Dim lTokenizer As New VBTokenizer()
                Dim lTokens As List(Of Token) = lTokenizer.TokenizeLine(lLineText)
                Dim lResult As New System.Text.StringBuilder()
                Dim lChanged As Boolean = False

                for each lToken in lTokens
                    If lToken.Type = TokenType.eIdentifier Then
                        Dim lCanonical As String = Nothing
                        Dim lFound As Boolean = False

                        If vContainingMember IsNot Nothing Then
                            lFound = pLocalIdentifierCaseMap.TryGetValue($"{vContainingMember.ScopeKey}::{lToken.Text}", lCanonical)
                        End If
                        If Not lFound Then
                            lFound = pIdentifierCaseMap.TryGetValue(lToken.Text, lCanonical)
                        End If

                        If lFound AndAlso Not lCanonical.Equals(lToken.Text, StringComparison.Ordinal) Then
                            lResult.Append(lCanonical)
                            lChanged = True
                        Else
                            lResult.Append(lToken.Text)
                        End If
                    Else
                        lResult.Append(lToken.Text)
                    End If
                Next

                If lChanged Then
                    pSourceFileInfo.TextLines(vLineIndex) = lResult.ToString()
                    pLineMetadata(vLineIndex).MarkChanged()
                    InvalidateLine(vLineIndex)
                    IsModified = True
                End If

            Catch ex As Exception
                Console.WriteLine($"CorrectUsageCasingOnLine error: {ex.Message}")
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
        ''' The innermost Sub/Function/Property containing the line, or Nothing if the line
        ''' isn't inside one (e.g. a field declared directly in a class body)
        ''' </param>
        Private Sub DetectAndApplyDeclarationCaseChange(vDecl As IdentifierDeclaration, vContainingMember As MemberScope)
            Try
                Dim lIsLocalLike As Boolean = IsLocalLikeScope(vDecl.Scope) AndAlso vContainingMember IsNot Nothing

                If lIsLocalLike Then
                    Dim lKey As String = $"{vContainingMember.ScopeKey}::{vDecl.Name}"

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

        ' Matches a Sub/Function/Property header line, capturing its name
        Private Shared ReadOnly MemberHeaderPattern As New Regex(
            "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:Overrides\s+|Overridable\s+|MustOverride\s+|NotOverridable\s+)?(?:Sub|Function|Property)\s+(\w+)",
            RegexOptions.IgnoreCase)

        ' Matches the End Sub/Function/Property that closes a member header
        Private Shared ReadOnly MemberEndPattern As New Regex("^\s*End\s+(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase)

        ' Matches a Class/Module/Structure header line, capturing its name
        Private Shared ReadOnly TypeHeaderPattern As New Regex(
            "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Partial\s+)?(?:Class|Module|Structure)\s+(\w+)",
            RegexOptions.IgnoreCase)

        ''' <summary>
        ''' Finds the innermost Sub/Function/Property whose header/End pair encloses vLineIndex,
        ''' via a plain text scan of the file's own lines - deliberately independent of the
        ''' (async, sometimes-stale-or-incomplete-while-typing) Roslyn syntax tree, since this
        ''' needs a stable, synchronous answer at the exact moment a line is exited
        ''' </summary>
        Private Function FindContainingMemberScope(vLineIndex As Integer) As MemberScope
            Try
                If pSourceFileInfo Is Nothing Then Return Nothing
                Dim lLines As IList(Of String) = pSourceFileInfo.TextLines
                If vLineIndex < 0 OrElse vLineIndex >= lLines.Count Then Return Nothing

                Dim lMemberStartLine As Integer = -1
                Dim lMemberName As String = Nothing

                for i As Integer = vLineIndex To 0 Step -1
                    Dim lLine As String = lLines(i)
                    Dim lHeaderMatch As Match = MemberHeaderPattern.Match(lLine)
                    If lHeaderMatch.Success Then
                        lMemberStartLine = i
                        lMemberName = lHeaderMatch.Groups(1).Value
                        Exit For
                    End If
                    ' A bare "End Sub/Function/Property" above vLineIndex (before finding any
                    ' header) means vLineIndex sits between members, not inside one
                    If i < vLineIndex AndAlso MemberEndPattern.IsMatch(lLine) Then Return Nothing
                Next

                If lMemberStartLine = -1 Then Return Nothing

                Dim lMemberEndLine As Integer = lLines.Count - 1
                for i As Integer = lMemberStartLine To lLines.Count - 1
                    If MemberEndPattern.IsMatch(lLines(i)) Then
                        lMemberEndLine = i
                        Exit For
                    End If
                Next

                If vLineIndex > lMemberEndLine Then Return Nothing

                Dim lTypeName As String = Nothing
                for i As Integer = lMemberStartLine To 0 Step -1
                    Dim lTypeMatch As Match = TypeHeaderPattern.Match(lLines(i))
                    If lTypeMatch.Success Then
                        lTypeName = lTypeMatch.Groups(1).Value
                        Exit For
                    End If
                Next

                Dim lScope As New MemberScope()
                lScope.StartLine = lMemberStartLine
                lScope.EndLine = lMemberEndLine
                lScope.ScopeKey = If(String.IsNullOrEmpty(lTypeName), lMemberName, $"{lTypeName}.{lMemberName}")
                Return lScope

            Catch ex As Exception
                Console.WriteLine($"FindContainingMemberScope error: {ex.Message}")
                Return Nothing
            End Try
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

        ''' <summary>
        ''' A Sub/Function/Property's line range and scope-qualifying key (e.g.
        ''' "ClassName.MethodName"), found via FindContainingMemberScope
        ''' </summary>
        Private Class MemberScope
            Public Property StartLine As Integer
            Public Property EndLine As Integer
            Public Property ScopeKey As String
        End Class

    End Class

End Namespace
