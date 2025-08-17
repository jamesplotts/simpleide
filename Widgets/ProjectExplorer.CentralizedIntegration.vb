' Managers/ProjectManager.CentralizedParsing.vb - Centralized parsing implementation
Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

' ProjectManager.CentralizedParsing.vb
' Created: 2025-08-12

Namespace Managers
    
    Partial Public Class ProjectManager
        
        ' ===== Centralized Parsing Implementation =====
        
        ''' <summary>
        ''' Load all project files into memory and parse them
        ''' </summary>
        Public Function LoadAndParseProject() As Boolean
            Try
                If pCurrentProjectInfo Is Nothing Then
                    Console.WriteLine("No project loaded")
                    Return False
                End If
                
                pIsLoadingStructure = True
                
                ' Get root namespace from project
                Dim lRootNamespaceName As String = pCurrentProjectInfo.RootNamespace
                If String.IsNullOrEmpty(lRootNamespaceName) Then
                    lRootNamespaceName = Path.GetFileNameWithoutExtension(pCurrentProjectInfo.ProjectPath)
                End If
                
                Console.WriteLine($"===== Loading and Parsing Project: {pCurrentProjectInfo.ProjectName} =====")
                Console.WriteLine($"Root Namespace: {lRootNamespaceName}")
                
                ' Initialize project syntax tree
                pProjectSyntaxTree = New SyntaxNode(CodeNodeType.eDocument, pCurrentProjectInfo.ProjectName)
                
                ' Create root namespace node
                Dim lRootNamespace As New SyntaxNode(CodeNodeType.eNamespace, lRootNamespaceName)
                lRootNamespace.IsImplicit = True
                pProjectSyntaxTree.AddChild(lRootNamespace)
                
                ' Get all source files to parse
                Dim lFilesToParse As List(Of String) = GetProjectSourceFiles()
                Console.WriteLine($"Found {lFilesToParse.Count} source files to parse")
                
                ' Phase 1: Load all files into memory
                Console.WriteLine("")
                Console.WriteLine("===== Phase 1: Loading Files =====")
                Dim lLoadedCount As Integer = 0
                
                For Each lFilePath In lFilesToParse
                    ' Create or get SourceFileInfo
                    Dim lFileInfo As SourceFileInfo = GetOrCreateSourceFileInfo(lFilePath)
                    
                    ' Set root namespace
                    lFileInfo.SetProjectRootNamespace(lRootNamespaceName)
                    
                    ' Load file content
                    If lFileInfo.EnsureLoaded() Then
                        lLoadedCount += 1
                        Console.WriteLine($"  Loaded: {lFileInfo.FileName}")
                    Else
                        Console.WriteLine($"  Failed to load: {lFileInfo.FileName}")
                    End If
                    
                    ' Raise progress event
                    RaiseEvent ParsingProgress(lLoadedCount, lFilesToParse.Count, lFileInfo.FileName)
                Next
                
                Console.WriteLine($"Loaded {lLoadedCount}/{lFilesToParse.Count} files")
                
                ' Phase 2: Parse all files to build object node structure
                Console.WriteLine("")
                Console.WriteLine("===== Phase 2: Parsing Files =====")
                Dim lParsedCount As Integer = 0
                
                For Each lFilePath In lFilesToParse
                    Dim lFileInfo As SourceFileInfo = pSourceFiles(lFilePath)
                    
                    ' Parse the file using centralized parser
                    If lFileInfo.ParseFile() Then
                        lParsedCount += 1
                        Console.WriteLine($"  Parsed: {lFileInfo.FileName}")
                        
                        ' Merge into project tree
                        lFileInfo.MergeIntoProjectTree(lRootNamespace)
                        
                        ' Raise event
                        RaiseEvent FileParsed(lFileInfo)
                    Else
                        Console.WriteLine($"  Failed to parse: {lFileInfo.FileName}")
                    End If
                    
                    ' Raise progress event
                    RaiseEvent ParsingProgress(lParsedCount, lFilesToParse.Count, lFileInfo.FileName)
                Next
                
                Console.WriteLine($"Parsed {lParsedCount}/{lFilesToParse.Count} files")
                
                ' Phase 3: Build cross-references and indices
                Console.WriteLine("")
                Console.WriteLine("===== Phase 3: Building Indices =====")
                BuildProjectIndices()
                
                ' Print final structure summary
                PrintProjectStructureSummary()
                
                pIsLoadingStructure = False
                
                ' Raise completion event
                RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                
                Console.WriteLine("")
                Console.WriteLine("===== Project Loading Complete =====")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"LoadAndParseProject error: {ex.Message}")
                pIsLoadingStructure = False
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Get or create SourceFileInfo for a file
        ''' </summary>
        Private Function GetOrCreateSourceFileInfo(vFilePath As String) As SourceFileInfo
            Try
                If pSourceFiles.ContainsKey(vFilePath) Then
                    Return pSourceFiles(vFilePath)
                End If
                
                ' Create new SourceFileInfo
                Dim lFileInfo As New SourceFileInfo(vFilePath, pCurrentProjectInfo.ProjectDirectory)
                pSourceFiles(vFilePath) = lFileInfo
                
                Return lFileInfo
                
            Catch ex As Exception
                Console.WriteLine($"GetOrCreateSourceFileInfo error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get all source files from the project
        ''' </summary>
        Private Function GetProjectSourceFiles() As List(Of String)
            Dim lFiles As New List(Of String)()
            
            Try
                If pCurrentProjectInfo Is Nothing Then Return lFiles
                
                ' Get files from compile items
                For Each lCompileItem In pCurrentProjectInfo.CompileItems
                    Dim lFullPath As String = Path.Combine(pCurrentProjectInfo.ProjectDirectory, lCompileItem)
                    
                    ' Only include .vb files that exist
                    If File.Exists(lFullPath) AndAlso 
                       lFullPath.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) Then
                        lFiles.Add(lFullPath)
                    End If
                Next
                
                ' Sort files for consistent processing
                lFiles.Sort()
                
                Return lFiles
                
            Catch ex As Exception
                Console.WriteLine($"GetSourceFiles error: {ex.Message}")
                Return lFiles
            End Try
        End Function
        
        ''' <summary>
        ''' Build project-wide indices for quick lookup
        ''' </summary>
        Private Sub BuildProjectIndices()
            Try
                ' Build type index
                pTypeIndex = New Dictionary(Of String, SyntaxNode)()
                
                ' Build member index
                pMemberIndex = New Dictionary(Of String, List(Of SyntaxNode))()
                
                ' Build namespace index
                pNamespaceIndex = New Dictionary(Of String, SyntaxNode)()
                
                ' Traverse tree and build indices
                If pProjectSyntaxTree IsNot Nothing Then
                    BuildIndicesFromNode(pProjectSyntaxTree)
                End If
                
                Console.WriteLine($"  Built indices: {pTypeIndex.Count} types, {pMemberIndex.Count} members, {pNamespaceIndex.Count} namespaces")
                
            Catch ex As Exception
                Console.WriteLine($"BuildProjectIndices error: {ex.Message}")
            End Try
        End Sub
        
        ' Index dictionaries
        Private pTypeIndex As Dictionary(Of String, SyntaxNode)
        Private pMemberIndex As Dictionary(Of String, List(Of SyntaxNode))
        Private pNamespaceIndex As Dictionary(Of String, SyntaxNode)
        
        ''' <summary>
        ''' Recursively build indices from syntax tree
        ''' </summary>
        Private Sub BuildIndicesFromNode(vNode As SyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Add to appropriate index based on node type
                Select Case vNode.NodeType
                    Case CodeNodeType.eNamespace
                        If Not pNamespaceIndex.ContainsKey(vNode.Name) Then
                            pNamespaceIndex(vNode.Name) = vNode
                        End If
                        
                    Case CodeNodeType.eClass, CodeNodeType.eModule, 
                         CodeNodeType.eInterface, CodeNodeType.eStructure, 
                         CodeNodeType.eEnum
                        ' Add to type index
                        Dim lTypeName As String = vNode.Name
                        If Not pTypeIndex.ContainsKey(lTypeName) Then
                            pTypeIndex(lTypeName) = vNode
                        End If
                        
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction,
                         CodeNodeType.eProperty, CodeNodeType.eEvent,
                         CodeNodeType.eField, CodeNodeType.eConst
                        ' Add to member index
                        Dim lMemberName As String = vNode.Name
                        If Not pMemberIndex.ContainsKey(lMemberName) Then
                            pMemberIndex(lMemberName) = New List(Of SyntaxNode)()
                        End If
                        pMemberIndex(lMemberName).Add(vNode)
                End Select
                
                ' Process children
                If vNode.Children IsNot Nothing Then
                    For Each lChild In vNode.Children
                        BuildIndicesFromNode(lChild)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildIndicesFromNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Print summary of project structure
        ''' </summary>
        Private Sub PrintProjectStructureSummary()
            Try
                Console.WriteLine("")
                Console.WriteLine("===== Project Structure Summary =====")
                
                If pProjectSyntaxTree Is Nothing Then
                    Console.WriteLine("No project structure")
                    Return
                End If
                
                ' Count node types
                Dim lCounts As New Dictionary(Of CodeNodeType, Integer)()
                CountNodeTypes(pProjectSyntaxTree, lCounts)
                
                ' Print counts
                Console.WriteLine("Node counts:")
                For Each lKvp In lCounts.OrderBy(Function(k) k.Key.ToString())
                    If lKvp.Value > 0 Then
                        Console.WriteLine($"  {lKvp.Key}: {lKvp.Value}")
                    End If
                Next
                
                ' Print file summary
                Console.WriteLine($"")
                Console.WriteLine($"Total files: {pSourceFiles.Count}")
                Dim lErrorCount as Integer
                For Each lSf As SourceFileInfo in pSourceFiles.Values
                    If lSf.ParseErrors IsNot Nothing Then lErrorCount += lSf.ParseErrors.Count
                Next
                Console.WriteLine($"Files with errors: {lErrorCount}")
                
            Catch ex As Exception
                Console.WriteLine($"PrintProjectStructureSummary error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Count node types in tree
        ''' </summary>
        Private Sub CountNodeTypes(vNode As SyntaxNode, vCounts As Dictionary(Of CodeNodeType, Integer))
            Try
                If vNode Is Nothing Then Return
                
                ' Count this node
                If Not vCounts.ContainsKey(vNode.NodeType) Then
                    vCounts(vNode.NodeType) = 0
                End If
                vCounts(vNode.NodeType) += 1
                
                ' Count children
                If vNode.Children IsNot Nothing Then
                    For Each lChild In vNode.Children
                        CountNodeTypes(lChild, vCounts)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CountNodeTypes error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Query Methods =====
        
        ''' <summary>
        ''' Get all source files
        ''' </summary>
        Public Function GetSourceFiles() As Dictionary(Of String, SourceFileInfo)
            Try
                Return pSourceFiles
                
            Catch ex As Exception
                Console.WriteLine($"GetSourceFiles error: {ex.Message}")
                Return New Dictionary(Of String, SourceFileInfo)()
            End Try
        End Function
        
        ''' <summary>
        ''' Find a type by name
        ''' </summary>
        Public Function FindType(vTypeName As String) As SyntaxNode
            Try
                If pTypeIndex IsNot Nothing AndAlso pTypeIndex.ContainsKey(vTypeName) Then
                    Return pTypeIndex(vTypeName)
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindType error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Find all members with a given name
        ''' </summary>
        Public Function FindMembers(vMemberName As String) As List(Of SyntaxNode)
            Try
                If pMemberIndex IsNot Nothing AndAlso pMemberIndex.ContainsKey(vMemberName) Then
                    Return pMemberIndex(vMemberName)
                End If
                
                Return New List(Of SyntaxNode)()
                
            Catch ex As Exception
                Console.WriteLine($"FindMembers error: {ex.Message}")
                Return New List(Of SyntaxNode)()
            End Try
        End Function
        
        ''' <summary>
        ''' Find a namespace by name
        ''' </summary>
        Public Function FindNamespace(vNamespaceName As String) As SyntaxNode
            Try
                If pNamespaceIndex IsNot Nothing AndAlso pNamespaceIndex.ContainsKey(vNamespaceName) Then
                    Return pNamespaceIndex(vNamespaceName)
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindNamespace error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get all types in the project
        ''' </summary>
        Public Function GetAllTypes() As List(Of SyntaxNode)
            Try
                If pTypeIndex IsNot Nothing Then
                    Return pTypeIndex.Values.ToList()
                End If
                
                Return New List(Of SyntaxNode)()
                
            Catch ex As Exception
                Console.WriteLine($"GetAllTypes error: {ex.Message}")
                Return New List(Of SyntaxNode)()
            End Try
        End Function
        
        ''' <summary>
        ''' Reparse a specific file (e.g., after editing)
        ''' </summary>
        Public Sub ReparseFile(vFilePath As String, Optional vContent As String = Nothing)
            Try
                If Not pSourceFiles.ContainsKey(vFilePath) Then
                    Console.WriteLine($"File not in project: {vFilePath}")
                    Return
                End If
                
                Dim lFileInfo As SourceFileInfo = pSourceFiles(vFilePath)
                
                ' Update content if provided
                If Not String.IsNullOrEmpty(vContent) Then
                    lFileInfo.Content = vContent
                End If
                
                ' Reparse the file
                If lFileInfo.ParseFile() Then
                    ' Rebuild project tree
                    RebuildProjectTree()
                    
                    ' Raise event
                    RaiseEvent FileParsed(lFileInfo)
                    RaiseEvent ProjectStructureChanged(ProjectManager.ConvertSyntaxNodeToDocumentNode(pProjectSyntaxTree))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ReparseFile error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Rebuild the entire project tree from parsed files
        ''' </summary>
        Private Sub RebuildProjectTree()
            Try
                Console.WriteLine("Rebuilding project tree...")
                
                ' Clear existing tree
                If pProjectSyntaxTree IsNot Nothing Then
                    pProjectSyntaxTree.Children.Clear()
                End If
                
                ' Get root namespace
                Dim lRootNamespaceName As String = pCurrentProjectInfo.RootNamespace
                If String.IsNullOrEmpty(lRootNamespaceName) Then
                    lRootNamespaceName = Path.GetFileNameWithoutExtension(pCurrentProjectInfo.ProjectPath)
                End If
                
                ' Create new root namespace
                Dim lRootNamespace As New SyntaxNode(CodeNodeType.eNamespace, lRootNamespaceName)
                lRootNamespace.IsImplicit = True
                pProjectSyntaxTree.AddChild(lRootNamespace)
                
                ' Merge all file structures
                For Each lFileInfo In pSourceFiles.Values
                    If lFileInfo.SyntaxTree IsNot Nothing Then
                        lFileInfo.MergeIntoProjectTree(lRootNamespace)
                    End If
                Next
                
                ' Rebuild indices
                BuildProjectIndices()
                
                Console.WriteLine("Project tree rebuilt")
                
            Catch ex As Exception
                Console.WriteLine($"RebuildProjectTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get IntelliSense suggestions at a specific location
        ''' </summary>
        Public Function GetIntelliSenseSuggestions(vFilePath As String, vLine As Integer, vColumn As Integer) As List(Of IntelliSenseSuggestion)
            Dim lSuggestions As New List(Of IntelliSenseSuggestion)()
            
            Try
                If Not pSourceFiles.ContainsKey(vFilePath) Then
                    Return lSuggestions
                End If
                
                Dim lFileInfo As SourceFileInfo = pSourceFiles(vFilePath)
                
                ' Get nodes at the current line
                Dim lNodesAtLine As List(Of SyntaxNode) = lFileInfo.GetNodesAtLine(vLine)
                
                ' Build suggestions based on context
                For Each lNode In lNodesAtLine
                    AddSuggestionsForNode(lNode, lSuggestions)
                Next
                
                ' Add global types and members
                AddGlobalSuggestions(lSuggestions)
                
                ' Sort by relevance
                lSuggestions.Sort(Function(a, b) b.Priority.CompareTo(a.Priority))
                
                Return lSuggestions
                
            Catch ex As Exception
                Console.WriteLine($"GetIntelliSenseSuggestions error: {ex.Message}")
                Return lSuggestions
            End Try
        End Function
        
        ''' <summary>
        ''' Add IntelliSense suggestions for a node
        ''' </summary>
        Private Sub AddSuggestionsForNode(vNode As SyntaxNode, vSuggestions As List(Of IntelliSenseSuggestion))
            Try
                If vNode Is Nothing Then Return
                
                ' Add members of the containing type
                If vNode.NodeType = CodeNodeType.eClass OrElse 
                   vNode.NodeType = CodeNodeType.eModule OrElse
                   vNode.NodeType = CodeNodeType.eStructure Then
                    
                    For Each lChild In vNode.Children
                        If IsAccessibleMember(lChild) Then
                            vSuggestions.Add(CreateSuggestion(lChild))
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddSuggestionsForNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Add global IntelliSense suggestions
        ''' </summary>
        Private Sub AddGlobalSuggestions(vSuggestions As List(Of IntelliSenseSuggestion))
            Try
                ' Add all types
                For Each lType In GetAllTypes()
                    vSuggestions.Add(CreateSuggestion(lType))
                Next
                
                ' Add VB.NET keywords
                AddKeywordSuggestions(vSuggestions)
                
            Catch ex As Exception
                Console.WriteLine($"AddGlobalSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Create IntelliSense suggestion from node
        ''' </summary>
        Private Function CreateSuggestion(vNode As SyntaxNode) As IntelliSenseSuggestion
            Dim lSuggestion As New IntelliSenseSuggestion()
            
            Try
                lSuggestion.Text = vNode.Name
                lSuggestion.SuggestionType = GetSuggestionType(vNode.NodeType)
                lSuggestion.Priority = GetSuggestionPriority(vNode.NodeType)
                
                ' Build description
                Dim lDescription As String = GetNodeTypeString(vNode.NodeType)
                If vNode.Visibility <> SyntaxNode.eVisibility.eUnspecified Then
                    lDescription = $"{vNode.Visibility} {lDescription}"
                End If
                lSuggestion.Description = $"{lDescription} {vNode.Name}"
                
                Return lSuggestion
                
            Catch ex As Exception
                Console.WriteLine($"CreateSuggestion error: {ex.Message}")
                Return lSuggestion
            End Try
        End Function
        
        ''' <summary>
        ''' Map node type to suggestion type
        ''' </summary>
        Private Function GetSuggestionType(vNodeType As CodeNodeType) As IntelliSenseSuggestionType
            Select Case vNodeType
                Case CodeNodeType.eClass, CodeNodeType.eModule,
                     CodeNodeType.eInterface, CodeNodeType.eStructure
                    Return IntelliSenseSuggestionType.eType
                    
                Case CodeNodeType.eMethod, CodeNodeType.eFunction
                    Return IntelliSenseSuggestionType.eMethod
                    
                Case CodeNodeType.eProperty
                    Return IntelliSenseSuggestionType.eProperty
                    
                Case CodeNodeType.eField, CodeNodeType.eConst
                    Return IntelliSenseSuggestionType.eVariable
                    
                Case CodeNodeType.eEvent
                    Return IntelliSenseSuggestionType.eEvent
                    
                Case CodeNodeType.eNamespace
                    Return IntelliSenseSuggestionType.eNamespace
                    
                Case Else
                    Return IntelliSenseSuggestionType.eUnspecified
            End Select
        End Function
        
        ''' <summary>
        ''' Get priority for suggestion type
        ''' </summary>
        Private Function GetSuggestionPriority(vNodeType As CodeNodeType) As Integer
            Select Case vNodeType
                Case CodeNodeType.eProperty
                    Return 100
                Case CodeNodeType.eMethod, CodeNodeType.eFunction
                    Return 90
                Case CodeNodeType.eField
                    Return 80
                Case CodeNodeType.eClass, CodeNodeType.eModule
                    Return 70
                Case Else
                    Return 50
            End Select
        End Function
        
        ''' <summary>
        ''' Get string representation of node type
        ''' </summary>
        Private Function GetNodeTypeString(vNodeType As CodeNodeType) As String
            Select Case vNodeType
                Case CodeNodeType.eClass : Return "Class"
                Case CodeNodeType.eModule : Return "Module"
                Case CodeNodeType.eInterface : Return "Interface"
                Case CodeNodeType.eStructure : Return "Structure"
                Case CodeNodeType.eEnum : Return "Enum"
                Case CodeNodeType.eMethod : Return "Sub"
                Case CodeNodeType.eFunction : Return "Function"
                Case CodeNodeType.eProperty : Return "Property"
                Case CodeNodeType.eField : Return "Field"
                Case CodeNodeType.eConst : Return "Const"
                Case CodeNodeType.eEvent : Return "Event"
                Case CodeNodeType.eNamespace : Return "Namespace"
                Case Else : Return vNodeType.ToString()
            End Select
        End Function
        
        ''' <summary>
        ''' Check if member is accessible
        ''' </summary>
        Private Function IsAccessibleMember(vNode As SyntaxNode) As Boolean
            ' For now, include all members
            ' TODO: Implement proper visibility checking based on context
            Return True
        End Function
        
        ''' <summary>
        ''' Add VB.NET keyword suggestions
        ''' </summary>
        Private Sub AddKeywordSuggestions(vSuggestions As List(Of IntelliSenseSuggestion))
            Dim lKeywords As String() = {
                "As", "Boolean", "Byte", "Class", "Const", "Dim", "Do", "Double",
                "Each", "Else", "ElseIf", "End", "Enum", "Event", "False", "For",
                "Function", "If", "Implements", "Imports", "In", "Inherits", "Integer",
                "Interface", "Is", "Loop", "Me", "Module", "Namespace", "New", "Next",
                "Nothing", "Object", "Of", "Private", "Property", "Protected", "Public",
                "Return", "Select", "Set", "Shared", "Single", "String", "Structure",
                "Sub", "Then", "True", "Try", "Until", "While", "With"
            }
            
            For Each lKeyword In lKeywords
                vSuggestions.Add(New IntelliSenseSuggestion With {
                    .Text = lKeyword,
                    .SuggestionType = IntelliSenseSuggestionType.eKeyword,
                    .Description = $"Keyword {lKeyword}",
                    .Priority = 40
                })
            Next
        End Sub
        
    End Class
    
End Namespace
