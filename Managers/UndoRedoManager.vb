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
        Private pMaxStackSize As Integer = 100
        Private pIsUndoingOrRedoing As Boolean = False
        Private pGroupingActions As Boolean = False
        Private pCurrentGroup As New List(Of UndoAction)()
        
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
            
            ' Raise the state changed event
            RaiseStateChanged()
        End Sub    
    
        ''' <summary>
        ''' Begin grouping actions
        ''' </summary>
        Public Sub BeginGroup()
            If Not pIsUndoingOrRedoing Then
                pGroupingActions = True
                pCurrentGroup.Clear()
            End If
        End Sub
        
        ''' <summary>
        ''' End grouping actions
        ''' </summary>
        Public Sub EndGroup()
            If pGroupingActions AndAlso Not pIsUndoingOrRedoing Then
                pGroupingActions = False
                If pCurrentGroup.Count > 0 Then
                    ' Add all grouped actions as individual actions
                    For Each lAction In pCurrentGroup
                        AddUndoAction(lAction)
                    Next
                    pCurrentGroup.Clear()
                End If
            End If
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
                
                Select Case lAction.Type
                    Case UndoActionType.eInsert
                        ' Undo insert by deleting
                        pEditor.DeleteTextDirect(lAction.StartPosition, lAction.EndPosition)
                        pEditor.SetCursorPosition(lAction.StartPosition)
                        
                    Case UndoActionType.eDelete
                        ' Undo delete by inserting
                        pEditor.InsertTextAtPosition(lAction.StartPosition, lAction.Text)
                        pEditor.SetCursorPosition(lAction.CursorPosition)
                        
                    Case UndoActionType.eReplace
                        ' Undo replace by restoring old text
                        pEditor.ReplaceText(lAction.StartPosition, lAction.EndPosition, lAction.OldText)
                        pEditor.SetCursorPosition(lAction.CursorPosition)
                        
                    Case UndoActionType.eDragDrop
                        HandleDragDropUndo(lAction)
                        
                End Select
                
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
        ''' Perform redo operation
        ''' </summary>
        Public Function Redo() As Boolean
            If Not CanRedo Then Return False
            
            Try
                pIsUndoingOrRedoing = True
                
                Dim lAction As UndoAction = pRedoStack.Pop()
                
                Select Case lAction.Type
                    Case UndoActionType.eInsert
                        ' Redo insert
                        pEditor.InsertTextAtPosition(lAction.StartPosition, lAction.Text)
                        pEditor.SetCursorPosition(lAction.EndPosition)
                        
                    Case UndoActionType.eDelete
                        ' Redo delete
                        pEditor.DeleteTextDirect(lAction.StartPosition, lAction.EndPosition)
                        pEditor.SetCursorPosition(lAction.CursorPosition)
                        
                    Case UndoActionType.eReplace
                        ' Redo replace
                        pEditor.ReplaceText(lAction.StartPosition, lAction.EndPosition, lAction.NewText)
                        pEditor.SetCursorPosition(lAction.CursorPosition)
                        
                    Case UndoActionType.eDragDrop
                        HandleDragDropRedo(lAction)
                        
                End Select
                
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
                ' Convert to array to preserve order
                If pUndoStack.Count > pMaxStackSize Then
                    Dim lActions() As UndoAction = pUndoStack.ToArray()
                    pUndoStack.Clear()
                    
                    ' Keep only the most recent actions
                    For i As Integer = Math.Max(0, lActions.Length - pMaxStackSize) To lActions.Length - 1
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
        ''' Begin grouping multiple actions into a single undo operation
        ''' </summary>
        ''' <remarks>
        ''' Use this when performing multiple operations that should be undone as a single unit.
        ''' Always pair with EndUserAction() in a Try/Finally block.
        ''' </remarks>
        Public Sub BeginUserAction()
            If Not pIsUndoingOrRedoing Then
                pGroupingActions = True
                pCurrentGroup.Clear()
            End If
        End Sub
        
        
        ''' <summary>
        ''' Creates a composite action from a group of actions
        ''' </summary>
        Private Function CreateGroupAction(vActions As List(Of UndoAction)) As UndoAction
            If vActions.Count = 0 Then Return Nothing
            
            Dim lGroupAction As New UndoAction()
            lGroupAction.Type = UndoActionType.eGroup  ' Add this to enum if not present
            
            ' Set start position from first action
            lGroupAction.StartPosition = vActions(0).StartPosition
            
            ' Set end position from last action
            lGroupAction.EndPosition = vActions(vActions.Count - 1).EndPosition
            
            ' Set cursor position from last action
            lGroupAction.CursorPosition = vActions(vActions.Count - 1).CursorPosition
            
            ' Store all text operations (for complex undo/redo)
            Dim lTextBuilder As New System.Text.StringBuilder()
            For Each lAction In vActions
                If Not String.IsNullOrEmpty(lAction.Text) Then
                    lTextBuilder.Append(lAction.Text)
                End If
            Next
            lGroupAction.Text = lTextBuilder.ToString()
            
            ' Store the group of actions for complex undo/redo
            ' You might want to add a GroupedActions property to UndoAction
            ' Or handle this differently based on your needs
            
            Return lGroupAction
        End Function
        
        ' ===== Alternative Enhanced Version with UndoGroup =====
        
        Private pCurrentUndoGroup As UndoGroup = Nothing
        
        ''' <summary>
        ''' Begin a user-defined action group with a specific type
        ''' </summary>
        ''' <param name="vGroupType">Type of group being created</param>
        Public Sub BeginUserAction(Optional vGroupType As UndoGroupType = UndoGroupType.eUserAction)
            If Not pIsUndoingOrRedoing AndAlso pCurrentUndoGroup Is Nothing Then
                pCurrentUndoGroup = New UndoGroup()
                pCurrentUndoGroup.GroupType = vGroupType
                pCurrentUndoGroup.StartTime = DateTime.Now
                pGroupingActions = True
            End If
        End Sub
        
        ''' <summary>
        ''' End action grouping and add to undo stack as a single group
        ''' </summary>
        ''' <remarks>
        ''' This completes the grouping started by BeginUserAction().
        ''' All actions recorded between Begin and End will be undone/redone together.
        ''' </remarks>
        Public Sub EndUserAction()
            If pGroupingActions AndAlso Not pIsUndoingOrRedoing Then
                pGroupingActions = False
                If pCurrentGroup.Count > 0 Then
                    ' Add all grouped actions as individual actions
                    For Each lAction In pCurrentGroup
                        AddUndoAction(lAction)
                    Next
                    pCurrentGroup.Clear()
                    ' Raise state changed event after completing the group
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
        ''' Example: Complex paste operation with formatting
        ''' </summary>
        Public Sub PasteWithFormatting(vEditor As IEditor, vText As String)
            BeginUserAction(UndoGroupType.ePaste)
            
            Try
                ' Delete current selection if any
                If vEditor.HasSelection Then
                    Dim lSelStart As EditorPosition = vEditor.SelectionStart
                    Dim lSelEnd As EditorPosition = vEditor.SelectionEnd
                    Dim lSelectedText As String = vEditor.GetSelectedText()
                    
                    RecordDeleteText(lSelStart, lSelEnd, lSelectedText, lSelStart)
                    vEditor.DeleteSelection()
                End If
                
                ' Insert the new text
                Dim lCursorPos As EditorPosition = vEditor.GetCursorPosition()
                RecordInsertText(lCursorPos, vText, lCursorPos)
                vEditor.InsertTextAtPosition(lCursorPos, vText)
                
                ' Apply formatting (this would also be recorded)
                ' ... formatting code ...
                
            Finally
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
                
                Console.WriteLine($"Replaced {lReplacementCount} occurrences")
                
            Finally
                EndUserAction()
            End Try
        End Sub
        
    End Class
    
End Namespace
