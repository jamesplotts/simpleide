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
                Static bolAlreadyRun as Boolean
                If Not bolAlreadyRun then
                    Dim E as New ProjectManagerRequestEventArgs
                    RaiseEvent ProjectManagerRequested(Me, E)
                    If E.ProjectManager IsNot Nothing Then 
                        pSourceFileInfo.ProjectManager = E.ProjectManager
                        bolAlreadyRun = True
                    End If
                End If
                DrawContent(vArgs.Cr)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawn error: {ex.Message}")
                Return True
            End Try
        End Function
        
        
        ' ===== Unified Drawing Method =====
        


        ''' <summary>
        ''' Main content drawing method simplified to use CharacterTokens array
        ''' </summary>
        ''' <param name="vContext">The Cairo drawing context</param>
        ''' <remarks>
        ''' This version reads from the CharacterTokens array in SourceFileInfo
        ''' and draws each character with its pre-computed color. No parsing happens here.
        ''' FIXED: Space characters now properly show selection background.
        ''' </remarks>
        Private Sub DrawContent(vContext As Cairo.Context)
            Try
                Dim lTopOffset As Integer = -3
                Dim lBarLengthHalf As Integer = 4
                
                ' Create a layout for text rendering
                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription
                
                ' Ensure color cache is initialized
                If pThemeColorCache Is Nothing OrElse pThemeColorCache.Length = 0 Then
                    InitializeThemeColorCache()
                End If
                
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
                
                ' Get default foreground color from cache
                Dim lDefaultFgColor As Cairo.Color = GetCachedTokenColor(SyntaxTokenType.eNormal)
                
                ' Draw text and selections character by character
                For lLineIndex As Integer = lFirstLine To lLastLine
                    If lLineIndex >= pLineCount Then Exit For
                    
                    ' Get line text from SourceFileInfo
                    Dim lLine As String = ""
                    If pSourceFileInfo IsNot Nothing AndAlso lLineIndex < pSourceFileInfo.TextLines.Count Then
                        lLine = pSourceFileInfo.TextLines(lLineIndex)
                    End If
                    
                    ' Get character tokens for this line (using byte array instead of CharacterColorInfo)
                    Dim lCharTokens() As Byte = Nothing
                    If pSourceFileInfo IsNot Nothing AndAlso 
                       pSourceFileInfo.CharacterTokens IsNot Nothing AndAlso 
                       lLineIndex < pSourceFileInfo.CharacterTokens.Length Then
                        lCharTokens = pSourceFileInfo.CharacterTokens(lLineIndex)
                    End If
                    
                    ' Calculate Y position for this line
                    Dim lLineTop As Integer = (lLineIndex - lFirstLine - 1) * pLineHeight + pTopPadding + lTopOffset + 3
                    Dim lY As Integer = lLineTop + lAscent
                    
                    ' Draw each visible character in the line
                    For lColIndex As Integer = lFirstColumn To Math.Min(lLine.Length - 1, lLastColumn)
                        If lColIndex >= lLine.Length Then Exit For
                        
                        Dim lChar As String = lLine(lColIndex).ToString()
                        
                        ' Calculate X position for this character
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
                        
                        ' Draw selection background if needed (including for spaces!)
                        If lInSelection Then
                            Dim lSelColor As Cairo.Color = lCurrentTheme.CairoColor(EditorTheme.Tags.eSelectionColor)
                            Dim lSelPattern As New Cairo.SolidPattern(lSelColor.R, lSelColor.G, lSelColor.B)
                            vContext.SetSource(lSelPattern)
                            vContext.Rectangle(lX, lLineTop + lAscent + 1, pCharWidth, pLineHeight)
                            vContext.Fill()
                            lSelPattern.Dispose()
                        End If
                        
                        ' FIXED: Don't skip spaces - we still need to draw them (even if invisible)
                        ' to maintain proper selection background
                        If lChar <> " " Then
                            ' Get text color from token byte array using cached colors
                            Dim lTextColor As Cairo.Color = lDefaultFgColor
                            Dim lUseBold As Boolean = False
                            Dim lUseItalic As Boolean = False
                            
                            If lCharTokens IsNot Nothing AndAlso 
                               lColIndex < lCharTokens.Length Then
                                ' Decode the token byte
                                Dim lTokenByte As Byte = lCharTokens(lColIndex)
                                Dim lTokenType As SyntaxTokenType = CharacterToken.GetTokenType(lTokenByte)
                                
                                ' Get cached color for this token type (FAST array lookup!)
                                lTextColor = GetCachedTokenColor(lTokenType)
                                
                                ' Check for style flags
                                lUseBold = CharacterToken.IsBold(lTokenByte)
                                lUseItalic = CharacterToken.IsItalic(lTokenByte)
                            End If
                            
                            ' Set font style if needed
                            If lUseBold OrElse lUseItalic Then
                                lLayout.FontDescription = GetCachedFont(lUseBold, lUseItalic)
                            ElseIf lLayout.FontDescription IsNot pFontDescription Then
                                lLayout.FontDescription = pFontDescription
                            End If
                            
                            ' Set the text color and draw the character
                            Dim lTextPattern As New Cairo.SolidPattern(lTextColor.R, lTextColor.G, lTextColor.B)
                            vContext.SetSource(lTextPattern)
                            
                            ' Draw the character
                            vContext.MoveTo(lX, lY)
                            lLayout.SetText(lChar)
                            Pango.CairoHelper.ShowLayout(vContext, lLayout)
                            lTextPattern.Dispose()
                        End If
                    Next
                Next
                Dim lCursorColor As Cairo.Color
                
                ' Draw cursor if visible
                If pCursorVisible AndAlso pCursorLine >= lFirstLine AndAlso pCursorLine <= lLastLine Then
                    ' Draw cursor line
                    lCursorColor = lCurrentTheme.CairoColor(EditorTheme.Tags.eCursorColor)
                Else
                    ' Draw cursor line
                    lCursorColor = lBgColor
                End if
                Dim lCursorPattern As New Cairo.SolidPattern(lCursorColor.R, lCursorColor.G, lCursorColor.B)

                If pCursorColumn >= lFirstColumn AndAlso pCursorColumn <= lLastColumn Then
                    Dim lCursorX As Integer = pLeftPadding + ((pCursorColumn - lFirstColumn) * pCharWidth)
                    Dim lCursorY As Integer = (pCursorLine - lFirstLine) * pLineHeight + pTopPadding + lTopOffset + 2
                    
                    
                    ' **********************************************
                    ' CRITICAL: LEAVE MY GODDAMN I BEAM CURSOR ALONE                        
                    ' **********************************************
                    vContext.SetSource(lCursorPattern)
                    vContext.LineWidth = 2.0
                    vContext.MoveTo(lCursorX, lCursorY + lTopOffset)
                    vContext.LineTo(lCursorX, lCursorY + pLineHeight + lTopOffset)
                    vContext.Stroke()
                    vContext.MoveTo(lCursorX - lBarLengthHalf, lCursorY + lTopOffset)
                    vContext.LineTo(lCursorX + lBarLengthHalf, lCursorY + lTopOffset)
'                    vContext.Stroke()
                    vContext.MoveTo(lCursorX - lBarLengthHalf, lCursorY + pLineHeight + lTopOffset)
                    vContext.LineTo(lCursorX + lBarLengthHalf, lCursorY + pLineHeight + lTopOffset)
                    vContext.Stroke()
'                    Pango.CairoHelper.ShowLayout(vContext, lLayout)
                    lCursorPattern.Dispose()
                End If
            
				            ' Clean up
                lLayout?.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"DrawContent error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles ContentChanged events from SourceFileInfo
        ''' </summary>
        ''' <param name="vSender">The SourceFileInfo that raised the event</param>
        ''' <param name="vArgs">Event arguments</param>
        ''' <remarks>
        ''' This is called after ForceImmediateParsing completes and updates CharacterTokens.
        ''' It triggers a redraw to show the newly colored syntax.
        ''' </remarks>
        Private Sub OnSourceFileContentChanged(vSender As Object, vArgs As EventArgs)
            Try
                ' Simply queue a redraw when content/rendering changes
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.QueueDraw()
                    Console.WriteLine("OnSourceFileContentChanged: Redraw queued after content change")
                End If
                
                ' Also update line numbers if visible
                If pLineNumberWidget IsNot Nothing AndAlso pLineNumberWidget.Visible Then
                    pLineNumberWidget.QueueDraw()
                End If
                
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

        
    End Class
    
End Namespace
