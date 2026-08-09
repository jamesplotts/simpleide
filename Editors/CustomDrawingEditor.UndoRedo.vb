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
                If pSettingsManager IsNot Nothing Then
                    pUndoRedoManager.MaxStackSize = pSettingsManager.UndoHistorySize
                End If
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
                ' Undo rewrites the buffer out from under whatever word/position
                ' CodeSense was tracking - close it rather than leave it stranded
                If pCodeSenseActive Then CancelCodeSense()

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
                    ' Undoing back to exactly the last saved/loaded position (see
                    ' UndoRedoManager.MarkClean/IsAtCleanPoint) means the buffer matches disk
                    ' again, not just "something was undone" - clear the modified flag rather
                    ' than leaving it permanently True from the first edit onward
                    IsModified = Not pUndoRedoManager.IsAtCleanPoint
                End If

                ' None of the low-level primitives Undo() drives (DeleteTextDirect,
                ' InsertTextAtPosition, ReplaceText) ever establish a selection of
                ' their own, so any selection still active at this point is stale
                ' (e.g. Paste leaves the pasted text selected - undoing the paste
                ' must not leave that selection pointing at text that's now gone)
                ClearSelection()

            Catch ex As Exception
                Console.WriteLine($"Undo error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Executes redo operation from the undo/redo manager
        ''' </summary>
        Public Sub Redo() Implements IEditor.Redo
            Try
                ' Same reasoning as Undo - redo rewrites the buffer out from under
                ' whatever word/position CodeSense was tracking
                If pCodeSenseActive Then CancelCodeSense()

                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.Redo()
                    ' See the matching comment in Undo() above
                    IsModified = Not pUndoRedoManager.IsAtCleanPoint
                End If

                ' Same reasoning as Undo - leave a plain cursor, not a stale selection
                ClearSelection()

            Catch ex As Exception
                Console.WriteLine($"Redo error: {ex.Message}")
            End Try
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