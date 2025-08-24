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
        ''' Main content drawing method with drag-drop indicator support
        ''' </summary>
        Private Sub DrawContent(vContext As Cairo.Context)
            Try
                Dim lTopOffset As Integer = -3
                Dim lBarLengthHalf As Integer = 4
        
                ' Create a layout for text rendering
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription
                
                ' CRITICAL CHANGE: Use GetActiveTheme instead of pThemeManager.GetCurrentThemeObject()
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
                
                ' Draw current line highlight if enabled
                If pHighlightCurrentLine AndAlso pCursorLine >= lFirstLine AndAlso pCursorLine <= lLastLine Then
                    ' Calculate Y position with the same offset used for text drawing
                    ' Apply the lTopOffset to align properly with the text
                    Dim lLineY As Integer = (pCursorLine - lFirstLine) * pLineHeight + pTopPadding + lTopOffset
                    Dim lCurrentLineColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eCurrentLineColor)
                    Dim lCurrentLinePattern As New Cairo.SolidPattern(lCurrentLineColor.R, lCurrentLineColor.G, lCurrentLineColor.B)
                    vContext.SetSource(lCurrentLinePattern)
                    ' Draw the highlight rectangle for the full line height
                    vContext.Rectangle(0, lLineY, pDrawingArea.AllocatedWidth, pLineHeight)
                    vContext.Fill()
                    lCurrentLinePattern.Dispose()
                End If
                
                ' Draw text and selections character by character
                For lLineIndex As Integer = lFirstLine To lLastLine
                    If lLineIndex >= pTextLines.Count Then Exit For
                    
                    Dim lLine As String = pTextLines(lLineIndex)
                    ' Calculate Y position for this line
                    ' The line rectangle starts here
                    Dim lLineTop As Integer = (lLineIndex - lFirstLine - 1) * pLineHeight + pTopPadding + lTopOffset + 3
                    ' The text baseline is positioned within the line rectangle
                    Dim lY As Integer = lLineTop + lAscent
                    
                    ' Draw each character
                    For lColIndex As Integer = 0 To lLine.Length - 1
                        If lColIndex < lFirstColumn Then Continue For
                        If lColIndex > lLastColumn Then Exit For
                        
                        Dim lChar As Char = lLine(lColIndex)
                        Dim lX As Integer = pLeftPadding + ((lColIndex - lFirstColumn) * pCharWidth)
                        
                        ' Check if this character is in selection
                        Dim lInSelection As Boolean = False
                        If pSelectionActive AndAlso pHasSelection Then
                            ' Normalize selection bounds
                            Dim lSelStartLine As Integer = pSelectionStartLine
                            Dim lSelStartCol As Integer = pSelectionStartColumn
                            Dim lSelEndLine As Integer = pSelectionEndLine
                            Dim lSelEndCol As Integer = pSelectionEndColumn
                            
                            ' Ensure start is before end
                            If lSelStartLine > lSelEndLine OrElse (lSelStartLine = lSelEndLine AndAlso lSelStartCol > lSelEndCol) Then
                                Dim lTempLine As Integer = lSelStartLine
                                Dim lTempCol As Integer = lSelStartCol
                                lSelStartLine = lSelEndLine
                                lSelStartCol = lSelEndCol
                                lSelEndLine = lTempLine
                                lSelEndCol = lTempCol
                            End If
                            
                            ' Check if character is in selection
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
                            ' Use lLineTop for the selection rectangle to align with the line
                            vContext.Rectangle(lX, lLineTop + lAscent + 1, pCharWidth, pLineHeight)
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
                        Dim lTextPattern As New Cairo.SolidPattern(lTextColor.R, lTextColor.G, lTextColor.B)
                        vContext.SetSource(lTextPattern)
                        
                        ' Draw the character
                        vContext.MoveTo(lX, lY)
                        lLayout.SetText(lChar.ToString())
                        Pango.CairoHelper.ShowLayout(vContext, lLayout)
                        
                        ' Dispose pattern after drawing
                        lTextPattern.Dispose()
                    Next
                Next
        
                ' Draw cursor if visible
                If pCursorVisible AndAlso
                  pCursorLine >= lFirstLine AndAlso pCursorLine <= lLastLine AndAlso
                  pCursorColumn >= lFirstColumn - 1 AndAlso pCursorColumn <= lLastColumn + 1 Then
                    Dim lCursorColor As Cairo.Color
                    If pCursorBlink Then
                         lCursorColor = lCurrentTheme.CairoColor(EditorTheme.Tags.eCursorColor)
                    Else
                         lCursorColor = lCurrentTheme.CairoColor(EditorTheme.Tags.eBackgroundColor)
                    End If
                    Dim lCursorPattern As New Cairo.SolidPattern(lCursorColor.R, lCursorColor.G, lCursorColor.B)
                    vContext.SetSource(lCursorPattern)
        
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
                    lCursorPattern.Dispose()
                End If
                
                ' Clean up
                lLayout.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"DrawContent error: {ex.Message}")
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
