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
        
        Private Function OnLineNumberAreaDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                DrawLineNumbers(vArgs.Cr)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnLineNumberAreaDraw error: {ex.Message}")
                Return True
            End Try
        End Function
        
        ' ===== Unified Drawing Method =====
        
        ''' <summary>
        ''' Main content drawing method with drag-drop indicator support
        ''' </summary>
        ''' <summary>
        ''' Main content drawing method with drag-drop indicator support
        ''' </summary>
        Private Sub DrawContent(vContext As Cairo.Context)
            Try
                Dim lTopOffset As Integer = -3
                Dim lBarLengthHalf As Integer = 4

                ' Create a layout for text rendering
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription
                Dim lCurrentTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()

                ' Get font metrics
                Dim lAscent As Integer = 0
                If pFontMetrics IsNot Nothing Then
                    lAscent = pFontMetrics.Ascent
                Else
                    lAscent = CInt(pLineHeight * 0.75)
                End If
                
                ' Draw background
                Dim Color As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eBackgroundColor)
                Dim Pattern As New Cairo.SolidPattern(Color.r, Color.g, Color.b)
                vContext.SetSource(Pattern)
                vContext.Rectangle(0, 0, pDrawingArea.AllocatedWidth, pDrawingArea.AllocatedHeight)
                vContext.Fill()
                Pattern.Dispose()
                
                ' Calculate visible range
                Dim lFirstLine As Integer = pFirstVisibleLine
                Dim lLastLine As Integer = Math.Min(pFirstVisibleLine + pTotalVisibleLines - 1, pLineCount - 1)
                Dim lFirstColumn As Integer = pFirstVisibleColumn
                Dim lLastColumn As Integer = pFirstVisibleColumn + pTotalVisibleColumns - 1
                
                ' Normalize selection bounds once
                Dim lSelStartLine As Integer = pSelectionStartLine
                Dim lSelStartCol As Integer = pSelectionStartColumn
                Dim lSelEndLine As Integer = pSelectionEndLine
                Dim lSelEndCol As Integer = pSelectionEndColumn
                If pSelectionActive Then
                    NormalizeSelection(lSelStartLine, lSelStartCol, lSelEndLine, lSelEndCol)
                End If

                ' Main drawing loop - iterate through visible lines
                For lLineIndex As Integer = lFirstLine To lLastLine
                    If lLineIndex >= pLineCount Then Exit For
                    
                    ' Get line text from SourceFileInfo if available, otherwise from pTextLines
                    Dim lLineText As String = ""
                    If pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.TextLines IsNot Nothing Then
                        If lLineIndex < pSourceFileInfo.TextLines.Count Then
                            lLineText = pSourceFileInfo.TextLines(lLineIndex)
                        End If
                    ElseIf pTextLines IsNot Nothing AndAlso lLineIndex < pTextLines.Count Then
                        lLineText = pTextLines(lLineIndex)
                    End If
                    
                    Dim lY As Integer = (lLineIndex - lFirstLine) * pLineHeight + pTopPadding + lAscent - pLineHeight
                    
                    ' Draw each visible character in the line
                    ' Start from first visible column (with buffer), not from 0
                    Dim lStartCol As Integer = Math.Max(0, lFirstColumn - 5)
                    Dim lEndCol As Integer = Math.Min(lLineText.Length - 1, lLastColumn + 5)
                    
                    For lColIndex As Integer = lStartCol To lEndCol
                        ' Calculate actual X position considering the character's position in the line
                        Dim lX As Integer = pLeftPadding + ((lColIndex - lFirstColumn) * pCharWidth)
                        
                        ' Skip if before or after viewport
                        If lX < pLeftPadding - pCharWidth OrElse lX > pViewportWidth Then Continue For
                        
                        ' Skip drawing character if it's beyond the line length
                        If lColIndex >= lLineText.Length Then Continue For
                        
                        ' Get the character
                        Dim lChar As Char = lLineText(lColIndex)
                        
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
                            Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eSelectionColor)
                            Dim lSelPattern As New Cairo.SolidPattern(Color.r, Color.g, Color.b)
                            vContext.SetSource(lSelPattern)
                            vContext.Rectangle(lX, lY - lAscent + pLineHeight + lTopOffset, pCharWidth, pLineHeight)
                            vContext.Fill()
                            lSelPattern.Dispose()
                        End If
                        
                        ' Determine text color
                        Dim lTextColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eForegroundColor)
                        
                        ' Check for syntax highlighting color
                        If pCharacterColors IsNot Nothing AndAlso 
                           lLineIndex < pCharacterColors.Length AndAlso
                           pCharacterColors(lLineIndex) IsNot Nothing AndAlso
                           lColIndex < pCharacterColors(lLineIndex).Length AndAlso
                           pCharacterColors(lLineIndex)(lColIndex) IsNot Nothing Then
                            lTextColor = pCharacterColors(lLineIndex)(lColIndex).CairoColor
                        End If
                        
                        ' Set the text color and draw the character
                        Dim lTextPattern As New Cairo.SolidPattern(lTextColor.r, lTextColor.g, lTextColor.b)
                        vContext.SetSource(lTextPattern)
                        
                        ' Draw the character
                        vContext.MoveTo(lX, lY)
                        lLayout.SetText(lChar.ToString())
                        Pango.CairoHelper.ShowLayout(vContext, lLayout)
                        
                        ' Dispose pattern after drawing
                        lTextPattern.Dispose()
                    Next
                Next

                ' CRITICAL FIX: Draw drag-drop indicators if active
                DrawDragDropIndicators(vContext)
                
                ' Draw cursor if visible
                If pCursorVisible AndAlso
                  pCursorLine >= lFirstLine AndAlso pCursorLine <= lLastLine AndAlso
                  pCursorColumn >= lFirstColumn - 1 AndAlso pCursorColumn <= lLastColumn + 1 Then
                    Dim lTextColor As Cairo.Color
                    If pCursorBlink Then
                         lTextColor = lCurrentTheme.CairoColor(EditorTheme.Tags.eCursorColor)
                    Else
                         lTextColor = lCurrentTheme.CairoColor(EditorTheme.Tags.eBackgroundColor)
                    End If
                    Pattern = New Cairo.SolidPattern(lTextColor.r, lTextColor.g, lTextColor.b)
                    vContext.SetSource(Pattern)

                    Dim lCursorX As Integer = pLeftPadding + ((pCursorColumn - lFirstColumn) * pCharWidth)
                    Dim lCursorY As Integer = (pCursorLine - lFirstLine) * pLineHeight + pTopPadding
                    
                    vContext.LineWidth = 2
                    vContext.MoveTo(lCursorX, lCursorY + lTopOffset)
                    vContext.LineTo(lCursorX, lCursorY + pLineHeight + lTopOffset)
                    vContext.MoveTo(lCursorX - lBarLengthHalf, lCursorY + lTopOffset)
                    vContext.LineTo(lCursorX + lBarLengthHalf, lCursorY + lTopOffset)
                    vContext.MoveTo(lCursorX - lBarLengthHalf, lCursorY + pLineHeight + lTopOffset)
                    vContext.LineTo(lCursorX + lBarLengthHalf, lCursorY + pLineHeight + lTopOffset)
                    vContext.Stroke()
                    Pattern.Dispose()
                End If
                
                ' Clean up
                lLayout.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"DrawContent error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Line Number Drawing =====
        
        Private Sub DrawLineNumbers(vContext As Cairo.Context)
            Try
                ' Set background color
                Dim lCurrentTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()

                Dim lTextColor As Cairo.Color  = lCurrentTheme.CairoColor(EditorTheme.Tags.eLineNumberBackgroundColor)
                Dim Pattern As Cairo.SolidPattern = New Cairo.SolidPattern(lTextColor.r, lTextColor.g, lTextColor.b)
                vContext.SetSource(Pattern)

                vContext.Rectangle(0 , 0, pLineNumberWidth, pLineNumberArea.AllocatedHeight)
                vContext.Fill()
                Pattern.Dispose()
                ' Set text color
                lTextColor = lCurrentTheme.CairoColor(EditorTheme.Tags.eForegroundColor)
                Dim lLineNumberColorPattern As Cairo.SolidPattern = New Cairo.SolidPattern(lTextColor.r, lTextColor.g, lTextColor.b)
                vContext.SetSource(lLineNumberColorPattern)
                
                ' Create layout for line numbers
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription
                lLayout.Alignment = Pango.Alignment.Right
                lLayout.Width = Pango.Units.FromPixels(pLineNumberWidth - 10)
                
                ' Get font ascent for consistent positioning with main text
                Dim lAscent As Integer = 0
                If pFontMetrics IsNot Nothing Then
                    lAscent = pFontMetrics.Ascent
                Else
                    lAscent = CInt(pLineHeight * 0.75)
                End If
                
                ' Draw line numbers for visible lines
                Dim lFirstLine As Integer = pFirstVisibleLine
                Dim lLastLine As Integer = Math.Min(pFirstVisibleLine + pTotalVisibleLines - 1, pLineCount - 1)
                lTextColor = lCurrentTheme.CairoColor(EditorTheme.Tags.eCurrentLineNumberColor)
                Dim lCurrentLineNumberColorPattern As Cairo.SolidPattern = New Cairo.SolidPattern(lTextColor.r, lTextColor.g, lTextColor.b)

                
                For lLineIndex As Integer = lFirstLine To lLastLine
                    ' Calculate Y position relative to viewport - matches text drawing
                    Dim lY As Integer = (lLineIndex - pFirstVisibleLine) * pLineHeight + pTopPadding + lAscent - pLineHeight
                    
                    ' Highlight current line number
                    If lLineIndex = pCursorLine Then
                        vContext.SetSource(lCurrentLineNumberColorPattern) ' Brighter for current Line
                    Else
                        vContext.SetSource(lLineNumberColorPattern)
                    End If
                    
                    ' Draw line number
                    vContext.MoveTo(5, lY)
                    lLayout.SetText((lLineIndex + 1).ToString())
                    Pango.CairoHelper.ShowLayout(vContext, lLayout)
                Next
                
                ' Draw right border
                vContext.SetSource(lLineNumberColorPattern)
                vContext.LineWidth = 1
                vContext.MoveTo(pLineNumberWidth - 0.5, 0)
                vContext.LineTo(pLineNumberWidth - 0.5, pLineNumberArea.AllocatedHeight)
                vContext.Stroke()
                lCurrentLineNumberColorPattern.Dispose()
                lLineNumberColorPattern.Dispose()
                ' Dispose layout
                lLayout.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"DrawLineNumbers error: {ex.Message}")
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
        
    End Class
    
End Namespace
