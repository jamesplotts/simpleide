' CustomDrawingEditor.NodeAccess.vb - IEditor.GetDocumentNodes implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' Gets the document node dictionary for this editor
        ''' </summary>
        ''' <returns>Always an empty dictionary - this editor's real parse tree is
        ''' SourceFileInfo.SyntaxTree / pRootNode (SyntaxNode), not the older DocumentNode
        ''' graph this interface member was designed for. Kept only because it's part of the
        ''' IEditor contract.</returns>
        Public Function GetDocumentNodes() As Dictionary(Of String, DocumentNode) Implements IEditor.GetDocumentNodes
            Return New Dictionary(Of String, DocumentNode)()
        End Function

    End Class

End Namespace
