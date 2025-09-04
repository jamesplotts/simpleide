' Editors/CustomDrawingEditor.Settings.vb - Settings and configuration management (partial class)
Imports Gtk
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers
Imports SimpleIDE.Syntax

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Theme and Color Management =====
        
        ' Note: Color properties are declared in CustomDrawingEditor.vb main file
        
        ' ApplyFont - Implementation of IEditor.ApplyFont
        Public Sub ApplyFont(vFontDescription As String) Implements IEditor.ApplyFont
            Try
                If String.IsNullOrEmpty(vFontDescription) Then Return
                
                ' Parse font description
                pFontDescription = Pango.FontDescription.FromString(vFontDescription)
                
                ' Update font metrics
                UpdateFontMetrics()
                
                ' Update line number width
                UpdateLineNumberWidth()
                
                ' Queue full redraw
                pDrawingArea?.QueueDraw()

                
            Catch ex As Exception
                Console.WriteLine($"ApplyFont error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Applies the current theme from ThemeManager
        ''' </summary>
        Public Sub ApplyTheme() Implements IEditor.ApplyTheme
            Try
                If pThemeManager IsNot Nothing Then
                    Dim lCurrentTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                    If lCurrentTheme IsNot Nothing Then
                        ApplyThemeInternal(lCurrentTheme)
                    End If
                End If
            Catch ex As Exception
                Console.WriteLine($"ApplyTheme (parameterless) error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows the context menu for the line number area
        ''' </summary>
        ''' <param name="vX">X coordinate for menu position</param>
        ''' <param name="vY">Y coordinate for menu position</param>
        Public Sub ShowLineNumberContextMenu(vX As Integer, vY As Integer)
            Try
                If pLineNumberContextMenu IsNot Nothing Then
                    pLineNumberContextMenu.PopupAtPointer(Nothing)
                End If
            Catch ex As Exception
                Console.WriteLine($"ShowLineNumberContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ' Update syntax colors from theme
        Private Sub UpdateSyntaxColorsFromTheme(vTheme As EditorTheme)
            Try
                If vTheme.SyntaxColors Is Nothing Then Return
                
                for each kvp in vTheme.SyntaxColors
                    Select Case kvp.Key
                        Case SyntaxColorSet.Tags.eKeyword
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eKeyword) = kvp.Value
                        Case SyntaxColorSet.Tags.eString
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eString) = kvp.Value
                        Case SyntaxColorSet.Tags.eComment
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eComment) = kvp.Value
                        Case SyntaxColorSet.Tags.eNumber
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eNumber) = kvp.Value
                        Case SyntaxColorSet.Tags.eOperator
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eOperator) = kvp.Value
                        Case SyntaxColorSet.Tags.eType
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eType) = kvp.Value
                        Case SyntaxColorSet.Tags.ePreprocessor
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.ePreprocessor) = kvp.Value
                        Case SyntaxColorSet.Tags.eIdentifier
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eIdentifier) = kvp.Value
                        Case SyntaxColorSet.Tags.eSelection
                            pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eSelection) = kvp.Value
                    End Select
                Next
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSyntaxColorsFromTheme error: {ex.Message}")
            End Try
        End Sub
        
        ' Apply theme colors to widget
        Private Sub ApplyThemeToWidget(vTheme As EditorTheme)
            Try
                ' Create CSS for immediate background color update
                Dim lCss As String = $"
                    drawingarea {{
                        background-color: {vTheme.BackgroundColor};
                    }}
                "
                
                ' Apply CSS to drawing area for immediate effect
                If pDrawingArea IsNot Nothing Then
                    CssHelper.ApplyCssToWidget(pDrawingArea, lCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                End If
                
                Console.WriteLine($"ApplyThemeToWidget: Applied immediate background color")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyThemeToWidget error: {ex.Message}")
            End Try
        End Sub
        
        ' Clear all syntax highlighting
        Private Sub ClearSyntaxHighlighting()
            Try
                ' Reset all character colors to default
                for i As Integer = 0 To pLineCount - 1
                    If pCharacterColors IsNot Nothing AndAlso i < pCharacterColors.Length Then
                        pCharacterColors(i) = New CharacterColorInfo() {}
                    End If
                    pLineMetadata(i).MarkChanged()
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ClearSyntaxHighlighting error: {ex.Message}")
            End Try
        End Sub
        
        ' Set editor dependencies (called by factory or main window)
        Public Sub SetDependencies(vSyntaxColorSet As SyntaxColorSet, vSettingsManager As SettingsManager)
            Try
                ' Store the settings manager
                pSettingsManager = vSettingsManager
                
                ' Only use the provided SyntaxColorSet if we don't have theme colors
                If pSyntaxColorSet Is Nothing Then
                    If vSyntaxColorSet IsNot Nothing Then
                        pSyntaxColorSet = vSyntaxColorSet
                        Console.WriteLine("SetDependencies: Using provided SyntaxColorSet")
                    Else
                        ' Create a default one if nothing provided
                        pSyntaxColorSet = New SyntaxColorSet()
                        Console.WriteLine("SetDependencies: Created default SyntaxColorSet")
                    End If
                    
                Else
                    Console.WriteLine("SetDependencies: Keeping theme-based SyntaxColorSet")
                End If        
	        
                ' Apply current settings
                If vSettingsManager IsNot Nothing Then
                    ApplySettingsFromManager()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetDependencies error: {ex.Message}")
            End Try
        End Sub
        
        ' Apply settings from settings manager
        Private Sub ApplySettingsFromManager()
            Try
                If pSettingsManager Is Nothing Then Return
                
                ' Apply editor settings
                SetTabWidth(pSettingsManager.TabWidth)
                SetUseTabs(pSettingsManager.UseTabs)
                SetShowLineNumbers(pSettingsManager.ShowLineNumbers)
                SetWordWrap(pSettingsManager.WordWrap)
                SetAutoIndent(pSettingsManager.AutoIndent)
                SetHighlightCurrentLine(pSettingsManager.HighlightCurrentLine)
                SetShowWhitespace(pSettingsManager.ShowWhitespace)
                ' Note: BracketHighlighting property doesn't exist in SettingsManager yet
                ' SetBracketHighlighting(pSettingsManager.BracketHighlighting)
                
                ' Apply font if available
                Dim lFontDesc As String = pSettingsManager.EditorFont
                If Not String.IsNullOrEmpty(lFontDesc) Then
                    SetFont(lFontDesc)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplySettingsFromManager error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Editor Settings =====
        
        ' Set tab width
        Public Sub SetTabWidth(vSpaces As Integer) Implements IEditor.SetTabWidth
            Try
                ' Validate input
                vSpaces = Math.Max(1, Math.Min(16, vSpaces))
                
                If pTabWidth <> vSpaces Then
                    pTabWidth = vSpaces
                    
                    ' Update indent size to match
                    pIndentSize = vSpaces
                    
                   
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetTabWidth error: {ex.Message}")
            End Try
        End Sub
        
        ' Set whether to use tabs or spaces
        Public Sub SetUseTabs(vUseTabs As Boolean) Implements IEditor.SetUseTabs
            Try
                If pUseTabs <> vUseTabs Then
                    pUseTabs = vUseTabs
                    
                   
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetUseTabs error: {ex.Message}")
            End Try
        End Sub
        
        ' Set whether to show line numbers
        Public Sub SetShowLineNumbers(vShow As Boolean) Implements IEditor.SetShowLineNumbers
            Try
                If pShowLineNumbers <> vShow Then
                    pShowLineNumbers = vShow

                    If pLineNumberWidget IsNot Nothing Then
                        pLineNumberWidget.QueueDraw()
                    End If
                    If pDrawingArea IsNot Nothing Then
                        pDrawingArea.QueueDraw()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetShowLineNumbers error: {ex.Message}")
            End Try
        End Sub
        
        ' Set word wrap
        Public Sub SetWordWrap(vWrap As Boolean) Implements IEditor.SetWordWrap
            Try
                If pWordWrap <> vWrap Then
                    pWordWrap = vWrap
                    
                    ' Update scrollbars
                    UpdateScrollbars()
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetWordWrap error: {ex.Message}")
            End Try
        End Sub
        
        ' Set auto-indent (internal method, not part of IEditor interface)
        Public Sub SetAutoIndent(vAutoIndent As Boolean)
            Try
                pAutoIndent = vAutoIndent
                
            Catch ex As Exception
                Console.WriteLine($"SetAutoIndent error: {ex.Message}")
            End Try
        End Sub
        
        ' Set highlight current line (internal method, not part of IEditor interface)
        Public Sub SetHighlightCurrentLine(vHighlight As Boolean)
            Try
                If pHighlightCurrentLine <> vHighlight Then
                    pHighlightCurrentLine = vHighlight
                    
                    ' Queue redraw of current line
                    InvalidateLine(pCursorLine)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetHighlightCurrentLine error: {ex.Message}")
            End Try
        End Sub
        
        ' Set show whitespace (internal method, not part of IEditor interface)
        Public Sub SetShowWhitespace(vShow As Boolean)
            Try
                If pShowWhitespace <> vShow Then
                    pShowWhitespace = vShow
                    
                    ' Queue full redraw
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetShowWhitespace error: {ex.Message}")
            End Try
        End Sub
        
        ' Set bracket highlighting (internal method, not part of IEditor interface)
        Private Sub SetBracketHighlighting(vEnabled As Boolean)
            Try
                If pBracketHighlightingEnabled <> vEnabled Then
                    pBracketHighlightingEnabled = vEnabled
                    
                    If Not vEnabled Then
                        ' Clear existing highlights
                        pMatchingBracketLine = -1
                        pMatchingBracketColumn = -1
                    Else
                        ' Update bracket matching 
                        UpdateBracketMatching() 
                    End If
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetBracketHighlighting error: {ex.Message}")
            End Try
        End Sub
        
        ' Set font (internal method, not part of IEditor interface)
        Private Sub SetFont(vFontDesc As String)
            Try
                If String.IsNullOrEmpty(vFontDesc) Then Return
                
                ' Parse font description
                pFontDescription = Pango.FontDescription.FromString(vFontDesc)
                
                ' Update font metrics
                UpdateFontMetrics()
                
                ' Update line number width
                UpdateLineNumberWidth()
                
                ' Queue full redraw
                pDrawingArea?.QueueDraw()

                
            Catch ex As Exception
                Console.WriteLine($"SetFont error: {ex.Message}")
            End Try
        End Sub
        
        ' Get current settings as dictionary (internal method, not part of IEditor interface)
        Private Function GetSettings() As Dictionary(Of String, Object)
            Try
                Dim lSettings As New Dictionary(Of String, Object)
                
                lSettings("TabWidth") = pTabWidth
                lSettings("UseTabs") = pUseTabs
                lSettings("ShowLineNumbers") = pShowLineNumbers
                lSettings("WordWrap") = pWordWrap
                lSettings("AutoIndent") = pAutoIndent
                lSettings("HighlightCurrentLine") = pHighlightCurrentLine
                lSettings("ShowWhitespace") = pShowWhitespace
                lSettings("BracketHighlighting") = pBracketHighlightingEnabled
                lSettings("Font") = pFontDescription?.ToString()
                
                Return lSettings
                
            Catch ex As Exception
                Console.WriteLine($"GetSettings error: {ex.Message}")
                Return New Dictionary(Of String, Object)
            End Try
        End Function
        
        ' Apply settings from dictionary 
        Public Sub ApplySettings(vSettings As Dictionary(Of String, Object))
            Try
                If vSettings Is Nothing Then Return
                
                If vSettings.ContainsKey("TabWidth") Then
                    SetTabWidth(Convert.ToInt32(vSettings("TabWidth")))
                End If
                
                If vSettings.ContainsKey("UseTabs") Then
                    SetUseTabs(Convert.ToBoolean(vSettings("UseTabs")))
                End If
                
                If vSettings.ContainsKey("ShowLineNumbers") Then
                    SetShowLineNumbers(Convert.ToBoolean(vSettings("ShowLineNumbers")))
                End If
                
                If vSettings.ContainsKey("WordWrap") Then
                    SetWordWrap(Convert.ToBoolean(vSettings("WordWrap")))
                End If
                
                If vSettings.ContainsKey("AutoIndent") Then
                    SetAutoIndent(Convert.ToBoolean(vSettings("AutoIndent")))
                End If
                
                If vSettings.ContainsKey("HighlightCurrentLine") Then
                    SetHighlightCurrentLine(Convert.ToBoolean(vSettings("HighlightCurrentLine")))
                End If
                
                If vSettings.ContainsKey("ShowWhitespace") Then
                    SetShowWhitespace(Convert.ToBoolean(vSettings("ShowWhitespace")))
                End If
                
                If vSettings.ContainsKey("BracketHighlighting") Then
                    SetBracketHighlighting(Convert.ToBoolean(vSettings("BracketHighlighting")))
                End If
                
                If vSettings.ContainsKey("Font") Then
                    Dim lFont As String = vSettings("Font")?.ToString()
                    If Not String.IsNullOrEmpty(lFont) Then
                        SetFont(lFont)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplySettings error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets theme colors without using ThemeManager
        ''' </summary>
        Public Sub SetThemeColors(vTheme As EditorTheme)
            Try
                If vTheme Is Nothing Then Return
                
                Console.WriteLine($"SetThemeColors called with theme: {vTheme.Name}")
                
                ' CRITICAL: Store the theme as the demo theme for use in drawing
                pDemoTheme = vTheme
                
                ' Update background colors
                pBackgroundColor = vTheme.BackgroundColor
                pForegroundColor = vTheme.ForegroundColor
                
                ' Update line number colors
                pLineNumberBgColor = vTheme.LineNumberBackgroundColor
                pLineNumberFgColor = vTheme.LineNumberColor
                
                ' Update selection and cursor colors
                pSelectionColor = vTheme.SelectionColor
                pCursorColor = vTheme.CursorColor
                pCurrentLineBgColor = vTheme.CurrentLineColor
                
                ' Set find highlight color
                If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eSelection) Then
                    pFindHighlightColor = vTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection)
                Else
                    pFindHighlightColor = vTheme.SelectionColor
                End If
                
                ' Update syntax colors if available
                If pSyntaxColorSet IsNot Nothing AndAlso vTheme.SyntaxColors IsNot Nothing Then
                    for each kvp in vTheme.SyntaxColors
                        Select Case kvp.Key
                            Case SyntaxColorSet.Tags.eKeyword
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eKeyword) = kvp.Value
                            Case SyntaxColorSet.Tags.eString
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eString) = kvp.Value
                            Case SyntaxColorSet.Tags.eComment
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eComment) = kvp.Value
                            Case SyntaxColorSet.Tags.eNumber
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eNumber) = kvp.Value
                            Case SyntaxColorSet.Tags.eOperator
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eOperator) = kvp.Value
                            Case SyntaxColorSet.Tags.eType
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eType) = kvp.Value
                            Case SyntaxColorSet.Tags.ePreprocessor
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.ePreprocessor) = kvp.Value
                            Case SyntaxColorSet.Tags.eIdentifier
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eIdentifier) = kvp.Value
                            Case SyntaxColorSet.Tags.eSelection
                                pSyntaxColorSet.SyntaxColor(SyntaxColorSet.Tags.eSelection) = kvp.Value
                        End Select
                    Next
                    
                End If
                
                ' Apply theme to drawing area background (CSS)
                ApplyThemeToWidget(vTheme)
                
                ' Queue full redraw to ensure everything updates
                pDrawingArea?.QueueDraw()

                
                Console.WriteLine($"SetThemeColors completed for theme: {vTheme.Name}")
                
            Catch ex As Exception
                Console.WriteLine($"SetThemeColors error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Missing IEditor properties =====
        
        ' SupportsSyntaxHighlighting property
        Public ReadOnly Property SupportsSyntaxHighlighting As Boolean Implements IEditor.SupportsSyntaxHighlighting
            Get
                Return True  ' CustomDrawingEditor supports syntax highlighting
            End Get
        End Property
        
        ' SupportsNavigation property
        Public ReadOnly Property SupportsNavigation As Boolean Implements IEditor.SupportsNavigation
            Get
                Return True  ' CustomDrawingEditor supports navigation
            End Get
        End Property

        ''' <summary>
        ''' Gets the currently active theme, considering demo mode
        ''' </summary>
        ''' <returns>The demo theme if in demo mode, otherwise the current theme from ThemeManager</returns>
        Private Function GetActiveTheme() As EditorTheme
            Try
                ' Check if we're in demo mode and have a demo theme
                If pSourceFileInfo IsNot Nothing AndAlso pSourceFileInfo.IsDemoMode AndAlso pDemoTheme IsNot Nothing Then
                    Return pDemoTheme
                End If
                
                ' Otherwise use the theme manager's current theme
                If pThemeManager IsNot Nothing Then
                    Return pThemeManager.GetCurrentThemeObject()
                End If
                
                ' Fallback - create a default theme
                Return New EditorTheme("Default")
                
            Catch ex As Exception
                Console.WriteLine($"GetActiveTheme error: {ex.Message}")
                Return New EditorTheme("Default")
            End Try
        End Function

        ''' <summary>
        ''' Requests asynchronous recoloring of syntax highlighting
        ''' </summary>
        ''' <remarks>
        ''' This is called after theme changes to update syntax colors without blocking the UI
        ''' </remarks>
        Private Sub RequestAsyncRecoloring()
            Try
                ' If we have a ProjectManager and SourceFileInfo, request recoloring through it
                If pProjectManager IsNot Nothing AndAlso pSourceFileInfo IsNot Nothing Then
                    Console.WriteLine("RequestAsyncRecoloring: Requesting color update from ProjectManager")
                    pProjectManager.UpdateFileColors(pSourceFileInfo)
                Else
                    ' Fallback to local recoloring
                    Console.WriteLine("RequestAsyncRecoloring: Using local ForceRecolorization")
                    ForceRecolorization()
                End If
            Catch ex As Exception
                Console.WriteLine($"RequestAsyncRecoloring error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
