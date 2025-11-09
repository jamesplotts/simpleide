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

' Add these aliases to resolve ambiguity:
Imports SimpleSyntaxNode = SimpleIDE.Syntax.SyntaxNode
Imports RoslynSyntaxNode = Microsoft.CodeAnalysis.SyntaxNode

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
        ''' Finds all methods in the project
        ''' </summary>
        Public Function GetAllMethods() As IEnumerable(Of MethodInfo)
            Try
                Dim lMethods As New List(Of MethodInfo)
                
                If RootNode IsNot Nothing Then
                    CollectMethods(RootNode, lMethods)
                End If
                
                Return lMethods
                
            Catch ex As Exception
                Console.WriteLine($"GetAllMethods error: {ex.Message}")
                Return New List(Of MethodInfo)()
            End Try
        End Function
        
        ''' <summary>
        ''' Finds a type by name
        ''' </summary>
        Public Function FindType(vTypeName As String) As SimpleSyntaxNode
            Try
                If RootNode Is Nothing Then Return Nothing
                
                Return FindTypeRecursive(RootNode, vTypeName)
                
            Catch ex As Exception
                Console.WriteLine($"FindType error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all members of a type
        ''' </summary>
        Public Function GetTypeMembers(vTypeName As String) As IEnumerable(Of SimpleSyntaxNode)
            Try
                Dim lType = FindType(vTypeName)
                If lType IsNot Nothing Then
                    Return lType.Children
                End If
                
                Return New List(Of SimpleSyntaxNode)()
                
            Catch ex As Exception
                Console.WriteLine($"GetTypeMembers error: {ex.Message}")
                Return New List(Of SimpleSyntaxNode)()
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
        Public Function GetAllErrors() As IEnumerable(Of Diagnostic)
            Try
                Dim lErrors As New List(Of Diagnostic)()
                
                ' Add project-level diagnostics
                lErrors.AddRange(Diagnostics.Where(Function(d) d.Severity = DiagnosticSeverity.error))
                
                ' Add file-level diagnostics
                for each lFile in Files.Values
                    lErrors.AddRange(lFile.GetErrors())
                Next
                
                Return lErrors
                
            Catch ex As Exception
                Console.WriteLine($"GetAllErrors error: {ex.Message}")
                Return New List(Of Diagnostic)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all warnings in the project
        ''' </summary>
        Public Function GetAllWarnings() As IEnumerable(Of Diagnostic)
            Try
                Dim lWarnings As New List(Of Diagnostic)()
                
                ' Add project-level diagnostics
                lWarnings.AddRange(Diagnostics.Where(Function(d) d.Severity = DiagnosticSeverity.Warning))
                
                ' Add file-level diagnostics
                for each lFile in Files.Values
                    lWarnings.AddRange(lFile.GetWarnings())
                Next
                
                Return lWarnings
                
            Catch ex As Exception
                Console.WriteLine($"GetAllWarnings error: {ex.Message}")
                Return New List(Of Diagnostic)()
            End Try
        End Function
        
        ''' <summary>
        ''' Merges multiple file syntax trees into a unified project tree
        ''' </summary>
        Public Sub BuildUnifiedTree()
            Try
                ' Create root node
                RootNode = New SimpleSyntaxNode(CodeNodeType.eProject, AssemblyName)
                
                ' Create root namespace node if specified
                Dim lNamespaceNode As SimpleSyntaxNode = Nothing
                If Not String.IsNullOrEmpty(RootNamespace) Then
                    lNamespaceNode = New SimpleSyntaxNode(CodeNodeType.eNamespace, RootNamespace)
                    lNamespaceNode.IsImplicit = True
                    RootNode.AddChild(lNamespaceNode)
                End If
                
                ' Dictionary to track partial classes
                Dim lPartialClasses As New Dictionary(Of String, SimpleSyntaxNode)(StringComparer.OrdinalIgnoreCase)
                
                ' Process each file
                for each lFile in Files.Values
                    If lFile.SimpleIDETree IsNot Nothing Then
                        MergeFileIntoProject(lFile.SimpleIDETree, If(lNamespaceNode, RootNode), lPartialClasses)
                    End If
                Next
                
                ' Sort children by type and name
                SortNodeChildren(RootNode)
                
            Catch ex As Exception
                Console.WriteLine($"BuildUnifiedTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the line metadata for a specific file
        ''' </summary>
        Public Function GetLineMetadata(vFilePath As String) As LineMetadata()
            Try
                If Files.ContainsKey(vFilePath) Then
                    Return Files(vFilePath).LineMetadata
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetLineMetadata error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Recursively collects type information
        ''' </summary>
        Private Sub CollectTypes(vNode As SimpleSyntaxNode, vTypes As List(Of TypeInfo))
            Try
                ' Check if this node is a type
                If vNode.NodeType = CodeNodeType.eClass OrElse
                   vNode.NodeType = CodeNodeType.eModule OrElse
                   vNode.NodeType = CodeNodeType.eInterface OrElse
                   vNode.NodeType = CodeNodeType.eStructure OrElse
                   vNode.NodeType = CodeNodeType.eEnum Then
                    
                    Dim lTypeInfo As New TypeInfo()
                    lTypeInfo.Name = vNode.Name
                    lTypeInfo.FullName = vNode.FullName
                    lTypeInfo.NodeType = vNode.NodeType
                    lTypeInfo.FilePath = vNode.FilePath
                    lTypeInfo.StartLine = vNode.StartLine
                    lTypeInfo.EndLine = vNode.EndLine
                    
                    vTypes.Add(lTypeInfo)
                End If
                
                ' Recurse into children
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
        ''' Recursively collects method information
        ''' </summary>
        Private Sub CollectMethods(vNode As SimpleSyntaxNode, vMethods As List(Of MethodInfo))
            Try
                ' Check if this node is a method/function
                If vNode.NodeType = CodeNodeType.eMethod OrElse
                   vNode.NodeType = CodeNodeType.eFunction OrElse
                   vNode.NodeType = CodeNodeType.eConstructor Then
                    
                    Dim lMethodInfo As New MethodInfo()
                    lMethodInfo.Name = vNode.Name
                    lMethodInfo.FullName = vNode.FullName
                    lMethodInfo.NodeType = vNode.NodeType
                    lMethodInfo.FilePath = vNode.FilePath
                    lMethodInfo.StartLine = vNode.StartLine
                    lMethodInfo.EndLine = vNode.EndLine
                    lMethodInfo.ReturnType = vNode.ReturnType
                    lMethodInfo.Parameters = vNode.Parameters
                    
                    vMethods.Add(lMethodInfo)
                End If
                
                ' Recurse into children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        CollectMethods(lChild, vMethods)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CollectMethods error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively finds a type by name
        ''' </summary>
        Private Function FindTypeRecursive(vNode As SimpleSyntaxNode, vTypeName As String) As SimpleSyntaxNode
            Try
                ' Check if this node is the type we're looking for
                If (vNode.NodeType = CodeNodeType.eClass OrElse
                    vNode.NodeType = CodeNodeType.eModule OrElse
                    vNode.NodeType = CodeNodeType.eInterface OrElse
                    vNode.NodeType = CodeNodeType.eStructure OrElse
                    vNode.NodeType = CodeNodeType.eEnum) AndAlso
                   String.Equals(vNode.Name, vTypeName, StringComparison.OrdinalIgnoreCase) Then
                    Return vNode
                End If
                
                ' Recurse into children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        Dim lResult = FindTypeRecursive(lChild, vTypeName)
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
        
        ''' <summary>
        ''' Merges a file's syntax tree into the project tree
        ''' </summary>
        Private Sub MergeFileIntoProject(vFileNode As SimpleSyntaxNode, vProjectNode As SimpleSyntaxNode, vPartialClasses As Dictionary(Of String, SimpleSyntaxNode))
            Try
                ' Process each child of the file node
                If vFileNode.Children IsNot Nothing Then
                    for each lChild in vFileNode.Children
                        MergeNodeIntoProject(lChild, vProjectNode, vPartialClasses)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeFileIntoProject error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Merges a node into the project tree
        ''' </summary>
        Private Sub MergeNodeIntoProject(vNode As SimpleSyntaxNode, vParent As SimpleSyntaxNode, vPartialClasses As Dictionary(Of String, SimpleSyntaxNode))
            Try
                ' Handle partial classes
                If vNode.IsPartial AndAlso 
                   (vNode.NodeType = CodeNodeType.eClass OrElse
                    vNode.NodeType = CodeNodeType.eModule OrElse
                    vNode.NodeType = CodeNodeType.eStructure) Then
                    
                    Dim lKey = vNode.FullName
                    If vPartialClasses.ContainsKey(lKey) Then
                        ' Merge with existing partial
                        Dim lExisting = vPartialClasses(lKey)
                        If vNode.Children IsNot Nothing Then
                            for each lChild in vNode.Children
                                lExisting.AddChild(lChild)
                            Next
                        End If
                    Else
                        ' First occurrence of this partial
                        vParent.AddChild(vNode)
                        vPartialClasses(lKey) = vNode
                    End If
                Else
                    ' Non-partial node
                    vParent.AddChild(vNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNodeIntoProject error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sorts the children of a node by type and name
        ''' </summary>
        Private Sub SortNodeChildren(vNode As SimpleSyntaxNode)
            Try
                If vNode?.Children Is Nothing OrElse vNode.Children.Count <= 1 Then Return
                
                ' Sort children
                vNode.Children.Sort(Function(a, b)
                    ' Sort by node type first
                    Dim lTypeCompare = a.NodeType.CompareTo(b.NodeType)
                    If lTypeCompare <> 0 Then Return lTypeCompare
                    
                    ' Then by name
                    Return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                End Function)
                
                ' Recursively sort children
                for each lChild in vNode.Children
                    SortNodeChildren(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SortNodeChildren error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
    ''' <summary>
    ''' Type information for GetAllTypes
    ''' </summary>
    Public Class TypeInfo
        Public Property Name As String
        Public Property FullName As String
        Public Property NodeType As CodeNodeType
        Public Property FilePath As String
        Public Property StartLine As Integer
        Public Property EndLine As Integer
    End Class
    
    ''' <summary>
    ''' Method information for GetAllMethods
    ''' </summary>
    Public Class MethodInfo
        Public Property Name As String
        Public Property FullName As String
        Public Property NodeType As CodeNodeType
        Public Property FilePath As String
        Public Property StartLine As Integer
        Public Property EndLine As Integer
        Public Property ReturnType As String
        Public Property Parameters As List(Of ParameterInfo)
    End Class
    
End Namespace