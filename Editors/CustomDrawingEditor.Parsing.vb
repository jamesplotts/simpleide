' Editors/CustomDrawingEditor.Parsing.vb - Fixed parsing implementation with character-based coloring
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== Parsing and Syntax Highlighting =====
        
        
       
        ' ===== Parsing Methods =====
        
        ''' <summary>
        ''' Raises the DocumentParsed event when parsing is complete
        ''' </summary>
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
        ''' Force a refresh of syntax highlighting
        ''' </summary>
        ''' <remarks>
        ''' Triggers reparsing through centralized ProjectManager.Parser
        ''' </remarks>
        Public Sub RefreshSyntaxHighlighting() Implements IEditor.RefreshSyntaxHighlighting
            Try
                ' Clear existing character colors
                If pCharacterColors IsNot Nothing Then
                    ReDim Preserve pCharacterColors(pLineCount - 1)
                Else
                    ReDim pCharacterColors(pLineCount - 1)
                End If
                
                ' Mark all lines as changed to force re-highlighting
                If pLineMetadata IsNot Nothing Then
                    for i As Integer = 0 To pLineCount - 1
                        pLineMetadata(i).IsChanged = True
                    Next
                End If
                
                ' Request parse through centralized system
                RequestParseViaEvent()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
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
                    for i As Integer = 0 To pLineMetadata.Length - 1
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
                
                ' FIXED: Ensure metadata is not null
                If lMetadata Is Nothing Then
                    lMetadata = New LineMetadata()
                    pLineMetadata(vLineIndex) = lMetadata
                End If
                
                ' Clear existing tokens
                lMetadata.SyntaxTokens.Clear()
        
                ' FIXED: Initialize character colors array for this line with proper null checking
                If pCharacterColors(vLineIndex) Is Nothing OrElse 
                   pCharacterColors(vLineIndex).Length <> lLineText.Length Then
                    ReDim pCharacterColors(vLineIndex)(Math.Max(0, lLineText.Length - 1))
                End If
                
                ' Set default color for all characters
                for i As Integer = 0 To lLineText.Length - 1
                    If pCharacterColors(vLineIndex)(i) Is Nothing Then
                        pCharacterColors(vLineIndex)(i) = New CharacterColorInfo(If(pForegroundColor, "#000000"))
                    Else
                        ' Update existing color info
                        pCharacterColors(vLineIndex)(i).Color = If(pForegroundColor, "#000000")
                    End If
                Next
        
                ' Apply VB.NET syntax highlighting
                ApplyVBNetSyntaxHighlightingCharacterBased(vLineIndex, lLineText, lMetadata)
        
            Catch ex As Exception
                Console.WriteLine($"ApplySyntaxHighlightingToLine error: {ex.Message}")
                Console.WriteLine($"  LineIndex={vLineIndex}, pTextLines.Count={pTextLines.Count}, pLineMetadata.Length={If(pLineMetadata IsNot Nothing, pLineMetadata.Length.ToString(), "null")}")
            End Try
        End Sub

        Dim lCommentColor As String = "#008000"    ' Default green for comments
        Dim lStringColor As String = "#A31515"     ' Default red for strings
        Dim lNumberColor As String = "#098658"     ' Default teal for numbers
        Dim lKeywordColor As String = "#0000FF"    ' Default blue for keywords
        Dim lTypeColor As String = "#2B91AF"       ' Default blue-green for types
        Dim lOperatorColor As String = "#000000"   ' Default black for operators
        Dim lIdentifierColor As String = "#000000" ' Default black for identifiers


        
        ''' <summary>
        ''' Apply VB.NET syntax highlighting to a line with proper null checks
        ''' </summary>
        ''' <param name="vLineIndex">The line index to highlight</param>
        ''' <param name="vLineText">The text of the line</param>
        ''' <param name="vMetadata">The line metadata to update</param>
        Private Sub ApplyVBNetSyntaxHighlightingCharacterBased(vLineIndex As Integer, vLineText As String, vMetadata As LineMetadata)
            Try
                If String.IsNullOrEmpty(vLineText) Then Return
                
                ' CRITICAL FIX: Use pSyntaxColorSet colors which are updated by SetThemeColors
                ' This ensures the preview editor uses the colors from the theme being edited
                ' rather than the globally active theme
                
                
                ' Check if we're in demo mode (preview editor)
                Dim lIsDemoMode As Boolean = False
                If pSourceFileInfo IsNot Nothing Then
                    lIsDemoMode = pSourceFileInfo.IsDemoMode
                End If
                
                ' If we have a SyntaxColorSet, use its colors (these are updated by SetThemeColors)
                If pSyntaxColorSet IsNot Nothing Then
                    ' Get colors from the SyntaxColorSet which contains the preview theme colors
                    lCommentColor = pSyntaxColorSet.GetColor(SyntaxColorSet.Tags.eComment)
                    lStringColor = pSyntaxColorSet.GetColor(SyntaxColorSet.Tags.eString)
                    lNumberColor = pSyntaxColorSet.GetColor(SyntaxColorSet.Tags.eNumber)
                    lKeywordColor = pSyntaxColorSet.GetColor(SyntaxColorSet.Tags.eKeyword)
                    lTypeColor = pSyntaxColorSet.GetColor(SyntaxColorSet.Tags.eType)
                    lOperatorColor = pSyntaxColorSet.GetColor(SyntaxColorSet.Tags.eOperator)
                    lIdentifierColor = pSyntaxColorSet.GetColor(SyntaxColorSet.Tags.eIdentifier)
                    
                    'Console.WriteLine($"Using SyntaxColorSet colors - Keyword: {lKeywordColor}, String: {lStringColor}, Comment: {lCommentColor}")
                    
                ' Only use ThemeManager colors if NOT in demo mode and no SyntaxColorSet
                ElseIf Not lIsDemoMode AndAlso pThemeManager IsNot Nothing Then
                    Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                    If lTheme IsNot Nothing Then
                        ' Use theme colors from the global theme
                        lCommentColor = lTheme.StringColor(EditorTheme.Tags.eCommentText)
                        lStringColor = lTheme.StringColor(EditorTheme.Tags.eStringText)
                        lNumberColor = lTheme.StringColor(EditorTheme.Tags.eNumberText)
                        lKeywordColor = lTheme.StringColor(EditorTheme.Tags.eKeywordText)
                        
                        Console.WriteLine($"Using ThemeManager colors - Keyword: {lKeywordColor}")
                    End If
                End If
                
                ' Ensure pCharacterColors array is properly initialized
                If pCharacterColors Is Nothing Then
                    ReDim pCharacterColors(Math.Max(vLineIndex, pLineCount - 1))
                End If
                
                If vLineIndex >= pCharacterColors.Length Then
                    ReDim Preserve pCharacterColors(vLineIndex)
                End If
                
                If pCharacterColors(vLineIndex) Is Nothing OrElse 
                   pCharacterColors(vLineIndex).Length <> vLineText.Length Then
                    ReDim pCharacterColors(vLineIndex)(Math.Max(0, vLineText.Length - 1))
                End If
                
                ' Initialize all characters with default foreground color first
                for i2 As Integer = 0 To vLineText.Length - 1
                    If pCharacterColors(vLineIndex)(i2) Is Nothing Then
                        pCharacterColors(vLineIndex)(i2) = New CharacterColorInfo(If(pForegroundColor, "#D4D4D4"))
                    Else
                        pCharacterColors(vLineIndex)(i2).Color = If(pForegroundColor, "#D4D4D4")
                    End If
                Next
                
                ' Track if we're in a comment or string
                Dim lInComment As Boolean = False
                Dim lInString As Boolean = False
                Dim lStringStartChar As Char = Nothing
                
                ' Check for comment line
                Dim lTrimmedText As String = vLineText.TrimStart()
                If lTrimmedText.StartsWith("'") OrElse lTrimmedText.ToUpper().StartsWith("REM ") Then
                    ' Entire line is a comment
                    lInComment = True
                    Dim lCommentStart As Integer = vLineText.IndexOf("'")
                    If lCommentStart = -1 Then
                        lCommentStart = vLineText.ToUpper().IndexOf("REM ")
                    End If
                    
                    for i2 As Integer = lCommentStart To vLineText.Length - 1
                        pCharacterColors(vLineIndex)(i2).Color = lCommentColor
                    Next
                    
                    ' Add token to metadata
                    If vMetadata IsNot Nothing Then
                        vMetadata.SyntaxTokens.Add(New SyntaxToken(lCommentStart, vLineText.Length - lCommentStart, 
                                                                  SyntaxTokenType.eComment, lCommentColor))
                    End If
                    Return ' Don't process anything else on comment lines
                End If
                
                ' Process the line character by character for strings and then apply other highlights
                Dim i As Integer = 0
                While i < vLineText.Length
                    Dim lChar As Char = vLineText(i)
                    
                    ' Check for string start/end
                    If lChar = """" AndAlso Not lInComment Then
                        If Not lInString Then
                            lInString = True
                            lStringStartChar = lChar
                            Dim lStringStart As Integer = i
                            
                            ' Find the end of the string
                            i += 1
                            While i < vLineText.Length
                                If vLineText(i) = """" Then
                                    ' Check for escaped quote
                                    If i + 1 < vLineText.Length AndAlso vLineText(i + 1) = """" Then
                                        i += 2 ' Skip escaped quote
                                        Continue While
                                    Else
                                        ' End of string found
                                        lInString = False
                                        i += 1
                                        Exit While
                                    End If
                                End If
                                i += 1
                            End While
                            
                            ' Color the entire string
                            for j As Integer = lStringStart To Math.Min(i - 1, vLineText.Length - 1)
                                pCharacterColors(vLineIndex)(j).Color = lStringColor
                            Next
                            
                            ' Add token to metadata
                            If vMetadata IsNot Nothing Then
                                vMetadata.SyntaxTokens.Add(New SyntaxToken(lStringStart, i - lStringStart, 
                                                                          SyntaxTokenType.eString, lStringColor))
                            End If
                            Continue While
                        End If
                    End If
                    
                    i += 1
                End While
                
                ' Now highlight keywords, types, numbers, etc. (avoiding strings)
                ' Use regex or word-by-word analysis
                Dim lWords As String() = System.Text.RegularExpressions.Regex.Split(vLineText, "\b")
                Dim lPosition As Integer = 0
                
                for each lWord As String in lWords
                    If Not String.IsNullOrEmpty(lWord) Then
                        ' Check if this position is already colored (string or comment)
                        Dim lAlreadyColored As Boolean = False
                        If lPosition < vLineText.Length Then
                            lAlreadyColored = (pCharacterColors(vLineIndex)(lPosition).Color = lStringColor OrElse
                                              pCharacterColors(vLineIndex)(lPosition).Color = lCommentColor)
                        End If
                        
                        If Not lAlreadyColored Then
                            ' Check for keywords
                            If IsVBKeyword(lWord) Then
                                for j As Integer = lPosition To Math.Min(lPosition + lWord.Length - 1, vLineText.Length - 1)
                                    pCharacterColors(vLineIndex)(j).Color = lKeywordColor
                                Next
                                
                                If vMetadata IsNot Nothing Then
                                    vMetadata.SyntaxTokens.Add(New SyntaxToken(lPosition, lWord.Length, 
                                                                              SyntaxTokenType.eKeyword, lKeywordColor))
                                End If
                                
                            ' Check for types
                            ElseIf IsVBType(lWord) Then
                                for j As Integer = lPosition To Math.Min(lPosition + lWord.Length - 1, vLineText.Length - 1)
                                    pCharacterColors(vLineIndex)(j).Color = lTypeColor
                                Next
                                
                                If vMetadata IsNot Nothing Then
                                    vMetadata.SyntaxTokens.Add(New SyntaxToken(lPosition, lWord.Length, 
                                                                              SyntaxTokenType.eType, lTypeColor))
                                End If
                                
                            ' Check for numbers
                            ElseIf IsNumeric(lWord) Then
                                for j As Integer = lPosition To Math.Min(lPosition + lWord.Length - 1, vLineText.Length - 1)
                                    pCharacterColors(vLineIndex)(j).Color = lNumberColor
                                Next
                                
                                If vMetadata IsNot Nothing Then
                                    vMetadata.SyntaxTokens.Add(New SyntaxToken(lPosition, lWord.Length, 
                                                                              SyntaxTokenType.eNumber, lNumberColor))
                                End If
                                
                            ' Check for operators (single character)
                            ElseIf lWord.Length = 1 AndAlso "+-*/=<>&|^".Contains(lWord) Then
                                pCharacterColors(vLineIndex)(lPosition).Color = lOperatorColor
                                
                                If vMetadata IsNot Nothing Then
                                    vMetadata.SyntaxTokens.Add(New SyntaxToken(lPosition, 1, 
                                                                              SyntaxTokenType.eOperator, lOperatorColor))
                                End If
                            End If
                        End If
                    End If
                    
                    lPosition += lWord.Length
                 Next
                
            Catch ex As Exception
                Console.WriteLine($"ApplyVBNetSyntaxHighlightingCharacterBased error: {ex.Message}")
                Console.WriteLine($"  Stack trace: {ex.StackTrace}")
            End Try
        End Sub
        
        ' Helper function to check if a word is a VB keyword
        Private Function IsVBKeyword(vWord As String) As Boolean
            Static lKeywords As HashSet(Of String) = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "AddHandler", "AddressOf", "Alias", "And", "AndAlso", "As", "Boolean", "ByRef", "Byte", "ByVal",
                "Call", "Case", "Catch", "CBool", "CByte", "CChar", "CDate", "CDbl", "CDec", "Char", "CInt", "Class",
                "CLng", "CObj", "Const", "Continue", "CSByte", "CShort", "CSng", "CStr", "CType", "CUInt", "CULng",
                "CUShort", "Date", "Decimal", "Declare", "Default", "Delegate", "Dim", "DirectCast", "Do", "Double",
                "Each", "Else", "ElseIf", "End", "EndIf", "Enum", "Erase", "Error", "Event", "Exit", "False", "Finally",
                "For", "Friend", "Function", "Get", "GetType", "GetXMLNamespace", "Global", "GoSub", "GoTo", "Handles",
                "If", "Implements", "Imports", "In", "Inherits", "Integer", "Interface", "Is", "IsNot", "Let", "Lib",
                "Like", "Long", "Loop", "Me", "Mod", "Module", "MustInherit", "MustOverride", "MyBase", "MyClass",
                "Namespace", "Narrowing", "New", "Next", "Not", "Nothing", "NotInheritable", "NotOverridable", "Object",
                "Of", "On", "Operator", "Option", "Optional", "Or", "OrElse", "Overloads", "Overridable", "Overrides",
                "ParamArray", "Partial", "Private", "Property", "Protected", "Public", "RaiseEvent", "ReadOnly", "ReDim",
                "REM", "RemoveHandler", "Resume", "Return", "SByte", "Select", "Set", "Shadows", "Shared", "Short",
                "Single", "Static", "Step", "Stop", "String", "Structure", "Sub", "SyncLock", "Then", "Throw", "To",
                "True", "Try", "TryCast", "TypeOf", "UInteger", "ULong", "UShort", "Using", "Variant", "Wend", "When",
                "While", "Widening", "With", "WithEvents", "WriteOnly", "Xor"
            }
            
            Return lKeywords.Contains(vWord)
        End Function

        ' Helper function to check if a word is a VB type
        Private Function IsVBType(vWord As String) As Boolean
            Static lTypes As HashSet(Of String) = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "Boolean", "Byte", "Char", "Date", "Decimal", "Double", "Integer", "Long", "Object",
                "SByte", "Short", "Single", "String", "UInteger", "ULong", "UShort",
                "List", "Dictionary", "HashSet", "Queue", "Stack", "ArrayList",
                "StringBuilder", "DateTime", "TimeSpan", "Guid", "Uri", "Regex"
            }
            
            Return lTypes.Contains(vWord)
        End Function
        
'         Private Sub ScheduleParsing()
'             Try
'                 ' CRITICAL FIX: Cancel any existing parse timer with proper cleanup
'                 If pParseTimer <> 0 Then
'                     Dim lTimerId As UInteger = pParseTimer
'                     pParseTimer = 0  ' Clear BEFORE removing
'                     Try
'                         GLib.Source.Remove(lTimerId)
'                     Catch
'                         ' Timer may have already expired - this is OK
'                     End Try
'                 End If
'                 
'                 ' Schedule new parse in 500ms
'                 pNeedsParse = True
'                 pParseTimer = GLib.Timeout.Add(500, AddressOf PerformParsing)
'                 
'             Catch ex As Exception
'                 Console.WriteLine($"ScheduleParsing error: {ex.Message}")
'                 pParseTimer = 0  ' Ensure it's cleared on error
'             End Try
'         End Sub
'         
'         Private Function PerformParsing() As Boolean
'             Try
'                 ' CRITICAL FIX: Clear timer ID immediately since we're returning False
'                 pParseTimer = 0
'                 
'                 If Not pNeedsParse Then Return False
'                 
'                 ' Parse the document - create parser as needed
'                 Dim lContent As String = GetAllText()
'                 
'                 ' Create a new VBParser instance instead of using stored VBCodeParser
'                 Dim lParser As New VBParser()
'                 
'                 ' Use the Parse method with proper parameters
'                 Dim lParseResult As VBParser.ParseResult = lParser.Parse(lContent, "SimpleIDE", pFilePath)
'                 
'                 ' Extract the root SyntaxNode from the ParseResult
'                 If lParseResult IsNot Nothing AndAlso lParseResult.RootNode IsNot Nothing Then
'                     pRootNode = lParseResult.RootNode
'                     
'                     ' Also update document nodes if they exist in the result
'                     If lParseResult.DocumentNodes IsNot Nothing Then
'                         pDocumentNodes = lParseResult.DocumentNodes
'                     End If
'                     
'                     If lParseResult.RootNodes IsNot Nothing Then
'                         pRootNodes = lParseResult.RootNodes
'                     End If
'                 End If
'                 
'                 ' Clear parse flag
'                 pNeedsParse = False
'                 
'                 ' Update display
'                 UpdateLineNumberWidth()
'                 pDrawingArea?.QueueDraw()
'                 
'                 ' Raise document parsed event
'                 If pRootNode IsNot Nothing Then
'                     RaiseEvent DocumentParsed(pRootNode)
'                 End If
'                 
'                 Return False  ' Don't repeat - timer is auto-removed
'                 
'             Catch ex As Exception
'                 Console.WriteLine($"PerformParsing error: {ex.Message}")
'                 ' Timer is already cleared at the top
'                 Return False
'             End Try
'         End Function
        
        ' Helper method to create a SyntaxNode from ParseResult
        Private Function CreateSyntaxNodeFromParseResult(vParseResult As ParseResult) As SyntaxNode
            Try
                If vParseResult Is Nothing Then Return Nothing
                
                ' Create a root document node
                Dim lRootNode As New SyntaxNode(CodeNodeType.eDocument, "document")
                lRootNode.StartLine = 0
                lRootNode.EndLine = pLineCount - 1
                
                ' Convert DocumentNodes to SyntaxNodes
                for each lDocNode in vParseResult.DocumentNodes.Values
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
                for each lAttr in vDocNode.Attributes
                    If Not lSyntaxNode.Attributes.ContainsKey(lAttr.key) Then
                        lSyntaxNode.Attributes(lAttr.key) = lAttr.Value.ToString()
                    End If
                Next
                
                ' Recursively convert children
                for each lChildNode in vDocNode.Children
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
                for i As Integer = 0 To pLineCount - 1
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

        ''' <summary>
        ''' Handle parse completion from ProjectManager
        ''' </summary>
        ''' <param name="vFile">The parsed SourceFileInfo</param>
        ''' <param name="vResult">Parse result from ProjectParser</param>
        Private Sub OnProjectParseCompleted(vFile As SourceFileInfo, vResult As Object)
            Try
                ' Check if this parse is for our file
                If vFile Is Nothing OrElse vFile IsNot pSourceFileInfo Then
                    Return
                End If
                
                Console.WriteLine($"CustomDrawingEditor received parse results for {vFile.FileName}")
                
                ' Update our syntax tree from the parsed result
                If vFile.SyntaxTree IsNot Nothing Then
                    pRootNode = vFile.SyntaxTree
                    
                    ' Update line metadata with parse information
                    UpdateLineMetadataFromParseResult()
                    
                    ' Update identifier case map from parsed nodes
                    UpdateIdentifierCaseMap()
                    
                    ' Apply syntax highlighting based on parse results
                    for i As Integer = 0 To Math.Min(pLineCount - 1, pLineMetadata.Length - 1)
                        If pLineMetadata(i).IsChanged Then
                            ApplySyntaxHighlightingToLine(i)
                            pLineMetadata(i).IsChanged = False
                        End If
                    Next
                    
                    ' Update line number width if needed
                    UpdateLineNumberWidth()
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                    
                    ' Raise document parsed event
                    RaiseDocumentParsedEvent()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectParseCompleted error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Subscribe to ProjectManager parse events
        ''' </summary>
        Private Sub SubscribeToProjectParser()
            Try
                ' Get MainWindow instance to access ProjectManager
                Dim lMainWindow As MainWindow = TryCast(Me.Toplevel, MainWindow)
                If lMainWindow Is Nothing Then
                    Console.WriteLine("SubscribeToProjectParser: MainWindow not found")
                    Return
                End If
                
                Dim lProjectManager As ProjectManager = ProjectManager
                If lProjectManager Is Nothing Then
                    Console.WriteLine("SubscribeToProjectParser: ProjectManager not available")
                    Return
                End If
                
                ' Subscribe to parse completed event
                RemoveHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                AddHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                
                Console.WriteLine("CustomDrawingEditor subscribed to ProjectManager.ParseCompleted")
                
            Catch ex As Exception
                Console.WriteLine($"SubscribeToProjectParser error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Recursively update line metadata from syntax nodes
        ''' </summary>
        Private Sub UpdateLineMetadataFromNode(vNode As SyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Add node reference to relevant lines
                If vNode.StartLine >= 0 AndAlso vNode.StartLine < pLineCount Then
                    For i As Integer = vNode.StartLine To Math.Min(vNode.EndLine, pLineCount - 1)
                        If i >= 0 AndAlso i < pLineMetadata.Length Then
                            ' Create a NodeReference from the SyntaxNode
                            Dim lNodeRef As New NodeReference(
                                vNode.GetFullyQualifiedName(),  ' NodeId
                                vNode.NodeType,                  ' NodeType
                                vNode.StartColumn,               ' StartColumn
                                vNode.EndColumn,                 ' EndColumn
                                i = vNode.StartLine              ' IsDefinition (true for first line)
                            )
                            pLineMetadata(i).NodeReferences.Add(lNodeRef)
                        End If
                    Next
                End If
                
                ' Process children
                If vNode.Children IsNot Nothing Then
                    For Each lChild In vNode.Children
                        UpdateLineMetadataFromNode(lChild)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineMetadataFromNode error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
