' Add: SimpleIDE.Models.TabClosingEventArgs
' To: New file TabClosingEventArgs.vb in Models folder

Imports System

Namespace Models
    
    ''' <summary>
    ''' Event arguments for the TabClosing event that allows handling or cancellation
    ''' </summary>
    Public Class TabClosingEventArgs
        Inherits EventArgs
        
        ''' <summary>
        ''' Gets the index of the tab being closed
        ''' </summary>
        Public ReadOnly Property TabIndex As Integer
        
        ''' <summary>
        ''' Gets or sets whether the close operation should be cancelled
        ''' </summary>
        Public Property Cancel As Boolean
        
        ''' <summary>
        ''' Gets or sets whether the event has been handled by the parent
        ''' </summary>
        ''' <remarks>
        ''' When set to True, the CustomDrawNotebook will not remove the tab
        ''' This allows the parent to handle the close in a custom way (e.g., hiding a panel)
        ''' </remarks>
        Public Property Handled As Boolean
        
        ''' <summary>
        ''' Gets the widget contained in the tab being closed
        ''' </summary>
        Public ReadOnly Property TabWidget As Gtk.Widget
        
        ''' <summary>
        ''' Gets the label text of the tab being closed
        ''' </summary>
        Public ReadOnly Property TabLabel As String
        
        ''' <summary>
        ''' Creates a new instance of TabClosingEventArgs
        ''' </summary>
        ''' <param name="vTabIndex">Index of the tab being closed</param>
        ''' <param name="vTabWidget">Widget contained in the tab</param>
        ''' <param name="vTabLabel">Label text of the tab</param>
        Public Sub New(vTabIndex As Integer, vTabWidget As Gtk.Widget, vTabLabel As String)
            TabIndex = vTabIndex
            TabWidget = vTabWidget
            TabLabel = vTabLabel
            Cancel = False
            Handled = False
        End Sub
        
    End Class
    
End Namespace