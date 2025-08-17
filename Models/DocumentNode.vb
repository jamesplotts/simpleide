' Models/DocumentNode.vb - Document graph node representation
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Models
    
    ' Represents a node in the document structure graph
    Public Class DocumentNode
        
        Public Property NodeId As String = ""
        Public Property Name As String = ""
        Public Property NodeType As CodeNodeType = CodeNodeType.eUnspecified
        Public Property StartLine As Integer = 0      ' 0-based Line number
        Public Property EndLine As Integer = 0        ' 0-based Line number  
        Public Property StartColumn As Integer = 0
        Public Property EndColumn As Integer = 0
        Public Property Parent As DocumentNode = Nothing
        Public Property Children As New List(Of DocumentNode)()
        Public Property Attributes As New Dictionary(Of String, String)()
        Public Property FilePath as String = ""
        Public Property IsPartial As Boolean = False
        Public Property IsPublic As Boolean = True
        Public Property IsPrivate As Boolean = False
        Public Property IsProtected As Boolean = False
        Public Property IsFriend As Boolean = False
        Public Property IsShared As Boolean = False
        Public Property IsOverridable As Boolean = False
        Public Property IsOverrides As Boolean = False
        Public Property IsMustOverride As Boolean = False
        Public Property IsNotOverridable As Boolean = False
        Public Property IsMustInherit as Boolean = false
        Public Property IsNotInheritable as Boolean = false
        Public Property IsReadOnly As Boolean = False
        Public Property IsWriteOnly As Boolean = False
        Public Property IsConst As Boolean = False
        Public Property IsWithEvents As Boolean = False
        Public Property Visibility As SyntaxNode.eVisibility = SyntaxNode.eVisibility.ePublic
        
        ' Navigation helpers
        Public ReadOnly Property FullName As String
            Get
                If Parent IsNot Nothing AndAlso Parent.NodeType <> CodeNodeType.eNamespace Then
                    Return $"{Parent.FullName}.{Name}"
                Else
                    Return Name
                End If
            End Get
        End Property
        
        Public ReadOnly Property DisplayText As String
            Get
                Select Case NodeType
                    Case CodeNodeType.eClass
                        Return $"Class {Name}"
                    Case CodeNodeType.eModule
                        Return $"Module {Name}"
                    Case CodeNodeType.eInterface
                        Return $"Interface {Name}"
                    Case CodeNodeType.eMethod
                        Return $"Sub {Name}"
                    Case CodeNodeType.eFunction
                        Return $"Function {Name}"
                    Case CodeNodeType.eProperty
                        Return $"Property {Name}"
                    Case CodeNodeType.eField
                        Return $"field {Name}"
                    Case CodeNodeType.eEvent
                        Return $"Event {Name}"
                    Case Else
                        Return Name
                End Select
            End Get
        End Property
        
        ' Check if a position is within this node
        Public Function ContainsPosition(vLine As Integer, vColumn As Integer) As Boolean
            If vLine < StartLine OrElse vLine > EndLine Then
                Return False
            End If
            
            If vLine = StartLine AndAlso vColumn < StartColumn Then
                Return False
            End If
            
            If vLine = EndLine AndAlso vColumn > EndColumn Then
                Return False
            End If
            
            Return True
        End Function
        
    End Class
    
    ' Result of parsing a document
    Public Class ParseResult
        
        Public Property Objects As New List(Of CodeObject)()
        Public Property members As New List(Of CodeMember)()
        Public Property DocumentNodes As New Dictionary(Of String, DocumentNode)()
        Public Property RootNodes As New List(Of DocumentNode)()
        Public LineMetadata() As Models.LineMetadata = {}
        Public Property Errors As New List(Of ParseError)()
        Public Property Warnings As New List(Of ParseError)()
        
        ' Statistics
        Public ReadOnly Property NodeCount As Integer
            Get
                Return DocumentNodes.Count
            End Get
        End Property
        
        Public ReadOnly Property HasErrors As Boolean
            Get
                Return Errors.Count > 0
            End Get
        End Property

        Public Sub ResizeLineMetadata(vNewSize As Integer)
            ReDim LineMetadata(vNewSize)
        End Sub
        
    End Class
    
    ' Undo/Redo action for text changes
    Public Class UndoAction
        
        Public Property ActionType As UndoActionType = UndoActionType.eUnspecified
        Public Property StartLine As Integer = 0
        Public Property StartColumn As Integer = 0
        Public Property EndLine As Integer = 0
        Public Property EndColumn As Integer = 0
        Public Property OldText As String = ""
        Public Property NewText As String = ""
        Public Property Timestamp As DateTime = DateTime.Now
        
        ' For cursor position restoration
        Public Property CursorLine As Integer = 0
        Public Property CursorColumn As Integer = 0
        Public Property SelectionStartLine As Integer = -1
        Public Property SelectionStartColumn As Integer = -1
        Public Property SelectionEndLine As Integer = -1
        Public Property SelectionEndColumn As Integer = -1
        
    End Class
    
    ' Group of related undo actions
    Public Class UndoGroup
        
        Public Property Actions As New List(Of UndoAction)()
        Public Property GroupType As UndoGroupType = UndoGroupType.eUnspecified
        Public Property StartTime As DateTime = DateTime.Now
        Public Property EndTime As DateTime = DateTime.Now
        
        ' Add an action to the group
        Public Sub AddAction(vAction As UndoAction)
            Actions.Add(vAction)
            EndTime = DateTime.Now
        End Sub
        
        ' Check if group can accept more actions
        Public Function CanMergeAction(vAction As UndoAction) As Boolean
            If Actions.Count = 0 Then Return True
            
            ' Check time threshold (500ms)
            If (vAction.Timestamp - EndTime).TotalMilliseconds > 500 Then
                Return False
            End If
            
            ' Check if action is contiguous with last action
            Dim lLastAction As UndoAction = Actions(Actions.Count - 1)
            
            ' For character typing, check if positions are adjacent
            If GroupType = UndoGroupType.eTyping Then
                Return vAction.StartLine = lLastAction.EndLine AndAlso _
                       vAction.StartColumn = lLastAction.EndColumn
            End If
            
            Return True
        End Function
        
    End Class
    
    ' Types of undo actions
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
        eLastValue
    End Enum
    
    ' Types of undo groups
    Public Enum UndoGroupType
        eUnspecified
        eTyping
        eDeletion
        ePaste
        eIndentation
        eComment
        eReplace
        eLastValue
    End Enum
    
End Namespace

