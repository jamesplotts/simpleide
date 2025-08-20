' ListBoxItem.vb
' Created: 2025-08-19 06:45:22

Imports System

Namespace Utilities

    ''' <summary>
    ''' Represents an item in the CustomDrawListBox
    ''' </summary>
    Public Class ListBoxItem
        Public Property Text As String
        Public Property Data As Object
        
        Public Sub New(vText As String, Optional vData As Object = Nothing)
            Text = vText
            Data = vData
        End Sub
        
        Public Overrides Function ToString() As String
            Return Text
        End Function
    End Class

End Namespace
