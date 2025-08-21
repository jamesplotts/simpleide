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
                pLineNumberArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyFont error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Apply theme from theme manager
        ''' </summary>
        Public Sub ApplyTheme() Implements IEditor.ApplyTheme
            Try
                If pThemeManager Is Nothing Then
                    Console.WriteLine("ApplyTheme: ThemeManager is Nothing")
                    Return
                End If
                
                Dim vTheme As EditorTheme = pThemeManager.GetCurrentThemeObject
        
                If vTheme Is Nothing Then Return
                
                ' Update background colors (using color properties from Drawing partial)
                pBackgroundColor = vTheme.BackgroundColor
                pForegroundColor = vTheme.ForegroundColor
                
                ' Update line number colors
                pLineNumberBgColor = vTheme.LineNumberBackgroundColor
                pLineNumberFgColor = vTheme.LineNumberColor
                
                ' Update selection and cursor colors
                pSelectionColor = vTheme.SelectionColor
                pCursorColor = vTheme.CursorColor
                pCurrentLineBgColor = vTheme.CurrentLineColor
                
                ' Set find highlight color - use selection color or a default
                If vTheme.SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eSelection) Then
                    pFindHighlightColor = vTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection)
                Else
                    pFindHighlightColor = vTheme.SelectionColor
                End If  
              
                ' Update syntax colors if available
                If pSyntaxColorSet IsNot Nothing AndAlso vTheme.SyntaxColors IsNot Nothing Then
                    UpdateSyntaxColorsFromTheme(vTheme)
                End If
                
                ' Apply theme to drawing area background
                ApplyThemeToWidget(vTheme)
                
                ' Queue full redraw
                pDrawingArea?.QueueDraw()
                pLineNumberArea?.QueueDraw()
                
                ' Raise event (event declared in main file)
                RaiseEvent ThemeChanged(vTheme)
                
            Catch ex As Exception
                Console.WriteLine($"ApplyTheme error: {ex.Message}")
            End Try
        End Sub
        
        ' Update syntax colors from theme
        Private Sub UpdateSyntaxColorsFromTheme(vTheme As EditorTheme)
            Try
                If vTheme.SyntaxColors Is Nothing Then Return
                
                For Each kvp In vTheme.SyntaxColors
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
                
                ' Re-parse to apply new colors
                ScheduleParse()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSyntaxColorsFromTheme error: {ex.Message}")
            End Try
        End Sub
        
        ' Apply theme colors to widget
        Private Sub ApplyThemeToWidget(vTheme As EditorTheme)
            Try
                ' Create CSS for the widget - Fixed: using underscore instead of hyphen
                Dim lCss As String = $"
                    .EditorWidget {{
                        background-color: {vTheme.BackgroundColor};
                        color: {vTheme.ForegroundColor};
                    }}
                "
                
                ' Apply CSS to drawing area
                If pDrawingArea IsNot Nothing Then
                    Dim lCssProvider As New CssProvider()
                    lCssProvider.LoadFromData(lCss)
                    pDrawingArea.StyleContext.AddProvider(lCssProvider, CUInt(StyleProviderPriority.Application))
                    pDrawingArea.StyleContext.AddClass("EditorWidget")
                End If
                
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
                pSyntaxColorSet = vSyntaxColorSet
                pSettingsManager = vSettingsManager
                
                ' Update syntax highlighter with new color set
                If pSyntaxHighlighter IsNot Nothing AndAlso vSyntaxColorSet IsNot Nothing Then
                    pSyntaxHighlighter = New VBSyntaxHighlighter(vSyntaxColorSet)
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
                    
                    ' Recalculate display if using spaces for tabs
                    If Not pUseTabs Then
                        ' Re-parse to update tab display
                        ScheduleParse()
                    End If
                    
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
                    
                    ' Re-parse to update tab display
                    ScheduleParse()
                    
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
                    
                    ' Update visibility
                    If pLineNumberArea IsNot Nothing Then
                        pLineNumberArea.Visible = vShow
                        
                        If vShow Then
                            pLineNumberArea.WidthRequest = pLineNumberWidth
                        Else
                            pLineNumberArea.WidthRequest = 0
                        End If
                    End If
                    
                    ' Queue redraw
                    pDrawingArea?.QueueDraw()
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
                        ' Update bracket matching (if method exists)
                        ' UpdateBracketMatching() ' Method needs to be implemented
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
                pLineNumberArea?.QueueDraw()
                
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
        
    End Class
    
End Namespace
