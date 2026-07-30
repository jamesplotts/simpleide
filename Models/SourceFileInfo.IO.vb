' Models/SourceFileInfo.IO.vb - File loading and saving operations
' Created: 2025-01-10
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text

Namespace Models
    
    Partial Public Class SourceFileInfo
        
        ' ===== File I/O Operations =====
        
        ''' <summary>
        ''' Loads the file content from disk and initializes data structures
        ''' </summary>
        ''' <returns>True if successful, False otherwise</returns>
        ''' <remarks>
        ''' This method loads the file content and requests async parsing from ProjectManager
        ''' </remarks>
        Public Function LoadContent() As Boolean
            Try
                ' Check if file exists
                If Not System.IO.File.Exists(pFilePath) Then
                    ' Initialize with empty content for new files
                    pTextLines.Clear()
                    pTextLines.Add("")
                    ReDim pLineMetadata(0)
                    pLineMetadata(0) = New LineMetadata()
                    pLineMetadata(0).ParseState = LineParseState.eUnparsed
                    ReDim pCharacterTokens(0)
                    pCharacterTokens(0) = New Byte() {}
                    pIsModified = True  ' New files are considered modified
                    Return True
                End If
                
                ' Read file content with encoding detection
                Dim lBytes As Byte() = System.IO.File.ReadAllBytes(pFilePath)
                pEncoding = DetectEncoding(lBytes)
                
                ' Convert to string using detected encoding
                Dim lContent As String = pEncoding.GetString(lBytes)
                
                ' Split into lines preserving empty lines
                Dim lLines As String() = lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                
                ' Clear and repopulate TextLines
                pTextLines.Clear()
                pTextLines.AddRange(lLines)
                
                ' Ensure at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
                InitializeLineMetadataArray()
                
                InitializeCharacterTokenArrays()
                
                ' Update state
                pIsModified = False
                pNeedsParsing = True
                pLastModified = System.IO.File.GetLastWriteTime(pFilePath)
                
                ' Request async parse
                RequestAsyncParse()
                
                Console.WriteLine($"Loaded {FileName}: {pTextLines.Count} lines")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"LoadContent error: {ex.Message}")
                Return False
            End Try
        End Function

        
        ''' <summary>
        ''' Saves content to disk
        ''' </summary>
        ''' <returns>True if successful, False otherwise</returns>
        Public Function SaveContent() As Boolean
            Try
                If String.IsNullOrEmpty(pFilePath) Then
                    Console.WriteLine("Cannot save: no file path")
                    Return False
                End If
                
                ' Ensure directory exists
                Dim lDirectory As String = System.IO.Path.GetDirectoryName(pFilePath)
                If Not String.IsNullOrEmpty(lDirectory) AndAlso Not System.IO.Directory.Exists(lDirectory) Then
                    System.IO.Directory.CreateDirectory(lDirectory)
                End If
                
                ' Write to file
                System.IO.File.WriteAllText(pFilePath, Content, pEncoding)
                
                ' Update state
                pIsModified = False
                pLastModified = DateTime.Now
                
                Console.WriteLine($"Saved {Content.Length} characters to {FileName}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SaveContent error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Saves content to the file
        ''' </summary>
        Public Function SaveToFile() As Boolean
            Return SaveContent()
        End Function
        
        ''' <summary>
        ''' Sets all text content at once
        ''' </summary>
        ''' <param name="vText">The complete text to set</param>
        Public Sub SetText(vText As String)
            Try
                ' Split into lines
                If String.IsNullOrEmpty(vText) Then
                    pTextLines.Clear()
                    pTextLines.Add("")
                Else
                    Dim lNormalized As String = vText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
                    Dim lLines() As String = lNormalized.Split({vbLf}, StringSplitOptions.None)
                    pTextLines.Clear()
                    pTextLines.AddRange(lLines)
                End If
                
                ' Ensure at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
                ' Initialize arrays
                ReDim pLineMetadata(pTextLines.Count - 1)
                ReDim pCharacterTokens(pTextLines.Count - 1)
                
                ' Initialize metadata and tokens for each line
                for i As Integer = 0 To pTextLines.Count - 1
                    pLineMetadata(i) = New LineMetadata()
                    pLineMetadata(i).UpdateHash(pTextLines(i))
                    pLineMetadata(i).ParseState = LineParseState.eUnparsed
                    
                    ' Initialize with default tokens
                    Dim lLineLength As Integer = pTextLines(i).Length
                    If lLineLength > 0 Then
                        pCharacterTokens(i) = pLineMetadata(i).GetEncodedTokens(lLineLength)
                    Else
                        pCharacterTokens(i) = New Byte() {}
                    End If
                Next
                
                ' Update state
                pIsModified = True
                pNeedsParsing = True
                
                ' Raise event
                RaiseTextChangedEvent(TextChangeType.eCompleteReplace, 0, pTextLines.Count - 1, pTextLines.Count)
                
            Catch ex As Exception
                Console.WriteLine($"SetText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Detects the text encoding of a byte array
        ''' </summary>
        ''' <param name="vBytes">The byte array to analyze</param>
        ''' <returns>The detected encoding (defaults to UTF8)</returns>
        Private Function DetectEncoding(vBytes As Byte()) As Encoding
            Try
                If vBytes Is Nothing OrElse vBytes.Length = 0 Then
                    Return Encoding.UTF8
                End If
                
                ' Check for BOM markers
                If vBytes.Length >= 3 AndAlso vBytes(0) = &HEF AndAlso vBytes(1) = &HBB AndAlso vBytes(2) = &HBF Then
                    Return Encoding.UTF8
                ElseIf vBytes.Length >= 2 AndAlso vBytes(0) = &HFF AndAlso vBytes(1) = &HFE Then
                    Return Encoding.Unicode  ' UTF-16 LE
                ElseIf vBytes.Length >= 2 AndAlso vBytes(0) = &HFE AndAlso vBytes(1) = &HFF Then
                    Return Encoding.BigEndianUnicode  ' UTF-16 BE
                ElseIf vBytes.Length >= 4 AndAlso vBytes(0) = &HFF AndAlso vBytes(1) = &HFE AndAlso 
                       vBytes(2) = 0 AndAlso vBytes(3) = 0 Then
                    Return Encoding.UTF32
                End If
                
                ' Default to UTF8 without BOM
                Return New UTF8Encoding(False)
                
            Catch ex As Exception
                Console.WriteLine($"DetectEncoding error: {ex.Message}")
                Return Encoding.UTF8
            End Try
        End Function
        
    End Class
    
End Namespace