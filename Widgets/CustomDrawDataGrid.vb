' Widgets/CustomDrawDataGrid.vb - Custom-drawn data grid with built-in scrollbar (NO ScrolledWindow)
Imports Gtk
Imports Gdk
Imports Cairo
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

' Created: 2025-01-03 - Redesigned to eliminate ScrolledWindow dependency

Namespace Widgets
    
    ''' <summary>
    ''' Self-contained data grid widget with built-in vertical scrollbar
    ''' </summary>
    Partial Public Class CustomDrawDataGrid
        Inherits Box
        
        ' ===== Constants =====
        Private Const DEFAULT_ROW_HEIGHT As Integer = 24
        Private Const DEFAULT_HEADER_HEIGHT As Integer = 28
        Private Const MIN_COLUMN_WIDTH As Integer = 30
        Private Const SORT_ARROW_SIZE As Integer = 8
        Private Const CELL_PADDING As Integer = 6
        Private Const GRID_LINE_WIDTH As Double = 0.5
        Private Const SCROLLBAR_WIDTH As Integer = 18
        
        ' ===== UI Components =====
        Private pMainBox As Box                    ' Horizontal box containing grid and scrollbar
        Private pGridBox As Box                    ' Vertical box containing header and content
        Private pDrawingArea As DrawingArea        ' Main content area
        Private pHeaderArea As DrawingArea         ' Header row
        Private pVScrollbar As Scrollbar           ' Vertical scrollbar (always visible)
        Private pHScrollbar As Scrollbar           ' Horizontal scrollbar
        Private pCornerBox As DrawingArea          ' Corner between scrollbars
        
        ' ===== Data Storage =====
        Private pColumns As New List(Of DataGridColumn)
        Private pRows As New List(Of DataGridRow)
        Private pVisibleRows As New List(Of Integer)  ' Indices of visible rows after filtering
        
        ' ===== Layout State =====
        Private pRowHeight As Integer = DEFAULT_ROW_HEIGHT
        Private pHeaderHeight As Integer = DEFAULT_HEADER_HEIGHT
        Private pViewportWidth As Integer = 0
        Private pViewportHeight As Integer = 0
        Private pContentWidth As Integer = 0
        Private pContentHeight As Integer = 0
        Private pTotalHeight As Integer = 0        ' Total height including non-visible rows
        
        ' ===== Scrolling State =====
        Private pScrollX As Integer = 0
        Private pScrollY As Integer = 0            ' Vertical scroll position in pixels
        Private pVerticalOffset As Integer = 0     ' First visible row index
        Private pHorizontalOffset As Integer = 0   ' Horizontal scroll in pixels
        Private pMaxVerticalScroll As Integer = 0  ' Maximum vertical scroll value
        
        ' ===== Selection State =====
        Private pSelectedRowIndex As Integer = -1
        Private pSelectedColumnIndex As Integer = -1
        Private pMultiSelectEnabled As Boolean = False
        Private pSelectedRows As New HashSet(Of Integer)
        Private pFocusedCell As New Gdk.Point(-1, -1)  ' Row, Column
        
        ' ===== Interaction State =====
        Private pHoverRowIndex As Integer = -1
        Private pHoverColumnIndex As Integer = -1
        Private pResizingColumn As Integer = -1
        Private pResizeStartX As Integer = 0
        Private pResizeStartWidth As Integer = 0
        Private pSortColumn As Integer = -1
        Private pSortAscending As Boolean = True
        
        ' ===== Theme =====
        Private pThemeManager As ThemeManager
        Private pThemeSubscribed As Boolean = False
        Private pBackgroundColor As String = "#FFFFFF"
        Private pAlternateRowColor As String = "#F8F8F8"
        Private pForegroundColor As String = "#000000"
        Private pGridLineColor As String = "#E0E0E0"
        Private pHeaderBackgroundColor As String = "#F0F0F0"
        Private pHeaderForegroundColor As String = "#000000"
        Private pSelectionColor As String = "#007ACC"
        Private pSelectionTextColor As String = "#FFFFFF"
        Private pHoverColor As String = "#E5F1FB"
        Private pFontFamily As String = "Sans"
        Private pFontSize As Integer = 10

        Public ReadOnly Property SelectionColor() As String
            Get
                Return pSelectionColor
            End Get
        End Property

        Public ReadOnly Property HoverColor() As String
            Get
                Return pHovercolor
            End Get
        End Property

        Public ReadOnly Property AlternateRowColor() As String
            Get
                Return pAlternateRowColor
            End Get
        End Property
        
        Public ReadOnly Property BackgroundColor() As String
            Get
                Return pBackgroundColor
            End Get
        End Property

        
        ' ===== Features =====
        Private pShowGridLines As Boolean = True
        Private pShowHeaders As Boolean = True
        Private pAlternateRowColors As Boolean = True
        Private pAllowColumnResize As Boolean = True
        Private pAllowSort As Boolean = True
        Private pAllowCellEdit As Boolean = False

        ' ===== Color Override System =====
        
        ''' <summary>
        ''' Delegate for custom row background color determination
        ''' </summary>
        Public Delegate Function GetRowBackgroundColorDelegate(vRowIndex As Integer, vRow As DataGridRow, vIsSelected As Boolean, vIsHover As Boolean) As String
        
        ''' <summary>
        ''' Delegate for custom cell foreground color determination
        ''' </summary>
        Public Delegate Function GetCellForegroundColorDelegate(vRowIndex As Integer, vColumnIndex As Integer, vCell As DataGridCell) As String
        
        ''' <summary>
        ''' Optional custom row background color provider
        ''' </summary>
        Public Property GetRowBackgroundColor As GetRowBackgroundColorDelegate
        
        ''' <summary>
        ''' Optional custom cell foreground color provider
        ''' </summary>
        Public Property GetCellForegroundColor As GetCellForegroundColorDelegate
        
        ' ===== Events =====
        Public Event SelectionChanged(vRowIndex As Integer, vColumnIndex As Integer, vRow As DataGridRow)
        Public Event RowDoubleClicked(vRowIndex As Integer, vRow As DataGridRow)
        Public Event CellDoubleClicked(vRowIndex As Integer, vColumnIndex As Integer, vValue As Object)
        Public Event ColumnResized(vColumnIndex As Integer, vNewWidth As Integer)
        Public Event SortChanged(vColumnIndex As Integer, vAscending As Boolean)
        Public Event CellEdited(vRowIndex As Integer, vColumnIndex As Integer, vOldValue As Object, vNewValue As Object)

        ''' <summary>
        ''' Event raised when an icon cell needs to be rendered
        ''' </summary>
        Public Shadows Event RenderIcon(vArgs As IconRenderEventArgs)  
      
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new self-contained CustomDrawDataGrid
        ''' </summary>
        Public Sub New()
            MyBase.New(Orientation.Horizontal, 0)
            
            BuildUI()
            SetupEventHandlers()
            InitializeDefaults()
            InitializeTheme()

        End Sub
        
        ' ===== UI Building =====
        
        ''' <summary>
        ''' Builds the UI components without ScrolledWindow
        ''' </summary>
        Private Sub BuildUI()
            Try
                ' Create main horizontal box (content | scrollbar)
                pMainBox = New Box(Orientation.Horizontal, 0)
                
                ' Create vertical box for header and content
                pGridBox = New Box(Orientation.Vertical, 0)
                
                ' Create header area
                pHeaderArea = New DrawingArea()
                pHeaderArea.HeightRequest = pHeaderHeight
                pHeaderArea.CanFocus = False
                pHeaderArea.Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or 
                                   EventMask.PointerMotionMask Or EventMask.LeaveNotifyMask
                pGridBox.PackStart(pHeaderArea, False, False, 0)
                
                ' Create drawing area for content
                pDrawingArea = New DrawingArea()
                pDrawingArea.CanFocus = True
                pDrawingArea.Events = EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or
                                    EventMask.PointerMotionMask Or EventMask.ScrollMask Or
                                    EventMask.KeyPressMask Or EventMask.KeyReleaseMask Or
                                    EventMask.LeaveNotifyMask
                pDrawingArea.Expand = True
                pGridBox.PackStart(pDrawingArea, True, True, 0)
                
                ' Create horizontal scrollbar (optional, at bottom)
                pHScrollbar = New Scrollbar(Orientation.Horizontal, New Adjustment(0, 0, 100, 1, 10, 10))
                pHScrollbar.NoShowAll = True  ' Hidden by default
                pGridBox.PackStart(pHScrollbar, False, False, 0)
                
                ' Add grid box to main box
                pMainBox.PackStart(pGridBox, True, True, 0)
                
                ' Create vertical scrollbar (always visible on right)
                pVScrollbar = New Scrollbar(Orientation.Vertical, New Adjustment(0, 0, 100, 1, 10, 10))
                pVScrollbar.WidthRequest = SCROLLBAR_WIDTH
                pMainBox.PackStart(pVScrollbar, False, False, 0)
                
                ' Add main box to this widget
                PackStart(pMainBox, True, True, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.BuildUI error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets up event handlers for all components
        ''' </summary>
        Private Sub SetupEventHandlers()
            Try
                ' Header events
                AddHandler pHeaderArea.Drawn, AddressOf OnHeaderDrawn
                AddHandler pHeaderArea.ButtonPressEvent, AddressOf OnHeaderButtonPress
                AddHandler pHeaderArea.ButtonReleaseEvent, AddressOf OnHeaderButtonRelease
                AddHandler pHeaderArea.MotionNotifyEvent, AddressOf OnHeaderMotionNotify
                AddHandler pHeaderArea.LeaveNotifyEvent, AddressOf OnHeaderLeave
                
                ' Content events
                AddHandler pDrawingArea.Drawn, AddressOf OnContentDrawn
                AddHandler pDrawingArea.ButtonPressEvent, AddressOf OnContentButtonPress
                AddHandler pDrawingArea.ButtonReleaseEvent, AddressOf OnContentButtonRelease
                AddHandler pDrawingArea.MotionNotifyEvent, AddressOf OnContentMotionNotify
                AddHandler pDrawingArea.ScrollEvent, AddressOf OnContentScroll
                AddHandler pDrawingArea.KeyPressEvent, AddressOf OnContentKeyPress
                AddHandler pDrawingArea.LeaveNotifyEvent, AddressOf OnContentLeave
                AddHandler pDrawingArea.SizeAllocated, AddressOf OnContentSizeAllocated
                
                ' Scrollbar events
                AddHandler pVScrollbar.ValueChanged, AddressOf OnVScrollbarValueChanged
                AddHandler pHScrollbar.ValueChanged, AddressOf OnHScrollbarValueChanged
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SetupEventHandlers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initializes default settings
        ''' </summary>
        Private Sub InitializeDefaults()
            Try
                ' Initialize visible rows list
                UpdateVisibleRows()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.InitializeDefaults error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Initializes theme colors from ThemeManager or SettingsManager
        ''' </summary>
        Private Sub InitializeTheme()
            Try
                ' Try to get ThemeManager from parent hierarchy
                If pThemeManager Is Nothing Then
                    ' Walk up the widget tree to find a MainWindow or other component with ThemeManager
                    Dim lParent As Widget = Me.Parent
                    While lParent IsNot Nothing
                        ' Check if parent is MainWindow (which has ThemeManager)
                        If TypeOf lParent Is Gtk.Window Then
                            ' Try to get ThemeManager through reflection or event
                            Exit While
                        End If
                        lParent = lParent.Parent
                    End While
                End If
                
                ' Get current theme name from settings if available
                Dim lThemeName As String = "Default Dark"
                Try
                    ' Try to get theme from environment or settings
                    Dim lSettingsPath As String = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SimpleIDE", "settings.json")
                    
                    If System.IO.File.Exists(lSettingsPath) Then
                        Dim lJson As String = System.IO.File.ReadAllText(lSettingsPath)
                        ' Simple extraction of CurrentTheme value
                        Dim lMatch As System.Text.RegularExpressions.Match = 
                            System.Text.RegularExpressions.Regex.Match(lJson, """CurrentTheme""\s*:\s*""([^""]+)""")
                        If lMatch.Success Then
                            lThemeName = lMatch.Groups(1).Value
                        End If
                    End If
                Catch
                    ' Ignore errors, use default
                End Try
                
                ' Apply theme colors based on theme name
                ApplyThemeColors(lThemeName)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.InitializeTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies theme colors based on theme name
        ''' </summary>
        ''' <param name="vThemeName">Name of the theme to apply</param>
        Private Sub ApplyThemeColors(vThemeName As String)
            Try
                Dim lIsDark As Boolean = vThemeName.ToLower().Contains("dark")
                
                If lIsDark Then
                    ' Dark theme colors (matching VS Code Dark+)
                    pBackgroundColor = "#1E1E1E"
                    pAlternateRowColor = "#252526"
                    pForegroundColor = "#D4D4D4"
                    pGridLineColor = "#3E3E42"
                    pHeaderBackgroundColor = "#2D2D30"
                    pHeaderForegroundColor = "#CCCCCC"
                    pSelectionColor = "#094771"
                    pSelectionTextColor = "#FFFFFF"
                    pHoverColor = "#2A2D2E"
                Else
                    ' Light theme colors
                    pBackgroundColor = "#FFFFFF"
                    pAlternateRowColor = "#F8F8F8"
                    pForegroundColor = "#000000"
                    pGridLineColor = "#E0E0E0"
                    pHeaderBackgroundColor = "#F0F0F0"
                    pHeaderForegroundColor = "#000000"
                    pSelectionColor = "#007ACC"
                    pSelectionTextColor = "#FFFFFF"
                    pHoverColor = "#E5F1FB"
                End If
                
                ' Force redraw with new colors
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.ApplyThemeColors error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the ThemeManager and applies current theme
        ''' </summary>
        ''' <param name="vThemeManager">The ThemeManager instance</param>
        Public Sub SetThemeManager(vThemeManager As ThemeManager)
            Try
                pThemeManager = vThemeManager
                
                If pThemeManager IsNot Nothing Then
                    ' Get and apply current theme
                    Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                    If lTheme IsNot Nothing Then
                        ApplyTheme(lTheme)
                    End If
                    
                    ' Subscribe to theme changes if not already subscribed
                    If Not pThemeSubscribed Then
                        AddHandler pThemeManager.ThemeChanged, AddressOf OnThemeChanged
                        pThemeSubscribed = True
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SetThemeManager error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies an EditorTheme to the data grid
        ''' </summary>
        ''' <param name="vTheme">The theme to apply</param>
        Public Sub ApplyTheme(vTheme As EditorTheme)
            Try
                If vTheme Is Nothing Then Return
                
                ' Apply theme colors
                pBackgroundColor = vTheme.BackgroundColor
                pForegroundColor = vTheme.ForegroundColor
                pSelectionColor = vTheme.SelectionColor
                pSelectionTextColor = "#FFFFFF"  ' Usually white for selections
                
                ' Derive other colors from theme
                pHeaderBackgroundColor = vTheme.LineNumberBackgroundColor
                pHeaderForegroundColor = vTheme.LineNumberColor
                pGridLineColor = vTheme.CurrentLineColor
                pHoverColor = vTheme.CurrentLineColor
                
                ' Calculate alternate row color (slightly different from background)
                pAlternateRowColor = LightenOrDarkenColor(pBackgroundColor, 0.05)
                
                ' Force redraw with new colors
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.ApplyTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles theme change events from ThemeManager
        ''' </summary>
        Private Sub OnThemeChanged(vTheme As EditorTheme)
            Try
                ApplyTheme(vTheme)
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnThemeChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Lightens or darkens a color by the specified amount
        ''' </summary>
        ''' <param name="vHexColor">Hex color string</param>
        ''' <param name="vAmount">Amount to lighten (positive) or darken (negative)</param>
        ''' <returns>Modified hex color string</returns>
        Private Function LightenOrDarkenColor(vHexColor As String, vAmount As Double) As String
            Try
                ' Parse hex color
                Dim lColor As New Gdk.RGBA()
                If Not lColor.Parse(vHexColor) Then Return vHexColor
                
                ' Determine if we should lighten or darken based on current brightness
                Dim lBrightness As Double = (lColor.Red * 0.299 + lColor.Green * 0.587 + lColor.Blue * 0.114)
                
                If lBrightness > 0.5 Then
                    ' Light color - darken it
                    lColor.Red = Math.Max(0, lColor.Red - vAmount)
                    lColor.Green = Math.Max(0, lColor.Green - vAmount)
                    lColor.Blue = Math.Max(0, lColor.Blue - vAmount)
                Else
                    ' Dark color - lighten it
                    lColor.Red = Math.Min(1, lColor.Red + vAmount)
                    lColor.Green = Math.Min(1, lColor.Green + vAmount)
                    lColor.Blue = Math.Min(1, lColor.Blue + vAmount)
                End If
                
                ' Convert back to hex
                Return String.Format("#{0:X2}{1:X2}{2:X2}",
                    CInt(lColor.Red * 255),
                    CInt(lColor.Green * 255),
                    CInt(lColor.Blue * 255))
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.LightenOrDarkenColor error: {ex.Message}")
                Return vHexColor
            End Try
        End Function
        
        
        ' ===== Public Properties =====
        
        ''' <summary>
        ''' Gets the columns collection
        ''' </summary>
        Public ReadOnly Property Columns As List(Of DataGridColumn)
            Get
                Return pColumns
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the rows collection
        ''' </summary>
        Public ReadOnly Property Rows As List(Of DataGridRow)
            Get
                Return pRows
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets whether grid lines are shown
        ''' </summary>
        Public Property ShowGridLines As Boolean
            Get
                Return pShowGridLines
            End Get
            Set(value As Boolean)
                pShowGridLines = value
                QueueDraw()
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether alternating row colors are used
        ''' </summary>
        Public Property AlternateRowColors As Boolean
            Get
                Return pAlternateRowColors
            End Get
            Set(value As Boolean)
                pAlternateRowColors = value
                QueueDraw()
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether columns can be resized
        ''' </summary>
        Public Property AllowColumnResize As Boolean
            Get
                Return pAllowColumnResize
            End Get
            Set(value As Boolean)
                pAllowColumnResize = value
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether columns can be sorted
        ''' </summary>
        Public Property AllowSort As Boolean
            Get
                Return pAllowSort
            End Get
            Set(value As Boolean)
                pAllowSort = value
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether multiple rows can be selected
        ''' </summary>
        Public Property MultiSelectEnabled As Boolean
            Get
                Return pMultiSelectEnabled
            End Get
            Set(value As Boolean)
                pMultiSelectEnabled = value
                If Not value Then
                    pSelectedRows.Clear()
                End If
            End Set
        End Property
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Adds a new row to the grid
        ''' </summary>
        Public Sub AddRow(vRow As DataGridRow)
            Try
                pRows.Add(vRow)
                UpdateVisibleRows()
                UpdateLayout()
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.AddRow error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clears all rows from the grid
        ''' </summary>
        Public Sub ClearRows()
            Try
                pRows.Clear()
                pVisibleRows.Clear()
                pSelectedRowIndex = -1
                pSelectedRows.Clear()
                pVerticalOffset = 0
                pScrollY = 0
                
                UpdateLayout()
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.ClearRows error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Forces a complete redraw of the grid
        ''' </summary>
        Public Overloads Sub QueueDraw()
            pHeaderArea?.QueueDraw()
            pDrawingArea?.QueueDraw()
        End Sub
        
        ' ===== Layout Methods =====
        
        ''' <summary>
        ''' Updates the overall layout of the grid
        ''' </summary>
        Private Sub UpdateLayout()
            Try
                UpdateContentDimensions()
                UpdateScrollbarRanges()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateLayout error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates content dimensions based on rows and columns
        ''' </summary>
        Private Sub UpdateContentDimensions()
            Try
                ' Calculate content width from columns
                pContentWidth = 0
                for each lColumn in pColumns
                    If lColumn.Visible Then
                        pContentWidth += lColumn.Width
                    End If
                Next
                
                ' Calculate total height from all rows
                pTotalHeight = pRows.Count * pRowHeight
                
                ' Calculate visible content height
                pContentHeight = Math.Min(pTotalHeight, pViewportHeight)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateContentDimensions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates scrollbar ranges and visibility
        ''' </summary>
        Private Sub UpdateScrollbarRanges()
            Try
                ' Calculate maximum scroll values
                pMaxVerticalScroll = Math.Max(0, pTotalHeight - pViewportHeight)
                
                ' Update vertical scrollbar
                pVScrollbar.Adjustment.Lower = 0
                pVScrollbar.Adjustment.Upper = pMaxVerticalScroll
                pVScrollbar.Adjustment.PageSize = pViewportHeight
                pVScrollbar.Adjustment.StepIncrement = pRowHeight
                pVScrollbar.Adjustment.PageIncrement = pViewportHeight
                
                ' Vertical scrollbar is always visible but may be disabled
                pVScrollbar.Sensitive = pMaxVerticalScroll > 0
                
                ' Update horizontal scrollbar (optional, hidden when not needed)
                Dim lNeedHScroll As Boolean = pContentWidth > pViewportWidth
                If lNeedHScroll Then
                    pHScrollbar.Adjustment.Lower = 0
                    pHScrollbar.Adjustment.Upper = pContentWidth
                    pHScrollbar.Adjustment.PageSize = pViewportWidth
                    pHScrollbar.Adjustment.StepIncrement = 20
                    pHScrollbar.Adjustment.PageIncrement = pViewportWidth
                    pHScrollbar.Visible = True
                Else
                    pHScrollbar.Visible = False
                    pHorizontalOffset = 0
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateScrollbarRanges error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the list of visible rows based on filters
        ''' </summary>
        Private Sub UpdateVisibleRows()
            Try
                pVisibleRows.Clear()
                for i As Integer = 0 To pRows.Count - 1
                    pVisibleRows.Add(i)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.UpdateVisibleRows error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        ''' <summary>
        ''' Handles content area size allocation
        ''' </summary>
        Private Sub OnContentSizeAllocated(vSender As Object, vArgs As SizeAllocatedArgs)
            Try
                pViewportWidth = vArgs.Allocation.Width
                pViewportHeight = vArgs.Allocation.Height
                
                ' Update column widths for auto-expand columns
                UpdateColumnWidths()
                
                UpdateLayout()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnContentSizeAllocated error: {ex.Message}")
            End Try
        End Sub    

        Private Function GetRowHeight(vRowIndex As Integer) As Integer
            Try
                If vRowIndex < 0 OrElse vRowIndex >= pRows.Count Then
                    Return pRowHeight
                End If
                
                Dim lRow As DataGridRow = pRows(vRowIndex)
                If lRow.CalculatedHeight > 0 Then
                    Return lRow.CalculatedHeight
                Else
                    Return pRowHeight
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.GetRowHeight error: {ex.Message}")
                Return pRowHeight
            End Try
        End Function
    
        ''' <summary>
        ''' Handles vertical scrollbar value changes
        ''' </summary>
        Private Sub OnVScrollbarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                pScrollY = CInt(pVScrollbar.Value)
                pVerticalOffset = pScrollY \ pRowHeight
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnVScrollbarValueChanged error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles horizontal scrollbar value changes
        ''' </summary>
        Private Sub OnHScrollbarValueChanged(vSender As Object, vArgs As EventArgs)
            Try
                pHorizontalOffset = CInt(pHScrollbar.Value)
                pHeaderArea.QueueDraw()
                pDrawingArea.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.OnHScrollbarValueChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Enables or disables word wrapping for a specific column
        ''' </summary>
        ''' <param name="vColumnIndex">Index of the column</param>
        ''' <param name="vEnabled">True to enable word wrap</param>
        ''' <param name="vMaxHeight">Maximum height for wrapped rows</param>
        Public Sub EnableWordWrap(vColumnIndex As Integer, vEnabled As Boolean, Optional vMaxHeight As Integer = 100)
            Try
                If vColumnIndex < 0 OrElse vColumnIndex >= pColumns.Count Then Return
                
                pColumns(vColumnIndex).WordWrap = vEnabled
                pColumns(vColumnIndex).MaxHeight = vMaxHeight
                
                ' Recalculate all row heights if enabling word wrap
                If vEnabled Then
                    for i As Integer = 0 To pRows.Count - 1
                        CalculateRowHeight(i)
                    Next
                Else
                    ' Reset all row heights to default
                    for each lRow As DataGridRow in pRows
                        lRow.CalculatedHeight = 0
                    Next
                End If
                
                UpdateLayout()
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.EnableWordWrap error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets a column to auto-expand to fill available space
        ''' </summary>
        ''' <param name="vColumnIndex">Index of the column</param>
        ''' <param name="vAutoExpand">True to enable auto-expand</param>
        Public Sub SetColumnAutoExpand(vColumnIndex As Integer, vAutoExpand As Boolean)
            Try
                If vColumnIndex < 0 OrElse vColumnIndex >= pColumns.Count Then Return
                
                pColumns(vColumnIndex).AutoExpand = vAutoExpand
                UpdateColumnWidths()
                UpdateLayout()
                QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawDataGrid.SetColumnAutoExpand error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace