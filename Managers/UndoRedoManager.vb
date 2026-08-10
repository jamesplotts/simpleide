' Managers/UndoRedoManager.vb - Manages undo/redo operations
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Managers
    
    ''' <summary>
    ''' Manages undo and redo operations for the editor
    ''' </summary>
    Public Class UndoRedoManager
        Implements IDisposable
        
        ' ===== Private Fields =====
        Private pUndoStack As New Stack(Of UndoAction)()
        Private pRedoStack As New Stack(Of UndoAction)()
        Private pEditor As IEditor
        ' 100 was far too small once every keystroke - not just every word/line - became its
        ' own undo action (see BracketAutoClose/Keyboard.vb, which record one eInsert per
        ' character typed): a single "Public Sub Foo(i As Integer)" declaration alone produces
        ' 30-40 pushes, so a few lines of normal typing silently exceeded 100 and
        ' EnforceStackLimit discarded the oldest entries - the start of what was typed -
        ' making it permanently un-undoable and leaving stray leftover text once the (now
        ' incomplete) history ran out. 10000 comfortably covers real editing sessions while
        ' still bounding memory (each UndoAction is small - a handful of strings/positions).
        ' Overridden per-editor from SettingsManager.UndoHistorySize - see
        ' CustomDrawingEditor.UndoRedo.vb/InitializeUndoRedo and CustomDrawingEditor.Settings.vb/
        ' OnSettingsZoomChanged's "UndoHistorySize" case - this is just the fallback used
        ' before a SettingsManager is available (or if there isn't one)
        Private pMaxStackSize As Integer = 10000
        Private pIsUndoingOrRedoing As Boolean = False
        Private pGroupingActions As Boolean = False
        Private pCurrentGroup As New List(Of UndoAction)()

        ''' <summary>
        ''' Nesting depth of Begin*/End* calls - only the OUTERMOST Begin call actually starts
        ''' a new group (clearing pCurrentGroup) and only the OUTERMOST End call actually
        ''' finalizes it (pushing it to the undo stack). Without this, a nested/re-entrant
        ''' Begin call (or one left over from an earlier Begin whose matching End never fired -
        ''' e.g. an exception or early Return on a call site not wrapped in Try/Finally) would
        ''' unconditionally clear() whatever was already buffered, silently discarding every
        ''' edit recorded since the leaked Begin - making all of it permanently un-undoable
        ''' with no error or indication anything went wrong
        ''' </summary>
        Private pGroupDepth As Integer = 0
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets whether undo is available
        ''' </summary>
        Public ReadOnly Property CanUndo As Boolean
            Get
                Return pUndoStack.Count > 0
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether redo is available
        ''' </summary>
        Public ReadOnly Property CanRedo As Boolean
            Get
                Return pRedoStack.Count > 0
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets the maximum stack size
        ''' </summary>
        Public Property MaxStackSize As Integer
            Get
                Return pMaxStackSize
            End Get
            Set(value As Integer)
                pMaxStackSize = Math.Max(1, value)
                EnforceStackLimit()
            End Set
        End Property
        
        ''' <summary>
        ''' Gets whether currently performing undo/redo
        ''' </summary>
        Public Property IsUndoingOrRedoing As Boolean
            Get
                Return pIsUndoingOrRedoing
            End Get
            Set(value as Boolean)
                pIsUndoingOrRedoing = value
            End Set
        End Property
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new undo/redo manager
        ''' </summary>
        Public Sub New(vEditor As IEditor)
            pEditor = vEditor
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Clear all undo/redo history
        ''' </summary>
        Public Sub Clear()
            pUndoStack.Clear()
            pRedoStack.Clear()
            pCurrentGroup.Clear()
            pGroupingActions = False

            ' pGroupDepth wasn't being reset here - if Clear() ever ran while a BeginUserAction()/
            ' EndUserAction() pair was still open, the depth counter would stay nonzero forever
            ' after, permanently unbalancing grouping: the next BeginUserAction() would see a
            ' nonzero depth and treat itself as a nested call, never starting a fresh group again
            pGroupDepth = 0

            ' pCleanMarkerAction wasn't being reset either - it would keep pointing at a now-
            ' discarded UndoAction from before the clear, so IsAtCleanPoint would read False
            ' instead of True immediately after a Clear() that followed a clean (saved) state.
            ' An empty stack should itself represent the clean point (see this field's own doc
            ' comment above), matching what a fresh UndoRedoManager starts with
            pCleanMarkerAction = Nothing

            ' Raise the state changed event
            RaiseStateChanged()
        End Sub

        ''' <summary>
        ''' The undo action that was on top of the stack the last time the document was saved
        ''' (or loaded) - Nothing means the clean point is an empty stack. Compared against by
        ''' IsAtCleanPoint after every Undo/Redo so the editor's modified flag can be cleared
        ''' when undoing lands back on exactly the saved state, not just left permanently True
        ''' the instant anything was ever typed (see CustomDrawingEditor.UndoRedo.vb's Undo/Redo
        ''' and CustomDrawingEditor.IO.vb's SaveContent/LoadContent, which call MarkClean())
        ''' </summary>
        Private pCleanMarkerAction As UndoAction = Nothing

        ''' <summary>
        ''' Marks the current top-of-undo-stack position as "clean" (matching saved content) -
        ''' call after a successful save or load
        ''' </summary>
        Public Sub MarkClean()
            pCleanMarkerAction = If(pUndoStack.Count > 0, pUndoStack.Peek(), Nothing)
        End Sub

        ''' <summary>
        ''' True if the undo stack's current position matches the last MarkClean() call - i.e.
        ''' undo/redo has navigated back to exactly the saved (or loaded) state
        ''' </summary>
        ''' <remarks>
        ''' Reference comparison, not content comparison - undo/redo move the same UndoAction
        ''' object between the undo and redo stacks rather than cloning it, so the object
        ''' identity captured by MarkClean() survives any number of Undo()/Redo() round trips.
        ''' It stops matching for good once a new edit is made after undoing past the clean
        ''' point, since AddUndoAction discards the redo stack (and the marked action with it) -
        ''' which is correct: the document really does differ from the saved state from then on
        ''' </remarks>
        Public ReadOnly Property IsAtCleanPoint As Boolean
            Get
                If pUndoStack.Count = 0 Then Return pCleanMarkerAction Is Nothing
                Return pUndoStack.Peek() Is pCleanMarkerAction
            End Get
        End Property


        ''' <summary>
        ''' Begin grouping actions - safe to call while already grouping (e.g. nested inside
        ''' another Begin*/End* pair, or if an earlier Begin's matching End never fired): only
        ''' the OUTERMOST call actually starts a fresh group. See pGroupDepth.
        ''' </summary>
        Public Sub BeginGroup()
            If Not pIsUndoingOrRedoing Then
                If pGroupDepth = 0 Then
                    pGroupingActions = True
                    pCurrentGroup.Clear()
                End If
                pGroupDepth += 1
            End If
        End Sub

        ''' <summary>
        ''' End grouping actions - only the OUTERMOST matching call actually finalizes and
        ''' pushes the group. See pGroupDepth.
        ''' </summary>
        Public Sub EndGroup()
            If pGroupDepth > 0 AndAlso Not pIsUndoingOrRedoing Then
                pGroupDepth -= 1
                If pGroupDepth = 0 Then
                    pGroupingActions = False
                    FinalizeGroup()
                End If
            End If
        End Sub

        ''' <summary>
        ''' Pushes the buffered pCurrentGroup actions onto the undo stack as a single unit -
        ''' the lone action directly if only one was recorded (no need for eGroup wrapping in
        ''' the common single-edit case), or one real eGroup action wrapping all of them so
        ''' Undo/Redo can replay or reverse the whole set atomically in a single Ctrl+Z/Ctrl+Y
        ''' </summary>
        ''' <remarks>
        ''' Previously, both BeginGroup/EndGroup and BeginUserAction/EndUserAction just pushed
        ''' every buffered action individually - despite doc comments and the "Usage Examples"
        ''' below claiming they'd undo/redo together as one unit, they never actually did,
        ''' silently requiring one Ctrl+Z per sub-action for any multi-step edit anywhere in
        ''' the app that used this grouping (clipboard paste, drag-drop, comment toggling,
        ''' search/replace-all, and several keyboard operations)
        ''' </remarks>
        Private Sub FinalizeGroup()
            If pCurrentGroup.Count = 0 Then Return

            If pCurrentGroup.Count = 1 Then
                AddUndoAction(pCurrentGroup(0))
            Else
                Dim lGroupAction As New UndoAction()
                lGroupAction.Type = UndoActionType.eGroup
                lGroupAction.StartPosition = pCurrentGroup(0).StartPosition
                lGroupAction.EndPosition = pCurrentGroup(pCurrentGroup.Count - 1).EndPosition
                lGroupAction.CursorPosition = pCurrentGroup(pCurrentGroup.Count - 1).CursorPosition
                lGroupAction.GroupedActions = New List(Of UndoAction)(pCurrentGroup)
                AddUndoAction(lGroupAction)
            End If

            pCurrentGroup.Clear()
        End Sub
        
        ''' <summary>
        ''' Event raised when undo/redo state changes
        ''' </summary>
        Public Event UndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean)
        
        ''' <summary>
        ''' Raises the UndoRedoStateChanged event
        ''' </summary>
        Private Sub RaiseStateChanged()
            Try
                RaiseEvent UndoRedoStateChanged(CanUndo, CanRedo)
            Catch ex As Exception
                Console.WriteLine($"RaiseStateChanged error: {ex.Message}")
            End Try
        End Sub

        ' ===== Recording Methods =====
        
        ''' <summary>
        ''' Records text insertion using EditorPosition
        ''' </summary>
        Public Sub RecordInsertText(vPosition As EditorPosition, vText As String, vNewCursorPos As EditorPosition)
            Try
                Dim lAction As UndoAction = UndoAction.CreateInsert(vPosition, vText, vNewCursorPos)
                lAction.Type = UndoActionType.eInsert
                AddAction(lAction)
            Catch ex As Exception
                Console.WriteLine($"RecordInsertText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Records text deletion using EditorPosition
        ''' </summary>
        Public Sub RecordDeleteText(vStartPos As EditorPosition, vEndPos As EditorPosition, 
                                  vDeletedText As String, vNewCursorPos As EditorPosition)
            Try
                Dim lAction As UndoAction = UndoAction.CreateDelete(vStartPos, vEndPos, vDeletedText, vNewCursorPos)
                lAction.Type = UndoActionType.eDelete
                AddAction(lAction)
            Catch ex As Exception
                Console.WriteLine($"RecordDeleteText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Records text deletion for undo (overload for backward compatibility)
        ''' </summary>
        Public Sub RecordDelete(vLine As Integer, vColumn As Integer, vText As String, vCursorLine As Integer, vCursorColumn As Integer)
            Dim lStartPos As New EditorPosition(vLine, vColumn)
            Dim lEndPos As New EditorPosition(vLine, vColumn + vText.Length)
            Dim lCursorPos As New EditorPosition(vCursorLine, vCursorColumn)
            RecordDeleteText(lStartPos, lEndPos, vText, lCursorPos)
        End Sub
        
        ''' <summary>
        ''' Records a character insertion using EditorPosition
        ''' </summary>
        Public Sub RecordInsertChar(vPosition As EditorPosition, vChar As Char, vNewCursorPos As EditorPosition)
            Try
                Dim lAction As UndoAction = UndoAction.CreateInsert(vPosition, vChar.ToString(), vNewCursorPos)
                lAction.Type = UndoActionType.eInsert
                AddAction(lAction)
            Catch ex As Exception
                Console.WriteLine($"RecordInsertChar error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Records a character deletion using EditorPosition
        ''' </summary>
        Public Sub RecordDeleteChar(vPosition As EditorPosition, vChar As Char, vNewCursorPos As EditorPosition)
            Try
                Dim lEndPos As New EditorPosition(vPosition.Line, vPosition.Column + 1)
                Dim lAction As UndoAction = UndoAction.CreateDelete(vPosition, lEndPos, vChar.ToString(), vNewCursorPos)
                lAction.Type = UndoActionType.eDelete
                AddAction(lAction)
            Catch ex As Exception
                Console.WriteLine($"RecordDeleteChar error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Records a line insertion using EditorPosition
        ''' </summary>
        Public Sub RecordInsertLine(vPosition As EditorPosition, vLineText As String, vNewCursorPos As EditorPosition)
            Try
                Dim lAction As UndoAction = UndoAction.CreateInsert(vPosition, vLineText, vNewCursorPos)
                lAction.Type = UndoActionType.eInsert
                AddAction(lAction)
            Catch ex As Exception
                Console.WriteLine($"RecordInsertLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Records a line deletion using EditorPosition
        ''' </summary>
        Public Sub RecordDeleteLine(vLineNumber As Integer, vLineText As String, vNewCursorPos As EditorPosition)
            Try
                Dim lStartPos As New EditorPosition(vLineNumber, 0)
                Dim lEndPos As New EditorPosition(vLineNumber + 1, 0) ' Includes newline
                Dim lAction As UndoAction = UndoAction.CreateDelete(lStartPos, lEndPos, vLineText, vNewCursorPos)
                lAction.Type = UndoActionType.eDelete
                AddAction(lAction)
            Catch ex As Exception
                Console.WriteLine($"RecordDeleteLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Records text replacement using EditorPosition
        ''' </summary>
        Public Sub RecordReplaceText(vStartPos As EditorPosition, vEndPos As EditorPosition,
                                    vOldText As String, vNewText As String, vNewCursorPos As EditorPosition)
            Try
                Dim lAction As UndoAction = UndoAction.CreateReplace(vStartPos, vEndPos, vOldText, vNewText, vNewCursorPos)
                lAction.Type = UndoActionType.eReplace
                AddAction(lAction)
            Catch ex As Exception
                Console.WriteLine($"RecordReplaceText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Records a replace operation for undo (backward compatibility)
        ''' </summary>
        Public Sub RecordReplace(vLine As Integer, vColumn As Integer, vOldText As String, vNewText As String)
            If pIsUndoingOrRedoing Then Return
            
            Dim lStartPos As New EditorPosition(vLine, vColumn)
            Dim lEndPos As New EditorPosition(vLine, vColumn + vOldText.Length)
            Dim lCursorPos As EditorPosition = pEditor.GetCursorPosition()
            RecordReplaceText(lStartPos, lEndPos, vOldText, vNewText, lCursorPos)
        End Sub

        
        ''' <summary>
        ''' Records a drag-drop operation for undo using EditorPosition
        ''' </summary>
        Public Sub RecordDragDrop(vSourceStart As EditorPosition, vSourceEnd As EditorPosition,
                                 vDropPosition As EditorPosition, vText As String,
                                 vNewCursorPos As EditorPosition)
            If pIsUndoingOrRedoing Then Return
            
            Dim lAction As New UndoAction()
            lAction.Type = UndoActionType.eDragDrop
            lAction.StartPosition = vSourceStart
            lAction.EndPosition = vSourceEnd
            lAction.Text = vText
            lAction.CursorPosition = vNewCursorPos
            
            ' Store drop position in SelectionStart for undo/redo
            lAction.SelectionStart = vDropPosition
            
            AddAction(lAction)
        End Sub
        
        ' ===== Undo/Redo Operations =====
        
        ''' <summary>
        ''' Perform undo operation
        ''' </summary>
        Public Function Undo() As Boolean
            If Not CanUndo Then Return False

            Try
                pIsUndoingOrRedoing = True

                Dim lAction As UndoAction = pUndoStack.Pop()

                ApplyUndoAction(lAction)

                ' Add to redo stack
                pRedoStack.Push(lAction)

                ' Raise the state changed event
                RaiseStateChanged()

                Return True

            Catch ex As Exception
                Console.WriteLine($"Undo error: {ex.Message}")
                Return False
            Finally
                pIsUndoingOrRedoing = False
            End Try
        End Function

        ''' <summary>
        ''' Applies the undo (reverse) effect of a single action to the editor - shared by
        ''' Undo() for the top-level popped action and, recursively, for each sub-action of
        ''' an eGroup action, applied in REVERSE order (undoing the most-recently-applied
        ''' sub-action first, matching how the group's edits were actually made)
        ''' </summary>
        Private Sub ApplyUndoAction(vAction As UndoAction)
            Select Case vAction.Type
                Case UndoActionType.eInsert
                    ' Undo insert by deleting
                    pEditor.DeleteTextDirect(vAction.StartPosition, vAction.EndPosition)
                    pEditor.SetCursorPosition(vAction.StartPosition)

                Case UndoActionType.eDelete
                    ' Undo delete by inserting. Cursor goes to EndPosition (right after
                    ' the reinserted text), not CursorPosition - CursorPosition is where
                    ' the cursor ended up AFTER the original delete (i.e. right BEFORE
                    ' the text being restored here), which would leave the cursor
                    ' stranded ahead of text the user just watched get undone
                    pEditor.InsertTextAtPosition(vAction.StartPosition, vAction.Text)
                    pEditor.SetCursorPosition(vAction.EndPosition)

                Case UndoActionType.eReplace
                    ' Undo replace: the buffer currently holds NewText, spanning
                    ' [StartPosition, CursorPosition) - CursorPosition was recorded as
                    ' "position right after NewText" when the replace was originally
                    ' performed. That (not EndPosition, which is the OLD text's span
                    ' end) is the range that must be handed to ReplaceText so it
                    ' deletes exactly what's really there. Cursor then lands at
                    ' EndPosition - right after the just-restored OldText
                    pEditor.ReplaceText(vAction.StartPosition, vAction.CursorPosition, vAction.OldText)
                    pEditor.SetCursorPosition(vAction.EndPosition)

                Case UndoActionType.eDragDrop
                    HandleDragDropUndo(vAction)

                Case UndoActionType.eGroup
                    If vAction.GroupedActions IsNot Nothing Then
                        for i As Integer = vAction.GroupedActions.Count - 1 To 0 Step -1
                            ApplyUndoAction(vAction.GroupedActions(i))
                        Next
                    End If

            End Select
        End Sub

        ''' <summary>
        ''' Perform redo operation
        ''' </summary>
        Public Function Redo() As Boolean
            If Not CanRedo Then Return False

            Try
                pIsUndoingOrRedoing = True

                Dim lAction As UndoAction = pRedoStack.Pop()

                ApplyRedoAction(lAction)

                ' Add back to undo stack
                pUndoStack.Push(lAction)

                ' Raise the state changed event
                RaiseStateChanged()

                Return True

            Catch ex As Exception
                Console.WriteLine($"Redo error: {ex.Message}")
                Return False
            Finally
                pIsUndoingOrRedoing = False
            End Try
        End Function

        ''' <summary>
        ''' Applies the redo (re-apply) effect of a single action to the editor - shared by
        ''' Redo() for the top-level popped action and, recursively, for each sub-action of
        ''' an eGroup action, applied in FORWARD order (replaying the sub-actions in the same
        ''' order they were originally made)
        ''' </summary>
        Private Sub ApplyRedoAction(vAction As UndoAction)
            Select Case vAction.Type
                Case UndoActionType.eInsert
                    ' Redo insert
                    pEditor.InsertTextAtPosition(vAction.StartPosition, vAction.Text)
                    pEditor.SetCursorPosition(vAction.EndPosition)

                Case UndoActionType.eDelete
                    ' Redo delete
                    pEditor.DeleteTextDirect(vAction.StartPosition, vAction.EndPosition)
                    pEditor.SetCursorPosition(vAction.CursorPosition)

                Case UndoActionType.eReplace
                    ' Redo replace
                    pEditor.ReplaceText(vAction.StartPosition, vAction.EndPosition, vAction.NewText)
                    pEditor.SetCursorPosition(vAction.CursorPosition)

                Case UndoActionType.eDragDrop
                    HandleDragDropRedo(vAction)

                Case UndoActionType.eGroup
                    If vAction.GroupedActions IsNot Nothing Then
                        for each lSubAction In vAction.GroupedActions
                            ApplyRedoAction(lSubAction)
                        Next
                    End If

            End Select
        End Sub
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Add action to undo stack
        ''' </summary>
        Private Sub AddUndoAction(vAction As UndoAction)
            If pIsUndoingOrRedoing Then Return

            ' Clear redo stack when new action is added
            pRedoStack.Clear()

            ' Add to undo stack
            pUndoStack.Push(vAction)

            ' Enforce stack size limit
            EnforceStackLimit()
        End Sub

        ''' <summary>
        ''' Enforce maximum stack size
        ''' </summary>
        Private Sub EnforceStackLimit()
            Try
                If pUndoStack.Count > pMaxStackSize Then
                    ' Stack(Of T).ToArray() returns items in POP order - index 0 is the TOP
                    ' (most recently pushed), the last index is the BOTTOM (oldest). The
                    ' previous version of this code treated index 0 as the oldest (insertion
                    ' order) instead, so it kept the OLDEST pMaxStackSize entries and discarded
                    ' the most recent ones, then rebuilt the stack with those old entries back
                    ' on top - meaning once any session's undo history exceeded pMaxStackSize
                    ' (100) actions, the very next Undo() would silently pop a stale action from
                    ' the START of the session instead of the actual last edit, with everything
                    ' after that scrambled the same way. Keep indices [0, pMaxStackSize) - the
                    ' actual most-recent entries - and push them back in REVERSE (oldest-of-the-
                    ' kept-ones first, so it lands at the bottom; newest last, so it's back on
                    ' top) to preserve the original top-to-bottom order.
                    Dim lActions() As UndoAction = pUndoStack.ToArray()
                    pUndoStack.Clear()

                    for i As Integer = Math.Min(pMaxStackSize, lActions.Length) - 1 To 0 Step -1
                        pUndoStack.Push(lActions(i))
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"EnforceStackLimit error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles undo for drag-drop operations
        ''' </summary>
        Private Sub HandleDragDropUndo(lAction As UndoAction)
            Try
                ' For drag-drop undo, we reverse the operation
                ' Delete from drop location
                Dim lDropPos As EditorPosition = lAction.SelectionStart
                Dim lLines() As String = lAction.Text.Split({Environment.NewLine}, StringSplitOptions.None)
                Dim lEndLine As Integer = lDropPos.Line + lLines.Length - 1
                Dim lEndColumn As Integer = If(lLines.Length = 1, lDropPos.Column + lAction.Text.Length, lLines(lLines.Length - 1).Length)
                
                pEditor.DeleteTextDirect(lDropPos, New EditorPosition(lEndLine, lEndColumn))
                
                ' Insert back at source location
                pEditor.InsertTextAtPosition(lAction.StartPosition, lAction.Text)
                
                ' Select the restored text
                pEditor.SetSelection(lAction.StartPosition, lAction.EndPosition)
                
                ' Restore cursor
                pEditor.SetCursorPosition(lAction.CursorPosition)
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDropUndo error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles redo for drag-drop operations
        ''' </summary>
        Private Sub HandleDragDropRedo(lAction As UndoAction)
            Try
                ' For drag-drop redo, we repeat the original operation
                ' Delete from source
                pEditor.DeleteTextDirect(lAction.StartPosition, lAction.EndPosition)
                
                ' Insert at drop location
                Dim lDropPos As EditorPosition = lAction.SelectionStart
                pEditor.InsertTextAtPosition(lDropPos, lAction.Text)
                
                ' Select the dropped text
                Dim lLines() As String = lAction.Text.Split({Environment.NewLine}, StringSplitOptions.None)
                Dim lEndLine As Integer = lDropPos.Line + lLines.Length - 1
                Dim lEndColumn As Integer = If(lLines.Length = 1, lDropPos.Column + lAction.Text.Length, lLines(lLines.Length - 1).Length)
                
                pEditor.SetSelection(lDropPos, New EditorPosition(lEndLine, lEndColumn))
                
            Catch ex As Exception
                Console.WriteLine($"HandleDragDropRedo error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== IDisposable Implementation =====
        
        ''' <summary>
        ''' Dispose of resources
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Clear()
            pEditor = Nothing
        End Sub


        ''' <summary>
        ''' Begin grouping multiple actions into a single undo operation - safe to call while
        ''' already grouping (nested, or a leftover from an earlier Begin whose matching End
        ''' never fired): only the OUTERMOST call actually starts a fresh group. See
        ''' pGroupDepth. Always pair with EndUserAction() in a Try/Finally block.
        ''' </summary>
        Public Sub BeginUserAction()
            If Not pIsUndoingOrRedoing Then
                If pGroupDepth = 0 Then
                    pGroupingActions = True
                    pCurrentGroup.Clear()
                End If
                pGroupDepth += 1
            End If
        End Sub


        ' ===== Alternative Enhanced Version with UndoGroup =====

        Private pCurrentUndoGroup As UndoGroup = Nothing

        ''' <summary>
        ''' Begin a user-defined action group with a specific type - shares the same
        ''' pGroupDepth nesting as the other Begin* overloads (pCurrentUndoGroup itself is
        ''' just metadata for this specific overload's own outermost call, not part of the
        ''' nesting/finalization decision)
        ''' </summary>
        ''' <param name="vGroupType">Type of group being created</param>
        Public Sub BeginUserAction(Optional vGroupType As UndoGroupType = UndoGroupType.eUserAction)
            If Not pIsUndoingOrRedoing Then
                If pGroupDepth = 0 Then
                    pCurrentUndoGroup = New UndoGroup()
                    pCurrentUndoGroup.GroupType = vGroupType
                    pCurrentUndoGroup.StartTime = DateTime.Now
                    pGroupingActions = True
                    pCurrentGroup.Clear()
                End If
                pGroupDepth += 1
            End If
        End Sub

        ''' <summary>
        ''' End action grouping and add to undo stack as a single group - only the OUTERMOST
        ''' matching End call actually finalizes and pushes the group. See pGroupDepth.
        ''' </summary>
        ''' <remarks>
        ''' This completes the grouping started by BeginUserAction() (either overload) or
        ''' BeginGroup(). All actions recorded between Begin and End are undone/redone together
        ''' as a single unit.
        ''' </remarks>
        Public Sub EndUserAction()
            If pGroupDepth > 0 AndAlso Not pIsUndoingOrRedoing Then
                pGroupDepth -= 1
                If pGroupDepth = 0 Then
                    pGroupingActions = False
                    FinalizeGroup()
                    pCurrentUndoGroup = Nothing
                    RaiseStateChanged()
                End If
            End If
        End Sub
       
        ''' <summary>
        ''' Add an action to the current group or directly to the stack
        ''' </summary>
        Private Sub AddAction(vAction As UndoAction)
            If pIsUndoingOrRedoing Then Return
            
            If pGroupingActions Then
                pCurrentGroup.Add(vAction)
            Else
                AddUndoAction(vAction)
                ' Raise the state changed event after adding action
                RaiseStateChanged()
            End If
        End Sub
        
        ' ===== Usage Examples =====
        
        ''' <summary>
        ''' Example: Indent multiple lines as a single undo operation
        ''' </summary>
        Public Sub IndentSelectedLines(vEditor As IEditor, vStartLine As Integer, vEndLine As Integer)
            ' Begin grouping
            BeginUserAction(UndoGroupType.eIndentation)
            
            Try
                For i As Integer = vStartLine To vEndLine
                    ' Each line modification is recorded but grouped
                    Dim lLine As String = vEditor.GetLineText(i)
                    Dim lIndentedLine As String = vbTab & lLine
                    
                    Dim lStartPos As New EditorPosition(i, 0)
                    Dim lEndPos As New EditorPosition(i, lLine.Length)
                    Dim lNewCursorPos As New EditorPosition(i, lIndentedLine.Length)
                    
                    RecordReplaceText(lStartPos, lEndPos, lLine, lIndentedLine, lNewCursorPos)
                    
                    ' Apply the change
                    vEditor.ReplaceText(lStartPos, lEndPos, lIndentedLine)
                Next
            Finally
                ' Always end grouping, even if error occurs
                EndUserAction()
            End Try
        End Sub
        
        
        ''' <summary>
        ''' Example: Find and replace all as single undo
        ''' </summary>
        Public Sub ReplaceAll(vEditor As IEditor, vFindText As String, vReplaceText As String)
            Dim lReplacementCount As Integer = 0
            
            BeginUserAction(UndoGroupType.eReplace)
            
            Try
                ' Find all occurrences and replace
                Dim lMatches As List(Of EditorPosition) = vEditor.FindAll(vFindText)
                
                ' Process in reverse order to maintain positions
                For i As Integer = lMatches.Count - 1 To 0 Step -1
                    Dim lMatchPos As EditorPosition = lMatches(i)
                    Dim lEndPos As New EditorPosition(lMatchPos.Line, lMatchPos.Column + vFindText.Length)
                    
                    ' Record the replacement
                    RecordReplaceText(lMatchPos, lEndPos, vFindText, vReplaceText, lEndPos)
                    
                    ' Perform the replacement
                    vEditor.ReplaceText(lMatchPos, lEndPos, vReplaceText)
                    lReplacementCount += 1
                Next
                
                #If DEBUG Then
                Console.WriteLine($"Replaced {lReplacementCount} occurrences")
                #End If
                
            Finally
                EndUserAction()
            End Try
        End Sub
        
    End Class
    
End Namespace
