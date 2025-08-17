' Editors/CustomDrawingEditor.Parsing.vb - Fixed parsing implementation with character-based coloring
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== Parsing and Syntax Highlighting =====
        
        
       
        ' ===== Parsing Methods =====
        
        Private Sub RaiseDocumentParsedEvent()
            Try
                ' Only raise if we have a valid structure
                If pRootNode IsNot Nothing Then
                    RaiseEvent DocumentParsed(pRootNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RaiseDocumentParsedEvent error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Request a parse of the current document
        ''' Called by external components (like ObjectExplorer) to trigger parsing
        ''' </summary>
        Public Sub RequestParse() Implements IEditor.RequestParse
            Try
                Console.WriteLine("RequestParse called")
                
                ' Schedule a full document parse
                ScheduleFullDocumentParse()
                
            Catch ex As Exception
                Console.WriteLine($"RequestParse error: {ex.Message}")
            End Try
        End Sub


        ' RefreshSyntaxHighlighting - Force a refresh of syntax highlighting
        Public Sub RefreshSyntaxHighlighting() Implements IEditor.RefreshSyntaxHighlighting
            Try
                ' Clear existing character colors
                If pCharacterColors IsNot Nothing Then
                    ReDim Preserve pCharacterColors(pLineCount - 1)
                Else
                    Dim pCharacterColors(pLineCount)
                End If
                
                ' Mark all lines as changed to force re-highlighting
                If pLineMetadata IsNot Nothing Then
                    For i As Integer = 0 To pLineCount - 1
                        pLineMetadata(i).MarkChanged()
                    Next
                End If
                
                ' Schedule immediate parse
                ScheduleParse()
                
                ' Force immediate redraw
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"RefreshSyntaxHighlighting error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Apply syntax highlighting to a specific line
        ''' </summary>
        ''' <param name="vLineIndex">The line index to highlight (0-based)</param>
        Private Sub ApplySyntaxHighlightingToLine(vLineIndex As Integer)
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= pTextLines.Count Then 
                    Console.WriteLine($"ApplySyntaxHighlightingToLine: Invalid line index {vLineIndex}, pTextLines.Count={pTextLines.Count}")
                    Return
                End If
        
                ' CRITICAL: Ensure metadata arrays are properly sized
                ' This handles cases where lines were added but metadata wasn't updated
                If pLineMetadata Is Nothing OrElse vLineIndex >= pLineMetadata.Length Then
                    Console.WriteLine($"ApplySyntaxHighlightingToLine: Resizing metadata from {If(pLineMetadata IsNot Nothing, pLineMetadata.Length.ToString(), "null")} to {vLineIndex + 1}")
                    ReDim Preserve pLineMetadata(Math.Max(vLineIndex, pTextLines.Count - 1))
                    
                    ' Initialize any new metadata entries
                    For i As Integer = 0 To pLineMetadata.Length - 1
                        If pLineMetadata(i) Is Nothing Then
                            pLineMetadata(i) = New LineMetadata()
                        End If
                    Next
                End If
                
                ' Ensure character colors array is also properly sized
                If pCharacterColors Is Nothing OrElse vLineIndex >= pCharacterColors.Length Then
                    ReDim Preserve pCharacterColors(Math.Max(vLineIndex, pTextLines.Count - 1))
                End If
        
                ' Apply theme if needed
                If pThemeManager IsNot Nothing Then
                    Static bolAlreadyRun As Boolean
                    If Not bolAlreadyRun Then
                        bolAlreadyRun = True
                        ApplyTheme()
                    End If
                End If
        
                ' Get line text and metadata
                Dim lLineText As String = pTextLines(vLineIndex)
                Dim lMetadata As LineMetadata = pLineMetadata(vLineIndex)
                
                ' Clear existing tokens
                lMetadata.SyntaxTokens.Clear()
        
                ' Initialize character colors array for this line
                ReDim pCharacterColors(vLineIndex)(Math.Max(0, lLineText.Length - 1))
                
                ' Set default color for all characters
                For i As Integer = 0 To lLineText.Length - 1
                    pCharacterColors(vLineIndex)(i) = New CharacterColorInfo(pForegroundColor)
                Next
        
                ' Apply VB.NET syntax highlighting
                ApplyVBNetSyntaxHighlightingCharacterBased(vLineIndex, lLineText, lMetadata)
        
            Catch ex As Exception
                Console.WriteLine($"ApplySyntaxHighlightingToLine error: {ex.Message}")
                Console.WriteLine($"  LineIndex={vLineIndex}, pTextLines.Count={pTextLines.Count}, pLineMetadata.Length={If(pLineMetadata IsNot Nothing, pLineMetadata.Length.ToString(), "null")}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Apply VB.NET syntax highlighting to a line with proper null checks
        ''' </summary>
        ''' <param name="vLineIndex">The line index to highlight</param>
        ''' <param name="vLineText">The text of the line</param>
        ''' <param name="vMetadata">The line metadata to update</param>
        Private Sub ApplyVBNetSyntaxHighlightingCharacterBased(vLineIndex As Integer, vLineText As String, vMetadata As LineMetadata)
            Try
                If String.IsNullOrEmpty(vLineText) Then Return
                
                ' Get theme with null check - use default colors if theme manager not available
                Dim lTheme As EditorTheme = Nothing
                Dim lCommentColor As String = "#008000"    ' Default green for comments
                Dim lStringColor As String = "#A31515"     ' Default red for strings
                Dim lNumberColor As String = "#098658"     ' Default teal for numbers
                Dim lKeywordColor As String = "#0000FF"    ' Default blue for keywords
                
                If pThemeManager IsNot Nothing Then
                    lTheme = pThemeManager.GetCurrentThemeObject()
                    If lTheme IsNot Nothing Then
                        ' Use theme colors
                        lCommentColor = lTheme.StringColor(EditorTheme.Tags.eCommentText)
                        lStringColor = lTheme.StringColor(EditorTheme.Tags.eStringText)
                        lNumberColor = lTheme.StringColor(EditorTheme.Tags.eNumberText)
                        lKeywordColor = lTheme.StringColor(EditorTheme.Tags.eKeywordText)
                    End If
                End If
                
                ' Ensure pCharacterColors array is properly initialized
                If pCharacterColors Is Nothing Then
                    ReDim pCharacterColors(Math.Max(vLineIndex, pLineCount - 1))
                End If
                
                If vLineIndex >= pCharacterColors.Length Then
                    ReDim Preserve pCharacterColors(vLineIndex)
                End If
                
                ' Initialize the character colors for this line if needed
                If pCharacterColors(vLineIndex) Is Nothing OrElse pCharacterColors(vLineIndex).Length <> vLineText.Length Then
                    ReDim pCharacterColors(vLineIndex)(Math.Max(0, vLineText.Length - 1))
                End If
                
                ' Initialize all characters with default foreground color
                For i As Integer = 0 To vLineText.Length - 1
                    pCharacterColors(vLineIndex)(i) = New CharacterColorInfo(If(pForegroundColor, "#000000"))
                Next
                
                ' First, check for comments (highest priority)
                Dim lCommentStart As Integer = -1
                Dim lInString As Boolean = False
                Dim lStringChar As Char = Nothing
                
                ' Find the first non-string comment
                For i As Integer = 0 To vLineText.Length - 1
                    Dim lChar As Char = vLineText(i)
                    
                    If Not lInString Then
                        If lChar = """" Then
                            lInString = True
                            lStringChar = """"
                        ElseIf lChar = "'" Then
                            lCommentStart = i
                            Exit For
                        End If
                    Else
                        If lChar = lStringChar Then
                            ' Check for escaped quote
                            If i + 1 < vLineText.Length AndAlso vLineText(i + 1) = lStringChar Then
                                i += 1 ' Skip the escaped quote
                            Else
                                lInString = False
                            End If
                        End If
                    End If
                Next
                
                ' Apply comment coloring if found
                If lCommentStart >= 0 Then
                    For i As Integer = lCommentStart To vLineText.Length - 1
                        pCharacterColors(vLineIndex)(i) = New CharacterColorInfo(lCommentColor)
                    Next
                    
                    ' Create token for metadata
                    Dim lCommentToken As New SyntaxToken(lCommentStart, vLineText.Length - lCommentStart, SyntaxTokenType.eComment, lCommentColor)
                    vMetadata.SyntaxTokens.Add(lCommentToken)
                End If
        
                ' Now process the rest of the line (up to comment start if any)
                Dim lProcessEnd As Integer = If(lCommentStart >= 0, lCommentStart - 1, vLineText.Length - 1)
                
                ' Process strings
                lInString = False
                Dim lStringStart As Integer = -1
                
                For i As Integer = 0 To lProcessEnd
                    Dim lChar As Char = vLineText(i)
                    
                    If Not lInString Then
                        If lChar = """" Then
                            lInString = True
                            lStringStart = i
                            lStringChar = """"
                        End If
                    Else
                        If lChar = lStringChar Then
                            ' Check for escaped quote
                            If i + 1 <= lProcessEnd AndAlso vLineText(i + 1) = lStringChar Then
                                i += 1 ' Skip the escaped quote
                            Else
                                ' End of string - color it
                                For j As Integer = lStringStart To i
                                    pCharacterColors(vLineIndex)(j) = New CharacterColorInfo(lStringColor)
                                Next
                                
                                ' Create token
                                Dim lStringToken As New SyntaxToken(lStringStart, i - lStringStart + 1, SyntaxTokenType.eString, lStringColor)
                                vMetadata.SyntaxTokens.Add(lStringToken)
                                
                                lInString = False
                            End If
                        End If
                    End If
                Next
                
                ' If string didn't close, color to end
                If lInString Then
                    For j As Integer = lStringStart To lProcessEnd
                        pCharacterColors(vLineIndex)(j) = New CharacterColorInfo(lStringColor)
                    Next
                    
                    Dim lStringToken As New SyntaxToken(lStringStart, lProcessEnd - lStringStart + 1, SyntaxTokenType.eString, lStringColor)
                    vMetadata.SyntaxTokens.Add(lStringToken)
                End If
                
                ' Process keywords and numbers using comprehensive keyword list
                Dim lKeywords As HashSet(Of String) = GetVBKeywords()
                
                ' Also add common types that should be colored as keywords
                Dim lTypes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                    "String", "Integer", "Boolean", "Object", "Long", "Short", "Byte", 
                    "Single", "Double", "Decimal", "Date", "Char", "Nothing",
                    "System", "Console", "Exception", "EventArgs", "List", "Dictionary",
                    "Gtk", "Gdk", "Cairo", "Widget", "Window", "Box", "Button", "Label"
                }
                
                ' Simple word processing - scan through the line character by character
                Dim lCurrentPos As Integer = 0
                While lCurrentPos <= lProcessEnd
                    ' Skip already processed characters (strings/comments)
                    If pCharacterColors(vLineIndex)(lCurrentPos) IsNot Nothing AndAlso 
                       Not String.IsNullOrEmpty(pCharacterColors(vLineIndex)(lCurrentPos).Color) AndAlso
                       pCharacterColors(vLineIndex)(lCurrentPos).Color <> pForegroundColor Then
                        lCurrentPos += 1
                        Continue While
                    End If
                    
                    If Char.IsDigit(vLineText(lCurrentPos)) Then
                        ' Process number
                        Dim lNumberStart As Integer = lCurrentPos
                        
                        While lCurrentPos <= lProcessEnd AndAlso 
                              (Char.IsDigit(vLineText(lCurrentPos)) OrElse vLineText(lCurrentPos) = "."c)
                            lCurrentPos += 1
                        End While
                        
                        ' Color the number
                        For i As Integer = lNumberStart To lCurrentPos - 1
                            pCharacterColors(vLineIndex)(i) = New CharacterColorInfo(lNumberColor)
                        Next
                        
                        ' Create token
                        Dim lNumberToken As New SyntaxToken(lNumberStart, lCurrentPos - lNumberStart, SyntaxTokenType.eNumber, lNumberColor)
                        vMetadata.SyntaxTokens.Add(lNumberToken)
                        
                    ElseIf Char.IsLetter(vLineText(lCurrentPos)) OrElse vLineText(lCurrentPos) = "_" Then
                        ' Process identifier or keyword
                        Dim lWordStart As Integer = lCurrentPos
                        
                        While lCurrentPos <= lProcessEnd AndAlso 
                              (Char.IsLetterOrDigit(vLineText(lCurrentPos)) OrElse vLineText(lCurrentPos) = "_")
                            lCurrentPos += 1
                        End While
                        
                        Dim lWord As String = vLineText.Substring(lWordStart, lCurrentPos - lWordStart)
                        
                        ' Check if it's a keyword or a type
                        If lKeywords.Contains(lWord) Then
                            ' Color as keyword
                            For i As Integer = lWordStart To lCurrentPos - 1
                                pCharacterColors(vLineIndex)(i) = New CharacterColorInfo(lKeywordColor)
                            Next
                            
                            ' Create token
                            Dim lKeywordToken As New SyntaxToken(lWordStart, lWord.Length, SyntaxTokenType.eKeyword, lKeywordColor)
                            vMetadata.SyntaxTokens.Add(lKeywordToken)
                        ElseIf lTypes.Contains(lWord) Then
                            ' Color as type (using keyword color for now, could use different color)
                            For i As Integer = lWordStart To lCurrentPos - 1
                                pCharacterColors(vLineIndex)(i) = New CharacterColorInfo(lKeywordColor)
                            Next
                            
                            ' Create token
                            Dim lTypeToken As New SyntaxToken(lWordStart, lWord.Length, SyntaxTokenType.eType, lKeywordColor)
                            vMetadata.SyntaxTokens.Add(lTypeToken)
                        End If
                        
                    Else
                        ' Other character, skip it
                        lCurrentPos += 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"ApplyVBNetSyntaxHighlightingCharacterBased error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get comprehensive list of VB.NET keywords
        ''' </summary>
        Private Function GetVBKeywords() As HashSet(Of String)
            Static lKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "addhandler", "addressof", "alias", "and", "andalso", "as", "boolean", "byref", "byte", "byval",
                "call", "case", "catch", "cbool", "cbyte", "cchar", "cdate", "cdbl", "cdec", "char", "cint",
                "class", "clng", "cobj", "const", "continue", "csbyte", "cshort", "csng", "cstr", "ctype",
                "cuint", "culng", "cushort", "date", "decimal", "declare", "default", "delegate", "dim",
                "directcast", "do", "double", "each", "else", "elseif", "end", "endif", "enum", "erase",
                "error", "event", "exit", "false", "finally", "for", "friend", "function", "get", "gettype",
                "getxmlnamespace", "global", "gosub", "goto", "handles", "if", "implements", "imports", "in",
                "inherits", "integer", "interface", "is", "isnot", "let", "lib", "like", "long", "loop",
                "me", "mod", "module", "mustinherit", "mustoverride", "mybase", "myclass", "namespace",
                "narrowing", "new", "next", "not", "nothing", "notinheritable", "notoverridable", "object",
                "of", "on", "operator", "option", "optional", "or", "orelse", "out", "overloads", "overridable",
                "overrides", "paramarray", "partial", "private", "property", "protected", "public", "raiseevent",
                "readonly", "redim", "rem", "removehandler", "resume", "return", "sbyte", "select", "set",
                "shadows", "shared", "short", "single", "static", "step", "stop", "string", "structure",
                "sub", "synclock", "then", "throw", "to", "true", "try", "trycast", "typeof", "uinteger",
                "ulong", "ushort", "using", "variant", "wend", "when", "while", "widening", "with", "withevents",
                "writeonly", "xor"
            }
            Return lKeywords
        End Function
        
        Private Sub ScheduleParsing()
            Try
                ' Cancel any existing parse timer
                If pParseTimer <> 0 Then
                    GLib.Source.Remove(pParseTimer)
                    pParseTimer = 0
                End If
                
                ' Schedule new parse in 500ms
                pNeedsParse = True
                pParseTimer = GLib.Timeout.Add(500, AddressOf PerformParsing)
                
            Catch ex As Exception
                Console.WriteLine($"ScheduleParsing error: {ex.Message}")
            End Try
        End Sub
        
        Private Function PerformParsing() As Boolean
            Try
                If Not pNeedsParse Then Return False
                
                ' Parse the document - create parser as needed
                Dim lContent As String = GetAllText()
                
                ' Create a new VBParser instance instead of using stored VBCodeParser
                Dim lParser As New VBParser()
                
                ' Use the Parse method with proper parameters
                Dim lParseResult As VBParser.ParseResult = lParser.Parse(lContent, "SimpleIDE", pFilePath)
                
                ' Extract the root SyntaxNode from the ParseResult
                If lParseResult IsNot Nothing AndAlso lParseResult.RootNode IsNot Nothing Then
                    pRootNode = lParseResult.RootNode
                    
                    ' Also update document nodes if they exist in the result
                    If lParseResult.DocumentNodes IsNot Nothing Then
                        pDocumentNodes = lParseResult.DocumentNodes
                    End If
                    
                    If lParseResult.RootNodes IsNot Nothing Then
                        pRootNodes = lParseResult.RootNodes
                    End If
                End If
                
                ' Update metadata with parse results
                UpdateMetadataFromParse()
                
                pNeedsParse = False
                pParseTimer = 0
                
                Return False ' Don't repeat
                
            Catch ex As Exception
                Console.WriteLine($"PerformParsing error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Helper method to create a SyntaxNode from ParseResult
        Private Function CreateSyntaxNodeFromParseResult(vParseResult As ParseResult) As SyntaxNode
            Try
                If vParseResult Is Nothing Then Return Nothing
                
                ' Create a root document node
                Dim lRootNode As New SyntaxNode(CodeNodeType.eDocument, "document")
                lRootNode.StartLine = 0
                lRootNode.EndLine = pLineCount - 1
                
                ' Convert DocumentNodes to SyntaxNodes
                For Each lDocNode In vParseResult.DocumentNodes.Values
                    If lDocNode.Parent Is Nothing Then ' Root Level Nodes only
                        Dim lSyntaxNode As SyntaxNode = ConvertDocumentNodeToSyntaxNode(lDocNode)
                        If lSyntaxNode IsNot Nothing Then
                            lRootNode.AddChild(lSyntaxNode)
                        End If
                    End If
                Next
                
                Return lRootNode
                
            Catch ex As Exception
                Console.WriteLine($"CreateSyntaxNodeFromParseResult error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Helper method to convert DocumentNode to SyntaxNode
        Private Function ConvertDocumentNodeToSyntaxNode(vDocNode As DocumentNode) As SyntaxNode
            Try
                If vDocNode Is Nothing Then Return Nothing
                
                Dim lSyntaxNode As New SyntaxNode(vDocNode.NodeType, vDocNode.Name)
                lSyntaxNode.StartLine = vDocNode.StartLine
                lSyntaxNode.EndLine = vDocNode.EndLine
                lSyntaxNode.StartColumn = vDocNode.StartColumn
                lSyntaxNode.EndColumn = vDocNode.EndColumn
                
                ' Copy visibility properties using the actual boolean properties
                If vDocNode.Attributes.ContainsKey("Visibility") Then
                    Dim lVisibility As String = vDocNode.Attributes("Visibility").ToString().ToLower()
                    Select Case lVisibility
                        Case "public"
                            lSyntaxNode.IsPublic = True
                            lSyntaxNode.IsPrivate = False
                            lSyntaxNode.IsProtected = False
                            lSyntaxNode.IsFriend = False
                        Case "private"
                            lSyntaxNode.IsPublic = False
                            lSyntaxNode.IsPrivate = True
                            lSyntaxNode.IsProtected = False
                            lSyntaxNode.IsFriend = False
                        Case "protected"
                            lSyntaxNode.IsPublic = False
                            lSyntaxNode.IsPrivate = False
                            lSyntaxNode.IsProtected = True
                            lSyntaxNode.IsFriend = False
                        Case "friend"
                            lSyntaxNode.IsPublic = False
                            lSyntaxNode.IsPrivate = False
                            lSyntaxNode.IsProtected = False
                            lSyntaxNode.IsFriend = True
                    End Select
                End If
                
                ' Copy other attributes
                If vDocNode.Attributes.ContainsKey("ReturnType") Then
                    lSyntaxNode.ReturnType = vDocNode.Attributes("ReturnType").ToString()
                End If
                
                If vDocNode.Attributes.ContainsKey("BaseType") Then
                    lSyntaxNode.BaseType = vDocNode.Attributes("BaseType").ToString()
                End If
                
                If vDocNode.Attributes.ContainsKey("IsPartial") Then
                    Boolean.TryParse(vDocNode.Attributes("IsPartial").ToString(), lSyntaxNode.IsPartial)
                End If
                
                If vDocNode.Attributes.ContainsKey("IsShared") Then
                    Boolean.TryParse(vDocNode.Attributes("IsShared").ToString(), lSyntaxNode.IsShared)
                End If
                
                ' Copy all other attributes to the SyntaxNode's Attributes dictionary
                For Each lAttr In vDocNode.Attributes
                    If Not lSyntaxNode.Attributes.ContainsKey(lAttr.key) Then
                        lSyntaxNode.Attributes(lAttr.key) = lAttr.Value.ToString()
                    End If
                Next
                
                ' Recursively convert children
                For Each lChildNode In vDocNode.Children
                    Dim lChildSyntaxNode As SyntaxNode = ConvertDocumentNodeToSyntaxNode(lChildNode)
                    If lChildSyntaxNode IsNot Nothing Then
                        lSyntaxNode.AddChild(lChildSyntaxNode)
                    End If
                Next
                
                Return lSyntaxNode
                
            Catch ex As Exception
                Console.WriteLine($"ConvertDocumentNodeToSyntaxNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        Private Sub UpdateMetadataFromParse()
            Try
                If pRootNode Is Nothing Then Return
                
                ' Update line metadata with node information
                ' This is where we'd enhance syntax highlighting with semantic information
                ' Mark lines as not changed since they've been parsed
                For i As Integer = 0 To pLineCount - 1
                    ' Instead of HasParseData, we can use the fact that NodeReferences exist
                    ' or simply mark the line as not changed after parsing
                    pLineMetadata(i).IsChanged = False
                    
                    ' Alternatively, if you want to check if parse data exists:
                    ' If pLineMetadata(i).NodeReferences.Count > 0 Then
                    '     ' Line has parse data
                    ' End If
                Next

                ' Raise DocumentParsed event after metadata update
                RaiseDocumentParsedEvent()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateMetadataFromParse error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
