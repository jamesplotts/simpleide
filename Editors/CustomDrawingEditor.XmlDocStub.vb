' CustomDrawingEditor.XmlDocStub.vb - Auto-expands "'''" typed alone on a line into a full
' XML doc comment skeleton for the member declared on the next line, matching CLAUDE.md's
' mandatory XML documentation convention: <summary> for everyone, <param> per parameter,
' <returns> for Functions, <value> for Properties.
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' Checks whether the just-typed character completed a bare "'''" line with a
        ''' recognized member declaration on the next line, and if so expands it into a full
        ''' XML doc skeleton
        ''' </summary>
        ''' <param name="vChar">The character that was just typed</param>
        Private Sub CheckXmlDocTrigger(vChar As Char)
            Try
                If vChar <> "'"c Then Return
                If pCursorLine < 0 OrElse pCursorLine >= pLineCount Then Return

                Dim lLineText As String = TextLines(pCursorLine)
                If lLineText.Trim() <> "'''" Then Return
                If pCursorColumn <> lLineText.Length Then Return ' must be at the end of the "'''" just typed

                Dim lTargetLine As Integer = pCursorLine + 1
                If lTargetLine >= pLineCount Then Return

                Dim lTargetCode As String = StripLineComment(TextLines(lTargetLine)).Trim()
                If lTargetCode.Length = 0 Then Return

                ' Concatenate a multi-line parameter list (VB allows this without a line
                ' continuation character inside parens) so parameter names aren't missed -
                ' only scan forward if there's actually an unmatched "(" to anchor the search
                Dim lFullSignatureText As String = lTargetCode
                If lTargetCode.Contains("("c) Then
                    Dim lSigEndLine As Integer = FindSignatureEndLine(lTargetLine)
                    If lSigEndLine > lTargetLine Then
                        Dim lParts As New List(Of String) From {lTargetCode}
                        for lLine As Integer = lTargetLine + 1 To lSigEndLine
                            lParts.Add(StripLineComment(TextLines(lLine)).Trim())
                        Next
                        lFullSignatureText = String.Join(" ", lParts)
                    End If
                End If

                Dim lIndent As String = GetLineIndentation(pCursorLine)
                Dim lBodyLines As List(Of String) = BuildXmlDocBodyLines(lTargetCode, lFullSignatureText, lIndent)
                If lBodyLines Is Nothing Then Return ' not a recognized declaration - leave "'''" as a plain comment

                ' lBodyLines(0) is the blank summary-content line - append " <summary>" to
                ' what's already typed, then the rest of the skeleton as new lines
                Dim lInsertText As String = " <summary>" & Environment.NewLine & String.Join(Environment.NewLine, lBodyLines)

                Dim lInsertPos As New EditorPosition(pCursorLine, pCursorColumn)
                If pUndoRedoManager IsNot Nothing Then
                    Dim lSegments As String() = lInsertText.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
                    Dim lEndPos As New EditorPosition(pCursorLine + lSegments.Length - 1, lSegments(lSegments.Length - 1).Length)
                    pUndoRedoManager.RecordInsertText(lInsertPos, lInsertText, lEndPos)
                End If
                pSourceFileInfo.InsertText(pCursorLine, pCursorColumn, lInsertText)

                ' Cursor lands on the blank summary-content line, ready to type
                SetCursorPosition(pCursorLine + 1, lBodyLines(0).Length)

                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"CheckXmlDocTrigger error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds the doc-skeleton lines that follow the "'''  &lt;summary&gt;" line: a
        ''' blank summary-content line, the closing "&lt;/summary&gt;" tag, one
        ''' "&lt;param&gt;" per parameter (Sub/Function/Property/Event only), and
        ''' "&lt;returns&gt;" (Function) or "&lt;value&gt;" (Property) - or Nothing if
        ''' vFirstLineCode doesn't look like a recognized declaration
        ''' </summary>
        Private Function BuildXmlDocBodyLines(vFirstLineCode As String, vFullSignatureText As String, vIndent As String) As List(Of String)
            Try
                Dim lWords As String() = vFirstLineCode.Split(New Char() {" "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)
                If lWords.Length = 0 Then Return Nothing

                Dim lIdx As Integer = 0
                While lIdx < lWords.Length AndAlso AutoEndModifierKeywords.Contains(lWords(lIdx))
                    lIdx += 1
                End While
                If lIdx >= lWords.Length Then Return Nothing
                Dim lKeyword As String = lWords(lIdx)

                Dim lIsFunction As Boolean = String.Equals(lKeyword, "Function", StringComparison.OrdinalIgnoreCase)
                Dim lIsProperty As Boolean = String.Equals(lKeyword, "Property", StringComparison.OrdinalIgnoreCase)
                Dim lHasParams As Boolean = String.Equals(lKeyword, "Sub", StringComparison.OrdinalIgnoreCase) OrElse
                                             lIsFunction OrElse lIsProperty OrElse
                                             String.Equals(lKeyword, "Event", StringComparison.OrdinalIgnoreCase) OrElse
                                             String.Equals(lKeyword, "Delegate", StringComparison.OrdinalIgnoreCase)

                Dim lRecognized As Boolean = lHasParams OrElse
                    String.Equals(lKeyword, "Class", StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(lKeyword, "Module", StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(lKeyword, "Structure", StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(lKeyword, "Interface", StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(lKeyword, "Enum", StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(lKeyword, "Namespace", StringComparison.OrdinalIgnoreCase)

                If Not lRecognized Then Return Nothing

                Dim lLines As New List(Of String)()
                Dim lPrefix As String = vIndent & "''' "

                lLines.Add(lPrefix) ' blank summary content line - cursor target
                lLines.Add(lPrefix & "</summary>")

                If lHasParams Then
                    for each lParamName As String in GetParameterNamesFromSignature(vFullSignatureText)
                        lLines.Add(lPrefix & $"<param name=""{lParamName}"">Description of parameter purpose and valid values</param>")
                    Next
                End If

                If lIsFunction Then
                    lLines.Add(lPrefix & "<returns>Description of return value</returns>")
                ElseIf lIsProperty Then
                    lLines.Add(lPrefix & "<value>Description of the property's value</value>")
                End If

                Return lLines

            Catch ex As Exception
                Console.WriteLine($"BuildXmlDocBodyLines error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Extracts parameter names from a (possibly multi-line-concatenated) signature's
        ''' parameter list, skipping ByRef/ByVal/Optional/ParamArray modifiers - depth-aware
        ''' across nested parens/braces/brackets so default values like array literals don't
        ''' get mistaken for parameter separators
        ''' </summary>
        Private Function GetParameterNamesFromSignature(vFullSignatureText As String) As List(Of String)
            Dim lResult As New List(Of String)()
            Try
                Dim lOpenIndex As Integer = vFullSignatureText.IndexOf("("c)
                If lOpenIndex < 0 Then Return lResult

                Dim lDepth As Integer = 0
                Dim lEndIndex As Integer = -1
                for i As Integer = lOpenIndex To vFullSignatureText.Length - 1
                    Select Case vFullSignatureText(i)
                        Case "("c
                            lDepth += 1
                        Case ")"c
                            lDepth -= 1
                            If lDepth = 0 Then
                                lEndIndex = i
                                Exit for
                            End If
                    End Select
                Next
                If lEndIndex < 0 Then Return lResult

                Dim lParamsText As String = vFullSignatureText.Substring(lOpenIndex + 1, lEndIndex - lOpenIndex - 1)
                If String.IsNullOrWhiteSpace(lParamsText) Then Return lResult

                Dim lChunks As New List(Of String)()
                Dim lChunkStart As Integer = 0
                Dim lChunkDepth As Integer = 0
                for i As Integer = 0 To lParamsText.Length - 1
                    Select Case lParamsText(i)
                        Case "("c, "{"c, "["c
                            lChunkDepth += 1
                        Case ")"c, "}"c, "]"c
                            lChunkDepth -= 1
                        Case ","c
                            If lChunkDepth = 0 Then
                                lChunks.Add(lParamsText.Substring(lChunkStart, i - lChunkStart))
                                lChunkStart = i + 1
                            End If
                    End Select
                Next
                lChunks.Add(lParamsText.Substring(lChunkStart))

                Dim lParamModifiers As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {"Optional", "ByRef", "ByVal", "ParamArray"}
                for each lChunk As String in lChunks
                    Dim lWords As String() = lChunk.Trim().Split(New Char() {" "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)
                    Dim lIdx As Integer = 0
                    While lIdx < lWords.Length AndAlso lParamModifiers.Contains(lWords(lIdx))
                        lIdx += 1
                    End While
                    If lIdx < lWords.Length Then
                        Dim lName As String = lWords(lIdx).Split("("c)(0)
                        If lName.Length > 0 Then lResult.Add(lName)
                    End If
                Next

            Catch ex As Exception
                Console.WriteLine($"GetParameterNamesFromSignature error: {ex.Message}")
            End Try
            Return lResult
        End Function

    End Class

End Namespace
