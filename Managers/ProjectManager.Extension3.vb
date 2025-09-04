' ProjectManager.Extension3.vb
' Created: 2025-08-18 08:16:26

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
        ''' Maps namespace names to their SyntaxNode representations
        ''' </summary>
        Private pNamespaceIndex As Dictionary(Of String, SyntaxNode)
        
        ''' <summary>
        ''' Maps type names to their SyntaxNode representations
        ''' </summary>
        Private pTypeIndex As Dictionary(Of String, SyntaxNode)
        
        ''' <summary>
        ''' Maps member names to lists of SyntaxNodes (multiple members can have same name)
        ''' </summary>
        Private pMemberIndex As Dictionary(Of String, List(Of SyntaxNode))

        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Gets all source files in the project
        ''' </summary>
        ''' <returns>List of source file paths</returns>
        Public Function GetProjectSourceFiles() As List(Of String)
            Try
                Dim lSourceFiles As New List(Of String)()
                
                If pCurrentProjectInfo Is Nothing Then
                    Return lSourceFiles
                End If
                
                Dim lProjectDir As String = pCurrentProjectInfo.ProjectDirectory
                If String.IsNullOrEmpty(lProjectDir) OrElse Not Directory.Exists(lProjectDir) Then
                    Return lSourceFiles
                End If
                
                ' FIXED: Use CompileItems from the project file instead of scanning all .vb files
                ' This ensures we only parse files that are actually part of the compilation
                If pCurrentProjectInfo.CompileItems IsNot Nothing Then
                    for each lCompileItem in pCurrentProjectInfo.CompileItems
                        ' Convert relative path to absolute path
                        Dim lFullPath As String = Path.Combine(lProjectDir, lCompileItem)
                        
                        ' Normalize the path
                        lFullPath = Path.GetFullPath(lFullPath)
                        
                        ' Only add if the file exists
                        If File.Exists(lFullPath) Then
                            lSourceFiles.Add(lFullPath)
                            Console.WriteLine($"  Adding source file: {lCompileItem}")
                        Else
                            Console.WriteLine($"  Warning: Source file not found: {lCompileItem}")
                        End If
                    Next
                Else
                    Console.WriteLine("  Warning: No CompileItems found in project info")
                End If
                
                Console.WriteLine($"GetProjectSourceFiles: Found {lSourceFiles.Count} source files")
                Return lSourceFiles
                
            Catch ex As Exception
                Console.WriteLine($"GetProjectSourceFiles error: {ex.Message}")
                Return New List(Of String)()
            End Try
        End Function
        
        ''' <summary>
        ''' Helper method to log namespace structure for debugging
        ''' </summary>
        Private Sub LogNamespaceStructure(vNode As SyntaxNode, vIndent As Integer)
            Try
                Dim lIndentStr As String = New String(" "c, vIndent * 2)
                
                ' Count children by type
                Dim lNamespaceCount As Integer = vNode.Children.Where(Function(c) c.NodeType = CodeNodeType.eNamespace).Count()
                Dim lClassCount As Integer = vNode.Children.Where(Function(c) c.NodeType = CodeNodeType.eClass).Count()
                Dim lOtherCount As Integer = vNode.Children.Count - lNamespaceCount - lClassCount

                Console.WriteLine($"{lIndentStr}{vNode.Name} ({vNode.NodeType}): " &
                                 $"{lNamespaceCount} namespaces, {lClassCount} classes, {lOtherCount} others")
                
                ' Log child namespaces only (to avoid too much output)
                for each lChild in vNode.Children
                    If lChild.NodeType = CodeNodeType.eNamespace Then
                        LogNamespaceStructure(lChild, vIndent + 1)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"LogNamespaceStructure error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper method that might be missing =====
        
        
        ''' <summary>
        ''' Build namespace index from project tree
        ''' </summary>
        Private Sub BuildNamespaceIndex()
            Try
                pNamespaceIndex.Clear()
                
                If pProjectSyntaxTree IsNot Nothing Then
                    BuildNamespaceIndexRecursive(pProjectSyntaxTree)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildNamespaceIndex error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively build namespace index
        ''' </summary>
        Private Sub BuildNamespaceIndexRecursive(vNode As SyntaxNode)
            Try
                If vNode.NodeType = CodeNodeType.eNamespace Then
                    pNamespaceIndex(vNode.Name) = vNode
                End If
                
                for each lChild in vNode.Children
                    BuildNamespaceIndexRecursive(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"BuildNamespaceIndexRecursive error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Build type index from project tree
        ''' </summary>
        Private Sub BuildTypeIndex()
            Try
                pTypeIndex.Clear()
                
                If pProjectSyntaxTree IsNot Nothing Then
                    BuildTypeIndexRecursive(pProjectSyntaxTree)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"BuildTypeIndex error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively build type index
        ''' </summary>
        Private Sub BuildTypeIndexRecursive(vNode As SyntaxNode)
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eModule, 
                         CodeNodeType.eStructure, CodeNodeType.eInterface, 
                         CodeNodeType.eEnum
                        pTypeIndex(vNode.Name) = vNode
                End Select
                
                for each lChild in vNode.Children
                    BuildTypeIndexRecursive(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"BuildTypeIndexRecursive error: {ex.Message}")
            End Try
        End Sub
 
        ''' <summary>
        ''' Process a file's syntax tree and merge it into the project namespace structure
        ''' </summary>
        ''' <param name="vFileInfo">The source file information to process</param>
        ''' <param name="vRootNamespace">The root namespace node of the project</param>
        ''' <param name="vNamespaceNodes">Dictionary tracking namespace nodes</param>
        ''' <param name="vRootNamespaceName">Name of the root namespace</param>
        Private Sub ProcessFileStructure(vFileInfo As SourceFileInfo, 
                                        vRootNamespace As SyntaxNode, 
                                        vNamespaceNodes As Dictionary(Of String, SyntaxNode),
                                        vRootNamespaceName As String)
            Try
                If vFileInfo Is Nothing OrElse vFileInfo.SyntaxTree Is Nothing Then
                    Console.WriteLine($"ProcessFileStructure: No syntax tree for {vFileInfo?.FileName}")
                    Return
                End If
                
                Console.WriteLine($"Processing file structure: {vFileInfo.FileName}")
                
                ' Process each top-level node in the file's syntax tree
                for each lNode in vFileInfo.SyntaxTree.Children
                    
                    ' Check if this is a namespace node
                    If lNode.NodeType = CodeNodeType.eNamespace Then
                        
                        ' Handle implicit root namespace
                        If lNode.IsImplicit AndAlso 
                           String.Equals(lNode.Name, vRootNamespaceName, StringComparison.OrdinalIgnoreCase) Then
                            
                            ' This is the implicit root namespace - merge its children directly
                            Console.WriteLine($"  Merging implicit root namespace children")
                            for each lChild in lNode.Children
                                MergeNodeIntoNamespace(lChild, vRootNamespace, vFileInfo.FilePath)
                            Next
                            
                        Else
                            ' This is an explicit namespace declaration
                            Dim lNamespaceName As String = lNode.Name
                            Console.WriteLine($"  Processing namespace: {lNamespaceName}")
                            
                            ' CRITICAL FIX: Check if namespace already exists in the ROOT namespace
                            ' not just in vNamespaceNodes dictionary
                            Dim lNamespaceNode As SyntaxNode = Nothing
                            
                            ' First check if it already exists as a child of root
                            for each lRootChild in vRootNamespace.Children
                                If lRootChild.NodeType = CodeNodeType.eNamespace AndAlso
                                   String.Equals(lRootChild.Name, lNamespaceName, StringComparison.OrdinalIgnoreCase) Then
                                    lNamespaceNode = lRootChild
                                    Console.WriteLine($"    Found existing namespace in root: {lNamespaceName}")
                                    Exit for
                                End If
                            Next
                            
                            ' If not found in root children, check dictionary
                            If lNamespaceNode Is Nothing AndAlso vNamespaceNodes.ContainsKey(lNamespaceName) Then
                                lNamespaceNode = vNamespaceNodes(lNamespaceName)
                                Console.WriteLine($"    Found namespace in dictionary: {lNamespaceName}")
                            End If
                            
                            ' Create new namespace node only if it doesn't exist
                            If lNamespaceNode Is Nothing Then
                                Console.WriteLine($"    Creating new namespace: {lNamespaceName}")
                                lNamespaceNode = New SyntaxNode(CodeNodeType.eNamespace, lNamespaceName)
                                
                                ' Initialize attributes
                                If lNamespaceNode.Attributes Is Nothing Then
                                    lNamespaceNode.Attributes = New Dictionary(Of String, String)()
                                End If
                                lNamespaceNode.Attributes("FilePath") = vFileInfo.FilePath
                                
                                vRootNamespace.AddChild(lNamespaceNode)
                                vNamespaceNodes(lNamespaceName) = lNamespaceNode
                            Else
                                ' Update file paths for existing namespace
                                If lNamespaceNode.Attributes Is Nothing Then
                                    lNamespaceNode.Attributes = New Dictionary(Of String, String)()
                                End If
                                
                                If Not lNamespaceNode.Attributes.ContainsKey("FilePaths") Then
                                    lNamespaceNode.Attributes("FilePaths") = vFileInfo.FilePath
                                Else
                                    Dim lPaths As String = lNamespaceNode.Attributes("FilePaths")
                                    If Not lPaths.Contains(vFileInfo.FilePath) Then
                                        lNamespaceNode.Attributes("FilePaths") = lPaths & ";" & vFileInfo.FilePath
                                    End If
                                End If
                            End If
                            
                            ' Merge the namespace contents
                            for each lChild in lNode.Children
                                MergeNodeIntoNamespace(lChild, lNamespaceNode, vFileInfo.FilePath)
                            Next
                        End If
                        
                    Else
                        ' Non-namespace top-level node (class, module, etc.)
                        ' These go directly into the root namespace
                        Console.WriteLine($"  Processing top-level {lNode.NodeType}: {lNode.Name}")
                        MergeNodeIntoNamespace(lNode, vRootNamespace, vFileInfo.FilePath)
                    End If
                Next
                
                Console.WriteLine($"Completed processing: {vFileInfo.FileName}")
                
            Catch ex As Exception
                Console.WriteLine($"ProcessFileStructure error: {ex.Message}")
                Console.WriteLine($"  File: {vFileInfo?.FileName}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to merge a node into a namespace, handling duplicates
        ''' </summary>
        ''' <param name="vNode">Node to merge</param>
        ''' <param name="vNamespaceNode">Target namespace node</param>
        ''' <param name="vFilePath">Source file path for tracking</param>
        Private Sub MergeNodeIntoNamespace(vNode As SyntaxNode, 
                                          vNamespaceNode As SyntaxNode, 
                                          vFilePath As String)
            Try
                If vNode Is Nothing OrElse vNamespaceNode Is Nothing Then Return
                
                ' DEBUG: Log merge operation
                Console.WriteLine($"    Merging {vNode.NodeType}: {vNode.Name} into namespace: {vNamespaceNode.Name}")
                
                ' Check if a node with this name and type already exists (partial classes)
                Dim lExistingNode As SyntaxNode = Nothing
                
                for each lChild in vNamespaceNode.Children
                    If String.Equals(lChild.Name, vNode.Name, StringComparison.OrdinalIgnoreCase) AndAlso
                       lChild.NodeType = vNode.NodeType Then
                        lExistingNode = lChild
                        Exit for
                    End If
                Next
                
                If lExistingNode IsNot Nothing Then
                    ' Merge into existing node (partial class scenario)
                    Console.WriteLine($"    Found existing {vNode.NodeType}: {vNode.Name} - merging members")
                    
                    ' Mark as partial
                    lExistingNode.IsPartial = True
                    
                    ' Initialize Attributes if needed
                    If lExistingNode.Attributes Is Nothing Then
                        lExistingNode.Attributes = New Dictionary(Of String, String)()
                    End If
                    
                    ' Track file paths
                    If Not lExistingNode.Attributes.ContainsKey("FilePaths") Then
                        lExistingNode.Attributes("FilePaths") = vFilePath
                    Else
                        Dim lPaths As String = lExistingNode.Attributes("FilePaths")
                        If Not lPaths.Contains(vFilePath) Then
                            lExistingNode.Attributes("FilePaths") = lPaths & ";" & vFilePath
                        End If
                    End If
                    
                    ' Merge members (check for duplicates)
                    Dim lMergedCount As Integer = 0
                    Dim lDuplicateCount As Integer = 0
                    
                    for each lMember in vNode.Children
                        ' Check if member already exists
                        Dim lExistingMember As SyntaxNode = FindChildByNameAndType(lExistingNode, lMember.Name, lMember.NodeType)
                        
                        If lExistingMember Is Nothing Then
                            ' Create new member node with deep copy
                            Dim lNewMember As New SyntaxNode(lMember.NodeType, lMember.Name)
                            lMember.CopyNodeAttributesTo(lNewMember)
                            
                            ' Initialize and set file path for member
                            If lNewMember.Attributes Is Nothing Then
                                lNewMember.Attributes = New Dictionary(Of String, String)()
                            End If
                            lNewMember.Attributes("FilePath") = vFilePath
                            
                            ' IMPORTANT: Copy ALL children of the member (parameters, etc.)
                            ' but DON'T recursively call MergeNodeIntoNamespace
                            for each lMemberChild in lMember.Children
                                lNewMember.AddChild(lMemberChild.Clone())
                            Next
                            
                            ' Add to existing node
                            lExistingNode.AddChild(lNewMember)
                            lMergedCount += 1
                        Else
                            lDuplicateCount += 1
                        End If
                    Next
                    
                    Console.WriteLine($"      Merged {lMergedCount} new members, skipped {lDuplicateCount} duplicates")
                    Console.WriteLine($"      Total members in merged class: {lExistingNode.Children.Count}")
                    
                Else
                    ' Create new node
                    Console.WriteLine($"    Creating new {vNode.NodeType}: {vNode.Name}")
                    
                    Dim lNewNode As New SyntaxNode(vNode.NodeType, vNode.Name)
                    vNode.CopyNodeAttributesTo(lNewNode)
                    
                    ' Mark as partial if appropriate
                    lNewNode.IsPartial = vNode.IsPartial
                    
                    ' Initialize and set file path
                    If lNewNode.Attributes Is Nothing Then
                        lNewNode.Attributes = New Dictionary(Of String, String)()
                    End If
                    lNewNode.Attributes("FilePath") = vFilePath
                    If lNewNode.IsPartial Then
                        lNewNode.Attributes("FilePaths") = vFilePath
                    End If
                    
                    ' Add to namespace
                    vNamespaceNode.AddChild(lNewNode)
                    
                    ' IMPORTANT: Add all children directly without recursive processing
                    ' Children of a class/module/interface are members and should be copied as-is
                    for each lChild in vNode.Children
                        ' Check if this is a namespace-level node type that needs merging
                        If lChild.NodeType = CodeNodeType.eNamespace OrElse
                           lChild.NodeType = CodeNodeType.eClass OrElse
                           lChild.NodeType = CodeNodeType.eModule OrElse
                           lChild.NodeType = CodeNodeType.eInterface OrElse
                           lChild.NodeType = CodeNodeType.eStructure OrElse
                           lChild.NodeType = CodeNodeType.eEnum OrElse
                           lChild.NodeType = CodeNodeType.eDelegate Then
                            ' These are container types that might need merging
                            MergeNodeIntoNamespace(lChild, lNewNode, vFilePath)
                        Else
                            ' These are members - copy them directly with all their children
                            Dim lNewMember As New SyntaxNode(lChild.NodeType, lChild.Name)
                            lChild.CopyNodeAttributesTo(lNewMember)
                            
                            If lNewMember.Attributes Is Nothing Then
                                lNewMember.Attributes = New Dictionary(Of String, String)()
                            End If
                            lNewMember.Attributes("FilePath") = vFilePath
                            
                            ' Copy all children of the member (like parameters)
                            for each lGrandChild in lChild.Children
                                lNewMember.AddChild(lGrandChild.Clone())
                            Next
                            
                            lNewNode.AddChild(lNewMember)
                        End If
                    Next
                    
                    Console.WriteLine($"      Added with {lNewNode.Children.Count} members")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNodeIntoNamespace error: {ex.Message}")
                Console.WriteLine($"  Node: {vNode?.Name} ({vNode?.NodeType})")
            End Try
        End Sub
        
        ' ===== Initialize Index Methods =====
        ' Add this to the constructor or initialization method
        
        ''' <summary>
        ''' Initialize the index dictionaries
        ''' </summary>
        Private Sub InitializeIndices()
            Try
                pNamespaceIndex = New Dictionary(Of String, SyntaxNode)(StringComparer.OrdinalIgnoreCase)
                pTypeIndex = New Dictionary(Of String, SyntaxNode)(StringComparer.OrdinalIgnoreCase)
                pMemberIndex = New Dictionary(Of String, List(Of SyntaxNode))(StringComparer.OrdinalIgnoreCase)
                
            Catch ex As Exception
                Console.WriteLine($"InitializeIndices error: {ex.Message}")
            End Try
        End Sub       

        ''' <summary>
        ''' Ensures all project source files are loaded into memory
        ''' </summary>
        ''' <returns>Number of files loaded</returns>
        Public Function EnsureAllFilesLoaded() As Integer
            Try
                Dim lLoadedCount As Integer = 0
                
                If pCurrentProjectInfo Is Nothing Then
                    Console.WriteLine("EnsureAllFilesLoaded: No project loaded")
                    Return 0
                End If
                
                ' Get list of all compile items from project
                Dim lCompileItems As List(Of String) = pCurrentProjectInfo.CompileItems
                If lCompileItems Is Nothing Then
                    Console.WriteLine("EnsureAllFilesLoaded: No compile items found")
                    Return 0
                End If
                
                Console.WriteLine($"EnsureAllFilesLoaded: Checking {lCompileItems.Count} compile items")
                
                for each lRelativePath in lCompileItems
                    Try
                        ' Convert to absolute path
                        Dim lFullPath As String = System.IO.Path.Combine(
                            pCurrentProjectInfo.ProjectDirectory, 
                            lRelativePath
                        )
                        lFullPath = System.IO.Path.GetFullPath(lFullPath)
                        
                        ' Check if file exists
                        If Not System.IO.File.Exists(lFullPath) Then
                            Console.WriteLine($"  File not found: {lRelativePath}")
                            Continue for
                        End If
                        
                        ' Check if already loaded
                        If pSourceFiles.ContainsKey(lFullPath) Then
                            Dim lSourceFile As SourceFileInfo = pSourceFiles(lFullPath)
                            If lSourceFile.IsLoaded AndAlso lSourceFile.TextLines IsNot Nothing Then
                                lLoadedCount += 1
                                Continue for
                            End If
                        End If
                        
                        ' Load the file
                        Console.WriteLine($"  Loading: {lRelativePath}")
                        Dim lNewSourceFile As New SourceFileInfo(lFullPath, "", pCurrentProjectInfo.ProjectDirectory)
                        
                        ' Read file content
                        Dim lContent As String = System.IO.File.ReadAllText(lFullPath)
                        lNewSourceFile.Content = lContent
                        lNewSourceFile.TextLines = New List(Of String)(
                            lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                        )
                        If lNewSourceFile.TextLines.Count = 0 Then
                            lNewSourceFile.TextLines.Add("")
                        End If
                        lNewSourceFile.IsLoaded = True
                        lNewSourceFile.RelativePath = lRelativePath
                        
                        ' Add or update in dictionary
                        pSourceFiles(lFullPath) = lNewSourceFile
                        lLoadedCount += 1
                        
                    Catch ex As Exception
                        Console.WriteLine($"  Error loading {lRelativePath}: {ex.Message}")
                    End Try
                Next
                
                Console.WriteLine($"EnsureAllFilesLoaded: {lLoadedCount} files loaded successfully")
                Return lLoadedCount
                
            Catch ex As Exception
                Console.WriteLine($"EnsureAllFilesLoaded error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Gets statistics about loaded files
        ''' </summary>
        ''' <returns>Tuple of (TotalFiles, LoadedFiles, TotalLines)</returns>
        Public Function GetLoadedFileStats() As (TotalFiles As Integer, LoadedFiles As Integer, TotalLines As Integer)
            Try
                Dim lTotalFiles As Integer = pSourceFiles.Count
                Dim lLoadedFiles As Integer = 0
                Dim lTotalLines As Integer = 0
                
                for each lEntry in pSourceFiles
                    If lEntry.Value.IsLoaded AndAlso lEntry.Value.TextLines IsNot Nothing Then
                        lLoadedFiles += 1
                        lTotalLines += lEntry.Value.TextLines.Count
                    End If
                Next
                
                Return (lTotalFiles, lLoadedFiles, lTotalLines)
                
            Catch ex As Exception
                Console.WriteLine($"GetLoadedFileStats error: {ex.Message}")
                Return (0, 0, 0)
            End Try
        End Function

        ''' <summary>
        ''' Checks if a file is loaded in memory
        ''' </summary>
        ''' <param name="vFilePath">The file path to check</param>
        ''' <returns>True if the file is loaded, False otherwise</returns>
        Public Function IsFileLoaded(vFilePath As String) As Boolean
            Try
                Return pSourceFiles.ContainsKey(vFilePath) AndAlso 
                       pSourceFiles(vFilePath).IsLoaded AndAlso
                       pSourceFiles(vFilePath).TextLines IsNot Nothing
                       
            Catch ex As Exception
                Console.WriteLine($"IsFileLoaded error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Dumps the project syntax tree structure for diagnostic purposes
        ''' </summary>
        ''' <param name="vIncludeMembers">Whether to include class members in the dump</param>
        ''' <returns>String representation of the tree structure</returns>
        Public Function DiagnosticTreeDump(Optional vIncludeMembers As Boolean = False) As String
            Try
                Dim lResult As New System.Text.StringBuilder()
                lResult.AppendLine("=== PROJECT SYNTAX TREE DUMP ===")
                
                If pProjectSyntaxTree Is Nothing Then
                    lResult.AppendLine("No project syntax tree available")
                    Return lResult.ToString()
                End If
                
                ' Dump the tree recursively
                DumpNodeRecursive(pProjectSyntaxTree, lResult, 0, vIncludeMembers)
                
                lResult.AppendLine("=== END DUMP ===")
                Return lResult.ToString()
                
            Catch ex As Exception
                Return $"DiagnosticTreeDump error: {ex.Message}"
            End Try
        End Function
        
        ''' <summary>
        ''' Recursively dumps a node and its children
        ''' </summary>
        Private Sub DumpNodeRecursive(vNode As SyntaxNode, 
                                     vBuilder As System.Text.StringBuilder, 
                                     vIndent As Integer,
                                     vIncludeMembers As Boolean)
            Try
                If vNode Is Nothing Then Return
                
                Dim lIndentStr As String = New String(" "c, vIndent * 2)
                
                ' Build node info
                Dim lNodeInfo As String = $"{lIndentStr}{vNode.NodeType}: {vNode.Name}"
                
                ' Add partial class info
                If vNode.NodeType = CodeNodeType.eClass AndAlso vNode.IsPartial Then
                    lNodeInfo &= " [PARTIAL]"
                    
                    ' Show file paths if available
                    If vNode.Attributes IsNot Nothing AndAlso vNode.Attributes.ContainsKey("FilePaths") Then
                        Dim lFilePaths As String = vNode.Attributes("FilePaths")
                        Dim lFiles As String() = lFilePaths.Split(";"c)
                        lNodeInfo &= $" ({lFiles.Length} files)"
                    End If
                End If
                
                ' Add child count
                If vNode.Children.Count > 0 Then
                    lNodeInfo &= $" ({vNode.Children.Count} children)"
                End If
                
                vBuilder.AppendLine(lNodeInfo)
                
                ' Process children based on node type
                for each lChild in vNode.Children
                    ' Skip members if not requested (unless it's a namespace or type)
                    Dim lShouldInclude As Boolean = True
                    
                    If Not vIncludeMembers Then
                        Select Case lChild.NodeType
                            Case CodeNodeType.eMethod, CodeNodeType.eFunction, 
                                 CodeNodeType.eProperty, CodeNodeType.eField,
                                 CodeNodeType.eEvent, CodeNodeType.eConstructor,
                                 CodeNodeType.eConstant, CodeNodeType.eConst
                                lShouldInclude = False
                        End Select
                    End If
                    
                    If lShouldInclude Then
                        DumpNodeRecursive(lChild, vBuilder, vIndent + 1, vIncludeMembers)
                    End If
                Next
                
                ' If we're skipping members, show a count
                If Not vIncludeMembers AndAlso 
                   (vNode.NodeType = CodeNodeType.eClass OrElse 
                    vNode.NodeType = CodeNodeType.eModule OrElse
                    vNode.NodeType = CodeNodeType.eInterface) Then
                    
                    Dim lMemberCount As Integer = 0
                    for each lChild in vNode.Children
                        Select Case lChild.NodeType
                            Case CodeNodeType.eMethod, CodeNodeType.eFunction,
                                 CodeNodeType.eProperty, CodeNodeType.eField,
                                 CodeNodeType.eEvent, CodeNodeType.eConstructor,
                                 CodeNodeType.eConstant, CodeNodeType.eConst
                                lMemberCount += 1
                        End Select
                    Next
                    
                    If lMemberCount > 0 Then
                        vBuilder.AppendLine($"{lIndentStr}  ... {lMemberCount} members")
                    End If
                End If
                
            Catch ex As Exception
                vBuilder.AppendLine($"{New String(" "c, vIndent * 2)}ERROR: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Verifies that namespaces are properly merged without duplicates
        ''' </summary>
        ''' <returns>True if structure is valid, False if duplicates found</returns>
        Public Function VerifyNamespaceMerge() As Boolean
            Try
                Console.WriteLine("=== VERIFYING NAMESPACE MERGE ===")
                
                If pProjectSyntaxTree Is Nothing Then
                    Console.WriteLine("No project tree to verify")
                    Return False
                End If
                
                Dim lHasDuplicates As Boolean = False
                
                ' Check each level for duplicate namespaces
                VerifyNodeChildren(pProjectSyntaxTree, "", lHasDuplicates)
                
                If Not lHasDuplicates Then
                    Console.WriteLine("✓ No duplicate namespaces found - merge successful!")
                Else
                    Console.WriteLine("✗ Duplicate namespaces detected - merge failed!")
                End If
                
                Console.WriteLine("=== END VERIFICATION ===")
                Return Not lHasDuplicates
                
            Catch ex As Exception
                Console.WriteLine($"VerifyNamespaceMerge error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Recursively verify node children for duplicates
        ''' </summary>
        Private Sub VerifyNodeChildren(vNode As SyntaxNode, vPath As String, ByRef vHasDuplicates As Boolean)
            Try
                ' Build current path
                Dim lCurrentPath As String = vPath
                If Not String.IsNullOrEmpty(vPath) Then
                    lCurrentPath &= "."
                End If
                lCurrentPath &= vNode.Name
                
                ' Check for duplicate children
                Dim lChildrenByName As New Dictionary(Of String, List(Of SyntaxNode))(StringComparer.OrdinalIgnoreCase)
                
                for each lChild in vNode.Children
                    Dim lKey As String = $"{lChild.Name}_{lChild.NodeType}"
                    
                    If Not lChildrenByName.ContainsKey(lKey) Then
                        lChildrenByName(lKey) = New List(Of SyntaxNode)()
                    End If
                    lChildrenByName(lKey).Add(lChild)
                Next
                
                ' Report any duplicates
                for each lKvp in lChildrenByName
                    If lKvp.Value.Count > 1 Then
                        Dim lParts() As String = lKvp.Key.Split("_"c)
                        Dim lName As String = lParts(0)
                        Dim lType As String = If(lParts.Length > 1, lParts(1), "Unknown")
                        
                        Console.WriteLine($"  DUPLICATE: {lCurrentPath} has {lKvp.Value.Count} '{lName}' nodes of type {lType}")
                        vHasDuplicates = True
                        
                        ' Show file paths for each duplicate
                        Dim lIndex As Integer = 1
                        for each lDupNode in lKvp.Value
                            Dim lFilePath As String = "Unknown"
                            If lDupNode.Attributes IsNot Nothing Then
                                If lDupNode.Attributes.ContainsKey("FilePath") Then
                                    lFilePath = lDupNode.Attributes("FilePath")
                                ElseIf lDupNode.Attributes.ContainsKey("FilePaths") Then
                                    lFilePath = lDupNode.Attributes("FilePaths")
                                End If
                            End If
                            Console.WriteLine($"    [{lIndex}] Children: {lDupNode.Children.Count}, Files: {lFilePath}")
                            lIndex += 1
                        Next
                    End If
                Next
                
                ' Recursively check children
                for each lChild in vNode.Children
                    If lChild.NodeType = CodeNodeType.eNamespace OrElse
                       lChild.NodeType = CodeNodeType.eDocument Then
                        VerifyNodeChildren(lChild, lCurrentPath, vHasDuplicates)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"VerifyNodeChildren error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Diagnoses why MainWindow and Program aren't appearing in the root namespace
        ''' </summary>
        Public Sub DiagnoseRootClasses()
            Try
                Console.WriteLine("=== DIAGNOSING ROOT CLASSES (MainWindow, Program) ===")
                
                ' Check if we have the project tree
                If pProjectSyntaxTree Is Nothing Then
                    Console.WriteLine("No project tree loaded")
                    Return
                End If
                
                ' Find the root namespace
                Dim lRootNamespace As SyntaxNode = Nothing
                For Each lChild In pProjectSyntaxTree.Children
                    If lChild.NodeType = CodeNodeType.eNamespace AndAlso lChild.Name = "SimpleIDE" Then
                        lRootNamespace = lChild
                        Exit For
                    End If
                Next
                
                If lRootNamespace Is Nothing Then
                    Console.WriteLine("Root Namespace 'SimpleIDE' not found")
                    Return
                End If
                
                Console.WriteLine($"Root namespace found with {lRootNamespace.Children.Count} children:")
                
                ' List all direct children of root namespace
                Dim lClasses As New List(Of SyntaxNode)()
                Dim lModules As New List(Of SyntaxNode)()
                Dim lNamespaces As New List(Of SyntaxNode)()
                Dim lOther As New List(Of SyntaxNode)()
                
                for each lChild in lRootNamespace.Children
                    Select Case lChild.NodeType
                        Case CodeNodeType.eClass
                            lClasses.Add(lChild)
                        Case CodeNodeType.eModule  
                            lModules.Add(lChild)
                        Case CodeNodeType.eNamespace
                            lNamespaces.Add(lChild)
                        Case Else
                            lOther.Add(lChild)
                    End Select
                Next
                
                Console.WriteLine($"  Namespaces: {lNamespaces.Count}")
                for each lNs in lNamespaces
                    Console.WriteLine($"    - {lNs.Name}")
                Next
                
                Console.WriteLine($"  Classes: {lClasses.Count}")
                for each lClass in lClasses
                    Dim lPartialInfo As String = If(lClass.IsPartial, " [PARTIAL]", "")
                    Dim lFileInfo As String = ""
                    If lClass.Attributes IsNot Nothing AndAlso lClass.Attributes.ContainsKey("FilePaths") Then
                        lFileInfo = $" Files: {lClass.Attributes("FilePaths")}"
                    End If
                    Console.WriteLine($"    - {lClass.Name}{lPartialInfo}{lFileInfo}")
                Next
                
                Console.WriteLine($"  Modules: {lModules.Count}")
                for each lModule in lModules
                    Dim lFileInfo As String = ""
                    If lModule.Attributes IsNot Nothing AndAlso lModule.Attributes.ContainsKey("FilePath") Then
                        lFileInfo = $" File: {lModule.Attributes("FilePath")}"
                    End If
                    Console.WriteLine($"    - {lModule.Name}{lFileInfo}")
                Next
                
                If lOther.Count > 0 Then
                    Console.WriteLine($"  Other: {lOther.Count}")
                    for each lNode in lOther
                        Console.WriteLine($"    - {lNode.Name} ({lNode.NodeType})")
                    Next
                End If
                
                ' Now check specific files
                Console.WriteLine()
                Console.WriteLine("Checking specific source files:")
                
                ' Check MainWindow.vb
                CheckSourceFile("MainWindow.vb")
                CheckSourceFile("Program.vb")
                
                Console.WriteLine("=== END DIAGNOSIS ===")
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseRootClasses error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper to check a specific source file's parse results
        ''' </summary>
        Private Sub CheckSourceFile(vFileName As String)
            Try
                Console.WriteLine($"  Checking {vFileName}:")
                
                ' Find the file in pSourceFiles
                Dim lFileInfo As SourceFileInfo = Nothing
                for each lKvp in pSourceFiles
                    If lKvp.Value.FileName.Equals(vFileName, StringComparison.OrdinalIgnoreCase) Then
                        lFileInfo = lKvp.Value
                        Exit for
                    End If
                Next
                
                If lFileInfo Is Nothing Then
                    Console.WriteLine($"    NOT FOUND in source files")
                    Return
                End If
                
                Console.WriteLine($"    Found: {lFileInfo.FilePath}")
                Console.WriteLine($"    Parsed: {lFileInfo.IsParsed}")
                
                If lFileInfo.SyntaxTree IsNot Nothing Then
                    Console.WriteLine($"    SyntaxTree children: {lFileInfo.SyntaxTree.Children.Count}")
                    for each lChild in lFileInfo.SyntaxTree.Children
                        Console.WriteLine($"      - {lChild.Name} ({lChild.NodeType})")
                        If lChild.NodeType = CodeNodeType.eNamespace Then
                            Console.WriteLine($"        IsImplicit: {lChild.IsImplicit}")
                            Console.WriteLine($"        Children: {lChild.Children.Count}")
                            for each lSubChild in lChild.Children
                                Console.WriteLine($"          - {lSubChild.Name} ({lSubChild.NodeType})")
                            Next
                        End If
                    Next
                Else
                    Console.WriteLine($"    SyntaxTree: Nothing")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CheckSourceFile error for {vFileName}: {ex.Message}")
            End Try
        End Sub



        ''' <summary>
        ''' Parse a single file using the centralized ProjectParser
        ''' </summary>
        ''' <param name="vFile">The source file to parse</param>
        ''' <returns>True if parse succeeded</returns>
        Public Function ParseFile(vFile As SourceFileInfo) As Boolean
            Try
                If vFile Is Nothing Then Return False
                
                Console.WriteLine($"ProjectManager.ParseFile: {vFile.FileName}")
                
                ' Parse the file content
                Dim lParseResult As Object = Parser.ParseContent(vFile.Content, RootNamespace, vFile.FilePath)
                
                ' Update the SourceFileInfo with parse results
                If lParseResult IsNot Nothing Then
                    ' Check if it's a ParseResult type
                    If TypeOf lParseResult Is ParseResult Then
                        Dim lResult As ParseResult = DirectCast(lParseResult, ParseResult)
                        
                        ' Extract the SyntaxNode tree from ParseResult
                        If lResult.RootNode IsNot Nothing Then
                            vFile.SyntaxTree = lResult.RootNode
                        End If
                        
                        ' CRITICAL FIX: Update LineMetadata with syntax tokens
                        ' This was missing - causing empty token lists
                        If lResult.LineMetadata IsNot Nothing Then
                            vFile.UpdateLineMetadata(lResult)
                            Console.WriteLine($"Updated LineMetadata for {vFile.FileName} with {lResult.LineMetadata.Length} lines")
                        End If
                        
                        ' Extract any errors
                        If lResult.Errors IsNot Nothing Then
                            vFile.ParseErrors = lResult.Errors
                        End If
                        
                        vFile.IsParsed = True
                        vFile.LastParsed = DateTime.Now
                        
                        ' Raise parse completed event with the SyntaxNode
                        RaiseEvent ParseCompleted(vFile, vFile.SyntaxTree)
                        
                        Console.WriteLine($"Parse complete: {vFile.FileName} - {If(vFile.SyntaxTree IsNot Nothing, "Success", "Failed")}")
                        Return vFile.SyntaxTree IsNot Nothing
                        
                    ElseIf TypeOf lParseResult Is SyntaxNode Then
                        ' Handle case where parser returns SyntaxNode directly (legacy)
                        Dim lNode As SyntaxNode = DirectCast(lParseResult, SyntaxNode)
                        vFile.SyntaxTree = lNode
                        vFile.IsParsed = True
                        vFile.LastParsed = DateTime.Now
                        
                        ' Clear any previous parse errors
                        If vFile.ParseErrors Is Nothing Then
                            vFile.ParseErrors = New List(Of ParseError)()
                        Else
                            vFile.ParseErrors.Clear()
                        End If
                        
                        ' Raise parse completed event
                        RaiseEvent ParseCompleted(vFile, lNode)
                        
                        Console.WriteLine($"Parse complete: {vFile.FileName} - Success (legacy)")
                        Return True
                    Else
                        ' Unknown result type
                        Console.WriteLine($"ProjectManager.ParseFile error: Unknown parse result type: {lParseResult.GetType().Name}")
                        Return False
                    End If
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.ParseFile error: {ex.Message}")
                
                ' Add error to file's parse errors
                If vFile.ParseErrors Is Nothing Then
                    vFile.ParseErrors = New List(Of ParseError)()
                End If
                
                vFile.ParseErrors.Add(New ParseError with {
                    .Message = $"Parse error: {ex.Message}",
                    .Line = 0,
                    .Column = 0,
                    .Severity = ParseErrorSeverity.eError
                })
                
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Wire up DocumentModel events when creating or loading
        ''' </summary>
        Private Sub WireDocumentModelEvents(vModel As DocumentModel)
            Try
                If vModel Is Nothing Then Return
                
                ' Wire up existing events
                AddHandler vModel.DocumentParsed, AddressOf OnDocumentParsed
                AddHandler vModel.StructureChanged, AddressOf OnDocumentStructureChanged
                AddHandler vModel.ModifiedStateChanged, AddressOf OnDocumentModifiedStateChanged
                
                ' Wire up the RequestProjectManager event to provide reference
                AddHandler vModel.RequestProjectManager, AddressOf OnDocumentModelRequestProjectManager
                
                Console.WriteLine($"Wired DocumentModel events for {vModel.FilePath}")
                
            Catch ex As Exception
                Console.WriteLine($"WireDocumentModelEvents error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle DocumentModel request for ProjectManager reference
        ''' </summary>
        Private Sub OnDocumentModelRequestProjectManager(sender As Object, e As ProjectManagerRequestEventArgs)
            Try
                ' Provide this ProjectManager instance to the requesting DocumentModel
                e.ProjectManager = Me
                Console.WriteLine("ProjectManager provided to DocumentModel via event")
                
            Catch ex As Exception
                Console.WriteLine($"OnDocumentModelRequestProjectManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Load a source file into memory
        ''' </summary>
        ''' <param name="vRelativePath">Relative path from project directory</param>
        ''' <returns>The loaded SourceFileInfo or Nothing on failure</returns>
        Private Function LoadSourceFile(vRelativePath As String) As SourceFileInfo
            Try
                If String.IsNullOrEmpty(vRelativePath) Then Return Nothing
                
                ' Get full path
                Dim lFullPath As String = Path.Combine(pCurrentProjectInfo.ProjectDirectory, vRelativePath)
                lFullPath = Path.GetFullPath(lFullPath)
                
                ' Check if already loaded
                If pSourceFiles.ContainsKey(lFullPath) Then
                    Return pSourceFiles(lFullPath)
                End If
                
                ' Create new SourceFileInfo
                Dim lSourceFile As New SourceFileInfo(lFullPath, "", pCurrentProjectInfo.ProjectDirectory)
                
                ' Set project root namespace
                lSourceFile.ProjectRootNamespace = pCurrentProjectInfo.GetEffectiveRootNamespace()
                
                ' Wire up the ProjectManagerRequested event
                AddHandler lSourceFile.ProjectManagerRequested, AddressOf OnSourceFileInfoRequestProjectManager
                
                ' Load content
                If lSourceFile.LoadContent() Then
                    pSourceFiles(lFullPath) = lSourceFile
                    Console.WriteLine($"Loaded source file: {lSourceFile.FileName}")
                    Return lSourceFile
                Else
                    Console.WriteLine($"Failed to load source file: {lFullPath}")
                    Return Nothing
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadSourceFile error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Handles SourceFileInfo request for ProjectManager reference
        ''' </summary>
        ''' <param name="sender">The SourceFileInfo requesting the reference</param>
        ''' <param name="e">Event arguments to receive the ProjectManager reference</param>
        Private Sub OnSourceFileInfoRequestProjectManager(sender As Object, e As ProjectManagerRequestEventArgs)
            Try
                ' Provide this ProjectManager instance to the requesting SourceFileInfo
                e.ProjectManager = Me
                
                Dim lSourceFile As SourceFileInfo = TryCast(sender, SourceFileInfo)
                If lSourceFile IsNot Nothing Then
                    'Console.WriteLine($"ProjectManager provided to SourceFileInfo: {lSourceFile.FileName}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnSourceFileInfoRequestProjectManager error: {ex.Message}")
            End Try
        End Sub

        ' Replace: SimpleIDE.Managers.ProjectManager.ApplyCurrentTheme
        ''' <summary>
        ''' Applies the current theme to all open files without re-parsing
        ''' </summary>
        ''' <remarks>
        ''' This method updates CharacterColors arrays based on existing LineMetadata tokens.
        ''' It does NOT trigger re-parsing - it only updates colors based on already-parsed token types.
        ''' This provides immediate theme updates without the overhead of re-parsing.
        ''' </remarks>
        Public Sub ApplyCurrentTheme()
            Try
                'Console.WriteLine("ProjectManager.ApplyCurrentTheme: Starting theme application")
                
                ' DIAGNOSTIC: Check if method is being called
                'Console.WriteLine($"ProjectManager.ApplyCurrentTheme: Method called at {DateTime.Now:HH:mm:ss.fff}")
                
                ' Verify we have a theme manager
                If pThemeManager Is Nothing Then
                    Console.WriteLine("ProjectManager.ApplyCurrentTheme: ERROR - No ThemeManager available")
                    Return
                End If
                
                'Console.WriteLine($"ProjectManager.ApplyCurrentTheme: ThemeManager found")
                
                ' Get the current theme
                Dim lCurrentTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                If lCurrentTheme Is Nothing Then
                '    Console.WriteLine("ProjectManager.ApplyCurrentTheme: ERROR - No current theme available")
                    Return
                End If
                
                'Console.WriteLine($"ProjectManager.ApplyCurrentTheme: Applying theme '{lCurrentTheme.Name}'")
                'Console.WriteLine($"ProjectManager.ApplyCurrentTheme: Theme foreground color: {lCurrentTheme.ForegroundColor}")
                
                ' Get default color from theme
                Dim lDefaultColor As String = lCurrentTheme.ForegroundColor
                If String.IsNullOrEmpty(lDefaultColor) Then
                    lDefaultColor = "#D4D4D4"  ' Fallback default
                    'Console.WriteLine($"ProjectManager.ApplyCurrentTheme: Using fallback Default color: {lDefaultColor}")
                Else
                '    Console.WriteLine($"ProjectManager.ApplyCurrentTheme: Using theme Default color: {lDefaultColor}")
                End If
                
                ' Create color map from current theme
                Dim lColorMap As Dictionary(Of SyntaxTokenType, String) = CreateThemeColorMap(lCurrentTheme)
                'Console.WriteLine($"ProjectManager.ApplyCurrentTheme: Color map created with {lColorMap.Count} entries")
                
                ' Report progress for theme application
                Dim lTotalFiles As Integer = pSourceFiles.Count
                Dim lProcessedFiles As Integer = 0
                
                ' Apply colors to each file's CharacterColors array
                For Each lKvp As KeyValuePair(Of String, SourceFileInfo) In pSourceFiles
                    Dim lSourceFile As SourceFileInfo = lKvp.Value
                    
                    ' Update status periodically
                    lProcessedFiles += 1
                    If lProcessedFiles Mod Math.Max(1, lTotalFiles \ 10) = 0 OrElse lProcessedFiles = lTotalFiles Then
                        ' Report progress (95-99% range for theme application)
                        Dim lProgress As Double = 95 + (lProcessedFiles / CDbl(lTotalFiles)) * 4
                        
                        ' This is already on UI thread if called from fixed ParseAllFilesAsync
                        RaiseEvent ParsingProgress(lProcessedFiles, lTotalFiles, $"Applying colors To {lSourceFile.FileName}")
                    End If
                    
                    ' Skip if not parsed
                    If Not lSourceFile.IsParsed OrElse lSourceFile.LineMetadata Is Nothing Then
                '       Console.WriteLine($"  Skipping {lSourceFile.FileName} - Not parsed")
                        Continue For
                    End If
                    
                    ' Check if file has syntax tokens
                    Dim lHasTokens As Boolean = False
                    For Each lMetadata In lSourceFile.LineMetadata
                        If lMetadata?.SyntaxTokens IsNot Nothing AndAlso lMetadata.SyntaxTokens.Count > 0 Then
                            lHasTokens = True
                            Exit For
                        End If
                    Next
                    
                    If Not lHasTokens Then
                 '      Console.WriteLine($"  Skipping {lSourceFile.FileName} - no syntax tokens")
                        Continue For
                    End If
                    
                    'Console.WriteLine($"  Applying theme To {lSourceFile.FileName}")
                    
                    ' Apply the color map to generate CharacterColors
                    UpdateFileColorsFromTheme(lSourceFile)
                    
                    ' Notify that rendering has changed
                    lSourceFile.NotifyRenderingChanged(0, lSourceFile.TextLines.Count - 1)
                Next
                
        '       Console.WriteLine($"ProjectManager.ApplyCurrentTheme: Completed applying theme To {lProcessedFiles} files")
                
                ' Final progress update
                RaiseEvent ParsingProgress(lTotalFiles, lTotalFiles, "Theme application complete")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.ApplyCurrentTheme error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates a color map from theme for syntax token types
        ''' </summary>
        ''' <param name="vTheme">The theme to extract colors from</param>
        ''' <returns>Dictionary mapping SyntaxTokenType to color strings</returns>
        Private Function CreateThemeColorMap(vTheme As EditorTheme) As Dictionary(Of SyntaxTokenType, String)
            Try
                Dim lColorMap As New Dictionary(Of SyntaxTokenType, String)
                
                ' Use theme's SyntaxColors if available
                If vTheme.SyntaxColors IsNot Nothing Then
                    ' Map SyntaxColorSet.Tags to SyntaxTokenType with colors
                    If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eKeyword) Then
                        lColorMap(SyntaxTokenType.eKeyword) = vTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword)
                    End If
                    
                    If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eString) Then
                        lColorMap(SyntaxTokenType.eString) = vTheme.SyntaxColors(SyntaxColorSet.Tags.eString)
                    End If
                    
                    If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eComment) Then
                        lColorMap(SyntaxTokenType.eComment) = vTheme.SyntaxColors(SyntaxColorSet.Tags.eComment)
                    End If
                    
                    If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eNumber) Then
                        lColorMap(SyntaxTokenType.eNumber) = vTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber)
                    End If
                    
                    If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eType) Then
                        lColorMap(SyntaxTokenType.eType) = vTheme.SyntaxColors(SyntaxColorSet.Tags.eType)
                    End If
                    
                    If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eIdentifier) Then
                        lColorMap(SyntaxTokenType.eIdentifier) = vTheme.SyntaxColors(SyntaxColorSet.Tags.eIdentifier)
                    End If
                    
                    If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eOperator) Then
                        lColorMap(SyntaxTokenType.eOperator) = vTheme.SyntaxColors(SyntaxColorSet.Tags.eOperator)
                    End If
                End If
                
                ' Add fallback colors for any missing mappings
                If Not lColorMap.ContainsKey(SyntaxTokenType.eKeyword) Then lColorMap(SyntaxTokenType.eKeyword) = "#569CD6"
                If Not lColorMap.ContainsKey(SyntaxTokenType.eString) Then lColorMap(SyntaxTokenType.eString) = "#CE9178"
                If Not lColorMap.ContainsKey(SyntaxTokenType.eComment) Then lColorMap(SyntaxTokenType.eComment) = "#6A9955"
                If Not lColorMap.ContainsKey(SyntaxTokenType.eNumber) Then lColorMap(SyntaxTokenType.eNumber) = "#B5CEA8"
                If Not lColorMap.ContainsKey(SyntaxTokenType.eType) Then lColorMap(SyntaxTokenType.eType) = "#4EC9B0"
                If Not lColorMap.ContainsKey(SyntaxTokenType.eIdentifier) Then lColorMap(SyntaxTokenType.eIdentifier) = vTheme.ForegroundColor
                If Not lColorMap.ContainsKey(SyntaxTokenType.eOperator) Then lColorMap(SyntaxTokenType.eOperator) = vTheme.ForegroundColor
                If Not lColorMap.ContainsKey(SyntaxTokenType.eNormal) Then lColorMap(SyntaxTokenType.eNormal) = vTheme.ForegroundColor
                
                Return lColorMap
                
            Catch ex As Exception
                Console.WriteLine($"CreateThemeColorMap error: {ex.Message}")
                ' Return a basic color map on error
                Return New Dictionary(Of SyntaxTokenType, String)
            End Try
        End Function
        




        ''' <summary>
        ''' Updates the CharacterColors array for a specific file based on current theme
        ''' </summary>
        ''' <param name="vFile">The source file to update colors for</param>
        ''' <remarks>
        ''' Public method to allow editors to request color updates without re-parsing.
        ''' This is useful when an editor is first shown or needs to refresh colors.
        ''' </remarks>
        Public Sub UpdateFileColors(vFile As SourceFileInfo)
            Try
                If vFile Is Nothing Then
                    Console.WriteLine($"UpdateFileColors: File is Nothing")
                    Return
                End If
                
                Console.WriteLine($"UpdateFileColors: Updating colors for {vFile.FileName}")
                
                ' Use the existing private method to do the actual work
                UpdateFileColorsFromTheme(vFile)
                
                ' Notify the file that rendering has changed
                vFile.NotifyRenderingChanged(0, vFile.TextLines.Count - 1)
                
                Console.WriteLine($"UpdateFileColors: Completed and notified rendering change")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFileColors error: {ex.Message}")
            End Try
        End Sub
        
    End Class

End Namespace
