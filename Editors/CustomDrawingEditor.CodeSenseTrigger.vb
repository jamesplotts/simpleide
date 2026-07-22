' CustomDrawingEditor_CodeSenseTrigger.vb - CodeSense automatic triggering implementation
' This partial class adds automatic CodeSense triggering when typing
Imports System
Imports Gtk
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        Private pLastTypedChar As Char = " "c
        Private pCodeSenseMinChars As Integer = 2  ' Minimum characters before showing CodeSense

        ''' <summary>
        ''' Checks if CodeSense should be triggered based on the typed character
        ''' </summary>
        ''' <param name="vChar">The character that was just typed</param>
        ''' <remarks>
        ''' Triggers/updates CodeSense synchronously on every keystroke - a debounce delay
        ''' here previously made the suggestion list visibly lag behind typing
        ''' </remarks>
        Private Sub CheckCodeSenseTrigger(vChar As Char)
            Try
                ' Don't trigger if CodeSense is disabled
                If pSettingsManager IsNot Nothing AndAlso Not pSettingsManager.CodeSenseEnabled Then
                    Return
                End If

                ' Store the last typed character
                pLastTypedChar = vChar

                ' Check for immediate trigger characters
                Select Case vChar
                    Case "."c
                        ' Period triggers member list immediately
                        TriggerCodeSenseImmediate(CodeSenseTriggerReason.eMemberList)

                    Case "("c
                        ' Opening parenthesis triggers parameter hints
                        TriggerCodeSenseImmediate(CodeSenseTriggerReason.eParameterHints)

                    Case " "c
                        ' Space after certain keywords triggers CodeSense
                        CheckKeywordTrigger()

                    Case Else
                        ' Regular characters update the suggestion list immediately
                        If Char.IsLetterOrDigit(vChar) OrElse vChar = "_"c Then
                            TriggerCodeSenseForCurrentWord()
                        End If
                End Select

            Catch ex As Exception
                Console.WriteLine($"CheckCodeSenseTrigger error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Triggers/updates CodeSense for the word currently being typed, if it has
        ''' reached the minimum length
        ''' </summary>
        Private Sub TriggerCodeSenseForCurrentWord()
            Try
                Dim lCurrentWord As String = GetCurrentWord()
                If lCurrentWord.Length >= pCodeSenseMinChars Then
                    TriggerCodeSenseForCompletion(lCurrentWord)
                End If

            Catch ex As Exception
                Console.WriteLine($"TriggerCodeSenseForCurrentWord error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Triggers CodeSense immediately for a specific reason
        ''' </summary>
        Private Sub TriggerCodeSenseImmediate(vReason As CodeSenseTriggerReason)
            Try
                ' Create context
                Dim lContext As New CodeSenseContext()
                lContext.TriggerReason = vReason
                lContext.TriggerPosition = New EditorPosition(pCursorLine, pCursorColumn)
                lContext.FileType = "vb"
                
                ' Get the current line text
                If pSourceFileInfo IsNot Nothing AndAlso pCursorLine < pSourceFileInfo.TextLines.Count Then
                    lContext.LineText = pSourceFileInfo.TextLines(pCursorLine)
                    lContext.LinePosition = pCursorColumn
                End If
                
                ' Get the trigger character
                If pCursorColumn > 0 AndAlso lContext.LineText.Length >= pCursorColumn Then
                    lContext.TriggerChar = lContext.LineText(pCursorColumn - 1)
                End If

                ' Map the trigger reason to the TriggerKind that CodeSenseEngine.GetSuggestions
                ' dispatches on, and set the fields each suggestion path actually reads.
                Select Case vReason
                    Case CodeSenseTriggerReason.eMemberList
                        ' For member list, get the object before the period
                        lContext.TriggerKind = CodeSenseTriggerKind.eDot
                        lContext.MemberAccessTarget = GetObjectBeforePeriod()
                        lContext.Prefix = ""

                    Case CodeSenseTriggerReason.eParameterHints
                        lContext.TriggerKind = CodeSenseTriggerKind.eOpenParen
                        lContext.MemberAccessTarget = GetIdentifierBeforeParen()
                        lContext.Prefix = ""

                    Case CodeSenseTriggerReason.eTypeList
                        lContext.TriggerKind = CodeSenseTriggerKind.eSpace
                        lContext.Prefix = ""

                    Case Else
                        lContext.TriggerKind = CodeSenseTriggerKind.eManual
                        lContext.Prefix = ""
                End Select

                ' Raise the CodeSenseRequested event
                RaiseEvent CodeSenseRequested(Me, lContext)
                
            Catch ex As Exception
                Console.WriteLine($"TriggerCodeSenseImmediate error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Triggers CodeSense for word completion
        ''' </summary>
        Private Sub TriggerCodeSenseForCompletion(vCurrentWord As String)
            Try
                ' Create context for completion
                Dim lContext As New CodeSenseContext()
                lContext.TriggerReason = CodeSenseTriggerReason.eCompletion
                lContext.TriggerPosition = New EditorPosition(pCursorLine, pCursorColumn)
                lContext.FileType = "vb"
                lContext.Prefix = vCurrentWord
                lContext.CurrentWord = vCurrentWord

                ' If the word being typed is immediately preceded by a dot (e.g. typing "IO"
                ' in "System.IO"), stay in member-list mode scoped to the identifier before
                ' that dot instead of falling back to a generic/project-wide search - otherwise
                ' every keystroke after the dot would abandon the member list entirely
                Dim lMemberTarget As String = GetMemberAccessTargetBeforeCurrentWord()
                If Not String.IsNullOrEmpty(lMemberTarget) Then
                    lContext.TriggerKind = CodeSenseTriggerKind.eDot
                    lContext.MemberAccessTarget = lMemberTarget
                Else
                    lContext.TriggerKind = CodeSenseTriggerKind.eManual
                End If

                ' Get the current line text
                If pSourceFileInfo IsNot Nothing AndAlso pCursorLine < pSourceFileInfo.TextLines.Count Then
                    lContext.LineText = pSourceFileInfo.TextLines(pCursorLine)
                    lContext.LinePosition = pCursorColumn
                End If
                
                ' Raise the CodeSenseRequested event
                RaiseEvent CodeSenseRequested(Me, lContext)
                
            Catch ex As Exception
                Console.WriteLine($"TriggerCodeSenseForCompletion error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Checks if space after a keyword should trigger CodeSense
        ''' </summary>
        Private Sub CheckKeywordTrigger()
            Try
                ' Get the word before the space
                Dim lPreviousWord As String = GetPreviousWord()
                If String.IsNullOrEmpty(lPreviousWord) Then Return
                
                ' Check if it's a keyword that should trigger CodeSense
                Select Case lPreviousWord.ToLower()
                    Case "imports", "inherits", "implements", "as", "new", "typeof", "gettype"
                        ' These keywords trigger type list
                        TriggerCodeSenseImmediate(CodeSenseTriggerReason.eTypeList)
                        
                    Case "dim", "private", "public", "protected", "friend", "shared", "readonly", "const"
                        ' Variable declaration keywords might trigger once a name is typed
                        TriggerCodeSenseForCurrentWord()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"CheckKeywordTrigger error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the current word being typed
        ''' </summary>
        Private Function GetCurrentWord() As String
            Try
                If pSourceFileInfo Is Nothing OrElse pCursorLine >= pSourceFileInfo.TextLines.Count Then
                    Return ""
                End If
                
                Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                If pCursorColumn > lLine.Length Then Return ""
                
                ' Find word start
                Dim lStart As Integer = pCursorColumn
                While lStart > 0 AndAlso (Char.IsLetterOrDigit(lLine(lStart - 1)) OrElse lLine(lStart - 1) = "_"c)
                    lStart -= 1
                End While
                
                ' Extract the word
                If lStart < pCursorColumn Then
                    Return lLine.Substring(lStart, pCursorColumn - lStart)
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetCurrentWord error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the word before the cursor (for keyword checking)
        ''' </summary>
        Private Function GetPreviousWord() As String
            Try
                If pSourceFileInfo Is Nothing OrElse pCursorLine >= pSourceFileInfo.TextLines.Count Then
                    Return ""
                End If
                
                Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                If pCursorColumn <= 1 Then Return ""
                
                ' Skip the space we just typed
                Dim lEnd As Integer = pCursorColumn - 1
                While lEnd > 0 AndAlso Char.IsWhiteSpace(lLine(lEnd - 1))
                    lEnd -= 1
                End While
                
                If lEnd = 0 Then Return ""
                
                ' Find word start
                Dim lStart As Integer = lEnd
                While lStart > 0 AndAlso (Char.IsLetterOrDigit(lLine(lStart - 1)) OrElse lLine(lStart - 1) = "_"c)
                    lStart -= 1
                End While
                
                ' Extract the word
                If lStart < lEnd Then
                    Return lLine.Substring(lStart, lEnd - lStart)
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetPreviousWord error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the identifier before a period (for member list)
        ''' </summary>
        Private Function GetObjectBeforePeriod() As String
            Try
                If pSourceFileInfo Is Nothing OrElse pCursorLine >= pSourceFileInfo.TextLines.Count Then
                    Return ""
                End If
                
                Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                If pCursorColumn <= 1 Then Return ""
                
                ' Make sure we just typed a period
                If lLine(pCursorColumn - 1) <> "."c Then Return ""
                
                ' Find the identifier before the period
                Dim lEnd As Integer = pCursorColumn - 1
                
                ' Skip any whitespace before the period
                While lEnd > 0 AndAlso Char.IsWhiteSpace(lLine(lEnd - 1))
                    lEnd -= 1
                End While
                
                If lEnd = 0 Then Return ""
                
                ' Find identifier start
                Dim lStart As Integer = lEnd
                While lStart > 0 AndAlso (Char.IsLetterOrDigit(lLine(lStart - 1)) OrElse lLine(lStart - 1) = "_"c)
                    lStart -= 1
                End While
                
                ' Extract the identifier
                If lStart < lEnd Then
                    Return lLine.Substring(lStart, lEnd - lStart)
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetObjectBeforePeriod error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' If the identifier currently being typed is immediately preceded by a dot (e.g.
        ''' typing "IO" in "System.IO"), returns the identifier before that dot
        ''' </summary>
        ''' <returns>The member-access target, or "" if the current word isn't after a dot</returns>
        Private Function GetMemberAccessTargetBeforeCurrentWord() As String
            Try
                If pSourceFileInfo Is Nothing OrElse pCursorLine >= pSourceFileInfo.TextLines.Count Then
                    Return ""
                End If

                Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)

                ' Find the start of the word currently being typed
                Dim lWordStart As Integer = Math.Min(pCursorColumn, lLine.Length)
                While lWordStart > 0 AndAlso (Char.IsLetterOrDigit(lLine(lWordStart - 1)) OrElse lLine(lWordStart - 1) = "_"c)
                    lWordStart -= 1
                End While

                If lWordStart = 0 OrElse lLine(lWordStart - 1) <> "."c Then Return ""

                ' Found a dot right before the current word - find the identifier before that dot
                Dim lEnd As Integer = lWordStart - 1
                While lEnd > 0 AndAlso Char.IsWhiteSpace(lLine(lEnd - 1))
                    lEnd -= 1
                End While

                If lEnd = 0 Then Return ""

                Dim lStart As Integer = lEnd
                While lStart > 0 AndAlso (Char.IsLetterOrDigit(lLine(lStart - 1)) OrElse lLine(lStart - 1) = "_"c)
                    lStart -= 1
                End While

                If lStart < lEnd Then
                    Dim lTarget As String = lLine.Substring(lStart, lEnd - lStart)
                    If lTarget.Equals("me", StringComparison.OrdinalIgnoreCase) Then Return "Me"
                    If lTarget.Equals("mybase", StringComparison.OrdinalIgnoreCase) Then Return "MyBase"
                    Return lTarget
                End If

                Return ""

            Catch ex As Exception
                Console.WriteLine($"GetMemberAccessTargetBeforeCurrentWord error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Gets the identifier before an opening parenthesis (for parameter hints)
        ''' </summary>
        Private Function GetIdentifierBeforeParen() As String
            Try
                If pSourceFileInfo Is Nothing OrElse pCursorLine >= pSourceFileInfo.TextLines.Count Then
                    Return ""
                End If

                Dim lLine As String = pSourceFileInfo.TextLines(pCursorLine)
                If pCursorColumn <= 1 Then Return ""

                ' Make sure we just typed an opening parenthesis
                If lLine(pCursorColumn - 1) <> "("c Then Return ""

                Dim lEnd As Integer = pCursorColumn - 1

                ' Skip any whitespace before the paren
                While lEnd > 0 AndAlso Char.IsWhiteSpace(lLine(lEnd - 1))
                    lEnd -= 1
                End While

                If lEnd = 0 Then Return ""

                ' Find identifier start
                Dim lStart As Integer = lEnd
                While lStart > 0 AndAlso (Char.IsLetterOrDigit(lLine(lStart - 1)) OrElse lLine(lStart - 1) = "_"c)
                    lStart -= 1
                End While

                If lStart < lEnd Then
                    Return lLine.Substring(lStart, lEnd - lStart)
                End If

                Return ""

            Catch ex As Exception
                Console.WriteLine($"GetIdentifierBeforeParen error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Cancels any pending CodeSense trigger
        ''' </summary>
        ''' <remarks>
        ''' Triggering is synchronous now (see TriggerCodeSenseForCurrentWord), so there's
        ''' never actually a pending trigger to cancel - kept as a no-op for callers that
        ''' still call this before hiding/updating the popup
        ''' </remarks>
        Public Sub CancelCodeSenseTrigger()
        End Sub
        
        ''' <summary>
        ''' Handles backspace key - cancels CodeSense if active
        ''' </summary>
        ''' <remarks>
        ''' Override or extend the existing HandleBackspace method
        ''' </remarks>
        Private Sub HandleBackspaceForCodeSense()
            Try
                ' Cancel any pending CodeSense
                CancelCodeSenseTrigger()
                
                ' If CodeSense is showing, update or hide it
                If pCodeSenseActive Then
                    Dim lCurrentWord As String = GetCurrentWord()
                    If lCurrentWord.Length < pCodeSenseMinChars Then
                        ' Hide CodeSense if word is too short
                        CancelCodeSense()
                    Else
                        ' Update CodeSense with new prefix
                        TriggerCodeSenseForCompletion(lCurrentWord)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandleBackspaceForCodeSense error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
    ''' <summary>
    ''' Reasons why CodeSense was triggered
    ''' </summary>

    
End Namespace