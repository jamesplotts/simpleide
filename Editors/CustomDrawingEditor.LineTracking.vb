' Editors/CustomDrawingEditor.LineTracking.vb - Track line changes for formatting and capitalization
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Line Tracking Fields =====
        Private pPreviousLine As Integer = -1
        Private pLineExitPending As Boolean = False
        
        ' ===== LineExited Event =====
        Public Event LineExited As EventHandler(Of LineExitedEventArgs) Implements IEditor.LineExited
        
        ' ===== Line Change Tracking =====

        ''' <summary>
        ''' Marks a line as being edited to prevent formatting while typing
        ''' </summary>
        ''' <param name="vLine">The line number being edited</param>
        ''' <remarks>
        ''' This method consolidates the duplicate implementations and properly
        ''' delegates to SourceFileInfo for all state management
        ''' </remarks>
        Public Sub SetEditingLine(vLine As Integer)
            Try
                ' Validate line number
                If vLine < 0 OrElse vLine >= pLineCount Then
                    Console.WriteLine($"SetEditingLine: Invalid line {vLine} (LineCount={pLineCount})")
                    Return
                End If
                
                ' If switching from another line, mark it as changed
                
                ' Update editing line tracking
                pEditingLine = vLine
                pLastEditedLine = vLine
                
                Console.WriteLine($"SetEditingLine: Now editing line {vLine}")
                
            Catch ex As Exception
                Console.WriteLine($"SetEditingLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Called when cursor moves to a different line
        ''' </summary>
        ''' <param name="vOldLine">The line number we're leaving</param>
        ''' <param name="vNewLine">The line number we're entering</param>
        ''' <remarks>
        ''' Properly coordinates with SourceFileInfo for marking changes and
        ''' requesting parsing while maintaining event firing for UI updates
        ''' </remarks>
        Private Sub OnLineChanged(vOldLine As Integer, vNewLine As Integer)
            Try
                ' Check if we actually changed lines
                If vOldLine = vNewLine Then Return
                
                ' Mark the line we're leaving as changed and fire event
                If vOldLine >= 0 AndAlso vOldLine < pLineCount Then
                    ' Mark line as changed for async parsing through SourceFileInfo
                    
                    ' Fire LineExited event for capitalization manager (KEEP THIS)
                    RaiseLineExitedEvent(vOldLine)
                    Console.WriteLine($"OnLineChanged: Raised LineExited for line {vOldLine}")
                End If
                
                ' Update editing line (use consolidated version)
                SetEditingLine(vNewLine)
                
                ' Update previous line tracker
                pPreviousLine = vNewLine
                
                
                Console.WriteLine($"OnLineChanged: Moved from line {vOldLine} to line {vNewLine}")
                
            Catch ex As Exception
                Console.WriteLine($"OnLineChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Marks a range of lines as changed and requests parsing
        ''' </summary>
        ''' <param name="vStartLine">First line in the range to mark</param>
        ''' <param name="vEndLine">Last line in the range (optional, defaults to start line)</param>
        ''' <remarks>
        ''' Delegates entirely to SourceFileInfo for state management and parse requests
        ''' </remarks>
        Private Sub MarkLinesChangedAndParse(vStartLine As Integer, Optional vEndLine As Integer = -1)
            Try
                If pSourceFileInfo Is Nothing Then 
                    Console.WriteLine("MarkLinesChangedAndParse: No SourceFileInfo available")
                    Return
                End If
                
                ' Determine actual end line
                Dim lEndLine As Integer = If(vEndLine >= 0, vEndLine, vStartLine)
                
                ' Validate range
                If vStartLine < 0 OrElse vStartLine >= pLineCount Then
                    Console.WriteLine($"MarkLinesChangedAndParse: Invalid start line {vStartLine}")
                    Return
                End If
                
                ' Clamp end line to valid range
                lEndLine = Math.Min(lEndLine, pLineCount - 1)
                
                
                
                ' Request async parse through SourceFileInfo
                pSourceFileInfo.RequestAsyncParse()
                Console.WriteLine("MarkLinesChangedAndParse: Requested async parse")
                
            Catch ex As Exception
                Console.WriteLine($"MarkLinesChangedAndParse error: {ex.Message}")
            End Try
        End Sub
    
        ''' <summary>
        ''' Override SetCursorPosition to track line changes
        ''' </summary>
        ''' <param name="vLine">Target line number</param>
        ''' <param name="vColumn">Target column number</param>
        ''' <remarks>
        ''' Wrapper that ensures OnLineChanged is called when moving between lines
        ''' </remarks>
        Private Sub SetCursorPosition_WithTracking(vLine As Integer, vColumn As Integer)
            Try
                ' Store old line before moving cursor
                Dim lOldLine As Integer = pCursorLine
                
                ' Call the base SetCursorPosition (which validates and clamps)
                SetCursorPosition(vLine, vColumn)
                
                ' Check if line changed and trigger line change handling
                If lOldLine <> pCursorLine Then
                    OnLineChanged(lOldLine, pCursorLine)
                    Console.WriteLine($"SetCursorPosition_WithTracking: Line changed from {lOldLine} to {pCursorLine}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetCursorPosition_WithTracking error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles the Enter key press to insert new line and manage line tracking
        ''' </summary>
        ''' <remarks>
        ''' Properly delegates all text manipulation to SourceFileInfo and ensures
        ''' proper event firing for line exits and parsing requests
        ''' </remarks>
        Private Sub HandleEnterKey()
            Try
                ' Check if read-only
                If pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Mark current line as changed before creating new line
                If pEditingLine >= 0 AndAlso pEditingLine < pLineCount Then
                    ' Mark line as changed for async parsing
                    
                    ' Fire LineExited event before leaving the line (for capitalization)
                    RaiseLineExitedEvent(pEditingLine)
                    Console.WriteLine($"HandleEnterKey: Raised LineExited for line {pEditingLine}")
                End If
                
                ' Get current line content from SourceFileInfo
                Dim lCurrentLine As String = pSourceFileInfo.TextLines(pCursorLine)
                
                ' Split at cursor position
                Dim lBeforeCursor As String = lCurrentLine.Substring(0, Math.Min(pCursorColumn, lCurrentLine.Length))
                Dim lAfterCursor As String = If(pCursorColumn < lCurrentLine.Length, 
                                                lCurrentLine.Substring(pCursorColumn), 
                                                "")
                
                
                ' Insert new line through SourceFileInfo
                pSourceFileInfo.InsertLine(pCursorLine + 1, lAfterCursor)
                
                ' Move cursor to start of new line
                SetCursorPosition(pCursorLine + 1, 0)
                
                ' Update editing line to the new line
                SetEditingLine(pCursorLine)
                
                ' Request async parse after inserting new line
                pSourceFileInfo.RequestAsyncParse()
                
                ' Mark document as modified
                IsModified = True
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea?.QueueDraw()
                
                ' Raise text changed event
                RaiseEvent TextChanged(Me, New EventArgs())
                
                Console.WriteLine($"HandleEnterKey: Inserted new line at {pCursorLine}")
                
            Catch ex As Exception
                Console.WriteLine($"HandleEnterKey error: {ex.Message}")
            End Try
        End Sub

        ' ===== LineExited Event Methods =====
        
        ''' <summary>
        ''' Raise the LineExited event for a specific line
        ''' </summary>
        Private Sub RaiseLineExitedEvent(vLineNumber As Integer)
            Try
                ' Validate line number
                If vLineNumber < 0 OrElse vLineNumber >= pLineCount Then Return
                
                ' Get the text of the line that was exited
                Dim lLineText As String = GetLineText(vLineNumber)
                
                ' Create event args
                Dim lArgs As New LineExitedEventArgs(vLineNumber, lLineText)
                
                ' Raise the event
                RaiseEvent LineExited(Me, lArgs)
                
            Catch ex As Exception
                Console.WriteLine($"RaiseLineExitedEvent error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Force a line exit event for the current line (useful when losing focus)
        ''' </summary>
        Public Sub ForceLineExit()
            Try
                If pPreviousLine >= 0 AndAlso pPreviousLine < pLineCount Then
                    RaiseLineExitedEvent(pPreviousLine)
                ElseIf pCursorLine >= 0 AndAlso pCursorLine < pLineCount Then
                    RaiseLineExitedEvent(pCursorLine)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ForceLineExit error: {ex.Message}")
            End Try
        End Sub
    End Class
    
End Namespace
