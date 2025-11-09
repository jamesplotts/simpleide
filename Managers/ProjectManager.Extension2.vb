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
        
        
        Public Function GetSourceInfo(vFilePath As String) As SourceFileInfo
            Return pSourceFiles(vFilePath)
        End Function
        
        ' Add method to get project syntax tree
        Public Function GetProjectSyntaxTree() As SyntaxNode
            Try
                Console.WriteLine($"DEBUG: GetProjectSyntaxTree called")
                Console.WriteLine($"  pProjectSyntaxTree is {If(pProjectSyntaxTree Is Nothing, "Nothing", "NOT Nothing")}")
                
                ' If we don't have a project syntax tree but we have source files, build it
                If pProjectSyntaxTree Is Nothing AndAlso pSourceFiles IsNot Nothing AndAlso pSourceFiles.Count > 0 Then
                    Console.WriteLine($"  Building project syntax tree from {pSourceFiles.Count} source files...")
                    BuildProjectSyntaxTree()
                End If
                
                If pProjectSyntaxTree IsNot Nothing Then
                    Console.WriteLine($"  Returning tree with {pProjectSyntaxTree.Children.Count} root children")
                Else
                    Console.WriteLine($"  Returning Nothing - tree could not be built")
                End If
                
                Return pProjectSyntaxTree
            Catch ex As Exception
                Console.WriteLine($"GetProjectSyntaxTree error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Build the complete project syntax tree from all loaded source files
        ''' </summary>
        Public Sub BuildProjectSyntaxTree()
            Try
                Console.WriteLine($"BuildProjectSyntaxTree: Starting with {pSourceFiles.Count} files")
                
                ' Create root document node
                pProjectSyntaxTree = New SyntaxNode(CodeNodeType.eDocument, If(pCurrentProjectInfo?.ProjectName, "Project"))
                
                ' Get root namespace name
                Dim lRootNamespaceName As String = "SimpleIDE"
                If pCurrentProjectInfo IsNot Nothing Then
                    lRootNamespaceName = pCurrentProjectInfo.GetEffectiveRootNamespace()
                End If
                
                Console.WriteLine($"  Using root namespace: {lRootNamespaceName}")
                
                ' Create root namespace node
                Dim lRootNamespace As New SyntaxNode(CodeNodeType.eNamespace, lRootNamespaceName)
                lRootNamespace.IsImplicit = True
                pProjectSyntaxTree.AddChild(lRootNamespace)
                
                ' Dictionary to track namespace nodes for merging
                Dim lNamespaceNodes As New Dictionary(Of String, SyntaxNode)(StringComparer.OrdinalIgnoreCase)
                lNamespaceNodes(lRootNamespaceName) = lRootNamespace
                
                ' Process each source file
                Dim lFileCount As Integer = 0
                For Each lKvp In pSourceFiles
                    Dim lFileInfo As SourceFileInfo = lKvp.Value
                    
                    If lFileInfo.SyntaxTree IsNot Nothing Then
                        lFileCount += 1
                        Console.WriteLine($"  Processing file {lFileCount}: {lFileInfo.FileName}")
                        
                        ' Merge file's syntax tree into project tree
                        MergeFileIntoProjectTree(lFileInfo, lRootNamespace, lNamespaceNodes)
                    Else
                        Console.WriteLine($"  Skipping {lFileInfo.FileName} - no syntax tree")
                    End If
                Next
                
                ' Sort the entire tree
                Console.WriteLine("  Sorting project tree...")
                SortNodeChildrenRecursively(pProjectSyntaxTree)
                
                Console.WriteLine($"BuildProjectSyntaxTree: Complete. Processed {lFileCount} files")
                Console.WriteLine($"  Root namespace has {lRootNamespace.Children.Count} direct children")
                
                ' Raise the structure loaded event
                RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                
            Catch ex As Exception
                Console.WriteLine($"BuildProjectSyntaxTree error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Merge a file's syntax tree into the project tree
        ''' </summary>
        Private Sub MergeFileIntoProjectTree(vFileInfo As SourceFileInfo, vRootNamespace As SyntaxNode, vNamespaceNodes As Dictionary(Of String, SyntaxNode))
            Try
                If vFileInfo.SyntaxTree Is Nothing Then Return
                
                ' Process each root node in the file
                For Each lNode In vFileInfo.SyntaxTree.Children
                    ' Check if this is the implicit root namespace
                    If lNode.NodeType = CodeNodeType.eNamespace AndAlso lNode.IsImplicit Then
                        ' Merge its children directly into our root namespace
                        For Each lChild In lNode.Children
                            MergeNodeIntoProjectTree(lChild, vRootNamespace, vFileInfo.FilePath, vNamespaceNodes)
                        Next
                    ElseIf lNode.NodeType = CodeNodeType.eNamespace Then
                        ' This is an explicit namespace - find or create it
                        Dim lNamespacePath As String = lNode.Name
                        Dim lTargetNamespace As SyntaxNode = FindOrCreateNamespace(lNamespacePath, vRootNamespace, vNamespaceNodes)
                        
                        ' Merge the namespace contents
                        For Each lChild In lNode.Children
                            MergeNodeIntoProjectTree(lChild, lTargetNamespace, vFileInfo.FilePath, vNamespaceNodes)
                        Next
                    Else
                        ' This is a type at file level - add to root namespace
                        MergeNodeIntoProjectTree(lNode, vRootNamespace, vFileInfo.FilePath, vNamespaceNodes)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"MergeFileIntoProjectTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Find or create a namespace node in the tree
        ''' </summary>
        Private Function FindOrCreateNamespace(vNamespacePath As String, vRootNamespace As SyntaxNode, vNamespaceNodes As Dictionary(Of String, SyntaxNode)) As SyntaxNode
            Try
                ' Check if we already have this namespace
                If vNamespaceNodes.ContainsKey(vNamespacePath) Then
                    Return vNamespaceNodes(vNamespacePath)
                End If
                
                ' Parse the namespace path (e.g., "SimpleIDE.Utilities")
                Dim lParts() As String = vNamespacePath.Split("."c)
                Dim lCurrentParent As SyntaxNode = vRootNamespace
                Dim lCurrentPath As String = vRootNamespace.Name
                
                ' Skip the root namespace if it's in the path
                Dim lStartIndex As Integer = 0
                If lParts.Length > 0 AndAlso String.Equals(lParts(0), vRootNamespace.Name, StringComparison.OrdinalIgnoreCase) Then
                    lStartIndex = 1
                End If
                
                ' Create or find each namespace in the hierarchy
                For i As Integer = lStartIndex To lParts.Length - 1
                    If i > lStartIndex Then
                        lCurrentPath &= "."
                    End If
                    lCurrentPath &= lParts(i)
                    
                    ' Check if this namespace already exists
                    If vNamespaceNodes.ContainsKey(lCurrentPath) Then
                        lCurrentParent = vNamespaceNodes(lCurrentPath)
                    Else
                        ' Find or create this namespace level
                        Dim lFoundNamespace As SyntaxNode = Nothing
                        For Each lChild In lCurrentParent.Children
                            If lChild.NodeType = CodeNodeType.eNamespace AndAlso _
                               String.Equals(lChild.Name, lParts(i), StringComparison.OrdinalIgnoreCase) Then
                                lFoundNamespace = lChild
                                Exit For
                            End If
                        Next
                        
                        If lFoundNamespace Is Nothing Then
                            ' Create new namespace node
                            lFoundNamespace = New SyntaxNode(CodeNodeType.eNamespace, lParts(i))
                            lCurrentParent.AddChild(lFoundNamespace)
                        End If
                        
                        ' Add to dictionary
                        vNamespaceNodes(lCurrentPath) = lFoundNamespace
                        lCurrentParent = lFoundNamespace
                    End If
                Next
                
                Return lCurrentParent
                
            Catch ex As Exception
                Console.WriteLine($"FindOrCreateNamespace error: {ex.Message}")
                Return vRootNamespace
            End Try
        End Function
        
        ''' <summary>
        ''' Merge a node into the project tree
        ''' </summary>
        Private Sub MergeNodeIntoProjectTree(vNode As SyntaxNode, vParentNode As SyntaxNode, vFilePath As String, vNamespaceNodes As Dictionary(Of String, SyntaxNode))
            Try
                If vNode Is Nothing Then Return
                
                ' Handle partial classes - merge if already exists
                If vNode.NodeType = CodeNodeType.eClass AndAlso vNode.IsPartial Then
                    Dim lExistingClass As SyntaxNode = FindChildByNameAndType(vParentNode, vNode.Name, CodeNodeType.eClass)
                    
                    If lExistingClass IsNot Nothing Then
                        ' Merge members into existing class
                        lExistingClass.IsPartial = True
                        
                        ' Add all members that don't already exist
                        For Each lMember In vNode.Children
                            Dim lExistingMember As SyntaxNode = FindChildByNameAndType(lExistingClass, lMember.Name, lMember.NodeType)
                            If lExistingMember Is Nothing Then
                                Dim lNewMember As New SyntaxNode(lMember.NodeType, lMember.Name)
                                CopyNodeAttributes(lMember, lNewMember)
                                lNewMember.FilePath = vFilePath
                                lExistingClass.AddChild(lNewMember)
                            End If
                        Next
                    Else
                        ' Create new partial class
                        Dim lNewClass As New SyntaxNode(CodeNodeType.eClass, vNode.Name)
                        CopyNodeAttributes(vNode, lNewClass)
                        lNewClass.IsPartial = True
                        lNewClass.FilePath = vFilePath
                        vParentNode.AddChild(lNewClass)
                        
                        ' Add all members
                        For Each lChild In vNode.Children
                            MergeNodeIntoProjectTree(lChild, lNewClass, vFilePath, vNamespaceNodes)
                        Next
                    End If
                    
                Else
                    ' For non-partial classes and other types, check if already exists
                    Dim lExistingNode As SyntaxNode = FindChildByNameAndType(vParentNode, vNode.Name, vNode.NodeType)
                    
                    If lExistingNode Is Nothing Then
                        ' Create new node
                        Dim lNewNode As New SyntaxNode(vNode.NodeType, vNode.Name)
                        CopyNodeAttributes(vNode, lNewNode)
                        lNewNode.FilePath = vFilePath
                        vParentNode.AddChild(lNewNode)
                        
                        ' Add all children
                        For Each lChild In vNode.Children
                            MergeNodeIntoProjectTree(lChild, lNewNode, vFilePath, vNamespaceNodes)
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNodeIntoProjectTree error: {ex.Message}")
            End Try
        End Sub

        
' Replace: SimpleIDE.Managers.ProjectManager.CreateEmptyFile
Public Function CreateEmptyFile(vFileName As String) As SourceFileInfo
    Try
        ' Create path for new file
        Dim lFilePath As String = Path.Combine(pCurrentProjectInfo.ProjectDirectory, vFileName)
        
        ' Create new SourceFileInfo
        Dim lFileInfo As New SourceFileInfo(lFilePath, $"' {vFileName}" & Environment.NewLine & _
                           Environment.NewLine & _
                           "Imports System" & Environment.NewLine & _
                           Environment.NewLine & _
                           $"' Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" & Environment.NewLine & _
                           Environment.NewLine & _
                           "Namespace " & pCurrentProjectInfo.GetEffectiveRootNamespace() & Environment.NewLine & _
                           Environment.NewLine & _
                           "End Namespace")
                           
        
        ' Set project root namespace
        lFileInfo.ProjectRootNamespace = pCurrentProjectInfo.GetEffectiveRootNamespace()
        lFileInfo.ProjectManager = Me
        
        ' IMPORTANT: Wire up events for the new SourceFileInfo
        WireSourceFileInfoEvents(lFileInfo)

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
                
                ' Clear existing models
                ClearDocumentModels()
                
                ' Get list of all VB source files from CompileItems
                Console.WriteLine("Collecting source files from project...")
                Dim lSourceFiles As New List(Of String)()
                
                ' FIXED: Use CompileItems from the project file instead of scanning all .vb files
                ' This ensures we only parse files that are actually part of the compilation
                If lProjectInfo.CompileItems IsNot Nothing Then
                    for each lCompileItem in lProjectInfo.CompileItems
                        ' Convert relative path to absolute path
                        Dim lFullPath As String = Path.Combine(lProjectInfo.ProjectDirectory, lCompileItem)
                        
                        ' Normalize the path
                        lFullPath = Path.GetFullPath(lFullPath)
                        
                        ' Only add if the file exists and has .vb extension
                        If File.Exists(lFullPath) AndAlso Path.GetExtension(lFullPath).ToLower() = ".vb" Then
                            lSourceFiles.Add(lFullPath)
                            Console.WriteLine($"  Source file: {lCompileItem}")
                        ElseIf Not File.Exists(lFullPath) Then
                            Console.WriteLine($"  Warning: Source file not found: {lCompileItem}")
                        End If
                    Next
                Else
                    Console.WriteLine("  Warning: No CompileItems found in project file")
                End If
                
                Console.WriteLine($"Found {lSourceFiles.Count} source files to load")
                                
                ' Set counts for progress
                pTotalFilesToLoad = lSourceFiles.Count
                pFilesLoaded = 0
                
                ' Load and create DocumentModels for each file
                for each lFilePath in lSourceFiles
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
                for each lNode in vFileInfo.SyntaxTree.Children
                    Console.WriteLine($"  Processing node: {lNode.Name} ({lNode.NodeType})")
                    
                    ' Check if this is the implicit root namespace
                    If lNode.NodeType = CodeNodeType.eNamespace AndAlso 
                       lNode.IsImplicit AndAlso
                       String.Equals(lNode.Name, vRootNamespace.Name, StringComparison.OrdinalIgnoreCase) Then
                        ' This is the implicit root namespace - merge its children directly into our root
                        Console.WriteLine($"    Found implicit root namespace with {lNode.Children.Count} children")
                        for each lChild in lNode.Children
                            Console.WriteLine($"      Merging child: {lChild.Name} ({lChild.NodeType})")
                            ' Use the RelativePath property that we added to SourceFileInfo
                            MergeNodeIntoProjectFixed(lChild, vRootNamespace, GetRelativePath(vFileInfo))
                        Next
                    Else
                        ' This is a regular node - merge it
                        MergeNodeIntoProjectFixed(lNode, vRootNamespace, GetRelativePath(vFileInfo))
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"MergeFileStructureFixed error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub

        Public Function GetRelativePath(vFileInfo As SourceFileInfo) As String
            Return vFileInfo.FilePath.Substring(CurrentProjectPath.Length).TrimStart(Path.DirectorySeparatorChar)
        End Function

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
                
                ' Wire up all events including RequestProjectManager
                WireDocumentModelEvents(lModel)
                
                ' Parse the document - it will use the ProjectManager via event
                lModel.ParseDocument()
                
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
                
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RegisterSourceFileInfo error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the SourceFileInfo for a specific file path
        ''' </summary>
        ''' <param name="vFilePath">The file path to look up</param>
        ''' <returns>The SourceFileInfo if found, Nothing otherwise</returns>
        ''' <remarks>
        ''' Ensures colors are initialized if the file has metadata
        ''' </remarks>
        Public Function GetSourceFileInfo(vFilePath As String) As SourceFileInfo
            Try
                If String.IsNullOrEmpty(vFilePath) Then Return Nothing
                
                ' Normalize the path
                Dim lNormalizedPath As String = System.IO.Path.GetFullPath(vFilePath)
                
                ' Look up in our collection
                If pSourceFiles.ContainsKey(lNormalizedPath) Then
                    Dim lSourceFile As SourceFileInfo = pSourceFiles(lNormalizedPath)
                    
                    ' CRITICAL: If file has metadata but no colors, apply colors now
                    If lSourceFile IsNot Nothing AndAlso lSourceFile.LineMetadata IsNot Nothing AndAlso lSourceFile.LineMetadata.Length > 0 Then

                        ' TODO: implement initialization of the CharacterTokens arrays.
                        
                    End If
                    
                    Return lSourceFile
                End If
                
                ' Not found
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetSourceFileInfo error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' In ProjectManager
' Replace: SimpleIDE.Managers.ProjectManager.SaveFile
' In ProjectManager
Public Function SaveFile(vSourceFile As SourceFileInfo) As Boolean
    Try
        If vSourceFile Is Nothing Then Return False
        
        ' Save the file (this sets SourceFileInfo.IsModified = False)
        If vSourceFile.SaveContent() Then
            ' Raise event for UI updates
            ' This should trigger MainWindow to update any open tabs for this file
            RaiseEvent FileSaved(vSourceFile.FilePath)
            
            Console.WriteLine($"ProjectManager.SaveFile: Saved {vSourceFile.FilePath}")
            Return True
        End If
        
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
                
                for each lChild in vNamespace.Children
                    If Not lChildrenByType.ContainsKey(lChild.NodeType) Then
                        lChildrenByType(lChild.NodeType) = New List(Of String)()
                    End If
                    lChildrenByType(lChild.NodeType).Add(lChild.Name)
                Next
                
                ' Print summary of children by type
                for each lKvp in lChildrenByType.OrderBy(Function(k) k.Key.ToString())
                    Console.WriteLine($"{lIndentStr}  {lKvp.Key}: {String.Join(", ", lKvp.Value)}")
                Next
                
                ' For classes and modules, show their nested types
                for each lChild in vNamespace.Children
                    If lChild.NodeType = CodeNodeType.eClass OrElse lChild.NodeType = CodeNodeType.eModule Then
                        Console.WriteLine($"{lIndentStr}  [{lChild.NodeType}] {lChild.Name} contains:")
                        
                        Dim lNestedTypes As New List(Of String)()
                        for each lNested in lChild.Children
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
                for each lChild in vNamespace.Children
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
                    for each lChild in pProjectSyntaxTree.Children
                        If lChild.NodeType = CodeNodeType.eNamespace AndAlso lChild.Name = "SimpleIDE" Then
                            DiagnoseNamespaceStructure(lChild, 0)
                            
                            ' Special check for Syntax namespace
                            for each lSubNamespace in lChild.Children
                                If lSubNamespace.NodeType = CodeNodeType.eNamespace AndAlso lSubNamespace.Name = "Syntax" Then
                                    Console.WriteLine("")
                                    Console.WriteLine("=== DETAILED SYNTAX NAMESPACE ANALYSIS ===")
                                    Console.WriteLine($"Syntax namespace has {lSubNamespace.Children.Count} direct children")
                                    
                                    ' Look for duplicates
                                    Dim lNameCounts As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                                    for each lSyntaxChild in lSubNamespace.Children
                                        Dim lKey As String = $"{lSyntaxChild.NodeType}:{lSyntaxChild.Name}"
                                        If Not lNameCounts.ContainsKey(lKey) Then
                                            lNameCounts(lKey) = 0
                                        End If
                                        lNameCounts(lKey) += 1
                                    Next
                                    
                                    ' Report duplicates
                                    Console.WriteLine("Duplicates found:")
                                    for each lKvp in lNameCounts.Where(Function(k) k.Value > 1)
                                        Console.WriteLine($"  {lKvp.Key} appears {lKvp.Value} times")
                                    Next
                                    
                                    ' Check if BlockInfo is in namespace children
                                    Dim lBlockInfoCount As Integer = 0
                                    for each lSyntaxChild in lSubNamespace.Children
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