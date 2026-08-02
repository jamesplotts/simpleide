Namespace Models

    ''' <summary>
    ''' Represents a single row within a Help tab section - either a clickable external
    ''' resource link (Url set) or a plain informational row like a keyboard shortcut (Url empty)
    ''' </summary>
    Public Class HelpResourceItem

        ''' <summary>
        ''' Gets or sets the primary label - a link's display title, or a shortcut key combo
        ''' </summary>
        Public Property Title As String

        ''' <summary>
        ''' Gets or sets the secondary descriptive text shown beneath or beside the title
        ''' </summary>
        Public Property Description As String

        ''' <summary>
        ''' Gets or sets the external URL to open when clicked; Nothing/empty renders this
        ''' item as a plain (non-clickable) row instead of a link
        ''' </summary>
        Public Property Url As String

        ''' <summary>
        ''' Initializes a new resource item
        ''' </summary>
        ''' <param name="vTitle">Display title or shortcut key combo</param>
        ''' <param name="vDescription">Secondary descriptive text</param>
        ''' <param name="vUrl">Optional external URL; omit for a non-clickable row</param>
        Public Sub New(vTitle As String, vDescription As String, Optional vUrl As String = Nothing)
            Title = vTitle
            Description = vDescription
            Url = vUrl
        End Sub

    End Class

End Namespace
