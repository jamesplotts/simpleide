' CustomDrawingEditor.AutoEndConstruct.vb - Auto-inserts the matching "End ..." (or
' Next/Loop) construct, with the cursor placed on an indented body line ready to type,
' when Enter completes a block-opening statement (Sub, Function, Property, Class, Module,
' Namespace, Select Case, If...Then, For, Do, While, Try, With, Structure, Interface, Enum).
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' Modifier keywords that can precede a block-opening keyword (Public Shared
        ' ReadOnly Property, MustOverride Overrides Sub, etc.) - skipped over when looking
        ' for the actual keyword that determines what block this is
        Private Shared ReadOnly AutoEndModifierKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "Public", "Private", "Protected", "Friend", "Shared", "Overridable", "Overrides",
            "MustOverride", "NotOverridable", "MustInherit", "NotInheritable", "Partial",
            "ReadOnly", "WriteOnly", "Default", "Static", "Overloads", "Shadows",
            "Widening", "Narrowing", "Async", "Iterator"
        }

        ''' <summary>
        ''' Examines the line that Enter just completed and, if it opens a recognized block
        ''' construct, inserts the matching End/Next/Loop line(s) with the cursor left on an
        ''' indented body line ready to type
        ''' </summary>
        ''' <param name="vCompletedLineIndex">0-based index of the line Enter completed</param>
        ''' <returns>True if it handled the insertion (caller should skip normal auto-indent)</returns>
        Private Function TryAutoCompleteBlockStatement(vCompletedLineIndex As Integer) As Boolean
            Try
                If vCompletedLineIndex < 0 OrElse vCompletedLineIndex >= pLineCount Then Return False

                Dim lIndent As String = GetLineIndentation(vCompletedLineIndex)
                Dim lCode As String = StripLineComment(TextLines(vCompletedLineIndex)).Trim()
                If lCode.Length = 0 Then Return False

                Dim lWords As String() = lCode.Split(New Char() {" "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)
                If lWords.Length = 0 Then Return False

                Dim lIdx As Integer = 0
                Dim lSeenModifiers As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                While lIdx < lWords.Length AndAlso AutoEndModifierKeywords.Contains(lWords(lIdx))
                    lSeenModifiers.Add(lWords(lIdx))
                    lIdx += 1
                End While

                ' Abstract/external members (interface methods, MustOverride, Declare/P-Invoke)
                ' have no body to generate
                If lSeenModifiers.Contains("MustOverride") Then Return False
                If lIdx < lWords.Length AndAlso String.Equals(lWords(lIdx), "Declare", StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If

                If lIdx >= lWords.Length Then Return False
                Dim lKeyword As String = lWords(lIdx)
                Dim lUnit As String = GetTabIndentString()
                Dim lBodyIndent As String = lIndent & lUnit

                Select Case True
                    Case String.Equals(lKeyword, "Property", StringComparison.OrdinalIgnoreCase)
                        If IsInsideInterfaceBlock(vCompletedLineIndex, lIndent) Then Return False
                        InsertPropertyBody(vCompletedLineIndex, lIndent, lBodyIndent, lCode,
                                            lSeenModifiers.Contains("ReadOnly"), lSeenModifiers.Contains("WriteOnly"))
                        Return True

                    Case String.Equals(lKeyword, "Sub", StringComparison.OrdinalIgnoreCase)
                        If IsInsideInterfaceBlock(vCompletedLineIndex, lIndent) Then Return False
                        If LineAlreadyClosesBlock(lCode, "End") Then Return False
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Sub")
                        Return True

                    Case String.Equals(lKeyword, "Function", StringComparison.OrdinalIgnoreCase)
                        If IsInsideInterfaceBlock(vCompletedLineIndex, lIndent) Then Return False
                        If LineAlreadyClosesBlock(lCode, "End") Then Return False
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Function")
                        Return True

                    Case String.Equals(lKeyword, "Class", StringComparison.OrdinalIgnoreCase)
                        InsertSpacedBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Class")
                        Return True

                    Case String.Equals(lKeyword, "Module", StringComparison.OrdinalIgnoreCase)
                        InsertSpacedBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Module")
                        Return True

                    Case String.Equals(lKeyword, "Namespace", StringComparison.OrdinalIgnoreCase)
                        InsertSpacedBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Namespace")
                        Return True

                    Case String.Equals(lKeyword, "Structure", StringComparison.OrdinalIgnoreCase)
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Structure")
                        Return True

                    Case String.Equals(lKeyword, "Interface", StringComparison.OrdinalIgnoreCase)
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Interface")
                        Return True

                    Case String.Equals(lKeyword, "Enum", StringComparison.OrdinalIgnoreCase)
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Enum")
                        Return True

                    Case String.Equals(lKeyword, "Select", StringComparison.OrdinalIgnoreCase)
                        If lIdx + 1 < lWords.Length AndAlso String.Equals(lWords(lIdx + 1), "Case", StringComparison.OrdinalIgnoreCase) Then
                            ' If the switched value is a simple identifier whose type resolves
                            ' to a known Enum, generate one Case per member (declaration order,
                            ' including eUnspecified/eLastValue) plus Case Else, instead of an
                            ' empty Select Case body
                            Dim lCaseKeywordIndex As Integer = lCode.IndexOf("Case", StringComparison.OrdinalIgnoreCase)
                            Dim lSwitchExpr As String = lCode.Substring(lCaseKeywordIndex + 4).Trim()
                            Dim lEnumNode As SyntaxNode = Nothing
                            If IsSimpleIdentifierToken(lSwitchExpr) Then
                                lEnumNode = TryGetEnumNodeForSelectCase(lSwitchExpr, vCompletedLineIndex)
                            End If

                            If lEnumNode IsNot Nothing Then
                                InsertSelectCaseEnumBody(lIndent, lBodyIndent, lEnumNode)
                            Else
                                InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Select")
                            End If
                            Return True
                        End If
                        Return False

                    Case String.Equals(lKeyword, "If", StringComparison.OrdinalIgnoreCase)
                        ' Only the multi-line form ("If x Then" with nothing after Then) needs
                        ' "End If" - "If x Then y" is a complete single-line statement
                        If String.Equals(lWords(lWords.Length - 1), "Then", StringComparison.OrdinalIgnoreCase) Then
                            InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End If")
                            Return True
                        End If
                        Return False

                    Case String.Equals(lKeyword, "For", StringComparison.OrdinalIgnoreCase)
                        If LineAlreadyClosesBlock(lCode, "Next") Then Return False
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "Next")
                        Return True

                    Case String.Equals(lKeyword, "Do", StringComparison.OrdinalIgnoreCase)
                        If LineAlreadyClosesBlock(lCode, "Loop") Then Return False
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "Loop")
                        Return True

                    Case String.Equals(lKeyword, "While", StringComparison.OrdinalIgnoreCase)
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End While")
                        Return True

                    Case String.Equals(lKeyword, "Try", StringComparison.OrdinalIgnoreCase)
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End Try")
                        Return True

                    Case String.Equals(lKeyword, "With", StringComparison.OrdinalIgnoreCase)
                        InsertSimpleBody(vCompletedLineIndex, lIndent, lBodyIndent, "End With")
                        Return True

                    Case Else
                        Return False
                End Select

            Catch ex As Exception
                Console.WriteLine($"TryAutoCompleteBlockStatement error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Inserts a single indented blank body line (where the cursor ends up) followed by
        ''' the closing line at the opener's own indent - used for Sub/Function/Select
        ''' Case/If/For/Do/While/Try/With/Structure/Interface/Enum
        ''' </summary>
        Private Sub InsertSimpleBody(vCompletedLineIndex As Integer, vIndent As String, vBodyIndent As String, vClosingText As String)
            Dim lInsertText As String = vBodyIndent & Environment.NewLine & vIndent & vClosingText
            InsertBlockText(lInsertText, New EditorPosition(pCursorLine, vBodyIndent.Length))
        End Sub

        ''' <summary>
        ''' Inserts the Class/Module/Namespace pattern: a blank line, then an indented blank
        ''' line (where the cursor ends up), then another blank line, then the closing line
        ''' </summary>
        Private Sub InsertSpacedBody(vCompletedLineIndex As Integer, vIndent As String, vBodyIndent As String, vClosingText As String)
            Dim lInsertText As String =
                Environment.NewLine & vBodyIndent & Environment.NewLine & Environment.NewLine & vIndent & vClosingText
            InsertBlockText(lInsertText, New EditorPosition(pCursorLine + 1, vBodyIndent.Length))
        End Sub

        ''' <summary>
        ''' Inserts a Property's Get/Set skeleton based on which of ReadOnly/WriteOnly (if
        ''' either) was present on the declaration line, using the property's declared type
        ''' (defaulting to Object if none is given) for the Set accessor's parameter
        ''' </summary>
        Private Sub InsertPropertyBody(vCompletedLineIndex As Integer, vIndent As String, vBodyIndent As String,
                                        vDeclarationCode As String, vIsReadOnly As Boolean, vIsWriteOnly As Boolean)
            Dim lType As String = GetPropertyTypeFromDeclaration(vDeclarationCode)
            Dim lInnerIndent As String = vBodyIndent & GetTabIndentString()
            Dim lSb As New StringBuilder()
            Dim lCursorLineOffset As Integer = 1 ' Get/Set skeleton always starts with an accessor line, cursor is the next line

            If Not vIsWriteOnly Then
                lSb.Append(vBodyIndent).Append("Get").Append(Environment.NewLine)
                lSb.Append(lInnerIndent).Append(Environment.NewLine)
                lSb.Append(vBodyIndent).Append("End Get")
                If Not vIsReadOnly Then lSb.Append(Environment.NewLine)
            End If

            If Not vIsReadOnly Then
                lSb.Append(vBodyIndent).Append("Set(value As ").Append(lType).Append(")").Append(Environment.NewLine)
                lSb.Append(lInnerIndent).Append(Environment.NewLine)
                lSb.Append(vBodyIndent).Append("End Set")
            End If

            lSb.Append(Environment.NewLine).Append(vIndent).Append("End Property")

            InsertBlockText(lSb.ToString(), New EditorPosition(pCursorLine + lCursorLineOffset, lInnerIndent.Length))
        End Sub

        ''' <summary>
        ''' Extracts the property's declared type from its declaration line (text after the
        ''' last " As " outside of the parameter list, to correctly handle indexed
        ''' properties), defaulting to "Object" if none is declared
        ''' </summary>
        Private Function GetPropertyTypeFromDeclaration(vDeclarationCode As String) As String
            Try
                Dim lAsIndex As Integer = vDeclarationCode.LastIndexOf(" As ", StringComparison.OrdinalIgnoreCase)
                If lAsIndex < 0 Then Return "Object"

                Dim lType As String = vDeclarationCode.Substring(lAsIndex + 4).Trim()
                ' Strip a trailing "Implements ..." clause if present
                Dim lImplementsIndex As Integer = lType.IndexOf(" Implements ", StringComparison.OrdinalIgnoreCase)
                If lImplementsIndex >= 0 Then lType = lType.Substring(0, lImplementsIndex).Trim()

                Return If(String.IsNullOrEmpty(lType), "Object", lType)

            Catch ex As Exception
                Console.WriteLine($"GetPropertyTypeFromDeclaration error: {ex.Message}")
                Return "Object"
            End Try
        End Function

        ''' <summary>
        ''' Inserts vText at the current cursor position (the freshly-created blank line
        ''' InsertNewLine already left the cursor on), records it for undo, and places the
        ''' cursor at vCursorTarget for typing
        ''' </summary>
        Private Sub InsertBlockText(vText As String, vCursorTarget As EditorPosition)
            Try
                Dim lInsertPos As New EditorPosition(pCursorLine, pCursorColumn)

                Dim lSegments As String() = vText.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
                Dim lEndLine As Integer = pCursorLine + lSegments.Length - 1
                Dim lEndColumn As Integer = lSegments(lSegments.Length - 1).Length
                Dim lEndPos As New EditorPosition(lEndLine, lEndColumn)

                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordInsertText(lInsertPos, vText, lEndPos)
                End If

                pSourceFileInfo.InsertText(pCursorLine, pCursorColumn, vText)
                SetCursorPosition(vCursorTarget.Line, vCursorTarget.Column)

            Catch ex As Exception
                Console.WriteLine($"InsertBlockText error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks (by indentation, as a proxy for enclosing scope) whether vLineIndex sits
        ''' directly inside an Interface block - interface members are declarations only and
        ''' never get a generated body
        ''' </summary>
        Private Function IsInsideInterfaceBlock(vLineIndex As Integer, vIndent As String) As Boolean
            Try
                for lLine As Integer = vLineIndex - 1 To 0 Step -1
                    Dim lLineText As String = TextLines(lLine)
                    Dim lTrimmed As String = StripLineComment(lLineText).Trim()
                    If lTrimmed.Length = 0 Then Continue for

                    Dim lLineIndent As String = GetLineIndentation(lLine)
                    If lLineIndent.Length >= vIndent.Length Then Continue for

                    Dim lWords As String() = lTrimmed.Split(New Char() {" "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)
                    Dim lIdx As Integer = 0
                    While lIdx < lWords.Length AndAlso AutoEndModifierKeywords.Contains(lWords(lIdx))
                        lIdx += 1
                    End While
                    If lIdx >= lWords.Length Then Return False

                    Return String.Equals(lWords(lIdx), "Interface", StringComparison.OrdinalIgnoreCase)
                Next

            Catch ex As Exception
                Console.WriteLine($"IsInsideInterfaceBlock error: {ex.Message}")
            End Try
            Return False
        End Function

        ''' <summary>
        ''' Best-effort check for whether a single-line form of the block already closes
        ''' itself on the same line (e.g. "For i = 1 To 10 : Console.WriteLine(i) : Next")
        ''' </summary>
        Private Function LineAlreadyClosesBlock(vCode As String, vClosingKeyword As String) As Boolean
            Dim lWords As String() = vCode.Split(New Char() {" "c, ControlChars.Tab, ":"c}, StringSplitOptions.RemoveEmptyEntries)
            for each lWord As String in lWords
                If String.Equals(lWord, vClosingKeyword, StringComparison.OrdinalIgnoreCase) Then Return True
            Next
            Return False
        End Function

        ''' <summary>
        ''' Strips a trailing line comment (text from an unquoted "'" to end of line)
        ''' </summary>
        Private Function StripLineComment(vLine As String) As String
            Dim lInString As Boolean = False
            for i As Integer = 0 To vLine.Length - 1
                Dim lChar As Char = vLine(i)
                If lChar = """"c Then
                    lInString = Not lInString
                ElseIf lChar = "'"c AndAlso Not lInString Then
                    Return vLine.Substring(0, i)
                End If
            Next
            Return vLine
        End Function

        ''' <summary>
        ''' Builds a Select Case body with one Case per Enum member (in declaration order,
        ''' including sentinel values like eUnspecified/eLastValue - matching what a real
        ''' exhaustive-switch tool would generate rather than guessing which members the
        ''' developer wants), plus a trailing Case Else, cursor on the first Case's body
        ''' </summary>
        Private Sub InsertSelectCaseEnumBody(vIndent As String, vBodyIndent As String, vEnumNode As SyntaxNode)
            Dim lEnumTypeName As String = vEnumNode.Name
            Dim lInnerIndent As String = vBodyIndent & GetTabIndentString()
            Dim lLines As New List(Of String)()
            Dim lFirstBodyLineIndex As Integer = -1

            If vEnumNode.Children IsNot Nothing Then
                for each lMember As SyntaxNode in vEnumNode.Children
                    If lMember.NodeType <> CodeNodeType.eEnumValue Then Continue for
                    lLines.Add(vBodyIndent & "Case " & lEnumTypeName & "." & lMember.Name)
                    If lFirstBodyLineIndex = -1 Then lFirstBodyLineIndex = lLines.Count
                    lLines.Add(lInnerIndent)
                Next
            End If

            lLines.Add(vBodyIndent & "Case Else")
            If lFirstBodyLineIndex = -1 Then lFirstBodyLineIndex = lLines.Count
            lLines.Add(lInnerIndent)

            lLines.Add(vIndent & "End Select")

            Dim lInsertText As String = String.Join(Environment.NewLine, lLines)
            InsertBlockText(lInsertText, New EditorPosition(pCursorLine + lFirstBodyLineIndex, lInnerIndent.Length))
        End Sub

        ''' <summary>
        ''' True if vText is a single bare identifier (letters/digits/underscore only,
        ''' starting with a letter or underscore) - used to restrict enum-aware Select Case
        ''' generation to simple "Select Case someVariable" forms rather than attempting to
        ''' type-infer arbitrary expressions
        ''' </summary>
        Private Function IsSimpleIdentifierToken(vText As String) As Boolean
            If String.IsNullOrEmpty(vText) Then Return False
            If Not (Char.IsLetter(vText(0)) OrElse vText(0) = "_"c) Then Return False
            for each lChar As Char in vText
                If Not (Char.IsLetterOrDigit(lChar) OrElse lChar = "_"c) Then Return False
            Next
            Return True
        End Function

        ''' <summary>
        ''' Resolves vIdentifier's declared type (as seen at vLine) and, if that type names a
        ''' known Enum, returns the Enum's SyntaxNode
        ''' </summary>
        Private Function TryGetEnumNodeForSelectCase(vIdentifier As String, vLine As Integer) As SyntaxNode
            Try
                Dim lTypeName As String = ResolveIdentifierTypeName(vIdentifier, vLine)
                If String.IsNullOrEmpty(lTypeName) Then Return Nothing

                ' Use the simple name in case the declared type was written fully qualified
                Dim lSimpleTypeName As String = lTypeName.Split("."c).Last()

                ' Same-file lookup first - pRootNode preserves declaration order, which
                ' matters for enum members (the project-wide tree sorts children
                ' alphabetically, which would scramble eUnspecified...eLastValue ordering)
                Dim lLocalEnum As SyntaxNode = FindEnumNodeByName(pRootNode, lSimpleTypeName)
                If lLocalEnum IsNot Nothing Then Return lLocalEnum

                If pProjectManager Is Nothing Then Return Nothing
                Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                If lProjectTree Is Nothing Then Return Nothing

                Dim lFoundInMergedTree As SyntaxNode = FindEnumNodeByName(lProjectTree, lSimpleTypeName)
                If lFoundInMergedTree Is Nothing OrElse String.IsNullOrEmpty(lFoundInMergedTree.FilePath) Then Return Nothing

                ' Re-fetch that file's own unsorted tree so the member order is correct
                Dim lFileInfo As SourceFileInfo = pProjectManager.GetSourceFileInfo(lFoundInMergedTree.FilePath)
                If lFileInfo Is Nothing OrElse lFileInfo.SyntaxTree Is Nothing Then Return lFoundInMergedTree

                Return If(FindEnumNodeByName(lFileInfo.SyntaxTree, lSimpleTypeName), lFoundInMergedTree)

            Catch ex As Exception
                Console.WriteLine($"TryGetEnumNodeForSelectCase error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Resolves vName's declared type name as seen at vLine: parameters and local
        ''' Dim/Const variables of the containing member first (correct VB scoping - these
        ''' shadow class members), then fields/properties anywhere in the project (reusing
        ''' the same project-wide lookup the hover-declaration tooltip uses)
        ''' </summary>
        Private Function ResolveIdentifierTypeName(vName As String, vLine As Integer) As String
            Try
                Dim lMemberNode As SyntaxNode = FindContainingMemberNode(pRootNode, vLine)
                If lMemberNode IsNot Nothing Then
                    If lMemberNode.Parameters IsNot Nothing Then
                        for each lParam As ParameterInfo in lMemberNode.Parameters
                            If String.Equals(lParam.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                                Return lParam.ParameterType
                            End If
                        Next
                    End If

                    If lMemberNode.Children IsNot Nothing Then
                        for each lChild As SyntaxNode in lMemberNode.Children
                            If lChild.NodeType = CodeNodeType.eVariable AndAlso
                               String.Equals(lChild.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                                Return lChild.DataType
                            End If
                        Next
                    End If
                End If

                If pProjectManager IsNot Nothing Then
                    Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                    If lProjectTree IsNot Nothing Then
                        Dim lMember As SyntaxNode = FindProjectMemberNode(lProjectTree, vName)
                        If lMember IsNot Nothing AndAlso (lMember.NodeType = CodeNodeType.eField OrElse lMember.NodeType = CodeNodeType.eProperty) Then
                            Return If(Not String.IsNullOrEmpty(lMember.DataType), lMember.DataType, lMember.ReturnType)
                        End If
                    End If
                End If

                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"ResolveIdentifierTypeName error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Recursively finds the first Enum node named vName
        ''' </summary>
        Private Function FindEnumNodeByName(vNode As SyntaxNode, vName As String) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                If vNode.NodeType = CodeNodeType.eEnum AndAlso String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                    Return vNode
                End If

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindEnumNodeByName(lChild, vName)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"FindEnumNodeByName error: {ex.Message}")
            End Try
            Return Nothing
        End Function

    End Class

End Namespace
