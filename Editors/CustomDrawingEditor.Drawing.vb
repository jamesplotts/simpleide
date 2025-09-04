' Editors/CustomDrawingEditor.Drawing.vb - Unified drawing implementation
'Option Strict
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax
Imports SimpleIDE.Managers

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        Private pThemeManager As ThemeManager

        ' ===== Main Drawing Event Handlers =====
        
        Private Shadows Function OnDrawn(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawContent(vArgs.Cr)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawn error: {ex.Message}")
                Return True
            End Try
        End Function
        
        
        ' ===== Unified Drawing Method =====
        


        ''' <summary>
        ''' Main content drawing method simplified to use CharacterColors array
        ''' </summary>
        ''' <remarks>
        ''' This version just reads from the CharacterColors array in SourceFileInfo
        ''' and draws each character with its pre-computed color. No parsing happens here.
        ''' </remarks>
        Private Sub DrawContent(vContext As Cairo.Context)
            Try
                Dim lTopOffset As Integer = -3
                Dim lBarLengthHalf As Integer = 4
        
                ' Create a layout for text rendering
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription
                
                ' Get current theme
                Dim lCurrentTheme As EditorTheme = GetActiveTheme()
        
                ' Get font metrics
                Dim lAscent As Integer = 0
                If pFontMetrics IsNot Nothing Then
                    lAscent = pFontMetrics.Ascent
                Else
                    lAscent = CInt(pLineHeight * 0.75)
                End If
                
                ' Draw background
                Dim lBgColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eBackgroundColor)
                Dim lBgPattern As New Cairo.SolidPattern(lBgColor.R, lBgColor.G, lBgColor.B)
                vContext.SetSource(lBgPattern)
                vContext.Rectangle(0, 0, pDrawingArea.AllocatedWidth, pDrawingArea.AllocatedHeight)
                vContext.Fill()
                lBgPattern.Dispose()
                
                ' Calculate visible range
                Dim lFirstLine As Integer = pFirstVisibleLine
                Dim lLastLine As Integer = Math.Min(lFirstLine + pTotalVisibleLines, pLineCount - 1)
                Dim lFirstColumn As Integer = pFirstVisibleColumn
                Dim lLastColumn As Integer = lFirstColumn + pTotalVisibleColumns

                
                ' Normalize selection bounds once
                Dim lSelStartLine As Integer = pSelectionStartLine
                Dim lSelStartCol As Integer = pSelectionStartColumn
                Dim lSelEndLine As Integer = pSelectionEndLine
                Dim lSelEndCol As Integer = pSelectionEndColumn
                If pSelectionActive Then
                    NormalizeSelection(lSelStartLine, lSelStartCol, lSelEndLine, lSelEndCol)
                End If
                
                ' Draw current line highlight if enabled
                If False AndAlso pCursorLine >= lFirstLine AndAlso pCursorLine <= lLastLine Then
                    Dim lLineY As Integer = (pCursorLine - lFirstLine) * pLineHeight + pTopPadding + lTopOffset
                    Dim lCurrentLineColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eCurrentLineColor)
                    Dim lCurrentLinePattern As New Cairo.SolidPattern(lCurrentLineColor.R, lCurrentLineColor.G, lCurrentLineColor.B)
                    vContext.SetSource(lCurrentLinePattern)
                    vContext.Rectangle(0, lLineY, pDrawingArea.AllocatedWidth, pLineHeight)
                    vContext.Fill()
                    lCurrentLinePattern.Dispose()
                End If
                
                ' Get default foreground color
                Dim lDefaultFgColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eForegroundColor)
                
                ' Draw text and selections character by character
                For lLineIndex As Integer = lFirstLine To lLastLine
                    If lLineIndex >= pLineCount Then Exit For
                    
                    ' Get line text from SourceFileInfo
                    Dim lLine As String = ""
                    If pSourceFileInfo IsNot Nothing AndAlso lLineIndex < pSourceFileInfo.TextLines.Count Then
                        lLine = pSourceFileInfo.TextLines(lLineIndex)
                    End If

                    ' SAFETY CHECK: Ensure character colors are initialized for this line
                    InitializeCharacterColorsIfNeeded(lLineIndex)
                    
                    ' Get character colors for this line
                    Dim lCharColors() As CharacterColorInfo = Nothing
                    If pSourceFileInfo IsNot Nothing AndAlso 
                       pSourceFileInfo.CharacterColors IsNot Nothing AndAlso 
                       lLineIndex < pSourceFileInfo.CharacterColors.Length Then
                        lCharColors = pSourceFileInfo.CharacterColors(lLineIndex)
                    End If
                    
                    ' Calculate Y position for this line
                    Dim lLineTop As Integer = (lLineIndex - lFirstLine - 1) * pLineHeight + pTopPadding + lTopOffset + 3
                    Dim lY As Integer = lLineTop + lAscent
                    
                    ' Calculate selection bounds for this line
                    If pSelectionActive Then
                        ' Calculate selection range for this line
                        lSelStartLine = Math.Min(pSelectionStartLine, pSelectionEndLine)
                        lSelEndLine = Math.Max(pSelectionStartLine, pSelectionEndLine)
                        
                        If lLineIndex >= lSelStartLine AndAlso lLineIndex <= lSelEndLine Then
                            If lLineIndex = lSelStartLine AndAlso lLineIndex = lSelEndLine Then
                                ' Single line selection
                                lSelStartCol = Math.Min(pSelectionStartColumn, pSelectionEndColumn)
                                lSelEndCol = Math.Max(pSelectionStartColumn, pSelectionEndColumn)
                            ElseIf lLineIndex = lSelStartLine Then
                                ' First line of multi-line selection
                                lSelStartCol = If(pSelectionStartLine < pSelectionEndLine, pSelectionStartColumn, pSelectionEndColumn)
                                lSelEndCol = lLine.Length
                            ElseIf lLineIndex = lSelEndLine Then
                                ' Last line of multi-line selection
                                lSelStartCol = 0
                                lSelEndCol = If(pSelectionStartLine < pSelectionEndLine, pSelectionEndColumn, pSelectionStartColumn)
                            Else
                                ' Middle line - entire line is selected
                                lSelStartCol = 0
                                lSelEndCol = lLine.Length
                            End If
                        End If
                    End If
                    
                    ' Draw each visible character
                    for lColIndex As Integer = lFirstColumn To Math.Min(lLastColumn, lLine.Length - 1)
                        If lColIndex >= lLine.Length Then Exit for
                        
                        Dim lChar As String = lLine(lColIndex).ToString()
                        
                        ' FIXED: Calculate X position based on visible column position
                        ' The character at lFirstColumn should be drawn at pLeftPadding
                        ' Each subsequent character is offset by pCharWidth from there
                        Dim lVisibleColumnIndex As Integer = lColIndex - lFirstColumn
                        Dim lX As Integer = pLeftPadding + (lVisibleColumnIndex * pCharWidth)
                        
                        ' Determine if this character is in selection
                        Dim lInSelection As Boolean = False
                        If pSelectionActive Then
                            If lLineIndex > lSelStartLine AndAlso lLineIndex < lSelEndLine Then
                                lInSelection = True
                            ElseIf lLineIndex = lSelStartLine AndAlso lLineIndex = lSelEndLine Then
                                lInSelection = (lColIndex >= lSelStartCol AndAlso lColIndex < lSelEndCol)
                            ElseIf lLineIndex = lSelStartLine Then
                                lInSelection = (lColIndex >= lSelStartCol)
                            ElseIf lLineIndex = lSelEndLine Then
                                lInSelection = (lColIndex < lSelEndCol)
                            End If
                        End If

                        ' Draw selection background if needed
                        If lInSelection Then
                            Dim lSelColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eSelectionColor)
                            Dim lSelPattern As New Cairo.SolidPattern(lSelColor.R, lSelColor.G, lSelColor.B)
                            vContext.SetSource(lSelPattern)
                            vContext.Rectangle(lX, lLineTop + lAscent + 1, pCharWidth, pLineHeight)
                            vContext.Fill()
                            lSelPattern.Dispose()
                        End If
                        
                        ' Get text color from CharacterColors array or use default
                        Dim lTextColor As Cairo.Color = lDefaultFgColor
                        If lCharColors IsNot Nothing AndAlso 
                           lColIndex < lCharColors.Length AndAlso
                           lCharColors(lColIndex) IsNot Nothing Then
                            ' Use pre-computed color from array
                            lTextColor = lCharColors(lColIndex).CairoColor
                        End If
                        
                        ' Set the text color and draw the character
                        Dim lTextPattern As New Cairo.SolidPattern(lTextColor.R, lTextColor.G, lTextColor.B)
                        vContext.SetSource(lTextPattern)
                        
                        ' Draw the character
                        vContext.MoveTo(lX, lY)
                        lLayout.SetText(lChar)
                        Pango.CairoHelper.ShowLayout(vContext, lLayout)
                        lTextPattern.Dispose()
                    Next
                Next
                
                ' Draw cursor if visible
                If pCursorBlink AndAlso pCursorLine >= lFirstLine AndAlso pCursorLine <= lLastLine Then
                    If pCursorColumn >= lFirstColumn AndAlso pCursorColumn <= lLastColumn Then
                        Dim lCursorX As Integer = pLeftPadding + ((pCursorColumn - lFirstColumn) * pCharWidth)
                        Dim lCursorY As Integer = (pCursorLine - lFirstLine) * pLineHeight + pTopPadding + lTopOffset + 3
                        
                        ' Draw cursor line
                        Dim lCursorColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eCursorColor)
                        Dim lCursorPattern As New Cairo.SolidPattern(lCursorColor.R, lCursorColor.G, lCursorColor.B)
                        vContext.SetSource(lCursorPattern)
                        vContext.LineWidth = 2.0
                        vContext.MoveTo(lCursorX, lCursorY + lTopOffset)
                        vContext.LineTo(lCursorX, lCursorY + pLineHeight + lTopOffset)
                        vContext.Stroke()
                        vContext.MoveTo(lCursorX - lBarLengthHalf, lCursorY + lTopOffset)
                        vContext.LineTo(lCursorX + lBarLengthHalf, lCursorY + lTopOffset)
                        vContext.Stroke()
                        vContext.MoveTo(lCursorX - lBarLengthHalf, lCursorY + pLineHeight + lTopOffset)
                        vContext.LineTo(lCursorX + lBarLengthHalf, lCursorY + pLineHeight + lTopOffset)
                        vContext.Stroke()
                        lCursorPattern.Dispose()
                        
                        ' Draw cursor position indicators if enabled
                        If False Then
                            ' Top indicator
                            vContext.MoveTo(lCursorX - lBarLengthHalf, 0)
                            vContext.LineTo(lCursorX + lBarLengthHalf, 0)
                            vContext.Stroke()
                            
                            ' Left indicator
                            vContext.MoveTo(0, lCursorY + pLineHeight / 2)
                            vContext.LineTo(pLeftPadding - 2, lCursorY + pLineHeight / 2)
                            vContext.Stroke()
                        End If
                    End If
                End If
                
                ' Clean up
                lLayout?.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"DrawContent error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles content changes in the SourceFileInfo
        ''' </summary>
        ''' <remarks>
        ''' Triggers a redraw when the underlying data changes
        ''' </remarks>
        Private Sub OnSourceFileContentChanged(sender As Object, e As EventArgs)
            Try
                ' The SourceFileInfo has changed, redraw to show new content
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"OnSourceFileContentChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Corner Box Drawing =====
        

        ''' <summary>
        ''' Helper function to convert hex color string to Cairo.Color
        ''' </summary>
        ''' <param name="vHex">Hex color string (e.g., "#FF0000")</param>
        ''' <returns>Cairo.Color structure</returns>
        Private Function HexToCairoColor(vHex As String) As Cairo.Color
            Try
                ' Remove the '#' prefix if present
                Dim lHex As String = vHex.TrimStart("#"c)
                
                ' Parse hex components
                Dim lR As Byte = Convert.ToByte(lHex.Substring(0, 2), 16)
                Dim lG As Byte = Convert.ToByte(lHex.Substring(2, 2), 16)
                Dim lB As Byte = Convert.ToByte(lHex.Substring(4, 2), 16)
                
                ' Convert to Cairo's [0.0, 1.0] range
                Return New Cairo.Color(lR / 255.0, lG / 255.0, lB / 255.0)
                
            Catch ex As Exception
                Console.WriteLine($"HexToCairoColor error: {ex.Message}")
                ' Return default color on error
                Return New Cairo.Color(0.5, 0.5, 0.5)
            End Try
        End Function
        
        ' ===== Helper method to invalidate cursor area =====
        
        Private Sub InvalidateCursor()
            Try
                ' Check if cursor is visible
                If pCursorLine < pFirstVisibleLine OrElse pCursorLine >= pFirstVisibleLine + pTotalVisibleLines Then Return
                If pCursorColumn < pFirstVisibleColumn - 1 OrElse pCursorColumn > pFirstVisibleColumn + pTotalVisibleColumns + 1 Then Return
                
                ' Calculate cursor area relative to viewport
                Dim lX As Integer = pLeftPadding + ((pCursorColumn - pFirstVisibleColumn) * pCharWidth) - 2
                Dim lY As Integer = (pCursorLine - pFirstVisibleLine) * pLineHeight + pTopPadding
                
                ' Queue draw for cursor area only
                pDrawingArea?.QueueDrawArea(lX - 4, lY - 4, 12, pLineHeight + 8)
                
            Catch ex As Exception
                Console.WriteLine($"InvalidateCursor error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks if a character is within the current selection
        ''' </summary>
        Private Function IsCharacterInSelection(vLine As Integer, vColumn As Integer) As Boolean
            Try
                If Not pSelectionActive Then Return False
                
                ' Normalize selection bounds
                Dim lStartLine As Integer = Math.Min(pSelectionStartLine, pSelectionEndLine)
                Dim lEndLine As Integer = Math.Max(pSelectionStartLine, pSelectionEndLine)
                Dim lStartCol As Integer = pSelectionStartColumn
                Dim lEndCol As Integer = pSelectionEndColumn
                
                ' Swap columns if needed
                If lStartLine = lEndLine AndAlso lStartCol > lEndCol Then
                    Dim lTemp As Integer = lStartCol
                    lStartCol = lEndCol
                    lEndCol = lTemp
                ElseIf pSelectionStartLine > pSelectionEndLine Then
                    ' Selection is backwards
                    lStartCol = pSelectionEndColumn
                    lEndCol = pSelectionStartColumn
                End If
                
                ' Check if character is in selection
                If vLine < lStartLine OrElse vLine > lEndLine Then
                    Return False
                ElseIf vLine = lStartLine AndAlso vLine = lEndLine Then
                    ' Single line selection
                    Return vColumn >= lStartCol AndAlso vColumn < lEndCol
                ElseIf vLine = lStartLine Then
                    ' First line of multi-line selection
                    Return vColumn >= lStartCol
                ElseIf vLine = lEndLine Then
                    ' Last line of multi-line selection
                    Return vColumn < lEndCol
                Else
                    ' Middle line of multi-line selection
                    Return True
                End If
                
            Catch ex As Exception
                Return False
            End Try
        End Function


        ''' <summary>
        ''' Helper to set Cairo source color from hex string
        ''' </summary>
        Private Sub SetSourceFromHex(vContext As Cairo.Context, vHexColor As String)
            Try
                Dim lColor As Cairo.Color = HexToCairoColor(vHexColor)
                vContext.SetSourceRGB(lColor.R, lColor.G, lColor.B)
            Catch ex As Exception
                ' Default to gray on error
                vContext.SetSourceRGB(0.83, 0.83, 0.83)
            End Try
        End Sub

        ''' <summary>
        ''' Ensures CharacterColors array is initialized with default colors if needed
        ''' </summary>
        ''' <param name="vLineIndex">The line index to check</param>
        ''' <remarks>
        ''' This is a safety check to prevent null reference exceptions during drawing.
        ''' It should NOT overwrite existing colors that were set by parsing.
        ''' </remarks>
        Private Sub InitializeCharacterColorsIfNeeded(vLineIndex As Integer)
            Try
                If pSourceFileInfo Is Nothing Then Return
                
                ' Check if this specific line needs initialization
                If vLineIndex >= 0 AndAlso vLineIndex < pSourceFileInfo.TextLines.Count Then
                    Dim lLineLength As Integer = pSourceFileInfo.TextLines(vLineIndex).Length
                    
                    ' Check if the line's character array needs to be created or resized
                    ' CRITICAL: Only initialize if the array doesn't exist or is the wrong size
                    ' Do NOT reinitialize if colors already exist and are the right size
                    If vLineIndex >= pSourceFileInfo.CharacterColors.Length Then
                        ' The main array needs to be extended
                        ReDim Preserve pSourceFileInfo.CharacterColors(pSourceFileInfo.TextLines.Count - 1)
                        Console.WriteLine($"InitializeCharacterColorsIfNeeded: Extended main array to {pSourceFileInfo.CharacterColors.Length} lines")
                    End If
                    
                    ' Now check the specific line
                    If pSourceFileInfo.CharacterColors(vLineIndex) Is Nothing Then
                        ' Line's character array doesn't exist - create it
                        If lLineLength > 0 Then
                            ReDim pSourceFileInfo.CharacterColors(vLineIndex)(lLineLength - 1)
                            
                            ' Get default color from theme
                            Dim lDefaultColor As String = "#D4D4D4"
                            If pThemeManager IsNot Nothing Then
                                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                                If lTheme IsNot Nothing Then
                                    lDefaultColor = lTheme.ForegroundColor
                                End If
                            End If
                            
                            ' Initialize each character with default color
                            for i As Integer = 0 To lLineLength - 1
                                pSourceFileInfo.CharacterColors(vLineIndex)(i) = New CharacterColorInfo(lDefaultColor)
                            Next
                            
                            Console.WriteLine($"InitializeCharacterColorsIfNeeded: Created new color array for line {vLineIndex} with {lLineLength} characters")
                        Else
                            ' Empty line
                            pSourceFileInfo.CharacterColors(vLineIndex) = New CharacterColorInfo() {}
                        End If
                        
                    ElseIf pSourceFileInfo.CharacterColors(vLineIndex).Length <> lLineLength Then
                        ' Line's character array exists but is the wrong size - resize it
                        ' This can happen when text is edited
                        Dim lOldColors() As CharacterColorInfo = pSourceFileInfo.CharacterColors(vLineIndex)
                        
                        If lLineLength > 0 Then
                            ReDim pSourceFileInfo.CharacterColors(vLineIndex)(lLineLength - 1)
                            
                            ' Get default color from theme
                            Dim lDefaultColor As String = "#D4D4D4"
                            If pThemeManager IsNot Nothing Then
                                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                                If lTheme IsNot Nothing Then
                                    lDefaultColor = lTheme.ForegroundColor
                                End If
                            End If
                            
                            ' Preserve existing colors where possible
                            for i As Integer = 0 To lLineLength - 1
                                If i < lOldColors.Length AndAlso lOldColors(i) IsNot Nothing Then
                                    ' Preserve existing color
                                    pSourceFileInfo.CharacterColors(vLineIndex)(i) = lOldColors(i)
                                Else
                                    ' Use default for new characters
                                    pSourceFileInfo.CharacterColors(vLineIndex)(i) = New CharacterColorInfo(lDefaultColor)
                                End If
                            Next
                            
                            Console.WriteLine($"InitializeCharacterColorsIfNeeded: Resized color array for line {vLineIndex} from {lOldColors.Length} to {lLineLength} characters")
                        Else
                            ' Empty line
                            pSourceFileInfo.CharacterColors(vLineIndex) = New CharacterColorInfo() {}
                        End If
                    End If
                    ' If the array exists and is the right size, don't touch it!
                    ' The colors were set by parsing and should be preserved
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InitializeCharacterColorsIfNeeded error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
