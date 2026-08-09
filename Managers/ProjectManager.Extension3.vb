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
                            #If DEBUG Then
                            Console.WriteLine($"  Adding source file: {lCompileItem}")
                            #End If
                        Else
                            #If DEBUG Then
                            Console.WriteLine($"  Warning: Source file not found: {lCompileItem}")
                            #End If
                        End If
                    Next
                Else
                    #If DEBUG Then
                    Console.WriteLine("  Warning: No CompileItems found in project info")
                    #End If
                End If
                
                #If DEBUG Then
                Console.WriteLine($"GetProjectSourceFiles: Found {lSourceFiles.Count} source files")
                #End If
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

#If DEBUG Then
                Console.WriteLine($"{lIndentStr}{vNode.Name} ({vNode.NodeType}): " &
                                 $"{lNamespaceCount} namespaces, {lClassCount} classes, {lOtherCount} others")
#End If

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
                    #If DEBUG Then
                    Console.WriteLine($"ProcessFileStructure: No syntax tree for {vFileInfo?.FileName}")
                    #End If
                    Return
                End If
                
                #If DEBUG Then
                Console.WriteLine($"Processing file structure: {vFileInfo.FileName}")
                #End If
                
                ' Process each top-level node in the file's syntax tree
                for each lNode in vFileInfo.SyntaxTree.Children
                    
                    ' Check if this is a namespace node
                    If lNode.NodeType = CodeNodeType.eNamespace Then
                        
                        ' Handle implicit root namespace
                        If lNode.IsImplicit AndAlso 
                           String.Equals(lNode.Name, vRootNamespaceName, StringComparison.OrdinalIgnoreCase) Then
                            
                            ' This is the implicit root namespace - merge its children directly
                            #If DEBUG Then
                            Console.WriteLine($"  Merging implicit root namespace children")
                            #End If
                            for each lChild in lNode.Children
                                MergeNodeIntoNamespace(lChild, vRootNamespace, vFileInfo.FilePath)
                            Next
                            
                        Else
                            ' This is an explicit namespace declaration
                            Dim lNamespaceName As String = lNode.Name
                            #If DEBUG Then
                            Console.WriteLine($"  Processing namespace: {lNamespaceName}")
                            #End If
                            
                            ' CRITICAL FIX: Check if namespace already exists in the ROOT namespace
                            ' not just in vNamespaceNodes dictionary
                            Dim lNamespaceNode As SyntaxNode = Nothing
                            
                            ' First check if it already exists as a child of root
                            for each lRootChild in vRootNamespace.Children
                                If lRootChild.NodeType = CodeNodeType.eNamespace AndAlso
                                   String.Equals(lRootChild.Name, lNamespaceName, StringComparison.OrdinalIgnoreCase) Then
                                    lNamespaceNode = lRootChild
                                    #If DEBUG Then
                                    Console.WriteLine($"    Found existing namespace in root: {lNamespaceName}")
                                    #End If
                                    Exit for
                                End If
                            Next
                            
                            ' If not found in root children, check dictionary
                            If lNamespaceNode Is Nothing AndAlso vNamespaceNodes.ContainsKey(lNamespaceName) Then
                                lNamespaceNode = vNamespaceNodes(lNamespaceName)
                                #If DEBUG Then
                                Console.WriteLine($"    Found namespace in dictionary: {lNamespaceName}")
                                #End If
                            End If
                            
                            ' Create new namespace node only if it doesn't exist
                            If lNamespaceNode Is Nothing Then
                                #If DEBUG Then
                                Console.WriteLine($"    Creating new namespace: {lNamespaceName}")
                                #End If
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
                        #If DEBUG Then
                        Console.WriteLine($"  Processing top-level {lNode.NodeType}: {lNode.Name}")
                        #End If
                        MergeNodeIntoNamespace(lNode, vRootNamespace, vFileInfo.FilePath)
                    End If
                Next
                
                #If DEBUG Then
                Console.WriteLine($"Completed processing: {vFileInfo.FileName}")
                #End If
                
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
                #If DEBUG Then
                Console.WriteLine($"    Merging {vNode.NodeType}: {vNode.Name} into namespace: {vNamespaceNode.Name}")
                #End If
                
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
                    #If DEBUG Then
                    Console.WriteLine($"    Found existing {vNode.NodeType}: {vNode.Name} - merging members")
                    #End If
                    
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
                    
                    #If DEBUG Then
                    Console.WriteLine($"      Merged {lMergedCount} new members, skipped {lDuplicateCount} duplicates")
                    #End If
                    #If DEBUG Then
                    Console.WriteLine($"      Total members in merged class: {lExistingNode.Children.Count}")
                    #End If
                    
                Else
                    ' Create new node
                    #If DEBUG Then
                    Console.WriteLine($"    Creating new {vNode.NodeType}: {vNode.Name}")
                    #End If
                    
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
                    
                    #If DEBUG Then
                    Console.WriteLine($"      Added with {lNewNode.Children.Count} members")
                    #End If
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
                    #If DEBUG Then
                    Console.WriteLine("EnsureAllFilesLoaded: No project loaded")
                    #End If
                    Return 0
                End If
                
                ' Get list of all compile items from project
                Dim lCompileItems As List(Of String) = pCurrentProjectInfo.CompileItems
                If lCompileItems Is Nothing Then
                    #If DEBUG Then
                    Console.WriteLine("EnsureAllFilesLoaded: No compile items found")
                    #End If
                    Return 0
                End If
                
                #If DEBUG Then
                Console.WriteLine($"EnsureAllFilesLoaded: Checking {lCompileItems.Count} compile items")
                #End If
                
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
                            #If DEBUG Then
                            Console.WriteLine($"  File not found: {lRelativePath}")
                            #End If
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
                        #If DEBUG Then
                        Console.WriteLine($"  Loading: {lRelativePath}")
                        #End If
                        Dim lNewSourceFile As New SourceFileInfo(lFullPath, "")

                        ' Set project context and wire events BEFORE loading - LoadContent
                        ' calls RequestAsyncParse internally, which needs ProjectManager
                        ' already available to avoid a wasted "No ProjectManager available"
                        ' async request (and its needless 500ms GLib retry timer) that the
                        ' explicit ParseFile call below immediately makes redundant anyway
                        lNewSourceFile.ProjectRootNamespace = pCurrentProjectInfo.GetEffectiveRootNamespace()
                        lNewSourceFile.ProjectManager = Me
                        WireSourceFileInfoEvents(lNewSourceFile)

                        ' Actually load the content from disk
                        If lNewSourceFile.LoadContent() Then
                            ' Parse the file to build its syntax tree
                            #If DEBUG Then
                            Console.WriteLine($"  Parsing: {lRelativePath}")
                            #End If
                            ParseFile(lNewSourceFile)
                            
                            ' Add or update in dictionary
                            pSourceFiles(lFullPath) = lNewSourceFile
                            lLoadedCount += 1
                        Else
                            #If DEBUG Then
                            Console.WriteLine($"  Failed to load content: {lRelativePath}")
                            #End If
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"  Error loading {lRelativePath}: {ex.Message}")
                    End Try
                Next
                
                #If DEBUG Then
                Console.WriteLine($"EnsureAllFilesLoaded: {lLoadedCount} files loaded successfully")
                #End If
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
                                 CodeNodeType.eConst
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
                                 CodeNodeType.eConst
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
                #If DEBUG Then
                Console.WriteLine("=== VERIFYING NAMESPACE MERGE ===")
                #End If
                
                If pProjectSyntaxTree Is Nothing Then
                    #If DEBUG Then
                    Console.WriteLine("No project tree to verify")
                    #End If
                    Return False
                End If
                
                Dim lHasDuplicates As Boolean = False
                
                ' Check each level for duplicate namespaces
                VerifyNodeChildren(pProjectSyntaxTree, "", lHasDuplicates)
                
                If Not lHasDuplicates Then
                    #If DEBUG Then
                    Console.WriteLine("✓ No duplicate namespaces found - merge successful!")
                    #End If
                Else
                    #If DEBUG Then
                    Console.WriteLine("✗ Duplicate namespaces detected - merge failed!")
                    #End If
                End If
                
                #If DEBUG Then
                Console.WriteLine("=== END VERIFICATION ===")
                #End If
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
                        
                        #If DEBUG Then
                        Console.WriteLine($"  DUPLICATE: {lCurrentPath} has {lKvp.Value.Count} '{lName}' nodes of type {lType}")
                        #End If
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
                            #If DEBUG Then
                            Console.WriteLine($"    [{lIndex}] Children: {lDupNode.Children.Count}, Files: {lFilePath}")
                            #End If
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
                #If DEBUG Then
                Console.WriteLine("=== DIAGNOSING ROOT CLASSES (MainWindow, Program) ===")
                #End If
                
                ' Check if we have the project tree
                If pProjectSyntaxTree Is Nothing Then
                    #If DEBUG Then
                    Console.WriteLine("No project tree loaded")
                    #End If
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
                    #If DEBUG Then
                    Console.WriteLine("Root Namespace 'SimpleIDE' not found")
                    #End If
                    Return
                End If
                
                #If DEBUG Then
                Console.WriteLine($"Root namespace found with {lRootNamespace.Children.Count} children:")
                #End If
                
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
                
                #If DEBUG Then
                Console.WriteLine($"  Namespaces: {lNamespaces.Count}")
                #End If
                for each lNs in lNamespaces
                    #If DEBUG Then
                    Console.WriteLine($"    - {lNs.Name}")
                    #End If
                Next
                
                #If DEBUG Then
                Console.WriteLine($"  Classes: {lClasses.Count}")
                #End If
                for each lClass in lClasses
                    Dim lPartialInfo As String = If(lClass.IsPartial, " [PARTIAL]", "")
                    Dim lFileInfo As String = ""
                    If lClass.Attributes IsNot Nothing AndAlso lClass.Attributes.ContainsKey("FilePaths") Then
                        lFileInfo = $" Files: {lClass.Attributes("FilePaths")}"
                    End If
                    #If DEBUG Then
                    Console.WriteLine($"    - {lClass.Name}{lPartialInfo}{lFileInfo}")
                    #End If
                Next
                
                #If DEBUG Then
                Console.WriteLine($"  Modules: {lModules.Count}")
                #End If
                for each lModule in lModules
                    Dim lFileInfo As String = ""
                    If lModule.Attributes IsNot Nothing AndAlso lModule.Attributes.ContainsKey("FilePath") Then
                        lFileInfo = $" File: {lModule.Attributes("FilePath")}"
                    End If
                    #If DEBUG Then
                    Console.WriteLine($"    - {lModule.Name}{lFileInfo}")
                    #End If
                Next
                
                If lOther.Count > 0 Then
                    #If DEBUG Then
                    Console.WriteLine($"  Other: {lOther.Count}")
                    #End If
                    for each lNode in lOther
                        #If DEBUG Then
                        Console.WriteLine($"    - {lNode.Name} ({lNode.NodeType})")
                        #End If
                    Next
                End If
                
                ' Now check specific files
                #If DEBUG Then
                Console.WriteLine()
                #End If
                #If DEBUG Then
                Console.WriteLine("Checking specific source files:")
                #End If
                
                ' Check MainWindow.vb
                CheckSourceFile("MainWindow.vb")
                CheckSourceFile("Program.vb")
                
                #If DEBUG Then
                Console.WriteLine("=== END DIAGNOSIS ===")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseRootClasses error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper to check a specific source file's parse results
        ''' </summary>
        Private Sub CheckSourceFile(vFileName As String)
            Try
                #If DEBUG Then
                Console.WriteLine($"  Checking {vFileName}:")
                #End If
                
                ' Find the file in pSourceFiles
                Dim lFileInfo As SourceFileInfo = Nothing
                for each lKvp in pSourceFiles
                    If lKvp.Value.FileName.Equals(vFileName, StringComparison.OrdinalIgnoreCase) Then
                        lFileInfo = lKvp.Value
                        Exit for
                    End If
                Next
                
                If lFileInfo Is Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"    NOT FOUND in source files")
                    #End If
                    Return
                End If
                
                #If DEBUG Then
                Console.WriteLine($"    Found: {lFileInfo.FilePath}")
                #End If
                #If DEBUG Then
                Console.WriteLine($"    Parsed: {lFileInfo.IsParsed}")
                #End If
                
                If lFileInfo.SyntaxTree IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"    SyntaxTree children: {lFileInfo.SyntaxTree.Children.Count}")
                    #End If
                    for each lChild in lFileInfo.SyntaxTree.Children
                        #If DEBUG Then
                        Console.WriteLine($"      - {lChild.Name} ({lChild.NodeType})")
                        #End If
                        If lChild.NodeType = CodeNodeType.eNamespace Then
                            #If DEBUG Then
                            Console.WriteLine($"        IsImplicit: {lChild.IsImplicit}")
                            #End If
                            #If DEBUG Then
                            Console.WriteLine($"        Children: {lChild.Children.Count}")
                            #End If
                            for each lSubChild in lChild.Children
                                #If DEBUG Then
                                Console.WriteLine($"          - {lSubChild.Name} ({lSubChild.NodeType})")
                                #End If
                            Next
                        End If
                    Next
                Else
                    #If DEBUG Then
                    Console.WriteLine($"    SyntaxTree: Nothing")
                    #End If
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

                ' Parse the file content - ParseContent only takes 2 parameters: content and filepath
                Dim lParseResult As SyntaxNode = Parser.ParseContent(vFile.TextContent, vFile.FilePath)
                
                ' Update the SourceFileInfo with parse results
                If lParseResult IsNot Nothing Then
                    vFile.SyntaxTree = lParseResult
                    vFile.ParseResult = lParseResult
                    
                    ' Generate metadata from the parse result
                    vFile.GenerateMetadata()
                    
                    vFile.NeedsParsing = False
                    vFile.LastParsed = DateTime.Now
                    
                    ' Raise parse completed event with the SyntaxNode
                    RaiseEvent ParseCompleted(vFile, vFile.SyntaxTree)
                    
                    #If DEBUG Then
                    Console.WriteLine($"Parse complete: {vFile.FileName} - {If(vFile.SyntaxTree IsNot Nothing, "Success", "Failed")}")
                    #End If
                    Return vFile.SyntaxTree IsNot Nothing
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.ParseFile error: {ex.Message}")
                
                ' Add error to file's parse errors
                If vFile.ParseErrors Is Nothing Then
                    vFile.ParseErrors = New List(Of ParseError)()
                End If
                
                vFile.ParseErrors.Add(New ParseError() with {
                    .Message = ex.Message,
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
                
                #If DEBUG Then
                Console.WriteLine($"Wired DocumentModel events for {vModel.FilePath}")
                #End If
                
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
                #If DEBUG Then
                Console.WriteLine("ProjectManager provided to DocumentModel via event")
                #End If
                
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
                Dim lSourceFile As New SourceFileInfo(lFullPath, "")
                
                
                ' Load content
                If lSourceFile.LoadContent() Then
                    pSourceFiles(lFullPath) = lSourceFile
                    #If DEBUG Then
                    Console.WriteLine($"Loaded source file: {lSourceFile.FileName}")
                    #End If
                    Return lSourceFile
                Else
                    #If DEBUG Then
                    Console.WriteLine($"Failed to load source file: {lFullPath}")
                    #End If
                    Return Nothing
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadSourceFile error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Finds the definition of a symbol in the project
        ''' </summary>
        ''' <param name="vWord">The symbol name to find</param>
        ''' <param name="vCurrentFilePath">The file path where the search originated</param>
        ''' <param name="vLine">The line number where the symbol was referenced (0-based)</param>
        ''' <param name="vColumn">The column position where the symbol was referenced (0-based)</param>
        ''' <returns>DefinitionInfo containing the location of the definition, or Nothing if not found</returns>
        ''' <remarks>
        ''' Searches through the project files using both syntax trees and direct text search
        ''' to find where the symbol is defined.
        ''' </remarks>
        ''' <summary>
        ''' Finds vWord's definition, preferring the current file
        ''' </summary>
        ''' <remarks>
        ''' Fast path first: resolves vWord against the candidate fully-qualified names implied
        ''' by the current file's own namespace and Imports statements (see
        ''' GetImportsDerivedCandidates in ProjectManager.SymbolIndex.vb), then looks those up
        ''' directly in the project-wide symbol index - O(1) regardless of project size, no
        ''' tree-walking at all. This covers the overwhelming majority of real "Go to
        ''' Definition" clicks (a type/member reference resolved the normal VB.NET way).
        '''
        ''' If that finds nothing (e.g. the word isn't a type/member reachable via Imports -
        ''' a local variable, or code with a typo), falls back to two slower passes: first a
        ''' tree-only sweep (current file first, then every other project file that has a
        ''' parsed syntax tree - which conclusively covers every definition in a successfully-
        ''' parsed file), and only if THAT finds nothing does a last pass fall back to the
        ''' regex text search, and only for files with no parsed tree at all. Previously every
        ''' file that simply didn't define the symbol paid for both a tree search AND a full
        ''' regex-per-line text scan on every single lookup, which is what made "Go to
        ''' Definition" slow on a project with hundreds of files.
        ''' </remarks>
        Public Function FindDefinition(vWord As String, vCurrentFilePath As String, vLine As Integer, vColumn As Integer) As DefinitionInfo
            Try
                #If DEBUG Then
                Console.WriteLine($"FindDefinition: Searching for '{vWord}' from {vCurrentFilePath}:{vLine}:{vColumn}")
                #End If

                If String.IsNullOrWhiteSpace(vWord) Then
                    #If DEBUG Then
                    Console.WriteLine("FindDefinition: Empty word")
                    #End If
                    Return Nothing
                End If

                ' ===== Fast path: Imports/namespace-derived candidates against the symbol index =====

                Dim lCandidates As List(Of String) = GetImportsDerivedCandidates(vCurrentFilePath, vWord)
                Dim lFastResult As DefinitionInfo = FindDefinitionByFqnCandidates(vWord, lCandidates)
                If lFastResult IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"FindDefinition: Found '{vWord}' via indexed lookup as '{lFastResult.FullyQualifiedName}'")
                    #End If
                    Return lFastResult
                End If

                ' ===== Pass 1: tree search only, current file first =====

                Dim lCurrentFileInfo As SourceFileInfo = Nothing
                If pSourceFiles.TryGetValue(vCurrentFilePath, lCurrentFileInfo) AndAlso lCurrentFileInfo.SyntaxTree IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine($"FindDefinition: Searching in current file's syntax tree")
                    #End If
                    Dim lDefinitionNode As SyntaxNode = FindDefinitionInNode(lCurrentFileInfo.SyntaxTree, vWord, True)

                    If lDefinitionNode IsNot Nothing Then
                        #If DEBUG Then
                        Console.WriteLine($"FindDefinition: Found in current file at line {lDefinitionNode.StartLine}")
                        #End If
                        Return New DefinitionInfo(lDefinitionNode, vCurrentFilePath)
                    End If
                End If

                #If DEBUG Then
                Console.WriteLine("FindDefinition: Searching project-wide syntax trees")
                #End If
                for each lFileEntry in pSourceFiles
                    Dim lFilePath As String = lFileEntry.Key
                    Dim lFileInfo As SourceFileInfo = lFileEntry.Value

                    If lFilePath = vCurrentFilePath Then Continue for ' already searched above
                    If lFileInfo.SyntaxTree Is Nothing Then Continue for ' handled in pass 2

                    Dim lDefinitionNode As SyntaxNode = FindDefinitionInNode(lFileInfo.SyntaxTree, vWord, True)
                    If lDefinitionNode IsNot Nothing Then
                        #If DEBUG Then
                        Console.WriteLine($"FindDefinition: Found in {lFilePath} at line {lDefinitionNode.StartLine}")
                        #End If
                        Return New DefinitionInfo(lDefinitionNode, lFilePath)
                    End If
                Next

                ' ===== Pass 2: text-search fallback, only for files with no parsed tree =====

                If lCurrentFileInfo IsNot Nothing AndAlso lCurrentFileInfo.SyntaxTree Is Nothing Then
                    Dim lDefInfo As DefinitionInfo = FindDefinitionInFileContent(lCurrentFileInfo, vWord)
                    If lDefInfo IsNot Nothing Then
                        #If DEBUG Then
                        Console.WriteLine($"FindDefinition: Found in current file via text search at line {lDefInfo.Line}")
                        #End If
                        Return lDefInfo
                    End If
                End If

                #If DEBUG Then
                Console.WriteLine("FindDefinition: Searching project-wide via text search (unparsed files only)")
                #End If
                for each lFileEntry in pSourceFiles
                    Dim lFilePath As String = lFileEntry.Key
                    Dim lFileInfo As SourceFileInfo = lFileEntry.Value

                    If lFilePath = vCurrentFilePath Then Continue for
                    If lFileInfo.SyntaxTree IsNot Nothing Then Continue for ' already conclusively searched in pass 1

                    Dim lDefInfo As DefinitionInfo = FindDefinitionInFileContent(lFileInfo, vWord)
                    If lDefInfo IsNot Nothing Then
                        #If DEBUG Then
                        Console.WriteLine($"FindDefinition: Found in {lFilePath} via text search at line {lDefInfo.Line}")
                        #End If
                        Return lDefInfo
                    End If
                Next

                #If DEBUG Then
                Console.WriteLine($"FindDefinition: Definition not found for '{vWord}'")
                #End If
                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"FindDefinition error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Searches for a definition directly in file content using text matching
        ''' </summary>
        ''' <param name="vFileInfo">The source file to search</param>
        ''' <param name="vSymbolName">The symbol name to find</param>
        ''' <returns>DefinitionInfo if found, Nothing otherwise</returns>
        Private Function FindDefinitionInFileContent(vFileInfo As SourceFileInfo, vSymbolName As String) As DefinitionInfo
            Try
                If vFileInfo Is Nothing OrElse vFileInfo.TextLines Is Nothing Then
                    Return Nothing
                End If
                
                ' Regular expressions for different definition patterns - compiled once per
                ' call rather than once per (line x pattern), which previously meant up to
                ' 7 fresh Regex compilations for EVERY line of EVERY file with no cached parse
                ' tree; across a project this size that dwarfed the actual matching cost
                Dim lPatterns As New List(Of System.Text.RegularExpressions.Regex) From {
                    New System.Text.RegularExpressions.Regex($"^\s*(Public |Private |Protected |Friend |Partial |MustInherit |NotInheritable )*\s*(Class|Module|Interface|Structure)\s+{vSymbolName}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    New System.Text.RegularExpressions.Regex($"^\s*(Public |Private |Protected |Friend |Shared |Overridable |Overrides |MustOverride |NotOverridable )*\s*(Sub|Function)\s+{vSymbolName}\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    New System.Text.RegularExpressions.Regex($"^\s*(Public |Private |Protected |Friend |ReadOnly |WriteOnly |Default |Shared )*\s*Property\s+{vSymbolName}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    New System.Text.RegularExpressions.Regex($"^\s*(Public |Private |Protected |Friend |WithEvents |Dim |Const )*\s*{vSymbolName}\s+(As|=)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    New System.Text.RegularExpressions.Regex($"^\s*(Public |Private |Protected |Friend )*\s*Event\s+{vSymbolName}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    New System.Text.RegularExpressions.Regex($"^\s*(Public |Private |Protected |Friend )*\s*Enum\s+{vSymbolName}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    New System.Text.RegularExpressions.Regex($"^\s*(Public |Private |Protected |Friend )*\s*Delegate\s+(Sub|Function)\s+{vSymbolName}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                }

                ' Search through lines
                For i As Integer = 0 To vFileInfo.TextLines.Count - 1
                    Dim lLine As String = vFileInfo.TextLines(i)

                    ' Try each pattern
                    For Each lRegex In lPatterns
                        Dim lMatch = lRegex.Match(lLine)
                        If lMatch.Success Then
                            ' Found a definition - calculate column position
                            Dim lSymbolIndex As Integer = lLine.IndexOf(vSymbolName, StringComparison.OrdinalIgnoreCase)
                            
                            ' Create DefinitionInfo
                            Dim lDefInfo As New DefinitionInfo()
                            lDefInfo.FilePath = vFileInfo.FilePath
                            lDefInfo.Line = i
                            lDefInfo.Column = If(lSymbolIndex >= 0, lSymbolIndex, 0)
                            lDefInfo.FullyQualifiedName = vSymbolName
                            
                            ' Determine node type from the match
                            If lMatch.Value.IndexOf("Class", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eClass
                            ElseIf lMatch.Value.IndexOf("Module", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eModule
                            ElseIf lMatch.Value.IndexOf("Interface", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eInterface
                            ElseIf lMatch.Value.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eMethod
                            ElseIf lMatch.Value.IndexOf("Function", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eFunction
                            ElseIf lMatch.Value.IndexOf("Property", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eProperty
                            ElseIf lMatch.Value.IndexOf("Event", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eEvent
                            ElseIf lMatch.Value.IndexOf("Enum", StringComparison.OrdinalIgnoreCase) >= 0 Then
                                lDefInfo.NodeType = CodeNodeType.eEnum
                            Else
                                lDefInfo.NodeType = CodeNodeType.eField
                            End If
                            
                            #If DEBUG Then
                            Console.WriteLine($"FindDefinitionInFileContent: Found '{vSymbolName}' at line {i + 1} as {lDefInfo.NodeType}")
                            #End If
                            Return lDefInfo
                        End If
                    Next
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindDefinitionInFileContent error: {ex.Message}")
                Return Nothing
            End Try
        End Function
                
        
        ''' <summary>
        ''' Recursively searches a syntax node tree for a definition
        ''' </summary>
        ''' <param name="vNode">The root node to search from</param>
        ''' <param name="vSymbolName">The symbol name to find</param>
        ''' <param name="vIsDefinition">Whether to look for definitions only</param>
        ''' <returns>The syntax node containing the definition, or Nothing if not found</returns>
        Private Function FindDefinitionInNode(vNode As SyntaxNode, vSymbolName As String, vIsDefinition As Boolean) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing
                
                ' Check if this node is a definition of the symbol
                If IsDefinitionNode(vNode) AndAlso String.Equals(vNode.Name, vSymbolName, StringComparison.OrdinalIgnoreCase) Then
                    #If DEBUG Then
                    Console.WriteLine($"FindDefinitionInNode: Found definition '{vNode.Name}' of type {vNode.NodeType}")
                    #End If
                    Return vNode
                End If
                
                ' Recursively search children
                For Each lChild In vNode.Children
                    Dim lResult As SyntaxNode = FindDefinitionInNode(lChild, vSymbolName, vIsDefinition)
                    If lResult IsNot Nothing Then
                        Return lResult
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindDefinitionInNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Determines if a syntax node represents a definition (not just a reference)
        ''' </summary>
        ''' <param name="vNode">The node to check</param>
        ''' <returns>True if this is a definition node, False otherwise</returns>
        Private Function IsDefinitionNode(vNode As SyntaxNode) As Boolean
            Try
                ' These node types represent definitions
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass,
                         CodeNodeType.eModule,
                         CodeNodeType.eInterface,
                         CodeNodeType.eStructure,
                         CodeNodeType.eEnum,
                         CodeNodeType.eMethod,
                         CodeNodeType.eFunction,
                         CodeNodeType.eProperty,
                         CodeNodeType.eField,
                         CodeNodeType.eEvent,
                         CodeNodeType.eConstructor,
                         CodeNodeType.eDelegate,
                         CodeNodeType.eEnumValue
                        Return True
                    Case Else
                        Return False
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"IsDefinitionNode error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Extracts the file path from a syntax node's attributes
        ''' </summary>
        ''' <param name="vNode">The node to get the file path from</param>
        ''' <returns>The file path, or empty string if not found</returns>
        Private Function GetFilePathFromNode(vNode As SyntaxNode) As String
            Try
                If vNode Is Nothing Then Return String.Empty
                
                ' Check the node's attributes for file path information
                If vNode.Attributes IsNot Nothing Then
                    ' Check for FilePath attribute
                    If vNode.Attributes.ContainsKey("FilePath") Then
                        Return vNode.Attributes("FilePath")
                    End If
                    
                    ' Check for FilePaths attribute (for partial classes)
                    If vNode.Attributes.ContainsKey("FilePaths") Then
                        Dim lPaths As String = vNode.Attributes("FilePaths")
                        ' Return the first path for now
                        Dim lPathArray() As String = lPaths.Split(";"c)
                        If lPathArray.Length > 0 Then
                            Return lPathArray(0)
                        End If
                    End If
                End If
                
                ' Try to get from parent nodes
                If vNode.Parent IsNot Nothing Then
                    Return GetFilePathFromNode(vNode.Parent)
                End If
                
                Return String.Empty
                
            Catch ex As Exception
                Console.WriteLine($"GetFilePathFromNode error: {ex.Message}")
                Return String.Empty
            End Try
        End Function

        
    End Class

End Namespace
