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
            CurrentLineNumberColor = "#C6C6C6"
            CursorColor = "#FFFFFF"
            
            ' Syntax colors - matching existing defaults
            SyntaxColors.Clear()
            SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#d2b48c"
            SyntaxColors(SyntaxColorSet.Tags.eType) = "#2B91AF"
            SyntaxColors(SyntaxColorSet.Tags.eString) = "#5f9ea0"
            SyntaxColors(SyntaxColorSet.Tags.eComment) = "#008000"
            SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#5f9ea0"
            SyntaxColors(SyntaxColorSet.Tags.eOperator) = "#808080"
            SyntaxColors(SyntaxColorSet.Tags.ePreprocessor) = "#808080"
            SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#FFFFFF"
            SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#3399FF"
            
            ' Font settings
            FontFamily = "Monospace"
            FontSize = 10
        End Sub
        
        ' Apply theme to SyntaxColorSet
        Public Sub ApplyToSyntaxColorSet(vColorSet As SyntaxColorSet)
            For Each kvp In SyntaxColors
                vColorSet.SyntaxColor(kvp.key) = kvp.Value
            Next
        End Sub
        
        ' Load theme from SyntaxColorSet
        Public Sub LoadFromSyntaxColorSet(vColorSet As SyntaxColorSet)
            For lTag As SyntaxColorSet.Tags = SyntaxColorSet.Tags.eKeyword To SyntaxColorSet.Tags.eSelection
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
        Public Function Clone() As EditorTheme
            Dim lNewTheme As New EditorTheme()
            lNewTheme.Name = Me.Name & " (Copy)"
            lNewTheme.Description = Me.Description
            lNewTheme.IsDarkTheme = Me.IsDarkTheme
            
            ' Copy colors
            lNewTheme.BackgroundColor = Me.BackgroundColor
            lNewTheme.ForegroundColor = Me.ForegroundColor
            lNewTheme.SelectionColor = Me.SelectionColor
            lNewTheme.CurrentLineColor = Me.CurrentLineColor
            lNewTheme.LineNumberColor = Me.LineNumberColor
            lNewTheme.LineNumberBackgroundColor = Me.LineNumberBackgroundColor
            lNewTheme.CurrentLineNumberColor = Me.CurrentLineNumberColor
            lNewTheme.CursorColor = Me.CursorColor
            
            ' Copy syntax colors
            lNewTheme.SyntaxColors.Clear()
            For Each kvp In Me.SyntaxColors
                lNewTheme.SyntaxColors(kvp.key) = kvp.Value
            Next
            
            ' Copy font settings
            lNewTheme.FontFamily = Me.FontFamily
            lNewTheme.FontSize = Me.FontSize
            
            Return lNewTheme
        End Function
        
        ' Notify theme changes
        Protected Sub OnThemeChanged()
            RaiseEvent ThemeChanged(Me, EventArgs.Empty)
        End Sub
        
        ' Predefined themes
        Public Shared Function GetBuiltInThemes() As List(Of EditorTheme)
            Dim lThemes As New List(Of EditorTheme)
            
            ' Default Dark theme
            lThemes.Add(New EditorTheme("Default Dark"))
            
            ' VS Code Dark+ theme
            Dim lVSCodeTheme As New EditorTheme("VS code Dark+")
            lVSCodeTheme.Description = "Visual Studio code Dark+ theme"
            lVSCodeTheme.IsDarkTheme = True
            lVSCodeTheme.BackgroundColor = "#1E1E1E"
            lVSCodeTheme.ForegroundColor = "#D4D4D4"
            lVSCodeTheme.SelectionColor = "#6699FF"
            lVSCodeTheme.LineNumberColor = "#858585"
            lVSCodeTheme.LineNumberBackgroundColor = "#1E1E1E"
            lVSCodeTheme.CurrentLineNumberColor = "#C6C6C6"
            lVSCodeTheme.CursorColor = "#AEAFAD"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#569CD6"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eType) = "#4EC9B0"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eString) = "#CE9178"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#6A9955"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#B5CEA8"
            lVSCodeTheme.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#FFFFFF"
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

    End Class

End Namespace
