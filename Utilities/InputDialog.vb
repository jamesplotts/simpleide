' InputDialog.vb - Simple input dialog for user text entry
Imports Gtk
Imports SimpleIDE.Widgets
Imports SimpleIDE.Managers

Namespace Utilities

    Public Class InputDialog
        Inherits Dialog

        Private pEntry As CustomDrawTextBox

        Public ReadOnly Property Text As String
            Get
                Return pEntry.Text
            End Get
        End Property

        Public Sub New(vParent As Window, vTitle As String, vPrompt As String, Optional vDefaultText As String = "", Optional vThemeManager As ThemeManager = Nothing)
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
            pEntry = New CustomDrawTextBox()
            pEntry.Text = vDefaultText
            pEntry.ThemeManager = vThemeManager
            ' Pressing Enter submits - there's no real default-widget mechanism for a
            ' custom-drawn button, so wire the entry's Activated signal straight to Respond
            AddHandler pEntry.Activated, Sub() Respond(ResponseType.Ok)
            lVBox.PackStart(pEntry, False, False, 0)

            ' Add to content area
            ContentArea.Add(lVBox)

            ' Buttons - custom-drawn, wired to the dialog's own Response mechanism
            Dim lButtonBox As New Box(Orientation.Horizontal, 6)
            lButtonBox.Halign = Align.End

            Dim lCancelButton As New CustomDrawButton("Cancel")
            lCancelButton.ThemeManager = vThemeManager
            AddHandler lCancelButton.Clicked, Sub() Respond(ResponseType.Cancel)
            lButtonBox.PackStart(lCancelButton, False, False, 0)

            Dim lOkButton As New CustomDrawButton("OK")
            lOkButton.ThemeManager = vThemeManager
            AddHandler lOkButton.Clicked, Sub() Respond(ResponseType.Ok)
            lButtonBox.PackStart(lOkButton, False, False, 0)

            lVBox.PackStart(lButtonBox, False, False, 0)

            ' Show all
            ShowAll()

            ' Focus entry and select all text
            pEntry.GrabFocus()
            If Not String.IsNullOrEmpty(vDefaultText) Then
                pEntry.InnerEntry.SelectRegion(0, vDefaultText.Length)
            End If
        End Sub

    End Class

End Namespace