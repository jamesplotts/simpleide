' Models/ProjectManagerRequestEventArgs.vb - EventArgs for requesting ProjectManager reference
Imports System
Imports SimpleIDE.Managers

Namespace Models
    
    ''' <summary>
    ''' EventArgs for requesting a reference to the ProjectManager
    ''' </summary>
    Public Class ProjectManagerRequestEventArgs
        Inherits EventArgs
        
        ''' <summary>
        ''' Gets or sets the ProjectManager reference
        ''' </summary>
        ''' <value>The ProjectManager instance, set by the event handler</value>
        Public Property ProjectManager As ProjectManager
        
        ''' <summary>
        ''' Gets whether a ProjectManager was provided
        ''' </summary>
        ''' <value>True if ProjectManager is not Nothing</value>
        Public ReadOnly Property HasProjectManager As Boolean
            Get
                Return ProjectManager IsNot Nothing
            End Get
        End Property
        
    End Class
    
End Namespace
