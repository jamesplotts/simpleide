' Models/SourceFileInfo.InternalOperations.vb - Internal helper methods for text operations
' Created: 2025-01-10
Imports System
Imports System.Collections.Generic

Namespace Models
    
    Partial Public Class SourceFileInfo
        
        ' ===== Internal Text Manipulation Helpers =====
        
        ''' <summary>
        ''' Internal method to insert text within a single line
        ''' </summary>
        Private Sub InsertSingleLineTextInternal(vLine As Integer, vColumn As Integer, vText As String)
            Dim lOldLine As String = pTextLines(vLine)
            Dim lOldLength As Integer = lOldLine.Length
            
            ' Validate and adjust column
            vColumn = Math.Max(0, Math.Min(vColumn, lOldLength))
            
            ' Build new line
            Dim lNewLine As String
            If vColumn = 0 Then
                lNewLine = vText & lOldLine
            ElseIf vColumn >= lOldLength Then
                lNewLine = lOldLine & vText
            Else
                lNewLine = lOldLine.Substring(0, vColumn) & vText & lOldLine.Substring(vColumn)
            End If
            
            ' Update the line
            pTextLines(vLine) = lNewLine
            
            ' Update metadata and tokens
            SetLineMetadataAndCharacterTokens(vLine)
            
            ' Update state
            pIsModified = True
            pNeedsParsing = True
            
            ' Raise event
            RaiseTextChangedEvent(TextChangeType.eLineModified, vLine, vLine, 1)
        End Sub
        
        ''' <summary>
        ''' Internal method to insert multi-line text
        ''' </summary>
        Private Sub InsertMultiLineTextInternal(vLine As Integer, vColumn As Integer, vNewLines() As String)
            If vNewLines.Length = 0 Then Return
            
            ' Get current line text
            Dim lCurrentLine As String = pTextLines(vLine)
            
            ' Validate column
            vColumn = Math.Max(0, Math.Min(vColumn, lCurrentLine.Length))
            
            ' Split current line at insertion point
            Dim lBeforeInsert As String = lCurrentLine.Substring(0, vColumn)
            Dim lAfterInsert As String = If(vColumn < lCurrentLine.Length, lCurrentLine.Substring(vColumn), "")
            
            ' Update first line
            pTextLines(vLine) = lBeforeInsert & vNewLines(0)
            SetLineMetadataAndCharacterTokens(vLine)

            
            ' Update tokens for first line
            If pCharacterTokens IsNot Nothing AndAlso vLine < pCharacterTokens.Length Then
                pCharacterTokens(vLine) = pLineMetadata(vLine).GetEncodedTokens(pTextLines(vLine).Length)
            End If
            
            ' Insert middle lines
            Dim lInsertPosition As Integer = vLine + 1
            for i As Integer = 1 To vNewLines.Length - 2
                InsertLineInternal(lInsertPosition, vNewLines(i))
                lInsertPosition += 1
            Next
            
            ' Insert last line with remainder
            InsertLineInternal(lInsertPosition, vNewLines(vNewLines.Length - 1) & lAfterInsert)
            
            ' Update state
            pIsModified = True
            pNeedsParsing = True
            
            ' Raise event
            RaiseTextChangedEvent(TextChangeType.eMultipleLines, vLine, vLine + vNewLines.Length - 1, vNewLines.Length)
        End Sub

        ''' <summary>
        ''' Internal method to delete multi-line text
        ''' </summary>
        Private Sub DeleteMultiLineTextInternal(vStartLine As Integer, vStartCol As Integer, 
                                               vEndLine As Integer, vEndCol As Integer)
            ' Get the text we're keeping from first and last lines
            Dim lFirstLine As String = pTextLines(vStartLine)
            Dim lLastLine As String = pTextLines(vEndLine)
            
            ' Validate columns
            vStartCol = Math.Max(0, Math.Min(vStartCol, lFirstLine.Length))
            vEndCol = Math.Max(0, Math.Min(vEndCol, lLastLine.Length))
            
            ' Build the resulting line
            Dim lResultLine As String = lFirstLine.Substring(0, vStartCol) & 
                                       If(vEndCol < lLastLine.Length, lLastLine.Substring(vEndCol), "")
            
            ' Update the first line
            pTextLines(vStartLine) = lResultLine
            SetLineMetadataAndCharacterTokens(vStartLine)
            
            ' Delete the lines in between (from end to start to maintain indices)
            for i As Integer = vEndLine To vStartLine + 1 Step -1
                DeleteLine(i)
            Next
            
            ' Update state
            pIsModified = True
            pNeedsParsing = True
            
            ' Raise event
            RaiseTextChangedEvent(TextChangeType.eMultipleLines, vStartLine, vStartLine, vEndLine - vStartLine + 1)
        End Sub
        
        ''' <summary>
        ''' Internal method to insert a new line with proper metadata handling
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based index where line should be inserted</param>
        ''' <param name="vText">Text content of the new line</param>
        ''' <remarks>
        ''' FIXED: Properly handles metadata creation for inserted lines during multi-line paste
        ''' </remarks>
        Private Sub InsertLineInternal(vLineIndex As Integer, vText As String)
            Try
                ' Validate index
                If vLineIndex < 0 Then vLineIndex = 0
                If vLineIndex > pTextLines.Count Then vLineIndex = pTextLines.Count
                
                ' Insert the line into text collection
                pTextLines.Insert(vLineIndex, If(vText, ""))
                
                ' Update LineMetadata array
                If pLineMetadata IsNot Nothing Then
                    Dim lOldLength As Integer = pLineMetadata.Length
                    ' Expand array to accommodate new line
                    ReDim Preserve pLineMetadata(lOldLength)
                    
                    ' Shift existing metadata down (work backwards to avoid overwriting)
                    For i As Integer = lOldLength To vLineIndex + 1 Step -1
                        pLineMetadata(i) = pLineMetadata(i - 1)
                    Next
                    
                    ' CRITICAL FIX: Create new metadata instance for the inserted line
                    ' Don't reuse or copy existing metadata
                    pLineMetadata(vLineIndex) = New LineMetadata()
                    
                    ' Parse the new line to get its tokens
                    Dim lTokens As List(Of SyntaxToken) = ParseLine(vText, vLineIndex)
                    
                    ' Set the metadata for the new line
                    If lTokens IsNot Nothing Then
                        pLineMetadata(vLineIndex).SyntaxTokens = lTokens
                    Else
                        pLineMetadata(vLineIndex).SyntaxTokens = New List(Of SyntaxToken)()
                    End If
                    
                    pLineMetadata(vLineIndex).UpdateHash(vText)
                    pLineMetadata(vLineIndex).ParseState = LineParseState.eParsed
                    
                    Console.WriteLine($"InsertLineInternal: Created new metadata for line {vLineIndex} with {pLineMetadata(vLineIndex).SyntaxTokens.Count} tokens")
                End If
                
                ' Update CharacterTokens array
                If pCharacterTokens IsNot Nothing Then
                    Dim lOldLength As Integer = pCharacterTokens.Length
                    ' Expand array to accommodate new line
                    ReDim Preserve pCharacterTokens(lOldLength)
                    
                    ' Shift existing tokens down (work backwards to avoid overwriting)
                    For i As Integer = lOldLength To vLineIndex + 1 Step -1
                        pCharacterTokens(i) = pCharacterTokens(i - 1)
                    Next
                    
                    ' CRITICAL FIX: Generate new tokens for the inserted line
                    ' Use the metadata we just created to generate proper tokens
                    Dim lLineLength As Integer = If(vText?.Length, 0)
                    If lLineLength > 0 AndAlso pLineMetadata(vLineIndex) IsNot Nothing Then
                        pCharacterTokens(vLineIndex) = pLineMetadata(vLineIndex).GetEncodedTokens(lLineLength)
                        Console.WriteLine($"InsertLineInternal: Generated {lLineLength} character tokens for line {vLineIndex}")
                    Else
                        pCharacterTokens(vLineIndex) = New Byte() {}
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InsertLineInternal error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                
                ' Ensure we at least have valid arrays to prevent crashes
                Try
                    If vLineIndex >= 0 AndAlso vLineIndex < pTextLines.Count Then
                        ' Ensure metadata exists
                        If pLineMetadata IsNot Nothing AndAlso vLineIndex < pLineMetadata.Length Then
                            If pLineMetadata(vLineIndex) Is Nothing Then
                                pLineMetadata(vLineIndex) = New LineMetadata()
                                pLineMetadata(vLineIndex).ParseState = LineParseState.eUnparsed
                            End If
                        End If
                        
                        ' Ensure character tokens exist
                        If pCharacterTokens IsNot Nothing AndAlso vLineIndex < pCharacterTokens.Length Then
                            If pCharacterTokens(vLineIndex) Is Nothing Then
                                pCharacterTokens(vLineIndex) = New Byte() {}
                            End If
                        End If
                    End If
                Catch
                    ' Ignore errors in error handler
                End Try
            End Try
        End Sub

        ''' <summary>
        ''' Internal helper for multi-line text insertion with proper metadata handling
        ''' </summary>
        ''' <param name="vLine">Zero-based line index where insertion starts</param>
        ''' <param name="vColumn">Zero-based column index in the line</param>
        ''' <param name="vText">Multi-line text to insert (contains newlines)</param>
        ''' <remarks>
        ''' FIXED: Ensures each inserted line gets its own metadata and tokens
        ''' </remarks>
        Private Sub InsertMultiLineTextInternal(vLine As Integer, vColumn As Integer, vText As String)
            Try
                ' Split text into lines
                Dim lNewLines() As String = vText.Split({vbLf}, StringSplitOptions.None)
                If lNewLines.Length = 0 Then Return
                
                Console.WriteLine($"InsertMultiLineTextInternal: Inserting {lNewLines.Length} lines at line {vLine}, column {vColumn}")
                
                ' Get current line
                Dim lCurrentLine As String = TextLines(vLine)
                vColumn = Math.Max(0, Math.Min(vColumn, lCurrentLine.Length))
                
                ' Split current line at insertion point
                Dim lBeforeInsert As String = lCurrentLine.Substring(0, vColumn)
                Dim lAfterInsert As String = If(vColumn < lCurrentLine.Length, lCurrentLine.Substring(vColumn), "")
                
                ' Update first line (combine with first new line)
                Dim lFirstNewLine As String = lBeforeInsert & lNewLines(0)
                pTextLines(vLine) = lFirstNewLine
                
                ' CRITICAL FIX: Update metadata for the modified first line
                SetLineMetadataAndCharacterTokens(vLine)
                Console.WriteLine($"InsertMultiLineTextInternal: Updated first line {vLine} with '{lFirstNewLine.Substring(0, Math.Min(20, lFirstNewLine.Length))}...'")
                
                ' Insert middle lines (if any)
                Dim lInsertPosition As Integer = vLine + 1
                For i As Integer = 1 To lNewLines.Length - 2
                    ' Insert each middle line
                    InsertLineInternal(lInsertPosition, lNewLines(i))
                    Console.WriteLine($"InsertMultiLineTextInternal: Inserted middle line at {lInsertPosition}")
                    lInsertPosition += 1
                Next
                
                ' Insert last line with remainder of original line
                Dim lLastNewLine As String = lNewLines(lNewLines.Length - 1) & lAfterInsert
                InsertLineInternal(lInsertPosition, lLastNewLine)
                Console.WriteLine($"InsertMultiLineTextInternal: Inserted last line at {lInsertPosition} with '{lLastNewLine.Substring(0, Math.Min(20, lLastNewLine.Length))}...'")
                
                ' CRITICAL FIX: Force immediate parsing of all affected lines
                ' This ensures proper syntax coloring after multi-line paste
                Dim lEndLine As Integer = vLine + lNewLines.Length - 1
                If pProjectManager IsNot Nothing Then
                    ' Request immediate parsing for visual feedback
                    Console.WriteLine($"InsertMultiLineTextInternal: Requesting immediate parse for lines {vLine} to {lEndLine}")
                    ForceImmediateParsing(vLine, lEndLine)
                Else
                    ' Fallback: At least ensure we have tokens for all lines
                    For i As Integer = vLine To lEndLine
                        If i < pLineMetadata.Length AndAlso pLineMetadata(i) IsNot Nothing Then
                            ' Ensure tokens are generated
                            If i < pCharacterTokens.Length Then
                                Dim lLineLength As Integer = pTextLines(i).Length
                                pCharacterTokens(i) = pLineMetadata(i).GetEncodedTokens(lLineLength)
                            End If
                        End If
                    Next
                End If
                
                ' Update state
                pIsModified = True
                pNeedsParsing = True
                
                ' Raise event for multi-line change
                RaiseTextChangedEvent(TextChangeType.eMultipleLines, vLine, lEndLine, lNewLines.Length)
                
                ' Request async parse for full document context
                RequestAsyncParse()
                
                Console.WriteLine($"InsertMultiLineTextInternal: Completed insertion of {lNewLines.Length} lines")
                
            Catch ex As Exception
                Console.WriteLine($"InsertMultiLineTextInternal error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub
        
    End Class
    
End Namespace