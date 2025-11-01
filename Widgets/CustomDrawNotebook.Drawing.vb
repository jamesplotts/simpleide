' CustomDrawNotebook.Drawing.vb - Custom rendering for notebook tabs
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    Partial Public Class CustomDrawNotebook
        
        ' ===== Drawing Constants =====
        Private Const ICON_SIZE As Integer = 16
        Private Const CLOSE_BUTTON_SIZE As Integer = 16
        Private Const TAB_PADDING As Integer = 8
        Private Const MODIFIED_DOT_SIZE As Integer = 6
        
        ' ===== Main Drawing Method =====
        
        ''' <summary>
        ''' Handles drawing of the tab bar
        ''' </summary>
        Private Sub OnTabBarDraw(vSender As Object, vArgs As DrawnArgs)
            Try
                Dim lContext As Context = vArgs.Cr
                Dim lWidth As Integer = pTabBar.AllocatedWidth
                Dim lHeight As Integer = pTabBar.AllocatedHeight
                
                ' Calculate the drawable area (excluding buttons)
                Dim lButtonsWidth As Integer = CalculateButtonsWidth()
                Dim lDrawableWidth As Integer = Math.Max(0, lWidth - lButtonsWidth)
                
                ' Clear background
                DrawBackground(lContext, lWidth, lHeight)
                
                ' Set clipping region to prevent tabs from drawing over buttons
                lContext.Save()
                lContext.Rectangle(0, 0, lDrawableWidth, lHeight)
                lContext.Clip()
                
                ' Draw all tabs
                for i As Integer = 0 To pTabs.Count - 1
                    DrawTab(lContext, i)
                Next
                
                ' Restore context (removes clipping)
                lContext.Restore()
                
                ' Draw drop indicator if dragging
                If pIsDragging AndAlso pDropTargetIndex >= 0 Then
                    DrawDropIndicator(lContext, pDropTargetIndex)
                End If
                
                vArgs.RetVal = True
                
            Catch ex As Exception
                Console.WriteLine($"OnTabBarDraw error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Background Drawing =====
        
        ''' <summary>
        ''' Draws the tab bar background
        ''' </summary>
        Private Sub DrawBackground(vContext As Context, vWidth As Integer, vHeight As Integer)
            Try
                ' Fill background
                vContext.Rectangle(0, 0, vWidth, vHeight)
                SetSourceColor(vContext, pThemeColors.Background)
                vContext.Fill()
                
                ' Draw bottom border
                vContext.MoveTo(0, vHeight - 0.5)
                vContext.LineTo(vWidth, vHeight - 0.5)
                SetSourceColor(vContext, pThemeColors.Border)
                vContext.LineWidth = 1
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawBackground error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Tab Drawing =====
        
        ''' <summary>
        ''' Draws a single tab
        ''' </summary>
        Private Sub DrawTab(vContext As Context, vIndex As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pTabs.Count Then Return
                
                Dim lTab As TabData = pTabs(vIndex)
                If Not lTab.IsVisible Then Return
        
                Dim lBounds As Cairo.Rectangle = lTab.Bounds
                
                ' Adjust for scroll offset
                Dim lX As Integer = lBounds.X - pScrollOffset
                
                ' Skip if completely off-screen
                If lX + lBounds.Width < 0 OrElse lX > pTabBar.AllocatedWidth Then
                    Return
                End If
                
                ' Save context state
                vContext.Save()
                
                ' Clip to visible area to prevent overflow
                vContext.Rectangle(0, 0, pTabBar.AllocatedWidth, pTabBar.AllocatedHeight)
                vContext.Clip()
                
                ' Determine tab state
                Dim lIsActive As Boolean = (vIndex = pCurrentTabIndex)
                Dim lIsHovered As Boolean = (vIndex = pHoveredTabIndex)
                Dim lIsDragging As Boolean = (vIndex = pDraggedTabIndex AndAlso pIsDragging)
                
                ' Create adjusted bounds for drawing
                Dim lDrawBounds As New Cairo.Rectangle(lX, lBounds.Y, lBounds.Width, lBounds.Height)
                
                ' Draw tab background
                DrawTabBackground(vContext, lDrawBounds, lIsActive, lIsHovered, lIsDragging)
                
                ' Draw tab content
                Dim lContentX As Integer = lX + TAB_PADDING
                Dim lContentY As Integer = lBounds.Y + (lBounds.Height - ICON_SIZE) \ 2
                
                ' Draw icon if present
                If Not String.IsNullOrEmpty(lTab.IconName) Then
                    DrawTabIcon(vContext, lTab.IconName, lContentX, lContentY)
                    lContentX += ICON_SIZE + 4
                End If
                
                ' Draw modified indicator
                If lTab.Modified Then
                    DrawModifiedIndicator(vContext, lContentX, 
                                        lBounds.Y + (lBounds.Height - MODIFIED_DOT_SIZE) \ 2)
                    lContentX += MODIFIED_DOT_SIZE + 4
                End If
                
                ' Calculate available width for label
                ' CRITICAL: Give label as much space as possible
                Dim lCloseButtonSpace As Integer = If(pShowCloseButtons, CLOSE_BUTTON_SIZE + 8, 0)
                Dim lMaxLabelWidth As Integer = lBounds.Width - (lContentX - lX) - lCloseButtonSpace - TAB_PADDING
                
                ' Draw label
                DrawTabLabel(vContext, lTab.Label, lContentX, lBounds.Y, 
                           lMaxLabelWidth, lBounds.Height, lIsActive)
                
                ' Update close button bounds with adjusted position
                lTab.CloseBounds = New Cairo.Rectangle(
                    lBounds.X + lBounds.Width - CLOSE_BUTTON_SIZE - TAB_PADDING,
                    lBounds.Y + (lBounds.Height - CLOSE_BUTTON_SIZE) \ 2,
                    CLOSE_BUTTON_SIZE,
                    CLOSE_BUTTON_SIZE
                )
                
                ' Draw close button ONLY if ShowCloseButtons is True
                If pShowCloseButtons Then
                    ' Update close button bounds
                    lTab.CloseBounds = New Cairo.Rectangle(
                        lBounds.X + lBounds.Width - CLOSE_BUTTON_SIZE - TAB_PADDING,
                        lBounds.Y + (lBounds.Height - CLOSE_BUTTON_SIZE) \ 2,
                        CLOSE_BUTTON_SIZE,
                        CLOSE_BUTTON_SIZE
                    )
                    
                    ' Draw close button
                    Dim lCloseX As Integer = lX + lBounds.Width - CLOSE_BUTTON_SIZE - TAB_PADDING
                    Dim lCloseY As Integer = lBounds.Y + (lBounds.Height - CLOSE_BUTTON_SIZE) \ 2
                    Dim lCloseHovered As Boolean = (vIndex = pHoveredCloseIndex)
                    DrawCloseButton(vContext, lCloseX, lCloseY, lCloseHovered)
                Else
                    ' Clear close bounds when close buttons are hidden
                    lTab.CloseBounds = New Cairo.Rectangle(0, 0, 0, 0)
                End If    
                            
                ' Restore context state
                vContext.Restore()
                
            Catch ex As Exception
                Console.WriteLine($"DrawTab error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the bounds for all tabs based on their content
        ''' </summary>
        Private Sub UpdateTabBounds()
            Try
                Dim lX As Integer = 0
                
                for i As Integer = 0 To pTabs.Count - 1
                    Dim lTab As TabData = pTabs(i)
                    
                    If lTab.IsVisible Then
                        ' Calculate the required width for this tab
                        Dim lWidth As Integer = CalculateTabWidth(i)
                        
                        ' Set tab bounds with calculated width
                        lTab.Bounds = New Cairo.Rectangle(lX, 0, lWidth, pTabHeight)
                        
                        ' Move X position for next tab
                        lX += lWidth
                    Else
                        ' Hidden tab has zero bounds
                        lTab.Bounds = New Cairo.Rectangle(0, 0, 0, 0)
                    End If
                Next
                
                ' Update total tabs width for scrolling
                pTotalTabsWidth = lX
                
                ' DO NOT CALL UpdateNavigationButtons here - it will cause circular dependency!
                ' Navigation buttons will be updated separately when needed
                
                ' Force redraw
                If pTabBar IsNot Nothing Then
                    pTabBar.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateTabBounds error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Forces recalculation of all tab widths and positions
        ''' </summary>
        ''' <remarks>
        ''' Call this when theme changes or font size changes
        ''' </remarks>
        Public Sub RecalculateTabSizes()
            Try
                UpdateTabBounds()
                
                ' Ensure current tab is visible
                If pCurrentTabIndex >= 0 AndAlso pCurrentTabIndex < pTabs.Count Then
                    EnsureTabVisible(pCurrentTabIndex)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RecalculateTabSizes error: {ex.Message}")
            End Try
        End Sub

        
        ' Replace: SimpleIDE.Widgets.CustomDrawNotebook.DrawTabBackground
        ''' <summary>
        ''' Draws the tab background with Manila folder-style curved edges
        ''' </summary>
        Private Sub DrawTabBackground(vContext As Context, vBounds As Cairo.Rectangle, vIsActive As Boolean, vIsHovered As Boolean, vIsDragging As Boolean)
            Try
                ' CRITICAL FIX: Don't subtract pScrollOffset here - it's already been applied in DrawTab
                Dim lX As Double = vBounds.X
                Dim lY As Double = vBounds.Y
                Dim lWidth As Double = vBounds.Width
                Dim lHeight As Double = vBounds.Height
                
                ' Adjust Y position for acctive tab (lift it up slightly)
                If vIsActive Then
                    lY -= 2
                    lHeight += 2
                End If
                
                ' Save context state
                vContext.Save()
                
                ' Create Manila folder-style tab path
                vContext.NewPath()
                
                ' Define curve parameters
                Dim lTopRadius As Double = 6.0  ' Radius for top corners
                Dim lSlopeWidth As Double = 12.0  ' Width of the angled/curved section
                
                ' Start from bottom-left
                vContext.MoveTo(lX, lY + lHeight)
                
                ' Left side - vertical line up, then curve at top-left
                vContext.LineTo(lX, lY + lTopRadius)
                vContext.Arc(lX + lTopRadius, lY + lTopRadius, lTopRadius, Math.PI, Math.PI * 1.5)
                
                ' Top edge - mostly straight
                vContext.LineTo(lX + lWidth - lSlopeWidth - lTopRadius, lY)
                
                ' Manila folder curve on right side
                ' This creates the characteristic angled tab edge
                Dim lControlX1 As Double = lX + lWidth - lSlopeWidth * 0.5
                Dim lControlY1 As Double = lY
                Dim lControlX2 As Double = lX + lWidth - lSlopeWidth * 0.3
                Dim lControlY2 As Double = lY + lHeight * 0.3
                Dim lEndX As Double = lX + lWidth
                Dim lEndY As Double = lY + lHeight
                
                ' Create smooth curve for Manila folder edge
                vContext.CurveTo(lControlX1, lControlY1, lControlX2, lControlY2, lEndX, lEndY)
                
                ' Close the path (bottom edge)
                vContext.ClosePath()
                
                ' Fill the tab background
                If vIsActive Then
                    ' Active tab gets the editor background color
                    SetSourceColor(vContext, pThemeColors.EditorBackground)
                ElseIf vIsHovered Then
                    ' Hovered tab gets a lighter shade
                    SetSourceColor(vContext, pThemeColors.TabHover)
                ElseIf vIsDragging Then
                    ' Dragging tab gets accent color with transparency
                    Dim lDragColor As Gdk.RGBA = pThemeColors.Accent
                    lDragColor.Alpha = 0.7
                    SetSourceColor(vContext, lDragColor)
                Else
                    ' Inactive tab
                    SetSourceColor(vContext, pThemeColors.TabInactive)
                End If
                
                vContext.FillPreserve()
                
                ' Draw the border
                vContext.LineWidth = 1.0
                
                If vIsActive Then
                    ' Active tab border matches the editor border but no bottom border
                    SetSourceColor(vContext, pThemeColors.Border)
                    vContext.Stroke()
                    
                    ' Cover the bottom border for active tab to connect with content
                    vContext.Rectangle(lX + 1, lY + lHeight - 1, lWidth - 2, 3)
                    SetSourceColor(vContext, pThemeColors.EditorBackground)
                    vContext.Fill()
                Else
                    ' Inactive tabs get subtle border
                    Dim lBorderColor As Gdk.RGBA = pThemeColors.Border
                    lBorderColor.Alpha = 0.5
                    SetSourceColor(vContext, lBorderColor)
                    vContext.Stroke()
                End If
                
                ' Add subtle gradient overlay for depth (optional)
                If vIsActive OrElse vIsHovered Then
                    Dim lGradient As New Cairo.LinearGradient(lX, lY, lX, lY + lHeight * 0.5)
                    lGradient.AddColorStop(0, New Cairo.Color(1, 1, 1, 0.1))  ' Slight highlight at top
                    lGradient.AddColorStop(1, New Cairo.Color(0, 0, 0, 0))     ' Fade to transparent
                    
                    vContext.NewPath()
                    ' Recreate the same path for gradient
                    vContext.MoveTo(lX, lY + lHeight)
                    vContext.LineTo(lX, lY + lTopRadius)
                    vContext.Arc(lX + lTopRadius, lY + lTopRadius, lTopRadius, Math.PI, Math.PI * 1.5)
                    vContext.LineTo(lX + lWidth - lSlopeWidth - lTopRadius, lY)
                    vContext.CurveTo(lControlX1, lControlY1, lControlX2, lControlY2, lEndX, lEndY)
                    vContext.ClosePath()
                    
                    vContext.SetSource(lGradient)
                    vContext.Fill()
                    
                    lGradient.Dispose()
                End If
                
                ' Restore context state
                vContext.Restore()
                
            Catch ex As Exception
                Console.WriteLine($"DrawTabBackground error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a tab icon
        ''' </summary>
        ''' <param name="vContext">Cairo context</param>
        ''' <param name="vIconName">Name of the icon to draw</param>
        ''' <param name="vX">X position</param>
        ''' <param name="vY">Y position</param>
        Private Sub DrawTabIcon(vContext As Context, vIconName As String, vX As Integer, vY As Integer)
            Try
                If String.IsNullOrEmpty(vIconName) Then Return
                
                ' Get the icon from the theme
                Dim lIconTheme As IconTheme = IconTheme.Default
                Dim lPixbuf As Pixbuf = Nothing
                
                Try
                    lPixbuf = lIconTheme.LoadIcon(vIconName, ICON_SIZE, IconLookupFlags.ForceSvg)
                Catch
                    ' Try as a generic icon if specific one not found
                    Try
                        lPixbuf = lIconTheme.LoadIcon("text-x-generic", ICON_SIZE, IconLookupFlags.ForceSvg)
                    Catch
                        ' Give up on icon
                        Return
                    End Try
                End Try
                
                If lPixbuf IsNot Nothing Then
                    ' Draw the icon
                    vContext.Save()
                    Gdk.CairoHelper.SetSourcePixbuf(vContext, lPixbuf, vX, vY)
                    vContext.Paint()
                    vContext.Restore()
                    
                    ' Dispose the pixbuf
                    lPixbuf.Dispose()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DrawIcon error: {ex.Message}")
            End Try
        End Sub        
        ''' <summary>
        ''' Draws the modified indicator (red dot)
        ''' </summary>
        Private Sub DrawModifiedIndicator(vContext As Context, vX As Integer, vY As Integer)
            Try
                vContext.Arc(vX + MODIFIED_DOT_SIZE \ 2, vY + MODIFIED_DOT_SIZE \ 2, 
                           MODIFIED_DOT_SIZE \ 2, 0, Math.PI * 2)
                SetSourceColor(vContext, pThemeColors.ModifiedIndicator)
                vContext.Fill()
                
            Catch ex As Exception
                Console.WriteLine($"DrawModifiedIndicator error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the tab label text with full Pango markup support
        ''' </summary>
        ''' <param name="vContext">Cairo context</param>
        ''' <param name="vText">Tab label text (may contain Pango markup)</param>
        ''' <param name="vX">X position</param>
        ''' <param name="vY">Y position</param>
        ''' <param name="vMaxWidth">Maximum width for the label</param>
        ''' <param name="vHeight">Height of the tab</param>
        ''' <param name="vIsActive">Whether this is the active tab</param>
        Private Sub DrawTabLabel(vContext As Context, vText As String, vX As Integer, vY As Integer, 
                                vMaxWidth As Integer, vHeight As Integer, vIsActive As Boolean)
            Try
                ' Create Pango layout for text
                Dim lLayout As Pango.Layout = pTabBar.CreatePangoLayout(Nothing)
                
                ' Check if the text contains markup by looking for common markup tags
                Dim lHasMarkup As Boolean = vText.Contains("<") AndAlso vText.Contains(">")
                
                If lHasMarkup Then
                    ' Parse the markup - this allows color and other formatting
                    Try
                        lLayout.SetMarkup(vText)
                    Catch ex As Exception
                        ' If markup parsing fails, fall back to plain text
                        Console.WriteLine($"DrawTabLabel: Markup parsing failed - {ex.Message}")
                        lLayout.SetText(vText)
                        lHasMarkup = False ' Reset flag since we fell back to plain text
                    End Try
                Else
                    ' Plain text - no markup
                    lLayout.SetText(vText)
                End If
                
                ' Set font with BOLD for active tab
                Dim lFontDesc As New Pango.FontDescription()
                lFontDesc.Family = "Sans"
                lFontDesc.Size = Pango.Units.FromPixels(10)
                
                ' ALWAYS make active tab BOLD
                If vIsActive Then
                    lFontDesc.Weight = Pango.Weight.Bold
                Else
                    lFontDesc.Weight = Pango.Weight.Normal
                End If
                
                lLayout.FontDescription = lFontDesc
                
                ' CRITICAL: NO TRUNCATION - Remove ellipsize and width constraints
                ' We want the full text to be displayed
                lLayout.Width = -1  ' No width limit
                lLayout.Ellipsize = Pango.EllipsizeMode.None  ' No ellipsizing
                
                ' Get text dimensions
                Dim lWidth, lHeight As Integer
                lLayout.GetPixelSize(lWidth, lHeight)
                
                ' Calculate Y position to vertically center text
                Dim lYCentered As Integer = vY + (vHeight - lHeight) \ 2
                
                ' Move to text position
                vContext.MoveTo(vX, lYCentered)
                
                ' Set text color based on active state
                If vIsActive Then
                    SetSourceColor(vContext, pThemeColors.Text)
                Else
                    SetSourceColor(vContext, pThemeColors.TextInactive)
                End If
                
                ' Draw the text using Pango
                Pango.CairoHelper.ShowLayout(vContext, lLayout)
                
            Catch ex As Exception
                Console.WriteLine($"DrawTabLabel error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws a close button
        ''' </summary>
        Private Sub DrawCloseButton(vContext As Context, vX As Integer, vY As Integer, vIsHovered As Boolean)
            Try
                ' Draw close button background if hovered
                If vIsHovered Then
                    vContext.SetSourceRGBA(1.0, 0.3, 0.3, 0.2)
                    vContext.Arc(vX + CLOSE_BUTTON_SIZE \ 2, vY + CLOSE_BUTTON_SIZE \ 2, 
                                CLOSE_BUTTON_SIZE \ 2, 0, Math.PI * 2)
                    vContext.Fill()
                End If
                
                ' Draw X
                vContext.LineWidth = 1.5
                If vIsHovered Then
                    vContext.SetSourceRGBA(1.0, 0.3, 0.3, 1.0)
                Else
                    vContext.SetSourceRGBA(0.5, 0.5, 0.5, 0.8)
                End If
                
                Dim lPadding As Integer = 4
                vContext.MoveTo(vX + lPadding, vY + lPadding)
                vContext.LineTo(vX + CLOSE_BUTTON_SIZE - lPadding, vY + CLOSE_BUTTON_SIZE - lPadding)
                vContext.Stroke()
                
                vContext.MoveTo(vX + CLOSE_BUTTON_SIZE - lPadding, vY + lPadding)
                vContext.LineTo(vX + lPadding, vY + CLOSE_BUTTON_SIZE - lPadding)
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawCloseButton error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Draws the drop indicator for drag and drop
        ''' </summary>
        Private Sub DrawDropIndicator(vContext As Context, vTargetIndex As Integer)
            Try
                If vTargetIndex < 0 OrElse vTargetIndex > pTabs.Count Then Return
                
                Dim lX As Double
                If vTargetIndex >= pTabs.Count Then
                    ' Drop at end
                    If pTabs.Count > 0 Then
                        lX = pTabs(pTabs.Count - 1).Bounds.X + pTabs(pTabs.Count - 1).Bounds.Width
                    Else
                        lX = 0
                    End If
                Else
                    lX = pTabs(vTargetIndex).Bounds.X
                End If
                
                lX -= pScrollOffset
                
                ' Draw insertion line
                vContext.LineWidth = 2
                SetSourceColor(vContext, pThemeColors.ModifiedIndicator)
                vContext.MoveTo(lX, 2)
                vContext.LineTo(lX, pTabHeight - 2)
                vContext.Stroke()
                
            Catch ex As Exception
                Console.WriteLine($"DrawDropIndicator error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Sets the source color for Cairo context
        ''' </summary>
        Private Sub SetSourceColor(vContext As Context, vColor As Gdk.RGBA, Optional vAlpha As Double = -1)
            Try
                If vAlpha >= 0 Then
                    vContext.SetSourceRGBA(vColor.Red, vColor.Green, vColor.Blue, vAlpha)
                Else
                    vContext.SetSourceRGBA(vColor.Red, vColor.Green, vColor.Blue, vColor.Alpha)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetSourceColor error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace