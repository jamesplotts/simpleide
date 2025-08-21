' Editors/CustomDrawingEditor.Editing.vb - Text editing operations with fixed InsertCharacter
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Text
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' CodeSense events
        Public Event CodeSenseRequested(sender As Object, Context As CodeSenseContext)
        Public Event CodeSenseCancelled(sender As Object, e As EventArgs)

        ' ===== Character At Position =====
        Private Sub DeleteCharacterAt(vLine As Integer, vColumn As Integer)
            Try
                If pIsReadOnly Then Return
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(vLine)
                If vColumn < 0 OrElse vColumn >= lLine.Length Then Return
                
                ' Get the character to be deleted for undo recording
                Dim lDeletedChar As Char = lLine(vColumn)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordDeleteChar(vLine, vColumn, lDeletedChar, vLine, vColumn)
                End If
                
                ' Remove the character
                pTextLines(vLine) = lLine.Remove(vColumn, 1)
                pLineMetadata(vLine).MarkChanged()
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Update display
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteCharacterAt error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Text Editing Operations =====
'        
'        Private Sub InsertCharacter(vChar As Char)
'            Try
'                If pIsReadOnly Then Return
'                
'                ' Ensure we have a valid line
'                If pCursorLine >= pLineCount Then
'                    ' Add empty lines if needed
'                    While pLineCount <= pCursorLine
'                        AddNewLine("")
'                    End While
'                End If
'                
'                ' Record for undo
'                If pUndoRedoManager IsNot Nothing Then
'                    pUndoRedoManager.RecordInsertChar(pCursorLine, pCursorColumn, vChar, pCursorLine, pCursorColumn + 1)
'                End If
'                
'                ' Insert character at cursor position
'                Dim lLine As String = pTextLines(pCursorLine)
'                If pCursorColumn <= lLine.Length Then
'                    pTextLines(pCursorLine) = lLine.Insert(pCursorColumn, vChar)
'                    
'                    ' Mark line as changed
'                    pLineMetadata(pCursorLine).MarkChanged()
'                    
'                    ' Move cursor
'                    SetCursorPosition(pCursorLine, pCursorColumn + 1)
'                    
'                    ' Mark as modified
'                    IsModified = True
'                    RaiseEvent TextChanged(Me, New EventArgs)
'                    
'                    ' FIXED: Ensure proper redraw of the current line
'                    InvalidateLine(pCursorLine)
'                    
'                    ' Also queue a full redraw to ensure everything is updated
'                    pDrawingArea.QueueDraw()
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"InsertCharacter error: {ex.Message}")
'            End Try
'        End Sub
        
        Private Sub InsertNewLine()
            Try
                If pIsReadOnly Then Return
                
                ' Ensure we have a valid line
                If pCursorLine >= pLineCount Then
                    AddNewLine("")
                    Return
                End If
                
                ' Split current line at cursor position
                Dim lCurrentLine As String = pTextLines(pCursorLine)
                Dim lBeforeCursor As String = lCurrentLine.Substring(0, Math.Min(pCursorColumn, lCurrentLine.Length))
                Dim lAfterCursor As String = If(pCursorColumn < lCurrentLine.Length, 
                                                lCurrentLine.Substring(pCursorColumn), "")
                
                ' Save the colors for the part after cursor (if any)
                Dim lAfterCursorColors() As CharacterColorInfo = Nothing
                If pCharacterColors IsNot Nothing AndAlso pCursorLine < pCharacterColors.Length AndAlso 
                   pCharacterColors(pCursorLine) IsNot Nothing AndAlso pCursorColumn < lCurrentLine.Length Then
                    Dim lOriginalColors() As CharacterColorInfo = pCharacterColors(pCursorLine)
                    If lOriginalColors.Length > pCursorColumn Then
                        ' Copy colors for the part that will move to the new line
                        ReDim lAfterCursorColors(lAfterCursor.Length - 1)
                        For i As Integer = 0 To lAfterCursor.Length - 1
                            If pCursorColumn + i < lOriginalColors.Length Then
                                lAfterCursorColors(i) = lOriginalColors(pCursorColumn + i)
                            Else
                                lAfterCursorColors(i) = New CharacterColorInfo()
                            End If
                        Next
                    End If
                End If
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                    pUndoRedoManager.RecordReplaceText(pCursorLine, 0, lCurrentLine, lBeforeCursor)
                    pUndoRedoManager.RecordInsertLine(pCursorLine + 1, lAfterCursor, pCursorLine + 1, 0)
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Update current line
                pTextLines(pCursorLine) = lBeforeCursor
                pLineMetadata(pCursorLine).MarkChanged()
                
                ' Resize color array for current line
                If pCharacterColors IsNot Nothing AndAlso pCursorLine < pCharacterColors.Length AndAlso
                   pCharacterColors(pCursorLine) IsNot Nothing Then
                    If lBeforeCursor.Length > 0 Then
                        ReDim Preserve pCharacterColors(pCursorLine)(lBeforeCursor.Length - 1)
                    Else
                        pCharacterColors(pCursorLine) = New CharacterColorInfo() {}
                    End If
                End If
                
                ' Insert new line
                InsertLineAt(pCursorLine + 1, lAfterCursor)
                
                ' Set colors for the new line if we saved them
                If lAfterCursorColors IsNot Nothing AndAlso pCursorLine + 1 < pCharacterColors.Length Then
                    pCharacterColors(pCursorLine + 1) = lAfterCursorColors
                End If
                
                ' Move cursor to beginning of next line
                SetCursorPosition(pCursorLine + 1, 0)
                
                ' Apply auto-indent if enabled
                If pSettingsManager IsNot Nothing AndAlso pSettingsManager.AutoIndent Then
                    ApplyAutoIndent(pCursorLine)
                End If
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"InsertNewLine error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub DeleteBackward()
            Try
                If pIsReadOnly Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                Else
                    If pCursorColumn > 0 Then
                        ' Delete character before cursor
                        Dim lLine As String = pTextLines(pCursorLine)
                        Dim lDeletedChar As Char = lLine(pCursorColumn - 1)
                        
                        ' Record for undo
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.RecordDeleteChar(pCursorLine, pCursorColumn - 1, lDeletedChar, pCursorLine, pCursorColumn - 1)
                        End If
                        
                        pTextLines(pCursorLine) = lLine.Remove(pCursorColumn - 1, 1)
                        pLineMetadata(pCursorLine).MarkChanged()
                        
                        ' Move cursor back
                        SetCursorPosition(pCursorLine, pCursorColumn - 1)
                        
                        IsModified = True
                        RaiseEvent TextChanged(Me, New EventArgs)
                    ElseIf pCursorLine > 0 Then
                        ' Join with previous line
                        Dim lPrevLine As String = pTextLines(pCursorLine - 1)
                        Dim lCurrentLine As String = pTextLines(pCursorLine)
                        Dim lNewColumn As Integer = lPrevLine.Length
                        
                        ' Record for undo
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.BeginUserAction()
                            pUndoRedoManager.RecordReplaceText(pCursorLine - 1, 0, lPrevLine, lPrevLine & lCurrentLine)
                            pUndoRedoManager.RecordDeleteLine(pCursorLine, lCurrentLine, pCursorLine - 1, lNewColumn)
                            pUndoRedoManager.EndUserAction()
                        End If
                        
                        ' Combine lines
                        pTextLines(pCursorLine - 1) = lPrevLine & lCurrentLine
                        pLineMetadata(pCursorLine - 1).MarkChanged()
                        
                        ' Remove current line
                        RemoveLines(pCursorLine, 1)
                        
                        ' Move cursor to join position
                        SetCursorPosition(pCursorLine - 1, lNewColumn)
                        
                        IsModified = True
                        RaiseEvent TextChanged(Me, New EventArgs)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteBackward error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub DeleteForward()
            Try
                If pIsReadOnly Then Return
                
                If pHasSelection Then
                    DeleteSelection()
                Else
                    If pCursorColumn < pTextLines(pCursorLine).Length Then
                        ' Delete character at cursor
                        DeleteCharacterAt(pCursorLine, pCursorColumn)
                    ElseIf pCursorLine < pLineCount - 1 Then
                        ' Join with next line
                        Dim lCurrentLine As String = pTextLines(pCursorLine)
                        Dim lNextLine As String = pTextLines(pCursorLine + 1)
                        
                        ' Record for undo
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.BeginUserAction()
                            pUndoRedoManager.RecordReplaceText(pCursorLine, 0, lCurrentLine, lCurrentLine & lNextLine)
                            pUndoRedoManager.RecordDeleteLine(pCursorLine + 1, lNextLine, pCursorLine, pCursorColumn)
                            pUndoRedoManager.EndUserAction()
                        End If
                        
                        ' Combine lines
                        pTextLines(pCursorLine) = lCurrentLine & lNextLine
                        pLineMetadata(pCursorLine).MarkChanged()
                        
                        ' Remove next line
                        RemoveLines(pCursorLine + 1, 1)
                        
                        IsModified = True
                        RaiseEvent TextChanged(Me, New EventArgs)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteForward error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub InsertText(vText As String) Implements IEditor.InsertText
            Try
                If pIsReadOnly OrElse String.IsNullOrEmpty(vText) Then Return
                
                ' Begin undo group for multi-character insert
                If pUndoRedoManager IsNot Nothing AndAlso vText.Length > 1 Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Process each character/line
                For Each lChar As Char In vText
                    If lChar = vbLf OrElse lChar = vbCr Then
                        ' Skip CR in CRLF pairs
                        If lChar = vbCr AndAlso vText.IndexOf(lChar) < vText.Length - 1 AndAlso vText(vText.IndexOf(lChar) + 1) = vbLf Then
                            Continue For
                        End If
                        InsertNewLine()
                    Else
                        InsertCharacter(lChar)
                    End If
                Next
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing AndAlso vText.Length > 1 Then
                    pUndoRedoManager.EndUserAction()
                End If
                If vText.IndexOf(Environment.NewLine) >= 0 Then ScheduleFullDocumentParse()
            Catch ex As Exception
                Console.WriteLine($"InsertText error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub ReplaceText(vText As String) Implements IEditor.ReplaceText
            Try
                If pIsReadOnly Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Delete current selection or all text
                If pHasSelection Then
                    DeleteSelection()
                Else
                    SelectAll()
                    DeleteSelection()
                End If
                
                ' Insert new text
                InsertText(vText)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                If vText.IndexOf(Environment.NewLine) >= 0 Then ScheduleFullDocumentParse()
            Catch ex As Exception
                Console.WriteLine($"ReplaceText error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        Private Sub NormalizeSelection(ByRef vStartLine As Integer, ByRef vStartColumn As Integer, 
                                       ByRef vEndLine As Integer, ByRef vEndColumn As Integer)
            Try
                ' Swap if selection is backwards
                If vEndLine < vStartLine OrElse (vEndLine = vStartLine AndAlso vEndColumn < vStartColumn) Then
                    ' Swap lines
                    Dim lTempLine As Integer = vStartLine
                    vStartLine = vEndLine
                    vEndLine = lTempLine
                    
                    ' Swap columns
                    Dim lTempColumn As Integer = vStartColumn
                    vStartColumn = vEndColumn
                    vEndColumn = lTempColumn
                End If
                
            Catch ex As Exception
                Console.WriteLine($"NormalizeSelection error: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateFontMetrics()
            Try
                ' Create a temporary surface to get a Cairo context for font measurement
                Using lSurface As Cairo.ImageSurface = New Cairo.ImageSurface(Cairo.Format.Argb32, 1, 1)
                    Using lContext As Cairo.Context = New Cairo.Context(lSurface)
                        ' Dispose old font metrics if exists
                        If pFontMetrics IsNot Nothing Then
                            pFontMetrics.Dispose()
                        End If
                        
                        ' Create new font metrics
                        pFontMetrics = New FontMetrics(pFontDescription, lContext)
                        
                        ' Update editor metrics from font metrics
                        pCharWidth = pFontMetrics.CharWidth
                        pLineHeight = pFontMetrics.CharHeight
                        
                        ' Log the update
                        Console.WriteLine($"UpdateFontMetrics: CharWidth={pCharWidth}, LineHeight={pLineHeight}")
                    End Using
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFontMetrics error: {ex.Message}")
                ' Set defaults if font metrics fail
                pCharWidth = 10
                pLineHeight = 20
            End Try
        End Sub
        
        ' ===== Missing IEditor implementations =====
        
        Public Sub InsertTextAtPosition(vLine As Integer, vColumn As Integer, vText As String) Implements IEditor.InsertTextAtPosition
            Try
                If pIsReadOnly Then Return
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                ' Begin undo group if inserting multiple characters
                If pUndoRedoManager IsNot Nothing AndAlso vText.Length > 1 Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Save current cursor position
                Dim lOldLine As Integer = pCursorLine
                Dim lOldColumn As Integer = pCursorColumn
                
                ' Move cursor to insertion point
                SetCursorPosition(vLine, vColumn)
                
                ' Insert the text using existing InsertText method
                InsertText(vText)
                
                ' Restore cursor if needed (optional based on requirements)
                ' SetCursorPosition(lOldLine, lOldColumn)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing AndAlso vText.Length > 1 Then
                    pUndoRedoManager.EndUserAction()
                End If
                If vText.IndexOf(Environment.NewLine) >= 0 Then ScheduleFullDocumentParse()
            Catch ex As Exception
                Console.WriteLine($"InsertTextAtPosition error: {ex.Message}")
            End Try
        End Sub

        Public Sub ReplaceSelection(vText As String)  Implements IEditor.ReplaceSelection
            Try
                If pIsReadOnly Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Delete current selection if any
                If pHasSelection Then
                    DeleteSelection()
                End If
                
                ' Insert new text
                InsertText(vText)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                If vText.IndexOf(Environment.NewLine) >= 0 Then ScheduleFullDocumentParse()
            Catch ex As Exception
                Console.WriteLine($"ReplaceSelection error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ApplyAutoIndent(vLine As Integer)
            Try
                If vLine <= 0 OrElse vLine >= pLineCount Then Return
                
                ' Get indentation from previous line
                Dim lPrevLine As String = pTextLines(vLine - 1)
                Dim lIndent As Integer = 0
                
                For Each lChar In lPrevLine
                    If lChar = " "c OrElse lChar = vbTab Then
                        lIndent += 1
                    Else
                        Exit For
                    End If
                Next
                
                If lIndent > 0 Then
                    Dim lIndentText As String = lPrevLine.Substring(0, lIndent)
                    pTextLines(vLine) = lIndentText & pTextLines(vLine)
                    SetCursorPosition(vLine, lIndent)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyAutoIndent error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InsertLineAt(vLineIndex As Integer, vText As String)
            Try
                ' Validate index
                If vLineIndex < 0 Then vLineIndex = 0
                If vLineIndex > pLineCount Then vLineIndex = pLineCount
                
                ' Insert the line in the list
                pTextLines.Insert(vLineIndex, vText)
                pLineCount = pTextLines.Count
                
                ' Preserve and shift existing metadata and colors
                Dim lOldMetadata() As LineMetadata = pLineMetadata
                Dim lOldCharacterColors()() As CharacterColorInfo = pCharacterColors
                
                ReDim pLineMetadata(pLineCount - 1)
                ReDim pCharacterColors(pLineCount - 1)
                
                ' Copy items before insertion point
                For i As Integer = 0 To vLineIndex - 1
                    If lOldMetadata IsNot Nothing AndAlso i < lOldMetadata.Length Then
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
                
                ' Initialize new line
                pLineMetadata(vLineIndex) = New LineMetadata()
                pCharacterColors(vLineIndex) = New CharacterColorInfo() {}
                
                ' Shift items after insertion point
                For i As Integer = vLineIndex To lOldMetadata.Length - 1
                    If i + 1 < pLineMetadata.Length Then
                        pLineMetadata(i + 1) = lOldMetadata(i)
                    End If
                    
                    If lOldCharacterColors IsNot Nothing AndAlso i < lOldCharacterColors.Length AndAlso i + 1 < pCharacterColors.Length Then
                        pCharacterColors(i + 1) = lOldCharacterColors(i)
                    End If
                Next
                
                ' Update line numbers display width if needed
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"InsertLineAt error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub RemoveLines(vStartLine As Integer, vCount As Integer)
            Try
                If vStartLine < 0 OrElse vStartLine >= pLineCount OrElse vCount <= 0 Then Return
                
                ' Adjust count if it exceeds available lines
                If vStartLine + vCount > pLineCount Then
                    vCount = pLineCount - vStartLine
                End If
                
                ' Remove lines from the List (this supports RemoveAt)
                For i As Integer = 1 To vCount
                    pTextLines.RemoveAt(vStartLine)
                Next
                
                ' Update line count
                pLineCount = pTextLines.Count
                
                ' Handle the arrays (pLineMetadata and pCharacterColors)
                If pLineCount > 0 Then
                    ' Create new arrays with the correct size
                    Dim lNewLineMetadata(pLineCount - 1) As LineMetadata
                    Dim lNewCharacterColors(pLineCount - 1)() As CharacterColorInfo
                    
                    ' Copy elements before the removed range
                    For i As Integer = 0 To vStartLine - 1
                        If i < pLineMetadata.Length Then
                            lNewLineMetadata(i) = pLineMetadata(i)
                        Else
                            lNewLineMetadata(i) = New LineMetadata()
                        End If
                        
                        If i < pCharacterColors.Length Then
                            lNewCharacterColors(i) = pCharacterColors(i)
                        Else
                            lNewCharacterColors(i) = New CharacterColorInfo() {}
                        End If
                    Next
                    
                    ' Copy elements after the removed range
                    Dim lSourceIndex As Integer = vStartLine + vCount
                    For i As Integer = vStartLine To pLineCount - 1
                        If lSourceIndex < pLineMetadata.Length Then
                            lNewLineMetadata(i) = pLineMetadata(lSourceIndex)
                        Else
                            lNewLineMetadata(i) = New LineMetadata()
                        End If
                        
                        If lSourceIndex < pCharacterColors.Length Then
                            lNewCharacterColors(i) = pCharacterColors(lSourceIndex)
                        Else
                            lNewCharacterColors(i) = New CharacterColorInfo() {}
                        End If
                        
                        lSourceIndex += 1
                    Next
                    
                    ' Replace the old arrays with the new ones
                    pLineMetadata = lNewLineMetadata
                    pCharacterColors = lNewCharacterColors
                Else
                    ' Ensure at least one line exists
                    pTextLines.Add("")
                    pLineCount = 1
                    ReDim pLineMetadata(0)
                    ReDim pCharacterColors(0)
                    pLineMetadata(0) = New LineMetadata()
                    pCharacterColors(0) = New CharacterColorInfo() {}
                End If
                
                ' Update display
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"RemoveLines error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AddNewLine(vText As String)
            Try
                InsertLineAt(pLineCount, vText)
            Catch ex As Exception
                Console.WriteLine($"AddNewLine error: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateAfterDeleteSelection(lStartLine As Integer, lStartColumn As Integer, lEndLine As Integer, lEndColumn As Integer)
            Try
                ' Create the new combined line
                Dim lStartLineText As String = pTextLines(lStartLine).Substring(0, lStartColumn)
                Dim lEndLineText As String = If(lEndLine < pLineCount, pTextLines(lEndLine).Substring(lEndColumn), "")
                Dim lCombinedLine As String = lStartLineText & lEndLineText
                
                ' Update the start line with combined text
                pTextLines(lStartLine) = lCombinedLine
                pLineMetadata(lStartLine).MarkChanged()
                
                ' Remove the lines between start and end (if any)
                If lEndLine > lStartLine Then
                    ' Use RemoveLines helper which properly handles metadata arrays
                    RemoveLines(lStartLine + 1, lEndLine - lStartLine)
                End If
                
                ' Update line count
                pLineCount = pTextLines.Count
                
            Catch ex As Exception
                Console.WriteLine($"UpdateAfterDeleteSelection error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
