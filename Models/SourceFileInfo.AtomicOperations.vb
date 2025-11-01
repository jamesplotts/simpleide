' Models/SourceFileInfo.AtomicOps.vb - Atomic text manipulation operations
' Created: 2025-01-01
Imports System
Imports System.Collections.Generic

Namespace Models
    
    ''' <summary>
    ''' Atomic text manipulation operations for SourceFileInfo
    ''' </summary>
    ''' <remarks>
    ''' ALL text modifications must go through these four methods only.
    ''' These handle CharacterTokens array updates properly and consistently.
    ''' </remarks>
    Partial Public Class SourceFileInfo
        
        ' ===== THE FOUR ATOMIC OPERATIONS =====
        
								''' <summary>
								''' Inserts a single character at the specified position
								''' </summary>
								''' <param name="vLine">Zero-based line index</param>
								''' <param name="vColumn">Zero-based column index</param>
								''' <param name="vChar">Character to insert</param>
								''' <remarks>
								''' IMPROVED: Better preserves existing syntax tokens during character insertion
								''' Only triggers re-parsing when necessary (e.g., inserting quotes or spaces)
								''' </remarks>
								Public Sub InsertCharacter(vLine As Integer, vColumn As Integer, vChar As Char)
								    Try
								        ' Validate line
								        If vLine < 0 OrElse vLine >= TextLines.Count Then
								            Console.WriteLine($"InsertCharacter: Invalid line {vLine}")
								            Return
								        End If
								        
								        ' Get current line
								        Dim lOldLine As String = TextLines(vLine)
								        Dim lOldLength As Integer = lOldLine.Length
								        
								        ' Clamp column to valid range
								        vColumn = Math.Max(0, Math.Min(vColumn, lOldLength))
								        
								        ' Build new line
								        Dim lNewLine As String = lOldLine.Insert(vColumn, vChar.ToString())
								        TextLines(vLine) = lNewLine
								        
								        ' Determine if we need to update tokens
								        Dim lNeedsTokenUpdate As Boolean = False
								        
								        ' Check if this character might affect tokenization
								        Select Case vChar
								            Case "'"c  ' Comment marker - always needs re-parse
								                lNeedsTokenUpdate = True
								            Case """"c  ' String delimiter - always needs re-parse
								                lNeedsTokenUpdate = True
								            Case " "c, vbTab(0)  ' Whitespace - might create new token boundaries
								                lNeedsTokenUpdate = True
								            Case "("c, ")"c, "["c, "]"c, "{"c, "}"c  ' Brackets
								                lNeedsTokenUpdate = True
								            Case ","c, "."c, ":"c, ";"c  ' Punctuation
								                lNeedsTokenUpdate = True
								            Case "="c, "+"c, "-"c, "*"c, "/"c, "<"c, ">"c, "&"c  ' Operators
								                lNeedsTokenUpdate = True
								            Case Else
								                ' For regular characters, check if we're at a token boundary
								                ' This is a simple heuristic - could be improved
								                If vColumn = 0 OrElse vColumn = lOldLength Then
								                    ' At line start or end - might affect tokens
								                    lNeedsTokenUpdate = True
								                ElseIf vColumn > 0 AndAlso vColumn < lOldLength Then
								                    ' Check characters around insertion point
								                    Dim lPrevChar As Char = lOldLine(vColumn - 1)
								                    Dim lNextChar As Char = lOldLine(vColumn)
								                    
								                    ' If inserting between whitespace and non-whitespace, might affect tokens
								                    If Char.IsWhiteSpace(lPrevChar) <> Char.IsWhiteSpace(lNextChar) Then
								                        lNeedsTokenUpdate = True
								                    End If
								                End If
								        End Select
								        
								        ' Update character tokens array
								        If pCharacterTokens IsNot Nothing AndAlso vLine < pCharacterTokens.Length Then
								            Dim lOldTokens() As Byte = pCharacterTokens(vLine)
								            
								            If Not lNeedsTokenUpdate AndAlso lOldTokens IsNot Nothing AndAlso lOldTokens.Length = lOldLength Then
								                ' Simple case: just shift tokens and insert a default token for the new character
								                ' This preserves existing syntax highlighting
								                
								                Dim lNewTokens(lNewLine.Length - 1) As Byte
								                
								                ' Copy tokens before insertion point
								                If vColumn > 0 Then
								                    Array.Copy(lOldTokens, 0, lNewTokens, 0, vColumn)
								                End If
								                
								                ' Insert token for new character
								                ' Use the token from adjacent character if available
								                If vColumn > 0 Then
								                    ' Use token from previous character
								                    lNewTokens(vColumn) = lOldTokens(vColumn - 1)
								                ElseIf vColumn < lOldLength Then
								                    ' Use token from next character
								                    lNewTokens(vColumn) = lOldTokens(vColumn)
								                Else
								                    ' Use default token
								                    lNewTokens(vColumn) = CharacterToken.CreateDefault()
								                End If
								                
								                ' Copy tokens after insertion point
								                If vColumn < lOldLength Then
								                    Array.Copy(lOldTokens, vColumn, lNewTokens, vColumn + 1, lOldLength - vColumn)
								                End If
								                
								                pCharacterTokens(vLine) = lNewTokens
								                
								                Console.WriteLine($"InsertCharacter: Preserved tokens with simple shift for line {vLine}")
								            Else
								                ' Complex case: need to update metadata and re-tokenize
								                SetLineMetadataAndCharacterTokens(vLine)
								            End If
								        Else
								            ' No existing tokens - update metadata
								            SetLineMetadataAndCharacterTokens(vLine)
								        End If
								        
								        ' Mark as modified and raise events
								        IsModified = True
								        NeedsParsing = True
								        RaiseTextChangedEvent(TextChangeType.eLineModified, vLine, vLine, 1)
								        
								        ' Request async parse for proper token updates
								        ' This will happen in the background without disrupting the UI
								        RequestAsyncParse()
								        
								    Catch ex As Exception
								        Console.WriteLine($"InsertCharacter error: {ex.Message}")
								        Console.WriteLine($"  Stack: {ex.StackTrace}")
								    End Try
								End Sub
								        
								''' <summary>
								''' Deletes a single character at the specified position
								''' </summary>
								''' <param name="vLine">Zero-based line index</param>
								''' <param name="vColumn">Zero-based column index</param>
								''' <remarks>
								''' IMPROVED: Better preserves existing syntax tokens during character deletion
								''' Only triggers re-parsing when necessary (e.g., deleting quotes or creating new tokens)
								''' </remarks>
								Public Sub DeleteCharacter(vLine As Integer, vColumn As Integer)
								    Try
								        ' Validate line and column
								        If vLine < 0 OrElse vLine >= TextLines.Count Then
								            Console.WriteLine($"DeleteCharacter: Invalid line {vLine}")
								            Return
								        End If
								        
								        Dim lOldLine As String = TextLines(vLine)
								        If vColumn < 0 OrElse vColumn >= lOldLine.Length Then
								            Console.WriteLine($"DeleteCharacter: Invalid column {vColumn} for line length {lOldLine.Length}")
								            Return
								        End If
								        
								        ' Get the character being deleted
								        Dim lDeletedChar As Char = lOldLine(vColumn)
								        
								        ' Build new line
								        Dim lNewLine As String = lOldLine.Remove(vColumn, 1)
								        TextLines(vLine) = lNewLine
								        
								        ' Determine if we need to update tokens
								        Dim lNeedsTokenUpdate As Boolean = False
								        
								        ' Check if the deleted character might affect tokenization
								        Select Case lDeletedChar
								            Case "'"c  ' Comment marker - always needs re-parse
								                lNeedsTokenUpdate = True
								            Case """"c  ' String delimiter - always needs re-parse
								                lNeedsTokenUpdate = True
								            Case " "c, vbTab(0)  ' Whitespace - might merge tokens
								                lNeedsTokenUpdate = True
								            Case "("c, ")"c, "["c, "]"c, "{"c, "}"c  ' Brackets
								                lNeedsTokenUpdate = True
								            Case ","c, "."c, ":"c, ";"c  ' Punctuation
								                lNeedsTokenUpdate = True
								            Case "="c, "+"c, "-"c, "*"c, "/"c, "<"c, ">"c, "&"c  ' Operators
								                lNeedsTokenUpdate = True
								            Case Else
								                ' For regular characters, check if we're merging tokens
								                If vColumn > 0 AndAlso vColumn < lOldLine.Length - 1 Then
								                    ' Check if we're deleting between different character types
								                    Dim lPrevChar As Char = lOldLine(vColumn - 1)
								                    Dim lNextChar As Char = lOldLine(vColumn + 1)
								                    
								                    ' If deleting between different character types, might merge tokens
								                    If (Char.IsLetter(lPrevChar) AndAlso Not Char.IsLetterOrDigit(lNextChar)) OrElse
								                       (Not Char.IsLetterOrDigit(lPrevChar) AndAlso Char.IsLetter(lNextChar)) Then
								                        lNeedsTokenUpdate = True
								                    End If
								                End If
								        End Select
								        
								        ' Update character tokens array
								        If pCharacterTokens IsNot Nothing AndAlso vLine < pCharacterTokens.Length Then
								            Dim lOldTokens() As Byte = pCharacterTokens(vLine)
								            
								            If Not lNeedsTokenUpdate AndAlso lOldTokens IsNot Nothing AndAlso lOldTokens.Length = lOldLine.Length Then
								                ' Simple case: just shift tokens left after deletion
								                ' This preserves existing syntax highlighting
								                
								                If lNewLine.Length > 0 Then
								                    Dim lNewTokens(lNewLine.Length - 1) As Byte
								                    
								                    ' Copy tokens before deletion point
								                    If vColumn > 0 Then
								                        Array.Copy(lOldTokens, 0, lNewTokens, 0, vColumn)
								                    End If
								                    
								                    ' Copy tokens after deletion point (shifted left)
								                    If vColumn < lOldLine.Length - 1 Then
								                        Array.Copy(lOldTokens, vColumn + 1, lNewTokens, vColumn, lOldLine.Length - vColumn - 1)
								                    End If
								                    
								                    pCharacterTokens(vLine) = lNewTokens
								                Else
								                    ' Line is now empty
								                    pCharacterTokens(vLine) = New Byte() {}
								                End If
								                
								                Console.WriteLine($"DeleteCharacter: Preserved tokens with simple shift for line {vLine}")
								            Else
								                ' Complex case: need to update metadata and re-tokenize
								                SetLineMetadataAndCharacterTokens(vLine)
								            End If
								        Else
								            ' No existing tokens - update metadata
								            SetLineMetadataAndCharacterTokens(vLine)
								        End If
								        
								        ' Mark as modified and raise events
								        IsModified = True
								        NeedsParsing = True
								        RaiseTextChangedEvent(TextChangeType.eLineModified, vLine, vLine, 1)
								        
								        ' Request async parse for proper token updates
								        ' This will happen in the background without disrupting the UI
								        RequestAsyncParse()
								        
								    Catch ex As Exception
								        Console.WriteLine($"DeleteCharacter error: {ex.Message}")
								        Console.WriteLine($"  Stack: {ex.StackTrace}")
								    End Try
								End Sub
								        
        ''' <summary>
        ''' Inserts text at the specified position (handles multi-line text)
        ''' </summary>
        ''' <param name="vLine">Zero-based line index</param>
        ''' <param name="vColumn">Zero-based column index</param>
        ''' <param name="vText">Text to insert (can contain newlines)</param>
        ''' <remarks>
        ''' Handles both single-line and multi-line insertions.
        ''' For single-line, preserves existing tokens where possible.
        ''' For multi-line, splits the current line and inserts new lines.
        ''' </remarks>
        Public Sub InsertText(vLine As Integer, vColumn As Integer, vText As String)
            Try
                If String.IsNullOrEmpty(vText) Then Return
                If vLine < 0 OrElse vLine >= TextLines.Count Then
                    Console.WriteLine($"InsertText: Invalid line {vLine}")
                    Return
                End If
                
                ' Normalize line endings
                vText = vText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
                
                ' Check if text contains newlines
                If vText.Contains(vbLf) Then
                    ' Multi-line insertion
                    InsertMultiLineTextInternal(vLine, vColumn, vText)
                Else
                    ' Single-line insertion
                    InsertSingleLineTextInternal(vLine, vColumn, vText)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InsertText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes text in the specified range
        ''' </summary>
        ''' <param name="vStartLine">Start line (zero-based)</param>
        ''' <param name="vStartColumn">Start column (zero-based)</param>
        ''' <param name="vEndLine">End line (zero-based)</param>
        ''' <param name="vEndColumn">End column (zero-based)</param>
        ''' <remarks>
        ''' Handles both single-line and multi-line deletions.
        ''' Preserves tokens outside the deletion range.
        ''' </remarks>
        Public Sub DeleteText(vStartLine As Integer, vStartColumn As Integer, 
                            vEndLine As Integer, vEndColumn As Integer)
            Try
                ' Validate range
                If vStartLine < 0 OrElse vStartLine >= TextLines.Count Then
                    Console.WriteLine($"DeleteText: Invalid start line {vStartLine}")
                    Return
                End If
                If vEndLine < 0 OrElse vEndLine >= TextLines.Count Then
                    Console.WriteLine($"DeleteText: Invalid end line {vEndLine}")
                    Return
                End If
                
                ' Ensure start comes before end
                If vStartLine > vEndLine OrElse (vStartLine = vEndLine AndAlso vStartColumn > vEndColumn) Then
                    ' Swap
                    Dim lTempLine As Integer = vStartLine
                    Dim lTempCol As Integer = vStartColumn
                    vStartLine = vEndLine
                    vStartColumn = vEndColumn
                    vEndLine = lTempLine
                    vEndColumn = lTempCol
                End If
                
                If vStartLine = vEndLine Then
                    ' Single-line deletion
                    ' TODO: This just needs implemented right here - no other method calls that functionality.

                    Dim lLine As String = pTextLines(vStartLine)
                    Dim lOldLength As Integer = lLine.Length
                    Dim vStartCol As Integer = vStartColumn
                    Dim vEndCol As Integer = vEndColumn
                    
                    ' Validate and adjust columns
                    vStartCol = Math.Max(0, Math.Min(vStartCol, lLine.Length))
                    vEndCol = Math.Max(vStartCol, Math.Min(vEndCol, lLine.Length))
                    
                    ' Build new line
                    Dim lNewLine As String
                    If vStartCol = 0 AndAlso vEndCol >= lLine.Length Then
                        lNewLine = ""
                    ElseIf vStartCol = 0 Then
                        lNewLine = lLine.Substring(vEndCol)
                    ElseIf vEndCol >= lLine.Length Then
                        lNewLine = lLine.Substring(0, vStartCol)
                    Else
                        lNewLine = lLine.Substring(0, vStartCol) & lLine.Substring(vEndCol)
                    End If
                    
                    ' Update the line
                    pTextLines(vStartLine) = lNewLine
                   
                    SetLineMetadataAndCharacterTokens(lLine)
                    
                    ' Update state
                    pIsModified = True
                    pNeedsParsing = True
                    
                    ' Raise event
                    RaiseTextChangedEvent(TextChangeType.eLineModified, lLine, lLine, 1)

                    'DeleteSingleLineTextInternal(vStartLine, vStartColumn, vEndColumn)
                Else
                    ' Multi-line deletion
                    DeleteMultiLineTextInternal(vStartLine, vStartColumn, vEndLine, vEndColumn)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteText error: {ex.Message}")
            End Try
        End Sub

								''' <summary>
								''' Updates line metadata and character tokens for a modified line
								''' </summary>
								''' <param name="vLineIndex">Zero-based line index</param>
								''' <remarks>
								''' FIXED: Now preserves existing tokens when possible during character edits
								''' Only re-parses when necessary (e.g., when tokens might have changed significantly)
								''' </remarks>
								Public Sub SetLineMetadataAndCharacterTokens(vLineIndex As Integer)
								    Try
								        Console.WriteLine("SourceFileInfo.SetLineMetadataAndCharacterTokens called")
                ' Validate line index
								        If vLineIndex < 0 OrElse vLineIndex >= pTextLines.Count Then Return
								        
								        Dim lLineText As String = pTextLines(vLineIndex)
								        Dim lLineLength As Integer = lLineText.Length
								        
								        ' Ensure we have LineMetadata array
								        If LineMetadata Is Nothing OrElse vLineIndex >= LineMetadata.Length Then
								            ' Expand array if needed
								            If LineMetadata Is Nothing Then
								                ReDim pLineMetadata(pTextLines.Count - 1)
								            ElseIf vLineIndex >= LineMetadata.Length Then
								                ReDim Preserve pLineMetadata(vLineIndex)
								            End If
								        End If
								        
								        ' Ensure we have metadata for this line
								        If LineMetadata(vLineIndex) Is Nothing Then
								            LineMetadata(vLineIndex) = New LineMetadata()
								        End If
								        
								        Dim lMetadata As LineMetadata = LineMetadata(vLineIndex)
								        
								        ' Check if the line has changed significantly enough to warrant re-parsing
								        Dim lNeedsParse As Boolean = False
								        
								        ' If we have no tokens at all, we need to parse
								        If lMetadata.SyntaxTokens Is Nothing OrElse lMetadata.SyntaxTokens.Count = 0 Then
								            lNeedsParse = True
								        Else
								            ' Check if the line content has changed significantly
								            ' For now, we'll be conservative and re-parse if:
								            ' 1. The line contains comment marker (')
								            ' 2. The line contains string delimiters (")
								            ' 3. The line hash has changed (indicating content change)
								            Dim lOldHash As String = lMetadata.LineHash
								            lMetadata.UpdateHash(lLineText)
								            
								            If lOldHash <> lMetadata.LineHash Then
								                ' Content changed - check if it's a significant change
								                If lLineText.Contains("'"c) OrElse lLineText.Contains(""""c) Then
								                    lNeedsParse = True ' Comments or strings may have changed
								                ElseIf lLineText.Contains(" "c) Then
								                    ' Check for new keywords by looking for space-delimited words
								                    ' This is a simple heuristic - could be improved
								                    lNeedsParse = True
								                End If
								            End If
								        End If
								        
								        ' Parse the line if needed, otherwise preserve existing tokens
								        If lNeedsParse Then
								            ' Try to parse the line with ProjectManager if available
								            Dim lTokens As List(Of SyntaxToken) = Nothing
								            
								            ' First try to get ProjectManager if we don't have it
								            If pProjectManager Is Nothing Then
								                Dim lArgs As New ProjectManagerRequestEventArgs()
								                RaiseEvent ProjectManagerRequested(Me, lArgs)
								                If lArgs.HasProjectManager Then
								                    pProjectManager = lArgs.ProjectManager
								                End If
								            End If
								            
								            ' If we have ProjectManager, use ParseLine
								            If pProjectManager IsNot Nothing Then
								                lTokens = ParseLine(lLineText, vLineIndex)
								            End If
								            
								            ' If parsing succeeded, update tokens
								            If lTokens IsNot Nothing AndAlso lTokens.Count > 0 Then
								                lMetadata.SyntaxTokens = lTokens
								                lMetadata.ParseState = LineParseState.eParsed
								                Console.WriteLine($"SetLineMetadataAndCharacterTokens: Parsed {lTokens.Count} tokens for line {vLineIndex}")
								            Else
								                ' Parsing failed or returned no tokens - apply defaults
								                lMetadata.SyntaxTokens = New List(Of SyntaxToken)()
								                lMetadata.ParseState = LineParseState.eUnparsed
								                Console.WriteLine($"SetLineMetadataAndCharacterTokens: Applied default tokens for line {vLineIndex}")
								            End If
								        Else
								            ' Preserve existing tokens but adjust for length changes if needed
								            ' The tokens themselves don't need to change for simple character insertions/deletions
								            ' within existing tokens
								            Console.WriteLine($"SetLineMetadataAndCharacterTokens: Preserving existing {lMetadata.SyntaxTokens.Count} tokens for line {vLineIndex}")
								        End If
								        
								        ' Always update the CharacterTokens array using GetEncodedTokens
								        ' This handles the actual byte encoding based on current SyntaxTokens
								        If pCharacterTokens IsNot Nothing AndAlso vLineIndex < pCharacterTokens.Length Then
								            pCharacterTokens(vLineIndex) = lMetadata.GetEncodedTokens(lLineLength)
								        ElseIf pCharacterTokens IsNot Nothing Then
								            ' Expand array if needed
								            ReDim Preserve pCharacterTokens(vLineIndex)
								            pCharacterTokens(vLineIndex) = lMetadata.GetEncodedTokens(lLineLength)
								        End If
								        
								        ' Mark that we need async parsing for proper token updates
								        ' This will happen in the background and update tokens properly
								        If lNeedsParse AndAlso pProjectManager Is Nothing Then
								            ' We couldn't parse now, so request async parse
								            pNeedsParsing = True
								            RequestAsyncParse()
								        End If
								        
								    Catch ex As Exception
								        Console.WriteLine($"SetLineMetadataAndCharacterTokens error: {ex.Message}")
								        Console.WriteLine($"  Stack: {ex.StackTrace}")
								        
								        ' On error, apply default tokens to prevent crashes
								        Try
								            If LineMetadata IsNot Nothing AndAlso vLineIndex < LineMetadata.Length Then
								                If LineMetadata(vLineIndex) Is Nothing Then
								                    LineMetadata(vLineIndex) = New LineMetadata()
								                End If
								                LineMetadata(vLineIndex).SyntaxTokens = New List(Of SyntaxToken)()
								                LineMetadata(vLineIndex).ParseState = LineParseState.eUnparsed
								                
								                If pCharacterTokens IsNot Nothing AndAlso vLineIndex < pCharacterTokens.Length Then
								                    Dim lLineLength As Integer = pTextLines(vLineIndex).Length
								                    pCharacterTokens(vLineIndex) = LineMetadata(vLineIndex).GetEncodedTokens(lLineLength)
								                End If
								            End If
								        Catch
								            ' Ignore errors in error handler
								        End Try
								    End Try
								End Sub

        ''' <summary>
        ''' Atomic method to delete a line
        ''' </summary>
        Public Sub DeleteLine(vLineIndex As Integer)
            ' Validate index
            If vLineIndex < 0 OrElse vLineIndex >= pTextLines.Count Then Return
            
            ' Don't delete the last line if it's the only one
            If pTextLines.Count = 1 Then
                pTextLines(0) = ""
                If pLineMetadata IsNot Nothing AndAlso pLineMetadata.Length > 0 Then
                    pLineMetadata(0) = New LineMetadata()
                    pLineMetadata(0).ParseState = LineParseState.eUnparsed
                End If
                If pCharacterTokens IsNot Nothing AndAlso pCharacterTokens.Length > 0 Then
                    pCharacterTokens(0) = New Byte() {}
                End If
                Return
            End If
            
            ' Remove the line
            pTextLines.RemoveAt(vLineIndex)
            
            ' Update LineMetadata
            If pLineMetadata IsNot Nothing AndAlso vLineIndex < pLineMetadata.Length Then
                ' Shift metadata up
                for i As Integer = vLineIndex To pLineMetadata.Length - 2
                    pLineMetadata(i) = pLineMetadata(i + 1)
                Next
                ReDim Preserve pLineMetadata(Math.Max(0, pLineMetadata.Length - 2))
            End If
            
            ' Update CharacterTokens
            If pCharacterTokens IsNot Nothing AndAlso vLineIndex < pCharacterTokens.Length Then
                ' Shift tokens up
                for i As Integer = vLineIndex To pCharacterTokens.Length - 2
                    pCharacterTokens(i) = pCharacterTokens(i + 1)
                Next
                ReDim Preserve pCharacterTokens(Math.Max(0, pCharacterTokens.Length - 2))
            End If
        End Sub   

        Public Sub InsertLine(vLineIndex As Integer, vLineText As String)
            If String.IsNullOrEmpty(vLineText) Then Return
            Dim lLineText As String = vLineText
            If Not lLineText.Contains(Environment.NewLine) AndAlso Not lLineText.Contains(vbCr) AndAlso Not lLineText.Contains(vbLf) Then
                lLineText += Environment.NewLine
            Else
                Throw New Exception("SourceFileInfo.InsertLine Error:  vLineText parameter is NOT a single line of text")
            End If
            InsertSingleLineTextInternal(vLineIndex, 0, lLineText)
        End Sub
        
    End Class
    
End Namespace 
