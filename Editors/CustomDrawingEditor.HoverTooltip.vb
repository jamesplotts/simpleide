' CustomDrawingEditor.HoverTooltip.vb - Hover tooltip that shows the declaration line for
' a local variable or const when hovering over a reference to it elsewhere in the same
' method/property, drawn as a Cairo overlay on the editor surface (same approach as the
' CodeSense popup) so it composes cleanly with folding/scrolling/zoom.
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' Matches the Object Explorer's hover-tooltip delay for consistency
        Private Const HoverDeclarationTooltipDelay As UInteger = 500

        Private pHoverTooltipTimerId As UInteger = 0
        Private pHoverTooltipVisible As Boolean = False
        Private pHoverTooltipText As String = ""
        Private pHoverTooltipLine As Integer = -1
        Private pHoverTooltipColumn As Integer = -1
        Private pHoverLastLine As Integer = -1
        Private pHoverLastColumn As Integer = -1
        Private pHoverLastWord As String = ""

        ''' <summary>
        ''' Hides the hover declaration tooltip when the mouse leaves the editor surface
        ''' </summary>
        Private Function OnEditorLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs) As Boolean
            Try
                ResetHoverDeclarationTracking()
            Catch ex As Exception
                Console.WriteLine($"OnEditorLeaveNotify error: {ex.Message}")
            End Try
            Return False
        End Function

        ''' <summary>
        ''' Tracks mouse position on every motion event to detect when the hovered word
        ''' changes, (re)starting the hover-delay timer as needed
        ''' </summary>
        ''' <param name="vX">Mouse X in drawing-area-local pixels</param>
        ''' <param name="vY">Mouse Y in drawing-area-local pixels</param>
        Private Sub UpdateHoverDeclarationTooltip(vX As Double, vY As Double)
            Try
                Dim lPos As EditorPosition = GetPositionFromCoordinates(vX, vY)
                Dim lWord As String = GetWordAt(lPos.Line, lPos.Column)

                If lPos.Line = pHoverLastLine AndAlso lWord = pHoverLastWord Then
                    Return ' Still hovering the same word/line - leave any timer/tooltip alone
                End If

                pHoverLastLine = lPos.Line
                pHoverLastColumn = lPos.Column
                pHoverLastWord = lWord

                CancelHoverDeclarationTimer()
                HideHoverDeclarationTooltip()

                If Not String.IsNullOrEmpty(lWord) Then
                    Dim lCapturedLine As Integer = lPos.Line
                    Dim lCapturedColumn As Integer = lPos.Column
                    Dim lCapturedWord As String = lWord
                    pHoverTooltipTimerId = GLib.Timeout.Add(HoverDeclarationTooltipDelay,
                        Function() ShowHoverDeclarationTooltip(lCapturedLine, lCapturedColumn, lCapturedWord))
                End If

            Catch ex As Exception
                Console.WriteLine($"UpdateHoverDeclarationTooltip error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Cancels the pending hover-delay timer, if any
        ''' </summary>
        Private Sub CancelHoverDeclarationTimer()
            If pHoverTooltipTimerId <> 0 Then
                Dim lTimerId As UInteger = pHoverTooltipTimerId
                pHoverTooltipTimerId = 0
                Try
                    GLib.Source.Remove(lTimerId)
                Catch
                    ' Timer may have already fired - fine
                End Try
            End If
        End Sub

        ''' <summary>
        ''' Hides the visible hover declaration tooltip, if any. Does NOT touch the
        ''' pHoverLast* hover-tracking fields - those track "what word is the mouse over
        ''' right now" independently of whether a tooltip is currently shown for it, and
        ''' UpdateHoverDeclarationTooltip calls this immediately after setting them for a new
        ''' word, so clearing them here would make ShowHoverDeclarationTooltip's "did the
        ''' mouse move on since this timer was scheduled" check always fail
        ''' </summary>
        Private Sub HideHoverDeclarationTooltip()
            If pHoverTooltipVisible Then
                pHoverTooltipVisible = False
                pDrawingArea?.QueueDraw()
            End If
        End Sub

        ''' <summary>
        ''' Fully resets hover tracking and hides the tooltip - call when the mouse leaves
        ''' the editor surface entirely
        ''' </summary>
        Private Sub ResetHoverDeclarationTracking()
            CancelHoverDeclarationTimer()
            pHoverLastLine = -1
            pHoverLastColumn = -1
            pHoverLastWord = ""
            HideHoverDeclarationTooltip()
        End Sub

        ''' <summary>
        ''' Hover-delay timer callback - resolves declaration text for the captured word (if
        ''' it names a parameter, local variable/const, or class member visible at that
        ''' position, and isn't already on the line declaring it) and shows the overlay
        ''' </summary>
        Private Function ShowHoverDeclarationTooltip(vLine As Integer, vColumn As Integer, vWord As String) As Boolean
            pHoverTooltipTimerId = 0
            Try
                ' Mouse may have moved on since this timer was scheduled
                If vLine <> pHoverLastLine OrElse vWord <> pHoverLastWord Then Return False
                If pRootNode Is Nothing Then Return False

                Dim lDeclText As String = FindIdentifierDeclarationText(vWord, vLine, vColumn)
                If String.IsNullOrEmpty(lDeclText) Then Return False

                pHoverTooltipText = lDeclText
                pHoverTooltipLine = vLine
                pHoverTooltipColumn = vColumn
                pHoverTooltipVisible = True
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"ShowHoverDeclarationTooltip error: {ex.Message}")
            End Try
            Return False ' One-shot timer
        End Function

        ''' <summary>
        ''' Resolves declaration text for vWord as seen from vLine, checking (in VB scoping
        ''' order): parameters of the containing member, local Dim/Const variables in the
        ''' containing member, then fields/consts/properties/methods/events of the containing
        ''' class. Returns Nothing if vWord isn't found, or if it IS found but its own
        ''' declaration starts on vLine (no point showing a tooltip for what's already on
        ''' screen).
        ''' </summary>
        Private Function FindIdentifierDeclarationText(vWord As String, vLine As Integer, vColumn As Integer) As String
            Try
                Dim lMemberNode As SyntaxNode = FindContainingMemberNode(pRootNode, vLine)
                If lMemberNode IsNot Nothing Then

                    ' Parameters
                    If lMemberNode.Parameters IsNot Nothing Then
                        for each lParam As ParameterInfo in lMemberNode.Parameters
                            If String.Equals(lParam.Name, vWord, StringComparison.OrdinalIgnoreCase) Then
                                If vLine = lMemberNode.StartLine Then Return Nothing
                                Return FormatParameterDeclaration(lParam)
                            End If
                        Next
                    End If

                    ' Local Dim/Const variables (flattened onto the member as eVariable children)
                    If lMemberNode.Children IsNot Nothing Then
                        for each lChild As SyntaxNode in lMemberNode.Children
                            If lChild.NodeType = CodeNodeType.eVariable AndAlso
                               String.Equals(lChild.Name, vWord, StringComparison.OrdinalIgnoreCase) Then
                                If lChild.StartLine = vLine Then Return Nothing
                                If lChild.StartLine < 0 OrElse lChild.StartLine >= pLineCount Then Return Nothing
                                Dim lText As String = TextLines(lChild.StartLine).Trim()
                                Return If(String.IsNullOrEmpty(lText), Nothing, lText)
                            End If
                        Next
                    End If
                End If

                ' Fields/consts/properties/methods/functions/events/constructors anywhere in
                ' the project - ProjectManager.GetProjectSyntaxTree() already merges every
                ' file's parsed structure into one in-memory tree (built once, cached), so
                ' this is a cheap recursive walk over already-parsed nodes. This deliberately
                ' does NOT use ProjectManager.FindDefinition: that does a per-file dictionary
                ' scan with a regex-based text-search fallback (recompiling several Regex
                ' objects per line) for every file whose syntax-tree lookup misses, which is
                ' far too expensive to run synchronously from a hover callback on a
                ' many-file project - it visibly froze the UI when tried.
                If pProjectManager IsNot Nothing Then
                    Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                    If lProjectTree IsNot Nothing Then
                        Dim lMember As SyntaxNode = FindProjectMemberNode(lProjectTree, vWord)
                        If lMember IsNot Nothing Then
                            Dim lSameFile As Boolean = pSourceFileInfo IsNot Nothing AndAlso
                                String.Equals(lMember.FilePath, pSourceFileInfo.FilePath, StringComparison.OrdinalIgnoreCase)
                            If lSameFile AndAlso lMember.StartLine = vLine Then Return Nothing

                            Dim lText As String = lMember.GetFullDeclaration()
                            Return If(String.IsNullOrEmpty(lText), Nothing, lText)
                        End If
                    End If
                End If

                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"FindIdentifierDeclarationText error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Recursively searches the whole-project syntax tree for the first field, const,
        ''' property, method, function, event, or constructor named vWord
        ''' </summary>
        Private Function FindProjectMemberNode(vNode As SyntaxNode, vWord As String) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eField, CodeNodeType.eConst, CodeNodeType.eProperty,
                         CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eEvent,
                         CodeNodeType.eConstructor
                        If String.Equals(vNode.Name, vWord, StringComparison.OrdinalIgnoreCase) Then
                            Return vNode
                        End If
                End Select

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindProjectMemberNode(lChild, vWord)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"FindProjectMemberNode error: {ex.Message}")
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' Formats a parameter's declaration the way it appears in the method signature,
        ''' e.g. "Optional vTabIndex As Integer = -1"
        ''' </summary>
        Private Function FormatParameterDeclaration(vParam As ParameterInfo) As String
            Dim lText As New System.Text.StringBuilder()

            If vParam.IsOptional Then lText.Append("Optional ")
            If vParam.IsByRef Then
                lText.Append("ByRef ")
            ElseIf vParam.IsParamArray Then
                lText.Append("ParamArray ")
            End If

            lText.Append(vParam.Name)

            If Not String.IsNullOrEmpty(vParam.ParameterType) Then
                lText.Append(" As ").Append(vParam.ParameterType)
            End If

            If vParam.IsOptional AndAlso Not String.IsNullOrEmpty(vParam.DefaultValue) Then
                lText.Append(" = ").Append(vParam.DefaultValue)
            End If

            Return lText.ToString()
        End Function

        ''' <summary>
        ''' Recursively finds the innermost method/function/constructor/property node whose
        ''' line range contains vLine
        ''' </summary>
        Private Function FindContainingMemberNode(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                If (vNode.NodeType = CodeNodeType.eMethod OrElse vNode.NodeType = CodeNodeType.eFunction OrElse
                    vNode.NodeType = CodeNodeType.eConstructor OrElse vNode.NodeType = CodeNodeType.eProperty) AndAlso
                   vNode.StartLine <= vLine AndAlso vNode.EndLine >= vLine Then
                    Return vNode
                End If

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindContainingMemberNode(lChild, vLine)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"FindContainingMemberNode error: {ex.Message}")
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' Gets the word at an arbitrary line/column, using the same word-boundary rules as
        ''' GetWordAtCursor
        ''' </summary>
        Private Function GetWordAt(vLine As Integer, vColumn As Integer) As String
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return ""

                Dim lLine As String = TextLines(vLine)
                If String.IsNullOrEmpty(lLine) Then Return ""

                Dim lStartCol As Integer = vColumn
                Dim lEndCol As Integer = vColumn

                If lStartCol >= lLine.Length Then
                    lStartCol = lLine.Length - 1
                    If lStartCol < 0 Then Return ""
                End If

                If lStartCol < lLine.Length AndAlso Not IsWordChar(lLine(lStartCol)) Then
                    If lStartCol > 0 AndAlso IsWordChar(lLine(lStartCol - 1)) Then
                        lStartCol -= 1
                        lEndCol = lStartCol
                    Else
                        Return ""
                    End If
                End If

                While lStartCol > 0 AndAlso IsWordChar(lLine(lStartCol - 1))
                    lStartCol -= 1
                End While

                While lEndCol < lLine.Length AndAlso IsWordChar(lLine(lEndCol))
                    lEndCol += 1
                End While

                If lEndCol > lStartCol Then
                    Return lLine.Substring(lStartCol, lEndCol - lStartCol)
                End If

                Return ""

            Catch ex As Exception
                Console.WriteLine($"GetWordAt error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Draws the hover declaration tooltip overlay, if currently visible - call from
        ''' DrawContent after normal content (and after the CodeSense popup) so it paints on
        ''' top
        ''' </summary>
        Private Sub DrawHoverDeclarationTooltip(vContext As Cairo.Context)
            Try
                If Not pHoverTooltipVisible OrElse String.IsNullOrEmpty(pHoverTooltipText) Then Return

                Dim lTheme As EditorTheme = GetActiveTheme()

                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription
                lLayout.SetText(pHoverTooltipText)

                Dim lTextWidth As Integer
                Dim lTextHeight As Integer
                lLayout.GetPixelSize(lTextWidth, lTextHeight)

                Const lPaddingX As Integer = 6
                Const lPaddingY As Integer = 3
                Dim lWidth As Integer = lTextWidth + lPaddingX * 2
                Dim lHeight As Integer = lTextHeight + lPaddingY * 2

                Dim lFirstLine As Integer = Math.Max(0, pFirstVisibleLine)
                Dim lVisualLine As Integer = SourceToVisualLine(pHoverTooltipLine)

                Dim lX As Integer = pLeftPadding + (pHoverTooltipColumn - pFirstVisibleColumn) * pCharWidth
                Dim lY As Integer = (lVisualLine - lFirstLine + 1) * pLineHeight + pTopPadding + CodeSenseRowTopCorrection

                ' Keep the box inside the viewport horizontally
                Dim lViewportWidth As Integer = If(pDrawingArea IsNot Nothing, pDrawingArea.AllocatedWidth, 0)
                If lX + lWidth > lViewportWidth Then
                    lX = Math.Max(0, lViewportWidth - lWidth)
                End If
                If lX < 0 Then lX = 0

                vContext.Save()

                Dim lBgColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eCurrentLineColor)
                Dim lBgPattern As New Cairo.SolidPattern(lBgColor.R, lBgColor.G, lBgColor.B)
                vContext.SetSource(lBgPattern)
                vContext.Rectangle(lX, lY, lWidth, lHeight)
                vContext.Fill()
                lBgPattern.Dispose()

                Dim lBorderColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eAccentColor)
                vContext.SetSourceRGB(lBorderColor.R, lBorderColor.G, lBorderColor.B)
                vContext.LineWidth = 1.0
                vContext.Rectangle(lX + 0.5, lY + 0.5, lWidth - 1, lHeight - 1)
                vContext.Stroke()

                Dim lTextColor As Cairo.Color = lTheme.CairoColor(EditorTheme.Tags.eForegroundColor)
                Dim lTextPattern As New Cairo.SolidPattern(lTextColor.R, lTextColor.G, lTextColor.B)
                vContext.SetSource(lTextPattern)
                vContext.MoveTo(lX + lPaddingX, lY + lPaddingY)
                Pango.CairoHelper.ShowLayout(vContext, lLayout)
                lTextPattern.Dispose()

                lLayout.Dispose()

                vContext.Restore()

            Catch ex As Exception
                Console.WriteLine($"DrawHoverDeclarationTooltip error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
