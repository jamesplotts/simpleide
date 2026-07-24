' CustomDrawingEditor.BracketAutoClose.vb - Typing (, {, [, or " auto-inserts the matching
' closer with the cursor left between them; typing the closing character when it's already
' the very next character just moves the cursor past it instead of inserting a duplicate.
' Suppressed while typing inside an existing string literal or comment, where the typed
' character is plain text content rather than code structure.
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        Private Shared ReadOnly BracketPairs As New Dictionary(Of Char, Char) From {
            {"("c, ")"c},
            {"{"c, "}"c},
            {"["c, "]"c}
        }
        Private Shared ReadOnly BracketClosers As New HashSet(Of Char) From {")"c, "}"c, "]"c}

        ''' <summary>
        ''' Handles bracket/quote auto-close and skip-over for a character about to be typed.
        ''' Call BEFORE the normal single-character insert; if this returns True it already
        ''' performed its own insertion and cursor movement and the normal insert should be
        ''' skipped for this keystroke.
        ''' </summary>
        ''' <param name="vChar">The character about to be typed</param>
        ''' <returns>True if this handled the keystroke completely</returns>
        Private Function HandleBracketAutoClose(vChar As Char) As Boolean
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return False
                If pCursorLine < 0 OrElse pCursorLine >= pLineCount Then Return False

                Dim lLine As String = TextLines(pCursorLine)
                Dim lNextChar As Char = If(pCursorColumn < lLine.Length, lLine(pCursorColumn), ControlChars.NullChar)

                ' Skip-over: typing a closing character that's already right there
                If (BracketClosers.Contains(vChar) OrElse vChar = """"c) AndAlso lNextChar = vChar Then
                    SetCursorPosition(pCursorLine, pCursorColumn + 1)
                    Return True
                End If

                If vChar = """"c Then
                    ' Typing a quote while already inside an open string closes it - a single
                    ' quote, not a new pair
                    If IsInsideStringOrComment(pCursorLine, pCursorColumn) Then Return False
                    InsertBracketPair(""""c, """"c)
                    Return True
                End If

                If BracketPairs.ContainsKey(vChar) Then
                    If IsInsideStringOrComment(pCursorLine, pCursorColumn) Then Return False
                    InsertBracketPair(vChar, BracketPairs(vChar))
                    Return True
                End If

                Return False

            Catch ex As Exception
                Console.WriteLine($"HandleBracketAutoClose error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Inserts vOpen immediately followed by vClose, leaving the cursor between them
        ''' </summary>
        Private Sub InsertBracketPair(vOpen As Char, vClose As Char)
            Dim lText As String = vOpen & vClose
            If pUndoRedoManager IsNot Nothing Then
                Dim lPos As New EditorPosition(pCursorLine, pCursorColumn)
                pUndoRedoManager.RecordInsertText(lPos, lText, New EditorPosition(pCursorLine, pCursorColumn + 2))
            End If
            pSourceFileInfo.InsertText(pCursorLine, pCursorColumn, lText)
            SetCursorPosition(pCursorLine, pCursorColumn + 1)
        End Sub

        ''' <summary>
        ''' True if vColumn on vLine sits inside a string literal or past a line comment's
        ''' start, based on a simple quote-parity count over the comment-stripped line - not
        ''' fully token-aware, but sufficient to suppress bracket auto-pairing while typing
        ''' plain text content
        ''' </summary>
        Private Function IsInsideStringOrComment(vLine As Integer, vColumn As Integer) As Boolean
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return False
                Dim lStripped As String = StripLineComment(TextLines(vLine))
                If vColumn > lStripped.Length Then Return True

                Dim lQuoteCount As Integer = 0
                for i As Integer = 0 To Math.Min(vColumn, lStripped.Length) - 1
                    If lStripped(i) = """"c Then lQuoteCount += 1
                Next
                Return (lQuoteCount Mod 2) = 1

            Catch ex As Exception
                Console.WriteLine($"IsInsideStringOrComment error: {ex.Message}")
                Return False
            End Try
        End Function

    End Class

End Namespace
