' Editors/CustomDrawingEditor.DragDrop.vb - Simplified implementation using new UndoRedoManager
Imports Gtk
Imports Gdk
Imports System
Imports System.Text
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Drag and Drop State =====
        Private pDragStarted As Boolean = False
        Private pDragData As String = ""
        Private pDragStartLine As Integer = -1
        Private pDragStartColumn As Integer = -1
        Private pDragEndLine As Integer = -1
        Private pDragEndColumn As Integer = -1
        Private pDropTargetLine As Integer = -1
        Private pDropTargetColumn As Integer = -1
        Private pShowDropIndicator As Boolean = False
        Private pIsDragSource As Boolean = False
        Private pDragWasMove As Boolean = False
        Private pWasDragSourceForDrop As Boolean = False 

        
        ' ===== Drag and Drop Configuration =====
        Private Shared ReadOnly TEXT_TARGET As String = "text/plain"
        Private Shared ReadOnly VBCODE_TARGET As String = "application/vb-code"
        Private Shared ReadOnly TEXT_TARGET_ENTRY As TargetEntry = New TargetEntry(TEXT_TARGET, TargetFlags.App Or TargetFlags.OtherWidget, 0)
        Private Shared ReadOnly VBCODE_TARGET_ENTRY As TargetEntry = New TargetEntry(VBCODE_TARGET, TargetFlags.App, 1)
        Private Shared ReadOnly DRAG_TARGETS() As TargetEntry = {TEXT_TARGET_ENTRY, VBCODE_TARGET_ENTRY}
        Private Shared ReadOnly DROP_TARGETS() As TargetEntry = {TEXT_TARGET_ENTRY, VBCODE_TARGET_ENTRY}
        
        ''' <summary>
        ''' Initialize drag and drop functionality for the editor
        ''' </summary>
        Private Sub InitializeDragDrop()
            Try
                ' Enable drop target functionality
                Gtk.Drag.DestSet(pDrawingArea, 
                                 DestDefaults.Motion Or DestDefaults.Highlight, 
                                 DROP_TARGETS, 
                                 DragAction.Copy Or DragAction.Move)
                
                ' Enable drag source functionality
                Gtk.Drag.SourceSet(pDrawingArea, 
                                   0, 
                                   DRAG_TARGETS, 
                                   DragAction.Copy Or DragAction.Move)
                
                ' Connect drop target events
                AddHandler pDrawingArea.DragMotion, AddressOf HandleDragMotion
                AddHandler pDrawingArea.DragLeave, AddressOf HandleDragLeave
                AddHandler pDrawingArea.DragDrop, AddressOf HandleDragDrop
                AddHandler pDrawingArea.DragDataReceived, AddressOf HandleDragDataReceived
                
                ' Connect drag source events
                AddHandler pDrawingArea.DragBegin, AddressOf HandleDragBegin      
                AddHandler pDrawingArea.DragDataGet, AddressOf HandleDragDataGet
                AddHandler pDrawingArea.DragEnd, AddressOf HandleDragEnd
                AddHandler pDrawingArea.DragFailed, AddressOf HandleDragFailed
                
                Console.WriteLine("Drag and drop initialized for CustomDrawingEditor")
                
            Catch ex As Exception
                Console.WriteLine($"InitializeDragDrop error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Drag Source Event Handlers =====
        
        ''' <summary>
        ''' Handles the beginning of a drag operation
        ''' </summary>
        Private Sub HandleDragBegin(vSender As Object, vArgs As DragBeginArgs)
            Try
                ' Get selected text if available
                If pHasSelection Then
                    pDragData = GetSelectedText()
                    pDragStarted = True
                    pIsDragSource = True
                    pWasDragSourceForDrop = True
                    
                    ' Store source selection coordinates
                    pDragStartLine = pSelectionStartLine
                    pDragStartColumn = pSelectionStartColumn
                    pDragEndLine = pSelectionEndLine
                    pDragEndColumn = pSelectionEndColumn
                    
                    ' Normalize the selection
                    NormalizeSelection(New EditorPosition(pDragStartLine, pDragStartColumn), New EditorPosition(pDragEndLine, pDragEndColumn))
                    
                    Console.WriteLine($"Drag started: {pDragData.Length} characters from ({pDragStartLine},{pDragStartColumn}) to ({pDragEndLine},{pDragEndColumn})")
                Else
                    Console.WriteLine("HandleDragBegin: No selection to drag")
                    vArgs.RetVal = False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragBegin error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Provides data for the drag operation
        ''' </summary>
        Private Sub HandleDragDataGet(vSender As Object, vArgs As DragDataGetArgs)
            Try
                If String.IsNullOrEmpty(pDragData) Then Return
                
                ' Provide data based on the requested target
                Select Case vArgs.Info
                    Case 0 ' Plain text target
                        vArgs.SelectionData.Text = pDragData
                    Case 1 ' VB Code target
                        vArgs.SelectionData.Text = pDragData
                End Select
                
                Console.WriteLine($"HandleDragDataGet: Provided {pDragData.Length} chars")
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDataGet error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the end of a drag operation
        ''' </summary>
        Private Sub HandleDragEnd(vSender As Object, vArgs As DragEndArgs)
            Try
                ' Reset drag state
                pDragStarted = False
                pIsDragSource = False
                pDragData = ""
                pDragWasMove = False
                pShowDropIndicator = False
                ' Note: Don't reset pWasDragSourceForDrop here - needed for HandleDragDataReceived
                
                ' Redraw to remove any drop indicators
                pDrawingArea.QueueDraw()
                
                Console.WriteLine("Drag operation ended")
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragEnd error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles drag operation failure
        ''' </summary>
        Private Function HandleDragFailed(vSender As Object, vArgs As DragFailedArgs) As Boolean
            Try
                Console.WriteLine($"Drag failed: {vArgs.Result}")
                
                ' Reset state
                pDragStarted = False
                pIsDragSource = False
                pWasDragSourceForDrop = False
                pDragData = ""
                pShowDropIndicator = False
                
                ' Redraw
                pDrawingArea.QueueDraw()
                
                vArgs.RetVal = True ' We handled it
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragFailed error: {ex.Message}")
                vArgs.RetVal = False
                Return False
            End Try
        End Function
        
        ' ===== Drop Target Event Handlers =====
        
        ''' <summary>
        ''' Handles drag motion over the drop target
        ''' </summary>
        Private Function HandleDragMotion(vSender As Object, vArgs As DragMotionArgs) As Boolean
            Try
                ' Calculate drop position
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.X, vArgs.Y)
                
                ' Update drop indicator position
                pDropTargetLine = lPos.Line
                pDropTargetColumn = lPos.Column
                pShowDropIndicator = True
                
                ' Determine if this would be a move or copy
                Dim lIsMove As Boolean = (vArgs.Context.Actions And DragAction.Move) = DragAction.Move
                
                ' Set the suggested action
                If lIsMove AndAlso (vArgs.Context.Actions And DragAction.Move) = DragAction.Move Then
                    Gdk.Drag.Status(vArgs.Context, DragAction.Move, vArgs.Time)
                Else
                    Gdk.Drag.Status(vArgs.Context, DragAction.Copy, vArgs.Time)
                End If
                
                ' Queue redraw to show drop indicator
                pDrawingArea.QueueDraw()
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragMotion error: {ex.Message}")
                vArgs.RetVal = False
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles when drag leaves the drop target
        ''' </summary>
        Private Sub HandleDragLeave(vSender As Object, vArgs As DragLeaveArgs)
            Try
                ' Hide drop indicator
                pShowDropIndicator = False
                
                ' Queue redraw to remove drop indicator
                pDrawingArea.QueueDraw()
                
                Console.WriteLine("Drag left editor area")
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragLeave error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the actual drop operation
        ''' </summary>
        Private Function HandleDragDrop(vSender As Object, vArgs As DragDropArgs) As Boolean
            Try
                ' Calculate drop position
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.X, vArgs.Y)
                
                ' Store drop position for use in HandleDragDataReceived
                pDropTargetLine = lPos.Line
                pDropTargetColumn = lPos.Column
                
                ' Request the data
                Dim lAtom As Atom = Atom.Intern(TEXT_TARGET, False)
                Gtk.Drag.GetData(pDrawingArea, vArgs.Context, lAtom, vArgs.Time)
                
                ' Track if this is a move operation
                pDragWasMove = (vArgs.Context.SelectedAction = DragAction.Move)
                
                Console.WriteLine($"Drop requested at ({pDropTargetLine}, {pDropTargetColumn})")
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDrop error: {ex.Message}")
                vArgs.RetVal = False
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles receiving the dropped data with simplified undo recording
        ''' </summary>
        Private Sub HandleDragDataReceived(vSender As Object, vArgs As DragDataReceivedArgs)
            Try
                ' Get the dropped text
                Dim lDroppedText As String = vArgs.SelectionData.Text
                
                If String.IsNullOrEmpty(lDroppedText) Then
                    Gtk.Drag.Finish(vArgs.Context, False, False, vArgs.Time)
                    pWasDragSourceForDrop = False
                    Return
                End If
                
                ' Check if this is from the same editor
                Dim lIsFromSameEditor As Boolean = pWasDragSourceForDrop
                
                ' Determine if this is a move or copy operation
                Dim lIsMove As Boolean = (vArgs.Context.SelectedAction = DragAction.Move)
                
                Console.WriteLine($"Received drop: IsMove={lIsMove}, FromSameEditor={lIsFromSameEditor}")
                
                ' Handle the drop
                If lIsFromSameEditor AndAlso lIsMove Then
                    ' Moving within the same editor - use simplified approach
                    PerformDragMove(lDroppedText, pDropTargetLine, pDropTargetColumn)
                Else
                    ' Copying (either from external source or Ctrl+drag)
                    PerformDragCopy(lDroppedText, pDropTargetLine, pDropTargetColumn)
                End If
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Hide drop indicator
                pShowDropIndicator = False
                
                ' Queue redraw
                pDrawingArea.QueueDraw()
                
                ' Signal successful drop
                Gtk.Drag.Finish(vArgs.Context, True, lIsMove AndAlso lIsFromSameEditor, vArgs.Time)
                
                ' Reset the flag
                pWasDragSourceForDrop = False
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDataReceived error: {ex.Message}")
                Gtk.Drag.Finish(vArgs.Context, False, False, vArgs.Time)
                pWasDragSourceForDrop = False
            End Try
        End Sub
        
        ''' <summary>
        ''' Performs a drag move operation with proper undo recording
        ''' </summary>
        Private Sub PerformDragMove(vSourceStartLine As Integer, vSourceStartColumn As Integer,
                                   vSourceEndLine As Integer, vSourceEndColumn As Integer,
                                   vTargetLine As Integer, vTargetColumn As Integer,
                                   vText As String)
            Try
                Console.WriteLine($"Move from ({vSourceStartLine},{vSourceStartColumn})-({vSourceEndLine},{vSourceEndColumn}) to ({vTargetLine},{vTargetColumn})")
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Calculate adjusted target position after source deletion
                Dim lAdjustedTargetLine As Integer = vTargetLine
                Dim lAdjustedTargetColumn As Integer = vTargetColumn
                
                If vTargetLine > vSourceEndLine Then
                    ' Target is after source, adjust for deletion
                    lAdjustedTargetLine -= (vSourceEndLine - vSourceStartLine)
                ElseIf vTargetLine = vSourceEndLine AndAlso vTargetColumn > vSourceEndColumn Then
                    ' Target is on same line after source
                    If vSourceStartLine = vSourceEndLine Then
                        lAdjustedTargetColumn -= (vSourceEndColumn - vSourceStartColumn)
                    End If
                End If
                
                ' Delete from source
                SetSelection(New EditorPosition(vSourceStartLine, vSourceStartColumn),
                            New EditorPosition(vSourceEndLine, vSourceEndColumn))
                DeleteSelection()
                
                ' Move cursor to adjusted target
                SetCursorPosition(lAdjustedTargetLine, lAdjustedTargetColumn)
                
                ' Insert at target
                InsertText(vText)
                
                ' Calculate final cursor position after insertion
                Dim lLines() As String = vText.Split({Environment.NewLine}, StringSplitOptions.None)
                Dim lNewCursorLine As Integer
                Dim lNewCursorColumn As Integer
                
                If lLines.Length = 1 Then
                    lNewCursorLine = lAdjustedTargetLine
                    lNewCursorColumn = lAdjustedTargetColumn + vText.Length
                Else
                    lNewCursorLine = lAdjustedTargetLine + lLines.Length - 1
                    lNewCursorColumn = lLines(lLines.Length - 1).Length
                End If
                
                ' Record the drag-drop operation for undo with the new cursor position
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordDragDrop(
                        New EditorPosition(vSourceStartLine, vSourceStartColumn),
                        New EditorPosition(vSourceEndLine, vSourceEndColumn),
                        New EditorPosition(lAdjustedTargetLine, lAdjustedTargetColumn),
                        vText,
                        New EditorPosition(lNewCursorLine, lNewCursorColumn)
                    )
                End If
                
                ' Select the moved text
                If lLines.Length = 1 Then
                    SetSelection(New EditorPosition(lAdjustedTargetLine, lAdjustedTargetColumn),
                               New EditorPosition(lAdjustedTargetLine, lAdjustedTargetColumn + vText.Length))
                Else
                    Dim lEndLine As Integer = lAdjustedTargetLine + lLines.Length - 1
                    Dim lEndColumn As Integer = lLines(lLines.Length - 1).Length
                    SetSelection(New EditorPosition(lAdjustedTargetLine, lAdjustedTargetColumn),
                               New EditorPosition(lEndLine, lEndColumn))
                End If
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                Console.WriteLine("Move operation completed")
                
            Catch ex As Exception
                Console.WriteLine($"PerformDragMove error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Performs a drag copy operation with proper undo recording
        ''' </summary>
        Private Sub PerformDragCopy(vText As String, vTargetLine As Integer, vTargetColumn As Integer)
            Try
                Console.WriteLine($"=== PerformDragCopy: Copying to ({vTargetLine},{vTargetColumn}) ===")
                
                ' For copy, we just need to record an insert operation
                ' The undo manager will handle this normally
                
                ' Move cursor to drop position
                SetCursorPosition(vTargetLine, vTargetColumn)
                
                ' Insert the text (this will automatically record undo)
                InsertText(vText)
                
                ' Select the inserted text
                Dim lLines() As String = vText.Split({Environment.NewLine}, StringSplitOptions.None)
                If lLines.Length = 1 Then
                    SetSelection(New EditorPosition(vTargetLine, vTargetColumn),
                               New EditorPosition(vTargetLine, vTargetColumn + vText.Length))
                Else
                    Dim lEndLine As Integer = vTargetLine + lLines.Length - 1
                    Dim lEndColumn As Integer = lLines(lLines.Length - 1).Length
                    SetSelection(New EditorPosition(vTargetLine, vTargetColumn), New EditorPosition(lEndLine, lEndColumn))
                End If
                
                Console.WriteLine("Copy operation completed")
                
            Catch ex As Exception
                Console.WriteLine($"PerformDragCopy error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to check if a position is within a selection
        ''' </summary>
        Private Function IsPositionInSelection(vLine As Integer, vColumn As Integer,
                                              vStartLine As Integer, vStartColumn As Integer,
                                              vEndLine As Integer, vEndColumn As Integer) As Boolean
            Try
                ' Check if position is before start
                If vLine < vStartLine Then Return False
                If vLine = vStartLine AndAlso vColumn < vStartColumn Then Return False
                
                ' Check if position is after end
                If vLine > vEndLine Then Return False
                If vLine = vEndLine AndAlso vColumn > vEndColumn Then Return False
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsPositionInSelection error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Performs a drag move operation with text and target position only (simplified overload)
        ''' </summary>
        ''' <param name="vText">Text to move</param>
        ''' <param name="vTargetLine">Target line for the drop</param>
        ''' <param name="vTargetColumn">Target column for the drop</param>
        Private Sub PerformDragMove(vText As String, vTargetLine As Integer, vTargetColumn As Integer)
            Try
                Console.WriteLine("=== PerformDragMove (3-param) START ===")
                
                ' Use the stored drag coordinates from when the drag started
                If pDragStartLine >= 0 AndAlso pDragEndLine >= 0 Then
                    ' Call the full version with the stored source coordinates
                    PerformDragMove(pDragStartLine, pDragStartColumn, 
                                  pDragEndLine, pDragEndColumn,
                                  vTargetLine, vTargetColumn, vText)
                Else
                    ' No valid source coordinates, just do a copy instead
                    Console.WriteLine("Warning: No valid source coordinates for drag move, performing copy instead")
                    PerformDragCopy(vText, vTargetLine, vTargetColumn)
                End If
                
                Console.WriteLine("=== PerformDragMove (3-param) END ===")
                
            Catch ex As Exception
                Console.WriteLine($"PerformDragMove (3-param) error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
