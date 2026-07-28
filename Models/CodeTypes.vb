' Models/CodeTypes.vb - Complete type definitions for code parsing and navigation
Imports System
Imports System.Collections.Generic

Namespace Models
    
    ' Code object types (class, module, etc.)
    Public Enum CodeObjectType
        eUnspecified
        eClass
        eModule
        eInterface
        eStructure
        eEnum
        eLastValue
    End Enum
    
    ' Code member types (method, property, etc.)
    Public Enum CodeMemberType
        eUnspecified
        eMethod
        eFunction
        eProperty
        eField
        eEvent
        eConstructor
        eLastValue
    End Enum
    
    ' Code template types for generation
    Public Enum CodeTemplateType
        eUnspecified
        eClass
        eModule
        eInterface
        eStructure
        eEnum
        eMethod
        eFunction
        eProperty
        eField
        eEvent
        eConstructor
        eEventHandler
        eLastValue
    End Enum
    
    ' Code insertion locations
    Public Enum CodeInsertLocation
        eUnspecified
        eAtCursor
        eEndOfClass
        eEndOfModule
        eEndOfFile
        eBeforeMethod
        eAfterMethod
        eLastValue
    End Enum
    
    ' Code node types for document graph
    Public Enum CodeNodeType
        eUnspecified
        eProject
        eNamespace
        eClass
        eModule
        eInterface
        eImport
        eStructure
        eDelegate
        eDocument
        eEnum
        eMethod
        eFunction
        eProperty
        eComment
        eRegion
        eField
        eConst
        eEvent
        eConstructor
        eParameter
        eVariable
        eOperator
        eEnumValue
        eFile
        eDeclare
        ''' <summary>Get accessor block of a property</summary>
        eGetAccessor
        ''' <summary>Set accessor block of a property</summary>
        eSetAccessor
        eLastValue
    End Enum
    
    ' Code object (class, module, etc.)
    Public Class CodeObject
        Public Property Name As String = ""
        Public Property ObjectType As CodeObjectType = CodeObjectType.eUnspecified
        Public Property StartLine As Integer = 0      ' 1-based Line number
        Public Property EndLine As Integer = 0        ' 1-based Line number
        Public Property StartColumn As Integer = 0
        Public Property EndColumn As Integer = 0
        Public Property members As New List(Of CodeMember)()
        Public Property ParentNamespace As String = ""
        Public Property Modifiers As New List(Of String)()
        Public Property InheritsFrom As String = ""
        Public Property ImplementsList As New List(Of String)()
        Public Property LineNumber As Integer
        
        Public ReadOnly Property DisplayText As String
            Get
                Select Case ObjectType
                    Case CodeObjectType.eClass
                        Return $"Class {Name}"
                    Case CodeObjectType.eModule
                        Return $"Module {Name}"
                    Case CodeObjectType.eInterface
                        Return $"Interface {Name}"
                    Case CodeObjectType.eStructure
                        Return $"Structure {Name}"
                    Case CodeObjectType.eEnum
                        Return $"Enum {Name}"
                    Case Else
                        Return Name
                End Select
            End Get
        End Property
    End Class
    
    ' Code member (method, property, etc.)
    Public Class CodeMember
        Public Property Name As String = ""
        Public Property MemberType As CodeMemberType = CodeMemberType.eUnspecified
        Public Property StartLine As Integer = 0      ' 1-based Line number
        Public Property EndLine As Integer = 0        ' 1-based Line number
        Public Property StartColumn As Integer = 0
        Public Property EndColumn As Integer = 0
        Public Property Parameters As String = ""
        Public Property ReturnType As String = ""
        Public Property ParentClass As String = ""
        Public Property Modifiers As New List(Of String)()
        Public Property IsShared As Boolean = False
        Public Property IsOverridable As Boolean = False
        Public Property IsOverrides As Boolean = False
        Public Property LineNumber As Integer
    
        ' FIXED: Removed "Type" property as it's a reserved keyword
        ' Use MemberType property directly instead
        
        
        Public ReadOnly Property DisplayText As String
            Get
                Select Case MemberType
                    Case CodeMemberType.eMethod
                        Return $"Sub {Name}({Parameters})"
                    Case CodeMemberType.eFunction
                        Dim lReturnInfo As String = If(String.IsNullOrEmpty(ReturnType), "", $" As {ReturnType}")
                        Return $"Function {Name}({Parameters}){lReturnInfo}"
                    Case CodeMemberType.eProperty
                        Dim lTypeInfo As String = If(String.IsNullOrEmpty(ReturnType), "", $" As {ReturnType}")
                        Return $"Property {Name}{lTypeInfo}"
                    Case CodeMemberType.eField
                        Dim lTypeInfo As String = If(String.IsNullOrEmpty(ReturnType), "", $" As {ReturnType}")
                        Return $"{Name}{lTypeInfo}"
                    Case CodeMemberType.eEvent
                        Return $"Event {Name}"
                    Case CodeMemberType.eConstructor
                        Return $"New({Parameters})"
                    Case Else
                        Return Name
                End Select
            End Get
        End Property
    End Class
    
'    ' Syntax highlight token
'    Public Class SyntaxToken
'        Public Property StartOffset As Integer = 0
'        Public Property EndOffset As Integer = 0
'        Public Property TokenType As SyntaxTokenType = SyntaxTokenType.eNormal
'        Public Property Text As String = ""
'    End Class
    
    ''' <summary>
    ''' Types of character tokens for syntax highlighting
    ''' </summary>
    Public Enum CharacterTokenType
        eText
        eKeyword
        eString
        eNumber
        eComment
        eOperator
        eIdentifier
        eType
        ePreprocessor
    End Enum
    
    ' Parse error information
    Public Class ParseError
        Public Property Message As String = ""
        Public Property Line As Integer = 0
        Public Property Column As Integer = 0
        Public Property Severity As ParseErrorSeverity = ParseErrorSeverity.eWarning
    End Class
    
    ' Parse error severity
    Public Enum ParseErrorSeverity
        eInfo
        eWarning
        eError
    End Enum

End Namespace