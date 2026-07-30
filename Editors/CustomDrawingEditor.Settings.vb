' Editors/CustomDrawingEditor.Settings.vb - Centralized settings management using SettingsManager
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax
Imports SimpleIDE.Widgets

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Settings-Driven Properties =====
        ' All editor settings now read directly from SettingsManager
        
        ''' <summary>
        ''' Gets the font description from current settings and zoom level
        ''' </summary>
        ''' <remarks>
        ''' Always returns current font with zoom level applied
        ''' </remarks>
        Private ReadOnly Property pFontDescription() As Pango.FontDescription
            Get
                Try
                    If pSettingsManager IsNot Nothing Then
                        ' Get base font family from EditorFont setting
                        Dim lBaseFontFamily As String = GetBaseFontFamilyFromSettings()
                        ' Get current zoom level
                        Dim lZoomLevel As Integer = pSettingsManager.EditorZoomLevel
                        ' Create font description with zoom
                        Return Pango.FontDescription.FromString($"{lBaseFontFamily} {lZoomLevel}")
                    Else
                        ' Fallback to default
                        Return Pango.FontDescription.FromString("Monospace 11")
                    End If
                Catch ex As Exception
                    Console.WriteLine($"pFontDescription error: {ex.Message}")
                    Return Pango.FontDescription.FromString("Monospace 11")
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Gets tab width from settings
        ''' </summary>
        Private ReadOnly Property pTabWidth() As Integer
            Get
                Return If(pSettingsManager?.TabWidth, 4)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether to use tabs from settings
        ''' </summary>
        Private ReadOnly Property pUseTabs() As Boolean
            Get
                Return If(pSettingsManager?.UseTabs, False)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether to show line numbers from settings
        ''' </summary>
        Private ReadOnly Property pShowLineNumbers() As Boolean
            Get
                Return If(pSettingsManager?.ShowLineNumbers, True)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets word wrap setting from settings
        ''' </summary>
        Private ReadOnly Property pWordWrap() As Boolean
            Get
                Return If(pSettingsManager?.WordWrap, False)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets auto indent setting from settings
        ''' </summary>
        Private ReadOnly Property pAutoIndent() As Boolean
            Get
                Return If(pSettingsManager?.AutoIndent, True)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets highlight current line setting from settings
        ''' </summary>
        Private ReadOnly Property pHighlightCurrentLine() As Boolean
            Get
                Return If(pSettingsManager?.HighlightCurrentLine, True)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets show whitespace setting from settings
        ''' </summary>
        Private ReadOnly Property pShowWhitespace() As Boolean
            Get
                Return If(pSettingsManager?.ShowWhitespace, False)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets current zoom level from settings
        ''' </summary>
        Private ReadOnly Property pCurrentZoomLevel() As Integer
            Get
                Return If(pSettingsManager?.EditorZoomLevel, 11)
            End Get
        End Property
        
        ' ===== Theme-Driven Properties =====
        ' Colors read from current theme via ThemeManager
        
        ''' <summary>
        ''' Gets background color from current theme
        ''' </summary>
        Private ReadOnly Property pBackgroundColor() As String
            Get
                Try
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    Return If(lTheme?.BackgroundColor, "#1E1E1E")
                Catch ex As Exception
                    Return "#1E1E1E"
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Gets foreground color from current theme
        ''' </summary>
        Private ReadOnly Property pForegroundColor() As String
            Get
                Try
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    Return If(lTheme?.ForegroundColor, "#D4D4D4")
                Catch ex As Exception
                    Return "#D4D4D4"
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Gets line number background color from current theme
        ''' </summary>
        Private ReadOnly Property pLineNumberBgColor() As String
            Get
                Try
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    Return If(lTheme?.LineNumberBackgroundColor, "#2D2D30")
                Catch ex As Exception
                    Return "#2D2D30"
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Gets line number foreground color from current theme
        ''' </summary>
        Private ReadOnly Property pLineNumberFgColor() As String
            Get
                Try
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    Return If(lTheme?.LineNumberColor, "#858585")
                Catch ex As Exception
                    Return "#858585"
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Gets selection color from current theme
        ''' </summary>
        Private ReadOnly Property pSelectionColor() As String
            Get
                Try
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    Return If(lTheme?.SelectionColor, "#264F78")
                Catch ex As Exception
                    Return "#264F78"
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Gets cursor color from current theme
        ''' </summary>
        Private ReadOnly Property pCursorColor() As String
            Get
                Try
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    Return If(lTheme?.CursorColor, "#AEAFAD")
                Catch ex As Exception
                    Return "#AEAFAD"
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Gets current line background color from current theme
        ''' </summary>
        Private ReadOnly Property pCurrentLineBgColor() As String
            Get
                Try
                    Dim lTheme As EditorTheme = GetActiveTheme()
                    Return If(lTheme?.CurrentLineColor, "#2A2A2A")
                Catch ex As Exception
                    Return "#2A2A2A"
                End Try
            End Get
        End Property
        
        ' ===== IEditor Interface Implementation =====
        
        ''' <summary>
        ''' Applies a font description by updating the EditorFont setting
        ''' </summary>
        ''' <param name="vFontDescription">Font description string</param>
        ''' <remarks>
        ''' Updates both EditorFont and EditorZoomLevel settings
        ''' </remarks>
        Public Sub ApplyFont(vFontDescription As String)
            Try
                If String.IsNullOrEmpty(vFontDescription) Then Return
                
                If pSettingsManager IsNot Nothing Then
                    ' Update the EditorFont setting
                    pSettingsManager.EditorFont = vFontDescription
                    
                    ' Extract font size and update zoom level
                    Dim lFontDesc As Pango.FontDescription = Pango.FontDescription.FromString(vFontDescription)
                    If lFontDesc IsNot Nothing Then
                        Dim lSize As Integer
                        If lFontDesc.SizeIsAbsolute Then
                            lSize = lFontDesc.Size
                        Else
                            lSize = CInt(Math.Round(lFontDesc.Size / Pango.Scale.PangoScale))
                        End If
                        lFontDesc.Dispose()
                        
                        ' Update zoom level to match
                        If lSize >= 6 AndAlso lSize <= 72 Then
                            pSettingsManager.EditorZoomLevel = lSize
                        End If
                    End If
                    
                    Console.WriteLine($"ApplyFont: Updated settings with {vFontDescription}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyFont error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies the current theme from ThemeManager
        ''' </summary>
        ''' <remarks>
        ''' Theme colors are automatically applied via theme-driven properties
        ''' </remarks>
        Public Sub ApplyTheme() Implements IEditor.ApplyTheme
            Try
                ' Update syntax colors from current theme
                UpdateSyntaxColorsFromTheme()
                
                ' CRITICAL: Update line number widget theme immediately
                If pLineNumberWidget IsNot Nothing Then
                    Dim lCurrentTheme As EditorTheme = GetActiveTheme()
                    If lCurrentTheme IsNot Nothing Then
                        pLineNumberWidget.UpdateTheme(lCurrentTheme)
                        Console.WriteLine($"ApplyTheme: Updated LineNumberWidget with theme '{lCurrentTheme.Name}'")
                    End If
                End If
                
                ' Apply theme CSS to drawing area
                ApplyThemeToWidget()
                
                ' Force redraw of all components
                pLineNumberWidget?.QueueDraw()
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine("ApplyTheme: Theme applied To all components")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets tab width via SettingsManager
        ''' </summary>
        ''' <param name="vSpaces">Number of spaces per tab</param>
        Public Sub SetTabWidth(vSpaces As Integer)
            Try
                If pSettingsManager IsNot Nothing Then
                    pSettingsManager.TabWidth = Math.Max(1, Math.Min(16, vSpaces))
                End If
            Catch ex As Exception
                Console.WriteLine($"SetTabWidth error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets whether to use tabs via SettingsManager
        ''' </summary>
        ''' <param name="vUseTabs">True to use tabs, False to use spaces</param>
        Public Sub SetUseTabs(vUseTabs As Boolean) 
            Try
                If pSettingsManager IsNot Nothing Then
                    pSettingsManager.UseTabs = vUseTabs
                End If
            Catch ex As Exception
                Console.WriteLine($"SetUseTabs error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets whether to show line numbers via SettingsManager
        ''' </summary>
        ''' <param name="vShow">True to show line numbers</param>
        Public Sub SetShowLineNumbers(vShow As Boolean)
            Try
                If pSettingsManager IsNot Nothing Then
                    pSettingsManager.ShowLineNumbers = vShow
                End If
            Catch ex As Exception
                Console.WriteLine($"SetShowLineNumbers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets word wrap via SettingsManager
        ''' </summary>
        ''' <param name="vWrap">True to enable word wrap</param>
        Public Sub SetWordWrap(vWrap As Boolean)
            Try
                If pSettingsManager IsNot Nothing Then
                    pSettingsManager.WordWrap = vWrap
                End If
            Catch ex As Exception
                Console.WriteLine($"SetWordWrap error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== IEditor Property Implementations =====
        
        ''' <summary>
        ''' Gets or sets auto indent setting via SettingsManager
        ''' </summary>
        Public Property AutoIndent As Boolean
            Get
                Return pAutoIndent
            End Get
            Set(Value As Boolean)
                Try
                    If pSettingsManager IsNot Nothing Then
                        pSettingsManager.AutoIndent = Value
                    End If
                Catch ex As Exception
                    Console.WriteLine($"AutoIndent setter error: {ex.Message}")
                End Try
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets use tabs setting via SettingsManager
        ''' </summary>
        Public property UseTabs() as Boolean
            Get
                Return pUseTabs
            End Get
            Set(Value As Boolean)
                Try
                    If pSettingsManager IsNot Nothing Then
                        pSettingsManager.UseTabs = Value
                    End If
                Catch ex As Exception
                    Console.WriteLine($"UseTabs setter error: {ex.Message}")
                End Try
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets tab width via SettingsManager
        ''' </summary>
        Public Property TabWidth As Integer 
            Get
                Return pTabWidth
            End Get
            Set(Value As Integer)
                Try
                    If pSettingsManager IsNot Nothing Then
                        pSettingsManager.TabWidth = Math.Max(1, Math.Min(16, Value))
                    End If
                Catch ex As Exception
                    Console.WriteLine($"TabWidth setter error: {ex.Message}")
                End Try
            End Set
        End Property
        
        ' ===== Editor Capability Properties =====
        
        ''' <summary>
        ''' Gets whether this editor supports syntax highlighting
        ''' </summary>
        Public ReadOnly Property SupportsSyntaxHighlighting As Boolean Implements IEditor.SupportsSyntaxHighlighting
            Get
                Return True
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether this editor supports navigation
        ''' </summary>
        Public ReadOnly Property SupportsNavigation As Boolean Implements IEditor.SupportsNavigation
            Get
                Return True
            End Get
        End Property
        
        ' ===== Settings Event Handling =====
        
        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.OnSettingsZoomChanged
        ''' <summary>
        ''' Handles settings changes to refresh editor display
        ''' </summary>
        ''' <param name="vSettingName">Name of changed setting</param>
        ''' <param name="vOldValue">Previous value</param>
        ''' <param name="vNewValue">New value</param>
        ''' <remarks>
        ''' Called when SettingsManager raises SettingsChanged event
        ''' </remarks>
        Private Sub OnSettingsZoomChanged(vSettingName As String, vOldValue As Object, vNewValue As Object)
            Try
                Console.WriteLine($"OnSettingsZoomChanged: {vSettingName} = {vNewValue}")
                
                Select Case vSettingName
                    Case "EditorZoomLevel", "EditorFont"
                        ' Font or zoom changed - update metrics and redraw
                        Console.WriteLine($"Zoom/Font changed: Updating metrics and redrawing")
                        
                        ' CRITICAL: Force FontMetrics recreation with new font size
                        pFontMetrics = Nothing  ' Clear cached metrics to force recalculation
                        
                        ' Update font metrics with new size
                        UpdateFontMetrics()
                        
                        ' Update line number width
                        UpdateLineNumberWidth()
                        
                        ' Update scrollbars for new text size
                        UpdateScrollbars()
                        
                        ' CRITICAL: Force immediate redraw of all components
                        Application.Invoke(Sub()
                            pLineNumberWidget?.QueueDraw()
                            pDrawingArea?.QueueDraw()
                            
                            ' Force GTK to process the redraw immediately
                            While Application.EventsPending()
                                Application.RunIteration(False)
                            End While
                        End Sub)
                        
                    Case "TabWidth", "UseTabs"
                        ' Tab settings changed - redraw
                        Console.WriteLine($"Tab settings changed: Redrawing")
                        pDrawingArea?.QueueDraw()
                        
                    Case "ShowLineNumbers"
                        ' Line numbers visibility changed
                        Console.WriteLine($"Line numbers visibility changed: {vNewValue}")
                        UpdateLineNumberWidth()
                        pLineNumberWidget?.QueueDraw()
                        pDrawingArea?.QueueDraw()
                        
                    Case "WordWrap"
                        ' Word wrap changed - update scrollbars and redraw
                        Console.WriteLine($"Word wrap changed: {vNewValue}")
                        UpdateScrollbars()
                        pDrawingArea?.QueueDraw()
                        
                    Case "ShowWhitespace", "HighlightCurrentLine", "BraceMatching", "AutoIndent"
                        ' Visual settings changed - redraw
                        Console.WriteLine($"{vSettingName} changed: Redrawing")
                        pDrawingArea?.QueueDraw()
                        
                    Case "CurrentTheme"
                        ' Theme changed - update colors and redraw
                        Console.WriteLine($"Theme changed: Updating colors")
                        UpdateSyntaxColorsFromTheme()
                        
                        ' CRITICAL: Update LineNumberWidget theme
                        If pLineNumberWidget IsNot Nothing Then
                            Dim lTheme As EditorTheme = GetActiveTheme()
                            If lTheme IsNot Nothing Then
                                pLineNumberWidget.UpdateTheme(lTheme)
                            End If
                        End If
                        
                        ApplyThemeToWidget()
                        pLineNumberWidget?.QueueDraw()
                        pDrawingArea?.QueueDraw()
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"OnSettingsZoomChanged error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Zoom Helper Methods =====
        
        ''' <summary>
        ''' Gets the base font family from the editor font setting
        ''' </summary>
        ''' <returns>Font family name</returns>
        Private Function GetBaseFontFamilyFromSettings() As String
            Try
                If pSettingsManager IsNot Nothing Then
                    Dim lFontSetting As String = pSettingsManager.EditorFont
                    If Not String.IsNullOrEmpty(lFontSetting) Then
                        Dim lFontDesc As Pango.FontDescription = Pango.FontDescription.FromString(lFontSetting)
                        If lFontDesc IsNot Nothing Then
                            Dim lFamily As String = lFontDesc.Family
                            lFontDesc.Dispose()
                            If Not String.IsNullOrEmpty(lFamily) Then
                                Return lFamily
                            End If
                        End If
                    End If
                End If
                
                Return "Monospace"
                
            Catch ex As Exception
                Console.WriteLine($"GetBaseFontFamilyFromSettings error: {ex.Message}")
                Return "Monospace"
            End Try
        End Function
        
        ''' <summary>
        ''' Applies the current zoom level from SettingsManager to the editor
        ''' </summary>
        Private Sub ApplyCurrentZoomFromSettings()
            Try
                ' Font description property automatically uses current zoom level
                ' Just update metrics and redraw
                UpdateFontMetrics()
                UpdateLineNumberWidth()
                pDrawingArea?.QueueDraw()
                
                Console.WriteLine($"ApplyCurrentZoomFromSettings: Applied {pCurrentZoomLevel}pt")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyCurrentZoomFromSettings error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Theme Helper Methods =====
        
        ''' <summary>
        ''' Gets the currently active theme
        ''' </summary>
        ''' <returns>Current theme from ThemeManager or demo theme</returns>
        Private Function GetActiveTheme() As EditorTheme
            Try
                ' Check for demo theme first (for theme previews)
                If pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.IsDemoMode AndAlso pDemoTheme IsNot Nothing Then
                    Return pDemoTheme
                End If
                
                ' Get current theme from ThemeManager
                If pThemeManager IsNot Nothing Then
                    Return pThemeManager.GetCurrentThemeObject()
                End If
                
                ' Fallback to default theme
                Return New EditorTheme("Default")
                
            Catch ex As Exception
                Console.WriteLine($"GetActiveTheme error: {ex.Message}")
                Return New EditorTheme("Default")
            End Try
        End Function
        
        ''' <summary>
        ''' Updates syntax colors from current theme
        ''' </summary>
        Private Sub UpdateSyntaxColorsFromTheme()
            Try
                Dim lTheme As EditorTheme = GetActiveTheme()
                If lTheme IsNot Nothing AndAlso pSyntaxColorSet IsNot Nothing Then
                    pSyntaxColorSet.UpdateFromTheme(lTheme)
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateSyntaxColorsFromTheme error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies theme CSS to the drawing area widget
        ''' </summary>
        Private Sub ApplyThemeToWidget()
            Try
                If pDrawingArea IsNot Nothing Then
                    Dim lCss As String = $"drawingarea {{ background-color: {pBackgroundColor}; }}"
                    CssHelper.ApplyCssToWidget(pDrawingArea, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                End If
            Catch ex As Exception
                Console.WriteLine($"ApplyThemeToWidget error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Initialization Methods =====
        
        ''' <summary>
        ''' Initializes the SettingsManager connection and event handlers
        ''' </summary>
        ''' <param name="vSettingsManager">Settings manager instance</param>
        Private Sub InitializeSettingsManager(vSettingsManager As SettingsManager)
            Try
                ' Connect to settings changed event
                If vSettingsManager IsNot Nothing Then
                    AddHandler vSettingsManager.SettingsChanged, AddressOf OnSettingsZoomChanged
                    
                    ' Apply current settings
                    ApplyCurrentZoomFromSettings()
                    
                    Console.WriteLine("CustomDrawingEditor: SettingsManager initialized with centralized properties")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"InitializeSettingsManager error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Disconnects from SettingsManager events during disposal
        ''' </summary>
        Private Sub DisconnectSettingsManager()
            Try
                If pSettingsManager IsNot Nothing Then
                    RemoveHandler pSettingsManager.SettingsChanged, AddressOf OnSettingsZoomChanged
                    Console.WriteLine("CustomDrawingEditor: SettingsManager disconnected")
                End If
            Catch ex As Exception
                Console.WriteLine($"DisconnectSettingsManager error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Legacy Support Methods =====
        ' These maintain compatibility with existing code that expects the old interface
        
        ''' <summary>
        ''' Sets theme colors for demo/preview mode
        ''' </summary>
        ''' <param name="vTheme">Theme to use for preview</param>
        Public Sub SetThemeColors(vTheme As EditorTheme)
            Try
                If vTheme IsNot Nothing Then
                    ' Store as demo theme
                    pDemoTheme = vTheme
                    
                    ' Update syntax colors and redraw
                    UpdateSyntaxColorsFromTheme()
                    ApplyThemeToWidget()
                    pDrawingArea?.QueueDraw()
                    
                    Console.WriteLine($"SetThemeColors: Demo theme '{vTheme.Name}' applied")
                End If
            Catch ex As Exception
                Console.WriteLine($"SetThemeColors error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace