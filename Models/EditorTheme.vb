' Models/EditorTheme.vb - Editor theme management
Imports System
Imports System.Collections.Generic

Namespace Models
    Public Class EditorTheme
        ' Theme identification
        Public Property Name As String
        Public Property Description As String
        Public Property IsDarkTheme As Boolean
        
        ' Editor colors
        Public Property BackgroundColor As String
        Public Property ForegroundColor As String
        Public Property SelectionColor As String
        Public Property CurrentLineColor As String
        Public Property LineNumberColor As String
        Public Property LineNumberBackgroundColor As String
        Public Property CurrentLineNumberColor As String
        Public Property CursorColor As String

        ' Status colors for data grids and messages
        Public Property ErrorColor As String
        Public Property WarningColor As String  
        Public Property InfoColor As String
        Public Property SuccessColor As String
        
        ' Syntax colors
        Public Property SyntaxColors As New Dictionary(Of SyntaxColorSet.Tags, String)
        
        ' Font settings
        Public Property FontFamily As String
        Public Property FontSize As Integer
        
        ' Events
        Public Event ThemeChanged(vSender As Object, vE As EventArgs)

        Public Enum Tags
            ' Editor colors
            eBackgroundColor 
            eForegroundColor 
            eSelectionColor 
            eCurrentLineColor 
            eLineNumberColor 
            eLineNumberBackgroundColor 
            eCurrentLineNumberColor 
            eCursorColor 
            eErrorColor
            eWarningColor
            eInfoColor
            eSuccessColor
            eKeywordText
            eTypeText
            eStringText
            eCommentText
            eNumberText
            eOperatorText
            ePreprocessorText
            eIdentifierText
            eSelectionText
        End Enum

        
        Public Sub New()
            ' Initialize with default values
            SetDefaults()
        End Sub
        
        Public Sub New(vName As String)
            Me.New()
            Name = vName
        End Sub
        
        ' Replace: SimpleIDE.Models.EditorTheme.SetDefaults
        ''' <summary>
        ''' Sets default theme values
        ''' </summary>
        Public Sub SetDefaults()
            Name = "Default Dark"
            Description = "Default dark theme for SimpleIDE"
            IsDarkTheme = True
            
            ' Editor colors
            BackgroundColor = "#1E1E1E"
            ForegroundColor = "#FFFFFF"
            SelectionColor = "#99BBFF"
            CurrentLineColor = "#2A2A2A"
            LineNumberColor = "#858585"
            LineNumberBackgroundColor = "#252526"
            CurrentLineNumberColor = "#FFFFFF"
            CursorColor = "#AEAFAD"
            
            ' Status colors
            ErrorColor = "#FF6B6B"
            WarningColor = "#FFB86C"
            InfoColor = "#6272A4"
            SuccessColor = "#50FA7B"
            
            ' Font settings
            FontFamily = "monospace"
            FontSize = 10
            
            ' Syntax colors
            SyntaxColors.Clear()
            SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#569CD6"
            SyntaxColors(SyntaxColorSet.Tags.eType) = "#4EC9B0"
            SyntaxColors(SyntaxColorSet.Tags.eString) = "#CE9178"
            SyntaxColors(SyntaxColorSet.Tags.eComment) = "#6A9955"
            SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#B5CEA8"
            SyntaxColors(SyntaxColorSet.Tags.eOperator) = "#D4D4D4"
            SyntaxColors(SyntaxColorSet.Tags.ePreprocessor) = "#9B9B9B"
            SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#FFFFFF"
            SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#ADD6FF"
        End Sub
        
        ' Apply theme to SyntaxColorSet
        Public Sub ApplyToSyntaxColorSet(vColorSet As SyntaxColorSet)
            for each kvp in SyntaxColors
                vColorSet.SyntaxColor(kvp.key) = kvp.Value
            Next
        End Sub
        
        ' Load theme from SyntaxColorSet
        Public Sub LoadFromSyntaxColorSet(vColorSet As SyntaxColorSet)
            for lTag As SyntaxColorSet.Tags = SyntaxColorSet.Tags.eKeyword To SyntaxColorSet.Tags.eSelection
                SyntaxColors(lTag) = vColorSet.SyntaxColor(lTag)
            Next
        End Sub
        
        ' Get font string in GTK format
        Public Function GetFontString() As String
            Return $"{FontFamily} {FontSize}"
        End Function
        
        ' Set font from GTK format string
        Public Sub SetFontFromString(vFontString As String)
            Try
                Dim lParts() As String = vFontString.Split(" "c)
                If lParts.Length >= 2 Then
                    FontSize = Integer.Parse(lParts(lParts.Length - 1))
                    FontFamily = String.Join(" ", lParts.Take(lParts.Length - 1))
                End If
            Catch ex As Exception
                Console.WriteLine($"error parsing font string: {ex.Message}")
            End Try
        End Sub
        
        ' Clone theme
        ' Replace: SimpleIDE.Models.EditorTheme.Clone
        ''' <summary>
        ''' Creates a deep copy of the theme
        ''' </summary>
        Public Function Clone() As EditorTheme
            Dim lNewTheme As New EditorTheme(Name & " Copy")
            lNewTheme.Description = Description
            lNewTheme.IsDarkTheme = IsDarkTheme
            lNewTheme.BackgroundColor = BackgroundColor
            lNewTheme.ForegroundColor = ForegroundColor
            lNewTheme.SelectionColor = SelectionColor
            lNewTheme.CurrentLineColor = CurrentLineColor
            lNewTheme.LineNumberColor = LineNumberColor
            lNewTheme.LineNumberBackgroundColor = LineNumberBackgroundColor
            lNewTheme.CurrentLineNumberColor = CurrentLineNumberColor
            lNewTheme.CursorColor = CursorColor
            
            ' Copy status colors
            lNewTheme.ErrorColor = ErrorColor
            lNewTheme.WarningColor = WarningColor
            lNewTheme.InfoColor = InfoColor
            lNewTheme.SuccessColor = SuccessColor
            
            ' Copy font settings
            lNewTheme.FontFamily = FontFamily
            lNewTheme.FontSize = FontSize
            
            ' Deep copy syntax colors
            for each kvp in SyntaxColors
                lNewTheme.SyntaxColors(kvp.Key) = kvp.Value
            Next
            
            Return lNewTheme
        End Function
        
        ' Notify theme changes
        Protected Sub OnThemeChanged()
            RaiseEvent ThemeChanged(Me, EventArgs.Empty)
        End Sub
        
        ' Predefined themes
        ' Replace: SimpleIDE.Models.EditorTheme.GetBuiltInThemes
        ''' <summary>
        ''' Gets the built-in themes
        ''' </summary>
        Public Shared Function GetBuiltInThemes() As List(Of EditorTheme)
            Dim lThemes As New List(Of EditorTheme)
            
            ' Default Dark theme (already initialized with defaults)
            Dim lDefaultDark As New EditorTheme("Default Dark")
            lThemes.Add(lDefaultDark)
            
            ' VS Code Dark theme
            Dim lVSCodeTheme As New EditorTheme("VS Code Dark")
            lVSCodeTheme.Description = "Visual Studio Code dark theme"
            lVSCodeTheme.IsDarkTheme = True
            lVSCodeTheme.BackgroundColor = "#1E1E1E"
            lVSCodeTheme.ForegroundColor = "#D4D4D4"
            lVSCodeTheme.SelectionColor = "#264F78"
            lVSCodeTheme.CurrentLineColor = "#2A2A2A"
            lVSCodeTheme.LineNumberColor = "#858585"
            lVSCodeTheme.LineNumberBackgroundColor = "#1E1E1E"
            lVSCodeTheme.CurrentLineNumberColor = "#C6C6C6"
            lVSCodeTheme.CursorColor = "#AEAFAD"
            lVSCodeTheme.ErrorColor = "#F48771"
            lVSCodeTheme.WarningColor = "#CCA700"
            lVSCodeTheme.InfoColor = "#75BEFF"
            lVSCodeTheme.SuccessColor = "#89D185"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#569CD6"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eType) = "#4EC9B0"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eString) = "#CE9178"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#6A9955"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#B5CEA8"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#D4D4D4"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#ADD6FF"
            lThemes.Add(lVSCodeTheme)
            
            ' Light theme
            Dim lLightTheme As New EditorTheme("Light")
            lLightTheme.Description = "Light theme for SimpleIDE"
            lLightTheme.IsDarkTheme = False
            lLightTheme.BackgroundColor = "#FFFFFF"
            lLightTheme.ForegroundColor = "#000000"
            lLightTheme.SelectionColor = "#ADD6FF"
            lLightTheme.LineNumberColor = "#2B7489"
            lLightTheme.LineNumberBackgroundColor = "#F3F3F3"
            lLightTheme.CurrentLineNumberColor = "#0B216F"
            lLightTheme.CursorColor = "#000000"
            lLightTheme.ErrorColor = "#D32F2F"
            lLightTheme.WarningColor = "#F57C00"
            lLightTheme.InfoColor = "#1976D2"
            lLightTheme.SuccessColor = "#388E3C"
            lLightTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#0000FF"
            lLightTheme.SyntaxColors(SyntaxColorSet.Tags.eType) = "#2B91AF"
            lLightTheme.SyntaxColors(SyntaxColorSet.Tags.eString) = "#A31515"
            lLightTheme.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#008000"
            lLightTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#098658"
            lLightTheme.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#000000"
            lLightTheme.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#ADD6FF"
            lThemes.Add(lLightTheme)
            
            Return lThemes
        End Function

        Public ReadOnly Property StringColor(vTag As EditorTheme.Tags) As String
            Get
               Select Case vTag
                   Case EditorTheme.Tags.eBackgroundColor 
                       Return BackgroundColor
                   Case EditorTheme.Tags.eSelectionColor 
                       Return SelectionColor
                   Case EditorTheme.Tags.eCurrentLineColor 
                       Return CurrentLineColor
                   Case EditorTheme.Tags.eLineNumberColor 
                       Return LineNumberColor
                   Case EditorTheme.Tags.eLineNumberBackgroundColor 
                       Return LineNumberBackgroundColor
                   Case EditorTheme.Tags.eCurrentLineNumberColor 
                       Return CurrentLineNumberColor
                   Case EditorTheme.Tags.eCursorColor 
                       Return CursorColor
                   Case EditorTheme.Tags.eKeywordText
                       Return SyntaxColors(SyntaxColorSet.Tags.eKeyword)
                   Case EditorTheme.Tags.eTypeText
                       Return SyntaxColors(SyntaxColorSet.Tags.eType)
                   Case EditorTheme.Tags.eStringText
                       Return SyntaxColors(SyntaxColorSet.Tags.eString)
                   Case EditorTheme.Tags.eCommentText
                       Return SyntaxColors(SyntaxColorSet.Tags.eComment)
                   Case EditorTheme.Tags.eNumberText
                       Return SyntaxColors(SyntaxColorSet.Tags.eNumber)
                   Case EditorTheme.Tags.eOperatorText
                       Return SyntaxColors(SyntaxColorSet.Tags.eOperator)
                   Case EditorTheme.Tags.ePreprocessorText
                       Return SyntaxColors(SyntaxColorSet.Tags.ePreprocessor)
                   Case EditorTheme.Tags.eIdentifierText
                       Return SyntaxColors(SyntaxColorSet.Tags.eIdentifier)
                   Case EditorTheme.Tags.eSelectionText
                       Return SyntaxColors(SyntaxColorSet.Tags.eSelection)
                   Case Else
                       Return ForegroundColor
              End Select
           End Get
        End Property

        Public ReadOnly Property CairoColor(vTag As EditorTheme.Tags) As Cairo.Color
           Get
               Select Case vTag
                   Case EditorTheme.Tags.eBackgroundColor 
                       Return HexToCairoColor(BackgroundColor)
                   Case EditorTheme.Tags.eForegroundColor 
                       Return HexToCairoColor(ForegroundColor)
                   Case EditorTheme.Tags.eSelectionColor 
                       Return HexToCairoColor(SelectionColor)
                   Case EditorTheme.Tags.eCurrentLineColor 
                       Return HexToCairoColor(CurrentLineColor)
                   Case EditorTheme.Tags.eLineNumberColor 
                       Return HexToCairoColor(LineNumberColor)
                   Case EditorTheme.Tags.eLineNumberBackgroundColor 
                       Return HexToCairoColor(LineNumberBackgroundColor)
                   Case EditorTheme.Tags.eCurrentLineNumberColor 
                       Return HexToCairoColor(CurrentLineNumberColor)
                   Case EditorTheme.Tags.eCursorColor 
                       Return HexToCairoColor(CursorColor)
                   Case EditorTheme.Tags.eKeywordText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eKeyword))
                   Case EditorTheme.Tags.eTypeText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eType))
                   Case EditorTheme.Tags.eStringText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eString))
                   Case EditorTheme.Tags.eCommentText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eComment))
                   Case EditorTheme.Tags.eNumberText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eNumber))
                   Case EditorTheme.Tags.eOperatorText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eOperator))
                   Case EditorTheme.Tags.ePreprocessorText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.ePreprocessor))
                   Case EditorTheme.Tags.eIdentifierText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eIdentifier))
                   Case EditorTheme.Tags.eSelectionText
                       Return HexToCairoColor(SyntaxColors(SyntaxColorSet.Tags.eSelection))
              End Select
           End Get
        End Property
    
        Private Function HexToCairoColor(hex As String) As Cairo.Color
            ' Remove the '#' prefix
            hex = hex.TrimStart("#"c)
            
            ' Parse hex components
            Dim r As Byte = Convert.ToByte(hex.Substring(0, 2), 16)
            Dim g As Byte = Convert.ToByte(hex.Substring(2, 2), 16)
            Dim b As Byte = Convert.ToByte(hex.Substring(4, 2), 16)
            
            ' Convert to Cairo's [0.0, 1.0] range
            Return New Cairo.Color(r / 255.0, g / 255.0, b / 255.0)
        End Function

        ''' <summary>
        ''' Gets a color from the theme by tag
        ''' </summary>
        ''' <param name="vTag">The theme color tag to retrieve</param>
        ''' <returns>Hex color string (e.g., "#FF0000")</returns>
        Public Function GetColor(vTag As EditorTheme.Tags) As String
            Try
                Select Case vTag
                    Case EditorTheme.Tags.eBackgroundColor
                        Return BackgroundColor
                    Case EditorTheme.Tags.eForegroundColor
                        Return ForegroundColor
                    Case EditorTheme.Tags.eSelectionColor
                        Return SelectionColor
                    Case EditorTheme.Tags.eCurrentLineColor
                        Return CurrentLineColor
                    Case EditorTheme.Tags.eLineNumberColor
                        Return LineNumberColor
                    Case EditorTheme.Tags.eLineNumberBackgroundColor
                        Return LineNumberBackgroundColor
                    Case EditorTheme.Tags.eCurrentLineNumberColor
                        Return CurrentLineNumberColor
                    Case EditorTheme.Tags.eCursorColor
                        Return CursorColor
                    Case EditorTheme.Tags.eErrorColor
                        Return ErrorColor
                    Case EditorTheme.Tags.eWarningColor
                        Return WarningColor
                    Case EditorTheme.Tags.eInfoColor
                        Return InfoColor
                    Case EditorTheme.Tags.eSuccessColor
                        Return SuccessColor
                    Case EditorTheme.Tags.eKeywordText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eKeyword) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eKeyword)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.eTypeText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eType) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eType)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.eStringText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eString) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eString)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.eCommentText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eComment) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eComment)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.eNumberText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eNumber) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eNumber)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.eOperatorText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eOperator) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eOperator)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.ePreprocessorText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.ePreprocessor) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.ePreprocessor)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.eIdentifierText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eIdentifier) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eIdentifier)
                        Else
                            Return ForegroundColor ' Fallback
                        End If
                    Case EditorTheme.Tags.eSelectionText
                        If SyntaxColors.ContainsKey(SyntaxColorSet.Tags.eSelection) Then
                            Return SyntaxColors(SyntaxColorSet.Tags.eSelection)
                        Else
                            Return SelectionColor ' Fallback to regular selection color
                        End If
                    Case Else
                        Return ForegroundColor ' Default fallback
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"EditorTheme.GetColor error: {ex.Message}")
                Return "#000000" ' Fallback to black on error
            End Try
        End Function

    End Class

End Namespace
