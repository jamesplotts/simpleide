' Models/UndoAction.vb - Undo/Redo action using EditorPosition
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

' UndoAction.vb
' Created: 2025-08-22 10:28:44

Namespace Models
    
    ''' <summary>
    ''' Represents an undoable/redoable action in the editor
    ''' </summary>
    Public Class UndoAction
        
        ''' <summary>
        ''' Type of action performed
        ''' </summary>
        Public Property Type As UndoActionType = UndoActionType.eUnspecified
        
        ''' <summary>
        ''' Starting position of the action
        ''' </summary>
        Public Property StartPosition As EditorPosition = New EditorPosition(0, 0)
        
        ''' <summary>
        ''' Ending position of the action
        ''' </summary>
        Public Property EndPosition As EditorPosition = New EditorPosition(0, 0)
        
        ''' <summary>
        ''' Text content for the action (used differently based on Type)
        ''' </summary>
        Public Property Text As String = ""
        
        ''' <summary>
        ''' Old text for replace operations
        ''' </summary>
        Public Property OldText As String = ""
        
        ''' <summary>
        ''' New text for replace operations
        ''' </summary>
        Public Property NewText As String = ""
        
        ''' <summary>
        ''' Timestamp when the action occurred
        ''' </summary>
        Public Property Timestamp As DateTime = DateTime.Now
        
        ''' <summary>
        ''' Cursor position to restore after undo/redo
        ''' </summary>
        Public Property CursorPosition As EditorPosition = New EditorPosition(0, 0)
        
        ''' <summary>
        ''' Selection start position (if selection existed)
        ''' </summary>
        Public Property SelectionStart As EditorPosition = New EditorPosition(-1, -1)
        
        ''' <summary>
        ''' Selection end position (if selection existed)
        ''' </summary>
        Public Property SelectionEnd As EditorPosition = New EditorPosition(-1, -1)
        
        ' ===== Helper Properties =====
        
        ''' <summary>
        ''' Gets whether this action had a selection
        ''' </summary>
        Public ReadOnly Property HasSelection As Boolean
            Get
                Return SelectionStart.Line >= 0 AndAlso SelectionStart.Column >= 0
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether this action is valid
        ''' </summary>
        Public ReadOnly Property IsValid As Boolean
            Get
                Return Type <> UndoActionType.eUnspecified
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether this is a text modification action
        ''' </summary>
        Public ReadOnly Property IsTextModification As Boolean
            Get
                Select Case Type
                    Case UndoActionType.eInsert, UndoActionType.eDelete, 
                         UndoActionType.eReplace, UndoActionType.ePaste, 
                         UndoActionType.eCut
                        Return True
                    Case Else
                        Return False
                End Select
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether this is a cursor-only action
        ''' </summary>
        Public ReadOnly Property IsCursorOnly As Boolean
            Get
                Return Type = UndoActionType.eCursorMove
            End Get
        End Property
        
        ' ===== Constructors =====
        
        ''' <summary>
        ''' Default constructor
        ''' </summary>
        Public Sub New()
        End Sub
        
        ''' <summary>
        ''' Create an insert action
        ''' </summary>
        Public Shared Function CreateInsert(vPosition As EditorPosition, vText As String, 
                                           vNewCursorPosition As EditorPosition) As UndoAction
            Dim lAction As New UndoAction()
            lAction.Type = UndoActionType.eInsert
            lAction.StartPosition = vPosition
            lAction.EndPosition = vNewCursorPosition
            lAction.Text = vText
            lAction.CursorPosition = vNewCursorPosition
            Return lAction
        End Function
        
        ''' <summary>
        ''' Create a delete action
        ''' </summary>
        Public Shared Function CreateDelete(vStartPosition As EditorPosition, 
                                           vEndPosition As EditorPosition, 
                                           vDeletedText As String,
                                           vNewCursorPosition As EditorPosition) As UndoAction
            Dim lAction As New UndoAction()
            lAction.Type = UndoActionType.eDelete
            lAction.StartPosition = vStartPosition
            lAction.EndPosition = vEndPosition
            lAction.Text = vDeletedText
            lAction.CursorPosition = vNewCursorPosition
            Return lAction
        End Function
        
        ''' <summary>
        ''' Create a replace action
        ''' </summary>
        Public Shared Function CreateReplace(vStartPosition As EditorPosition, 
                                            vEndPosition As EditorPosition,
                                            vOldText As String, vNewText As String,
                                            vNewCursorPosition As EditorPosition) As UndoAction
            Dim lAction As New UndoAction()
            lAction.Type = UndoActionType.eReplace
            lAction.StartPosition = vStartPosition
            lAction.EndPosition = vEndPosition
            lAction.OldText = vOldText
            lAction.NewText = vNewText
            lAction.CursorPosition = vNewCursorPosition
            Return lAction
        End Function
        
        ''' <summary>
        ''' Create a cursor move action
        ''' </summary>
        Public Shared Function CreateCursorMove(vFromPosition As EditorPosition, 
                                               vToPosition As EditorPosition) As UndoAction
            Dim lAction As New UndoAction()
            lAction.Type = UndoActionType.eCursorMove
            lAction.StartPosition = vFromPosition
            lAction.EndPosition = vToPosition
            lAction.CursorPosition = vFromPosition  ' Undo will restore to start position
            Return lAction
        End Function
        
        ''' <summary>
        ''' Clone this action
        ''' </summary>
        Public Function Clone() As UndoAction
            Dim lClone As New UndoAction()
            lClone.Type = Me.Type
            lClone.StartPosition = Me.StartPosition
            lClone.EndPosition = Me.EndPosition
            lClone.Text = Me.Text
            lClone.OldText = Me.OldText
            lClone.NewText = Me.NewText
            lClone.Timestamp = Me.Timestamp
            lClone.CursorPosition = Me.CursorPosition
            lClone.SelectionStart = Me.SelectionStart
            lClone.SelectionEnd = Me.SelectionEnd
            Return lClone
        End Function
        
        ''' <summary>
        ''' Get a string representation for debugging
        ''' </summary>
        Public Overrides Function ToString() As String
            Return $"{Type} at ({StartPosition.Line},{StartPosition.Column})-({EndPosition.Line},{EndPosition.Column})"
        End Function
        
    End Class
    
    ''' <summary>
    ''' Types of undo actions
    ''' </summary>
    Public Enum UndoActionType
        eUnspecified
        eInsert
        eDelete
        eReplace
        ePaste
        eCut
        eIndent
        eOutdent
        eComment
        eUncomment
        eCursorMove
        eDragDrop  
        eGroup
        eLastValue
    End Enum
    
End Namespace
