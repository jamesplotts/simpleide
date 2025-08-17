' Syntax/IntelliSenseEngine.vb - IntelliSense suggestion engine
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Reflection
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces

Namespace Syntax
    
    Public Class IntelliSenseEngine
        Implements IDisposable
        
        ' ===== Private Fields =====
        Private pProjectReferences As New List(Of Assembly)
        Private pDocumentNodes As Dictionary(Of String, DocumentNode)
        Private pRootNodes As List(Of DocumentNode)
        Private pTypeCache As New Dictionary(Of String, IntelliSenseTypeInfo)
        Private pMemberCache As New Dictionary(Of String, List(Of MemberInfo))
        Private pKeywordSuggestions As List(Of IntelliSenseSuggestion)
        Private pLastUpdateTime As DateTime = DateTime.MinValue
        Private pDisposed As Boolean = False
        
        ' ===== Constructor =====
        Public Sub New()
            Try
                ' Initialize keyword suggestions
                InitializeKeywordSuggestions()
                
                ' Load basic assemblies
                LoadCoreAssemblies()
                
            Catch ex As Exception
                Console.WriteLine($"IntelliSenseEngine constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ' Update the document nodes from an editor
        Public Sub UpdateDocumentNodes(vNodes As Dictionary(Of String, DocumentNode), vRootNodes As List(Of DocumentNode))
            Try
                pDocumentNodes = vNodes
                pRootNodes = vRootNodes
                pLastUpdateTime = DateTime.Now
                
                ' Clear member cache as document structure changed
                pMemberCache.Clear()
                
                Console.WriteLine($"IntelliSenseEngine updated with {vNodes.Count} Nodes")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateDocumentNodes error: {ex.Message}")
            End Try
        End Sub
        
        ' Add project reference assembly
        Public Sub AddReference(vAssemblyPath As String)
            Try
                Dim lAssembly As Assembly = Assembly.LoadFrom(vAssemblyPath)
                If Not pProjectReferences.Contains(lAssembly) Then
                    pProjectReferences.Add(lAssembly)
                    
                    ' Clear type cache to force reload
                    pTypeCache.Clear()
                    pMemberCache.Clear()
                    
                    Console.WriteLine($"Added Reference: {lAssembly.FullName}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddReference error: {ex.Message}")
            End Try
        End Sub
        
        ' Clear all references
        Public Sub ClearReferences()
            Try
                pProjectReferences.Clear()
                pTypeCache.Clear()
                pMemberCache.Clear()
                
            Catch ex As Exception
                Console.WriteLine($"ClearReferences error: {ex.Message}")
            End Try
        End Sub
        
        ' Get IntelliSense suggestions for context
        Public Function GetSuggestions(vContext As IntelliSenseContext) As List(Of IntelliSenseSuggestion)
            Dim lSuggestions As New List(Of IntelliSenseSuggestion)
            
            Try
                If vContext Is Nothing Then Return lSuggestions
                
                ' Determine suggestion type based on context
                If vContext.IsMemberAccess Then
                    ' Member access (after dot)
                    lSuggestions.AddRange(GetMemberSuggestions(vContext))
                ElseIf vContext.IsParameterContext Then
                    ' Parameter hints
                    lSuggestions.AddRange(GetParameterHints(vContext))
                Else
                    ' Contextual suggestions
                    lSuggestions.AddRange(GetContextualSuggestions(vContext))
                End If
                
                ' Sort suggestions by priority
                lSuggestions = lSuggestions.OrderByDescending(Function(s) GetSuggestionPriority(s)).ToList()
                
                Return lSuggestions
                
            Catch ex As Exception
                Console.WriteLine($"GetSuggestions error: {ex.Message}")
                Return lSuggestions
            End Try
        End Function
        
        ' ===== Private Methods - Initialization =====
        
        Private Sub InitializeKeywordSuggestions()
            Try
                pKeywordSuggestions = New List(Of IntelliSenseSuggestion)
                
                ' Add VB.NET keywords
                For Each lKeyword In VBLanguageDefinition.Keywords
                    pKeywordSuggestions.Add(New IntelliSenseSuggestion() With {
                        .Text = lKeyword,
                        .DisplayText = lKeyword,
                        .Description = $"VB.NET keyword: {lKeyword}",
                        .Icon = "keyword",
                        .SuggestionType = IntelliSenseSuggestionType.eKeyword
                    })
                Next
                
            Catch ex As Exception
                Console.WriteLine($"InitializeKeywordSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub LoadCoreAssemblies()
            Try
                ' Load basic .NET assemblies
                pProjectReferences.Add(GetType(String).Assembly)        ' mscorlib
                pProjectReferences.Add(GetType(Uri).Assembly)           ' System
                pProjectReferences.Add(GetType(Enumerable).Assembly)    ' System.Core
                
            Catch ex As Exception
                Console.WriteLine($"LoadCoreAssemblies error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods - Type Information =====
        
        Private Function GetTypeInfo(vTypeName As String) As IntelliSenseTypeInfo
            Try
                ' Check cache first
                If pTypeCache.ContainsKey(vTypeName) Then
                    Return pTypeCache(vTypeName)
                End If
                
                ' Search in document nodes first
                Dim lNode As DocumentNode = FindNodeByName(vTypeName)
                If lNode IsNot Nothing Then
                    Dim lInfo As New IntelliSenseTypeInfo(
                        lNode.Name,
                        lNode.FullName,
                        GetNodeNamespace(lNode),
                        lNode.NodeType = CodeNodeType.eClass,
                        lNode.NodeType = CodeNodeType.eInterface,
                        lNode.NodeType = CodeNodeType.eStructure,
                        lNode.NodeType = CodeNodeType.eEnum
                    )
                    pTypeCache(vTypeName) = lInfo
                    Return lInfo
                End If
                
                ' Search in referenced assemblies
                Dim lType As Type = FindTypeInAssemblies(vTypeName)
                If lType IsNot Nothing Then
                    ' Use ReflectionHelper to get type info and convert
                    Dim lReflectionInfo As ReflectionHelper.TypeInfo = ReflectionHelper.GetTypeInfo(lType)
                    Dim lInfo As New IntelliSenseTypeInfo(
                        lReflectionInfo.Name,
                        lReflectionInfo.FullName,
                        lReflectionInfo.TypeNamespace,
                        lType.IsClass,
                        lType.IsInterface,
                        lType.IsValueType,
                        lType.IsEnum
                    )
                    lInfo.Type = lType
                    pTypeCache(vTypeName) = lInfo
                    Return lInfo
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetTypeInfo error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' ===== Private Methods - Suggestion Generation =====
        
        Private Function GetMemberSuggestions(vContext As IntelliSenseContext) As List(Of IntelliSenseSuggestion)
            Dim lSuggestions As New List(Of IntelliSenseSuggestion)
            
            Try
                ' Get the type of the expression before the dot
                Dim lTypeName As String = GetExpressionType(vContext.MemberAccessTarget)
                If String.IsNullOrEmpty(lTypeName) Then Return lSuggestions
                
                ' Get type info
                Dim lTypeInfo As IntelliSenseTypeInfo = GetTypeInfo(lTypeName)
                If lTypeInfo Is Nothing Then Return lSuggestions
                
                ' Check if it's a type from document nodes
                Dim lNode As DocumentNode = FindNodeByName(lTypeName)
                If lNode IsNot Nothing Then
                    ' Add members from document node
                    For Each lChild In lNode.Children
                        If IsAccessibleMember(lChild, vContext) Then
                            lSuggestions.Add(CreateSuggestionFromNode(lChild))
                        End If
                    Next
                End If
                
                ' Add members from reflection if it's a .NET type
                If lTypeInfo.Type IsNot Nothing Then
                    Dim lMembers As MemberInfo() = lTypeInfo.Type.GetMembers(
                        BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static)
                    
                    For Each lMember In lMembers
                        If ShouldIncludeMember(lMember) Then
                            lSuggestions.Add(CreateSuggestionFromMember(lMember))
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetMemberSuggestions error: {ex.Message}")
            End Try
            
            Return lSuggestions
        End Function
        
        Private Function GetContextualSuggestions(vContext As IntelliSenseContext) As List(Of IntelliSenseSuggestion)
            Dim lSuggestions As New List(Of IntelliSenseSuggestion)
            
            Try
                ' Analyze the context to determine what to suggest
                Dim lLastKeyword As String = GetLastKeyword(vContext.LineText)
                
                Select Case lLastKeyword.ToLower()
                    Case "imports"
                        ' Suggest namespaces
                        lSuggestions.AddRange(GetNamespaceSuggestions())
                        
                    Case "inherits", "implements"
                        ' Suggest base types
                        lSuggestions.AddRange(GetTypeSuggestions(lLastKeyword = "implements"))
                        
                    Case "as", "of"
                        ' Suggest types
                        lSuggestions.AddRange(GetTypeSuggestions(False))
                        
                    Case "New"
                        ' Suggest constructible types
                        lSuggestions.AddRange(GetConstructibleTypeSuggestions())
                        
                    Case Else
                        ' At statement level - suggest keywords and types
                        If IsAtStatementLevel(vContext) Then
                            lSuggestions.AddRange(GetStatementLevelSuggestions(vContext))
                        Else
                            lSuggestions.AddRange(GetExpressionLevelSuggestions(vContext))
                        End If
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetContextualSuggestions error: {ex.Message}")
            End Try
            
            Return lSuggestions
        End Function
        
        Private Function GetParameterHints(vContext As IntelliSenseContext) As List(Of IntelliSenseSuggestion)
            ' TODO: Implement parameter hints
            Return New List(Of IntelliSenseSuggestion)
        End Function
        
        Private Function GetStatementLevelSuggestions(vContext As IntelliSenseContext) As List(Of IntelliSenseSuggestion)
            Dim lSuggestions As New List(Of IntelliSenseSuggestion)
            
            Try
                ' Statement level keywords
                Dim lStatementKeywords() As String = {
                    "Dim", "Public", "Private", "Protected", "Friend", "Shared",
                    "If", "for", "While", "Do", "Select", "Try", "Using",
                    "Class", "Module", "Structure", "Interface", "Enum",
                    "Function", "Sub", "Property"
                }
                
                For Each lKeyword In lStatementKeywords
                    lSuggestions.Add(New IntelliSenseSuggestion() With {
                        .Text = lKeyword,
                        .DisplayText = lKeyword,
                        .Description = $"VB.NET keyword: {lKeyword}",
                        .Icon = "keyword",
                        .SuggestionType = IntelliSenseSuggestionType.eKeyword
                    })
                Next
                
                ' Add common types
                lSuggestions.AddRange(GetCommonTypeSuggestions())
                
                ' Add local suggestions if available
                lSuggestions.AddRange(GetLocalSuggestions(vContext))
                
            Catch ex As Exception
                Console.WriteLine($"GetStatementLevelSuggestions error: {ex.Message}")
            End Try
            
            Return lSuggestions
        End Function
        
        Private Function GetExpressionLevelSuggestions(vContext As IntelliSenseContext) As List(Of IntelliSenseSuggestion)
            Dim lSuggestions As New List(Of IntelliSenseSuggestion)
            
            Try
                ' Add variables and members
                lSuggestions.AddRange(GetLocalSuggestions(vContext))
                
                ' Add common expression keywords
                Dim lExpressionKeywords() As String = {
                    "and", "Or", "Not", "AndAlso", "OrElse", "Xor",
                    "Is", "IsNot", "Like", "TypeOf", "GetType",
                    "True", "False", "Nothing", "Me", "MyBase", "MyClass"
                }
                
                For Each lKeyword In lExpressionKeywords
                    lSuggestions.Add(New IntelliSenseSuggestion() With {
                        .Text = lKeyword,
                        .DisplayText = lKeyword,
                        .Description = $"VB.NET keyword: {lKeyword}",
                        .Icon = "keyword",
                        .SuggestionType = IntelliSenseSuggestionType.eKeyword
                    })
                Next
                
            Catch ex As Exception
                Console.WriteLine($"GetExpressionLevelSuggestions error: {ex.Message}")
            End Try
            
            Return lSuggestions
        End Function
        
        Private Function GetLocalSuggestions(vContext As IntelliSenseContext) As List(Of IntelliSenseSuggestion)
            Dim lSuggestions As New List(Of IntelliSenseSuggestion)
            
            Try
                ' Add from context if available
                If vContext.LocalVariables IsNot Nothing Then
                    For Each lVar In vContext.LocalVariables
                        lSuggestions.Add(New IntelliSenseSuggestion() With {
                            .Text = lVar,
                            .DisplayText = lVar,
                            .Description = $"Local variable: {lVar}",
                            .Icon = "variable",
                            .SuggestionType = IntelliSenseSuggestionType.eVariable
                        })
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetLocalSuggestions error: {ex.Message}")
            End Try
            
            Return lSuggestions
        End Function
        
        ' ===== Helper Methods =====
        
        Private Function IsAtStatementLevel(vContext As IntelliSenseContext) As Boolean
            ' Simple heuristic: if line is mostly whitespace before cursor, we're at statement level
            Return String.IsNullOrWhiteSpace(vContext.LineText.Trim())
        End Function
        
        Private Function GetSuggestionPriority(vSuggestion As IntelliSenseSuggestion) As Integer
            ' Prioritize suggestions based on type
            Select Case vSuggestion.SuggestionType
                Case IntelliSenseSuggestionType.eKeyword
                    Return 100
                Case IntelliSenseSuggestionType.eMethod, IntelliSenseSuggestionType.eProperty
                    Return 90
                Case IntelliSenseSuggestionType.eType
                    Return 80
                Case IntelliSenseSuggestionType.eVariable
                    Return 70
                Case Else
                    Return 50
            End Select
        End Function
        
        ' Additional helper methods (simplified for brevity)
        Private Function FindNodeByName(vName As String) As DocumentNode
            ' Implementation would search through pDocumentNodes
            Return Nothing
        End Function
        
        Private Function GetNodeNamespace(vNode As DocumentNode) As String
            ' Implementation would extract namespace from node
            Return ""
        End Function
        
        Private Function FindTypeInAssemblies(vTypeName As String) As Type
            ' Implementation would search through pProjectReferences
            Return Nothing
        End Function
        
        Private Function GetLastKeyword(vText As String) As String
            ' Implementation would parse the text for the last keyword
            Return ""
        End Function
        
        Private Function GetExpressionType(vExpression As String) As String
            ' Implementation would analyze the expression to determine its type
            Return ""
        End Function
        
        Private Function IsAccessibleMember(vNode As DocumentNode, vContext As IntelliSenseContext) As Boolean
            ' Implementation would check member accessibility
            Return True
        End Function
        
        Private Function ShouldIncludeMember(vMember As MemberInfo) As Boolean
            ' Implementation would filter unwanted members
            Return True
        End Function
        
        Private Function CreateSuggestionFromNode(vNode As DocumentNode) As IntelliSenseSuggestion
            ' Implementation would create suggestion from document node
            Return New IntelliSenseSuggestion()
        End Function
        
        Private Function CreateSuggestionFromMember(vMember As MemberInfo) As IntelliSenseSuggestion
            ' Implementation would create suggestion from member info
            Return New IntelliSenseSuggestion()
        End Function
        
        Private Function GetNamespaceSuggestions() As List(Of IntelliSenseSuggestion)
            Return New List(Of IntelliSenseSuggestion)
        End Function
        
        Private Function GetTypeSuggestions(vInterfacesOnly As Boolean) As List(Of IntelliSenseSuggestion)
            Return New List(Of IntelliSenseSuggestion)
        End Function
        
        Private Function GetConstructibleTypeSuggestions() As List(Of IntelliSenseSuggestion)
            Return New List(Of IntelliSenseSuggestion)
        End Function
        
        Private Function GetCommonTypeSuggestions() As List(Of IntelliSenseSuggestion)
            Return New List(Of IntelliSenseSuggestion)
        End Function
        
        ' ===== IDisposable Implementation =====
        
        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
        
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not pDisposed Then
                If disposing Then
                    ' Dispose managed resources
                    pProjectReferences?.Clear()
                    pDocumentNodes?.Clear()
                    pRootNodes?.Clear()
                    pTypeCache?.Clear()
                    pMemberCache?.Clear()
                    pKeywordSuggestions?.Clear()
                End If
                
                pDisposed = True
            End If
        End Sub
        
    End Class
    
    ' ===== Supporting Classes =====
    
    ' Internal type information class to avoid conflict with ReflectionHelper.TypeInfo
    Friend Class IntelliSenseTypeInfo
        Public ReadOnly Property Name As String
        Public ReadOnly Property FullName As String
        Public ReadOnly Property [Namespace] As String
        Public ReadOnly Property IsClass As Boolean
        Public ReadOnly Property IsInterface As Boolean
        Public ReadOnly Property IsValueType As Boolean
        Public ReadOnly Property IsEnum As Boolean
        Public Property Type As Type
        
        Public Sub New(vName As String, vFullName As String, vNamespace As String, 
                       vIsClass As Boolean, vIsInterface As Boolean, 
                       vIsValueType As Boolean, vIsEnum As Boolean)
            Name = vName
            FullName = vFullName
            [Namespace] = vNamespace
            IsClass = vIsClass
            IsInterface = vIsInterface
            IsValueType = vIsValueType
            IsEnum = vIsEnum
        End Sub
    End Class
    
    ' Types of IntelliSense triggers
    Public Enum IntelliSenseTriggerType
        eUnspecified
        eDot            ' After "."
        eSpace          ' After space (contextual)
        eOpenParen      ' After "("
        eComma          ' After ","
        eManual         ' Ctrl+Space
        eLastValue
    End Enum

    
    ' IntelliSense suggestion kind enum (for compatibility)
    Public Enum IntelliSenseSuggestionKind
        eUnspecified
        eKeyword
        eClass
        eInterface
        eMethod
        eProperty
        eField
        eEvent
        eNamespace
        eLocalVariable
        eParameter
        eSnippet
        eOther
        eLastValue
    End Enum
    
    ' Types of IntelliSense suggestions
    Public Enum IntelliSenseSuggestionType
        eUnspecified
        eKeyword
        eType
        eNamespace
        eMethod
        eProperty
        eField
        eEvent
        eVariable
        eParameter
        eSnippet
        eOther
        eLastValue
    End Enum
    
End Namespace
