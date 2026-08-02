Imports System.Collections.Generic

Namespace Models

    ''' <summary>
    ''' Represents one titled group of HelpResourceItem rows shown on a Help tab page
    ''' </summary>
    Public Class HelpSection

        ''' <summary>
        ''' Gets or sets the section's header text
        ''' </summary>
        Public Property HeaderText As String

        ''' <summary>
        ''' Gets the ordered list of items belonging to this section
        ''' </summary>
        Public ReadOnly Property Items As New List(Of HelpResourceItem)

        ''' <summary>
        ''' Initializes a new, empty section with the given header
        ''' </summary>
        ''' <param name="vHeaderText">The section's header text</param>
        Public Sub New(vHeaderText As String)
            HeaderText = vHeaderText
        End Sub

    End Class

End Namespace
