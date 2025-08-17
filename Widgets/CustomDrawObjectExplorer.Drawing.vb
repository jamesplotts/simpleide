' Widgets/CustomDrawObjectExplorer.Drawing.vb - Drawing methods for Object Explorer
' Created: 2025-08-16
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing all drawing-related methods for the Object Explorer
    ''' </summary>
    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
        
        ' ===== Main Drawing Method =====
        

        
        ''' <summary>
        ''' Converts a pixbuf to grayscale for private members
        ''' </summary>
        Private Function ConvertToGrayscale(vPixbuf As Pixbuf) As Pixbuf
            Try
                Dim lCopy As Pixbuf = vPixbuf.Copy()
                lCopy.SaturateAndPixelate(lCopy, 0.0F, False)
                Return lCopy
                
            Catch ex As Exception
                Console.WriteLine($"ConvertToGrayscale error: {ex.Message}")
                Return vPixbuf
            End Try
        End Function
        
'        ''' <summary>
'        ''' Draws a fallback icon when the actual icon cannot be loaded
'        ''' </summary>
'        Private Sub DrawFallbackIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vNodeType As CodeNodeType)
'            Try
'                Dim lCenterX As Double = vX + pIconSize / 2
'                Dim lCenterY As Double = vY + pRowHeight / 2
'                Dim lRadius As Double = pIconSize / 3
'                
'                vContext.SetSourceRGB(0.3, 0.3, 0.7)
'                
'                Select Case vNodeType
'                    Case CodeNodeType.eNamespace
'                        ' Draw folder shape
'                        vContext.Rectangle(vX + 2, lCenterY - lRadius, pIconSize - 4, lRadius * 2)
'                        
'                    Case CodeNodeType.eClass, CodeNodeType.eModule
'                        ' Draw square
'                        vContext.Rectangle(lCenterX - lRadius, lCenterY - lRadius, lRadius * 2, lRadius * 2)
'                        
'                    Case CodeNodeType.eInterface
'                        ' Draw diamond
'                        vContext.MoveTo(lCenterX, lCenterY - lRadius)
'                        vContext.LineTo(lCenterX + lRadius, lCenterY)
'                        vContext.LineTo(lCenterX, lCenterY + lRadius)
'                        vContext.LineTo(lCenterX - lRadius, lCenterY)
'                        vContext.ClosePath()
'                        
'                    Case CodeNodeType.eMethod, CodeNodeType.eFunction
'                        ' Draw circle
'                        vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
'                        
'                    Case CodeNodeType.eProperty
'                        ' Draw wrench shape (simplified)
'                        vContext.Rectangle(lCenterX - lRadius/2, lCenterY - lRadius, lRadius, lRadius * 2)
'                        
'                    Case CodeNodeType.eEnum
'                        ' Draw list shape
'                        For i As Integer = -1 To 1
'                            vContext.Rectangle(vX + 4, lCenterY + i * lRadius/2 - 1, pIconSize - 8, 2)
'                        Next
'                        
'                    Case Else
'                        ' Draw triangle
'                        vContext.MoveTo(lCenterX, lCenterY - lRadius)
'                        vContext.LineTo(lCenterX + lRadius, lCenterY + lRadius)
'                        vContext.LineTo(lCenterX - lRadius, lCenterY + lRadius)
'                        vContext.ClosePath()
'                End Select
'                
'                vContext.Fill()
'                
'            Catch ex As Exception
'                Console.WriteLine($"DrawFallbackIcon error: {ex.Message}")
'            End Try
'        End Sub
        
        ' ===== Text Drawing =====
        

        
        ' ===== Highlight Drawing =====
        
        ''' <summary>
        ''' Draws selection highlight for the selected node
        ''' </summary>
        Private Sub DrawSelectionHighlight(vContext As Cairo.Context, vNode As VisualNode)
            Try
                vContext.SetSourceRGBA(0.2, 0.4, 0.8, 0.3) ' Light blue with transparency
                vContext.Rectangle(0, vNode.Y, pContentWidth, vNode.Height)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawSelectionHighlight error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws hover highlight for the hovered node
        ''' </summary>
        Private Sub DrawHoverHighlight(vContext As Cairo.Context, vNode As VisualNode)
            Try
                vContext.SetSourceRGBA(0.5, 0.5, 0.5, 0.1) ' Light gray with transparency
                vContext.Rectangle(0, vNode.Y, pContentWidth, vNode.Height)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawHoverHighlight error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Indentation Lines Drawing =====
        
        ''' <summary>
        ''' Draws indentation lines to show hierarchy
        ''' </summary>
        Private Sub DrawIndentationLines(vContext As Cairo.Context)
            Try
                vContext.SetSourceRGBA(0.8, 0.8, 0.8, 0.5)
                vContext.LineWidth = 1
                vContext.SetDash({2.0, 2.0}, 0)
                
                ' Draw vertical lines for each indentation level
                ' Track which levels need lines
                Dim lLevelLines As New Dictionary(Of Integer, List(Of Integer))
                
                For Each lNode In pVisibleNodes
                    If lNode.Level > 0 Then
                        If Not lLevelLines.ContainsKey(lNode.Level - 1) Then
                            lLevelLines(lNode.Level - 1) = New List(Of Integer)
                        End If
                        lLevelLines(lNode.Level - 1).Add(lNode.Y)
                    End If
                Next
                
                ' Draw the lines
                For Each lLevel In lLevelLines
                    If lLevel.Value.Count > 0 Then
                        Dim lX As Double = lLevel.Key * pIndentWidth + pPlusMinusSize / 2
                        Dim lMinY As Integer = lLevel.Value.Min()
                        Dim lMaxY As Integer = lLevel.Value.Max()
                        
                        vContext.MoveTo(lX, lMinY)
                        vContext.LineTo(lX, lMaxY + pRowHeight)
                        vContext.Stroke()
                    End If
                Next
                
                vContext.SetDash({}, 0) ' Reset dash
                
            Catch ex As Exception
                Console.WriteLine($"DrawIndentationLines error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Corner Box Drawing =====
        
        ''' <summary>
        ''' Draws the corner box grip handle
        ''' </summary>
        Private Function OnCornerBoxDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                
                ' Draw background matching scrollbar style
                lContext.SetSourceRGB(0.9, 0.9, 0.9)
                lContext.Rectangle(0, 0, pCornerBox.AllocatedWidth, pCornerBox.AllocatedHeight)
                lContext.Fill()
                
                ' Draw grip lines
                lContext.SetSourceRGB(0.6, 0.6, 0.6)
                lContext.LineWidth = 1
                
                For i As Integer = 4 To 12 Step 3
                    lContext.MoveTo(i, pCornerBox.AllocatedHeight - 4)
                    lContext.LineTo(pCornerBox.AllocatedWidth - 4, i)
                    lContext.Stroke()
                Next
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnCornerBoxDraw error: {ex.Message}")
                Return True
            End Try
        End Function
        
        ' ===== Font and Scale Management =====
        
        ''' <summary>
        ''' Updates font settings based on current scale
        ''' </summary>
        Private Sub UpdateFontSettings()
            Try
                If pFontDescription IsNot Nothing Then
                    pFontDescription.Dispose()
                End If
                
                ' Create scaled font
                Dim lFontName As String = pSettingsManager.GetString("ObjectExplorer.FontFamily", "Monospace")
                pFontDescription = FontDescription.FromString($"{lFontName} {pFontSize}")
                
                ' Get font metrics for layout calculations
                If pDrawingArea?.PangoContext IsNot Nothing Then
                    Using lLayout As New Pango.Layout(pDrawingArea.PangoContext)
                        lLayout.FontDescription = pFontDescription
                        lLayout.SetText("M")
                        Dim lInkRect, lLogicalRect As Pango.Rectangle
                        lLayout.GetPixelExtents(lInkRect, lLogicalRect)
                        ' Store metrics if needed for precise positioning
                    End Using
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFontSettings error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies the specified scale percentage to all visual elements
        ''' </summary>
        ''' <param name="vScalePercent">Scale percentage (50-200)</param>
        Private Sub ApplyScale(vScalePercent As Integer)
            Try
                pCurrentScale = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, vScalePercent))
                
                ' Calculate scaled sizes
                pIconSize = CInt(16 * pCurrentScale / 100)
                pFontSize = CSng(10 * pCurrentScale / 100)
                pRowHeight = pIconSize + ROW_PADDING
                pIndentWidth = CInt(pIconSize * INDENT_WIDTH_RATIO)
                pPlusMinusSize = CInt(pIconSize * PLUS_MINUS_SIZE_RATIO)
                
                ' Update font
                UpdateFontSettings()
                
                ' Rebuild visual tree with new dimensions
                RebuildVisualTree()
                
                ' Invalidate drawing
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyScale error: {ex.Message}")
            End Try
        End Sub


        

        

        

        

        

        
        ' ===== Zoom Constants =====
        Private Const ZOOM_STEP As Integer = 10  ' Percentage step for zoom in/out
        
        ' ===== Zoom Methods =====
        
        ''' <summary>
        ''' Increases the zoom level by scaling up the Object Explorer display
        ''' </summary>
        ''' <remarks>
        ''' Increases the current scale by ZOOM_STEP percentage points up to MAX_SCALE
        ''' </remarks>
        Public Sub ZoomIn()
            Try
                ' Calculate new scale
                Dim lNewScale As Integer = pCurrentScale + ZOOM_STEP
                
                ' Apply maximum limit
                If lNewScale > MAX_SCALE Then
                    lNewScale = MAX_SCALE
                End If
                
                ' Only update if scale changed
                If lNewScale <> pCurrentScale Then
                    ApplyScale(lNewScale)
                    SaveScaleSetting()
                    
                    ' Log the zoom change
                    Console.WriteLine($"ObjectExplorer ZoomIn: {pCurrentScale}% scale")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ZoomIn error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Decreases the zoom level by scaling down the Object Explorer display
        ''' </summary>
        ''' <remarks>
        ''' Decreases the current scale by ZOOM_STEP percentage points down to MIN_SCALE
        ''' </remarks>
        Public Sub ZoomOut()
            Try
                ' Calculate new scale
                Dim lNewScale As Integer = pCurrentScale - ZOOM_STEP
                
                ' Apply minimum limit
                If lNewScale < MIN_SCALE Then
                    lNewScale = MIN_SCALE
                End If
                
                ' Only update if scale changed
                If lNewScale <> pCurrentScale Then
                    ApplyScale(lNewScale)
                    SaveScaleSetting()
                    
                    ' Log the zoom change
                    Console.WriteLine($"ObjectExplorer ZoomOut: {pCurrentScale}% scale")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ZoomOut error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Resets the zoom level to the default scale
        ''' </summary>
        ''' <remarks>
        ''' Sets the scale back to DEFAULT_SCALE (100%)
        ''' </remarks>
        Public Sub ZoomReset()
            Try
                ' Reset to default scale
                If pCurrentScale <> DEFAULT_SCALE Then
                    ApplyScale(DEFAULT_SCALE)
                    SaveScaleSetting()
                    
                    ' Log the zoom reset
                    Console.WriteLine($"ObjectExplorer ZoomReset: {DEFAULT_SCALE}% scale")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ZoomReset error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the current zoom percentage
        ''' </summary>
        ''' <returns>Current scale as a percentage (50-200)</returns>
        Public Function GetZoomPercentage() As Integer
            Try
                Return pCurrentScale
                
            Catch ex As Exception
                Console.WriteLine($"GetZoomPercentage error: {ex.Message}")
                Return DEFAULT_SCALE
            End Try
        End Function
        
        ''' <summary>
        ''' Sets the zoom to a specific percentage
        ''' </summary>
        ''' <param name="vPercentage">Zoom percentage to apply (50-200)</param>
        Public Sub SetZoomPercentage(vPercentage As Integer)
            Try
                ' Validate percentage
                If vPercentage < MIN_SCALE OrElse vPercentage > MAX_SCALE Then
                    Console.WriteLine($"SetZoomPercentage: Invalid percentage {vPercentage}%")
                    Return
                End If
                
                ' Apply the scale if different
                If vPercentage <> pCurrentScale Then
                    ApplyScale(vPercentage)
                    SaveScaleSetting()
                    
                    ' Log the zoom change
                    Console.WriteLine($"ObjectExplorer SetZoom: {pCurrentScale}% scale")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetZoomPercentage error: {ex.Message}")
            End Try
        End Sub

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
        
        ''' <summary>
        ''' Draws all visible nodes in the tree
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <remarks>
        ''' Iterates through visible nodes and draws each one with viewport culling
        ''' </remarks>
        Private Sub DrawVisibleNodes(vContext As Cairo.Context)
            Try
                If pVisibleNodes Is Nothing OrElse pVisibleNodes.Count = 0 Then
                    Return
                End If
                
                ' Calculate visible range for viewport culling
                Dim lViewportTop As Integer = pScrollY
                Dim lViewportBottom As Integer = pScrollY + pViewportHeight
                
                ' Draw each visible node
                For Each lNode In pVisibleNodes
                    ' Skip nodes outside viewport (viewport culling)
                    If lNode.Y + lNode.Height < lViewportTop Then
                        Continue For
                    End If
                    If lNode.Y > lViewportBottom Then
                        Exit For ' Nodes are in order, so we can stop here
                    End If
                    
                    ' Draw the node components
                    DrawNode(vContext, lNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DrawVisibleNodes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws an individual node with all its components
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vNode">The visual node to draw</param>
        ''' <remarks>
        ''' Draws plus/minus, icon, and text for the node
        ''' </remarks>
        Private Sub DrawNode(vContext As Cairo.Context, vNode As VisualNode)
            Try
                If vNode Is Nothing Then Return
                
                Dim lX As Integer = vNode.X
                Dim lY As Integer = vNode.Y
                
                ' Draw plus/minus if node has children
                If vNode.HasChildren Then
                    DrawPlusMinus(vContext, lX, lY, vNode.IsExpanded)
                    lX += pPlusMinusSize + ICON_SPACING
                End If
                
                ' Draw icon
                DrawIcon(vContext, lX, lY, vNode.Node)
                lX += pIconSize + ICON_SPACING
                
                ' Draw text
                DrawNodeText(vContext, lX, lY, vNode)
                
            Catch ex As Exception
                Console.WriteLine($"DrawNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the plus/minus expand/collapse indicator
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vIsExpanded">Whether the node is expanded</param>
        ''' <remarks>
        ''' Draws a box with either + or - inside depending on expansion state
        ''' </remarks>
        Private Sub DrawPlusMinus(vContext As Cairo.Context, vX As Integer, vY As Integer, vIsExpanded As Boolean)
            Try
                ' Get theme colors
                Dim lThemeName As String = pSettingsManager.GetString("CurrentTheme", "Default Dark")
                Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                
                Dim lBorderColor As Cairo.Color
                Dim lSymbolColor As Cairo.Color
                
                If lIsDark Then
                    lBorderColor = HexToCairoColor("#808080")  ' Gray border for dark theme
                    lSymbolColor = HexToCairoColor("#C0C0C0")  ' Light gray symbol
                Else
                    lBorderColor = HexToCairoColor("#606060")  ' Dark gray border for light theme
                    lSymbolColor = HexToCairoColor("#303030")  ' Darker gray symbol
                End If
                
                ' Calculate center position
                Dim lCenterY As Integer = vY + pRowHeight \ 2
                Dim lHalfSize As Integer = pPlusMinusSize \ 2
                
                ' Draw box
                vContext.SetSourceRGB(lBorderColor.R, lBorderColor.G, lBorderColor.B)
                vContext.LineWidth = 1
                vContext.Rectangle(vX, lCenterY - lHalfSize, pPlusMinusSize, pPlusMinusSize)
                vContext.Stroke()
                
                ' Draw plus or minus
                vContext.SetSourceRGB(lSymbolColor.R, lSymbolColor.G, lSymbolColor.B)
                vContext.LineWidth = 1
                
                ' Horizontal line (for both plus and minus)
                vContext.MoveTo(vX + 2, lCenterY)
                vContext.LineTo(vX + pPlusMinusSize - 2, lCenterY)
                vContext.Stroke()
                
                ' Vertical line (only for plus)
                If Not vIsExpanded Then
                    vContext.MoveTo(vX + lHalfSize, lCenterY - lHalfSize + 2)
                    vContext.LineTo(vX + lHalfSize, lCenterY + lHalfSize - 2)
                    vContext.Stroke()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DrawPlusMinus error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the icon for a node based on its type
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vNode">The syntax node to get the icon for</param>
        ''' <remarks>
        ''' Attempts to load an icon from the theme, falls back to drawing a simple shape
        ''' </remarks>
        Private Sub DrawIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vNode As SyntaxNode)
            Try
                ' Calculate center position
                Dim lCenterY As Integer = vY + pRowHeight \ 2
                
                ' Try to get icon from theme
                Dim lIconName As String = GetIconNameForNode(vNode)
                Dim lIconTheme As IconTheme = IconTheme.Default
                
                If lIconTheme.HasIcon(lIconName) Then
                    Try
                        Dim lPixbuf As Pixbuf = lIconTheme.LoadIcon(lIconName, pIconSize, IconLookupFlags.ForceSvg)
                        
                        ' Apply grayscale for private members
                        If Not vNode.IsPublic AndAlso Not vNode.IsProtected Then
                            lPixbuf = ConvertToGrayscale(lPixbuf)
                        End If
                        
                        ' Draw the pixbuf
                        Gdk.CairoHelper.SetSourcePixbuf(vContext, lPixbuf, vX, lCenterY - pIconSize \ 2)
                        vContext.Paint()
                        
                        lPixbuf.Dispose()
                        Return
                    Catch
                        ' Fall through to draw fallback icon
                    End Try
                End If
                
                ' Draw fallback icon
                DrawFallbackIcon(vContext, vX, lCenterY - pIconSize \ 2, vNode.NodeType)
                
            Catch ex As Exception
                Console.WriteLine($"DrawIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the appropriate icon name for a node type
        ''' </summary>
        ''' <param name="vNode">The syntax node to get icon for</param>
        ''' <returns>Icon name string for the theme</returns>
        Private Function GetIconNameForNode(vNode As SyntaxNode) As String
            Try
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass
                        Return "application-x-executable-symbolic"
                    Case CodeNodeType.eInterface
                        Return "application-x-addon-symbolic"
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction
                        Return "system-run-symbolic"
                    Case CodeNodeType.eProperty
                        Return "document-properties-symbolic"
                    Case CodeNodeType.eField
                        Return "insert-object-symbolic"
                    Case CodeNodeType.eEvent
                        Return "starred-symbolic"
                    Case CodeNodeType.eEnum
                        Return "format-justify-left-symbolic"
                    Case CodeNodeType.eNamespace
                        Return "folder-symbolic"
                    Case CodeNodeType.eModule
                        Return "package-x-generic-symbolic"
                    Case CodeNodeType.eStructure
                        Return "view-list-symbolic"
                    Case CodeNodeType.eDelegate
                        Return "mail-forward-symbolic"
                    Case CodeNodeType.eRegion
                        Return "folder-open-symbolic"
                    Case Else
                        Return "text-x-generic-symbolic"
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetIconNameForNode error: {ex.Message}")
                Return "text-x-generic-symbolic"
            End Try
        End Function
        
        ''' <summary>
        ''' Draws a simple fallback icon when theme icon is not available
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vNodeType">Type of node to draw icon for</param>
        ''' <remarks>
        ''' Draws simple geometric shapes to represent different node types with theme-aware colors
        ''' </remarks>
        Private Sub DrawFallbackIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vNodeType As CodeNodeType)
            Try
                ' Get theme information
                Dim lThemeName As String = pSettingsManager.GetString("CurrentTheme", "Default Dark")
                Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                
                ' Set color based on node type and theme
                Dim lColor As Cairo.Color
                Select Case vNodeType
                    Case CodeNodeType.eClass
                        lColor = If(lIsDark, HexToCairoColor("#4EC9B0"), HexToCairoColor("#2B91AF"))  ' Teal/Blue
                    Case CodeNodeType.eInterface
                        lColor = If(lIsDark, HexToCairoColor("#B8D7A3"), HexToCairoColor("#6B8E23"))  ' Light/Dark green
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction
                        lColor = If(lIsDark, HexToCairoColor("#DCDCAA"), HexToCairoColor("#795E26"))  ' Yellow/Brown
                    Case CodeNodeType.eProperty
                        lColor = If(lIsDark, HexToCairoColor("#9CDCFE"), HexToCairoColor("#0070C0"))  ' Light/Dark blue
                    Case CodeNodeType.eField
                        lColor = If(lIsDark, HexToCairoColor("#C586C0"), HexToCairoColor("#9B4F96"))  ' Magenta
                    Case CodeNodeType.eEvent
                        lColor = If(lIsDark, HexToCairoColor("#CE9178"), HexToCairoColor("#A31515"))  ' Orange/Red
                    Case Else
                        lColor = If(lIsDark, HexToCairoColor("#808080"), HexToCairoColor("#606060"))  ' Gray
                End Select
                
                vContext.SetSourceRGB(lColor.R, lColor.G, lColor.B)
                
                ' Draw shape based on node type
                Dim lCenterX As Integer = vX + pIconSize \ 2
                Dim lCenterY As Integer = vY + pIconSize \ 2
                Dim lRadius As Integer = pIconSize \ 3
                
                Select Case vNodeType
                    Case CodeNodeType.eClass, CodeNodeType.eInterface
                        ' Rectangle
                        vContext.Rectangle(vX + 2, vY + 2, pIconSize - 4, pIconSize - 4)
                        
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction
                        ' Circle
                        vContext.Arc(lCenterX, lCenterY, lRadius, 0, 2 * Math.PI)
                        
                    Case CodeNodeType.eProperty, CodeNodeType.eField
                        ' Diamond
                        vContext.MoveTo(lCenterX, lCenterY - lRadius)
                        vContext.LineTo(lCenterX + lRadius, lCenterY)
                        vContext.LineTo(lCenterX, lCenterY + lRadius)
                        vContext.LineTo(lCenterX - lRadius, lCenterY)
                        vContext.ClosePath()
                        
                    Case CodeNodeType.eEvent
                        ' Star (simplified - just a triangle)
                        vContext.MoveTo(lCenterX, lCenterY - lRadius)
                        vContext.LineTo(lCenterX + lRadius, lCenterY + lRadius)
                        vContext.LineTo(lCenterX - lRadius, lCenterY + lRadius)
                        vContext.ClosePath()
                        
                    Case Else
                        ' Simple filled circle
                        vContext.Arc(lCenterX, lCenterY, lRadius \ 2, 0, 2 * Math.PI)
                End Select
                
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawFallbackIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Overridden DrawNodeText method with theme color support
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vNode">The visual node containing the text to draw</param>
        ''' <remarks>
        ''' This overrides the existing DrawNodeText to use theme colors
        ''' </remarks>
        Private Shadows Sub DrawNodeText(vContext As Cairo.Context, vX As Integer, vY As Integer, vNode As VisualNode)
            Try
                ' Create layout for text
                Dim lLayout As New Pango.Layout(pDrawingArea.PangoContext)
                lLayout.FontDescription = pFontDescription
                
                ' Build display text
                Dim lText As String = vNode.Node.Name
                
                ' Add modifiers if applicable
                If vNode.Node.IsShared Then
                    lText = lText & " (Shared)"
                End If
                
                ' Add overload/shadow indicators for methods
                If vNode.Node.NodeType = CodeNodeType.eMethod OrElse vNode.Node.NodeType = CodeNodeType.eFunction Then
                    If vNode.Node.IsOverrides Then
                        lText = lText & " (Overrides)"
                    ElseIf vNode.Node.Attributes.ContainsKey("Shadows") Then
                        lText = lText & " (Shadows)"
                    ElseIf vNode.Node.Attributes.ContainsKey("Overloads") Then
                        lText = lText & " (Overloads)"
                    End If
                End If
                
                lLayout.SetText(lText)
                
                ' Get theme information
                Dim lThemeName As String = pSettingsManager.GetString("CurrentTheme", "Default Dark")
                Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                
                ' Set color based on visibility and theme
                Dim lTextColor As Cairo.Color
                If vNode.Node.IsPublic Then
                    ' Public members - full contrast
                    lTextColor = If(lIsDark, HexToCairoColor("#D4D4D4"), HexToCairoColor("#000000"))
                ElseIf vNode.Node.IsProtected Then
                    ' Protected members - slightly dimmed
                    lTextColor = If(lIsDark, HexToCairoColor("#B0B0B0"), HexToCairoColor("#404040"))
                ElseIf vNode.Node.IsFriend Then
                    ' Friend members - medium contrast
                    lTextColor = If(lIsDark, HexToCairoColor("#909090"), HexToCairoColor("#606060"))
                Else
                    ' Private members - dimmed
                    lTextColor = If(lIsDark, HexToCairoColor("#707070"), HexToCairoColor("#808080"))
                End If
                
                vContext.SetSourceRGB(lTextColor.R, lTextColor.G, lTextColor.B)
                
                ' Draw text aligned vertically
                vContext.MoveTo(vX, vY + (pRowHeight - pFontSize) / 2)
                Pango.CairoHelper.ShowLayout(vContext, lLayout)
                
                lLayout.Dispose()
                
            Catch ex As Exception
                Console.WriteLine($"DrawNodeText error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updated initialization method to ensure proper rendering setup
        ''' </summary>
        Public Sub InitializeRendering()
            Try
                ' Ensure drawing area is set up for events
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.CanFocus = True
                    pDrawingArea.Events = pDrawingArea.Events Or EventMask.ExposureMask Or 
                                          EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                                          EventMask.PointerMotionMask Or EventMask.ScrollMask Or
                                          EventMask.KeyPressMask Or EventMask.KeyReleaseMask
                    
                    ' Ensure it's visible
                    pDrawingArea.Visible = True
                    pDrawingArea.ShowAll()
                End If
                
                ' Apply initial theme
                ApplyTheme()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeRendering error: {ex.Message}")
            End Try
        End Sub
        

        
        ''' <summary>
        ''' Updated DrawBackground method with proper theme colors
        ''' </summary>
        Private Sub DrawBackground(vContext As Cairo.Context)
            Try
                ' Get current theme from settings manager
                Dim lBackgroundColor As Cairo.Color
                
                If pSettingsManager IsNot Nothing Then
                    Dim lThemeName As String = pSettingsManager.GetString("CurrentTheme", "Default Dark")
                    Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                    
                    If lIsDark Then
                        ' Dark theme background - VS Code dark
                        lBackgroundColor = HexToCairoColor("#252526")
                    Else
                        ' Light theme background
                        lBackgroundColor = HexToCairoColor("#FFFFFF")
                    End If
                Else
                    ' Default to dark theme
                    lBackgroundColor = HexToCairoColor("#252526")
                End If
                
                ' Fill background
                vContext.SetSourceRGB(lBackgroundColor.R, lBackgroundColor.G, lBackgroundColor.B)
                vContext.Rectangle(0, 0, Math.Max(pViewportWidth, 100), Math.Max(pViewportHeight, 100))
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawBackground error: {ex.Message}")
            End Try
        End Sub

        

        


        ''' <summary>
        ''' Updated OnDrawingAreaDraw with better debugging
        ''' </summary>
        Private Function OnDrawingAreaDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                
                ' Always draw background
                DrawBackground(lContext)
                
                ' Debug output
                If pVisibleNodes Is Nothing Then
                    Console.WriteLine("OnDrawingAreaDraw: pVisibleNodes is Nothing!")
                    pVisibleNodes = New List(Of VisualNode)()
                End If
                
                Console.WriteLine($"OnDrawingAreaDraw: Drawing {pVisibleNodes.Count} nodes, Root={If(pRootNode IsNot Nothing, pRootNode.Name, "Nothing")}")
                
                ' Check if we have nodes to draw
                If pVisibleNodes.Count = 0 Then
                    ' Draw "No items" message
                    Dim lThemeName As String = If(pSettingsManager IsNot Nothing, pSettingsManager.GetString("CurrentTheme", "Default Dark"), "Default Dark")
                    Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                    
                    If lIsDark Then
                        lContext.SetSourceRGB(0.6, 0.6, 0.6)  ' Gray text for dark theme
                    Else
                        lContext.SetSourceRGB(0.4, 0.4, 0.4)  ' Darker gray for light theme
                    End If
                    
                    lContext.MoveTo(10, 30)
                    lContext.ShowText("No items to display")
                    
                    ' Check if we should have nodes
                    If pRootNode IsNot Nothing Then
                        lContext.MoveTo(10, 50)
                        lContext.ShowText($"(Root exists: {pRootNode.Name})")
                        
                        ' Try to rebuild if we have a root but no visible nodes
                        Console.WriteLine("Have root but no visible nodes - attempting rebuild...")
                        Application.Invoke(Sub()
                            RebuildVisualTree()
                        End Sub)
                    End If
                Else
                    ' We have nodes - draw them
                    Console.WriteLine($"Drawing {pVisibleNodes.Count} nodes...")
                    
                    ' Setup clipping for viewport
                    lContext.Rectangle(0, 0, Math.Max(pViewportWidth, 100), Math.Max(pViewportHeight, 100))
                    lContext.Clip()
                    
                    ' Apply scrolling translation
                    lContext.Translate(-pScrollX, -pScrollY)
                    
                    ' Draw all visible nodes
                    DrawVisibleNodes(lContext)
                    
                    ' Draw selection highlight if needed
                    If pSelectedNode IsNot Nothing Then
                        DrawSelectionHighlight(lContext, pSelectedNode)
                    End If
                    
                    ' Draw hover highlight if needed
                    If pHoveredNode IsNot Nothing AndAlso pHoveredNode IsNot pSelectedNode Then
                        DrawHoverHighlight(lContext, pHoveredNode)
                    End If
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaDraw error: {ex.Message}")
                ' Draw error message
                Try
                    Dim lContext As Cairo.Context = vArgs.Cr
                    lContext.SetSourceRGB(1.0, 0.0, 0.0)
                    lContext.MoveTo(10, 30)
                    lContext.ShowText($"Error: {ex.Message}")
                Catch
                    ' Ignore errors in error handler
                End Try
                Return False
            End Try
        End Function
        
    End Class
    
End Namespace
