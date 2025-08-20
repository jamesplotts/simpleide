' KeyboardHelper.vb - Utility functions for keyboard handling
Imports System
Imports Gdk

' KeyboardHelper.vb
' Created: 2025-08-20 14:11:42

Namespace Utilities
    
    ''' <summary>
    ''' Provides helper methods for keyboard event handling
    ''' </summary>
    Public Module KeyboardHelper
        
        ''' <summary>
        ''' Converts a GDK KeyValue to a human-readable string representation
        ''' </summary>
        ''' <param name="vKeyValue">The KeyValue from KeyPressEventArgs.Event.KeyValue</param>
        ''' <returns>String representation of the key (e.g., "Return", "Tab", "a", "A", "F1")</returns>
        Public Function GetKeyString(vKeyValue As UInteger) As String
            Try
                ' Try to cast to Gdk.Key enum
                Dim lKey As Gdk.Key = CType(vKeyValue, Gdk.Key)
                
                ' Handle special keys with friendly names
                Select Case lKey
                    ' Enter/Return keys
                    Case Gdk.Key.Return
                        Return "Return"
                    Case Gdk.Key.KP_Enter
                        Return "KP_Enter"
                        
                    ' Tab keys
                    Case Gdk.Key.Tab
                        Return "Tab"
                    Case Gdk.Key.ISO_Left_Tab
                        Return "Shift+Tab"
                        
                    ' Escape
                    Case Gdk.Key.Escape
                        Return "Escape"
                        
                    ' Space
                    Case Gdk.Key.space
                        Return "Space"
                        
                    ' Backspace and Delete
                    Case Gdk.Key.BackSpace
                        Return "BackSpace"
                    Case Gdk.Key.Delete
                        Return "Delete"
                    Case Gdk.Key.KP_Delete
                        Return "KP_Delete"
                        
                    ' Arrow keys
                    Case Gdk.Key.Up
                        Return "Up"
                    Case Gdk.Key.Down
                        Return "Down"
                    Case Gdk.Key.Left
                        Return "Left"
                    Case Gdk.Key.Right
                        Return "Right"
                    Case Gdk.Key.KP_Up
                        Return "KP_Up"
                    Case Gdk.Key.KP_Down
                        Return "KP_Down"
                    Case Gdk.Key.KP_Left
                        Return "KP_Left"
                    Case Gdk.Key.KP_Right
                        Return "KP_Right"
                        
                    ' Navigation keys
                    Case Gdk.Key.Home
                        Return "Home"
                    Case Gdk.Key.End
                        Return "End"
                    Case Gdk.Key.Page_Up
                        Return "PageUp"
                    Case Gdk.Key.Page_Down
                        Return "PageDown"
                    Case Gdk.Key.KP_Home
                        Return "KP_Home"
                    Case Gdk.Key.KP_End
                        Return "KP_End"
                    Case Gdk.Key.KP_Page_Up
                        Return "KP_PageUp"
                    Case Gdk.Key.KP_Page_Down
                        Return "KP_PageDown"
                        
                    ' Insert
                    Case Gdk.Key.Insert
                        Return "Insert"
                    Case Gdk.Key.KP_Insert
                        Return "KP_Insert"
                        
                    ' Function keys
                    Case Gdk.Key.F1
                        Return "F1"
                    Case Gdk.Key.F2
                        Return "F2"
                    Case Gdk.Key.F3
                        Return "F3"
                    Case Gdk.Key.F4
                        Return "F4"
                    Case Gdk.Key.F5
                        Return "F5"
                    Case Gdk.Key.F6
                        Return "F6"
                    Case Gdk.Key.F7
                        Return "F7"
                    Case Gdk.Key.F8
                        Return "F8"
                    Case Gdk.Key.F9
                        Return "F9"
                    Case Gdk.Key.F10
                        Return "F10"
                    Case Gdk.Key.F11
                        Return "F11"
                    Case Gdk.Key.F12
                        Return "F12"
                        
                    ' Modifier keys
                    Case Gdk.Key.Shift_L
                        Return "Shift_L"
                    Case Gdk.Key.Shift_R
                        Return "Shift_R"
                    Case Gdk.Key.Control_L
                        Return "Ctrl_L"
                    Case Gdk.Key.Control_R
                        Return "Ctrl_R"
                    Case Gdk.Key.Alt_L
                        Return "Alt_L"
                    Case Gdk.Key.Alt_R
                        Return "Alt_R"
                    Case Gdk.Key.Super_L
                        Return "Super_L"
                    Case Gdk.Key.Super_R
                        Return "Super_R"
                    Case Gdk.Key.Menu
                        Return "Menu"
                        
                    ' Lock keys
                    Case Gdk.Key.Caps_Lock
                        Return "CapsLock"
                    Case Gdk.Key.Num_Lock
                        Return "NumLock"
                    Case Gdk.Key.Scroll_Lock
                        Return "ScrollLock"
                        
                    ' Print Screen/Break
                    Case Gdk.Key.Print
                        Return "PrintScreen"
                    Case Gdk.Key.Pause
                        Return "Pause"
                    Case Gdk.Key.Break
                        Return "Break"
                        
                    ' Numeric keypad operators
                    Case Gdk.Key.KP_Add
                        Return "KP_Plus"
                    Case Gdk.Key.KP_Subtract
                        Return "KP_Minus"
                    Case Gdk.Key.KP_Multiply
                        Return "KP_Multiply"
                    Case Gdk.Key.KP_Divide
                        Return "KP_Divide"
                    Case Gdk.Key.KP_Decimal
                        Return "KP_Decimal"
                    Case Gdk.Key.KP_Separator
                        Return "KP_Separator"
                        
                    ' Numeric keypad numbers
                    Case Gdk.Key.KP_0
                        Return "KP_0"
                    Case Gdk.Key.KP_1
                        Return "KP_1"
                    Case Gdk.Key.KP_2
                        Return "KP_2"
                    Case Gdk.Key.KP_3
                        Return "KP_3"
                    Case Gdk.Key.KP_4
                        Return "KP_4"
                    Case Gdk.Key.KP_5
                        Return "KP_5"
                    Case Gdk.Key.KP_6
                        Return "KP_6"
                    Case Gdk.Key.KP_7
                        Return "KP_7"
                    Case Gdk.Key.KP_8
                        Return "KP_8"
                    Case Gdk.Key.KP_9
                        Return "KP_9"
                        
                    ' Regular number keys
                    Case Gdk.Key.Key_0
                        Return "0"
                    Case Gdk.Key.Key_1
                        Return "1"
                    Case Gdk.Key.Key_2
                        Return "2"
                    Case Gdk.Key.Key_3
                        Return "3"
                    Case Gdk.Key.Key_4
                        Return "4"
                    Case Gdk.Key.Key_5
                        Return "5"
                    Case Gdk.Key.Key_6
                        Return "6"
                    Case Gdk.Key.Key_7
                        Return "7"
                    Case Gdk.Key.Key_8
                        Return "8"
                    Case Gdk.Key.Key_9
                        Return "9"
                        
                    ' Symbol keys
                    Case Gdk.Key.plus
                        Return "+"
                    Case Gdk.Key.minus
                        Return "-"
                    Case Gdk.Key.asterisk
                        Return "*"
                    Case Gdk.Key.slash
                        Return "/"
                    Case Gdk.Key.equal
                        Return "="
                    Case Gdk.Key.underscore
                        Return "_"
                    Case Gdk.Key.period
                        Return "."
                    Case Gdk.Key.comma
                        Return ","
                    Case Gdk.Key.semicolon
                        Return ";"
                    Case Gdk.Key.colon
                        Return ":"
                    Case Gdk.Key.question
                        Return "?"
                    Case Gdk.Key.exclam
                        Return "!"
                    Case Gdk.Key.at
                        Return "@"
                    Case Gdk.Key.numbersign
                        Return "#"
                    Case Gdk.Key.dollar
                        Return "$"
                    Case Gdk.Key.percent
                        Return "%"
                    Case Gdk.Key.ampersand
                        Return "&"
                    Case Gdk.Key.parenleft
                        Return "("
                    Case Gdk.Key.parenright
                        Return ")"
                    Case Gdk.Key.bracketleft
                        Return "["
                    Case Gdk.Key.bracketright
                        Return "]"
                    Case Gdk.Key.braceleft
                        Return "{"
                    Case Gdk.Key.braceright
                        Return "}"
                    Case Gdk.Key.less
                        Return "<"
                    Case Gdk.Key.greater
                        Return ">"
                    Case Gdk.Key.bar
                        Return "|"
                    Case Gdk.Key.backslash
                        Return "\"
                    Case Gdk.Key.quotedbl
                        Return """"
                    Case Gdk.Key.apostrophe
                        Return "'"
                    Case Gdk.Key.grave
                        Return "`"
                    Case Gdk.Key.asciitilde
                        Return "~"
                    Case Gdk.Key.asciicircum
                        Return "^"
                        
                    Case Else
                        ' For regular ASCII characters (a-z, A-Z)
                        If vKeyValue >= 32 AndAlso vKeyValue < 127 Then
                            Return Convert.ToChar(vKeyValue).ToString()
                        Else
                            ' For any unhandled keys, return the enum name
                            Return lKey.ToString()
                        End If
                End Select
                
            Catch ex As Exception
                ' If we can't convert to enum, try to get char for printable range
                If vKeyValue >= 32 AndAlso vKeyValue < 127 Then
                    Return Convert.ToChar(vKeyValue).ToString()
                Else
                    Return $"Key_{vKeyValue}"
                End If
            End Try
        End Function
        
        ''' <summary>
        ''' Builds a complete keyboard shortcut string including modifiers
        ''' </summary>
        ''' <param name="vKeyValue">The KeyValue from KeyPressEventArgs.Event.KeyValue</param>
        ''' <param name="vModifiers">The modifier state from KeyPressEventArgs.Event.State</param>
        ''' <returns>Complete shortcut string (e.g., "Ctrl+S", "Ctrl+Shift+Tab")</returns>
        Public Function GetKeyboardShortcutString(vKeyValue As UInteger, vModifiers As ModifierType) As String
            Try
                Dim lParts As New List(Of String)
                
                ' Add modifiers in standard order
                If (vModifiers And ModifierType.ControlMask) = ModifierType.ControlMask Then
                    lParts.Add("Ctrl")
                End If
                
                If (vModifiers And ModifierType.Mod1Mask) = ModifierType.Mod1Mask Then ' Alt key
                    lParts.Add("Alt")
                End If
                
                If (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    ' Only add Shift if it's not already implied by the key
                    ' (e.g., don't show "Shift+!" since ! already implies Shift)
                    Dim lKeyString As String = GetKeyString(vKeyValue)
                    If Not IsShiftImpliedKey(lKeyString) Then
                        lParts.Add("Shift")
                    End If
                End If
                
                If (vModifiers And ModifierType.SuperMask) = ModifierType.SuperMask Then ' Windows/Super key
                    lParts.Add("Super")
                End If
                
                ' Add the key itself
                lParts.Add(GetKeyString(vKeyValue))
                
                ' Join with +
                Return String.Join("+", lParts)
                
            Catch ex As Exception
                Console.WriteLine($"GetKeyboardShortcutString error: {ex.Message}")
                Return GetKeyString(vKeyValue)
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a key string already implies Shift was pressed
        ''' </summary>
        Private Function IsShiftImpliedKey(vKeyString As String) As Boolean
            ' These characters already imply Shift was pressed
            Dim lShiftChars() As String = {"!", "@", "#", "$", "%", "^", "&", "*", "(", ")", 
                                          "_", "+", "{", "}", "|", ":", """", "<", ">", "?", "~"}
            
            Return lShiftChars.Contains(vKeyString) OrElse 
                   (vKeyString.Length = 1 AndAlso Char.IsUpper(vKeyString(0)))
        End Function
        
        ''' <summary>
        ''' Checks if a key combination matches a specific shortcut
        ''' </summary>
        ''' <param name="vKeyValue">The KeyValue from the event</param>
        ''' <param name="vModifiers">The modifier state from the event</param>
        ''' <param name="vTargetKey">The target key to match (e.g., "s", "S", "Tab")</param>
        ''' <param name="vRequireCtrl">Whether Ctrl must be pressed</param>
        ''' <param name="vRequireShift">Whether Shift must be pressed</param>
        ''' <param name="vRequireAlt">Whether Alt must be pressed</param>
        ''' <returns>True if the combination matches</returns>
        Public Function IsKeyboardShortcut(vKeyValue As UInteger, 
                                          vModifiers As ModifierType,
                                          vTargetKey As String,
                                          Optional vRequireCtrl As Boolean = False,
                                          Optional vRequireShift As Boolean = False,
                                          Optional vRequireAlt As Boolean = False) As Boolean
            Try
                ' Check modifiers
                Dim lHasCtrl As Boolean = (vModifiers And ModifierType.ControlMask) = ModifierType.ControlMask
                Dim lHasShift As Boolean = (vModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask
                Dim lHasAlt As Boolean = (vModifiers And ModifierType.Mod1Mask) = ModifierType.Mod1Mask
                
                ' Modifiers must match exactly
                If lHasCtrl <> vRequireCtrl OrElse
                   lHasShift <> vRequireShift OrElse
                   lHasAlt <> vRequireAlt Then
                    Return False
                End If
                
                ' Check the key
                Dim lKeyString As String = GetKeyString(vKeyValue)
                
                ' Case-insensitive comparison for letter keys
                Return String.Equals(lKeyString, vTargetKey, StringComparison.OrdinalIgnoreCase)
                
            Catch ex As Exception
                Console.WriteLine($"IsKeyboardShortcut error: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Module
    
End Namespace
