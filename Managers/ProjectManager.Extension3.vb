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
                    For Each lCompileItem In pCurrentProjectInfo.CompileItems
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
        ''' Rebuilds the project tree from parsed files
        ''' </summary>
        Public Sub RebuildProjectTree()
            Try
                Console.WriteLine("Rebuilding project tree...")
                
                If pProjectSyntaxTree Is Nothing Then
                    Console.WriteLine("No project syntax tree to rebuild")
                    Return
                End If
                
                ' Get root namespace
                Dim lRootNamespaceName As String = pCurrentProjectInfo?.RootNamespace
                If String.IsNullOrEmpty(lRootNamespaceName) Then
                    lRootNamespaceName = Path.GetFileNameWithoutExtension(pCurrentProjectInfo?.ProjectPath)
                End If
                
                ' Clear existing tree but keep document node
                pProjectSyntaxTree.Children.Clear()
                
                ' Re-create root namespace
                Dim lRootNamespace As New SyntaxNode(CodeNodeType.eNamespace, lRootNamespaceName)
                lRootNamespace.IsImplicit = True
                pProjectSyntaxTree.AddChild(lRootNamespace)
                
                ' Dictionary to track namespace nodes
                Dim lNamespaceNodes As New Dictionary(Of String, SyntaxNode)(StringComparer.OrdinalIgnoreCase)
                lNamespaceNodes(lRootNamespaceName) = lRootNamespace
                
                ' Rebuild from all parsed source files
                For Each lFileEntry In pSourceFiles
                    Dim lFileInfo As SourceFileInfo = lFileEntry.Value
                    
                    If lFileInfo.SyntaxTree IsNot Nothing Then
                        ProcessFileStructure(lFileInfo, lRootNamespace, lNamespaceNodes, lRootNamespaceName)
                    End If
                Next
                
                ' Rebuild indexes
                BuildNamespaceIndex()
                BuildTypeIndex()
                
                ' Raise event
                RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                RaiseEvent ProjectStructureChanged(ConvertSyntaxNodeToDocumentNode(pProjectSyntaxTree))
                
                Console.WriteLine($"Project tree rebuilt with {pSourceFiles.Count} files")
                
            Catch ex As Exception
                Console.WriteLine($"RebuildProjectTree error: {ex.Message}")
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
                
                For Each lChild In vNode.Children
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
                
                For Each lChild In vNode.Children
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
                For Each lNode In vFileInfo.SyntaxTree.Children
                    
                    ' Check if this is a namespace node
                    If lNode.NodeType = CodeNodeType.eNamespace Then
                        
                        ' Handle implicit root namespace
                        If lNode.IsImplicit AndAlso 
                           String.Equals(lNode.Name, vRootNamespaceName, StringComparison.OrdinalIgnoreCase) Then
                            
                            ' This is the implicit root namespace - merge its children directly
                            Console.WriteLine($"  Merging implicit root namespace children")
                            For Each lChild In lNode.Children
                                MergeNodeIntoNamespace(lChild, vRootNamespace, vFileInfo.FilePath)
                            Next
                            
                        Else
                            ' This is an explicit namespace declaration
                            Dim lNamespaceName As String = lNode.Name
                            Console.WriteLine($"  Processing namespace: {lNamespaceName}")
                            
                            ' Find or create the namespace node
                            Dim lNamespaceNode As SyntaxNode = Nothing
                            
                            If vNamespaceNodes.ContainsKey(lNamespaceName) Then
                                lNamespaceNode = vNamespaceNodes(lNamespaceName)
                            Else
                                ' Create new namespace node
                                lNamespaceNode = New SyntaxNode(CodeNodeType.eNamespace, lNamespaceName)
                                vRootNamespace.AddChild(lNamespaceNode)
                                vNamespaceNodes(lNamespaceName) = lNamespaceNode
                            End If
                            
                            ' Merge the namespace contents
                            For Each lChild In lNode.Children
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
                
                ' Check if a node with this name and type already exists (partial classes)
                Dim lExistingNode As SyntaxNode = Nothing
                
                For Each lChild In vNamespaceNode.Children
                    If String.Equals(lChild.Name, vNode.Name, StringComparison.OrdinalIgnoreCase) AndAlso
                       lChild.NodeType = vNode.NodeType Then
                        lExistingNode = lChild
                        Exit For
                    End If
                Next
                
                If lExistingNode IsNot Nothing Then
                    ' Merge into existing node (partial class scenario)
                    Console.WriteLine($"    Merging partial {vNode.NodeType}: {vNode.Name}")
                    
                    ' Merge children
                    For Each lChild In vNode.Children
                        lExistingNode.AddChild(lChild)
                    Next
                    
                    ' Track the file in attributes
                    If lExistingNode.Attributes Is Nothing Then
                        lExistingNode.Attributes = New Dictionary(Of String, String)()
                    End If
                    
                    ' Add this file to the list of files containing this node
                    If lExistingNode.Attributes.ContainsKey("FilePaths") Then
                        Dim lPaths As String = lExistingNode.Attributes("FilePaths")
                        If Not lPaths.Contains(vFilePath) Then
                            lExistingNode.Attributes("FilePaths") = lPaths & ";" & vFilePath
                        End If
                    Else
                        lExistingNode.Attributes("FilePaths") = vFilePath
                    End If
                    
                Else
                    ' Add as new node
                    Console.WriteLine($"    Adding new {vNode.NodeType}: {vNode.Name}")
                    vNamespaceNode.AddChild(vNode)
                    
                    ' Set file path attribute
                    If vNode.Attributes Is Nothing Then
                        vNode.Attributes = New Dictionary(Of String, String)()
                    End If
                    vNode.Attributes("FilePath") = vFilePath
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNodeIntoNamespace error: {ex.Message}")
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

        
    End Class

End Namespace
