' Managers/ProjectManager.vb - Centralized project management system
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
    

    ''' <summary>
    ''' Extension of ProjectManager that manages DocumentModel instances for all project files
    ''' </summary>
    Partial Public Class ProjectManager
        Implements IDisposable
        
        ' ===== Events =====
        Public Event ProjectLoaded(vProjectPath As String)
        Public Event ProjectClosed()
        Public Event ProjectModified()
        Public Event FileAdded(vFilePath As String)
        Public Event FileRemoved(vFilePath As String)
        Public Event FileRenamed(vOldPath As String, vNewPath As String)
        Public Event IdentifierMapUpdated()
        
        ' ===== Private Fields =====
        Private pCurrentProjectInfo As ProjectInfo
        Private pProjectMetadata As ProjectMetadata
        Private pProjectWatcher As System.IO.FileSystemWatcher
        Private pIsProjectOpen As Boolean = False
        Private pIsDirty As Boolean = False
        Private pRootNode as SyntaxNode
        
        ' ===== Properties =====
        Public ReadOnly Property IsProjectOpen As Boolean
            Get
                Return pIsProjectOpen
            End Get
        End Property
        
        Public ReadOnly Property CurrentProjectPath As String
            Get
                Return If(pCurrentProjectInfo?.ProjectPath, "")
            End Get
        End Property

        Public ReadOnly Property CurrentProjectInfo as ProjectInfo
            Get
                Return pCurrentProjectInfo
            End Get
        End Property
        
        Public ReadOnly Property CurrentProjectName As String
            Get
                Return If(pCurrentProjectInfo?.ProjectName, "")
            End Get
        End Property
        
        Public ReadOnly Property CurrentProjectDirectory As String
            Get
                Return If(pCurrentProjectInfo?.ProjectDirectory, "")
            End Get
        End Property
        
        Public ReadOnly Property IsDirty As Boolean
            Get
                Return pIsDirty
            End Get
        End Property

        Public Sub MarkDirty()
            pIsDirty = true
        End Sub
        
        ' ===== Constructor =====
        Public Sub New()
            ' Initialize
        End Sub
        
        ' ===== Project Operations =====
        
        ' Load a project
        Public Function LoadProject(vProjectPath As String) As Boolean
            Try
                ' Validate project file
                If Not File.Exists(vProjectPath) Then
                    Throw New FileNotFoundException($"project file not found: {vProjectPath}")
                End If
                
                ' Close current project if open
                If pIsProjectOpen Then
                    CloseProject()
                End If
                
                ' Parse project file
                Dim lParsedInfo As ProjectFileParser.ProjectInfo = ProjectFileParser.ParseProjectFile(vProjectPath)
                
                ' Convert to our extended ProjectInfo

                pCurrentProjectInfo = New ProjectInfo()
                pCurrentProjectInfo.ProjectName = lParsedInfo.ProjectName
                pCurrentProjectInfo.ProjectPath = lParsedInfo.ProjectPath
                pCurrentProjectInfo.ProjectDirectory = lParsedInfo.ProjectDirectory
                pCurrentProjectInfo.CompileItems = lParsedInfo.CompileItems
                pCurrentProjectInfo.References = lParsedInfo.References
                pCurrentProjectInfo.PackageReferences = lParsedInfo.PackageReferences
                
                ' Initialize source files list from compile items
                For Each lItem In lParsedInfo.CompileItems
                    pCurrentProjectInfo.SourceFiles.Add(Path.Combine(pCurrentProjectInfo.ProjectDirectory, lItem))
                Next
                
                ' Load project metadata
                LoadProjectMetadata()
                
                ' Setup file watcher
                SetupFileWatcher()
                
                ' Set flags
                pIsProjectOpen = True
                pIsDirty = False
                
                ' Raise event
                RaiseEvent ProjectLoaded(vProjectPath)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProject error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Close current project
        Public Sub CloseProject()
            Try
                If Not pIsProjectOpen Then Return
                
                ' Save any pending changes
                If pIsDirty Then
                    SaveProjectMetadata()
                End If
                
                ' Stop file watcher
                If pProjectWatcher IsNot Nothing Then
                    pProjectWatcher.EnableRaisingEvents = False
                    pProjectWatcher.Dispose()
                    pProjectWatcher = Nothing
                End If
                
                ' Clear project info
                pCurrentProjectInfo = Nothing
                pProjectMetadata = Nothing
                pIsProjectOpen = False
                pIsDirty = False
                
                ' Raise event
                RaiseEvent ProjectClosed()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.CloseProject error: {ex.Message}")
            End Try
        End Sub
        
        ' Get current project info
        Public Function GetProjectInfo(vProjectPath As String) As ProjectInfo
            Try
                ' If it's the current project, return cached info
                If pCurrentProjectInfo IsNot Nothing AndAlso 
                   pCurrentProjectInfo.ProjectPath.Equals(vProjectPath, StringComparison.OrdinalIgnoreCase) Then
                    Return pCurrentProjectInfo
                End If
                
                ' Otherwise parse the project file and convert
                Dim lParsedInfo As ProjectFileParser.ProjectInfo = ProjectFileParser.ParseProjectFile(vProjectPath)
                
                ' Convert to our extended ProjectInfo
                Dim lProjectInfo As New ProjectInfo()
                lProjectInfo.ProjectName = lParsedInfo.ProjectName
                lProjectInfo.ProjectPath = lParsedInfo.ProjectPath
                lProjectInfo.ProjectDirectory = lParsedInfo.ProjectDirectory
                lProjectInfo.CompileItems = lParsedInfo.CompileItems
                lProjectInfo.References = lParsedInfo.References
                lProjectInfo.PackageReferences = lParsedInfo.PackageReferences
                
                ' Initialize source files list from compile items
                For Each lItem In lParsedInfo.CompileItems
                    lProjectInfo.SourceFiles.Add(Path.Combine(lProjectInfo.ProjectDirectory, lItem))
                Next
                
                Return lProjectInfo
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetProjectInfo error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Save project info (updates metadata)
        Public Sub SaveProjectInfo(vProjectInfo As ProjectInfo)
            Try
                If vProjectInfo Is Nothing Then Return
                
                ' Update current project info if it's the same project
                If pCurrentProjectInfo IsNot Nothing AndAlso
                   pCurrentProjectInfo.ProjectPath.Equals(vProjectInfo.ProjectPath, StringComparison.OrdinalIgnoreCase) Then
                    pCurrentProjectInfo = vProjectInfo
                    pIsDirty = True
                    SaveProjectMetadata()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.SaveProjectInfo error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== File Operations =====
        
        ' Add file to project
        Public Function AddFileToProject(vFilePath As String, Optional vItemType As String = "Compile") As Boolean
            Try
                If Not pIsProjectOpen Then Return False
                
                ' Make path relative to project
                Dim lRelativePath As String = GetRelativePath(CurrentProjectDirectory, vFilePath)
                
                ' Load project file
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(CurrentProjectPath)
                
                ' Find or create ItemGroup
                Dim lItemGroup As XmlNode = FindOrCreateItemGroup(lDoc, vItemType)
                
                ' Check if already exists
                Dim lExisting As XmlNode = lDoc.SelectSingleNode($"//{vItemType}[@Include='{lRelativePath}']")
                If lExisting IsNot Nothing Then
                    Console.WriteLine($"File already in project: {lRelativePath}")
                    Return False
                End If
                
                ' Create new item element
                Dim lNewItem As XmlElement = lDoc.CreateElement(vItemType, lDoc.DocumentElement.NamespaceURI)
                lNewItem.SetAttribute("Include", lRelativePath)
                lItemGroup.AppendChild(lNewItem)
                
                ' Save project file
                SaveProjectFile(lDoc)
                
                ' Update project info
                If vItemType = "Compile" Then
                    pCurrentProjectInfo.CompileItems.Add(lRelativePath)
                End If
                
                ' Mark as dirty
                pIsDirty = True
                
                ' Raise events
                RaiseEvent FileAdded(vFilePath)
                RaiseEvent ProjectModified()
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.AddFileToProject error: {ex.Message}")
                Return False
            End Try
        End Function
        

        ''' <summary>
        ''' Remove a file from the project and its DocumentModel
        ''' </summary>
        Public Function RemoveFileFromProject(vFilePath As String) As Boolean
            Try
                If Not pIsProjectOpen Then Return False
                Dim lRelativePath As String = GetRelativePath(vFilePath)
                
                If Not pDocumentModels.ContainsKey(lRelativePath) Then
                    Console.WriteLine($"File not in project: {lRelativePath}")
                    Return False
                End If
                
                ' Get the model
                Dim lModel As DocumentModel = pDocumentModels(lRelativePath)
                
                ' Remove event handlers
                RemoveHandler lModel.DocumentParsed, AddressOf OnDocumentParsed
                RemoveHandler lModel.StructureChanged, AddressOf OnDocumentStructureChanged
                RemoveHandler lModel.ModifiedStateChanged, AddressOf OnDocumentModifiedStateChanged
                
                ' Remove from collection
                pDocumentModels.Remove(lRelativePath)
                
                ' Remove from active editors if present
                If pActiveEditors.ContainsKey(lRelativePath) Then
                    pActiveEditors.Remove(lRelativePath)
                End If
                
                ' Update project file
                If pCurrentProjectInfo IsNot Nothing Then
                    pCurrentProjectInfo.SourceFiles.Remove(vFilePath)
                    pCurrentProjectInfo.CompileItems.Remove(lRelativePath)
                    Dim lDoc As New XmlDocument()
                    lDoc.PreserveWhitespace = True
                    lDoc.Load(CurrentProjectPath)
                    SaveProjectFile(lDoc)
                End If
                
                ' Rebuild namespace tree
                BuildUnifiedNamespaceTree()
                
                ' Mark as dirty
                pIsDirty = True

                ' Raise events
                RaiseEvent DocumentModelRemoved(lRelativePath)
                RaiseEvent FileRemoved(vFilePath)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RemoveFileFromProject error: {ex.Message}")
                Return False
            End Try
        End Function

        
        ' ===== Identifier Case Management =====
        
        ' Get identifier case map
        Public Function GetIdentifierCaseMap() As Dictionary(Of String, String)
            Try
                If pProjectMetadata Is Nothing Then
                    Return New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                End If
                
                Return pProjectMetadata.IdentifierCaseMap
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetIdentifierCaseMap error: {ex.Message}")
                Return New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            End Try
        End Function
        
        ' Update identifier case
        Public Sub UpdateIdentifierCase(vOldCase As String, vNewCase As String)
            Try
                If pProjectMetadata Is Nothing Then Return
                
                pProjectMetadata.IdentifierCaseMap(vOldCase) = vNewCase
                pProjectMetadata.LastModified = DateTime.Now
                pIsDirty = True
                
                ' Save immediately for identifier changes
                SaveProjectMetadata()
                
                RaiseEvent IdentifierMapUpdated()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.UpdateIdentifierCase error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Build Configuration =====
        
        ' Get build configuration
        Public Function GetBuildConfiguration() As BuildConfiguration
            Try
                If pProjectMetadata?.BuildConfiguration IsNot Nothing Then
                    Return pProjectMetadata.BuildConfiguration
                End If
                
                ' Return default configuration
                Return New BuildConfiguration()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetBuildConfiguration error: {ex.Message}")
                Return New BuildConfiguration()
            End Try
        End Function
        
        ' Save build configuration
        Public Sub SaveBuildConfiguration(vConfig As BuildConfiguration)
            Try
                If pProjectMetadata Is Nothing Then Return
                
                pProjectMetadata.BuildConfiguration = vConfig
                pProjectMetadata.LastModified = DateTime.Now
                pIsDirty = True
                
                SaveProjectMetadata()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.SaveBuildConfiguration error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods =====
        
        ' Load project metadata
        Private Sub LoadProjectMetadata()
            Try
                Dim lMetadataPath As String = GetProjectMetadataPath()
                
                If File.Exists(lMetadataPath) Then
                    Dim lJson As String = File.ReadAllText(lMetadataPath)
                    pProjectMetadata = JsonConvert.DeserializeObject(Of ProjectMetadata)(lJson)
                Else
                    ' Create new metadata
                    pProjectMetadata = New ProjectMetadata()
                    pProjectMetadata.ProjectPath = CurrentProjectPath
                    pProjectMetadata.CreatedDate = DateTime.Now
                End If
                
                ' Ensure collections are initialized
                If pProjectMetadata.IdentifierCaseMap Is Nothing Then
                    pProjectMetadata.IdentifierCaseMap = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                End If
                
                If pProjectMetadata.CustomSettings Is Nothing Then
                    pProjectMetadata.CustomSettings = New Dictionary(Of String, String)()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProjectMetadata error: {ex.Message}")
                ' Create default metadata on error
                pProjectMetadata = New ProjectMetadata()
                pProjectMetadata.ProjectPath = CurrentProjectPath
            End Try
        End Sub
        
        ' Save project metadata
        Private Sub SaveProjectMetadata()
            Try
                If pProjectMetadata Is Nothing Then Return
                
                Dim lMetadataPath As String = GetProjectMetadataPath()
                
                ' Ensure directory exists
                Dim lDir As String = Path.GetDirectoryName(lMetadataPath)
                If Not Directory.Exists(lDir) Then
                    Directory.CreateDirectory(lDir)
                End If
                
                ' Serialize to JSON
                Dim lJson As String = JsonConvert.SerializeObject(pProjectMetadata, Newtonsoft.Json.Formatting.Indented)
                File.WriteAllText(lMetadataPath, lJson)
                
                pIsDirty = False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.SaveProjectMetadata error: {ex.Message}")
            End Try
        End Sub
        
        ' Get project metadata file path
        Private Function GetProjectMetadataPath() As String
            Try
                If String.IsNullOrEmpty(CurrentProjectPath) Then Return ""
                
                Dim lObjDir As String = Path.Combine(CurrentProjectDirectory, "obj", "SimpleIDE")
                Return Path.Combine(lObjDir, "project.Metadata.json")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetProjectMetadataPath error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' Setup file watcher
        Private Sub SetupFileWatcher()
            Try
                If pProjectWatcher IsNot Nothing Then
                    pProjectWatcher.Dispose()
                End If
                
                pProjectWatcher = New System.IO.FileSystemWatcher(CurrentProjectDirectory)
                pProjectWatcher.Filter = "*.vb"
                pProjectWatcher.IncludeSubdirectories = True
                pProjectWatcher.NotifyFilter = NotifyFilters.FileName Or NotifyFilters.LastWrite
                
                AddHandler pProjectWatcher.Created, AddressOf OnFileCreated
                AddHandler pProjectWatcher.Deleted, AddressOf OnFileDeleted
                AddHandler pProjectWatcher.Renamed, AddressOf OnFileRenamed
                
                pProjectWatcher.EnableRaisingEvents = True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.SetupFileWatcher error: {ex.Message}")
            End Try
        End Sub
        
        ' File watcher event handlers
        Private Sub OnFileCreated(vSender As Object, vArgs As FileSystemEventArgs)
            Try
                Console.WriteLine($"project file Created: {vArgs.Name}")
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.OnFileCreated error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFileDeleted(vSender As Object, vArgs As FileSystemEventArgs)
            Try
                Console.WriteLine($"project file deleted: {vArgs.Name}")
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.OnFileDeleted error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFileRenamed(vSender As Object, vArgs As RenamedEventArgs)
            Try
                Console.WriteLine($"project file renamed: {vArgs.OldName} -> {vArgs.Name}")
                RaiseEvent FileRenamed(vArgs.OldFullPath, vArgs.FullPath)
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.OnFileRenamed error: {ex.Message}")
            End Try
        End Sub
        
        
        Private Function FindOrCreateItemGroup(vDoc As XmlDocument, vItemType As String) As XmlNode
            Try
                ' Try to find existing ItemGroup with this item type
                Dim lItemGroup As XmlNode = vDoc.SelectSingleNode($"//ItemGroup[{vItemType}]")
                
                If lItemGroup Is Nothing Then
                    ' Create new ItemGroup
                    lItemGroup = vDoc.CreateElement("ItemGroup", vDoc.DocumentElement.NamespaceURI)
                    vDoc.DocumentElement.AppendChild(lItemGroup)
                End If
                
                Return lItemGroup
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.FindOrCreateItemGroup error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Sub SaveProjectFile(vDoc As XmlDocument)
            Try
                ' Format and save
                Dim lSettings As New XmlWriterSettings()
                lSettings.Indent = True
                lSettings.IndentChars = "  "
                lSettings.NewLineChars = Environment.NewLine
                lSettings.NewLineHandling = NewLineHandling.Replace
                
                Using lWriter As XmlWriter = XmlWriter.Create(CurrentProjectPath, lSettings)
                    vDoc.Save(lWriter)
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.SaveProjectFile error: {ex.Message}")
                Throw
            End Try
        End Sub
        
        ' ===== IDisposable Implementation =====
        Private pDisposed As Boolean = False
        
        Protected Overridable Sub Dispose(vDisposing As Boolean)
            If Not pDisposed Then
                If vDisposing Then
                    ' Dispose managed resources
                    CloseProject()
                End If
                pDisposed = True
            End If
        End Sub
        
        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        ' ===== Updated Event Delegates =====
        
        ''' <summary>
        ''' Event handler for project structure changes with SyntaxNode
        ''' </summary>
        Public Delegate Sub ProjectStructureChangedSyntaxEventHandler(vRootNode As SyntaxNode)
        
        ''' <summary>
        ''' Additional event for SyntaxNode structure changes
        ''' </summary>
        Public Event ProjectStructureChangedSyntax As ProjectStructureChangedSyntaxEventHandler
        
        ''' <summary>
        ''' Raise both events for compatibility
        ''' </summary>
        Private Sub RaiseStructureChangedEvents(vSyntaxNode As SyntaxNode)
            Try
                ' Raise the SyntaxNode event
                RaiseEvent ProjectStructureChangedSyntax(vSyntaxNode)
                
                ' Convert and raise DocumentNode event if possible
                Dim lDocumentNode As DocumentNode = ConvertSyntaxNodeToDocumentNode(vSyntaxNode)
                If lDocumentNode IsNot Nothing Then
                    RaiseEvent ProjectStructureChanged(lDocumentNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RaiseStructureChangedEvents error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Convert SyntaxNode to DocumentNode
        ''' </summary>
        Public Shared Function ConvertSyntaxNodeToDocumentNode(vSyntaxNode As SyntaxNode) As DocumentNode
            Try
                If vSyntaxNode Is Nothing Then Return Nothing
                
                ' Create corresponding DocumentNode
                Dim lDocNode As New DocumentNode()
                lDocNode.Name = vSyntaxNode.Name
                lDocNode.NodeType = vSyntaxNode.NodeType
                lDocNode.StartLine = vSyntaxNode.StartLine
                lDocNode.EndLine = vSyntaxNode.EndLine
                lDocNode.FilePath = vSyntaxNode.FilePath
                
                ' Convert visibility
                Select Case vSyntaxNode.Visibility
                    Case SyntaxNode.eVisibility.ePublic
                        lDocNode.Visibility = "Public"
                    Case SyntaxNode.eVisibility.ePrivate
                        lDocNode.Visibility = "Private"
                    Case SyntaxNode.eVisibility.eProtected
                        lDocNode.Visibility = "Protected"
                    Case SyntaxNode.eVisibility.eFriend
                        lDocNode.Visibility = "Friend"
                    Case Else
                        lDocNode.Visibility = ""
                End Select
                
                ' Convert other properties
                lDocNode.IsPartial = vSyntaxNode.IsPartial
                lDocNode.IsShared = vSyntaxNode.IsShared
                lDocNode.IsMustInherit = vSyntaxNode.IsMustInherit
                lDocNode.IsNotInheritable = vSyntaxNode.IsNotInheritable
                
                ' Convert children recursively
                For Each lChild In vSyntaxNode.Children
                    Dim lChildDoc As DocumentNode = ConvertSyntaxNodeToDocumentNode(lChild)
                    If lChildDoc IsNot Nothing Then
                        lDocNode.Children.Add(lChildDoc)
                        lChildDoc.Parent = lDocNode
                    End If
                Next
                
                Return lDocNode
                
            Catch ex As Exception
                Console.WriteLine($"ConvertSyntaxNodeToDocumentNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Convert DocumentNode to SyntaxNode
        ''' </summary>
        Public Function ConvertDocumentNodeToSyntaxNode(vDocNode As DocumentNode) As SyntaxNode
            Try
                If vDocNode Is Nothing Then Return Nothing
                
                ' Create corresponding SyntaxNode
                Dim lSyntaxNode As New SyntaxNode(vDocNode.NodeType, vDocNode.Name)
                lSyntaxNode.StartLine = vDocNode.StartLine
                lSyntaxNode.EndLine = vDocNode.EndLine
                lSyntaxNode.FilePath = vDocNode.FilePath
                
                ' Convert visibility
                lSyntaxNode.Visibility = vDocNode.Visibility
                
                ' Convert other properties
                lSyntaxNode.IsPartial = vDocNode.IsPartial
                lSyntaxNode.IsShared = vDocNode.IsShared
                lSyntaxNode.IsMustInherit = vDocNode.IsMustInherit
                lSyntaxNode.IsNotInheritable = vDocNode.IsNotInheritable
                
                ' Convert children recursively
                For Each lChild In vDocNode.Children
                    Dim lChildSyntax As SyntaxNode = ConvertDocumentNodeToSyntaxNode(lChild)
                    If lChildSyntax IsNot Nothing Then
                        lSyntaxNode.AddChild(lChildSyntax)
                    End If
                Next
                
                Return lSyntaxNode
                
            Catch ex As Exception
                Console.WriteLine($"ConvertDocumentNodeToSyntaxNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        

        
        ''' <summary>
        ''' Fixed version of MergeNodeIntoProject that properly handles Attributes
        ''' </summary>
        Private Sub MergeNodeIntoProjectFixed(vNode As SyntaxNode, vParentNode As SyntaxNode, vFilePath As String)
            Try
                If vNode Is Nothing Then Return
                
                Console.WriteLine($"    MergeNodeIntoProjectFixed: {vNode.Name} ({vNode.NodeType}) into {vParentNode.Name}")
                
                ' Ensure Attributes dictionary is initialized
                If vNode.Attributes Is Nothing Then
                    vNode.Attributes = New Dictionary(Of String, String)()
                End If
                
                ' Handle namespace nodes specially - merge if already exists
                If vNode.NodeType = CodeNodeType.eNamespace Then
                    ' Find or create namespace node
                    Dim lExistingNamespace As SyntaxNode = FindChildByNameAndType(vParentNode, vNode.Name, CodeNodeType.eNamespace)
                    
                    If lExistingNamespace IsNot Nothing Then
                        ' Merge into existing namespace
                        Console.WriteLine($"      Merging into existing namespace: {lExistingNamespace.Name}")
                        For Each lChild In vNode.Children
                            MergeNodeIntoProjectFixed(lChild, lExistingNamespace, vFilePath)
                        Next
                    Else
                        ' Create new namespace node
                        Console.WriteLine($"      Creating new namespace: {vNode.Name}")
                        Dim lNewNamespace As New SyntaxNode(CodeNodeType.eNamespace, vNode.Name)
                        vNode.CopyNodeAttributesTo(lNewNamespace)
                        
                        ' Initialize and set file path
                        If lNewNamespace.Attributes Is Nothing Then
                            lNewNamespace.Attributes = New Dictionary(Of String, String)()
                        End If
                        lNewNamespace.Attributes("FilePath") = vFilePath
                        
                        vParentNode.AddChild(lNewNamespace)
                        
                        ' Add children to new namespace
                        For Each lChild In vNode.Children
                            MergeNodeIntoProjectFixed(lChild, lNewNamespace, vFilePath)
                        Next
                    End If
                    
                ElseIf vNode.NodeType = CodeNodeType.eClass AndAlso vNode.IsPartial Then
                    ' Handle partial classes - merge members if class already exists
                    Dim lExistingClass As SyntaxNode = FindChildByNameAndType(vParentNode, vNode.Name, CodeNodeType.eClass)
                    
                    If lExistingClass IsNot Nothing Then
                        ' Merge members into existing class
                        Console.WriteLine($"      Merging partial class: {vNode.Name}")
                        lExistingClass.IsPartial = True ' Mark as partial
                        
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
                        
                        ' Merge all members
                        For Each lMember In vNode.Children
                            ' Check if this member already exists (avoid duplicates)
                            Dim lExistingMember As SyntaxNode = FindChildByNameAndType(lExistingClass, lMember.Name, lMember.NodeType)
                            If lExistingMember Is Nothing Then
                                Dim lNewMember As New SyntaxNode(lMember.NodeType, lMember.Name)
                                lMember.CopyNodeAttributesTo(lNewMember)
                                
                                ' Initialize and set file path
                                If lNewMember.Attributes Is Nothing Then
                                    lNewMember.Attributes = New Dictionary(Of String, String)()
                                End If
                                lNewMember.Attributes("FilePath") = vFilePath
                                
                                lExistingClass.AddChild(lNewMember)
                            End If
                        Next
                    Else
                        ' Create new partial class
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
                        
                        vParentNode.AddChild(lNewClass)
                        
                        ' Add all members
                        For Each lChild In vNode.Children
                            MergeNodeIntoProjectFixed(lChild, lNewClass, vFilePath)
                        Next
                    End If
                    
                Else
                    ' For non-namespace, non-partial-class nodes
                    ' Check if this exact node already exists
                    Dim lExistingNode As SyntaxNode = FindChildByNameAndType(vParentNode, vNode.Name, vNode.NodeType)
                    
                    If lExistingNode Is Nothing Then
                        ' Create new node in project tree
                        Console.WriteLine($"      Creating new {vNode.NodeType}: {vNode.Name}")
                        Dim lNewNode As New SyntaxNode(vNode.NodeType, vNode.Name)
                        vNode.CopyNodeAttributesTo(lNewNode)
                        
                        ' Initialize and set file path
                        If lNewNode.Attributes Is Nothing Then
                            lNewNode.Attributes = New Dictionary(Of String, String)()
                        End If
                        lNewNode.Attributes("FilePath") = vFilePath
                        
                        vParentNode.AddChild(lNewNode)
                        
                        ' Add all children
                        For Each lChild In vNode.Children
                            MergeNodeIntoProjectFixed(lChild, lNewNode, vFilePath)
                        Next
                    Else
                        Console.WriteLine($"      Node already exists: {vNode.Name} ({vNode.NodeType})")
                        
                        ' Initialize Attributes if needed
                        If lExistingNode.Attributes Is Nothing Then
                            lExistingNode.Attributes = New Dictionary(Of String, String)()
                        End If
                        
                        ' Update file paths if tracking multiple files
                        If Not lExistingNode.Attributes.ContainsKey("FilePaths") Then
                            lExistingNode.Attributes("FilePaths") = vFilePath
                        Else
                            Dim lPaths As String = lExistingNode.Attributes("FilePaths")
                            If Not lPaths.Contains(vFilePath) Then
                                lExistingNode.Attributes("FilePaths") = lPaths & ";" & vFilePath
                            End If
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNodeIntoProjectFixed error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub
        

        
        ''' <summary>
        ''' Update existing MergeFileStructure to use the fixed version
        ''' </summary>
        Private Shadows Sub MergeFileStructure(vFileInfo As SourceFileInfo, vRootNamespace As SyntaxNode)
            ' Call the fixed version
            MergeFileStructureFixed(vFileInfo, vRootNamespace)
        End Sub
        
        ''' <summary>
        ''' Update existing MergeNodeIntoProject to use the fixed version
        ''' </summary>
        Private Shadows Sub MergeNodeIntoProject(vNode As SyntaxNode, vParentNode As SyntaxNode, vFilePath As String)
            ' Call the fixed version
            MergeNodeIntoProjectFixed(vNode, vParentNode, vFilePath)
        End Sub
        
        ''' <summary>
        ''' Update existing CopyNodeAttributes to use the fixed version
        ''' </summary>
        Private Shadows Sub CopyNodeAttributes(vSource As SyntaxNode, vTarget As SyntaxNode)
            ' Call the fixed version
            vSource.CopyNodeAttributesTo(vTarget)
        End Sub

        ''' <summary>
        ''' Load and parse all project source files with correct namespace organization and sorting
        ''' </summary>
        Public Function LoadProjectStructure() As Boolean
            Try
                If Not pIsProjectOpen Then
                    Console.WriteLine("No project is open")
                    Return False
                End If
                
                pIsLoadingStructure = True
                pSourceFiles.Clear()
                
                ' Get the actual root namespace from project info
                ' Make sure pCurrentProjectInfo has the RootNamespace property populated
                Dim lRootNamespaceName As String
                If pCurrentProjectInfo IsNot Nothing AndAlso 
                   TypeOf pCurrentProjectInfo Is ProjectInfo Then
                    Dim lProjectInfo As ProjectInfo = DirectCast(pCurrentProjectInfo, ProjectInfo)
                    lRootNamespaceName = lProjectInfo.GetEffectiveRootNamespace()
                Else
                    ' Fallback to project name
                    lRootNamespaceName = Path.GetFileNameWithoutExtension(pCurrentProjectInfo.ProjectPath)
                End If
                
                Console.WriteLine($"Loading project structure with root namespace: {lRootNamespaceName}")
                
                ' Create root document node and namespace
                pProjectSyntaxTree = New SyntaxNode(CodeNodeType.eDocument, pCurrentProjectInfo.ProjectName)
                Dim lRootNamespace As New SyntaxNode(CodeNodeType.eNamespace, lRootNamespaceName)
                lRootNamespace.IsImplicit = True
                pProjectSyntaxTree.AddChild(lRootNamespace)
                
                ' Dictionary to track namespace nodes
                Dim lNamespaceNodes As New Dictionary(Of String, SyntaxNode)(StringComparer.OrdinalIgnoreCase)
                lNamespaceNodes(lRootNamespaceName) = lRootNamespace
                
                ' Get all source files
                Dim lSourceFiles As List(Of String) = GetProjectSourceFiles()
                Console.WriteLine($"Found {lSourceFiles.Count} source files")
                
                ' Parse and process each file
                For Each lFilePath In lSourceFiles
                    ' Create SourceFileInfo
                    Dim lFileInfo As New SourceFileInfo(lFilePath, pCurrentProjectInfo.ProjectDirectory)
                    lFileInfo.ProjectRootNamespace = lRootNamespaceName
                    
                    ' Raise progress event
                    RaiseEvent ParsingProgress(pSourceFiles.Count + 1, lSourceFiles.Count, lFileInfo.FileName)
                    
                    ' Load and parse the file
                    If lFileInfo.LoadAndParse() Then
                        ' Add to dictionary
                        pSourceFiles(lFilePath) = lFileInfo
                        
                        ' Process the parsed structure and organize by namespace
                        If lFileInfo.SyntaxTree IsNot Nothing Then
                            ProcessFileStructure(lFileInfo, lRootNamespace, lNamespaceNodes, lRootNamespaceName)
                        End If
                        
                        ' Raise file parsed event
                        RaiseEvent FileParsed(lFileInfo)
                    End If
                Next
                
                ' Build final namespace tree structure
                BuildNamespaceHierarchy(lNamespaceNodes, lRootNamespace)
                
                ' CRITICAL FIX: Sort the entire project tree recursively
                Console.WriteLine("Sorting project structure...")
                SortNodeChildrenRecursively(pProjectSyntaxTree)
                
                pIsLoadingStructure = False
                
                ' Raise project structure loaded event
                RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                
                Console.WriteLine($"Project structure loaded with root namespace: {lRootNamespaceName}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProjectStructure error: {ex.Message}")
                pIsLoadingStructure = False
                Return False
            End Try
        End Function

        ' ===== Supporting Classes =====
    
        ''' <summary>
        ''' Recursively sort all children of a syntax node with namespaces first, then alphabetically
        ''' </summary>
        ''' <param name="vNode">The node whose children to sort</param>
        Private Sub SortNodeChildrenRecursively(vNode As SyntaxNode)
            Try
                If vNode Is Nothing OrElse vNode.Children.Count = 0 Then Return
                
                ' Sort this node's children: namespaces first, then by name
                Dim lSortedChildren = vNode.Children.OrderBy(Function(n) GetNodeTypeSortPriority(n.NodeType)) _
                                                   .ThenBy(Function(n) n.Name, StringComparer.OrdinalIgnoreCase) _
                                                   .ToList()
                
                vNode.Children.Clear()
                vNode.Children.AddRange(lSortedChildren)
                
                ' Recursively sort each child's children
                For Each lChild In vNode.Children
                    SortNodeChildrenRecursively(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SortNodeChildrenRecursively error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Get sort priority for node types (namespaces first, then other types)
        ''' </summary>
        ''' <param name="vNodeType">The node type</param>
        ''' <returns>Sort priority (lower = higher priority)</returns>
        Private Function GetNodeTypeSortPriority(vNodeType As CodeNodeType) As Integer
            Select Case vNodeType
                ' Namespaces always come first
                Case CodeNodeType.eNamespace
                    Return 0
                    
                ' Then type definitions in logical order
                Case CodeNodeType.eInterface
                    Return 10
                Case CodeNodeType.eClass
                    Return 11
                Case CodeNodeType.eModule
                    Return 12
                Case CodeNodeType.eStructure
                    Return 13
                Case CodeNodeType.eEnum
                    Return 14
                Case CodeNodeType.eDelegate
                    Return 15
                    
                ' Then members grouped by type
                Case CodeNodeType.eConstructor
                    Return 20
                Case CodeNodeType.eProperty
                    Return 21
                Case CodeNodeType.eMethod
                    Return 22
                Case CodeNodeType.eFunction
                    Return 23
                Case CodeNodeType.eEvent
                    Return 24
                Case CodeNodeType.eField
                    Return 25
                Case CodeNodeType.eConstant
                    Return 26
                Case CodeNodeType.eOperator
                    Return 27
                    
                ' Everything else at the end
                Case Else
                    Return 99
            End Select
        End Function

    End Class
    
    ' Project metadata stored separately from project file
    Public Class ProjectMetadata
        Public Property ProjectPath As String
        Public Property CreatedDate As DateTime
        Public Property LastModified As DateTime
        Public Property IdentifierCaseMap As Dictionary(Of String, String)
        Public Property BuildConfiguration As BuildConfiguration
        Public Property CustomSettings As Dictionary(Of String, String)
        Public Property RecentSearches As List(Of String)
        Public Property Bookmarks As List(Of BookmarkInfo)
        
        Public Sub New()
            CreatedDate = DateTime.Now
            LastModified = DateTime.Now
            IdentifierCaseMap = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            CustomSettings = New Dictionary(Of String, String)()
            RecentSearches = New List(Of String)()
            Bookmarks = New List(Of BookmarkInfo)()
        End Sub
    End Class
    
    ' Bookmark information
    Public Class BookmarkInfo
        Public Property FilePath As String
        Public Property Line As Integer
        Public Property Description As String
        Public Property Created As DateTime
    End Class

    
End Namespace