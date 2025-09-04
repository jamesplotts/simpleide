' Widgets/CustomDrawObjectExplorer.Drawing.vb - Drawing methods for Object Explorer
' Created: 2025-08-16
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing all drawing-related methods for the Object Explorer
    ''' </summary>
    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
        
        ' ===== Main Drawing Method =====
        
        Private pThemeManager As ThemeManager
        
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
                
                for each lNode in pVisibleNodes
                    If lNode.Level > 0 Then
                        If Not lLevelLines.ContainsKey(lNode.Level - 1) Then
                            lLevelLines(lNode.Level - 1) = New List(Of Integer)
                        End If
                        lLevelLines(lNode.Level - 1).Add(lNode.Y)
                    End If
                Next
                
                ' Draw the lines
                for each lLevel in lLevelLines
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
                
                for i As Integer = 4 To 12 Step 3
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
                for each lNode in pVisibleNodes
                    ' Skip nodes outside viewport (viewport culling)
                    If lNode.Y + lNode.Height < lViewportTop Then
                        Continue for
                    End If
                    If lNode.Y > lViewportBottom Then
                        Exit for ' Nodes are in order, so we can stop here
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
                DrawNodeText(vContext, vNode, lX, lY)
                
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
        ''' Draws the icon for a node based on its type with colorful icons
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vNode">The syntax node to get the icon for</param>
        ''' <remarks>
        ''' Always uses colorful fallback icons instead of monochrome symbolic icons
        ''' </remarks>
        Private Sub DrawIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vNode As SyntaxNode)
            Try
                ' Calculate center position
                Dim lCenterY As Integer = vY + pRowHeight \ 2
                
                ' For now, always use the colorful fallback icons
                ' The theme icons are often monochrome/symbolic which look drab
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
        ''' Draws a colorful fallback icon for different node types
        ''' </summary>
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vNodeType">Type of node to draw icon for</param>
        ''' <remarks>
        ''' Uses vibrant colors matching VS Code's icon theme for better visual distinction
        ''' </remarks>
        Private Sub DrawFallbackIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vNodeType As CodeNodeType)
            Try
                ' Get theme information
                Dim lThemeName As String = If(pSettingsManager IsNot Nothing, 
                                              pSettingsManager.GetString("CurrentTheme", "Default Dark"), 
                                              "Default Dark")
                Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                
                ' Set vibrant colors based on node type and theme
                Dim lColor As Cairo.Color
                Select Case vNodeType
                    Case CodeNodeType.eNamespace
                        lColor = If(lIsDark, HexToCairoColor("#C77DFF"), HexToCairoColor("#9D4EDD"))  ' Purple for namespaces
                        
                    Case CodeNodeType.eClass
                        lColor = If(lIsDark, HexToCairoColor("#4EC9B0"), HexToCairoColor("#2B91AF"))  ' Teal/Blue for classes
                        
                    Case CodeNodeType.eInterface
                        lColor = If(lIsDark, HexToCairoColor("#B8D7A3"), HexToCairoColor("#6B8E23"))  ' Light/Dark green
                        
                    Case CodeNodeType.eModule
                        lColor = If(lIsDark, HexToCairoColor("#4CC9F0"), HexToCairoColor("#4361EE"))  ' Cyan/Blue for modules
                        
                    Case CodeNodeType.eStructure
                        lColor = If(lIsDark, HexToCairoColor("#7209B7"), HexToCairoColor("#560BAD"))  ' Deep purple for structures
                        
                    Case CodeNodeType.eEnum
                        lColor = If(lIsDark, HexToCairoColor("#F72585"), HexToCairoColor("#B5179E"))  ' Magenta for enums
                        
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction
                        lColor = If(lIsDark, HexToCairoColor("#DCDCAA"), HexToCairoColor("#795E26"))  ' Yellow/Brown for methods
                        
                    Case CodeNodeType.eProperty
                        lColor = If(lIsDark, HexToCairoColor("#9CDCFE"), HexToCairoColor("#0070C0"))  ' Light/Dark blue
                        
                    Case CodeNodeType.eField
                        lColor = If(lIsDark, HexToCairoColor("#51CF66"), HexToCairoColor("#2B8A3E"))  ' Green for fields
                        
                    Case CodeNodeType.eConstant
                        lColor = If(lIsDark, HexToCairoColor("#FFB700"), HexToCairoColor("#FF8500"))  ' Orange for constants
                        
                    Case CodeNodeType.eEvent
                        lColor = If(lIsDark, HexToCairoColor("#CE9178"), HexToCairoColor("#A31515"))  ' Orange/Red for events
                        
                    Case CodeNodeType.eDelegate
                        lColor = If(lIsDark, HexToCairoColor("#C586C0"), HexToCairoColor("#9B4F96"))  ' Light magenta
                        
                    Case CodeNodeType.eConstructor
                        lColor = If(lIsDark, HexToCairoColor("#4CC9F0"), HexToCairoColor("#4361EE"))  ' Cyan like methods
                        
                    Case CodeNodeType.eOperator
                        lColor = If(lIsDark, HexToCairoColor("#D4D4D4"), HexToCairoColor("#000000"))  ' Gray/Black
                        
                    Case CodeNodeType.eRegion
                        lColor = If(lIsDark, HexToCairoColor("#808080"), HexToCairoColor("#606060"))  ' Gray for regions
                        
                    Case Else
                        lColor = If(lIsDark, HexToCairoColor("#808080"), HexToCairoColor("#606060"))  ' Default gray
                End Select
                
                vContext.SetSourceRGB(lColor.R, lColor.G, lColor.B)
                
                ' Draw shapes based on node type for better visual distinction
                Dim lCenterX As Integer = vX + pIconSize \ 2
                Dim lCenterY As Integer = vY + pIconSize \ 2
                Dim lRadius As Integer = pIconSize \ 3
                
                Select Case vNodeType
                    Case CodeNodeType.eNamespace
                        ' Folder shape for namespaces
                        vContext.Rectangle(vX + 2, lCenterY - lRadius + 2, pIconSize - 4, lRadius * 2 - 4)
                        vContext.Fill()
                        ' Add a tab on top
                        vContext.Rectangle(vX + 2, lCenterY - lRadius - 2, (pIconSize - 4) \ 2, 4)
                        vContext.Fill()
                        
                    Case CodeNodeType.eClass, CodeNodeType.eModule
                        ' Square with rounded corners for classes/modules
                        Dim lSize As Integer = lRadius * 2 - 2
                        vContext.Rectangle(lCenterX - lRadius + 1, lCenterY - lRadius + 1, lSize, lSize)
                        vContext.Fill()
                        
                    Case CodeNodeType.eInterface
                        ' Diamond shape for interfaces
                        vContext.MoveTo(lCenterX, lCenterY - lRadius)
                        vContext.LineTo(lCenterX + lRadius, lCenterY)
                        vContext.LineTo(lCenterX, lCenterY + lRadius)
                        vContext.LineTo(lCenterX - lRadius, lCenterY)
                        vContext.ClosePath()
                        vContext.Fill()
                        
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor
                        ' Circle with small square inside for methods
                        vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                        vContext.Fill()
                        ' Add inner detail
                        vContext.SetSourceRGB(1, 1, 1)  ' White inner detail
                        vContext.Rectangle(lCenterX - 2, lCenterY - 2, 4, 4)
                        vContext.Fill()
                        vContext.SetSourceRGB(lColor.R, lColor.G, lColor.B)  ' Reset color
                        
                    Case CodeNodeType.eProperty, CodeNodeType.eField
                        ' Small square for properties/fields
                        Dim lSmallSize As Integer = lRadius * 3 \ 2
                        vContext.Rectangle(lCenterX - lSmallSize \ 2, lCenterY - lSmallSize \ 2, lSmallSize, lSmallSize)
                        vContext.Fill()
                        
                    Case CodeNodeType.eEnum
                        ' Three horizontal lines for enums
                        Dim lLineHeight As Integer = 2
                        Dim lLineSpacing As Integer = 3
                        for i As Integer = -1 To 1
                            vContext.Rectangle(vX + 3, lCenterY + i * (lLineHeight + lLineSpacing) - 1, 
                                             pIconSize - 6, lLineHeight)
                        Next
                        vContext.Fill()
                        
                    Case CodeNodeType.eEvent
                        ' Lightning bolt shape for events
                        vContext.MoveTo(lCenterX + 2, lCenterY - lRadius)
                        vContext.LineTo(lCenterX - 2, lCenterY)
                        vContext.LineTo(lCenterX, lCenterY)
                        vContext.LineTo(lCenterX - 2, lCenterY + lRadius)
                        vContext.Stroke()
                        
                    Case CodeNodeType.eStructure
                        ' Grid pattern for structures
                        vContext.Rectangle(lCenterX - lRadius, lCenterY - lRadius, lRadius * 2, lRadius * 2)
                        vContext.Fill()
                        ' Add grid lines
                        vContext.SetSourceRGB(1, 1, 1)  ' White lines
                        vContext.LineWidth = 1
                        vContext.MoveTo(lCenterX, lCenterY - lRadius)
                        vContext.LineTo(lCenterX, lCenterY + lRadius)
                        vContext.MoveTo(lCenterX - lRadius, lCenterY)
                        vContext.LineTo(lCenterX + lRadius, lCenterY)
                        vContext.Stroke()
                        
                    Case Else
                        ' Default circle for other types
                        vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                        vContext.Fill()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"DrawFallbackIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <param name="vContext">Cairo context to draw with</param>
        ''' <param name="vX">X position to draw at</param>
        ''' <param name="vY">Y position to draw at</param>
        ''' <param name="vNode">The visual node containing the text to draw</param>
        ''' <summary>
        ''' Draws node text with proper theme foreground color
        ''' </summary>
        Private Sub DrawNodeText(vContext As Cairo.Context, vNode As VisualNode, vX As Integer, vY As Integer)
            Try
                If vNode Is Nothing OrElse vNode.Node Is Nothing Then Return
                
                ' Determine text color based on visibility and hover state
                Dim lTextColor As Cairo.Color
                
                If vNode Is pHoveredNode Then
                    ' Hover color
                    lTextColor = HexToCairoColor("#FFFFFF")
                ElseIf vNode Is pSelectedNode Then
                    ' Selected color
                    lTextColor = HexToCairoColor("#FFFFFF")
                ElseIf vNode.Node.IsPrivate Then
                    ' Private member - grayed out
                    lTextColor = HexToCairoColor("#808080")
                Else
                    ' Normal text
                    lTextColor = HexToCairoColor("#D4D4D4")
                End If
                
                ' Set text color
                vContext.SetSourceRGB(lTextColor.R, lTextColor.G, lTextColor.B)
                
                ' Create text to display - just the name, no type info
                Dim lText As String = vNode.Node.Name
                
                ' Add modifiers only (no type information - that goes in tooltips)
                If vNode.Node.IsShared Then lText &= " (Shared)"
                If vNode.Node.IsPartial Then lText &= " (Partial)"
                
                ' Set font
                vContext.SelectFontFace("monospace", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal)
                vContext.SetFontSize(pFontSize)
                
                ' Calculate Y position for text (vertically centered)
                Dim lTextExtents As Cairo.TextExtents = vContext.TextExtents(lText)
                Dim lTextY As Integer = vY + (pRowHeight + lTextExtents.Height) \ 2
                
                ' Draw text
                vContext.MoveTo(vX, lTextY)
                vContext.ShowText(lText)
                
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
        ''' Draws the background with proper theme colors
        ''' </summary>
        Private Sub DrawBackground(vContext As Cairo.Context)
            Try
                ' Get current theme from settings manager
                Dim lBackgroundColor As Cairo.Color
                
                If pSettingsManager IsNot Nothing Then
                    Dim lThemeName As String = pSettingsManager.GetString("CurrentTheme", "Default Dark")
                    
                    ' Get the actual theme colors from ThemeManager
                    Dim lTheme As EditorTheme = pThemeManager?.GetTheme(lThemeName)
                    If lTheme IsNot Nothing Then
                        ' Use the theme's background color
                        lBackgroundColor = HexToCairoColor(lTheme.BackgroundColor)
                    Else
                        ' Fallback based on dark/light
                        Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                        If lIsDark Then
                            lBackgroundColor = HexToCairoColor("#252526")  ' VS Code dark
                        Else
                            lBackgroundColor = HexToCairoColor("#FFFFFF")  ' Light
                        End If
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
        ''' Initialize ThemeManager reference if not set
        ''' </summary>
        Private Sub EnsureThemeManager()
            Try
                If pThemeManager Is Nothing Then
                    Dim lGTMEA As New GetThemeManagerEventArgs
                    RaiseEvent GetThemeManager(lGTMEA)
                    pThemeManager = lGTMEA.ThemeManager
                End If
            Catch ex As Exception
                Console.WriteLine($"EnsureThemeManager error: {ex.Message}")
            End Try
        End Sub        

        Public Event GetThemeManager(vGetThemeManagerEventArgs As GetThemeManagerEventArgs)

        Public Class GetThemeManagerEventArgs
            Public ThemeManager As ThemeManager
        End Class

        ''' <summary>
        ''' Expands a specific node in the tree
        ''' </summary>
        Private Sub ExpandNode(vNode As VisualNode)
            Try
                If vNode Is Nothing OrElse Not vNode.HasChildren Then Return
                
                vNode.IsExpanded = True
                pExpandedNodes.Add(vNode.NodePath)
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ExpandNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Collapses a specific node in the tree
        ''' </summary>
        Private Sub CollapseNode(vNode As VisualNode)
            Try
                If vNode Is Nothing Then Return
                
                vNode.IsExpanded = False
                pExpandedNodes.Remove(vNode.NodePath)
                
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CollapseNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets whether private members are shown
        ''' </summary>
        Private Sub SetShowPrivateMembers(vShow As Boolean)
            Try
                pShowPrivateMembers = vShow
                pSettingsManager.SetBoolean("ObjectExplorer.ShowPrivateMembers", vShow)
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SetShowPrivateMembers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the sort mode for the object explorer
        ''' </summary>
        Private Sub SetSortMode(vMode As ObjectExplorerSortMode)
            Try
                pSortMode = vMode
                SaveSortModeSetting()
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"SetSortMode error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Main drawing handler with auto-recovery
        ''' </summary>
        Private Function OnDrawingAreaDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                'Console.WriteLine($"OnDrawingAreaDraw is ONLY called from the handler event.")
                ' Always draw background
                DrawBackground(lContext)
                
                ' CRITICAL: pVisibleNodes should NEVER be Nothing since it's initialized with New
                If pVisibleNodes Is Nothing Then
                    Console.WriteLine("ERROR: pVisibleNodes is Nothing! This should never happen!")
                    pVisibleNodes = New List(Of VisualNode)()
                End If
                
                'Console.WriteLine($"OnDrawingAreaDraw: Drawing {pVisibleNodes.Count} nodes, Root={If(pRootNode IsNot Nothing, pRootNode.Name, "Nothing")}")
                
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
                    lContext.ShowText("No items to display Now")
                    
                    ' IMPROVED: Try to recover if we should have nodes
                    If pRootNode IsNot Nothing Then
                        lContext.MoveTo(10, 50)
                        lContext.ShowText($"(Root exists: {pRootNode.Name})")
                        
                        'Console.WriteLine("Have root but no visible nodes - flagging for rebuild...")
                        pNeedsRebuild = True
                    ElseIf pLastValidRootNode IsNot Nothing AndAlso pIsProjectLoaded Then
                        ' No root but we have a last valid root - attempt recovery
                        lContext.MoveTo(10, 50)
                        lContext.ShowText("(Attempting recovery...)")
                        
                        'Console.WriteLine("No root but have last valid root - attempting recovery...")
                        Application.Invoke(Sub()
                            AttemptStructureRecovery()
                        End Sub)
                    Else 
                        Console.WriteLine("OnDrawingAreaDraw: pRootNode is Nothing, pLastValidRootNode is " + Iif((pLastValidRootNode Is Nothing), "Nothing", "Set") + ", pIsProjectLoaded = " + pIsProjectLoaded.ToString)
                    End If
                Else
                    ' We have nodes - draw them
                    'Console.WriteLine($"Drawing {pVisibleNodes.Count} nodes...")
                    
                    ' Setup clipping for viewport
                    lContext.Rectangle(0, 0, Math.Max(pViewportWidth, 100), Math.Max(pViewportHeight, 100))
                    lContext.Clip()
                    
                    ' Apply scrolling translation
                    lContext.Translate(-pScrollX, -pScrollY)
                    
                    ' Draw all visible nodes
                    DrawVisibleNodes(lContext)
                    
                    ' Draw hover highlight if needed
                    If pHoveredNode IsNot Nothing Then
                        DrawNodeHover(lContext, pHoveredNode)
                    End If
                    
                    ' Draw selection highlight if needed
                    If pSelectedNode IsNot Nothing Then
                        DrawNodeSelection(lContext, pSelectedNode)
                    End If
                End If
                
                ' Process rebuild flag after drawing completes
                If pNeedsRebuild AndAlso pRootNode IsNot Nothing Then
                    Application.Invoke(Sub()
                        pNeedsRebuild = False
                        'Console.WriteLine("Executing deferred rebuild...")
                        RebuildVisualTree()
                    End Sub)
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaDraw error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Draws selection highlight for selected node
        ''' </summary>
        ''' <param name="vContext">Cairo context for drawing</param>
        ''' <param name="vNode">The visual node to highlight</param>
        ''' <remarks>
        ''' Fixed to draw selection rectangle that properly encloses the node content
        ''' instead of spanning the full width
        ''' </remarks>
        Private Sub DrawNodeSelection(vContext As Cairo.Context, vNode As VisualNode)
            Try
                ' Get theme colors
                Dim lThemeName As String = If(pSettingsManager IsNot Nothing, pSettingsManager.GetString("CurrentTheme", "Default Dark"), "Default Dark")
                Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                
                ' Selection color
                If lIsDark Then
                    vContext.SetSourceRGBA(0.094, 0.447, 0.729, 0.3) ' Blue selection
                Else
                    vContext.SetSourceRGBA(0.0, 0.478, 1.0, 0.2)
                End If
                Dim lSelectionWidth As Integer = If(pViewportWidth > 0, pViewportWidth, pContentWidth)
                
                ' Draw selection rectangle that encloses the node content
                vContext.Rectangle(0, vNode.Y - 2, lSelectionWidth, pRowHeight + 2)
                vContext.Fill()
                
                ' Draw selection border for better visibility
                If lIsDark Then
                    vContext.SetSourceRGBA(0.094, 0.447, 0.729, 0.6) ' Darker blue for border
                Else
                    vContext.SetSourceRGBA(0.0, 0.478, 1.0, 0.4)
                End If
               
                vContext.Rectangle(0, vNode.Y - 2, lSelectionWidth, pRowHeight + 2)
                vContext.LineWidth = 1
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawNodeSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws hover highlight for hovered node
        ''' </summary>
        Private Sub DrawNodeHover(vContext As Cairo.Context, vNode As VisualNode)
            Try
                If vNode Is pSelectedNode Then Return ' Don't draw hover on selected
                
                ' Get theme colors
                Dim lThemeName As String = If(pSettingsManager IsNot Nothing, pSettingsManager.GetString("CurrentTheme", "Default Dark"), "Default Dark")
                Dim lIsDark As Boolean = lThemeName.ToLower().Contains("dark")
                
                ' Hover color
                If lIsDark Then
                    vContext.SetSourceRGBA(1.0, 1.0, 1.0, 0.05)
                Else
                    vContext.SetSourceRGBA(0.0, 0.0, 0.0, 0.05)
                End If
                Dim lSelectionWidth As Integer = If(pViewportWidth > 0, pViewportWidth, pContentWidth)
                ' Draw hover rectangle
                vContext.Rectangle(0, vNode.Y, lSelectionWidth, pRowHeight)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawNodeHover error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
