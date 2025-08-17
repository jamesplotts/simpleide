' ProjectExplorer.vb - Enhanced project explorer with toolbar and view toggle
Imports Gtk
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic
Imports System.Collections.Concurrent
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Widgets

    Public Partial Class ProjectExplorer
        Inherits Box

        ' Enums
        Public Enum ViewMode
            eUnspecified
            eTreeView
            eListView
            eLastValue
        End Enum

        ' Private fields - UI
        Private pToolbar As Toolbar
        Private pScrolledWindow As ScrolledWindow
        Private pTreeView As TreeView
        Private pTreeStore As TreeStore
        Private pListStore As ListStore
        Private pCurrentViewModel As ITreeModel
        Private pResourcesIter As TreeIter
        Private pHasResourcesNode As Boolean = False
        Private pCloseButton As ToolButton

        
        ' Private fields - Data
        Private pProjectDirectory As String = ""
        Private pProjectFile As String = ""
        Private pContextMenu As Menu
        Private pCurrentSelectedPath As String = ""
        Private pCurrentSelectedIter As TreeIter
        Private pCurrentIsFile As Boolean = False
        Private pCurrentViewMode As ViewMode = ViewMode.eTreeView
        Private pAllProjectFiles As New List(Of FileInfo)
        Private pSettingsManager As SettingsManager
        
        ' Private fields - Toolbar buttons
        Private pToggleViewButton As ToolButton
        Private pRefreshButton As ToolButton
        Private pCollapseAllButton As ToolButton
        Private pExpandAllButton As ToolButton

        ' Events
        Public Event FileSelected(vFilePath As String)
        Public Event ProjectFileSelected(vFilePath As String)
        Public Event ProjectModified()
        Public Event CloseRequested()


        ' Properties
        Public ReadOnly Property ProjectFile As String
            Get
                Return pProjectFile
            End Get
        End Property

        Public Sub New(vSettingsManager As SettingsManager)
            MyBase.New(Orientation.Vertical, 0)
            
            pSettingsManager = vSettingsManager
            
            ' Load saved view mode
            LoadViewModeSetting()
            
            ' Create UI
            BuildUI()
            
            ' Setup scrolled window
            pScrolledWindow = New ScrolledWindow()
            pScrolledWindow.SetPolicy(PolicyType.Never, PolicyType.Automatic)
            pScrolledWindow.SetSizeRequest(250, -1)
            
            ' Create data stores
            CreateDataStores()
            
            ' Create tree view
            CreateTreeView()
            
            ' Create context menu
            CreateContextMenu()
            RefreshReferences()

            
            ' Apply current view mode
            ApplyViewMode()
            'pScrolledWindow.ShowAll()
            
            ' Pack components
            PackStart(pToolbar, False, False, 0)
            PackStart(pScrolledWindow, True, True, 0)
            
            'ShowAll()
        End Sub

        Private Sub BuildUI()
            ' Create toolbar
            pToolbar = New Toolbar()
            pToolbar.ToolbarStyle = ToolbarStyle.Icons
            pToolbar.IconSize = IconSize.SmallToolbar
            
            ' Toggle view button
            pToggleViewButton = New ToolButton(Nothing, "Toggle View")
            pToggleViewButton.IconWidget = Image.NewFromIconName("view-list-symbolic", IconSize.SmallToolbar)
            pToggleViewButton.TooltipText = "Toggle between Tree and List view"
            AddHandler pToggleViewButton.Clicked, AddressOf OnToggleView
            pToolbar.Insert(pToggleViewButton, -1)
            
            ' Separator
            pToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Expand all button
            pExpandAllButton = New ToolButton(Nothing, "Expand All")
            pExpandAllButton.IconWidget = Image.NewFromIconName("list-add-symbolic", IconSize.SmallToolbar)
            pExpandAllButton.TooltipText = "Expand all folders"
            AddHandler pExpandAllButton.Clicked, AddressOf OnExpandAll
            pToolbar.Insert(pExpandAllButton, -1)
            
            ' Collapse all button
            pCollapseAllButton = New ToolButton(Nothing, "Collapse All")
            pCollapseAllButton.IconWidget = Image.NewFromIconName("list-remove-symbolic", IconSize.SmallToolbar)
            pCollapseAllButton.TooltipText = "Collapse all folders"
            AddHandler pCollapseAllButton.Clicked, AddressOf OnCollapseAll
            pToolbar.Insert(pCollapseAllButton, -1)
            
            ' Separator
            pToolbar.Insert(New SeparatorToolItem(), -1)
            
            ' Refresh button
            pRefreshButton = New ToolButton(Nothing, "Refresh")
            pRefreshButton.IconWidget = Image.NewFromIconName("view-Refresh-symbolic", IconSize.SmallToolbar)
            pRefreshButton.TooltipText = "Refresh project view"
            AddHandler pRefreshButton.Clicked, AddressOf OnRefresh
            pToolbar.Insert(pRefreshButton, -1)


            ' Add expanding separator to push close button to the right
            Dim lExpandingSeparator As New SeparatorToolItem()
            lExpandingSeparator.Draw = False
            lExpandingSeparator.Expand = True
            pToolbar.Insert(lExpandingSeparator, -1)
            
            ' Close button
            pCloseButton = New ToolButton(Nothing, "Close")
            pCloseButton.IconWidget = Image.NewFromIconName("window-close-symbolic", IconSize.SmallToolbar)
            pCloseButton.TooltipText = "Hide project Explorer"
            AddHandler pCloseButton.Clicked, AddressOf OnClose
            pToolbar.Insert(pCloseButton, -1)
        

        End Sub

        Private Sub CreateDataStores()
            ' Create tree store (name, full path, is file, node type)
            pTreeStore = New TreeStore(GetType(String), GetType(String), GetType(Boolean), GetType(String))
            
            ' Create list store for flat view (name, full path, is file, relative path)
            pListStore = New ListStore(GetType(String), GetType(String), GetType(Boolean), GetType(String))
            
            ' Set default model
            pCurrentViewModel = If(pCurrentViewMode = ViewMode.eTreeView, CType(pTreeStore, ITreeModel), CType(pListStore, ITreeModel))
        End Sub

        Private Sub CreateTreeView()
            ' Create tree view
            pTreeView = New TreeView(pCurrentViewModel)
            pTreeView.HeadersVisible = False
            pTreeView.EnableSearch = True
            pTreeView.SearchColumn = 0
            
            ' Add column with icon and text
            Dim lColumn As New TreeViewColumn()
            lColumn.Title = "Project Files"
            
            ' Icon renderer
            Dim lIconRenderer As New CellRendererPixbuf()
            lColumn.PackStart(lIconRenderer, False)
            lColumn.SetCellDataFunc(lIconRenderer, AddressOf RenderIconCell)
            
            ' Text renderer
            Dim lTextRenderer As New CellRendererText()
            lColumn.PackStart(lTextRenderer, True)
            
            ' Set text based on view mode
            If pCurrentViewMode = ViewMode.eTreeView Then
                lColumn.AddAttribute(lTextRenderer, "text", 0)
            Else
                ' For list view, show relative path
                lColumn.SetCellDataFunc(lTextRenderer, AddressOf RenderListTextCell)
            End If
            
            pTreeView.AppendColumn(lColumn)
            
            ' Handle activation
            AddHandler pTreeView.RowActivated, AddressOf OnRowActivated
            
            ' Handle right-click for context menu
            AddHandler pTreeView.ButtonReleaseEvent, AddressOf OnTreeViewButtonRelease
            
            ' Handle selection changes
            AddHandler pTreeView.Selection.Changed, AddressOf OnSelectionChanged
            
            ' Add to scrolled window
            pScrolledWindow.Add(pTreeView)
        End Sub

        Private Sub RenderListTextCell(vColumn As TreeViewColumn, vCell As CellRenderer, vModel As ITreeModel, vIter As TreeIter)
            Try
                Dim lTextRenderer As CellRendererText = CType(vCell, CellRendererText)
                Dim lRelativePath As String = CType(vModel.GetValue(vIter, 3), String)
                lTextRenderer.Text = lRelativePath
            Catch ex As Exception
                Console.WriteLine($"RenderListTextCell error: {ex.Message}")
            End Try
        End Sub

        Private Sub LoadViewModeSetting()
            Try
                Dim lSetting As String = pSettingsManager.GetSetting("ProjectExplorer.ViewMode", "TreeView")
                Select Case lSetting
                    Case "TreeView"
                        pCurrentViewMode = ViewMode.eTreeView
                    Case "ListView"
                        pCurrentViewMode = ViewMode.eListView
                    Case Else
                        pCurrentViewMode = ViewMode.eTreeView
                End Select
            Catch ex As Exception
                Console.WriteLine($"LoadViewModeSetting error: {ex.Message}")
                pCurrentViewMode = ViewMode.eTreeView
            End Try
        End Sub

        Private Sub SaveViewModeSetting()
            Try
                Dim lValue As String = If(pCurrentViewMode = ViewMode.eTreeView, "TreeView", "ListView")
                pSettingsManager.SetSetting("ProjectExplorer.ViewMode", lValue)
                pSettingsManager.SaveSettings()
            Catch ex As Exception
                Console.WriteLine($"SaveViewModeSetting error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnToggleView(vSender As Object, vE As EventArgs)
            Try
                ' Toggle view mode
                pCurrentViewMode = If(pCurrentViewMode = ViewMode.eTreeView, ViewMode.eListView, ViewMode.eTreeView)
                
                ' Save preference
                SaveViewModeSetting()
                
                ' Apply new view mode
                ApplyViewMode()
                
                ' Reload project to refresh view
                If Not String.IsNullOrEmpty(pProjectFile) Then
                    LoadProject(pProjectFile)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnToggleView error: {ex.Message}")
            End Try
        End Sub

        Private Sub ApplyViewMode()
            Try
                ' Update button icon based on current mode
                If pCurrentViewMode = ViewMode.eTreeView Then
                    ' Remove old icon widget if exists
                    If pToggleViewButton.IconWidget IsNot Nothing Then
                        pToggleViewButton.Remove(pToggleViewButton.IconWidget)
                    End If
                    ' Create new icon
                    Dim lIcon As Widget = Image.NewFromIconName("view-list-symbolic", IconSize.SmallToolbar)
                    pToggleViewButton.IconWidget = lIcon
                    lIcon.Show()
                    pToggleViewButton.TooltipText = "Switch to List view"
                    
                    ' Enable expand/collapse buttons
                    pExpandAllButton.Sensitive = True
                    pCollapseAllButton.Sensitive = True
                Else
                    ' Remove old icon widget if exists
                    If pToggleViewButton.IconWidget IsNot Nothing Then
                        pToggleViewButton.Remove(pToggleViewButton.IconWidget)
                    End If
                    ' Create new icon
                    Dim lIcon As Widget = Image.NewFromIconName("view-grid-symbolic", IconSize.SmallToolbar)
                    pToggleViewButton.IconWidget = lIcon
                    lIcon.Show()
                    pToggleViewButton.TooltipText = "Switch to Tree view"
                    
                    ' Disable expand/collapse buttons in list view
                    pExpandAllButton.Sensitive = False
                    pCollapseAllButton.Sensitive = False
                End If
                
                ' Update tree view model
                pCurrentViewModel = If(pCurrentViewMode = ViewMode.eTreeView, CType(pTreeStore, ITreeModel), CType(pListStore, ITreeModel))
                pTreeView.Model = pCurrentViewModel
                
                ' Update column renderer
                Dim lColumn As TreeViewColumn = pTreeView.GetColumn(0)
                If lColumn IsNot Nothing Then
                    ' Clear existing cell data funcs
                    lColumn.Clear()
                    
                    ' Re-add renderers
                    Dim lIconRenderer As New CellRendererPixbuf()
                    lColumn.PackStart(lIconRenderer, False)
                    lColumn.SetCellDataFunc(lIconRenderer, AddressOf RenderIconCell)
                    
                    Dim lTextRenderer As New CellRendererText()
                    lColumn.PackStart(lTextRenderer, True)
                    
                    If pCurrentViewMode = ViewMode.eTreeView Then
                        lColumn.AddAttribute(lTextRenderer, "text", 0)
                    Else
                        lColumn.SetCellDataFunc(lTextRenderer, AddressOf RenderListTextCell)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyViewMode error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnExpandAll(vSender As Object, vE As EventArgs)
            Try
                pTreeView.ExpandAll()
            Catch ex As Exception
                Console.WriteLine($"OnExpandAll error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnCollapseAll(vSender As Object, vE As EventArgs)
            Try
                pTreeView.CollapseAll()
                
                ' Keep project root expanded
                Dim lIter As TreeIter
                If pTreeStore.GetIterFirst(lIter) Then
                    pTreeView.ExpandRow(pTreeStore.GetPath(lIter), False)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnCollapseAll error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnRefresh(vSender As Object, vE As EventArgs)
            Try
                If Not String.IsNullOrEmpty(pProjectFile) Then
                    LoadProject(pProjectFile)
                End If
            Catch ex As Exception
                Console.WriteLine($"OnRefresh error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnSelectionChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    pCurrentSelectedPath = CType(pCurrentViewModel.GetValue(lIter, 1), String)
                    pCurrentIsFile = CType(pCurrentViewModel.GetValue(lIter, 2), Boolean)
                    pCurrentSelectedIter = lIter
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnSelectionChanged error: {ex.Message}")
            End Try
        End Sub

        ' Rest of the existing methods remain the same...
        Private Sub CreateContextMenu()
            pContextMenu = New Menu()
            
            ' Add New File
            Dim lAddFileItem As New MenuItem("Add New VB File...")
            AddHandler lAddFileItem.Activated, AddressOf OnAddNewFile
            pContextMenu.Append(lAddFileItem)
            
            ' Add Existing File
            Dim lAddExistingItem As New MenuItem("Add Existing File...")
            AddHandler lAddExistingItem.Activated, AddressOf OnAddExistingFile
            pContextMenu.Append(lAddExistingItem)
            
            ' Add New Folder
            Dim lAddFolderItem As New MenuItem("Add New Folder...")
            AddHandler lAddFolderItem.Activated, AddressOf OnAddNewFolder
            pContextMenu.Append(lAddFolderItem)
            
            'pContextMenu.Append(New SeparatorMenuItem())
            
            ' Resources submenu (initially hidden, shown only for Resources folder)
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
            
            pContextMenu.Append(lResourcesMenuItem)
            pContextMenu.Append(New SeparatorMenuItem())
            
            ' Remove from Project
            Dim lRemoveItem As New MenuItem("Remove from project")
            AddHandler lRemoveItem.Activated, AddressOf OnRemoveFromProject
            pContextMenu.Append(lRemoveItem)
            
            ' Delete File
            Dim lDeleteItem As New MenuItem("Delete File")
            AddHandler lDeleteItem.Activated, AddressOf OnDeleteFile
            pContextMenu.Append(lDeleteItem)
            
            pContextMenu.Append(New SeparatorMenuItem())
            
            ' Rename
            Dim lRenameItem As New MenuItem("Rename...")
            AddHandler lRenameItem.Activated, AddressOf OnRename
            pContextMenu.Append(lRenameItem)
            
            pContextMenu.Append(New SeparatorMenuItem())
            
            ' Properties
            Dim lPropertiesItem As New MenuItem("Properties")
            AddHandler lPropertiesItem.Activated, AddressOf OnShowProperties
            pContextMenu.Append(lPropertiesItem)

            AddResourcesContextMenuItems()
            AddManifestContextMenuItems()
            
            pContextMenu.ShowAll()
        End Sub

        Private Sub OnTreeViewButtonRelease(vSender As Object, vE As ButtonReleaseEventArgs)
            Try
                If vE.Event.Button = 3 Then ' Right click
                    ' Get the path at the cursor position
                    Dim lPath As TreePath = Nothing
                    Dim lColumn As TreeViewColumn = Nothing
                    Dim lCellX, lCellY As Integer
                    
                    If pTreeView.GetPathAtPos(CInt(vE.Event.x), CInt(vE.Event.y), lPath, lColumn, lCellX, lCellY) Then
                        ' CRITICAL: Grab focus first - this fixes the double-click issue
                        If Not pTreeView.HasFocus Then
                            pTreeView.GrabFocus()
                        End If
                        
                        ' Select the item
                        pTreeView.Selection.SelectPath(lPath)
                        
                        ' Update context menu based on selection
                        CustomizeContextMenu()
                        
                        ' Show context menu
                        pContextMenu.ShowAll()
                        pContextMenu.PopupAtPointer(vE.Event)
                    End If
                    
                    vE.RetVal = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnTreeViewButtonPress error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CustomizeContextMenu()
            Try
                ' Find the Resources menu item
                Dim lResourcesMenuItem As MenuItem = Nothing
                For Each lChild As Widget In pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lChild, MenuItem)
                        If lMenuItem.Name = "ResourcesMenu" Then
                            lResourcesMenuItem = lMenuItem
                            Exit For
                        End If
                    End If
                Next
                
                ' Check if current selection is the Resources folder
                If Not String.IsNullOrEmpty(pCurrentSelectedPath) AndAlso 
                   Not pCurrentIsFile AndAlso
                   (pCurrentSelectedPath.EndsWith("Resources") OrElse 
                    pCurrentSelectedPath.EndsWith($"{System.IO.Path.DirectorySeparatorChar}Resources")) Then
                    ' Show Resources menu
                    If lResourcesMenuItem IsNot Nothing Then
                        lResourcesMenuItem.Visible = True
                    End If
                Else
                    ' Hide Resources menu
                    If lResourcesMenuItem IsNot Nothing Then
                        lResourcesMenuItem.Visible = False
                    End If
                End If
                
                ' Update other menu items based on selection
                For Each lChild As Widget In pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lChild, MenuItem)
                        
                        Select Case lMenuItem.Label
                            Case "Add New VB File...", "Add Existing File...", "Add New Folder..."
                                ' Enable for folders, disable for files
                                lMenuItem.Sensitive = Not pCurrentIsFile
                                
                            Case "Remove from project"
                                ' Don't allow removing the project file or special folders
                                If pCurrentIsFile Then
                                    lMenuItem.Sensitive = Not pCurrentSelectedPath.Equals(pProjectFile, StringComparison.OrdinalIgnoreCase)
                                Else
                                    ' Check if it's a special folder
                                    Dim lFolderName As String = System.IO.Path.GetFileName(pCurrentSelectedPath)
                                    lMenuItem.Sensitive = Not (lFolderName = "My project" OrElse 
                                                             lFolderName = "Resources" OrElse 
                                                             lFolderName = "bin" OrElse 
                                                             lFolderName = "obj")
                                End If
                                
                            Case "Delete File"
                                ' Only for files
                                lMenuItem.Sensitive = pCurrentIsFile AndAlso 
                                                    Not pCurrentSelectedPath.Equals(pProjectFile, StringComparison.OrdinalIgnoreCase)
                                
                            Case "Rename..."
                                ' Enable for files and non-special folders
                                If pCurrentIsFile Then
                                    lMenuItem.Sensitive = Not pCurrentSelectedPath.Equals(pProjectFile, StringComparison.OrdinalIgnoreCase)
                                Else
                                    Dim lFolderName As String = System.IO.Path.GetFileName(pCurrentSelectedPath)
                                    lMenuItem.Sensitive = Not (lFolderName = "My project" OrElse 
                                                             lFolderName = "Resources" OrElse 
                                                             lFolderName = "bin" OrElse 
                                                             lFolderName = "obj" OrElse
                                                             pCurrentSelectedPath = pProjectDirectory)
                                End If
                        End Select
                    End If
                Next

                UpdateReferenceContextMenu()
                UpdateManifestContextMenu()
                UpdateResourcesContextMenu()

            Catch ex As Exception
                Console.WriteLine($"CustomizeContextMenu error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnRowActivated(vSender As Object, vE As RowActivatedArgs)
            Try
                Dim lIter As TreeIter
                If pCurrentViewModel.GetIter(lIter, vE.Path) Then
                    Dim lIsFile As Boolean = CType(pCurrentViewModel.GetValue(lIter, 2), Boolean)
                    Dim lPath As String = CType(pCurrentViewModel.GetValue(lIter, 1), String)
                    If HandleManifestActivation(lPath) Then
                        Return
                    End If
                    If HandleResourceActivation(lPath) Then  
                        Return
                    End If
                    If lIsFile Then
                        If lPath.ToLower().EndsWith(".vbproj") Then
                            RaiseEvent ProjectFileSelected(lPath)
                        Else
                            RaiseEvent FileSelected(lPath)
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnRowActivated error: {ex.Message}")
            End Try
        End Sub

        Public Sub LoadProject(vProjectFile As String)
            Try
                Console.WriteLine($"LoadProject: Loading {vProjectFile}")
                
                pProjectFile = vProjectFile
                pProjectDirectory = System.IO.Path.GetDirectoryName(vProjectFile)
                
                ' Clear stores
                pTreeStore.Clear()
                pListStore.Clear()
                pAllProjectFiles.Clear()

                ' Reset node flags
                pHasReferencesNode = False
                pHasManifestNode = False
                pHasResourcesNode = False  ' ADD this Line

                If pCurrentViewMode = ViewMode.eTreeView Then
                    LoadTreeView()
                Else
                    LoadListView()
                End If

                CreateReferencesNode()
                CreateManifestNode()
                CreateResourcesNode()

            Catch ex As Exception
                Console.WriteLine($"LoadProject error: {ex.Message}")
                ShowError($"Failed to load project: {ex.Message}")
            End Try
        End Sub

        Private Sub LoadTreeView()
            Try
                ' Add project root node
                Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pProjectFile)
                Dim lRootIter As TreeIter = pTreeStore.AppendValues(lProjectName, pProjectDirectory, False, "Project")
                
                ' Load project structure from .vbproj file
                ' FIX: Don't call LoadProject here - it causes infinite recursion!
                ' Instead, load the project files directly
                LoadProjectFiles(lRootIter)
                
                ' Expand root
                pTreeView.ExpandRow(pTreeStore.GetPath(lRootIter), False)
                
            Catch ex As Exception
                Console.WriteLine($"LoadTreeView error: {ex.Message}")
            End Try
        End Sub

        Private Sub LoadProjectFiles(vRootIter As TreeIter)
            Try
                ' Load project file
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Get all compile items
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lCompileNodes As XmlNodeList = lDoc.SelectNodes("//ms:Compile", lNamespaceManager)
                If lCompileNodes Is Nothing OrElse lCompileNodes.Count = 0 Then
                    ' Try without namespace
                    lCompileNodes = lDoc.SelectNodes("//Compile")
                End If
                
                ' Create folder structure
                Dim lFolders As New Dictionary(Of String, TreeIter)()
                
                For Each lNode As XmlNode In lCompileNodes
                    Dim lInclude As String = lNode.Attributes("Include")?.Value
                    If Not String.IsNullOrEmpty(lInclude) Then
                        ' Get full path
                        Dim lFullPath As String = System.IO.Path.Combine(pProjectDirectory, lInclude)
                        
                        ' Skip if file doesn't exist
                        If Not File.Exists(lFullPath) Then Continue For
                        
                        ' Get directory path parts
                        Dim lDir As String = System.IO.Path.GetDirectoryName(lInclude)
                        Dim lFileName As String = System.IO.Path.GetFileName(lInclude)
                        
                        ' Get or create parent folder
                        Dim lParentIter As TreeIter = vRootIter
                        If Not String.IsNullOrEmpty(lDir) Then
                            lParentIter = GetOrCreateFolder(lDir, vRootIter, lFolders)
                        End If
                        
                        ' Add file to tree
                        pTreeStore.AppendValues(lParentIter, lFileName, lFullPath, True, "File")
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"LoadProjectFiles error: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetOrCreateFolder(vPath As String, vRootIter As TreeIter, vFolders As Dictionary(Of String, TreeIter)) As TreeIter
            Try
                ' Check if folder already exists
                If vFolders.ContainsKey(vPath) Then
                    Return vFolders(vPath)
                End If
                
                ' Split path into parts
                Dim lParts As String() = vPath.Split(New Char() {System.IO.Path.DirectorySeparatorChar, "/"c}, StringSplitOptions.RemoveEmptyEntries)
                
                ' Build folder hierarchy
                Dim lParentIter As TreeIter = vRootIter
                Dim lCurrentPath As String = ""
                
                For Each lPart In lParts
                    If Not String.IsNullOrEmpty(lCurrentPath) Then
                        lCurrentPath &= System.IO.Path.DirectorySeparatorChar
                    End If
                    lCurrentPath &= lPart
                    
                    If vFolders.ContainsKey(lCurrentPath) Then
                        lParentIter = vFolders(lCurrentPath)
                    Else
                        ' Create folder node
                        Dim lFolderPath As String = System.IO.Path.Combine(pProjectDirectory, lCurrentPath)
                        lParentIter = pTreeStore.AppendValues(lParentIter, lPart, lFolderPath, False, "Folder")
                        vFolders(lCurrentPath) = lParentIter
                    End If
                Next
                
                Return lParentIter
                
            Catch ex As Exception
                Console.WriteLine($"GetOrCreateFolder error: {ex.Message}")
                Return vRootIter
            End Try
        End Function

        Private Sub LoadListView()
            Try
                ' First, collect all files from project
                CollectProjectFiles()
                
                ' Sort files by relative path
                pAllProjectFiles.Sort(Function(a, b) String.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase))
                
                ' Add to list store
                For Each lFile In pAllProjectFiles
                    pListStore.AppendValues(lFile.FileName, lFile.FullPath, lFile.IsFile, lFile.RelativePath)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"LoadListView error: {ex.Message}")
            End Try
        End Sub

        Private Sub CollectProjectFiles()
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Add project file itself
                pAllProjectFiles.Add(New FileInfo With {
                    .FileName = System.IO.Path.GetFileName(pProjectFile),
                    .FullPath = pProjectFile,
                    .RelativePath = System.IO.Path.GetFileName(pProjectFile),
                    .IsFile = True
                })
                
                ' Get all compile items
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lCompileNodes As XmlNodeList = lDoc.SelectNodes("//ms:Compile", lNamespaceManager)
                If lCompileNodes Is Nothing OrElse lCompileNodes.Count = 0 Then
                    ' Try without namespace
                    lCompileNodes = lDoc.SelectNodes("//Compile")
                End If
                
                For Each lNode As XmlNode In lCompileNodes
                    Dim lInclude As String = lNode.Attributes("Include")?.Value
                    If Not String.IsNullOrEmpty(lInclude) Then
                        Dim lFullPath As String = System.IO.Path.Combine(pProjectDirectory, lInclude)
                        If File.Exists(lFullPath) Then
                            pAllProjectFiles.Add(New FileInfo With {
                                .FileName = System.IO.Path.GetFileName(lFullPath),
                                .FullPath = lFullPath,
                                .RelativePath = lInclude,
                                .IsFile = True
                            })
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CollectProjectFiles error: {ex.Message}")
            End Try
        End Sub

'        Public Sub LoadProjectStructure(vRootIter As TreeIter)
'            Try
'                Dim lDoc As New XmlDocument()
'                lDoc.Load(pProjectFile)
'                
'                ' Add project file itself
'                AddFileToTree(vRootIter, pProjectFile, System.IO.Path.GetFileName(pProjectFile))
'                
'                ' Dictionary to track folders
'                Dim lFolders As New Dictionary(Of String, TreeIter)
'                
'                ' Lists to collect files and folders at root level
'                Dim lRootFiles As New List(Of Tuple(Of String, String)) ' (FullPath, RelativePath)
'                Dim lFolderFiles As New Dictionary(Of String, List(Of Tuple(Of String, String)))
'                
'                ' Create namespace manager
'                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
'                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
'                
'                ' Process Compile items (source code files)
'                ProcessCompileItems(lDoc, lNamespaceManager, lRootFiles, lFolderFiles)
'                
'                ' Process EmbeddedResource items (handled by Resources node - skip duplicates)
'                ' Note: EmbeddedResource items are now managed by the Resources node,
'                ' so we don't add them to the regular file tree to avoid duplication
'                
'                ' Process Content items (content files that should appear in tree)
'                ProcessContentItems(lDoc, lNamespaceManager, lRootFiles, lFolderFiles)
'                
'                ' Process None items (other files like configs, docs)
'                ProcessNoneItems(lDoc, lNamespaceManager, lRootFiles, lFolderFiles)
'                
'                ' Sort root files alphabetically
'                lRootFiles.Sort(Function(a, b) String.Compare(System.IO.Path.GetFileName(a.Item1), 
'                                                             System.IO.Path.GetFileName(b.Item1), 
'                                                             StringComparison.OrdinalIgnoreCase))
'                
'                ' Add root files first
'                For Each lFile In lRootFiles
'                    AddFileToTree(vRootIter, lFile.Item1, lFile.Item2)
'                Next
'                
'                ' Get sorted list of folders
'                Dim lSortedFolders = lFolderFiles.Keys.OrderBy(Function(x) x, StringComparer.OrdinalIgnoreCase).ToList()
'                
'                ' Add folders and their files
'                For Each lFolderPath In lSortedFolders
'                    ' Create folder hierarchy
'                    Dim lParentIter As TreeIter = vRootIter
'                    Dim lParts() As String = lFolderPath.Split("\"c, "/"c)
'                    Dim lCurrentPath As String = ""
'                    
'                    For Each lPart In lParts
'                        If Not String.IsNullOrEmpty(lPart) Then
'                            If lCurrentPath.Length > 0 Then
'                                lCurrentPath &= "/"
'                            End If
'                            lCurrentPath &= lPart
'                            
'                            If Not lFolders.ContainsKey(lCurrentPath) Then
'                                Dim lFullFolderPath As String = System.IO.Path.Combine(pProjectDirectory, lCurrentPath)
'                                Dim lFolderIter As TreeIter = pTreeStore.AppendValues(lParentIter, lPart, lFullFolderPath, False, "folder")
'                                lFolders(lCurrentPath) = lFolderIter
'                                lParentIter = lFolderIter
'                            Else
'                                lParentIter = lFolders(lCurrentPath)
'                            End If
'                        End If
'                    Next
'                    
'                    ' Sort files in this folder alphabetically
'                    Dim lFiles = lFolderFiles(lFolderPath)
'                    lFiles.Sort(Function(a, b) String.Compare(System.IO.Path.GetFileName(a.Item1), 
'                                                             System.IO.Path.GetFileName(b.Item1), 
'                                                             StringComparison.OrdinalIgnoreCase))
'                    
'                    ' Add files to folder
'                    For Each lFile In lFiles
'                        AddFileToTree(lParentIter, lFile.Item1, lFile.Item2)
'                    Next
'                Next
'                
'                ' Scan for other directories/files not in project (optional)
'                ScanDirectory(vRootIter, pProjectDirectory)
'                
'            Catch ex As Exception
'                Console.WriteLine($"LoadProjectStructure error: {ex.Message}")
'            End Try
'        End Sub

        ' Process Compile items (VB.NET source files)
        Private Sub ProcessCompileItems(vDoc As XmlDocument, vNsMgr As XmlNamespaceManager, 
                                       vRootFiles As List(Of Tuple(Of String, String)), 
                                       vFolderFiles As Dictionary(Of String, List(Of Tuple(Of String, String))))
            Try
                Dim lCompileNodes As XmlNodeList = vDoc.SelectNodes("//ms:Compile", vNsMgr)
                If lCompileNodes Is Nothing OrElse lCompileNodes.Count = 0 Then
                    ' Try without namespace
                    lCompileNodes = vDoc.SelectNodes("//Compile")
                End If
                
                ProcessItemNodes(lCompileNodes, vRootFiles, vFolderFiles)
                
            Catch ex As Exception
                Console.WriteLine($"error processing Compile items: {ex.Message}")
            End Try
        End Sub
        
        ' Process Content items (content files)
        Private Sub ProcessContentItems(vDoc As XmlDocument, vNsMgr As XmlNamespaceManager,
                                       vRootFiles As List(Of Tuple(Of String, String)),
                                       vFolderFiles As Dictionary(Of String, List(Of Tuple(Of String, String))))
            Try
                Dim lContentNodes As XmlNodeList = vDoc.SelectNodes("//ms:Content", vNsMgr)
                If lContentNodes Is Nothing OrElse lContentNodes.Count = 0 Then
                    ' Try without namespace
                    lContentNodes = vDoc.SelectNodes("//Content")
                End If
                
                ProcessItemNodes(lContentNodes, vRootFiles, vFolderFiles)
                
            Catch ex As Exception
                Console.WriteLine($"error processing Content items: {ex.Message}")
            End Try
        End Sub
        
        ' Process None items (other files like configs, docs)
        Private Sub ProcessNoneItems(vDoc As XmlDocument, vNsMgr As XmlNamespaceManager,
                                    vRootFiles As List(Of Tuple(Of String, String)),
                                    vFolderFiles As Dictionary(Of String, List(Of Tuple(Of String, String))))
            Try
                Dim lNoneNodes As XmlNodeList = vDoc.SelectNodes("//ms:None", vNsMgr)
                If lNoneNodes Is Nothing OrElse lNoneNodes.Count = 0 Then
                    ' Try without namespace
                    lNoneNodes = vDoc.SelectNodes("//None")
                End If
                
                ProcessItemNodes(lNoneNodes, vRootFiles, vFolderFiles)
                
            Catch ex As Exception
                Console.WriteLine($"error processing None items: {ex.Message}")
            End Try
        End Sub
        
        ' Generic method to process item nodes and organize them into files/folders
        Private Sub ProcessItemNodes(vNodes As XmlNodeList,
                                     vRootFiles As List(Of Tuple(Of String, String)),
                                     vFolderFiles As Dictionary(Of String, List(Of Tuple(Of String, String))))
            Try
                If vNodes Is Nothing Then Return
                
                For Each lNode As XmlNode In vNodes
                    Dim lInclude As String = lNode.Attributes("Include")?.Value
                    If Not String.IsNullOrEmpty(lInclude) Then
                        Dim lFullPath As String = System.IO.Path.Combine(pProjectDirectory, lInclude)
                        If File.Exists(lFullPath) Then
                            ' Check if file is in a subdirectory
                            Dim lDirectory As String = System.IO.Path.GetDirectoryName(lInclude)
                            If String.IsNullOrEmpty(lDirectory) Then
                                ' Root level file
                                vRootFiles.Add(New Tuple(Of String, String)(lFullPath, lInclude))
                            Else
                                ' File in subdirectory
                                If Not vFolderFiles.ContainsKey(lDirectory) Then
                                    vFolderFiles(lDirectory) = New List(Of Tuple(Of String, String))
                                End If
                                vFolderFiles(lDirectory).Add(New Tuple(Of String, String)(lFullPath, lInclude))
                            End If
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error processing item Nodes: {ex.Message}")
            End Try
        End Sub

        Private Sub AddFileToTree(vParentIter As TreeIter, vFullPath As String, vRelativePath As String)
            Dim lFileName As String = System.IO.Path.GetFileName(vFullPath)
            pTreeStore.AppendValues(vParentIter, lFileName, vFullPath, True, "file")
        End Sub

        Private Sub ScanDirectory(vParentIter As TreeIter, vDirectory As String)
            Try
                ' Skip if this is not the project root directory
                If vDirectory <> pProjectDirectory Then
                    Return
                End If
                
                ' Get all directories and files
                Dim lDirectories As New List(Of String)
                Dim lFiles As New List(Of String)
                
                ' Add subdirectories (excluding special ones)
                For Each lDir In Directory.GetDirectories(vDirectory)
                    Dim lDirName As String = System.IO.Path.GetFileName(lDir)
                    If Not lDirName.StartsWith(".") AndAlso 
                       lDirName <> "bin" AndAlso 
                       lDirName <> "obj" Then
                        lDirectories.Add(lDir)
                    End If
                Next
                
                ' Sort directories alphabetically
                lDirectories.Sort(Function(a, b) String.Compare(System.IO.Path.GetFileName(a), 
                                                               System.IO.Path.GetFileName(b), 
                                                               StringComparison.OrdinalIgnoreCase))
                
                ' Check if these directories are already in the tree
                ' Only add directories that aren't already there from the project file
                For Each lDir In lDirectories
                    Dim lDirName As String = System.IO.Path.GetFileName(lDir)
                    Dim lAlreadyExists As Boolean = False
                    
                    ' FIXED: Skip Resources folder - it's handled by CreateResourcesNode
                    If lDirName = "Resources" Then
                        Continue For
                    End If
                    
                    ' Check if this folder is already in the tree
                    Dim lChildIter As TreeIter
                    If pTreeStore.IterChildren(lChildIter, vParentIter) Then
                        Do
                            Dim lNodeName As String = CType(pTreeStore.GetValue(lChildIter, 0), String)
                            Dim lIsFile As Boolean = CType(pTreeStore.GetValue(lChildIter, 2), Boolean)
                            If Not lIsFile AndAlso lNodeName = lDirName Then
                                lAlreadyExists = True
                                Exit Do
                            End If
                        Loop While pTreeStore.IterNext(lChildIter)
                    End If
                    
                    If Not lAlreadyExists Then
                        Dim lDirIter As TreeIter = pTreeStore.AppendValues(vParentIter, lDirName, lDir, False, "folder")
                        
                        ' SPECIAL HANDLING: For My Project folder, scan its contents
                        If lDirName = "My project" Then
                            ScanMyProjectFolder(lDirIter, lDir)
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ScanDirectory error: {ex.Message}")
            End Try
        End Sub

        Private Sub ScanMyProjectFolder(vParentIter As TreeIter, vDirectory As String)
            Try
                ' Look for common My Project files
                Dim lFiles As New List(Of String)
                
                ' Check for AssemblyInfo.vb
                Dim lAssemblyInfo As String = System.IO.Path.Combine(vDirectory, "AssemblyInfo.vb")
                If File.Exists(lAssemblyInfo) Then
                    lFiles.Add(lAssemblyInfo)
                End If
                
                ' Check for Settings files
                Dim lSettingsFiles() As String = {
                    "Settings.settings",
                    "Settings.Designer.vb",
                    "Resources.resx",
                    "Resources.Designer.vb",
                    "Application.myapp",
                    "Application.Designer.vb"
                }
                
                For Each lFileName In lSettingsFiles
                    Dim lFilePath As String = System.IO.Path.Combine(vDirectory, lFileName)
                    If File.Exists(lFilePath) Then
                        lFiles.Add(lFilePath)
                    End If
                Next
                
                ' Add any other .vb files in My Project folder
                For Each lFile In Directory.GetFiles(vDirectory, "*.vb")
                    If Not lFiles.Contains(lFile) Then
                        lFiles.Add(lFile)
                    End If
                Next
                
                ' Sort files and add to tree
                lFiles.Sort(Function(a, b) String.Compare(System.IO.Path.GetFileName(a), 
                                                         System.IO.Path.GetFileName(b), 
                                                         StringComparison.OrdinalIgnoreCase))
                
                For Each lFile In lFiles
                    AddFileToTree(vParentIter, lFile, GetRelativePath(pProjectDirectory, lFile))
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error scanning My project folder: {ex.Message}")
            End Try
        End Sub


        Private Sub RenderIconCell(vColumn As TreeViewColumn, vCell As CellRenderer, vModel As ITreeModel, vIter As TreeIter)
            Try
                Dim lCellPixbuf As CellRendererPixbuf = CType(vCell, CellRendererPixbuf)
                Dim lIsFile As Boolean = CType(vModel.GetValue(vIter, 2), Boolean)
                
                If pCurrentViewMode = ViewMode.eTreeView Then
                    Dim lNodeType As String = CType(vModel.GetValue(vIter, 3), String)
                    
                    ' Set icon based on type
                    Select Case lNodeType
                        Case "project"
                            lCellPixbuf.IconName = "folder-open"
                        Case "folder"
                            lCellPixbuf.IconName = "folder"
                        Case "file"
                            Dim lFilePath As String = CType(vModel.GetValue(vIter, 1), String)
                            If lFilePath.ToLower().EndsWith(".vb") Then
                                lCellPixbuf.IconName = "text-x-vb"
                            ElseIf lFilePath.ToLower().EndsWith(".vbproj") Then
                                lCellPixbuf.IconName = "application-x-executable"
                            Else
                                lCellPixbuf.IconName = "text-x-generic-Template"
                            End If
                        Case "references"
                            lCellPixbuf.IconName = "package-x-generic"
                        Case "assembly"
                            lCellPixbuf.IconName = "application-x-sharedlib"
                        Case "Package"
                            lCellPixbuf.IconName = "package-x-generic"
                        Case "projectref"
                            lCellPixbuf.IconName = "folder-remote"
                        Case "manifest"
                            lCellPixbuf.IconName = "text-x-generic"
                        Case "resources"                 
                            lCellPixbuf.IconName = "folder-documents"
                        Case "resource-string"
                            lCellPixbuf.IconName = "text-x-generic"
                        Case "resource-image"
                            lCellPixbuf.IconName = "image-x-generic"
                        Case "resource-Icon"
                            lCellPixbuf.IconName = "image-x-generic"
                        Case "resource-audio"
                            lCellPixbuf.IconName = "audio-x-generic"
                        Case "resource-Text"
                            lCellPixbuf.IconName = "text-x-generic"
                        Case "resource-binary"
                            lCellPixbuf.IconName = "application-octet-stream"
                        Case Else
                            lCellPixbuf.IconName = "text-x-generic"
                    End Select
                Else
                    ' List view - simpler icons
                    If lIsFile Then
                        Dim lFilePath As String = CType(vModel.GetValue(vIter, 1), String)
                        If lFilePath.ToLower().EndsWith(".vb") Then
                            lCellPixbuf.IconName = "text-x-generic"
                        ElseIf lFilePath.ToLower().EndsWith(".vbproj") Then
                            lCellPixbuf.IconName = "application-x-executable"
                        Else
                            lCellPixbuf.IconName = "text-x-generic-template"
                        End If
                    Else
                        lCellPixbuf.IconName = "folder"
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RenderIconCell error: {ex.Message}")
            End Try
        End Sub

        ' FileInfo helper class
        Private Class FileInfo
            Public Property FileName As String
            Public Property FullPath As String
            Public Property RelativePath As String
            Public Property IsFile As Boolean
        End Class

        ' Context menu handlers remain the same...
        Private Sub OnAddNewFile(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New InputDialog(Nothing, "New VB File", "Enter file Name (without extension):")
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    Dim lFileName As String = lDialog.Text.Trim()
                    If Not String.IsNullOrEmpty(lFileName) Then
                        If Not lFileName.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) Then
                            lFileName &= ".vb"
                        End If
                        
                        ' Determine target directory
                        Dim lTargetDir As String = pProjectDirectory
                        If Not String.IsNullOrEmpty(pCurrentSelectedPath) AndAlso Not pCurrentIsFile Then
                            lTargetDir = pCurrentSelectedPath
                        ElseIf Not String.IsNullOrEmpty(pCurrentSelectedPath) AndAlso pCurrentIsFile Then
                            lTargetDir = System.IO.Path.GetDirectoryName(pCurrentSelectedPath)
                        End If
                        
                        Dim lFullPath As String = System.IO.Path.Combine(lTargetDir, lFileName)
                        
                        ' Check if file already exists
                        If File.Exists(lFullPath) Then
                            ShowError($"File '{lFileName}' already exists in this Location.")
                        Else
                            ' Create file with basic template
                            Dim lTemplate As String = $"' {lFileName}{Environment.NewLine}" &
                                                    $"' Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" &
                                                    $"{Environment.NewLine}" &
                                                    $"Imports System{Environment.NewLine}" &
                                                    $"{Environment.NewLine}" &
                                                    $"Namespace SimpleIDE{Environment.NewLine}" &
                                                    $"{Environment.NewLine}" &
                                                    $"    Public Class {System.IO.Path.GetFileNameWithoutExtension(lFileName)}{Environment.NewLine}" &
                                                    $"{Environment.NewLine}" &
                                                    $"    End Class{Environment.NewLine}" &
                                                    $"{Environment.NewLine}" &
                                                    $"End Namespace{Environment.NewLine}"
                            
                            File.WriteAllText(lFullPath, lTemplate)
                            
                            ' Add to project
                            AddFileToProject(lFullPath)
                            
                            ' Reload project
                            LoadProject(pProjectFile)
                            
                            ' Select and open the new file
                            RaiseEvent FileSelected(lFullPath)
                        End If
                    End If
                End If
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAddNewFile error: {ex.Message}")
                ShowError($"Failed to create New file: {ex.Message}")
            End Try
        End Sub

        Private Sub OnAddExistingFile(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Add Existing File",
                    Nothing,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Add", ResponseType.Accept
                )
                
                ' Add filters
                Dim lVBFilter As New FileFilter()
                lVBFilter.Name = "VB.NET Files (*.vb)"
                lVBFilter.AddPattern("*.vb")
                lDialog.AddFilter(lVBFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files (*.*)"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                ' Set initial directory
                If Not String.IsNullOrEmpty(pProjectDirectory) Then
                    lDialog.SetCurrentFolder(pProjectDirectory)
                End If
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lSelectedFile As String = lDialog.FileName
                    
                    ' Add to project
                    AddFileToProject(lSelectedFile)
                    
                    ' Reload project
                    LoadProject(pProjectFile)
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAddExistingFile error: {ex.Message}")
                ShowError($"Failed to add existing file: {ex.Message}")
            End Try
        End Sub

        Private Sub OnAddNewFolder(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New InputDialog(Nothing, "New Folder", "Enter folder Name:")
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    Dim lFolderName As String = lDialog.Text.Trim()
                    If Not String.IsNullOrEmpty(lFolderName) Then
                        ' Determine target directory
                        Dim lTargetDir As String = pProjectDirectory
                        If Not String.IsNullOrEmpty(pCurrentSelectedPath) AndAlso Not pCurrentIsFile Then
                            lTargetDir = pCurrentSelectedPath
                        ElseIf Not String.IsNullOrEmpty(pCurrentSelectedPath) AndAlso pCurrentIsFile Then
                            lTargetDir = System.IO.Path.GetDirectoryName(pCurrentSelectedPath)
                        End If
                        
                        Dim lFullPath As String = System.IO.Path.Combine(lTargetDir, lFolderName)
                        
                        ' Check if folder already exists
                        If Directory.Exists(lFullPath) Then
                            ShowError($"Folder '{lFolderName}' already exists in this Location.")
                        Else
                            ' Create folder
                            Directory.CreateDirectory(lFullPath)
                            
                            ' Reload project
                            LoadProject(pProjectFile)
                        End If
                    End If
                End If
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAddNewFolder error: {ex.Message}")
                ShowError($"Failed to create New folder: {ex.Message}")
            End Try
        End Sub

        Private Sub OnRemoveFromProject(vSender As Object, vE As EventArgs)
            Try
                If String.IsNullOrEmpty(pCurrentSelectedPath) Then Return
                
                If pCurrentIsFile Then
                    ' Don't allow removing the project file itself
                    If pCurrentSelectedPath.Equals(pProjectFile, StringComparison.OrdinalIgnoreCase) Then
                        ShowError("Cannot remove the project file from the project.")
                        Return
                    End If
                    
                    ' Confirm removal
                    Dim lDialog As New MessageDialog(
                        Nothing,
                        DialogFlags.Modal,
                        MessageType.Question,
                        ButtonsType.YesNo,
                        $"Remove '{System.IO.Path.GetFileName(pCurrentSelectedPath)}' from the project?{Environment.NewLine}(the file will not be deleted from disk)"
                    )
                    
                    If lDialog.Run() = CInt(ResponseType.Yes) Then
                        RemoveFileFromProject(pCurrentSelectedPath)
                        LoadProject(pProjectFile)
                    End If
                    
                    lDialog.Destroy()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnRemoveFromProject error: {ex.Message}")
                ShowError($"Failed to remove from project: {ex.Message}")
            End Try
        End Sub

        Private Sub OnDeleteFile(vSender As Object, vE As EventArgs)
            Try
                If String.IsNullOrEmpty(pCurrentSelectedPath) OrElse Not pCurrentIsFile Then Return
                
                ' Don't allow deleting the project file
                If pCurrentSelectedPath.Equals(pProjectFile, StringComparison.OrdinalIgnoreCase) Then
                    ShowError("Cannot Delete the project file.")
                    Return
                End If
                
                ' Confirm deletion
                Dim lDialog As New MessageDialog(
                    Nothing,
                    DialogFlags.Modal,
                    MessageType.Warning,
                    ButtonsType.YesNo,
                    $"Are you sure you want to permanently Delete '{System.IO.Path.GetFileName(pCurrentSelectedPath)}'?"
                )
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    ' Delete file
                    File.Delete(pCurrentSelectedPath)
                    
                    ' Remove from project if it's in there
                    RemoveFileFromProject(pCurrentSelectedPath)
                    
                    ' Reload project
                    LoadProject(pProjectFile)
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnDeleteFile error: {ex.Message}")
                ShowError($"Failed to Delete file: {ex.Message}")
            End Try
        End Sub

        Private Sub OnRename(vSender As Object, vE As EventArgs)
            Try
                If String.IsNullOrEmpty(pCurrentSelectedPath) Then Return
                
                Dim lOldName As String = System.IO.Path.GetFileName(pCurrentSelectedPath)
                Dim lDialog As New InputDialog(Nothing, "Rename", "Enter New Name:", lOldName)
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    Dim lNewName As String = lDialog.Text.Trim()
                    If Not String.IsNullOrEmpty(lNewName) AndAlso lNewName <> lOldName Then
                        Dim lDirectory As String = System.IO.Path.GetDirectoryName(pCurrentSelectedPath)
                        Dim lNewPath As String = System.IO.Path.Combine(lDirectory, lNewName)
                        
                        ' Check if target exists
                        If File.Exists(lNewPath) OrElse Directory.Exists(lNewPath) Then
                            ShowError($"'{lNewName}' already exists in this Location.")
                        Else
                            ' Rename file or directory
                            If pCurrentIsFile Then
                                File.Move(pCurrentSelectedPath, lNewPath)
                                
                                ' Update in project file if it's a VB file
                                If pCurrentSelectedPath.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) Then
                                    UpdateFileInProject(pCurrentSelectedPath, lNewPath)
                                End If
                            Else
                                Directory.Move(pCurrentSelectedPath, lNewPath)
                                UpdateFolderInProject(pCurrentSelectedPath, lNewPath)
                            End If
                            
                            ' Reload project
                            LoadProject(pProjectFile)
                        End If
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnRename error: {ex.Message}")
                ShowError($"Failed to rename: {ex.Message}")
            End Try
        End Sub

        Private Sub AddFileToProject(vFilePath As String)
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Make path relative to project
                Dim lRelativePath As String = GetRelativePath(pProjectDirectory, vFilePath)
                
                ' Check if already in project
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lExisting = lDoc.SelectSingleNode($"//ms:Compile[@Include='{lRelativePath}']", lNamespaceManager)
                If lExisting Is Nothing Then
                    lExisting = lDoc.SelectSingleNode($"//Compile[@Include='{lRelativePath}']")
                End If
                
                If lExisting IsNot Nothing Then
                    ShowMessage("File is already in the project.")
                    Return
                End If
                
                ' Find or create ItemGroup for Compile items
                Dim lItemGroup As XmlNode = lDoc.SelectSingleNode("//ms:ItemGroup[ms:Compile]", lNamespaceManager)
                If lItemGroup Is Nothing Then
                    lItemGroup = lDoc.SelectSingleNode("//ItemGroup[Compile]")
                End If
                
                If lItemGroup Is Nothing Then
                    ' Create new ItemGroup
                    lItemGroup = lDoc.CreateElement("ItemGroup", lDoc.DocumentElement.NamespaceURI)
                    lDoc.DocumentElement.AppendChild(lItemGroup)
                End If
                
                ' Add Compile element
                Dim lCompile As XmlElement = lDoc.CreateElement("Compile", lDoc.DocumentElement.NamespaceURI)
                lCompile.SetAttribute("Include", lRelativePath)
                lItemGroup.AppendChild(lCompile)
                
                ' Save project file
                lDoc.Save(pProjectFile)

                RaiseEvent ProjectModified()
                
                ' Raise project modified event
                RaiseEvent ProjectModified()
                
            Catch ex As Exception
                Throw New Exception($"Failed to add file to project: {ex.Message}")
            End Try
        End Sub

        Private Sub RemoveFileFromProject(vFilePath As String)
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Make path relative to project
                Dim lRelativePath As String = GetRelativePath(pProjectDirectory, vFilePath)
                
                ' Find the compile node
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lNode = lDoc.SelectSingleNode($"//ms:Compile[@Include='{lRelativePath}']", lNamespaceManager)
                If lNode Is Nothing Then
                    lNode = lDoc.SelectSingleNode($"//Compile[@Include='{lRelativePath}']")
                End If
                
                If lNode IsNot Nothing Then
                    lNode.ParentNode.RemoveChild(lNode)
                    
                    ' Save project file
                    lDoc.Save(pProjectFile)
                    
                    ' Raise project modified event
                    RaiseEvent ProjectModified()
                End If
                
            Catch ex As Exception
                Throw New Exception($"Failed to remove file from project: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateFileInProject(vOldPath As String, vNewPath As String)
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Make paths relative to project
                Dim lOldRelativePath As String = GetRelativePath(pProjectDirectory, vOldPath)
                Dim lNewRelativePath As String = GetRelativePath(pProjectDirectory, vNewPath)
                
                ' Find the compile node
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lNode As XmlNode = lDoc.SelectSingleNode($"//ms:Compile[@Include='{lOldRelativePath}']", lNamespaceManager)
                If lNode Is Nothing Then
                    lNode = lDoc.SelectSingleNode($"//Compile[@Include='{lOldRelativePath}']")
                End If
                
                If lNode IsNot Nothing Then
                    ' Update the Include attribute
                    CType(lNode, XmlElement).SetAttribute("Include", lNewRelativePath)
                    
                    ' Save project file
                    lDoc.Save(pProjectFile)
                    
                    ' Raise project modified event
                    RaiseEvent ProjectModified()
                End If
                
            Catch ex As Exception
                Throw New Exception($"Failed to update file in project: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateFolderInProject(vOldPath As String, vNewPath As String)
            ' TODO: Implement updating all files in a renamed folder
            RaiseEvent ProjectModified()
        End Sub

        Private Sub OnShowProperties(vSender As Object, vE As EventArgs)
            ' TODO: Show file/folder properties dialog
            ShowMessage("Properties dialog not yet implemented")
        End Sub

        Private Function GetRelativePath(vBasePath As String, vFullPath As String) As String
            Try
                Dim lBaseUri As New Uri(vBasePath & System.IO.Path.DirectorySeparatorChar)
                Dim lFullUri As New Uri(vFullPath)
                Return Uri.UnescapeDataString(lBaseUri.MakeRelativeUri(lFullUri).ToString().Replace("/"c, System.IO.Path.DirectorySeparatorChar))
            Catch ex As Exception
                Return vFullPath
            End Try
        End Function


        Private Sub ShowMessage(vMessage As String)
            Console.WriteLine($"ProjectExplorer: {vMessage}")
        End Sub
        
        ' Resource handling methods
        Private Sub OnAddStringResource(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New InputDialog(Nothing, "New String Resource", "Enter resource Name:")
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    Dim lResourceName As String = lDialog.Text.Trim()
                    If Not String.IsNullOrEmpty(lResourceName) Then
                        ' Get or create Resources.resx file
                        Dim lResourcesDir As String = System.IO.Path.Combine(pProjectDirectory, "Resources")
                        If Not Directory.Exists(lResourcesDir) Then
                            Directory.CreateDirectory(lResourcesDir)
                        End If
                        
                        Dim lResxFile As String = System.IO.Path.Combine(lResourcesDir, "Resources.resx")
                        
                        ' Create or update resx file
                        CreateOrUpdateResxFile(lResxFile, lResourceName, "String", "Enter Value here")
                        
                        ' Add to project if not already there
                        AddResourceFileToProject(lResxFile)
                        
                        ' Reload project
                        LoadProject(pProjectFile)
                        
                        ' Open the resx file
                        RaiseEvent FileSelected(lResxFile)
                    End If
                End If
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAddStringResource error: {ex.Message}")
                ShowError($"Failed to add string resource: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnAddImageResource(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select Image Resource",
                    Nothing,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Add", ResponseType.Accept
                )
                
                ' Add image filters
                Dim lImageFilter As New FileFilter()
                lImageFilter.Name = "Image Files"
                lImageFilter.AddPattern("*.png")
                lImageFilter.AddPattern("*.jpg")
                lImageFilter.AddPattern("*.jpeg")
                lImageFilter.AddPattern("*.gif")
                lImageFilter.AddPattern("*.bmp")
                lDialog.AddFilter(lImageFilter)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lSourceFile As String = lDialog.FileName
                    Dim lFileName As String = System.IO.Path.GetFileName(lSourceFile)
                    
                    ' Copy to Resources folder
                    Dim lResourcesDir As String = System.IO.Path.Combine(pProjectDirectory, "Resources")
                    If Not Directory.Exists(lResourcesDir) Then
                        Directory.CreateDirectory(lResourcesDir)
                    End If
                    
                    Dim lTargetFile As String = System.IO.Path.Combine(lResourcesDir, lFileName)
                    
                    ' Check if file already exists
                    If File.Exists(lTargetFile) Then
                        Dim lConfirmDialog As New MessageDialog(
                            Nothing,
                            DialogFlags.Modal,
                            MessageType.Question,
                            ButtonsType.YesNo,
                            $"File '{lFileName}' already exists in Resources. Overwrite?"
                        )
                        
                        If lConfirmDialog.Run() <> CInt(ResponseType.Yes) Then
                            lConfirmDialog.Destroy()
                            lDialog.Destroy()
                            Return
                        End If
                        lConfirmDialog.Destroy()
                    End If
                    
                    ' Copy file
                    File.Copy(lSourceFile, lTargetFile, True)
                    
                    ' Add to project as embedded resource
                    AddEmbeddedResourceToProject(lTargetFile)
                    
                    ' Reload project
                    LoadProject(pProjectFile)
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAddImageResource error: {ex.Message}")
                ShowError($"Failed to add image resource: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnAddIconResource(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select Icon Resource",
                    Nothing,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Add", ResponseType.Accept
                )
                
                ' Add icon filters
                Dim lIconFilter As New FileFilter()
                lIconFilter.Name = "IcOn Files"
                lIconFilter.AddPattern("*.ico")
                lIconFilter.AddPattern("*.png")
                lDialog.AddFilter(lIconFilter)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lSourceFile As String = lDialog.FileName
                    Dim lFileName As String = System.IO.Path.GetFileName(lSourceFile)
                    
                    ' Copy to Resources folder
                    Dim lResourcesDir As String = System.IO.Path.Combine(pProjectDirectory, "Resources")
                    If Not Directory.Exists(lResourcesDir) Then
                        Directory.CreateDirectory(lResourcesDir)
                    End If
                    
                    Dim lTargetFile As String = System.IO.Path.Combine(lResourcesDir, lFileName)
                    
                    ' Check if file already exists
                    If File.Exists(lTargetFile) Then
                        Dim lConfirmDialog As New MessageDialog(
                            Nothing,
                            DialogFlags.Modal,
                            MessageType.Question,
                            ButtonsType.YesNo,
                            $"File '{lFileName}' already exists in Resources. Overwrite?"
                        )
                        
                        If lConfirmDialog.Run() <> CInt(ResponseType.Yes) Then
                            lConfirmDialog.Destroy()
                            lDialog.Destroy()
                            Return
                        End If
                        lConfirmDialog.Destroy()
                    End If
                    
                    ' Copy file
                    File.Copy(lSourceFile, lTargetFile, True)
                    
                    ' Add to project as embedded resource
                    AddEmbeddedResourceToProject(lTargetFile)
                    
                    ' Reload project
                    LoadProject(pProjectFile)
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnAddIconResource error: {ex.Message}")
                ShowError($"Failed to add Icon resource: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateOrUpdateResxFile(vResxFile As String, vResourceName As String, vResourceType As String, vDefaultValue As String)
            Try
                Dim lDoc As XmlDocument
                
                If File.Exists(vResxFile) Then
                    lDoc = New XmlDocument()
                    lDoc.Load(vResxFile)
                Else
                    ' Create new resx file
                    lDoc = CreateNewResxDocument()
                End If
                
                ' Check if resource already exists
                Dim lExisting = lDoc.SelectSingleNode($"//Data[@Name='{vResourceName}']")
                If lExisting IsNot Nothing Then
                    ShowError($"Resource '{vResourceName}' already exists.")
                    Return
                End If
                
                ' Add new data element
                Dim lDataElement As XmlElement = lDoc.CreateElement("Data")
                lDataElement.SetAttribute("Name", vResourceName)
                lDataElement.SetAttribute("xml:space", "preserve")
                
                Dim lValueElement As XmlElement = lDoc.CreateElement("Value")
                lValueElement.InnerText = vDefaultValue
                lDataElement.AppendChild(lValueElement)
                
                If vResourceType <> "String" Then
                    Dim lTypeElement As XmlElement = lDoc.CreateElement("Type")
                    lTypeElement.InnerText = $"System.{vResourceType}, mscorlib"
                    lDataElement.AppendChild(lTypeElement)
                End If
                
                lDoc.DocumentElement.AppendChild(lDataElement)
                
                ' Save file
                lDoc.Save(vResxFile)
                
            Catch ex As Exception
                Throw New Exception($"Failed to create/update resx file: {ex.Message}")
            End Try
        End Sub
        
        Private Function CreateNewResxDocument() As XmlDocument
            Dim lDoc As New XmlDocument()
            lDoc.LoadXml("<?xml Version=""1.0"" Encoding=""utf-8""?>" & Environment.NewLine &
                        "<root>" & Environment.NewLine &
                        "  <xsd:schema Id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">" & Environment.NewLine &
                        "    <xsd:element Name=""root"" msdata:IsDataSet=""true"">" & Environment.NewLine &
                        "      <xsd:complexType>" & Environment.NewLine &
                        "        <xsd:choice maxOccurs=""unbounded"">" & Environment.NewLine &
                        "          <xsd:element Name=""Data"">" & Environment.NewLine &
                        "            <xsd:complexType>" & Environment.NewLine &
                        "              <xsd:sequence>" & Environment.NewLine &
                        "                <xsd:element Name=""Value"" Type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />" & Environment.NewLine &
                        "                <xsd:element Name=""comment"" Type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />" & Environment.NewLine &
                        "              </xsd:sequence>" & Environment.NewLine &
                        "              <xsd:attribute Name=""Name"" Type=""xsd:string"" msdata:Ordinal=""1"" />" & Environment.NewLine &
                        "              <xsd:attribute Name=""Type"" Type=""xsd:string"" msdata:Ordinal=""3"" />" & Environment.NewLine &
                        "              <xsd:attribute Name=""mimetype"" Type=""xsd:string"" msdata:Ordinal=""4"" />" & Environment.NewLine &
                        "            </xsd:complexType>" & Environment.NewLine &
                        "          </xsd:element>" & Environment.NewLine &
                        "          <xsd:element Name=""resheader"">" & Environment.NewLine &
                        "            <xsd:complexType>" & Environment.NewLine &
                        "              <xsd:sequence>" & Environment.NewLine &
                        "                <xsd:element Name=""Value"" Type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />" & Environment.NewLine &
                        "              </xsd:sequence>" & Environment.NewLine &
                        "              <xsd:attribute Name=""Name"" Type=""xsd:string"" use=""required"" />" & Environment.NewLine &
                        "            </xsd:complexType>" & Environment.NewLine &
                        "          </xsd:element>" & Environment.NewLine &
                        "        </xsd:choice>" & Environment.NewLine &
                        "      </xsd:complexType>" & Environment.NewLine &
                        "    </xsd:element>" & Environment.NewLine &
                        "  </xsd:schema>" & Environment.NewLine &
                        "  <resheader Name=""resmimetype"">" & Environment.NewLine &
                        "    <Value>Text/microsoft-resx</Value>" & Environment.NewLine &
                        "  </resheader>" & Environment.NewLine &
                        "  <resheader Name=""Version"">" & Environment.NewLine &
                        "    <Value>1.3</Value>" & Environment.NewLine &
                        "  </resheader>" & Environment.NewLine &
                        "  <resheader Name=""reader"">" & Environment.NewLine &
                        "    <Value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</Value>" & Environment.NewLine &
                        "  </resheader>" & Environment.NewLine &
                        "  <resheader Name=""writer"">" & Environment.NewLine &
                        "    <Value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</Value>" & Environment.NewLine &
                        "  </resheader>" & Environment.NewLine &
                        "</root>")
            Return lDoc
        End Function
        
        Private Sub AddResourceFileToProject(vFilePath As String)
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Make path relative to project
                Dim lRelativePath As String = GetRelativePath(pProjectDirectory, vFilePath)
                
                ' Check if already in project
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lExisting = lDoc.SelectSingleNode($"//ms:EmbeddedResource[@Include='{lRelativePath}']", lNamespaceManager)
                If lExisting Is Nothing Then
                    lExisting = lDoc.SelectSingleNode($"//EmbeddedResource[@Include='{lRelativePath}']")
                End If
                
                If lExisting IsNot Nothing Then
                    Return ' Already in project
                End If
                
                ' Find or create ItemGroup for EmbeddedResource items
                Dim lItemGroup As XmlNode = lDoc.SelectSingleNode("//ms:ItemGroup[ms:EmbeddedResource]", lNamespaceManager)
                If lItemGroup Is Nothing Then
                    lItemGroup = lDoc.SelectSingleNode("//ItemGroup[EmbeddedResource]")
                End If
                
                If lItemGroup Is Nothing Then
                    ' Create new ItemGroup
                    lItemGroup = lDoc.CreateElement("ItemGroup", lDoc.DocumentElement.NamespaceURI)
                    lDoc.DocumentElement.AppendChild(lItemGroup)
                End If
                
                ' Add EmbeddedResource element
                Dim lResource As XmlElement = lDoc.CreateElement("EmbeddedResource", lDoc.DocumentElement.NamespaceURI)
                lResource.SetAttribute("Include", lRelativePath)
                lItemGroup.AppendChild(lResource)
                
                ' Save project file
                lDoc.Save(pProjectFile)
                
                ' Raise project modified event
                RaiseEvent ProjectModified()
                
            Catch ex As Exception
                Throw New Exception($"Failed to add resource file to project: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AddEmbeddedResourceToProject(vFilePath As String)
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Make path relative to project
                Dim lRelativePath As String = GetRelativePath(pProjectDirectory, vFilePath)
                
                ' Check if already in project
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lExisting = lDoc.SelectSingleNode($"//ms:EmbeddedResource[@Include='{lRelativePath}']", lNamespaceManager)
                If lExisting Is Nothing Then
                    lExisting = lDoc.SelectSingleNode($"//EmbeddedResource[@Include='{lRelativePath}']")
                End If
                
                If lExisting IsNot Nothing Then
                    Return ' Already in project
                End If
                
                ' Find or create ItemGroup for EmbeddedResource items
                Dim lItemGroup As XmlNode = lDoc.SelectSingleNode("//ms:ItemGroup[ms:EmbeddedResource]", lNamespaceManager)
                If lItemGroup Is Nothing Then
                    lItemGroup = lDoc.SelectSingleNode("//ItemGroup[EmbeddedResource]")
                End If
                
                If lItemGroup Is Nothing Then
                    ' Create new ItemGroup
                    lItemGroup = lDoc.CreateElement("ItemGroup", lDoc.DocumentElement.NamespaceURI)
                    lDoc.DocumentElement.AppendChild(lItemGroup)
                End If
                
                ' Add EmbeddedResource element
                Dim lResource As XmlElement = lDoc.CreateElement("EmbeddedResource", lDoc.DocumentElement.NamespaceURI)
                lResource.SetAttribute("Include", lRelativePath)
                lItemGroup.AppendChild(lResource)
                
                ' Save project file
                lDoc.Save(pProjectFile)
                
                ' Raise project modified event
                RaiseEvent ProjectModified()
                
            Catch ex As Exception
                Throw New Exception($"Failed to add embedded resource to project: {ex.Message}")
            End Try
        End Sub

        Public Sub RefreshSpecialNodes()
            Try
                ' Refresh References
                If pHasReferencesNode Then
                    LoadProjectReferences()
                End If
                
                ' Refresh Manifest
                If pHasManifestNode Then
                    RefreshManifestNode()
                End If
                
                ' Refresh Resources
                If pHasResourcesNode Then  ' ADD this BLOCK
                    RefreshResourcesNode()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error refreshing special Nodes: {ex.Message}")
            End Try
        End Sub

        Private Sub OnClose(vSender As Object, vE As EventArgs)
            Try
                RaiseEvent CloseRequested()
            Catch ex As Exception
                Console.WriteLine($"error in close handler: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Refreshes the project tree view by reloading the current project
        ''' </summary>
        Public Sub RefreshTree()
            Try
                ' Only refresh if we have a project loaded
                If Not String.IsNullOrEmpty(pProjectFile) Then
                    LoadProject(pProjectFile)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RefreshTree error: {ex.Message}")
            End Try
        End Sub

        ' ===== Missing Methods =====
        
        ''' <summary>
        ''' Refresh the current project display
        ''' </summary>
        Public Sub RefreshProject()
            Try
                If Not String.IsNullOrEmpty(pProjectFile) Then
                    ' Reload the project
                    LoadProject(pProjectFile)
                End If
            Catch ex As Exception
                Console.WriteLine($"RefreshProject error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clear the project from display
        ''' </summary>
        Public Sub ClearProject()
            Try
                ' Clear the tree store
                If pTreeStore IsNot Nothing Then
                    pTreeStore.Clear()
                End If
                
                ' Clear stored paths
                pProjectFile = String.Empty
                pProjectDirectory = String.Empty
                
                ' Update UI state
                If pTreeView IsNot Nothing Then
                    pTreeView.Sensitive = False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ClearProject error: {ex.Message}")
            End Try
        End Sub


    End Class


End Namespace
