' ProjectSyntaxTree.vb - Represents the complete parsed structure of a VB.NET project
' Part of the Roslyn parser replacement
' Created: 2025-01-01

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.CodeAnalysis
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Parsers

' Add this alias:
Imports SimpleSyntaxNode = SimpleIDE.Syntax.SyntaxNode

Namespace Managers
    
    ''' <summary>
    ''' Represents the complete parsed structure of a VB.NET project
    ''' </summary>
    Public Class ProjectSyntaxTree
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets or sets the root namespace of the project
        ''' </summary>
        Public Property RootNamespace As String
        
        ''' <summary>
        ''' Gets or sets the assembly name
        ''' </summary>
        Public Property AssemblyName As String
        
        ''' <summary>
        ''' Gets or sets the collection of parsed files
        ''' </summary>
        Public Property Files As Dictionary(Of String, FileSyntaxTree)
        
        ''' <summary>
        ''' Gets or sets the list of project references
        ''' </summary>
        Public Property ProjectReferences As List(Of String)
        
        ''' <summary>
        ''' Gets or sets the list of package references
        ''' </summary>
        Public Property PackageReferences As List(Of String)
        
        ''' <summary>
        ''' Gets or sets the root SyntaxNode of the unified tree
        ''' </summary>
        Public Property RootNode As SimpleSyntaxNode
        
        ''' <summary>
        ''' Gets or sets parse diagnostics for the project
        ''' </summary>
        Public Property Diagnostics As List(Of Diagnostic)
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Initializes a new instance of ProjectSyntaxTree
        ''' </summary>
        Public Sub New()
            Files = New Dictionary(Of String, FileSyntaxTree)()
            ProjectReferences = New List(Of String)()
            PackageReferences = New List(Of String)()
            Diagnostics = New List(Of Diagnostic)()
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Gets the unified SyntaxNode tree for the entire project
        ''' </summary>
        Public Function GetUnifiedTree() As SimpleSyntaxNode
            Return RootNode
        End Function
        
        ''' <summary>
        ''' Finds all types in the project
        ''' </summary>
        Public Function GetAllTypes() As IEnumerable(Of TypeInfo)
            Try
                Dim lTypes As New List(Of TypeInfo)
                
                If RootNode IsNot Nothing Then
                    CollectTypes(RootNode, lTypes)
                End If
                
                Return lTypes
                
            Catch ex As Exception
                Console.WriteLine($"GetAllTypes error: {ex.Message}")
                Return New List(Of TypeInfo)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all namespaces in the project
        ''' </summary>
        Public Function GetAllNamespaces() As IEnumerable(Of String)
            Try
                Dim lNamespaces As New HashSet(Of String)()
                
                If RootNode IsNot Nothing Then
                    CollectNamespaces(RootNode, lNamespaces)
                End If
                
                Return lNamespaces.OrderBy(Function(n) n)
                
            Catch ex As Exception
                Console.WriteLine($"GetAllNamespaces error: {ex.Message}")
                Return New List(Of String)()
            End Try
        End Function
        
        ''' <summary>
        ''' Finds a type by its fully qualified name
        ''' </summary>
        Public Function FindType(vFullyQualifiedName As String) As SimpleSyntaxNode
            Try
                If String.IsNullOrEmpty(vFullyQualifiedName) OrElse RootNode Is Nothing Then
                    Return Nothing
                End If
                
                Return FindTypeRecursive(RootNode, vFullyQualifiedName)
                
            Catch ex As Exception
                Console.WriteLine($"FindType error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all members of a specific type
        ''' </summary>
        Public Function GetTypeMembers(vTypeName As String) As IEnumerable(Of SimpleSyntaxNode)
            Try
                Dim lType = FindType(vTypeName)
                If lType IsNot Nothing Then
                    Return lType.Children
                End If
                
                Return New List(Of SyntaxNode)()
                
            Catch ex As Exception
                Console.WriteLine($"GetTypeMembers error: {ex.Message}")
                Return New List(Of SyntaxNode)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets diagnostics for a specific file
        ''' </summary>
        Public Function GetFileDiagnostics(vFilePath As String) As IEnumerable(Of Diagnostic)
            Try
                If Files.ContainsKey(vFilePath) Then
                    Return Files(vFilePath).Diagnostics
                End If
                
                Return New List(Of Diagnostic)()
                
            Catch ex As Exception
                Console.WriteLine($"GetFileDiagnostics error: {ex.Message}")
                Return New List(Of Diagnostic)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all errors in the project
        ''' </summary>
        Public Function GetErrors() As IEnumerable(Of Diagnostic)
            Try
                Dim lErrors As New List(Of Diagnostic)()
                
                ' Collect errors from all files
                for each lFile in Files.Values
                    lErrors.AddRange(lFile.Diagnostics.Where(
                        Function(d) d.Severity = DiagnosticSeverity.error
                    ))
                Next
                
                ' Add project-level errors
                lErrors.AddRange(Diagnostics.Where(
                    Function(d) d.Severity = DiagnosticSeverity.error
                ))
                
                Return lErrors
                
            Catch ex As Exception
                Console.WriteLine($"GetErrors error: {ex.Message}")
                Return New List(Of Diagnostic)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all warnings in the project
        ''' </summary>
        Public Function GetWarnings() As IEnumerable(Of Diagnostic)
            Try
                Dim lWarnings As New List(Of Diagnostic)()
                
                ' Collect warnings from all files
                for each lFile in Files.Values
                    lWarnings.AddRange(lFile.Diagnostics.Where(
                        Function(d) d.Severity = DiagnosticSeverity.Warning
                    ))
                Next
                
                ' Add project-level warnings
                lWarnings.AddRange(Diagnostics.Where(
                    Function(d) d.Severity = DiagnosticSeverity.Warning
                ))
                
                Return lWarnings
                
            Catch ex As Exception
                Console.WriteLine($"GetWarnings error: {ex.Message}")
                Return New List(Of Diagnostic)()
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if the project has any errors
        ''' </summary>
        Public Function HasErrors() As Boolean
            Return GetErrors().Any()
        End Function
        
        ''' <summary>
        ''' Checks if the project has any warnings
        ''' </summary>
        Public Function HasWarnings() As Boolean
            Return GetWarnings().Any()
        End Function
        
        ''' <summary>
        ''' Gets a summary of the project structure
        ''' </summary>
        Public Function GetSummary() As ProjectSummary
            Try
                Dim lSummary As New ProjectSummary()
                
                lSummary.FileCount = Files.Count
                lSummary.NamespaceCount = GetAllNamespaces().Count()
                
                Dim lTypes = GetAllTypes()
                lSummary.TypeCount = lTypes.Count()
                lSummary.ClassCount = lTypes.Count(Function(t) t.NodeType = CodeNodeType.eClass)
                lSummary.InterfaceCount = lTypes.Count(Function(t) t.NodeType = CodeNodeType.eInterface)
                lSummary.ModuleCount = lTypes.Count(Function(t) t.NodeType = CodeNodeType.eModule)
                lSummary.StructureCount = lTypes.Count(Function(t) t.NodeType = CodeNodeType.eStructure)
                lSummary.EnumCount = lTypes.Count(Function(t) t.NodeType = CodeNodeType.eEnum)
                
                lSummary.ErrorCount = GetErrors().Count()
                lSummary.WarningCount = GetWarnings().Count()
                
                ' Count total lines of code
                lSummary.TotalLines = Files.Values.Sum(Function(f) If(f.LineMetadata?.Length, 0))
                
                Return lSummary
                
            Catch ex As Exception
                Console.WriteLine($"GetSummary error: {ex.Message}")
                Return New ProjectSummary()
            End Try
        End Function
        
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Recursively collects all types from the syntax tree
        ''' </summary>
        Private Sub CollectTypes(vNode As SimpleSyntaxNode, vTypes As List(Of TypeInfo))
            Try
                ' Check if this node is a type
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eInterface, 
                         CodeNodeType.eModule, CodeNodeType.eStructure, 
                         CodeNodeType.eEnum
                        
                        Dim lTypeInfo As New TypeInfo with {
                            .Name = vNode.Name,
                            .FullName = vNode.GetFullyQualifiedName(),
                            .NodeType = vNode.NodeType,
                            .IsPartial = vNode.IsPartial,
                            .IsPublic = vNode.IsPublic,
                            .FilePath = vNode.FilePath,
                            .BaseType = vNode.BaseType
                        }
                        
                        ' Add interfaces
                        If vNode.ImplementsList IsNot Nothing Then
                            lTypeInfo.Interfaces.AddRange(vNode.ImplementsList)
                        End If
                        
                        vTypes.Add(lTypeInfo)
                End Select
                
                ' Recurse through children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        CollectTypes(lChild, vTypes)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CollectTypes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively collects all namespaces from the syntax tree
        ''' </summary>
        Private Sub CollectNamespaces(vNode As SimpleSyntaxNode, vNamespaces As HashSet(Of String))
            Try
                ' Check if this node is a namespace
                If vNode.NodeType = CodeNodeType.eNamespace Then
                    vNamespaces.Add(vNode.Name)
                End If
                
                ' Recurse through children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        CollectNamespaces(lChild, vNamespaces)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CollectNamespaces error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively finds a type by its fully qualified name
        ''' </summary>
        Private Function FindTypeRecursive(vNode As SimpleSyntaxNode, vFullyQualifiedName As String) As SimpleSyntaxNode
            Try
                ' Check if this node matches
                If vNode.GetFullyQualifiedName() = vFullyQualifiedName Then
                    Select Case vNode.NodeType
                        Case CodeNodeType.eClass, CodeNodeType.eInterface,
                             CodeNodeType.eModule, CodeNodeType.eStructure,
                             CodeNodeType.eEnum
                            Return vNode
                    End Select
                End If
                
                ' Recurse through children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        Dim lResult = FindTypeRecursive(lChild, vFullyQualifiedName)
                        If lResult IsNot Nothing Then
                            Return lResult
                        End If
                    Next
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindTypeRecursive error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
    End Class
    
    ''' <summary>
    ''' Information about a type in the project
    ''' </summary>
    Public Class TypeInfo
        Public Property Name As String
        Public Property FullName As String
        Public Property NodeType As CodeNodeType
        Public Property IsPartial As Boolean
        Public Property IsPublic As Boolean
        Public Property FilePath As String
        Public Property BaseType As String
        Public Property Interfaces As New List(Of String)
    End Class
    
    ''' <summary>
    ''' Summary information about a project
    ''' </summary>
    Public Class ProjectSummary
        Public Property FileCount As Integer
        Public Property NamespaceCount As Integer
        Public Property TypeCount As Integer
        Public Property ClassCount As Integer
        Public Property InterfaceCount As Integer
        Public Property ModuleCount As Integer
        Public Property StructureCount As Integer
        Public Property EnumCount As Integer
        Public Property ErrorCount As Integer
        Public Property WarningCount As Integer
        Public Property TotalLines As Integer
    End Class
    
End Namespace