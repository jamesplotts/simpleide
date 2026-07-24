' CustomDrawingEditor.GenerateFieldFromParameter.vb - "Generate Field(s) From Parameters"
' quick action: for a Sub/Function/constructor parameter like "vTest As Integer" with no
' matching field yet, generates "Private pTest As Integer" above the method (and its XML
' doc comment, if any) plus "pTest = vTest" as the first line of the method body - built
' around this codebase's v-parameter/p-field Hungarian-notation convention.
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' Returns the parameters of the member containing the cursor that don't already
        ''' have a matching field in the containing class, or an empty list if the cursor
        ''' isn't inside a member with parameters (used both to decide whether to show the
        ''' context menu item and to perform the generation)
        ''' </summary>
        Private Function GetGenerateFieldCandidates() As List(Of ParameterInfo)
            Dim lResult As New List(Of ParameterInfo)()
            Try
                Dim lMemberNode As SyntaxNode = FindContainingMemberNode(pRootNode, pCursorLine)
                If lMemberNode Is Nothing OrElse lMemberNode.Parameters Is Nothing OrElse lMemberNode.Parameters.Count = 0 Then
                    Return lResult
                End If

                Dim lClassNode As SyntaxNode = lMemberNode.Parent
                If lClassNode Is Nothing OrElse lClassNode.Children Is Nothing Then Return lResult

                Dim lExistingFieldNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                for each lMember As SyntaxNode in lClassNode.Children
                    If lMember.NodeType = CodeNodeType.eField Then lExistingFieldNames.Add(lMember.Name)
                Next

                for each lParam As ParameterInfo in lMemberNode.Parameters
                    If Not lExistingFieldNames.Contains(GetFieldNameForParameter(lParam.Name)) Then
                        lResult.Add(lParam)
                    End If
                Next

            Catch ex As Exception
                Console.WriteLine($"GetGenerateFieldCandidates error: {ex.Message}")
            End Try
            Return lResult
        End Function

        ''' <summary>
        ''' Generates backing fields and constructor-body assignments for every parameter of
        ''' the member containing the cursor that doesn't already have a matching field
        ''' </summary>
        Private Sub GenerateFieldsFromParameters()
            Try
                Dim lMemberNode As SyntaxNode = FindContainingMemberNode(pRootNode, pCursorLine)
                If lMemberNode Is Nothing Then Return

                Dim lCandidates As List(Of ParameterInfo) = GetGenerateFieldCandidates()
                If lCandidates.Count = 0 Then Return

                Dim lFieldIndent As String = GetLineIndentation(lMemberNode.StartLine)
                Dim lBodyIndent As String = lFieldIndent & GetTabIndentString()

                ' Insert the field declarations above the member - and above its XML doc
                ' comment block, if any, so the doc stays attached to the method
                Dim lInsertFieldsAtLine As Integer = lMemberNode.StartLine
                While lInsertFieldsAtLine > 0 AndAlso TextLines(lInsertFieldsAtLine - 1).TrimStart().StartsWith("'''")
                    lInsertFieldsAtLine -= 1
                End While

                Dim lFieldLines As New List(Of String)()
                for each lParam As ParameterInfo in lCandidates
                    Dim lType As String = If(String.IsNullOrEmpty(lParam.ParameterType), "Object", lParam.ParameterType)
                    lFieldLines.Add(lFieldIndent & "Private " & GetFieldNameForParameter(lParam.Name) & " As " & lType)
                Next
                InsertLinesBefore(lInsertFieldsAtLine, String.Join(Environment.NewLine, lFieldLines) & Environment.NewLine)

                ' The field insertion shifted every subsequent line down
                Dim lFieldLineCount As Integer = lFieldLines.Count
                Dim lShiftedMemberStartLine As Integer = lMemberNode.StartLine + lFieldLineCount

                ' Find where the (possibly multi-line) parameter list actually closes, so the
                ' assignments go right after the signature rather than into the middle of it
                Dim lSignatureEndLine As Integer = FindSignatureEndLine(lShiftedMemberStartLine)

                Dim lAssignLines As New List(Of String)()
                for each lParam As ParameterInfo in lCandidates
                    lAssignLines.Add(lBodyIndent & GetFieldNameForParameter(lParam.Name) & " = " & lParam.Name)
                Next
                InsertLinesBefore(lSignatureEndLine + 1, String.Join(Environment.NewLine, lAssignLines) & Environment.NewLine)

                Dim lTotalShift As Integer = lFieldLineCount + lAssignLines.Count
                SetCursorPosition(pCursorLine + lTotalShift, pCursorColumn)

                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"GenerateFieldsFromParameters error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Derives a backing field name from a parameter name using this codebase's v->p
        ''' Hungarian-notation convention ("vTest" -> "pTest"), falling back to just
        ''' capitalizing and prefixing "p" for parameters that don't follow it
        ''' </summary>
        Private Function GetFieldNameForParameter(vParamName As String) As String
            If String.IsNullOrEmpty(vParamName) Then Return "pValue"
            If vParamName.Length > 1 AndAlso vParamName(0) = "v"c AndAlso Char.IsUpper(vParamName(1)) Then
                Return "p" & vParamName.Substring(1)
            End If
            Return "p" & Char.ToUpper(vParamName(0)) & vParamName.Substring(1)
        End Function

        ''' <summary>
        ''' Scans forward from vStartLine for the parameter list's closing ")" (VB allows a
        ''' parameter list to span multiple lines without a line-continuation character),
        ''' returning the line it's found on
        ''' </summary>
        Private Function FindSignatureEndLine(vStartLine As Integer) As Integer
            Dim lDepth As Integer = 0
            Dim lFoundOpen As Boolean = False
            for lLine As Integer = vStartLine To pLineCount - 1
                Dim lText As String = TextLines(lLine)
                for lCol As Integer = 0 To lText.Length - 1
                    Select Case lText(lCol)
                        Case "("c
                            lDepth += 1
                            lFoundOpen = True
                        Case ")"c
                            lDepth -= 1
                            If lFoundOpen AndAlso lDepth = 0 Then Return lLine
                    End Select
                Next
            Next
            Return vStartLine
        End Function

        ''' <summary>
        ''' Inserts vText (which must end with a newline) as whole new lines immediately
        ''' before vLine, recording it for undo
        ''' </summary>
        Private Sub InsertLinesBefore(vLine As Integer, vText As String)
            Try
                Dim lPos As New EditorPosition(vLine, 0)
                Dim lSegments As String() = vText.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
                Dim lEndPos As New EditorPosition(vLine + lSegments.Length - 1, lSegments(lSegments.Length - 1).Length)

                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordInsertText(lPos, vText, lEndPos)
                End If

                pSourceFileInfo.InsertText(vLine, 0, vText)

            Catch ex As Exception
                Console.WriteLine($"InsertLinesBefore error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
