' Editors/CustomDrawingEditor.Comment.vb - Find and Replace implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor


        ' Squares the selection then checks to see if each line in the selection starts with an apostrophe.
        ' If has an apostrophe, it is removed from the line.
        ' If it doesn't, an apostrophe is inserted as the first character of that line.
        Public Sub ToggleCommentBlock() Implements IEditor.ToggleCommentBlock
            ' TODO:  Implement
        End Sub

        ' Called to change a selection of text to whole lines instead of partial lines  
        ' I.e. The Beginning of selection changed to column 0, the end of selection moved to the column 0 of the next line.
        Public Sub SquareSelection() Implements IEditor.SquareSelection
            ' TODO:  Implement
        End Sub

    End Class

End Namespace
