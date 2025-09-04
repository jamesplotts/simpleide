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

        Private pThemeColorCache() As Cairo.Color
        Private pThemeFontCache As Dictionary(Of String, Pango.FontDescription)

        
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
        Private pCurrentLineColor As String = "#D4D4D4"
        
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
                
                Dim lLine As String = TextLines(pCursorLine)
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
                    Dim lLineText As String = TextLines(lLine)
                    
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
                    Dim lLineText As String = TextLines(lLine)
                    
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
                        lColumn = TextLines(lLine).Length - 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"SearchForMatchingBracketBackward error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Initializes the Cairo color cache from the current theme
        ''' </summary>
        ''' <remarks>
        ''' This caches all theme colors as Cairo.Color objects for fast rendering.
        ''' Called when theme changes or editor is initialized.
        ''' </remarks>
        Private Sub InitializeThemeColorCache()
            Try
                ' Get the current theme
                Dim lTheme As EditorTheme = GetActiveTheme()
                If lTheme Is Nothing Then 
                    Console.WriteLine("InitializeThemeColorCache: No active theme")
                    Return
                End If
                
                ' Allocate color cache array
                ReDim pThemeColorCache(SyntaxTokenType.eLastValue)
                
                ' Cache all token type colors as Cairo.Color objects
                pThemeColorCache(SyntaxTokenType.eNormal) = HexToCairoColor(lTheme.ForegroundColor)
                pThemeColorCache(SyntaxTokenType.eIdentifier) = HexToCairoColor(lTheme.ForegroundColor)
                pThemeColorCache(SyntaxTokenType.eOperator) = HexToCairoColor(lTheme.ForegroundColor)
                
                ' Use syntax colors if available
                If lTheme.SyntaxColors IsNot Nothing Then
                    pThemeColorCache(SyntaxTokenType.eKeyword) = HexToCairoColor(
                        lTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword))
                    pThemeColorCache(SyntaxTokenType.eString) = HexToCairoColor(
                        lTheme.SyntaxColors(SyntaxColorSet.Tags.eString))
                    pThemeColorCache(SyntaxTokenType.eComment) = HexToCairoColor(
                        lTheme.SyntaxColors(SyntaxColorSet.Tags.eComment))
                    pThemeColorCache(SyntaxTokenType.eNumber) = HexToCairoColor(
                        lTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber))
                    pThemeColorCache(SyntaxTokenType.eType) = HexToCairoColor(
                        lTheme.SyntaxColors(SyntaxColorSet.Tags.eType))
                    pThemeColorCache(SyntaxTokenType.ePreprocessor) = HexToCairoColor(
                        lTheme.SyntaxColors(SyntaxColorSet.Tags.ePreprocessor))
                Else
                    ' Fallback colors if syntax colors not available
                    pThemeColorCache(SyntaxTokenType.eKeyword) = HexToCairoColor("#569CD6")
                    pThemeColorCache(SyntaxTokenType.eString) = HexToCairoColor("#CE9178")
                    pThemeColorCache(SyntaxTokenType.eComment) = HexToCairoColor("#6A9955")
                    pThemeColorCache(SyntaxTokenType.eNumber) = HexToCairoColor("#B5CEA8")
                    pThemeColorCache(SyntaxTokenType.eType) = HexToCairoColor("#4EC9B0")
                    pThemeColorCache(SyntaxTokenType.ePreprocessor) = HexToCairoColor("#C586C0")
                End If
                
                ' Initialize font cache for bold/italic variants
                If pThemeFontCache Is Nothing Then
                    pThemeFontCache = New Dictionary(Of String, Pango.FontDescription)()
                Else
                    ' Dispose old font descriptions
                    For Each lFont In pThemeFontCache.Values
                        lFont?.Dispose()
                    Next
                    pThemeFontCache.Clear()
                End If
                
                ' Cache font variants
                If pFontDescription IsNot Nothing Then
                    ' Normal font
                    pThemeFontCache("Normal") = pFontDescription.Copy()
                    
                    ' Bold font
                    Dim lBoldFont As Pango.FontDescription = pFontDescription.Copy()
                    lBoldFont.Weight = Pango.Weight.Bold
                    pThemeFontCache("Bold") = lBoldFont
                    
                    ' Italic font
                    Dim lItalicFont As Pango.FontDescription = pFontDescription.Copy()
                    lItalicFont.Style = Pango.Style.Italic
                    pThemeFontCache("Italic") = lItalicFont
                    
                    ' Bold-Italic font
                    Dim lBoldItalicFont As Pango.FontDescription = pFontDescription.Copy()
                    lBoldItalicFont.Weight = Pango.Weight.Bold
                    lBoldItalicFont.Style = Pango.Style.Italic
                    pThemeFontCache("BoldItalic") = lBoldItalicFont
                End If
                
                Console.WriteLine($"InitializeThemeColorCache: Cached {pThemeColorCache.Length} colors")
                
            Catch ex As Exception
                Console.WriteLine($"InitializeThemeColorCache error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets the cached Cairo color for a specific token type
        ''' </summary>
        ''' <param name="vTokenType">The syntax token type</param>
        ''' <returns>The cached Cairo.Color for the token type</returns>
        Private Function GetCachedTokenColor(vTokenType As SyntaxTokenType) As Cairo.Color
            Try
                ' Ensure cache is initialized
                If pThemeColorCache Is Nothing OrElse pThemeColorCache.Length = 0 Then
                    InitializeThemeColorCache()
                End If
                
                ' Validate token type
                If vTokenType < 0 OrElse vTokenType >= SyntaxTokenType.eLastValue Then
                    vTokenType = SyntaxTokenType.eNormal
                End If
                
                ' Return cached color
                Return pThemeColorCache(vTokenType)
                
            Catch ex As Exception
                Console.WriteLine($"GetCachedTokenColor error: {ex.Message}")
                ' Return default foreground color as fallback
                Return New Cairo.Color(0.9, 0.9, 0.9) ' Light gray default
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the cached font for the specified style combination
        ''' </summary>
        ''' <param name="vBold">Whether the font should be bold</param>
        ''' <param name="vItalic">Whether the font should be italic</param>
        ''' <returns>The cached Pango.FontDescription for the style</returns>
        ''' <remarks>
        ''' Returns cached font descriptions for Normal, Bold, Italic, or BoldItalic styles.
        ''' Falls back to pFontDescription if cache entry doesn't exist.
        ''' </remarks>
        Private Function GetCachedFont(vBold As Boolean, vItalic As Boolean) As Pango.FontDescription
            Try
                ' Ensure font cache is initialized
                If pThemeFontCache Is Nothing Then
                    InitializeThemeColorCache()
                End If
                
                ' Determine cache key
                Dim lKey As String
                If vBold AndAlso vItalic Then
                    lKey = "BoldItalic"
                ElseIf vBold Then
                    lKey = "Bold"
                ElseIf vItalic Then
                    lKey = "Italic"
                Else
                    lKey = "Normal"
                End If
                
                ' Return cached font if it exists
                If pThemeFontCache.ContainsKey(lKey) Then
                    Return pThemeFontCache(lKey)
                End If
                
                ' Fallback to normal font
                Return pFontDescription
                
            Catch ex As Exception
                Console.WriteLine($"GetCachedFont error: {ex.Message}")
                ' Return default font as fallback
                Return pFontDescription
            End Try
        End Function
        
    End Class
    
End Namespace
