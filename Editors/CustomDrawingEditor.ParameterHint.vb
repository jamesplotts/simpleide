' CustomDrawingEditor.ParameterHint.vb - "Signature help" style popup showing the
' declaration of whichever parameter the cursor currently sits on inside a method/
' constructor call's parentheses, updating live as the cursor moves between commas.
' Drawn as a Cairo overlay on the editor surface, same approach as the CodeSense popup and
' the hover-declaration tooltip.
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

        Private pParamHintVisible As Boolean = False
        Private pParamHintText As String = ""
        Private pParamHintLine As Integer = -1
        Private pParamHintColumn As Integer = -1

        ''' <summary>
        ''' Recomputes and shows/hides the current-parameter hint based on the cursor's
        ''' position relative to an enclosing call's parentheses. Call after every cursor
        ''' move (including the ones InsertCharacter/backspace perform, since they all route
        ''' through SetCursorPosition).
        ''' </summary>
        Private Sub UpdateParameterHint()
            Try
                ' Don't compete with the CodeSense popup for the same screen space
                If pCodeSenseActive Then
                    HideParameterHint()
                    Return
                End If

                Dim lOpenLine As Integer
                Dim lOpenColumn As Integer
                Dim lParamIndex As Integer
                If Not FindEnclosingCallOpenParen(pCursorLine, pCursorColumn, lOpenLine, lOpenColumn, lParamIndex) Then
                    HideParameterHint()
                    Return
                End If

                Dim lCalleeName As String = GetIdentifierBeforeColumn(lOpenLine, lOpenColumn)
                If String.IsNullOrEmpty(lCalleeName) Then
                    HideParameterHint()
                    Return
                End If

                Dim lMethodNode As SyntaxNode = FindCallableMemberNode(lCalleeName)
                If lMethodNode Is Nothing OrElse lMethodNode.Parameters Is Nothing OrElse lMethodNode.Parameters.Count = 0 Then
                    HideParameterHint()
                    Return
                End If

                Dim lParam As ParameterInfo
                If lParamIndex < lMethodNode.Parameters.Count Then
                    lParam = lMethodNode.Parameters(lParamIndex)
                Else
                    ' Past the declared parameter count - only meaningful if the trailing
                    ' parameter is a ParamArray (accepts any number of extra arguments)
                    Dim lLast As ParameterInfo = lMethodNode.Parameters(lMethodNode.Parameters.Count - 1)
                    If Not lLast.IsParamArray Then
                        HideParameterHint()
                        Return
                    End If
                    lParam = lLast
                End If

                pParamHintText = FormatParameterDeclaration(lParam)
                pParamHintLine = pCursorLine
                pParamHintColumn = pCursorColumn
                pParamHintVisible = True
                pDrawingArea?.QueueDraw()

            Catch ex As Exception
                Console.WriteLine($"UpdateParameterHint error: {ex.Message}")
                HideParameterHint()
            End Try
        End Sub

        ''' <summary>
        ''' Hides the parameter hint popup, if shown
        ''' </summary>
        Private Sub HideParameterHint()
            If pParamHintVisible Then
                pParamHintVisible = False
                pDrawingArea?.QueueDraw()
            End If
        End Sub

        ''' <summary>
        ''' Scans backward from (vLine, vColumn) for the nearest enclosing unmatched "(",
        ''' skipping over balanced nested "(...)" pairs, and counts top-level commas along
        ''' the way to determine which argument position the cursor is at
        ''' </summary>
        ''' <returns>True if an enclosing "(" was found within the scan-back limit</returns>
        Private Function FindEnclosingCallOpenParen(vLine As Integer, vColumn As Integer,
                                                     ByRef vOpenLine As Integer, ByRef vOpenColumn As Integer,
                                                     ByRef vParamIndex As Integer) As Boolean
            Const MaxLinesBack As Integer = 50
            Dim lDepth As Integer = 0
            Dim lCommaCount As Integer = 0

            Dim lLine As Integer = vLine
            If lLine < 0 OrElse lLine >= pLineCount Then Return False
            Dim lCol As Integer = Math.Min(vColumn, TextLines(lLine).Length) - 1

            Dim lLinesScanned As Integer = 0
            While lLine >= 0 AndAlso lLinesScanned <= MaxLinesBack
                Dim lText As String = TextLines(lLine)

                While lCol >= 0
                    If lCol < lText.Length Then
                        Select Case lText(lCol)
                            Case ")"c
                                lDepth += 1
                            Case "("c
                                If lDepth = 0 Then
                                    vOpenLine = lLine
                                    vOpenColumn = lCol
                                    vParamIndex = lCommaCount
                                    Return True
                                Else
                                    lDepth -= 1
                                End If
                            Case ","c
                                If lDepth = 0 Then lCommaCount += 1
                        End Select
                    End If
                    lCol -= 1
                End While

                lLine -= 1
                lLinesScanned += 1
                If lLine >= 0 Then lCol = TextLines(lLine).Length - 1
            End While

            Return False
        End Function

        ''' <summary>
        ''' Gets the identifier immediately before the given column (skipping whitespace) -
        ''' used to get the callee name right before an open paren, at an arbitrary position
        ''' rather than the live cursor (see GetIdentifierBeforeParen for the cursor-only
        ''' version CodeSense's own "(" trigger uses)
        ''' </summary>
        Private Function GetIdentifierBeforeColumn(vLine As Integer, vColumn As Integer) As String
            If vLine < 0 OrElse vLine >= pLineCount Then Return ""
            Dim lLineText As String = TextLines(vLine)
            Dim lEnd As Integer = Math.Min(vColumn, lLineText.Length)

            While lEnd > 0 AndAlso Char.IsWhiteSpace(lLineText(lEnd - 1))
                lEnd -= 1
            End While
            If lEnd = 0 Then Return ""

            Dim lStart As Integer = lEnd
            While lStart > 0 AndAlso (Char.IsLetterOrDigit(lLineText(lStart - 1)) OrElse lLineText(lStart - 1) = "_"c)
                lStart -= 1
            End While

            If lStart < lEnd Then Return lLineText.Substring(lStart, lEnd - lStart)
            Return ""
        End Function

        ''' <summary>
        ''' Resolves vName to a callable node's parameters. Handles both plain calls
        ''' ("Foo(...)" -> method/function/constructor named "Foo") and object construction
        ''' ("New Foo(...)" -> the identifier before "(" is the TYPE name "Foo", so this
        ''' looks up Foo's own "New" constructor rather than matching any class's constructor
        ''' by coincidence of them all being named "New")
        ''' </summary>
        ''' <remarks>
        ''' Best-effort, first-match lookup - like CodeSenseEngine.GetParameterHints, this
        ''' does not disambiguate between overloads of the same name
        ''' </remarks>
        Private Function FindCallableMemberNode(vName As String) As SyntaxNode
            If pProjectManager Is Nothing Then Return Nothing
            Dim lTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
            If lTree Is Nothing Then Return Nothing

            Dim lClassNode As SyntaxNode = FindClassNodeByName(lTree, vName)
            If lClassNode IsNot Nothing AndAlso lClassNode.Children IsNot Nothing Then
                for each lMember As SyntaxNode in lClassNode.Children
                    If lMember.NodeType = CodeNodeType.eConstructor Then Return lMember
                Next
            End If

            Return FindCallableNode(lTree, vName)
        End Function

        ''' <summary>
        ''' Recursively finds the first class/module/structure node named vName
        ''' </summary>
        Private Function FindClassNodeByName(vNode As SyntaxNode, vName As String) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eStructure
                        If String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then Return vNode
                End Select

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindClassNodeByName(lChild, vName)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"FindClassNodeByName error: {ex.Message}")
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' Recursively finds the first method/function/constructor node named vName
        ''' </summary>
        Private Function FindCallableNode(vNode As SyntaxNode, vName As String) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor
                        If String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then Return vNode
                End Select

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindCallableNode(lChild, vName)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"FindCallableNode error: {ex.Message}")
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' Draws the parameter hint overlay, if currently visible - call from DrawContent
        ''' </summary>
        Private Sub DrawParameterHint(vContext As Cairo.Context)
            Try
                If Not pParamHintVisible OrElse String.IsNullOrEmpty(pParamHintText) Then Return

                Dim lTheme As EditorTheme = GetActiveTheme()

                Dim lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                lLayout.FontDescription = pFontDescription
                lLayout.SetText(pParamHintText)

                Dim lTextWidth As Integer
                Dim lTextHeight As Integer
                lLayout.GetPixelSize(lTextWidth, lTextHeight)

                Const lPaddingX As Integer = 6
                Const lPaddingY As Integer = 3
                Dim lWidth As Integer = lTextWidth + lPaddingX * 2
                Dim lHeight As Integer = lTextHeight + lPaddingY * 2

                Dim lFirstLine As Integer = Math.Max(0, pFirstVisibleLine)
                Dim lVisualLine As Integer = SourceToVisualLine(pParamHintLine)

                Dim lX As Integer = pLeftPadding + (pParamHintColumn - pFirstVisibleColumn) * pCharWidth
                Dim lY As Integer = (lVisualLine - lFirstLine + 1) * pLineHeight + pTopPadding + CodeSenseRowTopCorrection

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
                Console.WriteLine($"DrawParameterHint error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
