' PARTIAL FILE: CustomDrawingEditor.DragDrop.vb
' This is a COMPLETE REPLACEMENT for the existing CustomDrawingEditor.DragDrop.vb file
' However, it requires other partial class files to compile (Mouse.vb, Drawing.vb, etc.)

Imports Gtk
Imports Gdk
Imports System
Imports System.Text
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

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
                ' CRITICAL: First set up as drop target BEFORE source
                ' This ensures the widget can receive its own drags
                
                ' Enable drop target functionality
                Gtk.Drag.DestSet(pDrawingArea, 
                                 DestDefaults.Motion Or DestDefaults.Highlight, 
                                 DROP_TARGETS, 
                                 DragAction.Copy Or DragAction.Move)
                
                ' Enable drag source functionality
                ' Use 0 for button mask since we're starting drags manually
                Gtk.Drag.SourceSet(pDrawingArea, 
                                   0, 
                                   DRAG_TARGETS, 
                                   DragAction.Copy Or DragAction.Move)
                
                ' Connect drop target events FIRST (so they're ready when drag starts)
                AddHandler pDrawingArea.DragMotion, AddressOf HandleDragMotion
                AddHandler pDrawingArea.DragLeave, AddressOf HandleDragLeave
                AddHandler pDrawingArea.DragDrop, AddressOf HandleDragDrop
                AddHandler pDrawingArea.DragDataReceived, AddressOf HandleDragDataReceived
                
                ' Then connect drag source events
                AddHandler pDrawingArea.DragBegin, AddressOf HandleDragBegin      
                AddHandler pDrawingArea.DragDataGet, AddressOf HandleDragDataGet
                AddHandler pDrawingArea.DragEnd, AddressOf HandleDragEnd
                AddHandler pDrawingArea.DragFailed, AddressOf HandleDragFailed
                
                Console.WriteLine("Drag and drop initialized for CustomDrawingEditor")
                Console.WriteLine($"  Drag targets: {DRAG_TARGETS.Length} targets")
                Console.WriteLine($"  Drop targets: {DROP_TARGETS.Length} targets")
                
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
                Console.WriteLine($"HandleDragBegin: Drag started with {If(pDragData IsNot Nothing, pDragData.Length, 0)} characters")
                
                ' Check if we have data to drag (set by OnMotionNotify in Mouse.vb)
                If String.IsNullOrEmpty(pDragData) Then
                    ' If no data was set, try to get it now
                    If pHasSelection Then
                        pDragData = GetSelectedText()
                        pDragStarted = True
                        pIsDragSource = True
                        pWasDragSourceForDrop = True  ' CRITICAL FIX: Remember we're the source
                        pDragStartLine = pSelectionStartLine
                        pDragStartColumn = pSelectionStartColumn
                        pDragEndLine = pSelectionEndLine
                        pDragEndColumn = pSelectionEndColumn
                        NormalizeSelection(pDragStartLine, pDragStartColumn, pDragEndLine, pDragEndColumn)
                    End If
                Else
                    ' Data was already set by OnMotionNotify
                    pDragStarted = True
                    pIsDragSource = True
                    pWasDragSourceForDrop = True  ' CRITICAL FIX: Remember we're the source
                End If
                
                If String.IsNullOrEmpty(pDragData) Then
                    Console.WriteLine("HandleDragBegin: No text to drag")
                    vArgs.RetVal = False
                    Return
                End If
                
                ' TODO: Set custom drag icon if desired
                ' For now, use default drag icon
                
                Console.WriteLine($"Drag started: {pDragData.Length} characters")
                Console.WriteLine($"Source selection: ({pDragStartLine},{pDragStartColumn}) to ({pDragEndLine},{pDragEndColumn})")
                
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
                
                Console.WriteLine($"HandleDragDataGet: Providing {pDragData.Length} chars for target {vArgs.Info}")
                
                ' Provide data based on the requested target
                Select Case vArgs.Info
                    Case 0 ' Plain text target
                        vArgs.SelectionData.Text = pDragData
                    Case 1 ' VB Code target
                        vArgs.SelectionData.Text = pDragData
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDataGet error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the end of a drag operation
        ''' </summary>
        Private Sub HandleDragEnd(vSender As Object, vArgs As DragEndArgs)
            Try
                ' Check if this was a move operation
                If pIsDragSource AndAlso pDragWasMove Then
                    Console.WriteLine("Drag move operation completed")
                End If
                
                ' Reset drag state
                pDragStarted = False
                pIsDragSource = False
                pDragData = ""
                pDragWasMove = False
                pShowDropIndicator = False
                ' NOTE: Don't reset pWasDragSourceForDrop here - it's needed for HandleDragDataReceived
                
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
                
                ' Reset drag state
                pDragStarted = False
                pIsDragSource = False
                pDragData = ""
                pDragWasMove = False
                pShowDropIndicator = False
                
                ' Return false to allow default handling
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragFailed error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Drop Target Event Handlers =====
        
        ''' <summary>
        ''' Handles drag motion over the drop target
        ''' </summary>
        Private Function HandleDragMotion(vSender As Object, vArgs As DragMotionArgs) As Boolean
            Try
                ' Calculate drop position from coordinates
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.X, vArgs.Y)
                pDrawingArea.Window.Cursor = pDragCursor
                
                ' Update drop target position
                pDropTargetLine = lPos.Line
                pDropTargetColumn = lPos.Column
                pShowDropIndicator = True
                
                ' Check if this is a valid drop location
                Dim lIsValidDrop As Boolean = IsValidDropLocation(lPos.Line, lPos.Column)
                
                If lIsValidDrop Then
                    ' FIXED: Determine the action based on whether this is internal
                    Dim lDesiredAction As DragAction
                    
                    If pIsDragSource Then
                        ' Dragging within the same editor - prefer Move
                        ' The user can hold Ctrl to force a copy
                        lDesiredAction = DragAction.Move
                    Else
                        ' Dragging from external source - always copy
                        lDesiredAction = DragAction.Copy
                    End If
                    
                    ' Set the action we want
                    Gdk.Drag.Status(vArgs.Context, lDesiredAction, vArgs.Time)
                Else
                    ' Cannot accept the drop here
                    Gdk.Drag.Status(vArgs.Context, 0, vArgs.Time) ' 0 = no action
                End If
                
                ' Queue redraw to show drop indicator
                pDrawingArea.QueueDraw()
                
                ' Return true to indicate we're handling the drag
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
                ' Hide drop indicator but DON'T reset the position
                ' The position might still be needed if the drag re-enters
                pShowDropIndicator = False
                
                ' DON'T reset these - keep the last valid position
                ' pDropTargetLine = -1
                ' pDropTargetColumn = -1
                
                ' Queue redraw to remove drop indicator
                pDrawingArea.QueueDraw()
                
                Console.WriteLine($"Drag left editor area (keeping position: {pDropTargetLine}, {pDropTargetColumn})")
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragLeave error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the actual drop operation  
        ''' </summary>
        Private Function HandleDragDrop(vSender As Object, vArgs As DragDropArgs) As Boolean
            Try
                Console.WriteLine($"HandleDragDrop called at ({vArgs.X}, {vArgs.Y})")
                
                ' Calculate drop position
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.X, vArgs.Y)
                
                Console.WriteLine($"Calculated drop position: Line={lPos.Line}, Column={lPos.Column}")
                
                ' Store drop position for use in HandleDragDataReceived
                pDropTargetLine = lPos.Line
                pDropTargetColumn = lPos.Column
                
                ' Request the data - use Gtk.Drag to avoid ambiguity
                Dim lAtom As Atom = Atom.Intern(TEXT_TARGET, False)
                Gtk.Drag.GetData(pDrawingArea, vArgs.Context, lAtom, vArgs.Time)
                
                ' Track if this is a move operation
                pDragWasMove = (vArgs.Context.SelectedAction = DragAction.Move)
                
                Console.WriteLine($"Drop requested at ({pDropTargetLine}, {pDropTargetColumn})")
                
                vArgs.RetVal = True
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDrop error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
                vArgs.RetVal = False
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Handles receiving the dropped data
        ''' </summary>
        Private Sub HandleDragDataReceived(vSender As Object, vArgs As DragDataReceivedArgs)
            Try
                ' Get the dropped text
                Dim lDroppedText As String = vArgs.SelectionData.Text
                Console.WriteLine($"HandleDragDataReceived: Dropped text = '{lDroppedText}'")
                
                If String.IsNullOrEmpty(lDroppedText) Then
                    ' Signal failed drop
                    Gtk.Drag.Finish(vArgs.Context, False, False, vArgs.Time)
                    pWasDragSourceForDrop = False  ' Reset here on failure
                    Return
                End If
                
                ' CRITICAL FIX: Use pWasDragSourceForDrop instead of pIsDragSource
                ' because HandleDragEnd may have already reset pIsDragSource
                Dim lIsFromSameEditor As Boolean = pWasDragSourceForDrop
                
                ' Log drop target position
                Console.WriteLine($"Drop target: Line={pDropTargetLine}, Column={pDropTargetColumn}")
                Console.WriteLine($"Is drag source: {lIsFromSameEditor}, Action: {vArgs.Context.SelectedAction}")
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Determine if this is a move or copy operation
                Dim lIsMove As Boolean = (vArgs.Context.SelectedAction = DragAction.Move)
                
                ' Handle the drop based on whether it's from the same editor
                If lIsFromSameEditor Then
                    ' From the same editor
                    If lIsMove Then
                        Console.WriteLine("Performing drag MOVE operation")
                        ' Moving within the same editor - delete source after insert
                        PerformDragMoveFixed(lDroppedText, pDropTargetLine, pDropTargetColumn)
                    Else
                        Console.WriteLine("Performing drag COPY operation (Ctrl was held)")
                        ' Copying within the same editor
                        PerformDragCopy(lDroppedText, pDropTargetLine, pDropTargetColumn)
                    End If
                Else
                    ' From external source - always copy
                    Console.WriteLine("Performing drag COPY operation (external source)")
                    PerformDragCopy(lDroppedText, pDropTargetLine, pDropTargetColumn)
                End If
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Hide drop indicator
                pShowDropIndicator = False
                
                ' Queue redraw
                pDrawingArea.QueueDraw()
                
                ' Signal successful drop
                ' IMPORTANT: For MOVE operations, we must pass True for the delete_source parameter
                Gtk.Drag.Finish(vArgs.Context, True, lIsMove AndAlso lIsFromSameEditor, vArgs.Time)
                
                Console.WriteLine($"Drop completed successfully (delete source: {lIsMove AndAlso lIsFromSameEditor})")
                
                ' CRITICAL FIX: Reset the flag here after we're done using it
                pWasDragSourceForDrop = False
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDataReceived error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
                ' Signal failed drop
                Gtk.Drag.Finish(vArgs.Context, False, False, vArgs.Time)
                pWasDragSourceForDrop = False  ' Reset on error too
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Checks if a drop location is valid
        ''' </summary>
        Private Function IsValidDropLocation(vLine As Integer, vColumn As Integer) As Boolean
            Try
                ' Can't drop outside document bounds
                If vLine < 0 OrElse vLine >= pLineCount Then Return False
                
                ' If dropping within the same editor during a move operation
                If pIsDragSource AndAlso pDragStarted Then
                    ' Can't drop within the current selection
                    If IsPositionInSelection(vLine, vColumn) Then
                        Return False
                    End If
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidDropLocation error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a position is within the current selection
        ''' </summary>
        Private Function IsPositionInSelection(vLine As Integer, vColumn As Integer) As Boolean
            Try
                If Not pHasSelection Then Return False
                
                ' Normalize selection bounds
                Dim lStartLine As Integer = pSelectionStartLine
                Dim lStartColumn As Integer = pSelectionStartColumn
                Dim lEndLine As Integer = pSelectionEndLine
                Dim lEndColumn As Integer = pSelectionEndColumn
                NormalizeSelection(lStartLine, lStartColumn, lEndLine, lEndColumn)
                
                ' Check if position is within selection
                If vLine < lStartLine OrElse vLine > lEndLine Then Return False
                If vLine = lStartLine AndAlso vColumn < lStartColumn Then Return False
                If vLine = lEndLine AndAlso vColumn > lEndColumn Then Return False
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsPositionInSelection error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' FIXED implementation of drag move operation with correct position adjustment
        ''' </summary>
        Private Sub PerformDragMoveFixed(vText As String, vTargetLine As Integer, vTargetColumn As Integer)
            Try
                Console.WriteLine("=== PerformDragMoveFixed START ===")
                
                ' Store original source positions
                Dim lSourceStartLine As Integer = pDragStartLine
                Dim lSourceStartColumn As Integer = pDragStartColumn
                Dim lSourceEndLine As Integer = pDragEndLine
                Dim lSourceEndColumn As Integer = pDragEndColumn
                
                ' Normalize source selection
                NormalizeSelection(lSourceStartLine, lSourceStartColumn, lSourceEndLine, lSourceEndColumn)
                
                Console.WriteLine($"Source: ({lSourceStartLine},{lSourceStartColumn}) to ({lSourceEndLine},{lSourceEndColumn})")
                Console.WriteLine($"Target: ({vTargetLine},{vTargetColumn})")
                Console.WriteLine($"Text to move: '{vText}'")
                
                ' Check if target is within source selection - shouldn't happen but be safe
                If IsPositionInSelection(vTargetLine, vTargetColumn) Then
                    Console.WriteLine("Target is within source selection - aborting")
                    Return
                End If
                
                ' Begin undo group if not already started
                Dim lStartedUndo As Boolean = False
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                    lStartedUndo = True
                End If
                
                Try
                    ' Calculate if we need to adjust the target position
                    ' This is needed when deleting text before the target
                    Dim lAdjustedLine As Integer = vTargetLine
                    Dim lAdjustedColumn As Integer = vTargetColumn
                    
                    If vTargetLine > lSourceEndLine Then
                        ' Target is after source - adjust for lines that will be deleted
                        Dim lLinesDeleted As Integer = lSourceEndLine - lSourceStartLine
                        lAdjustedLine = vTargetLine - lLinesDeleted
                        Console.WriteLine($"Target after source: adjusting line {vTargetLine} -> {lAdjustedLine}")
                    ElseIf vTargetLine = lSourceEndLine AndAlso vTargetColumn > lSourceEndColumn Then
                        ' Target is on same line but after source
                        If lSourceStartLine = lSourceEndLine Then
                            ' Single line: adjust column
                            lAdjustedColumn = vTargetColumn - (lSourceEndColumn - lSourceStartColumn)
                            Console.WriteLine($"Target on same line: adjusting column {vTargetColumn} -> {lAdjustedColumn}")
                        Else
                            ' Multi-line: more complex adjustment
                            lAdjustedLine = lSourceStartLine
                            lAdjustedColumn = lSourceStartColumn + (vTargetColumn - lSourceEndColumn)
                            Console.WriteLine($"Target at end of multi-line: adjusting to ({lAdjustedLine},{lAdjustedColumn})")
                        End If
                    End If
                    
                    ' CRITICAL FIX: Use the proper deletion method instead of DeleteSelection
                    ' Step 1: Delete the source selection using DeleteRange (which is the IEditor interface method)
                    Console.WriteLine("Step 1: Deleting source selection")
                    
                    ' First clear any existing selection to avoid confusion
                    pHasSelection = False
                    pSelectionActive = False
                    
                    ' Use DeleteRange to delete the source text
                    DeleteRange(lSourceStartLine, lSourceStartColumn, lSourceEndLine, lSourceEndColumn)
                    
                    ' Step 2: Insert at the (possibly adjusted) target position
                    Console.WriteLine($"Step 2: Inserting at ({lAdjustedLine},{lAdjustedColumn})")
                    InsertTextAtPosition(lAdjustedLine, lAdjustedColumn, vText)
                    
                    ' Step 3: Select the newly inserted text
                    Dim lLines() As String = vText.Split({Environment.NewLine}, StringSplitOptions.None)
                    If lLines.Length = 1 Then
                        SetSelection(lAdjustedLine, lAdjustedColumn, 
                                   lAdjustedLine, lAdjustedColumn + vText.Length)
                    Else
                        Dim lLastLineLength As Integer = lLines(lLines.Length - 1).Length
                        SetSelection(lAdjustedLine, lAdjustedColumn,
                                   lAdjustedLine + lLines.Length - 1, lLastLineLength)
                    End If
                    
                    Console.WriteLine("Move operation completed successfully")
                    
                Finally
                    ' End undo group if we started it
                    If lStartedUndo AndAlso pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.EndUserAction()
                    End If
                End Try
                
                ' Clear drag state
                pDragStartLine = -1
                pDragStartColumn = -1
                pDragEndLine = -1
                pDragEndColumn = -1
                
                Console.WriteLine("=== PerformDragMoveFixed END ===")
                
            Catch ex As Exception
                Console.WriteLine($"PerformDragMoveFixed error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Performs a drag copy operation (insert at target)
        ''' </summary>
        Private Sub PerformDragCopy(vText As String, vTargetLine As Integer, vTargetColumn As Integer)
            Try
                Console.WriteLine("=== PerformDragCopy START ===")
                Console.WriteLine($"Copying to ({vTargetLine},{vTargetColumn})")
                
                ' Move cursor to drop position
                SetCursorPosition(vTargetLine, vTargetColumn)
                
                ' Insert the text
                InsertText(vText)
                
                ' Select the inserted text
                Dim lLines() As String = vText.Split({Environment.NewLine}, StringSplitOptions.None)
                If lLines.Length = 1 Then
                    SetSelection(vTargetLine, vTargetColumn, 
                               vTargetLine, vTargetColumn + vText.Length)
                Else
                    SetSelection(vTargetLine, vTargetColumn,
                               vTargetLine + lLines.Length - 1, lLines(lLines.Length - 1).Length)
                End If
                
                Console.WriteLine("Copy operation completed successfully")
                Console.WriteLine("=== PerformDragCopy END ===")
                
            Catch ex As Exception
                Console.WriteLine($"PerformDragCopy error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the drop indicator at the specified position
        ''' </summary>
        Private Sub DrawDropIndicator(vContext As Cairo.Context)
            Try
                If Not pShowDropIndicator OrElse pDropTargetLine < 0 Then Return
                
                ' Calculate position for drop indicator
                Dim lX As Double = GetXFromColumn(pDropTargetLine, pDropTargetColumn)
                Dim lY As Double = GetYFromLine(pDropTargetLine)
                
                ' Draw a vertical line to indicate drop position
                vContext.Save()
                vContext.SetSourceRGBA(0.2, 0.5, 0.8, 0.8) ' Blue-ish color
                vContext.LineWidth = 2
                vContext.MoveTo(lX, lY)
                vContext.LineTo(lX, lY + pLineHeight)
                vContext.Stroke()
                
                ' Draw a small triangle at the top
                vContext.MoveTo(lX - 3, lY)
                vContext.LineTo(lX + 3, lY)
                vContext.LineTo(lX, lY + 5)
                vContext.ClosePath()
                vContext.Fill()
                
                vContext.Restore()
                
            Catch ex As Exception
                Console.WriteLine($"DrawDropIndicator error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the X coordinate for a column position
        ''' </summary>
        Private Function GetXFromColumn(vLine As Integer, vColumn As Integer) As Double
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return 0
                
                ' Calculate width based on character width
                ' Adjust for horizontal scrolling
                Return pLeftPadding + (vColumn * pCharWidth) - (pFirstVisibleColumn * pCharWidth)
                
            Catch ex As Exception
                Console.WriteLine($"GetXFromColumn error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the Y coordinate for a line position
        ''' </summary>
        Private Function GetYFromLine(vLine As Integer) As Double
            Try
                ' Adjust for vertical scrolling
                Return (vLine - pFirstVisibleLine) * pLineHeight + pTopPadding
                
            Catch ex As Exception
                Console.WriteLine($"GetYFromLine error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Called during drawing to render drop indicator if active
        ''' </summary>
        ''' <remarks>
        ''' This should be called from the main drawing method (DrawContent)
        ''' after drawing the text but before drawing the cursor
        ''' </remarks>
        Public Sub DrawDragDropIndicators(vContext As Cairo.Context)
            Try
                If pShowDropIndicator Then
                    DrawDropIndicator(vContext)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DrawDragDropIndicators error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
