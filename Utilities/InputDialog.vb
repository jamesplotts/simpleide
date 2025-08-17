' InputDialog.vb - Simple input dialog for user text entry
Imports Gtk

Namespace Utilities

    Public Class InputDialog
        Inherits Dialog
        
        Private pEntry As Entry
        
        Public ReadOnly Property Text As String
            Get
                Return pEntry.Text
            End Get
        End Property
        
        Public Sub New(vParent As Window, vTitle As String, vPrompt As String, Optional vDefaultText As String = "")
            MyBase.New(vTitle, vParent, DialogFlags.Modal)
            
            ' Window setup
            SetDefaultSize(400, 150)
            SetPosition(WindowPosition.CenterOnParent)
            BorderWidth = 10
            
            ' Create content
            Dim lVBox As New Box(Orientation.Vertical, 6)
            
            ' Prompt label
            Dim lLabel As New Label(vPrompt)
            lLabel.Halign = Align.Start
            lVBox.PackStart(lLabel, False, False, 0)
            
            ' Entry field
            pEntry = New Entry()
            pEntry.Text = vDefaultText
            pEntry.ActivatesDefault = True
            lVBox.PackStart(pEntry, False, False, 0)
            
            ' Add to content area
            ContentArea.Add(lVBox)
            
            ' Add buttons
            AddButton("Cancel", ResponseType.Cancel)
            Dim lOkButton As Widget = AddButton("OK", ResponseType.Ok)
            
            ' Make OK the default button
            lOkButton.CanDefault = True
            lOkButton.GrabDefault()
            
            ' Show all
            ShowAll()
            
            ' Focus entry and select all text
            pEntry.GrabFocus()
            If Not String.IsNullOrEmpty(vDefaultText) Then
                pEntry.SelectRegion(0, vDefaultText.Length)
            End If
        End Sub
        
    End Class

End Namespace