' Editors/CustomDrawingEditor.UndoRedo.vb - Fixed with RecordDelete inside UndoRedoManager
Imports Gtk
Imports Gdk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor


        Public Sub Undo() Implements IEditor.Undo
            pUndoRedoManager.Undo()
        End Sub

        Public Sub Redo() Implements IEditor.Redo
            pUndoRedoManager.Redo()
        End Sub

        
        ' ===== UndoRedoManager Class =====
        Public Class UndoRedoManager
            Implements IDisposable
            
            ' Action types for undo/redo
            Public Enum ActionType
                eUnspecified
                eInsert
                eDelete
                eInsertLine
                eDeleteLine
                eReplace
                eLastValue
            End Enum
            
            ' Undo action data structure
            Public Class UndoAction
                Public ActionType As UndoRedoManager.ActionType
                Public Line As Integer
                Public Column As Integer
                Public EndLine As Integer
                Public EndColumn As Integer
                Public Text As String
                Public DeletedText As String ' for Replace operations
                Public CursorLine As Integer
                Public CursorColumn As Integer
                
                Public Sub New(vType As ActionType, vLine As Integer, vColumn As Integer, vText As String)
                    ActionType = vType
                    Line = vLine
                    Column = vColumn
                    Text = vText
                End Sub
            End Class
            
            Private pEditor As CustomDrawingEditor
            Private pUndoStack As New Stack(Of UndoRedoManager.UndoAction)
            Private pRedoStack As New Stack(Of UndoRedoManager.UndoAction)
            Private pIsUndoingOrRedoing As Boolean = False
            Private pGroupingActions As Boolean = False
            Private pCurrentGroup As New List(Of UndoAction)
            Private pMaxUndoLevels As Integer = 100
            
            ' Events
            Public Event StateChanged()
            Public Event CanUndoChanged(vCanUndo As Boolean)
            Public Event CanRedoChanged(vCanRedo As Boolean)
            Public Event TextChanged(o As Object, e As EventArgs)
            
            Public Sub New(vEditor As CustomDrawingEditor)
                pEditor = vEditor
            End Sub
            
            Public ReadOnly Property CanUndo As Boolean
                Get
                    Return pUndoStack.Count > 0
                End Get
            End Property
            
            Public ReadOnly Property CanRedo As Boolean
                Get
                    Return pRedoStack.Count > 0
                End Get
            End Property
            
            Public Sub BeginUserAction()
                pGroupingActions = True
                pCurrentGroup.Clear()
            End Sub
            
            Public Sub EndUserAction()
                If pGroupingActions AndAlso pCurrentGroup.Count > 0 Then
                    ' Add all actions as a single group
                    For Each lAction In pCurrentGroup
                        AddUndoAction(lAction)
                    Next
                End If
                pGroupingActions = False
                pCurrentGroup.Clear()
            End Sub
            
            Private Sub AddUndoAction(vAction As UndoAction)
                pUndoStack.Push(vAction)
                pRedoStack.Clear()
                
                ' Limit undo stack size
                While pUndoStack.Count > pMaxUndoLevels
                    Dim lOldActions As New List(Of UndoAction)(pUndoStack)
                    pUndoStack.Clear()
                    For i As Integer = 1 To lOldActions.Count - 1
                        pUndoStack.Push(lOldActions(i))
                    Next
                End While
                
                RaiseEvent StateChanged()
                RaiseEvent CanUndoChanged(True)
                RaiseEvent CanRedoChanged(False)
            End Sub
            
            Public Sub RecordInsert(vLine As Integer, vColumn As Integer, vText As String)
                If pIsUndoingOrRedoing Then Return
                
                Dim lAction As New UndoAction(ActionType.eInsert, vLine, vColumn, vText)
                lAction.CursorLine = pEditor.pCursorLine
                lAction.CursorColumn = pEditor.pCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub
            
            Public Sub RecordDelete(vLine As Integer, vColumn As Integer, vDeletedText As String)
                If pIsUndoingOrRedoing Then Return
                
                Dim lAction As New UndoAction(ActionType.eDelete, vLine, vColumn, vDeletedText)
                lAction.CursorLine = pEditor.pCursorLine
                lAction.CursorColumn = pEditor.pCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub
            
            Public Sub RecordReplace(vLine As Integer, vColumn As Integer, vOldText As String, vNewText As String)
                If pIsUndoingOrRedoing Then Return
                
                Dim lAction As New UndoAction(ActionType.eReplace, vLine, vColumn, vNewText)
                lAction.DeletedText = vOldText
                lAction.CursorLine = pEditor.pCursorLine
                lAction.CursorColumn = pEditor.pCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub
            
            Public Function Undo() As Boolean
                If pUndoStack.Count = 0 Then Return  False
                
                pIsUndoingOrRedoing = True
                Try
                    Dim lAction As UndoAction = pUndoStack.Pop()
                    Dim lCursorLine As Integer = lAction.Line
                    Dim lCursorColumn As Integer = lAction.Column
                    
                    Select Case lAction.ActionType
                        Case ActionType.eInsert
                            ' Undo insert by deleting
                            pEditor.DeleteTextAt(lAction.Line, lAction.Column, lAction.Text.Length)
                            
                        Case ActionType.eDelete
                            ' Undo delete by inserting
                            pEditor.InsertTextAt(lAction.Line, lAction.Column, lAction.Text)
                            
                        Case ActionType.eReplace
                            ' Undo replace by replacing back
                            pEditor.ReplaceTextAt(lAction.Line, lAction.Column, 
                                                lAction.Text.Length, lAction.DeletedText)
                    End Select
                    
                    ' Restore cursor position
                    pEditor.SetCursorPosition(lCursorLine, lCursorColumn)
                    
                    ' Add to redo stack
                    pRedoStack.Push(lAction)
                    If lAction.ActionType = UndoRedoManager.ActionType.eInsertLine OrElse lAction.ActionType = UndoRedoManager.ActionType.eDeleteLine Then 
                        pEditor.ScheduleFullDocumentParse()
                    End If
                    ' Update UI
                    pEditor.pDrawingArea?.QueueDraw()
                    pEditor.IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                    
                    ' Notify state change
                    RaiseEvent StateChanged()
                    RaiseEvent CanUndoChanged(CanUndo)
                    RaiseEvent CanRedoChanged(CanRedo)
                    
                    Return True
                Finally
                    pIsUndoingOrRedoing = False
                End Try
                Return False
            End Function
            
            Public Function Redo() As Boolean 
                If pRedoStack.Count = 0 Then Return False
                
                pIsUndoingOrRedoing = True
                Try
                    Dim lAction As UndoAction = pRedoStack.Pop()
                    Dim lCursorLine As Integer = lAction.CursorLine
                    Dim lCursorColumn As Integer = lAction.CursorColumn
                    
                    Select Case lAction.ActionType
                        Case ActionType.eInsert
                            ' Redo insert
                            pEditor.InsertTextAt(lAction.Line, lAction.Column, lAction.Text)
                            
                        Case ActionType.eDelete
                            ' Redo delete
                            pEditor.DeleteTextAt(lAction.Line, lAction.Column, lAction.Text.Length)
                            
                        Case ActionType.eReplace
                            ' Redo replace
                            pEditor.ReplaceTextAt(lAction.Line, lAction.Column,
                                                lAction.DeletedText.Length, lAction.Text)
                    End Select
                    
                    ' Restore cursor position after action
                    pEditor.SetCursorPosition(lAction.CursorLine, lAction.CursorColumn)
                    
                    ' Add back to undo stack
                    pUndoStack.Push(lAction)
                    If lAction.ActionType = UndoRedoManager.ActionType.eInsertLine OrElse lAction.ActionType = UndoRedoManager.ActionType.eDeleteLine Then 
                        pEditor.ScheduleFullDocumentParse()
                    End If
                    ' Update UI
                    pEditor.pDrawingArea?.QueueDraw()
                    pEditor.IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs)
                    
                    ' Notify state change
                    RaiseEvent StateChanged()
                    RaiseEvent CanUndoChanged(CanUndo)
                    RaiseEvent CanRedoChanged(CanRedo)
                    
                    Return True
                    
                Finally
                    pIsUndoingOrRedoing = False
                End Try
            End Function
            
            Public Sub Clear()
                pUndoStack.Clear()
                pRedoStack.Clear()
                pCurrentGroup.Clear()
                pGroupingActions = False
                
                RaiseEvent StateChanged()
                RaiseEvent CanUndoChanged(False)
                RaiseEvent CanRedoChanged(False)
            End Sub
            
            Public Sub Dispose() Implements IDisposable.Dispose
                Try
                    Clear()
                    pEditor = Nothing
                Catch ex As Exception
                    Console.WriteLine($"UndoRedoManager.Dispose error: {ex.Message}")
                End Try
            End Sub

            Public Sub RecordInsertChar(vLine As Integer, vColumn As Integer, vChar As Char, vCursorLine As Integer, vCursorColumn As Integer)
                If pIsUndoingOrRedoing = True Then Return
                
                Dim lAction As New UndoRedoManager.UndoAction(UndoRedoManager.ActionType.eInsert, vLine, vColumn, vChar.ToString())
                lAction.CursorLine = vCursorLine
                lAction.CursorColumn = vCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub
            
            Public Sub RecordDeleteChar(vLine As Integer, vColumn As Integer, vDeletedChar As Char, vCursorLine As Integer, vCursorColumn As Integer)
                If pIsUndoingOrRedoing Then Return
                
                Dim lAction As New UndoRedoManager.UndoAction(UndoRedoManager.ActionType.eDelete, vLine, vColumn, vDeletedChar.ToString())
                lAction.CursorLine = vCursorLine
                lAction.CursorColumn = vCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub
            
            Public Sub RecordReplaceText(vLine As Integer, vColumn As Integer, vOldText As String, vNewText As String)
                If pIsUndoingOrRedoing Then Return
                
                Dim lAction As New UndoRedoManager.UndoAction(UndoRedoManager.ActionType.eReplace, vLine, vColumn, vNewText)
                lAction.DeletedText = vOldText
                lAction.CursorLine = pEditor.pCursorLine
                lAction.CursorColumn = pEditor.pCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub
            
            Public Sub RecordInsertLine(vLine As Integer, vText As String, vCursorLine As Integer, vCursorColumn As Integer)
                If pIsUndoingOrRedoing Then Return
                
                Dim lAction As New UndoRedoManager.UndoAction(UndoRedoManager.ActionType.eInsertLine, vLine, 0, vText)
                lAction.CursorLine = vCursorLine
                lAction.CursorColumn = vCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub
            
            Public Sub RecordDeleteLine(vLine As Integer, vText As String, vCursorLine As Integer, vCursorColumn As Integer)
                If pIsUndoingOrRedoing Then Return
                
                Dim lAction As New UndoRedoManager.UndoAction(UndoRedoManager.ActionType.eDeleteLine, vLine, 0, vText)
                lAction.CursorLine = vCursorLine
                lAction.CursorColumn = vCursorColumn
                
                If pGroupingActions Then
                    pCurrentGroup.Add(lAction)
                Else
                    AddUndoAction(lAction)
                End If
            End Sub

            ' RecordDeleteText - Record deletion of text at a specific position
            ' This is called when text is deleted from the editor
            Public Sub RecordDeleteText(vLine As Integer, vColumn As Integer, vDeletedText As String, vCursorLine As Integer, vCursorColumn As Integer)
                Try
                    ' Don't record if we're in the middle of an undo/redo operation
                    If pIsUndoingOrRedoing Then Return
                    
                    ' Don't record empty deletions
                    If String.IsNullOrEmpty(vDeletedText) Then Return
                    
                    ' Create a delete action
                    Dim lAction As New UndoAction(ActionType.eDelete, vLine, vColumn, vDeletedText)
                    
                    ' Store the cursor position after the deletion
                    lAction.CursorLine = vCursorLine
                    lAction.CursorColumn = vCursorColumn
                    
                    ' If we're grouping actions (like for multi-line delete), add to group
                    If pGroupingActions Then
                        pCurrentGroup.Add(lAction)
                    Else
                        ' Otherwise add as a single action
                        AddUndoAction(lAction)
                    End If
                    
                Catch ex As Exception
                    Console.WriteLine($"RecordDeleteText error: {ex.Message}")
                End Try
            End Sub
            
        End Class
        
        ' ===== Undo/Redo Helper Methods =====
        ' These methods are called by UndoRedoManager to perform operations without recording them
        
        ' InsertTextAt - Insert text at a specific position without recording for undo
        Friend Sub InsertTextAt(vLine As Integer, vColumn As Integer, vText As String)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If String.IsNullOrEmpty(vText) Then Return
                
                ' Get the line
                Dim lLine As String = pTextLines(vLine)
                
                ' Ensure column is within valid range
                If vColumn < 0 Then vColumn = 0
                If vColumn > lLine.Length Then vColumn = lLine.Length
                
                ' Check if text contains newlines
                If vText.Contains(vbLf) OrElse vText.Contains(vbCr) Then
                    ' Complex multi-line insert
                    InsertMultiLineTextAt(vLine, vColumn, vText)
                Else
                    ' Simple single-line insert
                    ' Update the line in SourceFileInfo
                    If pSourceFileInfo IsNot Nothing AndAlso vLine < pSourceFileInfo.TextLines.Count Then
                        Dim lLineText As String = pTextLines(vLine)
                        Dim lNewLineText As String = lLineText.Substring(0, vColumn) & vText & lLineText.Substring(vColumn)
                        pSourceFileInfo.TextLines(vLine) = lNewLineText
                        pSourceFileInfo.Content = GetAllText() ' Sync content
                    End If
                    pLineMetadata(vLine).MarkChanged()
                End If
                
                ' Update display
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
                ' Raise text changed event
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ' DeleteTextAt - Delete a specified number of characters at a position without recording for undo
        Friend Sub DeleteTextAt(vLine As Integer, vColumn As Integer, vLength As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If vLength <= 0 Then Return
                
                ' Start from the specified position
                Dim lCurrentLine As Integer = vLine
                Dim lCurrentColumn As Integer = vColumn
                Dim lRemainingLength As Integer = vLength
                
                While lRemainingLength > 0 AndAlso lCurrentLine < pLineCount
                    Dim lLine As String = pTextLines(lCurrentLine)
                    
                    ' Ensure column is within valid range
                    If lCurrentColumn < 0 Then lCurrentColumn = 0
                    If lCurrentColumn > lLine.Length Then lCurrentColumn = lLine.Length
                    
                    ' Calculate how many characters to delete from this line
                    Dim lCharsInLine As Integer = lLine.Length - lCurrentColumn
                    
                    If lCharsInLine >= lRemainingLength Then
                        ' All remaining characters to delete are in this line
                        pTextLines(lCurrentLine) = lLine.Remove(lCurrentColumn, lRemainingLength)
                        pLineMetadata(lCurrentLine).MarkChanged()
                        lRemainingLength = 0
                    Else
                        ' Delete to end of line and continue to next line
                        If lCurrentLine < pLineCount - 1 Then
                            ' Join with next line
                            Dim lNextLine As String = pTextLines(lCurrentLine + 1)
                            pTextLines(lCurrentLine) = lLine.Substring(0, lCurrentColumn) & lNextLine
                            pLineMetadata(lCurrentLine).MarkChanged()
                            
                            ' Remove the next line
                            RemoveLines(lCurrentLine + 1, 1)
                            
                            ' Account for the newline character that was removed
                            lRemainingLength -= (lCharsInLine + 1)
                        Else
                            ' At last line, just truncate
                            pTextLines(lCurrentLine) = lLine.Substring(0, lCurrentColumn)
                            pLineMetadata(lCurrentLine).MarkChanged()
                            lRemainingLength = 0
                        End If
                    End If
                    
                    ' Move to next position
                    lCurrentColumn = 0
                End While
                
                ' Update display
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
                ' Raise text changed event
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"DeleteTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ReplaceTextAt - Replace text at a specific position without recording for undo
        Friend Sub ReplaceTextAt(vLine As Integer, vColumn As Integer, vOldLength As Integer, vNewText As String)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' First delete the old text
                If vOldLength > 0 Then
                    DeleteTextAt(vLine, vColumn, vOldLength)
                End If
                
                ' Then insert the new text
                If Not String.IsNullOrEmpty(vNewText) Then
                    InsertTextAt(vLine, vColumn, vNewText)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods for Multi-line Operations =====
        
        ' Insert multi-line text at a specific position
        Private Sub InsertMultiLineTextAt(vLine As Integer, vColumn As Integer, vText As String)
            Try
                ' Split the text into lines
                Dim lNewLines As String() = vText.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                If lNewLines.Length = 0 Then Return
                
                ' Get the current line
                Dim lCurrentLine As String = pTextLines(vLine)
                
                ' Split current line at insertion point
                Dim lBeforeCursor As String = lCurrentLine.Substring(0, Math.Min(vColumn, lCurrentLine.Length))
                Dim lAfterCursor As String = If(vColumn < lCurrentLine.Length, 
                                               lCurrentLine.Substring(vColumn), 
                                               "")
                
                ' First line: combine with text before cursor
                pTextLines(vLine) = lBeforeCursor & lNewLines(0)
                pLineMetadata(vLine).MarkChanged()
                
                ' Insert middle lines
                Dim lInsertPosition As Integer = vLine + 1
                For i As Integer = 1 To lNewLines.Length - 2
                    InsertLine(lInsertPosition, lNewLines(i))
                    lInsertPosition += 1
                Next
                
                ' Last line: combine with text after cursor
                If lNewLines.Length > 1 Then
                    InsertLine(lInsertPosition, lNewLines(lNewLines.Length - 1) & lAfterCursor)
                Else
                    ' Single line with newline characters - append the after cursor text
                    pTextLines(vLine) &= lAfterCursor
                End If
                
                ' Update line count
                pLineCount = pTextLines.Count
                
            Catch ex As Exception
                Console.WriteLine($"InsertMultiLineTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ' Insert a new line at a specific position
        Private Sub InsertLine(vPosition As Integer, vText As String)
            Try
                ' Ensure position is valid
                If vPosition < 0 Then vPosition = 0
                If vPosition > pLineCount Then vPosition = pLineCount
                
                ' Insert the line
                pTextLines.Insert(vPosition, vText)
                
                ' Handle metadata arrays
                Dim lOldMetadata As LineMetadata() = pLineMetadata
                Dim lOldCharacterColors()() As CharacterColorInfo = pCharacterColors
                
                ReDim pLineMetadata(pTextLines.Count - 1)
                ReDim pCharacterColors(pTextLines.Count - 1)
                
                ' Copy metadata and colors before insertion point
                For i As Integer = 0 To vPosition - 1
                    If i < lOldMetadata.Length Then
                        pLineMetadata(i) = lOldMetadata(i)
                    Else
                        pLineMetadata(i) = New LineMetadata()
                    End If
                    
                    If lOldCharacterColors IsNot Nothing AndAlso i < lOldCharacterColors.Length Then
                        pCharacterColors(i) = lOldCharacterColors(i)
                    Else
                        pCharacterColors(i) = New CharacterColorInfo() {}
                    End If
                Next
                
                ' Insert new metadata and colors
                pLineMetadata(vPosition) = New LineMetadata()
                pLineMetadata(vPosition).MarkChanged()
                pCharacterColors(vPosition) = New CharacterColorInfo() {}
                
                ' Copy metadata and colors after insertion point
                For i As Integer = vPosition To lOldMetadata.Length - 1
                    If i + 1 < pLineMetadata.Length Then
                        pLineMetadata(i + 1) = lOldMetadata(i)
                    End If
                    
                    If lOldCharacterColors IsNot Nothing AndAlso i < lOldCharacterColors.Length AndAlso i + 1 < pCharacterColors.Length Then
                        pCharacterColors(i + 1) = lOldCharacterColors(i)
                    End If
                Next
                
                ' Update line count
                pLineCount = pTextLines.Count
                
            Catch ex As Exception
                Console.WriteLine($"InsertLine error: {ex.Message}")
            End Try
        End Sub
        




        
    End Class
    
End Namespace