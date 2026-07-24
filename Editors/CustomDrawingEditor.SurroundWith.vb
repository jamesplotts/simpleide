' CustomDrawingEditor.SurroundWith.vb - "Surround Selection With" quick action: wraps the
' currently selected lines in Try/Catch, If, For, For Each, Using, or With, re-indenting the
' wrapped lines and leaving the cursor on (or a placeholder in) the opening line ready to type.
Imports Gtk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' The block constructs offered by "Surround Selection With"
        ''' </summary>
        Public Enum SurroundWithKind
            eUnspecified
            eTryCatch
            eIf
            eFor
            eForEach
            eUsing
            eWith
            eWhile
            eLastValue
        End Enum

        ''' <summary>
        ''' Wraps the currently selected lines in the given construct
        ''' </summary>
        Private Sub SurroundSelectionWith(vKind As SurroundWithKind)
            Try
                If Not pHasSelection Then Return

                Dim lStart As EditorPosition = GetSelectionStart()
                Dim lEnd As EditorPosition = GetSelectionEnd()
                Dim lStartLine As Integer = lStart.Line
                Dim lEndLine As Integer = lEnd.Line
                ' A selection ending at column 0 of a later line doesn't really include that
                ' line's content - don't wrap it
                If lEnd.Column = 0 AndAlso lEndLine > lStartLine Then lEndLine -= 1
                If lEndLine < lStartLine Then lEndLine = lStartLine

                Dim lOuterIndent As String = GetLineIndentation(lStartLine)
                Dim lInnerIndent As String = lOuterIndent & GetTabIndentString()
                Dim lUnit As String = GetTabIndentString()

                ' Re-indent each selected line by one extra level (skip blank lines so they
                ' don't pick up trailing whitespace)
                for lLine As Integer = lStartLine To lEndLine
                    If TextLines(lLine).Trim().Length > 0 Then
                        Dim lPos As New EditorPosition(lLine, 0)
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.RecordInsertText(lPos, lUnit, New EditorPosition(lLine, lUnit.Length))
                        End If
                        pSourceFileInfo.InsertText(lLine, 0, lUnit)
                    End If
                Next

                ' Insert the closing construct after the block first (its target line index
                ' isn't affected by the not-yet-performed opening-line insert below)
                Dim lClosingText As String = BuildSurroundClosing(vKind, lOuterIndent, lInnerIndent, lStartLine)
                If Not String.IsNullOrEmpty(lClosingText) Then
                    InsertLinesBefore(lEndLine + 1, lClosingText)
                End If

                ' Insert the opening construct before the block
                Dim lPlaceholder As String = ""
                Dim lOpeningLine As String = BuildSurroundOpening(vKind, lOuterIndent, lPlaceholder)
                InsertLinesBefore(lStartLine, lOpeningLine & Environment.NewLine)

                ClearSelection()

                If Not String.IsNullOrEmpty(lPlaceholder) Then
                    Dim lPlaceholderCol As Integer = lOpeningLine.IndexOf(lPlaceholder, StringComparison.Ordinal)
                    If lPlaceholderCol >= 0 Then
                        SetSelection(New EditorPosition(lStartLine, lPlaceholderCol),
                                     New EditorPosition(lStartLine, lPlaceholderCol + lPlaceholder.Length))
                        SetCursorPosition(lStartLine, lPlaceholderCol + lPlaceholder.Length)
                    Else
                        SetCursorPosition(lStartLine, lOpeningLine.Length)
                    End If
                Else
                    SetCursorPosition(lStartLine, lOpeningLine.Length)
                End If

                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"SurroundSelectionWith error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds the opening line for a surround-with construct and, via vPlaceholder, the
        ''' substring within it that should be selected for the user to type over (empty if
        ''' there isn't one)
        ''' </summary>
        Private Function BuildSurroundOpening(vKind As SurroundWithKind, vIndent As String, ByRef vPlaceholder As String) As String
            Select Case vKind
                Case SurroundWithKind.eTryCatch
                    vPlaceholder = ""
                    Return vIndent & "Try"
                Case SurroundWithKind.eIf
                    vPlaceholder = "condition"
                    Return vIndent & "If condition Then"
                Case SurroundWithKind.eFor
                    vPlaceholder = "count"
                    Return vIndent & "For i As Integer = 0 To count - 1"
                Case SurroundWithKind.eForEach
                    vPlaceholder = "collection"
                    Return vIndent & "For Each item In collection"
                Case SurroundWithKind.eUsing
                    vPlaceholder = "resource"
                    Return vIndent & "Using resource"
                Case SurroundWithKind.eWith
                    vPlaceholder = "target"
                    Return vIndent & "With target"
                Case SurroundWithKind.eWhile
                    vPlaceholder = "condition"
                    Return vIndent & "While condition"
                Case Else
                    vPlaceholder = ""
                    Return ""
            End Select
        End Function

        ''' <summary>
        ''' Builds the closing construct (and, for Try/Catch, the Catch clause) for a
        ''' surround-with block. Try/Catch's Console.WriteLine message uses the containing
        ''' member's name, matching CLAUDE.md's "Try-Catch everywhere with Console.WriteLine"
        ''' convention exactly as seen throughout this codebase.
        ''' </summary>
        Private Function BuildSurroundClosing(vKind As SurroundWithKind, vOuterIndent As String, vInnerIndent As String, vContextLine As Integer) As String
            Select Case vKind
                Case SurroundWithKind.eTryCatch
                    Dim lMethodNode As SyntaxNode = FindContainingMemberNode(pRootNode, vContextLine)
                    Dim lMethodName As String = If(lMethodNode IsNot Nothing AndAlso Not String.IsNullOrEmpty(lMethodNode.Name), lMethodNode.Name, "Block")
                    Return vOuterIndent & "Catch ex As Exception" & Environment.NewLine &
                           vInnerIndent & $"Console.WriteLine($""{lMethodName} error: {{ex.Message}}"")" & Environment.NewLine &
                           vOuterIndent & "End Try" & Environment.NewLine

                Case SurroundWithKind.eIf
                    Return vOuterIndent & "End If" & Environment.NewLine

                Case SurroundWithKind.eFor, SurroundWithKind.eForEach
                    Return vOuterIndent & "Next" & Environment.NewLine

                Case SurroundWithKind.eUsing
                    Return vOuterIndent & "End Using" & Environment.NewLine

                Case SurroundWithKind.eWith
                    Return vOuterIndent & "End With" & Environment.NewLine

                Case SurroundWithKind.eWhile
                    Return vOuterIndent & "End While" & Environment.NewLine

                Case Else
                    Return ""
            End Select
        End Function

    End Class

End Namespace
