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
        ''' <param name="vLineNumber">Zero-based line index where to insert</param>
        ''' <param name="vText">Text content for the new line</param>
        ''' <remarks>
        ''' Uses SourceFileInfo exclusively for line operations and triggers async parsing
        ''' </remarks>
        Friend Sub InsertLineAt(vLineNumber As Integer, vText As String)
            Try
                If vLineNumber < 0 OrElse vLineNumber > pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Use SourceFileInfo to insert the line
                pSourceFileInfo.InsertLine(vLineNumber, vText)
                
                ' Update line number width if needed
                UpdateLineNumberWidth()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"InsertLineAt error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Removes lines starting at the specified position
        ''' </summary>
        ''' <param name="vStartLine">Starting line index (0-based)</param>
        ''' <param name="vCount">Number of lines to remove</param>
        ''' <remarks>
        ''' Uses SourceFileInfo exclusively for line deletion and triggers async parsing
        ''' </remarks>
        Friend Sub RemoveLines(vStartLine As Integer, vCount As Integer)
            Try
                If vStartLine < 0 OrElse vStartLine >= pLineCount OrElse vCount <= 0 Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Calculate actual count to remove
                Dim lActualCount As Integer = Math.Min(vCount, pLineCount - vStartLine)
                
                ' Delete lines through SourceFileInfo
                for i As Integer = 0 To lActualCount - 1
                    pSourceFileInfo.DeleteLine(vStartLine)
                Next
                
                ' Update line number width if needed
                UpdateLineNumberWidth()
                
                ' Queue redraw
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"RemoveLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Deletes a single line and records it for undo
        ''' </summary>
        ''' <param name="vLine">Line index to delete (0-based)</param>
        ''' <remarks>
        ''' Uses SourceFileInfo for line deletion and manages undo recording
        ''' </remarks>
        Public Sub DeleteLine(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Get the line text for undo
                Dim lLineText As String = pSourceFileInfo.TextLines(vLine)
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    Dim lCursorPos As New EditorPosition(Math.Max(0, vLine - 1), 0)
                    pUndoRedoManager.RecordDeleteLine(vLine, lLineText, lCursorPos)
                End If
                
                ' Delete through SourceFileInfo
                pSourceFileInfo.DeleteLine(vLine)
                
                
                ' Ensure we have at least one line
                If pLineCount = 0 Then
                    pSourceFileInfo.InsertLine(0, "")
                End If
                
                ' Update cursor position if needed
                If pCursorLine >= pLineCount Then
                    SetCursorPosition(pLineCount - 1, 0)
                ElseIf pCursorLine > vLine Then
                    SetCursorPosition(pCursorLine - 1, pCursorColumn)
                End If
                
                ' Request async parsing
                pSourceFileInfo.RequestAsyncParse()
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Update UI
                UpdateLineNumberWidth()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Joins two lines together
        ''' </summary>
        ''' <param name="vLine">First line index to join with next (0-based)</param>
        ''' <remarks>
        ''' Uses SourceFileInfo exclusively for line operations
        ''' </remarks>
        Public Sub JoinLines(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount - 1 Then Return
                If pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                Dim lCurrentLine As String = pSourceFileInfo.TextLines(vLine)
                Dim lNextLine As String = pSourceFileInfo.TextLines(vLine + 1)
                
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
                
                ' Use SourceFileInfo to join lines
                pSourceFileInfo.JoinLines(vLine)
                
                
                ' Request async parsing
                pSourceFileInfo.RequestAsyncParse()
                
                ' Update cursor position if needed
                If pCursorLine > vLine + 1 Then
                    SetCursorPosition(pCursorLine - 1, pCursorColumn)
                ElseIf pCursorLine = vLine + 1 Then
                    SetCursorPosition(vLine, lCurrentLine.Length + pCursorColumn)
                End If
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Update UI
                UpdateLineNumberWidth()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"JoinLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Splits a line at the specified position
        ''' </summary>
        ''' <param name="vLine">Line index to split (0-based)</param>
        ''' <param name="vColumn">Column position where to split</param>
        ''' <remarks>
        ''' Uses SourceFileInfo for line operations
        ''' </remarks>
        Public Sub SplitLine(vLine As Integer, vColumn As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                If pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Use SourceFileInfo to split the line
                pSourceFileInfo.SplitLine(vLine, vColumn)
                
                
                ' Record for undo if needed
                If pUndoRedoManager IsNot Nothing Then
                    ' Record the split operation for undo
                    Dim lNewCursorPos As New EditorPosition(vLine + 1, 0)
                    ' Additional undo recording logic here if needed
                End If
                
                ' Request async parsing
                pSourceFileInfo.RequestAsyncParse()
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs)
                
                ' Update UI
                UpdateLineNumberWidth()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SplitLine error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Gets text in a range
        ''' </summary>
        ''' <param name="vStartLine">Start line (0-based)</param>
        ''' <param name="vStartColumn">Start column (0-based)</param>
        ''' <param name="vEndLine">End line (0-based)</param>
        ''' <param name="vEndColumn">End column (0-based)</param>
        ''' <returns>The text in the specified range</returns>
        ''' <remarks>
        ''' Uses SourceFileInfo.TextLines for text retrieval
        ''' </remarks>
        Public Function GetTextInRange(vStartLine As Integer, vStartColumn As Integer, 
                                      vEndLine As Integer, vEndColumn As Integer) As String
            Try
                ' Use SourceFileInfo's GetTextInRange method if available
                Return pSourceFileInfo.GetTextInRange(vStartLine, vStartColumn, vEndLine, vEndColumn)
            Catch ex As Exception
                Console.WriteLine($"GetTextInRange error: {ex.Message}")
                Return ""
            End Try
        End Function

        ' Helper method for GetTextInRange
        Private Function GetTextInRangeFromLines(vStartLine As Integer, vStartColumn As Integer,
                                                vEndLine As Integer, vEndColumn As Integer) As String
            Try
                Dim lTextLines As List(Of String) = pSourceFileInfo.TextLines
                
                ' Validate parameters
                If vStartLine < 0 OrElse vStartLine >= lTextLines.Count Then Return ""
                If vEndLine < 0 OrElse vEndLine >= lTextLines.Count Then Return ""
                
                ' Create EditorPosition objects for normalization
                Dim lStartPos As New EditorPosition(vStartLine, vStartColumn)
                Dim lEndPos As New EditorPosition(vEndLine, vEndColumn)
                
                ' Normalize the positions
                EditorPosition.Normalize(lStartPos, lEndPos)
                
                If lStartPos.Line = lEndPos.Line Then
                    ' Single line
                    Dim lLine As String = lTextLines(lStartPos.Line)
                    Dim lStart As Integer = Math.Max(0, Math.Min(lStartPos.Column, lLine.Length))
                    Dim lEnd As Integer = Math.Max(lStart, Math.Min(lEndPos.Column, lLine.Length))
                    Return lLine.Substring(lStart, lEnd - lStart)
                Else
                    ' Multiple lines
                    Dim lBuilder As New StringBuilder()
                    
                    ' First line
                    Dim lFirstLine As String = lTextLines(lStartPos.Line)
                    If lStartPos.Column < lFirstLine.Length Then
                        lBuilder.Append(lFirstLine.Substring(lStartPos.Column))
                    End If
                    lBuilder.AppendLine()
                    
                    ' Middle lines
                    for i As Integer = lStartPos.Line + 1 To lEndPos.Line - 1
                        lBuilder.AppendLine(lTextLines(i))
                    Next
                    
                    ' Last line
                    Dim lLastLine As String = lTextLines(lEndPos.Line)
                    lBuilder.Append(lLastLine.Substring(0, Math.Min(lEndPos.Column, lLastLine.Length)))
                    
                    Return lBuilder.ToString()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetTextInRangeFromLines error: {ex.Message}")
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
        ''' <param name="vText">The text for the new line</param>
        ''' <remarks>
        ''' Updated to use SourceFileInfo for line addition with proper encapsulation
        ''' </remarks>
        Private Sub AddNewLine(vText As String)
            Try
                If pSourceFileInfo IsNot Nothing Then
                    ' Add the line through SourceFileInfo - it will handle its own internal arrays
                    pSourceFileInfo.TextLines.Add(vText)
                    
                    
                    ' Request async parsing
                    pSourceFileInfo.RequestAsyncParse()
                    
                    ' Update editor UI
                    UpdateLineNumberWidth()
                    UpdateScrollbars()
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddNewLine error: {ex.Message}")
            End Try
        End Sub       
 
        ''' <summary>
        ''' Applies auto-indentation to the specified line
        ''' </summary>
        ''' <param name="vLineNumber">The line number to apply indentation to</param>
        ''' <remarks>
        ''' Updated to use SourceFileInfo for text manipulation
        ''' </remarks>
        Private Sub ApplyAutoIndent(vLineNumber As Integer)
            Try
                If Not pAutoIndent OrElse vLineNumber <= 0 OrElse vLineNumber >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Get previous line's indentation
                Dim lPreviousLine As String = pSourceFileInfo.TextLines(vLineNumber - 1)
                Dim lIndent As New System.Text.StringBuilder()
                
                ' Extract leading whitespace from previous line
                for each lChar As Char in lPreviousLine
                    If lChar = " "c OrElse lChar = vbTab Then
                        lIndent.Append(lChar)
                    Else
                        Exit for
                    End If
                Next
                
                ' Check if previous line ends with a block start keyword
                Dim lTrimmedPrevious As String = lPreviousLine.TrimEnd()
                Dim lBlockStarters() As String = {"Sub ", "Function ", "If ", "For ", "While ", "Do", "Select Case", 
                                                  "Class ", "Module ", "Namespace ", "Structure ", "Enum ", 
                                                  "Property ", "Get", "Set", "Try", "With "}
                
                ' Check if we should increase indent
                for each lStarter As String in lBlockStarters
                    If lTrimmedPrevious.StartsWith(lStarter, StringComparison.OrdinalIgnoreCase) OrElse
                       lTrimmedPrevious.Contains(" " & lStarter, StringComparison.OrdinalIgnoreCase) Then
                        ' Add one level of indentation (4 spaces or 1 tab)
                        ' TODO: Need to implement in SettingsManager a boolean setting for "UseSpacesForTabs"
                        'If pUseSpacesForTabs Then   
                            lIndent.Append(New String(" "c, pTabWidth))
                        Exit for
                    End If
                Next
                
                ' Apply indentation to current line if it's empty or starts with non-whitespace
                Dim lCurrentLine As String = pSourceFileInfo.TextLines(vLineNumber)
                If String.IsNullOrEmpty(lCurrentLine.Trim()) Then
                    ' Line is empty or only whitespace - replace with indentation
                    pSourceFileInfo.UpdateTextLine(vLineNumber, lIndent.ToString())
                    
                    ' Move cursor to end of indentation
                    SetCursorPosition(vLineNumber, lIndent.Length)
                ElseIf Not Char.IsWhiteSpace(lCurrentLine(0)) Then
                    ' Line starts with non-whitespace - prepend indentation
                    pSourceFileInfo.UpdateTextLine(vLineNumber, lIndent.ToString() & lCurrentLine)
                    
                    ' Move cursor to position after indentation
                    SetCursorPosition(vLineNumber, lIndent.Length)
                End If
                
                ' Request async parsing for the changed line
                pSourceFileInfo.RequestAsyncParse()
                
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
                
                Dim lLine As String = TextLines(vLine)
                Dim lColumn As Integer = Math.Min(vColumn, lLine.Length)
                
                TextLines(vLine) = lLine.Insert(lColumn, vText)
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

        ''' <summary>
        ''' Gets the text of a specific line
        ''' </summary>
        ''' <param name="vLineIndex">The line index to get (0-based)</param>
        ''' <returns>The text of the line, or empty string if invalid index</returns>
        ''' <remarks>
        ''' Updated to get text from SourceFileInfo
        ''' </remarks>
        Public Function GetLineText(vLineIndex As Integer) As String Implements IEditor.GetLineText
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pLineCount Then
                    Return ""
                End If
                
                If pSourceFileInfo IsNot Nothing AndAlso vLineIndex < pSourceFileInfo.TextLines.Count Then
                    Return pSourceFileInfo.TextLines(vLineIndex)
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetLineText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
    End Class
    
End Namespace
