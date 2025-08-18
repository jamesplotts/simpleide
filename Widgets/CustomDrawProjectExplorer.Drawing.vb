' Widgets/CustomDrawProjectExplorer.Drawing.vb - Drawing methods for Project Explorer
' Created: 2025-08-17
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.IO
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing all drawing-related methods for the Project Explorer
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Main Drawing Event Handler =====
        
        ''' <summary>
        ''' Handles the main drawing area draw event
        ''' </summary>
        Private Function OnDrawingAreaDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                
                ' Get current theme
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                
                ' Draw background
                Dim lBgColor As Cairo.Color = HexToCairoColor(lTheme.BackgroundColor)
                lContext.SetSourceRGB(lBgColor.R, lBgColor.G, lBgColor.B)
                lContext.Paint()
                
                ' Draw visible nodes
                DrawVisibleNodes(lContext)
                
                ' Draw selection highlight
                DrawSelection(lContext)
                
                ' Draw hover highlight
                DrawHover(lContext)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnDrawingAreaDraw error: {ex.Message}")
                Return True
            End Try
        End Function
        
        ''' <summary>
        ''' Draws the corner box
        ''' </summary>
        Private Function OnCornerBoxDraw(vSender As Object, vArgs As DrawnArgs) As Boolean
            Try
                Dim lContext As Cairo.Context = vArgs.Cr
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                
                ' Draw background matching scrollbar color (FIXED: using correct property name)
                Dim lBgColor As Cairo.Color = HexToCairoColor(lTheme.LineNumberBackgroundColor)
                lContext.SetSourceRGB(lBgColor.R, lBgColor.G, lBgColor.B)
                lContext.Paint()
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"OnCornerBoxDraw error: {ex.Message}")
                Return True
            End Try
        End Function

        
        ' ===== Drawing Methods =====
        
        ''' <summary>
        ''' Draws all visible nodes in the tree
        ''' </summary>
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
        Private Sub DrawNode(vContext As Cairo.Context, vNode As VisualProjectNode)
            Try
                If vNode Is Nothing OrElse vNode.Node Is Nothing Then Return
                
                Dim lX As Integer = vNode.X - pScrollX
                Dim lY As Integer = vNode.Y - pScrollY
                
                ' Draw plus/minus if node has children
                If vNode.HasChildren Then
                    DrawPlusMinus(vContext, lX + vNode.PlusMinusRect.X, lY + vNode.PlusMinusRect.Y, vNode.IsExpanded)
                End If
                
                ' Draw icon
                DrawNodeIcon(vContext, lX + vNode.IconRect.X, lY + vNode.IconRect.Y, vNode.Node)
                
                ' Draw text
                DrawNodeText(vContext, lX + vNode.TextRect.X, lY + vNode.TextRect.Y, vNode.Node)
                
            Catch ex As Exception
                Console.WriteLine($"DrawNode error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the plus/minus expansion indicator
        ''' </summary>
        Private Sub DrawPlusMinus(vContext As Cairo.Context, vX As Integer, vY As Integer, vIsExpanded As Boolean)
            Try
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                Dim lSize As Integer = pPlusMinusSize
                Dim lHalfSize As Integer = lSize \ 2
                Dim lCenterX As Integer = vX + lHalfSize
                Dim lCenterY As Integer = vY + pRowHeight \ 2
                
                ' Draw box
                Dim lBoxColor As Cairo.Color = HexToCairoColor(lTheme.ForegroundColor)
                vContext.SetSourceRGB(lBoxColor.R * 0.6, lBoxColor.G * 0.6, lBoxColor.B * 0.6)
                vContext.Rectangle(lCenterX - lHalfSize, lCenterY - lHalfSize, lSize, lSize)
                vContext.Stroke()
                
                ' Draw minus
                vContext.MoveTo(lCenterX - lHalfSize + 2, lCenterY)
                vContext.LineTo(lCenterX + lHalfSize - 2, lCenterY)
                vContext.Stroke()
                
                ' Draw plus (vertical line) if collapsed
                If Not vIsExpanded Then
                    vContext.MoveTo(lCenterX, lCenterY - lHalfSize + 2)
                    vContext.LineTo(lCenterX, lCenterY + lHalfSize - 2)
                    vContext.Stroke()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DrawPlusMinus error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the node icon
        ''' </summary>
        Private Sub DrawNodeIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vNode As ProjectNode)
            Try
                Dim lIconName As String = GetIconNameForNode(vNode)
                
                ' Try to load icon from theme
                Dim lPixbuf As Pixbuf = Nothing
                Try
                    lPixbuf = pIconTheme.LoadIcon(lIconName, pIconSize, IconLookupFlags.UseBuiltin)
                Catch
                    ' Try fallback icon
                    Try
                        lPixbuf = pIconTheme.LoadIcon("text-x-generic", pIconSize, IconLookupFlags.UseBuiltin)
                    Catch
                        ' Draw fallback shape
                        DrawFallbackIcon(vContext, vX, vY, vNode.NodeType)
                        Return
                    End Try
                End Try
                
                If lPixbuf IsNot Nothing Then
                    ' Center icon vertically in row
                    Dim lIconY As Integer = vY + (pRowHeight - pIconSize) \ 2
                    Gdk.CairoHelper.SetSourcePixbuf(vContext, lPixbuf, vX, lIconY)
                    vContext.Paint()
                    lPixbuf.Dispose()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DrawNodeIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a fallback icon when the actual icon cannot be loaded
        ''' </summary>
        Private Sub DrawFallbackIcon(vContext As Cairo.Context, vX As Integer, vY As Integer, vNodeType As ProjectNodeType)
            Try
                Dim lCenterX As Double = vX + pIconSize / 2
                Dim lCenterY As Double = vY + pRowHeight / 2
                Dim lRadius As Double = pIconSize / 3
                
                vContext.SetSourceRGB(0.3, 0.3, 0.7)
                
                Select Case vNodeType
                    Case ProjectNodeType.eFolder, ProjectNodeType.eProject
                        ' Draw folder shape
                        vContext.Rectangle(vX + 2, lCenterY - lRadius, pIconSize - 4, lRadius * 2)
                        
                    Case ProjectNodeType.eVBFile
                        ' Draw diamond for VB files
                        vContext.MoveTo(lCenterX, lCenterY - lRadius)
                        vContext.LineTo(lCenterX + lRadius, lCenterY)
                        vContext.LineTo(lCenterX, lCenterY + lRadius)
                        vContext.LineTo(lCenterX - lRadius, lCenterY)
                        vContext.ClosePath()
                        
                    Case Else
                        ' Draw circle for other files
                        vContext.Arc(lCenterX, lCenterY, lRadius, 0, Math.PI * 2)
                End Select
                
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawFallbackIcon error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the node text
        ''' </summary>
        Private Sub DrawNodeText(vContext As Cairo.Context, vX As Integer, vY As Integer, vNode As ProjectNode)
            Try
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                
                ' Set text color
                Dim lTextColor As Cairo.Color = HexToCairoColor(lTheme.ForegroundColor)
                vContext.SetSourceRGB(lTextColor.R, lTextColor.G, lTextColor.B)
                
                ' Set font
                vContext.SelectFontFace("monospace", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal)
                vContext.SetFontSize(pFontSize)
                
                ' Get text to display
                Dim lText As String = vNode.Name
                
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
        ''' Draws selection highlight
        ''' </summary>
        Private Sub DrawSelection(vContext As Cairo.Context)
            Try
                If pSelectedNode Is Nothing Then Return
                
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                
                ' Draw selection background (FIXED: using correct property name)
                Dim lSelectionColor As Cairo.Color = HexToCairoColor(lTheme.SelectionColor)
                vContext.SetSourceRGBA(lSelectionColor.R, lSelectionColor.G, lSelectionColor.B, 0.3)
                vContext.Rectangle(pSelectedNode.X - pScrollX, pSelectedNode.Y - pScrollY, 
                                 pSelectedNode.Width, pSelectedNode.Height)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawSelection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws hover highlight
        ''' </summary>
        Private Sub DrawHover(vContext As Cairo.Context)
            Try
                If pHoveredNode Is Nothing OrElse pHoveredNode Is pSelectedNode Then Return
                
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                
                ' Draw hover background (FIXED: using correct property name)
                Dim lHoverColor As Cairo.Color = HexToCairoColor(lTheme.CurrentLineColor)
                vContext.SetSourceRGBA(lHoverColor.R, lHoverColor.G, lHoverColor.B, 0.2)
                vContext.Rectangle(pHoveredNode.X - pScrollX, pHoveredNode.Y - pScrollY, 
                                 pHoveredNode.Width, pHoveredNode.Height)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawHover error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Icon Helper Methods =====
        
        ''' <summary>
        ''' Gets the appropriate icon name for a node
        ''' </summary>
        Private Function GetIconNameForNode(vNode As ProjectNode) As String
            Try
                ' Use custom icon if specified
                If Not String.IsNullOrEmpty(vNode.IconName) Then
                    Return vNode.IconName
                End If
                
                ' Determine icon by node type
                Select Case vNode.NodeType
                    Case ProjectNodeType.eProject
                        Return "folder-remote"
                    Case ProjectNodeType.eFolder
                        Return "folder"
                    Case ProjectNodeType.eVBFile
                        Return "text-x-generic"
                    Case ProjectNodeType.eXMLFile
                        Return "text-xml"
                    Case ProjectNodeType.eConfigFile
                        Return "preferences-system"
                    Case ProjectNodeType.eResourceFile
                        Return "image-x-generic"
                    Case ProjectNodeType.eReferences
                        Return "emblem-symbolic-link"
                    Case ProjectNodeType.eManifest
                        Return "application-certificate"
                    Case ProjectNodeType.eResources
                        Return "folder-pictures"
                    Case ProjectNodeType.eMyProject
                        Return "folder-development"
                    Case Else
                        Return "text-x-generic"
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetIconNameForNode error: {ex.Message}")
                Return "text-x-generic"
            End Try
        End Function
        
        ' ===== Utility Methods =====
        
        ''' <summary>
        ''' Converts hex color string to Cairo color
        ''' </summary>
        Private Function HexToCairoColor(vHexColor As String) As Cairo.Color
            Try
                Dim lHex As String = vHexColor.TrimStart("#"c)
                If lHex.Length <> 6 Then
                    Return New Cairo.Color(0.5, 0.5, 0.5)
                End If
                
                Dim lR As Byte = Convert.ToByte(lHex.Substring(0, 2), 16)
                Dim lG As Byte = Convert.ToByte(lHex.Substring(2, 2), 16)
                Dim lB As Byte = Convert.ToByte(lHex.Substring(4, 2), 16)
                
                Return New Cairo.Color(lR / 255.0, lG / 255.0, lB / 255.0)
                
            Catch ex As Exception
                Console.WriteLine($"HexToCairoColor error: {ex.Message}")
                Return New Cairo.Color(0.5, 0.5, 0.5)
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
                Dim lFontName As String = pSettingsManager.GetString("Explorer.FontFamily", "Monospace")
                pFontDescription = FontDescription.FromString($"{lFontName} {pFontSize}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFontSettings error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies the specified scale percentage to all visual elements
        ''' </summary>
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
                
                ' Update scale display in toolbar
                UpdateScaleDisplay()
                
                ' Rebuild visual tree with new dimensions
                RebuildVisualTree()
                
                ' Invalidate drawing
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyScale error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
