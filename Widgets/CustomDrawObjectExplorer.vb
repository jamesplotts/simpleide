' Widgets/CustomDrawObjectExplorer.vb - Custom drawn Object Explorer implementation (Main)
' Created: 2025-08-16
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Widgets
    
    ''' <summary>
    ''' Custom drawn implementation of the Object Explorer widget providing hierarchical code structure view
    ''' </summary>
    ''' <remarks>
    ''' Provides a custom-rendered tree view of code structure with viewport culling,
    ''' scaling support, and comprehensive navigation features
    ''' </remarks>
    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
        
        ' ===== Constants =====
        Private Const MIN_SCALE As Integer = 50
        Private Const MAX_SCALE As Integer = 200
        Private Const DEFAULT_SCALE As Integer = 100
        Private Const ICON_SPACING As Integer = 4
        Private Const PLUS_MINUS_SIZE_RATIO As Double = 0.75
        Private Const INDENT_WIDTH_RATIO As Double = 1.25
        Private Const ROW_PADDING As Integer = 4
        Private Const HOVER_TOOLTIP_DELAY As Integer = 500 ' milliseconds

        ' ===== Private Fields - State Preservation =====
        Private pLastValidRootNode As SyntaxNode  ' Store last valid root to recover from clears
        Private pIsProjectLoaded As Boolean = False  ' Track if a project is loaded
        Private pNeedsRebuild As Boolean = False
        Private pLastRebuildRoot As SyntaxNode = Nothing
        Private pLastRebuildHash As Integer = 0
        Private pIsRebuildingTree As Boolean = False  ' Prevent recursive rebuilds
        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when a node is selected (single-click) in the tree
        ''' </summary>
        ''' <param name="vNode">The selected syntax node</param>
        Public Event NodeSelected(vNode As SyntaxNode) Implements IObjectExplorer.NodeSelected
        
        ''' <summary>
        ''' Raised when a node is double-clicked in the tree
        ''' </summary>
        ''' <param name="vNode">The double-clicked syntax node</param>
        Public Event NodeDoubleClicked(vNode As SyntaxNode) Implements IObjectExplorer.NodeDoubleClicked
        
        ''' <summary>
        ''' Raised when a node is activated (typically via double-click or enter key)
        ''' </summary>
        ''' <param name="vNode">The activated syntax node</param>
        Public Event NodeActivated(vNode As SyntaxNode) Implements IObjectExplorer.NodeActivated
        
        ''' <summary>
        ''' Raised when the user requests to navigate to a file location
        ''' </summary>
        ''' <param name="vFilePath">Full path to the file to navigate to</param>
        ''' <param name="vLine">Line number to navigate to (1-based)</param>
        ''' <param name="vColumn">Column position to navigate to (1-based)</param>
        Public Event NavigateToFile(vFilePath As String, vPosition As EditorPosition) Implements IObjectExplorer.NavigateToFile
        
        ''' <summary>
        ''' Raised when the user requests to close the Object Explorer
        ''' </summary>
        Public Event CloseRequested() Implements IObjectExplorer.CloseRequested
        
        ' ===== Private Fields - UI Components =====
        Private pDrawingArea As DrawingArea
        Private pHScrollBar As CustomDrawScrollbar
        Private pVScrollBar As CustomDrawScrollbar
        Private pCornerBox As DrawingArea
        Private pSettingsManager As SettingsManager
        Private pProjectManager As ProjectManager
        Private pCurrentEditor As IEditor

        ''' <summary>
        ''' The currently loaded solution, when SetSolutionManager has been called - Nothing
        ''' for a plain single-project open, matching CustomDrawProjectExplorer's pattern
        ''' </summary>
        Private pSolutionManager As SolutionManager
        
        ' ===== Private Fields - Drawing State =====
        Private pCurrentScale As Integer = DEFAULT_SCALE
        Private pIconSize As Integer = 16
        Private pFontSize As Single = 10.0F
        Private pRowHeight As Integer = 20
        Private pIndentWidth As Integer = 20
        Private pPlusMinusSize As Integer = 12
        Private pFontDescription As FontDescription
        Private pFontMetrics As Utilities.FontMetrics
        
        ' ===== Private Fields - Tree Data =====
        Private pRootNode As SyntaxNode
        Private pVisibleNodes As New List(Of VisualNode)
        Private pExpandedNodes As New HashSet(Of String)
        Private pSelectedNode As VisualNode
        Private pHoveredNode As VisualNode
        Private pNodeCache As New Dictionary(Of String, VisualNode)
        
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
        Private pLastClickNode As VisualNode
        Private pContextMenu As Menu
        
        ' ===== Private Fields - Settings =====
        Private pSortMode As ObjectExplorerSortMode = ObjectExplorerSortMode.eDefault
        Private pShowPrivateMembers As Boolean = True
        Private pShowInheritedMembers As Boolean = False
        Private pShowRegions As Boolean = False
        Private pTypeAheadBuffer As String = ""
        Private pTypeAheadTimer As UInteger = 0
        
        ' ===== Inner Classes =====
        
        ''' <summary>
        ''' Visual representation of a syntax node with layout information
        ''' </summary>
        Private Class VisualNode
            Public Property Node As SyntaxNode
            Public Property X As Integer
            Public Property Y As Integer
            Public Property Width As Integer
            Public Property Height As Integer
            Public Property Level As Integer
            Public Property IsExpanded As Boolean
            Public Property IsVisible As Boolean
            Public Property HasChildren As Boolean
            Public Property Parent As VisualNode
            Public Property Children As New List(Of VisualNode)
            Public Property NodePath As String ' Unique path for tracking
        End Class
        
        ''' <summary>
        ''' Sort modes for the Object Explorer
        ''' </summary>
        Public Enum ObjectExplorerSortMode
            eUnspecified
            eDefault           ' Natural code order
            eAlphabetic       ' Alphabetic by name
            eByType           ' Group by type (methods, properties, etc.)
            eByVisibility     ' Group by visibility (public, private, etc.)
            eLastValue
        End Enum
        
        ''' <summary>
        ''' Click zones for hit testing
        ''' </summary>
        Private Enum ClickZone
            eNone
            ePlusMinus
            eIcon
            eText
        End Enum


        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Initializes a new instance of the CustomDrawObjectExplorer class
        ''' </summary>
        ''' <param name="vSettingsManager">Settings manager for persistence</param>
        ''' <param name="vThemeManager">Theme manager for visual styling (optional)</param>
        Public Sub New(vSettingsManager As SettingsManager, Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New(Orientation.Vertical, 0)  ' Changed to Vertical to accommodate toolbar
            
            Try
                pSettingsManager = vSettingsManager
                pThemeManager = vThemeManager
                
                ' Load settings including unified scale
                LoadSettings()
                
                ' Create toolbar FIRST (new)
                CreateToolbar()
                
                ' Create UI components
                CreateUIComponents()
                
                ' Setup event handlers
                SetupEventHandlers()
                
                ' Initialize drawing
                InitializeDrawing()
                
                ' Create context menu
                CreateContextMenu()
                
                ShowAll()
                
                #If DEBUG Then
                Console.WriteLine($"CustomDrawObjectExplorer initialized with unified scale: {pCurrentScale}%")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawObjectExplorer constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== UI Component Creation =====
        
        ''' <summary>
        ''' Creates the UI components for the custom drawing area
        ''' </summary>
        Private Sub CreateUIComponents()
            Try
                ' Create main container box (vertical)
                Dim lMainBox As New Box(Orientation.Vertical, 0)
                
                ' Create horizontal container for drawing area and vertical scrollbar
                Dim lHorizontalBox As New Box(Orientation.Horizontal, 0)
                
                ' Create drawing area
                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                
                ' FIXED: Set events explicitly including scroll mask
                pDrawingArea.Events = EventMask.ExposureMask Or 
                                     EventMask.ButtonPressMask Or 
                                     EventMask.ButtonReleaseMask Or 
                                     EventMask.PointerMotionMask Or 
                                     EventMask.ScrollMask Or 
                                     EventMask.KeyPressMask Or 
                                     EventMask.KeyReleaseMask Or
                                     EventMask.EnterNotifyMask Or
                                     EventMask.LeaveNotifyMask
                
                ' CRITICAL: Add scroll events explicitly after setting Events property
                pDrawingArea.AddEvents(CInt(EventMask.ScrollMask))
                
                pDrawingArea.Expand = True
                
                ' Create vertical scrollbar
                pVScrollBar = New CustomDrawScrollbar(Orientation.Vertical)
                pVScrollBar.ThemeManager = pThemeManager

                ' Create horizontal scrollbar
                pHScrollBar = New CustomDrawScrollbar(Orientation.Horizontal)
                pHScrollBar.ThemeManager = pThemeManager
                
                ' Create corner box
                pCornerBox = New DrawingArea()
                pCornerBox.SetSizeRequest(20, 20) ' Match scrollbar width/height
                
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
                
                ' Add to main container
                PackStart(lMainBox, True, True, 0)
                
            Catch ex As Exception
                Console.WriteLine($"CreateUIComponents error: {ex.Message}")
            End Try
        End Sub

        
        ' ===== Drawing Initialization =====
        
        ''' <summary>
        ''' Initializes drawing settings and fonts
        ''' </summary>
        Private Sub InitializeDrawing()
            Try
                ' Apply scale settings (already loaded from unified Explorer.TextScale)
                ApplyScale(pCurrentScale)
                
                ' Initialize font with unified settings
                UpdateFontSettings()
                
                ' Update toolbar scale display
                UpdateScaleDisplay()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeDrawing error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== IObjectExplorer Implementation =====
        
        ''' <summary>
        ''' Updates the tree structure and clears selection after refresh
        ''' </summary>
        ''' <param name="vRootNode">The new root node of the syntax tree</param>
        ''' <remarks>
        ''' Now preserves expanded nodes but clears selection after refresh
        ''' to avoid confusion with stale selections
        ''' </remarks>
        Public Sub UpdateStructure(vRootNode As SyntaxNode) Implements IObjectExplorer.UpdateStructure
            Try
                #If DEBUG Then
                Console.WriteLine($"UpdateStructure called with root: {If(vRootNode?.Name, "Nothing")}")
                #End If
                
                ' Check if this is actually a change
                If vRootNode Is pRootNode Then
                    #If DEBUG Then
                    Console.WriteLine("UpdateStructure: Same root, skipping update")
                    #End If
                    Return
                End If
                
                ' [... rest of existing UpdateStructure code ...]
                
                ' Mark as needing rebuild instead of calling RebuildVisualTree directly
                pNeedsRebuild = True
                RebuildVisualTree()  ' This will now check if rebuild is actually needed
                
                ' [... rest of method ...]
            Catch ex As Exception
                Console.WriteLine($"UpdateStructure error: {ex.Message}")
            End Try
        End Sub

        Public Sub MarkTreeDirty()
            pNeedsRebuild = True
        End Sub

        ''' <summary>
        ''' Gets the current tree view status for debugging
        ''' </summary>
        ''' <returns>Status string describing the current state</returns>
        Public Function GetTreeViewStatus() As String
            Try
                Dim lStatus As New System.Text.StringBuilder()
                lStatus.AppendLine($"CustomDrawObjectExplorer Status:")
                lStatus.AppendLine($"  Root Node: {If(pRootNode IsNot Nothing, "Present", "None")}")
                lStatus.AppendLine($"  Visible Nodes: {pVisibleNodes.Count}")
                lStatus.AppendLine($"  Expanded Nodes: {pExpandedNodes.Count}")
                lStatus.AppendLine($"  Selected Node: {If(pSelectedNode?.Node?.Name, "None")}")
                lStatus.AppendLine($"  Scale: {pCurrentScale}%")
                lStatus.AppendLine($"  Viewport: {pViewportWidth}x{pViewportHeight}")
                lStatus.AppendLine($"  Content: {pContentWidth}x{pContentHeight}")
                lStatus.AppendLine($"  Scroll: ({pScrollX}, {pScrollY})")
                Return lStatus.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"GetTreeViewStatus error: {ex.Message}")
                Return "Error getting status"
            End Try
        End Function
        
        ''' <summary>
        ''' Forces a complete refresh of the tree structure
        ''' </summary>
        Public Sub ForceCompleteRefresh()
            Try
                ' Clear caches
                pNodeCache.Clear()
                pVisibleNodes.Clear()
                
                ' Rebuild from root
                If pRootNode IsNot Nothing Then
                    RebuildVisualTree()
                End If
                
                ' Update display
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ForceCompleteRefresh error: {ex.Message}")
            End Try
        End Sub
        

        ''' <summary>
        ''' Sets the project structure and handles deferred realization if needed
        ''' </summary>
        ''' <param name="vRootNode">Root node of the project structure</param>
        Public Sub SetProjectStructure(vRootNode As SyntaxNode) Implements IObjectExplorer.SetProjectStructure
            Try
                UpdateStructure(vRootNode)
                
            Catch ex As Exception
                Console.WriteLine($"SetProjectStructure error: {ex.Message}")
            End Try
        End Sub
        



        ''' <summary>
        ''' Attempts to recover structure if it was cleared inappropriately
        ''' </summary>
        Private Sub AttemptStructureRecovery()
            Try
                If pRootNode Is Nothing AndAlso pLastValidRootNode IsNot Nothing AndAlso pIsProjectLoaded Then
                    #If DEBUG Then
                    Console.WriteLine("Attempting to recover Object Explorer structure...")
                    #End If
                    
                    ' Restore the root
                    pRootNode = pLastValidRootNode
                    
                    ' Rebuild the visual tree
                    RebuildVisualTree()
                    
                    #If DEBUG Then
                    Console.WriteLine($"Structure recovered with {pVisibleNodes.Count} nodes")
                    #End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AttemptStructureRecovery error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Refreshes the current structure, repopulating the tree with existing data
        ''' </summary>
        Public Sub RefreshStructure() Implements IObjectExplorer.RefreshStructure
            Try
                If pRootNode IsNot Nothing Then
                    RebuildVisualTree()
                    UpdateScrollbars()
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RefreshStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the current editor for context awareness
        ''' </summary>
        ''' <param name="vEditor">The editor to associate with the Object Explorer</param>
        Public Sub SetCurrentEditor(vEditor As IEditor) Implements IObjectExplorer.SetCurrentEditor
            Try
                pCurrentEditor = vEditor
                ' Could highlight current method/class based on cursor position
                
            Catch ex As Exception
                Console.WriteLine($"SetCurrentEditor error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initializes the Object Explorer with ProjectManager for centralized parsing
        ''' </summary>
        ''' <param name="vProjectManager">The project manager instance to use</param>
        ''' <remarks>
        ''' Now subscribes to ProjectManager.ParseCompleted events to receive
        ''' updates from the centralized ProjectParser instead of local parsing
        ''' </remarks>
        Public Sub InitializeWithProjectManager(vProjectManager As ProjectManager) Implements IObjectExplorer.InitializeWithProjectManager
            Try
                pProjectManager = vProjectManager
                
                ' Subscribe to ParseCompleted event from ProjectManager
                If pProjectManager IsNot Nothing Then
                    RemoveHandler pProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                    AddHandler pProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                    
                    RemoveHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
                    AddHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
                    
                    #If DEBUG Then
                    Console.WriteLine("CustomDrawObjectExplorer subscribed to ProjectManager parse events")
                    #End If
                End If
                
                ' Load initial project structure if available
                Dim lProjectTree As SyntaxNode = pProjectManager?.GetProjectSyntaxTree()
                If lProjectTree IsNot Nothing Then
                    LoadProjectStructure(lProjectTree)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InitializeWithProjectManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Loads a solution's projects into the picker and displays the startup project's
        ''' symbol tree - Object Explorer only ever shows one project at a time, so this
        ''' populates a toolbar combo the user can switch between the solution's member
        ''' projects with, rather than attempting to merge multiple projects' symbol trees
        ''' into one display
        ''' </summary>
        ''' <param name="vSolutionManager">The loaded solution's SolutionManager</param>
        Public Sub SetSolutionManager(vSolutionManager As SolutionManager)
            Try
                pSolutionManager = vSolutionManager

                If vSolutionManager Is Nothing OrElse vSolutionManager.AllProjects.Count = 0 Then
                    pProjectItem.Visible = False
                    Return
                End If

                pProjectCombo.RemoveAll()
                for each lMember in vSolutionManager.AllProjects
                    pProjectCombo.AppendText(lMember.CurrentProjectName)
                Next

                pProjectItem.Visible = True

                Dim lStartupIndex As Integer = 0
                Dim lProjects As IReadOnlyList(Of ProjectManager) = vSolutionManager.AllProjects
                for i As Integer = 0 To lProjects.Count - 1
                    If lProjects(i) Is vSolutionManager.StartupProject Then
                        lStartupIndex = i
                        Exit for
                    End If
                Next

                ' Setting Active raises Changed, which calls InitializeWithProjectManager for
                ' the newly-selected project - no separate explicit call needed here
                pProjectCombo.Active = lStartupIndex

            Catch ex As Exception
                Console.WriteLine($"SetSolutionManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Navigates to and highlights a specific node in the tree
        ''' </summary>
        ''' <param name="vNode">The node to navigate to</param>
        Public Sub NavigateToNode(vNode As SyntaxNode) Implements IObjectExplorer.NavigateToNode
            Try
                If vNode Is Nothing Then Return
                
                ' Find visual node
                Dim lVisualNode As VisualNode = FindVisualNode(vNode)
                If lVisualNode Is Nothing Then Return
                
                ' Ensure parent nodes are expanded
                EnsureNodeVisible(lVisualNode)
                
                ' Select the node
                SelectNode(lVisualNode)
                
                ' Scroll to make visible
                ScrollToNode(lVisualNode)
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Enables or disables the refresh button
        ''' </summary>
        ''' <param name="vEnabled">True to enable, False to disable</param>
        Public Sub SetRefreshEnabled(vEnabled As Boolean) Implements IObjectExplorer.SetRefreshEnabled
            Try
                ' This would enable/disable refresh action in toolbar/menu
                ' For now, just store state
                
            Catch ex As Exception
                Console.WriteLine($"SetRefreshEnabled error: {ex.Message}")
            End Try
        End Sub
        
        
        ''' <summary>
        ''' Called when the Object Explorer page is activated in the notebook
        ''' </summary>
        Public Sub OnPageActivated() Implements IObjectExplorer.OnPageActivated
            Try
                #If DEBUG Then
                Console.WriteLine("CustomDrawObjectExplorer.OnPageActivated called")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Initial state: Root=" + If(pRootNode IsNot Nothing, "Present", "Nothing") + ", LastValid=" + If(pLastValidRootNode IsNot Nothing, "Present", "Nothing") + ", IsProjectLoaded=" + pIsProjectLoaded.ToString)
                #End If
                
                ' Apply theme (should not affect tree structure)
                'CustomDrawObjectExplorer.ApplyTheme: Applied theme To Object Explorer: {pThemeManager?.GetCurrentTheme()}")
                ApplyTheme()
                
                ' Check if we need to restore from last valid state
                If pRootNode Is Nothing AndAlso pLastValidRootNode IsNot Nothing Then
                    #If DEBUG Then
                    Console.WriteLine("OnPageActivated: Restoring from last valid root")
                    #End If
                    pRootNode = pLastValidRootNode
                    pIsProjectLoaded = True
                    pNeedsRebuild = True  ' Mark for rebuild since we restored root
                End If
                
                ' Only rebuild if actually needed
                If IsRebuildNeeded() Then
                    #If DEBUG Then
                    Console.WriteLine("OnPageActivated: Rebuilding visual tree...")
                    #End If
                    RebuildVisualTree()
                Else
                    #If DEBUG Then
                    Console.WriteLine("OnPageActivated: Visual tree Is current, skipping rebuild")
                    #End If
                End If
                
                ' Always ensure drawing area is refreshed
                pDrawingArea?.QueueDraw()
                
                #If DEBUG Then
                Console.WriteLine($"OnPageActivated: Complete with {pVisibleNodes.Count} nodes")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Final state: Root=" + If(pRootNode IsNot Nothing, "Present", "Nothing") + ", LastValid=" + If(pLastValidRootNode IsNot Nothing, "Present", "Nothing") + ", IsProjectLoaded=" + pIsProjectLoaded.ToString)
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"OnPageActivated error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Diagnostic Methods (Debug builds only) =====
        
        ''' <summary>
        ''' Performs comprehensive diagnostic check of TreeView status
        ''' </summary>
        Public Sub DiagnoseTreeViewStatus() Implements IObjectExplorer.DiagnoseTreeViewStatus
            Try
                #If DEBUG Then
                Console.WriteLine(GetTreeViewStatus())
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"DiagnoseTreeViewStatus error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Tests TreeView visibility with sample data
        ''' </summary>
        Public Sub TestTreeViewVisibility() Implements IObjectExplorer.TestTreeViewVisibility
            Try
                ' Create test structure
                Dim lTestRoot As New SyntaxNode(CodeNodeType.eNamespace, "TestNamespace")
                Dim lTestClass As New SyntaxNode(CodeNodeType.eClass, "TestClass")
                lTestClass.IsPublic = True
                
                Dim lTestMethod As New SyntaxNode(CodeNodeType.eMethod, "TestMethod")
                lTestMethod.IsPublic = True
                lTestClass.AddChild(lTestMethod)
                
                lTestRoot.AddChild(lTestClass)
                
                UpdateStructure(lTestRoot)
                
            Catch ex As Exception
                Console.WriteLine($"TestTreeViewVisibility error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Adds test content to verify TreeView functionality
        ''' </summary>
        Public Sub TestWithSimpleContent() Implements IObjectExplorer.TestWithSimpleContent
            TestTreeViewVisibility()
        End Sub
        
        ''' <summary>
        ''' Diagnoses issues using a diagnostic label
        ''' </summary>
        Public Sub DiagnoseWithLabel() Implements IObjectExplorer.DiagnoseWithLabel
            DiagnoseTreeViewStatus()
        End Sub
        
        ''' <summary>
        ''' Checks and reports the current TreeView content
        ''' </summary>
        Public Sub CheckTreeViewContent() Implements IObjectExplorer.CheckTreeViewContent
            Try
                #If DEBUG Then
                Console.WriteLine($"TreeView Content Check:")
                #End If
                #If DEBUG Then
                Console.WriteLine($"  Total visible nodes: {pVisibleNodes.Count}")
                #End If
                
                for i As Integer = 0 To Math.Min(10, pVisibleNodes.Count - 1)
                    Dim lNode As VisualNode = pVisibleNodes(i)
                    #If DEBUG Then
                    Console.WriteLine($"  [{i}] {New String(" "c, lNode.Level * 2)}{lNode.Node.Name} ({lNode.Node.NodeType})")
                    #End If
                Next
                
                If pVisibleNodes.Count > 10 Then
                    #If DEBUG Then
                    Console.WriteLine($"  ... and {pVisibleNodes.Count - 10} more nodes")
                    #End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CheckTreeViewContent error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Updates scrollbar ranges based on content size
        ''' </summary>
        Private Sub UpdateScrollbars()
            Try
                ' Update horizontal scrollbar
                pHScrollBar.Adjustment.Lower = 0
                pHScrollBar.Adjustment.Upper = Math.Max(pContentWidth, pViewportWidth)
                pHScrollBar.Adjustment.PageSize = pViewportWidth
                pHScrollBar.Adjustment.StepIncrement = 20
                pHScrollBar.Adjustment.PageIncrement = pViewportWidth
                
                ' Update vertical scrollbar
                pVScrollBar.Adjustment.Lower = 0
                pVScrollBar.Adjustment.Upper = Math.Max(pContentHeight, pViewportHeight)
                pVScrollBar.Adjustment.PageSize = pViewportHeight
                pVScrollBar.Adjustment.StepIncrement = pRowHeight
                pVScrollBar.Adjustment.PageIncrement = pViewportHeight
                
                ' Ensure scroll positions are valid
                pScrollX = Math.Min(pScrollX, CInt(pHScrollBar.Adjustment.Upper - pHScrollBar.Adjustment.PageSize))
                pScrollY = Math.Min(pScrollY, CInt(pVScrollBar.Adjustment.Upper - pVScrollBar.Adjustment.PageSize))
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScrollbars error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Loads project structure from centralized parser while preserving UI state
        ''' </summary>
        ''' <param name="vProjectSyntaxTree">Complete project syntax tree from ProjectManager</param>
        ''' <remarks>
        ''' Preserves expanded nodes and selection across reloads when node paths match
        ''' </remarks>
        Public Sub LoadProjectStructure(vProjectSyntaxTree As SyntaxNode) Implements IObjectExplorer.LoadProjectStructure
            Try
                #If DEBUG Then
                Console.WriteLine($"LoadProjectStructure called with tree: {If(vProjectSyntaxTree?.Name, "Nothing")}")
                #End If
                
                ' Store current UI state before loading new structure
                Dim lPreviousExpandedPaths As New HashSet(Of String)(pExpandedNodes)
                Dim lPreviousSelectedPath As String = pSelectedNode?.NodePath
                Dim lPreviousScrollX As Double = pScrollX
                Dim lPreviousScrollY As Double = pScrollY
                
                #If DEBUG Then
                Console.WriteLine($"  Preserving UI state: {lPreviousExpandedPaths.Count} expanded paths")
                #End If
                
                If vProjectSyntaxTree Is Nothing Then
                    #If DEBUG Then
                    Console.WriteLine("LoadProjectStructure: No tree provided - clearing")
                    #End If
                    ClearStructure()
                    Return
                End If
                
                ' Store the project tree
                pRootNode = vProjectSyntaxTree
                pLastValidRootNode = vProjectSyntaxTree
                pIsProjectLoaded = True
                
                #If DEBUG Then
                Console.WriteLine($"  Loaded project with {vProjectSyntaxTree.Children.Count} root children")
                #End If
                
                ' Preserve expanded state for nodes that still exist
                pExpandedNodes = lPreviousExpandedPaths
                
                ' Always expand root namespace if single namespace pattern - root may be
                ' eDocument (old custom parser) or eProject (Roslyn-based ProjectParser)
                If (vProjectSyntaxTree.NodeType = CodeNodeType.eDocument OrElse
                    vProjectSyntaxTree.NodeType = CodeNodeType.eProject) AndAlso
                   vProjectSyntaxTree.Children.Count = 1 Then
                    Dim lFirstChild As SyntaxNode = vProjectSyntaxTree.Children(0)
                    If lFirstChild.NodeType = CodeNodeType.eNamespace Then
                        pExpandedNodes.Add(lFirstChild.Name)
                        #If DEBUG Then
                        Console.WriteLine($"  Auto-expanded root namespace: {lFirstChild.Name}")
                        #End If
                    End If
                End If
                
                ' Clear and rebuild visual representation
                pNodeCache.Clear()
                pVisibleNodes.Clear()
                RebuildVisualTree()
                
                ' Attempt to restore selection
                If Not String.IsNullOrEmpty(lPreviousSelectedPath) Then
                    Dim lNodeToSelect As VisualNode = pVisibleNodes.FirstOrDefault(
                        Function(n) n.NodePath = lPreviousSelectedPath)
                    
                    If lNodeToSelect IsNot Nothing Then
                        pSelectedNode = lNodeToSelect
                        #If DEBUG Then
                        Console.WriteLine($"  Selection restored to: {lNodeToSelect.Node.Name}")
                        #End If
                    End If
                End If
                
                ' Restore scroll position within valid bounds
                pScrollX = Math.Max(0, Math.Min(lPreviousScrollX, Math.Max(0, pContentWidth - pViewportWidth)))
                pScrollY = Math.Max(0, Math.Min(lPreviousScrollY, Math.Max(0, pContentHeight - pViewportHeight)))
                
                ' Update display
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                
                #If DEBUG Then
                Console.WriteLine($"LoadProjectStructure complete: {pVisibleNodes.Count} visible nodes")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadProjectStructure error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the theme manager for the Object Explorer
        ''' </summary>
        ''' <param name="vThemeManager">The theme manager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager

                ' The search entry is custom-drawn and isn't driven by native GTK CSS
                ' theming the way the old SearchEntry was, so it needs an explicit
                ' re-application here to pick up a live theme switch
                If pSearchEntry IsNot Nothing Then
                    pSearchEntry.ThemeManager = vThemeManager
                End If

                ' Custom-drawn scrollbars similarly need explicit re-theming
                If pVScrollBar IsNot Nothing Then pVScrollBar.ThemeManager = vThemeManager
                If pHScrollBar IsNot Nothing Then pHScrollBar.ThemeManager = vThemeManager
                If pScaleCombo IsNot Nothing Then pScaleCombo.ThemeManager = vThemeManager

                ' Refresh display with new theme
                RefreshTheme()
                
                #If DEBUG Then
                Console.WriteLine("ObjectExplorer ThemeManager set")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"SetThemeManager error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Refreshes the Object Explorer with current theme
        ''' </summary>
        Public Sub RefreshTheme()
            Try
                ' Force a complete redraw with current theme
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"RefreshTheme error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Shows a loading state message in the Object Explorer
        ''' </summary>
        Public Sub ShowLoadingState()
            Try
                ' Clear existing structure
                ClearStructure()
                
                ' Create a temporary "Loading..." syntax node
                Dim lLoadingSyntaxNode As New SyntaxNode() with {
                    .Name = "Loading project structure...",
                    .NodeType = CodeNodeType.eUnspecified,
                    .StartLine = 0,
                    .EndLine = 0
                }
                
                ' Create a visual node for it
                Dim lLoadingNode As New VisualNode() with {
                    .Node = lLoadingSyntaxNode,
                    .Level = 0,
                    .Y = 0,
                    .IsExpanded = False,
                    .HasChildren = False,
                    .NodePath = "Loading"
                }
                
                ' Add to visible nodes
                pVisibleNodes.Clear()
                pVisibleNodes.Add(lLoadingNode)
                
                ' Update display
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ShowLoadingState error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Hides the loading state message
        ''' </summary>
        Public Sub HideLoadingState()
            Try
                ' The loading state will be replaced when the actual tree is loaded
                ' This method is here for explicit clearing if needed
                If pVisibleNodes.Count = 1 AndAlso 
                   pVisibleNodes(0).Node IsNot Nothing AndAlso
                   pVisibleNodes(0).Node.Name = "Loading project structure..." Then
                    ClearStructure()
                End If
            Catch ex As Exception
                Console.WriteLine($"HideLoadingState error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clears all structure from the Object Explorer
        ''' </summary>
        Public Sub ClearStructure() Implements IObjectExplorer.ClearStructure
            Try
                pRootNode = Nothing
                pVisibleNodes.Clear()
                pExpandedNodes.Clear()
                pSelectedNode = Nothing
                pHoveredNode = Nothing
                pNodeCache.Clear()
                
                ' Clear search if active
                If pSearchEntry IsNot Nothing Then
                    pSearchEntry.Text = ""
                End If
                
                ' Redraw empty area
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ClearStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Refreshes the view after async parsing completes
        ''' </summary>
        Public Sub RefreshView()
            Try
                ' If we have a tree loaded, rebuild the visual representation
                If pRootNode IsNot Nothing Then
                    RebuildVisualTree()
                End If
            Catch ex As Exception
                Console.WriteLine($"RefreshView error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Clears the current project from the explorer
        ''' </summary>
        Public Sub ClearProject()
            Try
                ' Clear the tree
                pRootNode = Nothing
                pVisibleNodes.Clear()
                pNodeCache.Clear()
                
                ' Reset selection
                pSelectedNode = Nothing
                pHoveredNode = Nothing
                
                ' Clear expanded state
                pExpandedNodes.Clear()
                
                ' Reset scroll position
                pScrollY = 0
                
                ' Redraw
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ClearProject error: {ex.Message}")
            End Try
        End Sub
       
    End Class
    
End Namespace
