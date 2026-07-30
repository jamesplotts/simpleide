' Interfaces/IObjectExplorer.vb - Interface for the ObjectExplorer widget
' Created: 2025-08-16
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities


Namespace Interfaces
    
    ''' <summary>
    ''' Interface for the Object Explorer widget that displays code structure hierarchy
    ''' </summary>
    ''' <remarks>
    ''' This interface defines the contract for Object Explorer implementations,
    ''' providing methods for displaying and navigating code structure trees
    ''' </remarks>
    Public Interface IObjectExplorer
        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when a node is selected (single-click) in the tree
        ''' </summary>
        ''' <param name="vNode">The selected syntax node</param>
        Event NodeSelected(vNode As SyntaxNode)
        
        ''' <summary>
        ''' Raised when a node is double-clicked in the tree
        ''' </summary>
        ''' <param name="vNode">The double-clicked syntax node</param>
        Event NodeDoubleClicked(vNode As SyntaxNode)
        
        ''' <summary>
        ''' Raised when a node is activated (typically via double-click or enter key)
        ''' </summary>
        ''' <param name="vNode">The activated syntax node</param>
        Event NodeActivated(vNode As SyntaxNode)
        
        ''' <summary>
        ''' Raised when the user requests to navigate to a file location
        ''' </summary>
        ''' <param name="vFilePath">Full path to the file to navigate to</param>
        ''' <param name="vPosition">Position to navigate to (1-based for user display)</param>
        Event NavigateToFile(vFilePath As String, vPosition As EditorPosition)
         
        ''' <summary>
        ''' Raised when the user requests to close the Object Explorer
        ''' </summary>
        Event CloseRequested()
        
        ' ===== Enumerations =====
        
        ''' <summary>
        ''' Specifies the sort mode for displaying nodes in the tree
        ''' </summary>
        Enum SortMode
            ''' <summary>Unknown or unspecified sort mode</summary>
            eUnspecified
            ''' <summary>Sort by declaration order in source files</summary>
            eDeclarationOrder
            ''' <summary>Sort alphabetically by node name</summary>
            eAlphabetical
            ''' <summary>Sentinel value for enum bounds checking</summary>
            eLastValue
        End Enum
        
        ' ===== Core Structure Methods =====
        
        ''' <summary>
        ''' Updates the complete object hierarchy displayed in the explorer
        ''' </summary>
        ''' <param name="vRootNode">Root syntax node containing the complete hierarchy</param>
        ''' <remarks>
        ''' This method replaces the entire tree structure with the provided hierarchy
        ''' </remarks>
        Sub UpdateStructure(vRootNode As SyntaxNode)
        
        ''' <summary>
        ''' Loads project structure from centralized parser
        ''' </summary>
        ''' <param name="vProjectSyntaxTree">Complete project syntax tree from ProjectManager</param>
        ''' <remarks>
        ''' Used when integrating with the centralized parsing system
        ''' </remarks>
        Sub LoadProjectStructure(vProjectSyntaxTree As SyntaxNode)
        
        ''' <summary>
        ''' Sets the project structure and handles deferred realization if needed
        ''' </summary>
        ''' <param name="vRootNode">Root node of the project structure</param>
        ''' <remarks>
        ''' This method handles cases where the widget may not be fully realized yet
        ''' </remarks>
        Sub SetProjectStructure(vRootNode As SyntaxNode)
        
        ''' <summary>
        ''' Clears the structure completely, removing all nodes from the tree
        ''' </summary>
        Sub ClearStructure()
        
        ''' <summary>
        ''' Refreshes the current structure, repopulating the tree with existing data
        ''' </summary>
        Sub RefreshStructure()
        
        ' ===== Editor Integration Methods =====
        
        ''' <summary>
        ''' Sets the current editor for context awareness
        ''' </summary>
        ''' <param name="vEditor">The editor to associate with the Object Explorer</param>
        ''' <remarks>
        ''' Hooks up event handlers but doesn't change the displayed tree structure
        ''' </remarks>
        Sub SetCurrentEditor(vEditor As IEditor)
        
        ' ===== Project Manager Integration =====
        
        ''' <summary>
        ''' Initializes the Object Explorer with a ProjectManager for centralized parsing
        ''' </summary>
        ''' <param name="vProjectManager">The project manager instance to use</param>
        ''' <remarks>
        ''' Sets up event handlers for project structure changes
        ''' </remarks>
        Sub InitializeWithProjectManager(vProjectManager As ProjectManager)
        
        ' ===== Navigation Methods =====
        
        ''' <summary>
        ''' Navigates to and highlights a specific node in the tree
        ''' </summary>
        ''' <param name="vNode">The node to navigate to</param>
        ''' <remarks>
        ''' Expands parent nodes as needed and scrolls the node into view
        ''' </remarks>
        Sub NavigateToNode(vNode As SyntaxNode)
        
        ' ===== UI Control Methods =====
        
        ''' <summary>
        ''' Enables or disables the refresh button
        ''' </summary>
        ''' <param name="vEnabled">True to enable, False to disable</param>
        Sub SetRefreshEnabled(vEnabled As Boolean)
        
        ''' <summary>
        ''' Called when the notebook page containing this widget becomes active
        ''' </summary>
        ''' <remarks>
        ''' Handles widget realization and displays any pending data
        ''' </remarks>
        Sub OnPageActivated()
        
        ' ===== Diagnostic Methods (Debug builds only) =====
        
        ''' <summary>
        ''' Performs comprehensive diagnostic check of TreeView status
        ''' </summary>
        ''' <remarks>
        ''' Used for debugging visibility and rendering issues
        ''' </remarks>
        Sub DiagnoseTreeViewStatus()
        
        ''' <summary>
        ''' Tests TreeView visibility with sample data
        ''' </summary>
        ''' <remarks>
        ''' Used for debugging to verify the TreeView can display content
        ''' </remarks>
        Sub TestTreeViewVisibility()
        
        ''' <summary>
        ''' Adds test content to verify TreeView functionality
        ''' </summary>
        ''' <remarks>
        ''' Creates simple test nodes to verify rendering
        ''' </remarks>
        Sub TestWithSimpleContent()
        
        ''' <summary>
        ''' Diagnoses issues using a diagnostic label
        ''' </summary>
        ''' <remarks>
        ''' Shows diagnostic information directly in the UI
        ''' </remarks>
        Sub DiagnoseWithLabel()
        
        ''' <summary>
        ''' Checks and reports the current TreeView content
        ''' </summary>
        ''' <remarks>
        ''' Logs information about items currently in the tree
        ''' </remarks>
        Sub CheckTreeViewContent()
        
    End Interface
    
End Namespace
