' Replace entire file: CustomDrawingEditor.UndoRedo.vb
' Editors/CustomDrawingEditor.UndoRedo.vb - Undo/Redo integration
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor


        ''' <summary>
        ''' Initialize the undo/redo manager
        ''' </summary>
        Private Sub InitializeUndoRedo()
            Try
                pUndoRedoManager = New UndoRedoManager(Me)
                AddHandler pUndoRedoManager.UndoRedoStateChanged, AddressOf OnUndoRedoStateChanged
            Catch ex As Exception
                Console.WriteLine($"InitializeUndoRedo error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handle undo/redo state changes
        ''' </summary>
        Private Sub OnUndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean)
            Try
                RaiseEvent UndoRedoStateChanged(vCanUndo, vCanRedo)
            Catch ex As Exception
                Console.WriteLine($"OnUndoRedoStateChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Executes undo operation from the undo/redo manager
        ''' </summary>
        Public Sub Undo() Implements IEditor.Undo
            Try
                ' If there's a selection but nothing to undo, clear the selection
                If pHasSelection AndAlso pUndoRedoManager IsNot Nothing Then
                    If Not pUndoRedoManager.CanUndo Then
                        ' Nothing to undo, but we have a selection - clear it
                        ClearSelection()
                        Return
                    End If
                End If
                
                ' Perform the actual undo if available
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.Undo()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Undo error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Executes redo operation from the undo/redo manager
        ''' </summary>
        Public Sub Redo() Implements IEditor.Redo
            If pUndoRedoManager IsNot Nothing Then
                pUndoRedoManager.Redo()
            End If
        End Sub

        ''' <summary>
        ''' Clear undo/redo history
        ''' </summary>
        Public Sub ClearUndoRedo()
            If pUndoRedoManager IsNot Nothing Then
                pUndoRedoManager.Clear()
            End If
        End Sub

    End Class
    
End Namespace