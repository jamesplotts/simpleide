' Editors/CustomDrawingEditor.Colors.vb - Color property declarations for theming
Imports Gtk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

' CustomDrawingEditor.Colors.vb
' Created: 2025-08-10 07:44:12

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Color Properties for Theming =====
        ' These are used by the Drawing and Settings partial classes
        
        Private pBackgroundColor As String = "#1E1E1E"
        Private pForegroundColor As String = "#D4D4D4"
        Private pLineNumberBgColor As String = "#2D2D30"
        Private pLineNumberFgColor As String = "#858585"
        Private pSelectionColor As String = "#264F78"
        Private pCursorColor As String = "#AEAFAD"
        Private pCurrentLineBgColor As String = "#2A2A2A"
        Private pFindHighlightColor As String = "#515C6A"
        Private pHighlightCurrentLine As Boolean = True
        Private pShowWhitespace As Boolean = False
        Private pShowEndOfLine As Boolean = False
        Private pCurrentLineColor as String = "#D4D4D4"
        
        ' ===== Events =====
        
        ' Event raised when theme changes
        Public Event ThemeChanged(vTheme As EditorTheme)
        
        ' Event raised when font changes
        Public Event FontChanged(vFontDescription As String)
        
        ' Event raised when a setting changes
        Public Event SettingChanged(vSettingName As String, vNewValue As Object)
        
        ' ===== Helper Methods =====
        
        ' Placeholder for UpdateBracketMatching method
        ' This method updates the visual highlighting of matching brackets
        Private Sub UpdateBracketMatching()
            Try
                ' Clear previous matching bracket
                pMatchingBracketLine = -1
                pMatchingBracketColumn = -1
                
                If Not pBracketHighlightingEnabled Then Return
                
                ' Get current cursor position
                If pCursorLine >= pLineCount Then Return
                
                Dim lLine As String = pTextLines(pCursorLine)
                If pCursorColumn >= lLine.Length Then Return
                
                ' Check character at cursor
                Dim lChar As Char = lLine(pCursorColumn)
                Dim lMatchChar As Char = Nothing
                Dim lSearchForward As Boolean = False
                
                ' Determine what to search for
                Select Case lChar
                    Case "("c
                        lMatchChar = ")"c
                        lSearchForward = True
                    Case ")"c
                        lMatchChar = "("c
                        lSearchForward = False
                    Case "["c
                        lMatchChar = "]"c
                        lSearchForward = True
                    Case "]"c
                        lMatchChar = "["c
                        lSearchForward = False
                    Case "{"c
                        lMatchChar = "}"c
                        lSearchForward = True
                    Case "}"c
                        lMatchChar = "{"c
                        lSearchForward = False
                    Case Else
                        Return ' Not a bracket
                End Select
                
                ' Search for matching bracket
                If lSearchForward Then
                    SearchForMatchingBracketForward(lChar, lMatchChar)
                Else
                    SearchForMatchingBracketBackward(lChar, lMatchChar)
                End If
                
                ' Queue redraw if match found
                If pMatchingBracketLine >= 0 Then
                    InvalidateLine(pMatchingBracketLine)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateBracketMatching error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SearchForMatchingBracketForward(vOpenChar As Char, vCloseChar As Char)
            Try
                Dim lNestLevel As Integer = 1
                Dim lLine As Integer = pCursorLine
                Dim lColumn As Integer = pCursorColumn + 1
                
                While lLine < pLineCount
                    Dim lLineText As String = pTextLines(lLine)
                    
                    While lColumn < lLineText.Length
                        Dim lChar As Char = lLineText(lColumn)
                        
                        If lChar = vOpenChar Then
                            lNestLevel += 1
                        ElseIf lChar = vCloseChar Then
                            lNestLevel -= 1
                            If lNestLevel = 0 Then
                                ' Found matching bracket
                                pMatchingBracketLine = lLine
                                pMatchingBracketColumn = lColumn
                                Return
                            End If
                        End If
                        
                        lColumn += 1
                    End While
                    
                    lLine += 1
                    lColumn = 0
                End While
                
            Catch ex As Exception
                Console.WriteLine($"SearchForMatchingBracketForward error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SearchForMatchingBracketBackward(vCloseChar As Char, vOpenChar As Char)
            Try
                Dim lNestLevel As Integer = 1
                Dim lLine As Integer = pCursorLine
                Dim lColumn As Integer = pCursorColumn - 1
                
                While lLine >= 0
                    Dim lLineText As String = pTextLines(lLine)
                    
                    If lColumn < 0 Then
                        lColumn = lLineText.Length - 1
                    End If
                    
                    While lColumn >= 0
                        Dim lChar As Char = lLineText(lColumn)
                        
                        If lChar = vCloseChar Then
                            lNestLevel += 1
                        ElseIf lChar = vOpenChar Then
                            lNestLevel -= 1
                            If lNestLevel = 0 Then
                                ' Found matching bracket
                                pMatchingBracketLine = lLine
                                pMatchingBracketColumn = lColumn
                                Return
                            End If
                        End If
                        
                        lColumn -= 1
                    End While
                    
                    lLine -= 1
                    If lLine >= 0 Then
                        lColumn = pTextLines(lLine).Length - 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"SearchForMatchingBracketBackward error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
