' ICustomNotebook.vb - Interface for custom notebook compatibility
Imports Gtk
Imports System
Imports SimpleIDE.Models

Namespace Interfaces
    
    ''' <summary>
    ''' Interface for custom notebook widget to maintain compatibility with GTK Notebook
    ''' </summary>
    Public Interface ICustomNotebook
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets or sets the current page index
        ''' </summary>
        Property CurrentPage As Integer
        
        ''' <summary>
        ''' Gets the number of pages in the notebook
        ''' </summary>
        ReadOnly Property NPages As Integer
        
        ' ===== Core Methods (GTK Notebook Compatible) =====
        
        ''' <summary>
        ''' Appends a page to the notebook with text label
        ''' </summary>
        ''' <param name="vWidget">Widget to display in the page</param>
        ''' <param name="vLabel">Text label for the tab</param>
        ''' <param name="vIconName">Optional icon name for the tab</param>
        ''' <returns>Index of the new page</returns>
        ''' <remarks>
        ''' This overload is the preferred method for CustomDrawNotebook
        ''' </remarks>
        Overloads Function AppendPage(vWidget As Widget, vLabel As String, Optional vIconName As String = Nothing) As Integer        

        ''' <summary>
        ''' Removes a page from the notebook
        ''' </summary>
        ''' <param name="vPageNum">Index of the page to remove</param>
        Sub RemovePage(vPageNum As Integer)
        
        ''' <summary>
        ''' Gets the widget at the specified page
        ''' </summary>
        ''' <param name="vPageNum">Page index</param>
        ''' <returns>Widget at the specified page</returns>
        Function GetNthPage(vPageNum As Integer) As Widget
        
        ''' <summary>
        ''' Gets the tab label widget for a page
        ''' </summary>
        ''' <param name="vChild">Child widget of the page</param>
        ''' <returns>Tab label widget</returns>
        Function GetTabLabel(vPageNum As Integer) As String
        
        ''' <summary>
        ''' Sets the tab label for a page
        ''' </summary>
        ''' <param name="vChild">Child widget of the page</param>
        ''' <param name="vTabLabel">New tab label widget</param>
        Sub SetTabLabel(vPageNum As Integer, vTabText As String)
        
        ''' <summary>
        ''' Sets the tab label text for a page
        ''' </summary>
        ''' <param name="vChild">Child widget of the page</param>
        ''' <param name="vTabText">New tab label text</param>
        Sub SetTabLabelText(vChild As Widget, vTabText As String)
        
        ''' <summary>
        ''' Gets the page number of a widget
        ''' </summary>
        ''' <param name="vChild">Child widget to find</param>
        ''' <returns>Page index or -1 if not found</returns>
        Function PageNum(vChild As Widget) As Integer
        
        ' ===== Extended Methods =====
        
        ''' <summary>
        ''' Sets the modified state for a tab
        ''' </summary>
        ''' <param name="vPageNum">Page index</param>
        ''' <param name="vModified">Modified state</param>
        Sub SetTabModified(vPageNum As Integer, vModified As Boolean)
        
        ''' <summary>
        ''' Sets the icon for a tab
        ''' </summary>
        ''' <param name="vPageNum">Page index</param>
        ''' <param name="vIconName">Icon name</param>
        Sub SetTabIcon(vPageNum As Integer, vIconName As String)
        
        ''' <summary>
        ''' Shows the context menu for a tab
        ''' </summary>
        ''' <param name="vPageNum">Page index</param>
        ''' <param name="vX">X coordinate</param>
        ''' <param name="vY">Y coordinate</param>
        Sub ShowTabContextMenu(vPageNum As Integer, vX As Double, vY As Double)
        
        ''' <summary>
        ''' Switches to the next tab
        ''' </summary>
        Sub SwitchToNextTab()
        
        ''' <summary>
        ''' Switches to the previous tab
        ''' </summary>
        Sub SwitchToPreviousTab()
        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised before a tab is closed, allowing cancellation or custom handling
        ''' </summary>
        ''' <remarks>
        ''' Uses TabClosingEventArgs to allow Cancel or Handled options
        ''' </remarks>
        Event TabClosing(vSender As Object, vArgs As TabClosingEventArgs)        
        
        ''' <summary>
        ''' Raised when the current tab changes
        ''' </summary>
        Event CurrentTabChanged(vOldIndex As Integer, vNewIndex As Integer) 
        
        ''' <summary>
        ''' Raised after a tab is closed
        ''' </summary>
        Event TabClosed(vIndex As Integer) 
        
        ''' <summary>
        ''' Raised when tabs are reordered
        ''' </summary>
        Event TabReordered(vOldIndex As Integer, vNewIndex As Integer)  
        
        ''' <summary>
        ''' Raised when a tab requests a context menu
        ''' </summary>
        Event TabContextMenuRequested(vIndex As Integer, vX As Double, vY As Double)  
        
        ''' <summary>
        ''' Raised when tab modified state changes
        ''' </summary>
        Event TabModifiedChanged(vIndex As Integer, vModified As Boolean)  
        
        ''' <summary>
        ''' Raised when the hide panel button is clicked (for bottom panels)
        ''' </summary>
        Event HidePanelRequested()
        
    End Interface
    
End Namespace