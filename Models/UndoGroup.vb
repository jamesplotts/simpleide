' Models/UndoGroup.vb - Group of related undo actions
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

' UndoGroup.vb
' Created: 2025-08-22 10:33:45

Namespace Models
    
    ''' <summary>
    ''' Represents a group of related undo actions that should be undone/redone together
    ''' </summary>
    Public Class UndoGroup
        
        ''' <summary>
        ''' List of actions in this group
        ''' </summary>
        Public Property Actions As New List(Of UndoAction)()
        
        ''' <summary>
        ''' Type of group (for merging logic)
        ''' </summary>
        Public Property GroupType As UndoGroupType = UndoGroupType.eUnspecified
        
        ''' <summary>
        ''' When this group started
        ''' </summary>
        Public Property StartTime As DateTime = DateTime.Now
        
        ''' <summary>
        ''' When this group ended
        ''' </summary>
        Public Property EndTime As DateTime = DateTime.Now
        
        ''' <summary>
        ''' Add an action to the group
        ''' </summary>
        Public Sub AddAction(vAction As UndoAction)
            Actions.Add(vAction)
            EndTime = DateTime.Now
        End Sub
        
        ''' <summary>
        ''' Check if a new action can be merged into this group
        ''' </summary>
        Public Function CanMergeAction(vAction As UndoAction) As Boolean
            If Actions.Count = 0 Then Return True
            
            ' Check time threshold (500ms for continuous typing)
            If (vAction.Timestamp - EndTime).TotalMilliseconds > 500 Then
                Return False
            End If
            
            ' Check if action is contiguous with last action
            Dim lLastAction As UndoAction = Actions(Actions.Count - 1)
            
            ' For character typing, check if positions are adjacent
            If GroupType = UndoGroupType.eTyping Then
                Return vAction.StartPosition.Line = lLastAction.EndPosition.Line AndAlso _
                       vAction.StartPosition.Column = lLastAction.EndPosition.Column
            End If
            
            ' For deletions (backspace), check if positions are adjacent
            If GroupType = UndoGroupType.eDeletion Then
                ' Backspace goes backward
                Return vAction.EndPosition.Line = lLastAction.StartPosition.Line AndAlso _
                       vAction.EndPosition.Column = lLastAction.StartPosition.Column
            End If
            
            Return True
        End Function
        
        ''' <summary>
        ''' Gets the starting position of this group
        ''' </summary>
        Public ReadOnly Property StartPosition As EditorPosition
            Get
                If Actions.Count > 0 Then
                    Return Actions(0).StartPosition
                End If
                Return New EditorPosition(0, 0)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the ending position of this group
        ''' </summary>
        Public ReadOnly Property EndPosition As EditorPosition
            Get
                If Actions.Count > 0 Then
                    Return Actions(Actions.Count - 1).EndPosition
                End If
                Return New EditorPosition(0, 0)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the cursor position for this group
        ''' </summary>
        Public ReadOnly Property CursorPosition As EditorPosition
            Get
                If Actions.Count > 0 Then
                    Return Actions(Actions.Count - 1).CursorPosition
                End If
                Return New EditorPosition(0, 0)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether this group is empty
        ''' </summary>
        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return Actions.Count = 0
            End Get
        End Property
        
        ''' <summary>
        ''' Clear all actions in this group
        ''' </summary>
        Public Sub Clear()
            Actions.Clear()
            GroupType = UndoGroupType.eUnspecified
        End Sub
        
    End Class
    
    ''' <summary>
    ''' Types of undo groups
    ''' </summary>
    Public Enum UndoGroupType
        eUnspecified
        eTyping         ' Continuous character typing
        eDeletion       ' Continuous backspace/delete
        ePaste          ' Paste operation
        eIndentation    ' Indent/outdent operations
        eComment        ' Comment/uncomment operations
        eReplace        ' Replace operations
        eUserAction     ' Explicit user-defined group
        eLastValue
    End Enum
    
End Namespace
