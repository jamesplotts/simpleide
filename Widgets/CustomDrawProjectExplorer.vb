' Widgets/CustomDrawProjectExplorer.vb - Custom drawn Project Explorer implementation (Main)
' Created: 2025-08-17
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

Namespace Widgets
    
    ''' <summary>
    ''' Custom drawn implementation of the Project Explorer providing file tree view
    ''' </summary>
    ''' <remarks>
    ''' Provides a custom-rendered tree view of project files with viewport culling,
    ''' unified scaling support with Object Explorer, and comprehensive file management
    ''' </remarks>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Constants =====
        Private Const MIN_SCALE As Integer = 50
        Private Const MAX_SCALE As Integer = 200
        Private Const DEFAULT_SCALE As Integer = 100
        Private Const ICON_SPACING As Integer = 4
        Private Const PLUS_MINUS_SIZE_RATIO As Double = 0.75
        Private Const INDENT_WIDTH_RATIO As Double = 1.25
        Private Const ROW_PADDING As Integer = 4
        Private Const HOVER_TOOLTIP_DELAY As Integer = 500 ' milliseconds
        
        
        ''' <summary>
        ''' Specifies the click zone within a node
        ''' </summary>
        Public Enum ClickZone
            ''' <summary>Unknown or unspecified zone</summary>
            eUnspecified
            ''' <summary>Plus/minus expansion button area</summary>
            ePlusMinus
            ''' <summary>Icon area of the node</summary>
            eIcon
            ''' <summary>Text label area of the node</summary>
            eText
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum

        ' ===== Events =====

        ''' <summary>
        ''' Raised when the manifest node is selected
        ''' </summary>
        Public Event ManifestSelected()
        
        ''' <summary>
        ''' Raised when references are changed
        ''' </summary>
        Public Event ReferencesChanged()        

        ''' <summary>
        ''' Raised when a file is selected for opening
        ''' </summary>
        Public Event FileSelected(vFilePath As String)
        
        ''' <summary>
        ''' Raised when the project file itself is selected
        ''' </summary>
        Public Event ProjectFileSelected(vFilePath As String)
        
        ''' <summary>
        ''' Raised when the project is modified
        ''' </summary>
        Public Event ProjectModified()
        
        ''' <summary>
        ''' Raised when the close button is clicked
        ''' </summary>
        Public Event CloseRequested()
        
        ''' <summary>
        ''' Raised when a node is double-clicked
        ''' </summary>
        Public Event NodeDoubleClicked(vNode As ProjectNode)
        
        ' ===== Private Fields - UI Components =====
        Private pToolbar As Toolbar
        Private pDrawingArea As DrawingArea
        Private pHScrollBar As Scrollbar
        Private pVScrollBar As Scrollbar
        Private pCornerBox As DrawingArea
        Private pSettingsManager As SettingsManager
        Private pProjectManager As ProjectManager
        Private pThemeManager As ThemeManager
        Private pContextMenu As Menu
        
        ' ===== Private Fields - Drawing State =====
        Private pCurrentScale As Integer = DEFAULT_SCALE
        Private pIconSize As Integer = 16
        Private pFontSize As Single = 10.0F
        Private pRowHeight As Integer = 20
        Private pIndentWidth As Integer = 20
        Private pPlusMinusSize As Integer = 12
        Private pFontDescription As FontDescription
        Private pIconTheme As IconTheme
        
        ' ===== Private Fields - Project Data =====
        Private pProjectFile As String = ""
        Private pProjectDirectory As String = ""
        Private pRootNode As ProjectNode
        Private pVisibleNodes As New List(Of VisualProjectNode)
        Private pExpandedNodes As New HashSet(Of String)
        Private pSelectedNode As VisualProjectNode
        Private pHoveredNode As VisualProjectNode
        Private pNodeCache As New Dictionary(Of String, VisualProjectNode)
        
        ' ===== Private Fields - Special Nodes =====
        Private pReferencesNode As ProjectNode
        Private pResourcesNode As ProjectNode
        Private pManifestNode As ProjectNode
        Private pHasReferencesNode As Boolean = False
        Private pHasResourcesNode As Boolean = False
        Private pHasManifestNode As Boolean = False
        
        ' ===== Private Fields - Viewport State =====
        Private pScrollX As Integer = 0
        Private pScrollY As Integer = 0
        Private pContentWidth As Integer = 0
        Private pContentHeight As Integer = 0
        Private pViewportWidth As Integer = 0
        Private pViewportHeight As Integer = 0
        
        ' ===== Private Fields - Interaction State =====
        Private pMouseX As Integer = 0
        Private pMouseY As Integer = 0
        Private pTooltipTimer As UInteger = 0
        Private pLastClickTime As DateTime = DateTime.MinValue
        Private pIsDragging As Boolean = False
        Private pDragStartX As Integer = 0
        Private pDragStartY As Integer = 0

        ' Tooltip support
        Private pTooltipWindow As Gtk.Window
        Private pTooltipLabel As Label
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Initializes a new instance of the CustomDrawProjectExplorer
        ''' </summary>
        ''' <param name="vSettingsManager">Settings manager for configuration persistence</param>
        Public Sub New(vSettingsManager As SettingsManager, vProjectManager as ProjectManager)
            MyBase.New(Orientation.Vertical, 0)
            
            Try
                pSettingsManager = vSettingsManager
                pProjectManager = vProjectManager  
                pThemeManager = New ThemeManager(vSettingsManager)
                pIconTheme = IconTheme.Default
                
                ' Initialize components
                InitializeToolbar()
                CreateUIComponents()
                InitializeDrawing()
                InitializeContextMenu()
                
                ' Set up event handlers
                SetupEventHandlers()
                
                ' Load settings including unified text scale
                LoadSettings()
                
                ' Apply initial theme
                ApplyTheme()
                
                ' Show all components
                ShowAll()
                
                Console.WriteLine("CustomDrawProjectExplorer initialized")
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawProjectExplorer constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Initialization Methods =====
        
        ''' <summary>
        ''' Creates the main UI components
        ''' </summary>
        Private Sub CreateUIComponents()
            Try
                ' Create main container
                Dim lMainBox As New Box(Orientation.Vertical, 0)
                
                ' Create horizontal box for drawing area and vertical scrollbar
                Dim lHorizontalBox As New Box(Orientation.Horizontal, 0)
                
                ' Create drawing area
                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                pDrawingArea.Events = EventMask.ExposureMask Or EventMask.ButtonPressMask Or 
                                     EventMask.ButtonReleaseMask Or EventMask.PointerMotionMask Or 
                                     EventMask.ScrollMask Or EventMask.KeyPressMask Or 
                                     EventMask.KeyReleaseMask Or EventMask.LeaveNotifyMask
                
                ' Create scrollbars
                pVScrollBar = New Scrollbar(Orientation.Vertical, Nothing)
                pHScrollBar = New Scrollbar(Orientation.Horizontal, Nothing)
                
                ' Create corner box
                pCornerBox = New DrawingArea()
                pCornerBox.SetSizeRequest(20, 20)
                
                ' Pack horizontal box
                lHorizontalBox.PackStart(pDrawingArea, True, True, 0)
                lHorizontalBox.PackStart(pVScrollBar, False, False, 0)
                
                ' Create bottom box for horizontal scrollbar and corner
                Dim lBottomBox As New Box(Orientation.Horizontal, 0)
                lBottomBox.PackStart(pHScrollBar, True, True, 0)
                lBottomBox.PackStart(pCornerBox, False, False, 0)
                
                ' Pack main box
                lMainBox.PackStart(lHorizontalBox, True, True, 0)
                lMainBox.PackStart(lBottomBox, False, False, 0)
                
                ' Add to main container (after toolbar)
                PackStart(lMainBox, True, True, 0)
                
            Catch ex As Exception
                Console.WriteLine($"CreateUIComponents error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initializes drawing settings and fonts
        ''' </summary>
        Private Sub InitializeDrawing()
            Try
                ' Load unified text scale from Explorer.TextScale setting
                Dim lTextScale As Integer = pSettingsManager.GetInteger("Explorer.TextScale", DEFAULT_SCALE)
                ApplyScale(lTextScale)
                
                ' Initialize font
                UpdateFontSettings()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeDrawing error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets up all event handlers
        ''' </summary>
        Private Sub SetupEventHandlers()
            Try
                ' Drawing area events
                AddHandler pDrawingArea.Drawn, AddressOf OnDrawingAreaDraw
                AddHandler pDrawingArea.SizeAllocated, AddressOf OnDrawingAreaSizeAllocated
                AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnDrawingAreaButtonPress
                AddHandler pDrawingArea.ButtonReleaseEvent, AddressOf OnDrawingAreaButtonRelease
                AddHandler pDrawingArea.MotionNotifyEvent, AddressOf OnDrawingAreaMotionNotify
                AddHandler pDrawingArea.ScrollEvent, AddressOf OnDrawingAreaScroll
                AddHandler pDrawingArea.KeyPressEvent, AddressOf OnDrawingAreaKeyPress
                AddHandler pDrawingArea.LeaveNotifyEvent, AddressOf OnDrawingAreaLeaveNotify
                
                ' Scrollbar events
                AddHandler pHScrollBar.ValueChanged, AddressOf OnHScrollBarValueChanged
                AddHandler pVScrollBar.ValueChanged, AddressOf OnVScrollBarValueChanged
                
                ' Corner box drawing
                AddHandler pCornerBox.Drawn, AddressOf OnCornerBoxDraw
                
                ' Settings manager events
                If pSettingsManager IsNot Nothing Then
                    AddHandler pSettingsManager.SettingsChanged, AddressOf OnSettingsChanged
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetupEventHandlers error: {ex.Message}")
            End Try
        End Sub
       
        ' ===== Public Methods =====

        Public Sub LoadProjectFromManager()
            Try
                Console.WriteLine("LoadProjectFromManager: Loading from ProjectManager")
                
                If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                    Console.WriteLine("No project loaded in ProjectManager")
                    Return
                End If
                
                ' Clear existing data
                ClearProject()
                
                ' Get project info from manager
                Dim lProjectInfo As ProjectInfo = pProjectManager.CurrentProjectInfo
                If lProjectInfo Is Nothing Then
                    Console.WriteLine("No project info available")
                    Return
                End If
                
                pProjectFile = lProjectInfo.ProjectPath
                pProjectDirectory = lProjectInfo.ProjectDirectory
                
                Console.WriteLine($"Project: {lProjectInfo.ProjectName}")
                Console.WriteLine($"Directory: {pProjectDirectory}")
                Console.WriteLine($"Files count: {lProjectInfo.SourceFiles.Count}")
                
                ' Create root project node
                pRootNode = New ProjectNode() With {
                    .Name = lProjectInfo.ProjectName,
                    .Path = pProjectDirectory,
                    .NodeType = ProjectNodeType.eProject,
                    .IsFile = False,
                    .IsExpanded = True
                }
                
                ' Add to expanded nodes
                pExpandedNodes.Add(GetNodePath(pRootNode))
                
                ' Build tree from ProjectManager's file list
                BuildTreeFromFileList(lProjectInfo.SourceFiles)
                
                ' Create special nodes (References, etc.)
                CreateSpecialNodes()
                
                ' SORT THE TREE - This ensures folders come before files at every level
                pRootNode?.SortChildren()
                
                ' Rebuild visual representation
                RebuildVisualTree()
                
                ' Force redraw
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"Project loaded: {pVisibleNodes.Count} nodes visible")
                
            Catch ex As Exception
                Console.WriteLine($"LoadProjectFromManager error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Builds the project tree from a list of file paths
        ''' </summary>
        Private Sub BuildTreeFromFileList(vFiles As List(Of String))
            Try
                If vFiles Is Nothing OrElse vFiles.Count = 0 Then
                    Console.WriteLine("No files to process")
                    Return
                End If
                
                Console.WriteLine($"Building tree from {vFiles.Count} files")
                
                ' Group files by directory
                Dim lFilesByDir As New Dictionary(Of String, List(Of String))()
                
                For Each lFilePath In vFiles
                    ' Get relative path from project directory
                    Dim lRelativePath As String = GetRelativePathFromProject(lFilePath)
                    
                    ' Skip files outside project directory
                    If lRelativePath.StartsWith("..") Then Continue For
                    
                    ' Skip bin and obj directories
                    If lRelativePath.StartsWith("bin" & System.IO.Path.DirectorySeparatorChar) OrElse
                       lRelativePath.StartsWith("obj" & System.IO.Path.DirectorySeparatorChar) Then
                        Continue For
                    End If
                    
                    ' Get directory part
                    Dim lDirPath As String = System.IO.Path.GetDirectoryName(lRelativePath)
                    If String.IsNullOrEmpty(lDirPath) Then lDirPath = ""
                    
                    ' Add to dictionary
                    If Not lFilesByDir.ContainsKey(lDirPath) Then
                        lFilesByDir(lDirPath) = New List(Of String)()
                    End If
                    lFilesByDir(lDirPath).Add(lFilePath)
                Next
                
                Console.WriteLine($"Found {lFilesByDir.Count} directories")
                
                ' Process each directory
                For Each lDirEntry In lFilesByDir
                    Dim lDirPath As String = lDirEntry.Key
                    Dim lFiles As List(Of String) = lDirEntry.Value
                    
                    ' Create/find folder nodes for path
                    Dim lParentNode As ProjectNode = pRootNode
                    
                    If Not String.IsNullOrEmpty(lDirPath) Then
                        Dim lPathParts As String() = lDirPath.Split(System.IO.Path.DirectorySeparatorChar)
                        
                        For Each lPart In lPathParts
                            If String.IsNullOrEmpty(lPart) Then Continue For
                            
                            ' Find or create folder node
                            Dim lFolderNode As ProjectNode = lParentNode.Children.FirstOrDefault(
                                Function(n) n.Name = lPart AndAlso Not n.IsFile)
                            
                            If lFolderNode Is Nothing Then
                                ' Create new folder node
                                Dim lFullFolderPath As String = System.IO.Path.Combine(pProjectDirectory, 
                                    String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), lPathParts))
                                
                                lFolderNode = New ProjectNode() With {
                                    .Name = lPart,
                                    .Path = lFullFolderPath,
                                    .NodeType = GetFolderType(lPart),
                                    .IsFile = False
                                }
                                lParentNode.AddChild(lFolderNode)
                                Console.WriteLine($"  Created folder: {lPart}")
                            End If
                            
                            lParentNode = lFolderNode
                        Next
                    End If
                    
                    ' Add files to parent node
                    For Each lFilePath In lFiles
                        Dim lFileName As String = System.IO.Path.GetFileName(lFilePath)
                        
                        ' Check if file already exists
                        Dim lExisting As ProjectNode = lParentNode.Children.FirstOrDefault(
                            Function(n) n.Name = lFileName AndAlso n.IsFile)
                        
                        If lExisting Is Nothing Then
                            ' Create file node
                            Dim lFileNode As New ProjectNode() With {
                                .Name = lFileName,
                                .Path = lFilePath,
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
                            
                            lParentNode.AddChild(lFileNode)
                        End If
                    Next
                    
                    Console.WriteLine($"  Directory '{lDirPath}': {lFiles.Count} files")
                Next
                
            Catch ex As Exception
                Console.WriteLine($"BuildTreeFromFileList error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets relative path from project directory
        ''' </summary>
        Private Function GetRelativePathFromProject(vFullPath As String) As String
            Try
                If String.IsNullOrEmpty(pProjectDirectory) Then Return vFullPath
                
                Dim lFullPath As String = System.IO.Path.GetFullPath(vFullPath)
                Dim lProjectDir As String = System.IO.Path.GetFullPath(pProjectDirectory)
                
                If Not lProjectDir.EndsWith(System.IO.Path.DirectorySeparatorChar) Then
                    lProjectDir &= System.IO.Path.DirectorySeparatorChar
                End If
                
                If lFullPath.StartsWith(lProjectDir, StringComparison.OrdinalIgnoreCase) Then
                    Return lFullPath.Substring(lProjectDir.Length)
                End If
                
                Return vFullPath
                
            Catch ex As Exception
                Console.WriteLine($"GetRelativePathFromProject error: {ex.Message}")
                Return vFullPath
            End Try
        End Function
        
'        ''' <summary>
'        ''' Loads a project file and displays its structure
'        ''' </summary>
'        ''' <param name="vProjectFile">Path to the .vbproj file</param>
'        Public Sub LoadxProject(vProjectFile As String)
'            Try
'                Console.WriteLine($"LoadProject: Loading {vProjectFile}")
'                Console.WriteLine("=======================================")
'                
'                If Not File.Exists(vProjectFile) Then
'                    Console.WriteLine($"Project file not found: {vProjectFile}")
'                    Return
'
'                End If
'
'                Console.WriteLine("vProjectFile=" + vProjectFile)                
'                pProjectFile = vProjectFile
'                pProjectDirectory = System.IO.Path.GetDirectoryName(vProjectFile)
'                
'                ' Clear existing data
'                ClearProject()
'                
'                ' Create root project node
'                Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(vProjectFile)
'                pRootNode = New ProjectNode() With {
'                    .Name = lProjectName,
'                    .Path = pProjectDirectory,
'                    .NodeType = ProjectNodeType.eProject,
'                    .IsFile = False,
'                    .IsExpanded = True
'                }
'                
'                Console.WriteLine($"Created root node: {lProjectName}")
'                Console.WriteLine($"Project directory: {pProjectDirectory}")
'                
'                ' CRITICAL FIX: Add root node to expanded nodes BEFORE loading structure
'                Dim lRootPath As String = GetNodePath(pRootNode)
'                pExpandedNodes.Add(lRootPath)
'                Console.WriteLine($"Added root to expanded nodes: {lRootPath}")
'                
'                ' Load project structure from .vbproj
'                LoadProjectStructure()
'                
'                ' Add special nodes
'                CreateSpecialNodes()
'                
'                ' Debug: Log root node children
'                Console.WriteLine($"Root node has {pRootNode?.Children.Count} children:")
'                If pRootNode IsNot Nothing Then
'                    For Each lChild In pRootNode.Children
'                        Console.WriteLine($"  - {lChild.Name} (IsFile={lChild.IsFile}, Type={lChild.NodeType})")
'                        If Not lChild.IsFile AndAlso lChild.Children.Count > 0 Then
'                            Console.WriteLine($"    Has {lChild.Children.Count} children")
'                        End If
'                    Next
'                End If
'                
'                ' Now rebuild visual tree with the root already marked as expanded
'                RebuildVisualTree()
'                
'                ' Debug: Log visible nodes
'                Console.WriteLine($"Visible nodes count: {pVisibleNodes.Count}")
'                For Each lNode In pVisibleNodes
'                    Console.WriteLine($"  Visible: {lNode.Node.Name} at depth {lNode.Depth}")
'                Next
'                
'                Console.WriteLine("=======================================")
'                Console.WriteLine($"Project loaded: {lProjectName}")
'                
'                ' Refresh display
'                pDrawingArea?.QueueDraw()
'                
'            Catch ex As Exception
'                Console.WriteLine($"LoadProject error: {ex.Message}")
'                Console.WriteLine($"Stack trace: {ex.StackTrace}")
'            End Try
'        End Sub
        
        ''' <summary>
        ''' Refreshes the current project display
        ''' </summary>
        Public Sub RefreshProject()
            Try
                Console.WriteLine($"Calling pProjectExplorer.LoadProjectFromManager from CustomDrawProjectExplorer.RefreshProject")
                LoadProjectFromManager
                
            Catch ex As Exception
                Console.WriteLine($"RefreshProject error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clears the project from display
        ''' </summary>
        Public Sub ClearProject()
            Try
                pRootNode = Nothing
                pVisibleNodes.Clear()
                pNodeCache.Clear()
                pSelectedNode = Nothing
                pHoveredNode = Nothing
                
                pProjectFile = String.Empty
                pProjectDirectory = String.Empty
                
                pHasReferencesNode = False
                pHasResourcesNode = False
                pHasManifestNode = False
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ClearProject error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the currently selected file path
        ''' </summary>
        Public Function GetSelectedPath() As String
            Try
                If pSelectedNode?.Node?.IsFile = True Then
                    Return pSelectedNode.Node.Path
                End If
                Return String.Empty
                
            Catch ex As Exception
                Console.WriteLine($"GetSelectedPath error: {ex.Message}")
                Return String.Empty
            End Try
        End Function


        ''' <summary>
        ''' Refreshes the entire project tree
        ''' </summary>
        Public Sub RefreshTree()
            Try
                Console.WriteLine($"Calling pProjectExplorer.LoadProjectFromManager from CustomDrawProjectExplorer.RefreshTree")
                LoadProjectFromManager
                
            Catch ex As Exception
                Console.WriteLine($"RefreshTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Refreshes only the manifest node in the tree
        ''' </summary>
        Public Sub RefreshManifestNode()
            Try
                If pRootNode Is Nothing Then Return
                
                ' Remove existing manifest node if present
                If pManifestNode IsNot Nothing AndAlso pRootNode.Children.Contains(pManifestNode) Then
                    pRootNode.Children.Remove(pManifestNode)
                    pManifestNode = Nothing
                    pHasManifestNode = False
                End If
                
                ' Re-create manifest node
                CreateManifestNode()
                
                ' Rebuild visual tree and refresh display
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"RefreshManifestNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Refreshes the references node in the tree
        ''' </summary>
        Public Sub RefreshReferences()
            Try
                If pRootNode Is Nothing Then Return
                
                ' Remove existing references node if present
                If pReferencesNode IsNot Nothing AndAlso pRootNode.Children.Contains(pReferencesNode) Then
                    pRootNode.Children.Remove(pReferencesNode)
                    pReferencesNode = Nothing
                    pHasReferencesNode = False
                End If
                
                ' Re-create references node
                CreateReferencesNode()
                
                ' Rebuild visual tree and refresh display
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
                ' Raise event to notify listeners
                RaiseEvent ReferencesChanged()
                
            Catch ex As Exception
                Console.WriteLine($"RefreshReferences error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the project manager after construction (for initialization order issues)
        ''' </summary>
        ''' <param name="vProjectManager">The project manager instance</param>
        Public Sub SetProjectManager(vProjectManager As ProjectManager)
            Try
                pProjectManager = vProjectManager
                Console.WriteLine($"ProjectManager set in CustomDrawProjectExplorer: {If(pProjectManager IsNot Nothing, "Success", "Nothing")}")
            Catch ex As Exception
                Console.WriteLine($"SetProjectManager error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    

    
End Namespace