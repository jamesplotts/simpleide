' Editors/CustomDrawingEditor.Helpers.vb - Helper methods for text manipulation
Imports Gtk
Imports Gdk
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
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Inserts a line at the specified position
        ''' </summary>
        Friend Sub InsertLineAt(vLineNumber As Integer, vText As String)
            Try
                If vLineNumber < 0 OrElse vLineNumber > pLineCount Then Return
                
                ' Insert the line
                pTextLines.Insert(vLineNumber, vText)
                
                ' Insert metadata
                InsertLineMetadata(vLineNumber)
                
                ' Update line count
                pLineCount += 1
                
                ' Update line number width if needed
                UpdateLineNumberWidth()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertLineAt error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Removes lines starting at the specified position
        ''' </summary>
        Friend Sub RemoveLines(vStartLine As Integer, vCount As Integer)
            Try
                If vStartLine < 0 OrElse vStartLine >= pLineCount OrElse vCount <= 0 Then Return
                
                ' Calculate actual count to remove
                Dim lActualCount As Integer = Math.Min(vCount, pLineCount - vStartLine)
                
                ' Remove the lines
                For i As Integer = 0 To lActualCount - 1
                    pTextLines.RemoveAt(vStartLine)
                    RemoveLineMetadata(vStartLine)
                Next
                
                ' Update line count
                pLineCount -= lActualCount
                
                ' Ensure we have at least one line
                If pLineCount = 0 Then
                    AddNewLine("")
                End If
                
                ' Update line number width if needed
                UpdateLineNumberWidth()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"RemoveLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes a single line and records it for undo
        ''' </summary>
        Public Sub DeleteLine(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pIsReadOnly Then Return
                
                ' Get the line text for undo
                Dim lLineText As String = pTextLines(vLine)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    Dim lCursorPos As New EditorPosition(Math.Max(0, vLine - 1), 0)
                    pUndoRedoManager.RecordDeleteLine(vLine, lLineText, lCursorPos)
                End If
                
                ' Remove the line
                RemoveLines(vLine, 1)
                
                ' Update cursor position if needed
                If pCursorLine >= pLineCount Then
                    SetCursorPosition(pLineCount - 1, 0)
                ElseIf pCursorLine > vLine Then
                    SetCursorPosition(pCursorLine - 1, pCursorColumn)
                End If
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"DeleteLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Splits a line at the specified position
        ''' </summary>
        Public Sub SplitLine(vLine As Integer, vColumn As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pIsReadOnly Then Return
                
                Dim lLine As String = pTextLines(vLine)
                Dim lFirstPart As String = If(vColumn > 0, lLine.Substring(0, Math.Min(vColumn, lLine.Length)), "")
                Dim lSecondPart As String = If(vColumn < lLine.Length, lLine.Substring(vColumn), "")
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                    
                    ' Record replacing the current line
                    Dim lReplaceStart As New EditorPosition(vLine, 0)
                    Dim lReplaceEnd As New EditorPosition(vLine, lLine.Length)
                    Dim lNewCursorPos As New EditorPosition(vLine + 1, 0)
                    pUndoRedoManager.RecordReplaceText(lReplaceStart, lReplaceEnd, lLine, lFirstPart, lNewCursorPos)
                    
                    ' Record inserting the new line
                    Dim lInsertPos As New EditorPosition(vLine + 1, 0)
                    pUndoRedoManager.RecordInsertLine(lInsertPos, lSecondPart, lNewCursorPos)
                    
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Update the first line
                pTextLines(vLine) = lFirstPart
                pLineMetadata(vLine).MarkChanged()
                
                ' Insert the second line
                InsertLineAt(vLine + 1, lSecondPart)
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"SplitLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Joins two lines together
        ''' </summary>
        Public Sub JoinLines(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount - 1 Then Return
                If pIsReadOnly Then Return
                
                Dim lCurrentLine As String = pTextLines(vLine)
                Dim lNextLine As String = pTextLines(vLine + 1)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                    
                    ' Record replacing the current line with the joined text
                    Dim lReplaceStart As New EditorPosition(vLine, 0)
                    Dim lReplaceEnd As New EditorPosition(vLine, lCurrentLine.Length)
                    Dim lNewCursorPos As New EditorPosition(vLine, lCurrentLine.Length)
                    pUndoRedoManager.RecordReplaceText(lReplaceStart, lReplaceEnd, 
                                                      lCurrentLine, lCurrentLine & lNextLine, lNewCursorPos)
                    
                    ' Record deleting the next line
                    pUndoRedoManager.RecordDeleteLine(vLine + 1, lNextLine, lNewCursorPos)
                    
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Join the lines
                pTextLines(vLine) = lCurrentLine & lNextLine
                pLineMetadata(vLine).MarkChanged()
                
                ' Remove the next line
                RemoveLines(vLine + 1, 1)
                
                ' Update cursor position if needed
                If pCursorLine > vLine + 1 Then
                    SetCursorPosition(pCursorLine - 1, pCursorColumn)
                ElseIf pCursorLine = vLine + 1 Then
                    SetCursorPosition(vLine, lCurrentLine.Length + pCursorColumn)
                End If
                
                ScheduleFullDocumentParse()
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"JoinLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Duplicates the specified line
        ''' </summary>
        Public Sub DuplicateLine(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pIsReadOnly Then Return
                
                Dim lLineText As String = pTextLines(vLine)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    Dim lInsertPos As New EditorPosition(vLine + 1, 0)
                    Dim lNewCursorPos As New EditorPosition(vLine + 1, pCursorColumn)
                    pUndoRedoManager.RecordInsertLine(lInsertPos, lLineText, lNewCursorPos)
                End If
                
                ' Insert duplicate line
                InsertLineAt(vLine + 1, lLineText)
                
                ' Move cursor to the duplicated line
                SetCursorPosition(vLine + 1, pCursorColumn)
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"DuplicateLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Swaps two lines
        ''' </summary>
        Public Sub SwapLines(vLine1 As Integer, vLine2 As Integer)
            Try
                If vLine1 < 0 OrElse vLine1 >= pLineCount Then Return
                If vLine2 < 0 OrElse vLine2 >= pLineCount Then Return
                If vLine1 = vLine2 Then Return
                If pIsReadOnly Then Return
                
                ' Get line texts
                Dim lText1 As String = pTextLines(vLine1)
                Dim lText2 As String = pTextLines(vLine2)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                    
                    ' Record replacing first line
                    Dim lPos1Start As New EditorPosition(vLine1, 0)
                    Dim lPos1End As New EditorPosition(vLine1, lText1.Length)
                    Dim lCursorPos As New EditorPosition(pCursorLine, pCursorColumn)
                    pUndoRedoManager.RecordReplaceText(lPos1Start, lPos1End, lText1, lText2, lCursorPos)
                    
                    ' Record replacing second line
                    Dim lPos2Start As New EditorPosition(vLine2, 0)
                    Dim lPos2End As New EditorPosition(vLine2, lText2.Length)
                    pUndoRedoManager.RecordReplaceText(lPos2Start, lPos2End, lText2, lText1, lCursorPos)
                    
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Swap the lines
                pTextLines(vLine1) = lText2
                pTextLines(vLine2) = lText1
                pLineMetadata(vLine1).MarkChanged()
                pLineMetadata(vLine2).MarkChanged()
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SwapLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets text in a range
        ''' </summary>
        Public Function GetTextInRange(vStartLine As Integer, vStartColumn As Integer, 
                                      vEndLine As Integer, vEndColumn As Integer) As String
            Try
                ' Validate parameters
                If vStartLine < 0 OrElse vStartLine >= pLineCount Then Return ""
                If vEndLine < 0 OrElse vEndLine >= pLineCount Then Return ""
                
                ' Create EditorPosition objects from the parameters (not undefined variables!)
                Dim lStartPos As New EditorPosition(vStartLine, vStartColumn)
                Dim lEndPos As New EditorPosition(vEndLine, vEndColumn)
                
                ' Normalize the positions
                EditorPosition.Normalize(lStartPos, lEndPos)
                
                ' Extract the normalized values back to local variables for easier use
                Dim lStartLine As Integer = lStartPos.Line
                Dim lStartColumn As Integer = lStartPos.Column
                Dim lEndLine As Integer = lEndPos.Line
                Dim lEndColumn As Integer = lEndPos.Column
                
                If lStartLine = lEndLine Then
                    ' Single line
                    Dim lLine As String = pTextLines(lStartLine)
                    Dim lStart As Integer = Math.Max(0, Math.Min(lStartColumn, lLine.Length))
                    Dim lEnd As Integer = Math.Max(lStart, Math.Min(lEndColumn, lLine.Length))
                    Return lLine.Substring(lStart, lEnd - lStart)
                Else
                    ' Multiple lines
                    Dim lBuilder As New StringBuilder()
                    
                    ' First line
                    Dim lFirstLine As String = pTextLines(lStartLine)
                    If lStartColumn < lFirstLine.Length Then
                        lBuilder.Append(lFirstLine.Substring(lStartColumn))
                    End If
                    lBuilder.AppendLine()
                    
                    ' Middle lines
                    For i As Integer = lStartLine + 1 To lEndLine - 1
                        lBuilder.AppendLine(pTextLines(i))
                    Next
                    
                    ' Last line
                    Dim lLastLine As String = pTextLines(lEndLine)
                    lBuilder.Append(lLastLine.Substring(0, Math.Min(lEndColumn, lLastLine.Length)))
                    
                    Return lBuilder.ToString()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetTextInRange error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Replaces text in a range
        ''' </summary>
        Public Sub ReplaceTextInRange(vStartLine As Integer, vStartColumn As Integer,
                                     vEndLine As Integer, vEndColumn As Integer, vNewText As String)
            Try
                If pIsReadOnly Then Return
                
                ' Get the old text for undo
                Dim lOldText As String = GetTextInRange(vStartLine, vStartColumn, vEndLine, vEndColumn)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    Dim lStartPos As New EditorPosition(vStartLine, vStartColumn)
                    Dim lEndPos As New EditorPosition(vEndLine, vEndColumn)
                    
                    ' Calculate new cursor position
                    Dim lNewCursorPos As EditorPosition
                    If vNewText.Contains(Environment.NewLine) Then
                        Dim lLines() As String = vNewText.Split({Environment.NewLine}, StringSplitOptions.None)
                        lNewCursorPos = New EditorPosition(vStartLine + lLines.Length - 1, 
                                                          If(lLines.Length = 1, vStartColumn + vNewText.Length, lLines(lLines.Length - 1).Length))
                    Else
                        lNewCursorPos = New EditorPosition(vStartLine, vStartColumn + vNewText.Length)
                    End If
                    
                    pUndoRedoManager.RecordReplaceText(lStartPos, lEndPos, lOldText, vNewText, lNewCursorPos)
                End If
                
                ' Delete the old text
                DeleteTextDirect(vStartLine, vStartColumn, vEndLine, vEndColumn)
                
                ' Insert the new text
                SetCursorPosition(vStartLine, vStartColumn)
                InsertText(vNewText)
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceTextInRange error: {ex.Message}")
            End Try
        End Sub
        

        
        ''' <summary>
        ''' Helper to setup drag and drop
        ''' </summary>
        Private Sub SetupDragAndDrop()
            Try
                ' WARNING: Deprecated Gtk.Drag.Begin method
                ' This generates warning BC40008 but is necessary for drag-drop functionality
                ' Future GTK# versions may require migration to new API
                
                ' Setup as drag source
                Dim lSourceTargets As New TargetList()
                lSourceTargets.AddTextTargets(0)
                
                ' The following line generates BC40008 warning - this is expected
                Gtk.Drag.SourceSet(pDrawingArea, 
                                  ModifierType.Button1Mask,
                                  Nothing,
                                  DragAction.Copy Or DragAction.Move)
                
                Gtk.Drag.SourceSetTargetList(pDrawingArea, lSourceTargets)
                
                ' Setup as drop target
                Dim lDestTargets As New TargetList()
                lDestTargets.AddTextTargets(0)
                
                Gtk.Drag.DestSet(pDrawingArea,
                                DestDefaults.All,
                                Nothing,
                                DragAction.Copy Or DragAction.Move)
                
                Gtk.Drag.DestSetTargetList(pDrawingArea, lDestTargets)
                
                ' Connect drag events
                AddHandler pDrawingArea.DragBegin, AddressOf HandleDragBegin
                AddHandler pDrawingArea.DragDataGet, AddressOf HandleDragDataGet
                AddHandler pDrawingArea.DragEnd, AddressOf HandleDragEnd
                AddHandler pDrawingArea.DragFailed, AddressOf HandleDragFailed
                
                ' Connect drop events
                AddHandler pDrawingArea.DragMotion, AddressOf HandleDragMotion
                AddHandler pDrawingArea.DragLeave, AddressOf HandleDragLeave
                AddHandler pDrawingArea.DragDrop, AddressOf HandleDragDrop
                AddHandler pDrawingArea.DragDataReceived, AddressOf HandleDragDataReceived
                
                Console.WriteLine("Drag and drop setup complete")
                
            Catch ex As Exception
                Console.WriteLine($"SetupDragAndDrop error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Delete Implementation =====
        Public Sub Delete() Implements IEditor.Delete
            Try
                If pIsReadOnly Then Return
                
                If pSelectionActive AndAlso pHasSelection Then
                    ' Delete selection
                    DeleteSelection()
                Else
                    ' Delete character at cursor
                    DeleteForward()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Delete error: {ex.Message}")
            End Try
        End Sub


        

        ''' <summary>
        ''' Updates font metrics from the current font description
        ''' </summary>
        Private Sub UpdateFontMetrics()
            Try
                If pFontDescription Is Nothing Then 
                    ' Set reasonable defaults if no font description
                    pCharWidth = 8
                    pLineHeight = 18
                    Console.WriteLine("UpdateFontMetrics: No font description, using defaults")
                    
                    ' Still update dependent components even with defaults
                    UpdateLineNumberWidget()
                    UpdateScrollbars()
                    UpdateLineNumberWidth()
                    Return
                End If                
                ' Try to measure actual font metrics if drawing area is ready
                If pDrawingArea IsNot Nothing AndAlso pDrawingArea.IsRealized AndAlso pDrawingArea.PangoContext IsNot Nothing Then
                    ' Create a Pango layout to measure font metrics
                    Using lLayout As New Pango.Layout(pDrawingArea.PangoContext)
                        lLayout.FontDescription = pFontDescription
                        lLayout.SetText("M")  ' Use 'M' as standard character
                        
                        Dim lWidth, lHeight As Integer
                        lLayout.GetPixelSize(lWidth, lHeight)
                        
                        ' CRITICAL FIX: Ensure we never set line height to an unreasonably small value
                        ' Line height should be at least 10 pixels for any readable font
                        If lHeight < 10 Then
                            Console.WriteLine($"UpdateFontMetrics: Measured height too small ({lHeight}), using minimum")
                            pLineHeight = 18  ' Use reasonable default
                            pCharWidth = 8
                        Else
                            pCharWidth = lWidth
                            pLineHeight = lHeight + 2  ' Add some line spacing
                            Console.WriteLine($"UpdateFontMetrics: Measured from font - CharWidth={pCharWidth}, LineHeight={pLineHeight}")
                        End If
                    End Using
                Else
                    ' Drawing area not ready - calculate reasonable defaults from font size
                    Dim lFontSize As Integer = pFontDescription.Size
                    
                    If pFontDescription.SizeIsAbsolute Then
                        ' Size is in device units (pixels)
                        pLineHeight = Math.Max(10, lFontSize + 2)
                        pCharWidth = Math.Max(6, CInt(lFontSize * 0.6))  ' Approximate for monospace
                    Else
                        ' Size is in Pango units (1024 units per point)
                        Dim lSizeInPoints As Integer = CInt(lFontSize / Pango.Scale.PangoScale)
                        ' Convert points to pixels (assuming 96 DPI)
                        Dim lSizeInPixels As Integer = CInt(lSizeInPoints * 96 / 72)
                        pLineHeight = Math.Max(10, lSizeInPixels + 2)
                        pCharWidth = Math.Max(6, CInt(lSizeInPixels * 0.6))  ' Approximate for monospace
                    End If
                    
                    Console.WriteLine($"UpdateFontMetrics: Using calculated defaults - CharWidth={pCharWidth}, LineHeight={pLineHeight}")
                End If
                
                ' CRITICAL VALIDATION: Ensure metrics are never too small
                If pLineHeight < 10 Then
                    Console.WriteLine($"UpdateFontMetrics: WARNING - Line height {pLineHeight} too small, forcing to 18")
                    pLineHeight = 18
                End If
                If pCharWidth < 6 Then
                    Console.WriteLine($"UpdateFontMetrics: WARNING - Char width {pCharWidth} too small, forcing to 8")
                    pCharWidth = 8
                End If
                
                ' Update dependent components
                UpdateLineNumberWidget()
                UpdateScrollbars()
                UpdateLineNumberWidth()    
            
            Catch ex As Exception
                Console.WriteLine($"UpdateFontMetrics error: {ex.Message}")
                ' Fall back to safe defaults
                pCharWidth = 8
                pLineHeight = 18
            End Try
        End Sub        
        ''' <summary>
        ''' Adds a new line to the document
        ''' </summary>
        Private Sub AddNewLine(vText As String)
            Try
                pTextLines.Add(vText)
                
                ' Add metadata for the new line
                Dim lNewMetadata As New LineMetadata()
                Dim lNewColorInfo() As CharacterColorInfo = {}
                
                ' Resize arrays
                ReDim Preserve pLineMetadata(pLineCount)
                ReDim Preserve pCharacterColors(pLineCount)
                
                pLineMetadata(pLineCount) = lNewMetadata
                pCharacterColors(pLineCount) = lNewColorInfo
                
                pLineCount += 1
                
                UpdateLineNumberWidth()
                UpdateScrollbars()
                
            Catch ex As Exception
                Console.WriteLine($"AddNewLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies auto-indentation to the current line
        ''' </summary>
        Private Sub ApplyAutoIndent(vLineNumber As Integer)
            Try
                If Not pAutoIndent OrElse vLineNumber <= 0 OrElse vLineNumber >= pLineCount Then Return
                
                ' Get previous line's indentation
                Dim lPreviousLine As String = pTextLines(vLineNumber - 1)
                Dim lIndent As New System.Text.StringBuilder()
                
                For Each lChar As Char In lPreviousLine
                    If lChar = " "c OrElse lChar = vbTab Then
                        lIndent.Append(lChar)
                    Else
                        Exit For
                    End If
                Next
                
                ' Check if previous line ends with block start
                Dim lTrimmed As String = lPreviousLine.TrimEnd()
                If lTrimmed.EndsWith("Then", StringComparison.OrdinalIgnoreCase) OrElse
                   lTrimmed.EndsWith("Do", StringComparison.OrdinalIgnoreCase) OrElse
                   lTrimmed.EndsWith("While", StringComparison.OrdinalIgnoreCase) OrElse
                   lTrimmed.EndsWith("For", StringComparison.OrdinalIgnoreCase) Then
                    ' Add extra indentation
                    If pUseTabs Then
                        lIndent.Append(vbTab)
                    Else
                        lIndent.Append(New String(" "c, pTabWidth))
                    End If
                End If
                
                ' Apply indentation to current line
                If lIndent.Length > 0 Then
                    pTextLines(vLineNumber) = lIndent.ToString() & pTextLines(vLineNumber)
                    pCursorColumn = lIndent.Length
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyAutoIndent error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Inserts text at the specified position
        ''' </summary>
        Private Sub InsertTextAt(vLine As Integer, vColumn As Integer, vText As String)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(vLine)
                Dim lColumn As Integer = Math.Min(vColumn, lLine.Length)
                
                pTextLines(vLine) = lLine.Insert(lColumn, vText)
                pLineMetadata(vLine).MarkChanged()
                
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextAt error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Normalizes selection coordinates using individual integers (wrapper for backward compatibility)
        ''' </summary>
        ''' <param name="vStartLine">Start line (will be normalized)</param>
        ''' <param name="vStartColumn">Start column (will be normalized)</param>
        ''' <param name="vEndLine">End line (will be normalized)</param>
        ''' <param name="vEndColumn">End column (will be normalized)</param>
        Friend Sub NormalizeSelection(ByRef vStartLine As Integer, ByRef vStartColumn As Integer,
                                     ByRef vEndLine As Integer, ByRef vEndColumn As Integer)
            Try
                ' Create EditorPosition structures
                Dim lStart As New EditorPosition(vStartLine, vStartColumn)
                Dim lEnd As New EditorPosition(vEndLine, vEndColumn)
                
                ' Use the EditorPosition.Normalize method
                EditorPosition.Normalize(lStart, lEnd)
                
                ' Update the integer parameters
                vStartLine = lStart.Line
                vStartColumn = lStart.Column
                vEndLine = lEnd.Line
                vEndColumn = lEnd.Column
                
            Catch ex As Exception
                Console.WriteLine($"NormalizeSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Normalizes selection coordinates using EditorPosition structures (wrapper)
        ''' </summary>
        ''' <param name="vStartPos">Start position (will be normalized)</param>
        ''' <param name="vEndPos">End position (will be normalized)</param>
        Friend Sub NormalizeSelection(ByRef vStartPos As EditorPosition, ByRef vEndPos As EditorPosition)
            Try
                ' Delegate to the EditorPosition static method
                EditorPosition.Normalize(vStartPos, vEndPos)
                
            Catch ex As Exception
                Console.WriteLine($"NormalizeSelection (EditorPosition) error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
