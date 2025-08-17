' ProjectManager.Extension.vb
' Created: 2025-08-16 14:29:16

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
        ''' Process a file's structure and merge into namespace tree with proper hierarchy
        ''' </summary>
        Private Sub ProcessFileStructure(vFileInfo As SourceFileInfo, 
                                        vRootNamespace As SyntaxNode,
                                        vNamespaceNodes As Dictionary(Of String, SyntaxNode),
                                        vRootNamespaceName As String)
            Try
                If vFileInfo.SyntaxTree Is Nothing Then Return
                
                Console.WriteLine($"Processing file: {vFileInfo.FileName}")
                Console.WriteLine($"  SyntaxTree has {vFileInfo.SyntaxTree.Children.Count} children")
                
                ' Track current namespace context as we process nodes
                Dim lCurrentNamespaceContext As SyntaxNode = vRootNamespace
                Dim lCurrentNamespaceName As String = vRootNamespaceName
                
                ' Process the file's syntax tree
                For Each lTopNode In vFileInfo.SyntaxTree.Children
                    Console.WriteLine($"  Top node: {lTopNode.Name} (Type: {lTopNode.NodeType})")
                    
                    If lTopNode.NodeType = CodeNodeType.eNamespace Then
                        If lTopNode.Name = vRootNamespaceName Then
                            ' This is the implicit root namespace - process its children
                            Console.WriteLine($"    Found implicit namespace with {lTopNode.Children.Count} children")
                            For Each lChild In lTopNode.Children
                                ProcessNodeInContext(lChild, vRootNamespace, vNamespaceNodes, vRootNamespaceName, vFileInfo.RelativePath)
                            Next
                        Else
                            ' This is an explicit namespace declaration (like "Widgets" or "Syntax")
                            ' Create or get the full namespace
                            Dim lFullNamespaceName As String = vRootNamespaceName & "." & lTopNode.Name
                            Console.WriteLine($"    Found explicit namespace: {lTopNode.Name} -> {lFullNamespaceName}")
                            
                            ' Get or create this namespace node
                            Dim lNamespaceNode As SyntaxNode
                            If vNamespaceNodes.ContainsKey(lFullNamespaceName) Then
                                lNamespaceNode = vNamespaceNodes(lFullNamespaceName)
                            Else
                                lNamespaceNode = New SyntaxNode(CodeNodeType.eNamespace, lTopNode.Name)
                                vNamespaceNodes(lFullNamespaceName) = lNamespaceNode
                                ' Add to root namespace
                                vRootNamespace.AddChild(lNamespaceNode)
                            End If
                            
                            ' FIXED: Process ALL children through ProcessNodeInContext
                            ' This ensures classes/modules/structures are added with their complete child hierarchy
                            For Each lChild In lTopNode.Children
                                Console.WriteLine($"      Processing child: {lChild.Name} ({lChild.NodeType})")
                                ProcessNodeInContext(lChild, lNamespaceNode, vNamespaceNodes, lFullNamespaceName, vFileInfo.RelativePath)
                            Next
                        End If
                    Else
                        ' Regular node - add to current namespace context
                        ProcessNodeInContext(lTopNode, lCurrentNamespaceContext, vNamespaceNodes, lCurrentNamespaceName, vFileInfo.RelativePath)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessFileStructure error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Process a node in the current namespace context
        ''' </summary>
        Private Sub ProcessNodeInContext(vNode As SyntaxNode,
                                        vCurrentNamespace As SyntaxNode,
                                        vNamespaceNodes As Dictionary(Of String, SyntaxNode),
                                        vCurrentNamespaceName As String,
                                        vFilePath As String)
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eNamespace
                        ' Handle nested namespace
                        Dim lFullNamespaceName As String = vCurrentNamespaceName & "." & vNode.Name
                        
                        Dim lNamespaceNode As SyntaxNode
                        If vNamespaceNodes.ContainsKey(lFullNamespaceName) Then
                            lNamespaceNode = vNamespaceNodes(lFullNamespaceName)
                        Else
                            lNamespaceNode = New SyntaxNode(CodeNodeType.eNamespace, vNode.Name)
                            vNamespaceNodes(lFullNamespaceName) = lNamespaceNode
                            vCurrentNamespace.AddChild(lNamespaceNode)
                        End If
                        
                        ' Process children in this namespace context
                        For Each lChild In vNode.Children
                            ProcessNodeInContext(lChild, lNamespaceNode, vNamespaceNodes, lFullNamespaceName, vFilePath)
                        Next
                        
                    Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eInterface, 
                         CodeNodeType.eStructure, CodeNodeType.eEnum, CodeNodeType.eDelegate
                        ' FIXED: Add the complete type with ALL its children to the namespace
                        ' Don't process children separately - they should stay as children of the type
                        vNode.Metadata = New Dictionary(Of String, Object) From {{"FilePath", vFilePath}}
                        
                        ' The node already has its complete child hierarchy from the parser
                        ' Just add it as-is to preserve nested types, methods, properties, etc.
                        vCurrentNamespace.AddChild(vNode)
                        
                        ' DO NOT recursively process children of types - they should remain nested!
                        ' This was the bug - it was flattening the hierarchy
                        
                    Case CodeNodeType.eImport
                        ' Skip imports
                        
                    Case Else
                        ' Skip other nodes or add as needed
                        If ShouldIncludeInNamespaceTree(vNode) Then
                            vNode.Metadata = New Dictionary(Of String, Object) From {{"FilePath", vFilePath}}
                            vCurrentNamespace.AddChild(vNode)
                        End If
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessNodeInContext error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Process a node and add it to the appropriate namespace
        ''' </summary>
        Private Sub ProcessNodeForNamespace(vNode As SyntaxNode, 
                                           vRootNamespace As SyntaxNode,
                                           vNamespaceNodes As Dictionary(Of String, SyntaxNode),
                                           vRootNamespaceName As String,
                                           vFilePath As String)
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eNamespace
                        ' Handle explicit namespace declarations
                        Dim lFullNamespaceName As String
                        
                        ' Check if this is a sub-namespace (e.g., "Utilities" in a file that declares "Namespace Utilities")
                        If Not vNode.Name.Contains("."c) Then
                            ' This is a relative namespace declaration - prepend root namespace
                            lFullNamespaceName = vRootNamespaceName & "." & vNode.Name
                        Else
                            ' This is a fully qualified namespace
                            lFullNamespaceName = vNode.Name
                        End If
                        
                        ' Get or create namespace node
                        Dim lNamespaceNode As SyntaxNode
                        If vNamespaceNodes.ContainsKey(lFullNamespaceName) Then
                            lNamespaceNode = vNamespaceNodes(lFullNamespaceName)
                        Else
                            lNamespaceNode = New SyntaxNode(CodeNodeType.eNamespace, lFullNamespaceName)
                            vNamespaceNodes(lFullNamespaceName) = lNamespaceNode
                        End If
                        
                        ' Add children to this namespace
                        For Each lChild In vNode.Children
                            ProcessNodeForNamespace(lChild, lNamespaceNode, vNamespaceNodes, vRootNamespaceName, vFilePath)
                        Next
                        
                    Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eInterface, 
                         CodeNodeType.eStructure, CodeNodeType.eEnum
                        ' These are type declarations - add to current namespace
                        ' Store file path as metadata for display purposes
                        vNode.Metadata = New Dictionary(Of String, Object) From {{"FilePath", vFilePath}}
                        vRootNamespace.AddChild(vNode)
                        
                    Case CodeNodeType.eImport
                        ' Skip imports - they don't belong in the object tree
                        
                    Case Else
                        ' For other nodes, check if they should be added
                        If ShouldIncludeInNamespaceTree(vNode) Then
                            vNode.Metadata = New Dictionary(Of String, Object) From {{"FilePath", vFilePath}}
                            vRootNamespace.AddChild(vNode)
                        End If
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessNodeForNamespace error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Build the final namespace hierarchy
        ''' </summary>
        Private Sub BuildNamespaceHierarchy(vNamespaceNodes As Dictionary(Of String, SyntaxNode), 
                                           vRootNamespace As SyntaxNode)
            Try
                ' Process all namespaces and build parent-child relationships
                For Each lKvp In vNamespaceNodes
                    Dim lNamespaceName As String = lKvp.Key
                    Dim lNamespaceNode As SyntaxNode = lKvp.Value
                    
                    ' Skip the root namespace itself
                    If lNamespaceNode Is vRootNamespace Then Continue For
                    
                    ' Find parent namespace
                    Dim lLastDot As Integer = lNamespaceName.LastIndexOf("."c)
                    If lLastDot > 0 Then
                        Dim lParentName As String = lNamespaceName.Substring(0, lLastDot)
                        If vNamespaceNodes.ContainsKey(lParentName) Then
                            ' Add to parent namespace
                            vNamespaceNodes(lParentName).AddChild(lNamespaceNode)
                        Else
                            ' No parent found, add to root
                            vRootNamespace.AddChild(lNamespaceNode)
                        End If
                    Else
                        ' Top-level namespace, add to root
                        vRootNamespace.AddChild(lNamespaceNode)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"BuildNamespaceHierarchy error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Determine if a node should be included in the namespace tree
        ''' </summary>
        Private Function ShouldIncludeInNamespaceTree(vNode As SyntaxNode) As Boolean
            Select Case vNode.NodeType
                Case CodeNodeType.eDocument, CodeNodeType.eImport, CodeNodeType.eParameter, 
                     CodeNodeType.eVariable  ', CodeNodeType.eUnknown
                    Return False
                Case Else
                    Return True
            End Select
        End Function

        
        ''' <summary>
        ''' Dictionary of all source files in the project, keyed by full path
        ''' </summary>
        Private pSourceFiles As New Dictionary(Of String, SourceFileInfo)()
        
        ''' <summary>
        ''' The complete project syntax tree combining all files
        ''' </summary>
        Private pProjectSyntaxTree As SyntaxNode
        
        ''' <summary>
        ''' Indicates if project structure is currently being loaded
        ''' </summary>
        Private pIsLoadingStructure As Boolean = False
        
        ' ===== Events for Enhanced Features =====
        
        ''' <summary>
        ''' Raised when project structure has been fully parsed
        ''' </summary>
        Public Event ProjectStructureLoaded(vRootNode As SyntaxNode)
        
        ''' <summary>
        ''' Raised when a file in the project is parsed
        ''' </summary>
        Public Event FileParsed(vFileInfo As SourceFileInfo)
        
        ''' <summary>
        ''' Raised when parsing progress updates
        ''' </summary>
        Public Event ParsingProgress(vCurrent As Integer, vTotal As Integer, vFileName As String)
        
        ' ===== Enhanced Public Methods =====
        
        ''' <summary>
        ''' Load project with full structure parsing
        ''' </summary>
        Public Function LoadProjectWithParsing(vProjectPath As String) As Boolean
            Try
                '' First load the project normally
                If Not LoadProject(vProjectPath) Then Return False
                
                ' Now parse all source files
                Return LoadProjectStructure()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProjectWithParsing error: {ex.Message}")
                Return False
            End Try
        End Function
        
        
        ''' <summary>
        ''' All DocumentModels keyed by relative file path from project root
        ''' </summary>
        Private pDocumentModels As New Dictionary(Of String, DocumentModel)()
        
        ''' <summary>
        ''' Unified namespace tree combining all DocumentModels
        ''' </summary>
        Private pUnifiedNamespaceTree As DocumentNode
        
        ''' <summary>
        ''' Maps fully qualified names to DocumentNodes for fast lookup
        ''' </summary>
        Private pSymbolTable As New Dictionary(Of String, DocumentNode)(StringComparer.OrdinalIgnoreCase)
        
        ''' <summary>
        ''' Tracks which DocumentModels have active editors
        ''' </summary>
        Private pActiveEditors As New Dictionary(Of String, List(Of IEditor))()
        
       
        ''' <summary>
        ''' Total files to load for progress reporting
        ''' </summary>
        Private pTotalFilesToLoad As Integer = 0
        
        ''' <summary>
        ''' Files loaded so far
        ''' </summary>
        Private pFilesLoaded As Integer = 0
        
        ''' <summary>
        ''' Lock for thread-safe operations
        ''' </summary>
        Private pLoadLock As New Object()
        
        ' ===== DocumentModel Events =====
        
        ''' <summary>
        ''' Raised as files are loaded during project open
        ''' </summary>
        Public Event ProjectLoadProgress(vFilesLoaded As Integer, vTotalFiles As Integer, vCurrentFile As String)
        
        ''' <summary>
        ''' Raised when project structure changes (namespace tree updated)
        ''' </summary>
        Public Event ProjectStructureChanged(vRootNode As DocumentNode)
        
        ''' <summary>
        ''' Raised when a DocumentModel is created
        ''' </summary>
        Public Event DocumentModelCreated(vFilePath As String, vModel As DocumentModel)
        
        ''' <summary>
        ''' Raised when a DocumentModel is removed
        ''' </summary>
        Public Event DocumentModelRemoved(vFilePath As String)
        
        ''' <summary>
        ''' Raised when all documents have been loaded and parsed
        ''' </summary>
        Public Event AllDocumentsLoaded(vDocumentCount As Integer)
        
        ' ===== Public Methods - DocumentModel Access =====


        
        ''' <summary>
        ''' Get DocumentModel for a file path (relative or absolute)
        ''' </summary>
        Public Function GetDocumentModel(vFilePath As String) As DocumentModel
            Try
                ' Convert to relative path if absolute
                Dim lRelativePath As String = GetRelativePath(vFilePath)
                
                If pDocumentModels.ContainsKey(lRelativePath) Then
                    Return pDocumentModels(lRelativePath)
                End If
                
                ' Try with just filename if not found
                Dim lFileName As String = Path.GetFileName(vFilePath)
                For Each lKvp In pDocumentModels
                    If Path.GetFileName(lKvp.key) = lFileName Then
                        Return lKvp.Value
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetDocumentModel error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get all DocumentModels in the project
        ''' </summary>
        Public Function GetAllDocumentModels() As Dictionary(Of String, DocumentModel)
            Return New Dictionary(Of String, DocumentModel)(pDocumentModels)
        End Function
        
        ''' <summary>
        ''' Check if a DocumentModel exists for a file
        ''' </summary>
        Public Function HasDocumentModel(vFilePath As String) As Boolean
            Dim lRelativePath As String = GetRelativePath(vFilePath)
            Return pDocumentModels.ContainsKey(lRelativePath)
        End Function
        
        ''' <summary>
        ''' Get list of DocumentModels with unsaved changes
        ''' </summary>
        Public Function GetModifiedDocumentModels() As List(Of DocumentModel)
            Dim lModified As New List(Of DocumentModel)()
            
            For Each lModel In pDocumentModels.Values
                If lModel.IsModified Then
                    lModified.Add(lModel)
                End If
            Next
            
            Return lModified
        End Function
        
        ' ===== Public Methods - File Management =====
        
        ''' <summary>
        ''' Add a new file to the project and create its DocumentModel
        ''' </summary>
        Public Function AddFileToProject(vFilePath As String) As DocumentModel
            Try
                Dim lRelativePath As String = GetRelativePath(vFilePath)
                
                ' Check if already exists
                If pDocumentModels.ContainsKey(lRelativePath) Then
                    Console.WriteLine($"File already in project: {lRelativePath}")
                    Return pDocumentModels(lRelativePath)
                End If
                
                ' Create new DocumentModel
                Dim lModel As New DocumentModel(vFilePath)
                
                ' Load file if it exists
                If File.Exists(vFilePath) Then
                    lModel.LoadFromFile(vFilePath)
                End If
                
                ' Add to collection
                pDocumentModels(lRelativePath) = lModel
                
                ' Wire up events
                AddHandler lModel.DocumentParsed, AddressOf OnDocumentParsed
                AddHandler lModel.StructureChanged, AddressOf OnDocumentStructureChanged
                AddHandler lModel.ModifiedStateChanged, AddressOf OnDocumentModifiedStateChanged
                
                ' Update project file
                If pCurrentProjectInfo IsNot Nothing Then
                    pCurrentProjectInfo.SourceFiles.Add(vFilePath)
                    pCurrentProjectInfo.CompileItems.Add(lRelativePath)
                    Dim lDoc As New XmlDocument()
                    lDoc.Load(CurrentProjectPath)
                    SaveProjectFile(lDoc)
                End If
                
                ' Rebuild namespace tree
                BuildUnifiedNamespaceTree()
                
                ' Raise events
                RaiseEvent DocumentModelCreated(lRelativePath, lModel)
                RaiseEvent FileAdded(vFilePath)
                
                Return lModel
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.AddFileToProject error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        

        
        ' ===== Public Methods - Editor Coordination =====
        
        ''' <summary>
        ''' Register an editor as using a DocumentModel
        ''' </summary>
        Public Sub RegisterEditorForDocument(vFilePath As String, vEditor As IEditor)
            Try
                Dim lRelativePath As String = GetRelativePath(vFilePath)
                
                ' Get the DocumentModel
                Dim lModel As DocumentModel = GetDocumentModel(vFilePath)
                If lModel Is Nothing Then
                    Console.WriteLine($"No DocumentModel for file: {lRelativePath}")
                    Return
                End If
                
                ' Attach editor to model
                lModel.AttachEditor(vEditor)
                
                ' Track in active editors
                If Not pActiveEditors.ContainsKey(lRelativePath) Then
                    pActiveEditors(lRelativePath) = New List(Of IEditor)()
                End If
                
                If Not pActiveEditors(lRelativePath).Contains(vEditor) Then
                    pActiveEditors(lRelativePath).Add(vEditor)
                End If
                
                Console.WriteLine($"Registered Editor for {lRelativePath}")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RegisterEditorForDocument error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Unregister an editor from a DocumentModel
        ''' </summary>
        Public Sub UnregisterEditorForDocument(vFilePath As String, vEditor As IEditor)
            Try
                Dim lRelativePath As String = GetRelativePath(vFilePath)
                
                ' Get the DocumentModel
                Dim lModel As DocumentModel = GetDocumentModel(vFilePath)
                If lModel IsNot Nothing Then
                    lModel.DetachEditor(vEditor)
                End If
                
                ' Remove from active editors
                If pActiveEditors.ContainsKey(lRelativePath) Then
                    pActiveEditors(lRelativePath).Remove(vEditor)
                    
                    If pActiveEditors(lRelativePath).Count = 0 Then
                        pActiveEditors.Remove(lRelativePath)
                    End If
                End If
                
                Console.WriteLine($"Unregistered Editor for {lRelativePath}")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.UnregisterEditorForDocument error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods - Namespace Tree =====
        
        ''' <summary>
        ''' Get the unified namespace tree for the entire project
        ''' </summary>
        Public Function GetUnifiedNamespaceTree() As DocumentNode
            Return pUnifiedNamespaceTree
        End Function
        
        ''' <summary>
        ''' Find a symbol by fully qualified name
        ''' </summary>
        Public Function FindSymbol(vFullyQualifiedName As String) As DocumentNode
            If pSymbolTable.ContainsKey(vFullyQualifiedName) Then
                Return pSymbolTable(vFullyQualifiedName)
            End If
            Return Nothing
        End Function
        
        ''' <summary>
        ''' Get all symbols of a specific type
        ''' </summary>
        Public Function GetSymbolsOfType(vNodeType As CodeNodeType) As List(Of DocumentNode)
            Dim lResults As New List(Of DocumentNode)()
            
            For Each lNode In pSymbolTable.Values
                If lNode.NodeType = vNodeType Then
                    lResults.Add(lNode)
                End If
            Next
            
            Return lResults
        End Function
        
        ' ===== Private Methods - Namespace Tree Building =====
        
        ''' <summary>
        ''' Build unified namespace tree from all DocumentModels
        ''' </summary>
        Private Sub BuildUnifiedNamespaceTree()
            Try
                Console.WriteLine("Building unified namespace tree...")
                
                ' Create root node for SimpleIDE namespace
                pUnifiedNamespaceTree = New DocumentNode()
                pUnifiedNamespaceTree.NodeId = "root"
                pUnifiedNamespaceTree.Name = "SimpleIDE"
                pUnifiedNamespaceTree.NodeType = CodeNodeType.eNamespace
                
                ' Clear symbol table
                pSymbolTable.Clear()
                pSymbolTable("SimpleIDE") = pUnifiedNamespaceTree
                
                ' Dictionary to track partial classes
                Dim lPartialClasses As New Dictionary(Of String, DocumentNode)(StringComparer.OrdinalIgnoreCase)
                
                ' Process each DocumentModel
                For Each lKvp In pDocumentModels
                    Dim lModel As DocumentModel = lKvp.Value
                    
                    If lModel.RootNode IsNot Nothing Then
                        MergeDocumentIntoTree(lModel.RootNode, pUnifiedNamespaceTree, lPartialClasses)
                    End If
                Next
                
                ' Raise structure changed event
                RaiseEvent ProjectStructureChanged(pUnifiedNamespaceTree)
                
                Console.WriteLine($"Namespace tree built with {pSymbolTable.Count} symbols")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.BuildUnifiedNamespaceTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Merge a document's node tree into the unified tree
        ''' </summary>
        Private Sub MergeDocumentIntoTree(vSourceNode As DocumentNode, vTargetParent As DocumentNode, vPartialClasses As Dictionary(Of String, DocumentNode))
            Try
                If vSourceNode Is Nothing Then Return
                
                ' Process each child of the source node
                For Each lSourceChild In vSourceNode.Children
                    Dim lQualifiedName As String = BuildQualifiedName(vTargetParent, lSourceChild.Name)
                    
                    ' Handle different node types
                    Select Case lSourceChild.NodeType
                        Case CodeNodeType.eNamespace
                            ' Find or create namespace
                            Dim lNamespaceNode As DocumentNode = FindOrCreateNamespace(vTargetParent, lSourceChild.Name)
                            
                            ' Recursively merge children
                            MergeDocumentIntoTree(lSourceChild, lNamespaceNode, vPartialClasses)
                            
                        Case CodeNodeType.eClass, CodeNodeType.eModule
                            ' Check for partial class
                            If vPartialClasses.ContainsKey(lQualifiedName) Then
                                ' Merge into existing partial class
                                Dim lExistingClass As DocumentNode = vPartialClasses(lQualifiedName)
                                MergeClassMembers(lSourceChild, lExistingClass)
                            Else
                                ' Create new class node
                                Dim lClassNode As DocumentNode = CloneNode(lSourceChild)
                                vTargetParent.Children.Add(lClassNode)
                                lClassNode.Parent = vTargetParent
                                
                                ' Add to symbol table
                                pSymbolTable(lQualifiedName) = lClassNode
                                
                                ' Track as potential partial class
                                vPartialClasses(lQualifiedName) = lClassNode
                            End If
                            
                        Case Else
                            ' Other types (interfaces, structures, enums)
                            Dim lNode As DocumentNode = CloneNode(lSourceChild)
                            vTargetParent.Children.Add(lNode)
                            lNode.Parent = vTargetParent
                            
                            ' Add to symbol table
                            pSymbolTable(lQualifiedName) = lNode
                    End Select
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.MergeDocumentIntoTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Find or create a namespace node
        ''' </summary>
        Private Function FindOrCreateNamespace(vParent As DocumentNode, vName As String) As DocumentNode
            ' Look for existing namespace
            For Each lChild In vParent.Children
                If lChild.NodeType = CodeNodeType.eNamespace AndAlso 
                   String.Equals(lChild.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                    Return lChild
                End If
            Next
            
            ' Create new namespace
            Dim lNamespace As New DocumentNode()
            lNamespace.NodeId = Guid.NewGuid().ToString()
            lNamespace.Name = vName
            lNamespace.NodeType = CodeNodeType.eNamespace
            lNamespace.Parent = vParent
            vParent.Children.Add(lNamespace)
            
            ' Add to symbol table
            Dim lQualifiedName As String = BuildQualifiedName(vParent, vName)
            pSymbolTable(lQualifiedName) = lNamespace
            
            Return lNamespace
        End Function
        
        ''' <summary>
        ''' Merge members from a partial class into existing class
        ''' </summary>
        Private Sub MergeClassMembers(vSourceClass As DocumentNode, vTargetClass As DocumentNode)
            For Each lMember In vSourceClass.Children
                ' Clone the member and add to target
                Dim lClonedMember As DocumentNode = CloneNode(lMember)
                vTargetClass.Children.Add(lClonedMember)
                lClonedMember.Parent = vTargetClass
                
                ' Add member to symbol table
                Dim lQualifiedName As String = BuildQualifiedName(vTargetClass, lMember.Name)
                pSymbolTable(lQualifiedName) = lClonedMember
            Next
        End Sub
        
        ''' <summary>
        ''' Clone a DocumentNode (shallow copy)
        ''' </summary>
        Private Function CloneNode(vNode As DocumentNode) As DocumentNode
            Dim lClone As New DocumentNode()
            lClone.NodeId = vNode.NodeId
            lClone.Name = vNode.Name
            lClone.NodeType = vNode.NodeType
            lClone.StartLine = vNode.StartLine
            lClone.EndLine = vNode.EndLine
            lClone.StartColumn = vNode.StartColumn
            lClone.EndColumn = vNode.EndColumn
            
            ' Clone attributes
            For Each lKvp In vNode.Attributes
                lClone.Attributes(lKvp.key) = lKvp.Value
            Next
            
            ' Clone children recursively
            For Each lChild In vNode.Children
                Dim lClonedChild As DocumentNode = CloneNode(lChild)
                lClone.Children.Add(lClonedChild)
                lClonedChild.Parent = lClone
            Next
            
            Return lClone
        End Function
        
        ''' <summary>
        ''' Build fully qualified name for a node
        ''' </summary>
        Private Function BuildQualifiedName(vParent As DocumentNode, vName As String) As String
            Dim lParts As New List(Of String)()
            lParts.Add(vName)
            
            Dim lCurrent As DocumentNode = vParent
            While lCurrent IsNot Nothing
                If Not String.IsNullOrEmpty(lCurrent.Name) Then
                    lParts.Insert(0, lCurrent.Name)
                End If
                lCurrent = lCurrent.Parent
            End While
            
            Return String.Join(".", lParts)
        End Function
        
        ' ===== Private Methods - Utilities =====
        
        ''' <summary>
        ''' Get relative path from project root
        ''' </summary>
        Private Function GetRelativePath(vFilePath As String) As String
            Try
                If String.IsNullOrEmpty(pCurrentProjectInfo?.ProjectDirectory) Then
                    Return vFilePath
                End If
                
                Dim lFullPath As String = Path.GetFullPath(vFilePath)
                Dim lProjectDir As String = Path.GetFullPath(pCurrentProjectInfo.ProjectDirectory)
                
                If lFullPath.StartsWith(lProjectDir, StringComparison.OrdinalIgnoreCase) Then
                    Dim lRelative As String = lFullPath.Substring(lProjectDir.Length)
                    If lRelative.StartsWith(Path.DirectorySeparatorChar) Then
                        lRelative = lRelative.Substring(1)
                    End If
                    Return lRelative
                End If
                
                Return vFilePath
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetRelativePath error: {ex.Message}")
                Return vFilePath
            End Try
        End Function
        
        ''' <summary>
        ''' Clear all DocumentModels
        ''' </summary>
        Private Sub ClearDocumentModels()
            Try
                ' Remove event handlers
                For Each lModel In pDocumentModels.Values
                    RemoveHandler lModel.DocumentParsed, AddressOf OnDocumentParsed
                    RemoveHandler lModel.StructureChanged, AddressOf OnDocumentStructureChanged
                    RemoveHandler lModel.ModifiedStateChanged, AddressOf OnDocumentModifiedStateChanged
                Next
                
                ' Clear collections
                pDocumentModels.Clear()
                pActiveEditors.Clear()
                pSymbolTable.Clear()
                pUnifiedNamespaceTree = Nothing
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.ClearDocumentModels error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handle document parsed event
        ''' </summary>
        Private Sub OnDocumentParsed(vRootNode As DocumentNode)
            Try
                ' Rebuild namespace tree when a document is parsed
                BuildUnifiedNamespaceTree()
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.OnDocumentParsed error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle document structure changed event
        ''' </summary>
        Private Sub OnDocumentStructureChanged(vAffectedNodes As List(Of DocumentNode))
            Try
                ' Rebuild namespace tree when structure changes
                BuildUnifiedNamespaceTree()
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.OnDocumentStructureChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle document modified state changed
        ''' </summary>
        Private Sub OnDocumentModifiedStateChanged(vIsModified As Boolean)
            Try
                ' Update project dirty state
                Dim lHasModified As Boolean = False
                For Each lModel In pDocumentModels.Values
                    If lModel.IsModified Then
                        lHasModified = True
                        Exit For
                    End If
                Next
                
                If lHasModified <> pIsDirty Then
                    pIsDirty = lHasModified
                    RaiseEvent ProjectModified()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.OnDocumentModifiedStateChanged error: {ex.Message}")
            End Try
        End Sub
        
        


        Public ReadOnly Property SourceFiles() As Dictionary(Of String, SourceFileInfo)
            Get
                Return pSourceFiles
            End Get
        End Property
    
        ''' <summary>
        ''' Update a specific file's structure (e.g., when edited)
        ''' </summary>
        Public Sub UpdateFileStructure(vFilePath as String, vEditor As IEditor)
            Try
                If Not pSourceFiles.ContainsKey(vFilePath) Then
                    ' Create new SourceFileInfo if not exists
                    Dim lFileInfo As New SourceFileInfo(vFilePath, pCurrentProjectInfo.ProjectDirectory)
                    pSourceFiles(vFilePath) = lFileInfo
                End If
                
                Dim lSourceFile As SourceFileInfo = pSourceFiles(vFilePath)
                
                ' Update from editor
                lSourceFile.IsLoaded = True
                lSourceFile.Editor = vEditor
                'lSourceFile.UpdateFromEditor()
                
                ' Reparse the file
                If lSourceFile.ParseContent() Then
                    ' Rebuild project tree to include changes
                    RebuildProjectTree()
                    
                    ' Raise events
                    RaiseEvent FileParsed(lSourceFile)
                    RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.UpdateFileStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Refresh the entire project structure
        ''' </summary>
        Public Sub RefreshProjectStructure()
            Try
                Console.WriteLine("Refreshing project structure...")
                LoadProjectStructure()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RefreshProjectStructure error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Find a child node by name and type
        ''' </summary>
        Private Function FindChildByNameAndType(vParent As SyntaxNode, vName As String, vType As CodeNodeType) As SyntaxNode
            Try
                For Each lChild In vParent.Children
                    If lChild.Name = vName AndAlso lChild.NodeType = vType Then
                        Return lChild
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.FindChildByNameAndType error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Organize and sort the project tree
        ''' </summary>
        Private Sub OrganizeProjectTree(vRootNode As SyntaxNode)
            Try
                ' Sort children by type and name
                SortNodeChildren(vRootNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.OrganizeProjectTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively sort node children
        ''' </summary>
        Private Sub SortNodeChildren(vNode As SyntaxNode)
            Try
                If vNode Is Nothing OrElse vNode.Children.Count = 0 Then Return
                
                ' Sort children by type priority then name
                Dim lSortedChildren = vNode.Children.OrderBy(Function(n) GetNodeTypePriority(n.NodeType)) _
                                                    .ThenBy(Function(n) n.Name, StringComparer.OrdinalIgnoreCase) _
                                                    .ToList()
                
                vNode.Children.Clear()
                vNode.Children.AddRange(lSortedChildren)
                
                ' Recursively sort children
                For Each lChild In vNode.Children
                    SortNodeChildren(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.SortNodeChildren error: {ex.Message}")
            End Try
        End Sub
        
    End Class

End Namespace

