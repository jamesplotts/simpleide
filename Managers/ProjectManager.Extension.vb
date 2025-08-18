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
        ''' Find or create a namespace child node, preventing duplicates
        ''' </summary>
        Private Function FindOrCreateNamespaceChild(vParent As SyntaxNode, 
                                                   vNamespaceName As String,
                                                   vNamespaceNodes As Dictionary(Of String, SyntaxNode),
                                                   vFullNamespaceName As String) As SyntaxNode
            Try
                ' First check if it already exists as a child of the parent
                For Each lChild In vParent.Children
                    If lChild.NodeType = CodeNodeType.eNamespace AndAlso 
                       String.Equals(lChild.Name, vNamespaceName, StringComparison.OrdinalIgnoreCase) Then
                        Console.WriteLine($"    Found existing namespace child: {vNamespaceName}")
                        ' Update dictionary if not already there
                        If Not vNamespaceNodes.ContainsKey(vFullNamespaceName) Then
                            vNamespaceNodes(vFullNamespaceName) = lChild
                        End If
                        Return lChild
                    End If
                Next
                
                ' Check if it exists in the dictionary but not as a child (shouldn't happen but defensive)
                If vNamespaceNodes.ContainsKey(vFullNamespaceName) Then
                    Dim lExisting As SyntaxNode = vNamespaceNodes(vFullNamespaceName)
                    Console.WriteLine($"    WARNING: Namespace in dictionary but not as child: {vNamespaceName}")
                    Return lExisting
                End If
                
                ' Create new namespace node
                Console.WriteLine($"    Creating new namespace: {vNamespaceName}")
                Dim lNewNamespace As New SyntaxNode(CodeNodeType.eNamespace, vNamespaceName)
                
                ' Initialize attributes and set FilePath
                If lNewNamespace.Attributes Is Nothing Then
                    lNewNamespace.Attributes = New Dictionary(Of String, String)()
                End If
                lNewNamespace.Attributes("FilePath") = "" ' Namespaces don't have specific file paths
                
                ' Add to parent and dictionary
                vParent.AddChild(lNewNamespace)
                vNamespaceNodes(vFullNamespaceName) = lNewNamespace
                
                Return lNewNamespace
                
            Catch ex As Exception
                Console.WriteLine($"FindOrCreateNamespaceChild error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Process a node in the current namespace context with proper partial class and namespace merging
        ''' </summary>
        ''' <param name="vNode">The node to process</param>
        ''' <param name="vCurrentNamespace">The current namespace to add to</param>
        ''' <param name="vNamespaceNodes">Dictionary of namespace nodes</param>
        ''' <param name="vCurrentNamespaceName">Current namespace name</param>
        ''' <param name="vFilePath">File path for tracking</param>
        Private Sub ProcessNodeInContext(vNode As SyntaxNode,
                                        vCurrentNamespace As SyntaxNode,
                                        vNamespaceNodes As Dictionary(Of String, SyntaxNode),
                                        vCurrentNamespaceName As String,
                                        vFilePath As String)
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eNamespace
                        ' Handle nested namespace with merging
                        Dim lFullNamespaceName As String = vCurrentNamespaceName & "." & vNode.Name
                        
                        Dim lNamespaceNode As SyntaxNode
                        If vNamespaceNodes.ContainsKey(lFullNamespaceName) Then
                            ' Namespace already exists, use it
                            lNamespaceNode = vNamespaceNodes(lFullNamespaceName)
                            Console.WriteLine($"      Using existing namespace: {vNode.Name}")
                        Else
                            ' CRITICAL FIX: Check if namespace already exists as a child
                            lNamespaceNode = FindChildByNameAndType(vCurrentNamespace, vNode.Name, CodeNodeType.eNamespace)
                            
                            If lNamespaceNode Is Nothing Then
                                ' Create new namespace
                                lNamespaceNode = New SyntaxNode(CodeNodeType.eNamespace, vNode.Name)
                                vCurrentNamespace.AddChild(lNamespaceNode)
                                Console.WriteLine($"      Creating new namespace: {vNode.Name}")
                            Else
                                Console.WriteLine($"      Found existing namespace child: {vNode.Name}")
                            End If
                            
                            ' Add to dictionary for tracking
                            vNamespaceNodes(lFullNamespaceName) = lNamespaceNode
                        End If
                        
                        ' Process children in this namespace context
                        For Each lChild In vNode.Children
                            ProcessNodeInContext(lChild, lNamespaceNode, vNamespaceNodes, lFullNamespaceName, vFilePath)
                        Next
                        
                    Case CodeNodeType.eClass
                        ' Check for partial classes and merge them
                        If vNode.IsPartial Then
                            ' Look for existing class with same name
                            Dim lExistingClass As SyntaxNode = FindChildByNameAndType(vCurrentNamespace, vNode.Name, CodeNodeType.eClass)
                            
                            If lExistingClass IsNot Nothing Then
                                ' Merge into existing partial class
                                Console.WriteLine($"      Merging partial class: {vNode.Name} from {vFilePath}")
                                lExistingClass.IsPartial = True
                                
                                ' Initialize Attributes if needed
                                If lExistingClass.Attributes Is Nothing Then
                                    lExistingClass.Attributes = New Dictionary(Of String, String)()
                                End If
                                
                                ' Track file paths
                                If Not lExistingClass.Attributes.ContainsKey("FilePaths") Then
                                    lExistingClass.Attributes("FilePaths") = vFilePath
                                Else
                                    Dim lPaths As String = lExistingClass.Attributes("FilePaths")
                                    If Not lPaths.Contains(vFilePath) Then
                                        lExistingClass.Attributes("FilePaths") = lPaths & ";" & vFilePath
                                    End If
                                End If
                                
                                ' Merge all members from this partial definition
                                For Each lMember In vNode.Children
                                    ' Check if member already exists to avoid duplicates
                                    Dim lExistingMember As SyntaxNode = FindChildByNameAndType(lExistingClass, lMember.Name, lMember.NodeType)
                                    If lExistingMember Is Nothing Then
                                        ' Create new member node
                                        Dim lNewMember As New SyntaxNode(lMember.NodeType, lMember.Name)
                                        lMember.CopyNodeAttributesTo(lNewMember)
                                        
                                        ' Set file path for this member
                                        If lNewMember.Attributes Is Nothing Then
                                            lNewMember.Attributes = New Dictionary(Of String, String)()
                                        End If
                                        lNewMember.Attributes("FilePath") = vFilePath
                                        
                                        ' Add all children of the member (like parameters)
                                        For Each lChild In lMember.Children
                                            lNewMember.AddChild(lChild)
                                        Next
                                        
                                        lExistingClass.AddChild(lNewMember)
                                    Else
                                        ' Member already exists - could update file path info
                                        Console.WriteLine($"        Member already exists: {lMember.Name}")
                                    End If
                                Next
                            Else
                                ' First occurrence of this partial class
                                Console.WriteLine($"      Creating new partial class: {vNode.Name}")
                                Dim lNewClass As New SyntaxNode(CodeNodeType.eClass, vNode.Name)
                                vNode.CopyNodeAttributesTo(lNewClass)
                                lNewClass.IsPartial = True
                                
                                ' Initialize and set file paths
                                If lNewClass.Attributes Is Nothing Then
                                    lNewClass.Attributes = New Dictionary(Of String, String)()
                                End If
                                lNewClass.Attributes("FilePath") = vFilePath
                                lNewClass.Attributes("FilePaths") = vFilePath
                                
                                ' Add complete node with all children
                                vCurrentNamespace.AddChild(lNewClass)
                                
                                ' Add all members
                                For Each lChild In vNode.Children
                                    Dim lNewMember As New SyntaxNode(lChild.NodeType, lChild.Name)
                                    lChild.CopyNodeAttributesTo(lNewMember)
                                    
                                    If lNewMember.Attributes Is Nothing Then
                                        lNewMember.Attributes = New Dictionary(Of String, String)()
                                    End If
                                    lNewMember.Attributes("FilePath") = vFilePath
                                    
                                    ' Add all children of the member
                                    For Each lGrandChild In lChild.Children
                                        lNewMember.AddChild(lGrandChild)
                                    Next
                                    
                                    lNewClass.AddChild(lNewMember)
                                Next
                            End If
                        Else
                            ' Non-partial class - check if it already exists
                            Dim lExistingClass As SyntaxNode = FindChildByNameAndType(vCurrentNamespace, vNode.Name, CodeNodeType.eClass)
                            
                            If lExistingClass Is Nothing Then
                                ' Add the complete class with all its children
                                vNode.Metadata = New Dictionary(Of String, Object) From {{"FilePath", vFilePath}}
                                vCurrentNamespace.AddChild(vNode)
                            Else
                                Console.WriteLine($"      WARNING: Non-partial class {vNode.Name} already exists!")
                            End If
                        End If
                        
                    Case CodeNodeType.eModule, CodeNodeType.eInterface, 
                         CodeNodeType.eStructure, CodeNodeType.eEnum, CodeNodeType.eDelegate
                        ' Check if these types already exist too
                        Dim lExistingNode As SyntaxNode = FindChildByNameAndType(vCurrentNamespace, vNode.Name, vNode.NodeType)
                        
                        If lExistingNode Is Nothing Then
                            ' Add the complete type with ALL its children to the namespace
                            vNode.Metadata = New Dictionary(Of String, Object) From {{"FilePath", vFilePath}}
                            vCurrentNamespace.AddChild(vNode)
                            Console.WriteLine($"      Adding {vNode.NodeType}: {vNode.Name}")
                        Else
                            ' CRITICAL FIX: For modules, check if they should be merged
                            If vNode.NodeType = CodeNodeType.eModule AndAlso vNode.IsPartial Then
                                Console.WriteLine($"      Merging partial module: {vNode.Name}")
                                lExistingNode.IsPartial = True
                                
                                ' Track file paths
                                If lExistingNode.Attributes Is Nothing Then
                                    lExistingNode.Attributes = New Dictionary(Of String, String)()
                                End If
                                
                                If Not lExistingNode.Attributes.ContainsKey("FilePaths") Then
                                    lExistingNode.Attributes("FilePaths") = vFilePath
                                Else
                                    Dim lPaths As String = lExistingNode.Attributes("FilePaths")
                                    If Not lPaths.Contains(vFilePath) Then
                                        lExistingNode.Attributes("FilePaths") = lPaths & ";" & vFilePath
                                    End If
                                End If
                                
                                ' Merge members
                                For Each lMember In vNode.Children
                                    Dim lExistingMember As SyntaxNode = FindChildByNameAndType(lExistingNode, lMember.Name, lMember.NodeType)
                                    If lExistingMember Is Nothing Then
                                        Dim lNewMember As New SyntaxNode(lMember.NodeType, lMember.Name)
                                        lMember.CopyNodeAttributesTo(lNewMember)
                                        
                                        If lNewMember.Attributes Is Nothing Then
                                            lNewMember.Attributes = New Dictionary(Of String, String)()
                                        End If
                                        lNewMember.Attributes("FilePath") = vFilePath
                                        
                                        For Each lChild In lMember.Children
                                            lNewMember.AddChild(lChild)
                                        Next
                                        
                                        lExistingNode.AddChild(lNewMember)
                                    End If
                                Next
                            Else
                                Console.WriteLine($"      WARNING: {vNode.NodeType} {vNode.Name} already exists!")
                            End If
                        End If
                        
                    Case Else
                        ' Other node types (shouldn't normally appear at namespace level)
                        Console.WriteLine($"      Unexpected node type at namespace level: {vNode.NodeType} - {vNode.Name}")
                        vCurrentNamespace.AddChild(vNode)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessNodeInContext error: {ex.Message}")
                Console.WriteLine($"  Node: {vNode?.Name} ({vNode?.NodeType})")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
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
        ''' Helper method to find a child node by name and type
        ''' </summary>
        ''' <param name="vParent">Parent node to search in</param>
        ''' <param name="vName">Name to search for</param>
        ''' <param name="vNodeType">Type to search for</param>
        ''' <returns>The found node or Nothing</returns>
        Private Function FindChildByNameAndType(vParent As SyntaxNode, vName As String, vNodeType As CodeNodeType) As SyntaxNode
            If vParent Is Nothing OrElse String.IsNullOrEmpty(vName) Then
                Return Nothing
            End If
            
            For Each lChild In vParent.Children
                If lChild.Name = vName AndAlso lChild.NodeType = vNodeType Then
                    Return lChild
                End If
            Next
            
            Return Nothing
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

