' Models/SourceFileInfo.Parsing.vb - Parsing integration with ProjectManager
' Created: 2025-01-10
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports SimpleIDE.Syntax
Imports SimpleIDE.Managers

Namespace Models
    
    Partial Public Class SourceFileInfo
        
        ' ===== Parsing Integration Methods =====
        
' Replace: SimpleIDE.Models.SourceFileInfo.RequestAsyncParse
' Replace: SimpleIDE.Models.SourceFileInfo.RequestAsyncParse
Public Sub RequestAsyncParse()
    Try
        ' Only request if we need parsing
        If Not pNeedsParsing Then Return
        
        pNeedsParsing = True
        
        ' Try to get ProjectManager reference
        If pProjectManager Is Nothing Then
            Dim lArgs As New ProjectManagerRequestEventArgs()
            Console.WriteLine($"RequestAsyncParse: Requesting ProjectManager for {FileName}")
            RaiseEvent ProjectManagerRequested(Me, lArgs)
            
            If lArgs.HasProjectManager Then
                pProjectManager = lArgs.ProjectManager
                ' Also set the public property
                ProjectManager = lArgs.ProjectManager
                Console.WriteLine($"RequestAsyncParse: Got ProjectManager for {FileName}")
            End If
        End If
        
        If pProjectManager IsNot Nothing Then
            ' Request async parse through ProjectManager
            Console.WriteLine($"RequestAsyncParse: Requesting parse for {FileName}")
            
            ' Queue the parse request using async parse
            Task.Run(Function() pProjectManager.ParseFileAsync(Me))
            
            ' Raise rendering changed to trigger immediate redraw
            RaiseEvent RenderingChanged(Me, EventArgs.Empty)
            
        Else
            Console.WriteLine($"RequestAsyncParse: No ProjectManager available for {FileName}")
            
            ' For emergency/temporary SourceFileInfo, still notify rendering change
            ' This allows basic text editing even without parsing
            RaiseEvent RenderingChanged(Me, EventArgs.Empty)
            
            ' Try again to get ProjectManager after a delay
            If pRetryProjectManagerTimer = 0 Then
                pRetryProjectManagerTimer = GLib.Timeout.Add(500, AddressOf RetryGetProjectManager)
            End If
        End If
        
    Catch ex As Exception
        Console.WriteLine($"RequestAsyncParse error: {ex.Message}")
    End Try
End Sub

''' <summary>
''' Timer callback to retry getting ProjectManager
''' </summary>
''' <returns>False to stop timer, True to continue</returns>
Private Function RetryGetProjectManager() As Boolean
    Static sRetryCount As Integer = 0
    
    Try
        If pProjectManager IsNot Nothing Then
            ' We got it, stop retrying
            pRetryProjectManagerTimer = 0
            sRetryCount = 0
            Console.WriteLine($"RetryGetProjectManager: ProjectManager now available for {FileName}")
            
            ' Request parse now that we have ProjectManager
            If pNeedsParsing Then
                RequestAsyncParse()
            End If
            
            Return False ' Stop timer
        End If
        
        ' Try to get it again
        Dim lArgs As New ProjectManagerRequestEventArgs()
        RaiseEvent ProjectManagerRequested(Me, lArgs)
        
        If lArgs.HasProjectManager Then
            pProjectManager = lArgs.ProjectManager
            ProjectManager = lArgs.ProjectManager ' Set both private and public
            Console.WriteLine($"RetryGetProjectManager: Got ProjectManager for {FileName}")
            
            ' Request parse now that we have ProjectManager
            If pNeedsParsing Then
                RequestAsyncParse()
            End If
            
            pRetryProjectManagerTimer = 0
            sRetryCount = 0
            Return False ' Stop timer
        End If
        
        ' Still no ProjectManager, keep trying (up to a limit)
        sRetryCount += 1
        
        If sRetryCount > 10 Then
            Console.WriteLine($"RetryGetProjectManager: Giving up after 10 attempts for {FileName}")
            pRetryProjectManagerTimer = 0
            sRetryCount = 0
            Return False ' Stop timer
        End If
        
        Return True ' Continue retrying
        
    Catch ex As Exception
        Console.WriteLine($"RetryGetProjectManager error: {ex.Message}")
        pRetryProjectManagerTimer = 0
        sRetryCount = 0
        Return False
    End Try
End Function

        
        ''' <summary>
        ''' Parse the content of dfthe file using the centralized ProjectParser
        ''' </summary>
        ''' <returns>True if parsing succeeded, False otherwise</returns>
        ''' <remarks>
        ''' This method delegates to ProjectManager.ParseFile() to ensure proper 
        ''' project-wide context and single parser instance.
        ''' </remarks>
        Public Function ParseContent() As Boolean
            Try
                ' Check if content is loaded
                If pTextLines Is Nothing OrElse pTextLines.Count = 0 Then
                    Console.WriteLine($"Cannot parse {FileName}: content not loaded")
                    Return False
                End If
                
                If pProjectManager Is Nothing Then
                    ' Request ProjectManager reference via event
                    Dim lEventArgs As New ProjectManagerRequestEventArgs()
                    RaiseEvent ProjectManagerRequested(Me, lEventArgs)
                    pProjectManager = lEventArgs.ProjectManager
                End If
                
                If pProjectManager IsNot Nothing Then
                    ' Delegate to ProjectManager for centralized parsing
                    Return pProjectManager.ParseFile(Me)
                Else
                    Console.WriteLine($"ParseContent: No ProjectManager available")
                    Return False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseContent error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Updates the LineMetadata and CharacterTokens from a parse result
        ''' </summary>
        ''' <param name="vParseResult">The parse result containing metadata</param>
        Public Sub UpdateFromParseResult(vParseResult As ParseResult)
            Try
                If vParseResult Is Nothing Then Return
                
                ' Update LineMetadata from parse result
                If vParseResult.LineMetadata IsNot Nothing Then
                    Dim lLineCount As Integer = Math.Min(pTextLines.Count, vParseResult.LineMetadata.Length)
                    
                    for i As Integer = 0 To lLineCount - 1
                        If vParseResult.LineMetadata(i) IsNot Nothing Then
                            ' Only update if line was actually parsed
                            Dim lNewMetadata As LineMetadata = vParseResult.LineMetadata(i)
                            
                            If lNewMetadata.ParseState = LineParseState.eParsed Then
                                If i < pLineMetadata.Length Then
                                    pLineMetadata(i) = lNewMetadata
                                    
                                    ' Update CharacterTokens using GetEncodedTokens
                                    If i < pCharacterTokens.Length Then
                                        Dim lLineLength As Integer = pTextLines(i).Length
                                        pCharacterTokens(i) = pLineMetadata(i).GetEncodedTokens(lLineLength)
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If
                
                ' Update parse state
                pNeedsParsing = False
                pLastParsed = DateTime.Now
                
                ' Store parse result tree if provided
                If vParseResult.RootNode IsNot Nothing Then
                    pSyntaxTree = vParseResult.RootNode
                End If
                
                ' Copy parse errors if any
                If vParseResult.Errors IsNot Nothing AndAlso vParseResult.Errors.Count > 0 Then
                    pParseErrors = vParseResult.Errors
                Else
                    If pParseErrors IsNot Nothing Then pParseErrors.Clear()
                End If
                
                Console.WriteLine($"Updated from parse result: {FileName}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFromParseResult error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Notifies listeners that rendering has changed for the specified lines
        ''' </summary>
        ''' <param name="vStartLine">Starting line that changed</param>
        ''' <param name="vEndLine">Ending line that changed</param>
        Public Sub NotifyRenderingChanged(vStartLine As Integer, vEndLine As Integer)
            Try
                ' Raise the rendering changed event
                RaiseEvent RenderingChanged(Me, EventArgs.Empty)
                
                ' Also raise text lines changed for more specific notification
                Dim lArgs As New TextLinesChangedEventArgs() with {
                    .ChangeType = TextChangeType.eMultipleLines,
                    .StartLine = vStartLine,
                    .EndLine = vEndLine,
                    .LinesAffected = vEndLine - vStartLine + 1,
                    .NewLineCount = pTextLines.Count
                }
                RaiseEvent TextLinesChanged(Me, lArgs)
                
            Catch ex As Exception
                Console.WriteLine($"NotifyRenderingChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Ensures CharacterTokens array is properly sized and initialized
        ''' </summary>
        ''' <remarks>
        ''' Called by ProjectManager after parsing to ensure arrays are ready
        ''' </remarks>
        Public Sub EnsureCharacterTokens()
            Try
                Dim lLineCount As Integer = pTextLines.Count
                
                ' Ensure CharacterTokens array matches line count
                If pCharacterTokens Is Nothing OrElse pCharacterTokens.Length <> lLineCount Then
                    ReDim pCharacterTokens(lLineCount - 1)
                End If
                
                ' Initialize tokens for each line using LineMetadata
                for i As Integer = 0 To lLineCount - 1
                    Dim lLineLength As Integer = pTextLines(i).Length
                    
                    If pLineMetadata IsNot Nothing AndAlso i < pLineMetadata.Length AndAlso pLineMetadata(i) IsNot Nothing Then
                        ' Use GetEncodedTokens from LineMetadata
                        pCharacterTokens(i) = pLineMetadata(i).GetEncodedTokens(lLineLength)
                    Else
                        ' Fallback: Create default tokens
                        If lLineLength > 0 Then
                            ReDim pCharacterTokens(i)(lLineLength - 1)
                            Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                            for j As Integer = 0 To lLineLength - 1
                                pCharacterTokens(i)(j) = lDefaultToken
                            Next
                        Else
                            pCharacterTokens(i) = New Byte() {}
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"EnsureCharacterTokens error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the CharacterTokens from LineMetadata after parsing
        ''' </summary>
        ''' <remarks>
        ''' Used by ProjectManager to update tokens after parse completion
        ''' </remarks>
        Public Sub UpdateCharacterTokensFromMetadata()
            Try
                If pLineMetadata Is Nothing Then Return
                
                Dim lLineCount As Integer = Math.Min(pTextLines.Count, pLineMetadata.Length)
                
                ' Ensure CharacterTokens array is sized correctly
                If pCharacterTokens Is Nothing OrElse pCharacterTokens.Length <> lLineCount Then
                    ReDim pCharacterTokens(lLineCount - 1)
                End If
                
                ' Update tokens for each line
                for i As Integer = 0 To lLineCount - 1
                    If pLineMetadata(i) IsNot Nothing Then
                        Dim lLineLength As Integer = pTextLines(i).Length
                        pCharacterTokens(i) = pLineMetadata(i).GetEncodedTokens(lLineLength)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"UpdateCharacterTokensFromMetadata error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets or creates LineMetadata for a specific line
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <returns>The LineMetadata for the specified line</returns>
        Public Function GetLineMetadata(vLineIndex As Integer) As LineMetadata
            Try
                ' Validate line index
                If vLineIndex < 0 Then
                    Return New LineMetadata()
                End If
                
                ' Ensure arrays are large enough
                If pLineMetadata Is Nothing OrElse vLineIndex >= pLineMetadata.Length Then
                    ReDim Preserve pLineMetadata(vLineIndex)
                    ReDim Preserve pCharacterTokens(vLineIndex)
                    
                    for i As Integer = 0 To vLineIndex
                        If pLineMetadata(i) Is Nothing Then
                            pLineMetadata(i) = New LineMetadata()
                        End If
                        
                        ' Initialize CharacterTokens for this line
                        If i < pTextLines.Count Then
                            Dim lLineLength As Integer = pTextLines(i).Length
                            pCharacterTokens(i) = pLineMetadata(i).GetEncodedTokens(lLineLength)
                        End If
                    Next
                End If
                
                ' Ensure the specific element is not Nothing
                If pLineMetadata(vLineIndex) Is Nothing Then
                    pLineMetadata(vLineIndex) = New LineMetadata()
                    ' Update hash if we have the text
                    If pTextLines IsNot Nothing AndAlso vLineIndex < pTextLines.Count Then
                        pLineMetadata(vLineIndex).UpdateHash(pTextLines(vLineIndex))
                        
                        ' Initialize CharacterTokens for this line
                        Dim lLineLength As Integer = pTextLines(vLineIndex).Length
                        If vLineIndex < pCharacterTokens.Length Then
                            pCharacterTokens(vLineIndex) = pLineMetadata(vLineIndex).GetEncodedTokens(lLineLength)
                        End If
                    End If
                End If
                
                Return pLineMetadata(vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"GetLineMetadata error: {ex.Message}")
                Return New LineMetadata()
            End Try
        End Function

        ''' <summary>
        ''' Forces immediate parsing and syntax coloring for a range of lines
        ''' </summary>
        ''' <param name="vStartLine">Zero-based start line index</param>
        ''' <param name="vEndLine">Zero-based end line index</param>
        ''' <remarks>
        ''' This provides immediate visual feedback for paste operations.
        ''' Now uses GetEncodedTokens for consistent token generation after parsing.
        ''' </remarks>
        Public Sub ForceImmediateParsing(vStartLine As Integer, vEndLine As Integer)
            Try
                Console.WriteLine($"SourceFileInfo.ForceImmediateParsing: Parsing lines {vStartLine} to {vEndLine}")
                
                ' Ensure we have valid line indices
                vStartLine = Math.Max(0, vStartLine)
                vEndLine = Math.Min(vEndLine, TextLines.Count - 1)
                
                If vStartLine > vEndLine Then Return
                
                
                ' Parse and color each line in the range
                Dim lTokenCount As Integer = 0
                for lineIdx As Integer = vStartLine To vEndLine
                    Try
                        Dim lLineText As String = TextLines(lineIdx)
                        Dim lLineLength As Integer = lLineText.Length
                        
                        ' Parse the line to get tokens
                        Dim lTokens As List(Of SyntaxToken) = ParseLine(lLineText, lineIdx)
                        
                        If lTokens IsNot Nothing AndAlso lTokens.Count > 0 Then
                            lTokenCount += lTokens.Count
                            
                            ' Update LineMetadata with tokens
                            If LineMetadata IsNot Nothing AndAlso lineIdx < LineMetadata.Length Then
                                If LineMetadata(lineIdx) Is Nothing Then
                                    LineMetadata(lineIdx) = New LineMetadata()
                                End If
                                LineMetadata(lineIdx).SyntaxTokens = lTokens
                                LineMetadata(lineIdx).UpdateHash(lLineText)
                                LineMetadata(lineIdx).ParseState = LineParseState.eParsed
                                
                                ' Update CharacterTokens using GetEncodedTokens
                                If pCharacterTokens IsNot Nothing AndAlso lineIdx < pCharacterTokens.Length Then
                                    pCharacterTokens(lineIdx) = LineMetadata(lineIdx).GetEncodedTokens(lLineLength)
                                End If
                            End If
                        Else
                            ' No tokens found, apply default tokens using GetEncodedTokens
                            If LineMetadata IsNot Nothing AndAlso lineIdx < LineMetadata.Length Then
                                If LineMetadata(lineIdx) Is Nothing Then
                                    LineMetadata(lineIdx) = New LineMetadata()
                                End If
                                LineMetadata(lineIdx).SyntaxTokens.Clear()  ' Clear to ensure defaults
                                LineMetadata(lineIdx).UpdateHash(lLineText)
                                LineMetadata(lineIdx).ParseState = LineParseState.eParsed
                                
                                If pCharacterTokens IsNot Nothing AndAlso lineIdx < pCharacterTokens.Length Then
                                    pCharacterTokens(lineIdx) = LineMetadata(lineIdx).GetEncodedTokens(lLineLength)
                                End If
                            End If
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"ForceImmediateParsing line {lineIdx} error: {ex.Message}")
                        ' Apply default tokens on error
                        ApplyDefaultTokens(lineIdx)
                    End Try
                Next
                
                Console.WriteLine($"SourceFileInfo.ForceImmediateParsing: Parsed {lTokenCount} tokens in {vEndLine - vStartLine + 1} lines")
                
                ' Notify that rendering has changed
                NotifyRenderingChanged(vStartLine, vEndLine)
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ForceImmediateParsing error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates character tokens for a single line based on syntax tokens
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vTokens">List of syntax tokens for the line</param>
        ''' <remarks>
        ''' This method now delegates to LineMetadata.GetEncodedTokens for consistency.
        ''' It stores only token types, not actual color strings.
        ''' </remarks>
        Public Sub UpdateCharacterTokens(vLineIndex As Integer, vTokens As List(Of SyntaxToken))
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= pTextLines.Count Then Return
                If pCharacterTokens Is Nothing OrElse vLineIndex >= pCharacterTokens.Length Then Return
                
                Dim lLineText As String = pTextLines(vLineIndex)
                Dim lLineLength As Integer = lLineText.Length
                
                ' Ensure we have LineMetadata for this line
                If pLineMetadata IsNot Nothing AndAlso vLineIndex < pLineMetadata.Length Then
                    If pLineMetadata(vLineIndex) Is Nothing Then
                        pLineMetadata(vLineIndex) = New LineMetadata()
                    End If
                    
                    ' Update the LineMetadata's SyntaxTokens
                    pLineMetadata(vLineIndex).SyntaxTokens = If(vTokens, New List(Of SyntaxToken)())
                    
                    ' Use GetEncodedTokens to generate the byte array
                    pCharacterTokens(vLineIndex) = pLineMetadata(vLineIndex).GetEncodedTokens(lLineLength)
                Else
                    ' Fallback: Create default tokens if LineMetadata not available
                    If lLineLength > 0 Then
                        ReDim pCharacterTokens(vLineIndex)(lLineLength - 1)
                        Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                        for i As Integer = 0 To lLineLength - 1
                            pCharacterTokens(vLineIndex)(i) = lDefaultToken
                        Next
                    Else
                        pCharacterTokens(vLineIndex) = New Byte() {}
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateCharacterTokens error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Maps a TokenType to a SyntaxTokenType
        ''' </summary>
        ''' <param name="vTokenType">The token type to map</param>
        ''' <returns>The corresponding syntax token type</returns>
        Private Function MapTokenTypeToSyntaxType(vTokenType As TokenType) As SyntaxTokenType
            Select Case vTokenType
                Case TokenType.eKeyword
                    Return SyntaxTokenType.eKeyword
                Case TokenType.eIdentifier
                    Return SyntaxTokenType.eIdentifier
                Case TokenType.eStringLiteral
                    Return SyntaxTokenType.eString
                Case TokenType.eNumber
                    Return SyntaxTokenType.eNumber
                Case TokenType.eComment
                    Return SyntaxTokenType.eComment
                Case TokenType.eOperator
                    Return SyntaxTokenType.eOperator
                Case TokenType.eType
                    Return SyntaxTokenType.eType
                Case Else
                    Return SyntaxTokenType.eNormal
            End Select
        End Function

        ''' <summary>
        ''' Applies default token types to a line for immediate visual feedback
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <remarks>
        ''' Used to provide immediate visual feedback while waiting for async parse
        ''' </remarks>
        Private Sub ApplyDefaultTokens(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then Return
                If CharacterTokens Is Nothing OrElse vLineIndex >= CharacterTokens.Length Then Return
                
                Dim lLineLength As Integer = TextLines(vLineIndex).Length
                
                If lLineLength > 0 Then
                    ' Ensure array is properly sized
                    If CharacterTokens(vLineIndex) Is Nothing OrElse 
                       CharacterTokens(vLineIndex).Length <> lLineLength Then
                        ReDim CharacterTokens(vLineIndex)(lLineLength - 1)
                    End If
                    
                    ' Apply default token to all characters
                    Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                    for i As Integer = 0 To lLineLength - 1
                        CharacterTokens(vLineIndex)(i) = lDefaultToken
                    Next
                Else
                    CharacterTokens(vLineIndex) = New Byte() {}
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyDefaultTokens error: {ex.Message}")
            End Try
        End Sub

        ''' <remarks>
        ''' Provided for external request to parse a single line.
        ''' </remarks>
        Public Sub ParseLine(vLineIndex as Integer)
            SetLineMetadataAndCharacterTokens(vLineIndex)
        End Sub

        ''' <summary>
        ''' Parses a single line of text and returns syntax tokens with colors
        ''' </summary>
        ''' <param name="vLineText">The text of the line to parse</param>
        ''' <param name="vLineIndex">The zero-based index of the line</param>
        ''' <returns>List of syntax tokens with color information</returns>
        ''' <remarks>
        ''' This method is used for immediate syntax highlighting during paste operations.
        ''' It works with the ProjectManager to tokenize the line and apply theme colors.
        ''' </remarks>
        Public Function ParseLine(vLineText As String, vLineIndex As Integer) As List(Of SyntaxToken)
            Try
                Dim lTokens As New List(Of SyntaxToken)()
                
                If String.IsNullOrEmpty(vLineText) Then Return lTokens
                
                ' Ensure we have a ProjectManager
                If pProjectManager Is Nothing Then
                    ' Try to get it
                    Dim lArgs As New ProjectManagerRequestEventArgs()
                    RaiseEvent ProjectManagerRequested(Me, lArgs)
                    If lArgs.HasProjectManager Then
                        pProjectManager = lArgs.ProjectManager
                    End If
                End If
                
                ' If still no ProjectManager, return empty
                If pProjectManager Is Nothing Then
                    Console.WriteLine($"ParseLine: No ProjectManager available")
                    Return lTokens
                End If
                
                ' Use VBTokenizer to tokenize the line
                Dim lTokenizer As New Syntax.VBTokenizer()
                Dim lRawTokens As List(Of Syntax.Token) = lTokenizer.TokenizeLine(vLineText)
                
                ' Convert Token to SyntaxToken with colors
                for each lRawToken in lRawTokens
                    ' Map token type
                    Dim lSyntaxType As SyntaxTokenType = MapTokenTypeToSyntaxType(lRawToken.Type) 
                   
                    ' Create SyntaxToken with color
                    Dim lSyntaxToken As New SyntaxToken(
                        lRawToken.StartColumn,
                        lRawToken.EndColumn - lRawToken.StartColumn + 1,
                        lSyntaxType
                    )
                    
                    lTokens.Add(lSyntaxToken)
                Next
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"ParseLine error: {ex.Message}")
                Return New List(Of SyntaxToken)()
            End Try
        End Function

        ''' <summary>
        ''' Updates the LineMetadata array with new data
        ''' </summary>
        ''' <param name="vNewLineMetadata">The new metadata to copy</param>
        Public Sub UpdateLineMetadata(vNewLineMetadata As LineMetadata())
            If vNewLineMetadata Is Nothing Then
                pLineMetadata = Nothing
                Return
            End If
            
            ' Resize if needed
            If pLineMetadata Is Nothing OrElse pLineMetadata.Length <> vNewLineMetadata.Length Then
                ReDim pLineMetadata(vNewLineMetadata.Length - 1)
            End If
            
            ' Copy the contents
            for i As Integer = 0 To vNewLineMetadata.Length - 1
                pLineMetadata(i) = vNewLineMetadata(i)
            Next
        End Sub

''' <summary>
''' Ensures this SourceFileInfo has a ProjectManager connection
''' </summary>
''' <returns>True if ProjectManager is available, False otherwise</returns>
''' <remarks>
''' This method attempts to get a ProjectManager reference if not already available
''' </remarks>
Public Function EnsureProjectManagerConnection() As Boolean
    Try
        ' If we already have it, we're good
        If pProjectManager IsNot Nothing Then Return True
        
        ' Try to get it via event
        Dim lArgs As New ProjectManagerRequestEventArgs()
        Console.WriteLine($"SourceFileInfo.EnsureProjectManagerConnection: Requesting ProjectManager for {FileName}")
        RaiseEvent ProjectManagerRequested(Me, lArgs)
        
        If lArgs.HasProjectManager Then
            pProjectManager = lArgs.ProjectManager
            ProjectManager = lArgs.ProjectManager ' Set both private and public property
            Console.WriteLine($"SourceFileInfo.EnsureProjectManagerConnection: Got ProjectManager for {FileName}")
            Return True
        Else
            Console.WriteLine($"SourceFileInfo.EnsureProjectManagerConnection: No ProjectManager available for {FileName}")
            Return False
        End If
        
    Catch ex As Exception
        Console.WriteLine($"SourceFileInfo.EnsureProjectManagerConnection error: {ex.Message}")
        Return False
    End Try
End Function
        
    End Class
    
End Namespace 
