' ListBoxItem.vb
' Created: 2025-08-19 06:45:22

Imports System
Imports System.Collections.Generic

Namespace Utilities

    ''' <summary>
    ''' Represents an item in the CustomDrawListBox
    ''' </summary>
    Public Class ListBoxItem
        Public Property Text As String
        Public Property Data As Object

        ''' <summary>Optional detail text drawn right-aligned on the same row (e.g. a status or timestamp)</summary>
        Public Property SecondaryText As String = ""

        ''' <summary>Optional leading icons drawn left to right before Text</summary>
        Public Property Icons As New List(Of Gdk.Pixbuf)

        ''' <summary>Nesting depth for grouped/tree lists - 0 is top-level</summary>
        Public Property IndentLevel As Integer = 0

        ''' <summary>True if this row is a collapsible group header rather than a leaf item</summary>
        Public Property IsGroupHeader As Boolean = False

        ''' <summary>Whether a group header's children are currently shown - ignored for non-header rows</summary>
        Public Property IsExpanded As Boolean = True

        Public Sub New(vText As String, Optional vData As Object = Nothing)
            Text = vText
            Data = vData
        End Sub

        Public Overrides Function ToString() As String
            Return Text
        End Function
    End Class

End Namespace
