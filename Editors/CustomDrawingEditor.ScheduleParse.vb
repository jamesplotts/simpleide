' Editors/CustomDrawingEditor.ScheduleParse.vb - Line-based parsing and formatting
Imports Gtk
Imports System
Imports System.Text
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' Track the line currently being edited
        Private pEditingLine As Integer = -1
        Private pLastEditedLine As Integer = -1
        
        ' Case correction dictionary for known identifiers
        Private pIdentifierCaseMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        
        ' ===== ScheduleParse Implementation =====
        
        ''' <summary>
        ''' Schedule parsing for the current or last edited line
        ''' </summary>
        Private Sub ScheduleParse()
            Try
                ' CRITICAL FIX: Validate state before processing
                If Not ValidateEditorState() Then
                    Console.WriteLine("ScheduleParse: Editor state invalid, skipping parse")
                    Return
                End If
                
                ' If we have a line that was being edited, process it
                If pLastEditedLine >= 0 AndAlso pLastEditedLine < pLineCount Then
                    ProcessLineFormatting(pLastEditedLine)
                Else
                    ' Process all lines
                    For i As Integer = 0 To pLineCount - 1
                        ProcessLineFormatting(i)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ScheduleParse error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Schedule a full document parse with proper timer management
        ''' </summary>
        Public Sub ScheduleFullDocumentParse()
            Try
                ' CRITICAL FIX: Only try to remove timer if it's valid
                ' Set to 0 immediately after removal to prevent double-removal
                If pParseTimer <> 0 Then
                    Dim lTimerId As UInteger = pParseTimer
                    pParseTimer = 0  ' Clear BEFORE removing to prevent race conditions
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch ex As Exception
                        ' Timer may have already been removed - this is OK
                        Console.WriteLine($"Timer {lTimerId} already removed (this is normal)")
                    End Try
                End If
                
                ' Schedule parse after a short delay
                pParseTimer = GLib.Timeout.Add(500, AddressOf PerformFullDocumentParsing)
                
            Catch ex As Exception
                Console.WriteLine($"ScheduleFullDocumentParse error: {ex.Message}")
                pParseTimer = 0  ' Ensure it's cleared on error
            End Try
        End Sub

        ''' <summary>
        ''' Perform full document parsing
        ''' </summary>
        ''' <returns>False to stop the timer</returns>
        Private Function PerformFullDocumentParsing() As Boolean
            Try
                ' CRITICAL FIX: Clear timer ID immediately since we're returning False
                ' This prevents any other code from trying to remove it
                pParseTimer = 0
                
                ' Get all text
                Dim lCode As String = GetAllText()
                
                ' Create a new VBParser instance
                Dim lParser As New VBParser()
                
                ' Use the Parse method with proper parameters
                Dim lParseResult As VBParser.ParseResult = lParser.Parse(lCode, "SimpleIDE", pFilePath)
                
                ' Update document nodes
                If lParseResult IsNot Nothing Then
                    If lParseResult.DocumentNodes IsNot Nothing Then
                        pDocumentNodes = lParseResult.DocumentNodes
                    End If
                    
                    If lParseResult.RootNodes IsNot Nothing Then
                        pRootNodes = lParseResult.RootNodes
                    End If
                    
                    If lParseResult.RootNode IsNot Nothing Then
                        pRootNode = lParseResult.RootNode
                    End If
                End If
                
                ' Update line metadata
                UpdateLineMetadataFromParseResult(lParseResult)
                
                ' Update identifier case map
                UpdateIdentifierCaseMap()
                
                Return False ' Don't repeat - timer is automatically removed
                
            Catch ex As Exception
                Console.WriteLine($"PerformFullDocumentParsing error: {ex.Message}")
                ' Timer is already cleared at the top
                Return False
            End Try
        End Function
        
        ' ===== Helper to get all text =====
        Public Function GetAllText() As String
            Try
                If pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.TextLines IsNot Nothing Then
                    Return String.Join(Environment.NewLine, pSourceFileInfo.TextLines)
                End If
                Return ""
            Catch ex As Exception
                Console.WriteLine($"GetAllText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Process line formatting with proper null checks
        ''' </summary>
        ''' <param name="vLineIndex">The line index to format</param>
        Private Sub ProcessLineFormatting(vLineIndex As Integer)
            Try
                ' CRITICAL FIX: Add null checks for pSourceFileInfo and TextLines
                If pSourceFileInfo Is Nothing Then
                    Console.WriteLine("ProcessLineFormatting: pSourceFileInfo is Nothing")
                    Return
                End If
                
                If pSourceFileInfo.TextLines Is Nothing Then
                    Console.WriteLine("ProcessLineFormatting: pSourceFileInfo.TextLines is Nothing")
                    Return
                End If
                
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= pLineCount Then
                    Console.WriteLine($"ProcessLineFormatting: Invalid line index {vLineIndex}, pLineCount={pLineCount}")
                    Return
                End If
                
                ' Additional safety check against TextLines count
                If vLineIndex >= pSourceFileInfo.TextLines.Count Then
                    Console.WriteLine($"ProcessLineFormatting: Line index {vLineIndex} >= TextLines.Count {pSourceFileInfo.TextLines.Count}")
                    Return
                End If
                
                ' CRITICAL FIX: Ensure metadata arrays are properly initialized
                If pLineMetadata Is Nothing OrElse vLineIndex >= pLineMetadata.Length Then
                    Console.WriteLine($"ProcessLineFormatting: Resizing pLineMetadata from {If(pLineMetadata IsNot Nothing, pLineMetadata.Length.ToString(), "null")} to {Math.Max(vLineIndex + 1, pLineCount)}")
                    ReDim Preserve pLineMetadata(Math.Max(vLineIndex, pLineCount - 1))
                    
                    ' Initialize any new metadata entries
                    For i As Integer = 0 To pLineMetadata.Length - 1
                        If pLineMetadata(i) Is Nothing Then
                            pLineMetadata(i) = New LineMetadata()
                        End If
                    Next
                End If
                
                ' Ensure character colors array is properly initialized
                If pCharacterColors Is Nothing OrElse vLineIndex >= pCharacterColors.Length Then
                    Console.WriteLine($"ProcessLineFormatting: Resizing pCharacterColors from {If(pCharacterColors IsNot Nothing, pCharacterColors.Length.ToString(), "null")} to {Math.Max(vLineIndex + 1, pLineCount)}")
                    ReDim Preserve pCharacterColors(Math.Max(vLineIndex, pLineCount - 1))
                End If
                
                ' Get the line text safely
                Dim lOriginalText As String = pSourceFileInfo.TextLines(vLineIndex)
                
                ' Handle empty lines
                If String.IsNullOrEmpty(lOriginalText.Trim()) Then 
                    ' Mark empty lines as having highlighting too
                    If pLineMetadata(vLineIndex) IsNot Nothing Then
                        pLineMetadata(vLineIndex).HasHighlighting = True
                    End If
                    Return
                End If
                
                ' Step 1: Apply case correction and spacing with line index for context
                Dim lFormattedText As String = FormatLine(lOriginalText, vLineIndex)
                
                ' Update the line if it changed
                If lFormattedText <> lOriginalText Then
                    ' Update text in SourceFileInfo
                    pSourceFileInfo.TextLines(vLineIndex) = lFormattedText
                    
                    ' Mark as modified
                    IsModified = True
                    
                    ' Mark line metadata as changed
                    If pLineMetadata(vLineIndex) IsNot Nothing Then
                        pLineMetadata(vLineIndex).MarkChanged()
                    End If
                End If
                
                ' Step 2: Apply syntax highlighting
                ApplySyntaxHighlightingToLine(vLineIndex)
                
                ' Mark line as having highlighting
                If pLineMetadata(vLineIndex) IsNot Nothing Then
                    pLineMetadata(vLineIndex).HasHighlighting = True
                End If
                
                ' Step 3: Schedule redraw for this line
                InvalidateLine(vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessLineFormatting error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub

        ''' <summary>
        ''' Validates that the editor is in a consistent state
        ''' </summary>
        ''' <returns>True if the editor state is valid</returns>
        Private Function ValidateEditorState() As Boolean
            Try
                If pSourceFileInfo Is Nothing Then
                    Console.WriteLine("ValidateEditorState: pSourceFileInfo is Nothing")
                    Return False
                End If
                
                If pSourceFileInfo.TextLines Is Nothing Then
                    Console.WriteLine("ValidateEditorState: TextLines is Nothing")
                    Return False
                End If
                
                ' Sync line count with actual text lines
                Dim lActualLineCount As Integer = pSourceFileInfo.TextLines.Count
                If pLineCount <> lActualLineCount Then
                    Console.WriteLine($"ValidateEditorState: Line count mismatch - pLineCount={pLineCount}, actual={lActualLineCount}")
                    pLineCount = lActualLineCount
                    
                    ' Resize metadata arrays to match
                    ReDim Preserve pLineMetadata(Math.Max(0, pLineCount - 1))
                    ReDim Preserve pCharacterColors(Math.Max(0, pLineCount - 1))
                    
                    ' Initialize any null entries
                    For i As Integer = 0 To pLineCount - 1
                        If pLineMetadata(i) Is Nothing Then
                            pLineMetadata(i) = New LineMetadata()
                        End If
                        If pCharacterColors(i) Is Nothing Then
                            pCharacterColors(i) = New CharacterColorInfo() {}
                        End If
                    Next
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ValidateEditorState error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Format a line with case correction but preserve string contents
        ''' </summary>
        Private Shadows Function FormatLine(vLine As String, Optional vLineIndex As Integer = -1) As String
            Try
                ' Preserve original indentation
                Dim lOriginalIndent As String = ""
                Dim lTrimmedLine As String = vLine.TrimStart()
                
                ' Extract the original indentation
                If vLine.Length > lTrimmedLine.Length Then
                    lOriginalIndent = vLine.Substring(0, vLine.Length - lTrimmedLine.Length)
                End If
                
                ' If the line is empty or only whitespace, return as-is
                If String.IsNullOrWhiteSpace(lTrimmedLine) Then
                    Return vLine
                End If
                
                ' Tokenize the trimmed line with line index for multi-line string detection
                Dim lTokens As List(Of LineToken) = TokenizeLine(lTrimmedLine, vLineIndex)
                Dim lResult As New StringBuilder()
                
                ' Start with the original indentation
                lResult.Append(lOriginalIndent)
                
                ' Process tokens
                For i As Integer = 0 To lTokens.Count - 1
                    Dim lToken As LineToken = lTokens(i)
                    
                    Select Case lToken.Type
                        Case LineTokenType.eKeyword
                            ' Correct keyword case
                            lResult.Append(CorrectKeywordCase(lToken.Text))
                            
                        Case LineTokenType.eIdentifier
                            ' Correct identifier case based on declarations
                            lResult.Append(CorrectIdentifierCase(lToken.Text))
                            
                        Case LineTokenType.eStringLiteral, LineTokenType.eComment
                            ' IMPORTANT: Leave strings and comments completely unchanged
                            lResult.Append(lToken.Text)
                            
                        Case LineTokenType.eOperator
                            ' Don't format operators - just append them as-is
                            lResult.Append(lToken.Text)
                            
                        Case LineTokenType.eWhitespace
                            ' Preserve whitespace as-is
                            lResult.Append(lToken.Text)
                            
                        Case LineTokenType.eOther
                            ' Leave other tokens unchanged
                            lResult.Append(lToken.Text)
                    End Select
                Next
                
                Return lResult.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"FormatLine error: {ex.Message}")
                Return vLine ' Return original line on error
            End Try
        End Function


        ' Track if we're in a multi-line string for the current file
        Private pInMultiLineString As Boolean = False
        Private pMultiLineStringStartLine As Integer = -1

        
        ''' <summary>
        ''' Tokenize a line with proper multi-line string detection
        ''' </summary>
        Private Function TokenizeLine(vLine As String, vLineIndex As Integer) As List(Of LineToken)
            Dim lTokens As New List(Of LineToken)
            Dim i As Integer = 0
            Dim lLength As Integer = vLine.Length
            
            Try
                ' Check if we're inside a multi-line string by counting quotes in previous lines
                Dim lInString As Boolean = False
                If vLineIndex > 0 AndAlso vLineIndex < pLineCount Then
                    ' Count all quotes from the beginning of the file up to (but not including) this line
                    Dim lTotalQuoteCount As Integer = 0
                    
                    For lineNum As Integer = 0 To vLineIndex - 1
                        Dim lCheckLine As String = pTextLines(lineNum)
                        Dim lLineQuoteCount As Integer = 0
                        Dim j As Integer = 0
                        
                        While j < lCheckLine.Length
                            ' Check for comment - quotes after comments don't count
                            If lCheckLine(j) = "'"c Then
                                ' Skip rest of line if we hit a comment
                                Exit While
                            End If
                            
                            If lCheckLine(j) = """"c Then
                                ' Check if it's an escaped quote ("")
                                If j + 1 < lCheckLine.Length AndAlso lCheckLine(j + 1) = """"c Then
                                    ' Escaped quote - skip both characters
                                    j += 2
                                Else
                                    ' Real quote
                                    lLineQuoteCount += 1
                                    j += 1
                                End If
                            Else
                                j += 1
                            End If
                        End While
                        
                        lTotalQuoteCount += lLineQuoteCount
                    Next
                    
                    ' If odd number of quotes before this line, we're inside a string
                    lInString = (lTotalQuoteCount Mod 2) = 1
                End If
                
                ' If we're inside a multi-line string, handle accordingly
                If lInString Then
                    ' Look for the closing quote on this line
                    Dim lQuotePos As Integer = -1
                    Dim j As Integer = 0
                    
                    While j < vLine.Length
                        If vLine(j) = """"c Then
                            ' Check if it's escaped
                            If j + 1 < vLine.Length AndAlso vLine(j + 1) = """"c Then
                                ' Escaped quote - skip both
                                j += 2
                            Else
                                ' Found the closing quote
                                lQuotePos = j
                                Exit While
                            End If
                        Else
                            j += 1
                        End If
                    End While
                    
                    If lQuotePos >= 0 Then
                        ' Found closing quote - everything up to and including it is string literal
                        lTokens.Add(New LineToken(LineTokenType.eStringLiteral, vLine.Substring(0, lQuotePos + 1), True))
                        i = lQuotePos + 1
                        ' Continue processing the rest of the line normally
                    Else
                        ' No closing quote - entire line is part of the string
                        lTokens.Add(New LineToken(LineTokenType.eStringLiteral, vLine, True))
                        Return lTokens
                    End If
                End If
                
                ' Normal tokenization for the rest of the line (or whole line if not in string)
                While i < lLength
                    ' Check for comment
                    If vLine(i) = "'"c Then
                        ' Rest of line is comment
                        lTokens.Add(New LineToken(LineTokenType.eComment, vLine.Substring(i), True))
                        Exit While
                    End If
                    
                    ' Check for string literal
                    If vLine(i) = """"c Then
                        Dim lStringStart As Integer = i
                        i += 1
                        
                        ' Find end of string (handle doubled quotes)
                        Dim lFoundEnd As Boolean = False
                        While i < lLength
                            If vLine(i) = """"c Then
                                If i + 1 < lLength AndAlso vLine(i + 1) = """"c Then
                                    ' Doubled quote - skip both
                                    i += 2
                                Else
                                    ' End of string
                                    i += 1
                                    lFoundEnd = True
                                    Exit While
                                End If
                            Else
                                i += 1
                            End If
                        End While
                        
                        ' If we didn't find the end, this starts a multi-line string
                        lTokens.Add(New LineToken(LineTokenType.eStringLiteral, vLine.Substring(lStringStart, i - lStringStart), True))
                        Continue While
                    End If
                    
                    ' Check for whitespace
                    If Char.IsWhiteSpace(vLine(i)) Then
                        Dim lWsStart As Integer = i
                        While i < lLength AndAlso Char.IsWhiteSpace(vLine(i))
                            i += 1
                        End While
                        lTokens.Add(New LineToken(LineTokenType.eWhitespace, vLine.Substring(lWsStart, i - lWsStart), False))
                        Continue While
                    End If
                    
                    ' Check for operators
                    If IsOperatorChar(vLine(i)) Then
                        Dim lOpStart As Integer = i
                        While i < lLength AndAlso IsOperatorChar(vLine(i))
                            i += 1
                        End While
                        lTokens.Add(New LineToken(LineTokenType.eOperator, vLine.Substring(lOpStart, i - lOpStart), False))
                        Continue While
                    End If
                    
                    ' Check for word (keyword or identifier)
                    If Char.IsLetter(vLine(i)) OrElse vLine(i) = "_"c Then
                        Dim lWordStart As Integer = i
                        While i < lLength AndAlso (Char.IsLetterOrDigit(vLine(i)) OrElse vLine(i) = "_"c)
                            i += 1
                        End While
                        
                        Dim lWord As String = vLine.Substring(lWordStart, i - lWordStart)
                        Dim lTokenType As LineTokenType = If(IsVBKeyword(lWord), LineTokenType.eKeyword, LineTokenType.eIdentifier)
                        lTokens.Add(New LineToken(lTokenType, lWord, False))
                        Continue While
                    End If
                    
                    ' Other character
                    lTokens.Add(New LineToken(LineTokenType.eOther, vLine(i).ToString(), False))
                    i += 1
                End While
                
            Catch ex As Exception
                Console.WriteLine($"TokenizeLine error: {ex.Message}")
            End Try
            
            Return lTokens
        End Function


        ''' <summary>
        ''' Check if a line is within a multi-line string context
        ''' </summary>
        Private Function IsInMultiLineString(vLineIndex As Integer) As Boolean
            Try
                ' Scan backwards from the current line to find unclosed multi-line strings
                For i As Integer = vLineIndex - 1 To Math.Max(0, vLineIndex - 50) Step -1
                    Dim lLine As String = pTextLines(i)
                    
                    ' Check for $" that starts a multi-line string
                    Dim lPos As Integer = lLine.IndexOf("$""")
                    If lPos >= 0 Then
                        ' Found a potential multi-line string start
                        ' Check if it's closed on the same line
                        Dim lRestOfLine As String = lLine.Substring(lPos + 2)
                        Dim lQuotePos As Integer = 0
                        Dim lClosed As Boolean = False
                        
                        While lQuotePos < lRestOfLine.Length
                            lQuotePos = lRestOfLine.IndexOf(""""c, lQuotePos)
                            If lQuotePos < 0 Then Exit While
                            
                            ' Check if it's escaped
                            If lQuotePos + 1 < lRestOfLine.Length AndAlso lRestOfLine(lQuotePos + 1) = """"c Then
                                lQuotePos += 2 ' Skip escaped quote
                            Else
                                ' Found closing quote on same line
                                lClosed = True
                                Exit While
                            End If
                        End While
                        
                        If Not lClosed Then
                            ' This multi-line string is not closed, check if it extends to our line
                            ' Scan forward to see if it's closed before our line
                            For j As Integer = i + 1 To vLineIndex - 1
                                Dim lCheckLine As String = pTextLines(j)
                                Dim lCheckPos As Integer = 0
                                
                                While lCheckPos < lCheckLine.Length
                                    lCheckPos = lCheckLine.IndexOf(""""c, lCheckPos)
                                    If lCheckPos < 0 Then Exit While
                                    
                                    ' Check if it's escaped
                                    If lCheckPos + 1 < lCheckLine.Length AndAlso lCheckLine(lCheckPos + 1) = """"c Then
                                        lCheckPos += 2 ' Skip escaped quote
                                    Else
                                        ' Found closing quote
                                        Return False ' String was closed before our line
                                    End If
                                End While
                            Next
                            
                            ' String wasn't closed before our line, so we're in it
                            Return True
                        End If
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsInMultiLineString error: {ex.Message}")
                Return False
            End Try
        End Function

        
        ' Correct keyword case
        Private Function CorrectKeywordCase(vKeyword As String) As String
            ' VB.NET keywords with proper case
            Dim lKeywords As Dictionary(Of String, String) = GetKeywordCaseDictionary()
            
            Dim lProperCase As String = Nothing
            If lKeywords.TryGetValue(vKeyword.ToLower(), lProperCase) Then
                Return lProperCase
            End If
            
            Return vKeyword ' Return as-is if not found
        End Function
        
        ' Correct identifier case based on declarations
        Private Function CorrectIdentifierCase(vIdentifier As String) As String
            Dim lProperCase As String = Nothing
            If pIdentifierCaseMap.TryGetValue(vIdentifier, lProperCase) Then
                Return lProperCase
            End If
            
            Return vIdentifier ' Return as-is if not found
        End Function
        
        ' Format operator with proper spacing
        Private Function FormatOperator(vOperator As String, vAllTokens As List(Of LineToken), vCurrentToken As LineToken) As String
            ' Add spaces around most operators
            Select Case vOperator
                Case "=", "+", "-", "*", "/", "\", "^", "&", "<", ">", "<=", ">=", "<>", "and", "Or", "AndAlso", "OrElse"
                    Return " " & vOperator & " "
                    
                Case ".", "!", "?", ":"
                    ' No spaces around these
                    Return vOperator
                    
                Case "("
                    ' No space before, possible space after
                    Return vOperator
                    
                Case ")"
                    ' Possible space before, no space after
                    Return vOperator
                    
                Case ","
                    ' No space before, space after
                    Return vOperator & " "
                    
                Case Else
                    Return vOperator
            End Select
        End Function
        
        ' Check if character is an operator
        Private Function IsOperatorChar(vChar As Char) As Boolean
            Return "=+-*/\^&<>()[]{},.!?:".Contains(vChar)
        End Function
        
        ' Check if word is a VB keyword
        Private Function IsVBKeyword(vWord As String) As Boolean
            Dim lKeywords As HashSet(Of String) = GetVBKeywords()
            Return lKeywords.Contains(vWord.ToLower())
        End Function
        
        ' Get keyword case dictionary
        Private Function GetKeywordCaseDictionary() As Dictionary(Of String, String)
            Static lDict As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"addhandler", "AddHandler"}, {"addressof", "AddressOf"}, {"alias", "Alias"}, {"and", "and"},
                {"andalso", "AndAlso"}, {"as", "As"}, {"boolean", "Boolean"}, {"byref", "ByRef"},
                {"byte", "Byte"}, {"byval", "ByVal"}, {"call", "Call"}, {"case", "Case"},
                {"catch", "Catch"}, {"cbool", "CBool"}, {"cbyte", "CByte"}, {"cchar", "CChar"},
                {"cdate", "CDate"}, {"cdbl", "CDbl"}, {"cdec", "CDec"}, {"char", "Char"},
                {"cint", "CInt"}, {"class", "Class"}, {"clng", "CLng"}, {"cobj", "CObj"},
                {"const", "Const"}, {"continue", "Continue"}, {"csbyte", "CSByte"}, {"cshort", "CShort"},
                {"csng", "CSng"}, {"cstr", "CStr"}, {"ctype", "CType"}, {"cuint", "CUInt"},
                {"culng", "CULng"}, {"cushort", "CUShort"}, {"date", "Date"}, {"decimal", "Decimal"},
                {"declare", "Declare"}, {"default", "Default"}, {"delegate", "Delegate"}, {"dim", "Dim"},
                {"directcast", "DirectCast"}, {"do", "Do"}, {"double", "Double"}, {"each", "each"},
                {"else", "Else"}, {"elseif", "ElseIf"}, {"end", "End"}, {"endif", "EndIf"},
                {"enum", "Enum"}, {"erase", "Erase"}, {"error", "error"}, {"event", "Event"},
                {"exit", "Exit"}, {"false", "False"}, {"finally", "Finally"}, {"for", "for"},
                {"friend", "Friend"}, {"function", "Function"}, {"get", "Get"}, {"gettype", "GetType"},
                {"getxmlnamespace", "GetXMLNamespace"}, {"global", "Global"}, {"gosub", "GoSub"}, {"goto", "GoTo"},
                {"handles", "Handles"}, {"if", "If"}, {"implements", "Implements"}, {"imports", "Imports"},
                {"in", "in"}, {"inherits", "Inherits"}, {"integer", "Integer"}, {"interface", "Interface"},
                {"is", "Is"}, {"isnot", "IsNot"}, {"let", "Let"}, {"lib", "Lib"},
                {"like", "Like"}, {"long", "Long"}, {"loop", "Loop"}, {"me", "Me"},
                {"mod", "Mod"}, {"module", "Module"}, {"mustinherit", "MustInherit"}, {"mustoverride", "MustOverride"},
                {"mybase", "MyBase"}, {"myclass", "MyClass"}, {"namespace", "Namespace"}, {"narrowing", "Narrowing"},
                {"New", "New"}, {"next", "Next"}, {"not", "Not"}, {"nothing", "Nothing"},
                {"notinheritable", "NotInheritable"}, {"notoverridable", "NotOverridable"}, {"object", "Object"}, {"of", "Of"},
                {"on", "On"}, {"operator", "Operator"}, {"option", "Option"}, {"optional", "Optional"},
                {"or", "Or"}, {"orelse", "OrElse"}, {"out", "Out"}, {"overloads", "Overloads"},
                {"overridable", "Overridable"}, {"overrides", "Overrides"}, {"paramarray", "ParamArray"}, {"partial", "Partial"},
                {"private", "Private"}, {"property", "Property"}, {"protected", "Protected"}, {"public", "Public"},
                {"raiseevent", "RaiseEvent"}, {"readonly", "ReadOnly"}, {"redim", "ReDim"}, {"rem", "REM"},
                {"removehandler", "RemoveHandler"}, {"resume", "Resume"}, {"return", "Return"}, {"sbyte", "SByte"},
                {"select", "Select"}, {"set", "Set"}, {"shadows", "Shadows"}, {"shared", "Shared"},
                {"short", "Short"}, {"single", "Single"}, {"static", "Static"}, {"step", "Step"},
                {"stop", "Stop"}, {"string", "String"}, {"structure", "Structure"}, {"sub", "Sub"},
                {"synclock", "SyncLock"}, {"then", "Then"}, {"throw", "Throw"}, {"to", "To"},
                {"true", "True"}, {"try", "Try"}, {"trycast", "TryCast"}, {"typeof", "TypeOf"},
                {"uinteger", "UInteger"}, {"ulong", "ULong"}, {"ushort", "UShort"}, {"using", "Using"},
                {"variant", "Variant"}, {"wend", "Wend"}, {"when", "When"}, {"while", "While"},
                {"widening", "Widening"}, {"with", "with"}, {"withevents", "WithEvents"}, {"writeonly", "WriteOnly"},
                {"xor", "Xor"}
            }
            Return lDict
        End Function
        
        ''' <summary>
        ''' Update identifier case map (called by IdentifierCapitalizationManager)
        ''' </summary>
        Public Sub UpdateIdentifierCaseMap(vName As String, vProperCase As String)
            Try
                pIdentifierCaseMap(vName) = vProperCase
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseMap error: {ex.Message}")
            End Try
        End Sub
        
        ' Mark a line as being edited (no formatting while typing)
        Public Sub SetEditingLine(vLine As Integer)
            Try
                ' If switching from another line, format the previous one
                If pEditingLine >= 0 AndAlso pEditingLine <> vLine AndAlso pEditingLine < pLineCount Then
                    ProcessLineFormatting(pEditingLine)
                End If
                
                pEditingLine = vLine
                pLastEditedLine = vLine
                
            Catch ex As Exception
                Console.WriteLine($"SetEditingLine error: {ex.Message}")
            End Try
        End Sub
        
        ' Clear editing line (format it)
        Public Sub ClearEditingLine()
            Try
                If pEditingLine >= 0 AndAlso pEditingLine < pLineCount Then
                    ProcessLineFormatting(pEditingLine)
                    pEditingLine = -1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ClearEditingLine error: {ex.Message}")
            End Try
        End Sub

        ' Helper to update line metadata from parse result
        Private Sub UpdateLineMetadataFromParseResult(vParseResult As VBParser.ParseResult)
            Try
                If vParseResult Is Nothing Then Return
                
                ' Update line metadata if available
                If vParseResult.LineMetadata IsNot Nothing Then
                    For i As Integer = 0 To Math.Min(vParseResult.LineMetadata.Length - 1, pLineMetadata.Length - 1)
                        ' Copy relevant metadata
                        If vParseResult.LineMetadata(i) IsNot Nothing Then
                            ' Update syntax tokens if available
                            If vParseResult.LineMetadata(i).SyntaxTokens IsNot Nothing Then
                                pLineMetadata(i).SyntaxTokens = vParseResult.LineMetadata(i).SyntaxTokens
                            End If
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineMetadataFromParseResult error: {ex.Message}")
            End Try
        End Sub
        
        ' Helper to update identifier case map from parsed nodes
        Private Sub UpdateIdentifierCaseMap()
            Try
                pIdentifierCaseMap.Clear()
                
                ' Add all identifiers from parsed nodes
                If pDocumentNodes IsNot Nothing Then
                    For Each lKvp In pDocumentNodes
                        Dim lNode As DocumentNode = lKvp.Value
                        If Not String.IsNullOrEmpty(lNode.Name) Then
                            pIdentifierCaseMap(lNode.Name) = lNode.Name
                        End If
                    Next
                End If
                
                ' Also add from syntax tree if available
                If pRootNode IsNot Nothing Then
                    AddIdentifiersFromNode(pRootNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseMap error: {ex.Message}")
            End Try
        End Sub

        Private Sub AddIdentifiersFromNode(vNode As SyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                If Not String.IsNullOrEmpty(vNode.Name) Then
                    pIdentifierCaseMap(vNode.Name) = vNode.Name
                End If
                
                If vNode.Children IsNot Nothing Then
                    For Each lChild In vNode.Children
                        AddIdentifiersFromNode(lChild)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddIdentifiersFromNode error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
    ' ===== Helper Classes =====
    
    ' Token type enumeration
    Friend Enum LineTokenType
        eKeyword
        eIdentifier
        eStringLiteral
        eComment
        eOperator
        eWhitespace
        eOther
    End Enum
    
    ' Token class for line parsing
    Friend Class LineToken
        Public Property Type As LineTokenType
        Public Property Text As String
        Public Property PreserveSpacing As Boolean
        
        Public Sub New(vType As LineTokenType, vText As String, vPreserveSpacing As Boolean)
            Type = vType
            Text = vText
            PreserveSpacing = vPreserveSpacing
        End Sub
    End Class
        
    
End Namespace
