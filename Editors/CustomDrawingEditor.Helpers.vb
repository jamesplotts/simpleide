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
        ''' Deletes an entire line using atomic operations
        ''' </summary>
        ''' <param name="vLine">Line number to delete (0-based)</param>
        ''' <remarks>
        ''' Uses atomic DeleteText to remove entire line including newline
        ''' </remarks>
        Public Sub DeleteLine(vLine As Integer)
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                If vLine < 0 OrElse vLine >= pLineCount Then Return
                
                Dim lLineText As String = pSourceFileInfo.TextLines(vLine)
                Dim lStartPos As New EditorPosition(vLine, 0)
                Dim lEndPos As EditorPosition
                
                ' Determine end position based on whether it's the last line
                If vLine < pLineCount - 1 Then
                    ' Not last line - delete including newline
                    lEndPos = New EditorPosition(vLine + 1, 0)
                    
                    ' Record for undo
                    If pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, 
                                                         lLineText & Environment.NewLine, lStartPos)
                    End If
                Else
                    ' Last line - just delete the line content
                    lEndPos = New EditorPosition(vLine, lLineText.Length)
                    
                    ' If not the only line, also delete the preceding newline
                    If vLine > 0 Then
                        lStartPos = New EditorPosition(vLine - 1, pSourceFileInfo.TextLines(vLine - 1).Length)
                        
                        ' Record for undo
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, 
                                                             Environment.NewLine & lLineText, lStartPos)
                        End If
                    Else
                        ' Only line - just clear it
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.RecordDeleteText(lStartPos, lEndPos, lLineText, lStartPos)
                        End If
                    End If
                End If
                
                ' Use atomic DeleteText
                pSourceFileInfo.DeleteText(lStartPos.Line, lStartPos.Column, lEndPos.Line, lEndPos.Column)
                
                ' Adjust cursor if needed
                If pCursorLine > vLine Then
                    SetCursorPosition(pCursorLine - 1, pCursorColumn)
                ElseIf pCursorLine = vLine Then
                    SetCursorPosition(Math.Min(vLine, pLineCount - 1), 0)
                End If
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteLine error: {ex.Message}")
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
                ' Use the local helper method to get text from lines
                Return GetTextInRangeFromLines(vStartLine, vStartColumn, vEndLine, vEndColumn)
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
                
                ' CRITICAL FIX: Force recalculation of font metrics when zoom changes
                ' by creating a fresh FontMetrics object with current font
                If pDrawingArea IsNot Nothing AndAlso pDrawingArea.IsRealized Then
                    ' Create a temporary Cairo surface to get metrics
                    Using lSurface As Cairo.ImageSurface = New Cairo.ImageSurface(Cairo.Format.Argb32, 1, 1)
                        Using lContext As Cairo.Context = New Cairo.Context(lSurface)
                            ' Create new FontMetrics with current font (includes zoom)
                            pFontMetrics = New Utilities.FontMetrics(pFontDescription, lContext)
                            
                            ' Update character dimensions from new metrics
                            If pFontMetrics IsNot Nothing Then
                                pCharWidth = pFontMetrics.CharWidth
                                pLineHeight = pFontMetrics.CharHeight
                                Console.WriteLine($"UpdateFontMetrics: Updated from FontMetrics - CharWidth={pCharWidth}, LineHeight={pLineHeight}")
                            End If
                        End Using
                    End Using
                End If
                
                ' Fallback: Try to measure using Pango if FontMetrics failed
                If (pFontMetrics Is Nothing OrElse pCharWidth = 0 OrElse pLineHeight = 0) AndAlso
                   pDrawingArea IsNot Nothing AndAlso pDrawingArea.IsRealized AndAlso 
                   pDrawingArea.PangoContext IsNot Nothing Then
                   
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
                            Console.WriteLine($"UpdateFontMetrics: Measured from Pango - CharWidth={pCharWidth}, LineHeight={pLineHeight}")
                        End If
                    End Using
                End If
                
                ' Final fallback: Calculate from font description
                If pCharWidth = 0 OrElse pLineHeight = 0 Then
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
                UpdateLineNumberWidget()  ' This now updates theme as well
                UpdateScrollbars()
                UpdateLineNumberWidth()
                
                ' CRITICAL: Force immediate redraw after metrics update
                pDrawingArea?.QueueDraw()
                pLineNumberWidget?.QueueDraw()
                
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
        ''' Applies auto-indentation to the specified line based on the previous line
        ''' </summary>
        ''' <param name="vLine">Line to auto-indent (0-based)</param>
        ''' <remarks>
        ''' Uses atomic InsertText to add indentation
        ''' </remarks>
        Private Sub ApplyAutoIndent(vLine As Integer)
            Try
                If vLine <= 0 OrElse vLine >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                If pSettingsManager Is Nothing OrElse Not pSettingsManager.AutoIndent Then Return
                
                ' Get indentation from previous line
                Dim lPrevLine As String = pSourceFileInfo.TextLines(vLine - 1)
                Dim lIndent As String = GetLineIndentation(vLine - 1)
                
                If Not String.IsNullOrEmpty(lIndent) Then
                    ' Use atomic InsertText to add indentation
                    pSourceFileInfo.InsertText(vLine, 0, lIndent)
                    
                    ' Move cursor to after indentation
                    SetCursorPosition(vLine, lIndent.Length)
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
