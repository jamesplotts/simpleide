' Editors/CustomDrawingEditor.DragDrop.vb - Drag and Drop implementation for selected code
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
                
                ' Check if we have data to drag (set by CheckStartDrag)
                If String.IsNullOrEmpty(pDragData) Then
                    ' If no data was set by CheckStartDrag, try to get it now
                    If pHasSelection Then
                        pDragData = GetSelectedText()
                        pDragStarted = True
                        pIsDragSource = True
                        pDragStartLine = pSelectionStartLine
                        pDragStartColumn = pSelectionStartColumn
                        pDragEndLine = pSelectionEndLine
                        pDragEndColumn = pSelectionEndColumn
                        NormalizeSelection(pDragStartLine, pDragStartColumn, pDragEndLine, pDragEndColumn)
                    End If
                End If
                
                If String.IsNullOrEmpty(pDragData) Then
                    Console.WriteLine("HandleDragBegin: No text to drag")
                    vArgs.RetVal = False
                    Return
                End If
                
                ' TODO: Set custom drag icon if desired
                ' For now, use default drag icon
                
                Console.WriteLine($"Drag started: {pDragData.Length} characters")
                
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
                ' Note: GTK# 3 doesn't have DeleteData property, we track this through drag context action
                If pIsDragSource AndAlso pDragWasMove Then
                    ' The deletion is handled in HandleDragDataReceived for move operations
                    Console.WriteLine("Drag move operation completed")
                End If
                
                ' Reset drag state
                pDragStarted = False
                pIsDragSource = False
                pDragData = ""
                pDragWasMove = False
                pShowDropIndicator = False
                
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
                Console.WriteLine($"HandleDragMotion called at ({vArgs.X}, {vArgs.Y})")
                
                ' Calculate drop position from coordinates
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.X, vArgs.Y)
                
                ' Update drop target position
                pDropTargetLine = lPos.Line
                pDropTargetColumn = lPos.Column
                pShowDropIndicator = True
                
                ' Check if this is a valid drop location
                Dim lIsValidDrop As Boolean = IsValidDropLocation(lPos.Line, lPos.Column)
                Console.WriteLine($"Drop location ({lPos.Line}, {lPos.Column}) is {If(lIsValidDrop, "valid", "invalid")}")
                
                If lIsValidDrop Then
                    ' Indicate that we can accept the drop
                    Gdk.Drag.Status(vArgs.Context, vArgs.Context.SuggestedAction, vArgs.Time)
                Else
                    ' Indicate that we cannot accept the drop here
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
                ' Hide drop indicator
                pShowDropIndicator = False
                pDropTargetLine = -1
                pDropTargetColumn = -1
                
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
                Console.WriteLine($"HandleDragDrop called at ({vArgs.X}, {vArgs.Y})")
                
                ' Calculate drop position
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vArgs.X, vArgs.Y)
                
                ' Request the data - use Gtk.Drag to avoid ambiguity
                Dim lAtom As Atom = Atom.Intern(TEXT_TARGET, False)
                Gtk.Drag.GetData(pDrawingArea, vArgs.Context, lAtom, vArgs.Time)
                
                ' Store drop position for use in HandleDragDataReceived
                pDropTargetLine = lPos.Line
                pDropTargetColumn = lPos.Column
                
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
        ''' Handles receiving the dropped data
        ''' </summary>
        Private Sub HandleDragDataReceived(vSender As Object, vArgs As DragDataReceivedArgs)
            Try
                ' Get the dropped text
                Dim lDroppedText As String = vArgs.SelectionData.Text
                Console.WriteLine($"HandleDragDataReceived: Dropped text = '{lDroppedText}'")
                
                If String.IsNullOrEmpty(lDroppedText) Then
                    ' Signal failed drop - use Gtk.Drag to avoid ambiguity
                    Gtk.Drag.Finish(vArgs.Context, False, False, vArgs.Time)
                    Return
                End If
                
                ' Log drop target position
                Console.WriteLine($"Drop target: Line={pDropTargetLine}, Column={pDropTargetColumn}")
                Console.WriteLine($"Is drag source: {pIsDragSource}, Action: {vArgs.Context.SelectedAction}")
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Determine if this is a move or copy operation
                Dim lIsMove As Boolean = (vArgs.Context.SelectedAction = DragAction.Move)
                
                ' Handle the drop based on whether it's from the same editor
                If pIsDragSource AndAlso lIsMove Then
                    Console.WriteLine("Performing drag MOVE operation")
                    ' Moving within the same editor
                    PerformDragMove(lDroppedText, pDropTargetLine, pDropTargetColumn)
                Else
                    Console.WriteLine("Performing drag COPY operation")
                    ' Copying or dropping from external source
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
                
                ' Signal successful drop - use Gtk.Drag to avoid ambiguity
                Gtk.Drag.Finish(vArgs.Context, True, lIsMove, vArgs.Time)
                
                Console.WriteLine($"Drop completed successfully")
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDataReceived error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
                ' Signal failed drop - use Gtk.Drag to avoid ambiguity
                Gtk.Drag.Finish(vArgs.Context, False, False, vArgs.Time)
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
        ''' Performs a drag move operation (delete from source, insert at target)
        ''' </summary>
        Private Sub PerformDragMove(vText As String, vTargetLine As Integer, vTargetColumn As Integer)
            Try
                Console.WriteLine($"PerformDragMove: Moving '{vText}' from ({pDragStartLine},{pDragStartColumn})-({pDragEndLine},{pDragEndColumn}) to ({vTargetLine},{vTargetColumn})")
                
                ' CRITICAL FIX: Store the text BEFORE any modifications
                Dim lTextToMove As String = vText
                
                ' Store original selection bounds 
                Dim lOrigStartLine As Integer = pDragStartLine
                Dim lOrigStartColumn As Integer = pDragStartColumn
                Dim lOrigEndLine As Integer = pDragEndLine
                Dim lOrigEndColumn As Integer = pDragEndColumn
                
                ' Determine if we're moving forward or backward
                Dim lMovingForward As Boolean = False
                If vTargetLine > lOrigEndLine Then
                    lMovingForward = True
                ElseIf vTargetLine = lOrigEndLine AndAlso vTargetColumn > lOrigEndColumn Then
                    lMovingForward = True
                End If
                
                Console.WriteLine($"Moving direction: {If(lMovingForward, "Forward", "Backward")}")
                
                If lMovingForward Then
                    ' Moving forward - delete first, then insert at adjusted position
                    
                    ' Calculate how the deletion will affect the target position
                    Dim lAdjustedLine As Integer = vTargetLine
                    Dim lAdjustedColumn As Integer = vTargetColumn
                    
                    If lOrigStartLine = lOrigEndLine Then
                        ' Single line selection
                        If vTargetLine = lOrigEndLine Then
                            ' Same line - adjust column
                            lAdjustedColumn = vTargetColumn - (lOrigEndColumn - lOrigStartColumn)
                        End If
                    Else
                        ' Multi-line selection - adjust line
                        lAdjustedLine = vTargetLine - (lOrigEndLine - lOrigStartLine)
                        If vTargetLine = lOrigEndLine Then
                            ' Target is on the last line of selection
                            lAdjustedColumn = vTargetColumn - lOrigEndColumn
                        End If
                    End If
                    
                    Console.WriteLine($"Forward move - Adjusted target: ({lAdjustedLine},{lAdjustedColumn})")
                    
                    ' Delete the original selection
                    Console.WriteLine($"Deleting selection at ({lOrigStartLine},{lOrigStartColumn})-({lOrigEndLine},{lOrigEndColumn})")
                    SetSelection(lOrigStartLine, lOrigStartColumn, lOrigEndLine, lOrigEndColumn)
                    DeleteSelection()
                    
                    ' Insert at adjusted position
                    Console.WriteLine($"Inserting at ({lAdjustedLine},{lAdjustedColumn})")
                    SetCursorPosition(lAdjustedLine, lAdjustedColumn)
                    Me.InsertText(lTextToMove)
                    
                    ' Select the newly inserted text
                    Dim lLines() As String = lTextToMove.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                    Dim lEndLine As Integer = lAdjustedLine
                    Dim lEndColumn As Integer = lAdjustedColumn
                    
                    If lLines.Length = 1 Then
                        ' Single line - end column is start + length
                        lEndColumn = lAdjustedColumn + lTextToMove.Length
                    Else
                        ' Multi-line - end line is start + line count - 1
                        lEndLine = lAdjustedLine + lLines.Length - 1
                        lEndColumn = lLines(lLines.Length - 1).Length
                    End If
                    
                    Console.WriteLine($"Setting selection to ({lAdjustedLine},{lAdjustedColumn})-({lEndLine},{lEndColumn})")
                    SetSelection(lAdjustedLine, lAdjustedColumn, lEndLine, lEndColumn)
                    
                Else
                    ' Moving backward - insert first at target, then delete adjusted source
                    Console.WriteLine("Backward move")
                    
                    ' Insert at target position first
                    Console.WriteLine($"Inserting at target ({vTargetLine},{vTargetColumn})")
                    SetCursorPosition(vTargetLine, vTargetColumn)
                    Me.InsertText(lTextToMove)
                    
                    ' Calculate how the insertion affects the original selection position
                    Dim lLines() As String = lTextToMove.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                    Dim lLinesAdded As Integer = lLines.Length - 1
                    
                    Dim lAdjustedStartLine As Integer = lOrigStartLine
                    Dim lAdjustedStartColumn As Integer = lOrigStartColumn
                    Dim lAdjustedEndLine As Integer = lOrigEndLine
                    Dim lAdjustedEndColumn As Integer = lOrigEndColumn
                    
                    If vTargetLine < lOrigStartLine Then
                        ' Inserted before selection - adjust line numbers
                        lAdjustedStartLine += lLinesAdded
                        lAdjustedEndLine += lLinesAdded
                    ElseIf vTargetLine = lOrigStartLine AndAlso vTargetColumn <= lOrigStartColumn Then
                        ' Inserted on same line before selection
                        If lLinesAdded = 0 Then
                            ' Single line insert - adjust columns
                            lAdjustedStartColumn += lTextToMove.Length
                            If lOrigStartLine = lOrigEndLine Then
                                lAdjustedEndColumn += lTextToMove.Length
                            End If
                        Else
                            ' Multi-line insert - adjust lines and columns
                            lAdjustedStartLine += lLinesAdded
                            lAdjustedEndLine += lLinesAdded
                        End If
                    End If
                    
                    Console.WriteLine($"Deleting adjusted selection at ({lAdjustedStartLine},{lAdjustedStartColumn})-({lAdjustedEndLine},{lAdjustedEndColumn})")
                    
                    ' Delete the adjusted original selection
                    SetSelection(lAdjustedStartLine, lAdjustedStartColumn, lAdjustedEndLine, lAdjustedEndColumn)
                    DeleteSelection()
                    
                    ' Select the inserted text at target
                    Dim lEndLine As Integer = vTargetLine
                    Dim lEndColumn As Integer = vTargetColumn
                    
                    If lLines.Length = 1 Then
                        ' Single line - end column is start + length
                        lEndColumn = vTargetColumn + lTextToMove.Length
                    Else
                        ' Multi-line - end line is start + line count - 1
                        lEndLine = vTargetLine + lLines.Length - 1
                        lEndColumn = lLines(lLines.Length - 1).Length
                    End If
                    
                    Console.WriteLine($"Final selection: ({vTargetLine},{vTargetColumn})-({lEndLine},{lEndColumn})")
                    SetSelection(vTargetLine, vTargetColumn, lEndLine, lEndColumn)
                End If
                
                Console.WriteLine("PerformDragMove completed")
                
            Catch ex As Exception
                Console.WriteLine($"PerformDragMove error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Performs a drag copy operation (insert at target)
        ''' </summary>
        Private Sub PerformDragCopy(vText As String, vTargetLine As Integer, vTargetColumn As Integer)
            Try
                ' Move cursor to drop position
                SetCursorPosition(vTargetLine, vTargetColumn)
                
                ' Insert the text using the existing public method
                Me.InsertText(vText)
                
                ' Select the newly inserted text
                Dim lLines() As String = vText.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                Dim lEndLine As Integer = vTargetLine
                Dim lEndColumn As Integer = vTargetColumn
                
                If lLines.Length = 1 Then
                    ' Single line - end column is start + length
                    lEndColumn = vTargetColumn + vText.Length
                Else
                    ' Multi-line - end line is start + line count - 1
                    lEndLine = vTargetLine + lLines.Length - 1
                    lEndColumn = lLines(lLines.Length - 1).Length
                End If
                
                SetSelection(vTargetLine, vTargetColumn, lEndLine, lEndColumn)
                
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
                
                ' Get the line text from SourceFileInfo
                Dim lLineText As String = ""
                If pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.TextLines IsNot Nothing Then
                    If vLine < pSourceFileInfo.TextLines.Count Then
                        lLineText = If(vColumn > 0, 
                                      pSourceFileInfo.TextLines(vLine).Substring(0, Math.Min(vColumn, pSourceFileInfo.TextLines(vLine).Length)), 
                                      "")
                    End If
                End If
                
                ' Calculate width based on character width
                ' Adjust for horizontal scrolling
                Return pLeftPadding + (lLineText.Length * pCharWidth) - (pFirstVisibleColumn * pCharWidth)
                
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
