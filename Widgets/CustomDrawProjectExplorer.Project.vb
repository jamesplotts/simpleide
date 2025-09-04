' Widgets/CustomDrawProjectExplorer.Project.vb - Project loading and structure management
' Created: 2025-08-17
Imports Gtk
Imports System
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing project loading and structure management
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Project Loading Methods =====
        
        ''' <summary>
        ''' Loads the project structure from the .vbproj file
        ''' </summary>
        Private Sub LoadxProjectStructure()
            Try
                If Not System.IO.File.Exists(pProjectFile) Then Return
                
                ' Parse the .vbproj file
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Process compile items (source files)
                ProcessCompileItems(lDoc)
                
                ' Process folders
                ProcessProjectFolders()
                
                ' Sort the tree
                pRootNode?.SortChildren()
                
            Catch ex As Exception
                Console.WriteLine($"LoadProjectStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes compile items from the project file
        ''' </summary>
        Private Sub ProcessCompileItems(vDoc As XmlDocument)
            Try
                Console.WriteLine("ProcessCompileItems: Starting to process project file")
                
                ' First, try to detect if this is an SDK-style project
                Dim lIsSdkProject As Boolean = False
                Dim lRootElement As XmlElement = vDoc.DocumentElement
                
                If lRootElement IsNot Nothing Then
                    ' Check for Sdk attribute (SDK-style projects)
                    lIsSdkProject = lRootElement.HasAttribute("Sdk")
                    Console.WriteLine($"  Project type: {If(lIsSdkProject, "SDK-style", "Classic MSBuild")}")
                End If
                
                ' For SDK-style projects, we need to scan the actual file system
                ' because they use globbing patterns by default
                If lIsSdkProject Then
                    Console.WriteLine("  Using file system scan for SDK-style project")
                    ScanProjectDirectory()
                Else
                    ' For classic projects, parse the XML
                    Console.WriteLine("  Parsing XML for classic project")
                    
                    ' Setup namespace manager for classic MSBuild projects
                    Dim lNamespaceManager As New XmlNamespaceManager(vDoc.NameTable)
                    lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                    
                    ' Process different item types
                    ProcessXmlItems(vDoc, lNamespaceManager, "Compile")
                    ProcessXmlItems(vDoc, lNamespaceManager, "None")
                    ProcessXmlItems(vDoc, lNamespaceManager, "Content")
                    ProcessXmlItems(vDoc, lNamespaceManager, "EmbeddedResource")
                End If
                
                Console.WriteLine($"ProcessCompileItems: Completed, found {CountFiles(pRootNode)} files")
                
            Catch ex As Exception
                Console.WriteLine($"ProcessCompileItems error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a single project file
        ''' </summary>
        Private Sub ProcessProjectFile(vRelativePath As String)
            Try
                ' Normalize path separators
                Dim lNormalizedPath As String = vRelativePath.Replace("\"c, "/"c)
                Dim lPathParts As String() = lNormalizedPath.Split("/"c)
                
                ' Navigate/create folder structure
                Dim lCurrentNode As ProjectNode = pRootNode
                
                ' Process all parts except the last (which is the file)
                For i As Integer = 0 To lPathParts.Length - 2
                    Dim lFolderName As String = lPathParts(i)
                    
                    ' Skip empty parts
                    If String.IsNullOrEmpty(lFolderName) Then Continue For
                    
                    ' Check if folder already exists
                    Dim lExistingFolder As ProjectNode = lCurrentNode.Children.FirstOrDefault(
                        Function(n) n.Name = lFolderName AndAlso Not n.IsFile)
                    
                    If lExistingFolder IsNot Nothing Then
                        lCurrentNode = lExistingFolder
                    Else
                        ' Create new folder node
                        Dim lFolderPath As String = System.IO.Path.Combine(
                            pProjectDirectory, 
                            String.Join(System.IO.Path.DirectorySeparatorChar, lPathParts.Take(i + 1)))
                        
                        Dim lFolderNode As New ProjectNode() With {
                            .Name = lFolderName,
                            .Path = lFolderPath,
                            .NodeType = GetFolderType(lFolderName),
                            .IsFile = False,
                            .IconName = "folder"
                        }
                        
                        lCurrentNode.AddChild(lFolderNode)
                        lCurrentNode = lFolderNode
                    End If
                Next
                
                ' Add the file itself
                Dim lFileName As String = lPathParts(lPathParts.Length - 1)
                Dim lFilePath As String = System.IO.Path.Combine(pProjectDirectory, vRelativePath)
                
                ' Check if file already exists (avoid duplicates)
                Dim lExistingFile As ProjectNode = lCurrentNode.Children.FirstOrDefault(
                    Function(n) n.Name = lFileName AndAlso n.IsFile)
                
                If lExistingFile Is Nothing Then
                    Dim lFileNode As New ProjectNode() With {
                        .Name = lFileName,
                        .Path = lFilePath,
                        .NodeType = GetFileType(lFileName),
                        .IsFile = True
                    }
                    
                    ' Set appropriate tooltip
                    Select Case lFileNode.NodeType
                        Case ProjectNodeType.eVBFile
                            lFileNode.ToolTip = $"VB.NET Source File: {lFileName}"
                        Case ProjectNodeType.eXMLFile
                            lFileNode.ToolTip = $"XML File: {lFileName}"
                        Case ProjectNodeType.eResourceFile
                            lFileNode.ToolTip = $"Resource File: {lFileName}"
                        Case ProjectNodeType.eConfigFile
                            lFileNode.ToolTip = $"Configuration File: {lFileName}"
                        Case Else
                            lFileNode.ToolTip = lFileName
                    End Select
                    
                    lCurrentNode.AddChild(lFileNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProcessProjectFile error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes folders in the project directory
        ''' </summary>
        Private Sub ProcessProjectFolders()
            Try
                ' Scan for My Project folder if not already added
                Dim lMyProjectPath As String = System.IO.Path.Combine(pProjectDirectory, "My Project")
                If System.IO.Directory.Exists(lMyProjectPath) Then
                    Dim lExisting As ProjectNode = pRootNode.Children.FirstOrDefault(
                        Function(n) n.Name = "My Project" AndAlso Not n.IsFile)
                    
                    If lExisting Is Nothing Then
                        Dim lMyProjectNode As New ProjectNode() With {
                            .Name = "My Project",
                            .Path = lMyProjectPath,
                            .NodeType = ProjectNodeType.eMyProject,
                            .IsFile = False,
                            .IconName = "folder-development",
                            .ToolTip = "Project properties and settings"
                        }
                        
                        pRootNode.Children.Insert(0, lMyProjectNode)
                        lMyProjectNode.Parent = pRootNode
                        
                        ' Scan My Project folder contents
                        ScanFolderContents(lMyProjectNode, lMyProjectPath)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProcessProjectFolders error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Adds a folder and its contents to the tree
        ''' </summary>
        Private Sub AddFolderToTree(vFolderName As String, vFolderPath As String, vParentNode As ProjectNode)
            Try
                If vParentNode Is Nothing Then Return
                If Not System.IO.Directory.Exists(vFolderPath) Then Return
                
                ' Check if folder already exists
                Dim lExisting As ProjectNode = vParentNode.Children.FirstOrDefault(
                    Function(n) n.Name = vFolderName AndAlso Not n.IsFile)
                If lExisting IsNot Nothing Then Return
                
                ' Create folder node
                Dim lFolderNode As New ProjectNode() With {
                    .Name = vFolderName,
                    .Path = vFolderPath,
                    .NodeType = GetFolderType(vFolderName),
                    .IsFile = False
                }
                
                vParentNode.AddChild(lFolderNode)
                
                ' Scan contents
                ScanFolderContents(lFolderNode, vFolderPath)
                
            Catch ex As Exception
                Console.WriteLine($"AddFolderToTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Scans folder contents and adds them to the tree
        ''' </summary>
        Private Sub ScanFolderContents(vParentNode As ProjectNode, vFolderPath As String)
            Try
                If Not System.IO.Directory.Exists(vFolderPath) Then Return
                
                ' Add subdirectories
                Dim lDirectories As String() = System.IO.Directory.GetDirectories(vFolderPath)
                For Each lDir In lDirectories
                    Dim lDirName As String = System.IO.Path.GetFileName(lDir)
                    
                    ' Skip hidden folders
                    If lDirName.StartsWith(".") Then Continue For
                    
                    Dim lFolderNode As New ProjectNode() With {
                        .Name = lDirName,
                        .Path = lDir,
                        .NodeType = GetFolderType(lDirName),
                        .IsFile = False
                    }
                    
                    vParentNode.AddChild(lFolderNode)
                    
                    ' Recursively scan subfolders
                    ScanFolderContents(lFolderNode, lDir)
                Next
                
                ' Add files
                Dim lFiles As String() = System.IO.Directory.GetFiles(vFolderPath)
                For Each lFile In lFiles
                    Dim lFileName As String = System.IO.Path.GetFileName(lFile)
                    
                    ' Skip hidden files
                    If lFileName.StartsWith(".") Then Continue For
                    
                    Dim lFileNode As New ProjectNode() With {
                        .Name = lFileName,
                        .Path = lFile,
                        .NodeType = GetFileType(lFileName),
                        .IsFile = True
                    }
                    
                    vParentNode.AddChild(lFileNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ScanFolderContents error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates special nodes (References, Resources, Manifest)
        ''' </summary>
        Private Sub CreateSpecialNodes()
            Try
                ' Create References node if needed
                If Not pHasReferencesNode Then
                    CreateReferencesNode()
                End If
                
                ' Create Resources node if needed
                If Not pHasResourcesNode Then
                    CreateResourcesNode()
                End If
                
                ' Create Manifest node if needed
                If Not pHasManifestNode Then
                    CreateManifestNode()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CreateSpecialNodes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the References node
        ''' </summary>
        Private Sub CreateReferencesNode()
            Try
                pReferencesNode = New ProjectNode() With {
                    .Name = "References",
                    .Path = "",
                    .NodeType = ProjectNodeType.eReferences,
                    .IsFile = False,
                    .IconName = "emblem-symbolic-link",
                    .ToolTip = "Project references and dependencies"
                }
                
                ' Add at the beginning
                If pRootNode IsNot Nothing Then
                    pRootNode.Children.Insert(0, pReferencesNode)
                    pReferencesNode.Parent = pRootNode
                End If
                
                pHasReferencesNode = True
                
                ' TODO: Load actual references from project file
                
            Catch ex As Exception
                Console.WriteLine($"CreateReferencesNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the Resources node
        ''' </summary>
        Private Sub CreateResourcesNode()
            Try
                ' Check if Resources folder exists
                Dim lResourcesPath As String = System.IO.Path.Combine(pProjectDirectory, "Resources")
                If System.IO.Directory.Exists(lResourcesPath) Then
                    pResourcesNode = New ProjectNode() With {
                        .Name = "Resources",
                        .Path = lResourcesPath,
                        .NodeType = ProjectNodeType.eResources,
                        .IsFile = False,
                        .IconName = "folder-pictures",
                        .ToolTip = "Project resources (images, icons, etc.)"
                    }
                    
                    If pRootNode IsNot Nothing Then
                        ' Add after References if it exists, otherwise at beginning
                        Dim lInsertIndex As Integer = If(pHasReferencesNode, 1, 0)
                        pRootNode.Children.Insert(lInsertIndex, pResourcesNode)
                        pResourcesNode.Parent = pRootNode
                    End If
                    
                    pHasResourcesNode = True
                    
                    ' Scan resources folder
                    ScanFolderContents(pResourcesNode, lResourcesPath)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CreateResourcesNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the Manifest node
        ''' </summary>
        Public Sub CreateManifestNode()
            Try
                ' Check for app.manifest file
                Dim lManifestPath As String = System.IO.Path.Combine(pProjectDirectory, "app.manifest")
                If Not System.IO.File.Exists(lManifestPath) Then
                    lManifestPath = System.IO.Path.Combine(pProjectDirectory, "My Project", "app.manifest")
                End If
                
                If System.IO.File.Exists(lManifestPath) Then
                    pManifestNode = New ProjectNode() With {
                        .Name = "Application Manifest",
                        .Path = lManifestPath,
                        .NodeType = ProjectNodeType.eManifest,
                        .IsFile = True,
                        .IconName = "application-certificate",
                        .ToolTip = "Application manifest file"
                    }
                    
                    If pRootNode IsNot Nothing Then
                        ' Add after Resources if it exists, or after References, or at beginning
                        Dim lInsertIndex As Integer = 0
                        If pHasResourcesNode Then
                            lInsertIndex = 2
                        ElseIf pHasReferencesNode Then
                            lInsertIndex = 1
                        End If
                        
                        pRootNode.Children.Insert(lInsertIndex, pManifestNode)
                        pManifestNode.Parent = pRootNode
                    End If
                    
                    pHasManifestNode = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CreateManifestNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Finds or creates a folder node in the tree
        ''' </summary>
        Private Function FindOrCreateFolderNode(vParentNode As ProjectNode, vFolderName As String, vFolderPath As String) As ProjectNode
            Try
                If vParentNode Is Nothing Then Return Nothing
                
                ' Check if folder already exists
                For Each lChild In vParentNode.Children
                    If Not lChild.IsFile AndAlso lChild.Name = vFolderName Then
                        Return lChild
                    End If
                Next
                
                ' Create new folder node
                Dim lFolderNode As New ProjectNode() With {
                    .Name = vFolderName,
                    .Path = vFolderPath,
                    .NodeType = GetFolderType(vFolderName),
                    .IsFile = False
                }
                
                vParentNode.AddChild(lFolderNode)
                Return lFolderNode
                
            Catch ex As Exception
                Console.WriteLine($"FindOrCreateFolderNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the node type for a file based on its extension
        ''' </summary>
        Private Function GetFileType(vFileName As String) As ProjectNodeType
            Try
                Dim lExtension As String = System.IO.Path.GetExtension(vFileName).ToLower()
                
                Select Case lExtension
                    Case ".vb"
                        Return ProjectNodeType.eVBFile
                    Case ".xml", ".xaml"
                        Return ProjectNodeType.eXMLFile
                    Case ".resx", ".resources"
                        Return ProjectNodeType.eResourceFile
                    Case ".config", ".settings"
                        Return ProjectNodeType.eConfigFile
                    Case ".txt", ".md", ".log"
                        Return ProjectNodeType.eTextFile
                    Case ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg"
                        Return ProjectNodeType.eImageFile
                    Case Else
                        Return ProjectNodeType.eTextFile
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetFileType error: {ex.Message}")
                Return ProjectNodeType.eTextFile
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the node type for special folders
        ''' </summary>
        Private Function GetFolderType(vFolderName As String) As ProjectNodeType
            Try
                Select Case vFolderName.ToLower()
                    Case "my project"
                        Return ProjectNodeType.eMyProject
                    Case "resources"
                        Return ProjectNodeType.eResources
                    Case Else
                        Return ProjectNodeType.eFolder
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetFolderType error: {ex.Message}")
                Return ProjectNodeType.eFolder
            End Try
        End Function
        

        ''' <summary>
        ''' Process XML items of a specific type
        ''' </summary>
        Private Sub ProcessXmlItems(vDoc As XmlDocument, vNamespaceManager As XmlNamespaceManager, vItemType As String)
            Try
                ' Try with namespace first
                Dim lNodes As XmlNodeList = vDoc.SelectNodes($"//ms:{vItemType}[@Include]", vNamespaceManager)
                
                ' If no nodes found, try without namespace
                If lNodes Is Nothing OrElse lNodes.Count = 0 Then
                    lNodes = vDoc.SelectNodes($"//{vItemType}[@Include]")
                End If
                
                If lNodes IsNot Nothing AndAlso lNodes.Count > 0 Then
                    Console.WriteLine($"  Found {lNodes.Count} {vItemType} items")
                    
                    For Each lNode As XmlNode In lNodes
                        Dim lInclude As String = lNode.Attributes("Include")?.Value
                        If Not String.IsNullOrEmpty(lInclude) Then
                            ' Skip certain patterns
                            If lInclude.Contains("*") OrElse lInclude.Contains("$(") Then
                                Continue For
                            End If
                            
                            ProcessProjectFile(lInclude)
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProcessXmlItems error for {vItemType}: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Scan project directory for SDK-style projects
        ''' </summary>
        Private Sub ScanProjectDirectory()
            Try
                If String.IsNullOrEmpty(pProjectDirectory) OrElse Not Directory.Exists(pProjectDirectory) Then
                    Console.WriteLine("  Project directory not valid")
                    Return
                End If
                
                Console.WriteLine($"  Scanning directory: {pProjectDirectory}")
                
                ' Scan for VB files recursively
                ScanDirectoryRecursive(pProjectDirectory, pRootNode, "")
                
            Catch ex As Exception
                Console.WriteLine($"ScanProjectDirectory error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively scan directory and add files/folders to tree
        ''' </summary>
        Private Sub ScanDirectoryRecursive(vDirectory As String, vParentNode As ProjectNode, vRelativePath As String)
            Try
                'Console.WriteLine($"ScanDirectoryRecursive: {vDirectory}")
                'Console.WriteLine($"  Parent: {vParentNode?.Name}, RelativePath: '{vRelativePath}'")
                
                ' Only apply skip logic if we're NOT at the project root
                If vDirectory <> pProjectDirectory Then
                    Dim lDirName As String = System.IO.Path.GetFileName(vDirectory)
                    If lDirName = "bin" OrElse lDirName = "obj" OrElse lDirName.StartsWith(".") Then
                        'Console.WriteLine($"  Skipping directory: {lDirName}")
                        Return
                    End If
                Else
                    'Console.WriteLine("  This is the project root - processing all contents")
                End If
                
                ' Get all subdirectories
                Dim lDirs As String() = Directory.GetDirectories(vDirectory)
                'Console.WriteLine($"  Found {lDirs.Length} subdirectories")
                
                For Each lDir In lDirs
                    Dim lSubDirName As String = System.IO.Path.GetFileName(lDir)
                    
                    ' Skip hidden and build directories
                    If lSubDirName.StartsWith(".") OrElse 
                       lSubDirName = "bin" OrElse 
                       lSubDirName = "obj" Then
                        'Console.WriteLine($"    Skipping subdirectory: {lSubDirName}")
                        Continue For
                    End If
                    
                    'Console.WriteLine($"    Processing subdirectory: {lSubDirName}")
                    
                    ' Check if folder already exists
                    Dim lExistingFolder As ProjectNode = vParentNode.Children.FirstOrDefault(
                        Function(n) n.Name = lSubDirName AndAlso Not n.IsFile)
                    
                    Dim lFolderNode As ProjectNode
                    If lExistingFolder IsNot Nothing Then
                        lFolderNode = lExistingFolder
                        'Console.WriteLine($"      Using existing folder node")
                    Else
                        ' Create folder node
                        lFolderNode = New ProjectNode() With {
                            .Name = lSubDirName,
                            .Path = lDir,
                            .NodeType = GetFolderType(lSubDirName),
                            .IsFile = False
                        }
                        vParentNode.AddChild(lFolderNode)
                        'Console.WriteLine($"      Created new folder node (Type={lFolderNode.NodeType})")
                    End If
                    
                    ' Recursively scan subdirectory
                    Dim lNewRelativePath As String = If(String.IsNullOrEmpty(vRelativePath), 
                                                       lSubDirName, 
                                                       System.IO.Path.Combine(vRelativePath, lSubDirName))
                    ScanDirectoryRecursive(lDir, lFolderNode, lNewRelativePath)
                Next
                
                ' Get all files in current directory
                Dim lFiles As String() = Directory.GetFiles(vDirectory)
                'Console.WriteLine($"  Found {lFiles.Length} files")
                
                For Each lFile In lFiles
                    Dim lFileName As String = System.IO.Path.GetFileName(lFile)
                    
                    ' Skip hidden files
                    If lFileName.StartsWith(".") Then
                        'Console.WriteLine($"    Skipping hidden file: {lFileName}")
                        Continue For
                    End If
                    
                    ' Skip certain file types
                    Dim lExt As String = System.IO.Path.GetExtension(lFileName).ToLower()
                    If lExt = ".dll" OrElse lExt = ".exe" OrElse lExt = ".pdb" Then
                        Console.WriteLine($"    Skipping binary file: {lFileName}")
                        Continue For
                    End If
                    
                    ' Check if file already exists
                    Dim lExistingFile As ProjectNode = vParentNode.Children.FirstOrDefault(
                        Function(n) n.Name = lFileName AndAlso n.IsFile)
                    
                    If lExistingFile Is Nothing Then
                        ' Add file node
                        Dim lFileNode As New ProjectNode() With {
                            .Name = lFileName,
                            .Path = lFile,
                            .NodeType = GetFileType(lFileName),
                            .IsFile = True
                        }
                        
                        ' Set tooltip
                        Select Case lFileNode.NodeType
                            Case ProjectNodeType.eVBFile
                                lFileNode.ToolTip = $"VB.NET Source File: {lFileName}"
                            Case ProjectNodeType.eXMLFile
                                lFileNode.ToolTip = $"XML File: {lFileName}"
                            Case ProjectNodeType.eResourceFile
                                lFileNode.ToolTip = $"Resource File: {lFileName}"
                            Case ProjectNodeType.eConfigFile
                                lFileNode.ToolTip = $"Configuration File: {lFileName}"
                            Case Else
                                lFileNode.ToolTip = lFileName
                        End Select
                        
                        vParentNode.AddChild(lFileNode)
                        Console.WriteLine($"    Added file: {lFileName} (Type={lFileNode.NodeType})")
                    Else
                        Console.WriteLine($"    File already exists: {lFileName}")
                    End If
                Next
                
                Console.WriteLine($"  Completed scanning: {vDirectory} - Added {vParentNode.Children.Count} children to {vParentNode.Name}")
                
            Catch ex As Exception
                Console.WriteLine($"ScanDirectoryRecursive error: {ex.Message}")
                Console.WriteLine($"  Directory: {vDirectory}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Count total files in tree (helper for debugging)
        ''' </summary>
        Private Function CountFiles(vNode As ProjectNode) As Integer
            If vNode Is Nothing Then Return 0
            
            Dim lCount As Integer = If(vNode.IsFile, 1, 0)
            
            For Each lChild In vNode.Children
                lCount += CountFiles(lChild)
            Next
            
            Return lCount
        End Function
        
    End Class
    
End Namespace
