' ProjectManager.Extension2.vb
' Created: 2025-08-16 14:30:32

Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Xml
Imports System.Threading.Tasks
Imports System.Text.Json
Imports System.Collections.Concurrent
Imports Newtonsoft.Json
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Syntax
Imports SimpleIDE.Interfaces

Namespace Managers

    Partial Public Class ProjectManager

        ''' <summary>
        ''' Get sort priority for node types
        ''' </summary>
        Private Function GetNodeTypePriority(vNodeType As CodeNodeType) As Integer
            Select Case vNodeType
                Case CodeNodeType.eNamespace : Return 0
                Case CodeNodeType.eInterface : Return 1
                Case CodeNodeType.eClass : Return 2
                Case CodeNodeType.eModule : Return 3
                Case CodeNodeType.eStructure : Return 4
                Case CodeNodeType.eEnum : Return 5
                Case CodeNodeType.eConstructor : Return 10
                Case CodeNodeType.eProperty : Return 11
                Case CodeNodeType.eMethod : Return 12
                Case CodeNodeType.eFunction : Return 13
                Case CodeNodeType.eField : Return 14
                Case CodeNodeType.eEvent : Return 15
                Case Else : Return 99
            End Select
        End Function
        
        
        Public Function GetSourceInfo(vFilePath as String) As SourceFileInfo
            Return pSourceFiles(vFilePath)
        End Function
        
        ' Add method to get project syntax tree
        Public Function GetProjectSyntaxTree() As SyntaxNode
            Try
                Return pProjectSyntaxTree
            Catch ex As Exception
                Console.WriteLine($"GetProjectSyntaxTree error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Fixed CreateEmptyFile method with proper return
        Public Function CreateEmptyFile(vFileName As String) As SourceFileInfo
            Try
                ' Create path for new file
                Dim lFilePath As String = Path.Combine(pCurrentProjectInfo.ProjectDirectory, vFileName)
                
                ' Create new SourceFileInfo
                Dim lFileInfo As New SourceFileInfo(lFilePath, pCurrentProjectInfo.ProjectDirectory)
                
                ' Set default content
                lFileInfo.Content = $"' {vFileName}" & Environment.NewLine & _
                                   $"' Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" & Environment.NewLine & _
                                   Environment.NewLine & _
                                   "Imports System" & Environment.NewLine & _
                                   Environment.NewLine & _
                                   "Namespace " & pCurrentProjectInfo.GetEffectiveRootNamespace() & Environment.NewLine & _
                                   Environment.NewLine & _
                                   "End Namespace"
                                   
                lFileInfo.IsLoaded = True
                
                ' Add to source files collection
                If Not pSourceFiles.ContainsKey(lFilePath) Then
                    pSourceFiles(lFilePath) = lFileInfo
                End If
                
                Return lFileInfo
                
            Catch ex As Exception
                Console.WriteLine($"CreateEmptyFile error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Fixed LoadProjectWithDocuments - removed pCodeParser initialization
        Public Function LoadProjectWithDocuments(vProjectPath As String) As Boolean
            Try
                Console.WriteLine($"ProjectManager.LoadProjectWithDocuments: Loading {vProjectPath}")
                
                ' First parse the project file
                Dim lProjectInfo As ProjectFileParser.ProjectInfo = ProjectFileParser.ParseProjectFile(vProjectPath)
                If lProjectInfo Is Nothing Then
                    Console.WriteLine("Failed to parse project file")
                    Return False
                End If
                
                ' Store project info
                pCurrentProjectInfo = New ProjectInfo()
                pCurrentProjectInfo.ProjectPath = vProjectPath
                pCurrentProjectInfo.ProjectName = lProjectInfo.ProjectName
                pCurrentProjectInfo.ProjectDirectory = lProjectInfo.ProjectDirectory
                pCurrentProjectInfo.CompileItems = lProjectInfo.CompileItems
                pCurrentProjectInfo.References = lProjectInfo.References
                pCurrentProjectInfo.PackageReferences = lProjectInfo.PackageReferences
                pCurrentProjectInfo.SourceFiles = New List(Of String)()
                
                ' Don't initialize pCodeParser - create parsers as needed
                ' If pCodeParser Is Nothing Then
                '     pCodeParser = New VBCodeParser()  ' REMOVED
                ' End If
                
                ' Clear existing models
                ClearDocumentModels()
                
                ' Get list of all VB source files
                Dim lSourceFiles As New List(Of String)()
                For Each lCompileItem In lProjectInfo.CompileItems
                    If lCompileItem.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) Then
                        Dim lFullPath As String = Path.Combine(lProjectInfo.ProjectDirectory, lCompileItem)
                        If File.Exists(lFullPath) Then
                            lSourceFiles.Add(lFullPath)
                            pCurrentProjectInfo.SourceFiles.Add(lFullPath)
                        End If
                    End If
                Next
                
                ' Set counts for progress
                pTotalFilesToLoad = lSourceFiles.Count
                pFilesLoaded = 0
                
                ' Load and create DocumentModels for each file
                For Each lFilePath In lSourceFiles
                    Dim lModel As DocumentModel = CreateDocumentModel(lFilePath)
                    If lModel IsNot Nothing Then
                        ' Calculate relative path
                        Dim lRelativePath As String = GetRelativePath(lFilePath, lProjectInfo.ProjectDirectory)
                        pDocumentModels(lRelativePath) = lModel
                        
                        ' Raise events
                        pFilesLoaded += 1
                        RaiseEvent ProjectLoadProgress(pFilesLoaded, pTotalFilesToLoad, Path.GetFileName(lFilePath))
                        RaiseEvent DocumentModelCreated(lRelativePath, lModel)
                    End If
                Next
                
                ' Build unified namespace tree
                BuildUnifiedNamespaceTree()
                
                ' Raise completion event
                RaiseEvent AllDocumentsLoaded(pDocumentModels.Count)
                
                Console.WriteLine($"Loaded {pDocumentModels.Count} documents")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProjectWithDocuments error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Helper to get relative path
        Private Function GetRelativePath(vFullPath As String, vBasePath As String) As String
            Try
                Dim lFullPath As String = Path.GetFullPath(vFullPath)
                Dim lBasePath As String = Path.GetFullPath(vBasePath)
                
                If Not lBasePath.EndsWith(Path.DirectorySeparatorChar) Then
                    lBasePath &= Path.DirectorySeparatorChar
                End If
                
                If lFullPath.StartsWith(lBasePath, StringComparison.OrdinalIgnoreCase) Then
                    Return lFullPath.Substring(lBasePath.Length)
                End If
                
                Return Path.GetFileName(vFullPath)
                
            Catch ex As Exception
                Return Path.GetFileName(vFullPath)
            End Try
        End Function
        
        ' Fixed MergeFileStructureFixed to use RelativePath property
        Private Sub MergeFileStructureFixed(vFileInfo As SourceFileInfo, vRootNamespace As SyntaxNode)
            Try
                If vFileInfo.SyntaxTree Is Nothing Then Return
                
                Console.WriteLine($"Merging {vFileInfo.FileName} into project structure")
                
                ' Process each root node in the file
                For Each lNode In vFileInfo.SyntaxTree.Children
                    Console.WriteLine($"  Processing node: {lNode.Name} ({lNode.NodeType})")
                    
                    ' Check if this is the implicit root namespace
                    If lNode.NodeType = CodeNodeType.eNamespace AndAlso 
                       lNode.IsImplicit AndAlso
                       String.Equals(lNode.Name, vRootNamespace.Name, StringComparison.OrdinalIgnoreCase) Then
                        ' This is the implicit root namespace - merge its children directly into our root
                        Console.WriteLine($"    Found implicit root namespace with {lNode.Children.Count} children")
                        For Each lChild In lNode.Children
                            Console.WriteLine($"      Merging child: {lChild.Name} ({lChild.NodeType})")
                            ' Use the RelativePath property that we added to SourceFileInfo
                            MergeNodeIntoProjectFixed(lChild, vRootNamespace, vFileInfo.RelativePath)
                        Next
                    Else
                        ' This is a regular node - merge it
                        MergeNodeIntoProjectFixed(lNode, vRootNamespace, vFileInfo.RelativePath)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"MergeFileStructureFixed error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub

        ''' <summary>
        ''' Create a DocumentModel for a file
        ''' </summary>
        Private Function CreateDocumentModel(vFilePath As String) As DocumentModel
            Try
                ' Create new DocumentModel
                Dim lModel As New DocumentModel(vFilePath)
                
                ' Load file content if it exists
                If File.Exists(vFilePath) Then
                    lModel.LoadFromFile(vFilePath)
                End If
                
                ' Parse the document
                lModel.ParseDocument()
                
                ' Wire up events
                AddHandler lModel.DocumentParsed, AddressOf OnDocumentParsed
                AddHandler lModel.StructureChanged, AddressOf OnDocumentStructureChanged
                AddHandler lModel.ModifiedStateChanged, AddressOf OnDocumentModifiedStateChanged
                
                Return lModel
                
            Catch ex As Exception
                Console.WriteLine($"CreateDocumentModel error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get the project root property (for compatibility)
        ''' </summary>
        Public ReadOnly Property ProjectRoot As String
            Get
                Return CurrentProjectDirectory
            End Get
        End Property

        ''' <summary>
        ''' Register a SourceFileInfo with the ProjectManager
        ''' </summary>
        Public Sub RegisterSourceFileInfo(vFilePath As String, vSourceFileInfo As SourceFileInfo)
            Try
                If String.IsNullOrEmpty(vFilePath) OrElse vSourceFileInfo Is Nothing Then
                    Return
                End If
                
                ' Normalize the file path
                Dim lNormalizedPath As String = Path.GetFullPath(vFilePath)
                
                ' Store or update the SourceFileInfo
                pSourceFiles(lNormalizedPath) = vSourceFileInfo
                
                ' Set project root namespace if this is a project file
                If pCurrentProjectInfo IsNot Nothing Then
                    vSourceFileInfo.ProjectRootNamespace = pCurrentProjectInfo.GetEffectiveRootNamespace()
                End If
                
                Console.WriteLine($"ProjectManager.RegisterSourceFileInfo: Registered {lNormalizedPath}")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RegisterSourceFileInfo error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get a SourceFileInfo by file path
        ''' </summary>
        Public Function GetSourceFileInfo(vFilePath As String) As SourceFileInfo
            Try
                If String.IsNullOrEmpty(vFilePath) Then
                    Return Nothing
                End If
                
                ' Normalize the file path
                Dim lNormalizedPath As String = Path.GetFullPath(vFilePath)
                
                If pSourceFiles.ContainsKey(lNormalizedPath) Then
                    Return pSourceFiles(lNormalizedPath)
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetSourceFileInfo error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Save a file through its SourceFileInfo
        ''' </summary>
        Public Function SaveFile(vFilePath As String) As Boolean
            Try
                Dim lSourceFileInfo As SourceFileInfo = GetSourceFileInfo(vFilePath)
                If lSourceFileInfo IsNot Nothing Then
                    Return lSourceFileInfo.SaveContent()
                End If
                
                Console.WriteLine($"ProjectManager.SaveFile: No SourceFileInfo found for {vFilePath}")
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.SaveFile error: {ex.Message}")
                Return False
            End Try
        End Function

''' <summary>
        ''' Diagnostic method to print the actual structure of a namespace
        ''' </summary>
        Private Sub DiagnoseNamespaceStructure(vNamespace As SyntaxNode, vIndent As Integer)
            Try
                Dim lIndentStr As String = New String(" "c, vIndent * 2)
                Console.WriteLine($"{lIndentStr}[{vNamespace.NodeType}] {vNamespace.Name} - {vNamespace.Children.Count} children")
                
                ' Group children by type to see what's actually in there
                Dim lChildrenByType As New Dictionary(Of CodeNodeType, List(Of String))()
                
                For Each lChild In vNamespace.Children
                    If Not lChildrenByType.ContainsKey(lChild.NodeType) Then
                        lChildrenByType(lChild.NodeType) = New List(Of String)()
                    End If
                    lChildrenByType(lChild.NodeType).Add(lChild.Name)
                Next
                
                ' Print summary of children by type
                For Each lKvp In lChildrenByType.OrderBy(Function(k) k.Key.ToString())
                    Console.WriteLine($"{lIndentStr}  {lKvp.Key}: {String.Join(", ", lKvp.Value)}")
                Next
                
                ' For classes and modules, show their nested types
                For Each lChild In vNamespace.Children
                    If lChild.NodeType = CodeNodeType.eClass OrElse lChild.NodeType = CodeNodeType.eModule Then
                        Console.WriteLine($"{lIndentStr}  [{lChild.NodeType}] {lChild.Name} contains:")
                        
                        Dim lNestedTypes As New List(Of String)()
                        For Each lNested In lChild.Children
                            If lNested.NodeType = CodeNodeType.eStructure OrElse 
                               lNested.NodeType = CodeNodeType.eEnum OrElse
                               lNested.NodeType = CodeNodeType.eClass OrElse
                               lNested.NodeType = CodeNodeType.eInterface Then
                                lNestedTypes.Add($"{lNested.NodeType}:{lNested.Name}")
                            End If
                        Next
                        
                        If lNestedTypes.Count > 0 Then
                            Console.WriteLine($"{lIndentStr}    Nested types: {String.Join(", ", lNestedTypes)}")
                        End If
                    End If
                Next
                
                ' Recursively diagnose sub-namespaces
                For Each lChild In vNamespace.Children
                    If lChild.NodeType = CodeNodeType.eNamespace Then
                        DiagnoseNamespaceStructure(lChild, vIndent + 1)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseNamespaceStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Call this after LoadProjectStructure completes
        ''' </summary>
        Public Sub DiagnoseProjectStructure()
            Try
                Console.WriteLine("========== PROJECT STRUCTURE DIAGNOSIS ==========")
                
                If pProjectSyntaxTree IsNot Nothing Then
                    Console.WriteLine($"Project root: {pProjectSyntaxTree.Name} ({pProjectSyntaxTree.NodeType})")
                    Console.WriteLine($"Root has {pProjectSyntaxTree.Children.Count} children")
                    
                    ' Find the root namespace (should be SimpleIDE)
                    For Each lChild In pProjectSyntaxTree.Children
                        If lChild.NodeType = CodeNodeType.eNamespace AndAlso lChild.Name = "SimpleIDE" Then
                            DiagnoseNamespaceStructure(lChild, 0)
                            
                            ' Special check for Syntax namespace
                            For Each lSubNamespace In lChild.Children
                                If lSubNamespace.NodeType = CodeNodeType.eNamespace AndAlso lSubNamespace.Name = "Syntax" Then
                                    Console.WriteLine("")
                                    Console.WriteLine("=== DETAILED SYNTAX NAMESPACE ANALYSIS ===")
                                    Console.WriteLine($"Syntax namespace has {lSubNamespace.Children.Count} direct children")
                                    
                                    ' Look for duplicates
                                    Dim lNameCounts As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                                    For Each lSyntaxChild In lSubNamespace.Children
                                        Dim lKey As String = $"{lSyntaxChild.NodeType}:{lSyntaxChild.Name}"
                                        If Not lNameCounts.ContainsKey(lKey) Then
                                            lNameCounts(lKey) = 0
                                        End If
                                        lNameCounts(lKey) += 1
                                    Next
                                    
                                    ' Report duplicates
                                    Console.WriteLine("Duplicates found:")
                                    For Each lKvp In lNameCounts.Where(Function(k) k.Value > 1)
                                        Console.WriteLine($"  {lKvp.Key} appears {lKvp.Value} times")
                                    Next
                                    
                                    ' Check if BlockInfo is in namespace children
                                    Dim lBlockInfoCount As Integer = 0
                                    For Each lSyntaxChild In lSubNamespace.Children
                                        If lSyntaxChild.Name = "BlockInfo" Then
                                            lBlockInfoCount += 1
                                            Console.WriteLine($"  Found BlockInfo at namespace level: Parent={If(lSyntaxChild.Parent IsNot Nothing, lSyntaxChild.Parent.Name, "Nothing")}")
                                        End If
                                    Next
                                    
                                    If lBlockInfoCount > 0 Then
                                        Console.WriteLine($"ERROR: BlockInfo found {lBlockInfoCount} times at namespace level!")
                                    End If
                                End If
                            Next
                        End If
                    Next
                Else
                    Console.WriteLine("No project syntax tree available")
                End If
                
                Console.WriteLine("========== END DIAGNOSIS ==========")
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseProjectStructure error: {ex.Message}")
            End Try
        End Sub
        
    End Class

End Namespace