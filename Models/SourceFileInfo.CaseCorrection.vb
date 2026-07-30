 ' Models/SourceFileInfo.CaseCorrection.vb - Parsing integration with ProjectManager
' Created: 2025-01-10
Imports System
Imports System.Text
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports SimpleIDE.Syntax
Imports SimpleIDE.Managers

Namespace Models
    
    Partial Public Class SourceFileInfo


        ''' <summary>
        ''' Updates a line with automatic case correction for keywords and identifiers
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vNewText">The new text for the line</param>
        ''' <remarks>
        ''' Automatically corrects keyword and identifier casing as part of text storage.
        ''' This is a language feature, not a parsing concern.
        ''' </remarks>
        Public Sub UpdateTextLineWithCaseCorrection(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then Return
                
                Dim vNewText As String = TextLines(vLineIndex)

                ' Apply case corrections if enabled
                Dim lCorrectedText As String = vNewText
                lCorrectedText = ApplyCaseCorrection(vNewText, vLineIndex)
                
                ' Update the line
                TextLines(vLineIndex) = lCorrectedText
                
                ' Mark as modified and needing parse
                IsModified = True
                NeedsParsing = True
                MarkLineChanged(vLineIndex)
                
                ' Notify listeners
                RaiseEvent ContentChanged(Me, EventArgs.Empty)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateTextLineWithCaseCorrection error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Applies case correction to a line of text
        ''' </summary>
        ''' <param name="vText">The text to correct</param>
        ''' <param name="vLineIndex">Line index for context</param>
        ''' <returns>Text with corrected casing</returns>
        Private Function ApplyCaseCorrection(vText As String, vLineIndex As Integer) As String
            Try
                ' Preserve original indentation
                Dim lIndentLength As Integer = vText.Length - vText.TrimStart().Length
                Dim lIndent As String = vText.Substring(0, lIndentLength)
                Dim lContent As String = vText.Substring(lIndentLength)
                
                ' Don't process empty lines or comments
                If String.IsNullOrWhiteSpace(lContent) OrElse lContent.TrimStart().StartsWith("'") Then
                    Return vText
                End If
                
                ' Simple word-based correction (avoids complex parsing)
                Dim lWords As String() = SplitIntoWords(lContent)
                Dim lResult As New StringBuilder(lIndent)
                Dim lInString As Boolean = False
                Dim lLastPos As Integer = 0
                
                for each lWord in lWords
                    Dim lWordStart As Integer = lContent.IndexOf(lWord, lLastPos)
                    
                    ' Add any text before this word (operators, spaces, etc.)
                    If lWordStart > lLastPos Then
                        Dim lBetween As String = lContent.Substring(lLastPos, lWordStart - lLastPos)
                        lResult.Append(lBetween)
                        
                        ' Check if we entered a string
                        If lBetween.Contains("""") Then
                            lInString = Not lInString
                        End If
                    End If
                    
                    ' Apply correction if not in a string
                    If Not lInString Then
                        lResult.Append(CorrectWordCase(lWord))
                    Else
                        lResult.Append(lWord)
                    End If
                    
                    lLastPos = lWordStart + lWord.Length
                Next
                
                ' Add any remaining text
                If lLastPos < lContent.Length Then
                    lResult.Append(lContent.Substring(lLastPos))
                End If
                
                Return lResult.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyCaseCorrection error: {ex.Message}")
                Return vText ' Return original on error
            End Try
        End Function
        
        ''' <summary>
        ''' Corrects the case of a single word (keyword or identifier)
        ''' </summary>
        Private Function CorrectWordCase(vWord As String) As String
            Try
                ' Try keyword first
                If pKeywordCaseMap Is Nothing Then
                    InitializeKeywordCaseMap()
                End If
                
                Dim lCorrectCase As String = Nothing
                If pKeywordCaseMap.TryGetValue(vWord.ToLower(), lCorrectCase) Then
                    Return lCorrectCase
                End If
                
                ' Try identifier
                If pIdentifierCaseMap.TryGetValue(vWord.ToLower(), lCorrectCase) Then
                    Return lCorrectCase
                End If
                
                ' No correction needed
                Return vWord
                
            Catch ex As Exception
                Return vWord
            End Try
        End Function
        
        ''' <summary>
        ''' Updates the identifier case map from ProjectManager
        ''' </summary>
        ''' <param name="vIdentifierMap">Map of lowercase to proper case identifiers</param>
        Public Sub UpdateIdentifierCaseMap(vIdentifierMap As Dictionary(Of String, String))
            Try
                pIdentifierCaseMap.Clear()
                
                for each lKvp in vIdentifierMap
                    pIdentifierCaseMap(lKvp.Key.ToLower()) = lKvp.Value
                Next
                
               ' Console.WriteLine($"SourceFileInfo: Updated {pIdentifierCaseMap.Count} identifier cases")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseMap error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initializes the keyword case map with VB.NET keywords
        ''' </summary>
        Private Sub InitializeKeywordCaseMap()
            pKeywordCaseMap = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"AddHandler", "AddHandler"}, {"AddressOf", "AddressOf"}, {"Alias", "Alias"}, 
                {"and", "and"}, {"AndAlso", "AndAlso"}, {"As", "As"}, {"Boolean", "Boolean"}, 
                {"ByRef", "ByRef"}, {"Byte", "Byte"}, {"ByVal", "ByVal"}, {"Call", "Call"}, 
                {"Case", "Case"}, {"Catch", "Catch"}, {"CBool", "CBool"}, {"CByte", "CByte"}, 
                {"CChar", "CChar"}, {"CDate", "CDate"}, {"CDbl", "CDbl"}, {"CDec", "CDec"}, 
                {"Char", "Char"}, {"CInt", "CInt"}, {"Class", "Class"}, {"CLng", "CLng"}, 
                {"CObj", "CObj"}, {"Const", "Const"}, {"Continue", "Continue"}, 
                {"CSByte", "CSByte"}, {"CShort", "CShort"}, {"CSng", "CSng"}, {"CStr", "CStr"}, 
                {"CType", "CType"}, {"CUInt", "CUInt"}, {"CULng", "CULng"}, 
                {"CUShort", "CUShort"}, {"Date", "Date"}, {"Decimal", "Decimal"}, 
                {"Declare", "Declare"}, {"Default", "Default"}, {"Delegate", "Delegate"}, 
                {"Dim", "Dim"}, {"DirectCast", "DirectCast"}, {"Do", "Do"}, {"Double", "Double"}, 
                {"each", "each"}, {"Else", "Else"}, {"ElseIf", "ElseIf"}, {"End", "End"}, 
                {"Enum", "Enum"}, {"Erase", "Erase"}, {"error", "error"}, {"Event", "Event"}, 
                {"Exit", "Exit"}, {"False", "False"}, {"Finally", "Finally"}, {"for", "for"},
                {"Friend", "Friend"}, {"Function", "Function"}, {"Get", "Get"}, 
                {"GetType", "GetType"}, {"GetXMLNamespace", "GetXMLNamespace"}, 
                {"Global", "Global"}, {"GoSub", "GoSub"}, {"GoTo", "GoTo"}, {"Handles", "Handles"}, 
                {"If", "If"}, {"Implements", "Implements"}, {"Imports", "Imports"}, {"in", "in"}, 
                {"Inherits", "Inherits"}, {"Integer", "Integer"}, {"Interface", "Interface"}, 
                {"Is", "Is"}, {"IsNot", "IsNot"}, {"Let", "Let"}, {"Lib", "Lib"}, 
                {"Like", "Like"}, {"Long", "Long"}, {"Loop", "Loop"}, {"Me", "Me"}, 
                {"Mod", "Mod"}, {"Module", "Module"}, {"MustInherit", "MustInherit"}, 
                {"MustOverride", "MustOverride"}, {"MyBase", "MyBase"}, {"MyClass", "MyClass"}, 
                {"Namespace", "Namespace"}, {"Narrowing", "Narrowing"}, {"New", "New"}, 
                {"Next", "Next"}, {"Not", "Not"}, {"Nothing", "Nothing"}, 
                {"NotInheritable", "NotInheritable"}, {"NotOverridable", "NotOverridable"}, 
                {"Object", "Object"}, {"Of", "Of"}, {"On", "On"}, {"Operator", "Operator"}, 
                {"Option", "Option"}, {"Optional", "Optional"}, {"Or", "Or"}, {"OrElse", "OrElse"}, 
                {"Overloads", "Overloads"}, {"Overridable", "Overridable"}, 
                {"Overrides", "Overrides"}, {"ParamArray", "ParamArray"}, {"Partial", "Partial"}, 
                {"Private", "Private"}, {"Property", "Property"}, {"Protected", "Protected"}, 
                {"Public", "Public"}, {"RaiseEvent", "RaiseEvent"}, {"ReadOnly", "ReadOnly"}, 
                {"ReDim", "ReDim"}, {"REM", "REM"}, {"RemoveHandler", "RemoveHandler"}, 
                {"Resume", "Resume"}, {"Return", "Return"}, {"SByte", "SByte"}, 
                {"Select", "Select"}, {"Set", "Set"}, {"Shadows", "Shadows"}, 
                {"Shared", "Shared"}, {"Short", "Short"}, {"Single", "Single"}, 
                {"Static", "Static"}, {"Step", "Step"}, {"Stop", "Stop"}, {"String", "String"}, 
                {"Structure", "Structure"}, {"Sub", "Sub"}, {"SyncLock", "SyncLock"}, 
                {"Then", "Then"}, {"Throw", "Throw"}, {"To", "To"}, {"True", "True"}, 
                {"Try", "Try"}, {"TryCast", "TryCast"}, {"TypeOf", "TypeOf"}, {"UInteger", "UInteger"}, 
                {"ULong", "ULong"}, {"UShort", "UShort"}, {"Using", "Using"}, {"Variant", "Variant"}, 
                {"Wend", "Wend"}, {"When", "When"}, {"While", "While"}, {"Widening", "Widening"}, 
                {"with", "with"}, {"WithEvents", "WithEvents"}, {"WriteOnly", "WriteOnly"}, 
                {"Xor", "Xor"}
            }
        End Sub
        
        ''' <summary>
        ''' Simple word splitter that preserves positions
        ''' </summary>
        Private Function SplitIntoWords(vText As String) As String()
            Dim lWords As New List(Of String)
            Dim lCurrentWord As New StringBuilder()
            
            for each lChar in vText
                If Char.IsLetterOrDigit(lChar) OrElse lChar = "_"c Then
                    lCurrentWord.Append(lChar)
                Else
                    If lCurrentWord.Length > 0 Then
                        lWords.Add(lCurrentWord.ToString())
                        lCurrentWord.Clear()
                    End If
                End If
            Next
            
            If lCurrentWord.Length > 0 Then
                lWords.Add(lCurrentWord.ToString())
            End If
            
            Return lWords.ToArray()
        End Function

        ''' <summary>
        ''' Marks a line as changed, requiring re-parsing
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        Private Sub MarkLineChanged(vLineIndex As Integer)
            Try
                If LineMetadata IsNot Nothing AndAlso vLineIndex < LineMetadata.Length Then
                    ' Use the safe GetLineMetadata method
                    Dim lMetadata As LineMetadata = GetLineMetadata(vLineIndex)
                    lMetadata.MarkChanged()
                    
                    ' Notify that content has changed
                    IsModified = True
                    NeedsParsing = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MarkLineChanged error: {ex.Message}")
            End Try
        End Sub


    End Class

End Namespace
