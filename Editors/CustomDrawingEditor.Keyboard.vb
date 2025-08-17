' Editors/CustomDrawingEditor.Keyboard.vb - Updated keyboard handlers with Ctrl shortcuts
Imports System
Imports System.Collections.Generic
Imports Gtk
Imports Gdk
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Key Press Event Handler =====
        Private Function OnKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
            Try
                ' Reset cursor blink
                pCursorBlink = True
                pCursorVisible = True
                InvalidateCursor()
                
                Dim lKey As Gdk.key = CType(vArgs.Event.key, Gdk.key)
                Dim lModifiers As ModifierType = vArgs.Event.State
                
                ' CRITICAL FIX: Allow function keys to bubble up to MainWindow for accelerators
                ' F1-F12 keys should not be consumed by the editor
                Select Case lKey
                    Case Gdk.key.F1, Gdk.key.F2, Gdk.key.F3, Gdk.key.F4, Gdk.key.F5, Gdk.key.F6,
                         Gdk.key.F7, Gdk.key.F8, Gdk.key.F9, Gdk.key.F10, Gdk.key.F11, Gdk.key.F12
                        ' Let function keys pass through to MainWindow accelerators
                        vArgs.RetVal = False
                        Return False
                End Select
                
                ' Check for Control key combinations first (shortcuts)
                If (lModifiers And ModifierType.ControlMask) = ModifierType.ControlMask Then
                    ' Handle keyboard shortcuts using keyval to avoid ambiguity
                    ' Using ASCII values: a=97, c=99, f=102, r=114, s=115, v=118, x=120, y=121, z=122
                    Select Case vArgs.Event.KeyValue
                        ' Undo - Ctrl+Z (122)
                        Case 122, 90  ' z or Z
                            If (lModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                                ' Ctrl+Shift+Z - Redo
                                If CanRedo Then
                                    Redo()
                                End If
                            Else
                                ' Ctrl+Z - Undo
                                If CanUndo Then
                                    Undo()
                                End If
                            End If
                            vArgs.RetVal = True
                            Return True
                            
                        ' Redo - Ctrl+R (114)
                        Case 114, 82  ' r or R
                            If CanRedo Then
                                Redo()
                            End If
                            vArgs.RetVal = True
                            Return True
                            
                        ' Cut Line - Ctrl+Y (121)
                        Case 121, 89  ' y or Y
                            If Not pIsReadOnly Then
                                CutLine()
                            End If
                            vArgs.RetVal = True
                            Return True
                            
                        ' Select All - Ctrl+A (97)
                        Case 97, 65  ' a or A
                            SelectAll()
                            vArgs.RetVal = True
                            Return True
                            
                        ' Copy - Ctrl+C (99)
                        Case 99, 67  ' c or C
                            Copy()
                            vArgs.RetVal = True
                            Return True
                            
                        ' Cut - Ctrl+X (120)
                        Case 120, 88  ' x or X
                            If Not pIsReadOnly Then
                                Cut()
                            End If
                            vArgs.RetVal = True
                            Return True
                            
                        ' Paste - Ctrl+V (118)
                        Case 118, 86  ' v or V
                            If Not pIsReadOnly Then
                                Paste()
                            End If
                            vArgs.RetVal = True
                            Return True
                            
                        ' Find - Ctrl+F (102)
                        Case 102, 70  ' f or F
                            ' Let this bubble up to MainWindow
                            vArgs.RetVal = False
                            Return False
                            
                        ' Save - Ctrl+S (115)
                        Case 115, 83  ' s or S
                            ' Let this bubble up to MainWindow  
                            vArgs.RetVal = False
                            Return False
                            
                        ' Toggle comment - Ctrl+/ (47 or shift+/)
                        Case 47, 95  ' / or _
                            If Not pIsReadOnly Then
                                ToggleCommentBlock()
                            End If
                            vArgs.RetVal = True
                            Return True
                    End Select
                    
                    ' Check for Ctrl+Arrow keys (word navigation)
                    Select Case lKey
                        Case Gdk.key.Left, Gdk.key.KP_Left
                            HandleLeftKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                            
                        Case Gdk.key.Right, Gdk.key.KP_Right
                            HandleRightKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                            
                        Case Gdk.key.Home, Gdk.key.KP_Home
                            HandleHomeKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                            
                        Case Gdk.key.End, Gdk.key.KP_End
                            HandleEndKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                    End Select
                End If
                
                ' Handle navigation keys (without Control modifier)
                Select Case lKey
                    Case Gdk.key.Up, Gdk.key.KP_Up
                        HandleUpKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Down, Gdk.key.KP_Down
                        HandleDownKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Left, Gdk.key.KP_Left
                        HandleLeftKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Right, Gdk.key.KP_Right
                        HandleRightKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Home, Gdk.key.KP_Home
                        HandleHomeKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.End, Gdk.key.KP_End
                        HandleEndKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Page_Up, Gdk.key.KP_Page_Up
                        HandlePageUpKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Page_Down, Gdk.key.KP_Page_Down
                        HandlePageDownKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Escape
                        ' Clear selection on Escape
                        ClearSelection()
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Insert
                        ' Toggle insert/overwrite mode
                        pInsertMode = Not pInsertMode
                        ' Note: UpdateStatusBar method doesn't exist in CustomDrawingEditor
                        ' Status updates are handled by MainWindow
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Delete, Gdk.key.KP_Delete
                        If Not pIsReadOnly Then
                            HandleDelete()
                        End If
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.BackSpace
                        If Not pIsReadOnly Then
                            HandleBackspace()
                        End If
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Tab
                        If Not pIsReadOnly Then
                            If (lModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                                ' Shift+Tab - Unindent
                                OutdentSelection()
                            Else
                                ' Tab - Indent or insert tab
                                If HasSelection() Then
                                    IndentSelection()
                                Else
                                    InsertText(vbTab)
                                End If
                            End If
                        End If
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.key.Return, Gdk.key.KP_Enter
                        If Not pIsReadOnly Then
                            HandleReturn()
                        End If
                        vArgs.RetVal = True
                        Return True
                        
                    Case Else
                        ' Handle printable characters
                        If Not pIsReadOnly AndAlso vArgs.Event.KeyValue >= 32 AndAlso vArgs.Event.KeyValue < 127 Then
                            InsertCharacter(Convert.ToChar(vArgs.Event.KeyValue))
                            vArgs.RetVal = True
                            Return True
                        End If
                End Select
                
                ' Default: don't consume the event
                vArgs.RetVal = False
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnKeyPress error: {ex.Message}")
                vArgs.RetVal = False
                Return False
            End Try
        End Function

        Private Sub HandleBackspace()
            Try
                If pSelectionActive Then
                    ' Delete selection
                    DeleteSelection()
                ElseIf pCursorColumn > 0 Then
                    ' Delete character before cursor
                    Dim lLine As String = pTextLines(pCursorLine)
                    pTextLines(pCursorLine) = lLine.Remove(pCursorColumn - 1, 1)
                    pLineMetadata(pCursorLine).MarkChanged()
                    SetCursorPosition(pCursorLine, pCursorColumn - 1)
                    
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs())
                ElseIf pCursorLine > 0 Then
                    ' Join with previous line
                    Dim lPrevLine As String = pTextLines(pCursorLine - 1)
                    Dim lCurrentLine As String = pTextLines(pCursorLine)
                    Dim lNewColumn As Integer = lPrevLine.Length
                    
                    pTextLines(pCursorLine - 1) = lPrevLine & lCurrentLine
                    pTextLines.RemoveAt(pCursorLine)
                    
                    ' Handle metadata array (it's an array, not a List)
                    RemoveLineMetadata(pCursorLine)
                    pLineCount -= 1
                    
                    SetCursorPosition(pCursorLine - 1, lNewColumn)
                    
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs())
                End If
                
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleBackspace error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleDelete()
            Try
                If pSelectionActive Then
                    ' Delete selection
                    DeleteSelection()
                ElseIf pCursorColumn < pTextLines(pCursorLine).Length Then
                    ' Delete character at cursor
                    Dim lLine As String = pTextLines(pCursorLine)
                    pTextLines(pCursorLine) = lLine.Remove(pCursorColumn, 1)
                    pLineMetadata(pCursorLine).MarkChanged()
                    
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs())
                ElseIf pCursorLine < pLineCount - 1 Then
                    ' Join with next line
                    Dim lCurrentLine As String = pTextLines(pCursorLine)
                    Dim lNextLine As String = pTextLines(pCursorLine + 1)
                    
                    pTextLines(pCursorLine) = lCurrentLine & lNextLine
                    pTextLines.RemoveAt(pCursorLine + 1)
                    
                    ' Handle metadata array
                    RemoveLineMetadata(pCursorLine + 1)
                    pLineCount -= 1
                    
                    IsModified = True
                    RaiseEvent TextChanged(Me, New EventArgs())
                End If
                
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleDelete error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleReturn()
            Try
                If pSelectionActive Then
                    DeleteSelection()
                End If
                
                Dim lLine As String = pTextLines(pCursorLine)
                Dim lLeftPart As String = lLine.Substring(0, pCursorColumn)
                Dim lRightPart As String = lLine.Substring(pCursorColumn)
                
                ' Calculate indentation for new line
                Dim lIndent As String = ""
                For Each lChar As Char In lLeftPart
                    If lChar = " "c OrElse lChar = vbTab Then
                        lIndent &= lChar
                    Else
                        Exit For
                    End If
                Next
                
                ' Update current line and insert new line
                pTextLines(pCursorLine) = lLeftPart
                pTextLines.Insert(pCursorLine + 1, lIndent & lRightPart)
                
                ' Handle metadata array
                InsertLineMetadata(pCursorLine + 1)
                pLineCount += 1
                
                SetCursorPosition(pCursorLine + 1, lIndent.Length)
                
                IsModified = True
                RaiseEvent TextChanged(Me, New EventArgs())
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"HandleReturn error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleTab(vModifiers As ModifierType)
            Try
                If (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    ' Shift+Tab - outdent
                    If pSelectionActive Then
                        ' Outdent selected lines
                        OutdentSelection()
                    Else
                        ' Remove leading tab/spaces from current line
                        OutdentLine(pCursorLine)
                    End If
                Else
                    ' Tab - indent
                    If pSelectionActive Then
                        ' Indent selected lines
                        IndentSelection()
                    Else
                        ' Insert tab character
                        InsertText(vbTab)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandleTab error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods for Array Management =====
        Private Sub RemoveLineMetadata(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pLineMetadata.Length Then Return
                
                ' Create new arrays without the specified line
                Dim lNewMetadata(pLineMetadata.Length - 2) As LineMetadata
                Dim lNewColors(pCharacterColors.Length - 2)() As CharacterColorInfo
                
                ' Copy before removed line
                For i As Integer = 0 To vLineIndex - 1
                    lNewMetadata(i) = pLineMetadata(i)
                    lNewColors(i) = pCharacterColors(i)
                Next
                
                ' Copy after removed line
                For i As Integer = vLineIndex + 1 To pLineMetadata.Length - 1
                    lNewMetadata(i - 1) = pLineMetadata(i)
                    lNewColors(i - 1) = pCharacterColors(i)
                Next
                
                pLineMetadata = lNewMetadata
                pCharacterColors = lNewColors
                
            Catch ex As Exception
                Console.WriteLine($"RemoveLineMetadata error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InsertLineMetadata(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex > pLineMetadata.Length Then Return
                
                ' Create new arrays with one more element
                Dim lNewMetadata(pLineMetadata.Length) As LineMetadata
                Dim lNewColors(pCharacterColors.Length)() As CharacterColorInfo
                
                ' Copy before insertion point
                For i As Integer = 0 To vLineIndex - 1
                    If i < pLineMetadata.Length Then
                        lNewMetadata(i) = pLineMetadata(i)
                        lNewColors(i) = pCharacterColors(i)
                    End If
                Next
                
                ' Insert new metadata
                lNewMetadata(vLineIndex) = New LineMetadata()
                lNewColors(vLineIndex) = New CharacterColorInfo() {}
                
                ' Copy after insertion point
                For i As Integer = vLineIndex To pLineMetadata.Length - 1
                    lNewMetadata(i + 1) = pLineMetadata(i)
                    lNewColors(i + 1) = pCharacterColors(i)
                Next
                
                pLineMetadata = lNewMetadata
                pCharacterColors = lNewColors
                
            Catch ex As Exception
                Console.WriteLine($"InsertLineMetadata error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Word Navigation Methods =====
        Private Sub MoveToPreviousWord()
            Try
                Dim lLine As String = pTextLines(pCursorLine)
                Dim lColumn As Integer = pCursorColumn
                
                ' Skip current word if we're in the middle of one
                While lColumn > 0 AndAlso IsWordChar(lLine(lColumn - 1))
                    lColumn -= 1
                End While
                
                ' Skip whitespace
                While lColumn > 0 AndAlso Not IsWordChar(lLine(lColumn - 1))
                    lColumn -= 1
                End While
                
                ' Find start of previous word
                While lColumn > 0 AndAlso IsWordChar(lLine(lColumn - 1))
                    lColumn -= 1
                End While
                
                If lColumn <> pCursorColumn Then
                    SetCursorPosition(pCursorLine, lColumn)
                ElseIf pCursorLine > 0 Then
                    ' Move to end of previous line
                    SetCursorPosition(pCursorLine - 1, pTextLines(pCursorLine - 1).Length)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveToPreviousWord error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveToNextWord()
            Try
                Dim lLine As String = pTextLines(pCursorLine)
                Dim lColumn As Integer = pCursorColumn
                
                ' Skip current word
                While lColumn < lLine.Length AndAlso IsWordChar(lLine(lColumn))
                    lColumn += 1
                End While
                
                ' Skip whitespace
                While lColumn < lLine.Length AndAlso Not IsWordChar(lLine(lColumn))
                    lColumn += 1
                End While
                
                If lColumn <> pCursorColumn Then
                    SetCursorPosition(pCursorLine, lColumn)
                ElseIf pCursorLine < pLineCount - 1 Then
                    ' Move to beginning of next line
                    SetCursorPosition(pCursorLine + 1, 0)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveToNextWord error: {ex.Message}")
            End Try
        End Sub
        
        Private Function IsWordChar(vChar As Char) As Boolean
            Return Char.IsLetterOrDigit(vChar) OrElse vChar = "_"c
        End Function
        
        ' ===== Key Release Event Handler =====
        Private Function OnKeyRelease(vSender As Object, vArgs As KeyReleaseEventArgs) As Boolean
            Try
                ' Currently not used but available for future features
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnKeyRelease error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Navigation Key Handlers =====
        Private Sub HandleUpKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If pCursorLine > 0 Then
                    Dim lNewLine As Integer = pCursorLine - 1
                    Dim lNewColumn As Integer = Math.Min(pDesiredColumn, pTextLines(lNewLine).Length)
                    
                    SetCursorPosition(lNewLine, lNewColumn)
                    
                    If lShift Then
                        UpdateSelection(lNewLine, lNewColumn)
                    End If
                    
                    EnsureCursorVisible()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandleUpKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleDownKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If pCursorLine < pLineCount - 1 Then
                    Dim lNewLine As Integer = pCursorLine + 1
                    Dim lNewColumn As Integer = Math.Min(pDesiredColumn, pTextLines(lNewLine).Length)
                    
                    SetCursorPosition(lNewLine, lNewColumn)
                    
                    If lShift Then
                        UpdateSelection(lNewLine, lNewColumn)
                    End If
                    
                    EnsureCursorVisible()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandleDownKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleLeftKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                Dim lCtrl As Boolean = (vModifiers And ModifierType.ControlMask) = ModifierType.ControlMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If lCtrl Then
                    ' Move by word
                    MoveToPreviousWord()
                Else
                    ' Move by character
                    If pCursorColumn > 0 Then
                        SetCursorPosition(pCursorLine, pCursorColumn - 1)
                    ElseIf pCursorLine > 0 Then
                        ' Move to end of previous line
                        SetCursorPosition(pCursorLine - 1, pTextLines(pCursorLine - 1).Length)
                    End If
                End If
                
                pDesiredColumn = pCursorColumn
                
                If lShift Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                End If
                
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"HandleLeftKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleRightKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                Dim lCtrl As Boolean = (vModifiers And ModifierType.ControlMask) = ModifierType.ControlMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If lCtrl Then
                    ' Move by word
                    MoveToNextWord()
                Else
                    ' Move by character
                    If pCursorColumn < pTextLines(pCursorLine).Length Then
                        SetCursorPosition(pCursorLine, pCursorColumn + 1)
                    ElseIf pCursorLine < pLineCount - 1 Then
                        ' Move to beginning of next line
                        SetCursorPosition(pCursorLine + 1, 0)
                    End If
                End If
                
                pDesiredColumn = pCursorColumn
                
                If lShift Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                End If
                
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"HandleRightKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleHomeKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                Dim lCtrl As Boolean = (vModifiers And ModifierType.ControlMask) = ModifierType.ControlMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If lCtrl Then
                    ' Move to beginning of document
                    SetCursorPosition(0, 0)
                Else
                    ' Move to beginning of line
                    SetCursorPosition(pCursorLine, 0)
                End If
                
                pDesiredColumn = pCursorColumn
                
                If lShift Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                End If
                
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"HandleHomeKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandleEndKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                Dim lCtrl As Boolean = (vModifiers And ModifierType.ControlMask) = ModifierType.ControlMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If lCtrl Then
                    ' Move to end of document
                    SetCursorPosition(pLineCount - 1, pTextLines(pLineCount - 1).Length)
                Else
                    ' Move to end of line
                    SetCursorPosition(pCursorLine, pTextLines(pCursorLine).Length)
                End If
                
                pDesiredColumn = pCursorColumn
                
                If lShift Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                End If
                
                EnsureCursorVisible()
                
            Catch ex As Exception
                Console.WriteLine($"HandleEndKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandlePageUpKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                PageUp()
                
                If lShift Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandlePageUpKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandlePageDownKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                PageDown()
                
                If lShift Then
                    UpdateSelection(pCursorLine, pCursorColumn)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"HandlePageDownKey error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Focus Events =====
        Private Function OnFocusIn(vSender As Object, vArgs As FocusInEventArgs) As Boolean
            Try
                pCursorVisible = True
                pDrawingArea.QueueDraw()
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnFocusIn error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function OnFocusOut(vSender As Object, vArgs As FocusOutEventArgs) As Boolean
            Try
                pCursorVisible = False
                pDrawingArea.QueueDraw()
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"OnFocusOut error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Cut Line Implementation (Ctrl+Y) =====
        ''' <summary>
        ''' Cuts the entire current line to clipboard (VB classic Ctrl+Y behavior)
        ''' </summary>
        Private Sub CutLine()
            Try
                If pIsReadOnly OrElse pLineCount = 0 Then Return
                
                ' Begin undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.BeginUserAction()
                End If
                
                ' Get the current line text including line ending
                Dim lLineText As String = pTextLines(pCursorLine)
                If pCursorLine < pLineCount - 1 Then
                    ' Not the last line, include the line ending
                    lLineText &= Environment.NewLine
                End If
                
                ' Copy line text to clipboard
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                lClipboard.Text = lLineText
                
                ' Also copy to primary selection (X11 style)
                Dim lPrimary As Clipboard = Clipboard.Get(Gdk.Selection.Primary)
                lPrimary.Text = lLineText
                
                ' Delete the line
                If pLineCount > 1 Then
                    ' Multiple lines - remove this line
                    If pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.RecordDeleteLine(pCursorLine, lLineText, pCursorLine, 0)
                    End If
                    
                    pTextLines.RemoveAt(pCursorLine)
                    RemoveLineMetadata(pCursorLine)
                    pLineCount -= 1
                    
                    ' Adjust cursor position
                    If pCursorLine >= pLineCount Then
                        ' Was on last line, move to new last line
                        SetCursorPosition(pLineCount - 1, 0)
                    Else
                        ' Stay on same line number (which now has the next line's content)
                        SetCursorPosition(pCursorLine, 0)
                    End If
                Else
                    ' Only one line - just clear it
                    If pUndoRedoManager IsNot Nothing Then
                        pUndoRedoManager.RecordDelete(pCursorLine, 0, lLineText)
                    End If
                    
                    pTextLines(0) = ""
                    pLineMetadata(0).MarkChanged()
                    SetCursorPosition(0, 0)
                End If
                
                ' Clear any selection
                ClearSelection()
                
                ' End undo group
                If pUndoRedoManager IsNot Nothing Then
                    pUndoRedoManager.EndUserAction()
                End If
                
                ' Update UI
                IsModified = True
                UpdateLineNumberWidth()
                UpdateScrollbars()
                pDrawingArea.QueueDraw()
                RaiseEvent TextChanged(Me, New EventArgs())
                
            Catch ex As Exception
                Console.WriteLine($"CutLine error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
