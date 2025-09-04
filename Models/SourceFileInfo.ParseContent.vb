' Models/SourceFileInfo.ParseContent.vb - Enhanced parsing with project root namespace
Imports System
Imports System.IO
Imports SimpleIDE.Syntax

' SourceFileInfo.ParseContent.vb
' Created: 2025-08-10 13:36:13

Namespace Models
    
    Partial Public Class SourceFileInfo
        
        ' Store the project root namespace for parsing
        Private pProjectRootNamespace As String = "SimpleIDE"
        

        ''' <summary>
        ''' Parse the content of the file using the centralized ProjectParser
        ''' </summary>
        ''' <returns>True if parsing succeeded, False otherwise</returns>
        ''' <remarks>
        ''' This method delegates to ProjectManager.ParseFile() to ensure proper 
        ''' project-wide context and single parser instance.
        ''' </remarks>
        Public Function ParseContent() As Boolean
            Try
                ' Check if content is loaded
                If Not IsLoaded AndAlso Not IsDemoMode Then
                    Console.WriteLine($"Cannot parse {FileName}: content not loaded")
                    Return False
                End If
                
                If pProjectManager Is Nothing Then
                    ' Request ProjectManager reference via event
                    Dim lEventArgs As New ProjectManagerRequestEventArgs()
                    RaiseEvent ProjectManagerRequested(Me, lEventArgs)
                    pProjectManager = lEventArgs.ProjectManager
                End If
                
                If Not pProjectManager Is Nothing Then
                    ' Delegate to ProjectManager for centralized parsing
                    'Console.WriteLine($"SourceFileInfo.ParseContent: Delegating to ProjectManager for {FileName}")
                    Return pProjectManager.ParseFile(Me)
                Else
                    Console.WriteLine($"SourceFileInfo.ParseContent: No ProjectManager available for {FileName}")
                    Console.WriteLine($"  Parsing requires ProjectManager for proper context")
                    Return False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ParseContent error: {ex.Message}")
                
                ' Add error to ParseErrors collection
                If ParseErrors Is Nothing Then
                    ParseErrors = New List(Of ParseError)()
                End If
                
                ParseErrors.Add(New ParseError with {
                    .Message = ex.Message,
                    .Line = 0,
                    .Column = 0,
                    .Severity = ParseErrorSeverity.eError
                })
                
                Return False
            End Try
        End Function

       
        ''' <summary>
        ''' Event raised when text lines are modified
        ''' </summary>
        Public Event TextLinesChanged(sender As Object, e As TextLinesChangedEventArgs)
        
        ''' <summary>
        ''' Event arguments for TextLinesChanged event
        ''' </summary>
        Public Class TextLinesChangedEventArgs
            Inherits EventArgs
            
            Public Property ChangeType As TextChangeType
            Public Property StartLine As Integer
            Public Property EndLine As Integer
            Public Property LinesAffected As Integer
            Public Property NewLineCount As Integer
        End Class
        
        ''' <summary>
        ''' Types of text changes
        ''' </summary>
        Public Enum TextChangeType
            eUnspecified
            eLineInserted
            eLineDeleted
            eLineModified
            eMultipleLines
            eCompleteReplace
            eLastValue
        End Enum
        
        ''' <summary>
        ''' Inserts a new line at the specified position
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based index where to insert the line</param>
        ''' <param name="vText">The text for the new line</param>
        ''' <remarks>
        ''' Updates TextLines, LineMetadata, and CharacterColors arrays
        ''' </remarks>
        Public Sub InsertLine(vLineIndex As Integer, vText As String)
            Try
                ' Validate index
                If vLineIndex < 0 OrElse vLineIndex > pTextLines.Count Then
                    Console.WriteLine($"InsertLine: Invalid index {vLineIndex}, TextLines.Count={pTextLines.Count}")
                    Return
                End If
                
                ' Insert the line
                pTextLines.Insert(vLineIndex, If(vText, ""))
                
                ' Update LineMetadata array
                If pLineMetadata IsNot Nothing Then
                    ' Resize array
                    ReDim Preserve pLineMetadata(pTextLines.Count - 1)
                    
                    ' Shift existing metadata down
                    for i As Integer = pTextLines.Count - 1 To vLineIndex + 1 Step -1
                        pLineMetadata(i) = pLineMetadata(i - 1)
                    Next
                    
                    ' Create new metadata for inserted line
                    pLineMetadata(vLineIndex) = New LineMetadata()
                    pLineMetadata(vLineIndex).UpdateHash(vText)
                    pLineMetadata(vLineIndex).ParseState = LineParseState.eUnparsed
                End If
                
                
                ' Mark as modified and needs parsing
                IsModified = True
                NeedsParsing = True
                
                ' Raise event
                Dim lArgs As New TextLinesChangedEventArgs() with {
                    .ChangeType = TextChangeType.eLineInserted,
                    .StartLine = vLineIndex,
                    .EndLine = vLineIndex,
                    .LinesAffected = 1,
                    .NewLineCount = pTextLines.Count
                }
                RaiseEvent TextLinesChanged(Me, lArgs)
                
                ' Request async re-parse for this line
                RequestAsyncParse()
                
            Catch ex As Exception
                Console.WriteLine($"InsertLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes an entire line including newline
        ''' </summary>
        Public Sub DeleteLine(vLineIndex As Integer)
            If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then Return
            
            TextLines.RemoveAt(vLineIndex)
            
            ' Update arrays
            If pLineMetadata IsNot Nothing AndAlso vLineIndex < pLineMetadata.Length Then
                ' Shift metadata down
                for i As Integer = vLineIndex To pLineMetadata.Length - 2
                    pLineMetadata(i) = pLineMetadata(i + 1)
                Next
                ReDim Preserve pLineMetadata(LineMetadata.Length - 2)
            End If
            
            
            IsModified = True
            NeedsParsing = True
        End Sub

        
        ''' <summary>
        ''' Deletes multiple lines
        ''' </summary>
        ''' <param name="vStartLine">First line to delete (inclusive)</param>
        ''' <param name="vCount">Number of lines to delete</param>
        Public Sub DeleteLines(vStartLine As Integer, vCount As Integer)
            Try
                ' Validate parameters
                If vStartLine < 0 OrElse vStartLine >= TextLines.Count OrElse vCount <= 0 Then
                    Return
                End If
                
                ' Calculate actual count to remove
                Dim lActualCount As Integer = Math.Min(vCount, TextLines.Count - vStartLine)
                
                ' Ensure we keep at least one line
                If TextLines.Count - lActualCount < 1 Then
                    lActualCount = TextLines.Count - 1
                End If
                
                If lActualCount <= 0 Then Return
                
                ' Remove the lines
                for i As Integer = 0 To lActualCount - 1
                    TextLines.RemoveAt(vStartLine)
                Next
                
                ' Update LineMetadata array
                If pLineMetadata IsNot Nothing Then
                    ' Shift remaining metadata up
                    for i As Integer = vStartLine To TextLines.Count - 1
                        If i + lActualCount < pLineMetadata.Length Then
                            pLineMetadata(i) = pLineMetadata(i + lActualCount)
                        End If
                    Next
                    
                    ' Resize array
                    ReDim Preserve pLineMetadata(TextLines.Count - 1)
                End If
                
                
                ' Mark as modified
                IsModified = True
                NeedsParsing = True
                
                ' Raise event
                Dim lArgs As New TextLinesChangedEventArgs() with {
                    .ChangeType = TextChangeType.eMultipleLines,
                    .StartLine = vStartLine,
                    .EndLine = vStartLine + lActualCount - 1,
                    .LinesAffected = lActualCount,
                    .NewLineCount = TextLines.Count
                }
                RaiseEvent TextLinesChanged(Me, lArgs)
                
            Catch ex As Exception
                Console.WriteLine($"DeleteLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Joins two lines together
        ''' </summary>
        ''' <param name="vLineIndex">Index of the first line (will be joined with the next line)</param>
        Public Sub JoinLines(vLineIndex As Integer)
            Try
                ' Validate index
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count - 1 Then
                    Return
                End If
                
                ' Join the lines
                Dim lCombinedText As String = TextLines(vLineIndex) & TextLines(vLineIndex + 1)
                
                ' Update the first line
                UpdateTextLine(vLineIndex, lCombinedText)
                
                ' Delete the second line
                DeleteLine(vLineIndex + 1)
                
            Catch ex As Exception
                Console.WriteLine($"JoinLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Splits a line at the specified position
        ''' </summary>
        ''' <param name="vLineIndex">Index of the line to split</param>
        ''' <param name="vColumn">Column position where to split</param>
        Public Sub SplitLine(vLineIndex As Integer, vColumn As Integer)
            Try
                ' Validate index
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then
                    Return
                End If
                
                Dim lLine As String = TextLines(vLineIndex)
                
                ' Calculate split position
                vColumn = Math.Max(0, Math.Min(vColumn, lLine.Length))
                
                ' Split the line
                Dim lFirstPart As String = If(vColumn > 0, lLine.Substring(0, vColumn), "")
                Dim lSecondPart As String = If(vColumn < lLine.Length, lLine.Substring(vColumn), "")
                
                ' Update the first line
                UpdateTextLine(vLineIndex, lFirstPart)
                
                ' Insert the second line
                InsertLine(vLineIndex + 1, lSecondPart)
                
            Catch ex As Exception
                Console.WriteLine($"SplitLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets text in a range
        ''' </summary>
        ''' <param name="vStartLine">Start line (0-based)</param>
        ''' <param name="vStartColumn">Start column (0-based)</param>
        ''' <param name="vEndLine">End line (0-based)</param>
        ''' <param name="vEndColumn">End column (0-based)</param>
        ''' <returns>The text in the specified range</returns>
        Public Function GetTextInRange(vStartLine As Integer, vStartColumn As Integer, 
                                       vEndLine As Integer, vEndColumn As Integer) As String
            Try
                ' Validate parameters
                If vStartLine < 0 OrElse vStartLine >= TextLines.Count Then Return ""
                If vEndLine < 0 OrElse vEndLine >= TextLines.Count Then Return ""
                
                ' Normalize range
                If vStartLine > vEndLine OrElse (vStartLine = vEndLine AndAlso vStartColumn > vEndColumn) Then
                    ' Swap start and end
                    Dim lTempLine As Integer = vStartLine
                    Dim lTempCol As Integer = vStartColumn
                    vStartLine = vEndLine
                    vStartColumn = vEndColumn
                    vEndLine = lTempLine
                    vEndColumn = lTempCol
                End If
                
                ' Build result
                Dim lResult As New System.Text.StringBuilder()
                
                If vStartLine = vEndLine Then
                    ' Single line range
                    Dim lLine As String = TextLines(vStartLine)
                    vStartColumn = Math.Max(0, Math.Min(vStartColumn, lLine.Length))
                    vEndColumn = Math.Max(vStartColumn, Math.Min(vEndColumn, lLine.Length))
                    lResult.Append(lLine.Substring(vStartColumn, vEndColumn - vStartColumn))
                Else
                    ' Multi-line range
                    ' First line
                    Dim lFirstLine As String = TextLines(vStartLine)
                    vStartColumn = Math.Max(0, Math.Min(vStartColumn, lFirstLine.Length))
                    lResult.Append(lFirstLine.Substring(vStartColumn))
                    lResult.Append(Environment.NewLine)
                    
                    ' Middle lines
                    for i As Integer = vStartLine + 1 To vEndLine - 1
                        lResult.Append(TextLines(i))
                        lResult.Append(Environment.NewLine)
                    Next
                    
                    ' Last line
                    Dim lLastLine As String = TextLines(vEndLine)
                    vEndColumn = Math.Max(0, Math.Min(vEndColumn, lLastLine.Length))
                    lResult.Append(lLastLine.Substring(0, vEndColumn))
                End If
                
                Return lResult.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"GetTextInRange error: {ex.Message}")
                Return ""
            End Try
        End Function
        
    End Class
    
End Namespace
