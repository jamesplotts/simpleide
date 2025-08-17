' ProjectExplorer.Resources.vb - Resource node integration for Project Explorer
Imports Gtk
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Utilities

Namespace Widgets

    Partial Public Class ProjectExplorer
        
        ' Resource node constants
        Private Const RESOURCES_NODE_NAME As String = "Resources"
        
        ' Resource types enumeration
        Public Enum ResourceType
            eUnspecified
            eString
            eImage
            eIcon
            eAudio
            eBinary
            eText
            eLastValue
        End Enum
        
        ' Resource file info
        Public Class ResourceFileInfo
            Public Property Name As String
            Public Property FullPath As String
            Public Property RelativePath As String
            Public Property Type As ResourceType
            Public Property IsResx As Boolean
            Public Property ResourceCount As Integer = 0
        End Class
        
        ' Events
        Public Event ResourceFileSelected(vFilePath As String)
        Public Event ResourceModified()
        
        ' Create Resources node
        Public Sub CreateResourcesNode()
            Try
                If pHasResourcesNode OrElse pTreeStore Is Nothing Then
                    Return
                End If
                
                ' Get root iter
                Dim lRootIter As TreeIter
                If Not pTreeStore.GetIterFirst(lRootIter) Then
                    Return
                End If
                
                ' Find insertion point (after References node if it exists, otherwise after Manifest)
                Dim lInsertPosition As Integer = GetResourcesInsertPosition(lRootIter)
                
                ' Add Resources node
                pResourcesIter = pTreeStore.InsertWithValues(lRootIter, 
                    lInsertPosition,
                    RESOURCES_NODE_NAME,
                    "",
                    False,
                    "resources")
                
                pHasResourcesNode = True
                
                ' Load existing resources
                LoadProjectResources()
                
            Catch ex As Exception
                Console.WriteLine($"error creating Resources Node: {ex.Message}")
            End Try
        End Sub
        
        ' Get insertion position for Resources node
        Private Function GetResourcesInsertPosition(vRootIter As TreeIter) As Integer
            Try
                Dim lPosition As Integer = 0
                Dim lChildIter As TreeIter
                
                If pTreeStore.IterChildren(lChildIter, vRootIter) Then
                    Do
                        Dim lNodeType As String = CType(pTreeStore.GetValue(lChildIter, 3), String)
                        
                        ' Insert after References or Manifest, whichever comes last
                        If lNodeType = "References" OrElse lNodeType = "manifest" Then
                            lPosition = pTreeStore.IterNChildren(vRootIter)
                        ElseIf lNodeType = "Project" OrElse lNodeType = "file" Then
                            ' Insert before project files
                            Exit Do
                        End If
                        
                        lPosition += 1
                    Loop While pTreeStore.IterNext(lChildIter)
                End If
                
                Return lPosition
                
            Catch ex As Exception
                Console.WriteLine($"error getting resources insert position: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ' Load project resources
        Private Sub LoadProjectResources()
            Try
                If Not pHasResourcesNode OrElse String.IsNullOrEmpty(pProjectFile) Then
                    Return
                End If
                
                ' Clear existing resource nodes
                Dim lChildIter As TreeIter
                If pTreeStore.IterChildren(lChildIter, pResourcesIter) Then
                    While pTreeStore.Remove(lChildIter)
                        ' Keep removing first child until none left
                    End While
                End If
                
                ' Get all resource files from project
                Dim lResourceFiles As List(Of ResourceFileInfo) = GetProjectResourceFiles()
                
                ' Sort resources by name
                lResourceFiles = lResourceFiles.OrderBy(Function(r) r.Name).ToList()
                
                ' Add resource files to tree
                For Each lResource In lResourceFiles
                    AddResourceToTree(lResource)
                Next
                
                ' Collapse Resources node by default (even if it has children)
                If lResourceFiles.Count > 0 Then
                    Dim lPath As TreePath = pTreeStore.GetPath(pResourcesIter)
                    pTreeView.CollapseRow(lPath)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error loading project resources: {ex.Message}")
            End Try
        End Sub
        
        ' Get all resource files from project
        Private Function GetProjectResourceFiles() As List(Of ResourceFileInfo)
            Dim lResources As New List(Of ResourceFileInfo)
            
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Create namespace manager
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                ' Get all EmbeddedResource items
                Dim lResourceNodes As XmlNodeList = lDoc.SelectNodes("//ms:EmbeddedResource", lNamespaceManager)
                If lResourceNodes Is Nothing OrElse lResourceNodes.Count = 0 Then
                    ' Try without namespace
                    lResourceNodes = lDoc.SelectNodes("//EmbeddedResource")
                End If
                
                For Each lNode As XmlNode In lResourceNodes
                    Dim lInclude As String = lNode.Attributes("Include")?.Value
                    If Not String.IsNullOrEmpty(lInclude) Then
                        Dim lFullPath As String = System.IO.Path.Combine(pProjectDirectory, lInclude)
                        If File.Exists(lFullPath) Then
                            Dim lResourceInfo As New ResourceFileInfo With {
                                .Name = System.IO.Path.GetFileName(lFullPath),
                                .FullPath = lFullPath,
                                .RelativePath = lInclude,
                                .IsResx = lFullPath.ToLower().EndsWith(".resx"),
                                .Type = DetermineResourceType(lFullPath)
                            }
                            
                            ' Count resources in .resx files
                            If lResourceInfo.IsResx Then
                                lResourceInfo.ResourceCount = CountResxResources(lFullPath)
                            End If
                            
                            lResources.Add(lResourceInfo)
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error getting project resource files: {ex.Message}")
            End Try
            
            Return lResources
        End Function
        
        ' Determine resource type from file extension
        Private Function DetermineResourceType(vFilePath As String) As ResourceType
            Dim lExtension As String = System.IO.Path.GetExtension(vFilePath).ToLower()
            
            Select Case lExtension
                Case ".resx"
                    Return ResourceType.eString  ' .resx typically contains strings
                Case ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico"
                    Return ResourceType.eImage
                Case ".ico", ".cur"
                    Return ResourceType.eIcon
                Case ".wav", ".mp3", ".ogg"
                    Return ResourceType.eAudio
                Case ".txt", ".xml", ".json"
                    Return ResourceType.eText
                Case Else
                    Return ResourceType.eBinary
            End Select
        End Function
        
        ' Count resources in a .resx file
        Private Function CountResxResources(vFilePath As String) As Integer
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(vFilePath)
                
                ' Count data elements (excluding resheader elements)
                Dim lDataNodes As XmlNodeList = lDoc.SelectNodes("//Data")
                Return If(lDataNodes?.Count, 0)
                
            Catch ex As Exception
                Console.WriteLine($"error counting resx resources: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ' Add resource to tree
        Private Sub AddResourceToTree(vResource As ResourceFileInfo)
            Try
                Dim lDisplayName As String = vResource.Name
                
                ' Add count for .resx files
                If vResource.IsResx AndAlso vResource.ResourceCount > 0 Then
                    lDisplayName &= $" ({vResource.ResourceCount} items)"
                End If
                
                ' Determine icon based on resource type
                Dim lNodeType As String = GetResourceNodeType(vResource.Type)
                
                pTreeStore.AppendValues(pResourcesIter,
                    lDisplayName,              ' Column 0: String (Name)
                    vResource.FullPath,        ' Column 1: String (full Path)
                    True,                      ' Column 2: Boolean (is file)
                    lNodeType                  ' Column 3: String (Node Type)
                )
                
            Catch ex As Exception
                Console.WriteLine($"error adding resource to tree: {ex.Message}")
            End Try
        End Sub
        
        ' Get node type for resource type (for icon display)
        Private Function GetResourceNodeType(vType As ResourceType) As String
            Select Case vType
                Case ResourceType.eString
                    Return "resource-string"
                Case ResourceType.eImage
                    Return "resource-image"
                Case ResourceType.eIcon
                    Return "resource-icon"
                Case ResourceType.eAudio
                    Return "resource-audio"
                Case ResourceType.eText
                    Return "resource-text"
                Case Else
                    Return "resource-binary"
            End Select
        End Function
        
        ' Check if current selection is the Resources node
        Private Function IsResourcesNode() As Boolean
            Try
                Return pCurrentSelectedPath = RESOURCES_NODE_NAME
            Catch ex As Exception
                Console.WriteLine($"error checking resources Node: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Check if current selection is a resource file
        Private Function IsResourceFile() As Boolean
            Try
                If String.IsNullOrEmpty(pCurrentSelectedPath) OrElse Not pCurrentIsFile Then
                    Return False
                End If
                
                ' Check if parent is Resources node
                Dim lSelectedIter As TreeIter
                If pCurrentViewModel.GetIterFromString(lSelectedIter, pCurrentSelectedPath) Then
                    Dim lParentIter As TreeIter
                    If pCurrentViewModel.IterParent(lParentIter, lSelectedIter) Then
                        Dim lParentName As String = CType(pCurrentViewModel.GetValue(lParentIter, 0), String)
                        Return lParentName = RESOURCES_NODE_NAME
                    End If
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"error checking resource file: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Update resources context menu
        Private Sub UpdateResourcesContextMenu()
            Try
                Dim lIsResourcesNode As Boolean = IsResourcesNode()
                Dim lIsResourceFile As Boolean = IsResourceFile()
                
                ' Show/hide resource items
                For Each lChild As Widget In pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lChild, MenuItem)
                        
                        Select Case lMenuItem.Name
                            Case "ResourcesMenu"
                                lMenuItem.Visible = lIsResourcesNode
                            Case "OpenResourceItem"
                                lMenuItem.Visible = lIsResourceFile
                            Case "RemoveResourceItem"
                                lMenuItem.Visible = lIsResourceFile
                        End Select
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error updating resources Context menu: {ex.Message}")
            End Try
        End Sub
        
        ' Add resources context menu items
        Private Sub AddResourcesContextMenuItems()
            Try
                ' Resources menu (for Resources node)
                Dim lResourcesMenu As New Menu()
                Dim lResourcesMenuItem As New MenuItem("Add Resource")
                lResourcesMenuItem.Submenu = lResourcesMenu
                lResourcesMenuItem.Name = "ResourcesMenu"
                lResourcesMenuItem.NoShowAll = True
                
                ' Add String Resource
                Dim lAddStringResourceItem As New MenuItem("String Resource...")
                AddHandler lAddStringResourceItem.Activated, AddressOf OnAddStringResource
                lResourcesMenu.Append(lAddStringResourceItem)
                
                ' Add Image Resource
                Dim lAddImageResourceItem As New MenuItem("Image Resource...")
                AddHandler lAddImageResourceItem.Activated, AddressOf OnAddImageResource
                lResourcesMenu.Append(lAddImageResourceItem)
                
                ' Add Icon Resource
                Dim lAddIconResourceItem As New MenuItem("IcOn Resource...")
                AddHandler lAddIconResourceItem.Activated, AddressOf OnAddIconResource
                lResourcesMenu.Append(lAddIconResourceItem)
                
                ' Add Existing Resource File
                Dim lAddExistingResourceItem As New MenuItem("Existing Resource File...")
                AddHandler lAddExistingResourceItem.Activated, AddressOf OnAddExistingResourceFile
                lResourcesMenu.Append(lAddExistingResourceItem)
                
                lResourcesMenu.ShowAll()
                pContextMenu.Append(lResourcesMenuItem)
                
                ' Open Resource Item (for resource files)
                Dim lOpenResourceItem As New MenuItem("Open")
                lOpenResourceItem.Name = "OpenResourceItem"
                lOpenResourceItem.NoShowAll = True
                AddHandler lOpenResourceItem.Activated, AddressOf OnOpenResourceFile
                pContextMenu.Append(lOpenResourceItem)
                
                ' Remove Resource Item (for resource files)
                Dim lRemoveResourceItem As New MenuItem("Remove from project")
                lRemoveResourceItem.Name = "RemoveResourceItem"
                lRemoveResourceItem.NoShowAll = True
                AddHandler lRemoveResourceItem.Activated, AddressOf OnRemoveResourceFile
                pContextMenu.Append(lRemoveResourceItem)
                
            Catch ex As Exception
                Console.WriteLine($"error adding resources Context menu items: {ex.Message}")
            End Try
        End Sub
        
        ' Handle adding existing resource file
        Private Sub OnAddExistingResourceFile(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select Resource File",
                    Me.Toplevel,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Add", ResponseType.Accept
                )
                
                ' Add filters for common resource types
                Dim lImageFilter As New FileFilter()
                lImageFilter.Name = "Image Files"
                lImageFilter.AddPattern("*.png")
                lImageFilter.AddPattern("*.jpg")
                lImageFilter.AddPattern("*.jpeg")
                lImageFilter.AddPattern("*.gif")
                lImageFilter.AddPattern("*.bmp")
                lImageFilter.AddPattern("*.ico")
                lDialog.AddFilter(lImageFilter)
                
                Dim lAudioFilter As New FileFilter()
                lAudioFilter.Name = "Audio Files"
                lAudioFilter.AddPattern("*.wav")
                lAudioFilter.AddPattern("*.mp3")
                lAudioFilter.AddPattern("*.ogg")
                lDialog.AddFilter(lAudioFilter)
                
                Dim lTextFilter As New FileFilter()
                lTextFilter.Name = "Text Files"
                lTextFilter.AddPattern("*.txt")
                lTextFilter.AddPattern("*.xml")
                lTextFilter.AddPattern("*.json")
                lDialog.AddFilter(lTextFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lSelectedFile As String = lDialog.FileName
                    
                    ' Copy to Resources folder
                    Dim lResourcesDir As String = System.IO.Path.Combine(pProjectDirectory, "Resources")
                    If Not Directory.Exists(lResourcesDir) Then
                        Directory.CreateDirectory(lResourcesDir)
                    End If
                    
                    Dim lTargetFile As String = System.IO.Path.Combine(lResourcesDir, System.IO.Path.GetFileName(lSelectedFile))
                    
                    ' Check if file already exists
                    If File.Exists(lTargetFile) Then
                        Dim lConfirmDialog As New MessageDialog(Me.Toplevel, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo,
                            $"File '{System.IO.Path.GetFileName(lTargetFile)}' already exists in Resources folder. Overwrite?")
                        
                        If lConfirmDialog.Run() <> CInt(ResponseType.Yes) Then
                            lConfirmDialog.Destroy()
                            lDialog.Destroy()
                            Return
                        End If
                        lConfirmDialog.Destroy()
                    End If
                    
                    ' Copy file
                    File.Copy(lSelectedFile, lTargetFile, True)
                    
                    ' Add to project as embedded resource
                    AddEmbeddedResourceToProject(lTargetFile)
                    
                    ' Reload resources
                    LoadProjectResources()
                    
                    ' Raise event for project modification
                    RaiseEvent ResourceModified()
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error adding existing resource file: {ex.Message}")
                ShowError($"Failed to add resource file: {ex.Message}")
            End Try
        End Sub
        
        ' Handle opening resource file
        Private Sub OnOpenResourceFile(vSender As Object, vE As EventArgs)
            Try
                If Not String.IsNullOrEmpty(pCurrentSelectedPath) AndAlso pCurrentIsFile Then
                    RaiseEvent ResourceFileSelected(pCurrentSelectedPath)
                End If
            Catch ex As Exception
                Console.WriteLine($"error opening resource file: {ex.Message}")
            End Try
        End Sub
        
        ' Handle removing resource file
        Private Sub OnRemoveResourceFile(vSender As Object, vE As EventArgs)
            Try
                If String.IsNullOrEmpty(pCurrentSelectedPath) OrElse Not pCurrentIsFile Then
                    Return
                End If
                
                Dim lConfirmDialog As New MessageDialog(Me.Toplevel, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo,
                    $"Remove '{System.IO.Path.GetFileName(pCurrentSelectedPath)}' from project?{Environment.NewLine}{Environment.NewLine}this will not Delete the file from disk.")
                
                If lConfirmDialog.Run() = CInt(ResponseType.Yes) Then
                    ' Remove from project file
                    RemoveEmbeddedResourceFromProject(pCurrentSelectedPath)
                    
                    ' Reload resources
                    LoadProjectResources()
                    
                    ' Raise event for project modification
                    RaiseEvent ResourceModified()
                End If
                
                lConfirmDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error removing resource file: {ex.Message}")
                ShowError($"Failed to remove resource file: {ex.Message}")
            End Try
        End Sub
        
        ' Remove embedded resource from project
        Private Sub RemoveEmbeddedResourceFromProject(vFilePath As String)
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Make path relative to project
                Dim lRelativePath As String = GetRelativePath(pProjectDirectory, vFilePath)
                
                ' Create namespace manager
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                ' Find the resource node
                Dim lResourceNode As XmlNode = lDoc.SelectSingleNode($"//ms:EmbeddedResource[@Include='{lRelativePath}']", lNamespaceManager)
                If lResourceNode Is Nothing Then
                    lResourceNode = lDoc.SelectSingleNode($"//EmbeddedResource[@Include='{lRelativePath}']")
                End If
                
                If lResourceNode IsNot Nothing Then
                    ' Remove the node
                    lResourceNode.ParentNode.RemoveChild(lResourceNode)
                    
                    ' Save project file
                    lDoc.Save(pProjectFile)
                    
                    ' Raise project modified event
                    RaiseEvent ProjectModified()
                End If
                
            Catch ex As Exception
                Throw New Exception($"Failed to remove embedded resource from project: {ex.Message}")
            End Try
        End Sub
        
        ' Refresh Resources node (call after resource changes)
        Public Sub RefreshResourcesNode()
            Try
                If pHasResourcesNode Then
                    LoadProjectResources()
                End If
            Catch ex As Exception
                Console.WriteLine($"error refreshing resources Node: {ex.Message}")
            End Try
        End Sub
        
        ' Handle resource node activation
        Private Function HandleResourceActivation(vPath As String) As Boolean
            Try
                If IsResourcesNode() Then
                    ' Resources node clicked - could expand/collapse or show properties
                    Return True
                ElseIf IsResourceFile() Then
                    ' Resource file clicked - open it
                    RaiseEvent ResourceFileSelected(vPath)
                    Return True
                End If
                Return False
            Catch ex As Exception
                Console.WriteLine($"error handling resource activation: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Class

End Namespace