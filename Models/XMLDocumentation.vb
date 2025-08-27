' XMLDocumentation.vb
' Created: 2025-08-26 13:29:14

Imports System

Namespace Models

        ''' <summary>
        ''' Helper class to hold XML documentation during parsing
        ''' </summary>
        Public Class XmlDocInfo
            Public Property Summary As String = ""
            Public Property Parameters As New Dictionary(Of String, String)()
            Public Property Returns As String = ""
            Public Property Remarks As String = ""
            Public Property Example As String = ""
            Public Property Exceptions As New Dictionary(Of String, String)()
            Public Property SeeAlso As New List(Of String)()
            Public Property Value As String = ""
        End Class

End Namespace
