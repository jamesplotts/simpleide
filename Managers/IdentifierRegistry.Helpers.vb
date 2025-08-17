' Managers/IdentifierRegistry.Helpers.vb - Private helper methods for IdentifierRegistry
' This is a continuation of the IdentifierRegistry class 

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Text
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Managers
    
    ''' <summary>
    ''' Comprehensive in-memory registry of all identifiers with instant reference tracking
    ''' </summary>
    Partial Public Class IdentifierRegistry
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Check if a position in the content is inside a multi-line string
        ''' </summary>
        Private Function IsInsideMultiLineString(vContent As String, vPosition As Integer) As Boolean
            Try
                ' Count quotes from start to position
                Dim lQuoteCount As Integer = 0
                Dim i As Integer = 0
                
                While i < vPosition AndAlso i < vContent.Length
                    Dim lChar As Char = vContent(i)
                    
                    ' Check for comment start
                    If lChar = "'"c Then
                        ' Skip to end of line
                        While i < vPosition AndAlso i < vContent.Length AndAlso vContent(i) <> vbLf AndAlso vContent(i) <> vbCr
                            i += 1
                        End While
                        Continue While
                    End If
                    
                    ' Check for quote
                    If lChar = """"c Then
                        ' Check if it's escaped
                        If i + 1 < vContent.Length AndAlso vContent(i + 1) = """"c Then
                            ' Escaped quote - skip both
                            i += 2
                        Else
                            ' Real quote
                            lQuoteCount += 1
                            i += 1
                        End If
                    Else
                        i += 1
                    End If
                End While
                
                ' If odd number of quotes, we're inside a string
                Return (lQuoteCount Mod 2) = 1
                
            Catch ex As Exception
                Console.WriteLine($"IsInsideMultiLineString error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parse a line for declarations
        ''' </summary>
        Private Sub ParseLineForDeclarations(vFilePath As String, vLine As String, vLineNumber As Integer, 
                                            Optional vDocumentModel As DocumentModel = Nothing)
            Try
                ' Skip empty lines and comments
                Dim lTrimmed As String = vLine.TrimStart()
                If String.IsNullOrEmpty(lTrimmed) OrElse lTrimmed.StartsWith("'") Then Return
                
                ' Check if we're inside a multi-line string
                ' We need to track this at the file level to handle multi-line strings properly
                Dim lInMultiLineString As Boolean = False
                If Not String.IsNullOrEmpty(vFilePath) Then
                    pMultiLineStringsByFile.TryGetValue(vFilePath, lInMultiLineString)
                End If
                
                ' If the entire line is inside a multi-line string, skip it
                If lInMultiLineString Then
                    ' Check if this line ends the multi-line string
                    Dim lQuoteCount As Integer = 0
                    For i As Integer = 0 To vLine.Length - 1
                        If vLine(i) = """"c Then
                            If i + 1 < vLine.Length AndAlso vLine(i + 1) = """"c Then
                                i += 1 ' Skip escaped quote
                            Else
                                lQuoteCount += 1
                            End If
                        End If
                    Next
                    
                    ' If odd number of quotes, string ends on this line
                    If (lQuoteCount Mod 2) = 1 Then
                        pMultiLineStringsByFile(vFilePath) = False
                    End If
                    
                    Return ' Don't parse declarations in string literals
                End If
                
                ' Check if this line starts a multi-line string
                Dim lInString As Boolean = False
                Dim lLastNonStringPos As Integer = -1
                
                For i As Integer = 0 To vLine.Length - 1
                    If vLine(i) = """"c Then
                        If i + 1 < vLine.Length AndAlso vLine(i + 1) = """"c Then
                            i += 1 ' Skip escaped quote
                        Else
                            lInString = Not lInString
                            If Not lInString Then
                                lLastNonStringPos = i
                            End If
                        End If
                    ElseIf vLine(i) = "'"c AndAlso Not lInString Then
                        ' Comment starts - stop processing
                        Exit For
                    End If
                Next
                
                ' If we end in a string, mark it as multi-line
                If lInString AndAlso Not String.IsNullOrEmpty(vFilePath) Then
                    pMultiLineStringsByFile(vFilePath) = True
                End If
                
                ' Only parse the non-string portion of the line
                Dim lLineToParse As String = vLine
                If lLastNonStringPos >= 0 AndAlso lLastNonStringPos < vLine.Length - 1 Then
                    ' Truncate at the last closing quote
                    lLineToParse = vLine.Substring(0, lLastNonStringPos + 1)
                End If
                
                ' Variable declarations
                Dim lVarPattern As New Regex("(?:^|\s)(?:Dim|Private|Public|Protected|Friend)\s+(?:Shared\s+)?(?:ReadOnly\s+)?(\w+)(?:\s*,\s*(\w+))*\s+As\s+", RegexOptions.IgnoreCase)
                Dim lVarMatch As Match = lVarPattern.Match(lLineToParse)
                If lVarMatch.Success Then
                    For i As Integer = 1 To lVarMatch.Groups.Count - 1
                        If lVarMatch.Groups(i).Success AndAlso Not String.IsNullOrWhiteSpace(lVarMatch.Groups(i).Value) Then
                            RegisterDeclaration(lVarMatch.Groups(i).Value, IdentifierScope.eField, vFilePath, vLineNumber, lVarMatch.Groups(i).Index)
                        End If
                    Next
                End If
                
                ' Method declarations
                Dim lMethodPattern As New Regex("(?:^|\s)(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:Overrides\s+)?(?:Function|Sub)\s+(\w+)\s*\(", RegexOptions.IgnoreCase)
                Dim lMethodMatch As Match = lMethodPattern.Match(lLineToParse)
                If lMethodMatch.Success Then
                    RegisterDeclaration(lMethodMatch.Groups(1).Value, IdentifierScope.eMethod, vFilePath, vLineNumber, lMethodMatch.Groups(1).Index)
                End If
                
                ' Property declarations
                Dim lPropPattern As New Regex("(?:^|\s)(?:Public|Private|Protected|Friend)?\s*(?:Shared\s+)?(?:ReadOnly\s+|WriteOnly\s+)?Property\s+(\w+)\s*(?:\(|$|\s)", RegexOptions.IgnoreCase)
                Dim lPropMatch As Match = lPropPattern.Match(lLineToParse)
                If lPropMatch.Success Then
                    RegisterDeclaration(lPropMatch.Groups(1).Value, IdentifierScope.eProperty, vFilePath, vLineNumber, lPropMatch.Groups(1).Index)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseLineForDeclarations error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parse a line for references
        ''' </summary>
        Private Sub ParseLineForReferences(vFilePath As String, vLine As String, vLineNumber As Integer)
            Try
                ' Skip empty lines and comments
                Dim lTrimmed As String = vLine.TrimStart()
                If String.IsNullOrEmpty(lTrimmed) OrElse lTrimmed.StartsWith("'") Then Return
                
                ' Parse the line character by character to respect string boundaries
                Dim i As Integer = 0
                Dim lInString As Boolean = False
                Dim lInComment As Boolean = False
                
                While i < vLine.Length
                    Dim lChar As Char = vLine(i)
                    
                    ' Check for comment start (not in string)
                    If lChar = "'"c AndAlso Not lInString Then
                        ' Rest of line is comment - stop processing
                        Exit While
                    End If
                    
                    ' Check for string start/end
                    If lChar = """"c AndAlso Not lInComment Then
                        ' Check if it's escaped
                        If i + 1 < vLine.Length AndAlso vLine(i + 1) = """"c Then
                            ' Escaped quote - skip both
                            i += 2
                            Continue While
                        Else
                            ' Toggle string state
                            lInString = Not lInString
                            i += 1
                            Continue While
                        End If
                    End If
                    
                    ' If we're not in a string or comment, look for identifiers
                    If Not lInString AndAlso Not lInComment Then
                        ' Check if this starts an identifier
                        If (Char.IsLetter(lChar) OrElse lChar = "_"c) Then
                            Dim lStart As Integer = i
                            
                            ' Find the end of the identifier
                            While i < vLine.Length AndAlso (Char.IsLetterOrDigit(vLine(i)) OrElse vLine(i) = "_"c)
                                i += 1
                            End While
                            
                            Dim lIdentifier As String = vLine.Substring(lStart, i - lStart)
                            
                            ' Skip VB keywords
                            If Not IsKeyword(lIdentifier) Then
                                ' Check if this identifier has a declaration
                                Dim lKey As String = lIdentifier.ToLowerInvariant()
                                If pDeclarations.ContainsKey(lKey) Then
                                    RegisterReference(lIdentifier, vFilePath, vLineNumber, lStart, ReferenceKind.eUsage)
                                End If
                            End If
                        Else
                            i += 1
                        End If
                    Else
                        i += 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"ParseLineForReferences error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Update reference consistency after declaration change
        ''' </summary>
        Private Sub UpdateReferenceConsistency(vDeclaration As DeclarationInfo)
            Try
                ' Clear old inconsistencies for this declaration
                pInconsistencies.RemoveAll(Function(i) i.Declaration Is vDeclaration)
                
                ' Also clear from file-based index
                For Each lFileList In pInconsistenciesByFile.Values
                    lFileList.RemoveAll(Function(i) i.Declaration Is vDeclaration)
                Next
                
                ' Check each reference
                For Each lRef In vDeclaration.References
                    lRef.IsInconsistent = Not lRef.IdentifierName.Equals(vDeclaration.CanonicalCase, StringComparison.Ordinal)
                    
                    If lRef.IsInconsistent Then
                        ' Create inconsistency record with ALL properties set
                        Dim lInconsistency As New InconsistencyInfo With {
                            .Declaration = vDeclaration,
                            .Reference = lRef,
                            .ActualCase = lRef.IdentifierName,                    ' FIXED: Set ActualCase
                            .CorrectCase = vDeclaration.CanonicalCase,            ' FIXED: Set CorrectCase
                            .Message = $"'{lRef.IdentifierName}' should be '{vDeclaration.CanonicalCase}' at {System.IO.Path.GetFileName(lRef.FilePath)}:{lRef.Line + 1}:{lRef.Column + 1}"
                        }
                        
                        pInconsistencies.Add(lInconsistency)
                        
                        ' Add to file map
                        If Not pInconsistenciesByFile.ContainsKey(lRef.FilePath) Then
                            pInconsistenciesByFile(lRef.FilePath) = New List(Of InconsistencyInfo)()
                        End If
                        pInconsistenciesByFile(lRef.FilePath).Add(lInconsistency)
                        
                        RaiseEvent InconsistencyDetected(lInconsistency)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"UpdateReferenceConsistency error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get the character position where a line starts in the content
        ''' </summary>
        Private Function GetLineStartPosition(vContent As String, vLineNumber As Integer) As Integer
            Try
                If vLineNumber = 0 Then Return 0
                
                Dim lCurrentLine As Integer = 0
                
                For i As Integer = 0 To vContent.Length - 1
                    If lCurrentLine = vLineNumber Then
                        Return i
                    End If
                    
                    If vContent(i) = vbLf Then
                        lCurrentLine += 1
                    ElseIf vContent(i) = vbCr Then
                        lCurrentLine += 1
                        ' Skip LF if it follows CR
                        If i + 1 < vContent.Length AndAlso vContent(i + 1) = vbLf Then
                            i += 1
                        End If
                    End If
                Next
                
                Return vContent.Length
                
            Catch ex As Exception
                Console.WriteLine($"GetLineStartPosition error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Check if a word is a VB.NET keyword
        ''' </summary>
        Private Function IsKeyword(vWord As String) As Boolean
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
        
        ''' <summary>
        ''' Normalize a line of code - simplified version for backward compatibility
        ''' </summary>
        Public Function NormalizeLine(vLine As String) As String
            Try
                Return NormalizeLineSegment(vLine)
            Catch ex As Exception
                Console.WriteLine($"NormalizeLine error: {ex.Message}")
                Return vLine
            End Try
        End Function
        
        ''' <summary>
        ''' Normalize a line of code based on its context in the full file
        ''' </summary>
        Public Function NormalizeLine(vLine As String, vFileContent As String, vLineNumber As Integer) As String
            Try
                If String.IsNullOrEmpty(vLine) Then Return vLine
                
                ' Check if this line is inside a multi-line string
                Dim lLineStartPos As Integer = GetLineStartPosition(vFileContent, vLineNumber)
                If IsInsideMultiLineString(vFileContent, lLineStartPos) Then
                    ' Check if the line ends the multi-line string
                    Dim lQuotePos As Integer = 0
                    While lQuotePos < vLine.Length
                        If vLine(lQuotePos) = """"c Then
                            ' Check if escaped
                            If lQuotePos + 1 < vLine.Length AndAlso vLine(lQuotePos + 1) = """"c Then
                                lQuotePos += 2
                            Else
                                ' Found closing quote - normalize only the part after it
                                Dim lBeforeQuote As String = vLine.Substring(0, lQuotePos + 1)
                                Dim lAfterQuote As String = vLine.Substring(lQuotePos + 1)
                                Return lBeforeQuote & NormalizeLineSegment(lAfterQuote)
                            End If
                        Else
                            lQuotePos += 1
                        End If
                    End While
                    ' No closing quote found - entire line is in string
                    Return vLine
                End If
                
                ' Not in a multi-line string - process normally
                Return NormalizeLineSegment(vLine)
                
            Catch ex As Exception
                Console.WriteLine($"NormalizeLine error: {ex.Message}")
                Return vLine
            End Try
        End Function
        
        ''' <summary>
        ''' Normalize a segment of a line (not in multi-line string context)
        ''' </summary>
        Private Function NormalizeLineSegment(vLine As String) As String
            Try
                ' Build the normalized result
                Dim lResult As New StringBuilder()
                Dim i As Integer = 0
                Dim lInString As Boolean = False
                Dim lInInterpolatedString As Boolean = False
                Dim lInChar As Boolean = False
                
                While i < vLine.Length
                    Dim lChar As Char = vLine(i)
                    
                    ' Check for comment (not in string)
                    If lChar = "'"c AndAlso Not lInString AndAlso Not lInInterpolatedString AndAlso Not lInChar Then
                        ' Rest of line is comment, append as-is
                        lResult.Append(vLine.Substring(i))
                        Exit While
                    End If
                    
                    ' Check for REM comment
                    If Not lInString AndAlso Not lInInterpolatedString AndAlso Not lInChar Then
                        If i = 0 OrElse Not Char.IsLetterOrDigit(vLine(i - 1)) Then
                            If i + 3 <= vLine.Length AndAlso vLine.Substring(i, 3).ToUpper() = "REM" Then
                                If i + 3 = vLine.Length OrElse vLine(i + 3) = " "c Then
                                    ' Rest of line is REM comment
                                    lResult.Append(vLine.Substring(i))
                                    Exit While
                                End If
                            End If
                        End If
                    End If
                    
                    ' Check for character literal
                    If lChar = """"c AndAlso Not lInString AndAlso Not lInInterpolatedString AndAlso Not lInChar Then
                        ' Look ahead for "c pattern (character literal)
                        If i + 2 < vLine.Length AndAlso vLine(i + 2) = "c"c Then
                            ' This is a character literal
                            lResult.Append(vLine.Substring(i, 3))
                            i += 3
                            Continue While
                        End If
                    End If
                    
                    ' Check for string interpolation start ($")
                    If i < vLine.Length - 1 AndAlso lChar = "$"c AndAlso vLine(i + 1) = """"c AndAlso Not lInString AndAlso Not lInChar Then
                        ' Start of interpolated string
                        lInInterpolatedString = True
                        lResult.Append("$""")
                        i += 2
                        Continue While
                    End If
                    
                    ' Check for regular string start/end
                    If lChar = """"c AndAlso Not lInChar Then
                        If lInString OrElse lInInterpolatedString Then
                            ' Check for escaped quote
                            If i + 1 < vLine.Length AndAlso vLine(i + 1) = """"c Then
                                ' Escaped quote - add both
                                lResult.Append("""""")
                                i += 2
                                Continue While
                            Else
                                ' End of string
                                lInString = False
                                lInInterpolatedString = False
                                lResult.Append("""")
                                i += 1
                                Continue While
                            End If
                        Else
                            ' Start of string
                            lInString = True
                            lResult.Append("""")
                            i += 1
                            Continue While
                        End If
                    End If
                    
                    ' Handle content inside strings - don't normalize
                    If lInString OrElse lInInterpolatedString Then
                        lResult.Append(lChar)
                        i += 1
                        Continue While
                    End If
                    
                    ' Not in string - check for identifier to normalize
                    If Char.IsLetter(lChar) OrElse lChar = "_"c Then
                        Dim lWordStart As Integer = i
                        While i < vLine.Length AndAlso (Char.IsLetterOrDigit(vLine(i)) OrElse vLine(i) = "_"c)
                            i += 1
                        End While
                        
                        Dim lWord As String = vLine.Substring(lWordStart, i - lWordStart)
                        
                        ' Check if it's a keyword or identifier
                        Dim lNormalizedWord As String = GetNormalizedIdentifier(lWord)
                        lResult.Append(lNormalizedWord)
                    Else
                        ' Other character - append as-is
                        lResult.Append(lChar)
                        i += 1
                    End If
                End While
                
                Return lResult.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"NormalizeLineSegment error: {ex.Message}")
                Return vLine
            End Try
        End Function
        
        ''' <summary>
        ''' Get normalized identifier from registry
        ''' </summary>
        Private Function GetNormalizedIdentifier(vIdentifier As String) As String
            Try
                ' First check if it's a VB keyword - use proper casing
                Dim lKeywords As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"AddHandler", "AddHandler"}, {"AddressOf", "AddressOf"}, {"Alias", "Alias"}, {"And", "And"}, {"AndAlso", "AndAlso"},
                    {"As", "As"}, {"Boolean", "Boolean"}, {"ByRef", "ByRef"}, {"Byte", "Byte"}, {"ByVal", "ByVal"},
                    {"Call", "Call"}, {"Case", "Case"}, {"Catch", "Catch"}, {"CBool", "CBool"}, {"CByte", "CByte"},
                    {"CChar", "CChar"}, {"CDate", "CDate"}, {"CDbl", "CDbl"}, {"CDec", "CDec"}, {"Char", "Char"},
                    {"CInt", "CInt"}, {"Class", "Class"}, {"CLng", "CLng"}, {"CObj", "CObj"}, {"Const", "Const"},
                    {"Continue", "Continue"}, {"CSByte", "CSByte"}, {"CShort", "CShort"}, {"CSng", "CSng"}, {"CStr", "CStr"},
                    {"CType", "CType"}, {"CUInt", "CUInt"}, {"CULng", "CULng"}, {"CUShort", "CUShort"}, {"Date", "Date"},
                    {"Decimal", "Decimal"}, {"Declare", "Declare"}, {"Default", "Default"}, {"Delegate", "Delegate"}, {"Dim", "Dim"},
                    {"DirectCast", "DirectCast"}, {"Do", "Do"}, {"Double", "Double"}, {"Each", "Each"}, {"Else", "Else"},
                    {"ElseIf", "ElseIf"}, {"End", "End"}, {"EndIf", "EndIf"}, {"Enum", "Enum"}, {"Erase", "Erase"},
                    {"Error", "Error"}, {"Event", "Event"}, {"Exit", "Exit"}, {"False", "False"}, {"Finally", "Finally"},
                    {"For", "For"}, {"Friend", "Friend"}, {"Function", "Function"}, {"Get", "Get"}, {"GetType", "GetType"},
                    {"GetXMLNamespace", "GetXMLNamespace"}, {"Global", "Global"}, {"GoSub", "GoSub"}, {"GoTo", "GoTo"}, {"Handles", "Handles"},
                    {"If", "If"}, {"Implements", "Implements"}, {"Imports", "Imports"}, {"In", "In"}, {"Inherits", "Inherits"},
                    {"Integer", "Integer"}, {"Interface", "Interface"}, {"Is", "Is"}, {"IsNot", "IsNot"}, {"Let", "Let"},
                    {"Lib", "Lib"}, {"Like", "Like"}, {"Long", "Long"}, {"Loop", "Loop"}, {"Me", "Me"},
                    {"Mod", "Mod"}, {"Module", "Module"}, {"MustInherit", "MustInherit"}, {"MustOverride", "MustOverride"}, 
                    {"MyBase", "MyBase"}, {"MyClass", "MyClass"}, {"Namespace", "Namespace"}, {"Narrowing", "Narrowing"}, 
                    {"New", "New"}, {"Next", "Next"}, {"Not", "Not"}, {"Nothing", "Nothing"}, {"NotInheritable", "NotInheritable"}, 
                    {"NotOverridable", "NotOverridable"}, {"Object", "Object"}, {"Of", "Of"}, {"On", "On"}, {"Operator", "Operator"}, 
                    {"Option", "Option"}, {"Optional", "Optional"}, {"Or", "Or"}, {"OrElse", "OrElse"}, {"Overloads", "Overloads"}, 
                    {"Overridable", "Overridable"}, {"Overrides", "Overrides"}, {"ParamArray", "ParamArray"}, {"Partial", "Partial"}, 
                    {"Private", "Private"}, {"Property", "Property"}, {"Protected", "Protected"}, {"Public", "Public"}, 
                    {"RaiseEvent", "RaiseEvent"}, {"ReadOnly", "ReadOnly"}, {"ReDim", "ReDim"}, {"REM", "REM"}, 
                    {"RemoveHandler", "RemoveHandler"}, {"Resume", "Resume"}, {"Return", "Return"}, {"SByte", "SByte"}, 
                    {"Select", "Select"}, {"Set", "Set"}, {"Shadows", "Shadows"}, {"Shared", "Shared"}, {"Short", "Short"},
                    {"Single", "Single"}, {"Static", "Static"}, {"Step", "Step"}, {"Stop", "Stop"}, {"String", "String"}, 
                    {"Structure", "Structure"}, {"Sub", "Sub"}, {"SyncLock", "SyncLock"}, {"Then", "Then"}, {"Throw", "Throw"}, 
                    {"To", "To"}, {"True", "True"}, {"Try", "Try"}, {"TryCast", "TryCast"}, {"TypeOf", "TypeOf"}, 
                    {"UInteger", "UInteger"}, {"ULong", "ULong"}, {"UShort", "UShort"}, {"Using", "Using"}, {"Variant", "Variant"}, 
                    {"Wend", "Wend"}, {"When", "When"}, {"While", "While"}, {"Widening", "Widening"}, {"With", "With"}, 
                    {"WithEvents", "WithEvents"}, {"WriteOnly", "WriteOnly"}, {"Xor", "Xor"}
                }
                
                ' Check if it's a keyword
                If lKeywords.ContainsKey(vIdentifier) Then
                    Return lKeywords(vIdentifier)
                End If
                
                ' Check if we have a declaration for this identifier
                Dim lKey As String = vIdentifier.ToLowerInvariant()
                If pDeclarations.ContainsKey(lKey) Then
                    Dim lDeclaration As DeclarationInfo = pDeclarations(lKey)
                    Return lDeclaration.CanonicalCase
                End If
                
                ' Return as-is if not found
                Return vIdentifier
                
            Catch ex As Exception
                Return vIdentifier
            End Try
        End Function
        
        ''' <summary>
        ''' Find the closing quote for a string starting at the given position
        ''' Returns -1 if no closing quote is found on this line
        ''' </summary>
        Private Function FindClosingQuote(vLine As String, vStartPos As Integer) As Integer
            Dim lPos As Integer = vStartPos
            
            While lPos < vLine.Length
                If vLine(lPos) = """"c Then
                    ' Check if it's escaped
                    If lPos + 1 < vLine.Length AndAlso vLine(lPos + 1) = """"c Then
                        lPos += 2 ' Skip escaped quote
                    Else
                        ' Found closing quote
                        Return lPos
                    End If
                Else
                    lPos += 1
                End If
            End While
            
            ' No closing quote found
            Return -1
        End Function
        
        ''' <summary>
        ''' Replace identifier in line at specific column
        ''' </summary>
        Private Function ReplaceIdentifierInLine(vLine As String, vColumn As Integer, vOldName As String, vNewName As String) As String
            Try
                If vColumn < 0 OrElse vColumn >= vLine.Length Then Return vLine
                
                ' Extract the word at the position
                Dim lStart As Integer = vColumn
                Dim lEnd As Integer = vColumn
                
                ' Find word boundaries
                While lStart > 0 AndAlso (Char.IsLetterOrDigit(vLine(lStart - 1)) OrElse vLine(lStart - 1) = "_"c)
                    lStart -= 1
                End While
                
                While lEnd < vLine.Length AndAlso (Char.IsLetterOrDigit(vLine(lEnd)) OrElse vLine(lEnd) = "_"c)
                    lEnd += 1
                End While
                
                ' Replace the identifier
                Dim lBefore As String = vLine.Substring(0, lStart)
                Dim lAfter As String = If(lEnd < vLine.Length, vLine.Substring(lEnd), "")
                
                Return lBefore & vNewName & lAfter
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceIdentifierInLine error: {ex.Message}")
                Return vLine
            End Try
        End Function
        
        ''' <summary>
        ''' Update identifier in file on disk
        ''' </summary>
        Private Sub UpdateIdentifierInFile(vFilePath As String, vReferences As List(Of ReferenceInfo), vOldName As String, vNewName As String)
            Try
                If Not IO.File.Exists(vFilePath) Then Return
                
                Dim lContent As String = IO.File.ReadAllText(vFilePath)
                Dim lLines() As String = lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                
                ' Sort references by line and column (reverse order to maintain positions)
                Dim lSortedRefs = vReferences.OrderByDescending(Function(r) r.Line).ThenByDescending(Function(r) r.Column)
                
                For Each lRef In lSortedRefs
                    If lRef.Line >= 0 AndAlso lRef.Line < lLines.Length Then
                        lLines(lRef.Line) = ReplaceIdentifierInLine(lLines(lRef.Line), lRef.Column, vOldName, vNewName)
                    End If
                Next
                
                ' Write back
                IO.File.WriteAllText(vFilePath, String.Join(Environment.NewLine, lLines))
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierInFile error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Reset multi-line string tracking for a file (kept for compatibility)
        ''' </summary>
        Public Sub ResetMultiLineStringTracking(vFilePath As String)
            ' No longer needed with the new approach
        End Sub
        
    End Class
    
End Namespace
