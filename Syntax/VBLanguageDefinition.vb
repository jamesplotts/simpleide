 
' Models/VBLanguageDefinition.vb - VB.NET language keywords and types definition
Imports System.Collections.Generic

Namespace Syntax
    Public Class VBLanguageDefinition
        
        ' VB.NET Keywords (organized by category)
        Public Shared ReadOnly Property Keywords As String()
            Get
                Return _allKeywords
            End Get
        End Property
        
        ' Declaration keywords
        Private Shared ReadOnly _declarationKeywords As String() = {
            "Class", "Structure", "Module", "Interface", "Enum", "Delegate",
            "Function", "Sub", "Property", "Event", "Const", "Dim",
            "Private", "Public", "Protected", "Friend", "Shared", "Static",
            "ReadOnly", "WriteOnly", "WithEvents", "Shadows", "Overloads",
            "Overrides", "Overridable", "NotOverridable", "MustOverride",
            "MustInherit", "NotInheritable", "Partial", "Implements", "Inherits",
            "Get", "Set", "Let", "Declare", "Operator", "Widening", "Narrowing",
            "Default", "ParamArray", "Optional", "ByVal", "ByRef"
        }
        
        ' Control flow keywords
        Private Shared ReadOnly _controlFlowKeywords As String() = {
            "If", "Then", "Else", "ElseIf", "EndIf", "Select", "Case",
            "for", "To", "Step", "Next", "each", "in", "While", "Wend",
            "Do", "Loop", "Until", "Exit", "Continue", "GoTo", "GoSub",
            "Return", "Throw", "Try", "Catch", "Finally", "When", "with",
            "Using", "SyncLock"
        }
        
        ' Type keywords
        Private Shared ReadOnly _typeKeywords As String() = {
            "Boolean", "Byte", "Char", "Date", "Decimal", "Double",
            "Integer", "Long", "Object", "SByte", "Short", "Single",
            "String", "UInteger", "ULong", "UShort", "Variant"
        }
        
        ' Operator keywords
        Private Shared ReadOnly _operatorKeywords As String() = {
            "and", "AndAlso", "Or", "OrElse", "Not", "Xor",
            "Is", "IsNot", "Like", "TypeOf", "GetType",
            "Mod", "New", "Nothing", "True", "False",
            "Me", "MyBase", "MyClass"
        }
        
        ' Conversion keywords
        Private Shared ReadOnly _conversionKeywords As String() = {
            "CBool", "CByte", "CChar", "CDate", "CDbl", "CDec",
            "CInt", "CLng", "CObj", "CSByte", "CShort", "CSng",
            "CStr", "CType", "CUInt", "CULng", "CUShort",
            "DirectCast", "TryCast", "GetXMLNamespace"
        }
        
        ' Other keywords
        Private Shared ReadOnly _otherKeywords As String() = {
            "AddHandler", "RemoveHandler", "RaiseEvent", "Handles",
            "AddressOf", "Alias", "As", "Call", "End", "Erase",
            "error", "Global", "Imports", "Lib", "Namespace",
            "Of", "On", "Option", "ReDim", "REM", "Resume",
            "Stop"
        }
        
        ' All keywords combined
        Private Shared ReadOnly _allKeywords As String() = CombineArrays(
            _declarationKeywords,
            _controlFlowKeywords,
            _typeKeywords,
            _operatorKeywords,
            _conversionKeywords,
            _otherKeywords
        )
        
        ' Common .NET Framework types and classes
        Public Shared ReadOnly Property CommonTypes As String()
            Get
                Return _commonTypes
            End Get
        End Property
        
        Private Shared ReadOnly _commonTypes As String() = { _
            _ ' System namespace
            "System", "Console", "Math", "Convert", "DateTime", "TimeSpan", _
            "Exception", "ArgumentException", "InvalidOperationException", _
            "NotImplementedException", "ArgumentNullException", "IndexOutOfRangeException", _
            "Environment", "Version", "Guid", "Random", "GC", _
            _ ' Collections
            "Array", "List", "Dictionary", "HashSet", "Queue", "Stack", _
            "LinkedList", "SortedList", "SortedDictionary", "Collection", _
            "ArrayList", "Hashtable", _
            _ ' IO
            "File", "Directory", "Path", "FileInfo", "DirectoryInfo", _
            "StreamReader", "StreamWriter", "FileStream", "MemoryStream", _
            "BinaryReader", "BinaryWriter", "TextReader", "TextWriter", _
            _ ' Text
            "StringBuilder", "Regex", "Match", "MatchCollection", _
            "Encoding", "UTF8Encoding", "ASCIIEncoding", _
            _ ' Threading
            "Thread", "Task", "ThreadPool", "Monitor", "Mutex", _
            "Semaphore", "ManualResetEvent", "AutoResetEvent", _
            "Interlocked", "Volatile", _
            _ ' LINQ
            "Enumerable", "Queryable", "IEnumerable", "IQueryable", _
            _ ' Reflection
            "Type", "MethodInfo", "PropertyInfo", "FieldInfo", _
            "Assembly", "Attribute", _
            _ ' Diagnostics
            "Debug", "Trace", "Process", "Stopwatch", "EventLog" _
        }
        
        ' Preprocessor directives
        Public Shared ReadOnly Property PreprocessorDirectives As String()
            Get
                Return _preprocessorDirectives
            End Get
        End Property
        
        Private Shared ReadOnly _preprocessorDirectives As String() = {
            "#If", "#ElseIf", "#Else", "#End If",
            "#Const", "#Region", "#End Region",
            "#ExternalSource", "#End ExternalSource",
            "#Disable", "#Enable"
        }
        
        ' Get all operators for syntax highlighting
        Public Shared ReadOnly Property Operators As String()
            Get
                Return _operators
            End Get
        End Property
        
        Private Shared ReadOnly _operators As String() = {
            "+", "-", "*", "/", "\", "^", "=", "<>",
            "<", ">", "<=", ">=", "&", "+=", "-=",
            "*=", "/=", "\=", "^=", "&=", "<<", ">>"
        }
        
        ' Number formats
        Public Shared ReadOnly Property NumberPatterns As String()
            Get
                Return {
                    "\b\d+[LlSsIiUu]*\b",              ' Integer literals
                    "\b\d*\.\d+[FfDdRr]*\b",           ' Floating point literals
                    "&H[0-9A-Fa-f]+[LlSsIiUu]*\b",    ' Hexadecimal literals
                    "&O[0-7]+[LlSsIiUu]*\b",           ' Octal literals
                    "&b[01]+[LlSsIiUu]*\b"             ' Binary literals (VB.NET 15+)
                }
            End Get
        End Property
        
        ' String patterns
        Public Shared ReadOnly Property StringPatterns As String()
            Get
                Return {
                    """([^""]|"""")*""",               ' Regular strings
                    """[^""]""c"                       ' Character literals
                }
            End Get
        End Property
        
        ' Comment patterns
        Public Shared ReadOnly Property CommentPatterns As String()
            Get
                Return {
                    "'.*$",                            ' Single Line comment
                    "\bREM\s+.*$"                      ' REM comment
                }
            End Get
        End Property
        
        ' Helper method to combine arrays
        Private Shared Function CombineArrays(ParamArray arrays()() As String) As String()
            Dim lResult As New List(Of String)
            For Each arr In arrays
                lResult.AddRange(arr)
            Next
            Return lResult.ToArray()
        End Function
        
        ' Check if a word is a keyword
        Public Shared Function IsKeyword(vWord As String) As Boolean
            Return Keywords.any(Function(k) String.Equals(k, vWord, StringComparison.OrdinalIgnoreCase))
        End Function
        
        ' Check if a word is a common type
        Public Shared Function IsCommonType(vWord As String) As Boolean
            Return CommonTypes.any(Function(t) String.Equals(t, vWord, StringComparison.OrdinalIgnoreCase))
        End Function
        
        ' Get keyword category
        Public Shared Function GetKeywordCategory(vKeyword As String) As String
            If _declarationKeywords.any(Function(k) String.Equals(k, vKeyword, StringComparison.OrdinalIgnoreCase)) Then
                Return "Declaration"
            ElseIf _controlFlowKeywords.any(Function(k) String.Equals(k, vKeyword, StringComparison.OrdinalIgnoreCase)) Then
                Return "ControlFlow"
            ElseIf _typeKeywords.any(Function(k) String.Equals(k, vKeyword, StringComparison.OrdinalIgnoreCase)) Then
                Return "Type"
            ElseIf _operatorKeywords.any(Function(k) String.Equals(k, vKeyword, StringComparison.OrdinalIgnoreCase)) Then
                Return "Operator"
            ElseIf _conversionKeywords.any(Function(k) String.Equals(k, vKeyword, StringComparison.OrdinalIgnoreCase)) Then
                Return "Conversion"
            ElseIf _otherKeywords.any(Function(k) String.Equals(k, vKeyword, StringComparison.OrdinalIgnoreCase)) Then
                Return "Other"
            Else
                Return "Unknown"
            End If
        End Function
    End Class
End Namespace
