' WelcomeTabWidget.vb
' Custom-drawn welcome tab that doesn't rely on GTK layouts (mostly - see pDrawingArea)

Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Managers

Namespace Widgets

    ''' <summary>
    ''' A custom-drawn welcome tab widget that displays version info, shortcuts, and recent files.
    ''' Inherits Fixed (not DrawingArea directly) so the New Project/Open Project/Open File
    ''' actions can be real CustomDrawButton widgets, positioned to track the layout this
    ''' class already computes for its Cairo-painted content (penguin image, description,
    ''' keyboard shortcuts, recent files, scrollbar) on an internal pDrawingArea child
    ''' </summary>
    Public Class WelcomeTabWidget
        Inherits Fixed

        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when New Project button is clicked
        ''' </summary>
        Public Event NewProjectClicked As EventHandler
        
        ''' <summary>
        ''' Raised when Open Project button is clicked
        ''' </summary>
        Public Event OpenProjectClicked As EventHandler
        
        ''' <summary>
        ''' Raised when Open File button is clicked
        ''' </summary>
        Public Event OpenFileClicked As EventHandler
        
        ''' <summary>
        ''' Raised when a recent file is clicked
        ''' </summary>
        Public Event RecentFileClicked As EventHandler(Of String)

        ''' <summary>
        ''' Raised when a hyperlink in the description (e.g. the GitHub URL) is clicked
        ''' </summary>
        Public Event LinkClicked As EventHandler(Of String)

        ' ===== Private Fields =====

        Private pVersionInfo As String = "SimpleIDE v1.0"
        Private pRecentFiles As New List(Of String)()
        Private pThemeColors As WelcomeThemeColors
        Private pPressedButton As String = Nothing
        Private pThemeManager As ThemeManager

        ' The Cairo-painted content (everything except the three action buttons below)
        Private pDrawingArea As DrawingArea

        ' Real CustomDrawButton widgets for the three Quick Start actions - positioned via
        ' Move() from DrawQuickStart to track the same layout math used to paint everything
        ' above them, rather than duplicating that computation in a second place
        Private pNewProjectButton As CustomDrawButton
        Private pOpenProjectButton As CustomDrawButton
        Private pOpenFileButton As CustomDrawButton

        ' Layout constants
        Private Shadows Const MARGIN As Integer = 40
        Private Const COLUMN_SPACING As Integer = 60
        Private Const SECTION_SPACING As Integer = 30
        Private Const BUTTON_HEIGHT As Integer = 40
        Private Const BUTTON_WIDTH As Integer = 200
        Private Const LINE_HEIGHT As Integer = 24
        Private Const TITLE_SIZE As Double = 24
        Private Const HEADING_SIZE As Double = 16
        Private Const NORMAL_SIZE As Double = 14

        ' Hit-test rectangles for content that's still Cairo-painted (in content/unscrolled
        ' coordinates - callers must add/subtract pTopOffset as the existing code already did)
        Private pRecentFileRects As New List(Of Cairo.Rectangle)()
        Private pLinkRects As New List(Of Cairo.Rectangle)()
        Private pLinkUrls As New List(Of String)()

        Private pLastMouseX As Double
        Private pLastMouseY As Double

        Private pPenguinPixbuf As Gdk.Pixbuf = Nothing
        Private pIdeDescription As New List(Of String)

        Private pTopOffset As Integer = 0
        Private pContentHeight As Integer = 0
        Private pViewportHeight As Integer = 0
        Private pScrollbarWidth As Integer = 12
        Private pScrollbarVisible As Boolean = False
        Private pScrollbarRect As Cairo.Rectangle
        Private pScrollThumbRect As Cairo.Rectangle
        Private pIsDraggingScrollbar As Boolean = False
        Private pScrollDragStartY As Double = 0
        Private pScrollDragStartOffset As Integer = 0

        ' ===== Public Properties =====

        ''' <summary>
        ''' Gets or sets the ThemeManager used to theme the three CustomDrawButton actions
        ''' </summary>
        Public Property ThemeManager As ThemeManager
            Get
                Return pThemeManager
            End Get
            Set(value As ThemeManager)
                pThemeManager = value
                pNewProjectButton.ThemeManager = value
                pOpenProjectButton.ThemeManager = value
                pOpenFileButton.ThemeManager = value
            End Set
        End Property

        ' ===== Constructor =====

        ''' <summary>
        ''' Initializes a new instance of the WelcomeTabWidget class
        ''' </summary>
        Public Sub New()
            ' Deliberately no SetSizeRequest here - this widget already handles being given
            ' less than its natural content size via OnSizeAllocated's scrolling logic
            ' (pViewportHeight/pContentHeight/pScrollbarVisible below). A hard minimum here
            ' previously propagated up through the editor notebook (packed into the center
            ' Paned with shrink:=False, so it always gets at least its minimum) and, once the
            ' bottom panel was made shrinkable to fix it forcing the whole window to grow,
            ' left the bottom panel with nothing - it was being squeezed to fit around this
            ' widget's forced 800x600 floor whenever the Welcome tab was the only open tab.

            ' Initialize theme colors
            InitializeThemeColors()

            ' The Cairo-painted background/content, filling this Fixed's whole area
            pDrawingArea = New DrawingArea()
            pDrawingArea.CanFocus = True
            pDrawingArea.Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                    EventMask.PointerMotionMask Or EventMask.LeaveNotifyMask Or
                    EventMask.EnterNotifyMask Or EventMask.ExposureMask Or
                    EventMask.ScrollMask
            AddHandler pDrawingArea.Drawn, AddressOf OnDraw
            AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnButtonPress
            AddHandler pDrawingArea.ButtonReleaseEvent, AddressOf OnButtonRelease
            AddHandler pDrawingArea.MotionNotifyEvent, AddressOf OnMotionNotify
            AddHandler pDrawingArea.LeaveNotifyEvent, AddressOf OnLeaveNotify
            AddHandler pDrawingArea.ScrollEvent, AddressOf OnScrollEvent
            Put(pDrawingArea, 0, 0)

            AddHandler Me.SizeAllocated, AddressOf OnSizeAllocated

            ' The three Quick Start action buttons - repositioned each draw to track the
            ' Cairo layout computed in DrawQuickStart; initial position doesn't matter
            pNewProjectButton = New CustomDrawButton("📁 New Project")
            pNewProjectButton.SetSizeRequest(BUTTON_WIDTH, BUTTON_HEIGHT)
            AddHandler pNewProjectButton.Clicked, Sub(vSender As Object, vArgs As EventArgs) RaiseEvent NewProjectClicked(Me, EventArgs.Empty)
            Put(pNewProjectButton, 0, 0)

            pOpenProjectButton = New CustomDrawButton("📂 Open Project")
            pOpenProjectButton.SetSizeRequest(BUTTON_WIDTH, BUTTON_HEIGHT)
            AddHandler pOpenProjectButton.Clicked, Sub(vSender As Object, vArgs As EventArgs) RaiseEvent OpenProjectClicked(Me, EventArgs.Empty)
            Put(pOpenProjectButton, 0, 0)

            pOpenFileButton = New CustomDrawButton("📄 Open File")
            pOpenFileButton.SetSizeRequest(BUTTON_WIDTH, BUTTON_HEIGHT)
            AddHandler pOpenFileButton.Clicked, Sub(vSender As Object, vArgs As EventArgs) RaiseEvent OpenFileClicked(Me, EventArgs.Empty)
            Put(pOpenFileButton, 0, 0)

            ' Load the penguin image
            LoadPenguinImage()
            pIDEDescription = New List(Of String)
        End Sub
        
        ' ===== Public Properties =====
        
        ''' <summary>
        ''' Gets or sets the version information to display
        ''' </summary>
        Public Property VersionInfo As String
            Get
                Return pVersionInfo
            End Get
            Set(value As String)
                pVersionInfo = value
                QueueDraw()
            End Set
        End Property
        
        ' ===== Public Methods =====

        ''' <summary>
        ''' Forces a check of scrollbar visibility (call after adding to notebook)
        ''' </summary>
        Public Sub CheckScrollbarVisibility()
            Try
                ' Get current allocation
                Dim lAllocation As Gdk.Rectangle = Allocation
                pViewportHeight = lAllocation.Height
                
                ' Calculate content height
                pContentHeight = CalculateContentHeight()
                
                ' Determine if scrollbar is needed
                pScrollbarVisible = (pContentHeight > pViewportHeight)
                
                ' Reset scroll position if no scrollbar needed
                If Not pScrollbarVisible Then
                    pTopOffset = 0
                End If
                
                ' Force a redraw
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CheckScrollbarVisibility error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the list of recent files to display
        ''' </summary>
        ''' <param name="vRecentFiles">List of recent file paths</param>
        Public Sub SetRecentFiles(vRecentFiles As List(Of String))
            pRecentFiles.Clear()
            If vRecentFiles IsNot Nothing Then
                pRecentFiles.AddRange(vRecentFiles)
            End If
            QueueDraw()
        End Sub
        
        ''' <summary>
        ''' Updates the theme colors for the welcome tab
        ''' </summary>
        ''' <param name="vIsDarkTheme">True if using dark theme</param>
        Public Sub UpdateTheme(vIsDarkTheme As Boolean)
            If vIsDarkTheme Then
                ' Dark theme colors
                pThemeColors.Background = New Cairo.Color(0.15, 0.15, 0.15)
                pThemeColors.Text = New Cairo.Color(0.9, 0.9, 0.9)
                pThemeColors.Heading = New Cairo.Color(1, 1, 1)
                pThemeColors.Button = New Cairo.Color(0.25, 0.25, 0.25)
                pThemeColors.ButtonHover = New Cairo.Color(0.35, 0.35, 0.35)
                pThemeColors.ButtonPressed = New Cairo.Color(0.2, 0.2, 0.2)
                pThemeColors.ButtonText = New Cairo.Color(1, 1, 1)
                pThemeColors.Accent = New Cairo.Color(0.2, 0.5, 0.8)
                pThemeColors.Link = New Cairo.Color(0.4, 0.7, 1)
                pThemeColors.LinkHover = New Cairo.Color(0.5, 0.8, 1)
                pThemeColors.Separator = New Cairo.Color(0.3, 0.3, 0.32)  
            Else
                ' Light theme colors
                pThemeColors.Background = New Cairo.Color(0.98, 0.98, 0.98)
                pThemeColors.Text = New Cairo.Color(0.2, 0.2, 0.2)
                pThemeColors.Heading = New Cairo.Color(0, 0, 0)
                pThemeColors.Button = New Cairo.Color(0.9, 0.9, 0.9)
                pThemeColors.ButtonHover = New Cairo.Color(0.85, 0.85, 0.85)
                pThemeColors.ButtonPressed = New Cairo.Color(0.8, 0.8, 0.8)
                pThemeColors.ButtonText = New Cairo.Color(0, 0, 0)
                pThemeColors.Accent = New Cairo.Color(0.2, 0.5, 0.8)
                pThemeColors.Link = New Cairo.Color(0.1, 0.4, 0.7)
                pThemeColors.LinkHover = New Cairo.Color(0.2, 0.5, 0.8)
                pThemeColors.Separator = New Cairo.Color(0.8, 0.8, 0.8) 
            End If
            QueueDraw()
        End Sub
        
        ' ===== Private Methods - Initialization =====
        
        ''' <summary>
        ''' Initializes the default theme colors
        ''' </summary>
        Private Sub InitializeThemeColors()
            pThemeColors = New WelcomeThemeColors()
            UpdateTheme(True) ' Default to dark theme
        End Sub
        
        ' ===== Private Methods - Drawing =====
        
        ''' <summary>
        ''' Main drawing handler
        ''' </summary>
        Private Sub OnDraw(vSender As Object, vArgs As DrawnArgs)
            Dim lContext As Context = vArgs.Cr
            Dim lWidth As Integer = AllocatedWidth
            Dim lHeight As Integer = AllocatedHeight
            
            pViewportHeight = lHeight
            
            ' Clear button rectangles for new frame
            pRecentFileRects.Clear()
            
            ' Fill background
            lContext.SetSourceRGB(pThemeColors.Background.R, pThemeColors.Background.G, pThemeColors.Background.B)
            lContext.Paint()
            
            ' Save context for clipping
            lContext.Save()
            
            ' Determine if we need scrollbar
            pContentHeight = CalculateContentHeight()
            pScrollbarVisible = (pContentHeight > pViewportHeight)
            
            ' Adjust drawing width if scrollbar is visible
            Dim lDrawWidth As Integer = If(pScrollbarVisible, lWidth - pScrollbarWidth - 5, lWidth)
            
            ' Set clipping region to exclude scrollbar area
            lContext.Rectangle(0, 0, lDrawWidth, lHeight)
            lContext.Clip()
            
            ' Apply scroll offset
            lContext.Translate(0, -pTopOffset)
            
            ' Calculate column positions
            Dim lLeftColumnX As Integer = MARGIN
            Dim lRightColumnX As Integer = lDrawWidth \ 2 + COLUMN_SPACING \ 2
            Dim lColumnWidth As Integer = (lDrawWidth - 2 * MARGIN - COLUMN_SPACING) \ 2
            
            ' Draw left column
            Dim lY As Integer = MARGIN
            lY = DrawTitle(lContext, lLeftColumnX, lY, pVersionInfo)
            lY += SECTION_SPACING
            lY = DrawQuickStart(lContext, lLeftColumnX, lY, lColumnWidth)
            
            ' Draw vertical separator
            lContext.SetSourceRGBA(pThemeColors.Separator.R, pThemeColors.Separator.G, pThemeColors.Separator.B, 0.3)
            lContext.LineWidth = 1.0
            lContext.MoveTo(lDrawWidth \ 2, MARGIN)
            lContext.LineTo(lDrawWidth \ 2, Math.Max(lY + MARGIN, pContentHeight))
            lContext.Stroke()
            
            ' Draw right column
            lY = MARGIN + 20
            lY = DrawKeyboardShortcuts(lContext, lRightColumnX, lY, lColumnWidth)
            lY += SECTION_SPACING
            lY = DrawRecentFiles(lContext, lRightColumnX, lY, lColumnWidth)
            
            ' Restore context (removes clipping and translation)
            lContext.Restore()
            
            ' Draw scrollbar if needed
            If pScrollbarVisible Then
                DrawScrollbar(lContext, lWidth, lHeight)
            End If
            
            vArgs.RetVal = True
        End Sub
        
        ''' <summary>
        ''' Draws the title/version section with penguin image and wrapped description
        ''' </summary>
        Private Function DrawTitle(vContext As Context, vX As Integer, vY As Integer, vTitle As String) As Integer
            Dim lCurrentY As Integer = vY
            
            ' Draw the penguin image if loaded
            If pPenguinPixbuf IsNot Nothing Then
                Try
                    ' Save the current context state
                    vContext.Save()
                    
                    ' Center the penguin horizontally in the column
                    Dim lImageX As Integer = vX + 50
                    
                    ' Draw the pixbuf
                    Gdk.CairoHelper.SetSourcePixbuf(vContext, pPenguinPixbuf, lImageX, lCurrentY)
                    vContext.Paint()
                    
                    ' Restore context
                    vContext.Restore()
                    
                    lCurrentY += 128 + 20 ' Image height + spacing
                Catch ex As Exception
                    Console.WriteLine($"Error drawing penguin: {ex.Message}")
                End Try
            End If
            
            ' Draw the main title
            vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold)
            vContext.SetFontSize(TITLE_SIZE)
            vContext.SetSourceRGB(pThemeColors.Heading.R, pThemeColors.Heading.G, pThemeColors.Heading.B)
            vContext.MoveTo(vX, lCurrentY + TITLE_SIZE)
            vContext.ShowText("Welcome to SimpleIDE")
            
            lCurrentY += CInt(TITLE_SIZE) + 10
            
            ' Draw version info
            vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal)
            vContext.SetFontSize(HEADING_SIZE)
            vContext.SetSourceRGB(pThemeColors.Text.R, pThemeColors.Text.G, pThemeColors.Text.B)
            vContext.MoveTo(vX, lCurrentY + HEADING_SIZE)
            vContext.ShowText(pVersionInfo)
            
            lCurrentY += CInt(HEADING_SIZE) + 10
            
            ' Draw IDE description with word wrapping
            vContext.SetFontSize(NORMAL_SIZE)
            vContext.SetSourceRGBA(pThemeColors.Text.R, pThemeColors.Text.G, pThemeColors.Text.B, 0.8)
            
            ' Wrap and draw the description text
            Dim lMaxWidth As Integer = 350 ' Maximum width for text
            lCurrentY = DrawLines(vContext, vX, lCurrentY, lMaxWidth, NORMAL_SIZE)
            
            lCurrentY += 15
            
            ' Draw accent line under description
            vContext.SetSourceRGB(pThemeColors.Accent.R, pThemeColors.Accent.G, pThemeColors.Accent.B)
            vContext.LineWidth = 2
            vContext.MoveTo(vX, lCurrentY)
            vContext.LineTo(vX + 250, lCurrentY)
            vContext.Stroke()
            
            Return lCurrentY + 20
        End Function

        ''' <summary>
        ''' Draws text with word wrapping - a line that looks like a URL (starts with
        ''' "http://" or "https://") is drawn as a clickable hyperlink instead of plain
        ''' text, same visual treatment (Link/LinkHover color, underline on hover) and
        ''' hit-testing pattern DrawRecentFiles already uses for recent-file entries
        ''' </summary>
        ''' <param name="vContext">Cairo context</param>
        ''' <param name="vText">Text to draw</param>
        ''' <param name="vX">X position</param>
        ''' <param name="vY">Y position</param>
        ''' <param name="vMaxWidth">Maximum width before wrapping</param>
        ''' <param name="vFontSize">Font size</param>
        ''' <returns>Y position after the last line</returns>
        Private Function DrawLines(vContext As Context, vX As Integer, vY As Integer,
                                         vMaxWidth As Integer, vFontSize As Double) As Integer
            pLinkRects.Clear()
            pLinkUrls.Clear()

            Dim lLineHeight As Integer = CInt(vFontSize) + 4
            Dim lY As Integer = vY
            for each lLine As String in pIdeDescription
                Dim lLineTop As Integer = lY
                lY += lLineHeight

                If lLine.StartsWith("http://") OrElse lLine.StartsWith("https://") Then
                    Dim lExtents As TextExtents = vContext.TextExtents(lLine)
                    Dim lLinkRect As New Cairo.Rectangle(vX, lLineTop, lExtents.Width, lLineHeight)
                    pLinkRects.Add(lLinkRect)
                    pLinkUrls.Add(lLine)

                    Dim lIsHovered As Boolean = IsPointInRect(pLastMouseX, pLastMouseY, lLinkRect)
                    If lIsHovered Then
                        vContext.SetSourceRGB(pThemeColors.LinkHover.R, pThemeColors.LinkHover.G, pThemeColors.LinkHover.B)
                    Else
                        vContext.SetSourceRGB(pThemeColors.Link.R, pThemeColors.Link.G, pThemeColors.Link.B)
                    End If
                    vContext.MoveTo(vX, lY)
                    vContext.ShowText(lLine)

                    If lIsHovered Then
                        vContext.MoveTo(vX, lY + 2)
                        vContext.LineTo(vX + lExtents.Width, lY + 2)
                        vContext.LineWidth = 1
                        vContext.Stroke()
                    End If

                    ' Restore the normal (semi-transparent) description text color that
                    ' the caller set before calling DrawLines, for any lines after this one
                    vContext.SetSourceRGBA(pThemeColors.Text.R, pThemeColors.Text.G, pThemeColors.Text.B, 0.8)
                Else
                    vContext.MoveTo(vX, lY)
                    vContext.ShowText(lLine)
                End If
            Next

            Return lY
        End Function

        ''' <summary>
        ''' Sets the IDE description text
        ''' </summary>
        ''' <param name="vDescription">The description to display</param>
        Public Sub SetIdeDescription(vDescription As List(Of String))
            If vDescription Is Nothing then return
            pIdeDescription.Clear()
            pIdeDescription.Add("A lightweight, professional VB.NET IDE for Linux.")
            pIdeDescription.Add(" ")
            for each s as string in vDescription
                pIdeDescription.Add(s)
            next
            QueueDraw()
        End Sub
        
        ''' <summary>
        ''' Draws the Quick Start section heading and positions the three real
        ''' CustomDrawButton actions to match this same layout - Move() maps this
        ''' method's content-space Y down by the current scroll offset, matching what
        ''' the Cairo Translate in OnDraw does for everything that's still hand-painted
        ''' </summary>
        Private Function DrawQuickStart(vContext As Context, vX As Integer, vY As Integer, vWidth As Integer) As Integer
            ' Draw section heading
            vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold)
            vContext.SetFontSize(HEADING_SIZE)
            vContext.SetSourceRGB(pThemeColors.Heading.R, pThemeColors.Heading.G, pThemeColors.Heading.B)
            vContext.MoveTo(vX, vY + HEADING_SIZE)
            vContext.ShowText("Quick Start")

            Dim lButtonY As Integer = vY + HEADING_SIZE + 20

            Move(pNewProjectButton, vX, lButtonY - pTopOffset)
            lButtonY += BUTTON_HEIGHT + 15

            Move(pOpenProjectButton, vX, lButtonY - pTopOffset)
            lButtonY += BUTTON_HEIGHT + 15

            Move(pOpenFileButton, vX, lButtonY - pTopOffset)
            lButtonY += BUTTON_HEIGHT + 15

            Return lButtonY
        End Function

        
        ''' <summary>
        ''' Draws the keyboard shortcuts section
        ''' </summary>
        Private Function DrawKeyboardShortcuts(vContext As Context, vX As Integer, vY As Integer, vWidth As Integer) As Integer
            ' Draw section heading
            vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold)
            vContext.SetFontSize(HEADING_SIZE)
            vContext.SetSourceRGB(pThemeColors.Heading.R, pThemeColors.Heading.G, pThemeColors.Heading.B)
            vContext.MoveTo(vX, vY + HEADING_SIZE)
            vContext.ShowText("Keyboard Shortcuts")
            
            Dim lY As Integer = vY + HEADING_SIZE + 20
            
            ' Shortcut list
            Dim lShortcuts As New List(Of Tuple(Of String, String)) From {
                Tuple.Create("Ctrl+N", "New file"),
                Tuple.Create("Ctrl+O", "Open file"),
                Tuple.Create("Ctrl+S", "Save file"),
                Tuple.Create("Ctrl+Shift+S", "Save all"),
                Tuple.Create("F5", "Build and run"),
                Tuple.Create("F6", "Build project"),
                Tuple.Create("Ctrl+F", "Find"),
                Tuple.Create("Ctrl+H", "Replace"),
                Tuple.Create("Ctrl+G", "Go to line"),
                Tuple.Create("Ctrl+Z / Ctrl+Y", "Undo / Redo")
            }
            
            vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal)
            vContext.SetFontSize(NORMAL_SIZE)
            
            for each lShortcut in lShortcuts
                ' Draw shortcut key
                vContext.SetSourceRGB(pThemeColors.Accent.R, pThemeColors.Accent.G, pThemeColors.Accent.B)
                vContext.MoveTo(vX, lY + NORMAL_SIZE)
                vContext.ShowText(lShortcut.Item1)
                
                ' Draw description
                vContext.SetSourceRGB(pThemeColors.Text.R, pThemeColors.Text.G, pThemeColors.Text.B)
                vContext.MoveTo(vX + 120, lY + NORMAL_SIZE)
                vContext.ShowText("- " & lShortcut.Item2)
                
                lY += LINE_HEIGHT
            Next
            
            Return lY
        End Function
        
        ''' <summary>
        ''' Draws the recent files section
        ''' </summary>
        Private Function DrawRecentFiles(vContext As Context, vX As Integer, vY As Integer, vWidth As Integer) As Integer
            ' Draw section heading
            vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold)
            vContext.SetFontSize(HEADING_SIZE)
            vContext.SetSourceRGB(pThemeColors.Heading.R, pThemeColors.Heading.G, pThemeColors.Heading.B)
            vContext.MoveTo(vX, vY + HEADING_SIZE)
            vContext.ShowText("Recent Files")
            
            Dim lY As Integer = vY + HEADING_SIZE + 20
            
            If pRecentFiles.Count = 0 Then
                ' No recent files message
                vContext.SelectFontFace("Sans", FontSlant.Italic, FontWeight.Normal)
                vContext.SetFontSize(NORMAL_SIZE)
                vContext.SetSourceRGB(pThemeColors.Text.R, pThemeColors.Text.G, pThemeColors.Text.B)
                vContext.MoveTo(vX, lY + NORMAL_SIZE)
                vContext.ShowText("No recent files")
                lY += LINE_HEIGHT
            Else
                ' Draw recent files as clickable links
                vContext.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal)
                vContext.SetFontSize(NORMAL_SIZE)
                
                Dim lMaxFiles As Integer = Math.Min(10, pRecentFiles.Count)
                for i As Integer = 0 To lMaxFiles - 1
                    Dim lFile As String = pRecentFiles(i)
                    Dim lFileName As String = System.IO.Path.GetFileName(lFile)
                    
                    ' Create hit rectangle for this file
                    Dim lTextExtents As TextExtents = vContext.TextExtents(lFileName)
                    Dim lFileRect As New Cairo.Rectangle(vX, lY, lTextExtents.Width, LINE_HEIGHT)
                    pRecentFileRects.Add(lFileRect)
                    
                    ' Check if this file is hovered
                    Dim lIsHovered As Boolean = IsPointInRect(pLastMouseX, pLastMouseY, lFileRect)
                    
                    ' Draw file name as link
                    If lIsHovered Then
                        vContext.SetSourceRGB(pThemeColors.LinkHover.R, pThemeColors.LinkHover.G, pThemeColors.LinkHover.B)
                    Else
                        vContext.SetSourceRGB(pThemeColors.Link.R, pThemeColors.Link.G, pThemeColors.Link.B)
                    End If
                    vContext.MoveTo(vX, lY + NORMAL_SIZE)
                    vContext.ShowText(lFileName)
                    
                    ' Draw underline if hovered
                    If lIsHovered Then
                        vContext.MoveTo(vX, lY + NORMAL_SIZE + 2)
                        vContext.LineTo(vX + lTextExtents.Width, lY + NORMAL_SIZE + 2)
                        vContext.LineWidth = 1
                        vContext.Stroke()
                    End If
                    
                    lY += LINE_HEIGHT
                Next
            End If
            
            Return lY
        End Function
        
        ' ===== Private Methods - Input Handling =====
        
        ''' <summary>
        ''' Loads the penguin icon from embedded resources
        ''' </summary>
        Private Sub LoadPenguinImage()
            Try
                Dim lIconStream As System.IO.Stream = GetType(WelcomeTabWidget).Assembly.GetManifestResourceStream("SimpleIDE.icon.png")
                If lIconStream IsNot Nothing Then
                    pPenguinPixbuf = New Gdk.Pixbuf(lIconStream)
                    ' Scale to a nice size for the welcome tab (128x128)
                    pPenguinPixbuf = pPenguinPixbuf.ScaleSimple(128, 128, Gdk.InterpType.Bilinear)
                    lIconStream.Close()
                    Console.WriteLine("WelcomeTabWidget: Penguin image loaded successfully")
                Else
                    Console.WriteLine("WelcomeTabWidget: Could not find icon.png in resources")
                End If
            Catch ex As Exception
                Console.WriteLine($"WelcomeTabWidget.LoadPenguinImage error: {ex.Message}")
            End Try
        End Sub


        ''' <summary>
        ''' Handles mouse button press
        ''' </summary>
        Private Sub OnButtonPress(vSender As Object, vArgs As ButtonPressEventArgs)
            If vArgs.Event.Button <> 1 Then Return ' Only handle left button

            Dim lX As Double = vArgs.Event.X
            Dim lY As Double = vArgs.Event.Y

            ' Check if clicking on scrollbar
            If pScrollbarVisible AndAlso IsPointInRect(lX, lY, pScrollThumbRect) Then
                pIsDraggingScrollbar = True
                pScrollDragStartY = lY
                pScrollDragStartOffset = pTopOffset
                Return
            End If

            ' Apply scroll offset to click coordinates - the three Quick Start buttons are
            ' real widgets now and handle their own press/click directly, so only content
            ' that's still Cairo-painted (recent files, description hyperlinks) needs
            ' manual hit-testing here
            lY += pTopOffset

            pPressedButton = Nothing
            For i As Integer = 0 To pRecentFileRects.Count - 1
                If IsPointInRect(lX, lY, pRecentFileRects(i)) Then
                    pPressedButton = $"recent_{i}"
                    Exit For
                End If
            Next
            If pPressedButton Is Nothing Then
                For i As Integer = 0 To pLinkRects.Count - 1
                    If IsPointInRect(lX, lY, pLinkRects(i)) Then
                        pPressedButton = $"link_{i}"
                        Exit For
                    End If
                Next
            End If

            If pPressedButton IsNot Nothing Then
                QueueDraw()
            End If

            vArgs.RetVal = True
        End Sub

        ''' <summary>
        ''' Handles mouse button release
        ''' </summary>
        Private Sub OnButtonRelease(vSender As Object, vArgs As ButtonReleaseEventArgs)
            If vArgs.Event.Button <> 1 Then Return

            ' Stop scrollbar dragging
            If pIsDraggingScrollbar Then
                pIsDraggingScrollbar = False
                QueueDraw()
                vArgs.RetVal = True
                Return
            End If

            If pPressedButton Is Nothing Then Return

            Dim lX As Double = vArgs.Event.X
            Dim lY As Double = vArgs.Event.Y + pTopOffset  ' Apply scroll offset

            ' Check if still over the pressed element
            If pPressedButton.StartsWith("recent_") Then
                Dim lIndex As Integer = Integer.Parse(pPressedButton.Substring(7))
                If lIndex < pRecentFileRects.Count AndAlso IsPointInRect(lX, lY, pRecentFileRects(lIndex)) Then
                    If lIndex < pRecentFiles.Count Then
                        RaiseEvent RecentFileClicked(Me, pRecentFiles(lIndex))
                    End If
                End If
            ElseIf pPressedButton.StartsWith("link_") Then
                Dim lIndex As Integer = Integer.Parse(pPressedButton.Substring(5))
                If lIndex < pLinkRects.Count AndAlso IsPointInRect(lX, lY, pLinkRects(lIndex)) Then
                    If lIndex < pLinkUrls.Count Then
                        RaiseEvent LinkClicked(Me, pLinkUrls(lIndex))
                    End If
                End If
            End If

            pPressedButton = Nothing
            QueueDraw()
            vArgs.RetVal = True
        End Sub

        ''' <summary>
        ''' Handles mouse motion
        ''' </summary>
        Private Sub OnMotionNotify(vSender As Object, vArgs As MotionNotifyEventArgs)
            Dim lX As Double = vArgs.Event.X
            Dim lY As Double = vArgs.Event.Y

            ' Handle scrollbar dragging
            If pIsDraggingScrollbar Then
                Dim lDelta As Double = lY - pScrollDragStartY
                Dim lMaxScroll As Integer = Math.Max(0, pContentHeight - pViewportHeight)
                Dim lScrollRange As Integer = pViewportHeight - pScrollThumbRect.Height

                If lScrollRange > 0 Then
                    Dim lNewOffset As Integer = pScrollDragStartOffset + CInt((lDelta / lScrollRange) * lMaxScroll)
                    pTopOffset = Math.Max(0, Math.Min(lMaxScroll, lNewOffset))
                    QueueDraw()
                End If
                vArgs.RetVal = True
                Return
            End If

            ' Store mouse position for hover detection (recent files, description
            ' hyperlinks - both re-check IsPointInRect against this live each draw, so a
            ' redraw here is enough to keep their hover highlighting current; the three
            ' Quick Start buttons handle their own hover internally now)
            pLastMouseX = lX
            pLastMouseY = lY + pTopOffset  ' Apply scroll offset
            QueueDraw()

            vArgs.RetVal = True
        End Sub
        
        Private Sub OnScrollEvent(vSender As Object, vArgs As ScrollEventArgs)
            If Not pScrollbarVisible Then 
                vArgs.RetVal = False  ' Let parent handle if we don't need scrolling
                Return
            End If
            
            Dim lScrollAmount As Integer = 40  ' Pixels to scroll per wheel notch
            Dim lMaxScroll As Integer = Math.Max(0, pContentHeight - pViewportHeight)
            
            Select Case vArgs.Event.Direction
                Case ScrollDirection.Up
                    If pTopOffset > 0 Then
                        pTopOffset = Math.Max(0, pTopOffset - lScrollAmount)
                        QueueDraw()
                    End If
                    
                Case ScrollDirection.Down
                    If pTopOffset < lMaxScroll Then
                        pTopOffset = Math.Min(lMaxScroll, pTopOffset + lScrollAmount)
                        QueueDraw()
                    End If
                    
                Case ScrollDirection.Smooth
                    ' Handle smooth scrolling (touchpad) without GetScrollDeltas
                    ' For smooth scrolling, we'll use smaller increments
                    Dim lSmoothAmount As Integer = 10
                    
                    ' GTK# doesn't expose DeltaX/DeltaY directly in VB.NET
                    ' So we'll treat smooth scrolling as smaller increments
                    ' This is a limitation of the GTK# binding
                    pTopOffset = Math.Max(0, Math.Min(lMaxScroll, pTopOffset + lSmoothAmount))
                    QueueDraw()
            End Select
            
            vArgs.RetVal = True  ' We handled the event
        End Sub
        
        ''' <summary>
        ''' Handles mouse leave
        ''' </summary>
        Private Sub OnLeaveNotify(vSender As Object, vArgs As LeaveNotifyEventArgs)
            pPressedButton = Nothing
            QueueDraw()
            vArgs.RetVal = True
        End Sub
        
        ''' <summary>
        ''' Checks if a point is inside a rectangle
        ''' </summary>
        Private Function IsPointInRect(vX As Double, vY As Double, vRect As Cairo.Rectangle) As Boolean
            Return vX >= vRect.X AndAlso vX <= vRect.X + vRect.Width AndAlso
                   vY >= vRect.Y AndAlso vY <= vRect.Y + vRect.Height
        End Function

        ''' <summary>
        ''' Sets the theme colors directly
        ''' </summary>
        ''' <param name="vIsDarkTheme">True if using dark theme</param>
        Public Sub SetThemeColors(vIsDarkTheme As Boolean)
            UpdateTheme(vIsDarkTheme)
        End Sub

        Protected Overrides Sub Dispose(vDisposing As Boolean)
            Try
                If vDisposing Then
                    If pPenguinPixbuf IsNot Nothing Then
                        pPenguinPixbuf.Dispose()
                        pPenguinPixbuf = Nothing
                    End If
                End If
                MyBase.Dispose(vDisposing)
            Catch ex As Exception
                Console.WriteLine($"WelcomeTabWidget.Dispose error: {ex.Message}")
            End Try
        End Sub
        
        Private Function CalculateContentHeight() As Integer
            ' Calculate actual content height based on what we're drawing
            Dim lLeftHeight As Integer = MARGIN  ' Start with top margin
            Dim lRightHeight As Integer = MARGIN + 20  ' Right column starts lower
            
            ' Left column: Penguin image and title section
            If pPenguinPixbuf IsNot Nothing Then
                lLeftHeight += 128 + 20  ' Image height + spacing
            End If
            lLeftHeight += CInt(TITLE_SIZE) + 10  ' Title
            lLeftHeight += CInt(HEADING_SIZE) + 10  ' Version
            
            ' Description lines (with proper line height)
            If pIdeDescription IsNot Nothing Then
                lLeftHeight += pIdeDescription.Count * (CInt(NORMAL_SIZE) + 4)
            End If
            lLeftHeight += 15  ' Spacing after description
            lLeftHeight += 20  ' Accent line spacing
            
            ' Quick Start section
            lLeftHeight += SECTION_SPACING
            lLeftHeight += CInt(HEADING_SIZE) + 20  ' Section heading
            lLeftHeight += 3 * (BUTTON_HEIGHT + 15)  ' 3 buttons with spacing
            
            ' Right column: Keyboard shortcuts
            lRightHeight += CInt(HEADING_SIZE) + 20  ' Section heading
            lRightHeight += 10 * LINE_HEIGHT  ' 10 shortcuts
            lRightHeight += SECTION_SPACING
            
            ' Recent files section
            lRightHeight += CInt(HEADING_SIZE) + 20  ' Section heading
            Dim lRecentCount As Integer = If(pRecentFiles Is Nothing OrElse pRecentFiles.Count = 0, 1, pRecentFiles.Count)
            lRightHeight += lRecentCount * LINE_HEIGHT
            
            ' Return the taller column plus bottom margin
            Return Math.Max(lLeftHeight, lRightHeight) + MARGIN
        End Function     
           
        Private Sub DrawScrollbar(vContext As Context, vWidth As Integer, vHeight As Integer)
            ' Calculate scrollbar dimensions
            Dim lScrollbarX As Integer = vWidth - pScrollbarWidth - 2
            pScrollbarRect = New Cairo.Rectangle(lScrollbarX, 0, pScrollbarWidth, vHeight)
            
            ' Draw scrollbar track with better visibility
            vContext.SetSourceRGBA(pThemeColors.Separator.R, pThemeColors.Separator.G, pThemeColors.Separator.B, 0.3)
            vContext.Rectangle(pScrollbarRect.X, pScrollbarRect.Y, pScrollbarRect.Width, pScrollbarRect.Height)
            vContext.Fill()
            
            ' Draw track border for better definition
            vContext.SetSourceRGBA(pThemeColors.Separator.R, pThemeColors.Separator.G, pThemeColors.Separator.B, 0.5)
            vContext.LineWidth = (1)
            vContext.Rectangle(pScrollbarRect.X, pScrollbarRect.Y, pScrollbarRect.Width, pScrollbarRect.Height)
            vContext.Stroke()
            
            ' Calculate thumb size and position
            Dim lVisibleRatio As Double = vHeight / CDbl(pContentHeight)
            Dim lThumbHeight As Integer = Math.Max(30, CInt(lVisibleRatio * vHeight))
            Dim lMaxScroll As Integer = pContentHeight - vHeight
            Dim lThumbY As Integer = 0
            
            If lMaxScroll > 0 Then
                Dim lScrollRatio As Double = pTopOffset / CDbl(lMaxScroll)
                lThumbY = CInt(lScrollRatio * (vHeight - lThumbHeight))
            End If
            
            pScrollThumbRect = New Cairo.Rectangle(lScrollbarX + 2, lThumbY, pScrollbarWidth - 4, lThumbHeight)
            
            ' Draw thumb with better visibility
            Dim lThumbAlpha As Double = If(pIsDraggingScrollbar, 0.7, 0.5)
            vContext.SetSourceRGBA(pThemeColors.Text.R, pThemeColors.Text.G, pThemeColors.Text.B, lThumbAlpha)
            DrawRoundedRectangle(vContext, pScrollThumbRect.X, pScrollThumbRect.Y, 
                                 pScrollThumbRect.Width, pScrollThumbRect.Height, 2)
            vContext.Fill()
        End Sub
        
        ' Add: SimpleIDE.Widgets.WelcomeTabWidget.DrawRoundedRectangle
        Private Sub DrawRoundedRectangle(vContext As Context, vX As Double, vY As Double, 
                                         vWidth As Double, vHeight As Double, vRadius As Double)
            vContext.MoveTo(vX + vRadius, vY)
            vContext.LineTo(vX + vWidth - vRadius, vY)
            vContext.Arc(vX + vWidth - vRadius, vY + vRadius, vRadius, -Math.PI / 2, 0)
            vContext.LineTo(vX + vWidth, vY + vHeight - vRadius)
            vContext.Arc(vX + vWidth - vRadius, vY + vHeight - vRadius, vRadius, 0, Math.PI / 2)
            vContext.LineTo(vX + vRadius, vY + vHeight)
            vContext.Arc(vX + vRadius, vY + vHeight - vRadius, vRadius, Math.PI / 2, Math.PI)
            vContext.LineTo(vX, vY + vRadius)
            vContext.Arc(vX + vRadius, vY + vRadius, vRadius, Math.PI, 3 * Math.PI / 2)
            vContext.ClosePath()
        End Sub        
        
        ''' <summary>
        ''' Handles widget size allocation changes to update scrollbar visibility - also
        ''' keeps the internal pDrawingArea (which does all the actual Cairo painting)
        ''' sized and positioned to exactly fill this Fixed's own allocation, since Fixed
        ''' does not do that automatically for its children
        ''' </summary>
        ''' <remarks>
        ''' Deliberately uses SizeAllocate (an explicit, direct allocation) rather than
        ''' SetSizeRequest here. SetSizeRequest sets a widget's minimum/natural SIZE REQUEST,
        ''' which a Fixed container's own preferred-size computation folds into ITS reported
        ''' natural size - so setting it to "whatever pDrawingArea was just allocated" created
        ''' a self-reinforcing ratchet: once this widget was ever allocated a large height
        ''' (e.g. the full editor area before the bottom panel had ever been shown),
        ''' pDrawingArea's minimum size request locked in at that height, which then made
        ''' THIS widget (and everything above it - the editor notebook, the center Paned)
        ''' permanently report that same large height as ITS OWN minimum, silently
        ''' overriding any later attempt to give the paned's other child (the bottom panel)
        ''' more space, no matter what its Position was explicitly set to. SizeAllocate
        ''' assigns the child's on-screen rectangle directly without feeding back into
        ''' anyone's preferred-size negotiation, so it can't ratchet like this.
        ''' </remarks>
        Private Sub OnSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            If pDrawingArea IsNot Nothing Then
                pDrawingArea.SizeAllocate(New Gdk.Rectangle(0, 0, vArgs.Allocation.Width, vArgs.Allocation.Height))
            End If

            ' Recalculate content height and scrollbar visibility
            pViewportHeight = vArgs.Allocation.Height
            pContentHeight = CalculateContentHeight()
            
            Dim lWasVisible As Boolean = pScrollbarVisible
            pScrollbarVisible = (pContentHeight > pViewportHeight)
            
            ' Adjust scroll offset if needed
            If pScrollbarVisible Then
                Dim lMaxScroll As Integer = Math.Max(0, pContentHeight - pViewportHeight)
                If pTopOffset > lMaxScroll Then
                    pTopOffset = lMaxScroll
                End If
            Else
                pTopOffset = 0
            End If
            
            ' Redraw if visibility changed or if scrollbar is visible
            If lWasVisible <> pScrollbarVisible OrElse pScrollbarVisible Then
                QueueDraw()
            End If
        End Sub
        
        ' ===== Inner Classes =====
        
        

        ''' <summary>
        ''' Represents the theme colors used by the WelcomeTabWidget
        ''' </summary>
        Public Class WelcomeThemeColors
            
            ''' <summary>
            ''' Gets or sets the background color
            ''' </summary>
            Public Property Background As Cairo.Color

            Public Property Button As Cairo.Color
            Public Property Link As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the main text color
            ''' </summary>
            Public Property Text As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the heading text color
            ''' </summary>
            Public Property Heading As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the accent color for highlights
            ''' </summary>
            Public Property Accent As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the button normal state color
            ''' </summary>
            Public Property ButtonNormal As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the button hover state color
            ''' </summary>
            Public Property ButtonHover As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the button pressed state color
            ''' </summary>
            Public Property ButtonPressed As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the button text color
            ''' </summary>
            Public Property ButtonText As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the separator line color
            ''' </summary>
            Public Property Separator As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the normal link color
            ''' </summary>
            Public Property LinkNormal As Cairo.Color
            
            ''' <summary>
            ''' Gets or sets the hover link color
            ''' </summary>
            Public Property LinkHover As Cairo.Color

            
            ' Conversion function
            Public Function ConvertToCairoColor(rgba As RGBA) As Cairo.Color
                Return New Cairo.Color(rgba.Red, rgba.Green, rgba.Blue, rgba.Alpha)
            End Function


            ''' <summary>
            ''' Initializes a new instance with default light theme colors
            ''' </summary>
            Public Sub New()
                ' Initialize with default light theme
                Background = ConvertToCairoColor(New RGBA() with {.Red = 1.0, .Green = 1.0, .Blue = 1.0, .Alpha = 1.0})
                Text = ConvertToCairoColor(New RGBA() with {.Red = 0.2, .Green = 0.2, .Blue = 0.2, .Alpha = 1.0})
                Heading = ConvertToCairoColor(New RGBA() with {.Red = 0.1, .Green = 0.1, .Blue = 0.1, .Alpha = 1.0})
                Accent = ConvertToCairoColor(New RGBA() with {.Red = 0.0, .Green = 0.4, .Blue = 0.8, .Alpha = 1.0})
                ButtonNormal = ConvertToCairoColor(New RGBA() with {.Red = 0.94, .Green = 0.94, .Blue = 0.94, .Alpha = 1.0})
                ButtonHover = ConvertToCairoColor(New RGBA() with {.Red = 0.88, .Green = 0.88, .Blue = 0.88, .Alpha = 1.0})
                ButtonPressed = ConvertToCairoColor(New RGBA() with {.Red = 0.82, .Green = 0.82, .Blue = 0.82, .Alpha = 1.0})
                ButtonText = ConvertToCairoColor(New RGBA() with {.Red = 0.1, .Green = 0.1, .Blue = 0.1, .Alpha = 1.0})
                Separator = ConvertToCairoColor(New RGBA() with {.Red = 0.8, .Green = 0.8, .Blue = 0.8, .Alpha = 0.5})
                LinkNormal = ConvertToCairoColor(New RGBA() with {.Red = 0.0, .Green = 0.4, .Blue = 0.8, .Alpha = 1.0})
                LinkHover = ConvertToCairoColor(New RGBA() with {.Red = 0.0, .Green = 0.5, .Blue = 1.0, .Alpha = 1.0})
            End Sub

        End Class

        
    End Class  
    
End Namespace