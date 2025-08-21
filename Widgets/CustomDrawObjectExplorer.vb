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
        Private pNeedsRebuild As Boolean = False  

        ' ===== Private Fields - State Preservation =====
        Private pLastValidRootNode As SyntaxNode  ' Store last valid root to recover from clears
        Private pIsProjectLoaded As Boolean = False  ' Track if a project is loaded
        
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
        Public Event NavigateToFile(vFilePath As String, vLine As Integer, vColumn As Integer) Implements IObjectExplorer.NavigateToFile
        
        ''' <summary>
        ''' Raised when the user requests to close the Object Explorer
        ''' </summary>
        Public Event CloseRequested() Implements IObjectExplorer.CloseRequested
        
        ' ===== Private Fields - UI Components =====
        Private pDrawingArea As DrawingArea
        Private pHScrollBar As Scrollbar
        Private pVScrollBar As Scrollbar
        Private pCornerBox As DrawingArea
        Private pSettingsManager As SettingsManager
        Private pProjectManager As ProjectManager
        Private pCurrentEditor As IEditor
        
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
                
                Console.WriteLine($"CustomDrawObjectExplorer initialized with unified scale: {pCurrentScale}%")
                
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
                pVScrollBar = New Scrollbar(Orientation.Vertical, Nothing)
                
                ' Create horizontal scrollbar
                pHScrollBar = New Scrollbar(Orientation.Horizontal, Nothing)
                
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
'        Private Sub CreateUIComponents()
'            Try
'                ' Create main container box (vertical)
'                Dim lMainBox As New Box(Orientation.Vertical, 0)
'                
'                ' Create horizontal container for drawing area and vertical scrollbar
'                Dim lHorizontalBox As New Box(Orientation.Horizontal, 0)
'                
'                ' Create drawing area
'                pDrawingArea = New DrawingArea()
'                pDrawingArea.CanFocus = True
'                pDrawingArea.Events = EventMask.AllEventsMask
'                pDrawingArea.Expand = True
'                
'                ' Create vertical scrollbar
'                pVScrollBar = New Scrollbar(Orientation.Vertical, Nothing)
'                
'                ' Create horizontal scrollbar
'                pHScrollBar = New Scrollbar(Orientation.Horizontal, Nothing)
'                
'                ' Create corner box
'                pCornerBox = New DrawingArea()
'                pCornerBox.SetSizeRequest(20, 20) ' Match scrollbar width/height
'                
'                ' Pack horizontal box
'                lHorizontalBox.PackStart(pDrawingArea, True, True, 0)
'                lHorizontalBox.PackStart(pVScrollBar, False, False, 0)
'                
'                ' Create bottom box for horizontal scrollbar and corner
'                Dim lBottomBox As New Box(Orientation.Horizontal, 0)
'                lBottomBox.PackStart(pHScrollBar, True, True, 0)
'                lBottomBox.PackStart(pCornerBox, False, False, 0)
'                
'                ' Pack main box
'                lMainBox.PackStart(lHorizontalBox, True, True, 0)
'                lMainBox.PackStart(lBottomBox, False, False, 0)
'                
'                ' Add to main container
'                PackStart(lMainBox, True, True, 0)
'                
'            Catch ex As Exception
'                Console.WriteLine($"CreateUIComponents error: {ex.Message}")
'            End Try
'        End Sub
        
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
        ''' Updates the complete object hierarchy displayed in the explorer
        ''' </summary>
        ''' <param name="vRootNode">Root syntax node containing the complete hierarchy</param>
        Public Sub UpdateStructure(vRootNode As SyntaxNode) Implements IObjectExplorer.UpdateStructure
            Try
                Console.WriteLine($"UpdateStructure called with root: {If(vRootNode?.Name, "Nothing")}")
                
                If vRootNode Is Nothing Then
                    Console.WriteLine("UpdateStructure: Received Nothing root - clearing")
                    ClearStructure()
                    Return
                End If
                
                ' Store the new root
                pRootNode = vRootNode
                
                ' IMPORTANT: Also update the last valid root and project loaded flag
                pLastValidRootNode = vRootNode
                pIsProjectLoaded = True
                
                Console.WriteLine($"UpdateStructure: Set pRootNode, pLastValidRootNode, and pIsProjectLoaded=True")
                
                ' Auto-expand root namespace if it's the only child
                If vRootNode.NodeType = CodeNodeType.eDocument AndAlso vRootNode.Children.Count = 1 Then
                    Dim lFirstChild As SyntaxNode = vRootNode.Children(0)
                    If lFirstChild.NodeType = CodeNodeType.eNamespace Then
                        pExpandedNodes.Add(lFirstChild.Name)
                        Console.WriteLine($"Auto-expanded single root namespace: {lFirstChild.Name}")
                    End If
                End If
                
                ' Rebuild the visual tree
                RebuildVisualTree()
                
                ' Update scrollbars
                UpdateScrollbars()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"UpdateStructure complete: {pVisibleNodes.Count} visible nodes")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateStructure error: {ex.Message}")
            End Try
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
                
Console.WriteLine($"ForceCompleteRefresh  pVisibleNodesClear()")
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
        ''' Updated ClearStructure to allow recovery and show caller
        ''' </summary>
        Public Sub ClearStructure() Implements IObjectExplorer.ClearStructure
            Try
                ' DEBUG: Show stack trace to find who's calling this
                Console.WriteLine("=====================================")
                Console.WriteLine("ClearStructure called - STACK TRACE:")
                Console.WriteLine(Environment.StackTrace)
                Console.WriteLine("=====================================")
                
                Console.WriteLine("ClearStructure called - preserving last valid root for recovery")
                
                ' Save state before clearing
                If pRootNode IsNot Nothing Then
                    pLastValidRootNode = pRootNode
                    pIsProjectLoaded = True
                    Console.WriteLine($"ClearStructure: Saved root node with {pRootNode.Children.Count} children")
                End If
                
                ' Clear current display but keep last valid root for recovery
                pRootNode = Nothing
                Console.WriteLine($"ClearStructure  pVisibleNodesClear()")
                
                pVisibleNodes.Clear()
                pExpandedNodes.Clear()
                pNodeCache.Clear()
                pSelectedNode = Nothing
                pHoveredNode = Nothing
                
                ' Don't clear pLastValidRootNode or pIsProjectLoaded
                ' This allows recovery if needed
                
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ClearStructure error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Attempts to recover structure if it was cleared inappropriately
        ''' </summary>
        Private Sub AttemptStructureRecovery()
            Try
                If pRootNode Is Nothing AndAlso pLastValidRootNode IsNot Nothing AndAlso pIsProjectLoaded Then
                    Console.WriteLine("Attempting to recover Object Explorer structure...")
                    
                    ' Restore the root
                    pRootNode = pLastValidRootNode
                    
                    ' Rebuild the visual tree
                    RebuildVisualTree()
                    
                    Console.WriteLine($"Structure recovered with {pVisibleNodes.Count} nodes")
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
        ''' Initializes the Object Explorer with a project manager for centralized parsing
        ''' </summary>
        ''' <param name="vProjectManager">The project manager instance to use</param>
        Public Sub InitializeWithProjectManager(vProjectManager As ProjectManager) Implements IObjectExplorer.InitializeWithProjectManager
            Try
                pProjectManager = vProjectManager
                
                ' CRITICAL FIX: Use GetProjectSyntaxTree() method instead of property
                Dim lProjectTree As SyntaxNode = pProjectManager?.GetProjectSyntaxTree()
                If lProjectTree IsNot Nothing Then
                    LoadProjectStructure(lProjectTree)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InitializeWithProjectManager error: {ex.Message}")
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
                Console.WriteLine("CustomDrawObjectExplorer.OnPageActivated called")
                Console.WriteLine($"  Initial state: Root={If(pRootNode IsNot Nothing, "Present", "Nothing")}, LastValid={If(pLastValidRootNode IsNot Nothing, "Present", "Nothing")}, IsProjectLoaded={pIsProjectLoaded}")
                
                ' Ensure drawing area is realized
                If pDrawingArea IsNot Nothing AndAlso Not pDrawingArea.IsRealized Then
                    pDrawingArea.Realize()
                End If
                
                ' Apply theme (should not affect tree structure)
                ApplyTheme()
                
                ' Check if we need to restore or rebuild
                If pRootNode Is Nothing Then
                    ' Try to recover from last valid state
                    If pLastValidRootNode IsNot Nothing Then
                        Console.WriteLine("OnPageActivated: Restoring from last valid root")
                        pRootNode = pLastValidRootNode
                        pIsProjectLoaded = True
                    Else
                        Console.WriteLine("OnPageActivated: No structure to display")
                        pDrawingArea?.QueueDraw()
                        Return
                    End If
                End If
                
                ' Ensure visual tree is built
                If pRootNode IsNot Nothing Then
                    ' Always rebuild to ensure consistency
                    Console.WriteLine("OnPageActivated: Ensuring visual tree is built...")
                    
                    ' Save current state
                    Dim lSavedRoot As SyntaxNode = pRootNode
                    Dim lSavedLastValid As SyntaxNode = pLastValidRootNode
                    Dim lSavedIsLoaded As Boolean = pIsProjectLoaded
                    
                    ' Rebuild the tree
                    RebuildVisualTree()
                    
                    ' Restore state if it was lost during rebuild
                    If pRootNode Is Nothing Then
                        pRootNode = lSavedRoot
                    End If
                    If pLastValidRootNode Is Nothing Then
                        pLastValidRootNode = lSavedLastValid
                    End If
                    If Not pIsProjectLoaded Then
                        pIsProjectLoaded = lSavedIsLoaded
                    End If
                    
                    ' Update positions
                    CalculateNodePositions()
                    UpdateScrollbars()
                End If
                
                ' Force a redraw
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"OnPageActivated: Complete with {pVisibleNodes.Count} nodes")
                Console.WriteLine($"  Final state: Root={If(pRootNode IsNot Nothing, "Present", "Nothing")}, LastValid={If(pLastValidRootNode IsNot Nothing, "Present", "Nothing")}, IsProjectLoaded={pIsProjectLoaded}")
                
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
                Console.WriteLine(GetTreeViewStatus())
                
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
                Console.WriteLine($"TreeView Content Check:")
                Console.WriteLine($"  Total visible nodes: {pVisibleNodes.Count}")
                
                For i As Integer = 0 To Math.Min(10, pVisibleNodes.Count - 1)
                    Dim lNode As VisualNode = pVisibleNodes(i)
                    Console.WriteLine($"  [{i}] {New String(" "c, lNode.Level * 2)}{lNode.Node.Name} ({lNode.Node.NodeType})")
                Next
                
                If pVisibleNodes.Count > 10 Then
                    Console.WriteLine($"  ... and {pVisibleNodes.Count - 10} more nodes")
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
        ''' Updated LoadProjectStructure to preserve state
        ''' </summary>
        Public Sub LoadProjectStructure(vProjectSyntaxTree As SyntaxNode) Implements IObjectExplorer.LoadProjectStructure
            Try
                Console.WriteLine($"LoadProjectStructure called with tree: {If(vProjectSyntaxTree IsNot Nothing, vProjectSyntaxTree.Name, "Nothing")}")
                
                ' Store as last valid root for recovery
                If vProjectSyntaxTree IsNot Nothing Then
                    pLastValidRootNode = vProjectSyntaxTree
                    pIsProjectLoaded = True
                End If
                
                ' Ensure list exists
                EnsureVisibleNodesList()
                
                ' Update structure
                UpdateStructure(vProjectSyntaxTree)
                
                ' Verify state after load
                Console.WriteLine($"LoadProjectStructure complete: {pVisibleNodes.Count} nodes visible")
                
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
                
                ' Refresh display with new theme
                RefreshTheme()
                
                Console.WriteLine("ObjectExplorer ThemeManager set")
                
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

       
    End Class
    
End Namespace
