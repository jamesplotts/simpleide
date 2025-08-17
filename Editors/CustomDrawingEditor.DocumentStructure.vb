' CustomDrawingEditor.DocumentStructure.vb
' Created: 2025-08-04 22:26:57
' Editors/CustomDrawingEditor.DocumentStructure.vb - Document structure access and DocumentParsed event
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        Private pDocumentModel As DocumentModel
        
        ' ===== Events =====
        Public Event DocumentParsed(vRootNode As SyntaxNode) Implements IEditor.DocumentParsed
        
        ' ===== Document Structure Access =====
        
        ''' <summary>
        ''' Gets the parsed document structure as a SyntaxNode tree
        ''' </summary>
        Public Function GetDocumentStructure() As SyntaxNode Implements IEditor.GetDocumentStructure
            Try
                ' Return the root SyntaxNode from parsing
                Return pRootNode
                
            Catch ex As Exception
                Console.WriteLine($"GetDocumentStructure error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Public Sub SetDocumentModel(vModel As DocumentModel)
            Try
                pDocumentModel = vModel
                
                ' Load content from model
                If vModel IsNot Nothing Then
                    SetText(vModel.GetAllText())
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetDocumentModel error: {ex.Message}")
            End Try
        End Sub

        
        ' ===== Helper to update the parsing completion =====
        ' Call this method at the end of UpdateMetadataFromParse in CustomDrawingEditor.Parsing.vb
        Private Sub NotifyParsingComplete()
            Try
                ' Notify listeners that parsing is complete
                RaiseDocumentParsedEvent()
                
            Catch ex As Exception
                Console.WriteLine($"NotifyParsingComplete error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
