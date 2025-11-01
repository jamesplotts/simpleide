' Editors/CustomDrawingEditor.Clipboard.vb - Clipboard operations
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Syntax

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor 
        
        ' ===== Clipboard Operations =====
        
        ''' <summary>
        ''' Cuts the selected text to the clipboard
        ''' </summary>
        Public Sub Cut() Implements IEditor.Cut
            Try
                If Not pHasSelection OrElse pIsReadOnly Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Get selection bounds
                Dim lStart As EditorPosition = GetSelectionStart()
                Dim lEnd As EditorPosition = GetSelectionEnd()
                
                ' Get selected text for clipboard
                Dim lSelectedText As String = GetSelectedText()
                
                ' Copy to clipboard
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Atom.Intern("CLIPBOARD", False))
                lClipboard.Text = lSelectedText
                
                ' Delete the selection using atomic operation
                pSourceFileInfo.DeleteText(lStart.Line, lStart.Column, lEnd.Line, lEnd.Column)
                
                ' Set cursor to deletion point
                SetCursorPosition(lStart.Line, lStart.Column)
                ClearSelection()
                
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
            Catch ex As Exception
                Console.WriteLine($"Cut error: {ex.Message}")
            End Try
        End Sub

        Private Function GetSelectionStart() As EditorPosition
            Return New EditorPosition(pSelectionStartLine, pSelectionStartColumn)
        End Function

        Private Function GetSelectionEnd() As EditorPosition
            If pSelectionActive Then Return New EditorPosition(pSelectionEndLine, pSelectionEndColumn)
            Return GetSelectionStart()
        End Function

        
        ''' <summary>
        ''' Copies the selected text to the clipboard
        ''' </summary>
        Public Sub Copy() Implements IEditor.Copy
            Try
                If Not pHasSelection Then Return
                
                Dim lSelectedText As String = GetSelectedText()
                If Not String.IsNullOrEmpty(lSelectedText) Then
                    ' Copy to both clipboards
                    Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                    lClipboard.Text = lSelectedText
                    
                    ' Also copy to primary selection (X11 style)
                    Dim lPrimaryClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Primary)
                    lPrimaryClipboard.Text = lSelectedText
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Copy error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles clipboard text when received - automatically selects pasted text
        ''' </summary>
        ''' <param name="vClipboard">The clipboard source</param>
        ''' <param name="vText">The text to paste</param>
        ''' <remarks>
        ''' FIXED: Ensures proper metadata update and redraw for multi-line pastes
        ''' </remarks>
        Private Sub OnClipboardTextReceived(vClipboard As Clipboard, vText As String)
            Try
                If String.IsNullOrEmpty(vText) OrElse pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return
                
                Console.WriteLine($"OnClipboardTextReceived: Pasting {vText.Length} characters")
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Determine where the paste will start
                Dim lPasteStartLine As Integer
                Dim lPasteStartColumn As Integer
                
                If pHasSelection Then
                    ' Get normalized selection bounds - this is where paste will happen after deletion
                    Dim lStartPos As New EditorPosition(pSelectionStartLine, pSelectionStartColumn)
                    Dim lEndPos As New EditorPosition(pSelectionEndLine, pSelectionEndColumn)
                    
                    If lEndPos.IsLessThan( lStartPos ) Then
                        ' Swap if selection is backwards
                        Dim lTemp As EditorPosition = lStartPos
                        lStartPos = lEndPos
                        lEndPos = lTemp
                    End If
                    
                    lPasteStartLine = lStartPos.Line
                    lPasteStartColumn = lStartPos.Column
                    
                    ' Delete selection first
                    DeleteSelection()
                Else
                    ' Paste at cursor position
                    lPasteStartLine = pCursorLine
                    lPasteStartColumn = pCursorColumn
                End If
                
                ' Calculate where paste will end
                Dim lLines() As String = vText.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                Dim lPasteEndLine As Integer = lPasteStartLine
                Dim lPasteEndColumn As Integer = lPasteStartColumn
                
                If lLines.Length = 1 Then
                    ' Single line paste - end column is start + text length
                    lPasteEndColumn = lPasteStartColumn + vText.Length
                Else
                    ' Multi-line paste
                    lPasteEndLine = lPasteStartLine + lLines.Length - 1
                    lPasteEndColumn = lLines(lLines.Length - 1).Length
                    
                    ' If we're inserting in the middle of a line, account for text after insertion point
                    If lPasteStartLine < pLineCount Then
                        Dim lOriginalLine As String = pSourceFileInfo.TextLines(lPasteStartLine)
                        If lPasteStartColumn < lOriginalLine.Length Then
                            ' Text after cursor will be moved to last pasted line
                            ' The InsertText method handles this correctly
                        End If
                    End If
                End If 
                
                ' Use atomic InsertText for the paste operation 
                ' This will properly update metadata and character tokens
                pSourceFileInfo.InsertText(lPasteStartLine, lPasteStartColumn, vText)
                
                ' CRITICAL FIX: For multi-line pastes, ensure immediate visual update
                If lLines.Length > 1 Then
                    Console.WriteLine($"OnClipboardTextReceived: Multi-line paste detected ({lLines.Length} lines)")
                    
                    ' Force immediate parsing for visual feedback
                    pSourceFileInfo.ForceImmediateParsing(lPasteStartLine, lPasteEndLine)
                    
                    ' Explicitly queue a redraw to show the updated coloring
                    If pDrawingArea IsNot Nothing Then
                        pDrawingArea.QueueDraw()
                        Console.WriteLine($"OnClipboardTextReceived: Queued redraw for lines {lPasteStartLine} to {lPasteEndLine}")
                    End If
                End If
                
                ' Move cursor to end of pasted text
                SetCursorPosition(lPasteEndLine, lPasteEndColumn)
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Automatically select the pasted text
                Console.WriteLine($"OnClipboardTextReceived: Selecting pasted text from ({lPasteStartLine},{lPasteStartColumn}) to ({lPasteEndLine},{lPasteEndColumn})")
                
                ' Set the selection to cover the pasted text
                pSelectionStartLine = lPasteStartLine
                pSelectionStartColumn = lPasteStartColumn
                pSelectionEndLine = lPasteEndLine
                pSelectionEndColumn = lPasteEndColumn
                pSelectionActive = True
                pHasSelection = True
                
                ' Ensure cursor is visible
                EnsureCursorVisible()
                
                ' Queue final redraw to show selection and updated text
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.QueueDraw()
                End If
                
                ' Mark as modified
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                Console.WriteLine($"OnClipboardTextReceived: Paste operation completed")
                
            Catch ex As Exception
                Console.WriteLine($"OnClipboardTextReceived error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles primary clipboard text when received (middle-click paste)
        ''' </summary>
        Private Sub OnPrimaryClipboardReceived(vClipboard As Clipboard, vText As String)
            Try
                ' Same as regular paste but from primary selection
                OnClipboardTextReceived(vClipboard, vText)
                
            Catch ex As Exception
                Console.WriteLine($"OnPrimaryClipboardReceived error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Selection Deletion =====

        ''' <summary>
        ''' Deletes the current selection using atomic operation
        ''' </summary>
        ''' <remarks>
        ''' Refactored to use atomic DeleteText method
        ''' </remarks>
        Public Sub DeleteSelection() Implements IEditor.DeleteSelection
            Try
                If Not pHasSelection OrElse pSourceFileInfo Is Nothing OrElse pIsReadOnly Then Return
                
                ' Get selection bounds
                Dim lStart As EditorPosition = GetSelectionStart()
                Dim lEnd As EditorPosition = GetSelectionEnd()
                
                ' Get the selected text for undo
                Dim lSelectedText As String = GetSelectedText()
                
                ' Record for undo
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.RecordDeleteText(lStart, lEnd, lSelectedText, lStart)
                End If
                
                ' Use atomic DeleteText
                pSourceFileInfo.DeleteText(lStart.Line, lStart.Column, lEnd.Line, lEnd.Column)
                
                ' Set cursor to deletion point
                SetCursorPosition(lStart.Line, lStart.Column)
                ClearSelection()
                
                ' Update state
                IsModified = True
                RaiseEvent TextChanged(Me, EventArgs.Empty)
                
                ' Update UI
                UpdateLineNumberWidth()
                UpdateScrollbars()
                EnsureCursorVisible()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteSelection error: {ex.Message}")
            End Try
        End Sub
  
        ''' <summary>
        ''' Helper method to delete text within a single line (if needed separately)
        ''' </summary>
        ''' <remarks>
        ''' This is now simplified to just use SourceFileInfo methods
        ''' </remarks>
        Private Sub DeleteSingleLineSelection(vLine As Integer, vStartColumn As Integer, vEndColumn As Integer)
            Try
                If vLine >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Ensure columns are within bounds
                Dim lLineLength As Integer = pSourceFileInfo.TextLines(vLine).Length
                vStartColumn = Math.Max(0, Math.Min(vStartColumn, lLineLength))
                vEndColumn = Math.Max(vStartColumn, Math.Min(vEndColumn, lLineLength))
                
                ' Delete through SourceFileInfo
                If vEndColumn > vStartColumn Then
                    pSourceFileInfo.DeleteText(vLine, vStartColumn, vLine, vEndColumn)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DeleteSingleLineSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper method to delete text spanning multiple lines (if needed separately)
        ''' </summary>
        ''' <remarks>
        ''' This is now simplified to use SourceFileInfo methods
        ''' </remarks>
        Private Sub DeleteMultiLineSelection(vStartLine As Integer, vStartColumn As Integer, 
                                            vEndLine As Integer, vEndColumn As Integer)
            Try
                ' Validate line indices
                If vStartLine >= pLineCount OrElse vEndLine >= pLineCount Then Return
                If pSourceFileInfo Is Nothing Then Return
                
                ' Get the text to keep from first line (before selection)
                Dim lFirstLine As String = pSourceFileInfo.TextLines(vStartLine)
                Dim lKeepFromFirst As String = If(vStartColumn > 0 AndAlso vStartColumn <= lFirstLine.Length,
                                                 lFirstLine.Substring(0, vStartColumn), "")
                
                ' Get the text to keep from last line (after selection)
                Dim lLastLine As String = pSourceFileInfo.TextLines(vEndLine)
                Dim lKeepFromLast As String = If(vEndColumn < lLastLine.Length,
                                                lLastLine.Substring(vEndColumn), "")
                
                ' Combine the kept portions
                pSourceFileInfo.TextLines(vStartLine) = lKeepFromFirst & lKeepFromLast
                
                ' Remove all lines between start and end (working backwards)
                for i As Integer = vEndLine To vStartLine + 1 Step -1
                    pSourceFileInfo.DeleteText(i, 0, i, pSourceFileInfo.TextLines(i).Length)
                Next
                
                ' Ensure we always have at least one line
                If pLineCount = 0 Then
                    pSourceFileInfo.TextLines.Add("")
                End If
                
                ' Request async parse
                pSourceFileInfo.RequestAsyncParse()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteMultiLineSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Performs a smart paste operation that strips artifact comments and fixes indentation
        ''' </summary>
        ''' <remarks>
        ''' This method:
        ''' 1. Strips leading comment lines that start with single quote
        ''' 2. Strips empty lines at the beginning
        ''' 3. Adjusts indentation to match the current context
        ''' </remarks>
        Public Sub SmartPaste() Implements IEditor.SmartPaste
            Try
                ' Get clipboard text
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                lClipboard.RequestText(AddressOf OnSmartPasteTextReceived)
                
            Catch ex As Exception
                Console.WriteLine($"SmartPaste error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles clipboard text for smart paste operation
        ''' </summary>
        ''' <param name="vClipboard">The clipboard source</param>
        ''' <param name="vText">The text to paste</param>
        Private Sub OnSmartPasteTextReceived(vClipboard As Clipboard, vText As String)
            Try
                If String.IsNullOrEmpty(vText) OrElse pIsReadOnly Then Return
                
                ' Process the text before pasting
                Dim lProcessedText As String = ProcessTextForSmartPaste(vText)
                
                ' Now paste the processed text using the regular paste mechanism
                OnClipboardTextReceived(vClipboard, lProcessedText)
                
            Catch ex As Exception
                Console.WriteLine($"OnSmartPasteTextReceived error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes clipboard text for smart paste by stripping comments and fixing indentation
        ''' </summary>
        ''' <param name="vText">The raw clipboard text</param>
        ''' <returns>Processed text ready for pasting</returns>
        Private Function ProcessTextForSmartPaste(vText As String) As String
            Try
                If String.IsNullOrEmpty(vText) Then Return vText
                
                ' Step 1: Strip leading comment lines and empty lines
                Dim lLines As List(Of String) = vText.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None).ToList()
                
                ' Remove leading lines that are comments or empty
                While lLines.Count > 0
                    Dim lFirstLine As String = lLines(0)
                    Dim lTrimmed As String = lFirstLine.TrimStart()
                    
                    ' Check if it's a comment line (starts with ') or empty
                    If lTrimmed.StartsWith("'") OrElse String.IsNullOrWhiteSpace(lFirstLine) Then
                        lLines.RemoveAt(0)
                    Else
                        Exit While
                    End If
                End While
                
                ' If we removed all lines, return empty
                If lLines.Count = 0 Then Return ""
                
                ' Step 2: Determine the base indentation of the pasted content
                Dim lMinIndent As Integer = Integer.MaxValue
                For Each lLine As String In lLines
                    ' Skip empty lines when calculating minimum indent
                    If Not String.IsNullOrWhiteSpace(lLine) Then
                        Dim lIndent As Integer = GetLineIndentation(lLine)
                        If lIndent < lMinIndent Then
                            lMinIndent = lIndent
                        End If
                    End If
                Next
                
                ' If no non-empty lines found, use 0
                If lMinIndent = Integer.MaxValue Then lMinIndent = 0
                
                ' Step 3: Determine the target indentation based on current context
                Dim lTargetIndent As Integer = DetermineContextIndentation()
                
                ' Step 4: Adjust indentation of all lines
                Dim lAdjustedLines As New List(Of String)
                For Each lLine As String In lLines
                    If String.IsNullOrWhiteSpace(lLine) Then
                        ' Preserve empty lines
                        lAdjustedLines.Add("")
                    Else
                        ' Calculate this line's relative indentation
                        Dim lCurrentIndent As Integer = GetLineIndentation(lLine)
                        Dim lRelativeIndent As Integer = lCurrentIndent - lMinIndent
                        Dim lNewIndent As Integer = lTargetIndent + lRelativeIndent
                        
                        ' Build the new line with adjusted indentation
                        Dim lContent As String = lLine.TrimStart()
                        Dim lIndentString As String = New String(" "c, lNewIndent)
                        lAdjustedLines.Add(lIndentString & lContent)
                    End If
                Next
                
                ' Join the lines back together
                Return String.Join(Environment.NewLine, lAdjustedLines)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessTextForSmartPaste error: {ex.Message}")
                ' On error, return original text
                Return vText
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the number of leading spaces in a line
        ''' </summary>
        ''' <param name="vLine">The line to analyze</param>
        ''' <returns>Number of leading spaces</returns>
        Private Function GetLineIndentation(vLine As String) As Integer
            Try
                Dim lCount As Integer = 0
                For Each lChar As Char In vLine
                    If lChar = " "c Then
                        lCount += 1
                    ElseIf lChar = vbTab Then
                        ' Count tabs as 4 spaces (or use TabWidth setting)
                        lCount += 4
                    Else
                        Exit For
                    End If
                Next
                Return lCount
                
            Catch ex As Exception
                Console.WriteLine($"GetLineIndentation error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Determines the appropriate indentation level for the current paste context
        ''' </summary>
        ''' <returns>Number of spaces to indent</returns>
        Private Function DetermineContextIndentation() As Integer
            Try
                ' Get the current line where we're pasting
                Dim lCurrentLine As Integer = pCursorLine
                If pHasSelection Then
                    ' If there's a selection, use the start line
                    Dim lStartPos As New EditorPosition(pSelectionStartLine, pSelectionStartColumn)
                    Dim lEndPos As New EditorPosition(pSelectionEndLine, pSelectionEndColumn)
                    NormalizeSelection(lStartPos, lEndPos)
                    lCurrentLine = lStartPos.Line
                End If
                
                ' Analyze the syntax tree to determine context
                If pSourceFileInfo?.SyntaxTree IsNot Nothing Then
                    ' Find which node we're inside
                    Dim lContextNode As SyntaxNode = FindContainingNode(pSourceFileInfo.SyntaxTree, lCurrentLine)
                    
                    If lContextNode IsNot Nothing Then
                        ' Calculate indentation based on node type and nesting level
                        Return CalculateNodeIndentation(lContextNode)
                    End If
                End If
                
                ' Fallback: Look at nearby lines to determine indentation
                Return InferIndentationFromNearbyLines(lCurrentLine)
                
            Catch ex As Exception
                Console.WriteLine($"DetermineContextIndentation error: {ex.Message}")
                Return 4  ' Default to one indentation level
            End Try
        End Function
        
        ''' <summary>
        ''' Finds the syntax node containing the specified line
        ''' </summary>
        Private Function FindContainingNode(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            Try
                ' Check if this node contains the line
                If vLine >= vNode.StartLine AndAlso vLine <= vNode.EndLine Then
                    ' Check children for more specific container
                    For Each lChild As SyntaxNode In vNode.Children
                        Dim lChildResult As SyntaxNode = FindContainingNode(lChild, vLine)
                        If lChildResult IsNot Nothing Then
                            Return lChildResult
                        End If
                    Next
                    
                    ' No child contains it, so this node is the container
                    Return vNode
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindContainingNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Calculates the appropriate indentation for content within a syntax node
        ''' </summary>
        Private Function CalculateNodeIndentation(vNode As SyntaxNode) As Integer
            Try
                Dim lIndentLevel As Integer = 0
                Dim lIndentSize As Integer = 4  ' Or use TabWidth setting
                
                ' Walk up the tree to calculate nesting
                Dim lCurrent As SyntaxNode = vNode
                While lCurrent IsNot Nothing
                    Select Case lCurrent.NodeType
                        Case CodeNodeType.eNamespace
                            lIndentLevel += 1
                            
                        Case CodeNodeType.eClass, CodeNodeType.eStructure, 
                             CodeNodeType.eModule, CodeNodeType.eInterface
                            lIndentLevel += 1
                            
                        Case CodeNodeType.eMethod, CodeNodeType.eFunction, 
                             CodeNodeType.eProperty, CodeNodeType.eConstructor
                            ' Methods/properties are typically at class level + 1
                            ' But their content would be at class level + 2
                            ' Since we're pasting inside, add the extra level
                            lIndentLevel += 1
                            
                        Case CodeNodeType.eEnum
                            lIndentLevel += 1
                    End Select
                    
                    lCurrent = lCurrent.Parent
                End While
                
                ' For content inside methods, add one more level
                If vNode.NodeType = CodeNodeType.eMethod OrElse 
                   vNode.NodeType = CodeNodeType.eFunction OrElse
                   vNode.NodeType = CodeNodeType.eProperty OrElse
                   vNode.NodeType = CodeNodeType.eConstructor Then
                    lIndentLevel += 1
                End If
                
                Return lIndentLevel * lIndentSize
                
            Catch ex As Exception
                Console.WriteLine($"CalculateNodeIndentation error: {ex.Message}")
                Return 4
            End Try
        End Function
        
        ''' <summary>
        ''' Infers indentation from nearby non-empty lines
        ''' </summary>
        Private Function InferIndentationFromNearbyLines(vCurrentLine As Integer) As Integer
            Try
                ' Look at previous non-empty lines to determine indentation
                For i As Integer = vCurrentLine - 1 To Math.Max(0, vCurrentLine - 10) Step -1
                    If i >= 0 AndAlso i < pSourceFileInfo.TextLines.Count Then
                        Dim lLine As String = pSourceFileInfo.TextLines(i)
                        If Not String.IsNullOrWhiteSpace(lLine) Then
                            ' Check if this is a method/property declaration
                            Dim lTrimmed As String = lLine.TrimStart().ToLower()
                            If lTrimmed.StartsWith("Public ") OrElse
                               lTrimmed.StartsWith("Private ") OrElse
                               lTrimmed.StartsWith("Protected ") OrElse
                               lTrimmed.StartsWith("Friend ") OrElse
                               lTrimmed.StartsWith("Sub ") OrElse
                               lTrimmed.StartsWith("Function ") OrElse
                               lTrimmed.StartsWith("Property ") Then
                                ' Found a method/property declaration
                                ' Return its indentation
                                Return GetLineIndentation(lLine)
                            End If
                        End If
                    End If
                Next
                
                ' Default to one indentation level
                Return 4
                
            Catch ex As Exception
                Console.WriteLine($"InferIndentationFromNearbyLines error: {ex.Message}")
                Return 4
            End Try
        End Function
        
        ''' <summary>
        ''' Ensures proper syntax coloring after paste operations
        ''' </summary>
        ''' <param name="vStartLine">First line that was modified by paste</param>
        ''' <param name="vEndLine">Last line that was modified by paste</param>
        ''' <param name="vTextLength">Total length of pasted text</param>
        ''' <remarks>
        ''' This method delegates to SourceFileInfo for the actual parsing and coloring work.
        ''' It handles scheduling and UI updates while SourceFileInfo handles the parsing logic.
        ''' FIXED: Added explicit QueueDraw after ForceImmediateParsing to ensure redraw happens.
        ''' </remarks>
        Private Sub EnsurePasteColoring(vStartLine As Integer, vEndLine As Integer, vTextLength As Integer)
            Try
                ' Small pastes will be handled by normal async parsing
                If vTextLength < 100 AndAlso (vEndLine - vStartLine) < 3 Then
                    ' Small paste - let normal async parse handle it  
                    ' But still force immediate parsing for better UX
                    Console.WriteLine($"EnsurePasteColoring: Small paste, Using immediate parsing")
                    If pSourceFileInfo IsNot Nothing Then
                        pSourceFileInfo.ForceImmediateParsing(vStartLine, vEndLine)
                        
                        ' CRITICAL: Explicitly queue redraw after parsing
                        If pDrawingArea IsNot Nothing Then
                            pDrawingArea.QueueDraw()
                            Console.WriteLine("EnsurePasteColoring: Redraw queued after small paste parsing")
                        End If
                    End If
                    Return
                End If
                
                Console.WriteLine($"EnsurePasteColoring: Ensuring colors for lines {vStartLine} To {vEndLine} ({vTextLength} chars)")
                
                If pSourceFileInfo Is Nothing Then
                    Console.WriteLine("EnsurePasteColoring: No SourceFileInfo available")
                    Return
                End If
                
                ' For medium pastes (100-1000 chars or 3-20 lines), use immediate parsing
                If vTextLength < 1000 OrElse (vEndLine - vStartLine) < 20 Then
                    Console.WriteLine("EnsurePasteColoring: Using immediate parsing for medium paste")
                    
                    ' Let SourceFileInfo handle all the parsing and coloring internally
                    pSourceFileInfo.ForceImmediateParsing(vStartLine, vEndLine)
                    
                    ' CRITICAL: Explicitly queue redraw to show the colored text
                    If pDrawingArea IsNot Nothing Then
                        pDrawingArea.QueueDraw()
                        Console.WriteLine("EnsurePasteColoring: Redraw queued after medium paste parsing")
                    End If
                    
                    ' Also request async parse for full document context
                    pSourceFileInfo.RequestAsyncParse()
                    
                Else
                    ' Large paste (1000+ chars or 20+ lines)
                    ' Use immediate parsing for visible portion, then progressive for rest
                    Console.WriteLine($"EnsurePasteColoring: Large paste detected, Using progressive coloring")
                    
                    ' First, parse and color the visible portion immediately
                    Dim lVisibleStart As Integer = Math.Max(vStartLine, pFirstVisibleLine)
                    Dim lVisibleEnd As Integer = Math.Min(vEndLine, pFirstVisibleLine + pTotalVisibleLines)
                    
                    If lVisibleEnd >= lVisibleStart Then
                        Console.WriteLine($"EnsurePasteColoring: Immediate parse for visible lines {lVisibleStart} To {lVisibleEnd}")
                        pSourceFileInfo.ForceImmediateParsing(lVisibleStart, lVisibleEnd)
                        
                        ' CRITICAL: Explicitly queue redraw
                        If pDrawingArea IsNot Nothing Then
                            pDrawingArea.QueueDraw()
                            Console.WriteLine("EnsurePasteColoring: Redraw queued after visible portion parsing")
                        End If
                    End If
                    
                    ' Request async parse for the full document
                    pSourceFileInfo.RequestAsyncParse()
                    
                    ' Schedule parsing for non-visible portions
                    Task.Run(Async Function() As Task
                        Try
                            ' Parse lines before visible area if needed
                            If vStartLine < lVisibleStart Then
                                Await Task.Delay(100)
                                Gtk.Application.Invoke(Sub()
                                    Try
                                        Console.WriteLine($"EnsurePasteColoring: Parsing pre-visible lines {vStartLine} To {lVisibleStart - 1}")
                                        pSourceFileInfo.ForceImmediateParsing(vStartLine, lVisibleStart - 1)
                                        pDrawingArea?.QueueDraw()
                                    Catch ex As Exception
                                        Console.WriteLine($"EnsurePasteColoring pre-visible error: {ex.Message}")
                                    End Try
                                End Sub)
                            End If
                            
                            ' Parse lines after visible area if needed
                            If vEndLine > lVisibleEnd Then
                                Await Task.Delay(200)
                                Gtk.Application.Invoke(Sub()
                                    Try
                                        Console.WriteLine($"EnsurePasteColoring: Parsing post-visible lines {lVisibleEnd + 1} To {vEndLine}")
                                        pSourceFileInfo.ForceImmediateParsing(lVisibleEnd + 1, vEndLine)
                                        pDrawingArea?.QueueDraw()
                                    Catch ex As Exception
                                        Console.WriteLine($"EnsurePasteColoring post-visible error: {ex.Message}")
                                    End Try
                                End Sub)
                            End If
                            
                        Catch ex As Exception
                            Console.WriteLine($"EnsurePasteColoring task error: {ex.Message}")
                        End Try
                        
                        'Return Task.CompletedTask
                    End Function)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"EnsurePasteColoring error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
