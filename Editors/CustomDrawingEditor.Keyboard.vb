' Editors/CustomDrawingEditor.Keyboard.vb - Updated keyboard handlers with Ctrl shortcuts
Imports System
Imports System.Collections.Generic
Imports Gtk
Imports Gdk
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor


        ''' <summary>
        ''' Handles key press events with mouse cursor auto-hide
        ''' </summary>
        Private Function OnKeyPress(vSender As Object, vArgs As KeyPressEventArgs) As Boolean
            Try
                ' Hide mouse cursor when typing
                HideMouseCursor()
                
                ' Reset cursor blink
                pCursorBlink = True
                pCursorVisible = True
                InvalidateCursor()
                Console.WriteLine($"OnKeypress in CustomDrawingEditor.Keyboard.vb called.")
                
                ' Get key and modifiers
                Dim lKey As Gdk.Key = CType(vArgs.Event.Key, Gdk.Key)
                Dim lModifiers As ModifierType = vArgs.Event.State
                
                ' Debug output for Tab keys
                If lKey = Gdk.Key.Tab OrElse lKey = Gdk.Key.ISO_Left_Tab Then
                    Console.WriteLine($"Editor Tab Detection: Key={lKey}, KeyValue={vArgs.Event.KeyValue}, Modifiers={lModifiers}")
                End If
                
                ' SPECIAL HANDLING FOR TAB AND SHIFT+TAB (by KeyValue to avoid enum issues)
                ' Tab = 65289 (0xFF09), ISO_Left_Tab = 65056 (0xFE20)
                If vArgs.Event.KeyValue = 65289 Then
                    ' Regular Tab key
                    If Not pIsReadOnly Then
                        If (lModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                            ' Shift+Tab - Always outdent
                            Console.WriteLine("Tab with Shift modifier - calling OutdentSelection()")
                            OutdentSelection()
                        Else
                            ' Regular Tab
                            Console.WriteLine($"Regular Tab - SelectionActive={pSelectionActive}, HasSelection={pHasSelection}")
                            If pSelectionActive OrElse pHasSelection Then
                                Console.WriteLine("Has selection - calling IndentSelection()")
                                IndentSelection()
                            Else
                                Console.WriteLine("No selection - inserting Tab character")
                                InsertText(vbTab)
                            End If
                        End If
                    End If
                    vArgs.RetVal = True
                    Return True
                ElseIf vArgs.Event.KeyValue = 65056 Then
                    ' ISO_Left_Tab (this IS Shift+Tab on many systems)
                    If Not pIsReadOnly Then
                        Console.WriteLine("ISO_Left_Tab (Shift+Tab) detected - calling OutdentSelection()")
                        OutdentSelection()
                    End If
                    vArgs.RetVal = True
                    Return True
                End If
                
                ' CRITICAL FIX: Allow function keys to bubble up to MainWindow
                ' F1-F12 keys should not be consumed by the editor
                Select Case lKey
                    Case Gdk.Key.F1, Gdk.Key.F2, Gdk.Key.F3, Gdk.Key.F4, Gdk.Key.F5, Gdk.Key.F6,
                         Gdk.Key.F7, Gdk.Key.F8, Gdk.Key.F9, Gdk.Key.F10, Gdk.Key.F11, Gdk.Key.F12
                        ' Let function keys pass through to MainWindow
                        Console.WriteLine($"Function key {lKey} detected - passing to MainWindow")
                        vArgs.RetVal = False
                        Return False
                End Select
                
                ' Check for Control key combinations first (shortcuts)
                If (lModifiers and ModifierType.ControlMask) = ModifierType.ControlMask Then
                    ' Handle keyboard shortcuts using keyval to avoid ambiguity
                    ' Using ASCII values: a=97, c=99, f=102, g=103, r=114, s=115, v=118, x=120, y=121, z=122
                    Select Case vArgs.Event.KeyValue
                        ' Undo - Ctrl+Z (122)
                        Case 122, 90  ' z or Z
                            If (lModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask Then
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
                            
                        ' Cut/Copy/Paste - Ctrl+X/C/V
                        Case 120, 88  ' x or X
                            Cut()
                            vArgs.RetVal = True
                            Return True
                            
                        Case 99, 67  ' c or C
                            Copy()
                            vArgs.RetVal = True
                            Return True
                            
                        Case 118, 86  ' v or V
                            If (lModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                                ' Ctrl+Shift+V - Smart Paste
                                SmartPaste()
                            Else
                                ' Ctrl+V - Regular Paste
                                Paste()
                            End If
                            vArgs.RetVal = True
                            Return True
                            
                        ' Select All - Ctrl+A (97)
                        Case 97, 65  ' a or A
                            SelectAll()
                            vArgs.RetVal = True
                            Return True
                            
                        ' Cut Line - Ctrl+Y (121) - VB.NET traditional shortcut
                        Case 121, 89  ' y or Y
                            If Not pIsReadOnly Then
                                CutLine()
                            End If
                            vArgs.RetVal = True
                            Return True
                            
                        ' Find - Ctrl+F (102)
                        Case 102, 70  ' f or F
                            ' Let this bubble up to MainWindow
                            vArgs.RetVal = False
                            Return False
                            
                        ' Go to Line - Ctrl+G (103)
                        Case 103, 71  ' g or G
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
                        ' Zoom In - Ctrl+Plus (43 for plus, 61 for equals/plus without shift)
                        Case 43, 61, 65451  ' +, =, KP_Plus
                            ZoomIn()
                            vArgs.RetVal = True
                            Return True
                            
                        ' Zoom Out - Ctrl+Minus (45 for minus, 95 for underscore/minus)
                        Case 45, 65453  ' -, KP_Minus
                            ZoomOut()
                            vArgs.RetVal = True
                            Return True
                            
                        ' Zoom Reset - Ctrl+0 (48 for zero)
                        Case 48, 65456  ' 0, KP_0
                            ZoomReset()
                            vArgs.RetVal = True
                            Return True
                    End Select
                    
                    ' Check for Ctrl+Arrow keys (word navigation)
                    Select Case lKey
                        Case Gdk.Key.Left, Gdk.Key.KP_Left
                            HandleLeftKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                            
                        Case Gdk.Key.Right, Gdk.Key.KP_Right
                            HandleRightKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                            
                        Case Gdk.Key.Home, Gdk.Key.KP_Home
                            HandleHomeKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                            
                        Case Gdk.Key.End, Gdk.Key.KP_End
                            HandleEndKey(lModifiers)
                            vArgs.RetVal = True
                            Return True
                    End Select
                End If
                
                ' Handle regular navigation keys
                Select Case lKey
                    Case Gdk.Key.Up, Gdk.Key.KP_Up
                        pSourceFileInfo.SetLineMetadataAndCharacterTokens(pCursorLinep)
                        HandleUpKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Down, Gdk.Key.KP_Down
                        pSourceFileInfo.SetLineMetadataAndCharacterTokens(pCursorLinep)
                        HandleDownKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Left, Gdk.Key.KP_Left
                        HandleLeftKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Right, Gdk.Key.KP_Right
                        HandleRightKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Home, Gdk.Key.KP_Home
                        HandleHomeKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.End, Gdk.Key.KP_End
                        HandleEndKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Page_Up, Gdk.Key.KP_Page_Up
                        HandlePageUpKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Page_Down, Gdk.Key.KP_Page_Down
                        HandlePageDownKey(lModifiers)
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Escape
                        ' Clear selection on Escape
                        ClearSelection()
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Insert
                        ' Toggle insert/overwrite mode
                        pInsertMode = Not pInsertMode
                        ' Note: UpdateStatusBar method doesn't exist in CustomDrawingEditor
                        ' Status updates are handled by MainWindow
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Delete, Gdk.Key.KP_Delete
                        If Not pIsReadOnly Then
                            HandleDelete()
                        End If
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.BackSpace
                        If Not pIsReadOnly Then
                            HandleBackspace()
                        End If
                        vArgs.RetVal = True
                        Return True
                        
                    Case Gdk.Key.Return, Gdk.Key.KP_Enter
                        If Not pIsReadOnly Then
                            HandleReturn()
                        End If
                        vArgs.RetVal = True
                        Return True
                        
                    Case Else
                        ' CRITICAL FIX: Handle keypad characters first
                        ' Keypad numbers and operators need special handling
                        If HandleKeypadCharacter(lKey) Then
                            vArgs.RetVal = True
                            Return True
                        End If
                        
                        ' Handle regular printable characters
                        If Not pIsReadOnly AndAlso vArgs.Event.KeyValue >= 32 AndAlso vArgs.Event.KeyValue < 127 Then
                            Dim lChar As Char = ChrW(vArgs.Event.KeyValue)
                            
                            If pHasSelection Then
                                ' Delete selection first
                                Dim lStart As EditorPosition = GetSelectionStart()
                                Dim lEnd As EditorPosition = GetSelectionEnd()
                                pSourceFileInfo.DeleteText(lStart.Line, lStart.Column, lEnd.Line, lEnd.Column)
                                SetCursorPosition(lStart.Line, lStart.Column)
                                ClearSelection()
                            End If
                            
                            ' Insert the character
                            pSourceFileInfo.InsertCharacter(pCursorLine, pCursorColumn, lChar)
                            
                            ' Move cursor forward
                            SetCursorPosition(pCursorLine, pCursorColumn + 1)
                            
                            ' Track modification
                            OnTextModified()
                            
                            ' Redraw
                            InvalidateLine(pCursorLine)
                            
                            vArgs.RetVal = True
                            Return True
                        Else
                            ' Debug: Log any unhandled keys
                            Console.WriteLine($"Editor Unhandled Key: Key={lKey}, KeyValue={vArgs.Event.KeyValue}, Modifiers={lModifiers}")
                            ' Let unhandled keys pass through
                            vArgs.RetVal = False
                            Return False
                        End If
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnKeyPress error: {ex.Message}")
                vArgs.RetVal = False
                Return False
            End Try
        End Function

        
        Private Sub HandleTab(vModifiers As ModifierType)
            Try
                If (vModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask Then
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
        
        
        ' ===== Word Navigation Methods =====
        Private Sub MoveToPreviousWord()
            Try
                Dim lLine As String = TextLines(pCursorLine)
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
                    SetCursorPosition(pCursorLine - 1, TextLines(pCursorLine - 1).Length)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MoveToPreviousWord error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub MoveToNextWord()
            Try
                Dim lLine As String = TextLines(pCursorLine)
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
                Dim lShift As Boolean = (vModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If pCursorLine > 0 Then
                    Dim lNewLine As Integer = pCursorLine - 1
                    Dim lNewColumn As Integer = Math.Min(pDesiredColumn, TextLines(lNewLine).Length)
                    
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
                Dim lShift As Boolean = (vModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask
                
                If lShift AndAlso Not pSelectionActive Then
                    StartSelection(pCursorLine, pCursorColumn)
                ElseIf Not lShift Then
                    ClearSelection()
                End If
                
                If pCursorLine < pLineCount - 1 Then
                    Dim lNewLine As Integer = pCursorLine + 1
                    Dim lNewColumn As Integer = Math.Min(pDesiredColumn, TextLines(lNewLine).Length)
                    
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
                Dim lShift As Boolean = (vModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask
                Dim lCtrl As Boolean = (vModifiers and ModifierType.ControlMask) = ModifierType.ControlMask
                
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
                        SetCursorPosition(pCursorLine - 1, TextLines(pCursorLine - 1).Length)
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
                Dim lShift As Boolean = (vModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask
                Dim lCtrl As Boolean = (vModifiers and ModifierType.ControlMask) = ModifierType.ControlMask
                
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
                    If pCursorColumn < TextLines(pCursorLine).Length Then
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
        
        ''' <summary>
        ''' Handles Home key press with proper scrolling
        ''' </summary>
        ''' <param name="vModifiers">Active modifier keys</param>
        Private Sub HandleHomeKey(vModifiers As ModifierType)
            Try
                If vModifiers.HasFlag(ModifierType.ControlMask) Then
                    ' Ctrl+Home - go to start of document
                    If vModifiers.HasFlag(ModifierType.ShiftMask) Then
                        ' Ctrl+Shift+Home - extend selection to start
                        If Not pHasSelection Then
                            StartSelection(pCursorLine, pCursorColumn)
                        End If
                        SetCursorPosition(0, 0)
                        UpdateSelection(pCursorLine, pCursorColumn)
                    Else
                        ' Just move cursor to start
                        SetCursorPosition(0, 0)
                        If pHasSelection Then
                            ClearSelection()
                        End If
                    End If
                    
                    ' Scroll to top
                    ScrollToTop()
                    
                Else
                    ' Regular Home - go to start of line
                    If vModifiers.HasFlag(ModifierType.ShiftMask) Then
                        ' Shift+Home - extend selection to start of line
                        If Not pHasSelection Then
                            StartSelection(pCursorLine, pCursorColumn)
                        End If
                        SetCursorPosition(pCursorLine, 0)
                        UpdateSelection(pCursorLine, pCursorColumn)
                    Else
                        ' Just move cursor to start of line
                        SetCursorPosition(pCursorLine, 0)
                        If pHasSelection Then
                            ClearSelection()
                        End If
                    End If
                End If
                
                InvalidateCursor()
                
            Catch ex As Exception
                Console.WriteLine($"HandleHomeKey error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles End key press with proper scrolling when bottom panel is visible
        ''' </summary>
        ''' <param name="vModifiers">Active modifier keys</param>
        Private Sub HandleEndKey(vModifiers As ModifierType)
            Try
                If vModifiers.HasFlag(ModifierType.ControlMask) Then
                    ' Ctrl+End - go to end of document
                    Dim lLastLine As Integer = pLineCount - 1
                    Dim lLastLineLength As Integer = If(TextLines(lLastLine)?.Length, 0)
                    
                    If vModifiers.HasFlag(ModifierType.ShiftMask) Then
                        ' Ctrl+Shift+End - extend selection to end
                        If Not pHasSelection Then
                            StartSelection(pCursorLine, pCursorColumn)
                        End If
                        SetCursorPosition(lLastLine, lLastLineLength)
                        UpdateSelection(pCursorLine, pCursorColumn)
                    Else
                        ' Just move cursor to end
                        SetCursorPosition(lLastLine, lLastLineLength)
                        If pHasSelection Then
                            ClearSelection()
                        End If
                    End If
                    
                    ' FIXED: Force scroll to bottom with proper calculation
                    ScrollToBottom()
                    
                Else
                    ' Regular End - go to end of line
                    Dim lLineLength As Integer = If(TextLines(pCursorLine)?.Length, 0)
                    
                    If vModifiers.HasFlag(ModifierType.ShiftMask) Then
                        ' Shift+End - extend selection to end of line
                        If Not pHasSelection Then
                            StartSelection(pCursorLine, pCursorColumn)
                        End If
                        SetCursorPosition(pCursorLine, lLineLength)
                        UpdateSelection(pCursorLine, pCursorColumn)
                    Else
                        ' Just move cursor to end of line
                        SetCursorPosition(pCursorLine, lLineLength)
                        If pHasSelection Then
                            ClearSelection()
                        End If
                    End If
                End If
                
                InvalidateCursor()
                
            Catch ex As Exception
                Console.WriteLine($"HandleEndKey error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub HandlePageUpKey(vModifiers As ModifierType)
            Try
                Dim lShift As Boolean = (vModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask
                
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
                Dim lShift As Boolean = (vModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask
                
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
        
        ''' <summary>
        ''' Handles keypad character input using atomic operations
        ''' </summary>
        ''' <param name="vKey">The keypad key pressed</param>
        ''' <returns>True if handled, False otherwise</returns>
        ''' <remarks>
        ''' Refactored to use atomic character operations exclusively
        ''' </remarks>
        Private Function HandleKeypadCharacter(vKey As Gdk.Key) As Boolean
            Try
                If pIsReadOnly OrElse pSourceFileInfo Is Nothing Then Return False
                
                Dim lChar As Char? = Nothing
                
                ' Map keypad keys to characters
                Select Case vKey
                    Case Gdk.Key.KP_0 : lChar = "0"c
                    Case Gdk.Key.KP_1 : lChar = "1"c
                    Case Gdk.Key.KP_2 : lChar = "2"c
                    Case Gdk.Key.KP_3 : lChar = "3"c
                    Case Gdk.Key.KP_4 : lChar = "4"c
                    Case Gdk.Key.KP_5 : lChar = "5"c
                    Case Gdk.Key.KP_6 : lChar = "6"c
                    Case Gdk.Key.KP_7 : lChar = "7"c
                    Case Gdk.Key.KP_8 : lChar = "8"c
                    Case Gdk.Key.KP_9 : lChar = "9"c
                    Case Gdk.Key.KP_Add : lChar = "+"c
                    Case Gdk.Key.KP_Subtract : lChar = "-"c
                    Case Gdk.Key.KP_Multiply : lChar = "*"c
                    Case Gdk.Key.KP_Divide : lChar = "/"c
                    Case Gdk.Key.KP_Decimal : lChar = "."c
                    Case Gdk.Key.KP_Equal : lChar = "="c
                    Case Gdk.Key.KP_Space : lChar = " "c
                    Case Gdk.Key.KP_Tab : lChar = vbTab(0)
                    Case Else
                        Return False  ' Not a keypad character
                End Select
                
                If lChar.HasValue Then
                    ' Handle character insertion using atomic operation
                    If pHasSelection Then
                        DeleteSelection()
                    End If
                    
                    ' Handle insert vs overwrite mode
                    If pInsertMode OrElse pCursorColumn >= pSourceFileInfo.TextLines(pCursorLine).Length Then
                        ' Insert mode or at end of line - use atomic insert
                        InsertCharacter(lChar.Value)
                    Else
                        ' Overwrite mode - delete then insert
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.BeginUserAction()
                        End If
                        
                        ' Delete the character at cursor
                        DeleteCharacterAt(pCursorLine, pCursorColumn)
                        ' Insert the new character
                        InsertCharacter(lChar.Value)
                        
                        If pUndoRedoManager IsNot Nothing Then
                            pUndoRedoManager.EndUserAction()
                        End If
                    End If
                    
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"HandleKeypadCharacter error: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Class
    
End Namespace
