' MainWindow.Comment.vb - Comment/uncomment functionality for MainWindow
Imports Gtk
Imports System
Imports System.Text
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Models

Partial Public Class MainWindow
    
    ' ===== Comment/Uncomment Implementation =====
    
    ' Toggle comment on selected lines
    Public Sub OnToggleComment(vSender As Object, vArgs As EventArgs)
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab?.Editor Is Nothing Then Return
            
            ' Check if we can get selection from the editor
            If TypeOf lCurrentTab.Editor Is IEditor Then
                Dim lEditor As IEditor = DirectCast(lCurrentTab.Editor, IEditor)
                If lEditor IsNot Nothing Then
                    lEditor.ToggleCommentBlock()
            
                    ' Mark as modified
                    MarkTabModified(lEditor)
                End If
            End If

        Catch ex As Exception
            Console.WriteLine($"OnToggleComment error: {ex.Message}")
        End Try
    End Sub
    
    ' Find start of line containing position
    Private Function FindLineStart(vContent As String, vPosition As Integer) As Integer
        Try
            If vPosition <= 0 Then Return 0
            If vPosition >= vContent.Length Then vPosition = vContent.Length - 1
            
            ' Search backwards for line break
            for i As Integer = vPosition To 0 Step -1
                If i = 0 Then Return 0
                If vContent(i - 1) = vbLf OrElse vContent(i - 1) = vbCr Then
                    Return i
                End If
            Next
            
            Return 0
            
        Catch ex As Exception
            Console.WriteLine($"FindLineStart error: {ex.Message}")
            Return -1
        End Try
    End Function
    
    ' Find end of line containing position
    Private Function FindLineEnd(vContent As String, vPosition As Integer) As Integer
        Try
            If vPosition < 0 Then vPosition = 0
            If vPosition >= vContent.Length Then Return vContent.Length
            
            ' Search forwards for line break
            for i As Integer = vPosition To vContent.Length - 1
                If vContent(i) = vbLf OrElse vContent(i) = vbCr Then
                    Return i
                End If
            Next
            
            Return vContent.Length
            
        Catch ex As Exception
            Console.WriteLine($"FindLineEnd error: {ex.Message}")
            Return -1
        End Try
    End Function

    
End Class
