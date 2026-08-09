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

        ''' <summary>
        ''' Rewrites the word immediately preceding vColumn on vLine to its canonical VB.NET
        ''' keyword casing (e.g. "dim" becomes "Dim"), if it's a reserved keyword that was just
        ''' typed with different casing
        ''' </summary>
        ''' <param name="vLine">Zero-based line the word is on</param>
        ''' <param name="vColumn">Column immediately after the word (where the word-ending
        ''' delimiter - space, punctuation, Enter - was just typed)</param>
        ''' <remarks>
        ''' Classic VB.NET IDE behavior: a keyword auto-corrects the instant you finish typing
        ''' it, independent of CodeSense. Uses SourceFileInfo.GetKeywordCanonicalCase so the
        ''' keyword table (SourceFileInfo.CaseCorrection.vb) stays the single source of truth,
        ''' shared with the file-load-time correction pass.
        ''' </remarks>
        ''' <remarks>
        ''' Requires vColumn to be a genuine trailing word boundary (nothing identifier-like
        ''' continues immediately after it) - this is what makes it safe to call from
        ''' SetCursorPosition on every cursor move, including the single-character advances
        ''' InsertCharacter makes while typing forward through a word: at that moment the
        ''' letter just typed still sits right at vColumn, so the check fails and no premature
        ''' correction happens on a not-yet-finished word (e.g. "for" mid-way through "format")
        ''' </remarks>
        Private Sub CorrectKeywordEndingAt(vLine As Integer, vColumn As Integer)
            Try
                If pSourceFileInfo Is Nothing OrElse vLine < 0 OrElse vLine >= pLineCount Then Return
                If IsInsideStringOrComment(vLine, vColumn) Then Return

                Dim lLine As String = pSourceFileInfo.TextLines(vLine)
                If vColumn > lLine.Length Then Return
                If vColumn < lLine.Length AndAlso (Char.IsLetterOrDigit(lLine(vColumn)) OrElse lLine(vColumn) = "_"c) Then Return

                Dim lWordStart As Integer = vColumn
                While lWordStart > 0 AndAlso (Char.IsLetterOrDigit(lLine(lWordStart - 1)) OrElse lLine(lWordStart - 1) = "_"c)
                    lWordStart -= 1
                End While
                If lWordStart >= vColumn Then Return

                Dim lWord As String = lLine.Substring(lWordStart, vColumn - lWordStart)
                Dim lCanonical As String = pSourceFileInfo.GetKeywordCanonicalCase(lWord)
                If lCanonical Is Nothing OrElse lCanonical.Equals(lWord, StringComparison.Ordinal) Then Return

                pSourceFileInfo.TextLines(vLine) = lLine.Substring(0, lWordStart) & lCanonical & lLine.Substring(vColumn)
                pLineMetadata(vLine).MarkChanged()
                InvalidateLine(vLine)
                IsModified = True

            Catch ex As Exception
                Console.WriteLine($"CorrectKeywordEndingAt error: {ex.Message}")
            End Try
        End Sub

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
                    #If DEBUG Then
                    Console.WriteLine($"SetEditingLine: Invalid line {vLine} (LineCount={pLineCount})")
                    #End If
                    Return
                End If
                
                ' If switching from another line, mark it as changed
                
                ' Update editing line tracking
                pEditingLine = vLine
                pLastEditedLine = vLine
                
                #If DEBUG Then
                Console.WriteLine($"SetEditingLine: Now editing line {vLine}")
                #End If
                
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
                    pSourceFileInfo.ParseLine(vOldLine)
                    ' Fire LineExited event for capitalization manager (KEEP THIS)
                    RaiseLineExitedEvent(vOldLine)
                    #If DEBUG Then
                    Console.WriteLine($"OnLineChanged: Raised LineExited for line {vOldLine}")
                    #End If
                End If
                
                ' Update editing line (use consolidated version)
                SetEditingLine(vNewLine)
                
                ' Update previous line tracker
                pPreviousLine = vNewLine

                ' Recompute the matching block-keyword highlight for the new line
                UpdateKeywordPairHighlight()

                #If DEBUG Then
                Console.WriteLine($"OnLineChanged: Moved from line {vOldLine} to line {vNewLine}")
                #End If
                
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
                    #If DEBUG Then
                    Console.WriteLine("MarkLinesChangedAndParse: No SourceFileInfo available")
                    #End If
                    Return
                End If
                
                ' Determine actual end line
                Dim lEndLine As Integer = If(vEndLine >= 0, vEndLine, vStartLine)
                
                ' Validate range
                If vStartLine < 0 OrElse vStartLine >= pLineCount Then
                    #If DEBUG Then
                    Console.WriteLine($"MarkLinesChangedAndParse: Invalid start line {vStartLine}")
                    #End If
                    Return
                End If
                
                ' Clamp end line to valid range
                lEndLine = Math.Min(lEndLine, pLineCount - 1)
                
                
                
                ' Request async parse through SourceFileInfo
                pSourceFileInfo.RequestAsyncParse()
                #If DEBUG Then
                Console.WriteLine("MarkLinesChangedAndParse: Requested async parse")
                #End If
                
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
                    #If DEBUG Then
                    Console.WriteLine($"SetCursorPosition_WithTracking: Line changed from {lOldLine} to {pCursorLine}")
                    #End If
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

                    ' Enter is a word boundary too - correct a keyword finished right before it
                    CorrectKeywordEndingAt(pCursorLine, pCursorColumn)

                    ' Fire LineExited event before leaving the line (for capitalization)
                    RaiseLineExitedEvent(pEditingLine)
                    #If DEBUG Then
                    Console.WriteLine($"HandleEnterKey: Raised LineExited for line {pEditingLine}")
                    #End If
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
                
                #If DEBUG Then
                Console.WriteLine($"HandleEnterKey: Inserted new line at {pCursorLine}")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"HandleEnterKey error: {ex.Message}")
            End Try
        End Sub

        ' ===== LineExited Event Methods =====
        
        ''' <summary>
        ''' Raise the LineExited event for a specific line
        ''' </summary>
        ''' <remarks>
        ''' Also runs declaration-case tracking for the exited line (see
        ''' ProcessLineFormattingWithDeclarationTracking in
        ''' CustomDrawingEditor.IdentifierCaseSync.vb) - if a declaration's name was retyped
        ''' with different casing on this line, that new casing gets propagated to every other
        ''' reference project-wide, matching classic VS VB.NET identifier-case behavior. This
        ''' used to be wired through a dedicated "capitalization manager" subscriber to
        ''' LineExited that no longer exists, so it's called directly here instead.
        ''' </remarks>
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

                ' Detect and propagate any declaration-case change on this line
                ProcessLineFormattingWithDeclarationTracking(vLineNumber)

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
